namespace McpApi.Core.Storage;

using McpApi.Core.Models;

/// <summary>
/// Storage interface for MCP API tokens.
/// </summary>
public interface IMcpTokenStore
{
    /// <summary>
    /// Gets a token by its ID for a specific user.
    /// </summary>
    Task<McpToken?> GetAsync(string userId, string tokenId, CancellationToken ct = default);

    /// <summary>
    /// Gets a token by its hash (for authentication lookups).
    /// </summary>
    Task<McpToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Gets all tokens for a user.
    /// </summary>
    Task<IReadOnlyList<McpToken>> GetAllForUserAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a token.
    /// </summary>
    Task<McpToken> UpsertAsync(McpToken token, CancellationToken ct = default);

    /// <summary>
    /// Deletes a token.
    /// </summary>
    Task DeleteAsync(string userId, string tokenId, CancellationToken ct = default);

    /// <summary>
    /// Updates the last used timestamp for a token.
    /// </summary>
    Task UpdateLastUsedAsync(string userId, string tokenId, CancellationToken ct = default);
}
