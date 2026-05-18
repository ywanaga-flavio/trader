using System.Text.Json.Serialization;

namespace Trader.Providers.PortfolioPersonal.Models;

internal record PpiNewOrder
{
    [JsonPropertyName("accountNumber")]
    public required string AccountNumber { get; init; }

    [JsonPropertyName("quantity")]
    public double Quantity { get; init; }

    [JsonPropertyName("price")]
    public double? Price { get; init; }

    [JsonPropertyName("activationPrice")]
    public double? ActivationPrice { get; init; }

    [JsonPropertyName("ticker")]
    public required string Ticker { get; init; }

    [JsonPropertyName("instrumentType")]
    public string? InstrumentType { get; init; }

    [JsonPropertyName("quantityType")]
    public string? QuantityType { get; init; }

    [JsonPropertyName("operationTerm")]
    public string? OperationTerm { get; init; }

    [JsonPropertyName("operationMaxDate")]
    public DateTimeOffset? OperationMaxDate { get; init; }

    /// <summary>BUY = "COMPRA", SELL = "VENTA" — values from PPI /Configuration/Operations.</summary>
    [JsonPropertyName("operation")]
    public required string Operation { get; init; }

    [JsonPropertyName("settlement")]
    public string? Settlement { get; init; }

    [JsonPropertyName("operationType")]
    public string? OperationType { get; init; }

    [JsonPropertyName("disclaimers")]
    public List<PpiDisclaimer>? Disclaimers { get; init; }

    /// <summary>Client idempotency key — maps to OrderRequest.IdempotencyKey.</summary>
    [JsonPropertyName("externalID")]
    public string? ExternalId { get; init; }
}

internal record PpiDisclaimer
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; init; }

    [JsonPropertyName("accepted")]
    public bool Accepted { get; init; }
}

internal record PpiCancelOrder
{
    [JsonPropertyName("accountNumber")]
    public required string AccountNumber { get; init; }

    /// <summary>PPI-assigned numeric order ID.</summary>
    [JsonPropertyName("orderID")]
    public int? OrderId { get; init; }

    /// <summary>Client external ID — used when numeric ID is not available.</summary>
    [JsonPropertyName("externalID")]
    public string? ExternalId { get; init; }
}

internal record PpiClientOrder
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("instrumentType")]
    public string? InstrumentType { get; init; }

    [JsonPropertyName("operation")]
    public string? Operation { get; init; }

    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("date")]
    public DateTimeOffset Date { get; init; }

    [JsonPropertyName("settlement")]
    public string? Settlement { get; init; }

    [JsonPropertyName("quantity")]
    public double Quantity { get; init; }

    [JsonPropertyName("orderType")]
    public string? OrderType { get; init; }

    [JsonPropertyName("operationType")]
    public string? OperationType { get; init; }

    [JsonPropertyName("operationMaxDate")]
    public DateTimeOffset? OperationMaxDate { get; init; }

    [JsonPropertyName("price")]
    public double? Price { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("amount")]
    public double Amount { get; init; }

    [JsonPropertyName("externalID")]
    public string? ExternalId { get; init; }
}

internal record PpiFullOrder : PpiClientOrder
{
    [JsonPropertyName("disclaimers")]
    public List<PpiDisclaimer>? Disclaimers { get; init; }
}
