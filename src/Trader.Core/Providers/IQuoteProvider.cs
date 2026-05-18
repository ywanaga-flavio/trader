using Trader.Core.Models;

namespace Trader.Core.Providers;

public interface IQuoteProvider
{
    string ProviderId { get; }

    /// <summary>
    /// Streams live quotes for the given symbols. Implementations that lack WebSocket
    /// support should poll periodically and yield each update. Reconnects automatically.
    /// </summary>
    IAsyncEnumerable<Quote> StreamQuotesAsync(
        IEnumerable<string> symbols,
        CancellationToken ct);

    /// <summary>Returns OHLCV bars between <paramref name="from"/> and <paramref name="to"/>.</summary>
    Task<IReadOnlyList<Bar>> GetHistoricalAsync(
        string symbol,
        DateTimeOffset from,
        DateTimeOffset to,
        BarInterval interval,
        CancellationToken ct);

    /// <summary>Returns the most recent quote for a single symbol.</summary>
    Task<Quote> GetLastQuoteAsync(string symbol, CancellationToken ct);

    /// <summary>
    /// Returns bid/ask order book for a symbol. Returns null if the provider does not support it.
    /// </summary>
    Task<OrderBook?> GetOrderBookAsync(string symbol, CancellationToken ct);

    /// <summary>Searches instruments by ticker, name, market or type.</summary>
    Task<IReadOnlyList<Instrument>> SearchInstrumentsAsync(
        string? ticker = null,
        string? name = null,
        string? market = null,
        string? type = null,
        CancellationToken ct = default);
}
