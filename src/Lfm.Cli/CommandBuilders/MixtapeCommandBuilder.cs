using System.CommandLine;
using System.CommandLine.Invocation;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

public static class MixtapeCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("mixtape", "Generate a random mixtape playlist with weighted sampling from your listening history");

        // Required parameters
        var limitOption = new Option<int>("--totaltracks", "Number of tracks to include in the mixtape") { IsRequired = true };
        limitOption.AddAlias("-tt");

        // Mixtape-specific parameters
        var biasOption = new Option<float?>("--bias", "Bias towards frequently played tracks (0.0=random, 1.0=weighted by play count)");
        biasOption.AddAlias("-b");

        var minPlaysOption = new Option<int?>("--min-plays", "Minimum play count required for tracks to be included");
        minPlaysOption.AddAlias("-mp");

        var tracksPerArtistOption = new Option<int>("--tracks-per-artist", () => 1, "Maximum number of tracks per artist");
        tracksPerArtistOption.AddAlias("-tpa");

        // Standard options
        var periodOption = StandardCommandOptions.CreatePeriodOption();
        var (fromOption, toOption, yearOption) = StandardCommandOptions.CreateDateOptions();
        var userOption = StandardCommandOptions.CreateUserOption();

        var rangeOption = new Option<string>("--range", "Range of positions to display (not applicable for mixtape)");
        rangeOption.AddAlias("-r");

        var delayOption = new Option<int?>("--delay", "Delay between API requests in milliseconds (0 = no throttling, overrides config)");
        delayOption.AddAlias("-d");

        var verboseOption = StandardCommandOptions.CreateVerboseOption();
        var timingOption = StandardCommandOptions.CreateTimingOption();
        var timerOption = StandardCommandOptions.CreateTimerOption();

        // Cache control options
        var forceCacheOption = new Option<bool>("--force-cache", "Force use of cached data, skip API calls if possible");
        var forceApiOption = new Option<bool>("--force-api", "Force fresh API calls, bypass cache");
        var noCacheOption = new Option<bool>("--no-cache", "Disable caching entirely for this command");

        // Spotify options
        var playNowOption = new Option<bool>("--playnow", "Queue tracks to Spotify and start playing immediately");
        var playlistOption = new Option<string>("--playlist", "Save tracks to a Spotify playlist with the given name");
        playlistOption.AddAlias("-pl");
        var shuffleOption = new Option<bool>("--shuffle", "Shuffle the order when sending to Spotify");
        shuffleOption.AddAlias("-s");
        var deviceOption = new Option<string>("--device", "Specific Spotify device to use (overrides config default)");
        deviceOption.AddAlias("-dev");

        var seedOption = new Option<int?>("--seed", "Random seed for reproducible mixtapes (auto-generated if not specified)");

        var jsonOption = new Option<bool>("--json", "Output results in JSON format");

        // Add options to command
        command.AddOption(limitOption);
        command.AddOption(biasOption);
        command.AddOption(minPlaysOption);
        command.AddOption(tracksPerArtistOption);
        command.AddOption(userOption);
        command.AddOption(periodOption);
        command.AddOption(rangeOption);
        command.AddOption(delayOption);
        command.AddOption(verboseOption);
        command.AddOption(timingOption);
        command.AddOption(timerOption);
        command.AddOption(forceCacheOption);
        command.AddOption(forceApiOption);
        command.AddOption(noCacheOption);
        command.AddOption(fromOption);
        command.AddOption(toOption);
        command.AddOption(yearOption);
        command.AddOption(playNowOption);
        command.AddOption(playlistOption);
        command.AddOption(shuffleOption);
        command.AddOption(deviceOption);
        command.AddOption(seedOption);
        command.AddOption(jsonOption);

        command.SetHandler(async (InvocationContext context) =>
        {
            var limit = context.ParseResult.GetValueForOption(limitOption);
            var bias = context.ParseResult.GetValueForOption(biasOption);
            var minPlays = context.ParseResult.GetValueForOption(minPlaysOption);
            var tracksPerArtist = context.ParseResult.GetValueForOption(tracksPerArtistOption);
            var user = context.ParseResult.GetValueForOption(userOption);
            var period = context.ParseResult.GetValueForOption(periodOption);
            var range = context.ParseResult.GetValueForOption(rangeOption);
            var delay = context.ParseResult.GetValueForOption(delayOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var timing = context.ParseResult.GetValueForOption(timingOption);
            var timer = context.ParseResult.GetValueForOption(timerOption);
            var forceCache = context.ParseResult.GetValueForOption(forceCacheOption);
            var forceApi = context.ParseResult.GetValueForOption(forceApiOption);
            var noCache = context.ParseResult.GetValueForOption(noCacheOption);
            var from = context.ParseResult.GetValueForOption(fromOption);
            var to = context.ParseResult.GetValueForOption(toOption);
            var year = context.ParseResult.GetValueForOption(yearOption);
            var playNow = context.ParseResult.GetValueForOption(playNowOption);
            var playlist = context.ParseResult.GetValueForOption(playlistOption);
            var shuffle = context.ParseResult.GetValueForOption(shuffleOption);
            var device = context.ParseResult.GetValueForOption(deviceOption);
            var seed = context.ParseResult.GetValueForOption(seedOption);
            var json = context.ParseResult.GetValueForOption(jsonOption);

            var mixtapeCommand = services.GetRequiredService<MixtapeCommand>();
            await mixtapeCommand.ExecuteAsync(limit, period, user, bias, minPlays, tracksPerArtist, range, delay, verbose, timing, forceCache, forceApi, noCache, timer, from, to, year, playNow, playlist, shuffle, device, seed, json);
        });

        return command;
    }
}