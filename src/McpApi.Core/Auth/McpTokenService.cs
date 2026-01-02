namespace McpApi.Core.Auth;

using System.Security.Cryptography;
using McpApi.Core.Models;
using McpApi.Core.Storage;

/// <summary>
/// Service for managing MCP API tokens with secure hashing.
/// </summary>
public class McpTokenService : IMcpTokenService
{
    private readonly IMcpTokenStore _store;

    public McpTokenService(IMcpTokenStore store)
    {
        _store = store;
    }

    public async Task<CreateTokenResult> CreateTokenAsync(
        string userId,
        string name,
        DateTime? expiresAt = null,
        CancellationToken ct = default)
    {
        // Generate a cryptographically secure token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var plaintextToken = $"mcp_{Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";

        // Hash the token for storage
        var tokenHash = HashToken(plaintextToken);

        var token = new McpToken
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = tokenHash,
            Name = name,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        await _store.UpsertAsync(token, ct);

        return new CreateTokenResult(token, plaintextToken);
    }

    public async Task<McpToken?> ValidateTokenAsync(string plaintextToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(plaintextToken))
            return null;

        // Hash the provided token and look it up
        var tokenHash = HashToken(plaintextToken);
        var token = await _store.GetByHashAsync(tokenHash, ct);

        if (token == null)
            return null;

        // Check if token is valid
        if (!token.IsValid)
            return null;

        // Update last used timestamp (fire and forget)
        _ = _store.UpdateLastUsedAsync(token.UserId, token.Id, ct);

        return token;
    }

    public Task<IReadOnlyList<McpToken>> GetUserTokensAsync(string userId, CancellationToken ct = default)
    {
        return _store.GetAllForUserAsync(userId, ct);
    }

    public async Task RevokeTokenAsync(string userId, string tokenId, CancellationToken ct = default)
    {
        var token = await _store.GetAsync(userId, tokenId, ct);
        if (token != null)
        {
            token.IsRevoked = true;
            await _store.UpsertAsync(token, ct);
        }
    }

    public Task DeleteTokenAsync(string userId, string tokenId, CancellationToken ct = default)
    {
        return _store.DeleteAsync(userId, tokenId, ct);
    }

    /// <summary>
    /// Computes SHA-256 hash of the token for secure storage.
    /// </summary>
    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
