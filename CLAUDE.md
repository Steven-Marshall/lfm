# Last.fm CLI - Claude Session Notes

## Project Overview
Last.fm CLI tool (.NET) for music statistics. Production-ready with Spotify/Sonos playback, 33-tool MCP server, and file-based caching (119x performance improvement).

## Architecture
- **Lfm.Cli**: CLI commands using System.CommandLine
- **Lfm.Core**: Services, models, configuration, caching
- **Lfm.Spotify**: Spotify playback integration
- **Lfm.Sonos**: Sonos via node-sonos-http-api
- **MCP Server**: `lfm-mcp-release/server-core.js` (32 tools)

## Key Files
- `src/Lfm.Cli/Program.cs` - Entry point and DI
- `src/Lfm.Core/Services/LastFmApiClient.cs` - Last.fm API client
- `src/Lfm.Core/Services/SetlistFmApiClient.cs` - Setlist.fm API client
- `src/Lfm.Core/Services/CachedLastFmApiClient.cs` - Cache decorator
- `src/Lfm.Spotify/SpotifyStreamer.cs` - Spotify integration
- `lfm-mcp-release/lfm-guidelines.md` - LLM usage guidelines

## Build Commands
```bash
# Development
dotnet build -c Release

# Publish — win-arm64 is the primary delivery target (laptop is ARM as of 2026-06)
dotnet publish src/Lfm.Cli -c Release -r win-arm64 -o publish/win-arm64 --self-contained false

# Other targets (kept for office laptop / Spark Linux deployment)
dotnet publish src/Lfm.Cli -c Release -r win-x64 -o publish/win-x64 --self-contained false
dotnet publish src/Lfm.Cli -c Release -r linux-x64 -o publish/linux-x64 --self-contained false
```

**Executables**: `./publish/win-arm64/lfm.exe` (primary), `./publish/win-x64/lfm.exe`, or `./publish/linux-x64/lfm`

**Installing locally**: the MCP server spawns `lfm` from PATH, so the published binary needs to be on PATH (or the `lfm` shim updated to point at it) before a reconnect picks up CLI changes.

---

## Recent Sessions

### Session: 2026-06-18 (Album Tracks Tool + Spotify Reauth Handling)
- **Status**: ✅ COMPLETE — deployed to Spark (arm64 container rebuilt + healthy)
- **CLI v1.11.0 / MCP v0.6.0**
- **New tool**: `lfm_album_tracks` — canonical Spotify tracklist (track #, disc #, durations, per-track artists) to plug LLM blind spot on track positions
  - CLI: `lfm album-tracks <artist> <album> [--exact-match] [--json]`
  - Spotify endpoint: `/v1/albums/{id}/tracks` with pagination via `next`
  - Same disambiguation contract as `lfm_play_now` (multipleVersionsDetected + exactMatch retry)
- **Spotify reauth handling** (ahead of 2026-07-20 6-month refresh-token expiry):
  - New `SpotifyReauthRequiredException` thrown specifically on `invalid_grant`
  - `EnsureValidAccessTokenAsync` distinguishes interactive CLI from headless MCP via `Console.IsInputRedirected` — interactive falls through to OAuth flow, headless throws cleanly (no more deadlock on `Console.ReadLine`)
  - Dead refresh tokens cleared from saved config on confirmed `invalid_grant`
  - PlayCommand emits structured `{ errorCode: "spotify_reauth_required", action: "..." }` JSON
  - `executeLfmCommand` in server-core.js now passes structured-JSON stdout through to MCP on non-zero exit (also fixes the existing `multipleVersionsDetected` round-trip that was being lost)
- **Album disambiguation case-insensitivity** — flipped all 5 sites from `StringComparison.Ordinal` to `OrdinalIgnoreCase` (matches the existing playlist-disambiguation convention; "Out of Season" now resolves to Spotify's "Out Of Season")
- **Build target** — win-arm64 is now the primary local target (laptop is ARM as of 2026-06)
- **Guidelines updated** — `lfm-guidelines.md` track-positions section routes canonical-position questions to `lfm_album_tracks`, keeps `lfm_check verbose` for scrobble-coverage

### Session: 2025-12-28 (MCP Exit Error Investigation)
- **Status**: ⚠️ KNOWN UPSTREAM ISSUE
- **Problem**: "1 MCP server failed" error on `/exit`
- **Root Cause**: Node.js core bug in `node::ResetStdio()` during shutdown
- **Impact**: Cosmetic only - MCP works correctly during session
- **Reference**: GitHub issue #7718, `docs/MCP_EXIT_ERROR.md`

### Session: 2025-11-21 (Setlist.fm MCP Integration)
- **Status**: ✅ COMPLETE
- **Summary**: Added `lfm_concerts` and `lfm_setlist` MCP tools
- **CLI Commands**: `lfm concerts "Artist"`, `lfm setlist "id"`
- **Error Handling**: 404s treated as "no results" (Debug level, not Error)
- **Files**: ConfigCommand.cs, SetlistCommand.cs, ConcertsCommand.cs, server-core.js

### Session: 2025-11-12 (Logging Fix & Date Range Query Debugging)
- **Status**: 🟡 IN PROGRESS
- **Problem**: Year queries returning empty via MCP
- **Root Cause**: .NET console logger polluting stdout with JSON
- **Fix**: `LogToStandardErrorThreshold = LogLevel.Trace` in Program.cs
- **Open Issue**: `--year 2025` intermittent failures, `--from/--to` works reliably
- **Next Steps**: Investigate `--year` vs `--from/--to` parameter processing

---

## Project Status

### ✅ Complete
- All core commands (artists, tracks, albums, recommendations, toptracks, mixtape)
- Date range support, Unicode symbols, API throttling (200ms default)
- Spotify + Sonos playback with unified play command
- MCP server (32 tools), Setlist.fm integration
- File-based caching (119x improvement)
- Config export/import, Docker deployment

### ✅ Spotify API Migration (Complete as of 2026-02-28)
Spotify breaking changes: https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security

All four changes complete and tested:
1. **Playlist creation endpoint**: `POST /v1/users/{userId}/playlists` → `POST /v1/me/playlists` — DONE (2026-02-13)
2. **Add tracks endpoint**: `POST /v1/playlists/{id}/tracks` → `POST /v1/playlists/{id}/items` — DONE (2026-02-13)
3. **Unfollow playlist endpoint**: `DELETE /v1/playlists/{id}/followers` → `DELETE /v1/me/library?uris=spotify:playlist:{id}` — DONE (2026-02-28). Note: `uris` must be a query parameter, not request body.
4. **Playlist field rename**: `[JsonPropertyName("tracks")]` → `[JsonPropertyName("items")]` on `SpotifyPlaylistItem` in `SpotifyModels.cs:153` — DONE (2026-02-28). API now returns both fields during transition.

- OAuth scope `user-library-modify` added
- Spark deployment needs re-auth with new refresh token

### 📋 TODO
- **Local build on office laptop** — `git pull` + `dotnet publish` for win-x64 to pick up v1.11.0 (Spotify migration + album-tracks tool)

### 📋 Future Enhancements
- **Progress Bars**: Long-running operation feedback (see `progressbarproject.md`)
- **Enhanced Filtering**: Recommendation filters
- **Extended Analytics**: Advanced statistics

---

## Documentation

- **[docs/SESSION_HISTORY.md](docs/SESSION_HISTORY.md)** - Archived sessions (2025-01-16 through 2025-11-10)
- **[docs/IMPLEMENTATION_NOTES.md](docs/IMPLEMENTATION_NOTES.md)** - Technical reference
- **[docs/LESSONS_LEARNED.md](docs/LESSONS_LEARNED.md)** - Debugging patterns
- **[docs/MCP_EXIT_ERROR.md](docs/MCP_EXIT_ERROR.md)** - Known MCP shutdown issue (upstream bug)
- **[RELEASE.md](RELEASE.md)** - Release process (v1.5.0 CLI / v0.2.0 MCP)
