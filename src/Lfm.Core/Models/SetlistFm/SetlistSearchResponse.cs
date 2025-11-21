using System.Text.Json.Serialization;

namespace Lfm.Core.Models.SetlistFm;

public class SetlistSearchResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("itemsPerPage")]
    public int ItemsPerPage { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("setlist")]
    public List<Setlist> Setlists { get; set; } = new();
}
