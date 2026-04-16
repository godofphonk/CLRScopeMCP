#!/bin/bash
# Build LINUX x64 binary for local development testing
#
# Usage:
#   ./deployments/dev/build-local-binaries.sh [version]
#
# This script builds a framework-dependent single-file binary for CLRScope MCP
# on Linux x64 only. It's intended for local development testing.
#
# For actual publishing, use GitHub Actions workflow (.github/workflows/release.yml)

set -e

VERSION=${1:-"0.1.0"}
OUTPUT_DIR="../DEV/releases/$VERSION"
PROJECT="../../src/ClrScope.Mcp/ClrScope.Mcp.csproj"
HEAP_PARSER_PROJECT="../../src/ClrScope.HeapParser/ClrScope.HeapParser.csproj"

echo "Building CLRScope MCP v$VERSION (Linux x64 only)..."

# Clean and create output directory
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build HeapParser as a dependency
echo "Building HeapParser..."
dotnet build "$HEAP_PARSER_PROJECT" -c Release -r linux-x64 --self-contained false

# Linux x64
echo "Building linux-x64..."
dotnet publish "$PROJECT" \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -p:DebugSymbols=false \
  -p:DebugType=None \
  -o "$OUTPUT_DIR"

# Copy HeapParser to output directory
echo "Copying HeapParser to output..."
HEAP_PARSER_BUILD_DIR="../../src/ClrScope.HeapParser/bin/Release/net10.0/linux-x64"
cp "$HEAP_PARSER_BUILD_DIR/ClrScope.HeapParser.dll" "$OUTPUT_DIR/"
cp "$HEAP_PARSER_BUILD_DIR/ClrScope.HeapParser.runtimeconfig.json" "$OUTPUT_DIR/"
cp "$HEAP_PARSER_BUILD_DIR/ClrScope.HeapParser.deps.json" "$OUTPUT_DIR/"

# Copy HeapParser dependencies (DotNetHeapDump, etc.)
cp "$HEAP_PARSER_BUILD_DIR/ClrScope.ThirdParty.DotNetHeapDump.dll" "$OUTPUT_DIR/" 2>/dev/null || true
cp "$HEAP_PARSER_BUILD_DIR/Microsoft.Diagnostics.FastSerialization.dll" "$OUTPUT_DIR/" 2>/dev/null || true

# Rename binary to standard naming
mv "$OUTPUT_DIR/ClrScope.Mcp" "$OUTPUT_DIR/clrscope-mcp"

# Make binary executable
chmod +x "$OUTPUT_DIR/clrscope-mcp"

echo "✅ Built to $OUTPUT_DIR"
echo ""
echo "Binary ready for local testing:"
echo "  - $OUTPUT_DIR/clrscope-mcp"
echo "Note: Requires .NET 10.0 runtime to be installed on the target system"
