using Lfm.Cli.Services;
using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

public class MixtapeCommand : BaseCommand
{
    private readonly ILastFmService _lastFmService;
    private readonly IDisplayService _displayService;
    private readonly ISpotifyStreamingService _spotifyStreamingService;

    public MixtapeCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILastFmService lastFmService,
        IDisplayService displayService,
        ISpotifyStreamingService spotifyStreamingService,
        ILogger<MixtapeCommand> logger,
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
        float? bias = null,
        int? minPlays = null,
        int tracksPerArtist = 1,
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
        int? seed = null,
        bool json = false)
    {
        await ExecuteWithErrorHandlingAndTimerAsync("mixtape command", async () =>
        {
            // Configure cache behavior and timing
            ConfigureCaching(timing, forceCache, forceApi, noCache);

            if (!await ValidateApiKeyAsync())
                return;

            // Get configuration defaults
            var config = await _configManager.LoadAsync();
            var effectiveBias = bias ?? config.DefaultMixtapeBias;
            var effectiveMinPlays = minPlays ?? config.DefaultMinPlays;

            // Validate parameters
            if (limit <= 0)
            {
                Console.WriteLine("Error: Target tracks must be greater than 0");
                return;
            }

            if (effectiveBias < 0.0f || effectiveBias > 1.0f)
            {
                Console.WriteLine("Error: Bias must be between 0.0 (random) and 1.0 (weighted by play count)");
                return;
            }

            if (tracksPerArtist <= 0)
            {
                Console.WriteLine("Error: --tracks-per-artist must be greater than 0");
                return;
            }

            // Validate mutually exclusive parameters
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
                Console.WriteLine($"Generating mixtape with bias {effectiveBias:F2} (0=random, 1=weighted)");
                Console.WriteLine($"Target tracks: {limit} (max {tracksPerArtist} per artist)");
                Console.WriteLine($"Minimum plays: {effectiveMinPlays}");
                Console.WriteLine($"Time period: {displayPeriod}");

                if (config.EnableTagFiltering && config.ExcludedTags.Any())
                {
                    Console.WriteLine($"Tag filtering: enabled ({config.ExcludedTags.Count} excluded tags)");
                }

                Console.WriteLine();
            }

            // Generate mixtape
            List<Track> tracks;

            if (isDateRange)
            {
                tracks = await _lastFmService.GetMixtapeTracksAsync(
                    user, limit, null, fromDate, toDate, effectiveBias, effectiveMinPlays, tracksPerArtist, true, seed);
            }
            else
            {
                tracks = await _lastFmService.GetMixtapeTracksAsync(
                    user, limit, resolvedPeriod, null, null, effectiveBias, effectiveMinPlays, tracksPerArtist, true, seed);
            }

            if (!tracks.Any())
            {
                if (json)
                {
                    var emptyResult = new Dictionary<string, object>
                    {
                        ["tracks"] = new object[0],
                        ["count"] = 0,
                        ["bias"] = effectiveBias,
                        ["minPlays"] = effectiveMinPlays,
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
                    ["tracks"] = tracks.Select(t => new {
                        name = t.Name,
                        artist = t.Artist.Name,
                        playcount = t.PlayCount,
                        url = t.Url
                    }).ToArray(),
                    ["count"] = tracks.Count,
                    ["bias"] = effectiveBias,
                    ["minPlays"] = effectiveMinPlays,
                    ["tracksPerArtist"] = tracksPerArtist,
                    ["totalArtists"] = tracks.Select(t => t.Artist.Name).Distinct().Count()
                };
                Console.WriteLine(JsonSerializer.Serialize(jsonResult, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                var artistCount = tracks.Select(t => t.Artist.Name).Distinct().Count();
                Console.WriteLine($"{_symbols.Music} Mixtape: {tracks.Count} tracks from {artistCount} different artists");
                Console.WriteLine($"Bias: {effectiveBias:F2} | Min plays: {effectiveMinPlays} | Max per artist: {tracksPerArtist}");
                Console.WriteLine();
                _displayService.DisplayTracksForUser(tracks, 1);
            }

            // Display timing results if requested
            DisplayTimingResults();

            // Stream to Spotify if requested
            if ((playNow || !string.IsNullOrEmpty(playlist)) && tracks.Any())
            {
                var playlistTitle = string.IsNullOrEmpty(playlist) ? "Mixtape" : playlist;
                if (!string.IsNullOrEmpty(displayPeriod))
                    playlistTitle += $" ({displayPeriod})";
                else if (effectiveBias != 0.3f)
                    playlistTitle += $" (bias {effectiveBias:F2})";

                await _spotifyStreamingService.StreamTracksAsync(tracks, playNow, playlist, playlistTitle, shuffle, device, verbose);
            }

        }, timer);
    }
}