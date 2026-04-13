using ClrScope.Mcp.Domain.Sessions;
using Xunit;

namespace ClrScope.Mcp.Tests.Domain;

public class SessionIdTests
{
    [Fact]
    public void New_ReturnsSessionIdWithPrefix()
    {
        // Act
        var sessionId = SessionId.New();

        // Assert
        Assert.StartsWith("ses_", sessionId.Value);
    }

    [Fact]
    public void New_ReturnsDifferentIds()
    {
        // Act
        var id1 = SessionId.New();
        var id2 = SessionId.New();

        // Assert
        Assert.NotEqual(id1.Value, id2.Value);
    }

    [Fact]
    public void Constructor_AcceptsValidString()
    {
        // Arrange
        var value = "session_test123";

        // Act
        var sessionId = new SessionId(value);

        // Assert
        Assert.Equal(value, sessionId.Value);
    }

    [Fact]
    public void Value_ReturnsOriginalString()
    {
        // Arrange
        var value = "session_abc123";
        var sessionId = new SessionId(value);

        // Act
        var result = sessionId.Value;

        // Assert
        Assert.Equal(value, result);
    }
}
