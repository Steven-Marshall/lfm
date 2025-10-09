# LFM MCP Server

Model Context Protocol (MCP) server for the Last.fm CLI tool. This enables natural language interaction with your Last.fm data through Claude Code.

## Important: Session Initialization

**⚠️ For the best experience, initialize at the start of every session!**

Start each new conversation with Claude Code by asking it to initialize:
- "Initialize my Last.fm session"
- "Start Last.fm"
- Any similar request that triggers `lfm_init`

This loads essential guidelines including:
- User music preferences (artists to avoid, listening style)
- Response style guidance (be a DJ buddy, not a data analyst)
- Technical metadata interpretation rules (prevents misreading album data)
- Tool selection and best practices

**Why initialize every session?**
- Ensures accurate interpretation of metadata discrepancies
- Sets the right conversational tone (music discovery, not data analysis)
- Loads user preferences for better recommendations
- Prevents common mistakes like misinterpreting track play counts

## Features

- **lfm_init**: Initialize session with guidelines and preferences (call this first!)
- **lfm_tracks**: Get top tracks from Last.fm
- **lfm_artists**: Get top artists from Last.fm
- **lfm_albums**: Get top albums from Last.fm
- **lfm_recommendations**: Get music recommendations based on listening history
- **lfm_check**: Check listening history for artists, tracks, or albums
- **lfm_similar**: Find artists similar to a specific artist
- **lfm_play_now**: Play tracks/albums on Spotify immediately
- **lfm_queue**: Add tracks/albums to Spotify queue

## Prerequisites

1. **LFM CLI installed**: The `lfm` command must be available in your system PATH
2. **Node.js**: Version 16 or higher
3. **Last.fm API key**: Must be configured in the LFM CLI

## Installation

1. Clone or download this directory
2. Install dependencies:
   ```bash
   npm install
   ```

## Configuration for Claude Code

Add this server to your Claude Code MCP configuration:

```json
{
  "mcpServers": {
    "lfm": {
      "command": "node",
      "args": ["path/to/lfm-mcp-release/server.js"],
      "cwd": "path/to/lfm-mcp-release"
    }
  }
}
```

## Usage

Once configured, start each session by asking Claude Code to initialize:

**First message in any session:**
- "Initialize my Last.fm session" (Claude will call `lfm_init`)

**Then you can ask natural language questions about your music:**
- "What are my top 5 tracks from 2023?"
- "Show me my top artists from last month"
- "Give me some music recommendations based on my overall listening"
- "What albums did I listen to most in the 6 month period?"
- "Check if I've listened to Pink Floyd's Dark Side of the Moon"
- "Play Radiohead's OK Computer on Spotify"

## Available Parameters

All tools support:
- `limit`: Number of results (1-1000)
- `period`: Time period (overall, 7day, 1month, 3month, 6month, 12month)
- `from`/`to`: Date range (YYYY-MM-DD or YYYY)
- `year`: Specific year shortcut

Recommendations tool additionally supports:
- `filter`: Minimum play count filter to exclude known artists

## Troubleshooting

1. **"lfm command not found"**: Ensure the LFM CLI is installed and in your PATH
2. **API errors**: Make sure your Last.fm API key is configured with `lfm config`
3. **Permission errors**: Ensure Node.js has permission to execute the lfm command