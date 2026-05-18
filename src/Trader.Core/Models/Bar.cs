namespace Trader.Core.Models;

/// <summary>OHLCV bar (candlestick).</summary>
public record Bar
{
    public required string Symbol { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required decimal Open { get; init; }
    public required decimal High { get; init; }
    public required decimal Low { get; init; }
    public required decimal Close { get; init; }
    public decimal Volume { get; init; }
    public BarInterval Interval { get; init; }
    public string? Settlement { get; init; }
}

public enum BarInterval
{
    Minute1,
    Minute5,
    Minute15,
    Minute30,
    Hour1,
    Hour4,
    Day1,
    Week1,
    Month1
}
