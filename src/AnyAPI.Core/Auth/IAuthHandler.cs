namespace AnyAPI.Core.Auth;

/// <summary>
/// Handles authentication for API requests.
/// </summary>
public interface IAuthHandler
{
    /// <summary>
    /// Applies authentication to the HTTP request.
    /// </summary>
    Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default);

    /// <summary>
    /// Refreshes credentials if needed (e.g., OAuth token refresh).
    /// Returns true if credentials were refreshed.
    /// </summary>
    Task<bool> RefreshIfNeededAsync(CancellationToken ct = default);
}
