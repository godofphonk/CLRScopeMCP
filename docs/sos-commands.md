# SOS Commands Reference

SOS (Son of Strike) is a powerful debugging extension for .NET that provides deep runtime inspection capabilities. CLRScope MCP exposes SOS commands through the `analyze_dump_sos` tool.

## Overview

SOS commands are used for deep analysis of memory dumps (`analyze_dump_sos`). They provide access to:

- Thread states and stack traces
- Managed heap inspection
- Exception information
- GC heap details
- Assembly and module information
- Performance counters

## Prerequisites

**Required:** `dotnet-dump` must be installed
```bash
dotnet tool install -g dotnet-dump
```

**Required:** `dotnet-symbol` for symbol resolution (optional but recommended)
```bash
dotnet tool install -g dotnet-symbol
dotnet-symbol set-symbol-server https://msdl.microsoft.com/download/symbols
```

## Common SOS Commands

### Thread Analysis

| Command | Purpose | Use Case |
|---------|---------|----------|
| `threads` | List all managed threads with their state | Identify hung/blocked threads, thread count |
| `clrstack` | Show managed stack trace for current thread | See what code is executing on each thread |
| `threadstate` | Show thread state details | Understand thread blocking/waiting states |

**Example:**
```bash
mcp1_analyze_dump_sos --artifactId <dump_id> --command threads
mcp1_analyze_dump_sos --artifactId <dump_id> --command clrstack
```

### Heap Analysis

| Command | Purpose | Use Case |
|---------|---------|----------|
| `dumpheap -stat` | Show heap statistics by type | Identify large object types, memory usage |
| `dumpheap -mt <MethodTable> -min <size>` | Dump objects of specific type | Inspect specific object instances |
| `gcroot <address>` | Find what keeps an object alive | Trace object retention chains |
| `objsize <address>` | Show object size including retained size | Understand memory impact of specific objects |

**Example:**
```bash
mcp1_analyze_dump_sos --artifactId <dump_id> --command "dumpheap -stat"
mcp1_analyze_dump_sos --artifactId <dump_id> --command "gcroot 0x0000023456789010"
```

### Exception Analysis

| Command | Purpose | Use Case |
|---------|---------|----------|
| `pe` | Print last exception on current thread | Debug unhandled exceptions |
| `threads -special` | Show threads with special states | Find threads with exceptions |
| `dumpstackobjects` | Show objects on stack | Identify stack-allocated objects |

**Example:**
```bash
mcp1_analyze_dump_sos --artifactId <dump_id> --command pe
mcp1_analyze_dump_sos --artifactId <dump_id> --command dumpstackobjects
```

### Assembly/Module Analysis

| Command | Purpose | Use Case |
|---------|---------|----------|
| `clrmodules` | List loaded CLR modules | Verify assembly loading, missing dependencies |
| `dumpassembly` | Show assembly details | Debug assembly loading issues |
| `dumpdomain` | Show application domains | Understand app domain structure |

**Example:**
```bash
mcp1_analyze_dump_sos --artifactId <dump_id> --command clrmodules
mcp1_analyze_dump_sos --artifactId <dump_id> --command dumpassembly
```

## When to Use SOS Commands

### Use SOS When:
- You need detailed thread stack information beyond `collect_stacks`
- You're investigating complex memory leaks at object level
- You need to trace exception propagation
- You're debugging assembly loading issues
- You need to understand GC heap structure at low level

### Use Alternatives When:
- Quick thread overview needed → `collect_stacks` (faster, simpler)
- High-level memory analysis needed → `analyze_heap` with gcdump (faster, dominator tree)
- Automated pattern detection needed → `detect_patterns`

## Best Practices

1. **Start with `threads`** - Get overview of all thread states first
2. **Use `clrstack` selectively** - Pick specific threads from `threads` output
3. **Combine with heap analysis** - Use `analyze_heap` for type stats, then SOS for object-level details
4. **Resolve symbols first** - Run `symbols_resolve` before SOS for better stack traces
5. **Focus on relevant threads** - Don't analyze all threads, prioritize blocked/waiting ones

## Common Workflows

### Hang/Deadlock Investigation
```bash
# 1. List threads to identify blocked ones
mcp1_analyze_dump_sos --artifactId <dump_id> --command threads

# 2. Get stack traces for blocked threads
mcp1_analyze_dump_sos --artifactId <dump_id> --command clrstack

# 3. Look for locks and wait states
mcp1_analyze_dump_sos --artifactId <dump_id> --command syncblk
```

### Memory Leak Investigation (Object Level)
```bash
# 1. Get heap statistics
mcp1_analyze_dump_sos --artifactId <dump_id> --command "dumpheap -stat"

# 2. Find suspect type and get its MethodTable
# (output from previous command)

# 3. Dump objects of that type
mcp1_analyze_dump_sos --artifactId <dump_id> --command "dumpheap -mt <MethodTable> -min 1000"

# 4. Trace retention for specific object
mcp1_analyze_dump_sos --artifactId <dump_id> --command "gcroot <address>"
```

### Exception Investigation
```bash
# 1. Find threads with exceptions
mcp1_analyze_dump_sos --artifactId <dump_id> --command "threads -special"

# 2. Print exception details
mcp1_analyze_dump_sos --artifactId <dump_id> --command pe

# 3. Get stack trace for exception thread
mcp1_analyze_dump_sos --artifactId <dump_id> --command clrstack
```

## Limitations

- SOS commands only work with dump artifacts, not gcdump or trace artifacts
- Requires dotnet-dump to be installed
- Some commands may fail if symbols are not available
- Complex commands may take time to execute on large dumps
- Not all SOS commands are exposed through `analyze_dump_sos` (only most common ones)

## Troubleshooting

### SOS Command Fails
- Verify dotnet-dump is installed
- Check dump file integrity
- Try simpler command first
- Ensure artifact is a valid dump (not gcdump)

### No Symbol Information
- Run `symbols_resolve` before SOS commands
- Check symbol server configuration
- Verify symbols are available for target assemblies

### Command Takes Too Long
- Large dumps (>2GB) may be slow
- Use simpler commands first (e.g., `threads` instead of complex heap analysis)
- Consider using gcdump for memory analysis instead
