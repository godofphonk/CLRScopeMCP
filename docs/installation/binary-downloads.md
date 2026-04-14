# Binary Distribution Installation

Download pre-built binaries for your platform without requiring .NET SDK installation.

## Download

Visit the [GitHub Releases](https://github.com/godofphonk/CLRScopeMCP/releases/latest) page and download the appropriate binary:

- **Linux x64:** `clrscope-mcp-linux-x64.tar.gz`
- **Linux ARM64:** `clrscope-mcp-linux-arm64.tar.gz`
- **macOS ARM64:** `clrscope-mcp-osx-arm64.tar.gz` (Apple Silicon)
- **Windows x64:** `clrscope-mcp-win-x64.zip`

## Installation

### Linux/macOS

```bash
# Extract
tar xzf clrscope-mcp-linux-x64.tar.gz

# Make executable
chmod +x ClrScope.Mcp

# Verify
./ClrScope.Mcp --version
```

### Windows

```powershell
# Extract
unzip clrscope-mcp-win-x64.zip

# Verify
.\ClrScope.Mcp.exe --version
```

## System-wide Installation (Optional)

### Linux/macOS

```bash
# Move to /usr/local/bin
sudo mv ClrScope.Mcp /usr/local/bin/clrscope-mcp

# Verify
clrscope-mcp --version
```

### Windows

Add the directory containing `ClrScope.Mcp.exe` to your PATH environment variable.

## Usage

### CLI Mode

```bash
# Linux/macOS
./ClrScope.Mcp --help

# Windows
.\ClrScope.Mcp.exe --help
```

### MCP Server Mode

Configure in your IDE's MCP settings (e.g., VS Code `settings.json`):

```json
{
  "mcpServers": {
    "clrscope": {
      "command": "/path/to/ClrScope.Mcp",
      "args": []
    }
  }
}
```

## Advantages

- No .NET SDK required
- Self-contained single-file binary
- Works on any supported platform
- Easy to distribute and deploy

## Requirements

- Operating system: Linux (x64/ARM64), macOS ARM64, or Windows x64
- No .NET SDK or runtime required
