using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

public class ArtistsCommand : BaseCommand
{
    private readonly IDisplayService _displayService;

    public ArtistsCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        IDisplayService displayService,
        ILogger<ArtistsCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
    }

    public async Task ExecuteAsync(int limit, string? period, string? username, string? range = null, int? delayMs = null, bool verbose = false, bool timing = false, bool forceCache = false, bool forceApi = false, bool noCache = false, bool timer = false, string? from = null, string? to = null, string? year = null)
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

            // Resolve period parameters (--period, --from/--to, or --year)
            var (isDateRange, resolvedPeriod, fromDate, toDate) = ResolvePeriodParameters(period, from, to, year);
            
            // Handle range logic
            if (!string.IsNullOrEmpty(range))
            {
                var (isValid, startIndex, endIndex, errorMessage) = ParseRange(range);
                if (!isValid)
                {
                    Console.WriteLine(errorMessage);
                    return;
                }
                
                // Note: Range queries with date ranges currently use period-based API calls
                // This could be enhanced in the future to support date range + range queries
                var (rangeArtists, totalCount) = await ExecuteRangeQueryAsync<Artist, TopArtists>(
                    startIndex,
                    endIndex,
                    _apiClient.GetTopArtistsAsync,
                    response => response.Artists,
                    response => response.Attributes.Total,
                    "artists",
                    user,
                    resolvedPeriod,
                    delayMs,
                    verbose);
                
                if (!rangeArtists.Any())
                {
                    Console.WriteLine(ErrorMessages.Format(ErrorMessages.NoItemsInRange, "artists"));
                    return;
                }
                
                _displayService.DisplayArtists(rangeArtists, startIndex);
                _displayService.DisplayRangeInfo("artists", startIndex, endIndex, rangeArtists.Count, totalCount, verbose);
                return;
            }

            // Display appropriate message based on query type
            if (verbose)
            {
                if (isDateRange && fromDate.HasValue && toDate.HasValue)
                {
                    Console.WriteLine($"Getting top {limit} artists for {user} ({DateRangeParser.FormatDateRange(fromDate.Value, toDate.Value)})...\n");
                }
                else
                {
                    Console.WriteLine($"Getting top {limit} artists for {user} ({resolvedPeriod})...\n");
                }
            }

            // Get artists using appropriate API method
            var result = await GetTopArtistsWithPeriodAsync(user, isDateRange, resolvedPeriod, fromDate, toDate, limit);

            if (result?.Artists == null || !result.Artists.Any())
            {
                Console.WriteLine(ErrorMessages.NoArtistsFound);
                return;
            }

            _displayService.DisplayArtists(result.Artists, 1);
            _displayService.DisplayTotalInfo("artists", result.Attributes.Total, verbose);
        }, timer);
    }
}