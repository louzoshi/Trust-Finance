# Trust Finance

Personal finance and investing manager built as a single **.NET 10 Blazor Server**
app. Track income and expenses across categories, run a B3 portfolio with average
price and real returns, browse what you could buy, and set budgets and goals — with
a dashboard that reports actual net worth, cash plus investments.

It runs as one process against one SQLite file. There is no API to host, no
database server to provision and no build step for the frontend: `dotnet run`
and the app is up. The interface is in Brazilian Portuguese (R$, `dd/MM/yyyy`,
comma decimals); the code and these docs are in English.

Every record — transactions and categories alike — belongs to the user who
created it, enforced in the service layer and covered by tests that run against
a real database with the real migrations applied.

---

## Tech stack

- .NET 10 / ASP.NET Core, Blazor Server with interactive server rendering
- Entity Framework Core 10 + SQLite, migrations applied on startup
- Cookie authentication; passwords hashed with ASP.NET Core `PasswordHasher`
  (PBKDF2 with a per-user salt)
- Market data from [brapi.dev](https://brapi.dev), with an offline sample provider
  so the app works before any token is configured
- `Blazor-ApexCharts` for the charts
- Hand-written CSS on a 4px scale, light/dark themes, responsive to phone width
- xUnit + FluentAssertions, running against in-memory SQLite; Playwright for the
  end-to-end journeys

---

## Repository layout

```
Trust-Finance.Domain/    Entities and pure business logic — no EF, no ASP.NET
  Entities/              User, Category, Transaction, Trade, Budget, alerts, settings
  Finance/               MonthlyFlow: the cash dashboard calculation
  Investing/             Portfolio: positions, average price, realized/unrealized
  Planning/              Budgets, goals and the notification rules
  Slug.cs                "Mercado & Padaria" -> "mercado-padaria"
Trust-Finance.Data/      EF Core: DbContext, entity mappings, migrations
Trust-Finance.App/       The Blazor Server app
  Components/Pages/      Dashboard, transactions, categories, portfolio, market,
                         goals, settings, login, register
  Components/Shared/     Charts, stat tiles, notification tray, palette
  Components/Layout/     Signed-in shell and the bare auth layout
  Services/              Account, Category, Transaction, Investment, Watchlist,
                         Alert, Budget, Notification, Settings, UiState
  Services/Market/       Provider abstraction, brapi.dev client, offline catalogue
  Forms/                 Form models and their validation attributes
  wwwroot/               Stylesheet and the pre-paint theme script
Trust-Finance.Tests/     xUnit tests for the domain and the service layer
Trust-Finance.IntegrationTests/
                         The app booted in-process and driven over HTTP
Trust-Finance.E2ETests/  Playwright journeys against the built app in a real browser
```

The dependency direction is one-way: `App` → `Data` → `Domain`. The domain
project references nothing, which is what keeps the dashboard maths testable
without a database.

---

## Features

- **Authentication** — register and sign in against a cookie session. Every
  page requires it; the login and register pages are the two exceptions.
- **Categories** — full CRUD, owned by the user who created them. Slugs are
  unique per user, so two people can each have a `mercado` category.
- **Transactions** — full CRUD, each with a direction (income or expense), an
  amount stored positive, a date and a category.
- **Portfolio** — buys and sells with fees, weighted average price, realized and
  unrealized results, allocation by asset class and day change.
- **Market** — browse stocks, FIIs, ETFs, BDRs, fixed income and crypto with
  quotes, dividend yield and where each class is traded. Watchlist and price
  alerts included.
- **Goals** — monthly budget per category, monthly contribution target and an
  emergency reserve, each with live progress.
- **Notifications** — a tray fed by price alerts that fired, budgets that blew
  their ceiling, and a contribution goal still unmet late in the month.
- **Dashboard** — net worth (cash plus investments), balance for the month
  against last month, six months of income against expense, spending by
  category, portfolio summary, budget progress and recent activity.
- **Settings** — density, accent colour, start page, privacy mode, market-data
  token and refresh interval, and per-source notification switches.
- **Light and dark theme** — chosen per browser, applied before the first paint
  so there is no flash of the wrong one, and followed by the charts.
- **Responsive** — the sidebar becomes a drawer and tables become cards on a
  phone; no horizontal scrolling.

### Market data

Out of the box the app answers from a built-in catalogue of real B3 tickers at
plausible prices, clearly labelled as samples, so every screen works before you
sign up for anything. Save a free [brapi.dev](https://brapi.dev) token under
**Configurações → Dados de mercado** and the same screens switch to live quotes.
Every provider call degrades rather than throws: a rate limit or a dropped
connection leaves positions priced at cost and flagged, never a blank page.

Nothing on the market screen is investment advice — it is a directory and a price
board.

Amounts are stored positive and the sign lives in the transaction type, so a
typo cannot silently turn an expense into income.

---

## Running locally

Prerequisite: .NET SDK 10.x. That is the whole list — no Node, no Docker, no
database server.

```bash
dotnet run --project Trust-Finance.App
```

Open `http://localhost:5062`, create an account, and you are in. The schema is
applied on every start, so a fresh install gets its tables and an upgrade gets
its new columns without anyone running a migration by hand.

### Where the data lives

A single SQLite file in the per-user data folder:

- Linux — `~/.local/share/TrustFinance/trustfinance.db`
- Windows — `%LOCALAPPDATA%\TrustFinance\trustfinance.db`

To point somewhere else — a scratch database, a copy you want to poke at —
override the connection string:

```bash
ConnectionStrings__Default="Data Source=/tmp/scratch.db" dotnet run --project Trust-Finance.App
```

### Installing it for real use

```bash
dotnet publish Trust-Finance.App -c Release
```

Outside Development the app opens the default browser at startup, so someone who
double-clicked the executable gets a window instead of a silent process. If that
fails — headless box, locked-down desktop — the URL is on the first log line.

### Migrations

```bash
dotnet tool restore
dotnet ef migrations add SomeChange --project Trust-Finance.Data
```

`Trust-Finance.Data` carries an `IDesignTimeDbContextFactory`, so the tooling
works against that project alone without booting the app.

---

## Tests

```bash
dotnet test
```

That runs three suites, each answering a question the one below it cannot.

**Unit** (`Trust-Finance.Tests`) covers the domain calculations (`Portfolio`,
`MonthlyFlow`, `Planning`, `Notices`, `Slug`) and the service layer, and runs
against a real SQLite database held in memory for the length of each test,
created by running the actual migrations. That matters: the unique index on
`(UserId, Slug)`, the cascade from a category to its transactions and
`decimal(18,2)` round-tripping are all enforced by the database, and a test
double would wave them through.

What it checks, beyond the happy paths:

- Per-user isolation — one user cannot read, update or delete another user's
  transactions or categories, and cannot file a transaction against somebody
  else's category
- Slugs collide per user and not across users
- Passwords are stored as a hash, emails are normalized before comparison
- Cents survive the round trip, and deleting a category takes its transactions
- Average price is weighted, moves on a buy and holds on a sell, and does not
  depend on the order trades were entered in
- Fees raise the cost basis on a buy and cut proceeds on a sell
- A holding with no quote is valued at cost and flagged, never at zero
- Budgets count expenses only, so income cannot buy back spent room

**Integration** (`Trust-Finance.IntegrationTests`) boots the whole app in-process
and talks to it over HTTP: every page renders, an anonymous request is sent to
the login, registering sets the cookie and lands on the dashboard, a form post
without its antiforgery token is rejected, and the brapi.dev client survives a
rate limit, a bad status and malformed JSON without throwing. A few tests call
brapi.dev for real, to notice when the response shape changes; they are tagged
so a run without internet can skip them:

```bash
dotnet test --filter "Category!=Network"
```

**End-to-end** (`Trust-Finance.E2ETests`) launches the built app as its own
process and drives it with a headless Chromium through Playwright — recording a
trade and reading the average price back off the screen, seeing both validation
messages at once, filtering the market, following an asset. This is the only
layer that can tell whether a page actually became interactive: a Blazor Server
page is inert until its circuit connects, and no HTTP-level test sees that. The
first run needs the browser downloaded once:

```bash
dotnet build
pwsh Trust-Finance.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
```

---

## Roadmap

Scope is deliberately capped at personal finance. The goal is a v1.0 that a
handful of real people use, not a business-management system.

**Towards v1.0**

- Recurring transactions (salary, rent, subscriptions)
- Date filtering and paging on the transactions list, so a long history stays
  workable
- Dividends received as first-class records, feeding a real yield-on-cost
- Portfolio value over time, charted against CDI
- Editing an account: display name, email, password
- Exporting to CSV, and a backup of the database file that does not involve
  finding it on disk

**Later**

- CSV / OFX statement import and broker note (nota de corretagem) parsing
- Multiple accounts (checking, credit card, cash)
- Tax helpers: monthly sales ceiling, darf estimates
- A public deployment over HTTPS, for the people who would rather not run it
  themselves
