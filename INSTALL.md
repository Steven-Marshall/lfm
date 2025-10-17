# Installing LFM

LFM is a command-line tool for accessing your Last.fm music statistics. This guide will help you install it on your computer.

## Choose Your Platform

- [Windows](#windows)
- [macOS](#macos)
- [Linux](#linux)

---

## Windows

### Quick Install (Recommended)

1. **Open PowerShell**
   - Press `Windows + X`
   - Select "Windows PowerShell" or "Terminal"

2. **Copy and paste this command:**
   ```powershell
   iwr -useb https://raw.githubusercontent.com/Steven-Marshall/lfm/master/install.ps1 | iex
   ```

3. **Wait for installation** (about 30 seconds)

4. **Verify installation:**
   ```powershell
   lfm --version
   ```
   You should see: `1.5.0` (or later)

### Manual Install

1. **Download the Windows version**
   - Go to [Latest Release](https://github.com/Steven-Marshall/lfm/releases/latest)
   - Download `lfm-windows-x64.exe`

2. **Create a folder for LFM**
   ```powershell
   mkdir C:\Users\$env:USERNAME\lfm
   ```

3. **Move the downloaded file**
   - Move `lfm-windows-x64.exe` to `C:\Users\YourUsername\lfm\`
   - Rename it to `lfm.exe`

4. **Add to PATH**
   - Press `Windows + R`
   - Type: `sysdm.cpl` and press Enter
   - Click "Environment Variables"
   - Under "User variables", select "Path"
   - Click "Edit" → "New"
   - Add: `C:\Users\YourUsername\lfm`
   - Click "OK" on all windows

5. **Restart PowerShell** and verify:
   ```powershell
   lfm --version
   ```

### Requirements

- **Operating System**: Windows 10 or later
- **No additional software needed** - the self-contained version includes everything

---

## macOS

### Quick Install (Recommended)

1. **Open Terminal**
   - Press `Cmd + Space`
   - Type "Terminal" and press Enter

2. **Copy and paste this command:**
   ```bash
   curl -fsSL https://raw.githubusercontent.com/Steven-Marshall/lfm/master/install.sh | bash
   ```

3. **Wait for installation** (about 30 seconds)

4. **Verify installation:**
   ```bash
   lfm --version
   ```
   You should see: `1.5.0` (or later)

### Manual Install

1. **Download the correct version for your Mac**
   - Go to [Latest Release](https://github.com/Steven-Marshall/lfm/releases/latest)
   - **Intel Mac**: Download `lfm-macos-intel.zip`
   - **Apple Silicon (M1/M2/M3)**: Download `lfm-macos-apple-silicon.zip`

   *Not sure which you have? Open Terminal and type:*
   ```bash
   uname -m
   ```
   - If it says `x86_64` → Intel Mac
   - If it says `arm64` → Apple Silicon

2. **Extract the download**
   - Double-click the downloaded `.zip` file
   - This creates a file called `lfm` (no extension)

3. **Move to local bin**
   ```bash
   mkdir -p ~/.local/bin
   mv ~/Downloads/lfm ~/.local/bin/
   chmod +x ~/.local/bin/lfm
   ```

4. **Add to PATH** (if not already there)

   For **bash** (older macOS):
   ```bash
   echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bash_profile
   source ~/.bash_profile
   ```

   For **zsh** (macOS Catalina and later):
   ```bash
   echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.zshrc
   source ~/.zshrc
   ```

5. **Verify installation:**
   ```bash
   lfm --version
   ```

### Requirements

- **Operating System**: macOS 10.15 (Catalina) or later
- **No additional software needed** - the self-contained version includes everything

---

## Linux

### Quick Install (Recommended)

1. **Open Terminal**
   - Press `Ctrl + Alt + T` (on most distributions)

2. **Copy and paste this command:**
   ```bash
   curl -fsSL https://raw.githubusercontent.com/Steven-Marshall/lfm/master/install.sh | bash
   ```

3. **Wait for installation** (about 30 seconds)

4. **Verify installation:**
   ```bash
   lfm --version
   ```
   You should see: `1.5.0` (or later)

### Manual Install

1. **Download the Linux version**
   - Go to [Latest Release](https://github.com/Steven-Marshall/lfm/releases/latest)
   - Download `lfm-linux-x64`

2. **Move to local bin**
   ```bash
   mkdir -p ~/.local/bin
   mv ~/Downloads/lfm-linux-x64 ~/.local/bin/lfm
   chmod +x ~/.local/bin/lfm
   ```

3. **Add to PATH** (if not already there)
   ```bash
   echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
   source ~/.bashrc
   ```

   *Note: If you use a different shell (zsh, fish, etc.), update the appropriate config file*

4. **Verify installation:**
   ```bash
   lfm --version
   ```

### Requirements

- **Operating System**: Most modern Linux distributions (Ubuntu 18.04+, Debian 10+, Fedora 30+, etc.)
- **No additional software needed** - the self-contained version includes everything

---

## What's Installed?

The LFM tool gives you access to:
- Your Last.fm music statistics (top artists, tracks, albums)
- Music recommendations based on your listening
- Spotify playback control (optional)
- Sonos playback control (optional)
- Natural language music queries via Claude (optional MCP integration)

---

## Next Steps

Now that LFM is installed:

1. **Configure LFM** → [Quick Start Guide](QUICKSTART.md)
2. **(Optional) Setup MCP Server** → [MCP Setup Guide](MCP_SETUP.md)

---

## Troubleshooting

### "Command not found" error

**On macOS/Linux:**
- Make sure `~/.local/bin` is in your PATH
- Try closing and reopening your terminal
- Run the PATH export command from the installation steps

**On Windows:**
- Make sure you restarted PowerShell after installation
- Check that the folder is in your PATH (see Manual Install step 4)

### Permission denied errors

**On macOS/Linux:**
```bash
chmod +x ~/.local/bin/lfm
```

### More help

See the [Troubleshooting Guide](TROUBLESHOOTING.md) for common issues and solutions.

---

## Uninstalling

### Windows
```powershell
Remove-Item -Recurse C:\Users\$env:USERNAME\lfm
# Then remove from PATH via Environment Variables
```

### macOS/Linux
```bash
rm ~/.local/bin/lfm
```

Configuration and cache files are stored separately and can be removed with:
```bash
# Windows
Remove-Item -Recurse $env:APPDATA\lfm

# macOS/Linux
rm -rf ~/.config/lfm ~/.cache/lfm
```
