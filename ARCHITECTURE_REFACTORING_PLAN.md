# Architecture Refactoring Plan - Output Layer Separation

**Date**: 2025-09-30
**Status**: Proposal - Awaiting Decision
**Priority**: Medium (Technical Debt, Not Blocking)

## Executive Summary

The project has a **layering violation** where `Lfm.Core` (business logic) directly writes to Console via `DisplayService.cs`. This happened naturally as the tool evolved from CLI-only → CLI + JSON → CLI + MCP integration.

**Current State**: ✅ Working well, 80% feature complete
**Issue**: Core layer coupled to Console output (61 Console.WriteLine calls)
**Impact**: Medium - affects testability and future extensibility, but not blocking current functionality

## Project Context & Goals

### Original Vision
- Quick command-line stats viewer for developers
- Fast access to Last.fm data without web browser

### Current Reality
- CLI tool ✅ (Working great)
- MCP integration ✅ (Works beautifully with LLM orchestration)
- JSON output for MCP ✅ (CLI commands have `--json` flag)

### Future Possibilities
- **Near term**: Voice interface via iPhone app → LLM → MCP → Spotify playlist creation
- **Maybe someday**: Web UI for users to safely connect Last.fm + Spotify accounts
- **Note**: Not planning to replace Last.fm web interface itself

### Key Constraint
This is a **hobbyist tool** - not in a critical state, working well for current needs.

## Technical Analysis

### The Problem in Detail

**Current Architecture**:
```
User → CLI Command → Core Services → DisplayService (Console.WriteLine) → Terminal
                   ↓
                   → JSON serialization in Command → MCP
```

**Layering Violation**:
- `src/Lfm.Core/Services/DisplayService.cs` contains 61 `Console.WriteLine` calls
- Core layer should not know about presentation
- Makes Core untestable without console output
- Prevents Core reuse in non-console contexts

**Where It Happens**:
```
src/Lfm.Core/Services/DisplayService.cs (145 lines)
├─ DisplayArtists() - Console formatting
├─ DisplayTracks() - Console formatting
├─ DisplayAlbums() - Console formatting
└─ Used by: TracksCommand, ArtistsCommand, AlbumsCommand, RecommendationsCommand
```

**Why This Happened**:
1. Started as CLI tool → DisplayService made sense in Core
2. Added MCP → Added `if (json)` branches to commands
3. CLI orchestration logic (year → date range, cache flags) stayed in commands
4. Now: CLI is the natural interface layer, but Core still has display code

### Important Architectural Note

**CLI Commands ≠ Core Functions** (This is actually good!)

CLI does orchestration:
```
lfm tracks --year 2023 --limit 20 --force-api

↓ CLI Command Layer:
  - Parses --year 2023 → DateTime(2023-01-01) to DateTime(2023-12-31)
  - Validates parameters
  - Decides which Core API to call (period vs date range)
  - Handles cache behavior flags
  - Chooses output format (console vs JSON)

↓ Core Layer:
  - GetTopTracksForDateRangeAsync(username, from, to, limit)
  - Makes Last.fm API calls
  - Aggregates data
  - Returns TopTracks model

↓ CLI Command Layer:
  - Formats output (console or JSON)
  - Handles errors
```

**Implication**: MCP calling CLI commands makes perfect sense - the orchestration logic lives there.

## Proposed Solutions

### Option 1: CLI-Focused Output Abstraction ⭐ RECOMMENDED

**Approach**: Move DisplayService to CLI layer, create output writer abstraction within CLI only.

**Architecture**:
```
src/Lfm.Cli/Services/ICommandOutputWriter.cs    (NEW)
├─ WriteArtists(TopArtists, startRank)
├─ WriteTracks(TopTracks, startRank)
├─ WriteAlbums(TopAlbums, startRank)
├─ WriteError(message)
└─ WriteSuccess(message)

src/Lfm.Cli/Services/ConsoleOutputWriter.cs     (NEW)
└─ Implements ICommandOutputWriter with Console.WriteLine formatting

src/Lfm.Cli/Services/JsonOutputWriter.cs        (NEW)
└─ Implements ICommandOutputWriter with JSON serialization

src/Lfm.Core/Services/DisplayService.cs         (MOVE TO CLI)
└─ Move entire file to src/Lfm.Cli/Services/DisplayService.cs
   (Or delete if logic moves to OutputWriters)
```

**Command Pattern**:
```csharp
public class ArtistsCommand : BaseCommand
{
    private readonly ILastFmApiClient _apiClient;
    private readonly ICommandOutputWriter _consoleWriter;
    private readonly ICommandOutputWriter _jsonWriter;

    public async Task ExecuteAsync(..., bool json = false)
    {
        // Select writer based on output format
        var writer = json ? _jsonWriter : _consoleWriter;

        // Fetch data from Core
        var result = await _apiClient.GetTopArtistsAsync(...);

        // Output via writer (no more if/else branches!)
        if (result.Success)
            writer.WriteArtists(result.Data);
        else
            writer.WriteError(result.Error.Message);
    }
}
```

**Changes Required**:
1. Create `ICommandOutputWriter` interface (1 file, ~30 lines)
2. Create `ConsoleOutputWriter` implementation (1 file, ~150 lines)
3. Create `JsonOutputWriter` implementation (1 file, ~150 lines)
4. Move `DisplayService.cs` from Core to CLI (or integrate into writers)
5. Update DI registration in `Program.cs` (~10 lines)
6. Update all commands to use writers (~17 command files)
   - Replace `if (json)` branches with writer selection
   - Replace `_displayService.DisplayXxx()` with `writer.WriteXxx()`

**Affected Commands** (17 files):
- TracksCommand.cs
- ArtistsCommand.cs
- AlbumsCommand.cs
- RecommendationsCommand.cs
- TopTracksCommand.cs
- MixtapeCommand.cs
- ArtistSearchCommand.cs
- SimilarCommand.cs
- CheckCommand.cs
- CreatePlaylistCommand.cs
- ApiStatusCommand.cs
- CacheStatusCommand.cs
- CacheClearCommand.cs
- ConfigCommand.cs
- SpotifyCommand.cs
- BenchmarkCacheCommand.cs
- TestCacheCommand.cs

**Effort Estimate**: 3-4 hours
- Interface design: 30 mins
- ConsoleOutputWriter: 1 hour
- JsonOutputWriter: 1 hour
- Command updates: 1.5-2 hours
- Testing: 30 mins

**Benefits**:
✅ Fixes layering violation (Core no longer has Console dependency)
✅ Removes all `if (json)` branches from commands
✅ Makes commands cleaner and more testable
✅ Easier to add new output formats (XML, CSV, etc.)
✅ Embraces CLI-as-interface reality
✅ Minimal disruption to working code

**Limitations**:
⚠️ Core still not directly usable by hypothetical Web UI
⚠️ Web UI would still call CLI commands (like MCP does)
⚠️ Defers "make Core fully reusable" decision until actually needed

---

### Option 2: Full Core Output Abstraction

**Approach**: Create output abstraction in Core, inject implementation from CLI.

**Architecture**:
```
src/Lfm.Core/Services/IOutputWriter.cs          (NEW - Core)
├─ WriteLine(string)
├─ WriteTable(headers, rows, columnWidths)
└─ WriteJson(object)

src/Lfm.Cli/Services/ConsoleOutputWriter.cs    (NEW - CLI)
└─ Implements IOutputWriter with Console.WriteLine

src/Lfm.Cli/Services/JsonOutputWriter.cs       (NEW - CLI)
└─ Implements IOutputWriter with JSON to Console

src/Lfm.Core/Services/DisplayService.cs        (REFACTOR)
└─ Takes IOutputWriter in constructor
   Uses _output.WriteLine() instead of Console.WriteLine()
```

**Pattern**:
```csharp
// Core Layer
public class DisplayService
{
    private readonly IOutputWriter _output;

    public DisplayService(IOutputWriter output)
    {
        _output = output;
    }

    public void DisplayArtists(TopArtists artists)
    {
        _output.WriteLine();
        _output.WriteTable(
            headers: new[] { "Rank", "Artist", "Plays" },
            rows: PrepareArtistRows(artists),
            widths: new[] { 4, 40, 10 }
        );
    }
}

// CLI Layer
services.AddTransient<IOutputWriter, ConsoleOutputWriter>();
services.AddTransient<DisplayService>();
```

**Effort Estimate**: 6-8 hours
- Interface design: 1 hour
- Output writer implementations: 2 hours
- DisplayService refactor: 2 hours
- Command updates: 2 hours
- Testing: 1 hour

**Benefits**:
✅ Core becomes truly reusable (no Console dependency)
✅ Web UI could use Core services directly
✅ Better testability with mock writers
✅ Clean separation of concerns

**Limitations**:
⚠️ More complex (Core needs to know about output abstraction)
⚠️ More work for uncertain future benefit
⚠️ May be over-engineering if Web UI uses CLI anyway (like MCP does)

---

### Option 3: Do Nothing (Keep Current State)

**Approach**: Accept current architecture as-is.

**Rationale**:
- ✅ Working well for current needs (CLI + MCP)
- ✅ 80% feature complete
- ✅ Not blocking any current functionality
- ✅ MCP integration is excellent
- ✅ Can refactor later if Web UI becomes real requirement

**Technical Debt**:
- ⚠️ Core layer has Console dependency (testability issue)
- ⚠️ Commands have `if (json)` branches (maintainability)
- ⚠️ DisplayService in wrong layer (architectural purity)

**When to Revisit**:
- When building Web UI becomes concrete plan
- When adding 5+ new output formats
- When Console coupling causes actual pain
- When writing unit tests for Core becomes priority

---

## Decision Matrix

| Aspect | Option 1: CLI-Focused | Option 2: Core Abstraction | Option 3: Do Nothing |
|--------|----------------------|---------------------------|---------------------|
| **Effort** | 3-4 hours | 6-8 hours | 0 hours |
| **Core Purity** | Partial fix | Complete fix | No change |
| **CLI Cleanliness** | ✅ Much better | ✅ Better | ⚠️ Current state |
| **Web UI Ready** | ⚠️ Via CLI | ✅ Direct Core usage | ❌ Not ready |
| **Risk** | 🟢 Low | 🟡 Medium | 🟢 None |
| **Future Proof** | 🟡 Good enough | ✅ Very good | ⚠️ Technical debt |
| **MCP Impact** | 🟢 None (better) | 🟢 None | 🟢 None |

## Recommendation

**Primary**: **Option 1** (CLI-Focused Output Abstraction)

**Reasoning**:
1. Fixes the immediate architectural issue (Console in Core)
2. Makes commands cleaner (removes `if (json)` branches)
3. Minimal effort (3-4 hours) for significant benefit
4. Embraces reality: CLI is the natural interface layer
5. MCP → CLI pattern is working beautifully
6. Doesn't over-engineer for uncertain Web UI future
7. Can still refactor to Option 2 later if needed

**Secondary**: **Option 3** (Do Nothing)

**If**:
- Focusing on new features is higher priority
- Web UI plans solidify → then do Option 2
- Technical debt isn't causing actual pain yet

**Not Recommended**: **Option 2** at this time
- More work for speculative benefit
- Would only be needed if Web UI calls Core directly
- But Web UI would likely call CLI anyway (proven MCP pattern)

## Implementation Plan (If Choosing Option 1)

### Phase 1: Create Abstractions (1 hour)
1. Create `src/Lfm.Cli/Services/ICommandOutputWriter.cs`
   - Methods: WriteArtists, WriteTracks, WriteAlbums, WriteError, WriteSuccess
2. Create `src/Lfm.Cli/Services/ConsoleOutputWriter.cs`
   - Move formatting logic from DisplayService
3. Create `src/Lfm.Cli/Services/JsonOutputWriter.cs`
   - Extract JSON logic from commands

### Phase 2: Update Infrastructure (30 mins)
4. Move `DisplayService.cs` from Core to CLI (or delete if absorbed into writers)
5. Update `Program.cs` DI registration
   ```csharp
   services.AddTransient<ConsoleOutputWriter>();
   services.AddTransient<JsonOutputWriter>();
   ```

### Phase 3: Update Commands (2 hours)
6. Update each command to use output writers
   - Replace `if (json) { ... } else { ... }` with writer selection
   - Replace DisplayService calls with writer calls
7. Priority order:
   - High usage: TracksCommand, ArtistsCommand, AlbumsCommand (start here)
   - Medium: RecommendationsCommand, TopTracksCommand, CheckCommand
   - Low: Config, Cache, Test commands (do last)

### Phase 4: Testing (30 mins)
8. Test console output: `./publish/win-x64/lfm.exe tracks --limit 5`
9. Test JSON output: `./publish/win-x64/lfm.exe tracks --limit 5 --json`
10. Test MCP integration: Verify Claudette can still use tools
11. Build and verify: `dotnet build -c Release`

### Phase 5: Commit (15 mins)
12. Git commit: "refactor: Separate output concerns from Core to CLI layer"
13. Version bump: 1.4.1 (patch - architectural cleanup)

## Testing Checklist

After refactoring, verify:
- [ ] Console output still looks correct (tables, formatting)
- [ ] JSON output is valid and parseable
- [ ] MCP tools still work via Claudette
- [ ] All commands work with both `--json` and default output
- [ ] Error messages display properly in both formats
- [ ] Cache status, config display work correctly
- [ ] Build succeeds with 0 warnings

## Related Technical Debt

Other items identified in code review:
1. **ConfigCommand.cs is huge** (849 lines, 25 methods) - Consider splitting
2. **LastFmApiClient.cs is large** (1,012 lines) - Consider extracting interface
3. **MCP server.js is repetitive** (1,476 lines) - Could use config-driven approach
4. **Parameter list explosion** - Some commands have 15+ parameters

These are **lower priority** and can be addressed separately.

## Future Considerations

### If Building Web UI
- **Option A**: Web UI spawns CLI commands (like MCP does) → No further refactoring needed
- **Option B**: Web UI uses Core directly → Would need Option 2 refactor then
- **Option C**: Web UI calls REST API → API layer spawns CLI or uses Core

### If Adding More Output Formats
- CSV export: Add `CsvOutputWriter` implementation
- HTML reports: Add `HtmlOutputWriter` implementation
- XML: Add `XmlOutputWriter` implementation
- All trivial additions with Option 1 architecture

### If Adding Voice Interface (iPhone App → LLM → MCP)
- No changes needed - MCP → CLI pattern already proven
- Just need MCP server accessible to phone app
- CLI orchestration logic is perfect for LLM-mediated calls

## References

- Current architecture: `src/Lfm.Core/Services/DisplayService.cs`
- MCP integration: `lfm-mcp-release/server.js`
- Commands affected: `src/Lfm.Cli/Commands/*.cs` (17 files)
- Related discussion: CLAUDE.md "Debugging Lessons Learned"

## Decision

**Status**: ⏸️ Awaiting decision after overnight consideration

**Options**:
1. ✅ Proceed with Option 1 (CLI-focused refactor)
2. ⏭️ Defer to later (Option 3 - focus on album check feature) ← **PRIORITIZING THIS**
3. 🎯 Go all-in with Option 2 (Core abstraction)

**Next Steps**:
- Sleep on it
- Decide based on priority: New features vs. architectural cleanup
- Remember: Not in critical state, working well currently

---

## 2025-09-30 Update

**Decision**: Defer architectural refactor, prioritize **Album Check Feature** with full track-level analysis.

**Rationale**:
- Output refactoring is 3-6 hours of architectural cleanup
- Album check is new feature with immediate value
- Not in critical architectural state
- Can revisit output abstraction later if needed

**Next Session**: Implement full album check with track breakdown (see ALBUM_CHECK_FEATURE_PLAN.md)