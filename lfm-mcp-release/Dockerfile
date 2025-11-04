# Multi-stage Dockerfile for LFM MCP Server
# Supports both AMD64 and ARM64 architectures

# Build argument for target architecture (linux-x64 or linux-arm64)
ARG TARGET_ARCH=linux-x64

# Stage 1: Build .NET CLI binary
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-dotnet

ARG TARGET_ARCH
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

# Build and publish for specified architecture
# Default: linux-x64 (for local testing)
# Override with --build-arg TARGET_ARCH=linux-arm64 for Spark/ARM64
RUN dotnet publish ./Lfm.Cli/Lfm.Cli.csproj \
    -c Release \
    -r ${TARGET_ARCH} \
    -o /app/lfm \
    --self-contained false

# Stage 2: Runtime image with Node.js
FROM node:20-slim

# Install .NET runtime dependencies and .NET runtime
RUN apt-get update && apt-get install -y \
    ca-certificates \
    libc6 \
    libgcc-s1 \
    libgssapi-krb5-2 \
    libicu72 \
    libssl3 \
    libstdc++6 \
    zlib1g \
    wget \
    && rm -rf /var/lib/apt/lists/*

# Install .NET 8 Runtime (matches target framework in csproj)
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir /usr/share/dotnet \
    && rm dotnet-install.sh \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

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

# Default: Run Streamable HTTP server
CMD ["/app/docker-entrypoint.sh"]
