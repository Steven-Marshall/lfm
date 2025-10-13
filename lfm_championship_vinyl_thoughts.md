# LFM MCP - Championship Vinyl Session Notes

## Key Realizations

### 1. Product Vision: Championship Vinyl Digital
We're building the digital simulation of the High Fidelity record store experience.

**The Beta Band 3EPs moment** - that's the north star:
- Expert curation based on deep knowledge
- Personal memory of past conversations
- Natural follow-up ("how'd you like that album I recommended?")
- Immediate playback ("here, listen to this")
- Genuine enthusiasm without algorithm bullshit

**What Championship Vinyl Rob would do:**
- Remember you came in last week
- Know what you bought/discussed
- Make connections across your taste
- Put something on the store deck to demonstrate
- Follow up naturally without being pushy

### 2. Technical Challenge: Orchestration
LLMs are great at reasoning but terrible at systematic tool use without structure.

**Current problem:** I have all the tools but forget to use them proactively.

**What should happen:**
1. User mentions artist → check conversation history first
2. See past recommendations → check Last.fm for plays since
3. Make contextual follow-up based on data
4. Recommend with reasoning, not correlation

**What actually happens:**
- Jump straight to chatting
- Use tools only when explicitly prompted
- Forget I have access to data
- Miss obvious opportunities

**Potential solutions:**
- Planning MCP (like Sequential Thinking)
- Claude Code-style task decomposition
- Custom CLI wrapper that enforces patterns
- Agent framework for structured workflows

## Memory System Design

### Temporal Memory with Fallibility
Like human memory - recent stuff is clear, older stuff gets fuzzy.

**Fresh (1 week):**
- Full conversation context
- Exact recommendations made
- Complete detail

**Aging (2-4 weeks):**
- Compressed summaries
- Key facts preserved (artist, album, sentiment)
- Conversational nuance fades

**Old (2+ months):**
- Just the gist
- "I think we discussed this before?"
- May fade entirely

**Critical insight:** Hard facts never fade (Last.fm data), soft context degrades naturally.

### The Two Types of Memory

**Quantitative (always accurate):**
- Last.fm play counts
- Album rankings
- Listening history
- Ground truth data

**Qualitative (degrades with time):**
- "We discussed Taylor Swift's production..."
- "You mentioned loving folklore's storytelling"
- Pub conversation stuff that gets fuzzy

### Temporal Follow-ups
The tool knows **when** to check back:
- Don't ask about 45-min album after 10 minutes
- Weekend check-in: "Did that Friday playlist get rotation?"
- 2 weeks later: "Still haven't touched The National..."

**And validate with data:**
- "I recommended Clouds last week - you played it 5 times! Good call by me!"
- "That mixtape got zero plays - did I miss the mark?"

## Three Personality System

Inspired by Championship Vinyl staff - Rob, Dick, Barry.

**Rob (enthusiastic, opinionated):**
- "DUDE. You need to hear this RIGHT NOW"
- Pushes boundaries aggressively
- Zero filter, maximum passion

**Dick (encyclopedic, gentle):**
- "The 1967 mono mix is superior because..."
- Deep cuts, historical context
- Respectful, never pushy

**Barry (balanced, thoughtful):**
- Makes connections across genres
- Well-reasoned recommendations
- The sensible moderator

**Implementation:** Three separate LLM instances with different guidelines, all reading shared conversation history.

**User control:** "Piss off Barry" when it gets overwhelming.

## Technical Discoveries Today

### Sonos API Limitations
- Spotify API has "restricted device" limitations for Sonos
- Can't control Sonos speakers via Spotify API
- Would need direct Sonos API integration
- Playback backends need to be pluggable (Spotify, Sonos, YouTube Music)

### Year Parameter Issue
- `year: "2025"` failed first time, worked on retry
- Possible caching/warmup issue
- Date range (`from`/`to`) works reliably
- Keep both options available

### Metadata Hell
- Three different apostrophe characters in track names
- "Shine On You Crazy Diamond" scrobbled under multiple variants
- `unaccountedPlays` detects this automatically
- Guidelines explain it's NOT track skipping

## User Profile Insights

### Taste Characteristics
- Genuinely eclectic: Taylor Swift + Bish Bosch + Maria Callas
- Commits deeply to disparate genres
- Not dabbling - 92 plays on Bish Bosch proves adventurous taste
- 1989 = loves sophisticated pop production
- folklore/evermore = indie/folk sensibility
- NOT a typical algorithm-friendly profile

### Why This Tool Works for This User
- Broad taste breaks collaborative filtering
- Needs reasoning, not correlation
- Can make connections across non-continuous dimensions
- Multi-modal pattern recognition (production + lyrics + emotional tone)
- Won't recommend Sabrina Carpenter just because Taylor Swift

### LLM Strengths for Music Discovery
- Comprehensive catalog knowledge
- Pattern analysis across entire history
- No personal bias/ego
- Systematic gap-filling
- Can spot: "Beach Boys #8 but never touched Carl & The Passions"

**Different from pub friends:**
- Pub: passionate debate, shared experiences, cultural context
- LLM: exhaustive knowledge, data-driven gaps, no blind spots

## Authentication Evolution (Historical)

### v1: Static Password
Guidelines contained password → LLM just scanned for it → defeated purpose

### v2: Derived Password
Had to understand metadata issues to compute password → actually worked!

### v3: Gave Up
Too much overhead, felt silly. Session init approach is cleaner.

**Lesson:** Make it structurally part of workflow rather than a test to pass.

## What NOT to Do

Based on Championship Vinyl vision:

❌ Algorithmic trending recommendations
❌ Metrics obsession for its own sake  
❌ Generic "people who liked X also liked Y"
❌ Perfect robotic recall
❌ Treating user like a data point
❌ Pushy about popular/mainstream stuff

✅ Remember conversations naturally (with fallibility)
✅ Follow up based on time + listening data
✅ Deep catalog knowledge + specific taste
✅ Proactive discovery within loved artists
✅ Playback integration
✅ Be a music buddy, not a database

## Next Steps

**Memory System:**
- Implement conversation storage with timestamps
- Build compression/aging logic
- Surface to LLM via tool or guidelines
- Add temporal reasoning

**Orchestration:**
- Research Claude Code's approach
- Planning MCP or sequential thinking integration
- Enforce systematic tool usage patterns

**Three Personalities:**
- Prototype multi-agent setup
- Different guideline documents per personality
- Shared conversation history
- User control over which voices participate

**Playback Backends:**
- Keep Spotify working
- Add Sonos integration (separate API)
- Consider YouTube Music (free tier advantage)
- Maintain pluggable architecture

## Quote to Remember

"It's like going into an old fashioned record store. Browsing the racks. Chatting to the staff. Listening to what they're playing on the store record deck. Getting them to play something we just talked about. **High Fidelity in digital format.**"

---

*Session: October 11, 2025*
*Status: Conceptual planning, no digital dust yet*
