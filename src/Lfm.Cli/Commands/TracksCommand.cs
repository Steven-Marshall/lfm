using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

public class TracksCommand : BaseCommand
{
    private readonly ILastFmService _lastFmService;
    private readonly IDisplayService _displayService;

    public TracksCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILastFmService lastFmService,
        IDisplayService displayService,
        ILogger<TracksCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _lastFmService = lastFmService ?? throw new ArgumentNullException(nameof(lastFmService));
        _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
    }

    public async Task ExecuteAsync(int limit, string? period, string? username, string? artist, string? range = null, int? delayMs = null, bool verbose = false, bool timing = false, bool forceCache = false, bool forceApi = false, bool noCache = false, bool timer = false, string? from = null, string? to = null, string? year = null, bool json = false)
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

                if (verbose && !json)
                {
                    Console.WriteLine($"Getting top {limit} tracks for artist: {artist}...\n");
                }
                
                // Use service layer for artist tracks
                var artistResult = await _lastFmService.GetArtistTopTracksAsync(artist, limit);
                
                if (artistResult?.Tracks == null || !artistResult.Tracks.Any())
                {
                    if (json)
                    {
                        var emptyArtistResult = new Dictionary<string, object>
                        {
                            ["track"] = new object[0],
                            ["@attr"] = new Dictionary<string, string>
                            {
                                ["artist"] = artist,
                                ["totalPages"] = "0",
                                ["page"] = "1",
                                ["total"] = "0",
                                ["perPage"] = limit.ToString()
                            }
                        };
                        var jsonOutput = JsonSerializer.Serialize(emptyArtistResult, new JsonSerializerOptions { WriteIndented = true });
                        Console.WriteLine(jsonOutput);
                    }
                    else
                    {
                        Console.WriteLine(ErrorMessages.Format(ErrorMessages.NoArtistItemsFound, "tracks", artist));
                    }
                    return;
                }

                if (json)
                {
                    var jsonOutput = JsonSerializer.Serialize(artistResult, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(jsonOutput);
                }
                else
                {
                    _displayService.DisplayTracksForArtist(artistResult.Tracks, 1);
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
                
                if (!json)
                {
                    _displayService.DisplayOperationStart("tracks", user, displayPeriod, null, startIndex, endIndex, verbose);
                }

                // Use service layer for range query
                var (rangeTracks, totalCount) = await _lastFmService.GetUserTopTracksRangeAsync(user, resolvedPeriod, startIndex, endIndex);
                
                if (!rangeTracks.Any())
                {
                    if (json)
                    {
                        var emptyRangeResult = new
                        {
                            tracks = new object[0],
                            range = new { start = startIndex, end = endIndex, count = 0 },
                            total = totalCount
                        };
                        var jsonOutput = JsonSerializer.Serialize(emptyRangeResult, new JsonSerializerOptions { WriteIndented = true });
                        Console.WriteLine(jsonOutput);
                    }
                    else
                    {
                        Console.WriteLine(ErrorMessages.Format(ErrorMessages.NoItemsInRange, "tracks"));
                    }
                    return;
                }
                
                if (json)
                {
                    var jsonOutput = JsonSerializer.Serialize(new
                    {
                        tracks = rangeTracks,
                        range = new { start = startIndex, end = endIndex, count = rangeTracks.Count },
                        total = totalCount
                    }, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(jsonOutput);
                }
                else
                {
                    _displayService.DisplayTracksForUser(rangeTracks, startIndex);
                    _displayService.DisplayRangeInfo("tracks", startIndex, endIndex, rangeTracks.Count, totalCount, verbose);
                }

                return;
            }

            // Use standardized display service for operation start
            if (!json)
            {
                _displayService.DisplayOperationStart("tracks", user, displayPeriod, limit, verbose: verbose);
            }

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
                if (json)
                {
                    var emptyResult = new Dictionary<string, object>
                    {
                        ["track"] = new object[0],
                        ["@attr"] = new Dictionary<string, string>
                        {
                            ["user"] = user,
                            ["totalPages"] = "0",
                            ["page"] = "1",
                            ["total"] = "0",
                            ["perPage"] = limit.ToString()
                        }
                    };
                    var jsonOutput = JsonSerializer.Serialize(emptyResult, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(jsonOutput);
                }
                else
                {
                    Console.WriteLine(ErrorMessages.NoTracksFound);
                }
                return;
            }

            if (json)
            {
                var jsonOutput = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(jsonOutput);
            }
            else
            {
                _displayService.DisplayTracksForUser(result.Tracks, 1);
                _displayService.DisplayTotalInfo("tracks", result.Attributes.Total, verbose);
            }

            if (timing && !json)
            {
                DisplayTimingResults();
            }

        }, timer);
    }
}