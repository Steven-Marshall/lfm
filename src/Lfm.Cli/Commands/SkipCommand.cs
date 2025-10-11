using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Lfm.Spotify;
using Lfm.Sonos;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

/// <summary>
/// Command for skipping tracks on Spotify or Sonos
/// </summary>
public class SkipCommand : BasePlaybackCommand
{
    public SkipCommand(
        IConfigurationManager configManager,
        ILogger<SkipCommand> logger,
        ISymbolProvider symbolProvider,
        IPlaylistStreamer spotifyStreamer,
        ISonosStreamer sonosStreamer)
        : base(configManager, logger, symbolProvider, spotifyStreamer, sonosStreamer)
    {
    }

    /// <summary>
    /// Execute skip command
    /// </summary>
    public async Task<int> ExecuteAsync(bool previous = false, string? player = null, string? room = null)
    {
        var direction = previous ? Lfm.Spotify.SkipDirection.Previous : Lfm.Spotify.SkipDirection.Next;
        var directionText = previous ? "previous track" : "next track";

        return await ExecutePlaybackActionAsync(
            player,
            room,
            async () =>
            {
                var success = await _spotifyStreamer.SkipAsync(direction);
                if (success)
                {
                    Console.WriteLine($"⏭️  Skipped to {directionText}");
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} Failed to skip track");
                }
                return success;
            },
            async (targetRoom) =>
            {
                // Convert Spotify SkipDirection to Sonos SkipDirection
                var sonosDirection = direction == Lfm.Spotify.SkipDirection.Next
                    ? Lfm.Sonos.SkipDirection.Next
                    : Lfm.Sonos.SkipDirection.Previous;

                await _sonosStreamer.SkipAsync(targetRoom, sonosDirection);
                Console.WriteLine($"⏭️  Skipped to {directionText} on Sonos room '{targetRoom}'");
            },
            "skip");
    }
}
