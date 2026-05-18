---
description: >-
  Use when creating or modifying provider classes, provider interfaces, or DI
  registration for quote, broker, or notification providers. Enforces the
  provider abstraction pattern, resilience wrapping, and configuration
  conventions.
applyTo: "src/Trader.Providers/**"
---

# Provider Pattern Rules

See [docs/PROVIDERS.md](../../docs/PROVIDERS.md) for the full guide.

## Interface compliance
- Every provider must implement the full interface (`IQuoteProvider`, `IBrokerProvider`, or `INotificationProvider`)
- `ProviderId` must be a unique lowercase slug (e.g. `"binance"`, `"alpaca"`)
- The provider class name must follow the pattern `<Name><Type>Provider` (e.g. `BinanceQuoteProvider`)

## Construction
- Never use `new HttpClient()` — inject via `IHttpClientFactory`
- Never read secrets directly from `IConfiguration` — use `IOptions<TOptions>` with a typed options class
- Options class must live alongside the provider: `<Name>Options.cs` in the same folder

## Resilience (mandatory)
- Every call to an external HTTP endpoint or WebSocket must be wrapped in the standard Polly `ResiliencePipeline`
- Apply the pipeline at the **DI registration site** (`ServiceCollectionExtensions.cs`), not inside the provider class
- See [docs/RESILIENCE.md](../../docs/RESILIENCE.md) — use `ProviderResiliencePipeline.Build()`

## WebSocket providers
- Must implement auto-reconnect with exponential backoff
- Must respect the `CancellationToken` on every `await` and `yield return`
- Disconnect and reconnect cleanly on token cancellation

## Order placement (broker providers only)
- Do **not** retry order placement automatically — duplicate fills are worse than a missed order
- Always forward the `IdempotencyKey` from `OrderRequest` to the broker API
- Return a typed `OrderResult` — never throw on a broker business error (e.g. insufficient funds); return a failed result with a reason code

## Testing
- Unit test every provider with a mocked `HttpMessageHandler`
- Fake providers (`FakeQuoteProvider`, `FakeBrokerProvider`) are in `tests/Trader.UnitTests/Fakes/` — use them, don't create new mocks inline
