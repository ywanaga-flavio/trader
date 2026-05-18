# Provider Abstraction Pattern

## Core Principle

**All external integrations are behind interfaces.** Swapping a provider = changing one DI registration in `appsettings.json`. No other code changes.

## Provider Types

| Interface | Responsibility | Example Implementations |
|-----------|----------------|------------------------|
| `IQuoteProvider` | Real-time and historical market data | AlphaVantage, Binance, Yahoo Finance, Polygon.io |
| `IBrokerProvider` | Place, cancel, and query orders | Alpaca, Interactive Brokers, Binance, Paper Trading |
| `INotificationProvider` | Deliver alerts to users | Email (SMTP), Slack, Telegram, PushNotification |
| `IExchangeRateProvider` | Fiat/crypto FX rates | Open Exchange Rates, CoinGecko |

## Interface Contracts

### IQuoteProvider

```csharp
public interface IQuoteProvider
{
    string ProviderId { get; }                          // e.g. "binance", "alphavantage"
    IAsyncEnumerable<Quote> StreamQuotesAsync(
        IEnumerable<string> symbols,
        CancellationToken ct);
    Task<IReadOnlyList<Quote>> GetHistoricalAsync(
        string symbol, DateTimeOffset from, DateTimeOffset to,
        BarInterval interval, CancellationToken ct);
    Task<Quote> GetLastQuoteAsync(string symbol, CancellationToken ct);
}
```

### IBrokerProvider

```csharp
public interface IBrokerProvider
{
    string ProviderId { get; }
    Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct);
    Task<OrderResult> CancelOrderAsync(string orderId, CancellationToken ct);
    Task<IReadOnlyList<OrderResult>> GetOpenOrdersAsync(CancellationToken ct);
    Task<AccountInfo> GetAccountInfoAsync(CancellationToken ct);
}
```

### INotificationProvider

```csharp
public interface INotificationProvider
{
    string ProviderId { get; }
    Task SendAsync(Notification notification, CancellationToken ct);
}
```

## Configuration-Driven Selection

`appsettings.json`:
```json
{
  "Providers": {
    "Quote": "binance",
    "Broker": "alpaca",
    "Notification": "telegram"
  },
  "Binance": {
    "ApiKey": "",
    "SecretKey": "",
    "WebSocketUrl": "wss://stream.binance.com:9443"
  },
  "Alpaca": {
    "ApiKey": "",
    "SecretKey": "",
    "PaperTrading": true
  },
  "Telegram": {
    "BotToken": "",
    "ChatId": ""
  }
}
```

DI registration (`Trader.Infrastructure/ServiceCollectionExtensions.cs`):
```csharp
services.AddQuoteProvider(config["Providers:Quote"]);   // resolves by ProviderId
services.AddBrokerProvider(config["Providers:Broker"]);
services.AddNotificationProvider(config["Providers:Notification"]);
```

## Adding a New Provider

1. Create folder: `src/Trader.Providers/<Type>/<ProviderName>/`
2. Implement the interface: `<ProviderName><Type>Provider.cs`
3. Register in `ServiceCollectionExtensions.cs` with the `ProviderId` key
4. Add config section to `appsettings.json` and document required keys
5. Write unit tests using a mock HTTP client / WebSocket

> **Use the `/add-provider` prompt** for a guided scaffold of steps 1–5.

## Implemented Providers

### Portfolio Personal (PPI) — Argentine Broker

`ProviderId = "portfoliopersonal"`

Implements both `IQuoteProvider` and `IBrokerProvider`. REST-only (no WebSocket).

**Files**

| File | Purpose |
|------|---------|
| `src/Trader.Providers/PortfolioPersonal/PortfolioPersonalOptions.cs` | Configuration options (credentials, base URL, polling) |
| `src/Trader.Providers/PortfolioPersonal/PortfolioPersonalAuthenticator.cs` | Singleton token manager — login + proactive refresh |
| `src/Trader.Providers/PortfolioPersonal/PortfolioPersonalQuoteProvider.cs` | `IQuoteProvider` — polls `/MarketData/Current` |
| `src/Trader.Providers/PortfolioPersonal/PortfolioPersonalBrokerProvider.cs` | `IBrokerProvider` — orders, account info |
| `src/Trader.Providers/PortfolioPersonal/Models/` | Internal PPI API DTOs (not exposed to core) |
| `src/Trader.Providers/PortfolioPersonal/PortfolioPersonalExtensions.cs` | DI registration extension |

**Authentication**

PPI uses a 4-credential login (no request body):
```
POST /api/1.0/Account/LoginApi
Headers: AuthorizedClient, ClientKey, ApiKey, ApiSecret
```
Returns a token array. Authenticated requests require:
```
Authorization: Bearer {accessToken}
AuthorizedClient: ...
ClientKey: ...
```
Token refresh uses `POST /api/1.0/Account/RefreshToken` with `{ "refreshToken": "..." }`.
The `PortfolioPersonalAuthenticator` handles this transparently, refreshing 2 minutes before expiry.

**Registration**

```csharp
services.AddPortfolioPersonalProviders(configuration.GetSection("PortfolioPersonal"));
```

**appsettings.json**

```json
"PortfolioPersonal": {
  "AuthorizedClient": "",
  "ClientKey": "",
  "ApiKey": "",
  "ApiSecret": "",
  "AccountNumber": "",
  "BaseUrl": "https://clientapi_sandbox.portfoliopersonal.com",
  "QuotePollingIntervalSeconds": 5,
  "TokenRefreshBufferMinutes": 2
}
```

> Credentials must be supplied via environment variables in production:
> `PORTFOLIOPERSONAL__AUTHORIZEDCLIENT`, `PORTFOLIOPERSONAL__CLIENTKEY`,
> `PORTFOLIOPERSONAL__APIKEY`, `PORTFOLIOPERSONAL__APISECRET`

---

## Resilience Wrapper

Every provider call must be wrapped in a Polly `ResiliencePipeline`. Do not add resilience logic inside the provider class itself — apply it at the registration site.

See [RESILIENCE.md](RESILIENCE.md) for the standard pipeline definition.

## Paper Trading / Simulation

The built-in `PaperBrokerProvider` intercepts orders and simulates fills based on current quotes. Set `"Broker": "paper"` for development or backtesting. It implements `IBrokerProvider` exactly like a real broker.

## Testing Providers

```csharp
// Use the provided test double
var fakeQuotes = new FakeQuoteProvider();
fakeQuotes.Enqueue(new Quote { Symbol = "BTC/USD", Price = 65000m });

var agent = new MarketDataAgent(fakeQuotes, ...);
```

`FakeQuoteProvider` and `FakeBrokerProvider` live in `tests/Trader.UnitTests/Fakes/`.
