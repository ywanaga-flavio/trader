namespace Trader.Core.Models;

/// <summary>A single market quote (last trade / snapshot).</summary>
public record Quote
{
    public required string Symbol { get; init; }
    public required decimal Price { get; init; }
    public decimal Volume { get; init; }
    public decimal OpeningPrice { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal PreviousClose { get; init; }
    public decimal Change { get; init; }
    public string? ChangePercent { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? Provider { get; init; }
    public string? Market { get; init; }
    public string? Settlement { get; init; }
}
