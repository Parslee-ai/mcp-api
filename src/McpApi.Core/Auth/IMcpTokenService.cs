namespace McpApi.Core.Auth;

using McpApi.Core.Models;

/// <summary>
/// Service for managing MCP API tokens.
/// </summary>
public interface IMcpTokenService
{
    /// <summary>
    /// Creates a new API token for a user.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="name">A friendly name for the token.</param>
    /// <param name="expiresAt">Optional expiration date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created token and its plaintext value (only shown once).</returns>
    Task<CreateTokenResult> CreateTokenAsync(
        string userId,
        string name,
        DateTime? expiresAt = null,
        CancellationToken ct = default);

    /// <summary>
    /// Validates a plaintext token and returns the associated user info.
    /// </summary>
    /// <param name="plaintextToken">The token value to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The validated token if valid, null otherwise.</returns>
    Task<McpToken?> ValidateTokenAsync(string plaintextToken, CancellationToken ct = default);

    /// <summary>
    /// Gets all tokens for a user (without exposing the token values).
    /// </summary>
    Task<IReadOnlyList<McpToken>> GetUserTokensAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Revokes a token, making it invalid for future use.
    /// </summary>
    Task RevokeTokenAsync(string userId, string tokenId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a token permanently.
    /// </summary>
    Task DeleteTokenAsync(string userId, string tokenId, CancellationToken ct = default);
}
