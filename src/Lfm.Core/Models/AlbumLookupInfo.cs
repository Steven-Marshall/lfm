using System.Text.Json.Serialization;

namespace Lfm.Core.Models;

/// <summary>
/// Represents album information with user-specific playcount data from album.getInfo API
/// </summary>
public class AlbumLookupInfo
{
    [JsonPropertyName("album")]
    public AlbumDetails Album { get; set; } = new();

    public class AlbumDetails
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("artist")]
        public string Artist { get; set; } = string.Empty;

        [JsonPropertyName("mbid")]
        public string? Mbid { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("listeners")]
        public string Listeners { get; set; } = "0";

        [JsonPropertyName("playcount")]
        public string Playcount { get; set; } = "0";

        [JsonPropertyName("userplaycount")]
        public int? UserPlaycount { get; set; }

        [JsonPropertyName("tracks")]
        public TracksWrapper? Tracks { get; set; }

        [JsonPropertyName("tags")]
        public TagsWrapper? Tags { get; set; }

        public int GetUserPlaycount() =>
            UserPlaycount ?? 0;

        public int GetGlobalPlaycount() =>
            int.TryParse(Playcount, out var count) ? count : 0;

        public int GetTrackCount() =>
            Tracks?.Track?.Count ?? 0;
    }

    public class TracksWrapper
    {
        [JsonPropertyName("track")]
        public List<AlbumTrack> Track { get; set; } = new();
    }

    public class AlbumTrack
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public int? Duration { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("@attr")]
        public TrackAttributes? Attributes { get; set; }

        // Will be populated separately via track.getInfo calls
        public int UserPlaycount { get; set; } = 0;
    }

    public class TrackAttributes
    {
        [JsonPropertyName("rank")]
        public int? Rank { get; set; }
    }

    public class TagsWrapper
    {
        [JsonPropertyName("tag")]
        public List<Tag> Tag { get; set; } = new();
    }

    public class Tag
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }
}
