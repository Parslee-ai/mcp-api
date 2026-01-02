namespace McpApi.Core.Models;

/// <summary>
/// Points to a secret stored in Azure Key Vault.
/// Never stores actual secret values - only references.
/// </summary>
public class SecretReference
{
    /// <summary>Key Vault secret name.</summary>
    public required string SecretName { get; set; }

    /// <summary>Optional specific version of the secret.</summary>
    public string? Version { get; set; }

    /// <summary>Key Vault URI override (if different from default).</summary>
    public string? VaultUri { get; set; }
}
