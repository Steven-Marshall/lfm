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

            // Validate that either track or album is provided, but not both
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

            if (!string.IsNullOrWhiteSpace(track) && !string.IsNullOrWhiteSpace(album))
            {
                var error = "Cannot specify both track and album. Choose one.";
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

            // Build track list
            List<Track> tracksToPlay;
            if (!string.IsNullOrWhiteSpace(track))
            {
                // Single track mode
                tracksToPlay = new List<Track>
                {
                    new Track
                    {
                        Name = track,
                        Artist = new ArtistInfo { Name = artist }
                    }
                };
            }
            else
            {
                // Album mode - use Spotify API to get album tracks
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

                // Note: SearchSpotifyAlbumTracksAsync is internal to SpotifyStreamer
                // For now, we'll need to use a workaround - create Track objects for Last.fm album lookup
                // and rely on Spotify search to find them

                // Get album info from Last.fm to get track listing
                var config = await _configManager.LoadAsync();
                var albumInfo = await _apiClient.GetAlbumInfoAsync(artist, album!, config.DefaultUsername ?? "");
                if (albumInfo == null || albumInfo.Album.Tracks?.Track == null || !albumInfo.Album.Tracks.Track.Any())
                {
                    var error = $"Album not found or has no tracks: {artist} - {album}";
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

                // Convert album tracks to Track objects
                tracksToPlay = albumInfo.Album.Tracks.Track.Select(t => new Track
                {
                    Name = t.Name,
                    Artist = new ArtistInfo { Name = artist }
                }).ToList();
            }

            // Execute based on mode: play now (interrupt current) or queue (add to end)
            PlaylistStreamResult result;
            if (queue)
            {
                // Queue mode: add to end of current queue
                result = await _spotifyStreamer.QueueTracksAsync(tracksToPlay, device);
            }
            else
            {
                // Play now mode: interrupt current playback and start immediately
                result = await _spotifyStreamer.PlayNowAsync(tracksToPlay, device);
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
                        result.TracksFound
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

    private void OutputJson(bool success, string message, string? artist = null, string? track = null, string? album = null, int? trackCount = null)
    {
        var output = new
        {
            success = success,
            message = message,
            artist = artist,
            track = track,
            album = album,
            trackCount = trackCount
        };

        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
    }
}
