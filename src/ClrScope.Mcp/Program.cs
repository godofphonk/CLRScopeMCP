using ClrScope.Mcp.CLI;

namespace ClrScope.Mcp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        return await CommandLineParser.ParseAndInvokeAsync(args);
    }
}
