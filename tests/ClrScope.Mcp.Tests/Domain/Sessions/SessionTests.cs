using ClrScope.Mcp.Domain.Sessions;
using Xunit;

namespace ClrScope.Mcp.Tests.Domain.Sessions;

public class SessionTests
{
    [Fact]
    public void Session_CreatesWithValidParameters()
    {
        // Arrange
        var sessionId = new SessionId("session-id");
        var kind = SessionKind.Dump;
        var pid = 1234;
        var status = SessionStatus.Running;
        var createdAtUtc = DateTime.UtcNow;

        // Act
        var session = new Session(
            sessionId,
            kind,
            pid,
            status,
            createdAtUtc,
            null, // CompletedAtUtc
            null, // Error
            null  // Profile
        );

        // Assert
        Assert.Equal(sessionId, session.SessionId);
        Assert.Equal(kind, session.Kind);
        Assert.Equal(pid, session.Pid);
        Assert.Equal(status, session.Status);
        Assert.Equal(createdAtUtc, session.CreatedAtUtc);
        Assert.Null(session.CompletedAtUtc);
        Assert.Null(session.Error);
        Assert.Null(session.Profile);
        Assert.Equal(SessionPhase.Preflight, session.Phase);
        Assert.Null(session.IncidentId);
    }

    [Fact]
    public void Session_CreatesWithOptionalParameters()
    {
        // Arrange
        var sessionId = new SessionId("session-id");
        var kind = SessionKind.Dump;
        var pid = 1234;
        var status = SessionStatus.Running;
        var createdAtUtc = DateTime.UtcNow;
        var completedAtUtc = DateTime.UtcNow.AddMinutes(5);
        var error = "Test error";
        var profile = "cpu-sampling";
        var phase = SessionPhase.Completed;
        var incidentId = "incident-123";

        // Act
        var session = new Session(
            sessionId,
            kind,
            pid,
            status,
            createdAtUtc,
            completedAtUtc,
            error,
            profile,
            phase,
            incidentId
        );

        // Assert
        Assert.Equal(sessionId, session.SessionId);
        Assert.Equal(kind, session.Kind);
        Assert.Equal(pid, session.Pid);
        Assert.Equal(status, session.Status);
        Assert.Equal(createdAtUtc, session.CreatedAtUtc);
        Assert.Equal(completedAtUtc, session.CompletedAtUtc);
        Assert.Equal(error, session.Error);
        Assert.Equal(profile, session.Profile);
        Assert.Equal(phase, session.Phase);
        Assert.Equal(incidentId, session.IncidentId);
    }

    [Fact]
    public void AsFailed_SetsStatusToFailed()
    {
        // Arrange
        var session = new Session(
            new SessionId("session-id"),
            SessionKind.Dump,
            1234,
            SessionStatus.Running,
            DateTime.UtcNow,
            null,
            null,
            null
        );

        // Act
        var failedSession = session.AsFailed("Test error");

        // Assert
        Assert.Equal(SessionStatus.Failed, failedSession.Status);
        Assert.Equal(SessionPhase.Failed, failedSession.Phase);
        Assert.Equal("Test error", failedSession.Error);
        Assert.NotNull(failedSession.CompletedAtUtc);
    }

    [Fact]
    public void AsFailed_SetsCompletedAtUtcWhenNull()
    {
        // Arrange
        var session = new Session(
            new SessionId("session-id"),
            SessionKind.Dump,
            1234,
            SessionStatus.Running,
            DateTime.UtcNow,
            null, // CompletedAtUtc
            null,
            null
        );

        // Act
        var failedSession = session.AsFailed();

        // Assert
        Assert.NotNull(failedSession.CompletedAtUtc);
    }

    [Fact]
    public void AsFailed_PreservesExistingCompletedAtUtc()
    {
        // Arrange
        var existingCompletedAt = DateTime.UtcNow.AddMinutes(-5);
        var session = new Session(
            new SessionId("session-id"),
            SessionKind.Dump,
            1234,
            SessionStatus.Running,
            DateTime.UtcNow,
            existingCompletedAt,
            null,
            null
        );

        // Act
        var failedSession = session.AsFailed();

        // Assert
        Assert.Equal(existingCompletedAt, failedSession.CompletedAtUtc);
    }

    [Fact]
    public void AsCancelled_SetsStatusToCancelled()
    {
        // Arrange
        var session = new Session(
            new SessionId("session-id"),
            SessionKind.Dump,
            1234,
            SessionStatus.Running,
            DateTime.UtcNow,
            null,
            null,
            null
        );

        // Act
        var cancelledSession = session.AsCancelled();

        // Assert
        Assert.Equal(SessionStatus.Cancelled, cancelledSession.Status);
        Assert.Equal(SessionPhase.Cancelled, cancelledSession.Phase);
        Assert.NotNull(cancelledSession.CompletedAtUtc);
    }

    [Fact]
    public void AsCancelled_SetsCompletedAtUtcWhenNull()
    {
        // Arrange
        var session = new Session(
            new SessionId("session-id"),
            SessionKind.Dump,
            1234,
            SessionStatus.Running,
            DateTime.UtcNow,
            null, // CompletedAtUtc
            null,
            null
        );

        // Act
        var cancelledSession = session.AsCancelled();

        // Assert
        Assert.NotNull(cancelledSession.CompletedAtUtc);
    }

    [Fact]
    public void AsCancelled_PreservesExistingCompletedAtUtc()
    {
        // Arrange
        var existingCompletedAt = DateTime.UtcNow.AddMinutes(-5);
        var session = new Session(
            new SessionId("session-id"),
            SessionKind.Dump,
            1234,
            SessionStatus.Running,
            DateTime.UtcNow,
            existingCompletedAt,
            null,
            null
        );

        // Act
        var cancelledSession = session.AsCancelled();

        // Assert
        Assert.Equal(existingCompletedAt, cancelledSession.CompletedAtUtc);
    }

    [Fact]
    public void AsCompleted_SetsStatusToCompleted()
    {
        // Arrange
        var session = new Session(
            new SessionId("session-id"),
            SessionKind.Dump,
            1234,
            SessionStatus.Running,
            DateTime.UtcNow,
            null,
            null,
            null
        );

        // Act
        var completedSession = session.AsCompleted();

        // Assert
        Assert.Equal(SessionStatus.Completed, completedSession.Status);
        Assert.Equal(SessionPhase.Completed, completedSession.Phase);
        Assert.NotNull(completedSession.CompletedAtUtc);
    }

    [Fact]
    public void AsCompleted_SetsCompletedAtUtcWhenNull()
    {
        // Arrange
        var session = new Session(
            new SessionId("session-id"),
            SessionKind.Dump,
            1234,
            SessionStatus.Running,
            DateTime.UtcNow,
            null, // CompletedAtUtc
            null,
            null
        );

        // Act
        var completedSession = session.AsCompleted();

        // Assert
        Assert.NotNull(completedSession.CompletedAtUtc);
    }

    [Fact]
    public void AsCompleted_PreservesExistingCompletedAtUtc()
    {
        // Arrange
        var existingCompletedAt = DateTime.UtcNow.AddMinutes(-5);
        var session = new Session(
            new SessionId("session-id"),
            SessionKind.Dump,
            1234,
            SessionStatus.Running,
            DateTime.UtcNow,
            existingCompletedAt,
            null,
            null
        );

        // Act
        var completedSession = session.AsCompleted();

        // Assert
        Assert.Equal(existingCompletedAt, completedSession.CompletedAtUtc);
    }
}
