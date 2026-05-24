namespace Trader.News.Worker.Summarization;

/// <summary>
/// Fetches and cleans the main body text of a web article for summarization.
/// </summary>
public interface IArticleBodyFetcher
{
    /// <summary>
    /// Downloads the page at <paramref name="uri"/> and returns the cleaned article text,
    /// or <c>null</c> if the page could not be fetched or yielded no usable content.
    /// </summary>
    Task<string?> FetchBodyAsync(string uri, CancellationToken cancellationToken = default);
}
