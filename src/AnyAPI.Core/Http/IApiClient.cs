namespace AnyAPI.Core.Http;

using AnyAPI.Core.Models;

/// <summary>
/// Interface for making dynamic API calls.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Executes an API call based on registration and endpoint definition.
    /// </summary>
    Task<ApiResponse> ExecuteAsync(
        ApiRegistration api,
        ApiEndpoint endpoint,
        Dictionary<string, object?> parameters,
        CancellationToken ct = default);
}
