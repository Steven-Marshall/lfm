using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

public static class SonosCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("sonos", "Manage Sonos integration and playback");

        // Rooms command - list available Sonos rooms
        var roomsCommand = new Command("rooms", "List available Sonos rooms");

        // Status command - show playback status
        var statusCommand = new Command("status", "Show current playback status");
        var roomOption = new Option<string?>("--room", "Sonos room name (uses default if not specified)");
        roomOption.AddAlias("-r");
        var jsonOption = new Option<bool>("--json", "Output as JSON");
        jsonOption.AddAlias("-j");
        statusCommand.AddOption(roomOption);
        statusCommand.AddOption(jsonOption);

        // Set up handlers
        roomsCommand.SetHandler(async () =>
        {
            var roomsCmd = services.GetRequiredService<SonosRoomsCommand>();
            await roomsCmd.ExecuteAsync();
        });

        statusCommand.SetHandler(async (string? room, bool json) =>
        {
            var statusCmd = services.GetRequiredService<SonosStatusCommand>();
            await statusCmd.ExecuteAsync(room, json);
        }, roomOption, jsonOption);

        // Add subcommands
        command.AddCommand(roomsCommand);
        command.AddCommand(statusCommand);

        return command;
    }
}
