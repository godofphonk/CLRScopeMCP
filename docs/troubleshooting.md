# Troubleshooting

This document provides solutions to common issues when using CLRScope MCP.

## Installation Issues

### Tool Not Found After Installation

**Problem:** `clrscope-mcp` command not found after installation.

**Solutions:**
- Verify installation: `dotnet tool list -g`
- Add to PATH: `export PATH="$PATH:$HOME/.dotnet/tools"` (Linux/macOS)
- Restart terminal to apply PATH changes
- Check .NET SDK is installed: `dotnet --version`

### Permission Denied

**Problem:** Permission denied when running CLRScope MCP or diagnostic tools.

**Solutions:**
- Check file permissions: `ls -la clrscope-mcp` or `ls -la ~/.dotnet/tools/`
- Make executable: `chmod +x clrscope-mcp`
- For system-wide installation, may require elevated permissions (not recommended for production)
- Check directory permissions for artifact storage

### .NET Runtime Not Found

**Problem:** CLRScope MCP fails to start with .NET runtime error.

**Solutions:**
- Install .NET 10.0 runtime: https://dotnet.microsoft.com/download/dotnet/10.0
- Verify installation: `dotnet --version`
- Ensure PATH includes .NET runtime
- For Linux: `sudo apt-get install dotnet-runtime-10.0`

## Process Detection Issues

### No .NET Processes Found

**Problem:** `runtime_list_targets` returns empty list.

**Solutions:**
- Verify .NET processes are running: `ps aux | grep dotnet`
- Check CLRScope MCP has process permissions
- Try with elevated permissions if needed
- Verify .NET processes are attachable (not already being debugged)

### Process Not Attachable

**Problem:** `runtime_inspect_target` returns process not attachable.

**Solutions:**
- Process may be in suspended state
- Another debugger may be attached
- Process may have already crashed
- Try collecting data before process termination

## Artifact Collection Issues

### Memory Dump Collection Fails

**Problem:** `collect_dump` fails with error.

**Solutions:**
- Verify dotnet-dump is installed: `dotnet-dump --version`
- Check process permissions: may require elevated privileges
- Ensure sufficient disk space for dump file
- Try alternative: `gcore <pid>` (Linux) or Procdump (Windows)
- Check if process is in zombie state

### GC Heap Snapshot Fails

**Problem:** `collect_gcdump` fails with error.

**Solutions:**
- Verify dotnet-gcdump is installed: `dotnet-gcdump --version`
- Process may not support gcdump (older .NET versions)
- Try full memory dump instead
- Check process is not already being analyzed

### Thread Stacks Collection Fails

**Problem:** `collect_stacks` returns empty or fails.

**Solutions:**
- Verify dotnet-stack is installed: `dotnet-stack --version`
- Process may have no managed threads
- Check process is still running
- Try collecting during active work period

### Performance Counters Collection Fails

**Problem:** `collect_counters` returns no data.

**Solutions:**
- Verify dotnet-counters is installed: `dotnet-counters --version`
- Check provider name is correct (e.g., System.Runtime)
- Process may not emit counters
- Try different provider: `Microsoft.AspNetCore.Hosting` for web apps

### CPU Trace Collection Fails

**Problem:** `collect_trace` fails or produces empty file.

**Solutions:**
- Verify correct profile: use `cpu-sampling` or default
- Check duration is sufficient (minimum 10 seconds)
- Process may have low CPU activity
- Try longer duration: 30-60 seconds

## Analysis Issues

### Artifact Not Found

**Problem:** `artifact_summarize` or other tools report artifact not found.

**Solutions:**
- Verify artifact ID is correct
- Check artifact store: `mcp1_artifact_list`
- Artifact may have been cleaned up
- Re-collect the artifact if needed

### SOS Commands Fail

**Problem:** `analyze_dump_sos` fails with SOS error.

**Solutions:**
- Verify dotnet-dump is installed and available
- Dump file may be corrupted
- Try different SOS command
- Check if dump file is compressed (auto-decompression should handle this)

### Pattern Detection Finds Nothing

**Problem:** `detect_patterns` returns no patterns.

**Solutions:**
- Artifact may not contain pattern data
- Try different focus: memory, threads, cpu, io
- Use `artifact_summarize` for general analysis
- Patterns may not be present in collected data

### Flame Graph Generation Fails

**Problem:** `visualize_flame_graph` fails with error.

**Solutions:**
- For Dump artifacts: ensure dotnet-dump is available (v1.2.0)
- For Trace artifacts: verify trace contains CPU sampling data
- For Stacks artifacts: check JSON format is valid
- Try with `auto_analyze=true` for Dump/Trace artifacts
- Check artifact file exists and is readable

### Flame Graph Shows No Data

**Problem:** Flame graph renders but shows empty or placeholder.

**Solutions:**
- Artifact may not contain stack data
- Check artifact type: Dump/Trace require preprocessing (v1.2.0)
- Try with `analysis_mode=force` to re-analyze
- Verify artifact was collected successfully
- Check log for parsing errors

### Heap Visualization Fails (v1.2.0)

**Problem:** `visualize_heap_snapshot` or `visualize_nettrace_heap` fails with error.

**Solutions:**
- For .gcdump: Verify file is valid gcdump format
- For .nettrace: Check preflight validation results for partial/incomplete data
- .nettrace heap snapshots are unreliable even with correct keywords
- Use .gcdump files for heap visualization instead of .nettrace
- Check process-based parsing timeout (5 minutes for reliability)
- Verify ClrScope.HeapParser.dll is in correct location

### Heap Visualization Shows Partial Data (v1.2.0)

**Problem:** Heap visualization returns error about partial heap data.

**Solutions:**
- For .nettrace: Preflight validation detected incomplete heap snapshot events
- Use dotnet-gcdump collect instead of dotnet-trace collect for heap snapshots
- For .nettrace with correct keywords (0x1980001): May still be partial due to EventPipe limitations
- Use .gcdump files for reliable heap visualization
- .nettrace suitable for CPU flame graphs, performance counters, trace analysis

## Caching Issues

### Cache Not Working

**Problem**: Flame graph always re-analyzes instead of using cache.

**Solutions:**
- Check `analysis_mode` parameter: use `auto` or `reuse`
- Verify cache key hasn't changed (artifact hash, kind, version)
- Cache may have been cleared by cleanup
- Try `analysis_mode=force` to test re-analysis

### Cache Miss Errors

**Problem:** `analysis_mode=reuse` fails with cache miss error.

**Solutions:**
- Use `analysis_mode=auto` to analyze and cache
- Verify artifact exists and is valid
- Check if artifact was modified (hash changed)
- Re-run with `analysis_mode=force` to populate cache

## Session Issues

### Session Not Found

**Problem:** `session_get` or `session_analyze` reports session not found.

**Solutions:**
- Verify session ID is correct
- Session may have been cleaned up
- Check session list: `mcp1_session_list` (if available)
- Re-collect data to create new session

### Session Analysis Fails

**Problem:** `session_analyze` fails with error.

**Solutions:**
- Verify both session IDs are valid
- Baseline session may not exist
- Sessions may be incompatible (different artifact types)
- Use `artifact_summarize` on individual artifacts instead

## Performance Issues

### Slow Analysis

**Problem:** Analysis takes very long time.

**Solutions:**
- Large dump files (>2GB) take time to analyze
- Use caching: `analysis_mode=reuse` for subsequent analyses
- Consider using gcdump instead of full dump for memory issues
- Reduce collection duration for traces

### High Memory Usage

**Problem:** CLRScope MCP uses excessive memory.

**Solutions:**
- Large artifacts in memory: clean up old artifacts
- Use `artifact_cleanup` to remove old data
- Reduce collection frequency
- Process artifacts individually instead of in bulk

### Disk Space Issues

**Problem:** Artifact storage consuming too much disk space.

**Solutions:**
- Clean up old artifacts: `mcp1_artifact_cleanup --strategy age --maxAge 7d`
- Remove duplicates: `mcp1_artifact_cleanup --strategy duplicates`
- Configure artifact root to larger disk
- Enable dump compression to save space

## Configuration Issues

### Artifact Root Path Issues

**Problem:** Path validation errors for artifact storage.

**Solutions:**
- Verify artifact root exists and is writable
- Check configuration in appsettings.json
- Ensure path is absolute, not relative
- Check file system permissions

### MCP Server Connection Issues

**Problem:** IDE cannot connect to CLRScope MCP server.

**Solutions:**
- Verify MCP configuration in IDE settings
- Check command path is correct
- Ensure CLRScope MCP is installed and in PATH
- Try running manually: `clrscope-mcp --demo`
- Check IDE logs for connection errors

## Symbol Resolution Issues

### Symbols Not Loading

**Problem:** Stack traces show function addresses instead of names.

**Solutions:**
- Configure symbol server: `dotnet-symbol set-symbol-server https://msdl.microsoft.com/download/symbols`
- Verify network connectivity to symbol server
- Use `resolve_symbols` tool for specific artifact
- Check firewall/proxy settings
- Symbols may not be available for custom builds

### Symbol Download Slow

**Problem:** Symbol resolution takes very long time.

**Solutions:**
- First download is slow, subsequent uses cache
- Use local symbol server if available
- Disable symbol resolution if not needed
- Pre-download symbols for common libraries

## Platform-Specific Issues

### Linux

#### Permission Denied for Process Attachment

**Problem:** Cannot attach to process due to permissions.

**Solutions:**
- Check ptrace scope: `cat /proc/sys/kernel/yama/ptrace_scope`
- Temporarily disable: `echo 0 | sudo tee /proc/sys/kernel/kernel/yama/ptrace_scope`
- Run CLRScope MCP with appropriate user permissions
- Use container with proper capabilities

#### gcore Not Found

**Problem:** Alternative dump collection fails.

**Solutions:**
- Install gdb: `sudo apt-get install gdb` (Ubuntu/Debian)
- Install gdb: `sudo yum install gdb` (RHEL/CentOS)
- Use dotnet-dump instead

### macOS

#### Xcode Command Line Tools Missing

**Problem:** System tools not available.

**Solutions:**
- Install Xcode Command Line Tools: `xcode-select --install`
- Accept license agreement if prompted
- Restart terminal after installation

### Windows

#### Taskkill Not Found

**Problem:** Process cleanup fails.

**Solutions:**
- Taskkill should be pre-installed on Windows
- Verify Windows version supports required features
- Check PATH includes System32 directory

#### Procdump Not Found

**Problem:** Alternative dump collection fails.

**Solutions:**
- Download Procdump from Microsoft
- Add to PATH or use full path
- Use dotnet-dump instead

## Getting Help

### Check Logs

Review CLRScope MCP logs for detailed error information:
- Logs location depends on configuration
- Check IDE MCP server logs
- Enable verbose logging if needed

### Verify Installation

Run health check:
```bash
clrscope-mcp --demo
```

### System Capabilities

Check system health and capabilities:
```bash
mcp1_system_health
mcp1_system_capabilities
```

### Documentation

- [Investigation Guides](investigation-guides.md) - Step-by-step guides
- [Best Practices](best-practices.md) - Recommended workflows
- [Requirements](requirements.md) - System requirements
- [Integration](integration.md) - Tool setup

## Common Error Messages

### "Artifact not found"

- Verify artifact ID
- Check artifact list
- Re-collect if needed

### "Process not attachable"

- Check process state
- Verify permissions
- Try different process

### "SOS command failed"

- Verify dotnet-dump installation
- Check dump file integrity
- Try alternative command

### "No stack data found"

- Verify artifact type
- Check collection succeeded
- Try with auto_analyze=true

### "Cache miss"

- Use analysis_mode=auto
- Verify artifact exists
- Re-run with analysis_mode=force

## Conclusion

Most CLRScope MCP issues can be resolved by:
1. Verifying tool installation and PATH configuration
2. Checking process permissions and state
3. Using appropriate artifact types for the investigation
4. Following best practices for collection and analysis
5. Consulting logs for detailed error information

If issues persist, check system health and capabilities, then consult documentation or seek help.
