using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lfm.Cli.Commands;

public class RecentCommand : BaseCommand
{
    private readonly ILastFmService _lastFmService;

    public RecentCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILastFmService lastFmService,
        ISymbolProvider symbolProvider,
        ILogger<RecentCommand> logger)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _lastFmService = lastFmService ?? throw new ArgumentNullException(nameof(lastFmService));
    }

    public async Task ExecuteAsync(
        int limit = 20,
        int? hours = null,
        string? username = null,
        bool json = false,
        bool forceCache = false,
        bool forceApi = false,
        bool noCache = false)
    {
        await ExecuteWithErrorHandlingAsync("recent command", async () =>
        {
            // Configure cache behavior
            ConfigureCaching(timing: false, forceCache, forceApi, noCache);

            // Get username (from parameter or config default)
            var effectiveUsername = await GetUsernameAsync(username);
            if (effectiveUsername == null)
                return;

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
                    var albumInfo = !string.IsNullOrEmpty(track.Album.Name) ? $" ({track.Album.Name})" : "";

                    Console.WriteLine($"  {nowPlayingIndicator}{track.Artist.Name} - {track.Name}{albumInfo} [{timeInfo}]");
                }

                Console.WriteLine();
                Console.WriteLine($"Total: {result.Tracks.Count} tracks");
            }
        });
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
