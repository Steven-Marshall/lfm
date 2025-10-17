# Troubleshooting Guide

Common issues and solutions for LFM.

## Table of Contents

- [Installation Issues](#installation-issues)
- [Configuration Issues](#configuration-issues)
- [Usage Issues](#usage-issues)
- [MCP Integration Issues](#mcp-integration-issues)
- [Performance Issues](#performance-issues)
- [Spotify/Sonos Issues](#spotifysonos-issues)
- [Getting More Help](#getting-more-help)

---

## Installation Issues

### "Command not found: lfm" (macOS/Linux)

**Problem**: Terminal can't find the `lfm` command after installation.

**Solution**:

1. Check if `~/.local/bin` is in your PATH:
   ```bash
   echo $PATH | grep ".local/bin"
   ```

2. If it's not there, add it:
   ```bash
   # For bash
   echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
   source ~/.bashrc

   # For zsh (macOS default)
   echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.zshrc
   source ~/.zshrc
   ```

3. Verify the binary exists:
   ```bash
   ls -l ~/.local/bin/lfm
   ```

4. Make sure it's executable:
   ```bash
   chmod +x ~/.local/bin/lfm
   ```

### "Command not found: lfm" (Windows)

**Problem**: PowerShell can't find the `lfm` command.

**Solution**:

1. Verify the executable exists:
   ```powershell
   ls C:\Users\$env:USERNAME\lfm\lfm.exe
   ```

2. Check if the folder is in your PATH:
   - Press `Windows + R`
   - Type `sysdm.cpl` and press Enter
   - Click "Environment Variables"
   - Look for `C:\Users\YourUsername\lfm` in the Path variable

3. If it's not there, add it:
   - Under "User variables", select "Path"
   - Click "Edit" → "New"
   - Add: `C:\Users\YourUsername\lfm`
   - Click OK on all windows

4. **Important**: Restart PowerShell/Terminal after changing PATH

### ".NET Runtime Required" Error

**Problem**: Error message about missing .NET runtime when running `lfm`.

**Cause**: You downloaded the framework-dependent version instead of the self-contained version.

**Solution**:

**Option 1** (Recommended): Download the self-contained version
- Go to [Latest Release](https://github.com/Steven-Marshall/lfm/releases/latest)
- Download the file with your platform in the name (e.g., `lfm-windows-x64.exe`)
- These versions include .NET and don't require separate installation

**Option 2**: Install .NET 8 Runtime
- Download from: https://dotnet.microsoft.com/download/dotnet/8.0
- Choose "Run desktop apps" → Your platform
- Install and try `lfm` again

### Permission Denied (macOS/Linux)

**Problem**: "Permission denied" when trying to run `lfm`.

**Solution**:

```bash
chmod +x ~/.local/bin/lfm
```

---

## Configuration Issues

### "API Key Invalid" Error

**Problem**: Error when trying to use LFM commands.

**Solution**:

1. Verify your API key is set:
   ```bash
   lfm config
   ```
   Look for `ApiKey` in the output.

2. Double-check your API key:
   - Visit https://www.last.fm/api/account
   - Copy your API key (not API Secret)
   - Set it again:
     ```bash
     lfm config set api-key YOUR_API_KEY_HERE
     ```

3. Make sure there are no extra spaces or quotes:
   ```bash
   # Wrong:
   lfm config set api-key "abc123"  # Don't include quotes

   # Right:
   lfm config set api-key abc123
   ```

### "User Not Found" Error

**Problem**: Error about username not being found.

**Solution**:

1. Check your configured username:
   ```bash
   lfm config
   ```

2. Verify it matches your Last.fm username exactly:
   - Visit https://www.last.fm
   - Log in
   - Your username is in the top right

3. Update if needed:
   ```bash
   lfm config set username YOUR_LASTFM_USERNAME
   ```

### Config File Location

If you need to manually edit or check the config file:

**Windows**: `%APPDATA%\lfm\config.json`
- Full path: `C:\Users\YourName\AppData\Roaming\lfm\config.json`

**macOS/Linux**: `~/.config/lfm/config.json`

---

## Usage Issues

### "Artist Not Found" Despite Correct Name

**Problem**: LFM says artist not found even though you spelled it correctly.

**Solution**:

LFM uses Last.fm's autocorrect feature, which usually works well. Try:

1. **Check exact spelling on Last.fm website**
   - Search for the artist on https://www.last.fm
   - Use the exact name from the URL

2. **Try variations**:
   ```bash
   # All these should work for The Beatles:
   lfm check "The Beatles"
   lfm check "Beatles"
   lfm check "beatles"
   ```

3. **For tracks with punctuation**, try both with and without:
   ```bash
   lfm check "Artist" "Song (feat. Other Artist)"
   lfm check "Artist" "Song feat Other Artist"
   ```

### Slow Query Performance

**Problem**: Queries taking a long time to complete.

**Cause**: First query for new data needs to call the Last.fm API.

**Solutions**:

1. **Wait for cache to populate**: First query is slow, subsequent queries are fast

2. **Check cache status**:
   ```bash
   lfm cache-status
   ```

3. **Clear corrupted cache** (if queries never get faster):
   ```bash
   lfm cache-clear --all
   ```

4. **Use smaller limits** for testing:
   ```bash
   lfm artists --limit 5  # Instead of --limit 100
   ```

### Unicode Characters Show as "?"

**Problem**: Special characters, symbols, or hearts display as `?`.

**Solution**:

1. **Enable Unicode support**:
   ```bash
   lfm config set unicode enabled
   ```

2. **Use auto-detection** (recommended):
   ```bash
   lfm config set unicode auto
   ```

3. **Disable Unicode** if problems persist:
   ```bash
   lfm config set unicode disabled
   ```

4. **On Windows**: Make sure you're using Windows Terminal or PowerShell 7+ (not old PowerShell or CMD)

---

## MCP Integration Issues

### "lfm command not found" (from MCP)

**Problem**: MCP server can't find the `lfm` command, even though it works in terminal.

**Cause**: The PATH environment variable in MCP context is different from your terminal.

**Solution**:

Add the PATH to your MCP config file:

**Windows** (`claude_desktop_config.json` or `mcp.json`):
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

### Claude Doesn't Respond to Last.fm Queries

**Problem**: Claude ignores Last.fm questions or says it can't help.

**Checklist**:

1. ✅ **Restart Claude** after adding MCP config

2. ✅ **Verify config file exists** at correct location:
   - Claude Code: `%APPDATA%\claude-code\mcp.json` (Windows) or `~/.config/claude-code/mcp.json` (Mac/Linux)
   - Claude Desktop: `%APPDATA%\Claude\claude_desktop_config.json` (Windows) or `~/Library/Application Support/Claude/claude_desktop_config.json` (Mac)

3. ✅ **Check JSON syntax**:
   - No trailing commas
   - All quotes are straight quotes (`"` not `"` or `"`)
   - Properly escaped backslashes on Windows (`\\` not `\`)

4. ✅ **Test MCP server manually**:
   ```bash
   cd ~/lfm-mcp
   node server.js
   ```
   Press Ctrl+C to stop. If you see errors, fix those first.

5. ✅ **Initialize the session**:
   Ask Claude: "Initialize my Last.fm session"

### Node.js Not Installed

**Problem**: Error about Node.js when setting up MCP.

**Solution**:

1. Check if Node.js is installed:
   ```bash
   node --version
   ```

2. If not installed, download from:
   https://nodejs.org
   - Choose the "LTS" (Long Term Support) version
   - Install for your platform

3. Verify installation:
   ```bash
   node --version  # Should show v18.x.x or v20.x.x
   npm --version   # Should show 9.x.x or 10.x.x
   ```

### npm install Failures

**Problem**: `npm install` fails with errors.

**Solutions**:

1. **Make sure you're in the right directory**:
   ```bash
   cd ~/lfm-mcp
   ls package.json  # Should show the file
   ```

2. **Clear npm cache and retry**:
   ```bash
   npm cache clean --force
   rm -rf node_modules package-lock.json
   npm install
   ```

3. **Check Node.js version**:
   ```bash
   node --version  # Must be v16 or later
   ```

4. **Check internet connection**: npm needs to download packages

---

## Performance Issues

### High Memory Usage

**Problem**: LFM using a lot of memory.

**Cause**: Large cache or deep search queries.

**Solutions**:

1. **Clear old cache**:
   ```bash
   lfm cache-clear --expired
   ```

2. **Limit deep searches**:
   ```bash
   # Instead of:
   lfm artist-tracks "Artist" --deep

   # Try:
   lfm artist-tracks "Artist" --depth 2000 --limit 20
   ```

### Cache Taking Up Space

**Problem**: Cache folder is large.

**Check cache size**:
```bash
lfm cache-status
```

**Clear cache**:
```bash
# Clear expired entries only
lfm cache-clear --expired

# Clear everything (will rebuild on next query)
lfm cache-clear --all
```

**Cache location**:
- Windows: `%LOCALAPPDATA%\lfm\cache\`
- macOS/Linux: `~/.cache/lfm/`

---

## Spotify/Sonos Issues

### "No Active Spotify Device" Error

**Problem**: Can't play music - no active device.

**Solution**:

1. **Open Spotify** on any device (phone, computer, web player)

2. **Play something** (anything) then pause it
   - This activates the device

3. **Try wake command**:
   ```bash
   lfm spotify devices  # List available devices
   lfm config set-spotify-default-device "Device Name"
   ```

4. **Or activate directly** (future feature):
   ```bash
   lfm activate-device
   ```

### Spotify Authentication Issues

**Problem**: Errors about Spotify authentication or invalid credentials.

**Solution**:

1. **Get Spotify Developer Credentials**:
   - Visit https://developer.spotify.com/dashboard
   - Log in with your Spotify account
   - Click "Create an App"
   - Note your **Client ID** and **Client Secret**

2. **Configure LFM**:
   ```bash
   lfm config set-spotify-client-id YOUR_CLIENT_ID
   lfm config set-spotify-client-secret YOUR_CLIENT_SECRET
   ```

3. **First use**: LFM will open a browser for you to authorize the app

### Sonos Room Not Found

**Problem**: Error about Sonos room not being found.

**Solution**:

1. **List available rooms**:
   ```bash
   lfm sonos rooms
   ```

2. **Use exact room name**:
   ```bash
   lfm play "Artist" --room "Living Room"  # Use exact name from list
   ```

3. **Set default room**:
   ```bash
   lfm config set-sonos-default-room "Living Room"
   ```

4. **Check node-sonos-http-api is running**:
   - This must be set up separately on your network
   - See: https://github.com/jishi/node-sonos-http-api

---

## Common Error Messages

### "Rate limit exceeded"

**Problem**: Too many API requests to Last.fm.

**Solution**: Wait a minute and try again. LFM has built-in throttling, but very rapid queries can still hit limits.

### "Network error" or "Connection failed"

**Checklist**:
- ✅ Check internet connection
- ✅ Verify Last.fm is online: https://www.last.fm
- ✅ Try the API status command:
  ```bash
  lfm api-status
  ```

### "Invalid JSON output"

**Problem**: Error parsing JSON from API.

**Solution**:
1. Try again (might be temporary API issue)
2. Clear cache: `lfm cache-clear --all`
3. If persists, [report an issue](https://github.com/Steven-Marshall/lfm/issues)

---

## Getting More Help

### Check Command Help

Most commands have built-in help:

```bash
lfm --help
lfm artists --help
lfm config --help
```

### Enable Verbose Output

For debugging, use `--verbose` or `--timing`:

```bash
lfm artists --limit 5 --verbose --timing
```

This shows:
- API response times
- Cache hits/misses
- Detailed progress

### Check Logs

**Windows**: `%LOCALAPPDATA%\lfm\logs\`
**macOS/Linux**: `~/.cache/lfm/logs/`

### Report a Bug

If you've found a bug:

1. **Gather information**:
   ```bash
   lfm --version
   lfm config  # Remove your API key before sharing!
   ```

2. **Create an issue**:
   https://github.com/Steven-Marshall/lfm/issues

3. **Include**:
   - Your operating system
   - LFM version
   - Steps to reproduce
   - Error message (full text)
   - What you expected to happen

### Community Discussions

For questions, ideas, or general discussion:
https://github.com/Steven-Marshall/lfm/discussions

---

## Quick Diagnostic Checklist

If LFM isn't working, try these in order:

1. ✅ **Version check**: `lfm --version`
2. ✅ **Config check**: `lfm config` (API key and username set?)
3. ✅ **API status**: `lfm api-status`
4. ✅ **Simple query**: `lfm artists --limit 3`
5. ✅ **Cache clear**: `lfm cache-clear --all` (if queries never work)
6. ✅ **Restart terminal** (if just installed)

If all fail, check:
- Internet connection
- Last.fm website is up
- Your Last.fm account has scrobbles

---

**Still stuck? [Open an issue](https://github.com/Steven-Marshall/lfm/issues) - we're here to help!**
