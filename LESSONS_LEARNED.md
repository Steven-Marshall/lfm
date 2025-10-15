# Last.fm CLI - Debugging Lessons Learned

This file contains debugging patterns, critical thinking lessons, and best practices learned during development.

---

## 🎯 Critical Thinking Over Quick Fixes

**Lesson from 2010 Cache Bug** (Session: 2025-09-30)

As an AI coding assistant, there's a tendency to "make it work" rather than "understand why it's broken." This can lead to premature workarounds instead of proper diagnosis.

### The Bug Pattern:
- `lfm artists --year 2010` returned empty results ❌
- `lfm tracks --year 2010` worked perfectly ✅
- Other years (2009, 2011) worked fine ✅

### Initial Mistake:
Suspected Last.fm API limitations and considered implementing automatic chunking workarounds.

### What Should Have Been Obvious:
- Both commands use the same underlying `GetRecentTracksAsync` API method
- If the API worked for tracks, it should work for artists
- Inconsistent behavior between similar operations strongly suggests **our code**, not the API

### The Real Cause:
Empty responses were being cached due to insufficient validation in `CachedLastFmApiClient.cs`:

```csharp
// WRONG: Caches empty objects
if (apiResult != null) {
    await CacheAsync(apiResult);
}

// RIGHT: Validate data exists before caching
if (apiResult != null && HasData(apiResult)) {
    await CacheAsync(apiResult);
}
```

### Key Principle:

> **If the bug pattern defies logic for external factors, it's almost certainly our own code. Dig there first.** 🎯

### Debugging Checklist:
1. ✅ Do similar operations behave differently? → Suspect our code
2. ✅ Does forcing fresh data (`--force-api`) work? → Cache/state issue
3. ✅ Are symptoms logically inconsistent with external API behavior? → Our bug
4. ✅ Before implementing workarounds, validate the problem is external
5. ✅ Be skeptical and critical, especially when symptoms don't make sense

### Never:
- ❌ Implement "split year into 2" workarounds without understanding root cause
- ❌ Jump to "API must be broken" conclusions without testing with fresh data
- ❌ Accept illogical behavior patterns as "just how it is"
- ❌ Fix to get working without deep dive into why it failed

---

## 🔍 Metadata Matching Complexity

**Lesson from Album Check Implementation** (Session: 2025-10-06)

### The Challenge:
Last.fm track metadata varies significantly between API endpoints and actual scrobbles, leading to mismatches when trying to correlate data.

### Common Variations:
1. **Apostrophe Variants**:
   - Album API: `'` (U+0027)
   - Scrobbles: `'` (U+2018) or `'` (U+2019)
   - Example: "Wish You Were Here" vs "Wish You Were Here"

2. **Featuring Artists**:
   - Album API: "exile"
   - Scrobbles: "exile (feat. Bon Iver)"

3. **Track Part Numbers**:
   - Album API: "Shine On You Crazy Diamond"
   - Scrobbles: "Shine On You Crazy Diamond (Parts 1-5)"

4. **Remaster Suffixes**:
   - Album API: "Hey Jude"
   - Scrobbles: "Hey Jude - 2015 Remaster"

5. **Punctuation Spacing**:
   - Album API: "Walk Away Renée/Pretty Ballerina"
   - Scrobbles: "Walk Away Renée / Pretty Ballerina"

### Implemented Solutions:

**Apostrophe Retry Logic**:
- Try original name first
- Retry with left quote variant (U+2018)
- Retry with right quote variant (U+2019)
- Most common and cost-effective to handle

**Discrepancy Detection**:
- Calculate unaccounted plays: `albumTotal - sumOfTracks`
- Report factually: "16 plays unaccounted for"
- Explain possible causes (featuring artists, remasters, part numbers)
- Don't assume user behavior (skipping tracks)

### What We Didn't Implement:
- **Punctuation spacing normalization** - "feels a stretch too far" (user decision)
- **Fuzzy matching for all variations** - would require 200+ extra API calls per album
- **AI-based similarity detection** - overkill for the problem

### Key Insight:

> **Metadata mismatches are expected and common. Design for graceful degradation with helpful explanations, not perfect matching.**

### Fallback Strategy:
When check returns 0 plays but shouldn't:
1. Use `lfm_artist_albums` or `lfm_artist_tracks` with `deep: true`
2. Search actual scrobble history (more resilient to metadata variations)
3. Real scrobbles are source of truth, not Last.fm's canonical names

---

## ⚡ API Performance Analysis

**Lesson from Throttling Optimization** (Session: 2025-10-06)

### The Investigation:
Analyzed API performance with detailed timing breakdown to identify bottlenecks.

### Findings:
- **HTTP/Network: 90-95% of time** (400-4000ms, highly variable)
- **JSON Parsing: <2% of time** (17-32ms, consistently fast)
- **JSON Stream Reading: Negligible** (0-1ms)
- **Cache Writing: ~3% of time** (20-50ms)
- **Throttle: Variable** (0-200ms depending on call spacing)

### Key Insight:

> **Last.fm's server response time is the bottleneck, not our code. Don't optimize the wrong thing.**

### Implications:
- Caching is crucial (119x improvement achieved)
- Throttling prevents 500 errors but can't speed up individual calls
- Parallel calls help with deep searches (batch of 5 concurrent)
- JSON processing optimization would have minimal impact

### Configuration Changes:
- Default throttle: 100ms → 200ms (safer compliance with 5 req/sec limit)
- Parallel API calls: 5 concurrent with 1 second between batches
- Throttling disabled during parallel execution to maximize throughput

---

## 🎵 Spotify API Race Conditions

**Lesson from Album Queueing Bug** (Session: 2025-01-19)

### The Problem:
Simon & Garfunkel "Bookends" (12 tracks) had 2 tracks missing and some reordered when queued to Spotify.

### Root Cause:
12 separate API calls (1 play + 11 individual queues) caused race conditions:
```csharp
// WRONG: Multiple sequential API calls
await PlayTrack(track1);
await QueueTrack(track2);
await QueueTrack(track3);
// ... 9 more calls
```

### Solution:
ONE atomic API call with all URIs:
```csharp
// RIGHT: Single atomic operation
await StartPlaybackAsync(allTrackUris);  // Spotify supports up to 100 URIs
```

### Key Insight:

> **When APIs support batch operations, use them. Multiple sequential calls can cause race conditions and ordering issues.**

### User Confirmation:
"it queued up cleanly as well so the spotify album play fix is working" ✅

---

## 🧠 LLM Blind Spots in MCP Context

**Lesson from Guidelines Refinement** (Session: 2025-01-19)

### The Pattern:
LLMs confidently hallucinate track positions and album metadata despite lacking reliable training data for edge cases.

### Examples:
- "Track 2 is..." when it's actually track 1
- "This 12-track album..." when track count varies by edition
- Which tracks appear on which albums (varies by region, edition)

### Why It Happens:
Track-level metadata is edge-case data for LLMs. They'll fill gaps with false confidence.

### Solution:
**Always verify with tools**:
- `lfm_check(artist, album, verbose: true)` → Full tracklist
- `lfm_current_track()` → Current playback position
- `lfm_recent_tracks(hours: 1-2)` → Recent listening context

### Guideline Addition:
"NEVER reference track numbers or positions without verification."

### Key Insight:

> **LLMs excel at reasoning and context, but fail at precise edge-case data. Design workflows that verify before stating facts.**

---

## 📊 Understanding Depth Parameter

**Lesson from Left Banke Deep Search** (Session: 2025-01-19)

### The Misconception:
Depth parameter searches chronologically (most recent N tracks).

### The Reality:
Depth parameter searches by **popularity ranking** (top N most-played tracks).

### Example Edge Case:
- User has 100,000 total scrobbles
- Left Banke: 14 plays total, ranks #2324 by play count
- `depth: 2000` misses them entirely (user has 2,323 tracks with MORE plays)

### Breadth Listener Problem:
- Guided By Voices: 100 albums × 1-2 plays each
- All tracks rank #5000-7000 by play count
- `depth: 2000` finds ZERO tracks

### Solution:
**Default to `deep: true`** for artist discovery:
- First search: 30+ seconds (comprehensive)
- Subsequent searches: Instant (cached)
- Works for both popular and obscure artists
- No guessing about ranking thresholds

### Key Insight:

> **Popularity ranking ≠ chronological. For discovery, cache + deep search is more reliable than guessing depth thresholds.**

---

## 💬 Response Style for Music Conversations

**Lesson from User Feedback** (Session: 2025-01-19)

### The Problem:
LLM responses were too verbose, data-analyst style instead of conversational DJ buddy style.

### User Feedback:
"stop all the blurb... 'Yes you have, you've listened to White Light/White Heat 4-5 times'. done."

### Bad Example:
```
Yes! Track breakdown: Sister Ray (5 plays), The Gift (4 plays)...
[8 more lines of track-by-track data]
...That title track showing 0 is almost certainly a metadata mismatch -
likely scrobbled under a different name (remaster suffix, etc.).
No one listens to White Light/White Heat 4-5 times and skips the opening title track...
```

### Good Example:
```
Yes, you've listened to White Light/White Heat 4-5 times
(21 plays, including the 17-minute 'Sister Ray').
```

### Guideline Addition:
- Answer directly with ONE interesting detail max
- Don't provide track-by-track breakdowns unless asked
- Don't explain metadata discrepancy math in detail
- Don't turn it into a music history lesson

### Key Insight:

> **Be a DJ buddy, not a data analyst. Concise answers with one interesting detail, not exhaustive breakdowns.**

---

## 🔮 Future Patterns to Document

As the project evolves, document new lessons here:
- Token optimization strategies (contextual reminders concept)
- MCP tool usage patterns (when to use recommendations vs LLM reasoning)
- Configuration migration strategies
- Testing patterns for cross-platform compatibility
- Error handling patterns for external API dependencies

---

**Last Updated**: 2025-01-19
**See Also**:
- [SESSION_HISTORY.md](SESSION_HISTORY.md) - Detailed session notes
- [IMPLEMENTATION_NOTES.md](IMPLEMENTATION_NOTES.md) - Implementation details
- [CLAUDE.md](CLAUDE.md) - Current project state
