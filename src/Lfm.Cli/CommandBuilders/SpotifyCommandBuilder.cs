using System.CommandLine;
using Lfm.Cli.Commands;
using Lfm.Spotify;
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

        // Activate device command
        var activateCommand = new Command("activate-device", "Wake up / activate a Spotify device to make it ready for commands");
        var deviceNameOption = new Option<string?>("--device", "Device name to activate (uses config default if not specified)");
        deviceNameOption.AddAlias("-d");
        activateCommand.AddOption(deviceNameOption);

        // Current track command
        var currentCommand = new Command("current", "Get currently playing track");
        var jsonOption = new Option<bool>("--json", "Output as JSON");
        jsonOption.AddAlias("-j");
        currentCommand.AddOption(jsonOption);

        // Pause command
        var pauseCommand = new Command("pause", "Pause current playback");

        // Resume command
        var resumeCommand = new Command("resume", "Resume paused playback");

        // Skip command
        var skipCommand = new Command("skip", "Skip to next or previous track");
        var previousOption = new Option<bool>("--previous", "Skip to previous track instead of next");
        previousOption.AddAlias("-p");
        skipCommand.AddOption(previousOption);

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

        activateCommand.SetHandler(async (string? deviceName) =>
        {
            var spotifyCommand = services.GetRequiredService<SpotifyCommand>();
            await spotifyCommand.ActivateDeviceAsync(deviceName);
        }, deviceNameOption);

        currentCommand.SetHandler(async (bool json) =>
        {
            var spotifyCommand = services.GetRequiredService<SpotifyCommand>();
            await spotifyCommand.GetCurrentTrackAsync(json);
        }, jsonOption);

        pauseCommand.SetHandler(async () =>
        {
            var spotifyCommand = services.GetRequiredService<SpotifyCommand>();
            await spotifyCommand.PauseAsync();
        });

        resumeCommand.SetHandler(async () =>
        {
            var spotifyCommand = services.GetRequiredService<SpotifyCommand>();
            await spotifyCommand.ResumeAsync();
        });

        skipCommand.SetHandler(async (bool previous) =>
        {
            var spotifyCommand = services.GetRequiredService<SpotifyCommand>();
            var direction = previous ? SkipDirection.Previous : SkipDirection.Next;
            await spotifyCommand.SkipAsync(direction);
        }, previousOption);

        // Add subcommands
        command.AddCommand(listCommand);
        command.AddCommand(deleteCommand);
        command.AddCommand(devicesCommand);
        command.AddCommand(activateCommand);
        command.AddCommand(currentCommand);
        command.AddCommand(pauseCommand);
        command.AddCommand(resumeCommand);
        command.AddCommand(skipCommand);

        return command;
    }
}