using McpApi.Core.Models;

namespace McpApi.Api.Services;

/// <summary>
/// Service for accessing the currently authenticated user in API context.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    bool IsAuthenticated { get; }
    Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
