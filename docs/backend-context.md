# RealEstate Backend Context

## Project purpose

This backend is for a real estate platform. The goal is not just a basic listing website. The long-term direction is a modern real estate intelligence platform with listings, search, filters, comparisons, price insights, agent tools, agencies, CRM features, and AI-assisted workflows.

Current focus: backend foundation before frontend work.

Current backend status: **listing/auth/ownership + Agencies MVP foundation are implemented.**

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
Agencies
Agency members
Agency-owned listings
Agency membership permissions
Public agency profiles
Agency dashboard basics
Swagger testing
Unit tests
Integration tests
```

Current test status:

```text
115/115 tests passing
```

Next backend direction:

```text
Chapter 7 — Backend cleanup and structure hardening
Chapter 8 — Publishing, visibility, and verification rules
Later Agency Phase 2
Later CRM/client notes/saved listings
Later payments/subscriptions
Later AI-assisted workflows
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
xUnit unit/integration tests
FluentAssertions
Microsoft.AspNetCore.Mvc.Testing
Testcontainers PostgreSQL
```

Tests use a real temporary PostgreSQL container, not EF in-memory provider.

---

## Solution structure

Important: repositories are directly under `RealEstate.Infrastructure/Persistence/Repositories`. There is **no** nested `Listings` folder under repositories.

Current important structure:

```text
src/
  RealEstate.Api
    Authentication
      CurrentUserService.cs
    Controllers
      AgenciesController.cs
      AuthController.cs
      HealthController.cs
      ListingsController.cs
    Program.cs

  RealEstate.Application
    Agencies
      Commands
        CreateAgency
        UpdateAgency
      Dtos
      Mappings
      Queries
        GetAgencyById
        GetAgencyBySlug
        GetAgencyListings
        GetAgencyMembers
        GetMyAgencies
      ReadModels
      Repositories
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
      ServiceResult.cs
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
    Users
      Repositories

  RealEstate.Domain
    Common
      IAuditableEntity.cs
    Entities
      Agency.cs
      AgencyMember.cs
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
        AgencyConfiguration.cs
        AgencyMemberConfiguration.cs
        UserConfiguration.cs
        ListingConfiguration.cs
        ListingTranslationConfiguration.cs
        ListingImageConfiguration.cs
        ListingApartmentDetailsConfiguration.cs
        ListingHouseDetailsConfiguration.cs
      Migrations
      Repositories
        AgencyRepository.cs
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
      Agencies
        AgenciesEndpointTests.cs
        AgencyPersistenceTests.cs
        AgencyTestHelpers.cs
      Auth
        AuthEndpointTests.cs
        AuthTestHelpers.cs
      Listings
        ListingsEndpointTests.cs
        ListingImagesEndpointTests.cs
        ListingTestHelpers.cs
    Unit
      Application
        Listings
          CreateListingValidatorTests.cs
          ListingMappingExtensionsTests.cs
      Domain
        Entities
          AgencyTests.cs
          ListingTests.cs
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
If AgencyId is provided:
    IAgencyRepository.ExistsAsync checks agency exists
    IAgencyRepository.IsActiveMemberAsync checks active agency membership
  ↓
Listing aggregate root created
  ↓
CreatedByUserId assigned
  ↓
AgencyId assigned only when allowed
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

Example agency update flow:

```text
PUT /api/agencies/{id}
  ↓
[Authorize]
  ↓
AgenciesController.UpdateAgency
  ↓
UpdateAgencyHandler
  ↓
UpdateAgencyValidator
  ↓
ICurrentUserService gets logged-in user id
  ↓
IAgencyRepository.GetByIdForUpdateAsync fetches tracked agency
  ↓
IAgencyRepository.GetMemberAccessReadOnlyAsync returns Role + Status
  ↓
Handler checks Active + Owner
  ↓
Agency.UpdateProfile updates allowed fields
  ↓
IAgencyRepository.SaveChangesAsync
  ↓
AgencyResponse returned
```

Rules:

```text
Controllers stay thin.
Handlers contain use-case/application logic.
Domain contains entities, enums, and core business rules.
Infrastructure contains EF Core, repositories, database config, migrations, security, and local storage.
Application owns repository interfaces.
Infrastructure implements repository interfaces.
Repositories are data-focused.
Business/authorization decisions stay in handlers, domain methods, or future policy services.
No MediatR yet.
No AutoMapper yet.
No FluentValidation package yet.
No generic repository / Unit of Work yet.
```

---

## Important architecture decisions

### Aggregate rule

`Listing` is the aggregate root for listings.

Child entities:

```text
ListingTranslation
ListingImage
ListingApartmentDetails
ListingHouseDetails
```

`Agency` is the aggregate root for agency profile/membership setup.

Child/member entity:

```text
AgencyMember
```

Current DbContext rule:

```text
Expose aggregate roots publicly.
Do not expose child entities as public DbSets unless needed.
Child entities are accessed through aggregate navigation properties or internal Set<TEntity>() usage.
```

Current known public DbSets:

```csharp
public DbSet<Listing> Listings => Set<Listing>();
public DbSet<User> Users => Set<User>();
public DbSet<Agency> Agencies => Set<Agency>();
```

Repository may use `_dbContext.Set<ListingImage>()` or `_dbContext.Set<AgencyMember>()` internally when needed.

---

### ReadModels convention

Use feature-level `ReadModels` folders for internal query/database projection shapes.

Rule:

```text
Dtos       = API request/response shapes
ReadModels = query/database projection shapes
Entities   = domain/business objects
```

Current agency read models include:

```text
Agencies/ReadModels/UserAgencyMembershipReadModel.cs
Agencies/ReadModels/AgencyMemberReadModel.cs
Agencies/ReadModels/AgencyMemberAccessReadModel.cs
```

Do not create empty `ReadModels` folders in other features until needed.

---

### Repository watch-outs

Repositories should stay data-focused.

Good repository responsibilities:

```text
create agency
get agency by id
get agency by slug
check agency exists
get member access data
get agency members read model
get filtered listings
save changes
```

Avoid putting business decisions inside repositories.

Do not hide rules like these in repositories:

```text
can user manage agency?
can member update agency?
can member manage listing?
can agency publish listing?
does subscription allow this action?
```

Business/authorization decisions should stay in handlers, domain methods, or future policy services.

Known watch-list:

```text
ListingRepository.GetFilteredReadOnlyAsync(GetListingsQuery query, ...)
```

This is acceptable for now because it applies database filters, pagination, includes, and ordering. But it is growing. Future cleanup may introduce a `ListingSearchCriteria` or query helper/specification if it becomes too large.

Do not add business visibility/subscription/permission rules inside this repository method.

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
Any authenticated user can create listings/agencies until stricter verification rules are added.
```

---

### Agency

Entity:

```text
Agency
```

Table:

```text
Agencies
```

Fields:

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

Agency statuses:

```text
PendingVerification
Active
Disabled
Rejected
```

Current behavior:

```text
Created agencies start as PendingVerification.
PendingVerification agencies are publicly readable for now.
Agency verification/admin approval is not implemented yet.
Slug is set on create and is not updateable through the update profile endpoint.
```

Important methods:

```text
AddMember(...)
UpdateProfile(...)
```

---

### AgencyMember

Entity:

```text
AgencyMember
```

Table:

```text
AgencyMembers
```

Fields:

```text
Id
AgencyId
UserId
Role
Status
CreatedAtUtc
ModifiedAtUtc
```

Agency member roles:

```text
Owner
Agent
```

Agency member statuses:

```text
Active
Pending
Disabled
```

Current membership rules:

```text
Agency creator becomes Owner with Active status.
A user cannot be added twice to the same agency.
Agency actions must check AgencyMember.Status == Active.
Updating agency profile requires Active Owner.
Reading agency members requires active agency membership.
Creating agency listings requires active agency membership.
```

---

### Listing

Entity:

```text
Listing
```

Table:

```text
Listings
```

Common listing fields:

```text
Id
CreatedByUserId
AgencyId
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
AgencyId links listing to an agency when the listing is agency-owned.
Floor and TotalFloors were removed from Listing.
Floor and TotalFloors belong only to ListingApartmentDetails.
```

Relationship:

```text
User 1 → many Listings
Agency 1 → many Listings
Listing.CreatedByUserId is nullable in database for compatibility with older/dev data, but new authenticated listings assign it.
Listing.AgencyId is nullable. Null means personal listing.
```

Ownership shapes:

```text
Personal listing:
  CreatedByUserId = listing creator/owner
  AgencyId = null

Agency listing:
  CreatedByUserId = user who created the listing
  AgencyId = agency that owns/groups the listing
```

Important current rule:

```text
Same-agency membership does not automatically give full management rights over another member’s listing yet.
For MVP, listing/image management still follows creator ownership unless explicitly extended later.
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

Agency/user enums:

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

AgencyStatus
  PendingVerification
  Active
  Disabled
  Rejected

AgencyMemberRole
  Owner
  Agent

AgencyMemberStatus
  Active
  Pending
  Disabled
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
Only the listing creator/owner can upload/delete/set primary/reorder images.
Same-agency members cannot manage another member's listing images yet.
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

Register behavior:

```text
Creates user.
Normalizes email.
Blocks duplicate normalized email.
Hashes password using ASP.NET Core PasswordHasher.
Does not return JWT token.
Creates user with status PendingVerification.
```

### Login

```http
POST /api/auth/login
```

Login behavior:

```text
Wrong password returns 401.
Unknown email returns 401.
Both use generic invalid credentials behavior.
Returns JWT accessToken on success.
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

### Auth endpoints

```http
POST /api/auth/register
POST /api/auth/login
```

---

### Listing endpoints

#### Create listing

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
Can create agency listing only if AgencyId is provided and current user is active agency member.
```

Agency listing behavior:

```text
Missing agency -> 404
Existing agency but user is not active member -> 403
Existing agency and active member -> listing created with AgencyId
```

Free listing limit message:

```text
Free listing limit reached. Each user can create up to 3 listings.
```

Current watch-out:

```text
Free listing limit currently counts by CreatedByUserId.
Agency listings still count against the creating user's free limit.
```

#### Get paginated / filtered listings

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
agencyId
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

#### Get my listings

```http
GET /api/listings/my?lang=mk&page=1&pageSize=20
```

Auth:

```text
Requires JWT.
Returns only listings where CreatedByUserId equals logged-in user id.
Returns 401 without token.
```

#### Get listing by ID

```http
GET /api/listings/{id}?lang=en
GET /api/listings/{id}?lang=mk
```

Auth:

```text
Public.
```

Missing listing returns:

```http
404 Not Found
```

`PricePerSquareMeter` is rounded to 2 decimals in response mapping.

---

### Image endpoints

All image endpoints require JWT and listing owner/creator.

Expected authorization behavior:

```text
No token      -> 401 Unauthorized
Wrong user    -> 403 Forbidden
Listing owner -> success
```

Endpoints:

```http
POST /api/listings/{listingId}/images
DELETE /api/listings/{listingId}/images/{imageId}
PUT /api/listings/{listingId}/images/{imageId}/primary
PUT /api/listings/{listingId}/images/order
```

---

### Agency endpoints

#### Create agency

```http
POST /api/agencies
```

Auth:

```text
Requires JWT.
```

Behavior:

```text
Creates agency.
Creator automatically becomes Owner member.
Duplicate slug returns 400.
Created agency starts as PendingVerification.
```

Returns:

```http
201 Created
400 Bad Request
401 Unauthorized
```

#### Get public agency profile by id

```http
GET /api/agencies/{id}
```

Auth:

```text
Public.
```

Behavior:

```text
Returns public agency profile.
Missing agency returns 404.
```

#### Get public agency profile by slug

```http
GET /api/agencies/by-slug/{slug}
```

Auth:

```text
Public.
```

Behavior:

```text
Returns public agency profile by slug.
Slug is normalized to lowercase by handler.
Missing agency returns 404.
Used by public frontend URLs like /agencies/dom-real-estate.
```

#### Get my agencies

```http
GET /api/agencies/my
```

Auth:

```text
Requires JWT.
```

Behavior:

```text
Returns agencies the current user belongs to.
Returns membership role/status.
Returns empty array if user has no agencies.
Does not filter only active memberships.
Action endpoints still enforce Active status separately.
```

#### Get agency members

```http
GET /api/agencies/{id}/members
```

Auth:

```text
Requires JWT.
```

Behavior:

```text
Missing agency -> 404
Current user is not active member -> 403
Current user is active member -> 200 with members
Disabled/Pending members cannot read members.
```

For MVP, any active agency member can read members.

Owner-only rules are reserved for mutation endpoints.

#### Get agency listings

```http
GET /api/agencies/{id}/listings?lang=en&page=1&pageSize=20
```

Auth:

```text
Public.
```

Behavior:

```text
Missing agency -> 404
Existing agency with no listings -> 200 OK empty paged result
Existing agency with listings -> 200 OK paged agency listings
```

This endpoint reuses listing query/filtering logic through `IListingRepository`.

#### Update agency profile

```http
PUT /api/agencies/{id}
```

Auth:

```text
Requires JWT.
```

Behavior:

```text
Missing agency -> 404
No token -> 401
Non-member -> 403
Active Agent -> 403
Disabled Owner -> 403
Active Owner -> 200 and updates profile
```

Allowed update fields:

```text
Name
Description
PhoneNumber
Email
WebsiteUrl
AddressLine
City
Municipality
```

Not updateable here:

```text
Slug
Status
LogoUrl
Members
Roles
Verification
```

---

## Current database tables

```text
Users
Agencies
AgencyMembers
Listings
ListingTranslations
ListingImages
ListingApartmentDetails
ListingHouseDetails
__EFMigrationsHistory
```

Important columns in `Agencies`:

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

Important columns in `AgencyMembers`:

```text
Id
AgencyId
UserId
Role
Status
CreatedAtUtc
ModifiedAtUtc
```

Important columns in `Listings`:

```text
Id
CreatedByUserId
AgencyId
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
Agency
AgencyMember
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
run API/unit tests
  ↓
delete container
```

Current test files:

```text
Integration/Auth/AuthEndpointTests.cs
Integration/Auth/AuthTestHelpers.cs
Integration/Listings/ListingsEndpointTests.cs
Integration/Listings/ListingImagesEndpointTests.cs
Integration/Listings/ListingTestHelpers.cs
Integration/Agencies/AgenciesEndpointTests.cs
Integration/Agencies/AgencyPersistenceTests.cs
Integration/Agencies/AgencyTestHelpers.cs
Unit/Application/Listings/CreateListingValidatorTests.cs
Unit/Application/Listings/ListingMappingExtensionsTests.cs
Unit/Domain/Entities/AgencyTests.cs
Unit/Domain/Entities/ListingTests.cs
```

Latest known status:

```text
dotnet test passed
Current count: 115/115
```

Important testing policy:

```text
Do not chase fake 100% unit coverage.
Add unit tests when there is real domain, validation, mapping, or permission logic.
Add integration tests for important API behavior and permission boundaries.
When touching old logic, check if a test exists and add one if the behavior is important.
```

Known test structure issue:

```text
AgenciesEndpointTests.cs is now large.
ListingsEndpointTests.cs and ListingImagesEndpointTests.cs are also large.
Next cleanup chapter should split huge integration test files into focused test classes.
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

Recommended final check before commit:

```bash
dotnet build
dotnet test
dotnet format
dotnet test
git status
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
Core unit tests
Agencies foundation
Agency members foundation
Listing agency ownership foundation
Agency members can create agency listings
Agency listing query support
Agency listing ownership rules locked
Create agency endpoint
Public agency profile by id
My agencies endpoint
Agency members read endpoint
Public agency profile by slug
Public agency listings endpoint
Update agency profile endpoint
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

### Task 6A — Agencies foundation

Added:

```text
Agency entity
AgencyStatus enum
AgencyConfiguration
DbSet<Agency>
Agencies table
unique slug index
```

Migration:

```text
AddAgenciesTable
```

### Task 6B — Agency members foundation

Added:

```text
AgencyMember entity
AgencyMemberRole enum
AgencyMemberStatus enum
Agency.Members collection
Agency.AddMember(...)
AgencyMemberConfiguration
Agency members persistence tests
Agency unit tests
```

Migration:

```text
AddAgencyMembersTable
```

### Task 6C — Agency listing ownership rules

Split into smaller parts:

```text
6C-1 Listing agency ownership foundation
6C-2 Agency members can create agency listings
6C-3 Agency listing read/query support
6C-4 Agency listing ownership rule polish
```

Added:

```text
Listing.AgencyId
Listing.AssignAgency(...)
Listing -> Agency relationship
agencyId in create listing request
agency exists check
active agency member check
403 for non-member
404 for missing agency
AgencyId in ListingResponse
agencyId filter on GET /api/listings
tests locking same-agency member cannot manage another member's listing images
```

### Task 6D — Agency endpoints/dashboard basics

Completed MVP endpoints:

```text
6D-1 Create agency endpoint
6D-2 Public agency profile by id
6D-3 My agencies endpoint
6D-4 Agency members read endpoint
6D-5 Public agency profile by slug
6D-6 Public agency listings endpoint
6D-7 Update agency profile endpoint
```

Added:

```text
POST /api/agencies
GET /api/agencies/{id}
GET /api/agencies/my
GET /api/agencies/{id}/members
GET /api/agencies/by-slug/{slug}
GET /api/agencies/{id}/listings
PUT /api/agencies/{id}
ReadModels convention
agency permission tests
owner-only agency update rule
```

---

## Current backend status

Backend listing/auth/ownership + Agencies MVP foundation is complete.

Current business rules:

```text
Users can register and login.
JWT is used for protected endpoints.
Public users can browse listings.
Authenticated users can create listings.
Each user can create up to 3 listings for free.
Authenticated users can view their own listings.
Only listing owners can manage listing images.
Agencies can be created by authenticated users.
Agency creator becomes Active Owner.
Agency listings can only be created by active agency members.
Agency members can be read by active agency members.
Agency profile can only be updated by Active Owner.
Agency public profile can be read by id or slug.
Agency public listings can be read through agency endpoint.
User status/verification is not enforced yet.
Agency PendingVerification status is not enforced for public visibility yet.
Payments/subscriptions are not implemented yet.
```

---

## Current architecture risks / watch-outs

### Ownership and permissions

The backend now has multiple ownership concepts:

```text
CreatedByUserId
AgencyId
AgencyMember.Role
AgencyMember.Status
User.Status
Agency.Status
Listing.Status
```

Future code must be careful not to mix these accidentally.

Before adding any new protected action, ask:

```text
Is this a personal listing?
Is this an agency listing?
Is the current user the creator?
Is the current user an active agency member?
Does role matter? Owner vs Agent?
Is the user PendingVerification?
Is the agency PendingVerification?
Is the listing Draft/Active/Archived?
```

### Verification

Current known limitations:

```text
New users can be PendingVerification.
Agencies can be PendingVerification.
PendingVerification users are not fully blocked yet.
PendingVerification agencies are publicly readable for now.
Publishing/visibility rules are not fully designed yet.
```

Future decisions needed:

```text
Can PendingVerification users create listings?
Can PendingVerification users create agencies?
Can PendingVerification agencies publish listings?
Should public agency pages show pending agencies?
Should public listings require agency verification?
Can Draft listings be publicly visible?
Who can publish/unpublish/archive listings?
```

### Free listing limit

Current behavior:

```text
Free listing limit counts by CreatedByUserId.
Agency listings still count against the creating user's free limit.
```

Future decision needed:

```text
Personal listings may count against user free limit.
Agency listings may count against agency subscription/plan.
Or both may apply depending on payment model.
```

Do not change this casually without explicit payment/subscription rules.

### Large files

Files getting large:

```text
AgenciesEndpointTests.cs
ListingsEndpointTests.cs
ListingImagesEndpointTests.cs
ListingRepository.cs
```

Next cleanup chapter should reduce test file size and review repository structure before adding another big product area.

---

## Next planned work

### Chapter 7 — Backend cleanup and structure hardening

Goal:

```text
Clean the backend structure before adding another major product chapter.
Prevent huge files and hidden architecture debt.
Do not change business behavior unless tests reveal bugs.
```

Recommended tasks:

```text
7A — Split huge agency integration tests into focused files
7B — Split huge listing integration tests into focused files
7C — Improve shared test helpers only where reuse is real
7D — Review ListingRepository filtering method and decide if query helper/specification is needed
7E — Review AgencyRepository size after agency endpoints
7F — Run full test/format pass and update backend context
```

Important cleanup rule:

```text
Refactor structure, not behavior.
Tests should stay green after each small cleanup task.
Do not combine cleanup with new product features.
```

### Chapter 8 — Publishing, visibility, and verification rules

Recommended next product chapter after cleanup.

Goal:

```text
Define what is public, what is draft, who can publish, and how user/agency verification affects visibility.
```

Likely tasks:

```text
Listing publish/unpublish/archive endpoint
Public listing visibility rules
Draft vs Active behavior
PendingVerification user restrictions
PendingVerification agency restrictions
Agency listing publish rules
Admin/verification decisions
Tests for public/private visibility boundaries
```

---

## Future agency Phase 2 tasks

Not part of Agencies MVP foundation:

```text
Invite agency member
Accept invitation
Remove/disable member
Change member role
Agency verification/admin approval
Agency subscription/payment limits
Agency logo upload
Slug update with redirect/history strategy
Owner transfer
Richer agency dashboard metrics
```

---

## Remaining backend ideas for later

Do not mix these into cleanup or visibility tasks unless intentionally starting that chapter:

```text
Payments/subscriptions
Listing boosts
Admin moderation
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
