# Trust Finance

Full-stack personal finance manager built with a **.NET 10 REST API** and a
**React + TypeScript** single-page app. Users register, sign in with JWT, and
track income and expenses across categories, with a dashboard that summarizes
monthly volume and spending by category.

The project is organized to mirror the backend's layered architecture on the
frontend. Every record — transactions and categories alike — belongs to the user
who created it, enforced in the service layer and covered by integration tests
that run against a real SQL Server.

---

## Tech stack

**Backend**

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core 10 + SQL Server
- JWT bearer authentication, role-based authorization
- Password hashing via ASP.NET Core `PasswordHasher` (PBKDF2)
- FluentValidation-style data annotations
- Swagger / OpenAPI
- xUnit + FluentAssertions — unit tests on EF Core InMemory, integration tests
  against a real SQL Server started by Testcontainers
- Docker Compose for the database

**Frontend** (`Trust-Finance.Web/`)

- Vite + React 19 + TypeScript (strict)
- React Router with JWT-protected routes
- TanStack Query for server state
- Recharts for the dashboard
- CSS design tokens with light/dark themes

---

## Repository layout

```
Trust-Finance.Api/     ASP.NET Core Web API
  Controllers/         HTTP endpoints (thin; delegate to services)
  Services/            Business rules (Account, Category, Transaction, Token)
  Models/              EF Core entities
  ViewModels/          Request/response DTOs and validation
  Data/                DbContext and entity mappings
  Extensions/          ModelState + ClaimsPrincipal helpers
  Migrations/          EF Core migrations
Trust-Finance.Tests/   xUnit unit tests for the service layer
Trust-Finance.IntegrationTests/
  Infrastructure/      Testcontainers + WebApplicationFactory harness
  Api/                 HTTP-level tests per endpoint group
Trust-Finance.Web/     React + TypeScript SPA (see its own README)
```

---

## Features

- **Authentication** — register with validation, sign in returning a JWT,
  routes protected by `[Authorize]` and role.
- **Categories** — full CRUD, owned by the user who created them. Slugs are unique
  per user, so two people can each have a `groceries` category.
- **Transactions** — full CRUD, scoped to the authenticated user.
- **Dashboard** — monthly volume, spend by category, KPI tiles, recent activity.
- **API docs** — Swagger UI in development.

Passwords are hashed with ASP.NET Core's `PasswordHasher<T>` (PBKDF2 with a
per-user salt). Authentication uses JWT so only authorized users reach
protected routes.

---

## Running locally

### Prerequisites

- .NET SDK 10.x
- Node.js 20+
- Docker + Docker Compose

### 1. Start the database

```bash
docker compose up -d
```

This runs SQL Server in a container (see `docker-compose.yml`).

### 2. Configure and run the API

The API reads its connection string and JWT key from configuration. Provide
them via `appsettings.Development.json` or environment variables:

```bash
# example (bash)
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=TrustFinance;User ID=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True"
export JwtKey="a-long-random-development-secret"
```

Apply migrations and run:

```bash
cd Trust-Finance.Api
dotnet ef database update
dotnet run
```

The API listens on `http://localhost:5151`; Swagger is at
`http://localhost:5151/swagger`.

### 3. Run the frontend

```bash
cd Trust-Finance.Web
npm install
npm run dev
```

Open `http://localhost:5173`. The Vite dev server proxies `/api` to the API,
so no CORS configuration is required locally.

---

## Tests

```bash
dotnet test                                             # everything
dotnet test Trust-Finance.Tests                         # unit tests only
dotnet test Trust-Finance.IntegrationTests              # integration tests only
```

**Unit tests** cover the service layer (business rules such as rejecting duplicate
emails and duplicate category slugs) and run against EF Core InMemory, so they
do not need SQL Server.

**Integration tests** boot the API through `WebApplicationFactory<Program>` — the
same `Program.cs` that runs in production — against a throwaway SQL Server that
[Testcontainers](https://testcontainers.com/) starts for the test run. Nothing is
mocked or substituted: requests go through the real middleware pipeline, the real
DI graph, the real EF Core SQL Server provider and the real migrations. A single
container is shared across the suite and [Respawn](https://github.com/jbogard/Respawn)
truncates every table between tests.

They cover what only shows up once the whole stack is wired together:

- JWT issuing and validation — wrong signing key, expired and tampered tokens
- `[Authorize]` and role policies — 401 for anonymous callers, 403 for a
  non-admin reaching the admin area
- Per-user data isolation — one user cannot read, update or delete another
  user's transactions or categories, and cannot file a transaction against
  somebody else's category
- Database constraints the InMemory provider does not enforce — the composite
  unique index on `(UserId, Slug)`, the transaction foreign keys,
  `ON DELETE CASCADE`, and `decimal(18,2)` round-tripping
- The HTTP error contract — status codes and the `ResultViewModel` envelope

The only requirement is a running Docker daemon.

---

## Roadmap

Scope is deliberately capped at personal finance. The goal is a v1.0 that a handful
of real people use, not a business-management system.

**Towards v1.0**

- Transaction type (income / expense) and a balance-based dashboard
- Recurring transactions (salary, rent, subscriptions)
- Server-side summary endpoint plus pagination and date filtering, so the dashboard
  stops loading the full history into the browser
- Auth hardening — refresh tokens, login rate limiting, issuer/audience validation
- Global exception-handling middleware to replace the per-controller try/catch
- A public deployment over HTTPS

**Later**

- CSV / OFX statement import
- Multiple accounts (checking, credit card, cash)
