using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

public class AlbumsCommand : BaseCommand
{
    private readonly ILastFmService _lastFmService;
    private readonly IDisplayService _displayService;

    public AlbumsCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILastFmService lastFmService,
        IDisplayService displayService,
        ILogger<AlbumsCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _lastFmService = lastFmService ?? throw new ArgumentNullException(nameof(lastFmService));
        _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
    }

    public async Task ExecuteAsync(int limit, string? period, string? username, string? range = null, int? delayMs = null, bool verbose = false, bool timing = false, bool forceCache = false, bool forceApi = false, bool noCache = false, bool timer = false, string? from = null, string? to = null, string? year = null)
    {
        await ExecuteWithErrorHandlingAndTimerAsync("albums command", async () =>
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
                    Console.WriteLine(errorMessage);
                    return;
                }
                
                _displayService.DisplayOperationStart("albums", user, displayPeriod, null, startIndex, endIndex, verbose);

                // Use service layer for range query
                var (rangeAlbums, totalCount) = await _lastFmService.GetUserTopAlbumsRangeAsync(user, resolvedPeriod, startIndex, endIndex);
                
                if (!rangeAlbums.Any())
                {
                    Console.WriteLine(ErrorMessages.Format(ErrorMessages.NoItemsInRange, "albums"));
                    return;
                }
                
                _displayService.DisplayAlbums(rangeAlbums, startIndex);
                _displayService.DisplayRangeInfo("albums", startIndex, endIndex, rangeAlbums.Count, totalCount, verbose);
                return;
            }

            // Use standardized display service for operation start
            _displayService.DisplayOperationStart("albums", user, displayPeriod, limit, verbose: verbose);

            // Use service layer for basic query
            TopAlbums? result;
            if (isDateRange && fromDate.HasValue && toDate.HasValue)
            {
                result = await _lastFmService.GetUserTopAlbumsForDateRangeAsync(user, fromDate.Value, toDate.Value, limit);
            }
            else
            {
                result = await _lastFmService.GetUserTopAlbumsAsync(user, resolvedPeriod, limit);
            }

            if (result?.Albums == null || !result.Albums.Any())
            {
                if (isDateRange)
                {
                    _displayService.DisplayError("No albums found for the specified date range.");
                    _displayService.DisplayError("Note: Album data depends on Last.fm's track metadata. Many tracks may not have complete album information.");
                    _displayService.DisplayError("Try using --period overall or a different time period, or use the 'tracks' command instead.");
                }
                else
                {
                    _displayService.DisplayError(ErrorMessages.NoAlbumsFound);
                }
                return;
            }

            _displayService.DisplayAlbums(result.Albums, 1);
            _displayService.DisplayTotalInfo("albums", result.Attributes.Total, verbose);

        }, timer);
    }
}