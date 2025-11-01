#!/usr/bin/env node

/**
 * Script to create server-core.js from server.js
 * Extracts shared logic into a reusable module
 */

const fs = require('fs');
const path = require('path');

console.log('Creating server-core.js...');

const serverJs = fs.readFileSync(path.join(__dirname, 'server.js'), 'utf8');

// Extract sections (line numbers are 1-indexed, arrays are 0-indexed)
const lines = serverJs.split('\n');

// Utilities (lines 16-165) -> array index 15-164
const utilities = lines.slice(15, 165).join('\n');

// Guidelines (lines 167-270) -> array index 166-269
const guidelines = lines.slice(166, 270).join('\n');

// Server creation and handlers (lines 273-2613) -> array index 272-2612
// This includes server instantiation, ListToolsRequestSchema, and CallToolRequestSchema handlers
const serverSection = lines.slice(272, 2613).join('\n');

// Now build server-core.js
const serverCore = `// server-core.js
// Shared MCP server logic for LFM
// Extracted from server.js for reuse across stdio and HTTP transports

const { spawn } = require('child_process');
const { Server } = require('@modelcontextprotocol/sdk/server/index.js');
const {
  ListToolsRequestSchema,
  CallToolRequestSchema
} = require('@modelcontextprotocol/sdk/types.js');
const fs = require('fs');
const path = require('path');

// ============================================
// UTILITY FUNCTIONS
// ============================================

${utilities}

// ============================================
// GUIDELINES FUNCTIONS
// ============================================

${guidelines}

// ============================================
// MCP SERVER FACTORY
// ============================================

/**
 * Creates and configures an MCP server instance with all LFM tools
 * @returns {Server} Configured MCP server ready to connect to a transport
 */
function createMcpServer() {
  // Create MCP server
${serverSection}

  // Return the configured server
  return server;
}

// ============================================
// EXPORTS
// ============================================

module.exports = {
  createMcpServer,
  executeLfmCommand,   // Exported for testing
  parseJsonOutput      // Exported for testing
};
`;

// Write server-core.js
fs.writeFileSync(path.join(__dirname, 'server-core.js'), serverCore, 'utf8');

console.log('✅ server-core.js created successfully!');
console.log(`   Total lines: ${serverCore.split('\n').length}`);
console.log('   Extracted:');
console.log(`     - Utility functions (lines 16-165)`);
console.log(`     - Guidelines functions (lines 167-270)`);
console.log(`     - Server creation and all tool handlers (lines 273-2613)`);
console.log('');
console.log('Next: Test that server-core.js exports work correctly');
