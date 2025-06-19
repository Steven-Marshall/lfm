using System.Text.Json;
using Lfm.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Lfm.Core.Services.Cache;

/// <summary>
/// File-based implementation of cache storage using JSON files for data and metadata.
/// </summary>
public class FileCacheStorage : ICacheStorage
{
    private readonly ICacheDirectoryHelper _cacheDirectoryHelper;
    private readonly ILogger<FileCacheStorage> _logger;

    public FileCacheStorage(
        ICacheDirectoryHelper cacheDirectoryHelper,
        ILogger<FileCacheStorage> logger)
    {
        _cacheDirectoryHelper = cacheDirectoryHelper ?? throw new ArgumentNullException(nameof(cacheDirectoryHelper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> StoreAsync(string key, string jsonData, int expiryMinutes = 10)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
        
        if (jsonData == null)
            throw new ArgumentNullException(nameof(jsonData));

        try
        {
            // Ensure cache directory exists
            if (!_cacheDirectoryHelper.EnsureCacheDirectoryExists())
            {
                _logger.LogError("Failed to ensure cache directory exists");
                return false;
            }

            var dataFilePath = _cacheDirectoryHelper.GetCacheFilePath($"{key}.json");
            var metaFilePath = _cacheDirectoryHelper.GetCacheFilePath($"{key}.meta");

            // Create metadata
            var metadata = new CacheMetadata
            {
                Key = key,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
                SizeBytes = System.Text.Encoding.UTF8.GetByteCount(jsonData)
            };

            // Write data and metadata files
            await File.WriteAllTextAsync(dataFilePath, jsonData);
            var metadataJson = JsonSerializer.Serialize(metadata, GetJsonOptions());
            await File.WriteAllTextAsync(metaFilePath, metadataJson);

            _logger.LogDebug("Cached data for key {Key}, expires at {ExpiresAt}", key, metadata.ExpiresAt);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store cache entry for key {Key}", key);
            return false;
        }
    }

    public async Task<string?> RetrieveAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

        try
        {
            var dataFilePath = _cacheDirectoryHelper.GetCacheFilePath($"{key}.json");
            var metaFilePath = _cacheDirectoryHelper.GetCacheFilePath($"{key}.meta");

            // Check if files exist
            if (!File.Exists(dataFilePath) || !File.Exists(metaFilePath))
            {
                _logger.LogDebug("Cache miss for key {Key} - files not found", key);
                return null;
            }

            // Read and validate metadata
            var metadataJson = await File.ReadAllTextAsync(metaFilePath);
            var metadata = JsonSerializer.Deserialize<CacheMetadata>(metadataJson, GetJsonOptions());

            if (metadata == null)
            {
                _logger.LogWarning("Invalid metadata for cache key {Key}", key);
                return null;
            }

            // Check if expired
            if (DateTime.UtcNow > metadata.ExpiresAt)
            {
                _logger.LogDebug("Cache entry for key {Key} has expired", key);
                // Optionally cleanup expired entry
                _ = Task.Run(() => RemoveAsync(key));
                return null;
            }

            // Read and return data
            var jsonData = await File.ReadAllTextAsync(dataFilePath);
            _logger.LogDebug("Cache hit for key {Key}", key);
            return jsonData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve cache entry for key {Key}", key);
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

        try
        {
            var metaFilePath = _cacheDirectoryHelper.GetCacheFilePath($"{key}.meta");

            if (!File.Exists(metaFilePath))
                return false;

            // Check if expired
            var metadataJson = await File.ReadAllTextAsync(metaFilePath);
            var metadata = JsonSerializer.Deserialize<CacheMetadata>(metadataJson, GetJsonOptions());

            if (metadata == null)
                return false;

            var exists = DateTime.UtcNow <= metadata.ExpiresAt;
            if (!exists)
            {
                // Cleanup expired entry
                _ = Task.Run(() => RemoveAsync(key));
            }

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence of cache key {Key}", key);
            return false;
        }
    }

    public Task<bool> RemoveAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

        try
        {
            var dataFilePath = _cacheDirectoryHelper.GetCacheFilePath($"{key}.json");
            var metaFilePath = _cacheDirectoryHelper.GetCacheFilePath($"{key}.meta");

            var removed = false;

            if (File.Exists(dataFilePath))
            {
                File.Delete(dataFilePath);
                removed = true;
            }

            if (File.Exists(metaFilePath))
            {
                File.Delete(metaFilePath);
                removed = true;
            }

            if (removed)
            {
                _logger.LogDebug("Removed cache entry for key {Key}", key);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove cache entry for key {Key}", key);
            return Task.FromResult(false);
        }
    }

    public async Task<int> CleanupExpiredAsync()
    {
        try
        {
            var cacheDir = _cacheDirectoryHelper.GetCacheDirectory();
            if (!Directory.Exists(cacheDir))
                return 0;

            var metaFiles = Directory.GetFiles(cacheDir, "*.meta");
            var removed = 0;
            var now = DateTime.UtcNow;

            foreach (var metaFile in metaFiles)
            {
                try
                {
                    var metadataJson = await File.ReadAllTextAsync(metaFile);
                    var metadata = JsonSerializer.Deserialize<CacheMetadata>(metadataJson, GetJsonOptions());

                    if (metadata != null && now > metadata.ExpiresAt)
                    {
                        var success = await RemoveAsync(metadata.Key);
                        if (success)
                            removed++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process metadata file {MetaFile} during cleanup", metaFile);
                }
            }

            if (removed > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired cache entries", removed);
            }

            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired cache entries");
            return 0;
        }
    }

    public Task<bool> ClearAllAsync()
    {
        try
        {
            var cacheDir = _cacheDirectoryHelper.GetCacheDirectory();
            if (!Directory.Exists(cacheDir))
                return Task.FromResult(true);

            var files = Directory.GetFiles(cacheDir);
            var removed = 0;

            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    removed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete cache file {File}", file);
                }
            }

            _logger.LogInformation("Cleared {Count} cache files", removed);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear all cache entries");
            return Task.FromResult(false);
        }
    }

    public async Task<CacheStatistics> GetStatisticsAsync()
    {
        var stats = new CacheStatistics
        {
            CacheDirectory = _cacheDirectoryHelper.GetCacheDirectory()
        };

        try
        {
            var cacheDir = _cacheDirectoryHelper.GetCacheDirectory();
            if (!Directory.Exists(cacheDir))
                return stats;

            var metaFiles = Directory.GetFiles(cacheDir, "*.meta");
            var now = DateTime.UtcNow;
            var totalSize = 0L;
            var expiredCount = 0;
            DateTime? oldest = null;
            DateTime? newest = null;

            foreach (var metaFile in metaFiles)
            {
                try
                {
                    var metadataJson = await File.ReadAllTextAsync(metaFile);
                    var metadata = JsonSerializer.Deserialize<CacheMetadata>(metadataJson, GetJsonOptions());

                    if (metadata != null)
                    {
                        totalSize += metadata.SizeBytes;

                        if (now > metadata.ExpiresAt)
                            expiredCount++;

                        if (oldest == null || metadata.CreatedAt < oldest)
                            oldest = metadata.CreatedAt;

                        if (newest == null || metadata.CreatedAt > newest)
                            newest = metadata.CreatedAt;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process metadata file {MetaFile} for statistics", metaFile);
                }
            }

            stats.TotalEntries = metaFiles.Length;
            stats.TotalFiles = metaFiles.Length * 2; // Each entry has .json + .meta files
            stats.ExpiredEntries = expiredCount;
            stats.TotalSizeBytes = totalSize;
            stats.OldestEntry = oldest;
            stats.NewestEntry = newest;

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache statistics");
            return stats;
        }
    }

    public async Task<int> CleanupAsync()
    {
        try
        {
            var cacheDir = _cacheDirectoryHelper.GetCacheDirectory();
            if (!Directory.Exists(cacheDir))
                return 0;

            // Step 1: Remove expired entries first
            var expiredRemoved = await CleanupExpiredAsync();
            _logger.LogDebug("Removed {Count} expired cache entries", expiredRemoved);

            // Step 2: Check if we still need to remove more entries based on size/count limits
            // (This would require config access - for now, just return expired count)
            // TODO: Implement LRU cleanup based on config limits

            return expiredRemoved;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform cache cleanup");
            return 0;
        }
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}

/// <summary>
/// Metadata for cache entries stored in .meta files.
/// </summary>
internal class CacheMetadata
{
    public string Key { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public long SizeBytes { get; set; }
}