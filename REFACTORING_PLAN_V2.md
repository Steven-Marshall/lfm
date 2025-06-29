# Last.fm CLI Refactoring Plan V2

## Overview
This document outlines a comprehensive refactoring plan to eliminate code duplication, improve maintainability, and prepare for MCP server implementation while preserving the existing CLI interface.

## Current State Analysis

### Code Duplication Issues
- **Command Logic**: 80% overlap between commands (cache setup, username resolution, date parsing, range queries)
- **API Client Methods**: 300+ lines of nearly identical HTTP/JSON handling patterns
- **Display Logic**: Inconsistent formatting between commands
- **Error Handling**: Mixed patterns across the codebase

### Architectural Problems
- **Mixed Responsibilities**: Commands handle parsing + business logic + display + cache management
- **Overloaded BaseCommand**: Violates Single Responsibility Principle
- **Inconsistent Error Handling**: No centralized error classification
- **Performance Issues**: Unbounded cache growth, inefficient date aggregation

## Refactoring Priorities

### Priority 1: Extract Service Layer (CRITICAL)
**Goal**: Eliminate command duplication by extracting business logic

**Tasks**:
- [ ] Create `ILastFmService` interface
- [ ] Implement `LastFmService` with methods:
  - [ ] `GetRecommendationsAsync(RecommendationRequest)`
  - [ ] `GetTopArtistsAsync(TopArtistsRequest)`
  - [ ] `GetTopTracksAsync(TopTracksRequest)`
  - [ ] `GetTopAlbumsAsync(TopAlbumsRequest)`
  - [ ] `CreatePlaylistAsync(PlaylistRequest)`
- [ ] Create request/response models:
  - [ ] `RecommendationRequest/Result`
  - [ ] `TopArtistsRequest/Result`
  - [ ] `TopTracksRequest/Result`
  - [ ] `TopAlbumsRequest/Result`
  - [ ] `PlaylistRequest/Result`
- [ ] Refactor commands to use service layer (one by one):
  - [ ] `RecommendationsCommand`
  - [ ] `ArtistsCommand`
  - [ ] `TracksCommand`
  - [ ] `AlbumsCommand`
  - [ ] `ArtistTracksCommand`
  - [ ] `ArtistAlbumsCommand`

**Expected Outcome**: Commands reduced from 200-300 lines to 50 lines each

### Priority 2: Standardize API Client (HIGH)
**Goal**: Eliminate 80% of API method duplication

**Tasks**:
- [ ] Create generic `ApiRequest<T>` pattern
- [ ] Extract common HTTP/JSON logic to base methods
- [ ] Standardize error handling across all API methods
- [ ] Implement retry logic consistently
- [ ] Add request/response logging uniformly

**Current**: 6 API methods × 50 lines = 300 lines
**Target**: 6 API methods × 10 lines + 1 base method × 100 lines = 160 lines

### Priority 3: Centralize Display Logic (HIGH)
**Goal**: Consistent UX across all commands

**Tasks**:
- [ ] Audit all console output in commands
- [ ] Move all display logic to `IDisplayService`
- [ ] Standardize progress indicators
- [ ] Consistent symbol usage via `ISymbolProvider`
- [ ] Uniform error message formatting
- [ ] Standardize verbose logging patterns

**Methods to Add**:
- [ ] `DisplayRecommendations(RecommendationResult)`
- [ ] `DisplayPlaylist(PlaylistResult)`
- [ ] `DisplayProgress(string operation, int current, int total)`
- [ ] `DisplayError(ErrorResult error)`

### Priority 4: Improve Error Handling (MEDIUM)
**Goal**: Consistent, user-friendly error management

**Tasks**:
- [ ] Create `Result<T>` pattern to replace null returns
- [ ] Define error classification:
  - [ ] `ApiError` (network, rate limit, invalid key)
  - [ ] `ValidationError` (invalid parameters)
  - [ ] `DataError` (no results found)
- [ ] Centralize error message formatting
- [ ] Remove exception throwing from API layer
- [ ] Add retry logic for transient errors

**Models to Create**:
```csharp
public class Result<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public ErrorResult? Error { get; set; }
}

public class ErrorResult
{
    public ErrorType Type { get; set; }
    public string Message { get; set; }
    public string? TechnicalDetails { get; set; }
}
```

### Priority 5: Optimize Performance (MEDIUM)
**Goal**: Better memory usage and response times

**Tasks**:
- [ ] Implement streaming aggregation for date ranges
- [ ] Add early termination for common queries
- [ ] Optimize cache key generation (use simpler hash)
- [ ] Implement proper cache invalidation strategy
- [ ] Add configurable parallelism settings
- [ ] Memory profiling and optimization

**Targets**:
- Date range queries: Use streaming instead of loading all pages
- Cache keys: Use MD5 instead of SHA256
- Memory usage: Cap at reasonable limits

### Priority 6: Strengthen Data Models (LOW)
**Goal**: Type safety and validation

**Tasks**:
- [ ] Convert string properties to appropriate types:
  - [ ] `PlayCount` → `int`
  - [ ] `Rank` → `int`
  - [ ] `Match` → `decimal`
- [ ] Add model validation
- [ ] Consistent property naming
- [ ] Required field enforcement

## Implementation Strategy

### Phase 1: Foundation (Priority 1-2)
**Duration**: 1-2 weeks
**Focus**: Service layer + API standardization
**Deliverable**: Clean separation of business logic from CLI concerns

### Phase 2: User Experience (Priority 3-4)
**Duration**: 1 week  
**Focus**: Consistent display + error handling
**Deliverable**: Uniform UX across all commands

### Phase 3: Optimization (Priority 5-6)
**Duration**: 1 week
**Focus**: Performance + data models
**Deliverable**: Production-ready codebase

## MCP Preparation Benefits

After this refactoring:

1. **Service Layer → MCP Tools**: Direct mapping from `ILastFmService` methods to MCP tools
2. **Request/Response Models**: Perfect for JSON serialization in MCP
3. **Error Handling**: `Result<T>` pattern translates perfectly to MCP responses
4. **No Business Logic in Commands**: MCP tools become thin wrappers around service layer

## Testing Strategy

### Unit Tests to Add
- [ ] `LastFmService` business logic tests
- [ ] `DateRangeParser` validation tests
- [ ] `CacheKeyGenerator` collision tests
- [ ] Error handling scenarios

### Integration Tests to Add
- [ ] End-to-end CLI command tests
- [ ] Cache behavior validation
- [ ] API client resilience tests

## Success Metrics

### Code Quality
- **Before**: 2000+ lines in commands
- **After**: <800 lines in commands
- **Before**: 6 methods × 50 lines API duplication
- **After**: 6 methods × 10 lines + shared base

### Maintainability
- **Adding new command**: 200-300 lines → 50 lines
- **Adding new API method**: 50 lines → 10 lines
- **Fixing bugs**: Single location instead of multiple

### User Experience
- **Consistent formatting**: All commands use same display patterns
- **Better error messages**: User-friendly with technical details available
- **Reliable caching**: Proper invalidation and size management

## Risk Mitigation

### Backward Compatibility
- **CLI Interface**: No changes to command signatures or behavior
- **Configuration**: Existing config files continue to work
- **Caching**: Existing cache remains valid

### Rollback Plan
- **Git Tags**: Tag before each major refactoring phase
- **Feature Flags**: Keep old code paths during transition
- **Incremental Migration**: Refactor one command at a time

## Dependencies

### External Libraries
- No new dependencies required
- Consider adding for future:
  - `FluentValidation` for model validation
  - `Polly` for retry policies

### Internal Dependencies
- All refactoring uses existing `Lfm.Core` infrastructure
- Builds on current caching, configuration, and API client foundation

## Deliverables

### Phase 1
- [ ] `Lfm.Core/Services/ILastFmService.cs`
- [ ] `Lfm.Core/Services/LastFmService.cs`
- [ ] `Lfm.Core/Models/Requests/` (5 request models)
- [ ] `Lfm.Core/Models/Results/` (5 result models)
- [ ] Refactored commands (6 commands)

### Phase 2
- [ ] Enhanced `IDisplayService` with all display methods
- [ ] `Lfm.Core/Models/ErrorResult.cs`
- [ ] `Lfm.Core/Models/Result.cs`
- [ ] Consistent error handling across all commands

### Phase 3
- [ ] Optimized `LastFmApiClient` with streaming
- [ ] Improved cache management
- [ ] Type-safe data models
- [ ] Performance benchmarks

## Next Steps

1. **Commit Current State**: Ensure all changes are committed to git
2. **Create Feature Branch**: `git checkout -b refactor/service-layer`
3. **Start Phase 1**: Begin with `ILastFmService` interface design
4. **Incremental Progress**: One command at a time to minimize risk

---

*This plan maintains full backward compatibility while dramatically improving code quality and preparing for MCP implementation.*