# Chapter 8 — Publishing, Visibility, and Verification Rules

## Purpose

Chapter 8 defines how listings become public, how hidden listings behave, and how user/agency verification affects publishing and management access.

This chapter was rule-first, then code.

The goal was to prevent confusion between:

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

## Final status

Chapter 8 is completed.

Completed tasks:

```text
8A — Write publishing/visibility rules doc
8B — Add/update listing status domain methods
8C — Add publish listing command/endpoint
8D — Add unpublish listing command/endpoint
8E — Add archive listing command/endpoint
8F — Restrict public listing visibility to Active listings
8G — Add agency dashboard listings endpoint
8H — Extract agency listing access checker
8I — Docs/context update
```

Final tests:

```text
204/204 passing
```

---

## Core invariant

Public listing APIs show only `Active` listings.

`Draft` and `Archived` listings are hidden publicly.

Public `GET /api/listings/{id}` returns:

```http
404 Not Found
```

for missing, `Draft`, `Archived`, or otherwise non-public listings.

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

## Listing status visibility

Main Chapter 8 statuses:

```text
Draft
Active
Archived
```

Visibility rules:

```text
Draft    -> private / dashboard only
Active   -> public
Archived -> hidden from public flow
```

Other listing statuses are also not public unless explicitly made public later:

```text
Reserved
Sold
Rented
```

Current public rule:

```text
Only Active is public.
Everything else is hidden from public listing APIs.
```

---

## Public endpoints

These public endpoints expose only `Active` listings:

```http
GET /api/listings
GET /api/listings/{id}
GET /api/agencies/{id}/listings
```

---

## GET /api/listings

Rules:

```text
Returns only Active listings.
Does not return Draft listings.
Does not return Archived listings.
Keeps existing filters/pagination behavior.
```

Important implementation rule:

```text
Visibility filtering is applied before pagination and totalCount.
```

So `totalCount` counts only public-visible listings.

Implementation:

```text
ListingRepository.GetFilteredReadOnlyAsync filters ListingStatus.Active before count/pagination.
```

---

## GET /api/listings/{id}

Rules:

```text
Active listing     -> 200 OK
Draft listing      -> 404 Not Found
Archived listing   -> 404 Not Found
Reserved listing   -> 404 Not Found
Sold listing       -> 404 Not Found
Rented listing     -> 404 Not Found
Missing listing    -> 404 Not Found
```

Reason:

```text
Public users should not be able to distinguish between missing and hidden listings.
```

---

## GET /api/agencies/{id}/listings

This is the public agency profile listings endpoint.

Rules:

```text
Existing agency + Active listings only -> 200 OK
Existing agency + no Active listings -> 200 OK with empty paged result
Missing agency -> 404 Not Found
```

Important:

```text
Agency existence is still checked.
Listing visibility is filtered to Active only.
```

This endpoint does not show agency Draft or Archived listings.

---

## Personal owner dashboard endpoint

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

## Agency dashboard listings endpoint

Chapter 8 added a private agency management endpoint:

```http
GET /api/agencies/{id}/dashboard/listings
```

Query parameters:

```text
lang
status
page
pageSize
```

Example:

```http
GET /api/agencies/{id}/dashboard/listings?lang=en&status=Draft&page=1&pageSize=20
```

Rules:

```text
Requires JWT.
Returns agency listings for management.
Can return Draft listings.
Can return Active listings.
Can return Archived listings.
Can optionally filter by ListingStatus.
Does not use public Active-only filtering.
```

Purpose:

```text
Agency members need a private dashboard view for managing all agency listings.
```

This completes the visibility split:

```text
Public users -> Active only
Personal owner -> own Draft / Active / Archived
Agency members -> agency Draft / Active / Archived
```

---

## Publishing endpoints

Chapter 8 added:

```http
PUT /api/listings/{id}/publish
PUT /api/listings/{id}/unpublish
PUT /api/listings/{id}/archive
```

All three require JWT.

No restore endpoint was added in Chapter 8.

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

Idempotent:

```text
Active -> Active
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

Idempotent:

```text
Draft -> Draft
```

Not allowed:

```text
Archived -> Draft
```

Behavior:

```text
Active listing + allowed user -> 200 OK
Draft listing + allowed user -> 200 OK idempotent
Archived listing + allowed user -> 400 Bad Request
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

Idempotent:

```text
Archived -> Archived
```

Behavior:

```text
Draft listing + allowed user -> 200 OK
Active listing + allowed user -> 200 OK
Archived listing + allowed user -> 200 OK idempotent
Reserved/Sold/Rented listing + allowed user -> 400 Bad Request
```

Reason:

```text
Archive is a safe final hidden state action.
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
```

Personal publish:

```text
Requires owner.
Requires User.Status = Active.
PendingVerification user cannot publish.
Disabled user cannot publish.
```

Personal unpublish:

```text
Requires owner.
Blocked if User.Status = Disabled.
PendingVerification user can unpublish.
Active user can unpublish.
```

Personal archive:

```text
Requires owner.
Blocked if User.Status = Disabled.
PendingVerification user can archive.
Active user can archive.
```

Reason:

```text
A user who cannot publish should still be able to remove/hide their listing unless the account is Disabled.
```

---

## Agency listing rules

An agency listing is:

```text
Listing.AgencyId != null
```

Agency listing permissions are based on current agency membership, not only `CreatedByUserId`.

Reason:

```text
A user may have created an agency listing in the past but later lost agency access.
Current AgencyMember access decides whether the user can manage agency-owned listings.
```

Allowed agency listing roles:

```text
Active Owner
Active Agent
```

Not allowed:

```text
Non-member
Pending member
Disabled member
Other unsupported roles
```

Important permission difference:

```text
Agency profile update -> Active Owner only
Agency listing management -> Active Owner or Active Agent
```

Reason:

```text
Agents need to manage listings.
Agency profile control should stay owner-only.
```

---

## Agency publish rules

Agency publish requires:

```text
User.Status = Active
Agency.Status = Active
AgencyMember.Status = Active
AgencyMember.Role = Owner or Agent
```

Blocked:

```text
PendingVerification user
Disabled user
PendingVerification agency
Disabled agency
Rejected agency
Pending agency member
Disabled agency member
Non-member
```

Reason:

```text
Publishing exposes content publicly, so both the user and agency must be verified/active.
```

---

## Agency unpublish rules

Agency unpublish requires:

```text
User.Status != Disabled
Agency exists
AgencyMember.Status = Active
AgencyMember.Role = Owner or Agent
```

Agency status does not block unpublish.

Allowed even if agency is:

```text
PendingVerification
Disabled
Rejected
```

Reason:

```text
Unpublish hides content. Active agency members should be able to remove public visibility even if the agency is no longer Active.
```

---

## Agency archive rules

Agency archive requires:

```text
User.Status != Disabled
Agency exists
AgencyMember.Status = Active
AgencyMember.Role = Owner or Agent
```

Agency status does not block archive.

Allowed even if agency is:

```text
PendingVerification
Disabled
Rejected
```

Reason:

```text
Archive hides/removes listing from public flow. Active agency members should be able to clean up old agency listings.
```

---

## Agency dashboard listings rules

Agency dashboard listings require:

```text
User.Status != Disabled
Agency exists
AgencyMember.Status = Active
AgencyMember.Role = Owner or Agent
```

Agency status does not block dashboard viewing.

Allowed even if agency is:

```text
PendingVerification
Disabled
Rejected
```

Reason:

```text
Active agency members may still need to view/manage old listings even if the agency is not currently publishable.
```

---

## User status rules

Current user statuses:

```text
Active
Disabled
PendingVerification
```

Publishing:

```text
Active -> can publish if ownership/member rules pass
PendingVerification -> cannot publish
Disabled -> cannot publish
```

Unpublish/archive/dashboard management:

```text
Active -> allowed if ownership/member rules pass
PendingVerification -> allowed for hide/manage actions if ownership/member rules pass
Disabled -> blocked
```

Reason:

```text
Pending users can prepare or hide data.
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

Agency publishing:

```text
Active -> agency listings can be published if member rules pass
PendingVerification -> cannot publish agency listings
Disabled -> cannot publish agency listings
Rejected -> cannot publish agency listings
```

Agency unpublish/archive/dashboard:

```text
Agency status does not block these actions.
```

Reason:

```text
Only publishing exposes content publicly.
Unpublish/archive/dashboard management can be needed even when the agency is not Active.
```

Chapter 8 does not change public agency profile visibility:

```http
GET /api/agencies/{id}
GET /api/agencies/by-slug/{slug}
```

Chapter 8 changes listing visibility, not agency profile visibility.

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
Any non-Active listing by id
```

Reason:

```text
Public users should not know whether a hidden listing exists.
```

---

### Protected listing action endpoints

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
Pending member tries agency listing action
PendingVerification user tries to publish
PendingVerification agency tries to publish
Disabled agency tries to publish
Rejected agency tries to publish
```

Use:

```http
404 Not Found
```

When:

```text
Listing does not exist
Agency does not exist when required for agency listing access
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
Reserved/Sold/Rented -> Archived archive attempt
```

---

## Domain method rules

Domain methods exist on `Listing`:

```csharp
public void Publish()
public void Unpublish()
public void Archive()
```

Domain method responsibility:

```text
Handle valid listing status transition.
Reject invalid listing status transition.
```

Domain method must not check:

```text
current user
agency membership
JWT
repositories
database
```

Reason:

```text
Listing knows how its own status can change.
Handlers know who is allowed to request the change.
```

Responsibility split:

```text
Handler:
- load listing
- load current user
- check owner/member/verification permissions
- call listing.Publish(), listing.Unpublish(), or listing.Archive()
- save changes

Listing entity:
- change Draft -> Active
- reject Archived -> Active
- change Active -> Draft
- change Draft/Active -> Archived
```

---

## Application/handler rules

Handlers contain application-level action flow.

Implemented handlers:

```text
PublishListingHandler
UnpublishListingHandler
ArchiveListingHandler
GetAgencyDashboardListingsHandler
```

Implemented commands:

```text
PublishListingCommand
UnpublishListingCommand
ArchiveListingCommand
```

Folder structure:

```text
src/RealEstate.Application/Listings/Commands/PublishListing
src/RealEstate.Application/Listings/Commands/UnpublishListing
src/RealEstate.Application/Listings/Commands/ArchiveListing
src/RealEstate.Application/Agencies/Queries/GetAgencyDashboardListings
```

Handlers keep action-specific logic explicit:

```text
current user lookup
user status rule
listing lookup
personal owner check
domain status transition
save changes
response mapping
```

Shared agency listing access logic was extracted to:

```text
AgencyListingAccessChecker
```

---

## AgencyListingAccessChecker

Chapter 8 added an application-level helper:

```text
src/RealEstate.Application/Agencies/Permissions/AgencyListingAccessChecker.cs
```

Purpose:

```text
Centralize repeated agency listing access checks.
Avoid permission drift between publish/unpublish/archive/dashboard handlers.
```

It handles:

```text
Agency exists
Agency must be Active when publishing
Active agency member required
Owner or Agent role required
```

It supports:

```text
EnsureCanPublishAgencyListingsAsync
EnsureCanManageAgencyListingsAsync
```

Important:

```text
Publish uses active-agency check.
Unpublish/archive/dashboard use management check without active-agency requirement.
```

Handlers still keep:

```text
User.Status rule
Personal ownership rule
Listing status transition rule
```

Reason:

```text
Publishing, unpublishing, archiving, and dashboard viewing have different user/action rules.
Only the repeated agency access logic was extracted.
```

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

Good repository/data methods:

```text
GetByIdForUpdateAsync
GetByIdReadOnlyAsync
GetFilteredReadOnlyAsync
GetByCreatedByUserIdAsync
GetByAgencyIdForDashboardReadOnlyAsync
CountByCreatedByUserIdAsync
GetMemberAccessReadOnlyAsync
GetByIdReadOnlyAsync
ExistsAsync
```

The handler/checker decides:

```text
Is the user owner?
Is the user Active?
Is the agency Active?
Is the member Active?
Is the role Owner or Agent?
```

---

## Public query implementation rule

Public listing queries must apply visibility before count and pagination.

Current implementation:

```text
GetFilteredReadOnlyAsync filters ListingStatus.Active before CountAsync, Skip, and Take.
```

Private/dashboard queries must not accidentally reuse public visibility rules when they need hidden statuses.

Private examples:

```text
GET /api/listings/my
GET /api/agencies/{id}/dashboard/listings
```

These can return non-Active statuses.

---

## Endpoint/controller rules

Controllers remain thin.

Controller responsibilities:

```text
Read route/query/body input.
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

Those belong in handlers, domain methods, or small application permission helpers.

---

## Testing summary

Chapter 8 added/updated tests for:

```text
Listing status domain methods.
Publish endpoint.
Unpublish endpoint.
Archive endpoint.
Public listing Active-only visibility.
Public listing by id 404 for hidden statuses.
My listings still showing Draft/Active/Archived.
Public agency listings showing Active only.
Agency dashboard listings showing Draft/Active/Archived.
Agency dashboard status filter.
Personal permission rules.
Agency/member permission rules.
User verification restrictions.
Agency verification restrictions.
Permission cleanup behavior remaining unchanged.
```

Final test count:

```text
204/204 passing
```

---

## Important implementation watch-outs

### Watch-out 1: registered users may be PendingVerification

Registration creates users as `PendingVerification`.

Publish tests need an `Active` user.

Test helpers can mark users Active directly in integration setup.

Do not add production admin verification endpoints just for tests.

---

### Watch-out 2: agencies may start PendingVerification

Agency creation can create agencies as `PendingVerification`.

Agency publish tests need an `Active` agency.

Test helpers can mark agencies Active directly in integration setup.

Do not add production admin approval endpoints in Chapter 8.

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

Chapter 8 does not change image management rules.

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

Current free listing limit stays as-is.

---

## Final endpoint summary

Public listing endpoints:

```http
GET /api/listings
GET /api/listings/{id}
GET /api/agencies/{id}/listings
```

Private listing/dashboard endpoints:

```http
GET /api/listings/my
GET /api/agencies/{id}/dashboard/listings
```

Protected status action endpoints:

```http
PUT /api/listings/{id}/publish
PUT /api/listings/{id}/unpublish
PUT /api/listings/{id}/archive
```

---

## Completion checklist

Chapter 8 is complete because:

```text
Rules doc exists.
Listing status domain methods exist.
Publish endpoint exists.
Unpublish endpoint exists.
Archive endpoint exists.
Public listing list shows Active only.
Public listing by id returns 404 for Draft/Archived/non-Active.
Public agency listings show Active only.
My listings still shows Draft/Active/Archived.
Agency dashboard listings show Draft/Active/Archived.
Agency dashboard listings support optional status filter.
Personal listing publishing permissions are tested.
Agency listing publishing permissions are tested.
User verification publish restriction is tested.
Agency verification publish restriction is tested.
Agency listing access checker was extracted.
dotnet build passes.
dotnet test passes: 204/204.
backend-context.md is updated after this chapter.
```

---

## Final Chapter 8 context summary

```text
Chapter 8 completed:
- Public listing APIs show only Active listings.
- Draft/Archived listings are hidden publicly.
- Public GET listing by id returns 404 for hidden/non-Active listings.
- Added publish/unpublish/archive endpoints.
- Personal listing publishing requires owner + Active user.
- Personal unpublish/archive require owner and block Disabled users.
- Agency listing publishing requires Active user, Active agency, Active agency member, and Owner/Agent role.
- Agency unpublish/archive require active agency member Owner/Agent, but agency status does not block hiding actions.
- Added private agency dashboard listings endpoint.
- Agency dashboard listings show Draft/Active/Archived to active Owner/Agent members.
- My listings shows all owned statuses.
- AgencyListingAccessChecker centralizes repeated agency listing permission checks.
- Tests passing: 204/204.
```
