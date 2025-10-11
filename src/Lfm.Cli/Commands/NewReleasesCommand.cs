using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

// DISABLED: Spotify's /browse/new-releases API is broken (returns months-old albums)
// See: https://community.spotify.com/t5/Spotify-for-Developers/Web-API-Get-New-Releases-API-Returning-Old-Items/m-p/6069709
// Keeping implementation for future alternative data source (AOTY, Discogs, etc.)
public class NewReleasesCommand : BaseCommand
{
    private readonly ILastFmService _lastFmService;

    public NewReleasesCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILastFmService lastFmService,
        ILogger<NewReleasesCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _lastFmService = lastFmService ?? throw new ArgumentNullException(nameof(lastFmService));
    }

    public async Task ExecuteAsync(int limit = 50, bool json = false, bool timer = false)
    {
        await ExecuteWithErrorHandlingAndTimerAsync("new-releases command", async () =>
        {
            // Validate limit parameter (1-50 per Spotify API)
            if (limit < 1 || limit > 50)
            {
                Console.WriteLine($"{_symbols.Error} Limit must be between 1 and 50.");
                return;
            }

            if (!json)
            {
                Console.WriteLine($"{_symbols.Music} Getting new album releases from Spotify...");
                Console.WriteLine();
            }

            // Call service layer to get new releases
            var result = await _lastFmService.GetNewReleasesAsync(limit);

            if (!result.Success)
            {
                if (json)
                {
                    var errorOutput = new
                    {
                        success = false,
                        message = result.Message
                    };
                    Console.WriteLine(JsonSerializer.Serialize(errorOutput, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} {result.Message}");
                }
                return;
            }

            if (!result.Albums.Any())
            {
                if (json)
                {
                    var emptyOutput = new
                    {
                        success = true,
                        albums = Array.Empty<object>(),
                        total = 0,
                        source = result.Source
                    };
                    Console.WriteLine(JsonSerializer.Serialize(emptyOutput, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} No new releases found.");
                }
                return;
            }

            if (json)
            {
                var jsonOutput = new
                {
                    success = true,
                    albums = result.Albums.Select(album => new
                    {
                        name = album.Name,
                        artist = album.Artist,
                        releaseDate = album.ReleaseDate,
                        releaseDatePrecision = album.ReleaseDatePrecision,
                        totalTracks = album.TotalTracks,
                        albumType = album.AlbumType,
                        spotifyId = album.SpotifyId,
                        spotifyUrl = album.SpotifyUrl,
                        imageUrl = album.ImageUrl
                    }),
                    total = result.Total,
                    source = result.Source
                };
                Console.WriteLine(JsonSerializer.Serialize(jsonOutput, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine($"{_symbols.Success} Found {result.Total} new releases:");
                Console.WriteLine();

                var counter = 1;
                foreach (var album in result.Albums)
                {
                    Console.WriteLine($"  {counter}. {album.Artist} - {album.Name}");
                    Console.WriteLine($"     Release: {album.ReleaseDate} | Type: {album.AlbumType} | Tracks: {album.TotalTracks}");
                    if (!string.IsNullOrEmpty(album.SpotifyUrl))
                    {
                        Console.WriteLine($"     {album.SpotifyUrl}");
                    }
                    Console.WriteLine();
                    counter++;
                }

                Console.WriteLine($"Total: {result.Total} new album releases");
            }

        }, timer);
    }
}
