# RealEstate Backend Context

## Project purpose

This backend is for a real estate platform. The goal is not just a basic listing website. The long-term direction is a modern real estate intelligence platform with listings, search, filters, comparisons, price insights, agent tools, agencies, CRM features, and AI-assisted workflows.

Current focus: build a clean backend foundation before frontend work.

The listing backend is now in strong MVP shape:

```text
Listings
Translations
Images
Apartment details
House details
Filters
Pagination
Swagger testing
Integration tests
```

Next backend direction:

```text
Users/auth foundation
Roles
Later agencies/agents
Later listing ownership
Later CRM/client notes
```

---

## Tech stack

```text
.NET / ASP.NET Core
C#
Clean Architecture
Entity Framework Core
PostgreSQL
Docker / Docker Compose
Swagger / Swashbuckle
xUnit integration tests
FluentAssertions
Microsoft.AspNetCore.Mvc.Testing
Testcontainers PostgreSQL
```

Tests use a real temporary PostgreSQL container, not EF in-memory provider.

---

## Solution structure

```text
src/
  RealEstate.Api
    Controllers
      ListingsController.cs
      HealthController.cs
    Program.cs

  RealEstate.Application
    Common
      Files
      Storage
      PagedResult.cs
    Listings
      Commands
        CreateListing
        UploadListingImage
        DeleteListingImage
        SetPrimaryListingImage
        ReorderListingImages
      Queries
        GetListings
        GetListingById
      Dtos
      Mappings
      Repositories

  RealEstate.Domain
    Common
      IAuditableEntity.cs
    Entities
      Listing.cs
      ListingTranslation.cs
      ListingImage.cs
      ListingApartmentDetails.cs
      ListingHouseDetails.cs
    Enums

  RealEstate.Infrastructure
    Persistence
      Configurations
      Migrations
      Repositories
      RealEstateDbContext.cs
    Storage
      LocalFileStorageService.cs
      LocalFileStorageOptions.cs
    DependencyInjection.cs

tests/
  RealEstate.Tests
    Integration
      Listings
        ListingsEndpointTests.cs
        ListingImagesEndpointTests.cs
        ListingTestHelpers.cs
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

Example listing create flow:

```text
POST /api/listings
  ↓
ListingsController.CreateListing
  ↓
CreateListingHandler
  ↓
CreateListingValidator
  ↓
Listing aggregate root created
  ↓
ListingTranslation children attached
  ↓
ApartmentDetails or HouseDetails attached based on PropertyType
  ↓
IListingRepository.CreateAsync
  ↓
ListingRepository
  ↓
RealEstateDbContext.SaveChangesAsync
  ↓
PostgreSQL
```

Rules:

```text
Controllers stay thin.
Handlers contain use-case/application logic.
Domain contains entities, enums, and core business rules.
Infrastructure contains EF Core, repositories, database config, migrations, and local storage.
Application owns repository interfaces.
Infrastructure implements repository interfaces.
No MediatR yet.
No AutoMapper yet.
No FluentValidation package yet.
No generic repository / Unit of Work yet.
```

---

## Important architecture decisions

### Aggregate rule

`Listing` is the aggregate root.

Child entities:

```text
ListingTranslation
ListingImage
ListingApartmentDetails
ListingHouseDetails
```

Current DbContext rule:

```text
Expose DbSet<Listing> publicly.
Do not expose child entities as public DbSets unless needed.
Child entities are accessed through Listing navigation properties.
```

Current known public DbSet:

```csharp
public DbSet<Listing> Listings => Set<Listing>();
```

Repository uses `_dbContext.Set<ListingImage>()` internally when needed for image insert/delete.

---

## Domain model

### Main entity

```text
Listing
```

Common listing fields:

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
YearBuilt
YearRenovated
BalconyCount
ParkingSpaces
HasBasement
IsExchangePossible
HeatingType
FurnishingStatus
Condition
Orientation
Latitude
Longitude
CreatedAtUtc
ModifiedAtUtc
```

Important: `Floor` and `TotalFloors` were removed from `Listing`.

They now belong only to:

```text
ListingApartmentDetails
```

---

## Property-specific details

### Apartment details

Entity:

```text
ListingApartmentDetails
```

Table:

```text
ListingApartmentDetails
```

Fields:

```text
ListingId
ApartmentType
Floor
TotalFloors
HasElevator
```

Relationship:

```text
Listing 1 → 0/1 ListingApartmentDetails
```

### House details

Entity:

```text
ListingHouseDetails
```

Table:

```text
ListingHouseDetails
```

Fields:

```text
ListingId
HouseType
NumberOfFloors
YardAreaSquareMeters
```

Relationship:

```text
Listing 1 → 0/1 ListingHouseDetails
```

Request rule:

```text
If PropertyType = Apartment:
  apartmentDetails required
  houseDetails must be null

If PropertyType = House:
  houseDetails required
  apartmentDetails must be null
```

---

## Enums

Current important enums:

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

Currency
  EUR
  MKD
  USD
  etc.

HeatingType
  Unknown
  None
  Electric
  Central
  Gas
  Wood
  HeatPump
  Other

FurnishingStatus
  Unknown
  Unfurnished
  SemiFurnished
  Furnished

PropertyCondition
  Unknown
  New
  Renovated
  Good
  NeedsRenovation

Orientation
  Unknown
  North
  South
  East
  West
  NorthEast
  NorthWest
  SouthEast
  SouthWest

ApartmentType
  Unknown
  Studio
  Standard
  Penthouse
  Duplex
  Loft
  Maisonette
  Other

HouseType
  Unknown
  Detached
  SemiDetached
  Terraced
  Townhouse
  Villa
  Cottage
  Other
```

Enums are stored as strings in PostgreSQL using EF Core conversions.

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
LanguageCode
Title
Description
AddressLine
City
Municipality
Neighborhood
```

Location structure:

```text
City          = Skopje / Скопје
Municipality  = Centar / Центар
Neighborhood  = Center / Центар
AddressLine   = street/address
```

Each listing can have multiple translations.

Unique rule:

```text
One translation per language per listing.
```

Language behavior:

```text
GET listing with ?lang=mk returns mk translation if available.
If requested language is missing, fallback to first available translation.
```

---

## Listing images

Entity:

```text
ListingImage
```

Table:

```text
ListingImages
```

Fields:

```text
Id
ListingId
Url
StoredFileName
ContentType
SizeBytes
SortOrder
IsPrimary
CreatedAtUtc
ModifiedAtUtc
```

Image storage:

```text
Local filesystem
src/RealEstate.Api/wwwroot/uploads/listings/{listingId}/{storedFileName}
```

Public URL:

```text
/uploads/listings/{listingId}/{storedFileName}
```

Ignored by Git:

```text
src/RealEstate.Api/wwwroot/uploads/
```

Image validation:

```text
Max size: 5 MB
Allowed extensions: .jpg, .jpeg, .png, .webp
Allowed content types: image/jpeg, image/png, image/webp
Max images per listing: 20
```

Image rules:

```text
First uploaded image becomes primary.
Images have SortOrder.
Only one primary image per listing.
```

Database constraint:

```text
Filtered unique index on ListingId where IsPrimary = true.
```

Important implementation detail:

`SetPrimaryListingImageHandler` uses a two-phase save:

```csharp
foreach (var image in listing.Images)
{
    image.IsPrimary = false;
}

// Save in two phases because the database enforces only one primary image per listing.
// A single SaveChanges call can fail if EF updates the new primary before clearing the old one.
await _listingRepository.SaveChangesAsync(cancellationToken);

selectedImage.IsPrimary = true;

await _listingRepository.SaveChangesAsync(cancellationToken);
```

Reason:

```text
C# memory state can be correct, but EF Core SQL update order is not guaranteed.
PostgreSQL checks the filtered unique index during updates.
Two-phase save prevents temporary duplicate primary images.
```

---

## Current listing endpoints

### Health endpoints

```http
GET /api/health
GET /api/health/database
```

Database health endpoint checks PostgreSQL connectivity.

---

### Create listing

```http
POST /api/listings
```

Creates a new listing with translations and either apartment or house details.

Apartment request shape:

```json
{
  "listingType": "Sale",
  "propertyType": "Apartment",
  "price": 126000,
  "currency": "EUR",
  "areaSquareMeters": 72,
  "rooms": 3,
  "bathrooms": 1,
  "yearBuilt": 2018,
  "yearRenovated": 2022,
  "balconyCount": 2,
  "parkingSpaces": 1,
  "hasBasement": true,
  "isExchangePossible": false,
  "heatingType": "Central",
  "furnishingStatus": "Furnished",
  "condition": "Good",
  "orientation": "SouthEast",
  "latitude": 41.9981,
  "longitude": 21.4254,
  "apartmentDetails": {
    "apartmentType": "Standard",
    "floor": 4,
    "totalFloors": 8,
    "hasElevator": true
  },
  "houseDetails": null,
  "translations": []
}
```

House request shape:

```json
{
  "listingType": "Sale",
  "propertyType": "House",
  "price": 180000,
  "currency": "EUR",
  "areaSquareMeters": 120,
  "rooms": 4,
  "bathrooms": 2,
  "yearBuilt": 2005,
  "yearRenovated": 2020,
  "balconyCount": 1,
  "parkingSpaces": 2,
  "hasBasement": true,
  "isExchangePossible": false,
  "heatingType": "Gas",
  "furnishingStatus": "SemiFurnished",
  "condition": "Good",
  "orientation": "South",
  "latitude": 41.9981,
  "longitude": 21.4254,
  "apartmentDetails": null,
  "houseDetails": {
    "houseType": "Detached",
    "numberOfFloors": 2,
    "yardAreaSquareMeters": 350
  },
  "translations": []
}
```

Returns:

```http
201 Created
```

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
municipality
neighborhood
heatingType
furnishingStatus
condition
hasBasement
hasElevator
apartmentType
houseType
minYardAreaSquareMeters
maxYardAreaSquareMeters
page
pageSize
```

Example apartment filter:

```http
GET /api/listings?lang=en&propertyType=Apartment&heatingType=Central&furnishingStatus=Furnished&condition=Good&hasBasement=true&hasElevator=true&apartmentType=Standard&page=1&pageSize=20
```

Example house filter:

```http
GET /api/listings?lang=en&propertyType=House&houseType=Detached&minYardAreaSquareMeters=300&maxYardAreaSquareMeters=400&page=1&pageSize=20
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

Apartment response example:

```json
{
  "propertyType": "Apartment",
  "pricePerSquareMeter": 1750,
  "apartmentDetails": {
    "apartmentType": "Standard",
    "floor": 4,
    "totalFloors": 8,
    "hasElevator": true
  },
  "houseDetails": null
}
```

House response example:

```json
{
  "propertyType": "House",
  "pricePerSquareMeter": 1500,
  "apartmentDetails": null,
  "houseDetails": {
    "houseType": "Detached",
    "numberOfFloors": 2,
    "yardAreaSquareMeters": 350
  }
}
```

`PricePerSquareMeter` is rounded to 2 decimals in response mapping.

Example:

```text
125000 / 58 = 2155.1724...
Response = 2155.17
```

---

## Image endpoints

### Upload image

```http
POST /api/listings/{listingId}/images
```

Accepts multipart/form-data field:

```text
file
```

Returns:

```http
201 Created
```

Response:

```json
{
  "id": "guid",
  "url": "/uploads/listings/{listingId}/{fileName}.jpg",
  "contentType": "image/jpeg",
  "sizeBytes": 123456,
  "sortOrder": 0,
  "isPrimary": true
}
```

### Delete image

```http
DELETE /api/listings/{listingId}/images/{imageId}
```

If primary image is deleted, next image becomes primary.

Returns:

```http
204 No Content
```

### Set primary image

```http
PUT /api/listings/{listingId}/images/{imageId}/primary
```

Returns selected image response.

### Reorder images

```http
PUT /api/listings/{listingId}/images/order
```

Request:

```json
{
  "imageIds": ["guid1", "guid2", "guid3"]
}
```

Returns ordered image list.

---

## Current database tables

```text
Listings
ListingTranslations
ListingImages
ListingApartmentDetails
ListingHouseDetails
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
YearBuilt
YearRenovated
BalconyCount
ParkingSpaces
HasBasement
IsExchangePossible
HeatingType
FurnishingStatus
Condition
Orientation
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
Municipality
Neighborhood
```

Important columns in `ListingImages`:

```text
Id
ListingId
Url
StoredFileName
ContentType
SizeBytes
SortOrder
IsPrimary
CreatedAtUtc
ModifiedAtUtc
```

Important columns in `ListingApartmentDetails`:

```text
ListingId
ApartmentType
Floor
TotalFloors
HasElevator
```

Important columns in `ListingHouseDetails`:

```text
ListingId
HouseType
NumberOfFloors
YardAreaSquareMeters
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
ListingImage
```

`RealEstateDbContext.SaveChangesAsync` automatically sets:

```text
CreatedAtUtc on create
ModifiedAtUtc on update
```

Handlers should not manually set auditing timestamps.

---

## Current testing setup

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

Current integration test files:

```text
ListingsEndpointTests.cs
ListingImagesEndpointTests.cs
ListingTestHelpers.cs
```

Current tests cover:

```text
POST valid apartment listing returns 201
POST valid house listing returns 201
POST invalid price returns 400
GET listings returns paginated response
GET listings with price filter returns matching listings
GET listings with municipality filter returns matching listings
GET listings with apartment filters returns matching listings
GET listings with house filters returns matching listings
GET listing by ID returns requested language
GET missing listing returns 404
PricePerSquareMeter returns rounded value
Upload listing image returns 201
First uploaded image becomes primary
Delete image returns 204
Deleting primary image makes next image primary
Set primary image works
Only one image remains primary
PrimaryImageUrl points to selected primary image
Reorder listing images works
```

Latest known status after Task 4C:

```text
dotnet test passed
Expected count around 19/19 after Task 4C
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

Format:

```bash
dotnet format
```

---

## Current completed backend features

```text
Clean Architecture structure
Docker PostgreSQL setup
EF Core setup
Health endpoints
Database health check
Listing aggregate model
Listing translations
Apartment/house property detail split
Common listing details
Municipality field
Listing images
Local file storage
Primary image logic
Delete image
Set primary image
Reorder images
Pagination
Search/filtering
PricePerSquareMeter calculation and rounding
Automatic auditing fields
Swagger testing
Integration tests with real PostgreSQL Testcontainers
Frontend CORS support
```

---

## Recent completed tasks

### Task 4A — Common listing details + municipality

Added:

```text
BalconyCount
ParkingSpaces
HasBasement
IsExchangePossible
HeatingType
FurnishingStatus
Condition
YearRenovated
Orientation
Municipality
```

Migration:

```text
AddListingCommonDetailsAndMunicipality
```

### Task 4B — Apartment and house listing details

Added:

```text
ListingApartmentDetails
ListingHouseDetails
ApartmentType
HouseType
```

Moved out of `Listing`:

```text
Floor
TotalFloors
```

Migration:

```text
AddListingPropertyDetails
```

### Task 4C — Listing filters and response polish

Added:

```text
heatingType filter
furnishingStatus filter
condition filter
hasBasement filter
hasElevator filter
apartmentType filter
houseType filter
min/max yard area filter
PricePerSquareMeter rounding
stronger primary image test
two-phase save comment
```

Commit message:

```text
Add listing filters and response polish
```

---

## Current backend status

Backend listing module is ready for frontend integration.

However, the next planned backend work is users/auth foundation before frontend, because future frontend flows need:

```text
user accounts
agent accounts
agency ownership
my listings
protected create listing
saved listings
CRM notes later
```

---

## Next planned work

### Task 5A — User accounts foundation

Branch suggestion:

```bash
git checkout development
git pull
git checkout -b feature/user-accounts-foundation
```

Goal:

```text
Create basic user domain/database foundation.
No login yet.
No JWT yet.
No agencies yet.
No CRM yet.
```

Proposed first user table:

```text
Users
  Id
  Email
  PasswordHash
  FirstName
  LastName
  PhoneNumber
  Role
  Status
  CreatedAtUtc
  ModifiedAtUtc
```

Proposed enums:

```text
UserRole
  Admin
  AgencyOwner
  Agent
  User

UserStatus
  Active
  Disabled
  PendingVerification
```

Recommended split:

```text
Task 5A — User table/domain/migration
Task 5B — Register/Login with password hashing
Task 5C — JWT auth + protected endpoints
Task 5D — Listing ownership with CreatedByUserId / My listings
Task 6A — Agencies
Task 6B — Agency members
Task 6C — Agent profiles
Task 7 — CRM clients/notes/saved listings
```

Important decision:

```text
Do not build agencies before users.
Users first.
Agencies later.
AgencyMembers connects users to agencies.
Listings later belong to users/agents/agencies.
```

---

## Remaining backend ideas for later

Do not mix these into Task 5A:

```text
Full auth/JWT
Agencies
Agency members
Agent profiles
Listing ownership
Favorites
Saved searches
CRM clients
Client notes
Map search
Comparable listings
Average price analytics
AI document analyzer
Voice note helper
Admin moderation
Payments/subscriptions
```

Keep one clean task at a time.

Current next task:

```text
Task 5A — User accounts foundation
```
