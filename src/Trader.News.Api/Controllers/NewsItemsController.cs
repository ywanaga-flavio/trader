using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Trader.News.Api.Models;
using Trader.News.Data;
using Trader.News.Data.Enums;

namespace Trader.News.Api.Controllers;

/// <summary>
/// Read-only query endpoints for persisted news items.
/// Requires the <c>marketdata</c> role (all default users carry it).
/// </summary>
[ApiController]
[Route("api/news-items")]
[Authorize(Roles = "trader,marketdata")]
public sealed class NewsItemsController : ControllerBase
{
    private readonly NewsDbContext _db;

    public NewsItemsController(NewsDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns news items with optional filters.
    /// Results are ordered by <c>newsDate</c> descending (most recent first).
    /// </summary>
    /// <param name="sourceId">Filter by source ID.</param>
    /// <param name="classification">Filter by classification enum value.</param>
    /// <param name="from">UTC lower bound for <c>newsDate</c>.</param>
    /// <param name="to">UTC upper bound for <c>newsDate</c>.</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 50, max 200).</param>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? sourceId,
        [FromQuery] NewsClassification? classification,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 200);
        page = Math.Max(page, 1);

        var query = _db.NewsItems.AsNoTracking();

        if (sourceId.HasValue)
            query = query.Where(n => n.SourceId == sourceId.Value);

        if (classification.HasValue)
            query = query.Where(n => n.Classification == classification.Value);

        if (from.HasValue)
            query = query.Where(n => n.NewsDate >= from.Value);

        if (to.HasValue)
            query = query.Where(n => n.NewsDate <= to.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.NewsDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new
        {
            total,
            page,
            pageSize,
            items = items.Select(NewsItemResponse.FromEntity),
        });
    }

    /// <summary>Returns a single news item by ID.</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var item = await _db.NewsItems.FindAsync([id], ct);
        return item is null ? NotFound() : Ok(NewsItemResponse.FromEntity(item));
    }
}
