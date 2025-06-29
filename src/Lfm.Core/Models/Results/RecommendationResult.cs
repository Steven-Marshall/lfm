namespace Lfm.Core.Models.Results;

/// <summary>
/// Represents a music recommendation result from the recommendation algorithm
/// </summary>
public class RecommendationResult
{
    /// <summary>
    /// The recommended artist name
    /// </summary>
    public string ArtistName { get; set; } = string.Empty;
    
    /// <summary>
    /// Composite score based on similarity and occurrence across multiple source artists
    /// Formula: (average_similarity_score) × (occurrence_count)
    /// </summary>
    public float Score { get; set; }
    
    /// <summary>
    /// Average similarity score across all source artists that recommended this artist
    /// </summary>
    public float AverageSimilarity { get; set; }
    
    /// <summary>
    /// Number of user's top artists that had this artist as a similar recommendation
    /// Higher values indicate broader appeal across user's taste
    /// </summary>
    public int OccurrenceCount { get; set; }
    
    /// <summary>
    /// User's current play count for this artist (0 if never listened)
    /// Used for filtering recommendations
    /// </summary>
    public int UserPlayCount { get; set; }
    
    /// <summary>
    /// Optional: Top tracks by this artist (if requested)
    /// </summary>
    public List<Track>? TopTracks { get; set; }
    
    /// <summary>
    /// Names of user's top artists that led to this recommendation
    /// Useful for explaining why this artist was recommended
    /// </summary>
    public List<string> SourceArtists { get; set; } = new();
}