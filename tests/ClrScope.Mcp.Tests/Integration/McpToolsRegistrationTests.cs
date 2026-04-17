using ClrScope.Mcp.DependencyInjection;
using ModelContextProtocol.Server;
using System.Reflection;
using Xunit;

namespace ClrScope.Mcp.Tests.Integration;

public class McpToolsRegistrationTests
{
    [Fact]
    public void All_Tool_Classes_With_McpServerToolType_Are_Registered_WithTools()
    {
        // Arrange
        var assembly = Assembly.GetAssembly(typeof(ClrScopeServiceCollectionExtensions))!;
        
        // Find all classes with [McpServerToolType] attribute
        var toolTypes = assembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .ToList();

        // Find all methods with [McpServerTool] attribute
        var toolMethods = new HashSet<string>();
        foreach (var toolType in toolTypes)
        {
            var methods = toolType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null);
            
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (attr != null && !string.IsNullOrEmpty(attr.Name))
                {
                    toolMethods.Add(attr.Name);
                }
            }
        }

        // Get the source code of ClrScopeServiceCollectionExtensions to check WithTools calls
        var extensionsPath = Path.Combine(
            assembly.Location, 
            "..", "..", "..", "..", "..", "..", 
            "src", "ClrScope.Mcp", "DependencyInjection", 
            "ClrScopeServiceCollectionExtensions.cs");
        
        var extensionsCode = File.ReadAllText(extensionsPath);

        // Act & Assert
        // Verify each tool class with [McpServerToolType] is mentioned in WithTools calls
        foreach (var toolType in toolTypes)
        {
            var typeName = toolType.Name;
            var isRegistered = extensionsCode.Contains($".WithTools<{typeName}>()");
            Assert.True(isRegistered, 
                $"Tool class {typeName} with [McpServerToolType] is not registered with WithTools<{typeName}>() in ClrScopeServiceCollectionExtensions");
        }
    }

    [Fact]
    public void Expected_Tool_Classes_Are_Registered_WithTools()
    {
        // Arrange - expected tool classes from DI registration
        var expectedToolClasses = new[]
        {
            "RuntimeTools",
            "CollectTools",
            "CollectCountersTools",
            "SystemTools",
            "SessionTools",
            "ArtifactCrudTools",
            "ArtifactLifecycleTools",
            "AnalysisTools",
            "ResourceTools",
            "SummaryTools",
            "PatternDetectionTools",
            "HeapAnalysisTools",
            "SessionAnalysisTools",
            "WorkflowAutomationTools"
        };

        var assembly = Assembly.GetAssembly(typeof(ClrScopeServiceCollectionExtensions))!;
        var extensionsPath = Path.Combine(
            assembly.Location, 
            "..", "..", "..", "..", "..", "..", 
            "src", "ClrScope.Mcp", "DependencyInjection", 
            "ClrScopeServiceCollectionExtensions.cs");
        
        var extensionsCode = File.ReadAllText(extensionsPath);

        // Act & Assert
        foreach (var expectedTypeName in expectedToolClasses)
        {
            var isRegistered = extensionsCode.Contains($".WithTools<{expectedTypeName}>()");
            Assert.True(isRegistered, 
                $"Expected tool class {expectedTypeName} is not registered with WithTools<{expectedTypeName}>() in ClrScopeServiceCollectionExtensions");
        }
    }

    [Fact]
    public void All_McpServerTool_Methods_Have_Tool_Class_Registered()
    {
        // Arrange
        var assembly = Assembly.GetAssembly(typeof(ClrScopeServiceCollectionExtensions))!;
        
        // Find all classes with [McpServerToolType] attribute
        var toolTypes = assembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .ToList();

        // Find all methods with [McpServerTool] attribute
        var toolMethods = new List<(string ToolName, string ClassName)>();
        foreach (var toolType in toolTypes)
        {
            var methods = toolType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null);
            
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (attr != null && !string.IsNullOrEmpty(attr.Name))
                {
                    toolMethods.Add((attr.Name, toolType.Name));
                }
            }
        }

        var extensionsPath = Path.Combine(
            assembly.Location, 
            "..", "..", "..", "..", "..", "..", 
            "src", "ClrScope.Mcp", "DependencyInjection", 
            "ClrScopeServiceCollectionExtensions.cs");
        
        var extensionsCode = File.ReadAllText(extensionsPath);

        // Act & Assert
        // Verify each tool class that has [McpServerTool] methods is registered
        var registeredClasses = new HashSet<string>();
        foreach (var (toolName, className) in toolMethods)
        {
            registeredClasses.Add(className);
            var isRegistered = extensionsCode.Contains($".WithTools<{className}>()");
            Assert.True(isRegistered, 
                $"Tool class {className} has [McpServerTool] method '{toolName}' but is not registered with WithTools<{className}>() in ClrScopeServiceCollectionExtensions");
        }
    }
}
