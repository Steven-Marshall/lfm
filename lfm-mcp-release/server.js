#!/usr/bin/env node

/**
 * LFM MCP Server - Stdio Transport
 *
 * This is the entry point for stdio-based MCP communication (Claude Desktop, Claude Code).
 * All shared logic is in server-core.js for reuse across different transports.
 */

const { createMcpServer } = require('./server-core.js');
const { StdioServerTransport } = require('@modelcontextprotocol/sdk/server/stdio.js');

// Start the server with stdio transport
async function main() {
  const server = createMcpServer();
  const transport = new StdioServerTransport();
  await server.connect(transport);

  // Don't log to console - it breaks MCP JSON communication over stdio
  // Server is now running and listening on stdin/stdout
}

main().catch(console.error);
