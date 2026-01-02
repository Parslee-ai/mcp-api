namespace McpApi.Core.Storage;

using McpApi.Core.Models;

/// <summary>
/// Repository interface for API registrations with multi-tenant support.
/// All methods require a userId for tenant isolation.
/// </summary>
public interface IApiRegistrationStore
{
    /// <summary>Gets an API registration by ID for a specific user.</summary>
    Task<ApiRegistration?> GetAsync(string userId, string id, CancellationToken ct = default);

    /// <summary>Gets all API registrations for a specific user.</summary>
    Task<IReadOnlyList<ApiRegistration>> GetAllAsync(string userId, CancellationToken ct = default);

    /// <summary>Gets only enabled API registrations for a specific user.</summary>
    Task<IReadOnlyList<ApiRegistration>> GetEnabledAsync(string userId, CancellationToken ct = default);

    /// <summary>Creates or updates an API registration (metadata only, not endpoints).</summary>
    Task<ApiRegistration> UpsertAsync(ApiRegistration registration, CancellationToken ct = default);

    /// <summary>Deletes an API registration and all its endpoints for a specific user.</summary>
    Task DeleteAsync(string userId, string id, CancellationToken ct = default);

    /// <summary>Checks if an API registration exists for a specific user.</summary>
    Task<bool> ExistsAsync(string userId, string id, CancellationToken ct = default);

    /// <summary>Gets all endpoints for an API.</summary>
    Task<IReadOnlyList<ApiEndpoint>> GetEndpointsAsync(string userId, string apiId, CancellationToken ct = default);

    /// <summary>Gets enabled endpoints for an API.</summary>
    Task<IReadOnlyList<ApiEndpoint>> GetEnabledEndpointsAsync(string userId, string apiId, CancellationToken ct = default);

    /// <summary>Gets a specific endpoint by API ID and endpoint ID.</summary>
    Task<ApiEndpoint?> GetEndpointAsync(string userId, string apiId, string endpointId, CancellationToken ct = default);

    /// <summary>
    /// Saves all endpoints for an API (batch upsert).
    /// Sets <see cref="ApiEndpoint.ApiId"/> and <see cref="ApiEndpoint.UserId"/> on each endpoint.
    /// </summary>
    Task SaveEndpointsAsync(string userId, string apiId, IEnumerable<ApiEndpoint> endpoints, CancellationToken ct = default);

    /// <summary>Updates a single endpoint.</summary>
    Task<ApiEndpoint> UpdateEndpointAsync(ApiEndpoint endpoint, CancellationToken ct = default);

    /// <summary>Gets endpoint count for an API.</summary>
    Task<int> GetEndpointCountAsync(string userId, string apiId, CancellationToken ct = default);

    /// <summary>Gets enabled endpoint count for an API.</summary>
    Task<int> GetEnabledEndpointCountAsync(string userId, string apiId, CancellationToken ct = default);

    /// <summary>Searches enabled endpoints across all enabled APIs for a specific user.</summary>
    Task<IReadOnlyList<EndpointSearchResult>> SearchEndpointsAsync(string userId, string query, int limit = 20, CancellationToken ct = default);

    /// <summary>Gets count of API registrations for a user (for tier limits).</summary>
    Task<int> GetApiCountAsync(string userId, CancellationToken ct = default);
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
