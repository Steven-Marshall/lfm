# New Releases Feature - Research Notes

## Current Status: DISABLED

The new-releases feature has been disabled due to Spotify API issues.

## Problem with Spotify API

**Endpoint:** `GET https://api.spotify.com/v1/browse/new-releases`

**Issue:** Returns albums that are 5-7 months old instead of actual new releases
- Broken since at least May 2024
- No official response from Spotify
- Community thread: https://community.spotify.com/t5/Spotify-for-Developers/Web-API-Get-New-Releases-API-Returning-Old-Items/m-p/6069709

**Example output (October 2025):**
- Returning albums from March-May 2025
- No way to filter by actual release date
- Appears to be abandoned/deprecated by Spotify

## Code Status

**Preserved for future implementation:**
- ✅ `NewReleasesCommand.cs` - Command implementation
- ✅ `NewReleasesCommandBuilder.cs` - CLI builder
- ✅ `NewReleasesModels.cs` - Data models
- ✅ `SpotifyStreamer.GetNewReleasesAsync()` - Spotify integration
- ✅ `LastFmService.GetNewReleasesAsync()` - Service layer
- ✅ MCP server.js tool definition and handler

**Disabled in:**
- ❌ Program.cs - Command registration commented out
- ❌ Program.cs - DI registration commented out
- ❌ server.js - Tool definition commented out
- ❌ server.js - Tool handler commented out
- ❌ README.md - Documentation removed

**Comments added:** All files have comments explaining the Spotify API is broken and linking to the community thread.

## Research Needed: Alternative Data Sources

### Option 1: Album of the Year (AOTY)
- Website: https://www.albumoftheyear.org/
- Has comprehensive release data
- **Research needed:** Does AOTY have a public API?
- **Research needed:** Any scraping allowed in ToS?

### Option 2: Discogs
- Website: https://www.discogs.com/
- Known for comprehensive music database
- **Research needed:** Does Discogs API support new releases queries?
- API docs: https://www.discogs.com/developers

### Option 3: MusicBrainz
- Website: https://musicbrainz.org/
- Open music database
- Has API: https://musicbrainz.org/doc/MusicBrainz_API
- **Research needed:** Does it track release dates well?

### Option 4: Last.fm API
- We already use Last.fm for user listening data
- **Research needed:** Does Last.fm have new releases data?
- API docs: https://www.last.fm/api

### Option 5: Other Options
- Bandcamp new releases
- Rate Your Music
- Apple Music API
- Other music databases

## Implementation Notes

When a good data source is found:

1. **Keep the same interface:**
   - `GetNewReleasesAsync(int limit)` in service layer
   - Same models: `NewReleasesResult`, `NewAlbumRelease`
   - Same CLI command structure

2. **Update data source:**
   - Replace `SpotifyStreamer.GetNewReleasesAsync()` implementation
   - Or create new service for the data source
   - Map response to existing models

3. **Re-enable:**
   - Uncomment in Program.cs (CLI + DI)
   - Uncomment in server.js (MCP tool + handler)
   - Add back to README.md

4. **Add filtering if needed:**
   - Consider `--days` parameter to filter by recency
   - Consider genre/style filtering
   - Consider region filtering

## Date: October 10, 2025

Ready for research into alternative data sources.
