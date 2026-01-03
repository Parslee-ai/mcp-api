using McpApi.Core.Models;

namespace McpApi.Core.Storage;

public interface IRefreshTokenStore
{
    Task<RefreshToken?> GetByIdAsync(string userId, string tokenId, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveTokensForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task UpsertAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task RevokeAsync(string userId, string tokenId, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task DeleteExpiredAsync(CancellationToken cancellationToken = default);
}
