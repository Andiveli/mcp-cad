using System.Reflection;
using McpCad.Core.Exceptions;
using ModelContextProtocol.Server;

namespace McpCad.Tools;

/// <summary>
/// Ensures MCP tool method names are unique across registered tool types before server startup.
/// </summary>
public static class ToolRegistrationValidator
{
    public static void EnsureNoCollisions(params Type[] toolTypes)
    {
        var byName = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var toolType in toolTypes)
        {
            var providerLabel = toolType.FullName ?? toolType.Name;
            foreach (var method in GetToolMethods(toolType))
            {
                if (!byName.TryGetValue(method, out var list))
                {
                    list = new List<string>();
                    byName[method] = list;
                }

                if (!list.Contains(providerLabel, StringComparer.Ordinal))
                {
                    list.Add(providerLabel);
                }
            }
        }

        foreach (var (toolName, providers) in byName)
        {
            if (providers.Count > 1)
            {
                throw new ToolRegistrationException(toolName, providers);
            }
        }
    }

    public static IReadOnlyList<string> GetToolMethodNames(Type toolType) =>
        GetToolMethods(toolType).ToList();

    private static IEnumerable<string> GetToolMethods(Type toolType)
    {
        return toolType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(m => m.Name);
    }
}