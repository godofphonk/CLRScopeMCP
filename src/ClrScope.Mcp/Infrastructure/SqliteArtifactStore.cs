using ClrScope.Mcp.Domain;
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
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT artifact_id, kind, status, file_path, diag_uri, file_uri, sha256, size_bytes, pid, session_id, created_at_utc
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
    }

    public async Task<IReadOnlyList<Artifact>> GetBySessionAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT artifact_id, kind, status, file_path, diag_uri, file_uri, sha256, size_bytes, pid, session_id, created_at_utc
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
    }

    public async Task<Artifact> CreateAsync(
        ArtifactKind kind,
        string filePath,
        long sizeBytes,
        int pid,
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        var artifactId = ArtifactId.New();
        var sha256 = await ComputeSha256Async(filePath, cancellationToken);
        var now = DateTime.UtcNow;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO artifacts (artifact_id, kind, status, file_path, diag_uri, file_uri, sha256, size_bytes, pid, session_id, created_at_utc)
            VALUES ($artifactId, $kind, $status, $filePath, $diagUri, $fileUri, $sha256, $sizeBytes, $pid, $sessionId, $createdAtUtc)
            """;
        command.Parameters.AddWithValue("$artifactId", artifactId.Value);
        command.Parameters.AddWithValue("$kind", kind.ToString());
        command.Parameters.AddWithValue("$status", ArtifactStatus.Pending.ToString());
        command.Parameters.AddWithValue("$filePath", filePath);
        command.Parameters.AddWithValue("$diagUri", DBNull.Value);
        command.Parameters.AddWithValue("$fileUri", DBNull.Value);
        command.Parameters.AddWithValue("$sha256", sha256);
        command.Parameters.AddWithValue("$sizeBytes", sizeBytes);
        command.Parameters.AddWithValue("$pid", pid);
        command.Parameters.AddWithValue("$sessionId", sessionId.Value);
        command.Parameters.AddWithValue("$createdAtUtc", now.ToString("o"));

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
            now
        );
    }

    public async Task<IReadOnlyList<Artifact>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT artifact_id, kind, status, file_path, diag_uri, file_uri, sha256, size_bytes, pid, session_id, created_at_utc
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
    }

    public async Task DeleteAsync(ArtifactId artifactId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM artifacts WHERE artifact_id = $artifactId
            """;
        command.Parameters.AddWithValue("$artifactId", artifactId.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Artifact artifact, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE artifacts
            SET status = $status,
                diag_uri = $diagUri,
                file_uri = $fileUri
            WHERE artifact_id = $artifactId
            """;
        command.Parameters.AddWithValue("$status", artifact.Status.ToString());
        command.Parameters.AddWithValue("$diagUri", artifact.DiagUri ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$fileUri", artifact.FileUri ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$artifactId", artifact.ArtifactId.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Artifact MapFromReader(SqliteDataReader reader)
    {
        return new Artifact(
            new ArtifactId(reader.GetString(0)),
            Enum.Parse<ArtifactKind>(reader.GetString(1)),
            Enum.Parse<ArtifactStatus>(reader.GetString(2)),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetInt64(7),
            reader.GetInt32(8),
            new SessionId(reader.GetString(9)),
            DateTime.Parse(reader.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind)
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
