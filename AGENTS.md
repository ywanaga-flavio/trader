# Trader — AI Agent Instructions

Multi-agent trading system for stock and crypto markets. Supports swappable providers, runs cloud or on-premise, with a responsive web/mobile frontend.

## Agent Behaviour — Mandatory Rules

- **Clarification before action**: When any requirement, concept, or scope is ambiguous or unclear, the agent MUST ask clarifying questions before proceeding. Do not assume or infer intent when there is genuine ambiguity.
- **Explicit confirmation**: Require explicit per-change confirmation before any code edit. Do not treat generic acknowledgements as blanket approval. Present the planned change scope and wait for confirmation.

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
  Trader.Api/                    ← IMPLEMENTED — Gateway API: JWT auth, token issuance
    Auth/  ← JwtTokenService, LoginRequest, LoginResponse
    Controllers/  ← AuthController (POST /api/auth/token)
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
  Trader.News.Data/              ← IMPLEMENTED — EF Core data layer (DB: trader_news)
    Enums/          ← NewsSourceCategory, NewsClassification, NewsValuation
    Entities/       ← NewsSource, NewsItem
    Encryption/     ← IAesEncryptionService, AesEncryptionService (AES-256-CBC)
    NewsDbContext.cs
    NewsDataExtensions.cs        ← services.AddNewsDb(connectionString)
    NewsDbContextFactory.cs      ← design-time factory for EF migrations
    Migrations/                  ← EF migrations (InitialCreate: news_sources, news_items)
  Trader.News.Worker/            ← IMPLEMENTED — Hangfire server (max 5 workers, queue "news")
    Jobs/           ← NewsSchedulerJob (recurring, every minute), ProcessNewsSourceJob
    Providers/      ← INewsSourceProvider, RssNewsSourceProvider, HtmlNewsSourceProvider,
                       TwitterNewsSourceProvider (scaffold), NewsSourceProviderFactory
  Trader.News.Api/               ← IMPLEMENTED — REST API port 5300 + Hangfire dashboard
    Controllers/    ← NewsSourcesController (CRUD), NewsItemsController (read), NewsJobsController (trigger)
    Models/         ← NewsDtos (request/response DTOs)
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

## Gateway API (`Trader.Api`)

Entry point for all client traffic. Authenticates users and issues JWT tokens consumed by downstream services.

### `POST /api/auth/token`

Request:
```json
{ "username": "trader", "password": "Trader@1234!" }
```
Response:
```json
{ "token": "eyJ...", "expiresAt": "2026-05-18T..." }
```

Use the token as `Authorization: Bearer <token>` on all downstream API calls.

**Roles** (hardcoded for development — replace with DB-backed store before production):

| Username | Roles |
|---|---|
| `admin` | admin, trader, marketdata |
| `trader` | trader, marketdata |
| `viewer` | marketdata |

**JWT config** (shared across all services via `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`):
```json
"Jwt": {
  "Key": "",           // env: JWT__KEY (min 32 chars)
  "Issuer": "TraderApi",
  "Audience": "TraderClients",
  "ExpiryMinutes": 60
}
```

**Run**:
```bash
cd src/Trader.Api
dotnet run           # http://localhost:5000
```

Swagger UI: `http://localhost:5000/swagger`

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

**Auth**: JWT Bearer. Tokens must be issued by `Trader.Api` (`Issuer: TraderApi`). Role `marketdata` is required (all three default users carry it).

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

## News Subsystem

Implemented as three separate projects sharing the `Trader.News.Data` EF Core layer.
Uses a dedicated PostgreSQL database: `trader_news`.

### `Trader.News.Data` — Shared data layer

- **DB tables**: `news_sources`, `news_items` (both in `trader_news` database).
- **Enums (code-only)**: `NewsSourceCategory` (Rss, Media, Blog, Social), `NewsClassification` (Economic, Political, Market, Technology, International, Corporate), `NewsValuation` (Positive, Negative, Neutral).
- **Encryption**: `IAesEncryptionService` / `AesEncryptionService` (AES-256-CBC). Key sourced from env var `NEWS_ENCRYPTION_KEY` or config `NewsEncryption:Key`. Used for `NewsSource.PasswordEncrypted`.
- Register with: `services.AddNewsDb(connectionString)`
- Migrations: `dotnet ef migrations add <name> --project src/Trader.News.Data --startup-project src/Trader.News.Api`

### `Trader.News.Worker` — Hangfire job server

- Hangfire server: `WorkerCount = 5`, queue `"news"`, storage = PostgreSQL schema `hangfire` in `trader_news`.
- `NewsSchedulerJob`: recurring (every minute). Checks all enabled sources against `LastExecution + SearchIntervalMinutes`. Enqueues `ProcessNewsSourceJob` per due source using deterministic job ID `news-source-{id}` to prevent duplicates.
- `ProcessNewsSourceJob`: fetches news via the appropriate `INewsSourceProvider`, persists new items (dedup by source+URI), updates `LastExecution`.
- **Providers**:
  - `RssNewsSourceProvider` — RSS/Atom via `System.ServiceModel.Syndication`
  - `HtmlNewsSourceProvider` — HTML scraping via `HtmlAgilityPack` (schema.org + Open Graph fallback)
  - `TwitterNewsSourceProvider` — scaffold only (logs warning, no-op until API key configured)
  - `NewsSourceProviderFactory` — resolves provider by `NewsSourceCategory`

**Required env vars**:

| Variable | Purpose |
|---|---|
| `NEWS_DB_PWD` | PostgreSQL password for `trader_news` |
| `NEWS_ENCRYPTION_KEY` | AES-256 key (min 1 char, hashed to 32 bytes) for encrypting source passwords |

**Run**:
```bash
cd src/Trader.News.Worker
dotnet run
```

### `Trader.News.Api` — REST API + Hangfire dashboard

**Port**: 5300 (HTTP) / 5301 (HTTPS)

| Endpoint | Description |
|---|---|
| `GET /api/news-sources` | List all news sources (roles: trader, marketdata) |
| `GET /api/news-sources/{id}` | Get single source |
| `POST /api/news-sources` | Create source — password encrypted automatically (role: trader) |
| `PUT /api/news-sources/{id}` | Update source (role: trader) |
| `DELETE /api/news-sources/{id}` | Delete source + items (role: trader) |
| `GET /api/news-items` | Query news items with filters: sourceId, classification, from, to, page, pageSize |
| `GET /api/news-items/{id}` | Get single news item |
| `POST /api/news-sources/{id}/process` | Manually enqueue processing job (role: trader) |
| `/hangfire` | Hangfire dashboard (role: admin, JWT required) |
| `/swagger` | Swagger UI |

**Auth**: JWT Bearer — same `Jwt:Key/Issuer/Audience` config as `Trader.Api`. Roles: `trader`, `marketdata`, `admin`.

**Run**:
```bash
cd src/Trader.News.Api
dotnet run           # http://localhost:5300
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

# Gateway (issues JWT tokens)
cd src/Trader.Api
dotnet run                    # http://localhost:5000

# Set required env vars before starting MarketData services
$env:TRADER_QUOTAS_DB_PWD = "<db_password>"

# Set required env vars before starting News services
$env:NEWS_DB_PWD = "<news_db_password>"
$env:NEWS_ENCRYPTION_KEY = "<aes_key_min_1_char>"

# News Worker (Hangfire job server)
cd src/Trader.News.Worker
dotnet run

# News API
cd src/Trader.News.Api
dotnet run                    # http://localhost:5300

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
