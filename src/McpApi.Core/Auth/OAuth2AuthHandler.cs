namespace McpApi.Core.Auth;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using McpApi.Core.Models;
using McpApi.Core.Secrets;

/// <summary>
/// Auth handler for OAuth2 client credentials flow with automatic token refresh.
/// </summary>
public class OAuth2AuthHandler : IAuthHandler
{
    private readonly OAuth2AuthConfig _config;
    private readonly ISecretResolver _secretResolver;
    private readonly UserSecretContext? _userContext;
    private readonly HttpClient _httpClient;

    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public OAuth2AuthHandler(
        OAuth2AuthConfig config,
        ISecretResolver secretResolver,
        UserSecretContext? userContext,
        HttpClient httpClient)
    {
        _config = config;
        _secretResolver = secretResolver;
        _userContext = userContext;
        _httpClient = httpClient;
    }

    public async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        await RefreshIfNeededAsync(ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public async Task<bool> RefreshIfNeededAsync(CancellationToken ct = default)
    {
        // Check if refresh needed (with buffer before expiry)
        if (_accessToken != null && DateTime.UtcNow < _tokenExpiry.AddSeconds(-Constants.OAuth2.TokenRefreshBufferSeconds))
            return false;

        await _refreshLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiry.AddSeconds(-Constants.OAuth2.TokenRefreshBufferSeconds))
                return false;

            var clientId = await ResolveSecretAsync(_config.ClientId, ct);
            var clientSecret = await ResolveSecretAsync(_config.ClientSecret, ct);

            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, _config.TokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = string.Join(" ", _config.Scopes)
                })
            };

            var response = await _httpClient.SendAsync(tokenRequest, ct);
            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            if (tokenResponse == null)
                throw new InvalidOperationException("Failed to parse token response");

            _accessToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            return true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

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

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }
}
