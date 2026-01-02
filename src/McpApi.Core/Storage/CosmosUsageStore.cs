namespace McpApi.Core.Storage;

using System.Net;
using McpApi.Core.Models;
using Microsoft.Azure.Cosmos;

/// <summary>
/// Cosmos DB implementation of IUsageStore.
/// Uses userId as partition key for multi-tenant isolation.
/// </summary>
public class CosmosUsageStore : IUsageStore
{
    private readonly Container _container;

    public CosmosUsageStore(CosmosClient cosmosClient, string databaseName)
    {
        var database = cosmosClient.GetDatabase(databaseName);
        _container = database.GetContainer(Constants.Cosmos.Usage);
    }

    public async Task<UsageRecord?> GetAsync(string userId, string yearMonth, CancellationToken ct = default)
    {
        var id = UsageRecord.CreateId(userId, yearMonth);
        try
        {
            var response = await _container.ReadItemAsync<UsageRecord>(
                id,
                new PartitionKey(userId),
                cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<UsageRecord?> GetCurrentMonthAsync(string userId, CancellationToken ct = default)
    {
        var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");
        return GetAsync(userId, yearMonth, ct);
    }

    public async Task<IReadOnlyList<UsageRecord>> GetRangeAsync(
        string userId,
        string startYearMonth,
        string endYearMonth,
        CancellationToken ct = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId AND c.yearMonth >= @start AND c.yearMonth <= @end ORDER BY c.yearMonth")
            .WithParameter("@userId", userId)
            .WithParameter("@start", startYearMonth)
            .WithParameter("@end", endYearMonth);

        var results = new List<UsageRecord>();
        using var iterator = _container.GetItemQueryIterator<UsageRecord>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<UsageRecord> IncrementApiCallCountAsync(string userId, CancellationToken ct = default)
    {
        var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");
        var id = UsageRecord.CreateId(userId, yearMonth);
        var now = DateTime.UtcNow;

        // Try to get existing record
        var existing = await GetAsync(userId, yearMonth, ct);

        if (existing != null)
        {
            // Update existing record
            existing.ApiCallCount++;
            existing.LastCallAt = now;
            return await UpsertAsync(existing, ct);
        }

        // Create new record for this month
        var record = new UsageRecord
        {
            Id = id,
            UserId = userId,
            YearMonth = yearMonth,
            ApiCallCount = 1,
            FirstCallAt = now,
            LastCallAt = now
        };

        return await UpsertAsync(record, ct);
    }

    public async Task<UsageRecord> UpsertAsync(UsageRecord record, CancellationToken ct = default)
    {
        var response = await _container.UpsertItemAsync(
            record,
            new PartitionKey(record.UserId),
            cancellationToken: ct);
        return response.Resource;
    }
}
