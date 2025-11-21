# Last.fm CLI - Claude Session Notes

## Project Overview
Last.fm CLI tool written in C# (.NET) for retrieving music statistics. The project has undergone significant refactoring to eliminate code duplication, simplify architecture, and optimize API usage. All major features are complete and production-ready.

## Current Architecture
- **Lfm.Cli**: CLI interface with commands and command builders
- **Lfm.Core**: Core functionality including services, models, and configuration
- **Lfm.Spotify**: Spotify integration for playback control
- **Lfm.Sonos**: Sonos integration via node-sonos-http-api
- **Setlist.fm Integration**: Concert search and setlist retrieval
- Uses System.CommandLine for CLI framework
- **File-based caching** with comprehensive cache management (119x performance improvement)
- Centralized error handling and display services
- **MCP Server**: Full integration with 32 tools for LLM interactions

## Key Files
- `src/Lfm.Cli/Program.cs` - Main entry point and DI setup
- `src/Lfm.Cli/Commands/BaseCommand.cs` - Shared command functionality
- `src/Lfm.Core/Services/LastFmApiClient.cs` - API client for Last.fm
- `src/Lfm.Core/Services/SetlistFmApiClient.cs` - API client for Setlist.fm
- `src/Lfm.Core/Services/CachedLastFmApiClient.cs` - Decorator with comprehensive caching
- `src/Lfm.Core/Configuration/LfmConfig.cs` - Configuration with cache/Spotify/Sonos/Setlist.fm settings
- `src/Lfm.Spotify/SpotifyStreamer.cs` - Spotify playback integration
- `src/Lfm.Sonos/SonosStreamer.cs` - Sonos playback integration
- `lfm-mcp-release/server-core.js` - MCP server core (2,580 lines, 32 tools)
- `lfm-mcp-release/lfm-guidelines.md` - LLM usage guidelines (480 lines)

## Recent Sessions

### Session: 2025-11-21 (Setlist.fm MCP Integration)
- **Status**: ✅ COMPLETE - MCP tools for Setlist.fm integration fully implemented and tested
- **Major Accomplishments**:
  - **MCP Tool Implementation**: Added `lfm_concerts` and `lfm_setlist` tools to MCP server
  - **Config Display Update**: Updated `lfm config show` to display Setlist.fm API key status
  - **Empty Setlist Context**: Added `_note` field to setlist JSON output for better MCP integration
  - **Error Handling Improvements**: Refined 404 error handling to treat "no results" as expected behavior, not errors
- **Implementation Details**:
  - `lfm_concerts`: Search concerts by artist with filters (city, country, venue, tour, date, year, page)
  - `lfm_setlist`: Get detailed setlist by ID (concert details, full tracklist, venue information)
  - Both tools follow existing MCP patterns with proper validation, error handling, and JSON output
  - CLI commands return clean JSON with helpful messages for MCP consumers
- **Error Handling Pattern**:
  - 404s are "no results found", not errors (logged at Debug level, not Error)
  - Specific catch blocks for HttpRequestException with NotFound/404
  - User-friendly messages with actionable tips
  - Clean JSON responses for MCP integration
- **Testing Results**:
  - ✅ `lfm concerts "Radiohead" --year "2024" --json` - Returns 5 concerts with full details
  - ✅ `lfm setlist "13582d35" --json` - Returns 26-track setlist with concert info
  - ✅ JavaScript syntax validation passed
  - ✅ Both MCP tools properly registered and implemented
- **Files Modified**:
  - `src/Lfm.Cli/Commands/ConfigCommand.cs` - Added Setlist.fm API key display (line 142)
  - `src/Lfm.Cli/Commands/SetlistCommand.cs` - Added `_note` field for empty setlists, improved 404 handling
  - `src/Lfm.Cli/Commands/ConcertsCommand.cs` - Improved 404 error handling with helpful tips
  - `src/Lfm.Core/Services/SetlistFmApiClient.cs` - Changed 404 logging to Debug level
  - `lfm-mcp-release/server-core.js` - Added 2 new MCP tools (lines 1199-1256 definitions, 2498-2577 implementations)
- **Key Insights**:
  - Setlist.fm API returns 404 for both "artist not found" and "no concerts found"
  - MCP always uses JSON mode, so all output must be clean and parseable
  - Contextual metadata fields (`_note`, `_suggestion`) help LLMs interpret results
  - Log stderr separation (from Session 2025-11-12) ensures JSON output safety
- **Build Status**: ✅ Clean build, all features tested and working
- **MCP Integration**: ✅ Complete with 32 tools total (30 existing + 2 new Setlist.fm tools)

### Session: 2025-11-12 (Logging Fix & Date Range Query Debugging)
- **Status**: 🟡 IN PROGRESS - Logging fix deployed, intermittent MCP failures under investigation
- **Initial Problem**: Year-based queries (`--year 2025`) returning empty results via MCP, appeared intermittent
- **Root Cause Discovered**: .NET console logger writes to stdout by default, polluting JSON output for MCP server
- **The Fix**: `src/Lfm.Cli/Program.cs` (lines 216-218)
  ```csharp
  logging.AddConsole(options => {
      options.LogToStandardErrorThreshold = LogLevel.Trace;  // Redirect ALL logs to stderr
  });
  ```
- **Why This Matters**:
  - MCP server's `parseJsonOutput()` expects clean JSON in stdout
  - Date range queries trigger "Aggregating..." logs (always, even without debug mode)
  - These logs contaminated stdout → `parseJsonOutput()` failed → empty results
- **Investigation Journey**:
  1. **Initial Suspicion**: Blamed Last.fm API for 500 errors (user corrected: "it's ALWAYS OUR CODE")
  2. **Discovery**: Logs going to stdout instead of stderr
  3. **Testing**: Local Windows CLI showed logs mixed with JSON in stdout
  4. **Solution**: Single line fix to redirect logs to stderr
  5. **Deployment**: Synced to Spark, rebuilt Docker container (ARM64)
  6. **Validation**: CLI works perfectly when tested directly via SSH
  7. **Mystery**: MCP still returns empty results intermittently
- **Key Testing Results**:
  - ✅ **CLI Direct (SSH)**: `lfm artists --year 2025 --limit 10` returns clean JSON with 10 artists
    - Stdout: Clean JSON only
    - Stderr: Logs only (or empty when debug logging disabled)
  - ⚠️ **MCP via Claude Desktop**: Still intermittent empty results
    - Sometimes works (especially with cache hit)
    - Often fails with `{"success": true, "artists": [], "count": 0}`
- **Debug Logging Insights** (enabled temporarily):
  - Retry logic IS working correctly:
    - Page 1: 500 error → retry after 1000ms → 200 success ✅
    - Page 2: 500 error → retry after 1000ms → 200 success ✅
  - Year 2025 queries take 90+ seconds (many API pages, each with potential retries)
  - When Last.fm is stable: All retries succeed → full results
  - When Last.fm is flaky: Some pages fail after 3 retries → empty results
- **Files Modified**:
  - `src/Lfm.Cli/Program.cs` - Logging stderr redirect (line 216-218)
  - `src/Lfm.Cli/Commands/ConfigCommand.cs` - Added `DiffConfigAsync()` method
  - `src/Lfm.Cli/CommandBuilders/ConfigCommandBuilder.cs` - Wired up config diff command
  - `lfm-mcp-release/Dockerfile` - Changed SDK 9.0 → 8.0 (line 8)
  - `C:\Users\steve\AppData\Roaming\Claude\claude_desktop_config.json` - Removed local MCP servers
- **Spark Deployment Status**:
  - Source synced: Nov 12 11:01 (Program.cs with logging fix)
  - Binary built: Nov 12 12:33 (in Docker container)
  - Container: Recreated with `--force-recreate`, using new image
  - URL: `https://spark.taild15745.ts.net/mcp` (Tailscale Funnel)
- **Source File Verification** (all match local via md5sum):
  - `src/Lfm.Cli/Program.cs` - Nov 12 11:01 ✅
  - `src/Lfm.Core/Services/LastFmApiClient.cs` - Nov 11 15:05 ✅
  - `src/Lfm.Cli/Commands/ConfigCommand.cs` - Nov 12 11:47 ✅
  - `lfm-mcp-release/server-core.js` - Nov 4 17:35 ✅
  - `lfm-mcp-release/server-http.js` - Nov 11 12:41 ✅
- **Remaining Mystery**:
  - MCP server logs show NO tool execution logging (only session connect/disconnect)
  - CLI works perfectly, MCP fails intermittently
  - Suggests issue in MCP server layer (server-http.js → server-core.js → CLI)
  - Need to investigate why tool calls aren't being logged
- **🔍 CRITICAL FINDING** (End of Session):
  - **`--year 2025` fails intermittently** via MCP → returns empty results
  - **`--from 2025-01-01 --to 2025-12-31` WORKS reliably** via MCP → returns full results
  - Same date range, different parameter syntax, different behavior
  - **Hypothesis**: `--year` parameter handling differs from `--from/--to` in CLI or MCP layer
  - This suggests the bug is NOT in Last.fm API or network issues (those would affect both)
  - Points to specific code path difference in how year vs date range parameters are processed
- **Next Steps** (When Resuming):
  1. **PRIORITY**: Investigate `--year` vs `--from/--to` parameter processing differences
  2. Check if `--year` parameter is being passed correctly to CLI from MCP
  3. Compare CLI output for `--year 2025` vs `--from 2025-01-01 --to 2025-12-31` directly
  4. Test MCP server directly (curl/HTTP) with both parameter styles
  5. Add MCP-level logging to trace parameter passing
  6. Evaluate timeout handling for long-running queries (90+ seconds)
- **User Insights**:
  - "it's ALWAYS OUR CODE until definitely proven otherwise" (debugging philosophy)
  - Emphasized step-by-step investigation without running ahead
  - Confirmed intermittent behavior: some queries work, others fail with same parameters
- **Build Status**: ✅ Clean build, binary deployed to Spark, CLI works perfectly via SSH

### Session: 2025-11-10 (Tailscale Funnel Remote MCP Access)
- **Status**: ✅ COMPLETE - Remote MCP access working on iPhone via Tailscale Funnel
- **Major Accomplishments**:
  - **Tailscale Funnel Setup**: Public HTTPS exposure for MCP server without port forwarding
  - **Claude iOS Integration**: Successfully configured custom connector for remote MCP access
  - **Cloudflare Cleanup**: Removed temporary Cloudflare tunnel setup
  - **Sonos Configuration Fix**: Corrected network configuration for Docker environment
  - **Comprehensive Documentation**: Created TAILSCALE_SETUP.md deployment guide
- **Key Technical Details**:
  - **Public URL**: `https://laptopstudio.taild15745.ts.net` (Tailscale Funnel)
  - **Port Configuration**: Changed from 8002:8002 to 8443:8002 (Funnel requires 443/8443/10000)
  - **Authentication**: Temporarily disabled (Claude.ai only supports OAuth, not Bearer tokens)
  - **Sonos Bridge**: Corrected to `http://192.168.1.24:5005` (pitorrent, not pihole)
  - **Docker Networking**: Container can reach network IPs but not localhost on host
- **Implementation Steps**:
  1. Cleaned up Cloudflare Tunnel (stopped quick tunnel, deleted config files)
  2. Installed Tailscale on Windows via `winget install tailscale.tailscale`
  3. Authenticated Tailscale and enabled Funnel (required one-time tailnet approval)
  4. Tested connectivity: Health endpoint ✅, MCP endpoint with auth ✅
  5. Disabled authentication for Claude.ai compatibility
  6. Configured custom connector: `https://laptopstudio.taild15745.ts.net/mcp`
  7. Tested on iPhone Claude app: **"working very well"** ✅
  8. Fixed Sonos bridge IP (discovered .21 was pihole, .24 is pitorrent with bridge)
- **Configuration Changes**:
  - `docker-compose.yml`:
    - Port mapping: `8443:8002` (Tailscale Funnel compatible)
    - Authentication disabled: `# - AUTH_TOKEN=${LFM_AUTH_TOKEN}`
  - Both config files updated:
    - Sonos bridge: `http://192.168.1.24:5005` (pitorrent)
    - Works from both Windows host and Docker container
  - Created `TAILSCALE_SETUP.md` (350+ lines):
    - Complete installation guide for Windows
    - Testing procedures and troubleshooting
    - Configuration management strategies
    - Environment variable override documentation
    - Spark (ARM64) migration plan
- **Security Considerations**:
  - **Current**: HTTPS via Tailscale Funnel (end-to-end encrypted), no app-level auth
  - **Future**: Add oauth2-proxy sidecar for production authentication
  - Tailscale provides secure tunnel (cannot decrypt traffic)
  - URL is obscure but publicly accessible
- **Next Steps**:
  - Test Sonos playback from iPhone via MCP tools
  - Deploy to Spark (ARM64) for production use
  - Consider OAuth proxy for production authentication
  - Update Claude.ai connector to Spark's URL when ready
- **Files Modified**:
  - `lfm-mcp-release/docker-compose.yml` - Port mapping and auth disabled
  - `lfm-mcp-release/config.json` - Sonos bridge IP corrected to .24
  - `C:\Users\steve\AppData\Roaming\lfm\config.json` - Sonos bridge IP corrected
  - `TAILSCALE_SETUP.md` - NEW - Complete deployment guide (353 lines)
- **User Feedback**:
  - "holy moly. it's working!!" (iPhone test success)
  - "working very well" (final verification)
- **Build Status**: ✅ Docker container running, Tailscale Funnel active

### Session: 2025-11-04 (Config Export/Import Commands)
- **Status**: ✅ COMPLETE - CLI commands for config management and Docker deployment
- **Major Features Implemented**:
  - **Config Export Command**: Export configuration to any location or Docker deployment
  - **Config Import Command**: Import configuration with validation and automatic backup
  - **Docker Integration**: Direct export to Docker mount point with optional container restart
  - **Project Root Detection**: Automatically finds lfm-mcp-release/ directory
- **CLI Commands Added**:
  - `lfm config export --to-docker` - Export to lfm-mcp-release/config.json
  - `lfm config export --to-docker --restart` - Export and restart container
  - `lfm config export --output <path>` - Export to custom location
  - `lfm config import <file>` - Import with validation and backup
- **Key Technical Components**:
  - `ConfigCommand.ExportConfigAsync()` - Export logic with project root detection
  - `ConfigCommand.ImportConfigAsync()` - Import with JSON validation and backup
  - `ConfigCommand.FindProjectRoot()` - Recursive directory search for lfm-mcp-release/
  - Docker restart integration using `docker-compose` CLI
- **Implementation Details**:
  - Project root detection searches up directory tree for lfm-mcp-release/
  - Import validates JSON before copying (prevents corrupting config)
  - Automatic timestamped backups on import
  - Helpful error messages suggest alternatives when project root not found
- **Documentation Updates**:
  - Updated DOCKER.md with new Quick Start section showing export command
  - Added "Configuration Updates" section to Updating workflow
  - Moved from "Future Enhancement Plans" to "Implemented" in CLAUDE.md
- **Testing Results**:
  - ✅ Export to Docker working correctly
  - ✅ Docker restart integration functional
  - ✅ Project root detection working (finds lfm-mcp-release/ from subdirectories)
  - ✅ Error handling for missing project directory
- **User Value**:
  - Eliminates manual config file copying for Docker deployments
  - Simplifies workflow: `lfm config export --to-docker --restart` (one command)
  - Safe import with automatic backups
  - Works from any directory within project
- **Build Status**: ✅ Clean build (0 errors, 10 pre-existing nullable warnings)
- **Files Modified**:
  - `src/Lfm.Cli/Commands/ConfigCommand.cs` - Added export/import methods
  - `src/Lfm.Cli/CommandBuilders/ConfigCommandBuilder.cs` - Added CLI subcommands
  - `lfm-mcp-release/DOCKER.md` - Added Quick Start export section
  - `CLAUDE.md` - Moved from TODO to implemented

### Session: 2025-11-02 (Streamable HTTP Upgrade & Local Model Testing)
- **Status**: ✅ COMPLETE - MCP SDK upgraded, local model limitations identified, server validated
- **Major Accomplishments**:
  - **MCP SDK Upgrade**: 0.6.1 → 1.20.2 (Streamable HTTP transport)
  - **Transport Migration**: SSE → Streamable HTTP (MCP Spec 2025-03-26)
  - **Multi-Platform Testing**: Tested with AnythingLLM, Open WebUI, and Claude Code
  - **Critical Finding**: Local open-source models have unreliable tool-calling behavior
- **Architecture Changes**:
  - Removed MCPO service (not needed for Streamable HTTP)
  - Migrated from `SSEServerTransport` to `StreamableHTTPServerTransport`
  - Single `/mcp` endpoint for all communication (POST, GET, DELETE)
  - Updated docker-compose.yml to remove MCPO bloat
- **Key Technical Changes**:
  - `server-http.js`: Migrated to StreamableHTTPServerTransport with session management
  - Fixed express.json() body parsing issue (was consuming request stream)
  - Proper session cleanup with `cleanupSession()` function
  - Updated transport endpoints: POST /mcp (init/messages), GET /mcp (SSE stream), DELETE /mcp (close)
- **Testing Results**:
  - ✅ **Claude Code**: Perfect integration, reliable tool calling
  - ⚠️ **AnythingLLM + Qwen 30B**: First query works, subsequent queries fabricate data
  - ⚠️ **AnythingLLM + GPT-OSS 120B**: Queries 1-2 work, complex query 3 fabricates
  - ⚠️ **Open WebUI + GPT-OSS 120B**: lfm_init works, next query fabricates despite connection working
  - ✅ **Open WebUI + Claude Sonnet 4.5**: Flawless execution across multiple complex queries
- **Root Cause Analysis**:
  - **NOT a server issue**: Connection works, auth passes, sessions maintained
  - **NOT a transport issue**: Streamable HTTP protocol working correctly
  - **Model behavior issue**: Local models choose to fabricate instead of calling available tools
  - Evidence: All platforms showed "Request body: undefined" in logs, but tools still executed successfully
  - The misleading log is just timing (body logged before transport reads stream internally)
- **AnythingLLM Issues Discovered**:
  - Agent mode auto-deactivates after brief timeout
  - Requires `@agent` prefix on every query to maintain tool access
  - Even with `@agent`, tool-calling unreliable after first success
  - Session state bug: First query after reset works, subsequent queries fail
- **Open WebUI Issues Discovered**:
  - Same pattern: Connection works, but models don't reliably call tools
  - 120B model thinking: "We need to fetch data... must assume we have access... provide plausible output"
  - Model knows tools exist, knows what they do, but chooses fabrication
- **Validation Testing with Claude**:
  - User tested Claude Sonnet 4.5 via Claude Code against same MCP server
  - Complex multi-query test: "Top 25 artists, top 25 albums, compare to last 3 months"
  - Result: ✅ Perfect execution - called all appropriate tools, compared datasets, identified patterns
  - Demonstrates server is production-ready and working perfectly
- **Key Insights**:
  - **Tool-calling is a frontier capability** where Claude maintains significant edge
  - Local models (even 120B parameters) lack reliable tool-calling behavior
  - Problem worsens with query complexity (simple queries work, complex analysis fails)
  - The "Request body: undefined" debug log was a red herring - not the actual problem
- **Files Modified**:
  - `lfm-mcp-release/package.json` - MCP SDK 0.6.1 → 1.20.2
  - `lfm-mcp-release/server-http.js` - Migrated to StreamableHTTPServerTransport
  - `lfm-mcp-release/docker-compose.yml` - Removed MCPO service
  - Fixed body parsing middleware to not consume request stream
- **Configuration Tested**:
  - AnythingLLM: `plugins/anythingllm_mcp_servers.json` with streamable-http type
  - Open WebUI: v0.6.34 (confirmed MCP support)
  - Claude Code: `.claude.json` with http transport type
- **Production Recommendations**:
  - ✅ Deploy to Spark for Claude Code use (proven reliable)
  - ❌ Don't rely on local models for MCP tool-calling (unreliable)
  - ⏸️ Future test: Claude Sonnet 4.5 via Open WebUI (isolate platform vs model variable)
- **Next Steps**:
  - Deploy to Spark (ARM64) for Claude Code remote access
  - Optional: Test Claude via Open WebUI to confirm platform vs model hypothesis
  - Document findings for future local model MCP development
- **Build Status**: ✅ Clean build, Docker container working perfectly
- **Branch**: `feature/sse-transport` (renamed from SSE, now Streamable HTTP)

### Session: 2025-11-01 (SSE/MCPO Docker Deployment - LOCAL TESTING COMPLETE)
- **Status**: 🟡 IN PROGRESS - Local x64 testing complete, ready for Spark ARM64 deployment
- **Major Features Implemented**:
  - **SSE Transport for Remote Access**: HTTP/SSE server for Claude Code/Desktop remote connectivity
  - **MCPO Integration**: MCP over OpenAPI for Open WebUI browser-based LLM chat
  - **Docker Multi-Transport Architecture**: Single codebase supporting stdio, SSE, and OpenAPI
  - **Multi-Architecture Support**: Build system supports both linux-x64 and linux-arm64
- **Architecture Overview**:
  ```
  Current Setup (Windows x64):
  ├── lfm-mcp-http (port 8002) → Claude Code via SSE ✅ TESTED
  └── lfm-mcpo (port 8001) → Open WebUI via OpenAPI ✅ TESTED

  Future Setup (Spark ARM64):
  ├── lfm-mcp-http (port 8002) → Remote Claude access
  └── lfm-mcpo (port 8001) → Open WebUI integration
  ```
- **Key Technical Components**:
  - `lfm-mcp-release/server-core.js` - Shared MCP logic extracted from server.js
  - `lfm-mcp-release/server-http.js` - SSE transport implementation (HTTP server wrapping MCP)
  - `lfm-mcp-release/Dockerfile` - Multi-stage build (.NET SDK → Node.js runtime)
  - `lfm-mcp-release/Dockerfile.mcpo` - Multi-stage build (.NET SDK → Python 3.11 + Node.js + mcpo)
  - `lfm-mcp-release/docker-entrypoint.sh` - Startup script for SSE server
  - `lfm-mcp-release/docker-entrypoint-mcpo.sh` - Startup script for MCPO server
  - `lfm-mcp-release/docker-compose.yml` - Orchestrates both services with shared config/cache
  - `lfm-mcp-release/.env` - TARGET_ARCH configuration (linux-x64 or linux-arm64)
- **Critical Fixes During Implementation**:
  1. **Session Routing Bug**: POST messages routed to `sessionArray[0]` (oldest) → fixed to use `sessionArray[sessionArray.length - 1]` (newest)
  2. **Config File Path**: LFM CLI hardcodes `/root/.config/lfm/config.json` → entrypoint copies mounted config to expected location
  3. **Environment Variables**: Node.js spawn() wasn't passing env vars → added `env: process.env` parameter
  4. **libicu Dependency**: Python 3.11-slim uses Debian Trixie with libicu74 → changed from `libicu72` to `libicu-dev`
  5. **Claude Desktop SSE**: Discovered Claude Desktop only supports stdio, not SSE (Zod validation rejects)
- **Testing Results** (Local x64):
  - ✅ SSE Server (port 8002):
    - Claude Code successfully connected via `.claude.json` config
    - Last.fm API: 10/10 endpoints healthy
    - Sonos playback: Successfully played "Hallogallo" by NEU!
    - Spotify playback: Successfully played "Every You Every Me" by Placebo
  - ✅ MCPO Server (port 8001):
    - OpenAPI docs available at http://localhost:8001/docs
    - Tested `POST /lfm_tracks` - Returned top 5 tracks correctly
    - Tested `POST /lfm_current_track` - Showed "Hallogallo" paused on Sonos
    - All 30+ MCP tools exposed as REST endpoints
- **Configuration Files**:
  - `.claude.json` (Claude Code) - SSE transport config:
    ```json
    "lfm-docker": {
      "type": "sse",
      "url": "http://localhost:8002/sse",
      "headers": {
        "Authorization": "Bearer <token>"
      }
    }
    ```
  - `claude_desktop_config.json` - Stdio only (SSE not supported by Claude Desktop)
- **Docker Configuration**:
  - Shared volumes: `config.json` (read-only) and `lfm-cache` (persistent)
  - Environment variables: `HTTP_PORT`, `AUTH_TOKEN`, `ALLOWED_ORIGINS`, `MCPO_PORT`, `LFM_CACHE_PATH`
  - Health checks: SSE uses Node.js HTTP check, MCPO uses curl (but MCPO doesn't have /health endpoint - this is expected)
- **⏸️ NEXT STEPS (After Break)**:
  1. **Test MCPO from WebUI on Laptop**: Verify Open WebUI can connect to http://localhost:8001 and use LFM tools
  2. **Verify Spotify Integration in WebUI**: Ensure playback commands work through OpenAPI endpoints
  3. **Deploy to Spark (ARM64)**:
     - Update `.env`: Change `TARGET_ARCH=linux-arm64`
     - Rebuild both containers: `docker-compose build`
     - Copy `docker-compose.yml`, `.env`, `config.json` to Spark
     - Start services on Spark: `docker-compose up -d`
     - Update Claude Code config to point to Spark's IP: `http://<spark-ip>:8002/sse`
     - Configure Open WebUI to use Spark's MCPO: `http://<spark-ip>:8001`
- **Build Status**: ✅ Clean build on x64, ready for ARM64 rebuild
- **Branch**: `feature/sse-transport` (ready to merge after Spark deployment tested)
- **Files Modified**:
  - `lfm-mcp-release/server-core.js` - Environment variable passing fix (line 22-25)
  - `lfm-mcp-release/server-http.js` - Session routing fix (line 210), debug logging (lines 257-258)
  - `lfm-mcp-release/docker-entrypoint.sh` - Config file copy (lines 20-29)
  - `lfm-mcp-release/Dockerfile` - Multi-arch build arg support (lines 4-5, 10, 28-31)
  - `lfm-mcp-release/Dockerfile.mcpo` - NEW - MCPO Docker build (98 lines)
  - `lfm-mcp-release/docker-entrypoint-mcpo.sh` - NEW - MCPO startup script (36 lines)
  - `lfm-mcp-release/docker-compose.yml` - Enabled MCPO service, added build args (lines 7-11, 36-58)
  - `lfm-mcp-release/.env` - Documented TARGET_ARCH configuration (lines 11-18)

### Session: 2025-10-30 (Spotify Playlist Management)
- **Status**: ✅ COMPLETE - Full playlist playback and listing functionality
- **Major Features Implemented**:
  - **Playlist Playback by Name**: Play user playlists with fuzzy/exact name matching
  - **Playlist Listing**: List all user playlists with track counts and ownership status
  - **Two-Phase Disambiguation**: Same pattern as albums (discovery → exact match)
  - **Dual Player Support**: Both Spotify and Sonos integration
  - **MCP Integration**: Two new tools (lfm_play_playlist, lfm_get_playlists)
- **Key Technical Components**:
  - `SpotifyModels.cs`: New `PlaylistSearchResult` model for disambiguation
  - `SpotifyStreamer.cs`:
    - `SearchPlaylistByNameAsync()` - Fuzzy search with exactMatch parameter
    - `PlayPlaylistAsync()` - Play playlist by ID
  - `SonosStreamer.cs`: `PlayPlaylistAsync()` using `spotify:user:spotify:playlist:{id}` format
  - `PlaylistCommand.cs`: Play command with player routing and disambiguation
  - `PlaylistsCommand.cs`: List all user playlists
  - `server.js`: Two new MCP tools added
- **Implementation Details**:
  - **User Playlists Only**: No public/Spotify playlists (design decision for simplicity)
  - **Fuzzy Search**: Case-insensitive contains matching by default
  - **Exact Match**: Case-insensitive equality when `exactMatch=true`
  - **Edge Case Handling**: Multiple playlists with identical names → take first
  - **URI Formats**:
    - Spotify: `spotify:playlist:{id}`
    - Sonos: `spotify:user:spotify:playlist:{id}` (required prefix per node-sonos-http-api)
- **Interface Updates**:
  - `IPlaylistStreamer`: Added `SearchPlaylistByNameAsync()` and `PlayPlaylistAsync()`
  - `ISonosStreamer`: Added `PlayPlaylistAsync()`
- **CLI Commands**:
  - `lfm playlists` - List all playlists with track counts
  - `lfm playlist --name "Name"` - Play playlist
  - `lfm playlist --name "Name" --exact-match` - Force exact matching
  - Works with `--player`, `--device`, `--room`, `--json` flags
- **MCP Tools**:
  - `lfm_get_playlists()` - Returns array of {name, trackCount, isOwned}
  - `lfm_play_playlist(playlistName, exactMatch, player, device, room)`
- **Testing & Verification**:
  - ✅ Clean build (0 errors, 10 pre-existing warnings)
  - ✅ Consistent disambiguation pattern with albums
  - ✅ Both Spotify and Sonos playback paths implemented
- **Build Status**: ✅ Clean build, files copied to publish/win-x64
- **Commit**: `9a434e4` - feat: Add Spotify playlist playback and listing

### Session: 2025-10-29 (Album Disambiguation & Deep Search Bug Fixes)
- **Status**: ✅ COMPLETE - Spotify album exact matching + deep search timeout JSON fix + lfm_check guidelines
- **Major Features Implemented**:
  - **Two-Phase Album Selection**: Discovery phase shows all versions, exact match phase forces specific album
  - **Client-Side Filtering**: Request multiple Spotify results and filter for exact name match
  - **MCP Integration**: Added exactMatch parameter to lfm_play_now and lfm_queue tools
  - **Comprehensive Guidelines**: Added detailed usage documentation for LLMs
- **Problem Discovery**:
  - User's friend (Claudette) discovered "Neu!" by Neu! was playing "Neu! 75" instead of correct album
  - Root cause: Spotify's search API is always fuzzy/relevance-based with no exact-match mode
  - Code was using `limit=1`, only getting Spotify's top-ranked result (most popular)
- **Solution Architecture**:
  - Changed from `limit=1` to `limit=10` to get multiple Spotify results
  - Phase 1 (Discovery): When multiple versions found without exactMatch, return error listing all options
  - Phase 2 (Exact Match): When exactMatch=true, filter client-side for exact album name match
  - Edge case handling: When multiple albums have identical names, take first (most popular/canonical)
- **Key Technical Components**:
  - `SpotifyModels.cs`: New `AlbumSearchResult` and `AlbumVersionInfo` models
  - `SpotifyStreamer.cs`: Updated `SearchSpotifyAlbumTracksAsync` and `SearchSpotifyAlbumUriAsync` with exactMatch parameter
  - `PlayCommand.cs`: Added exactMatch parameter, enhanced error output with album version details
  - `PlayCommandBuilder.cs`: Added `--exact-match` CLI option, migrated to InvocationContext pattern
  - `server.js`: Added exactMatch parameter to lfm_play_now and lfm_queue MCP tools
  - `lfm-guidelines.md`: Added "Album Disambiguation - Handling Multiple Versions" section (lines 384-447)
- **Implementation Details**:
  - Both album search methods updated in 4 locations (primary + fallback paths)
  - Client-side exact matching: `albums.Where(a => a.Name.Equals(albumName, StringComparison.Ordinal))`
  - Edge case: When exactMatch finds multiple identical names, takes first (Spotify's canonical version)
  - Enhanced JSON error output includes all album versions with track counts and release dates
- **Brutal Edge Case**:
  - Discovered during testing: "Neu!" has TWO albums with identical names "Neu!" (1972)
  - Distinguishing features: 6 tracks (original) vs 39 tracks (deluxe/compilation)
  - Solution: When exactMatch=true and identical names exist, automatically take first result
  - User feedback: "brutal" but pragmatic solution for rare edge case
- **System.CommandLine Fix**:
  - Hit 10-parameter limit when adding exactMatch parameter
  - Migrated PlayCommandBuilder to InvocationContext pattern
  - Removes parameter count limitation for future extensibility
- **Guidelines for LLM Usage**:
  - Two-phase workflow: Discover options first, then use exactMatch to select specific version
  - User preference: Original studio albums over remasters/live/greatest hits unless specified
  - When to use: After "multiple versions detected" error, for self-titled albums, when user specifies version
  - When not to use: Don't use on first attempt (need to see options first)
- **Testing & Verification**:
  - ✅ Discovery phase correctly returns multiple album versions with details
  - ✅ Exact match phase correctly filters to specific album
  - ✅ Edge case handling verified with Neu! albums (identical names)
  - ✅ MCP tools properly pass exactMatch parameter to CLI
- **Build Status**: ✅ Clean build, workaround for file lock (copy from bin to publish)
- **Commit**: `273cebb` - feat: Add album disambiguation for exact matching
- **User Insight**: "funnily enough, on first listen of each album, i think neu!75 is my favourite"
- **Deep Search Bug Fix** (discovered and fixed same session):
  - **Problem**: `lfm_artist_albums` with `deep: true` returning `[{}]` from MCP
  - **Root Cause**: When Last.fm API endpoints were down, deep search timed out
  - **Secondary Cause**: ArtistSearchCommand timeout handler didn't respect `--json` flag
  - **Result**: MCP server received plain text ("⏰ Search timed out...") instead of JSON
  - **parseJsonOutput failure**: Couldn't find valid JSON, returned malformed `[{}]`
  - **Fix**: Modified OperationCanceledException handler in ArtistSearchCommand.cs (lines 281-329)
    - JSON mode: Returns `[]` for no matches, valid JSON array for partial results
    - Text mode: Keeps existing user-friendly messages
  - **Testing**: Verified with 1-second timeout returning valid `[]` instead of text
  - **Commit**: `99a8a43` - fix: Deep search timeout now respects --json flag
- **lfm_check Guidelines** (added to lfm-guidelines.md):
  - **Problem**: LLMs using `lfm_check` for album exploration hitting metadata mismatches
  - **Real Example**: Peter Gabriel conversation showed pattern:
    - User: "what have i listened to of his... probably PG 3 or 4 the most"
    - LLM: Multiple `lfm_check` calls with "Peter Gabriel 3: Melt", "Peter Gabriel 4: Security" → 0 plays
    - Reality: Scrobbled as "Peter Gabriel 3", "Security" (without prefix/suffix)
    - Total: 299 plays, but only 29 accounted for (270 missing due to metadata issues)
    - Solution: `lfm_artist_albums` immediately showed all 10 albums with correct play counts
  - **Guideline Added** (55 lines): "Using lfm_check - When It Works and When It Doesn't"
    - When to use: Artist checks (reliable), specific verification, yes/no questions
    - When NOT to use: Exploration ("what have you listened to"), building comprehensive views
    - Problem: Metadata fragmentation (remasters, punctuation, featured artists, spacing)
    - Better approach: Use `lfm_artist_albums` or `lfm_artist_tracks` for exploration
    - Shows ACTUAL metadata in user's scrobbles, not guessed canonical names
  - **Key Insight**: "album_artist is sort of the preferred way" - user's observation
  - **Commit**: `99a8a43` - same commit as deep search fix

### Session: 2025-10-15 (Documentation Refactoring, Parser Fix, Token Optimization)
- **Status**: ✅ COMPLETE - Documentation split, MCP parser fixed, token usage optimized
- **Major Accomplishments**:
  - **Documentation Refactoring**: Split CLAUDE.md (703→329 lines) into 4 focused files
  - **Contextual Reminders**: Added just-in-time guidance to 3 MCP tools
  - **Parser Bug Fix**: Fixed position-based JSON extraction for array-root responses
  - **Token Optimization**: ~50% token reduction for large MCP queries
  - **Spotify Auth Fix**: Improved resilience with List<string> signature
- **Documentation Split**:
  - Created `SESSION_HISTORY.md` - 7 archived sessions (250 lines)
  - Created `IMPLEMENTATION_NOTES.md` - Technical reference for completed features (184 lines)
  - Created `LESSONS_LEARNED.md` - Debugging patterns and best practices (299 lines)
  - Created `PARSING_BUG_ANALYSIS.md` - Comprehensive parser bug investigation
  - CLAUDE.md reduced by 53% while maintaining current work focus
- **Contextual Reminders Implementation**:
  - `lfm_check` (verbose mode): `_response_guidance` - Concise response pattern reminder
  - `lfm_artist_tracks`: `_depth_guidance` - Depth is popularity ranking, not chronological
  - `lfm_artist_albums`: `_depth_guidance` - Same depth parameter clarification
  - Concept: Just-in-time guidance appearing when context is relevant
  - Proven pattern from planning mode system reminders
- **Parser Bug Fix** (`lfm-mcp-release/server.js` lines 45-165):
  - **Root Cause**: Object-first bias extracted nested objects from array-root responses
  - **Problem Cases**:
    - `artist_albums` returning only first album from array `[{album1}, {album2}]`
    - `play_now` returning `[]` instead of full response object
  - **Solution**: Position-based parser extracts whichever structure (`[` or `{`) appears first
  - **Implementation**: Helper functions `extractArray()` and `extractObject()` with fallback logic
  - **Result**: Correctly handles both object-root and array-root JSON structures
- **Token Optimization** (`lfm-mcp-release/server.js` lines 139-165):
  - Added compact helper functions: `compactAlbum()`, `compactArtist()`, `compactTrack()`
  - Strip URLs and MBIDs that LLMs can't use
  - Applied to `lfm_albums`, `lfm_artists`, `lfm_tracks` handlers
  - **Result**: ~50% token reduction (100 albums: ~10,300 → ~5,150 tokens)
  - Architecture: Post-filtering at MCP layer (cache stores full data for all consumers)
- **Spotify Authentication Fix** (`src/Lfm.Spotify/SpotifyStreamer.cs`):
  - Changed `StartPlaybackAsync` signature from `string` to `List<string>`
  - Ensures atomic album playback (all tracks in one API call)
  - Prevents race conditions in track ordering
  - Complements Session 2025-01-19 Spotify album queueing fix
- **Key Insights**:
  - Position-based parser is architecturally correct (not a band-aid fix)
  - Post-filtering optimization: Only place to strip data is MCP layer
  - Data flow: Last.fm API → Cache (full) → MCP Server (filtered) → LLM
  - Contextual reminders: 82% token savings vs upfront guidelines (500 + 25/tool vs 4,000)
- **📋 Future Work - Playback Orchestration**:
  - **Issue**: LLM writes narrative before checking playback state
  - **User Feedback**: "you could have checked... but you were mid narrative"
  - **Example Failure**:
    - User asks "Is Fifth Dimension next?"
    - LLM writes long WHY narrative
    - LLM asks tentative "Want me to queue it?"
    - User had JUST FINISHED Mr. Tambourine Man (tools could show this)
  - **What Should Have Happened**:
    - Check `lfm_recent_tracks` → see Mr. Tambourine Man just finished
    - Check `lfm_current_track` → confirm nothing playing
    - Respond: "I see you just finished Mr. Tambourine Man. Fifth Dimension is the logical next step - shall I queue it?"
  - **The Challenge**:
    - Failure happens during text generation (before tool calls)
    - No technical hook point exists to intercept
    - Need to train behavioral reflex: "About to suggest playback? Check state first."
  - **User's Ideal Outcome** ("Championship Vinyl" DJ):
    - Data-informed AND permission-seeking
    - Confident AND polite (not tentative "want me to?")
    - Aware AND respectful (not pushy auto-play)
    - Template: "I see [data]. [Brief context]. Shall I [action]?"
  - **Proposed Solution - Phased Approach**:
    1. **Guideline** (immediate): Add prominent guideline with trigger word recognition
       - "Before phrases like 'Want me to queue', 'Shall I play' → STOP, check state first"
       - Mandatory workflow: recent_tracks → current_track → informed offer
       - Response template with good/bad examples
    2. **Test & Observe**: Collect failure/success patterns
    3. **Contextual Reminder** (if needed): Add `_playback_suggestion_guidance` to state-checking tools
    4. **Hook Experiment** (if needed): user-prompt-submit-hook with pattern detection
  - **Key Insight**: Problem is behavioral (text generation phase), not technical (tool call phase)
  - **Action**: Draft guideline using "DJ watching the floor" framing, position prominently in lfm-guidelines.md
  - **Status**: ✅ IMPLEMENTED (Session 2025-10-16) - Added "Playback State Awareness" section to lfm-guidelines.md
- **Build Status**: ✅ Clean build, all features tested and working
- **Testing**: Parser handles all cases, token reduction verified with 100 albums query
- **Documentation**: See `docs/PARSING_BUG_ANALYSIS.md` for detailed parser investigation

### Session: 2025-10-09 (Spotify Album Disambiguation & Parallel API Calls)
- **Status**: ✅ COMPLETE - Album version disambiguation and parallel API processing for deep searches
- **Major Features Implemented**:
  - **Album Version Disambiguation**: Detect and handle tracks that exist on multiple albums
  - **Parallel API Processing**: Batch parallel API calls for artist-tracks/artist-albums with intelligent throttling
  - **MCP Guidelines Refactoring**: Simplified from quiz-based to trust-based initialization system
  - **Album Check JSON Enhancements**: Added `guidelinesSuggested` and `interpretationGuidance` fields
- **Key Technical Components**:
  - `PlayCommand.cs`: Allow track + album parameter combination for version disambiguation
  - `SpotifyStreamer.cs`:
    - `SearchSpotifyTrackWithDetailsAsync`: Returns `TrackSearchResult` with version detection
    - `PlayNowFromUrisAsync`: Direct URI playback for albums
    - `QueueFromUrisAsync`: Direct URI queueing for albums
    - `SearchSpotifyAlbumTracksAsync`: One-shot album search returning all track URIs
  - `SpotifyModels.cs`: New `TrackSearchResult` model with `HasMultipleVersions` and `AlbumVersions` list
  - `ArtistSearchCommand.cs`: Parallel batch processing using `Task.WhenAll` with rate limiting
  - `CachedLastFmApiClient.cs`: `DisableThrottling` property for parallel batch mode
  - `LfmConfig.cs`: New `ParallelApiCalls` setting (default: 5 concurrent calls)
  - `CheckCommand.cs`: Enhanced JSON output with interpretation guidance
- **Album Version Disambiguation**:
  - **Problem**: Tracks like "Live and Let Die" exist on studio albums, greatest hits, live albums
  - **Solution**: Detect multiple versions, return error listing all album options
  - **User preference**: Studio albums preferred over live/greatest hits unless explicitly requested
  - **MCP integration**: LLM can specify album parameter when multiple versions detected
  - **Example**: "Hey Jude" appears on "Hey Jude", "1967-1970", "Past Masters" - user chooses which
- **Parallel API Calls**:
  - **Use case**: Deep searches (artist-tracks, artist-albums) that page through 1000+ pages
  - **Implementation**: Batch of 5 API calls executed concurrently using `Task.WhenAll`
  - **Rate limiting**: 1 second between batches to respect Last.fm's 5 req/sec limit
  - **Throttling**: Individual call throttling disabled during parallel execution
  - **Batch timing**: Dynamic delay calculation ensures compliance with rate limits
  - **Performance**: Significant speedup for deep searches without violating API limits
- **Configuration**:
  - `ParallelApiCalls`: Number of concurrent API calls in batch mode (default: 5)
  - Compatible with existing cache and throttle settings
- **Build Status**: ✅ Clean build, all features tested and working
- **Ready For**: Production use with MCP integration

### Session: 2025-10-11 (Sonos Integration & Unified Playback)
- **Status**: ✅ COMPLETE - Full Sonos playback integration with unified play command
- **Major Features Implemented**:
  - **Unified Play Command**: Single command supporting both Spotify and Sonos players
  - **Config-Driven Defaults**: DefaultPlayer and DefaultRoom configuration eliminates need for MCP to ask
  - **Player Routing**: Intelligent routing based on configuration with parameter overrides
  - **Sonos HTTP Bridge**: Integration with node-sonos-http-api for Sonos control
  - **Shared Search Logic**: Unified track search for both players with multiple version detection
- **Implementation Details**:
  - **node-sonos-http-api**: Node.js HTTP bridge running as systemd service on Raspberry Pi
  - **Room Management**: Auto-discovery, validation, and config storage of Sonos rooms
  - **Album Playback Strategy**: Album URIs for Sonos (plays as unit), track URIs for Spotify
  - **Queue Support**: Both immediate playback and queue modes for both players
- **New Configuration Options**:
  - `DefaultPlayer`: "Spotify" or "Sonos" (determines playback target)
  - `Sonos.HttpApiBaseUrl`: Bridge URL (e.g., "http://192.168.1.24:5005")
  - `Sonos.DefaultRoom`: Default Sonos room for playback
  - `Sonos.TimeoutMs`: HTTP timeout for Sonos API calls (default: 5000)
  - `Sonos.RoomCacheDurationMinutes`: Cache duration for room discovery (default: 5)
- **CLI Parameters Added**:
  - `--player` / `-p`: Override default player (Spotify/Sonos)
  - `--room` / `-r`: Override default Sonos room
  - Works with existing: `--track`, `--album`, `--queue`, `--device` (Spotify only)
- **Key Technical Components**:
  - `PlayCommand.cs`: Unified player routing with shared track search at top level
  - `PlayCommandBuilder.cs`: Enhanced CLI interface with player and room options
  - `SonosStreamer.cs`: HTTP client for node-sonos-http-api communication
  - `ISonosStreamer`: Interface defining Sonos operations (PlayNowAsync, QueueAsync, etc.)
  - `SpotifyStreamer.SearchSpotifyAlbumUriAsync`: New method for album URI lookup
  - `SonosConfig`: Configuration model for Sonos settings
- **Critical Bug Fixes**:
  - **URI Encoding Issue**: Sonos API requires raw Spotify URIs, not URL-encoded (colons must be preserved)
  - **Album Queue Behavior**: Using album URIs instead of track URIs for proper album playback on Sonos
  - **Multiple Version Detection**: Extended from Spotify-only to both players via unified search
  - **Access Token Initialization**: Added missing `EnsureValidAccessTokenAsync()` in `SearchSpotifyTrackWithDetailsAsync`
- **Sonos Commands Implemented**:
  - `lfm sonos rooms` - List all available Sonos rooms with grouping info
  - `lfm sonos status <room>` - Get current playback state for room
  - `lfm sonos pause <room>` - Pause playback
  - `lfm sonos resume <room>` - Resume playback
  - `lfm sonos skip <room> [next|previous]` - Skip tracks
  - `lfm config set-sonos-api-url <url>` - Configure bridge URL
  - `lfm config set-sonos-default-room "Room Name"` - Set default room
- **Usage Examples**:
  - `lfm play "Pink Floyd" --track "Money" --album "Dark Side of the Moon"` - Uses config default player
  - `lfm play "Pink Floyd" --track "Money" --player Sonos --room "Kitchen"` - Explicit player/room
  - `lfm play "Pink Floyd" --album "Dark Side of the Moon" --queue` - Queue entire album
- **Architecture Highlights**:
  - **Unified Search**: Single `SearchSpotifyTrackWithDetailsAsync` call before player routing
  - **Shared Logic**: Multiple version detection, album disambiguation work for both players
  - **Minimal Code Duplication**: Spotify search logic reused for both playback targets
  - **Config Flexibility**: Per-environment configuration (office uses Spotify, home uses Sonos)
- **Testing Results**:
  - ✅ Track playback to Sonos with multiple version detection
  - ✅ Album playback to Sonos as complete unit
  - ✅ Queue mode working for both tracks and albums
  - ✅ Player routing working with config defaults and parameter overrides
  - ✅ Room validation and error handling working correctly
  - ✅ Fuzzy album matching working ("darks side" matches "The Dark Side of the Moon")
- **Performance**: Leverages existing Spotify search API, no additional Last.fm API calls required
- **Build Status**: ✅ Clean build, all Sonos functionality tested and working
- **Ready For**: Production use and MCP integration

### Session: 2025-01-19 (MCP Guidelines Refinement & Spotify Album Queueing Fix)
- **Status**: ✅ COMPLETE - MCP guidelines enhanced, critical Spotify bug fixed, architecture optimization evaluated
- **Major Accomplishments**:
  - **MCP Guidelines Updates**: 4 sections added/modified based on real-world testing feedback
  - **Spotify Album Queueing Bug Fix**: Fixed missing/reordered tracks in album playback
  - **Depth Parameter Clarification**: Corrected understanding of popularity ranking vs chronological search
  - **Guidelines Architecture Evaluation**: Analyzed token usage and optimization strategies
  - **Contextual Reminders Concept**: Proposed just-in-time guidance system (82% token savings)
- **Guidelines Updates** (`lfm-mcp-release/lfm-guidelines.md`):
  1. **Track Position Hallucination Prevention** (lines 98-125):
     - User feedback: LLM confidently stating "track 2 is..." when it's actually track 1
     - Added "NEVER reference track numbers or positions without verification"
     - Verification workflow: Use `lfm_check(verbose: true)`, `lfm_current_track()`, or `lfm_recent_tracks()`
     - Key insight: Track positions are edge-case data for LLMs, leading to false confidence
  2. **lfm_check Fallback Strategy** (lines 83-91):
     - When check returns 0 plays, don't assume user hasn't heard it
     - Fallback: Use `lfm_artist_albums` or `lfm_artist_tracks` with `deep: true`
     - Handles metadata variations: spacing ("Renée / Pretty" vs "Renée/Pretty"), remaster suffixes, featuring artists
     - Real scrobbles are source of truth, not Last.fm's canonical names
  3. **Depth Parameter Simplification** (lines 137-165):
     - **CRITICAL**: Clarified depth = popularity ranking (top N by play count), NOT chronological
     - User correction: "is the depth 'tracks' or 'pages'? let's think about edge cases"
     - Example: Left Banke with 14 plays ranks #2324, missed at depth:2000 (user has 2,323 tracks with MORE plays)
     - Edge case: Breadth listeners (Guided By Voices: 100 albums × 1-2 plays each = all tracks rank 5000-7000, depth:2000 finds ZERO)
     - Simplified approach: Default to `deep: true`, let cache handle performance
     - Removed complex tier explanations that led to wrong mental model
  4. **Concise Response Pattern** (lines 265-281):
     - User feedback: "stop all the blurb... 'Yes you have, you've listened to White Light/White Heat 4-5 times'. done."
     - Pattern: Answer directly with ONE interesting detail max
     - Good example: "Yes, you've listened to White Light/White Heat 4-5 times (21 plays, including the 17-minute 'Sister Ray')."
     - Bad example: Track-by-track breakdown, metadata discrepancy math explanations, music history lessons
- **Spotify Bug Fix** (`src/Lfm.Spotify/SpotifyStreamer.cs`):
  - **Problem**: Simon & Garfunkel "Bookends" (12 tracks) had 2 tracks missing and some reordered
  - **Root Cause**: 12 separate API calls (1 play + 11 individual queues) caused race conditions
  - **Solution**: Changed to ONE atomic API call with all URIs
  - **Implementation**:
    - Modified `StartPlaybackAsync` signature from `string` to `List<string>` (lines 1118-1128)
    - Rewrote `PlayNowFromUrisAsync` to use single atomic call (lines 233-277)
    - Spotify Web API supports up to 100 URIs in one request
  - **User Confirmation**: "it queued up cleanly as well so the spotify album play fix is working" ✅
- **Metadata Matching Investigation**:
  - **Case**: Left Banke album "Walk Away Renée / Pretty Ballerina" returned 0 plays
  - **Root Cause**: Spacing difference (MCP query had spaces around slash, scrobbles didn't)
  - **Findings**:
    - Last.fm autocorrect enabled for all lookups (handles typos, capitalization)
    - Custom apostrophe retry logic handles Unicode variants (U+0027, U+2018, U+2019)
    - NO handling for punctuation spacing differences (slash, dash, etc.)
  - **User Decision**: "yes. that feels a stretch too far" (regarding adding slash fuzzy matching)
  - **Mitigation**: Added guidelines recommending fallback to artist_albums with deep:true
- **Guidelines Architecture Analysis**:
  - **Current State**: 479 lines, 2,805 words, ~4,000 tokens (2% of 200K context window)
  - **Project Size**: 19,230 LOC C# code, 2,458 lines MCP server (server.js)
  - **User Concern**: "it's getting quite long... on the edge of becoming top heavy"
  - **Options Evaluated**:
    1. Single file with TOC summary (baseline)
    2. Split into focused documents (core/technical/examples) - 75% token savings
    3. Hybrid section-based retrieval
    4. **Contextual reminders in MCP tool outputs** (user's idea) - 82% savings
  - **Contextual Reminders Concept**:
    - User insight: "with our experience with planning mode it might be more responsive to putting in reminders in either the mcp tool or comments in function outputs"
    - Embed `_guideline_reminder` fields in MCP tool responses
    - Just-in-time guidance appearing exactly when context is relevant
    - Proven pattern from plan mode system reminders
    - Token comparison: 500 baseline + 25 per tool call vs 4,000 baseline
  - **User Decision**: "i think this is a sleep on it moment for me"
  - **Approach**: "step by step and tests what works and what doesn't as we go along. be systematic about it."
  - **Documentation**: User requested documenting insights for future reference even though implementation deferred
- **Key Insights**:
  - **Depth Parameter**: Popularity ranking, not chronological - fundamental misunderstanding in guidelines
  - **LLM Blind Spots**: Track positions and album metadata are edge cases leading to false confidence
  - **Response Style**: Users want concise answers, not data analyst explanations
  - **Just-in-Time Guidance**: More elegant than file splitting, proven pattern from existing systems
- **Testing & Verification**:
  - Left Banke deep search working correctly with proper depth understanding
  - Spotify album queueing verified by user in production
  - Guidelines updates validated through real conversation examples
  - Token analysis confirms guidelines are only 2% of context budget
- **Build Status**: ✅ Clean build (0 errors, 10 pre-existing nullable warnings unrelated to changes)
- **Next Steps**:
  - User to evaluate contextual reminders approach
  - Systematic testing if/when implemented
  - Continue gathering evidence on guidelines usage patterns
- **✅ Recommendations Tool Usage Guidelines** (Session 2025-10-16):
  - **Status**: ✅ IMPLEMENTED - Added "Using lfm_recommendations - LLM Reasoning First" section to lfm-guidelines.md
  - **Issue**: Recommendations tool may be overused in MCP context
  - **History**: Built originally for CLI, later exposed via MCP
  - **Key Insight**: For good reasoning LLMs, recommendations is rarely needed (9/10 times LLM reasoning is better)
  - **Implementation**: Added 83-line section (lines 108-190) emphasizing:
    - "In general, lfm_recommendations is NOT a great starting point"
    - "Your musical knowledge is more valuable than raw similarity scores"
    - When NOT to use: Beatles, Pink Floyd, well-known artists (LLM knows discography)
    - When to use (rarely): Truly obscure artists, as supplement to augment AND filter
    - Workflow: LLM reasoning first → optionally supplement with tool → augment/filter → curate
  - **Examples**: Real bad/good examples showing over-reliance vs LLM-first reasoning
  - **User Clarification**: "not even just starting pool then filter... augment AND filter with judgment"
- **✅ Listen Before You Speak - Core Workflow Methodology** (Session 2025-10-16):
  - **Status**: ✅ IMPLEMENTED - Added "Listen Before You Speak" section to Response Style (lfm-guidelines.md lines 31-43)
  - **Origin**: Test LLM feedback: "I need to internalize: data → think → more data → think → narrative"
  - **Problem**: LLM starting narratives before completing research phase, discovering gaps mid-response
  - **Real Example**: Taylor Swift analysis claiming she's an "outlier" without checking artist_albums showed she's top-tier (#9, #13, #23)
  - **Implementation**: Ultra-concise 9-line section positioned immediately after "Be a DJ buddy" header
  - **Key Principles**:
    - "A DJ buddy has spun the records before talking about them"
    - Workflow: data → think → more data → think → narrative (NOT start narrative → discover gaps)
    - The test: "Would you need to check mid-response?" → If yes, gather more data first
    - "When you can see the complete pattern → speak with authority"
  - **Design Philosophy**: REALLY SIMPLE AND CLEAN - foundational methodology before DO/DON'T lists
  - **Integration**: Extends DJ metaphor (spun the records = gathered the data)

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
- Spotify/Sonos integration requires additional configuration

## Current Project Plans & Status

### ✅ Primary Development - COMPLETE
**All major architecture and functionality complete**. The CLI tool is fully functional with:
- ✅ Complete service layer architecture
- ✅ Comprehensive API throttling and reliability
- ✅ Full caching implementation with management (119x improvement)
- ✅ All core commands working (artists, tracks, albums, recommendations, toptracks, mixtape)
- ✅ Date range support across all commands
- ✅ Unicode symbol support with auto-detection
- ✅ Spotify + Sonos playback integration
- ✅ MCP server with 30 tools for LLM interaction
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

#### 2. **Config Export/Import Commands** ✅
- **Status**: ✅ IMPLEMENTED (Session 2025-11-04)
- **Commands Available**:
  - `lfm config export --to-docker` - Export to lfm-mcp-release/config.json for Docker
  - `lfm config export --to-docker --restart` - Export and restart container automatically
  - `lfm config export --output <path>` - Export to any file location
  - `lfm config import <file>` - Import config from file (with validation and backup)
- **Key Features**:
  - Automatic project root detection (searches up directory tree)
  - Docker container restart integration
  - JSON validation before import
  - Automatic backup of existing config on import
  - Helpful error messages with fallback suggestions

#### 3. **Setlist.fm Integration** ✅
- **Status**: ✅ COMPLETE - Core functionality implemented and tested (Session 2025-11-21)
- **Documentation**: See [SETLIST_INTEGRATION.md](SETLIST_INTEGRATION.md) for complete plan
- **Implemented Features**:
  - ✅ `lfm concerts` CLI command - Search concerts by artist with filters (city, country, venue, tour, date, year)
  - ✅ `lfm setlist` CLI command - Get full tracklist for specific concert by ID
  - ✅ `lfm_concerts` MCP tool - Concert search for LLM integration
  - ✅ `lfm_setlist` MCP tool - Setlist retrieval for LLM integration
  - ✅ SetlistFmApiClient - Rate-limited API client (500ms throttle, 2 req/sec)
  - ✅ Comprehensive error handling - 404s treated as "no results", not errors
  - ✅ JSON output support - Clean, parseable responses for MCP integration
  - ✅ Configuration support - API key management via config
- **Implementation Details**:
  - API client with proper throttling and error handling
  - Rich CLI output with pagination support
  - MCP integration following existing tool patterns
  - Contextual metadata fields (`_note`, `_suggestion`) for LLM guidance
- **Testing Results**:
  - ✅ Concert search working correctly with all filters
  - ✅ Setlist retrieval returning full tracklists with venue details
  - ✅ Empty setlist handling with contextual notes
  - ✅ MCP tools properly integrated and tested
- **Future Enhancements**:
  - Automatic playlist creation from setlists
  - Concert attendance tracking
  - Tour archiving workflows

#### 4. **Additional Features** (Future Considerations)
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

### 🚀 Distribution & Releases

**Current Version**: 1.5.0 (CLI) / 0.2.0 (MCP Server)

**Release Infrastructure** (Session 2025-10-17):
- ✅ Automated GitHub Actions workflow (`.github/workflows/release.yml`)
- ✅ One-liner installation scripts (`install.ps1`, `install.sh`)
- ✅ Comprehensive documentation suite (INSTALL.md, QUICKSTART.md, MCP_SETUP.md, TROUBLESHOOTING.md)
- ✅ Landing page README routing users to appropriate docs

**Creating a New Release** - See [RELEASE.md](RELEASE.md) for detailed instructions:

1. **Update version numbers**:
   - `src/Lfm.Cli/Lfm.Cli.csproj` → `<Version>1.6.0</Version>`
   - `lfm-mcp-release/server.js` → `version: '0.3.0'`
   - `CHANGELOG.md` → Add new release section

2. **Commit and tag**:
   ```bash
   git add src/Lfm.Cli/Lfm.Cli.csproj lfm-mcp-release/server.js CHANGELOG.md
   git commit -m "chore: Bump version to 1.6.0"
   git push
   git tag -a v1.6.0 -m "Release v1.6.0"
   git push --tags
   ```

3. **GitHub Actions automatically**:
   - Builds all 4 platform binaries (Windows, macOS Intel/ARM, Linux)
   - Packages MCP server with **lfm-guidelines.md** (critical file)
   - Creates GitHub Release with all assets
   - Generates release notes

**Installation Methods**:
- **One-liner installers**: Detect platform, download latest, configure PATH
  - Windows: `iwr -useb https://raw.githubusercontent.com/Steven-Marshall/lfm/master/install.ps1 | iex`
  - macOS/Linux: `curl -fsSL https://raw.githubusercontent.com/Steven-Marshall/lfm/master/install.sh | bash`
- **Manual download**: From GitHub Releases page
- **Building from source**: See README.md

**Key Files**:
- `RELEASE.md` - Complete release process documentation
- `INSTALL.md` - Platform-specific installation guide
- `QUICKSTART.md` - 5-minute getting started guide
- `MCP_SETUP.md` - Claude integration setup
- `TROUBLESHOOTING.md` - Common issues reference

---

## 📚 Documentation References

**This file focuses on current work and recent sessions. For historical context and implementation details, see:**

- **[docs/SESSION_HISTORY.md](docs/SESSION_HISTORY.md)** - Archived sessions (2025-01-16 through 2025-10-06)
  - Cache Implementation (Session 2025-01-17)
  - Recommendations Feature (Session 2025-01-28)
  - Spotify Device Management (Session 2025-09-19)
  - Albums Bug Fix & API Throttling (Session 2025-06-28)
  - TopTracks Command (Session 2025-09-24)
  - Album Track Checking (Session 2025-10-06)

- **[docs/IMPLEMENTATION_NOTES.md](docs/IMPLEMENTATION_NOTES.md)** - Completed implementations
  - Refactoring Status (all tasks completed)
  - Cache Implementation (119x performance improvement)
  - Unicode Symbol Support (auto-detection across platforms)
  - API Throttling (200ms default, parallel calls support)
  - Spotify + Sonos Integration
  - MCP Server Integration

- **[docs/LESSONS_LEARNED.md](docs/LESSONS_LEARNED.md)** - Debugging patterns and best practices
  - Critical Thinking Over Quick Fixes (2010 Cache Bug)
  - Metadata Matching Complexity (apostrophe variants, featuring artists)
  - API Performance Analysis (HTTP bottleneck identification)
  - Spotify API Race Conditions (atomic batch operations)
  - LLM Blind Spots in MCP Context (track position hallucinations)
  - Understanding Depth Parameter (popularity ranking vs chronological)
  - Response Style for Music Conversations (DJ buddy vs data analyst)
