# CLRScope MCP

![Version](https://img.shields.io/github/v/release/godofphonk/CLRScopeMCP)
![Badge](https://img.shields.io/badge/.NET-10.0%2B-purple)
![Badge](https://img.shields.io/badge/license-MIT-green)

> MCP server for comprehensive .NET application diagnostics

## Overview

CLRScope MCP provides AI-powered diagnostic capabilities for .NET applications through the Model Context Protocol. It enables LLM agents to perform deep analysis of .NET processes including performance profiling, memory leak detection, thread analysis, and automated pattern detection.

**Highlights:**
- Heap analysis with dominator tree (Cooper-Harvey-Kennedy algorithm) for accurate retained size calculation
- Retainer path analysis: trace object retention chains from GC roots to any target node
- Type statistics (top N types) and diff comparison between gcdumps
- Process-based heap parsing via ClrScope.HeapParser with 5-minute timeout
- Import existing .gcdump and .nettrace files for analysis (heap analysis supports .gcdump only)
- Automated workflow bundles for common diagnostic scenarios

**1.3.0 changes:**
- Removed unreliable EventPipe/.nettrace heap-analysis path (adapters, facades, preflight). `.nettrace` is now used exclusively for CPU/trace analysis; heap analysis requires `.gcdump`.
- Fixed ReverseBFS bugs in `DominatorTreeCalculator` (correct retained-size computation for deep graphs).
- Added heap-analysis benchmarks and `MemoryPressureApp` under `test-data/` for realistic load scenarios.

## Quick Start for AI Agents

Once CLRScope MCP is configured in your IDE, just describe the problem in natural language:

| Scenario | Example Prompt |
|----------|---------------|
| **High CPU** | "My .NET app has 100% CPU. PID 12345. Find the cause." |
| **Memory Leak** | "App memory keeps growing. PID 12345. Investigate." |
| **Hang/Deadlock** | "App is frozen, not responding. PID 12345. Check for deadlock." |
| **Baseline** | "Collect baseline performance data for PID 12345." |
| **Compare** | "Compare current performance with previous baseline session." |

The AI agent will automatically select and execute the appropriate CLRScope tools.

## Features

| Feature | Description |
|---------|-------------|
| 🎯 **Runtime Detection** | Discover and inspect attachable .NET processes |
| 📊 **Performance Counters** | Real-time CPU, memory, GC, and thread pool metrics |
| 🧠 **Heap Analysis** | Dominator tree, retained size, type statistics, diff comparison, and retainer path tracing |
| 📁 **Artifact Import** | Import existing .gcdump and .nettrace files for offline analysis |
| 💾 **Memory Dump Analysis** | Full memory dump collection with optional compression |
| 🔍 **SOS Commands** | Sequential SOS command execution for deep .NET runtime analysis |
| 🧩 **Pattern Detection** | Automatic detection of memory leaks, deadlocks, thread pool starvation, high CPU |
| 📦 **Artifact Management** | Pagination, filtering, pinning, and cleanup strategies |
| 📈 **Baseline Comparison** | Compare diagnostic sessions with baseline for deviation analysis |
| 🤖 **Automated Workflows** | One-command diagnostic bundles for high CPU, memory leaks, hangs, and baseline |

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
dnx ClrScope.Mcp@1.3.0 --yes
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
      "args": ["ClrScope.Mcp@1.3.0", "--yes"]
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

## Available Tools

### Collection
| Tool | Description |
|------|-------------|
| `collect_trace` | EventPipe trace (CPU sampling, GC heap, custom providers) |
| `collect_dump` | Full memory dump with optional heap and compression |
| `collect_gcdump` | GC heap snapshot (lightweight alternative to full dump) |
| `collect_stacks` | Managed thread stacks (text or JSON) |
| `collect_counters` | Performance counters (System.Runtime, ASP.NET, etc.) |
| `import_gcdump` | Import existing .gcdump file |
| `import_trace` | Import existing .nettrace file |

### Analysis
| Tool | Description |
|------|-------------|
| `artifact_summarize` | Automated summary with key metrics and recommendations |
| `detect_patterns` | Pattern detection: memory leaks, deadlocks, thread pool starvation, high CPU |
| `analyze_dump_sos` | SOS commands on dump files (threads, clrstack, dumpheap, etc.) |
| `symbols_resolve` | Load symbols via dotnet-symbol |
| `analyze_heap` | Analyze .gcdump heap snapshot: type statistics (top N types), object list (with node IDs for find_retainer_paths), or diff comparison between two snapshots |
| `find_retainer_paths` | Trace object retention chains from GC roots to a target node (use analyze_heap with `analysisType: objects` to get node IDs) |
| `session_analyze` | Session analysis with optional baseline comparison |

### Runtime
| Tool | Description |
|------|-------------|
| `runtime_list_targets` | List all attachable .NET processes |
| `runtime_inspect_target` | Detailed info about a specific .NET process |

### Automated Workflows
| Tool | Description |
|------|-------------|
| `workflow_automated_high_cpu_bundle` | trace + counters + stacks |
| `workflow_automated_memory_leak_bundle` | gcdump + counters + trace (gc-heap) |
| `workflow_automated_hang_bundle` | dump + stacks + counters |
| `workflow_automated_baseline_bundle` | counters + trace + gcdump + stacks |

### Management
| Tool | Description |
|------|-------------|
| `artifact_list` | List artifacts with filtering and pagination |
| `artifact_get_metadata` | Get artifact metadata by ID |
| `artifact_read_text` | Read text content of an artifact |
| `artifact_pin` / `artifact_unpin` | Protect artifacts from automatic cleanup |
| `artifact_cleanup` | Clean old artifacts (by age or duplicates) |
| `artifact_delete` | Delete specific artifact |
| `session_get` | Get session info |
| `session_cancel` | Cancel active session |
| `session_group_by_incident` | Group sessions by incident ID |
| `system_health` | Server health check |
| `system_capabilities` | Available capabilities and feature flags |

## Documentation

- [Investigation Guides](docs/investigation-guides.md) - Step-by-step diagnostic workflows for common scenarios
- [Best Practices](docs/best-practices.md) - Collection parameters, analysis techniques, and optimization tips
- [Troubleshooting](docs/troubleshooting.md) - Solutions to common issues and error messages
- [SOS Commands](docs/sos-commands.md) - Reference for SOS debugging commands
- [Tool Integration](docs/integration.md) - Required CLI tools and installation instructions
- [Full Requirements](docs/requirements.md) - System and platform requirements
