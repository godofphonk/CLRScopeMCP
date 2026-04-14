#!/bin/bash
# Build LINUX x64 binary for local development testing
#
# Usage:
#   ./deployments/build-local-binaries.sh [version]
#
# This script builds a self-contained single-file binary for CLRScope MCP
# on Linux x64 only. It's intended for local development testing.
#
# For actual publishing, use GitHub Actions workflow (.github/workflows/release.yml)

set -e

VERSION=${1:-"0.1.0"}
OUTPUT_DIR="../DEV/releases/$VERSION"
PROJECT="../src/ClrScope.Mcp/ClrScope.Mcp.csproj"

echo "Building CLRScope MCP v$VERSION (Linux x64 only)..."

# Clean and create output directory
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Linux x64
echo "Building linux-x64..."
dotnet publish "$PROJECT" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:DebugSymbols=false \
  -p:DebugType=None \
  -o "$OUTPUT_DIR"

# Rename binary to standard naming
mv "$OUTPUT_DIR/ClrScope.Mcp" "$OUTPUT_DIR/clrscope-mcp"

# Make binary executable
chmod +x "$OUTPUT_DIR/clrscope-mcp"

echo "✅ Built to $OUTPUT_DIR"
echo ""
echo "Binary ready for local testing:"
echo "  - $OUTPUT_DIR/clrscope-mcp"
