using System.Diagnostics;

namespace TestTargetApp;

class Program
{
    static async Task Main(string[] args)
    {
        var cpuLoad = args.Contains("--cpu-load");
        var memPressure = args.Contains("--mem-pressure");
        var durationArg = args.FirstOrDefault(a => a.StartsWith("--duration="));
        int? durationSeconds = null;

        if (durationArg != null)
        {
            if (int.TryParse(durationArg.Substring("--duration=".Length), out var duration))
            {
                durationSeconds = duration;
            }
        }

        Console.WriteLine($"TestTargetApp started (PID: {Environment.ProcessId})");
        Console.WriteLine($"CPU Load: {cpuLoad}, Memory Pressure: {memPressure}");
        if (durationSeconds.HasValue)
        {
            Console.WriteLine($"Auto-termination in {durationSeconds} seconds");
        }
        Console.WriteLine("Press Ctrl+C to exit cleanly");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("\nCtrl+C received, shutting down gracefully...");
            cts.Cancel();
            e.Cancel = true;
        };

        var heartbeatTask = HeartbeatLoopAsync(cts.Token);
        var workloadTask = cpuLoad ? CpuWorkloadAsync(cts.Token) : Task.CompletedTask;
        var memTask = memPressure ? MemoryPressureAsync(cts.Token) : Task.CompletedTask;

        if (durationSeconds.HasValue)
        {
            await Task.WhenAny(
                Task.Delay(TimeSpan.FromSeconds(durationSeconds.Value), cts.Token),
                heartbeatTask,
                workloadTask,
                memTask
            );
            Console.WriteLine("Duration elapsed, shutting down...");
            cts.Cancel();
        }

        await Task.WhenAll(heartbeatTask, workloadTask, memTask);
        Console.WriteLine("TestTargetApp exited cleanly");
    }

    static async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Heartbeat - PID: {Environment.ProcessId}");
            try
            {
                await Task.Delay(2000, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    static async Task CpuWorkloadAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Simple CPU-bound work
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 100 && !cancellationToken.IsCancellationRequested)
            {
                Math.Sqrt(Math.PI);
            }
            try
            {
                await Task.Delay(50, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    static async Task MemoryPressureAsync(CancellationToken cancellationToken)
    {
        var allocations = new List<byte[]>();
        var random = new Random();

        while (!cancellationToken.IsCancellationRequested)
        {
            // Allocate 10MB on LOH
            var buffer = new byte[10 * 1024 * 1024];
            random.NextBytes(buffer);
            allocations.Add(buffer);

            Console.WriteLine($"Allocated {allocations.Count * 10}MB total");

            try
            {
                await Task.Delay(5000, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
