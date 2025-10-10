using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

public class RecentCommand
{
    private readonly ILastFmService _lastFmService;
    private readonly IConfigurationManager _configManager;
    private readonly ISymbolProvider _symbols;
    private readonly ILogger<RecentCommand> _logger;

    public RecentCommand(
        ILastFmService lastFmService,
        IConfigurationManager configManager,
        ISymbolProvider symbolProvider,
        ILogger<RecentCommand> logger)
    {
        _lastFmService = lastFmService ?? throw new ArgumentNullException(nameof(lastFmService));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _symbols = symbolProvider ?? throw new ArgumentNullException(nameof(symbolProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(
        int limit = 20,
        int? hours = null,
        string? username = null,
        bool json = false)
    {
        try
        {
            var config = await _configManager.LoadAsync();
            var effectiveUsername = username ?? config.DefaultUsername;

            if (string.IsNullOrEmpty(effectiveUsername))
            {
                Console.WriteLine($"{_symbols.Error} No username specified. Use --user or set default username with 'lfm config set username YOUR_USERNAME'");
                return;
            }

            var result = await _lastFmService.GetUserRecentTracksAsync(effectiveUsername, limit, hours);

            if (result?.Tracks == null || !result.Tracks.Any())
            {
                if (json)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        success = false,
                        message = "No recent tracks found"
                    }, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    var timeDescription = hours.HasValue ? $"last {hours} hours" : "last 7 days";
                    Console.WriteLine($"{_symbols.Music} No tracks found in {timeDescription}");
                }
                return;
            }

            if (json)
            {
                var jsonOutput = new
                {
                    success = true,
                    tracks = result.Tracks.Select(t => new
                    {
                        track = t.Name,
                        artist = t.Artist.Name,
                        album = t.Album.Name,
                        timestamp = t.Date?.UnixTimestamp,
                        date = t.Date?.Text,
                        nowPlaying = t.Attributes?.NowPlaying != null
                    }).ToList(),
                    count = result.Tracks.Count
                };
                Console.WriteLine(JsonSerializer.Serialize(jsonOutput, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                var timeDescription = hours.HasValue ? $"last {hours} hours" : "last 7 days";
                Console.WriteLine($"{_symbols.Music} Recent tracks for {effectiveUsername} ({timeDescription}):");
                Console.WriteLine();

                foreach (var track in result.Tracks)
                {
                    var nowPlayingIndicator = track.Attributes?.NowPlaying != null ? $"{_symbols.Music} " : "";
                    var timeInfo = GetFormattedTime(track.Date);

                    Console.WriteLine($"  {nowPlayingIndicator}{track.Artist.Name} - {track.Name} [{timeInfo}]");
                }

                Console.WriteLine();
                Console.WriteLine($"Total: {result.Tracks.Count} tracks");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent tracks");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }

    private string GetFormattedTime(Lfm.Core.Models.DateInfo? date)
    {
        if (date == null || !long.TryParse(date.UnixTimestamp, out var unixTimestamp))
            return "unknown time";

        var trackTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
        var localTime = trackTime.ToLocalTime();
        var now = DateTime.UtcNow;
        var timeSpan = now - trackTime.UtcDateTime;

        string timeAgo;
        if (timeSpan.TotalMinutes < 1)
            timeAgo = "just now";
        else if (timeSpan.TotalMinutes < 60)
            timeAgo = $"{(int)timeSpan.TotalMinutes}m ago";
        else if (timeSpan.TotalHours < 24)
            timeAgo = $"{(int)timeSpan.TotalHours}h ago";
        else if (timeSpan.TotalDays < 7)
            timeAgo = $"{(int)timeSpan.TotalDays}d ago";
        else
            timeAgo = localTime.ToString("MMM dd");

        var timestamp = localTime.ToString("HH:mm");
        return $"{timestamp}, {timeAgo}";
    }
}
