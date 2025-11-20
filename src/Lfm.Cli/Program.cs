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
            TopTracksCommandBuilder.Build(host.Services),
            MixtapeCommandBuilder.Build(host.Services),
            AlbumsCommandBuilder.Build(host.Services),
            RecentCommandBuilder.Build(host.Services),
            // DISABLED: Spotify's /browse/new-releases API is broken (returns months-old albums)
            // See: https://community.spotify.com/t5/Spotify-for-Developers/Web-API-Get-New-Releases-API-Returning-Old-Items/m-p/6069709
            // NewReleasesCommandBuilder.Build(host.Services),
            ArtistTracksCommandBuilder.Build(host.Services),
            ArtistAlbumsCommandBuilder.Build(host.Services),
            RecommendationsCommandBuilder.Build(host.Services),
            ConfigCommandBuilder.Build(host.Services),
            CacheStatusCommandBuilder.Build(host.Services),
            CacheClearCommandBuilder.Build(host.Services),
            TestCacheCommandBuilder.Build(host.Services),
            BenchmarkCacheCommandBuilder.Build(host.Services),
            SpotifyCommandBuilder.Build(host.Services),
            SonosCommandBuilder.Build(host.Services),
            ApiStatusCommandBuilder.Build(host.Services),
            CheckCommandBuilder.Build(host.Services),
            CreatePlaylistCommandBuilder.Build(host.Services),
            SimilarCommandBuilder.Build(host.Services),
            PlayCommandBuilder.Build(host.Services),
            PlaylistCommandBuilder.Build(host.Services),
            PlaylistsCommandBuilder.Build(host.Services),
            PauseCommandBuilder.Build(host.Services),
            ResumeCommandBuilder.Build(host.Services),
            SkipCommandBuilder.Build(host.Services),
            CurrentCommandBuilder.Build(host.Services)
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

                    return new LastFmApiClient(httpClient, logger, config.ApiKey, config.ApiThrottleMs, config.EnableApiDebugLogging, config.MaxApiRetries, config.RetryBaseDelayMs);
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

                // Spotify services (register first so we can inject into LastFmService)
                services.AddTransient<Lfm.Spotify.IPlaylistStreamer>(serviceProvider =>
                {
                    var configManager = serviceProvider.GetRequiredService<IConfigurationManager>();
                    var config = configManager.LoadAsync().GetAwaiter().GetResult();
                    return new Lfm.Spotify.SpotifyStreamer(config.Spotify, configManager);
                });

                // Sonos services
                services.AddTransient<Lfm.Sonos.ISonosStreamer>(serviceProvider =>
                {
                    var configManager = serviceProvider.GetRequiredService<IConfigurationManager>();
                    var config = configManager.LoadAsync().GetAwaiter().GetResult();
                    var logger = serviceProvider.GetRequiredService<ILogger<Lfm.Sonos.SonosStreamer>>();
                    return new Lfm.Sonos.SonosStreamer(config.Sonos, logger);
                });

                // Service layer
                services.AddTransient<ILastFmService>(serviceProvider =>
                {
                    var apiClient = serviceProvider.GetRequiredService<ILastFmApiClient>();
                    var configManager = serviceProvider.GetRequiredService<IConfigurationManager>();
                    var tagFilterService = serviceProvider.GetRequiredService<ITagFilterService>();
                    var logger = serviceProvider.GetRequiredService<ILogger<LastFmService>>();
                    var spotifyStreamer = serviceProvider.GetRequiredService<Lfm.Spotify.IPlaylistStreamer>();

                    return new LastFmService(apiClient, configManager, tagFilterService, logger, spotifyStreamer);
                });
                services.AddTransient<ISpotifyStreamingService, SpotifyStreamingService>();
                services.AddTransient<IPlaylistInputParser, PlaylistInputParser>();
                
                services.AddTransient<ArtistsCommand>();
                services.AddTransient<TracksCommand>();
                services.AddTransient<TopTracksCommand>();
                services.AddTransient<MixtapeCommand>();
                services.AddTransient<AlbumsCommand>();
                services.AddTransient<RecentCommand>();
                // DISABLED: Spotify's /browse/new-releases API is broken
                // services.AddTransient<NewReleasesCommand>();
                services.AddTransient<RecommendationsCommand>();
                services.AddTransient<SpotifyCommand>();

                // Cache management commands
                services.AddTransient<CacheStatusCommand>();
                services.AddTransient<CacheClearCommand>();
                services.AddTransient<ApiStatusCommand>();

                // Lookup commands
                services.AddTransient<CheckCommand>();
                services.AddTransient<CreatePlaylistCommand>();
                services.AddTransient<SimilarCommand>();
                services.AddTransient<PlayCommand>();
                services.AddTransient<PlaylistCommand>();
                services.AddTransient<PlaylistsCommand>();
                services.AddTransient<PauseCommand>();
                services.AddTransient<ResumeCommand>();
                services.AddTransient<SkipCommand>();
                services.AddTransient<CurrentCommand>();
                services.AddTransient<SonosRoomsCommand>();
                services.AddTransient<SonosStatusCommand>();
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
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole(options =>
                {
                    options.LogToStandardErrorThreshold = LogLevel.Trace;  // Redirect all logs to stderr
                });

                // Check if API debug logging is enabled in config
                try
                {
                    var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "lfm", "config.json");
                    if (File.Exists(configPath))
                    {
                        var configJson = File.ReadAllText(configPath);
                        if (configJson.Contains("\"enableApiDebugLogging\": true"))
                        {
                            logging.SetMinimumLevel(LogLevel.Information);
                        }
                        else
                        {
                            logging.SetMinimumLevel(LogLevel.Warning);
                        }
                    }
                    else
                    {
                        logging.SetMinimumLevel(LogLevel.Warning);
                    }
                }
                catch
                {
                    // If config loading fails, default to Warning level
                    logging.SetMinimumLevel(LogLevel.Warning);
                }
            });

}