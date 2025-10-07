# Last.fm MCP Usage Guidelines

## Temporal Parameter Selection

### When users mention YEARS (2023, 2024, 2025):
- ✅ **USE**: `year: "2025"`
- ❌ **AVOID**: `period: "7day"` or `period: "12month"`
- **Example**: "music in 2025" → `year: "2025"`

### When users mention RELATIVE time:
- **"recently"** / **"lately"** → `period: "1month"`
- **"this week"** → `period: "7day"`
- **"this month"** → `period: "1month"`
- **"overall"** / **"all time"** → `period: "overall"`

### When users mention SPECIFIC RANGES:
- **"since June"** → `from: "2025-06-01"`
- **"January to March"** → `from: "2025-01-01", to: "2025-03-31"`
- **"last 6 months"** → `period: "6month"`

## Tool Selection Guide

### Finding Similar Artists:
- **User asks for "artists similar to X"**: Use `lfm_similar` to find artists similar to a specific artist
  - Example: "artists similar to Holly Humberstone" → `lfm_similar(artist: "Holly Humberstone", limit: 20)`
  - Returns: List of similar artists with similarity scores from Last.fm
  - Follow-up: Use `lfm_bulk_check` to verify which ones user has/hasn't heard

- **User asks for "recommendations based on my taste"**: Use `lfm_recommendations` to analyze user's overall listening
  - Example: "recommend new music" → `lfm_recommendations(filter: 1, totalArtists: 20)`
  - Returns: Artists similar to user's top artists, with tracks
  - Based on: User's listening history, not a specific artist

### Key Difference:
- `lfm_similar`: "Find artists like X" (specific artist as seed)
- `lfm_recommendations`: "Find new music for me" (user's taste as seed)

## Discovery Workflows

### Creating Discovery Playlists:
1. **Get recommendations**: Use `lfm_recommendations` with appropriate temporal parameters
2. **Filter for new artists**: Set `filter: 1` (minimum 1 play to exclude) or use `lfm_bulk_check`
3. **Create playlist**: Use `lfm_create_playlist` with curated tracks

### Checking Listening History:
- **Single artist/track/album**: Use `lfm_check`
- **Multiple items**: Use `lfm_bulk_check` for efficiency
- **Before recommendations**: Check if user wants "new" vs "familiar" artists

## Understanding Album Play Counts

Album play counts are the sum of all track plays from that album. **Be cautious about comparing album play counts across genres or formats:**

### Track Count Variations
- **Classical/opera albums**: Often 40-80+ tracks (e.g., Mozart's Le Nozze di Figaro)
- **Pop/rock albums**: Typically 10-15 tracks (e.g., Taylor Swift's 1989)
- **Box sets & compilations**: Can have 30-60+ tracks (e.g., The Magnetic Fields' 69 Love Songs)
- **Singles/EPs**: Usually 3-5 tracks

### Interpretation Guidelines

**Example Comparison**:
- Le Nozze di Figaro: 781 plays across 80 tracks = deep engagement with full opera (~10 plays per track)
- 1989: 491 plays across 13 tracks = repeated intentional listening (~38 plays per track)

**Using Track-Level Data** (with `verbose: true`):
- Identifies favorite tracks vs. skipped tracks
- Reveals listening patterns (full album vs. cherry-picking)
- Shows engagement depth (evenly distributed vs. heavy rotation)

**Acknowledge complexity** rather than making direct numerical comparisons. Use `lfm_check` with the album parameter to get track count context.

### Checking Albums

**Basic Check**:
```
lfm_check(artist: "Taylor Swift", album: "1989")
// Returns: { userPlaycount: 491, trackCount: 13 }
```

**Detailed Analysis** (with track breakdown):
```
lfm_check(artist: "Taylor Swift", album: "1989", verbose: true)
// Returns: userPlaycount, trackCount, plus per-track play counts and listening patterns
```

**Use Cases**:
- Verify if user has heard a recommended album
- Understand engagement level (track count context)
- Identify favorite tracks from an album (verbose mode)
- Analyze listening patterns (full album vs. singles)

### Interpreting Unaccounted Plays

When using `verbose: true` for album checks, you may see a discrepancy between the album's total playcount and the sum of individual track plays. This is indicated by the `unaccountedPlays` field in the response.

**What it means**:
- Last.fm's album playcount includes ALL scrobbles for that album
- Individual track lookups may fail to match some scrobbles due to name variations
- Common causes: featuring artists, remixes, remastered versions, bonus tracks

**Example**:
```json
{
  "album": "folklore",
  "userPlaycount": 455,
  "trackPlaycountSum": 415,
  "unaccountedPlays": 40,
  "hasDiscrepancy": true,
  "tracks": [
    { "name": "exile", "userPlaycount": 0 }
  ]
}
```

**Interpretation Guidelines**:
- A track showing 0 plays mid-album with `hasDiscrepancy: true` likely indicates a name mismatch, not that the user skipped it
- The actual scrobbles exist but are under a different name (e.g., "exile (feat. Bon Iver)" vs "exile")
- Users rarely skip single tracks in heavily-played albums
- Check `unaccountedPlays` value - if it matches the 0-play track count, it's almost certainly a mismatch

**When recommending music**: If an album has high playcount but some tracks show 0, don't assume those tracks are disliked. The unaccounted plays suggest the user actually heard the full album.

**Real-World Example - Pink Floyd "Wish You Were Here"**:
```json
{
  "album": "Wish You Were Here",
  "artist": "Pink Floyd",
  "userPlaycount": 56,
  "trackCount": 5,
  "trackPlaycountSum": 40,
  "unaccountedPlays": 16,
  "hasDiscrepancy": true,
  "tracks": [
    { "name": "Shine on You Crazy Diamond", "userPlaycount": 0 },
    { "name": "Welcome to the Machine", "userPlaycount": 9 },
    { "name": "Have a Cigar", "userPlaycount": 8 },
    { "name": "Wish You Were Here", "userPlaycount": 23 },
    { "name": "Shine On You Crazy Diamond (Part Two)", "userPlaycount": 0 }
  ]
}
```

**Interpretation**:
- Both parts of "Shine On You Crazy Diamond" show 0 plays
- 16 unaccounted plays matches the missing epic track
- **Likely cause**: Scrobbles say "Shine On You Crazy Diamond (Parts 1-5)" / "(Parts 6-9)"
- **Album naming varies**: First pressing vinyl uses (1-5), some CDs use (I-V), streaming uses different variants
- **Listening pattern deduction**: ~8 full album listens (16 plays ÷ 2 parts) + ~12 extra title track plays
- **Recommendation insight**: User loves the whole album, with special attachment to title track - not someone who skips the epic progressive pieces

### Analyzing Track Position Patterns

**IMPORTANT**: Always account for metadata discrepancies first before analyzing position patterns.

**Step 1: Adjust for Zero-Play Tracks with Discrepancies**

Before analyzing listening patterns by track position, check if `hasDiscrepancy: true` and tracks show 0 plays:

1. Calculate likely actual plays: `unaccountedPlays ÷ number of 0-play tracks`
2. Mentally adjust those tracks to their likely actual play count
3. THEN analyze the pattern

**Example - Pink Floyd "Dark Side of the Moon"**:
- **Raw data**: "Us and Them" shows 1 play
- **Context**: `unaccountedPlays: 10`, other tracks: 9-12 plays
- **Adjusted**: "Us and Them" likely has ~10 plays (not 1)
- **Don't conclude**: "user skips track 7" when there's a discrepancy - adjust first, then analyze

**Step 2: Interpret Position Patterns**

After adjusting for discrepancies, track play counts relative to album position reveal listening behavior:

**Front-Loaded Patterns** (high plays at start, declining toward end):
- Example: Tracks 1-3: 15 plays → Tracks 8-10: 5 plays
- **Interpretation**: Interrupted listens, album loses interest mid-way
- User likely doesn't complete the album regularly

**Back-Loaded Patterns** (higher plays at end):
- Example: Most tracks: 10 plays → Final tracks: 12-15 plays
- **Interpretation**: Completes album PLUS returns to favorites at end
- **Key insight**: Proves engagement through to the end
- User is invested in the full album journey

**Even Distribution**:
- Example: All tracks within 1-2 plays of each other
- **Interpretation**: Proper album listener, full work experience
- User treats album as cohesive artistic statement

## Common Parameter Patterns

### Recommendations:
- **Discovery focus**: Use `filter: 1` to exclude known artists
- **Familiar music**: Use `filter: 0` (default) to include all
- **Playlist creation**: Combine with `playlist` and `playNow` parameters

### Time Periods:
- **Current year**: `year: "2025"` (not `period: "12month"`)
- **Rolling periods**: `period: "1month"`, `period: "6month"`, etc.
- **Specific dates**: `from` and `to` parameters

### Playlist Creation:
- **Track format**: `[{"artist": "Name", "track": "Title"}, ...]`
- **Naming**: Playlist names auto-prefixed with "lfm-"
- **Spotify features**: Use `shuffle`, `playNow`, `device` as needed

## Troubleshooting

### "No results" issues:
- Check if temporal parameters are too restrictive
- Verify artist/track names are spelled correctly
- Consider broader time periods for more data

### Discovery not working:
- Ensure `filter` parameter excludes known artists
- Use `lfm_bulk_check` to verify artist listening status
- Consider expanding recommendation count for more options

## Best Practices

### Multi-step Workflows:
1. Always check user's actual listening before making assumptions
2. Use appropriate temporal parameters based on user language
3. Verify artist novelty for discovery playlists
4. Provide clear feedback about what was found/created

### Contextual Filtering (LLM Value-Add):
- **Maintain conversational context**: If user says "I don't like X", filter that out from subsequent results
- **Apply nuanced preferences**: "Not too pop-py", "More experimental", etc.
- **Combine multiple signals**: Last.fm data + user stated preferences + listening history
- **Tool composition**: `lfm_similar` → apply context → `lfm_bulk_check` → filtered recommendations

### Error Handling:
- If tracks not found, continue with available tracks
- Report both successes and failures clearly
- Suggest alternatives when original request fails