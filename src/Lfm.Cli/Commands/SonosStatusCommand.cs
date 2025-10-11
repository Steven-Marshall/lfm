using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Lfm.Sonos;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

public class SonosStatusCommand
{
    private readonly ISonosStreamer _sonosStreamer;
    private readonly IConfigurationManager _configManager;
    private readonly ISymbolProvider _symbols;
    private readonly ILogger<SonosStatusCommand> _logger;

    public SonosStatusCommand(
        ISonosStreamer sonosStreamer,
        IConfigurationManager configManager,
        ISymbolProvider symbolProvider,
        ILogger<SonosStatusCommand> logger)
    {
        _sonosStreamer = sonosStreamer ?? throw new ArgumentNullException(nameof(sonosStreamer));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _symbols = symbolProvider ?? throw new ArgumentNullException(nameof(symbolProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(string? roomName = null, bool json = false)
    {
        try
        {
            if (!await _sonosStreamer.IsAvailableAsync())
            {
                Console.WriteLine($"{_symbols.Error} Sonos bridge not available.");
                Console.WriteLine($"{_symbols.Tip} Check your Sonos API URL configuration with: lfm config show");
                return;
            }

            // Determine which room to query
            if (string.IsNullOrWhiteSpace(roomName))
            {
                var config = await _configManager.LoadAsync();
                roomName = config.Sonos.DefaultRoom;

                if (string.IsNullOrWhiteSpace(roomName))
                {
                    Console.WriteLine($"{_symbols.Error} No room specified and no default room configured.");
                    Console.WriteLine($"{_symbols.Tip} Use --room parameter or set default with: lfm config set-sonos-default-room \"Room Name\"");
                    return;
                }
            }

            // Validate room exists
            try
            {
                await _sonosStreamer.ValidateRoomAsync(roomName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{_symbols.Error} {ex.Message}");
                Console.WriteLine($"{_symbols.Tip} List available rooms with: lfm sonos rooms");
                return;
            }

            var state = await _sonosStreamer.GetPlaybackStateAsync(roomName);

            if (state == null)
            {
                if (json)
                {
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = false, message = "Unable to get playback state" }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} Unable to get playback state for room '{roomName}'");
                }
                return;
            }

            if (json)
            {
                var result = new
                {
                    success = true,
                    room = roomName,
                    track = state.CurrentTrack,
                    artist = state.Artist,
                    album = state.Album,
                    playbackState = state.PlaybackState,
                    volume = state.Volume,
                    trackNumber = state.TrackNumber,
                    elapsedTime = state.ElapsedTime,
                    duration = state.Duration
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                var playingStatus = state.PlaybackState == "PLAYING" ? "▶️  Playing" :
                                   state.PlaybackState == "PAUSED" ? "⏸️  Paused" :
                                   "⏹️  Stopped";

                Console.WriteLine($"\n{playingStatus} in {roomName}:");

                if (!string.IsNullOrEmpty(state.CurrentTrack))
                {
                    Console.WriteLine($"  Track: {state.CurrentTrack}");
                    Console.WriteLine($"  Artist: {state.Artist}");
                    Console.WriteLine($"  Album: {state.Album}");

                    if (state.Duration.HasValue && state.Duration.Value > 0)
                    {
                        Console.WriteLine($"  Progress: {FormatTime(state.ElapsedTime ?? 0)} / {FormatTime(state.Duration.Value)}");
                    }

                    Console.WriteLine($"  Volume: {state.Volume}%");
                    Console.WriteLine($"  Track #: {state.TrackNumber}");
                }
                else
                {
                    Console.WriteLine($"  No track information available");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Sonos status");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }

    private static string FormatTime(int seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return $"{(int)span.TotalMinutes}:{span.Seconds:D2}";
    }
}
