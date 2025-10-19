using Lfm.Sonos.Models;

namespace Lfm.Sonos;

public interface ISonosStreamer
{
    /// <summary>
    /// Check if the Sonos HTTP API bridge is available
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Get all available Sonos rooms (with caching)
    /// </summary>
    Task<List<SonosRoom>> GetRoomsAsync();

    /// <summary>
    /// Play a Spotify track/album immediately, starting playback if needed
    /// </summary>
    Task PlayNowAsync(string spotifyUri, string roomName);

    /// <summary>
    /// Add a Spotify track/album to the queue
    /// </summary>
    Task QueueAsync(string spotifyUri, string roomName);

    /// <summary>
    /// Clear the current queue in a room
    /// </summary>
    Task ClearQueueAsync(string roomName);

    /// <summary>
    /// Pause playback in a room
    /// </summary>
    Task PauseAsync(string roomName);

    /// <summary>
    /// Resume playback in a room
    /// </summary>
    Task ResumeAsync(string roomName);

    /// <summary>
    /// Skip to next or previous track
    /// </summary>
    Task SkipAsync(string roomName, SkipDirection direction);

    /// <summary>
    /// Get current playback state for a room
    /// </summary>
    Task<SonosPlaybackState?> GetPlaybackStateAsync(string roomName);

    /// <summary>
    /// Validate that a room exists
    /// </summary>
    Task ValidateRoomAsync(string roomName);
}

public enum SkipDirection
{
    Next,
    Previous
}
