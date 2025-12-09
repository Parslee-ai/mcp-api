namespace AnyAPI.Core.OpenApi;

using AnyAPI.Core.Models;
using Microsoft.OpenApi.Models;

/// <summary>
/// Flattens OpenAPI schemas into simplified JsonSchema representation.
/// Handles $ref resolution with circular reference protection.
/// Thread-safe: each Flatten call uses its own visited refs tracking.
/// </summary>
public class SchemaFlattener
{
    private const int MaxDepth = 10;

    /// <summary>
    /// Flattens an OpenAPI schema into a JsonSchema.
    /// </summary>
    public JsonSchema Flatten(OpenApiSchema? schema)
    {
        return FlattenInternal(schema, 0, new HashSet<string>());
    }

    private JsonSchema FlattenInternal(OpenApiSchema? schema, int depth, HashSet<string> visitedRefs)
    {
        if (schema == null)
        {
            return new JsonSchema { Type = "object" };
        }

        // Circular reference protection
        if (depth > MaxDepth)
        {
            return new JsonSchema { Type = "object", Description = "(max depth reached)" };
        }

        // Handle reference
        if (schema.Reference != null)
        {
            var refId = schema.Reference.Id;
            if (visitedRefs.Contains(refId))
            {
                return new JsonSchema { Type = "object", Description = $"(circular ref to {refId})" };
            }
            visitedRefs.Add(refId);
        }

        var result = new JsonSchema
        {
            Type = MapType(schema.Type ?? "object"),
            Format = schema.Format,
            Description = schema.Description,
            Minimum = (double?)schema.Minimum,
            Maximum = (double?)schema.Maximum,
            MinLength = schema.MinLength,
            MaxLength = schema.MaxLength,
            Pattern = schema.Pattern,
            Default = schema.Default?.ToString(),
            Example = schema.Example?.ToString()
        };

        // Handle enum
        if (schema.Enum?.Count > 0)
        {
            result.Enum = schema.Enum
                .Select(e => e?.ToString() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        // Handle array items
        if (schema.Type == "array" && schema.Items != null)
        {
            result.Items = FlattenInternal(schema.Items, depth + 1, visitedRefs);
        }

        // Handle object properties
        if (schema.Properties?.Count > 0)
        {
            result.Properties = new Dictionary<string, JsonSchema>();
            foreach (var (name, prop) in schema.Properties)
            {
                result.Properties[name] = FlattenInternal(prop, depth + 1, visitedRefs);
            }
        }

        // Handle required properties
        if (schema.Required?.Count > 0)
        {
            result.Required = schema.Required.ToList();
        }

        // Clear visited ref after processing (allows same ref in different branches)
        if (schema.Reference != null)
        {
            visitedRefs.Remove(schema.Reference.Id);
        }

        return result;
    }

    private static string MapType(string openApiType)
    {
        return openApiType.ToLowerInvariant() switch
        {
            "integer" => "integer",
            "number" => "number",
            "string" => "string",
            "boolean" => "boolean",
            "array" => "array",
            "object" => "object",
            _ => "string"
        };
    }
}
