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

Compose services:

| Service | Address |
|---|---|
| PostgreSQL | `localhost:5432` |
| Redis | `localhost:6379` |
| RabbitMQ AMQP | `localhost:5672` |
| RabbitMQ management UI | `http://localhost:15672` |

See [infra/README.md](infra/README.md) for infrastructure details.

## Project structure

The current solution layers will evolve toward independently deployable services:

```text
Core/        Shared technical infrastructure and abstractions
Entities/    Current domain models and DTOs
DataAccess/  Current EF Core data access
Business/    Current business rules and services
WebAPI/      Current HTTP entry point
ConsoleUI/   Console entry point
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
