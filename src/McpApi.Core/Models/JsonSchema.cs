namespace McpApi.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Simplified JSON Schema representation for MCP tool parameters.
/// </summary>
public class JsonSchema
{
    /// <summary>JSON Schema type (string, number, integer, boolean, array, object).</summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>Format hint (e.g., "date-time", "email", "uri").</summary>
    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; set; }

    /// <summary>Human-readable description.</summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>Allowed enumeration values.</summary>
    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Enum { get; set; }

    /// <summary>Schema for array items.</summary>
    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonSchema? Items { get; set; }

    /// <summary>Properties for object type.</summary>
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonSchema>? Properties { get; set; }

    /// <summary>Required property names for object type.</summary>
    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Required { get; set; }

    /// <summary>Minimum value for number/integer.</summary>
    [JsonPropertyName("minimum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Minimum { get; set; }

    /// <summary>Maximum value for number/integer.</summary>
    [JsonPropertyName("maximum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Maximum { get; set; }

    /// <summary>Minimum string length.</summary>
    [JsonPropertyName("minLength")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MinLength { get; set; }

    /// <summary>Maximum string length.</summary>
    [JsonPropertyName("maxLength")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxLength { get; set; }

    /// <summary>Regex pattern for string validation.</summary>
    [JsonPropertyName("pattern")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Pattern { get; set; }

    /// <summary>Default value.</summary>
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Default { get; set; }

    /// <summary>Example value.</summary>
    [JsonPropertyName("example")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Example { get; set; }
}
