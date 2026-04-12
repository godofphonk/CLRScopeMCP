using ClrScope.Mcp.Domain;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// In-memory registry for tracking active operations and supporting cancellation by session ID
/// </summary>
public class ActiveOperationRegistry : IActiveOperationRegistry
{
    private readonly ConcurrentDictionary<SessionId, CancellationTokenSource> _activeOperations = new();
    private readonly ILogger<ActiveOperationRegistry> _logger;

    public ActiveOperationRegistry(ILogger<ActiveOperationRegistry> logger)
    {
        _logger = logger;
    }

    public bool TryRegister(SessionId sessionId, CancellationTokenSource cts)
    {
        if (_activeOperations.TryAdd(sessionId, cts))
        {
            _logger.LogDebug("Registered active operation for session {SessionId}", sessionId.Value);
            return true;
        }

        _logger.LogWarning("Session {SessionId} already registered in active operations", sessionId.Value);
        return false;
    }

    public bool TryCancel(SessionId sessionId, string reason)
    {
        if (_activeOperations.TryGetValue(sessionId, out var cts))
        {
            _logger.LogInformation("Cancelling operation for session {SessionId}: {Reason}", sessionId.Value, reason);
            cts.Cancel();
            return true;
        }

        _logger.LogWarning("Session {SessionId} not found in active operations for cancellation", sessionId.Value);
        return false;
    }

    public void Complete(SessionId sessionId)
    {
        if (_activeOperations.TryRemove(sessionId, out _))
        {
            _logger.LogDebug("Completed operation for session {SessionId}", sessionId.Value);
        }
    }
}
