namespace AnyAPI.Web.Services;

using AnyAPI.Core.Models;
using AnyAPI.Core.OpenApi;
using AnyAPI.Core.Postman;
using AnyAPI.Core.Storage;

/// <summary>
/// Business logic service for managing API registrations.
/// </summary>
public class ApiManagementService
{
    private readonly IApiRegistrationStore _store;
    private readonly IOpenApiParser _parser;
    private readonly OpenApiDiscovery _discovery;
    private readonly PostmanCollectionParser _postmanParser;

    public ApiManagementService(
        IApiRegistrationStore store,
        IOpenApiParser parser,
        OpenApiDiscovery discovery,
        PostmanCollectionParser postmanParser)
    {
        _store = store;
        _parser = parser;
        _discovery = discovery;
        _postmanParser = postmanParser;
    }

    /// <summary>
    /// Discovers and registers an API from a base URL.
    /// Supports both OpenAPI specs and Postman Collections.
    /// </summary>
    public async Task<ApiRegistration> RegisterApiAsync(
        string baseUrl,
        string? specUrl = null,
        CancellationToken ct = default)
    {
        // Discover spec URL if not provided
        if (string.IsNullOrEmpty(specUrl))
        {
            specUrl = await _discovery.DiscoverAsync(baseUrl, ct);
            if (specUrl == null)
            {
                throw new InvalidOperationException(
                    $"Could not discover OpenAPI spec at {baseUrl}. Please provide the spec URL manually.");
            }
        }

        // Fetch the spec to detect format
        ApiRegistration registration;
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(specUrl, ct);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(ct);

        // Detect if this is a Postman Collection or OpenAPI spec
        if (PostmanCollectionParser.IsPostmanCollection(content))
        {
            registration = _postmanParser.ParseFromJson(content, specUrl);
        }
        else
        {
            // Parse as OpenAPI spec
            registration = await _parser.ParseAsync(specUrl, ct);
        }

        // Check if API already exists
        if (await _store.ExistsAsync(registration.Id, ct))
        {
            throw new InvalidOperationException($"API '{registration.Id}' is already registered.");
        }

        // Extract endpoints before saving (they'll be stored separately)
        var endpoints = registration.Endpoints.ToList();

        // Save API metadata to database
        await _store.UpsertAsync(registration, ct);

        // Save endpoints separately
        await _store.SaveEndpointsAsync(registration.Id, endpoints, ct);

        return registration;
    }

    /// <summary>
    /// Gets all registered APIs.
    /// </summary>
    public Task<IReadOnlyList<ApiRegistration>> GetAllApisAsync(CancellationToken ct = default)
        => _store.GetAllAsync(ct);

    /// <summary>
    /// Gets a specific API by ID.
    /// </summary>
    public Task<ApiRegistration?> GetApiAsync(string id, CancellationToken ct = default)
        => _store.GetAsync(id, ct);

    /// <summary>
    /// Gets a specific API by ID with its endpoints loaded.
    /// </summary>
    public async Task<ApiRegistration?> GetApiWithEndpointsAsync(string id, CancellationToken ct = default)
    {
        var api = await _store.GetAsync(id, ct);
        if (api != null)
        {
            var endpoints = await _store.GetEndpointsAsync(id, ct);
            api.Endpoints = endpoints.ToList();
        }
        return api;
    }

    /// <summary>
    /// Gets endpoints for an API.
    /// </summary>
    public Task<IReadOnlyList<ApiEndpoint>> GetEndpointsAsync(string apiId, CancellationToken ct = default)
        => _store.GetEndpointsAsync(apiId, ct);

    /// <summary>
    /// Gets endpoint count for an API.
    /// </summary>
    public Task<int> GetEndpointCountAsync(string apiId, CancellationToken ct = default)
        => _store.GetEndpointCountAsync(apiId, ct);

    /// <summary>
    /// Gets enabled endpoint count for an API.
    /// </summary>
    public Task<int> GetEnabledEndpointCountAsync(string apiId, CancellationToken ct = default)
        => _store.GetEnabledEndpointCountAsync(apiId, ct);

    /// <summary>
    /// Updates an API registration.
    /// </summary>
    public Task<ApiRegistration> UpdateApiAsync(ApiRegistration api, CancellationToken ct = default)
        => _store.UpsertAsync(api, ct);

    /// <summary>
    /// Deletes an API registration.
    /// </summary>
    public Task DeleteApiAsync(string id, CancellationToken ct = default)
        => _store.DeleteAsync(id, ct);

    /// <summary>
    /// Toggles the enabled status of an API.
    /// </summary>
    public async Task<ApiRegistration> ToggleApiAsync(string id, bool enabled, CancellationToken ct = default)
    {
        var api = await _store.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"API '{id}' not found.");

        api.IsEnabled = enabled;
        return await _store.UpsertAsync(api, ct);
    }

    /// <summary>
    /// Toggles the enabled status of an endpoint.
    /// </summary>
    public async Task<ApiEndpoint> ToggleEndpointAsync(
        string apiId,
        string endpointId,
        bool enabled,
        CancellationToken ct = default)
    {
        var endpoint = await _store.GetEndpointAsync(apiId, endpointId, ct)
            ?? throw new InvalidOperationException($"Endpoint '{endpointId}' not found in API '{apiId}'.");

        endpoint.IsEnabled = enabled;
        return await _store.UpdateEndpointAsync(endpoint, ct);
    }

    /// <summary>
    /// Refreshes an API by re-parsing its OpenAPI spec.
    /// </summary>
    public async Task<ApiRegistration> RefreshApiAsync(string id, CancellationToken ct = default)
    {
        var existingApi = await _store.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"API '{id}' not found.");

        if (string.IsNullOrEmpty(existingApi.SpecUrl))
        {
            throw new InvalidOperationException($"API '{id}' does not have a spec URL to refresh from.");
        }

        // Get existing endpoints to preserve enabled states
        var existingEndpoints = await _store.GetEndpointsAsync(id, ct);

        var refreshedApi = await _parser.ParseAsync(existingApi.SpecUrl, ct);
        var newEndpoints = refreshedApi.Endpoints.ToList();

        // Preserve settings from existing API
        refreshedApi.Id = existingApi.Id;
        refreshedApi.IsEnabled = existingApi.IsEnabled;
        refreshedApi.Auth = existingApi.Auth;
        refreshedApi.CreatedAt = existingApi.CreatedAt;
        refreshedApi.ETag = existingApi.ETag;

        // Preserve endpoint enabled states
        foreach (var endpoint in newEndpoints)
        {
            var existingEndpoint = existingEndpoints
                .FirstOrDefault(e => e.OperationId == endpoint.OperationId);
            if (existingEndpoint != null)
            {
                endpoint.IsEnabled = existingEndpoint.IsEnabled;
            }
        }

        // Save API metadata
        await _store.UpsertAsync(refreshedApi, ct);

        // Save endpoints
        await _store.SaveEndpointsAsync(id, newEndpoints, ct);

        return refreshedApi;
    }

    /// <summary>
    /// Updates the authentication configuration for an API.
    /// </summary>
    public async Task<ApiRegistration> UpdateAuthConfigAsync(
        string id,
        AuthConfiguration authConfig,
        CancellationToken ct = default)
    {
        var api = await _store.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"API '{id}' not found.");

        api.Auth = authConfig;
        return await _store.UpsertAsync(api, ct);
    }
}
