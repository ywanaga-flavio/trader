---
description: >-
  Use when creating or modifying frontend components, hooks, pages, or API
  client code. Enforces real-time data patterns, mobile-responsive layout,
  auth token handling, and trading UI safety conventions.
applyTo: "frontend/**"
---

# Frontend Rules

See [docs/FRONTEND.md](../../docs/FRONTEND.md) for the full guide.

## Data fetching
- Never call `fetch` directly in components — use hooks (`useQuotes`, `useOrders`, etc.) or TanStack Query
- All REST calls go through `lib/api.ts` which handles auth headers and error normalisation
- Real-time data (live quotes, portfolio updates) uses SignalR via `lib/signalr.ts` — one shared connection, never per-component

## Real-time subscriptions
- Subscribe to SignalR events inside `useEffect` with cleanup (`connection.off`)
- Components subscribe to a pre-built hook, not directly to `connection.on`
- Throttle quote rendering: aggregate ticks client-side, update state at most every 500 ms

## Order mutations — safety rules
- Every order action (place, cancel) **must** show a confirmation dialog before the API call
- Never fire-and-forget from a click handler — always `await` and handle errors visibly
- Disable the submit button while the mutation is in-flight (use TanStack Mutation `isPending`)

## Authentication
- `accessToken` stored in memory only (never `localStorage`)
- `refreshToken` stored in `httpOnly` cookie — do not read or write it from JS
- Attach `Authorization: Bearer <token>` via the interceptor in `lib/api.ts`
- On 401 response: clear tokens and redirect to `/login`

## Mobile-responsive
- Design mobile-first (`sm:` breakpoint = 375 px)
- Bottom navigation on mobile (`< lg`), sidebar on desktop (`lg:`)
- Touch targets minimum 44 × 44 px
- Charts must use Recharts `ResponsiveContainer` — no fixed pixel widths

## Performance
- Wrap frequently re-rendering rows (quote tickers, order rows) in `React.memo`
- Use `@tanstack/react-virtual` for lists longer than 50 items (order book, trade history)
- Lazy-load heavy chart libraries with `next/dynamic`
- Never add global `loading.tsx` that blocks the entire page — use per-component Suspense boundaries

## Never do
- Never store secrets or API keys in frontend code or `NEXT_PUBLIC_*` env vars
- Never call provider APIs directly from the frontend — always go through `Trader.Api`
- Never render raw user input as HTML (XSS risk)
