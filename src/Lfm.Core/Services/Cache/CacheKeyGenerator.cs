using System.Security.Cryptography;
using System.Text;

namespace Lfm.Core.Services.Cache;

/// <summary>
/// Generates consistent, collision-resistant cache keys from API parameters.
/// </summary>
public interface ICacheKeyGenerator
{
    /// <summary>
    /// Generates a cache key from API call parameters.
    /// </summary>
    /// <param name="method">Last.fm API method (e.g., "user.getTopTracks")</param>
    /// <param name="user">Username</param>
    /// <param name="period">Time period (e.g., "overall", "7day")</param>
    /// <param name="limit">Items per page limit</param>
    /// <param name="page">Page number</param>
    /// <returns>SHA256-based cache key</returns>
    string GenerateKey(string method, string user, string period, int limit, int page);

    /// <summary>
    /// Generates a cache key from a raw parameter string.
    /// </summary>
    /// <param name="parameters">Raw parameter string</param>
    /// <returns>SHA256-based cache key</returns>
    string GenerateKey(string parameters);
}

public class CacheKeyGenerator : ICacheKeyGenerator
{
    public string GenerateKey(string method, string user, string period, int limit, int page)
    {
        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method cannot be null or empty", nameof(method));
        
        if (string.IsNullOrWhiteSpace(user))
            throw new ArgumentException("User cannot be null or empty", nameof(user));

        if (string.IsNullOrWhiteSpace(period))
            throw new ArgumentException("Period cannot be null or empty", nameof(period));

        // Create normalized parameter string
        var parameters = $"{method.ToLowerInvariant()}|{user.ToLowerInvariant()}|{period.ToLowerInvariant()}|{limit}|{page}";
        
        return GenerateKey(parameters);
    }

    public string GenerateKey(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
            throw new ArgumentException("Parameters cannot be null or empty", nameof(parameters));

        // Generate SHA256 hash
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(parameters));
        
        // Convert to hex string (lowercase for consistency)
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        return hashHex;
    }
}

/// <summary>
/// Extension methods for creating cache keys from common API call patterns.
/// </summary>
public static class CacheKeyExtensions
{
    /// <summary>
    /// Creates a cache key for user.getTopTracks API calls.
    /// </summary>
    public static string ForTopTracks(this ICacheKeyGenerator generator, string user, string period, int limit, int page)
        => generator.GenerateKey("user.getTopTracks", user, period, limit, page);

    /// <summary>
    /// Creates a cache key for user.getTopArtists API calls.
    /// </summary>
    public static string ForTopArtists(this ICacheKeyGenerator generator, string user, string period, int limit, int page)
        => generator.GenerateKey("user.getTopArtists", user, period, limit, page);

    /// <summary>
    /// Creates a cache key for user.getTopAlbums API calls.
    /// </summary>
    public static string ForTopAlbums(this ICacheKeyGenerator generator, string user, string period, int limit, int page)
        => generator.GenerateKey("user.getTopAlbums", user, period, limit, page);

    /// <summary>
    /// Creates a cache key for artist.getTopTracks API calls.
    /// </summary>
    public static string ForArtistTopTracks(this ICacheKeyGenerator generator, string artist, int limit)
        => generator.GenerateKey("artist.getTopTracks", artist, "n/a", limit, 1);

    /// <summary>
    /// Creates a cache key for artist.getTopAlbums API calls.
    /// </summary>
    public static string ForArtistTopAlbums(this ICacheKeyGenerator generator, string artist, int limit)
        => generator.GenerateKey("artist.getTopAlbums", artist, "n/a", limit, 1);

    /// <summary>
    /// Creates a cache key for artist.getSimilar API calls.
    /// </summary>
    public static string ForSimilarArtists(this ICacheKeyGenerator generator, string artist, int limit)
        => generator.GenerateKey("artist.getSimilar", artist, "n/a", limit, 1);
}