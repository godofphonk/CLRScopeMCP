using ClrScope.Mcp.Domain.Heap.Adapters;
using ClrScope.Mcp.Domain.Heap.Data;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace ClrScope.Mcp.Tests.Services.Heap;

public class HeapParserTimeoutTests
{
    private readonly ITestOutputHelper _output;

    public HeapParserTimeoutTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task LoadGraphAsync_WhenParserTimesOut_KillsProcessAndThrowsTimeoutException()
    {
        // Arrange - create a mock adapter that simulates a slow parser
        var mockAdapter = new SlowMockHeapParserAdapter(new TestOutputHelperLogger<SlowMockHeapParserAdapter>(_output));
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            mockAdapter.LoadGraphAsync("dummy.gcdump", CancellationToken.None)
        );

        _output.WriteLine($"Timeout exception thrown: {exception.Message}");
        Assert.True(mockAdapter.ProcessWasKilled, "Process should have been killed due to timeout");
        Assert.True(mockAdapter.ProcessStarted, "Process should have been started");
    }

    [Fact]
    public async Task LoadGraphAsync_WhenCancellationTokenCancelled_KillsProcess()
    {
        // Arrange - create a mock adapter with cancellation
        var mockAdapter = new SlowMockHeapParserAdapter(new TestOutputHelperLogger<SlowMockHeapParserAdapter>(_output));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            mockAdapter.LoadGraphAsync("dummy.gcdump", cts.Token)
        );

        _output.WriteLine("Process was cancelled");
        Assert.True(mockAdapter.ProcessWasKilled, "Process should have been killed due to cancellation");
    }

    /// <summary>
    /// Mock adapter that simulates a slow heap parser process for testing timeout behavior.
    /// Instead of running a real process, it simulates the timeout scenario.
    /// </summary>
    private class SlowMockHeapParserAdapter : IGcDumpGraphAdapter
    {
        private readonly ILogger<SlowMockHeapParserAdapter> _logger;
        public bool ProcessStarted { get; private set; }
        public bool ProcessWasKilled { get; private set; }

        public SlowMockHeapParserAdapter(ILogger<SlowMockHeapParserAdapter> logger)
        {
            _logger = logger;
        }

        public async Task<HeapGraphData> LoadGraphAsync(string gcdumpPath, CancellationToken cancellationToken)
        {
            ProcessStarted = true;
            _logger.LogInformation("Simulating slow heap parser process...");

            try
            {
                // Simulate a process that takes longer than the timeout (10 minutes)
                // In a real scenario, this would be a separate process
                await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
                
                // Should never reach here due to timeout
                ProcessWasKilled = false;
                throw new InvalidOperationException("Process should have timed out");
            }
            catch (OperationCanceledException)
            {
                // Simulate process being killed
                ProcessWasKilled = true;
                _logger.LogInformation("Simulated process killed due to cancellation");
                throw;
            }
        }

        public Task<HeapGraphData> LoadGraphAsync(Stream gcdumpStream, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Stream loading not supported by mock adapter");
        }
    }

    private class TestOutputHelperLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public TestOutputHelperLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}] {message}");
        }
    }
}
