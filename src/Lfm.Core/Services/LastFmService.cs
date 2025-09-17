using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace Lfm.Core.Services;

/// <summary>
/// Service layer implementation for Last.fm business logic operations.
/// Encapsulates complex business operations and abstracts them from CLI command implementation details.
/// </summary>
public class LastFmService : ILastFmService
{
    private readonly ILastFmApiClient _apiClient;
    private readonly IConfigurationManager _configManager;
    private readonly ITagFilterService _tagFilterService;
    private readonly ILogger<LastFmService> _logger;

    public LastFmService(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ITagFilterService tagFilterService,
        ILogger<LastFmService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _tagFilterService = tagFilterService ?? throw new ArgumentNullException(nameof(tagFilterService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Basic user content retrieval
    public async Task<TopArtists?> GetUserTopArtistsAsync(string username, string period, int limit = 10, int page = 1)
    {
        return await _apiClient.GetTopArtistsAsync(username, period, limit, page);
    }

    public async Task<TopTracks?> GetUserTopTracksAsync(string username, string period, int limit = 10, int page = 1)
    {
        return await _apiClient.GetTopTracksAsync(username, period, limit, page);
    }

    public async Task<TopAlbums?> GetUserTopAlbumsAsync(string username, string period, int limit = 10, int page = 1)
    {
        return await _apiClient.GetTopAlbumsAsync(username, period, limit, page);
    }

    // Date range variants
    public async Task<TopArtists?> GetUserTopArtistsForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        return await _apiClient.GetTopArtistsForDateRangeAsync(username, from, to, limit);
    }

    public async Task<TopTracks?> GetUserTopTracksForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        return await _apiClient.GetTopTracksForDateRangeAsync(username, from, to, limit);
    }

    public async Task<TopAlbums?> GetUserTopAlbumsForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        return await _apiClient.GetTopAlbumsForDateRangeAsync(username, from, to, limit);
    }

    // Artist-specific content
    public async Task<TopTracks?> GetArtistTopTracksAsync(string artistName, int limit = 10)
    {
        return await _apiClient.GetArtistTopTracksAsync(artistName, limit);
    }

    public async Task<TopAlbums?> GetArtistTopAlbumsAsync(string artistName, int limit = 10)
    {
        return await _apiClient.GetArtistTopAlbumsAsync(artistName, limit);
    }

    public async Task<SimilarArtists?> GetSimilarArtistsAsync(string artistName, int limit = 50)
    {
        return await _apiClient.GetSimilarArtistsAsync(artistName, limit);
    }

    // Range queries - complex operations that span multiple pages
    public async Task<(List<Artist> items, string totalCount)> GetUserTopArtistsRangeAsync(string username, string period, int startIndex, int endIndex)
    {
        return await ExecuteRangeQueryAsync<Artist, TopArtists>(
            username,
            period,
            startIndex,
            endIndex,
            (user, per, limit, page) => _apiClient.GetTopArtistsAsync(user, per, limit, page),
            response => response.Artists,
            response => response.Attributes.Total);
    }

    public async Task<(List<Track> items, string totalCount)> GetUserTopTracksRangeAsync(string username, string period, int startIndex, int endIndex)
    {
        return await ExecuteRangeQueryAsync<Track, TopTracks>(
            username,
            period,
            startIndex,
            endIndex,
            (user, per, limit, page) => _apiClient.GetTopTracksAsync(user, per, limit, page),
            response => response.Tracks,
            response => response.Attributes.Total);
    }

    public async Task<(List<Album> items, string totalCount)> GetUserTopAlbumsRangeAsync(string username, string period, int startIndex, int endIndex)
    {
        return await ExecuteRangeQueryAsync<Album, TopAlbums>(
            username,
            period,
            startIndex,
            endIndex,
            (user, per, limit, page) => _apiClient.GetTopAlbumsAsync(user, per, limit, page),
            response => response.Albums,
            response => response.Attributes.Total);
    }

    // Deep search operations - search through user's entire history
    public async Task<List<Track>> SearchUserTracksForArtistAsync(string username, string artistName, int limit, int maxDepth = int.MaxValue, CancellationToken cancellationToken = default)
    {
        var foundTracks = new List<Track>();
        var page = 1;
        var processedItems = 0;

        while (foundTracks.Count < limit && processedItems < maxDepth && !cancellationToken.IsCancellationRequested)
        {
            // Apply throttling between API calls (except for first page)
            if (page > 1)
            {
                // Throttling now handled by CachedLastFmApiClient
            }

            var result = await _apiClient.GetTopTracksAsync(username, "overall", SearchConstants.Api.MaxItemsPerPage, page);
            
            if (result?.Tracks == null || !result.Tracks.Any())
                break;

            var matchingTracks = result.Tracks
                .Where(track => string.Equals(track.Artist.Name, artistName, StringComparison.OrdinalIgnoreCase))
                .Take(limit - foundTracks.Count)
                .ToList();

            foundTracks.AddRange(matchingTracks);
            processedItems += result.Tracks.Count;
            page++;
        }

        return foundTracks.Take(limit).ToList();
    }

    public async Task<List<Album>> SearchUserAlbumsForArtistAsync(string username, string artistName, int limit, int maxDepth = int.MaxValue, CancellationToken cancellationToken = default)
    {
        var foundAlbums = new List<Album>();
        var page = 1;
        var processedItems = 0;

        while (foundAlbums.Count < limit && processedItems < maxDepth && !cancellationToken.IsCancellationRequested)
        {
            // Apply throttling between API calls (except for first page)
            if (page > 1)
            {
                // Throttling now handled by CachedLastFmApiClient
            }

            var result = await _apiClient.GetTopAlbumsAsync(username, "overall", SearchConstants.Api.MaxItemsPerPage, page);
            
            if (result?.Albums == null || !result.Albums.Any())
                break;

            var matchingAlbums = result.Albums
                .Where(album => string.Equals(album.Artist.Name, artistName, StringComparison.OrdinalIgnoreCase))
                .Take(limit - foundAlbums.Count)
                .ToList();

            foundAlbums.AddRange(matchingAlbums);
            processedItems += result.Albums.Count;
            page++;
        }

        return foundAlbums.Take(limit).ToList();
    }

    // Advanced features
    public async Task<List<RecommendationResult>> GetMusicRecommendationsAsync(string username,
        int analysisLimit = 20,
        int recommendationLimit = 20,
        int filterThreshold = 0,
        int tracksPerArtist = 0,
        string period = "overall",
        bool excludeTags = false)
    {
        // Step 1: Get user's top artists for analysis
        var topArtists = await _apiClient.GetTopArtistsAsync(username, period, analysisLimit);
        if (topArtists?.Artists == null || !topArtists.Artists.Any())
        {
            return new List<RecommendationResult>();
        }

        // Step 2: Build user's play count map for filtering
        var userPlayCounts = await GetUserArtistPlayCountsAsync(username);

        // Step 3: Get similar artists for each top artist (sequential with throttling)
        var recommendations = new Dictionary<string, RecommendationData>();

        for (int i = 0; i < topArtists.Artists.Count; i++)
        {
            var topArtist = topArtists.Artists[i];

            try
            {
                // Apply throttling between API calls (except for first call)
                if (i > 0)
                {
                    // Throttling now handled by CachedLastFmApiClient
                }

                var similar = await _apiClient.GetSimilarArtistsAsync(topArtist.Name);

                if (similar?.Artists != null)
                {
                    foreach (var similarArtist in similar.Artists)
                    {
                        if (!float.TryParse(similarArtist.Match, out var matchScore))
                            matchScore = 0;

                        if (recommendations.TryGetValue(similarArtist.Name, out var existing))
                        {
                            existing.TotalSimilarity += matchScore;
                            existing.OccurrenceCount++;
                            existing.SourceArtists.Add(topArtist.Name);
                        }
                        else
                        {
                            recommendations[similarArtist.Name] = new RecommendationData
                            {
                                Artist = similarArtist,
                                TotalSimilarity = matchScore,
                                OccurrenceCount = 1,
                                SourceArtists = new List<string> { topArtist.Name }
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get similar artists for {Artist}", topArtist.Name);
            }
        }

        // Step 4: Filter by play count and create initial recommendations
        var initialRecommendations = recommendations.Values
            .Where(r =>
            {
                if (userPlayCounts.TryGetValue(r.Artist.Name, out var playCount))
                {
                    return playCount < filterThreshold;
                }
                return true; // Include if we don't have data (assume new artist)
            })
            .Select(r => new RecommendationResult
            {
                ArtistName = r.Artist.Name,
                Score = (r.TotalSimilarity / r.OccurrenceCount) * r.OccurrenceCount,
                AverageSimilarity = r.TotalSimilarity / r.OccurrenceCount,
                OccurrenceCount = r.OccurrenceCount,
                UserPlayCount = userPlayCounts.GetValueOrDefault(r.Artist.Name, 0),
                SourceArtists = r.SourceArtists
            })
            .OrderByDescending(r => r.Score)
            .ToList();

        // Step 5: Apply tag filtering if requested via CLI flag or enabled in config
        var filteredRecommendations = initialRecommendations;
        var config = await _configManager.LoadAsync();

        // Apply filtering if:
        // 1. --exclude-tags CLI flag is used, OR
        // 2. EnableTagFiltering is true in config (automatic filtering)
        if ((excludeTags || config.EnableTagFiltering) && config.ExcludedTags.Any())
        {
            filteredRecommendations = await ApplyDynamicTagFilteringAsync(
                initialRecommendations, config, recommendationLimit);
        }

        // Take final limit
        filteredRecommendations = filteredRecommendations.Take(recommendationLimit).ToList();

        // Step 5: Fetch top tracks if requested (sequential with throttling)
        if (tracksPerArtist > 0)
        {
            for (int i = 0; i < filteredRecommendations.Count; i++)
            {
                var rec = filteredRecommendations[i];
                
                try
                {
                    // Apply throttling between API calls (except for first call)
                    if (i > 0)
                    {
                        // Throttling now handled by CachedLastFmApiClient
                    }

                    var tracks = await _apiClient.GetArtistTopTracksAsync(rec.ArtistName, tracksPerArtist);
                    if (tracks?.Tracks != null)
                    {
                        rec.TopTracks = tracks.Tracks;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get tracks for {Artist}", rec.ArtistName);
                }
            }
        }

        return filteredRecommendations;
    }

    public async Task<List<RecommendationResult>> GetMusicRecommendationsForDateRangeAsync(string username,
        DateTime from,
        DateTime to,
        int analysisLimit = 20,
        int recommendationLimit = 20,
        int filterThreshold = 0,
        int tracksPerArtist = 0,
        bool excludeTags = false)
    {
        // Step 1: Get user's top artists for the date range
        var topArtists = await _apiClient.GetTopArtistsForDateRangeAsync(username, from, to, analysisLimit);
        if (topArtists?.Artists == null || !topArtists.Artists.Any())
        {
            return new List<RecommendationResult>();
        }

        // Step 2: Build user's play count map for filtering (use overall data for filtering)
        var userPlayCounts = await GetUserArtistPlayCountsAsync(username);

        // Step 3: Get similar artists for each top artist (sequential with throttling)
        var recommendations = new Dictionary<string, RecommendationData>();

        for (int i = 0; i < topArtists.Artists.Count; i++)
        {
            var topArtist = topArtists.Artists[i];

            try
            {
                // Apply throttling between API calls (except for first call)
                if (i > 0)
                {
                    // Throttling now handled by CachedLastFmApiClient
                }

                var similar = await _apiClient.GetSimilarArtistsAsync(topArtist.Name);

                if (similar?.Artists != null)
                {
                    foreach (var similarArtist in similar.Artists)
                    {
                        if (!float.TryParse(similarArtist.Match, out var matchScore))
                            matchScore = 0;

                        if (recommendations.TryGetValue(similarArtist.Name, out var existing))
                        {
                            existing.TotalSimilarity += matchScore;
                            existing.OccurrenceCount++;
                            existing.SourceArtists.Add(topArtist.Name);
                        }
                        else
                        {
                            recommendations[similarArtist.Name] = new RecommendationData
                            {
                                Artist = similarArtist,
                                TotalSimilarity = matchScore,
                                OccurrenceCount = 1,
                                SourceArtists = new List<string> { topArtist.Name }
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get similar artists for {Artist}", topArtist.Name);
            }
        }

        // Step 4: Filter by play count and create initial recommendations
        var initialRecommendations = recommendations.Values
            .Where(r =>
            {
                if (userPlayCounts.TryGetValue(r.Artist.Name, out var playCount))
                {
                    return playCount < filterThreshold;
                }
                return true; // Include if we don't have data (assume new artist)
            })
            .Select(r => new RecommendationResult
            {
                ArtistName = r.Artist.Name,
                Score = (r.TotalSimilarity / r.OccurrenceCount) * r.OccurrenceCount,
                AverageSimilarity = r.TotalSimilarity / r.OccurrenceCount,
                OccurrenceCount = r.OccurrenceCount,
                UserPlayCount = userPlayCounts.GetValueOrDefault(r.Artist.Name, 0),
                SourceArtists = r.SourceArtists
            })
            .OrderByDescending(r => r.Score)
            .ToList();

        // Step 5: Apply tag filtering if requested via CLI flag or enabled in config
        var filteredRecommendations = initialRecommendations;
        var config = await _configManager.LoadAsync();

        // Apply filtering if:
        // 1. --exclude-tags CLI flag is used, OR
        // 2. EnableTagFiltering is true in config (automatic filtering)
        if ((excludeTags || config.EnableTagFiltering) && config.ExcludedTags.Any())
        {
            filteredRecommendations = await ApplyDynamicTagFilteringAsync(
                initialRecommendations, config, recommendationLimit);
        }

        // Take final limit
        filteredRecommendations = filteredRecommendations.Take(recommendationLimit).ToList();

        // Step 5: Fetch top tracks if requested (sequential with throttling)
        if (tracksPerArtist > 0)
        {
            for (int i = 0; i < filteredRecommendations.Count; i++)
            {
                var rec = filteredRecommendations[i];
                
                try
                {
                    // Apply throttling between API calls (except for first call)
                    if (i > 0)
                    {
                        // Throttling now handled by CachedLastFmApiClient
                    }

                    var tracks = await _apiClient.GetArtistTopTracksAsync(rec.ArtistName, tracksPerArtist);
                    if (tracks?.Tracks != null)
                    {
                        rec.TopTracks = tracks.Tracks;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get tracks for {Artist}", rec.ArtistName);
                }
            }
        }

        return filteredRecommendations;
    }

    public async Task<Dictionary<string, int>> GetUserArtistPlayCountsAsync(string username, int maxArtists = int.MaxValue)
    {
        var userPlayCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        const int pageSize = 1000;
        var page = 1;
        var processedArtists = 0;

        while (processedArtists < maxArtists)
        {
            // Apply throttling between API calls (except for first page)
            if (page > 1)
            {
                // Throttling now handled by CachedLastFmApiClient
            }

            var result = await _apiClient.GetTopArtistsAsync(username, "overall", pageSize, page);
            
            if (result?.Artists == null || !result.Artists.Any())
                break;

            foreach (var artist in result.Artists.Take(maxArtists - processedArtists))
            {
                if (int.TryParse(artist.PlayCount, out var playCount))
                {
                    userPlayCounts[artist.Name] = playCount;
                }
            }

            processedArtists += result.Artists.Count;
            
            var totalPages = int.TryParse(result.Attributes.TotalPages, out var tp) ? tp : 1;
            if (page >= totalPages)
                break;
                
            page++;
        }

        return userPlayCounts;
    }

    // New Result-based methods for gradual migration
    public async Task<Result<TopArtists>> GetUserTopArtistsWithResultAsync(string username, string period, int limit = 10, int page = 1)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(username))
                return Result<TopArtists>.ValidationError("Username cannot be empty");
            
            if (string.IsNullOrWhiteSpace(period))
                return Result<TopArtists>.ValidationError("Period cannot be empty");
            
            if (limit <= 0 || limit > 1000)
                return Result<TopArtists>.ValidationError("Limit must be between 1 and 1000");

            var result = await _apiClient.GetTopArtistsAsync(username, period, limit, page);
            
            if (result == null)
                return Result<TopArtists>.DataError("No artist data returned from Last.fm");
            
            if (result.Artists == null || !result.Artists.Any())
                return Result<TopArtists>.DataError($"No artists found for user '{username}' in period '{period}'");

            return Result<TopArtists>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top artists for user {Username}", username);
            return Result<TopArtists>.ApiError("Failed to retrieve artist data", ex.Message);
        }
    }

    public async Task<Result<List<RecommendationResult>>> GetMusicRecommendationsWithResultAsync(string username,
        int analysisLimit = 20,
        int recommendationLimit = 20,
        int filterThreshold = 0,
        int tracksPerArtist = 0,
        string period = "overall",
        bool excludeTags = false)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(username))
                return Result<List<RecommendationResult>>.ValidationError("Username cannot be empty");
            
            if (analysisLimit <= 0 || analysisLimit > 200)
                return Result<List<RecommendationResult>>.ValidationError("Analysis limit must be between 1 and 200");
            
            if (recommendationLimit <= 0 || recommendationLimit > 100)
                return Result<List<RecommendationResult>>.ValidationError("Recommendation limit must be between 1 and 100");

            // Call the existing implementation
            var recommendations = await GetMusicRecommendationsAsync(username, analysisLimit, recommendationLimit, filterThreshold, tracksPerArtist, period, excludeTags);
            
            if (!recommendations.Any())
                return Result<List<RecommendationResult>>.DataError($"No recommendations found for user '{username}' with current filter settings");

            return Result<List<RecommendationResult>>.Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating recommendations for user {Username}", username);
            return Result<List<RecommendationResult>>.ApiError("Failed to generate recommendations", ex.Message);
        }
    }

    public async Task<Result> ValidateUserConfigurationAsync(string? username = null)
    {
        try
        {
            var config = await _configManager.LoadAsync();
            
            // Check API key
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                return Result.ConfigurationError(
                    "Last.fm API key is not configured", 
                    "Use 'lfm config set api-key YOUR_KEY' to set your API key");
            }
            
            // Check username (either provided or default)
            var effectiveUsername = username ?? config.DefaultUsername;
            if (string.IsNullOrEmpty(effectiveUsername))
            {
                return Result.ConfigurationError(
                    "No username specified and no default username configured",
                    "Provide --user parameter or use 'lfm config set username YOUR_USERNAME'");
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating user configuration");
            return Result.ConfigurationError("Failed to validate configuration", ex.Message);
        }
    }

    // Private helper methods
    private async Task<(List<T> items, string totalCount)> ExecuteRangeQueryAsync<T, TResponse>(
        string username,
        string period,
        int startIndex,
        int endIndex,
        Func<string, string, int, int, Task<TResponse?>> apiCall,
        Func<TResponse, List<T>> extractItems,
        Func<TResponse, string> extractTotal)
        where TResponse : class
    {
        var allItems = new List<T>();
        int rangeSize = endIndex - startIndex + 1;
        string totalCount = "0";
        
        // Calculate which pages we need
        int startPage = ((startIndex - 1) / SearchConstants.Api.MaxItemsPerPage) + 1;
        int endPage = ((endIndex - 1) / SearchConstants.Api.MaxItemsPerPage) + 1;
        
        for (int page = startPage; page <= endPage && allItems.Count < rangeSize; page++)
        {
            // Apply throttling between API calls (except for first page)
            if (page > startPage)
            {
                // Throttling now handled by CachedLastFmApiClient
            }

            var pageResult = await apiCall(username, period, SearchConstants.Api.MaxItemsPerPage, page);
            
            if (pageResult == null)
                break;
            
            var pageItems = extractItems(pageResult);
            if (pageItems == null || !pageItems.Any())
                break;
            
            totalCount = extractTotal(pageResult);
            
            // Calculate which items from this page we need
            int pageStartPosition = (page - 1) * SearchConstants.Api.MaxItemsPerPage + 1;
            
            int takeStartIndex = Math.Max(0, startIndex - pageStartPosition);
            int takeEndIndex = Math.Min(SearchConstants.Api.MaxItemsPerPage - 1, endIndex - pageStartPosition);
            int takeCount = takeEndIndex - takeStartIndex + 1;
            
            if (takeCount > 0)
            {
                var pageSelection = pageItems
                    .Skip(takeStartIndex)
                    .Take(takeCount)
                    .ToList();
                
                allItems.AddRange(pageSelection);
            }
        }
        
        var rangeItems = allItems.Take(rangeSize).ToList();
        return (rangeItems, totalCount);
    }

    /// <summary>
    /// Applies tag filtering with dynamic candidate expansion to ensure we get the requested number of recommendations
    /// </summary>
    private async Task<List<RecommendationResult>> ApplyDynamicTagFilteringAsync(
        List<RecommendationResult> allCandidates,
        LfmConfig config,
        int targetLimit)
    {
        var multiplier = 2;
        const int maxMultiplier = 10; // Safety limit to prevent infinite expansion
        const int minCandidatesPerMultiplier = 20; // Minimum candidates to try per iteration

        _logger.LogDebug("Starting dynamic tag filtering for {TargetLimit} recommendations from {TotalCandidates} candidates",
            targetLimit, allCandidates.Count);

        while (multiplier <= maxMultiplier)
        {
            // Calculate how many candidates to check this iteration
            var candidateCount = Math.Max(
                targetLimit * multiplier,
                minCandidatesPerMultiplier);

            // Don't exceed available candidates
            candidateCount = Math.Min(candidateCount, allCandidates.Count);

            var candidatesForFiltering = allCandidates.Take(candidateCount).ToList();

            _logger.LogDebug("Attempt {Multiplier}: Checking {CandidateCount} candidates (multiplier: {MultiplierValue}x)",
                multiplier / 2 + 1, candidateCount, multiplier);

            // Create a temporary config with increased API budget for this attempt
            var tempConfig = new LfmConfig
            {
                ExcludedTags = config.ExcludedTags,
                TagFilterThreshold = config.TagFilterThreshold,
                EnableTagFiltering = config.EnableTagFiltering,
                MaxTagLookups = Math.Max(config.MaxTagLookups, candidateCount / 2) // Increase budget based on candidates
            };

            var filteredResults = await ApplyTagFilteringAsync(candidatesForFiltering, tempConfig, targetLimit);

            // If we got enough results, we're done
            if (filteredResults.Count >= targetLimit)
            {
                _logger.LogInformation("Dynamic filtering successful: Got {ResultCount} results after checking {CandidateCount} candidates",
                    filteredResults.Count, candidateCount);
                return filteredResults.Take(targetLimit).ToList();
            }

            // If we've checked all available candidates, break
            if (candidateCount >= allCandidates.Count)
            {
                _logger.LogWarning("Checked all {TotalCandidates} candidates but only found {ResultCount} after filtering. Adding unfiltered candidates to reach target.",
                    allCandidates.Count, filteredResults.Count);

                // Add unfiltered candidates to reach target
                var remainingNeeded = targetLimit - filteredResults.Count;
                var filteredArtistNames = filteredResults.Select(r => r.ArtistName).ToHashSet();
                var additionalCandidates = allCandidates
                    .Where(r => !filteredArtistNames.Contains(r.ArtistName))
                    .Take(remainingNeeded)
                    .ToList();

                filteredResults.AddRange(additionalCandidates);

                _logger.LogInformation("Final result: {FilteredCount} filtered + {UnfilteredCount} unfiltered = {TotalCount} recommendations",
                    filteredResults.Count - additionalCandidates.Count, additionalCandidates.Count, filteredResults.Count);

                return filteredResults.Take(targetLimit).ToList();
            }

            // Increase multiplier for next iteration
            multiplier += 2; // Try 2x, 4x, 6x, 8x, 10x
        }

        // Safety fallback - this shouldn't normally be reached
        _logger.LogWarning("Dynamic filtering reached maximum multiplier ({MaxMultiplier}x). Falling back to unfiltered results.",
            maxMultiplier);

        return allCandidates.Take(targetLimit).ToList();
    }

    private async Task<List<RecommendationResult>> ApplyTagFilteringAsync(
        List<RecommendationResult> candidateRecommendations,
        LfmConfig config,
        int targetLimit)
    {
        var filteredResults = new List<RecommendationResult>();
        var excludedResults = new List<(RecommendationResult recommendation, List<string> matchingTags)>();
        int apiCallsUsed = 0;
        int candidatesToCheck = Math.Min(candidateRecommendations.Count, targetLimit * 2); // N*2 as specified

        _logger.LogDebug("Starting tag filtering: checking {CandidateCount} candidates, budget: {MaxApiCalls}",
            candidatesToCheck, config.MaxTagLookups);

        for (int i = 0; i < candidatesToCheck && apiCallsUsed < config.MaxTagLookups; i++)
        {
            var candidate = candidateRecommendations[i];

            try
            {
                // Apply throttling between API calls
                if (apiCallsUsed > 0)
                {
                    // Throttling now handled by CachedLastFmApiClient
                }

                var artistTags = await _apiClient.GetArtistTopTagsAsync(candidate.ArtistName, autocorrect: true);
                apiCallsUsed++;

                if (_tagFilterService.ShouldExcludeArtist(artistTags, config))
                {
                    // Find which tags caused the exclusion for verbose output
                    var matchingTags = new List<string>();
                    if (artistTags?.Tags != null)
                    {
                        matchingTags = artistTags.Tags
                            .Where(tag => tag.Count >= config.TagFilterThreshold &&
                                         config.ExcludedTags.Any(excludedTag =>
                                             string.Equals(excludedTag, tag.Name, StringComparison.OrdinalIgnoreCase)))
                            .Select(tag => $"{tag.Name}: {tag.Count}")
                            .ToList();
                    }

                    excludedResults.Add((candidate, matchingTags));
                    _logger.LogDebug("Excluded {ArtistName} due to tags: {MatchingTags}",
                        candidate.ArtistName, string.Join(", ", matchingTags));
                }
                else
                {
                    filteredResults.Add(candidate);

                    // If we have enough results, we can stop early
                    if (filteredResults.Count >= targetLimit)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get tags for {ArtistName}, including in results", candidate.ArtistName);
                // On error, include the artist (benefit of the doubt)
                filteredResults.Add(candidate);
            }
        }

        // If we need more results and haven't hit API limit, add remaining unfiltered candidates
        if (filteredResults.Count < targetLimit && apiCallsUsed < config.MaxTagLookups)
        {
            var remainingNeeded = targetLimit - filteredResults.Count;
            var uncheckedCandidates = candidateRecommendations
                .Skip(candidatesToCheck)
                .Take(remainingNeeded)
                .ToList();

            filteredResults.AddRange(uncheckedCandidates);

            if (uncheckedCandidates.Any())
            {
                _logger.LogDebug("Added {UncheckedCount} unfiltered candidates to reach target limit", uncheckedCandidates.Count);
            }
        }

        // Log filtering statistics
        _logger.LogInformation("Tag filtering complete: kept {KeptCount}, excluded {ExcludedCount}, API calls: {ApiCalls}/{Budget}",
            filteredResults.Count, excludedResults.Count, apiCallsUsed, config.MaxTagLookups);

        // Log verbose details about excluded artists
        if (excludedResults.Any())
        {
            var excludedArtists = string.Join(", ", excludedResults.Select(r => r.recommendation.ArtistName));
            _logger.LogInformation("Excluded artists by tag filtering: {Artists}", excludedArtists);
        }

        return filteredResults.Take(targetLimit).ToList();
    }

    // Helper class for recommendation processing
    private class RecommendationData
    {
        public SimilarArtist Artist { get; set; } = new();
        public float TotalSimilarity { get; set; }
        public int OccurrenceCount { get; set; }
        public List<string> SourceArtists { get; set; } = new();
    }
}