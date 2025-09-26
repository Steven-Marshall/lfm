using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

public class ArtistsCommand : BaseCommand
{
    private readonly ILastFmService _lastFmService;
    private readonly IDisplayService _displayService;

    public ArtistsCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILastFmService lastFmService,
        IDisplayService displayService,
        ILogger<ArtistsCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _lastFmService = lastFmService ?? throw new ArgumentNullException(nameof(lastFmService));
        _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
    }

    public async Task ExecuteAsync(int limit, string? period, string? username, string? range = null, int? delayMs = null, bool verbose = false, bool timing = false, bool forceCache = false, bool forceApi = false, bool noCache = false, bool timer = false, string? from = null, string? to = null, string? year = null, bool json = false)
    {
        await ExecuteWithErrorHandlingAndTimerAsync("artists command", async () =>
        {
            // Configure cache behavior and timing
            ConfigureCaching(timing, forceCache, forceApi, noCache);

            if (!await ValidateApiKeyAsync())
                return;

            var user = await GetUsernameAsync(username);
            if (user == null)
                return;

            // Validate limit parameter
            ValidateLimit(limit);

            // Resolve period parameters (--period, --from/--to, or --year)
            var (isDateRange, resolvedPeriod, fromDate, toDate) = ResolvePeriodParameters(period, from, to, year);
            
            // Determine display period format for consistent messaging
            var displayPeriod = isDateRange && fromDate.HasValue && toDate.HasValue 
                ? DateRangeParser.FormatDateRange(fromDate.Value, toDate.Value)
                : resolvedPeriod;
            
            // Handle range logic using service layer
            if (!string.IsNullOrEmpty(range))
            {
                if (!ValidateAndHandleRange(range, _displayService, out var startIndex, out var endIndex))
                {
                    return;
                }
                
                if (!json)
                {
                    _displayService.DisplayOperationStart("artists", user, displayPeriod, null, startIndex, endIndex, verbose);
                }

                // Use service layer for range query
                var (rangeArtists, totalCount) = await _lastFmService.GetUserTopArtistsRangeAsync(user, resolvedPeriod, startIndex, endIndex);
                
                if (!rangeArtists.Any())
                {
                    if (json)
                    {
                        var emptyRangeResult = new
                        {
                            artists = new object[0],
                            range = new { start = startIndex, end = endIndex, count = 0 },
                            total = totalCount
                        };
                        var jsonOutput = JsonSerializer.Serialize(emptyRangeResult, new JsonSerializerOptions { WriteIndented = true });
                        Console.WriteLine(jsonOutput);
                    }
                    else
                    {
                        _displayService.DisplayError(ErrorMessages.Format(ErrorMessages.NoItemsInRange, "artists"));
                    }
                    return;
                }
                
                if (json)
                {
                    var jsonOutput = JsonSerializer.Serialize(new
                    {
                        artists = rangeArtists,
                        range = new { start = startIndex, end = endIndex, count = rangeArtists.Count },
                        total = totalCount
                    }, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(jsonOutput);
                }
                else
                {
                    _displayService.DisplayArtists(rangeArtists, startIndex);
                    _displayService.DisplayRangeInfo("artists", startIndex, endIndex, rangeArtists.Count, totalCount, verbose);
                }
                return;
            }

            // Use standardized display service for operation start
            if (!json)
            {
                _displayService.DisplayOperationStart("artists", user, displayPeriod, limit, verbose: verbose);
            }

            // Use service layer for basic queries
            TopArtists? result;
            if (isDateRange && fromDate.HasValue && toDate.HasValue)
            {
                result = await _lastFmService.GetUserTopArtistsForDateRangeAsync(user, fromDate.Value, toDate.Value, limit);
            }
            else
            {
                result = await _lastFmService.GetUserTopArtistsAsync(user, resolvedPeriod, limit);
            }

            if (result?.Artists == null || !result.Artists.Any())
            {
                if (json)
                {
                    var emptyResult = new Dictionary<string, object>
                    {
                        ["artist"] = new object[0],
                        ["@attr"] = new Dictionary<string, string>
                        {
                            ["user"] = user,
                            ["totalPages"] = "0",
                            ["page"] = "1",
                            ["total"] = "0",
                            ["perPage"] = limit.ToString()
                        }
                    };
                    var jsonOutput = JsonSerializer.Serialize(emptyResult, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(jsonOutput);
                }
                else
                {
                    _displayService.DisplayError(ErrorMessages.NoArtistsFound);
                }
                return;
            }

            if (json)
            {
                var jsonOutput = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(jsonOutput);
            }
            else
            {
                _displayService.DisplayArtists(result.Artists, 1);
                _displayService.DisplayTotalInfo("artists", result.Attributes.Total, verbose);
            }
        }, timer);
    }
}