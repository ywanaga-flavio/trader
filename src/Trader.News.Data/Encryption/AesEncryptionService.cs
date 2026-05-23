using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Trader.News.Data.Encryption;

/// <summary>
/// AES-256-CBC encryption service. The key is derived from the <c>NEWS_ENCRYPTION_KEY</c>
/// environment variable (or <c>NewsEncryption:Key</c> in configuration) via SHA-256,
/// so the raw value can be any length ≥ 1 character.
/// </summary>
public sealed class AesEncryptionService : IAesEncryptionService
{
    private readonly byte[] _key;

    /// <param name="configuration">Used to resolve the encryption key from config or env var.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when neither the environment variable nor the configuration key is set.
    /// </exception>
    public AesEncryptionService(IConfiguration configuration)
    {
        var rawKey = Environment.GetEnvironmentVariable("NEWS_ENCRYPTION_KEY")
            ?? configuration["NewsEncryption:Key"]
            ?? throw new InvalidOperationException(
                "AES encryption key not configured. " +
                "Set environment variable NEWS_ENCRYPTION_KEY or NewsEncryption:Key in appsettings.");

        // Derive a stable 256-bit key regardless of raw key length.
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
    }

    /// <inheritdoc />
    public string Encrypt(string plainText)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainText);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend the IV so it can be recovered on decryption.
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        aes.IV.CopyTo(result, 0);
        cipherBytes.CopyTo(result, aes.IV.Length);

        return Convert.ToBase64String(result);
    }

    /// <inheritdoc />
    public string Decrypt(string cipherText)
    {
        ArgumentException.ThrowIfNullOrEmpty(cipherText);

        var allBytes = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;

        // The first 16 bytes are the IV.
        var iv = allBytes[..16];
        var cipherBytes = allBytes[16..];

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
