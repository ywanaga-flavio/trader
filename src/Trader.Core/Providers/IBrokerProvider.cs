using Trader.Core.Models;

namespace Trader.Core.Providers;

public interface IBrokerProvider
{
    string ProviderId { get; }

    /// <summary>
    /// Places a new order. Does NOT retry on failure — duplicate fills are worse than a missed order.
    /// The <see cref="OrderRequest.IdempotencyKey"/> must be forwarded to the broker when supported.
    /// Returns a typed result; never throws on broker business errors.
    /// </summary>
    Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct);

    /// <summary>Cancels an existing order by its broker order ID or external/idempotency ID.</summary>
    Task<OrderResult> CancelOrderAsync(string orderId, CancellationToken ct);

    /// <summary>Cancels all alive orders for an account.</summary>
    Task<bool> CancelAllOrdersAsync(string accountNumber, CancellationToken ct);

    /// <summary>Returns the active (open/pending) orders for the given account.</summary>
    Task<IReadOnlyList<OrderResult>> GetOpenOrdersAsync(string accountNumber, CancellationToken ct);

    /// <summary>Returns all orders between two dates.</summary>
    Task<IReadOnlyList<OrderResult>> GetOrdersAsync(
        string accountNumber,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default);

    /// <summary>Returns the detail of a single order.</summary>
    Task<OrderResult?> GetOrderAsync(string accountNumber, string orderId, CancellationToken ct);

    /// <summary>Returns account balances, cash availability, and positions.</summary>
    Task<AccountInfo> GetAccountInfoAsync(string accountNumber, CancellationToken ct);

    /// <summary>Returns a list of all accounts for the authenticated client.</summary>
    Task<IReadOnlyList<BrokerAccount>> GetAccountsAsync(CancellationToken ct);

    /// <summary>
    /// Returns a budget estimate for an order without placing it.
    /// </summary>
    Task<OrderResult> GetOrderBudgetAsync(OrderRequest request, CancellationToken ct);
}
