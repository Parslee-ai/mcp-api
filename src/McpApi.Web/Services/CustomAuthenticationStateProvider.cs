using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Claims;

namespace McpApi.Web.Services;

/// <summary>
/// Custom authentication state provider that uses browser storage for session persistence.
/// </summary>
public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedSessionStorage _sessionStorage;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    private const string UserSessionKey = "UserSession";

    public CustomAuthenticationStateProvider(ProtectedSessionStorage sessionStorage)
    {
        _sessionStorage = sessionStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var storedPrincipal = await _sessionStorage.GetAsync<UserSession>(UserSessionKey);
            if (storedPrincipal.Success && storedPrincipal.Value != null)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, storedPrincipal.Value.UserId),
                    new(ClaimTypes.Email, storedPrincipal.Value.Email),
                    new("Tier", storedPrincipal.Value.Tier)
                };

                var identity = new ClaimsIdentity(claims, "CustomAuth");
                _currentUser = new ClaimsPrincipal(identity);
            }
        }
        catch
        {
            // Session storage not available (e.g., during prerendering)
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }

        return new AuthenticationState(_currentUser);
    }

    public async Task LoginAsync(string userId, string email, string tier)
    {
        var session = new UserSession
        {
            UserId = userId,
            Email = email,
            Tier = tier
        };

        await _sessionStorage.SetAsync(UserSessionKey, session);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new("Tier", tier)
        };

        var identity = new ClaimsIdentity(claims, "CustomAuth");
        _currentUser = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    public async Task LogoutAsync()
    {
        await _sessionStorage.DeleteAsync(UserSessionKey);
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    private class UserSession
    {
        public required string UserId { get; set; }
        public required string Email { get; set; }
        public string Tier { get; set; } = "free";
    }
}
