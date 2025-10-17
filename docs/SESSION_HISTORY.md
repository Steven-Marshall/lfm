# Last.fm CLI - Session History Archive

This file contains detailed historical session notes. For current work and recent sessions, see [CLAUDE.md](CLAUDE.md).

---

## Session: 2025-01-16 (Previous)
- **Status**: Reviewed project after file resync, completed cache removal
- **Findings**:
  - Most refactoring tasks completed successfully
  - ErrorMessages.cs exists and is properly integrated
  - Architecture is clean and well-organized
- **Completed**:
  - Marked Task 2.3 (Standardize Error Handling) as completed
  - Marked Task 3.1 (Remove Redundant API Calls) as completed
  - **REMOVED** entire in-memory cache system (600+ lines) for clean slate
- **Next**: Design and implement file-based caching system

## Session: 2025-01-17 (Cache Implementation)
- **Status**: ✅ COMPLETE - Full file-based caching system implemented and tested
- **Major Accomplishments**:
  - **Phase 0**: Cross-platform foundation with XDG Base Directory support
  - **Phase 1-2**: Core cache infrastructure (FileCacheStorage, CacheKeyGenerator, etc.)
  - **Phase 3**: API client integration with decorator pattern (CachedLastFmApiClient)
  - **Phase 3.5**: Internal timing validation and performance verification (119x improvement)
  - **Phase 4**: Cache control with user-facing flags and management commands
  - **Final**: Compiler warning cleanup (CS1998, CS8604 resolved)
- **Files Created/Modified**: 15+ files across cache infrastructure, command builders, and commands
- **Performance Achieved**: 119x speed improvement (6,783ms → 57ms for 10 API calls)
- **Build Status**: ✅ Both Linux and Windows builds complete with 0 warnings, 0 errors
- **Ready For**: User testing and validation

## Session: 2025-01-28 (Recommendations Feature)
- **Status**: ✅ COMPLETE - Music recommendations feature implemented
- **Feature Added**: `recommendations` command for discovering new artists
- **Implementation Details**:
  - Uses Last.fm's `artist.getSimilar` API endpoint
  - Analyzes user's top artists to find similar artists
  - Filters out artists user already knows (configurable play count threshold)
  - Scoring algorithm: similarity × occurrence count across multiple top artists
  - Full cache support with existing infrastructure
- **Key Components**:
  - `GetSimilarArtistsAsync` method in API client and cached wrapper
  - `SimilarArtist` and `SimilarArtists` models
  - `RecommendationsCommand` with parallel similar artist fetching
  - `RecommendationsCommandBuilder` with options for limit, filter, and artist count
- **Command Options**:
  - `--limit` / `-l`: Number of recommendations to return (default: 20)
  - `--filter` / `-f`: Minimum play count filter (default: 0)
  - `--artist-limit` / `-a`: Number of top artists to analyze
  - All standard options: period, range, cache flags, verbose, timing
- **Performance**: Benefits from cached artist data and parallel API calls
- **Documentation**: README.md and CLAUDE.md updated

## Session: 2025-09-19 (Spotify Device Management & Auto-Playback)
- **Status**: ✅ COMPLETE - Advanced Spotify device selection and automatic playback implemented
- **Major Features Added**:
  - **Device Management System**: Comprehensive device listing, configuration, and selection
  - **Automatic Playback Start**: Resolves issue where queueing failed when no active Spotify session
  - **Device Selection Priority**: CLI parameter > config default > active device > smart prioritization
- **Implementation Details**:
  - Device selection logic with 4-tier priority system
  - Automatic playback initiation when no active session (starts first track, queues remaining)
  - Config file storage for default device preferences
  - Command-line device override capabilities
- **New Commands Added**:
  - `lfm spotify devices` - List available Spotify devices with status/volume
  - `lfm config set-spotify-default-device "Device Name"` - Set config default
  - `lfm config clear-spotify-default-device` - Clear config default
  - `--device` / `-dev` option on tracks/recommendations commands
- **Key Technical Components**:
  - Enhanced `EnsurePlaybackActiveAsync` with device parameter and priority logic
  - New `StartPlaybackAsync` method for playback initiation via Spotify Web API
  - `DefaultDevice` field added to `SpotifyConfig` with config management
  - Updated interface signatures throughout service layer for device parameter flow
- **Device Selection Logic** (src/Lfm.Spotify/SpotifyStreamer.cs:525-572):
  1. CLI `--device` parameter (highest priority)
  2. Config file `DefaultDevice` setting
  3. Currently active Spotify device
  4. Smart prioritization: Computer > Smartphone > Speaker > other
- **Automatic Playback** (src/Lfm.Spotify/SpotifyStreamer.cs:65-83):
  - Detects when no active playback session exists
  - Starts playing first track on selected device using `/v1/me/player/play` endpoint
  - Queues remaining tracks normally via existing queue mechanism
- **Status**: All device management functionality working correctly
- **Next Steps**: User validation and real-world testing

## Known Issues for Future Sessions

### 🐛 Recommendations Duplicate Track Bug
- **Issue**: Recommendations showing duplicate tracks with slight variations
- **Example**: Wings showing both "Live And Let Die - 2018 Remaster" and "Live and Let Die"
- **Root Cause**: Last.fm API returns multiple versions/remasters as separate tracks
- **Potential Solutions**:
  - Implement track name normalization (remove remaster suffixes, clean punctuation)
  - Add duplicate detection based on similarity scoring
  - Filter tracks by release date preference (original vs remaster)
- **Impact**: Low priority - doesn't break functionality but reduces recommendation quality
- **Location**: Likely in recommendations track fetching logic (RecommendationsCommand.cs)

### 🔍 Future Enhancement Opportunities
- **Progress Bars**: As documented in progressbarproject.md for long-running operations
- **Enhanced Track Filtering**: More sophisticated duplicate detection across all commands
- **Device Auto-Discovery**: Automatic detection of new Spotify devices
- **Playlist Export**: JSON/CSV export functionality for query results

## Session: 2025-06-28 (Albums Bug Fix & API Throttling)
- **Status**: ✅ COMPLETE - Critical bug fixes and comprehensive API throttling
- **Branch**: `refactor/service-layer`

### ✅ Albums Date Range Bug - FIXED
**Problem**: `albums --year 2023` returned no results despite user having albums
**Root Cause**: `AlbumInfo` model used `[JsonPropertyName("name")]` but Last.fm recent tracks API returns album names as `"#text"`
**Solution**: Changed to `[JsonPropertyName("#text")]` to match API response format
**Testing**: ✅ Verified working for all date ranges

### ✅ API Throttling Implementation - COMPLETE
**Problem**: Aggressive API calls causing 500 Internal Server Errors from Last.fm
**Root Cause Analysis**:
- **Parallel execution**: Introduced in commit `7958f61` (June 28) for recommendations
- **Missing throttling**: Date range aggregation had no delays between API calls
- **No rate limiting**: Individual and paginated calls lacked throttling

**Solutions Implemented**:

1. **Removed Parallel Execution**:
   - Converted `Task.Run` + `Task.WhenAll` to sequential loops
   - Removed `ConcurrentDictionary`, switched to regular `Dictionary`
   - Applied to recommendations similar artist + track fetching

2. **Added Comprehensive Throttling** (100ms delays):
   - **Date Range Aggregation**: `GetTopAlbumsForDateRangeAsync`, `GetTopTracksForDateRangeAsync`, `GetTopArtistsForDateRangeAsync`
   - **Range Queries**: `ExecuteRangeQueryAsync` for `--range` parameters
   - **Deep Search Operations**: `SearchUserTracksForArtistAsync`, `SearchUserAlbumsForArtistAsync`
   - **Artist Play Count Mapping**: `GetUserArtistPlayCountsAsync`
   - **Recommendations**: Sequential similar artist + track lookups

3. **Preserved One-Shot Calls** (no throttling):
   - `artists --period overall` - single API call
   - `tracks --period overall` - single API call
   - `albums --period overall` - single API call

**Results**:
- ✅ **API Reliability**: Eliminated 500 errors from aggressive requests
- ✅ **Albums Bug Fixed**: Date ranges now return proper album results
- ✅ **Performance**: One-shot calls remain fast, multi-call operations properly throttled
- ✅ **Testing**: All functionality verified working across different scenarios

### ✅ Error Caching Analysis - VERIFIED SAFE
**Investigation**: Checked if 500 errors were being cached
**Finding**: `if (apiResult != null)` in cache - we **only cache successful results** ✅
**Status**: No caching issues, 500 errors correctly not cached

### ✅ Configuration Integration
- **Throttle Setting**: `ApiThrottleMs = 100` (configurable via `config set-throttle`)
- **DI Integration**: Throttle value passed from config to `LastFmApiClient` constructor
- **Applied Consistently**: All multi-call operations use configured throttle value

**Build Status**: ✅ Clean build, 0 warnings, 0 errors
**Architecture Status**: Comprehensive API throttling implemented, albums bug resolved

### ✅ Previous Phase 1 & 2 Refactoring (COMPLETED)
**Service Layer**: ✅ Full `ILastFmService` with 13 core methods extracted
**Display Logic**: ✅ Centralized via enhanced `IDisplayService`
**Command Simplification**: ✅ Commands reduced to pure CLI concerns
**Business Logic**: ✅ 370+ lines moved from commands to service layer
**Error Handling**: ✅ Result<T> pattern and ErrorResult classification implemented

## Session: 2025-09-24 (TopTracks Command & Architectural Separation)
- **Status**: ✅ COMPLETE - New toptracks command with artist diversity algorithm
- **Major Accomplishments**:
  - **Architectural Separation**: Clean separation between information (`tracks`) and action (`toptracks`) commands
  - **TopTracks Command**: New command implementing expanding window algorithm for maximum artist diversity
  - **Dual Algorithm Approach**: Expanding windows for periods, large sample filtering for date ranges
  - **Configurable Diversity**: User-configurable `DateRangeDiversityMultiplier` setting with CLI management
  - **MCP Integration**: Updated MCP server with new `lfm_toptracks` tool
  - **Performance Optimization**: Resolved API throttling issues and cache expiry problems
  - **Command Naming**: Final naming settled on `toptracks` for clarity and consistency
- **Key Technical Components**:
  - `TopTracksCommand.cs`: Diverse playlist generation with tracks-per-artist limiting
  - `TopTracksCommandBuilder.cs`: CLI interface for new command with `--tracks-per-artist` parameter
  - `LfmConfig.cs`: Added `DateRangeDiversityMultiplier` property (default: 10)
  - `ConfigCommand.cs`: Added `SetDateRangeMultiplierAsync` method with validation
  - Updated MCP server with clean command separation
- **Algorithm Details**:
  - **Period Queries**: Expanding window algorithm (1-20, 21-40, 41-60...) for optimal diversity
  - **Date Range Queries**: Large sample approach using configurable multiplier (limit × tracks-per-artist × multiplier)
  - **Performance**: Pragmatic approach balancing diversity with API efficiency
- **User Experience**: Preview mode by default, action only with `--playlist` or `--playnow`
- **Documentation**: Updated README.md with new command examples and configuration options
- **Build Status**: ✅ Clean build, fully functional, ready for user testing

## Session: 2025-10-06 (Album Track Checking & API Performance Analysis)
- **Status**: ✅ COMPLETE - Album checking with track-level breakdown and comprehensive API timing diagnostics
- **Major Features Implemented**:
  - **Album Check Command**: Check album play counts with detailed per-track breakdown
  - **Apostrophe Variant Handling**: Automatic retry with Unicode variants (U+0027, U+2018, U+2019) for track/album lookups
  - **Discrepancy Detection**: Intelligent detection and reporting of unaccounted plays due to name mismatches
  - **API Performance Diagnostics**: Detailed timing breakdown (throttle, HTTP, JSON-read, JSON-parse, cache)
  - **Throttle Optimization**: Changed default from 100ms to 200ms for Last.fm's 5 req/sec limit
- **Key Technical Components**:
  - `CheckCommand.cs`: Album checking with `--album` parameter and `--verbose` for track breakdown
  - `GetTrackPlaycountWithApostropheRetry`: Handles Unicode apostrophe variants in track names
  - Discrepancy calculation: `albumTotal - sumOfTracks` with user-friendly messaging
  - `LastFmApiClient`: Added `LastHttpMs`, `LastJsonReadMs`, `LastJsonParseMs` timing properties
  - `CachedLastFmApiClient`: Enhanced timing breakdown for all cache behaviors
- **Album Check Features**:
  - Console output: Track-by-track breakdown with play counts, percentages, most-played indicator
  - JSON output: Full data including `unaccountedPlays`, `hasDiscrepancy` fields
  - Listening pattern analysis: "Heavy rotation" vs "Balanced listening"
  - Discrepancy notes: Helpful explanation about featuring artists, remixes, remastered versions
- **Apostrophe Handling**:
  - Problem: Album API returns U+0027 (`'`), but scrobbles may have U+2018 (`'`) or U+2019 (`'`)
  - Solution: Try original name, then left quote variant, then right quote variant
  - Example: "Wish You Were Here" vs "Wish You Were Here" vs "Wish You Were Here"
  - Applied to: Track lookups, album lookups, individual track checks
- **Discrepancy Detection**:
  - Classic example: "exile" (album API) vs "exile (feat. Bon Iver)" (scrobbles)
  - Shows: "40 plays unaccounted for" instead of misleading "0 plays"
  - Pink Floyd example: "Shine On You Crazy Diamond" vs "Shine On You Crazy Diamond (Parts 1-5)"
  - Provides factual information without expensive fuzzy matching (would require 200+ API calls)
- **API Performance Investigation**:
  - **Finding**: HTTP/Last.fm backend is the bottleneck (90-95% of time)
  - **Timing breakdown** for single API call:
    - HTTP network transfer: 400-4000ms (highly variable, Last.fm server performance)
    - JSON parsing: 17-32ms (consistently fast, <2% of total time)
    - JSON stream reading: 0-1ms (negligible)
    - Cache writing: 20-50ms (~3% of total time)
    - Throttle: 0-200ms (depends on spacing between calls)
  - **Comparison across query types** (limit=10):
    - Tracks: ~850ms HTTP
    - Albums: ~400-3900ms HTTP (highly variable!)
    - Artists: ~390ms HTTP
  - **Conclusion**: Data volume/JSON processing is NOT the issue - it's Last.fm's server response time
- **MCP Integration**:
  - Updated `lfm-guidelines.md` with discrepancy interpretation guidance
  - Added examples of unaccounted plays and how to interpret them
  - Guidance for LLMs: Don't assume 0-play tracks are disliked if album has high playcount
  - Real-world example: Taylor Swift "folklore" with "exile (feat. Bon Iver)" mismatch
- **Configuration Changes**:
  - `LfmConfig.cs`: `ApiThrottleMs` default changed from 100ms to 200ms
  - Safer compliance with Last.fm's documented 5 req/sec rate limit
- **Example Use Case** (Pink Floyd "Wish You Were Here"):
  - Album: 56 plays total
  - Track breakdown: 40 plays accounted (WYWH title track: 23, others: 17)
  - Unaccounted: 16 plays (likely "Shine On You Crazy Diamond" with different part naming)
  - **Inference**: ~8 full album listens + ~12 extra title track plays
  - Demonstrates value of discrepancy approach for understanding listening patterns
- **Build Status**: ✅ Clean build, 0 errors, comprehensive timing diagnostics available
- **Ready For**: User testing of album check features and MCP integration
