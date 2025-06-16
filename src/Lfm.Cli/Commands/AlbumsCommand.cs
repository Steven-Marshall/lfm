using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Cli.Commands;

public class AlbumsCommand : BaseCommand
{
    private readonly IDisplayService _displayService;

    public AlbumsCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        IDisplayService displayService,
        ILogger<AlbumsCommand> logger)
        : base(apiClient, configManager, logger)
    {
        _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
    }

    public async Task ExecuteAsync(int limit, string period, string? username, string? range = null, int? delayMs = null, bool verbose = false)
    {
        await ExecuteWithErrorHandlingAsync("albums command", async () =>
        {
            if (!await ValidateApiKeyAsync())
                return;

            var user = await GetUsernameAsync(username);
            if (user == null)
                return;

            // Handle range logic
            if (!string.IsNullOrEmpty(range))
            {
                var (isValid, startIndex, endIndex, errorMessage) = ParseRange(range);
                if (!isValid)
                {
                    Console.WriteLine(errorMessage);
                    return;
                }
                
                var (rangeAlbums, totalCount) = await ExecuteRangeQueryAsync<Album, TopAlbums>(
                    startIndex,
                    endIndex,
                    _apiClient.GetTopAlbumsAsync,
                    response => response.Albums,
                    response => response.Attributes.Total,
                    "albums",
                    user,
                    period,
                    delayMs,
                    verbose);
                
                if (!rangeAlbums.Any())
                {
                    Console.WriteLine(ErrorMessages.Format(ErrorMessages.NoItemsInRange, "albums"));
                    return;
                }
                
                _displayService.DisplayAlbums(rangeAlbums, startIndex);
                if (verbose)
                {
                    Console.WriteLine($"\nShowing albums {startIndex}-{Math.Min(endIndex, startIndex + rangeAlbums.Count - 1)} of {totalCount}");
                }
                return;
            }

            if (verbose)
            {
                Console.WriteLine($"♫ Getting top {limit} albums for {user} ({period})...\n");
            }

            var result = await _apiClient.GetTopAlbumsAsync(user, period, limit);

            if (result?.Albums == null || !result.Albums.Any())
            {
                Console.WriteLine(ErrorMessages.NoAlbumsFound);
                return;
            }

            _displayService.DisplayAlbums(result.Albums, 1);
            if (verbose)
            {
                Console.WriteLine($"\nTotal albums: {result.Attributes.Total}");
            }
        });
    }

}