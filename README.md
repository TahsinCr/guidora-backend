# Guidora Backend

Guidora Backend is a .NET platform designed to run on Docker and evolve into independently deployable microservices.

## Development principles

- Keep project-local planning notes private; they are intentionally excluded
  from the repository and do not replace this committed architecture guidance.
- Services must be able to run as independent Docker containers.
- Each service must own its domain model, `DbContext`, and migrations.
- Services must not access each other's tables directly.
- RabbitMQ is optional in V1; introduce broker-backed events only when an
  asynchronous integration/use-case is added.
- Redis is used for caching and temporary distributed state, not as the source of truth.
- Validation aspects resolve closed `IValidator<T>` services from the active
  request scope, so validators may use DI dependencies and generic base classes.
- Aspect logs redact sensitive top-level and nested DTO fields (including token,
  password, credential, cookie, and signing-key names); unserializable values
  are omitted rather than logged raw.
- Secrets must never be committed to the repository.
- Ongoing work must be recorded in the `Unreleased` section of `CHANGELOG.md`.
- A versioned changelog section must not be created before an official release.

## Development environment

Requirements:

- .NET 10 SDK
- Docker Engine
- Docker Compose

The required SDK feature band is pinned in `global.json` (currently 10.0.401).
Run `dotnet restore NetCoreBackend.sln` after opening the solution so Rider can
load NuGet assets and framework references correctly.

Start the local infrastructure:

```bash
cp .env.example .env
# Replace the default passwords in .env
docker compose up -d
docker compose ps
```

The default Compose stack builds and runs the backend with PostgreSQL and Redis.
RabbitMQ is optional and is not a dependency of the synchronous V1 API; start it
only when explicitly needed with `docker compose --profile messaging up -d`.
Copy `.env.example` to `.env` and replace every placeholder secret before
starting the stack. The backend is exposed on `localhost:8080`; `/health/live`
checks process availability and `/health/ready` checks PostgreSQL and Redis.

Compose services:

| Service | Address |
|---|---|
| PostgreSQL | `localhost:5432` |
| Redis | `localhost:6379` |
| RabbitMQ AMQP (optional `messaging` profile) | `localhost:5672` |
| RabbitMQ management UI (optional) | `http://localhost:15672` |

Infrastructure ports bind to loopback by default. Set `INFRA_BIND_ADDRESS` to
another address only when remote host access is intentionally required; for a
production deployment, prefer keeping PostgreSQL, Redis, and RabbitMQ private
to the Docker network.

See [infra/README.md](infra/README.md) for infrastructure details.

The Compose configuration targets PostgreSQL 18 and mounts its named volume at
`/var/lib/postgresql`, as required by the PostgreSQL 18 image layout. Do not
reuse a volume created with an older major PostgreSQL image without a planned
`pg_upgrade` migration.

## Database schema lifecycle

The application uses EF Core migrations for schema changes. `EnsureCreated` is
not used because it bypasses migration history. Startup migration is available
as an explicit switch:

```json
{
  "Database": {
    "ApplyMigrationsOnStartup": false
  }
}
```

Keep the switch disabled by default in production. For local development it may
be enabled after reviewed migrations exist. Production deployments should apply
migrations through a controlled migration bundle or reviewed SQL deployment.

The backend reads PostgreSQL from `ConnectionStrings:Guidora`, overridden in
Compose with the `postgres` service hostname. Redis is a V1 API dependency;
RabbitMQ is an optional infrastructure profile, not required by the API.
Configure browser origins through `Cors:AllowedOrigins` (Compose:
`WEB_CLIENT_ORIGIN`). Behind a TLS-terminating proxy, set
`ForwardedHeaders:KnownProxies` to the proxy IP addresses; arbitrary forwarded
headers must not be trusted. Login, registration, and refresh endpoints have
configurable IP-partitioned limits under `RateLimiting:Login`, `Register`, and
`Refresh`. Refresh cookies are Secure outside Development. HTTP request logs
record method, path, response status, duration, and trace ID only. Query strings,
headers, and request/response bodies are excluded so secret query parameters and
payloads are not recorded.

## Redis cache and RabbitMQ

Redis is the shared cache provider for public catalog reads. Cache policy is
declared on each use-case with `CacheAspect(profile, durationMinutes)`. An
omitted/non-positive duration means no time-based expiration; Redis may still
evict the entry under memory pressure. Product and category writes invalidate
their related profiles only after a successful business result and, when used,
after the database transaction commits. Cache values are JSON envelopes with
their concrete result type; PostgreSQL remains the source of truth.
Authentication session validation uses a separate, one-minute distributed
Redis entry.

Use cases that know the affected Product return an internal cache-invalidation
directive so detail entries are targeted by Product ID while affected catalog
listings are invalidated by profile. The directive is not serialized in the HTTP
result. Redis pattern invalidation deletes matching keys in bounded batches.

Aspect priorities define wrapper nesting, not the timestamp of each aspect's
callback. Authorization and validation guard the use case; `CacheRemoveAspect`
wraps the transaction so its success hook executes after the inner transaction
commits. Failed business results and rollbacks do not invalidate caches.
`PerformanceAspect` wraps cache and transaction interceptors, so its elapsed
time includes cache hits, database commit, and post-commit invalidation; its
completion hook therefore runs after those inner callbacks. Threshold breaches
are emitted as structured `ILogger` warnings, including slow synchronous
exceptions, rather than being limited to Debug-only output. The paged Product
catalog, Product detail, and Category navigation reads currently use a
one-second threshold; measurements include both cache-hit and cache-miss paths.
Business Manager attributes are declared in the selector's outer-to-inner
priority order, and a contract test checks every decorated public Manager
method against the production selector. Thus cache removal is declared before
the transaction wrapper even though its invalidation callback runs after the
transaction commits. Operation logging runs from the post-operation hook after
the wrapped pipeline finishes.

Redis is configured as disposable cache infrastructure: AOF and RDB persistence
are disabled, and the global eviction policy is `allkeys-lfu`. Compose defaults
to `REDIS_MAXMEMORY=256mb` and `REDIS_CONTAINER_MEMORY_LIMIT=320m`; override
these deployment settings through `.env` after measuring the target host.

Fresh V1 databases seed only `admin` and `community` verification types; Bot
Verification remains out of scope until its workflow exists. The cleanup
migration preserves a legacy `bot` lookup if historical verification rows still
reference it.

Secured-operation claims are discovered from Business aspects. Startup
synchronization is additive and idempotent: it creates missing claims but never
prunes existing catalog entries. Enable
`Authorization:SyncOperationClaimsOnStartup` only after the PostgreSQL schema is
available (Compose setting: `AUTHORIZATION_SYNC_OPERATION_CLAIMS_ON_STARTUP`);
role assignments remain explicit seed/admin-managed data.

The `popular-products` five-minute profile is reserved for the homepage once a
view-event and aggregation model is approved; it is intentionally not populated
from an invented metric. Per-user state, authorization, favorites, reports,
and mutable moderation data are not catalog-cached.

User ban, restore, and explicit authorization invalidation require the seeded
`User.Manage` OperationClaim, assigned only to the Admin role. Invalidation
increments the user's authorization version, revokes active refresh sessions,
commits those database changes together, then evicts their token-validation
cache entries only after the outer transaction commits (or immediately after
the local commit when no ambient transaction exists). Generic repository
physical deletion is deliberately excluded;
only explicitly opted-in DAL contracts expose `Purge`, not the normal domain
delete workflow.
Synchronous and asynchronous repository list reads are no-tracking by default;
single-entity lookup remains tracked for update workflows.

Audit reversion is available to Admins through
`POST /api/audit/{auditLogId}/revert`, gated by the `Audit.Revert` claim.
V1 reverses Product create/update/delete and accepted-edit-proposal snapshots,
and Category create/update/deactivation snapshots. Each reversal verifies that
the live fields still match the audited new state, appends a linked immutable
audit entry, and uses PostgreSQL `xmin` optimistic concurrency so a concurrent
write returns HTTP 409 rather than being overwritten. Unsupported audit entity
types/actions and repeated reversals are rejected; audit history is never
rewritten or deleted.

RabbitMQ is configured as a durable topic exchange named `guidora.events`.
`IEventPublisher` publishes JSON events with persistent delivery mode. No
consumer, worker, scheduler, or synchronous Product workflow is coupled to the
broker in V1; commands must first persist their canonical transaction, then a
future outbox-backed integration can publish an event safely.

## Catalog governance in V1

Product create/update requests carry Category and Tag IDs. Business rules check
that Categories and Tags are active and that each TagCategory's single/multiple
selection and exclusive-tag constraints are respected. Unverified Products can
be updated directly; edits to actively verified Products are stored as separate
pending proposals containing the scalar, Category, and Tag snapshot. Proposal
acceptance revalidates the snapshot before atomically applying it; rejection
leaves the canonical Product unchanged. Document and media edits are not part of
the V1 proposal snapshot.

Product names are unique case-insensitively and slugs are unique in PostgreSQL.
Website URLs are normalized before persistence; exact normalized URL matches
are rejected by the Business identity rule and a PostgreSQL unique index also
protects against concurrent duplicate submissions. Whether different URLs on
the same canonical domain should be treated as duplicates remains undecided.
Public catalog reads expose only `published` Products. Product listing
endpoints are database-paginated (`page`, `pageSize`, capped at 100) and
category filtering is performed in the database rather than materializing
category Product IDs in the API process.
Only active Categories are public: listing Products by an inactive or unknown
Category returns 404. Deactivating a Category invalidates both navigation and
Product-listing caches after the database transaction commits.
`GET /api/categories` exposes active Categories only. Admins and any role
assigned the `Category.Manage` claim can inspect the complete management
catalog, including inactive rows, through `GET /api/categories/management`.
This claim is seeded to Admin by default.

The public tag catalog is exposed by `GET /api/tag-categories` and
`GET /api/tags?tagCategoryId={id}`. Both return active entries only. Moderator
and Admin management operations create, update, and deactivate tags and tag
categories. Deactivating a TagCategory also deactivates its Tags so they cannot
remain selectable through the public catalog.

User-submitted Product text, comments, document/changelog content, media alt
text, and contributor labels pass through a local deterministic moderation
matcher. Its English and Turkish lexicons are embedded in the Business assembly
and attributed in `Business/Resources/Moderation/ATTRIBUTION.md`; the matcher is
built once during Autofac container startup. The matcher
normalizes common obfuscations and uses token boundaries; it is a heuristic
baseline, not a complete language policy or an automated truth/quality check.
Report descriptions are intentionally not filtered so users can quote content
they are reporting. Updating the word lists requires preserving upstream
attribution and reviewing false-positive/false-negative behavior.

Moderators and Admins can page through the oldest pending proposal and open
report queues (`GET /api/product-edit-proposals/pending` and
`GET /api/product-reports/open`; `pageSize` is capped at 100). They can accept
or reject proposals, resolve or dismiss reports, and grant or revoke Admin
verification through the corresponding governance controllers. These actions
use OperationClaims (`Product.Proposal.Review`, `ProductReport.Review`, and
`Product.Verify`), not frontend role checks. Product reports are submitted by
an authenticated user. Audit entries for Product, Category, proposal,
verification, and report changes are append-only JSONB old/new snapshots.

Admin and Community verification are separate verification types. Admin
verification commands are implemented in V1; Community verification remains a
feature without a voting threshold/eligibility algorithm, so no vote workflow is
invented or exposed yet. Bot verification remains future scope.

## Community and Product content

Authenticated users can page through their saved Products with
`GET /api/favorites/mine?page=1&pageSize=50`; they can use
`POST /api/favorites/products/{productId}` and
`DELETE /api/favorites/products/{productId}` to add or remove one.
Product comments can be listed with
`GET /api/products/{productId}/comments?page=1&pageSize=50` and created at
`/api/products/{productId}/comments`; authors delete their comments by changing
them to the domain's `deleted` status, while moderators can hide or restore
eligible comments or block them at `POST /api/comments/{id}/block`. Public
comment reads include only active comments on
published Products whose authors remain active; closing a User account does not
rewrite that User's comment rows. Comment replies are not part of V1.

Product Documents, Media metadata, Changelogs, and Contributors use separate
Product-scoped resources under `/api/products/{productId}/documents`,
`/media`, `/changelogs`, and `/contributors`. Documents store Markdown/text
content in PostgreSQL. Media currently stores validated HTTP(S) references and
metadata only: binary upload and its physical storage provider remain an open
infrastructure decision. Changelog entries are version-unique and append-only.
Contributor records credit a User once per Product; users may add/remove their
own credit, while the Product creator can manage other credits. Direct Document
and Media mutations are rejected for actively verified Products because those
resource kinds do not yet participate in edit proposals. All write workflows
use operation claims, validation, transaction boundaries, and audit records.

The EF design-time factory reads `ConnectionStrings__Guidora` when creating
migrations. Add each schema change as a reviewed migration, for example:

```bash
ConnectionStrings__Guidora='Host=localhost;Port=5432;Database=guidora;Username=guidora;Password=your-password' \
  dotnet ef migrations add AddReviewedSchemaChange \
  --project DataAccess --startup-project WebAPI \
  --output-dir Migrations
```

Before applying `ProductIdentityConstraints` to a database that already has
Product rows, check for names that would collide under its new case-insensitive
unique index:

```sql
SELECT lower(btrim("Name")) AS normalized_name, count(*)
FROM "Products"
GROUP BY lower(btrim("Name"))
HAVING count(*) > 1;
```

Resolve any returned rows deliberately before applying the migration; no Product
data is automatically removed or renamed.

## Authentication sessions

Guidora uses a short-lived JWT access token together with a rotating refresh
session. Refresh tokens are random secrets, stored only as hashes in
PostgreSQL, and are delivered to the browser in an HttpOnly cookie. The
PostgreSQL session record is the security source of truth; Redis caches a
successful access-token session validation for one minute and is explicitly
invalidated on logout, ban, or authorization invalidation.

The default token settings are deliberately persistent without being
indefinite: access token 15 minutes, refresh session 30 days, remembered
session 90 days, and an absolute rolling-session limit of 180 days. A ban,
direct user-claim change, or explicit authorization invalidation revokes all
of the user's sessions and increments its authorization version. The Admin-only
role API assigns/removes roles and grants/revokes OperationClaims; affected
users' authorization versions and sessions are invalidated in the same
transaction. The last active Admin assignment and the Admin role's
`Role.Manage` permission are protected from removal.

Authentication, user-security, Product, and Category request paths use
cancellation-aware asynchronous database operations. Refresh rotation consumes
the prior session with one conditional PostgreSQL update, so concurrent reuse
cannot create multiple replacement sessions; replay invalidation happens only
after the failed rotation transaction has rolled back. Logout uses an atomic,
idempotent session revocation as well.

## Project structure

The current solution layers will evolve toward independently deployable services:

```text
Core/        Shared technical infrastructure and abstractions
Entities/    Current domain models and DTOs
DataAccess/  Current EF Core data access
Business/    Current business rules and services
WebAPI/      Current HTTP entry point
infra/       Local Docker infrastructure
```

The first architectural split will separate the Identity and Catalog bounded contexts. Every new service must have its own application, data access, migrations, and test boundaries.

Tests remain one project and follow the production layer boundaries:

```text
Tests/Tests.csproj
├── Core/       shared test doubles plus Core tests
├── Entities/
├── DataAccess/
├── Business/
├── WebAPI/
└── Integration/  cross-layer tests against configured infrastructure
```

PostgreSQL, Redis, and RabbitMQ integration checks use explicit
`GUIDORA_TEST_*` environment variables and are skipped when those are not set.
They are not separate projects or solutions.

Repository mutations only stage EF Core changes. A scoped `IUnitOfWork` owns
the commit boundary, while `TransactionScopeAspect` supports synchronous,
`Task`, and `Task<T>` commands. Audit persistence is append-only and is not a
generic CRUD repository.

## Verification

```bash
dotnet restore
dotnet build NetCoreBackend.sln --configuration Release
dotnet test Tests/Tests.csproj --configuration Release
```

Relevant build and test commands must be run after code changes, and their results should be recorded in the commit or development notes.

## Change workflow

1. Make the change as a small, clearly scoped step.
2. Update the `Unreleased` section in `CHANGELOG.md`.
3. Update this README when the architecture or development workflow changes materially.
4. Run the relevant verification commands.
5. Create a meaningful commit.
