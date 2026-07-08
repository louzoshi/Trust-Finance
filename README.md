# Trust Finance

Full-stack personal finance manager built with a **.NET 8 REST API** and a
**React + TypeScript** single-page app. Users register, sign in with JWT, and
track income and expenses across categories, with a dashboard that summarizes
monthly volume and spending by category.

The project is organized to mirror the backend's layered architecture on the
frontend, and is being grown from a personal-finance tool toward a small
business-management system (sales, products, payment methods).

---

## Tech stack

**Backend**

- .NET 8 / ASP.NET Core Web API
- Entity Framework Core 8 + SQL Server
- JWT bearer authentication, role-based authorization
- Password hashing via ASP.NET Core `PasswordHasher` (PBKDF2)
- FluentValidation-style data annotations
- Swagger / OpenAPI
- xUnit + FluentAssertions + EF Core InMemory (unit tests)
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
Trust-Finance.Web/     React + TypeScript SPA (see its own README)
```

---

## Features

- **Authentication** — register with validation, sign in returning a JWT,
  routes protected by `[Authorize]` and role.
- **Categories** — full CRUD.
- **Transactions** — full CRUD, scoped to the authenticated user.
- **Dashboard** — monthly volume, spend by category, KPI tiles, recent activity.
- **API docs** — Swagger UI in development.

Passwords are hashed with ASP.NET Core's `PasswordHasher<T>` (PBKDF2 with a
per-user salt). Authentication uses JWT so only authorized users reach
protected routes.

---

## Running locally

### Prerequisites

- .NET SDK 8.x
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
dotnet test
```

Unit tests cover the service layer (business rules such as rejecting duplicate
emails and duplicate category slugs) and run against EF Core InMemory, so they
do not need SQL Server.

---

## Roadmap

- Transaction type (income / expense) and balance-based dashboard KPIs
- Multi-tenancy (organizations, per-org roles, EF Core global query filters)
- Sales module (products, sale items with frozen prices, payment methods)
- Server-side reporting endpoints with pagination and date filtering
- Integration tests with Testcontainers, global error handling, CI/CD to Azure
