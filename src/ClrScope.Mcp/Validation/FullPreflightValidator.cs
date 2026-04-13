using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Options;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClrScope.Mcp.Validation;

public class FullPreflightValidator : IPreflightValidator
{
    private readonly IOptions<ClrScopeOptions> _options;
    private readonly ILogger<FullPreflightValidator> _logger;

    public FullPreflightValidator(IOptions<ClrScopeOptions> options, ILogger<FullPreflightValidator> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<PreflightResult> ValidateCollectAsync(int pid, CancellationToken cancellationToken = default)
    {
        // Validation 1: PID > 0
        if (pid <= 0)
        {
            return PreflightResult.Failure(
                ClrScopeError.VALIDATION_INVALID_PID,
                $"PID must be greater than 0, got {pid}");
        }

        // Validation 2: Process exists
        try
        {
            System.Diagnostics.Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return PreflightResult.Failure(
                ClrScopeError.PREFLIGHT_PROCESS_NOT_FOUND,
                $"Process with PID {pid} not found");
        }

        // Validation 3: Process is .NET
        var publishedProcesses = DiagnosticsClient.GetPublishedProcesses();
        if (!publishedProcesses.Contains(pid))
        {
            return PreflightResult.Failure(
                ClrScopeError.PREFLIGHT_NOT_DOTNET,
                $"Process with PID {pid} is not a .NET process or not attachable");
        }

        // Validation 4: ArtifactRoot is writable
        var artifactRoot = _options.Value.GetArtifactRoot();
        if (!Directory.Exists(artifactRoot))
        {
            try
            {
                Directory.CreateDirectory(artifactRoot);
            }
            catch (Exception ex)
            {
                return PreflightResult.Failure(
                    ClrScopeError.PREFLIGHT_ARTIFACT_ROOT_NOT_WRITABLE,
                    $"Cannot create artifact root directory: {ex.Message}");
            }
        }

        try
        {
            var testFile = Path.Combine(artifactRoot, ".write_test");
            await File.WriteAllTextAsync(testFile, "test", cancellationToken);
            File.Delete(testFile);
        }
        catch (Exception ex)
        {
            return PreflightResult.Failure(
                ClrScopeError.PREFLIGHT_ARTIFACT_ROOT_NOT_WRITABLE,
                $"Artifact root is not writable: {ex.Message}");
        }

        // Validation 5: Disk space check (basic)
        try
        {
            var driveInfo = new DriveInfo(artifactRoot);
            if (driveInfo.AvailableFreeSpace < 100 * 1024 * 1024) // 100 MB minimum
            {
                return PreflightResult.Failure(
                    ClrScopeError.PREFLIGHT_DISK_SPACE_LOW,
                    $"Insufficient disk space: {driveInfo.AvailableFreeSpace / (1024 * 1024)} MB available, minimum 100 MB required");
            }
        }
        catch (Exception ex)
        {
            // Non-critical, log warning but don't fail
            _logger.LogWarning("Could not check disk space: {Message}", ex.Message);
        }

        // Validation 6: TMPDIR mismatch (Unix only)
        var tmpdirMismatch = CheckTmpdirMismatch(pid);
        if (tmpdirMismatch != null)
        {
            _logger.LogWarning("TMPDIR mismatch detected: {Message}", tmpdirMismatch);
            // Return as warning, not failure (best-effort)
        }

        // Validation 7: DOTNET_EnableDiagnostics check
        var diagnosticsDisabled = CheckDiagnosticsDisabled(pid);
        if (diagnosticsDisabled)
        {
            return PreflightResult.Failure(
                ClrScopeError.PREFLIGHT_DIAGNOSTICS_DISABLED,
                "DOTNET_EnableDiagnostics is set to 0 or 1, diagnostics may be disabled");
        }

        // Validation 8: Container capabilities (SYS_PTRACE)
        var containerCapabilities = CheckContainerCapabilities();
        if (!containerCapabilities)
        {
            _logger.LogWarning("Container capabilities check failed: SYS_PTRACE may not be available");
            // Return as warning, not failure (best-effort)
        }

        // Validation 9: cgroup limits (Linux only)
        var cgroupLimits = CheckCgroupLimits();
        if (cgroupLimits != null)
        {
            _logger.LogWarning("cgroup limits detected: {Message}", cgroupLimits);
            // Return as warning, not failure (best-effort)
        }

        // Validation 10: namespace checks (PID namespace)
        var namespaceCheck = CheckNamespaceCompatibility(pid);
        if (namespaceCheck != null)
        {
            _logger.LogWarning("Namespace compatibility issue: {Message}", namespaceCheck);
            // Return as warning, not failure (best-effort)
        }

        return PreflightResult.Success();
    }

    private string? CheckTmpdirMismatch(int pid)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return null; // Not applicable on Windows
        }

        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);

            // Read TMPDIR from target process environment
            var processTmpdir = GetProcessEnvironmentVariable(pid, "TMPDIR");

            // If TMPDIR is set, check if it's accessible
            if (!string.IsNullOrEmpty(processTmpdir) && !Directory.Exists(processTmpdir))
            {
                return $"Process TMPDIR '{processTmpdir}' does not exist or is not accessible";
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not check TMPDIR mismatch");
            return null;
        }
    }

    private string? GetProcessEnvironmentVariable(int pid, string variableName)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null; // Only supported on Linux
        }

        try
        {
            var environPath = $"/proc/{pid}/environ";
            if (!File.Exists(environPath))
            {
                return null;
            }

            var environContent = File.ReadAllText(environPath);
            var variables = environContent.Split('\0', StringSplitOptions.RemoveEmptyEntries);

            foreach (var variable in variables)
            {
                var parts = variable.Split('=', 2);
                if (parts.Length == 2 && parts[0] == variableName)
                {
                    return parts[1];
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read environment variable {VariableName} from process {Pid}", variableName, pid);
            return null;
        }
    }

    private bool CheckDiagnosticsDisabled(int pid)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);

            // Check DOTNET_EnableDiagnostics environment variable from target process
            var enableDiagnostics = GetProcessEnvironmentVariable(pid, "DOTNET_EnableDiagnostics");
            if (enableDiagnostics == "0" || enableDiagnostics == "1")
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not check DOTNET_EnableDiagnostics");
            return false;
        }
    }

    private bool CheckContainerCapabilities()
    {
        if (!OperatingSystem.IsLinux())
        {
            return true; // Not applicable on non-Linux
        }

        try
        {
            // Check if running in container
            var dockerEnv = File.Exists("/.dockerenv");
            if (!dockerEnv)
            {
                return true; // Not in container, no need to check capabilities
            }

            // Check SYS_PTRACE capability (basic check via /proc/self/status)
            if (File.Exists("/proc/self/status"))
            {
                var status = File.ReadAllText("/proc/self/status");
                // This is a simplified check - in production, proper capability checking requires more complex logic
                return true; // Assume available for now
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not check container capabilities");
            return true; // Assume available on error
        }
    }

    private string? CheckCgroupLimits()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null; // Not applicable on non-Linux
        }

        try
        {
            // Check if running in container
            var dockerEnv = File.Exists("/.dockerenv");
            if (!dockerEnv)
            {
                return null; // Not in container
            }

            // Try cgroup v2 first
            var cgroupV2MemoryPath = "/sys/fs/cgroup/memory.max";
            var cgroupV2CpuPath = "/sys/fs/cgroup/cpu.max";

            if (File.Exists(cgroupV2MemoryPath) || File.Exists(cgroupV2CpuPath))
            {
                // cgroup v2 detected
                return CheckCgroupV2Limits(cgroupV2MemoryPath, cgroupV2CpuPath);
            }

            // Fallback to cgroup v1
            return CheckCgroupV1Limits();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not check cgroup limits");
            return null;
        }
    }

    private string? CheckCgroupV2Limits(string memoryPath, string cpuPath)
    {
        // Check memory limit (cgroup v2)
        if (File.Exists(memoryPath))
        {
            var memoryLimit = File.ReadAllText(memoryPath).Trim();
            if (memoryLimit != "max" && long.TryParse(memoryLimit, out var limitBytes))
            {
                var limitMB = limitBytes / (1024 * 1024);
                if (limitMB < 512) // Less than 512 MB
                {
                    return $"Low memory limit detected (cgroup v2): {limitMB} MB";
                }
            }
        }

        // Check CPU limit (cgroup v2)
        if (File.Exists(cpuPath))
        {
            var cpuLimit = File.ReadAllText(cpuPath).Trim();
            // Format: "$quota $period" or "max $period"
            var parts = cpuLimit.Split(' ');
            if (parts.Length >= 2 && parts[0] != "max" && long.TryParse(parts[0], out var quota) && long.TryParse(parts[1], out var period))
            {
                var cpuLimitValue = (double)quota / period;
                if (cpuLimitValue < 0.5) // Less than 0.5 CPU
                {
                    return $"Low CPU limit detected (cgroup v2): {cpuLimitValue:F2} CPUs";
                }
            }
        }

        return null;
    }

    private string? CheckCgroupV1Limits()
    {
        // Check memory limit (cgroup v1)
        var memoryLimitPath = "/sys/fs/cgroup/memory/memory.limit_in_bytes";
        if (File.Exists(memoryLimitPath))
        {
            var memoryLimit = File.ReadAllText(memoryLimitPath).Trim();
            if (long.TryParse(memoryLimit, out var limitBytes))
            {
                var limitMB = limitBytes / (1024 * 1024);
                if (limitMB < 512) // Less than 512 MB
                {
                    return $"Low memory limit detected (cgroup v1): {limitMB} MB";
                }
            }
        }

        // Check CPU limit (cgroup v1)
        var cpuQuotaPath = "/sys/fs/cgroup/cpu/cpu.cfs_quota_us";
        var cpuPeriodPath = "/sys/fs/cgroup/cpu/cpu.cfs_period_us";
        if (File.Exists(cpuQuotaPath) && File.Exists(cpuPeriodPath))
        {
            var quota = File.ReadAllText(cpuQuotaPath).Trim();
            var period = File.ReadAllText(cpuPeriodPath).Trim();
            if (int.TryParse(quota, out var quotaValue) && int.TryParse(period, out var periodValue))
            {
                if (quotaValue > 0)
                {
                    var cpuLimit = (double)quotaValue / periodValue;
                    if (cpuLimit < 0.5) // Less than 0.5 CPU
                    {
                        return $"Low CPU limit detected (cgroup v1): {cpuLimit:F2} CPUs";
                    }
                }
            }
        }

        return null;
    }

    private string? CheckNamespaceCompatibility(int pid)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null; // Not applicable on non-Linux
        }

        try
        {
            // Check if running in container
            var dockerEnv = File.Exists("/.dockerenv");
            if (!dockerEnv)
            {
                return null; // Not in container
            }

            // Check PID namespace
            var pidNamespacePath = $"/proc/{pid}/ns/pid";
            if (File.Exists(pidNamespacePath))
            {
                // In production, we would compare namespace inodes to detect PID namespace isolation
                // For now, just log a warning that we're in a container
                return "Running in container with PID namespace isolation - process visibility may be limited";
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not check namespace compatibility");
            return null;
        }
    }
}
