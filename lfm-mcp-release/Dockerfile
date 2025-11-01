# Multi-stage Dockerfile for LFM MCP Server
# Supports both AMD64 and ARM64 architectures

# Stage 1: Build .NET CLI binary
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-dotnet

WORKDIR /src

# Copy C# project files
COPY src/Lfm.Cli/*.csproj ./Lfm.Cli/
COPY src/Lfm.Core/*.csproj ./Lfm.Core/
COPY src/Lfm.Spotify/*.csproj ./Lfm.Spotify/
COPY src/Lfm.Sonos/*.csproj ./Lfm.Sonos/

# Restore dependencies
RUN dotnet restore ./Lfm.Cli/Lfm.Cli.csproj

# Copy source code
COPY src/ ./

# Build and publish for Linux ARM64
# Note: Change to linux-x64 if deploying to AMD64
RUN dotnet publish ./Lfm.Cli/Lfm.Cli.csproj \
    -c Release \
    -r linux-arm64 \
    -o /app/lfm \
    --self-contained false

# Stage 2: Runtime image with Node.js
FROM node:20-slim

# Install .NET runtime dependencies
RUN apt-get update && apt-get install -y \
    ca-certificates \
    libc6 \
    libgcc-s1 \
    libgssapi-krb5-2 \
    libicu72 \
    libssl3 \
    libstdc++6 \
    zlib1g \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy .NET binary from build stage
COPY --from=build-dotnet /app/lfm /app/lfm

# Copy MCP server files
COPY lfm-mcp-release/package*.json ./
COPY lfm-mcp-release/server-core.js ./
COPY lfm-mcp-release/server.js ./
COPY lfm-mcp-release/server-http.js ./
COPY lfm-mcp-release/lfm-guidelines.md ./

# Install Node.js dependencies
RUN npm install --production

# Create directories for config and cache
RUN mkdir -p /app/config /app/cache

# Add lfm binary to PATH
ENV PATH="/app/lfm:${PATH}"

# Copy startup script
COPY lfm-mcp-release/docker-entrypoint.sh ./
RUN chmod +x docker-entrypoint.sh

# Expose HTTP port
EXPOSE 8002

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD node -e "require('http').get('http://localhost:8002/health', (r) => process.exit(r.statusCode === 200 ? 0 : 1))"

# Default: Run HTTP/SSE server
CMD ["/app/docker-entrypoint.sh"]
