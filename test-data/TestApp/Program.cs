using System;
using System.Collections.Generic;
using System.Threading;

namespace TestApp;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Test process started. PID: " + Environment.ProcessId);
        Console.WriteLine("Press Ctrl+C to exit...");
        
        // Create some objects to ensure heap has data
        var objects = new List<object>();
        for (int i = 0; i < 1000; i++)
        {
            objects.Add(new TestObject { Id = i, Name = $"Object_{i}" });
        }
        
        // Keep process running
        while (true)
        {
            Thread.Sleep(1000);
            // Periodically create more objects to keep heap alive
            if (objects.Count < 2000)
            {
                for (int i = 0; i < 10; i++)
                {
                    objects.Add(new TestObject { Id = objects.Count, Name = $"Object_{objects.Count}" });
                }
            }
        }
    }
}

class TestObject
{
    public int Id { get; set; }
    public string Name { get; set; }
    public byte[] Data { get; set; } = new byte[1024];
}
