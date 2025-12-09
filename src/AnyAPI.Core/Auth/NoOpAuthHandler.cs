namespace AnyAPI.Core.Auth;

/// <summary>
/// Auth handler for APIs that require no authentication.
/// </summary>
public class NoOpAuthHandler : IAuthHandler
{
    public Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> RefreshIfNeededAsync(CancellationToken ct = default)
        => Task.FromResult(false);
}
