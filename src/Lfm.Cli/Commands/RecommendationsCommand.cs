using Lfm.Cli.Services;
using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Models.Results;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

public class RecommendationsCommand : BaseCommand
{
    private readonly ILastFmService _lastFmService;
    private readonly IDisplayService _displayService;
    private readonly ISpotifyStreamingService _spotifyStreamingService;

    public RecommendationsCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILastFmService lastFmService,
        IDisplayService displayService,
        ISpotifyStreamingService spotifyStreamingService,
        ILogger<RecommendationsCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _lastFmService = lastFmService ?? throw new ArgumentNullException(nameof(lastFmService));
        _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
        _spotifyStreamingService = spotifyStreamingService ?? throw new ArgumentNullException(nameof(spotifyStreamingService));
    }

    public async Task ExecuteAsync(
        int limit,
        string? period,
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
        int filter = 0,
        int tracksPerArtist = 0,
        string? from = null,
        string? to = null,
        string? year = null,
        bool excludeTags = false,
        bool playNow = false,
        string? playlist = null,
        bool shuffle = false,
        string? device = null)
    {
        await ExecuteWithErrorHandlingAndTimerAsync("recommendations command", async () =>
        {
            // Configure cache behavior and timing
            ConfigureCaching(timing, forceCache, forceApi, noCache);

            // If Spotify streaming is requested and tracksPerArtist is still 0 (default), set it to 1
            if ((playNow || !string.IsNullOrEmpty(playlist)) && tracksPerArtist == 0)
            {
                tracksPerArtist = 1;
                if (verbose)
                {
                    Console.WriteLine("Setting tracks-per-artist to 1 for Spotify streaming...");
                }
            }

            if (!await ValidateApiKeyAsync())
                return;

            var user = await GetUsernameAsync(username);
            if (user == null)
                return;

            // Validate limit parameter
            ValidateLimit(limit);

            // Resolve period parameters (--period, --from/--to, or --year)
            var (isDateRange, resolvedPeriod, fromDate, toDate) = ResolvePeriodParameters(period, from, to, year);

            // Determine display period format for consistent messaging
            var displayPeriod = isDateRange && fromDate.HasValue && toDate.HasValue 
                ? DateRangeParser.FormatDateRange(fromDate.Value, toDate.Value)
                : resolvedPeriod;

            // Handle range queries differently - need to get specific range of artists first
            int analysisLimit = limit;
            if (!string.IsNullOrEmpty(range))
            {
                if (!ValidateAndHandleRange(range, _displayService, out var startIndex, out var endIndex))
                {
                    return;
                }
                
                if (verbose)
                {
                    Console.WriteLine($"Getting artists {startIndex}-{endIndex} for {user} ({displayPeriod}) for analysis...");
                }

                // Use service layer for range query
                var (rangeArtists, totalCount) = await _lastFmService.GetUserTopArtistsRangeAsync(user, resolvedPeriod, startIndex, endIndex);
                
                if (!rangeArtists.Any())
                {
                    _displayService.DisplayError(ErrorMessages.Format(ErrorMessages.NoItemsInRange, "artists"));
                    return;
                }
                
                analysisLimit = rangeArtists.Count;
            }

            // Use standardized display service for operation start
            _displayService.DisplayOperationStart("recommendations", user, displayPeriod, analysisLimit, verbose: verbose);

            // Use service layer for recommendations - this replaces 200+ lines of complex logic
            List<RecommendationResult> recommendations;
            
            if (isDateRange && fromDate.HasValue && toDate.HasValue)
            {
                // Use date range specific method for accurate year/date filtering
                recommendations = await _lastFmService.GetMusicRecommendationsForDateRangeAsync(
                    username: user,
                    from: fromDate.Value,
                    to: toDate.Value,
                    analysisLimit: analysisLimit,
                    recommendationLimit: recommendationLimit,
                    filterThreshold: filter,
                    tracksPerArtist: tracksPerArtist,
                    excludeTags: excludeTags);
            }
            else
            {
                // Use standard period-based method
                recommendations = await _lastFmService.GetMusicRecommendationsAsync(
                    username: user,
                    analysisLimit: analysisLimit,
                    recommendationLimit: recommendationLimit,
                    filterThreshold: filter,
                    tracksPerArtist: tracksPerArtist,
                    period: resolvedPeriod,
                    excludeTags: excludeTags);
            }

            // Display results
            if (!recommendations.Any())
            {
                _displayService.DisplayError($"\nNo new recommendations found with filter >= {filter} plays.");
                _displayService.DisplayError("Try lowering the filter value or analyzing a different time period.");
                return;
            }

            // Use centralized recommendations display
            _displayService.DisplayRecommendations(recommendations, filter, tracksPerArtist, verbose, _symbols);

            // Stream to Spotify if requested
            if ((playNow || !string.IsNullOrEmpty(playlist)) && recommendations.Any())
            {
                var playlistTitle = "Music Recommendations";
                if (!string.IsNullOrEmpty(resolvedPeriod))
                    playlistTitle += $" (based on {resolvedPeriod})";
                else if (isDateRange)
                    playlistTitle += $" (based on {from} to {to})";

                await _spotifyStreamingService.StreamRecommendationsAsync(recommendations, playNow, playlist, playlistTitle, shuffle, device);
            }

        }, timer);
    }
}