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

### NuGet Global Tool
```bash
dotnet tool install --global ClrScope.Mcp
```

### Binary Downloads

Download pre-built binaries from the [GitHub Releases](https://github.com/godofphonk/CLRScopeMCP/releases/latest) page:

- **Linux x64:** `clrscope-mcp-linux-x64.tar.gz`
- **Linux ARM64:** `clrscope-mcp-linux-arm64.tar.gz`
- **macOS ARM64:** `clrscope-mcp-osx-arm64.tar.gz`
- **Windows x64:** `clrscope-mcp-win-x64.zip`

Extract and run:
```bash
# Linux/macOS
tar xzf clrscope-mcp-linux-x64.tar.gz
chmod +x ClrScope.Mcp
./ClrScope.Mcp --version

# Windows
unzip clrscope-mcp-win-x64.zip
.\ClrScope.Mcp.exe --version
```

### IDE Configuration (VS Code, Visual Studio, etc.)

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

## Documentation

- [Investigation Guides](docs/investigation-guides.md) - Step-by-step guides for memory leaks, hangs, high CPU, and baseline performance collection
- [Tool Integration](docs/integration.md) - Required CLI tools and installation instructions for diagnostic features
