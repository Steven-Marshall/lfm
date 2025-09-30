# Last.fm MCP Usage Guidelines

## Temporal Parameter Selection

### When users mention YEARS (2023, 2024, 2025):
- ✅ **USE**: `year: "2025"`
- ❌ **AVOID**: `period: "7day"` or `period: "12month"`
- **Example**: "music in 2025" → `year: "2025"`

### When users mention RELATIVE time:
- **"recently"** / **"lately"** → `period: "1month"`
- **"this week"** → `period: "7day"`
- **"this month"** → `period: "1month"`
- **"overall"** / **"all time"** → `period: "overall"`

### When users mention SPECIFIC RANGES:
- **"since June"** → `from: "2025-06-01"`
- **"January to March"** → `from: "2025-01-01", to: "2025-03-31"`
- **"last 6 months"** → `period: "6month"`

## Tool Selection Guide

### Finding Similar Artists:
- **User asks for "artists similar to X"**: Use `lfm_similar` to find artists similar to a specific artist
  - Example: "artists similar to Holly Humberstone" → `lfm_similar(artist: "Holly Humberstone", limit: 20)`
  - Returns: List of similar artists with similarity scores from Last.fm
  - Follow-up: Use `lfm_bulk_check` to verify which ones user has/hasn't heard

- **User asks for "recommendations based on my taste"**: Use `lfm_recommendations` to analyze user's overall listening
  - Example: "recommend new music" → `lfm_recommendations(filter: 1, totalArtists: 20)`
  - Returns: Artists similar to user's top artists, with tracks
  - Based on: User's listening history, not a specific artist

### Key Difference:
- `lfm_similar`: "Find artists like X" (specific artist as seed)
- `lfm_recommendations`: "Find new music for me" (user's taste as seed)

## Discovery Workflows

### Creating Discovery Playlists:
1. **Get recommendations**: Use `lfm_recommendations` with appropriate temporal parameters
2. **Filter for new artists**: Set `filter: 1` (minimum 1 play to exclude) or use `lfm_bulk_check`
3. **Create playlist**: Use `lfm_create_playlist` with curated tracks

### Checking Listening History:
- **Single artist/track**: Use `lfm_check`
- **Multiple items**: Use `lfm_bulk_check` for efficiency
- **Before recommendations**: Check if user wants "new" vs "familiar" artists

## Common Parameter Patterns

### Recommendations:
- **Discovery focus**: Use `filter: 1` to exclude known artists
- **Familiar music**: Use `filter: 0` (default) to include all
- **Playlist creation**: Combine with `playlist` and `playNow` parameters

### Time Periods:
- **Current year**: `year: "2025"` (not `period: "12month"`)
- **Rolling periods**: `period: "1month"`, `period: "6month"`, etc.
- **Specific dates**: `from` and `to` parameters

### Playlist Creation:
- **Track format**: `[{"artist": "Name", "track": "Title"}, ...]`
- **Naming**: Playlist names auto-prefixed with "lfm-"
- **Spotify features**: Use `shuffle`, `playNow`, `device` as needed

## Troubleshooting

### "No results" issues:
- Check if temporal parameters are too restrictive
- Verify artist/track names are spelled correctly
- Consider broader time periods for more data

### Discovery not working:
- Ensure `filter` parameter excludes known artists
- Use `lfm_bulk_check` to verify artist listening status
- Consider expanding recommendation count for more options

## Best Practices

### Multi-step Workflows:
1. Always check user's actual listening before making assumptions
2. Use appropriate temporal parameters based on user language
3. Verify artist novelty for discovery playlists
4. Provide clear feedback about what was found/created

### Contextual Filtering (LLM Value-Add):
- **Maintain conversational context**: If user says "I don't like X", filter that out from subsequent results
- **Apply nuanced preferences**: "Not too pop-py", "More experimental", etc.
- **Combine multiple signals**: Last.fm data + user stated preferences + listening history
- **Tool composition**: `lfm_similar` → apply context → `lfm_bulk_check` → filtered recommendations

### Error Handling:
- If tracks not found, continue with available tracks
- Report both successes and failures clearly
- Suggest alternatives when original request fails