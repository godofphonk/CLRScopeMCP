# NuGet Global Tool Installation

Install ClrScope.Mcp as a global .NET tool for system-wide access.

## Installation

```bash
dotnet tool install --global ClrScope.Mcp
```

This installs the tool in your global .NET tools directory (`~/.dotnet/tools` on Linux/macOS, `%USERPROFILE%\.dotnet\tools` on Windows) and adds it to your PATH.

## Verification

```bash
clrscope-mcp --version
```

Output:
```
CLRScope MCP v1.1.0
```

## Usage

### CLI Mode

Run directly from command line:
```bash
clrscope-mcp --help
clrscope-mcp --demo
```

### MCP Server Mode

Configure in your IDE's MCP settings (e.g., VS Code `settings.json`):

```json
{
  "mcpServers": {
    "clrscope": {
      "command": "clrscope-mcp",
      "args": []
    }
  }
}
```

## Uninstallation

```bash
dotnet tool uninstall --global ClrScope.Mcp
```

## Advantages

- System-wide availability
- Automatic PATH configuration
- Simple one-command installation
- Easy to update with `dotnet tool update --global ClrScope.Mcp`

## Requirements

- **.NET SDK 10.0 or later** (includes .NET 10.0 runtime)
  - Download: https://dotnet.microsoft.com/download/dotnet/10.0
  - Verify installation: `dotnet --version`
- Internet connection for initial download
