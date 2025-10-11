using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Lfm.Spotify;
using Lfm.Sonos;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

/// <summary>
/// Command for resuming playback on Spotify or Sonos
/// </summary>
public class ResumeCommand : BasePlaybackCommand
{
    public ResumeCommand(
        IConfigurationManager configManager,
        ILogger<ResumeCommand> logger,
        ISymbolProvider symbolProvider,
        IPlaylistStreamer spotifyStreamer,
        ISonosStreamer sonosStreamer)
        : base(configManager, logger, symbolProvider, spotifyStreamer, sonosStreamer)
    {
    }

    /// <summary>
    /// Execute resume command
    /// </summary>
    public async Task<int> ExecuteAsync(string? player = null, string? room = null)
    {
        return await ExecutePlaybackActionAsync(
            player,
            room,
            async () =>
            {
                var success = await _spotifyStreamer.ResumeAsync();
                if (success)
                {
                    Console.WriteLine($"▶️  Resumed");
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} Failed to resume playback");
                }
                return success;
            },
            async (targetRoom) =>
            {
                await _sonosStreamer.ResumeAsync(targetRoom);
                Console.WriteLine($"▶️  Resumed Sonos room '{targetRoom}'");
            },
            "resume");
    }
}
