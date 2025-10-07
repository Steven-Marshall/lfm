using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Command builder for the check command
/// </summary>
public static class CheckCommandBuilder
{
    /// <summary>
    /// Builds the check command for verifying user's listening history
    /// </summary>
    /// <param name="services">Service provider for dependency injection</param>
    /// <returns>Configured check command</returns>
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("check", "Check if you have listened to an artist, track, or album");

        // Arguments
        var artistArgument = new Argument<string>(
            name: "artist",
            description: "Artist name to check");

        var trackArgument = new Argument<string?>(
            name: "track",
            description: "Track name to check (optional - if not provided, checks artist only)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        command.AddArgument(artistArgument);
        command.AddArgument(trackArgument);

        // Options
        var albumOption = new Option<string?>(
            aliases: new[] { "--album", "-a" },
            description: "Album name to check (requires artist parameter)");

        var usernameOption = new Option<string?>(
            aliases: new[] { "--user", "-u" },
            description: "Last.fm username (uses default if not specified)");

        var timingOption = new Option<bool>(
            aliases: new[] { "--timing", "-t" },
            description: "Show API response times");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Show additional information (for albums: includes track-level breakdown)");

        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output results in JSON format (for albums only)");

        command.AddOption(albumOption);
        command.AddOption(usernameOption);
        command.AddOption(timingOption);
        command.AddOption(verboseOption);
        command.AddOption(jsonOption);

        // Handler
        command.SetHandler(async (string artist, string? track, string? album, string? username, bool timing, bool verbose, bool json) =>
        {
            var checkCommand = services.GetRequiredService<CheckCommand>();

            int result;
            if (!string.IsNullOrEmpty(album))
            {
                // Album check
                result = await checkCommand.ExecuteAsync(artist, album, username, timing, verbose, json);
            }
            else if (!string.IsNullOrEmpty(track))
            {
                // Track check
                result = await checkCommand.ExecuteAsync(artist, track, username, timing, verbose);
            }
            else
            {
                // Artist check only
                result = await checkCommand.ExecuteAsync(artist, username, timing, verbose);
            }

            Environment.ExitCode = result;

        }, artistArgument, trackArgument, albumOption, usernameOption, timingOption, verboseOption, jsonOption);

        return command;
    }
}