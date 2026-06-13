# AGENTS.md — RealEstate Backend Instructions

## Project

This repository is the backend for a real estate platform.

Use the existing Clean Architecture structure. Do not rewrite architecture unless explicitly asked.

---

## Architecture rules

Follow this flow:

```text
RealEstate.Api
  → RealEstate.Application
  → RealEstate.Infrastructure
  → RealEstate.Domain
```

Runtime/database flow:

```text
Controller
  → Handler
  → Repository interface
  → Repository implementation
  → RealEstateDbContext
  → PostgreSQL
```

Layer responsibilities:

```text
RealEstate.Api
  HTTP controllers, Program.cs, Swagger, CORS, API wiring.

RealEstate.Application
  Use cases, handlers, request/query models, DTOs, mappings, repository interfaces.

RealEstate.Domain
  Entities, enums, common domain interfaces/rules.

RealEstate.Infrastructure
  EF Core, DbContext, entity configurations, migrations, repository implementations, external infrastructure.
```

---

## Current conventions

Use feature folders inside Application:

```text
Listings
  Commands
    CreateListing
  Queries
    GetListings
    GetListingById
  Dtos
  Mappings
  Repositories
```

Controllers must stay thin.

Handlers should contain application/use-case logic.

Repositories should contain database query logic.

DbContext should stay lean:

```text
DbSets
OnModelCreating
SaveChangesAsync auditing
```

Entity configurations must stay in:

```text
RealEstate.Infrastructure/Persistence/Configurations
```

---

## Do not introduce these unless explicitly requested

```text
MediatR
AutoMapper
FluentValidation package
Generic repository
UnitOfWork abstraction
Full CQRS framework
New architecture style
```

Current style is CQRS-lite with explicit handlers.

---

## Database rules

Database provider:

```text
PostgreSQL
```

EF Core provider:

```text
Npgsql.EntityFrameworkCore.PostgreSQL
```

Use EF Core migrations.

Do not edit old applied migrations unless explicitly instructed.

New schema changes require a new migration.

Use:

```bash
dotnet ef migrations add MigrationName --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api --output-dir Persistence/Migrations
```

Then:

```bash
dotnet ef database update --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api
```

---

## Auditing

Entities that need timestamps should implement:

```text
IAuditableEntity
```

Currently:

```text
Listing : IAuditableEntity
```

`RealEstateDbContext.SaveChangesAsync` sets:

```text
CreatedAtUtc
ModifiedAtUtc
```

Do not manually set `CreatedAtUtc` in handlers.

---

## Listing aggregate rules

`Listing` is the aggregate root.

`ListingTranslation` is a child entity.

Do not expose `DbSet<ListingTranslation>` unless there is a strong reason.

Access translations through:

```text
Listing.Translations
```

---

## Current endpoints

```http
GET /api/health
GET /api/health/database
POST /api/listings
GET /api/listings
GET /api/listings/{id}
```

`GET /api/listings` supports:

```text
lang
listingType
propertyType
minPrice
maxPrice
city
neighborhood
page
pageSize
```

Response is paginated:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

---

## Testing rules

Use integration tests for endpoint behavior.

Testing stack:

```text
xUnit
FluentAssertions
Microsoft.AspNetCore.Mvc.Testing
Testcontainers.PostgreSql
```

Tests should use temporary PostgreSQL via Testcontainers.

Do not write tests against the local development database.

Run before finishing backend changes:

```bash
dotnet build
dotnet test
```

---

## Package/version caution

Keep EF Core/Npgsql package versions compatible.

If there are EF version warnings, inspect `.csproj` files and align versions carefully.

Do not add EF Core references to Domain or Application.

EF Core belongs in Infrastructure. API may reference design-time packages only if needed.

---

## Current completed backend work

Completed:

```text
Backend solution structure
Docker PostgreSQL setup
EF Core setup
Health endpoints
Listing domain model
Listing translations
EF configurations
Migrations
Basic listing endpoints
API cleanup
Automatic auditing
Integration tests
Search filters + pagination
CORS for frontend localhost
```

---

## Frontend readiness

Backend is ready for initial frontend connection.

Local API example:

```http
http://localhost:5231/api/listings?lang=en&page=1&pageSize=20
```

Allowed frontend dev origins:

```text
http://localhost:3000
https://localhost:3000
http://localhost:5173
https://localhost:5173
```

---

## Before completing any backend task

Always run:

```bash
dotnet build
dotnet test
```

If API behavior changes, also manually smoke test with Swagger or browser.
