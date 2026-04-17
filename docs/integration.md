# Tool Integration

CLRScope MCP integrates with .NET CLI tools for diagnostics. Some features require additional tools.

## Required Dependencies

### .NET SDK

**Purpose:** Install and run all CLI tools

**Version:** .NET SDK 10.0 or higher

**Install:**
```bash
# Linux
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0

# macOS
brew install dotnet

# Windows
# Download from https://dotnet.microsoft.com/download
```

## CLI Tools by Function

### dotnet-dump

**Purpose:** Collect memory dumps and perform SOS analysis

**Used in:** `collect_dump`, `analyze_dump_sos`

**Install:**
```bash
dotnet tool install -g dotnet-dump
```

**Alternatives:** gcore (Linux), Procdump (Windows)

**Without this tool:** Memory dumps not collected via dotnet-dump (use OS tools instead), SOS analysis not available

---

### dotnet-gcdump

**Purpose:** Collect GC heap snapshots

**Used in:** `collect_gcdump`, `analyze_heap`, `find_retainer_paths`, `import_gcdump`

**Install:**
```bash
dotnet tool install -g dotnet-gcdump
```

**Without this tool:** GC heap snapshots not collected (use full memory dump instead), heap analysis not available

**Note:** Heap analysis is supported only for `.gcdump` artifacts. The EventPipe/`.nettrace` heap-analysis path was removed in 1.3.0 due to persistent issues in the vendored `EventPipeDotNetHeapDumper`. `.nettrace` files remain useful for CPU sampling and trace analysis. Heap analysis includes dominator tree calculation (Cooper-Harvey-Kennedy algorithm) for retained size and retainer path tracing.

---

### dotnet-stack

**Purpose:** Collect managed thread stacks

**Used in:** `collect_stacks`

**Install:**
```bash
dotnet tool install -g dotnet-stack
```

**Without this tool:** Thread stacks not collected

---

### dotnet-counters

**Purpose:** Collect performance counters

**Used in:** `collect_counters`

**Providers:**
- `System.Runtime` - CPU, memory, GC, thread pool
- `Microsoft.AspNetCore.Hosting` - HTTP metrics
- `System.Net.Http` - HTTP client metrics

**Install:**
```bash
dotnet tool install -g dotnet-counters
```

**Without this tool:** Performance counters not collected

---

### dotnet-symbol

**Purpose:** Load symbols for detailed analysis

**Used in:** `resolve_symbols`

**Install:**
```bash
dotnet tool install -g dotnet-symbol
dotnet-symbol set-symbol-server https://msdl.microsoft.com/download/symbols
```

**Without this tool:** Symbols not loaded automatically

## Minimal Configuration

**Required:**
- .NET SDK 10.0+
- dotnet-dump (or gcore on Linux)

**Recommended:**
- dotnet-gcdump (heap analysis with dominator tree and retainer paths)
- dotnet-stack (thread analysis)
- dotnet-counters (performance monitoring)

**Optional:**
- dotnet-symbol (improved stack traces)

## .NET Version Compatibility

| Tool          | .NET 6 | .NET 7 | .NET 8 | .NET 9 | .NET 10 |
|---------------|--------|--------|--------|--------|---------|
| dotnet-dump    | ✅     | ✅     | ✅     | ✅     | ✅      |
| dotnet-gcdump  | ✅     | ✅     | ✅     | ✅     | ✅      |
| dotnet-stack   | ✅     | ✅     | ✅     | ✅     | ✅      |
