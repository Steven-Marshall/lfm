# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.5.1] - 2025-10-17

### Fixed
- **GitHub Release Automation** - Fixed automated release process to enable installation scripts
  - Ensured GitHub Actions workflow runs on tag push
  - Creates proper GitHub Release with all platform binaries
  - Fixes installation scripts that depend on GitHub Releases

### Documentation
- **User Documentation Suite** - Comprehensive documentation for end users
  - Created INSTALL.md - Platform-specific installation guide
  - Created QUICKSTART.md - 5-minute getting started guide
  - Created MCP_SETUP.md - Claude Code/Desktop integration setup
  - Created TROUBLESHOOTING.md - Common issues reference
  - Updated README.md to user-friendly landing page
  - Created RELEASE.md - Release process documentation

### Maintenance
- **Repository Cleanup** - Removed legacy files and improved organization
  - Removed lfm-mcp-prototype directory (2,061 files including node_modules)
  - Removed old planning docs and test files from git
  - Moved development docs to docs/ folder
  - Updated .gitignore to prevent re-adding legacy files

## [1.5.0] - 2025-10-17

### Added

#### MCP Server Enhancements (v0.2.0)
- **"Listen Before You Speak" methodology** - Core workflow guidance for LLM interactions
  - Emphasizes data → think → more data → think → narrative pattern
  - Prevents starting narratives before completing research phase
- **"Using lfm_recommendations - LLM Reasoning First"** - Guidelines for proper tool usage
  - Emphasizes LLM musical knowledge over algorithmic recommendations
  - Clear guidance on when NOT to use recommendations (well-known artists)
  - Proper workflow: LLM reasoning first, optionally supplement with tool
- **"Playback State Awareness"** - Mandatory workflow before playback suggestions
  - Trigger words identification ("I've queued", "Want me to queue", etc.)
  - Required state checks: recent_tracks + current_track before suggesting playback
  - Response templates for data-informed suggestions

#### Features
- **Sonos Integration** - Full playback support via node-sonos-http-api
  - Unified play command supporting both Spotify and Sonos
  - Room management and device routing
  - Config-driven defaults (DefaultPlayer, DefaultRoom)
- **Recent Tracks Feature** - Temporal listening history with chronological ordering
  - Most recent first ordering
  - Configurable hours lookback parameter
  - Perfect for detecting listening patterns
- **Spotify Playback Controls** - Comprehensive playback management
  - Play now / queue functionality
  - Pause, resume, skip commands
  - Current track status
  - Device activation support
- **Album Disambiguation** - Handle tracks on multiple albums
  - Detect and report multiple album versions
  - Allow track + album parameter combination
  - User preference for studio vs live/greatest hits versions
- **API Status Checker** - Diagnose Last.fm connectivity issues
  - Health status for multiple endpoints
  - Verbose mode with HTTP details
  - JSON output for monitoring/automation
- **Parallel API Processing** - Batch processing for deep searches
  - Configurable concurrent API calls (default: 5)
  - Intelligent rate limiting (1 second between batches)
  - Significant speedup for artist-tracks/artist-albums deep searches

### Improved
- **MCP Parser** - Fixed position-based JSON extraction for array-root responses
  - Correctly handles both object-root and array-root JSON structures
  - Helper functions: extractArray() and extractObject() with fallback logic
- **Token Optimization** - ~50% reduction in MCP response tokens
  - Added compact helper functions (compactAlbum, compactArtist, compactTrack)
  - Strip URLs and MBIDs that LLMs can't use
  - Applied to lfm_albums, lfm_artists, lfm_tracks handlers
- **Spotify Authentication** - Improved resilience
  - Changed StartPlaybackAsync signature from string to List<string>
  - Ensures atomic album playback (all tracks in one API call)
  - Prevents race conditions in track ordering
- **Documentation Refactoring** - Split CLAUDE.md into focused files
  - Created SESSION_HISTORY.md - 7 archived sessions
  - Created IMPLEMENTATION_NOTES.md - Technical reference
  - Created LESSONS_LEARNED.md - Debugging patterns and best practices
  - CLAUDE.md reduced by 53% while maintaining current work focus

### Fixed
- **Album Queueing** - Fixed missing/reordered tracks in Spotify album playback
  - Changed from 12 separate API calls to ONE atomic call
  - Supports up to 100 URIs in one request
- **Depth Parameter Clarification** - Corrected understanding in guidelines
  - Depth = popularity ranking (top N by play count), NOT chronological
  - Updated guidelines to recommend deep:true as default
- **Cache Control** - Added support for cache control in recent command
  - Respects cache expiry settings
  - Configurable throttling behavior

## [1.4.0] - Previous Release

### Added
- Enhanced cache system with comprehensive management
- Comprehensive parameter validation and API improvements
- Tag filtering system for recommendations
- Recommendation diversity controls (totalArtists/totalTracks)
- Artist-specific track and album search
- Last.fm autocorrect support for artist name matching

### Improved
- Service layer architecture refactoring
- Date range support across all commands
- Unicode symbol support with auto-detection
- API throttling configuration (default: 200ms)

## [1.0.0] - Initial Release

### Added
- Core Last.fm API integration
- Basic commands: artists, tracks, albums
- File-based caching system
- Cross-platform support (Windows, Linux, WSL)
- Configuration management
- Spotify basic integration

---

[1.5.0]: https://github.com/Steven-Marshall/lfm/compare/v1.0.0...v1.5.0
[1.4.0]: https://github.com/Steven-Marshall/lfm/compare/v1.0.0...v1.4.0
[1.0.0]: https://github.com/Steven-Marshall/lfm/releases/tag/v1.0.0
