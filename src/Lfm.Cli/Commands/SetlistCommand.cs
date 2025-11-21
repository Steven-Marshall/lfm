using System.Text.Json;
using Microsoft.Extensions.Logging;
using Lfm.Core.Configuration;
using Lfm.Core.Services;

namespace Lfm.Cli.Commands;

public class SetlistCommand
{
    private readonly ISetlistFmApiClient _setlistFmClient;
    private readonly ILogger<SetlistCommand> _logger;
    private readonly IConfigurationManager _configManager;
    private bool _isJsonMode = false;

    public SetlistCommand(
        ILogger<SetlistCommand> logger,
        ISetlistFmApiClient setlistFmClient,
        IConfigurationManager configManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _setlistFmClient = setlistFmClient ?? throw new ArgumentNullException(nameof(setlistFmClient));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
    }

    public async Task ExecuteAsync(string setlistId, bool json = false)
    {
        _isJsonMode = json;

        try
        {
            _logger.LogDebug("Getting setlist: {SetlistId}", setlistId);

            var setlist = await _setlistFmClient.GetSetlistAsync(setlistId);

            if (json)
            {
                OutputJson(setlist);
            }
            else
            {
                OutputFormatted(setlist);
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("NotFound") || ex.Message.Contains("404"))
        {
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new {
                    success = false,
                    error = $"Setlist '{setlistId}' not found.",
                    message = "Please check the setlist ID and try again."
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine($"Setlist '{setlistId}' not found.");
                Console.WriteLine("\nPlease check the setlist ID and try again.");
                Console.WriteLine("You can find setlist IDs using: lfm concerts <artist>");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting setlist");
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "An error occurred while fetching the setlist. Please try again." }));
            }
            else
            {
                Console.WriteLine("Error: An unexpected error occurred while fetching the setlist.");
                Console.WriteLine("Please check your internet connection and try again.");
            }
        }
    }

    private void OutputJson(Core.Models.SetlistFm.Setlist setlist)
    {
        var tracks = setlist.Sets.Set.SelectMany((set, setIndex) =>
            set.Songs.Select((song, songIndex) => new
            {
                number = setIndex == 0
                    ? songIndex + 1
                    : setlist.Sets.Set[0].Songs.Count + songIndex + 1,
                name = song.Name,
                artist = song.Cover?.Name ?? setlist.Artist.Name,
                cover = song.Cover != null ? new
                {
                    original_artist = song.Cover.Name,
                    mbid = song.Cover.Mbid
                } : null,
                info = song.Info,
                tape = song.Tape,
                encore = set.Encore ?? 0
            })
        ).ToArray();

        var hasSetlist = tracks.Any();

        var output = new
        {
            success = true,
            concert = new
            {
                id = setlist.Id,
                date = setlist.EventDate,
                artist = setlist.Artist.Name,
                venue = new
                {
                    name = setlist.Venue.Name,
                    city = setlist.Venue.City.Name,
                    state = setlist.Venue.City.State,
                    country = setlist.Venue.City.Country.Name
                },
                tour = setlist.Tour?.Name
            },
            tracks,
            _note = hasSetlist ? null : "Setlist not available for this concert. Visit the URL to contribute.",
            info = setlist.Info,
            url = setlist.Url
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine(JsonSerializer.Serialize(output, options));
    }

    private void OutputFormatted(Core.Models.SetlistFm.Setlist setlist)
    {
        // Concert header
        Console.WriteLine($"{setlist.Artist.Name}");
        Console.WriteLine($"{setlist.EventDate} - {setlist.Venue.Name}");
        Console.WriteLine($"{setlist.Venue.City.Name}, {setlist.Venue.City.Country.Name}");

        if (setlist.Tour != null)
        {
            Console.WriteLine($"Tour: {setlist.Tour.Name}");
        }

        if (!string.IsNullOrWhiteSpace(setlist.Info))
        {
            Console.WriteLine($"\nNotes: {setlist.Info}");
        }

        Console.WriteLine();

        // Check if setlist is empty
        if (!setlist.Sets.Set.Any() || !setlist.Sets.Set.Any(s => s.Songs.Any()))
        {
            Console.WriteLine("Setlist not available for this concert.");
            Console.WriteLine($"\nSource: {setlist.Url}");
            return;
        }

        // Track count
        var totalTracks = setlist.Sets.Set.SelectMany(s => s.Songs).Count();
        Console.WriteLine($"Setlist ({totalTracks} tracks):");
        Console.WriteLine(new string('-', 80));

        int trackNumber = 1;

        // Output each set
        foreach (var set in setlist.Sets.Set)
        {
            if (set.Encore.HasValue && set.Encore.Value > 0)
            {
                Console.WriteLine($"\nEncore {set.Encore}:");
                Console.WriteLine(new string('-', 80));
            }

            foreach (var song in set.Songs)
            {
                var trackInfo = $"{trackNumber,3}. {song.Name}";

                if (song.Cover != null)
                {
                    trackInfo += $" ({song.Cover.Name} cover)";
                }

                if (song.Tape == true)
                {
                    trackInfo += " [tape]";
                }

                if (!string.IsNullOrWhiteSpace(song.Info))
                {
                    trackInfo += $" - {song.Info}";
                }

                Console.WriteLine(trackInfo);
                trackNumber++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Source: {setlist.Url}");
    }
}
