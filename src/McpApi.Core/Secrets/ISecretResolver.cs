namespace McpApi.Core.Secrets;

using McpApi.Core.Models;

/// <summary>
/// Resolves secret values from SecretReference objects.
/// Supports both encrypted (Cosmos) and Key Vault storage modes.
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// Resolves a secret value from a SecretReference.
    /// </summary>
    /// <param name="reference">The secret reference to resolve.</param>
    /// <param name="userId">User ID for decrypting user-specific secrets.</param>
    /// <param name="userSalt">User's encryption salt for decryption.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved secret value.</returns>
    Task<string> ResolveAsync(SecretReference reference, string userId, string userSalt, CancellationToken ct = default);

    /// <summary>
    /// Creates an encrypted SecretReference for storing a secret value.
    /// </summary>
    /// <param name="plaintext">The secret value to encrypt.</param>
    /// <param name="userId">User ID for user-specific encryption.</param>
    /// <param name="userSalt">User's encryption salt.</param>
    /// <returns>A SecretReference containing the encrypted value.</returns>
    SecretReference Encrypt(string plaintext, string userId, string userSalt);
}

/// <summary>
/// Resolves secrets from either encrypted Cosmos storage or Azure Key Vault.
/// </summary>
public class SecretResolver : ISecretResolver
{
    private readonly IEncryptionService _encryptionService;
    private readonly ISecretProvider? _keyVaultProvider;

    /// <summary>
    /// Creates a new secret resolver.
    /// </summary>
    /// <param name="encryptionService">Service for encrypting/decrypting values.</param>
    /// <param name="keyVaultProvider">Optional Key Vault provider for legacy secrets. Can be null if Key Vault is not configured.</param>
    public SecretResolver(IEncryptionService encryptionService, ISecretProvider? keyVaultProvider = null)
    {
        _encryptionService = encryptionService;
        _keyVaultProvider = keyVaultProvider;
    }

    public async Task<string> ResolveAsync(SecretReference reference, string userId, string userSalt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        // Prefer encrypted storage
        if (reference.IsEncrypted)
        {
            if (string.IsNullOrEmpty(reference.Iv) || string.IsNullOrEmpty(reference.AuthTag))
            {
                throw new InvalidOperationException("Encrypted secret is missing IV or AuthTag.");
            }

            var encryptedData = new EncryptedData
            {
                Ciphertext = reference.EncryptedValue!,
                Iv = reference.Iv,
                AuthTag = reference.AuthTag
            };

            return _encryptionService.Decrypt(userId, userSalt, encryptedData);
        }

        // Fall back to Key Vault
        if (reference.IsKeyVaultReference)
        {
            if (_keyVaultProvider == null)
            {
                throw new InvalidOperationException(
                    "Key Vault is not configured. Cannot resolve legacy secret reference. " +
                    "Please re-configure the secret using encrypted storage.");
            }

            return await _keyVaultProvider.GetSecretAsync(reference.SecretName!, ct);
        }

        throw new InvalidOperationException("SecretReference has neither encrypted value nor Key Vault reference.");
    }

    public SecretReference Encrypt(string plaintext, string userId, string userSalt)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(userSalt);

        var encrypted = _encryptionService.Encrypt(userId, userSalt, plaintext);

        return SecretReference.FromEncrypted(
            encrypted.Ciphertext,
            encrypted.Iv,
            encrypted.AuthTag);
    }
}
