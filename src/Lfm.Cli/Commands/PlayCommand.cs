using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Lfm.Spotify;
using Lfm.Sonos;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

/// <summary>
/// Command for playing tracks or albums immediately or adding to queue
/// </summary>
public class PlayCommand : BaseCommand
{
    private readonly IPlaylistStreamer _spotifyStreamer;
    private readonly ISonosStreamer _sonosStreamer;

    public PlayCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILogger<PlayCommand> logger,
        ISymbolProvider symbolProvider,
        IPlaylistStreamer spotifyStreamer,
        ISonosStreamer sonosStreamer)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _spotifyStreamer = spotifyStreamer ?? throw new ArgumentNullException(nameof(spotifyStreamer));
        _sonosStreamer = sonosStreamer ?? throw new ArgumentNullException(nameof(sonosStreamer));
    }

    /// <summary>
    /// Execute play/queue command for a track or album
    /// </summary>
    public async Task<int> ExecuteAsync(
        string artist,
        string? track = null,
        string? album = null,
        string? device = null,
        string? player = null,
        string? room = null,
        bool queue = false,
        bool json = false)
    {
        try
        {
            // Load config to determine player
            var config = await _configManager.LoadAsync();

            // Determine which player to use: parameter > config default
            PlayerType targetPlayer;
            if (!string.IsNullOrWhiteSpace(player))
            {
                if (!Enum.TryParse<PlayerType>(player, true, out targetPlayer))
                {
                    var error = $"Invalid player '{player}'. Valid options: Spotify, Sonos";
                    if (json)
                    {
                        OutputJson(false, error);
                    }
                    else
                    {
                        Console.WriteLine($"{_symbols.Error} {error}");
                    }
                    return 1;
                }
            }
            else
            {
                targetPlayer = config.DefaultPlayer;
            }

            // Store target room for Sonos (if applicable)
            string? targetRoom = null;

            // Validate availability based on target player
            if (targetPlayer == PlayerType.Spotify)
            {
                if (!await _spotifyStreamer.IsAvailableAsync())
                {
                    var error = "Spotify is not configured or authenticated. Please run 'lfm spotify auth' first.";
                    if (json)
                    {
                        OutputJson(false, error);
                    }
                    else
                    {
                        Console.WriteLine($"{_symbols.Error} {error}");
                    }
                    return 1;
                }
            }
            else // Sonos
            {
                if (!await _sonosStreamer.IsAvailableAsync())
                {
                    var error = $"Sonos bridge not available at {config.Sonos.HttpApiBaseUrl}. Check your configuration.";
                    if (json)
                    {
                        OutputJson(false, error);
                    }
                    else
                    {
                        Console.WriteLine($"{_symbols.Error} {error}");
                        Console.WriteLine($"{_symbols.Tip} Set bridge URL with: lfm config set-sonos-api-url <url>");
                    }
                    return 1;
                }

                // Determine which Sonos room to use
                targetRoom = room ?? config.Sonos.DefaultRoom;
                if (string.IsNullOrWhiteSpace(targetRoom))
                {
                    var error = "No Sonos room specified and no default room configured.";
                    if (json)
                    {
                        OutputJson(false, error);
                    }
                    else
                    {
                        Console.WriteLine($"{_symbols.Error} {error}");
                        Console.WriteLine($"{_symbols.Tip} Use --room parameter or set default with: lfm config set-sonos-default-room \"Room Name\"");
                    }
                    return 1;
                }

                // Validate room exists
                try
                {
                    await _sonosStreamer.ValidateRoomAsync(targetRoom);
                }
                catch (Exception ex)
                {
                    var error = ex.Message;
                    if (json)
                    {
                        OutputJson(false, error);
                    }
                    else
                    {
                        Console.WriteLine($"{_symbols.Error} {error}");
                        Console.WriteLine($"{_symbols.Tip} List available rooms with: lfm sonos rooms");
                    }
                    return 1;
                }
            }

            // Validate artist name
            if (string.IsNullOrWhiteSpace(artist))
            {
                var error = "Artist name is required";
                if (json)
                {
                    OutputJson(false, error);
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} {error}");
                }
                return 1;
            }

            // Validate that at least one of track or album is provided
            if (string.IsNullOrWhiteSpace(track) && string.IsNullOrWhiteSpace(album))
            {
                var error = "Either track or album must be specified";
                if (json)
                {
                    OutputJson(false, error);
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} {error}");
                }
                return 1;
            }

            // Note: track + album combination is now ALLOWED for version disambiguation

            // Execute based on track or album mode
            PlaylistStreamResult result;
            Lfm.Spotify.Models.TrackSearchResult? trackSearchResult = null;

            if (!string.IsNullOrWhiteSpace(track))
            {
                // Track mode (with optional album for disambiguation)

                // Ensure we have SpotifyStreamer for track searches
                if (_spotifyStreamer is not SpotifyStreamer spotifyStreamer)
                {
                    var error = "Spotify streamer not available";
                    if (json)
                    {
                        OutputJson(false, error, artist, track, album);
                    }
                    else
                    {
                        Console.WriteLine($"{_symbols.Error} {error}");
                    }
                    return 1;
                }

                // Do track search ONCE at the top (shared by both Spotify and Sonos)
                var trackToSearch = new Track
                {
                    Name = track,
                    Artist = new ArtistInfo { Name = artist }
                };

                trackSearchResult = await spotifyStreamer.SearchSpotifyTrackWithDetailsAsync(trackToSearch, album);

                // Check for multiple versions if no album was specified
                if (string.IsNullOrWhiteSpace(album) && trackSearchResult.HasMultipleVersions)
                {
                    if (json)
                    {
                        OutputJsonError(
                            "Multiple versions found - album parameter required",
                            artist,
                            track,
                            null,
                            trackSearchResult
                        );
                    }
                    else
                    {
                        Console.WriteLine($"{_symbols.Error} Multiple album versions found for this track:");
                        foreach (var albumVersion in trackSearchResult.AlbumVersions)
                        {
                            Console.WriteLine($"  - {albumVersion}");
                        }
                        Console.WriteLine($"\nPlease specify the album with: --album \"Album Name\"");
                    }
                    return 1;
                }

                // Check if track was found
                if (trackSearchResult.SpotifyUri == null)
                {
                    var error = string.IsNullOrWhiteSpace(album)
                        ? $"Track not found on Spotify: {artist} - {track}"
                        : $"Track not found on album: {artist} - {track} ({album})";

                    if (json)
                    {
                        OutputJson(false, error, artist, track, album);
                    }
                    else
                    {
                        Console.WriteLine($"{_symbols.Error} {error}");
                    }
                    return 1;
                }

                // Now route to the appropriate player with the found URI
                var spotifyUris = new List<string> { trackSearchResult.SpotifyUri };

                if (targetPlayer == PlayerType.Sonos)
                {
                    result = await PlayOnSonosAsync(spotifyUris, targetRoom!, queue);
                }
                else
                {
                    if (queue)
                    {
                        result = await spotifyStreamer.QueueFromUrisAsync(spotifyUris, device);
                    }
                    else
                    {
                        result = await spotifyStreamer.PlayNowFromUrisAsync(spotifyUris, device);
                    }
                }
            }
            else
            {
                // Album mode - use one-shot Spotify album search
                if (_spotifyStreamer is not SpotifyStreamer spotifyStreamer)
                {
                    var error = "Spotify streamer not available";
                    if (json)
                    {
                        OutputJson(false, error, artist, null, album);
                    }
                    else
                    {
                        Console.WriteLine($"{_symbols.Error} {error}");
                    }
                    return 1;
                }

                // For Sonos, use album URI; for Spotify, use individual track URIs
                if (targetPlayer == PlayerType.Sonos)
                {
                    // Get album URI for Sonos
                    var albumUri = await spotifyStreamer.SearchSpotifyAlbumUriAsync(artist, album!);

                    if (albumUri == null)
                    {
                        var error = $"Album not found on Spotify: {artist} - {album}";
                        if (json)
                        {
                            OutputJson(false, error, artist, null, album);
                        }
                        else
                        {
                            Console.WriteLine($"{_symbols.Error} {error}");
                        }
                        return 1;
                    }

                    // Play album as a single unit on Sonos
                    result = await PlayOnSonosAsync(new List<string> { albumUri }, targetRoom!, queue);
                }
                else
                {
                    // Get individual track URIs for Spotify
                    var spotifyUris = await spotifyStreamer.SearchSpotifyAlbumTracksAsync(artist, album!);

                    if (!spotifyUris.Any())
                    {
                        var error = $"Album not found on Spotify: {artist} - {album}";
                        if (json)
                        {
                            OutputJson(false, error, artist, null, album);
                        }
                        else
                        {
                            Console.WriteLine($"{_symbols.Error} {error}");
                        }
                        return 1;
                    }

                    // Play or queue the album tracks on Spotify
                    if (queue)
                    {
                        result = await spotifyStreamer.QueueFromUrisAsync(spotifyUris, device);
                    }
                    else
                    {
                        result = await spotifyStreamer.PlayNowFromUrisAsync(spotifyUris, device);
                    }
                }
            }

            if (result.Success)
            {
                if (json)
                {
                    OutputJson(
                        true,
                        queue ? $"Queued {result.TracksFound} tracks" : $"Playing {result.TracksFound} tracks",
                        artist,
                        track,
                        album,
                        result.TracksFound,
                        trackSearchResult
                    );
                }
                else
                {
                    Console.WriteLine($"{_symbols.Success} {result.Message}");
                    if (result.NotFoundTracks.Any())
                    {
                        Console.WriteLine($"\n{_symbols.Error} {result.NotFoundTracks.Count} track(s) not found on Spotify");
                    }
                }
                return 0;
            }
            else
            {
                if (json)
                {
                    OutputJson(false, result.Message, artist, track, album);
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} {result.Message}");
                }
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing play command");
            if (json)
            {
                OutputJson(false, ex.Message);
            }
            else
            {
                Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
            }
            return 1;
        }
    }

    private void OutputJson(bool success, string message, string? artist = null, string? track = null, string? album = null, int? trackCount = null, Lfm.Spotify.Models.TrackSearchResult? searchResult = null)
    {
        var output = new
        {
            success = success,
            message = message,
            artist = artist,
            track = track,
            album = album,
            trackCount = trackCount,
            multipleVersionsDetected = searchResult?.HasMultipleVersions ?? false,
            albumVersions = searchResult?.AlbumVersions ?? new List<string>(),
            warning = searchResult?.HasMultipleVersions == true
                ? "Multiple versions found across different albums. Consider specifying the 'album' parameter for precise matching. Note: Users typically don't want live or greatest hits versions unless explicitly requested."
                : null
        };

        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
    }

    private void OutputJsonError(string errorMessage, string? artist = null, string? track = null, string? album = null, Lfm.Spotify.Models.TrackSearchResult? searchResult = null)
    {
        // Identify which album is likely the studio/standard version
        string? suggestedAlbum = null;
        if (searchResult?.AlbumVersions != null && searchResult.AlbumVersions.Any())
        {
            // Heuristic: prefer albums without "Live", "Greatest", "Deluxe", "Remaster" in the name
            suggestedAlbum = searchResult.AlbumVersions.FirstOrDefault(a =>
                !a.Contains("Live", StringComparison.OrdinalIgnoreCase) &&
                !a.Contains("Greatest", StringComparison.OrdinalIgnoreCase) &&
                !a.Contains("Deluxe", StringComparison.OrdinalIgnoreCase) &&
                !a.Contains("Remaster", StringComparison.OrdinalIgnoreCase)
            ) ?? searchResult.AlbumVersions.First();
        }

        var output = new
        {
            success = false,
            error = errorMessage,
            artist = artist,
            track = track,
            album = album,
            multipleVersionsDetected = true,
            albumVersions = searchResult?.AlbumVersions ?? new List<string>(),
            suggestion = $"Users typically prefer studio albums over live/greatest hits versions unless explicitly requested. The studio album appears to be '{suggestedAlbum}'. Please retry with the 'album' parameter set to your preferred version."
        };

        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Play Spotify URIs on Sonos
    /// </summary>
    private async Task<PlaylistStreamResult> PlayOnSonosAsync(List<string> spotifyUris, string roomName, bool queue)
    {
        try
        {
            if (queue)
            {
                // Queue all tracks
                foreach (var uri in spotifyUris)
                {
                    await _sonosStreamer.QueueAsync(uri, roomName);
                }
                return new PlaylistStreamResult
                {
                    Success = true,
                    Message = $"Queued {spotifyUris.Count} track(s) to Sonos room '{roomName}'",
                    TracksFound = spotifyUris.Count,
                    NotFoundTracks = new List<string>()
                };
            }
            else
            {
                // Play first track, then queue the rest
                if (spotifyUris.Any())
                {
                    await _sonosStreamer.PlayNowAsync(spotifyUris[0], roomName);

                    // Queue remaining tracks if any
                    for (int i = 1; i < spotifyUris.Count; i++)
                    {
                        await _sonosStreamer.QueueAsync(spotifyUris[i], roomName);
                    }
                }

                return new PlaylistStreamResult
                {
                    Success = true,
                    Message = $"Playing {spotifyUris.Count} track(s) on Sonos room '{roomName}'",
                    TracksFound = spotifyUris.Count,
                    NotFoundTracks = new List<string>()
                };
            }
        }
        catch (Exception ex)
        {
            return new PlaylistStreamResult
            {
                Success = false,
                Message = $"Error playing on Sonos: {ex.Message}",
                TracksFound = 0,
                NotFoundTracks = new List<string>()
            };
        }
    }
}
