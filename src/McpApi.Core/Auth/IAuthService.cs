using McpApi.Core.Models;

namespace McpApi.Core.Auth;

/// <summary>
/// Result of an authentication operation.
/// </summary>
public record AuthResult(bool Success, string? ErrorMessage = null, User? User = null);

/// <summary>
/// Interface for authentication operations.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new user with email and password.
    /// </summary>
    Task<AuthResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user with email and password.
    /// </summary>
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a user's email using the verification token.
    /// </summary>
    Task<AuthResult> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a new email verification link to the user.
    /// </summary>
    Task<AuthResult> ResendEmailVerificationAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the user's phone number and sends a verification code.
    /// </summary>
    Task<AuthResult> SetPhoneNumberAsync(string userId, string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a user's phone number using the verification code.
    /// </summary>
    Task<AuthResult> VerifyPhoneAsync(string userId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their ID.
    /// </summary>
    Task<User?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a user's password.
    /// </summary>
    Task<AuthResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
