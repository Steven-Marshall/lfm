using Lfm.Sonos.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lfm.Sonos;

public class SonosStreamer : ISonosStreamer
{
    private readonly HttpClient _httpClient;
    private readonly SonosConfig _config;
    private readonly ILogger<SonosStreamer> _logger;

    // Room cache
    private List<SonosRoom>? _cachedRooms;
    private DateTime? _roomCacheExpiry;

    public SonosStreamer(SonosConfig config, ILogger<SonosStreamer> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(config.TimeoutMs)
        };
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var response = await _httpClient.GetAsync($"{_config.HttpApiBaseUrl}/zones", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Sonos bridge availability check failed");
            return false;
        }
    }

    public async Task<List<SonosRoom>> GetRoomsAsync()
    {
        // Check cache
        if (_cachedRooms != null && _roomCacheExpiry.HasValue && DateTime.UtcNow < _roomCacheExpiry.Value)
        {
            _logger.LogDebug("Returning cached Sonos rooms ({Count} rooms)", _cachedRooms.Count);
            return _cachedRooms;
        }

        // Fetch from API
        _logger.LogDebug("Fetching Sonos rooms from bridge at {BaseUrl}", _config.HttpApiBaseUrl);
        var url = $"{_config.HttpApiBaseUrl}/zones";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var zones = JsonSerializer.Deserialize<List<ZoneResponse>>(json);

        var rooms = zones?.Select(z => new SonosRoom
        {
            Name = z.Coordinator?.RoomName ?? "Unknown",
            Coordinator = z.Coordinator?.Uuid ?? string.Empty,
            Members = z.Members?.Select(m => m.RoomName ?? "Unknown").ToList() ?? new()
        }).ToList() ?? new();

        // Update cache
        _cachedRooms = rooms;
        _roomCacheExpiry = DateTime.UtcNow.AddMinutes(_config.RoomCacheDurationMinutes);

        _logger.LogInformation("Discovered {Count} Sonos rooms", rooms.Count);
        return rooms;
    }

    public async Task PlayNowAsync(string spotifyUri, string roomName)
    {
        await ValidateAvailabilityAsync();
        await ValidateRoomAsync(roomName);

        // Get current state to check if playing
        var state = await GetPlaybackStateAsync(roomName);
        var wasPlaying = state?.PlaybackState == "PLAYING";

        // Clear the queue first to match Spotify "play now" behavior (clean slate)
        _logger.LogDebug("Clearing queue before play now on room '{Room}'", roomName);
        await ClearQueueAsync(roomName);

        // Call /room/spotify/now/uri endpoint
        var encodedRoom = Uri.EscapeDataString(roomName);
        // NOTE: Don't encode the Spotify URI - node-sonos-http-api expects it raw in the URL path
        var url = $"{_config.HttpApiBaseUrl}/{encodedRoom}/spotify/now/{spotifyUri}";

        try
        {
            _logger.LogDebug("Calling Sonos API: {Url}", url);
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Sonos API error {StatusCode}: {Error}", response.StatusCode, errorBody);
                throw new InvalidOperationException(
                    $"Failed to play on Sonos room '{roomName}'. HTTP {response.StatusCode}: {errorBody}");
            }

            _logger.LogInformation("Started playback on Sonos room '{Room}': {Uri}", roomName, spotifyUri);

            // If not playing before, ensure playback starts
            if (!wasPlaying)
            {
                _logger.LogDebug("Room was stopped, ensuring playback starts");
                await Task.Delay(500); // Brief delay for queue to populate
                await ResumeAsync(roomName);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Failed to play on Sonos room '{roomName}'. Network error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Play a Spotify playlist by ID on Sonos
    /// </summary>
    public async Task PlayPlaylistAsync(string playlistId, string roomName)
    {
        // Construct Spotify playlist URI with required Sonos format
        // Note: Sonos requires "spotify:user:spotify:playlist:{id}" format
        var playlistUri = $"spotify:user:spotify:playlist:{playlistId}";
        await PlayNowAsync(playlistUri, roomName);
    }

    public async Task QueueAsync(string spotifyUri, string roomName)
    {
        await ValidateAvailabilityAsync();
        await ValidateRoomAsync(roomName);

        var encodedRoom = Uri.EscapeDataString(roomName);
        // NOTE: Don't encode the Spotify URI - node-sonos-http-api expects it raw in the URL path
        var url = $"{_config.HttpApiBaseUrl}/{encodedRoom}/spotify/queue/{spotifyUri}";

        try
        {
            _logger.LogDebug("Calling Sonos API: {Url}", url);
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Sonos API error {StatusCode}: {Error}", response.StatusCode, errorBody);
                throw new InvalidOperationException(
                    $"Failed to queue on Sonos room '{roomName}'. HTTP {response.StatusCode}: {errorBody}");
            }

            _logger.LogInformation("Queued on Sonos room '{Room}': {Uri}", roomName, spotifyUri);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Failed to queue on Sonos room '{roomName}'. Network error: {ex.Message}", ex);
        }
    }

    public async Task ClearQueueAsync(string roomName)
    {
        await ValidateAvailabilityAsync();
        await ValidateRoomAsync(roomName);

        var encodedRoom = Uri.EscapeDataString(roomName);
        var url = $"{_config.HttpApiBaseUrl}/{encodedRoom}/clearqueue";

        try
        {
            _logger.LogDebug("Calling Sonos API: {Url}", url);
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Sonos API error {StatusCode}: {Error}", response.StatusCode, errorBody);
                throw new InvalidOperationException(
                    $"Failed to clear queue on Sonos room '{roomName}'. HTTP {response.StatusCode}: {errorBody}");
            }

            _logger.LogInformation("Cleared queue on Sonos room '{Room}'", roomName);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Failed to clear queue on Sonos room '{roomName}'. Network error: {ex.Message}", ex);
        }
    }

    public async Task PauseAsync(string roomName)
    {
        await ValidateAvailabilityAsync();
        await ValidateRoomAsync(roomName);

        var encodedRoom = Uri.EscapeDataString(roomName);
        var url = $"{_config.HttpApiBaseUrl}/{encodedRoom}/pause";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Paused Sonos room '{Room}'", roomName);
    }

    public async Task ResumeAsync(string roomName)
    {
        await ValidateAvailabilityAsync();
        await ValidateRoomAsync(roomName);

        var encodedRoom = Uri.EscapeDataString(roomName);
        var url = $"{_config.HttpApiBaseUrl}/{encodedRoom}/play";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Resumed Sonos room '{Room}'", roomName);
    }

    public async Task SkipAsync(string roomName, SkipDirection direction)
    {
        await ValidateAvailabilityAsync();
        await ValidateRoomAsync(roomName);

        var encodedRoom = Uri.EscapeDataString(roomName);
        var endpoint = direction == SkipDirection.Next ? "next" : "previous";
        var url = $"{_config.HttpApiBaseUrl}/{encodedRoom}/{endpoint}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Skipped {Direction} on Sonos room '{Room}'", direction, roomName);
    }

    public async Task<SonosPlaybackState?> GetPlaybackStateAsync(string roomName)
    {
        await ValidateAvailabilityAsync();
        await ValidateRoomAsync(roomName);

        var encodedRoom = Uri.EscapeDataString(roomName);
        var url = $"{_config.HttpApiBaseUrl}/{encodedRoom}/state";

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var state = JsonSerializer.Deserialize<StateResponse>(json);

            if (state == null)
                return null;

            return new SonosPlaybackState
            {
                CurrentTrack = state.CurrentTrack?.Title ?? string.Empty,
                Artist = state.CurrentTrack?.Artist ?? string.Empty,
                Album = state.CurrentTrack?.Album ?? string.Empty,
                PlaybackState = state.PlaybackState ?? "STOPPED",
                Volume = state.Volume,
                TrackNumber = state.TrackNo,
                ElapsedTime = state.ElapsedTime,
                Duration = state.CurrentTrack?.Duration
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task ValidateRoomAsync(string roomName)
    {
        var rooms = await GetRoomsAsync();

        if (!rooms.Any(r => r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase)))
        {
            var availableRooms = string.Join(", ", rooms.Select(r => r.Name));
            throw new InvalidOperationException(
                $"Sonos room '{roomName}' not found. Available rooms: {availableRooms}");
        }
    }

    private async Task ValidateAvailabilityAsync()
    {
        if (!await IsAvailableAsync())
        {
            throw new InvalidOperationException(
                $"Sonos bridge (node-sonos-http-api) is not available at {_config.HttpApiBaseUrl}. " +
                $"Ensure the Node.js server is running and accessible.");
        }
    }

    // Internal models for parsing API responses
    internal class ZoneResponse
    {
        [JsonPropertyName("uuid")]
        public string? Uuid { get; set; }

        [JsonPropertyName("coordinator")]
        public CoordinatorInfo? Coordinator { get; set; }

        [JsonPropertyName("members")]
        public List<MemberInfo>? Members { get; set; }
    }

    internal class CoordinatorInfo
    {
        [JsonPropertyName("roomName")]
        public string? RoomName { get; set; }

        [JsonPropertyName("uuid")]
        public string? Uuid { get; set; }
    }

    internal class MemberInfo
    {
        [JsonPropertyName("roomName")]
        public string? RoomName { get; set; }

        [JsonPropertyName("uuid")]
        public string? Uuid { get; set; }
    }

    internal class StateResponse
    {
        [JsonPropertyName("currentTrack")]
        public TrackInfo? CurrentTrack { get; set; }

        [JsonPropertyName("playbackState")]
        public string? PlaybackState { get; set; }

        [JsonPropertyName("volume")]
        public int Volume { get; set; }

        [JsonPropertyName("trackNo")]
        public int TrackNo { get; set; }

        [JsonPropertyName("elapsedTime")]
        public int ElapsedTime { get; set; }
    }

    internal class TrackInfo
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("artist")]
        public string? Artist { get; set; }

        [JsonPropertyName("album")]
        public string? Album { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }
    }
}
