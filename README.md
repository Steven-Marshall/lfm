# LFM - Last.fm CLI Tool

A powerful command-line interface for retrieving your Last.fm music statistics with intelligent caching and cross-platform support.

## Features

- 🎵 **Comprehensive Statistics** - Top artists, tracks, and albums with play counts
- 🎸 **Music Recommendations** - Discover new artists based on your listening history
- 🚀 **Smart Caching** - File-based cache system for improved performance
- 🌍 **Cross-Platform** - Windows, Linux, and WSL support
- 🎨 **Unicode Support** - Auto-detecting terminal capabilities with graceful ASCII fallbacks
- 🔍 **Smart Artist Matching** - Automatic name correction for artist variations
- ⚡ **Fast Performance** - Optimized API usage with configurable throttling
- 🔧 **Flexible Configuration** - User-friendly config management
- 📊 **Cache Management** - Built-in cache status and cleanup commands

## Installation

### Requirements

- **.NET 9 Runtime** (required for framework-dependent builds)
- **Last.fm API Key** (free from [Last.fm API](https://www.last.fm/api))

### Download

Download the latest release for your platform:

- **Windows**: `lfm.exe` (3.9MB)
- **Linux**: `lfm` (2.8MB)

### Setup

1. **Get a Last.fm API Key**:
   - Visit https://www.last.fm/api/account/create
   - Create a new application
   - Copy your API key

2. **Configure the tool**:
   ```bash
   # Set your API key
   lfm config set api-key YOUR_API_KEY_HERE
   
   # Set your default username
   lfm config set username YOUR_LASTFM_USERNAME
   ```

## Usage

### Basic Commands

```bash
# Get your top 10 artists
lfm artists

# Get your top 20 tracks from the last 7 days
lfm tracks --limit 20 --period 7day

# Get your top albums with detailed timing
lfm albums --timing

# Get your top tracks by a specific artist (supports name variations)
lfm artist-tracks "Radiohead"
# Also works with: "radiohead", etc.

# Get your top albums by a specific artist (supports name variations)  
lfm artist-albums "Pink Floyd"
# Also works with: "pink floyd", etc.

# Get global top tracks for an artist (supports name variations)
lfm tracks --artist "The Beatles"
# Also works with: "Beatles", "beatles", etc.

# Get personalized artist recommendations
lfm recommendations
# Exclude artists you already know (10+ plays)
lfm recommendations --filter 10 --limit 20
# Analyze more artists for better recommendations
lfm recommendations --artist-limit 50 --period 12month --verbose

# Filter out specific genres using tags (e.g., classical, christmas)
lfm recommendations --exclude-tags --verbose
# Configure excluded tags
lfm config add-excluded-tag "classical"
lfm config add-excluded-tag "christmas"
lfm config show-excluded-tags
```

### Cache Management

```bash
# View cache status and statistics
lfm cache-status

# Clear expired cache entries
lfm cache-clear --expired

# Clear all cache entries
lfm cache-clear --all
```

### Advanced Options

#### Cache Behavior
- `--force-cache` - Use cached data regardless of expiry
- `--force-api` - Always call API and cache result
- `--no-cache` - Disable caching entirely

#### Performance & Debugging
- `--timing` - Show API response times and cache hits/misses
- `--verbose` - Detailed progress information
- `--delay <ms>` - Throttle API requests (e.g., `--delay 1000`)

#### Filtering & Ranges
- `--range 10-20` - Display specific position ranges
- `--limit 50` - Number of results to display
- `--period overall|7day|1month|3month|6month|12month`

### Example Workflows

```bash
# Quick overview of your music
lfm artists --limit 5
lfm tracks --limit 5 --period 1month

# Deep dive into a specific artist
lfm artist-tracks "Pink Floyd" --limit 20 --timing

# Discover new music
lfm recommendations --filter 5 --limit 10
# - Analyzes your top artists
# - Finds similar artists you haven't listened to much
# - Filter excludes artists with 5+ plays

# Discover new music with genre filtering
lfm recommendations --exclude-tags --filter 5 --limit 20
# - Additionally filters out genres based on your configured tags
# - Useful for excluding classical, christmas, or other unwanted genres

# Performance analysis
lfm benchmark-cache your-username

# Cache maintenance
lfm cache-status
lfm cache-clear --expired
```

## Configuration

Configuration is stored in platform-appropriate locations:
- **Windows**: `%APPDATA%\lfm\config.json`
- **Linux**: `~/.config/lfm/config.json`

### Available Settings

```bash
# View current configuration
lfm config

# Set API key
lfm config set api-key YOUR_KEY

# Set default username
lfm config set username YOUR_USERNAME

# Set default period
lfm config set default-period 1month

# Set default limit
lfm config set default-limit 20

# Enable/disable Unicode symbols
lfm config set unicode enabled|disabled|auto
```

## Cache System

LFM uses an intelligent file-based caching system that:

- **Reduces redundant API calls** for frequently accessed data
- **Respects Last.fm rate limits** with configurable throttling
- **Cross-platform storage** using XDG Base Directory standards
- **Automatic cleanup** with configurable size and age limits
- **Smart expiry** based on data freshness requirements

### Cache Configuration

```bash
# Cache is stored in:
# Windows: %LOCALAPPDATA%\lfm\cache\
# Linux: ~/.cache/lfm/

# Default settings:
# - Expiry: 60 minutes
# - Max size: 100 MB
# - Max files: 10,000
# - Cleanup interval: 6 hours
```

## Building from Source

### Prerequisites

- **.NET 9 SDK**
- **Git**

### Build Instructions

```bash
# Clone the repository
git clone https://github.com/Steven-Marshall/lfm.git
cd lfm

# Build for your platform
dotnet build -c Release

# Or publish for specific platforms
dotnet publish src/Lfm.Cli -c Release -r win-x64 -o publish/win-x64 --self-contained false
dotnet publish src/Lfm.Cli -c Release -r linux-x64 -o publish/linux-x64 --self-contained false

# Run tests (if available)
dotnet test
```

## Platform Support

### Windows
- **PowerShell 5.x** - Full Unicode support with auto-encoding
- **PowerShell 7+** - Native Unicode support
- **Command Prompt** - Unicode with auto-configuration
- **Windows Terminal** - Full feature support

### Linux/WSL
- **Most terminals** - Full Unicode support
- **Legacy terminals** - Graceful ASCII fallback
- **WSL integration** - Seamless Windows interoperability

## Performance

The caching system provides significant performance improvements for repeated queries:
- **Cold start**: Initial API calls as needed
- **Warm cache**: Fast file system access for cached data
- **Cache hit rate**: High for repeated queries within expiry time
- **API throttling**: Configurable to respect Last.fm rate limits

## Troubleshooting

### Common Issues

1. **API Key Errors**
   ```bash
   # Verify your API key is set
   lfm config
   
   # Reset if needed
   lfm config set api-key YOUR_NEW_KEY
   ```

2. **Artist Not Found Issues**
   ```bash
   # The tool automatically corrects artist name variations
   # These commands support autocorrect for "The Beatles":
   lfm artist-tracks "Beatles"        # Personal tracks by artist
   lfm artist-albums "beatles"        # Personal albums by artist  
   lfm tracks --artist "The Beatles"  # Global tracks by artist
   
   # If still not found, try the exact name from Last.fm website
   ```

3. **Unicode Display Issues**
   ```bash
   # Force ASCII mode if Unicode characters appear as "?"
   lfm config set unicode disabled
   
   # Or let auto-detection handle it
   lfm config set unicode auto
   ```

4. **Performance Issues**
   ```bash
   # Check cache status
   lfm cache-status
   
   # Clear if cache is corrupted
   lfm cache-clear --all
   
   # Benchmark performance
   lfm benchmark-cache your-username
   ```

5. **.NET Runtime Missing**
   - Download .NET 9 Runtime from https://dotnet.microsoft.com/download/dotnet/9.0
   - Choose "Run desktop apps" runtime for your platform

### Getting Help

- **Built-in help**: `lfm --help` or `lfm <command> --help`
- **Issues**: [GitHub Issues](https://github.com/Steven-Marshall/lfm/issues)
- **Last.fm API**: [Official Documentation](https://www.last.fm/api)

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

**Version**: 1.1.0  
**Author**: Steven Marshall  
**Repository**: https://github.com/Steven-Marshall/lfm