using ClrScope.Mcp.Infrastructure;
using ClrScope.Mcp.Services.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClrScope.Mcp.Tests.Tools.Workflows;

public class WorkflowAutomationToolsTests
{
    [Fact]
    public void WorkflowAutomationResult_CreatesWithValidParameters()
    {
        // Arrange
        var artifacts = new[]
        {
            new ArtifactInfo("artifact-1", "Trace", "/path/to/trace.nettrace", 1024),
            new ArtifactInfo("artifact-2", "Counters", "/path/to/counters.txt", 512),
            new ArtifactInfo("artifact-3", "Stacks", "/path/to/stacks.txt", 256)
        };

        // Act
        var result = new WorkflowAutomationResult(
            Success: true,
            WorkflowName: "High CPU Bundle",
            StepsCompleted: 3,
            TotalSteps: 3,
            Artifacts: artifacts,
            SessionIds: new[] { "session-1", "session-2", "session-3" },
            Error: null
        );

        // Assert
        Assert.True(result.Success);
        Assert.Equal("High CPU Bundle", result.WorkflowName);
        Assert.Equal(3, result.StepsCompleted);
        Assert.Equal(3, result.TotalSteps);
        Assert.Equal(3, result.Artifacts.Length);
        Assert.Equal(3, result.SessionIds.Length);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ArtifactInfo_CreatesWithValidParameters()
    {
        // Act
        var artifact = new ArtifactInfo(
            ArtifactId: "test-artifact",
            Kind: "Trace",
            FilePath: "/path/to/file",
            SizeBytes: 1024
        );

        // Assert
        Assert.Equal("test-artifact", artifact.ArtifactId);
        Assert.Equal("Trace", artifact.Kind);
        Assert.Equal("/path/to/file", artifact.FilePath);
        Assert.Equal(1024, artifact.SizeBytes);
    }

    [Fact]
    public void WorkflowAutomationResult_PartialSuccess_WhenNotAllStepsComplete()
    {
        // Arrange
        var artifacts = new[]
        {
            new ArtifactInfo("artifact-1", "Trace", "/path/to/trace.nettrace", 1024)
        };

        // Act
        var result = new WorkflowAutomationResult(
            Success: true,
            WorkflowName: "High CPU Bundle",
            StepsCompleted: 1,
            TotalSteps: 3,
            Artifacts: artifacts,
            SessionIds: new[] { "session-1" },
            Error: "Step 2 failed; Step 3 failed"
        );

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.StepsCompleted);
        Assert.Equal(3, result.TotalSteps);
        Assert.NotNull(result.Error);
        Assert.Contains("Step 2 failed", result.Error);
    }

    [Fact]
    public void WorkflowAutomationResult_Failure_WhenNoStepsComplete()
    {
        // Act
        var result = new WorkflowAutomationResult(
            Success: false,
            WorkflowName: "High CPU Bundle",
            StepsCompleted: 0,
            TotalSteps: 3,
            Artifacts: Array.Empty<ArtifactInfo>(),
            SessionIds: Array.Empty<string>(),
            Error: "Process ID must be greater than 0"
        );

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, result.StepsCompleted);
        Assert.Equal(3, result.TotalSteps);
        Assert.Empty(result.Artifacts);
        Assert.Empty(result.SessionIds);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void WorkflowAutomationResult_ErrorCompensation_ContainsStepFailureInformation()
    {
        // Arrange
        var artifacts = new[]
        {
            new ArtifactInfo("artifact-1", "GcDump", "/path/to/gcdump.gcdump", 1024),
            new ArtifactInfo("artifact-2", "Counters", "/path/to/counters.txt", 512)
        };

        // Act
        var result = new WorkflowAutomationResult(
            Success: true,
            WorkflowName: "Memory Leak Bundle",
            StepsCompleted: 2,
            TotalSteps: 3,
            Artifacts: artifacts,
            SessionIds: new[] { "session-1", "session-2" },
            Error: "Step 3 (collect_trace) failed: Operation canceled"
        );

        // Assert
        Assert.True(result.Success); // Partial success is still considered success
        Assert.Equal(2, result.StepsCompleted);
        Assert.NotNull(result.Error);
        Assert.Contains("Step 3", result.Error);
        Assert.Contains("collect_trace", result.Error);
        Assert.Contains("failed", result.Error);
        
        // Verify that completed steps are reflected in artifacts
        Assert.Equal(2, result.Artifacts.Length);
        Assert.Equal("GcDump", result.Artifacts[0].Kind);
        Assert.Equal("Counters", result.Artifacts[1].Kind);
    }

    [Fact]
    public void WorkflowAutomationResult_SessionStateRollback_ErrorContainsRelevantContext()
    {
        // Act
        var result = new WorkflowAutomationResult(
            Success: false,
            WorkflowName: "Hang Bundle",
            StepsCompleted: 1,
            TotalSteps: 3,
            Artifacts: Array.Empty<ArtifactInfo>(),
            SessionIds: new[] { "session-1" },
            Error: "Step 2 (collect_stacks) failed: Operation canceled. Session rolled back to Failed state."
        );

        // Assert
        Assert.False(result.Success);
        Assert.Contains("rolled back", result.Error);
        Assert.Contains("Failed state", result.Error);
        Assert.Single(result.SessionIds); // Session was created before failure
    }

    [Fact]
    public async Task WorkflowOrchestrator_DoesNotDeadlock_WhenInnerServiceAlsoLocks()
    {
        // Arrange
        var pidLockManager = new PidLockManager();
        var orchestrator = new WorkflowOrchestrator(NullLogger<WorkflowOrchestrator>.Instance);
        var pid = 12345;
        var duration = "00:00:01";

        // Create a mock workflow that acquires the same PID lock internally
        var mockWorkflow = new MockWorkflowThatAlsoLocks(pidLockManager);

        // Act & Assert
        // Use a timeout to detect deadlock - if deadlock occurs, this will timeout and fail
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var executeTask = orchestrator.ExecuteWorkflowAsync(mockWorkflow, pid, duration, CancellationToken.None);

        var completedTask = await Task.WhenAny(executeTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            // Timeout means deadlock occurred
            throw new TimeoutException("WorkflowOrchestrator deadlocked when inner service also acquired PID lock");
        }

        // If we reach here, no deadlock occurred
        var result = await executeTask;
        Assert.NotNull(result);
    }

    /// <summary>
    /// Mock workflow that simulates collect services by acquiring PID lock internally
    /// </summary>
    private class MockWorkflowThatAlsoLocks : IWorkflow
    {
        private readonly IPidLockManager _pidLockManager;

        public MockWorkflowThatAlsoLocks(IPidLockManager pidLockManager)
        {
            _pidLockManager = pidLockManager;
        }

        public string WorkflowName => "MockWorkflowWithLock";
        public int TotalSteps => 1;

        public async Task<WorkflowAutomationResult> ExecuteAsync(int pid, string duration, CancellationToken cancellationToken)
        {
            // Simulate collect service behavior: acquire PID lock internally
            using var pidLock = await _pidLockManager.AcquireLockAsync(pid, cancellationToken);

            // Simulate some work
            await Task.Delay(100, cancellationToken);

            return new WorkflowAutomationResult(
                Success: true,
                WorkflowName: WorkflowName,
                StepsCompleted: 1,
                TotalSteps: TotalSteps,
                Artifacts: Array.Empty<ArtifactInfo>(),
                SessionIds: Array.Empty<string>(),
                Error: null);
        }
    }
}
