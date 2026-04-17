#!/bin/bash
# Script to generate test data from MemoryPressureApp for benchmarks

set -e

echo "=== Generating test data from MemoryPressureApp for benchmarks ==="

# Build MemoryPressureApp
echo "Building MemoryPressureApp..."
dotnet build test-data/MemoryPressureApp/MemoryPressureApp.csproj -c Release

# Run MemoryPressureApp in background
echo "Starting MemoryPressureApp..."
dotnet run --project test-data/MemoryPressureApp/MemoryPressureApp.csproj -c Release &
MEMORY_APP_PID=$!

# Wait for the app to allocate memory
echo "Waiting for MemoryPressureApp to allocate memory (15 seconds)..."
sleep 15

# Get the actual PID of the MemoryPressureApp
# Note: The app prints its PID, but we can also find it by process name
APP_PID=$(pgrep -f "MemoryPressureApp" | head -n 1)

if [ -z "$APP_PID" ]; then
    echo "Error: Could not find MemoryPressureApp process"
    kill $MEMORY_APP_PID 2>/dev/null || true
    exit 1
fi

echo "MemoryPressureApp PID: $APP_PID"

# Collect GC dump
echo "Collecting GC dump..."
OUTPUT_FILE="test-data/memory-pressure.gcdump"
dotnet-gcdump collect -p $APP_PID -o $OUTPUT_FILE

# Kill the MemoryPressureApp
echo "Stopping MemoryPressureApp..."
kill $MEMORY_APP_PID 2>/dev/null || true

# Copy to benchmark project
echo "Copying GC dump to benchmark project..."
mkdir -p bench/ClrScope.Benchmarks/test-data
cp $OUTPUT_FILE bench/ClrScope.Benchmarks/test-data/memory-pressure.gcdump

echo "=== Test data generated successfully ==="
echo "GC dump saved to: $OUTPUT_FILE"
echo "Benchmark test data: bench/ClrScope.Benchmarks/test-data/memory-pressure.gcdump"
