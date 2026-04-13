using ClrScope.Mcp.Domain.Artifacts;
using ClrScope.Mcp.Domain.Sessions;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace ClrScope.Mcp.Infrastructure;

public class SqliteArtifactStore : ISqliteArtifactStore
{
    private readonly string _connectionString;

    public SqliteArtifactStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Artifact?> GetAsync(ArtifactId artifactId, CancellationToken cancellationToken = default)
    {
        return await RetryAsync(async () =>
        {
            await using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                SELECT artifact_id, kind, status, file_path, diag_uri, file_uri, sha256, size_bytes, pid, session_id, created_at_utc, pinned
                FROM artifacts
                WHERE artifact_id = $artifactId
                """;
            command.Parameters.AddWithValue("$artifactId", artifactId.Value);

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

    public async Task<IReadOnlyList<Artifact>> GetBySessionAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        return await RetryAsync(async () =>
        {
            await using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                SELECT artifact_id, kind, status, file_path, diag_uri, file_uri, sha256, size_bytes, pid, session_id, created_at_utc, pinned
                FROM artifacts
                WHERE session_id = $sessionId
                """;
            command.Parameters.AddWithValue("$sessionId", sessionId.Value);

            var artifacts = new List<Artifact>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                artifacts.Add(MapFromReader(reader));
            }

            return artifacts;
        }, cancellationToken);
    }

    public async Task<Artifact> CreateAsync(
        ArtifactKind kind,
        string filePath,
        long sizeBytes,
        int pid,
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        return await RetryAsync(async () =>
        {
            var artifactId = ArtifactId.New();
            var fileInfo = new FileInfo(filePath);
            
            // Skip SHA-256 for large files (>100MB) to avoid double disk pass and delayed completion
            var sha256 = fileInfo.Length > 100 * 1024 * 1024 ? "skipped_for_large_file" : await ComputeSha256Async(filePath, cancellationToken);
            var now = DateTime.UtcNow;

            await using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO artifacts (artifact_id, kind, status, file_path, diag_uri, file_uri, sha256, size_bytes, pid, session_id, created_at_utc, pinned)
                VALUES ($artifactId, $kind, $status, $filePath, $diagUri, $fileUri, $sha256, $sizeBytes, $pid, $sessionId, $createdAtUtc, $pinned)
                """;
            command.Parameters.AddWithValue("$artifactId", artifactId.Value);
            command.Parameters.AddWithValue("$kind", kind.ToString());
            command.Parameters.AddWithValue("$status", ArtifactStatus.Pending.ToString());
            command.Parameters.AddWithValue("$filePath", filePath);
            command.Parameters.AddWithValue("$diagUri", string.Empty);
            command.Parameters.AddWithValue("$fileUri", string.Empty);
            command.Parameters.AddWithValue("$sha256", sha256);
            command.Parameters.AddWithValue("$sizeBytes", sizeBytes);
            command.Parameters.AddWithValue("$pid", pid);
            command.Parameters.AddWithValue("$sessionId", sessionId.Value);
            command.Parameters.AddWithValue("$createdAtUtc", now.ToString("o"));
            command.Parameters.AddWithValue("$pinned", 0);

            await command.ExecuteNonQueryAsync(cancellationToken);

            return new Artifact(
                artifactId,
                kind,
                ArtifactStatus.Pending,
                filePath,
                null,
                null,
                sha256,
                sizeBytes,
                pid,
                sessionId,
                now,
                false
            );
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<Artifact>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await RetryAsync(async () =>
        {
            await using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                SELECT artifact_id, kind, status, file_path, diag_uri, file_uri, sha256, size_bytes, pid, session_id, created_at_utc, pinned
                FROM artifacts
                ORDER BY created_at_utc DESC
                """;

            var artifacts = new List<Artifact>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            while (await reader.ReadAsync(cancellationToken))
            {
                artifacts.Add(MapFromReader(reader));
            }

            return artifacts;
        }, cancellationToken);
    }

    public async Task DeleteAsync(ArtifactId artifactId, CancellationToken cancellationToken = default)
    {
        await RetryAsync(async () =>
        {
            await using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM artifacts WHERE artifact_id = $artifactId
                """;
            command.Parameters.AddWithValue("$artifactId", artifactId.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task UpdateAsync(Artifact artifact, CancellationToken cancellationToken = default)
    {
        await RetryAsync(async () =>
        {
            await using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE artifacts
                SET status = $status,
                    diag_uri = $diagUri,
                    file_uri = $fileUri,
                    pinned = $pinned
                WHERE artifact_id = $artifactId
                """;
            command.Parameters.AddWithValue("$status", artifact.Status.ToString());
            command.Parameters.AddWithValue("$diagUri", artifact.DiagUri ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$fileUri", artifact.FileUri ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$pinned", artifact.Pinned ? 1 : 0);
            command.Parameters.AddWithValue("$artifactId", artifact.ArtifactId.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    private static Artifact MapFromReader(SqliteDataReader reader)
    {
        return new Artifact(
            new ArtifactId(reader.GetString(0)),
            Enum.Parse<ArtifactKind>(reader.GetString(1)),
            Enum.Parse<ArtifactStatus>(reader.GetString(2)),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6),
            reader.GetInt64(7),
            reader.GetInt32(8),
            new SessionId(reader.GetString(9)),
            DateTime.Parse(reader.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.IsDBNull(11) ? false : reader.GetInt32(11) == 1
        );
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
