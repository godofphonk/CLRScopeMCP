# CLRScope MCP

![Version](https://img.shields.io/github/v/release/godofphonk/CLRScopeMCP)
![Badge](https://img.shields.io/badge/.NET-10.0%2B-purple)
![Badge](https://img.shields.io/badge/license-MIT-green)

> MCP server for comprehensive .NET application diagnostics

## Overview

CLRScope MCP provides AI-powered diagnostic capabilities for .NET applications through the Model Context Protocol. It enables LLM agents to perform deep analysis of .NET processes including performance profiling, memory leak detection, thread analysis, and automated pattern detection.

## Features

| Feature | Description |
|---------|-------------|
| 🎯 **Runtime Detection** | Attachable .NET process detection (host OS only; target architecture currently unavailable) |
| 📊 **Performance Counters** | Real-time CPU, memory, GC, and thread pool metrics |
| 🔥 **Flame Graph Visualization** | Enhanced stack trace visualization with Dump/Trace support, caching, and progress reporting (v1.2.0) |
| **Memory Dump Analysis** | Compressed dump support with automatic decompression |
| **SOS Commands** | Sequential SOS command execution for deep analysis |
| **Pattern Detection** | Automatic detection of memory leaks, deadlocks, high CPU |
| **Artifact Management** | Pagination, filtering, and cleanup strategies |
| **Baseline Comparison** | Compare diagnostic sessions with baseline |
| 🤖 **Automated Workflows** | One-click diagnostic bundles for common scenarios (high CPU, memory leaks, hangs, baseline) |

## System Requirements

- **.NET 10.0 runtime** must be installed on your system
  - Download: https://dotnet.microsoft.com/download/dotnet/10.0
  - Verify installation: `dotnet --version`

## Installation

### Recommended: NuGet Global Tool ⭐

The recommended installation method for most users:

```bash
dotnet tool install --global ClrScope.Mcp
```

[See detailed instructions](docs/installation/nuget-global-tool.md)

### MCP Installation (Official .NET MCP Flow) 🚀

For MCP clients that support the official .NET MCP discovery mechanism (VS Code, Visual Studio, etc.), CLRScope MCP can be installed and run using the `dnx` command from .NET 10 SDK:

```bash
dnx ClrScope.Mcp@1.2.0 --yes
```

This command will:
- Download the CLRScope.Mcp package from NuGet.org
- Extract and execute the MCP server
- Wait for MCP protocol messages over stdin/stdout

**MCP Client Configuration:**

For VS Code or Visual Studio, configure your MCP settings (e.g., VS Code `settings.json`):

```json
{
  "mcpServers": {
    "clrscope": {
      "type": "stdio",
      "command": "dnx",
      "args": ["ClrScope.Mcp@1.2.0", "--yes"]
    }
  }
}
```

**Requirements:**
- .NET 10 SDK or later (includes the `dnx` command)
- No pre-installation of the global tool required

This approach provides better discoverability through the MCP Registry and follows the official Microsoft pattern for .NET MCP servers.

### Alternative Installation Methods

- [Binary Downloads](docs/installation/binary-downloads.md) - Pre-built binaries for your platform (no .NET SDK required)
- [Local .NET Tool](docs/installation/dotnet-local-tool.md) - Project-specific installation

### IDE Configuration (Legacy)

After installation, configure your IDE's MCP settings (e.g., VS Code `settings.json`):

```json
{
  "mcpServers": {
    "clrscope": {
      "command": "/path/to/clrscope-mcp",
      "args": []
    }
  }
}
```
## Required Utilities

For full functionality, the following CLI tools are required:

- **dotnet-dump** - Memory dump collection
- **dotnet-gcdump** - GC heap snapshots
- **dotnet-stack** - Thread stack analysis
- **dotnet-counters** - Performance monitoring
- **dotnet-symbol** - Symbol resolution

See [Full Requirements](docs/requirements.md) for installation instructions and feature-specific requirements.

## Documentation

- [Investigation Guides](docs/investigation-guides.md) - Step-by-step guides for memory leaks, hangs, high CPU, and baseline performance collection
- [Best Practices](docs/best-practices.md) - Recommended workflows and optimization techniques for effective diagnostics
- [Troubleshooting](docs/troubleshooting.md) - Solutions to common issues and error messages
- [Tool Integration](docs/integration.md) - Required CLI tools and installation instructions for diagnostic features
- [Full Requirements](docs/requirements.md) - Comprehensive requirements for using all CLRScope MCP features
