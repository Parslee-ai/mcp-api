namespace McpApi.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents an API token for MCP server authentication.
/// Tokens are created by users in the web UI and used to authenticate MCP sessions.
/// </summary>
public class McpToken
{
    /// <summary>
    /// Unique identifier for the token.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// The user who owns this token. Used as partition key.
    /// </summary>
    [JsonPropertyName("userId")]
    public required string UserId { get; set; }

    /// <summary>
    /// SHA-256 hash of the token value. The actual token is only shown once at creation.
    /// </summary>
    public required string TokenHash { get; set; }

    /// <summary>
    /// User-provided name to identify this token (e.g., "Development", "Production").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Optional expiration date. Null means the token never expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Whether the token has been revoked by the user.
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// When the token was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the token was last used to authenticate.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Checks if the token is valid (not expired and not revoked).
    /// </summary>
    public bool IsValid => !IsRevoked && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}

/// <summary>
/// Result of creating a new token. Contains the plaintext token (only shown once).
/// </summary>
public record CreateTokenResult(McpToken Token, string PlaintextToken);
