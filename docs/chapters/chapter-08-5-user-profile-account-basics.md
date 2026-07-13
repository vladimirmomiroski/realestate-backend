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

## Final status

Chapter 8.5 is completed.

Completed tasks:

```text
8.5A — User profile/account rules document
8.5B — User profile/avatar foundation
8.5C — GET /api/users/me
8.5D — PUT /api/users/me/profile
8.5E — PUT /api/users/me/avatar
8.5F — DELETE /api/users/me/avatar
8.5G — Docs/context update
```

Final implemented endpoints:

```http
GET /api/users/me
PUT /api/users/me/profile
PUT /api/users/me/avatar
DELETE /api/users/me/avatar
```

Final behavior:

```text
GET /api/users/me returns the current authenticated user profile.
PUT /api/users/me/profile updates FirstName, LastName, and PhoneNumber.
PUT /api/users/me/avatar uploads the first avatar or replaces the existing avatar.
DELETE /api/users/me/avatar removes avatar metadata and deletes the stored avatar file when present.
```

Final architecture outcome:

```text
UsersController handles current-user profile endpoints.
Handlers contain use-case logic.
User entity owns profile/avatar state changes.
UserRepository stays data-focused.
Local file storage is reused for avatar files.
No public user profiles were added.
No email/password/account verification flows were added.
```

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

Chapter 8.5 intentionally does not add:

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

The `User` entity already had:

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

These fields were not duplicated.

Existing profile field behavior remained unchanged:

```text
FirstName is required.
LastName is required.
PhoneNumber is optional.
```

Reason:

```text
FirstName, LastName, and PhoneNumber nullability follows the existing User entity and EF configuration.
This avoids accidental migration conflicts, duplicate fields, or incorrect nullable/non-nullable changes.
```

---

## New avatar fields

Chapter 8.5 added avatar metadata fields to `User`:

```text
AvatarUrl
AvatarStoredFileName
AvatarContentType
AvatarSizeBytes
```

These fields are only for the current user's profile avatar.

No separate avatar table was added.

Reason:

```text
A user has only one avatar.
A separate UserAvatar entity/table is unnecessary for MVP.
```

Migration:

```text
AddUserAvatarFields
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

Current user cannot be resolved:

```http
401 Unauthorized
```

Implementation detail:

```text
ServiceResultStatus.Unauthorized was added so current-user handlers can return clean 401 responses.
```

---

## User status rules

Current user statuses:

```text
Active
PendingVerification
Disabled
```

### Read profile

Endpoint:

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

Endpoints:

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

Response:

```text
UserProfileResponse
```

Response fields:

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

Request:

```text
UpdateUserProfileRequest
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

Response:

```text
UserProfileResponse
```

Rules:

```text
No token -> 401
Invalid token -> 401
Active user -> 200
PendingVerification user -> 200
Disabled user -> 403
Missing/blank FirstName -> 400
Missing/blank LastName -> 400
Too long FirstName -> 400
Too long LastName -> 400
Too long PhoneNumber -> 400
```

Validation:

```text
FirstName is required and max 100 characters.
LastName is required and max 100 characters.
PhoneNumber is optional and max 50 characters.
Strings are trimmed by User.UpdateProfile(...).
Blank PhoneNumber becomes null.
```

Reason:

```text
FirstName and LastName are required in the existing User model.
PhoneNumber is optional in the existing User model.
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
Old avatar file is deleted after successful database save.
```

Response:

```text
UserProfileResponse
```

Reason:

```text
Frontend immediately receives the full updated account state.
```

Allowed file extensions:

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
Second upload replaces avatar metadata -> 200
```

Important implementation rule:

```text
Do not delete the old avatar file before the new avatar is successfully saved to the database.
```

Implemented flow:

```text
1. Resolve current user.
2. Validate uploaded file.
3. Load tracked User.
4. Block Disabled user.
5. Store new avatar file.
6. Keep old avatar stored filename in memory.
7. Update user avatar fields through User.SetAvatar(...).
8. Save database changes.
9. Delete old avatar file after successful database save.
10. If database save fails after file storage, clean up the newly uploaded file where practical.
```

Reason:

```text
This avoids losing the old avatar if the new avatar metadata fails to persist.
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
Delete is idempotent.
```

Response:

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

Implemented flow:

```text
1. Resolve current user.
2. Load tracked User.
3. Block Disabled user.
4. Keep old avatar stored filename in memory.
5. Clear avatar fields through User.RemoveAvatar().
6. Save database changes.
7. Delete old avatar file if one existed.
8. Return 204 No Content.
```

Reason:

```text
Frontend can safely call delete without first checking whether an avatar exists.
```

---

## Domain rules

User profile/avatar state belongs to the `User` entity.

Implemented domain methods:

```text
UpdateProfile(...)
SetAvatar(...)
RemoveAvatar()
```

Domain methods handle:

```text
updating profile fields
trimming profile values
normalizing blank phone number to null
setting avatar metadata
clearing avatar metadata
```

Domain methods do not handle:

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

Implemented application structure:

```text
src/RealEstate.Application/Users/Dtos
src/RealEstate.Application/Users/Mappings
src/RealEstate.Application/Users/Queries/GetCurrentUser
src/RealEstate.Application/Users/Commands/UpdateCurrentUserProfile
src/RealEstate.Application/Users/Commands/UploadCurrentUserAvatar
src/RealEstate.Application/Users/Commands/DeleteCurrentUserAvatar
```

Implemented handlers:

```text
GetCurrentUserHandler
UpdateCurrentUserProfileHandler
UploadCurrentUserAvatarHandler
DeleteCurrentUserAvatarHandler
```

Handlers:

```text
Read current user id from ICurrentUserService.
Load User from IUserRepository.
Apply user status rules.
Validate request/file.
Call User domain methods.
Save changes through repository.
Map User to UserProfileResponse.
```

Handlers do not:

```text
Contain EF Core query details.
Contain controller-specific HTTP code.
Hide business decisions inside repository methods.
```

---

## Repository rules

Repositories stay data-focused.

Used repository methods:

```text
GetByIdReadOnlyAsync
GetByIdForUpdateAsync
SaveChangesAsync
```

Bad repository methods were not added:

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

Avatar storage reuses the existing file storage abstraction.

`IFileStorageService` was extended with:

```text
SaveUserAvatarAsync
DeleteUserAvatarAsync
```

`LocalFileStorageService` stores avatars under:

```text
wwwroot/uploads/users/{userId}/avatar/{storedFileName}
```

Public avatar URL:

```text
/uploads/users/{userId}/avatar/{storedFileName}
```

Reason:

```text
The existing storage abstraction already handled local file storage for listing images.
A small extension was enough.
No duplicate storage service was needed.
```

---

## Controller rules

Implemented controller:

```text
UsersController
```

Endpoints were not added to:

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
Read request body/form file.
Call handler.
Map ServiceResult to HTTP response.
```

Controller does not contain:

```text
profile mutation rules
user status rules
file validation rules
storage path logic
repository calls
```

---

## DTO rules

Implemented DTOs:

```text
UserProfileResponse
UpdateUserProfileRequest
```

No avatar-specific response DTO was added.

Reason:

```text
GET /me, profile update, and avatar upload all return the same current-user profile shape.
Frontend receives one consistent current-user response.
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
Current user from token no longer exists
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

## Testing summary

Integration tests were added for:

```text
GET /api/users/me
PUT /api/users/me/profile
PUT /api/users/me/avatar
DELETE /api/users/me/avatar
```

Test files:

```text
tests/RealEstate.Tests/Integration/Users/UsersEndpointTests.Setup.cs
tests/RealEstate.Tests/Integration/Users/UsersEndpointTests.GetMe.cs
tests/RealEstate.Tests/Integration/Users/UsersEndpointTests.UpdateProfile.cs
tests/RealEstate.Tests/Integration/Users/UsersEndpointTests.Avatar.cs
```

Partial class split was used to match the existing integration test style.

---

## GET /api/users/me tests

Covered:

```text
No token -> 401
Valid token -> 200
Response belongs to current user
Response does not return another user's data
Response includes email, role, status, profile fields
Disabled user can still read own profile
```

---

## PUT /api/users/me/profile tests

Covered:

```text
No token -> 401
Active user can update profile
PendingVerification user can update profile
Disabled user cannot update profile -> 403
Missing FirstName -> 400
Missing LastName -> 400
Updated profile persists
Read-only fields cannot be changed through this endpoint
```

Read-only fields are protected by request DTO shape.

Request DTO does not include:

```text
Email
Role
Status
PasswordHash
AvatarUrl
```

---

## PUT /api/users/me/avatar tests

Covered:

```text
No token -> 401
Active user can upload avatar -> 200
PendingVerification user can upload avatar -> 200
Disabled user cannot upload avatar -> 403
Missing file -> 400
Empty file -> 400
Invalid extension -> 400
Invalid content type -> 400
File too large -> 400
AvatarUrl is saved
Avatar metadata is saved
Second upload replaces avatar metadata
```

---

## DELETE /api/users/me/avatar tests

Covered:

```text
No token -> 401
Active user can delete avatar -> 204
PendingVerification user can delete avatar -> 204
Disabled user cannot delete avatar -> 403
Deleting when no avatar still returns 204
Avatar fields are cleared after delete
```

---

## Database changes

Migration:

```text
AddUserAvatarFields
```

Users table added columns:

```text
AvatarUrl
AvatarStoredFileName
AvatarContentType
AvatarSizeBytes
```

No changes were made to existing profile field nullability.

---

## Implementation order

Completed task split:

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

Completed:

```text
Created docs/chapters/chapter-08-5-user-profile-account-basics.md.
Locked current-user endpoint rules, editable fields, avatar behavior, status rules, validation, storage, and test expectations.
```

---

## 8.5B — User profile/avatar DTOs and model changes

Completed:

```text
Added avatar fields to User.
Added User.UpdateProfile(...).
Added User.SetAvatar(...).
Added User.RemoveAvatar().
Updated UserConfiguration.
Added migration AddUserAvatarFields.
Added UserProfileResponse.
Added user profile mapping extension.
Added IUserRepository.GetByIdForUpdateAsync.
```

---

## 8.5C — GET /api/users/me

Completed:

```text
Added GetCurrentUserQuery.
Added GetCurrentUserHandler.
Added UsersController.GetMe.
Added integration tests.
Disabled users can read own profile.
```

---

## 8.5D — PUT /api/users/me/profile

Completed:

```text
Added UpdateUserProfileRequest.
Added UpdateCurrentUserProfileCommand.
Added UpdateCurrentUserProfileHandler.
Added UsersController.UpdateProfile.
Added integration tests.
Active and PendingVerification users can update.
Disabled users are blocked.
Read-only fields cannot be changed through this endpoint.
```

---

## 8.5E — PUT /api/users/me/avatar

Completed:

```text
Added UploadCurrentUserAvatarCommand.
Added UploadCurrentUserAvatarHandler.
Extended IFileStorageService with avatar save/delete methods.
Extended LocalFileStorageService with avatar save/delete methods.
Added UsersController.UploadAvatar.
Added integration tests.
PUT is used for both first upload and replace.
```

---

## 8.5F — DELETE /api/users/me/avatar

Completed:

```text
Added DeleteCurrentUserAvatarCommand.
Added DeleteCurrentUserAvatarHandler.
Added UsersController.DeleteAvatar.
Added integration tests.
Delete is idempotent.
Avatar fields are cleared.
Stored avatar file is deleted when present.
```

---

## 8.5G — Docs/context update

Completed:

```text
Updated Chapter 8.5 rules document with final status and implemented behavior.
backend-context.md must be updated with the compressed Chapter 8.5 handoff summary and locked roadmap.
```

Locked roadmap to add to backend context:

```text
Chapter 9 — Agency Phase 2
Chapter 9.5 — Frontend readiness
Then frontend
```

---

## Completion checklist

Chapter 8.5 is complete because:

```text
Rules document exists.
GET /api/users/me works.
PUT /api/users/me/profile works.
PUT /api/users/me/avatar works for first upload and replace.
DELETE /api/users/me/avatar works.
Disabled users can read profile but cannot mutate profile/avatar.
PendingVerification users can read and mutate profile/avatar.
Avatar validation is tested.
Avatar fields are persisted.
Avatar replacement updates metadata.
Avatar delete clears avatar fields.
Controllers remain thin.
Handlers contain use-case logic.
Repositories remain data-focused.
No duplicate existing User fields were added.
No unrelated auth/account features were added.
dotnet build passes.
dotnet test passes.
Chapter 8.5 rules document is updated with final behavior.
backend-context.md is updated with Chapter 8.5 completion and locked roadmap.
```

---

## Final Chapter 8.5 context summary

```text
Chapter 8.5 completed:
- Added current-user account/profile basics.
- Added GET /api/users/me.
- Added PUT /api/users/me/profile.
- Added PUT /api/users/me/avatar for upload and replace.
- Added DELETE /api/users/me/avatar.
- Added avatar fields to User.
- Added local avatar storage under /uploads/users/{userId}/avatar/{storedFileName}.
- Active and PendingVerification users can update profile/avatar.
- Disabled users can read profile but cannot mutate profile/avatar.
- UserProfileResponse is the consistent current-user response shape.
- No email/password/public-profile/account-admin features were added.
```
