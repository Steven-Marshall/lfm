#!/usr/bin/env node

const { spawn } = require('child_process');
const { Server } = require('@modelcontextprotocol/sdk/server/index.js');
const { StdioServerTransport } = require('@modelcontextprotocol/sdk/server/stdio.js');
const {
  ListToolsRequestSchema,
  CallToolRequestSchema
} = require('@modelcontextprotocol/sdk/types.js');

// Cross-platform imports
const fs = require('fs');
const path = require('path');

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

// Cross-platform guidelines loading
const guidelinesPath = path.join(__dirname, 'lfm-guidelines.md');
let guidelinesContent = null;
let guidelinesProvided = false; // Track if guidelines have been shown this session

// Load guidelines on startup with error handling
try {
  guidelinesContent = fs.readFileSync(guidelinesPath, 'utf8');
  // Don't use console.log - it breaks MCP JSON communication
} catch (error) {
  // Don't use console.warn - it breaks MCP JSON communication
  // Guidelines will gracefully fail with appropriate error message
}

// Extract brief guidelines for auto-provision
function getBriefGuidelines() {
  if (!guidelinesContent) return null;

  return `📋 Last.fm Usage Guidelines (auto-loaded on first use):

🗓️ **Temporal Parameters:**
• Year mentions (2023, 2024, 2025) → use year="2025"
• Relative time ("recently", "lately") → use period="1month"
• "this week" → period="7day"

🎵 **Discovery Workflows:**
• New music: use filter=1 to exclude known artists
• Check listening history with lfm_bulk_check before creating playlists
• Use lfm_recommendations → filter → lfm_create_playlist

💡 **Call lfm_guidelines for detailed guidance anytime**`;
}

// Extract specific section from guidelines
function extractSection(content, section) {
  if (!content) {
    return 'Guidelines not available. Please ensure lfm-guidelines.md exists in the server directory.';
  }

  if (section === 'all') {
    return content;
  }

  // Map simplified enum values to actual section headings
  const sectionMap = {
    'temporal': 'Temporal Parameter Selection',
    'workflows': 'Discovery Workflows',
    'patterns': 'Common Parameter Patterns',
    'troubleshooting': 'Troubleshooting',
    'practices': 'Best Practices'
  };

  const actualSection = sectionMap[section] || section;

  // Extract specific section by heading
  const sectionRegex = new RegExp(`## ${actualSection}[\\s\\S]*?(?=## |$)`, 'i');
  const match = content.match(sectionRegex);

  if (match) {
    return match[0];
  }

  // If section not found, return available sections
  const headings = content.match(/## ([^\n]+)/g) || [];
  const availableSections = headings.map(h => h.replace('## ', '')).join(', ');

  return `Section "${section}" not found. Available sections: ${availableSections}`;
}

// Wrap tool response with auto-guidelines on first use
function wrapWithAutoGuidelines(toolResponse) {
  if (guidelinesProvided) {
    return toolResponse; // Guidelines already shown this session
  }

  const briefGuidelines = getBriefGuidelines();
  if (!briefGuidelines) {
    return toolResponse; // No guidelines available
  }

  guidelinesProvided = true; // Mark as provided

  // Prepend guidelines to the tool response
  const originalContent = toolResponse.content || [];
  return {
    ...toolResponse,
    content: [
      {
        type: 'text',
        text: briefGuidelines
      },
      {
        type: 'text',
        text: '---'
      },
      ...originalContent
    ]
  };
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
        description: 'Get top tracks from Last.fm (information only, use lfm_toptracks for playlists)',
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
            totalTracks: {
              type: 'number',
              description: 'Total number of tracks for the playlist (cannot be used with totalArtists)',
              minimum: 1,
              maximum: 100
            },
            totalArtists: {
              type: 'number',
              description: 'Total number of artists to include (cannot be used with totalTracks)',
              minimum: 1,
              maximum: 50,
              default: 20
            },
            filter: {
              type: 'number',
              description: 'Minimum play count filter (exclude artists with >= this many plays)',
              default: 0
            },
            tracksPerArtist: {
              type: 'number',
              description: 'Maximum tracks per artist (default: 1)',
              default: 1,
              minimum: 1,
              maximum: 10
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
      },
      {
        name: 'lfm_toptracks',
        description: 'Create playlists from your top tracks with optional artist diversity',
        inputSchema: {
          type: 'object',
          properties: {
            totalTracks: {
              type: 'number',
              description: 'Total number of tracks for the playlist (cannot be used with totalArtists)',
              minimum: 1,
              maximum: 200,
              default: 20
            },
            totalArtists: {
              type: 'number',
              description: 'Total number of artists to include (cannot be used with totalTracks)',
              minimum: 1,
              maximum: 100
            },
            tracksPerArtist: {
              type: 'number',
              description: 'Maximum tracks per artist (default: 1)',
              default: 1,
              minimum: 1,
              maximum: 10
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
        name: 'lfm_mixtape',
        description: 'Generate a weighted random mixtape from your listening history',
        inputSchema: {
          type: 'object',
          properties: {
            limit: {
              type: 'number',
              description: 'Number of tracks to include in the mixtape',
              minimum: 1,
              maximum: 500,
              default: 25
            },
            bias: {
              type: 'number',
              description: 'Randomness bias (0=completely random, 1=weighted by play count)',
              minimum: 0,
              maximum: 1,
              default: 0.3
            },
            minPlays: {
              type: 'number',
              description: 'Minimum play count filter for tracks',
              minimum: 0,
              default: 0
            },
            tracksPerArtist: {
              type: 'number',
              description: 'Maximum tracks per artist (default: 1)',
              default: 1,
              minimum: 1,
              maximum: 10
            },
            seed: {
              type: 'number',
              description: 'Random seed for reproducible mixtapes'
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
              description: 'Save mixtape to a Spotify playlist with this name'
            },
            playNow: {
              type: 'boolean',
              description: 'Queue mixtape to Spotify and start playing immediately',
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
        name: 'lfm_api_status',
        description: 'Check the health status of Last.fm API endpoints to diagnose connection issues',
        inputSchema: {
          type: 'object',
          properties: {
            verbose: {
              type: 'boolean',
              description: 'Show detailed output including HTTP details',
              default: false
            },
            json: {
              type: 'boolean',
              description: 'Output results in JSON format',
              default: true
            }
          }
        }
      },
      {
        name: 'lfm_check',
        description: 'Check if user has listened to an artist, track, or album with optional track-level breakdown',
        inputSchema: {
          type: 'object',
          properties: {
            artist: {
              type: 'string',
              description: 'Artist name to check'
            },
            track: {
              type: 'string',
              description: 'Track name to check (optional - if not provided, checks artist only)'
            },
            album: {
              type: 'string',
              description: 'Album name to check (optional - provides album play count and track count)'
            },
            user: {
              type: 'string',
              description: 'Last.fm username (uses default if not specified)'
            },
            verbose: {
              type: 'boolean',
              description: 'Show additional information. For albums: includes per-track play counts and listening patterns',
              default: false
            }
          },
          required: ['artist']
        }
      },
      {
        name: 'lfm_bulk_check',
        description: 'Check multiple artists or tracks at once for efficiency',
        inputSchema: {
          type: 'object',
          properties: {
            items: {
              type: 'array',
              description: 'Array of artists or tracks to check',
              items: {
                type: 'object',
                properties: {
                  artist: {
                    type: 'string',
                    description: 'Artist name'
                  },
                  track: {
                    type: 'string',
                    description: 'Track name (optional)'
                  }
                },
                required: ['artist']
              }
            },
            user: {
              type: 'string',
              description: 'Last.fm username (uses default if not specified)'
            }
          },
          required: ['items']
        }
      },
      {
        name: 'lfm_create_playlist',
        description: 'Create a Spotify playlist from a list of artist/track pairs',
        inputSchema: {
          type: 'object',
          properties: {
            tracks: {
              type: 'array',
              description: 'List of track objects with artist and track properties',
              items: {
                type: 'object',
                properties: {
                  artist: {
                    type: 'string',
                    description: 'Artist name'
                  },
                  track: {
                    type: 'string',
                    description: 'Track name'
                  }
                },
                required: ['artist', 'track']
              }
            },
            playlistName: {
              type: 'string',
              description: 'Playlist name (will be prefixed with \'lfm\' if not already)'
            },
            playNow: {
              type: 'boolean',
              description: 'Queue tracks to Spotify and start playing immediately',
              default: false
            },
            shuffle: {
              type: 'boolean',
              description: 'Shuffle the track order when sending to Spotify',
              default: false
            },
            device: {
              type: 'string',
              description: 'Specific Spotify device to use (overrides config default)'
            },
            verbose: {
              type: 'boolean',
              description: 'Show detailed information about track lookups',
              default: false
            }
          },
          required: ['tracks']
        }
      },
      {
        name: 'lfm_similar',
        description: 'Find artists similar to a specified artist using Last.fm\'s similarity algorithm',
        inputSchema: {
          type: 'object',
          properties: {
            artist: {
              type: 'string',
              description: 'Artist name to find similar artists for'
            },
            limit: {
              type: 'number',
              description: 'Number of similar artists to return (1-100)',
              default: 20,
              minimum: 1,
              maximum: 100
            }
          },
          required: ['artist']
        }
      },
      {
        name: 'lfm_guidelines',
        description: 'Get usage guidelines and best practices for Last.fm MCP tools',
        inputSchema: {
          type: 'object',
          properties: {
            section: {
              type: 'string',
              description: 'Specific guideline section to retrieve',
              enum: ['all', 'temporal', 'workflows', 'patterns', 'troubleshooting', 'practices'],
              default: 'all'
            }
          }
        }
      },
      {
        name: 'lfm_play_now',
        description: 'Play a track or album immediately on Spotify',
        inputSchema: {
          type: 'object',
          properties: {
            artist: {
              type: 'string',
              description: 'Artist name'
            },
            track: {
              type: 'string',
              description: 'Track name (required if album not specified)'
            },
            album: {
              type: 'string',
              description: 'Album name (required if track not specified)'
            },
            device: {
              type: 'string',
              description: 'Specific Spotify device to use (overrides config default)'
            }
          },
          required: ['artist']
        }
      },
      {
        name: 'lfm_queue',
        description: 'Add a track or album to the end of the Spotify queue',
        inputSchema: {
          type: 'object',
          properties: {
            artist: {
              type: 'string',
              description: 'Artist name'
            },
            track: {
              type: 'string',
              description: 'Track name (required if album not specified)'
            },
            album: {
              type: 'string',
              description: 'Album name (required if track not specified)'
            },
            device: {
              type: 'string',
              description: 'Specific Spotify device to use (overrides config default)'
            }
          },
          required: ['artist']
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
      const playlist = args.playlist;
      const playNow = args.playNow || false;
      const shuffle = args.shuffle || false;
      const device = args.device;

      // Build command arguments
      const cmdArgs = ['tracks', '--limit', limit.toString(), '--json'];

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

      const response = {
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

      return wrapWithAutoGuidelines(response);
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
      const totalTracks = args.totalTracks;
      const totalArtists = args.totalArtists || 20;
      const filter = args.filter || 0;
      const tracksPerArtist = args.tracksPerArtist || 1;
      const period = args.period || 'overall';
      const from = args.from;
      const to = args.to;
      const year = args.year;
      const playlist = args.playlist;
      const playNow = args.playNow || false;
      const shuffle = args.shuffle || false;
      const device = args.device;

      // Validate mutual exclusivity
      if (totalTracks && totalArtists && args.totalArtists !== undefined) {
        throw new Error('Cannot specify both totalTracks and totalArtists. Choose one.');
      }

      // Build command arguments with new parameter system
      const cmdArgs = ['recommendations', '--filter', filter.toString(), '--tracks-per-artist', tracksPerArtist.toString(), '--json'];

      if (totalTracks) {
        cmdArgs.push('--totaltracks', totalTracks.toString());
      } else {
        cmdArgs.push('--totalartists', totalArtists.toString());
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

  if (name === 'lfm_toptracks') {
    try {
      const totalTracks = args.totalTracks || 20;
      const totalArtists = args.totalArtists;
      const tracksPerArtist = args.tracksPerArtist || 1;
      const period = args.period || 'overall';
      const from = args.from;
      const to = args.to;
      const year = args.year;
      const playlist = args.playlist;
      const playNow = args.playNow || false;
      const shuffle = args.shuffle || false;
      const device = args.device;

      // Validate mutual exclusivity
      if (args.totalTracks && args.totalArtists) {
        throw new Error('Cannot specify both totalTracks and totalArtists. Choose one.');
      }

      // Build command arguments with new parameter system
      const cmdArgs = ['toptracks', '--tracks-per-artist', tracksPerArtist.toString(), '--json'];

      if (totalArtists) {
        cmdArgs.push('--totalartists', totalArtists.toString());
      } else {
        cmdArgs.push('--totaltracks', totalTracks.toString());
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

  if (name === 'lfm_mixtape') {
    try {
      const limit = args.limit || 25;
      const bias = args.bias !== undefined ? args.bias : 0.3;
      const minPlays = args.minPlays || 0;
      const tracksPerArtist = args.tracksPerArtist || 1;
      const seed = args.seed;
      const period = args.period || 'overall';
      const from = args.from;
      const to = args.to;
      const year = args.year;
      const playlist = args.playlist;
      const playNow = args.playNow || false;
      const shuffle = args.shuffle || false;
      const device = args.device;

      // Build command arguments
      const cmdArgs = ['mixtape', '--totaltracks', limit.toString(), '--bias', bias.toString(), '--min-plays', minPlays.toString(), '--tracks-per-artist', tracksPerArtist.toString(), '--json'];

      // Add seed if specified
      if (seed !== undefined) {
        cmdArgs.push('--seed', seed.toString());
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
              tracks: result.tracks || [],
              count: result.count || 0,
              bias: result.bias || bias,
              minPlays: result.minPlays || minPlays,
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

  if (name === 'lfm_api_status') {
    try {
      const verbose = args.verbose || false;
      const json = args.json !== undefined ? args.json : true;

      // Build command arguments
      const cmdArgs = ['api-status'];

      if (verbose) {
        cmdArgs.push('--verbose');
      }

      if (json) {
        cmdArgs.push('--json');
      }

      const output = await executeLfmCommand(cmdArgs);

      // For JSON output, parse and return structured data
      if (json) {
        const result = parseJsonOutput(output);
        return {
          content: [
            {
              type: 'text',
              text: JSON.stringify({
                success: true,
                timestamp: result.timestamp,
                summary: result.summary,
                endpoints: result.endpoints,
                healthyEndpoints: result.endpoints?.filter(e => e.healthy) || [],
                unhealthyEndpoints: result.endpoints?.filter(e => !e.healthy) || []
              }, null, 2)
            }
          ]
        };
      } else {
        // For non-JSON output, return the raw text
        return {
          content: [
            {
              type: 'text',
              text: output
            }
          ]
        };
      }
    } catch (error) {
      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              success: false,
              error: error.message,
              message: 'Failed to check API status. This could indicate a configuration issue or that lfm is not properly installed.'
            }, null, 2)
          }
        ],
        isError: true
      };
    }
  }

  if (name === 'lfm_check') {
    try {
      const artist = args.artist;
      const track = args.track;
      const album = args.album;
      const user = args.user;
      const verbose = args.verbose || false;

      if (!artist) {
        throw new Error('Artist name is required');
      }

      // Build command arguments
      const cmdArgs = ['check', artist];

      // Album takes precedence over track
      if (album) {
        cmdArgs.push('--album', album);
        if (verbose) {
          cmdArgs.push('--verbose');
        }
        cmdArgs.push('--json');  // Always use JSON for albums
      } else if (track) {
        cmdArgs.push(track);
        if (verbose) {
          cmdArgs.push('--verbose');
        }
      } else {
        // Artist only
        if (verbose) {
          cmdArgs.push('--verbose');
        }
      }

      if (user) {
        cmdArgs.push('--user', user);
      }

      const output = await executeLfmCommand(cmdArgs);

      // If album check with JSON, parse and return structured data
      if (album) {
        try {
          const result = parseJsonOutput(output);
          return {
            content: [
              {
                type: 'text',
                text: JSON.stringify(result, null, 2)
              }
            ]
          };
        } catch (parseError) {
          // Fallback to raw output if JSON parsing fails
          return {
            content: [
              {
                type: 'text',
                text: output.trim()
              }
            ]
          };
        }
      }

      return {
        content: [
          {
            type: 'text',
            text: output.trim()
          }
        ]
      };
    } catch (error) {
      return {
        content: [
          {
            type: 'text',
            text: `Error checking listening history: ${error.message}`
          }
        ],
        isError: true
      };
    }
  }

  if (name === 'lfm_bulk_check') {
    try {
      const items = args.items;
      const user = args.user;

      if (!Array.isArray(items) || items.length === 0) {
        throw new Error('Items array is required and must not be empty');
      }

      const results = [];

      for (const item of items) {
        if (!item.artist) {
          results.push(`Error: Missing artist name for item`);
          continue;
        }

        try {
          // Build command arguments for each item
          const cmdArgs = ['check', item.artist];

          if (item.track) {
            cmdArgs.push(item.track);
          }

          if (user) {
            cmdArgs.push('--user', user);
          }

          const output = await executeLfmCommand(cmdArgs);
          results.push(output.trim());
        } catch (itemError) {
          const itemDesc = item.track ? `${item.artist} - ${item.track}` : item.artist;
          results.push(`${itemDesc}: Error - ${itemError.message}`);
        }
      }

      return {
        content: [
          {
            type: 'text',
            text: results.join('\n')
          }
        ]
      };
    } catch (error) {
      return {
        content: [
          {
            type: 'text',
            text: `Error in bulk check: ${error.message}`
          }
        ],
        isError: true
      };
    }
  }

  if (name === 'lfm_create_playlist') {
    try {
      const tracks = args.tracks;
      const playlistName = args.playlistName;
      const playNow = args.playNow || false;
      const shuffle = args.shuffle || false;
      const device = args.device;
      const verbose = args.verbose || false;

      if (!Array.isArray(tracks) || tracks.length === 0) {
        throw new Error('Tracks array is required and must not be empty');
      }

      // Convert tracks array to JSON format for the CLI command
      const jsonInput = JSON.stringify(tracks);

      // Build command arguments
      const cmdArgs = ['create-playlist', jsonInput, '--json'];

      if (playlistName) {
        cmdArgs.push('--playlist', playlistName);
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
      if (verbose) {
        cmdArgs.push('--verbose');
      }

      const output = await executeLfmCommand(cmdArgs);

      return {
        content: [
          {
            type: 'text',
            text: `Playlist creation result:\n${output}`
          }
        ]
      };
    } catch (error) {
      return {
        content: [
          {
            type: 'text',
            text: `Error creating playlist: ${error.message}`
          }
        ],
        isError: true
      };
    }
  }

  if (name === 'lfm_similar') {
    try {
      const artist = args.artist;
      const limit = args.limit || 20;

      if (!artist) {
        throw new Error('Artist name is required');
      }

      // Build command arguments
      const cmdArgs = ['similar', artist, '--limit', limit.toString(), '--json'];

      const output = await executeLfmCommand(cmdArgs);
      const result = parseJsonOutput(output);

      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify(result, null, 2)
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

  if (name === 'lfm_guidelines') {
    try {
      const section = args.section || 'all';
      const guidelines = extractSection(guidelinesContent, section);

      return {
        content: [
          {
            type: 'text',
            text: guidelines
          }
        ]
      };
    } catch (error) {
      return {
        content: [
          {
            type: 'text',
            text: `Error retrieving guidelines: ${error.message}`
          }
        ],
        isError: true
      };
    }
  }

  if (name === 'lfm_play_now') {
    try {
      const artist = args.artist;
      const track = args.track;
      const album = args.album;
      const device = args.device;

      if (!artist) {
        throw new Error('Artist name is required');
      }

      if (!track && !album) {
        throw new Error('Either track or album must be specified');
      }

      if (track && album) {
        throw new Error('Cannot specify both track and album');
      }

      // Build command arguments
      const cmdArgs = ['play', artist, '--json'];

      if (track) {
        cmdArgs.push('--track', track);
      }

      if (album) {
        cmdArgs.push('--album', album);
      }

      if (device) {
        cmdArgs.push('--device', device);
      }

      const output = await executeLfmCommand(cmdArgs);
      const result = parseJsonOutput(output);

      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify(result, null, 2)
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

  if (name === 'lfm_queue') {
    try {
      const artist = args.artist;
      const track = args.track;
      const album = args.album;
      const device = args.device;

      if (!artist) {
        throw new Error('Artist name is required');
      }

      if (!track && !album) {
        throw new Error('Either track or album must be specified');
      }

      if (track && album) {
        throw new Error('Cannot specify both track and album');
      }

      // Build command arguments
      const cmdArgs = ['play', artist, '--queue', '--json'];

      if (track) {
        cmdArgs.push('--track', track);
      }

      if (album) {
        cmdArgs.push('--album', album);
      }

      if (device) {
        cmdArgs.push('--device', device);
      }

      const output = await executeLfmCommand(cmdArgs);
      const result = parseJsonOutput(output);

      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify(result, null, 2)
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