using System.CommandLine;
using System.Linq;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Cli.CommandBuilders;

public static class RecommendationsCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        // Playlist generation options - mutually exclusive
        var totalTracksOption = StandardCommandOptions.CreateTotalTracksOption();
        var totalArtistsOption = StandardCommandOptions.CreateTotalArtistsOption(20); // default 20 artists like before
        var tracksPerArtistOption = StandardCommandOptions.CreateTracksPerArtistOption(1);

        var filterOption = new Option<int>("--filter", () => 0, "Minimum play count to filter out (exclude artists with >= this many plays)");
        filterOption.AddAlias("-f");

        var artistLimitOption = new Option<int>("--artist-limit", () => Defaults.ItemLimit, "Number of top artists to analyze for recommendations");
        artistLimitOption.AddAlias("-a");
        
        var periodOption = StandardCommandOptions.CreatePeriodOption();
        var (fromOption, toOption, yearOption) = StandardCommandOptions.CreateDateOptions();
        var userOption = StandardCommandOptions.CreateUserOption();

        var rangeOption = new Option<string>("--range", "Range of artists to analyze (e.g., --range 10-20 for positions 10-20, both inclusive)");
        rangeOption.AddAlias("-r");

        var delayOption = new Option<int?>("--delay", "Delay between API requests in milliseconds (0 = no throttling, overrides config)");
        delayOption.AddAlias("-d");

        var verboseOption = StandardCommandOptions.CreateVerboseOption();
        var timingOption = new Option<bool>("--timing", "Show detailed API timing information (cache hits/misses and response times)");
        timingOption.AddAlias("-t");
        var timerOption = StandardCommandOptions.CreateTimerOption();

        var excludeTagsOption = new Option<bool>("--exclude-tags", "Filter out artists based on excluded tags configured in settings");
        excludeTagsOption.AddAlias("-et");

        var playNowOption = new Option<bool>("--playnow", "Queue recommendations to Spotify and start playing immediately");
        var playlistOption = new Option<string>("--playlist", "Save recommendations to a Spotify playlist with the given name");
        playlistOption.AddAlias("-pl");
        var shuffleOption = new Option<bool>("--shuffle", "Shuffle the order when sending to Spotify");
        shuffleOption.AddAlias("-s");
        var deviceOption = new Option<string>("--device", "Specific Spotify device to use (overrides config default)");
        deviceOption.AddAlias("-dev");

        var jsonOption = new Option<bool>("--json", "Output results in JSON format");
        jsonOption.AddAlias("-j");

        var (forceCacheOption, forceApiOption, noCacheOption) = CommandOptionBuilders.BuildCacheOptions();

        var command = new Command("recommendations", "Get artist recommendations based on your listening history")
        {
            totalTracksOption,
            totalArtistsOption,
            tracksPerArtistOption,
            filterOption,
            artistLimitOption,
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
            timerOption,
            excludeTagsOption,
            playNowOption,
            playlistOption,
            shuffleOption,
            deviceOption,
            jsonOption
        };

        command.SetHandler(async (context) =>
        {
            var totalTracks = context.ParseResult.GetValueForOption(totalTracksOption);
            var totalArtists = context.ParseResult.GetValueForOption(totalArtistsOption);
            var tracksPerArtist = context.ParseResult.GetValueForOption(tracksPerArtistOption);
            var filter = context.ParseResult.GetValueForOption(filterOption);
            var artistLimit = context.ParseResult.GetValueForOption(artistLimitOption);
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
            var excludeTags = context.ParseResult.GetValueForOption(excludeTagsOption);
            var playNow = context.ParseResult.GetValueForOption(playNowOption);
            var playlist = context.ParseResult.GetValueForOption(playlistOption);
            var shuffle = context.ParseResult.GetValueForOption(shuffleOption);
            var device = context.ParseResult.GetValueForOption(deviceOption);
            var json = context.ParseResult.GetValueForOption(jsonOption);

            // Validate mutual exclusivity - check if user explicitly provided both
            var hasExplicitTotalTracks = context.ParseResult.Tokens.Any(t =>
                t.Value == "--totaltracks" || t.Value == "-tt");
            var hasExplicitTotalArtists = context.ParseResult.Tokens.Any(t =>
                t.Value == "--totalartists" || t.Value == "-ta");

            if (hasExplicitTotalTracks && hasExplicitTotalArtists)
            {
                Console.Error.WriteLine("Error: Cannot specify both --totaltracks and --totalartists. Choose one.");
                context.ExitCode = 1;
                return;
            }

            // Calculate the actual limit based on what was specified
            int limit;
            if (totalTracks.HasValue)
            {
                // Calculate number of artists needed for the track count
                limit = (int)Math.Ceiling((double)totalTracks.Value / tracksPerArtist);
            }
            else
            {
                // Use totalArtists (which has a default of 20)
                limit = totalArtists ?? 20;
            }

            var recommendationsCommand = services.GetRequiredService<RecommendationsCommand>();
            await recommendationsCommand.ExecuteAsync(
                artistLimit,
                period,
                user,
                range,
                delay,
                verbose,
                timing,
                forceCache,
                forceApi,
                noCache,
                timer,
                limit,
                filter,
                tracksPerArtist,
                from,
                to,
                year,
                excludeTags,
                playNow,
                playlist,
                shuffle,
                device,
                json);
        });

        return command;
    }
}