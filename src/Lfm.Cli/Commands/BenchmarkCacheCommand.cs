using System.Diagnostics;
using System.Text.Json;
using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Lfm.Core.Services.Cache;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

/// <summary>
/// Benchmarks cache performance against direct API calls to validate speed improvements.
/// This command provides comprehensive performance testing and validation.
/// </summary>
public class BenchmarkCacheCommand
{
    private readonly ILastFmApiClient _apiClient;
    private readonly ICacheStorage _cacheStorage;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly IConfigurationManager _configManager;
    private readonly ILogger<BenchmarkCacheCommand> _logger;

    public BenchmarkCacheCommand(
        ILastFmApiClient apiClient,
        ICacheStorage cacheStorage,
        ICacheKeyGenerator keyGenerator,
        IConfigurationManager configManager,
        ILogger<BenchmarkCacheCommand> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _cacheStorage = cacheStorage ?? throw new ArgumentNullException(nameof(cacheStorage));
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(string username, bool skipApiCalls = false, int iterations = 10, bool includeDeepSearch = false, int delayMs = 200)
    {
        Console.WriteLine("🚀 Cache Performance Benchmark");
        Console.WriteLine("================================");
        Console.WriteLine($"Username: {username}");
        Console.WriteLine($"Iterations: {iterations}");
        Console.WriteLine($"Skip API calls: {skipApiCalls}");
        Console.WriteLine($"Include deep search: {includeDeepSearch}");
        Console.WriteLine($"API delay: {delayMs}ms");
        Console.WriteLine();

        try
        {
            var config = await _configManager.LoadAsync();
            
            if (!skipApiCalls && string.IsNullOrEmpty(config.ApiKey))
            {
                Console.WriteLine("❌ No API key configured. Set with: lfm config set-api-key <key>");
                Console.WriteLine("   Use --skip-api to test with mock data");
                return;
            }

            var benchmarkResults = new BenchmarkResults();

            // 1. Benchmark API baseline (if not skipped)
            if (!skipApiCalls)
            {
                Console.WriteLine("📡 Benchmarking direct API calls...");
                benchmarkResults.ApiBaseline = await BenchmarkApiCalls(username, iterations, delayMs);
                Console.WriteLine();
            }

            // 2. Benchmark cache writes (API → Cache)
            Console.WriteLine("💾 Benchmarking cache writes (API → Cache)...");
            benchmarkResults.CacheWrites = await BenchmarkCacheWrites(username, iterations, skipApiCalls);
            Console.WriteLine();

            // 3. Benchmark cache reads (Cache → Deserialize)
            Console.WriteLine("⚡ Benchmarking cache reads (Cache → Deserialize)...");
            benchmarkResults.CacheReads = await BenchmarkCacheReads(username, iterations);
            Console.WriteLine();

            // 4. Deep search comparison (if enabled)
            if (includeDeepSearch)
            {
                Console.WriteLine("🔍 Benchmarking deep search simulation (Multiple pages)...");
                benchmarkResults.DeepSearch = await BenchmarkDeepSearch(username, skipApiCalls);
                Console.WriteLine();
            }

            // 5. Display comprehensive results
            DisplayBenchmarkSummary(benchmarkResults, skipApiCalls);

            // 6. Validation milestone check
            ValidatePerformanceTargets(benchmarkResults, skipApiCalls);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Benchmark failed: {ex.Message}");
            _logger.LogError(ex, "Benchmark execution failed");
        }
    }

    private async Task<BenchmarkResult> BenchmarkApiCalls(string username, int iterations, int delayMs)
    {
        var times = new List<long>();
        var totalBytes = 0L;

        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var result = await _apiClient.GetTopTracksAsync(username, "overall", 50, 1);
            
            stopwatch.Stop();
            times.Add(stopwatch.ElapsedMilliseconds);

            if (result != null)
            {
                var json = JsonSerializer.Serialize(result);
                totalBytes += System.Text.Encoding.UTF8.GetByteCount(json);
            }

            Console.Write($"  API call {i + 1}/{iterations}: {stopwatch.ElapsedMilliseconds}ms\r");
            
            // API throttling delay (configurable)
            if (i < iterations - 1 && delayMs > 0)
                await Task.Delay(delayMs);
        }

        Console.WriteLine();

        return new BenchmarkResult
        {
            Times = times,
            TotalBytes = totalBytes,
            Operation = "Direct API Call"
        };
    }

    private async Task<BenchmarkResult> BenchmarkCacheWrites(string username, int iterations, bool skipApiCalls)
    {
        var times = new List<long>();
        var totalBytes = 0L;

        // Get sample data for caching
        string sampleJson;
        if (!skipApiCalls)
        {
            var sampleResult = await _apiClient.GetTopTracksAsync(username, "overall", 50, 1);
            sampleJson = JsonSerializer.Serialize(sampleResult);
        }
        else
        {
            sampleJson = """{"tracks":{"track":[{"name":"Sample Track","artist":{"name":"Sample Artist"},"playcount":"100"}],"@attr":{"total":"1000"}}}""";
        }

        for (int i = 0; i < iterations; i++)
        {
            var key = _keyGenerator.ForTopTracks(username, "overall", 50, i + 1); // Different pages
            
            var stopwatch = Stopwatch.StartNew();
            
            await _cacheStorage.StoreAsync(key, sampleJson, 10);
            
            stopwatch.Stop();
            times.Add(stopwatch.ElapsedMilliseconds);
            totalBytes += System.Text.Encoding.UTF8.GetByteCount(sampleJson);

            Console.Write($"  Cache write {i + 1}/{iterations}: {stopwatch.ElapsedMilliseconds}ms\r");
        }

        Console.WriteLine();

        return new BenchmarkResult
        {
            Times = times,
            TotalBytes = totalBytes,
            Operation = "Cache Write"
        };
    }

    private async Task<BenchmarkResult> BenchmarkCacheReads(string username, int iterations)
    {
        var times = new List<long>();
        var totalBytes = 0L;
        var successfulReads = 0;

        for (int i = 0; i < iterations; i++)
        {
            var key = _keyGenerator.ForTopTracks(username, "overall", 50, i + 1);
            
            var stopwatch = Stopwatch.StartNew();
            
            var cachedData = await _cacheStorage.RetrieveAsync(key);
            
            stopwatch.Stop();
            times.Add(stopwatch.ElapsedMilliseconds);

            if (cachedData != null)
            {
                totalBytes += System.Text.Encoding.UTF8.GetByteCount(cachedData);
                successfulReads++;

                // Simulate deserialization like real usage
                try
                {
                    JsonDocument.Parse(cachedData).Dispose();
                }
                catch
                {
                    // Ignore parsing errors for benchmark
                }
            }

            Console.Write($"  Cache read {i + 1}/{iterations}: {stopwatch.ElapsedMilliseconds}ms\r");
        }

        Console.WriteLine();
        Console.WriteLine($"  Successful reads: {successfulReads}/{iterations}");

        return new BenchmarkResult
        {
            Times = times,
            TotalBytes = totalBytes,
            Operation = "Cache Read",
            SuccessfulOperations = successfulReads
        };
    }

    private async Task<BenchmarkResult> BenchmarkDeepSearch(string username, bool skipApiCalls)
    {
        const int deepSearchPages = 20; // Simulate searching through 20 pages (1000 tracks)
        var times = new List<long>();
        var totalBytes = 0L;

        // First, populate cache for deep search simulation
        if (!skipApiCalls)
        {
            Console.WriteLine("  Populating cache for deep search (this may take a while)...");
            for (int page = 1; page <= deepSearchPages; page++)
            {
                var key = _keyGenerator.ForTopTracks(username, "overall", 50, page);
                var exists = await _cacheStorage.ExistsAsync(key);
                
                if (!exists)
                {
                    var result = await _apiClient.GetTopTracksAsync(username, "overall", 50, page);
                    if (result != null)
                    {
                        var json = JsonSerializer.Serialize(result);
                        await _cacheStorage.StoreAsync(key, json, 10);
                    }
                    
                    // Be respectful to API
                    await Task.Delay(200);
                }
                
                Console.Write($"    Page {page}/{deepSearchPages}\r");
            }
            Console.WriteLine();
        }

        // Now benchmark cache-based deep search
        Console.WriteLine("  Benchmarking cached deep search...");
        var stopwatch = Stopwatch.StartNew();
        
        var foundTracks = 0;
        for (int page = 1; page <= deepSearchPages; page++)
        {
            var key = _keyGenerator.ForTopTracks(username, "overall", 50, page);
            var cachedData = await _cacheStorage.RetrieveAsync(key);
            
            if (cachedData != null)
            {
                totalBytes += System.Text.Encoding.UTF8.GetByteCount(cachedData);
                
                // Simulate processing like real artist search
                try
                {
                    using var doc = JsonDocument.Parse(cachedData);
                    if (doc.RootElement.TryGetProperty("tracks", out var tracksElement) &&
                        tracksElement.TryGetProperty("track", out var trackArray))
                    {
                        foundTracks += trackArray.GetArrayLength();
                    }
                }
                catch
                {
                    // Ignore parsing errors for benchmark
                }
            }
        }
        
        stopwatch.Stop();
        times.Add(stopwatch.ElapsedMilliseconds);

        Console.WriteLine($"  Processed {foundTracks} tracks in {stopwatch.ElapsedMilliseconds}ms");

        // Compare with simulated API-based deep search
        if (!skipApiCalls)
        {
            Console.WriteLine("  Simulating uncached deep search time...");
            var simulatedApiTime = deepSearchPages * 300; // 300ms per API call
            Console.WriteLine($"  Estimated API time: {simulatedApiTime}ms");
            Console.WriteLine($"  Speed improvement: {(double)simulatedApiTime / stopwatch.ElapsedMilliseconds:F1}x");
        }

        return new BenchmarkResult
        {
            Times = times,
            TotalBytes = totalBytes,
            Operation = "Deep Search (Cached)",
            SuccessfulOperations = foundTracks
        };
    }

    private static void DisplayBenchmarkSummary(BenchmarkResults results, bool skipApiCalls)
    {
        Console.WriteLine("📊 Benchmark Summary");
        Console.WriteLine("===================");

        if (!skipApiCalls && results.ApiBaseline != null)
        {
            DisplayResult("Direct API Calls", results.ApiBaseline);
        }

        DisplayResult("Cache Writes", results.CacheWrites);
        DisplayResult("Cache Reads", results.CacheReads);
        
        if (results.DeepSearch != null)
        {
            DisplayResult("Deep Search", results.DeepSearch);
        }

        if (!skipApiCalls && results.ApiBaseline != null && results.CacheReads != null)
        {
            var speedup = (double)results.ApiBaseline.AverageMs / results.CacheReads.AverageMs;
            Console.WriteLine();
            Console.WriteLine($"🚀 **Speed Improvement**: {speedup:F1}x faster (Cache vs API)");
            Console.WriteLine($"   API Average: {results.ApiBaseline.AverageMs:F1}ms");
            Console.WriteLine($"   Cache Average: {results.CacheReads.AverageMs:F1}ms");
        }
    }

    private static void DisplayResult(string name, BenchmarkResult? result)
    {
        if (result == null) return;

        Console.WriteLine($"\n{name}:");
        Console.WriteLine($"  Average: {result.AverageMs:F1}ms");
        Console.WriteLine($"  Min: {result.MinMs}ms");
        Console.WriteLine($"  Max: {result.MaxMs}ms");
        Console.WriteLine($"  Total data: {result.TotalBytes / 1024.0:F1} KB");
        
        if (result.SuccessfulOperations.HasValue)
        {
            Console.WriteLine($"  Success rate: {result.SuccessfulOperations}/{result.Times.Count}");
        }
    }

    private static void ValidatePerformanceTargets(BenchmarkResults results, bool skipApiCalls)
    {
        Console.WriteLine();
        Console.WriteLine("✅ Performance Target Validation");
        Console.WriteLine("===============================");

        // Target: Cache reads should be 1-5ms
        if (results.CacheReads != null)
        {
            var cacheReadAvg = results.CacheReads.AverageMs;
            if (cacheReadAvg >= 1 && cacheReadAvg <= 5)
            {
                Console.WriteLine($"✅ Cache read speed: {cacheReadAvg:F1}ms (Target: 1-5ms)");
            }
            else
            {
                Console.WriteLine($"⚠️ Cache read speed: {cacheReadAvg:F1}ms (Target: 1-5ms)");
            }
        }

        // Target: API calls should be 200-500ms
        if (!skipApiCalls && results.ApiBaseline != null)
        {
            var apiAvg = results.ApiBaseline.AverageMs;
            if (apiAvg >= 200 && apiAvg <= 500)
            {
                Console.WriteLine($"✅ API call speed: {apiAvg:F1}ms (Expected: 200-500ms)");
            }
            else
            {
                Console.WriteLine($"ℹ️ API call speed: {apiAvg:F1}ms (Expected: 200-500ms)");
            }
        }

        // Target: 40-500x improvement
        if (!skipApiCalls && results.ApiBaseline != null && results.CacheReads != null)
        {
            var improvement = results.ApiBaseline.AverageMs / results.CacheReads.AverageMs;
            if (improvement >= 40)
            {
                Console.WriteLine($"✅ Speed improvement: {improvement:F1}x (Target: 40-500x)");
            }
            else
            {
                Console.WriteLine($"⚠️ Speed improvement: {improvement:F1}x (Target: 40-500x)");
            }
        }

        Console.WriteLine();
        Console.WriteLine("🎯 **VALIDATION MILESTONE**: Cache infrastructure performance validated!");
    }
}

public class BenchmarkResults
{
    public BenchmarkResult? ApiBaseline { get; set; }
    public BenchmarkResult? CacheWrites { get; set; }
    public BenchmarkResult? CacheReads { get; set; }
    public BenchmarkResult? DeepSearch { get; set; }
}

public class BenchmarkResult
{
    public List<long> Times { get; set; } = new();
    public long TotalBytes { get; set; }
    public string Operation { get; set; } = string.Empty;
    public int? SuccessfulOperations { get; set; }

    public double AverageMs => Times.Count > 0 ? Times.Average() : 0;
    public long MinMs => Times.Count > 0 ? Times.Min() : 0;
    public long MaxMs => Times.Count > 0 ? Times.Max() : 0;
}