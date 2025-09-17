using Lfm.Core.Models;
using Lfm.Core.Models.Results;

namespace Lfm.Core.Services;

/// <summary>
/// Service layer interface for Last.fm business logic operations.
/// Abstracts complex business operations from CLI command implementation details.
/// </summary>
public interface ILastFmService
{
    // Basic user content retrieval
    /// <summary>
    /// Gets user's top artists for a specified period
    /// </summary>
    Task<TopArtists?> GetUserTopArtistsAsync(string username, string period, int limit = 10, int page = 1);
    
    /// <summary>
    /// Gets user's top tracks for a specified period
    /// </summary>
    Task<TopTracks?> GetUserTopTracksAsync(string username, string period, int limit = 10, int page = 1);
    
    /// <summary>
    /// Gets user's top albums for a specified period
    /// </summary>
    Task<TopAlbums?> GetUserTopAlbumsAsync(string username, string period, int limit = 10, int page = 1);
    
    // Date range variants
    /// <summary>
    /// Gets user's top artists for a specific date range
    /// </summary>
    Task<TopArtists?> GetUserTopArtistsForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10);
    
    /// <summary>
    /// Gets user's top tracks for a specific date range
    /// </summary>
    Task<TopTracks?> GetUserTopTracksForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10);
    
    /// <summary>
    /// Gets user's top albums for a specific date range
    /// </summary>
    Task<TopAlbums?> GetUserTopAlbumsForDateRangeAsync(string username, DateTime from, DateTime to, int limit = 10);
    
    // Artist-specific content
    /// <summary>
    /// Gets top tracks for a specific artist (global Last.fm data)
    /// </summary>
    Task<TopTracks?> GetArtistTopTracksAsync(string artistName, int limit = 10);
    
    /// <summary>
    /// Gets top albums for a specific artist (global Last.fm data)
    /// </summary>
    Task<TopAlbums?> GetArtistTopAlbumsAsync(string artistName, int limit = 10);
    
    /// <summary>
    /// Gets similar artists for a specific artist
    /// </summary>
    Task<SimilarArtists?> GetSimilarArtistsAsync(string artistName, int limit = 50);
    
    // Range queries - complex operations that span multiple pages
    /// <summary>
    /// Gets a specific range of user's top artists (e.g., ranks 50-100)
    /// </summary>
    Task<(List<Artist> items, string totalCount)> GetUserTopArtistsRangeAsync(string username, string period, int startIndex, int endIndex);
    
    /// <summary>
    /// Gets a specific range of user's top tracks (e.g., ranks 50-100)
    /// </summary>
    Task<(List<Track> items, string totalCount)> GetUserTopTracksRangeAsync(string username, string period, int startIndex, int endIndex);
    
    /// <summary>
    /// Gets a specific range of user's top albums (e.g., ranks 50-100)
    /// </summary>
    Task<(List<Album> items, string totalCount)> GetUserTopAlbumsRangeAsync(string username, string period, int startIndex, int endIndex);
    
    // Deep search operations - search through user's entire history
    /// <summary>
    /// Searches user's entire track history for tracks by a specific artist
    /// </summary>
    Task<List<Track>> SearchUserTracksForArtistAsync(string username, string artistName, int limit, int maxDepth = int.MaxValue, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches user's entire album history for albums by a specific artist
    /// </summary>
    Task<List<Album>> SearchUserAlbumsForArtistAsync(string username, string artistName, int limit, int maxDepth = int.MaxValue, CancellationToken cancellationToken = default);
    
    // Advanced features
    /// <summary>
    /// Generates personalized music recommendations based on user's listening history
    /// </summary>
    Task<List<RecommendationResult>> GetMusicRecommendationsAsync(string username,
        int analysisLimit = 20,
        int recommendationLimit = 20,
        int filterThreshold = 0,
        int tracksPerArtist = 0,
        string period = "overall",
        bool excludeTags = false);
    
    /// <summary>
    /// Generates personalized music recommendations based on user's listening history for a specific date range
    /// </summary>
    Task<List<RecommendationResult>> GetMusicRecommendationsForDateRangeAsync(string username,
        DateTime from,
        DateTime to,
        int analysisLimit = 20,
        int recommendationLimit = 20,
        int filterThreshold = 0,
        int tracksPerArtist = 0,
        bool excludeTags = false);
    
    /// <summary>
    /// Builds a comprehensive map of user's artist play counts
    /// Used for filtering and recommendation algorithms
    /// </summary>
    Task<Dictionary<string, int>> GetUserArtistPlayCountsAsync(string username, int maxArtists = int.MaxValue);

    // New Result-based methods for gradual migration
    /// <summary>
    /// Gets user's top artists with proper error handling
    /// </summary>
    Task<Result<TopArtists>> GetUserTopArtistsWithResultAsync(string username, string period, int limit = 10, int page = 1);
    
    /// <summary>
    /// Gets music recommendations with comprehensive error handling
    /// </summary>
    Task<Result<List<RecommendationResult>>> GetMusicRecommendationsWithResultAsync(string username,
        int analysisLimit = 20,
        int recommendationLimit = 20,
        int filterThreshold = 0,
        int tracksPerArtist = 0,
        string period = "overall",
        bool excludeTags = false);

    /// <summary>
    /// Validates user configuration (API key, username, etc.)
    /// </summary>
    Task<Result> ValidateUserConfigurationAsync(string? username = null);
}