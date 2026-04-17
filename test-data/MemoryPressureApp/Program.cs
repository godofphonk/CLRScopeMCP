using System;
using System.Collections.Generic;
using System.Threading;

namespace MemoryPressureApp;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Memory Pressure Test Application");
        Console.WriteLine("PID: " + Environment.ProcessId);
        Console.WriteLine("This application allocates >100MB of heap memory for testing");
        Console.WriteLine("Press Ctrl+C to exit...");
        
        // Allocate large objects to create >100MB heap
        var largeObjects = new List<byte[]>();
        var targetSizeMB = 150;
        var chunkSize = 10 * 1024 * 1024; // 10MB chunks
        var chunksNeeded = targetSizeMB * 1024 * 1024 / chunkSize;
        
        Console.WriteLine($"Allocating {targetSizeMB}MB in {chunksNeeded} chunks of {chunkSize / 1024 / 1024}MB each...");
        
        for (int i = 0; i < chunksNeeded; i++)
        {
            var chunk = new byte[chunkSize];
            // Fill with some data to ensure it's allocated
            for (int j = 0; j < chunkSize; j += 4096)
            {
                chunk[j] = (byte)(i % 256);
            }
            largeObjects.Add(chunk);
            
            if ((i + 1) % 10 == 0)
            {
                Console.WriteLine($"Allocated {(i + 1) * chunkSize / 1024 / 1024}MB...");
            }
        }
        
        Console.WriteLine($"Total allocated: {largeObjects.Count * chunkSize / 1024 / 1024}MB");
        Console.WriteLine("Memory allocated. Keeping references alive...");
        
        // Also create many smaller objects to increase object count
        var smallObjects = new List<object>();
        Console.WriteLine("Creating many small objects to increase object count...");
        
        for (int i = 0; i < 1000000; i++)
        {
            smallObjects.Add(new object());
            
            if ((i + 1) % 100000 == 0)
            {
                Console.WriteLine($"Created {i + 1} small objects...");
            }
        }
        
        Console.WriteLine($"Total objects: {largeObjects.Count + smallObjects.Count:N0}");
        Console.WriteLine("Memory pressure test ready. Waiting for heap dump collection...");
        
        // Keep references alive
        while (true)
        {
            Thread.Sleep(1000);
            // Occasionally touch the data to keep it alive
            if (largeObjects.Count > 0)
            {
                var _ = largeObjects[0][0];
            }
        }
    }
}
