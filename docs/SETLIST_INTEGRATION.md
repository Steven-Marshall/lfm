# Setlist.fm API Integration - Enhancement Plan

## Overview

Integrate setlist.fm API to enable concert setlist discovery and Spotify playlist creation from live performances. This complements Last.fm listening history with live concert data.

## Motivation

**User Workflow Discovery:**
User request: "Extract setlist from this URL and build me a Spotify playlist"
- Currently requires: WebFetch → LLM parsing → lfm_create_playlist
- With integration: Direct API → structured data → playlist creation
- Result: Faster, more reliable, no HTML parsing

**Unique Value:**
- Only comprehensive source for concert setlists
- MusicBrainz ID linkage with Last.fm data
- Historical concert discovery (what was played when/where)
- Attendance tracking (if API supports it)

## API Details

**Base URL:** `https://api.setlist.fm/rest/1.0`

**Authentication:**
- Requires API key via `x-api-key` header
- Users must register at setlist.fm
- Similar to Last.fm (header-based auth)

**Rate Limits:**
- Not documented (need to test)
- Likely generous (smaller user base than Last.fm)

**Key Endpoints:**

1. **Search Artist Setlists**
   - `GET /artist/{mbid}/setlists`
   - Params: `p` (page)
   - Uses MusicBrainz ID (Last.fm already provides these!)

2. **Get Specific Setlist**
   - `GET /setlist/{setlistId}`
   - Returns: Full concert details + tracklist

3. **Search Setlists** (flexible search)
   - `GET /search/setlists`
   - Params: `artistName`, `cityName`, `venueId`, `date`, `tourName`
   - More flexible than artist endpoint

4. **User Endpoints** (potential)
   - `GET /user/{userId}/attended`
   - Concert attendance tracking (if available)

**Response Format:**
- JSON (default) or XML
- Includes: venue, city, tour name, date, track order

## Proposed Tools

### Tool 1: Concert Discovery
**Name:** `lfm_artist_concerts`

**Purpose:** Find concerts for an artist with filtering

**Parameters:**
```typescript
{
  artist: string,           // Artist name (required)
  from?: string,           // Start date (YYYY-MM-DD)
  to?: string,             // End date (YYYY-MM-DD)
  venue?: string,          // Venue name filter
  city?: string,           // City name filter
  tour?: string,           // Tour name filter
  limit?: number           // Max concerts to return (default: 20)
}
```

**Returns:**
```json
{
  "success": true,
  "artist": "Radiohead",
  "concerts": [
    {
      "id": "340c9df",
      "date": "2025-11-04",
      "venue": {
        "name": "Movistar Arena",
        "city": "Madrid",
        "country": "Spain"
      },
      "tour": "European Tour 2025",
      "trackCount": 25
    }
  ],
  "count": 20
}
```

**CLI Equivalent:**
```bash
lfm concerts "Radiohead" --from 2025-01-01 --to 2025-12-31 --limit 10
```

### Tool 2: Setlist Details
**Name:** `lfm_setlist`

**Purpose:** Get full tracklist for a specific concert

**Parameters:**
```typescript
{
  setlist_id: string       // Setlist ID from lfm_artist_concerts
}
```

**Returns:**
```json
{
  "success": true,
  "concert": {
    "id": "340c9df",
    "date": "2025-11-04",
    "venue": "Movistar Arena, Madrid, Spain",
    "tour": "European Tour 2025"
  },
  "tracks": [
    {"song": "Let Down", "info": "First time since 2004"},
    {"song": "2 + 2 = 5"},
    {"song": "Sit Down. Stand Up.", "tape": true},
    ...
  ],
  "trackCount": 25,
  "encores": 2
}
```

**CLI Equivalent:**
```bash
lfm setlist 340c9df
```

## User Workflows

### Workflow 1: Recent Concert Playlist
```
User: "Get me Radiohead's latest setlist and make a playlist"

1. lfm_artist_concerts("Radiohead", limit: 1)
   → Returns: Madrid concert (340c9df)

2. lfm_setlist("340c9df")
   → Returns: 25 tracks

3. LLM structures: [{artist: "Radiohead", track: "Let Down"}, ...]

4. lfm_create_playlist("Radiohead Madrid 2025-11-04", tracks)
   → Spotify playlist created
```

### Workflow 2: Tour Archive
```
User: "Build playlists from Radiohead's 2025 European tour"

1. lfm_artist_concerts("Radiohead", from: "2025-01-01", to: "2025-12-31")
   → Returns: 15 concerts

2. For each concert:
   - lfm_setlist(concert_id)
   - lfm_create_playlist("Radiohead - {city} {date}", tracks)

3. Result: 15 playlists archiving entire tour
```

### Workflow 3: Venue History
```
User: "What did artists play at Madison Square Garden in 2024?"

1. lfm_artist_concerts(artist: "various", venue: "Madison Square Garden",
                       from: "2024-01-01", to: "2024-12-31")
   → Returns: Concert list

2. User browses, picks interesting shows
3. Get setlists and create playlists as needed
```

## Architecture Design

### New Components

**1. SetlistFmApiClient**
```csharp
public class SetlistFmApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public async Task<SetlistSearchResponse> GetArtistSetlistsAsync(
        string mbid, int page = 1);

    public async Task<Setlist> GetSetlistAsync(string setlistId);

    public async Task<SetlistSearchResponse> SearchSetlistsAsync(
        string? artistName, string? cityName, string? venue,
        DateTime? from, DateTime? to, int page = 1);
}
```

**2. Configuration Integration**
```csharp
public class LfmConfig
{
    // Existing properties...
    public string? SetlistFmApiKey { get; set; }
}
```

**3. CLI Commands**
- `ConcertsCommand.cs` - Artist concert search
- `SetlistCommand.cs` - Get specific setlist

**4. MCP Tools**
- Add to `server-core.js`:
  - `lfm_artist_concerts` handler
  - `lfm_setlist` handler

### Integration Points

**MusicBrainz IDs:**
- Last.fm API already returns MusicBrainz IDs for artists
- Can map Last.fm artists → setlist.fm directly
- Example: Radiohead = `a74b1b7f-71a5-4011-9441-d0b5e4122711`

**Throttling Strategy:**
- Simple API throttling (200ms between requests, same as Last.fm)
- Respect rate limits discovered during testing
- No complex caching needed:
  - Setlist queries are one-off lookups (not repeated aggregation)
  - Usage pattern: "Show me the Madrid setlist" → 25 tracks, done
  - Unlike Last.fm (massive dataset aggregation), setlist.fm is ad-hoc queries
  - Optional: Light session cache (same setlist queried twice in one session)

**Error Handling:**
- Graceful degradation if API key not configured
- Clear messaging: "Set API key with: lfm config set-setlistfm-api-key"

## Implementation Plan

### Phase 1: Prototype & Validation (1-2 days)
**Goal:** Verify API quality and feasibility

1. **API Key Acquisition**
   - Register at setlist.fm
   - Test approval process (manual vs automatic)
   - Document any restrictions

2. **Rate Limit Testing**
   - Make 100+ requests in quick succession
   - Measure throttling behavior
   - Determine safe request rate

3. **Data Quality Assessment**
   - Test popular artists (complete data?)
   - Test niche artists (coverage gaps?)
   - Test historical data (how far back?)
   - Verify MusicBrainz ID linkage

4. **Go/No-Go Decision**
   - ✅ If: Easy API key, reasonable limits, good data
   - ❌ If: Manual approval delays, strict limits, poor coverage

### Phase 2: Core Implementation (2-3 days)
**Goal:** Basic API client and CLI commands

1. **API Client** (`src/Lfm.Core/Services/SetlistFmApiClient.cs`)
   - HttpClient setup with API key header
   - Basic endpoints: artist setlists, get setlist
   - Error handling and logging
   - Unit tests with mock responses

2. **Models** (`src/Lfm.Core/Models/`)
   - `Setlist.cs` - Concert details + tracklist
   - `SetlistSearchResponse.cs` - Paginated results
   - `Venue.cs`, `Tour.cs` - Supporting types

3. **Configuration**
   - Add `SetlistFmApiKey` to `LfmConfig`
   - CLI command: `lfm config set-setlistfm-api-key <key>`

4. **Throttling**
   - Simple request throttling (200ms, same pattern as Last.fm)
   - No complex caching infrastructure needed
   - Optional: Simple in-memory session cache for repeated queries

### Phase 3: CLI Commands (1-2 days)
**Goal:** User-facing commands with rich output

1. **ConcertsCommand** (`src/Lfm.Cli/Commands/ConcertsCommand.cs`)
   - Display: Table with date, venue, city, track count
   - Filters: --from, --to, --venue, --city, --limit
   - JSON mode for scripting

2. **SetlistCommand** (`src/Lfm.Cli/Commands/SetlistCommand.cs`)
   - Display: Concert header + ordered tracklist
   - Show encore separators
   - Include notes (first time played, covers, etc.)
   - JSON mode

3. **Testing**
   - Manual testing with various artists
   - Edge cases: no data, API errors, invalid IDs

### Phase 4: MCP Integration (1 day)
**Goal:** LLM-friendly tools for playlist generation

1. **MCP Tools** (`lfm-mcp-release/server-core.js`)
   - `lfm_artist_concerts` - Wrapper around CLI
   - `lfm_setlist` - Wrapper around CLI
   - JSON output parsing

2. **Documentation** (`lfm-mcp-release/lfm-guidelines.md`)
   - Usage examples
   - Workflow patterns
   - Integration with lfm_create_playlist

3. **Testing**
   - End-to-end: Discover concert → Get setlist → Create playlist
   - Verify LLM can compose tools correctly

### Phase 5: Polish & Documentation (1 day)
**Goal:** Production-ready release

1. **Error Messages**
   - Clear guidance for missing API key
   - Handle "artist not found" gracefully
   - Suggest alternatives for incomplete data

2. **Documentation**
   - Update README with setlist.fm setup
   - Add to QUICKSTART.md
   - MCP_SETUP.md examples

3. **CLAUDE.md Updates**
   - Add session notes
   - Document implementation details

## Potential Features (Future)

### Concert Attendance Tracking
If setlist.fm API supports user attendance:
- `lfm_my_concerts` - Shows concerts user attended
- Integration with Last.fm: "Did you attend this show? You scrobbled 18/25 tracks that day"
- Playlist generation: "Build playlists from shows I've been to"

### Statistical Analysis
- Most played songs across tour
- Setlist variety analysis
- Compare setlists between cities/dates
- "Deep cuts" identification (rarely played tracks)

### Social Features
- "Friends who attended this show"
- Collaborative playlist from multiple attendees' perspectives

## Risk Assessment

### Risks

1. **API Key Friction** (Medium)
   - Risk: Manual approval process delays adoption
   - Mitigation: Clear documentation, optional feature
   - Impact: Lower adoption if cumbersome

2. **Rate Limits** (Medium)
   - Risk: Strict limits prevent batch operations
   - Mitigation: Aggressive caching, request throttling
   - Impact: Slower tour archive workflows

3. **Data Coverage** (Low)
   - Risk: Incomplete setlists for niche artists
   - Mitigation: Clear messaging, graceful degradation
   - Impact: Feature less valuable for certain users

4. **API Changes** (Low)
   - Risk: Breaking changes to API
   - Mitigation: Version pinning, comprehensive tests
   - Impact: Maintenance burden

### Mitigation Strategies

1. **Optional Integration**
   - Feature only active if API key configured
   - Main lfm functionality unaffected
   - Clear setup instructions

2. **Polite API Usage**
   - Simple throttling respects rate limits
   - Optional light caching for same-session queries
   - Focus on being a good API citizen

3. **Graceful Degradation**
   - Helpful error messages
   - Suggest manual entry if API fails
   - WebFetch fallback (existing user pattern)

## Success Metrics

**Adoption:**
- % of users who configure setlist.fm API key
- Number of concert searches per month
- Playlists created from setlists

**Quality:**
- API error rate (should be <1%)
- Response time consistency
- User feedback on data accuracy

**Performance:**
- Average response time for concert search
- Time to build playlist from setlist
- API error rate and throttling effectiveness

## Open Questions

1. **Does setlist.fm API support user attendance tracking?**
   - Need to explore user endpoints
   - Could enable powerful "my concerts" features

2. **What are actual rate limits?**
   - Need hands-on testing
   - Will determine batch operation feasibility

3. **How handle incomplete/inaccurate setlists?**
   - Community-edited data may have errors
   - Strategy for handling missing tracks?

4. **Should we support setlist editing/contribution?**
   - API may support POST/PUT for user contributions
   - Ethical/community consideration

5. **Integration with Last.fm scrobbles?**
   - "You listened to 18/25 tracks from this setlist on this date"
   - Automatic concert attendance inference?

## Cover Version Handling Strategy

**The Challenge:**

When bands play covers at concerts (e.g., Foo Fighters covering Queen's "Under Pressure"), there are multiple valid approaches for playlist creation:

**Options:**

1. **Original Artist (not ideal)**
   - Pro: Track always available on Spotify
   - Con: Doesn't reflect the concert experience
   - Con: User wanted Foo Fighters, gets Queen
   - Example: "Under Pressure" by Queen/Bowie

2. **Performing Artist First, Fallback to Original**
   - Pro: Tries to match concert experience
   - Con: Most live covers not on streaming services
   - Con: Requires two Spotify lookups (expensive)
   - Example: Try "Under Pressure" by Foo Fighters → fails → use Queen version

3. **Performing Artist Only (Skip if Not Found)**
   - Pro: Playlist is authentic to performer
   - Con: Missing tracks create incomplete experience
   - Con: Could result in very short playlists
   - Example: Skip "Under Pressure" entirely if Foo Fighters version not found

4. **User Preference (Configurable)**
   - Config option: `CoverHandling` = `Original | Performer | Skip`
   - Pro: User chooses their preference
   - Con: Adds configuration complexity
   - Default: TBD during implementation

**Proposed Approach:**

**Phase 1 Implementation:**
- Default behavior: **Performing Artist First, Fallback to Original**
- Reasoning: Best of both worlds - try to match concert, but ensure completeness
- MCP behavior: Same default (no exposed configuration yet)

**Future Enhancement:**
- Add config option: `lfm config set-cover-handling [performer|original|skip]`
- MCP parameter: `lfm_setlist(setlist_id, cover_handling: "performer")`

**Implementation Considerations:**

1. **Cover Detection:**
   - Setlist.fm marks covers with `cover` field or `info` notes
   - Parse artist info: "(Queen cover)" or "cover of Original Artist"
   - Detect "with Guest Artist" vs actual covers

2. **Spotify Lookup Strategy:**
   ```
   For each cover track:
   1. Try: Search for "{track}" by {performing_artist}
   2. If found: Use it
   3. If not found: Parse original artist from cover info
   4. Try: Search for "{track}" by {original_artist}
   5. If still not found: Report missing track
   ```

3. **Error Reporting:**
   - Verbose mode shows cover handling decisions
   - Example: "✓ Under Pressure - Using Queen version (Foo Fighters version not found on Spotify)"
   - Helps user understand why playlist differs from setlist

4. **Performance:**
   - Each cover = up to 2 Spotify API calls
   - Concert with 5 covers = 10 extra API calls worst case
   - Throttling ensures we stay within rate limits

**CLI Output Enhancement:**
```
Creating playlist "Foo Fighters - Madison Square Garden 2024"...
✓ 1. Everlong - Foo Fighters
✓ 2. The Pretender - Foo Fighters
⚠ 3. Under Pressure - Queen (cover, Foo Fighters version not available)
✓ 4. Learn to Fly - Foo Fighters
✓ 23. Best of You - Foo Fighters

Playlist created: 23/25 tracks added (2 covers using original artist versions)
```

**Decision:** Finalize strategy during Phase 1 implementation after seeing real-world data patterns from setlist.fm API.

## Next Steps

1. **API Key Registration** - Test approval process
2. **Rate Limit Testing** - Establish safe request patterns
3. **Data Quality Check** - Sample various artists/dates
4. **Go/No-Go Decision** - Based on prototype findings
5. **Implementation** - Follow phased plan if approved

---

**Status:** 📋 PLANNED
**Priority:** HIGH - Unique value, clean architecture fit
**Estimated Effort:** 5-7 days full implementation
**Dependencies:** setlist.fm API key approval process
