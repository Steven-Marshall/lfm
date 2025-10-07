using System.Text.Json;
using System.Web;
using Lfm.Core.Models;
using Lfm.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace Lfm.Core.Services;

public interface ILastFmApiClient
{
    // Legacy nullable methods (maintained for compatibility)
    Task<TopArtists?> GetTopArtistsAsync(string username, string period = "overall", int limit = 10, int page = 1);
    Task<TopTracks?> GetTopTracksAsync(string username, string period = "overall", int limit = 10, int page = 1);
    Task<TopAlbums?> GetTopAlbumsAsync(string username, string period = "overall", int limit = 10, int page = 1);
    Task<TopTracks?> GetArtistTopTracksAsync(string artist, int limit = 10);
    Task<TopAlbums?> GetArtistTopAlbumsAsync(string artist, int limit = 10);
    Task<SimilarArtists?> GetSimilarArtistsAsync(string artist, int limit = 50);
    Task<TopTags?> GetArtistTopTagsAsync(string artist, bool autocorrect = true);
    
    // Date range methods
    Task<RecentTracks?> GetRecentTracksAsync(string username, DateTime from, DateTime to, int limit = 200, int page = 1);
    Task<TopArtists?> GetTopArtistsForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10);
    Task<TopTracks?> GetTopTracksForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10);
    Task<TopAlbums?> GetTopAlbumsForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10);
    
    // New Result-based methods for better error handling
    Task<Result<TopArtists>> GetTopArtistsWithResultAsync(string username, string period = "overall", int limit = 10, int page = 1);
    Task<Result<TopTracks>> GetTopTracksWithResultAsync(string username, string period = "overall", int limit = 10, int page = 1);
    Task<Result<TopAlbums>> GetTopAlbumsWithResultAsync(string username, string period = "overall", int limit = 10, int page = 1);
    Task<Result<TopTracks>> GetArtistTopTracksWithResultAsync(string artist, int limit = 10);
    Task<Result<TopAlbums>> GetArtistTopAlbumsWithResultAsync(string artist, int limit = 10);
    Task<Result<SimilarArtists>> GetSimilarArtistsWithResultAsync(string artist, int limit = 50);
    Task<Result<TopTags>> GetArtistTopTagsWithResultAsync(string artist, bool autocorrect = true);
    
    // Result-based date range methods
    Task<Result<RecentTracks>> GetRecentTracksWithResultAsync(string username, DateTime from, DateTime to, int limit = 200, int page = 1);
    Task<Result<TopArtists>> GetTopArtistsForDateRangeWithResultAsync(string username, DateTime from, DateTime to, int limit = 10);
    Task<Result<TopTracks>> GetTopTracksForDateRangeWithResultAsync(string username, DateTime from, DateTime to, int limit = 10);
    Task<Result<TopAlbums>> GetTopAlbumsForDateRangeWithResultAsync(string username, DateTime from, DateTime to, int limit = 10);

    // Lookup methods for checking user's listening history
    Task<ArtistLookupInfo?> GetArtistInfoAsync(string artist, string username);
    Task<TrackLookupInfo?> GetTrackInfoAsync(string artist, string track, string username);
    Task<AlbumLookupInfo?> GetAlbumInfoAsync(string artist, string album, string username);
    Task<Result<ArtistLookupInfo>> GetArtistInfoWithResultAsync(string artist, string username);
    Task<Result<TrackLookupInfo>> GetTrackInfoWithResultAsync(string artist, string track, string username);
    Task<Result<AlbumLookupInfo>> GetAlbumInfoWithResultAsync(string artist, string album, string username);
}

public class LastFmApiClient : ILastFmApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LastFmApiClient> _logger;
    private readonly string _apiKey;
    private readonly int _apiThrottleMs;
    private readonly bool _enableDebugLogging;
    private const string BaseUrl = "https://ws.audioscrobbler.com/2.0/";

    // Timing properties for last API call (used by CachedLastFmApiClient for detailed breakdown)
    public long LastHttpMs { get; private set; }
    public long LastJsonReadMs { get; private set; }
    public long LastJsonParseMs { get; private set; }

    public LastFmApiClient(HttpClient httpClient, ILogger<LastFmApiClient> logger, string apiKey, int apiThrottleMs = 100, bool enableDebugLogging = false)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _apiThrottleMs = apiThrottleMs;
        _enableDebugLogging = enableDebugLogging;
    }

    public async Task<TopArtists?> GetTopArtistsAsync(string username, string period = "overall", int limit = 10, int page = 1)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["method"] = "user.getTopArtists",
                ["user"] = username,
                ["period"] = period,
                ["limit"] = limit.ToString(),
                ["page"] = page.ToString(),
                ["api_key"] = _apiKey,
                ["format"] = "json"
            };

            var response = await MakeRequestAsync(parameters);
            if (response == null) return null;

            var jsonParseStopwatch = System.Diagnostics.Stopwatch.StartNew();
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out _))
            {
                jsonParseStopwatch.Stop();
                LastJsonParseMs = jsonParseStopwatch.ElapsedMilliseconds;
                var error = root.GetProperty("error").GetInt32();
                var message = root.GetProperty("message").GetString();
                _logger.LogError("Last.fm API error {Error}: {Message}", error, message);
                return null;
            }

            if (root.TryGetProperty("topartists", out var topArtistsElement))
            {
                var topArtists = JsonSerializer.Deserialize<TopArtists>(topArtistsElement.GetRawText(), GetJsonOptions());
                jsonParseStopwatch.Stop();
                LastJsonParseMs = jsonParseStopwatch.ElapsedMilliseconds;
                return topArtists;
            }

            jsonParseStopwatch.Stop();
            LastJsonParseMs = jsonParseStopwatch.ElapsedMilliseconds;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top artists for user {Username}", username);
            return null;
        }
    }

    public async Task<TopTracks?> GetTopTracksAsync(string username, string period = "overall", int limit = 10, int page = 1)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["method"] = "user.getTopTracks",
                ["user"] = username,
                ["period"] = period,
                ["limit"] = limit.ToString(),
                ["page"] = page.ToString(),
                ["api_key"] = _apiKey,
                ["format"] = "json"
            };

            var response = await MakeRequestAsync(parameters);
            if (response == null) return null;

            var jsonParseStopwatch = System.Diagnostics.Stopwatch.StartNew();
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out _))
            {
                jsonParseStopwatch.Stop();
                LastJsonParseMs = jsonParseStopwatch.ElapsedMilliseconds;
                var error = root.GetProperty("error").GetInt32();
                var message = root.GetProperty("message").GetString();
                _logger.LogError("Last.fm API error {Error}: {Message}", error, message);
                return null;
            }

            if (root.TryGetProperty("toptracks", out var topTracksElement))
            {
                var topTracks = JsonSerializer.Deserialize<TopTracks>(topTracksElement.GetRawText(), GetJsonOptions());
                jsonParseStopwatch.Stop();
                LastJsonParseMs = jsonParseStopwatch.ElapsedMilliseconds;
                return topTracks;
            }

            jsonParseStopwatch.Stop();
            LastJsonParseMs = jsonParseStopwatch.ElapsedMilliseconds;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top tracks for user {Username}", username);
            return null;
        }
    }

    public async Task<TopAlbums?> GetTopAlbumsAsync(string username, string period = "overall", int limit = 10, int page = 1)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["method"] = "user.getTopAlbums",
                ["user"] = username,
                ["period"] = period,
                ["limit"] = limit.ToString(),
                ["page"] = page.ToString(),
                ["api_key"] = _apiKey,
                ["format"] = "json"
            };

            var response = await MakeRequestAsync(parameters);
            if (response == null) return null;

            var jsonParseStopwatch = System.Diagnostics.Stopwatch.StartNew();
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out _))
            {
                jsonParseStopwatch.Stop();
                LastJsonParseMs = jsonParseStopwatch.ElapsedMilliseconds;
                var error = root.GetProperty("error").GetInt32();
                var message = root.GetProperty("message").GetString();
                _logger.LogError("Last.fm API error {Error}: {Message}", error, message);
                return null;
            }

            if (root.TryGetProperty("topalbums", out var topAlbumsElement))
            {
                var topAlbums = JsonSerializer.Deserialize<TopAlbums>(topAlbumsElement.GetRawText(), GetJsonOptions());
                jsonParseStopwatch.Stop();
                LastJsonParseMs = jsonParseStopwatch.ElapsedMilliseconds;
                return topAlbums;
            }

            jsonParseStopwatch.Stop();
            LastJsonParseMs = jsonParseStopwatch.ElapsedMilliseconds;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top albums for user {Username}", username);
            return null;
        }
    }

    public async Task<TopTracks?> GetArtistTopTracksAsync(string artist, int limit = 10)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["method"] = "artist.getTopTracks",
                ["artist"] = artist,
                ["limit"] = limit.ToString(),
                ["autocorrect"] = "1",
                ["api_key"] = _apiKey,
                ["format"] = "json"
            };

            var response = await MakeRequestAsync(parameters);
            if (response == null) return null;

            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out _))
            {
                var error = root.GetProperty("error").GetInt32();
                var message = root.GetProperty("message").GetString();
                _logger.LogError("Last.fm API error {Error}: {Message}", error, message);
                return null;
            }

            if (root.TryGetProperty("toptracks", out var topTracksElement))
            {
                var topTracks = JsonSerializer.Deserialize<TopTracks>(topTracksElement.GetRawText(), GetJsonOptions());
                return topTracks;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top tracks for artist {Artist}", artist);
            return null;
        }
    }

    public async Task<TopAlbums?> GetArtistTopAlbumsAsync(string artist, int limit = 10)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["method"] = "artist.getTopAlbums",
                ["artist"] = artist,
                ["limit"] = limit.ToString(),
                ["autocorrect"] = "1",
                ["api_key"] = _apiKey,
                ["format"] = "json"
            };

            var response = await MakeRequestAsync(parameters);
            if (response == null) return null;

            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out _))
            {
                var error = root.GetProperty("error").GetInt32();
                var message = root.GetProperty("message").GetString();
                _logger.LogError("Last.fm API error {Error}: {Message}", error, message);
                return null;
            }

            if (root.TryGetProperty("topalbums", out var topAlbumsElement))
            {
                var topAlbums = JsonSerializer.Deserialize<TopAlbums>(topAlbumsElement.GetRawText(), GetJsonOptions());
                return topAlbums;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top albums for artist {Artist}", artist);
            return null;
        }
    }

    public async Task<SimilarArtists?> GetSimilarArtistsAsync(string artist, int limit = 50)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["method"] = "artist.getSimilar",
                ["artist"] = artist,
                ["limit"] = limit.ToString(),
                ["autocorrect"] = "1",
                ["api_key"] = _apiKey,
                ["format"] = "json"
            };

            var response = await MakeRequestAsync(parameters);
            if (response == null) return null;

            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out _))
            {
                var error = root.GetProperty("error").GetInt32();
                var message = root.GetProperty("message").GetString();
                _logger.LogError("Last.fm API error {Error}: {Message}", error, message);
                return null;
            }

            if (root.TryGetProperty("similarartists", out var similarArtistsElement))
            {
                var similarArtists = JsonSerializer.Deserialize<SimilarArtists>(similarArtistsElement.GetRawText(), GetJsonOptions());
                return similarArtists;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting similar artists for {Artist}", artist);
            return null;
        }
    }

    public async Task<TopTags?> GetArtistTopTagsAsync(string artist, bool autocorrect = true)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["method"] = "artist.getTopTags",
                ["artist"] = artist,
                ["autocorrect"] = autocorrect ? "1" : "0",
                ["api_key"] = _apiKey,
                ["format"] = "json"
            };

            var response = await MakeRequestAsync(parameters);
            if (response == null) return null;

            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out _))
            {
                var error = root.GetProperty("error").GetInt32();
                var message = root.GetProperty("message").GetString();
                _logger.LogError("Last.fm API error {Error}: {Message}", error, message);
                return null;
            }

            if (root.TryGetProperty("toptags", out var topTagsElement))
            {
                var topTags = JsonSerializer.Deserialize<TopTags>(topTagsElement.GetRawText(), GetJsonOptions());
                return topTags;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top tags for {Artist}", artist);
            return null;
        }
    }

    public async Task<RecentTracks?> GetRecentTracksAsync(string username, DateTime from, DateTime to, int limit = 200, int page = 1)
    {
        try
        {
            var fromTimestamp = DateRangeParser.ToUnixTimestamp(from);
            var toTimestamp = DateRangeParser.ToUnixTimestamp(to);
            
            var parameters = new Dictionary<string, string>
            {
                ["method"] = "user.getRecentTracks",
                ["user"] = username,
                ["from"] = fromTimestamp.ToString(),
                ["to"] = toTimestamp.ToString(),
                ["limit"] = limit.ToString(),
                ["page"] = page.ToString(),
                ["api_key"] = _apiKey,
                ["format"] = "json"
            };

            _logger.LogDebug("Getting recent tracks for {User} from {FromTs} to {ToTs} (page {Page})", 
                username, fromTimestamp, toTimestamp, page);

            var response = await MakeRequestAsync(parameters);
            if (response == null) return null;

            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out _))
            {
                var error = root.GetProperty("error").GetInt32();
                var message = root.GetProperty("message").GetString();
                _logger.LogError("Last.fm API error {Error}: {Message}", error, message);
                return null;
            }

            if (root.TryGetProperty("recenttracks", out var recentTracksElement))
            {
                var recentTracks = JsonSerializer.Deserialize<RecentTracks>(recentTracksElement.GetRawText(), GetJsonOptions());
                return recentTracks;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent tracks for user {Username} from {From} to {To}", 
                username, from, to);
            return null;
        }
    }

    public async Task<TopArtists?> GetTopArtistsForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        try
        {
            _logger.LogInformation("Aggregating artist data from {FromTo}", 
                DateRangeParser.FormatDateRange(from, to));

            var artistCounts = new Dictionary<string, ArtistAggregation>(StringComparer.OrdinalIgnoreCase);
            var pageSize = 1000; // Max allowed by Last.fm API
            var page = 1;
            var hasMore = true;
            var totalProcessed = 0;

            while (hasMore)
            {
                // Apply throttling for subsequent API calls to avoid overwhelming Last.fm
                if (page > 1 && _apiThrottleMs > 0)
                {
                    await Task.Delay(_apiThrottleMs);
                }

                var recentTracks = await GetRecentTracksAsync(username, from, to, pageSize, page);
                
                if (recentTracks?.Tracks == null || !recentTracks.Tracks.Any())
                {
                    _logger.LogDebug("No recent tracks returned for page {Page}", page);
                    break;
                }

                _logger.LogDebug("Got {TrackCount} tracks for page {Page}", recentTracks.Tracks.Count, page);

                // Filter out "now playing" tracks and aggregate
                var validTracks = recentTracks.Tracks
                    .Where(t => t.Attributes?.NowPlaying == null)
                    .ToList();

                _logger.LogDebug("After filtering: {ValidTrackCount} valid tracks", validTracks.Count);

                foreach (var track in validTracks)
                {
                    var artistName = track.Artist.Name;
                    if (string.IsNullOrWhiteSpace(artistName)) continue;

                    if (artistCounts.TryGetValue(artistName, out var existing))
                    {
                        existing.PlayCount++;
                    }
                    else
                    {
                        artistCounts[artistName] = new ArtistAggregation
                        {
                            Name = artistName,
                            PlayCount = 1,
                            Url = track.Artist.Url,
                            Mbid = track.Artist.Mbid
                        };
                    }
                }

                totalProcessed += validTracks.Count;
                var totalPages = int.TryParse(recentTracks.Attributes.TotalPages, out var tp) ? tp : 1;
                hasMore = page < totalPages;
                page++;

                _logger.LogDebug("Processed page {Page} of {TotalPages} ({TracksProcessed} tracks so far)", 
                    page - 1, totalPages, totalProcessed);
            }

            // Convert to Artist objects and sort by play count
            var topArtists = artistCounts.Values
                .OrderByDescending(a => a.PlayCount)
                .Take(limit)
                .Select((a, index) => new Artist
                {
                    Name = a.Name,
                    PlayCount = a.PlayCount.ToString(),
                    Url = a.Url,
                    Mbid = a.Mbid,
                    Attributes = new ArtistAttributes { Rank = (index + 1).ToString() }
                })
                .ToList();

            _logger.LogInformation("Aggregated {TotalTracks} tracks into {UniqueArtists} unique artists, returning top {Limit}", 
                totalProcessed, artistCounts.Count, limit);

            return new TopArtists
            {
                Artists = topArtists,
                Attributes = new TopArtistsAttributes
                {
                    User = username,
                    Total = artistCounts.Count.ToString(),
                    Page = "1",
                    PerPage = limit.ToString(),
                    TotalPages = "1"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top artists for date range {From} to {To}", from, to);
            return null;
        }
    }

    public async Task<TopTracks?> GetTopTracksForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        try
        {
            _logger.LogInformation("Aggregating track data from {FromTo}", 
                DateRangeParser.FormatDateRange(from, to));

            var trackCounts = new Dictionary<string, TrackAggregation>(StringComparer.OrdinalIgnoreCase);
            var pageSize = 1000;
            var page = 1;
            var hasMore = true;
            var totalProcessed = 0;

            while (hasMore)
            {
                // Apply throttling for subsequent API calls to avoid overwhelming Last.fm
                if (page > 1 && _apiThrottleMs > 0)
                {
                    await Task.Delay(_apiThrottleMs);
                }

                var recentTracks = await GetRecentTracksAsync(username, from, to, pageSize, page);
                
                if (recentTracks?.Tracks == null || !recentTracks.Tracks.Any())
                    break;

                var validTracks = recentTracks.Tracks
                    .Where(t => t.Attributes?.NowPlaying == null)
                    .ToList();

                foreach (var track in validTracks)
                {
                    var trackKey = $"{track.Name}|{track.Artist.Name}";
                    if (string.IsNullOrWhiteSpace(track.Name) || string.IsNullOrWhiteSpace(track.Artist.Name)) 
                        continue;

                    if (trackCounts.TryGetValue(trackKey, out var existing))
                    {
                        existing.PlayCount++;
                    }
                    else
                    {
                        trackCounts[trackKey] = new TrackAggregation
                        {
                            Name = track.Name,
                            PlayCount = 1,
                            Url = track.Url,
                            Mbid = track.Mbid,
                            Artist = new ArtistInfo 
                            { 
                                Name = track.Artist.Name, 
                                Mbid = track.Artist.Mbid, 
                                Url = track.Artist.Url 
                            }
                        };
                    }
                }

                totalProcessed += validTracks.Count;
                var totalPages = int.TryParse(recentTracks.Attributes.TotalPages, out var tp) ? tp : 1;
                hasMore = page < totalPages;
                page++;
            }

            var topTracks = trackCounts.Values
                .OrderByDescending(t => t.PlayCount)
                .Take(limit)
                .Select((t, index) => new Track
                {
                    Name = t.Name,
                    PlayCount = t.PlayCount.ToString(),
                    Url = t.Url,
                    Mbid = t.Mbid,
                    Artist = t.Artist,
                    Attributes = new TrackAttributes { Rank = (index + 1).ToString() }
                })
                .ToList();

            return new TopTracks
            {
                Tracks = topTracks,
                Attributes = new TopTracksAttributes
                {
                    User = username,
                    Total = trackCounts.Count.ToString(),
                    Page = "1",
                    PerPage = limit.ToString(),
                    TotalPages = "1"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top tracks for date range {From} to {To}", from, to);
            return null;
        }
    }

    public async Task<TopAlbums?> GetTopAlbumsForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        try
        {
            _logger.LogInformation("Aggregating album data from {FromTo}", 
                DateRangeParser.FormatDateRange(from, to));

            var albumCounts = new Dictionary<string, AlbumAggregation>(StringComparer.OrdinalIgnoreCase);
            var pageSize = 1000;
            var page = 1;
            var hasMore = true;
            var totalProcessed = 0;
            var tracksWithAlbumData = 0;

            while (hasMore)
            {
                // Apply throttling for subsequent API calls to avoid overwhelming Last.fm
                if (page > 1 && _apiThrottleMs > 0)
                {
                    await Task.Delay(_apiThrottleMs);
                }

                var recentTracks = await GetRecentTracksAsync(username, from, to, pageSize, page);
                
                if (recentTracks?.Tracks == null || !recentTracks.Tracks.Any())
                    break;

                var allValidTracks = recentTracks.Tracks
                    .Where(t => t.Attributes?.NowPlaying == null)
                    .ToList();
                
                var tracksWithAlbums = allValidTracks
                    .Where(t => t.Album != null && !string.IsNullOrWhiteSpace(t.Album.Name))
                    .ToList();

                totalProcessed += allValidTracks.Count;
                tracksWithAlbumData += tracksWithAlbums.Count;

                _logger.LogDebug("Got {TrackCount} tracks for page {Page}, {AlbumTracks} with album data", 
                    allValidTracks.Count, page, tracksWithAlbums.Count);

                foreach (var track in tracksWithAlbums)
                {
                    var albumKey = $"{track.Album.Name}|{track.Artist.Name}";

                    if (albumCounts.TryGetValue(albumKey, out var existing))
                    {
                        existing.PlayCount++;
                    }
                    else
                    {
                        albumCounts[albumKey] = new AlbumAggregation
                        {
                            Name = track.Album.Name,
                            PlayCount = 1,
                            Mbid = track.Album.Mbid,
                            Artist = new ArtistInfo 
                            { 
                                Name = track.Artist.Name, 
                                Mbid = track.Artist.Mbid, 
                                Url = track.Artist.Url 
                            }
                        };
                    }
                }

                var totalPages = int.TryParse(recentTracks.Attributes.TotalPages, out var tp) ? tp : 1;
                hasMore = page < totalPages;
                page++;
            }

            var topAlbums = albumCounts.Values
                .OrderByDescending(a => a.PlayCount)
                .Take(limit)
                .Select((a, index) => new Album
                {
                    Name = a.Name,
                    PlayCount = a.PlayCount.ToString(),
                    Mbid = a.Mbid,
                    Artist = a.Artist,
                    Attributes = new AlbumAttributes { Rank = (index + 1).ToString() }
                })
                .ToList();

            _logger.LogInformation("Aggregated {TotalTracks} tracks into {UniqueAlbums} unique albums (from {TracksWithAlbumData} tracks with album data)", 
                totalProcessed, albumCounts.Count, tracksWithAlbumData);

            return new TopAlbums
            {
                Albums = topAlbums,
                Attributes = new TopAlbumsAttributes
                {
                    User = username,
                    Total = albumCounts.Count.ToString(),
                    Page = "1",
                    PerPage = limit.ToString(),
                    TotalPages = "1"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top albums for date range {From} to {To}", from, to);
            return null;
        }
    }

    private async Task<string?> MakeRequestAsync(Dictionary<string, string> parameters)
    {
        var query = string.Join("&", parameters.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));
        var url = $"{BaseUrl}?{query}";
        var maskedUrl = url.Replace(_apiKey, "***");

        // Debug logging - detailed request information
        if (_enableDebugLogging)
        {
            _logger.LogInformation("API DEBUG - Request Details:");
            _logger.LogInformation("  Method: {Method}", parameters.GetValueOrDefault("method", "unknown"));
            _logger.LogInformation("  Artist: {Artist}", parameters.GetValueOrDefault("artist", "n/a"));
            _logger.LogInformation("  Username: {Username}", parameters.GetValueOrDefault("user", "n/a"));
            _logger.LogInformation("  Period: {Period}", parameters.GetValueOrDefault("period", "n/a"));
            _logger.LogInformation("  Autocorrect: {Autocorrect}", parameters.GetValueOrDefault("autocorrect", "n/a"));
            _logger.LogInformation("  Full URL: {Url}", maskedUrl);
        }

        _logger.LogDebug("Making request to: {Url}", maskedUrl);

        var httpStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var response = await _httpClient.GetAsync(url);
            httpStopwatch.Stop();
            var httpMs = httpStopwatch.ElapsedMilliseconds;

            // Debug logging - response information
            if (_enableDebugLogging)
            {
                _logger.LogInformation("API DEBUG - Response Details:");
                _logger.LogInformation("  Status: {StatusCode} {StatusText}", (int)response.StatusCode, response.StatusCode);
                _logger.LogInformation("  HTTP Time: {ElapsedMs}ms", httpMs);

                // Log response headers if debug is enabled
                foreach (var header in response.Headers)
                {
                    _logger.LogDebug("  Header {HeaderName}: {HeaderValue}", header.Key, string.Join(", ", header.Value));
                }
                foreach (var header in response.Content.Headers)
                {
                    _logger.LogDebug("  Content-Header {HeaderName}: {HeaderValue}", header.Key, string.Join(", ", header.Value));
                }
            }

            response.EnsureSuccessStatusCode();

            var jsonReadStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var content = await response.Content.ReadAsStringAsync();
            jsonReadStopwatch.Stop();

            // Store timing for CachedLastFmApiClient to use
            LastHttpMs = httpMs;
            LastJsonReadMs = jsonReadStopwatch.ElapsedMilliseconds;

            if (_enableDebugLogging)
            {
                _logger.LogInformation("API DEBUG - Content Length: {ContentLength} characters", content?.Length ?? 0);
                _logger.LogInformation("  JSON Read Time: {ElapsedMs}ms", jsonReadStopwatch.ElapsedMilliseconds);

                // Log first 200 characters of response for debugging (avoid huge logs)
                if (!string.IsNullOrEmpty(content))
                {
                    var preview = content.Length > 200 ? content.Substring(0, 200) + "..." : content;
                    _logger.LogDebug("API DEBUG - Response Preview: {Preview}", preview);
                }
            }

            return content;
        }
        catch (HttpRequestException ex)
        {
            httpStopwatch.Stop();
            _logger.LogError(ex, "HTTP error making request to Last.fm API (took {ElapsedMs}ms)", httpStopwatch.ElapsedMilliseconds);

            if (_enableDebugLogging)
            {
                _logger.LogInformation("API DEBUG - HTTP Error Details:");
                _logger.LogInformation("  Request: {Method} for {Artist}",
                    parameters.GetValueOrDefault("method", "unknown"),
                    parameters.GetValueOrDefault("artist", parameters.GetValueOrDefault("user", "n/a")));
                _logger.LogInformation("  URL: {Url}", maskedUrl);
                _logger.LogInformation("  Error: {ErrorMessage}", ex.Message);
            }

            return null;
        }
    }

    // New Result-based methods for better error handling
    public async Task<Result<TopArtists>> GetTopArtistsWithResultAsync(string username, string period = "overall", int limit = 10, int page = 1)
    {
        return await ExecuteWithResultAsync(
            () => GetTopArtistsAsync(username, period, limit, page),
            $"getting top artists for user {username}");
    }

    public async Task<Result<TopTracks>> GetTopTracksWithResultAsync(string username, string period = "overall", int limit = 10, int page = 1)
    {
        return await ExecuteWithResultAsync(
            () => GetTopTracksAsync(username, period, limit, page),
            $"getting top tracks for user {username}");
    }

    public async Task<Result<TopAlbums>> GetTopAlbumsWithResultAsync(string username, string period = "overall", int limit = 10, int page = 1)
    {
        return await ExecuteWithResultAsync(
            () => GetTopAlbumsAsync(username, period, limit, page),
            $"getting top albums for user {username}");
    }

    public async Task<Result<TopTracks>> GetArtistTopTracksWithResultAsync(string artist, int limit = 10)
    {
        return await ExecuteWithResultAsync(
            () => GetArtistTopTracksAsync(artist, limit),
            $"getting top tracks for artist {artist}");
    }

    public async Task<Result<TopAlbums>> GetArtistTopAlbumsWithResultAsync(string artist, int limit = 10)
    {
        return await ExecuteWithResultAsync(
            () => GetArtistTopAlbumsAsync(artist, limit),
            $"getting top albums for artist {artist}");
    }

    public async Task<Result<SimilarArtists>> GetSimilarArtistsWithResultAsync(string artist, int limit = 50)
    {
        return await ExecuteWithResultAsync(
            () => GetSimilarArtistsAsync(artist, limit),
            $"getting similar artists for {artist}");
    }

    public async Task<Result<TopTags>> GetArtistTopTagsWithResultAsync(string artist, bool autocorrect = true)
    {
        return await ExecuteWithResultAsync(
            () => GetArtistTopTagsAsync(artist, autocorrect),
            $"getting top tags for {artist}");
    }

    public async Task<Result<RecentTracks>> GetRecentTracksWithResultAsync(string username, DateTime from, DateTime to, int limit = 200, int page = 1)
    {
        return await ExecuteWithResultAsync(
            () => GetRecentTracksAsync(username, from, to, limit, page),
            $"getting recent tracks for user {username}");
    }

    public async Task<Result<TopArtists>> GetTopArtistsForDateRangeWithResultAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        return await ExecuteWithResultAsync(
            () => GetTopArtistsForDateRangeAsync(username, from, to, limit),
            $"getting top artists for user {username} date range");
    }

    public async Task<Result<TopTracks>> GetTopTracksForDateRangeWithResultAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        return await ExecuteWithResultAsync(
            () => GetTopTracksForDateRangeAsync(username, from, to, limit),
            $"getting top tracks for user {username} date range");
    }

    public async Task<Result<TopAlbums>> GetTopAlbumsForDateRangeWithResultAsync(string username, DateTime from, DateTime to, int limit = 10)
    {
        return await ExecuteWithResultAsync(
            () => GetTopAlbumsForDateRangeAsync(username, from, to, limit),
            $"getting top albums for user {username} date range");
    }

    // Lookup methods implementation
    public async Task<ArtistLookupInfo?> GetArtistInfoAsync(string artist, string username)
    {
        var parameters = new Dictionary<string, string>
        {
            ["method"] = "artist.getInfo",
            ["artist"] = artist,
            ["api_key"] = _apiKey,
            ["format"] = "json",
            ["autocorrect"] = "1"
        };

        // Only add username if provided (it's optional for artist.getInfo)
        if (!string.IsNullOrEmpty(username))
        {
            parameters["username"] = username;
        }

        var response = await MakeRequestAsync(parameters);
        if (response == null) return null;

        try
        {
            return JsonSerializer.Deserialize<ArtistLookupInfo>(response, GetJsonOptions());
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize artist info response");
            return null;
        }
    }

    public async Task<TrackLookupInfo?> GetTrackInfoAsync(string artist, string track, string username)
    {
        var parameters = new Dictionary<string, string>
        {
            ["method"] = "track.getInfo",
            ["artist"] = artist,
            ["track"] = track,
            ["api_key"] = _apiKey,
            ["format"] = "json",
            ["autocorrect"] = "1"
        };

        // Only add username if provided (it's optional for track.getInfo)
        if (!string.IsNullOrEmpty(username))
        {
            parameters["username"] = username;
        }

        var response = await MakeRequestAsync(parameters);
        if (response == null) return null;

        try
        {
            return JsonSerializer.Deserialize<TrackLookupInfo>(response, GetJsonOptions());
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize track info response");
            return null;
        }
    }

    public async Task<AlbumLookupInfo?> GetAlbumInfoAsync(string artist, string album, string username)
    {
        var parameters = new Dictionary<string, string>
        {
            ["method"] = "album.getInfo",
            ["artist"] = artist,
            ["album"] = album,
            ["api_key"] = _apiKey,
            ["format"] = "json",
            ["autocorrect"] = "1"
        };

        // Only add username if provided (it's optional for album.getInfo)
        if (!string.IsNullOrEmpty(username))
        {
            parameters["username"] = username;
        }

        var response = await MakeRequestAsync(parameters);
        if (response == null) return null;

        try
        {
            return JsonSerializer.Deserialize<AlbumLookupInfo>(response, GetJsonOptions());
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize album info response");
            return null;
        }
    }

    public async Task<Result<ArtistLookupInfo>> GetArtistInfoWithResultAsync(string artist, string username)
    {
        return await ExecuteWithResultAsync(
            () => GetArtistInfoAsync(artist, username),
            $"getting artist info for {artist}");
    }

    public async Task<Result<TrackLookupInfo>> GetTrackInfoWithResultAsync(string artist, string track, string username)
    {
        return await ExecuteWithResultAsync(
            () => GetTrackInfoAsync(artist, track, username),
            $"getting track info for {artist} - {track}");
    }

    public async Task<Result<AlbumLookupInfo>> GetAlbumInfoWithResultAsync(string artist, string album, string username)
    {
        return await ExecuteWithResultAsync(
            () => GetAlbumInfoAsync(artist, album, username),
            $"getting album info for {artist} - {album}");
    }

    private async Task<Result<T>> ExecuteWithResultAsync<T>(Func<Task<T?>> operation, string operationDescription) where T : class
    {
        try
        {
            var result = await operation();
            if (result == null)
            {
                return Result<T>.Fail(new ErrorResult(
                    ErrorType.ApiError,
                    $"Failed {operationDescription}",
                    "The Last.fm API returned no data or encountered an error"));
            }
            return Result<T>.Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return Result<T>.Fail(new ErrorResult(
                ErrorType.NetworkError,
                $"Network error while {operationDescription}",
                ex.Message));
        }
        catch (JsonException ex)
        {
            return Result<T>.Fail(new ErrorResult(
                ErrorType.DataError,
                $"Invalid response format while {operationDescription}",
                ex.Message));
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(new ErrorResult(
                ErrorType.UnknownError,
                $"Unexpected error while {operationDescription}",
                ex.Message));
        }
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}