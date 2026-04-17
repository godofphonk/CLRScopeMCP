# Investigation Guides

Step-by-step guides for common .NET diagnostic scenarios using CLRScope MCP.

## Automated Workflows (Recommended)

For most scenarios, start with an automated workflow. Each workflow collects all necessary artifacts in the correct sequence with a single command.

| Workflow | Steps | Use When |
|----------|-------|----------|
| `workflow_automated_high_cpu_bundle` | trace → counters → stacks | CPU usage is abnormally high |
| `workflow_automated_memory_leak_bundle` | gcdump → counters → trace (gc-heap) | Memory keeps growing over time |
| `workflow_automated_hang_bundle` | dump → stacks → counters | Application is frozen/unresponsive |
| `workflow_automated_baseline_bundle` | counters → trace → gcdump → stacks | Collect reference data from healthy state |

**Parameters:**
- `pid` (required) — target .NET process ID
- `duration` (optional) — collection duration in `hh:mm:ss` format (default: `00:01:00`, use `00:00:05`–`00:00:30` for quick tests)

**Returns:** success status, collected artifacts with IDs, session IDs, execution time.

---

## Workflow Selection Guide

### Decision Tree for Workflow Selection

```
Is the issue reproducible?
├─ Yes
│  ├─ Is the system responsive?
│  │  ├─ Yes → Use Baseline + Issue workflow
│  │  │         1. Collect baseline: workflow_automated_baseline_bundle
│  │  │         2. Reproduce issue
│  │  │         3. Collect issue: appropriate workflow (CPU/Memory/Hang)
│  │  │         4. Compare: session_analyze with baseline
│  │  └─ No (frozen) → Use Hang workflow immediately
│  │         workflow_automated_hang_bundle
│  └─ No (intermittent)
│     └─ Use Baseline + Monitoring workflow
│        1. Collect baseline: workflow_automated_baseline_bundle
│        2. Set up continuous monitoring
│        3. Trigger collection on issue occurrence
└─ No (production issue, can't reproduce)
   └─ Use existing artifacts if available
      1. artifact_list to find relevant artifacts
      2. artifact_summarize for quick analysis
      3. session_analyze for comparison
```

### Scenario-Based Workflow Recommendations

#### High CPU (>80% sustained)

**Symptoms:**
- Process CPU consistently above 80%
- Slow response times
- High thread pool queue length

**Recommended Workflow:**
```
workflow_automated_high_cpu_bundle
```

**Parameters:**
- `duration`: `00:01:00` (1 minute) for sustained issues
- `duration`: `00:00:30` (30 seconds) for quick checks

**Follow-up Analysis:**
1. `artifact_summarize` — automated analysis
2. `detect_patterns` with `patternTypes: high_cpu` — identify hot paths
3. Review trace data for hot methods
4. Compare with baseline if available

**When to use manual collection instead:**
- Need to correlate with specific user actions
- Need to collect during specific time window
- Need to collect multiple samples over time

---

#### Memory Leak (Memory Growing Over Time)

**Symptoms:**
- Process memory grows continuously
- OutOfMemoryException after extended runtime
- High GC pause times

**Recommended Workflow:**
```
workflow_automated_memory_leak_bundle
```

**Parameters:**
- `duration`: `00:01:00` (1 minute) — duration less critical for memory
- Run at regular intervals (e.g., every hour) to track growth

**Follow-up Analysis:**
1. `artifact_summarize` — automated analysis
2. `analyze_heap` with `analysisType: type_stats` — identify top types
3. `analyze_heap` with `analysisType: diff` — compare baseline vs issue
4. `analyze_heap` with `analysisType: objects` — get node IDs
5. `find_retainer_paths` — trace retention chains
6. `detect_patterns` with `patternTypes: memory_leaks` — automated detection

**Critical:** Always collect baseline before issue occurs for meaningful diff analysis.

---

#### Hang/Deadlock (Application Frozen)

**Symptoms:**
- Application completely unresponsive
- Requests timing out
- UI frozen

**Recommended Workflow:**
```
workflow_automated_hang_bundle
```

**Parameters:**
- `duration`: `00:00:30` (30 seconds) — quick collection is critical
- Collect immediately when hang occurs

**Follow-up Analysis:**
1. `artifact_summarize` — automated analysis
2. `analyze_dump_sos` with `command: threads` — identify blocked threads
3. `analyze_dump_sos` with `command: clrstack` — get stack traces
4. `detect_patterns` with `patternTypes: deadlocks` — detect circular waits
5. Review thread pool counters

**Critical:** Time-sensitive - must collect dump immediately when hang occurs.

---

#### Baseline Performance Collection

**Purpose:** Collect reference data from healthy system state for future comparison.

**Recommended Workflow:**
```
workflow_automated_baseline_bundle
```

**Parameters:**
- `duration`: `00:01:00` (1 minute) — sufficient for baseline
- Run during normal operation (no issues)

**Follow-up:**
1. `artifact_pin` — pin baseline artifacts to prevent cleanup
2. Document baseline conditions (load, user count, etc.)
3. Use `session_analyze` with `baselineSessionId` for future comparisons

**When to collect baseline:**
- Before deploying new version
- During normal peak hours
- Before making configuration changes
- As part of regular monitoring schedule

---

#### Intermittent Issues

**Symptoms:**
- Issue occurs sporadically
- Cannot reproduce on demand
- Issue happens under specific load conditions

**Recommended Approach:**
```
1. Collect baseline: workflow_automated_baseline_bundle
2. Set up continuous monitoring
3. Trigger collection when issue occurs
```

**Manual Collection Strategy:**
- Use individual tools for flexible collection
- Collect multiple samples over time
- Correlate with application logs
- Use `artifact_summarize` on each sample

**Follow-up:**
- Compare samples to identify patterns
- Use `session_analyze` to group related artifacts
- Look for correlation with specific events

---

#### Production Debugging (No Reproduction)

**Situation:** Issue occurred in production, cannot reproduce locally.

**Recommended Approach:**
```
1. artifact_list — find existing artifacts
2. artifact_summarize — quick analysis
3. session_analyze — group related artifacts
4. Manual investigation based on available data
```

**If artifacts available:**
- Use appropriate manual investigation guide
- Compare with baseline if available
- Focus on what data is available

**If no artifacts available:**
- Document what happened (logs, metrics)
- Plan for future artifact collection
- Set up monitoring to catch next occurrence

---

### Workflow Comparison

| Scenario | Recommended Workflow | Duration | Priority | Baseline Needed? |
|----------|---------------------|----------|----------|------------------|
| High CPU | `workflow_automated_high_cpu_bundle` | 1 min | High | Recommended |
| Memory Leak | `workflow_automated_memory_leak_bundle` | 1 min | High | Required |
| Hang/Deadlock | `workflow_automated_hang_bundle` | 30 sec | Critical | Optional |
| Baseline | `workflow_automated_baseline_bundle` | 1 min | High | N/A |
| Intermittent | Manual tools | Variable | Medium | Required |
| Production Debug | Existing artifacts | N/A | High | Recommended |

### Common Mistakes to Avoid

1. **Not collecting baseline** — Cannot determine if metrics are abnormal
2. **Using wrong workflow** — e.g., using CPU workflow for memory issues
3. **Collecting too late** — especially for hang/deadlock scenarios
4. **Collecting too short** — insufficient data for analysis
5. **Not pinning baseline artifacts** — automatic cleanup removes them
6. **Using manual collection when workflow exists** — increases error risk

---

## Manual Investigation Guides

For granular control or when automated workflows are not suitable.

### High CPU Investigation

1. `runtime_list_targets` — find the .NET process with high CPU
2. `runtime_inspect_target` — verify process details
3. `collect_trace` with `cpu-sampling` profile (30–60 sec) — capture CPU activity
4. `collect_counters` with `System.Runtime` provider — CPU and thread pool metrics
5. `collect_stacks` — snapshot of all thread stacks
6. `artifact_summarize` — automated analysis with key findings
7. `detect_patterns` with `patternTypes: high_cpu` — identify hot paths
8. Review recommendations and optimize identified methods

**Quick alternative:** `workflow_automated_high_cpu_bundle`

---

### Memory Leak Investigation

1. `runtime_list_targets` — find the .NET process with high memory
2. `collect_gcdump` — capture GC heap snapshot (baseline)
3. Wait for memory to grow, then `collect_gcdump` again (issue snapshot)
4. `analyze_heap` — type statistics with retained size (dominator tree analysis)
5. `analyze_heap` with `analysisType: diff` and `baselineArtifactId` — compare baseline vs issue
6. `analyze_heap` with `analysisType: objects` — get list of objects with their node IDs (sorted by size or count)
7. `find_retainer_paths` with `targetNodeId` — trace retention chain from GC roots to suspect object
8. `collect_counters` with `System.Runtime` — GC metrics, allocation rate
9. `detect_patterns` with `patternTypes: memory_leaks` — automated leak detection

**Quick alternative:** `workflow_automated_memory_leak_bundle`

**Tip:** Heap analysis accepts only `.gcdump` files. Since 1.3.0 the `.nettrace` heap-analysis path is removed; use `collect_gcdump` (or `dotnet-gcdump`) for heap snapshots.
---

### Hang/Deadlock Investigation

1. `runtime_list_targets` — find the hung .NET process
2. `collect_dump` — capture full memory dump immediately
3. `collect_stacks` — capture managed thread stacks
4. `analyze_dump_sos` with command `threads` — list all threads and their states
5. `analyze_dump_sos` with command `clrstack` — get stack traces per thread
6. `detect_patterns` with `patternTypes: deadlocks` — detect circular wait patterns
7. `collect_counters` with `System.Runtime` — check thread pool queue length
8. Look for: blocked threads, circular waits, async/await deadlocks, thread pool starvation

**Quick alternative:** `workflow_automated_hang_bundle`

---

### Baseline Performance Collection

1. `runtime_list_targets` — identify the target process in healthy state
2. `runtime_inspect_target` — verify process details
3. `workflow_automated_baseline_bundle` with short duration (30–60 sec)
4. `artifact_pin` — pin baseline artifacts to prevent automatic cleanup
5. Later, use `session_analyze` with `baselineSessionId` to compare with issue session

**Tip:** Always collect baseline data before investigating issues. Without baseline, you can't tell if metrics are abnormal.

---

## Analysis Workflow

After collecting artifacts (manually or via workflow):

1. **`artifact_summarize`** — start here for automated high-level analysis
2. **`detect_patterns`** — check for known problem patterns
3. **`analyze_heap`** — for memory issues: type stats with retained size via dominator tree (gcdump artifacts)
4. **`find_retainer_paths`** — trace why a specific object is retained in memory (gcdump artifacts)
5. **`analyze_dump_sos`** — for deep .NET runtime analysis (dump artifacts)
6. **`session_analyze`** — compare sessions, especially with baseline
