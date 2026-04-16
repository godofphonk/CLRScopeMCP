# Best Practices

This document provides best practices for using CLRScope MCP effectively in diagnostic scenarios.

## General Guidelines

### 1. Start with Automated Workflows

For common diagnostic scenarios, use automated workflows instead of manual steps:

- **High CPU**: `workflow_automated_high_cpu_bundle`
- **Memory Leak**: `workflow_automated_memory_leak_bundle`
- **Hang/Deadlock**: `workflow_automated_hang_bundle`
- **Baseline**: `workflow_automated_baseline_bundle`

Automated workflows collect all necessary artifacts in the correct sequence, reducing the chance of missing critical data.

### 2. Collect Baseline Data

Before investigating issues, collect baseline performance data from a healthy system state:

```bash
# Collect baseline
mcp1_workflow_automated_baseline_bundle --pid <healthy_pid> --duration 00:01:00

# Save with descriptive name for comparison
```

Baseline data provides context for comparison when investigating issues.

### 3. Use Appropriate Collection Duration

- **CPU Profiling**: 30-60 seconds for representative data
- **Memory Leak**: 5-10 minutes to capture allocation patterns
- **Performance Counters**: 60 seconds for stable metrics
- **Baseline**: 60 seconds for consistent comparison

Longer durations may not provide additional value and can produce large files.

### 4. Verify Process State Before Collection

Always verify the target process is in the expected state:

```bash
# Check process is .NET
mcp1_runtime_inspect_target --pid <pid>

# List all .NET processes
mcp1_runtime_list_targets
```

This prevents wasting time on non-.NET processes or processes that have already crashed.

## Collection Parameter Guidelines

### Trace Collection (collect_trace)

**Duration:**
- **CPU Profiling**: 30-60 seconds for representative data
- **Memory Allocation**: 60-120 seconds to capture allocation patterns
- **Baseline**: 60 seconds for consistent comparison
- **Avoid**: Durations > 5 minutes (produces large files with diminishing returns)

**Profile Selection:**
- **cpu-sampling**: Use for CPU investigation, hot path identification
- **gc-heap**: Use for memory allocation analysis, GC behavior
- **default**: Use for general runtime events, baseline collection
- **Custom providers**: Use when specific diagnostics needed (e.g., HTTP, SQL)

**Custom Providers (if needed):**
- Format: `ProviderName:Level:Keywords`
- Example: `Microsoft-Windows-DotNETRuntime:Informational:0x00000001`
- Use sparingly - more providers = larger files

### Dump Collection (collect_dump)

**Include Heap:**
- **true** (default): Include heap for memory leak investigation, object analysis
- **false**: Faster collection, smaller file for thread analysis only
- **Recommendation**: Use false for hang/deadlock investigation, true for memory leaks

**Compression:**
- **true**: Reduces file size significantly (2-5x compression ratio)
- **false** (default): Faster collection, larger file
- **Recommendation**: Use true for large processes (>1GB memory), false for small processes

**When to Use:**
- **Hang/Deadlock**: Collect dump immediately when process is hung
- **Memory Leak**: Use gcdump first, dump if gcdump insufficient
- **Crash Analysis**: Collect dump as soon as possible after crash
- **Avoid**: Frequent dump collection in production (high overhead)

### GC Heap Snapshot (collect_gcdump)

**When to Use:**
- **Memory Leak Investigation**: Faster than full dump, focuses on heap
- **Object Analysis**: Identify large objects, type distribution
- **GC Behavior**: Analyze generation distribution, collection patterns
- **Heap Analysis**: Advanced analysis with type statistics and diff comparison (JSON/text output only)

**When Not to Use:**
- **Thread Analysis**: Use stacks or dump instead
- **Native Memory**: Use full dump instead
- **Crash Analysis**: Use full dump instead

**Limitations:**
- Requires process to be in stable state
- May not capture all objects during collection
- Not suitable for real-time monitoring

**Best Practices (v1.2.0):**
- Use `analyze_heap` for type statistics with retained size (dominator tree analysis)
- Use `find_retainer_paths` to trace why specific objects are kept alive
- Process-based parsing via ClrScope.HeapParser with 5-minute timeout for reliability
- Compare baseline vs issue gcdumps using diff analysis to identify growing objects
- Use .gcdump files for heap analysis (reliable) vs .nettrace (unreliable for heap data)

### Thread Stacks (collect_stacks)

**Output Format:**
- **json** (recommended): Structured data, easier to parse programmatically
- **text**: Human-readable, compatible with dotnet-stack output
- **Recommendation**: Use json for automated analysis, text for manual inspection

**When to Use:**
- **Hang/Deadlock**: Capture blocking patterns, circular wait chains
- **Thread Pool Analysis**: Identify starvation, queue buildup
- **Async/Await Issues**: Detect deadlocks in async code

**Best Practices:**
- Collect during active issue state
- Use json format for programmatic analysis
- Combine with counters for thread pool context

### Performance Counters (collect_counters)

**Duration:**
- **CPU Profiling**: 30-60 seconds for representative data
- **Memory Analysis**: 60-120 seconds to capture GC patterns
- **Thread Pool**: 60 seconds to observe thread behavior
- **Baseline**: 60 seconds for consistent comparison

**Provider Selection:**
- **System.Runtime** (default): CPU, memory, GC, thread pool metrics
- **Microsoft.AspNetCore.Hosting**: HTTP metrics for web applications
- **System.Net.Http**: HTTP client metrics
- **System.Net.Security**: TLS metrics
- **Multiple providers**: Use comma-separated list for comprehensive monitoring

**Provider Selection Guide:**
- **General diagnostics**: System.Runtime only
- **Web applications**: System.Runtime + Microsoft.AspNetCore.Hosting
- **HTTP services**: System.Runtime + System.Net.Http
- **Comprehensive**: System.Runtime + Microsoft.AspNetCore.Hosting + System.Net.Http

**Avoid:**
- Too many providers (large files, overhead)
- Very long durations (>5 minutes)
- Collecting during high load (may impact performance)

### Artifact Import (import_gcdump, import_trace)

**When to Use:**
- **Existing Files**: Import previously collected .gcdump or .nettrace files for analysis
- **Offline Analysis**: Analyze artifacts without requiring live process
- **Cross-Session**: Compare artifacts collected at different times

**Best Practices (v1.2.0):**
- Use `import_gcdump` for heap analysis (reliable)
- Use `import_trace` for CPU profiling, performance counters, trace analysis
- Note: .nettrace heap snapshots are unreliable even with correct keywords
- For heap analysis, prefer .gcdump over .nettrace

## Artifact-Specific Best Practices

### Memory Dumps

- **Compress dumps**: Use compression to save disk space
- **Collect during issue**: Dump should capture the exact state when the problem occurs
- **Include SOS analysis**: Use `analyze_dump_sos` for immediate insights
- **Consider size**: Large dumps (>2GB) may take time to analyze

### GC Heap Snapshots

- **Use gcdump for leaks**: Faster than full dumps for memory leak investigation
- **Multiple snapshots**: Compare before/after to identify growing objects
- **Check generation distribution**: Large Gen2 indicates long-lived objects

### Thread Stacks

- **Capture during hang**: Stacks show blocking patterns
- **Review thread pool**: Check for starvation or queue buildup
- **Look for async/await**: Identify deadlocks in async code

### Performance Counters

- **Use appropriate providers**: System.Runtime for general metrics, others for specific features
- **Monitor trends**: Single snapshots may not show patterns
- **Correlate with traces**: Combine counters with trace data for context

### CPU Traces

- **Use correct profile**: `cpu-sampling` for CPU issues, `gc-heap` for memory
- **Sample rate**: Default rate is usually sufficient
- **Check hot paths**: Focus on methods with highest CPU time

## Analysis Best Practices

### 1. Start with artifact_summarize

Use `artifact_summarize` for automated analysis before manual investigation:

```bash
mcp1_artifact_summarize --artifact_id <id> --focus all
```

This provides high-level findings and recommendations.

### 2. Use Pattern Detection

Leverage automated pattern detection for common issues:

```bash
mcp1_detect_patterns --artifact_id <id>
```

Detects memory leaks, deadlocks, thread pool starvation, and high CPU patterns.

### 3. Compare with Baseline

Use session analysis to compare problematic state with baseline:

```bash
mcp1_session_analyze --session_id <problem_session> --baseline_session_id <baseline_session>
```

Identifies deviations from normal behavior.

### 4. Use Heap Analysis for Memory Analysis

For GcDump artifacts, use heap analysis to understand memory distribution. The dominator tree calculates accurate retained sizes (not just shallow sizes):

```bash
mcp1_analyze_heap --artifact_id <id> --analysis_type type_stats
```

Compare baseline vs issue snapshots with diff analysis:

```bash
mcp1_analyze_heap --artifact_id <issue_id> --analysis_type diff --baselineArtifactId <baseline_id>
```

### 5. Use Retainer Paths for Leak Root Cause

Once you identify a suspect type via `analyze_heap`, get the node IDs of specific objects:

```bash
# Get list of objects with their node IDs (sorted by shallow size)
mcp1_analyze_heap --artifact_id <id> --analysis_type objects --metric shallow_size --maxTypes 50

# Then trace retention chain for a specific object
mcp1_find_retainer_paths --artifact_id <id> --targetNodeId <node_id> --maxPaths 10
```

This shows why an object is kept alive by tracing paths from GC roots to the target.

## Performance Optimization

### Artifact Cleanup

Regularly clean up old artifacts to prevent disk space issues:

```bash
# Clean artifacts older than 7 days
mcp1_artifact_cleanup --strategy age --maxAge 7d

# Remove duplicates
mcp1_artifact_cleanup --strategy duplicates
```

### Artifact Pinning

Pin important artifacts to prevent automatic cleanup:

```bash
mcp1_artifact_pin --artifact_id <id>
```

## Security Best Practices

### Path Validation

CLRScope MCP validates artifact paths are within the configured artifact root. Ensure proper configuration:

```json
{
  "ArtifactRoot": "/path/to/secure/artifact/directory"
}
```

### Process Permissions

Diagnostic tools may require elevated permissions. Consider:

- Run CLRScope MCP with appropriate user permissions
- Avoid running as root unless necessary
- Use containerized environments for isolation

### Symbol Server

When using symbol resolution, configure symbol server securely:

```bash
dotnet-symbol set-symbol-server https://msdl.microsoft.com/download/symbols
```

## Common Pitfalls to Avoid

### 1. Collecting Too Much Data

- **Problem**: Excessive collection produces large files and long analysis times
- **Solution**: Use appropriate durations and targeted collection

### 2. Ignoring Baseline Data

- **Problem**: Cannot determine if metrics are abnormal without context
- **Solution**: Always collect baseline for comparison

### 3. Wrong Artifact Type

- **Problem**: Using wrong collection method for the issue type
- **Solution**: Match artifact type to investigation goal (dump for hangs, trace for CPU, etc.)

### 4. Missing Context

- **Problem**: Artifacts collected without understanding system state
- **Solution**: Document system conditions, load, and timing

### 5. Not Using Automated Workflows

- **Problem**: Manual collection misses critical steps or order
- **Solution**: Use automated workflows for consistency

## Workflow Recommendations

### High CPU Investigation

1. Collect baseline during normal operation
2. Use `workflow_automated_high_cpu_bundle` during issue (duration: 30–60 sec)
3. Analyze with `artifact_summarize` and `detect_patterns`
4. Review trace data for hot methods
5. Compare with baseline using `session_analyze`

### Memory Leak Investigation

1. Collect baseline gcdump from healthy state
2. Wait for issue to manifest (5–10 minutes)
3. Collect gcdump during issue
4. Use `analyze_heap` with `analysis_type: diff` to compare baseline vs issue
5. Use `find_retainer_paths` to trace why suspect objects are retained
6. Use `artifact_summarize` with `focus: memory`
7. Identify growing object types and their retention chains

### Hang/Deadlock Investigation

1. Collect dump immediately during hang
2. Use `analyze_dump_sos` with `threads` and `clrstack`
3. Use `detect_patterns` with `patternTypes: deadlocks`
4. Look for circular wait patterns and blocked threads
5. Check thread pool state in counters

### Baseline Performance Collection

1. Identify representative process via `runtime_list_targets`
2. Use `workflow_automated_baseline_bundle` (duration: 30–60 sec)
3. Pin artifacts with `artifact_pin` to prevent cleanup
4. Use `session_analyze` with `baselineSessionId` for future comparisons

## Conclusion

Following these best practices ensures efficient diagnostic investigations using CLRScope MCP. Start with automated workflows, use appropriate artifact types, and always collect baseline data for comparison.
