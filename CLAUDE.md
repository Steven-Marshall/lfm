# Last.fm CLI - Claude Session Notes

## Project Overview
Last.fm CLI tool written in C# (.NET) for retrieving music statistics. The project has undergone significant refactoring to eliminate code duplication, simplify architecture, and optimize API usage.

## Current Architecture
- **Lfm.Cli**: CLI interface with commands and command builders
- **Lfm.Core**: Core functionality including services, models, and configuration
- Uses System.CommandLine for CLI framework
- Implements caching with configurable in-memory cache
- Centralized error handling and display services

## Refactoring Status (Per REFACTORING_PLAN.md)

### ✅ Priority 1: Eliminate Duplication (COMPLETED)
- **Task 1.1**: Base Command Class ✅
- **Task 1.2**: Consolidate Range Logic ✅  
- **Task 1.3**: Unify Display Logic ✅
- **Task 1.4**: Merge Artist Search Commands ✅

### ✅ Priority 2: Simplify Architecture (COMPLETED)
- **Task 2.1**: Simplify Range Service ✅
- **Task 2.2**: Extract Command Builders ✅
- **Task 2.3**: Standardize Error Handling ✅

### ⏳ Priority 3: Optimize API Usage (PENDING)
- **Task 3.1**: Remove Redundant API Calls ⏳
- **Task 3.2**: Implement Response Caching ⏳ (Infrastructure exists, may need verification)
- **Task 3.3**: Optimize Deep Search Performance ⏳

## Key Files
- `src/Lfm.Cli/Program.cs` - Main entry point and DI setup
- `src/Lfm.Cli/Commands/BaseCommand.cs` - Shared command functionality
- `src/Lfm.Core/Configuration/ErrorMessages.cs` - Centralized error messages
- `src/Lfm.Core/Services/LastFmApiClient.cs` - API client
- `src/Lfm.Core/Services/CachedLastFmApiClient.cs` - Cached API client wrapper
- `src/Lfm.Core/Services/DisplayService.cs` - Display formatting

## Current Issues/Next Steps

### File-Based Caching Design (For Future Implementation)

**Context**: In-memory cache removed as inappropriate for CLI (doesn't persist between process calls). Need smart file-based cache.

**Key Requirements:**
- Cache API responses to disk for persistence between CLI calls
- Smart indexing to avoid loading unnecessary cache data
- Performance: 1-5ms cache hits vs 200-500ms API calls (40-500x improvement)
- Storage: ~250-300MB for full 200K track library (reasonable)

**Cache Structure Design:**
```
%APPDATA%/lfm/cache/
├── index.json              # 1KB - metadata of what's cached
├── user-tracks-pages/      # Chunked data
│   ├── 1-50.json          # 25MB chunks
│   ├── 51-100.json
├── artist-filtered/        # Pre-filtered results
│   ├── taylor-swift.json  # 25KB
│   └── pink-floyd.json
└── metadata.json          # Expiry times, cache config
```

**Critical Design Principles:**
1. Index-first approach (avoid loading 150MB on simple commands)
2. Selective loading based on command type
3. Configurable expiry (5-10 minutes default)
4. Graceful degradation (API fallback if cache fails)

**Performance Analysis Complete:**
- Deep search first call: ~80 seconds (200 API calls)
- Deep search cached: ~0.8 seconds (100x faster)
- Simple calls: Stay fast (don't load unnecessary cache)

**Next Steps**: Design cache key strategy and implement index-based loading.

## Recent Sessions

### Session: 2025-01-16
- **Status**: Reviewed project after file resync, completed cache removal
- **Findings**: 
  - Most refactoring tasks completed successfully
  - ErrorMessages.cs exists and is properly integrated
  - Architecture is clean and well-organized
- **Completed**: 
  - Marked Task 2.3 (Standardize Error Handling) as completed
  - Marked Task 3.1 (Remove Redundant API Calls) as completed  
  - **REMOVED** entire in-memory cache system (600+ lines) for clean slate
- **Next**: Design and implement file-based caching system

## Build/Test Commands
*To be documented when discovered*

## Notes
- Uses .NET 8/9 (both target frameworks present)
- Publishes to both Windows and Linux
- Configuration stored in user's AppData/lfm folder
- API key required from Last.fm