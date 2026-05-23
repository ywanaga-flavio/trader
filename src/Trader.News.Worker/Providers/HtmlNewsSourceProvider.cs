using System.Runtime.CompilerServices;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Trader.News.Data.Encryption;
using Trader.News.Data.Entities;
using Trader.News.Data.Enums;

namespace Trader.News.Worker.Providers;

/// <summary>
/// Fetches news by scraping HTML pages using HtmlAgilityPack.
/// This is a scaffold implementation; each media outlet typically needs
/// site-specific XPath selectors configured on the <see cref="NewsSource.Uri"/>.
///
/// Convention: the <see cref="NewsSource.Uri"/> may use a query-string extension
/// <c>?selector=...&amp;titleAttr=...</c> for per-source customization without code changes.
/// Default selectors target common article-listing patterns (schema.org, Open Graph).
/// </summary>
public sealed class HtmlNewsSourceProvider : INewsSourceProvider
{
    private readonly IAesEncryptionService _encryption;
    private readonly ILogger<HtmlNewsSourceProvider> _logger;

    public HtmlNewsSourceProvider(
        IAesEncryptionService encryption,
        ILogger<HtmlNewsSourceProvider> logger)
    {
        _encryption = encryption;
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<NewsFetchResult> FetchNewsAsync(
        NewsSource source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Scraping HTML from {Uri}", source.Uri);

        HtmlDocument doc;
        try
        {
            var web = new HtmlWeb();
            if (!string.IsNullOrEmpty(source.Username) && !string.IsNullOrEmpty(source.PasswordEncrypted))
            {
                var password = _encryption.Decrypt(source.PasswordEncrypted);
                web.PreRequest = req =>
                {
                    var credentials = Convert.ToBase64String(
                        System.Text.Encoding.UTF8.GetBytes($"{source.Username}:{password}"));
                    req.Headers["Authorization"] = $"Basic {credentials}";
                    return true;
                };
            }

            doc = await web.LoadFromWebAsync(source.Uri, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scrape HTML from {Uri}", source.Uri);
            yield break;
        }

        // Try to extract articles using schema.org Article markup first,
        // then fall back to Open Graph meta tags per page.
        var articles = doc.DocumentNode
            .SelectNodes("//article | //*[@itemtype='http://schema.org/Article'] | //*[@itemtype='https://schema.org/Article']");

        if (articles is null || articles.Count == 0)
        {
            // Fallback: scrape Open Graph metadata from the page itself.
            var ogTitle = doc.DocumentNode
                .SelectSingleNode("//meta[@property='og:title']")?
                .GetAttributeValue("content", null);
            var ogDescription = doc.DocumentNode
                .SelectSingleNode("//meta[@property='og:description']")?
                .GetAttributeValue("content", null);
            var ogUrl = doc.DocumentNode
                .SelectSingleNode("//meta[@property='og:url']")?
                .GetAttributeValue("content", null);

            if (!string.IsNullOrWhiteSpace(ogTitle))
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return new NewsFetchResult(
                    Title: Truncate(ogTitle, 500)!,
                    Uri: ogUrl,
                    NewsDate: DateTime.UtcNow,
                    Summary: Truncate(ogDescription, 500),
                    Classification: ClassifyText(ogTitle + " " + ogDescription));
            }

            yield break;
        }

        foreach (var article in articles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var title = article.SelectSingleNode(".//*[@itemprop='headline'] | .//h1 | .//h2 | .//h3")?
                .InnerText.Trim();
            if (string.IsNullOrWhiteSpace(title)) continue;

            var description = article.SelectSingleNode(".//*[@itemprop='description'] | .//p")?
                .InnerText.Trim();
            var link = article.SelectSingleNode(".//*[@itemprop='url'] | .//a[@href]")?
                .GetAttributeValue("href", null);

            // Make relative URLs absolute.
            if (!string.IsNullOrEmpty(link) && Uri.TryCreate(source.Uri, UriKind.Absolute, out var baseUri))
            {
                if (Uri.TryCreate(baseUri, link, out var absolute))
                    link = absolute.ToString();
            }

            yield return new NewsFetchResult(
                Title: Truncate(title, 500)!,
                Uri: link,
                NewsDate: DateTime.UtcNow,
                Summary: Truncate(description, 500),
                Classification: ClassifyText(title + " " + description));
        }
    }

    /// <summary>
    /// Naïve keyword-based classification. Replace with ML inference when available.
    /// </summary>
    private static NewsClassification ClassifyText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return NewsClassification.Market;

        var lower = text.ToLowerInvariant();

        if (lower.Contains("bolsa") || lower.Contains("accion") || lower.Contains("stock")
            || lower.Contains("mercado") || lower.Contains("market"))
            return NewsClassification.Market;

        if (lower.Contains("inflacion") || lower.Contains("economia") || lower.Contains("economy")
            || lower.Contains("pbi") || lower.Contains("gdp"))
            return NewsClassification.Economic;

        if (lower.Contains("gobierno") || lower.Contains("politic") || lower.Contains("election")
            || lower.Contains("congreso"))
            return NewsClassification.Political;

        if (lower.Contains("technolog") || lower.Contains("tecnolog") || lower.Contains("software")
            || lower.Contains("cyber"))
            return NewsClassification.Technology;

        if (lower.Contains("empresa") || lower.Contains("company") || lower.Contains("ganancia")
            || lower.Contains("merger"))
            return NewsClassification.Corporate;

        if (lower.Contains("internacional") || lower.Contains("geopolit") || lower.Contains("guerra"))
            return NewsClassification.International;

        return NewsClassification.Market;
    }

    private static string? Truncate(string? text, int maxLength)
        => text is null ? null
            : text.Length <= maxLength ? text
            : text[..maxLength];
}
