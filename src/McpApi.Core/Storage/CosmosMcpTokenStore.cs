namespace McpApi.Core.Storage;

using System.Net;
using McpApi.Core.Models;
using Microsoft.Azure.Cosmos;

/// <summary>
/// Cosmos DB implementation of IMcpTokenStore.
/// Uses userId as partition key for multi-tenant isolation.
/// </summary>
public class CosmosMcpTokenStore : IMcpTokenStore
{
    private readonly Container _container;

    public CosmosMcpTokenStore(CosmosClient cosmosClient, string databaseName)
    {
        var database = cosmosClient.GetDatabase(databaseName);
        _container = database.GetContainer(Constants.Cosmos.Tokens);
    }

    public async Task<McpToken?> GetAsync(string userId, string tokenId, CancellationToken ct = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<McpToken>(
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

    public async Task<McpToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        // Cross-partition query to find token by hash
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.tokenHash = @tokenHash AND c.isRevoked = false")
            .WithParameter("@tokenHash", tokenHash);

        using var iterator = _container.GetItemQueryIterator<McpToken>(query);

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            return response.FirstOrDefault();
        }

        return null;
    }

    public async Task<IReadOnlyList<McpToken>> GetAllForUserAsync(string userId, CancellationToken ct = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId ORDER BY c.createdAt DESC")
            .WithParameter("@userId", userId);

        var results = new List<McpToken>();
        using var iterator = _container.GetItemQueryIterator<McpToken>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<McpToken> UpsertAsync(McpToken token, CancellationToken ct = default)
    {
        var response = await _container.UpsertItemAsync(
            token,
            new PartitionKey(token.UserId),
            cancellationToken: ct);
        return response.Resource;
    }

    public async Task DeleteAsync(string userId, string tokenId, CancellationToken ct = default)
    {
        try
        {
            await _container.DeleteItemAsync<McpToken>(
                tokenId,
                new PartitionKey(userId),
                cancellationToken: ct);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already deleted, ignore
        }
    }

    public async Task UpdateLastUsedAsync(string userId, string tokenId, CancellationToken ct = default)
    {
        var token = await GetAsync(userId, tokenId, ct);
        if (token != null)
        {
            token.LastUsedAt = DateTime.UtcNow;
            await UpsertAsync(token, ct);
        }
    }

    public async Task DeleteAllForUserAsync(string userId, CancellationToken ct = default)
    {
        var tokens = await GetAllForUserAsync(userId, ct);
        var deleteTasks = tokens.Select(t => DeleteAsync(userId, t.Id, ct));
        await Task.WhenAll(deleteTasks);
    }
}
