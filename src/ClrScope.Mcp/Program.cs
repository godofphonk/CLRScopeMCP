using ClrScope.Mcp.CLI;

namespace ClrScope.Mcp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var command = CliCommands.ParseCommand(args);
        return await CliCommands.ExecuteAsync(command, args);
    }
}
