namespace McpApi.Core.Http;

using McpApi.Core.Auth;
using McpApi.Core.Models;

/// <summary>
/// HTTP client for executing dynamic API calls.
/// </summary>
public class DynamicApiClient : IApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthHandlerFactory _authHandlerFactory;

    public DynamicApiClient(IHttpClientFactory httpClientFactory, IAuthHandlerFactory authHandlerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _authHandlerFactory = authHandlerFactory;
    }

    public async Task<ApiResponse> ExecuteAsync(
        ApiRegistration api,
        ApiEndpoint endpoint,
        Dictionary<string, object?> parameters,
        CancellationToken ct = default)
    {
        // Build request
        var request = RequestBuilder.Build(api.BaseUrl, endpoint, parameters);

        // Apply authentication
        var authHandler = CreateAuthHandler(api);
        await authHandler.ApplyAuthAsync(request, ct);

        // Add common headers
        request.Headers.TryAddWithoutValidation("User-Agent", Constants.Http.UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        // Execute request using a fresh HttpClient from the factory
        using var httpClient = _httpClientFactory.CreateClient(Constants.HttpClients.DynamicApi);
        var response = await httpClient.SendAsync(request, ct);

        // Build response
        var headers = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(
                h => h.Key,
                h => h.Value.ToArray());

        var body = await response.Content.ReadAsStringAsync(ct);

        return new ApiResponse
        {
            StatusCode = (int)response.StatusCode,
            ReasonPhrase = response.ReasonPhrase,
            Headers = headers,
            Body = body
        };
    }

    private IAuthHandler CreateAuthHandler(ApiRegistration api)
    {
        // Create a fresh auth handler each time to ensure config changes are respected
        // Auth handlers rely on ISecretProvider's internal cache for performance
        return _authHandlerFactory.Create(api.Auth);
    }
}
