using System.CommandLine;
using Lfm.Cli.Commands;
using Lfm.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Cli.CommandBuilders;

public static class ArtistAlbumsCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var limitOption = StandardCommandOptions.CreateLimitOption("albums");
        
        var deepOption = new Option<bool>("--deep", "Search through ALL your albums (slower but comprehensive)");
        
        var delayOption = new Option<int?>("--delay", "Delay between API requests in milliseconds (0 = no throttling, overrides config)");
        delayOption.AddAlias("-d");
        
        var depthOption = new Option<int?>("--depth", "Maximum number of items to search through (0 = unlimited, overrides --deep and config)");
        
        var timeoutOption = new Option<int?>("--timeout", "Search timeout in seconds (0 = no timeout, overrides config)");
        
        var verboseOption = StandardCommandOptions.CreateVerboseOption();

        var timingOption = new Option<bool>("--timing", "Show detailed API timing information (cache hits/misses and response times)");
        timingOption.AddAlias("-t");

        var timerOption = StandardCommandOptions.CreateTimerOption();

        var (forceCacheOption, forceApiOption, noCacheOption) = CommandOptionBuilders.BuildCacheOptions();

        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output in JSON format (for MCP integration)");

        var artistArg = new Argument<string>("artist", "Artist name");

        var command = new Command("artist-albums", "Get your most played albums by a specific artist from your listening history")
        {
            artistArg,
            limitOption,
            deepOption,
            delayOption,
            depthOption,
            timeoutOption,
            verboseOption,
            timingOption,
            forceCacheOption,
            forceApiOption,
            noCacheOption,
            timerOption,
            jsonOption
        };

        command.SetHandler(async (context) =>
        {
            var artist = context.ParseResult.GetValueForArgument(artistArg);
            var limit = context.ParseResult.GetValueForOption(limitOption);
            var deep = context.ParseResult.GetValueForOption(deepOption);
            var delay = context.ParseResult.GetValueForOption(delayOption);
            var depth = context.ParseResult.GetValueForOption(depthOption);
            var timeout = context.ParseResult.GetValueForOption(timeoutOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var timing = context.ParseResult.GetValueForOption(timingOption);
            var forceCache = context.ParseResult.GetValueForOption(forceCacheOption);
            var forceApi = context.ParseResult.GetValueForOption(forceApiOption);
            var noCache = context.ParseResult.GetValueForOption(noCacheOption);
            var timer = context.ParseResult.GetValueForOption(timerOption);
            var json = context.ParseResult.GetValueForOption(jsonOption);

            var artistAlbumsCommand = services.GetRequiredService<ArtistSearchCommand<Album, TopAlbums>>();
            await artistAlbumsCommand.ExecuteAsync(artist, limit, deep, delay, depth, timeout, verbose, timing, forceCache, forceApi, noCache, timer, json);
        });

        return command;
    }
}