using System.Text.Json.Serialization;

namespace Trader.Providers.PortfolioPersonal.Models;

internal record PpiInstrument
{
    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("market")]
    public string? Market { get; init; }
}

internal record PpiInstrumentPrice
{
    [JsonPropertyName("date")]
    public DateTimeOffset Date { get; init; }

    [JsonPropertyName("price")]
    public double Price { get; init; }

    [JsonPropertyName("volume")]
    public double Volume { get; init; }

    [JsonPropertyName("openingPrice")]
    public double OpeningPrice { get; init; }

    [JsonPropertyName("max")]
    public double Max { get; init; }

    [JsonPropertyName("min")]
    public double Min { get; init; }

    [JsonPropertyName("previousClose")]
    public double PreviousClose { get; init; }

    [JsonPropertyName("marketChange")]
    public double MarketChange { get; init; }

    [JsonPropertyName("marketChangePercent")]
    public string? MarketChangePercent { get; init; }
}

internal record PpiInstrumentIntradayPrice
{
    [JsonPropertyName("date")]
    public DateTimeOffset Date { get; init; }

    [JsonPropertyName("price")]
    public double Price { get; init; }

    [JsonPropertyName("volume")]
    public double Volume { get; init; }
}

internal record PpiInstrumentBook
{
    [JsonPropertyName("date")]
    public DateTimeOffset Date { get; init; }

    [JsonPropertyName("offers")]
    public List<PpiInstrumentOffer>? Offers { get; init; }

    [JsonPropertyName("bids")]
    public List<PpiInstrumentOffer>? Bids { get; init; }
}

internal record PpiInstrumentOffer
{
    [JsonPropertyName("position")]
    public int Position { get; init; }

    [JsonPropertyName("price")]
    public double Price { get; init; }

    [JsonPropertyName("quantity")]
    public double Quantity { get; init; }
}
