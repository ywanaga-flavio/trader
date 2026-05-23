using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trader.News.Data;

namespace Trader.News.Api.Controllers;

/// <summary>
/// Allows manual triggering of news processing jobs via the REST API.
/// Requires the <c>trader</c> role.
/// </summary>
[ApiController]
[Route("api/news-sources")]
[Authorize(Roles = "trader")]
public sealed class NewsJobsController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly IBackgroundJobClient _jobClient;
    private readonly ILogger<NewsJobsController> _logger;

    public NewsJobsController(
        NewsDbContext db,
        IBackgroundJobClient jobClient,
        ILogger<NewsJobsController> logger)
    {
        _db = db;
        _jobClient = jobClient;
        _logger = logger;
    }

    /// <summary>
    /// Manually enqueues a processing job for the given source.
    /// Uses a deterministic job ID to prevent duplicate enqueues.
    /// </summary>
    [HttpPost("{id:int}/process")]
    public async Task<IActionResult> TriggerProcessing(int id, CancellationToken ct)
    {
        var source = await _db.NewsSources.FindAsync([id], ct);
        if (source is null) return NotFound();
        if (!source.IsEnabled) return BadRequest(new { error = "Source is disabled." });

        var jobId = $"news-source-{source.Id}";

        _jobClient.Enqueue<Trader.News.Worker.Jobs.ProcessNewsSourceJob>(
            jobId,
            job => job.RunAsync(source.Id, CancellationToken.None));

        _logger.LogInformation(
            "Manual processing triggered for source {Id} ({Name}), jobId={JobId}.",
            source.Id, source.Name, jobId);

        return Accepted(new { jobId, sourceId = source.Id, sourceName = source.Name });
    }
}
