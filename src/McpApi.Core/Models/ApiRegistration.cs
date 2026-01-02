namespace McpApi.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Root entity representing a registered API in the system.
/// Stored in Cosmos DB with partition key on UserId for multi-tenancy.
/// </summary>
public class ApiRegistration
{
    /// <summary>Unique identifier (e.g., "github", "stripe", "openai").</summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>Owner user ID (partition key for multi-tenancy). Set by service before saving.</summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "";

    /// <summary>Display name for the dashboard.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Base URL for API calls (e.g., "https://api.github.com").</summary>
    public required string BaseUrl { get; set; }

    /// <summary>Original OpenAPI spec URL (for refresh).</summary>
    public string? SpecUrl { get; set; }

    /// <summary>OpenAPI spec version (3.0.0, 3.1.0).</summary>
    public required string OpenApiVersion { get; set; }

    /// <summary>API version from spec info.</summary>
    public string? ApiVersion { get; set; }

    /// <summary>Brief description from spec.</summary>
    public string? Description { get; set; }

    /// <summary>Authentication configuration.</summary>
    public required AuthConfiguration Auth { get; set; }

    /// <summary>All endpoints discovered from the spec.</summary>
    public List<ApiEndpoint> Endpoints { get; set; } = [];

    /// <summary>Whether this API is active and tools should be exposed.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>When the spec was last parsed/refreshed.</summary>
    public DateTime LastRefreshed { get; set; }

    /// <summary>When this registration was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Cosmos DB ETag for optimistic concurrency.</summary>
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }

    /// <summary>Gets count of enabled endpoints.</summary>
    [JsonIgnore]
    public int EnabledEndpointCount => Endpoints.Count(e => e.IsEnabled);
}
