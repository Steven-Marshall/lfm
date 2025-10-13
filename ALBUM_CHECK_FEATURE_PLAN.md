# Album Check Feature - Full Implementation Plan

**Date**: 2025-09-30
**Status**: Ready for Implementation (Next Session)
**Approach**: Single-phase full implementation with track-level analysis

## Feature Overview

Add comprehensive album checking with track-level play count breakdown for detailed listening analysis.

### User Stories

1. **Basic Check**: "Have I listened to this album?"
   ```bash
   lfm check "Taylor Swift" --album "1989"
   # Taylor Swift - 1989: 491 plays (13 tracks)
   ```

2. **Detailed Analysis**: Track-level breakdown with listening patterns
   ```bash
   lfm check "Taylor Swift" --album "1989" --verbose
   # Taylor Swift - 1989: 491 plays (13 tracks)
   #
   # Track Breakdown:
   #   1. Welcome to New York: 28 plays (6%)
   #   2. Blank Space: 89 plays (18%) ← Most played
   #   3. Style: 76 plays (15%)
   #   4. Out of the Woods: 31 plays (6%)
   #   5. Shake It Off: 71 plays (14%)
   #   ... (remaining 8 tracks)
   #
   # Listening Pattern: Heavy rotation on 3 tracks (36% of plays)
   ```

3. **MCP Integration**: LLM can check album listening history
   ```javascript
   lfm_check(artist: "Black Country, New Road", album: "Ants From Up There")
   // Returns: 690 plays, 10 tracks, track breakdown for analysis
   ```

4. **LLM Context**: Guidelines help interpret track count weighting
   - Mozart opera (781 plays, 80 tracks) vs Taylor Swift (491 plays, 13 tracks)
   - Classical/opera: exploratory listening across many tracks
   - Pop/rock: intentional repeated listening to favorite tracks

## API Analysis

### Last.fm API Calls Required

**Album Info**: `album.getInfo`
```
Parameters: artist, album, username, autocorrect=1
Returns:
  - album.name
  - album.artist
  - album.userplaycount (total plays across all tracks)
  - album.tracks.track[] array:
    - track.name
    - track.duration
    - track.url
    - track.artist (nested)
```

**Per-Track Info**: `track.getInfo` (called N times for N tracks)
```
Parameters: artist, track, username
Returns:
  - track.name
  - track.userplaycount (user plays for this specific track)
  - track.artist
  - track.album
```

**Total API Calls**: 1 (album) + N (tracks) = N+1 calls

### Performance Analysis

**Typical Album** (13 tracks):
- API calls: 14 (1 album + 13 tracks)
- Throttle: 100ms between calls
- Time: ~1.4 seconds
- Cached: Instant (future lookups)

**Opera Album** (80 tracks):
- API calls: 81 (1 album + 80 tracks)
- Throttle: 100ms between calls
- Time: ~8.1 seconds
- Cached: Instant (future lookups)

**Assessment**: ✅ Reasonable performance, caching makes it fast for repeat lookups

## Implementation Plan

### Phase 1: Core Infrastructure (2 hours)

#### 1. Create AlbumLookupInfo Model
**File**: `src/Lfm.Core/Models/AlbumLookupInfo.cs`

```csharp
public class AlbumLookupInfo
{
    [JsonPropertyName("album")]
    public AlbumDetails Album { get; set; } = new();

    public class AlbumDetails
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("artist")]
        public string Artist { get; set; } = string.Empty;

        [JsonPropertyName("userplaycount")]
        public string? UserPlaycount { get; set; }

        [JsonPropertyName("listeners")]
        public string Listeners { get; set; } = "0";

        [JsonPropertyName("playcount")]
        public string Playcount { get; set; } = "0";

        [JsonPropertyName("tracks")]
        public TracksWrapper? Tracks { get; set; }

        [JsonPropertyName("tags")]
        public TagsWrapper? Tags { get; set; }

        public int GetUserPlaycount() =>
            int.TryParse(UserPlaycount, out var count) ? count : 0;

        public int GetGlobalPlaycount() =>
            int.TryParse(Playcount, out var count) ? count : 0;

        public int GetTrackCount() =>
            Tracks?.Track?.Count ?? 0;
    }

    public class TracksWrapper
    {
        [JsonPropertyName("track")]
        public List<AlbumTrack> Track { get; set; } = new();
    }

    public class AlbumTrack
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public string Duration { get; set; } = "0";

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("@attr")]
        public TrackAttributes? Attributes { get; set; }

        // Will be populated separately via track.getInfo calls
        public int UserPlaycount { get; set; } = 0;
    }

    public class TrackAttributes
    {
        [JsonPropertyName("rank")]
        public string Rank { get; set; } = string.Empty;
    }

    public class TagsWrapper
    {
        [JsonPropertyName("tag")]
        public List<Tag> Tag { get; set; } = new();
    }

    public class Tag
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }
}
```

#### 2. Add API Client Methods
**File**: `src/Lfm.Core/Services/LastFmApiClient.cs`

Add to `ILastFmApiClient` interface:
```csharp
// Lookup methods for checking user's listening history
Task<AlbumLookupInfo?> GetAlbumInfoAsync(string artist, string album, string username);
Task<Result<AlbumLookupInfo>> GetAlbumInfoWithResultAsync(string artist, string album, string username);
```

Implement in `LastFmApiClient` class:
```csharp
public async Task<AlbumLookupInfo?> GetAlbumInfoAsync(string artist, string album, string username)
{
    var parameters = new Dictionary<string, string>
    {
        ["method"] = "album.getInfo",
        ["artist"] = artist,
        ["album"] = album,
        ["username"] = username,
        ["autocorrect"] = "1"
    };

    try
    {
        var response = await CallApiAsync(parameters);

        if (_enableDebugLogging)
        {
            _logger.LogDebug("Album info response: {Response}", response);
        }

        return JsonSerializer.Deserialize<AlbumLookupInfo>(response);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to deserialize album info response");
        return null;
    }
}

public async Task<Result<AlbumLookupInfo>> GetAlbumInfoWithResultAsync(string artist, string album, string username)
{
    return await ExecuteWithResultAsync(
        () => GetAlbumInfoAsync(artist, album, username),
        $"getting album info for {artist} - {album}");
}
```

#### 3. Cached Client Integration
**File**: `src/Lfm.Core/Services/CachedLastFmApiClient.cs`

Add wrapping methods with cache support:
```csharp
public async Task<AlbumLookupInfo?> GetAlbumInfoAsync(string artist, string album, string username)
{
    return await ExecuteWithCacheAsync(
        () => _apiClient.GetAlbumInfoAsync(artist, album, username),
        CacheBehavior.Normal,
        "album.getInfo",
        artist, album, username);
}

public async Task<Result<AlbumLookupInfo>> GetAlbumInfoWithResultAsync(string artist, string album, string username)
{
    return await ExecuteWithCacheAndResultAsync(
        () => _apiClient.GetAlbumInfoAsync(artist, album, username),
        CacheBehavior.Normal,
        "album.getInfo",
        artist, album, username);
}
```

Update `HasData<T>()` method to include AlbumLookupInfo:
```csharp
private bool HasData<T>(T result) where T : class
{
    return result switch
    {
        TopArtists artists => artists.Artists?.Any() == true,
        TopTracks tracks => tracks.Tracks?.Any() == true,
        TopAlbums albums => albums.Albums?.Any() == true,
        RecentTracks recentTracks => recentTracks.Tracks?.Any() == true,
        SimilarArtists similarArtists => similarArtists.Artists?.Any() == true,
        TopTags tags => tags.Tags?.Any() == true,
        ArtistLookupInfo artistLookup => !string.IsNullOrEmpty(artistLookup.Artist?.Name),
        TrackLookupInfo trackLookup => !string.IsNullOrEmpty(trackLookup.Track?.Name),
        AlbumLookupInfo albumLookup => !string.IsNullOrEmpty(albumLookup.Album?.Name), // NEW
        _ => true
    };
}
```

### Phase 2: CLI Command (2.5 hours)

#### 4. Extend CheckCommand
**File**: `src/Lfm.Cli/Commands/CheckCommand.cs`

Add third overload for album checking:
```csharp
/// <summary>
/// Check if user has listened to a specific album with track-level breakdown
/// </summary>
public async Task<int> ExecuteAsync(
    string artist,
    string album,
    string? username = null,
    bool timing = false,
    bool verbose = false,
    bool json = false)
{
    var config = await _configManager.LoadAsync();
    var user = username ?? config.DefaultUsername;

    if (string.IsNullOrEmpty(user))
    {
        _logger.LogError("Username not specified and no default username configured");
        return 1;
    }

    var stopwatch = timing ? System.Diagnostics.Stopwatch.StartNew() : null;

    try
    {
        if (verbose && !json)
            _logger.LogInformation("Checking listening history for album: {Artist} - {Album}", artist, album);

        // Get album info
        var result = await _apiClient.GetAlbumInfoWithResultAsync(artist, album, user);

        if (!result.Success)
        {
            if (json)
            {
                var errorOutput = new { success = false, error = result.Error?.Message ?? "Unknown error" };
                Console.WriteLine(JsonSerializer.Serialize(errorOutput, _jsonOptions));
            }
            else
            {
                _logger.LogError("Failed to check album: {Error}", result.Error?.Message ?? "Unknown error");
            }
            return 1;
        }

        var albumInfo = result.Data;
        var userPlaycount = albumInfo.Album.GetUserPlaycount();
        var trackCount = albumInfo.Album.GetTrackCount();

        // If verbose, fetch per-track play counts
        List<TrackPlayInfo>? trackBreakdown = null;
        if (verbose && albumInfo.Album.Tracks?.Track != null)
        {
            trackBreakdown = await FetchTrackPlaycounts(
                albumInfo.Album.Tracks.Track,
                artist,
                user,
                config.ApiThrottleMs);
        }

        stopwatch?.Stop();

        // Output results
        if (json)
        {
            OutputAlbumJson(albumInfo, trackBreakdown, stopwatch);
        }
        else
        {
            OutputAlbumConsole(albumInfo, trackBreakdown, verbose, stopwatch);
        }

        return 0;
    }
    catch (Exception ex)
    {
        if (json)
        {
            var errorOutput = new { success = false, error = ex.Message };
            Console.WriteLine(JsonSerializer.Serialize(errorOutput, _jsonOptions));
        }
        else
        {
            _logger.LogError(ex, "Error checking album listening history");
            Console.WriteLine($"Error: {ex.Message}");
        }
        return 1;
    }
}

private async Task<List<TrackPlayInfo>> FetchTrackPlaycounts(
    List<AlbumTrack> tracks,
    string artist,
    string username,
    int throttleMs)
{
    var trackPlays = new List<TrackPlayInfo>();

    foreach (var track in tracks)
    {
        var trackInfo = await _apiClient.GetTrackInfoAsync(artist, track.Name, username);

        trackPlays.Add(new TrackPlayInfo
        {
            Name = track.Name,
            UserPlaycount = trackInfo?.Track.GetUserPlaycount() ?? 0
        });

        // Throttle to respect API limits
        if (throttleMs > 0)
            await Task.Delay(throttleMs);
    }

    return trackPlays;
}

private void OutputAlbumConsole(
    AlbumLookupInfo albumInfo,
    List<TrackPlayInfo>? trackBreakdown,
    bool verbose,
    Stopwatch? stopwatch)
{
    var album = albumInfo.Album;
    var userPlaycount = album.GetUserPlaycount();
    var trackCount = album.GetTrackCount();

    if (userPlaycount == 0)
    {
        Console.WriteLine($"{album.Artist} - {album.Name}: Never played");
    }
    else
    {
        Console.WriteLine($"{album.Artist} - {album.Name}: {userPlaycount:N0} plays ({trackCount} tracks)");

        if (trackBreakdown != null && trackBreakdown.Any())
        {
            Console.WriteLine("\nTrack Breakdown:");

            var totalTrackPlays = trackBreakdown.Sum(t => t.UserPlaycount);
            var maxPlays = trackBreakdown.Max(t => t.UserPlaycount);

            for (int i = 0; i < trackBreakdown.Count; i++)
            {
                var track = trackBreakdown[i];
                var percentage = totalTrackPlays > 0
                    ? (track.UserPlaycount * 100.0 / totalTrackPlays)
                    : 0;

                var indicator = track.UserPlaycount == maxPlays ? " ← Most played" : "";
                Console.WriteLine($"  {i + 1,2}. {track.Name,-50} {track.UserPlaycount,4} plays ({percentage:F0}%){indicator}");
            }

            // Simple pattern analysis
            var topThreePlays = trackBreakdown.OrderByDescending(t => t.UserPlaycount).Take(3).Sum(t => t.UserPlaycount);
            var topThreePercentage = totalTrackPlays > 0 ? (topThreePlays * 100.0 / totalTrackPlays) : 0;

            if (topThreePercentage > 50)
            {
                Console.WriteLine($"\nListening Pattern: Heavy rotation on top 3 tracks ({topThreePercentage:F0}% of plays)");
            }
            else
            {
                Console.WriteLine($"\nListening Pattern: Balanced listening across album");
            }
        }
    }

    if (verbose && !trackBreakdown)
    {
        Console.WriteLine($"Global plays: {album.GetGlobalPlaycount():N0}");
        if (album.Tags?.Tag.Any() == true)
        {
            var tags = string.Join(", ", album.Tags.Tag.Take(5).Select(t => t.Name));
            Console.WriteLine($"Tags: {tags}");
        }
    }

    if (timing && stopwatch != null)
    {
        Console.WriteLine($"\nResponse time: {stopwatch.ElapsedMilliseconds}ms");
    }
}

private void OutputAlbumJson(
    AlbumLookupInfo albumInfo,
    List<TrackPlayInfo>? trackBreakdown,
    Stopwatch? stopwatch)
{
    var album = albumInfo.Album;

    var output = new
    {
        success = true,
        artist = album.Artist,
        album = album.Name,
        userPlaycount = album.GetUserPlaycount(),
        trackCount = album.GetTrackCount(),
        globalPlaycount = album.GetGlobalPlaycount(),
        tracks = trackBreakdown?.Select(t => new
        {
            name = t.Name,
            userPlaycount = t.UserPlaycount
        }).ToList(),
        tags = album.Tags?.Tag.Select(t => t.Name).ToList(),
        responseTimeMs = stopwatch?.ElapsedMilliseconds
    };

    Console.WriteLine(JsonSerializer.Serialize(output, _jsonOptions));
}

private class TrackPlayInfo
{
    public string Name { get; set; } = string.Empty;
    public int UserPlaycount { get; set; }
}
```

#### 5. Update CheckCommandBuilder
**File**: `src/Lfm.Cli/CommandBuilders/CheckCommandBuilder.cs`

Add album option:
```csharp
var albumOption = new Option<string?>(
    aliases: new[] { "--album", "-a" },
    description: "Album name to check (requires artist parameter)");

// Update command handler logic to detect album parameter
command.SetHandler(async (artist, track, album, username, timing, verbose, json) =>
{
    var checkCommand = serviceProvider.GetRequiredService<CheckCommand>();

    if (!string.IsNullOrEmpty(album))
    {
        // Album check
        return await checkCommand.ExecuteAsync(artist, album, username, timing, verbose, json);
    }
    else if (!string.IsNullOrEmpty(track))
    {
        // Track check
        return await checkCommand.ExecuteAsync(artist, track, username, timing, verbose);
    }
    else
    {
        // Artist check
        return await checkCommand.ExecuteAsync(artist, username, timing, verbose);
    }
}, artistArgument, trackOption, albumOption, usernameOption, timingOption, verboseOption, jsonOption);
```

### Phase 3: MCP Integration (1 hour)

#### 6. Update MCP Server
**File**: `lfm-mcp-release/server.js`

Update `lfm_check` tool schema:
```javascript
{
  name: 'lfm_check',
  description: 'Check if user has listened to an artist, track, or album',
  inputSchema: {
    type: 'object',
    properties: {
      artist: {
        type: 'string',
        description: 'Artist name to check'
      },
      track: {
        type: 'string',
        description: 'Track name to check (optional - if not provided, checks artist only)'
      },
      album: {  // NEW
        type: 'string',
        description: 'Album name to check (optional - if provided, checks album with optional track breakdown)'
      },
      user: {
        type: 'string',
        description: 'Last.fm username (uses default if not specified)'
      },
      verbose: {  // NEW
        type: 'boolean',
        description: 'Include detailed track-level breakdown for albums',
        default: false
      }
    },
    required: ['artist']
  }
}
```

Update handler:
```javascript
if (name === 'lfm_check') {
  try {
    const artist = args.artist;
    const track = args.track;
    const album = args.album;  // NEW
    const user = args.user;
    const verbose = args.verbose || false;  // NEW

    const cmdArgs = ['check', artist];

    if (album) {
      cmdArgs.push('--album', album);
      if (verbose) {
        cmdArgs.push('--verbose');
      }
    } else if (track) {
      cmdArgs.push('--track', track);
    }

    if (user) {
      cmdArgs.push('--user', user);
    }

    if (verbose) {
      cmdArgs.push('--verbose');
    }

    cmdArgs.push('--json');

    const output = await executeLfmCommand(cmdArgs);
    const result = parseJsonOutput(output);

    return {
      content: [
        {
          type: 'text',
          text: JSON.stringify(result, null, 2)
        }
      ]
    };
  } catch (error) {
    return {
      content: [
        {
          type: 'text',
          text: `Error checking listening history: ${error.message}`
        }
      ],
      isError: true
    };
  }
}
```

### Phase 4: Guidelines & Documentation (30 mins)

#### 7. Update MCP Guidelines
**File**: `lfm-mcp-release/lfm-guidelines.md`

Add new section:
```markdown
## Understanding Album Play Counts

Album play counts are the sum of all track plays from that album. **Be cautious about
comparing album play counts across genres or formats:**

### Track Count Variations
- **Classical/opera albums**: Often 40-80+ tracks (e.g., Mozart's Le Nozze di Figaro)
- **Pop/rock albums**: Typically 10-15 tracks (e.g., Taylor Swift's 1989)
- **Box sets & compilations**: Can have 30-60+ tracks (e.g., The Magnetic Fields' 69 Love Songs)
- **Singles/EPs**: Usually 3-5 tracks

### Interpretation Guidelines

**Example Comparison**:
- Le Nozze di Figaro: 781 plays across 80 tracks = ~10 plays per track (deep engagement with full opera)
- 1989: 491 plays across 13 tracks = ~38 plays per track (repeated intentional listening)

**Using Track-Level Data** (with `verbose: true`):
- Identifies favorite tracks vs. skipped tracks
- Reveals listening patterns (full album vs. cherry-picking)
- Shows engagement depth (evenly distributed vs. heavy rotation)

**Acknowledge complexity** rather than making direct numerical comparisons.
Use `lfm_check` with the album parameter to get track count context.

### Checking Albums

**Basic Check**:
```
lfm_check(artist: "Taylor Swift", album: "1989")
// Returns: 491 plays, 13 tracks
```

**Detailed Analysis** (with track breakdown):
```
lfm_check(artist: "Taylor Swift", album: "1989", verbose: true)
// Returns: 491 plays, 13 tracks, plus per-track play counts and patterns
```

**Use Cases**:
- Verify if user has heard a recommended album
- Understand engagement level (track count context)
- Identify favorite tracks from an album (verbose mode)
- Analyze listening patterns (full album vs. singles)
```

#### 8. Update README
**File**: `README.md`

Add to features:
```markdown
- 📀 **Album Listening Analysis** - Check album play counts with optional track-level breakdown
```

Add to usage examples:
```bash
# Check if you've listened to an album
lfm check "Taylor Swift" --album "1989"

# Get detailed track-level breakdown
lfm check "Black Country, New Road" --album "Ants From Up There" --verbose

# Compare listening patterns across albums
lfm check "Mozart" --album "Le Nozze di Figaro" --verbose
lfm check "The Magnetic Fields" --album "69 Love Songs" --verbose
```

### Phase 5: Testing & Validation (1 hour)

#### 9. Manual Testing

Test cases:
```bash
# 1. Basic album check
./publish/win-x64/lfm.exe check "Taylor Swift" --album "1989"
# Expected: Album name, play count, track count

# 2. Album never played
./publish/win-x64/lfm.exe check "Unknown Artist" --album "Unknown Album"
# Expected: "Never played"

# 3. Verbose mode (track breakdown)
./publish/win-x64/lfm.exe check "Taylor Swift" --album "1989" --verbose
# Expected: Full track list with play counts, listening pattern analysis

# 4. JSON output
./publish/win-x64/lfm.exe check "Taylor Swift" --album "1989" --json
# Expected: Valid JSON with album data

# 5. JSON + verbose
./publish/win-x64/lfm.exe check "Taylor Swift" --album "1989" --verbose --json
# Expected: JSON with tracks array populated

# 6. Classical album (many tracks)
./publish/win-x64/lfm.exe check "Mozart" --album "Le Nozze di Figaro" --verbose
# Expected: ~8 seconds, full 80-track breakdown

# 7. Cache behavior
./publish/win-x64/lfm.exe check "Taylor Swift" --album "1989" --verbose --timing
# Run twice - second should be instant (cached)

# 8. Timing flag
./publish/win-x64/lfm.exe check "Taylor Swift" --album "1989" --verbose --timing
# Expected: Shows response time at end
```

#### 10. MCP Testing with Claudette

Test via Claude chat:
```
User: "Have I listened to Taylor Swift's 1989 album?"
Claudette: [Uses lfm_check with album parameter]

User: "What are my most played tracks from that album?"
Claudette: [Uses lfm_check with album + verbose]

User: "How does my listening to Ants From Up There compare?"
Claudette: [Uses lfm_check for both albums, interprets with track count context]
```

## Error Handling

### Expected Errors
1. **Album not found**: Return "Never played" (not an error)
2. **API rate limit**: Handled by existing throttling (100ms)
3. **Network timeout**: Handled by Result<T> pattern
4. **Invalid track data**: Skip track, continue with others
5. **Empty track list**: Show album-level data only

### Edge Cases
1. **Album with 0 tracks**: Display album plays, note no track data
2. **Tracks with 0 plays**: Include in breakdown, shows listening patterns
3. **Very long album names**: Truncate display (keep full in JSON)
4. **Special characters in names**: URL encode for API, display as-is

## Performance Considerations

### Caching Strategy
- **Album metadata**: Cache for 60 minutes (configurable)
- **Track play counts**: Cache for 60 minutes (configurable)
- **Cache keys**: Include artist + album + username + "verbose" flag
- **Cache invalidation**: Standard expiry, --force-api flag to refresh

### Throttling
- API calls throttled at 100ms (configurable via ApiThrottleMs)
- Verbose mode: Sequential track fetching (respects throttle)
- Non-verbose: Single album API call (fast)

### User Experience
- Non-verbose mode: ~200ms (1 API call)
- Verbose mode (13 tracks): ~1.5 seconds (14 API calls)
- Verbose mode (80 tracks): ~8 seconds (81 API calls)
- Cached results: Instant (no API calls)

## Success Criteria

✅ **Functional**:
- Album check returns play count + track count
- Verbose mode shows per-track breakdown
- JSON output is valid and parseable
- MCP integration works via Claudette
- Caching reduces repeat lookup time to near-instant

✅ **Quality**:
- Clean build (0 warnings, 0 errors)
- Consistent with existing check command patterns
- Proper error handling via Result<T>
- Guidelines help LLM interpret track count context

✅ **Performance**:
- Normal albums: < 2 seconds
- Large albums: < 10 seconds
- Cached lookups: < 100ms

## Estimated Timeline

| Phase | Task | Hours |
|-------|------|-------|
| 1 | Core Infrastructure (models, API) | 2.0 |
| 2 | CLI Command (CheckCommand update) | 2.5 |
| 3 | MCP Integration (server.js) | 1.0 |
| 4 | Guidelines & Documentation | 0.5 |
| 5 | Testing & Validation | 1.0 |
| **Total** | | **7 hours** |

## Next Session Checklist

When starting implementation:
- [ ] Review this plan
- [ ] Start with Phase 1 (models + API client)
- [ ] Build incrementally, test after each phase
- [ ] Use TodoWrite to track progress
- [ ] Commit after each major phase completes

## Future Enhancements (Out of Scope)

- Pattern analysis beyond "top 3 tracks" (skip patterns, chronological listening)
- Album comparison tools (compare 2+ albums)
- Bulk album checking (like bulk artist check)
- Album recommendations based on track-level preferences
- Export album analysis to CSV/JSON files

These can be added in future versions if valuable.