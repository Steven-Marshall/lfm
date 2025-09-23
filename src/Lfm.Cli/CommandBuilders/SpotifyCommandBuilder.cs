using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

public static class SpotifyCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("spotify", "Manage Spotify playlists and integration");

        // List playlists command
        var listCommand = new Command("list-playlists", "List your Spotify playlists");
        var patternOption = new Option<string?>("--pattern", "Filter playlists by pattern (supports wildcards like lfm-*)");
        patternOption.AddAlias("-p");
        listCommand.AddOption(patternOption);

        // Delete playlists command
        var deleteCommand = new Command("delete-playlists", "Delete playlists by pattern (supports wildcards)");
        var patternsArg = new Argument<string[]>("patterns", "One or more patterns to match playlist names (e.g., 'lfm-*', 'Test*')");
        var dryRunOption = new Option<bool>("--dry-run", "Show what would be deleted without actually deleting");
        dryRunOption.AddAlias("-n");
        deleteCommand.AddArgument(patternsArg);
        deleteCommand.AddOption(dryRunOption);

        // List devices command
        var devicesCommand = new Command("devices", "List available Spotify devices");

        // Set up handlers
        listCommand.SetHandler(async (string? pattern) =>
        {
            var spotifyCommand = services.GetRequiredService<SpotifyCommand>();
            await spotifyCommand.ListPlaylistsAsync(pattern);
        }, patternOption);

        deleteCommand.SetHandler(async (string[] patterns, bool dryRun) =>
        {
            var spotifyCommand = services.GetRequiredService<SpotifyCommand>();
            await spotifyCommand.DeletePlaylistsAsync(patterns, dryRun);
        }, patternsArg, dryRunOption);

        devicesCommand.SetHandler(async () =>
        {
            var spotifyCommand = services.GetRequiredService<SpotifyCommand>();
            await spotifyCommand.ListDevicesAsync();
        });

        // Add subcommands
        command.AddCommand(listCommand);
        command.AddCommand(deleteCommand);
        command.AddCommand(devicesCommand);

        return command;
    }
}