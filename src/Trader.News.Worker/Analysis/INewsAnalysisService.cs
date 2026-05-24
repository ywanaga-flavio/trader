namespace Trader.News.Worker.Analysis;

/// <summary>
/// Result of ML-based topic classification and sentiment analysis for a single news article.
/// </summary>
/// <param name="ClassificationId">Integer value of <c>NewsClassification</c> with the highest NLI entailment score.</param>
/// <param name="ClassificationScore">NLI entailment confidence score for <see cref="ClassificationId"/> (0.0–1.0).</param>
/// <param name="SentimentId">Integer value of <c>NewsValuation</c> with the highest NLI entailment score.</param>
/// <param name="SentimentScore">NLI entailment confidence score for <see cref="SentimentId"/> (0.0–1.0).</param>
public sealed record NewsAnalysisResult(
    int ClassificationId,
    double ClassificationScore,
    int SentimentId,
    double SentimentScore);

/// <summary>
/// Performs zero-shot topic classification and sentiment analysis on news article text
/// using an ONNX NLI model.
/// </summary>
public interface INewsAnalysisService
{
    /// <summary>
    /// Analyzes the topic and sentiment of a news article.
    /// Returns <c>null</c> when the ONNX model files are not present or inference fails —
    /// the caller should store the item with null analysis fields in that case.
    /// </summary>
    /// <param name="title">Article headline.</param>
    /// <param name="summary">Optional article body or lead paragraph.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<NewsAnalysisResult?> AnalyzeAsync(
        string title,
        string? summary,
        CancellationToken cancellationToken = default);
}
