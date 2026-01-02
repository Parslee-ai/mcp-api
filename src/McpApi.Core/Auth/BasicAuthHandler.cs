namespace McpApi.Core.Auth;

using System.Net.Http.Headers;
using System.Text;
using McpApi.Core.Models;
using McpApi.Core.Secrets;

/// <summary>
/// Auth handler for HTTP Basic authentication.
/// </summary>
public class BasicAuthHandler : IAuthHandler
{
    private readonly BasicAuthConfig _config;
    private readonly ISecretProvider _secretProvider;

    public BasicAuthHandler(BasicAuthConfig config, ISecretProvider secretProvider)
    {
        _config = config;
        _secretProvider = secretProvider;
    }

    public async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        // Let ISecretProvider handle caching - supports secret rotation
        var username = await _secretProvider.GetSecretAsync(_config.Username.SecretName, ct);
        var password = await _secretProvider.GetSecretAsync(_config.Password.SecretName, ct);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    public Task<bool> RefreshIfNeededAsync(CancellationToken ct = default)
        => Task.FromResult(false);
}
