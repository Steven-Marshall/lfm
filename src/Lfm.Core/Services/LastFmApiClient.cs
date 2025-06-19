using System.Text.Json;
using System.Web;
using Lfm.Core.Models;
using Microsoft.Extensions.Logging;

namespace Lfm.Core.Services;

public interface ILastFmApiClient
{
    Task<TopArtists?> GetTopArtistsAsync(string username, string period = "overall", int limit = 10, int page = 1);
    Task<TopTracks?> GetTopTracksAsync(string username, string period = "overall", int limit = 10, int page = 1);
    Task<TopAlbums?> GetTopAlbumsAsync(string username, string period = "overall", int limit = 10, int page = 1);
    Task<TopTracks?> GetArtistTopTracksAsync(string artist, int limit = 10);
    Task<TopAlbums?> GetArtistTopAlbumsAsync(string artist, int limit = 10);
}

public class LastFmApiClient : ILastFmApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LastFmApiClient> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://ws.audioscrobbler.com/2.0/";

    public LastFmApiClient(HttpClient httpClient, ILogger<LastFmApiClient> logger, string apiKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
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

            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out _))
            {
                var error = root.GetProperty("error").GetInt32();
                var message = root.GetProperty("message").GetString();
                _logger.LogError("Last.fm API error {Error}: {Message}", error, message);
                return null;
            }

            if (root.TryGetProperty("topartists", out var topArtistsElement))
            {
                var topArtists = JsonSerializer.Deserialize<TopArtists>(topArtistsElement.GetRawText(), GetJsonOptions());
                return topArtists;
            }

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

    private async Task<string?> MakeRequestAsync(Dictionary<string, string> parameters)
    {
        var query = string.Join("&", parameters.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));
        var url = $"{BaseUrl}?{query}";
        
        _logger.LogDebug("Making request to: {Url}", url.Replace(_apiKey, "***"));

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error making request to Last.fm API");
            return null;
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