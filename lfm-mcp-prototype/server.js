#!/usr/bin/env node

const { spawn } = require('child_process');
const { Server } = require('@modelcontextprotocol/sdk/server/index.js');
const { StdioServerTransport } = require('@modelcontextprotocol/sdk/server/stdio.js');
const {
  ListToolsRequestSchema,
  CallToolRequestSchema
} = require('@modelcontextprotocol/sdk/types.js');

// Simple function to execute lfm command
async function executeLfmCommand(args) {
  return new Promise((resolve, reject) => {
    // Use the main lfm installation
    const lfmPath = 'lfm';
    const process = spawn(lfmPath, args, { cwd: __dirname });

    let stdout = '';
    let stderr = '';

    process.stdout.on('data', (data) => {
      stdout += data.toString();
    });

    process.stderr.on('data', (data) => {
      stderr += data.toString();
    });

    process.on('close', (code) => {
      if (code !== 0) {
        reject(new Error(`Command failed with code ${code}: ${stderr}`));
      } else {
        // Return stdout only, ignore stderr (which may contain logging/warnings)
        resolve(stdout);
      }
    });
  });
}

// Parse JSON output from lfm CLI
function parseJsonOutput(output) {
  try {
    // First try to parse the output as-is
    return JSON.parse(output);
  } catch (error) {
    // If that fails, try to extract JSON from mixed output

    // Method 1: Find a complete JSON object by counting braces
    let startIndex = output.indexOf('{');
    if (startIndex !== -1) {
      let braceCount = 0;
      let endIndex = -1;

      for (let i = startIndex; i < output.length; i++) {
        if (output[i] === '{') braceCount++;
        else if (output[i] === '}') {
          braceCount--;
          if (braceCount === 0) {
            endIndex = i + 1;
            break;
          }
        }
      }

      if (endIndex > startIndex) {
        try {
          const jsonStr = output.substring(startIndex, endIndex);
          return JSON.parse(jsonStr);
        } catch (innerError) {
          // Fall through to next method
        }
      }
    }

    // Method 2: Find JSON array
    startIndex = output.indexOf('[');
    if (startIndex !== -1) {
      let bracketCount = 0;
      let endIndex = -1;

      for (let i = startIndex; i < output.length; i++) {
        if (output[i] === '[') bracketCount++;
        else if (output[i] === ']') {
          bracketCount--;
          if (bracketCount === 0) {
            endIndex = i + 1;
            break;
          }
        }
      }

      if (endIndex > startIndex) {
        try {
          const jsonStr = output.substring(startIndex, endIndex);
          return JSON.parse(jsonStr);
        } catch (innerError) {
          // Fall through to error
        }
      }
    }

    throw new Error(`Failed to parse JSON output: ${error.message}`);
  }
}

// Create MCP server
const server = new Server(
  {
    name: 'lfm-mcp',
    version: '0.1.0',
  },
  {
    capabilities: {
      tools: {},
    },
  }
);

// Add the tracks tool
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: [
      {
        name: 'lfm_tracks',
        description: 'Get top tracks from Last.fm',
        inputSchema: {
          type: 'object',
          properties: {
            limit: {
              type: 'number',
              description: 'Number of tracks to return (1-1000)',
              default: 20
            },
            period: {
              type: 'string',
              description: 'Time period',
              enum: ['overall', '7day', '1month', '3month', '6month', '12month'],
              default: 'overall'
            },
            from: {
              type: 'string',
              description: 'Start date (YYYY-MM-DD or YYYY)'
            },
            to: {
              type: 'string',
              description: 'End date (YYYY-MM-DD or YYYY)'
            },
            year: {
              type: 'string',
              description: 'Specific year (YYYY) - shortcut for entire year'
            }
          }
        }
      },
      {
        name: 'lfm_toptracks',
        description: 'Create playlists from top tracks with optional artist diversity',
        inputSchema: {
          type: 'object',
          properties: {
            limit: {
              type: 'number',
              description: 'Number of tracks to return (1-1000)',
              default: 20
            },
            tracksPerArtist: {
              type: 'number',
              description: 'Maximum tracks per artist (creates diversity)',
              default: 1
            },
            period: {
              type: 'string',
              description: 'Time period',
              enum: ['overall', '7day', '1month', '3month', '6month', '12month'],
              default: 'overall'
            },
            from: {
              type: 'string',
              description: 'Start date (YYYY-MM-DD or YYYY)'
            },
            to: {
              type: 'string',
              description: 'End date (YYYY-MM-DD or YYYY)'
            },
            year: {
              type: 'string',
              description: 'Specific year (YYYY) - shortcut for entire year'
            },
            playlist: {
              type: 'string',
              description: 'Save tracks to a Spotify playlist with this name'
            },
            playNow: {
              type: 'boolean',
              description: 'Queue tracks to Spotify and start playing immediately',
              default: false
            },
            shuffle: {
              type: 'boolean',
              description: 'Shuffle the order when sending to Spotify',
              default: false
            },
            device: {
              type: 'string',
              description: 'Specific Spotify device to use (overrides config default)'
            }
          }
        }
      },
      {
        name: 'lfm_artists',
        description: 'Get top artists from Last.fm',
        inputSchema: {
          type: 'object',
          properties: {
            limit: {
              type: 'number',
              description: 'Number of artists to return (1-1000)',
              default: 20
            },
            period: {
              type: 'string',
              description: 'Time period',
              enum: ['overall', '7day', '1month', '3month', '6month', '12month'],
              default: 'overall'
            },
            from: {
              type: 'string',
              description: 'Start date (YYYY-MM-DD or YYYY)'
            },
            to: {
              type: 'string',
              description: 'End date (YYYY-MM-DD or YYYY)'
            },
            year: {
              type: 'string',
              description: 'Specific year (YYYY) - shortcut for entire year'
            }
          }
        }
      },
      {
        name: 'lfm_albums',
        description: 'Get top albums from Last.fm',
        inputSchema: {
          type: 'object',
          properties: {
            limit: {
              type: 'number',
              description: 'Number of albums to return (1-1000)',
              default: 20
            },
            period: {
              type: 'string',
              description: 'Time period',
              enum: ['overall', '7day', '1month', '3month', '6month', '12month'],
              default: 'overall'
            },
            from: {
              type: 'string',
              description: 'Start date (YYYY-MM-DD or YYYY)'
            },
            to: {
              type: 'string',
              description: 'End date (YYYY-MM-DD or YYYY)'
            },
            year: {
              type: 'string',
              description: 'Specific year (YYYY) - shortcut for entire year'
            }
          }
        }
      },
      {
        name: 'lfm_recommendations',
        description: 'Get music recommendations based on listening history',
        inputSchema: {
          type: 'object',
          properties: {
            limit: {
              type: 'number',
              description: 'Number of recommendations to return (1-100)',
              default: 20
            },
            filter: {
              type: 'number',
              description: 'Minimum play count filter (exclude artists with >= this many plays)',
              default: 0
            },
            tracksPerArtist: {
              type: 'number',
              description: 'Number of top tracks to include per recommended artist',
              default: 0
            },
            period: {
              type: 'string',
              description: 'Time period for analysis',
              enum: ['overall', '7day', '1month', '3month', '6month', '12month'],
              default: 'overall'
            },
            from: {
              type: 'string',
              description: 'Start date (YYYY-MM-DD or YYYY)'
            },
            to: {
              type: 'string',
              description: 'End date (YYYY-MM-DD or YYYY)'
            },
            year: {
              type: 'string',
              description: 'Specific year (YYYY) - shortcut for entire year'
            },
            playlist: {
              type: 'string',
              description: 'Save recommendations to a Spotify playlist with this name'
            },
            playNow: {
              type: 'boolean',
              description: 'Queue recommendations to Spotify and start playing immediately',
              default: false
            },
            shuffle: {
              type: 'boolean',
              description: 'Shuffle the order when sending to Spotify',
              default: false
            },
            device: {
              type: 'string',
              description: 'Specific Spotify device to use (overrides config default)'
            }
          }
        }
      }
    ]
  };
});

// Handle tool calls
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  if (name === 'lfm_tracks') {
    try {
      const limit = args.limit || 20;
      const period = args.period || 'overall';
      const from = args.from;
      const to = args.to;
      const year = args.year;

      // Build command arguments
      const cmdArgs = ['tracks', '--limit', limit.toString(), '--json'];

      // Handle date range parameters
      if (year) {
        cmdArgs.push('--year', year);
      } else if (from || to) {
        // CLI requires both from and to when using date ranges
        const fromDate = from || '2005-01-01'; // Last.fm launched in 2005
        const toDate = to || new Date().toISOString().split('T')[0]; // Today
        cmdArgs.push('--from', fromDate, '--to', toDate);
      } else if (period !== 'overall') {
        cmdArgs.push('--period', period);
      }

      const output = await executeLfmCommand(cmdArgs);
      const result = parseJsonOutput(output);

      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              success: true,
              tracks: result.track || [],
              count: result.track ? result.track.length : 0,
              attributes: result['@attr'] || {}
            }, null, 2)
          }
        ]
      };
    } catch (error) {
      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              success: false,
              error: error.message
            }, null, 2)
          }
        ],
        isError: true
      };
    }
  }

  if (name === 'lfm_toptracks') {
    try {
      const limit = args.limit || 20;
      const tracksPerArtist = args.tracksPerArtist || 1;
      const period = args.period || 'overall';
      const from = args.from;
      const to = args.to;
      const year = args.year;
      const playlist = args.playlist;
      const playNow = args.playNow || false;
      const shuffle = args.shuffle || false;
      const device = args.device;

      // Build command arguments
      const cmdArgs = ['toptracks', '--limit', limit.toString(), '--tracks-per-artist', tracksPerArtist.toString(), '--json'];

      // Add Spotify parameters if specified
      if (playlist) {
        cmdArgs.push('--playlist', playlist);
      }
      if (playNow) {
        cmdArgs.push('--playnow');
      }
      if (shuffle) {
        cmdArgs.push('--shuffle');
      }
      if (device) {
        cmdArgs.push('--device', device);
      }

      // Handle date range parameters
      if (year) {
        cmdArgs.push('--year', year);
      } else if (from || to) {
        // CLI requires both from and to when using date ranges
        const fromDate = from || '2005-01-01'; // Last.fm launched in 2005
        const toDate = to || new Date().toISOString().split('T')[0]; // Today
        cmdArgs.push('--from', fromDate, '--to', toDate);
      } else if (period !== 'overall') {
        cmdArgs.push('--period', period);
      }

      const output = await executeLfmCommand(cmdArgs);
      const result = parseJsonOutput(output);

      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              success: true,
              tracks: result.tracks || [],
              count: result.count || 0,
              tracksPerArtist: result.tracksPerArtist || tracksPerArtist,
              totalArtists: result.totalArtists || 0
            }, null, 2)
          }
        ]
      };
    } catch (error) {
      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              success: false,
              error: error.message
            }, null, 2)
          }
        ],
        isError: true
      };
    }
  }

  if (name === 'lfm_artists') {
    try {
      const limit = args.limit || 20;
      const period = args.period || 'overall';
      const from = args.from;
      const to = args.to;
      const year = args.year;

      // Build command arguments
      const cmdArgs = ['artists', '--limit', limit.toString(), '--json'];

      // Handle date range parameters
      if (year) {
        cmdArgs.push('--year', year);
      } else if (from || to) {
        // CLI requires both from and to when using date ranges
        const fromDate = from || '2005-01-01'; // Last.fm launched in 2005
        const toDate = to || new Date().toISOString().split('T')[0]; // Today
        cmdArgs.push('--from', fromDate, '--to', toDate);
      } else if (period !== 'overall') {
        cmdArgs.push('--period', period);
      }


      const output = await executeLfmCommand(cmdArgs);
      const result = parseJsonOutput(output);

      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              success: true,
              artists: result.artist || [],
              count: result.artist ? result.artist.length : 0,
              attributes: result['@attr'] || {}
            }, null, 2)
          }
        ]
      };
    } catch (error) {
      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              success: false,
              error: error.message
            }, null, 2)
          }
        ],
        isError: true
      };
    }
  }

  if (name === 'lfm_albums') {
    try {
      const limit = args.limit || 20;
      const period = args.period || 'overall';
      const from = args.from;
      const to = args.to;
      const year = args.year;

      // Build command arguments
      const cmdArgs = ['albums', '--limit', limit.toString(), '--json'];

      // Handle date range parameters
      if (year) {
        cmdArgs.push('--year', year);
      } else if (from || to) {
        // CLI requires both from and to when using date ranges
        const fromDate = from || '2005-01-01'; // Last.fm launched in 2005
        const toDate = to || new Date().toISOString().split('T')[0]; // Today
        cmdArgs.push('--from', fromDate, '--to', toDate);
      } else if (period !== 'overall') {
        cmdArgs.push('--period', period);
      }


      const output = await executeLfmCommand(cmdArgs);
      const result = parseJsonOutput(output);

      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              success: true,
              albums: result.album || [],
              count: result.album ? result.album.length : 0,
              attributes: result['@attr'] || {}
            }, null, 2)
          }
        ]
      };
    } catch (error) {
      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              success: false,
              error: error.message
            }, null, 2)
          }
        ],
        isError: true
      };
    }
  }

  if (name === 'lfm_recommendations') {
    try {
      const limit = args.limit || 20;
      const filter = args.filter || 0;
      const tracksPerArtist = args.tracksPerArtist || 0;
      const period = args.period || 'overall';
      const from = args.from;
      const to = args.to;
      const year = args.year;
      const playlist = args.playlist;
      const playNow = args.playNow || false;
      const shuffle = args.shuffle || false;
      const device = args.device;

      // Build command arguments
      const cmdArgs = ['recommendations', '--limit', limit.toString(), '--filter', filter.toString(), '--json'];

      // Add tracks per artist if specified
      if (tracksPerArtist > 0) {
        cmdArgs.push('--tracks-per-artist', tracksPerArtist.toString());
      }

      // Add Spotify parameters if specified
      if (playlist) {
        cmdArgs.push('--playlist', playlist);
      }
      if (playNow) {
        cmdArgs.push('--playnow');
      }
      if (shuffle) {
        cmdArgs.push('--shuffle');
      }
      if (device) {
        cmdArgs.push('--device', device);
      }

      // Handle date range parameters
      if (year) {
        cmdArgs.push('--year', year);
      } else if (from || to) {
        // CLI requires both from and to when using date ranges
        const fromDate = from || '2005-01-01'; // Last.fm launched in 2005
        const toDate = to || new Date().toISOString().split('T')[0]; // Today
        cmdArgs.push('--from', fromDate, '--to', toDate);
      } else if (period !== 'overall') {
        cmdArgs.push('--period', period);
      }

      const output = await executeLfmCommand(cmdArgs);
      const result = parseJsonOutput(output);

      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              success: true,
              recommendations: result.recommendations || [],
              count: result.count || 0,
              filter: result.filter || filter,
              user: result.user || '',
              period: result.period || period
            }, null, 2)
          }
        ]
      };
    } catch (error) {
      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              success: false,
              error: error.message
            }, null, 2)
          }
        ],
        isError: true
      };
    }
  }

  throw new Error(`Unknown tool: ${name}`);
});

// Start the server
async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

main().catch(console.error);