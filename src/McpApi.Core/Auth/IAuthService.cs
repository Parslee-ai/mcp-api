using McpApi.Core.Models;

namespace McpApi.Core.Auth;

/// <summary>
/// Result of an authentication operation.
/// </summary>
public record AuthResult(bool Success, string? ErrorMessage = null, User? User = null);

/// <summary>
/// OAuth user information received from an OAuth provider.
/// </summary>
public record OAuthUserInfo(
    string Provider,
    string ProviderId,
    string Email,
    string? DisplayName,
    string? AvatarUrl
);

/// <summary>
/// Interface for authentication operations.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates or registers a user via OAuth.
    /// Creates a new user if one doesn't exist for the provider/providerId.
    /// </summary>
    Task<AuthResult> OAuthLoginAsync(OAuthUserInfo userInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their ID.
    /// </summary>
    Task<User?> GetUserAsync(string userId, CancellationToken cancellationToken = default);
}
