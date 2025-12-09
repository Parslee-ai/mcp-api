namespace AnyAPI.Core.Auth;

using System.Web;
using AnyAPI.Core.Models;
using AnyAPI.Core.Secrets;

/// <summary>
/// Auth handler for API key authentication (header, query, or cookie).
/// </summary>
public class ApiKeyAuthHandler : IAuthHandler
{
    private readonly ApiKeyAuthConfig _config;
    private readonly ISecretProvider _secretProvider;

    public ApiKeyAuthHandler(ApiKeyAuthConfig config, ISecretProvider secretProvider)
    {
        _config = config;
        _secretProvider = secretProvider;
    }

    public async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        // Let ISecretProvider handle caching - supports secret rotation
        var apiKey = await _secretProvider.GetSecretAsync(_config.Secret.SecretName, ct);

        switch (_config.In.ToLowerInvariant())
        {
            case "header":
                request.Headers.TryAddWithoutValidation(_config.ParameterName, apiKey);
                break;

            case "query":
                var uriBuilder = new UriBuilder(request.RequestUri!);
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                query[_config.ParameterName] = apiKey;
                uriBuilder.Query = query.ToString();
                request.RequestUri = uriBuilder.Uri;
                break;

            case "cookie":
                request.Headers.TryAddWithoutValidation("Cookie", $"{_config.ParameterName}={apiKey}");
                break;

            default:
                throw new InvalidOperationException($"Unknown API key placement: {_config.In}");
        }
    }

    public Task<bool> RefreshIfNeededAsync(CancellationToken ct = default)
        => Task.FromResult(false);
}
