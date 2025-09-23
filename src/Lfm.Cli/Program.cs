using System.CommandLine;
using Lfm.Cli.CommandBuilders;
using Lfm.Cli.Commands;
using Lfm.Cli.Services;
using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Core.Services;
using Lfm.Core.Services.Cache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        var rootCommand = new RootCommand("Last.fm CLI tool for getting your music statistics")
        {
            ArtistsCommandBuilder.Build(host.Services),
            TracksCommandBuilder.Build(host.Services),
            AlbumsCommandBuilder.Build(host.Services),
            ArtistTracksCommandBuilder.Build(host.Services),
            ArtistAlbumsCommandBuilder.Build(host.Services),
            RecommendationsCommandBuilder.Build(host.Services),
            ConfigCommandBuilder.Build(host.Services),
            CacheStatusCommandBuilder.Build(host.Services),
            CacheClearCommandBuilder.Build(host.Services),
            TestCacheCommandBuilder.Build(host.Services),
            BenchmarkCacheCommandBuilder.Build(host.Services),
            SpotifyCommandBuilder.Build(host.Services)
        };

        return await rootCommand.InvokeAsync(args);
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddHttpClient();
                
                services.AddSingleton<IConfigurationManager, ConfigurationManager>();
                services.AddSingleton<ICacheDirectoryHelper, CacheDirectoryHelper>();
                services.AddSingleton<ISymbolProvider, SymbolProvider>();
                
                // Cache services
                services.AddSingleton<ICacheStorage, FileCacheStorage>();
                services.AddSingleton<ICacheKeyGenerator, CacheKeyGenerator>();
                
                // Register the actual LastFm API client
                services.AddSingleton<LastFmApiClient>(serviceProvider =>
                {
                    var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                    var httpClient = httpClientFactory.CreateClient();
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "lfm-cli/1.0");
                    
                    var logger = serviceProvider.GetRequiredService<ILogger<LastFmApiClient>>();
                    var configManager = serviceProvider.GetRequiredService<IConfigurationManager>();
                    var config = configManager.LoadAsync().GetAwaiter().GetResult();
                    
                    return new LastFmApiClient(httpClient, logger, config.ApiKey, config.ApiThrottleMs);
                });

                // Register the cached wrapper as the main interface
                services.AddSingleton<ILastFmApiClient>(serviceProvider =>
                {
                    var innerClient = serviceProvider.GetRequiredService<LastFmApiClient>();
                    var cacheStorage = serviceProvider.GetRequiredService<ICacheStorage>();
                    var keyGenerator = serviceProvider.GetRequiredService<ICacheKeyGenerator>();
                    var logger = serviceProvider.GetRequiredService<ILogger<CachedLastFmApiClient>>();
                    var configManager = serviceProvider.GetRequiredService<IConfigurationManager>();
                    
                    return new CachedLastFmApiClient(innerClient, cacheStorage, keyGenerator, logger, configManager, 10);
                });
                
                services.AddTransient<IDisplayService, DisplayService>();
                services.AddTransient<ITagFilterService, TagFilterService>();

                // Service layer
                services.AddTransient<ILastFmService, LastFmService>();
                services.AddTransient<ISpotifyStreamingService, SpotifyStreamingService>();

                // Spotify services
                services.AddTransient<Lfm.Spotify.IPlaylistStreamer>(serviceProvider =>
                {
                    var configManager = serviceProvider.GetRequiredService<IConfigurationManager>();
                    var config = configManager.LoadAsync().GetAwaiter().GetResult();
                    return new Lfm.Spotify.SpotifyStreamer(config.Spotify, configManager);
                });
                
                services.AddTransient<ArtistsCommand>();
                services.AddTransient<TracksCommand>();
                services.AddTransient<AlbumsCommand>();
                services.AddTransient<RecommendationsCommand>();
                services.AddTransient<SpotifyCommand>();
                
                // Cache management commands
                services.AddTransient<CacheStatusCommand>();
                services.AddTransient<CacheClearCommand>();
                // Artist search commands using generic implementation
                services.AddTransient<ArtistSearchCommand<Track, TopTracks>>(serviceProvider =>
                {
                    var apiClient = serviceProvider.GetRequiredService<ILastFmApiClient>();
                    var configManager = serviceProvider.GetRequiredService<IConfigurationManager>();
                    var displayService = serviceProvider.GetRequiredService<IDisplayService>();
                    var logger = serviceProvider.GetRequiredService<ILogger<ArtistSearchCommand<Track, TopTracks>>>();
                    var symbolProvider = serviceProvider.GetRequiredService<ISymbolProvider>();
                    
                    return new ArtistSearchCommand<Track, TopTracks>(
                        apiClient,
                        configManager,
                        displayService,
                        logger,
                        symbolProvider,
                        "tracks",
                        (user, period, limit, page) => apiClient.GetTopTracksAsync(user, period, limit, page),
                        response => response.Tracks,
                        track => track.Artist.Name,
                        (tracks, startRank) => displayService.DisplayTracksForUser(tracks, startRank));
                });
                
                services.AddTransient<ArtistSearchCommand<Album, TopAlbums>>(serviceProvider =>
                {
                    var apiClient = serviceProvider.GetRequiredService<ILastFmApiClient>();
                    var configManager = serviceProvider.GetRequiredService<IConfigurationManager>();
                    var displayService = serviceProvider.GetRequiredService<IDisplayService>();
                    var logger = serviceProvider.GetRequiredService<ILogger<ArtistSearchCommand<Album, TopAlbums>>>();
                    var symbolProvider = serviceProvider.GetRequiredService<ISymbolProvider>();
                    
                    return new ArtistSearchCommand<Album, TopAlbums>(
                        apiClient,
                        configManager,
                        displayService,
                        logger,
                        symbolProvider,
                        "albums",
                        (user, period, limit, page) => apiClient.GetTopAlbumsAsync(user, period, limit, page),
                        response => response.Albums,
                        album => album.Artist.Name,
                        (albums, startRank) => displayService.DisplayAlbums(albums, startRank));
                });
                services.AddTransient<ConfigCommand>(serviceProvider =>
                {
                    var configManager = serviceProvider.GetRequiredService<IConfigurationManager>();
                    var logger = serviceProvider.GetRequiredService<ILogger<ConfigCommand>>();
                    var symbolProvider = serviceProvider.GetRequiredService<ISymbolProvider>();
                    return new ConfigCommand(configManager, logger, symbolProvider);
                });
                services.AddTransient<RecommendationsCommand>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            });

}