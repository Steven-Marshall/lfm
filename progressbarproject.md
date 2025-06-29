# Progress Bar Implementation Plan

## Overview
Implement consistent progress reporting for long-running operations in the Last.fm CLI tool. This will provide users with real-time feedback during operations that process multiple API calls and can take 10+ seconds to complete.

## Motivation
After implementing comprehensive API throttling, several operations now take significantly longer but provide no feedback to users:
- Date range aggregation: 30+ seconds for full year queries
- Range queries: 10-30+ seconds for large ranges  
- Recommendations: 10-20+ seconds with sequential processing
- Deep search operations: Variable duration

## Target Operations

### High Priority
1. **Date Range Aggregation** (albums/tracks/artists --year/--from/--to)
   - Current experience: `albums --year 2023` shows nothing for 30+ seconds
   - Proposed: `Processing page 5 of 18... (2,847 tracks processed, 156 albums found)`
   - Known total: ✅ Total pages from first API response

2. **Range Queries** (--range parameter)
   - Current experience: `artists --range 1-1000` shows nothing for 20+ seconds
   - Proposed: `Loading page 12 of 20... (fetching artists 501-550)`
   - Known total: ✅ Calculate startPage/endPage upfront

### Medium Priority
3. **Recommendations Command**
   - Current experience: No feedback while analyzing artists sequentially
   - Proposed: `Analyzing artist 8 of 20... (Taylor Swift → finding similar artists)`
   - Known total: ✅ Number of top artists to analyze

4. **Deep Search Operations**
   - Current experience: Silent during potentially long searches
   - Proposed: `Searched 5,000 tracks, found 12 matches... (continuing)`
   - Known total: ❌ Unknown completion point

## Technical Design

### Core Interface
```csharp
public interface IProgressReporter
{
    void ReportProgress(int current, int? total = null, string? details = null);
    void ReportStatus(string message);
    void Complete(string? finalMessage = null);
    bool IsEnabled { get; }
}
```

### Implementation Classes
```csharp
public class ConsoleProgressReporter : IProgressReporter
{
    // Fancy progress bars with percentage and details
    // Format: [████████░░] 80% Processing page 16 of 20...
}

public class NullProgressReporter : IProgressReporter  
{
    // Silent implementation for piped output or --quiet mode
    // All methods are no-ops
}
```

### Integration Strategy
Progress reporting will be injected into existing throttled loops at the natural delay points:

```csharp
// Date range aggregation example
for (int page = 1; page <= totalPages && hasMore; page++)
{
    // Report progress before each API call
    progress?.ReportProgress(page, totalPages, $"page {page} - {tracksProcessed} tracks processed");
    
    // Apply existing throttling
    if (page > 1 && _apiThrottleMs > 0)
        await Task.Delay(_apiThrottleMs);
    
    // Existing API call logic...
}
```

## Implementation Plan

### Phase 1: Core Infrastructure
**Files to create:**
- `src/Lfm.Core/Services/Progress/IProgressReporter.cs`
- `src/Lfm.Core/Services/Progress/ConsoleProgressReporter.cs` 
- `src/Lfm.Core/Services/Progress/NullProgressReporter.cs`
- `src/Lfm.Core/Services/Progress/ProgressReporterFactory.cs`

**Key features:**
- Auto-detection of console vs. piped output
- Integration with existing `--verbose` flag
- Configurable progress styles (bar vs. counter)

### Phase 2: Date Range Integration
**Files to modify:**
- `src/Lfm.Core/Services/LastFmApiClient.cs`
  - `GetTopAlbumsForDateRangeAsync`
  - `GetTopTracksForDateRangeAsync` 
  - `GetTopArtistsForDateRangeAsync`

**Progress format:**
```
[████████░░] 82% Page 15 of 18 (12,847 tracks → 234 albums)
```

### Phase 3: Range Query Integration
**Files to modify:**
- `src/Lfm.Core/Services/LastFmService.cs`
  - `ExecuteRangeQueryAsync<T, TResponse>` method

**Progress format:**
```
[██████░░░░] 60% Loading page 12 of 20 (artists 501-600)
```

### Phase 4: Recommendations Integration
**Files to modify:**
- `src/Lfm.Core/Services/LastFmService.cs`
  - `GetMusicRecommendationsAsync`
  - `GetMusicRecommendationsForDateRangeAsync`

**Progress format:**
```
[███████░░░] 70% Analyzing artist 14 of 20 (Blur → finding similar)
```

### Phase 5: Deep Search Integration
**Files to modify:**
- `src/Lfm.Core/Services/LastFmService.cs`
  - `SearchUserTracksForArtistAsync`
  - `SearchUserAlbumsForArtistAsync`
  - `GetUserArtistPlayCountsAsync`

**Progress format:**
```
Searched 8,247 tracks, found 23 matches (continuing...)
```

## Configuration Integration

### Command Line Options
- `--progress` / `--no-progress` - Force enable/disable progress bars
- `--quiet` - Implies `--no-progress`
- Integration with existing `--verbose` flag for detailed progress

### Auto-Detection Logic
```csharp
// Disable progress bars when:
bool shouldShowProgress = !Console.IsOutputRedirected && 
                         !Console.IsErrorRedirected && 
                         !isQuietMode &&
                         estimatedDurationSeconds > 5;
```

## User Experience

### Before (Current)
```bash
$ lfm albums --year 2023
# 30+ seconds of silence...
# Results appear suddenly
```

### After (With Progress)
```bash
$ lfm albums --year 2023
[████████░░] 82% Page 15 of 18 (12,847 tracks → 234 albums)
# Real-time updates every 100ms
# User knows progress and can estimate completion
```

### Verbose Mode Enhancement
```bash
$ lfm albums --year 2023 --verbose
Getting albums for smarshal (2023-01-01 to 2023-12-31)...
[██░░░░░░░░] 18% Page 3 of 18 (2,234 tracks → 67 albums found)
  ↳ Cache miss: calling Last.fm API
  ↳ Processing 1,000 tracks from page 3
  ↳ Found 15 new albums: "The Ballad of Darren", "1989 (Taylor's Version)"...
[████░░░░░░] 35% Page 6 of 18 (5,891 tracks → 134 albums found)
  ↳ Cache hit: using cached data (saved 1.2s)
```

## Technical Considerations

### Performance Impact
- **Minimal**: Progress updates only occur at existing throttle points
- **No additional API calls**: Uses existing pagination data
- **Optional**: Can be completely disabled for automated scripts

### Console Detection
```csharp
public static bool ShouldShowProgress()
{
    // Don't show progress if output is redirected
    if (Console.IsOutputRedirected || Console.IsErrorRedirected)
        return false;
        
    // Don't show if running in CI/automated environment
    if (Environment.GetEnvironmentVariable("CI") != null)
        return false;
        
    // Check terminal capabilities
    return Environment.UserInteractive;
}
```

### Error Handling
- Progress bars should gracefully handle console resize
- Should clear progress line before displaying errors
- Must not interfere with normal output formatting

## Testing Strategy

### Unit Tests
- `NullProgressReporter` - verify all methods are no-ops
- `ConsoleProgressReporter` - verify output formatting (without actual console)
- Progress calculation logic

### Integration Tests  
- Test with actual long-running operations
- Verify progress accuracy against known totals
- Test console detection logic

### Manual Testing Scenarios
1. **Normal operation**: `albums --year 2023` shows progress
2. **Piped output**: `albums --year 2023 | grep "Taylor"` shows no progress
3. **Quiet mode**: `albums --year 2023 --quiet` shows no progress
4. **Verbose mode**: Enhanced progress with detailed information
5. **Range queries**: `artists --range 100-200` shows page progress
6. **Recommendations**: Shows artist-by-artist analysis progress

## Future Enhancements

### Estimated Time Remaining
Once we have timing data, could add ETA estimates:
```
[████████░░] 82% Page 15 of 18 (ETA: 4s remaining)
```

### Cancel Support
Could integrate with `CancellationToken` for user cancellation:
```
[████░░░░░░] 40% Press Ctrl+C to cancel...
```

### Progress Persistence
For very long operations, could save progress to allow resume:
```bash
$ lfm albums --year 2000-2023  # Very long operation
# Interrupted at 60%...
$ lfm albums --year 2000-2023 --resume
Resuming from 2015... [████████░░] 80%
```

## Effort Estimate

**Development Time**: 2-3 days
- **Phase 1** (Infrastructure): 4-6 hours
- **Phase 2** (Date Range): 2-3 hours  
- **Phase 3** (Range Queries): 1-2 hours
- **Phase 4** (Recommendations): 1-2 hours
- **Phase 5** (Deep Search): 2-3 hours
- **Testing & Polish**: 3-4 hours

**Complexity**: Medium
- **Easy**: Integration points already exist (throttle loops)
- **Medium**: Console detection and formatting logic
- **Easy**: Most business logic unchanged

**Value**: High
- Dramatically improves UX for long operations
- Makes the tool feel more professional and responsive
- Helps users understand what's happening during delays

## Dependencies

### Prerequisites
- ✅ **API Throttling**: Already implemented (provides integration points)
- ✅ **Consistent Loop Structure**: Throttled loops are uniform
- ✅ **Error Handling**: Existing error patterns can be preserved

### External Libraries
- **Option 1**: Pure .NET Console API (recommended)
- **Option 2**: Third-party progress bar library (e.g., ShellProgressBar)
- **Recommendation**: Start with pure .NET for simplicity

## Success Criteria

### Functional Requirements
- ✅ Progress bars appear for operations taking >5 seconds
- ✅ Accurate progress percentages where total is known
- ✅ Graceful degradation when total is unknown
- ✅ No progress bars in piped/automated scenarios
- ✅ Integration with existing verbose/quiet flags

### Performance Requirements  
- ✅ <10ms overhead per progress update
- ✅ No additional API calls required
- ✅ No impact on final operation results

### Usability Requirements
- ✅ Clear, readable progress format
- ✅ Informative details (pages, items processed)
- ✅ Consistent behavior across all operations
- ✅ Easy to disable when not wanted

---

## Conclusion

This progress bar implementation would significantly improve the user experience for the Last.fm CLI tool, particularly after the addition of API throttling which increased operation duration. The design leverages existing throttling infrastructure, making implementation straightforward while providing substantial value to users.

The phased approach allows for incremental delivery, with the highest-value date range operations tackled first. The auto-detection logic ensures progress bars enhance the experience without interfering with automated usage.