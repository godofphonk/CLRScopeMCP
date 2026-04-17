namespace ClrScope.Mcp.Services.Workflows;

/// <summary>
/// Information about a diagnostic artifact
/// </summary>
public record ArtifactInfo(
    string ArtifactId,
    string Kind,
    string? FilePath,
    long SizeBytes
);

/// <summary>
/// Result of an automated diagnostic workflow execution
/// </summary>
public record WorkflowAutomationResult(
    bool Success,
    string WorkflowName,
    int StepsCompleted,
    int TotalSteps,
    ArtifactInfo[] Artifacts,
    string[] SessionIds,
    string? Error
);
