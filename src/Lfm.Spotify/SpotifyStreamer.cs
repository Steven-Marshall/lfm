using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Lfm.Core.Configuration;
using Lfm.Core.Models;
using Lfm.Spotify.Models;

namespace Lfm.Spotify;

public class SpotifyStreamer : IPlaylistStreamer
{
    private readonly HttpClient _httpClient;
    private readonly SpotifyConfig _config;
    private readonly IConfigurationManager _configManager;
    private string? _accessToken;
    private DateTime _tokenExpiry;

    public string Name => "Spotify";

    public SpotifyStreamer(SpotifyConfig config, IConfigurationManager configManager)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _httpClient = new HttpClient();
    }

    public async Task<bool> IsAvailableAsync()
    {
        // Check if Spotify config is set up
        if (string.IsNullOrEmpty(_config.ClientId) || string.IsNullOrEmpty(_config.ClientSecret))
        {
            return false;
        }

        // Try to get a valid access token
        try
        {
            await EnsureValidAccessTokenAsync();
            return !string.IsNullOrEmpty(_accessToken);
        }
        catch
        {
            return false;
        }
    }

    public async Task<PlaylistStreamResult> QueueTracksAsync(List<Track> tracks, string? device = null)
    {
        var result = new PlaylistStreamResult();

        try
        {
            await EnsureValidAccessTokenAsync();

            Console.WriteLine($"🎵 Queueing {tracks.Count} tracks to Spotify...");

            // Check if playback is active, if not try to start it
            var hasActivePlayback = await EnsurePlaybackActiveAsync(device);

            var tracksToProcess = tracks.ToList();
            var startedPlayback = false;

            // If no active playback, start playing the first track
            if (!hasActivePlayback && tracksToProcess.Any())
            {
                var firstTrack = tracksToProcess.First();
                var firstSpotifyUri = await SearchSpotifyTrackAsync(firstTrack);

                if (firstSpotifyUri != null)
                {
                    var startSuccess = await StartPlaybackAsync(firstSpotifyUri, device);
                    if (startSuccess)
                    {
                        Console.WriteLine($"🎵 Started playing: {firstTrack.Artist.Name} - {firstTrack.Name}");
                        result.TracksFound++;
                        result.TracksProcessed++;
                        startedPlayback = true;
                        tracksToProcess.RemoveAt(0); // Remove the first track since we started playing it
                    }
                }
            }

            if (!hasActivePlayback && !startedPlayback)
            {
                Console.WriteLine("⚠️  Could not start Spotify playback automatically.");
                Console.WriteLine("    Please start playing any song in Spotify and try again.");
                result.Success = false;
                result.Message = "No active Spotify playback device found";
                return result;
            }

            // Queue remaining tracks
            foreach (var track in tracksToProcess)
            {
                var spotifyUri = await SearchSpotifyTrackAsync(track);
                if (spotifyUri != null)
                {
                    var queueSuccess = await AddToQueueAsync(spotifyUri);
                    if (queueSuccess)
                    {
                        result.TracksFound++;
                        Console.WriteLine($"✅ Queued: {track.Artist.Name} - {track.Name}");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Failed to queue: {track.Artist.Name} - {track.Name}");
                    }
                }
                else
                {
                    result.NotFoundTracks.Add($"{track.Artist.Name} - {track.Name}");
                    Console.WriteLine($"🔍 Not found: {track.Artist.Name} - {track.Name}");
                }

                result.TracksProcessed++;

                // Rate limiting
                if (_config.RateLimitDelayMs > 0)
                {
                    await Task.Delay(_config.RateLimitDelayMs);
                }
            }

            result.Success = result.TracksFound > 0;
            result.Message = $"Queued {result.TracksFound}/{result.TracksProcessed} tracks";

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error queueing tracks: {ex.Message}";
            return result;
        }
    }

    public async Task<List<PlaylistInfo>> GetUserPlaylistsAsync()
    {
        var playlists = new List<PlaylistInfo>();

        try
        {
            await EnsureValidAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            // Get current user ID once
            var currentUserId = await GetCurrentUserIdAsync();

            var url = "https://api.spotify.com/v1/me/playlists?limit=50";

            while (url != null)
            {
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var playlistResponse = JsonSerializer.Deserialize<SpotifyPlaylistsResponse>(json);
                    if (playlistResponse?.Items != null)
                    {
                        foreach (var playlist in playlistResponse.Items)
                        {
                            playlists.Add(new PlaylistInfo
                            {
                                Id = playlist.Id,
                                Name = playlist.Name,
                                TracksCount = playlist.Tracks.Total,
                                IsOwned = playlist.Owner.Id == currentUserId
                            });
                        }
                    }

                    url = playlistResponse?.Next; // Next page URL
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting playlists: {ex.Message}");
        }

        return playlists;
    }

    public async Task<bool> DeletePlaylistAsync(string playlistId)
    {
        try
        {
            await EnsureValidAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.DeleteAsync($"https://api.spotify.com/v1/playlists/{playlistId}/followers");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<SpotifyDevice>> GetDevicesAsync()
    {
        try
        {
            await EnsureValidAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.GetAsync("https://api.spotify.com/v1/me/player/devices");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var devices = JsonSerializer.Deserialize<SpotifyDevicesResponse>(json);
                return devices?.Devices ?? new List<SpotifyDevice>();
            }

            return new List<SpotifyDevice>();
        }
        catch
        {
            return new List<SpotifyDevice>();
        }
    }

    public async Task<PlaylistStreamResult> SavePlaylistAsync(List<Track> tracks, string playlistName, string? device = null)
    {
        var result = new PlaylistStreamResult();

        try
        {
            await EnsureValidAccessTokenAsync();

            Console.WriteLine($"🎵 Creating Spotify playlist '{playlistName}' with {tracks.Count} tracks...");

            // Get user ID
            var userId = await GetCurrentUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                result.Message = "Failed to get Spotify user ID";
                return result;
            }

            // Create playlist
            var playlist = await CreatePlaylistAsync(userId, playlistName);
            if (playlist == null)
            {
                result.Message = "Failed to create Spotify playlist";
                return result;
            }

            // Search for tracks and collect URIs
            var spotifyUris = new List<string>();
            foreach (var track in tracks)
            {
                var spotifyUri = await SearchSpotifyTrackAsync(track);
                if (spotifyUri != null)
                {
                    spotifyUris.Add(spotifyUri);
                    result.TracksFound++;
                    Console.WriteLine($"✅ Found: {track.Artist.Name} - {track.Name}");
                }
                else
                {
                    result.NotFoundTracks.Add($"{track.Artist.Name} - {track.Name}");
                    Console.WriteLine($"🔍 Not found: {track.Artist.Name} - {track.Name}");
                }

                result.TracksProcessed++;

                // Rate limiting
                if (_config.RateLimitDelayMs > 0)
                {
                    await Task.Delay(_config.RateLimitDelayMs);
                }
            }

            // Add tracks to playlist
            if (spotifyUris.Any())
            {
                await AddTracksToPlaylistAsync(playlist.Id, spotifyUris);
            }

            result.Success = result.TracksFound > 0;
            result.Message = $"Created playlist '{playlistName}' with {result.TracksFound}/{result.TracksProcessed} tracks";

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error creating playlist: {ex.Message}";
            return result;
        }
    }

    private async Task EnsureValidAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return; // Token is still valid
        }

        if (!string.IsNullOrEmpty(_config.RefreshToken))
        {
            // Try to refresh the token
            await RefreshAccessTokenAsync();
        }
        else
        {
            // Need to do initial OAuth flow
            await DoInitialOAuthFlowAsync();
        }
    }

    private async Task RefreshAccessTokenAsync()
    {
        var request = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", _config.RefreshToken)
        });

        var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var response = await _httpClient.PostAsync("https://accounts.spotify.com/api/token", request);
        var json = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var tokenResponse = JsonSerializer.Deserialize<SpotifyTokenResponse>(json);
            if (tokenResponse != null)
            {
                _accessToken = tokenResponse.AccessToken;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60); // 60 second buffer
            }
        }
        else
        {
            throw new Exception($"Failed to refresh Spotify token: {json}");
        }
    }

    private async Task DoInitialOAuthFlowAsync()
    {
        // Use 127.0.0.1 instead of localhost (localhost not allowed by Spotify)
        var redirectUri = "http://127.0.0.1:8888/callback";

        var authUrl = $"https://accounts.spotify.com/authorize?" +
                     $"client_id={_config.ClientId}&" +
                     $"response_type=code&" +
                     $"redirect_uri={HttpUtility.UrlEncode(redirectUri)}&" +
                     $"scope={HttpUtility.UrlEncode("user-modify-playback-state user-read-playback-state playlist-modify-private playlist-modify-public playlist-read-private playlist-read-collaborative")}";

        Console.WriteLine("\n🎵 Spotify Authentication Required!");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("1. Click or visit this URL to authorize the application:");
        Console.WriteLine($"   {authUrl}");
        Console.WriteLine();
        Console.WriteLine("2. After authorization, you'll be redirected to a page that won't load");
        Console.WriteLine("   (this is expected - the redirect URL will look like http://127.0.0.1:8888/callback?code=...)");
        Console.WriteLine();
        Console.WriteLine("3. Copy the entire URL from your browser's address bar and paste it here:");
        Console.WriteLine("   (or just copy the 'code' parameter value)");
        Console.WriteLine();
        Console.Write("Paste the redirect URL or authorization code: ");

        var input = Console.ReadLine();
        if (string.IsNullOrEmpty(input))
        {
            throw new Exception("Authorization code is required");
        }

        // Extract code from URL or use input directly
        var code = ExtractCodeFromInput(input);
        if (string.IsNullOrEmpty(code))
        {
            throw new Exception("Could not extract authorization code from input");
        }

        await ExchangeCodeForTokensAsync(code);
    }

    private string ExtractCodeFromInput(string input)
    {
        // If it looks like a URL, extract the code parameter
        if (input.Contains("code="))
        {
            var uri = new Uri(input.Contains("://") ? input : $"http://127.0.0.1:8888{input}");
            var query = HttpUtility.ParseQueryString(uri.Query);
            return query["code"] ?? string.Empty;
        }

        // Otherwise assume it's the code directly
        return input.Trim();
    }

    private async Task ExchangeCodeForTokensAsync(string code)
    {
        var request = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", "http://127.0.0.1:8888/callback")
        });

        var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var response = await _httpClient.PostAsync("https://accounts.spotify.com/api/token", request);
        var json = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var tokenResponse = JsonSerializer.Deserialize<SpotifyTokenResponse>(json);
            if (tokenResponse != null)
            {
                _accessToken = tokenResponse.AccessToken;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);

                // Save refresh token to config automatically
                if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                {
                    await SaveRefreshTokenAsync(tokenResponse.RefreshToken);
                    Console.WriteLine($"\n✅ Refresh token saved! Future commands won't require re-authorization.");
                }
            }
        }
        else
        {
            throw new Exception($"Failed to exchange code for tokens: {json}");
        }
    }

    private async Task<string?> SearchSpotifyTrackAsync(Track track)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        // Try precise search first
        var query = HttpUtility.UrlEncode($"artist:\"{track.Artist.Name}\" track:\"{track.Name}\"");
        var searchUrl = $"https://api.spotify.com/v1/search?q={query}&type=track&limit=1";

        var response = await _httpClient.GetAsync(searchUrl);
        var json = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var searchResponse = JsonSerializer.Deserialize<SpotifySearchResponse>(json);
            var firstTrack = searchResponse?.Tracks?.Items.FirstOrDefault();

            if (firstTrack != null)
            {
                return firstTrack.Uri;
            }
        }

        // Try loose search if precise search failed and fallback is enabled
        if (_config.FallbackToLooseSearch)
        {
            query = HttpUtility.UrlEncode($"{track.Artist.Name} {track.Name}");
            searchUrl = $"https://api.spotify.com/v1/search?q={query}&type=track&limit=1";

            response = await _httpClient.GetAsync(searchUrl);
            json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var searchResponse = JsonSerializer.Deserialize<SpotifySearchResponse>(json);
                return searchResponse?.Tracks?.Items.FirstOrDefault()?.Uri;
            }
        }

        return null;
    }

    private async Task<bool> AddToQueueAsync(string trackUri)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var queueUrl = $"https://api.spotify.com/v1/me/player/queue?uri={HttpUtility.UrlEncode(trackUri)}";
        var response = await _httpClient.PostAsync(queueUrl, null);

        if (!response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // First track, provide helpful message
            if (!_playbackWarningShown)
            {
                Console.WriteLine("⚠️  No active Spotify playback detected. Please:");
                Console.WriteLine("    1. Open Spotify on your device");
                Console.WriteLine("    2. Start playing any song");
                Console.WriteLine("    3. Try the command again");
                _playbackWarningShown = true;
            }
        }

        return response.IsSuccessStatusCode;
    }

    private bool _playbackWarningShown = false;

    private async Task<bool> EnsurePlaybackActiveAsync(string? device = null)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        // First check if playback is already active
        var playbackResponse = await _httpClient.GetAsync("https://api.spotify.com/v1/me/player");

        if (playbackResponse.IsSuccessStatusCode)
        {
            var json = await playbackResponse.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(json))
            {
                // Playback is active
                return true;
            }
        }

        // No active playback, try to get available devices
        var devicesResponse = await _httpClient.GetAsync("https://api.spotify.com/v1/me/player/devices");
        if (!devicesResponse.IsSuccessStatusCode)
        {
            return false;
        }

        var devicesJson = await devicesResponse.Content.ReadAsStringAsync();
        var devices = JsonSerializer.Deserialize<SpotifyDevicesResponse>(devicesJson);

        if (devices?.Devices == null || !devices.Devices.Any())
        {
            Console.WriteLine("ℹ️  No Spotify devices found. Please open Spotify on any device.");
            return false;
        }

        // Show all available devices
        if (devices.Devices.Count > 1)
        {
            Console.WriteLine($"📱 Found {devices.Devices.Count} Spotify devices:");
            foreach (var dev in devices.Devices)
            {
                var status = dev.IsActive ? " (active)" : "";
                Console.WriteLine($"    • {dev.Name} ({dev.Type}){status}");
            }
        }

        // Device selection priority: CLI parameter > config default > active device > smart prioritization
        SpotifyDevice? selectedDevice = null;

        // 1. Check if specific device was requested via CLI parameter
        if (!string.IsNullOrEmpty(device))
        {
            selectedDevice = devices.Devices.FirstOrDefault(d =>
                d.Name.Equals(device, StringComparison.OrdinalIgnoreCase));

            if (selectedDevice == null)
            {
                Console.WriteLine($"⚠️  Requested device '{device}' not found. Available devices:");
                foreach (var dev in devices.Devices)
                {
                    Console.WriteLine($"    • {dev.Name}");
                }
                // Continue with fallback logic
            }
        }

        // 2. Check config default device if no CLI device or CLI device not found
        if (selectedDevice == null && !string.IsNullOrEmpty(_config.DefaultDevice))
        {
            selectedDevice = devices.Devices.FirstOrDefault(d =>
                d.Name.Equals(_config.DefaultDevice, StringComparison.OrdinalIgnoreCase));

            if (selectedDevice != null)
            {
                Console.WriteLine($"📱 Using config default device: {selectedDevice.Name}");
            }
        }

        // 3. Check for currently active device
        if (selectedDevice == null)
        {
            selectedDevice = devices.Devices.FirstOrDefault(d => d.IsActive);
        }

        // 4. Smart prioritization: Computer > Smartphone > Speaker > other
        if (selectedDevice == null)
        {
            selectedDevice = devices.Devices.FirstOrDefault(d => d.Type.Equals("Computer", StringComparison.OrdinalIgnoreCase))
                        ?? devices.Devices.FirstOrDefault(d => d.Type.Equals("Smartphone", StringComparison.OrdinalIgnoreCase))
                        ?? devices.Devices.FirstOrDefault(d => d.Type.Equals("Speaker", StringComparison.OrdinalIgnoreCase))
                        ?? devices.Devices.First();
        }

        var activeDevice = selectedDevice;

        Console.WriteLine($"🎵 Using device: {activeDevice.Name} ({activeDevice.Type})");

        // Try to start playback on the device
        if (!activeDevice.IsActive)
        {
            // Transfer playback to this device
            var transferRequest = new
            {
                device_ids = new[] { activeDevice.Id },
                play = false  // Don't auto-play
            };

            var transferJson = JsonSerializer.Serialize(transferRequest);
            var transferContent = new StringContent(transferJson, Encoding.UTF8, "application/json");

            var transferResponse = await _httpClient.PutAsync("https://api.spotify.com/v1/me/player", transferContent);

            if (transferResponse.IsSuccessStatusCode || transferResponse.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                Console.WriteLine($"✅ Activated device: {activeDevice.Name}");
                await Task.Delay(1000); // Give Spotify a moment to activate the device
                return true;
            }
        }

        return activeDevice.IsActive;
    }

    private async Task<string?> GetCurrentUserIdAsync()
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.GetAsync("https://api.spotify.com/v1/me");
        var json = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var user = JsonSerializer.Deserialize<SpotifyUser>(json);
            return user?.Id;
        }

        return null;
    }

    private async Task<SpotifyPlaylist?> CreatePlaylistAsync(string userId, string playlistName)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var request = new CreatePlaylistRequest
        {
            Name = playlistName,
            Description = "Created by lfm CLI tool",
            Public = false
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"https://api.spotify.com/v1/users/{userId}/playlists", content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            return JsonSerializer.Deserialize<SpotifyPlaylist>(responseJson);
        }

        return null;
    }

    private async Task<bool> AddTracksToPlaylistAsync(string playlistId, List<string> trackUris)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var request = new AddTracksRequest { Uris = trackUris };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"https://api.spotify.com/v1/playlists/{playlistId}/tracks", content);
        return response.IsSuccessStatusCode;
    }

    private async Task SaveRefreshTokenAsync(string refreshToken)
    {
        try
        {
            var config = await _configManager.LoadAsync();
            config.Spotify.RefreshToken = refreshToken;
            await _configManager.SaveAsync(config);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not save refresh token: {ex.Message}");
            Console.WriteLine($"You may need to re-authorize on next use.");
        }
    }

    private async Task<bool> StartPlaybackAsync(string trackUri, string? device = null)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        try
        {
            // Prepare the play request
            var playRequest = new
            {
                uris = new[] { trackUri }
            };

            // If a specific device is requested, include it
            string playUrl = "https://api.spotify.com/v1/me/player/play";
            if (!string.IsNullOrEmpty(device))
            {
                // First try to get the device ID
                var devices = await GetDevicesAsync();
                var targetDevice = devices.FirstOrDefault(d =>
                    d.Name.Equals(device, StringComparison.OrdinalIgnoreCase));

                if (targetDevice != null)
                {
                    playUrl += $"?device_id={targetDevice.Id}";
                }
            }

            var json = JsonSerializer.Serialize(playRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(playUrl, content);

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️  Failed to start playback: {response.StatusCode} - {errorContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error starting playback: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}