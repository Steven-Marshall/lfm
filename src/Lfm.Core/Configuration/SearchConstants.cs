namespace Lfm.Core.Configuration;

/// <summary>
/// Centralized constants for search operations and pagination
/// </summary>
public static class SearchConstants
{
    /// <summary>
    /// Last.fm API pagination settings
    /// </summary>
    public static class Api
    {
        /// <summary>
        /// Maximum items per page supported by Last.fm API
        /// </summary>
        public const int MaxItemsPerPage = 50;
        
        /// <summary>
        /// Recommended page size for efficient API calls
        /// </summary>
        public const int RecommendedPageSize = 50;
    }
    
    /// <summary>
    /// Search configuration for artist filtering
    /// </summary>
    public static class ArtistSearch
    {
        /// <summary>
        /// Progress update interval (show progress every N pages)
        /// </summary>
        public const int ProgressUpdateInterval = 10;
        
        /// <summary>
        /// Early termination multiplier for normal search
        /// (stop when found results >= limit * this value)
        /// </summary>
        public const int EarlyTerminationMultiplier = 3;
    }
    
    /// <summary>
    /// Range query pagination settings
    /// </summary>
    public static class RangeQuery
    {
        /// <summary>
        /// Maximum items per page for range queries
        /// </summary>
        public const int MaxItemsPerPage = 50;
    }
    
    /// <summary>
    /// Default values for commands
    /// </summary>
    public static class Defaults
    {
        /// <summary>
        /// Default number of items to display
        /// </summary>
        public const int ItemLimit = 10;
        
        /// <summary>
        /// Default time period for user stats
        /// </summary>
        public const string TimePeriod = "overall";
    }
    
    /// <summary>
    /// Display formatting constants
    /// </summary>
    public static class Display
    {
        /// <summary>
        /// Maximum length for artist names before truncation
        /// </summary>
        public const int ArtistNameMaxLength = 40;
        
        /// <summary>
        /// Maximum length for track names before truncation
        /// </summary>
        public const int TrackNameMaxLength = 40;
        
        /// <summary>
        /// Maximum length for album names before truncation
        /// </summary>
        public const int AlbumNameMaxLength = 40;
        
        /// <summary>
        /// Maximum length for secondary artist names before truncation
        /// </summary>
        public const int SecondaryArtistNameMaxLength = 30;
        
        /// <summary>
        /// Characters to append when truncating strings
        /// </summary>
        public const string TruncationSuffix = "...";
        
        /// <summary>
        /// Length of truncation suffix
        /// </summary>
        public const int TruncationSuffixLength = 3;
    }
}