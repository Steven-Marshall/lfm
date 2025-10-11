using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Command builder for the skip command
/// </summary>
public static class SkipCommandBuilder
{
    /// <summary>
    /// Builds the skip command for skipping tracks on Spotify or Sonos
    /// </summary>
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("skip", "Skip to next or previous track on Spotify or Sonos");

        // Options
        var previousOption = new Option<bool>(
            aliases: new[] { "--previous", "--prev" },
            description: "Skip to previous track (default: skip to next)");

        var playerOption = new Option<string?>(
            aliases: new[] { "--player", "-p" },
            description: "Music player: Spotify or Sonos (overrides config default)");

        var roomOption = new Option<string?>(
            aliases: new[] { "--room", "-r" },
            description: "Sonos room name (overrides config default, Sonos only)");

        command.AddOption(previousOption);
        command.AddOption(playerOption);
        command.AddOption(roomOption);

        // Handler
        command.SetHandler(async (bool previous, string? player, string? room) =>
        {
            var skipCommand = services.GetRequiredService<SkipCommand>();
            var result = await skipCommand.ExecuteAsync(previous, player, room);
            Environment.ExitCode = result;
        }, previousOption, playerOption, roomOption);

        return command;
    }
}
