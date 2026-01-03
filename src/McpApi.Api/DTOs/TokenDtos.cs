using System.ComponentModel.DataAnnotations;
using McpApi.Core.Models;

namespace McpApi.Api.DTOs;

public record CreateTokenRequest(
    [Required] string Name,
    DateTime? ExpiresAt = null
);

public record McpTokenDto(
    string Id,
    string Name,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? ExpiresAt,
    bool IsRevoked,
    bool IsActive
);

public record CreateTokenResponse(
    McpTokenDto Token,
    string PlaintextToken
);

public static class TokenDtoExtensions
{
    public static McpTokenDto ToDto(this McpToken token)
    {
        return new McpTokenDto(
            token.Id,
            token.Name,
            token.CreatedAt,
            token.LastUsedAt,
            token.ExpiresAt,
            token.IsRevoked,
            token.IsValid
        );
    }
}
