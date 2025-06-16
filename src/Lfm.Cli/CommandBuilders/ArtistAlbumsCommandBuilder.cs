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
        var limitOption = new Option<int>("--limit", () => Defaults.ItemLimit, "Number of albums to display");
        limitOption.AddAlias("-l");
        
        var deepOption = new Option<bool>("--deep", "Search through ALL your albums (slower but comprehensive)");
        
        var delayOption = new Option<int?>("--delay", "Delay between API requests in milliseconds (0 = no throttling, overrides config)");
        delayOption.AddAlias("-d");
        
        var depthOption = new Option<int?>("--depth", "Maximum number of items to search through (0 = unlimited, overrides --deep and config)");
        
        var timeoutOption = new Option<int?>("--timeout", "Search timeout in seconds (0 = no timeout, overrides config)");
        timeoutOption.AddAlias("-t");
        
        var verboseOption = new Option<bool>("--verbose", "Show detailed progress information");
        verboseOption.AddAlias("-v");
        
        var artistArg = new Argument<string>("artist", "Artist name");

        var command = new Command("artist-albums", "Get your most played albums by a specific artist from your listening history")
        {
            artistArg,
            limitOption,
            deepOption,
            delayOption,
            depthOption,
            timeoutOption,
            verboseOption
        };

        command.SetHandler(async (string artist, int limit, bool deep, int? delay, int? depth, int? timeout, bool verbose) =>
        {
            var artistAlbumsCommand = services.GetRequiredService<ArtistSearchCommand<Album, TopAlbums>>();
            await artistAlbumsCommand.ExecuteAsync(artist, limit, deep, delay, depth, timeout, verbose);
        }, artistArg, limitOption, deepOption, delayOption, depthOption, timeoutOption, verboseOption);

        return command;
    }
}