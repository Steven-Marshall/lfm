using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Cli.CommandBuilders;

public static class ArtistsCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var limitOption = new Option<int>("--limit", () => Defaults.ItemLimit, "Number of artists to display");
        limitOption.AddAlias("-l");
        
        var periodOption = new Option<string>("--period", () => Defaults.TimePeriod, "Time period: overall, 7day, 1month, 3month, 6month, 12month");
        periodOption.AddAlias("-p");
        
        var userOption = new Option<string>("--user", "Last.fm username (uses configured default if not specified)");
        userOption.AddAlias("-u");

        var rangeOption = new Option<string>("--range", "Range of positions to display (e.g., --range 10-20 for positions 10-20, both inclusive)");
        rangeOption.AddAlias("-r");

        var delayOption = new Option<int?>("--delay", "Delay between API requests in milliseconds (0 = no throttling, overrides config)");
        delayOption.AddAlias("-d");

        var verboseOption = new Option<bool>("--verbose", "Show detailed progress information");
        verboseOption.AddAlias("-v");

        var timingOption = new Option<bool>("--timing", "Show detailed API timing information (cache hits/misses and response times)");
        timingOption.AddAlias("-t");

        var timerOption = new Option<bool>("--timer", "Display total execution time");

        var (forceCacheOption, forceApiOption, noCacheOption) = CommandOptionBuilders.BuildCacheOptions();

        var command = new Command("artists", "Get your top artists with play counts")
        {
            limitOption,
            periodOption,
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
            var user = context.ParseResult.GetValueForOption(userOption);
            var range = context.ParseResult.GetValueForOption(rangeOption);
            var delay = context.ParseResult.GetValueForOption(delayOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var timing = context.ParseResult.GetValueForOption(timingOption);
            var forceCache = context.ParseResult.GetValueForOption(forceCacheOption);
            var forceApi = context.ParseResult.GetValueForOption(forceApiOption);
            var noCache = context.ParseResult.GetValueForOption(noCacheOption);
            var timer = context.ParseResult.GetValueForOption(timerOption);
            
            var artistsCommand = services.GetRequiredService<ArtistsCommand>();
            await artistsCommand.ExecuteAsync(limit, period ?? Defaults.TimePeriod, user, range, delay, verbose, timing, forceCache, forceApi, noCache, timer);
        });

        return command;
    }
}