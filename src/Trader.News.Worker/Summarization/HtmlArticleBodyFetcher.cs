using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Trader.News.Worker.Summarization;

/// <summary>
/// Fetches an article page and extracts the main body text using HtmlAgilityPack.
/// Strips navigation, headers, footers, scripts and styles before returning
/// up to 3 000 characters of clean text.
/// </summary>
public sealed class HtmlArticleBodyFetcher : IArticleBodyFetcher
{
    // Tags whose entire subtree should be removed before text extraction.
    private static readonly string[] _removedTags =
        ["script", "style", "nav", "header", "footer", "aside", "form", "iframe", "noscript"];

    // Candidate container tags tried in order; first non-empty match wins.
    private static readonly string[] _containerTags = ["article", "main"];

    private const int MaxBodyChars = 3_000;
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;
    private readonly ILogger<HtmlArticleBodyFetcher> _logger;

    public HtmlArticleBodyFetcher(ILogger<HtmlArticleBodyFetcher> logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = HttpTimeout };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; TraderNewsBot/1.0)");
    }

    /// <inheritdoc/>
    public async Task<string?> FetchBodyAsync(string uri, CancellationToken cancellationToken = default)
    {
        string html;
        try
        {
            html = await _http.GetStringAsync(uri, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch article body from {Uri}.", uri);
            return null;
        }

        try
        {
            return ExtractText(html);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse article body from {Uri}.", uri);
            return null;
        }
    }

    private static string? ExtractText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove noise nodes.
        foreach (var tag in _removedTags)
        {
            foreach (var node in doc.DocumentNode.SelectNodes($"//{tag}") ?? Enumerable.Empty<HtmlNode>())
                node.Remove();
        }

        // Try semantic containers first, fall back to <body>.
        HtmlNode? container = null;
        foreach (var tag in _containerTags)
        {
            container = doc.DocumentNode.SelectSingleNode($"//{tag}");
            if (container is not null) break;
        }
        container ??= doc.DocumentNode.SelectSingleNode("//body");

        if (container is null) return null;

        var text = HtmlEntity.DeEntitize(container.InnerText);
        text = NormalizeWhitespace(text);

        return text.Length == 0 ? null
             : text.Length <= MaxBodyChars ? text
             : text[..MaxBodyChars];
    }

    private static string NormalizeWhitespace(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        bool lastWasWhitespace = true; // skip leading whitespace

        foreach (var ch in text)
        {
            if (ch == '\r') continue;

            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasWhitespace)
                {
                    sb.Append(ch == '\n' ? '\n' : ' ');
                    lastWasWhitespace = true;
                }
            }
            else
            {
                sb.Append(ch);
                lastWasWhitespace = false;
            }
        }

        return sb.ToString().Trim();
    }
}
