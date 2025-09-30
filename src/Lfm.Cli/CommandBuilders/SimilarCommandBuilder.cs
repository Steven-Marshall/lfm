using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Command builder for the similar command
/// </summary>
public static class SimilarCommandBuilder
{
    /// <summary>
    /// Builds the similar command for finding artists similar to a specified artist
    /// </summary>
    /// <param name="services">Service provider for dependency injection</param>
    /// <returns>Configured similar command</returns>
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("similar", "Find artists similar to a specified artist");

        // Arguments
        var artistArgument = new Argument<string>(
            name: "artist",
            description: "Artist name to find similar artists for");

        command.AddArgument(artistArgument);

        // Options
        var limitOption = new Option<int>(
            aliases: new[] { "--limit", "-l" },
            description: "Number of similar artists to return (default: 20, max: 100)",
            getDefaultValue: () => 20);

        var timingOption = new Option<bool>(
            aliases: new[] { "--timing", "-t" },
            description: "Show API response times");

        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output in JSON format (for MCP integration)");

        command.AddOption(limitOption);
        command.AddOption(timingOption);
        command.AddOption(jsonOption);

        // Handler
        command.SetHandler(async (string artist, int limit, bool timing, bool json) =>
        {
            // Validate limit
            if (limit < 1 || limit > 100)
            {
                Console.Error.WriteLine("Error: Limit must be between 1 and 100");
                Environment.ExitCode = 1;
                return;
            }

            var similarCommand = services.GetRequiredService<SimilarCommand>();
            var result = await similarCommand.ExecuteAsync(artist, limit, timing, json);
            Environment.ExitCode = result;

        }, artistArgument, limitOption, timingOption, jsonOption);

        return command;
    }
}