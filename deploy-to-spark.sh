#!/bin/bash
# Deploy LFM MCP Server to Spark (ARM64)
#
# Usage: ./deploy-to-spark.sh [options]
#   --rebuild       Force rebuild of Docker container
#   --no-config     Don't sync config.json (keeps remote config unchanged)
#   --export-first  Run 'lfm config export --to-docker' before syncing
#
# Prerequisites:
#   - SSH access to spark (ssh spark should work without password)
#   - rsync installed locally

set -e

SPARK_HOST="spark"
SPARK_DIR="~/lfm-deployment"
LOCAL_DIR="$(cd "$(dirname "$0")" && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${GREEN}=========================================${NC}"
echo -e "${GREEN}LFM MCP Deployment to Spark${NC}"
echo -e "${GREEN}=========================================${NC}"
echo ""

# Parse arguments
REBUILD=false
SYNC_CONFIG=true
EXPORT_FIRST=false

for arg in "$@"; do
    case $arg in
        --rebuild)
            REBUILD=true
            ;;
        --no-config)
            SYNC_CONFIG=false
            ;;
        --export-first)
            EXPORT_FIRST=true
            ;;
    esac
done

echo -e "${BLUE}Options:${NC}"
echo "  Rebuild container: $REBUILD"
echo "  Sync config.json:  $SYNC_CONFIG"
echo "  Export config:     $EXPORT_FIRST"
echo ""

# Step 0: Export config if requested
if [ "$EXPORT_FIRST" = true ]; then
    echo -e "${GREEN}[0/5] Exporting local config to Docker config...${NC}"
    if command -v lfm &> /dev/null; then
        lfm config export --to-docker
    elif [ -f "$LOCAL_DIR/publish/win-x64/lfm.exe" ]; then
        "$LOCAL_DIR/publish/win-x64/lfm.exe" config export --to-docker
    else
        echo -e "${RED}  Error: lfm command not found. Please run export manually.${NC}"
        exit 1
    fi
    echo ""
fi

# Step 1: Sync source files
echo -e "${GREEN}[1/5] Syncing source files to Spark...${NC}"

# Check if rsync is available, otherwise use scp
if command -v rsync &> /dev/null; then
    # Sync C# source (for CLI build)
    echo "  Syncing src/ with rsync..."
    rsync -avz --delete \
        --exclude 'bin/' \
        --exclude 'obj/' \
        "$LOCAL_DIR/src/" \
        "$SPARK_HOST:$SPARK_DIR/src/"

    # Build rsync command for MCP release
    RSYNC_EXCLUDES="--exclude 'node_modules/' --exclude '.env'"
    if [ "$SYNC_CONFIG" = false ]; then
        RSYNC_EXCLUDES="$RSYNC_EXCLUDES --exclude 'config.json'"
    fi

    # Sync MCP server files
    echo "  Syncing lfm-mcp-release/ with rsync..."
    eval rsync -avz --delete \
        $RSYNC_EXCLUDES \
        "$LOCAL_DIR/lfm-mcp-release/" \
        "$SPARK_HOST:$SPARK_DIR/lfm-mcp-release/"
else
    # Fallback to scp for Windows
    echo "  Using scp (rsync not available)..."

    # Sync C# source
    echo "  Syncing src/ ..."
    scp -r "$LOCAL_DIR/src/Lfm.Cli" "$SPARK_HOST:$SPARK_DIR/src/"
    scp -r "$LOCAL_DIR/src/Lfm.Core" "$SPARK_HOST:$SPARK_DIR/src/"
    scp -r "$LOCAL_DIR/src/Lfm.Spotify" "$SPARK_HOST:$SPARK_DIR/src/"
    scp -r "$LOCAL_DIR/src/Lfm.Sonos" "$SPARK_HOST:$SPARK_DIR/src/"

    # Sync MCP server files (key files only, skip node_modules)
    echo "  Syncing lfm-mcp-release/ ..."
    scp "$LOCAL_DIR/lfm-mcp-release/server-core.js" "$SPARK_HOST:$SPARK_DIR/lfm-mcp-release/"
    scp "$LOCAL_DIR/lfm-mcp-release/server-http.js" "$SPARK_HOST:$SPARK_DIR/lfm-mcp-release/"
    scp "$LOCAL_DIR/lfm-mcp-release/server.js" "$SPARK_HOST:$SPARK_DIR/lfm-mcp-release/"
    scp "$LOCAL_DIR/lfm-mcp-release/package.json" "$SPARK_HOST:$SPARK_DIR/lfm-mcp-release/"
    scp "$LOCAL_DIR/lfm-mcp-release/Dockerfile" "$SPARK_HOST:$SPARK_DIR/lfm-mcp-release/"
    scp "$LOCAL_DIR/lfm-mcp-release/docker-compose.yml" "$SPARK_HOST:$SPARK_DIR/lfm-mcp-release/"
    scp "$LOCAL_DIR/lfm-mcp-release/docker-entrypoint.sh" "$SPARK_HOST:$SPARK_DIR/lfm-mcp-release/"
    scp "$LOCAL_DIR/lfm-mcp-release/lfm-guidelines.md" "$SPARK_HOST:$SPARK_DIR/lfm-mcp-release/"

    # Sync config if requested
    if [ "$SYNC_CONFIG" = true ]; then
        echo "  Syncing config.json ..."
        scp "$LOCAL_DIR/lfm-mcp-release/config.json" "$SPARK_HOST:$SPARK_DIR/lfm-mcp-release/"
    fi
fi

echo -e "${GREEN}  Done!${NC}"
echo ""

# Step 2: Check current container status
echo -e "${GREEN}[2/5] Checking container status...${NC}"
ssh "$SPARK_HOST" "cd $SPARK_DIR/lfm-mcp-release && docker compose ps"
echo ""

# Step 3: Rebuild or skip
if [ "$REBUILD" = true ]; then
    echo -e "${GREEN}[3/5] Rebuilding Docker container...${NC}"
    ssh "$SPARK_HOST" "cd $SPARK_DIR/lfm-mcp-release && docker compose build --no-cache"
else
    echo -e "${YELLOW}[3/5] Skipping rebuild (use --rebuild to force)${NC}"
fi
echo ""

# Step 4: Restart container
if [ "$REBUILD" = true ]; then
    echo -e "${GREEN}[4/5] Restarting container with new image...${NC}"
    ssh "$SPARK_HOST" "cd $SPARK_DIR/lfm-mcp-release && docker compose up -d --force-recreate"
else
    echo -e "${GREEN}[4/5] Restarting container...${NC}"
    ssh "$SPARK_HOST" "cd $SPARK_DIR/lfm-mcp-release && docker compose restart"
fi
echo ""

# Step 5: Verify deployment
echo -e "${GREEN}[5/5] Verifying deployment...${NC}"
sleep 3
ssh "$SPARK_HOST" "cd $SPARK_DIR/lfm-mcp-release && docker compose ps"
echo ""

# Check container logs for errors
echo -e "${GREEN}Recent container logs:${NC}"
ssh "$SPARK_HOST" "cd $SPARK_DIR/lfm-mcp-release && docker compose logs --tail=10"
echo ""

echo -e "${GREEN}=========================================${NC}"
echo -e "${GREEN}Deployment complete!${NC}"
echo -e "${GREEN}=========================================${NC}"
echo ""
echo "MCP Server URL: https://spark.taild15745.ts.net/mcp"
echo ""
echo "To test:"
echo "  curl https://spark.taild15745.ts.net/health"
echo ""
echo -e "${YELLOW}NOTE: After redeploying, MCP clients need to reconnect:${NC}"
echo "  - Claude.ai: Disconnect and reconnect the custom connector"
echo "  - Claude Code: Run /mcp to reconnect"
echo ""
