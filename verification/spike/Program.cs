using Microsoft.Data.Sqlite;

namespace TransactionSpike;

class Program
{
    static async Task Main(string[] args)
    {
        var dbPath = "/tmp/test_transaction.db";
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        Console.WriteLine("=== PC14: Transactional Integrity Verification ===");
        Console.WriteLine();

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        // Create tables
        var createCmd = connection.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                session_id TEXT PRIMARY KEY,
                status TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS artifacts (
                artifact_id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                status TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(session_id)
            );
            """;
        await createCmd.ExecuteNonQueryAsync();

        Console.WriteLine("[PC14] Test 1: Successful transaction");
        await TestSuccessfulTransaction(connection);
        Console.WriteLine();

        Console.WriteLine("[PC14] Test 2: Failed transaction (rollback)");
        await TestFailedTransaction(connection);
        Console.WriteLine();

        Console.WriteLine("[PC14] Verification Complete");
    }

    static async Task TestSuccessfulTransaction(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        try
        {
            // Insert session
            var insertSession = connection.CreateCommand();
            insertSession.CommandText = "INSERT INTO sessions (session_id, status) VALUES ('ses_001', 'Completed')";
            await insertSession.ExecuteNonQueryAsync();

            // Insert artifact
            var insertArtifact = connection.CreateCommand();
            insertArtifact.CommandText = "INSERT INTO artifacts (artifact_id, session_id, status) VALUES ('art_001', 'ses_001', 'Completed')";
            await insertArtifact.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            Console.WriteLine("  SUCCESS: Transaction committed");

            // Verify both records exist
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM sessions WHERE session_id = 'ses_001'";
            var sessionCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            checkCmd.CommandText = "SELECT COUNT(*) FROM artifacts WHERE artifact_id = 'art_001'";
            var artifactCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            Console.WriteLine($"  Session records: {sessionCount}, Artifact records: {artifactCount}");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"  FAILED: {ex.Message}");
        }
    }

    static async Task TestFailedTransaction(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        try
        {
            // Insert session
            var insertSession = connection.CreateCommand();
            insertSession.CommandText = "INSERT INTO sessions (session_id, status) VALUES ('ses_002', 'Completed')";
            await insertSession.ExecuteNonQueryAsync();

            // Insert artifact
            var insertArtifact = connection.CreateCommand();
            insertArtifact.CommandText = "INSERT INTO artifacts (artifact_id, session_id, status) VALUES ('art_002', 'ses_002', 'Completed')";
            await insertArtifact.ExecuteNonQueryAsync();

            // Intentionally fail with duplicate key
            var failCmd = connection.CreateCommand();
            failCmd.CommandText = "INSERT INTO sessions (session_id, status) VALUES ('ses_002', 'Failed')";
            await failCmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            Console.WriteLine("  UNEXPECTED: Transaction should have failed");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"  SUCCESS: Transaction rolled back as expected: {ex.Message}");

            // Verify no orphaned records
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM sessions WHERE session_id = 'ses_002'";
            var sessionCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            checkCmd.CommandText = "SELECT COUNT(*) FROM artifacts WHERE artifact_id = 'art_002'";
            var artifactCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            Console.WriteLine($"  Session records: {sessionCount}, Artifact records: {artifactCount}");

            if (sessionCount == 0 && artifactCount == 0)
            {
                Console.WriteLine("  SUCCESS: No orphaned data after rollback");
            }
            else
            {
                Console.WriteLine("  FAILED: Orphaned data detected");
            }
        }
    }
}
