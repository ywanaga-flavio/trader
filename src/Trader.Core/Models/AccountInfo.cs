namespace Trader.Core.Models;

/// <summary>Account balance, cash availability, and positions.</summary>
public record AccountInfo
{
    public required string AccountNumber { get; init; }
    public string? AccountName { get; init; }
    public IReadOnlyList<CurrencyBalance> Balances { get; init; } = [];
    public IReadOnlyList<Position> Positions { get; init; } = [];
}

public record CurrencyBalance
{
    public required string Currency { get; init; }
    public decimal Available { get; init; }
    public string? Settlement { get; init; }
}

public record Position
{
    public required string Symbol { get; init; }
    public string? Description { get; init; }
    public string? Currency { get; init; }
    public decimal Quantity { get; init; }
    public decimal LastPrice { get; init; }
    public decimal MarketValue { get; init; }
    public decimal? CostBasis { get; init; }
    public decimal? UnrealizedPnL { get; init; }
}

/// <summary>Simplified account descriptor (for account listing).</summary>
public record BrokerAccount
{
    public required string AccountNumber { get; init; }
    public string? Name { get; init; }
    public string? ExternalId { get; init; }
}
