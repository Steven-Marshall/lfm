using System.CommandLine;
using System.Linq;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Cli.CommandBuilders;

public static class TopTracksCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        // Playlist generation options - mutually exclusive
        var totalTracksOption = StandardCommandOptions.CreateTotalTracksOption(20); // default 20 tracks
        var totalArtistsOption = StandardCommandOptions.CreateTotalArtistsOption();
        var tracksPerArtistOption = StandardCommandOptions.CreateTracksPerArtistOption(1);

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

        var playNowOption = new Option<bool>("--playnow", "Queue tracks to Spotify and start playing immediately");
        var playlistOption = new Option<string>("--playlist", "Save tracks to a Spotify playlist with the given name");
        playlistOption.AddAlias("-pl");
        var shuffleOption = new Option<bool>("--shuffle", "Shuffle the order when sending to Spotify");
        shuffleOption.AddAlias("-s");
        var deviceOption = new Option<string>("--device", "Specific Spotify device to use (overrides config default)");
        deviceOption.AddAlias("-dev");

        var jsonOption = new Option<bool>("--json", "Output results in JSON format");
        jsonOption.AddAlias("-j");

        var (forceCacheOption, forceApiOption, noCacheOption) = CommandOptionBuilders.BuildCacheOptions();

        var command = new Command("toptracks", "Create playlists from your top tracks with optional artist diversity")
        {
            totalTracksOption,
            totalArtistsOption,
            tracksPerArtistOption,
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

            // For toptracks, the limit is the number of tracks (different from recommendations)
            int limit;
            if (totalArtists.HasValue)
            {
                // Calculate total tracks from artists * tracks per artist
                limit = totalArtists.Value * tracksPerArtist;
            }
            else
            {
                // Use totalTracks (which has a default of 20)
                limit = totalTracks ?? 20;
            }

            var topTracksCommand = services.GetRequiredService<TopTracksCommand>();
            await topTracksCommand.ExecuteAsync(limit, period, user, tracksPerArtist, range, delay, verbose, timing, forceCache, forceApi, noCache, timer, from, to, year, playNow, playlist, shuffle, device, json);
        });

        return command;
    }
}