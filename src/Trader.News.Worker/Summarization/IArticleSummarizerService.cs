namespace Trader.News.Worker.Summarization;

/// <summary>
/// Produces a concise summary of a news article body using a language model.
/// </summary>
public interface IArticleSummarizerService
{
    /// <summary>
    /// <c>true</c> when the underlying model is loaded and ready.
    /// When <c>false</c>, <see cref="SummarizeAsync"/> always returns <c>null</c>.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Summarizes <paramref name="text"/> in 2-3 sentences.
    /// Returns <c>null</c> if the model is unavailable or inference fails.
    /// </summary>
    Task<string?> SummarizeAsync(string text, CancellationToken cancellationToken = default);
}
