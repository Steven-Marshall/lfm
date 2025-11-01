#!/bin/bash
# Deployment Environment Checks
# Run this on both spark and raspberry pi to verify readiness

echo "========================================="
echo "LFM MCP Deployment Environment Check"
echo "========================================="
echo ""

# Check hostname
echo "📍 Hostname: $(hostname)"
echo ""

# Check Docker
echo "🐳 Docker:"
if command -v docker &> /dev/null; then
    echo "  ✅ Docker installed: $(docker --version)"
    echo "  ✅ Docker running: $(docker info > /dev/null 2>&1 && echo 'Yes' || echo 'No - start with: sudo systemctl start docker')"
else
    echo "  ❌ Docker not installed"
fi
echo ""

# Check Docker Compose
echo "🐳 Docker Compose:"
if command -v docker-compose &> /dev/null; then
    echo "  ✅ docker-compose: $(docker-compose --version)"
elif docker compose version &> /dev/null; then
    echo "  ✅ docker compose (plugin): $(docker compose version)"
else
    echo "  ❌ Docker Compose not installed"
fi
echo ""

# Check port 8002
echo "🔌 Port 8002 (LFM MCP SSE):"
if netstat -tuln 2>/dev/null | grep -q :8002; then
    echo "  ⚠️  Port 8002 is IN USE:"
    netstat -tuln | grep :8002
elif ss -tuln 2>/dev/null | grep -q :8002; then
    echo "  ⚠️  Port 8002 is IN USE:"
    ss -tuln | grep :8002
else
    echo "  ✅ Port 8002 is available"
fi
echo ""

# Check port 8001
echo "🔌 Port 8001 (MCPO for Open WebUI):"
if netstat -tuln 2>/dev/null | grep -q :8001; then
    echo "  ⚠️  Port 8001 is IN USE:"
    netstat -tuln | grep :8001
elif ss -tuln 2>/dev/null | grep -q :8001; then
    echo "  ⚠️  Port 8001 is IN USE:"
    ss -tuln | grep :8001
else
    echo "  ✅ Port 8001 is available"
fi
echo ""

# Check Open WebUI
echo "🌐 Open WebUI:"
if docker ps | grep -q open-webui; then
    echo "  ✅ Open WebUI container running:"
    docker ps | grep open-webui | awk '{print "     Container: " $NF ", Ports: " $(NF-1)}'

    # Get network info
    WEBUI_CONTAINER=$(docker ps | grep open-webui | awk '{print $NF}')
    echo "  📡 Networks:"
    docker inspect $WEBUI_CONTAINER --format '{{range $key, $value := .NetworkSettings.Networks}}    - {{$key}}{{"\n"}}{{end}}'
else
    echo "  ⚠️  Open WebUI container not running"
    echo "     (This is OK if deploying to different machine)"
fi
echo ""

# Check disk space
echo "💾 Disk Space:"
df -h / | tail -1 | awk '{print "  Available: " $4 " (" $5 " used)"}'
echo ""

# Check architecture
echo "🏗️  Architecture:"
echo "  CPU: $(uname -m)"
echo "  OS: $(uname -s)"
if [ -f /etc/os-release ]; then
    . /etc/os-release
    echo "  Distribution: $NAME $VERSION"
fi
echo ""

# Check .NET compatibility (needed for LFM CLI)
echo "📦 .NET Runtime:"
if command -v dotnet &> /dev/null; then
    echo "  ✅ .NET installed: $(dotnet --version)"
else
    echo "  ⚠️  .NET not installed (OK - Docker will provide it)"
fi
echo ""

# Check Node.js (for MCP server)
echo "📦 Node.js:"
if command -v node &> /dev/null; then
    echo "  ✅ Node.js: $(node --version)"
else
    echo "  ⚠️  Node.js not installed (OK - Docker will provide it)"
fi
echo ""

echo "========================================="
echo "✅ Check complete!"
echo "========================================="
echo ""
echo "Next steps:"
echo "1. If Docker not running: sudo systemctl start docker"
echo "2. If ports in use: identify and stop conflicting services"
echo "3. Note the Open WebUI network name (if present)"
echo "4. Share this output for review"
