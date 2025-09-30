using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

public static class CreatePlaylistCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("create-playlist", "Create a Spotify playlist from a list of artist/track pairs");

        // Arguments
        var inputArgument = new Argument<string>(
            name: "input",
            description: "Track list in comma-separated format (artist,track;artist2,track2) or JSON format (use --json flag)");

        command.AddArgument(inputArgument);

        // Options
        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Input is in JSON format: [{\"artist\":\"...\",\"track\":\"...\"}]");

        var playlistOption = new Option<string?>(
            aliases: new[] { "--playlist", "-pl" },
            description: "Playlist name (will be prefixed with 'lfm' if not already)");

        var playNowOption = new Option<bool>(
            aliases: new[] { "--playnow" },
            description: "Queue tracks to Spotify and start playing immediately");

        var shuffleOption = new Option<bool>(
            aliases: new[] { "--shuffle", "-s" },
            description: "Shuffle the track order when sending to Spotify");

        var deviceOption = new Option<string?>(
            aliases: new[] { "--device", "-dev" },
            description: "Specific Spotify device to use (overrides config default)");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Show detailed information about track lookups");

        var timingOption = new Option<bool>(
            aliases: new[] { "--timing", "-t" },
            description: "Show API timing information");

        var timerOption = new Option<bool>(
            aliases: new[] { "--timer" },
            description: "Show total execution time");

        command.AddOption(jsonOption);
        command.AddOption(playlistOption);
        command.AddOption(playNowOption);
        command.AddOption(shuffleOption);
        command.AddOption(deviceOption);
        command.AddOption(verboseOption);
        command.AddOption(timingOption);
        command.AddOption(timerOption);

        // Handler
        command.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForArgument(inputArgument);
            var isJson = context.ParseResult.GetValueForOption(jsonOption);
            var playlistName = context.ParseResult.GetValueForOption(playlistOption);
            var playNow = context.ParseResult.GetValueForOption(playNowOption);
            var shuffle = context.ParseResult.GetValueForOption(shuffleOption);
            var device = context.ParseResult.GetValueForOption(deviceOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var timing = context.ParseResult.GetValueForOption(timingOption);
            var timer = context.ParseResult.GetValueForOption(timerOption);

            var createPlaylistCommand = services.GetRequiredService<CreatePlaylistCommand>();

            await createPlaylistCommand.ExecuteAsync(
                input,
                playlistName,
                isJson,
                playNow,
                shuffle,
                device,
                verbose,
                timing,
                timer);
        });

        return command;
    }
}