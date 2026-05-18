# Trader — AI Agent Instructions

Multi-agent trading system for stock and crypto markets. Supports swappable providers, runs cloud or on-premise, with a responsive web/mobile frontend.

## Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for component boundaries, data flow, and agent responsibilities.

**Stack**
- Backend: C# / .NET 9, ASP.NET Core, SignalR (real-time)
- Messaging: MassTransit + RabbitMQ (or Azure Service Bus in cloud)
- Database: PostgreSQL (TimescaleDB extension for time-series market data)
- Cache: Redis (quotes, sessions, rate-limit state
)
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
  Trader.MarketData.Data/        ← EF Core shared data layer (TimescaleDB)
    Entities/  ← InstrumentType, Instrument, QuoteDaily, QuoteIntraday
    MarketDataDbContext.cs
    MarketDataDataExtensions.cs  ← services.AddMarketDataDb(connectionString)
    Migrations/                  ← EF migrations (InitialCreate includes TimescaleDB hypertables)
  Trader.MarketData.Api/         ← REST + gRPC market-data query API
    Controllers/  ← QuotesController, InstrumentsController
    Grpc/         ← QuoteGrpcService (implements quotes.proto)
    Services/     ← QuoteQueryService (online/DB fallback logic)
    Models/       ← QuoteResponse, DailyQuotesResponse, DataSource enum
    Protos/       ← quotes.proto (6 methods incl. StreamQuotes server-streaming)
  Trader.MarketData.Worker/      ← Background worker: instrument discovery + polling
    Configuration/  ← MarketDataWorkerOptions, ProviderConfig, MarketHoursConfig
    Workers/        ← HistoricalQuoteWorker, IntradayQuoteWorker
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

## MarketData Subsystem

Implemented as two separate projects that share the `Trader.MarketData.Data` EF Core layer.

### `Trader.MarketData.Data` — Shared data layer

- **DB tables**: `instrument_types`, `instruments`, `quote_daily` (daily OHLCV, TimescaleDB hypertable on `date`), `quote_intraday` (tick data, hypertable on `timestamp`).
- Every record carries a `ProviderId` string identifying its data origin.
- Register with: `services.AddMarketDataDb(connectionString)`
- Migrations: `dotnet ef migrations add <name> --project src/Trader.MarketData.Data --startup-project src/Trader.MarketData.Api`

### `Trader.MarketData.Api` — REST + gRPC query API

| REST endpoint | Description |
|---|---|
| `GET /api/quotes/last/{symbol}?online=false` | Latest quote |
| `GET /api/quotes/daily/{symbol}?from=&to=&online=false` | Daily OHLCV bars |
| `GET /api/quotes/intraday/{symbol}?date=&online=false` | Intraday ticks |
| `GET /api/quotes/by-type/{instrumentType}?date=` | All quotes by instrument type |
| `GET /api/instruments?query=&market=&type=` | Search instruments |

gRPC service `QuoteService` (see `Protos/quotes.proto`) mirrors the REST API and adds `StreamQuotes` server-streaming.

**Fallback strategy**: `online=false` (default) queries DB only. `online=true` tries the configured provider first; on failure falls back to DB with `dataSource="database_fallback"` in the response.

**Auth**: JWT Bearer (same issuer/audience as rest of system).

**Run**:
```bash
cd src/Trader.MarketData.Api
dotnet run           # REST: http://localhost:5200, gRPC: https://localhost:5201
```

### `Trader.MarketData.Worker` — Background polling worker

- `HistoricalQuoteWorker`: on startup, discovers instruments via `SearchInstrumentsAsync` and back-fills daily bars from `Historical.FromDate` to today (upsert-skip).
- `IntradayQuoteWorker`: polls during configured market hours (per-market time-zone schedule), inserts intraday ticks.
- Config section: `MarketData.Providers[]` — see `src/Trader.MarketData.Worker/appsettings.json` for the full schema.

**Run**:
```bash
cd src/Trader.MarketData.Worker
dotnet run
```

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

## Documentation — Mandatory Rule

> **Every code change, new feature, or modification to existing behaviour MUST be reflected in documentation before the task is considered complete.**

This rule applies to all agents and contributors. Specifically:

1. **Code-level documentation**: Public types, interfaces, and non-obvious methods must have XML doc comments (`<summary>`, `<param>`, `<returns>`). Internal DTOs need at minimum a one-line comment when their purpose is not obvious from the name.

2. **`AGENTS.md` (this file)**: Update the relevant section whenever:
   - A new provider is implemented or an existing one changes its capabilities or configuration.
   - A new agent is added or its message contracts change.
   - A new project/assembly is added to the solution.
   - Build, test, or run instructions change.

3. **`docs/` files**: Update the corresponding document whenever:
   - `docs/PROVIDERS.md` — any provider is added, modified, or deprecated.
   - `docs/ARCHITECTURE.md` — component boundaries, data flows, or the agent list changes.
   - `docs/SECURITY.md` — auth flows, secrets handling, or authorization rules change.
   - `docs/RESILIENCE.md` — Polly pipelines or retry strategies change.
   - `docs/DEPLOYMENT.md` — docker-compose, Kubernetes manifests, or CI/CD pipelines change.
   - `docs/FRONTEND.md` — component structure, SignalR patterns, or auth flows change.
   - `docs/PERFORMANCE.md` — latency targets, caching strategy, or profiling tooling change.

**Checklist before marking any task done:**
- [ ] XML doc comments added/updated for changed public API.
- [ ] `AGENTS.md` updated if providers, agents, projects, or commands changed.
- [ ] Relevant `docs/*.md` file(s) updated to reflect the change.
- [ ] No stale references left in documentation pointing to renamed/deleted files or types.

## Deployment

See [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) for cloud (Azure/AWS/GCP) and on-premise (docker-compose/k8s) setup.

## Frontend

See [docs/FRONTEND.md](docs/FRONTEND.md) for component structure, real-time data patterns, and mobile-responsive guidelines.

## Performance

See [docs/PERFORMANCE.md](docs/PERFORMANCE.md) for latency targets, caching strategy, and profiling notes.
