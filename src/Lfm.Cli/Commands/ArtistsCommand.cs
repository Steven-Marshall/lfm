using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;

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
            
            // Determine display period format for consistent messaging
            var displayPeriod = isDateRange && fromDate.HasValue && toDate.HasValue 
                ? DateRangeParser.FormatDateRange(fromDate.Value, toDate.Value)
                : resolvedPeriod;
            
            // Handle range logic using service layer
            if (!string.IsNullOrEmpty(range))
            {
                var (isValid, startIndex, endIndex, errorMessage) = ParseRange(range);
                if (!isValid)
                {
                    _displayService.DisplayValidationError(errorMessage ?? "Invalid range format");
                    return;
                }
                
                _displayService.DisplayOperationStart("artists", user, displayPeriod, null, startIndex, endIndex, verbose);

                // Use service layer for range query
                var (rangeArtists, totalCount) = await _lastFmService.GetUserTopArtistsRangeAsync(user, resolvedPeriod, startIndex, endIndex);
                
                if (!rangeArtists.Any())
                {
                    _displayService.DisplayError(ErrorMessages.Format(ErrorMessages.NoItemsInRange, "artists"));
                    return;
                }
                
                _displayService.DisplayArtists(rangeArtists, startIndex);
                _displayService.DisplayRangeInfo("artists", startIndex, endIndex, rangeArtists.Count, totalCount, verbose);
                return;
            }

            // Use standardized display service for operation start
            _displayService.DisplayOperationStart("artists", user, displayPeriod, limit, verbose: verbose);

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
                _displayService.DisplayError(ErrorMessages.NoArtistsFound);
                return;
            }

            _displayService.DisplayArtists(result.Artists, 1);
            _displayService.DisplayTotalInfo("artists", result.Attributes.Total, verbose);
        }, timer);
    }
}