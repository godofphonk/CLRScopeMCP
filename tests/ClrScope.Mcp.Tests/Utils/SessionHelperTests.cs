using ClrScope.Mcp.Domain.Sessions;
using Xunit;

namespace ClrScope.Mcp.Tests.Utils;

public class SessionHelperTests
{
    [Fact]
    public void AsFailed_SetsStatusFailedAndPhaseFailed()
    {
        // Arrange
        var session = new Session(
            SessionId.New(),
            SessionKind.Dump,
            12345,
            SessionStatus.Running,
            DateTime.UtcNow,
            null,
            null,
            null,
            SessionPhase.Attaching
        );

        // Act
        var failedSession = session.AsFailed("test error");

        // Assert
        Assert.Equal(SessionStatus.Failed, failedSession.Status);
        Assert.Equal(SessionPhase.Failed, failedSession.Phase);
        Assert.Equal("test error", failedSession.Error);
    }

    [Fact]
    public void AsFailed_SetsCompletedAtUtc()
    {
        // Arrange
        var session = new Session(
            SessionId.New(),
            SessionKind.Dump,
            12345,
            SessionStatus.Running,
            DateTime.UtcNow,
            null,
            null,
            null,
            SessionPhase.Attaching
        );

        // Act
        var failedSession = session.AsFailed();

        // Assert
        Assert.NotNull(failedSession.CompletedAtUtc);
        Assert.True(failedSession.CompletedAtUtc > DateTime.MinValue);
    }

    [Fact]
    public void AsFailed_PreservesExistingCompletedAtUtc()
    {
        // Arrange
        var existingTimestamp = DateTime.UtcNow.AddMinutes(-5);
        var session = new Session(
            SessionId.New(),
            SessionKind.Dump,
            12345,
            SessionStatus.Running,
            DateTime.UtcNow,
            existingTimestamp,
            null,
            null,
            SessionPhase.Attaching
        );

        // Act
        var failedSession = session.AsFailed("test error");

        // Assert
        Assert.Equal(existingTimestamp, failedSession.CompletedAtUtc);
    }

    [Fact]
    public void AsCancelled_SetsStatusCancelledAndPhaseCancelled()
    {
        // Arrange
        var session = new Session(
            SessionId.New(),
            SessionKind.Trace,
            12345,
            SessionStatus.Running,
            DateTime.UtcNow,
            null,
            null,
            null,
            SessionPhase.Attaching
        );

        // Act
        var cancelledSession = session.AsCancelled();

        // Assert
        Assert.Equal(SessionStatus.Cancelled, cancelledSession.Status);
        Assert.Equal(SessionPhase.Cancelled, cancelledSession.Phase);
    }

    [Fact]
    public void AsCancelled_SetsCompletedAtUtc()
    {
        // Arrange
        var session = new Session(
            SessionId.New(),
            SessionKind.Trace,
            12345,
            SessionStatus.Running,
            DateTime.UtcNow,
            null,
            null,
            null,
            SessionPhase.Attaching
        );

        // Act
        var cancelledSession = session.AsCancelled();

        // Assert
        Assert.NotNull(cancelledSession.CompletedAtUtc);
        Assert.True(cancelledSession.CompletedAtUtc > DateTime.MinValue);
    }

    [Fact]
    public void AsCancelled_PreservesExistingCompletedAtUtc()
    {
        // Arrange
        var existingTimestamp = DateTime.UtcNow.AddMinutes(-5);
        var session = new Session(
            SessionId.New(),
            SessionKind.Trace,
            12345,
            SessionStatus.Running,
            DateTime.UtcNow,
            existingTimestamp,
            null,
            null,
            SessionPhase.Attaching
        );

        // Act
        var cancelledSession = session.AsCancelled();

        // Assert
        Assert.Equal(existingTimestamp, cancelledSession.CompletedAtUtc);
    }

    [Fact]
    public void AsCompleted_SetsStatusCompletedAndPhaseCompleted()
    {
        // Arrange
        var session = new Session(
            SessionId.New(),
            SessionKind.Counters,
            12345,
            SessionStatus.Running,
            DateTime.UtcNow,
            null,
            null,
            null,
            SessionPhase.Attaching
        );

        // Act
        var completedSession = session.AsCompleted();

        // Assert
        Assert.Equal(SessionStatus.Completed, completedSession.Status);
        Assert.Equal(SessionPhase.Completed, completedSession.Phase);
    }

    [Fact]
    public void AsCompleted_SetsCompletedAtUtc()
    {
        // Arrange
        var session = new Session(
            SessionId.New(),
            SessionKind.Counters,
            12345,
            SessionStatus.Running,
            DateTime.UtcNow,
            null,
            null,
            null,
            SessionPhase.Attaching
        );

        // Act
        var completedSession = session.AsCompleted();

        // Assert
        Assert.NotNull(completedSession.CompletedAtUtc);
        Assert.True(completedSession.CompletedAtUtc > DateTime.MinValue);
    }

    [Fact]
    public void AsCompleted_PreservesExistingCompletedAtUtc()
    {
        // Arrange
        var existingTimestamp = DateTime.UtcNow.AddMinutes(-5);
        var session = new Session(
            SessionId.New(),
            SessionKind.Counters,
            12345,
            SessionStatus.Running,
            DateTime.UtcNow,
            existingTimestamp,
            null,
            null,
            SessionPhase.Attaching
        );

        // Act
        var completedSession = session.AsCompleted();

        // Assert
        Assert.Equal(existingTimestamp, completedSession.CompletedAtUtc);
    }

    [Fact]
    public void HelperMethods_DoNotModifyOriginalSession()
    {
        // Arrange
        var session = new Session(
            SessionId.New(),
            SessionKind.Dump,
            12345,
            SessionStatus.Running,
            DateTime.UtcNow,
            null,
            null,
            null,
            SessionPhase.Attaching
        );

        // Act
        var failedSession = session.AsFailed();
        var cancelledSession = session.AsCancelled();
        var completedSession = session.AsCompleted();

        // Assert
        Assert.Equal(SessionStatus.Running, session.Status);
        Assert.Equal(SessionPhase.Attaching, session.Phase);
        Assert.Null(session.CompletedAtUtc);
    }
}
