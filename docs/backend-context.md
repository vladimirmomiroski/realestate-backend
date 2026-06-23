# RealEstate Backend Context

## Project purpose

This backend is for a real estate platform. The goal is not just a basic listing website. The long-term direction is a modern real estate intelligence platform with listings, search, filters, comparisons, price insights, agent tools, agencies, CRM features, and AI-assisted workflows.

Current focus: backend foundation before frontend work.

Current backend status: **listing module + auth/listing ownership foundation are complete enough to move into Agencies next.**

The backend now supports:

```text
Listings
Translations
Images
Apartment details
House details
Filters
Pagination
Users
Register
Login
Password hashing
JWT auth
Protected listing creation
Listing ownership
My listings
Image owner authorization
Free listing limit per user
Swagger testing
Integration tests
```

Next backend direction:

```text
Agencies
Agency members
Agent profiles
Later admin/role rules
Later CRM/client notes
Later saved listings/favorites
Later payments/subscriptions
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
JWT Bearer Authentication
xUnit integration tests
FluentAssertions
Microsoft.AspNetCore.Mvc.Testing
Testcontainers PostgreSQL
```

Tests use a real temporary PostgreSQL container, not EF in-memory provider.

---

## Solution structure

Important: repositories are directly under `RealEstate.Infrastructure/Persistence/Repositories`. There is **no** nested `Listings` folder under repositories.

```text
src/
  RealEstate.Api
    Authentication
      CurrentUserService.cs
    Controllers
      ListingsController.cs
      AuthController.cs
      HealthController.cs
    Program.cs

  RealEstate.Application
    Auth
      Commands
        RegisterUser
        LoginUser
      Dtos
      Repositories
    Common
      Authentication
        ICurrentUserService.cs
      Files
      Security
        IJwtTokenGenerator.cs
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
        GetMyListings
      Dtos
      Mappings
      Repositories

  RealEstate.Domain
    Common
      IAuditableEntity.cs
    Entities
      User.cs
      Listing.cs
      ListingTranslation.cs
      ListingImage.cs
      ListingApartmentDetails.cs
      ListingHouseDetails.cs
    Enums

  RealEstate.Infrastructure
    Persistence
      Configurations
        UserConfiguration.cs
        ListingConfiguration.cs
        ListingTranslationConfiguration.cs
        ListingImageConfiguration.cs
        ListingApartmentDetailsConfiguration.cs
        ListingHouseDetailsConfiguration.cs
      Migrations
      Repositories
        UserRepository.cs
        ListingRepository.cs
      RealEstateDbContext.cs
    Security
      JwtOptions.cs
      JwtTokenGenerator.cs
      PasswordHasherService.cs
    Storage
      LocalFileStorageService.cs
      LocalFileStorageOptions.cs
    DependencyInjection.cs

tests/
  RealEstate.Tests
    Integration
      Auth
        AuthEndpointTests.cs
        AuthTestHelpers.cs
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
[Authorize]
  ↓
ListingsController.CreateListing
  ↓
CreateListingHandler
  ↓
CreateListingValidator
  ↓
ICurrentUserService gets logged-in user id from JWT claims
  ↓
IListingRepository.CountByCreatedByUserIdAsync checks free listing limit
  ↓
Listing aggregate root created
  ↓
CreatedByUserId assigned
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
Infrastructure contains EF Core, repositories, database config, migrations, security, and local storage.
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
Expose aggregate roots publicly.
Do not expose child entities as public DbSets unless needed.
Child entities are accessed through Listing navigation properties or internal Set<TEntity>() usage.
```

Current known public DbSets:

```csharp
public DbSet<Listing> Listings => Set<Listing>();
public DbSet<User> Users => Set<User>();
```

Repository may use `_dbContext.Set<ListingImage>()` internally when needed for image insert/delete.

---

## Domain model

### User

Entity:

```text
User
```

Table:

```text
Users
```

Fields:

```text
Id
Email
NormalizedEmail
PasswordHash
FirstName
LastName
PhoneNumber
Role
Status
CreatedAtUtc
ModifiedAtUtc
```

Important rule:

```text
NormalizedEmail is used for case-insensitive uniqueness.
```

User roles:

```text
Admin
AgencyOwner
Agent
User
```

User statuses:

```text
Active
Disabled
PendingVerification
```

Current behavior:

```text
Register creates user as PendingVerification.
PendingVerification is not enforced yet.
Any authenticated user can create listings until stricter rules are added.
```

---

### Listing

Entity:

```text
Listing
```

Common listing fields:

```text
Id
CreatedByUserId
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

Important:

```text
CreatedByUserId links listing to the user who created it.
Floor and TotalFloors were removed from Listing.
Floor and TotalFloors belong only to ListingApartmentDetails.
```

Relationship:

```text
User 1 → many Listings
Listing.CreatedByUserId is nullable in database for compatibility with older/dev data, but new authenticated listings assign it.
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

Current important listing enums:

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
Default language fallback used in listing create route values is mk.
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
Only the listing owner can upload/delete/set primary/reorder images.
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

## Auth and security

### Register

```http
POST /api/auth/register
```

Request:

```json
{
  "email": "user@test.com",
  "password": "Password123!",
  "firstName": "Test",
  "lastName": "User",
  "phoneNumber": "+38970123456"
}
```

Response:

```json
{
  "user": {
    "id": "guid",
    "email": "user@test.com",
    "firstName": "Test",
    "lastName": "User",
    "phoneNumber": "+38970123456",
    "role": "User",
    "status": "PendingVerification"
  }
}
```

Register behavior:

```text
Creates user.
Normalizes email.
Blocks duplicate normalized email.
Hashes password using ASP.NET Core PasswordHasher.
Does not return JWT token.
```

### Login

```http
POST /api/auth/login
```

Request:

```json
{
  "email": "user@test.com",
  "password": "Password123!"
}
```

Response:

```json
{
  "accessToken": "eyJ...",
  "user": {
    "id": "guid",
    "email": "user@test.com",
    "firstName": "Test",
    "lastName": "User",
    "phoneNumber": "+38970123456",
    "role": "User",
    "status": "PendingVerification"
  }
}
```

Login behavior:

```text
Wrong password returns 401.
Unknown email returns 401.
Both use generic invalid credentials behavior.
```

### JWT

JWT settings are stored in `appsettings.json`:

```json
{
  "Jwt": {
    "Issuer": "RealEstate.Api",
    "Audience": "RealEstate.Client",
    "Secret": "CHANGE_THIS_LOCAL_DEV_SECRET_AT_LEAST_32_CHARACTERS_LONG",
    "AccessTokenExpirationMinutes": 60
  }
}
```

JWT claims include:

```text
sub
email
ClaimTypes.NameIdentifier
ClaimTypes.Email
ClaimTypes.Role
```

`CurrentUserService` reads the current user id from:

```text
ClaimTypes.NameIdentifier
```

Important `Program.cs` auth setup:

```text
AddAuthentication configures JwtBearerDefaults.AuthenticationScheme as default authenticate and challenge scheme.
UseAuthentication() must be before UseAuthorization().
```

Swagger uses bearer auth. With current Swashbuckle/OpenAPI version, the security requirement uses the new lambda style:

```csharp
options.AddSecurityRequirement(openApiDocument => new OpenApiSecurityRequirement
{
    {
        new OpenApiSecuritySchemeReference("Bearer", openApiDocument),
        new List<string>()
    }
});
```

Important Swagger usage:

```text
Click Authorize.
Paste only accessToken value.
Do not paste "Bearer ".
Do not paste the whole JSON response.
Swagger adds "Bearer " automatically.
```

---

## Current endpoints

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

Auth:

```text
Requires JWT.
Returns 401 without token.
Assigns CreatedByUserId from logged-in user.
Each user can create up to 3 free listings.
4th listing returns 400 Bad Request.
```

Free listing limit message:

```text
Free listing limit reached. Each user can create up to 3 listings.
```

Creates a new listing with translations and either apartment or house details.

Returns:

```http
201 Created
400 Bad Request
401 Unauthorized
```

Apartment request shape:

```json
{
  "listingType": "Sale",
  "propertyType": "Apartment",
  "status": "Active",
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
  "status": "Active",
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

---

### Get paginated / filtered listings

```http
GET /api/listings
```

Auth:

```text
Public.
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

### Get my listings

```http
GET /api/listings/my?lang=mk&page=1&pageSize=20
```

Auth:

```text
Requires JWT.
Returns only listings where CreatedByUserId equals logged-in user id.
Returns 401 without token.
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

---

### Get listing by ID

```http
GET /api/listings/{id}?lang=en
GET /api/listings/{id}?lang=mk
```

Auth:

```text
Public.
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

All image endpoints require JWT and listing owner.

Expected authorization behavior:

```text
No token      -> 401 Unauthorized
Wrong user    -> 403 Forbidden
Listing owner -> success
```

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
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
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
401 Unauthorized
403 Forbidden
404 Not Found
```

### Set primary image

```http
PUT /api/listings/{listingId}/images/{imageId}/primary
```

Returns selected image response.

Returns:

```http
200 OK
401 Unauthorized
403 Forbidden
404 Not Found
```

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

Returns:

```http
200 OK
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
```

---

## Current database tables

```text
Users
Listings
ListingTranslations
ListingImages
ListingApartmentDetails
ListingHouseDetails
__EFMigrationsHistory
```

Important columns in `Users`:

```text
Id
Email
NormalizedEmail
PasswordHash
FirstName
LastName
PhoneNumber
Role
Status
CreatedAtUtc
ModifiedAtUtc
```

Important columns in `Listings`:

```text
Id
CreatedByUserId
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
User
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
AuthEndpointTests.cs
AuthTestHelpers.cs
ListingsEndpointTests.cs
ListingImagesEndpointTests.cs
ListingTestHelpers.cs
```

Current tests cover:

```text
Register valid user returns 201
Duplicate register returns 409
Password is hashed, not plain text
Invalid email returns 400
Short password returns 400
Login valid credentials returns 200 and access token
Wrong password returns 401
Unknown email returns 401
POST listing without token returns 401
POST listing with token returns 201
Created listing stores CreatedByUserId
Free listing limit blocks 4th listing for same user
Free listing limit is per user, not global
GET my listings without token returns 401
GET my listings returns only current user's listings
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
Upload listing image without token returns 401
Delete image without token returns 401
Set primary image without token returns 401
Reorder images without token returns 401
Wrong user cannot upload image and gets 403
Wrong user cannot delete image and gets 403
Wrong user cannot set primary image and gets 403
Wrong user cannot reorder images and gets 403
Owner can upload listing image and gets 201
First uploaded image becomes primary
Delete image returns 204 for owner
Deleting primary image makes next image primary
Set primary image works for owner
Only one image remains primary
PrimaryImageUrl points to selected primary image
Reorder listing images works for owner
```

Latest known status:

```text
dotnet test passed
Current count: 41/41
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
Users table
Register user
Login user
Password hashing
JWT auth
Swagger bearer auth
Protected listing creation
Listing ownership with CreatedByUserId
My listings endpoint
Owner authorization for listing image actions
Free listing limit: 3 listings per user
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

### Task 5A — User accounts foundation

Added:

```text
User entity
Users table
UserRole enum
UserStatus enum
UserConfiguration
DbSet<User>
normalized email unique index
```

Migration:

```text
AddUsersTable
```

### Task 5B — Register user + password hashing

Added:

```text
POST /api/auth/register
RegisterRequest
AuthResponse
AuthUserResponse
IUserRepository
UserRepository
IPasswordHasher
PasswordHasherService
RegisterUserHandler
Register integration tests
```

### Task 5C — Login user + password verification

Added:

```text
POST /api/auth/login
LoginRequest
LoginResponse
LoginUserHandler
Password verification
Login integration tests
```

### Task 5D — JWT authentication foundation

Added:

```text
JwtOptions
IJwtTokenGenerator
JwtTokenGenerator
JWT bearer setup in Program.cs
Swagger bearer auth setup
Login returns accessToken
```

### Task 5E — Protected listing creation + listing ownership

Added:

```text
CreatedByUserId on Listing
AssignCreator method
CreatedByUserId EF config/index/FK
ICurrentUserService
CurrentUserService
[Authorize] on POST /api/listings
CreateListingHandler assigns creator from JWT user id
Integration tests
```

Migration:

```text
AddListingCreatedByUserId
```

### Task 5F — My listings endpoint

Added:

```text
GET /api/listings/my
GetMyListingsQuery
GetMyListingsHandler
IListingRepository.GetByCreatedByUserIdAsync
[Authorize] on my listings endpoint
Integration tests for unauthorized and current-user-only listing results
```

### Task 5G — Owner authorization for listing image actions

Added owner checks for:

```text
POST /api/listings/{listingId}/images
DELETE /api/listings/{listingId}/images/{imageId}
PUT /api/listings/{listingId}/images/{imageId}/primary
PUT /api/listings/{listingId}/images/order
```

Added:

```text
[Authorize] on all image mutation endpoints
NotListingOwner error values
ICurrentUserService injection in image handlers
CreatedByUserId ownership checks
403 Forbidden for wrong user
401 Unauthorized for missing token
Integration tests for no-token and wrong-user scenarios
Updated image happy-path tests to authorize as owner
```

### Task 5H — Free listing limit per user

Added:

```text
Max 3 free listings per user
IListingRepository.CountByCreatedByUserIdAsync
ListingRepository.CountByCreatedByUserIdAsync
CreateListingHandler limit check before save
400 Bad Request when user already has 3 listings
Integration tests proving 4th listing is blocked and limit is per user
```

No migration required for this task.

---

## Current backend status

Backend listing/auth/ownership module is ready to move into Agencies.

Current business rules:

```text
Users can register and login.
JWT is used for protected endpoints.
Public users can browse listings.
Authenticated users can create listings.
Each user can create up to 3 listings for free.
Authenticated users can view their own listings.
Only listing owners can manage listing images.
User status/verification is not enforced yet.
Role-based create rules are not enforced yet.
Payments/subscriptions are not implemented yet.
Agencies are not implemented yet.
```

---

## Next planned work

### Task 6A — Agencies foundation

Branch suggestion:

```bash
git checkout development
git pull
git checkout -b feature/agencies-foundation
```

Goal:

```text
Create agency domain/database foundation.
No full agency dashboard yet.
No payments yet.
No CRM yet.
```

Expected first agency tables:

```text
Agencies
AgencyMembers
```

Possible Agency fields:

```text
Id
Name
Slug
Description
LogoUrl
PhoneNumber
Email
WebsiteUrl
AddressLine
City
Municipality
Status
CreatedAtUtc
ModifiedAtUtc
```

Possible AgencyMember fields:

```text
Id
AgencyId
UserId
Role
Status
CreatedAtUtc
ModifiedAtUtc
```

Important future relationship:

```text
Users can belong to agencies through AgencyMembers.
Listings later can belong to an individual user, an agency, or both depending on final business rules.
```

Do not overbuild in Task 6A.

Recommended split:

```text
Task 6A — Agencies table/domain/migration
Task 6B — Agency members
Task 6C — Agency listing ownership rules
Task 6D — Agency endpoints/dashboard basics
Task 7 — CRM clients/notes/saved listings
```

---

## Remaining backend ideas for later

Do not mix these into Task 6A:

```text
Payments/subscriptions
Listing boosts
Admin moderation
Public agency pages
Advanced agent profiles
CRM clients
Client notes
Saved listings
Favorites
Saved searches
Map search
Comparable listings
Average price analytics
AI document analyzer
Voice note helper
Notifications
Email verification
Password reset
Refresh tokens
OAuth / Sign in with Google
```
