using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Lfm.Core.Configuration;

public class LfmConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultUsername { get; set; } = string.Empty;
    public string DefaultPeriod { get; set; } = "overall";
    public int DefaultLimit { get; set; } = 10;
    public int ApiThrottleMs { get; set; } = 100;
    public int NormalSearchDepth { get; set; } = 10000;
    public int DeepSearchTimeoutSeconds { get; set; } = 300;
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