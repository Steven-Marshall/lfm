using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

public static class NewReleasesCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("new-releases", "Get new album releases from Spotify");

        // Options
        var limitOption = new Option<int>(
            "--limit",
            getDefaultValue: () => 50,
            "Number of new releases to return (1-50)");
        limitOption.AddAlias("-l");

        var jsonOption = new Option<bool>(
            "--json",
            "Output as JSON");
        jsonOption.AddAlias("-j");

        var timerOption = new Option<bool>(
            "--timer",
            "Display execution timer");

        command.AddOption(limitOption);
        command.AddOption(jsonOption);
        command.AddOption(timerOption);

        command.SetHandler(async (int limit, bool json, bool timer) =>
        {
            var newReleasesCommand = services.GetRequiredService<NewReleasesCommand>();
            await newReleasesCommand.ExecuteAsync(limit, json, timer);
        }, limitOption, jsonOption, timerOption);

        return command;
    }
}
