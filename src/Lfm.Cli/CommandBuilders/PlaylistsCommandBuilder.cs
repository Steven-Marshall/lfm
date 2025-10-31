using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Command builder for the playlists command
/// </summary>
public static class PlaylistsCommandBuilder
{
    /// <summary>
    /// Builds the playlists command for listing user's Spotify playlists
    /// </summary>
    /// <param name="services">Service provider for dependency injection</param>
    /// <returns>Configured playlists command</returns>
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("playlists", "List all your Spotify playlists");

        // Options
        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output results in JSON format");

        command.AddOption(jsonOption);

        // Handler
        command.SetHandler(async (context) =>
        {
            var json = context.ParseResult.GetValueForOption(jsonOption);

            var playlistsCommand = services.GetRequiredService<PlaylistsCommand>();
            var result = await playlistsCommand.ExecuteAsync(json);
            Environment.ExitCode = result;
        });

        return command;
    }
}
