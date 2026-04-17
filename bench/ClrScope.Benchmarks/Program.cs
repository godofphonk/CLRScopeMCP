using BenchmarkDotNet.Running;
using ClrScope.Benchmarks.HeapAnalysis;

// Run heap analysis benchmarks
BenchmarkRunner.Run<HeapAnalysisBenchmarks>();
