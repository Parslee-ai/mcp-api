namespace AnyAPI.Core.Models;

/// <summary>
/// Response definition for documentation and validation.
/// </summary>
public class ResponseDefinition
{
    /// <summary>Response description.</summary>
    public string? Description { get; set; }

    /// <summary>Content type to schema mapping.</summary>
    public Dictionary<string, JsonSchema>? Content { get; set; }
}
