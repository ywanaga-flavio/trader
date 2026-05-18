# Resilience & Fault Tolerance

## Principles

1. **Fail fast, recover gracefully** — don't let a provider outage cascade into agent failures
2. **All external calls are unreliable** — apply a pipeline to every `IQuoteProvider`, `IBrokerProvider`, and `INotificationProvider` call
3. **Bulkhead** — each provider runs in isolated thread pools; one slow provider doesn't starve others
4. **Observe** — every resilience event (retry, circuit open, timeout) emits an OpenTelemetry span and a log entry

## Standard Polly v8 Pipeline

Apply this pipeline at the DI registration site, not inside provider implementations.

```csharp
// Trader.Infrastructure/Resilience/ProviderResiliencePipeline.cs
public static ResiliencePipeline<T> BuildProviderPipeline<T>(
    string providerName,
    ILoggerFactory loggerFactory)
{
    return new ResiliencePipelineBuilder<T>()
        .AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromSeconds(1),
            SegmentsPerWindow = 10
        }))
        .AddTimeout(TimeSpan.FromSeconds(10))
        .AddRetry(new RetryStrategyOptions<T>
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<T>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .Handle<BrokerTransientException>()
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(60),
            OnOpened = args =>
            {
                loggerFactory.CreateLogger(providerName)
                    .LogError("Circuit OPEN for {Provider}", providerName);
                return ValueTask.CompletedTask;
            }
        })
        .Build();
}
```

## Order Execution — Extra Safety

Order placement is **not retried automatically** to avoid duplicate fills. Use idempotency keys.

```csharp
// Always include a client-generated idempotency key
var request = new OrderRequest
{
    Symbol = "BTC/USD",
    Quantity = 0.1m,
    Side = OrderSide.Buy,
    IdempotencyKey = Guid.NewGuid().ToString("N")  // ← required
};
```

The `ExecutionAgent` checks for an existing order with the same `IdempotencyKey` before placing.

## WebSocket Reconnection (Market Data)

Quote streams over WebSocket must auto-reconnect:

```csharp
public async IAsyncEnumerable<Quote> StreamQuotesAsync(
    IEnumerable<string> symbols, [EnumeratorCancellation] CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        await foreach (var quote in ConnectAndStreamAsync(symbols, ct)
                           .WithCancellation(ct))
        {
            yield return quote;
        }
        // exponential backoff before reconnect
        await Task.Delay(_backoff.Next(), ct);
    }
}
```

## Health Checks

Each provider registers a health check. The API exposes `/health` (liveness) and `/health/ready` (readiness including provider status).

```csharp
services.AddHealthChecks()
    .AddCheck<QuoteProviderHealthCheck>("quote-provider")
    .AddCheck<BrokerProviderHealthCheck>("broker-provider")
    .AddNpgSql(connectionString, name: "postgres")
    .AddRedis(redisConnection, name: "redis")
    .AddRabbitMQ(rabbitUri, name: "rabbitmq");
```

## Bulkhead (Isolated Execution)

Use `Bulkhead` from Polly or separate `Channel<T>` queues per agent to prevent one slow consumer from blocking the message bus processing loop.

## Fallback Strategies

| Scenario | Fallback |
|----------|----------|
| Quote provider down | Serve last-known quote from Redis cache (max age: configurable) |
| Broker provider unreachable | Queue order with `PendingRetry` status; retry when circuit closes |
| Notification provider down | Log alert to DB; retry on next notification sweep |

## Observability

Every resilience event should carry:
- `provider.name` attribute
- `resilience.event` attribute (`retry`, `circuit_open`, `timeout`, `rate_limited`)
- Correlation ID from the triggering trade event

See [DEPLOYMENT.md](DEPLOYMENT.md) for Grafana dashboard setup.
