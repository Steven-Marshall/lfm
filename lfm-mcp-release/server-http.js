#!/usr/bin/env node

// Load environment variables from .env file (for local development)
require('dotenv').config();

/**
 * LFM MCP Server - Streamable HTTP Transport
 *
 * This server provides Streamable HTTP transport for remote MCP access.
 * It enables Claude Desktop, Claude Code, and other MCP clients to connect
 * remotely instead of requiring local stdio communication.
 *
 * Architecture:
 * - POST /mcp        → Initialize session or send messages
 * - GET /mcp         → Establish event stream (Server-Sent Events) for server→client messages
 * - DELETE /mcp      → Close session
 * - GET /health      → Health check endpoint
 *
 * Streamable HTTP Benefits (vs old HTTP+SSE):
 * - Single endpoint (no /sse + /message coordination)
 * - Session resumability (reconnect without losing state)
 * - No 5-minute idle timeout
 * - Dynamic upgrade to SSE for long-running tasks
 * - Built-in session management with secure IDs
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

const { version } = require('./package.json');
const express = require('express');
const cors = require('cors');
const { randomUUID } = require('crypto');
const { createMcpServer } = require('./server-core.js');
const { StreamableHTTPServerTransport } = require('@modelcontextprotocol/sdk/server/streamableHttp.js');

// ============================================
// CONFIGURATION
// ============================================

// Parse command line arguments
const args = process.argv.slice(2);
const port = args.includes('--port')
  ? parseInt(args[args.indexOf('--port') + 1])
  : parseInt(process.env.HTTP_PORT || '8002');

const allowedOrigins = (process.env.ALLOWED_ORIGINS || '*').split(',').map(o => o.trim());

// Session timeout: default 8 hours (480 minutes)
// Can be overridden with SESSION_TIMEOUT_MINUTES environment variable
const SESSION_TIMEOUT_MS = parseInt(process.env.SESSION_TIMEOUT_MINUTES || '480') * 60 * 1000;

// ============================================
// SESSION MANAGEMENT
// ============================================

// Map of session ID -> { transport, server, timeoutHandle, lastActivity }
const sessions = new Map();

/**
 * Clean up a session when it closes
 */
function cleanupSession(sessionId) {
  const session = sessions.get(sessionId);
  if (session) {
    try {
      // Cancel any pending timeout
      if (session.timeoutHandle) {
        clearTimeout(session.timeoutHandle);
      }

      // Close the transport
      session.transport.close();

      // Close the MCP server instance
      if (session.server && typeof session.server.close === 'function') {
        session.server.close();
      }
    } catch (error) {
      console.error(`Error closing session ${sessionId}:`, error);
    }
    sessions.delete(sessionId);
    console.error(`Session ${sessionId} closed. Active sessions: ${sessions.size}`);
  }
}

/**
 * Update session activity timestamp and reset timeout
 */
function touchSession(sessionId) {
  const session = sessions.get(sessionId);
  if (!session) return;

  // Cancel the old timeout
  if (session.timeoutHandle) {
    clearTimeout(session.timeoutHandle);
  }

  // Start a new timeout
  session.timeoutHandle = setTimeout(() => {
    console.error(`Session ${sessionId} timed out after ${SESSION_TIMEOUT_MS / 60000} minutes of inactivity`);
    cleanupSession(sessionId);
  }, SESSION_TIMEOUT_MS);

  // Update last activity timestamp
  session.lastActivity = Date.now();
}

// ============================================
// EXPRESS APP SETUP
// ============================================

const app = express();

// Trust proxy (required when behind Tailscale Funnel or other reverse proxy)
app.set('trust proxy', true);

// CORS configuration
const corsOptions = {
  origin: (origin, callback) => {
    // Allow requests with no origin (like mobile apps or curl requests)
    if (!origin) return callback(null, true);

    if (allowedOrigins.includes('*') || allowedOrigins.includes(origin)) {
      callback(null, true);
    } else {
      callback(new Error('Not allowed by CORS'));
    }
  },
  credentials: true,
  methods: 'GET,POST,DELETE',
  exposedHeaders: [
    'mcp-session-id',
    'last-event-id',
    'mcp-protocol-version'
  ]
};

app.use(cors(corsOptions));

// IMPORTANT: Do NOT parse body for /mcp endpoint - StreamableHTTPServerTransport needs raw stream
// Only parse body for other endpoints (like /health)
app.use((req, res, next) => {
  if (req.path === '/mcp') {
    // Skip body parsing for MCP endpoint
    return next();
  }
  // Parse JSON for other endpoints
  express.json()(req, res, next);
});

// ============================================
// AUTHENTICATION
// ============================================

// Authentication disabled - MCP server is publicly accessible
// Future: Implement Auth0 resource server pattern for production
console.error('[Auth] ⚠️  Authentication DISABLED (not recommended for production)');

// ============================================
// HEALTH CHECK ENDPOINT
// ============================================

app.get('/health', (req, res) => {
  res.json({
    status: 'healthy',
    transport: 'streamable-http',
    activeSessions: sessions.size,
    version: version,
    authentication: 'disabled',
    spec: '2025-03-26'
  });
});

// ============================================
// MCP ENDPOINT - Streamable HTTP
// ============================================

/**
 * POST /mcp - Initialize session or send message
 */
app.post('/mcp', async (req, res) => {
  try {
    const sessionId = req.headers['mcp-session-id'];
    let session;

    // Check if session exists
    if (sessionId && sessions.has(sessionId)) {
      // Use existing session
      session = sessions.get(sessionId);
      // Reset timeout on activity
      touchSession(sessionId);
    } else {
      // Create new session
      const transport = new StreamableHTTPServerTransport({
        sessionIdGenerator: () => randomUUID(),
        onsessioninitialized: (newSessionId) => {
          console.error(`New session initialized: ${newSessionId}. Active sessions: ${sessions.size + 1}`);
        }
      });

      // Create MCP server instance for this session
      const server = createMcpServer();

      // Create session timeout
      const timeoutHandle = setTimeout(() => {
        const actualSessionId = transport.sessionId;
        console.error(`Session ${actualSessionId} timed out after ${SESSION_TIMEOUT_MS / 60000} minutes of inactivity`);
        cleanupSession(actualSessionId);
      }, SESSION_TIMEOUT_MS);

      // Create session object
      // Note: sessionState for lfm_init is managed internally by server-core.js
      session = {
        transport,
        server,
        timeoutHandle,
        lastActivity: Date.now()
      };

      // Connect server to transport
      await server.connect(transport);

      // Store session (sessionId will be set by transport during handleRequest)
      // We'll update the map after handleRequest completes
      session.tempTransport = transport;
    }

    // Handle the request
    await session.transport.handleRequest(req, res);

    // If this was a new session, store it in the map
    if (session.tempTransport) {
      const actualSessionId = session.transport.sessionId;
      if (actualSessionId) {
        sessions.set(actualSessionId, session);
        delete session.tempTransport;
        // Touch session to ensure timeout is properly set with correct sessionId
        touchSession(actualSessionId);
      }
    }
  } catch (error) {
    console.error('==== ERROR handling POST /mcp ====');
    console.error('Error message:', error.message);
    console.error('Error stack:', error.stack);
    console.error('==================================');
    if (!res.headersSent) {
      res.status(500).json({
        error: 'Failed to handle MCP request',
        message: error.message,
        stack: error.stack
      });
    }
  }
});

/**
 * GET /mcp - Establish SSE stream
 */
app.get('/mcp', async (req, res) => {
  try {
    const sessionId = req.headers['mcp-session-id'];

    if (!sessionId || !sessions.has(sessionId)) {
      return res.status(404).json({
        error: 'Session not found',
        message: 'Initialize a session first via POST /mcp'
      });
    }

    const session = sessions.get(sessionId);

    // Reset timeout on activity
    touchSession(sessionId);

    // Set up cleanup when SSE connection closes
    req.on('close', () => {
      console.error(`SSE connection closed for session ${sessionId}`);
    });

    // Handle SSE stream
    await session.transport.handleRequest(req, res);
  } catch (error) {
    console.error('Error handling GET /mcp:', error);
    if (!res.headersSent) {
      res.status(500).json({
        error: 'Failed to establish SSE stream',
        message: error.message
      });
    }
  }
});

/**
 * DELETE /mcp - Close session
 */
app.delete('/mcp', async (req, res) => {
  try {
    const sessionId = req.headers['mcp-session-id'];

    if (!sessionId || !sessions.has(sessionId)) {
      return res.status(404).json({
        error: 'Session not found'
      });
    }

    cleanupSession(sessionId);

    res.json({
      success: true,
      message: 'Session closed'
    });
  } catch (error) {
    console.error('Error handling DELETE /mcp:', error);
    res.status(500).json({
      error: 'Failed to close session',
      message: error.message
    });
  }
});

// ============================================
// 404 HANDLER
// ============================================

app.use((req, res) => {
  res.status(404).json({
    error: 'Not found',
    availableEndpoints: {
      'GET /health': 'Health check',
      'POST /mcp': 'Initialize session or send message (requires auth)',
      'GET /mcp': 'Establish SSE stream (requires auth + session ID)',
      'DELETE /mcp': 'Close session (requires auth + session ID)'
    }
  });
});

// ============================================
// SERVER STARTUP
// ============================================

const server = app.listen(port, '0.0.0.0', () => {
  console.error('========================================');
  console.error('LFM MCP Server (Streamable HTTP)');
  console.error('========================================');
  console.error(`Port:        ${port}`);
  console.error(`Endpoint:    http://localhost:${port}/mcp`);
  console.error(`Health:      http://localhost:${port}/health`);
  console.error(`Auth:        DISABLED (⚠️  not recommended for production)`);
  console.error(`CORS:        ${allowedOrigins.join(', ')}`);
  console.error(`Timeout:     ${SESSION_TIMEOUT_MS / 60000} minutes`);
  console.error(`Spec:        2025-03-26 (Streamable HTTP)`);
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

  // Close Express server
  server.close(() => {
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
