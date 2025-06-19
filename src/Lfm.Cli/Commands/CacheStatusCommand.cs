using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Lfm.Core.Services.Cache;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

/// <summary>
/// Command to display cache status and statistics
/// </summary>
public class CacheStatusCommand : BaseCommand
{
    private readonly ICacheStorage _cacheStorage;

    public CacheStatusCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ICacheStorage cacheStorage,
        ILogger<CacheStatusCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _cacheStorage = cacheStorage ?? throw new ArgumentNullException(nameof(cacheStorage));
    }

    public async Task ExecuteAsync()
    {
        await ExecuteWithErrorHandlingAsync("cache-status command", async () =>
        {
            var config = await _configManager.LoadAsync();
            var stats = await _cacheStorage.GetStatisticsAsync();

            Console.WriteLine($"{_symbols.Stats} Cache Status:");
            Console.WriteLine();

            // Configuration
            Console.WriteLine($"{_symbols.Settings} Configuration:");
            Console.WriteLine($"   Cache Enabled: {(config.CacheEnabled ? $"{_symbols.Success} Yes" : $"{_symbols.Error} No")}");
            Console.WriteLine($"   Cache Directory: {stats.CacheDirectory}");
            Console.WriteLine($"   Expiry Time: {config.CacheExpiryMinutes} minutes");
            Console.WriteLine($"   Max Size: {config.MaxCacheSizeMB} MB");
            Console.WriteLine($"   Max Files: {config.MaxCacheFiles:N0}");
            Console.WriteLine($"   Max Age: {config.MaxCacheAgeDays} days");
            Console.WriteLine($"   Cleanup Interval: {config.CleanupIntervalHours} hours");
            Console.WriteLine();

            // Statistics
            Console.WriteLine($"{_symbols.Stats} Current Usage:");
            
            if (stats.TotalEntries == 0)
            {
                Console.WriteLine("   Cache is empty");
                return;
            }

            var sizeMB = stats.TotalSizeBytes / (1024.0 * 1024.0);
            var sizePercent = (sizeMB / config.MaxCacheSizeMB) * 100;
            var filePercent = (double)stats.TotalFiles / config.MaxCacheFiles * 100;

            Console.WriteLine($"   Entries: {stats.TotalEntries:N0}");
            Console.WriteLine($"   Files: {stats.TotalFiles:N0} / {config.MaxCacheFiles:N0} ({filePercent:F1}%)");
            Console.WriteLine($"   Size: {sizeMB:F1} MB / {config.MaxCacheSizeMB} MB ({sizePercent:F1}%)");
            Console.WriteLine($"   Expired: {stats.ExpiredEntries:N0} entries");
            
            if (stats.OldestEntry.HasValue)
            {
                var oldestAge = DateTime.UtcNow - stats.OldestEntry.Value;
                Console.WriteLine($"   Oldest Entry: {FormatTimeAgo(oldestAge)}");
            }
            
            if (stats.NewestEntry.HasValue)
            {
                var newestAge = DateTime.UtcNow - stats.NewestEntry.Value;
                Console.WriteLine($"   Newest Entry: {FormatTimeAgo(newestAge)}");
            }

            Console.WriteLine();

            // Cleanup information
            Console.WriteLine("🧹 Cleanup Status:");
            var timeSinceLastCleanup = DateTime.Now - config.LastCacheCleanup;
            var cleanupInterval = TimeSpan.FromHours(config.CleanupIntervalHours);
            
            if (config.LastCacheCleanup == DateTime.MinValue)
            {
                Console.WriteLine("   Last Cleanup: Never");
            }
            else
            {
                Console.WriteLine($"   Last Cleanup: {FormatTimeAgo(timeSinceLastCleanup)}");
            }
            
            var nextCleanup = cleanupInterval - timeSinceLastCleanup;
            if (nextCleanup.TotalMilliseconds > 0)
            {
                Console.WriteLine($"   Next Cleanup: in {FormatDuration(nextCleanup)}");
            }
            else
            {
                Console.WriteLine("   Next Cleanup: Due now");
            }

            // Warnings
            Console.WriteLine();
            if (sizePercent > 80)
            {
                Console.WriteLine($"{_symbols.Error}  Cache size is approaching limit (>80%)");
            }
            if (filePercent > 80)
            {
                Console.WriteLine($"{_symbols.Error}  Cache file count is approaching limit (>80%)");
            }
            // Smart expired entries warning - considers cleanup recency and storage impact
            var expiredPercent = stats.TotalEntries > 0 ? (double)stats.ExpiredEntries / stats.TotalEntries : 0;
            var cleanupOverdue = config.LastCacheCleanup == DateTime.MinValue || timeSinceLastCleanup > cleanupInterval.Add(TimeSpan.FromHours(2));
            var significantExpiredCount = expiredPercent > 0.3; // >30% expired
            var recentCleanup = timeSinceLastCleanup < TimeSpan.FromMinutes(30);
            
            if (significantExpiredCount && (cleanupOverdue || !recentCleanup))
            {
                if (cleanupOverdue)
                {
                    Console.WriteLine($"{_symbols.Error}  Many expired entries detected ({stats.ExpiredEntries}/{stats.TotalEntries}) and cleanup is overdue - consider running 'lfm cache-clear --expired'");
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error}  Many expired entries detected ({stats.ExpiredEntries}/{stats.TotalEntries}) - consider running 'lfm cache-clear --expired'");
                }
            }
        });
    }

    private static string FormatTimeAgo(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{timeSpan.Days} day{(timeSpan.Days == 1 ? "" : "s")} ago";
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours} hour{(timeSpan.Hours == 1 ? "" : "s")} ago";
        if (timeSpan.TotalMinutes >= 1)
            return $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes == 1 ? "" : "s")} ago";
        return "just now";
    }

    private static string FormatDuration(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{timeSpan.Days} day{(timeSpan.Days == 1 ? "" : "s")}";
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours} hour{(timeSpan.Hours == 1 ? "" : "s")}";
        return $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes == 1 ? "" : "s")}";
    }
}