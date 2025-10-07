using System.Text.Json.Serialization;

namespace Lfm.Core.Models;

public class LastFmResponse<T>
{
    [JsonPropertyName("error")]
    public int? Error { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    public T? Data { get; set; }
}

public class Artist
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("playcount")]
    public string PlayCount { get; set; } = "0";
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    
    [JsonPropertyName("mbid")]
    public string Mbid { get; set; } = string.Empty;
    
    [JsonPropertyName("@attr")]
    public ArtistAttributes? Attributes { get; set; }
}

public class ArtistAttributes
{
    [JsonPropertyName("rank")]
    public string Rank { get; set; } = string.Empty;
}

public class Track
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("playcount")]
    public string PlayCount { get; set; } = "0";
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    
    [JsonPropertyName("mbid")]
    public string Mbid { get; set; } = string.Empty;
    
    [JsonPropertyName("artist")]
    public ArtistInfo Artist { get; set; } = new();
    
    [JsonPropertyName("@attr")]
    public TrackAttributes? Attributes { get; set; }
}

public class ArtistInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("mbid")]
    public string Mbid { get; set; } = string.Empty;
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class RecentTrackArtistInfo
{
    [JsonPropertyName("#text")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("mbid")]
    public string Mbid { get; set; } = string.Empty;
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class TrackAttributes
{
    [JsonPropertyName("rank")]
    public string Rank { get; set; } = string.Empty;
}

public class Album
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("playcount")]
    public string PlayCount { get; set; } = "0";
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    
    [JsonPropertyName("mbid")]
    public string Mbid { get; set; } = string.Empty;
    
    [JsonPropertyName("artist")]
    public ArtistInfo Artist { get; set; } = new();
    
    [JsonPropertyName("@attr")]
    public AlbumAttributes? Attributes { get; set; }
}

public class AlbumAttributes
{
    [JsonPropertyName("rank")]
    public string Rank { get; set; } = string.Empty;
}

public class TopArtists
{
    [JsonPropertyName("artist")]
    public List<Artist> Artists { get; set; } = new();
    
    [JsonPropertyName("@attr")]
    public TopArtistsAttributes Attributes { get; set; } = new();
}

public class TopTracks
{
    [JsonPropertyName("track")]
    public List<Track> Tracks { get; set; } = new();
    
    [JsonPropertyName("@attr")]
    public TopTracksAttributes Attributes { get; set; } = new();
}

public class TopAlbums
{
    [JsonPropertyName("album")]
    public List<Album> Albums { get; set; } = new();
    
    [JsonPropertyName("@attr")]
    public TopAlbumsAttributes Attributes { get; set; } = new();
}

public class TopArtistsAttributes
{
    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;
    
    [JsonPropertyName("totalPages")]
    public string TotalPages { get; set; } = "0";
    
    [JsonPropertyName("page")]
    public string Page { get; set; } = "0";
    
    [JsonPropertyName("total")]
    public string Total { get; set; } = "0";
    
    [JsonPropertyName("perPage")]
    public string PerPage { get; set; } = "0";
}

public class TopTracksAttributes
{
    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;
    
    [JsonPropertyName("totalPages")]
    public string TotalPages { get; set; } = "0";
    
    [JsonPropertyName("page")]
    public string Page { get; set; } = "0";
    
    [JsonPropertyName("total")]
    public string Total { get; set; } = "0";
    
    [JsonPropertyName("perPage")]
    public string PerPage { get; set; } = "0";
}

public class TopAlbumsAttributes
{
    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;
    
    [JsonPropertyName("totalPages")]
    public string TotalPages { get; set; } = "0";
    
    [JsonPropertyName("page")]
    public string Page { get; set; } = "0";
    
    [JsonPropertyName("total")]
    public string Total { get; set; } = "0";
    
    [JsonPropertyName("perPage")]
    public string PerPage { get; set; } = "0";
}
public class SimilarArtist
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("mbid")]
    public string Mbid { get; set; } = string.Empty;
    
    [JsonPropertyName("match")]
    public string Match { get; set; } = "0";
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    
    [JsonPropertyName("streamable")]
    public string Streamable { get; set; } = "0";
}

public class SimilarArtists
{
    [JsonPropertyName("artist")]
    public List<SimilarArtist> Artists { get; set; } = new();
    
    [JsonPropertyName("@attr")]
    public SimilarArtistsAttributes Attributes { get; set; } = new();
}

public class SimilarArtistsAttributes
{
    [JsonPropertyName("artist")]
    public string Artist { get; set; } = string.Empty;
}

public class RecentTrack
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("artist")]
    public RecentTrackArtistInfo Artist { get; set; } = new();
    
    [JsonPropertyName("album")]
    public AlbumInfo Album { get; set; } = new();
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    
    [JsonPropertyName("mbid")]
    public string Mbid { get; set; } = string.Empty;
    
    [JsonPropertyName("date")]
    public DateInfo? Date { get; set; }
    
    [JsonPropertyName("@attr")]
    public RecentTrackAttributes? Attributes { get; set; }
}

public class AlbumInfo
{
    [JsonPropertyName("#text")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("mbid")]
    public string Mbid { get; set; } = string.Empty;
}

public class DateInfo
{
    [JsonPropertyName("uts")]
    public string UnixTimestamp { get; set; } = "0";
    
    [JsonPropertyName("#text")]
    public string Text { get; set; } = string.Empty;
}

public class RecentTrackAttributes
{
    [JsonPropertyName("nowplaying")]
    public string? NowPlaying { get; set; }
}

public class RecentTracks
{
    [JsonPropertyName("track")]
    public List<RecentTrack> Tracks { get; set; } = new();
    
    [JsonPropertyName("@attr")]
    public RecentTracksAttributes Attributes { get; set; } = new();
}

public class RecentTracksAttributes
{
    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;
    
    [JsonPropertyName("totalPages")]
    public string TotalPages { get; set; } = "0";
    
    [JsonPropertyName("page")]
    public string Page { get; set; } = "0";
    
    [JsonPropertyName("total")]
    public string Total { get; set; } = "0";
    
    [JsonPropertyName("perPage")]
    public string PerPage { get; set; } = "0";
}

// Helper classes for date range aggregation
internal class ArtistAggregation
{
    public string Name { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Mbid { get; set; } = string.Empty;
}

internal class TrackAggregation
{
    public string Name { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Mbid { get; set; } = string.Empty;
    public ArtistInfo Artist { get; set; } = new();
}

internal class AlbumAggregation
{
    public string Name { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public string Mbid { get; set; } = string.Empty;
    public ArtistInfo Artist { get; set; } = new();
}

public class Tag
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int? Count { get; set; }
}

public class TopTags
{
    [JsonPropertyName("tag")]
    public List<Tag> Tags { get; set; } = new();

    [JsonPropertyName("@attr")]
    public TopTagsAttributes Attributes { get; set; } = new();
}

public class TopTagsAttributes
{
    [JsonPropertyName("artist")]
    public string Artist { get; set; } = string.Empty;
}
