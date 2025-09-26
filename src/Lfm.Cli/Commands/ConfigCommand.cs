using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

public class ConfigCommand
{
    private readonly IConfigurationManager _configManager;
    private readonly ILogger<ConfigCommand> _logger;
    private readonly ISymbolProvider _symbols;

    public ConfigCommand(
        IConfigurationManager configManager,
        ILogger<ConfigCommand> logger,
        ISymbolProvider symbolProvider)
    {
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _symbols = symbolProvider ?? throw new ArgumentNullException(nameof(symbolProvider));
    }

    public async Task SetApiKeyAsync(string apiKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine($"{_symbols.Error} API key cannot be empty.");
                Console.WriteLine($"{_symbols.Tip} Get your API key from: https://www.last.fm/api/account/create");
                return;
            }

            var config = await _configManager.LoadAsync();
            config.ApiKey = apiKey.Trim();
            await _configManager.SaveAsync(config);

            Console.WriteLine($"{_symbols.Success} API key saved successfully.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting API key");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
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
            Console.WriteLine();
            Console.WriteLine("Cache Settings:");
            Console.WriteLine($"Cache Enabled: {(config.CacheEnabled ? "✅ Yes" : "❌ No")}");
            Console.WriteLine($"Cache Expiry: {config.CacheExpiryMinutes} minutes");
            Console.WriteLine($"Max Cache Size: {config.MaxCacheSizeMB}MB");
            Console.WriteLine($"Max Cache Files: {config.MaxCacheFiles:N0}");
            Console.WriteLine($"Max Cache Age: {config.MaxCacheAgeDays} days");
            Console.WriteLine();
            Console.WriteLine("Display Settings:");
            Console.WriteLine($"Unicode Symbols: {config.UnicodeSymbols}");

            Console.WriteLine();
            Console.WriteLine("Playlist Diversity:");
            Console.WriteLine($"Date Range Multiplier: {config.DateRangeDiversityMultiplier} (affects toptracks --year/--from/--to sampling)");
            Console.WriteLine($"Max Empty Windows: {config.MaxEmptyWindows} (search persistence for diverse playlists)");

            Console.WriteLine();
            Console.WriteLine("Spotify Integration:");
            Console.WriteLine($"Client ID: {(string.IsNullOrEmpty(config.Spotify.ClientId) ? "❌ Not set" : "✅ Set")}");
            Console.WriteLine($"Client Secret: {(string.IsNullOrEmpty(config.Spotify.ClientSecret) ? "❌ Not set" : "✅ Set")}");
            Console.WriteLine($"Refresh Token: {(string.IsNullOrEmpty(config.Spotify.RefreshToken) ? "❌ Not set" : "✅ Set")}");
            Console.WriteLine($"Rate Limit Delay: {config.Spotify.RateLimitDelayMs}ms");
            Console.WriteLine($"Search Timeout: {config.Spotify.SearchTimeoutMs}ms");
            Console.WriteLine($"Max Retries: {config.Spotify.MaxRetries}");
            Console.WriteLine($"Fallback Search: {(config.Spotify.FallbackToLooseSearch ? "✅ Yes" : "❌ No")}");
            Console.WriteLine($"Default Device: {(string.IsNullOrEmpty(config.Spotify.DefaultDevice) ? "❌ Not set" : config.Spotify.DefaultDevice)}");

            Console.WriteLine();
            Console.WriteLine("🏷️ Tag Filtering:");
            Console.WriteLine($"Filtering Enabled: {(config.EnableTagFiltering ? "✅ Yes" : "❌ No")}");
            Console.WriteLine($"Tag Threshold: {config.TagFilterThreshold} (minimum tag count for exclusion)");
            Console.WriteLine($"Max API Lookups: {config.MaxTagLookups} (per recommendation request)");

            if (config.ExcludedTags.Any())
            {
                var tagCount = config.ExcludedTags.Count;
                var displayTags = tagCount <= 5
                    ? string.Join(", ", config.ExcludedTags.OrderBy(t => t))
                    : string.Join(", ", config.ExcludedTags.OrderBy(t => t).Take(5)) + $", ... (+{tagCount - 5} more)";

                Console.WriteLine($"Excluded Tags ({tagCount}): {displayTags}");

                if (tagCount > 5)
                {
                    Console.WriteLine($"  💡 Use 'lfm config show-excluded-tags' to see all {tagCount} tags");
                }
            }
            else
            {
                Console.WriteLine("Excluded Tags: None configured");
            }

            if (string.IsNullOrEmpty(config.ApiKey))
            {
                Console.WriteLine();
                Console.WriteLine("💡 To get started:");
                Console.WriteLine("1. Get an API key from: https://www.last.fm/api/account/create");
                Console.WriteLine("2. Set it with: lfm config set-api-key <your-api-key>");
                Console.WriteLine("3. Set your username: lfm config set-user <your-username>");
            }
            else if (config.ExcludedTags.Any() && !config.EnableTagFiltering)
            {
                Console.WriteLine();
                Console.WriteLine("💡 Tag Filtering Tip:");
                Console.WriteLine("You have excluded tags configured but filtering is disabled.");
                Console.WriteLine("Enable with: lfm config enable-tag-filtering");
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

    public async Task SetCacheExpiryAsync(int expiryMinutes)
    {
        try
        {
            if (expiryMinutes < 1)
            {
                Console.WriteLine("❌ Cache expiry must be >= 1 minute.");
                return;
            }

            var config = await _configManager.LoadAsync();
            config.CacheExpiryMinutes = expiryMinutes;
            await _configManager.SaveAsync(config);

            Console.WriteLine($"✅ Cache expiry set to {expiryMinutes} minutes.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache expiry");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task SetUnicodeSymbolsAsync(string unicodeMode)
    {
        try
        {
            if (!Enum.TryParse<UnicodeSupport>(unicodeMode, true, out var mode))
            {
                Console.WriteLine("❌ Invalid Unicode mode. Valid options: Auto, Enabled, Disabled");
                return;
            }

            var config = await _configManager.LoadAsync();
            config.UnicodeSymbols = mode;
            await _configManager.SaveAsync(config);

            var description = mode switch
            {
                UnicodeSupport.Auto => "auto-detect terminal capabilities",
                UnicodeSupport.Enabled => "always use Unicode symbols",
                UnicodeSupport.Disabled => "always use ASCII fallbacks",
                _ => mode.ToString()
            };

            Console.WriteLine($"✅ Unicode symbols set to {mode} ({description}).");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Unicode symbols");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task ShowExcludedTagsAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();

            Console.WriteLine($"{_symbols.Settings} Current excluded tags configuration:");
            Console.WriteLine($"  Filtering enabled: {(config.EnableTagFiltering ? _symbols.Success : _symbols.Error)} {config.EnableTagFiltering}");
            Console.WriteLine($"  Tag threshold: {config.TagFilterThreshold}");
            Console.WriteLine($"  Excluded tags ({config.ExcludedTags.Count}):");

            if (config.ExcludedTags.Any())
            {
                foreach (var tag in config.ExcludedTags.OrderBy(t => t))
                {
                    Console.WriteLine($"    - {tag}");
                }
            }
            else
            {
                Console.WriteLine($"    {_symbols.Error} No tags excluded");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing excluded tags");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task AddExcludedTagsAsync(string[] tags)
    {
        if (tags == null || tags.Length == 0)
        {
            Console.WriteLine($"{_symbols.Error} No tags provided.");
            return;
        }

        try
        {
            var config = await _configManager.LoadAsync();
            var addedTags = new List<string>();
            var skippedTags = new List<string>();

            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    skippedTags.Add($"'{tag}' (empty)");
                    continue;
                }

                var trimmedTag = tag.Trim();
                if (config.ExcludedTags.Any(t => string.Equals(t, trimmedTag, StringComparison.OrdinalIgnoreCase)))
                {
                    skippedTags.Add($"'{trimmedTag}' (already excluded)");
                    continue;
                }

                config.ExcludedTags.Add(trimmedTag);
                addedTags.Add(trimmedTag);
            }

            if (addedTags.Any())
            {
                await _configManager.SaveAsync(config);

                if (addedTags.Count == 1)
                {
                    Console.WriteLine($"{_symbols.Success} Added '{addedTags[0]}' to excluded tags.");
                }
                else
                {
                    Console.WriteLine($"{_symbols.Success} Added {addedTags.Count} tags to excluded tags:");
                    foreach (var tag in addedTags)
                    {
                        Console.WriteLine($"  + {tag}");
                    }
                }

                Console.WriteLine($"  Total excluded tags: {config.ExcludedTags.Count}");
                Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
            }

            if (skippedTags.Any())
            {
                Console.WriteLine($"{_symbols.StopSign} Skipped {skippedTags.Count} tags:");
                foreach (var skipped in skippedTags)
                {
                    Console.WriteLine($"  - {skipped}");
                }
            }

            if (!addedTags.Any() && !skippedTags.Any())
            {
                Console.WriteLine($"{_symbols.Error} No valid tags to add.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding excluded tags");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task AddExcludedTagAsync(string tag)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                Console.WriteLine($"{_symbols.Error} Tag cannot be empty.");
                return;
            }

            var config = await _configManager.LoadAsync();
            var trimmedTag = tag.Trim();

            if (config.ExcludedTags.Any(t => string.Equals(t, trimmedTag, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"{_symbols.StopSign} Tag '{trimmedTag}' is already excluded.");
                return;
            }

            config.ExcludedTags.Add(trimmedTag);
            await _configManager.SaveAsync(config);

            Console.WriteLine($"{_symbols.Success} Added '{trimmedTag}' to excluded tags.");
            Console.WriteLine($"  Total excluded tags: {config.ExcludedTags.Count}");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding excluded tag");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task RemoveExcludedTagsAsync(string[] tags)
    {
        if (tags == null || tags.Length == 0)
        {
            Console.WriteLine($"{_symbols.Error} No tags provided.");
            return;
        }

        try
        {
            var config = await _configManager.LoadAsync();
            var removedTags = new List<string>();
            var notFoundTags = new List<string>();

            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    notFoundTags.Add($"'{tag}' (empty)");
                    continue;
                }

                var trimmedTag = tag.Trim();
                var removed = config.ExcludedTags.RemoveAll(t =>
                    string.Equals(t, trimmedTag, StringComparison.OrdinalIgnoreCase));

                if (removed > 0)
                {
                    removedTags.Add(trimmedTag);
                }
                else
                {
                    notFoundTags.Add($"'{trimmedTag}' (not found)");
                }
            }

            if (removedTags.Any())
            {
                await _configManager.SaveAsync(config);

                if (removedTags.Count == 1)
                {
                    Console.WriteLine($"{_symbols.Success} Removed '{removedTags[0]}' from excluded tags.");
                }
                else
                {
                    Console.WriteLine($"{_symbols.Success} Removed {removedTags.Count} tags from excluded tags:");
                    foreach (var tag in removedTags)
                    {
                        Console.WriteLine($"  - {tag}");
                    }
                }

                Console.WriteLine($"  Total excluded tags: {config.ExcludedTags.Count}");
                Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
            }

            if (notFoundTags.Any())
            {
                Console.WriteLine($"{_symbols.StopSign} Could not remove {notFoundTags.Count} tags:");
                foreach (var notFound in notFoundTags)
                {
                    Console.WriteLine($"  - {notFound}");
                }
            }

            if (!removedTags.Any() && !notFoundTags.Any())
            {
                Console.WriteLine($"{_symbols.Error} No valid tags to remove.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing excluded tags");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task RemoveExcludedTagAsync(string tag)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                Console.WriteLine($"{_symbols.Error} Tag cannot be empty.");
                return;
            }

            var config = await _configManager.LoadAsync();
            var trimmedTag = tag.Trim();

            var removed = config.ExcludedTags.RemoveAll(t =>
                string.Equals(t, trimmedTag, StringComparison.OrdinalIgnoreCase));

            if (removed == 0)
            {
                Console.WriteLine($"{_symbols.StopSign} Tag '{trimmedTag}' was not found in excluded tags.");
                return;
            }

            await _configManager.SaveAsync(config);

            Console.WriteLine($"{_symbols.Success} Removed '{trimmedTag}' from excluded tags.");
            Console.WriteLine($"  Total excluded tags: {config.ExcludedTags.Count}");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing excluded tag");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task ClearExcludedTagsAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            var count = config.ExcludedTags.Count;

            if (count == 0)
            {
                Console.WriteLine($"{_symbols.StopSign} No excluded tags to clear.");
                return;
            }

            config.ExcludedTags.Clear();
            await _configManager.SaveAsync(config);

            Console.WriteLine($"{_symbols.Success} Cleared all {count} excluded tags.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing excluded tags");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task SetTagThresholdAsync(int threshold)
    {
        try
        {
            if (threshold < 0)
            {
                Console.WriteLine($"{_symbols.Error} Threshold must be >= 0.");
                return;
            }

            var config = await _configManager.LoadAsync();
            config.TagFilterThreshold = threshold;
            await _configManager.SaveAsync(config);

            Console.WriteLine($"{_symbols.Success} Tag filter threshold set to {threshold}.");
            Console.WriteLine($"  Only tags with count >= {threshold} will trigger exclusion.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting tag threshold");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task SetMaxTagLookupsAsync(int maxLookups)
    {
        try
        {
            if (maxLookups < 0)
            {
                Console.WriteLine($"{_symbols.Error} Max tag lookups must be >= 0.");
                return;
            }

            var config = await _configManager.LoadAsync();
            config.MaxTagLookups = maxLookups;
            await _configManager.SaveAsync(config);

            Console.WriteLine($"{_symbols.Success} Max tag lookups set to {maxLookups}.");
            Console.WriteLine($"  Tag filtering will use at most {maxLookups} API calls per recommendation request.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting max tag lookups");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task SetTagFilteringEnabledAsync(bool enabled)
    {
        try
        {
            var config = await _configManager.LoadAsync();
            config.EnableTagFiltering = enabled;
            await _configManager.SaveAsync(config);

            var status = enabled ? "enabled" : "disabled";
            Console.WriteLine($"{_symbols.Success} Tag filtering has been {status}.");

            if (enabled && !config.ExcludedTags.Any())
            {
                Console.WriteLine($"{_symbols.Tip} No excluded tags configured yet. Add tags using:");
                Console.WriteLine("  lfm config add-excluded-tag \"classical\"");
            }

            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting tag filtering enabled state");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task SetApiDebugLoggingEnabledAsync(bool enabled)
    {
        try
        {
            var config = await _configManager.LoadAsync();
            config.EnableApiDebugLogging = enabled;
            await _configManager.SaveAsync(config);

            var status = enabled ? "enabled" : "disabled";
            Console.WriteLine($"{_symbols.Success} API debug logging has been {status}.");

            if (enabled)
            {
                Console.WriteLine($"{_symbols.Tip} Debug logging will generate detailed output for all API calls.");
                Console.WriteLine("  This may significantly increase log verbosity and performance impact.");
                Console.WriteLine("  Use 'lfm config disable-debug-logging' to turn it off when debugging is complete.");
            }

            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting API debug logging enabled state");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.GenericError, ex.Message));
        }
    }

    public async Task SetSpotifyClientIdAsync(string clientId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                Console.WriteLine($"{_symbols.Error} Spotify Client ID cannot be empty.");
                Console.WriteLine($"{_symbols.Tip} Get your Client ID from: https://developer.spotify.com/dashboard");
                return;
            }

            var config = await _configManager.LoadAsync();
            config.Spotify.ClientId = clientId.Trim();
            await _configManager.SaveAsync(config);

            Console.WriteLine($"{_symbols.Success} Spotify Client ID saved successfully.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Spotify Client ID");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }

    public async Task SetSpotifyClientSecretAsync(string clientSecret)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                Console.WriteLine($"{_symbols.Error} Spotify Client Secret cannot be empty.");
                Console.WriteLine($"{_symbols.Tip} Get your Client Secret from: https://developer.spotify.com/dashboard");
                return;
            }

            var config = await _configManager.LoadAsync();
            config.Spotify.ClientSecret = clientSecret.Trim();
            await _configManager.SaveAsync(config);

            Console.WriteLine($"{_symbols.Success} Spotify Client Secret saved successfully.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Spotify Client Secret");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }

    public async Task ClearSpotifyRefreshTokenAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            config.Spotify.RefreshToken = string.Empty;
            await _configManager.SaveAsync(config);

            Console.WriteLine($"{_symbols.Success} Spotify refresh token cleared.");
            Console.WriteLine($"{_symbols.Tip} Next Spotify command will require re-authorization with updated permissions.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Spotify refresh token");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }

    public async Task SetSpotifyDefaultDeviceAsync(string deviceName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                Console.WriteLine($"{_symbols.Error} Device name cannot be empty.");
                Console.WriteLine($"{_symbols.Tip} Use 'lfm spotify devices' to see available devices.");
                return;
            }

            var config = await _configManager.LoadAsync();
            config.Spotify.DefaultDevice = deviceName.Trim();
            await _configManager.SaveAsync(config);

            Console.WriteLine($"{_symbols.Success} Default Spotify device set to: {deviceName.Trim()}");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Spotify default device");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }

    public async Task ClearSpotifyDefaultDeviceAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            var previousDevice = config.Spotify.DefaultDevice;
            config.Spotify.DefaultDevice = string.Empty;
            await _configManager.SaveAsync(config);

            if (!string.IsNullOrEmpty(previousDevice))
            {
                Console.WriteLine($"{_symbols.Success} Default Spotify device cleared (was: {previousDevice}).");
            }
            else
            {
                Console.WriteLine($"{_symbols.Success} Default Spotify device cleared.");
            }
            Console.WriteLine($"Will now use automatic device selection.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Spotify default device");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }

    public async Task SetDateRangeMultiplierAsync(int multiplier)
    {
        try
        {
            if (multiplier < 1 || multiplier > 100)
            {
                Console.WriteLine($"{_symbols.Error} Multiplier must be between 1 and 100.");
                Console.WriteLine($"{_symbols.Tip} Higher values = more diverse playlists but slower performance.");
                return;
            }

            var config = await _configManager.LoadAsync();
            var previousMultiplier = config.DateRangeDiversityMultiplier;
            config.DateRangeDiversityMultiplier = multiplier;
            await _configManager.SaveAsync(config);

            Console.WriteLine($"{_symbols.Success} Date range diversity multiplier set to: {multiplier} (was: {previousMultiplier})");
            Console.WriteLine("This affects toptracks commands with date ranges (--year, --from/--to).");
            Console.WriteLine($"{_symbols.Tip} Sample size formula: limit × tracks-per-artist × {multiplier}");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting date range diversity multiplier");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }

    public async Task SetMaxEmptyWindowsAsync(int maxWindows)
    {
        try
        {
            if (maxWindows < 1 || maxWindows > 50)
            {
                Console.WriteLine($"{_symbols.Error} Max empty windows must be between 1 and 50.");
                Console.WriteLine($"{_symbols.Tip} Higher values = more thorough searching but slower performance for diverse playlists.");
                return;
            }

            var config = await _configManager.LoadAsync();
            var previousValue = config.MaxEmptyWindows;
            config.MaxEmptyWindows = maxWindows;
            await _configManager.SaveAsync(config);

            Console.WriteLine($"{_symbols.Success} Max empty windows set to: {maxWindows} (was: {previousValue})");
            Console.WriteLine("This affects toptracks commands when searching for artist diversity.");
            Console.WriteLine($"{_symbols.Tip} Algorithm will search through {maxWindows} consecutive empty windows before giving up.");
            Console.WriteLine(ErrorMessages.Format(ErrorMessages.ConfigSavedTo, _configManager.GetConfigPath()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting max empty windows");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }
}