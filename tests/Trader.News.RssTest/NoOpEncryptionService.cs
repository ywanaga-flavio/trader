using Trader.News.Data.Encryption;

namespace Trader.News.RssTest;

/// <summary>
/// No-op encryption stub — public RSS feeds carry no encrypted credentials.
/// </summary>
internal sealed class NoOpEncryptionService : IAesEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}
