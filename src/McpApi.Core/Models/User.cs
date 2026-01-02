namespace McpApi.Core.Models;

/// <summary>
/// Represents a registered user in the system.
/// </summary>
public class User
{
    /// <summary>
    /// Unique identifier for the user (GUID).
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// User's email address (used for login).
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// User's phone number (optional, for SMS verification).
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// BCrypt hash of the user's password.
    /// </summary>
    public required string PasswordHash { get; set; }

    /// <summary>
    /// Whether the user's email has been verified.
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// Whether the user's phone number has been verified.
    /// </summary>
    public bool PhoneVerified { get; set; }

    /// <summary>
    /// Token for email verification (sent via link).
    /// </summary>
    public string? EmailVerificationToken { get; set; }

    /// <summary>
    /// Expiration time for the email verification token.
    /// </summary>
    public DateTime? EmailVerificationTokenExpiry { get; set; }

    /// <summary>
    /// 6-digit code for phone verification (sent via SMS).
    /// </summary>
    public string? PhoneVerificationCode { get; set; }

    /// <summary>
    /// Expiration time for the phone verification code.
    /// </summary>
    public DateTime? PhoneVerificationCodeExpiry { get; set; }

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
