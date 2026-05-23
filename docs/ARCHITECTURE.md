# System Architecture

## Overview

The Trader system is a multi-agent pipeline that ingests market data, evaluates trading signals, manages risk, and executes orders. All external integrations (quotes, brokers, notifications) are hidden behind swappable provider interfaces.

```
┌─────────────────────────────────────────────────────────────────┐
│                        FRONTEND (Next.js PWA)                   │
│   Dashboard · Portfolio · Orders · Alerts · Settings            │
└────────────────────────┬────────────────────────────────────────┘
                         │ REST + WebSocket (SignalR)
┌────────────────────────▼────────────────────────────────────────┐
│                     Trader.Api (ASP.NET Core)                   │
│   Auth (JWT) · REST endpoints · SignalR hub · Health checks     │
└──────┬─────────────────────────────────────────────┬────────────┘
       │ MassTransit / RabbitMQ                       │ MassTransit
┌──────▼──────────────────────────────────────────────────────────┐
│                        Agent Layer                              │
│                                                                 │
│  MarketDataAgent ──► SignalAgent ──► RiskAgent ──► ExecutionAgent
│       │                                                │        │
│  (IQuoteProvider)                           (IBrokerProvider)   │
│                                                         │       │
│  PortfolioAgent ◄───────────────────────────────────────┘       │
│  NotificationAgent ◄──────────────── (INotificationProvider)   │
└─────────────────────────────────────────────────────────────────┘
       │                                               │
┌──────▼──────────────┐                   ┌────────────▼──────────┐
│  TimescaleDB        │                   │  Redis Cache          │
│  (market data,      │                   │  (live quotes,        │
│   orders, trades,   │                   │   sessions,           │
│   positions)        │                   │   rate-limit state)   │
└─────────────────────┘                   └───────────────────────┘
```

## Component Boundaries

| Component | Responsibility | Key Interfaces | Status |
|-----------|----------------|----------------|--------|
| `Trader.Api` | HTTP gateway, JWT token issuance, SignalR hub, rate limiting | — | ✅ Implemented |
| `Trader.MarketData.Api` | REST + gRPC market-data queries, online/DB fallback | `IQuoteProvider` | ✅ Implemented |
| `Trader.MarketData.Worker` | Instrument discovery, historical back-fill, intraday polling | `IQuoteProvider` | ✅ Implemented |
| `Trader.MarketData.Data` | EF Core + TimescaleDB shared data layer | — | ✅ Implemented |
| `Trader.Agents` | Business logic pipeline | `IAgent` | 🛠️ Planned |
| `Trader.Providers` | All external I/O (PPI implemented) | `IQuoteProvider`, `IBrokerProvider`, `INotificationProvider` | ✅ PPI done |
| `Trader.Core` | Domain models, events, shared abstractions | `ITradingStrategy`, `IOrderValidator` | ✅ Implemented |
| `Trader.Infrastructure` | DB, cache, messaging wire-up | `IUnitOfWork`, `IMarketDataRepository` | 🛠️ Planned |
| `frontend/` | Next.js app | — | 🛠️ Planned |

## Agent Responsibilities

### MarketDataAgent
- Connects to the configured `IQuoteProvider` (WebSocket or polling)
- Normalises quotes into `QuoteReceived` domain events
- Publishes to message bus; stores raw data in TimescaleDB

### SignalAgent
- Subscribes to `QuoteReceived`
- Evaluates registered `ITradingStrategy` implementations
- Publishes `TradingSignal` (BUY/SELL/HOLD + confidence)

### RiskAgent
- Subscribes to `TradingSignal`
- Validates against position limits, drawdown rules, daily loss caps
- Publishes `OrderApproved` or `OrderRejected` with reason

### ExecutionAgent
- Subscribes to `OrderApproved`
- Places orders via `IBrokerProvider`
- Publishes `OrderSubmitted`, `OrderFilled`, `OrderFailed`

### PortfolioAgent
- Subscribes to `OrderFilled`
- Recalculates positions and P&L
- Persists state; pushes updates to API hub

### NotificationAgent
- Subscribes to key events (`OrderFilled`, `OrderFailed`, `RiskLimitBreached`)
- Delivers alerts via `INotificationProvider`

## Data Flow (happy path)

```
IQuoteProvider → MarketDataAgent → QuoteReceived
→ SignalAgent → TradingSignal
→ RiskAgent → OrderApproved
→ ExecutionAgent → (IBrokerProvider) → OrderFilled
→ PortfolioAgent → position update → SignalR → frontend
→ NotificationAgent → (INotificationProvider) → user alert
```

## Project Structure (target)

```
trader/
├── src/
│   ├── Trader.Api/              ✅ Gateway API — JWT token issuance, Swagger UI
│   ├── Trader.Core/             ✅ Domain models, events, interfaces
│   ├── Trader.Providers/        ✅ PortfolioPersonal provider (IQuoteProvider + IBrokerProvider)
│   ├── Trader.MarketData.Data/  ✅ EF Core shared data layer (TimescaleDB)
│   ├── Trader.MarketData.Api/   ✅ REST + gRPC market-data query API
│   ├── Trader.MarketData.Worker/ ✅ Background polling worker
│   ├── Trader.Agents/           🛠️ Background agent services (planned)
│   ├── Trader.Infrastructure/   🛠️ EF Core, Redis, MassTransit config (planned)
│   └── Trader.AppHost/          🛠️ .NET Aspire orchestration (optional)
├── frontend/                    🛠️ Next.js 15 app (planned)
├── deploy/
│   ├── docker-compose.yml       On-premise stack
│   └── k8s/                     Kubernetes manifests
├── tests/
│   ├── Trader.UnitTests/
│   └── Trader.IntegrationTests/ ✅ TestContainers-based (15 tests)
└── docs/                        ← you are here
```

## Technology Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Messaging | MassTransit + RabbitMQ | Provider-agnostic; swap to Azure SB / SQS for cloud |
| Time-series DB | PostgreSQL + TimescaleDB | SQL familiarity, good compression, continuous aggregates |
| Real-time push | SignalR | Integrates with ASP.NET Core; supports WebSocket + SSE fallback |
| Resilience | Polly v8 (ResiliencePipeline) | Retry, circuit-breaker, timeout, rate-limiter in one pipeline |
| Auth | JWT + ASP.NET Core Identity | Standard, auditable, cloud-compatible |
| Observability | OpenTelemetry → Grafana/Tempo | Vendor-neutral traces, metrics, logs |
