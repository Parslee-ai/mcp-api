namespace AnyAPI.Core.Storage;

using System.Net;
using System.Text.Json;
using AnyAPI.Core.Models;
using Microsoft.Azure.Cosmos;

/// <summary>
/// Cosmos DB implementation of IApiRegistrationStore.
/// Uses separate containers for API metadata and endpoints to handle large specs.
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

    public async Task<ApiRegistration?> GetAsync(string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _apiContainer.ReadItemAsync<ApiRegistration>(
                id,
                new PartitionKey(id),
                cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ApiRegistration>> GetAllAsync(CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c");
        return await ExecuteApiQueryAsync(query, ct);
    }

    public async Task<IReadOnlyList<ApiRegistration>> GetEnabledAsync(CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.isEnabled = true");
        return await ExecuteApiQueryAsync(query, ct);
    }

    public async Task<ApiRegistration> UpsertAsync(ApiRegistration registration, CancellationToken ct = default)
    {
        // Clear endpoints from the registration - they're stored separately
        registration.Endpoints = [];

        var response = await _apiContainer.UpsertItemAsync(
            registration,
            new PartitionKey(registration.Id),
            new ItemRequestOptions { IfMatchEtag = registration.ETag },
            ct);

        registration.ETag = response.ETag;
        return registration;
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        // Delete API registration first - if this fails, no orphaned endpoints are created
        // Endpoints without a parent API are harmless and can be cleaned up later
        try
        {
            await _apiContainer.DeleteItemAsync<ApiRegistration>(
                id,
                new PartitionKey(id),
                cancellationToken: ct);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already deleted - still try to clean up any orphaned endpoints
        }

        // Delete all endpoints (best effort cleanup)
        var endpoints = await GetEndpointsAsync(id, ct);
        var deleteTasks = endpoints.Select(async endpoint =>
        {
            try
            {
                await _endpointContainer.DeleteItemAsync<ApiEndpoint>(
                    endpoint.Id,
                    new PartitionKey(id),
                    cancellationToken: ct);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Already deleted
            }
        });

        await Task.WhenAll(deleteTasks);
    }

    public async Task<bool> ExistsAsync(string id, CancellationToken ct = default)
    {
        var registration = await GetAsync(id, ct);
        return registration != null;
    }

    public async Task<IReadOnlyList<ApiEndpoint>> GetEndpointsAsync(string apiId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.apiId = @apiId")
            .WithParameter("@apiId", apiId);
        return await ExecuteEndpointQueryAsync(query, ct);
    }

    public async Task<IReadOnlyList<ApiEndpoint>> GetEnabledEndpointsAsync(string apiId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.apiId = @apiId AND c.isEnabled = true")
            .WithParameter("@apiId", apiId);
        return await ExecuteEndpointQueryAsync(query, ct);
    }

    public async Task<ApiEndpoint?> GetEndpointAsync(string apiId, string endpointId, CancellationToken ct = default)
    {
        try
        {
            var response = await _endpointContainer.ReadItemAsync<ApiEndpoint>(
                endpointId,
                new PartitionKey(apiId),
                cancellationToken: ct);
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
    /// <remarks>
    /// This method sets <see cref="ApiEndpoint.ApiId"/> on each endpoint before saving.
    /// Callers should be aware that input objects are modified.
    /// </remarks>
    public async Task SaveEndpointsAsync(string apiId, IEnumerable<ApiEndpoint> endpoints, CancellationToken ct = default)
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

            await semaphore.WaitAsync(ct);

            var capturedEndpoint = endpoint; // Capture for closure
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _endpointContainer.UpsertItemAsync(
                        capturedEndpoint,
                        new PartitionKey(apiId),
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
            new PartitionKey(endpoint.ApiId),
            cancellationToken: ct);
        return response.Resource;
    }

    public async Task<int> GetEndpointCountAsync(string apiId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.apiId = @apiId")
            .WithParameter("@apiId", apiId);

        using var iterator = _endpointContainer.GetItemQueryIterator<int>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            return response.FirstOrDefault();
        }
        return 0;
    }

    public async Task<int> GetEnabledEndpointCountAsync(string apiId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.apiId = @apiId AND c.isEnabled = true")
            .WithParameter("@apiId", apiId);

        using var iterator = _endpointContainer.GetItemQueryIterator<int>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            return response.FirstOrDefault();
        }
        return 0;
    }

    private async Task<IReadOnlyList<ApiRegistration>> ExecuteApiQueryAsync(
        QueryDefinition query,
        CancellationToken ct)
    {
        var results = new List<ApiRegistration>();
        using var iterator = _apiContainer.GetItemQueryIterator<ApiRegistration>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }

        return results;
    }

    private async Task<IReadOnlyList<ApiEndpoint>> ExecuteEndpointQueryAsync(
        QueryDefinition query,
        CancellationToken ct)
    {
        var results = new List<ApiEndpoint>();
        using var iterator = _endpointContainer.GetItemQueryIterator<ApiEndpoint>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<IReadOnlyList<EndpointSearchResult>> SearchEndpointsAsync(
        string query,
        int limit = 20,
        CancellationToken ct = default)
    {
        // Get enabled APIs first (small set, cached in practice)
        var enabledApis = await GetEnabledAsync(ct);
        var apiLookup = enabledApis.ToDictionary(a => a.Id, a => a.DisplayName);
        var apiIds = apiLookup.Keys.ToList();

        if (apiIds.Count == 0)
            return [];

        // Build parameterized query for enabled endpoints matching the search
        var queryLower = query.ToLowerInvariant();
        var queryDef = new QueryDefinition(
            @"SELECT c.apiId, c.operationId, c.method, c.path, c.summary
              FROM c
              WHERE c.isEnabled = true
              AND ARRAY_CONTAINS(@apiIds, c.apiId)
              AND (CONTAINS(LOWER(c.operationId), @query)
                   OR CONTAINS(LOWER(c.path), @query)
                   OR CONTAINS(LOWER(c.summary), @query))")
            .WithParameter("@apiIds", apiIds)
            .WithParameter("@query", queryLower);

        var results = new List<EndpointSearchResult>();
        using var iterator = _endpointContainer.GetItemQueryIterator<EndpointSearchMatch>(
            queryDef,
            requestOptions: new QueryRequestOptions { MaxItemCount = limit });

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

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
