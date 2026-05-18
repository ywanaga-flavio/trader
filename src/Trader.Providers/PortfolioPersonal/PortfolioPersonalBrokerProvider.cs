using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trader.Core.Models;
using Trader.Core.Providers;
using Trader.Providers.PortfolioPersonal.Models;

namespace Trader.Providers.PortfolioPersonal;

/// <summary>
/// Broker provider for Portfolio Personal (PPI).
///
/// Key behaviours:
/// - PlaceOrderAsync maps an OrderRequest to PpiNewOrder and POSTs to /Order/Confirm.
///   The IdempotencyKey is forwarded as externalID. NO automatic retry (idempotency is
///   the caller's responsibility).
/// - All authenticated endpoints add Authorization: Bearer + AuthorizedClient, ClientKey.
/// - Account balance is read from /Account/BalancesAndPositions.
/// - Account listing is read from /Account/Accounts.
///
/// Capabilities:
///   PlaceOrderAsync       → POST /api/1.0/Order/Confirm
///   GetOrderBudgetAsync   → POST /api/1.0/Order/Budget
///   CancelOrderAsync      → POST /api/1.0/Order/Cancel
///   CancelAllOrdersAsync  → POST /api/1.0/Order/MassCancel
///   GetOpenOrdersAsync    → GET  /api/1.0/Order/ActiveOrders
///   GetOrdersAsync        → GET  /api/1.0/Order/Orders
///   GetOrderAsync         → GET  /api/1.0/Order/Detail
///   GetAccountInfoAsync   → GET  /api/1.0/Account/BalancesAndPositions
///   GetAccountsAsync      → GET  /api/1.0/Account/Accounts
/// </summary>
public sealed class PortfolioPersonalBrokerProvider : IBrokerProvider
{
    public string ProviderId => "portfoliopersonal";

    private readonly PortfolioPersonalAuthenticator _auth;
    private readonly HttpClient _http;
    private readonly PortfolioPersonalOptions _opts;
    private readonly ILogger<PortfolioPersonalBrokerProvider> _logger;

    public PortfolioPersonalBrokerProvider(
        PortfolioPersonalAuthenticator auth,
        HttpClient http,
        IOptions<PortfolioPersonalOptions> options,
        ILogger<PortfolioPersonalBrokerProvider> logger)
    {
        _auth = auth;
        _http = http;
        _opts = options.Value;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // PlaceOrderAsync — POST /api/1.0/Order/Confirm
    // IMPORTANT: No automatic retry. Idempotency is ensured via externalID.
    // -------------------------------------------------------------------------

    public async Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct)
    {
        _logger.LogInformation(
            "PPI placing order: {Side} {Quantity} {Symbol} externalID={IdempotencyKey}",
            request.Side, request.Quantity, request.Symbol, request.IdempotencyKey);

        var ppiOrder = MapToNewOrder(request);

        using var httpRequest = await _auth.BuildAuthorizedRequestAsync(
            HttpMethod.Post, "/api/1.0/Order/Confirm", ct);
        httpRequest.Content = JsonContent.Create(ppiOrder);

        var response = await _http.SendAsync(httpRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "PPI PlaceOrder failed: {Status} {Body}", response.StatusCode, body);
            return OrderResult.Failure($"{(int)response.StatusCode}: {body}");
        }

        var order = await response.Content.ReadFromJsonAsync<PpiClientOrder>(ct);
        return order is null
            ? OrderResult.Failure("PPI returned empty order response")
            : MapToOrderResult(order);
    }

    // -------------------------------------------------------------------------
    // GetOrderBudgetAsync — POST /api/1.0/Order/Budget (dry-run, no cash movement)
    // -------------------------------------------------------------------------

    public async Task<OrderResult> GetOrderBudgetAsync(OrderRequest request, CancellationToken ct)
    {
        var ppiOrder = MapToNewOrder(request);

        using var httpRequest = await _auth.BuildAuthorizedRequestAsync(
            HttpMethod.Post, "/api/1.0/Order/Budget", ct);
        httpRequest.Content = JsonContent.Create(ppiOrder);

        var response = await _http.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var order = await response.Content.ReadFromJsonAsync<PpiClientOrder>(ct);
        return order is null
            ? OrderResult.Failure("PPI returned empty budget response")
            : MapToOrderResult(order);
    }

    // -------------------------------------------------------------------------
    // CancelOrderAsync — POST /api/1.0/Order/Cancel
    // -------------------------------------------------------------------------

    public async Task<OrderResult> CancelOrderAsync(string orderId, CancellationToken ct)
    {
        var cancelRequest = new PpiCancelOrder
        {
            AccountNumber = _opts.AccountNumber ?? string.Empty,
            OrderId = int.TryParse(orderId, out var numId) ? numId : null,
            ExternalId = int.TryParse(orderId, out _) ? null : orderId
        };

        using var httpRequest = await _auth.BuildAuthorizedRequestAsync(
            HttpMethod.Post, "/api/1.0/Order/Cancel", ct);
        httpRequest.Content = JsonContent.Create(cancelRequest);

        var response = await _http.SendAsync(httpRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("PPI CancelOrder failed: {Status} {Body}", response.StatusCode, body);
            return OrderResult.Failure($"{(int)response.StatusCode}: {body}", orderId);
        }

        var order = await response.Content.ReadFromJsonAsync<PpiClientOrder>(ct);
        return order is null
            ? new OrderResult { IsSuccess = true, OrderId = orderId, Status = OrderStatus.Cancelled }
            : MapToOrderResult(order);
    }

    // -------------------------------------------------------------------------
    // CancelAllOrdersAsync — POST /api/1.0/Order/MassCancel
    // -------------------------------------------------------------------------

    public async Task<bool> CancelAllOrdersAsync(string accountNumber, CancellationToken ct)
    {
        var url = $"/api/1.0/Order/MassCancel?accountNumber={Uri.EscapeDataString(accountNumber)}";
        using var httpRequest = await _auth.BuildAuthorizedRequestAsync(HttpMethod.Post, url, ct);

        var response = await _http.SendAsync(httpRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PPI MassCancel failed: {Status}", response.StatusCode);
            return false;
        }

        return true;
    }

    // -------------------------------------------------------------------------
    // GetOpenOrdersAsync — GET /api/1.0/Order/ActiveOrders
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<OrderResult>> GetOpenOrdersAsync(
        string accountNumber, CancellationToken ct)
    {
        var url = $"/api/1.0/Order/ActiveOrders?accountNumber={Uri.EscapeDataString(accountNumber)}";
        using var request = await _auth.BuildAuthorizedRequestAsync(HttpMethod.Get, url, ct);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var orders = await response.Content.ReadFromJsonAsync<PpiClientOrder[]>(ct) ?? [];
        return orders.Select(MapToOrderResult).ToList();
    }

    // -------------------------------------------------------------------------
    // GetOrdersAsync — GET /api/1.0/Order/Orders
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<OrderResult>> GetOrdersAsync(
        string accountNumber,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct)
    {
        var query = new List<string>
        {
            $"accountNumber={Uri.EscapeDataString(accountNumber)}"
        };
        if (from.HasValue) query.Add($"dateFrom={from.Value:o}");
        if (to.HasValue)   query.Add($"dateTo={to.Value:o}");

        var url = "/api/1.0/Order/Orders?" + string.Join("&", query);
        using var request = await _auth.BuildAuthorizedRequestAsync(HttpMethod.Get, url, ct);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var orders = await response.Content.ReadFromJsonAsync<PpiClientOrder[]>(ct) ?? [];
        return orders.Select(MapToOrderResult).ToList();
    }

    // -------------------------------------------------------------------------
    // GetOrderAsync — GET /api/1.0/Order/Detail
    // -------------------------------------------------------------------------

    public async Task<OrderResult?> GetOrderAsync(
        string accountNumber, string orderId, CancellationToken ct)
    {
        var url = $"/api/1.0/Order/Detail" +
                  $"?accountNumber={Uri.EscapeDataString(accountNumber)}" +
                  $"&orderID={Uri.EscapeDataString(orderId)}";

        using var request = await _auth.BuildAuthorizedRequestAsync(HttpMethod.Get, url, ct);
        var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var order = await response.Content.ReadFromJsonAsync<PpiClientOrder>(ct);
        return order is null ? null : MapToOrderResult(order);
    }

    // -------------------------------------------------------------------------
    // GetAccountInfoAsync — GET /api/1.0/Account/BalancesAndPositions
    // -------------------------------------------------------------------------

    public async Task<AccountInfo> GetAccountInfoAsync(
        string accountNumber, CancellationToken ct)
    {
        var url = $"/api/1.0/Account/BalancesAndPositions?accountNumber={Uri.EscapeDataString(accountNumber)}";
        using var request = await _auth.BuildAuthorizedRequestAsync(HttpMethod.Get, url, ct);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var groups = await response.Content.ReadFromJsonAsync<PpiGroupedAvailability[]>(ct) ?? [];

        var balances = groups
            .SelectMany(g => (g.Availability ?? [])
                .Select(a => new CurrencyBalance
                {
                    Currency = g.Currency ?? string.Empty,
                    Available = (decimal)a.Amount,
                    Settlement = a.Settlement
                }))
            .ToList();

        return new AccountInfo
        {
            AccountNumber = accountNumber,
            Balances = balances
        };
    }

    // -------------------------------------------------------------------------
    // GetAccountsAsync — GET /api/1.0/Account/Accounts
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<BrokerAccount>> GetAccountsAsync(CancellationToken ct)
    {
        using var request = await _auth.BuildAuthorizedRequestAsync(
            HttpMethod.Get, "/api/1.0/Account/Accounts", ct);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var accounts = await response.Content.ReadFromJsonAsync<PpiAccount[]>(ct) ?? [];

        return accounts.Select(a => new BrokerAccount
        {
            AccountNumber = a.AccountNumber ?? string.Empty,
            Name = a.Name,
            ExternalId = a.ExternalId
        }).ToList();
    }

    // -------------------------------------------------------------------------
    // Mappings
    // -------------------------------------------------------------------------

    private static PpiNewOrder MapToNewOrder(OrderRequest r) => new()
    {
        AccountNumber = r.AccountNumber,
        Ticker = r.Symbol,
        Quantity = (double)r.Quantity,
        Price = r.Price.HasValue ? (double)r.Price.Value : null,
        ActivationPrice = r.StopPrice.HasValue ? (double)r.StopPrice.Value : null,
        // PPI uses "COMPRA"/"VENTA"
        Operation = r.Side == OrderSide.Buy ? "COMPRA" : "VENTA",
        // PPI order types: "PRECIO_LIMITE", "PRECIO_MERCADO"
        OperationType = r.OrderType == OrderType.Market ? "PRECIO_MERCADO" : "PRECIO_LIMITE",
        Settlement = r.Settlement,
        InstrumentType = r.InstrumentType,
        OperationMaxDate = r.ExpiresAt,
        ExternalId = r.IdempotencyKey,
        // Default quantity type — PPI requires "CANTIDAD"
        QuantityType = "CANTIDAD",
        Disclaimers = []
    };

    private static OrderResult MapToOrderResult(PpiClientOrder o) => new()
    {
        IsSuccess = true,
        OrderId = o.Id.ToString(),
        IdempotencyKey = o.ExternalId,
        Symbol = o.Ticker,
        Side = MapSide(o.Operation),
        Status = MapStatus(o.Status),
        Quantity = (decimal?)o.Quantity,
        Price = o.Price.HasValue ? (decimal)o.Price.Value : null,
        Amount = (decimal?)o.Amount,
        Currency = o.Currency,
        Settlement = o.Settlement,
        CreatedAt = o.Date,
        RawStatus = o.Status
    };

    private static OrderSide? MapSide(string? op) => op?.ToUpperInvariant() switch
    {
        "COMPRA" => OrderSide.Buy,
        "VENTA"  => OrderSide.Sell,
        _        => null
    };

    private static OrderStatus MapStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "PENDIENTE"          => OrderStatus.Pending,
        "ACTIVA"             => OrderStatus.Open,
        "PARCIALMENTE_LLENA" => OrderStatus.PartiallyFilled,
        "LLENA"              => OrderStatus.Filled,
        "CANCELADA"          => OrderStatus.Cancelled,
        "RECHAZADA"          => OrderStatus.Rejected,
        "EXPIRADA"           => OrderStatus.Expired,
        _                    => OrderStatus.Unknown
    };

}
