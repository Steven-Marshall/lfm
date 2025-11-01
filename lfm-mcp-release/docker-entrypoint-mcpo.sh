#!/bin/bash
set -e

echo "========================================="
echo "LFM MCPO Server - Docker Startup"
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

# Copy config to where lfm CLI expects it (/root/.config/lfm/ on Linux)
LFM_CONFIG_DIR="/root/.config/lfm"
mkdir -p "$LFM_CONFIG_DIR"
cp "$CONFIG_FILE" "$LFM_CONFIG_DIR/config.json"

# Set LFM cache path
export LFM_CACHE_PATH="$CACHE_DIR"

# Ensure cache directory exists
mkdir -p "$CACHE_DIR"

echo "Configuration:"
echo "  Config file: $CONFIG_FILE"
echo "  Cache dir:   $CACHE_DIR"
echo "  MCPO port:   ${MCPO_PORT:-8001}"
echo "========================================="

# Start MCPO wrapping the stdio MCP server
exec mcpo --port "${MCPO_PORT:-8001}" -- node /app/server.js
