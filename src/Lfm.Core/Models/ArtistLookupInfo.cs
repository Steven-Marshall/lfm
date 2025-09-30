using System.Text.Json.Serialization;

namespace Lfm.Core.Models;

/// <summary>
/// Represents artist information with user-specific playcount data from artist.getInfo API
/// </summary>
public class ArtistLookupInfo
{
    [JsonPropertyName("artist")]
    public ArtistDetails Artist { get; set; } = new();

    public class ArtistDetails
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("mbid")]
        public string? Mbid { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("stats")]
        public ArtistStats Stats { get; set; } = new();

        [JsonPropertyName("tags")]
        public TagsWrapper? Tags { get; set; }
    }

    public class ArtistStats
    {
        [JsonPropertyName("listeners")]
        public string Listeners { get; set; } = "0";

        [JsonPropertyName("playcount")]
        public string Playcount { get; set; } = "0";

        [JsonPropertyName("userplaycount")]
        public string? UserPlaycount { get; set; }

        public int GetUserPlaycount() =>
            int.TryParse(UserPlaycount, out var count) ? count : 0;

        public int GetGlobalPlaycount() =>
            int.TryParse(Playcount, out var count) ? count : 0;
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