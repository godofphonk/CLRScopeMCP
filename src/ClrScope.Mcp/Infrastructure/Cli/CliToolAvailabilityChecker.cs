using ClrScope.Mcp.Contracts;
using Microsoft.Extensions.Logging;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Checks availability of external CLI tools via dotnet tool list
/// </summary>
public sealed class CliToolAvailabilityChecker : ICliToolAvailabilityChecker
{
    private readonly ICliCommandRunner _cliRunner;
    private readonly ILogger<CliToolAvailabilityChecker> _logger;

    public CliToolAvailabilityChecker(
        ICliCommandRunner cliRunner,
        ILogger<CliToolAvailabilityChecker> logger)
    {
        _cliRunner = cliRunner;
        _logger = logger;
    }

    public async Task<CliToolAvailability> CheckAvailabilityAsync(string toolName, CancellationToken cancellationToken = default)
    {
        return await CheckAvailabilityInternalAsync(toolName, cancellationToken);
    }

    public CliToolAvailability CheckAvailabilitySync(string toolName)
    {
        try
        {
            return CheckAvailabilityInternalAsync(toolName, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            return new CliToolAvailability(toolName, false, null, GetInstallHint(toolName));
        }
    }

    private async Task<CliToolAvailability> CheckAvailabilityInternalAsync(string toolName, CancellationToken cancellationToken)
    {
        try
        {
            // Check via dotnet tool list --global
            var result = await _cliRunner.ExecuteAsync("dotnet", new[] { "tool", "list", "--global" }, cancellationToken);
            
            if (result.ExitCode == 0 && result.StandardOutput.Contains(toolName))
            {
                var version = ExtractVersion(result.StandardOutput, toolName);
                _logger.LogDebug("CLI tool {ToolName} is available (global), version: {Version}", toolName, version);
                return new CliToolAvailability(toolName, true, version, GetInstallHint(toolName));
            }

            // Check via dotnet tool list (local)
            var localResult = await _cliRunner.ExecuteAsync("dotnet", new[] { "tool", "list" }, cancellationToken);
            
            if (localResult.ExitCode == 0 && localResult.StandardOutput.Contains(toolName))
            {
                var version = ExtractVersion(localResult.StandardOutput, toolName);
                _logger.LogDebug("CLI tool {ToolName} is available (local), version: {Version}", toolName, version);
                return new CliToolAvailability(toolName, true, version, GetInstallHint(toolName));
            }

            // Check via which/where
            var checkCommand = Environment.OSVersion.Platform == PlatformID.Unix 
                ? "which" 
                : "where";
            
            var pathResult = await _cliRunner.ExecuteAsync(checkCommand, new[] { toolName }, cancellationToken);
            
            if (pathResult.ExitCode == 0)
            {
                _logger.LogDebug("CLI tool {ToolName} is available in PATH", toolName);
                return new CliToolAvailability(toolName, true, null, GetInstallHint(toolName));
            }

            _logger.LogDebug("CLI tool {ToolName} is not available", toolName);
            return new CliToolAvailability(toolName, false, null, GetInstallHint(toolName));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check availability of CLI tool {ToolName}", toolName);
            return new CliToolAvailability(toolName, false, null, GetInstallHint(toolName));
        }
    }

    private static string? ExtractVersion(string output, string toolName)
    {
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains(toolName))
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    return parts[1];
                }
            }
        }
        return null;
    }

    private static string GetInstallHint(string toolName)
    {
        return toolName switch
        {
            "dotnet-dump" => """
Install using one of the following methods:

1) Global:
   dotnet tool install --global dotnet-dump

2) Repo-local (recommended for teams):
   dotnet new tool-manifest   # if manifest doesn't exist yet
   dotnet tool install dotnet-dump
   dotnet tool restore

Restart MCP server / client after installation.
""",
            "dotnet-symbol" => """
Install using one of the following methods:

1) Global:
   dotnet tool install --global dotnet-symbol

2) Repo-local (recommended for teams):
   dotnet new tool-manifest   # if manifest doesn't exist yet
   dotnet tool install dotnet-symbol
   dotnet tool restore

Restart MCP server / client after installation.
""",
            _ => $"Install: dotnet tool install --global {toolName}"
        };
    }
}
