using System.Net;
using McpApi.Core.Models;
using Microsoft.Azure.Cosmos;

namespace McpApi.Core.Storage;

public class CosmosRefreshTokenStore : IRefreshTokenStore
{
    private readonly Container _container;

    public CosmosRefreshTokenStore(CosmosClient cosmosClient, string databaseName)
    {
        var database = cosmosClient.GetDatabase(databaseName);
        _container = database.GetContainer(Constants.Cosmos.RefreshTokens);
    }

    public async Task<RefreshToken?> GetByIdAsync(string userId, string tokenId, CancellationToken ct = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<RefreshToken>(
                tokenId,
                new PartitionKey(userId),
                cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.tokenHash = @tokenHash AND c.isRevoked = false")
            .WithParameter("@tokenHash", tokenHash);

        using var iterator = _container.GetItemQueryIterator<RefreshToken>(query);

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            return response.FirstOrDefault();
        }

        return null;
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveTokensForUserAsync(string userId, CancellationToken ct = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId AND c.isRevoked = false AND c.expiresAt > @now ORDER BY c.createdAt DESC")
            .WithParameter("@userId", userId)
            .WithParameter("@now", DateTime.UtcNow);

        var results = new List<RefreshToken>();
        using var iterator = _container.GetItemQueryIterator<RefreshToken>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }

        return results;
    }

    public async Task UpsertAsync(RefreshToken token, CancellationToken ct = default)
    {
        await _container.UpsertItemAsync(
            token,
            new PartitionKey(token.UserId),
            cancellationToken: ct);
    }

    public async Task RevokeAsync(string userId, string tokenId, CancellationToken ct = default)
    {
        var token = await GetByIdAsync(userId, tokenId, ct);
        if (token != null && !token.IsRevoked)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            await UpsertAsync(token, ct);
        }
    }

    public async Task RevokeAllForUserAsync(string userId, CancellationToken ct = default)
    {
        var activeTokens = await GetActiveTokensForUserAsync(userId, ct);
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            await UpsertAsync(token, ct);
        }
    }

    public async Task DeleteExpiredAsync(CancellationToken ct = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.expiresAt < @now")
            .WithParameter("@now", DateTime.UtcNow.AddDays(-7)); // Keep for 7 days after expiry for audit

        using var iterator = _container.GetItemQueryIterator<RefreshToken>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            foreach (var token in response)
            {
                try
                {
                    await _container.DeleteItemAsync<RefreshToken>(
                        token.Id,
                        new PartitionKey(token.UserId),
                        cancellationToken: ct);
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Already deleted
                }
            }
        }
    }

    public async Task DeleteAllForUserAsync(string userId, CancellationToken ct = default)
    {
        // Query all refresh tokens for the user (including revoked/expired)
        var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId")
            .WithParameter("@userId", userId);

        using var iterator = _container.GetItemQueryIterator<RefreshToken>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        var deleteTasks = new List<Task>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            foreach (var token in response)
            {
                deleteTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _container.DeleteItemAsync<RefreshToken>(
                            token.Id,
                            new PartitionKey(userId),
                            cancellationToken: ct);
                    }
                    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        // Already deleted
                    }
                }, ct));
            }
        }

        await Task.WhenAll(deleteTasks);
    }
}
