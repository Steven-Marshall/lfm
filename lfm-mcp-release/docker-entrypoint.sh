#!/bin/bash
set -e

echo "========================================="
echo "LFM MCP Server - Docker Startup"
echo "========================================="

# Configuration directory
CONFIG_DIR="/app/config"
CONFIG_FILE="$CONFIG_DIR/config.json"

# Ensure config directory exists
mkdir -p "$CONFIG_DIR"

# If Spotify refresh token is provided via environment variable, write to config
if [ -n "$SPOTIFY_REFRESH_TOKEN" ]; then
  echo "Configuring Spotify refresh token..."

  # Create or update config.json with Spotify settings
  cat > "$CONFIG_FILE" <<EOF
{
  "LastFmUsername": "${LASTFM_USERNAME:-}",
  "LastFmApiKey": "${LASTFM_API_KEY:-}",
  "Spotify": {
    "ClientId": "${SPOTIFY_CLIENT_ID:-}",
    "ClientSecret": "${SPOTIFY_CLIENT_SECRET:-}",
    "RefreshToken": "$SPOTIFY_REFRESH_TOKEN"
  },
  "CacheDurationMinutes": ${CACHE_DURATION_MINUTES:-60},
  "ApiThrottleMs": ${API_THROTTLE_MS:-200},
  "ParallelApiCalls": ${PARALLEL_API_CALLS:-5}
}
EOF
  echo "✓ Spotify configuration written to $CONFIG_FILE"
fi

# If Sonos configuration is provided, add to config
if [ -n "$SONOS_API_URL" ]; then
  echo "Configuring Sonos integration..."

  # Use jq to merge Sonos config if config file exists, otherwise create new
  if [ -f "$CONFIG_FILE" ]; then
    TEMP_FILE=$(mktemp)
    jq --arg url "$SONOS_API_URL" \
       --arg room "${SONOS_DEFAULT_ROOM:-}" \
       '.Sonos = {
          "HttpApiBaseUrl": $url,
          "DefaultRoom": $room,
          "TimeoutMs": 5000,
          "RoomCacheDurationMinutes": 5
        }' "$CONFIG_FILE" > "$TEMP_FILE"
    mv "$TEMP_FILE" "$CONFIG_FILE"
  fi
  echo "✓ Sonos configuration added to $CONFIG_FILE"
fi

# Set LFM config path environment variable
export LFM_CONFIG_PATH="$CONFIG_DIR"
export LFM_CACHE_PATH="/app/cache"

echo "Configuration:"
echo "  Config dir: $LFM_CONFIG_PATH"
echo "  Cache dir:  $LFM_CACHE_PATH"
echo "  HTTP port:  ${HTTP_PORT:-8002}"
echo "  Auth:       ${AUTH_TOKEN:+Enabled}"
echo "========================================="

# Start the MCP HTTP server
exec node server-http.js
