namespace Lfm.Core.Services.Cache;

/// <summary>
/// Interface for cache storage operations. Handles storing and retrieving raw JSON strings
/// from the cache with expiry support.
/// </summary>
public interface ICacheStorage
{
    /// <summary>
    /// Stores a JSON string in the cache with the specified key.
    /// </summary>
    /// <param name="key">Unique cache key identifier</param>
    /// <param name="jsonData">Raw JSON string to cache</param>
    /// <param name="expiryMinutes">Expiry time in minutes (default: 10 minutes)</param>
    /// <returns>True if stored successfully, false otherwise</returns>
    Task<bool> StoreAsync(string key, string jsonData, int expiryMinutes = 10);

    /// <summary>
    /// Retrieves a JSON string from the cache if it exists and hasn't expired.
    /// </summary>
    /// <param name="key">Cache key to retrieve</param>
    /// <returns>Cached JSON string if found and valid, null otherwise</returns>
    Task<string?> RetrieveAsync(string key);

    /// <summary>
    /// Checks if a cache entry exists and is still valid (not expired).
    /// </summary>
    /// <param name="key">Cache key to check</param>
    /// <returns>True if cache entry exists and is valid, false otherwise</returns>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Removes a specific cache entry.
    /// </summary>
    /// <param name="key">Cache key to remove</param>
    /// <returns>True if removed successfully or didn't exist, false on error</returns>
    Task<bool> RemoveAsync(string key);

    /// <summary>
    /// Removes all expired cache entries.
    /// </summary>
    /// <returns>Number of entries removed</returns>
    Task<int> CleanupExpiredAsync();

    /// <summary>
    /// Performs intelligent cleanup based on size and LRU policy.
    /// Removes expired entries first, then uses LRU to remove additional entries if needed.
    /// </summary>
    /// <returns>Number of entries removed</returns>
    Task<int> CleanupAsync();

    /// <summary>
    /// Removes all cache entries.
    /// </summary>
    /// <returns>True if cleared successfully, false otherwise</returns>
    Task<bool> ClearAllAsync();

    /// <summary>
    /// Gets cache statistics (total entries, total size, etc.).
    /// </summary>
    /// <returns>Cache statistics object</returns>
    Task<CacheStatistics> GetStatisticsAsync();
}

/// <summary>
/// Cache statistics information.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Total number of cache entries.
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// Total number of cache files (entries * 2 for .json + .meta files).
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Number of expired entries.
    /// </summary>
    public int ExpiredEntries { get; set; }

    /// <summary>
    /// Total cache size in bytes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Cache directory path.
    /// </summary>
    public string CacheDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Oldest cache entry timestamp.
    /// </summary>
    public DateTime? OldestEntry { get; set; }

    /// <summary>
    /// Newest cache entry timestamp.
    /// </summary>
    public DateTime? NewestEntry { get; set; }
}