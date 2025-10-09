using System.CommandLine;
using Lfm.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Cli.CommandBuilders;

public static class ConfigCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("config", "Manage Last.fm API key and default username configuration");
        
        var setApiKeyCommand = new Command("set-api-key", "Set your Last.fm API key (get from https://www.last.fm/api/account/create)");
        var apiKeyArg = new Argument<string>("api-key", "Your Last.fm API key");
        setApiKeyCommand.AddArgument(apiKeyArg);
        
        var setUserCommand = new Command("set-user", "Set your default Last.fm username");
        var userArg = new Argument<string>("username", "Your Last.fm username");
        setUserCommand.AddArgument(userArg);
        
        var showCommand = new Command("show", "Display current API key and username configuration");
        
        var setThrottleCommand = new Command("set-throttle", "Set API request throttle delay in milliseconds");
        var throttleArg = new Argument<int>("milliseconds", "Delay between API requests in milliseconds (0 = no throttling)");
        setThrottleCommand.AddArgument(throttleArg);
        
        var setDepthCommand = new Command("set-search-depth", "Set normal search depth (number of items to search through)");
        var depthArg = new Argument<int>("items", "Number of items to search through in normal searches");
        setDepthCommand.AddArgument(depthArg);
        
        var setTimeoutCommand = new Command("set-deep-timeout", "Set deep search timeout in seconds");
        var timeoutArg = new Argument<int>("seconds", "Timeout for deep searches in seconds (0 = no timeout)");
        setTimeoutCommand.AddArgument(timeoutArg);

        var setParallelCallsCommand = new Command("set-parallel-calls", "Set number of parallel API calls for batch requests");
        var parallelCallsArg = new Argument<int>("calls", "Number of parallel calls (1-10, default: 5)");
        setParallelCallsCommand.AddArgument(parallelCallsArg);

        var setCacheExpiryCommand = new Command("set-cache-expiry", "Set cache expiry time in minutes");
        var cacheExpiryArg = new Argument<int>("minutes", "Cache expiry time in minutes (default: 10)");
        setCacheExpiryCommand.AddArgument(cacheExpiryArg);
        
        var setUnicodeCommand = new Command("set-unicode", "Set Unicode symbols mode");
        var unicodeArg = new Argument<string>("mode", "Unicode mode: Auto, Enabled, or Disabled");
        setUnicodeCommand.AddArgument(unicodeArg);

        // Tag filtering commands
        var showExcludedTagsCommand = new Command("show-excluded-tags", "Display current excluded tags configuration");

        var addExcludedTagCommand = new Command("add-excluded-tag", "Add one or more tags to the excluded tags list");
        var addTagArg = new Argument<string[]>("tags", "One or more tags to exclude (e.g., 'classical', 'christmas', 'jazz')");
        addExcludedTagCommand.AddArgument(addTagArg);

        var removeExcludedTagCommand = new Command("remove-excluded-tag", "Remove one or more tags from the excluded tags list");
        var removeTagArg = new Argument<string[]>("tags", "One or more tags to remove from exclusion list");
        removeExcludedTagCommand.AddArgument(removeTagArg);

        var clearExcludedTagsCommand = new Command("clear-excluded-tags", "Clear all excluded tags");

        var setTagThresholdCommand = new Command("set-tag-threshold", "Set minimum tag count required for exclusion");
        var thresholdArg = new Argument<int>("threshold", "Minimum tag count (default: 30)");
        setTagThresholdCommand.AddArgument(thresholdArg);

        var setMaxTagLookupsCommand = new Command("set-max-tag-lookups", "Set maximum API calls for tag filtering");
        var maxLookupsArg = new Argument<int>("max-lookups", "Maximum API calls for tag lookups (default: 20)");
        setMaxTagLookupsCommand.AddArgument(maxLookupsArg);

        var enableTagFilteringCommand = new Command("enable-tag-filtering", "Enable tag filtering for recommendations");
        var disableTagFilteringCommand = new Command("disable-tag-filtering", "Disable tag filtering for recommendations");

        var enableDebugLoggingCommand = new Command("enable-debug-logging", "Enable detailed API debug logging");
        var disableDebugLoggingCommand = new Command("disable-debug-logging", "Disable detailed API debug logging");

        // Spotify configuration commands
        var setSpotifyClientIdCommand = new Command("set-spotify-client-id", "Set your Spotify Client ID");
        var spotifyClientIdArg = new Argument<string>("client-id", "Your Spotify Client ID from developer dashboard");
        setSpotifyClientIdCommand.AddArgument(spotifyClientIdArg);

        var setSpotifyClientSecretCommand = new Command("set-spotify-client-secret", "Set your Spotify Client Secret");
        var spotifyClientSecretArg = new Argument<string>("client-secret", "Your Spotify Client Secret from developer dashboard");
        setSpotifyClientSecretCommand.AddArgument(spotifyClientSecretArg);

        var clearSpotifyRefreshTokenCommand = new Command("clear-spotify-refresh-token", "Clear Spotify refresh token to force re-authorization");

        var setSpotifyDefaultDeviceCommand = new Command("set-spotify-default-device", "Set default Spotify device for playback");
        var spotifyDeviceArg = new Argument<string>("device-name", "Name of the Spotify device (use 'lfm spotify devices' to list)");
        setSpotifyDefaultDeviceCommand.AddArgument(spotifyDeviceArg);

        var clearSpotifyDefaultDeviceCommand = new Command("clear-spotify-default-device", "Clear default Spotify device (use automatic selection)");

        var setDateRangeMultiplierCommand = new Command("set-date-range-multiplier", "Set diversity multiplier for date range playlist queries");
        var multiplierArg = new Argument<int>("multiplier", "Multiplier for sample size (default: 10, higher = more diverse but slower)");
        setDateRangeMultiplierCommand.AddArgument(multiplierArg);

        var setMaxEmptyWindowsCommand = new Command("set-max-empty-windows", "Set maximum empty windows for playlist diversity search");
        var maxWindowsArg = new Argument<int>("windows", "Max empty windows to search through (default: 5, higher = more thorough but slower)");
        setMaxEmptyWindowsCommand.AddArgument(maxWindowsArg);

        setApiKeyCommand.SetHandler(async (string apiKey) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetApiKeyAsync(apiKey);
        }, apiKeyArg);

        setUserCommand.SetHandler(async (string username) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetUsernameAsync(username);
        }, userArg);

        showCommand.SetHandler(async () =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.ShowConfigAsync();
        });
        
        setThrottleCommand.SetHandler(async (int throttleMs) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetApiThrottleAsync(throttleMs);
        }, throttleArg);
        
        setDepthCommand.SetHandler(async (int depth) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetSearchDepthAsync(depth);
        }, depthArg);
        
        setTimeoutCommand.SetHandler(async (int timeout) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetDeepTimeoutAsync(timeout);
        }, timeoutArg);

        setParallelCallsCommand.SetHandler(async (int parallelCalls) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetParallelCallsAsync(parallelCalls);
        }, parallelCallsArg);

        setCacheExpiryCommand.SetHandler(async (int expiryMinutes) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetCacheExpiryAsync(expiryMinutes);
        }, cacheExpiryArg);
        
        setUnicodeCommand.SetHandler(async (string unicodeMode) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetUnicodeSymbolsAsync(unicodeMode);
        }, unicodeArg);

        showExcludedTagsCommand.SetHandler(async () =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.ShowExcludedTagsAsync();
        });

        addExcludedTagCommand.SetHandler(async (string[] tags) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.AddExcludedTagsAsync(tags);
        }, addTagArg);

        removeExcludedTagCommand.SetHandler(async (string[] tags) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.RemoveExcludedTagsAsync(tags);
        }, removeTagArg);

        clearExcludedTagsCommand.SetHandler(async () =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.ClearExcludedTagsAsync();
        });

        setTagThresholdCommand.SetHandler(async (int threshold) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetTagThresholdAsync(threshold);
        }, thresholdArg);

        setMaxTagLookupsCommand.SetHandler(async (int maxLookups) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetMaxTagLookupsAsync(maxLookups);
        }, maxLookupsArg);

        enableTagFilteringCommand.SetHandler(async () =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetTagFilteringEnabledAsync(true);
        });

        disableTagFilteringCommand.SetHandler(async () =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetTagFilteringEnabledAsync(false);
        });

        enableDebugLoggingCommand.SetHandler(async () =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetApiDebugLoggingEnabledAsync(true);
        });

        disableDebugLoggingCommand.SetHandler(async () =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetApiDebugLoggingEnabledAsync(false);
        });

        setSpotifyClientIdCommand.SetHandler(async (string clientId) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetSpotifyClientIdAsync(clientId);
        }, spotifyClientIdArg);

        setSpotifyClientSecretCommand.SetHandler(async (string clientSecret) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetSpotifyClientSecretAsync(clientSecret);
        }, spotifyClientSecretArg);

        clearSpotifyRefreshTokenCommand.SetHandler(async () =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.ClearSpotifyRefreshTokenAsync();
        });

        setSpotifyDefaultDeviceCommand.SetHandler(async (string deviceName) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetSpotifyDefaultDeviceAsync(deviceName);
        }, spotifyDeviceArg);

        clearSpotifyDefaultDeviceCommand.SetHandler(async () =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.ClearSpotifyDefaultDeviceAsync();
        });

        setDateRangeMultiplierCommand.SetHandler(async (int multiplier) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetDateRangeMultiplierAsync(multiplier);
        }, multiplierArg);

        setMaxEmptyWindowsCommand.SetHandler(async (int windows) =>
        {
            var configCommand = services.GetRequiredService<ConfigCommand>();
            await configCommand.SetMaxEmptyWindowsAsync(windows);
        }, maxWindowsArg);

        command.AddCommand(setApiKeyCommand);
        command.AddCommand(setUserCommand);
        command.AddCommand(showCommand);
        command.AddCommand(setThrottleCommand);
        command.AddCommand(setDepthCommand);
        command.AddCommand(setTimeoutCommand);
        command.AddCommand(setParallelCallsCommand);
        command.AddCommand(setCacheExpiryCommand);
        command.AddCommand(setUnicodeCommand);
        command.AddCommand(showExcludedTagsCommand);
        command.AddCommand(addExcludedTagCommand);
        command.AddCommand(removeExcludedTagCommand);
        command.AddCommand(clearExcludedTagsCommand);
        command.AddCommand(setTagThresholdCommand);
        command.AddCommand(setMaxTagLookupsCommand);
        command.AddCommand(enableTagFilteringCommand);
        command.AddCommand(disableTagFilteringCommand);
        command.AddCommand(enableDebugLoggingCommand);
        command.AddCommand(disableDebugLoggingCommand);
        command.AddCommand(setSpotifyClientIdCommand);
        command.AddCommand(setSpotifyClientSecretCommand);
        command.AddCommand(clearSpotifyRefreshTokenCommand);
        command.AddCommand(setSpotifyDefaultDeviceCommand);
        command.AddCommand(clearSpotifyDefaultDeviceCommand);
        command.AddCommand(setDateRangeMultiplierCommand);
        command.AddCommand(setMaxEmptyWindowsCommand);

        return command;
    }
}