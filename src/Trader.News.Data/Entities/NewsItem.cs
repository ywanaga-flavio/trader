using Trader.News.Data.Enums;

namespace Trader.News.Data.Entities;

/// <summary>
/// A single news article fetched and persisted by the news processing pipeline.
/// Analysis fields (ClassificationId, ClassificationScore, SentimentId, SentimentScore)
/// are populated by the ONNX NLI analysis step; they are null on initial insert.
/// </summary>
public class NewsItem
{
    public long Id { get; set; }

    /// <summary>Foreign key to the <see cref="NewsSource"/> that produced this item.</summary>
    public int SourceId { get; set; }

    /// <summary>Canonical URL of the news article. May be null for social posts without a link.</summary>
    public string? Uri { get; set; }

    /// <summary>UTC timestamp when this record was persisted.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Publication date reported by the source. Null when not available.</summary>
    public DateTime? NewsDate { get; set; }

    /// <summary>Headline or title of the article (max 500 chars).</summary>
    public required string Title { get; set; }

    /// <summary>
    /// Truncated body or lead paragraph of the article.
    /// Stored up to <see cref="SummaryMaxLength"/> characters.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>Maximum length enforced when persisting <see cref="Summary"/>.</summary>
    public const int SummaryMaxLength = 500;

    /// <summary>
    /// Integer value of <see cref="NewsClassification"/> assigned by the ML analysis pipeline.
    /// Null until the analysis pipeline has processed this item.
    /// </summary>
    public int? ClassificationId { get; set; }

    /// <summary>
    /// NLI entailment confidence score for <see cref="ClassificationId"/> (0.0–1.0).
    /// Null until evaluated.
    /// </summary>
    public double? ClassificationScore { get; set; }

    /// <summary>
    /// Integer value of <see cref="NewsValuation"/> (sentiment) assigned by the ML analysis pipeline.
    /// Null until the analysis pipeline has processed this item.
    /// </summary>
    public int? SentimentId { get; set; }

    /// <summary>
    /// NLI entailment confidence score for <see cref="SentimentId"/> (0.0–1.0).
    /// Null until evaluated.
    /// </summary>
    public double? SentimentScore { get; set; }

    /// <summary>Navigation property to the parent source.</summary>
    public NewsSource? Source { get; set; }
}
