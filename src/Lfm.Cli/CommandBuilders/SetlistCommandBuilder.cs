using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Command builder for the setlist command
/// </summary>
public static class SetlistCommandBuilder
{
    /// <summary>
    /// Builds the setlist command for retrieving a specific concert setlist
    /// </summary>
    /// <param name="services">Service provider for dependency injection</param>
    /// <returns>Configured setlist command</returns>
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("setlist", "Get details for a specific concert setlist");

        // Arguments
        var setlistIdArgument = new Argument<string>(
            name: "setlist-id",
            description: "Setlist ID (get from concerts search)");

        command.AddArgument(setlistIdArgument);

        // Options
        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output results in JSON format");

        command.AddOption(jsonOption);

        // Handler
        command.SetHandler(async (string setlistId, bool json) =>
        {
            var setlistCommand = services.GetRequiredService<SetlistCommand>();
            await setlistCommand.ExecuteAsync(setlistId, json);
        }, setlistIdArgument, jsonOption);

        return command;
    }
}
