using Lfm.Cli.Services;
using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

public class TopTracksCommand : BaseCommand
{
    private readonly ILastFmService _lastFmService;
    private readonly IDisplayService _displayService;
    private readonly ISpotifyStreamingService _spotifyStreamingService;

    public TopTracksCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILastFmService lastFmService,
        IDisplayService displayService,
        ISpotifyStreamingService spotifyStreamingService,
        ILogger<TopTracksCommand> logger,
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
        int tracksPerArtist,
        string? range = null,
        int? delayMs = null,
        bool verbose = false,
        bool timing = false,
        bool forceCache = false,
        bool forceApi = false,
        bool noCache = false,
        bool timer = false,
        string? from = null,
        string? to = null,
        string? year = null,
        bool playNow = false,
        string? playlist = null,
        bool shuffle = false,
        string? device = null,
        bool json = false)
    {
        await ExecuteWithErrorHandlingAndTimerAsync("toptracks command", async () =>
        {
            // Configure cache behavior and timing
            ConfigureCaching(timing, forceCache, forceApi, noCache);

            if (!await ValidateApiKeyAsync())
                return;

            // Validate parameters
            // ValidateLimit(limit); // Temporarily disabled for testing

            if (tracksPerArtist <= 0)
            {
                Console.WriteLine("Error: --tracks-per-artist must be greater than 0");
                return;
            }

            // tracks-per-artist and range are mutually exclusive
            if (!string.IsNullOrEmpty(range) && tracksPerArtist > 0)
            {
                Console.WriteLine("Error: --range and --tracks-per-artist cannot be used together");
                return;
            }

            var user = await GetUsernameAsync(username);
            if (user == null)
                return;

            // Resolve period parameters (--period, --from/--to, or --year)
            var (isDateRange, resolvedPeriod, fromDate, toDate) = ResolvePeriodParameters(period, from, to, year);

            // Determine display period format for consistent messaging
            var displayPeriod = GetDisplayPeriod(isDateRange, fromDate, toDate, resolvedPeriod);

            if (verbose && !json)
            {
                Console.WriteLine($"Creating diverse playlist with {tracksPerArtist} tracks per artist...");
                Console.WriteLine($"Target playlist size: {limit} tracks");
                Console.WriteLine($"Time period: {displayPeriod}");
                Console.WriteLine();
            }

            // Implement expanding window algorithm for diverse playlists
            var finalTracks = new List<Track>();
            var artistTrackCounts = new Dictionary<string, int>();

            if (isDateRange)
            {
                // For date ranges, use larger fixed sample since expanding approach is too slow
                if (verbose && !json)
                {
                    Console.WriteLine("Date range queries don't support expanding windows - using large sample strategy...");
                }

                // Get configurable diversity multiplier
                var config = await _configManager.LoadAsync();
                var sampleSize = Math.Max(limit * tracksPerArtist * config.DateRangeDiversityMultiplier, 200);

                if (verbose && !json)
                {
                    Console.WriteLine($"Sampling {sampleSize} tracks for diversity...");
                }

                var result = await _lastFmService.GetUserTopTracksForDateRangeAsync(user, fromDate!.Value, toDate!.Value, sampleSize);
                var allTracks = result?.Tracks ?? new List<Track>();

                if (!allTracks.Any())
                {
                    if (verbose && !json)
                    {
                        Console.WriteLine("No tracks found in date range");
                    }
                }
                else
                {
                    // Apply tracks-per-artist filter
                    foreach (var track in allTracks)
                    {
                        var artistName = track.Artist.Name;
                        var currentArtistCount = artistTrackCounts.GetValueOrDefault(artistName, 0);

                        if (currentArtistCount < tracksPerArtist)
                        {
                            finalTracks.Add(track);
                            artistTrackCounts[artistName] = currentArtistCount + 1;

                            if (finalTracks.Count >= limit)
                                break;
                        }
                    }

                    if (verbose && !json)
                    {
                        var uniqueArtists = finalTracks.Select(t => t.Artist.Name).Distinct().Count();
                        Console.WriteLine($"Found {finalTracks.Count} tracks from {uniqueArtists} artists in sample of {allTracks.Count}");
                    }
                }
            }
            else
            {
                // For periods, use expanding window algorithm
                var config = await _configManager.LoadAsync();
                var windowStart = 1;
                var windowSize = limit; // Start with target limit
                var maxIterations = 10; // Prevent infinite loops
                var iteration = 0;
                var emptyWindows = 0; // Track consecutive empty windows

                while (finalTracks.Count < limit && iteration < maxIterations)
                {
                    iteration++;
                    var windowEnd = windowStart + windowSize - 1;

                    if (verbose && !json)
                    {
                        Console.WriteLine($"Iteration {iteration}: Fetching tracks {windowStart}-{windowEnd}...");
                    }

                    // Get tracks for current window
                    var (windowTracks, _) = await _lastFmService.GetUserTopTracksRangeAsync(user, resolvedPeriod, windowStart, windowEnd);

                    if (!windowTracks.Any())
                    {
                        if (verbose && !json)
                        {
                            Console.WriteLine($"No more tracks found at positions {windowStart}-{windowEnd}");
                        }
                        break; // No more tracks available
                    }

                    // Add tracks respecting tracks-per-artist limit
                    var addedFromWindow = 0;
                    foreach (var track in windowTracks)
                    {
                        var artistName = track.Artist.Name;
                        var currentArtistCount = artistTrackCounts.GetValueOrDefault(artistName, 0);

                        // Check if we can add this track
                        if (currentArtistCount < tracksPerArtist)
                        {
                            finalTracks.Add(track);
                            artistTrackCounts[artistName] = currentArtistCount + 1;
                            addedFromWindow++;

                            if (finalTracks.Count >= limit)
                                break;
                        }
                    }

                    if (verbose && !json)
                    {
                        Console.WriteLine($"Added {addedFromWindow} tracks from this window. Total: {finalTracks.Count}/{limit}");
                    }

                    // If we didn't add any tracks from this window, try a few more windows before giving up
                    if (addedFromWindow == 0)
                    {
                        emptyWindows++;
                        if (verbose && !json)
                        {
                            Console.WriteLine("No new diverse tracks found in this window");
                        }

                        // Give up after configured number of consecutive empty windows
                        if (emptyWindows >= config.MaxEmptyWindows)
                        {
                            if (verbose && !json)
                            {
                                Console.WriteLine($"Reached maximum diversity with available data after {config.MaxEmptyWindows} empty windows");
                            }
                            break;
                        }
                    }
                    else
                    {
                        // Reset empty window counter when we find tracks
                        emptyWindows = 0;
                    }

                    // Move to next window
                    windowStart = windowEnd + 1;
                }

                if (verbose && !json && finalTracks.Count < limit)
                {
                    Console.WriteLine($"Reached maximum diversity with {finalTracks.Count} tracks from available data");
                }
            }

            if (!finalTracks.Any())
            {
                if (json)
                {
                    var emptyResult = new Dictionary<string, object>
                    {
                        ["tracks"] = new object[0],
                        ["count"] = 0,
                        ["tracksPerArtist"] = tracksPerArtist,
                        ["totalArtists"] = 0
                    };
                    Console.WriteLine(JsonSerializer.Serialize(emptyResult, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    Console.WriteLine("No tracks found for the specified criteria.");
                }
                return;
            }

            // Display results
            if (json)
            {
                var jsonResult = new Dictionary<string, object>
                {
                    ["tracks"] = finalTracks.Select(t => new {
                        name = t.Name,
                        artist = t.Artist.Name,
                        playcount = t.PlayCount,
                        url = t.Url
                    }).ToArray(),
                    ["count"] = finalTracks.Count,
                    ["tracksPerArtist"] = tracksPerArtist,
                    ["totalArtists"] = finalTracks.Select(t => t.Artist.Name).Distinct().Count()
                };
                Console.WriteLine(JsonSerializer.Serialize(jsonResult, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                var artistCount = finalTracks.Select(t => t.Artist.Name).Distinct().Count();
                Console.WriteLine($"Found {finalTracks.Count} tracks from {artistCount} different artists");
                Console.WriteLine();
                Console.WriteLine("Top Tracks Playlist:");
                _displayService.DisplayTracksForUser(finalTracks, 1);
            }

            // Display timing results if requested
            DisplayTimingResults();

            // Stream to Spotify if requested
            if ((playNow || !string.IsNullOrEmpty(playlist)) && finalTracks.Any())
            {
                var playlistTitle = string.IsNullOrEmpty(playlist) ? "Top Tracks Playlist" : playlist;
                if (!string.IsNullOrEmpty(displayPeriod))
                    playlistTitle += $" ({displayPeriod})";

                await _spotifyStreamingService.StreamTracksAsync(finalTracks, playNow, playlist, playlistTitle, shuffle, device, verbose);
            }

        }, timer);
    }
}