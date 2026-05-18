namespace Trader.MarketData.Api.Models;

/// <summary>Indicates where the quote data came from.</summary>
public enum DataSource
{
    /// <summary>Data retrieved live from the configured provider.</summary>
    Online,

    /// <summary>Data served from the local database (default query mode).</summary>
    Database,

    /// <summary>Online query failed; data returned from the database as fallback.</summary>
    DatabaseFallback
}

public record QuoteResponse
{
    public required string Symbol { get; init; }
    public decimal Price { get; init; }
    public decimal Volume { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal? PreviousClose { get; init; }
    public decimal? Change { get; init; }
    public string? ChangePercent { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string? ProviderId { get; init; }
    public string? Settlement { get; init; }
    public DataSource DataSource { get; init; }
    /// <summary>Populated when <see cref="DataSource"/> is <see cref="DataSource.DatabaseFallback"/>.</summary>
    public string? FallbackReason { get; init; }
}

public record DailyQuoteResponse
{
    public required string Symbol { get; init; }
    public DateOnly Date { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
    public decimal? PreviousClose { get; init; }
    public decimal? Change { get; init; }
    public string? ChangePercent { get; init; }
    public string? Settlement { get; init; }
    public string? ProviderId { get; init; }
}

public record DailyQuotesResponse
{
    public required IReadOnlyList<DailyQuoteResponse> Quotes { get; init; }
    public DataSource DataSource { get; init; }
    public string? FallbackReason { get; init; }
}

public record IntradayTickResponse
{
    public required string Symbol { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public decimal Price { get; init; }
    public decimal Volume { get; init; }
    public decimal? Open { get; init; }
    public decimal? High { get; init; }
    public decimal? Low { get; init; }
    public decimal? Change { get; init; }
    public string? ChangePercent { get; init; }
    public string? ProviderId { get; init; }
}

public record IntradayQuotesResponse
{
    public required IReadOnlyList<IntradayTickResponse> Ticks { get; init; }
    public DataSource DataSource { get; init; }
    public string? FallbackReason { get; init; }
}

public record InstrumentResponse
{
    public int Id { get; init; }
    public required string Ticker { get; init; }
    public string? Description { get; init; }
    public string? Currency { get; init; }
    public string? Market { get; init; }
    public string? InstrumentType { get; init; }
    public string? ProviderId { get; init; }
}
