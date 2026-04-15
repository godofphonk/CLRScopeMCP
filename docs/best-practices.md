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

### 4. Use Flame Graphs for Stack Analysis

For Dump and Stacks artifacts, use flame graph visualization:

```bash
mcp1_visualize_flame_graph --artifact_id <id> --format svg --auto_analyze true
```

Provides visual representation of call stacks and hot paths.

## Performance Optimization

### Artifact Cleanup

Regularly clean up old artifacts to prevent disk space issues:

```bash
# Clean artifacts older than 7 days
mcp1_artifact_cleanup --strategy age --maxAge 7d

# Remove duplicates
mcp1_artifact_cleanup --strategy duplicates
```

### Cache Management

Flame graph preprocessing is cached by default. Use `analysis_mode` to control:

- `auto`: Use cache or analyze (default)
- `reuse`: Only use cached data (fast)
- `force`: Re-analyze (slow, fresh data)

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
2. Use `workflow_automated_high_cpu_bundle` during issue
3. Analyze with `artifact_summarize` and `detect_patterns`
4. Review flame graph for hot methods
5. Compare with baseline

### Memory Leak Investigation

1. Collect baseline gcdump
2. Wait for issue to manifest (5-10 minutes)
3. Collect gcdump during issue
4. Compare baseline vs issue
5. Use `artifact_summarize` with `focus memory`
6. Identify growing object types

### Hang/Deadlock Investigation

1. Collect dump immediately during hang
2. Use `analyze_dump_sos` with `threads` and `clrstack`
3. Use `visualize_flame_graph` for stack visualization
4. Look for circular wait patterns
5. Check thread pool state

### Baseline Performance Collection

1. Identify representative process
2. Use `workflow_automated_baseline_bundle`
3. Save artifacts with descriptive names
4. Document system conditions
5. Store in dedicated location

## Conclusion

Following these best practices ensures efficient, effective, and reliable diagnostic investigations using CLRScope MCP. Automated workflows, appropriate artifact selection, and proper analysis techniques lead to faster problem resolution.
