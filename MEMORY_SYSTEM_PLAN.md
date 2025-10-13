# LFM Memory System - Planning Document

**Status:** Planning Phase
**Created:** October 13, 2025
**Goal:** Championship Vinyl-style memory across sessions

---

## 🎯 Vision & Philosophy

### The Championship Vinyl Experience

We're building digital simulation of the High Fidelity record store experience:
- Expert remembers past conversations naturally
- Follows up on recommendations with data validation
- Knows what you discussed last week
- Makes connections across your taste
- **Memory with fallibility** - like human memory, not perfect database recall

**Key Principle:** "Music buddy not database"
- Recent stuff is clear (1 week)
- Older stuff gets fuzzy (2-4 weeks)
- Ancient stuff is just the gist (2+ months)
- Hard facts never fade (Last.fm data), soft context degrades naturally

### Why Memory Matters

**Problem:** LLMs are great at reasoning but forget context between sessions
- Can't follow up on recommendations
- Repeats same suggestions
- No persistent taste understanding
- Misses obvious connections

**Solution:** Queryable memory system that enables:
- "I recommended X last week, you played it 5 times! Great call"
- "Still haven't touched that Zombies album..."
- "We discussed your love for baroque pop - here's more"
- Temporal awareness (don't check 45-min album after 10 minutes)

---

## 🏗️ Architecture Decision

### Technology: SQLite + FTS5

**Why SQLite over JSON:**
- Fuzzy search is critical (artist name variations, typos)
- FTS5 (Full-Text Search) handles fuzzy matching automatically
- Proper indexing for complex queries
- Still just a single file (no server, no complexity)
- Scales to 10,000+ entries with consistent 5-20ms queries

**Performance:**
```
1,000 entries:
- JSON fuzzy search: 100-200ms
- SQLite FTS5: 5-10ms

5,000 entries:
- JSON: 500ms-2000ms (painful)
- SQLite: 10-30ms (still fast)
```

**Storage Location:**
```
~/.lfm-memory/
└── conversations.db    (single SQLite file)
```

### Code Organization

**Keep memory code completely separate:**
```
lfm-mcp/
├── tools/              (existing Last.fm tools)
├── memory/             (NEW - isolated)
│   ├── storage.js      (SQLite operations)
│   ├── schema.sql      (database schema)
│   └── queries.js      (search logic)
└── server.js           (exposes memory tools via MCP)
```

**Integration:** Memory tools exposed via MCP alongside existing Last.fm tools, but codebase is isolated

---

## 💾 Storage Schema

### SQLite Tables

```sql
CREATE TABLE memories (
    id TEXT PRIMARY KEY,
    timestamp DATETIME NOT NULL,
    type TEXT NOT NULL,  -- recommendation, insight, event, feedback, discussion
    entities TEXT NOT NULL,  -- JSON array: ["Artist", "Album"]
    summary TEXT NOT NULL,
    importance INTEGER DEFAULT 5,
    metadata TEXT,  -- JSON blob for flexible data
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Indexes for fast queries
CREATE INDEX idx_timestamp ON memories(timestamp);
CREATE INDEX idx_type ON memories(type);
CREATE INDEX idx_importance ON memories(importance);

-- Full-text search on entities and summary
CREATE VIRTUAL TABLE memories_fts USING fts5(
    id UNINDEXED,
    entities,
    summary,
    content='memories'
);

-- Keep FTS in sync
CREATE TRIGGER memories_after_insert AFTER INSERT ON memories BEGIN
    INSERT INTO memories_fts(id, entities, summary)
    VALUES (new.id, new.entities, new.summary);
END;
```

### Query Examples

```sql
-- Fuzzy artist search (handles "zombies", "The Zombies", "zombie")
SELECT * FROM memories
WHERE id IN (
    SELECT id FROM memories_fts
    WHERE entities MATCH 'zombies'
)
ORDER BY timestamp DESC LIMIT 5;

-- Recent recommendations
SELECT * FROM memories
WHERE type = 'recommendation'
  AND timestamp > datetime('now', '-7 days')
ORDER BY importance DESC;

-- Everything about artist in last month
SELECT * FROM memories
WHERE id IN (
    SELECT id FROM memories_fts
    WHERE entities MATCH 'beatles'
)
AND timestamp > datetime('now', '-30 days');
```

---

## 📝 Memory Types

### 1. Recommendation
Tracks recommendations made to validate with play counts later.

```json
{
  "type": "recommendation",
  "entities": ["The Zombies", "Odessey and Oracle"],
  "summary": "Recommended baroque pop album after user expressed love for complex arrangements",
  "importance": 8,
  "metadata": {
    "recommended_at": "2025-10-13",
    "check_after_days": 7,
    "reason": "Baroque pop production quality"
  }
}
```

### 2. Insight
Taste preferences and pattern understanding.

```json
{
  "type": "insight",
  "entities": ["storytelling", "lyrics", "Taylor Swift"],
  "summary": "User deeply values narrative songwriting; mentioned folklore as exemplar",
  "importance": 9,
  "metadata": {
    "context": "Discussion about Taylor Swift production",
    "tags": ["preference", "storytelling"]
  }
}
```

### 3. Event
Concerts, plans, temporal context.

```json
{
  "type": "event",
  "entities": ["The National", "concert"],
  "summary": "Seeing The National live November 2025, excited about Boxer deep cuts",
  "importance": 7,
  "metadata": {
    "date": "2025-11-15",
    "event_type": "concert"
  }
}
```

### 4. Feedback
Reactions to recommendations (especially negative - learn from mistakes).

```json
{
  "type": "feedback",
  "entities": ["Tame Impala", "Currents"],
  "summary": "Too psychedelic - user prefers melodic over experimental",
  "importance": 9,
  "metadata": {
    "sentiment": "negative",
    "original_rec_id": "uuid-123",
    "reason": "Too experimental"
  }
}
```

### 5. Discussion
Deep conversations worth remembering.

```json
{
  "type": "discussion",
  "entities": ["Pink Floyd", "The Wall", "concept albums"],
  "summary": "Deep dive on narrative concept albums; user values storytelling over pure musicality",
  "importance": 7,
  "metadata": {
    "duration_minutes": 15,
    "key_points": ["Narrative structure", "Roger Waters' writing"]
  }
}
```

---

## 🎚️ Storage Strategy - START LIGHT

**⚠️ IMPORTANT: Default to NOT storing**

Problem: Too much memory = noise that overwhelms LLM
Solution: Only store high-value memories initially

### What to Store (Phase 1 - Conservative)

**✅ ALWAYS Store:**

1. **Recommendations Made** (importance 8)
   - Any album/track recommended for discovery
   - Needs follow-up validation
   - Check play counts after reasonable time

2. **Strong Negative Feedback** (importance 9-10)
   - "That rec didn't work"
   - "I don't like X genre"
   - Learn from mistakes, prevent repeats

3. **Core Taste Insights** (importance 8-9)
   - "I value storytelling over production"
   - "I love baroque pop arrangements"
   - Shapes all future recommendations

4. **Upcoming Events** (importance 7)
   - Concerts, festivals
   - Natural follow-up opportunities

### What NOT to Store

**❌ SKIP:**

1. **Informational Queries**
   - "Show me top albums from 2023"
   - Just data retrieval, no insight

2. **Technical/Administrative**
   - "How do I use --force-api?"
   - Tool usage questions

3. **Small Talk**
   - "Hello", "Thanks"
   - Conversational noise

4. **Casual Mentions**
   - "Oh I like that song" (passing comment)
   - Only store if strong sentiment

5. **Already Stored Info**
   - Check for duplicates before storing
   - Update existing rather than create new

### Importance Scoring

```
10 = Critical (absolute rules, strong dislikes)
9  = Major insights, negative feedback
8  = Recommendations, core preferences
7  = Events, discovery gaps
6  = Casual insights
5  = Context (may age out)
3  = Ephemeral
1  = Barely worth storing
```

**Aging Policy:**
- Importance ≥ 8: Keep indefinitely
- Importance 5-7: Compress after 1 month
- Importance < 5: Delete after 2 months

### Deduplication Strategy

Before storing, check if similar memory exists:

```sql
SELECT * FROM memories
WHERE id IN (
    SELECT id FROM memories_fts
    WHERE entities MATCH 'taylor swift'
    AND summary MATCH 'storytelling'
)
AND timestamp > datetime('now', '-30 days');
```

**If found:** Don't duplicate
**If not found:** Store as new entry

---

## 🛠️ Tools Design

### 1. `lfm_memory_add`

Store a memory entry with automatic deduplication.

**Parameters:**
```json
{
  "type": "recommendation|insight|event|feedback|discussion",
  "entities": ["Artist Name", "Album Name"],
  "summary": "What happened and why it matters",
  "importance": 1-10,
  "metadata": {
    // Type-specific flexible data
  }
}
```

**Behavior:**
- Checks for duplicates (last 30 days)
- Validates importance is reasonable
- Returns storage confirmation with ID

**Example:**
```json
{
  "type": "recommendation",
  "entities": ["The Zombies", "Odessey and Oracle"],
  "summary": "Recommended for baroque pop complexity",
  "importance": 8,
  "metadata": {
    "check_after_days": 7
  }
}
```

### 2. `lfm_memory_query`

Retrieve relevant memories (NOT session context dump).

**Parameters:**
```json
{
  "artist": "The Zombies",        // Fuzzy entity search
  "album": "Odessey and Oracle",  // Optional: more specific
  "type": "recommendation",       // Optional: filter by type
  "since_days": 14,              // Optional: temporal filter
  "importance_min": 5,           // Optional: relevance filter
  "limit": 5                     // Don't return everything
}
```

**Returns:**
```json
[
  {
    "id": "uuid-123",
    "timestamp": "2025-10-11T14:30:00Z",
    "type": "recommendation",
    "entities": ["The Zombies", "Odessey and Oracle"],
    "summary": "Recommended baroque pop album...",
    "importance": 8,
    "days_ago": 2
  }
]
```

**Key behavior:** Returns structured, ranked results - NOT raw context dump

### 3. `lfm_memory_validate`

Check if recommendation was played (data-driven follow-up).

**Parameters:**
```json
{
  "recommendation_id": "uuid-123"
}
```

**Returns:**
```json
{
  "recommendation": {
    "artist": "The Zombies",
    "album": "Odessey and Oracle",
    "recommended_at": "2025-10-11",
    "days_since": 2
  },
  "validation": {
    "plays_since_rec": 0,
    "user_heard_before": false,
    "follow_up_suggestion": "You haven't touched it yet - did it not land?"
  }
}
```

**Behavior:** Cross-references with Last.fm play data for honest feedback

### 4. `lfm_memory_force_add`

Manual override for edge cases.

**Parameters:**
```json
{
  "summary": "User's dad introduced them to Pink Floyd as a kid",
  "entities": ["Pink Floyd", "personal history"],
  "type": "insight",
  "importance": 6,
  "skip_dedup": true
}
```

**When to use:** LLM judges "this feels important" but doesn't fit automatic rules

---

## 🔄 Implementation Phases

### Phase 1: Basic Storage + Query (2-3 days)

**Deliverables:**
- SQLite database with FTS5
- `lfm_memory_add` and `lfm_memory_query` tools
- Simple JSON-based storage
- Manual querying via guidelines

**What works:**
- Store recommendations/insights/events
- Query by artist/type/date
- Fuzzy entity matching
- Deduplication

**What doesn't:**
- No automatic storage (manual only)
- No recommendation validation yet
- No aging/compression

### Phase 2: Recommendation Validation (1-2 days)

**Deliverables:**
- `lfm_memory_validate` tool
- Integration with Last.fm play count API
- Auto-generate follow-up suggestions

**What works:**
- "I recommended X, you played it Y times"
- Data-driven follow-up prompts
- Honest accountability

### Phase 3: Aging & Compression (2-3 days)

**Deliverables:**
- Compress summaries older than 2 weeks
- Mark old memories as "fuzzy" (lower confidence)
- Delete very old low-importance memories

**What works:**
- Memory degrades like human memory
- Storage stays manageable
- Recent context stays clear

### Phase 4: Hooks (Optional - 3-5 days)

**Only if Phase 1-3 shows LLM forgets to check memory consistently**

**Deliverables:**
- Pre-tool hooks (auto-inject memory context)
- Post-tool hooks (auto-store after playback/recommendations)
- Systematic memory checking

**What works:**
- Never forgets to check memory
- Automatic storage after key events
- Reduced cognitive load on LLM

---

## 📊 Performance Expectations

**Query Performance:**
- Exact entity match: 5-10ms
- Fuzzy entity match: 5-10ms (FTS5 handles this)
- Date range filter: 5-10ms
- Complex multi-filter: 10-20ms
- Full-text search: 10-15ms

**Scalability:**
- 1,000 entries: ~5-20ms queries
- 5,000 entries: ~10-30ms queries
- 10,000+ entries: ~15-40ms queries

**Storage:**
- Single entry: ~300-500 bytes
- 1,000 entries: ~400KB
- 5,000 entries: ~2MB
- 10,000 entries: ~4MB

---

## ❌ What NOT to Do

Based on Championship Vinyl vision and practical experience:

1. **Don't dump all memory as session context**
   - Context window pollution
   - Overwhelming detail
   - Use targeted queries instead

2. **Don't store everything**
   - More noise than signal
   - LLM obsesses on irrelevant detail
   - Default to NOT storing

3. **Don't rely on perfect LLM discipline**
   - Add hooks if LLM consistently forgets
   - But try guidelines first

4. **Don't over-complicate initially**
   - Start with manual storage
   - Add automation only where needed
   - Iterate based on real usage

5. **Don't treat memory as perfect recall**
   - Fuzzy memory feels more human
   - Compression and aging are features, not bugs

---

## ✅ Success Criteria

**Memory system is working if:**

1. **Natural Follow-ups**
   - LLM checks memory before recommending
   - "I recommended X last week, you played it 5 times!"
   - "Still haven't touched that album..."

2. **No Duplicate Recommendations**
   - Queries memory before suggesting
   - Remembers what didn't work
   - Learns from negative feedback

3. **Taste Understanding Persists**
   - "We discussed your love for baroque pop"
   - "You mentioned valuing storytelling over production"
   - Core insights shape recommendations across sessions

4. **Doesn't Obsess on Irrelevant Detail**
   - Memory retrieval is targeted, not dump
   - Only high-importance memories persist
   - Casual mentions age out naturally

5. **Data-Driven Accountability**
   - Validates recommendations with play data
   - Honest feedback loop
   - "That rec got zero plays - did I miss the mark?"

---

## 🚀 Getting Started

**First implementation focus:**

1. Set up SQLite database with FTS5
2. Implement `lfm_memory_add` and `lfm_memory_query`
3. Update guidelines for manual memory checking
4. Test with real conversations
5. Observe what gets stored, what gets queried
6. Iterate based on actual usage patterns

**Remember:** Start light. Only store high-importance memories. Add more types if we see value. Better to miss some context than overwhelm with noise.

---

**Next Steps:** Implement Phase 1 when ready to build
