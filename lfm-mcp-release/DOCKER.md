# LFM MCP Server - Docker Deployment

This directory contains Docker configuration for deploying the LFM MCP server with HTTP/SSE transport, enabling remote access from Claude Desktop, Claude Code, and browser-based LLM interfaces.

## Architecture

The Docker deployment provides two interfaces to the same LFM CLI backend:

1. **HTTP/SSE Transport** (`server-http.js`) - Port 8002
   - For Claude Desktop and Claude Code remote access
   - Native MCP protocol over Server-Sent Events
   - Bearer token authentication
   - RESTful health check endpoint

2. **MCPO** (`mcpo` - optional, commented out) - Port 8001
   - For Open WebUI integration
   - Wraps stdio MCP → OpenAPI conversion
   - Enables browser-based LLM chat access

Both interfaces share:
- Same .NET CLI binary (`lfm`)
- Same config and cache (via Docker volumes)
- Same 30 MCP tools
- Same Spotify/Sonos integration

## Files

- `Dockerfile` - Multi-stage build (ARM64/AMD64 support)
- `docker-compose.yml` - Service orchestration
- `docker-entrypoint.sh` - Startup script with config injection
- `.env.example` - Environment variable template

## Quick Start

### 1. Obtain Spotify Refresh Token (Optional)

If you want Spotify playback control from the containerized server:

```bash
# On your local machine with the lfm CLI installed:
./publish/win-x64/lfm.exe config spotify

# This will:
# 1. Prompt for Client ID and Client Secret
# 2. Open browser for OAuth authorization
# 3. Save refresh token to config.json

# Extract the refresh token:
cat %LOCALAPPDATA%/lfm/config.json
# Look for: "RefreshToken": "BQD..."
```

### 2. Create Environment File

```bash
cd lfm-mcp-release
cp .env.example .env

# Edit .env with your values:
# - LFM_AUTH_TOKEN (generate: openssl rand -base64 32)
# - LASTFM_USERNAME
# - LASTFM_API_KEY
# - SPOTIFY_REFRESH_TOKEN (from step 1)
# - etc.
```

### 3. Build and Run

```bash
# Build the image
docker-compose build

# Start the service
docker-compose up -d

# Check logs
docker-compose logs -f lfm-mcp-http

# Check health
curl http://localhost:8002/health
```

## Architecture Selection

### ARM64 (Raspberry Pi, Apple Silicon, Spark)

The default `Dockerfile` builds for `linux-arm64`:

```dockerfile
RUN dotnet publish ./Lfm.Cli/Lfm.Cli.csproj \
    -c Release \
    -r linux-arm64 \
    --self-contained false
```

### AMD64 (Most servers)

Change line 24 in `Dockerfile` to:

```dockerfile
RUN dotnet publish ./Lfm.Cli/Lfm.Cli.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false
```

## Endpoints

### Health Check (No Auth)
```bash
curl http://your-server:8002/health
```

Response:
```json
{
  "status": "healthy",
  "transport": "sse",
  "activeSessions": 0,
  "version": "0.3.0",
  "authentication": "enabled"
}
```

### SSE Connection (Requires Auth)
```bash
curl -N -H "Authorization: Bearer your-token" \
  http://your-server:8002/sse
```

### POST Messages (Requires Auth)
```bash
curl -X POST \
  -H "Authorization: Bearer your-token" \
  -H "Content-Type: application/json" \
  -d '{"method":"tools/list"}' \
  http://your-server:8002/message
```

## Claude Desktop/Code Configuration

Update your `.claude.json` to use the remote SSE transport:

```json
{
  "mcpServers": {
    "lfm": {
      "url": "http://your-server-ip:8002/sse",
      "transport": {
        "type": "sse",
        "headers": {
          "Authorization": "Bearer your-auth-token"
        }
      }
    }
  }
}
```

## Deployment to Spark (or other ARM64 server)

### 1. Copy files to server

```bash
# From your Windows machine:
scp -r lfm-mcp-release steven@spark:/home/steven/lfm-mcp
scp -r src steven@spark:/home/steven/lfm/src
```

Or use git:

```bash
# SSH to spark
ssh steven@spark

# Clone repository
git clone <your-repo-url> /home/steven/lfm
cd /home/steven/lfm/lfm-mcp-release
```

### 2. Create .env file

```bash
cp .env.example .env
nano .env  # Fill in your values
```

### 3. Build and deploy

```bash
# Build for ARM64
docker-compose build

# Start service
docker-compose up -d

# Verify
docker-compose ps
docker-compose logs -f
curl http://localhost:8002/health
```

### 4. Configure firewall (if needed)

```bash
# Allow port 8002 from VPN network
sudo ufw allow from 192.168.1.0/24 to any port 8002
```

## Volumes

Two Docker volumes are created for persistence:

- `lfm-config` - Configuration files (`/app/config`)
- `lfm-cache` - API response cache (`/app/cache`)

These persist across container restarts and updates.

### Inspecting volumes

```bash
# List volumes
docker volume ls

# Inspect config
docker run --rm -v lfm-config:/config alpine ls -la /config

# Clear cache
docker run --rm -v lfm-cache:/cache alpine rm -rf /cache/*
```

## Troubleshooting

### Container won't start

```bash
# Check logs
docker-compose logs lfm-mcp-http

# Common issues:
# - Missing .env file
# - Invalid environment variables
# - Port 8002 already in use
```

### Health check failing

```bash
# Test from inside container
docker exec lfm-mcp-http curl http://localhost:8002/health

# Test from host
curl http://localhost:8002/health
```

### Architecture mismatch

```bash
# Check your server architecture
uname -m

# ARM64 outputs: aarch64, arm64
# AMD64 outputs: x86_64

# Rebuild for correct architecture (edit Dockerfile first)
docker-compose build --no-cache
```

### Spotify integration not working

```bash
# Verify refresh token is in config
docker exec lfm-mcp-http cat /app/config/config.json

# Test lfm CLI directly
docker exec lfm-mcp-http lfm config show

# Check logs for OAuth errors
docker-compose logs -f lfm-mcp-http
```

## Updating

```bash
# Pull latest changes
git pull

# Rebuild and restart
docker-compose down
docker-compose build
docker-compose up -d
```

## Production Considerations

1. **VPN Only**: Do not expose port 8002 to the public internet
2. **Strong Auth Token**: Use `openssl rand -base64 32` or similar
3. **HTTPS Reverse Proxy**: Use nginx/traefik for TLS termination
4. **Resource Limits**: Add to docker-compose.yml:
   ```yaml
   deploy:
     resources:
       limits:
         cpus: '1'
         memory: 512M
   ```

## Multi-User Considerations

Current implementation is single-user per instance. For multi-user:

1. Run separate containers per user (different ports)
2. Use different `LASTFM_USERNAME` per container
3. Isolate volumes: `lfm-config-user1`, `lfm-cache-user1`, etc.
4. Use reverse proxy for routing (path-based or subdomain)

Example multi-user docker-compose:

```yaml
services:
  lfm-user1:
    extends: lfm-mcp-http
    container_name: lfm-user1
    ports:
      - "8002:8002"
    environment:
      - LASTFM_USERNAME=user1
    volumes:
      - lfm-config-user1:/app/config
      - lfm-cache-user1:/app/cache

  lfm-user2:
    extends: lfm-mcp-http
    container_name: lfm-user2
    ports:
      - "8003:8002"
    environment:
      - LASTFM_USERNAME=user2
    volumes:
      - lfm-config-user2:/app/config
      - lfm-cache-user2:/app/cache
```

## Development

### Testing locally (Windows)

```bash
# Build
docker-compose build

# Run
docker-compose up

# Test endpoints
curl http://localhost:8002/health
```

### Debugging

```bash
# Shell into container
docker exec -it lfm-mcp-http /bin/bash

# Check lfm CLI
lfm --version
lfm config show

# Check Node.js
node --version
node -e "console.log(require('./server-core.js'))"

# Check environment
env | grep LFM
env | grep SPOTIFY
```
