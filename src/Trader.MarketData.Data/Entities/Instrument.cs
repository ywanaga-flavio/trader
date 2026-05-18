namespace Trader.MarketData.Data.Entities;

/// <summary>
/// A tradeable financial instrument discovered via <c>IQuoteProvider.SearchInstrumentsAsync</c>.
/// New instruments are inserted automatically when discovered during population.
/// </summary>
public class Instrument
{
    public int Id { get; set; }

    /// <summary>Ticker symbol (e.g. "GGAL", "BTC/USD").</summary>
    public required string Ticker { get; set; }

    public string? Description { get; set; }

    /// <summary>ISO 4217 currency code (e.g. "ARS", "USD").</summary>
    public string? Currency { get; set; }

    /// <summary>Exchange or market (e.g. "BYMA", "COINBASE").</summary>
    public string? Market { get; set; }

    public int? InstrumentTypeId { get; set; }
    public InstrumentType? InstrumentType { get; set; }

    /// <summary>Provider that discovered this instrument.</summary>
    public required string ProviderId { get; set; }

    /// <summary>Whether the worker should continue tracking this instrument.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<QuoteDaily> DailyQuotes { get; set; } = [];
    public ICollection<QuoteIntraday> IntradayQuotes { get; set; } = [];
}
