namespace McpApi.Api.DTOs;

public record AuthResponse(
    string AccessToken,
    UserDto User
);

public record UserDto(
    string Id,
    string Email,
    string? DisplayName,
    string? AvatarUrl,
    string? OAuthProvider,
    bool EmailVerified,
    string Tier,
    DateTime CreatedAt
);

public record MessageResponse(string Message);

public record ErrorResponse(string Error, string? Details = null);
