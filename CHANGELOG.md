# Changelog

All notable changes to the Guidora backend are documented here.

## [Unreleased]

### Added

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

### Architecture

- Established the direction toward independently deployable Docker services.
- Each service will own its domain model, data access, and database migrations.
- Preserved the existing JWT claim flow and the existing boolean `User.Status`
  property.
- Kept legacy Product fields temporarily so the existing Business/API flow can
  be migrated without an unsafe breaking change.
- Kept startup migration disabled by default until the first reviewed migration
  exists.

### Verification

- Static configuration and diff checks passed.
- Runtime build and migration execution remain pending because the environment
  does not currently have the .NET runtime/SDK installed.
- Added the explicit `System.Threading.Tasks` import required by `Program.cs`
  because implicit usings are disabled.

### Notes

- The PostgreSQL infrastructure is available, but the application EF Core provider migration is not complete yet.
- No official release has been published yet.
