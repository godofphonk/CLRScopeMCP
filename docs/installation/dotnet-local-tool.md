# Local .NET Tool Installation

Install ClrScope.Mcp as a local tool in a specific project directory. This is useful for project-specific tooling or when you don't want system-wide installation.

## Installation

### 1. Navigate to your project directory

```bash
cd /path/to/your/project
```

### 2. Initialize tool manifest

```bash
dotnet new tool-manifest
```

This creates a `.config/dotnet-tools.json` file in your project directory.

### 3. Install the tool

```bash
dotnet tool install ClrScope.Mcp
```

The tool is now available only in this directory and its subdirectories.

## Verification

```bash
dotnet tool run clrscope-mcp --version
```

Output:
```
CLRScope MCP v1.0.0
```

Alternative invocation:
```bash
dotnet clrscope-mcp --version
```

## Usage

### CLI Mode

```bash
dotnet tool run clrscope-mcp --help
dotnet clrscope-mcp --demo
```

### MCP Server Mode

Configure in your IDE's MCP settings (e.g., VS Code `settings.json`):

```json
{
  "mcpServers": {
    "clrscope": {
      "command": "dotnet",
      "args": ["clrscope-mcp"]
    }
  }
}
```

Note: The working directory for the MCP server must be the project directory where the tool is installed.

## Uninstallation

```bash
dotnet tool uninstall ClrScope.Mcp
```

## Advantages

- Project-specific installation
- No system-wide PATH changes
- Version isolation per project
- Easy to include in project setup scripts

## Requirements

- .NET SDK 10.0 or later
- Internet connection for initial download

## When to Use

- Project-specific diagnostic workflows
- CI/CD pipelines with isolated environments
- When you need different tool versions per project
- To avoid system-wide installation
