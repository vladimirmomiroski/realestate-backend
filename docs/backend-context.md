# RealEstate Backend Context

## Documentation policy

For any chapter that touches sensitive business rules, permissions, visibility, verification, payments, subscriptions, security, or public data exposure, create a dedicated `docs/chapters/*.md` rules document before implementation starts.

`backend-context.md` remains the compressed AI handoff/context file. Detailed rule documents are created only when the rules are important enough to affect future architecture or permissions.

### AI implementation context policy

When implementation or planning depends on existing project structure, repository methods, EF Core mappings, test helpers, DI registration, controller patterns, database schema, or permission/security logic, ask for the exact relevant files before giving final implementation code.

This is especially required for:

```text
integration tests
repository changes
EF Core queries/mappings
direct database setup in tests
controller/handler wiring
permission/security logic
code that depends on existing helper methods or naming conventions
```

Rule:

```text
Do not guess project-specific file structure or helper names when exact files are needed.
Ask for the relevant files first, then provide compile-safe code.
```

## Project purpose

This backend is for a real estate platform. The goal is not just a basic listing website. The long-term direction is a modern real estate intelligence platform with listings, search, filters, comparisons, price insights, agent tools, agencies, CRM features, and AI-assisted workflows.

Current focus: backend foundation before frontend work.

Current backend status: **listing/auth/ownership + user profile/account basics + Agencies MVP + Chapter 8 publishing/visibility system are implemented.**

Chapter 7 backend cleanup and structure hardening is complete.

Chapter 8 publishing, visibility, verification restrictions, and agency listing dashboard visibility are complete.

Chapter 8.5 user profile/account basics are complete.

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
Public agency listings
Agency dashboard listings
Publish listing
Unpublish listing
Archive listing
Public Active-only listing visibility
Private personal dashboard listing visibility
Private agency dashboard listing visibility
Agency listing access checker
Swagger testing
Unit tests
Integration tests
Cleaner test structure
Cleaner listing repository query structure
Current user profile endpoint
Current user profile update endpoint
User avatar upload/replace endpoint
User avatar delete endpoint
User avatar fields and storage
```

Current test status:

```text
230/230 tests passing
```

Next backend direction:

```text
Chapter 8.5 is closed.
Locked roadmap:
Chapter 9 — Agency Phase 2
Chapter 9.5 — Frontend readiness
Then switch to frontend
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
      UsersController.cs
    Program.cs

  RealEstate.Application
    Agencies
      Commands
        CreateAgency
        UpdateAgency
      Dtos
      Mappings
      Permissions
        AgencyListingAccessChecker.cs
      Queries
        GetAgencyById
        GetAgencyBySlug
        GetAgencyListings
        GetAgencyDashboardListings
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
        PublishListing
        UnpublishListing
        ArchiveListing
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
      Commands
        UpdateCurrentUserProfile
        UploadCurrentUserAvatar
        DeleteCurrentUserAvatar
      Dtos
      Mappings
      Queries
        GetCurrentUser
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
        AgenciesEndpointTests.Setup.cs
        AgenciesEndpointTests.Create.cs
        AgenciesEndpointTests.GetById.cs
        AgenciesEndpointTests.GetBySlug.cs
        AgenciesEndpointTests.MyAgencies.cs
        AgenciesEndpointTests.Members.cs
        AgenciesEndpointTests.Listings.cs
        AgenciesEndpointTests.DashboardListings.cs
        AgenciesEndpointTests.UpdateProfile.cs
        AgencyPersistenceTests.cs
        AgencyTestHelpers.cs
      Auth
        AuthEndpointTests.cs
        AuthTestHelpers.cs
      Listings
        ListingsEndpointTests.Setup.cs
        ListingsEndpointTests.Create.cs
        ListingsEndpointTests.AgencyOwnership.cs
        ListingsEndpointTests.GetAll.cs
        ListingsEndpointTests.Filters.cs
        ListingsEndpointTests.GetById.cs
        ListingsEndpointTests.MyListings.cs
        ListingsEndpointTests.Publishing.cs
        ListingsEndpointTests.Unpublishing.cs
        ListingsEndpointTests.Archiving.cs
        ListingsEndpointTests.PublicVisibility.cs
        ListingImagesEndpointTests.Setup.cs
        ListingImagesEndpointTests.Upload.cs
        ListingImagesEndpointTests.Delete.cs
        ListingImagesEndpointTests.SetPrimary.cs
        ListingImagesEndpointTests.Reorder.cs
        ListingImagesEndpointTests.Authorization.cs
        ListingPersistenceTests.cs
        ListingTestHelpers.cs
      Users
        UsersEndpointTests.Setup.cs
        UsersEndpointTests.GetMe.cs
        UsersEndpointTests.UpdateProfile.cs
        UsersEndpointTests.Avatar.cs
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
public DbSet<User> Users => Set<User>();
public DbSet<Listing> Listings => Set<Listing>();
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

Current repository cleanup status:

```text
ListingRepository.GetFilteredReadOnlyAsync was cleaned in Chapter 7.
ListingRepository.GetFilteredReadOnlyAsync now applies public Active-only visibility before count/pagination.
ListingRepository.GetByAgencyIdForDashboardReadOnlyAsync supports private agency dashboard listing queries without public Active-only filtering.
AgencyRepository was reviewed in Chapter 7 and left unchanged because it is still readable and data-focused.
AgencyListingAccessChecker was added in Chapter 8 to keep repeated agency listing permission rules out of handlers and repositories.
```

ListingRepository currently uses private helpers for query structure:

```text
ApplyBasicFilters
ApplyPropertyDetailFilters
ApplyLocationFilters
ApplyListingIncludes
NormalizePagination
```

Future cleanup may introduce a `ListingSearchCriteria`, `ListingQueryBuilder`, or specification/query helper only if search/filtering grows much larger.

Do not add business visibility/subscription/permission rules inside listing repository filtering methods.

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
AvatarUrl
AvatarStoredFileName
AvatarContentType
AvatarSizeBytes
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
PendingVerification users can create draft listings/agencies for now.
PendingVerification users cannot publish listings.
Disabled users cannot publish/unpublish/archive listings or view agency dashboard listings.
GET /api/users/me returns current user profile for Active, PendingVerification, and Disabled users.
PendingVerification users can update profile and upload/delete avatar.
Disabled users cannot update profile or upload/delete avatar.
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
Only Active agencies can publish agency listings.
Agency status does not block unpublish/archive/dashboard listing management.
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

Important current rules:

```text
Public listing APIs show only Active listings.
Draft and Archived listings are hidden publicly.
Public GET by id returns 404 for non-Active listings.
Personal dashboard shows own Draft/Active/Archived listings.
Agency dashboard listings endpoint shows agency Draft/Active/Archived listings to active Owner/Agent members.
Same-agency members can publish/unpublish/archive agency listings when active Owner/Agent.
Same-agency members still cannot manage another member's listing images yet.
For MVP, image management still follows creator ownership unless explicitly extended later.
```

Listing status methods:

```text
Publish()
Unpublish()
Archive()
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

Current image cleanup watch-outs:

```text
Image handlers are acceptable for MVP.
Owner checks are repeated but still simple.
Future cleanup may introduce a shared listing ownership guard if image/listing protected actions grow.
File cleanup after failed database save may be improved later.
ListingsController may later be split into ListingsController + ListingImagesController if image endpoints grow.
```

---

## User avatars

User avatar fields are stored directly on `User`.

Fields:

```text
AvatarUrl
AvatarStoredFileName
AvatarContentType
AvatarSizeBytes
```

Avatar storage:

```text
Local filesystem
src/RealEstate.Api/wwwroot/uploads/users/{userId}/avatar/{storedFileName}
```

Public URL:

```text
/uploads/users/{userId}/avatar/{storedFileName}
```

Ignored by Git:

```text
src/RealEstate.Api/wwwroot/uploads/
```

Avatar validation:

```text
Max size: 5 MB
Allowed extensions: .jpg, .jpeg, .png, .webp
Allowed content types: image/jpeg, image/png, image/webp
```

Avatar rules:

```text
PUT /api/users/me/avatar is used for both first upload and replacement.
DELETE /api/users/me/avatar is idempotent.
Active and PendingVerification users can upload/replace/delete avatar.
Disabled users cannot upload/replace/delete avatar.
No separate UserAvatar table exists.
```

Implementation detail:

```text
Avatar upload stores the new file first, updates User avatar fields, saves database changes, then deletes the old avatar file.
If database save fails after file storage, the newly uploaded file is cleaned up where practical.
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

### User profile/account endpoints

All user profile/account endpoints require JWT.

```http
GET /api/users/me
PUT /api/users/me/profile
PUT /api/users/me/avatar
DELETE /api/users/me/avatar
```

#### Get current user profile

```http
GET /api/users/me
```

Behavior:

```text
Returns current authenticated user profile.
Returns 401 without token.
Active, PendingVerification, and Disabled users can read their own profile.
Returns UserProfileResponse.
```

Response includes:

```text
Id
Email
FirstName
LastName
PhoneNumber
Role
Status
AvatarUrl
CreatedAtUtc
ModifiedAtUtc
```

#### Update current user profile

```http
PUT /api/users/me/profile
```

Editable fields:

```text
FirstName
LastName
PhoneNumber
```

Behavior:

```text
Returns 401 without token.
Active users can update profile.
PendingVerification users can update profile.
Disabled users return 403.
FirstName and LastName are required strings.
PhoneNumber is optional.
Read-only fields such as Email, Role, Status, PasswordHash, and avatar fields cannot be changed through this endpoint.
```

#### Upload/replace current user avatar

```http
PUT /api/users/me/avatar
```

Behavior:

```text
Used for both first avatar upload and avatar replacement.
Returns updated UserProfileResponse.
Active users can upload/replace avatar.
PendingVerification users can upload/replace avatar.
Disabled users return 403.
Missing, empty, invalid type, invalid extension, or too-large files return 400.
Old avatar file is deleted only after the new avatar is successfully saved.
```

Validation:

```text
Max size: 5 MB
Allowed extensions: .jpg, .jpeg, .png, .webp
Allowed content types: image/jpeg, image/png, image/webp
```

#### Delete current user avatar

```http
DELETE /api/users/me/avatar
```

Behavior:

```text
Returns 204 No Content on success.
Delete is idempotent.
Active users can delete avatar.
PendingVerification users can delete avatar.
Disabled users return 403.
Avatar fields are cleared from Users table.
Stored avatar file is deleted when it exists.
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
Created listings start as Draft.
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

Visibility:

```text
Returns only Active listings.
Draft/Archived/non-Active listings are hidden.
Active visibility filter is applied before totalCount and pagination.
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
Returns Draft, Active, and Archived owned listings.
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

Visibility:

```text
Active listing -> 200 OK
Draft/Archived/non-Active listing -> 404 Not Found
Missing listing -> 404 Not Found
```

Reason:

```text
Public users should not know whether hidden listings exist.
```

`PricePerSquareMeter` is rounded to 2 decimals in response mapping.

#### Publish listing

```http
PUT /api/listings/{id}/publish
```

Auth:

```text
Requires JWT.
```

Rules:

```text
Personal listing: owner + User.Status Active.
Agency listing: User.Status Active + Agency.Status Active + active agency member Owner/Agent.
Draft -> Active.
Active -> Active idempotent OK.
Archived -> 400 Bad Request.
```

#### Unpublish listing

```http
PUT /api/listings/{id}/unpublish
```

Auth:

```text
Requires JWT.
```

Rules:

```text
Personal listing: owner and user not Disabled.
Agency listing: user not Disabled + active agency member Owner/Agent.
Agency status does not block unpublish.
Active -> Draft.
Draft -> Draft idempotent OK.
Archived -> 400 Bad Request.
```

#### Archive listing

```http
PUT /api/listings/{id}/archive
```

Auth:

```text
Requires JWT.
```

Rules:

```text
Personal listing: owner and user not Disabled.
Agency listing: user not Disabled + active agency member Owner/Agent.
Agency status does not block archive.
Draft -> Archived.
Active -> Archived.
Archived -> Archived idempotent OK.
Reserved/Sold/Rented -> 400 Bad Request.
```

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
Chapter 8 did not change public agency profile visibility.
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
Chapter 8 did not change public agency profile visibility.
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

#### Get public agency listings

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
Existing agency with no Active listings -> 200 OK empty paged result
Existing agency with Active listings -> 200 OK paged Active agency listings
Draft/Archived/non-Active agency listings are hidden publicly
```

This endpoint reuses public listing query/filtering logic through `IListingRepository`.

#### Get agency dashboard listings

```http
GET /api/agencies/{id}/dashboard/listings?lang=en&status=Draft&page=1&pageSize=20
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
Pending/Disabled member -> 403
Disabled user -> 403
Active Owner -> 200
Active Agent -> 200
```

Visibility:

```text
Private agency dashboard endpoint.
Returns Draft, Active, and Archived agency listings.
Supports optional status filter.
Does not require Agency.Status Active.
```

Reason:

```text
Active agency members may need to manage/hide old listings even if the agency is PendingVerification, Disabled, or Rejected.
```

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
AvatarUrl
AvatarStoredFileName
AvatarContentType
AvatarSizeBytes
CreatedAtUtc
ModifiedAtUtc
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

Integration/Agencies/AgenciesEndpointTests.Setup.cs
Integration/Agencies/AgenciesEndpointTests.Create.cs
Integration/Agencies/AgenciesEndpointTests.GetById.cs
Integration/Agencies/AgenciesEndpointTests.GetBySlug.cs
Integration/Agencies/AgenciesEndpointTests.MyAgencies.cs
Integration/Agencies/AgenciesEndpointTests.Members.cs
Integration/Agencies/AgenciesEndpointTests.Listings.cs
Integration/Agencies/AgenciesEndpointTests.UpdateProfile.cs
Integration/Agencies/AgencyPersistenceTests.cs
Integration/Agencies/AgencyTestHelpers.cs

Integration/Listings/ListingsEndpointTests.Setup.cs
Integration/Listings/ListingsEndpointTests.Create.cs
Integration/Listings/ListingsEndpointTests.AgencyOwnership.cs
Integration/Listings/ListingsEndpointTests.GetAll.cs
Integration/Listings/ListingsEndpointTests.Filters.cs
Integration/Listings/ListingsEndpointTests.GetById.cs
Integration/Listings/ListingsEndpointTests.MyListings.cs
Integration/Listings/ListingImagesEndpointTests.Setup.cs
Integration/Listings/ListingImagesEndpointTests.Upload.cs
Integration/Listings/ListingImagesEndpointTests.Delete.cs
Integration/Listings/ListingImagesEndpointTests.SetPrimary.cs
Integration/Listings/ListingImagesEndpointTests.Reorder.cs
Integration/Listings/ListingImagesEndpointTests.Authorization.cs
Integration/Listings/ListingPersistenceTests.cs
Integration/Listings/ListingTestHelpers.cs

Unit/Application/Listings/CreateListingValidatorTests.cs
Unit/Application/Listings/ListingMappingExtensionsTests.cs
Unit/Domain/Entities/AgencyTests.cs
Unit/Domain/Entities/ListingTests.cs
```

Latest known status:

```text
dotnet test passed
Current count: 230/230
```

Important testing policy:

```text
Do not chase fake 100% unit coverage.
Add unit tests when there is real domain, validation, mapping, or permission logic.
Add integration tests for important API behavior and permission boundaries.
When touching old logic, check if a test exists and add one if the behavior is important.
```

Current test structure status:

```text
Large agency/listing/listing-image integration test files were split in Chapter 7.
Partial class split was used to keep the same fixture, constructor, fields, and helpers while separating tests by feature.
This avoided duplicate fixture/container setup and kept behavior unchanged.
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
Listing publish endpoint
Listing unpublish endpoint
Listing archive endpoint
Public Active-only listing visibility
Private agency dashboard listings endpoint
Agency listing status filter for dashboard
Agency listing access checker
Integration tests with real PostgreSQL Testcontainers
Frontend CORS support
Backend cleanup and structure hardening
Cleaner integration test structure
Cleaner listing repository query structure
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

### Task 7A — Split agency endpoint tests

Changed:

```text
Split large AgenciesEndpointTests.cs into focused partial class files.
Kept one shared fixture/constructor/setup file.
Moved tests by endpoint/feature.
No behavior changes.
```

Resulting files:

```text
AgenciesEndpointTests.Setup.cs
AgenciesEndpointTests.Create.cs
AgenciesEndpointTests.GetById.cs
AgenciesEndpointTests.GetBySlug.cs
AgenciesEndpointTests.MyAgencies.cs
AgenciesEndpointTests.Members.cs
AgenciesEndpointTests.Listings.cs
AgenciesEndpointTests.UpdateProfile.cs
```

### Task 7B — Split listing endpoint tests

Changed:

```text
Split large ListingsEndpointTests.cs into focused partial class files.
Kept one shared fixture/constructor/setup file.
Moved tests by listing behavior area.
No behavior changes.
```

Resulting files:

```text
ListingsEndpointTests.Setup.cs
ListingsEndpointTests.Create.cs
ListingsEndpointTests.AgencyOwnership.cs
ListingsEndpointTests.GetAll.cs
ListingsEndpointTests.Filters.cs
ListingsEndpointTests.GetById.cs
ListingsEndpointTests.MyListings.cs
```

### Task 7C — Split listing image endpoint tests

Changed:

```text
Split large ListingImagesEndpointTests.cs into focused partial class files.
Kept one shared fixture/constructor/setup file.
Moved image helper methods into setup file.
No behavior changes.
```

Resulting files:

```text
ListingImagesEndpointTests.Setup.cs
ListingImagesEndpointTests.Upload.cs
ListingImagesEndpointTests.Delete.cs
ListingImagesEndpointTests.SetPrimary.cs
ListingImagesEndpointTests.Reorder.cs
ListingImagesEndpointTests.Authorization.cs
```

### Task 7D — Clean listing test helpers

Changed:

```text
Cleaned ListingTestHelpers to remove duplicated listing creation logic.
Extracted shared listing POST/read-id flow into a private helper.
Left AuthTestHelpers and AgencyTestHelpers unchanged because they were already simple.
No behavior changes.
```

### Task 7E — Clean ListingRepository query structure

Changed:

```text
Cleaned ListingRepository.GetFilteredReadOnlyAsync.
Extracted private helper methods for filters, includes, and pagination.
Kept repository as one file.
Did not add specification pattern or query builder yet.
No behavior changes.
```

Current helper structure:

```text
ApplyBasicFilters
ApplyPropertyDetailFilters
ApplyLocationFilters
ApplyListingIncludes
NormalizePagination
```

### Task 7F — Review AgencyRepository

Result:

```text
AgencyRepository reviewed.
No structural change needed.
Repository is still readable and data-focused.
```

### Task 7G — Final cleanup pass and docs update

Completed:

```text
Final project review after cleanup.
Controllers reviewed and left unchanged.
Program.cs reviewed and left unchanged.
RealEstateDbContext reviewed and left unchanged.
Application/Infrastructure DI reviewed and left unchanged.
LocalFileStorageService, CurrentUserService, and JwtTokenGenerator reviewed and left unchanged.
Backend context updated to reflect Chapter 7 completion.
```

### Task 8A — Publishing/visibility rules doc

Completed:

```text
Created dedicated Chapter 8 rules document.
Locked core visibility and permission rules before implementation.
```

### Task 8B — Listing status domain methods

Completed:

```text
Added Listing.Publish().
Added Listing.Unpublish().
Added Listing.Archive().
Added/updated domain unit tests for valid, invalid, and idempotent transitions.
```

### Task 8C — Publish listing endpoint

Completed:

```text
PUT /api/listings/{id}/publish
Personal publish requires owner + Active user.
Agency publish requires Active user + Active agency + active agency member Owner/Agent.
Archived listings cannot be published back to Active.
Tests reached 149/149.
```

### Task 8D — Unpublish listing endpoint

Completed:

```text
PUT /api/listings/{id}/unpublish
Personal unpublish requires owner and blocks Disabled user.
Agency unpublish requires active agency member Owner/Agent.
Agency status does not block unpublish.
Tests reached 165/165.
```

### Task 8E — Archive listing endpoint

Completed:

```text
PUT /api/listings/{id}/archive
Personal archive requires owner and blocks Disabled user.
Agency archive requires active agency member Owner/Agent.
Agency status does not block archive.
Archive is idempotent for already Archived listings.
Tests reached 184/184.
```

### Task 8F — Public listing visibility

Completed:

```text
GET /api/listings filters to Active before count/pagination.
GET /api/listings/{id} returns 404 for non-Active listings.
GET /api/agencies/{id}/listings also exposes only Active listings through public filtering.
GET /api/listings/my still shows Draft/Active/Archived owned listings.
Old public-read tests were updated to activate listings explicitly.
Tests reached 192/192.
```

### Task 8G — Agency dashboard listings endpoint

Completed:

```text
GET /api/agencies/{id}/dashboard/listings
Private endpoint for agency listing management.
Returns Draft/Active/Archived agency listings.
Supports optional ListingStatus filter.
Requires user not Disabled and active agency member Owner/Agent.
Agency status does not block dashboard viewing.
Tests reached 204/204.
```

### Task 8H — Permission cleanup

Completed:

```text
Added AgencyListingAccessChecker.
Centralized repeated agency listing access checks.
Publish still requires Active agency.
Unpublish/archive/dashboard listing access do not require Active agency.
Handlers keep user-status, personal ownership, and action-specific rules.
Tests remained 204/204.
```

### Task 8.5A — User profile/account rules doc

Completed:

```text
Created dedicated Chapter 8.5 rules document.
Locked current-user profile, profile update, avatar upload/replace, avatar delete, user status, storage, and test rules before implementation.
```

### Task 8.5B — User profile/avatar foundation

Completed:

```text
Added User avatar fields.
Added User.UpdateProfile().
Added User.SetAvatar().
Added User.RemoveAvatar().
Added UserProfileResponse.
Added User mapping extension.
Added IUserRepository.GetByIdForUpdateAsync.
Added AddUserAvatarFields migration.
```

Migration:

```text
AddUserAvatarFields
```

### Task 8.5C — Current user profile endpoint

Completed:

```text
GET /api/users/me
Returns current authenticated user profile.
Requires JWT.
Disabled users can still read own profile.
Added UsersController.
Added GetCurrentUserQuery and GetCurrentUserHandler.
Added integration tests.
```

### Task 8.5D — Current user profile update endpoint

Completed:

```text
PUT /api/users/me/profile
Updates FirstName, LastName, and PhoneNumber only.
Requires JWT.
Active and PendingVerification users can update profile.
Disabled users are blocked with 403.
Read-only fields cannot be changed through this endpoint.
Added integration tests.
```

### Task 8.5E — Current user avatar upload/replace endpoint

Completed:

```text
PUT /api/users/me/avatar
Used for both first avatar upload and avatar replacement.
Requires JWT.
Active and PendingVerification users can upload/replace avatar.
Disabled users are blocked with 403.
Validates image size, extension, and content type.
Stores avatars under /uploads/users/{userId}/avatar/{storedFileName}.
Old avatar file is deleted after successful replacement.
Added integration tests.
```

### Task 8.5F — Current user avatar delete endpoint

Completed:

```text
DELETE /api/users/me/avatar
Requires JWT.
Active and PendingVerification users can delete avatar.
Disabled users are blocked with 403.
Delete is idempotent.
Avatar fields are cleared from User.
Stored avatar file is deleted when it exists.
Added integration tests.
```

### Task 8.5G — Docs/context update

Completed:

```text
Updated Chapter 8.5 rules document.
Updated backend-context.md.
Locked next roadmap: Chapter 9, Chapter 9.5, then frontend.
```

---

## Current backend status

Backend listing/auth/ownership + Agencies MVP foundation is complete.

Chapter 7 cleanup and structure hardening is complete.

Chapter 8 publishing, visibility, verification restrictions, and agency dashboard listing visibility are complete.

Chapter 8.5 user profile/account basics are complete.

Current business rules:

```text
Users can register and login.
JWT is used for protected endpoints.
Public users can browse listings.
Public listing APIs show only Active listings.
Draft/Archived/non-Active listings are hidden publicly.
Public GET listing by id returns 404 for hidden/non-Active listings.
Authenticated users can create Draft listings.
Each user can create up to 3 listings for free.
Authenticated users can view their own Draft/Active/Archived listings.
Only listing owners can manage listing images.
Agencies can be created by authenticated users.
Agency creator becomes Active Owner.
Agency listings can only be created by active agency members.
Agency members can be read by active agency members.
Agency profile can only be updated by Active Owner.
Agency public profile can be read by id or slug.
Agency public listings show only Active agency listings.
Agency dashboard listings show Draft/Active/Archived to active Owner/Agent members.
Personal publish requires owner + Active user.
Agency publish requires Active user + Active agency + active agency member Owner/Agent.
Unpublish/archive block Disabled users but allow PendingVerification users to hide/manage allowed listings.
Agency unpublish/archive/dashboard access do not require Active agency.
Payments/subscriptions are not implemented yet.
Current user profile can be read through GET /api/users/me.
Current user profile can be updated through PUT /api/users/me/profile.
Current user avatar can be uploaded/replaced through PUT /api/users/me/avatar.
Current user avatar can be deleted through DELETE /api/users/me/avatar.
```

Current cleanup status:

```text
Large integration test files are split by feature.
Listing test helper duplication has been reduced.
ListingRepository query structure is cleaner.
AgencyRepository is acceptable as-is.
AgencyListingAccessChecker centralizes repeated agency listing permission checks.
Controllers are thin enough for now.
Image handlers are acceptable for MVP.
Program.cs, DbContext, DI, storage, current user, and JWT code are acceptable for now.
```

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
Does User.Status need to be Active or only not Disabled?
Does Agency.Status need to be Active or can inactive agencies still manage/hide data?
Is the listing Draft/Active/Archived?
Is this public visibility or private dashboard visibility?
```

### Verification

Current Chapter 8 enforcement:

```text
PendingVerification users can create drafts but cannot publish listings.
Disabled users cannot publish/unpublish/archive listings or view agency dashboard listings.
PendingVerification agencies cannot publish agency listings.
Disabled/Rejected agencies cannot publish agency listings.
Agency status does not block unpublish/archive/dashboard listing management.
```

Future decisions still needed:

```text
Can PendingVerification users keep creating listings long term?
Should public agency pages show pending agencies?
Should agency profile visibility require agency verification?
Should admin approval/verification endpoints be added?
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

### Repository/query growth

ListingRepository is cleaner now, but future search features may still require a better query structure.

Future candidates only if needed:

```text
ListingSearchCriteria
ListingQueryBuilder
Specification/query helper
Map bounding box filters
Sorting options
Full-text search
Visibility/publishing filters
```

Do not add these early.

### Image handling

Image handlers are acceptable for MVP.

Future cleanup may be useful when image features grow:

```text
Shared listing ownership guard
Shared image response mapper
File cleanup if database save fails after file upload
Possible ListingImagesController split if image endpoints grow
```

Do not refactor this now unless changing image behavior.

### Controllers

Controllers are currently acceptable and thin.

Possible later split:

```text
Move image endpoints from ListingsController into ListingImagesController
```

Only do this if image endpoints grow further.

### Program.cs and production readiness

Program.cs is acceptable for MVP.

Future production cleanup may include:

```text
Move JWT setup into extension method
Move Swagger setup into extension method
Move CORS origins into configuration
Review HTTPS redirection
Review production secrets handling
```

Do not over-clean this before production needs are real.

---

## Next planned work

Chapter 8.5 is completed and documented.

Locked roadmap:

```text
Chapter 9 — Agency Phase 2
Chapter 9.5 — Frontend readiness
Then switch to frontend
```

Do not start Chapter 9 until Agency Phase 2 rules are chosen clearly.

---

## Chapter 8 completed summary

Core visibility rule:

```text
Draft = private / dashboard only
Active = public
Archived = hidden from public flow
```

Public endpoints:

```http
GET /api/listings
GET /api/listings/{id}
GET /api/agencies/{id}/listings
```

Rules:

```text
Public listing APIs show only Active listings.
Public GET by id returns 404 for hidden/non-Active listings.
Public agency listings show only Active listings.
```

Private dashboard endpoints:

```http
GET /api/listings/my
GET /api/agencies/{id}/dashboard/listings
```

Rules:

```text
My listings shows own Draft/Active/Archived listings.
Agency dashboard listings shows agency Draft/Active/Archived listings to active Owner/Agent members.
```

Protected listing status endpoints:

```http
PUT /api/listings/{id}/publish
PUT /api/listings/{id}/unpublish
PUT /api/listings/{id}/archive
```

Rules:

```text
Personal publish requires owner + Active user.
Agency publish requires Active user + Active agency + active agency member Owner/Agent.
Personal unpublish/archive require owner and block Disabled users.
Agency unpublish/archive require active agency member Owner/Agent and block Disabled users.
Agency status does not block unpublish/archive/dashboard listing access.
Archived restore was not added in Chapter 8.
```

Permission cleanup:

```text
AgencyListingAccessChecker centralizes repeated agency listing access checks.
Handlers still keep user-status, personal ownership, and action-specific status transition rules.
Repositories remain data-focused.
```

Detailed rules document:

```text
docs/chapters/chapter-08-publishing-visibility-verification.md
```

Final Chapter 8 test status:

```text
204/204 tests passing
```

---

## Chapter 8.5 completed summary

Chapter 8.5 added authenticated current-user profile/account basics.

Endpoints:

```http
GET /api/users/me
PUT /api/users/me/profile
PUT /api/users/me/avatar
DELETE /api/users/me/avatar
```

Current-user profile rules:

```text
GET /api/users/me returns current authenticated user profile.
Active, PendingVerification, and Disabled users can read their own profile.
PUT /api/users/me/profile updates only FirstName, LastName, and PhoneNumber.
Email, NormalizedEmail, PasswordHash, Role, Status, CreatedAtUtc, ModifiedAtUtc, and avatar fields are not updateable through profile update.
```

Avatar rules:

```text
PUT /api/users/me/avatar handles both first avatar upload and replacement.
DELETE /api/users/me/avatar removes avatar and is idempotent.
Active and PendingVerification users can update profile and upload/delete avatar.
Disabled users cannot mutate profile/avatar.
```

Avatar validation:

```text
Max size: 5 MB
Allowed extensions: .jpg, .jpeg, .png, .webp
Allowed content types: image/jpeg, image/png, image/webp
```

Avatar storage:

```text
Local filesystem:
src/RealEstate.Api/wwwroot/uploads/users/{userId}/avatar/{storedFileName}

Public URL:
/uploads/users/{userId}/avatar/{storedFileName}
```

Implementation summary:

```text
Added UsersController.
Added UserProfileResponse and UpdateUserProfileRequest.
Added user profile/avatar handlers.
Added User.UpdateProfile(), User.SetAvatar(), and User.RemoveAvatar().
Added avatar fields to Users table.
Extended file storage with SaveUserAvatarAsync and DeleteUserAvatarAsync.
Added user endpoint integration tests.
```

Detailed rules document:

```text
docs/chapters/chapter-08-5-user-profile-account-basics.md
```

Final Chapter 8.5 test status:

```text
230/230 tests passing
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
```
