using System.Text.Json;
using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

/// <summary>
/// Command for finding similar artists
/// </summary>
public class SimilarCommand : BaseCommand
{
    public SimilarCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILogger<SimilarCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
    }

    /// <summary>
    /// Get similar artists for a specified artist
    /// </summary>
    public async Task<int> ExecuteAsync(string artist, int limit = 20, bool timing = false, bool json = false)
    {
        var stopwatch = timing ? System.Diagnostics.Stopwatch.StartNew() : null;

        try
        {
            if (!json)
            {
                Console.WriteLine($"Finding artists similar to {artist}...\n");
            }

            var result = await _apiClient.GetSimilarArtistsWithResultAsync(artist, limit);

            stopwatch?.Stop();

            if (!result.Success)
            {
                if (json)
                {
                    var errorOutput = new
                    {
                        success = false,
                        error = result.Error?.Message ?? "Unknown error"
                    };
                    Console.WriteLine(JsonSerializer.Serialize(errorOutput, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    _logger.LogError("Failed to get similar artists: {Error}", result.Error?.Message ?? "Unknown error");
                }
                return 1;
            }

            var similarArtists = result.Data;

            if (json)
            {
                // JSON output for MCP
                var jsonOutput = new
                {
                    success = true,
                    artist = similarArtists.Attributes.Artist,
                    similarArtists = similarArtists.Artists.Select(a => new
                    {
                        name = a.Name,
                        match = a.Match,
                        url = a.Url,
                        mbid = a.Mbid
                    }).ToList(),
                    count = similarArtists.Artists.Count
                };
                Console.WriteLine(JsonSerializer.Serialize(jsonOutput, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                // Human-readable output
                Console.WriteLine($"Artists similar to {similarArtists.Attributes.Artist}:\n");

                int rank = 1;
                foreach (var similar in similarArtists.Artists)
                {
                    var matchPercent = (double.Parse(similar.Match) * 100).ToString("F0");
                    Console.WriteLine($"{rank,3}. {similar.Name,-40} (Match: {matchPercent}%)");
                    rank++;
                }

                Console.WriteLine($"\nTotal: {similarArtists.Artists.Count} similar artists");
            }

            if (timing && stopwatch != null && !json)
            {
                Console.WriteLine($"\nResponse time: {stopwatch.ElapsedMilliseconds}ms");
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (json)
            {
                var errorOutput = new
                {
                    success = false,
                    error = ex.Message
                };
                Console.WriteLine(JsonSerializer.Serialize(errorOutput, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                _logger.LogError(ex, "Error getting similar artists");
                Console.WriteLine($"Error: {ex.Message}");
            }
            return 1;
        }
    }
}