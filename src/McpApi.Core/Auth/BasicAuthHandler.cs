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
    private readonly ISecretResolver _secretResolver;
    private readonly UserSecretContext? _userContext;

    public BasicAuthHandler(BasicAuthConfig config, ISecretResolver secretResolver, UserSecretContext? userContext)
    {
        _config = config;
        _secretResolver = secretResolver;
        _userContext = userContext;
    }

    public async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        var username = await ResolveSecretAsync(_config.Username, ct);
        var password = await ResolveSecretAsync(_config.Password, ct);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
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
