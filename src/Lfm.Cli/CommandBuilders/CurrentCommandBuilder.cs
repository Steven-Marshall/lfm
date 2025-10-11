using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Command builder for the current command
/// </summary>
public static class CurrentCommandBuilder
{
    /// <summary>
    /// Builds the current command for getting current track info from Spotify or Sonos
    /// </summary>
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("current", "Get currently playing track information from Spotify or Sonos");

        // Options
        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output results in JSON format");

        var playerOption = new Option<string?>(
            aliases: new[] { "--player", "-p" },
            description: "Music player: Spotify or Sonos (overrides config default)");

        var roomOption = new Option<string?>(
            aliases: new[] { "--room", "-r" },
            description: "Sonos room name (overrides config default, Sonos only)");

        command.AddOption(jsonOption);
        command.AddOption(playerOption);
        command.AddOption(roomOption);

        // Handler
        command.SetHandler(async (bool json, string? player, string? room) =>
        {
            var currentCommand = services.GetRequiredService<CurrentCommand>();
            var result = await currentCommand.ExecuteAsync(json, player, room);
            Environment.ExitCode = result;
        }, jsonOption, playerOption, roomOption);

        return command;
    }
}
