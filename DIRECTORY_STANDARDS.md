# Directory Structure & Cross-Platform Build Standards

## Project Directory Structure

```
lfm/
├── src/                        # Source code
│   ├── Lfm.Cli/               # CLI application project
│   └── Lfm.Core/              # Core library project
├── build/                      # Build artifacts (local development)
│   ├── Debug/
│   └── Release/
├── publish/                    # Published distributions
│   ├── win-x64/               # Windows x64 binaries
│   ├── linux-x64/             # Linux x64 binaries
│   └── osx-x64/               # macOS x64 binaries (future)
├── docs/                       # Documentation
├── scripts/                    # Build and automation scripts
├── tests/                      # Unit tests (future)
├── *.md                       # Project documentation
└── *.sln                      # Solution file
```

## Cross-Platform Build Standards

### Runtime Identifiers (RIDs)
Use standard .NET RIDs for consistent cross-platform builds:
- **Windows**: `win-x64`
- **Linux**: `linux-x64` 
- **macOS**: `osx-x64` (future support)

### Build Commands

#### Development Builds
```bash
# Debug build (any platform)
dotnet build --configuration Debug

# Release build (any platform)  
dotnet build --configuration Release
```

#### Cross-Platform Publishing
```bash
# Windows x64 (from any platform)
dotnet publish src/Lfm.Cli -c Release -r win-x64 -o publish/win-x64 --self-contained false

# Linux x64 (from any platform)
dotnet publish src/Lfm.Cli -c Release -r linux-x64 -o publish/linux-x64 --self-contained false

# macOS x64 (future)
dotnet publish src/Lfm.Cli -c Release -r osx-x64 -o publish/osx-x64 --self-contained false
```

#### Single Command Multi-Platform Build
```bash
# Build for all supported platforms
dotnet publish src/Lfm.Cli -c Release -r win-x64 -o publish/win-x64 --self-contained false
dotnet publish src/Lfm.Cli -c Release -r linux-x64 -o publish/linux-x64 --self-contained false
```

### Path Handling Standards

#### Project Files (.csproj, .sln)
- **Use forward slashes** in all project references: `../Lfm.Core/Lfm.Core.csproj`
- **Relative paths only** for project references
- **Avoid hardcoded absolute paths**

#### C# Code
```csharp
// ✅ GOOD - Cross-platform path handling
var configPath = Path.Combine(baseDir, "config", "settings.json");

// ❌ BAD - Windows-specific
var configPath = baseDir + "\\config\\settings.json";

// ✅ GOOD - Cross-platform directory detection  
if (OperatingSystem.IsWindows()) { /* Windows-specific */ }
if (OperatingSystem.IsLinux()) { /* Linux-specific */ }
```

### Configuration Locations

#### User Data Directories
Follow platform conventions for user data:

```csharp
// Config files (settings, user preferences)
// Windows: %APPDATA%\lfm\ → C:\Users\user\AppData\Roaming\lfm\
// Linux: ~/.config/lfm/
var configDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
    "lfm"
);

// Cache files (temporary, performance data)  
// Windows: %LOCALAPPDATA%\lfm\cache\ → C:\Users\user\AppData\Local\lfm\cache\
// Linux: ~/.cache/lfm/
var cacheDir = GetCacheDirectory();

private static string GetCacheDirectory()
{
    if (OperatingSystem.IsWindows())
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lfm", "cache");
    
    var xdgCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
    return !string.IsNullOrEmpty(xdgCache) 
        ? Path.Combine(xdgCache, "lfm")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "lfm");
}
```

### Development Environment Setup

#### Required Tools
- **.NET 9.0 SDK** - Cross-platform development
- **Git** - Version control with cross-platform line endings
- **WSL** - Windows developers should test Linux compatibility

#### Git Configuration (Cross-Platform Line Endings)
```bash
# Set line ending handling for the repository
git config core.autocrlf false
git config core.eol lf
```

#### IDE Configuration
- **File Encoding**: UTF-8 (no BOM)
- **Line Endings**: LF (Unix-style)
- **Indentation**: 4 spaces (no tabs)

### Testing Standards

#### Cross-Platform Testing Checklist
- [ ] Build succeeds on Windows
- [ ] Build succeeds on Linux/WSL  
- [ ] Published binaries run on target platforms
- [ ] File paths work correctly on both platforms
- [ ] Configuration directories created properly
- [ ] Cache directories follow platform conventions

#### Test Commands
```bash
# Test current platform build
dotnet run --project src/Lfm.Cli --configuration Release

# Test published binary (Linux)
./publish/linux-x64/lfm --version

# Test published binary (Windows)  
.\publish\win-x64\lfm.exe --version
```

### Build Script Standards (Future)

Create platform-specific build scripts in `scripts/` directory:
- `scripts/build.sh` - Linux/macOS build script
- `scripts/build.bat` - Windows build script  
- `scripts/publish-all.sh` - Multi-platform publish script

## Best Practices Summary

1. **Always use `Path.Combine()`** for file paths
2. **Test on both Windows and Linux/WSL** during development
3. **Use standard .NET RIDs** for publishing
4. **Follow platform conventions** for user directories
5. **Keep build artifacts separate** from source code
6. **Use relative paths** in project files
7. **Maintain consistent directory structure** across environments

## Integration with Caching Project

The caching system will follow these standards:
- **Cache location**: Platform-appropriate cache directory
- **File paths**: Cross-platform `Path.Combine()` usage
- **Testing**: Validate on both Windows and Linux/WSL
- **Build integration**: No impact on existing build process