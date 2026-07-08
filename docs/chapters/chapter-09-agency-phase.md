# Chapter 9 — Agency Phase 2 Rules

## Purpose

Chapter 9 expands the agency system after the Agencies MVP foundation.

This chapter adds the minimum backend functionality needed for real agency management before frontend work starts.

It focuses on:

```text
Agency invitations
Agency member management
Agency logo upload/delete
Admin agency verification
Agency dashboard summary
Integration tests
```

This chapter must stay practical. It is not a payments, CRM, notifications, email, or advanced analytics chapter.

---

## Implementation status

Current status:

```text
Planned / rules-first stage
```

Chapter 9 starts with this rules document before implementation.

Reason:

```text
Agency Phase 2 touches permissions, verification, public exposure, file storage, and member access.
Those rules must be locked before code.
```

---

## Chapter task split

Recommended implementation order:

```text
9A — Agency Phase 2 rules document
9B — Agency invitation entity/foundation
9C — Invite agency member
9D — Get agency invitations
9E — Accept agency invitation by token/code
9F — Cancel agency invitation
9G — Disable agency member
9H — Change agency member role
9I — Agency logo upload/delete
9J — Admin agency verification endpoints
9K — Agency dashboard summary
9L — Docs/context update
```

---

## Final scope

Chapter 9 includes:

```text
Create agency invitation
List agency invitations
Accept agency invitation by token/code
Cancel agency invitation
Disable agency member
Change agency member role
Upload/replace agency logo
Delete agency logo
Approve agency
Reject agency
Disable agency
Get agency dashboard summary
```

---

## Out of scope

Chapter 9 intentionally does not add:

```text
Payments
Subscriptions
Agency plans
Agency listing limits
Slug update
Slug redirect/history strategy
Public agent profiles
Public agency staff pages
Email sending
CRM
Client notes
Notifications
Advanced analytics
Audit log UI
Agency deletion
Hard-delete members
Hard-delete invitations
Owner transfer as a separate complex flow
Refresh tokens
Email verification
Password reset
```

Reason:

```text
Chapter 9 should make agencies manageable without turning into billing, CRM, email, notifications, or advanced admin.
```

---

## Existing agency rules that must remain unchanged

Current agency behavior remains valid:

```text
Created agencies start as PendingVerification.
Agency creator becomes Active Owner.
PendingVerification agencies are publicly readable for now.
Agency profile update requires Active Owner.
Agency dashboard listings require active Owner/Agent and user not Disabled.
Agency dashboard listing access does not require Agency.Status Active.
Only Active agencies can publish agency listings.
Agency unpublish/archive/dashboard management does not require Active agency.
```

Important:

```text
Chapter 9 must not weaken Chapter 8 publishing/visibility rules.
```

---

## Core permission model

Current agency member roles:

```text
Owner
Agent
```

Current agency member statuses:

```text
Active
Pending
Disabled
```

Chapter 9 permission baseline:

```text
Active Owner -> can manage agency profile, logo, invitations, members, roles, and dashboard.
Active Agent -> can manage agency listings and view dashboard summary, but cannot manage members/invitations/agency profile/logo.
Pending member -> no agency management access.
Disabled member -> no agency management access.
Non-member -> no agency management access.
Disabled user -> blocked from protected agency mutations and private dashboard access.
```

Reason:

```text
Owner controls agency-level administration.
Agent controls listing work only.
```

---

## User status rules

Current user statuses:

```text
Active
PendingVerification
Disabled
```

Chapter 9 rules:

```text
Active user -> can perform agency actions if membership/role rules pass.
PendingVerification user -> can accept invitations and prepare agency membership, but cannot publish listings due to Chapter 8 rules.
Disabled user -> cannot mutate agency data, accept invitations, manage members, manage logo, or view private agency dashboard summary.
```

Reason:

```text
PendingVerification users may prepare their account and agency access.
Disabled users should not perform protected agency operations.
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

Chapter 9 agency status behavior:

```text
PendingVerification agency -> owners can manage profile/logo/members/invitations/dashboard, but cannot publish listings.
Active agency -> normal agency operation.
Disabled agency -> owners/agents may still view/manage private dashboard data where allowed, but cannot publish listings.
Rejected agency -> owners/agents may still view/manage private dashboard data where allowed, but cannot publish listings.
```

Admin status changes are intentionally small in Chapter 9:

```text
Approve agency
Reject agency
Disable agency
```

No reactivation endpoint is added in Chapter 9.

Reason:

```text
Publishing is public exposure and requires Active agency.
Private cleanup/management can still be needed for non-active agencies.
Admin verification should stay small until real admin workflows exist.
```

---

# 9B — Agency invitation foundation

## New entity

Add a new entity:

```text
AgencyInvitation
```

Recommended table:

```text
AgencyInvitations
```

Recommended fields:

```text
Id
AgencyId
Email
NormalizedEmail
Role
Status
Token
Code
InvitedByUserId
AcceptedByUserId
CreatedAtUtc
ModifiedAtUtc
ExpiresAtUtc
AcceptedAtUtc
CancelledAtUtc
```

Field notes:

```text
Email -> original invited email.
NormalizedEmail -> uppercase/lowercase-normalized value for matching and duplicate checks.
Role -> role that will be assigned if accepted.
Status -> invitation lifecycle state.
Token/Code -> accept credential. Accept flow must not rely only on invitation id.
InvitedByUserId -> owner who created the invitation.
AcceptedByUserId -> user who accepted the invitation.
ExpiresAtUtc -> optional expiration support.
AcceptedAtUtc -> set when accepted.
CancelledAtUtc -> set when cancelled.
```

Important token/code rule:

```text
Invitation id is not enough for accepting an invitation.
Accept flow must use invitation Token/Code.
```

Reason:

```text
An invitation id can be guessed or leaked more easily from internal routes/logs.
A random token/code acts as the actual invitation credential.
```

Implementation note:

```text
Use either Token or Code consistently in the final implementation.
The rules document allows both names because the exact API shape can be chosen during implementation.
Do not require both if one strong random accept credential is enough.
```

Recommended practical choice:

```text
Use Token for API/link style acceptance.
Use Code only if later adding short manual invite codes.
```

No email sending is added in Chapter 9.

Reason:

```text
The backend can create and expose the invitation token/code to the owner for now.
Actual email delivery is a separate notification/email chapter.
```

---

## Invitation statuses

Add enum:

```text
AgencyInvitationStatus
```

Values:

```text
Pending
Accepted
Cancelled
Expired
```

Behavior:

```text
Pending -> can be accepted or cancelled.
Accepted -> final successful state.
Cancelled -> final owner-cancelled state.
Expired -> final expired state.
```

---

## Invitation role values

Invitation role should use existing agency member roles:

```text
Owner
Agent
```

MVP recommendation:

```text
Owners can invite Agents.
Owners can invite Owners only if last-owner safety rules are implemented and tested.
```

Simpler implementation option:

```text
Allow Owner and Agent invitations, but protect last-owner rules in member role/disable endpoints.
```

Preferred Chapter 9 rule:

```text
Allow invitations for Owner and Agent.
```

Reason:

```text
Multi-owner agencies are useful, but ownership safety is handled by disable/demotion rules.
```

---

## Duplicate invitation/member rules

Rules:

```text
Cannot create a pending invitation for the same agency + normalized email if one already exists.
Cannot accept an invitation if the accepting user is already an active member of the agency.
Cannot create duplicate AgencyMember rows for the same agency + user.
```

Recommended behavior:

```text
Duplicate pending invitation -> 400 Bad Request.
Already member during accept -> 400 Bad Request or idempotent success, define in implementation.
```

Preferred Chapter 9 rule:

```text
Already member during accept -> 400 Bad Request.
```

Reason:

```text
This catches incorrect flows early and avoids silently hiding duplicate access bugs.
```

---

# 9C — Invite agency member

## Endpoint

Recommended endpoint:

```http
POST /api/agencies/{agencyId}/invitations
```

Auth:

```text
Requires JWT.
```

Request:

```text
Email
Role
```

Response:

```text
AgencyInvitationResponse
```

Response should include:

```text
Id
AgencyId
Email
Role
Status
Token or Code
InvitedByUserId
CreatedAtUtc
ExpiresAtUtc
```

Important:

```text
Returning Token/Code is acceptable in Chapter 9 because no email sending exists yet.
Frontend/admin can display/copy it during development.
```

Later, when email sending exists:

```text
Token/Code should not be repeatedly exposed except immediately after creation or through a controlled resend flow.
```

---

## Permission rules

Allowed:

```text
Active Owner
```

Blocked:

```text
No token -> 401
Non-member -> 403
Active Agent -> 403
Pending member -> 403
Disabled member -> 403
Disabled user -> 403
Missing agency -> 404
```

Reason:

```text
Inviting members changes agency access and must be owner-level.
```

---

## Validation rules

Request validation:

```text
Email is required.
Email must be valid enough for current project standards.
Role is required.
Role must be Owner or Agent.
```

Duplicate checks:

```text
Same agency + normalized email already has Pending invitation -> 400.
Same agency + user already member -> 400, if invited email belongs to existing user and membership can be resolved.
```

Token/code generation:

```text
Generate a strong random token/code on create.
Token/code must be unique enough to use as accept credential.
```

---

# 9D — Get agency invitations

## Endpoint

Recommended endpoint:

```http
GET /api/agencies/{agencyId}/invitations
```

Auth:

```text
Requires JWT.
```

Query parameters:

```text
status optional
page optional
pageSize optional
```

MVP option:

```text
Return all invitations for the agency without pagination if count is expected to be small.
```

Preferred Chapter 9 rule:

```text
Use simple list, no pagination, unless current repository/test patterns make pagination easier.
```

Reason:

```text
Agency invitations are low-volume data.
Do not overbuild.
```

---

## Permission rules

Allowed:

```text
Active Owner
```

Blocked:

```text
No token -> 401
Non-member -> 403
Active Agent -> 403
Pending member -> 403
Disabled member -> 403
Disabled user -> 403
Missing agency -> 404
```

Reason:

```text
Invitations expose pending access to the agency.
This is owner-level administration data.
```

---

# 9E — Accept agency invitation by token/code

## Endpoint

Recommended endpoint:

```http
PUT /api/agencies/invitations/accept
```

Alternative endpoint:

```http
PUT /api/agencies/invitations/{tokenOrCode}/accept
```

Preferred Chapter 9 endpoint:

```http
PUT /api/agencies/invitations/accept
```

Request:

```text
Token or Code
```

Auth:

```text
Requires JWT.
```

Reason:

```text
Token/code should be treated as the invitation credential.
Invitation id alone should not be enough to accept.
```

---

## Accept rules

Allowed:

```text
Authenticated Active user whose normalized email matches invitation NormalizedEmail.
Authenticated PendingVerification user whose normalized email matches invitation NormalizedEmail.
```

Blocked:

```text
No token/JWT -> 401
Disabled user -> 403
Token/code not found -> 404
Invitation email does not match current user email -> 403
Invitation is Cancelled -> 400
Invitation is Accepted -> 400
Invitation is Expired -> 400
Invitation has passed ExpiresAtUtc -> 400 and may be marked Expired
Already a member of the agency -> 400
```

On successful accept:

```text
Create AgencyMember.
Set AgencyMember.AgencyId from invitation.
Set AgencyMember.UserId from current user.
Set AgencyMember.Role from invitation.
Set AgencyMember.Status = Active.
Set invitation Status = Accepted.
Set invitation AcceptedByUserId = current user id.
Set invitation AcceptedAtUtc.
Save changes.
```

Important duplicate rule:

```text
Never create duplicate agency membership for the same agency + user.
```

---

## Why token/code instead of id-only accept

Rule:

```text
Accepting an invitation must not rely only on invitation id.
```

Reason:

```text
Invitation id identifies the row.
Token/code proves the user has the invitation credential.
```

Even with email matching, token/code is still required.

Reason:

```text
Email matching protects against accepting someone else's invitation after login.
Token/code protects against accepting by guessing or discovering an invitation id.
```

---

# 9F — Cancel agency invitation

## Endpoint

Recommended endpoint:

```http
PUT /api/agencies/{agencyId}/invitations/{invitationId}/cancel
```

Auth:

```text
Requires JWT.
```

---

## Permission rules

Allowed:

```text
Active Owner
```

Blocked:

```text
No token -> 401
Non-member -> 403
Active Agent -> 403
Pending member -> 403
Disabled member -> 403
Disabled user -> 403
Missing agency -> 404
Missing invitation -> 404
Invitation belongs to another agency -> 404
```

Reason:

```text
Cancelling invitations is agency access administration.
```

---

## Status transition rules

Allowed:

```text
Pending -> Cancelled
```

Idempotent:

```text
Cancelled -> Cancelled
```

Blocked:

```text
Accepted -> 400 Bad Request
Expired -> 400 Bad Request
```

Reason:

```text
Cancelling an already accepted invitation would not remove the created membership.
Expired invitations are already unusable.
```

---

# 9G — Disable agency member

## Endpoint

Recommended endpoint:

```http
PUT /api/agencies/{agencyId}/members/{memberId}/disable
```

Auth:

```text
Requires JWT.
```

---

## Permission rules

Allowed:

```text
Active Owner can disable another agency member.
```

Blocked:

```text
No token -> 401
Non-member -> 403
Active Agent -> 403
Pending member -> 403
Disabled member -> 403
Disabled user -> 403
Missing agency -> 404
Missing target member -> 404
Target member belongs to another agency -> 404
```

Self-disable:

```text
Owner should not disable themselves in Chapter 9.
```

Reason:

```text
Self-disable can accidentally lock the owner out.
A separate leave-agency/ownership-transfer flow can be added later if needed.
```

---

## Last active Owner rule

Rule:

```text
Cannot disable the last active Owner of an agency.
```

Behavior:

```http
400 Bad Request
```

Reason:

```text
An agency must always have at least one active Owner.
```

---

## Status behavior

Allowed:

```text
Active -> Disabled
Pending -> Disabled
```

Idempotent:

```text
Disabled -> Disabled
```

No hard delete:

```text
Members are not physically deleted in Chapter 9.
```

Reason:

```text
Soft disabling preserves history and avoids deleting relationships used by listings/auditing.
```

---

# 9H — Change agency member role

## Endpoint

Recommended endpoint:

```http
PUT /api/agencies/{agencyId}/members/{memberId}/role
```

Auth:

```text
Requires JWT.
```

Request:

```text
Role
```

Allowed roles:

```text
Owner
Agent
```

---

## Permission rules

Allowed:

```text
Active Owner
```

Blocked:

```text
No token -> 401
Non-member -> 403
Active Agent -> 403
Pending member -> 403
Disabled member -> 403
Disabled user -> 403
Missing agency -> 404
Missing target member -> 404
Target member belongs to another agency -> 404
Invalid role -> 400
```

Reason:

```text
Changing roles changes agency authority and must be owner-only.
```

---

## Role transition rules

Allowed:

```text
Agent -> Owner
Owner -> Agent
```

Idempotent:

```text
Owner -> Owner
Agent -> Agent
```

Blocked:

```text
Owner -> Agent if this member is the last active Owner -> 400 Bad Request
```

Recommended target member status rule:

```text
Can change role only for Active members.
```

Reason:

```text
Changing roles for Disabled/Pending members creates confusing access expectations.
Re-enable/reactivate member flow is not part of Chapter 9.
```

---

# 9I — Agency logo upload/delete

## Endpoints

Recommended endpoints:

```http
PUT /api/agencies/{agencyId}/logo
DELETE /api/agencies/{agencyId}/logo
```

Auth:

```text
Requires JWT.
```

---

## Permission rules

Allowed:

```text
Active Owner
```

Blocked:

```text
No token -> 401
Non-member -> 403
Active Agent -> 403
Pending member -> 403
Disabled member -> 403
Disabled user -> 403
Missing agency -> 404
```

Reason:

```text
Agency logo is agency profile branding, not listing work.
Profile branding should stay owner-controlled.
```

---

## Agency logo fields

Existing field:

```text
LogoUrl
```

Recommended new agency fields:

```text
LogoStoredFileName
LogoContentType
LogoSizeBytes
```

No separate agency logo table.

Reason:

```text
An agency has one current logo.
A separate table is unnecessary for MVP.
Metadata is useful for safe replacement/deletion.
```

---

## File validation

Reuse existing image/avatar validation rules:

```text
Max size: 5 MB
Allowed extensions: .jpg, .jpeg, .png, .webp
Allowed content types: image/jpeg, image/png, image/webp
```

Storage path:

```text
wwwroot/uploads/agencies/{agencyId}/logo/{storedFileName}
```

Public URL:

```text
/uploads/agencies/{agencyId}/logo/{storedFileName}
```

---

## Upload/replace behavior

Endpoint:

```http
PUT /api/agencies/{agencyId}/logo
```

Behavior:

```text
If agency has no logo -> upload and set logo fields.
If agency already has logo -> store new logo, update fields, save database, then delete old logo file.
```

Important implementation rule:

```text
Do not delete the old logo file before the new logo metadata is successfully saved to the database.
```

Reason:

```text
This matches the safe avatar replacement pattern and avoids losing the old logo if database save fails.
```

---

## Delete logo behavior

Endpoint:

```http
DELETE /api/agencies/{agencyId}/logo
```

Behavior:

```text
Clears logo fields.
Deletes stored logo file if it exists.
Delete is idempotent.
```

Response:

```http
204 No Content
```

Reason:

```text
Frontend can safely call delete without checking whether a logo exists first.
```

---

# 9J — Admin agency verification endpoints

## Scope

Admin verification is intentionally small in Chapter 9.

Included:

```text
Approve agency
Reject agency
Disable agency
```

Not included:

```text
Reactivate agency
Admin agency edit
Admin member management
Verification documents
Verification notes
Audit log UI
```

Reason:

```text
The backend only needs minimum status control before frontend readiness.
```

---

## Endpoints

Recommended endpoints:

```http
PUT /api/admin/agencies/{agencyId}/approve
PUT /api/admin/agencies/{agencyId}/reject
PUT /api/admin/agencies/{agencyId}/disable
```

Auth:

```text
Requires JWT.
Requires User.Role = Admin.
Requires User.Status = Active.
```

Blocked:

```text
No token -> 401
Non-admin user -> 403
PendingVerification admin user -> 403
Disabled admin user -> 403
Missing agency -> 404
```

Reason:

```text
Agency verification changes public trust and publishability.
Only active admins should do it.
```

---

## Status transition rules

### Approve agency

Endpoint:

```http
PUT /api/admin/agencies/{agencyId}/approve
```

Allowed:

```text
PendingVerification -> Active
Rejected -> Active
```

Idempotent:

```text
Active -> Active
```

Blocked:

```text
Disabled -> 400 Bad Request
```

Reason:

```text
Disabled agency should not be silently reactivated by approve in Chapter 9.
No reactivation endpoint exists yet.
```

---

### Reject agency

Endpoint:

```http
PUT /api/admin/agencies/{agencyId}/reject
```

Allowed:

```text
PendingVerification -> Rejected
```

Idempotent:

```text
Rejected -> Rejected
```

Blocked:

```text
Active -> 400 Bad Request
Disabled -> 400 Bad Request
```

Reason:

```text
Reject is for verification review, not for disabling an existing active agency.
```

---

### Disable agency

Endpoint:

```http
PUT /api/admin/agencies/{agencyId}/disable
```

Allowed:

```text
PendingVerification -> Disabled
Active -> Disabled
Rejected -> Disabled
```

Idempotent:

```text
Disabled -> Disabled
```

Reason:

```text
Disable is the admin safety action for removing agency publishability/access trust.
```

---

## Effect on listing behavior

Admin status changes must respect Chapter 8 listing rules:

```text
Only Active agencies can publish agency listings.
Agency status does not block unpublish/archive/dashboard listing management.
Public listing visibility remains Listing.Status = Active only.
```

Important:

```text
Disabling an agency does not automatically archive/unpublish existing listings in Chapter 9.
```

Reason:

```text
Automatic listing status changes are a separate business decision.
Chapter 9 only changes agency status.
```

---

# 9K — Agency dashboard summary

## Endpoint

Recommended endpoint:

```http
GET /api/agencies/{agencyId}/dashboard/summary
```

Auth:

```text
Requires JWT.
```

---

## Permission rules

Allowed:

```text
Active Owner
Active Agent
```

Blocked:

```text
No token -> 401
Non-member -> 403
Pending member -> 403
Disabled member -> 403
Disabled user -> 403
Missing agency -> 404
```

Agency status:

```text
Agency.Status does not block dashboard summary viewing.
```

Reason:

```text
Active members may need to inspect/manage agency dashboard data even when the agency is PendingVerification, Disabled, or Rejected.
```

---

## Response shape

Recommended response:

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

Do not add advanced analytics in Chapter 9.

Out of scope:

```text
Revenue
Lead tracking
Listing performance
Views/clicks
Conversion analytics
Charts
Time-series stats
```

Reason:

```text
Dashboard summary should support frontend layout and basic management only.
```

---

# Architecture rules

## Clean Architecture split

Rules:

```text
Controllers stay thin.
Handlers contain use-case/application logic.
Repositories stay data-focused.
Domain entities own their own state transitions.
Infrastructure owns EF Core mappings, persistence, file storage, and security.
Application owns repository interfaces.
Infrastructure implements repository interfaces.
```

---

## Controller rules

Controllers should:

```text
Read route/query/body/form input.
Call handlers.
Map ServiceResult to HTTP response.
```

Controllers should not contain:

```text
Owner checks
Agent checks
Invitation status transitions
Member disable/demotion rules
Last active Owner rule
File storage logic
EF Core queries
```

---

## Handler rules

Handlers should:

```text
Resolve current user.
Load required agency/member/invitation/listing data.
Apply user status rules.
Apply member role/status rules.
Apply invitation/member/logo/admin use-case rules.
Call domain methods where state changes belong to entities.
Save changes through repositories.
Return DTO response through ServiceResult.
```

---

## Repository rules

Repositories stay data-focused.

Good repository methods:

```text
Get agency by id for update
Get agency by id read-only
Get invitation by id for update
Get invitation by token/code for update
Get invitations by agency id
Check pending invitation exists by agency + normalized email
Get member access read model
Get member by id for update
Count active owners by agency id
Get dashboard summary counts
Save changes
```

Bad repository methods:

```text
CanUserInviteMemberAsync
CanUserCancelInvitationAsync
CanUserAcceptInvitationAsync
CanUserDisableMemberAsync
CanUserChangeRoleAsync
CanAdminApproveAgencyAsync
```

Reason:

```text
Repositories fetch data.
Handlers or small application-level permission helpers decide rules.
```

---

## Permission helper rule

A small application-level helper is allowed if repeated agency admin access checks become duplicated.

Possible helper:

```text
AgencyAdminAccessChecker
```

Possible responsibilities:

```text
Ensure agency exists.
Ensure current user is not Disabled.
Ensure current user is Active Owner.
Ensure current user is Active Owner or Active Agent for dashboard summary.
```

Important:

```text
Do not create this helper before duplication exists.
```

Reason:

```text
Avoid overbuilding.
Extract only when repeated permission flow becomes noisy.
```

---

## Domain method rules

Recommended domain methods:

Agency:

```text
SetLogo(...)
RemoveLogo()
Approve()
Reject()
Disable()
```

AgencyMember:

```text
Disable()
ChangeRole(...)
```

AgencyInvitation:

```text
Accept(...)
Cancel()
Expire()
```

Domain methods should handle:

```text
Entity state transitions
Basic invariant protection that belongs to the entity
Setting entity fields consistently
```

Domain methods should not handle:

```text
JWT/current user lookup
HTTP status codes
EF Core queries
Repository calls
Cross-entity permission checks
File storage
```

Cross-entity rules such as last active Owner should be handled in the handler because it requires counting other members.

---

## DTO rules

Recommended DTOs:

```text
CreateAgencyInvitationRequest
AgencyInvitationResponse
AcceptAgencyInvitationRequest
ChangeAgencyMemberRoleRequest
AgencyMemberResponse or existing member response reuse
AgencyDashboardSummaryResponse
AgencyResponse reuse for admin verification and logo update where practical
```

Response reuse rule:

```text
Reuse existing AgencyResponse when the endpoint returns updated agency profile/status/logo state.
Do not create unnecessary response DTOs.
```

---

# Error behavior

Use:

```http
401 Unauthorized
```

When:

```text
No JWT token
Invalid JWT token
Current user id cannot be resolved
```

Use:

```http
403 Forbidden
```

When:

```text
Authenticated user lacks required agency role/status
Authenticated user is Disabled
Authenticated user is not Admin for admin endpoint
Invitation email does not match current user email
```

Use:

```http
404 Not Found
```

When:

```text
Agency does not exist
Invitation does not exist
Invitation token/code does not exist
Member does not exist
Target resource belongs to another agency
```

Use:

```http
400 Bad Request
```

When:

```text
Validation fails
Duplicate pending invitation exists
Already member accepts invitation
Invitation is not Pending during accept
Invitation is Accepted/Expired during cancel
Cannot disable last active Owner
Cannot demote last active Owner
Invalid admin status transition
Invalid file type/extension/size
```

Use:

```http
200 OK
```

When:

```text
Invite created if not using 201
Invitation accepted
Invitation cancelled
Member disabled
Member role changed
Logo uploaded/replaced
Admin status changed
Dashboard summary returned
```

Use:

```http
201 Created
```

Optional for:

```text
Invitation created
```

Preferred:

```text
Use 201 Created for POST invitation creation if route conventions are simple.
Use 200 OK if existing controller style prefers simple ServiceResult mapping.
```

Use:

```http
204 No Content
```

When:

```text
Agency logo deleted successfully, including idempotent no-logo case.
```

---

# Testing rules

## Testing policy

Add integration tests for important API behavior and permission boundaries.

Add unit tests only when there is real domain logic worth testing.

Do not chase fake 100% unit coverage.

---

## Recommended integration test files

Use existing partial-class style under agencies tests.

Possible files:

```text
tests/RealEstate.Tests/Integration/Agencies/AgenciesEndpointTests.Invitations.cs
tests/RealEstate.Tests/Integration/Agencies/AgenciesEndpointTests.MemberManagement.cs
tests/RealEstate.Tests/Integration/Agencies/AgenciesEndpointTests.Logo.cs
tests/RealEstate.Tests/Integration/Agencies/AgenciesEndpointTests.AdminVerification.cs
tests/RealEstate.Tests/Integration/Agencies/AgenciesEndpointTests.DashboardSummary.cs
```

Only split further if files become too large.

---

## Invitation tests

Cover:

```text
No token invite -> 401
Active Owner can invite -> success
Active Agent cannot invite -> 403
Non-member cannot invite -> 403
Disabled user cannot invite -> 403
Duplicate pending invitation -> 400
Invitation response includes Token/Code on create
Active/PendingVerification invited user can accept by Token/Code -> success
Accept with invitation id only is not supported
Accept with wrong Token/Code -> 404
Accept with mismatched email -> 403
Disabled user cannot accept -> 403
Cancelled invitation cannot be accepted -> 400
Accepted invitation cannot be accepted again -> 400
Expired invitation cannot be accepted -> 400
Accept creates AgencyMember with correct role/status
Accept marks invitation Accepted
Accept does not create duplicate membership
Active Owner can cancel pending invitation -> success
Active Agent cannot cancel invitation -> 403
Cancelled invitation cancel is idempotent
Accepted invitation cannot be cancelled -> 400
```

---

## Member management tests

Cover:

```text
No token disable member -> 401
Active Owner can disable member -> success
Active Agent cannot disable member -> 403
Non-member cannot disable member -> 403
Disabled user cannot disable member -> 403
Cannot disable last active Owner -> 400
Owner cannot self-disable in Chapter 9 -> 400 or 403, define in implementation
Disabling Disabled member is idempotent
Active Owner can change Agent to Owner -> success
Active Owner can change Owner to Agent if another active Owner exists -> success
Cannot demote last active Owner -> 400
Active Agent cannot change roles -> 403
Invalid role -> 400
Role change for Disabled/Pending member -> 400
```

Preferred self-disable response:

```http
400 Bad Request
```

Reason:

```text
The authenticated user may have permission generally, but the requested transition is invalid.
```

---

## Logo tests

Cover:

```text
No token upload logo -> 401
Active Owner can upload logo -> 200
Active Agent cannot upload logo -> 403
Non-member cannot upload logo -> 403
Disabled user cannot upload logo -> 403
Missing agency -> 404
Missing file -> 400
Empty file -> 400
Invalid extension -> 400
Invalid content type -> 400
Too large file -> 400
Second upload replaces logo metadata -> 200
Delete logo returns 204
Delete logo with no logo still returns 204
Active Agent cannot delete logo -> 403
Logo fields persist after upload
Logo fields clear after delete
```

---

## Admin verification tests

Cover:

```text
No token approve/reject/disable -> 401
Non-admin cannot approve/reject/disable -> 403
PendingVerification admin user cannot approve/reject/disable -> 403
Disabled admin user cannot approve/reject/disable -> 403
Active admin can approve PendingVerification agency -> Active
Active admin can approve Rejected agency -> Active
Approve Active agency is idempotent
Approve Disabled agency -> 400
Active admin can reject PendingVerification agency -> Rejected
Reject Rejected agency is idempotent
Reject Active agency -> 400
Reject Disabled agency -> 400
Active admin can disable PendingVerification agency -> Disabled
Active admin can disable Active agency -> Disabled
Active admin can disable Rejected agency -> Disabled
Disable Disabled agency is idempotent
```

---

## Dashboard summary tests

Cover:

```text
No token -> 401
Missing agency -> 404
Non-member -> 403
Pending member -> 403
Disabled member -> 403
Disabled user -> 403
Active Owner -> 200
Active Agent -> 200
Agency status does not block dashboard summary
Counts include Draft/Active/Archived listings correctly
Counts include active members correctly
Counts include pending invitations correctly
```

---

# Database changes

Expected new table:

```text
AgencyInvitations
```

Expected Agency columns:

```text
LogoStoredFileName
LogoContentType
LogoSizeBytes
```

Existing `LogoUrl` remains.

Expected constraints/indexes:

```text
Index on AgencyInvitations.AgencyId
Index on AgencyInvitations.NormalizedEmail
Unique index on AgencyInvitations.Token or Code, if using one field
Optional unique filtered index on AgencyId + NormalizedEmail where Status = Pending
```

Important:

```text
Do not expose AgencyInvitation as a public DbSet unless needed by current project aggregate rules.
Repository can use Set<AgencyInvitation>() internally if following existing child-entity access style.
```

Final DbSet decision should follow current project style after exact files are reviewed.

---

# Implementation file request policy

Before final implementation code, ask for exact existing files.

Required files will likely include:

```text
src/RealEstate.Domain/Entities/Agency.cs
src/RealEstate.Domain/Entities/AgencyMember.cs
src/RealEstate.Domain/Enums/AgencyStatus.cs
src/RealEstate.Domain/Enums/AgencyMemberRole.cs
src/RealEstate.Domain/Enums/AgencyMemberStatus.cs
src/RealEstate.Application/Agencies/Repositories/IAgencyRepository.cs
src/RealEstate.Infrastructure/Persistence/Repositories/AgencyRepository.cs
src/RealEstate.Infrastructure/Persistence/Configurations/AgencyConfiguration.cs
src/RealEstate.Infrastructure/Persistence/Configurations/AgencyMemberConfiguration.cs
src/RealEstate.Infrastructure/Persistence/RealEstateDbContext.cs
src/RealEstate.Api/Controllers/AgenciesController.cs
src/RealEstate.Api/Controllers/UsersController.cs
src/RealEstate.Infrastructure/Storage/LocalFileStorageService.cs
src/RealEstate.Application/Common/Storage/IFileStorageService.cs
src/RealEstate.Application/Common/ServiceResult.cs
src/RealEstate.Infrastructure/DependencyInjection.cs
tests/RealEstate.Tests/Integration/Agencies/AgenciesEndpointTests.Setup.cs
tests/RealEstate.Tests/Integration/Agencies/AgencyTestHelpers.cs
```

Additional files may be needed depending on current admin/auth patterns.

Rule:

```text
Do not guess project-specific helper names, route mapping style, DI registration style, or test setup details.
Ask for files first, then write compile-safe code.
```

---

# 9L — Docs/context update

After implementation and tests pass, update:

```text
docs/chapters/chapter-09-agency-phase-2.md
backend-context.md
```

Final update should include:

```text
Final implemented endpoints
Final permission rules
Final database changes
Final test count
Important implementation notes
Out-of-scope items that remain postponed
Next chapter: Chapter 9.5 — Frontend readiness
```

Do not update backend-context.md as final until Chapter 9 implementation is complete and tests pass.

Reason:

```text
backend-context.md is the compressed AI handoff file.
It should describe final implemented state, not planned behavior pretending to be complete.
```
