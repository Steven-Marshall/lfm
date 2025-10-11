using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Lfm.Spotify;
using Lfm.Sonos;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

/// <summary>
/// Command for getting current track info from Spotify or Sonos
/// </summary>
public class CurrentCommand : BasePlaybackCommand
{
    public CurrentCommand(
        IConfigurationManager configManager,
        ILogger<CurrentCommand> logger,
        ISymbolProvider symbolProvider,
        IPlaylistStreamer spotifyStreamer,
        ISonosStreamer sonosStreamer)
        : base(configManager, logger, symbolProvider, spotifyStreamer, sonosStreamer)
    {
    }

    /// <summary>
    /// Execute current track command
    /// </summary>
    public async Task<int> ExecuteAsync(bool json = false, string? player = null, string? room = null)
    {
        try
        {
            // Load config to determine player
            var config = await _configManager.LoadAsync();

            // Determine which player to use
            var targetPlayer = DetermineTargetPlayer(player, config);
            if (targetPlayer == null)
            {
                if (json)
                {
                    OutputJson(false, "Invalid player specified");
                }
                return 1;
            }

            // Route to appropriate player
            if (targetPlayer == PlayerType.Spotify)
            {
                return await GetSpotifyCurrentTrackAsync(json);
            }
            else // Sonos
            {
                return await GetSonosCurrentTrackAsync(json, room, config);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing current command");
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

    private async Task<int> GetSpotifyCurrentTrackAsync(bool json)
    {
        if (!await _spotifyStreamer.IsAvailableAsync())
        {
            if (json)
            {
                OutputJson(false, "Spotify is not configured or authenticated");
            }
            else
            {
                Console.WriteLine($"{_symbols.Error} Spotify is not configured or authenticated. Please run 'lfm spotify auth' first.");
            }
            return 1;
        }

        var currentTrack = await _spotifyStreamer.GetCurrentlyPlayingAsync();

        if (currentTrack == null)
        {
            if (json)
            {
                OutputJson(false, "No track currently playing");
            }
            else
            {
                Console.WriteLine($"{_symbols.Music} No track currently playing");
            }
            return 0;
        }

        if (json)
        {
            var result = new
            {
                success = true,
                player = "Spotify",
                track = currentTrack.TrackName,
                artist = currentTrack.ArtistName,
                album = currentTrack.AlbumName,
                progressMs = currentTrack.ProgressMs,
                durationMs = currentTrack.DurationMs,
                isPlaying = currentTrack.IsPlaying,
                device = currentTrack.DeviceName,
                deviceType = currentTrack.DeviceType
            };
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            var playingStatus = currentTrack.IsPlaying ? "▶️  Playing" : "⏸️  Paused";
            Console.WriteLine($"\n{playingStatus}:");
            Console.WriteLine($"  Track: {currentTrack.TrackName}");
            Console.WriteLine($"  Artist: {currentTrack.ArtistName}");
            Console.WriteLine($"  Album: {currentTrack.AlbumName}");
            Console.WriteLine($"  Progress: {FormatTime(currentTrack.ProgressMs)} / {FormatTime(currentTrack.DurationMs)}");
            Console.WriteLine($"  Device: {currentTrack.DeviceName} ({currentTrack.DeviceType})");
        }
        return 0;
    }

    private async Task<int> GetSonosCurrentTrackAsync(bool json, string? room, LfmConfig config)
    {
        if (!await _sonosStreamer.IsAvailableAsync())
        {
            if (json)
            {
                OutputJson(false, $"Sonos bridge not available at {config.Sonos.HttpApiBaseUrl}");
            }
            else
            {
                Console.WriteLine($"{_symbols.Error} Sonos bridge not available at {config.Sonos.HttpApiBaseUrl}. Check your configuration.");
                Console.WriteLine($"{_symbols.Tip} Set bridge URL with: lfm config set-sonos-api-url <url>");
            }
            return 1;
        }

        // Validate Sonos room
        var (isValid, targetRoom) = await ValidateSonosRoomAsync(room, config);
        if (!isValid)
        {
            if (json)
            {
                OutputJson(false, "No Sonos room specified and no default room configured");
            }
            return 1;
        }

        var state = await _sonosStreamer.GetPlaybackStateAsync(targetRoom!);

        if (state == null || string.IsNullOrEmpty(state.CurrentTrack))
        {
            if (json)
            {
                OutputJson(false, $"No track currently playing on Sonos room '{targetRoom}'");
            }
            else
            {
                Console.WriteLine($"{_symbols.Music} No track currently playing on Sonos room '{targetRoom}'");
            }
            return 0;
        }

        if (json)
        {
            var result = new
            {
                success = true,
                player = "Sonos",
                room = targetRoom,
                track = state.CurrentTrack,
                artist = state.Artist,
                album = state.Album,
                playbackState = state.PlaybackState,
                volume = state.Volume,
                trackNumber = state.TrackNumber,
                elapsedTime = state.ElapsedTime,
                duration = state.Duration
            };
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            var playingStatus = state.PlaybackState == "PLAYING" ? "▶️  Playing" : state.PlaybackState == "PAUSED_PLAYBACK" ? "⏸️  Paused" : state.PlaybackState;
            Console.WriteLine($"\n{playingStatus} (Sonos - {targetRoom}):");
            Console.WriteLine($"  Track: {state.CurrentTrack}");
            Console.WriteLine($"  Artist: {state.Artist}");
            Console.WriteLine($"  Album: {state.Album}");
            if (state.Duration.HasValue && state.Duration.Value > 0)
            {
                var elapsed = state.ElapsedTime.HasValue ? state.ElapsedTime.Value : 0;
                Console.WriteLine($"  Progress: {FormatTime(elapsed * 1000)} / {FormatTime(state.Duration.Value * 1000)}");
            }
            Console.WriteLine($"  Volume: {state.Volume}%");
        }
        return 0;
    }

    private void OutputJson(bool success, string message)
    {
        var result = new { success, message };
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string FormatTime(int milliseconds)
    {
        var span = TimeSpan.FromMilliseconds(milliseconds);
        return $"{(int)span.TotalMinutes}:{span.Seconds:D2}";
    }
}
