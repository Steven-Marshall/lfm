using Lfm.Core.Services;
using Lfm.Spotify;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

public class SpotifyCommand
{
    private readonly IPlaylistStreamer _spotifyStreamer;
    private readonly ISymbolProvider _symbols;
    private readonly ILogger<SpotifyCommand> _logger;

    public SpotifyCommand(
        IPlaylistStreamer spotifyStreamer,
        ISymbolProvider symbolProvider,
        ILogger<SpotifyCommand> logger)
    {
        _spotifyStreamer = spotifyStreamer ?? throw new ArgumentNullException(nameof(spotifyStreamer));
        _symbols = symbolProvider ?? throw new ArgumentNullException(nameof(symbolProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ListPlaylistsAsync(string? pattern = null)
    {
        try
        {
            if (!await _spotifyStreamer.IsAvailableAsync())
            {
                Console.WriteLine($"{_symbols.Error} Spotify not available. Make sure you have configured Client ID and Client Secret.");
                return;
            }

            Console.WriteLine($"{_symbols.Music} Getting your Spotify playlists...");

            var playlists = await _spotifyStreamer.GetUserPlaylistsAsync();
            if (!playlists.Any())
            {
                Console.WriteLine($"{_symbols.StopSign} No playlists found.");
                return;
            }

            // Filter by pattern if provided
            if (!string.IsNullOrEmpty(pattern))
            {
                var wildcard = pattern.Replace("*", ".*");
                playlists = playlists.Where(p => System.Text.RegularExpressions.Regex.IsMatch(p.Name, wildcard, System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList();

                if (!playlists.Any())
                {
                    Console.WriteLine($"{_symbols.StopSign} No playlists match pattern '{pattern}'.");
                    return;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"{_symbols.Music} Found {playlists.Count} playlist(s):");
            Console.WriteLine();

            foreach (var playlist in playlists.OrderBy(p => p.Name))
            {
                var ownerText = playlist.IsOwned ? "owned" : "followed";
                Console.WriteLine($"  {playlist.Name} ({playlist.TracksCount} tracks, {ownerText})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing playlists");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }

    public async Task DeletePlaylistsAsync(string[] patterns, bool dryRun = false)
    {
        try
        {
            if (!await _spotifyStreamer.IsAvailableAsync())
            {
                Console.WriteLine($"{_symbols.Error} Spotify not available. Make sure you have configured Client ID and Client Secret.");
                return;
            }

            Console.WriteLine($"{_symbols.Music} Getting your Spotify playlists...");

            var allPlaylists = await _spotifyStreamer.GetUserPlaylistsAsync();
            if (!allPlaylists.Any())
            {
                Console.WriteLine($"{_symbols.StopSign} No playlists found.");
                return;
            }

            var playlistsToDelete = new List<Lfm.Spotify.Models.PlaylistInfo>();

            // Find playlists matching any of the patterns
            foreach (var pattern in patterns)
            {
                var wildcard = pattern.Replace("*", ".*");
                var matches = allPlaylists.Where(p =>
                    System.Text.RegularExpressions.Regex.IsMatch(p.Name, wildcard, System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList();

                foreach (var match in matches)
                {
                    if (!playlistsToDelete.Any(p => p.Id == match.Id))
                    {
                        playlistsToDelete.Add(match);
                    }
                }
            }

            if (!playlistsToDelete.Any())
            {
                Console.WriteLine($"{_symbols.StopSign} No playlists match the specified patterns.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"{_symbols.StopSign} Found {playlistsToDelete.Count} playlist(s) to delete:");
            Console.WriteLine();

            foreach (var playlist in playlistsToDelete.OrderBy(p => p.Name))
            {
                var ownerText = playlist.IsOwned ? "owned" : "followed";
                Console.WriteLine($"  {playlist.Name} ({playlist.TracksCount} tracks, {ownerText})");
            }

            if (dryRun)
            {
                Console.WriteLine();
                Console.WriteLine($"{_symbols.Tip} Dry run mode - no playlists were actually deleted.");
                return;
            }

            Console.WriteLine();
            Console.Write($"Are you sure you want to delete these {playlistsToDelete.Count} playlists? (y/N): ");
            var confirmation = Console.ReadLine()?.Trim().ToLower();

            if (confirmation != "y" && confirmation != "yes")
            {
                Console.WriteLine($"{_symbols.StopSign} Operation cancelled.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"{_symbols.StopSign} Deleting playlists...");

            var deleted = 0;
            var failed = 0;

            foreach (var playlist in playlistsToDelete)
            {
                var success = await _spotifyStreamer.DeletePlaylistAsync(playlist.Id);
                if (success)
                {
                    Console.WriteLine($"{_symbols.Success} Deleted: {playlist.Name}");
                    deleted++;
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} Failed to delete: {playlist.Name}");
                    failed++;
                }

                // Rate limiting
                await Task.Delay(100);
            }

            Console.WriteLine();
            Console.WriteLine($"{_symbols.Success} Deleted {deleted} playlists.");
            if (failed > 0)
            {
                Console.WriteLine($"{_symbols.Error} Failed to delete {failed} playlists.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting playlists");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }

    public async Task ListDevicesAsync()
    {
        try
        {
            if (!await _spotifyStreamer.IsAvailableAsync())
            {
                Console.WriteLine($"{_symbols.Error} Spotify not available. Make sure you have configured Client ID and Client Secret.");
                return;
            }

            Console.WriteLine($"{_symbols.Music} Getting your Spotify devices...");

            var devices = await _spotifyStreamer.GetDevicesAsync();
            if (!devices.Any())
            {
                Console.WriteLine($"{_symbols.StopSign} No Spotify devices found.");
                Console.WriteLine($"{_symbols.Tip} Open Spotify on any device to make it available.");
                return;
            }

            Console.WriteLine($"📱 Found {devices.Count} Spotify device{(devices.Count == 1 ? "" : "s")}:");
            Console.WriteLine();

            foreach (var device in devices.OrderBy(d => !d.IsActive).ThenBy(d => d.Name))
            {
                var status = device.IsActive ? $" {_symbols.Success} (active)" : "";
                var volume = device.VolumePercent.HasValue ? $" | Volume: {device.VolumePercent}%" : "";

                Console.WriteLine($"  🎵 {device.Name}");
                Console.WriteLine($"      Type: {device.Type}{status}{volume}");

                if (!string.IsNullOrEmpty(device.Id))
                {
                    Console.WriteLine($"      ID: {device.Id}");
                }

                Console.WriteLine();
            }

            Console.WriteLine($"{_symbols.Tip} Use 'lfm config set-spotify-default-device \"Device Name\"' to set a default device.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing devices");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }

    public async Task ActivateDeviceAsync(string? deviceName = null)
    {
        try
        {
            if (!await _spotifyStreamer.IsAvailableAsync())
            {
                Console.WriteLine($"{_symbols.Error} Spotify not available. Make sure you have configured Client ID and Client Secret.");
                return;
            }

            Console.WriteLine($"{_symbols.Music} Activating Spotify device...");

            var success = await _spotifyStreamer.ActivateDeviceAsync(deviceName);

            if (success)
            {
                Console.WriteLine($"{_symbols.Success} Device is now ready to receive commands!");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating device");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }
}