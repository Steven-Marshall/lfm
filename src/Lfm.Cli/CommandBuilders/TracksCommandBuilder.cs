using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Cli.CommandBuilders;

public static class TracksCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var limitOption = StandardCommandOptions.CreateLimitOption("tracks");
        var periodOption = StandardCommandOptions.CreatePeriodOption();
        var (fromOption, toOption, yearOption) = StandardCommandOptions.CreateDateOptions();
        var userOption = StandardCommandOptions.CreateUserOption();
        
        var artistOption = new Option<string>("--artist", "Get global top tracks for specific artist (from Last.fm, not your personal data)");
        artistOption.AddAlias("-a");

        var rangeOption = new Option<string>("--range", "Range of positions to display (e.g., --range 10-20 for positions 10-20, both inclusive)");
        rangeOption.AddAlias("-r");

        var delayOption = new Option<int?>("--delay", "Delay between API requests in milliseconds (0 = no throttling, overrides config)");
        delayOption.AddAlias("-d");

        var verboseOption = StandardCommandOptions.CreateVerboseOption();
        var timingOption = new Option<bool>("--timing", "Show detailed API timing information (cache hits/misses and response times)");
        timingOption.AddAlias("-t");
        var timerOption = StandardCommandOptions.CreateTimerOption();

        var playNowOption = new Option<bool>("--playnow", "Queue tracks to Spotify and start playing immediately");
        var playlistOption = new Option<string>("--playlist", "Save tracks to a Spotify playlist with the given name");
        playlistOption.AddAlias("-pl");
        var shuffleOption = new Option<bool>("--shuffle", "Shuffle the order when sending to Spotify");
        shuffleOption.AddAlias("-s");
        var deviceOption = new Option<string>("--device", "Specific Spotify device to use (overrides config default)");
        deviceOption.AddAlias("-dev");

        var (forceCacheOption, forceApiOption, noCacheOption) = CommandOptionBuilders.BuildCacheOptions();

        var command = new Command("tracks", "Get your top tracks with play counts, or global tracks for an artist")
        {
            limitOption,
            periodOption,
            fromOption,
            toOption,
            yearOption,
            userOption,
            artistOption,
            rangeOption,
            delayOption,
            verboseOption,
            timingOption,
            forceCacheOption,
            forceApiOption,
            noCacheOption,
            timerOption,
            playNowOption,
            playlistOption,
            shuffleOption,
            deviceOption
        };

        command.SetHandler(async (context) =>
        {
            var limit = context.ParseResult.GetValueForOption(limitOption);
            var period = context.ParseResult.GetValueForOption(periodOption);
            var from = context.ParseResult.GetValueForOption(fromOption);
            var to = context.ParseResult.GetValueForOption(toOption);
            var year = context.ParseResult.GetValueForOption(yearOption);
            var user = context.ParseResult.GetValueForOption(userOption);
            var artist = context.ParseResult.GetValueForOption(artistOption);
            var range = context.ParseResult.GetValueForOption(rangeOption);
            var delay = context.ParseResult.GetValueForOption(delayOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var timing = context.ParseResult.GetValueForOption(timingOption);
            var forceCache = context.ParseResult.GetValueForOption(forceCacheOption);
            var forceApi = context.ParseResult.GetValueForOption(forceApiOption);
            var noCache = context.ParseResult.GetValueForOption(noCacheOption);
            var timer = context.ParseResult.GetValueForOption(timerOption);
            var playNow = context.ParseResult.GetValueForOption(playNowOption);
            var playlist = context.ParseResult.GetValueForOption(playlistOption);
            var shuffle = context.ParseResult.GetValueForOption(shuffleOption);
            var device = context.ParseResult.GetValueForOption(deviceOption);

            var tracksCommand = services.GetRequiredService<TracksCommand>();
            await tracksCommand.ExecuteAsync(limit, period, user, artist, range, delay, verbose, timing, forceCache, forceApi, noCache, timer, from, to, year, playNow, playlist, shuffle, device);
        });

        return command;
    }
}