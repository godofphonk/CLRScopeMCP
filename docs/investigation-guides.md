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
4. `visualize_heap_snapshot` — type distribution analysis
5. `visualize_heap_snapshot` with `view: diff` and `baselineArtifactId` — compare baseline vs issue
6. `visualize_heap_snapshot` with `view: retainer_paths` — find what holds objects in memory
7. `collect_counters` with `System.Runtime` — GC metrics, allocation rate
8. `detect_patterns` with `patternTypes: memory_leaks` — automated leak detection

**Quick alternative:** `workflow_automated_memory_leak_bundle`

**Tip:** Use `.gcdump` files for heap visualization (reliable). `.nettrace` files are unreliable for heap data.

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
3. **`visualize_heap_snapshot`** — for memory issues (gcdump artifacts)
4. **`analyze_dump_sos`** — for deep .NET runtime analysis (dump artifacts)
5. **`session_analyze`** — compare sessions, especially with baseline
