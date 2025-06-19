using System.CommandLine;
using Lfm.Cli.Commands;
using Lfm.Core.Configuration;
using Lfm.Core.Services.Cache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.CommandBuilders;

/// <summary>
/// Temporary command builder for testing cache directory functionality.
/// This will be removed once caching is fully implemented.
/// </summary>
public static class TestCacheCommandBuilder
{
    public static Command Build(IServiceProvider serviceProvider)
    {
        var command = new Command("test-cache", "Test cache directory functionality (temporary command)")
        {
            IsHidden = true // Hide from normal help output
        };

        command.SetHandler(() =>
        {
            var cacheDirectoryHelper = serviceProvider.GetRequiredService<ICacheDirectoryHelper>();
            var cacheStorage = serviceProvider.GetRequiredService<ICacheStorage>();
            var keyGenerator = serviceProvider.GetRequiredService<ICacheKeyGenerator>();
            var logger = serviceProvider.GetRequiredService<ILogger<TestCacheCommand>>();
            
            var testCommand = new TestCacheCommand(cacheDirectoryHelper, cacheStorage, keyGenerator, logger);
            testCommand.Execute();
        });

        return command;
    }
}