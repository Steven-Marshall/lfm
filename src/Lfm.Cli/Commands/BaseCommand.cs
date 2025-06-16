using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

/// <summary>
/// Base class for all Last.fm CLI commands providing common functionality
/// </summary>
public abstract class BaseCommand
{
    protected readonly ILastFmApiClient _apiClient;
    protected readonly IConfigurationManager _configManager;
    protected readonly ILogger _logger;

    protected BaseCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILogger logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    /// Handles and logs command execution errors
    /// </summary>
    /// <param name="ex">Exception that occurred</param>
    /// <param name="operation">Description of the operation that failed</param>
    protected void HandleCommandError(Exception ex, string operation)
    {
        _logger.LogError(ex, "Error executing {Operation}", operation);
        Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
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
            Console.WriteLine($"♫ Getting {itemTypeName} {startIndex}-{endIndex} for {user} ({period})...\n");
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
}