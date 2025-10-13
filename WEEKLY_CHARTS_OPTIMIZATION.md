# Weekly Charts API Optimization Investigation

**Date**: 2024-12-23
**Status**: Research Phase - Not Implemented
**Priority**: High - Could solve major performance issues

## 🚨 Current Problem

**Date range queries are extremely slow and often timeout:**
- `lfm tracks --from 2020 --to 2024` → Times out after processing 50,000+ individual scrobbles
- `lfm tracks --year 2023` → Works but processes 10,000+ individual scrobbles (slow)
- `lfm tracks --period overall` → Fast (uses Last.fm's pre-aggregated data)

**Root cause**: Current implementation uses `user.getRecentTracks` with date filtering, requiring:
- 50+ API calls for large date ranges
- Processing thousands of individual scrobbles in memory
- Manual aggregation and counting

## 💡 Potential Solution: Weekly Charts API

### Last.fm's Weekly Chart Endpoints:
- `user.getWeeklyChartList` - Lists available weekly periods for a user
- `user.getWeeklyTrackChart` - Pre-aggregated top tracks for a specific week
- `user.getWeeklyArtistChart` - Pre-aggregated top artists for a specific week
- `user.getWeeklyAlbumChart` - Pre-aggregated top albums for a specific week

### Performance Math:
**Current approach** (5-year range):
- ~50,000 individual scrobbles ÷ 1000 per page = 50+ slow API calls
- Manual aggregation of massive dataset
- **Result**: Times out

**Weekly charts approach** (5-year range):
- 5 years × 52 weeks = ~260 fast pre-aggregated API calls
- Last.fm has already done the aggregation
- 260 calls × 50ms throttling = ~13 seconds + API response time
- **Potential result**: ~20-30 seconds instead of timeout
- **~7x performance improvement** (conservative estimate)

## 📋 How It Would Work

```
User Request: lfm tracks --from 2020-01-01 --to 2024-12-31 --limit 10

Step 1: getWeeklyChartList(user=smarshal)
        Returns: Array of {from: timestamp, to: timestamp} periods

Step 2: Filter periods to date range (2020-2024)
        Result: ~260 weekly periods

Step 3: For each period, call getWeeklyTrackChart(user, from, to)
        260 API calls returning pre-aggregated track data

Step 4: Merge all weekly results and re-aggregate
        Combine play counts across weeks for same tracks

Step 5: Return top N tracks by total play count
```

## ✅ Advantages

1. **Uses Last.fm's pre-aggregated data** (should be much faster)
2. **Handles arbitrary date ranges** (unlike limited period options)
3. **Reasonable API call count** (~50/year vs 1000s of individual scrobbles)
4. **No individual scrobble processing** required
5. **Historical coverage** (depends on when Last.fm started weekly charts)
6. **No authentication required** (just API key)

## ⚠️ Unknowns & Limitations

### Critical Constraints:
1. **Must use Last.fm's predefined weekly periods** - can't use arbitrary week boundaries
2. **Date ranges must come from getWeeklyChartList** - can't specify custom dates
3. **Unknown historical depth** - how far back do weekly charts go?
4. **Unknown chart size** - how many tracks per weekly chart? (Top 10? 50? 100?)
5. **Unknown user coverage** - are charts available for all users?

### Implementation Questions:
1. **Chart completeness**: What if a user has gaps in weekly charts?
2. **Performance reality**: Are weekly chart calls actually fast?
3. **Data quality**: Do weekly aggregations match manual scrobble aggregation?
4. **Edge cases**: Partial weeks at range boundaries?

## 🧪 Testing Plan

### Phase 1: Basic API Testing
1. Test `getWeeklyChartList` for a user - how far back does it go?
2. Test `getWeeklyTrackChart` performance - how fast are individual calls?
3. Check weekly chart data quality vs current implementation

### Phase 2: Performance Testing
1. Implement basic weekly aggregation for small date range (1 year)
2. Compare results with current scrobble-based approach
3. Measure actual performance improvement

### Phase 3: Production Implementation
1. Implement full weekly charts logic in `GetTopTracksForDateRangeAsync`
2. Add fallback to current approach for users without sufficient weekly data
3. Add progress indicators for long operations

## 💭 Alternative Approaches Considered

### Option 1: Accept Current Limitations
- Only support Last.fm's built-in periods
- Reject arbitrary date ranges entirely

### Option 2: Statistical Sampling
- Process every 10th page of scrobbles instead of all
- Much faster, probably reasonably accurate

### Option 3: Smart Caching + Chunking
- Break ranges into years, cache results
- First query slow, subsequent queries fast

**Weekly charts still seems most promising** - could provide both performance and flexibility.

## 📈 Expected Impact

If weekly charts work as expected:
- ✅ Fix timeout issues for multi-year date ranges
- ✅ Enable MCP service to handle "15-year" recommendation queries
- ✅ Make date range queries 5-10x faster
- ✅ Improve overall user experience for historical data queries
- ✅ Unlock more sophisticated date range features

## 🔜 Next Steps

1. **Complete current MCP playlist testing** (higher priority)
2. **Implement weekly charts proof-of-concept**
3. **Performance benchmarking** against current approach
4. **Production implementation** if results are positive

---

**Status**: Documented for future investigation
**Potential Impact**: High - could solve fundamental performance bottleneck
**Risk**: Medium - unknowns about API limitations and historical coverage