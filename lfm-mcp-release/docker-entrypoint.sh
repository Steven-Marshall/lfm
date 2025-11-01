#!/bin/bash
set -e

echo "========================================="
echo "LFM MCP Server - Docker Startup"
echo "========================================="

# Configuration paths
CONFIG_DIR="/app/config"
CONFIG_FILE="$CONFIG_DIR/config.json"
CACHE_DIR="/app/cache"

# Verify config file exists
if [ ! -f "$CONFIG_FILE" ]; then
  echo "❌ ERROR: config.json not found at $CONFIG_FILE"
  echo "   Ensure config.json is mounted via docker-compose volume"
  exit 1
fi

# Set LFM config and cache paths
export LFM_CONFIG_PATH="$CONFIG_DIR"
export LFM_CACHE_PATH="$CACHE_DIR"

# Ensure cache directory exists
mkdir -p "$CACHE_DIR"

echo "Configuration:"
echo "  Config file: $CONFIG_FILE"
echo "  Cache dir:   $CACHE_DIR"
echo "  HTTP port:   ${HTTP_PORT:-8002}"
echo "  Auth:        ${AUTH_TOKEN:+Enabled}"
echo "========================================="

# Start the MCP HTTP server
exec node server-http.js
