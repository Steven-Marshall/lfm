# Spotify Integration Deep Dive

## How Spotify Integration Currently Works

### Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│  LFM CLI (C# .NET)                                           │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ SpotifyStreamer.cs                                     │ │
│  │                                                        │ │
│  │  1. OAuth Flow (one-time or when refresh fails)       │ │
│  │  2. Token Refresh (automatic, uses refresh_token)     │ │
│  │  3. Playback Control (play, queue, pause, etc.)       │ │
│  └────────────────────────────────────────────────────────┘ │
│           │                                                  │
│           │ HTTPS API calls                                 │
│           ▼                                                  │
└───────────┼──────────────────────────────────────────────────┘
            │
            ▼
┌───────────────────────────────────────────────────────────────┐
│  Spotify Web API (accounts.spotify.com, api.spotify.com)     │
└───────────────────────────────────────────────────────────────┘
```

---

## OAuth Flow Explained (Step-by-Step)

### Scenario 1: First Time Setup (No Refresh Token)

```
User runs: lfm play "Pink Floyd" --track "Money"
                    │
                    ▼
            SpotifyStreamer detects:
            - No refresh token in config
            - OR refresh token is invalid/expired
                    │
                    ▼
        ┌───────────────────────────────┐
        │ DoInitialOAuthFlowAsync()     │
        └───────────────────────────────┘
                    │
    ┌───────────────┴────────────────────────┐
    │                                        │
    ▼                                        ▼
1. Generate authorization URL          2. Print to console:
   with redirect_uri:                     "Visit this URL: https://..."
   http://127.0.0.1:8888/callback         "Paste the redirect URL here:"

                                       3. User clicks URL
                                          Opens browser
                                          Logs into Spotify
                                          Authorizes app

                                       4. Spotify redirects to:
                                          http://127.0.0.1:8888/callback?code=ABC123...
                                          (Page won't load - expected!)

                                       5. User copies URL
                                          Pastes into console

    ┌───────────────┴────────────────────────┐
    │                                        │
    ▼                                        ▼
6. Extract code from URL              7. Exchange code for tokens
   code = "ABC123..."                    POST to accounts.spotify.com/api/token
                                         Body: {
                                           grant_type: "authorization_code",
                                           code: "ABC123...",
                                           redirect_uri: "http://127.0.0.1:8888/callback"
                                         }
                                         Auth: Basic base64(clientId:clientSecret)

                                       8. Spotify returns:
                                          {
                                            "access_token": "xyz789...",     (expires in 1 hour)
                                            "refresh_token": "refresh456...", (never expires unless revoked)
                                            "expires_in": 3600
                                          }

                                       9. Save refresh_token to config.json:
                                          %APPDATA%/lfm/config.json
                                          {
                                            "spotify": {
                                              "refreshToken": "refresh456..."
                                            }
                                          }

                                      10. Store access_token in memory
                                          (not saved to disk - only valid 1 hour)

                                      11. Continue with playback...
```

### Scenario 2: Subsequent Usage (Refresh Token Exists)

```
User runs: lfm play "The Beatles" --track "Here Comes The Sun"
                    │
                    ▼
            SpotifyStreamer detects:
            - Refresh token exists in config
            - Access token is expired (or missing)
                    │
                    ▼
        ┌───────────────────────────────┐
        │ RefreshAccessTokenAsync()     │
        └───────────────────────────────┘
                    │
                    ▼
1. POST to accounts.spotify.com/api/token
   Body: {
     grant_type: "refresh_token",
     refresh_token: "refresh456..."
   }
   Auth: Basic base64(clientId:clientSecret)

                    │
                    ▼
2. Spotify returns new access_token:
   {
     "access_token": "newxyz123...",  (new token, expires in 1 hour)
     "expires_in": 3600
   }
   Note: refresh_token is NOT returned (keep using old one)

                    │
                    ▼
3. Store new access_token in memory
   Update _tokenExpiry timestamp

                    │
                    ▼
4. Continue with playback...
```

---

## Token Lifecycle

```
┌─────────────────────────────────────────────────────────────┐
│  refresh_token                                              │
│  - Never expires (unless revoked by user)                   │
│  - Stored in: %APPDATA%/lfm/config.json                     │
│  - Used to: Get new access_tokens                           │
│  - Required scope: user-modify-playback-state, etc.         │
└─────────────────────────────────────────────────────────────┘
                        │
                        │ Used to get ▼
                        │
┌─────────────────────────────────────────────────────────────┐
│  access_token                                               │
│  - Expires in: 1 hour                                       │
│  - Stored in: Memory only (never saved to disk)             │
│  - Used for: All Spotify API calls                          │
│  - Automatically refreshed when expired                     │
└─────────────────────────────────────────────────────────────┘
```

---

## Current Implementation Details

### Files Involved

1. **`LfmConfig.cs`** - Configuration model
   ```csharp
   public class SpotifyConfig {
     public string ClientId { get; set; }
     public string ClientSecret { get; set; }
     public string RefreshToken { get; set; }  // ← Persisted
     public string DefaultDevice { get; set; }
   }
   ```

2. **`SpotifyStreamer.cs`** - OAuth & Playback
   - `EnsureValidAccessTokenAsync()` - Check if token needs refresh
   - `RefreshAccessTokenAsync()` - Get new access token
   - `DoInitialOAuthFlowAsync()` - Full OAuth flow (one-time)
   - `SaveRefreshTokenAsync()` - Save to config.json
   - `PlayNowAsync()`, `QueueTracksAsync()`, etc. - Playback methods

3. **`config.json`** - User's config file
   ```json
   {
     "spotify": {
       "clientId": "your-app-client-id",
       "clientSecret": "your-app-secret",
       "refreshToken": "BQD...xyz",  // ← This is the key!
       "defaultDevice": "DESKTOP-ABC123"
     }
   }
   ```

### Key Insight: Why This Works Locally

The refresh token is stored in `%APPDATA%/lfm/config.json`:
- **Windows:** `C:\Users\steve\AppData\Roaming\lfm\config.json`
- **Linux:** `~/.config/lfm/config.json`

Every time you run `lfm`, it:
1. Reads config.json
2. Finds refresh_token
3. Exchanges it for a fresh access_token
4. Makes API calls
5. No user interaction needed!

---

## What Changes with SSE/Docker?

### Challenge 1: Interactive OAuth Flow

**Current (local CLI):**
```
User runs: lfm play ...
CLI prints: "Visit this URL: https://..."
CLI waits: for user to paste redirect URL
User pastes: http://127.0.0.1:8888/callback?code=ABC...
CLI saves: refresh_token to config.json
```

**With Docker (remote server):**
```
Problem: Server has no console for user interaction!
Problem: redirect_uri http://127.0.0.1:8888/callback won't work
         (127.0.0.1 inside container ≠ 127.0.0.1 on user's browser)
```

### Challenge 2: Config Storage Location

**Current:**
```
Config: %APPDATA%/lfm/config.json (single user)
Cache:  %LOCALAPPDATA%/lfm/cache/ (single user)
```

**With Docker:**
```
Config: /root/.config/lfm/config.json (inside container)
Cache:  /root/.cache/lfm/ (inside container)

Volume mounted: Docker volume persists data
But: Still single-user (no multi-tenancy yet)
```

---

## Solution Strategy

### Option A: Pre-Configure Refresh Token (Recommended for Now)

**One-time setup on Windows:**
```bash
# 1. Run OAuth flow locally
lfm config set-spotify-client-id "your-id"
lfm config set-spotify-client-secret "your-secret"
lfm play "test" --track "test"  # Triggers OAuth flow

# 2. Copy refresh token from config
cat %APPDATA%/lfm/config.json | grep refreshToken

# 3. Set it in Docker environment
# Add to docker-compose.yml:
environment:
  - SPOTIFY_REFRESH_TOKEN=BQD...xyz
```

**Startup script in container:**
```javascript
// Before starting server
if (process.env.SPOTIFY_REFRESH_TOKEN) {
  const config = loadConfig();
  config.spotify.refreshToken = process.env.SPOTIFY_REFRESH_TOKEN;
  saveConfig(config);
}
```

**Pros:**
- ✅ Works immediately
- ✅ No web UI needed
- ✅ Refresh token rarely changes

**Cons:**
- ⚠️ Manual step (copy token)
- ⚠️ If token revoked, need to redo locally

### Option B: Web-Based OAuth Flow (Future Enhancement)

**Add OAuth endpoints to server-http.js:**

```javascript
// GET /spotify/authorize
app.get('/spotify/authorize', (req, res) => {
  const authUrl = `https://accounts.spotify.com/authorize?` +
    `client_id=${SPOTIFY_CLIENT_ID}&` +
    `redirect_uri=http://spark:8002/spotify/callback&` +
    `response_type=code&` +
    `scope=user-modify-playback-state...`;

  res.redirect(authUrl);
});

// GET /spotify/callback
app.get('/spotify/callback', async (req, res) => {
  const code = req.query.code;

  // Exchange code for tokens
  const tokens = await exchangeCodeForTokens(code);

  // Save refresh_token to config
  const config = loadConfig();
  config.spotify.refreshToken = tokens.refresh_token;
  saveConfig(config);

  res.send('✅ Spotify authorized! You can close this window.');
});
```

**User flow:**
1. Visit: `http://spark:8002/spotify/authorize`
2. Browser redirects to Spotify
3. User authorizes
4. Spotify redirects back to `http://spark:8002/spotify/callback`
5. Server saves refresh_token automatically
6. Done!

**Pros:**
- ✅ User-friendly (click a link)
- ✅ No manual token copying
- ✅ Works from any device on VPN

**Cons:**
- ⚠️ Requires Spotify app redirect_uri update
- ⚠️ Adds complexity (OAuth endpoints)
- ⚠️ Need HTTPS in production (Spotify requirement for non-localhost)

---

## Recommendation for Your Setup

### Phase 1 (Now): Option A - Pre-Configure Token

**Why:**
- ✅ You're single-user (just you)
- ✅ Refresh token rarely changes
- ✅ Gets you working immediately
- ✅ Can enhance later if needed

**Steps:**
1. Run OAuth locally (already done if you've used Spotify before)
2. Extract refresh token from `%APPDATA%/lfm/config.json`
3. Add to `.env` file for Docker:
   ```bash
   SPOTIFY_REFRESH_TOKEN=BQD...your-token-here...
   ```
4. Update docker-compose to inject token on startup

**Time to implement:** 15 minutes

### Phase 2 (Future): Option B - Web OAuth

**When:**
- If you want to demo to others
- If your refresh token gets revoked
- If you want multiple users (far future)

**Time to implement:** 2-3 hours

---

## What About Token Security in Docker?

### Current Security Model

**Local (Windows):**
- Config stored in user's AppData folder
- File permissions: user-only read/write
- Not shared across users

**Docker (Spark):**
- Config in Docker volume (persistent)
- File permissions: root-only inside container
- Volume not accessible from host (unless mounted)
- Environment variable: visible in `docker inspect` (⚠️)

### Recommended Security Approach

**Option 1: Environment Variable (Simple)**
```yaml
# docker-compose.yml
environment:
  - SPOTIFY_REFRESH_TOKEN=${SPOTIFY_REFRESH_TOKEN}
```

```bash
# .env (add to .gitignore!)
SPOTIFY_REFRESH_TOKEN=BQD...
```

**Pros:** Simple, works immediately
**Cons:** Visible in docker inspect

**Option 2: Docker Secrets (Better)**
```bash
# Create secret
echo "BQD..." | docker secret create spotify_refresh_token -

# Use in compose
secrets:
  spotify_refresh_token:
    external: true
```

**Pros:** More secure, not visible in docker inspect
**Cons:** Requires Docker Swarm mode

**Option 3: Encrypted Config File (Best)**
```bash
# Encrypt config.json before storing in volume
# Decrypt on container startup with passphrase
```

**Pros:** Most secure
**Cons:** Complex, adds startup overhead

**Recommendation:** Start with Option 1 (env var), upgrade to Option 2 if you want better security.

---

## Config/Cache in Docker

### Current Layout (Local)

```
Windows:
C:\Users\steve\AppData\Roaming\lfm\
├── config.json                    (25 KB)
└── cache\
    ├── user.smarshal.artists.json
    ├── user.smarshal.tracks.json
    └── ... (thousands of cache files)

C:\Users\steve\AppData\Local\lfm\
└── cache\                         (100+ MB)
```

### Docker Layout

```
Container:
/root/.config/lfm/
└── config.json                    ← Config volume

/root/.cache/lfm/
└── ...                           ← Cache volume

docker-compose.yml:
volumes:
  lfm-config:    → /root/.config/lfm
  lfm-cache:     → /root/.cache/lfm
```

**Volume persistence:**
- Survives container restarts
- Survives container rebuilds (docker-compose up --build)
- Lost if volume is deleted (docker volume rm)

**Backup strategy:**
```bash
# Backup volumes
docker run --rm -v lfm-config:/data -v $(pwd):/backup \
  alpine tar czf /backup/lfm-config-backup.tar.gz -C /data .

docker run --rm -v lfm-cache:/data -v $(pwd):/backup \
  alpine tar czf /backup/lfm-cache-backup.tar.gz -C /data .

# Restore volumes
docker run --rm -v lfm-config:/data -v $(pwd):/backup \
  alpine tar xzf /backup/lfm-config-backup.tar.gz -C /data
```

---

## Impact on MCP Tools

### No Changes to Tool Handlers!

**Good news:** The tool handlers in `server.js` just call the CLI:

```javascript
const cmdArgs = ['play', '--track', trackName, '--artist', artistName];
const output = await executeLfmCommand(cmdArgs);
```

The CLI (`lfm` binary) handles:
- Reading config.json
- Finding refresh_token
- Refreshing access_token
- Making Spotify API calls

**This means:**
- ✅ No changes to server-core.js for Spotify
- ✅ No changes to MCP tool definitions
- ✅ Works exactly the same remotely as locally

**Only requirement:**
- Config file must have valid refresh_token
- (Set via Option A or Option B above)

---

## Summary Table

| Aspect | Local (Current) | Docker (SSE) | Change Required? |
|--------|----------------|--------------|------------------|
| OAuth Flow | Interactive CLI | Pre-configure token | ✅ Yes (one-time) |
| Refresh Token | config.json (local) | config.json (volume) | ✅ Yes (copy token) |
| Access Token | Memory (auto-refresh) | Memory (auto-refresh) | ❌ No change |
| Config Path | %APPDATA%/lfm | /root/.config/lfm | ❌ No change (handled by CLI) |
| Cache Path | %LOCALAPPDATA%/lfm | /root/.cache/lfm | ❌ No change (handled by CLI) |
| Playback API Calls | Direct from CLI | Direct from CLI | ❌ No change |
| Device Selection | --device flag | --device flag | ❌ No change |
| MCP Tool Handlers | Call CLI binary | Call CLI binary | ❌ No change |

**Bottom line:** Spotify integration "just works" once you copy the refresh token to Docker config. The CLI handles everything else!

---

## Next Steps

1. **Review this plan** ✓ (you're here)
2. **Approve refactor approach** (DETAILED_REFACTOR_PLAN.md)
3. **I implement Phase 1-3** (server-core.js + server-http.js)
4. **Test locally** (Windows, SSE transport)
5. **Add Spotify token injection** (15 minutes)
6. **Docker deployment** (Phase 5-6)
7. **Test from all clients** (Claude Desktop, Code, Open WebUI)

Ready to proceed?
