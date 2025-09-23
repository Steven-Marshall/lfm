using Lfm.Cli.Services;
using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

public class TracksCommand : BaseCommand
{
    private readonly ILastFmService _lastFmService;
    private readonly IDisplayService _displayService;
    private readonly ISpotifyStreamingService _spotifyStreamingService;

    public TracksCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILastFmService lastFmService,
        IDisplayService displayService,
        ISpotifyStreamingService spotifyStreamingService,
        ILogger<TracksCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _lastFmService = lastFmService ?? throw new ArgumentNullException(nameof(lastFmService));
        _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
        _spotifyStreamingService = spotifyStreamingService ?? throw new ArgumentNullException(nameof(spotifyStreamingService));
    }

    public async Task ExecuteAsync(int limit, string? period, string? username, string? artist, string? range = null, int? delayMs = null, bool verbose = false, bool timing = false, bool forceCache = false, bool forceApi = false, bool noCache = false, bool timer = false, string? from = null, string? to = null, string? year = null, bool playNow = false, string? playlist = null, bool shuffle = false, string? device = null)
    {
        await ExecuteWithErrorHandlingAndTimerAsync("tracks command", async () =>
        {
            // Configure cache behavior and timing
            ConfigureCaching(timing, forceCache, forceApi, noCache);

            if (!await ValidateApiKeyAsync())
                return;

            // Validate limit parameter
            ValidateLimit(limit);

            // Handle artist-specific tracks (global Last.fm data)
            if (!string.IsNullOrEmpty(artist))
            {
                // Validate artist parameter
                ValidateArtist(artist);

                if (!string.IsNullOrEmpty(range))
                {
                    Console.WriteLine(ErrorMessages.RangeNotSupportedWithArtist);
                    return;
                }

                if (verbose)
                {
                    Console.WriteLine($"Getting top {limit} tracks for artist: {artist}...\n");
                }
                
                // Use service layer for artist tracks
                var artistResult = await _lastFmService.GetArtistTopTracksAsync(artist, limit);
                
                if (artistResult?.Tracks == null || !artistResult.Tracks.Any())
                {
                    Console.WriteLine(ErrorMessages.Format(ErrorMessages.NoArtistItemsFound, "tracks", artist));
                    return;
                }

                _displayService.DisplayTracksForArtist(artistResult.Tracks, 1);

                // Stream to Spotify if requested
                if ((playNow || !string.IsNullOrEmpty(playlist)) && artistResult.Tracks.Any())
                {
                    var playlistTitle = $"Top Tracks by {artist}";
                    await _spotifyStreamingService.StreamTracksAsync(artistResult.Tracks, playNow, playlist, playlistTitle, shuffle, device);
                }

                return;
            }

            // Handle user tracks
            var user = await GetUsernameAsync(username);
            if (user == null)
                return;

            // Resolve period parameters (--period, --from/--to, or --year)
            var (isDateRange, resolvedPeriod, fromDate, toDate) = ResolvePeriodParameters(period, from, to, year);
            
            // Determine display period format for consistent messaging
            var displayPeriod = isDateRange && fromDate.HasValue && toDate.HasValue 
                ? DateRangeParser.FormatDateRange(fromDate.Value, toDate.Value)
                : resolvedPeriod;

            // Handle range logic using service layer
            if (!string.IsNullOrEmpty(range))
            {
                if (!ValidateAndHandleRange(range, _displayService, out var startIndex, out var endIndex))
                {
                    return;
                }
                
                _displayService.DisplayOperationStart("tracks", user, displayPeriod, null, startIndex, endIndex, verbose);

                // Use service layer for range query
                var (rangeTracks, totalCount) = await _lastFmService.GetUserTopTracksRangeAsync(user, resolvedPeriod, startIndex, endIndex);
                
                if (!rangeTracks.Any())
                {
                    Console.WriteLine(ErrorMessages.Format(ErrorMessages.NoItemsInRange, "tracks"));
                    return;
                }
                
                _displayService.DisplayTracksForUser(rangeTracks, startIndex);
                _displayService.DisplayRangeInfo("tracks", startIndex, endIndex, rangeTracks.Count, totalCount, verbose);

                // Stream to Spotify if requested
                if ((playNow || !string.IsNullOrEmpty(playlist)) && rangeTracks.Any())
                {
                    var playlistTitle = $"Top Tracks for {user}";
                    if (!string.IsNullOrEmpty(displayPeriod))
                        playlistTitle += $" ({displayPeriod})";
                    await _spotifyStreamingService.StreamTracksAsync(rangeTracks, playNow, playlist, playlistTitle, shuffle, device);
                }

                return;
            }

            // Use standardized display service for operation start
            _displayService.DisplayOperationStart("tracks", user, displayPeriod, limit, verbose: verbose);

            // Use service layer for basic query
            TopTracks? result;
            if (isDateRange && fromDate.HasValue && toDate.HasValue)
            {
                result = await _lastFmService.GetUserTopTracksForDateRangeAsync(user, fromDate.Value, toDate.Value, limit);
            }
            else
            {
                result = await _lastFmService.GetUserTopTracksAsync(user, resolvedPeriod, limit);
            }

            if (result?.Tracks == null || !result.Tracks.Any())
            {
                Console.WriteLine(ErrorMessages.NoTracksFound);
                return;
            }

            _displayService.DisplayTracksForUser(result.Tracks, 1);
            _displayService.DisplayTotalInfo("tracks", result.Attributes.Total, verbose);

            // Stream to Spotify if requested
            if ((playNow || !string.IsNullOrEmpty(playlist)) && result.Tracks.Any())
            {
                var playlistTitle = $"Top Tracks for {user}";
                if (!string.IsNullOrEmpty(displayPeriod))
                    playlistTitle += $" ({displayPeriod})";
                await _spotifyStreamingService.StreamTracksAsync(result.Tracks, playNow, playlist, playlistTitle, shuffle, device);
            }

        }, timer);
    }
}