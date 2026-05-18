using System.Text.Json.Serialization;

namespace Trader.Providers.PortfolioPersonal.Models;

internal record PpiToken
{
    [JsonPropertyName("creationDate")]
    public DateTimeOffset CreationDate { get; init; }

    [JsonPropertyName("expirationDate")]
    public DateTimeOffset ExpirationDate { get; init; }

    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("expires")]
    public int ExpiresInSeconds { get; init; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("tokenType")]
    public string? TokenType { get; init; }
}

internal record PpiRefreshRequest
{
    [JsonPropertyName("refreshToken")]
    public required string RefreshToken { get; init; }
}
