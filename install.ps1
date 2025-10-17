# LFM Installation Script for Windows
# Usage: iwr -useb https://raw.githubusercontent.com/Steven-Marshall/lfm/master/install.ps1 | iex

param(
    [string]$Version = "latest"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  LFM - Last.fm CLI Tool Installer     " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Define installation directory
$InstallDir = "$env:USERPROFILE\lfm"
$BinaryPath = "$InstallDir\lfm.exe"

# Create installation directory
Write-Host "Creating installation directory..." -ForegroundColor Yellow
if (!(Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Determine download URL
$DownloadUrl = if ($Version -eq "latest") {
    "https://github.com/Steven-Marshall/lfm/releases/latest/download/lfm-windows-x64.zip"
} else {
    "https://github.com/Steven-Marshall/lfm/releases/download/v$Version/lfm-windows-x64.zip"
}

Write-Host "Downloading LFM from GitHub..." -ForegroundColor Yellow
$TempZip = "$env:TEMP\lfm-windows-x64.zip"

try {
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $TempZip -UseBasicParsing
    Write-Host "✓ Download complete" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to download LFM" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}

# Extract archive
Write-Host "Extracting files..." -ForegroundColor Yellow
try {
    # Remove existing exe if present
    if (Test-Path $BinaryPath) {
        Remove-Item $BinaryPath -Force
    }

    # Extract using .NET (works on all Windows versions)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $ZipFile = [System.IO.Compression.ZipFile]::OpenRead($TempZip)
    $Entry = $ZipFile.Entries | Where-Object { $_.Name -eq "lfm.exe" }

    if ($Entry) {
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($Entry, $BinaryPath, $true)
        Write-Host "✓ Extraction complete" -ForegroundColor Green
    } else {
        throw "lfm.exe not found in archive"
    }

    $ZipFile.Dispose()
} catch {
    Write-Host "✗ Failed to extract files" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
} finally {
    # Clean up temp file
    if (Test-Path $TempZip) {
        Remove-Item $TempZip -Force
    }
}

# Add to PATH if not already there
Write-Host "Checking PATH configuration..." -ForegroundColor Yellow
$UserPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($UserPath -notlike "*$InstallDir*") {
    Write-Host "Adding LFM to PATH..." -ForegroundColor Yellow
    try {
        $NewPath = "$UserPath;$InstallDir"
        [Environment]::SetEnvironmentVariable("Path", $NewPath, "User")

        # Update current session PATH
        $env:Path = "$env:Path;$InstallDir"

        Write-Host "✓ Added to PATH" -ForegroundColor Green
    } catch {
        Write-Host "⚠ Failed to add to PATH automatically" -ForegroundColor Yellow
        Write-Host "Please add manually: $InstallDir" -ForegroundColor Yellow
    }
} else {
    Write-Host "✓ Already in PATH" -ForegroundColor Green
}

# Verify installation
Write-Host ""
Write-Host "Verifying installation..." -ForegroundColor Yellow
try {
    # Refresh PATH for verification
    $env:Path = [Environment]::GetEnvironmentVariable("Path", "User") + ";" + [Environment]::GetEnvironmentVariable("Path", "Machine")

    $VersionOutput = & $BinaryPath --version 2>&1
    Write-Host "✓ LFM installed successfully: $VersionOutput" -ForegroundColor Green
} catch {
    Write-Host "⚠ Installation complete but verification failed" -ForegroundColor Yellow
    Write-Host "You may need to restart PowerShell" -ForegroundColor Yellow
}

# Installation complete
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Installation Complete!                " -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Next steps
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Get your Last.fm API key:" -ForegroundColor White
Write-Host "   https://www.last.fm/api/account/create" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Configure LFM:" -ForegroundColor White
Write-Host "   lfm config set api-key YOUR_API_KEY" -ForegroundColor Gray
Write-Host "   lfm config set username YOUR_LASTFM_USERNAME" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Try it out:" -ForegroundColor White
Write-Host "   lfm artists --limit 5" -ForegroundColor Gray
Write-Host ""
Write-Host "4. (Optional) Set up MCP for Claude integration:" -ForegroundColor White
Write-Host "   See: https://github.com/Steven-Marshall/lfm/blob/master/MCP_SETUP.md" -ForegroundColor Gray
Write-Host ""

# Prompt to restart shell if PATH was modified
if ($UserPath -notlike "*$InstallDir*") {
    Write-Host "⚠ Important: Restart PowerShell for PATH changes to take effect" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "For help: lfm --help" -ForegroundColor White
Write-Host "Documentation: https://github.com/Steven-Marshall/lfm" -ForegroundColor White
Write-Host ""
