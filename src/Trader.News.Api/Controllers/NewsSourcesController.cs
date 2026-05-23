using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Trader.News.Api.Models;
using Trader.News.Data;
using Trader.News.Data.Encryption;
using Trader.News.Data.Entities;

namespace Trader.News.Api.Controllers;

/// <summary>
/// CRUD management of news sources.
/// Requires the <c>trader</c> role; GET endpoints also accept <c>marketdata</c>.
/// </summary>
[ApiController]
[Route("api/news-sources")]
[Authorize]
public sealed class NewsSourcesController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly IAesEncryptionService _encryption;
    private readonly ILogger<NewsSourcesController> _logger;

    public NewsSourcesController(
        NewsDbContext db,
        IAesEncryptionService encryption,
        ILogger<NewsSourcesController> logger)
    {
        _db = db;
        _encryption = encryption;
        _logger = logger;
    }

    /// <summary>Returns all news sources (enabled and disabled).</summary>
    [HttpGet]
    [Authorize(Roles = "trader,marketdata")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var sources = await _db.NewsSources
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        return Ok(sources.Select(NewsSourceResponse.FromEntity));
    }

    /// <summary>Returns a single news source by ID.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "trader,marketdata")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var source = await _db.NewsSources.FindAsync([id], ct);
        return source is null ? NotFound() : Ok(NewsSourceResponse.FromEntity(source));
    }

    /// <summary>Creates a new news source. Password is encrypted before persisting.</summary>
    [HttpPost]
    [Authorize(Roles = "trader")]
    public async Task<IActionResult> Create(
        [FromBody] CreateNewsSourceRequest request,
        CancellationToken ct)
    {
        var entity = new NewsSource
        {
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            IsEnabled = request.IsEnabled,
            Uri = request.Uri,
            Username = request.Username,
            PasswordEncrypted = string.IsNullOrEmpty(request.Password)
                ? null
                : _encryption.Encrypt(request.Password),
            SearchIntervalMinutes = request.SearchIntervalMinutes,
        };

        _db.NewsSources.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created news source {Id} ({Name}).", entity.Id, entity.Name);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id },
            NewsSourceResponse.FromEntity(entity));
    }

    /// <summary>Updates an existing news source. Omit <c>password</c> to keep the existing one.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "trader")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateNewsSourceRequest request,
        CancellationToken ct)
    {
        var entity = await _db.NewsSources.FindAsync([id], ct);
        if (entity is null) return NotFound();

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Category = request.Category;
        entity.IsEnabled = request.IsEnabled;
        entity.Uri = request.Uri;
        entity.Username = request.Username;
        entity.SearchIntervalMinutes = request.SearchIntervalMinutes;

        if (!string.IsNullOrEmpty(request.Password))
            entity.PasswordEncrypted = _encryption.Encrypt(request.Password);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated news source {Id} ({Name}).", entity.Id, entity.Name);

        return Ok(NewsSourceResponse.FromEntity(entity));
    }

    /// <summary>Deletes a news source and all its associated news items.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "trader")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.NewsSources.FindAsync([id], ct);
        if (entity is null) return NotFound();

        _db.NewsSources.Remove(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted news source {Id} ({Name}).", entity.Id, entity.Name);

        return NoContent();
    }
}
