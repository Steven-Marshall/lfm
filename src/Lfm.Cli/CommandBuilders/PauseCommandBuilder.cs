using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Command builder for the pause command
/// </summary>
public static class PauseCommandBuilder
{
    /// <summary>
    /// Builds the pause command for pausing playback on Spotify or Sonos
    /// </summary>
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("pause", "Pause current playback on Spotify or Sonos");

        // Options
        var playerOption = new Option<string?>(
            aliases: new[] { "--player", "-p" },
            description: "Music player: Spotify or Sonos (overrides config default)");

        var roomOption = new Option<string?>(
            aliases: new[] { "--room", "-r" },
            description: "Sonos room name (overrides config default, Sonos only)");

        command.AddOption(playerOption);
        command.AddOption(roomOption);

        // Handler
        command.SetHandler(async (string? player, string? room) =>
        {
            var pauseCommand = services.GetRequiredService<PauseCommand>();
            var result = await pauseCommand.ExecuteAsync(player, room);
            Environment.ExitCode = result;
        }, playerOption, roomOption);

        return command;
    }
}
