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

| Component | Responsibility | Key Interfaces |
|-----------|----------------|----------------|
| `Trader.Api` | HTTP/WS gateway, auth, rate limiting | — |
| `Trader.Agents` | Business logic pipeline | `IAgent` |
| `Trader.Providers` | All external I/O | `IQuoteProvider`, `IBrokerProvider`, `INotificationProvider` |
| `Trader.Core` | Domain models, events, shared abstractions | `ITradingStrategy`, `IOrderValidator` |
| `Trader.Infrastructure` | DB, cache, messaging wire-up | `IUnitOfWork`, `IMarketDataRepository` |
| `frontend/` | Next.js app | — |

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
│   ├── Trader.Api/              ASP.NET Core host, SignalR hub
│   ├── Trader.Agents/           Background agent services
│   ├── Trader.Core/             Domain models, events, interfaces
│   ├── Trader.Providers/        Provider implementations
│   ├── Trader.Infrastructure/   EF Core, Redis, MassTransit config
│   └── Trader.AppHost/          .NET Aspire orchestration (optional)
├── frontend/                    Next.js 15 app
├── deploy/
│   ├── docker-compose.yml       On-premise stack
│   └── k8s/                     Kubernetes manifests
├── tests/
│   ├── Trader.UnitTests/
│   └── Trader.IntegrationTests/ (TestContainers)
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
