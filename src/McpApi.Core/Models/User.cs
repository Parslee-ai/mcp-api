namespace McpApi.Core.Models;

/// <summary>
/// Represents a registered user in the system.
/// OAuth-only authentication is supported (no password-based accounts).
/// </summary>
public class User
{
    /// <summary>
    /// Unique identifier for the user (GUID).
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// User's email address (used for identification).
    /// Stored in lowercase for consistent lookups.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// OAuth provider name (google, github).
    /// </summary>
    public string? OAuthProvider { get; set; }

    /// <summary>
    /// Unique identifier from the OAuth provider.
    /// </summary>
    public string? OAuthProviderId { get; set; }

    /// <summary>
    /// User's display name from OAuth provider (optional).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// URL to user's avatar/profile picture from OAuth provider (optional).
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Whether the user's email has been verified.
    /// OAuth users are automatically verified.
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// Salt used for deriving per-user encryption key.
    /// </summary>
    public string? EncryptionKeySalt { get; set; }

    /// <summary>
    /// User's subscription tier (free, pro, enterprise).
    /// </summary>
    public string Tier { get; set; } = "free";

    /// <summary>
    /// When the user account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the user last logged in.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}
