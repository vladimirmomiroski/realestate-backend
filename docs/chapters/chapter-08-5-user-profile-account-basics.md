# Chapter 8.5 — User Profile / Account Basics Rules

## Purpose

Chapter 8.5 adds basic authenticated user account/profile functionality.

This chapter is intentionally small and focused.

It adds:

```text
GET current user profile
Update current user profile
Upload/replace current user avatar
Delete current user avatar
Basic integration tests
```

The goal is to prepare the backend for frontend account/dashboard usage without overbuilding a full identity/account system.

---

## Final scope

Chapter 8.5 includes these endpoints:

```http
GET /api/users/me
PUT /api/users/me/profile
PUT /api/users/me/avatar
DELETE /api/users/me/avatar
```

Avatar upload and avatar replace use the same endpoint:

```http
PUT /api/users/me/avatar
```

Reason:

```text
User avatar is one resource.
PUT is appropriate for creating or replacing that resource.
No separate POST avatar endpoint is needed.
```

---

## Out of scope

Do not add these in Chapter 8.5:

```text
Change email
Email verification
Change password
Forgot password
Refresh tokens
OAuth/social login
Admin user management
Public user profiles
Public agent profiles
Agency staff public profiles
User search
Notification preferences
Billing/account subscription settings
User deletion
Account deactivation
```

Reason:

```text
This chapter is only for current-user profile/account basics.
```

---

## Existing User fields

The current `User` entity already has:

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

Do not duplicate these fields.

Do not casually change existing nullability or database behavior for these fields.

Before implementation, inspect the actual current files:

```text
User.cs
UserConfiguration.cs
current migrations/database schema
register/login request behavior
```

Reason:

```text
FirstName, LastName, and PhoneNumber nullability must follow the existing User entity and EF configuration.
We do not want accidental migration conflicts, duplicate fields, or incorrect nullable/non-nullable changes.
```

---

## New avatar fields

Avatar support may require adding fields to `User`.

Recommended fields:

```text
AvatarUrl
AvatarStoredFileName
AvatarContentType
AvatarSizeBytes
```

These fields are only for the user profile avatar.

Do not create a separate avatar table in Chapter 8.5.

Reason:

```text
A user has only one avatar.
A separate UserAvatar entity/table is unnecessary for MVP.
```

---

## Auth rules

All Chapter 8.5 endpoints require JWT authentication.

No token:

```http
401 Unauthorized
```

Invalid token:

```http
401 Unauthorized
```

Authenticated user missing from database:

```http
401 Unauthorized
```

or project-consistent equivalent if the existing code already handles this differently.

Implementation must follow the existing project convention after checking current handlers/controllers.

---

## User status rules

Current user statuses:

```text
Active
PendingVerification
Disabled
```

### Read profile

```http
GET /api/users/me
```

Allowed:

```text
Active
PendingVerification
Disabled
```

Reason:

```text
Even Disabled users may need the frontend to show account status.
Reading own account data does not mutate anything.
```

### Mutate profile/avatar

These endpoints mutate account data:

```http
PUT /api/users/me/profile
PUT /api/users/me/avatar
DELETE /api/users/me/avatar
```

Allowed:

```text
Active
PendingVerification
```

Blocked:

```text
Disabled
```

Disabled user mutation response:

```http
403 Forbidden
```

Reason:

```text
PendingVerification users may prepare their account.
Disabled users should not perform protected account mutations.
```

---

## GET /api/users/me

Endpoint:

```http
GET /api/users/me
```

Auth:

```text
Requires JWT.
```

Purpose:

```text
Return the current authenticated user's account/profile data.
```

Recommended response fields:

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

Read-only fields in this chapter:

```text
Id
Email
Role
Status
CreatedAtUtc
ModifiedAtUtc
```

Rules:

```text
No token -> 401
Invalid token -> 401
Authenticated user exists -> 200
Disabled authenticated user -> 200
```

Reason:

```text
Frontend needs a reliable current-user endpoint for dashboard/account UI.
```

---

## PUT /api/users/me/profile

Endpoint:

```http
PUT /api/users/me/profile
```

Auth:

```text
Requires JWT.
```

Purpose:

```text
Update basic current-user profile fields.
```

Editable fields:

```text
FirstName
LastName
PhoneNumber
```

Not editable here:

```text
Id
Email
NormalizedEmail
PasswordHash
Role
Status
AvatarUrl
AvatarStoredFileName
AvatarContentType
AvatarSizeBytes
CreatedAtUtc
ModifiedAtUtc
```

Rules:

```text
No token -> 401
Invalid token -> 401
Active user -> 200
PendingVerification user -> 200
Disabled user -> 403
```

Validation rules must follow the existing User entity and EF configuration.

Recommended MVP validation after inspecting current model:

```text
FirstName max length should match existing UserConfiguration.
LastName max length should match existing UserConfiguration.
PhoneNumber max length should match existing UserConfiguration.
Strings should be trimmed.
Empty string/null behavior must match existing entity/configuration decisions.
```

Important:

```text
Do not decide whether empty FirstName/LastName becomes null or empty string until User.cs and UserConfiguration.cs are checked.
```

Reason:

```text
Avoid accidental schema/nullability changes.
```

---

## PUT /api/users/me/avatar

Endpoint:

```http
PUT /api/users/me/avatar
```

Auth:

```text
Requires JWT.
```

Purpose:

```text
Upload first avatar or replace existing avatar.
```

Behavior:

```text
If user has no avatar -> upload and set avatar fields.
If user already has avatar -> replace avatar fields with new file info.
Old avatar file should be deleted after successful database save.
```

Response:

```text
Return updated current-user profile response.
```

or a smaller avatar response if that matches the existing API style better.

Recommended response:

```text
UserProfileResponse
```

Reason:

```text
Frontend immediately receives the full updated account state.
```

Allowed file types:

```text
.jpg
.jpeg
.png
.webp
```

Allowed content types:

```text
image/jpeg
image/png
image/webp
```

Maximum file size:

```text
5 MB
```

Reason:

```text
Matches existing listing image validation and avoids introducing unnecessary special rules.
```

Storage path:

```text
src/RealEstate.Api/wwwroot/uploads/users/{userId}/avatar/{storedFileName}
```

Public URL:

```text
/uploads/users/{userId}/avatar/{storedFileName}
```

Rules:

```text
No token -> 401
Invalid token -> 401
Active user valid image -> 200
PendingVerification user valid image -> 200
Disabled user -> 403
Missing file -> 400
Empty file -> 400
Invalid extension -> 400
Invalid content type -> 400
File too large -> 400
```

Important implementation rule:

```text
Do not delete the old avatar file before the new avatar is successfully saved to the database.
```

Recommended flow:

```text
1. Load current user.
2. Block Disabled user.
3. Validate uploaded file.
4. Store new file.
5. Keep old avatar file info in memory.
6. Update user avatar fields.
7. Save database changes.
8. Delete old avatar file after successful database save.
```

If database save fails after file storage:

```text
Try to clean up the newly uploaded file.
```

Reason:

```text
Avoid orphaned files where practical.
```

---

## DELETE /api/users/me/avatar

Endpoint:

```http
DELETE /api/users/me/avatar
```

Auth:

```text
Requires JWT.
```

Purpose:

```text
Remove the current user's avatar.
```

Behavior:

```text
Clears avatar fields on User.
Deletes avatar file from storage if it exists.
```

Recommended response:

```http
204 No Content
```

Rules:

```text
No token -> 401
Invalid token -> 401
Active user with avatar -> 204
PendingVerification user with avatar -> 204
Active user without avatar -> 204
PendingVerification user without avatar -> 204
Disabled user -> 403
```

Delete should be idempotent.

Reason:

```text
Frontend can safely call delete without first checking whether an avatar exists.
```

---

## Domain rules

User profile/avatar state belongs to the `User` entity.

Recommended domain methods:

```text
UpdateProfile(...)
SetAvatar(...)
RemoveAvatar()
```

Domain methods may handle:

```text
updating profile fields
setting avatar metadata
clearing avatar metadata
basic invariant protection
```

Domain methods must not handle:

```text
JWT/current user lookup
HTTP status codes
file upload validation
filesystem paths
repository calls
database calls
```

Reason:

```text
The User entity owns its own profile/avatar state.
Application handlers own the use-case flow.
Infrastructure owns storage/database details.
```

---

## Application/handler rules

Handlers contain use-case logic.

Recommended application structure:

```text
src/RealEstate.Application/Users/Dtos
src/RealEstate.Application/Users/Queries/GetCurrentUser
src/RealEstate.Application/Users/Commands/UpdateCurrentUserProfile
src/RealEstate.Application/Users/Commands/UploadCurrentUserAvatar
src/RealEstate.Application/Users/Commands/DeleteCurrentUserAvatar
```

Handlers should:

```text
Read current user id from ICurrentUserService.
Load User from IUserRepository.
Apply user status rules.
Validate request/file.
Call User domain methods.
Save changes through repository.
Map User to response DTO.
```

Handlers should not:

```text
Contain EF Core query details.
Contain controller-specific HTTP code.
Hide business decisions inside repository methods.
```

---

## Repository rules

Repositories stay data-focused.

Good repository methods:

```text
GetByIdReadOnlyAsync
GetByIdForUpdateAsync
SaveChangesAsync
```

or whatever exact naming matches the existing repository.

Bad repository methods:

```text
CanUserUpdateProfileAsync
CanUserUploadAvatarAsync
CanUserDeleteAvatarAsync
```

Reason:

```text
Permission/business rules belong in handlers or small application-level policy helpers, not repositories.
```

---

## Storage rules

Avatar storage should reuse the existing storage abstraction if it already supports the needed behavior.

Before implementation, inspect:

```text
RealEstate.Application/Common/Storage
RealEstate.Infrastructure/Storage
LocalFileStorageService
LocalFileStorageOptions
existing listing image upload handlers
```

Do not duplicate storage logic if existing file storage can be reused cleanly.

Do not over-generalize storage if a small extension is enough.

---

## Controller rules

Add:

```text
UsersController
```

Do not put these endpoints in:

```text
AuthController
```

Reason:

```text
AuthController handles register/login.
UsersController handles authenticated user resource operations.
```

Controller responsibilities:

```text
Read request body/query/form file.
Call handler.
Map ServiceResult to HTTP response.
```

Controller must not contain:

```text
profile mutation rules
user status rules
file validation rules
storage path logic
repository calls
```

---

## DTO rules

Recommended DTOs:

```text
UserProfileResponse
UpdateUserProfileRequest
```

Potential avatar-specific DTO is optional:

```text
UserAvatarResponse
```

Recommended response reuse:

```text
Return UserProfileResponse from GET /me, profile update, and avatar upload.
```

Reason:

```text
Frontend receives one consistent current-user shape.
```

---

## Error behavior

Use:

```http
401 Unauthorized
```

When:

```text
No JWT token
Invalid JWT token
Current user id cannot be resolved from token
```

Use:

```http
403 Forbidden
```

When:

```text
Disabled user attempts profile/avatar mutation
```

Use:

```http
400 Bad Request
```

When:

```text
Profile validation fails
Avatar file is missing
Avatar file is empty
Avatar extension is invalid
Avatar content type is invalid
Avatar file is too large
```

Use:

```http
200 OK
```

When:

```text
GET /me succeeds
PUT /me/profile succeeds
PUT /me/avatar succeeds
```

Use:

```http
204 No Content
```

When:

```text
DELETE /me/avatar succeeds
```

including when there was no avatar to delete.

---

## Testing plan

Add integration tests for:

```text
GET /api/users/me
PUT /api/users/me/profile
PUT /api/users/me/avatar
DELETE /api/users/me/avatar
```

Recommended test files:

```text
tests/RealEstate.Tests/Integration/Users/UsersEndpointTests.Setup.cs
tests/RealEstate.Tests/Integration/Users/UsersEndpointTests.GetMe.cs
tests/RealEstate.Tests/Integration/Users/UsersEndpointTests.UpdateProfile.cs
tests/RealEstate.Tests/Integration/Users/UsersEndpointTests.Avatar.cs
```

Use partial class split if it matches the current integration test style.

---

## GET /api/users/me tests

Required tests:

```text
No token -> 401
Valid token -> 200
Response belongs to current user
Response includes email, role, status, profile fields
Disabled user can still read own profile
```

---

## PUT /api/users/me/profile tests

Required tests:

```text
No token -> 401
Active user can update profile
PendingVerification user can update profile
Disabled user cannot update profile -> 403
Updated profile persists
Read-only fields cannot be changed through this endpoint
```

Read-only fields should be protected naturally by request DTO shape.

Do not add request properties for:

```text
Email
Role
Status
PasswordHash
AvatarUrl
```

---

## PUT /api/users/me/avatar tests

Required tests:

```text
No token -> 401
Active user can upload avatar -> 200
PendingVerification user can upload avatar -> 200
Disabled user cannot upload avatar -> 403
Missing file -> 400
Invalid extension -> 400
Invalid content type -> 400
File too large -> 400
AvatarUrl is saved
Second upload replaces avatar
```

If practical, test that old avatar metadata is replaced.

File deletion can be tested lightly or left as infrastructure behavior if direct filesystem assertions make tests brittle.

---

## DELETE /api/users/me/avatar tests

Required tests:

```text
No token -> 401
Active user can delete avatar -> 204
PendingVerification user can delete avatar -> 204
Disabled user cannot delete avatar -> 403
Deleting when no avatar still returns 204
Avatar fields are cleared after delete
```

---

## Migration

Expected migration only if avatar fields are added:

```text
AddUserAvatarFields
```

or:

```text
AddUserProfileAvatarFields
```

Do not add migration changes for existing profile fields unless inspection proves they are required.

---

## Implementation order

Recommended task split:

```text
8.5A — Rules document
8.5B — User profile/avatar DTOs and model changes
8.5C — GET /api/users/me
8.5D — PUT /api/users/me/profile
8.5E — PUT /api/users/me/avatar
8.5F — DELETE /api/users/me/avatar
8.5G — Docs/context update
```

---

## 8.5A — Rules document

Create:

```text
docs/chapters/chapter-08-5-user-profile-account-basics.md
```

Lock:

```text
current-user endpoints
profile editable/read-only fields
avatar upload/replace/delete behavior
user status rules
storage path
validation rules
test expectations
```

---

## 8.5B — User profile/avatar DTOs and model changes

Before writing implementation code, inspect exact files:

```text
User.cs
UserConfiguration.cs
IUserRepository.cs
UserRepository.cs
storage abstractions
existing listing image upload code
```

Add only needed avatar fields.

Do not duplicate existing fields.

---

## 8.5C — GET /api/users/me

Add:

```text
GetCurrentUserQuery
GetCurrentUserHandler
UserProfileResponse
UsersController.GetMe
integration tests
```

---

## 8.5D — PUT /api/users/me/profile

Add:

```text
UpdateCurrentUserProfileCommand
UpdateCurrentUserProfileHandler
UpdateUserProfileRequest
UsersController.UpdateProfile
integration tests
```

---

## 8.5E — PUT /api/users/me/avatar

Add:

```text
UploadCurrentUserAvatarCommand
UploadCurrentUserAvatarHandler
UsersController.UploadAvatar
integration tests
```

Use:

```http
PUT /api/users/me/avatar
```

for both first upload and replace.

---

## 8.5F — DELETE /api/users/me/avatar

Add:

```text
DeleteCurrentUserAvatarCommand
DeleteCurrentUserAvatarHandler
UsersController.DeleteAvatar
integration tests
```

---

## 8.5G — Docs/context update

Update:

```text
backend-context.md
docs/chapters/chapter-08-5-user-profile-account-basics.md
```

Also update locked roadmap in backend context:

```text
Chapter 8.5 — User profile/account basics
Chapter 9 — Agency Phase 2
Chapter 9.5 — Frontend readiness
Then frontend
```

---

## Completion checklist

Chapter 8.5 is complete when:

```text
Rules document exists.
GET /api/users/me works.
PUT /api/users/me/profile works.
PUT /api/users/me/avatar works for first upload and replace.
DELETE /api/users/me/avatar works.
Disabled users cannot mutate profile/avatar.
PendingVerification users can mutate profile/avatar.
Avatar validation is tested.
Avatar fields are persisted.
Avatar delete clears avatar fields.
Controllers remain thin.
Handlers contain use-case logic.
Repositories remain data-focused.
No duplicate existing User fields are added.
No unrelated auth/account features are added.
dotnet build passes.
dotnet test passes.
backend-context.md is updated with Chapter 8.5 completion and locked roadmap.
```
