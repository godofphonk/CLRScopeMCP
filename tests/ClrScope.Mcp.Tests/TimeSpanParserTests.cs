using ClrScope.Mcp.Infrastructure.Utils;
using Xunit;

namespace ClrScope.Mcp.Tests;

public class TimeSpanParserTests
{
    // TryParseDuration tests (hh:mm:ss format)

    [Fact]
    public void TryParseDuration_ValidFormat_ReturnsTrue()
    {
        // Act
        var result = TimeSpanParser.TryParseDuration("01:30:45", out var timeSpan);

        // Assert
        Assert.True(result);
        Assert.Equal(new TimeSpan(1, 30, 45), timeSpan);
    }

    [Fact]
    public void TryParseDuration_ZeroValues_ReturnsTrue()
    {
        // Act
        var result = TimeSpanParser.TryParseDuration("00:00:00", out var timeSpan);

        // Assert
        Assert.True(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseDuration_EmptyString_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseDuration("", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseDuration_WhitespaceString_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseDuration("   ", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseDuration_InvalidPartsCount_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseDuration("01:30", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseDuration_NegativeHours_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseDuration("-01:30:45", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseDuration_NegativeMinutes_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseDuration("01:-30:45", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseDuration_MinutesOutOfRange_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseDuration("01:60:45", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseDuration_SecondsOutOfRange_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseDuration("01:30:60", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseDuration_NonNumericParts_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseDuration("ab:cd:ef", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void ParseDuration_ValidFormat_ReturnsTimeSpan()
    {
        // Act
        var result = TimeSpanParser.ParseDuration("01:30:45");

        // Assert
        Assert.Equal(new TimeSpan(1, 30, 45), result);
    }

    [Fact]
    public void ParseDuration_InvalidFormat_ThrowsFormatException()
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => TimeSpanParser.ParseDuration("invalid"));
    }

    // TryParseMaxAge tests (7d, 24h, 60m, 3600s format)

    [Fact]
    public void TryParseMaxAge_DaysFormat_ReturnsTrue()
    {
        // Act
        var result = TimeSpanParser.TryParseMaxAge("7d", out var timeSpan);

        // Assert
        Assert.True(result);
        Assert.Equal(TimeSpan.FromDays(7), timeSpan);
    }

    [Fact]
    public void TryParseMaxAge_HoursFormat_ReturnsTrue()
    {
        // Act
        var result = TimeSpanParser.TryParseMaxAge("24h", out var timeSpan);

        // Assert
        Assert.True(result);
        Assert.Equal(TimeSpan.FromHours(24), timeSpan);
    }

    [Fact]
    public void TryParseMaxAge_MinutesFormat_ReturnsTrue()
    {
        // Act
        var result = TimeSpanParser.TryParseMaxAge("60m", out var timeSpan);

        // Assert
        Assert.True(result);
        Assert.Equal(TimeSpan.FromMinutes(60), timeSpan);
    }

    [Fact]
    public void TryParseMaxAge_SecondsFormat_ReturnsTrue()
    {
        // Act
        var result = TimeSpanParser.TryParseMaxAge("3600s", out var timeSpan);

        // Assert
        Assert.True(result);
        Assert.Equal(TimeSpan.FromSeconds(3600), timeSpan);
    }

    [Fact]
    public void TryParseMaxAge_ZeroSeconds_ReturnsTrue()
    {
        // Act
        var result = TimeSpanParser.TryParseMaxAge("0s", out var timeSpan);

        // Assert
        Assert.True(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseMaxAge_EmptyString_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseMaxAge("", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseMaxAge_WhitespaceString_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseMaxAge("   ", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseMaxAge_TooShortString_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseMaxAge("1", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseMaxAge_NegativeValue_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseMaxAge("-7d", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseMaxAge_InvalidUnit_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseMaxAge("7x", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseMaxAge_NonNumericValue_ReturnsFalse()
    {
        // Act
        var result = TimeSpanParser.TryParseMaxAge("ab", out var timeSpan);

        // Assert
        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void TryParseMaxAge_WithWhitespace_ReturnsTrue()
    {
        // Act
        var result = TimeSpanParser.TryParseMaxAge(" 7d ", out var timeSpan);

        // Assert
        Assert.True(result);
        Assert.Equal(TimeSpan.FromDays(7), timeSpan);
    }

    [Fact]
    public void ParseMaxAge_ValidFormat_ReturnsTimeSpan()
    {
        // Act
        var result = TimeSpanParser.ParseMaxAge("7d");

        // Assert
        Assert.Equal(TimeSpan.FromDays(7), result);
    }

    [Fact]
    public void ParseMaxAge_InvalidFormat_ThrowsFormatException()
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => TimeSpanParser.ParseMaxAge("invalid"));
    }
}
