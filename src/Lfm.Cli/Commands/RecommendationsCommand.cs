using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Lfm.Cli.Commands;

public class RecommendationsCommand : BaseCommand
{
    private readonly IDisplayService _displayService;

    public RecommendationsCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        IDisplayService displayService,
        ILogger<RecommendationsCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
    }

    public async Task ExecuteAsync(
        int limit, 
        string period, 
        string? username, 
        string? range = null, 
        int? delayMs = null, 
        bool verbose = false, 
        bool timing = false, 
        bool forceCache = false, 
        bool forceApi = false, 
        bool noCache = false, 
        bool timer = false,
        int recommendationLimit = 20,
        int filter = 0)
    {
        await ExecuteWithErrorHandlingAndTimerAsync("recommendations command", async () =>
        {
            // Configure cache behavior and timing
            ConfigureCaching(timing, forceCache, forceApi, noCache);

            if (!await ValidateApiKeyAsync())
                return;

            var user = await GetUsernameAsync(username);
            if (user == null)
                return;

            // Step 1: Get user's top artists (reusing artists command logic)
            List<Artist> topArtists;
            int totalArtists;
            
            if (!string.IsNullOrEmpty(range))
            {
                var (isValid, startIndex, endIndex, errorMessage) = ParseRange(range);
                if (!isValid)
                {
                    Console.WriteLine(errorMessage);
                    return;
                }
                
                var (rangeArtists, totalCount) = await ExecuteRangeQueryAsync<Artist, TopArtists>(
                    startIndex,
                    endIndex,
                    _apiClient.GetTopArtistsAsync,
                    response => response.Artists,
                    response => response.Attributes.Total,
                    "artists",
                    user,
                    period,
                    delayMs,
                    verbose);
                
                if (!rangeArtists.Any())
                {
                    Console.WriteLine(ErrorMessages.Format(ErrorMessages.NoItemsInRange, "artists"));
                    return;
                }
                
                topArtists = rangeArtists;
                totalArtists = int.TryParse(totalCount, out var tc) ? tc : rangeArtists.Count;
            }
            else
            {
                if (verbose)
                {
                    Console.WriteLine($"Getting top {limit} artists for {user} ({period})...");
                }

                var result = await _apiClient.GetTopArtistsAsync(user, period, limit);

                if (result?.Artists == null || !result.Artists.Any())
                {
                    Console.WriteLine(ErrorMessages.NoArtistsFound);
                    return;
                }

                topArtists = result.Artists;
                totalArtists = int.TryParse(result.Attributes.Total, out var t) ? t : topArtists.Count;
            }

            if (verbose)
            {
                Console.WriteLine($"\nAnalyzing {topArtists.Count} top artists for recommendations...");
            }

            // Step 2: Build user's play count map from overall top artists
            var userPlayCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            
            if (verbose)
            {
                Console.WriteLine($"\nFetching your complete artist library for filtering (filter={filter})...");
            }

            // Fetch user's overall top artists with high limit for filtering
            const int pageSize = 1000;
            var page = 1;
            var hasMore = true;
            
            while (hasMore)
            {
                var overallResult = await _apiClient.GetTopArtistsAsync(user, "overall", pageSize, page);
                
                if (overallResult?.Artists == null || !overallResult.Artists.Any())
                    break;
                
                foreach (var artist in overallResult.Artists)
                {
                    if (int.TryParse(artist.PlayCount, out var playCount))
                    {
                        userPlayCounts[artist.Name] = playCount;
                    }
                }
                
                var totalPages = int.TryParse(overallResult.Attributes.TotalPages, out var tp) ? tp : 1;
                hasMore = page < totalPages;
                page++;
                
                if (verbose && hasMore)
                {
                    Console.WriteLine($"  Loaded page {page - 1} of {totalPages} ({userPlayCounts.Count} artists so far)");
                }
            }
            
            if (verbose)
            {
                Console.WriteLine($"  Loaded {userPlayCounts.Count} artists from your library");
            }

            // Step 3: Get similar artists for each top artist
            var recommendations = new ConcurrentDictionary<string, RecommendationScore>();
            var tasks = new List<Task>();
            
            foreach (var topArtist in topArtists)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var similar = await _apiClient.GetSimilarArtistsAsync(topArtist.Name);
                        
                        if (similar?.Artists != null)
                        {
                            foreach (var similarArtist in similar.Artists)
                            {
                                // Parse match score (0-1 float)
                                if (!float.TryParse(similarArtist.Match, out var matchScore))
                                    matchScore = 0;
                                
                                recommendations.AddOrUpdate(
                                    similarArtist.Name,
                                    new RecommendationScore 
                                    { 
                                        Artist = similarArtist,
                                        Score = matchScore,
                                        OccurrenceCount = 1,
                                        SimilarTo = new List<string> { topArtist.Name }
                                    },
                                    (key, existing) =>
                                    {
                                        existing.Score += matchScore;
                                        existing.OccurrenceCount++;
                                        existing.SimilarTo.Add(topArtist.Name);
                                        return existing;
                                    });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get similar artists for {Artist}", topArtist.Name);
                    }
                }));
            }
            
            await Task.WhenAll(tasks);
            
            // Step 4: Filter and score recommendations
            var filteredRecommendations = recommendations.Values
                .Where(r => 
                {
                    // Check if we have play count data for this artist
                    if (userPlayCounts.TryGetValue(r.Artist.Name, out var playCount))
                    {
                        // Filter out if play count >= filter threshold
                        return playCount < filter;
                    }
                    // If we don't have data, include it (assume new artist)
                    return true;
                })
                .Select(r => 
                {
                    // Final score = average match score * occurrence count
                    r.FinalScore = (r.Score / r.OccurrenceCount) * r.OccurrenceCount;
                    return r;
                })
                .OrderByDescending(r => r.FinalScore)
                .Take(recommendationLimit)
                .ToList();
            
            // Step 5: Display results
            if (!filteredRecommendations.Any())
            {
                Console.WriteLine($"\nNo new recommendations found with filter >= {filter} plays.");
                Console.WriteLine("Try lowering the filter value or analyzing a different time period.");
                return;
            }
            
            Console.WriteLine($"\n🎵 Top {filteredRecommendations.Count} Recommendations (filter: >= {filter} plays)\n");
            
            var index = 1;
            foreach (var rec in filteredRecommendations)
            {
                var playCount = userPlayCounts.TryGetValue(rec.Artist.Name, out var pc) ? pc : 0;
                var avgMatch = rec.Score / rec.OccurrenceCount;
                
                Console.WriteLine($"{index,3}. {rec.Artist.Name}");
                Console.WriteLine($"     Match: {avgMatch:P0} | Similar to {rec.OccurrenceCount} of your top artists");
                Console.WriteLine($"     Your plays: {playCount} | URL: {rec.Artist.Url}");
                
                if (verbose && rec.SimilarTo.Count <= 5)
                {
                    Console.WriteLine($"     Similar to: {string.Join(", ", rec.SimilarTo)}");
                }
                
                Console.WriteLine();
                index++;
            }
            
            if (verbose)
            {
                var totalFound = recommendations.Count;
                var filtered = totalFound - filteredRecommendations.Count;
                Console.WriteLine($"Statistics: {totalFound} unique artists found, {filtered} filtered out, {filteredRecommendations.Count} recommended");
            }
        }, timer);
    }
    
    private class RecommendationScore
    {
        public SimilarArtist Artist { get; set; } = new();
        public float Score { get; set; }
        public int OccurrenceCount { get; set; }
        public List<string> SimilarTo { get; set; } = new();
        public float FinalScore { get; set; }
    }
}