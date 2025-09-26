# LFM MCP Server

Model Context Protocol (MCP) server for the Last.fm CLI tool. This enables natural language interaction with your Last.fm data through Claude Code.

## Features

- **lfm_tracks**: Get top tracks from Last.fm
- **lfm_artists**: Get top artists from Last.fm
- **lfm_albums**: Get top albums from Last.fm
- **lfm_recommendations**: Get music recommendations based on listening history

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

Once configured, you can ask Claude Code natural language questions about your music:

- "What are my top 5 tracks from 2023?"
- "Show me my top artists from last month"
- "Give me some music recommendations based on my overall listening"
- "What albums did I listen to most in the 6 month period?"

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