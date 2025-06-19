using System.CommandLine;
using Lfm.Cli.Commands;
using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Lfm.Core.Services.Cache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Command builder for cache performance benchmarking.
/// </summary>
public static class BenchmarkCacheCommandBuilder
{
    public static Command Build(IServiceProvider serviceProvider)
    {
        var command = new Command("benchmark-cache", "Benchmark cache performance against direct API calls")
        {
            IsHidden = false // Make this visible for performance validation
        };

        // Username argument
        var usernameArgument = new Argument<string?>("username", () => null, "Last.fm username (uses default if not provided)");
        command.AddArgument(usernameArgument);

        // Options
        var skipApiOption = new Option<bool>("--skip-api", () => false, "Skip actual API calls (use mock data for testing)");
        var iterationsOption = new Option<int>("--iterations", () => 10, "Number of iterations for each test (default: 10)");
        var deepSearchOption = new Option<bool>("--deep-search", () => false, "Include deep search benchmark (multiple pages)");
        var delayOption = new Option<int>("--delay", () => 200, "Delay between API calls in milliseconds (0 = no throttling, default: 200)");
        
        command.AddOption(skipApiOption);
        command.AddOption(iterationsOption);
        command.AddOption(deepSearchOption);
        command.AddOption(delayOption);

        command.SetHandler(async (string? username, bool skipApi, int iterations, bool deepSearch, int delay) =>
        {
            var apiClient = serviceProvider.GetRequiredService<ILastFmApiClient>();
            var cacheStorage = serviceProvider.GetRequiredService<ICacheStorage>();
            var keyGenerator = serviceProvider.GetRequiredService<ICacheKeyGenerator>();
            var configManager = serviceProvider.GetRequiredService<IConfigurationManager>();
            var logger = serviceProvider.GetRequiredService<ILogger<BenchmarkCacheCommand>>();

            var benchmarkCommand = new BenchmarkCacheCommand(
                apiClient, 
                cacheStorage, 
                keyGenerator, 
                configManager, 
                logger);

            // Get username from config if not provided
            if (string.IsNullOrEmpty(username))
            {
                var config = await configManager.LoadAsync();
                username = config.DefaultUsername;
                
                if (string.IsNullOrEmpty(username))
                {
                    Console.WriteLine("❌ No username provided and no default configured.");
                    Console.WriteLine("   Provide username: lfm benchmark-cache <username>");
                    Console.WriteLine("   Or set default: lfm config set-user <username>");
                    return;
                }
            }

            await benchmarkCommand.ExecuteAsync(username, skipApi, iterations, deepSearch, delay);
        }, usernameArgument, skipApiOption, iterationsOption, deepSearchOption, delayOption);

        return command;
    }
}