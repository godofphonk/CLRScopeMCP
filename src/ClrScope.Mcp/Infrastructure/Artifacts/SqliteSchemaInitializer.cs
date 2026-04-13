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

        // Enable WAL mode for better concurrency
        await EnableWalModeAsync(connection, cancellationToken);
        
        // Set busy timeout to 5 seconds
        await SetBusyTimeoutAsync(connection, cancellationToken);

        await CreateSchemaInfoTableAsync(connection, cancellationToken);
        await RunMigrationsAsync(connection, cancellationToken);
    }

    private static async Task EnableWalModeAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SetBusyTimeoutAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout=5000;";
        await command.ExecuteNonQueryAsync(cancellationToken);
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
        // Use transaction to ensure atomic migration batch
        using var transaction = connection.BeginTransaction();

        try
        {
            var currentVersion = await GetCurrentVersionAsync(connection, cancellationToken);

            if (currentVersion >= 1)
            {
                if (currentVersion < 2)
                {
                    await Migration002_AddSessionPhaseAsync(connection, cancellationToken);
                }
                if (currentVersion < 3)
                {
                    await Migration003_AddArtifactPinnedAsync(connection, cancellationToken);
                }
            }
            else
            {
                await Migration001_CreateSessionsAndArtifactsAsync(connection, cancellationToken);
                await Migration002_AddSessionPhaseAsync(connection, cancellationToken);
                await Migration003_AddArtifactPinnedAsync(connection, cancellationToken);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(" + tableName + ")";

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(1); // Column name is at index 1
            if (name == columnName)
            {
                return true;
            }
        }
        return false;
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

    private static async Task Migration002_AddSessionPhaseAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        // Check if column already exists to avoid duplicate column error
        if (await ColumnExistsAsync(connection, "sessions", "phase", cancellationToken))
        {
            // Column already exists, just update version
            var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = "INSERT OR IGNORE INTO schema_info (version, applied_at_utc) VALUES (2, datetime('now'))";
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        var command = connection.CreateCommand();
        command.CommandText = """
            ALTER TABLE sessions ADD COLUMN phase TEXT NOT NULL DEFAULT 'Preflight';
            INSERT INTO schema_info (version, applied_at_utc) VALUES (2, datetime('now'));
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task Migration003_AddArtifactPinnedAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        // Check if column already exists to avoid duplicate column error
        if (await ColumnExistsAsync(connection, "artifacts", "pinned", cancellationToken))
        {
            // Column already exists, just update version
            var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = "INSERT OR IGNORE INTO schema_info (version, applied_at_utc) VALUES (3, datetime('now'))";
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        var command = connection.CreateCommand();
        command.CommandText = """
            ALTER TABLE artifacts ADD COLUMN pinned INTEGER NOT NULL DEFAULT 0;
            INSERT INTO schema_info (version, applied_at_utc) VALUES (3, datetime('now'));
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
