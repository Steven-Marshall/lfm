# Quick Start Guide

Get up and running with LFM in 5 minutes!

## Prerequisites

- ✅ LFM installed → [Installation Guide](INSTALL.md)
- ✅ Last.fm account with scrobbling enabled

---

## Step 1: Get Your Last.fm API Key

LFM needs an API key to access your Last.fm data.

1. **Visit the Last.fm API page:**
   - Go to https://www.last.fm/api/account/create
   - Log in to your Last.fm account if prompted

2. **Create an application:**
   - **Application name**: `LFM CLI` (or any name you like)
   - **Application description**: `Personal Last.fm statistics tool`
   - Check the box: "I have read and agree to the API Terms of Service"
   - Click "Submit"

3. **Copy your API Key**
   - You'll see a page with your **API Key** (a long string of letters and numbers)
   - Copy this key - you'll need it in the next step

---

## Step 2: Configure LFM

Open your terminal/command prompt and run:

```bash
# Set your API key
lfm config set api-key YOUR_API_KEY_HERE

# Set your Last.fm username
lfm config set username YOUR_LASTFM_USERNAME
```

**Example:**
```bash
lfm config set api-key a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6
lfm config set username musiclover123
```

---

## Step 3: Try It Out!

### See Your Top 5 Artists

```bash
lfm artists --limit 5
```

**Example output:**
```
 # Artist                      Plays
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 1 Taylor Swift                2,453
 2 The Beatles                 1,892
 3 Radiohead                   1,654
 4 Pink Floyd                  1,432
 5 Arctic Monkeys              1,201
```

### See Your Top Tracks From Last Month

```bash
lfm tracks --period 1month --limit 10
```

### Check If You've Listened to an Artist

```bash
lfm check "The Beatles"
```

**Output:** `The Beatles: 1,892 plays`

### Get Your Top Albums

```bash
lfm albums --limit 10
```

---

## Most Useful Commands

### Viewing Your Stats

```bash
# Top artists (all time)
lfm artists --limit 20

# Top tracks from last week
lfm tracks --period 7day --limit 15

# Top albums from 2024
lfm albums --year 2024 --limit 10

# Recent listening history (last 24 hours)
lfm recent --hours 24 --limit 20
```

### Checking Your Listening History

```bash
# Check if you've listened to an artist
lfm check "Radiohead"

# Check if you've listened to a specific track
lfm check "Radiohead" "Paranoid Android"

# Check with detailed album breakdown
lfm check "Pink Floyd" --album "Dark Side of the Moon" --verbose
```

### Music Discovery

```bash
# Find artists similar to one you like
lfm similar "Taylor Swift" --limit 10

# Get personalized recommendations (exclude artists you already know well)
lfm recommendations --filter 10 --totalartists 20

# Search your listening history for a specific artist's tracks
lfm artist-tracks "The Beatles" --limit 20
```

### Time Periods

You can use these time periods with most commands:

- `--period 7day` - Last 7 days
- `--period 1month` - Last month
- `--period 3month` - Last 3 months
- `--period 6month` - Last 6 months
- `--period 12month` - Last 12 months
- `--period overall` - All time (default)

Or use specific dates:

```bash
# Specific year
lfm artists --year 2023 --limit 10

# Date range
lfm tracks --from 2024-01-01 --to 2024-06-30 --limit 20
```

---

## Command Cheat Sheet

| What You Want | Command |
|---------------|---------|
| Top 10 artists | `lfm artists --limit 10` |
| Top tracks this week | `lfm tracks --period 7day --limit 10` |
| Top albums of 2024 | `lfm albums --year 2024` |
| Recent listening | `lfm recent --limit 20` |
| Check an artist | `lfm check "Artist Name"` |
| Find similar artists | `lfm similar "Artist Name"` |
| Get recommendations | `lfm recommendations --filter 5` |
| Artist's top tracks | `lfm artist-tracks "Artist Name"` |
| List all playlists | `lfm playlists` |
| Play a playlist | `lfm playlist --name "Playlist Name"` |
| Search concerts | `lfm concerts "Artist Name"` |
| View a setlist | `lfm setlist <setlist-id>` |
| Help with any command | `lfm <command> --help` |

---

## Next Steps

### Optional: Setlist.fm Integration

Search for concerts and view setlists:

```bash
# Configure Setlist.fm (get API key from https://www.setlist.fm/settings/api)
lfm config set-setlistfm-api-key YOUR_API_KEY

# Search for concerts by an artist
lfm concerts "Radiohead" --year 2024

# Get a specific setlist
lfm setlist 13582d35
```

### Optional: Spotify Integration

If you have a Spotify account, you can queue music directly from LFM:

```bash
# Configure Spotify (requires Spotify Developer credentials)
lfm config set-spotify-client-id YOUR_CLIENT_ID
lfm config set-spotify-client-secret YOUR_CLIENT_SECRET

# Then queue music:
lfm play "Pink Floyd" --album "Dark Side of the Moon"

# List and play your playlists:
lfm playlists
lfm playlist --name "Your Playlist Name"
```

See the [full README](README.md#spotify-integration) for details.

### Optional: Use with Claude

Want to ask Claude about your music using natural language?

→ Continue to [MCP Setup Guide](MCP_SETUP.md)

---

## Getting Help

- **Command help**: `lfm --help` or `lfm <command> --help`
- **Common issues**: [Troubleshooting Guide](TROUBLESHOOTING.md)
- **Full documentation**: [README.md](README.md)
- **Report bugs**: [GitHub Issues](https://github.com/Steven-Marshall/lfm/issues)

---

## Tips & Tricks

### Save Time with Aliases

Add these to your shell config (`~/.bashrc`, `~/.zshrc`, or PowerShell profile):

```bash
# Bash/Zsh
alias lfa='lfm artists --limit 10'
alias lft='lfm tracks --period 1month --limit 10'
alias lfr='lfm recent --limit 20'

# PowerShell
Set-Alias lfa 'lfm artists --limit 10'
```

### Performance Tip

LFM uses intelligent caching. The first query might be slow, but subsequent queries are much faster!

If you want to clear the cache:
```bash
lfm cache-clear --all
```

### JSON Output

Most commands support `--json` for programmatic use:

```bash
lfm artists --limit 5 --json
```

---

**Ready to explore? Start with `lfm artists` and see what you discover about your listening habits!**
