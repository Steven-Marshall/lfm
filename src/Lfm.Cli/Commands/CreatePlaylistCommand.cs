using Lfm.Cli.Services;
using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

public class CreatePlaylistCommand : BaseCommand
{
    private readonly IPlaylistInputParser _inputParser;
    private readonly ISpotifyStreamingService _spotifyStreamingService;

    public CreatePlaylistCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        IPlaylistInputParser inputParser,
        ISpotifyStreamingService spotifyStreamingService,
        ILogger<CreatePlaylistCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _inputParser = inputParser ?? throw new ArgumentNullException(nameof(inputParser));
        _spotifyStreamingService = spotifyStreamingService ?? throw new ArgumentNullException(nameof(spotifyStreamingService));
    }

    public async Task ExecuteAsync(
        string input,
        string? playlistName = null,
        bool isJson = false,
        bool playNow = false,
        bool shuffle = false,
        string? device = null,
        bool verbose = false,
        bool timing = false,
        bool timer = false)
    {
        await ExecuteWithErrorHandlingAndTimerAsync("create-playlist command", async () =>
        {
            if (!await ValidateApiKeyAsync())
                return;

            // Parse input
            var parseResult = isJson
                ? _inputParser.ParseJson(input)
                : _inputParser.ParseCommaSeparated(input);

            if (!parseResult.IsValid)
            {
                Console.WriteLine($"{_symbols.Error} Invalid input format:");
                foreach (var error in parseResult.Errors)
                {
                    Console.WriteLine($"  • {error}");
                }
                return;
            }

            var trackRequests = parseResult.Data!;

            if (verbose)
            {
                Console.WriteLine($"Parsed {trackRequests.Count} track requests");
                Console.WriteLine();
            }

            // Look up tracks to create proper Track objects for Spotify
            var foundTracks = new List<Track>();
            var notFoundTracks = new List<string>();
            var errors = new List<string>();

            Console.WriteLine($"{_symbols.Settings} Looking up tracks...");

            for (int i = 0; i < trackRequests.Count; i++)
            {
                var request = trackRequests[i];

                if (verbose)
                    Console.WriteLine($"Looking up: {request.Artist} - {request.Track}");

                try
                {
                    var trackResult = await _apiClient.GetTrackInfoWithResultAsync(request.Artist, request.Track, "");

                    if (trackResult.Success && trackResult.Data != null)
                    {
                        // Convert TrackLookupInfo to Track format needed by Spotify service
                        var track = new Track
                        {
                            Name = trackResult.Data.Track.Name,
                            Artist = new ArtistInfo { Name = trackResult.Data.Track.Artist.Name },
                            Url = trackResult.Data.Track.Url
                        };
                        foundTracks.Add(track);

                        if (verbose)
                            Console.WriteLine($"  {_symbols.Success} Found: {track.Artist.Name} - {track.Name}");
                    }
                    else
                    {
                        notFoundTracks.Add($"{request.Artist} - {request.Track}");
                        if (verbose)
                            Console.WriteLine($"  {_symbols.Error} Not found: {request.Artist} - {request.Track}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Error looking up {request.Artist} - {request.Track}: {ex.Message}");
                    if (verbose)
                        Console.WriteLine($"  {_symbols.Error} Error: {ex.Message}");
                }

                // Add small delay to be respectful to the API
                if (i < trackRequests.Count - 1)
                    await Task.Delay(50);
            }

            Console.WriteLine();

            if (!foundTracks.Any())
            {
                Console.WriteLine($"{_symbols.Error} No tracks were found on Last.fm");
                if (notFoundTracks.Any())
                {
                    Console.WriteLine("Tracks not found:");
                    foreach (var track in notFoundTracks)
                    {
                        Console.WriteLine($"  • {track}");
                    }
                }
                return;
            }

            // Display results
            Console.WriteLine($"{_symbols.Success} Found {foundTracks.Count} of {trackRequests.Count} tracks");

            if (notFoundTracks.Any())
            {
                Console.WriteLine($"{_symbols.StopSign} {notFoundTracks.Count} tracks not found:");
                foreach (var track in notFoundTracks.Take(5))
                {
                    Console.WriteLine($"  • {track}");
                }
                if (notFoundTracks.Count > 5)
                {
                    Console.WriteLine($"  ... and {notFoundTracks.Count - 5} more");
                }
            }

            if (errors.Any())
            {
                Console.WriteLine($"{_symbols.StopSign} {errors.Count} errors occurred:");
                foreach (var error in errors.Take(3))
                {
                    Console.WriteLine($"  • {error}");
                }
                if (errors.Count > 3)
                {
                    Console.WriteLine($"  ... and {errors.Count - 3} more");
                }
            }

            Console.WriteLine();

            // Create playlist name (don't add prefix here - SpotifyStreamingService handles it)
            var finalPlaylistName = playlistName ?? "Custom Playlist";

            // Stream to Spotify
            if (foundTracks.Any())
            {
                await _spotifyStreamingService.StreamTracksAsync(
                    foundTracks,
                    playNow,
                    finalPlaylistName,
                    finalPlaylistName,
                    shuffle,
                    device,
                    verbose);
            }

            // Display timing results if requested
            DisplayTimingResults();

        }, timer);
    }
}