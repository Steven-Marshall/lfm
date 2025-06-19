namespace Lfm.Core.Configuration;

/// <summary>
/// Defines how the cache should behave for API requests
/// </summary>
public enum CacheBehavior
{
    /// <summary>
    /// Normal behavior: Use cache with expiry checking, fall back to API on cache miss
    /// </summary>
    Normal,
    
    /// <summary>
    /// Force cache usage: Use cached data regardless of expiry timestamp, fall back to API if not cached
    /// </summary>
    ForceCache,
    
    /// <summary>
    /// Force API usage: Always call API and cache the result, ignore existing cache
    /// </summary>
    ForceApi,
    
    /// <summary>
    /// Disable caching: Call API directly without reading or writing cache
    /// </summary>
    NoCache
}