using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Cli.CommandBuilders;

public static class AlbumsCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var limitOption = StandardCommandOptions.CreateLimitOption("albums");
        var periodOption = StandardCommandOptions.CreatePeriodOption();
        var (fromOption, toOption, yearOption) = StandardCommandOptions.CreateDateOptions();
        var userOption = StandardCommandOptions.CreateUserOption();

        var rangeOption = new Option<string>("--range", "Range of positions to display (e.g., --range 10-20 for positions 10-20, both inclusive)");
        rangeOption.AddAlias("-r");

        var delayOption = new Option<int?>("--delay", "Delay between API requests in milliseconds (0 = no throttling, overrides config)");
        delayOption.AddAlias("-d");

        var verboseOption = StandardCommandOptions.CreateVerboseOption();
        var timingOption = new Option<bool>("--timing", "Show detailed API timing information (cache hits/misses and response times)");
        timingOption.AddAlias("-t");
        var timerOption = StandardCommandOptions.CreateTimerOption();

        var (forceCacheOption, forceApiOption, noCacheOption) = CommandOptionBuilders.BuildCacheOptions();

        var command = new Command("albums", "Get your top albums with play counts")
        {
            limitOption,
            periodOption,
            fromOption,
            toOption,
            yearOption,
            userOption,
            rangeOption,
            delayOption,
            verboseOption,
            timingOption,
            forceCacheOption,
            forceApiOption,
            noCacheOption,
            timerOption
        };

        command.SetHandler(async (context) =>
        {
            var limit = context.ParseResult.GetValueForOption(limitOption);
            var period = context.ParseResult.GetValueForOption(periodOption);
            var from = context.ParseResult.GetValueForOption(fromOption);
            var to = context.ParseResult.GetValueForOption(toOption);
            var year = context.ParseResult.GetValueForOption(yearOption);
            var user = context.ParseResult.GetValueForOption(userOption);
            var range = context.ParseResult.GetValueForOption(rangeOption);
            var delay = context.ParseResult.GetValueForOption(delayOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var timing = context.ParseResult.GetValueForOption(timingOption);
            var forceCache = context.ParseResult.GetValueForOption(forceCacheOption);
            var forceApi = context.ParseResult.GetValueForOption(forceApiOption);
            var noCache = context.ParseResult.GetValueForOption(noCacheOption);
            var timer = context.ParseResult.GetValueForOption(timerOption);
            
            var albumsCommand = services.GetRequiredService<AlbumsCommand>();
            await albumsCommand.ExecuteAsync(limit, period, user, range, delay, verbose, timing, forceCache, forceApi, noCache, timer, from, to, year);
        });

        return command;
    }
}