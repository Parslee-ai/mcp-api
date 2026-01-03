using System.Security.Claims;
using McpApi.Core.Auth;
using McpApi.Core.Models;

namespace McpApi.Api.Services;

/// <summary>
/// Gets current user from HttpContext (JWT claims).
/// </summary>
public class HttpContextCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthService _authService;

    public HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor, IAuthService authService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authService = authService;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = UserId;
        if (userId == null)
        {
            return null;
        }

        return await _authService.GetUserAsync(userId, cancellationToken);
    }
}
