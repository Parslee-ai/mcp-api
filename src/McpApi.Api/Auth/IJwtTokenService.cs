using McpApi.Core.Models;

namespace McpApi.Api.Auth;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user);
    Task<(RefreshToken token, string plaintext)> GenerateRefreshTokenAsync(string userId, CancellationToken ct = default);
    Task<RefreshToken?> ValidateRefreshTokenAsync(string plaintextToken, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string userId, string tokenId, CancellationToken ct = default);
    Task RevokeAllRefreshTokensAsync(string userId, CancellationToken ct = default);
}
