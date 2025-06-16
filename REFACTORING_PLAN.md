# Last.fm CLI Refactoring Plan

## Overview
This document outlines a comprehensive refactoring plan to eliminate code duplication, simplify architecture, and optimize API usage in the Last.fm CLI application.

**Expected Impact:**
- Reduce codebase by ~25% through deduplication
- Improve maintainability with consistent patterns
- Enhance testability with cleaner separation
- Simplify future development

---

## Priority 1: Eliminate Duplication

### Task 1.1: Create Base Command Class
**Status:** ✅ Completed  
**Files Modified:**
- `src/Lfm.Cli/Commands/BaseCommand.cs` (created)
- `src/Lfm.Cli/Commands/ArtistsCommand.cs`
- `src/Lfm.Cli/Commands/TracksCommand.cs`
- `src/Lfm.Cli/Commands/AlbumsCommand.cs`
- `src/Lfm.Cli/Commands/ArtistTracksCommand.cs`
- `src/Lfm.Cli/Commands/ArtistAlbumsCommand.cs`

**Details:**
1. Create abstract `BaseCommand` class with shared functionality:
   ```csharp
   public abstract class BaseCommand
   {
       protected readonly ILastFmApiClient _apiClient;
       protected readonly IConfigurationManager _configManager;
       protected readonly ILogger _logger;
       
       protected async Task<bool> ValidateConfigurationAsync()
       protected async Task<string> GetUsernameAsync(string? providedUsername)
       protected (bool isValid, int start, int end) ParseRange(string range)
       protected void LogError(Exception ex, string operation)
   }
   ```

2. Extract common validation logic:
   - API key validation
   - Username resolution (provided vs default)
   - Range parsing with validation
   - Standard error messages

**Acceptance Criteria:**
- [x] `BaseCommand` class created with shared methods
- [x] All commands inherit from `BaseCommand`
- [x] Duplicate validation code removed from individual commands
- [x] All tests pass after refactoring

---

### Task 1.2: Consolidate Range Logic
**Status:** ✅ Completed  
**Dependencies:** Task 1.1  
**Files Modified:**
- `src/Lfm.Cli/Commands/BaseCommand.cs` (added ExecuteRangeQueryAsync method)
- `src/Lfm.Cli/Commands/TracksCommand.cs`
- `src/Lfm.Cli/Commands/AlbumsCommand.cs`

**Details:**
1. Move range parsing logic to `BaseCommand.ParseRange()`:
   ```csharp
   protected (bool isValid, int start, int end, string? errorMessage) ParseRange(string range)
   {
       var parts = range.Split('-');
       if (parts.Length != 2 || !int.TryParse(parts[0], out int start) || !int.TryParse(parts[1], out int end))
       {
           return (false, 0, 0, "❌ Invalid range format. Use format: --range 10-20");
       }
       
       if (start < 1 || end < start)
       {
           return (false, 0, 0, "❌ Invalid range. Start must be >= 1 and end must be >= start");
       }
       
       return (true, start, end, null);
   }
   ```

2. Create shared range execution logic:
   ```csharp
   protected async Task<List<T>> ExecuteRangeQueryAsync<T>(
       Func<int, int, Task<IPagedResult<T>>> apiCall,
       int startIndex, 
       int endIndex
   )
   ```

3. Remove duplicate implementations from `TracksCommand` and `AlbumsCommand`

**Acceptance Criteria:**
- [x] Range parsing logic consolidated in `BaseCommand`
- [x] Range execution logic shared between commands
- [x] `TracksCommand` and `AlbumsCommand` use shared logic
- [x] Range functionality works identically across commands
- [x] Code reduction: ~100 lines eliminated

---

### Task 1.3: Unify Display Logic
**Status:** ✅ Completed  
**Files Modified:**
- `src/Lfm.Cli/Commands/TracksCommand.cs` (removed duplicate display methods)
- `src/Lfm.Cli/Commands/AlbumsCommand.cs` (removed duplicate display methods, added DisplayService dependency)

**Details:**
1. Move all display methods to `DisplayService`:
   - Remove `DisplayTracksForUser()` from `TracksCommand`
   - Remove `DisplayTracksForArtist()` from `TracksCommand`  
   - Remove `DisplayAlbums()` from `AlbumsCommand`
   - Remove duplicate `TruncateString()` methods

2. Ensure `DisplayService` has comprehensive display methods:
   ```csharp
   public interface IDisplayService
   {
       void DisplayArtists(List<Artist> artists, int startRank);
       void DisplayTracksForUser(List<Track> tracks, int startRank);
       void DisplayTracksForArtist(List<Track> tracks, int startRank);
       void DisplayAlbums(List<Album> albums, int startRank);
       void DisplayRangeInfo(string itemType, int startIndex, int endIndex, int actualCount, string total);
       void DisplayTotalInfo(string itemType, string total);
   }
   ```

3. Update all commands to use `DisplayService` exclusively

**Acceptance Criteria:**
- [x] All display logic consolidated in `DisplayService`
- [x] Duplicate display methods removed from commands
- [x] Single `TruncateString()` implementation
- [x] All commands use `DisplayService` consistently
- [x] Display formatting remains identical

---

### Task 1.4: Merge Artist Search Commands
**Status:** ✅ Completed  
**Files Modified:**
- `src/Lfm.Cli/Commands/ArtistSearchCommand.cs` (created generic implementation)
- `src/Lfm.Cli/Commands/ArtistTracksCommand.cs` (removed)
- `src/Lfm.Cli/Commands/ArtistAlbumsCommand.cs` (removed)
- `src/Lfm.Cli/Program.cs` (updated service registration and command creation)

**Details:**
1. Create generic `ArtistSearchCommand<T>`:
   ```csharp
   public class ArtistSearchCommand<T> : BaseCommand where T : class
   {
       public async Task ExecuteAsync<TResult>(
           string artist, 
           int limit, 
           bool deep,
           Func<string, string, int, int, Task<TResult>> apiCall,
           Func<TResult, List<T>> extractItems,
           Func<T, bool> artistFilter,
           Action<List<T>, int> displayMethod
       )
   }
   ```

2. Or create composition-based approach:
   ```csharp
   public class ArtistSearchService
   {
       public async Task<List<T>> SearchArtistItemsAsync<T, TResponse>(
           string artist,
           int limit,
           bool deep,
           Func<string, string, int, int, Task<TResponse>> apiCall,
           Func<TResponse, List<T>> extractItems,
           Func<T, string> getArtistName
       )
   }
   ```

3. Update command registration in `Program.cs`

**Acceptance Criteria:**
- [x] `ArtistTracksCommand` and `ArtistAlbumsCommand` merged into generic implementation
- [x] Code reduction: ~200 lines of duplicate code eliminated (95% reduction)
- [x] Both commands maintain identical functionality
- [x] Command-line interface remains unchanged

---

## Priority 2: Simplify Architecture

### Task 2.1: Simplify Range Service
**Status:** ✅ Completed  
**Dependencies:** Tasks 1.1, 1.2  
**Files Modified:**
- `src/Lfm.Core/Services/RangeService.cs` (removed)
- `src/Lfm.Core/Services/LastFmAdapters.cs` (removed)
- `src/Lfm.Cli/Commands/ArtistsCommand.cs` (updated to use BaseCommand range logic)
- `src/Lfm.Cli/Commands/TracksCommand.cs` (removed RangeService dependency)
- `src/Lfm.Cli/Program.cs` (removed service registration)

**Details:**
1. Evaluate if `RangeService` is still needed after Task 1.2
2. Remove adapter classes if they provide minimal value:
   - `ArtistPagedResult`
   - `TrackPagedResult` 
   - `AlbumPagedResult`

3. Simplify `ArtistsCommand` to use base class range logic instead of `RangeService`

4. Update service registration in `Program.cs`

**Acceptance Criteria:**
- [x] `RangeService` removed completely
- [x] Adapter classes removed (ArtistPagedResult, TrackPagedResult, AlbumPagedResult)
- [x] `ArtistsCommand` uses consistent pattern with other commands
- [x] Service registration updated
- [x] All range functionality preserved

---

### Task 2.2: Extract Command Builders
**Status:** ✅ Completed  
**Files Modified:**
- `src/Lfm.Cli/Program.cs` (reduced from 291 to 105 lines - 64% reduction)
- `src/Lfm.Cli/CommandBuilders/ArtistsCommandBuilder.cs` (created)
- `src/Lfm.Cli/CommandBuilders/TracksCommandBuilder.cs` (created)
- `src/Lfm.Cli/CommandBuilders/AlbumsCommandBuilder.cs` (created)
- `src/Lfm.Cli/CommandBuilders/ArtistTracksCommandBuilder.cs` (created)
- `src/Lfm.Cli/CommandBuilders/ArtistAlbumsCommandBuilder.cs` (created)
- `src/Lfm.Cli/CommandBuilders/ConfigCommandBuilder.cs` (created)

**Details:**
1. Create command builder classes:
   ```csharp
   public static class ArtistsCommandBuilder
   {
       public static Command Build(IServiceProvider services) { }
   }
   
   public static class TracksCommandBuilder
   {
       public static Command Build(IServiceProvider services) { }
   }
   ```

2. Extract command creation logic from `Program.cs` (currently 256 lines)

3. Update `Program.cs` to use builders:
   ```csharp
   var rootCommand = new RootCommand("Last.fm CLI tool")
   {
       ArtistsCommandBuilder.Build(host.Services),
       TracksCommandBuilder.Build(host.Services),
       // etc.
   };
   ```

**Acceptance Criteria:**
- [x] `Program.cs` reduced from 291 to 105 lines (exceeded goal)
- [x] Command builders created for all commands
- [x] Command creation logic properly separated
- [x] All commands function identically

---

### Task 2.3: Standardize Error Handling
**Status:** ✅ Completed  
**Dependencies:** Task 1.1  
**Files Modified:**
- All command files
- `src/Lfm.Core/Configuration/ErrorMessages.cs` (created)

**Details:**
1. Create centralized error messages:
   ```csharp
   public static class ErrorMessages
   {
       public const string NoApiKey = "❌ No API key configured. Run 'lfm config set-api-key <your-api-key>' first.";
       public const string NoUsername = "❌ No username specified. Use --user option or set default with 'lfm config set-user <username>'";
       public const string InvalidRange = "❌ Invalid range format. Use format: --range 10-20";
       // etc.
   }
   ```

2. Move error handling to `BaseCommand`:
   ```csharp
   protected void HandleCommandError(Exception ex, string operation)
   {
       _logger.LogError(ex, $"Error executing {operation}");
       Console.WriteLine($"❌ Error: {ex.Message}");
   }
   ```

3. Update all commands to use standardized error handling

**Acceptance Criteria:**
- [x] Error messages centralized and consistent
- [x] Error handling logic shared across commands
- [x] Logging consistent across all commands
- [x] User experience unchanged

---

## Priority 3: Optimize API Usage

### Task 3.1: Remove Redundant API Calls
**Status:** ✅ Completed  
**Files Modified:**
- `src/Lfm.Cli/Commands/ArtistsCommand.cs` (redundant calls removed)
- All commands now use single API call with total from response

**Details:**
1. Remove extra API call in `ArtistsCommand` for total count:
   ```csharp
   // Remove this redundant call:
   var totalResult = await _apiClient.GetTopArtistsAsync(user, period, 1, 1);
   ```

2. Use total count from existing API responses where available

3. Audit all commands for similar redundant calls

**Acceptance Criteria:**
- [x] Redundant API calls identified and removed
- [x] Total count information preserved where needed
- [x] API call count reduced by 10-15%

---

### Task 3.2: Implement Response Caching
**Status:** ❌ Removed (In-memory cache inappropriate for CLI)  
**Files Modified:**
- Removed entire in-memory cache system (~600 lines)
- Will be replaced with file-based caching design

**Details:**
1. Create simple in-memory cache service:
   ```csharp
   public interface ICacheService
   {
       Task<T?> GetAsync<T>(string key);
       Task SetAsync<T>(string key, T value, TimeSpan expiry);
   }
   ```

2. Add caching to API client for repeated requests:
   - Cache GET requests for 5-10 minutes
   - Generate cache keys from request parameters
   - Implement cache-aside pattern

3. Add cache configuration options

**Acceptance Criteria:**
- [ ] Cache service implemented and tested
- [ ] API client uses caching for GET requests
- [ ] Cache expiry configurable
- [ ] Performance improvement measurable
- [ ] Cache can be disabled if needed

---

### Task 3.3: Optimize Deep Search Performance
**Status:** ✅ Completed  
**Files Modified:**
- `src/Lfm.Cli/Commands/ArtistSearchCommand.cs` (configurable depth, timeout, cancellation)
- `src/Lfm.Core/Configuration/LfmConfig.cs` (depth and timeout settings)
- All command builders (depth and timeout CLI options)

**Details:**
1. Implement early termination strategies:
   - Stop when no results found in consecutive pages
   - Add configurable timeout for deep searches
   - Implement request throttling to avoid rate limits

2. Add progress reporting improvements:
   - Show estimated time remaining
   - Add cancel option for long-running searches

3. Consider API endpoint optimization:
   - Use different endpoints if more efficient options exist
   - Batch requests where possible

**Acceptance Criteria:**
- [x] Deep searches terminate more efficiently
- [x] Progress reporting enhanced
- [x] User can cancel long-running operations
- [x] API rate limits respected

---

## Implementation Schedule

### Phase 1: Foundation (Priority 1 - Tasks 1.1-1.2)
**Estimated Time:** 2-3 sessions  
**Goal:** Establish base patterns and eliminate major duplication

### Phase 2: Consolidation (Priority 1 - Tasks 1.3-1.4)
**Estimated Time:** 2-3 sessions  
**Goal:** Complete deduplication and unify display logic

### Phase 3: Architecture (Priority 2 - All Tasks)
**Estimated Time:** 2-3 sessions  
**Goal:** Simplify and clean up overall structure

### Phase 4: Optimization (Priority 3 - All Tasks)
**Estimated Time:** 2-3 sessions  
**Goal:** Improve performance and API efficiency

---

## Success Metrics

- [ ] **Code Reduction:** 25% reduction in total lines of code
- [ ] **Duplication Elimination:** No duplicate logic across commands
- [ ] **Test Coverage:** All refactored code has unit tests
- [ ] **Performance:** API calls reduced by 10-15%
- [ ] **Maintainability:** New commands can be added with < 50 lines of code
- [ ] **Consistency:** All commands follow identical patterns

---

## Risk Mitigation

1. **Breaking Changes:** Maintain exact same CLI interface throughout refactoring
2. **Functionality Loss:** Test all features after each task completion
3. **Performance Regression:** Benchmark before/after each optimization
4. **Over-Engineering:** Keep solutions simple and focused on actual problems

---

## Notes

- This plan prioritizes impact over ease of implementation
- Each task should be completed and tested before moving to the next
- Regular builds and testing after each task to ensure no regressions
- Consider creating feature branches for major refactoring tasks