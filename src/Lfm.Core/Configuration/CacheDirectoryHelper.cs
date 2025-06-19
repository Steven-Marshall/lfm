using Microsoft.Extensions.Logging;

namespace Lfm.Core.Configuration;

/// <summary>
/// Cross-platform helper for managing cache directory paths following platform conventions.
/// </summary>
public interface ICacheDirectoryHelper
{
    /// <summary>
    /// Gets the platform-appropriate cache directory path.
    /// Windows: %LOCALAPPDATA%\lfm\cache\
    /// Linux: ~/.cache/lfm/
    /// </summary>
    string GetCacheDirectory();
    
    /// <summary>
    /// Ensures the cache directory exists, creating it if necessary.
    /// </summary>
    /// <returns>True if directory exists or was created successfully, false otherwise.</returns>
    bool EnsureCacheDirectoryExists();
    
    /// <summary>
    /// Gets the full path for a cache file with the given filename.
    /// </summary>
    /// <param name="filename">The cache filename (without path)</param>
    /// <returns>Full path to the cache file</returns>
    string GetCacheFilePath(string filename);
}

public class CacheDirectoryHelper : ICacheDirectoryHelper
{
    private readonly ILogger<CacheDirectoryHelper> _logger;
    private readonly string _cacheDirectory;

    public CacheDirectoryHelper(ILogger<CacheDirectoryHelper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheDirectory = DetermineCacheDirectory();
    }

    public string GetCacheDirectory() => _cacheDirectory;

    public bool EnsureCacheDirectoryExists()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                _logger.LogDebug("Cache directory already exists: {CacheDirectory}", _cacheDirectory);
                return true;
            }

            Directory.CreateDirectory(_cacheDirectory);
            _logger.LogInformation("Created cache directory: {CacheDirectory}", _cacheDirectory);
            
            // Verify the directory was created successfully
            if (Directory.Exists(_cacheDirectory))
            {
                return true;
            }
            
            _logger.LogError("Failed to verify cache directory creation: {CacheDirectory}", _cacheDirectory);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cache directory: {CacheDirectory}", _cacheDirectory);
            return false;
        }
    }

    public string GetCacheFilePath(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Filename cannot be null or empty", nameof(filename));

        return Path.Combine(_cacheDirectory, filename);
    }

    private static string DetermineCacheDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows: %LOCALAPPDATA%\lfm\cache\
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "lfm", "cache");
        }

        // Linux/Unix - Follow XDG Base Directory specification
        var xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrEmpty(xdgCacheHome))
        {
            // XDG_CACHE_HOME is set, use it
            return Path.Combine(xdgCacheHome, "lfm");
        }

        // XDG_CACHE_HOME not set, use default ~/.cache/lfm
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDirectory, ".cache", "lfm");
    }
}