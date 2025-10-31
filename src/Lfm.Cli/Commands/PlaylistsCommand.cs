using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Lfm.Spotify;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

/// <summary>
/// Command for listing user's Spotify playlists
/// </summary>
public class PlaylistsCommand : BaseCommand
{
    private readonly IPlaylistStreamer _spotifyStreamer;

    public PlaylistsCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILogger<PlaylistsCommand> logger,
        ISymbolProvider symbolProvider,
        IPlaylistStreamer spotifyStreamer)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _spotifyStreamer = spotifyStreamer ?? throw new ArgumentNullException(nameof(spotifyStreamer));
    }

    /// <summary>
    /// Execute playlists listing command
    /// </summary>
    public async Task<int> ExecuteAsync(bool json = false)
    {
        try
        {
            // Check if Spotify is available
            if (!await _spotifyStreamer.IsAvailableAsync())
            {
                var error = "Spotify authentication required. Please configure Spotify credentials.";
                if (json)
                {
                    OutputJson(false, error);
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} {error}");
                    Console.WriteLine($"{_symbols.Tip} Run: lfm config set-spotify-client-id <your-client-id>");
                }
                return 1;
            }

            // Get all user playlists
            var playlists = await _spotifyStreamer.GetUserPlaylistsAsync();

            if (!playlists.Any())
            {
                if (json)
                {
                    Console.WriteLine("[]");
                }
                else
                {
                    Console.WriteLine($"{_symbols.Music} No playlists found in your Spotify library.");
                    Console.WriteLine($"{_symbols.Tip} Create playlists in Spotify or save public playlists to your library.");
                }
                return 0;
            }

            if (json)
            {
                var jsonOutput = playlists.Select(p => new
                {
                    name = p.Name,
                    trackCount = p.TracksCount,
                    isOwned = p.IsOwned
                }).ToList();
                Console.WriteLine(JsonSerializer.Serialize(jsonOutput, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine($"\n{_symbols.Music} Your Spotify Playlists ({playlists.Count} total):\n");

                foreach (var playlist in playlists)
                {
                    var ownerIndicator = playlist.IsOwned ? "(owned)" : "(followed)";
                    Console.WriteLine($"  • {playlist.Name}");
                    Console.WriteLine($"     {playlist.TracksCount} tracks {ownerIndicator}");
                }

                Console.WriteLine($"\n{_symbols.Tip} Play a playlist: lfm playlist --name \"<playlist name>\"");
            }

            return 0;
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
}
