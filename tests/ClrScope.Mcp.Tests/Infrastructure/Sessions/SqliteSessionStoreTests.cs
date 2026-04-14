using ClrScope.Mcp.Domain.Sessions;
using ClrScope.Mcp.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Sessions;

public class SqliteSessionStoreTests : IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteSessionStore _store;
    private readonly string _dbPath;

    public SqliteSessionStoreTests()
    {
        // Use temporary file-based SQLite database for testing
        _dbPath = Path.Combine(Path.GetTempPath(), $"clrscope_test_{Guid.NewGuid()}.db");
        _connectionString = $"Data Source={_dbPath}";
        _store = new SqliteSessionStore(_connectionString);
        
        // Initialize schema
        var initializer = new SqliteSchemaInitializer(_connectionString);
        initializer.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task CreateAsync_CreatesSessionWithPendingStatus()
    {
        // Arrange
        var kind = SessionKind.Trace;
        var pid = 1234;

        // Act
        var session = await _store.CreateAsync(kind, pid);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(kind, session.Kind);
        Assert.Equal(pid, session.Pid);
        Assert.Equal(SessionStatus.Pending, session.Status);
        Assert.Equal(SessionPhase.Preflight, session.Phase);
        Assert.NotNull(session.SessionId);
        Assert.NotNull(session.CreatedAtUtc);
        Assert.Null(session.CompletedAtUtc);
        Assert.Null(session.Error);
    }

    [Fact]
    public async Task CreateAsync_CreatesSessionWithProfile()
    {
        // Arrange
        var kind = SessionKind.Trace;
        var pid = 1234;
        var profile = "cpu-sampling";

        // Act
        var session = await _store.CreateAsync(kind, pid, profile);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(profile, session.Profile);
    }

    [Fact]
    public async Task CreateAsync_CreatesUniqueSessionIds()
    {
        // Arrange
        var kind = SessionKind.Trace;
        var pid = 1234;

        // Act
        var session1 = await _store.CreateAsync(kind, pid);
        var session2 = await _store.CreateAsync(kind, pid);

        // Assert
        Assert.NotEqual(session1.SessionId, session2.SessionId);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenSessionNotFound()
    {
        // Arrange
        var sessionId = SessionId.New();

        // Act
        var session = await _store.GetAsync(sessionId);

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public async Task GetAsync_ReturnsSession_WhenSessionExists()
    {
        // Arrange
        var kind = SessionKind.Trace;
        var pid = 1234;
        var createdSession = await _store.CreateAsync(kind, pid);

        // Act
        var retrievedSession = await _store.GetAsync(createdSession.SessionId);

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Equal(createdSession.SessionId, retrievedSession.SessionId);
        Assert.Equal(kind, retrievedSession.Kind);
        Assert.Equal(pid, retrievedSession.Pid);
        Assert.Equal(SessionStatus.Pending, retrievedSession.Status);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesSessionStatus()
    {
        // Arrange
        var kind = SessionKind.Trace;
        var pid = 1234;
        var session = await _store.CreateAsync(kind, pid);
        var updatedSession = session with { Status = SessionStatus.Running };

        // Act
        await _store.UpdateAsync(updatedSession);
        var retrievedSession = await _store.GetAsync(session.SessionId);

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Equal(SessionStatus.Running, retrievedSession.Status);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesSessionError()
    {
        // Arrange
        var kind = SessionKind.Trace;
        var pid = 1234;
        var session = await _store.CreateAsync(kind, pid);
        var error = "Test error message";
        var updatedSession = session with { Error = error, Status = SessionStatus.Failed };

        // Act
        await _store.UpdateAsync(updatedSession);
        var retrievedSession = await _store.GetAsync(session.SessionId);

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Equal(error, retrievedSession.Error);
        Assert.Equal(SessionStatus.Failed, retrievedSession.Status);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCompletedAtUtc()
    {
        // Arrange
        var kind = SessionKind.Trace;
        var pid = 1234;
        var session = await _store.CreateAsync(kind, pid);
        var completedAt = DateTime.UtcNow;
        var updatedSession = session with { CompletedAtUtc = completedAt, Status = SessionStatus.Completed };

        // Act
        await _store.UpdateAsync(updatedSession);
        var retrievedSession = await _store.GetAsync(session.SessionId);

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.NotNull(retrievedSession.CompletedAtUtc);
        Assert.Equal(SessionStatus.Completed, retrievedSession.Status);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesSessionPhase()
    {
        // Arrange
        var kind = SessionKind.Trace;
        var pid = 1234;
        var session = await _store.CreateAsync(kind, pid);
        var updatedSession = session with { Phase = SessionPhase.Collecting };

        // Act
        await _store.UpdateAsync(updatedSession);
        var retrievedSession = await _store.GetAsync(session.SessionId);

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Equal(SessionPhase.Collecting, retrievedSession.Phase);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesMultipleFields()
    {
        // Arrange
        var kind = SessionKind.Trace;
        var pid = 1234;
        var session = await _store.CreateAsync(kind, pid);
        var completedAt = DateTime.UtcNow;
        var error = "Test error";
        var updatedSession = session with 
        { 
            Status = SessionStatus.Failed, 
            CompletedAtUtc = completedAt, 
            Error = error,
            Phase = SessionPhase.Completed
        };

        // Act
        await _store.UpdateAsync(updatedSession);
        var retrievedSession = await _store.GetAsync(session.SessionId);

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Equal(SessionStatus.Failed, retrievedSession.Status);
        Assert.NotNull(retrievedSession.CompletedAtUtc);
        Assert.Equal(error, retrievedSession.Error);
        Assert.Equal(SessionPhase.Completed, retrievedSession.Phase);
    }

    [Fact]
    public async Task GetAsync_PreservesCreatedAtUtc()
    {
        // Arrange
        var kind = SessionKind.Trace;
        var pid = 1234;
        var createdSession = await _store.CreateAsync(kind, pid);

        // Act
        var retrievedSession = await _store.GetAsync(createdSession.SessionId);

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Equal(createdSession.CreatedAtUtc, retrievedSession.CreatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_HandlesDifferentSessionKinds()
    {
        // Arrange & Act
        var traceSession = await _store.CreateAsync(SessionKind.Trace, 1234);
        var dumpSession = await _store.CreateAsync(SessionKind.Dump, 5678);
        var countersSession = await _store.CreateAsync(SessionKind.Counters, 9012);
        var gcDumpSession = await _store.CreateAsync(SessionKind.GcDump, 3456);

        // Assert
        Assert.Equal(SessionKind.Trace, traceSession.Kind);
        Assert.Equal(SessionKind.Dump, dumpSession.Kind);
        Assert.Equal(SessionKind.Counters, countersSession.Kind);
        Assert.Equal(SessionKind.GcDump, gcDumpSession.Kind);
    }

    [Fact]
    public async Task CreateAsync_StoresPidCorrectly()
    {
        // Arrange
        var pid = 9999;

        // Act
        var session = await _store.CreateAsync(SessionKind.Trace, pid);

        // Assert
        Assert.Equal(pid, session.Pid);
        var retrievedSession = await _store.GetAsync(session.SessionId);
        Assert.Equal(pid, retrievedSession.Pid);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotModifyImmutableFields()
    {
        // Arrange
        var kind = SessionKind.Trace;
        var pid = 1234;
        var session = await _store.CreateAsync(kind, pid);
        var originalSessionId = session.SessionId;
        var originalCreatedAt = session.CreatedAtUtc;
        var originalKind = session.Kind;
        var originalPid = session.Pid;
        var originalProfile = session.Profile;

        var updatedSession = session with { Status = SessionStatus.Running };

        // Act
        await _store.UpdateAsync(updatedSession);
        var retrievedSession = await _store.GetAsync(session.SessionId);

        // Assert
        Assert.Equal(originalSessionId, retrievedSession.SessionId);
        Assert.Equal(originalCreatedAt, retrievedSession.CreatedAtUtc);
        Assert.Equal(originalKind, retrievedSession.Kind);
        Assert.Equal(originalPid, retrievedSession.Pid);
        Assert.Equal(originalProfile, retrievedSession.Profile);
    }
}
