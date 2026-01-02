namespace McpApi.Core.Models;

/// <summary>
/// Reference to a secret value. Supports two storage modes:
/// 1. Key Vault reference (legacy): Uses SecretName to reference Azure Key Vault
/// 2. Encrypted (new): Stores AES-256-GCM encrypted value in Cosmos DB
///
/// The mode is determined by which fields are populated:
/// - If EncryptedValue is set: encrypted mode (decrypted with user's derived key)
/// - If SecretName is set: Key Vault mode (fetched from Azure Key Vault)
/// </summary>
public class SecretReference
{
    // === Encrypted storage (preferred for multi-tenant) ===

    /// <summary>
    /// Base64-encoded AES-256-GCM encrypted value.
    /// When set, Iv and AuthTag must also be set.
    /// </summary>
    public string? EncryptedValue { get; set; }

    /// <summary>
    /// Base64-encoded initialization vector for AES-GCM.
    /// </summary>
    public string? Iv { get; set; }

    /// <summary>
    /// Base64-encoded authentication tag for AES-GCM.
    /// </summary>
    public string? AuthTag { get; set; }

    // === Key Vault storage (legacy) ===

    /// <summary>
    /// Key Vault secret name. Used for legacy/global secrets.
    /// </summary>
    public string? SecretName { get; set; }

    /// <summary>
    /// Optional specific version of the Key Vault secret.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Key Vault URI override (if different from default).
    /// </summary>
    public string? VaultUri { get; set; }

    // === Helpers ===

    /// <summary>
    /// Returns true if this is an encrypted secret (stored in Cosmos).
    /// </summary>
    public bool IsEncrypted => !string.IsNullOrEmpty(EncryptedValue);

    /// <summary>
    /// Returns true if this is a Key Vault reference.
    /// </summary>
    public bool IsKeyVaultReference => !string.IsNullOrEmpty(SecretName) && !IsEncrypted;

    /// <summary>
    /// Creates a new encrypted secret reference.
    /// </summary>
    public static SecretReference FromEncrypted(string encryptedValue, string iv, string authTag)
    {
        return new SecretReference
        {
            EncryptedValue = encryptedValue,
            Iv = iv,
            AuthTag = authTag
        };
    }

    /// <summary>
    /// Creates a new Key Vault secret reference.
    /// </summary>
    public static SecretReference FromKeyVault(string secretName, string? version = null, string? vaultUri = null)
    {
        return new SecretReference
        {
            SecretName = secretName,
            Version = version,
            VaultUri = vaultUri
        };
    }
}
