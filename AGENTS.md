# Trader — AI Agent Instructions

Multi-agent trading system for stock and crypto markets. Supports swappable providers, runs cloud or on-premise, with a responsive web/mobile frontend.

## Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for component boundaries, data flow, and agent responsibilities.

**Stack**
- Backend: C# / .NET 9, ASP.NET Core, SignalR (real-time)
- Messaging: MassTransit + RabbitMQ (or Azure Service Bus in cloud)
- Database: PostgreSQL (TimescaleDB extension for time-series market data)
- Cache: Redis (quotes, sessions, rate-limit state)
- Frontend: Next.js 15 (App Router), React, Tailwind CSS, PWA-ready
- Containers: Docker + docker-compose (on-prem), Kubernetes (cloud)

## Provider Abstraction — The Key Pattern

**Every external integration is hidden behind an interface.** Swapping a provider is changing one DI registration.

See [docs/PROVIDERS.md](docs/PROVIDERS.md) for the full pattern, naming conventions, and examples.

```
src/
  Trader.Core/
    Providers/
      IQuoteProvider.cs          ← interface: StreamQuotes, GetHistorical, GetOrderBook, SearchInstruments
      IBrokerProvider.cs         ← interface: PlaceOrder, CancelOrder, GetAccount, GetOrders
    Models/
      Quote.cs, Bar.cs, OrderBook.cs, Instrument.cs
      OrderRequest.cs, OrderResult.cs, AccountInfo.cs
  Trader.Providers/
    PortfolioPersonal/           ← IMPLEMENTED — Argentine broker (stocks + bonds)
      PortfolioPersonalOptions.cs
      PortfolioPersonalAuthenticator.cs
      PortfolioPersonalQuoteProvider.cs
      PortfolioPersonalBrokerProvider.cs
      PortfolioPersonalExtensions.cs
      Models/  ← PpiToken, PpiOrder, PpiMarketData, PpiAccount (internal DTOs)
```

**Rule**: Never instantiate a provider directly. Always inject `IXxxProvider`. Configuration selects the implementation.

## Implemented Providers

### Portfolio Personal (PPI) — `ProviderId = "portfoliopersonal"`

Argentine broker. Implements **both** `IQuoteProvider` and `IBrokerProvider`. REST-only (no WebSocket).

**IQuoteProvider capabilities:**
- `StreamQuotesAsync` — polls `GET /api/1.0/MarketData/Current` at configurable interval (default 5s)
- `GetLastQuoteAsync` — single symbol snapshot (price, OHLCV, change%)
- `GetHistoricalAsync` — daily OHLCV bars via `GET /api/1.0/MarketData/Search`
- `GetOrderBookAsync` — bid/ask book via `GET /api/1.0/MarketData/Book`
- `SearchInstrumentsAsync` — search by ticker/name/market/type

**IBrokerProvider capabilities:**
- `PlaceOrderAsync` — `POST /api/1.0/Order/Confirm` (no auto-retry; IdempotencyKey → externalID)
- `GetOrderBudgetAsync` — dry-run budget estimate without placing order
- `CancelOrderAsync` / `CancelAllOrdersAsync` — cancel by numeric ID or externalID
- `GetOpenOrdersAsync` / `GetOrdersAsync` / `GetOrderAsync` — order history and state
- `GetAccountInfoAsync` — balances by currency and settlement (CI / 24hs / 48hs)
- `GetAccountsAsync` — list all client accounts

**Auth**: 4-header login (`AuthorizedClient`, `ClientKey`, `ApiKey`, `ApiSecret`). Token auto-refreshes 2 min before expiry via `PortfolioPersonalAuthenticator` (singleton).

**Registration**:
```csharp
services.AddPortfolioPersonalProviders(configuration.GetSection("PortfolioPersonal"));
```

**Required config** (`appsettings.json` + env var overrides):
```json
"PortfolioPersonal": {
  "AuthorizedClient": "",  // env: PORTFOLIOPERSONAL__AUTHORIZEDCLIENT
  "ClientKey": "",         // env: PORTFOLIOPERSONAL__CLIENTKEY
  "ApiKey": "",            // env: PORTFOLIOPERSONAL__APIKEY
  "ApiSecret": "",         // env: PORTFOLIOPERSONAL__APISECRET
  "AccountNumber": "",
  "BaseUrl": "https://clientapi_sandbox.portfoliopersonal.com"
}
```

See [docs/PROVIDERS.md](docs/PROVIDERS.md) for full provider documentation.

## Agent System

Each agent is a background service implementing `IAgent` (or `BackgroundService`). Agents communicate via MassTransit messages, not direct calls.

```
src/
  Trader.Agents/
    MarketDataAgent/    ← subscribes to quote feeds, publishes QuoteReceived
    SignalAgent/        ← evaluates strategies, publishes TradingSignal
    RiskAgent/          ← validates orders against risk rules, publishes OrderApproved/Rejected
    ExecutionAgent/     ← places orders via IBrokerProvider
    PortfolioAgent/     ← tracks positions, P&L
    NotificationAgent/  ← sends alerts via INotificationProvider
```

## Build & Test

```bash
# Solution root
dotnet build
dotnet test

# Run locally
docker-compose up -d          # starts Postgres, Redis, RabbitMQ
cd src/Trader.Api
dotnet run

# Frontend
cd frontend
npm install
npm run dev                   # http://localhost:3000
```

## Key Conventions

- **Configuration**: All provider selections and secrets via `appsettings.json` + environment variable overrides. Never hardcode keys.
- **Resilience**: Use Polly policies (retry, circuit-breaker) on every provider call. See [docs/RESILIENCE.md](docs/RESILIENCE.md).
- **Security**: All trading operations require JWT auth + role-based authorization. See [docs/SECURITY.md](docs/SECURITY.md).
- **Logging**: Structured logging via Serilog. Every trade event must be logged with correlation ID.
- **Testing**: Unit-test provider logic with mock `IXxxProvider`. Integration tests use TestContainers.

## Deployment

See [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) for cloud (Azure/AWS/GCP) and on-premise (docker-compose/k8s) setup.

## Frontend

See [docs/FRONTEND.md](docs/FRONTEND.md) for component structure, real-time data patterns, and mobile-responsive guidelines.

## Performance

See [docs/PERFORMANCE.md](docs/PERFORMANCE.md) for latency targets, caching strategy, and profiling notes.
