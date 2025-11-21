using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Command builder for the concerts command
/// </summary>
public static class ConcertsCommandBuilder
{
    /// <summary>
    /// Builds the concerts command for searching concert setlists
    /// </summary>
    /// <param name="services">Service provider for dependency injection</param>
    /// <returns>Configured concerts command</returns>
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("concerts", "Search for concerts by artist with optional filters");

        // Arguments
        var artistArgument = new Argument<string>(
            name: "artist",
            description: "Artist name to search for");

        command.AddArgument(artistArgument);

        // Options
        var cityOption = new Option<string?>(
            aliases: new[] { "--city", "-c" },
            description: "City name to filter by (e.g., London)");

        var countryOption = new Option<string?>(
            aliases: new[] { "--country" },
            description: "Country code to filter by (e.g., GB, US)");

        var venueOption = new Option<string?>(
            aliases: new[] { "--venue", "-v" },
            description: "Venue name to filter by");

        var tourOption = new Option<string?>(
            aliases: new[] { "--tour" },
            description: "Tour name to filter by");

        var dateOption = new Option<string?>(
            aliases: new[] { "--date", "-d" },
            description: "Specific date to filter by (DD-MM-YYYY format)");

        var yearOption = new Option<string?>(
            aliases: new[] { "--year", "-y" },
            description: "Year to filter by (YYYY format)");

        var pageOption = new Option<int>(
            aliases: new[] { "--page", "-p" },
            description: "Page number for pagination (default: 1)",
            getDefaultValue: () => 1);

        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output results in JSON format");

        command.AddOption(cityOption);
        command.AddOption(countryOption);
        command.AddOption(venueOption);
        command.AddOption(tourOption);
        command.AddOption(dateOption);
        command.AddOption(yearOption);
        command.AddOption(pageOption);
        command.AddOption(jsonOption);

        // Handler
        command.SetHandler(async (context) =>
        {
            var artist = context.ParseResult.GetValueForArgument(artistArgument);
            var city = context.ParseResult.GetValueForOption(cityOption);
            var country = context.ParseResult.GetValueForOption(countryOption);
            var venue = context.ParseResult.GetValueForOption(venueOption);
            var tour = context.ParseResult.GetValueForOption(tourOption);
            var date = context.ParseResult.GetValueForOption(dateOption);
            var year = context.ParseResult.GetValueForOption(yearOption);
            var page = context.ParseResult.GetValueForOption(pageOption);
            var json = context.ParseResult.GetValueForOption(jsonOption);

            var concertsCommand = services.GetRequiredService<ConcertsCommand>();
            await concertsCommand.ExecuteAsync(artist, city, country, venue, tour, date, year, page, json);
        });

        return command;
    }
}
