using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Lfm.Core.Configuration;

public enum UnicodeSupport
{
    Auto,     // Auto-detect terminal capabilities
    Enabled,  // Force Unicode symbols
    Disabled  // Force ASCII fallbacks
}

public class LfmConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultUsername { get; set; } = string.Empty;
    public string DefaultPeriod { get; set; } = "overall";
    public int DefaultLimit { get; set; } = 10;
    public int ApiThrottleMs { get; set; } = 100;
    public int NormalSearchDepth { get; set; } = 10000;
    public int DeepSearchTimeoutSeconds { get; set; } = 300;
    
    // Cache Configuration
    public bool CacheEnabled { get; set; } = true;
    public int CacheExpiryMinutes { get; set; } = 10;
    public int MaxCacheSizeMB { get; set; } = 100;
    public int MaxCacheFiles { get; set; } = 10000;
    public int MaxCacheAgeDays { get; set; } = 30;
    public DateTime LastCacheCleanup { get; set; } = DateTime.MinValue;
    public int CleanupIntervalHours { get; set; } = 6;
    
    // Display Configuration
    public UnicodeSupport UnicodeSymbols { get; set; } = UnicodeSupport.Auto;

    // Playlist Diversity Configuration
    public int DateRangeDiversityMultiplier { get; set; } = 10;
    public int MaxEmptyWindows { get; set; } = 5;

    // Mixtape Configuration
    public float DefaultMixtapeBias { get; set; } = 0.3f;
    public int DefaultMinPlays { get; set; } = 0;
    public int MaxMixtapeSampleSize { get; set; } = 50000;

    // Tag Filtering Configuration
    public List<string> ExcludedTags { get; set; } = new()
    {
        "classical", "classical music", "opera", "symphony", "orchestral",
        "baroque", "romantic era", "contemporary classical", "chamber music",
        "choral", "classical crossover"
    };

    public int TagFilterThreshold { get; set; } = 30;
    public bool EnableTagFiltering { get; set; } = false;
    public int MaxTagLookups { get; set; } = 20;

    // Spotify Configuration
    public SpotifyConfig Spotify { get; set; } = new();
    public string DefaultPlayer { get; set; } = "text"; // "text", "spotify"

    // Debug settings
    public bool EnableApiDebugLogging { get; set; } = false;
}

public class SpotifyConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string DefaultDevice { get; set; } = string.Empty;
    public int RateLimitDelayMs { get; set; } = 100;
    public int SearchTimeoutMs { get; set; } = 5000;
    public int MaxRetries { get; set; } = 3;
    public bool FallbackToLooseSearch { get; set; } = true;
}

public interface IConfigurationManager
{
    Task<LfmConfig> LoadAsync();
    Task SaveAsync(LfmConfig config);
    string GetConfigPath();
}

public class ConfigurationManager : IConfigurationManager
{
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly string _configPath;

    public ConfigurationManager(ILogger<ConfigurationManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "lfm"
        );
        
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "config.json");
    }

    public string GetConfigPath() => _configPath;

    public async Task<LfmConfig> LoadAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogInformation("Config file not found, creating default config at {Path}", _configPath);
                var defaultConfig = new LfmConfig();
                await SaveAsync(defaultConfig);
                return defaultConfig;
            }

            var json = await File.ReadAllTextAsync(_configPath);
            var config = JsonSerializer.Deserialize<LfmConfig>(json, GetJsonOptions());
            
            if (config == null)
            {
                _logger.LogWarning("Failed to deserialize config, using default");
                return new LfmConfig();
            }

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration from {Path}", _configPath);
            return new LfmConfig();
        }
    }

    public async Task SaveAsync(LfmConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, GetJsonOptions());
            await File.WriteAllTextAsync(_configPath, json);
            _logger.LogDebug("Configuration saved to {Path}", _configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration to {Path}", _configPath);
            throw;
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