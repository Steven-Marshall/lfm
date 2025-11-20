# SSE Transport Implementation Plan

## Goal
Enable remote access to LFM MCP server from Claude Desktop, Claude Code, and Open WebUI while maintaining backward compatibility with stdio transport.

## Architecture Decision

### Option A: Separate Entry Points (Recommended - Minimal Changes)
```
server.js          (stdio transport - unchanged)
server-http.js     (HTTP/SSE transport - new)
server-core.js     (shared handlers - refactor)
```

**Pros:**
- ✅ Backward compatible (stdio mode untouched)
- ✅ Clear separation of concerns
- ✅ Easy to maintain

**Cons:**
- ⚠️ Requires refactoring server.js to extract handlers

### Option B: Single Entry Point with Mode Selection
```
server.js          (supports both stdio and HTTP via env var)
```

**Pros:**
- ✅ Single codebase
- ✅ No duplication

**Cons:**
- ⚠️ More complex single file
- ⚠️ Risk of breaking existing stdio mode

## Recommended Implementation (Option A)

### Phase 1: Extract Shared Code (1-2 hours)

**Create `server-core.js`:**
```javascript
// Shared utilities and handlers
module.exports = {
  executeLfmCommand,
  parseJsonOutput,
  createToolsList,
  createToolHandlers
};
```

**Refactor `server.js`:**
```javascript
const { createToolsList, createToolHandlers } = require('./server-core.js');

// Rest stays the same
```

### Phase 2: Create HTTP Server (30 minutes)

**`server-http.js`** - Already created (above)
- Uses SSEServerTransport
- Imports handlers from server-core.js
- Adds authentication
- Handles CORS

### Phase 3: Docker Deployment (1 hour)

**Dockerfile:**
```dockerfile
FROM node:20-slim AS build-cli
# Build .NET CLI (existing)

FROM node:20-slim
COPY --from=build-cli /usr/local/bin/lfm /usr/local/bin/lfm
WORKDIR /app/mcp
COPY lfm-mcp-release/ ./
RUN npm ci --production

# Expose both stdio (for MCPO) and HTTP (for direct SSE)
EXPOSE 8002

# Default to HTTP mode
ENV MCP_TRANSPORT=http
ENV HTTP_PORT=8002

# Can override to run stdio mode
CMD ["node", "server-http.js"]
```

**docker-compose.yml:**
```yaml
version: '3.8'

services:
  # LFM MCP with HTTP/SSE transport
  lfm-mcp:
    build: .
    ports:
      - "8002:8002"  # SSE endpoint
    environment:
      - HTTP_PORT=8002
      - AUTH_TOKEN=${LFM_AUTH_TOKEN}
      - ALLOWED_ORIGINS=*  # Or specific origins
    volumes:
      - lfm-config:/root/.config/lfm
      - lfm-cache:/root/.cache/lfm
    networks:
      - lfm-network
    restart: unless-stopped

  # MCPO (for Open WebUI compatibility)
  mcpo:
    image: ghcr.io/open-webui/mcpo:main
    ports:
      - "8001:8000"
    volumes:
      - ./mcpo-config-docker.json:/config.json
    command: >
      --config /config.json
      --api-key ${MCPO_API_KEY}
      --host 0.0.0.0
    depends_on:
      - lfm-mcp
    networks:
      - lfm-network
      - openwebui-network

networks:
  lfm-network:
    internal: true
  openwebui-network:
    external: true

volumes:
  lfm-config:
  lfm-cache:
```

### Phase 4: Client Configuration (15 minutes)

**For Claude Desktop/Code** - `.claude.json`:
```json
{
  "lfm": {
    "type": "sse",
    "url": "http://spark:8002/sse",
    "headers": {
      "Authorization": "Bearer your-secret-token"
    }
  }
}
```

**For Open WebUI** - Stays the same (uses MCPO):
```
Tool Server URL: http://spark:8001
API Key: your-mcpo-key
```

## Testing Plan

### Test 1: Health Check
```bash
curl http://spark:8002/health
# Expected: {"status":"healthy","transport":"sse","activeSessions":0}
```

### Test 2: SSE Connection
```bash
curl -N -H "Authorization: Bearer your-token" http://spark:8002/sse
# Expected: SSE stream established
```

### Test 3: Claude Desktop Connection
1. Update .claude.json
2. Restart Claude Desktop
3. Test: "Show my top 5 artists"
4. Verify: MCP tool called successfully

### Test 4: Claude Code Connection
1. Same .claude.json config
2. Restart Claude Code
3. Test LFM tools
4. Verify: Works same as Claude Desktop

### Test 5: Open WebUI Connection
1. Verify MCPO still works
2. Test LFM tools from chat
3. Verify: No regression

## Migration Steps

1. **Local Testing (Windows):**
   ```bash
   cd C:\Users\steve\OneDrive\Documents\code\lfm\lfm-mcp-release
   node server-http.js --port 8002 --auth-token test-token
   ```

2. **Test from Claude Desktop:**
   - Update .claude.json to SSE mode
   - Restart Claude
   - Try: "What are my top artists?"

3. **Deploy to Spark:**
   ```bash
   # Copy LFM to spark
   scp -r ~/OneDrive/Documents/code/lfm spark:~/lfm

   # Build and run Docker
   ssh spark
   cd ~/lfm
   docker-compose up -d
   ```

4. **Update Claude configs to point to spark:**
   ```json
   {
     "lfm": {
       "type": "sse",
       "url": "http://spark:8002/sse",
       "headers": {
         "Authorization": "Bearer ${LFM_AUTH_TOKEN}"
       }
     }
   }
   ```

5. **Test from all clients:**
   - Claude Desktop (Windows)
   - Claude Code (Windows)
   - Open WebUI (browser, any device)
   - Mobile browser (via VPN)

## Security Considerations

1. **Authentication:**
   - Required for production (--auth-token)
   - Use strong random token
   - Store in environment variable

2. **CORS:**
   - Set specific origins in production
   - Avoid '*' for public deployments

3. **VPN Access:**
   - LFM server only accessible within VPN
   - No public internet exposure

4. **Config/Cache Isolation:**
   - Single user for now (simple)
   - Future: Multi-user with user-specific paths

## Next Steps

1. ✅ server-http.js created (basic structure)
2. ⏳ Extract shared code to server-core.js
3. ⏳ Import all 30 tool handlers
4. ⏳ Test locally on Windows
5. ⏳ Create Dockerfile and docker-compose
6. ⏳ Deploy to spark
7. ⏳ Update .claude.json configs
8. ⏳ Test from all clients

## Estimated Timeline

- **Phase 1** (Extract shared code): 1-2 hours
- **Phase 2** (Complete HTTP server): 30 minutes
- **Phase 3** (Docker deployment): 1 hour
- **Phase 4** (Client configs): 15 minutes
- **Testing**: 1 hour

**Total: 3-4 hours** for complete implementation
