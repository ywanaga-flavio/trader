# Security

## Threat Model Summary

Trading systems are high-value targets. Key risks:
- Unauthorized order placement (financial loss)
- API key/secret leakage (account takeover)
- Replay attacks on signed requests
- Injection attacks via symbol or order parameters
- Insider data exfiltration

## Authentication & Authorization

### API (internal and external)

- **JWT Bearer tokens** — issued by `Trader.Api`, short-lived (15 min access + 7 day refresh)
- **Roles**: `Viewer` (read-only), `Trader` (place orders), `Admin` (manage config/users)
- **Endpoints that place or cancel orders require `Trader` role** — enforced with `[Authorize(Roles = "Trader")]`
- **SignalR hub** — authenticate with the same JWT via query-string token on WebSocket upgrade

```csharp
// Minimum viable authorization on execution endpoints
[Authorize(Roles = "Trader")]
[HttpPost("orders")]
public Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request) { ... }
```

### Provider API Keys

- **Never store secrets in source code or appsettings committed to VCS**
- Use environment variables, Azure Key Vault, AWS Secrets Manager, or Docker Secrets
- Rotate keys periodically; revoke on any suspected compromise
- `ISecretProvider` abstraction in `Trader.Infrastructure` — swap between local env vars and cloud vault

```json
// appsettings.json — placeholder only, never real values
{
  "Binance": {
    "ApiKey": "",
    "SecretKey": ""
  }
}
```

## Request Signing (Broker/Exchange Calls)

Most broker APIs require HMAC-SHA256 signed requests. Sign in the provider implementation, never expose the raw secret beyond the provider class.

```csharp
private string Sign(string payload)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
    return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLower();
}
```

## Input Validation

- Validate all order parameters at the API boundary (FluentValidation)
- Whitelist allowed trading symbols — reject unknown symbols before forwarding to broker
- Enforce order size limits, price limits, and daily turnover caps in `RiskAgent`

```csharp
public class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator(ISymbolWhitelist whitelist)
    {
        RuleFor(x => x.Symbol).Must(whitelist.Contains).WithMessage("Unknown symbol");
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(MaxQuantity);
        RuleFor(x => x.Price).GreaterThan(0).When(x => x.OrderType == OrderType.Limit);
    }
}
```

## Audit Log

Every trade event must be persisted to an immutable audit table **before** the action is taken:

```
audit_log (id, timestamp, user_id, action, symbol, quantity, price, ip_address, correlation_id, outcome)
```

- Never delete or update audit rows — append-only
- Correlation ID must flow from the API request through all agent events

## Rate Limiting

- API-level rate limiting via ASP.NET Core `RateLimiter` middleware (per user + per IP)
- Provider-level rate limiting via Polly `RateLimiter` (see [RESILIENCE.md](RESILIENCE.md))
- Limits configurable per role: `Viewer` → read-heavy, `Trader` → order limits enforced

## Network Security

| Environment | Recommendation |
|-------------|----------------|
| On-premise | API behind reverse proxy (nginx/Caddy), TLS terminated at edge, internal services on private network |
| Cloud | API Gateway or Load Balancer with WAF, VPC private subnets for DB/cache/MQ |
| All | HTTPS only, HSTS, no HTTP endpoints in production |

## Secrets Rotation

1. Generate new key at the exchange/broker
2. Update secret in vault/env — use zero-downtime dual-key window if possible
3. Redeploy (or use config hot-reload if implemented)
4. Revoke old key
5. Record rotation in audit log

## Dependency Security

- Run `dotnet list package --vulnerable` in CI
- Enable Dependabot / Renovate for automated PRs on CVEs
- Pin Docker base image digests in production Dockerfiles

## OWASP Top 10 Checklist

| Risk | Mitigation |
|------|-----------|
| Broken Access Control | Role-based auth on all write endpoints |
| Cryptographic Failures | TLS everywhere, HMAC signing, no plaintext secrets |
| Injection | Input validation, parameterised EF Core queries |
| Insecure Design | Threat model documented, RiskAgent as safety layer |
| Security Misconfiguration | Environment-based config, no debug endpoints in prod |
| Vulnerable Components | Dependabot + `dotnet list package --vulnerable` in CI |
| Auth Failures | Short-lived JWTs, refresh token rotation, account lockout |
| Data Integrity Failures | Idempotency keys, immutable audit log |
| Logging Failures | Structured Serilog, correlation IDs, audit log |
| SSRF | Provider URLs whitelisted in config; no user-supplied URLs |
