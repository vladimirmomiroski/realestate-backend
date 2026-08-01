# RealEstate Backend Context

## 1. Purpose of this file

This file is the compressed implementation handoff for the RealEstate backend.

Use it to understand:

- the current backend state
- the architecture and engineering rules
- the implemented business and permission model
- the important technical decisions
- the current test state
- unresolved decisions and known risks
- the locked roadmap

Detailed chapter rules belong in `docs/chapters/*.md`.
This file should describe the current system, not repeat the full implementation history.

## 2. Documentation and AI implementation policy

Create a dedicated `docs/chapters/*.md` rules document before implementing a chapter that affects:

```text
permissions
security
public visibility
verification
payments
subscriptions
sensitive business rules
data exposure
```

When implementation depends on project-specific structure, inspect the exact relevant files before giving final compile-ready code.

This is especially important for:

```text
repository changes
EF Core projections or mappings
integration-test setup
controller and DI wiring
permission/security logic
existing helper names
entity constructors and private setters
database seeding in tests
```

Rule:

```text
Do not guess project-specific names, helpers, schema fields, or conventions.
Inspect the exact files first.
```

## 3. Project snapshot

The backend supports a real estate platform with:

```text
users and authentication
personal listings
agency-owned listings
listing translations
listing images
apartment and house details
search and filtering
publishing and visibility rules
user profile and avatar management
agencies and memberships
agency invitations
agency member management
agency logo management
platform-admin agency verification
agency dashboard listings
agency dashboard summary
deterministic multilingual public search and sorting
structured location and literal text search
comparable listings
coordinate-ready listing responses
```

Current backend phase:

```text
Chapter 9 and its 9L documentation cleanup are complete.
Chapter 10 — Search and Discovery Phase 2 is complete.
Chapter 11 — Data Integrity and Targeted Hardening is complete.
Chapter 12 — API Consistency, Observability, and Frontend Readiness is complete.
No Chapter 12 implementation task remains.
The next product phase is frontend foundation and implementation.
```

Current test state:

```text
1001 passed
0 failed
0 skipped
query-review build: 0 warnings, 0 errors
solution build: 0 warnings, 0 errors
15 migrations
pending model: clean
```

Current frontend integration baseline:

```text
Canonical API failures use documented ProblemDetails schemas with stable codes and request correlation.
X-Request-ID is returned on API, health, and served-media responses.
HTTP pagination uses the unified PagedResponse<T> contract.
Invitation lists apply effective expiry without writing on read.
GET /api/health is dependency-free liveness.
GET /api/health/readiness and GET /api/health/database are PostgreSQL readiness routes.
CORS binds Cors:AllowedOrigins and fails closed when no origins are configured.
Uploaded-media URLs remain API-relative /uploads/... paths, and the tracked wwwroot placeholder supports clean checkout.
OpenAPI is structurally tested; Bearer requirements are operation-derived and Swagger middleware remains Development-only.
```

## 4. Tech stack

```text
.NET 10
ASP.NET Core
C#
Clean Architecture
Entity Framework Core
PostgreSQL
Docker / Docker Compose
Swagger / Swashbuckle
JWT Bearer Authentication
xUnit
FluentAssertions
Microsoft.AspNetCore.Mvc.Testing
Testcontainers PostgreSQL
```

Integration tests run against a real temporary PostgreSQL container, not EF InMemory.

## 5. Solution structure

```text
src/
  RealEstate.Api
  RealEstate.Application
  RealEstate.Domain
  RealEstate.Infrastructure

tests/
  RealEstate.Tests
```

Main responsibilities:

```text
Api
- HTTP endpoints
- authentication middleware integration
- current-user adapter
- request/response mapping at the HTTP boundary

Application
- use-case handlers
- validators
- permission checkers
- repository interfaces
- API DTOs
- internal read models

Domain
- entities
- enums
- local state transitions
- business invariants

Infrastructure
- EF Core
- PostgreSQL persistence
- repository implementations
- migrations
- JWT generation
- password hashing
- local file storage

Tests
- domain and application unit tests
- PostgreSQL-backed API integration tests
```

Repositories are located directly under:

```text
src/RealEstate.Infrastructure/Persistence/Repositories
```

## 6. Architecture flow

```text
HTTP request
  ↓
Controller
  ↓
Application handler
  ↓
Repository interface / permission checker / storage abstraction
  ↓
Infrastructure implementation
  ↓
RealEstateDbContext
  ↓
PostgreSQL
```

Engineering rules:

```text
Controllers stay thin.
Handlers own use-case orchestration.
Domain entities own local status transitions and invariants.
Repositories stay data-focused.
Application owns repository interfaces.
Infrastructure implements repository interfaces.
Read models are used for database/query projections.
Authorization decisions do not belong in repositories.
Do not introduce abstractions before repeated complexity justifies them.
```

Current intentional choices:

```text
No MediatR
No AutoMapper
No FluentValidation package
No generic repository
No custom Unit of Work abstraction
Manual DI registration
```

## 7. Aggregate and model conventions

### Listing aggregate

`Listing` is the aggregate root for:

```text
ListingTranslation
ListingImage
ListingApartmentDetails
ListingHouseDetails
```

### Agency aggregate

`Agency` is the aggregate root for agency profile and membership setup.

Related entities include:

```text
AgencyMember
AgencyInvitation
```

### DbContext convention

Expose aggregate roots publicly when useful.
Use navigation properties or internal `Set<TEntity>()` access for child entities when a public `DbSet` is unnecessary.

### Read-model convention

```text
Dtos       = API request/response shapes
ReadModels = database/query projection shapes
Entities   = domain/business objects
```

Feature-specific read models live under the feature, for example:

```text
RealEstate.Application/Agencies/ReadModels
```

## 8. Core architecture and cleanup decisions already completed

The backend has already gone through targeted cleanup and hardening.

Completed cleanup includes:

```text
large integration test files split into focused partial-class files
shared fixture/setup preserved
listing test helper duplication reduced
ListingRepository filtering/query code split into focused private helpers
AgencyRepository reviewed and kept data-focused
AgencyListingAccessChecker extracted
AgencyAdminAccessChecker extracted
PlatformAdminAccessChecker added
disabled-user permission drift fixed in agency and listing creation paths
invitation created/list responses split to avoid exposing token/code in list responses
CreateListingRequest no longer controls listing status
invitation acceptance locked to token-only
Manager permissions intentionally restricted
agency logo file cleanup and replacement behavior covered by tests
admin verification transitions moved into Agency domain methods
dashboard summary implemented as one EF read projection
```

Do not reintroduce the removed duplication or permission drift.

## 9. Authentication and users

### Register

```http
POST /api/auth/register
```

Behavior:

```text
normalizes email
enforces normalized-email uniqueness
hashes password
creates user as PendingVerification
does not return a JWT
```

### Login

```http
POST /api/auth/login
```

Behavior:

```text
returns generic invalid-credentials behavior for unknown email or wrong password
returns JWT access token on success
```

JWT includes:

```text
sub
email
ClaimTypes.NameIdentifier
ClaimTypes.Email
ClaimTypes.Role
```

`CurrentUserService` resolves the user ID from:

```text
ClaimTypes.NameIdentifier
```

### User roles

```text
User
Agent
AgencyOwner
Admin
```

Important:

```text
UserRole.Admin is a global platform role.
It is not the same as AgencyMemberRole.Owner.
```

### User statuses

```text
PendingVerification
Active
Disabled
```

Current status behavior:

```text
PendingVerification users can create drafts and agencies.
PendingVerification users cannot publish listings.
Disabled users are blocked from profile/avatar mutations, listing creation and status transitions, and protected agency/dashboard actions.
Listing-image mutations reload the current user and enforce the established status policy in addition to ownership or agency permission checks.
Disabled users may still read their own profile.
```

### User profile endpoints

```http
GET    /api/users/me
PUT    /api/users/me/profile
PUT    /api/users/me/avatar
DELETE /api/users/me/avatar
```

Rules:

```text
GET /me is allowed for Active, PendingVerification, and Disabled users.
Profile update changes only FirstName, LastName, and PhoneNumber.
Active and PendingVerification users can update profile/avatar.
Disabled users cannot mutate profile/avatar.
Avatar delete is idempotent.
```

## 10. Listings

### Ownership

Personal listing:

```text
CreatedByUserId = creator
AgencyId = null
```

Agency listing:

```text
CreatedByUserId = user who created the listing
AgencyId = owning/grouping agency
```

Important:

```text
CreatedByUserId and AgencyId represent different concepts.
Do not treat agency ownership as creator ownership.
```

### Listing statuses

Current enum includes:

```text
Draft
Active
Reserved
Sold
Rented
Archived
```

Current implemented transitions:

```text
Publish()
Unpublish()
Archive()
```

### Visibility rules

Public listing endpoints expose only `Active` listings.

```text
Draft and Archived listings are hidden publicly.
Public GET by ID returns 404 for non-Active listings.
Public agency listings also expose only Active listings.
```

Private endpoints:

```text
GET /api/listings/my
GET /api/agencies/{agencyId}/dashboard/listings
```

These expose allowed non-public statuses to authorized users.

### Publishing rules

Personal publish:

```text
listing owner
User.Status == Active
Draft -> Active
Active -> Active idempotent
Archived -> 409 Conflict
```

Agency publish:

```text
User.Status == Active
Agency.Status == Active
active agency membership
role Owner or Agent
```

### Unpublish/archive rules

Personal:

```text
listing owner
user must not be Disabled
```

Agency:

```text
active Owner or Agent membership
user must not be Disabled
Agency.Status does not block unpublish/archive
```

### Listing creation count

Current production rule:

```text
An authenticated Active user has no application-level per-user count limit on listing creation.
Existing authentication, Disabled-user, request-validation, and agency-permission rules still apply.
Future subscription, billing, quota, or plan limits must be implemented separately as an explicit feature.
```

PendingVerification draft-creation behavior remains governed by the existing user-status rules.

### Images

Storage:

```text
src/RealEstate.Api/wwwroot/uploads/listings/{listingId}/{storedFileName}
```

Rules:

```text
max 5 MB
.jpg, .jpeg, .png, .webp
matching MIME required
max 20 images per listing
first image becomes primary
one primary image per listing
creator ownership still controls image mutations
```

Same-agency members can manage listing publishing/status, but cannot manage another creator's listing images yet.

The filtered unique primary-image index requires the existing two-phase primary-image update.

Chapter 11 image-integrity guarantees:

```text
Disabled creators cannot upload, delete, set primary, or reorder images.
Failed or cancelled local copies leave no partial final or temporary file.
A noncommitted listing-image upload compensates exactly its newly stored file.
Upload, delete, set-primary, and reorder serialize on the Listings parent row.
The 20-image cap, append order, and single-primary invariant are revalidated under that lock.
Primary delete and set-primary keep both save phases in one transaction.
Physical deletion remains post-commit and may leave an orphan if deletion fails (CH11-FILE-01).
```

## 11. Search and listing queries

Public listing search uses one shared Active-only repository path for general search and public agency listings. It supports deterministic pagination and:

```text
agency
listing type
property type
newest, priceAsc, and priceDesc ordering with UUID tie-breakers
explicit currency-safe price filtering and sorting
inclusive area and room ranges
city
municipality
neighborhood
literal four-field q search
heating
furnishing
condition
basement
elevator
apartment type
house type
yard-area range
```

The effective translation is selected deterministically by requested language, then `mk`, then PostgreSQL `C` language ordering and translation UUID. Structured location and `q` predicates use that one effective row; `%`, `_`, and `\` are escaped as literal characters.

Comparable listings use an Active source and Active candidates, same listing/property type and currency, positive price/area, effective-language/city eligibility, and the locked six-key deterministic order. Coordinates are validated as an all-or-nothing pair and by latitude/longitude range.

All four paged listing HTTP surfaces return the unified `PagedResponse<ListingResponse>` contract with `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasNextPage`, and `hasPreviousPage`. `PagedResult<T>` is internal repository data only. Offset pagination is retained; private listing paths use deterministic `CreatedAtUtc DESC, Id DESC` ordering.

Final query-shape work completed:

```text
QS1 — execute each ordered page root once and hydrate translations/images by selected IDs
QS2 — set-based effective-translation filtering for location and q
QS3 — scalar-eligible comparable candidates before translation selection, with selected-ID hydration
```

The accepted `IX_ListingTranslations_Q_Trigram` four-column GIN index supports the existing literal `%contains%` `ILIKE` contract. It does not add fuzzy search, full-text search, canonical locations, exchange rates, or a separate search platform. Permanent evidence is under `docs/benchmarks/chapter-10f/evidence/`.

## 12. Agencies

### Agency statuses

```text
PendingVerification
Active
Disabled
Rejected
```

New agencies start as:

```text
PendingVerification
```

### Public agency profiles

```http
GET /api/agencies/{id}
GET /api/agencies/by-slug/{slug}
```

Public agency-profile visibility has not yet been restricted by status.

### Agency members

Roles:

```text
Owner
Manager
Agent
```

Statuses:

```text
Active
Pending
Disabled
```

Important:

```text
Manager exists in the enum but remains intentionally restricted.
Manager is not assignable through current invitation or role-change flows.
Manager is not allowed to manage agency listings or dashboard summary.
```

Agency creator becomes:

```text
Active Owner
```

A user cannot belong to the same agency twice.

### Permission checkers

#### AgencyAdminAccessChecker

Scope:

```text
agency-level administration
```

Requires:

```text
current user exists
user not Disabled
agency exists
active agency membership
AgencyMemberRole.Owner
```

Used for actions such as:

```text
agency profile update
invitation create/list/cancel
member disable
member role change
agency logo management
```

#### AgencyListingAccessChecker

Scope:

```text
agency listing and private dashboard access
```

Allows:

```text
active Owner
active Agent
```

Manager is blocked.

Publishing can additionally require:

```text
Agency.Status == Active
```

Private management/dashboard actions do not require an Active agency.

#### PlatformAdminAccessChecker

Scope:

```text
global platform administration
```

Requires the database user to be:

```text
UserRole.Admin
UserStatus.Active
```

It reloads the user from the database and does not trust only the JWT role claim.

A user may be both a platform Admin and an agency Owner, but neither role implies the other.

## 13. Agency invitations

Entity/table:

```text
AgencyInvitation
AgencyInvitations
```

Important fields:

```text
AgencyId
Email
NormalizedEmail
Token
Code
Role
Status
InvitedByUserId
ExpiresAtUtc
CreatedAtUtc
ModifiedAtUtc
```

Invitation statuses:

```text
Pending
Accepted
Cancelled
Expired
```

Current rules:

```text
Active Owner creates invitations.
Active Owner lists invitations.
Active Owner cancels invitations.
Acceptance is token-only.
Code is generated/reserved for possible future use but is not used for acceptance.
Only Owner and Agent are assignable.
Manager is not assignable.
Duplicate pending invitations are blocked.
Invitation email must match the accepting user.
Expired invitations cannot be accepted.
Acceptance creates an Active agency membership.
Accept and cancel serialize terminal transitions on the invitation row.
Membership creation and Pending -> Accepted commit atomically.
An elapsed Pending invitation can be expired and replaced atomically.
Concurrent creates retain at most one live stored Pending invitation.
List reads capture one UTC value and present stored Pending invitations with ExpiresAtUtc <= utcNow as Expired.
The Pending list filter returns only stored Pending invitations with ExpiresAtUtc > utcNow.
The Expired list filter returns stored Expired invitations plus elapsed/equal stored Pending invitations.
Effective list presentation is no-tracking and write-free; accept, cancel, and replacement actions retain their established persisted transitions.
```

List responses do not expose token or code.

## 14. Agency member management

### Disable member

```http
PUT /api/agencies/{agencyId}/members/{memberId}/disable
```

Rules:

```text
Active Owner only
cannot disable self
an active Owner cannot be disabled if they are the last active Owner in the agency
already Disabled is idempotent
```

### Change role

```http
PUT /api/agencies/{agencyId}/members/{memberId}/role
```

Rules:

```text
Active Owner only
target membership must be Active
assignable roles: Owner or Agent
same role is idempotent
last active Owner cannot be demoted
ownership handoff is done by promoting another Owner first
existing Manager may be changed to Owner or Agent as a recovery path
```

Concurrency guarantee:

```text
Owner demotion and member disable serialize on the Agencies parent row.
The actor, target, and Active Owner count are re-read after locking.
Concurrent mutations cannot leave an agency without an Active Owner.
```

## 15. Agency logo management

Endpoints:

```http
PUT    /api/agencies/{agencyId}/logo
DELETE /api/agencies/{agencyId}/logo
```

Authorization:

```text
Active agency Owner only
Agency.Status does not block private logo management
```

Storage:

```text
src/RealEstate.Api/wwwroot/uploads/agencies/{agencyId}/logo/{storedFileName}
```

Metadata on `Agency`:

```text
LogoUrl
LogoStoredFileName
LogoContentType
LogoSizeBytes
```

Rules:

```text
max 5 MB
.jpg, .jpeg, .png, .webp
matching MIME required
upload replaces existing logo
new file is stored before DB update
new file is removed if DB save fails
old file is removed after successful replacement
delete clears metadata before physical deletion
delete is idempotent
```

## 16. Platform-admin agency verification

Controller scope:

```text
/api/admin/agencies
```

Endpoints:

```http
PUT /api/admin/agencies/{agencyId}/approve
PUT /api/admin/agencies/{agencyId}/reject
PUT /api/admin/agencies/{agencyId}/disable
```

Authorization:

```text
Active global UserRole.Admin only
```

Status transitions:

```text
Approve
PendingVerification -> Active
Rejected            -> Active
Active              -> Active idempotent
Disabled            -> 409 Conflict

Reject
PendingVerification -> Rejected
Rejected            -> Rejected idempotent
Active              -> 409 Conflict
Disabled            -> 409 Conflict

Disable
PendingVerification -> Disabled
Active              -> Disabled
Rejected            -> Disabled
Disabled            -> Disabled idempotent
```

These transitions are implemented as domain methods on `Agency`.

## 17. Agency dashboards

### Dashboard listings

```http
GET /api/agencies/{agencyId}/dashboard/listings
```

Allows:

```text
active Owner
active Agent
```

Returns private agency listings and supports optional status filtering.

Agency status does not block access.

### Dashboard summary

```http
GET /api/agencies/{agencyId}/dashboard/summary
```

Response includes:

```text
AgencyId
AgencyName
AgencyStatus
TotalListings
DraftListings
ActiveListings
ArchivedListings
MembersCount
ActiveMembersCount
PendingInvitationsCount
```

Count rules:

```text
TotalListings = all listings owned by the requested agency
DraftListings = Draft only
ActiveListings = Active only
ArchivedListings = Archived only
MembersCount = all membership rows
ActiveMembersCount = Active memberships only
PendingInvitationsCount =
  Status == Pending
  and ExpiresAtUtc > current UTC time
```

The repository uses one read-only EF projection with database-side scalar counts.

Current conclusion:

```text
one database round trip expected
no Include
no collection loading
no N+1
raw SQL not justified at this stage
existing AgencyId indexes are sufficient for now
```

## 18. Current endpoint overview

### Cross-cutting HTTP contract

```text
Non-health API failures use concrete canonical ProblemDetails schemas.
Validation failures add field/root errors; all canonical failures expose code and traceId.
Documented responses expose X-Request-ID, and 401 responses document WWW-Authenticate.
Bearer security metadata is derived per operation from AllowAnonymous/Authorize metadata.
Swagger middleware and UI remain Development-only.
The generated OpenAPI document is structurally tested for security, errors, pagination, string enums, multipart uploads, relative media paths, and health.
```

### Health

```http
GET /api/health
GET /api/health/readiness
GET /api/health/database
```

`GET /api/health` is anonymous dependency-free process liveness. The readiness and database-alias routes are anonymous PostgreSQL probes with identical `200` ready and sanitized `503` unavailable semantics.

### Auth

```http
POST /api/auth/register
POST /api/auth/login
```

### Users

```http
GET    /api/users/me
PUT    /api/users/me/profile
PUT    /api/users/me/avatar
DELETE /api/users/me/avatar
```

### Listings

```http
POST /api/listings
GET  /api/listings
GET  /api/listings/{id}
GET  /api/listings/{id}/comparables
GET  /api/listings/my

PUT /api/listings/{id}/publish
PUT /api/listings/{id}/unpublish
PUT /api/listings/{id}/archive

POST   /api/listings/{listingId}/images
DELETE /api/listings/{listingId}/images/{imageId}
PUT    /api/listings/{listingId}/images/{imageId}/primary
PUT    /api/listings/{listingId}/images/order
```

### Agencies

```http
POST /api/agencies
GET  /api/agencies/{id}
GET  /api/agencies/by-slug/{slug}
GET  /api/agencies/my
GET  /api/agencies/{id}/members
GET  /api/agencies/{id}/listings
PUT  /api/agencies/{id}

GET /api/agencies/{agencyId}/dashboard/listings
GET /api/agencies/{agencyId}/dashboard/summary

POST /api/agencies/{agencyId}/invitations
GET  /api/agencies/{agencyId}/invitations
PUT  /api/agencies/invitations/accept
PUT  /api/agencies/{agencyId}/invitations/{invitationId}/cancel

PUT /api/agencies/{agencyId}/members/{memberId}/disable
PUT /api/agencies/{agencyId}/members/{memberId}/role

PUT    /api/agencies/{agencyId}/logo
DELETE /api/agencies/{agencyId}/logo
```

### Platform administration

```http
PUT /api/admin/agencies/{agencyId}/approve
PUT /api/admin/agencies/{agencyId}/reject
PUT /api/admin/agencies/{agencyId}/disable
```

## 19. Persistence and storage

Current important tables:

```text
Users
Agencies
AgencyMembers
AgencyInvitations
Listings
ListingTranslations
ListingImages
ListingApartmentDetails
ListingHouseDetails
__EFMigrationsHistory
```

Schema additions completed through Chapter 10 include:

```text
user avatar metadata
agency invitation table
agency logo metadata
pg_trgm extension
IX_ListingTranslations_Q_Trigram four-column GIN index
```

Chapters 11 and 12 added no schema or migration. The repository remains at 15 committed migrations. The authoritative zero-to-latest PostgreSQL verification, repeat update, and catalog inspection passed, and the Chapter 12 closeout independently confirmed no pending model changes.

Enums are stored as strings in PostgreSQL through EF Core conversions.

### Auditing

`IAuditableEntity` provides:

```text
CreatedAtUtc
ModifiedAtUtc
```

`RealEstateDbContext.SaveChangesAsync` sets audit timestamps automatically.

Handlers should not set audit timestamps manually unless a separate business timestamp is required.

### File storage

Current local storage areas:

```text
/uploads/listings/{listingId}/...
/uploads/users/{userId}/avatar/...
/uploads/agencies/{agencyId}/logo/...
```

`wwwroot/uploads` is ignored by Git.

The tracked `src/RealEstate.Api/wwwroot/.gitkeep` guarantees the static-media root exists in a clean checkout. `LocalFileStorageService` creates `uploads` and deeper directories only when saving; public URLs remain API-relative `/uploads/...` paths.

All three local save areas use one residue-free write primitive: copy to a unique same-directory temporary file, dispose the completed stream, then publish with a non-overwriting move. Failed and cancelled copies remove the operation-owned temporary file.

## 20. Testing strategy

Testing stack:

```text
xUnit
FluentAssertions
WebApplicationFactory
PostgreSQL Testcontainers
```

Testing policy:

```text
Use domain/unit tests for meaningful transitions, validation, and mapping.
Use integration tests for endpoint behavior, persistence, permissions, and visibility.
Do not chase artificial 100% unit coverage.
Use focused partial-class integration test files by feature.
Use a fresh DI scope when asserting persisted state.
Respect real business rules in test setup.
```

Important tested boundaries include:

```text
401 / 403 / 404 / 400 behavior
listing public/private visibility
personal vs agency listing ownership
agency membership roles/statuses
disabled-user restrictions
invitation expiry and acceptance
member disable and role change
agency logo replacement/deletion
platform-admin verification
dashboard summary isolation and counts
deterministic public sorting, filtering, count, and pagination
effective-translation and structured-location parity
literal q scope and wildcard escaping
comparable eligibility and six-key ordering
public Active-only and authorized private non-Active visibility
pg_trgm extension and trigram-index catalog shape
deterministic owner, invitation, and listing-image PostgreSQL lock races
invitation and image transaction rollback at injected persistence boundaries
local-file failure/cancellation residue cleanup
listing-image database/filesystem compensation boundaries
canonical ProblemDetails failures, stable error codes, and X-Request-ID correlation
single-owner unexpected-exception and request-completion logging
operation-derived OpenAPI security, canonical schemas, pagination, multipart, media, and health metadata
unified PagedResponse<T> pagination across all four listing-page endpoints
effective invitation-expiry presentation and no-write read behavior
liveness and PostgreSQL readiness, including timeout and client-abort behavior
fail-closed configurable CORS and isolated clean-checkout upload-to-static delivery
```

## 21. Development workflow

Branching:

```text
main        = stable releases/merges
development = integration branch
feature/*   = scoped implementation branches
docs/*      = documentation-only branches
```

Preferred workflow:

```text
1. Lock sensitive rules in a chapter document.
2. Inspect exact relevant files.
3. Implement a small checkpoint.
4. Run build/tests.
5. Use Codex for read-only review.
6. Fix only evidence-based findings.
7. Commit the checkpoint.
8. Update chapter/context docs at chapter completion.
```

Codex review rules:

```text
read-only
no branch checkout when working tree is dirty
compare against development using git show/git diff
do not modify files
do not commit
do not run migrations unless explicitly requested
```

## 22. Deferred decisions and known risks

Keep detailed unresolved findings in:

```text
docs/backend-quality-handoff.md
```

Important current decisions/risks:

### Chapter 11 retained limitations and owner decisions

```text
CH11-DB-01: Listings.CreatedByUserId remains nullable pending an authorized data/backfill decision.
CH11-DB-02: request validation is not broadly duplicated as PostgreSQL check constraints without a data/policy decision.
CH11-STATE-01: no global optimistic-concurrency or authorization-freshness policy was introduced outside protected invariants.
CH11-FILE-01: a committed database deletion followed by failed physical deletion can still leave an orphan file.
```

### Manager role

```text
Manager remains intentionally restricted.
Do not expand Manager permissions without explicit product rules.
```

### Invitation expiration consistency

```text
CH11-STATE-02 is resolved for reads.
Elapsed/equal stored Pending invitations are presented and filtered as effectively Expired.
Reads remain no-write; accept, cancel, and replacement actions still own persisted expiry transitions.
Dashboard summary continues to count only actionable Pending invitations.
```

### Error contracts

```text
Chapter 12 established one canonical ProblemDetails contract for API failures.
Known application conflicts map to 409 with stable machine codes.
Unexpected failures have one exception owner and one structured Error log.
Request completion has one structured Information log with X-Request-ID correlation.
```

### Pagination and query contracts

```text
PagedResponse<T> is the only HTTP pagination carrier.
PagedResult<T> remains an internal repository read carrier.
All four listing pagination operations share the seven-member response contract.
Cursor pagination remains deferred until scale or product evidence justifies a breaking sort-specific design.
```

### Production JWT configuration

```text
C12-CONFIG-01 remains unresolved.
The base appsettings.json local JWT placeholder can flow to non-Development hosts.
Production secret relocation and startup validation belong to Chapter 13 or explicit deployment hardening.
Chapter 12 frontend readiness does not certify production secret configuration.
```

### Search/query growth

```text
Chapter 10 completed focused EF query-shape corrections without adding a specification system or raw production SQL.
The accepted trigram GIN index is a measured read-heavy tradeoff, not a general search-framework decision.
Broader fuzzy search, full-text search, and external search remain deferred product/infrastructure choices.
```

### File storage

```text
Local storage is acceptable for current development.
Cloud/object storage is deferred until deployment needs justify it.
```

## 23. Locked roadmap

### Current completed milestone and next work

```text
Completed: Chapter 12 — API Consistency, Observability, and Frontend Readiness
Next product phase: Frontend foundation and implementation
```

Chapter 10 completion includes:

```text
deterministic public search, translation/location behavior, and comparables
PostgreSQL query-shape corrections and the accepted trigram index
authoritative permanent evidence and independent reproducibility verification
631/631 passing tests
```

Chapter 11 completion includes:

```text
serialized last-active-owner, invitation-terminal, and listing-image aggregate mutations
atomic elapsed-invitation replacement and named uniqueness-conflict handling
disabled-user listing-image authorization
residue-free local writes and listing-image upload compensation
15/15 fresh migrations, empty repeat update, valid catalog objects, and no pending model changes
704/704 passing tests
```

Chapter 12 completion includes:

```text
canonical API failures, normalized conflicts, request IDs, and structured completion/error logging
unified HTTP pagination and effective invitation-expiry presentation
dependency-free liveness and PostgreSQL readiness
configurable fail-closed CORS and clean-checkout static media
operation-accurate, structurally tested OpenAPI and safe developer request samples
1001/1001 passing tests, clean builds, 15 migrations, and no pending model changes
```

### Completed backend chapters before frontend

```text
Chapter 11 — Data Integrity and Targeted Hardening

Chapter 12 — API Consistency, Observability, and Frontend Readiness
```

Frontend foundation and implementation is now the next product phase.

### Later planned backend chapters

```text
Chapter 13 — Authentication and Account Security Phase 2

Chapter 14 — Background Jobs and Notifications

Chapter 15 — Agency Workspace Phase 3
```

The order and scope of Chapters 13–15 may change after frontend development and real workflow feedback.

## 24. Chapter focus summaries

### Chapter 10 — Search and Discovery Phase 2

Completed capabilities:

```text
deterministic sorting and currency-safe price behavior
area/room and existing public filters
deterministic effective translations and structured location matching
minimal literal four-field q search
same-currency deterministic comparable listings
coordinate validation and map-marker readiness
QS1/QS2/QS3 query-shape corrections
evidence-backed pg_trgm GIN index
permanent benchmark evidence and clean reproducibility verification
```

The original Q1 first-page sequence was approximately 3,511 ms. The final reproducibility run was approximately 11 ms with the same literal matching semantics, total, locked ordered IDs, and semantic result hash. The populated index is approximately 27 MB and carries substantial measured translation-write/WAL amplification; it was accepted for the read-heavy public-search path, not as a free or universal optimization.

Authoritative evidence: `docs/benchmarks/chapter-10f/evidence/` (benchmark commit `db3ad3220f58a23b752c62d2f33b0a01fff864f1`; semantic result hash `7f74f991bf29b6f3ad24d48f2e8e13ecf9f375ea6f9eb0da8f18204c528bfb36`).

### Chapter 11 — Data Integrity and Targeted Hardening

Completed capabilities:

```text
agency-owner parent-row serialization and post-lock actor revalidation
invitation terminal/replacement serialization and narrow named-conflict translation
disabled-user listing-image authorization
residue-free listing/avatar/logo writes
listing-image upload compensation
serialized image cap, ordering, primary, delete, and reorder invariants
deterministic PostgreSQL concurrency and injected rollback evidence
fresh migration, repeat-update, pending-model, and catalog verification
```

### Chapter 12 — API Consistency, Observability, and Frontend Readiness

Completed capabilities:

```text
canonical ProblemDetails, stable error codes, and deliberate 409 conflicts
single-owner exception handling and structured request-completion logging
X-Request-ID response/body/log correlation
unified PagedResponse<T> pagination and deterministic private pages
effective invitation-expiry presentation without write-on-read
dependency-free liveness and PostgreSQL readiness
Cors:AllowedOrigins fail-closed configuration and clean-checkout static media
operation-derived Bearer metadata and structurally tested OpenAPI
safe, current developer HTTP samples
```

### Chapter 13 — Authentication and Account Security Phase 2

Deferred until later:

```text
refresh tokens
logout/revocation
password reset
email verification
change password
login protection
```

### Chapter 14 — Background Jobs and Notifications

Deferred until there is real asynchronous work:

```text
invitation email delivery
saved-search alerts
scheduled cleanup
retry handling
outbox/background processing
```

### Chapter 15 — Agency Workspace Phase 3

Possible later product features:

```text
listing assignment to agents
internal notes
agency activity history
agent-specific work views
ownership/responsibility workflows
```

## 25. Next-task policy

When continuing in a new chat:

```text
Read backend-context.md first.
Read the current chapter rules document.
Inspect exact relevant files before compile-ready code.
Keep changes scoped to the current chapter/checkpoint.
Do not change architecture casually.
Do not mix unrelated business-rule changes into the current task.
Run build/tests before commit.
Use read-only review for important features.
```

Current next task:

```text
Begin frontend foundation and implementation against the completed Chapter 12 contract.
```

After frontend development begins, backend defects discovered through real integration may be handled on focused bugfix branches with regression tests. They must not silently expand a completed chapter or an unrelated current chapter.
