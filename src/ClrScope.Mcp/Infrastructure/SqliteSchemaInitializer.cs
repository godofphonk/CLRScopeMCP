using Microsoft.Data.Sqlite;

namespace ClrScope.Mcp.Infrastructure;

public class SqliteSchemaInitializer
{
    private readonly string _connectionString;

    public SqliteSchemaInitializer(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await CreateSchemaInfoTableAsync(connection, cancellationToken);
        await RunMigrationsAsync(connection, cancellationToken);
    }

    private static async Task CreateSchemaInfoTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_info (
                version INTEGER NOT NULL PRIMARY KEY,
                applied_at_utc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RunMigrationsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var currentVersion = await GetCurrentVersionAsync(connection, cancellationToken);

        if (currentVersion >= 1)
        {
            return;
        }

        await Migration001_CreateSessionsAndArtifactsAsync(connection, cancellationToken);
    }

    private static async Task<int> GetCurrentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(version) FROM schema_info";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result == null || result == DBNull.Value)
        {
            return 0;
        }
        return Convert.ToInt32(result);
    }

    private static async Task Migration001_CreateSessionsAndArtifactsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                session_id TEXT NOT NULL PRIMARY KEY,
                kind TEXT NOT NULL,
                pid INTEGER NOT NULL,
                status TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                completed_at_utc TEXT,
                error TEXT,
                profile TEXT
            );

            CREATE TABLE IF NOT EXISTS artifacts (
                artifact_id TEXT NOT NULL PRIMARY KEY,
                kind TEXT NOT NULL,
                status TEXT NOT NULL,
                file_path TEXT NOT NULL,
                diag_uri TEXT NOT NULL,
                file_uri TEXT NOT NULL,
                sha256 TEXT NOT NULL,
                size_bytes INTEGER NOT NULL,
                pid INTEGER NOT NULL,
                session_id TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(session_id)
            );

            CREATE INDEX IF NOT EXISTS idx_artifacts_session ON artifacts(session_id);
            CREATE INDEX IF NOT EXISTS idx_artifacts_status ON artifacts(status);
            CREATE INDEX IF NOT EXISTS idx_artifacts_kind ON artifacts(kind);

            INSERT INTO schema_info (version, applied_at_utc) VALUES (1, datetime('now'));
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
