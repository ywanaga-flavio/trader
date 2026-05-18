namespace Trader.Core.Models;

/// <summary>Order book (bids and offers).</summary>
public record OrderBook
{
    public required string Symbol { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public IReadOnlyList<OrderBookLevel> Bids { get; init; } = [];
    public IReadOnlyList<OrderBookLevel> Offers { get; init; } = [];
}

public record OrderBookLevel
{
    public int Position { get; init; }
    public decimal Price { get; init; }
    public decimal Quantity { get; init; }
}
