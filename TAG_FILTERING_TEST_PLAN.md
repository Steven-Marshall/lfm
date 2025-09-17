# Tag Filtering Feature - Test Plan

## Feature Summary
The tag filtering feature allows users to exclude artists from recommendations based on music genres/tags from Last.fm's tag database. This is particularly useful for filtering out genres like classical music, christmas music, or any other tags the user wants to avoid.

## Configuration Commands

### 1. View Current Configuration
```bash
# Show all excluded tags
lfm config show-excluded-tags

# Show general config (includes tag settings)
lfm config show
```

### 2. Manage Excluded Tags
```bash
# Add a tag to exclude list
lfm config add-excluded-tag "classical"
lfm config add-excluded-tag "christmas"
lfm config add-excluded-tag "opera"

# Remove a tag from exclude list
lfm config remove-excluded-tag "opera"

# Clear all excluded tags
lfm config clear-excluded-tags
```

### 3. Configure Tag Filtering Settings
```bash
# Set tag count threshold (default: 30)
# Only tags with count >= threshold will trigger exclusion
lfm config set-tag-threshold 50

# Set max API lookups for tag filtering (default: 20)
# Limits the number of tag API calls per recommendation request
lfm config set-max-tag-lookups 30
```

## Using Tag Filtering with Recommendations

### Basic Usage
```bash
# Get recommendations WITHOUT tag filtering (default)
lfm recommendations

# Get recommendations WITH tag filtering
lfm recommendations --exclude-tags

# Short alias
lfm recommendations -et
```

### Combined with Other Options
```bash
# With verbose output to see which artists were filtered
lfm recommendations --exclude-tags --verbose

# With different time periods
lfm recommendations --exclude-tags --period 3month
lfm recommendations --exclude-tags --year 2023

# With play count filter and tag filter
lfm recommendations --exclude-tags --filter 10

# With limited recommendations
lfm recommendations --exclude-tags --limit 10

# Full example with multiple options
lfm recommendations --exclude-tags --verbose --limit 30 --filter 5 --period 6month
```

## Test Scenarios

### Test 1: Basic Tag Filtering
1. Configure some common classical tags:
   ```bash
   lfm config add-excluded-tag "classical"
   lfm config add-excluded-tag "classical music"
   lfm config add-excluded-tag "opera"
   lfm config add-excluded-tag "symphony"
   ```

2. Run recommendations without filtering:
   ```bash
   lfm recommendations --limit 20
   ```
   Note any classical artists that appear.

3. Run with filtering enabled:
   ```bash
   lfm recommendations --exclude-tags --limit 20 --verbose
   ```
   Verify classical artists are excluded and see verbose output.

### Test 2: API Budget Management
1. Set a low API budget:
   ```bash
   lfm config set-max-tag-lookups 5
   ```

2. Run with many candidates:
   ```bash
   lfm recommendations --exclude-tags --limit 50 --verbose
   ```
   Should only check tags for first 5 artists (API budget limit).

### Test 3: Tag Threshold Testing
1. Set a high threshold:
   ```bash
   lfm config set-tag-threshold 100
   ```

2. Run recommendations:
   ```bash
   lfm recommendations --exclude-tags --verbose
   ```
   Only very prominent tags (count >= 100) will trigger exclusion.

### Test 4: N*2 Candidate Checking
The system checks 2x the requested recommendations for filtering:
```bash
# Request 10 recommendations, system checks up to 20 candidates
lfm recommendations --exclude-tags --limit 10 --verbose
```
This ensures enough results even after filtering.

### Test 5: Date Range Filtering
```bash
# Test with specific year
lfm recommendations --exclude-tags --year 2024 --verbose

# Test with date range
lfm recommendations --exclude-tags --from 2024-01-01 --to 2024-06-30 --verbose
```

### Test 6: Disable/Enable Filtering
```bash
# Clear all tags (disable filtering)
lfm config clear-excluded-tags

# Verify no filtering occurs even with --exclude-tags
lfm recommendations --exclude-tags --verbose

# Re-add tags to re-enable
lfm config add-excluded-tag "classical"
```

## Expected Behaviors

### When Tag Filtering is Active
- Artists matching excluded tags are removed from recommendations
- Verbose mode shows which artists were filtered and why
- System checks N*2 candidates to ensure enough results
- API budget limits the number of tag lookups
- Results are cached to avoid repeated API calls

### Performance Considerations
- Tag lookups use the existing cache system (10-minute default)
- API throttling applies (100ms between calls)
- Maximum 20 tag lookups by default (configurable)

## Troubleshooting

### No Artists Being Filtered
- Check if tags are configured: `lfm config show-excluded-tags`
- Verify filtering is enabled in config
- Check tag threshold isn't too high
- Use --verbose to see tag matching details

### Too Many Artists Filtered
- Increase tag threshold: `lfm config set-tag-threshold 50`
- Review excluded tags list and remove overly broad tags
- Consider being more specific with tags

### API Rate Limiting
- Reduce max lookups: `lfm config set-max-tag-lookups 10`
- Use cache to avoid repeated lookups
- Allow throttling between requests

## Configuration Defaults

After setup, the default configuration includes these classical-related tags:
- classical
- classical music
- opera
- symphony
- orchestral
- baroque
- romantic era
- contemporary classical
- chamber music
- choral
- classical crossover

Users can customize this list based on their preferences.