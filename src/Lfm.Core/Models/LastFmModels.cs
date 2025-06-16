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