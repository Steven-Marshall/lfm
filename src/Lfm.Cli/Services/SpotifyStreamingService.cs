using Lfm.Core.Models;
using Lfm.Core.Models.Results;
using Lfm.Core.Services;
using Lfm.Spotify;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Services;

public class SpotifyStreamingService : ISpotifyStreamingService
{
    private readonly IPlaylistStreamer _spotifyStreamer;
    private readonly ISymbolProvider _symbols;
    private readonly ILogger<SpotifyStreamingService> _logger;

    public SpotifyStreamingService(
        IPlaylistStreamer spotifyStreamer,
        ISymbolProvider symbolProvider,
        ILogger<SpotifyStreamingService> logger)
    {
        _spotifyStreamer = spotifyStreamer ?? throw new ArgumentNullException(nameof(spotifyStreamer));
        _symbols = symbolProvider ?? throw new ArgumentNullException(nameof(symbolProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsAvailableAsync()
    {
        return await _spotifyStreamer.IsAvailableAsync();
    }

    public async Task StreamTracksAsync(List<Track> tracks, bool playNow, string? playlistName, string defaultPlaylistTitle, bool shuffle = false, string? device = null)
    {
        if (!playNow && string.IsNullOrEmpty(playlistName))
        {
            return; // No streaming requested
        }

        try
        {
            if (!await _spotifyStreamer.IsAvailableAsync())
            {
                Console.WriteLine($"{_symbols.Error} Spotify not available. Make sure you have configured Client ID and Client Secret.");
                return;
            }

            // Shuffle tracks if requested (only for Spotify, not for display)
            var tracksToStream = tracks;
            if (shuffle)
            {
                var random = new Random();
                tracksToStream = tracks.OrderBy(x => random.Next()).ToList();
                Console.WriteLine($"{_symbols.Music} Shuffled tracks for Spotify");
            }

            // Add lfm- prefix if not already present
            var finalPlaylistName = string.IsNullOrEmpty(playlistName) ? null :
                playlistName.StartsWith("lfm-", StringComparison.OrdinalIgnoreCase) ? playlistName : $"lfm-{playlistName}";

            if (playNow)
            {
                var result = await _spotifyStreamer.QueueTracksAsync(tracksToStream, device);
                if (result.Success)
                {
                    Console.WriteLine($"{_symbols.Success} {result.Message}");
                    if (result.NotFoundTracks.Any())
                    {
                        Console.WriteLine($"⚠️  Could not find {result.NotFoundTracks.Count} tracks on Spotify");
                    }
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} Failed to queue tracks: {result.Message}");
                }
            }

            if (!string.IsNullOrEmpty(finalPlaylistName))
            {
                var result = await _spotifyStreamer.SavePlaylistAsync(tracksToStream, finalPlaylistName, device);
                if (result.Success)
                {
                    Console.WriteLine($"{_symbols.Success} {result.Message}");
                    if (result.NotFoundTracks.Any())
                    {
                        Console.WriteLine($"⚠️  Could not find {result.NotFoundTracks.Count} tracks on Spotify");
                    }
                }
                else
                {
                    Console.WriteLine($"{_symbols.Error} Failed to create playlist: {result.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming tracks to Spotify");
            Console.WriteLine($"{_symbols.Error} Spotify error: {ex.Message}");
        }
    }

    public async Task StreamRecommendationsAsync(List<RecommendationResult> recommendations, bool playNow, string? playlistName, string defaultPlaylistTitle, bool shuffle = false, string? device = null)
    {
        if (!playNow && string.IsNullOrEmpty(playlistName))
        {
            return; // No streaming requested
        }

        // Convert recommendations to tracks for Spotify streaming
        var tracks = ConvertRecommendationsToTracks(recommendations);
        await StreamTracksAsync(tracks, playNow, playlistName, defaultPlaylistTitle, shuffle, device);
    }

    private List<Track> ConvertRecommendationsToTracks(List<RecommendationResult> recommendations)
    {
        var tracks = new List<Track>();
        foreach (var rec in recommendations)
        {
            // If we have track recommendations, use them
            if (rec.TopTracks != null && rec.TopTracks.Any())
            {
                foreach (var topTrack in rec.TopTracks)
                {
                    var track = new Track
                    {
                        Name = topTrack.Name,
                        Artist = new ArtistInfo { Name = rec.ArtistName },
                        PlayCount = topTrack.PlayCount.ToString()
                    };
                    tracks.Add(track);
                }
            }
            else
            {
                // Fall back to searching for the artist's top track
                var track = new Track
                {
                    Name = "", // Empty name will make Spotify search for artist's top track
                    Artist = new ArtistInfo { Name = rec.ArtistName },
                    PlayCount = "0"
                };
                tracks.Add(track);
            }
        }
        return tracks;
    }
}