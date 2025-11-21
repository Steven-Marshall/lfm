using System.Text.Json;
using Microsoft.Extensions.Logging;
using Lfm.Core.Configuration;
using Lfm.Core.Services;

namespace Lfm.Cli.Commands;

public class ConcertsCommand
{
    private readonly ISetlistFmApiClient _setlistFmClient;
    private readonly ILogger<ConcertsCommand> _logger;
    private readonly IConfigurationManager _configManager;
    private bool _isJsonMode = false;

    public ConcertsCommand(
        ILogger<ConcertsCommand> logger,
        ISetlistFmApiClient setlistFmClient,
        IConfigurationManager configManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _setlistFmClient = setlistFmClient ?? throw new ArgumentNullException(nameof(setlistFmClient));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
    }

    public async Task ExecuteAsync(
        string artist,
        string? city = null,
        string? country = null,
        string? venue = null,
        string? tour = null,
        string? date = null,
        string? year = null,
        int page = 1,
        bool json = false)
    {
        _isJsonMode = json;

        try
        {
            _logger.LogDebug("Searching concerts: Artist={Artist}, City={City}, Country={Country}, Venue={Venue}, Tour={Tour}, Date={Date}, Year={Year}, Page={Page}",
                artist, city, country, venue, tour, date, year, page);

            var response = await _setlistFmClient.SearchSetlistsAsync(
                artistName: artist,
                cityName: city,
                countryCode: country,
                venueName: venue,
                tourName: tour,
                date: date,
                year: year,
                page: page);

            if (json)
            {
                OutputJson(response);
            }
            else
            {
                OutputTable(response, artist, city, country, venue, tour, date, year, page);
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("NotFound") || ex.Message.Contains("404"))
        {
            // 404 from Setlist.fm means no results found, not an actual error
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new {
                    success = true,
                    concerts = Array.Empty<object>(),
                    count = 0,
                    message = $"No concerts found for '{artist}' with the specified filters."
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine($"No concerts found for '{artist}' with the specified filters.");
                Console.WriteLine("\nTips:");
                Console.WriteLine("  • Check the artist name spelling");
                Console.WriteLine("  • Try removing filters (--city, --year, etc.) to broaden the search");
                Console.WriteLine("  • Not all artists have setlists on setlist.fm");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching concerts");
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "An error occurred while searching concerts. Please try again." }));
            }
            else
            {
                Console.WriteLine("Error: An unexpected error occurred while searching concerts.");
                Console.WriteLine("Please check your internet connection and try again.");
            }
        }
    }

    private void OutputJson(Core.Models.SetlistFm.SetlistSearchResponse response)
    {
        var appliedFilters = new List<string>();
        var output = new
        {
            success = true,
            concerts = response.Setlists.Select(s => new
            {
                id = s.Id,
                date = s.EventDate,
                artist = s.Artist.Name,
                venue = new
                {
                    name = s.Venue.Name,
                    city = s.Venue.City.Name,
                    state = s.Venue.City.State,
                    country = s.Venue.City.Country.Name,
                    countryCode = s.Venue.City.Country.Code
                },
                tour = s.Tour?.Name,
                trackCount = s.Sets.Set.SelectMany(set => set.Songs).Count(),
                url = s.Url
            }).ToArray(),
            count = response.Setlists.Count,
            pagination = new
            {
                page = response.Page,
                itemsPerPage = response.ItemsPerPage,
                total = response.Total,
                totalPages = (int)Math.Ceiling(response.Total / (double)response.ItemsPerPage)
            },
            _suggestion = response.Total > 40
                ? "Large result set. Consider adding filters: --city, --country, --year, --venue, or --tour to narrow results."
                : null
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine(JsonSerializer.Serialize(output, options));
    }

    private void OutputTable(
        Core.Models.SetlistFm.SetlistSearchResponse response,
        string artist,
        string? city,
        string? country,
        string? venue,
        string? tour,
        string? date,
        string? year,
        int page)
    {
        var totalPages = (int)Math.Ceiling(response.Total / (double)response.ItemsPerPage);

        Console.WriteLine($"Found {response.Total} concerts for {artist}");

        // Show applied filters
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(city)) filters.Add($"City: {city}");
        if (!string.IsNullOrWhiteSpace(country)) filters.Add($"Country: {country}");
        if (!string.IsNullOrWhiteSpace(venue)) filters.Add($"Venue: {venue}");
        if (!string.IsNullOrWhiteSpace(tour)) filters.Add($"Tour: {tour}");
        if (!string.IsNullOrWhiteSpace(date)) filters.Add($"Date: {date}");
        if (!string.IsNullOrWhiteSpace(year)) filters.Add($"Year: {year}");

        if (filters.Any())
        {
            Console.WriteLine($"Filters: {string.Join(", ", filters)}");
        }

        Console.WriteLine($"Showing page {page} of {totalPages} ({response.Setlists.Count} concerts)\n");

        if (!response.Setlists.Any())
        {
            Console.WriteLine("No concerts found.");
            return;
        }

        // Table header
        Console.WriteLine($"{"ID",-12} {"Date",-12} {"Venue",-30} {"City",-20} {"Country",-15} {"Tracks",8}");
        Console.WriteLine(new string('-', 110));

        // Table rows
        foreach (var concert in response.Setlists)
        {
            var trackCount = concert.Sets.Set.SelectMany(set => set.Songs).Count();
            var trackDisplay = trackCount > 0 ? trackCount.ToString() : "-";
            var venueDisplay = TruncateString(concert.Venue.Name, 28);
            var cityDisplay = TruncateString(concert.Venue.City.Name, 18);
            var countryDisplay = TruncateString(concert.Venue.City.Country.Name, 13);

            Console.WriteLine($"{concert.Id,-12} {concert.EventDate,-12} {venueDisplay,-30} {cityDisplay,-20} {countryDisplay,-15} {trackDisplay,8}");
        }

        Console.WriteLine();

        // Attribution (T&C requirement)
        Console.WriteLine($"Source: https://www.setlist.fm");

        // Pagination guidance
        if (totalPages > 1)
        {
            Console.WriteLine($"\nUse --page {page + 1} to see more results (up to page {totalPages})");
        }

        // Filter suggestion for large result sets
        if (response.Total > 40 && filters.Count < 3)
        {
            Console.WriteLine("\n💡 Tip: Large result set. Consider adding more filters to narrow results:");
            var suggestions = new List<string>();
            if (string.IsNullOrWhiteSpace(city)) suggestions.Add("--city London");
            if (string.IsNullOrWhiteSpace(country)) suggestions.Add("--country GB");
            if (string.IsNullOrWhiteSpace(year)) suggestions.Add("--year 2024");
            if (string.IsNullOrWhiteSpace(venue)) suggestions.Add("--venue \"O2 Arena\"");
            if (string.IsNullOrWhiteSpace(tour)) suggestions.Add("--tour \"Tour Name\"");

            Console.WriteLine($"   {string.Join(" ", suggestions.Take(3))}");
        }
    }

    private static string TruncateString(string str, int maxLength)
    {
        if (string.IsNullOrEmpty(str)) return "-";
        return str.Length <= maxLength ? str : str[..(maxLength - 2)] + "..";
    }
}
