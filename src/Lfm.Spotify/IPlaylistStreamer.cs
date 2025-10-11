using Lfm.Core.Models;
using Lfm.Spotify.Models;

namespace Lfm.Spotify;

/// <summary>
/// Interface for streaming playlists to different music players
/// </summary>
public interface IPlaylistStreamer
{
    /// <summary>
    /// Name of the streaming service
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Check if the streaming service is available
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Queue tracks for immediate playback
    /// </summary>
    Task<PlaylistStreamResult> QueueTracksAsync(List<Track> tracks, string? device = null);

    /// <summary>
    /// Play tracks immediately, interrupting current playback
    /// </summary>
    Task<PlaylistStreamResult> PlayNowAsync(List<Track> tracks, string? device = null);

    /// <summary>
    /// Save tracks as a named playlist
    /// </summary>
    Task<PlaylistStreamResult> SavePlaylistAsync(List<Track> tracks, string playlistName, string? device = null);

    /// <summary>
    /// Get user's playlists
    /// </summary>
    Task<List<PlaylistInfo>> GetUserPlaylistsAsync();

    /// <summary>
    /// Delete/unfollow a playlist
    /// </summary>
    Task<bool> DeletePlaylistAsync(string playlistId);

    /// <summary>
    /// Get available Spotify devices
    /// </summary>
    Task<List<SpotifyDevice>> GetDevicesAsync();

    /// <summary>
    /// Activate/wake up a Spotify device to make it ready for commands
    /// </summary>
    Task<bool> ActivateDeviceAsync(string? deviceName = null);

    /// <summary>
    /// Get currently playing track information
    /// </summary>
    Task<CurrentTrackInfo?> GetCurrentlyPlayingAsync();

    /// <summary>
    /// Pause current playback
    /// </summary>
    Task<bool> PauseAsync();

    /// <summary>
    /// Resume paused playback
    /// </summary>
    Task<bool> ResumeAsync();

    /// <summary>
    /// Skip to next or previous track
    /// </summary>
    Task<bool> SkipAsync(SkipDirection direction = SkipDirection.Next);

    /// <summary>
    /// Get new album releases from Spotify
    /// </summary>
    Task<List<SpotifyNewReleaseAlbum>> GetNewReleasesAsync(int limit = 50);
}

/// <summary>
/// Result of playlist streaming operation
/// </summary>
public class PlaylistStreamResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TracksProcessed { get; set; }
    public int TracksFound { get; set; }
    public List<string> NotFoundTracks { get; set; } = new();
}

/// <summary>
/// Direction for skipping tracks
/// </summary>
public enum SkipDirection
{
    Next,
    Previous
}

/// <summary>
/// Information about currently playing track
/// </summary>
public class CurrentTrackInfo
{
    public string TrackName { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string AlbumName { get; set; } = string.Empty;
    public int ProgressMs { get; set; }
    public int DurationMs { get; set; }
    public bool IsPlaying { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
}