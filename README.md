# Trust Finance

Personal finance and investing for someone living in Brazil, as a single **.NET 10
Blazor Server** app. It tracks what comes in and goes out across accounts and cards,
runs a B3 portfolio with average price, payouts and the monthly tax it owes, values a
fixed-income book on the curve and at market, and reports a net worth that is actually
the sum of all three.

It runs as one process against one SQLite file. There is no API to host, no database
server to provision and no build step for the frontend: `dotnet run` and the app is up.
The interface is in Brazilian Portuguese (R$, `dd/MM/yyyy`, comma decimals); the code
and these docs are in English.

![The dashboard: net worth, the month against the last, six months of income against
expense, spending by category, portfolio and budgets](docs/images/dashboard.png)

## What makes it more than a spreadsheet

Three things a general-purpose finance app does not do, and that are wrong more often
than not when they are attempted.

**Fixed income has two honest values, and it shows both.** *Na curva* is the contract —
principal compounded at the agreed rate, what you receive at maturity. *A mercado* is
the remaining flow discounted at today's rate — what selling early would fetch. They
diverge when rates move, and the statement your broker sends you only shows the first.

![The fixed income book: both marks side by side, net of tax and custody, with FGC
coverage per issuer](docs/images/renda-fixa.png)

**The tax on your B3 trades is computed, not estimated.** Day trade netted per ticker
per day at 20%, the R$ 20.000 stock exemption applied to *sales* rather than gains,
FIIs at 20% with no exemption, losses carried forward inside their own bucket, the
broker's withholding credited, and DARFs under R$ 10 accumulating to the next month.

![The tax screen: month by month, with the per-bucket breakdown of one month
expanded](docs/images/impostos.png)

**Statements import without ever duplicating a line.** The bank's own identifier
travels with every imported row under a unique index, so the same OFX file imported
twice creates nothing the second time — and a row you had already typed by hand is
recognised and reconciled instead of duplicated.

![The portfolio: allocation, money-weighted return against the CDI, positions with
yield on cost](docs/images/carteira.png)

## See it running

```bash
git clone https://github.com/louzoshi/Trust-Finance.git
cd Trust-Finance
Auth__Bypass=true Demo__Enabled=true dotnet run --project Trust-Finance.App
```

That opens on `http://localhost:5062` already signed in, with a year of plausible
finances: eight months of a ledger across a checking account and a card, a purchase in
five instalments, recurrences, a B3 portfolio with a split and a year of payouts, and a
fixed-income book that deliberately passes the FGC limit at one issuer so the warning
has something to say.

Every date is relative to the day it runs, so the demo never goes stale. It seeds only
into an empty database and never touches one that already has rows. **Both flags are
for the demo alone** — `Auth__Bypass` signs every visitor in as the same local account.

## Deploying the demo

The repository carries a `Dockerfile` and a `fly.toml`. The image builds the app in the
SDK image and ships only the runtime, runs as the non-root account the .NET image
provides, and serves HTTP on 8080 behind whatever terminates TLS.

```bash
docker build -t trust-finance .
docker run -p 8080:8080 -e Auth__Bypass=true -e Demo__Enabled=true trust-finance
```

On Fly.io, where `fly.toml` already carries the demo flags and forces HTTPS:

```bash
fly launch --no-deploy   # once, to pick the app name and region
fly deploy
```

The demo runs with no volume on purpose: the SQLite file lives on the container's own
filesystem, so a visitor who deletes everything gets a clean database back on the next
restart. For an install meant to keep its data, mount a volume at `/data` and leave
both demo flags off.

---

## Tech stack

- .NET 10 / ASP.NET Core, Blazor Server with interactive server rendering
- Entity Framework Core 10 + SQLite, migrations applied on startup
- Cookie authentication; passwords hashed with ASP.NET Core `PasswordHasher`
  (PBKDF2 with a per-user salt)
- Market data from [brapi.dev](https://brapi.dev), with an offline sample provider
  so the app works before any token is configured
- The Banco Central's open SGS series for the daily CDI, SELIC and IPCA — no token,
  no account, and a flat estimate clearly labelled as one when it cannot be reached
- `Blazor-ApexCharts` for the charts
- Hand-written CSS on a 4px scale, light/dark themes, responsive to phone width
- xUnit + FluentAssertions, running against in-memory SQLite; Playwright for the
  end-to-end journeys

---

## Repository layout

```
Trust-Finance.Domain/    Entities and pure business logic — no EF, no ASP.NET
  Entities/              User, Account, Category, CategoryRule, Transaction,
                         RecurringTransaction, Trade, Budget, alerts, settings
  Banking/               OFX statements, boleto typed lines, Pix BR Code
  Markets/               Business-day calendar, 252 rate conventions, yield curve
                         (flat forward), fixed-income pricing, IR/IOF, FGC
  Finance/               MonthlyFlow: the cash dashboard calculation;
                         Schedule: when a recurrence falls due;
                         CardStatements: statements and account balances
  Investing/             Portfolio: positions, average price, realized/unrealized;
                         Performance: XIRR and the CDI comparison
  Tax/                   CapitalGains: the monthly DARF computation
  Planning/              Budgets, goals and the notification rules
  Slug.cs                "Mercado & Padaria" -> "mercado-padaria"
Trust-Finance.Data/      EF Core: DbContext, entity mappings, migrations
Trust-Finance.App/       The Blazor Server app
  Components/Pages/      Dashboard, transactions, accounts, card statements,
                         import, recurrences, categories, portfolio, fixed
                         income, payouts, tax, market, goals, settings, login,
                         register
  Components/Shared/     Charts, stat tiles, notification tray, palette
  Components/Layout/     Signed-in shell and the bare auth layout
  Services/              UserAccount, Account, Category, CategoryRule,
                         Transaction, Recurrence, Import, Investment,
                         FixedIncome, Payout, CorporateAction, Watchlist, Alert,
                         Budget,
                         Notification, Settings, UiState
  Services/Market/       Provider abstraction, brapi.dev client, offline catalogue,
                         Banco Central series (CDI, SELIC, IPCA)
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
- **Accounts** — checking, savings, cash and credit cards. Every transaction
  belongs to one; a new user gets a checking account without being asked. The
  screen reports cash on hand, what the cards owe and the net of the two.
- **Credit-card statements** — a purchase lands on the statement closing on or
  after its date, so buying the day after closing falls on the month after;
  instalments are split across months with the odd cent on the first; paying the
  statement is a transfer from another account, and each statement reads open,
  paid or overdue.
![A credit card's statements: purchases grouped by closing date, instalments labelled,
and the transfer that paid one of them](docs/images/fatura.png)

- **Transfers** — two halves tied by one id, saved and deleted together. Neither
  half counts as income or expense anywhere: moving money between your own
  pockets is not earning or spending it.
- **OFX import** — a statement from a bank or a card, previewed line by line
  before anything is written. The bank's own id (FITID) travels with every
  imported row under a unique index, so importing the same file twice creates
  nothing the second time. A row typed by hand before the statement arrived is
  recognised by amount and date and merely reconciled, never duplicated. The
  categories you pick are remembered as rules, so the next statement from the
  same bank arrives mostly filed.
- **Boleto and Pix** — paste a boleto's typed line or a Pix copia-e-cola code on
  the transactions screen and the form fills itself: value, due date and who is
  being paid. Both are validated first — a boleto's field and general check
  digits (modulo 10 and 11) and a Pix payload's CRC-16 — so a mistyped code is
  refused rather than filed.
- **Transactions** — full CRUD, each with a direction (income or expense), an
  amount stored positive, a date, a category and an account. The list is filtered by period
  (this month, last month, three months, this year, everything, or a custom
  range) with income, expense and balance for whatever is showing.
![The ledger, filtered by period, with the income, expense and balance of what is
showing](docs/images/lancamentos.png)

- **Recurring transactions** — salary, rent, subscriptions: weekly, monthly or
  yearly, from a start date, with an optional end. Each occurrence is posted as
  an ordinary transaction the first time the app is opened on or after its date,
  so it can be adjusted or deleted on its own, and it stays on the ledger if the
  recurrence is later removed. A monthly recurrence on the 31st lands on the
  28th of February and back on the 31st of March — occurrences are computed from
  the start date, never from the previous one, so they cannot drift.
- **Portfolio** — buys and sells with fees, weighted average price, realized and
  unrealized results, allocation by asset class, day change and yield on cost.
- **Performance against the CDI** — a money-weighted annual return (XIRR over the
  dated cash flows) next to the CDI over the same days, the "% do CDI" a bank
  statement would print, and what the same deposits would be worth today had they
  sat at CDI. The daily CDI comes from the Banco Central's open-data series; when
  it cannot be reached the page says so and uses a flat estimate.
- **Fixed income** — CDB, LCI, LCA, Tesouro and the rest, valued the way a back office
  values them: **na curva** (principal compounded at the contracted rate, what the
  holder gets at maturity) next to **a mercado** (the remaining flow discounted at
  today's rate, what selling early would fetch). Post-fixed paper accrues against the
  real daily CDI or SELIC from the Banco Central, using CETIP's convention for a
  percentage of the CDI — each day is taken down to its percentage *before*
  compounding, not the accumulated factor afterwards. Every figure is also shown net
  of the regressive income tax, the IOF of the first 30 days and B3's custody fee, so
  a 12% CDB reads as the ~9,6% it actually pays. **FGC coverage** is computed per
  issuer against the R$ 250.000 limit and the R$ 1.000.000 four-year ceiling.
- **Payouts** — dividends, JCP (with the 15% withheld at source) and FII income,
  recorded gross and net, feeding yield on cost per position and the annual
  withholding total.
- **Corporate actions** — splits, reverse splits and bonus shares at the cost the
  issuer declared, applied to quantity and average price on their date so a
  1:10 split does not read as a 90% loss.
- **Monthly capital-gains tax** — the DARF each month owes on B3 sales: day trade
  netted per ticker per day at 20%, stock sales under R$ 20.000 exempt (the
  ceiling is on sales, not gains; ETFs and BDRs neither count nor qualify), FIIs
  at 20% with no exemption, crypto exempt under R$ 35.000, losses carried forward
  inside their own bucket, the broker's 0,005% and 1% withholding credited, DARFs
  under R$ 10 accumulating, and the due date on the last business day of the
  following month.
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

## Running it for real

Prerequisite: .NET SDK 10.x. That is the whole list — no Node, no Docker, no
database server.

```bash
dotnet run --project Trust-Finance.App
```

Open `http://localhost:5062`, create an account, and you are in — no demo flags, so
the sign-in screen appears and the database starts empty. The schema is applied on
every start, so a fresh install gets its tables and an upgrade gets its new columns
without anyone running a migration by hand.

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
- A recurrence posts every occurrence due up to today, once, and stops at its
  end date; correcting it never re-posts what was already posted; deleting it
  keeps the rows it posted
- The period filter keeps both ends of the range and never shows another
  user's rows
- A split multiplies quantity and divides the average price, a reverse split
  drops the fraction, a bonus adds shares at the declared cost; an event on the
  same day as a trade applies first, and one before the first trade is ignored
- Yield on cost is per share over average price, so selling half the position
  after a payout does not double it
- The tax computation is checked against worked examples: the R$ 20.000 ceiling
  is on sales, ETFs do not qualify, a loss in an exempt month still carries, a
  loss in FIIs does not shelter stocks, a day with both sides splits at the
  matched quantity, a DARF of R$ 6 waits for the next R$ 6, withholding carries
  within the year, a split on a quiet day still reaches the next sale
- XIRR recovers a known rate, compounding at CDI skips the deposit day, the
  annualization is on 252 business days
- Importing the same statement twice creates its lines once; an overlapping file
  adds only what is new; a hand-typed row is reconciled rather than duplicated,
  and cannot be claimed by two lines; the same bank id on another account is
  still new; one uncategorized line stops the whole import
- A boleto with a mistyped field is refused, and so is one whose fields check but
  whose general digit does not; the due-date factor picks the cycle nearest to
  today, since it rolled over in February 2025
- A Pix code with one changed digit fails its CRC; the CRC itself matches the
  reference vector for "123456789"
- OFX parses both the SGML flavour banks export and the XML one, with the time
  zone suffix dropped and a comma decimal accepted
- The business-day calendar matches the published Easter dates and the national
  holidays that follow from them; 2025 lands on exactly 252 business days and
  2026 on 249, which is why the convention is a definition and not a count
- A rate over 252 days returns the annual rate exactly, and half a year is the
  square root rather than half the rate
- The percentage-of-CDI convention is checked against the shortcut it is often
  confused with: compounding day by day is convex, so scaling the accumulated
  factor understates paper above 100% of the CDI and overstates paper below it
- Flat-forward interpolation is verified by its defining property — the forward
  rate between any two interior points of a gap is constant — and the curve is
  flat outside the quoted vertices rather than extrapolated
- A prefixado is worth less than its curve when rates rose and more when they
  fell; discounting at the contracted rate returns the curve exactly; post-fixed
  paper is not marked at all, and an IPCA+ paper is left on its curve rather
  than discounted at a nominal rate
- The regressive table steps on its published boundaries, IOF reaches zero on
  the thirtieth day and shrinks the base income tax sees, an LCI at 10% beats a
  CDB at 12% after tax, and Tesouro Selic pays no custody on its first R$ 10.000
- Splitting R$ 500.000 across two banks is fully covered by the FGC and leaving
  it at one is not; treasury paper is left out rather than counted as uncovered

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

- The real (IPCA) curve, so inflation-linked paper can be marked to market too
- The ANBIMA reference rates, in place of a flat curve at the CDI

- Paging on the transactions list, for a "Tudo" view that spans years
- Portfolio value over time from price history, for a time-weighted return
  next to the money-weighted one
- National holidays in the DARF due date
- Editing an account: display name, email, password
- Exporting to CSV, and a backup of the database file that does not involve
  finding it on disk

**Later**

- CSV import and broker note (nota de corretagem) parsing
- Open Finance Brasil: ingesting accounts and cards in the API's own shape
- The annual return: "Bens e direitos" at cost, exempt income, income taxed
  at source, straight from the ledger
- A public deployment over HTTPS, for the people who would rather not run it
  themselves
