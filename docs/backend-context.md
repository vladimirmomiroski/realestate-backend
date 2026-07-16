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
```

Current backend phase:

```text
Chapter 9 feature implementation is complete.
Chapter 9L documentation cleanup is in progress.
Frontend work has not started yet.
```

Current test state:

```text
416/416 tests passing
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
Listing image mutations currently enforce authenticated creator ownership but do not separately reload/check User.Status.
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
Archived -> 400
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

## 11. Search and listing queries

Current public listing search supports pagination and filters including:

```text
agency
listing type
property type
price range
city
municipality
neighborhood
heating
furnishing
condition
basement
elevator
apartment type
house type
yard-area range
```

Current repository helper structure includes:

```text
ApplyBasicFilters
ApplyPropertyDetailFilters
ApplyLocationFilters
ApplyListingIncludes
NormalizePagination
```

Do not introduce specifications or a query-builder abstraction until Chapter 10 confirms the need.

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

Known risk:

```text
The active-owner count check is not yet protected against a concurrency race.
```

This belongs in Chapter 11.

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
Disabled            -> 400

Reject
PendingVerification -> Rejected
Rejected            -> Rejected idempotent
Active              -> 400
Disabled            -> 400

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

### Health

```http
GET /api/health
GET /api/health/database
```

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

Schema additions completed through Chapter 9 include:

```text
user avatar metadata
agency invitation table
agency logo metadata
```

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

### Last-owner concurrency

```text
The last-active-owner demotion rule currently depends on an application-level count.
Concurrent operations could race.
Address in Chapter 11.
```

### Manager role

```text
Manager remains intentionally restricted.
Do not expand Manager permissions without explicit product rules.
```

### Invitation expiration consistency

```text
Dashboard summary counts only actionable Pending invitations.
Invitation-list behavior is primarily status-based.
A pending row may remain Pending until an action marks it Expired.
Review consistency only if it becomes a real product issue.
```

### Error contracts

```text
Controllers currently map ServiceResult statuses manually.
Error response shapes are not yet fully standardized.
Address in Chapter 12.
```

### Pagination and query contracts

```text
Pagination conventions exist but are not yet globally standardized.
Review in Chapter 12.
```

### Search/query growth

```text
Current query structure is acceptable.
Do not introduce a specification system prematurely.
Chapter 10 will determine the next search/query architecture.
```

### File storage

```text
Local storage is acceptable for current development.
Cloud/object storage is deferred until deployment needs justify it.
```

## 23. Locked roadmap

### Current completion task

```text
Chapter 9L — Documentation cleanup
```

Scope:

```text
refresh Chapter 9 rules document
rewrite backend-context.md
clean docs/backend-quality-handoff.md
remove stale/resolved information
record the final test count and current roadmap
```

### Backend chapters before frontend

```text
Chapter 10 — Search and Discovery Phase 2

Chapter 11 — Data Integrity and Targeted Hardening

Chapter 12 — API Consistency, Observability, and Frontend Readiness
```

Then begin frontend development.

### Later planned backend chapters

```text
Chapter 13 — Authentication and Account Security Phase 2

Chapter 14 — Background Jobs and Notifications

Chapter 15 — Agency Workspace Phase 3
```

The order and scope of Chapters 13–15 may change after frontend development and real workflow feedback.

## 24. Chapter focus summaries

### Chapter 10 — Search and Discovery Phase 2

Likely focus:

```text
sorting
better filters
location normalization
text search
suggestions
query-shape review
index review based on actual searches
execution-plan analysis where needed
```

Do not add raw SQL or advanced search infrastructure without evidence.

### Chapter 11 — Data Integrity and Targeted Hardening

Likely focus:

```text
last-owner concurrency
invitation acceptance races
transaction boundaries
unique-constraint handling
idempotency review
other verified quality-backlog items
```

### Chapter 12 — API Consistency, Observability, and Frontend Readiness

Likely focus:

```text
ProblemDetails / consistent errors
global exception handling
structured logging
correlation/request IDs
pagination consistency
OpenAPI cleanup
CORS/configuration review
frontend-safe contracts
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
Finish Chapter 9L documentation cleanup.
Then begin Chapter 10 planning and rules.
```
