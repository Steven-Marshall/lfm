using System.Text.Json.Serialization;

namespace Lfm.Core.Models;

/// <summary>
/// Represents track information with user-specific playcount and love status from track.getInfo API
/// </summary>
public class TrackLookupInfo
{
    [JsonPropertyName("track")]
    public TrackDetails Track { get; set; } = new();

    public class TrackDetails
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("mbid")]
        public string? Mbid { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public string Duration { get; set; } = "0";

        [JsonPropertyName("listeners")]
        public string Listeners { get; set; } = "0";

        [JsonPropertyName("playcount")]
        public string Playcount { get; set; } = "0";

        [JsonPropertyName("userplaycount")]
        public string? UserPlaycount { get; set; }

        [JsonPropertyName("userloved")]
        public string? UserLoved { get; set; }

        [JsonPropertyName("artist")]
        public ArtistBasic Artist { get; set; } = new();

        [JsonPropertyName("album")]
        public AlbumBasic? Album { get; set; }

        [JsonPropertyName("toptags")]
        public TagsWrapper? TopTags { get; set; }

        public int GetUserPlaycount() =>
            int.TryParse(UserPlaycount, out var count) ? count : 0;

        public bool IsUserLoved() =>
            UserLoved == "1";

        public int GetGlobalPlaycount() =>
            int.TryParse(Playcount, out var count) ? count : 0;
    }

    public class ArtistBasic
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("mbid")]
        public string? Mbid { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    public class AlbumBasic
    {
        [JsonPropertyName("artist")]
        public string Artist { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("image")]
        public List<Image>? Images { get; set; }
    }

    public class Image
    {
        [JsonPropertyName("#text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public string Size { get; set; } = string.Empty;
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