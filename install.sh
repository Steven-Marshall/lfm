#!/usr/bin/env bash
# LFM Installation Script for macOS and Linux
# Usage: curl -fsSL https://raw.githubusercontent.com/Steven-Marshall/lfm/master/install.sh | bash

set -e

VERSION="${1:-latest}"

echo ""
echo "========================================"
echo "  LFM - Last.fm CLI Tool Installer     "
echo "========================================"
echo ""

# Detect OS and architecture
OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
    Darwin*)
        if [ "$ARCH" = "arm64" ]; then
            PLATFORM="macos-apple-silicon"
            ARCHIVE_EXT="zip"
        else
            PLATFORM="macos-intel"
            ARCHIVE_EXT="zip"
        fi
        ;;
    Linux*)
        PLATFORM="linux-x64"
        ARCHIVE_EXT="tar.gz"
        ;;
    *)
        echo "✗ Unsupported operating system: $OS"
        exit 1
        ;;
esac

echo "Detected: $OS ($ARCH)"
echo "Platform: $PLATFORM"
echo ""

# Define installation directory
INSTALL_DIR="$HOME/.local/bin"
BINARY_PATH="$INSTALL_DIR/lfm"

# Create installation directory
echo "Creating installation directory..."
mkdir -p "$INSTALL_DIR"

# Determine download URL
if [ "$VERSION" = "latest" ]; then
    DOWNLOAD_URL="https://github.com/Steven-Marshall/lfm/releases/latest/download/lfm-${PLATFORM}.${ARCHIVE_EXT}"
else
    DOWNLOAD_URL="https://github.com/Steven-Marshall/lfm/releases/download/v${VERSION}/lfm-${PLATFORM}.${ARCHIVE_EXT}"
fi

# Download
echo "Downloading LFM from GitHub..."
TEMP_FILE="/tmp/lfm-download.${ARCHIVE_EXT}"

if command -v curl &> /dev/null; then
    curl -fSL "$DOWNLOAD_URL" -o "$TEMP_FILE"
elif command -v wget &> /dev/null; then
    wget -q "$DOWNLOAD_URL" -O "$TEMP_FILE"
else
    echo "✗ Neither curl nor wget found. Please install one of them."
    exit 1
fi

echo "✓ Download complete"

# Extract archive
echo "Extracting files..."
if [ "$ARCHIVE_EXT" = "zip" ]; then
    # macOS
    unzip -o "$TEMP_FILE" -d "/tmp/lfm-extract" > /dev/null
    mv "/tmp/lfm-extract/lfm" "$BINARY_PATH"
    rm -rf "/tmp/lfm-extract"
else
    # Linux (tar.gz)
    tar -xzf "$TEMP_FILE" -C "/tmp"
    mv "/tmp/lfm" "$BINARY_PATH"
fi

echo "✓ Extraction complete"

# Make executable
chmod +x "$BINARY_PATH"
echo "✓ Made executable"

# Clean up
rm -f "$TEMP_FILE"

# Add to PATH if not already there
echo ""
echo "Checking PATH configuration..."

# Detect shell
SHELL_NAME="$(basename "$SHELL")"
case "$SHELL_NAME" in
    bash)
        if [ "$OS" = "Darwin" ]; then
            SHELL_RC="$HOME/.bash_profile"
        else
            SHELL_RC="$HOME/.bashrc"
        fi
        ;;
    zsh)
        SHELL_RC="$HOME/.zshrc"
        ;;
    fish)
        SHELL_RC="$HOME/.config/fish/config.fish"
        PATH_CMD='set -gx PATH $HOME/.local/bin $PATH'
        ;;
    *)
        SHELL_RC="$HOME/.profile"
        ;;
esac

# Default PATH command for bash/zsh
if [ -z "$PATH_CMD" ]; then
    PATH_CMD='export PATH="$HOME/.local/bin:$PATH"'
fi

# Check if already in PATH
if [[ ":$PATH:" != *":$INSTALL_DIR:"* ]]; then
    echo "Adding LFM to PATH in $SHELL_RC..."

    # Create shell RC if it doesn't exist
    touch "$SHELL_RC"

    # Add to PATH if not already there
    if ! grep -q "\.local/bin" "$SHELL_RC"; then
        echo "" >> "$SHELL_RC"
        echo "# Added by LFM installer" >> "$SHELL_RC"
        echo "$PATH_CMD" >> "$SHELL_RC"
        echo "✓ Added to PATH"
        echo ""
        echo "⚠ Important: Run 'source $SHELL_RC' or restart your terminal"
    else
        echo "✓ PATH entry already exists in $SHELL_RC"
    fi

    # Update PATH for current session
    export PATH="$INSTALL_DIR:$PATH"
else
    echo "✓ Already in PATH"
fi

# Verify installation
echo ""
echo "Verifying installation..."
if [ -x "$BINARY_PATH" ]; then
    VERSION_OUTPUT=$("$BINARY_PATH" --version 2>&1 || echo "version check failed")
    echo "✓ LFM installed successfully: $VERSION_OUTPUT"
else
    echo "⚠ Installation complete but verification failed"
    echo "You may need to restart your terminal"
fi

# Installation complete
echo ""
echo "========================================"
echo "  Installation Complete!                "
echo "========================================"
echo ""

# Next steps
echo "Next Steps:"
echo ""
echo "1. Get your Last.fm API key:"
echo "   https://www.last.fm/api/account/create"
echo ""
echo "2. Configure LFM:"
echo "   lfm config set api-key YOUR_API_KEY"
echo "   lfm config set username YOUR_LASTFM_USERNAME"
echo ""
echo "3. Try it out:"
echo "   lfm artists --limit 5"
echo ""
echo "4. (Optional) Set up MCP for Claude integration:"
echo "   https://github.com/Steven-Marshall/lfm/blob/master/MCP_SETUP.md"
echo ""
echo "For help: lfm --help"
echo "Documentation: https://github.com/Steven-Marshall/lfm"
echo ""
