using ClrScope.Mcp.Contracts;
using Xunit;

namespace ClrScope.Mcp.Tests.Contracts;

public class ClrScopeErrorTests
{
    [Fact]
    public void ClrScopeError_HasValidationInvalidPid()
    {
        // Act & Assert
        Assert.Equal(0, (int)ClrScopeError.VALIDATION_INVALID_PID);
    }

    [Fact]
    public void ClrScopeError_HasValidationInvalidDuration()
    {
        // Act & Assert
        Assert.Equal(1, (int)ClrScopeError.VALIDATION_INVALID_DURATION);
    }

    [Fact]
    public void ClrScopeError_HasValidationInvalidProfile()
    {
        // Act & Assert
        Assert.Equal(2, (int)ClrScopeError.VALIDATION_INVALID_PROFILE);
    }

    [Fact]
    public void ClrScopeError_HasValidationMissingRequiredField()
    {
        // Act & Assert
        Assert.Equal(3, (int)ClrScopeError.VALIDATION_MISSING_REQUIRED_FIELD);
    }

    [Fact]
    public void ClrScopeError_HasPreflightProcessNotFound()
    {
        // Act & Assert
        Assert.Equal(4, (int)ClrScopeError.PREFLIGHT_PROCESS_NOT_FOUND);
    }

    [Fact]
    public void ClrScopeError_HasPreflightNotDotnet()
    {
        // Act & Assert
        Assert.Equal(5, (int)ClrScopeError.PREFLIGHT_NOT_DOTNET);
    }

    [Fact]
    public void ClrScopeError_HasPreflightDiagnosticsDisabled()
    {
        // Act & Assert
        Assert.Equal(6, (int)ClrScopeError.PREFLIGHT_DIAGNOSTICS_DISABLED);
    }

    [Fact]
    public void ClrScopeError_HasPreflightArtifactRootNotWritable()
    {
        // Act & Assert
        Assert.Equal(7, (int)ClrScopeError.PREFLIGHT_ARTIFACT_ROOT_NOT_WRITABLE);
    }

    [Fact]
    public void ClrScopeError_HasPreflightDiskSpaceLow()
    {
        // Act & Assert
        Assert.Equal(8, (int)ClrScopeError.PREFLIGHT_DISK_SPACE_LOW);
    }

    [Fact]
    public void ClrScopeError_HasRuntimeProcessExited()
    {
        // Act & Assert
        Assert.Equal(9, (int)ClrScopeError.RUNTIME_PROCESS_EXITED);
    }

    [Fact]
    public void ClrScopeError_HasRuntimeAttachFailed()
    {
        // Act & Assert
        Assert.Equal(10, (int)ClrScopeError.RUNTIME_ATTACH_FAILED);
    }

    [Fact]
    public void ClrScopeError_HasRuntimeCollectionFailed()
    {
        // Act & Assert
        Assert.Equal(11, (int)ClrScopeError.RUNTIME_COLLECTION_FAILED);
    }

    [Fact]
    public void ClrScopeError_HasRuntimeCancellationFailed()
    {
        // Act & Assert
        Assert.Equal(12, (int)ClrScopeError.RUNTIME_CANCELLATION_FAILED);
    }

    [Fact]
    public void ClrScopeError_HasStorageSessionNotFound()
    {
        // Act & Assert
        Assert.Equal(13, (int)ClrScopeError.STORAGE_SESSION_NOT_FOUND);
    }

    [Fact]
    public void ClrScopeError_HasStorageArtifactNotFound()
    {
        // Act & Assert
        Assert.Equal(14, (int)ClrScopeError.STORAGE_ARTIFACT_NOT_FOUND);
    }

    [Fact]
    public void ClrScopeError_HasStorageDatabaseError()
    {
        // Act & Assert
        Assert.Equal(15, (int)ClrScopeError.STORAGE_DATABASE_ERROR);
    }

    [Fact]
    public void ClrScopeError_HasStorageFileIoError()
    {
        // Act & Assert
        Assert.Equal(16, (int)ClrScopeError.STORAGE_FILE_IO_ERROR);
    }

    [Fact]
    public void ClrScopeError_HasSeventeenValues()
    {
        // Act & Assert
        Assert.Equal(17, Enum.GetValues<ClrScopeError>().Length);
    }

    [Fact]
    public void ClrScopeError_AllValuesAreDistinct()
    {
        // Arrange
        var values = Enum.GetValues<ClrScopeError>();

        // Act & Assert
        Assert.Equal(values.Length, values.Cast<ClrScopeError>().Distinct().Count());
    }
}
