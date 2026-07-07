# Trust Finance — Web

React + TypeScript single-page app for the Trust-Finance API. It mirrors the
backend's layered architecture:

| Backend (ASP.NET Core) | Frontend (React) |
|---|---|
| `Models/` / `ViewModels/` | `src/types/` |
| `Services/` | `src/api/` (one module per resource) |
| `Controllers/` | `src/features/` (pages + feature components) |
| `Extensions/` | `src/lib/` (http client, JWT decoding, formatting) |

## Stack

- Vite + React 19 + TypeScript (strict)
- React Router for routing, protected routes via JWT
- TanStack Query for server state (caching + invalidation)
- Recharts for the dashboard charts
- Hand-rolled CSS design tokens with light/dark themes

## Features

- Sign in / sign up against `api/account` (JWT stored client-side, claims
  decoded for the session user)
- Dashboard: monthly volume, category breakdown, KPIs, recent transactions
- Transactions: list, create, edit, delete
- Categories: list, create
- Light/dark theme, persisted per user preference

## Running locally

Prerequisites: Node 20+, the API running on `http://localhost:5151`
(see the root README).

```bash
npm install
npm run dev
```

The dev server proxies `/api/*` to the API, so no CORS configuration is
needed locally. Open http://localhost:5173.

## Production build

```bash
npm run build   # type-checks then bundles to dist/
```

Set `VITE_API_URL` to the deployed API base URL (e.g.
`https://api.example.com/api`) when building for an environment where the
API is not behind the same origin.
