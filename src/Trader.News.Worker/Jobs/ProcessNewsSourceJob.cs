using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trader.News.Data;
using Trader.News.Data.Entities;
using Trader.News.Worker.Analysis;
using Trader.News.Worker.Providers;
using Trader.News.Worker.Summarization;

namespace Trader.News.Worker.Jobs;

/// <summary>
/// Hangfire job that fetches and persists news items for a single <see cref="NewsSource"/>.
/// Runs on the <c>news</c> queue with a max concurrency of 5 workers.
/// Updates <see cref="NewsSource.LastExecution"/> on successful completion.
/// </summary>
public sealed class ProcessNewsSourceJob
{
    private readonly NewsDbContext _db;
    private readonly NewsSourceProviderFactory _providerFactory;
    private readonly INewsAnalysisService _analysisService;
    private readonly IArticleBodyFetcher _bodyFetcher;
    private readonly IArticleSummarizerService _summarizer;
    private readonly ILogger<ProcessNewsSourceJob> _logger;

    public ProcessNewsSourceJob(
        NewsDbContext db,
        NewsSourceProviderFactory providerFactory,
        INewsAnalysisService analysisService,
        IArticleBodyFetcher bodyFetcher,
        IArticleSummarizerService summarizer,
        ILogger<ProcessNewsSourceJob> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _analysisService = analysisService;
        _bodyFetcher = bodyFetcher;
        _summarizer = summarizer;
        _logger = logger;
    }

    /// <summary>
    /// Fetches news for the source identified by <paramref name="sourceId"/>,
    /// persists new items (skipping duplicates), and updates <c>LastExecution</c>.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [Queue("news")]
    public async Task RunAsync(int sourceId, CancellationToken cancellationToken = default)
    {
        var source = await _db.NewsSources.FindAsync([sourceId], cancellationToken);
        if (source is null)
        {
            _logger.LogWarning("Source {SourceId} not found; skipping.", sourceId);
            return;
        }

        if (!source.IsEnabled)
        {
            _logger.LogInformation("Source {SourceId} ({Name}) is disabled; skipping.", sourceId, source.Name);
            return;
        }

        _logger.LogInformation(
            "Processing news source {SourceId} ({Name}) [{Category}].",
            source.Id, source.Name, source.Category);

        var provider = _providerFactory.GetProvider(source.Category);
        int inserted = 0, skipped = 0;

        await foreach (var result in provider.FetchNewsAsync(source, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Dedup: skip if a news item with the same URI already exists for this source.
            if (!string.IsNullOrEmpty(result.Uri))
            {
                var exists = await _db.NewsItems
                    .AnyAsync(n => n.SourceId == source.Id && n.Uri == result.Uri, cancellationToken);

                if (exists)
                {
                    skipped++;
                    continue;
                }
            }

            // If no summary was provided by the feed, try to fetch and summarize the article.
            var summary = result.Summary;
            if (string.IsNullOrWhiteSpace(summary) && !string.IsNullOrEmpty(result.Uri) && _summarizer.IsAvailable)
            {
                _logger.LogDebug("Fetching article body for summarization: {Uri}", result.Uri);
                var body = await _bodyFetcher.FetchBodyAsync(result.Uri, cancellationToken);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    summary = await _summarizer.SummarizeAsync(body, cancellationToken);
                    if (summary is not null)
                        _logger.LogDebug("Generated summary for {Uri}: {Summary}", result.Uri, summary);
                }
            }

            var item = new NewsItem
            {
                SourceId    = source.Id,
                Uri         = result.Uri,
                CreatedAt   = DateTime.UtcNow,
                NewsDate    = result.NewsDate,
                Title       = result.Title,
                Summary     = TruncateSummary(summary),
            };

            // Run ML analysis inline; null means model not available — fields stay null.
            var analysis = await _analysisService.AnalyzeAsync(result.Title, result.Summary, cancellationToken);
            item.ClassificationId    = analysis?.ClassificationId;
            item.ClassificationScore = analysis?.ClassificationScore;
            item.SentimentId         = analysis?.SentimentId;
            item.SentimentScore      = analysis?.SentimentScore;

            _db.NewsItems.Add(item);
            inserted++;
        }

        if (inserted > 0)
            await _db.SaveChangesAsync(cancellationToken);

        // Update last execution timestamp regardless of whether new items were found.
        await _db.NewsSources
            .Where(s => s.Id == source.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.LastExecution, DateTime.UtcNow),
                cancellationToken);

        _logger.LogInformation(
            "Source {SourceId} ({Name}) processed: {Inserted} inserted, {Skipped} skipped.",
            source.Id, source.Name, inserted, skipped);
    }

    private static string? TruncateSummary(string? text)
    {
        if (text is null) return null;
        return text.Length <= NewsItem.SummaryMaxLength
            ? text
            : text[..NewsItem.SummaryMaxLength];
    }
}
