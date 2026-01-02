namespace McpApi.Core.Http;

using McpApi.Core.Models;
using McpApi.Core.Secrets;

/// <summary>
/// Interface for making dynamic API calls.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Executes an API call based on registration and endpoint definition.
    /// </summary>
    /// <param name="api">The API registration containing base URL and auth config.</param>
    /// <param name="endpoint">The endpoint to call.</param>
    /// <param name="parameters">Request parameters.</param>
    /// <param name="userContext">User context for decrypting secrets. Required if secrets are encrypted.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ApiResponse> ExecuteAsync(
        ApiRegistration api,
        ApiEndpoint endpoint,
        Dictionary<string, object?> parameters,
        UserSecretContext? userContext = null,
        CancellationToken ct = default);
}
