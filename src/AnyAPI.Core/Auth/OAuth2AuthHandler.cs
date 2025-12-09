namespace AnyAPI.Core.Auth;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AnyAPI.Core.Models;
using AnyAPI.Core.Secrets;

/// <summary>
/// Auth handler for OAuth2 client credentials flow with automatic token refresh.
/// </summary>
public class OAuth2AuthHandler : IAuthHandler
{
    private readonly OAuth2AuthConfig _config;
    private readonly ISecretProvider _secretProvider;
    private readonly HttpClient _httpClient;

    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public OAuth2AuthHandler(
        OAuth2AuthConfig config,
        ISecretProvider secretProvider,
        HttpClient httpClient)
    {
        _config = config;
        _secretProvider = secretProvider;
        _httpClient = httpClient;
    }

    public async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        await RefreshIfNeededAsync(ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public async Task<bool> RefreshIfNeededAsync(CancellationToken ct = default)
    {
        // Check if refresh needed (with 30s buffer)
        if (_accessToken != null && DateTime.UtcNow < _tokenExpiry.AddSeconds(-30))
            return false;

        await _refreshLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiry.AddSeconds(-30))
                return false;

            var clientId = await _secretProvider.GetSecretAsync(_config.ClientId.SecretName, ct);
            var clientSecret = await _secretProvider.GetSecretAsync(_config.ClientSecret.SecretName, ct);

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
