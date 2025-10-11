using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Lfm.Spotify;
using Lfm.Sonos;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

/// <summary>
/// Command for pausing playback on Spotify or Sonos
/// </summary>
public class PauseCommand : BasePlaybackCommand
{
    public PauseCommand(
        IConfigurationManager configManager,
        ILogger<PauseCommand> logger,
        ISymbolProvider symbolProvider,
        IPlaylistStreamer spotifyStreamer,
        ISonosStreamer sonosStreamer)
        : base(configManager, logger, symbolProvider, spotifyStreamer, sonosStreamer)
    {
    }

    /// <summary>
    /// Execute pause command
    /// </summary>
    public async Task<int> ExecuteAsync(string? player = null, string? room = null)
    {
        return await ExecutePlaybackActionAsync(
            player,
            room,
            async () =>
            {
                var success = await _spotifyStreamer.PauseAsync();
                if (success)
                {
                    Console.WriteLine($"⏸️  Paused");
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} Failed to pause playback");
                }
                return success;
            },
            async (targetRoom) =>
            {
                await _sonosStreamer.PauseAsync(targetRoom);
                Console.WriteLine($"⏸️  Paused Sonos room '{targetRoom}'");
            },
            "pause");
    }
}
