using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

public static class AlbumTracksCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("album-tracks", "Get an album's canonical tracklist from Spotify (track numbers, disc numbers, durations)");

        var artistArgument = new Argument<string>(name: "artist", description: "Artist name");
        var albumArgument = new Argument<string>(name: "album", description: "Album name");

        command.AddArgument(artistArgument);
        command.AddArgument(albumArgument);

        var exactMatchOption = new Option<bool>(
            aliases: new[] { "--exact-match", "-e" },
            description: "Force exact album name matching to resolve ambiguity (default: false)",
            getDefaultValue: () => false);

        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output results in JSON format");

        command.AddOption(exactMatchOption);
        command.AddOption(jsonOption);

        command.SetHandler(async (context) =>
        {
            var artist = context.ParseResult.GetValueForArgument(artistArgument);
            var album = context.ParseResult.GetValueForArgument(albumArgument);
            var exactMatch = context.ParseResult.GetValueForOption(exactMatchOption);
            var json = context.ParseResult.GetValueForOption(jsonOption);

            var cmd = services.GetRequiredService<AlbumTracksCommand>();
            var result = await cmd.ExecuteAsync(artist, album, exactMatch, json);
            Environment.ExitCode = result;
        });

        return command;
    }
}
