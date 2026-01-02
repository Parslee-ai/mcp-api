namespace McpApi.Core.Models;

/// <summary>
/// Represents an API parameter (path, query, header, cookie).
/// </summary>
public class ParameterDefinition
{
    /// <summary>Parameter name.</summary>
    public required string Name { get; set; }

    /// <summary>Location: path, query, header, cookie.</summary>
    public required string In { get; set; }

    /// <summary>Whether the parameter is required.</summary>
    public bool Required { get; set; }

    /// <summary>Parameter description for tool hints.</summary>
    public string? Description { get; set; }

    /// <summary>JSON Schema for the parameter value.</summary>
    public required JsonSchema Schema { get; set; }

    /// <summary>Example value.</summary>
    public object? Example { get; set; }

    /// <summary>Default value if not provided.</summary>
    public object? Default { get; set; }

    /// <summary>Whether to exclude from MCP tool schema.</summary>
    public bool ExcludeFromTool { get; set; }
}
