namespace ClrScope.Mcp.Contracts;

public record InspectTargetResult(
    bool Found,
    bool Attachable,
    RuntimeTargetDetails? Details,
    string[] Warnings,
    string? Error
)
{
    public static InspectTargetResult NotFound(string? error = null) =>
        new(false, false, null, Array.Empty<string>(), error);

    public static InspectTargetResult NotAttachable(string reason, string[] warnings) =>
        new(true, false, null, warnings, reason);

    public static InspectTargetResult Success(RuntimeTargetDetails details, string[] warnings) =>
        new(true, true, details, warnings, null);
}

public record RuntimeTargetDetails(
    int Pid,
    string ProcessName,
    string? CommandLine,
    string OperatingSystem,
    string ProcessArchitecture
);
