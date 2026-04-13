using ClrScope.Mcp.Domain.Sessions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ClrScope.Mcp.Infrastructure;

/// <summary>
/// In-memory registry for tracking active operations and supporting cancellation by session ID
/// Implements IDisposable to ensure proper cleanup of CancellationTokenSource resources
/// </summary>
public class ActiveOperationRegistry : IActiveOperationRegistry, IDisposable
{
    private readonly ConcurrentDictionary<SessionId, CancellationTokenSource> _activeOperations = new();
    private readonly ILogger<ActiveOperationRegistry> _logger;
    private bool _disposed;

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
            
            // Remove and dispose to prevent resource retention
            if (_activeOperations.TryRemove(sessionId, out _))
            {
                cts.Dispose();
                _logger.LogDebug("Disposed CancellationTokenSource for cancelled session {SessionId}", sessionId.Value);
            }
            
            return true;
        }

        _logger.LogWarning("Session {SessionId} not found in active operations for cancellation", sessionId.Value);
        return false;
    }

    public void Complete(SessionId sessionId)
    {
        if (_activeOperations.TryRemove(sessionId, out var cts))
        {
            // Dispose the CancellationTokenSource to prevent resource retention
            cts.Dispose();
            _logger.LogDebug("Completed and disposed operation for session {SessionId}", sessionId.Value);
        }
    }

    /// <summary>
    /// Dispose all active CancellationTokenSource instances to prevent resource retention
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing ActiveOperationRegistry with {Count} active operations", _activeOperations.Count);
        
        foreach (var (sessionId, cts) in _activeOperations)
        {
            try
            {
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                    _logger.LogDebug("Cancelled operation during disposal for session {SessionId}", sessionId.Value);
                }
                cts.Dispose();
                _logger.LogDebug("Disposed CancellationTokenSource for session {SessionId}", sessionId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispose CancellationTokenSource for session {SessionId}", sessionId.Value);
            }
        }

        _activeOperations.Clear();
        _disposed = true;
    }
}
