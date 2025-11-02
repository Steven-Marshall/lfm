# Context Window Considerations for LFM MCP Server

## Overview

This document captures lessons learned about context window management, guidelines distribution, and the challenges of maintaining persistent guidance across long LLM sessions.

**Date:** 2025-11-02
**Context:** Session work on SSE/MCPO transport implementation revealed critical context flooding issues

---

## The Problem We Discovered

### Initial Issue: Guidelines Flooding Context Window

**Symptoms:**
- Open WebUI with 64K context model started "speaking Spanish" mid-conversation
- Model behavior became erratic and confused
- Responses became wobbly and unreliable

**Root Cause:**
- `lfm_init` tool returns **~23,000 tokens** of full guidelines
- Open WebUI called `lfm_init` **3 times** in rapid succession
- Result: **69,000 tokens** of identical guidelines flooded context window
- Actual conversation squeezed out, model degraded

### Why This Matters Even for Large Context Models

Even with 200K context (Claude Code):
- Guidelines: 23K tokens
- Code files: 50K tokens
- Conversation history: 30K tokens
- Tool results: 20K tokens
- **Total: 123K used** (only 77K remaining for actual work)

**Key Insight:** Context is precious even in large windows. Repeated content is wasteful.

---

## How Context Windows Work

### Basic Strategies

**Simple FIFO (First In, First Out):**
```
[System prompt] [Message 1] [Message 2] [Message 3] ... [Message N]
                 ↑ Oldest gets dropped first when full
```

**Smart Truncation (Modern Systems):**
- **System prompt:** Always retained (pinned)
- **Recent messages:** Last ~5-10 exchanges always kept
- **Middle messages:** Dropped when space needed
- **Tool results:** Often compressed or summarized

### What Gets Priority

**High Priority (Kept):**
1. System instructions/prompts
2. Recent conversation (last few exchanges)
3. Tool definitions/schemas
4. Currently relevant code context

**Low Priority (Dropped First):**
1. Old tool results (data)
2. Middle conversation history
3. Repeated/duplicate content
4. Large blocks of reference material

### Critical Distinction: System Prompt vs Tool Results

**System Prompts:**
- ✅ Pinned at start of context
- ✅ Never dropped
- ✅ Always available to model
- ✅ Treated as "instructions"

**Tool Results (including our guidelines):**
- ⚠️ Can be dropped when context fills
- ⚠️ Often first to go in smart truncation
- ⚠️ Treated as "data" not "instructions"
- ⚠️ May be lost in long sessions

**This is why loading 23K tokens via `lfm_init` tool result is risky!**

---

## Transport-Specific Behavior

### Stateful vs Stateless Transports

**Stateful (Stdio, SSE):**
- Single long-lived Node.js process per session
- `sessionState = { initialized: false }` persists in memory
- Deduplication works: 2nd+ `lfm_init` calls return brief message
- **Result:** Guidelines loaded once (~23K), subsequent calls ~10 tokens ✅

**Stateless (MCPO/OpenAPI):**
- Fresh Node.js process spawned per request
- `sessionState` resets every call
- Deduplication fails: Every `lfm_init` returns full guidelines
- **Result:** 3 calls = 69K tokens of identical content ❌

### Implementation Status

**Master Branch (`server.js`):**
```javascript
// Deduplication check added (Commit: de6f0fe)
if (sessionState.initialized) {
  return {
    content: [{
      type: 'text',
      text: '✅ Already initialized. Guidelines are active and ready to use.'
    }]
  };
}
sessionState.initialized = true;
// Return full guidelines...
```

**Feature Branch (`server-core.js`):**
- Same fix applied with detailed comments about limitations
- Explains stateful vs stateless behavior
- Documented MCPO workarounds (disable tool or implement shared state)

---

## Solutions & Recommendations

### ✅ Implemented: Deduplication for Stateful Transports

**What we did:**
- Added check to prevent repeated guidelines dumps in same session
- First call: Full 23K tokens
- Subsequent calls: Brief 10-token confirmation
- **Savings:** ~46K tokens per session (2 extra calls avoided)

**Effectiveness:**
- ✅ **Stdio (Claude Desktop/Code):** Tested and working perfectly
- ✅ **SSE:** Should work (same stateful architecture)
- ❌ **MCPO:** Doesn't work (stateless, needs different approach)

**For MCPO users:** Disable/hide `lfm_init` tool in LLM interface since brief guidelines auto-load anyway.

### 🎯 Recommended: Tool Description Enrichment

**Problem:**
- We can't control context retention from MCP server
- No way to mark content as "keep this over other data"
- Tool results (even guidelines) can be dropped in long sessions

**Solution: Embed Critical Rules in Tool Descriptions**

Tool descriptions are part of the MCP schema and likely persist longer than tool results.

**Current (minimal):**
```javascript
{
  name: 'lfm_tracks',
  description: 'Get top tracks from Last.fm',
  inputSchema: { /* ... */ }
}
```

**Proposed (enriched):**
```javascript
{
  name: 'lfm_tracks',
  description: `Get user's top tracks from Last.fm.

  TEMPORAL PARAMETERS:
  - Use year="2025" when user mentions specific year (NOT period)
  - Use period="1month" for "recently"/"lately"
  - Use period="7day" for "this week"
  - Use period="overall" for all-time stats

  RESPONSE STYLE:
  - Be concise (1-2 sentences max)
  - Answer directly with one interesting detail
  - Avoid track-by-track breakdowns unless requested

  Returns tracks with play counts and rankings.`,
  inputSchema: { /* ... */ }
}
```

**Benefits:**
- ✅ Rules persist (part of tool schema, not conversation)
- ✅ Contextual (appear when tool is relevant)
- ✅ No token waste in conversation history
- ✅ Each tool carries its own "mini-guidelines"
- ✅ Works for all transports (stdio, SSE, MCPO)

### Current Architecture (Layered Approach)

**1. Brief Auto-Guidelines (~200 tokens):**
- Auto-prepended to first tool response
- Quick reference for temporal parameters and common workflows
- Small enough to persist longer in context

**2. Full Guidelines (~23,000 tokens):**
- Available via explicit `lfm_init` call
- Comprehensive reference for edge cases
- Can be dropped later, user can re-call if needed

**3. Tool-Specific Context (Future Enhancement):**
- Embed critical rules in individual tool descriptions
- Persistent and contextually relevant
- No additional token cost in conversation

---

## Technical Details

### sessionState Scope and Limitations

```javascript
// Session state tracking (persists for duration of server process)
//
// IMPORTANT: This state is per-process and only works for STATEFUL transports:
// ✅ Stdio (Claude Desktop/Code) - One long-lived Node.js process per session
// ✅ SSE (HTTP transport) - One long-lived process with persistent connections
// ❌ MCPO/OpenAPI - May spawn new process per request (stateless REST)
//
// For MCPO, consider disabling lfm_init tool or implementing shared state (Redis/file)
const sessionState = {
  initialized: false
};
```

**Why this matters:**
- Node.js process lifecycle = state lifecycle
- Stateful transports: Process lives for entire session
- Stateless transports: New process per request = fresh state

### Token Counts

**Guidelines File (`lfm-guidelines.md`):**
- Lines: 775
- Words: 4,514
- Characters: 29,949
- **Estimated tokens:** ~23,000 (at ~1.3 chars/token)

**Brief Guidelines (auto-prepended):**
- **Estimated tokens:** ~200
- Temporal parameter quick reference
- Discovery workflow tips
- Pointer to full guidelines

**Deduplication Savings:**
- Before: 3 calls × 23K = 69K tokens
- After: 1 × 23K + 2 × 10 = 23,020 tokens
- **Savings:** ~46K tokens (~70% reduction)

---

## Future Considerations

### Option: MCP Prompts Feature

MCP supports a `prompts` feature (currently unused):

```javascript
server.setRequestHandler(ListPromptsRequestSchema, async () => ({
  prompts: [
    {
      name: "lfm-guidelines",
      description: "Last.fm usage guidelines for music queries",
      arguments: []
    }
  ]
}));
```

**Potential benefit:** Claude Code *might* treat prompts differently than tool results.
**Status:** Unproven - need to test if prompts have better context persistence.

### Option: Shared State for MCPO

For stateless transports, implement persistent state storage:

**Approaches:**
1. **Redis:** Shared in-memory state across requests
2. **File-based cache:** Simple JSON file tracking init state per session
3. **Database:** Overkill but possible for multi-user scenarios

**Trade-offs:**
- Adds complexity and dependencies
- Requires session tracking mechanism
- May not be worth it vs simply disabling `lfm_init` in MCPO

### Option: Dynamic Guidelines Loading

Instead of all-or-nothing:

**Smart loading based on tool usage:**
- User calls `lfm_tracks`? Include temporal parameter rules
- User calls `lfm_recommendations`? Include discovery workflow tips
- User calls `lfm_play_now`? Include playback state awareness rules

**Benefits:**
- Just-in-time guidance (only what's needed)
- Smaller per-request token cost
- More resistant to context eviction

**Challenges:**
- More complex implementation
- May miss cross-cutting concerns
- Requires careful rule categorization

---

## Testing Notes

### Verified Working (Stdio Transport)

**Test Environment:** Claude Desktop (Windows)
**Date:** 2025-11-02

**Test procedure:**
1. Fresh conversation with LFM MCP server
2. Call `lfm_init` (should return full guidelines)
3. Call `lfm_init` again (should return brief confirmation)

**Results:**
- ✅ First call: Full guidelines (~33K tokens including success message)
- ✅ Second call: "✅ Already initialized. Guidelines are active and ready to use." (~10 tokens)
- ✅ No context flooding
- ✅ No Spanish mode or erratic behavior
- ✅ Model stayed consistent throughout conversation

**Conclusion:** Deduplication fix works perfectly for stateful transports.

### Known Issues (MCPO Transport)

**Test Environment:** Open WebUI on laptop, MCPO server on Docker
**Model:** gpt-oss:20b (64K context)

**Observed behavior:**
- ❌ Multiple `lfm_init` calls each return full 23K tokens
- ❌ Context window fills rapidly
- ❌ Model behavior degrades (Spanish mode observed)
- ❌ Deduplication check ineffective (stateless spawning)

**Workaround:** Hide/disable `lfm_init` tool in Open WebUI interface. Brief guidelines auto-load anyway.

---

## Key Takeaways

1. **Context is precious** - Even 200K contexts can fill quickly with repeated large payloads

2. **Tool results are ephemeral** - Guidelines in tool results can be dropped; system prompts cannot

3. **Transport matters** - Stateful (stdio/SSE) vs stateless (MCPO) fundamentally changes behavior

4. **Deduplication works (with caveats)** - Perfect for stdio/SSE, ineffective for MCPO

5. **Embed critical rules in schemas** - Tool descriptions persist better than tool results

6. **Layer your guidance** - Brief auto-load + full on-demand + tool-specific context

7. **Test in production conditions** - Context issues only appear in real usage patterns

---

## Related Files

- `lfm-mcp-release/server.js` (master) - Monolithic server with deduplication fix
- `lfm-mcp-release/server-core.js` (feature branch) - Refactored shared logic with fix
- `lfm-mcp-release/lfm-guidelines.md` - Full guidelines file (775 lines, ~23K tokens)
- `CLAUDE.md` - Session notes documenting discovery and implementation

## Commits

- `de6f0fe` (master) - "fix: Prevent lfm_init from flooding context on repeated calls"
- `bf8af4c` (feature/sse-transport) - "docs: Add comments explaining sessionState limitations"
