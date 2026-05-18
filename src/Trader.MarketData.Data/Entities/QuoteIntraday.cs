namespace Trader.MarketData.Data.Entities;

/// <summary>
/// Intraday tick / snapshot for an instrument — records price evolution during the trading session.
///
/// TimescaleDB hypertable partitioned by <see cref="Timestamp"/>.
/// Unique constraint: (InstrumentId, Timestamp) — one row per tick per instrument.
/// </summary>
public class QuoteIntraday
{
    public long Id { get; set; }

    public int InstrumentId { get; set; }
    public Instrument Instrument { get; set; } = null!;

    /// <summary>Moment this price was captured (UTC).</summary>
    public DateTimeOffset Timestamp { get; set; }

    public decimal Price { get; set; }
    public decimal Volume { get; set; }

    // Optional OHLCV within the polling window (some providers return these)
    public decimal? Open { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }

    public decimal? Change { get; set; }
    public string? ChangePercent { get; set; }

    /// <summary>Provider that originated this record.</summary>
    public required string ProviderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
