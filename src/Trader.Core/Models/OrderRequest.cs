namespace Trader.Core.Models;

/// <summary>Request to place a new order.</summary>
public record OrderRequest
{
    /// <summary>Broker/account number where the order is placed.</summary>
    public required string AccountNumber { get; init; }

    /// <summary>Instrument ticker.</summary>
    public required string Symbol { get; init; }

    /// <summary>BUY or SELL.</summary>
    public required OrderSide Side { get; init; }

    /// <summary>MARKET, LIMIT, or STOP.</summary>
    public required OrderType OrderType { get; init; }

    public required decimal Quantity { get; init; }

    /// <summary>Required for LIMIT and STOP orders.</summary>
    public decimal? Price { get; init; }

    /// <summary>Activation price for STOP orders.</summary>
    public decimal? StopPrice { get; init; }

    /// <summary>e.g. "24hs", "48hs", "CI" (contado inmediato in Argentina).</summary>
    public string? Settlement { get; init; }

    /// <summary>Instrument type required by some brokers (e.g. "ACCIONES", "BONOS").</summary>
    public string? InstrumentType { get; init; }

    /// <summary>Client-generated idempotency key. Forwarded to the broker as externalID.</summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>Optional expiry for GTD orders.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

public enum OrderSide { Buy, Sell }
public enum OrderType { Market, Limit, Stop }
