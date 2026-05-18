namespace Trader.MarketData.Data.Entities;

/// <summary>
/// End-of-day (daily) OHLCV snapshot for an instrument.
///
/// TimescaleDB hypertable partitioned by <see cref="Date"/>.
/// Unique constraint: (InstrumentId, Date, Settlement) — allows the same
/// instrument to have separate rows per settlement term (CI, 24hs, 48hs).
/// </summary>
public class QuoteDaily
{
    public long Id { get; set; }

    public int InstrumentId { get; set; }
    public Instrument Instrument { get; set; } = null!;

    /// <summary>Trading date (date only, no time component).</summary>
    public DateOnly Date { get; set; }

    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }

    /// <summary>Total traded volume for the day.</summary>
    public decimal Volume { get; set; }

    public decimal? PreviousClose { get; set; }
    public decimal? Change { get; set; }
    public string? ChangePercent { get; set; }

    /// <summary>Settlement term: "CI", "24hs", "48hs", or null when not applicable.</summary>
    public string? Settlement { get; set; }

    /// <summary>Provider that originated this record (e.g. "portfoliopersonal").</summary>
    public required string ProviderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
