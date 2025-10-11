using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Command builder for the play command
/// </summary>
public static class PlayCommandBuilder
{
    /// <summary>
    /// Builds the play command for playing or queueing tracks/albums on Spotify
    /// </summary>
    /// <param name="services">Service provider for dependency injection</param>
    /// <returns>Configured play command</returns>
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("play", "Play or queue a track or album on Spotify or Sonos");

        // Arguments
        var artistArgument = new Argument<string>(
            name: "artist",
            description: "Artist name");

        command.AddArgument(artistArgument);

        // Options
        var trackOption = new Option<string?>(
            aliases: new[] { "--track", "-t" },
            description: "Track name to play or queue");

        var albumOption = new Option<string?>(
            aliases: new[] { "--album", "-a" },
            description: "Album name to play or queue");

        var deviceOption = new Option<string?>(
            aliases: new[] { "--device", "-dev" },
            description: "Spotify device to use (overrides config default, Spotify only)");

        var playerOption = new Option<string?>(
            aliases: new[] { "--player", "-p" },
            description: "Music player: Spotify or Sonos (overrides config default)");

        var roomOption = new Option<string?>(
            aliases: new[] { "--room", "-r" },
            description: "Sonos room name (overrides config default, Sonos only)");

        var queueOption = new Option<bool>(
            aliases: new[] { "--queue", "-q" },
            description: "Add to queue instead of playing immediately (default: false = play now)",
            getDefaultValue: () => false);

        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output results in JSON format");

        command.AddOption(trackOption);
        command.AddOption(albumOption);
        command.AddOption(deviceOption);
        command.AddOption(playerOption);
        command.AddOption(roomOption);
        command.AddOption(queueOption);
        command.AddOption(jsonOption);

        // Handler
        command.SetHandler(async (string artist, string? track, string? album, string? device, string? player, string? room, bool queue, bool json) =>
        {
            var playCommand = services.GetRequiredService<PlayCommand>();
            var result = await playCommand.ExecuteAsync(artist, track, album, device, player, room, queue, json);
            Environment.ExitCode = result;

        }, artistArgument, trackOption, albumOption, deviceOption, playerOption, roomOption, queueOption, jsonOption);

        return command;
    }
}
