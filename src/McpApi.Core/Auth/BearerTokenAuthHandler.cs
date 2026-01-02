namespace McpApi.Core.Auth;

using System.Net.Http.Headers;
using McpApi.Core.Models;
using McpApi.Core.Secrets;

/// <summary>
/// Auth handler for Bearer token authentication.
/// </summary>
public class BearerTokenAuthHandler : IAuthHandler
{
    private readonly BearerTokenAuthConfig _config;
    private readonly ISecretResolver _secretResolver;
    private readonly UserSecretContext? _userContext;

    public BearerTokenAuthHandler(BearerTokenAuthConfig config, ISecretResolver secretResolver, UserSecretContext? userContext)
    {
        _config = config;
        _secretResolver = secretResolver;
        _userContext = userContext;
    }

    public async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        var token = await ResolveSecretAsync(_config.Secret, ct);
        request.Headers.Authorization = new AuthenticationHeaderValue(_config.Prefix, token);
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

        return _secretResolver.ResolveAsync(secret, "", "", ct);
    }
}
