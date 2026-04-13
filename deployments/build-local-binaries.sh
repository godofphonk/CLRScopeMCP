#!/bin/bash
# Build self-contained binaries for all platforms (local development testing)
#
# Usage:
#   ./deployments/build-local-binaries.sh [version]
#
# This script builds self-contained single-file binaries for CLRScope MCP
# across all supported platforms. It's intended for local development testing
# to verify that binaries can be built successfully before pushing to CI/CD.
#
# For actual publishing, use GitHub Actions workflow (.github/workflows/release.yml)

set -e

VERSION=${1:-"0.1.0"}
OUTPUT_DIR="../releases/$VERSION"
PROJECT="../src/ClrScope.Mcp/ClrScope.Mcp.csproj"

echo "Building CLRScope MCP v$VERSION binaries (local development)..."

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
  -o "$OUTPUT_DIR/linux-x64"

# Linux ARM64
echo "Building linux-arm64..."
dotnet publish "$PROJECT" \
  -c Release \
  -r linux-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -o "$OUTPUT_DIR/linux-arm64"

# macOS x64
echo "Building osx-x64..."
dotnet publish "$PROJECT" \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -o "$OUTPUT_DIR/osx-x64"

# macOS ARM64 (Apple Silicon)
echo "Building osx-arm64..."
dotnet publish "$PROJECT" \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -o "$OUTPUT_DIR/osx-arm64"

# Windows x64
echo "Building win-x64..."
dotnet publish "$PROJECT" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -o "$OUTPUT_DIR/win-x64"

# Rename binaries to standard naming
mv "$OUTPUT_DIR/linux-x64/ClrScope.Mcp" "$OUTPUT_DIR/linux-x64/clrscope-mcp"
mv "$OUTPUT_DIR/linux-arm64/ClrScope.Mcp" "$OUTPUT_DIR/linux-arm64/clrscope-mcp"
mv "$OUTPUT_DIR/osx-x64/ClrScope.Mcp" "$OUTPUT_DIR/osx-x64/clrscope-mcp"
mv "$OUTPUT_DIR/osx-arm64/ClrScope.Mcp" "$OUTPUT_DIR/osx-arm64/clrscope-mcp"
mv "$OUTPUT_DIR/win-x64/ClrScope.Mcp.exe" "$OUTPUT_DIR/win-x64/clrscope-mcp.exe"

# Make binaries executable
chmod +x "$OUTPUT_DIR/linux-x64/clrscope-mcp"
chmod +x "$OUTPUT_DIR/linux-arm64/clrscope-mcp"
chmod +x "$OUTPUT_DIR/osx-x64/clrscope-mcp"
chmod +x "$OUTPUT_DIR/osx-arm64/clrscope-mcp"

echo "✅ Built to $OUTPUT_DIR"
echo ""
echo "Binaries ready for local testing:"
echo "  - $OUTPUT_DIR/linux-x64/clrscope-mcp"
echo "  - $OUTPUT_DIR/linux-arm64/clrscope-mcp"
echo "  - $OUTPUT_DIR/osx-x64/clrscope-mcp"
echo "  - $OUTPUT_DIR/osx-arm64/clrscope-mcp"
echo "  - $OUTPUT_DIR/win-x64/clrscope-mcp.exe"
