---
name: add-strategy
description: >-
  Use when adding a new trading strategy to the Trader system. Scaffolds the
  ITradingStrategy implementation, parameters class, and unit tests with
  historical data fixtures. Invoke with: /add-strategy <StrategyName>
argument-hint: "StrategyName (e.g. MovingAverageCrossover, RSIMeanReversion)"
---

# Add Trading Strategy: ${StrategyName}

You are implementing a new **trading strategy** named **${StrategyName}**.

Strategies are evaluated by `SignalAgent` on every `QuoteReceived` event.

## Steps

### 1. Understand the interface

```csharp
// src/Trader.Core/Strategies/ITradingStrategy.cs
public interface ITradingStrategy
{
    string StrategyId { get; }          // unique slug, e.g. "ma-crossover"
    IReadOnlyList<string> Symbols { get; }  // symbols this strategy trades

    Task<TradingSignal?> EvaluateAsync(
        Quote latestQuote,
        IReadOnlyList<Quote> history,   // recent bars, newest first
        CancellationToken ct);
}
```

Return `null` if there is no signal (HOLD). Only return `BUY` or `SELL` with a `Confidence` (0–1).

### 2. Create the strategy class

File: `src/Trader.Agents/SignalAgent/Strategies/${StrategyName}.cs`

```csharp
public sealed class ${StrategyName} : ITradingStrategy
{
    private readonly ${StrategyName}Parameters _params;
    private readonly ILogger<${StrategyName}> _logger;

    public string StrategyId => "${strategy_id}";
    public IReadOnlyList<string> Symbols { get; }

    public ${StrategyName}(
        IOptions<${StrategyName}Parameters> options,
        ILogger<${StrategyName}> logger)
    {
        _params = options.Value;
        _logger = logger;
        Symbols = _params.Symbols;
    }

    public Task<TradingSignal?> EvaluateAsync(
        Quote latest, IReadOnlyList<Quote> history, CancellationToken ct)
    {
        // TODO: strategy logic
        // Example: return a BUY signal
        // return Task.FromResult<TradingSignal?>(new TradingSignal
        // {
        //     StrategyId = StrategyId,
        //     Symbol = latest.Symbol,
        //     Side = SignalSide.Buy,
        //     Confidence = 0.75,
        //     CorrelationId = Guid.NewGuid()
        // });

        return Task.FromResult<TradingSignal?>(null);
    }
}
```

### 3. Create parameters class

File: `src/Trader.Agents/SignalAgent/Strategies/${StrategyName}Parameters.cs`

```csharp
public class ${StrategyName}Parameters
{
    public List<string> Symbols { get; set; } = new();
    // add strategy-specific tuning parameters
    // e.g. public int ShortPeriod { get; set; } = 9;
    //      public int LongPeriod  { get; set; } = 21;
}
```

### 4. Register in DI

In `src/Trader.Infrastructure/ServiceCollectionExtensions.cs`:

```csharp
services.Configure<${StrategyName}Parameters>(
    config.GetSection("Strategies:${StrategyName}"));
services.AddSingleton<ITradingStrategy, ${StrategyName}>();
```

### 5. Add config section

`appsettings.json`:

```json
"Strategies": {
  "${StrategyName}": {
    "Symbols": ["BTC/USD", "ETH/USD"]
  }
}
```

### 6. Write unit tests

File: `tests/Trader.UnitTests/Strategies/${StrategyName}Tests.cs`

Cover:
- Input that should produce a BUY signal
- Input that should produce a SELL signal
- Insufficient history → returns `null`
- Edge cases (flat market, single data point)

Use the quote builder helper:

```csharp
var history = QuoteFixtures.BuildBars("BTC/USD", count: 50,
    trend: PriceTrend.Upward, startPrice: 60_000m);
var signal = await strategy.EvaluateAsync(history[0], history, ct);
Assert.Equal(SignalSide.Buy, signal?.Side);
```

`QuoteFixtures` lives in `tests/Trader.UnitTests/Fixtures/`.

### 7. Verify

```bash
dotnet build
dotnet test --filter "Category=${StrategyName}"
```

### Checklist

- [ ] `StrategyId` is a unique lowercase slug
- [ ] Returns `null` (not HOLD signal) when no signal
- [ ] Parameters configurable via `appsettings.json`
- [ ] No side effects — `EvaluateAsync` is pure (reads only, does not place orders)
- [ ] Unit tests cover at least buy, sell, and no-signal scenarios
- [ ] History length validated — return null if insufficient bars
