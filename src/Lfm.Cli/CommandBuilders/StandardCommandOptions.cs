using System.CommandLine;
using Lfm.Core.Configuration;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Factory class for creating standardized command options to eliminate duplication
/// </summary>
public static class StandardCommandOptions
{
    public static Option<int> CreateLimitOption(string itemType, int defaultValue = Defaults.ItemLimit)
    {
        var option = new Option<int>("--limit", () => defaultValue, $"Number of {itemType} to display");
        option.AddAlias("-l");
        return option;
    }

    public static Option<string> CreatePeriodOption()
    {
        var option = new Option<string>("--period", "Time period: overall, 7day, 1month, 3month, 6month, 12month");
        option.AddAlias("-p");
        return option;
    }

    public static Option<string> CreateUserOption()
    {
        var option = new Option<string>("--user", "Last.fm username (uses configured default if not specified)");
        option.AddAlias("-u");
        return option;
    }

    public static Option<string> CreateRangeOption()
    {
        return new Option<string>("--range", "Show specific range (e.g., 1-50, 101-200)");
    }

    public static (Option<string> from, Option<string> to, Option<string> year) CreateDateOptions()
    {
        var fromOption = new Option<string>("--from", "Start date (YYYY-MM-DD or YYYY)");
        var toOption = new Option<string>("--to", "End date (YYYY-MM-DD or YYYY)");
        var yearOption = new Option<string>("--year", "Specific year (YYYY) - shortcut for entire year");
        return (fromOption, toOption, yearOption);
    }

    public static Option<int> CreateDelayOption()
    {
        return new Option<int>("--delay", "Delay between API calls in milliseconds");
    }

    public static Option<bool> CreateVerboseOption()
    {
        var option = new Option<bool>("--verbose", "Show detailed progress information");
        option.AddAlias("-v");
        return option;
    }

    public static Option<bool> CreateTimingOption()
    {
        return new Option<bool>("--timing", "Show API call timing information");
    }

    public static Option<bool> CreateTimerOption()
    {
        return new Option<bool>("--timer", "Show total execution time");
    }
}