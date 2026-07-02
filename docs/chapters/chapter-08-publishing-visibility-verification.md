# Chapter 8 — Publishing, Visibility, and Verification Rules

## Purpose

Chapter 8 defines how listings become public, how hidden listings behave, and how user/agency verification affects publishing.

This chapter must be rule-first, then code.

The goal is to prevent confusion between:

```text
Listing.Status
User.Status
Agency.Status
AgencyMember.Role
AgencyMember.Status
Listing.CreatedByUserId
Listing.AgencyId
```

These fields affect different things and must not be mixed accidentally.

---

## Core invariant

Public API shows only `Active` listings.

`Draft` and `Archived` listings are hidden publicly.

Public `GET /api/listings/{id}` returns:

```http
404 Not Found
```

for missing, `Draft`, or `Archived` listings.

It must not return:

```http
403 Forbidden
```

for hidden public listings.

Reason:

```text
Public users should not know that hidden listings exist.
```

---

## Listing statuses used in Chapter 8

Current listing statuses involved in this chapter:

```text
Draft
Active
Archived
```

Visibility rules:

```text
Draft    -> hidden publicly
Active   -> visible publicly
Archived -> hidden publicly
```

Chapter 8 does not focus on:

```text
Reserved
Sold
Rented
```

Those statuses can stay untouched unless existing tests require them.

---

## Public endpoints

These public endpoints must only expose `Active` listings:

```http
GET /api/listings
GET /api/listings/{id}
GET /api/agencies/{id}/listings
```

### GET /api/listings

Rules:

```text
Returns only Active listings.
Does not return Draft listings.
Does not return Archived listings.
Keeps existing filters/pagination behavior.
```

Important:

```text
Visibility filtering must apply before pagination count is calculated.
```

So `totalCount` must count only public-visible listings.

---

### GET /api/listings/{id}

Rules:

```text
Active listing   -> 200 OK
Draft listing    -> 404 Not Found
Archived listing -> 404 Not Found
Missing listing  -> 404 Not Found
```

Reason:

```text
Public users should not be able to distinguish between missing and hidden listings.
```

---

### GET /api/agencies/{id}/listings

Rules:

```text
Existing agency + Active listings only -> 200 OK
Existing agency + only Draft listings -> 200 OK with empty paged result
Existing agency + only Archived listings -> 200 OK with empty paged result
Missing agency -> 404 Not Found
```

Important:

```text
Agency existence is still checked.
Listing visibility is filtered to Active only.
```

---

## Owner/dashboard endpoint

Current owner/dashboard endpoint:

```http
GET /api/listings/my
```

Rules:

```text
Requires JWT.
Returns only listings where CreatedByUserId equals current user id.
Can return Draft listings.
Can return Active listings.
Can return Archived listings.
```

Reason:

```text
Owners need to manage all of their own listings from dashboard.
```

This endpoint must not be changed to public-only Active filtering.

---

## Publishing endpoints

Chapter 8 adds:

```http
PUT /api/listings/{id}/publish
PUT /api/listings/{id}/unpublish
PUT /api/listings/{id}/archive
```

All three require JWT.

No restore endpoint in Chapter 8.

Skipped for now:

```http
PUT /api/listings/{id}/restore
```

Reason:

```text
Archived is treated as final hidden state for now.
Restore behavior can be added later when there is a real product need.
```

---

## Status transition rules

### Publish

Allowed:

```text
Draft -> Active
```

Not allowed in Chapter 8:

```text
Archived -> Active
```

Behavior:

```text
Draft listing + allowed user -> 200 OK
Active listing + allowed user -> 200 OK idempotent
Archived listing + allowed user -> 400 Bad Request
```

Recommended MVP choice:

```text
Active -> Active publish call should be idempotent and return 200 OK.
Archived -> Active should return 400 Bad Request.
```

Reason:

```text
Idempotent publish is frontend-friendly.
Archived restore is intentionally not supported.
```

---

### Unpublish

Allowed:

```text
Active -> Draft
```

Recommended behavior:

```text
Draft -> Draft unpublish call returns 200 OK idempotent.
Active -> Draft returns 200 OK.
Archived -> Draft returns 400 Bad Request.
```

Reason:

```text
Unpublish means remove from public visibility but keep editable.
Archived should stay final hidden state.
```

---

### Archive

Allowed:

```text
Draft -> Archived
Active -> Archived
```

Recommended behavior:

```text
Draft -> Archived returns 200 OK.
Active -> Archived returns 200 OK.
Archived -> Archived returns 200 OK idempotent.
```

Reason:

```text
Archive is a safe final state action.
Repeated archive calls should not break frontend flow.
```

---

## Personal listing rules

A personal listing is:

```text
Listing.AgencyId = null
```

Ownership source:

```text
Listing.CreatedByUserId
```

Rules:

```text
Only the listing owner can publish.
Only the listing owner can unpublish.
Only the listing owner can archive.
User must be Active / verified enough to publish.
PendingVerification user cannot publish.
Disabled user cannot publish.
```

Recommended for unpublish/archive:

```text
Owner can unpublish/archive even if user verification later changes,
unless user is Disabled.
```

Reason:

```text
A user who cannot publish should still be able to remove their listing from public visibility.
```

So for personal listings:

```text
Publish:
- requires owner
- requires User.Status = Active

Unpublish:
- requires owner
- blocked if User.Status = Disabled

Archive:
- requires owner
- blocked if User.Status = Disabled
```

---

## Agency listing rules

An agency listing is:

```text
Listing.AgencyId != null
```

Required checks:

```text
Current user must be an Active agency member.
Agency must exist.
Agency must be Active to publish.
Disabled agency cannot publish.
PendingVerification agency cannot publish.
Disabled member cannot publish/unpublish/archive.
```

Allowed roles for agency listing publishing:

```text
Active Owner -> can publish/unpublish/archive agency listings
Active Agent -> can publish/unpublish/archive agency listings
```

Important permission difference:

```text
Agency profile update -> Active Owner only
Agency listing publishing -> Active Owner or Active Agent
```

Reason:

```text
Agents need to manage listings.
Agency profile control should stay owner-only.
```

Recommended for unpublish/archive:

```text
Active agency member can unpublish/archive agency listings.
Agency does not need to be Active for unpublish/archive if the action hides content.
Disabled agency should still allow hiding content if needed.
```

But keep MVP simple:

```text
Publish:
- requires Active agency
- requires Active member
- Owner or Agent allowed

Unpublish:
- requires Active member
- Owner or Agent allowed

Archive:
- requires Active member
- Owner or Agent allowed
```

---

## User status rules

Current user statuses:

```text
Active
Disabled
PendingVerification
```

Publishing rule:

```text
Active -> can publish if ownership/member rules pass
PendingVerification -> cannot publish
Disabled -> cannot publish
```

Recommended action behavior:

```text
PendingVerification user:
- can create draft listings
- cannot publish listings

Disabled user:
- cannot publish
- cannot unpublish
- cannot archive
```

Reason:

```text
Pending users can prepare data.
Disabled users should not perform protected actions.
```

---

## Agency status rules

Current agency statuses:

```text
PendingVerification
Active
Disabled
Rejected
```

Publishing rule:

```text
Active -> agency listings can be published if member rules pass
PendingVerification -> agency listings cannot be published
Disabled -> agency listings cannot be published
Rejected -> agency listings cannot be published
```

Recommended visibility rule for agency public profile:

```text
Do not change public agency profile visibility in Chapter 8 unless explicitly decided.
```

Current Chapter 8 focus is listing visibility, not agency profile visibility.

So this chapter changes:

```http
GET /api/agencies/{id}/listings
```

but does not necessarily change:

```http
GET /api/agencies/{id}
GET /api/agencies/by-slug/{slug}
```

---

## Error behavior

### Public hidden listings

Use:

```http
404 Not Found
```

For:

```text
Missing listing
Draft listing by id
Archived listing by id
```

### Protected publishing endpoints

Use:

```http
401 Unauthorized
```

When:

```text
No JWT token
Invalid JWT token
```

Use:

```http
403 Forbidden
```

When:

```text
Authenticated user is not allowed to perform the action
Non-owner tries personal listing action
Non-member tries agency listing action
Disabled member tries agency listing action
Wrong agency member tries action
PendingVerification user tries to publish
PendingVerification agency tries to publish
```

Use:

```http
404 Not Found
```

When:

```text
Listing does not exist
```

Recommended for protected endpoints:

```text
If listing exists but user has no relationship to it, 403 is acceptable.
```

Reason:

```text
Protected dashboard actions are different from public browsing.
The user is authenticated and trying to mutate a resource.
```

Use:

```http
400 Bad Request
```

When:

```text
Status transition is invalid
Archived -> Active publish attempt
Archived -> Draft unpublish attempt
```

Recommended MVP choice:

```text
Use 403 for permission/verification failures.
Use 400 for invalid status transitions.
```

---

## Domain method rules

Add or update domain methods on `Listing`.

Recommended methods:

```csharp
public void Publish()
public void Unpublish()
public void Archive()
```

Rules inside domain methods:

```text
Domain method handles valid listing status transition.
Domain method does not check current user.
Domain method does not check agency membership.
Domain method does not check JWT.
Domain method does not call repositories.
```

Reason:

```text
Listing knows how its own status can change.
Handlers know who is allowed to request the change.
```

Example responsibility split:

```text
Handler:
- load listing
- load current user
- check owner/member/verification permissions
- call listing.Publish()
- save changes

Listing entity:
- change Draft -> Active
- reject Archived -> Active
- change Active -> Draft
- change Draft/Active -> Archived
```

---

## Handler/application rules

Publishing handlers should contain application-level permission logic.

Expected handlers:

```text
PublishListingHandler
UnpublishListingHandler
ArchiveListingHandler
```

Expected commands:

```text
PublishListingCommand
UnpublishListingCommand
ArchiveListingCommand
```

Recommended folder structure:

```text
src/RealEstate.Application/Listings/Commands/PublishListing
src/RealEstate.Application/Listings/Commands/UnpublishListing
src/RealEstate.Application/Listings/Commands/ArchiveListing
```

Each command can be simple:

```csharp
public sealed record PublishListingCommand(Guid ListingId);
```

Handlers need:

```text
IListingRepository
IUserRepository
IAgencyRepository
ICurrentUserService
```

Exact dependencies may change based on existing repository methods.

---

## Repository rules

Repositories stay data-focused.

Do not put business permission decisions inside repositories.

Bad repository methods:

```text
CanUserPublishListingAsync
CanMemberManageAgencyListingAsync
CanAgencyPublishListingAsync
```

Good repository methods:

```text
GetByIdForUpdateAsync
GetPublicActiveByIdReadOnlyAsync
GetFilteredPublicActiveReadOnlyAsync
CountByCreatedByUserIdAsync
```

Good agency repository data methods:

```text
GetMemberAccessReadOnlyAsync
GetByIdReadOnlyAsync
ExistsAsync
```

The handler should decide:

```text
Is the user owner?
Is the user Active?
Is the agency Active?
Is the member Active?
Is the role Owner or Agent?
```

---

## Public query implementation rule

Public listing queries must make visibility intent obvious.

Good options:

```text
GetPublicActiveListingsAsync(...)
GetPublicActiveListingByIdAsync(...)
GetAgencyPublicActiveListingsAsync(...)
```

Also acceptable:

```text
GetFilteredReadOnlyAsync(..., onlyActivePublic: true)
GetByIdReadOnlyAsync(..., onlyActivePublic: true)
```

Avoid hidden magic like:

```text
Repository always filters Active without handler making it clear.
```

Reason:

```text
Some endpoints need all owned statuses.
Some endpoints need only public Active listings.
The difference must be explicit.
```

---

## Endpoint/controller rules

Controller remains thin.

Controller responsibilities:

```text
Read route id.
Pass command/query to handler.
Map ServiceResult to HTTP response.
```

Controller should not contain:

```text
Ownership checks
Agency member checks
Verification checks
Status transition rules
```

Those belong in handlers/domain methods.

---

## Testing plan

Add integration tests for important API behavior and permission boundaries.

Do not chase fake unit coverage.

Add unit tests only where domain status transitions become meaningful.

---

### Unit tests

Recommended file:

```text
tests/RealEstate.Tests/Unit/Domain/Entities/ListingTests.cs
```

Add tests for:

```text
Publish changes Draft to Active.
Publish keeps Active as Active if idempotent.
Publish throws/fails for Archived.

Unpublish changes Active to Draft.
Unpublish keeps Draft as Draft if idempotent.
Unpublish fails for Archived.

Archive changes Draft to Archived.
Archive changes Active to Archived.
Archive keeps Archived as Archived if idempotent.
```

Exact test style depends on current domain pattern.

---

### Public listing visibility tests

Recommended file:

```text
tests/RealEstate.Tests/Integration/Listings/ListingsEndpointTests.GetAll.cs
```

Tests:

```text
GET /api/listings returns Active listings.
GET /api/listings does not return Draft listings.
GET /api/listings does not return Archived listings.
GET /api/listings totalCount counts only Active listings.
```

---

### Public listing by id tests

Recommended file:

```text
tests/RealEstate.Tests/Integration/Listings/ListingsEndpointTests.GetById.cs
```

Tests:

```text
GET /api/listings/{id} returns 200 for Active listing.
GET /api/listings/{id} returns 404 for Draft listing.
GET /api/listings/{id} returns 404 for Archived listing.
GET /api/listings/{id} returns 404 for missing listing.
```

---

### My listings tests

Recommended file:

```text
tests/RealEstate.Tests/Integration/Listings/ListingsEndpointTests.MyListings.cs
```

Tests:

```text
GET /api/listings/my returns Draft listings owned by current user.
GET /api/listings/my returns Active listings owned by current user.
GET /api/listings/my returns Archived listings owned by current user.
GET /api/listings/my does not return listings owned by another user.
```

---

### Agency public listings tests

Recommended file:

```text
tests/RealEstate.Tests/Integration/Agencies/AgenciesEndpointTests.Listings.cs
```

Tests:

```text
GET /api/agencies/{id}/listings returns Active agency listings.
GET /api/agencies/{id}/listings does not return Draft agency listings.
GET /api/agencies/{id}/listings does not return Archived agency listings.
GET /api/agencies/{id}/listings totalCount counts only Active listings.
Missing agency returns 404.
Existing agency with no Active listings returns empty paged result.
```

---

### Personal publish endpoint tests

Recommended file:

```text
tests/RealEstate.Tests/Integration/Listings/ListingsEndpointTests.Publishing.cs
```

Tests:

```text
No token cannot publish.
Owner can publish own Draft listing.
Owner can publish own already Active listing idempotently.
Owner cannot publish Archived listing.
Non-owner cannot publish another user's listing.
PendingVerification user cannot publish.
Disabled user cannot publish.

Owner can unpublish own Active listing.
Owner can unpublish own Draft listing idempotently.
Owner cannot unpublish Archived listing.
Non-owner cannot unpublish another user's listing.

Owner can archive own Draft listing.
Owner can archive own Active listing.
Owner can archive own Archived listing idempotently.
Non-owner cannot archive another user's listing.
```

---

### Agency publish endpoint tests

Recommended file:

```text
tests/RealEstate.Tests/Integration/Listings/ListingsEndpointTests.AgencyPublishing.cs
```

Tests:

```text
Active Owner can publish agency Draft listing.
Active Agent can publish agency Draft listing.
Disabled member cannot publish agency listing.
Pending member cannot publish agency listing.
Non-member cannot publish agency listing.
PendingVerification agency cannot publish agency listing.
Disabled agency cannot publish agency listing.
Rejected agency cannot publish agency listing.

Active Owner can unpublish agency Active listing.
Active Agent can unpublish agency Active listing.
Disabled member cannot unpublish agency listing.
Non-member cannot unpublish agency listing.

Active Owner can archive agency Draft listing.
Active Agent can archive agency Active listing.
Disabled member cannot archive agency listing.
Non-member cannot archive agency listing.
```

---

## Important implementation watch-outs

### Watch-out 1: current registered users may be PendingVerification

Current registration creates users as `PendingVerification`.

If tests use normal register flow, publish may fail unless test helpers can create or update an `Active` user.

Options:

```text
Use test helper to create Active user directly in database.
Or add test-only setup helper to mark user Active.
Do not add production admin verification endpoint just for tests.
```

---

### Watch-out 2: current agencies start PendingVerification

Current agency creation creates agency as `PendingVerification`.

Agency publish tests need an `Active` agency.

Options:

```text
Use test helper to mark agency Active.
Or seed agency directly as Active in integration test setup.
Do not add production admin approval endpoint in Chapter 8 unless explicitly starting admin verification.
```

---

### Watch-out 3: public filters and pagination

When filtering public listings:

```text
Apply Active visibility filter before pagination.
Apply Active visibility filter before totalCount.
Keep existing search/filter behavior unchanged.
```

---

### Watch-out 4: do not break owner/image rules

Current image actions are owner-based.

Chapter 8 should not accidentally change image management rules.

Existing rule remains:

```text
Only listing creator/owner can manage listing images.
Same-agency members cannot manage another member's listing images yet.
```

Publishing agency listings is different from managing another member's images.

---

### Watch-out 5: do not mix payments into Chapter 8

Chapter 8 does not implement:

```text
payments
subscriptions
listing boosts
paid publishing
agency plans
free listing limit redesign
```

Current free listing limit stays as-is unless explicitly changed later.

---

## Chapter 8 task split

```text
8A — Write publishing/visibility rules doc
8B — Add/update listing status domain methods
8C — Add publish listing command/endpoint
8D — Add unpublish listing command/endpoint
8E — Add archive listing command/endpoint
8F — Filter public GET /api/listings to Active only
8G — Filter public GET /api/listings/{id} to Active only, else 404
8H — Filter public GET /api/agencies/{id}/listings to Active only
8I — Keep GET /api/listings/my showing Draft/Active/Archived
8J — Tests for owner/user verification rules
8K — Tests for agency/member/agency verification rules
8L — Docs/context update
```

---

## Completion checklist

Chapter 8 is complete when:

```text
Rules doc exists.
Listing status domain methods exist.
Publish endpoint exists.
Unpublish endpoint exists.
Archive endpoint exists.
Public listing list shows Active only.
Public listing by id returns 404 for Draft/Archived.
Public agency listings show Active only.
My listings still shows Draft/Active/Archived.
Personal listing publishing permissions are tested.
Agency listing publishing permissions are tested.
User verification publish restriction is tested.
Agency verification publish restriction is tested.
dotnet build passes.
dotnet test passes.
backend-context.md is updated.
```

Final expected context update after Chapter 8:

```text
Chapter 8 completed:
- Public listing APIs show only Active listings.
- Draft/Archived listings are hidden publicly.
- Public GET listing by id returns 404 for hidden listings.
- Added publish/unpublish/archive endpoints.
- Personal listing publishing requires owner + Active user.
- Agency listing publishing requires Active agency member and Active agency.
- Active Owner and Active Agent can publish/unpublish/archive agency listings.
- My listings shows all owned statuses.
- Tests passing: X/X.
```
