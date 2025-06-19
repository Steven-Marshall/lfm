using Lfm.Core.Configuration;
using Lfm.Core.Services.Cache;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Lfm.Cli.Commands;

/// <summary>
/// Temporary command to test cache functionality across platforms.
/// This will be removed once caching is fully implemented.
/// </summary>
public class TestCacheCommand
{
    private readonly ICacheDirectoryHelper _cacheDirectoryHelper;
    private readonly ICacheStorage _cacheStorage;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly ILogger<TestCacheCommand> _logger;

    public TestCacheCommand(
        ICacheDirectoryHelper cacheDirectoryHelper,
        ICacheStorage cacheStorage,
        ICacheKeyGenerator keyGenerator,
        ILogger<TestCacheCommand> logger)
    {
        _cacheDirectoryHelper = cacheDirectoryHelper ?? throw new ArgumentNullException(nameof(cacheDirectoryHelper));
        _cacheStorage = cacheStorage ?? throw new ArgumentNullException(nameof(cacheStorage));
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Execute()
    {
        _logger.LogInformation("Testing cache directory functionality...");
        
        // Test 1: Get cache directory path
        var cacheDir = _cacheDirectoryHelper.GetCacheDirectory();
        Console.WriteLine($"Cache directory path: {cacheDir}");
        
        // Test 2: Check if directory exists
        var exists = Directory.Exists(cacheDir);
        Console.WriteLine($"Directory exists: {exists}");
        
        // Test 3: Ensure directory exists
        var created = _cacheDirectoryHelper.EnsureCacheDirectoryExists();
        Console.WriteLine($"Directory creation result: {created}");
        
        // Test 4: Verify directory exists after creation
        var existsAfter = Directory.Exists(cacheDir);
        Console.WriteLine($"Directory exists after creation: {existsAfter}");
        
        // Test 5: Test cache file path generation
        var testFilePath = _cacheDirectoryHelper.GetCacheFilePath("test-cache-file.json");
        Console.WriteLine($"Sample cache file path: {testFilePath}");
        
        // Test 6: Test with different filename patterns
        var hashFilePath = _cacheDirectoryHelper.GetCacheFilePath("a1b2c3d4e5f6g7h8.json");
        Console.WriteLine($"Hash-based cache file path: {hashFilePath}");
        
        // Test 7: Test file path handling with special characters (cache key simulation)
        Console.WriteLine("\n🔤 Testing special character handling:");
        
        var specialCharTests = new[]
        {
            "simple-test.json",
            "user_with_underscore.json", 
            "hash-with-dashes.json",
            "numbers123.json",
            "MixedCaseTest.json",
            "dots.in.filename.json",
            "very-long-filename-that-might-be-a-sha256-hash-0123456789abcdef.json"
        };
        
        foreach (var testFilename in specialCharTests)
        {
            try
            {
                var specialPath = _cacheDirectoryHelper.GetCacheFilePath(testFilename);
                Console.WriteLine($"  ✅ {testFilename} → {specialPath}");
                
                // Verify the path is valid by checking it can be parsed
                var dir = Path.GetDirectoryName(specialPath);
                var file = Path.GetFileName(specialPath);
                if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file))
                {
                    Console.WriteLine($"    ❌ Invalid path structure");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ {testFilename} → Error: {ex.Message}");
            }
        }
        
        // Test 8: Test invalid filename handling
        Console.WriteLine("\n🚫 Testing invalid filename handling:");
        var invalidTests = new[] { "", "   ", null };
        
        foreach (var invalidFilename in invalidTests)
        {
            try
            {
                var invalidPath = _cacheDirectoryHelper.GetCacheFilePath(invalidFilename!);
                Console.WriteLine($"  ❌ '{invalidFilename}' should have failed but returned: {invalidPath}");
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"  ✅ '{invalidFilename}' correctly rejected");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ '{invalidFilename}' failed with unexpected error: {ex.Message}");
            }
        }
        
        // Test 9: Show what Windows paths would look like
        Console.WriteLine("\n🪟 Windows path simulation:");
        Console.WriteLine($"Current OS: {(OperatingSystem.IsWindows() ? "Windows" : "Linux/Unix")}");
        
        if (!OperatingSystem.IsWindows())
        {
            // Simulate what Windows paths would be
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            
            Console.WriteLine($"Current UserProfile: {userProfile}");
            Console.WriteLine($"Current LocalApplicationData: {localAppData}");
            
            // Show what Windows would theoretically generate
            Console.WriteLine("On Windows, cache would be at: %LOCALAPPDATA%\\lfm\\cache\\");
            Console.WriteLine("Example: C:\\Users\\username\\AppData\\Local\\lfm\\cache\\");
        }
        
        Console.WriteLine("\n✅ Cache directory test completed successfully!");
        Console.WriteLine("\n📋 Summary:");
        Console.WriteLine($"  Platform: {(OperatingSystem.IsWindows() ? "Windows" : "Linux/Unix")}");
        Console.WriteLine($"  Cache dir: {cacheDir}");
        Console.WriteLine($"  Created: {created}");
        Console.WriteLine($"  Exists: {existsAfter}");
        
        // Test 10: Cache Storage Operations
        Console.WriteLine("\n💾 Testing cache storage operations:");
        TestCacheOperations().Wait();
    }

    private async Task TestCacheOperations()
    {
        try
        {
            // Test cache key generation
            var key1 = _keyGenerator.ForTopTracks("testuser", "overall", 50, 1);
            var key2 = _keyGenerator.ForTopArtists("testuser", "7day", 20, 2);
            var key3 = _keyGenerator.ForArtistTopTracks("Pink Floyd", 10);
            
            Console.WriteLine($"  ✅ Generated cache keys:");
            Console.WriteLine($"    TopTracks: {key1}");
            Console.WriteLine($"    TopArtists: {key2}");
            Console.WriteLine($"    ArtistTracks: {key3}");

            // Test data storage and retrieval
            var testJson1 = """{"tracks":{"track":[{"name":"Test Track","artist":{"name":"Test Artist"}}]}}""";
            var testJson2 = """{"artists":{"artist":[{"name":"Test Artist","playcount":"100"}]}}""";
            
            // Store cache entries
            var stopwatch = Stopwatch.StartNew();
            var stored1 = await _cacheStorage.StoreAsync(key1, testJson1, 1); // 1 minute expiry for testing
            var stored2 = await _cacheStorage.StoreAsync(key2, testJson2, 10); // 10 minute expiry
            stopwatch.Stop();
            
            Console.WriteLine($"  ✅ Storage test:");
            Console.WriteLine($"    Store 1: {stored1}");
            Console.WriteLine($"    Store 2: {stored2}");
            Console.WriteLine($"    Storage time: {stopwatch.ElapsedMilliseconds}ms");

            // Test existence checks
            var exists1 = await _cacheStorage.ExistsAsync(key1);
            var exists2 = await _cacheStorage.ExistsAsync(key2);
            var exists3 = await _cacheStorage.ExistsAsync("nonexistent-key");
            
            Console.WriteLine($"  ✅ Existence test:");
            Console.WriteLine($"    Key1 exists: {exists1}");
            Console.WriteLine($"    Key2 exists: {exists2}");
            Console.WriteLine($"    Nonexistent exists: {exists3}");

            // Test retrieval
            stopwatch.Restart();
            var retrieved1 = await _cacheStorage.RetrieveAsync(key1);
            var retrieved2 = await _cacheStorage.RetrieveAsync(key2);
            var retrieved3 = await _cacheStorage.RetrieveAsync("nonexistent-key");
            stopwatch.Stop();
            
            Console.WriteLine($"  ✅ Retrieval test:");
            Console.WriteLine($"    Retrieved 1: {(retrieved1 != null ? "✅ Success" : "❌ Failed")}");
            Console.WriteLine($"    Retrieved 2: {(retrieved2 != null ? "✅ Success" : "❌ Failed")}");
            Console.WriteLine($"    Retrieved nonexistent: {(retrieved3 == null ? "✅ Correctly null" : "❌ Unexpected data")}");
            Console.WriteLine($"    Retrieval time: {stopwatch.ElapsedMilliseconds}ms");

            // Test data integrity
            var dataIntact1 = retrieved1 == testJson1;
            var dataIntact2 = retrieved2 == testJson2;
            
            Console.WriteLine($"  ✅ Data integrity test:");
            Console.WriteLine($"    Data 1 intact: {dataIntact1}");
            Console.WriteLine($"    Data 2 intact: {dataIntact2}");

            // Test statistics
            var stats = await _cacheStorage.GetStatisticsAsync();
            Console.WriteLine($"  ✅ Statistics test:");
            Console.WriteLine($"    Total entries: {stats.TotalEntries}");
            Console.WriteLine($"    Total size: {stats.TotalSizeBytes} bytes");
            Console.WriteLine($"    Expired entries: {stats.ExpiredEntries}");

            // Test cache key consistency (same parameters should generate same key)
            var keyConsistency1 = _keyGenerator.ForTopTracks("testuser", "overall", 50, 1);
            var keyConsistency2 = _keyGenerator.ForTopTracks("testuser", "overall", 50, 1);
            var keyDifferent = _keyGenerator.ForTopTracks("testuser", "overall", 50, 2);
            
            Console.WriteLine($"  ✅ Key consistency test:");
            Console.WriteLine($"    Same params generate same key: {keyConsistency1 == keyConsistency2}");
            Console.WriteLine($"    Different params generate different key: {keyConsistency1 != keyDifferent}");

            // Test cleanup
            var removedCount = await _cacheStorage.CleanupExpiredAsync();
            Console.WriteLine($"  ✅ Cleanup test:");
            Console.WriteLine($"    Cleaned up {removedCount} expired entries");

            Console.WriteLine($"\n💾 Cache storage test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Cache storage test failed: {ex.Message}");
            _logger.LogError(ex, "Cache storage test failed");
        }
    }
}