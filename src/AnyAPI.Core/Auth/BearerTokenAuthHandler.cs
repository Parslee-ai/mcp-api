namespace AnyAPI.Core.Auth;

using System.Net.Http.Headers;
using AnyAPI.Core.Models;
using AnyAPI.Core.Secrets;

/// <summary>
/// Auth handler for Bearer token authentication.
/// </summary>
public class BearerTokenAuthHandler : IAuthHandler
{
    private readonly BearerTokenAuthConfig _config;
    private readonly ISecretProvider _secretProvider;

    public BearerTokenAuthHandler(BearerTokenAuthConfig config, ISecretProvider secretProvider)
    {
        _config = config;
        _secretProvider = secretProvider;
    }

    public async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        // Let ISecretProvider handle caching - supports secret rotation
        var token = await _secretProvider.GetSecretAsync(_config.Secret.SecretName, ct);
        request.Headers.Authorization = new AuthenticationHeaderValue(_config.Prefix, token);
    }

    public Task<bool> RefreshIfNeededAsync(CancellationToken ct = default)
        => Task.FromResult(false);
}
