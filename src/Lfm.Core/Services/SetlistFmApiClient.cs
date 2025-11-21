using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Lfm.Core.Models.SetlistFm;

namespace Lfm.Core.Services;

public interface ISetlistFmApiClient
{
    Task<SetlistSearchResponse> SearchSetlistsAsync(
        string artistName,
        string? cityName = null,
        string? countryCode = null,
        string? venueName = null,
        string? tourName = null,
        string? date = null,
        string? year = null,
        int page = 1);

    Task<Setlist> GetSetlistAsync(string setlistId);
}

public class SetlistFmApiClient : ISetlistFmApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SetlistFmApiClient> _logger;
    private readonly string _apiKey;
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly SemaphoreSlim _throttleLock = new(1, 1);
    private const int ThrottleDelayMs = 500; // 2 req/sec = 500ms
    private const string BaseUrl = "https://api.setlist.fm/rest/1.0/";

    public SetlistFmApiClient(
        HttpClient httpClient,
        ILogger<SetlistFmApiClient> logger,
        string apiKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiKey = !string.IsNullOrWhiteSpace(apiKey)
            ? apiKey
            : throw new ArgumentException("Setlist.fm API key is required", nameof(apiKey));

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
    }

    public async Task<SetlistSearchResponse> SearchSetlistsAsync(
        string artistName,
        string? cityName = null,
        string? countryCode = null,
        string? venueName = null,
        string? tourName = null,
        string? date = null,
        string? year = null,
        int page = 1)
    {
        if (string.IsNullOrWhiteSpace(artistName))
        {
            throw new ArgumentException("Artist name is required", nameof(artistName));
        }

        var queryParams = new Dictionary<string, string>
        {
            ["artistName"] = artistName,
            ["p"] = page.ToString()
        };

        if (!string.IsNullOrWhiteSpace(cityName))
            queryParams["cityName"] = cityName;

        if (!string.IsNullOrWhiteSpace(countryCode))
            queryParams["countryCode"] = countryCode;

        if (!string.IsNullOrWhiteSpace(venueName))
            queryParams["venueName"] = venueName;

        if (!string.IsNullOrWhiteSpace(tourName))
            queryParams["tourName"] = tourName;

        if (!string.IsNullOrWhiteSpace(date))
            queryParams["date"] = date;

        if (!string.IsNullOrWhiteSpace(year))
            queryParams["year"] = year;

        var queryString = string.Join("&", queryParams.Select(kvp =>
            $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));

        var endpoint = $"search/setlists?{queryString}";

        _logger.LogDebug("Searching setlists: {Endpoint}", endpoint);

        return await MakeRequestAsync<SetlistSearchResponse>(endpoint);
    }

    public async Task<Setlist> GetSetlistAsync(string setlistId)
    {
        if (string.IsNullOrWhiteSpace(setlistId))
        {
            throw new ArgumentException("Setlist ID is required", nameof(setlistId));
        }

        var endpoint = $"setlist/{setlistId}";

        _logger.LogDebug("Getting setlist: {Endpoint}", endpoint);

        return await MakeRequestAsync<Setlist>(endpoint);
    }

    private async Task<T> MakeRequestAsync<T>(string endpoint)
    {
        await ThrottleRequestAsync();

        try
        {
            var response = await _httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();

                // 404 is expected when no results are found, log at debug level
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogDebug(
                        "Setlist.fm API returned 404 (not found) for endpoint: {Endpoint}",
                        endpoint);
                }
                else
                {
                    _logger.LogError(
                        "Setlist.fm API error: {StatusCode} - {Error}",
                        response.StatusCode,
                        errorContent);
                }

                throw new HttpRequestException(
                    $"Setlist.fm API returned {response.StatusCode}: {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                throw new InvalidOperationException("Deserialization returned null");
            }

            return result;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            _logger.LogError(ex, "Error making request to {Endpoint}", endpoint);
            throw;
        }
    }

    private async Task ThrottleRequestAsync()
    {
        await _throttleLock.WaitAsync();
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            var millisecondsToWait = ThrottleDelayMs - (int)timeSinceLastRequest.TotalMilliseconds;

            if (millisecondsToWait > 0)
            {
                _logger.LogDebug("Throttling request for {Milliseconds}ms", millisecondsToWait);
                await Task.Delay(millisecondsToWait);
            }

            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _throttleLock.Release();
        }
    }
}
