namespace McpApi.Web.Services;

using McpApi.Core.GraphQL;
using McpApi.Core.Models;
using McpApi.Core.OpenApi;
using McpApi.Core.Postman;
using McpApi.Core.Services;
using McpApi.Core.Storage;
using McpApi.Core.Validation;

/// <summary>
/// Business logic service for managing API registrations.
/// Requires authenticated user context for multi-tenant isolation.
/// </summary>
public class ApiManagementService
{
    private readonly IApiRegistrationStore _store;
    private readonly IOpenApiParser _parser;
    private readonly OpenApiDiscovery _discovery;
    private readonly PostmanCollectionParser _postmanParser;
    private readonly GraphQLSchemaParser _graphqlParser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly IUsageTrackingService _usageTracking;

    public ApiManagementService(
        IApiRegistrationStore store,
        IOpenApiParser parser,
        OpenApiDiscovery discovery,
        PostmanCollectionParser postmanParser,
        GraphQLSchemaParser graphqlParser,
        IHttpClientFactory httpClientFactory,
        ICurrentUserService currentUser,
        IUsageTrackingService usageTracking)
    {
        _store = store;
        _parser = parser;
        _discovery = discovery;
        _postmanParser = postmanParser;
        _graphqlParser = graphqlParser;
        _httpClientFactory = httpClientFactory;
        _currentUser = currentUser;
        _usageTracking = usageTracking;
    }

    private string GetRequiredUserId()
    {
        return _currentUser.UserId
            ?? throw new InvalidOperationException("User must be authenticated to perform this operation.");
    }

    private async Task<string> GetUserTierAsync(CancellationToken ct = default)
    {
        var user = await _currentUser.GetCurrentUserAsync(ct);
        return user?.Tier ?? "free";
    }

    private async Task CheckApiLimitsAsync(string userId, string userTier, int endpointCount, CancellationToken ct)
    {
        var currentApiCount = await _store.GetApiCountAsync(userId, ct);
        if (!_usageTracking.CanRegisterApi(userTier, currentApiCount))
        {
            var (_, maxApis, _) = TierLimits.GetLimits(userTier);
            throw new UsageLimitExceededException("APIs", currentApiCount, maxApis);
        }

        var (_, _, maxEndpoints) = TierLimits.GetLimits(userTier);
        if (maxEndpoints != int.MaxValue && endpointCount > maxEndpoints)
        {
            throw new InvalidOperationException(
                $"This API has {endpointCount} endpoints, which exceeds your tier limit of {maxEndpoints}. " +
                "Upgrade your plan to register APIs with more endpoints.");
        }
    }

    /// <summary>
    /// Discovers and registers an API from a base URL.
    /// Supports OpenAPI specs, Postman Collections, and GraphQL endpoints.
    /// </summary>
    public async Task<ApiRegistration> RegisterApiAsync(
        string baseUrl,
        string? specUrl = null,
        CancellationToken ct = default)
    {
        // Validate URLs to prevent SSRF attacks
        UrlValidator.ValidateExternalUrl(baseUrl);
        if (!string.IsNullOrEmpty(specUrl))
        {
            UrlValidator.ValidateExternalUrl(specUrl);
        }

        // Check if this is a GraphQL endpoint (either by URL pattern or explicit)
        var targetUrl = specUrl ?? baseUrl;
        if (GraphQLSchemaParser.LooksLikeGraphQLEndpoint(targetUrl))
        {
            return await RegisterGraphQLApiAsync(targetUrl, ct);
        }

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
        using var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(specUrl, ct);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(ct);

        // Detect format: Postman Collection, GraphQL SDL, or OpenAPI spec
        if (PostmanCollectionParser.IsPostmanCollection(content))
        {
            registration = _postmanParser.ParseFromJson(content, specUrl);
        }
        else if (GraphQLSchemaParser.IsGraphQLSchema(content))
        {
            registration = _graphqlParser.ParseFromSdl(content, baseUrl);
        }
        else
        {
            // Parse as OpenAPI spec (handles both OpenAPI 3.x and Swagger 2.0)
            registration = await _parser.ParseAsync(specUrl, ct);
        }

        var userId = GetRequiredUserId();
        var userTier = await GetUserTierAsync(ct);

        // Check if API already exists for this user
        if (await _store.ExistsAsync(userId, registration.Id, ct))
        {
            throw new InvalidOperationException($"API '{registration.Id}' is already registered.");
        }

        // Extract endpoints before saving (they'll be stored separately)
        var endpoints = registration.Endpoints.ToList();

        // Check usage limits (API count and endpoint count)
        await CheckApiLimitsAsync(userId, userTier, endpoints.Count, ct);

        // Set user ownership
        registration.UserId = userId;

        // Save API metadata to database
        await _store.UpsertAsync(registration, ct);

        // Save endpoints separately
        await _store.SaveEndpointsAsync(userId, registration.Id, endpoints, ct);

        return registration;
    }

    /// <summary>
    /// Gets all registered APIs for the current user.
    /// </summary>
    public Task<IReadOnlyList<ApiRegistration>> GetAllApisAsync(CancellationToken ct = default)
        => _store.GetAllAsync(GetRequiredUserId(), ct);

    /// <summary>
    /// Gets a specific API by ID for the current user.
    /// </summary>
    public Task<ApiRegistration?> GetApiAsync(string id, CancellationToken ct = default)
        => _store.GetAsync(GetRequiredUserId(), id, ct);

    /// <summary>
    /// Gets a specific API by ID with its endpoints loaded for the current user.
    /// </summary>
    public async Task<ApiRegistration?> GetApiWithEndpointsAsync(string id, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var api = await _store.GetAsync(userId, id, ct);
        if (api != null)
        {
            var endpoints = await _store.GetEndpointsAsync(userId, id, ct);
            api.Endpoints = endpoints.ToList();
        }
        return api;
    }

    /// <summary>
    /// Gets endpoints for an API.
    /// </summary>
    public Task<IReadOnlyList<ApiEndpoint>> GetEndpointsAsync(string apiId, CancellationToken ct = default)
        => _store.GetEndpointsAsync(GetRequiredUserId(), apiId, ct);

    /// <summary>
    /// Gets endpoint count for an API.
    /// </summary>
    public Task<int> GetEndpointCountAsync(string apiId, CancellationToken ct = default)
        => _store.GetEndpointCountAsync(GetRequiredUserId(), apiId, ct);

    /// <summary>
    /// Gets enabled endpoint count for an API.
    /// </summary>
    public Task<int> GetEnabledEndpointCountAsync(string apiId, CancellationToken ct = default)
        => _store.GetEnabledEndpointCountAsync(GetRequiredUserId(), apiId, ct);

    /// <summary>
    /// Updates an API registration.
    /// </summary>
    public Task<ApiRegistration> UpdateApiAsync(ApiRegistration api, CancellationToken ct = default)
    {
        // Ensure the API belongs to the current user
        var userId = GetRequiredUserId();
        if (api.UserId != userId)
        {
            throw new InvalidOperationException("Cannot update an API that belongs to another user.");
        }
        return _store.UpsertAsync(api, ct);
    }

    /// <summary>
    /// Deletes an API registration.
    /// </summary>
    public Task DeleteApiAsync(string id, CancellationToken ct = default)
        => _store.DeleteAsync(GetRequiredUserId(), id, ct);

    /// <summary>
    /// Toggles the enabled status of an API.
    /// </summary>
    public async Task<ApiRegistration> ToggleApiAsync(string id, bool enabled, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var api = await _store.GetAsync(userId, id, ct)
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
        var userId = GetRequiredUserId();
        var endpoint = await _store.GetEndpointAsync(userId, apiId, endpointId, ct)
            ?? throw new InvalidOperationException($"Endpoint '{endpointId}' not found in API '{apiId}'.");

        endpoint.IsEnabled = enabled;
        return await _store.UpdateEndpointAsync(endpoint, ct);
    }

    /// <summary>
    /// Refreshes an API by re-parsing its OpenAPI spec.
    /// </summary>
    public async Task<ApiRegistration> RefreshApiAsync(string id, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();
        var existingApi = await _store.GetAsync(userId, id, ct)
            ?? throw new InvalidOperationException($"API '{id}' not found.");

        if (string.IsNullOrEmpty(existingApi.SpecUrl))
        {
            throw new InvalidOperationException($"API '{id}' does not have a spec URL to refresh from.");
        }

        // Get existing endpoints to preserve enabled states
        var existingEndpoints = await _store.GetEndpointsAsync(userId, id, ct);

        var refreshedApi = await _parser.ParseAsync(existingApi.SpecUrl, ct);
        var newEndpoints = refreshedApi.Endpoints.ToList();

        // Preserve settings from existing API
        refreshedApi.Id = existingApi.Id;
        refreshedApi.UserId = userId;
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
        await _store.SaveEndpointsAsync(userId, id, newEndpoints, ct);

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
        var userId = GetRequiredUserId();
        var api = await _store.GetAsync(userId, id, ct)
            ?? throw new InvalidOperationException($"API '{id}' not found.");

        api.Auth = authConfig;
        return await _store.UpsertAsync(api, ct);
    }

    /// <summary>
    /// Registers a GraphQL API by introspecting the endpoint.
    /// </summary>
    private async Task<ApiRegistration> RegisterGraphQLApiAsync(string graphqlUrl, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var userTier = await GetUserTierAsync(ct);
        var registration = await _graphqlParser.ParseFromEndpointAsync(graphqlUrl, ct);

        // Check if API already exists for this user
        if (await _store.ExistsAsync(userId, registration.Id, ct))
        {
            throw new InvalidOperationException($"API '{registration.Id}' is already registered.");
        }

        // Extract endpoints before saving
        var endpoints = registration.Endpoints.ToList();

        // Check usage limits (API count and endpoint count)
        await CheckApiLimitsAsync(userId, userTier, endpoints.Count, ct);

        // Set user ownership
        registration.UserId = userId;

        // Save API metadata to database
        await _store.UpsertAsync(registration, ct);

        // Save endpoints separately
        await _store.SaveEndpointsAsync(userId, registration.Id, endpoints, ct);

        return registration;
    }
}
