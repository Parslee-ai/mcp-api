using McpApi.Core.Models;

namespace McpApi.Core.Storage;

/// <summary>
/// Interface for user storage operations.
/// </summary>
public interface IUserStore
{
    /// <summary>
    /// Gets a user by their unique ID.
    /// </summary>
    Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their email address.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their OAuth provider and provider-specific ID.
    /// </summary>
    Task<User?> GetByOAuthProviderAsync(string provider, string providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their email verification token.
    /// </summary>
    Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a user.
    /// </summary>
    Task<User> UpsertAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user by their ID.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an email is already registered.
    /// </summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
}
