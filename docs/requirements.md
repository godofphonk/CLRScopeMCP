# CLRScope MCP - Full Requirements

This document provides comprehensive requirements for using all CLRScope MCP features.

## System Requirements

### Operating System
- **Linux**: Ubuntu 20.04+, Debian 11+, RHEL 8+, or equivalent
- **macOS**: macOS 11+ (Big Sur) or later
- **Windows**: Windows 10/11 or Windows Server 2019+

### .NET Runtime
- **Required**: .NET 10.0 Runtime or higher

### .NET SDK (for CLI tools)
- **Required**: .NET SDK 10.0 or higher
- **Install**: [Download from Microsoft](https://dotnet.microsoft.com/download)

## CLI Tools by Feature

### Memory Dump Collection

#### dotnet-dump
- **Purpose**: Collect memory dumps from .NET processes
- **Used by**: `collect_dump`, `analyze_dump_sos`
- **Install**: `dotnet tool install -g dotnet-dump`
- **Required for**: Memory dump collection and SOS analysis
- **Alternatives**: 
  - Linux: `gcore` (part of gdb)
  - Windows: Procdump

#### dotnet-symbol
- **Purpose**: Download and cache symbols for detailed analysis
- **Used by**: `resolve_symbols`, `analyze_dump_sos`
- **Install**: 
  ```bash
  dotnet tool install -g dotnet-symbol
  dotnet-symbol set-symbol-server https://msdl.microsoft.com/download/symbols
  ```
- **Required for**: Accurate stack traces with function names and line numbers

### GC Heap Analysis

#### dotnet-gcdump
- **Purpose**: Collect GC heap snapshots (lighter than full dumps)
- **Used by**: `collect_gcdump`
- **Install**: `dotnet tool install -g dotnet-gcdump`
- **Required for**: Fast GC heap analysis without full memory dump

### Thread Analysis

#### dotnet-stack
- **Purpose**: Collect managed thread stacks
- **Used by**: `collect_stacks`
- **Install**: `dotnet tool install -g dotnet-stack`
- **Required for**: Thread deadlock detection and stack analysis

### Performance Monitoring

#### dotnet-counters
- **Purpose**: Collect performance counters in real-time
- **Used by**: `collect_counters`
- **Install**: `dotnet tool install -g dotnet-counters`
- **Required for**: CPU, memory, GC, and thread pool metrics
- **Supported Providers**:
  - `System.Runtime` - CPU, memory, GC, thread pool
  - `Microsoft.AspNetCore.Hosting` - HTTP metrics
  - `System.Net.Http` - HTTP client metrics
  - `System.Net.NameResolution` - DNS metrics
  - `System.Net.Security` - TLS metrics
  - `System.Net.Sockets` - socket metrics
  - `Microsoft.AspNetCore.Kestrel` - Kestrel server metrics
  - `Microsoft.AspNetCore.Routing` - routing metrics
  - `Microsoft.AspNetCore.RateLimiting` - rate limiting metrics

### System Tools (Platform-Specific)

#### Linux
- **pkill**: Required for process tree cleanup (usually pre-installed)
- **gcore**: Alternative to dotnet-dump for memory dumps (part of gdb)

#### macOS
- **gcore**: Alternative to dotnet-dump for memory dumps (part of gdb)

#### Windows
- **taskkill**: Required for process tree cleanup (pre-installed)
- **Procdump**: Alternative to dotnet-dump for memory dumps

## Feature-Specific Requirements

### All Features
- **Minimum**: .NET SDK 10.0+

### Memory Dump Analysis
- **Required**: dotnet-dump
- **Recommended**: dotnet-symbol (for symbol resolution)

### Thread Analysis
- **Required**: dotnet-stack

### Performance Monitoring
- **Required**: dotnet-counters

### GC Heap Analysis
- **Required**: dotnet-gcdump
- **v1.2.0 Heap Visualization**: Process-based parsing via ClrScope.HeapParser with 5-minute timeout for reliability

### Pattern Detection (Memory Leaks, Deadlocks, High CPU)
- **Required**: dotnet-dump or dotnet-gcdump
- **Recommended**: dotnet-stack, dotnet-symbol

### Baseline Comparison
- **Required**: Any collection tool (dump, gcdump, stacks, counters)

### Artifact Management
- **No additional tools required** (built-in SQLite storage)

## Installation Summary

### Minimal Setup (Basic Runtime Detection)
```bash
# Install .NET SDK
# Then install CLRScope MCP
dotnet tool install --global ClrScope.Mcp
```

### Recommended Setup (Full Diagnostics)
```bash
# Install .NET SDK 10.0+

# Install all diagnostic tools
dotnet tool install -g dotnet-dump
dotnet tool install -g dotnet-gcdump
dotnet tool install -g dotnet-stack
dotnet tool install -g dotnet-counters
dotnet tool install -g dotnet-symbol

# Configure symbol server
dotnet-symbol set-symbol-server https://msdl.microsoft.com/download/symbols

# Install CLRScope MCP
dotnet tool install --global ClrScope.Mcp
```

## Platform-Specific Notes

### Linux
- Some distributions may require additional packages:
  ```bash
  # Ubuntu/Debian
  sudo apt-get install gdb

  # RHEL/CentOS
  sudo yum install gdb
  ```

### macOS
- Install Xcode Command Line Tools for gdb:
  ```bash
  xcode-select --install
  ```

### Windows
- No additional system packages required
- All CLI tools work out of the box with .NET SDK

## Verification

After installation, verify tools are available:

```bash
dotnet-dump --version
dotnet-gcdump --version
dotnet-stack --version
dotnet-counters --version
dotnet-symbol --version
```

Run CLRScope MCP health check:

```bash
clrscope-mcp --demo
```

## Troubleshooting

### Tool Not Found
- Ensure .NET SDK is in PATH
- Run `dotnet tool list -g` to verify global tools are installed
- Try `export PATH="$PATH:$HOME/.dotnet/tools"` (Linux/macOS)

### Permission Denied
- Some tools may require elevated permissions for attaching to processes
- Run with `sudo` on Linux/macOS if needed (not recommended for production)

### Symbol Download Fails
- Ensure network connectivity to Microsoft Symbol Server
- Check firewall/proxy settings
- Use local symbol server if available

## Additional Resources

- [.NET Diagnostics Tools](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/)
- [dotnet-dump Documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump)
- [dotnet-symbol Documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-symbol)
