using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Cli.CommandBuilders;

public static class AlbumsCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var limitOption = new Option<int>("--limit", () => Defaults.ItemLimit, "Number of albums to display");
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

        var command = new Command("albums", "Get your top albums with play counts")
        {
            limitOption,
            periodOption,
            userOption,
            rangeOption,
            delayOption,
            verboseOption
        };

        command.SetHandler(async (int limit, string period, string user, string range, int? delay, bool verbose) =>
        {
            var albumsCommand = services.GetRequiredService<AlbumsCommand>();
            await albumsCommand.ExecuteAsync(limit, period, user, range, delay, verbose);
        }, limitOption, periodOption, userOption, rangeOption, delayOption, verboseOption);

        return command;
    }
}