using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Lfm.Spotify;
using Lfm.Sonos;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

/// <summary>
/// Base class for unified playback commands that support both Spotify and Sonos
/// </summary>
public abstract class BasePlaybackCommand
{
    protected readonly IConfigurationManager _configManager;
    protected readonly ILogger _logger;
    protected readonly ISymbolProvider _symbols;
    protected readonly IPlaylistStreamer _spotifyStreamer;
    protected readonly ISonosStreamer _sonosStreamer;

    protected BasePlaybackCommand(
        IConfigurationManager configManager,
        ILogger logger,
        ISymbolProvider symbolProvider,
        IPlaylistStreamer spotifyStreamer,
        ISonosStreamer sonosStreamer)
    {
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _symbols = symbolProvider ?? throw new ArgumentNullException(nameof(symbolProvider));
        _spotifyStreamer = spotifyStreamer ?? throw new ArgumentNullException(nameof(spotifyStreamer));
        _sonosStreamer = sonosStreamer ?? throw new ArgumentNullException(nameof(sonosStreamer));
    }

    /// <summary>
    /// Executes a playback action with unified player routing
    /// </summary>
    /// <param name="player">Optional player override (Spotify/Sonos)</param>
    /// <param name="room">Optional Sonos room override</param>
    /// <param name="spotifyAction">Action to execute on Spotify</param>
    /// <param name="sonosAction">Action to execute on Sonos (receives room name)</param>
    /// <param name="operationName">Name of operation for logging (e.g., "pause", "resume")</param>
    /// <returns>Exit code (0 = success, 1 = error)</returns>
    protected async Task<int> ExecutePlaybackActionAsync(
        string? player,
        string? room,
        Func<Task<bool>> spotifyAction,
        Func<string, Task> sonosAction,
        string operationName)
    {
        try
        {
            // Load config to determine player
            var config = await _configManager.LoadAsync();

            // Determine which player to use: parameter > config default
            var targetPlayer = DetermineTargetPlayer(player, config);
            if (targetPlayer == null)
            {
                return 1;
            }

            // Route to appropriate player
            if (targetPlayer == PlayerType.Spotify)
            {
                return await ExecuteSpotifyActionAsync(spotifyAction, operationName);
            }
            else // Sonos
            {
                return await ExecuteSonosActionAsync(sonosAction, room, config, operationName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing {Operation} command", operationName);
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Determines target player from parameter or config default
    /// </summary>
    protected PlayerType? DetermineTargetPlayer(string? player, LfmConfig config)
    {
        if (!string.IsNullOrWhiteSpace(player))
        {
            if (!Enum.TryParse<PlayerType>(player, true, out var targetPlayer))
            {
                Console.WriteLine($"{_symbols.Error} Invalid player '{player}'. Valid options: Spotify, Sonos");
                return null;
            }
            return targetPlayer;
        }
        return config.DefaultPlayer;
    }

    /// <summary>
    /// Validates Sonos room and returns the target room name
    /// </summary>
    protected async Task<(bool isValid, string? roomName)> ValidateSonosRoomAsync(string? room, LfmConfig config)
    {
        // Determine which Sonos room to use
        var targetRoom = room ?? config.Sonos.DefaultRoom;
        if (string.IsNullOrWhiteSpace(targetRoom))
        {
            Console.WriteLine($"{_symbols.Error} No Sonos room specified and no default room configured.");
            Console.WriteLine($"{_symbols.Tip} Use --room parameter or set default with: lfm config set-sonos-default-room \"Room Name\"");
            return (false, null);
        }

        // Validate room exists
        try
        {
            await _sonosStreamer.ValidateRoomAsync(targetRoom);
            return (true, targetRoom);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{_symbols.Error} {ex.Message}");
            Console.WriteLine($"{_symbols.Tip} List available rooms with: lfm sonos rooms");
            return (false, null);
        }
    }

    /// <summary>
    /// Executes action on Spotify with availability check
    /// </summary>
    private async Task<int> ExecuteSpotifyActionAsync(Func<Task<bool>> action, string operationName)
    {
        if (!await _spotifyStreamer.IsAvailableAsync())
        {
            Console.WriteLine($"{_symbols.Error} Spotify is not configured or authenticated. Please run 'lfm spotify auth' first.");
            return 1;
        }

        var success = await action();
        return success ? 0 : 1;
    }

    /// <summary>
    /// Executes action on Sonos with availability and room validation
    /// </summary>
    private async Task<int> ExecuteSonosActionAsync(
        Func<string, Task> action,
        string? room,
        LfmConfig config,
        string operationName)
    {
        if (!await _sonosStreamer.IsAvailableAsync())
        {
            Console.WriteLine($"{_symbols.Error} Sonos bridge not available at {config.Sonos.HttpApiBaseUrl}. Check your configuration.");
            Console.WriteLine($"{_symbols.Tip} Set bridge URL with: lfm config set-sonos-api-url <url>");
            return 1;
        }

        // Determine which Sonos room to use
        var targetRoom = room ?? config.Sonos.DefaultRoom;
        if (string.IsNullOrWhiteSpace(targetRoom))
        {
            Console.WriteLine($"{_symbols.Error} No Sonos room specified and no default room configured.");
            Console.WriteLine($"{_symbols.Tip} Use --room parameter or set default with: lfm config set-sonos-default-room \"Room Name\"");
            return 1;
        }

        // Validate room exists
        try
        {
            await _sonosStreamer.ValidateRoomAsync(targetRoom);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{_symbols.Error} {ex.Message}");
            Console.WriteLine($"{_symbols.Tip} List available rooms with: lfm sonos rooms");
            return 1;
        }

        await action(targetRoom);
        return 0;
    }
}
