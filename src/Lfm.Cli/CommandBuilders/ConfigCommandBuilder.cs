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

        command.AddCommand(setApiKeyCommand);
        command.AddCommand(setUserCommand);
        command.AddCommand(showCommand);
        command.AddCommand(setThrottleCommand);
        command.AddCommand(setDepthCommand);
        command.AddCommand(setTimeoutCommand);

        return command;
    }
}