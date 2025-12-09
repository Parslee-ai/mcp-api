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
        _apiContainer = _client.GetContainer(databaseName, "api-registrations");
        _endpointContainer = _client.GetContainer(databaseName, "api-endpoints");
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
        // Delete all endpoints first
        var endpoints = await GetEndpointsAsync(id, ct);
        foreach (var endpoint in endpoints)
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
        }

        // Delete the API registration
        try
        {
            await _apiContainer.DeleteItemAsync<ApiRegistration>(
                id,
                new PartitionKey(id),
                cancellationToken: ct);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already deleted
        }
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

    public async Task SaveEndpointsAsync(string apiId, IEnumerable<ApiEndpoint> endpoints, CancellationToken ct = default)
    {
        // Materialize to list to avoid multiple enumeration
        var endpointList = endpoints.ToList();
        if (endpointList.Count == 0) return;

        // Batch upsert endpoints in parallel with throttling
        var semaphore = new SemaphoreSlim(10); // Max 10 concurrent operations
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

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await Task.CompletedTask;
    }
}
