using Trader.News.Data.Enums;

namespace Trader.News.Worker.Providers;

/// <summary>
/// Resolves the correct <see cref="INewsSourceProvider"/> implementation
/// for a given <see cref="NewsSourceCategory"/>.
/// </summary>
public sealed class NewsSourceProviderFactory
{
    private readonly RssNewsSourceProvider _rss;
    private readonly HtmlNewsSourceProvider _html;
    private readonly TwitterNewsSourceProvider _twitter;

    public NewsSourceProviderFactory(
        RssNewsSourceProvider rss,
        HtmlNewsSourceProvider html,
        TwitterNewsSourceProvider twitter)
    {
        _rss = rss;
        _html = html;
        _twitter = twitter;
    }

    /// <summary>
    /// Returns the provider that handles the given <paramref name="category"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown when no provider is registered for the given category.
    /// </exception>
    public INewsSourceProvider GetProvider(NewsSourceCategory category) => category switch
    {
        NewsSourceCategory.Rss => _rss,
        NewsSourceCategory.Media => _html,
        NewsSourceCategory.Blog => _html,
        NewsSourceCategory.Social => _twitter,
        _ => throw new NotSupportedException($"No provider registered for category '{category}'.")
    };
}
