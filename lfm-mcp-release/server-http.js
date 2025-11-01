#!/usr/bin/env node

/**
 * LFM MCP Server - HTTP/SSE Transport
 *
 * This server provides HTTP/SSE transport for remote MCP access.
 * It enables Claude Desktop, Claude Code, and other MCP clients to connect
 * remotely instead of requiring local stdio communication.
 *
 * Architecture:
 * - GET /sse        → Establishes SSE connection (server→client messages)
 * - POST /message   → Receives client→server messages
 * - GET /health     → Health check endpoint
 *
 * Authentication: Bearer token (required for production)
 * CORS: Configurable via ALLOWED_ORIGINS environment variable
 *
 * Usage:
 *   node server-http.js --port 8002 --auth-token your-secret-token
 *
 * Environment Variables:
 *   HTTP_PORT        - Port to listen on (default: 8002)
 *   AUTH_TOKEN       - Bearer token for authentication (required for production)
 *   ALLOWED_ORIGINS  - Comma-separated CORS origins (default: *)
 */

const http = require('http');
const { createMcpServer } = require('./server-core.js');
const { SSEServerTransport } = require('@modelcontextprotocol/sdk/server/sse.js');

// ============================================
// CONFIGURATION
// ============================================

// Parse command line arguments
const args = process.argv.slice(2);
const port = args.includes('--port')
  ? parseInt(args[args.indexOf('--port') + 1])
  : parseInt(process.env.HTTP_PORT || '8002');

const authToken = args.includes('--auth-token')
  ? args[args.indexOf('--auth-token') + 1]
  : process.env.AUTH_TOKEN;

const allowedOrigins = (process.env.ALLOWED_ORIGINS || '*').split(',').map(o => o.trim());

// ============================================
// SESSION MANAGEMENT
// ============================================

const sessions = new Map(); // sessionId -> { transport, server }

/**
 * Clean up a session when it closes
 */
function cleanupSession(sessionId) {
  const session = sessions.get(sessionId);
  if (session) {
    try {
      session.transport.close();
    } catch (error) {
      console.error(`Error closing transport for session ${sessionId}:`, error);
    }
    sessions.delete(sessionId);
    console.error(`Session ${sessionId} closed. Active sessions: ${sessions.size}`);
  }
}

// ============================================
// HTTP REQUEST HANDLER
// ============================================

const httpServer = http.createServer(async (req, res) => {
  // CORS headers
  const origin = req.headers.origin || '*';
  const allowedOrigin = allowedOrigins.includes('*') || allowedOrigins.includes(origin)
    ? origin
    : allowedOrigins[0];

  res.setHeader('Access-Control-Allow-Origin', allowedOrigin);
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
  res.setHeader('Access-Control-Allow-Credentials', 'true');

  // Handle preflight requests
  if (req.method === 'OPTIONS') {
    res.writeHead(200);
    res.end();
    return;
  }

  // Authentication check (skip for health endpoint)
  if (req.url !== '/health') {
    if (authToken) {
      const authHeader = req.headers.authorization;
      if (!authHeader || !authHeader.startsWith('Bearer ')) {
        res.writeHead(401, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({
          error: 'Missing or invalid Authorization header',
          message: 'Include "Authorization: Bearer <token>" header'
        }));
        return;
      }

      const token = authHeader.substring(7); // Remove "Bearer " prefix
      if (token !== authToken) {
        res.writeHead(403, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({
          error: 'Invalid authentication token',
          message: 'The provided token is not valid'
        }));
        return;
      }
    }
  }

  // Route handling
  const url = new URL(req.url, `http://${req.headers.host}`);

  // ============================================
  // HEALTH CHECK ENDPOINT
  // ============================================
  if (url.pathname === '/health' && req.method === 'GET') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({
      status: 'healthy',
      transport: 'sse',
      activeSessions: sessions.size,
      version: '0.3.0',
      authentication: authToken ? 'enabled' : 'disabled'
    }));
    return;
  }

  // ============================================
  // SSE ENDPOINT - Establish server→client stream
  // ============================================
  if (url.pathname === '/sse' && req.method === 'GET') {
    try {
      // Create SSE transport
      const transport = new SSEServerTransport('/message', res);

      // Create MCP server instance for this session
      const server = createMcpServer();

      // Store session
      const sessionId = transport.sessionId;
      sessions.set(sessionId, { transport, server });

      // Set up cleanup on close
      transport.onclose = () => {
        cleanupSession(sessionId);
      };

      transport.onerror = (error) => {
        console.error(`Transport error for session ${sessionId}:`, error);
        cleanupSession(sessionId);
      };

      // Connect server to transport
      await server.connect(transport);

      console.error(`New SSE connection established. Session: ${sessionId}. Active sessions: ${sessions.size}`);

      // SSE transport handles the response, don't end it here
      return;
    } catch (error) {
      console.error('Error establishing SSE connection:', error);
      if (!res.headersSent) {
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({
          error: 'Failed to establish SSE connection',
          message: error.message
        }));
      }
      return;
    }
  }

  // ============================================
  // POST ENDPOINT - Receive client→server messages
  // ============================================
  if (url.pathname === '/message' && req.method === 'POST') {
    let body = '';

    req.on('data', chunk => {
      body += chunk.toString();
    });

    req.on('end', async () => {
      try {
        const message = JSON.parse(body);

        // Find the right session's transport to handle this message
        // In a production system with multiple clients, you'd route by session ID
        // For now, we'll use the first (and likely only) active session
        const sessionArray = Array.from(sessions.values());

        if (sessionArray.length === 0) {
          res.writeHead(404, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({
            error: 'No active session found',
            message: 'Establish an SSE connection first via GET /sse'
          }));
          return;
        }

        // Handle message with the session's transport
        // The SSE transport will route it to the connected server
        const { transport } = sessionArray[0];
        await transport.handleMessage(message);

        res.writeHead(202, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ accepted: true }));
      } catch (error) {
        console.error('Error handling POST message:', error);
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({
          error: 'Failed to process message',
          message: error.message
        }));
      }
    });

    return;
  }

  // ============================================
  // 404 - Unknown route
  // ============================================
  res.writeHead(404, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify({
    error: 'Not found',
    availableEndpoints: {
      'GET /health': 'Health check',
      'GET /sse': 'Establish SSE connection (requires auth)',
      'POST /message': 'Send client message (requires auth)'
    }
  }));
});

// ============================================
// SERVER STARTUP
// ============================================

httpServer.listen(port, '0.0.0.0', () => {
  console.error('========================================');
  console.error('LFM MCP HTTP Server (SSE Transport)');
  console.error('========================================');
  console.error(`Port:        ${port}`);
  console.error(`SSE:         http://localhost:${port}/sse`);
  console.error(`POST:        http://localhost:${port}/message`);
  console.error(`Health:      http://localhost:${port}/health`);
  console.error(`Auth:        ${authToken ? 'Enabled' : 'DISABLED (⚠️  not recommended for production)'}`);
  console.error(`CORS:        ${allowedOrigins.join(', ')}`);
  console.error('========================================');
  console.error('Waiting for connections...');
  console.error('');
});

// ============================================
// GRACEFUL SHUTDOWN
// ============================================

function shutdown() {
  console.error('\nShutting down gracefully...');

  // Close all active sessions
  sessions.forEach((session, sessionId) => {
    cleanupSession(sessionId);
  });

  // Close HTTP server
  httpServer.close(() => {
    console.error('Server closed');
    process.exit(0);
  });

  // Force exit after 5 seconds if graceful shutdown fails
  setTimeout(() => {
    console.error('Forced shutdown after timeout');
    process.exit(1);
  }, 5000);
}

process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);

// ============================================
// ERROR HANDLING
// ============================================

process.on('uncaughtException', (error) => {
  console.error('Uncaught exception:', error);
  shutdown();
});

process.on('unhandledRejection', (reason, promise) => {
  console.error('Unhandled rejection at:', promise, 'reason:', reason);
  shutdown();
});
