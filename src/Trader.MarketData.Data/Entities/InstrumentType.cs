namespace Trader.MarketData.Data.Entities;

/// <summary>
/// Catalogue of financial instrument types (e.g. ACCIONES, BONOS, CEDEAR, FCI, CRYPTO).
/// Stored in DB so new types can be added without code changes.
/// </summary>
public class InstrumentType
{
    public int Id { get; set; }

    /// <summary>Short code used in API queries (e.g. "ACCIONES", "CRYPTO").</summary>
    public required string Code { get; set; }

    /// <summary>Human-readable description.</summary>
    public string? Description { get; set; }

    public ICollection<Instrument> Instruments { get; set; } = [];
}
