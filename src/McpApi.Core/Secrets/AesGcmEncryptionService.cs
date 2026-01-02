namespace McpApi.Core.Secrets;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// AES-256-GCM encryption service with HKDF-based per-user key derivation.
///
/// Architecture:
/// - Master key stored in Azure Key Vault
/// - Per-user keys derived using HKDF(masterKey, userId + salt)
/// - AES-256-GCM provides authenticated encryption
/// </summary>
public class AesGcmEncryptionService : IEncryptionService
{
    private const int KeySizeBytes = 32;  // AES-256
    private const int IvSizeBytes = 12;   // GCM recommended IV size
    private const int TagSizeBytes = 16;  // GCM tag size
    private const int SaltSizeBytes = 32;

    private readonly byte[] _masterKey;

    /// <summary>
    /// Creates a new encryption service with the given master key.
    /// </summary>
    /// <param name="masterKeyBase64">Base64-encoded 32-byte master key from Key Vault.</param>
    public AesGcmEncryptionService(string masterKeyBase64)
    {
        _masterKey = Convert.FromBase64String(masterKeyBase64);
        if (_masterKey.Length != KeySizeBytes)
        {
            throw new ArgumentException(
                $"Master key must be {KeySizeBytes} bytes ({KeySizeBytes * 8} bits). " +
                $"Got {_masterKey.Length} bytes.",
                nameof(masterKeyBase64));
        }
    }

    public EncryptedData Encrypt(string userId, string userSalt, string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(userSalt);
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        var userKey = DeriveUserKey(userId, userSalt);
        var iv = RandomNumberGenerator.GetBytes(IvSizeBytes);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(userKey, TagSizeBytes);
        aes.Encrypt(iv, plaintextBytes, ciphertext, tag);

        return new EncryptedData
        {
            Ciphertext = Convert.ToBase64String(ciphertext),
            Iv = Convert.ToBase64String(iv),
            AuthTag = Convert.ToBase64String(tag)
        };
    }

    public string Decrypt(string userId, string userSalt, EncryptedData encryptedData)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(userSalt);
        ArgumentNullException.ThrowIfNull(encryptedData);

        var userKey = DeriveUserKey(userId, userSalt);
        var iv = Convert.FromBase64String(encryptedData.Iv);
        var ciphertext = Convert.FromBase64String(encryptedData.Ciphertext);
        var tag = Convert.FromBase64String(encryptedData.AuthTag);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(userKey, TagSizeBytes);
        aes.Decrypt(iv, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public string GenerateSalt()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltSizeBytes));
    }

    /// <summary>
    /// Derives a user-specific encryption key using HKDF.
    /// Key = HKDF-SHA256(masterKey, userId + salt, "mcpapi-user-encryption")
    /// </summary>
    private byte[] DeriveUserKey(string userId, string userSalt)
    {
        var salt = Convert.FromBase64String(userSalt);
        var info = Encoding.UTF8.GetBytes($"mcpapi-user-encryption:{userId}");

        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            _masterKey,
            KeySizeBytes,
            salt,
            info);
    }
}

/// <summary>
/// Configuration options for the encryption service.
/// </summary>
public class EncryptionOptions
{
    /// <summary>
    /// Key Vault secret name containing the master encryption key.
    /// Default: "mcpapi-master-encryption-key"
    /// </summary>
    public string MasterKeySecretName { get; set; } = "mcpapi-master-encryption-key";
}
