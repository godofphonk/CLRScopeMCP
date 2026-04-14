# CLRScope MCP

![Badge](https://img.shields.io/badge/version-v0.1.0-blue)
![Badge](https://img.shields.io/badge/.NET-10.0-purple)
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
- **Memory Dump Analysis** | Compressed dump support with automatic decompression
- **SOS Commands** | Sequential SOS command execution for deep analysis
- **Pattern Detection** | Automatic detection of memory leaks, deadlocks, high CPU
- **Artifact Management** | Pagination, filtering, and cleanup strategies
- **Baseline Comparison** | Compare diagnostic sessions with baseline

## Installation

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