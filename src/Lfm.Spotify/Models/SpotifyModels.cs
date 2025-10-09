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