using ClrScope.Mcp.Infrastructure;
using Xunit;

namespace ClrScope.Mcp.Tests;

public class SqliteSchemaInitializerTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _connectionString;
    private readonly SqliteSchemaInitializer _initializer;

    public SqliteSchemaInitializerTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"clrscope_test_{Guid.NewGuid()}.db");
        _connectionString = $"Data Source={_tempDbPath}";
        _initializer = new SqliteSchemaInitializer(_connectionString);
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            File.Delete(_tempDbPath);
        }
    }

    [Fact]
    public async Task InitializeAsync_CreatesFreshSchema()
    {
        // Act
        await _initializer.InitializeAsync(CancellationToken.None);

        // Assert - database file should exist
        Assert.True(File.Exists(_tempDbPath));
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        // Act - initialize twice
        await _initializer.InitializeAsync(CancellationToken.None);
        await _initializer.InitializeAsync(CancellationToken.None);

        // Assert - should not throw, second init should be idempotent
        Assert.True(File.Exists(_tempDbPath));
    }

    [Fact]
    public async Task InitializeAsync_EnablesForeignKeys()
    {
        // Act
        await _initializer.InitializeAsync(CancellationToken.None);

        // Assert - verify foreign keys are enabled by checking pragma
        using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, CancellationToken.None);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys";
        var result = await command.ExecuteScalarAsync(CancellationToken.None);

        Assert.Equal(1L, result);
    }

    [Fact]
    public async Task InitializeAsync_CreatesSessionsTable()
    {
        // Act
        await _initializer.InitializeAsync(CancellationToken.None);

        // Assert - check sessions table exists
        using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, CancellationToken.None);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='sessions'";
        var result = await command.ExecuteScalarAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("sessions", result);
    }

    [Fact]
    public async Task InitializeAsync_CreatesArtifactsTable()
    {
        // Act
        await _initializer.InitializeAsync(CancellationToken.None);

        // Assert - check artifacts table exists
        using var connection = await SqliteSchemaInitializer.OpenConnectionWithForeignKeysAsync(_connectionString, CancellationToken.None);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='artifacts'";
        var result = await command.ExecuteScalarAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("artifacts", result);
    }
}
