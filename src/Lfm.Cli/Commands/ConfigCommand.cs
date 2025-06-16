using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

public class ConfigCommand
{
    private readonly IConfigurationManager _configManager;
    private readonly ILogger<ConfigCommand> _logger;

    public ConfigCommand(
        IConfigurationManager configManager,
        ILogger<ConfigCommand> logger)
    {
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SetApiKeyAsync(string apiKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine(ErrorMessages.EmptyApiKey);
                return;
            }

            var config = await _configManager.LoadAsync();
            config.ApiKey = apiKey.Trim();
            await _configManager.SaveAsync(config);

            Console.WriteLine(ErrorMessages.ApiKeySaved);
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting API key");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task SetUsernameAsync(string username)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                Console.WriteLine(ErrorMessages.EmptyUsername);
                return;
            }

            var config = await _configManager.LoadAsync();
            config.DefaultUsername = username.Trim();
            await _configManager.SaveAsync(config);

            Console.WriteLine(ErrorMessages.UsernameSaved);
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting username");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task ShowConfigAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            
            Console.WriteLine("📋 Current Configuration:");
            Console.WriteLine($"Config file: {_configManager.GetConfigPath()}");
            Console.WriteLine();
            
            Console.WriteLine($"API Key: {(string.IsNullOrEmpty(config.ApiKey) ? "❌ Not set" : "✅ Set")}");
            Console.WriteLine($"Default Username: {(string.IsNullOrEmpty(config.DefaultUsername) ? "❌ Not set" : config.DefaultUsername)}");
            Console.WriteLine($"Default Period: {config.DefaultPeriod}");
            Console.WriteLine($"Default Limit: {config.DefaultLimit}");
            Console.WriteLine($"API Throttle: {config.ApiThrottleMs}ms delay between requests");
            Console.WriteLine($"Normal Search Depth: {config.NormalSearchDepth:N0} items");
            Console.WriteLine($"Deep Search Timeout: {config.DeepSearchTimeoutSeconds} seconds");
            
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                Console.WriteLine();
                Console.WriteLine("💡 To get started:");
                Console.WriteLine("1. Get an API key from: https://www.last.fm/api/account/create");
                Console.WriteLine("2. Set it with: lfm config set-api-key <your-api-key>");
                Console.WriteLine("3. Set your username: lfm config set-user <your-username>");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing configuration");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task SetApiThrottleAsync(int throttleMs)
    {
        try
        {
            if (throttleMs < 0)
            {
                Console.WriteLine("❌ API throttle must be >= 0 milliseconds (0 = no throttling).");
                return;
            }

            var config = await _configManager.LoadAsync();
            config.ApiThrottleMs = throttleMs;
            await _configManager.SaveAsync(config);

            var throttleDesc = throttleMs == 0 ? "disabled (no throttling)" : $"set to {throttleMs}ms";
            Console.WriteLine($"✅ API throttle {throttleDesc}.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting API throttle");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task SetSearchDepthAsync(int depth)
    {
        try
        {
            if (depth < 1)
            {
                Console.WriteLine("❌ Search depth must be >= 1 items.");
                return;
            }

            var config = await _configManager.LoadAsync();
            config.NormalSearchDepth = depth;
            await _configManager.SaveAsync(config);

            Console.WriteLine($"✅ Normal search depth set to {depth:N0} items.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting search depth");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task SetDeepTimeoutAsync(int timeoutSeconds)
    {
        try
        {
            if (timeoutSeconds < 0)
            {
                Console.WriteLine("❌ Timeout must be >= 0 seconds (0 = no timeout).");
                return;
            }

            var config = await _configManager.LoadAsync();
            config.DeepSearchTimeoutSeconds = timeoutSeconds;
            await _configManager.SaveAsync(config);

            var timeoutDesc = timeoutSeconds == 0 ? "disabled (no timeout)" : $"set to {timeoutSeconds} seconds";
            Console.WriteLine($"✅ Deep search timeout {timeoutDesc}.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting deep search timeout");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }
}