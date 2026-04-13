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
            // Only "0" means diagnostics disabled; "1" or unset means enabled
            var enableDiagnostics = GetProcessEnvironmentVariable(pid, "DOTNET_EnableDiagnostics");
            if (enableDiagnostics == "0")
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
}
