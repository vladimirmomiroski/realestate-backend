# Backend Quality Handoff

## Purpose

This file tracks only verified unresolved backend issues, risks, and deferred hardening work.

It is a live source-controlled issue register, not a project history, completed-work log, second backend context, or resolved-findings archive.

## Maintenance Rules

- Add only evidence-based issues.
- Every issue must include repository evidence.
- Every issue must include the smallest safe direction.
- Remove the entire issue after it is fixed, tested, and reviewed.
- Do not keep a resolved/archive section.
- Update the relevant chapter document and backend-context.md when a fix changes a lasting rule.
- Review this file at the end of every chapter.

## Unresolved Issues

- **Last-owner role-change concurrency**
  - Area: Agency member role changes.
  - Risk: Two concurrent Owner -> Agent demotions may both observe a safe active-owner count and leave the agency without an active Owner.
  - Evidence: `ChangeAgencyMemberRoleHandler` calls `IAgencyRepository.CountActiveOwnersAsync(...)` before `AgencyMember.ChangeRole(...)`; there is no transaction, row lock, concurrency token, or database-backed invariant protecting the count-and-mutate sequence.
  - Smallest safe direction: Add concurrency protection for Owner -> Agent demotion, or enforce the invariant with a database-backed strategy, then cover the behavior with targeted tests.
  - Target chapter: Chapter 11 — Data Integrity and Targeted Hardening.

- **Disabled listing creators can still mutate listing images**
  - Area: Listing image mutations.
  - Risk: A Disabled user who created a listing can still upload, delete, reorder, or set primary listing images if their JWT is otherwise valid.
  - Evidence: `UploadListingImageHandler`, `DeleteListingImageHandler`, `SetPrimaryListingImageHandler`, and `ReorderListingImagesHandler` enforce authenticated `CreatedByUserId` ownership but do not reload the current `User` or check `UserStatus.Disabled`.
  - Smallest safe direction: Decide whether listing image mutations should follow the same Disabled-user blocking rule as listing creation and listing status transitions; if yes, reload the current user in the image handlers or centralize the check behind a focused listing-owner guard.
  - Target chapter: Chapter 11 — Data Integrity and Targeted Hardening.

- **Deterministic listing test setup uses raw SQL**
  - Area: Listing integration-test fixtures.
  - Risk: `ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(...)` bypasses normal EF change tracking for deterministic status and timestamp setup, which makes the fixture less aligned with the preferred test setup style.
  - Evidence: The helper calls `RealEstateDbContext.Database.ExecuteSqlInterpolatedAsync(...)` to update `Status` and `CreatedAtUtc`; the raw SQL is test-only and is not used by production query or repository code.
  - Smallest safe direction: Later replace the helper with `ExecuteUpdateAsync` or tracked EF setup if that remains practical and deterministic.
  - Acceptance: This is low-priority test cleanup, not a production architecture issue, and does not block Chapter 10B.
  - Target task: Low-priority test cleanup.

- **Invitation expiry can remain status-stale until touched**
  - Area: Agency invitation lifecycle and API contract.
  - Risk: An invitation can remain `Status = Pending` after `ExpiresAtUtc` has passed, so list responses may show an expired-but-still-Pending row until accept/cancel logic touches and marks it Expired.
  - Evidence: `AcceptAgencyInvitationHandler` and `CancelAgencyInvitationHandler` mark a Pending invitation Expired when `ExpiresAtUtc <= utcNow`; `GetAgencyInvitationsHandler` lists invitations by stored status only; dashboard summary deliberately counts only `Status == Pending && ExpiresAtUtc > utcNow`.
  - Smallest safe direction: Decide whether invitation list responses should expose stored status only, compute an effective status, or run an explicit expiration process before listing.
  - Target chapter: Chapter 12 — API Consistency, Observability, and Frontend Readiness.

- **API error response shapes are inconsistent**
  - Area: HTTP error contracts.
  - Risk: Frontend clients must handle multiple error shapes for similar failures, including plain strings, `{ message = ... }` objects, empty `Forbid()` responses, and default bad-request bodies.
  - Evidence: `UsersController` and `AuthController` often return anonymous `{ message = ... }` objects; `ListingsController`, `AgenciesController`, and `AdminAgenciesController` often return `BadRequest(result.Error)` or `Unauthorized(result.Error)`; forbidden responses usually use `Forbid()` with no response body.
  - Smallest safe direction: Choose a small frontend-facing error contract, such as consistent `ProblemDetails` or a simple `{ message }` shape, and apply it endpoint-by-endpoint without changing business behavior.
  - Target chapter: Chapter 12 — API Consistency, Observability, and Frontend Readiness.

- **Pagination contracts are duplicated**
  - Area: API pagination contracts.
  - Risk: Two paginated response types can drift and make frontend contracts harder to standardize.
  - Evidence: `PagedResult<T>` and `PagedResponse<T>` both expose `Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`, `HasNextPage`, and `HasPreviousPage`; public listing search returns `PagedResponse<ListingResponse>`, while my listings, agency listings, and dashboard listings return `PagedResult<ListingResponse>`.
  - Smallest safe direction: Choose one public pagination contract and use it consistently across public, personal, agency, and dashboard listing endpoints.
  - Target chapter: Chapter 12 — API Consistency, Observability, and Frontend Readiness.

- **Location filters have broad translation matching semantics**
  - Area: Listing search filters.
  - Risk: Location filters may match a listing through one translation while the response is rendered in another requested language, and leading-wildcard `ILike` filters can become slow as data grows.
  - Evidence: `ListingRepository.ApplyLocationFilters(...)` searches `City`, `Municipality`, and `Neighborhood` with `EF.Functions.ILike(..., "%term%")` across `Listing.Translations.Any(...)`; it does not scope those filters to the requested display language.
  - Smallest safe direction: In Chapter 10, define whether location filters search all translations or only the requested language, then decide whether indexing, normalized location fields, or full-text/search infrastructure is justified.
  - Target chapter: Chapter 10 — Search and Discovery Phase 2.

- **Listing image upload can orphan a file if database persistence fails**
  - Area: Local file storage and listing image upload.
  - Risk: A physical listing image file can remain on disk if the file save succeeds but `SaveChangesAsync` fails while adding the `ListingImage` row.
  - Evidence: `UploadListingImageHandler` calls `SaveListingImageAsync(...)`, then adds the `ListingImage`, then calls `SaveChangesAsync(...)` without a cleanup catch; `UploadCurrentUserAvatarHandler` and `UploadAgencyLogoHandler` already delete the newly stored file when database persistence fails.
  - Smallest safe direction: Add the same cleanup-on-save-failure pattern used by avatar and agency logo upload, then cover the behavior with a focused test or storage fake if practical.
  - Target chapter: Chapter 11 — Data Integrity and Targeted Hardening.
