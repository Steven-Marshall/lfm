using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Lfm.Core.Services.Cache;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

/// <summary>
/// Command to clear cache entries with various options
/// </summary>
public class CacheClearCommand : BaseCommand
{
    private readonly ICacheStorage _cacheStorage;

    public CacheClearCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ICacheStorage cacheStorage,
        ILogger<CacheClearCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _cacheStorage = cacheStorage ?? throw new ArgumentNullException(nameof(cacheStorage));
    }

    public async Task ExecuteAsync(bool expiredOnly = false, bool confirm = false)
    {
        await ExecuteWithErrorHandlingAsync("cache-clear command", async () =>
        {
            var stats = await _cacheStorage.GetStatisticsAsync();
            
            if (stats.TotalEntries == 0)
            {
                Console.WriteLine("Cache is already empty.");
                return;
            }

            // Show what will be cleared
            if (expiredOnly)
            {
                if (stats.ExpiredEntries == 0)
                {
                    Console.WriteLine("No expired cache entries found.");
                    return;
                }
                
                Console.WriteLine($"Will remove {stats.ExpiredEntries} expired cache entries.");
            }
            else
            {
                var sizeMB = stats.TotalSizeBytes / (1024.0 * 1024.0);
                Console.WriteLine($"Will remove all {stats.TotalEntries} cache entries ({sizeMB:F1} MB).");
            }

            // Confirmation prompt
            if (!confirm)
            {
                Console.Write("Are you sure? (y/N): ");
                var response = Console.ReadLine();
                if (string.IsNullOrEmpty(response) || !response.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Cache clear cancelled.");
                    return;
                }
            }

            // Perform the clear operation
            int removedCount;
            var startTime = DateTime.Now;
            
            if (expiredOnly)
            {
                Console.WriteLine($"{_symbols.Cleanup} Removing expired cache entries...");
                removedCount = await _cacheStorage.CleanupExpiredAsync();
            }
            else
            {
                Console.WriteLine($"{_symbols.Cleanup} Clearing all cache entries...");
                var success = await _cacheStorage.ClearAllAsync();
                removedCount = success ? stats.TotalEntries : 0;
            }

            var elapsed = DateTime.Now - startTime;
            
            if (removedCount > 0)
            {
                Console.WriteLine($"{_symbols.Success} Successfully removed {removedCount} cache entries in {elapsed.TotalMilliseconds:F0}ms");
                
                // Update last cleanup time in config if we did a cleanup
                if (expiredOnly)
                {
                    var config = await _configManager.LoadAsync();
                    config.LastCacheCleanup = DateTime.Now;
                    await _configManager.SaveAsync(config);
                }
            }
            else
            {
                Console.WriteLine($"{_symbols.Error} Failed to clear cache entries");
            }
        });
    }
}