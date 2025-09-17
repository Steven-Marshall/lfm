using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Microsoft.Extensions.Logging;

namespace Lfm.Core.Services;

public interface ITagFilterService
{
    /// <summary>
    /// Checks if an artist should be excluded based on their tags and current configuration.
    /// </summary>
    /// <param name="artistTags">The artist's top tags</param>
    /// <param name="config">Current configuration</param>
    /// <returns>True if artist should be excluded, false otherwise</returns>
    bool ShouldExcludeArtist(TopTags? artistTags, LfmConfig config);

    /// <summary>
    /// Filters a list of artist names, removing those that match excluded tags.
    /// This method fetches tags for each artist and applies filtering.
    /// </summary>
    /// <param name="artistNames">List of artist names to filter</param>
    /// <param name="config">Current configuration</param>
    /// <returns>Filtered list of artist names and count of excluded artists</returns>
    Task<(List<string> filteredArtists, int excludedCount)> FilterArtistsAsync(
        List<string> artistNames,
        LfmConfig config);
}

public class TagFilterService : ITagFilterService
{
    private readonly ILastFmApiClient _apiClient;
    private readonly ILogger<TagFilterService> _logger;

    public TagFilterService(
        ILastFmApiClient apiClient,
        ILogger<TagFilterService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool ShouldExcludeArtist(TopTags? artistTags, LfmConfig config)
    {
        // If filtering disabled or no tags to exclude, don't exclude anyone
        if (!config.EnableTagFiltering || !config.ExcludedTags.Any())
            return false;

        // If artist has no tags, don't exclude (benefit of the doubt)
        if (artistTags?.Tags == null || !artistTags.Tags.Any())
            return false;

        // Check if any of the artist's tags match excluded tags above threshold
        foreach (var tag in artistTags.Tags)
        {
            // Skip tags below threshold
            if (tag.Count < config.TagFilterThreshold)
                continue;

            // Check if this tag is in the excluded list (case-insensitive)
            if (config.ExcludedTags.Any(excludedTag =>
                string.Equals(excludedTag, tag.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogDebug("Excluding artist with tag '{TagName}' (count: {Count})", tag.Name, tag.Count);
                return true;
            }
        }

        return false;
    }

    public async Task<(List<string> filteredArtists, int excludedCount)> FilterArtistsAsync(
        List<string> artistNames,
        LfmConfig config)
    {
        var filteredArtists = new List<string>();
        var excludedCount = 0;

        foreach (var artistName in artistNames)
        {
            try
            {
                // Throttling now handled by CachedLastFmApiClient

                var artistTags = await _apiClient.GetArtistTopTagsAsync(artistName, autocorrect: true);

                if (ShouldExcludeArtist(artistTags, config))
                {
                    excludedCount++;
                    _logger.LogDebug("Excluded artist: {ArtistName}", artistName);
                }
                else
                {
                    filteredArtists.Add(artistName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get tags for {ArtistName}, including in results", artistName);
                // On error, include the artist (benefit of the doubt)
                filteredArtists.Add(artistName);
            }
        }

        _logger.LogInformation("Tag filtering: {FilteredCount} artists kept, {ExcludedCount} excluded",
            filteredArtists.Count, excludedCount);

        return (filteredArtists, excludedCount);
    }
}