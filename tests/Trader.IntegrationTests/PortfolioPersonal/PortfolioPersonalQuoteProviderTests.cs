using Microsoft.Extensions.DependencyInjection;
using Trader.Core.Providers;
using Trader.Providers.PortfolioPersonal;

namespace Trader.IntegrationTests.PortfolioPersonal;

/// <summary>
/// Online integration tests for PortfolioPersonalQuoteProvider.
///
/// Tests hit the PPI sandbox. They are skipped when credentials are not present,
/// so the CI pipeline stays green without secrets.
///
/// To run locally:
///   1. Fill in appsettings.test.json (see template — file is .gitignored)
///   OR set environment variables:
///      PORTFOLIOPERSONAL__AUTHORIZEDCLIENT, CLIENTKEY, APIKEY, APISECRET
///
///   2. dotnet test --filter "Category=Online"
/// </summary>
[Trait("Category", "Online")]
public class PortfolioPersonalQuoteProviderTests : IClassFixture<PortfolioPersonalFixture>
{
    private readonly PortfolioPersonalFixture _fx;
    private readonly IQuoteProvider _quotes;

    public PortfolioPersonalQuoteProviderTests(PortfolioPersonalFixture fx)
    {
        _fx = fx;
        _quotes = fx.Services.GetRequiredService<IQuoteProvider>();
    }

    // -------------------------------------------------------------------------
    // Auth
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task Authenticator_ObtainsToken_WhenCredentialsAreValid()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");

        var auth = _fx.Services.GetRequiredService<PortfolioPersonalAuthenticator>();

        var token = await auth.GetAccessTokenAsync(CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(token), "Access token should not be empty.");
    }

    [SkippableFact]
    public async Task Authenticator_ReturnsSameToken_OnSecondCall_BeforeExpiry()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");

        var auth = _fx.Services.GetRequiredService<PortfolioPersonalAuthenticator>();

        var token1 = await auth.GetAccessTokenAsync(CancellationToken.None);
        var token2 = await auth.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(token1, token2);
    }

    // -------------------------------------------------------------------------
    // GetLastQuoteAsync
    // -------------------------------------------------------------------------

    [SkippableTheory]
    [InlineData("GGAL")]
    [InlineData("YPF")]
    public async Task GetLastQuoteAsync_ReturnsQuoteWithPositivePrice(string ticker)
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");

        var quote = await _quotes.GetLastQuoteAsync(ticker, CancellationToken.None);

        Assert.NotNull(quote);
        Assert.Equal(ticker, quote.Symbol);
        Assert.True(quote.Price > 0, $"Price for {ticker} should be positive, got {quote.Price}.");
        Assert.Equal("portfoliopersonal", quote.Provider);
    }

    [SkippableFact]
    public async Task GetLastQuoteAsync_IncludesOhlcvFields()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");

        var quote = await _quotes.GetLastQuoteAsync("GGAL", CancellationToken.None);

        Assert.True(quote.High >= quote.Low, "High should be >= Low.");
        Assert.True(quote.Volume >= 0, "Volume should not be negative.");
        Assert.True(quote.Timestamp > DateTimeOffset.MinValue, "Timestamp should be set.");
    }

    // -------------------------------------------------------------------------
    // GetHistoricalAsync
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GetHistoricalAsync_ReturnsDailyBars_ForPastMonth()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");

        var to = DateTimeOffset.UtcNow.Date;
        var from = to.AddDays(-30);

        var bars = await _quotes.GetHistoricalAsync(
            "GGAL", from, to, Core.Models.BarInterval.Day1, CancellationToken.None);

        Assert.NotNull(bars);
        Assert.NotEmpty(bars);

        foreach (var bar in bars)
        {
            Assert.Equal("GGAL", bar.Symbol);
            Assert.True(bar.High >= bar.Low, $"Bar H/L invalid at {bar.Timestamp}.");
            Assert.True(bar.Close > 0, $"Bar close <= 0 at {bar.Timestamp}.");
        }
    }

    // -------------------------------------------------------------------------
    // GetOrderBookAsync
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GetOrderBookAsync_ReturnsBidsAndOffers()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");

        var book = await _quotes.GetOrderBookAsync("GGAL", CancellationToken.None);

        Assert.NotNull(book);
        Assert.Equal("GGAL", book.Symbol);
        Assert.NotEmpty(book.Bids);
        Assert.NotEmpty(book.Offers);

        var topBid = book.Bids.MinBy(b => b.Position)!;
        var topOffer = book.Offers.MinBy(o => o.Position)!;
        Assert.True(topOffer.Price >= topBid.Price, "Best offer should be >= best bid.");
    }

    // -------------------------------------------------------------------------
    // SearchInstrumentsAsync
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task SearchInstrumentsAsync_ByTicker_ReturnsMatchingInstruments()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");

        var results = await _quotes.SearchInstrumentsAsync(ticker: "GGAL");

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Contains(results, i => i.Ticker.Contains("GGAL", StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public async Task SearchInstrumentsAsync_ByMarket_ReturnsBYMAInstruments()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");

        var results = await _quotes.SearchInstrumentsAsync(market: "BYMA");

        Assert.NotNull(results);
        Assert.NotEmpty(results);
    }

    // -------------------------------------------------------------------------
    // StreamQuotesAsync
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task StreamQuotesAsync_YieldsAtLeastOneQuotePerSymbol()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var symbols = new[] { "GGAL", "YPF" };
        var received = new HashSet<string>();

        await foreach (var quote in _quotes.StreamQuotesAsync(symbols, cts.Token))
        {
            received.Add(quote.Symbol);
            if (received.Count == symbols.Length)
                break;
        }

        foreach (var sym in symbols)
            Assert.Contains(sym, received);
    }
}
