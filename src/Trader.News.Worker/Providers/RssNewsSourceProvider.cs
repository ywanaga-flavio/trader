using System.Runtime.CompilerServices;
using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Logging;
using Trader.News.Data.Encryption;
using Trader.News.Data.Entities;

namespace Trader.News.Worker.Providers;

/// <summary>
/// Fetches news from RSS 2.0 and Atom feeds using <see cref="SyndicationFeed"/>.
/// Supports HTTP Basic authentication via <see cref="NewsSource.Username"/> /
/// <see cref="NewsSource.PasswordEncrypted"/>.
/// </summary>
public sealed class RssNewsSourceProvider : INewsSourceProvider
{
    private readonly IAesEncryptionService _encryption;
    private readonly ILogger<RssNewsSourceProvider> _logger;

    public RssNewsSourceProvider(
        IAesEncryptionService encryption,
        ILogger<RssNewsSourceProvider> logger)
    {
        _encryption = encryption;
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<NewsFetchResult> FetchNewsAsync(
        NewsSource source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching RSS feed from {Uri}", source.Uri);

        SyndicationFeed feed;
        try
        {
            using var httpClient = BuildHttpClient(source);
            var xmlContent = await httpClient.GetStringAsync(source.Uri, cancellationToken);
            using var reader = XmlReader.Create(new StringReader(xmlContent));
            feed = SyndicationFeed.Load(reader);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch RSS feed from {Uri}", source.Uri);
            yield break;
        }

        foreach (var item in feed.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var title = item.Title?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title)) continue;

            var summary = item.Summary?.Text
                ?? (item.Content as TextSyndicationContent)?.Text;
            var uri = item.Links.FirstOrDefault()?.Uri?.ToString();
            var pubDate = item.PublishDate == DateTimeOffset.MinValue
                ? item.LastUpdatedTime.UtcDateTime
                : item.PublishDate.UtcDateTime;

            yield return new NewsFetchResult(
                Title: Truncate(title, 500) ?? title,
                Uri: uri,
                NewsDate: pubDate == DateTime.MinValue ? null : pubDate,
                Summary: Truncate(summary, 500));
        }
    }

    private HttpClient BuildHttpClient(NewsSource source)
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        if (!string.IsNullOrEmpty(source.Username) && !string.IsNullOrEmpty(source.PasswordEncrypted))
        {
            var password = _encryption.Decrypt(source.PasswordEncrypted);
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{source.Username}:{password}"));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        return client;
    }

    private static string? Truncate(string? text, int maxLength)
        => text is null ? null
            : text.Length <= maxLength ? text
            : text[..maxLength];
}
