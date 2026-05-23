---
description: >-
  Use when writing or modifying trading operations, authentication, order
  placement, API endpoints, secret handling, input validation, or any security-
  sensitive code. Enforces OWASP Top 10 and trading-specific security rules.
applyTo: "src/**"
---

# Security Rules for Trader Source Code

See [docs/SECURITY.md](../../docs/SECURITY.md) for the full security guide.

## Mandatory checks on every change to `src/**`

### Secrets & credentials
- Never hardcode API keys, passwords, or JWT secrets — use `IOptions<T>` bound to env vars
- `appsettings.json` committed to VCS must contain only empty string placeholders
- Provider secrets are read only inside the provider class; never passed through events or logs
- DB password is injected at runtime from `TRADER_QUOTAS_DB_PWD`; never written to appsettings
- JWT signing key is injected from `JWT__KEY` (minimum 32 characters)

### Authentication & authorization
- Every endpoint that places, modifies, or cancels an order **must** have `[Authorize(Roles = "trader")]`
- Market-data read endpoints require `[Authorize(Roles = "marketdata")]`
- Other read-only endpoints require at least `[Authorize]` (any authenticated user)
- JWT tokens are issued exclusively by `Trader.Api` (`Issuer: TraderApi`). All other services only validate, never issue.
- SignalR hub methods that push order state must verify the user owns the resource

### Input validation
- All `[FromBody]` request models must have a corresponding `AbstractValidator<T>` (FluentValidation)
- Trading symbols must be validated against a whitelist before forwarding to `IBrokerProvider`
- Order quantities and prices must have explicit upper bounds in the validator

### Audit log
- Every order action (place, cancel, fill, reject) must write to `audit_log` **before** the action executes
- Include: `user_id`, `action`, `symbol`, `quantity`, `price`, `ip_address`, `correlation_id`
- Audit table is append-only — no UPDATE or DELETE on audit rows

### Idempotency
- Order placement requests must include a `IdempotencyKey` (client-generated GUID)
- `ExecutionAgent` must check for an existing order with the same key before forwarding to the broker

### Logging safety
- Never log raw secrets, full JWT tokens, or private keys
- Log the `CorrelationId` on every trade event log line

### EF Core / SQL
- Use parameterised queries (EF Core default) — never string-concatenate SQL
- Use `AsNoTracking()` for read-only queries
