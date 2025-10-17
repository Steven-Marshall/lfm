# LFM - Last.fm CLI Tool

A powerful command-line interface for retrieving your Last.fm music statistics with intelligent caching, cross-platform support, and AI integration.

## What is LFM?

LFM brings your Last.fm listening data to the command line with features like:

- 🎵 **Music Statistics** - Top artists, tracks, and albums with play counts
- 🔍 **Discovery** - Find similar artists and get personalized recommendations
- 📊 **Listening History** - Check if you've listened to specific artists, tracks, or albums
- 🎧 **Playback Integration** - Queue music directly to Spotify or Sonos
- 🤖 **AI Integration** - Use with Claude Code or Claude Desktop for natural language queries
- 🚀 **Smart Caching** - Fast performance with intelligent cache management
- 🌍 **Cross-Platform** - Works on Windows, macOS, and Linux

## Quick Install

Choose your platform:

**Windows:**
```powershell
iwr -useb https://raw.githubusercontent.com/Steven-Marshall/lfm/master/install.ps1 | iex
```

**macOS/Linux:**
```bash
curl -fsSL https://raw.githubusercontent.com/Steven-Marshall/lfm/master/install.sh | bash
```

After installation, configure with your Last.fm API key:
```bash
lfm config set api-key YOUR_API_KEY_HERE
lfm config set username YOUR_LASTFM_USERNAME
```

Get your API key at: https://www.last.fm/api/account/create

## Getting Started

- **[Installation Guide](INSTALL.md)** - Detailed platform-specific installation instructions
- **[Quick Start Guide](QUICKSTART.md)** - Get up and running in 5 minutes
- **[MCP Setup Guide](MCP_SETUP.md)** - Use LFM with Claude Code or Claude Desktop
- **[Troubleshooting Guide](TROUBLESHOOTING.md)** - Common issues and solutions

## Usage Examples

### Basic Commands

```bash
# Get your top 10 artists
lfm artists --limit 10

# Get your top tracks from last month
lfm tracks --period 1month --limit 20

# Get your top albums from 2024
lfm albums --year 2024 --limit 10

# Check if you've listened to an artist
lfm check "Pink Floyd"

# Check if you've listened to a specific track
lfm check "Pink Floyd" "Comfortably Numb"

# Check album with detailed track breakdown
lfm check "Pink Floyd" --album "Dark Side of the Moon" --verbose
```

### Discovery

```bash
# Find artists similar to one you like
lfm similar "Taylor Swift" --limit 10

# Get personalized recommendations (exclude artists you already know)
lfm recommendations --filter 10 --totalartists 20

# Create a diverse playlist from your top tracks
lfm toptracks --period 1month --totaltracks 25 --tracks-per-artist 1

# Generate a weighted random mixtape
lfm mixtape --limit 30 --bias 0.5
```

### Playback (Spotify/Sonos)

```bash
# Play a track on Spotify or Sonos
lfm play "Pink Floyd" --track "Money" --album "Dark Side of the Moon"

# Queue an entire album
lfm play "Pink Floyd" --album "Dark Side of the Moon" --queue

# Play recommendations immediately
lfm recommendations --filter 10 --totalartists 15 --playnow
```

For complete command reference, see the [Quick Start Guide](QUICKSTART.md).

## Claude Integration

Use LFM with Claude Code or Claude Desktop to ask natural language questions about your music:

- "What were my top artists in 2024?"
- "Show me my most-played albums from last month"
- "Give me music recommendations based on my listening history"
- "Play Radiohead's OK Computer on Spotify"

See the [MCP Setup Guide](MCP_SETUP.md) for installation instructions.

## Features

### Music Statistics
- Top artists, tracks, and albums with play counts
- Flexible time periods (7 days, 1 month, 3 months, 6 months, 12 months, all time)
- Custom date ranges (e.g., `--from 2024-01-01 --to 2024-06-30`)
- Specific year queries (e.g., `--year 2023`)
- Artist-specific track and album searches

### Discovery & Recommendations
- Similar artist discovery using Last.fm's similarity algorithm
- Personalized recommendations based on your listening history
- Filter recommendations by play count (discover truly new artists)
- Tag-based filtering (exclude genres like classical, christmas)
- Diversity controls (limit tracks per artist)
- Weighted random mixtapes with configurable bias

### Listening History
- Check if you've listened to specific artists, tracks, or albums
- Detailed album analysis with track-level breakdowns
- Play count verification
- Identify which tracks you've heard from an album

### Playback Integration
- **Spotify Integration** - Queue tracks and albums, create playlists, control playback
- **Sonos Integration** - Play music directly on Sonos speakers
- Device management and default configuration
- Album version disambiguation (studio vs live vs greatest hits)

### Performance & Reliability
- Smart file-based caching (119x performance improvement)
- Configurable API throttling (respects Last.fm rate limits)
- Parallel API processing for deep searches
- Cache management tools (status, cleanup)
- API health status checker

### Developer Features
- Unicode support with auto-detection
- Smart artist name matching (autocorrect enabled)
- JSON output for programmatic use
- Verbose mode for debugging
- Comprehensive error handling

## Configuration

```bash
# View current configuration
lfm config

# Set API key and username
lfm config set api-key YOUR_API_KEY
lfm config set username YOUR_USERNAME

# Configure Spotify (requires Spotify app credentials)
lfm config set-spotify-client-id YOUR_CLIENT_ID
lfm config set-spotify-client-secret YOUR_CLIENT_SECRET
lfm config set-spotify-default-device "Device Name"

# Configure Sonos (requires node-sonos-http-api)
lfm config set-sonos-api-url "http://192.168.1.24:5005"
lfm config set-sonos-default-room "Living Room"

# Configure default settings
lfm config set default-period 1month
lfm config set default-limit 20
lfm config set unicode enabled|disabled|auto

# Manage excluded genres
lfm config add-excluded-tag "classical"
lfm config show-excluded-tags
```

Configuration is stored in:
- **Windows**: `%APPDATA%\lfm\config.json`
- **macOS/Linux**: `~/.config/lfm/config.json`

## Cache Management

```bash
# View cache status
lfm cache-status

# Clear expired entries
lfm cache-clear --expired

# Clear all cache entries
lfm cache-clear --all
```

Cache is stored in:
- **Windows**: `%LOCALAPPDATA%\lfm\cache\`
- **macOS/Linux**: `~/.cache/lfm/`

## Advanced Usage

### Time Periods

Use these with most commands:
- `--period 7day` - Last 7 days
- `--period 1month` - Last month
- `--period 3month` - Last 3 months
- `--period 6month` - Last 6 months
- `--period 12month` - Last 12 months
- `--period overall` - All time (default)

Or use specific dates:
- `--year 2023` - Entire year
- `--from 2024-01-01 --to 2024-06-30` - Custom date range

### Cache Options

- `--force-cache` - Use cached data regardless of expiry
- `--force-api` - Always call API and cache result
- `--no-cache` - Disable caching entirely

### Performance Options

- `--timing` - Show API response times and cache hits/misses
- `--verbose` - Detailed progress information
- `--delay <ms>` - Throttle API requests

### Output Options

- `--json` - JSON output for programmatic use
- `--limit <n>` - Number of results (1-1000)
- `--range 10-20` - Display specific position ranges

## Building from Source

### Prerequisites

- **.NET 8 or 9 SDK**
- **Git**

### Build Instructions

```bash
# Clone the repository
git clone https://github.com/Steven-Marshall/lfm.git
cd lfm

# Build for your platform
dotnet build -c Release

# Or publish self-contained binaries
dotnet publish src/Lfm.Cli -c Release -r win-x64 -o publish/win-x64 --self-contained true
dotnet publish src/Lfm.Cli -c Release -r osx-x64 -o publish/osx-x64 --self-contained true
dotnet publish src/Lfm.Cli -c Release -r osx-arm64 -o publish/osx-arm64 --self-contained true
dotnet publish src/Lfm.Cli -c Release -r linux-x64 -o publish/linux-x64 --self-contained true
```

## Platform Support

### Windows
- PowerShell 5.x, PowerShell 7+, Command Prompt
- Windows Terminal recommended for full Unicode support
- Windows 10 or later

### macOS
- Intel Macs (x86_64)
- Apple Silicon Macs (ARM64 / M1/M2/M3)
- macOS 10.15 (Catalina) or later

### Linux
- Most modern distributions (Ubuntu 18.04+, Debian 10+, Fedora 30+)
- Full Unicode support in most terminals

## Getting Help

- **Built-in help**: `lfm --help` or `lfm <command> --help`
- **Documentation**: See [QUICKSTART.md](QUICKSTART.md), [INSTALL.md](INSTALL.md), [MCP_SETUP.md](MCP_SETUP.md)
- **Troubleshooting**: See [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
- **Issues**: [GitHub Issues](https://github.com/Steven-Marshall/lfm/issues)
- **Discussions**: [GitHub Discussions](https://github.com/Steven-Marshall/lfm/discussions)

## Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

### Development Setup

1. Fork the repository
2. Clone your fork
3. Create a feature branch
4. Make your changes
5. Test thoroughly
6. Submit a pull request

## License

This project is open source. See the repository for license details.

## Acknowledgments

- **Last.fm** - For providing the excellent music tracking API
- **.NET Community** - For the robust cross-platform framework
- **Contributors** - Thank you for making this tool better

---

**Version**: 1.5.0
**Author**: Steven Marshall
**Repository**: https://github.com/Steven-Marshall/lfm
