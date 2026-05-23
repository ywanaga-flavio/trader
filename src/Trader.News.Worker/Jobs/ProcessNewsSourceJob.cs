using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trader.News.Data;
using Trader.News.Data.Entities;
using Trader.News.Worker.Providers;

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
    private readonly ILogger<ProcessNewsSourceJob> _logger;

    public ProcessNewsSourceJob(
        NewsDbContext db,
        NewsSourceProviderFactory providerFactory,
        ILogger<ProcessNewsSourceJob> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
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

            var item = new NewsItem
            {
                SourceId = source.Id,
                Uri = result.Uri,
                CreatedAt = DateTime.UtcNow,
                NewsDate = result.NewsDate,
                Title = result.Title,
                Summary = TruncateSummary(result.Summary),
                Classification = result.Classification,
                ValuationId = null,
                ValuationScore = null,
            };

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
