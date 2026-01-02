namespace McpApi.Core.Storage;

using McpApi.Core.Models;

/// <summary>
/// Repository interface for API registrations.
/// </summary>
public interface IApiRegistrationStore
{
    /// <summary>Gets an API registration by ID (without endpoints).</summary>
    Task<ApiRegistration?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Gets all API registrations (without endpoints).</summary>
    Task<IReadOnlyList<ApiRegistration>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Gets only enabled API registrations (without endpoints).</summary>
    Task<IReadOnlyList<ApiRegistration>> GetEnabledAsync(CancellationToken ct = default);

    /// <summary>Creates or updates an API registration (metadata only, not endpoints).</summary>
    Task<ApiRegistration> UpsertAsync(ApiRegistration registration, CancellationToken ct = default);

    /// <summary>Deletes an API registration and all its endpoints.</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>Checks if an API registration exists.</summary>
    Task<bool> ExistsAsync(string id, CancellationToken ct = default);

    /// <summary>Gets all endpoints for an API.</summary>
    Task<IReadOnlyList<ApiEndpoint>> GetEndpointsAsync(string apiId, CancellationToken ct = default);

    /// <summary>Gets enabled endpoints for an API.</summary>
    Task<IReadOnlyList<ApiEndpoint>> GetEnabledEndpointsAsync(string apiId, CancellationToken ct = default);

    /// <summary>Gets a specific endpoint by API ID and endpoint ID.</summary>
    Task<ApiEndpoint?> GetEndpointAsync(string apiId, string endpointId, CancellationToken ct = default);

    /// <summary>
    /// Saves all endpoints for an API (batch upsert).
    /// Sets <see cref="ApiEndpoint.ApiId"/> on each endpoint.
    /// </summary>
    Task SaveEndpointsAsync(string apiId, IEnumerable<ApiEndpoint> endpoints, CancellationToken ct = default);

    /// <summary>Updates a single endpoint.</summary>
    Task<ApiEndpoint> UpdateEndpointAsync(ApiEndpoint endpoint, CancellationToken ct = default);

    /// <summary>Gets endpoint count for an API.</summary>
    Task<int> GetEndpointCountAsync(string apiId, CancellationToken ct = default);

    /// <summary>Gets enabled endpoint count for an API.</summary>
    Task<int> GetEnabledEndpointCountAsync(string apiId, CancellationToken ct = default);

    /// <summary>Searches enabled endpoints across all enabled APIs.</summary>
    Task<IReadOnlyList<EndpointSearchResult>> SearchEndpointsAsync(string query, int limit = 20, CancellationToken ct = default);
}

/// <summary>
/// Result of an endpoint search including API context.
/// </summary>
public record EndpointSearchResult(
    string ApiId,
    string ApiName,
    string OperationId,
    string Method,
    string Path,
    string? Summary);
