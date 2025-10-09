using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Lfm.Spotify;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

/// <summary>
/// Command for playing tracks or albums immediately or adding to queue
/// </summary>
public class PlayCommand : BaseCommand
{
    private readonly IPlaylistStreamer _spotifyStreamer;

    public PlayCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILogger<PlayCommand> logger,
        ISymbolProvider symbolProvider,
        IPlaylistStreamer spotifyStreamer)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _spotifyStreamer = spotifyStreamer ?? throw new ArgumentNullException(nameof(spotifyStreamer));
    }

    /// <summary>
    /// Execute play/queue command for a track or album
    /// </summary>
    public async Task<int> ExecuteAsync(
        string artist,
        string? track = null,
        string? album = null,
        string? device = null,
        bool queue = false,
        bool json = false)
    {
        try
        {
            // Validate Spotify availability
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

                // Check for multiple versions if no album specified
                if (_spotifyStreamer is SpotifyStreamer streamer && string.IsNullOrWhiteSpace(album))
                {
                    var trackToSearch = new Track
                    {
                        Name = track,
                        Artist = new ArtistInfo { Name = artist }
                    };

                    trackSearchResult = await streamer.SearchSpotifyTrackWithDetailsAsync(trackToSearch, null);

                    // If multiple versions detected, fail and ask LLM to specify album
                    if (trackSearchResult.HasMultipleVersions)
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
                }

                // If album was specified with track, search with album filter first
                if (_spotifyStreamer is SpotifyStreamer spotifyStreamer && !string.IsNullOrWhiteSpace(album))
                {
                    // Use album-aware track search
                    var trackToSearch = new Track
                    {
                        Name = track,
                        Artist = new ArtistInfo { Name = artist }
                    };

                    var searchResult = await spotifyStreamer.SearchSpotifyTrackWithDetailsAsync(trackToSearch, album);

                    if (searchResult.SpotifyUri == null)
                    {
                        var error = $"Track not found on album: {artist} - {track} ({album})";
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

                    // Play the specific track URI we found
                    var spotifyUris = new List<string> { searchResult.SpotifyUri };

                    if (queue)
                    {
                        result = await spotifyStreamer.QueueFromUrisAsync(spotifyUris, device);
                    }
                    else
                    {
                        result = await spotifyStreamer.PlayNowFromUrisAsync(spotifyUris, device);
                    }
                }
                else
                {
                    // No album specified, use normal track search
                    var tracksToPlay = new List<Track>
                    {
                        new Track
                        {
                            Name = track,
                            Artist = new ArtistInfo { Name = artist }
                        }
                    };

                    if (queue)
                    {
                        result = await _spotifyStreamer.QueueTracksAsync(tracksToPlay, device);
                    }
                    else
                    {
                        result = await _spotifyStreamer.PlayNowAsync(tracksToPlay, device);
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

                // Search for album on Spotify and get all track URIs
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

                // Play or queue the album tracks
                if (queue)
                {
                    result = await spotifyStreamer.QueueFromUrisAsync(spotifyUris, device);
                }
                else
                {
                    result = await spotifyStreamer.PlayNowFromUrisAsync(spotifyUris, device);
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
}
