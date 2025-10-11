namespace Lfm.Core.Models;

public class NewReleasesResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<NewAlbumRelease> Albums { get; set; } = new();
    public int Total { get; set; }
    public string Source { get; set; } = "spotify";
}

public class NewAlbumRelease
{
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public string ReleaseDatePrecision { get; set; } = string.Empty;
    public int TotalTracks { get; set; }
    public string AlbumType { get; set; } = string.Empty;
    public string SpotifyId { get; set; } = string.Empty;
    public string SpotifyUrl { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}
