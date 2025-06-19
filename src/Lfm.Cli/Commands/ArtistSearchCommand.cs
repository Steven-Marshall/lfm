using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Cli.Commands;

/// <summary>
/// Generic command for searching user's listening history by artist
/// </summary>
/// <typeparam name="T">Type of items to search (Track or Album)</typeparam>
/// <typeparam name="TResponse">Type of API response</typeparam>
public class ArtistSearchCommand<T, TResponse> : BaseCommand
    where T : class
    where TResponse : class
{
    private readonly IDisplayService _displayService;
    private readonly string _itemTypeName;
    private readonly Func<string, string, int, int, Task<TResponse?>> _apiCall;
    private readonly Func<TResponse, List<T>> _extractItems;
    private readonly Func<T, string> _getArtistName;
    private readonly Action<List<T>, int> _displayMethod;

    public ArtistSearchCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        IDisplayService displayService,
        ILogger logger,
        ISymbolProvider symbolProvider,
        string itemTypeName,
        Func<string, string, int, int, Task<TResponse?>> apiCall,
        Func<TResponse, List<T>> extractItems,
        Func<T, string> getArtistName,
        Action<List<T>, int> displayMethod)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
        _itemTypeName = itemTypeName ?? throw new ArgumentNullException(nameof(itemTypeName));
        _apiCall = apiCall ?? throw new ArgumentNullException(nameof(apiCall));
        _extractItems = extractItems ?? throw new ArgumentNullException(nameof(extractItems));
        _getArtistName = getArtistName ?? throw new ArgumentNullException(nameof(getArtistName));
        _displayMethod = displayMethod ?? throw new ArgumentNullException(nameof(displayMethod));
    }

    public async Task ExecuteAsync(string artist, int limit, bool deep = false, int? delayMs = null, int? depth = null, int? timeoutSeconds = null, bool verbose = false, bool timing = false, bool forceCache = false, bool forceApi = false, bool noCache = false, bool timer = false)
    {
        await ExecuteWithErrorHandlingAndTimerAsync($"artist-{_itemTypeName} command", async () =>
        {
            // Configure cache behavior and timing
            if (_apiClient is CachedLastFmApiClient cachedClient)
            {
                if (timing)
                {
                    cachedClient.EnableTiming = true;
                    cachedClient.TimingResults.Clear();
                }
                
                // Set cache behavior based on flags
                if (noCache) cachedClient.CacheBehavior = Lfm.Core.Configuration.CacheBehavior.NoCache;
                else if (forceApi) cachedClient.CacheBehavior = Lfm.Core.Configuration.CacheBehavior.ForceApi;
                else if (forceCache) cachedClient.CacheBehavior = Lfm.Core.Configuration.CacheBehavior.ForceCache;
                else cachedClient.CacheBehavior = Lfm.Core.Configuration.CacheBehavior.Normal;
            }

            if (!await ValidateApiKeyAsync())
                return;

            var user = await GetUsernameAsync(null);
            if (user == null)
                return;

            if (!ValidateArtistName(artist))
                return;

            // Load config for search parameters
            var config = await _configManager.LoadAsync();
            
            // Determine search depth
            int maxItems;
            if (depth.HasValue)
            {
                maxItems = depth.Value == 0 ? int.MaxValue : depth.Value;
            }
            else if (deep)
            {
                maxItems = int.MaxValue; // Unlimited for --deep
            }
            else
            {
                maxItems = config.NormalSearchDepth; // Use config default
            }
            
            // Determine timeout
            int timeoutMs = timeoutSeconds.HasValue 
                ? (timeoutSeconds.Value == 0 ? -1 : timeoutSeconds.Value * 1000)
                : config.DeepSearchTimeoutSeconds * 1000;

            if (verbose)
            {
                Console.WriteLine($"Getting your top {_itemTypeName} by {artist}...");
                
                // Display search parameters
                if (maxItems == int.MaxValue)
                {
                    Console.WriteLine($"(Unlimited search through ALL your {_itemTypeName})");
                }
                else
                {
                    Console.WriteLine($"(Searching through up to {maxItems:N0} {_itemTypeName})");
                }
                
                if (timeoutMs > 0)
                {
                    Console.WriteLine($"(Timeout: {timeoutMs / 1000} seconds)");
                }
                Console.WriteLine("Press Ctrl+C to cancel search\n");
            }
            
            var artistItems = new List<T>();
            var maxPages = maxItems == int.MaxValue ? int.MaxValue : (maxItems / Api.RecommendedPageSize) + 1;
            var itemsSearched = 0;
            
            // Setup cancellation tokens for timeout and user cancellation
            using var timeoutCts = timeoutMs > 0 ? new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs)) : new CancellationTokenSource();
            using var userCts = new CancellationTokenSource();
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, userCts.Token);
            
            // Register Ctrl+C handler
            bool userCancelled = false;
            void CancelHandler(object? sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true; // Prevent immediate termination
                userCancelled = true;
                userCts.Cancel();
            }
            
            Console.CancelKeyPress += CancelHandler;
            
            try
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync($"Searching {_itemTypeName}...", async ctx =>
                {
                    for (int page = 1; page <= maxPages && itemsSearched < maxItems; page++)
                    {
                        // Check for cancellation (timeout or user)
                        combinedCts.Token.ThrowIfCancellationRequested();
                        
                        // Apply throttling before API call (except for first page)
                        if (page > 1)
                        {
                            await ApplyApiThrottleAsync(delayMs);
                        }
                        
                        var result = await _apiCall(user, Defaults.TimePeriod, Api.RecommendedPageSize, page);
                        
                        if (result == null)
                        {
                            break;
                        }
                        
                        var pageItems = _extractItems(result);
                        if (pageItems == null || !pageItems.Any())
                        {
                            break;
                        }
                        
                        // Respect depth limit
                        int itemsToProcess = Math.Min(pageItems.Count, maxItems - itemsSearched);
                        var limitedPageItems = pageItems.Take(itemsToProcess).ToList();
                        
                        itemsSearched += limitedPageItems.Count;
                        
                        // Filter items by artist for this page and add matches
                        var pageMatches = limitedPageItems
                            .Where(item => _getArtistName(item).Equals(artist, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        
                        artistItems.AddRange(pageMatches);
                        
                        // Update status
                        var depthInfo = maxItems == int.MaxValue ? "" : $" (depth: {itemsSearched:N0}/{maxItems:N0})";
                        ctx.Status($"Searched {itemsSearched:N0} {_itemTypeName}, found {artistItems.Count} matches{depthInfo}...");
                        
                        // Stop if we have enough matches or no more pages
                        if (pageItems.Count < Api.MaxItemsPerPage)
                        {
                            break;
                        }
                        
                        // Stop early if we have way more matches than needed (but not for unlimited searches)
                        if (maxItems != int.MaxValue && artistItems.Count >= limit * ArtistSearch.EarlyTerminationMultiplier)
                        {
                            break;
                        }
                    }
                });
            
                // Take only the requested limit, preserving the original order (highest play counts first)
                artistItems = artistItems.Take(limit).ToList();
                
                if (verbose)
                {
                    Console.WriteLine($"Finished searching {itemsSearched} {_itemTypeName} total.\n");
                }
                
                if (!artistItems.Any())
                {
                    Console.WriteLine(ErrorMessages.Format(ErrorMessages.NoArtistItemsFound, _itemTypeName, artist));
                    Console.WriteLine(ErrorMessages.ArtistSearchSuggestion);
                    return;
                }

                _displayMethod(artistItems, 1);
                if (verbose)
                {
                    Console.WriteLine($"\nShowing your top {artistItems.Count} {_itemTypeName} by: {artist}");
                }
            }
            catch (OperationCanceledException)
            {
                if (userCancelled)
                {
                    Console.WriteLine($"\n🛑 Search cancelled by user.");
                }
                else
                {
                    Console.WriteLine($"\n⏰ Search timed out after {timeoutMs / 1000} seconds.");
                }
                
                Console.WriteLine($"Searched {itemsSearched:N0} {_itemTypeName} and found {artistItems.Count} matches.");
                
                if (artistItems.Any())
                {
                    // Take only the requested limit, preserving the original order
                    artistItems = artistItems.Take(limit).ToList();
                    if (verbose)
                    {
                        Console.WriteLine($"\nShowing partial results - your top {artistItems.Count} {_itemTypeName} by: {artist}");
                    }
                    _displayMethod(artistItems, 1);
                }
                else
                {
                    var reason = userCancelled ? "cancellation" : "timeout";
                    Console.WriteLine($"No matches found before {reason}.");
                }
            }
            finally
            {
                // Clean up Ctrl+C handler
                Console.CancelKeyPress -= CancelHandler;
            }

            // Display timing results if enabled
            if (timing)
            {
                DisplayTimingResults();
            }
        }, timer);
    }
}