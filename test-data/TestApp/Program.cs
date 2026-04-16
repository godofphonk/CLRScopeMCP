using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Test process started. PID: " + Environment.ProcessId);
        Console.WriteLine("Press Ctrl+C to exit...");
        
        // Create multiple threads with deep call stacks
        var threads = new List<Thread>();
        for (int i = 0; i < 5; i++)
        {
            var threadId = i;
            var thread = new Thread(() => WorkerThread(threadId));
            thread.Start();
            threads.Add(thread);
        }
        
        // Keep main thread working
        while (true)
        {
            MainThreadWork();
            Thread.Sleep(100);
        }
    }
    
    static void MainThreadWork()
    {
        var sum = 0;
        for (int i = 0; i < 100000; i++)
        {
            sum += Calculate(i);
        }
    }
    
    static int Calculate(int n)
    {
        return RecursiveCalc(n, 10);
    }
    
    static int RecursiveCalc(int n, int depth)
    {
        if (depth <= 0) return n;
        return RecursiveCalc(n + depth, depth - 1) + n;
    }
    
    static void WorkerThread(int threadId)
    {
        while (true)
        {
            ThreadWork1(threadId);
            ThreadWork2(threadId);
            ThreadWork3(threadId);
        }
    }
    
    static void ThreadWork1(int threadId)
    {
        var sum = 0;
        for (int i = 0; i < 50000; i++)
        {
            sum += i % 100;
        }
    }
    
    static void ThreadWork2(int threadId)
    {
        var list = new List<int>();
        for (int i = 0; i < 1000; i++)
        {
            list.Add(i);
        }
        ProcessList(list);
    }
    
    static void ThreadWork3(int threadId)
    {
        RecursiveWork(threadId, 5);
    }
    
    static void ProcessList(List<int> list)
    {
        var sum = 0;
        foreach (var item in list)
        {
            sum += item * 2;
        }
    }
    
    static void RecursiveWork(int n, int depth)
    {
        if (depth <= 0) return;
        var sum = 0;
        for (int i = 0; i < 10000; i++)
        {
            sum += i;
        }
        RecursiveWork(n + depth, depth - 1);
    }
}
