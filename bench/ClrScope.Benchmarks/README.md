# CLRScope Benchmarks

This project contains performance benchmarks for heap-analysis operations.

## Running Benchmarks

### Run all benchmarks

```bash
dotnet run -c Release --project bench/ClrScope.Benchmarks/ClrScope.Benchmarks.csproj
```

### Run specific benchmark category

```bash
dotnet run -c Release --project bench/ClrScope.Benchmarks/ClrScope.Benchmarks.csproj --filter "*Load*"
dotnet run -c Release --project bench/ClrScope.Benchmarks/ClrScope.Benchmarks.csproj --filter "*Analysis*"
dotnet run -c Release --project bench/ClrScope.Benchmarks/ClrScope.Benchmarks.csproj --filter "*Memory*"
```

## Benchmark Categories

- **Load**: Loading GC dump files
- **Analysis**: Heap analysis operations (retained size calculation, retainer paths, top types/objects)
- **Memory**: Memory operations (graph cloning)
- **Scalability**: Multiple calculations for stress testing

## Test Data

The benchmarks use test GC dump files located in `test-data/`:
- `test-data.gcdump` - Default test data for benchmarks

## Generating Test Data from MemoryPressureApp

To generate realistic test data with MemoryPressureApp:

1. Build and run MemoryPressureApp:

```bash
dotnet run --project test-data/MemoryPressureApp/MemoryPressureApp.csproj
```

2. Note the PID displayed by the application (e.g., "PID: 12345")

3. In another terminal, collect a GC dump:

```bash
dotnet-gcdump collect -p <PID> -o test-data/memory-pressure.gcdump
```

4. Copy the generated `.gcdump` file to the benchmark project:

```bash
cp test-data/memory-pressure.gcdump bench/ClrScope.Benchmarks/test-data/memory-pressure.gcdump
```

5. Update the benchmark code to use the new test data file.

## MemoryPressureApp

MemoryPressureApp is a test application that allocates >100MB of heap memory for realistic benchmarking scenarios. It creates:
- Large objects (10MB chunks, 15 chunks = 150MB)
- Many small objects (1,000,000 objects)

This provides a realistic heap structure for benchmarking heap-analysis operations.
