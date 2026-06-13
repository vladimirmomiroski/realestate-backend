# RealEstate Backend Context

## Project purpose

This backend is for a real estate platform. The goal is not just a basic listing website. The long-term direction is a modern real estate intelligence platform with listings, search, filters, comparisons, price insights, agent tools, and AI-assisted workflows.

Current focus: build a clean backend foundation before switching to frontend.

---

## Tech stack

* .NET / ASP.NET Core
* C#
* Clean Architecture
* Entity Framework Core
* PostgreSQL
* Docker / Docker Compose
* Swagger / Swashbuckle
* xUnit integration tests
* Testcontainers PostgreSQL for isolated test database

---

## Solution structure

```text
src/
  RealEstate.Api
    Controllers
    Program.cs

  RealEstate.Application
    Common
    Listings
      Commands
      Queries
      Dtos
      Mappings
      Repositories

  RealEstate.Domain
    Entities
    Enums
    Common

  RealEstate.Infrastructure
    Persistence
      Configurations
      Migrations
      Repositories
      RealEstateDbContext.cs
    DependencyInjection.cs

tests/
  RealEstate.Tests
    Integration
```

---

## Architecture flow

```text
HTTP request
  ↓
Controller
  ↓
Application Handler
  ↓
Repository Interface
  ↓
Infrastructure Repository
  ↓
RealEstateDbContext
  ↓
PostgreSQL
```

Rules:

* Controllers stay thin.
* Handlers contain use-case/application logic.
* Domain contains entities, enums, and business rules.
* Infrastructure contains EF Core, repositories, database configuration, and migrations.
* Application owns repository interfaces.
* Infrastructure implements repository interfaces.
* No MediatR yet.
* No AutoMapper yet.
* No FluentValidation package yet.
* No generic repository / Unit of Work yet.

---

## Current completed backend features

### Health endpoints

```http
GET /api/health
GET /api/health/database
```

Database health endpoint checks PostgreSQL connectivity.

---

### Listing domain model

Main entity:

```text
Listing
```

Child entity:

```text
ListingTranslation
```

Enums:

```text
ListingType
  Sale
  Rent

PropertyType
  Apartment
  House

ListingStatus
  Draft
  Active
  Reserved
  Sold
  Rented
  Archived
```

Important decision:

* `Listing` is the aggregate root.
* `ListingTranslation` is a child entity.
* `DbContext` exposes `DbSet<Listing>` only.
* `ListingTranslation` is accessed through `Listing.Translations`.

---

## Translation model

Fixed app labels are handled by frontend localization:

```text
Apartment / Стан
House / Куќа
Sale / Продажба
Rent / Изнајмување
```

Custom listing text is stored in backend translations:

```text
Title
Description
AddressLine
City
Neighborhood
```

Each listing can have multiple translations.

Unique rule:

```text
One translation per language per listing.
```

---

## Current listing endpoints

### Create listing

```http
POST /api/listings
```

Creates a new listing with one or more translations.

Returns:

```http
201 Created
```

Uses submitted first translation language for response.

---

### Get paginated / filtered listings

```http
GET /api/listings
```

Supported query parameters:

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

Example:

```http
GET /api/listings?lang=en&propertyType=Apartment&listingType=Sale&minPrice=80000&maxPrice=150000&page=1&pageSize=20
```

Response shape:

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

Page size behavior:

```text
Default pageSize = 20
Maximum pageSize = 100
Minimum page = 1
```

---

### Get listing by ID

```http
GET /api/listings/{id}?lang=en
GET /api/listings/{id}?lang=mk
```

Returns requested language if available, otherwise falls back to first available translation.

Missing listing returns:

```http
404 Not Found
```

---

## Current database tables

```text
Listings
ListingTranslations
__EFMigrationsHistory
```

Important columns in `Listings`:

```text
Id
ListingType
PropertyType
Status
Price
Currency
AreaSquareMeters
Rooms
Bathrooms
Floor
TotalFloors
YearBuilt
Latitude
Longitude
CreatedAtUtc
ModifiedAtUtc
```

Important columns in `ListingTranslations`:

```text
Id
ListingId
LanguageCode
Title
Description
AddressLine
City
Neighborhood
```

---

## Auditing

Auditing interface:

```text
IAuditableEntity
```

Located in:

```text
RealEstate.Domain/Common/IAuditableEntity.cs
```

Currently used by:

```text
Listing
```

`RealEstateDbContext.SaveChangesAsync` automatically sets:

```text
CreatedAtUtc on create
ModifiedAtUtc on update
```

`CreateListingHandler` should not manually set `CreatedAtUtc`.

---

## Current testing setup

Integration tests exist for listing endpoints.

Testing stack:

```text
xUnit
FluentAssertions
Microsoft.AspNetCore.Mvc.Testing
Testcontainers.PostgreSql
```

Tests use a temporary PostgreSQL Docker container, not the local development database.

Test flow:

```text
dotnet test
  ↓
start temporary PostgreSQL container
  ↓
apply migrations
  ↓
run API tests
  ↓
delete container
```

Current tests cover:

```text
POST valid listing returns 201
POST invalid price returns 400
GET listings returns paginated response
GET listings with price filter returns matching listings
GET listing by ID returns requested language
GET missing listing returns 404
```

---

## Local development database

Docker PostgreSQL values:

```text
Database: realestate_db
User: realestate_user
Password: realestate_password
Host: localhost
Port: 5432
```

Common commands:

```bash
docker compose up -d
docker compose down
```

Do not use this unless intentionally deleting database data:

```bash
docker compose down -v
```

---

## Important commands

Build:

```bash
dotnet build
```

Run tests:

```bash
dotnet test
```

Run API:

```bash
dotnet run --project src/RealEstate.Api
```

Add migration:

```bash
dotnet ef migrations add MigrationName --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api --output-dir Persistence/Migrations
```

Update database:

```bash
dotnet ef database update --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api
```

---

## Current backend status

Completed:

```text
Clean Architecture structure
Docker PostgreSQL setup
EF Core setup
Listing domain model
Listing EF configurations
Migrations
Basic listing endpoints
API cleanup
Automatic auditing fields
Integration tests
Listing search filters + pagination
Frontend CORS support
```

Backend is ready for initial frontend integration.

---

## Remaining backend ideas for later

Do not do these before initial frontend unless explicitly decided:

```text
Users/auth
Agents/agencies
Listing images
Update/delete listings
Favorites
Map search
Comparable listings
Average price analytics
AI document analyzer
Agent notes
Voice note helper
Admin panel
```

Next likely backend work after frontend begins:

```text
Listing images
Update/edit listing
Auth/users
Agent ownership of listings
```
