# LFM MCP Service Implementation Plan

## Overview
Create a completely separate TypeScript/Node.js MCP server that wraps the existing lfm CLI tool. This service will enable natural language interaction with Last.fm data through Claude, without modifying any existing lfm code.

## Core Principles
- **Zero Changes to Existing LFM**: No modifications to the C# codebase
- **Complete Separation**: Independent project with separate repository
- **CLI Wrapping**: Execute lfm.exe as external process and parse output
- **Structured Interface**: Convert CLI text output to JSON for MCP consumption
- **Tool Specialization**: Each component does what it does best
  - **Claude**: Natural language understanding and conversation flow
  - **MCP Server**: Clean parameter validation and CLI integration
  - **LFM**: Music data retrieval and Spotify operations

## Project Structure
```
lfm-mcp/                    # Completely separate repository
├── package.json
├── tsconfig.json
├── src/
│   ├── server.ts           # Main MCP server entry point
│   ├── lfm-wrapper.ts      # CLI execution and output parsing
│   ├── tools/
│   │   ├── tracks.ts       # lfm tracks command wrapper
│   │   ├── artists.ts      # lfm artists command wrapper
│   │   ├── albums.ts       # lfm albums command wrapper
│   │   ├── recommendations.ts # lfm recommendations wrapper
│   │   ├── spotify.ts      # Spotify commands wrapper
│   │   └── config.ts       # Configuration commands wrapper
│   ├── parsers/
│   │   ├── tracks-parser.ts    # Parse tracks command output
│   │   ├── artists-parser.ts   # Parse artists command output
│   │   └── spotify-parser.ts   # Parse Spotify command output
│   ├── types.ts            # TypeScript interfaces
│   └── utils/
│       ├── validation.ts   # Parameter validation and sanitization
│       └── mapping.ts      # Map MCP parameters to CLI arguments
├── docs/
│   ├── README.md
│   ├── SETUP.md            # Installation and configuration
│   └── API.md              # MCP tools documentation
└── dist/                   # Built files
```

## Implementation Strategy

### Phase 1: Core Infrastructure (Week 1)
**Goal:** Basic MCP server that can execute lfm commands

**Tasks:**
1. Initialize Node.js/TypeScript project with MCP SDK
2. Implement CLI wrapper (`lfm-wrapper.ts`)
   - Execute lfm.exe with parameters
   - Capture stdout/stderr
   - Handle exit codes and errors
3. Create output parsers for basic commands
   - Parse tabular output into structured objects
   - Handle different output formats
4. Implement basic MCP tools:
   - `lfm_tracks()`
   - `lfm_artists()`
   - `lfm_albums()`

**CLI Integration:**
```typescript
// Example: lfm-wrapper.ts
export async function executeCommand(command: string, args: string[]): Promise<LfmResult> {
  const process = spawn('./lfm.exe', [command, ...args]);
  const stdout = await captureOutput(process);
  return parseOutput(command, stdout);
}
```

### Phase 2: Full Command Coverage (Week 2)
**Goal:** Wrap all existing lfm commands

**Additional MCP Tools:**
- `lfm_artist_tracks(artist, options)`
- `lfm_artist_albums(artist, options)`
- `lfm_recommendations(options)`
- `lfm_spotify_devices()`
- `lfm_spotify_list_playlists()`
- `lfm_spotify_delete_playlists(patterns)`
- `lfm_config_show()`
- `lfm_config_set(key, value)`

**Spotify Integration:**
- Wrap commands with `--playnow` and `--playlist` flags
- Parse Spotify operation results
- Handle device selection parameters

### Phase 3: Polish & Optimization (Week 3)
**Goal:** Robust, production-ready MCP server

**Features:**
- Enhanced error handling and recovery
- Performance optimization for CLI calls
- Comprehensive logging and debugging
- Configuration management improvements
- Documentation and examples

**No Natural Language Processing:**
The MCP server provides **simple, clean tools** - Claude handles all natural language interpretation. Each tool does what it does best:

- **Claude (LLM)**: Interprets "pandemic music" → calls `lfm_tracks({from: "2021-01-01", to: "2021-12-31"})`
- **MCP Server**: Validates parameters, executes CLI, returns structured data
- **LFM CLI**: Retrieves Last.fm data, manages Spotify integration

## MCP Tool Mapping

### Core Commands
```typescript
// Direct CLI mappings
lfm_tracks(period?, from?, to?, limit?, artist?, playnow?, playlist?, device?)
  → lfm.exe tracks [--period X] [--from X] [--to X] [--limit X] [--playnow] [--playlist X] [--device X]

lfm_artists(period?, from?, to?, limit?)
  → lfm.exe artists [options]

lfm_albums(period?, from?, to?, limit?)
  → lfm.exe albums [options]

lfm_recommendations(period?, from?, to?, limit?, filter?, artistLimit?, tracksPerArtist?, playnow?, playlist?)
  → lfm.exe recommendations [options]

lfm_artist_tracks(artist, period?, from?, to?, limit?, playnow?, playlist?)
  → lfm.exe artist-tracks "artist" [options]

lfm_spotify_devices()
  → lfm.exe spotify devices

lfm_config_show()
  → lfm.exe config
```

### Response Format
All tools return structured JSON instead of CLI text:
```typescript
interface TracksResponse {
  success: boolean;
  tracks: Array<{
    rank: number;
    name: string;
    artist: string;
    plays: number;
  }>;
  error?: string;
  executionTime?: number;
}
```

## Deployment & Distribution

### Standalone Distribution
- Build single executable with `pkg` or similar
- No runtime dependencies required
- Cross-platform support (Windows/Linux/macOS)

### Configuration
```json
// lfm-mcp-config.json
{
  "lfmExecutablePath": "./lfm.exe",  // Path to lfm CLI
  "defaultTimeout": 30000,           // Command timeout
  "enableCache": true,               // Use lfm's caching
  "logLevel": "info"
}
```

### Claude Desktop Integration
```json
// Add to Claude Desktop settings
{
  "mcpServers": {
    "lfm": {
      "command": "lfm-mcp",
      "args": ["--config", "/path/to/lfm-mcp-config.json"]
    }
  }
}
```

## Example Usage Flow

### User Request
"Create a playlist from my 2021 listening, but limit to 2 tracks per artist"

### MCP Tool Calls
1. `lfm_tracks({from: "2021-01-01", to: "2021-12-31", limit: 50})`
2. *Claude processes results and filters to 2 per artist*
3. `lfm_tracks({tracks: filteredTracks, playlist: "2021 Favorites"})`

### CLI Commands Executed
1. `lfm.exe tracks --from 2021-01-01 --to 2021-12-31 --limit 50`
2. `lfm.exe tracks --playlist "2021 Favorites" [track selection logic]`

## Benefits of This Approach

### For LFM Project
- ✅ **Zero risk** - No changes to existing codebase
- ✅ **Independent development** - MCP server can evolve separately
- ✅ **Optional feature** - Users can choose to install or not
- ✅ **Stable interface** - Uses established CLI commands

### For Users
- ✅ **Natural language** - Claude interprets intent and calls appropriate tools
- ✅ **Conversational** - Iterative playlist building and music discovery
- ✅ **Intelligent analysis** - Claude can analyze patterns and make connections
- ✅ **Enhanced workflows** - Chain multiple operations with context awareness

### For Development
- ✅ **Technology choice** - Use best tools for MCP (TypeScript/Node.js)
- ✅ **Rapid iteration** - Can experiment without affecting CLI
- ✅ **Clear separation** - Well-defined boundaries between projects
- ✅ **Maintainability** - Smaller, focused codebase

## Success Criteria
1. All existing lfm CLI commands accessible via MCP tools
2. Structured JSON responses for all operations
3. Natural language date/parameter interpretation working
4. Spotify integration (playlists, queuing) functional
5. Zero modifications to existing lfm C# codebase
6. Easy installation and setup for end users

## Technical Considerations

### CLI Output Parsing Strategy
The MCP server will need to parse various output formats from lfm:

**Tabular Data (tracks, artists, albums):**
```
Rank Track                    Artist              Plays
---------------------------------------------------------------
1    Song Name                Artist Name         42
2    Another Song             Another Artist      38
```

**Spotify Operations:**
```
🎵 Queueing 5 tracks to Spotify...
✅ Queued: Artist - Track Name
✅ Queued 5/5 tracks
```

**Device Lists:**
```
📱 Found 2 Spotify devices:
  🎵 iPhone (Smartphone) ✅ (active) | Volume: 100%
  🎵 Web Player (Computer) | Volume: 100%
```

### Error Handling
- Parse stderr for error messages
- Handle exit codes appropriately
- Provide meaningful error responses in MCP format
- Fallback for unexpected output formats

### Performance Optimization
- Cache command results when appropriate
- Use lfm's built-in caching system
- Minimize process spawning overhead
- Consider connection pooling for frequent operations

## The Power of Specialization

This architecture leverages each tool's strengths:

**Claude's Strengths:**
- Understanding natural language intent
- Making connections between data points
- Conversational context and memory
- Complex reasoning about music preferences

**LFM's Strengths:**
- Efficient Last.fm API integration with caching
- Robust Spotify integration with device management
- Proven CLI interface and error handling
- Music domain expertise and data structures

**MCP Server's Role:**
- Simple, reliable bridge between Claude and LFM
- Parameter validation and sanitization
- Structured data formatting for optimal AI consumption
- Abstraction layer that keeps both sides clean

**Example User Experience:**
```
User: "I want to discover music similar to what I was listening to during early 2022,
       but avoid artists I already know well"

Claude: *Understands this as: get 2022 data, find recommendations, filter known artists*
        *Calls lfm_tracks({from: "2022-01-01", to: "2022-06-30"}) to analyze that period*
        *Calls lfm_recommendations({period: "overall", filter: 10}) to find new artists*
        *Analyzes the results and suggests a curated mix*
        *Calls lfm_queue_spotify() to start playing*

Result: Intelligent music discovery that combines your listening history with new recommendations
```

This creates a truly symbiotic relationship where each component amplifies the others' capabilities.