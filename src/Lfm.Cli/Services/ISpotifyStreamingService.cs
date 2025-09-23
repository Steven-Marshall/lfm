using Lfm.Core.Models;
using Lfm.Core.Models.Results;

namespace Lfm.Cli.Services;

public interface ISpotifyStreamingService
{
    Task<bool> IsAvailableAsync();
    Task StreamTracksAsync(List<Track> tracks, bool playNow, string? playlistName, string defaultPlaylistTitle, bool shuffle = false, string? device = null);
    Task StreamRecommendationsAsync(List<RecommendationResult> recommendations, bool playNow, string? playlistName, string defaultPlaylistTitle, bool shuffle = false, string? device = null);
}