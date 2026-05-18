# Performance

## Latency Targets

| Operation | Target P99 | Notes |
|-----------|------------|-------|
| Quote update to browser | < 100 ms | WebSocket end-to-end from exchange feed |
| Order placement (API → broker) | < 500 ms | Includes network to broker |
| REST API (read endpoints) | < 50 ms | With Redis cache warm |
| REST API (write endpoints) | < 200 ms | DB write + publish message |
| Portfolio recalculation | < 1 s | After order fill event |
| Historical data query (30d OHLCV) | < 300 ms | TimescaleDB continuous aggregate |

---

## Caching Strategy

```
Request
  │
  ├─ Redis cache hit? → return immediately (< 5 ms)
  │
  └─ Cache miss → DB query → store in Redis → return

Cache TTLs:
  Live quotes:          5 seconds   (or until next quote arrives)
  Last quote (fallback):60 seconds
  OHLCV bars (closed):  indefinite (immutable past data)
  Portfolio snapshot:   10 seconds
  User session:         15 minutes (sliding)
```

Use `IDistributedCache` abstraction — backed by Redis in production, in-memory in tests.

```csharp
// Pattern: cache-aside with typed helper
public async Task<Quote?> GetLastQuoteAsync(string symbol, CancellationToken ct)
{
    var key = $"quote:{symbol}";
    var cached = await _cache.GetAsync<Quote>(key, ct);
    if (cached is not null) return cached;

    var quote = await _db.Quotes
        .Where(q => q.Symbol == symbol)
        .OrderByDescending(q => q.Timestamp)
        .FirstOrDefaultAsync(ct);

    if (quote is not null)
        await _cache.SetAsync(key, quote, TimeSpan.FromSeconds(60), ct);

    return quote;
}
```

---

## Database Optimisations

### TimescaleDB Hypertables

Market data (quotes, OHLCV bars) must use TimescaleDB hypertables for automatic time-based partitioning:

```sql
CREATE TABLE quotes (
    time        TIMESTAMPTZ NOT NULL,
    symbol      TEXT        NOT NULL,
    price       NUMERIC     NOT NULL,
    volume      NUMERIC
);
SELECT create_hypertable('quotes', 'time');
CREATE INDEX ON quotes (symbol, time DESC);
```

### Continuous Aggregates for OHLCV

```sql
CREATE MATERIALIZED VIEW ohlcv_1m
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 minute', time) AS bucket,
    symbol,
    FIRST(price, time) AS open,
    MAX(price)         AS high,
    MIN(price)         AS low,
    LAST(price, time)  AS close,
    SUM(volume)        AS volume
FROM quotes
GROUP BY bucket, symbol;
```

### EF Core Query Tips

- Use `AsNoTracking()` for all read-only queries
- Avoid N+1: use `Include` or explicit joins, not lazy loading
- For bulk inserts of quote data, use `BulkInsert` (EFCore.BulkExtensions) instead of `SaveChanges` in a loop

---

## Message Bus Throughput

- Use **batch consumers** in MassTransit for `QuoteReceived` events (high-volume)
- Configure prefetch count per queue to match consumer throughput
- Monitor queue depth: alert if `QuoteReceived` queue exceeds 10k messages

```csharp
// Batch consumer for quote events
public class QuoteBatchConsumer : IConsumer<Batch<QuoteReceived>>
{
    public async Task Consume(ConsumeContext<Batch<QuoteReceived>> context)
    {
        var quotes = context.Message.Select(m => m.Message.Quote).ToList();
        await _repository.BulkInsertQuotesAsync(quotes);
    }
}
```

---

## SignalR / WebSocket

- Group clients by subscribed symbols: `await Groups.AddToGroupAsync(connId, $"quote:{symbol}")`
- Send only to the relevant group, not broadcast-all
- Throttle SignalR quote pushes to max 1 update/second per symbol per client (configurable)
- Use MessagePack protocol instead of JSON for binary efficiency

```csharp
services.AddSignalR().AddMessagePackProtocol();
```

---

## Agent Processing Pipeline

- `MarketDataAgent` processes incoming quotes on a dedicated `Channel<Quote>` with bounded capacity (backpressure)
- If the channel is full (slow downstream), log a warning and drop the oldest quote (non-critical for tick data)
- Critical events (orders, fills) use unbounded channels with dead-letter handling

```csharp
var channel = Channel.CreateBounded<Quote>(new BoundedChannelOptions(1000)
{
    FullMode = BoundedChannelFullMode.DropOldest
});
```

---

## Frontend Performance

- Use `React.memo` and `useMemo` on quote ticker rows — these re-render at high frequency
- Virtualise long lists (`@tanstack/react-virtual`) for order book and trade history
- Debounce chart data updates: aggregate incoming ticks client-side, update chart every 500 ms
- Code-split routes with `next/dynamic`; the chart library is heavy — lazy load it
- Service Worker (PWA) caches static assets; API calls are never cached by SW

---

## Profiling

```bash
# .NET — collect a trace
dotnet-trace collect --process-id <pid> --duration 00:00:30

# .NET — memory dump
dotnet-dump collect -p <pid>

# PostgreSQL slow queries
SELECT query, mean_exec_time, calls
FROM pg_stat_statements
ORDER BY mean_exec_time DESC LIMIT 20;

# Redis info
redis-cli info stats
redis-cli info memory
```

Baseline benchmarks should be run with `BenchmarkDotNet` for critical paths (quote normalisation, strategy evaluation).
