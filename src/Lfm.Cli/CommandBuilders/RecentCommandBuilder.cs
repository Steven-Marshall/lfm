using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

public static class RecentCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("recent", "Get recently played tracks in chronological order");

        // Options
        var limitOption = new Option<int>(
            "--limit",
            getDefaultValue: () => 20,
            "Number of recent tracks to return (1-200)");
        limitOption.AddAlias("-l");

        var hoursOption = new Option<int?>(
            "--hours",
            "Number of hours to look back (default: 7 days)");
        hoursOption.AddAlias("-h");

        var usernameOption = new Option<string?>(
            "--user",
            "Last.fm username (uses default if not specified)");
        usernameOption.AddAlias("-u");

        var jsonOption = new Option<bool>(
            "--json",
            "Output as JSON");
        jsonOption.AddAlias("-j");

        var (forceCacheOption, forceApiOption, noCacheOption) = CommandOptionBuilders.BuildCacheOptions();

        command.AddOption(limitOption);
        command.AddOption(hoursOption);
        command.AddOption(usernameOption);
        command.AddOption(jsonOption);
        command.AddOption(forceCacheOption);
        command.AddOption(forceApiOption);
        command.AddOption(noCacheOption);

        command.SetHandler(async (int limit, int? hours, string? username, bool json, bool forceCache, bool forceApi, bool noCache) =>
        {
            var recentCommand = services.GetRequiredService<RecentCommand>();
            await recentCommand.ExecuteAsync(limit, hours, username, json, forceCache, forceApi, noCache);
        }, limitOption, hoursOption, usernameOption, jsonOption, forceCacheOption, forceApiOption, noCacheOption);

        return command;
    }
}
