using McpApi.Core.Models;

namespace McpApi.Web.Services;

/// <summary>
/// Service for accessing the currently authenticated user.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's ID, or null if not authenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets whether the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the current user, or null if not authenticated.
    /// </summary>
    Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
