using System.Security.Cryptography;
using Microsoft.Azure.Cosmos;
using McpApi.Core.Storage;
using User = McpApi.Core.Models.User;

namespace McpApi.Core.Auth;

/// <summary>
/// Implementation of authentication operations for OAuth-based login.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserStore _userStore;

    public AuthService(IUserStore userStore)
    {
        _userStore = userStore;
    }

    public async Task<AuthResult> OAuthLoginAsync(OAuthUserInfo userInfo, CancellationToken cancellationToken = default)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(userInfo.Provider))
        {
            return new AuthResult(false, "OAuth provider is required.");
        }

        if (string.IsNullOrWhiteSpace(userInfo.ProviderId))
        {
            return new AuthResult(false, "OAuth provider ID is required.");
        }

        if (string.IsNullOrWhiteSpace(userInfo.Email))
        {
            return new AuthResult(false, "Email is required from OAuth provider.");
        }

        // Look for existing user by OAuth provider
        var user = await _userStore.GetByOAuthProviderAsync(userInfo.Provider, userInfo.ProviderId, cancellationToken);

        if (user == null)
        {
            // Check if email already exists with a different OAuth provider
            // SECURITY: Do NOT auto-link accounts - this prevents account takeover attacks
            // where an attacker creates an account with a victim's email on a different provider
            var existingUserByEmail = await _userStore.GetByEmailAsync(userInfo.Email, cancellationToken);
            if (existingUserByEmail != null)
            {
                // Email is already registered with a different OAuth provider
                // User must contact support to link accounts (prevents account takeover)
                var existingProvider = existingUserByEmail.OAuthProvider ?? "password";
                return new AuthResult(false,
                    $"An account with this email already exists using {existingProvider} authentication. " +
                    "Please sign in with your original provider, or contact support to link your accounts.");
            }

            // Create new user with race condition protection
            user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = userInfo.Email.ToLowerInvariant(),
                OAuthProvider = userInfo.Provider,
                OAuthProviderId = userInfo.ProviderId,
                DisplayName = userInfo.DisplayName,
                AvatarUrl = userInfo.AvatarUrl,
                EmailVerified = true, // OAuth emails are verified
                EncryptionKeySalt = GenerateSecureToken(),
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            try
            {
                await _userStore.UpsertAsync(user, cancellationToken);
                return new AuthResult(true, User: user);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Race condition: another request created the user concurrently
                // Retry the lookup and return the existing user
                var existingUser = await _userStore.GetByOAuthProviderAsync(userInfo.Provider, userInfo.ProviderId, cancellationToken);
                if (existingUser != null)
                {
                    return new AuthResult(true, User: existingUser);
                }

                // If still not found by provider, check by email (concurrent creation with different ID)
                existingUser = await _userStore.GetByEmailAsync(userInfo.Email, cancellationToken);
                if (existingUser != null)
                {
                    var existingProvider = existingUser.OAuthProvider ?? "password";
                    return new AuthResult(false,
                        $"An account with this email already exists using {existingProvider} authentication. " +
                        "Please sign in with your original provider, or contact support to link your accounts.");
                }

                // Unexpected conflict - rethrow
                throw;
            }
        }

        // Existing user - update their info and last login
        user.DisplayName = userInfo.DisplayName ?? user.DisplayName;
        user.AvatarUrl = userInfo.AvatarUrl ?? user.AvatarUrl;
        user.Email = userInfo.Email.ToLowerInvariant(); // Update email in case it changed
        user.LastLoginAt = DateTime.UtcNow;

        await _userStore.UpsertAsync(user, cancellationToken);
        return new AuthResult(true, User: user);
    }

    public async Task<User?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _userStore.GetByIdAsync(userId, cancellationToken);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
