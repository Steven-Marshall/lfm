using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Cli.Commands;

public class TracksCommand : BaseCommand
{
    private readonly IDisplayService _displayService;

    public TracksCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        IDisplayService displayService,
        ILogger<TracksCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
    }

    public async Task ExecuteAsync(int limit, string period, string? username, string? artist, string? range = null, int? delayMs = null, bool verbose = false, bool timing = false, bool forceCache = false, bool forceApi = false, bool noCache = false, bool timer = false)
    {
        await ExecuteWithErrorHandlingAndTimerAsync("tracks command", async () =>
        {
            // Configure cache behavior and timing
            ConfigureCaching(timing, forceCache, forceApi, noCache);

            if (!await ValidateApiKeyAsync())
                return;

            if (!string.IsNullOrEmpty(artist))
            {
                if (!string.IsNullOrEmpty(range))
                {
                    Console.WriteLine(ErrorMessages.RangeNotSupportedWithArtist);
                    return;
                }

                if (verbose)
                {
                    Console.WriteLine($"Getting top {limit} tracks for artist: {artist}...\n");
                }
                
                var artistResult = await _apiClient.GetArtistTopTracksAsync(artist, limit);
                
                if (artistResult?.Tracks == null || !artistResult.Tracks.Any())
                {
                    Console.WriteLine(ErrorMessages.Format(ErrorMessages.NoArtistItemsFound, "tracks", artist));
                    return;
                }

                _displayService.DisplayTracksForArtist(artistResult.Tracks, 1);
            }
            else
            {
                var user = await GetUsernameAsync(username);
                if (user == null)
                    return;

                // Handle range logic
                if (!string.IsNullOrEmpty(range))
                {
                    var (isValid, startIndex, endIndex, errorMessage) = ParseRange(range);
                    if (!isValid)
                    {
                        Console.WriteLine(errorMessage);
                        return;
                    }
                    
                    var (rangeTracks, totalCount) = await ExecuteRangeQueryAsync<Track, TopTracks>(
                        startIndex,
                        endIndex,
                        _apiClient.GetTopTracksAsync,
                        response => response.Tracks,
                        response => response.Attributes.Total,
                        "tracks",
                        user,
                        period,
                        delayMs,
                        verbose);
                    
                    if (!rangeTracks.Any())
                    {
                        Console.WriteLine(ErrorMessages.Format(ErrorMessages.NoItemsInRange, "tracks"));
                        return;
                    }
                    
                    _displayService.DisplayTracksForUser(rangeTracks, startIndex);
                    if (verbose)
                    {
                        Console.WriteLine($"\nShowing tracks {startIndex}-{Math.Min(endIndex, startIndex + rangeTracks.Count - 1)} of {totalCount}");
                    }
                    return;
                }

                if (verbose)
                {
                    Console.WriteLine($"Getting top {limit} tracks for {user} ({period})...\n");
                }

                var result = await _apiClient.GetTopTracksAsync(user, period, limit);

                if (result?.Tracks == null || !result.Tracks.Any())
                {
                    Console.WriteLine(ErrorMessages.NoTracksFound);
                    return;
                }

                _displayService.DisplayTracksForUser(result.Tracks, 1);
                _displayService.DisplayTotalInfo("tracks", result.Attributes.Total, verbose);
            }

            // Display timing results if enabled
            if (timing)
            {
                DisplayTimingResults();
            }
        }, timer);
    }

}