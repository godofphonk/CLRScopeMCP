using ClrScope.Mcp.Tools.Sessions;
using Xunit;

namespace ClrScope.Mcp.Tests.Infrastructure.Tools.Sessions;

public class SessionToolsTests
{
    #region GetSession Tests

    [Fact]
    public async Task GetSession_ReturnsError_WhenSessionIdIsEmpty()
    {
        // Arrange
        var sessionId = "";

        // Act
        var result = await SessionTools.GetSession(sessionId, null!);

        // Assert
        Assert.False(result.Found);
        Assert.Equal("Session ID must not be empty", result.Error);
    }

    [Fact]
    public async Task GetSession_ReturnsError_WhenSessionIdIsWhitespace()
    {
        // Arrange
        var sessionId = "   ";

        // Act
        var result = await SessionTools.GetSession(sessionId, null!);

        // Assert
        Assert.False(result.Found);
        Assert.Equal("Session ID must not be empty", result.Error);
    }

    [Fact]
    public async Task GetSession_ReturnsError_WhenSessionIdIsNull()
    {
        // Arrange
        string sessionId = null!;

        // Act
        var result = await SessionTools.GetSession(sessionId, null!);

        // Assert
        Assert.False(result.Found);
        Assert.Equal("Session ID must not be empty", result.Error);
    }

    #endregion

    #region CancelSession Tests

    [Fact]
    public async Task CancelSession_ReturnsError_WhenSessionIdIsEmpty()
    {
        // Arrange
        var sessionId = "";

        // Act
        var result = await SessionTools.CancelSession(sessionId, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Session ID must not be empty", result.Message);
    }

    [Fact]
    public async Task CancelSession_ReturnsError_WhenSessionIdIsWhitespace()
    {
        // Arrange
        var sessionId = "   ";

        // Act
        var result = await SessionTools.CancelSession(sessionId, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Session ID must not be empty", result.Message);
    }

    [Fact]
    public async Task CancelSession_ReturnsError_WhenSessionIdIsNull()
    {
        // Arrange
        string sessionId = null!;

        // Act
        var result = await SessionTools.CancelSession(sessionId, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Session ID must not be empty", result.Message);
    }

    #endregion
}
