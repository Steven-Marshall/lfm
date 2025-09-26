using Lfm.Core.Configuration;
using Lfm.Core.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using System.Web;

namespace Lfm.Cli.Commands;

public class ApiStatusCommand : BaseCommand, IDisposable
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://ws.audioscrobbler.com/2.0/";

    private struct EndpointTest
    {
        public string Name { get; init; }
        public string Method { get; init; }
        public Dictionary<string, string> Parameters { get; init; }
        public string Feature { get; init; }
    }

    public ApiStatusCommand(
        ILastFmApiClient apiClient,
        IConfigurationManager configManager,
        ILogger<ApiStatusCommand> logger,
        ISymbolProvider symbolProvider)
        : base(apiClient, configManager, logger, symbolProvider)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "lfm-cli/1.0");
    }

    public async Task ExecuteAsync(bool verbose = false, bool json = false)
    {
        await ExecuteWithErrorHandlingAsync("API status check", async () =>
        {
            if (!await ValidateApiKeyAsync())
                return;

            var config = await _configManager.LoadAsync();
            var testUser = !string.IsNullOrEmpty(config.DefaultUsername) ? config.DefaultUsername : "rj";

            var endpoints = new EndpointTest[]
            {
                new() {
                    Name = "user.getTopArtists",
                    Method = "user.getTopArtists",
                    Parameters = new() { ["user"] = testUser, ["period"] = "overall", ["limit"] = "1" },
                    Feature = "Core functionality"
                },
                new() {
                    Name = "user.getTopTracks",
                    Method = "user.getTopTracks",
                    Parameters = new() { ["user"] = testUser, ["period"] = "overall", ["limit"] = "1" },
                    Feature = "Core functionality"
                },
                new() {
                    Name = "user.getTopAlbums",
                    Method = "user.getTopAlbums",
                    Parameters = new() { ["user"] = testUser, ["period"] = "overall", ["limit"] = "1" },
                    Feature = "Core functionality"
                },
                new() {
                    Name = "user.getRecentTracks",
                    Method = "user.getRecentTracks",
                    Parameters = new() { ["user"] = testUser, ["limit"] = "1" },
                    Feature = "Date range aggregation"
                },
                new() {
                    Name = "artist.getTopTracks",
                    Method = "artist.getTopTracks",
                    Parameters = new() { ["artist"] = "The Beatles", ["limit"] = "1" },
                    Feature = "Artist-tracks command"
                },
                new() {
                    Name = "artist.getTopAlbums",
                    Method = "artist.getTopAlbums",
                    Parameters = new() { ["artist"] = "The Beatles", ["limit"] = "1" },
                    Feature = "Artist-albums command"
                },
                new() {
                    Name = "artist.getSimilar",
                    Method = "artist.getSimilar",
                    Parameters = new() { ["artist"] = "The Beatles", ["limit"] = "1" },
                    Feature = "Recommendations system"
                },
                new() {
                    Name = "artist.getTopTags",
                    Method = "artist.getTopTags",
                    Parameters = new() { ["artist"] = "The Beatles" },
                    Feature = "Tag filtering"
                }
            };

            var results = new List<EndpointResult>();

            if (!json)
            {
                Console.WriteLine($"{_symbols.Settings} Last.fm API Status Check");
                Console.WriteLine();
            }

            foreach (var endpoint in endpoints)
            {
                var result = await TestEndpointAsync(endpoint, verbose && !json, config.ApiKey);
                results.Add(result);

                if (!json)
                {
                    var statusIcon = result.IsHealthy ? _symbols.Success : _symbols.Error;
                    var timing = result.ResponseTime.HasValue ? $"{result.ResponseTime}ms" : "timeout";

                    Console.WriteLine($"{statusIcon} {endpoint.Name,-25} {timing,8} - {endpoint.Feature}");

                    if (verbose && !string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        Console.WriteLine($"    Error: {result.ErrorMessage}");
                    }
                }
            }

            if (json)
            {
                var jsonResult = new
                {
                    timestamp = DateTime.UtcNow,
                    endpoints = results.Select(r => new {
                        name = r.EndpointName,
                        healthy = r.IsHealthy,
                        responseTimeMs = r.ResponseTime,
                        feature = r.Feature,
                        error = r.ErrorMessage
                    }).ToArray(),
                    summary = new {
                        total = results.Count,
                        healthy = results.Count(r => r.IsHealthy),
                        unhealthy = results.Count(r => !r.IsHealthy)
                    }
                };

                Console.WriteLine(JsonSerializer.Serialize(jsonResult, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine();
                var healthy = results.Count(r => r.IsHealthy);
                var total = results.Count;

                if (healthy == total)
                {
                    Console.WriteLine($"{_symbols.Success} All endpoints healthy ({healthy}/{total})");
                }
                else
                {
                    Console.WriteLine($"{_symbols.StopSign} {total - healthy} endpoints unhealthy ({healthy}/{total} healthy)");
                    Console.WriteLine();
                    Console.WriteLine("Affected features:");

                    foreach (var failed in results.Where(r => !r.IsHealthy))
                    {
                        Console.WriteLine($"  • {failed.Feature}");
                    }

                    if (!verbose)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"{_symbols.Tip} Use --verbose for HTTP details");
                    }
                }
            }
        });
    }

    private async Task<EndpointResult> TestEndpointAsync(EndpointTest endpoint, bool verbose, string apiKey)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (verbose)
            {
                Console.Write($"Testing {endpoint.Name}...");
            }

            // Build URL with parameters
            var parameters = new Dictionary<string, string>(endpoint.Parameters)
            {
                ["method"] = endpoint.Method,
                ["api_key"] = apiKey,
                ["format"] = "json"
            };

            var query = string.Join("&", parameters.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));
            var url = $"{BaseUrl}?{query}";

            // Make simple HTTP request
            using var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            stopwatch.Stop();

            if (verbose)
            {
                Console.WriteLine($" {stopwatch.ElapsedMilliseconds}ms ({(int)response.StatusCode})");
            }

            // Simple health check: HTTP 200 + non-empty response
            var isHealthy = response.IsSuccessStatusCode && !string.IsNullOrEmpty(content);
            var errorMessage = !response.IsSuccessStatusCode
                ? $"HTTP {(int)response.StatusCode} {response.StatusCode}"
                : string.IsNullOrEmpty(content) ? "Empty response" : null;

            return new EndpointResult
            {
                EndpointName = endpoint.Name,
                Feature = endpoint.Feature,
                IsHealthy = isHealthy,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ErrorMessage = errorMessage
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            if (verbose)
            {
                Console.WriteLine($" Failed after {stopwatch.ElapsedMilliseconds}ms");
            }

            return new EndpointResult
            {
                EndpointName = endpoint.Name,
                Feature = endpoint.Feature,
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    private struct EndpointResult
    {
        public string EndpointName { get; init; }
        public string Feature { get; init; }
        public bool IsHealthy { get; init; }
        public long? ResponseTime { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}