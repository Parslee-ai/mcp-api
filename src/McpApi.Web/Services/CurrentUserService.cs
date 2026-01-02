using McpApi.Core.Auth;
using McpApi.Core.Models;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace McpApi.Web.Services;

/// <summary>
/// Implementation of current user service using Blazor authentication.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IAuthService _authService;

    public CurrentUserService(
        AuthenticationStateProvider authStateProvider,
        IAuthService authService)
    {
        _authStateProvider = authStateProvider;
        _authService = authService;
    }

    public string? UserId
    {
        get
        {
            var authState = _authStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult();
            return authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            var authState = _authStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult();
            return authState.User.Identity?.IsAuthenticated ?? false;
        }
    }

    public async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        return await _authService.GetUserAsync(userId, cancellationToken);
    }
}
