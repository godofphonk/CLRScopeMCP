using ClrScope.Mcp.DependencyInjection;
using ModelContextProtocol.Server;
using System.Reflection;
using Xunit;

namespace ClrScope.Mcp.Tests.Integration;

public class McpToolsRegistrationTests
{
    private static Assembly GetClrScopeAssembly()
    {
        return Assembly.GetAssembly(typeof(ClrScopeServiceCollectionExtensions))!;
    }

    private static Type[] GetToolTypes(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .ToArray();
    }

    [Fact]
    public void RegisteredToolTypes_Matches_Assembly_Tool_Types()
    {
        // Arrange
        var assembly = GetClrScopeAssembly();
        var registeredToolTypes = ClrScopeServiceCollectionExtensions.RegisteredToolTypes;
        var assemblyToolTypes = GetToolTypes(assembly);

        // Act & Assert
        // Verify that all types in RegisteredToolTypes have [McpServerToolType] attribute
        foreach (var registeredType in registeredToolTypes)
        {
            var attr = registeredType.GetCustomAttribute<McpServerToolTypeAttribute>();
            Assert.True(attr != null,
                $"Type {registeredType.Name} in RegisteredToolTypes doesn't have [McpServerToolType] attribute");
        }

        // Verify that all types with [McpServerToolType] in assembly are in RegisteredToolTypes
        var registeredSet = registeredToolTypes.ToHashSet();
        var missingTypes = assemblyToolTypes
            .Where(t => !registeredSet.Contains(t))
            .Select(t => t.Name)
            .ToList();

        Assert.True(missingTypes.Count == 0,
            $"Types with [McpServerToolType] not in RegisteredToolTypes: {string.Join(", ", missingTypes)}");
    }

    [Fact]
    public void All_Registered_Tool_Types_Have_Tool_Methods()
    {
        // Arrange
        var registeredToolTypes = ClrScopeServiceCollectionExtensions.RegisteredToolTypes;

        // Act & Assert
        // Verify each registered tool type has at least one [McpServerTool] method
        foreach (var toolType in registeredToolTypes)
        {
            var methods = toolType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
                .ToList();

            Assert.True(methods.Count > 0,
                $"Tool type {toolType.Name} has no [McpServerTool] methods");
        }
    }
}
