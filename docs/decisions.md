# Technical decisions

What was decided, why, and what would change if this stopped being a single-user
install and became a product. Each entry states the decision, the reasoning and the
trigger that would reopen it. Nothing under "as a product" is built or planned; it is
the answer to "what would you do", written down so it does not have to be reinvented
in an interview or a planning meeting.

---

## 1. One process, one file

**Decided.** The app is a single .NET process against a single SQLite file. No API
tier, no database server, no frontend build.

**Why.** The target is a handful of real people running it themselves. For that,
every extra moving part is a reason the install fails: a database server to provision,
a second process to keep alive, a build step to have the right Node for. `dotnet run`
and it is up is the whole pitch. SQLite in WAL mode handles the write rate of a
personal ledger with room to spare, and EF Core keeps the schema portable to a
server database later (see §3).

**Reopens when** more than one machine has to serve the same data. SQLite is a file
on one disk with one writer; the moment a second instance exists, it stops being an
option.

---

## 2. A modular monolith, not microservices

**Decided.** One deployable. The domain is split by folder — `Banking`, `Markets`,
`Finance`, `Investing`, `Tax`, `Planning` — with a one-way dependency
`App → Data → Domain`, and the domain project references nothing.

**Why microservices add nothing here.** The value of a service boundary is letting a
part be deployed, scaled or owned separately. None of those pressures exist: one
team, one release, one database, and a load that a shared-CPU VM serves comfortably.
What they would cost, on the other hand, is concrete:

- **The transactional core does not split.** A transfer is two rows saved together.
  The monthly tax reads trades, payouts and corporate actions in one query. The audit
  trail is written by the `DbContext` on the same save as the row it describes, which
  is what makes it complete — a write nobody instrumented is still on the trail. Put
  those behind network calls and every one of them needs a saga or an outbox to keep
  the guarantee it gets for free from a local transaction.
- **Per-user isolation is a `WHERE UserId = @user` on every query.** With one
  database that is testable and provably enforced; across services it becomes a
  contract each one has to honour on its own.
- **Optimistic concurrency rides the row version in the `WHERE` clause.** That is a
  database feature. It does not exist between services.

**What the modular split already buys.** The seams that would matter are already
seams. `Domain` has no EF and no ASP.NET, so the tax computation or the fixed-income
pricer could be lifted into a separate process — a Lambda, a worker — by referencing
the project, without rewriting it. `IMarketDataProvider` is the one interface that
talks to the outside world, and it is already behind a factory and a cache. A service
extracted later starts from a folder that was designed to leave.

**The two candidates, if one ever leaves.** Both are things that are *not* per-user
and *not* transactional:

1. **Market-data fetching.** Quotes are the same for every user, but the cache is per
   user and the token is per user. As a product, one fetcher pulls each ticker once
   on a schedule and everyone reads the result — that respects the provider's rate
   limit once instead of once per user, and it is a scheduled job with no state of
   its own. First to leave.
2. **Scheduled work.** Recurrences are posted when a page is opened
   (`RecurrenceService.PostDueAsync`) and price alerts are evaluated when the tray is
   opened. That is correct for one person who opens the app; it is wrong for a product
   where a user expects the salary to be on the ledger and the alert in their inbox
   whether or not they logged in. This is a worker on a clock, not a service with an
   API.

Neither is a microservice in the architectural sense. They are a scheduled job and a
worker, and they would still reference `Domain` and write to the same database.

**Reopens when** a second team owns part of the system, or one part's load profile is
clearly different from the rest (an Open Finance ingestion pipeline receiving
webhooks from every bank would be the first such case).

---

## 3. What actually changes to run as a product

Everything below is what breaks at *two instances*, not at a million users. Listed
in the order it would have to be done, with the AWS service that fits and why. None
of it is adopted today, because all of it is billed and the current target is not.

| What breaks | Today | As a product | Why that service |
|---|---|---|---|
| **Database** | SQLite file, one writer | **RDS for PostgreSQL** (or Aurora Serverless v2 if load is spiky) | EF Core provider swap; migrations regenerated once. Managed backups, point-in-time recovery, and the `decimal(18,2)` and unique indexes carry over unchanged. Postgres row-level security can enforce per-user isolation as a second line beneath the queries. |
| **Data-protection key ring** | `keys/` folder beside the database | **SSM Parameter Store** via `Amazon.AspNetCore.DataProtection.SSM`, or `PersistKeysToDbContext` in the same Postgres | Two instances with two key rings cannot read each other's cookies or antiforgery tokens: every load-balanced request would sign the user out. Must be shared before the second instance exists. |
| **In-memory state** | `IMemoryCache`: sign-in throttle, quotes, CDI/SELIC/IPCA series | **ElastiCache (Valkey/Redis)** through `IDistributedCache` | The throttle counted per instance lets an attacker multiply the twenty tries by the instance count. The quote cache per instance multiplies calls to the provider. Both are keyed strings with a TTL, which is exactly what a distributed cache is. |
| **Compute** | Fly machine, `Dockerfile` | **ECS on Fargate behind an ALB**, `sa-east-1` | The image already exists. Blazor Server holds a WebSocket per visitor, so the web tier needs long-lived connections and **sticky sessions at the load balancer** — which rules out Lambda for the web tier and makes App Runner conditional on its WebSocket support. `/health` becomes the target-group check. |
| **Scheduled work** | On page open | **EventBridge Scheduler → ECS scheduled task** (or Lambda referencing `Domain`) | Post recurrences at midnight, evaluate alerts on a quote refresh, run the monthly DARF on the first business day. A clock invoking the existing services; no new logic. |
| **Market data** | Per-user token, per-user cache | One **scheduled fetcher** writing quotes to a `Quotes` table or the cache; a product-level provider contract instead of a user token | See §2. It also removes the case where a user pastes an invalid token and gets sample prices without noticing. |
| **Secrets** | Provider token in the user's settings row, plain | **Secrets Manager** for the app's own credentials; **KMS** envelope encryption for anything per-user that is a credential | A database dump today contains every user's brapi token in clear. |
| **Backups** | Copy the file | RDS automated snapshots + PITR | The roadmap's "backup that does not involve finding the file on disk" becomes a platform feature. |
| **Email** | None | **SES** | A product needs password reset and email verification, neither of which exists because a single-user install has no one to email. This is a feature gap before it is an infrastructure one. |
| **Edge** | Fly terminates TLS | **CloudFront** in front of the ALB; **WAF** with a rate-based rule on `/login` | Static assets from the edge; the WAF rule complements the in-app throttle rather than replacing it — the app's rule is per account and per address, the WAF's is per address at the edge before the request costs a database read. |
| **Observability** | Console logs | **CloudWatch Logs** + **OpenTelemetry** to X-Ray | Structured logs are already there through `ILogger`; the exporter is the change. Alarms on `/health`, on ALB 5xx and on the provider's error rate. |
| **Data residency / LGPD** | The user's own disk | Everything in `sa-east-1`, encryption at rest through KMS on RDS and S3, an export and a delete of a user's data as features | Personal financial data of Brazilian residents. The audit trail already answers "what changed and when"; export and erasure are the two things the law adds that are not built. |

**Order of magnitude.** The minimum viable set — one Fargate task, `db.t4g.micro`
Postgres, an ALB — sits in the tens of dollars a month, and the ALB alone is a fixed
share of that regardless of traffic. That is the reason none of it is on: it is the
price of a product, paid before there is one.

**The cheap intermediate step.** Before any of the above, a single Fly machine with a
volume and **Litestream replicating the SQLite file to S3** gives continuous backup
for cents a month, and is how a v1.0 for a handful of people would actually run. It
changes nothing in the code.

---

## 4. Considered and not adopted

- **Cognito** for authentication. It would bring MFA and password reset ready-made,
  but the sign-in throttle, the per-account lockout and the current-password check on
  profile changes are all in the app, tested, and part of what the audit trail sees.
  Moving the identity out means moving those out, and Cognito's hosted UI is not the
  app's UI. Reconsider if social login or SAML becomes a requirement; until then, add
  TOTP in-app.
- **DynamoDB.** The data is relational and the queries are joins — tax over trades,
  payouts and corporate actions; statements over transactions and accounts. A
  document store would push those joins into code that today is one LINQ query.
- **Lambda for the web tier.** Blazor Server is a persistent WebSocket circuit; a
  function that ends when the response is sent cannot host one. Lambda fits the
  scheduled and event-driven work in §3, referencing `Domain` as a library.
- **SQS between the app and the workers.** There is nothing to queue yet: scheduled
  work runs on a clock against the database. A queue appears with Open Finance —
  webhooks from banks arriving at a rate the app does not control — and not before.
- **Kubernetes.** One image, one service, no sidecars. ECS on Fargate carries the
  same container with none of the control plane to run.
- **Microservices.** §2.

---

## 5. What would be built first, in order

If the question is "what is the first month of making this a product":

1. Postgres behind EF Core, with the migrations regenerated. Everything else depends
   on a database that more than one process can open.
2. Shared data-protection keys and a distributed cache, in that order — the first
   because two instances without it sign everyone out, the second because the throttle
   is a security control and it stops working at two.
3. Email, and with it password reset and verification. A feature, but nothing
   customer-facing ships without it.
4. The scheduled worker for recurrences and alerts, and the single market-data
   fetcher. This is where the product stops behaving like an app someone has to open.
5. Fargate, ALB with stickiness, CloudFront, WAF, CloudWatch. Infrastructure last,
   because until 1–4 exist there is nothing that needs more than one machine.
