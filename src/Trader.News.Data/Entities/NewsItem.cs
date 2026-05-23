using Trader.News.Data.Enums;

namespace Trader.News.Data.Entities;

/// <summary>
/// A single news article fetched and persisted by the news processing pipeline.
/// Valuation fields are populated by a later enrichment step; they are null on initial insert.
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

    /// <summary>Thematic classification of this item.</summary>
    public NewsClassification Classification { get; set; }

    /// <summary>
    /// Identifier of the valuation assigned by the analysis pipeline.
    /// Stores the integer value of <see cref="NewsValuation"/>; null until evaluated.
    /// </summary>
    public int? ValuationId { get; set; }

    /// <summary>
    /// Confidence or sentiment score produced by the analysis pipeline (0.0–1.0).
    /// Null until evaluated.
    /// </summary>
    public double? ValuationScore { get; set; }

    /// <summary>Navigation property to the parent source.</summary>
    public NewsSource? Source { get; set; }
}
