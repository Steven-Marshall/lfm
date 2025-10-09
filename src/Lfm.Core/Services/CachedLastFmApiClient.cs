using System.Diagnostics;
using System.Text.Json;
using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Models.Results;
using Lfm.Core.Services.Cache;
using Microsoft.Extensions.Logging;

namespace Lfm.Core.Services;

/// <summary>
/// Cached decorator for LastFmApiClient that provides transparent caching of API responses.
/// Implements cache-first strategy: check cache → return if found → fallback to API → cache result.
/// </summary>
public class CachedLastFmApiClient : ILastFmApiClient
{
    private readonly ILastFmApiClient _innerClient;
    private readonly ICacheStorage _cacheStorage;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly ILogger<CachedLastFmApiClient> _logger;
    private readonly IConfigurationManager _configManager;
    private readonly int _defaultCacheExpiryMinutes;
    private DateTime _lastApiCallTime = DateTime.MinValue;
    private readonly SemaphoreSlim _throttleSemaphore = new(1, 1);

    public bool EnableTiming { get; set; } = false;
    public List<TimingInfo> TimingResults { get; } = new();
    public CacheBehavior CacheBehavior { get; set; } = CacheBehavior.Normal;
    public bool DisableThrottling { get; set; } = false;
    public DateTime? WallClockStartTime { get; set; } = null;
    
    public class TimingInfo
    {
        public string Method { get; set; } = "";
        public bool CacheHit { get; set; }
        public long ElapsedMs { get; set; }
        public string Details { get; set; } = "";
    }

    public CachedLastFmApiClient(
        ILastFmApiClient innerClient,
        ICacheStorage cacheStorage,
        ICacheKeyGenerator keyGenerator,
        ILogger<CachedLastFmApiClient> logger,
        IConfigurationManager configManager,
        int defaultCacheExpiryMinutes = 10)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _cacheStorage = cacheStorage ?? throw new ArgumentNullException(nameof(cacheStorage));
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _defaultCacheExpiryMinutes = defaultCacheExpiryMinutes;
    }

    public async Task<TopArtists?> GetTopArtistsAsync(string username, string period = "overall", int limit = 10, int page = 1)
    {
        var cacheKey = _keyGenerator.ForTopArtists(username, period, limit, page);
        
        return await GetWithCacheAsync<TopArtists>(
            cacheKey,
            async () => await _innerClient.GetTopArtistsAsync(username, period, limit, page),
            "GetTopArtists",
            username, period, limit, page);
    }

    public async Task<TopTracks?> GetTopTracksAsync(string username, string period = "overall", int limit = 10, int page = 1)
    {
        var cacheKey = _keyGenerator.ForTopTracks(username, period, limit, page);
        
        return await GetWithCacheAsync<TopTracks>(
            cacheKey,
            async () => await _innerClient.GetTopTracksAsync(username, period, limit, page),
            "GetTopTracks",
            username, period, limit, page);
    }

    public async Task<TopAlbums?> GetTopAlbumsAsync(string username, string period = "overall", int limit = 10, int page = 1)
    {
        var cacheKey = _keyGenerator.ForTopAlbums(username, period, limit, page);
        
        return await GetWithCacheAsync<TopAlbums>(
            cacheKey,
            async () => await _innerClient.GetTopAlbumsAsync(username, period, limit, page),
            "GetTopAlbums",
            username, period, limit, page);
    }

    public async Task<TopTracks?> GetArtistTopTracksAsync(string artist, int limit = 10)
    {
        var cacheKey = _keyGenerator.ForArtistTopTracks(artist, limit);
        
        return await GetWithCacheAsync<TopTracks>(
            cacheKey,
            async () => await _innerClient.GetArtistTopTracksAsync(artist, limit),
            "GetArtistTopTracks",
            artist, "n/a", limit, 1);
    }

    public async Task<TopAlbums?> GetArtistTopAlbumsAsync(string artist, int limit = 10)
    {
        var cacheKey = _keyGenerator.ForArtistTopAlbums(artist, limit);
        
        return await GetWithCacheAsync<TopAlbums>(
            cacheKey,
            async () => await _innerClient.GetArtistTopAlbumsAsync(artist, limit),
            "GetArtistTopAlbums",
            artist, "n/a", limit, 1);
    }

    public async Task<SimilarArtists?> GetSimilarArtistsAsync(string artist, int limit = 50)
    {
        var cacheKey = _keyGenerator.ForSimilarArtists(artist, limit);
        
        return await GetWithCacheAsync<SimilarArtists>(
            cacheKey,
            async () => await _innerClient.GetSimilarArtistsAsync(artist, limit),
            "GetSimilarArtists",
            artist, "n/a", limit, 1);
    }

    public async Task<TopTags?> GetArtistTopTagsAsync(string artist, bool autocorrect = true)
    {
        var cacheKey = _keyGenerator.ForArtistTopTags(artist, autocorrect);

        return await GetWithCacheAsync<TopTags>(
            cacheKey,
            async () => await _innerClient.GetArtistTopTagsAsync(artist, autocorrect),
            "GetArtistTopTags",
            artist, autocorrect.ToString(), 1, 1);
    }

    /// <summary>
    /// Generic cache-aware method that handles different cache behaviors and cleanup.
    /// </summary>
    /// <typeparam name="T">The response type to deserialize</typeparam>
    /// <param name="cacheKey">Cache key for this request</param>
    /// <param name="apiCall">Function to call the actual API if cache miss</param>
    /// <param name="methodName">Method name for logging</param>
    /// <param name="logParams">Parameters for logging (user, period, limit, page)</param>
    /// <returns>The response object or null if not found</returns>
    private async Task<T?> GetWithCacheAsync<T>(
        string cacheKey,
        Func<Task<T?>> apiCall,
        string methodName,
        params object[] logParams) where T : class
    {
        var stopwatch = EnableTiming ? Stopwatch.StartNew() : null;
        
        try
        {
            // Check if cleanup is needed (non-blocking)
            _ = Task.Run(async () => await TryCleanupIfNeededAsync());
            
            var config = await _configManager.LoadAsync();
            var effectiveBehavior = config.CacheEnabled ? CacheBehavior : CacheBehavior.NoCache;
            
            // Handle NoCache behavior - bypass cache entirely
            if (effectiveBehavior == CacheBehavior.NoCache)
            {
                _logger.LogDebug("Cache disabled for {Method}, calling API directly", methodName);

                // Apply throttling for no-cache API calls
                await ApplyApiThrottlingAsync(config.ApiThrottleMs);

                var directResult = await apiCall();
                RecordApiCallComplete();

                if (EnableTiming && stopwatch != null)
                {
                    stopwatch.Stop();
                    TimingResults.Add(new TimingInfo
                    {
                        Method = methodName,
                        CacheHit = false,
                        ElapsedMs = stopwatch.ElapsedMilliseconds,
                        Details = $"no-cache mode, params: {string.Join(", ", logParams)}"
                    });
                }
                
                return directResult;
            }
            
            // Handle ForceApi behavior - ignore cache, but store result
            if (effectiveBehavior == CacheBehavior.ForceApi)
            {
                _logger.LogDebug("Force API mode for {Method}, bypassing cache", methodName);

                // Apply throttling for forced API calls
                var forceThrottleWatch = EnableTiming ? Stopwatch.StartNew() : null;
                await ApplyApiThrottlingAsync(config.ApiThrottleMs);
                forceThrottleWatch?.Stop();
                var forceThrottleMs = forceThrottleWatch?.ElapsedMilliseconds ?? 0;

                // Make the actual API call
                var forceApiResult = await apiCall();
                RecordApiCallComplete();

                // Get detailed timing from underlying LastFmApiClient if available
                long forceHttpMs = 0, forceJsonReadMs = 0, forceJsonParseMs = 0;
                if (_innerClient is LastFmApiClient forceConcreteClient)
                {
                    forceHttpMs = forceConcreteClient.LastHttpMs;
                    forceJsonReadMs = forceConcreteClient.LastJsonReadMs;
                    forceJsonParseMs = forceConcreteClient.LastJsonParseMs;
                }

                // Cache the result
                var forceApiCacheWatch = EnableTiming ? Stopwatch.StartNew() : null;
                if (forceApiResult != null && HasData(forceApiResult))
                {
                    await TryCacheResultAsync(cacheKey, forceApiResult, methodName, config.CacheExpiryMinutes);
                }
                else if (forceApiResult != null && !HasData(forceApiResult))
                {
                    _logger.LogDebug("Not caching empty response for {Method} (force-api mode)", methodName);
                }
                forceApiCacheWatch?.Stop();
                var forceApiCacheMs = forceApiCacheWatch?.ElapsedMilliseconds ?? 0;

                if (EnableTiming && stopwatch != null)
                {
                    stopwatch.Stop();
                    var breakdown = $"throttle:{forceThrottleMs}ms, http:{forceHttpMs}ms, json-read:{forceJsonReadMs}ms, json-parse:{forceJsonParseMs}ms, cache:{forceApiCacheMs}ms";
                    TimingResults.Add(new TimingInfo
                    {
                        Method = methodName,
                        CacheHit = false,
                        ElapsedMs = stopwatch.ElapsedMilliseconds,
                        Details = $"force-api mode, params: {string.Join(", ", logParams)} [{breakdown}]"
                    });
                }

                return forceApiResult;
            }
            
            // Try cache first (Normal and ForceCache behaviors)
            var cachedJson = await _cacheStorage.RetrieveAsync(cacheKey);
            if (cachedJson != null)
            {
                try
                {
                    var cachedResult = JsonSerializer.Deserialize<T>(cachedJson, GetJsonOptions());
                    if (cachedResult != null)
                    {
                        var shouldUseCache = effectiveBehavior == CacheBehavior.ForceCache || 
                                           await IsCacheValidAsync(cacheKey, config.CacheExpiryMinutes);
                        
                        if (shouldUseCache)
                        {
                            _logger.LogDebug("Cache hit for {Method} with params {Params}", methodName, string.Join(", ", logParams));
                            
                            if (EnableTiming && stopwatch != null)
                            {
                                stopwatch.Stop();
                                TimingResults.Add(new TimingInfo
                                {
                                    Method = methodName,
                                    CacheHit = true,
                                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                                    Details = $"{effectiveBehavior.ToString().ToLower()}, params: {string.Join(", ", logParams)}"
                                });
                            }
                            
                            return cachedResult;
                        }
                        else
                        {
                            _logger.LogDebug("Cache expired for {Method}, calling API", methodName);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize cached data for {Method}, falling back to API", methodName);
                }
            }

            // Cache miss or expired - call API with throttling
            _logger.LogDebug("Cache miss for {Method} with params {Params}, calling API", methodName, string.Join(", ", logParams));

            // Apply throttling only for actual API calls (not cache hits)
            var throttleWatch = EnableTiming ? Stopwatch.StartNew() : null;
            await ApplyApiThrottlingAsync(config.ApiThrottleMs);
            throttleWatch?.Stop();
            var throttleMs = throttleWatch?.ElapsedMilliseconds ?? 0;

            // Make the actual API call (includes HTTP + JSON read + JSON parse)
            var apiResult = await apiCall();

            // Record completion time for next throttle calculation
            RecordApiCallComplete();

            // Get detailed timing from underlying LastFmApiClient if available
            long httpMs = 0, jsonReadMs = 0, jsonParseMs = 0;
            if (_innerClient is LastFmApiClient concreteClient)
            {
                httpMs = concreteClient.LastHttpMs;
                jsonReadMs = concreteClient.LastJsonReadMs;
                jsonParseMs = concreteClient.LastJsonParseMs;
            }

            // Cache the result
            var cacheWriteWatch = EnableTiming ? Stopwatch.StartNew() : null;
            if (apiResult != null && HasData(apiResult))
            {
                await TryCacheResultAsync(cacheKey, apiResult, methodName, config.CacheExpiryMinutes);
            }
            else if (apiResult != null && !HasData(apiResult))
            {
                _logger.LogDebug("Not caching empty response for {Method}", methodName);
            }
            cacheWriteWatch?.Stop();
            var cacheWriteMs = cacheWriteWatch?.ElapsedMilliseconds ?? 0;

            if (EnableTiming && stopwatch != null)
            {
                stopwatch.Stop();
                var breakdown = $"throttle:{throttleMs}ms, http:{httpMs}ms, json-read:{jsonReadMs}ms, json-parse:{jsonParseMs}ms, cache:{cacheWriteMs}ms";
                TimingResults.Add(new TimingInfo
                {
                    Method = methodName,
                    CacheHit = false,
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                    Details = $"{effectiveBehavior.ToString().ToLower()}, params: {string.Join(", ", logParams)} [{breakdown}]"
                });
            }

            return apiResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in cached API call for {Method}", methodName);
            
            // On any cache-related error, fallback to direct API call
            try
            {
                _logger.LogInformation("Falling back to direct API call for {Method}", methodName);

                // Apply throttling for fallback API calls
                var config2 = await _configManager.LoadAsync();
                await ApplyApiThrottlingAsync(config2.ApiThrottleMs);

                var fallbackResult = await apiCall();
                RecordApiCallComplete();

                if (EnableTiming && stopwatch != null)
                {
                    stopwatch.Stop();
                    TimingResults.Add(new TimingInfo
                    {
                        Method = methodName,
                        CacheHit = false,
                        ElapsedMs = stopwatch.ElapsedMilliseconds,
                        Details = $"fallback API call, params: {string.Join(", ", logParams)}"
                    });
                }
                
                return fallbackResult;
            }
            catch (Exception apiEx)
            {
                _logger.LogError(apiEx, "Both cached and direct API calls failed for {Method}", methodName);
                
                if (EnableTiming && stopwatch != null)
                {
                    stopwatch.Stop();
                    TimingResults.Add(new TimingInfo
                    {
                        Method = methodName,
                        CacheHit = false,
                        ElapsedMs = stopwatch.ElapsedMilliseconds,
                        Details = $"FAILED - params: {string.Join(", ", logParams)}"
                    });
                }
                
                return null;
            }
        }
    }

    public async Task<RecentTracks?> GetRecentTracksAsync(string username, DateTime from, DateTime to, int limit = 200, int page = 1)
    {
        var dateRange = DateRangeParser.FormatDateRange(from, to);
        var cacheKey = _keyGenerator.ForRecentTracks(username, dateRange, limit, page);
        
        return await GetWithCacheAsync<RecentTracks>(
            cacheKey,
            async () => await _innerClient.GetRecentTracksAsync(username, from, to, limit, page),
            "GetRecentTracks",
            username, dateRange, limit, page);
    }

    public async Task<TopArtists?> GetTopArtistsForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        var dateRange = DateRangeParser.FormatDateRange(from, to);
        var cacheKey = _keyGenerator.ForTopArtistsDateRange(username, dateRange, limit);
        
        return await GetWithCacheAsync<TopArtists>(
            cacheKey,
            async () => await _innerClient.GetTopArtistsForDateRangeAsync(username, from, to, limit),
            "GetTopArtistsForDateRange",
            username, dateRange, limit, 1);
    }

    public async Task<TopTracks?> GetTopTracksForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        var dateRange = DateRangeParser.FormatDateRange(from, to);
        var cacheKey = _keyGenerator.ForTopTracksDateRange(username, dateRange, limit);
        
        return await GetWithCacheAsync<TopTracks>(
            cacheKey,
            async () => await _innerClient.GetTopTracksForDateRangeAsync(username, from, to, limit),
            "GetTopTracksForDateRange",
            username, dateRange, limit, 1);
    }

    public async Task<TopAlbums?> GetTopAlbumsForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        var dateRange = DateRangeParser.FormatDateRange(from, to);
        var cacheKey = _keyGenerator.ForTopAlbumsDateRange(username, dateRange, limit);
        
        return await GetWithCacheAsync<TopAlbums>(
            cacheKey,
            async () => await _innerClient.GetTopAlbumsForDateRangeAsync(username, from, to, limit),
            "GetTopAlbumsForDateRange",
            username, dateRange, limit, 1);
    }

    /// <summary>
    /// Checks if a response object contains meaningful data that should be cached.
    /// Empty responses (e.g., empty artist/track/album lists) should not be cached.
    /// </summary>
    private bool HasData<T>(T result) where T : class
    {
        return result switch
        {
            TopArtists artists => artists.Artists?.Any() == true,
            TopTracks tracks => tracks.Tracks?.Any() == true,
            TopAlbums albums => albums.Albums?.Any() == true,
            RecentTracks recentTracks => recentTracks.Tracks?.Any() == true,
            SimilarArtists similarArtists => similarArtists.Artists?.Any() == true,
            TopTags tags => tags.Tags?.Any() == true,
            ArtistLookupInfo artistLookup => !string.IsNullOrEmpty(artistLookup.Artist?.Name),
            TrackLookupInfo trackLookup => !string.IsNullOrEmpty(trackLookup.Track?.Name),
            AlbumLookupInfo albumLookup => !string.IsNullOrEmpty(albumLookup.Album?.Name),
            _ => true // For unknown types, cache them (conservative approach)
        };
    }

    /// <summary>
    /// Attempts to cache the API result, handling errors gracefully
    /// </summary>
    private async Task TryCacheResultAsync<T>(string cacheKey, T result, string methodName, int expiryMinutes)
    {
        try
        {
            var jsonToCache = JsonSerializer.Serialize(result, GetJsonOptions());
            var cached = await _cacheStorage.StoreAsync(cacheKey, jsonToCache, expiryMinutes);
            
            if (cached)
            {
                _logger.LogDebug("Cached API response for {Method}", methodName);
            }
            else
            {
                _logger.LogWarning("Failed to cache API response for {Method}", methodName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching API response for {Method}", methodName);
        }
    }
    
    /// <summary>
    /// Checks if cached data is still valid based on expiry time
    /// </summary>
    private async Task<bool> IsCacheValidAsync(string cacheKey, int expiryMinutes)
    {
        try
        {
            return await _cacheStorage.ExistsAsync(cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking cache validity for key {Key}", cacheKey);
            return false;
        }
    }

    /// <summary>
    /// Applies throttling only for actual API calls, not cache hits.
    /// This ensures we respect rate limits without slowing down cached responses.
    /// NOTE: This only enforces the delay. Caller must call RecordApiCallComplete() after the HTTP request.
    /// Can be disabled via DisableThrottling property for parallel batch operations.
    /// </summary>
    private async Task ApplyApiThrottlingAsync(int throttleMs)
    {
        if (throttleMs <= 0 || DisableThrottling)
        {
            return; // No throttling configured or disabled for parallel batch mode
        }

        await _throttleSemaphore.WaitAsync();
        try
        {
            var timeSinceLastCall = DateTime.UtcNow - _lastApiCallTime;
            var requiredDelay = TimeSpan.FromMilliseconds(throttleMs);

            if (timeSinceLastCall < requiredDelay)
            {
                var delayNeeded = requiredDelay - timeSinceLastCall;
                _logger.LogDebug("Throttling API call for {DelayMs}ms", delayNeeded.TotalMilliseconds);
                await Task.Delay(delayNeeded);
            }
        }
        finally
        {
            _throttleSemaphore.Release();
        }
    }

    /// <summary>
    /// Records that an API call has completed. Must be called after HTTP request finishes.
    /// </summary>
    private void RecordApiCallComplete()
    {
        _lastApiCallTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Performs cache cleanup if needed based on configuration
    /// </summary>
    private async Task TryCleanupIfNeededAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            
            // Check if cleanup is needed
            var timeSinceLastCleanup = DateTime.Now - config.LastCacheCleanup;
            var cleanupInterval = TimeSpan.FromHours(config.CleanupIntervalHours);
            
            if (timeSinceLastCleanup < cleanupInterval)
            {
                return; // No cleanup needed yet
            }
            
            _logger.LogDebug("Starting cache cleanup - last cleanup was {Hours:F1} hours ago", 
                timeSinceLastCleanup.TotalHours);
            
            // Get cache statistics
            var stats = await _cacheStorage.GetStatisticsAsync();
            var needsCleanup = stats.TotalSizeBytes > config.MaxCacheSizeMB * 1024 * 1024 * 0.8 ||
                             stats.TotalFiles > config.MaxCacheFiles * 0.8;
            
            if (needsCleanup)
            {
                await _cacheStorage.CleanupAsync();
                _logger.LogInformation("Cache cleanup completed");
            }
            
            // Update last cleanup time
            config.LastCacheCleanup = DateTime.Now;
            await _configManager.SaveAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache cleanup failed, continuing normally");
        }
    }

    // New Result-based methods for better error handling
    public async Task<Result<TopArtists>> GetTopArtistsWithResultAsync(string username, string period = "overall", int limit = 10, int page = 1)
    {
        var cacheKey = _keyGenerator.ForTopArtists(username, period, limit, page);
        
        return await GetWithCacheResultAsync<TopArtists>(
            cacheKey,
            async () => await _innerClient.GetTopArtistsWithResultAsync(username, period, limit, page),
            "GetTopArtists",
            username, period, limit, page);
    }

    public async Task<Result<TopTracks>> GetTopTracksWithResultAsync(string username, string period = "overall", int limit = 10, int page = 1)
    {
        var cacheKey = _keyGenerator.ForTopTracks(username, period, limit, page);
        
        return await GetWithCacheResultAsync<TopTracks>(
            cacheKey,
            async () => await _innerClient.GetTopTracksWithResultAsync(username, period, limit, page),
            "GetTopTracks",
            username, period, limit, page);
    }

    public async Task<Result<TopAlbums>> GetTopAlbumsWithResultAsync(string username, string period = "overall", int limit = 10, int page = 1)
    {
        var cacheKey = _keyGenerator.ForTopAlbums(username, period, limit, page);
        
        return await GetWithCacheResultAsync<TopAlbums>(
            cacheKey,
            async () => await _innerClient.GetTopAlbumsWithResultAsync(username, period, limit, page),
            "GetTopAlbums",
            username, period, limit, page);
    }

    public async Task<Result<TopTracks>> GetArtistTopTracksWithResultAsync(string artist, int limit = 10)
    {
        var cacheKey = _keyGenerator.ForArtistTopTracks(artist, limit);
        
        return await GetWithCacheResultAsync<TopTracks>(
            cacheKey,
            async () => await _innerClient.GetArtistTopTracksWithResultAsync(artist, limit),
            "GetArtistTopTracks",
            artist, "n/a", limit, 1);
    }

    public async Task<Result<TopAlbums>> GetArtistTopAlbumsWithResultAsync(string artist, int limit = 10)
    {
        var cacheKey = _keyGenerator.ForArtistTopAlbums(artist, limit);
        
        return await GetWithCacheResultAsync<TopAlbums>(
            cacheKey,
            async () => await _innerClient.GetArtistTopAlbumsWithResultAsync(artist, limit),
            "GetArtistTopAlbums",
            artist, "n/a", limit, 1);
    }

    public async Task<Result<SimilarArtists>> GetSimilarArtistsWithResultAsync(string artist, int limit = 50)
    {
        var cacheKey = _keyGenerator.ForSimilarArtists(artist, limit);
        
        return await GetWithCacheResultAsync<SimilarArtists>(
            cacheKey,
            async () => await _innerClient.GetSimilarArtistsWithResultAsync(artist, limit),
            "GetSimilarArtists",
            artist, "n/a", limit, 1);
    }

    public async Task<Result<TopTags>> GetArtistTopTagsWithResultAsync(string artist, bool autocorrect = true)
    {
        var cacheKey = _keyGenerator.ForArtistTopTags(artist, autocorrect);

        return await GetWithCacheResultAsync<TopTags>(
            cacheKey,
            async () => await _innerClient.GetArtistTopTagsWithResultAsync(artist, autocorrect),
            "GetArtistTopTags",
            artist, autocorrect.ToString(), 1, 1);
    }

    public async Task<Result<RecentTracks>> GetRecentTracksWithResultAsync(string username, DateTime from, DateTime to, int limit = 200, int page = 1)
    {
        var cacheKey = _keyGenerator.ForRecentTracks(username, DateRangeParser.FormatDateRange(from, to), limit, page);
        
        return await GetWithCacheResultAsync<RecentTracks>(
            cacheKey,
            async () => await _innerClient.GetRecentTracksWithResultAsync(username, from, to, limit, page),
            "GetRecentTracks",
            username, DateRangeParser.FormatDateRange(from, to), limit, page);
    }

    public async Task<Result<TopArtists>> GetTopArtistsForDateRangeWithResultAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        var cacheKey = _keyGenerator.ForTopArtistsDateRange(username, DateRangeParser.FormatDateRange(from, to), limit);
        
        return await GetWithCacheResultAsync<TopArtists>(
            cacheKey,
            async () => await _innerClient.GetTopArtistsForDateRangeWithResultAsync(username, from, to, limit),
            "GetTopArtistsDateRange",
            username, DateRangeParser.FormatDateRange(from, to), limit, 1);
    }

    public async Task<Result<TopTracks>> GetTopTracksForDateRangeWithResultAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        var cacheKey = _keyGenerator.ForTopTracksDateRange(username, DateRangeParser.FormatDateRange(from, to), limit);
        
        return await GetWithCacheResultAsync<TopTracks>(
            cacheKey,
            async () => await _innerClient.GetTopTracksForDateRangeWithResultAsync(username, from, to, limit),
            "GetTopTracksDateRange",
            username, DateRangeParser.FormatDateRange(from, to), limit, 1);
    }

    public async Task<Result<TopAlbums>> GetTopAlbumsForDateRangeWithResultAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        var cacheKey = _keyGenerator.ForTopAlbumsDateRange(username, DateRangeParser.FormatDateRange(from, to), limit);
        
        return await GetWithCacheResultAsync<TopAlbums>(
            cacheKey,
            async () => await _innerClient.GetTopAlbumsForDateRangeWithResultAsync(username, from, to, limit),
            "GetTopAlbumsDateRange",
            username, DateRangeParser.FormatDateRange(from, to), limit, 1);
    }

    // Lookup methods with caching
    public async Task<ArtistLookupInfo?> GetArtistInfoAsync(string artist, string username)
    {
        var cacheKey = _keyGenerator.ForArtistInfo(artist, username);

        return await GetWithCacheAsync<ArtistLookupInfo>(
            cacheKey,
            async () => await _innerClient.GetArtistInfoAsync(artist, username),
            "GetArtistInfo",
            artist, username);
    }

    public async Task<TrackLookupInfo?> GetTrackInfoAsync(string artist, string track, string username)
    {
        var cacheKey = _keyGenerator.ForTrackInfo(artist, track, username);

        return await GetWithCacheAsync<TrackLookupInfo>(
            cacheKey,
            async () => await _innerClient.GetTrackInfoAsync(artist, track, username),
            "GetTrackInfo",
            artist, track, username);
    }

    public async Task<AlbumLookupInfo?> GetAlbumInfoAsync(string artist, string album, string username)
    {
        var cacheKey = _keyGenerator.ForAlbumInfo(artist, album, username);

        return await GetWithCacheAsync<AlbumLookupInfo>(
            cacheKey,
            async () => await _innerClient.GetAlbumInfoAsync(artist, album, username),
            "GetAlbumInfo",
            artist, album, username);
    }

    public async Task<Result<ArtistLookupInfo>> GetArtistInfoWithResultAsync(string artist, string username)
    {
        var cacheKey = _keyGenerator.ForArtistInfo(artist, username);

        return await GetWithCacheResultAsync<ArtistLookupInfo>(
            cacheKey,
            async () => await _innerClient.GetArtistInfoWithResultAsync(artist, username),
            "GetArtistInfo",
            artist, username);
    }

    public async Task<Result<TrackLookupInfo>> GetTrackInfoWithResultAsync(string artist, string track, string username)
    {
        var cacheKey = _keyGenerator.ForTrackInfo(artist, track, username);

        return await GetWithCacheResultAsync<TrackLookupInfo>(
            cacheKey,
            async () => await _innerClient.GetTrackInfoWithResultAsync(artist, track, username),
            "GetTrackInfo",
            artist, track, username);
    }

    public async Task<Result<AlbumLookupInfo>> GetAlbumInfoWithResultAsync(string artist, string album, string username)
    {
        var cacheKey = _keyGenerator.ForAlbumInfo(artist, album, username);

        return await GetWithCacheResultAsync<AlbumLookupInfo>(
            cacheKey,
            async () => await _innerClient.GetAlbumInfoWithResultAsync(artist, album, username),
            "GetAlbumInfo",
            artist, album, username);
    }

    private async Task<Result<T>> GetWithCacheResultAsync<T>(
        string cacheKey,
        Func<Task<Result<T>>> apiCall,
        string methodName,
        params object[] logParams) where T : class
    {
        // Wrap the Result-based API call to work with the existing cache infrastructure
        var nullableResult = await GetWithCacheAsync<T>(
            cacheKey,
            async () => 
            {
                var result = await apiCall();
                return result.Success ? result.Data : null;
            },
            methodName,
            logParams);

        if (nullableResult != null)
        {
            return Result<T>.Ok(nullableResult);
        }
        else
        {
            // If GetWithCacheAsync returned null, we need to call the API directly to get the error details
            var directResult = await apiCall();
            return directResult.Success 
                ? Result<T>.Fail(new ErrorResult(ErrorType.DataError, "Unexpected null result from API", ""))
                : directResult;
        }
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}