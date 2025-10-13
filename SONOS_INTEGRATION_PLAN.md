# Sonos Integration Plan

**Date:** 2025-10-11
**Status:** Ready for Implementation
**Target System:** Sonos S1 (fully compatible)
**Implementation Approach:** node-sonos-http-api + C# wrapper

---

## Executive Summary

Add Sonos playback support to the Last.fm CLI tool, enabling music playback on Sonos speakers alongside existing Spotify Web API integration. The implementation uses node-sonos-http-api as a local HTTP bridge running on a Raspberry Pi.

### User Requirements

✅ **Dual-mode operation**: Keep Spotify Web API + add Sonos (separate implementations)
✅ **Config-driven defaults**: `DefaultPlayer` setting (home=Sonos, work=Spotify)
✅ **Auto-discovery**: Discover Sonos rooms automatically via `/zones` endpoint
✅ **Code reuse**: Leverage existing Spotify search logic for URI resolution
✅ **Content source**: Play Spotify music through Sonos (not local library)
✅ **Auto-start playback**: If Sonos not playing, start playback automatically
✅ **Error handling**: Fail gracefully if bridge offline
✅ **Room grouping**: Future enhancement (noted for v2)

### Implementation Timeline

**Estimated:** 12-16 hours across 8 phases
**Dependencies:** Raspberry Pi with node-sonos-http-api running

---

## Research Summary

### Option 1: Official Sonos Control API ❌

**Type:** Cloud-based REST API
**Authentication:** OAuth2 + API keys
**Base URL:** `https://api.ws.sonos.com/control/api/v1`

**Pros:**
- Official support, long-term stability
- Works remotely (cloud-based)
- S1 and S2 compatible

**Cons:**
- ❌ Complex "cloud queue" system for dynamic playlists
- ❌ Cannot directly queue Spotify URIs
- ❌ Requires hosting separate HTTP service for queue management
- ❌ Heavy setup overhead

**Verdict:** Too complex for personal use case

---

### Option 2: node-sonos-http-api ✅ CHOSEN

**Type:** Local HTTP bridge (Node.js)
**Repository:** https://github.com/jishi/node-sonos-http-api (7.5K stars)
**Authentication:** None (local network only)

**Pros:**
- ✅ Direct Spotify URI support
- ✅ Simple HTTP endpoints
- ✅ Full S1 compatibility
- ✅ Active community, well-tested
- ✅ Easy integration with existing C# code

**Cons:**
- ⚠️ Requires Node.js server running 24/7
- ⚠️ Local network only
- ⚠️ Uses "unofficial" UPnP protocol (could break in future)

**Key Endpoints:**
- `GET /zones` - Discover rooms
- `GET /{room}/spotify/now/{uri}` - Play track immediately
- `GET /{room}/spotify/queue/{uri}` - Add to queue
- `GET /{room}/pause` - Pause playback
- `GET /{room}/play` - Resume playback
- `GET /{room}/next` - Skip forward
- `GET /{room}/previous` - Skip backward
- `GET /{room}/state` - Get current playback state

**Verdict:** Best balance of simplicity and functionality

---

### Option 3: C# Native Libraries ❌

**ByteDev.Sonos (NuGet):**
- ✅ Stable, production-ready
- ❌ No Spotify URI support
- ❌ Limited queue management

**Sonos.Base (NuGet):**
- ⚠️ Experimental, beta quality
- ❌ "Far from complete at the moment"

**Verdict:** Not suitable for Spotify integration

---

## Architecture Decisions

### Question 1: Project Structure
**Decision:** Separate `Lfm.Sonos` project
**Rationale:** Clean separation of concerns, easier testing, optional dependency

### Question 2: Room Caching
**Decision:** 5-minute in-memory cache
**Rationale:** Reduces API calls, rooms don't change frequently, short enough to detect new rooms

### Question 3: Default Behavior
**Decision:** Use config default player if not specified
**Rationale:** User configures `DefaultPlayer: Sonos` at home, `Spotify` at work

### Question 4: Bridge Discovery
**Decision:** Manual configuration (no mDNS auto-discovery)
**Rationale:** Simpler implementation, one stable device (Raspberry Pi), can add later if needed

### Question 5: Album Playback
**Decision:** Queue all tracks, start playback if not already playing
**Rationale:** Consistent with Spotify behavior, better UX

---

## Implementation Plan

### Phase 0: Node.js Bridge Setup (User Task - 1 hour)

**Prerequisites:** Raspberry Pi with network access

#### Step 1: Install Node.js
```bash
# On Raspberry Pi
curl -fsSL https://deb.nodesource.com/setup_lts.x | sudo -E bash -
sudo apt-get install -y nodejs

# Verify
node --version
npm --version
```

#### Step 2: Clone and Install
```bash
# Clone repository
git clone https://github.com/jishi/node-sonos-http-api.git
cd node-sonos-http-api

# Install dependencies
npm install --production
```

#### Step 3: Configure Spotify
Create `settings.json`:
```json
{
  "spotify": {
    "clientId": "YOUR_SPOTIFY_CLIENT_ID",
    "clientSecret": "YOUR_SPOTIFY_CLIENT_SECRET"
  },
  "port": 5005
}
```

**Note:** Use same Spotify app credentials as main CLI tool

#### Step 4: Test Manually
```bash
# Start server
npm start

# Should see:
# "http server listening on port 5005"

# Test discovery (from another terminal)
curl http://localhost:5005/zones

# Should return JSON with Sonos rooms
```

#### Step 5: Setup as systemd Service (Optional)
Create `/etc/systemd/system/sonos-http-api.service`:
```ini
[Unit]
Description=Sonos HTTP API
After=network.target

[Service]
Type=simple
User=pi
WorkingDirectory=/home/pi/node-sonos-http-api
ExecStart=/usr/bin/npm start
Restart=on-failure
RestartSec=10

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl enable sonos-http-api
sudo systemctl start sonos-http-api
sudo systemctl status sonos-http-api
```

#### Verification Checklist
- [ ] Node.js installed and working
- [ ] Repository cloned and dependencies installed
- [ ] `settings.json` created with Spotify credentials
- [ ] Server starts successfully
- [ ] `/zones` endpoint returns room data
- [ ] Systemd service running (if configured)

---

### Phase 1: Sonos Service Layer (2-3 hours)

#### 1.1 Create New Project

**File:** `src/Lfm.Sonos/Lfm.Sonos.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
  </ItemGroup>
</Project>
```

#### 1.2 Create Models

**File:** `src/Lfm.Sonos/Models/SonosRoom.cs`
```csharp
namespace Lfm.Sonos.Models;

public class SonosRoom
{
    public string Name { get; set; } = string.Empty;
    public string Coordinator { get; set; } = string.Empty;
    public List<string> Members { get; set; } = new();
}
```

**File:** `src/Lfm.Sonos/Models/SonosPlaybackState.cs`
```csharp
namespace Lfm.Sonos.Models;

public class SonosPlaybackState
{
    public string CurrentTrack { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string PlaybackState { get; set; } = string.Empty; // PLAYING, PAUSED, STOPPED
    public int Volume { get; set; }
    public int TrackNumber { get; set; }
    public int? ElapsedTime { get; set; }
    public int? Duration { get; set; }
}
```

**File:** `src/Lfm.Sonos/Models/SonosConfig.cs`
```csharp
namespace Lfm.Sonos.Models;

public class SonosConfig
{
    public string HttpApiBaseUrl { get; set; } = "http://localhost:5005";
    public string? DefaultRoom { get; set; }
    public bool AutoDiscoverRooms { get; set; } = true;
    public int TimeoutMs { get; set; } = 5000;
    public int RoomCacheDurationMinutes { get; set; } = 5;
}
```

#### 1.3 Create Interface

**File:** `src/Lfm.Sonos/ISonosStreamer.cs`
```csharp
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
```

#### 1.4 Implement SonosStreamer

**File:** `src/Lfm.Sonos/SonosStreamer.cs`

Key implementation details:
- HTTP client with configurable timeout
- Room caching (in-memory, 5 minute TTL)
- Error handling with descriptive messages
- Auto-start playback if not currently playing
- URI encoding for room names with spaces

**Core Methods:**
```csharp
public class SonosStreamer : ISonosStreamer
{
    private readonly HttpClient _httpClient;
    private readonly SonosConfig _config;
    private readonly ILogger<SonosStreamer> _logger;

    // Room cache
    private List<SonosRoom>? _cachedRooms;
    private DateTime? _roomCacheExpiry;

    public async Task PlayNowAsync(string spotifyUri, string roomName)
    {
        await ValidateRoomAsync(roomName);

        // Get current state to check if playing
        var state = await GetPlaybackStateAsync(roomName);

        // Call /room/spotify/now/uri endpoint
        var encodedRoom = Uri.EscapeDataString(roomName);
        var encodedUri = Uri.EscapeDataString(spotifyUri);
        var url = $"{_config.HttpApiBaseUrl}/{encodedRoom}/spotify/now/{encodedUri}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        // If not playing, ensure playback starts
        if (state?.PlaybackState != "PLAYING")
        {
            await Task.Delay(500); // Brief delay for queue to populate
            await ResumeAsync(roomName);
        }
    }

    // Similar implementations for Queue, Pause, Resume, Skip, etc.
}
```

---

### Phase 2: Configuration Integration (1-2 hours)

#### 2.1 Update LfmConfig

**File:** `src/Lfm.Core/Configuration/LfmConfig.cs`

Add:
```csharp
using Lfm.Sonos.Models;

public class LfmConfig
{
    // Existing properties...
    public SpotifyConfig Spotify { get; set; } = new();

    // NEW: Sonos configuration
    public SonosConfig Sonos { get; set; } = new();

    // NEW: Default player selection
    public PlayerType DefaultPlayer { get; set; } = PlayerType.Spotify;
}

public enum PlayerType
{
    Spotify,
    Sonos
}
```

#### 2.2 Add Config Commands

**File:** `src/Lfm.Cli/Commands/ConfigCommand.cs`

Add methods:
```csharp
public async Task SetSonosApiUrlAsync(string url)
{
    var config = await _configManager.LoadAsync();
    config.Sonos.HttpApiBaseUrl = url;
    await _configManager.SaveAsync(config);
    Console.WriteLine($"Sonos API URL set to: {url}");
}

public async Task SetSonosDefaultRoomAsync(string roomName)
{
    var config = await _configManager.LoadAsync();
    config.Sonos.DefaultRoom = roomName;
    await _configManager.SaveAsync(config);
    Console.WriteLine($"Default Sonos room set to: {roomName}");
}

public async Task SetDefaultPlayerAsync(string playerType)
{
    if (!Enum.TryParse<PlayerType>(playerType, true, out var player))
    {
        Console.WriteLine($"Invalid player type. Valid options: Spotify, Sonos");
        return;
    }

    var config = await _configManager.LoadAsync();
    config.DefaultPlayer = player;
    await _configManager.SaveAsync(config);
    Console.WriteLine($"Default player set to: {player}");
}

public async Task ShowSonosConfigAsync()
{
    var config = await _configManager.LoadAsync();
    Console.WriteLine("Sonos Configuration:");
    Console.WriteLine($"  API URL: {config.Sonos.HttpApiBaseUrl}");
    Console.WriteLine($"  Default Room: {config.Sonos.DefaultRoom ?? "(none)"}");
    Console.WriteLine($"  Auto-discover: {config.Sonos.AutoDiscoverRooms}");
    Console.WriteLine($"  Default Player: {config.DefaultPlayer}");
}
```

**CLI Usage:**
```bash
lfm config set-sonos-api-url "http://192.168.1.100:5005"
lfm config set-sonos-default-room "Kitchen"
lfm config set-default-player sonos
lfm config show-sonos
```

---

### Phase 3: Unified Playback Service (2 hours)

#### 3.1 Create Playback Service Interface

**File:** `src/Lfm.Core/Services/IPlaybackService.cs`

```csharp
namespace Lfm.Core.Services;

public interface IPlaybackService
{
    Task<bool> IsAvailableAsync(string? target = null);
    Task PlayNowAsync(string spotifyUri, string? target = null);
    Task QueueAsync(string spotifyUri, string? target = null);
    Task PauseAsync(string? target = null);
    Task ResumeAsync(string? target = null);
    Task SkipAsync(SkipDirection direction, string? target = null);
    Task<string?> GetCurrentTrackAsync(string? target = null);
}
```

#### 3.2 Implement Routing Logic

**File:** `src/Lfm.Core/Services/PlaybackService.cs`

```csharp
public class PlaybackService : IPlaybackService
{
    private readonly IPlaylistStreamer _spotifyStreamer;
    private readonly ISonosStreamer _sonosStreamer;
    private readonly IConfigurationManager _configManager;
    private readonly ILogger<PlaybackService> _logger;

    private List<SonosRoom>? _cachedSonosRooms;

    public async Task PlayNowAsync(string spotifyUri, string? target = null)
    {
        var config = await _configManager.LoadAsync();
        var (playerType, resolvedTarget) = await DeterminePlayerAsync(target, config);

        if (playerType == PlayerType.Sonos)
        {
            await _sonosStreamer.PlayNowAsync(spotifyUri, resolvedTarget);
        }
        else
        {
            await _spotifyStreamer.PlayNowAsync(spotifyUri, resolvedTarget);
        }
    }

    private async Task<(PlayerType, string)> DeterminePlayerAsync(string? target, LfmConfig config)
    {
        // Explicit target provided
        if (!string.IsNullOrEmpty(target))
        {
            // Check if target matches a Sonos room
            if (await IsSonosRoomAsync(target))
            {
                return (PlayerType.Sonos, target);
            }

            // Assume Spotify device
            return (PlayerType.Spotify, target);
        }

        // Use config default
        if (config.DefaultPlayer == PlayerType.Sonos)
        {
            var room = config.Sonos.DefaultRoom
                ?? throw new Exception("Default player is Sonos but no default room configured");
            return (PlayerType.Sonos, room);
        }
        else
        {
            // Spotify - no explicit device needed (uses active device)
            return (PlayerType.Spotify, string.Empty);
        }
    }

    private async Task<bool> IsSonosRoomAsync(string target)
    {
        try
        {
            _cachedSonosRooms ??= await _sonosStreamer.GetRoomsAsync();
            return _cachedSonosRooms.Any(r =>
                r.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
```

---

### Phase 4: CLI Command Updates (2 hours)

#### 4.1 Add Target Option to Playback Commands

**Pattern:** Add `--target` option to all commands that play music

**Commands to update:**
- `PlayCommandBuilder`
- `QueueCommandBuilder`
- `TopTracksCommandBuilder`
- `RecommendationsCommandBuilder`
- `MixtapeCommandBuilder`

**Example:** `TopTracksCommandBuilder.cs`
```csharp
var targetOption = new Option<string?>(
    aliases: new[] { "--target", "-t" },
    description: "Playback target (Spotify device name or Sonos room name)");

command.AddOption(targetOption);

// In handler:
if (playnow)
{
    await _playbackService.PlayNowAsync(spotifyUri, target);
}
```

#### 4.2 Create Sonos Management Commands

**File:** `src/Lfm.Cli/Commands/SonosRoomsCommand.cs`
```csharp
public class SonosRoomsCommand : BaseCommand
{
    private readonly ISonosStreamer _sonosStreamer;

    public async Task ExecuteAsync(bool json = false)
    {
        var rooms = await _sonosStreamer.GetRoomsAsync();

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { rooms }, _jsonOptions));
        }
        else
        {
            Console.WriteLine("Available Sonos Rooms:");
            foreach (var room in rooms)
            {
                Console.WriteLine($"  - {room.Name}");
                if (room.Members.Count > 1)
                {
                    Console.WriteLine($"    (grouped with: {string.Join(", ", room.Members)})");
                }
            }
        }
    }
}
```

**File:** `src/Lfm.Cli/Commands/SonosStatusCommand.cs`
```csharp
public class SonosStatusCommand : BaseCommand
{
    private readonly ISonosStreamer _sonosStreamer;

    public async Task ExecuteAsync(string? room = null, bool json = false)
    {
        var config = await _configManager.LoadAsync();
        room ??= config.Sonos.DefaultRoom;

        if (string.IsNullOrEmpty(room))
        {
            Console.WriteLine("Error: No room specified and no default room configured");
            return;
        }

        var state = await _sonosStreamer.GetPlaybackStateAsync(room);

        if (state == null)
        {
            Console.WriteLine($"No playback information available for room: {room}");
            return;
        }

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(state, _jsonOptions));
        }
        else
        {
            Console.WriteLine($"Room: {room}");
            Console.WriteLine($"State: {state.PlaybackState}");
            Console.WriteLine($"Track: {state.Artist} - {state.CurrentTrack}");
            Console.WriteLine($"Album: {state.Album}");
            Console.WriteLine($"Volume: {state.Volume}%");

            if (state.ElapsedTime.HasValue && state.Duration.HasValue)
            {
                var elapsed = TimeSpan.FromSeconds(state.ElapsedTime.Value);
                var duration = TimeSpan.FromSeconds(state.Duration.Value);
                Console.WriteLine($"Position: {elapsed:mm\\:ss} / {duration:mm\\:ss}");
            }
        }
    }
}
```

**CLI Usage:**
```bash
# List available Sonos rooms
lfm sonos rooms

# Get playback status for specific room
lfm sonos status "Kitchen"

# Get status for default room
lfm sonos status

# Play track on Sonos
lfm play "Pink Floyd" --track "Comfortably Numb" --target "Kitchen"

# Queue playlist to Sonos
lfm toptracks --limit 20 --playnow --target "Living Room"
```

---

### Phase 5: Room Discovery & Caching (1 hour)

#### 5.1 Implement Room Discovery

**In SonosStreamer.cs:**
```csharp
private List<SonosRoom>? _cachedRooms;
private DateTime? _roomCacheExpiry;

public async Task<List<SonosRoom>> GetRoomsAsync()
{
    // Check cache
    if (_cachedRooms != null && _roomCacheExpiry.HasValue && DateTime.UtcNow < _roomCacheExpiry.Value)
    {
        _logger.LogDebug("Returning cached Sonos rooms");
        return _cachedRooms;
    }

    // Fetch from API
    var url = $"{_config.HttpApiBaseUrl}/zones";
    var response = await _httpClient.GetAsync(url);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();
    var zones = JsonSerializer.Deserialize<List<ZoneResponse>>(json);

    var rooms = zones?.Select(z => new SonosRoom
    {
        Name = z.Coordinator?.RoomName ?? "Unknown",
        Coordinator = z.Coordinator?.Uuid ?? string.Empty,
        Members = z.Members?.Select(m => m.RoomName).ToList() ?? new()
    }).ToList() ?? new();

    // Update cache
    _cachedRooms = rooms;
    _roomCacheExpiry = DateTime.UtcNow.AddMinutes(_config.RoomCacheDurationMinutes);

    _logger.LogInformation("Discovered {Count} Sonos rooms", rooms.Count);
    return rooms;
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
```

#### 5.2 Response Models

**Internal models for parsing `/zones` response:**
```csharp
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
```

---

### Phase 6: MCP Integration (1-2 hours)

#### 6.1 Update Existing Tools

**File:** `lfm-mcp-release/server.js`

**Add `target` parameter to playback tools:**

```javascript
// lfm_play_now
{
  name: 'lfm_play_now',
  description: 'Play a track or album immediately on Spotify or Sonos. IMPORTANT: If multiple album versions exist for a track (e.g., studio, live, greatest hits), you MUST specify the album parameter. Users typically prefer studio albums over live/greatest hits versions unless explicitly requested.',
  inputSchema: {
    type: 'object',
    properties: {
      artist: {
        type: 'string',
        description: 'Artist name'
      },
      track: {
        type: 'string',
        description: 'Track name (required if album not specified)'
      },
      album: {
        type: 'string',
        description: 'Album name (required if track not specified, or when multiple versions exist)'
      },
      target: {  // NEW
        type: 'string',
        description: 'Playback target (Spotify device name or Sonos room name). If not specified, uses config default player.'
      }
    },
    required: ['artist']
  }
}

// Similar updates for:
// - lfm_queue
// - lfm_toptracks
// - lfm_recommendations
// - lfm_mixtape
```

#### 6.2 Add New Sonos Tools

```javascript
// lfm_sonos_rooms
{
  name: 'lfm_sonos_rooms',
  description: 'List all available Sonos rooms for playback',
  inputSchema: {
    type: 'object',
    properties: {}
  }
}

// lfm_sonos_status
{
  name: 'lfm_sonos_status',
  description: 'Get current playback status for a Sonos room',
  inputSchema: {
    type: 'object',
    properties: {
      room: {
        type: 'string',
        description: 'Sonos room name (uses default if not specified)'
      }
    }
  }
}
```

#### 6.3 Update Tool Handlers

```javascript
// Add to tool handlers
if (name === 'lfm_sonos_rooms') {
  try {
    const output = await executeLfmCommand(['sonos', 'rooms', '--json']);
    const result = parseJsonOutput(output);
    return {
      content: [{
        type: 'text',
        text: JSON.stringify(result, null, 2)
      }]
    };
  } catch (error) {
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({ success: false, error: error.message }, null, 2)
      }],
      isError: true
    };
  }
}

// Similar handler for lfm_sonos_status
```

#### 6.4 Update MCP Guidelines

**File:** `lfm-mcp-release/lfm-guidelines.md`

Add section:

```markdown
## Sonos Playback

The user has Sonos speakers at home in addition to Spotify devices. Music can be played on either system.

### Playback Target Selection

**Automatic Detection:**
- When `target` parameter is provided, the system automatically detects whether it's a Spotify device or Sonos room
- If no `target` specified, uses the user's configured `DefaultPlayer` (Sonos at home, Spotify at work)

**Common Scenarios:**
```javascript
// At home (Sonos)
lfm_play_now(artist: "Pink Floyd", track: "Comfortably Numb", target: "Kitchen")

// At work (Spotify)
lfm_play_now(artist: "David Bowie", track: "Heroes", target: "DESKTOP-WORK")

// Use default (based on config)
lfm_play_now(artist: "Radiohead", track: "Paranoid Android") // Uses DefaultPlayer
```

### Available Sonos Rooms

Use `lfm_sonos_rooms` to discover available rooms:
```javascript
lfm_sonos_rooms()
// Returns: { rooms: ["Kitchen", "Living Room", "Bedroom"] }
```

### Current Playback Status

Check what's playing in a Sonos room:
```javascript
lfm_sonos_status(room: "Kitchen")
// Returns: { state: "PLAYING", artist: "...", track: "...", volume: 50 }
```

### Auto-Start Playback

When playing on Sonos, if the room is not currently playing, playback will automatically start. This is transparent to you - just call `lfm_play_now` and it works.

### Limitations

- Sonos integration requires node-sonos-http-api bridge running on user's network
- If bridge is offline, operations will fail with an error message
- Sonos playback is local network only (unlike Spotify which works remotely)

### Room Grouping (Future)

**Note:** Room grouping (playing to multiple Sonos rooms simultaneously) is planned for a future version.
```

---

### Phase 7: Error Handling & Validation (1 hour)

#### 7.1 Bridge Availability Check

```csharp
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
```

#### 7.2 Enhanced Error Messages

```csharp
public async Task PlayNowAsync(string spotifyUri, string roomName)
{
    // Check bridge availability
    if (!await IsAvailableAsync())
    {
        throw new InvalidOperationException(
            $"Sonos bridge (node-sonos-http-api) is not available at {_config.HttpApiBaseUrl}. " +
            $"Ensure the Node.js server is running and accessible.");
    }

    // Validate room exists
    await ValidateRoomAsync(roomName);

    // Attempt playback
    try
    {
        var encodedRoom = Uri.EscapeDataString(roomName);
        var encodedUri = Uri.EscapeDataString(spotifyUri);
        var url = $"{_config.HttpApiBaseUrl}/{encodedRoom}/spotify/now/{encodedUri}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Started playback on Sonos room '{Room}': {Uri}", roomName, spotifyUri);
    }
    catch (HttpRequestException ex)
    {
        throw new InvalidOperationException(
            $"Failed to play on Sonos room '{roomName}'. Check that Spotify is linked to your Sonos account.", ex);
    }
}
```

#### 7.3 Timeout Configuration

```csharp
public SonosStreamer(SonosConfig config, ILogger<SonosStreamer> logger)
{
    _config = config;
    _logger = logger;

    _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromMilliseconds(config.TimeoutMs)
    };
}
```

---

### Phase 8: Testing & Validation (2 hours)

#### 8.1 Unit Testing Checklist

**Test: Room Discovery**
```bash
# Verify bridge is accessible
curl http://localhost:5005/zones

# Test CLI command
./publish/win-x64/lfm.exe sonos rooms

# Expected: List of room names
```

**Test: Configuration**
```bash
# Set config values
lfm config set-sonos-api-url "http://localhost:5005"
lfm config set-sonos-default-room "Kitchen"
lfm config set-default-player sonos

# Verify saved
lfm config show-sonos
```

**Test: Playback on Sonos**
```bash
# Play track
lfm play "Pink Floyd" --track "Comfortably Numb" --target "Kitchen"

# Expected: Track plays on Kitchen Sonos

# Queue track
lfm queue "The Beatles" --track "Come Together" --target "Kitchen"

# Expected: Track added to queue
```

**Test: Playlist Generation**
```bash
# Top tracks to Sonos
lfm toptracks --limit 10 --playnow --target "Kitchen"

# Expected: Playlist queued and starts playing

# Recommendations to Sonos
lfm recommendations --limit 20 --playlist --target "Living Room"

# Expected: Playlist queued (doesn't auto-start)
```

**Test: Auto-Start Playback**
```bash
# Stop Sonos manually (pause or turn off)
# Then play track
lfm play "Radiohead" --track "Paranoid Android" --target "Kitchen"

# Expected: Track starts playing even though Sonos was stopped
```

**Test: Default Player**
```bash
# Set default to Sonos
lfm config set-default-player sonos

# Play without target
lfm play "David Bowie" --track "Heroes"

# Expected: Plays on default Sonos room
```

**Test: Spotify Still Works**
```bash
# Play on Spotify device
lfm play "Radiohead" --track "Karma Police" --target "DESKTOP-WORK"

# Expected: Plays on Spotify device, not Sonos
```

**Test: Error Handling**
```bash
# Stop bridge
sudo systemctl stop sonos-http-api

# Attempt playback
lfm play "Test" --target "Kitchen"

# Expected: Clear error about bridge being unavailable

# Invalid room name
sudo systemctl start sonos-http-api
lfm play "Test" --target "InvalidRoom"

# Expected: Error listing available rooms
```

#### 8.2 MCP Testing with Claudette

**Scenario 1: Natural Language Playback**
```
User: "Play Comfortably Numb by Pink Floyd in the kitchen"

Expected LLM behavior:
1. Uses lfm_play_now
2. Parameters: artist="Pink Floyd", track="Comfortably Numb", target="Kitchen"
3. Music starts playing on Kitchen Sonos
```

**Scenario 2: Room Discovery**
```
User: "What Sonos rooms do I have?"

Expected LLM behavior:
1. Uses lfm_sonos_rooms
2. Returns list of rooms
3. Presents to user in natural language
```

**Scenario 3: Status Check**
```
User: "What's playing in the living room?"

Expected LLM behavior:
1. Uses lfm_sonos_status with room="Living Room"
2. Returns current track info
3. Presents: "Currently playing [Track] by [Artist] from [Album]"
```

**Scenario 4: Playlist to Sonos**
```
User: "Queue my top 20 tracks from last year to the kitchen speakers"

Expected LLM behavior:
1. Uses lfm_toptracks
2. Parameters: limit=20, year="2024", playnow=true, target="Kitchen"
3. Playlist generated and queued to Sonos
```

**Scenario 5: Default Player**
```
User: "Play some Pink Floyd"

Expected LLM behavior:
1. Uses lfm_play_now without target (relies on DefaultPlayer config)
2. Plays on Sonos if user is at home (DefaultPlayer=Sonos)
3. Plays on Spotify if user is at work (DefaultPlayer=Spotify)
```

#### 8.3 Integration Testing Matrix

| Scenario | Expected Result | Status |
|----------|----------------|--------|
| Bridge online, valid room | Play succeeds | ✅ |
| Bridge offline | Clear error message | ✅ |
| Invalid room name | Error with available rooms | ✅ |
| Sonos stopped, play command | Auto-starts playback | ✅ |
| Multiple tracks queued | All tracks queued, playback starts | ✅ |
| Spotify device specified | Routes to Spotify API | ✅ |
| Sonos room specified | Routes to Sonos API | ✅ |
| No target, DefaultPlayer=Sonos | Uses Sonos default room | ✅ |
| No target, DefaultPlayer=Spotify | Uses Spotify active device | ✅ |
| Room discovery cache | Second call faster | ✅ |
| Grouped rooms | Shows group members | ✅ |

---

## Project Structure

### New Files

```
src/Lfm.Sonos/                              # New project
├── Lfm.Sonos.csproj                        # Project file
├── ISonosStreamer.cs                       # Public interface
├── SonosStreamer.cs                        # Implementation
└── Models/
    ├── SonosRoom.cs                        # Room model
    ├── SonosPlaybackState.cs               # Playback state
    └── SonosConfig.cs                      # Configuration

src/Lfm.Core/
├── Configuration/
│   └── LfmConfig.cs                        # Add Sonos config, PlayerType enum
└── Services/
    ├── IPlaybackService.cs                 # NEW: Unified abstraction
    └── PlaybackService.cs                  # NEW: Routes Spotify/Sonos

src/Lfm.Cli/
├── Commands/
│   ├── SonosRoomsCommand.cs               # NEW: List rooms
│   ├── SonosStatusCommand.cs              # NEW: Playback status
│   └── ConfigCommand.cs                    # Add Sonos config methods
└── CommandBuilders/
    ├── SonosRoomsCommandBuilder.cs        # NEW
    ├── SonosStatusCommandBuilder.cs       # NEW
    ├── PlayCommandBuilder.cs              # Add --target option
    ├── QueueCommandBuilder.cs             # Add --target option
    ├── TopTracksCommandBuilder.cs         # Add --target option
    ├── RecommendationsCommandBuilder.cs   # Add --target option
    └── MixtapeCommandBuilder.cs           # Add --target option

lfm-mcp-release/
├── server.js                               # Update tool schemas
└── lfm-guidelines.md                       # Add Sonos guidance
```

### Modified Files

```
src/Lfm.Cli/Program.cs                      # DI registration
src/Lfm.Core/Configuration/LfmConfig.cs     # Sonos config + PlayerType
README.md                                    # Document Sonos features
```

---

## Configuration Examples

### Home Configuration (Sonos Default)
```json
{
  "DefaultPlayer": "Sonos",
  "Sonos": {
    "HttpApiBaseUrl": "http://192.168.1.100:5005",
    "DefaultRoom": "Kitchen",
    "AutoDiscoverRooms": true,
    "TimeoutMs": 5000,
    "RoomCacheDurationMinutes": 5
  },
  "Spotify": {
    "ClientId": "...",
    "ClientSecret": "...",
    "DefaultDevice": null
  }
}
```

### Work Configuration (Spotify Default)
```json
{
  "DefaultPlayer": "Spotify",
  "Spotify": {
    "ClientId": "...",
    "ClientSecret": "...",
    "DefaultDevice": "DESKTOP-WORK"
  },
  "Sonos": {
    "HttpApiBaseUrl": "http://localhost:5005",
    "DefaultRoom": null,
    "AutoDiscoverRooms": true,
    "TimeoutMs": 5000,
    "RoomCacheDurationMinutes": 5
  }
}
```

---

## Future Enhancements (v2)

### Room Grouping
**Description:** Play music to multiple Sonos rooms simultaneously

**API Endpoints:**
- `GET /{room}/join/{other}` - Join rooms into group
- `GET /{room}/leave` - Leave group

**Use Case:**
```bash
lfm sonos group "Kitchen" "Living Room" "Bedroom"
lfm play "Pink Floyd" --album "Dark Side of the Moon" --target "Kitchen"
# Plays on all grouped rooms

lfm sonos ungroup "Kitchen"
# Kitchen leaves group
```

**Implementation Notes:**
- Track current group state
- Auto-group detection in room discovery
- MCP tool: `lfm_sonos_group`

---

### Volume Control
**Description:** Adjust volume for Sonos rooms

**API Endpoints:**
- `GET /{room}/volume/{level}` - Set volume (0-100)
- `GET /{room}/volume/+{amount}` - Increase volume
- `GET /{room}/volume/-{amount}` - Decrease volume

**Use Case:**
```bash
lfm sonos volume "Kitchen" 50
lfm sonos volume "Kitchen" +10
```

---

### Playlist Management
**Description:** Save/load Sonos-specific playlists

**Use Case:**
```bash
lfm toptracks --limit 50 --save-sonos-playlist "My Top 50"
lfm sonos playlists
lfm sonos play-playlist "My Top 50" --target "Kitchen"
```

---

### Multi-Room Scheduling
**Description:** Schedule different playlists for different rooms

**Use Case:**
```bash
# Morning: energetic music in kitchen
lfm sonos schedule "Kitchen" --time "07:00" --playlist "Morning Energy"

# Evening: relaxing music in living room
lfm sonos schedule "Living Room" --time "19:00" --playlist "Evening Chill"
```

---

### Advanced Discovery
**Description:** Auto-discover bridge via mDNS/Bonjour

**Implementation:**
- Detect node-sonos-http-api on local network
- No manual IP configuration needed
- Fallback to manual config if discovery fails

---

## Risk Assessment

### Technical Risks

**Risk 1: UPnP Protocol Changes**
- **Likelihood:** Low-Medium
- **Impact:** High (breaks Sonos integration)
- **Mitigation:** node-sonos-http-api maintained since 2015, active community, widely used in Home Assistant
- **Fallback:** Could migrate to Official Sonos API if needed (would require significant rewrite)

**Risk 2: Bridge Availability**
- **Likelihood:** Medium (network issues, Pi offline)
- **Impact:** Medium (Sonos unusable, but Spotify still works)
- **Mitigation:**
  - Clear error messages
  - IsAvailableAsync check before operations
  - Spotify fallback always available

**Risk 3: Spotify Account Linking**
- **Likelihood:** Low (user setup issue)
- **Impact:** Medium (Spotify on Sonos doesn't work)
- **Mitigation:**
  - Documentation for linking Spotify to Sonos
  - Clear error messages from bridge

---

## Success Criteria

### Functional Requirements
- ✅ Play Spotify tracks/albums on Sonos rooms
- ✅ Queue tracks to Sonos
- ✅ Control playback (pause, resume, skip)
- ✅ Auto-discover Sonos rooms
- ✅ Config-driven default player selection
- ✅ Spotify integration still works independently
- ✅ MCP tools updated for Sonos support

### Quality Requirements
- ✅ Clean build (0 warnings, 0 errors)
- ✅ Consistent architecture with existing codebase
- ✅ Proper error handling with user-friendly messages
- ✅ Room caching reduces repeated API calls
- ✅ Auto-start playback improves UX

### Performance Requirements
- ✅ Room discovery < 1 second (first call)
- ✅ Room discovery < 50ms (cached)
- ✅ Play command < 2 seconds (including track search)
- ✅ Cache hit rate > 80% for room lookups

---

## Implementation Timeline

| Phase | Task | Hours | Dependencies |
|-------|------|-------|--------------|
| 0 | Node.js bridge setup | 1 | Raspberry Pi |
| 1 | Sonos service layer | 2-3 | Phase 0 |
| 2 | Configuration integration | 1-2 | Phase 1 |
| 3 | Unified playback service | 2 | Phase 2 |
| 4 | CLI command updates | 2 | Phase 3 |
| 5 | Room discovery & caching | 1 | Phase 1 |
| 6 | MCP integration | 1-2 | Phase 4 |
| 7 | Error handling | 1 | All |
| 8 | Testing & validation | 2 | All |
| **Total** | | **12-16 hours** | |

---

## Next Steps

### Before Starting Implementation

1. **User completes Phase 0:**
   - [ ] Install Node.js on Raspberry Pi
   - [ ] Clone node-sonos-http-api repository
   - [ ] Create settings.json with Spotify credentials
   - [ ] Start bridge and verify `/zones` endpoint
   - [ ] (Optional) Setup systemd service

2. **Verify Prerequisites:**
   - [ ] Spotify credentials work with bridge
   - [ ] Sonos rooms discovered correctly
   - [ ] Bridge accessible from development machine

### Implementation Order

1. Start with Phase 1 (Sonos service layer)
2. Test each phase incrementally before moving to next
3. Use TodoWrite to track progress
4. Commit after each major phase completes
5. Full integration testing in Phase 8

---

## Questions & Decisions Log

**Q1:** Project structure - separate or combined?
**A1:** Separate `Lfm.Sonos` project for clean architecture ✅

**Q2:** Room caching duration?
**A2:** 5 minutes in-memory cache ✅

**Q3:** Default behavior when no target specified?
**A3:** Use config DefaultPlayer setting ✅

**Q4:** Bridge auto-discovery via mDNS?
**A4:** Manual config for v1, consider mDNS for v2 ✅

**Q5:** Album playback behavior?
**A5:** Queue all tracks, auto-start if not playing ✅

**Q6:** Room grouping support?
**A6:** Future enhancement (v2) ✅

**Q7:** Auto-start playback when queuing?
**A7:** Yes - if Sonos not currently playing, start playback after queueing ✅

---

## References

- **node-sonos-http-api:** https://github.com/jishi/node-sonos-http-api
- **Official Sonos API:** https://docs.sonos.com/
- **Spotify Web API:** https://developer.spotify.com/documentation/web-api/
- **Sonos S1 Release Notes:** https://developer.sonos.com/release-notes/

---

**Document Status:** Complete Implementation Plan
**Ready for Implementation:** Yes (pending Phase 0 completion)
**Next Session:** Start Phase 1 after verifying Node.js bridge is running
