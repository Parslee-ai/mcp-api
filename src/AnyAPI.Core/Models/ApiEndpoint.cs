namespace AnyAPI.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a single API endpoint/operation that becomes an MCP tool.
/// Stored in separate Cosmos container with apiId as partition key.
/// </summary>
public class ApiEndpoint
{
    /// <summary>Unique ID within the API (e.g., "repos-create").</summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>Parent API registration ID (partition key). Set when saving to store.</summary>
    [JsonPropertyName("apiId")]
    public string ApiId { get; set; } = "";

    /// <summary>OpenAPI operation ID.</summary>
    public required string OperationId { get; set; }

    /// <summary>HTTP method (GET, POST, PUT, DELETE, PATCH).</summary>
    public required string Method { get; set; }

    /// <summary>Path template (e.g., "/repos/{owner}/{repo}").</summary>
    public required string Path { get; set; }

    /// <summary>Human-readable summary for tool description.</summary>
    public string? Summary { get; set; }

    /// <summary>Detailed description.</summary>
    public string? Description { get; set; }

    /// <summary>OpenAPI tags for grouping.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Path, query, and header parameters.</summary>
    public List<ParameterDefinition> Parameters { get; set; } = [];

    /// <summary>Request body definition (if any).</summary>
    public RequestBodyDefinition? RequestBody { get; set; }

    /// <summary>Expected responses by status code.</summary>
    public Dictionary<string, ResponseDefinition> Responses { get; set; } = new();

    /// <summary>Security requirements specific to this endpoint.</summary>
    public List<string>? SecurityRequirements { get; set; }

    /// <summary>Whether this endpoint is enabled for MCP exposure.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>MCP tool name override (auto-generated if null).</summary>
    public string? ToolNameOverride { get; set; }

    /// <summary>
    /// Generates the MCP tool name for this endpoint.
    /// </summary>
    public string GetToolName(string apiId)
    {
        if (!string.IsNullOrEmpty(ToolNameOverride))
            return ToolNameOverride;

        var tag = Tags.FirstOrDefault()?.ToLowerInvariant() ?? "api";
        var operation = OperationId.ToLowerInvariant().Replace("_", "-");
        return $"{apiId}.{tag}.{operation}";
    }
}
