namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// Provides correlation IDs for tracing operations across logs
/// </summary>
public interface ICorrelationIdProvider
{
    string GetCorrelationId();
    string GenerateCorrelationId();
}

public class CorrelationIdProvider : ICorrelationIdProvider
{
    private readonly AsyncLocal<string> _currentCorrelationId = new();

    public string GetCorrelationId()
    {
        return _currentCorrelationId.Value ?? GenerateCorrelationId();
    }

    public string GenerateCorrelationId()
    {
        var correlationId = Guid.NewGuid().ToString("N")[..16];
        _currentCorrelationId.Value = correlationId;
        return correlationId;
    }

    public void SetCorrelationId(string correlationId)
    {
        _currentCorrelationId.Value = correlationId;
    }
}
