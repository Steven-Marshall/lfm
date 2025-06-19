using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

public static class CacheClearCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var expiredOption = new Option<bool>("--expired", "Remove only expired cache entries");
        expiredOption.AddAlias("-e");

        var confirmOption = new Option<bool>("--yes", "Skip confirmation prompt");
        confirmOption.AddAlias("-y");

        var command = new Command("cache-clear", "Clear cache entries")
        {
            expiredOption,
            confirmOption
        };

        command.SetHandler(async (bool expired, bool confirm) =>
        {
            var cacheClearCommand = services.GetRequiredService<CacheClearCommand>();
            await cacheClearCommand.ExecuteAsync(expired, confirm);
        }, expiredOption, confirmOption);

        return command;
    }
}