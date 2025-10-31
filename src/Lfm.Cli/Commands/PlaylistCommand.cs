using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Lfm.Spotify;
using Lfm.Spotify.Models;
using Lfm.Sonos;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

/// <summary>
/// Command for playing user's Spotify playlists
/// </summary>
public class PlaylistCommand : BaseCommand
{
    private readonly IPlaylistStreamer _spotifyStreamer;
    private readonly ISonosStreamer _sonosStreamer;

    public PlaylistCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILogger<PlaylistCommand> logger,
        ISymbolProvider symbolProvider,
        IPlaylistStreamer spotifyStreamer,
        ISonosStreamer sonosStreamer)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _spotifyStreamer = spotifyStreamer ?? throw new ArgumentNullException(nameof(spotifyStreamer));
        _sonosStreamer = sonosStreamer ?? throw new ArgumentNullException(nameof(sonosStreamer));
    }

    /// <summary>
    /// Execute playlist playback command
    /// </summary>
    public async Task<int> ExecuteAsync(
        string name,
        string? device = null,
        string? player = null,
        string? room = null,
        bool json = false,
        bool exactMatch = false)
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

            // Validate Sonos configuration if using Sonos
            if (targetPlayer == PlayerType.Sonos)
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
                        Console.WriteLine($"{_symbols.Tip} Set default room with: lfm config set-sonos-default-room \"Room Name\"");
                    }
                    return 1;
                }
            }

            // Search for playlist by name
            var playlistSearchResult = await _spotifyStreamer.SearchPlaylistByNameAsync(name, exactMatch);

            // Check if multiple matches detected
            if (playlistSearchResult.HasMultipleMatches)
            {
                var errorMessage = $"Multiple playlists found. Please use --exact-match flag to select a specific playlist.";
                if (json)
                {
                    OutputJsonPlaylistError(errorMessage, name, playlistSearchResult);
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} {errorMessage}");
                    Console.WriteLine($"\nAvailable playlists:");
                    foreach (var playlist in playlistSearchResult.Playlists)
                    {
                        Console.WriteLine($"  - {playlist.Name} ({playlist.TracksCount} tracks)");
                    }
                }
                return 1;
            }

            // Check if no matches found
            if (string.IsNullOrEmpty(playlistSearchResult.PlaylistUri))
            {
                var error = $"Playlist '{name}' not found in your Spotify library.";
                if (json)
                {
                    OutputJson(false, error);
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} {error}");
                    Console.WriteLine($"{_symbols.Tip} Make sure you've created or saved this playlist in Spotify.");
                }
                return 1;
            }

            // Get the playlist ID from the URI
            var playlistId = playlistSearchResult.Playlists.First().Id;
            var playlistName = playlistSearchResult.Playlists.First().Name;
            var trackCount = playlistSearchResult.Playlists.First().TracksCount;

            // Play on the selected player
            if (targetPlayer == PlayerType.Spotify)
            {
                // Use Spotify
                var result = await _spotifyStreamer.PlayPlaylistAsync(playlistId, device);

                if (result.Success)
                {
                    if (json)
                    {
                        OutputJson(true, $"Now playing playlist '{playlistName}' on Spotify", new
                        {
                            playlistName = playlistName,
                            trackCount = trackCount,
                            player = "Spotify",
                            device = device ?? "(default)"
                        });
                    }
                    else
                    {
                        Console.WriteLine($"{_symbols.Success} Now playing playlist '{playlistName}' ({trackCount} tracks) on Spotify");
                        if (!string.IsNullOrWhiteSpace(device))
                        {
                            Console.WriteLine($"   Device: {device}");
                        }
                    }
                    return 0;
                }
                else
                {
                    var error = $"Failed to play playlist on Spotify: {result.Message}";
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
                // Use Sonos
                await _sonosStreamer.PlayPlaylistAsync(playlistId, targetRoom!);

                if (json)
                {
                    OutputJson(true, $"Now playing playlist '{playlistName}' on Sonos", new
                    {
                        playlistName = playlistName,
                        trackCount = trackCount,
                        player = "Sonos",
                        room = targetRoom
                    });
                }
                else
                {
                    Console.WriteLine($"{_symbols.Success} Now playing playlist '{playlistName}' ({trackCount} tracks) on Sonos");
                    Console.WriteLine($"   Room: {targetRoom}");
                }
                return 0;
            }
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error: {ex.Message}";
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

    private void OutputJson(bool success, string message, object? data = null)
    {
        var output = new
        {
            success = success,
            message = message,
            data = data
        };
        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void OutputJsonPlaylistError(string errorMessage, string name, PlaylistSearchResult searchResult)
    {
        var output = new
        {
            success = false,
            error = errorMessage,
            playlistName = name,
            multipleMatchesDetected = true,
            playlists = searchResult.Playlists.Select(p => new
            {
                name = p.Name,
                trackCount = p.TracksCount
            }).ToList(),
            suggestion = $"Multiple playlists found. Use --exact-match flag for exact name matching."
        };
        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
    }
}
