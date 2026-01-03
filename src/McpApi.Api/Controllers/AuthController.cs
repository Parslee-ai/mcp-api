using System.Security.Claims;
using McpApi.Api.Auth;
using McpApi.Api.DTOs;
using McpApi.Core.Auth;
using McpApi.Core.Models;
using McpApi.Core.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpApi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IConfiguration _configuration;
    private readonly IApiRegistrationStore _apiStore;
    private readonly IMcpTokenStore _mcpTokenStore;
    private readonly IUsageStore _usageStore;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IUserStore _userStore;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IJwtTokenService jwtTokenService,
        IConfiguration configuration,
        IApiRegistrationStore apiStore,
        IMcpTokenStore mcpTokenStore,
        IUsageStore usageStore,
        IRefreshTokenStore refreshTokenStore,
        IUserStore userStore,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _jwtTokenService = jwtTokenService;
        _configuration = configuration;
        _apiStore = apiStore;
        _mcpTokenStore = mcpTokenStore;
        _usageStore = usageStore;
        _refreshTokenStore = refreshTokenStore;
        _userStore = userStore;
        _logger = logger;
    }

    /// <summary>
    /// Initiates GitHub OAuth login flow.
    /// </summary>
    [HttpGet("login/github")]
    public IActionResult LoginGitHub([FromQuery] string? returnUrl)
    {
        var redirectUrl = GetOAuthRedirectUrl(returnUrl);
        var properties = new AuthenticationProperties
        {
            RedirectUri = $"/api/auth/callback/github/complete?returnUrl={Uri.EscapeDataString(redirectUrl)}",
            Items = { { "returnUrl", redirectUrl } }
        };
        return Challenge(properties, "GitHub");
    }

    /// <summary>
    /// Handles GitHub OAuth callback after authentication.
    /// </summary>
    [HttpGet("callback/github/complete")]
    public async Task<IActionResult> GitHubCallback([FromQuery] string? returnUrl, CancellationToken ct)
    {
        return await HandleOAuthCallback("github", returnUrl, ct);
    }

    private async Task<IActionResult> HandleOAuthCallback(string provider, string? returnUrl, CancellationToken ct)
    {
        // Authenticate with the temporary cookie scheme
        var result = await HttpContext.AuthenticateAsync("OAuthTemp");
        if (!result.Succeeded || result.Principal == null)
        {
            var errorRedirect = GetErrorRedirectUrl(returnUrl, "Authentication failed");
            return Redirect(errorRedirect);
        }

        // Extract user info from claims
        var claims = result.Principal.Claims.ToList();
        var providerId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        var avatarUrl = claims.FirstOrDefault(c => c.Type == "urn:github:avatar")?.Value;

        if (string.IsNullOrEmpty(providerId) || string.IsNullOrEmpty(email))
        {
            var errorRedirect = GetErrorRedirectUrl(returnUrl, "Could not retrieve user information from provider");
            return Redirect(errorRedirect);
        }

        // Create/update user via auth service
        var userInfo = new OAuthUserInfo(provider, providerId, email, name, avatarUrl);
        var authResult = await _authService.OAuthLoginAsync(userInfo, ct);

        if (!authResult.Success || authResult.User == null)
        {
            var errorRedirect = GetErrorRedirectUrl(returnUrl, authResult.ErrorMessage ?? "Login failed");
            return Redirect(errorRedirect);
        }

        // Generate JWT tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(authResult.User);
        var (refreshToken, refreshTokenPlaintext) = await _jwtTokenService.GenerateRefreshTokenAsync(authResult.User.Id, ct);

        // Set refresh token cookie
        SetRefreshTokenCookie(refreshTokenPlaintext, refreshToken.ExpiresAt);

        // Clear the temporary OAuth cookie
        await HttpContext.SignOutAsync("OAuthTemp");

        // Redirect to frontend with access token
        var finalRedirectUrl = GetSuccessRedirectUrl(returnUrl, accessToken);
        return Redirect(finalRedirectUrl);
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

    /// <summary>
    /// Deletes the user's account and all associated data (GDPR compliance).
    /// This permanently removes:
    /// - All API registrations and their endpoints
    /// - All MCP tokens
    /// - All usage records
    /// - All refresh tokens
    /// - The user account itself
    /// </summary>
    [HttpDelete("account")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
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

        _logger.LogInformation("Account deletion initiated for user {UserId} ({Email})", userId, user.Email);

        try
        {
            // Delete all user data in parallel where possible
            // Order: dependent data first, then user record last

            // 1. Delete API registrations (includes endpoints)
            await _apiStore.DeleteAllForUserAsync(userId, ct);
            _logger.LogInformation("Deleted API registrations for user {UserId}", userId);

            // 2. Delete MCP tokens
            await _mcpTokenStore.DeleteAllForUserAsync(userId, ct);
            _logger.LogInformation("Deleted MCP tokens for user {UserId}", userId);

            // 3. Delete usage records
            await _usageStore.DeleteAllForUserAsync(userId, ct);
            _logger.LogInformation("Deleted usage records for user {UserId}", userId);

            // 4. Delete all refresh tokens (not just revoke - permanently delete)
            await _refreshTokenStore.DeleteAllForUserAsync(userId, ct);
            _logger.LogInformation("Deleted refresh tokens for user {UserId}", userId);

            // 5. Delete user record last
            await _userStore.DeleteAsync(userId, ct);
            _logger.LogInformation("Deleted user record for user {UserId}", userId);

            // Clear the refresh token cookie
            ClearRefreshTokenCookie();

            _logger.LogInformation("Account deletion completed for user {UserId}", userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Account deletion failed for user {UserId}", userId);
            return StatusCode(500, new ErrorResponse("Account deletion failed. Please contact support."));
        }
    }

    /// <summary>
    /// Returns the configured OAuth providers.
    /// </summary>
    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var providers = new List<string>();

        if (!string.IsNullOrEmpty(_configuration["GitHub:ClientId"]))
        {
            providers.Add("github");
        }

        return Ok(new { providers });
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
            SameSite = SameSiteMode.Lax, // Required for OAuth redirect
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
            SameSite = SameSiteMode.Lax
        });
    }

    private string GetFrontendUrl()
    {
        return _configuration["App:FrontendUrl"] ?? "http://localhost:3000";
    }

    /// <summary>
    /// Validates that a return URL is safe (relative path or same origin).
    /// Prevents open redirect attacks by rejecting external URLs.
    /// </summary>
    private bool IsValidReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl))
        {
            return true; // Will use default
        }

        // Allow relative paths that start with / but not // (protocol-relative URLs)
        if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//"))
        {
            return true;
        }

        // Allow URLs that match our frontend origin
        var frontendUrl = GetFrontendUrl();
        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri) &&
            Uri.TryCreate(frontendUrl, UriKind.Absolute, out var frontendUri))
        {
            // Check if the host matches our frontend
            return string.Equals(uri.Host, frontendUri.Host, StringComparison.OrdinalIgnoreCase) &&
                   uri.Port == frontendUri.Port &&
                   string.Equals(uri.Scheme, frontendUri.Scheme, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private string GetOAuthRedirectUrl(string? returnUrl)
    {
        var frontendUrl = GetFrontendUrl();

        // Validate returnUrl to prevent open redirect attacks
        if (!IsValidReturnUrl(returnUrl))
        {
            return $"{frontendUrl}/auth/callback";
        }

        return returnUrl ?? $"{frontendUrl}/auth/callback";
    }

    private string GetSuccessRedirectUrl(string? returnUrl, string accessToken)
    {
        var redirectUrl = GetOAuthRedirectUrl(returnUrl);
        // Use fragment identifier (#) instead of query parameter (?)
        // Fragment is never sent to the server, preventing token leakage via logs, Referer header, etc.
        return $"{redirectUrl}#token={accessToken}";
    }

    private string GetErrorRedirectUrl(string? returnUrl, string error)
    {
        var frontendUrl = GetFrontendUrl();

        // Validate returnUrl to prevent open redirect attacks
        string redirectUrl;
        if (!IsValidReturnUrl(returnUrl))
        {
            redirectUrl = $"{frontendUrl}/auth/login";
        }
        else
        {
            redirectUrl = returnUrl ?? $"{frontendUrl}/auth/login";
        }

        var separator = redirectUrl.Contains('?') ? '&' : '?';
        return $"{redirectUrl}{separator}error={Uri.EscapeDataString(error)}";
    }

    private static UserDto ToUserDto(User user)
    {
        return new UserDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.OAuthProvider,
            user.EmailVerified,
            user.Tier,
            user.CreatedAt);
    }
}
