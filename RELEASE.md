# Release Process

This document describes how to create a new release of LFM.

## Overview

LFM uses automated GitHub Actions to build and publish releases. The process is triggered by pushing a version tag.

## Prerequisites

- All changes committed and pushed to `master` branch
- CHANGELOG.md updated with new version details
- Version numbers updated in code (see below)

## Release Steps

### 1. Update Version Numbers

Update version in these files:

**CLI Version** (`src/Lfm.Cli/Lfm.Cli.csproj`):
```xml
<Version>1.6.0</Version>
```

**MCP Server Version** (`lfm-mcp-release/server.js`):
```javascript
const server = new Server(
  {
    name: 'lfm-mcp',
    version: '0.3.0',  // Update this
  },
```

### 2. Update CHANGELOG.md

Add a new section at the top of CHANGELOG.md:

```markdown
## [1.6.0] - 2025-MM-DD

### Added
- New feature descriptions

### Improved
- Enhancement descriptions

### Fixed
- Bug fix descriptions
```

### 3. Commit Version Changes

```bash
git add src/Lfm.Cli/Lfm.Cli.csproj lfm-mcp-release/server.js CHANGELOG.md
git commit -m "chore: Bump version to 1.6.0"
git push
```

### 4. Create and Push Tag

```bash
# Create annotated tag
git tag -a v1.6.0 -m "Release v1.6.0"

# Push tag to GitHub
git push --tags
```

### 5. GitHub Actions Takes Over

Once the tag is pushed, GitHub Actions automatically:

1. **Builds binaries** for all platforms:
   - Windows x64 (self-contained)
   - macOS Intel (self-contained)
   - macOS Apple Silicon (self-contained)
   - Linux x64 (self-contained)

2. **Packages MCP server**:
   - server.js
   - lfm-guidelines.md (CRITICAL - must be included)
   - package.json
   - package-lock.json
   - README.md

3. **Creates GitHub Release**:
   - Generates release notes from template
   - Uploads all binary archives
   - Uploads MCP server package
   - Makes release public

### 6. Verify Release

1. Go to https://github.com/Steven-Marshall/lfm/releases
2. Check that v1.6.0 appears with all assets
3. Verify download links work
4. Test installation scripts point to new version

## What Gets Built

### CLI Binaries

- **lfm-windows-x64.zip** (~60MB self-contained)
  - Includes .NET runtime
  - No prerequisites needed

- **lfm-macos-intel.zip** (~60MB self-contained)
  - For Intel Macs (x86_64)
  - Includes .NET runtime

- **lfm-macos-apple-silicon.zip** (~60MB self-contained)
  - For M1/M2/M3 Macs (ARM64)
  - Includes .NET runtime

- **lfm-linux-x64.tar.gz** (~60MB self-contained)
  - For most Linux distributions
  - Includes .NET runtime

### MCP Server Package

- **lfm-mcp-server-v0.3.0.zip**
  - server.js (MCP server implementation)
  - **lfm-guidelines.md** (CRITICAL - LLM behavior guidelines)
  - package.json (Node.js dependencies)
  - package-lock.json (Locked dependency versions)
  - README.md (MCP-specific documentation)

## Installation Scripts

The one-liner installation scripts automatically download the "latest" release:

**Windows:**
```powershell
iwr -useb https://raw.githubusercontent.com/Steven-Marshall/lfm/master/install.ps1 | iex
```

**macOS/Linux:**
```bash
curl -fsSL https://raw.githubusercontent.com/Steven-Marshall/lfm/master/install.sh | bash
```

These scripts:
- Detect platform automatically
- Download appropriate binary from latest release
- Extract to correct location
- Add to PATH
- Verify installation
- Show next steps

## Troubleshooting

### Build Fails

Check GitHub Actions logs:
1. Go to https://github.com/Steven-Marshall/lfm/actions
2. Click on the failed workflow run
3. Review build logs for errors

Common issues:
- Version number mismatch
- Missing files in MCP package
- .NET SDK version incompatibility

### Tag Already Exists

If you need to recreate a tag:
```bash
# Delete local tag
git tag -d v1.6.0

# Delete remote tag
git push origin :refs/tags/v1.6.0

# Recreate and push
git tag -a v1.6.0 -m "Release v1.6.0"
git push --tags
```

### Release Not Created

If GitHub Actions completes but no release appears:
- Check repository settings → Actions → Workflow permissions
- Ensure "Read and write permissions" is enabled
- Re-run the workflow

## Manual Release (Fallback)

If GitHub Actions is unavailable, you can build manually:

```bash
# Build all platforms
dotnet publish src/Lfm.Cli -c Release -r win-x64 -o publish/win-x64 --self-contained true
dotnet publish src/Lfm.Cli -c Release -r osx-x64 -o publish/osx-x64 --self-contained true
dotnet publish src/Lfm.Cli -c Release -r osx-arm64 -o publish/osx-arm64 --self-contained true
dotnet publish src/Lfm.Cli -c Release -r linux-x64 -o publish/linux-x64 --self-contained true

# Create archives
cd publish/win-x64 && zip -r ../../lfm-windows-x64.zip lfm.exe && cd ../..
cd publish/osx-x64 && zip -r ../../lfm-macos-intel.zip lfm && cd ../..
cd publish/osx-arm64 && zip -r ../../lfm-macos-apple-silicon.zip lfm && cd ../..
cd publish/linux-x64 && tar -czf ../../lfm-linux-x64.tar.gz lfm && cd ../..

# Package MCP server
mkdir -p lfm-mcp-server
cp lfm-mcp-release/server.js lfm-mcp-server/
cp lfm-mcp-release/lfm-guidelines.md lfm-mcp-server/
cp lfm-mcp-release/package.json lfm-mcp-server/
cp lfm-mcp-release/package-lock.json lfm-mcp-server/
cp lfm-mcp-release/README.md lfm-mcp-server/
zip -r lfm-mcp-server-v0.3.0.zip lfm-mcp-server/

# Create GitHub Release manually via web UI
# Upload all archives
```

## Version Numbering

LFM follows [Semantic Versioning](https://semver.org/):

- **MAJOR** (1.x.x) - Breaking changes
- **MINOR** (x.6.x) - New features, backward compatible
- **PATCH** (x.x.1) - Bug fixes, backward compatible

CLI and MCP server versions can differ:
- CLI: 1.6.0
- MCP Server: 0.3.0

This is normal - the MCP server version reflects its own API stability.

## Post-Release

After release is published:

1. **Announce** (if applicable):
   - GitHub Discussions
   - Social media
   - Documentation updates

2. **Monitor**:
   - GitHub Issues for bug reports
   - Download counts
   - User feedback

3. **Update CLAUDE.md**:
   - Document any new features
   - Update version references
   - Archive old session notes if needed

## Quick Reference

```bash
# Standard release process
git add src/Lfm.Cli/Lfm.Cli.csproj lfm-mcp-release/server.js CHANGELOG.md
git commit -m "chore: Bump version to 1.6.0"
git push
git tag -a v1.6.0 -m "Release v1.6.0"
git push --tags
```

That's it! GitHub Actions handles the rest.
