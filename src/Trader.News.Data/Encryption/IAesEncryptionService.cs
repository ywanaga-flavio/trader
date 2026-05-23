namespace Trader.News.Data.Encryption;

/// <summary>
/// Provides AES-256 encryption and decryption for sensitive fields
/// (e.g. <c>NewsSource.PasswordEncrypted</c>).
/// </summary>
public interface IAesEncryptionService
{
    /// <summary>Encrypts <paramref name="plainText"/> and returns a Base64-encoded cipher string.</summary>
    string Encrypt(string plainText);

    /// <summary>Decrypts a Base64-encoded cipher string produced by <see cref="Encrypt"/>.</summary>
    string Decrypt(string cipherText);
}
