using Trader.News.Data.Entities;
using Trader.News.Data.Enums;

namespace Trader.News.Api.Models;

// ── Request DTOs ──────────────────────────────────────────────────────────────

/// <summary>Payload for creating a new news source.</summary>
public sealed record CreateNewsSourceRequest(
    string Name,
    string? Description,
    NewsSourceCategory Category,
    bool IsEnabled,
    string Uri,
    string? Username,
    /// <summary>Plain-text password — will be AES-encrypted before persisting.</summary>
    string? Password,
    int SearchIntervalMinutes);

/// <summary>Payload for updating an existing news source.</summary>
public sealed record UpdateNewsSourceRequest(
    string Name,
    string? Description,
    NewsSourceCategory Category,
    bool IsEnabled,
    string Uri,
    string? Username,
    /// <summary>Plain-text password — provide null to leave unchanged.</summary>
    string? Password,
    int SearchIntervalMinutes);

// ── Response DTOs ─────────────────────────────────────────────────────────────

/// <summary>News source representation returned by the API (never exposes the encrypted password).</summary>
public sealed record NewsSourceResponse(
    int Id,
    string Name,
    string? Description,
    NewsSourceCategory Category,
    bool IsEnabled,
    string Uri,
    string? Username,
    int SearchIntervalMinutes,
    DateTime? LastExecution)
{
    public static NewsSourceResponse FromEntity(NewsSource e) => new(
        e.Id, e.Name, e.Description, e.Category,
        e.IsEnabled, e.Uri, e.Username,
        e.SearchIntervalMinutes, e.LastExecution);
}

/// <summary>News item representation returned by the API.</summary>
public sealed record NewsItemResponse(
    long Id,
    int SourceId,
    string? Uri,
    DateTime CreatedAt,
    DateTime? NewsDate,
    string Title,
    string? Summary,
    NewsClassification Classification,
    int? ValuationId,
    double? ValuationScore)
{
    public static NewsItemResponse FromEntity(NewsItem e) => new(
        e.Id, e.SourceId, e.Uri, e.CreatedAt, e.NewsDate,
        e.Title, e.Summary, e.Classification,
        e.ValuationId, e.ValuationScore);
}
