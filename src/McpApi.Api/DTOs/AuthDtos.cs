using System.ComponentModel.DataAnnotations;

namespace McpApi.Api.DTOs;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword
);

public record SetPhoneRequest(
    [Required, Phone] string PhoneNumber
);

public record VerifyPhoneRequest(
    [Required, StringLength(6, MinimumLength = 6)] string Code
);

public record AuthResponse(
    string AccessToken,
    UserDto User
);

public record UserDto(
    string Id,
    string Email,
    string? PhoneNumber,
    bool EmailVerified,
    bool PhoneVerified,
    string Tier,
    DateTime CreatedAt
);

public record MessageResponse(string Message);

public record ErrorResponse(string Error, string? Details = null);
