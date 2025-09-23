# Last.fm CLI - Claude Session Notes

## Project Overview
Last.fm CLI tool written in C# (.NET) for retrieving music statistics. The project has undergone significant refactoring to eliminate code duplication, simplify architecture, and optimize API usage.

## Current Architecture
- **Lfm.Cli**: CLI interface with commands and command builders
- **Lfm.Core**: Core functionality including services, models, and configuration
- Uses System.CommandLine for CLI framework
- **Implements file-based caching with comprehensive cache management**
- Centralized error handling and display services

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

## Key Files
- `src/Lfm.Cli/Program.cs` - Main entry point and DI setup
- `src/Lfm.Cli/Commands/BaseCommand.cs` - Shared command functionality
- `src/Lfm.Core/Configuration/ErrorMessages.cs` - Centralized error messages
- `src/Lfm.Core/Services/LastFmApiClient.cs` - API client
- `src/Lfm.Core/Services/CachedLastFmApiClient.cs` - Decorator with comprehensive caching
- `src/Lfm.Core/Services/Cache/FileCacheStorage.cs` - File-based cache storage
- `src/Lfm.Core/Configuration/LfmConfig.cs` - Configuration with cache settings
- `src/Lfm.Cli/Commands/CacheStatusCommand.cs` - Cache status display
- `src/Lfm.Cli/Commands/CacheClearCommand.cs` - Cache management
- `src/Lfm.Core/Services/DisplayService.cs` - Display formatting

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

**Next Steps**: User testing and validation

## Recent Sessions

### Session: 2025-01-16 (Previous)
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

### Session: 2025-01-17 (Cache Implementation)
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

### Session: 2025-01-28 (Recommendations Feature)
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

### Session: 2025-09-19 (Spotify Device Management & Auto-Playback)
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

### Known Issues for Future Sessions

#### 🐛 Recommendations Duplicate Track Bug
- **Issue**: Recommendations showing duplicate tracks with slight variations
- **Example**: Wings showing both "Live And Let Die - 2018 Remaster" and "Live and Let Die"
- **Root Cause**: Last.fm API returns multiple versions/remasters as separate tracks
- **Potential Solutions**:
  - Implement track name normalization (remove remaster suffixes, clean punctuation)
  - Add duplicate detection based on similarity scoring
  - Filter tracks by release date preference (original vs remaster)
- **Impact**: Low priority - doesn't break functionality but reduces recommendation quality
- **Location**: Likely in recommendations track fetching logic (RecommendationsCommand.cs)

#### 🔍 Future Enhancement Opportunities
- **Progress Bars**: As documented in progressbarproject.md for long-running operations
- **Enhanced Track Filtering**: More sophisticated duplicate detection across all commands
- **Device Auto-Discovery**: Automatic detection of new Spotify devices
- **Playlist Export**: JSON/CSV export functionality for query results

### Session: 2025-06-28 (Albums Bug Fix & API Throttling)
- **Status**: ✅ COMPLETE - Critical bug fixes and comprehensive API throttling
- **Branch**: `refactor/service-layer`

#### ✅ Albums Date Range Bug - FIXED
**Problem**: `albums --year 2023` returned no results despite user having albums
**Root Cause**: `AlbumInfo` model used `[JsonPropertyName("name")]` but Last.fm recent tracks API returns album names as `"#text"`
**Solution**: Changed to `[JsonPropertyName("#text")]` to match API response format
**Testing**: ✅ Verified working for all date ranges

#### ✅ API Throttling Implementation - COMPLETE
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

#### ✅ Error Caching Analysis - VERIFIED SAFE
**Investigation**: Checked if 500 errors were being cached
**Finding**: `if (apiResult != null)` in cache - we **only cache successful results** ✅
**Status**: No caching issues, 500 errors correctly not cached

#### ✅ Configuration Integration
- **Throttle Setting**: `ApiThrottleMs = 100` (configurable via `config set-throttle`)
- **DI Integration**: Throttle value passed from config to `LastFmApiClient` constructor
- **Applied Consistently**: All multi-call operations use configured throttle value

**Build Status**: ✅ Clean build, 0 warnings, 0 errors
**Architecture Status**: Comprehensive API throttling implemented, albums bug resolved

#### ✅ Previous Phase 1 & 2 Refactoring (COMPLETED)
**Service Layer**: ✅ Full `ILastFmService` with 13 core methods extracted
**Display Logic**: ✅ Centralized via enhanced `IDisplayService`
**Command Simplification**: ✅ Commands reduced to pure CLI concerns
**Business Logic**: ✅ 370+ lines moved from commands to service layer
**Error Handling**: ✅ Result<T> pattern and ErrorResult classification implemented

## Build/Test Commands

⚠️ **CRITICAL**: Always use `publish/` directory structure per DIRECTORY_STANDARDS.md ⚠️

**Standard Build Commands:**
- **Development Build**: `dotnet build -c Release`
- **Windows Publish**: `dotnet publish src/Lfm.Cli -c Release -r win-x64 -o publish/win-x64 --self-contained false`
- **Linux Publish**: `dotnet publish src/Lfm.Cli -c Release -r linux-x64 -o publish/linux-x64 --self-contained false`

**Executable Locations:**
- **Windows**: `./publish/win-x64/lfm.exe` ← USE THIS
- **Linux**: `./publish/linux-x64/lfm` ← USE THIS

**Testing Commands:**
- **Test Cache**: `./publish/linux-x64/lfm test-cache` (internal testing command)
- **Benchmark Cache**: `./publish/linux-x64/lfm benchmark-cache` (performance validation)

## Notes
- Uses .NET 8/9 (both target frameworks present)
- Publishes to both Windows and Linux
- Configuration stored in user's AppData/lfm folder
- API key required from Last.fm

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

## Current Project Plans & Status

### ✅ Primary Development - COMPLETE
**All major architecture and functionality complete**. The CLI tool is fully functional with:
- ✅ Complete service layer architecture 
- ✅ Comprehensive API throttling and reliability
- ✅ Full caching implementation with management
- ✅ All core commands working (artists, tracks, albums, recommendations)
- ✅ Date range support across all commands
- ✅ Unicode symbol support with auto-detection
- ✅ Clean build with 0 warnings, 0 errors

### 📋 Future Enhancement Plans

#### 1. **Progress Bar Implementation** 📊
- **Status**: Detailed plan created in `progressbarproject.md`
- **Goal**: Add progress bars for long-running operations (30+ seconds)
- **Priority**: High value for user experience
- **Effort**: 2-3 days implementation
- **Key Benefits**: 
  - Real-time feedback for date range aggregation
  - Progress indicators for range queries and recommendations
  - Professional feel for long operations
- **Architecture**: IProgressReporter interface with Console/Null implementations
- **Integration**: Leverages existing throttling points for minimal code changes

#### 2. **Additional Features** (Future Considerations)
- **Enhanced Filtering**: More sophisticated recommendation filters
- **Export Functionality**: JSON/CSV export for query results  
- **Playlist Generation**: Create playlists from recommendations
- **Extended Analytics**: Advanced statistics and insights
- **Configuration Enhancements**: More granular settings

### 🎯 Current State Assessment
- **Codebase Quality**: Excellent - clean architecture, comprehensive error handling
- **Performance**: Optimized with 119x cache improvements + proper API throttling
- **Reliability**: Robust with comprehensive error handling and graceful degradation
- **User Experience**: Good foundation, progress bars would be primary UX enhancement
- **Maintainability**: High - well-structured with clear separation of concerns

### 📝 Development Notes
- **Build Status**: ✅ Consistently clean builds across platforms
- **Testing**: Manual testing comprehensive, all core functionality verified
- **Documentation**: Well-documented with comprehensive README and session notes
- **Configuration**: Flexible with user-configurable settings for all major behaviors