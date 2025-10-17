# Setting Up LFM with Claude

Use LFM with Claude Code or Claude Desktop to ask natural language questions about your music!

## What You'll Get

Once set up, you can ask Claude questions like:
- "What were my top artists in 2024?"
- "Show me my most-played albums from last month"
- "Give me music recommendations based on my listening history"
- "Check if I've listened to Pink Floyd's Dark Side of the Moon"
- "Play Radiohead's OK Computer on Spotify"

Claude will use the LFM MCP server to answer using your actual Last.fm data.

---

## Prerequisites

Before starting, make sure you have:

✅ **LFM CLI installed** → [Installation Guide](INSTALL.md)
✅ **LFM configured** with API key and username → [Quick Start](QUICKSTART.md)
✅ **Node.js 16 or later** installed
✅ **Claude Code or Claude Desktop** installed

### Check Node.js Installation

```bash
node --version
```

If you see a version number like `v18.x.x` or `v20.x.x`, you're good!

**Don't have Node.js?**
Download from: https://nodejs.org (choose the "LTS" version)

---

## Installation Methods

Choose one:
- **[Option A: Automated Setup](#option-a-automated-setup-recommended)** - Easiest (when available)
- **[Option B: Manual Setup](#option-b-manual-setup)** - Step-by-step guide

---

## Option A: Automated Setup (Recommended)

**Note**: This feature will be available in the next LFM release (v1.6.0)

Once available, simply run:

```bash
lfm setup-mcp
```

The wizard will:
1. Check if Node.js is installed
2. Ask if you use Claude Code or Claude Desktop
3. Install the MCP server files
4. Configure Claude automatically
5. Test the connection

Until then, use [Option B: Manual Setup](#option-b-manual-setup) below.

---

## Option B: Manual Setup

### Step 1: Download MCP Server

1. **Go to the latest release:**
   https://github.com/Steven-Marshall/lfm/releases/latest

2. **Download** `lfm-mcp-server-v0.2.0.zip` (or latest version)

3. **Extract the zip file** to a permanent location:
   - **Windows**: `C:\Users\YourName\lfm-mcp`
   - **macOS/Linux**: `~/lfm-mcp`

   **Important**: Don't put it in your Downloads folder - it needs to stay in this location!

### Step 2: Install Dependencies

Open terminal in the extracted folder and run:

```bash
cd ~/lfm-mcp  # or your extraction location

npm install
```

This installs the required Node.js packages.

### Step 3: Verify LFM is in PATH

The MCP server needs to be able to run the `lfm` command. Test this:

```bash
lfm --version
```

If this works, great! If not, see [Troubleshooting: LFM not in PATH](#lfm-not-in-path)

### Step 4: Configure Claude

The configuration steps differ depending on whether you use Claude Code or Claude Desktop.

---

#### For Claude Code Users

1. **Find your Claude Code config file:**

   **Windows**:
   ```
   %APPDATA%\claude-code\mcp.json
   ```
   Full path: `C:\Users\YourName\AppData\Roaming\claude-code\mcp.json`

   **macOS/Linux**:
   ```
   ~/.config/claude-code/mcp.json
   ```

2. **Edit the config file** (create it if it doesn't exist):

   **Windows**:
   ```json
   {
     "mcpServers": {
       "lfm": {
         "command": "node",
         "args": ["C:\\Users\\YourName\\lfm-mcp\\server.js"],
         "cwd": "C:\\Users\\YourName\\lfm-mcp"
       }
     }
   }
   ```

   **macOS/Linux**:
   ```json
   {
     "mcpServers": {
       "lfm": {
         "command": "node",
         "args": ["/Users/YourName/lfm-mcp/server.js"],
         "cwd": "/Users/YourName/lfm-mcp"
       }
     }
   }
   ```

   **Important**: Replace `YourName` with your actual username!

3. **Save the file**

4. **Restart Claude Code**

---

#### For Claude Desktop Users

1. **Find your Claude Desktop config file:**

   **Windows**:
   ```
   %APPDATA%\Claude\claude_desktop_config.json
   ```

   **macOS**:
   ```
   ~/Library/Application Support/Claude/claude_desktop_config.json
   ```

   **Linux**:
   ```
   ~/.config/Claude/claude_desktop_config.json
   ```

2. **Edit the config file**:

   **Windows**:
   ```json
   {
     "mcpServers": {
       "lfm": {
         "command": "node",
         "args": ["C:\\Users\\YourName\\lfm-mcp\\server.js"],
         "cwd": "C:\\Users\\YourName\\lfm-mcp"
       }
     }
   }
   ```

   **macOS/Linux**:
   ```json
   {
     "mcpServers": {
       "lfm": {
         "command": "node",
         "args": ["/Users/YourName/lfm-mcp/server.js"],
         "cwd": "/Users/YourName/lfm-mcp"
       }
     }
   }
   ```

   **Important**: Replace `YourName` with your actual username!

   **If you already have other MCP servers**, add the `lfm` entry inside the existing `mcpServers` object:

   ```json
   {
     "mcpServers": {
       "existing-server": {
         ...
       },
       "lfm": {
         "command": "node",
         "args": ["/Users/YourName/lfm-mcp/server.js"],
         "cwd": "/Users/YourName/lfm-mcp"
       }
     }
   }
   ```

3. **Save the file**

4. **Restart Claude Desktop**

---

### Step 5: Test the Connection

1. **Open Claude** (Code or Desktop)

2. **Start a new conversation**

3. **Type this message**:
   ```
   Initialize my Last.fm session
   ```

4. **Claude should respond** with something like:
   ```
   ✅ Last.fm MCP service initialized successfully!

   I've loaded the guidelines and I'm ready to help you with:
   - Music discovery
   - Listening stats
   - Playlist creation
   ...
   ```

5. **Try asking a question**:
   ```
   What were my top 5 artists last month?
   ```

   Claude should respond with your actual Last.fm data!

---

## Troubleshooting

### Config File Not Found

If the config file doesn't exist:

1. Create the directory structure:
   ```bash
   # Windows (PowerShell)
   mkdir -Force "$env:APPDATA\claude-code"
   # or
   mkdir -Force "$env:APPDATA\Claude"

   # macOS/Linux
   mkdir -p ~/.config/claude-code
   # or
   mkdir -p ~/Library/Application\ Support/Claude
   ```

2. Create the file with your text editor

### LFM Not in PATH

If the MCP server can't find `lfm`, you have two options:

**Option 1: Use full path to lfm** (easiest)

In your MCP config, add the full path to the lfm executable:

**Windows**:
```json
{
  "mcpServers": {
    "lfm": {
      "command": "node",
      "args": ["C:\\Users\\YourName\\lfm-mcp\\server.js"],
      "cwd": "C:\\Users\\YourName\\lfm-mcp",
      "env": {
        "PATH": "C:\\Users\\YourName\\lfm;${PATH}"
      }
    }
  }
}
```

**macOS/Linux**:
```json
{
  "mcpServers": {
    "lfm": {
      "command": "node",
      "args": ["/Users/YourName/lfm-mcp/server.js"],
      "cwd": "/Users/YourName/lfm-mcp",
      "env": {
        "PATH": "/Users/YourName/.local/bin:${PATH}"
      }
    }
  }
}
```

**Option 2: Add lfm to your system PATH**
See [Installation Guide - Manual Install](INSTALL.md#manual-install)

### "npm install" Errors

If `npm install` fails:

1. Make sure you're in the right directory:
   ```bash
   cd ~/lfm-mcp
   ls package.json  # Should show the file
   ```

2. Try deleting `node_modules` and `package-lock.json`, then retry:
   ```bash
   rm -rf node_modules package-lock.json
   npm install
   ```

3. Make sure Node.js is up to date:
   ```bash
   node --version  # Should be v16 or later
   ```

### Claude Doesn't Respond to Last.fm Queries

1. **Check the MCP server is configured:**
   Look for a section in your config file with `"lfm": {...}`

2. **Check paths are correct:**
   - No typos in file paths
   - Paths use forward slashes on macOS/Linux
   - Paths use double backslashes on Windows (`\\` not `\`)
   - Your actual username (not `YourName`)

3. **Restart Claude** completely (quit and reopen)

4. **Check MCP logs** (if available):
   - Claude Code: Look in the developer console
   - Claude Desktop: Check for error messages

5. **Test the server manually:**
   ```bash
   cd ~/lfm-mcp
   node server.js
   ```
   If this shows errors, fix those first

### Still Having Issues?

See the [Troubleshooting Guide](TROUBLESHOOTING.md) for more help, or [open an issue](https://github.com/Steven-Marshall/lfm/issues).

---

## What's Included in the MCP Server

The MCP server package includes:

- **server.js** - The MCP server itself
- **lfm-guidelines.md** - Guidelines for Claude on how to use your music data
- **package.json** - Node.js dependencies
- **README.md** - MCP-specific documentation

**Important**: Don't delete any of these files! They're all needed for the server to work properly.

---

## Using LFM with Claude

### Best Practices

1. **Start each session with initialization:**
   ```
   Initialize my Last.fm session
   ```

2. **Be specific with time periods:**
   - ❌ "Show me my top artists"
   - ✅ "Show me my top artists from 2024"

3. **Ask natural questions:**
   ```
   "What were my most-played albums last month?"
   "Give me recommendations based on my recent listening"
   "Check if I've listened to any Pink Floyd albums"
   ```

### Example Queries

Music stats:
- "What are my top 10 artists of all time?"
- "Show me my most-played tracks from last week"
- "What albums did I listen to most in January 2024?"

Discovery:
- "Find artists similar to Taylor Swift"
- "Give me music recommendations I haven't heard"
- "What Radiohead albums have I listened to?"

Playback (if Spotify configured):
- "Play Pink Floyd's Dark Side of the Moon"
- "Queue some music similar to what I've been listening to"

---

## Uninstalling

To remove the MCP integration:

1. **Remove from Claude config:**
   - Delete the `"lfm"` entry from your `mcp.json` or `claude_desktop_config.json`

2. **Delete the MCP server folder:**
   ```bash
   # Windows
   Remove-Item -Recurse C:\Users\YourName\lfm-mcp

   # macOS/Linux
   rm -rf ~/lfm-mcp
   ```

3. **Restart Claude**

The LFM CLI tool will still be installed and usable from the command line.

---

## Next Steps

- **Explore Commands**: [Quick Start Guide](QUICKSTART.md)
- **Get Help**: [Troubleshooting Guide](TROUBLESHOOTING.md)
- **Full Documentation**: [README.md](README.md)

**Happy music exploring! 🎵**
