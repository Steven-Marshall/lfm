using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Command builder for the playlist command
/// </summary>
public static class PlaylistCommandBuilder
{
    /// <summary>
    /// Builds the playlist command for playing user's Spotify playlists
    /// </summary>
    /// <param name="services">Service provider for dependency injection</param>
    /// <returns>Configured playlist command</returns>
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("playlist", "Play a Spotify playlist by name");

        // Options (no arguments - using --name option)
        var nameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Playlist name (required)")
        {
            IsRequired = true
        };

        var deviceOption = new Option<string?>(
            aliases: new[] { "--device", "-dev" },
            description: "Spotify device to use (overrides config default, Spotify only)");

        var playerOption = new Option<string?>(
            aliases: new[] { "--player", "-p" },
            description: "Music player: Spotify or Sonos (overrides config default)");

        var roomOption = new Option<string?>(
            aliases: new[] { "--room", "-r" },
            description: "Sonos room name (overrides config default, Sonos only)");

        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output results in JSON format");

        var exactMatchOption = new Option<bool>(
            aliases: new[] { "--exact-match", "-e" },
            description: "Force exact playlist name matching to resolve ambiguity (default: false)",
            getDefaultValue: () => false);

        command.AddOption(nameOption);
        command.AddOption(deviceOption);
        command.AddOption(playerOption);
        command.AddOption(roomOption);
        command.AddOption(jsonOption);
        command.AddOption(exactMatchOption);

        // Handler
        command.SetHandler(async (context) =>
        {
            var name = context.ParseResult.GetValueForOption(nameOption);
            var device = context.ParseResult.GetValueForOption(deviceOption);
            var player = context.ParseResult.GetValueForOption(playerOption);
            var room = context.ParseResult.GetValueForOption(roomOption);
            var json = context.ParseResult.GetValueForOption(jsonOption);
            var exactMatch = context.ParseResult.GetValueForOption(exactMatchOption);

            var playlistCommand = services.GetRequiredService<PlaylistCommand>();
            var result = await playlistCommand.ExecuteAsync(name!, device, player, room, json, exactMatch);
            Environment.ExitCode = result;
        });

        return command;
    }
}
