using ClrScope.Mcp.Contracts;
using ClrScope.Mcp.Options;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClrScope.Mcp.Validation;

public class PreflightValidator : IPreflightValidator
{
    private readonly IOptions<ClrScopeOptions> _options;
    private readonly ILogger<PreflightValidator> _logger;

    public PreflightValidator(IOptions<ClrScopeOptions> options, ILogger<PreflightValidator> logger)
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

        // Validation 4: Diagnostics enabled
        try
        {
            var client = new DiagnosticsClient(pid);
            // If DiagnosticsClient can be created, diagnostics is enabled
            _logger.LogDebug("Diagnostics is accessible for PID {Pid}", pid);
        }
        catch (Exception ex)
        {
            return PreflightResult.Failure(
                ClrScopeError.PREFLIGHT_DIAGNOSTICS_DISABLED,
                $"Diagnostics is not accessible for process {pid}: {ex.Message}");
        }

        // Validation 5: ArtifactRoot is writable
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

        return PreflightResult.Success();
    }
}
