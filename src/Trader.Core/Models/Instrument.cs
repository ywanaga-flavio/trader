namespace Trader.Core.Models;

/// <summary>Searchable financial instrument.</summary>
public record Instrument
{
    public required string Ticker { get; init; }
    public string? Description { get; init; }
    public string? Currency { get; init; }
    public string? Type { get; init; }
    public string? Market { get; init; }
}
