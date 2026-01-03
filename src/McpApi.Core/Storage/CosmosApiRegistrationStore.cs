namespace McpApi.Core.Storage;

using System.Net;
using McpApi.Core.Models;
using Microsoft.Azure.Cosmos;

/// <summary>
/// Cosmos DB implementation of IApiRegistrationStore with multi-tenant support.
/// Uses userId as partition key for tenant isolation.
/// </summary>
public class CosmosApiRegistrationStore : IApiRegistrationStore, IAsyncDisposable
{
    private readonly CosmosClient _client;
    private readonly Container _apiContainer;
    private readonly Container _endpointContainer;

    public CosmosApiRegistrationStore(string connectionString, string databaseName)
    {
        var options = new CosmosClientOptions
        {
            // Use System.Text.Json serializer for proper polymorphic type support
            Serializer = new CosmosSystemTextJsonSerializer()
        };

        _client = new CosmosClient(connectionString, options);
        _apiContainer = _client.GetContainer(databaseName, Constants.Cosmos.ApiRegistrations);
        _endpointContainer = _client.GetContainer(databaseName, Constants.Cosmos.ApiEndpoints);
    }

    public async Task<ApiRegistration?> GetAsync(string userId, string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _apiContainer.ReadItemAsync<ApiRegistration>(
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

    public async Task<IReadOnlyList<ApiRegistration>> GetAllAsync(string userId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId")
            .WithParameter("@userId", userId);
        return await ExecuteApiQueryAsync(query, userId, ct);
    }

    public async Task<IReadOnlyList<ApiRegistration>> GetEnabledAsync(string userId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId AND c.isEnabled = true")
            .WithParameter("@userId", userId);
        return await ExecuteApiQueryAsync(query, userId, ct);
    }

    public async Task<ApiRegistration> UpsertAsync(ApiRegistration registration, CancellationToken ct = default)
    {
        // Clear endpoints from the registration - they're stored separately
        registration.Endpoints = [];

        var response = await _apiContainer.UpsertItemAsync(
            registration,
            new PartitionKey(registration.UserId),
            new ItemRequestOptions { IfMatchEtag = registration.ETag },
            ct);

        registration.ETag = response.ETag;
        return registration;
    }

    public async Task DeleteAsync(string userId, string id, CancellationToken ct = default)
    {
        // Delete API registration first - if this fails, no orphaned endpoints are created
        try
        {
            await _apiContainer.DeleteItemAsync<ApiRegistration>(
                id,
                new PartitionKey(userId),
                cancellationToken: ct);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already deleted - still try to clean up any orphaned endpoints
        }

        // Delete all endpoints (best effort cleanup)
        var endpoints = await GetEndpointsAsync(userId, id, ct);
        var deleteTasks = endpoints.Select(async endpoint =>
        {
            try
            {
                await _endpointContainer.DeleteItemAsync<ApiEndpoint>(
                    endpoint.Id,
                    new PartitionKey(userId),
                    cancellationToken: ct);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Already deleted
            }
        });

        await Task.WhenAll(deleteTasks);
    }

    public async Task<bool> ExistsAsync(string userId, string id, CancellationToken ct = default)
    {
        var registration = await GetAsync(userId, id, ct);
        return registration != null;
    }

    public async Task<IReadOnlyList<ApiEndpoint>> GetEndpointsAsync(string userId, string apiId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId AND c.apiId = @apiId")
            .WithParameter("@userId", userId)
            .WithParameter("@apiId", apiId);
        return await ExecuteEndpointQueryAsync(query, userId, ct);
    }

    public async Task<IReadOnlyList<ApiEndpoint>> GetEnabledEndpointsAsync(string userId, string apiId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId AND c.apiId = @apiId AND c.isEnabled = true")
            .WithParameter("@userId", userId)
            .WithParameter("@apiId", apiId);
        return await ExecuteEndpointQueryAsync(query, userId, ct);
    }

    public async Task<ApiEndpoint?> GetEndpointAsync(string userId, string apiId, string endpointId, CancellationToken ct = default)
    {
        try
        {
            var response = await _endpointContainer.ReadItemAsync<ApiEndpoint>(
                endpointId,
                new PartitionKey(userId),
                cancellationToken: ct);

            // Verify the endpoint belongs to the specified API
            if (response.Resource.ApiId != apiId)
                return null;

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Saves endpoints to the endpoint container.
    /// </summary>
    public async Task SaveEndpointsAsync(string userId, string apiId, IEnumerable<ApiEndpoint> endpoints, CancellationToken ct = default)
    {
        // Materialize to list to avoid multiple enumeration
        var endpointList = endpoints.ToList();
        if (endpointList.Count == 0) return;

        // Batch upsert endpoints in parallel with throttling
        var semaphore = new SemaphoreSlim(Constants.Cosmos.BatchConcurrency);
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        foreach (var endpoint in endpointList)
        {
            endpoint.ApiId = apiId;
            endpoint.UserId = userId;

            await semaphore.WaitAsync(ct);

            var capturedEndpoint = endpoint;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _endpointContainer.UpsertItemAsync(
                        capturedEndpoint,
                        new PartitionKey(userId),
                        cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(new Exception($"Failed to save endpoint {capturedEndpoint.Id}: {ex.Message}", ex));
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);

        if (exceptions.Count > 0)
        {
            throw new AggregateException($"Failed to save {exceptions.Count} of {endpointList.Count} endpoints", exceptions);
        }
    }

    public async Task<ApiEndpoint> UpdateEndpointAsync(ApiEndpoint endpoint, CancellationToken ct = default)
    {
        var response = await _endpointContainer.UpsertItemAsync(
            endpoint,
            new PartitionKey(endpoint.UserId),
            cancellationToken: ct);
        return response.Resource;
    }

    public async Task<int> GetEndpointCountAsync(string userId, string apiId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.userId = @userId AND c.apiId = @apiId")
            .WithParameter("@userId", userId)
            .WithParameter("@apiId", apiId);

        using var iterator = _endpointContainer.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            return response.FirstOrDefault();
        }
        return 0;
    }

    public async Task<int> GetEnabledEndpointCountAsync(string userId, string apiId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.userId = @userId AND c.apiId = @apiId AND c.isEnabled = true")
            .WithParameter("@userId", userId)
            .WithParameter("@apiId", apiId);

        using var iterator = _endpointContainer.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            return response.FirstOrDefault();
        }
        return 0;
    }

    public async Task<int> GetApiCountAsync(string userId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.userId = @userId")
            .WithParameter("@userId", userId);

        using var iterator = _apiContainer.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            return response.FirstOrDefault();
        }
        return 0;
    }

    private async Task<IReadOnlyList<ApiRegistration>> ExecuteApiQueryAsync(
        QueryDefinition query,
        string userId,
        CancellationToken ct)
    {
        var results = new List<ApiRegistration>();
        using var iterator = _apiContainer.GetItemQueryIterator<ApiRegistration>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }

        return results;
    }

    private async Task<IReadOnlyList<ApiEndpoint>> ExecuteEndpointQueryAsync(
        QueryDefinition query,
        string userId,
        CancellationToken ct)
    {
        var results = new List<ApiEndpoint>();
        using var iterator = _endpointContainer.GetItemQueryIterator<ApiEndpoint>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<IReadOnlyList<EndpointSearchResult>> SearchEndpointsAsync(
        string userId,
        string query,
        int limit = 20,
        CancellationToken ct = default)
    {
        // Get enabled APIs first
        var enabledApis = await GetEnabledAsync(userId, ct);
        var apiLookup = enabledApis.ToDictionary(a => a.Id, a => a.DisplayName);
        var apiIds = apiLookup.Keys.ToList();

        if (apiIds.Count == 0)
            return [];

        // Build parameterized query for enabled endpoints matching the search
        var queryLower = query.ToLowerInvariant();
        var queryDef = new QueryDefinition(
            @"SELECT c.apiId, c.operationId, c.method, c.path, c.summary
              FROM c
              WHERE c.userId = @userId
              AND c.isEnabled = true
              AND ARRAY_CONTAINS(@apiIds, c.apiId)
              AND (CONTAINS(LOWER(c.operationId), @query)
                   OR CONTAINS(LOWER(c.path), @query)
                   OR CONTAINS(LOWER(c.summary), @query))")
            .WithParameter("@userId", userId)
            .WithParameter("@apiIds", apiIds)
            .WithParameter("@query", queryLower);

        var results = new List<EndpointSearchResult>();
        using var iterator = _endpointContainer.GetItemQueryIterator<EndpointSearchMatch>(
            queryDef,
            requestOptions: new QueryRequestOptions
            {
                MaxItemCount = limit,
                PartitionKey = new PartitionKey(userId)
            });

        while (iterator.HasMoreResults && results.Count < limit)
        {
            var response = await iterator.ReadNextAsync(ct);
            foreach (var match in response)
            {
                if (results.Count >= limit) break;
                if (apiLookup.TryGetValue(match.ApiId, out var apiName))
                {
                    results.Add(new EndpointSearchResult(
                        match.ApiId,
                        apiName,
                        match.OperationId,
                        match.Method,
                        match.Path,
                        match.Summary));
                }
            }
        }

        return results;
    }

    private sealed class EndpointSearchMatch
    {
        public string ApiId { get; set; } = "";
        public string OperationId { get; set; } = "";
        public string Method { get; set; } = "";
        public string Path { get; set; } = "";
        public string? Summary { get; set; }
    }

    public async Task DeleteAllForUserAsync(string userId, CancellationToken ct = default)
    {
        // Get all APIs for the user
        var apis = await GetAllAsync(userId, ct);

        // Delete each API and its endpoints
        var deleteTasks = apis.Select(api => DeleteAsync(userId, api.Id, ct));
        await Task.WhenAll(deleteTasks);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
