using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trader.MarketData.Api.Models;
using Trader.MarketData.Api.Services;

namespace Trader.MarketData.Api.Controllers;

/// <summary>
/// REST endpoints for querying instruments stored in the local database.
/// </summary>
[Authorize]
[ApiController]
[Route("api/instruments")]
[Produces("application/json")]
public class InstrumentsController : ControllerBase
{
    private readonly QuoteQueryService _svc;

    public InstrumentsController(QuoteQueryService svc) => _svc = svc;

    /// <summary>Searches instruments by ticker, description, market, or instrument type.</summary>
    /// <param name="query">Free-text search against ticker and description (case-insensitive).</param>
    /// <param name="market">Filter by market (e.g. BYMA, NYSE).</param>
    /// <param name="type">Filter by instrument type code (e.g. ACCIONES, CEDEAR).</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InstrumentResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? query = null,
        [FromQuery] string? market = null,
        [FromQuery] string? type = null,
        CancellationToken ct = default)
    {
        var result = await _svc.SearchInstrumentsAsync(query, market, type, ct);
        return Ok(result);
    }
}
