using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Lfm.Cli.Commands;

/// <summary>
/// Base class for all Last.fm CLI commands providing common functionality
/// </summary>
public abstract class BaseCommand
{
    protected readonly ILastFmApiClient _apiClient;
    protected readonly IConfigurationManager _configManager;
    protected readonly ILogger _logger;
    protected readonly ISymbolProvider _symbols;
    protected bool _isJsonMode = false;

    protected BaseCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILogger logger,
        ISymbolProvider symbolProvider)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _symbols = symbolProvider ?? throw new ArgumentNullException(nameof(symbolProvider));
    }

    /// <summary>
    /// Configures cache behavior and timing based on command flags
    /// </summary>
    protected void ConfigureCaching(bool timing = false, bool forceCache = false, bool forceApi = false, bool noCache = false)
    {
        if (_apiClient is CachedLastFmApiClient cachedClient)
        {
            if (timing)
            {
                cachedClient.EnableTiming = true;
                cachedClient.TimingResults.Clear();
                cachedClient.WallClockStartTime = DateTime.UtcNow;
            }

            // Set cache behavior based on flags
            if (noCache) cachedClient.CacheBehavior = Lfm.Core.Configuration.CacheBehavior.NoCache;
            else if (forceApi) cachedClient.CacheBehavior = Lfm.Core.Configuration.CacheBehavior.ForceApi;
            else if (forceCache) cachedClient.CacheBehavior = Lfm.Core.Configuration.CacheBehavior.ForceCache;
            else cachedClient.CacheBehavior = Lfm.Core.Configuration.CacheBehavior.Normal;
        }
    }

    /// <summary>
    /// Resolves the time period to use based on provided parameters: --period, --from/--to, or --year
    /// </summary>
    /// <param name="periodStr">Period parameter (e.g., "overall", "12month")</param>
    /// <param name="fromStr">From date parameter</param>
    /// <param name="toStr">To date parameter</param>
    /// <param name="yearStr">Year parameter</param>
    /// <returns>Tuple with (isDateRange, period, fromDate, toDate) - period is used for API calls, dates for aggregation</returns>
    protected (bool isDateRange, string period, DateTime? fromDate, DateTime? toDate) ResolvePeriodParameters(
        string? periodStr, string? fromStr, string? toStr, string? yearStr)
    {
        try
        {
            // Validate parameter combinations
            DateRangeParser.ValidateDateRangeParameters(periodStr, fromStr, toStr, yearStr);

            // Handle --year parameter (highest priority after validation)
            if (!string.IsNullOrWhiteSpace(yearStr))
            {
                var (from, to) = DateRangeParser.ParseYearRange(yearStr);
                return (true, "daterange", from, to);
            }

            // Handle --from/--to parameters
            if (!string.IsNullOrWhiteSpace(fromStr) && !string.IsNullOrWhiteSpace(toStr))
            {
                var (from, to) = DateRangeParser.ParseDateRange(fromStr, toStr);
                return (true, "daterange", from, to);
            }

            // Handle --period parameter (or default)
            var period = periodStr ?? "overall";
            ValidatePeriod(period);
            return (false, period, null, null);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Gets top artists using either period-based or date range API calls
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="isDateRange">Whether to use date range API</param>
    /// <param name="period">Period for period-based API calls</param>
    /// <param name="fromDate">Start date for date range API calls</param>
    /// <param name="toDate">End date for date range API calls</param>
    /// <param name="limit">Number of items to return</param>
    /// <param name="page">Page number (for period-based calls only)</param>
    /// <returns>Top artists response or null</returns>
    protected async Task<TopArtists?> GetTopArtistsWithPeriodAsync(
        string username, bool isDateRange, string period, DateTime? fromDate, DateTime? toDate, 
        int limit = 10, int page = 1)
    {
        if (isDateRange && fromDate.HasValue && toDate.HasValue)
        {
            return await _apiClient.GetTopArtistsForDateRangeAsync(username, fromDate.Value, toDate.Value, limit);
        }
        else
        {
            return await _apiClient.GetTopArtistsAsync(username, period, limit, page);
        }
    }

    /// <summary>
    /// Gets top tracks using either period-based or date range API calls
    /// </summary>
    protected async Task<TopTracks?> GetTopTracksWithPeriodAsync(
        string username, bool isDateRange, string period, DateTime? fromDate, DateTime? toDate, 
        int limit = 10, int page = 1)
    {
        if (isDateRange && fromDate.HasValue && toDate.HasValue)
        {
            return await _apiClient.GetTopTracksForDateRangeAsync(username, fromDate.Value, toDate.Value, limit);
        }
        else
        {
            return await _apiClient.GetTopTracksAsync(username, period, limit, page);
        }
    }

    /// <summary>
    /// Gets top albums using either period-based or date range API calls
    /// </summary>
    protected async Task<TopAlbums?> GetTopAlbumsWithPeriodAsync(
        string username, bool isDateRange, string period, DateTime? fromDate, DateTime? toDate, 
        int limit = 10, int page = 1)
    {
        if (isDateRange && fromDate.HasValue && toDate.HasValue)
        {
            return await _apiClient.GetTopAlbumsForDateRangeAsync(username, fromDate.Value, toDate.Value, limit);
        }
        else
        {
            return await _apiClient.GetTopAlbumsAsync(username, period, limit, page);
        }
    }

    /// <summary>
    /// Validates that API key is configured
    /// </summary>
    /// <returns>True if API key is configured, false otherwise</returns>
    protected async Task<bool> ValidateApiKeyAsync()
    {
        var config = await _configManager.LoadAsync();
        
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            Console.WriteLine(ErrorMessages.NoApiKey);
            Console.WriteLine(ErrorMessages.ApiKeyInfo);
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// Gets the username to use, either provided or from configuration default
    /// </summary>
    /// <param name="providedUsername">Username provided via command line</param>
    /// <returns>Username to use, or null if none available</returns>
    protected async Task<string?> GetUsernameAsync(string? providedUsername)
    {
        if (!string.IsNullOrEmpty(providedUsername))
        {
            return providedUsername;
        }

        var config = await _configManager.LoadAsync();
        var defaultUsername = config.DefaultUsername;
        
        if (string.IsNullOrEmpty(defaultUsername))
        {
            Console.WriteLine(ErrorMessages.NoUsername);
            return null;
        }
        
        return defaultUsername;
    }

    /// <summary>
    /// Parses and validates a range string (e.g., "10-20")
    /// </summary>
    /// <param name="range">Range string to parse</param>
    /// <returns>Tuple with validation result, start index, end index, and error message</returns>
    protected (bool isValid, int start, int end, string? errorMessage) ParseRange(string range)
    {
        var parts = range.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int start) || !int.TryParse(parts[1], out int end))
        {
            return (false, 0, 0, ErrorMessages.InvalidRangeFormat);
        }
        
        if (start < 1 || end < start)
        {
            return (false, 0, 0, ErrorMessages.InvalidRangeValues);
        }
        
        return (true, start, end, null);
    }

    /// <summary>
    /// Validates that a period is supported by the Last.fm API
    /// </summary>
    /// <param name="period">Period to validate</param>
    /// <exception cref="ArgumentException">Thrown when period is invalid</exception>
    protected static void ValidatePeriod(string period)
    {
        var validPeriods = new HashSet<string>
        {
            "overall", "7day", "1month", "3month", "6month", "12month"
        };

        if (!validPeriods.Contains(period))
        {
            var validPeriodsStr = string.Join(", ", validPeriods.OrderBy(p => p));
            throw new ArgumentException($"Invalid period '{period}'. Valid periods are: {validPeriodsStr}");
        }
    }

    /// <summary>
    /// Validates that a limit is within Last.fm API constraints
    /// </summary>
    /// <param name="limit">Limit to validate</param>
    /// <exception cref="ArgumentException">Thrown when limit is invalid</exception>
    protected static void ValidateLimit(int limit)
    {
        if (limit < 1 || limit > 1000)
        {
            throw new ArgumentException($"Invalid limit '{limit}'. Limit must be between 1 and 1000");
        }
    }

    /// <summary>
    /// Validates that a username is not empty or whitespace
    /// </summary>
    /// <param name="username">Username to validate</param>
    /// <exception cref="ArgumentException">Thrown when username is invalid</exception>
    protected static void ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be empty");
        }
    }

    /// <summary>
    /// Validates that an artist name is not empty or whitespace
    /// </summary>
    /// <param name="artist">Artist name to validate</param>
    /// <exception cref="ArgumentException">Thrown when artist name is invalid</exception>
    protected static void ValidateArtist(string? artist)
    {
        if (string.IsNullOrWhiteSpace(artist))
        {
            throw new ArgumentException("Artist name cannot be empty");
        }
    }

    /// <summary>
    /// Handles and logs command execution errors
    /// </summary>
    /// <param name="ex">Exception that occurred</param>
    /// <param name="operation">Description of the operation that failed</param>
    protected void HandleCommandError(Exception ex, string operation)
    {
        _logger.LogError(ex, "Error executing {Operation}", operation);

        if (_isJsonMode)
        {
            var errorOutput = new { success = false, error = ex.Message };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorOutput,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    /// <summary>
    /// Validates that an artist name is not empty or whitespace
    /// </summary>
    /// <param name="artist">Artist name to validate</param>
    /// <returns>True if artist name is valid, false otherwise</returns>
    protected bool ValidateArtistName(string artist)
    {
        if (string.IsNullOrWhiteSpace(artist))
        {
            Console.WriteLine(ErrorMessages.EmptyArtistName);
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// Executes a command with standard error handling
    /// </summary>
    /// <param name="operation">Description of the operation</param>
    /// <param name="commandLogic">The command logic to execute</param>
    protected async Task ExecuteWithErrorHandlingAsync(string operation, Func<Task> commandLogic)
    {
        try
        {
            await commandLogic();
        }
        catch (Exception ex)
        {
            HandleCommandError(ex, operation);
        }
    }

    /// <summary>
    /// Applies API rate limiting delay if configured
    /// </summary>
    /// <param name="overrideDelayMs">Override delay from CLI parameter, null to use config</param>
    protected async Task ApplyApiThrottleAsync(int? overrideDelayMs = null)
    {
        int delayMs;
        
        if (overrideDelayMs.HasValue)
        {
            delayMs = overrideDelayMs.Value;
        }
        else
        {
            var config = await _configManager.LoadAsync();
            delayMs = config.ApiThrottleMs;
        }
        
        if (delayMs > 0)
        {
            await Task.Delay(delayMs);
        }
    }

    /// <summary>
    /// Executes a range query across multiple pages and returns the specified range of items
    /// </summary>
    /// <typeparam name="T">Type of items to retrieve</typeparam>
    /// <typeparam name="TResponse">Type of API response</typeparam>
    /// <param name="startIndex">Starting position (1-based)</param>
    /// <param name="endIndex">Ending position (1-based, inclusive)</param>
    /// <param name="apiCall">Function to call API for a specific page</param>
    /// <param name="extractItems">Function to extract items from API response</param>
    /// <param name="extractTotal">Function to extract total count from API response</param>
    /// <param name="itemTypeName">Name of item type for display messages</param>
    /// <param name="user">Username for display messages</param>
    /// <param name="period">Time period for display messages</param>
    /// <param name="overrideDelayMs">Override delay from CLI parameter</param>
    /// <param name="verbose">Whether to show verbose output</param>
    /// <returns>Tuple with items list and total count</returns>
    protected async Task<(List<T> items, string totalCount)> ExecuteRangeQueryAsync<T, TResponse>(
        int startIndex,
        int endIndex,
        Func<string, string, int, int, Task<TResponse?>> apiCall,
        Func<TResponse, List<T>> extractItems,
        Func<TResponse, string> extractTotal,
        string itemTypeName,
        string user,
        string period,
        int? overrideDelayMs = null,
        bool verbose = false)
        where TResponse : class
    {
        if (verbose)
        {
            Console.WriteLine($"Getting {itemTypeName} {startIndex}-{endIndex} for {user} ({period})...\n");
        }

        var allItems = new List<T>();
        int rangeSize = endIndex - startIndex + 1;
        string totalCount = "0";

        // Calculate which pages we need
        int startPage = ((startIndex - 1) / Lfm.Core.Configuration.SearchConstants.Api.MaxItemsPerPage) + 1;
        int endPage = ((endIndex - 1) / Lfm.Core.Configuration.SearchConstants.Api.MaxItemsPerPage) + 1;

        for (int page = startPage; page <= endPage && allItems.Count < rangeSize; page++)
        {
            // Apply throttling before API call (except for first page)
            if (page > startPage)
            {
                await ApplyApiThrottleAsync(overrideDelayMs);
            }

            var pageResult = await apiCall(user, period, Lfm.Core.Configuration.SearchConstants.Api.MaxItemsPerPage, page);

            if (pageResult == null)
            {
                break;
            }

            var pageItems = extractItems(pageResult);
            if (pageItems == null || !pageItems.Any())
            {
                break;
            }

            totalCount = extractTotal(pageResult);

            // Calculate which items from this page we need
            int pageStartPosition = (page - 1) * Lfm.Core.Configuration.SearchConstants.Api.MaxItemsPerPage + 1;

            int takeStartIndex = Math.Max(0, startIndex - pageStartPosition);
            int takeEndIndex = Math.Min(Lfm.Core.Configuration.SearchConstants.Api.MaxItemsPerPage - 1, endIndex - pageStartPosition);
            int takeCount = takeEndIndex - takeStartIndex + 1;

            if (takeCount > 0)
            {
                var pageSelection = pageItems
                    .Skip(takeStartIndex)
                    .Take(takeCount)
                    .ToList();

                allItems.AddRange(pageSelection);
            }
        }

        var rangeItems = allItems.Take(rangeSize).ToList();
        return (rangeItems, totalCount);
    }

    /// <summary>
    /// Displays timing information from cached API client if timing is enabled
    /// </summary>
    protected void DisplayTimingResults()
    {
        if (_apiClient is CachedLastFmApiClient cachedClient && cachedClient.EnableTiming)
        {
            var timingResults = cachedClient.TimingResults;
            if (timingResults.Any())
            {
                Console.WriteLine($"\n{_symbols.Timer} API Timing Results:");
                Console.WriteLine("Method              | Cache | Time (ms) | Details");
                Console.WriteLine("--------------------+-------+-----------+----------");

                var totalTime = 0L;
                var cacheHits = 0;
                var totalCalls = timingResults.Count;

                foreach (var timing in timingResults)
                {
                    var status = timing.CacheHit ? " HIT " : "MISS";
                    Console.WriteLine($"{timing.Method,-19} | {status} | {timing.ElapsedMs,8} | {timing.Details}");
                    totalTime += timing.ElapsedMs;
                    if (timing.CacheHit) cacheHits++;
                }

                Console.WriteLine("--------------------+-------+-----------+----------");

                // Calculate wall-clock time if available
                if (cachedClient.WallClockStartTime.HasValue)
                {
                    var wallClockMs = (long)(DateTime.UtcNow - cachedClient.WallClockStartTime.Value).TotalMilliseconds;
                    var speedupRatio = totalTime > 0 ? (double)totalTime / wallClockMs : 1.0;
                    Console.WriteLine($"Total: {totalCalls} calls, {cacheHits} cache hits ({(double)cacheHits/totalCalls*100:F1}%)");
                    Console.WriteLine($"Wall-clock time: {wallClockMs}ms ({wallClockMs/1000.0:F2}s)");
                    Console.WriteLine($"Sum of API times: {totalTime}ms ({speedupRatio:F2}x parallelization)");
                }
                else
                {
                    Console.WriteLine($"Total: {totalCalls} calls, {cacheHits} cache hits ({(double)cacheHits/totalCalls*100:F1}%), {totalTime}ms");
                }

                // Clear results for next command
                timingResults.Clear();
                cachedClient.WallClockStartTime = null;
            }
        }
    }

    /// <summary>
    /// Executes a command with standard error handling and optional timing measurement
    /// </summary>
    /// <param name="operation">Description of the operation</param>
    /// <param name="commandLogic">The command logic to execute</param>
    /// <param name="enableTimer">Whether to measure and display total execution time</param>
    protected async Task ExecuteWithErrorHandlingAndTimerAsync(string operation, Func<Task> commandLogic, bool enableTimer = false)
    {
        Stopwatch? timer = null;
        
        if (enableTimer)
        {
            timer = Stopwatch.StartNew();
        }
        
        try
        {
            await commandLogic();
        }
        catch (Exception ex)
        {
            HandleCommandError(ex, operation);
        }
        finally
        {
            if (timer != null)
            {
                timer.Stop();
                Console.WriteLine($"\n{_symbols.Timer} Total execution time: {timer.ElapsedMilliseconds}ms ({timer.Elapsed.TotalSeconds:F2}s)");
            }
        }
    }

    /// <summary>
    /// Executes a command with optional end-to-end timing measurement
    /// </summary>
    /// <param name="commandLogic">The command logic to execute</param>
    /// <param name="enableTimer">Whether to measure and display total execution time</param>
    protected async Task ExecuteWithTimerAsync(Func<Task> commandLogic, bool enableTimer = false)
    {
        Stopwatch? timer = null;
        
        if (enableTimer)
        {
            timer = Stopwatch.StartNew();
        }
        
        try
        {
            await commandLogic();
        }
        finally
        {
            if (timer != null)
            {
                timer.Stop();
                Console.WriteLine($"\n{_symbols.Timer} Total execution time: {timer.ElapsedMilliseconds}ms ({timer.Elapsed.TotalSeconds:F2}s)");
            }
        }
    }

    /// <summary>
    /// Determines the display format for period information
    /// </summary>
    /// <param name="isDateRange">Whether this is a date range query</param>
    /// <param name="fromDate">Start date for date range</param>
    /// <param name="toDate">End date for date range</param>
    /// <param name="resolvedPeriod">Standard period string</param>
    /// <returns>Formatted period string for display</returns>
    protected string GetDisplayPeriod(bool isDateRange, DateTime? fromDate, DateTime? toDate, string resolvedPeriod)
    {
        return isDateRange && fromDate.HasValue && toDate.HasValue
            ? DateRangeParser.FormatDateRange(fromDate.Value, toDate.Value)
            : resolvedPeriod;
    }

    /// <summary>
    /// Validates range parameter and displays standardized error message if invalid
    /// </summary>
    /// <param name="range">Range string to validate</param>
    /// <param name="displayService">Display service for error messages</param>
    /// <param name="startIndex">Output start index if valid</param>
    /// <param name="endIndex">Output end index if valid</param>
    /// <returns>True if range is valid, false otherwise</returns>
    protected bool ValidateAndHandleRange(string range, IDisplayService displayService, out int startIndex, out int endIndex)
    {
        var (isValid, start, end, errorMessage) = ParseRange(range);
        if (!isValid)
        {
            displayService.DisplayValidationError(errorMessage ?? "Invalid range format");
            startIndex = endIndex = 0;
            return false;
        }
        startIndex = start;
        endIndex = end;
        return true;
    }
}