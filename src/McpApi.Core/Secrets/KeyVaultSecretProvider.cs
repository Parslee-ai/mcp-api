namespace McpApi.Core.Secrets;

using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Azure Key Vault implementation of ISecretProvider with in-memory caching.
/// </summary>
public class KeyVaultSecretProvider : ISecretProvider, IDisposable
{
    private readonly SecretClient _client;
    private readonly MemoryCache _cache;
    private readonly TimeSpan _cacheDuration;
    private bool _disposed;

    public KeyVaultSecretProvider(SecretProviderOptions options)
    {
        _client = new SecretClient(new Uri(options.VaultUri), new DefaultAzureCredential());
        _cache = new MemoryCache(new MemoryCacheOptions());
        _cacheDuration = options.CacheDuration;
    }

    public async Task<string> GetSecretAsync(string secretName, CancellationToken ct = default)
    {
        var value = await TryGetSecretAsync(secretName, ct);
        return value ?? throw new KeyNotFoundException($"Secret '{secretName}' not found in Key Vault.");
    }

    public async Task<string?> TryGetSecretAsync(string secretName, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(secretName, out string? cached))
            return cached;

        try
        {
            var response = await _client.GetSecretAsync(secretName, cancellationToken: ct);
            var value = response.Value.Value;

            _cache.Set(secretName, value, _cacheDuration);
            return value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SetSecretAsync(string secretName, string value, CancellationToken ct = default)
    {
        await _client.SetSecretAsync(secretName, value, ct);
        _cache.Set(secretName, value, _cacheDuration);
    }

    public async Task DeleteSecretAsync(string secretName, CancellationToken ct = default)
    {
        await _client.StartDeleteSecretAsync(secretName, ct);
        _cache.Remove(secretName);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _cache.Dispose();
        _disposed = true;
    }
}
