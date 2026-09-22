# Guidora Backend

Guidora Backend is a .NET platform designed to run on Docker and evolve into independently deployable microservices.

## Development principles

- Services must be able to run as independent Docker containers.
- Each service must own its domain model, `DbContext`, and migrations.
- Services must not access each other's tables directly.
- Asynchronous service communication must use RabbitMQ events.
- Redis is used for caching and temporary distributed state, not as the source of truth.
- Secrets must never be committed to the repository.
- Ongoing work must be recorded in the `Unreleased` section of `CHANGELOG.md`.
- A versioned changelog section must not be created before an official release.

## Development environment

Requirements:

- .NET 10 SDK
- Docker Engine
- Docker Compose

Start the local infrastructure:

```bash
cp .env.example .env
# Replace the default passwords in .env
docker compose up -d
docker compose ps
```

The Compose stack builds and runs the backend together with PostgreSQL, Redis,
and RabbitMQ. Copy `.env.example` to `.env` and replace every placeholder
secret before starting the stack. The backend is exposed on `localhost:8080`
and its liveness endpoint is available at `/health`.

Compose services:

| Service | Address |
|---|---|
| PostgreSQL | `localhost:5432` |
| Redis | `localhost:6379` |
| RabbitMQ AMQP | `localhost:5672` |
| RabbitMQ management UI | `http://localhost:15672` |

See [infra/README.md](infra/README.md) for infrastructure details.

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

The backend reads the PostgreSQL connection from the standard
`ConnectionStrings:Guidora` configuration key. In Docker Compose this is
overridden with the `postgres` service hostname. Redis and RabbitMQ are
provisioned as infrastructure dependencies; feature-specific integrations are
added only when their application contracts are defined.

## Redis cache and RabbitMQ

Redis is the shared cache provider for public catalog reads. Cache policy is
declared through `CacheAspect` profiles and configured under `Caching:Profiles`:
catalog navigation (60 minutes), catalog listings (10 minutes), and individual
product details (15 minutes). Product and category writes invalidate their
related profiles. Cache values are JSON envelopes with their concrete result
type; PostgreSQL remains the source of truth. Authentication session validation
uses a separate, one-minute distributed Redis entry.

The `popular-products` five-minute profile is reserved for the homepage once a
view-event and aggregation model is approved; it is intentionally not populated
from an invented metric. Per-user state, authorization, favorites, reports,
and mutable moderation data are not catalog-cached.

RabbitMQ is configured as a durable topic exchange named `guidora.events`.
`IEventPublisher` publishes JSON events with persistent delivery mode. No
consumer, worker, scheduler, or synchronous Product workflow is coupled to the
broker in V1; commands must first persist their canonical transaction, then a
future outbox-backed integration can publish an event safely.

The EF design-time factory reads `ConnectionStrings__Guidora` when creating
migrations. With the .NET SDK installed, create the first migration with:

```bash
ConnectionStrings__Guidora='Host=localhost;Port=5432;Database=guidora;Username=guidora;Password=your-password' \
  dotnet ef migrations add InitialGuidoraSchema \
  --project DataAccess --startup-project WebAPI \
  --output-dir Migrations
```

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
of the user's sessions and increments its authorization version. Future
UserRole and RoleOperationClaim command handlers must call the same
invalidation service after changing permissions.

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

## Verification

```bash
dotnet restore
dotnet build NetCoreBackend.sln --configuration Release
```

Relevant build and test commands must be run after code changes, and their results should be recorded in the commit or development notes.

## Change workflow

1. Make the change as a small, clearly scoped step.
2. Update the `Unreleased` section in `CHANGELOG.md`.
3. Update this README when the architecture or development workflow changes materially.
4. Run the relevant verification commands.
5. Create a meaningful commit.
