---
mode: agent
description: >-
  Use when adding a new provider to the Trader system (quote, broker, or
  notification). Guides scaffold of interface implementation, DI registration,
  config, and tests. Invoke with: /add-provider <type> <name>
---

# Add Provider: ${type} — ${name}

You are implementing a new **${type} provider** named **${name}** for the Trader system.

Follow [docs/PROVIDERS.md](../../docs/PROVIDERS.md) for the full pattern.

## Steps

### 1. Determine the interface

| type | interface | location |
|------|-----------|----------|
| quote | `IQuoteProvider` | `src/Trader.Providers/Quotes/` |
| broker | `IBrokerProvider` | `src/Trader.Providers/Brokers/` |
| notification | `INotificationProvider` | `src/Trader.Providers/Notifications/` |

Read the interface file to understand all methods you must implement.

### 2. Create the provider class

File path: `src/Trader.Providers/<Type>/<Name>/<Name><Type>Provider.cs`

Requirements:
- Implement the interface completely
- Set `ProviderId` to a lowercase slug, e.g. `"binance"`, `"alpaca"`, `"telegram"`
- Inject `HttpClient` (or `IWebSocketClient`) via constructor — never create `new HttpClient()`
- Wrap every external call in the standard Polly pipeline (see [docs/RESILIENCE.md](../../docs/RESILIENCE.md))
- Use `ILogger<T>` for structured logging; include `ProviderId` and `symbol` in log scopes
- Never store API keys as fields — read from `IOptions<${Name}Options>` injected at construction

### 3. Create options class

File: `src/Trader.Providers/<Type>/<Name>/<Name>Options.cs`

```csharp
public class ${Name}Options
{
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    // add provider-specific fields
}
```

### 4. Register in DI

In `src/Trader.Infrastructure/ServiceCollectionExtensions.cs`, add a case to the provider switch:

```csharp
case "${name_lowercase}":
    services.Configure<${Name}Options>(config.GetSection("${Name}"));
    services.AddHttpClient<${Name}${Type}Provider>();
    services.AddSingleton<I${Type}Provider>(sp =>
    {
        var opts = sp.GetRequiredService<IOptions<${Name}Options>>();
        var http = sp.GetRequiredService<IHttpClientFactory>();
        var logger = sp.GetRequiredService<ILogger<${Name}${Type}Provider>>();
        var pipeline = ProviderResiliencePipeline.Build("${name_lowercase}", sp.GetRequiredService<ILoggerFactory>());
        return new ${Name}${Type}Provider(opts, http, logger, pipeline);
    });
    break;
```

### 5. Add config section to appsettings.json

```json
"${Name}": {
  "ApiKey": "",
  "SecretKey": ""
}
```

Document any additional required keys with comments.

### 6. Write unit tests

File: `tests/Trader.UnitTests/Providers/${Name}${Type}ProviderTests.cs`

- Mock `HttpMessageHandler` to simulate API responses
- Test: successful data retrieval
- Test: transient HTTP error triggers retry
- Test: unexpected response format returns a meaningful exception

### 7. Verify

```bash
dotnet build
dotnet test --filter "Category=${Name}"
```

Set `"Providers:${type}": "${name_lowercase}"` in your local `appsettings.json` and run the API to validate end-to-end.
