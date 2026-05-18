using System.Text.Json.Serialization;

namespace Trader.Providers.PortfolioPersonal.Models;

internal record PpiAccount
{
    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("externalID")]
    public string? ExternalId { get; init; }
}

internal record PpiGroupedAvailability
{
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("availability")]
    public List<PpiAvailability>? Availability { get; init; }
}

internal record PpiAvailability
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("amount")]
    public double Amount { get; init; }

    [JsonPropertyName("settlement")]
    public string? Settlement { get; init; }
}

internal record PpiMovement
{
    [JsonPropertyName("agreementDate")]
    public DateTimeOffset AgreementDate { get; init; }

    [JsonPropertyName("settlementDate")]
    public DateTimeOffset SettlementDate { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("amount")]
    public double Amount { get; init; }

    [JsonPropertyName("price")]
    public double Price { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }

    [JsonPropertyName("quantity")]
    public double Quantity { get; init; }

    [JsonPropertyName("balance")]
    public double Balance { get; init; }
}
