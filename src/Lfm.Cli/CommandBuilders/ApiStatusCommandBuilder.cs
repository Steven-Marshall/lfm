using Lfm.Cli.Commands;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

public static class ApiStatusCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("api-status", "Check the health status of Last.fm API endpoints");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Show detailed output including progress and error messages")
        {
            IsRequired = false
        };

        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output results in JSON format")
        {
            IsRequired = false
        };

        command.AddOption(verboseOption);
        command.AddOption(jsonOption);

        command.SetHandler(async (bool verbose, bool json) =>
        {
            var apiStatusCommand = services.GetRequiredService<ApiStatusCommand>();
            await apiStatusCommand.ExecuteAsync(verbose, json);
        }, verboseOption, jsonOption);

        return command;
    }
}