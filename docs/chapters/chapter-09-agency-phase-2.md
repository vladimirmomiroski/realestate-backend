# Chapter 9 — Agency Phase 2

## 1. Purpose

Chapter 9 expands the Agencies MVP into a usable agency-management backend.

The chapter adds:

```text
agency invitations
agency member management
agency logo management
platform-admin agency verification
agency dashboard summary
targeted permission and API-contract hardening
integration and domain coverage
```

Chapter 9 intentionally stays below the level of a full CRM, billing system, notification platform, or analytics product.

Detailed implementation is complete through 9K.
9L documentation cleanup is the final chapter task.

## 2. Final implementation status

```text
9A  — Agency Phase 2 rules document: completed
9B  — Agency invitation entity/foundation: completed
9C  — Create agency invitation: completed
9D  — Get agency invitations: completed
9D.1 — Targeted safety cleanup/hardening: completed
9E  — Accept agency invitation by token: completed
9F  — Cancel agency invitation: completed
9G  — Disable agency member: completed
9H  — Change agency member role: completed
9I  — Agency logo upload/delete: completed
9J  — Platform-admin agency verification: completed
9K  — Agency dashboard summary: completed
9L  — Documentation/context/quality-handoff cleanup: in progress
```

Current verification checkpoint:

```text
dotnet build passed
dotnet test passed
416/416 tests passing
```

## 3. Final Chapter 9 scope

Implemented:

```text
Create agency invitation
List agency invitations with optional status filter
Accept agency invitation by token
Cancel agency invitation
Disable agency member
Change agency member role
Upload or replace agency logo
Delete agency logo
Approve agency
Reject agency
Disable agency
Get agency dashboard summary
```

Also completed during the chapter:

```text
shared agency-admin permission checker
platform-admin permission checker
disabled-user permission hardening
invitation response-contract split
listing-create contract cleanup
Manager restriction cleanup
dashboard-summary EF projection
```

## 4. Out of scope

Chapter 9 does not add:

```text
payments
subscriptions
agency plans
agency listing quotas
paid seats
slug update/history/redirects
public agent profiles
public agency staff pages
email delivery
notifications
CRM clients
client notes
advanced analytics
audit-log UI
agency deletion
hard-delete members
hard-delete invitations
dedicated owner-transfer endpoint
refresh tokens
email verification
password reset
reactivate-agency endpoint
verification documents
verification notes
```

These remain separate product or infrastructure decisions.

## 5. Existing rules preserved from earlier chapters

Chapter 9 does not weaken Chapter 8 publishing or visibility behavior.

Preserved rules:

```text
Created agencies start as PendingVerification.
Agency creator becomes an Active Owner.
PendingVerification agencies remain publicly readable for now.
Public listing APIs expose only Active listings.
Only Active agencies can publish agency listings.
Agency unpublish/archive/private-dashboard access does not require Agency.Status Active.
Agency listing work is available to active Owner and active Agent members.
Disabled users remain blocked from protected listing-status and agency/dashboard operations.
Same-agency members still cannot manage another creator's listing images.
```

Important ownership distinction:

```text
CreatedByUserId = user who created the listing
AgencyId        = agency that owns/groups the listing
```

Agency membership permissions do not replace creator ownership for listing-image mutations.

## 6. Final role and status model

### 6.1 User statuses

```text
Active
PendingVerification
Disabled
```

Chapter 9 behavior:

```text
Active users may perform agency actions when membership/role rules pass.
PendingVerification users may prepare agency access and accept invitations.
PendingVerification users may administer an agency when they are an Active Owner member.
PendingVerification users still cannot publish listings.
Disabled users cannot create agencies, create listings, accept invitations, administer agencies, manage members/logos, or use private agency dashboards.
```

### 6.2 Agency statuses

```text
PendingVerification
Active
Disabled
Rejected
```

Behavior:

```text
Only Active agencies can publish agency listings.
Agency status does not block private profile/logo/member/invitation/dashboard management.
Disabled or Rejected agencies may still need private cleanup and management.
Admin status changes do not automatically mutate listing statuses.
```

### 6.3 Agency member roles

```text
Owner
Manager
Agent
```

Final Manager rule:

```text
Manager exists in the enum but has no active Chapter 9 permission set.
Manager cannot administer the agency.
Manager cannot manage agency listings.
Manager cannot view dashboard summary.
Manager cannot be assigned through invitation or normal role-change input.
An existing Manager may be changed to Owner or Agent as a recovery path.
```

### 6.4 Agency member statuses

```text
Active
Pending
Disabled
```

Only `Active` membership can authorize Chapter 9 agency actions.

## 7. Permission architecture

### 7.1 AgencyAdminAccessChecker

Purpose:

```text
shared agency-level administration access
```

Checks:

```text
current user is authenticated and resolvable
current user is not Disabled
agency exists
membership exists
membership status is Active
membership role is Owner
```

Used by owner-admin flows such as:

```text
agency profile update
invitation create/list/cancel
member disable
member role change
agency logo upload/delete
```

### 7.2 AgencyListingAccessChecker

Purpose:

```text
agency listing and private dashboard access
```

Allows:

```text
Active Owner
Active Agent
```

Blocks:

```text
Manager
Pending member
Disabled member
Non-member
```

Publishing may additionally require:

```text
Agency.Status == Active
User.Status == Active
```

Private listing-management and dashboard access do not require an Active agency.

### 7.3 PlatformAdminAccessChecker

Purpose:

```text
global platform administration
```

Requires the persisted user to be:

```text
UserRole.Admin
UserStatus.Active
```

The checker reloads the current user from the database and does not rely only on the role claim embedded in the JWT.

Important separation:

```text
AgencyMemberRole.Owner is agency-local.
UserRole.Admin is platform-global.
Neither implies the other.
```

## 8. Chapter 9 safety cleanup completed during implementation

A targeted cleanup checkpoint was inserted after invitation listing and before the remaining permission-heavy endpoints.

The goal was to stop existing permission and contract drift from spreading.

### 8.1 Agency permission hardening

Completed:

```text
Added AgencyAdminAccessChecker.
Centralized repeated Active Owner administration checks.
Blocked Disabled users from agency creation.
Blocked Disabled users from private agency member reads.
Kept invitation administration behind the shared checker.
Added integration coverage for disabled-user agency behavior.
```

The checker owns shared access resolution only.
Invitation-specific rules remain in invitation handlers/repositories.

### 8.2 Invitation response-contract cleanup

The original shared invitation response was split.

Create response:

```text
AgencyInvitationCreatedResponse
```

Includes:

```text
Token
Code
```

List/accept/cancel response:

```text
AgencyInvitationListItemResponse
```

Does not include:

```text
Token
Code
```

Final exposure rule:

```text
Token and Code are returned only immediately after invitation creation.
They are not exposed by list, accept, or cancel responses.
```

### 8.3 Listing-create permission and contract cleanup

Completed:

```text
Removed Status from CreateListingRequest.
New listings always start as Draft.
CreateListingHandler resolves and reloads the current user.
Missing/unresolvable current user returns 401.
Disabled users cannot create listings.
Agency listing creation uses AgencyListingAccessChecker.
Active Owner and Active Agent may create agency listings.
Manager is blocked.
Agency status does not block creating a Draft agency listing.
```

Publishing remains stricter and requires an Active agency.

### 8.4 Cleanup boundaries

The cleanup did not introduce:

```text
framework changes
repository redesign
generic policy framework
pagination redesign
global error framework
broad test refactor
search redesign
```

## 9. Agency invitation foundation

### 9.1 Entity and table

```text
Entity: AgencyInvitation
Table:  AgencyInvitations
```

Important fields:

```text
Id
AgencyId
Email
NormalizedEmail
Token
Code
Role
Status
InvitedByUserId
AcceptedByUserId
ExpiresAtUtc
AcceptedAtUtc
CancelledAtUtc
CreatedAtUtc
ModifiedAtUtc
```

### 9.2 Statuses

```text
Pending
Accepted
Cancelled
Expired
```

Lifecycle:

```text
Pending -> Accepted
Pending -> Cancelled
Pending -> Expired
```

Accepted, Cancelled, and Expired are terminal in Chapter 9.

### 9.3 Token and code

Final rule:

```text
Token is the only Chapter 9 accept credential.
Invitation ID alone cannot be used to accept.
Code is stored and returned on creation but is reserved for possible future manual-code support.
Chapter 9 does not accept invitations by Code.
```

`Token` has a unique database index.

Chapter 9 does not rely on Code uniqueness because Code is not an active accept credential.

### 9.4 Role assignment

Assignable invitation roles:

```text
Owner
Agent
```

Blocked:

```text
Manager
```

### 9.5 Duplicate protection

Rules:

```text
A second Pending invitation for the same agency + normalized email is rejected.
A user already belonging to the agency cannot accept an invitation into a duplicate membership.
AgencyMember uniqueness protects agency + user membership duplication.
```

## 10. Create agency invitation

Endpoint:

```http
POST /api/agencies/{agencyId}/invitations
```

Response:

```http
201 Created
```

Response contract:

```text
AgencyInvitationCreatedResponse
```

Request:

```text
Email
Role
```

Authorization:

```text
Active Owner membership
current user not Disabled
agency status does not block invitation creation
```

Allowed user status:

```text
Active
PendingVerification
```

Blocked:

```text
No JWT -> 401
Unresolvable current user -> 401
Missing agency -> 404
Non-member -> 403
Agent -> 403
Manager -> 403
Pending/Disabled member -> 403
Disabled user -> 403
Invalid request/role -> 400
Duplicate Pending invitation -> 400
Existing agency member for invited user -> 400
```

Validation:

```text
Email required
Email normalized for comparison
Email length/format validated
Role must be Owner or Agent
Manager rejected
Strong random Token generated
Short random Code generated
Expiration timestamp assigned
```

Create response exposes Token and Code because email delivery is outside Chapter 9.

## 11. List agency invitations

Endpoint:

```http
GET /api/agencies/{agencyId}/invitations
```

Optional filter:

```text
status
```

Response:

```http
200 OK
```

Response contract:

```text
IReadOnlyList<AgencyInvitationListItemResponse>
```

Rules:

```text
Active Owner only
simple non-paged list
optional status filtering
Token and Code are never included
agency status does not block access
```

Blocked authorization and not-found behavior follows `AgencyAdminAccessChecker`.

## 12. Accept agency invitation

Endpoint:

```http
PUT /api/agencies/invitations/accept
```

Request:

```json
{
  "token": "..."
}
```

Response:

```http
200 OK
```

Response contract:

```text
AgencyInvitationListItemResponse
```

Token and Code are not returned.

Allowed:

```text
Authenticated Active user with matching normalized email
Authenticated PendingVerification user with matching normalized email
```

Blocked:

```text
No JWT -> 401
Unresolvable current user -> 401
Missing/blank token -> 400
Disabled user -> 403
Unknown token -> 404
Invitation email mismatch -> 403
Accepted invitation -> 400
Cancelled invitation -> 400
Expired invitation -> 400
Past ExpiresAtUtc while still Pending -> mark Expired, persist, return 400
Already an agency member -> 400
```

On success:

```text
Create Active AgencyMember.
Copy role from invitation.
Associate membership with current user and invitation agency.
Mark invitation Accepted.
Set AcceptedByUserId.
Set AcceptedAtUtc.
Save the membership and invitation state.
```

The accept flow uses token lookup and loads the agency with members for duplicate-membership protection.

It does not use `AgencyAdminAccessChecker` because acceptance is performed by the invited user, not by an existing agency Owner.

## 13. Cancel agency invitation

Endpoint:

```http
PUT /api/agencies/{agencyId}/invitations/{invitationId}/cancel
```

Response:

```http
200 OK
```

Response contract:

```text
AgencyInvitationListItemResponse
```

Authorization:

```text
Active Owner only
```

Resource rules:

```text
Missing agency -> 404
Missing invitation -> 404
Invitation belonging to another agency -> 404
```

Transitions:

```text
Pending -> Cancelled
```

Rejected:

```text
Accepted -> 400
Cancelled -> 400
Expired -> 400
Pending invitation past ExpiresAtUtc -> mark Expired, persist, return 400
```

Cancellation does not remove an already-created membership.

## 14. Disable agency member

Endpoint:

```http
PUT /api/agencies/{agencyId}/members/{memberId}/disable
```

Response:

```http
204 No Content
```

Authorization:

```text
Active Owner only
```

Rules:

```text
Owner cannot disable themselves.
Target member must belong to the requested agency.
Missing target or cross-agency target returns 404.
Active -> Disabled.
Pending -> Disabled.
Disabled -> Disabled idempotent.
No hard delete.
```

Last-owner invariant:

```text
A sole Active Owner cannot disable themselves because self-disable is blocked.
Another Active Owner may disable an Active Owner because the acting Active Owner remains.
```

The disable handler does not perform an active-owner count query.

## 15. Change agency member role

Endpoint:

```http
PUT /api/agencies/{agencyId}/members/{memberId}/role
```

Response:

```http
204 No Content
```

Request:

```text
Role
```

Authorization:

```text
Active Owner only
```

Assignable roles:

```text
Owner
Agent
```

Target rules:

```text
Target member must be Active.
Agent -> Owner allowed.
Owner -> Agent allowed when another Active Owner remains.
Same role is idempotent.
Manager input is rejected.
Existing Manager may be changed to Owner or Agent as a recovery path.
Cross-agency target returns 404.
```

Last-owner invariant:

```text
The last Active Owner cannot be demoted to Agent.
```

Recommended ownership handoff:

```text
Promote another Active member to Owner first.
Then demote the old Owner.
```

The same concurrency risk as member disabling remains deferred to Chapter 11.

## 16. Agency logo management

Endpoints:

```http
PUT    /api/agencies/{agencyId}/logo
DELETE /api/agencies/{agencyId}/logo
```

Authorization:

```text
Active Owner only
current user not Disabled
agency status does not block private logo management
```

### 16.1 Stored metadata

Agency fields:

```text
LogoUrl
LogoStoredFileName
LogoContentType
LogoSizeBytes
```

No separate logo table exists.

### 16.2 Storage

Filesystem path:

```text
src/RealEstate.Api/wwwroot/uploads/agencies/{agencyId}/logo/{storedFileName}
```

Public URL:

```text
/uploads/agencies/{agencyId}/logo/{storedFileName}
```

### 16.3 Validation

```text
Maximum size: 5 MB
Allowed extensions: .jpg, .jpeg, .png, .webp
Allowed MIME types: image/jpeg, image/png, image/webp
Missing file -> 400
Empty file -> 400
Invalid extension/type/size -> 400
```

### 16.4 Upload and replacement

Upload response:

```http
200 OK with AgencyResponse
```

Safe replacement order:

```text
1. Store the new file.
2. Update Agency logo metadata.
3. Save database changes.
4. If save fails, delete the newly stored file and rethrow.
5. After successful save, delete the old stored file.
```

The old logo is not removed before the replacement is safely persisted.

### 16.5 Delete

Delete response:

```http
204 No Content
```

Behavior:

```text
Clear database metadata.
Save the cleared state.
Delete the stored file when present.
No existing logo -> 204 idempotent.
```

## 17. Platform-admin agency verification

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

Response:

```http
200 OK with AgencyResponse
```

Authorization:

```text
UserRole.Admin
UserStatus.Active
```

Blocked:

```text
No JWT -> 401
Unresolvable current user -> 401
Non-admin -> 403
PendingVerification admin -> 403
Disabled admin -> 403
Missing agency -> 404
```

The database user is reloaded through `PlatformAdminAccessChecker`.

### 17.1 Approve

```text
PendingVerification -> Active
Rejected            -> Active
Active              -> Active idempotent
Disabled            -> 400
```

### 17.2 Reject

```text
PendingVerification -> Rejected
Rejected            -> Rejected idempotent
Active              -> 400
Disabled            -> 400
```

### 17.3 Disable

```text
PendingVerification -> Disabled
Active              -> Disabled
Rejected            -> Disabled
Disabled            -> Disabled idempotent
```

These transitions are implemented as domain methods on `Agency`.

No reactivation endpoint exists in Chapter 9.

### 17.4 Listing effect

```text
Only Active agencies can publish new agency listings.
Changing Agency.Status does not automatically archive or unpublish existing listings.
Public listing visibility remains controlled by Listing.Status.
Private unpublish/archive/dashboard management remains available under existing permission rules.
```

## 18. Agency dashboard summary

Endpoint:

```http
GET /api/agencies/{agencyId}/dashboard/summary
```

Response:

```http
200 OK with AgencyDashboardSummaryResponse
```

Authorization:

```text
Active Owner
Active Agent
```

Blocked:

```text
No JWT -> 401
Unresolvable current user -> 401
Missing agency -> 404
Non-member -> 403
Manager -> 403
Pending member -> 403
Disabled member -> 403
Disabled user -> 403
```

User status:

```text
PendingVerification current user is allowed when membership is Active Owner or Active Agent.
```

Agency status:

```text
PendingVerification, Active, Disabled, and Rejected agencies may all be viewed privately.
```

Response fields:

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

Count semantics:

```text
TotalListings
- all listings where Listing.AgencyId equals the requested agency

DraftListings
- requested agency + ListingStatus.Draft

ActiveListings
- requested agency + ListingStatus.Active

ArchivedListings
- requested agency + ListingStatus.Archived

MembersCount
- all AgencyMember rows for the requested agency

ActiveMembersCount
- requested agency + AgencyMemberStatus.Active

PendingInvitationsCount
- requested agency
- AgencyInvitationStatus.Pending
- ExpiresAtUtc > utcNow
```

Important boundary:

```text
ExpiresAtUtc == utcNow is not actionable and is not counted.
```

Query architecture:

```text
one read-only EF projection
database-side scalar Count subqueries
one database round trip expected
no Include
no collection loading
no N+1
DateTime.UtcNow passed into repository as utcNow
```

Current conclusion:

```text
Raw SQL is not justified.
Existing AgencyId indexes are sufficient at this stage.
```

## 19. Persistence changes

### 19.1 AgencyInvitations

Added:

```text
AgencyInvitations table
AgencyInvitationStatus enum
AgencyInvitation EF configuration
repository abstraction/implementation
```

Important persisted fields:

```text
agency relationship
inviter relationship
optional accepter relationship
original and normalized email
token
code
role
status
expiry
accepted/cancelled timestamps
auditing timestamps
```

Important indexes/constraints:

```text
unique index on Token
index supporting agency lookup
index supporting normalized-email lookup
existing AgencyMember agency+user uniqueness remains authoritative for duplicate membership
```

Duplicate Pending invitations are also prevented by application-level existence checks.

### 19.2 Agency logo metadata

Added to `Agencies`:

```text
LogoStoredFileName
LogoContentType
LogoSizeBytes
```

Existing `LogoUrl` remains.

### 19.3 Auditing

`AgencyInvitation` participates in the existing auditing model:

```text
CreatedAtUtc
ModifiedAtUtc
```

Business timestamps remain explicit:

```text
ExpiresAtUtc
AcceptedAtUtc
CancelledAtUtc
```

## 20. Architecture rules confirmed by Chapter 9

### Controllers

Controllers:

```text
read route/query/body/form input
call handlers
map ServiceResult to HTTP responses
```

Controllers do not contain:

```text
role checks
membership-status checks
last-owner checks
invitation transitions
admin transitions
file-storage logic
EF Core queries
```

### Handlers

Handlers:

```text
resolve current user
apply status and permission rules
load tracked/read-only data
coordinate cross-entity rules
call domain transitions
save through repositories
return DTOs through ServiceResult
```

### Domain

Domain methods own local transitions such as:

```text
AgencyInvitation.Accept
AgencyInvitation.Cancel
AgencyInvitation.MarkExpired
AgencyMember.Disable
AgencyMember.ChangeRole
Agency.SetLogo
Agency.RemoveLogo
Agency.Approve
Agency.Reject
Agency.Disable
```

Cross-entity rules such as counting Active Owners remain in application handlers because they require repository data.

### Repositories

Repositories remain data-focused.

Good Chapter 9 responsibilities:

```text
get invitation by token for update
get invitation by id for update
list invitations
check duplicate Pending invitation
load agency with members
get member for update
count Active Owners
project dashboard summary
save changes
```

Authorization decisions remain outside repositories.

## 21. HTTP error behavior

```text
401 Unauthorized
- no/invalid JWT
- current user ID or database user cannot be resolved

403 Forbidden
- authenticated user lacks required role/status
- current user is Disabled
- invitation email does not match current user
- non-admin uses platform-admin endpoint

404 Not Found
- agency does not exist
- invitation/token/member does not exist
- target resource belongs to another agency

400 Bad Request
- validation failure
- duplicate Pending invitation
- already-member acceptance
- invalid invitation state
- expired invitation
- self-disable
- last-owner disable/demotion
- invalid role
- invalid agency status transition
- invalid logo file

201 Created
- invitation created

200 OK
- invitation list
- invitation accept
- invitation cancel
- logo upload/replace
- admin verification transition
- dashboard summary

204 No Content
- member disable
- member role change
- logo delete
```

## 22. Test coverage

Chapter 9 integration and domain coverage includes:

```text
authorization: 401/403 boundaries
resource isolation: 404 for cross-agency targets
Disabled-user restrictions
PendingVerification-user allowed flows
Owner/Agent/Manager separation
invitation create/list/accept/cancel
Token-only acceptance
Token/Code exposure rules
email matching
invitation expiry and terminal states
duplicate membership protection
member disable and idempotency
role changes and idempotency
last Active Owner protection
logo validation, persistence, replacement, cleanup, and deletion
platform-admin separation and status transitions
dashboard summary permissions, status independence, counts, and data isolation
```

Dashboard-summary tests also prove exclusion of:

```text
personal listings
other-agency listings
other-agency invitations
Accepted invitations
Cancelled invitations
Expired invitations
expired-but-still-Pending invitations
```

Final test checkpoint:

```text
416/416 passing
```

## 23. Known deferred risks and decisions

### 23.1 Last-owner concurrency

Owner -> Agent demotion uses an application-level active-owner count.

Risk:

```text
Two concurrent role-change operations could both observe a safe count and then demote owners.
```

Deferred to:

```text
Chapter 11 — Data Integrity and Targeted Hardening
```

### 23.2 Manager permissions

Manager remains intentionally restricted.

Do not expand Manager behavior without explicit product rules and tests.

### 23.3 Invitation expiration consistency

Acceptance, cancellation, and dashboard summary treat an expired timestamp as non-actionable.

Invitation rows may remain `Pending` until an action marks them `Expired`.

The dashboard summary deliberately counts only:

```text
Status == Pending && ExpiresAtUtc > utcNow
```

A broader automatic-expiration strategy is deferred.

### 23.4 Email delivery

Chapter 9 creates invitation credentials but does not deliver email.

Email/background processing belongs to a later chapter.

### 23.5 Agency reactivation

No reactivate endpoint exists.

`Approve` cannot reactivate a Disabled agency.

### 23.6 Automatic listing status changes

Agency disable/reject does not mutate listing statuses.

Any moderation policy for bulk unpublish/archive must be designed separately.

## 24. Chapter completion summary

Chapter 9 completed the following backend capabilities:

```text
Agency invitation lifecycle
Owner-only invitation administration
Token-only invitation acceptance
Active membership creation
Member soft-disable
Member role changes
Last-owner protection
Agency logo storage and lifecycle
Active platform-admin agency verification
Agency dashboard summary
Shared agency-admin access enforcement
Platform-admin separation
Permission and DTO cleanup
PostgreSQL-backed integration coverage
```

Chapter 9 preserved:

```text
thin controllers
use-case-focused handlers
domain-owned local transitions
data-focused repositories
feature-level DTO/read-model conventions
existing listing publishing and visibility rules
```

Chapter 9 intentionally did not add billing, email delivery, CRM, advanced analytics, or frontend-specific infrastructure.

## 25. 9L documentation closeout

9L updates:

```text
docs/backend-context.md
docs/chapters/chapter-09-agency-phase-2.md
docs/backend-quality-handoff.md
```

9L goals:

```text
record final implemented behavior
remove planned/recommended wording that no longer applies
remove resolved quality findings
keep only real deferred risks
record 416/416 test checkpoint
align roadmap across documentation
```

## 26. Roadmap after Chapter 9

```text
Chapter 10 — Search and Discovery Phase 2

Chapter 11 — Data Integrity and Targeted Hardening

Chapter 12 — API Consistency, Observability, and Frontend Readiness

Then begin frontend development

Chapter 13 — Authentication and Account Security Phase 2

Chapter 14 — Background Jobs and Notifications

Chapter 15 — Agency Workspace Phase 3
```

Chapters 13–15 are later plans. Their order and scope may change after frontend work and real workflow feedback.
