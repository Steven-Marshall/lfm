# New Album Discovery Feature - Design Document

## Overview

Enable LLMs to answer queries like: **"What 3 new album releases should I listen to given my overall listening history?"**

The feature combines:
- **Spotify's new releases API** - What's recently released
- **Last.fm listening history** - What the user likes
- **Last.fm similarity data** - Bridge between user's taste and new artists

## User Query Examples

- "Recommend 3 new albums based on my overall listening history"
- "What new albums came out based on my last year's listening?"
- "Show me new releases from artists I listened to in 2024"
- "Any new albums from artists similar to what I've been into lately?"

## Architecture Decision

**One Intelligent MCP Tool:** `lfm_discover_new_albums`

**Rationale:**
- Complex scoring logic better encapsulated server-side
- Easier to optimize (caching, early termination, parallel calls)
- Cleaner LLM interface
- Consistent algorithm across calls
- Can leverage existing `lfm_artists` code internally without duplication

## Algorithm Flow

### Step 1: Get User's Artist Preferences
```
Input: Temporal parameters (period/year/from/to)
Internal call: Use existing lfm_artists logic (no code duplication)
Returns: User's top 50 artists with play counts
```

### Step 2: Get Spotify New Releases
```
Call: Spotify API /v1/browse/new-releases
Parameters:
  - limit: 50
  - offset: 0
Returns: 50 recently released albums (last 14 days default)
```

### Step 3: Score Each New Album

**For each new release album:**

#### 3a. Direct Artist Match Check
```
Is album.artist in user's top 50 artists?

├─> YES (Direct Match):
│   ├─> Score = 1000 + (1000 - artistRank)
│   │   Examples:
│   │   - User's #1 artist: 1000 + 999 = 1999
│   │   - User's #12 artist: 1000 + 988 = 1988
│   │   - User's #50 artist: 1000 + 950 = 1950
│   ├─> matchType = "direct_match"
│   ├─> relatedToArtist = album.artist
│   └─> Continue to "already listened" check
│
└─> NO: Continue to similarity check
```

#### 3b. Similarity Scoring (if not direct match)
```
Call: lfm_similar(artist=album.artist, limit=100)
Returns: Up to 100 similar artists with similarity scores

For each similar artist returned:
├─> Is this similar artist in user's top 50?
│   └─> YES:
│       ├─> Calculate: score = userArtistPlays × similarityScore
│       │   Examples:
│       │   - Similar to artist with 500 plays, 0.8 similarity: 400
│       │   - Similar to artist with 1000 plays, 0.5 similarity: 500
│       └─> Track best match (highest score)
└─> NO: Continue

Best similarity match becomes the album's score
matchType = "similar_to"
relatedToArtist = best matching user artist
```

#### 3c. Already Listened Check
```
Call: lfm_check(artist=album.artist, album=album.name, verbose=true)
Returns:
├─> hasListened: true/false
├─> playCount: number (album plays)
└─> tracks: [...] (track breakdown if verbose)

Important: Still include album in results, just flag it
Purpose: LLM can say "I see you've already heard this"
```

### Step 4: Early Termination Optimization

```
If direct matches found >= 10:
├─> Stop checking similarity for remaining albums
├─> Set remaining albums: score=0, matchType="unchecked"
└─> Continue to already-listened checks for all

Rationale:
├─> Saves 10-15 seconds when user has many direct matches
├─> 10 direct matches already gives plenty of recommendations
└─> Can still return unchecked albums as "exploration" picks
```

### Step 5: Sort and Return Results

```
Sort albums by score (descending)
Return top 20 albums (configurable via limit parameter)
Include albums with score=0 at the end for "exploration" picks
```

## Scoring Formula Summary

| Match Type | Score Calculation | Range | Priority |
|------------|------------------|-------|----------|
| Direct Match | `1000 + (1000 - artistRank)` | 1950-1999 | Highest |
| Similarity Match | `userPlays × similarityScore` | 0-1000+ | Medium |
| No Match / Unchecked | `0` | 0 | Lowest |

## Performance Analysis

### API Call Breakdown (50 new releases)

| Operation | Calls | Time Each | Total Time | Cached? |
|-----------|-------|-----------|------------|---------|
| Get user's top artists | 1 | 200-500ms | 0.5s | ✅ Hours |
| Get Spotify new releases | 1 | 200-400ms | 0.4s | ✅ 4-6 hours |
| Similarity checks (all) | 50 | 400ms | 20s | ✅ Days/weeks |
| Similarity checks (early stop) | 10-20 | 400ms | 4-8s | ✅ Days/weeks |
| Already-listened checks | 50 | 400ms | 20s | ✅ Hours |

### Total Time Estimates

| Scenario | Time | Notes |
|----------|------|-------|
| **Worst case** (no cache, no early stop) | ~40s | First run ever |
| **Typical case** (mixed cache, early stop) | ~10-15s | Normal usage |
| **Best case** (warm cache) | ~5s | Subsequent queries |

### Optimization: Early Termination
- When 10+ direct matches found: Stop similarity checks
- Reduces typical time from 15-20s → 10-15s
- Still provides comprehensive results

## Implementation Decisions

### 1. Similarity Check Coverage
**Decision:** Check all 50 new releases (with early termination)
- 15-25 seconds is acceptable for discovery query
- Missing good matches worse than slight delay
- Early termination helps when many direct matches exist
- Caching improves subsequent queries dramatically

### 2. Already-Listened Checking
**Decision:** Check all 50 albums upfront
- Enables aggregate stats ("15 matched, 3 already heard")
- Cache makes subsequent calls fast
- Better user experience with complete information

### 3. Return Format
**Decision:** Return top 20 by default, configurable via `limit`
- Enough variety for LLM to work with
- Not overwhelming
- Can request more if needed

### 4. Zero-Score Albums
**Decision:** Include them, sorted last
- LLM can offer "exploration" picks
- Flexibility for different recommendation styles
- User might want to discover outside their bubble

### 5. Architecture
**Decision:** One intelligent MCP tool
- Scoring logic too complex for LLM orchestration
- Better caching as a unit
- Easier to optimize (parallel calls, early termination)
- Reuses existing `lfm_artists` code internally

## MCP Tool Schema

```javascript
{
  name: 'lfm_discover_new_albums',
  description: 'Discover new album releases personalized to user listening history. Combines Spotify new releases, Last.fm artist preferences, and similarity matching. Returns scored recommendations with "already listened" flags.',
  inputSchema: {
    type: 'object',
    properties: {
      // Temporal parameters (for artist preferences)
      period: {
        type: 'string',
        enum: ['overall', '7day', '1month', '3month', '6month', '12month'],
        default: 'overall',
        description: 'Time period for user artist preferences'
      },
      year: {
        type: 'string',
        description: 'Specific year (YYYY) - e.g., "2024" for 2024 listening'
      },
      from: {
        type: 'string',
        description: 'Start date (YYYY-MM-DD or YYYY)'
      },
      to: {
        type: 'string',
        description: 'End date (YYYY-MM-DD or YYYY)'
      },

      // New release parameters
      daysBack: {
        type: 'number',
        description: 'How many days back to consider "new" releases',
        default: 14,
        minimum: 1,
        maximum: 90
      },

      // Performance/filtering
      topArtistsLimit: {
        type: 'number',
        description: 'Number of user top artists to consider',
        default: 50,
        minimum: 10,
        maximum: 100
      },
      includeListened: {
        type: 'boolean',
        description: 'Include albums user has already listened to (with flag)',
        default: true
      },
      limit: {
        type: 'number',
        description: 'Maximum albums to return',
        default: 20,
        minimum: 1,
        maximum: 50
      }
    }
  }
}
```

## JSON Response Format

```json
{
  "success": true,
  "userArtistsPeriod": "overall",
  "userArtistsCount": 50,
  "newReleasesCount": 50,
  "newReleasesDaysBack": 14,
  "matchedCount": 12,
  "directMatchCount": 5,
  "similarMatchCount": 7,
  "alreadyListenedCount": 2,
  "earlyTermination": true,
  "albums": [
    {
      // Album metadata
      "album": "The Dark Side of the Moon (50th Anniversary)",
      "artist": "Pink Floyd",
      "releaseDate": "2025-10-01",
      "totalTracks": 12,
      "spotifyId": "abc123",
      "spotifyUrl": "https://open.spotify.com/album/...",
      "imageUrl": "https://i.scdn.co/image/...",

      // Match scoring
      "matchType": "direct_match",
      "score": 1988,
      "relatedToArtist": "Pink Floyd",
      "userArtistRank": 12,
      "userArtistPlays": 1453,
      "similarityScore": null,

      // Listening history
      "hasListened": true,
      "albumPlayCount": 156,
      "userNote": "You've listened to this album 156 times"
    },
    {
      "album": "New Shoegaze Album",
      "artist": "Dream Artist",
      "releaseDate": "2025-10-05",
      "totalTracks": 10,
      "spotifyId": "def456",
      "spotifyUrl": "https://open.spotify.com/album/...",
      "imageUrl": "https://i.scdn.co/image/...",

      "matchType": "similar_to",
      "score": 856,
      "relatedToArtist": "Slowdive",
      "userArtistRank": 8,
      "userArtistPlays": 892,
      "similarityScore": 0.96,

      "hasListened": false,
      "albumPlayCount": 0,
      "userNote": null
    },
    {
      "album": "Exploration Pick",
      "artist": "Unknown Artist",
      "releaseDate": "2025-10-03",
      "totalTracks": 8,
      "spotifyId": "ghi789",
      "spotifyUrl": "https://open.spotify.com/album/...",
      "imageUrl": "https://i.scdn.co/image/...",

      "matchType": "no_match",
      "score": 0,
      "relatedToArtist": null,
      "userArtistRank": null,
      "userArtistPlays": null,
      "similarityScore": null,

      "hasListened": false,
      "albumPlayCount": 0,
      "userNote": null
    }
  ]
}
```

## LLM Presentation Examples

### Direct Match + Already Listened
```
"I see you've already listened to Pink Floyd's 'The Dark Side of the Moon
(50th Anniversary)' - you've played it 156 times! Pink Floyd is your #12
most-played artist overall with 1,453 plays."
```

### Direct Match + Not Listened
```
"NEW from The National: 'First Two Pages of Frankenstein' just dropped!
They're your #5 most-played artist with 2,341 plays. Want me to play it?"
```

### Similarity Match + Not Listened
```
"Based on your love of Slowdive (#8 artist, 892 plays), you might enjoy
'New Shoegaze Album' by Dream Artist (96% similarity match). Want to give it a try?"
```

### Mixed Recommendation Set
```
"I found 12 new albums matching your taste! Here are the top 3:

1. ⭐ The National - 'First Two Pages' (NEW - your #5 artist)
2. ⭐ Arcade Fire - 'We' (NEW - your #3 artist)
3. 🎵 Dream Artist - 'New Shoegaze Album' (96% similar to Slowdive)

You've already heard 2 of the other matches. Want to hear these or see more?"
```

### Exploration Picks
```
"All top matches are artists you know. Want to explore something new?
Here are highly-rated new releases outside your usual taste:
- 'Exploration Album' by Unknown Artist (Jazz fusion)
Just say the word and I'll play it!"
```

## Caching Strategy

### Cache Keys and TTLs

| Data Type | Cache Key | TTL | Rationale |
|-----------|-----------|-----|-----------|
| User top artists | `user:artists:{username}:{period/year/from/to}` | 4 hours | Updates as user listens |
| Spotify new releases | `spotify:new_releases:{daysBack}:{region}` | 6 hours | Updates Friday mornings |
| Artist similarity | `lastfm:similar:{artistName}` | 7 days | Rarely changes |
| Album listening check | `user:album:{username}:{artist}:{album}` | 2 hours | Updates as user listens |

### Cache Warming

On first query:
- ~40 seconds (cold cache)

On subsequent queries (same session):
- ~5-10 seconds (warm cache)

On queries next day:
- ~10-15 seconds (partial cache hit)

## Technical Implementation Notes

### Spotify API Integration

**New endpoint needed:** `GET /v1/browse/new-releases`
- Add to `SpotifyStreamer.cs`
- Implement as `GetNewReleasesAsync(int limit, int daysBack, string region)`
- Returns: `List<SpotifyAlbumInfo>`

### Service Layer

**New method in `ILastFmService` and `LastFmService`:**
```csharp
Task<NewAlbumDiscoveryResult> DiscoverNewAlbumsAsync(
    string username,
    string? period = null,
    string? year = null,
    DateTime? from = null,
    DateTime? to = null,
    int daysBack = 14,
    int topArtistsLimit = 50,
    bool includeListened = true,
    int limit = 20
);
```

**Internal flow:**
1. Call existing `GetUserTopArtistsAsync()` - no code duplication
2. Call new Spotify `GetNewReleasesAsync()`
3. For each new release:
   - Check direct match
   - If no match, call `GetSimilarArtistsAsync()` (existing)
   - Call `CheckAlbumListeningAsync()` (new helper)
4. Score, sort, return

### CLI Command

**New command:** `lfm discover-new-albums`

Options:
- All temporal options (--period, --year, --from, --to)
- --days-back (default: 14)
- --limit (default: 20)
- --json (for MCP integration)

### MCP Integration

**New tool:** `lfm_discover_new_albums`
- Located in `lfm-mcp-release/server.js`
- Calls CLI: `lfm discover-new-albums --json [options]`
- Parses JSON response
- Returns formatted result to LLM

### Error Handling

**Graceful degradation:**
- If Spotify API unavailable: Return error with helpful message
- If similarity API slow: Continue with direct matches only
- If album check fails: Mark as "unknown" instead of false
- If no matches found: Return empty with explanation

### Testing Scenarios

1. **User with popular artists:** Should get many direct matches, early termination
2. **User with niche artists:** Should get similarity matches
3. **User who listens to new releases:** Should see "already listened" flags
4. **Brand new user:** Should get exploration picks
5. **Different time periods:** Overall vs last year vs last month should vary results

## Future Enhancements

### Phase 2 (Future)
- Genre/tag filtering
- Exclude specific artists
- Multi-hop similarity (similar to similar)
- Collaborative filtering hints
- Release type filtering (studio album vs live vs compilation)

### Phase 3 (Future)
- Learn from user feedback ("I liked this" / "not for me")
- Trending analysis (velocity of plays)
- Discovery profiles (adventurous vs conservative)
- Integration with Spotify's recommendation engine

## Success Metrics

- Query completion time < 20 seconds (typical case)
- Cache hit rate > 60% (after warm-up)
- Match rate > 50% (at least half of new releases have some score)
- User satisfaction (qualitative - do recommendations feel relevant?)

## Open Questions / Future Decisions

1. Should we support region filtering for Spotify new releases? (US vs UK vs Global)
2. Should we filter out live albums, compilations, etc.?
3. Should we support "exclude artists" parameter? (e.g., "not Taylor Swift")
4. Should we add a "diversity" parameter to encourage exploration?
5. Should we track which recommendations user actually listened to?

---

**Document Status:** Complete Design Specification
**Ready for Implementation:** Yes
**Next Step:** Implement when ready to code
**Estimated Implementation Time:** 6-8 hours (Medium complexity)
