namespace McpApi.Core.Secrets;

/// <summary>
/// Configuration options for the secret provider.
/// </summary>
public class SecretProviderOptions
{
    /// <summary>Azure Key Vault URI.</summary>
    public required string VaultUri { get; set; }

    /// <summary>Duration to cache secrets in memory.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
}
