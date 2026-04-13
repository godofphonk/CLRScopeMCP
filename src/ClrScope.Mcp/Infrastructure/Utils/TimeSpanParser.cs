namespace ClrScope.Mcp.Infrastructure.Utils;

public static class TimeSpanParser
{
    /// <summary>
    /// Parse duration in hh:mm:ss format
    /// </summary>
    public static bool TryParseDuration(string duration, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(duration))
        {
            return false;
        }

        var parts = duration.Split(':');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var hours) || hours < 0)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var minutes) || minutes < 0 || minutes >= 60)
        {
            return false;
        }

        if (!int.TryParse(parts[2], out var seconds) || seconds < 0 || seconds >= 60)
        {
            return false;
        }

        result = new TimeSpan(hours, minutes, seconds);
        return true;
    }

    /// <summary>
    /// Parse duration in hh:mm:ss format, throws FormatException on failure
    /// </summary>
    public static TimeSpan ParseDuration(string duration)
    {
        if (!TryParseDuration(duration, out var result))
        {
            throw new FormatException($"Duration must be in hh:mm:ss format, got: {duration}");
        }

        return result;
    }

    /// <summary>
    /// Parse max age in format like "7d", "24h", "60m", "3600s"
    /// </summary>
    public static bool TryParseMaxAge(string maxAge, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(maxAge))
        {
            return false;
        }

        var trimmed = maxAge.Trim();
        if (trimmed.Length < 2)
        {
            return false;
        }

        var unit = trimmed[^1];
        var valueStr = trimmed[..^1];

        if (!int.TryParse(valueStr, out var value) || value < 0)
        {
            return false;
        }

        result = unit switch
        {
            'd' => TimeSpan.FromDays(value),
            'h' => TimeSpan.FromHours(value),
            'm' => TimeSpan.FromMinutes(value),
            's' => TimeSpan.FromSeconds(value),
            _ => TimeSpan.Zero
        };

        return result != TimeSpan.Zero || unit == 's' && value == 0;
    }

    /// <summary>
    /// Parse max age in format like "7d", "24h", "60m", "3600s", throws FormatException on failure
    /// </summary>
    public static TimeSpan ParseMaxAge(string maxAge)
    {
        if (!TryParseMaxAge(maxAge, out var result))
        {
            throw new FormatException($"Max age must be in format like '7d', '24h', '60m', '3600s', got: {maxAge}");
        }

        return result;
    }
}
