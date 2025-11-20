# Detailed Clean Refactor Plan (Option 1)

## Overview
Extract shared code from server.js (2620 lines) into reusable modules, then create SSE transport server that uses the same handlers. Zero code duplication, maintainable architecture.

---

## Current Architecture

```
server.js (2620 lines - monolithic)
├─ executeLfmCommand()          [lines 16-42]
├─ parseJsonOutput()             [lines 44-137]
├─ compactAlbum/Artist/Track()   [lines 139-165]
├─ 30 tool definitions           [ListToolsRequestSchema handler]
├─ 30 tool handlers              [CallToolRequestSchema handler]
└─ main() with stdio transport   [lines 2615-2621]
```

**Problem:** All logic is embedded in one file. Adding HTTP transport means duplicating 2600+ lines.

---

## Target Architecture

```
server-core.js (NEW - ~1800 lines)
├─ executeLfmCommand()
├─ parseJsonOutput()
├─ Helper functions
└─ exports: { createMcpServer }

server.js (REFACTORED - ~50 lines)
├─ Import createMcpServer from core
├─ Wrapper for stdio transport
└─ Backward compatible (no breaking changes)

server-http.js (NEW - ~200 lines)
├─ Import createMcpServer from core
├─ HTTP server with SSE transport
├─ Authentication & CORS
├─ Session management
└─ Health check endpoint

package.json (UPDATED)
├─ New dependencies (if needed)
└─ Scripts for both modes
```

---

## Phase 1: Extract Core Logic (60-90 minutes)

### Step 1.1: Create server-core.js skeleton

```javascript
// server-core.js
const { spawn } = require('child_process');
const { Server } = require('@modelcontextprotocol/sdk/server/index.js');
const {
  ListToolsRequestSchema,
  CallToolRequestSchema
} = require('@modelcontextprotocol/sdk/types.js');
const fs = require('fs');
const path = require('path');

// ========================================
// UTILITIES (Lines 16-165 from server.js)
// ========================================

async function executeLfmCommand(args) { ... }
function parseJsonOutput(output) { ... }
function compactAlbum(album) { ... }
function compactArtist(artist) { ... }
function compactTrack(track) { ... }

// ========================================
// TOOL DEFINITIONS
// ========================================

function getToolsList() {
  return [
    {
      name: 'lfm_init',
      description: '...',
      inputSchema: { ... }
    },
    // ... all 30 tools
  ];
}

// ========================================
// TOOL HANDLERS
// ========================================

async function handleToolCall(name, args) {
  // Mega switch/if-else with all 30 tool handlers
  if (name === 'lfm_init') { ... }
  else if (name === 'lfm_tracks') { ... }
  // ... all 30 handlers

  throw new Error(`Unknown tool: ${name}`);
}

// ========================================
// MCP SERVER FACTORY
// ========================================

function createMcpServer() {
  const server = new Server(
    {
      name: 'lfm-mcp-server',
      version: '1.0.0',
    },
    {
      capabilities: {
        tools: {},
      },
    }
  );

  // Load guidelines once
  const guidelinesPath = path.join(__dirname, 'lfm-guidelines.md');
  let guidelines = '';
  try {
    guidelines = fs.readFileSync(guidelinesPath, 'utf-8');
  } catch (error) {
    console.warn('Warning: Could not load lfm-guidelines.md');
  }

  // Register handlers
  server.setRequestHandler(ListToolsRequestSchema, async () => {
    return { tools: getToolsList() };
  });

  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;
    return await handleToolCall(name, args, guidelines);
  });

  return server;
}

// Export public API
module.exports = {
  createMcpServer,
  executeLfmCommand,   // Exported for testing
  parseJsonOutput,      // Exported for testing
};
```

**What we're extracting:**
- ✅ All utilities (executeLfmCommand, parseJsonOutput, compact functions)
- ✅ All 30 tool definitions
- ✅ All 30 tool handlers
- ✅ Guidelines loading
- ✅ Server setup logic

**What stays separate:**
- ❌ Transport layer (stdio vs HTTP)
- ❌ Server startup code
- ❌ Environment-specific config

### Step 1.2: Copy & Paste Strategy

1. **Copy lines 16-165** (utilities) → server-core.js top
2. **Extract tool definitions** from ListToolsRequestSchema handler → `getToolsList()`
3. **Extract tool handlers** from CallToolRequestSchema handler → `handleToolCall()`
4. **Add exports** at bottom

**Time estimate:** 45 minutes (copy-paste + testing)

---

## Phase 2: Refactor server.js (15-20 minutes)

### Step 2.1: Slim down to wrapper

```javascript
#!/usr/bin/env node

const { createMcpServer } = require('./server-core.js');
const { StdioServerTransport } = require('@modelcontextprotocol/sdk/server/stdio.js');

// Start the server with stdio transport
async function main() {
  const server = createMcpServer();
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error('LFM MCP Server running (stdio mode)');
}

main().catch(console.error);
```

**Result:**
- ✅ server.js goes from 2620 → ~15 lines
- ✅ Backward compatible (stdio transport unchanged)
- ✅ No changes needed to .claude.json for existing users

### Step 2.2: Verification

```bash
# Test stdio mode still works
node server.js
# Should connect just like before
```

**Time estimate:** 15 minutes

---

## Phase 3: Create server-http.js (30-45 minutes)

### Step 3.1: HTTP wrapper with SSE transport

```javascript
#!/usr/bin/env node

const http = require('http');
const { createMcpServer } = require('./server-core.js');
const { SSEServerTransport } = require('@modelcontextprotocol/sdk/server/sse.js');

// Parse CLI args
const args = process.argv.slice(2);
const port = args.includes('--port')
  ? parseInt(args[args.indexOf('--port') + 1])
  : parseInt(process.env.HTTP_PORT || '8002');

const authToken = args.includes('--auth-token')
  ? args[args.indexOf('--auth-token') + 1]
  : process.env.AUTH_TOKEN;

const allowedOrigins = (process.env.ALLOWED_ORIGINS || '*').split(',');

// Session management
const sessions = new Map(); // sessionId -> { transport, server }

// HTTP request handler
const httpServer = http.createServer(async (req, res) => {
  // 1. CORS headers
  // 2. Authentication check
  // 3. Route handling:
  //    - GET /health
  //    - GET /sse (establish SSE connection)
  //    - POST /message (receive client messages)
  // 4. Session management
});

httpServer.listen(port, '0.0.0.0', () => {
  console.log(`LFM MCP HTTP Server running on port ${port}`);
  console.log(`SSE endpoint: http://localhost:${port}/sse`);
  console.log(`POST endpoint: http://localhost:${port}/message`);
  console.log(`Health check: http://localhost:${port}/health`);
  console.log(`Auth: ${authToken ? 'Enabled' : 'DISABLED'}`);
});

// Graceful shutdown
process.on('SIGINT', () => {
  console.log('Shutting down...');
  sessions.forEach(({ transport }) => transport.close());
  httpServer.close(() => process.exit(0));
});
```

**Features:**
- ✅ Bearer token authentication
- ✅ CORS support
- ✅ Session management (multiple clients)
- ✅ Health check endpoint
- ✅ Graceful shutdown

**Time estimate:** 30 minutes (already partially written)

---

## Phase 4: Testing (30-45 minutes)

### Test 1: stdio mode (backward compatibility)
```bash
cd lfm-mcp-release
node server.js
# Should work exactly as before
```

### Test 2: HTTP server local
```bash
node server-http.js --port 8002 --auth-token test123
# Visit http://localhost:8002/health
# Expected: {"status":"healthy","transport":"sse","activeSessions":0}
```

### Test 3: SSE connection
```bash
curl -N -H "Authorization: Bearer test123" http://localhost:8002/sse
# Should establish SSE stream
```

### Test 4: Claude Desktop with SSE
Update `.claude.json`:
```json
{
  "lfm-sse": {
    "type": "sse",
    "url": "http://localhost:8002/sse",
    "headers": {
      "Authorization": "Bearer test123"
    }
  }
}
```
Restart Claude Desktop → Test: "Show my top 5 artists"

---

## Phase 5: Docker Deployment (45-60 minutes)

### Step 5.1: Create Dockerfile

```dockerfile
# Multi-stage build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-cli
WORKDIR /src
COPY src/ .
RUN dotnet publish src/Lfm.Cli/Lfm.Cli.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/lfm

FROM node:20-slim
# Install .NET runtime dependencies
RUN apt-get update && \
    apt-get install -y libicu-dev && \
    rm -rf /var/lib/apt/lists/*

# Copy CLI binary
COPY --from=build-cli /app/lfm /usr/local/bin/lfm
RUN chmod +x /usr/local/bin/lfm

# Setup MCP server
WORKDIR /app/mcp
COPY lfm-mcp-release/package*.json ./
RUN npm ci --production
COPY lfm-mcp-release/ ./

# Expose HTTP/SSE port
EXPOSE 8002

# Default environment
ENV HTTP_PORT=8002
ENV NODE_ENV=production

# Run HTTP server by default
CMD ["node", "server-http.js"]
```

### Step 5.2: Create docker-compose.yml

```yaml
version: '3.8'

services:
  # LFM MCP with HTTP/SSE transport
  lfm-mcp-http:
    build: .
    container_name: lfm-mcp-http
    ports:
      - "8002:8002"  # SSE endpoint for Claude Desktop/Code
    environment:
      - HTTP_PORT=8002
      - AUTH_TOKEN=${LFM_AUTH_TOKEN:-change-me-in-production}
      - ALLOWED_ORIGINS=*
    volumes:
      - lfm-config:/root/.config/lfm
      - lfm-cache:/root/.cache/lfm
    networks:
      - lfm-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8002/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  # MCPO (for Open WebUI compatibility)
  mcpo:
    image: ghcr.io/open-webui/mcpo:main
    container_name: lfm-mcpo
    ports:
      - "8001:8000"
    volumes:
      - ./mcpo-config-docker.json:/config.json
    command: >
      --config /config.json
      --api-key ${MCPO_API_KEY:-change-me-in-production}
      --host 0.0.0.0
    depends_on:
      lfm-mcp-http:
        condition: service_healthy
    networks:
      - lfm-network
      - openwebui-network
    restart: unless-stopped

networks:
  lfm-network:
    driver: bridge
  openwebui-network:
    external: true  # Assumes Open WebUI already has a network

volumes:
  lfm-config:
    driver: local
  lfm-cache:
    driver: local
```

### Step 5.3: Create .env file

```bash
# .env (don't commit this!)
LFM_AUTH_TOKEN=your-secret-token-for-claude-desktop
MCPO_API_KEY=your-secret-token-for-open-webui
```

### Step 5.4: Create mcpo-config-docker.json

```json
{
  "mcpServers": {
    "lfm": {
      "command": "docker",
      "args": [
        "exec",
        "-i",
        "lfm-mcp-http",
        "node",
        "/app/mcp/server.js"
      ],
      "env": {}
    }
  }
}
```

**Note:** MCPO connects to the **stdio mode** (server.js) running inside the container, while Claude Desktop/Code connect to **HTTP mode** (server-http.js) via port 8002.

---

## Phase 6: Deployment to Spark (30 minutes)

### Step 6.1: Copy to spark

```bash
# From Windows
scp -r C:\Users\steve\OneDrive\Documents\code\lfm spark:~/lfm-deploy
```

### Step 6.2: Build and run

```bash
ssh spark
cd ~/lfm-deploy

# Create .env file
cat > .env << 'EOF'
LFM_AUTH_TOKEN=your-actual-secret-token
MCPO_API_KEY=your-mcpo-secret-token
EOF

# Build and start
docker-compose up -d

# Check logs
docker-compose logs -f lfm-mcp-http

# Verify health
curl http://localhost:8002/health
```

### Step 6.3: Update Claude Desktop config

```json
{
  "lfm": {
    "type": "sse",
    "url": "http://spark:8002/sse",
    "headers": {
      "Authorization": "Bearer your-actual-secret-token"
    }
  }
}
```

Restart Claude Desktop/Code → Test!

---

## Total Time Estimate

| Phase | Task | Time |
|-------|------|------|
| 1 | Extract server-core.js | 60-90 min |
| 2 | Refactor server.js | 15-20 min |
| 3 | Create server-http.js | 30-45 min |
| 4 | Testing (local) | 30-45 min |
| 5 | Docker setup | 45-60 min |
| 6 | Deploy to spark | 30 min |
| **Total** | | **3.5-5 hours** |

---

## Risk Mitigation

### Backup Strategy
```bash
# Before starting
cp server.js server.js.backup
git add server.js
git commit -m "Backup before refactor"
```

### Rollback Plan
If something breaks:
1. `git checkout server.js.backup`
2. Delete server-core.js and server-http.js
3. Continue using stdio mode locally

### Incremental Testing
- Test after each phase
- Don't proceed if tests fail
- Keep original server.js until HTTP mode is confirmed working

---

## Success Criteria

✅ **Phase 1-3 Complete:**
- server-core.js exports createMcpServer()
- server.js is <20 lines, works with stdio
- server-http.js works on localhost:8002

✅ **Phase 4 Complete:**
- Claude Desktop connects via SSE to localhost:8002
- All LFM tools work (test at least 5 different tools)
- No regressions in functionality

✅ **Phase 5-6 Complete:**
- Docker container builds successfully
- Container runs on spark
- Health check passes
- Claude Desktop/Code connect remotely to spark:8002
- Open WebUI still works via MCPO on spark:8001

---

## Next Steps After Success

1. **Update documentation**
   - Add SSE transport to README
   - Update INSTALL.md with Docker instructions
   - Add troubleshooting for remote access

2. **Security hardening**
   - Generate strong random tokens
   - Document VPN-only access
   - Consider adding IP allowlisting

3. **Monitoring**
   - Add logging to server-http.js
   - Set up Docker container monitoring
   - Track active sessions

4. **Future enhancements**
   - Multi-user support (Phase 2 of original plan)
   - Database for config/cache (Phase 3 of original plan)
   - Spotify OAuth web interface (Phase 4 of original plan)

---

Ready to proceed? I can start with Phase 1 once you approve this plan.
