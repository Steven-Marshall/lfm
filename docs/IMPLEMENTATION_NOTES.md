# Last.fm CLI - Implementation Notes

This file contains detailed notes on completed major implementations. For current work, see [CLAUDE.md](CLAUDE.md).

---

## Refactoring Status (Per REFACTORING_PLAN.md)

### ✅ Priority 1: Eliminate Duplication (COMPLETED)
- **Task 1.1**: Base Command Class ✅
- **Task 1.2**: Consolidate Range Logic ✅
- **Task 1.3**: Unify Display Logic ✅
- **Task 1.4**: Merge Artist Search Commands ✅

### ✅ Priority 2: Simplify Architecture (COMPLETED)
- **Task 2.1**: Simplify Range Service ✅
- **Task 2.2**: Extract Command Builders ✅
- **Task 2.3**: Standardize Error Handling ✅

### ✅ Priority 3: Optimize API Usage (COMPLETED)
- **Task 3.1**: Remove Redundant API Calls ✅
- **Task 3.2**: Implement Response Caching ✅ (File-based cache implemented)
- **Task 3.3**: Optimize Deep Search Performance ✅ (119x improvement achieved)

---

## Cache Implementation Status

### ✅ File-Based Caching System (COMPLETED)

**Implementation Complete**: Full file-based caching system with comprehensive management capabilities.

**Key Features Implemented:**
- **Raw API Response Caching**: Caches actual Last.fm API JSON responses
- **Cross-Platform Support**: XDG Base Directory compliance (Linux/Windows/WSL)
- **Performance**: 119x speed improvement achieved (6,783ms → 57ms for 10 API calls)
- **Cache Management**: Automatic cleanup with expiry-first then LRU strategy
- **User Control**: Cache behavior flags (--force-cache, --force-api, --no-cache)
- **Management Commands**: cache-status and cache-clear with detailed feedback
- **Configuration**: Comprehensive cache settings in LfmConfig.cs

**Cache Architecture:**
- **Decorator Pattern**: CachedLastFmApiClient wraps LastFmApiClient transparently
- **SHA256 Key Generation**: Collision-resistant cache keys from API parameters
- **Storage**: Individual JSON files with metadata for each cached response
- **Cleanup**: Configurable size limits, file counts, and age-based expiry
- **Behaviors**: Normal, ForceCache, ForceApi, NoCache modes

**Commands with Cache Support:**
- ✅ `tracks` command (all cache flags)
- ✅ `artist-tracks` command (all cache flags)
- ✅ `artists` command (all cache flags)
- ✅ `albums` command (all cache flags)
- ✅ `artist-albums` command (all cache flags)
- ✅ `recommendations` command (all cache flags)

**Management Commands:**
- ✅ `cache-status` - Comprehensive status display with warnings
- ✅ `cache-clear` - Clear all or expired entries with confirmation

**Implementation Session**: See SESSION_HISTORY.md for Session 2025-01-17 details

---

## Unicode Symbol Implementation Status

### ✅ Unicode Auto-Detection & Compatibility (COMPLETED)

**Implementation Complete**: Full Unicode symbol support with auto-detection and manual override capabilities.

**Key Features Implemented:**
- **Cross-Platform Compatibility**: Supports PowerShell 5.x, PowerShell 7+, cmd.exe, WSL, Linux terminals
- **Auto-Detection Logic**: Automatically detects terminal Unicode capabilities
- **Manual Override**: Config option to force enable/disable Unicode symbols
- **Graceful Fallback**: ASCII alternatives when Unicode not supported
- **Automatic UTF-8 Encoding**: Sets console encoding to UTF-8 when Unicode is detected

**Architecture:**
- **SymbolProvider Service**: Centralized symbol management with Unicode/ASCII alternatives
- **Configuration Integration**: UnicodeSupport enum (Auto, Enabled, Disabled) in LfmConfig.cs
- **Detection Logic**: Environment variable checks, PowerShell version detection, Windows Terminal detection
- **Encoding Fix**: Automatic UTF-8 console encoding when Unicode symbols are used

**Commands with Unicode Support:**
- ✅ All commands use ISymbolProvider for consistent symbol display
- ✅ Timing displays: ⏱️ vs [TIME]
- ✅ Status indicators: ✅❌ vs [OK][X]
- ✅ Cache status: 📊📈🧹 vs [CONFIG][STATS][CLEANUP]

### ✅ Unicode Auto-Detection Issue (RESOLVED)

**Problem**: Config set to "Auto" was initially failing in PowerShell 5/7.

**Root Cause**: Console encoding defaulting to Codepage 850 instead of UTF-8.

**Solution Implemented**:
- **EnsureUtf8Encoding()** method in SymbolProvider automatically sets UTF-8 encoding when Unicode is detected
- **Graceful fallback** to ASCII if UTF-8 encoding fails
- **Environment-agnostic detection** using WT_SESSION and fallback methods

**Comprehensive Testing Results** (Session 2025-01-18):

| Environment | Config=Auto | Config=Enabled | Unicode Symbols | Console Encoding |
|-------------|-------------|----------------|-----------------|------------------|
| **PowerShell 5** | ✅ Perfect | ✅ Perfect | ♫ ✅ ❌ ⏱️ 📋 | UTF-8 (CP: 65001) |
| **PowerShell 7** | ✅ Perfect | ✅ Perfect | ♫ ✅ ❌ ⏱️ 📋 | UTF-8 (CP: 65001) |
| **WSL** | ✅ Perfect | ✅ Perfect | ♫ ✅ ❌ ⏱️ 📋 | UTF-8 (CP: 65001) |
| **CMD** | ✅ Perfect | ✅ Perfect | ♫ ✅ ❌ ⏱️ 📋 | UTF-8 (CP: 65001) |

**Key Technical Insights:**
- `PSEdition` environment variable is not propagated to child processes (expected behavior)
- `WT_SESSION` detection works reliably across all Windows Terminal environments
- Automatic UTF-8 encoding setting resolves all console encoding issues
- Detection logic works correctly across all tested environments

**Status**: ✅ **COMPLETELY RESOLVED** - Unicode auto-detection working perfectly across all environments

**Investigation Tools Created:**
- `DetectTest/DetectTest/Program.cs` - Standalone detection testing program (validated solution)
- `src/Lfm.Cli/Commands/TestUnicodeCommand.cs` - In-app Unicode debugging command

---

## API Throttling Implementation

**Completed**: Session 2025-06-28 (See SESSION_HISTORY.md for full details)

**Key Changes:**
- Default throttle changed from 100ms to 200ms (Session 2025-10-06)
- Comprehensive throttling on all multi-call operations
- Parallel execution removed from recommendations to prevent 500 errors
- Configuration integration: `ApiThrottleMs` setting in LfmConfig.cs

**Performance Impact:**
- Eliminates 500 Internal Server Errors from Last.fm
- One-shot calls remain fast (no throttling)
- Multi-call operations properly rate-limited

---

## Spotify Integration

### Device Management (Session 2025-09-19)
- 4-tier device selection priority system
- Auto-playback initiation when no active session
- Config storage for default device preferences

### Album Disambiguation (Session 2025-10-09)
- Multiple album version detection
- Track + album parameter combination support
- Parallel API calls for deep searches (5 concurrent with rate limiting)

### Sonos Integration (Session 2025-10-11)
- Unified play command (Spotify + Sonos)
- node-sonos-http-api HTTP bridge integration
- Album URI vs track URI playback strategies
- Config-driven player defaults

---

## MCP Server Integration

**Current Status**: Fully operational MCP server with 28 tools

**Key Components:**
- `lfm-mcp-release/server.js` - MCP server implementation (2,347 lines)
- `lfm-mcp-release/lfm-guidelines.md` - LLM usage guidelines (480 lines)
- Trust-based initialization via `lfm_init` tool

**Tools Implemented:**
- Information: tracks, artists, albums, recent_tracks, check, bulk_check
- Discovery: recommendations, similar, artist_tracks, artist_albums
- Playlists: toptracks, mixtape, create_playlist
- Playback: play_now, queue, current_track, pause, resume, skip
- Management: api_status, activate_device

**Guidelines Evolution:**
- Simplified from quiz-based to trust-based (Session 2025-10-09)
- Enhanced with real-world feedback (Session 2025-01-19)
- Track position hallucination prevention
- Depth parameter clarification (popularity ranking, not chronological)
- Concise response patterns

See SESSION_HISTORY.md and CLAUDE.md for detailed session notes.
