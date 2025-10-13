const { spawn } = require('child_process');

async function testMcpServer() {
  const server = spawn('node', ['server.js'], {
    stdio: ['pipe', 'pipe', 'inherit'],
    cwd: __dirname
  });

  // Test tools/list
  const listRequest = {
    jsonrpc: '2.0',
    id: 1,
    method: 'tools/list',
    params: {}
  };

  server.stdin.write(JSON.stringify(listRequest) + '\n');

  // Test tools/call
  const callRequest = {
    jsonrpc: '2.0',
    id: 2,
    method: 'tools/call',
    params: {
      name: 'lfm_tracks',
      arguments: {
        limit: 5,
        period: '1month'
      }
    }
  };

  server.stdin.write(JSON.stringify(callRequest) + '\n');

  let output = '';
  server.stdout.on('data', (data) => {
    output += data.toString();
    console.log('Response:', data.toString());
  });

  setTimeout(() => {
    server.kill();
    console.log('Test completed');
  }, 5000);
}

testMcpServer().catch(console.error);