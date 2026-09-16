# Changelog

All notable changes to this project are documented in this file.

## [1.0.1] - 2026-09-16

### Added

- Added a reference to the W3Schools Northwind sample database used by the
  application.

### Changed

- Expanded the README with setup requirements, database configuration, API
  endpoints, JWT usage, development commands and security notes.
- Documented the current `v1.0` release and MIT license in the README.

## [1.0.0] - 2026-09-16

### Changed

- Migrated all solution projects from .NET 6 to .NET 10.
- Updated dependencies for .NET 10 compatibility:
  - Autofac `9.3.2`
  - Autofac.Extensions.DependencyInjection `11.0.2`
  - Autofac.Extras.DynamicProxy `8.1.0`
  - FluentValidation `12.1.1`
  - FluentValidation.AspNetCore `11.3.1`
  - Microsoft.EntityFrameworkCore.SqlServer `10.0.12`
  - Microsoft.AspNetCore.Authentication.JwtBearer `10.0.12`
  - Microsoft.Extensions.Configuration `10.0.12`
  - Microsoft.IdentityModel.Tokens `8.22.0`
  - System.IdentityModel.Tokens.Jwt `8.22.0`
  - Swashbuckle.AspNetCore `10.2.3`
  - log4net `3.4.0`
  - Newtonsoft.Json `13.0.4`
- Added `global.json` to pin the solution to the .NET 10.0.401 SDK feature band.
- Preserved the existing Core, Entities, DataAccess, Business, WebAPI and
  ConsoleUI architecture and project references.

### Fixed

- Removed nullable-reference warnings across the core, business and entity
  layers by defining safe defaults and explicit null handling.
- Preserved exception stack traces in `TransactionScopeAspect` by using bare
  `throw` re-throws.
- Hardened dependency resolution and configuration validation for cache,
  logging, validation, JWT and HTTP context services.
- Removed the vulnerable log4net version and updated to the current package
  release.

### Verification

- Restore completed successfully.
- Release build completed successfully with zero errors and zero warnings.
- WebAPI startup and Swagger endpoint smoke test completed successfully.
- No automated test projects are currently included in the solution.
