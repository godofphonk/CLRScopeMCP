using ClrScope.Mcp.Contracts;
using Xunit;

namespace ClrScope.Mcp.Tests.Contracts;

public class PreflightResultTests
{
    [Fact]
    public void PreflightResult_CreatesWithValidParameters()
    {
        // Act
        var result = new PreflightResult(
            IsValid: true,
            Error: null,
            Message: null
        );

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.Error);
        Assert.Null(result.Message);
    }

    [Fact]
    public void PreflightResult_CreatesWithErrorAndMessage()
    {
        // Arrange
        var error = ClrScopeError.PREFLIGHT_PROCESS_NOT_FOUND;

        // Act
        var result = new PreflightResult(
            IsValid: false,
            Error: error,
            Message: "Process not found"
        );

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Equal("Process not found", result.Message);
    }

    [Fact]
    public void PreflightResult_Success_CreatesWithDefaultValues()
    {
        // Act
        var result = PreflightResult.Success();

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.Error);
        Assert.Null(result.Message);
    }

    [Fact]
    public void PreflightResult_Failure_CreatesWithErrorAndMessage()
    {
        // Arrange
        var error = ClrScopeError.PREFLIGHT_DIAGNOSTICS_DISABLED;

        // Act
        var result = PreflightResult.Failure(error, "Diagnostics are disabled");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Equal("Diagnostics are disabled", result.Message);
    }

    [Fact]
    public void PreflightResult_Failure_WithValidationError()
    {
        // Arrange
        var error = ClrScopeError.VALIDATION_INVALID_PID;

        // Act
        var result = PreflightResult.Failure(error, "PID must be greater than 0");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Equal("PID must be greater than 0", result.Message);
    }

    [Fact]
    public void PreflightResult_Failure_WithRuntimeError()
    {
        // Arrange
        var error = ClrScopeError.RUNTIME_ATTACH_FAILED;

        // Act
        var result = PreflightResult.Failure(error, "Failed to attach to process");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Equal("Failed to attach to process", result.Message);
    }

    [Fact]
    public void PreflightResult_Failure_WithStorageError()
    {
        // Arrange
        var error = ClrScopeError.STORAGE_SESSION_NOT_FOUND;

        // Act
        var result = PreflightResult.Failure(error, "Session not found");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Equal("Session not found", result.Message);
    }
}
