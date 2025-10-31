using System.Text.Json.Serialization;

namespace Lfm.Spotify.Models;

// OAuth Token Response
public class SpotifyTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
}

// Search API Response
public class SpotifySearchResponse
{
    [JsonPropertyName("tracks")]
    public SpotifyTracksResult? Tracks { get; set; }
}

public class SpotifyTracksResult
{
    [JsonPropertyName("items")]
    public List<SpotifyTrack> Items { get; set; } = new();
}

public class SpotifyTrack
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("artists")]
    public List<SpotifyArtist> Artists { get; set; } = new();

    [JsonPropertyName("album")]
    public SpotifyAlbum? Album { get; set; }
}

public class SpotifyArtist
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class SpotifyAlbum
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Result of track search with album version detection
/// </summary>
public class TrackSearchResult
{
    public string? SpotifyUri { get; set; }
    public bool HasMultipleVersions { get; set; }
    public List<string> AlbumVersions { get; set; } = new();
}

/// <summary>
/// Result of album search with version detection
/// </summary>
public class AlbumSearchResult
{
    public string? SpotifyUri { get; set; }
    public List<string>? TrackUris { get; set; }
    public bool HasMultipleVersions { get; set; }
    public List<AlbumVersionInfo> AlbumVersions { get; set; } = new();
}

/// <summary>
/// Information about a specific album version
/// </summary>
public class AlbumVersionInfo
{
    public string Name { get; set; } = string.Empty;
    public string? ReleaseDate { get; set; }
    public string Uri { get; set; } = string.Empty;
    public int TrackCount { get; set; }
}

// Playlist Creation
public class CreatePlaylistRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("public")]
    public bool Public { get; set; } = false;
}

public class SpotifyPlaylist
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;
}

// Add tracks to playlist
public class AddTracksRequest
{
    [JsonPropertyName("uris")]
    public List<string> Uris { get; set; } = new();
}

// User profile (for getting user ID)
public class SpotifyUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

// Playlist listing response
public class SpotifyPlaylistsResponse
{
    [JsonPropertyName("items")]
    public List<SpotifyPlaylistItem> Items { get; set; } = new();

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class SpotifyPlaylistItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("tracks")]
    public SpotifyPlaylistTracks Tracks { get; set; } = new();

    [JsonPropertyName("owner")]
    public SpotifyUser Owner { get; set; } = new();
}

public class SpotifyPlaylistTracks
{
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

// Simple playlist info for our use
public class PlaylistInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TracksCount { get; set; }
    public bool IsOwned { get; set; }
}

/// <summary>
/// Result of playlist search with multiple match detection
/// </summary>
public class PlaylistSearchResult
{
    public string? PlaylistUri { get; set; }
    public bool HasMultipleMatches { get; set; }
    public List<PlaylistInfo> Playlists { get; set; } = new();
}

// Device models for playback control
public class SpotifyDevicesResponse
{
    [JsonPropertyName("devices")]
    public List<SpotifyDevice> Devices { get; set; } = new();
}

public class SpotifyDevice
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_private_session")]
    public bool IsPrivateSession { get; set; }

    [JsonPropertyName("is_restricted")]
    public bool IsRestricted { get; set; }

    [JsonPropertyName("volume_percent")]
    public int? VolumePercent { get; set; }
}

// Album Search Response
public class SpotifyAlbumSearchResponse
{
    [JsonPropertyName("albums")]
    public SpotifyAlbumsResult? Albums { get; set; }
}

public class SpotifyAlbumsResult
{
    [JsonPropertyName("items")]
    public List<SpotifyAlbumItem> Items { get; set; } = new();
}

public class SpotifyAlbumItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("total_tracks")]
    public int TotalTracks { get; set; }
}

// Album Tracks Response
public class SpotifyAlbumTracksResponse
{
    [JsonPropertyName("items")]
    public List<SpotifyAlbumTrack> Items { get; set; } = new();
}

public class SpotifyAlbumTrack
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

// Playback State Response
public class SpotifyPlaybackState
{
    [JsonPropertyName("device")]
    public SpotifyDevice? Device { get; set; }

    [JsonPropertyName("progress_ms")]
    public int ProgressMs { get; set; }

    [JsonPropertyName("is_playing")]
    public bool IsPlaying { get; set; }

    [JsonPropertyName("item")]
    public SpotifyPlaybackTrack? Item { get; set; }
}

public class SpotifyPlaybackTrack
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("artists")]
    public List<SpotifyArtist>? Artists { get; set; }

    [JsonPropertyName("album")]
    public SpotifyAlbum? Album { get; set; }

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; set; }
}

// New Releases Response
public class SpotifyNewReleasesResponse
{
    [JsonPropertyName("albums")]
    public SpotifyNewReleasesAlbumsWrapper? Albums { get; set; }
}

public class SpotifyNewReleasesAlbumsWrapper
{
    [JsonPropertyName("items")]
    public List<SpotifyNewReleaseAlbum> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("previous")]
    public string? Previous { get; set; }
}

public class SpotifyNewReleaseAlbum
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("album_type")]
    public string AlbumType { get; set; } = string.Empty;

    [JsonPropertyName("artists")]
    public List<SpotifyArtist> Artists { get; set; } = new();

    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; set; } = string.Empty;

    [JsonPropertyName("release_date_precision")]
    public string ReleaseDatePrecision { get; set; } = string.Empty;

    [JsonPropertyName("total_tracks")]
    public int TotalTracks { get; set; }

    [JsonPropertyName("images")]
    public List<SpotifyImage> Images { get; set; } = new();

    [JsonPropertyName("external_urls")]
    public SpotifyExternalUrls? ExternalUrls { get; set; }
}

public class SpotifyImage
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }
}

public class SpotifyExternalUrls
{
    [JsonPropertyName("spotify")]
    public string Spotify { get; set; } = string.Empty;
}