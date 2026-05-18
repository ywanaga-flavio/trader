using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trader.MarketData.Api.Models;
using Trader.MarketData.Api.Services;

namespace Trader.MarketData.Api.Controllers;

/// <summary>
/// REST endpoints for querying market quotes.
///
/// Default behaviour: queries the local database.
/// Add <c>?online=true</c> to query the configured provider first (with DB fallback).
/// The response always includes a <c>dataSource</c> field indicating the origin.
/// </summary>
[Authorize]
[ApiController]
[Route("api/quotes")]
[Produces("application/json")]
public class QuotesController : ControllerBase
{
    private readonly QuoteQueryService _svc;

    public QuotesController(QuoteQueryService svc) => _svc = svc;

    /// <summary>Returns the latest quote for a single symbol.</summary>
    /// <param name="symbol">Ticker symbol (e.g. GGAL, BTC/USD).</param>
    /// <param name="online">When true, tries the live provider before the database.</param>
    [HttpGet("last/{symbol}")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLastQuote(
        string symbol, [FromQuery] bool online = false, CancellationToken ct = default)
    {
        var result = await _svc.GetLastQuoteAsync(symbol, online, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Returns daily OHLCV bars for a symbol between two dates.</summary>
    /// <param name="symbol">Ticker symbol.</param>
    /// <param name="from">Start date (YYYY-MM-DD). Defaults to 30 days ago.</param>
    /// <param name="to">End date (YYYY-MM-DD). Defaults to today.</param>
    /// <param name="online">When true, tries the live provider before the database.</param>
    [HttpGet("daily/{symbol}")]
    [ProducesResponseType<DailyQuotesResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDailyQuotes(
        string symbol,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] bool online = false,
        CancellationToken ct = default)
    {
        var effectiveFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var effectiveTo   = to   ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await _svc.GetDailyQuotesAsync(symbol, effectiveFrom, effectiveTo, online, ct);
        return Ok(result);
    }

    /// <summary>Returns intraday ticks for a symbol on a specific date.</summary>
    /// <param name="symbol">Ticker symbol.</param>
    /// <param name="date">Trading date (YYYY-MM-DD). Defaults to today.</param>
    /// <param name="online">When true and date is today, fetches the latest tick first.</param>
    [HttpGet("intraday/{symbol}")]
    [ProducesResponseType<IntradayQuotesResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIntradayQuotes(
        string symbol,
        [FromQuery] DateOnly? date = null,
        [FromQuery] bool online = false,
        CancellationToken ct = default)
    {
        var effectiveDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await _svc.GetIntradayQuotesAsync(symbol, effectiveDate, online, ct);
        return Ok(result);
    }

    /// <summary>Returns the last daily quote for every instrument of a given type.</summary>
    /// <param name="instrumentType">Instrument type code (e.g. ACCIONES, CRYPTO).</param>
    /// <param name="date">Trading date. Defaults to today.</param>
    [HttpGet("by-type/{instrumentType}")]
    [ProducesResponseType<DailyQuotesResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuotesByType(
        string instrumentType,
        [FromQuery] DateOnly? date = null,
        CancellationToken ct = default)
    {
        var effectiveDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await _svc.GetQuotesByTypeAsync(instrumentType, effectiveDate, ct);
        return Ok(result);
    }
}
