namespace Trader.Core.Models;

/// <summary>Result of a broker order operation (placement, cancellation, query).</summary>
public record OrderResult
{
    public required bool IsSuccess { get; init; }

    /// <summary>Broker-assigned order ID.</summary>
    public string? OrderId { get; init; }

    /// <summary>Client-provided idempotency key (externalID).</summary>
    public string? IdempotencyKey { get; init; }

    public string? Symbol { get; init; }
    public OrderSide? Side { get; init; }
    public OrderStatus Status { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? FilledQuantity { get; init; }
    public decimal? Price { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public string? Settlement { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Reason code when <see cref="IsSuccess"/> is false (e.g. "InsufficientFunds").</summary>
    public string? FailureReason { get; init; }

    /// <summary>Raw provider-specific status string for logging/debugging.</summary>
    public string? RawStatus { get; init; }

    public static OrderResult Failure(string reason, string? orderId = null) =>
        new() { IsSuccess = false, OrderId = orderId, FailureReason = reason };
}

public enum OrderStatus
{
    Unknown,
    Pending,
    Open,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected,
    Expired
}
