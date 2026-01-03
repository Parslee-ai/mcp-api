using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using McpApi.Core.Models;
using McpApi.Core.Storage;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace McpApi.Api.Auth;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(IOptions<JwtOptions> options, IRefreshTokenStore refreshTokenStore)
    {
        _options = options.Value;
        _refreshTokenStore = refreshTokenStore;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
    }

    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email),
            new("tier", user.Tier),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(RefreshToken token, string plaintext)> GenerateRefreshTokenAsync(string userId, CancellationToken ct = default)
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var plaintext = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var tokenHash = HashToken(plaintext);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenStore.UpsertAsync(refreshToken, ct);

        return (refreshToken, plaintext);
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string plaintextToken, CancellationToken ct = default)
    {
        var tokenHash = HashToken(plaintextToken);
        var token = await _refreshTokenStore.GetByHashAsync(tokenHash, ct);

        if (token == null || !token.IsActive)
        {
            return null;
        }

        return token;
    }

    public async Task RevokeRefreshTokenAsync(string userId, string tokenId, CancellationToken ct = default)
    {
        await _refreshTokenStore.RevokeAsync(userId, tokenId, ct);
    }

    public async Task RevokeAllRefreshTokensAsync(string userId, CancellationToken ct = default)
    {
        await _refreshTokenStore.RevokeAllForUserAsync(userId, ct);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
