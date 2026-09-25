# Changelog

All notable changes to the Guidora backend are documented here.

## [Unreleased]

### Fixed

- Preserved each refresh session's fixed absolute expiry across token rotations,
  and recorded refresh-token replay once so a previously rotated token cannot
  repeatedly revoke sessions created after its first replay.
- Marked replay atomically in both revoked-token and concurrent-rotation paths;
  repeated use of the same old token cannot repeatedly invalidate new sessions.
- Rejected Product edit proposals based on an older PostgreSQL `xmin` revision;
  Product and Category update, delete, and deactivation concurrency conflicts
  now return HTTP 409.
- Returned HTTP 409 for concurrent Community verification revocation.
- Serialized last-active-Admin removal and ban checks with a transaction-scoped
  PostgreSQL advisory lock.
- Excluded `CancellationToken` arguments from cache keys and changed Product name
  duplicate checks to compare the indexed `citext` column directly.
- Prevented a user from creating another open report for the same Product while
  their previous report is awaiting moderation; a partial unique index protects
  this rule from concurrent submissions, and the migration preserves old rows
  while closing duplicate open reports.
- Replaced TagCategory assignment materialization with a database-side grouped
  existence query, and deactivated category Tags with one bulk update.
- Replaced broad catalog-listing Redis key scans with profile-generation bumps;
  targeted Product detail invalidation remains scoped to the Product ID. Changed
  Redis eviction to `volatile-lfu` so non-expiring generation counters survive.
- Switched async catalog reads and post-commit cache invalidation to asynchronous
  Redis calls, including targeted key scans.
- Compacted large Document and Changelog audit content to hashes, sizes, and a
  single-range change summary capped at 8 KiB.
- Added the V1 Community Verification vote workflow: active User-role members
  other than the Product creator may vote once per Product; five affirmative
  votes verify it, and Moderators/Admins may revoke with a note.
- Settled Product website identity: exact normalized URLs are unique, while
  different URLs on the same vendor domain are allowed.
- Backfilled existing session absolute expiry from the first linked rotation;
  legacy pending proposals require resubmission because their original Product
  revision cannot be reconstructed safely.

- Mapped derived FluentValidation exceptions to HTTP 400 as well as the base
  exception type, preventing specialized validation failures from becoming 500s.
- Removed the unused synchronous OperationClaim service surface; permission
  synchronization remains owned by the dedicated startup security tool.
- Made the trusted forwarded-header proxy configurable in Docker Compose without
  trusting arbitrary forwarded headers by default.

### Added

- Corrected `PerformanceAspect` nesting so elapsed time includes cache hits,
  transaction commit, and post-commit invalidation; threshold breaches now use
  structured logging, and synchronous exceptions are measured before rethrow.
  Enabled one-second instrumentation on the main catalog read paths and added
  timing and interceptor-order contract tests.
- Added production HTTP request logging for method, path, response status,
  duration, and trace ID only; a TestServer test verifies query strings and
  message bodies are excluded.
- Added middleware regression coverage ensuring missing authentication maps to
  HTTP 401 and denied permissions map to HTTP 403.
- Added a PostgreSQL integration test for the full seeded community-user to
  `Product.Update` effective-permission path.
- Added controller contract tests that pin login, registration, and refresh to
  their intended rate-limit policies.
- Preserved full IPv6 client addresses in authentication rate-limit partitions
  while normalizing only IPv4-mapped IPv6 addresses.
- Applied accepted Product edit snapshots using relationship deltas, retaining
  unchanged category and tag links instead of deleting and reinserting them.
- Mapped duplicate catalog identities and stale governance state transitions to
  conflict results so the API returns HTTP 409 rather than generic HTTP 400.
- Added regression coverage for duplicate Tag and TagCategory slug conflicts.
- Mapped Business ownership/authorization denials to HTTP 403 and invalid
  resource-state operations to HTTP 409.
- Preserved HTTP 404 semantics when account-management commands target a missing
  user, without invalidating sessions for nonexistent accounts.
- Added explicit selector-order regression tests for Product create, update,
  and status-based delete so authorization, validation, cache invalidation, and
  transaction nesting cannot silently drift.
- Added regression coverage confirming public Category list/detail reads omit
  inactive categories.
- Product listing by an inactive or unknown Category now returns 404 before
  querying Products; Category deactivation also invalidates catalog-listing
  caches after commit.
- Added a `Category.Manage`-secured management catalog endpoint so authorized
  operators can inspect inactive Categories without exposing them publicly.
- Added a PostgreSQL unique index for normalized Product website URLs so
  concurrent requests cannot bypass the exact-URL duplicate rule.
- Product updates now return a dedicated localized success message instead of
  leaking the empty success result from the internal category-replacement step.
- Tag management now rejects making a Tag exclusive only when an existing
  Product selection would become invalid; safe exclusive changes remain allowed.
- Cache invalidation for Product edits now runs only when the canonical Product
  changes; submitting a verified Product proposal leaves catalog caches intact.
  Result directives are consumed by the post-commit CacheRemoveAspect, so the
  targeted invalidation is actually executed.
- Hardened `LogAspect`/`ExceptionLogAspect` parameter redaction recursively for
  nested DTOs, collections, credentials, cookies, keys, and tokens. Logging now
  omits values it cannot safely serialize; added recursive redaction tests.
- Updated `ValidationAspect` to resolve typed FluentValidation validators from
  the current request scope, supporting validator dependencies and generic base
  classes; added scoped-DI and validation-failure tests.
- Removed the V1-out-of-scope Bot Verification type from the active lookup seed;
  added a reversible, reference-preserving cleanup migration and bounded Redis
  pattern invalidation to avoid materializing unbounded matching keys in memory.
- Added result-driven post-commit cache invalidation for proposal acceptance and
  audit reversion, targeting the affected Product detail rather than evicting
  every detail entry in the cache profile.

- Completed the public active Tag/TagCategory catalog and moderator/admin
  management endpoints, with validation, audit, transactional writes, seeded
  permissions, and cascading deactivation of selectable tags.
- Added authentication request validation, configurable IP rate limits,
  remember-me session selection, Secure production refresh cookies, and legacy
  password-hash upgrade after successful login. Added PostgreSQL xmin
  optimistic concurrency for refresh-token rotation and granted the User role
  the Product.Update permission required by the community edit workflow.
- Added database identity checks for normalized Product website URLs, targeted
  per-Product cache variation/invalidation, cache-envelope recovery as a cache
  miss, and removed detail-cache invalidation from writes that do not affect
  the Product entity returned by the detail endpoint.
- Added PostgreSQL/Redis readiness checks, a process liveness endpoint,
  configurable CORS and trusted forwarded-proxy handling. RabbitMQ is now an
  optional Compose `messaging` profile and is not a V1 backend startup dependency.
- Added Admin-only role and permission management endpoints, bulk authorization
  session invalidation, last-Admin safeguards, explicit Comment blocking, and
  safeguards against moving Product-used Tags or invalidating existing multi-tag
  selections through TagCategory edits.
- Added xmin concurrency tokens for report/proposal/verification reviews and a
  filtered unique revert-provenance index. Favorite insertion is now idempotent
  under concurrent requests, and shared BusinessRules runners accept explicit
  success messages.
- Converted per-request access-token session validation to async PostgreSQL and
  Redis operations. PostgreSQL unique-constraint races now map to a sanitized
  HTTP 409 response instead of leaking as an unhandled 500.
- Converted Product, Category, authentication, user-security, and session
  invalidation request paths to cancellation-aware async database operations.
  Refresh rotation now atomically consumes a refresh session with a conditional
  PostgreSQL update, invalidates replayed sessions outside the failed rotation,
  and evicts the old validation-cache key only after a successful commit.
  Logout revocation is also an atomic, idempotent update. Added AuthManager unit
  coverage and live PostgreSQL concurrent-rotation/logout tests.

- Restricted user ban, restore, and authorization invalidation to the
  Admin-only `User.Manage` OperationClaim; added Admin-only audit reversion for
  Product/Category snapshots with state checks, PostgreSQL xmin optimistic
  concurrency, append-only reversal records, and HTTP conflict responses.
  Added the `Audit.Revert` permission and migration. Moved
  physical deletion out of the generic repository contract into explicit
  opt-in purge DALs and made list reads no-tracking by default. Added focused
  permissions, repository, session invalidation, revert, and PostgreSQL
  concurrency tests. Session invalidation now commits authorization-version
  changes and revocations before evicting token-validation cache entries, and
  defers eviction until ambient transaction completion.

- Added a locally built Aho-Corasick moderation matcher with attributed English
  and Turkish lexicons for user-authored catalog/community content, plus tests
  for normalization, obfuscation, token boundaries, and allowed text.
- Added normalized Product name/slug/HTTP(S) website identity handling,
  case-insensitive name and slug collision checks, PostgreSQL name uniqueness,
  and public catalog filtering to published Products. Shared vendor URLs remain
  allowed because semantic Product duplication is still an open domain rule.
- Added the reviewed `ProductIdentityConstraints` migration; it enforces
  case-insensitive Product name uniqueness without imposing an unapproved
  WebsiteUrl uniqueness constraint.
- Enabled administrative Redis commands only in the infrastructure test client
  so it can verify the configured LFU policy and disabled persistence.
- Added user-scoped, idempotent Product favorites and Product comments with
  database-paginated reads, author-only status-based deletion, moderator
  visibility controls, public visibility filtering against Product and User
  status, audit records, and seeded OperationClaim permissions.
- Added Product Documents (Markdown content), Media metadata (validated HTTP(S)
  URL references), append-only versioned Changelogs, and Contributor credit
  workflows with validation, audit records, and Product-scoped APIs. Binary
  media upload/storage remains intentionally unselected.
- Completed async Product and Category write commands with cancellation support,
  actor-attributed JSONB audit records, and validated Product Category/Tag
  association replacement.
- Added the V1 governance use cases: verified Product edits become scalar,
  Category, and Tag proposals; moderators can accept/reject proposals, grant or
  revoke Admin verification, and process Product reports.
- Added database-paginated pending-proposal and open-report queues, plus
  `ProductReport.Review` permission seed/migration for Moderator and Admin.
- Added Business, validation, API, and governance workflow tests in the single
  Tests project; Community verification voting remains intentionally undefined.
- Consolidated business-rule execution into one null-safe sequential/concurrent
  runner and removed the generic CRUD Business contract.
- Added test-covered Catalog business rules for duplicate category/product
  names, active category assignments, and TagCategory selection constraints;
  Category commands now accept DTOs and deactivate without physical deletion.
- Added additive, idempotent OperationClaim discovery with an opt-in startup
  synchronization setting; legacy seeded permission names remain stable and
  stale claims are not automatically pruned.
- Added result-aware transaction commit/rollback and cache invalidation, stable
  interceptor ordering, post-operation logging, and convention-based secured
  operation claim names. The cache-after-commit test now obtains interceptors
  from the production selector rather than manually arranging them.
- Normalized Business Manager aspect declarations to the selector's actual
  outer-to-inner order and added a contract test covering every decorated
  public Manager method, preventing source-order drift from runtime behavior.
- Made CacheAspect TTL explicit at the use case; absent TTL has no time-based
  expiry. Redis now uses disposable allkeys-LFU caching with separate data and
  container memory limits and persistence disabled.
- Added focused Core, Business, DataAccess, and WebAPI tests inside the sole
  `Tests/Tests.csproj` project.

- Added Turkish `MEMORY.md` as the project-local source for durable
  architecture decisions, active priorities, and deferred work.
- Removed inherited Product inventory fields and the duplicate scalar Product
  Category relationship; catalog classification now reads through
  `ProductCategories` only.
- Added typed Product edit-proposal workflow states and removed unsupported
  comment reply persistence from V1.
- Added deterministic identity, status, reporting, and verification lookup
  seeds. New registrations now receive the seeded `User` role rather than a
  copied direct claim.
- Replaced new-password HMAC hashing with PBKDF2-SHA512 and constant-time
  verification while retaining a temporary legacy verification path.
- Added 403 handling for authorization failures and redacted sensitive
  password, token, secret, and authorization parameters from technical logs.
- Protected Category mutations with `Category.Manage` and corrected legacy
  Product and Category update/delete HTTP verbs.
- Distinguished unauthenticated requests as 401 from authenticated but
  unauthorized requests as 403.
- Recorded the foundation-cleanup state and deferred work in the Turkish
  project memory.
- Added Product create/update command DTOs, JWT-derived creator assignment,
  category aggregate validation, and transactional category replacement.
- Removed the obsolete ConsoleUI source project from the repository.
- Corrected the PostgreSQL EF Core provider reference to the available and
  EF Core 10-compatible Npgsql 10.0.3 release.
- Added the ASP.NET Core framework reference required by Core HTTP abstractions
  and fixed build-blocking DTO, cache namespace, and EF migration metadata calls.
- Documented the pinned local .NET SDK and solution restore workflow required
  for Rider NuGet and framework-reference resolution.
- Hardened catalog and identity schema constraints with case-insensitive
  PostgreSQL email/slug uniqueness, bounded profile values, moderation queue
  indexes, proposal description limits, and validated `jsonb` audit payloads.
- Excluded inactive roles from effective-permission resolution and changed the
  Product delete command to a status-based soft delete.
- Added a scoped Unit of Work and asynchronous repository read/write contract;
  repository mutations now stage EF changes and command handlers own commits.
- Added the EF Core design-time dependency required to generate and review the
  initial PostgreSQL migration from the WebAPI startup project.
- Made transaction interception safe for `Task` and `Task<T>` command methods,
  constrained audit persistence to an append-only DAL, and removed legacy
  entity-based Product write commands from the public service contract.
- Made cache and performance aspects await-aware and added the initial xUnit
  test project with coverage collection support.
- Organized the single test project by production layer and a root-level
  integration-test area, keeping test ownership explicit without creating a
  second test solution or project.
- Moved the sole test project directly to `Tests/Tests.csproj`; no product-
  prefixed test project or nested test-project directory remains.
- Documented the immutable single-project test layout and its layer/integration
  folder ownership rules in the project memory.
- Corrected the PostgreSQL 18 Compose data-volume mount so a fresh local
  database can initialize successfully.
- Added the missing Redis configuration and infrastructure documentation
  referenced by Docker Compose.
- Renamed the inherited `NorthwindContext` and design-time factory to
  `GuidoraDbContext` across the data-access and API layers.
- Switched the EF Core data access provider from SQL Server LocalDB to
  configuration-driven PostgreSQL with Npgsql.
- Registered `GuidoraDbContext` through ASP.NET Core dependency injection and
  made all EF DAL implementations share the scoped context.
- Added a multi-stage backend Dockerfile, Compose backend service, dependency
  health conditions, environment template values, and a liveness endpoint.
- Added explicit EF Core entity configurations for keys, foreign keys, indexes,
  duplicate-prevention constraints, and delete behavior.
- Added a design-time `GuidoraDbContext` factory for PostgreSQL migrations.
- Removed the stale SQL Server database logger configuration; technical logs
  now remain file-based until a PostgreSQL logging contract is deliberately
  introduced.
- Added rotating, hashed refresh sessions with 15-minute access tokens and
  persistent session settings for frictionless but revocable sign-in.
- Added JWT session and authorization-version validation backed by PostgreSQL
  and a one-minute Redis validation cache.
- Added user authentication state, session invalidation, ban/restore, direct
  claim invalidation, and effective direct-plus-role claim resolution.
- Added local PostgreSQL, Redis, and RabbitMQ infrastructure with Docker Compose.
- Added persistent volumes, health checks, and a private Docker network.
- Added `.env.example` for local infrastructure configuration.
- Added Guidora entity foundations for profiles, roles, product statuses, tags,
  tag categories, documents, media, changelogs, comments, favorites, reports,
  contributors, proposals, verification and audit records.
- Added abstract DAL contracts and Entity Framework DAL implementations for the
  new entity foundations.
- Added an opt-in EF Core startup migration switch through
  `Database:ApplyMigrationsOnStartup`.
- Removed the secondary `ServiceProvider` construction from the dependency
  resolver; cross-cutting aspects now resolve services from the final host
  container.
- Made performance timing invocation-local and ensured interception cleanup
  runs even when the intercepted operation throws.
- Enabled async-flow propagation for transaction scopes.
- Fixed registration creating a duplicate authentication-state row by making
  state creation single-sourced through session invalidation.
- Wrapped user registration in the existing transaction aspect so user,
  claim, and authentication-state writes commit atomically.
- Added profile-driven Redis cache aspects for public catalog navigation,
  listings, and product details, with write-side cache invalidation.
- Added a RabbitMQ durable-topic publisher abstraction and Compose-to-API
  configuration binding without coupling V1 commands to a worker or consumer.

### Architecture

- Established the direction toward independently deployable Docker services.
- Each service will own its domain model, data access, and database migrations.
- Preserved the existing JWT claim flow and the existing boolean `User.Status`
  property.
- Kept legacy Product fields temporarily so the existing Business/API flow can
  be migrated without an unsafe breaking change.
- Kept startup migration disabled by default until the first reviewed migration
  exists.
- Removed the obsolete ConsoleUI project from the solution and backend image
  build path.
- Kept PostgreSQL as the cache source of truth; the homepage popular-products
  cache remains reserved until the views/metrics domain is designed.

### Verification

- Static configuration and diff checks passed.
- Runtime build and migration execution remain pending because the environment
  does not currently have the .NET runtime/SDK installed.
- Added the explicit `System.Threading.Tasks` import required by `Program.cs`
  because implicit usings are disabled.

### Notes

- The PostgreSQL infrastructure is available, but the application EF Core provider migration is not complete yet.
- No official release has been published yet.
