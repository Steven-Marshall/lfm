using System.Text.Json.Serialization;

namespace Lfm.Core.Models.SetlistFm;

public class Setlist
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("versionId")]
    public string VersionId { get; set; } = string.Empty;

    [JsonPropertyName("eventDate")]
    public string EventDate { get; set; } = string.Empty;

    [JsonPropertyName("lastUpdated")]
    public string LastUpdated { get; set; } = string.Empty;

    [JsonPropertyName("artist")]
    public SetlistArtist Artist { get; set; } = new();

    [JsonPropertyName("venue")]
    public Venue Venue { get; set; } = new();

    [JsonPropertyName("tour")]
    public Tour? Tour { get; set; }

    [JsonPropertyName("sets")]
    public Sets Sets { get; set; } = new();

    [JsonPropertyName("info")]
    public string? Info { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class SetlistArtist
{
    [JsonPropertyName("mbid")]
    public string Mbid { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sortName")]
    public string SortName { get; set; } = string.Empty;

    [JsonPropertyName("disambiguation")]
    public string Disambiguation { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class Venue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public City City { get; set; } = new();

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class City
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("stateCode")]
    public string StateCode { get; set; } = string.Empty;

    [JsonPropertyName("coords")]
    public Coordinates? Coords { get; set; }

    [JsonPropertyName("country")]
    public Country Country { get; set; } = new();
}

public class Coordinates
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("long")]
    public double Long { get; set; }
}

public class Country
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class Tour
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class Sets
{
    [JsonPropertyName("set")]
    public List<Set> Set { get; set; } = new();
}

public class Set
{
    [JsonPropertyName("encore")]
    public int? Encore { get; set; }

    [JsonPropertyName("song")]
    public List<Song> Songs { get; set; } = new();
}

public class Song
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("info")]
    public string? Info { get; set; }

    [JsonPropertyName("cover")]
    public Cover? Cover { get; set; }

    [JsonPropertyName("tape")]
    public bool? Tape { get; set; }
}

public class Cover
{
    [JsonPropertyName("mbid")]
    public string Mbid { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sortName")]
    public string SortName { get; set; } = string.Empty;

    [JsonPropertyName("disambiguation")]
    public string Disambiguation { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
