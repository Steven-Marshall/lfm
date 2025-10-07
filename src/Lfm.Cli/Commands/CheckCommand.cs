using System.Text.Json;
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

            // If 0 plays and track has apostrophe, retry with smart quote variants
            var userPlaycount = result.Data?.Track.GetUserPlaycount() ?? 0;
            if (userPlaycount == 0 && track.Contains('\''))
            {
                userPlaycount = await GetTrackPlaycountWithApostropheRetry(artist, track, user, config.ApiThrottleMs);

                // Fetch full track info again with the successful variant if we found plays
                if (userPlaycount > 0)
                {
                    // Try left quote variant
                    var leftQuoteVariant = track.Replace('\'', '\u2018');
                    var retryResult = await _apiClient.GetTrackInfoWithResultAsync(artist, leftQuoteVariant, user);
                    if (retryResult.Success && retryResult.Data.Track.GetUserPlaycount() > 0)
                    {
                        result = retryResult;
                    }
                    else
                    {
                        // Try right quote variant
                        var rightQuoteVariant = track.Replace('\'', '\u2019');
                        retryResult = await _apiClient.GetTrackInfoWithResultAsync(artist, rightQuoteVariant, user);
                        if (retryResult.Success && retryResult.Data.Track.GetUserPlaycount() > 0)
                        {
                            result = retryResult;
                        }
                    }
                }
            }

            stopwatch?.Stop();

            if (!result.Success)
            {
                _logger.LogError("Failed to check track: {Error}", result.Error?.Message ?? "Unknown error");
                return 1;
            }

            var trackInfo = result.Data;
            userPlaycount = trackInfo.Track.GetUserPlaycount();
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

    /// <summary>
    /// Check if user has listened to a specific album with track-level breakdown
    /// </summary>
    public async Task<int> ExecuteAsync(
        string artist,
        string album,
        string? username = null,
        bool timing = false,
        bool verbose = false,
        bool json = false)
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
            if (verbose && !json)
                _logger.LogInformation("Checking listening history for album: {Artist} - {Album}", artist, album);

            // Get album info
            var result = await _apiClient.GetAlbumInfoWithResultAsync(artist, album, user);

            // If failed/0 plays and album has apostrophe, retry with smart quote variants
            if ((!result.Success || result.Data.Album.GetUserPlaycount() == 0) && album.Contains('\''))
            {
                // Try LEFT SINGLE QUOTATION MARK (U+2018)
                var leftQuoteVariant = album.Replace('\'', '\u2018');
                var retryResult = await _apiClient.GetAlbumInfoWithResultAsync(artist, leftQuoteVariant, user);
                if (retryResult.Success && retryResult.Data.Album.GetUserPlaycount() > 0)
                {
                    result = retryResult;
                }
                else
                {
                    // Try RIGHT SINGLE QUOTATION MARK (U+2019)
                    var rightQuoteVariant = album.Replace('\'', '\u2019');
                    retryResult = await _apiClient.GetAlbumInfoWithResultAsync(artist, rightQuoteVariant, user);
                    if (retryResult.Success && retryResult.Data.Album.GetUserPlaycount() > 0)
                    {
                        result = retryResult;
                    }
                }
            }

            if (!result.Success)
            {
                if (json)
                {
                    var errorOutput = new { success = false, error = result.Error?.Message ?? "Unknown error" };
                    Console.WriteLine(JsonSerializer.Serialize(errorOutput, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    _logger.LogError("Failed to check album: {Error}", result.Error?.Message ?? "Unknown error");
                }
                return 1;
            }

            var albumInfo = result.Data;
            var userPlaycount = albumInfo.Album.GetUserPlaycount();
            var trackCount = albumInfo.Album.GetTrackCount();

            // If verbose, fetch per-track play counts
            List<TrackPlayInfo>? trackBreakdown = null;
            if (verbose && albumInfo.Album.Tracks?.Track != null && albumInfo.Album.Tracks.Track.Any())
            {
                trackBreakdown = await FetchTrackPlaycounts(
                    albumInfo.Album.Tracks.Track,
                    artist,
                    user,
                    config.ApiThrottleMs);
            }

            stopwatch?.Stop();

            // Output results
            if (json)
            {
                OutputAlbumJson(albumInfo, trackBreakdown, stopwatch);
            }
            else
            {
                OutputAlbumConsole(albumInfo, trackBreakdown, verbose, stopwatch);
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (json)
            {
                var errorOutput = new { success = false, error = ex.Message };
                Console.WriteLine(JsonSerializer.Serialize(errorOutput, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                _logger.LogError(ex, "Error checking album listening history");
                Console.WriteLine($"Error: {ex.Message}");
            }
            return 1;
        }
    }

    private async Task<List<TrackPlayInfo>> FetchTrackPlaycounts(
        List<Lfm.Core.Models.AlbumLookupInfo.AlbumTrack> tracks,
        string artist,
        string username,
        int throttleMs)
    {
        var trackPlays = new List<TrackPlayInfo>();

        foreach (var track in tracks)
        {
            var userPlaycount = await GetTrackPlaycountWithApostropheRetry(artist, track.Name, username, throttleMs);

            trackPlays.Add(new TrackPlayInfo
            {
                Name = track.Name,  // Keep original name for display
                UserPlaycount = userPlaycount
            });

            // Throttle to respect API limits
            if (throttleMs > 0)
                await Task.Delay(throttleMs);
        }

        return trackPlays;
    }

    /// <summary>
    /// Gets track playcount with automatic retry using apostrophe variants.
    /// Last.fm's APIs return regular apostrophes (U+0027) but scrobble data may contain
    /// smart quotes (U+2018, U+2019), causing lookups to fail. This method retries with
    /// all apostrophe variants if the initial lookup returns 0 plays.
    /// </summary>
    private async Task<int> GetTrackPlaycountWithApostropheRetry(
        string artist,
        string trackName,
        string username,
        int throttleMs)
    {
        // Try original name first
        var trackInfo = await _apiClient.GetTrackInfoAsync(artist, trackName, username);
        var userPlaycount = trackInfo?.Track.GetUserPlaycount() ?? 0;

        // If we got plays, we're done
        if (userPlaycount > 0)
            return userPlaycount;

        // If track name contains regular apostrophe (U+0027), try smart quote variants
        if (trackName.Contains('\''))
        {
            // Try LEFT SINGLE QUOTATION MARK (U+2018)
            var leftQuoteVariant = trackName.Replace('\'', '\u2018');
            if (throttleMs > 0) await Task.Delay(throttleMs);

            trackInfo = await _apiClient.GetTrackInfoAsync(artist, leftQuoteVariant, username);
            userPlaycount = trackInfo?.Track.GetUserPlaycount() ?? 0;
            if (userPlaycount > 0)
                return userPlaycount;

            // Try RIGHT SINGLE QUOTATION MARK (U+2019)
            var rightQuoteVariant = trackName.Replace('\'', '\u2019');
            if (throttleMs > 0) await Task.Delay(throttleMs);

            trackInfo = await _apiClient.GetTrackInfoAsync(artist, rightQuoteVariant, username);
            userPlaycount = trackInfo?.Track.GetUserPlaycount() ?? 0;
            if (userPlaycount > 0)
                return userPlaycount;
        }

        // No plays found with any variant
        return 0;
    }

    private void OutputAlbumConsole(
        Lfm.Core.Models.AlbumLookupInfo albumInfo,
        List<TrackPlayInfo>? trackBreakdown,
        bool verbose,
        System.Diagnostics.Stopwatch? stopwatch)
    {
        var album = albumInfo.Album;
        var userPlaycount = album.GetUserPlaycount();
        var trackCount = album.GetTrackCount();

        if (userPlaycount == 0)
        {
            Console.WriteLine($"{album.Artist} - {album.Name}: Never played");
        }
        else
        {
            Console.WriteLine($"{album.Artist} - {album.Name}: {userPlaycount:N0} plays ({trackCount} tracks)");

            if (trackBreakdown != null && trackBreakdown.Any())
            {
                Console.WriteLine("\nTrack Breakdown:");

                var totalTrackPlays = trackBreakdown.Sum(t => t.UserPlaycount);
                var maxPlays = trackBreakdown.Max(t => t.UserPlaycount);

                for (int i = 0; i < trackBreakdown.Count; i++)
                {
                    var track = trackBreakdown[i];
                    var percentage = totalTrackPlays > 0
                        ? (track.UserPlaycount * 100.0 / totalTrackPlays)
                        : 0;

                    var indicator = track.UserPlaycount == maxPlays && maxPlays > 0 ? " ← Most played" : "";
                    Console.WriteLine($"  {i + 1,2}. {track.Name,-50} {track.UserPlaycount,4} plays ({percentage:F0}%){indicator}");
                }

                // Check for discrepancy between album total and track sum
                var discrepancy = userPlaycount - totalTrackPlays;
                if (discrepancy > 0)
                {
                    Console.WriteLine($"\nNote: {discrepancy:N0} plays unaccounted for in track breakdown.");
                    Console.WriteLine($"These may be scrobbled under different track names (e.g., featuring artists, remixes).");
                }
            }
        }

        if (verbose && trackBreakdown == null)
        {
            Console.WriteLine($"Global plays: {album.GetGlobalPlaycount():N0}");
            if (album.Tags?.Tag.Any() == true)
            {
                var tags = string.Join(", ", album.Tags.Tag.Take(5).Select(t => t.Name));
                Console.WriteLine($"Tags: {tags}");
            }
        }

        if (stopwatch != null)
        {
            Console.WriteLine($"\nResponse time: {stopwatch.ElapsedMilliseconds}ms");
        }
    }

    private void OutputAlbumJson(
        Lfm.Core.Models.AlbumLookupInfo albumInfo,
        List<TrackPlayInfo>? trackBreakdown,
        System.Diagnostics.Stopwatch? stopwatch)
    {
        var album = albumInfo.Album;
        var userPlaycount = album.GetUserPlaycount();
        var trackPlaycountSum = trackBreakdown?.Sum(t => t.UserPlaycount) ?? 0;
        var unaccountedPlays = userPlaycount - trackPlaycountSum;

        var output = new
        {
            success = true,
            artist = album.Artist,
            album = album.Name,
            userPlaycount = userPlaycount,
            trackCount = album.GetTrackCount(),
            globalPlaycount = album.GetGlobalPlaycount(),
            trackPlaycountSum = trackBreakdown != null ? trackPlaycountSum : (int?)null,
            unaccountedPlays = trackBreakdown != null && unaccountedPlays > 0 ? unaccountedPlays : (int?)null,
            hasDiscrepancy = trackBreakdown != null && unaccountedPlays > 0,
            tracks = trackBreakdown?.Select(t => new
            {
                name = t.Name,
                userPlaycount = t.UserPlaycount
            }).ToList(),
            tags = album.Tags?.Tag.Select(t => t.Name).ToList(),
            responseTimeMs = stopwatch?.ElapsedMilliseconds
        };

        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
    }

    private class TrackPlayInfo
    {
        public string Name { get; set; } = string.Empty;
        public int UserPlaycount { get; set; }
    }
}