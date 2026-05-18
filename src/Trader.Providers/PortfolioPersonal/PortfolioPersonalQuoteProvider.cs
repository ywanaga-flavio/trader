using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trader.Core.Models;
using Trader.Core.Providers;
using Trader.Providers.PortfolioPersonal.Models;

namespace Trader.Providers.PortfolioPersonal;

/// <summary>
/// Quote provider for Portfolio Personal (PPI).
///
/// PPI does not offer a WebSocket feed. StreamQuotesAsync polls
/// GET /api/1.0/MarketData/Current for each symbol at the configured
/// QuotePollingInterval, yielding a Quote whenever the price changes.
///
/// Capabilities:
///   - StreamQuotesAsync      → polling of /MarketData/Current
///   - GetLastQuoteAsync      → /MarketData/Current
///   - GetHistoricalAsync     → /MarketData/Search (daily OHLCV)
///   - GetOrderBookAsync      → /MarketData/Book
///   - SearchInstrumentsAsync → /MarketData/SearchInstrument
/// </summary>
public sealed class PortfolioPersonalQuoteProvider : IQuoteProvider
{
    public string ProviderId => "portfoliopersonal";

    private readonly PortfolioPersonalAuthenticator _auth;
    private readonly HttpClient _http;
    private readonly PortfolioPersonalOptions _opts;
    private readonly ILogger<PortfolioPersonalQuoteProvider> _logger;

    public PortfolioPersonalQuoteProvider(
        PortfolioPersonalAuthenticator auth,
        HttpClient http,
        IOptions<PortfolioPersonalOptions> options,
        ILogger<PortfolioPersonalQuoteProvider> logger)
    {
        _auth = auth;
        _http = http;
        _opts = options.Value;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // StreamQuotesAsync — polling loop (no WebSocket available)
    // -------------------------------------------------------------------------

    public async IAsyncEnumerable<Quote> StreamQuotesAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var symbolList = symbols.ToList();
        _logger.LogInformation(
            "PPI quote stream started for {Count} symbol(s): {Symbols}",
            symbolList.Count, string.Join(", ", symbolList));

        while (!ct.IsCancellationRequested)
        {
            foreach (var symbol in symbolList)
            {
                if (ct.IsCancellationRequested) yield break;

                Quote? quote = null;
                try
                {
                    quote = await GetLastQuoteAsync(symbol, ct);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PPI failed to fetch quote for {Symbol}", symbol);
                }

                if (quote is not null)
                    yield return quote;
            }

            try
            {
                await Task.Delay(_opts.QuotePollingInterval, ct);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    // -------------------------------------------------------------------------
    // GetLastQuoteAsync — GET /api/1.0/MarketData/Current
    // -------------------------------------------------------------------------

    public async Task<Quote> GetLastQuoteAsync(string symbol, CancellationToken ct)
    {
        var url = $"/api/1.0/MarketData/Current?Ticker={Uri.EscapeDataString(symbol)}";
        using var request = await _auth.BuildAuthorizedRequestAsync(HttpMethod.Get, url, ct);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var price = await response.Content.ReadFromJsonAsync<PpiInstrumentPrice>(ct)
                    ?? throw new InvalidOperationException($"PPI returned null price for {symbol}");

        return MapToQuote(symbol, price);
    }

    // -------------------------------------------------------------------------
    // GetHistoricalAsync — GET /api/1.0/MarketData/Search
    // Returns daily OHLCV bars (PPI returns InstrumentPrice[] — open/high/low/close/vol)
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<Bar>> GetHistoricalAsync(
        string symbol,
        DateTimeOffset from,
        DateTimeOffset to,
        BarInterval interval,
        CancellationToken ct)
    {
        // PPI historical endpoint returns daily data; interval mapping is informational only
        var url = $"/api/1.0/MarketData/Search" +
                  $"?Ticker={Uri.EscapeDataString(symbol)}" +
                  $"&DateFrom={from:o}" +
                  $"&DateTo={to:o}";

        using var request = await _auth.BuildAuthorizedRequestAsync(HttpMethod.Get, url, ct);
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var prices = await response.Content.ReadFromJsonAsync<PpiInstrumentPrice[]>(ct) ?? [];

        return prices.Select(p => new Bar
        {
            Symbol = symbol,
            Timestamp = p.Date,
            Open = (decimal)p.OpeningPrice,
            High = (decimal)p.Max,
            Low = (decimal)p.Min,
            Close = (decimal)p.Price,
            Volume = (decimal)p.Volume,
            Interval = BarInterval.Day1
        }).ToList();
    }

    // -------------------------------------------------------------------------
    // GetOrderBookAsync — GET /api/1.0/MarketData/Book
    // -------------------------------------------------------------------------

    public async Task<OrderBook?> GetOrderBookAsync(string symbol, CancellationToken ct)
    {
        var url = $"/api/1.0/MarketData/Book?Ticker={Uri.EscapeDataString(symbol)}";
        using var request = await _auth.BuildAuthorizedRequestAsync(HttpMethod.Get, url, ct);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var book = await response.Content.ReadFromJsonAsync<PpiInstrumentBook>(ct);
        if (book is null) return null;

        return new OrderBook
        {
            Symbol = symbol,
            Timestamp = book.Date,
            Bids = book.Bids?.Select(MapOffer).ToList() ?? [],
            Offers = book.Offers?.Select(MapOffer).ToList() ?? []
        };
    }

    // -------------------------------------------------------------------------
    // SearchInstrumentsAsync — GET /api/1.0/MarketData/SearchInstrument
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<Instrument>> SearchInstrumentsAsync(
        string? ticker = null,
        string? name = null,
        string? market = null,
        string? type = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(ticker))
            query.Add($"Ticker={Uri.EscapeDataString(ticker)}");
        if (!string.IsNullOrWhiteSpace(name))
            query.Add($"Name={Uri.EscapeDataString(name)}");
        if (!string.IsNullOrWhiteSpace(market))
            query.Add($"Market={Uri.EscapeDataString(market)}");
        if (!string.IsNullOrWhiteSpace(type))
            query.Add($"Type={Uri.EscapeDataString(type)}");

        var url = "/api/1.0/MarketData/SearchInstrument" +
                  (query.Count > 0 ? "?" + string.Join("&", query) : "");

        using var request = await _auth.BuildAuthorizedRequestAsync(HttpMethod.Get, url, ct);
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var instruments = await response.Content.ReadFromJsonAsync<PpiInstrument[]>(ct) ?? [];

        return instruments.Select(i => new Instrument
        {
            Ticker = i.Ticker ?? string.Empty,
            Description = i.Description,
            Currency = i.Currency,
            Type = i.Type,
            Market = i.Market
        }).ToList();
    }

    // -------------------------------------------------------------------------
    // Mappings
    // -------------------------------------------------------------------------

    private static Quote MapToQuote(string symbol, PpiInstrumentPrice p) => new()
    {
        Symbol = symbol,
        Price = (decimal)p.Price,
        Volume = (decimal)p.Volume,
        OpeningPrice = (decimal)p.OpeningPrice,
        High = (decimal)p.Max,
        Low = (decimal)p.Min,
        PreviousClose = (decimal)p.PreviousClose,
        Change = (decimal)p.MarketChange,
        ChangePercent = p.MarketChangePercent,
        Timestamp = p.Date,
        Provider = "portfoliopersonal"
    };

    private static OrderBookLevel MapOffer(PpiInstrumentOffer o) => new()
    {
        Position = o.Position,
        Price = (decimal)o.Price,
        Quantity = (decimal)o.Quantity
    };
}
