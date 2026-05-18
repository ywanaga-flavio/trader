# Frontend

## Stack

- **Next.js 15** (App Router, React Server Components where applicable)
- **React 19**
- **Tailwind CSS** — responsive utility-first styling
- **shadcn/ui** — accessible component primitives
- **@microsoft/signalr** — real-time connection to the API hub
- **TanStack Query v5** — server state, caching, background refresh
- **Recharts** — candlestick and portfolio charts
- **PWA** (`next-pwa`) — installable on mobile, works offline for read-only views
- **Zustand** — lightweight client-side state (active symbol, UI preferences)

---

## Project Structure

```
frontend/
├── app/
│   ├── (auth)/
│   │   ├── login/page.tsx
│   │   └── layout.tsx
│   ├── (app)/
│   │   ├── dashboard/page.tsx       live quotes, portfolio summary
│   │   ├── portfolio/page.tsx       positions, P&L chart
│   │   ├── orders/page.tsx          order book, place/cancel
│   │   ├── alerts/page.tsx          notification history
│   │   └── settings/page.tsx        provider config, preferences
│   └── layout.tsx                   root layout, auth guard
├── components/
│   ├── charts/
│   │   ├── CandlestickChart.tsx
│   │   └── PortfolioChart.tsx
│   ├── trading/
│   │   ├── QuoteTicker.tsx          live price strip
│   │   ├── OrderForm.tsx
│   │   └── PositionRow.tsx
│   └── ui/                          shadcn/ui re-exports
├── lib/
│   ├── api.ts                       typed REST client (fetch + JWT)
│   ├── signalr.ts                   hub connection singleton
│   └── auth.ts                      token storage, refresh logic
├── hooks/
│   ├── useQuotes.ts                 subscribe to live quotes via SignalR
│   ├── usePortfolio.ts
│   └── useOrders.ts
└── public/
    └── manifest.json                PWA manifest
```

---

## Real-Time Data Pattern

Use a **single persistent SignalR connection**. Components subscribe to events through a React context, not individual connections.

```tsx
// lib/signalr.ts
const connection = new HubConnectionBuilder()
  .withUrl('/hubs/trading', { accessTokenFactory: () => getAccessToken() })
  .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
  .configureLogging(LogLevel.Warning)
  .build();

// hooks/useQuotes.ts
export function useQuotes(symbols: string[]) {
  const [quotes, setQuotes] = useState<Record<string, Quote>>({});
  useEffect(() => {
    connection.on('QuoteReceived', (quote: Quote) => {
      if (symbols.includes(quote.symbol))
        setQuotes(prev => ({ ...prev, [quote.symbol]: quote }));
    });
    return () => connection.off('QuoteReceived');
  }, [symbols]);
  return quotes;
}
```

---

## Mobile-Responsive Guidelines

- **Mobile-first breakpoints**: design for `sm` (375px) first, enhance for `lg` (1024px)
- Bottom navigation bar on mobile (`<768px`), sidebar on desktop
- Touch targets minimum 44×44px (WCAG 2.5.5)
- Charts must be scrollable/zoomable on touch; use `recharts` responsive containers
- Order form: large input fields, confirm dialog before submit on mobile
- PWA install prompt on first visit (mobile)

```tsx
// Responsive layout example
<div className="flex flex-col lg:flex-row">
  <Sidebar className="hidden lg:flex" />
  <BottomNav className="flex lg:hidden" />
  <main className="flex-1 p-4">{children}</main>
</div>
```

---

## Authentication Flow

1. POST `/api/auth/login` → receive `{ accessToken, refreshToken }`
2. Store `accessToken` in memory (not localStorage), `refreshToken` in `httpOnly` cookie
3. Attach `Authorization: Bearer <token>` to all API requests via fetch interceptor
4. Auto-refresh 1 minute before expiry using TanStack Query background refetch
5. On 401 → redirect to `/login`, clear tokens

---

## State Management

| State Type | Tool |
|------------|------|
| Server data (quotes, orders, portfolio) | TanStack Query |
| Real-time streaming data | SignalR + local React state |
| UI state (selected symbol, chart interval) | Zustand |
| Auth state | Zustand (persisted to sessionStorage) |

---

## Environment Variables

```bash
# frontend/.env.local
NEXT_PUBLIC_API_URL=http://localhost:7220
NEXT_PUBLIC_HUB_URL=http://localhost:7220/hubs/trading
```

---

## Build & Deploy

```bash
cd frontend
npm install
npm run dev          # development with HMR
npm run build        # production build
npm run start        # serve production build locally

# Docker
docker build -t trader-frontend .
# Static export for CDN
npm run build && next export
```

---

## Key Conventions

- **No direct fetch calls in components** — use hooks or TanStack Query
- **All API calls go through `lib/api.ts`** — handles auth headers and error normalisation
- **Never store secrets in frontend** — only non-sensitive config in `NEXT_PUBLIC_*`
- **Confirm dialogs for all order mutations** — never fire-and-forget from a button click
- **Loading and error states are mandatory** for all async data — use `Suspense` + `ErrorBoundary`
