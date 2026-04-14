using ClrScope.Mcp.Domain.Sessions;
using Microsoft.Data.Sqlite;

namespace ClrScope.Mcp.Infrastructure;

public class SqliteSessionStore : ISqliteSessionStore
{
    private readonly string _connectionString;

    public SqliteSessionStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Session?> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        return await RetryAsync(async () =>
        {
            await using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                SELECT session_id, kind, pid, status, created_at_utc, completed_at_utc, error, profile, phase, incident_id
                FROM sessions
                WHERE session_id = $sessionId
                """;
            command.Parameters.AddWithValue("$sessionId", sessionId.Value);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapFromReader(reader);
            }

            return null;
        }, cancellationToken);
    }

    private static async Task<T> RetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await operation();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6) // SQLITE_BUSY or SQLITE_LOCKED
            {
                if (i == maxRetries - 1)
                    throw;

                await Task.Delay(TimeSpan.FromMilliseconds(100 * (i + 1)), cancellationToken);
            }
        }
        throw new InvalidOperationException("Retry failed");
    }

    private static async Task RetryAsync(Func<Task> operation, CancellationToken cancellationToken, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await operation();
                return;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6) // SQLITE_BUSY or SQLITE_LOCKED
            {
                if (i == maxRetries - 1)
                    throw;

                await Task.Delay(TimeSpan.FromMilliseconds(100 * (i + 1)), cancellationToken);
            }
        }
        throw new InvalidOperationException("Retry failed");
    }

    public async Task<Session> CreateAsync(SessionKind kind, int pid, string? profile = null, CancellationToken cancellationToken = default)
    {
        return await CreateAsync(kind, pid, profile, null, cancellationToken);
    }

    public async Task<Session> CreateAsync(SessionKind kind, int pid, string? profile, string? incidentId, CancellationToken cancellationToken = default)
    {
        return await RetryAsync(async () =>
        {
            var sessionId = SessionId.New();
            var now = DateTime.UtcNow;

            await using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO sessions (session_id, kind, pid, status, created_at_utc, completed_at_utc, error, profile, phase, incident_id)
                VALUES ($sessionId, $kind, $pid, $status, $createdAtUtc, $completedAtUtc, $error, $profile, $phase, $incidentId)
                """;
            command.Parameters.AddWithValue("$sessionId", sessionId.Value);
            command.Parameters.AddWithValue("$kind", kind.ToString());
            command.Parameters.AddWithValue("$pid", pid);
            command.Parameters.AddWithValue("$status", SessionStatus.Pending.ToString());
            command.Parameters.AddWithValue("$createdAtUtc", now.ToString("o"));
            command.Parameters.AddWithValue("$completedAtUtc", DBNull.Value);
            command.Parameters.AddWithValue("$error", DBNull.Value);
            command.Parameters.AddWithValue("$profile", profile ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$phase", SessionPhase.Preflight.ToString());
            command.Parameters.AddWithValue("$incidentId", incidentId ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);

            return new Session(
                sessionId,
                kind,
                pid,
                SessionStatus.Pending,
                now,
                null,
                null,
                profile,
                SessionPhase.Preflight,
                incidentId
            );
        }, cancellationToken);
    }

    public async Task UpdateAsync(Session session, CancellationToken cancellationToken = default)
    {
        await RetryAsync(async () =>
        {
            await using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE sessions
                SET status = $status,
                    completed_at_utc = $completedAtUtc,
                    error = $error,
                    phase = $phase
                WHERE session_id = $sessionId
                """;
            command.Parameters.AddWithValue("$status", session.Status.ToString());
            command.Parameters.AddWithValue("$completedAtUtc", session.CompletedAtUtc?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$error", session.Error ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$phase", session.Phase.ToString());
            command.Parameters.AddWithValue("$sessionId", session.SessionId.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<Session>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await RetryAsync(async () =>
        {
            await using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                SELECT session_id, kind, pid, status, created_at_utc, completed_at_utc, error, profile, phase, incident_id
                FROM sessions
                ORDER BY created_at_utc DESC
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var sessions = new List<Session>();

            while (await reader.ReadAsync(cancellationToken))
            {
                sessions.Add(MapFromReader(reader));
            }

            return sessions.AsReadOnly();
        }, cancellationToken);
    }

    private static Session MapFromReader(SqliteDataReader reader)
    {
        return new Session(
            new SessionId(reader.GetString(0)),
            Enum.Parse<SessionKind>(reader.GetString(1)),
            reader.GetInt32(2),
            Enum.Parse<SessionStatus>(reader.GetString(3)),
            DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? SessionPhase.Preflight : Enum.Parse<SessionPhase>(reader.GetString(8)),
            reader.FieldCount > 9 && !reader.IsDBNull(9) ? reader.GetString(9) : null
        );
    }
}
