namespace AnyAPI.Core.Models;

/// <summary>
/// Request body schema for POST/PUT/PATCH operations.
/// </summary>
public class RequestBodyDefinition
{
    /// <summary>Whether the body is required.</summary>
    public bool Required { get; set; }

    /// <summary>Description of the request body.</summary>
    public string? Description { get; set; }

    /// <summary>Content type to schema mapping (e.g., "application/json" -> schema).</summary>
    public Dictionary<string, JsonSchema> Content { get; set; } = new();
}
