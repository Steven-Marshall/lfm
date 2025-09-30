using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

/// <summary>
/// Command for checking user's listening history for specific artists or tracks
/// </summary>
public class CheckCommand : BaseCommand
{
    public CheckCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILogger<CheckCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
    }

    /// <summary>
    /// Check if user has listened to an artist
    /// </summary>
    public async Task<int> ExecuteAsync(string artist, string? username = null, bool timing = false, bool verbose = false)
    {
        var config = await _configManager.LoadAsync();
        var user = username ?? config.DefaultUsername;
        if (string.IsNullOrEmpty(user))
        {
            _logger.LogError("Username not specified and no default username configured");
            return 1;
        }

        var stopwatch = timing ? System.Diagnostics.Stopwatch.StartNew() : null;

        try
        {
            if (verbose) _logger.LogInformation("Checking listening history for artist: {Artist}", artist);

            var result = await _apiClient.GetArtistInfoWithResultAsync(artist, user);

            stopwatch?.Stop();

            if (!result.Success)
            {
                _logger.LogError("Failed to check artist: {Error}", result.Error?.Message ?? "Unknown error");
                return 1;
            }

            var artistInfo = result.Data;
            var userPlaycount = artistInfo.Artist.Stats.GetUserPlaycount();
            var globalPlaycount = artistInfo.Artist.Stats.GetGlobalPlaycount();

            if (userPlaycount == 0)
            {
                Console.WriteLine($"{artistInfo.Artist.Name}: Never played");
            }
            else
            {
                Console.WriteLine($"{artistInfo.Artist.Name}: {userPlaycount:N0} plays");
            }

            if (verbose)
            {
                Console.WriteLine($"Global plays: {globalPlaycount:N0}");
                if (artistInfo.Artist.Tags?.Tag.Any() == true)
                {
                    var tags = string.Join(", ", artistInfo.Artist.Tags.Tag.Take(5).Select(t => t.Name));
                    Console.WriteLine($"Tags: {tags}");
                }
            }

            if (timing && stopwatch != null)
            {
                Console.WriteLine($"Response time: {stopwatch.ElapsedMilliseconds}ms");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking artist listening history");
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Check if user has listened to a specific track
    /// </summary>
    public async Task<int> ExecuteAsync(string artist, string track, string? username = null, bool timing = false, bool verbose = false)
    {
        var config = await _configManager.LoadAsync();
        var user = username ?? config.DefaultUsername;
        if (string.IsNullOrEmpty(user))
        {
            _logger.LogError("Username not specified and no default username configured");
            return 1;
        }

        var stopwatch = timing ? System.Diagnostics.Stopwatch.StartNew() : null;

        try
        {
            if (verbose) _logger.LogInformation("Checking listening history for track: {Artist} - {Track}", artist, track);

            var result = await _apiClient.GetTrackInfoWithResultAsync(artist, track, user);

            stopwatch?.Stop();

            if (!result.Success)
            {
                _logger.LogError("Failed to check track: {Error}", result.Error?.Message ?? "Unknown error");
                return 1;
            }

            var trackInfo = result.Data;
            var userPlaycount = trackInfo.Track.GetUserPlaycount();
            var isLoved = trackInfo.Track.IsUserLoved();
            var globalPlaycount = trackInfo.Track.GetGlobalPlaycount();

            if (userPlaycount == 0)
            {
                Console.WriteLine($"{trackInfo.Track.Artist.Name} - {trackInfo.Track.Name}: Never played");
            }
            else
            {
                var loveStatus = isLoved ? " ❤️" : "";
                Console.WriteLine($"{trackInfo.Track.Artist.Name} - {trackInfo.Track.Name}: {userPlaycount:N0} plays{loveStatus}");
            }

            if (verbose)
            {
                Console.WriteLine($"Global plays: {globalPlaycount:N0}");
                if (!string.IsNullOrEmpty(trackInfo.Track.Duration) && int.TryParse(trackInfo.Track.Duration, out var duration))
                {
                    var minutes = duration / 60000;
                    var seconds = (duration % 60000) / 1000;
                    Console.WriteLine($"Duration: {minutes}:{seconds:D2}");
                }
                if (trackInfo.Track.Album != null)
                {
                    Console.WriteLine($"Album: {trackInfo.Track.Album.Title}");
                }
                if (trackInfo.Track.TopTags?.Tag.Any() == true)
                {
                    var tags = string.Join(", ", trackInfo.Track.TopTags.Tag.Take(5).Select(t => t.Name));
                    Console.WriteLine($"Tags: {tags}");
                }
            }

            if (timing && stopwatch != null)
            {
                Console.WriteLine($"Response time: {stopwatch.ElapsedMilliseconds}ms");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking track listening history");
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}