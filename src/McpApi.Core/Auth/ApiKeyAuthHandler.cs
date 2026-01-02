namespace McpApi.Core.Auth;

using System.Web;
using McpApi.Core.Models;
using McpApi.Core.Secrets;

/// <summary>
/// Auth handler for API key authentication (header, query, or cookie).
/// </summary>
public class ApiKeyAuthHandler : IAuthHandler
{
    private readonly ApiKeyAuthConfig _config;
    private readonly ISecretResolver _secretResolver;
    private readonly UserSecretContext? _userContext;

    public ApiKeyAuthHandler(ApiKeyAuthConfig config, ISecretResolver secretResolver, UserSecretContext? userContext)
    {
        _config = config;
        _secretResolver = secretResolver;
        _userContext = userContext;
    }

    public async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        var apiKey = await ResolveSecretAsync(_config.Secret, ct);

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

    private Task<string> ResolveSecretAsync(SecretReference secret, CancellationToken ct)
    {
        if (secret.IsEncrypted)
        {
            if (_userContext == null)
                throw new InvalidOperationException("User context required for encrypted secrets.");

            return _secretResolver.ResolveAsync(secret, _userContext.UserId, _userContext.EncryptionSalt, ct);
        }

        // Key Vault reference - no user context needed
        return _secretResolver.ResolveAsync(secret, "", "", ct);
    }
}
