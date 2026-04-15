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

    public async Task<PreflightResult> ValidateCollectAsync(int pid, CollectionOperationType operationType, CancellationToken cancellationToken = default)
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
            var testFileName = Path.GetRandomFileName();
            var testFile = Path.Combine(artifactRoot, testFileName);
            await File.WriteAllTextAsync(testFile, "test", cancellationToken);
            File.Delete(testFile);
        }
        catch (Exception ex)
        {
            return PreflightResult.Failure(
                ClrScopeError.PREFLIGHT_ARTIFACT_ROOT_NOT_WRITABLE,
                $"Artifact root is not writable: {ex.Message}");
        }

        // Validation 5: Disk space check (operation-specific)
        try
        {
            var driveInfo = new DriveInfo(artifactRoot);
            var requiredSpace = EstimateRequiredSpace(pid, operationType);

            if (driveInfo.AvailableFreeSpace < requiredSpace)
            {
                return PreflightResult.Failure(
                    ClrScopeError.PREFLIGHT_DISK_SPACE_LOW,
                    $"Insufficient disk space: {driveInfo.AvailableFreeSpace / (1024 * 1024)} MB available, minimum {requiredSpace / (1024 * 1024)} MB required for {operationType}");
            }
        }
        catch (Exception ex)
        {
            // Non-critical, log warning but don't fail
            _logger.LogWarning("Could not check disk space: {Message}", ex.Message);
        }

        // Validation 6: DOTNET_EnableDiagnostics check
        var diagnosticsDisabled = CheckDiagnosticsDisabled(pid);
        if (diagnosticsDisabled)
        {
            return PreflightResult.Failure(
                ClrScopeError.PREFLIGHT_DIAGNOSTICS_DISABLED,
                "DOTNET_EnableDiagnostics is set to 0, diagnostics are disabled");
        }

        return PreflightResult.Success();
    }

    private long EstimateRequiredSpace(int pid, CollectionOperationType operationType)
    {
        // Base minimum for any operation
        const long BaseMinimum = 50 * 1024 * 1024; // 50 MB

        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);

            switch (operationType)
            {
                case CollectionOperationType.Dump:
                    // Conservative estimate: WorkingSet × 1.5 for dump + compression overhead
                    var workingSet = process.WorkingSet64;
                    var dumpRequired = (long)(workingSet * 1.5);
                    return Math.Max(BaseMinimum, dumpRequired);

                case CollectionOperationType.GcDump:
                    // GC heap dump is typically smaller than full dump: managed heap × 1.2
                    var managedMemory = process.PrivateMemorySize64;
                    var gcDumpRequired = (long)(managedMemory * 1.2);
                    return Math.Max(BaseMinimum, gcDumpRequired);

                case CollectionOperationType.Trace:
                    // Trace: duration × expected throughput (conservative 10 MB/s for 60s = 600 MB)
                    const long TraceDurationSeconds = 60;
                    const long TraceThroughputMBps = 10;
                    var traceRequired = TraceDurationSeconds * TraceThroughputMBps * 1024 * 1024;
                    return Math.Max(BaseMinimum, traceRequired);

                case CollectionOperationType.Stacks:
                    // Stacks are typically small: 10 MB
                    const long StacksRequired = 10 * 1024 * 1024;
                    return Math.Max(BaseMinimum, StacksRequired);

                case CollectionOperationType.Counters:
                    // Counters are very small: 5 MB
                    const long CountersRequired = 5 * 1024 * 1024;
                    return Math.Max(BaseMinimum, CountersRequired);

                default:
                    return BaseMinimum;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not estimate required space for PID {Pid}, using base minimum", pid);
            return BaseMinimum;
        }
    }

    private string? GetProcessEnvironmentVariable(int pid, string variableName)
    {
        try
        {
            var diagnosticsClient = new DiagnosticsClient(pid);
            var environment = diagnosticsClient.GetProcessEnvironment();

            if (environment.TryGetValue(variableName, out var value))
            {
                return value;
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
            // Only "0" means diagnostics disabled; "1" or unset means enabled
            var enableDiagnostics = GetProcessEnvironmentVariable(pid, "DOTNET_EnableDiagnostics");
            if (enableDiagnostics == "0")
            {
                return true;
            }

            // Check DOTNET_EnableDiagnostics_IPC environment variable (.NET 8+)
            // When set to "0", disables the Diagnostic Port and cannot be overridden
            var enableDiagnosticsIpc = GetProcessEnvironmentVariable(pid, "DOTNET_EnableDiagnostics_IPC");
            if (enableDiagnosticsIpc == "0")
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not check diagnostics environment variables");
            return false;
        }
    }
}
