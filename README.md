# CLRScope MCP

![Badge](https://img.shields.io/badge/version-v1.0.0-blue)
![Badge](https://img.shields.io/badge/license-MIT-green)

> MCP server for comprehensive .NET application diagnostics

## Overview

CLRScope MCP provides AI-powered diagnostic capabilities for .NET applications through the Model Context Protocol. It enables LLM agents to perform deep analysis of .NET processes including performance profiling, memory leak detection, thread analysis, and automated pattern detection.

## Features

| Feature | Description |
|---------|-------------|
| 🎯 **Runtime Detection** | OS/Architecture detection for attachable .NET processes |
| 📊 **Performance Counters** | Real-time CPU, memory, GC, and thread pool metrics |
| 🔥 **Flame Graph Visualization** | Interactive stack trace visualization |
| **Memory Dump Analysis** | Compressed dump support with automatic decompression |
| **SOS Commands** | Sequential SOS command execution for deep analysis |
| **Pattern Detection** | Automatic detection of memory leaks, deadlocks, high CPU |
| **Artifact Management** | Pagination, filtering, and cleanup strategies |
| **Baseline Comparison** | Compare diagnostic sessions with baseline

## Installation

### Recommended: NuGet Global Tool ⭐

The recommended installation method for most users:

```bash
dotnet tool install --global ClrScope.Mcp
```

[See detailed instructions](docs/installation/nuget-global-tool.md)

### Alternative Installation Methods

- [Binary Downloads](docs/installation/binary-downloads.md) - Pre-built binaries for your platform (no .NET SDK required)
- [Local .NET Tool](docs/installation/dotnet-local-tool.md) - Project-specific installation

### IDE Configuration (VS Code, Visual Studio, etc.)

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
- [Tool Integration](docs/integration.md) - Required CLI tools and installation instructions for diagnostic features
- [Full Requirements](docs/requirements.md) - Comprehensive requirements for using all CLRScope MCP features
- [Full Requirements](docs/requirements.md) for installation instructions and feature-specific requirements.

