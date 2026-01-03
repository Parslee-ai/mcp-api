using System.Security.Claims;
using McpApi.Api.Auth;
using McpApi.Api.DTOs;
using McpApi.Core.Auth;
using McpApi.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpApi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(IAuthService authService, IJwtTokenService jwtTokenService)
    {
        _authService = authService;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request.Email, request.Password, ct);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorMessage ?? "Registration failed"));
        }

        return Ok(new MessageResponse("Registration successful. Please check your email to verify your account."));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password, ct);

        if (!result.Success || result.User == null)
        {
            return Unauthorized(new ErrorResponse(result.ErrorMessage ?? "Invalid credentials"));
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(result.User);
        var (refreshToken, refreshTokenPlaintext) = await _jwtTokenService.GenerateRefreshTokenAsync(result.User.Id, ct);

        SetRefreshTokenCookie(refreshTokenPlaintext, refreshToken.ExpiresAt);

        return Ok(new AuthResponse(accessToken, ToUserDto(result.User)));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refreshTokenValue = Request.Cookies["refresh_token"];
        if (string.IsNullOrEmpty(refreshTokenValue))
        {
            return Unauthorized(new ErrorResponse("No refresh token provided"));
        }

        var existingToken = await _jwtTokenService.ValidateRefreshTokenAsync(refreshTokenValue, ct);
        if (existingToken == null)
        {
            return Unauthorized(new ErrorResponse("Invalid or expired refresh token"));
        }

        var user = await _authService.GetUserAsync(existingToken.UserId, ct);
        if (user == null)
        {
            return Unauthorized(new ErrorResponse("User not found"));
        }

        // Revoke old token and create new one (token rotation)
        await _jwtTokenService.RevokeRefreshTokenAsync(existingToken.UserId, existingToken.Id, ct);
        var (newRefreshToken, newRefreshTokenPlaintext) = await _jwtTokenService.GenerateRefreshTokenAsync(user.Id, ct);

        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        SetRefreshTokenCookie(newRefreshTokenPlaintext, newRefreshToken.ExpiresAt);

        return Ok(new AuthResponse(accessToken, ToUserDto(user)));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        await _jwtTokenService.RevokeAllRefreshTokensAsync(userId, ct);
        ClearRefreshTokenCookie();

        return Ok(new MessageResponse("Logged out successfully"));
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token, CancellationToken ct)
    {
        var result = await _authService.VerifyEmailAsync(token, ct);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorMessage ?? "Email verification failed"));
        }

        return Ok(new MessageResponse("Email verified successfully. You can now log in."));
    }

    [HttpPost("resend-verification")]
    [Authorize]
    public async Task<IActionResult> ResendVerification(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _authService.ResendEmailVerificationAsync(userId, ct);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorMessage ?? "Failed to resend verification email"));
        }

        return Ok(new MessageResponse("Verification email sent"));
    }

    [HttpPost("phone")]
    [Authorize]
    public async Task<IActionResult> SetPhone([FromBody] SetPhoneRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _authService.SetPhoneNumberAsync(userId, request.PhoneNumber, ct);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorMessage ?? "Failed to set phone number"));
        }

        return Ok(new MessageResponse("Verification code sent to your phone"));
    }

    [HttpPost("verify-phone")]
    [Authorize]
    public async Task<IActionResult> VerifyPhone([FromBody] VerifyPhoneRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _authService.VerifyPhoneAsync(userId, request.Code, ct);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorMessage ?? "Phone verification failed"));
        }

        return Ok(new MessageResponse("Phone number verified successfully"));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _authService.GetUserAsync(userId, ct);
        if (user == null)
        {
            return NotFound(new ErrorResponse("User not found"));
        }

        return Ok(ToUserDto(user));
    }

    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.ErrorMessage ?? "Failed to change password"));
        }

        return Ok(new MessageResponse("Password changed successfully"));
    }

    private string? GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private void SetRefreshTokenCookie(string token, DateTime expires)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expires
        };

        Response.Cookies.Append("refresh_token", token, cookieOptions);
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict
        });
    }

    private static UserDto ToUserDto(User user)
    {
        return new UserDto(
            user.Id,
            user.Email,
            user.PhoneNumber,
            user.EmailVerified,
            user.PhoneVerified,
            user.Tier,
            user.CreatedAt);
    }
}
