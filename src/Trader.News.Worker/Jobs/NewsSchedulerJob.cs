using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trader.News.Data;

namespace Trader.News.Worker.Jobs;

/// <summary>
/// Recurring Hangfire job that inspects all enabled news sources and enqueues
/// a <see cref="ProcessNewsSourceJob"/> for each source whose polling interval
/// has elapsed since <c>LastExecution</c>.
///
/// Registered as a recurring job with a 1-minute cron expression.
/// Uses a fixed job ID per source to prevent duplicate enqueue while a job is
/// already pending or executing.
/// </summary>
public sealed class NewsSchedulerJob
{
    private readonly NewsDbContext _db;
    private readonly IBackgroundJobClient _jobClient;
    private readonly ILogger<NewsSchedulerJob> _logger;

    public NewsSchedulerJob(
        NewsDbContext db,
        IBackgroundJobClient jobClient,
        ILogger<NewsSchedulerJob> logger)
    {
        _db = db;
        _jobClient = jobClient;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates all enabled sources and schedules processing for those that are due.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var sources = await _db.NewsSources
            .AsNoTracking()
            .Where(s => s.IsEnabled)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("NewsSchedulerJob: evaluating {Count} enabled sources.", sources.Count);

        foreach (var source in sources)
        {
            var isDue = source.LastExecution is null
                || (now - source.LastExecution.Value).TotalMinutes >= source.SearchIntervalMinutes;

            if (!isDue)
            {
                _logger.LogDebug("Source {Id} ({Name}) is not due yet.", source.Id, source.Name);
                continue;
            }

            // Use a deterministic job ID so Hangfire rejects duplicate enqueues
            // while a job for the same source is already pending or running.
            var jobId = $"news-source-{source.Id}";

            try
            {
                _jobClient.Enqueue<ProcessNewsSourceJob>(
                    jobId,
                    job => job.RunAsync(source.Id, CancellationToken.None));

                _logger.LogInformation(
                    "Enqueued processing job for source {Id} ({Name}), jobId={JobId}.",
                    source.Id, source.Name, jobId);
            }
            catch (Exception ex)
            {
                // Hangfire may throw when a job with the same ID is already enqueued.
                _logger.LogDebug(ex,
                    "Could not enqueue job {JobId} — likely already pending.", jobId);
            }
        }
    }
}
