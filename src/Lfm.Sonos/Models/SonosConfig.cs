namespace Lfm.Sonos.Models;

public class SonosConfig
{
    /// <summary>
    /// Base URL for the node-sonos-http-api bridge (e.g., http://192.168.1.24:5005)
    /// </summary>
    public string HttpApiBaseUrl { get; set; } = "http://localhost:5005";

    /// <summary>
    /// Default room to use when no room is specified
    /// </summary>
    public string? DefaultRoom { get; set; }

    /// <summary>
    /// Whether to automatically discover rooms on startup
    /// </summary>
    public bool AutoDiscoverRooms { get; set; } = true;

    /// <summary>
    /// HTTP request timeout in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// How long to cache discovered rooms (in minutes)
    /// </summary>
    public int RoomCacheDurationMinutes { get; set; } = 5;
}
