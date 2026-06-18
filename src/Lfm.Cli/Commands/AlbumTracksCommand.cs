using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Lfm.Spotify;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

/// Command for getting an album's canonical tracklist from Spotify.
/// Primary use: providing track numbers, disc numbers, and durations to the MCP
/// layer so the LLM does not hallucinate track positions when introducing music.
public class AlbumTracksCommand
{
    private readonly IConfigurationManager _configManager;
    private readonly ILogger<AlbumTracksCommand> _logger;
    private readonly ISymbolProvider _symbols;
    private readonly IPlaylistStreamer _spotifyStreamer;

    public AlbumTracksCommand(
        IConfigurationManager configManager,
        ILogger<AlbumTracksCommand> logger,
        ISymbolProvider symbolProvider,
        IPlaylistStreamer spotifyStreamer)
    {
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _symbols = symbolProvider ?? throw new ArgumentNullException(nameof(symbolProvider));
        _spotifyStreamer = spotifyStreamer ?? throw new ArgumentNullException(nameof(spotifyStreamer));
    }

    public async Task<int> ExecuteAsync(string artist, string album, bool exactMatch = false, bool json = false)
    {
        try
        {
            if (_spotifyStreamer is not SpotifyStreamer spotifyStreamer)
            {
                var error = "Spotify streamer not available";
                if (json) OutputJsonError(error, artist, album);
                else Console.WriteLine($"{_symbols.Error} {error}");
                return 1;
            }

            var result = await spotifyStreamer.GetAlbumTracksAsync(artist, album, exactMatch);

            if (result.HasMultipleVersions)
            {
                var errorMessage = "Multiple album versions found. Retry with --exact-match and the exact album name.";
                if (json)
                {
                    OutputJsonMultipleVersions(errorMessage, artist, album, result);
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} {errorMessage}");
                    Console.WriteLine();
                    Console.WriteLine("Available versions:");
                    foreach (var v in result.AlbumVersions)
                    {
                        Console.WriteLine($"  - {v.Name} ({v.ReleaseDate}) - {v.TrackCount} tracks");
                    }
                }
                return 1;
            }

            if (result.SpotifyUri is null || !result.Tracks.Any())
            {
                var error = exactMatch
                    ? $"No exact match found for album: {artist} - {album}"
                    : $"Album not found on Spotify: {artist} - {album}";
                if (json) OutputJsonError(error, artist, album);
                else Console.WriteLine($"{_symbols.Error} {error}");
                return 1;
            }

            if (json)
            {
                OutputJsonSuccess(artist, album, result);
            }
            else
            {
                OutputText(artist, album, result);
            }

            return 0;
        }
        catch (SpotifyReauthRequiredException ex)
        {
            _logger.LogError(ex, "Spotify reauthentication required");
            if (json) OutputJsonReauth(ex.Message);
            else
            {
                Console.WriteLine($"{_symbols.Error} {ex.Message}");
                Console.WriteLine($"{_symbols.Tip} Run: lfm config spotify-auth");
            }
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing album-tracks command");
            if (json) OutputJsonError(ex.Message, artist, album);
            else Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
            return 1;
        }
    }

    private static void OutputJsonSuccess(string artist, string album, Lfm.Spotify.Models.AlbumTracksResult result)
    {
        var output = new
        {
            success = true,
            artist,
            album,
            resolvedAlbum = new
            {
                name = result.AlbumName,
                releaseDate = result.ReleaseDate,
                totalTracks = result.TotalTracks,
                uri = result.SpotifyUri
            },
            tracks = result.Tracks.Select(t => new
            {
                trackNumber = t.TrackNumber,
                discNumber = t.DiscNumber,
                name = t.Name,
                artists = t.Artists,
                durationMs = t.DurationMs,
                durationFormatted = FormatDuration(t.DurationMs),
                uri = t.Uri,
                isPlayable = t.IsPlayable
            }).ToList()
        };

        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        }));
    }

    private static void OutputJsonMultipleVersions(string error, string artist, string album, Lfm.Spotify.Models.AlbumTracksResult result)
    {
        var output = new
        {
            success = false,
            error,
            artist,
            album,
            multipleVersionsDetected = true,
            albumVersions = result.AlbumVersions.Select(v => new
            {
                name = v.Name,
                releaseDate = v.ReleaseDate,
                trackCount = v.TrackCount
            }).ToList(),
            suggestion = "Multiple album versions found. Pick one and retry with exactMatch: true."
        };
        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void OutputJsonError(string error, string? artist, string? album)
    {
        var output = new { success = false, error, artist, album };
        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        }));
    }

    private static void OutputJsonReauth(string message)
    {
        var output = new
        {
            success = false,
            errorCode = "spotify_reauth_required",
            error = message,
            action = "Spotify needs re-authentication. Ask the user to run `lfm config spotify-auth` on the host where the MCP server runs."
        };
        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void OutputText(string artist, string album, Lfm.Spotify.Models.AlbumTracksResult result)
    {
        Console.WriteLine($"{_symbols.Success} {result.AlbumName} ({result.ReleaseDate}) - {result.TotalTracks} tracks");
        Console.WriteLine();

        var lastDisc = -1;
        foreach (var t in result.Tracks)
        {
            if (t.DiscNumber != lastDisc && result.Tracks.Any(x => x.DiscNumber != t.DiscNumber))
            {
                Console.WriteLine($"Disc {t.DiscNumber}");
                lastDisc = t.DiscNumber;
            }
            Console.WriteLine($"  {t.TrackNumber,2}. {t.Name}  [{FormatDuration(t.DurationMs)}]");
        }
    }

    private static string FormatDuration(int ms)
    {
        var totalSeconds = ms / 1000;
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:D2}";
    }
}
