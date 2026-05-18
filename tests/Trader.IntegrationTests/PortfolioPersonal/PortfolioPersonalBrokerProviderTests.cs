using Microsoft.Extensions.DependencyInjection;
using Trader.Core.Models;
using Trader.Core.Providers;

namespace Trader.IntegrationTests.PortfolioPersonal;

/// <summary>
/// Online integration tests for PortfolioPersonalBrokerProvider.
///
/// Order placement tests are marked [Fact(Skip=...)] by default and must be
/// explicitly un-skipped and run against sandbox only. Cancel tests run
/// against real sandbox orders to avoid leaving open positions.
///
/// To run locally:
///   Fill credentials in appsettings.test.json then:
///   dotnet test --filter "Category=Online"
/// </summary>
[Trait("Category", "Online")]
public class PortfolioPersonalBrokerProviderTests : IClassFixture<PortfolioPersonalFixture>
{
    private readonly PortfolioPersonalFixture _fx;
    private readonly IBrokerProvider _broker;

    public PortfolioPersonalBrokerProviderTests(PortfolioPersonalFixture fx)
    {
        _fx = fx;
        _broker = fx.Services.GetRequiredService<IBrokerProvider>();
    }

    // -------------------------------------------------------------------------
    // Account
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GetAccountsAsync_ReturnsAtLeastOneAccount()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");

        var accounts = await _broker.GetAccountsAsync(CancellationToken.None);

        Assert.NotNull(accounts);
        Assert.NotEmpty(accounts);

        foreach (var acc in accounts)
            Assert.False(string.IsNullOrWhiteSpace(acc.AccountNumber),
                "Account number should not be empty.");
    }

    [SkippableFact]
    public async Task GetAccountInfoAsync_ReturnsBalances_ForDefaultAccount()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");
        Skip.If(string.IsNullOrWhiteSpace(_fx.Options.AccountNumber),
            "AccountNumber not configured — skipping balance test.");

        var info = await _broker.GetAccountInfoAsync(
            _fx.Options.AccountNumber, CancellationToken.None);

        Assert.NotNull(info);
        Assert.Equal(_fx.Options.AccountNumber, info.AccountNumber);
        Assert.NotEmpty(info.Balances);

        foreach (var bal in info.Balances)
            Assert.False(string.IsNullOrWhiteSpace(bal.Currency),
                "Balance entry should have a currency.");
    }

    // -------------------------------------------------------------------------
    // Order queries (read-only)
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GetOpenOrdersAsync_ReturnsListWithoutError()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");
        Skip.If(string.IsNullOrWhiteSpace(_fx.Options.AccountNumber),
            "AccountNumber not configured.");

        var orders = await _broker.GetOpenOrdersAsync(
            _fx.Options.AccountNumber, CancellationToken.None);

        Assert.NotNull(orders);
        // May be empty in sandbox — just assert no exception and valid structure
        foreach (var o in orders)
            Assert.False(string.IsNullOrWhiteSpace(o.OrderId),
                "Order ID should not be empty.");
    }

    [SkippableFact]
    public async Task GetOrdersAsync_ForPastMonth_ReturnsListWithoutError()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");
        Skip.If(string.IsNullOrWhiteSpace(_fx.Options.AccountNumber),
            "AccountNumber not configured.");

        var to = DateTimeOffset.UtcNow;
        var from = to.AddDays(-30);

        var orders = await _broker.GetOrdersAsync(
            _fx.Options.AccountNumber, from, to, CancellationToken.None);

        Assert.NotNull(orders);
    }

    // -------------------------------------------------------------------------
    // Order budget (dry-run, no cash movement)
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GetOrderBudgetAsync_ReturnsEstimate_ForLimitOrder()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured — skipping online test.");
        Skip.If(string.IsNullOrWhiteSpace(_fx.Options.AccountNumber),
            "AccountNumber not configured.");

        var request = new OrderRequest
        {
            AccountNumber = _fx.Options.AccountNumber,
            Symbol = "GGAL",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Quantity = 1,
            Price = 1m,          // well below market — will not fill
            Settlement = "24hs",
            InstrumentType = "ACCIONES",
            IdempotencyKey = $"budget-test-{Guid.NewGuid():N}"
        };

        var result = await _broker.GetOrderBudgetAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        // Budget can succeed or return a business error (insufficient funds in sandbox)
        // Either way it should not throw
    }

    // -------------------------------------------------------------------------
    // Order placement + cancel — SANDBOX ONLY, skipped by default
    // To run: remove the Skip parameter from the Fact attribute below.
    // -------------------------------------------------------------------------

    [SkippableFact(typeof(SkipException))]
    [Trait("Category", "Destructive")]
    public async Task PlaceOrderAsync_AndCancel_LimitOrder_InSandbox()
    {
        Skip.IfNot(_fx.CredentialsAvailable, "PPI credentials not configured.");
        Skip.If(string.IsNullOrWhiteSpace(_fx.Options.AccountNumber),
            "AccountNumber not configured.");
        // Guard: this test places a real sandbox order. Remove the line below to run it.
        Skip.If(true, "Order placement test — set Skip.If(false) to run against PPI sandbox.");

        var idempotencyKey = $"it-{Guid.NewGuid():N}";

        var request = new OrderRequest
        {
            AccountNumber = _fx.Options.AccountNumber,
            Symbol = "GGAL",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Quantity = 1,
            Price = 1m,           // far below market — will not fill
            Settlement = "24hs",
            InstrumentType = "ACCIONES",
            IdempotencyKey = idempotencyKey
        };

        // Place
        var placed = await _broker.PlaceOrderAsync(request, CancellationToken.None);

        Assert.NotNull(placed);
        Assert.True(placed.IsSuccess,
            $"PlaceOrder failed: {placed.FailureReason}");
        Assert.False(string.IsNullOrWhiteSpace(placed.OrderId),
            "Placed order should have an ID.");

        // Cancel immediately so we don't leave open orders in sandbox
        var cancelled = await _broker.CancelOrderAsync(placed.OrderId!, CancellationToken.None);

        Assert.True(cancelled.IsSuccess,
            $"CancelOrder failed: {cancelled.FailureReason}");
        Assert.True(
            cancelled.Status is OrderStatus.Cancelled or OrderStatus.Unknown,
            $"Expected Cancelled status, got {cancelled.Status}.");
    }
}
