using Trader.News.Data.Entities;
using Trader.News.Data.Enums;

namespace Trader.News.Worker.Providers;

/// <summary>
/// Result of fetching a single news article from a source.
/// </summary>
public sealed record NewsFetchResult(
    string Title,
    string? Uri,
    DateTime? NewsDate,
    string? Summary,
    NewsClassification Classification);

/// <summary>
/// Abstracts the mechanism used to fetch news articles from a specific type of source.
/// Implement one provider per <see cref="NewsSourceCategory"/>.
/// </summary>
public interface INewsSourceProvider
{
    /// <summary>
    /// Lazily fetches news items from <paramref name="source"/>.
    /// Only items whose topic matches supported <see cref="NewsClassification"/> values
    /// are yielded; irrelevant items should be skipped.
    /// </summary>
    /// <param name="source">The fully populated source entity (URI, credentials, etc.).</param>
    /// <param name="cancellationToken">Token to cancel the enumeration.</param>
    IAsyncEnumerable<NewsFetchResult> FetchNewsAsync(
        NewsSource source,
        CancellationToken cancellationToken = default);
}
