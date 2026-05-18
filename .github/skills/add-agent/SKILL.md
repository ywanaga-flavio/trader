---
name: add-agent
description: >-
  Use when adding a new trading agent to the Trader system. Scaffolds the
  BackgroundService, MassTransit consumer/publisher, DI registration, and unit
  tests. Invoke with: /add-agent <AgentName>
argument-hint: "AgentName (e.g. ArbitrageAgent, SentimentAgent)"
---

# Add Trading Agent: ${AgentName}

You are implementing a new **Trader agent** named **${AgentName}**.

See [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) for agent responsibilities and the message pipeline.

## Steps

### 1. Understand the pipeline position

Decide what message(s) this agent **consumes** and what it **publishes**:

| Agent type | Typical input | Typical output |
|------------|---------------|----------------|
| Data collector | External feed | Domain event (e.g. `QuoteReceived`) |
| Signal evaluator | `QuoteReceived` | `TradingSignal` |
| Risk validator | `TradingSignal` | `OrderApproved` / `OrderRejected` |
| Executor | `OrderApproved` | `OrderSubmitted`, `OrderFilled` |
| Portfolio tracker | `OrderFilled` | `PortfolioUpdated` |
| Notifier | any alert event | side-effect (no publish) |

### 2. Create domain events (if new)

File: `src/Trader.Core/Events/${AgentName}Event.cs`

```csharp
public record ${AgentName}Event
{
    public Guid CorrelationId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    // add event-specific properties
}
```

Use `record` types — events are immutable value objects.

### 3. Create the agent class

File: `src/Trader.Agents/${AgentName}/${AgentName}.cs`

```csharp
public sealed class ${AgentName} : BackgroundService, IConsumer<InputEvent>
{
    private readonly ILogger<${AgentName}> _logger;
    private readonly IPublishEndpoint _publish;

    public ${AgentName}(ILogger<${AgentName}> logger, IPublishEndpoint publish)
    {
        _logger = logger;
        _publish = publish;
    }

    // Called by MassTransit for each inbound message
    public async Task Consume(ConsumeContext<InputEvent> context)
    {
        using var scope = _logger.BeginScope(new { context.Message.CorrelationId });
        _logger.LogInformation("${AgentName} processing {EventType}", nameof(InputEvent));

        // TODO: business logic

        await _publish.Publish(new OutputEvent
        {
            CorrelationId = context.Message.CorrelationId
        }, context.CancellationToken);
    }

    // BackgroundService lifecycle (use for init / cleanup if needed)
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.CompletedTask;
}
```

### 4. Register in DI

In `src/Trader.Infrastructure/ServiceCollectionExtensions.cs`:

```csharp
services.AddHostedService<${AgentName}>();

// In the MassTransit config block:
cfg.AddConsumer<${AgentName}>();
```

### 5. Write unit tests

File: `tests/Trader.UnitTests/Agents/${AgentName}Tests.cs`

- Test: valid input → correct event published
- Test: invalid input → no publish, error logged
- Test: cancellation respected
- Use `InMemoryTestHarness` from `MassTransit.Testing` to verify publish/consume

```csharp
var harness = new InMemoryTestHarness();
harness.Consumer<${AgentName}>();
await harness.Start();
await harness.InputQueueSendEndpoint.Send(new InputEvent { ... });
Assert.True(await harness.Published.Any<OutputEvent>());
```

### 6. Verify

```bash
dotnet build
dotnet test --filter "Category=${AgentName}"
```

### Checklist

- [ ] Domain events defined as `record` types in `Trader.Core`
- [ ] CorrelationId flows through all events (never create a new one mid-pipeline)
- [ ] Structured logging with `BeginScope` containing CorrelationId
- [ ] Cancellation token forwarded to all async calls
- [ ] Agent registered as `IHostedService` and MassTransit consumer
- [ ] Unit tests cover happy path and error path
