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

- **Transaction cleanup can replace an in-flight exception**
  - Area: Chapter 11A agency-owner transaction cleanup.
  - Risk: In the rare case where rollback or transaction disposal fails while another exception is already propagating, the cleanup exception may replace the original exception. This is mainly an observability and diagnostic risk, not a proven last-owner invariant gap.
  - Evidence: `AgencyRepository.BeginLastActiveOwnerMutationAsync(...)` rolls back and disposes inside its exception path, and `AgencyOwnerMutationScope.DisposeAsync()` performs the same cleanup for an uncommitted scope; neither path separately preserves an already-propagating exception if cleanup itself fails.
  - Smallest safe direction: Revisit exception preservation only if a later transaction or observability checkpoint naturally introduces a broader policy for retaining original and cleanup failures.
  - Classification: Non-blocking, not required to reopen Chapter 11A, and suitable for evidence-based cleanup when related future work touches the same code.
  - Target task: Related future transaction or observability cleanup.

- **Concurrency-test request tasks are not drained after orchestration failure**
  - Area: Chapter 11A deterministic concurrency-test cleanup.
  - Risk: If deterministic test orchestration fails after HTTP request tasks start but before the normal awaits, the database gate is released but those tasks are not explicitly drained. The shared timeout bounds them, so this is test-failure-path hygiene rather than incomplete concurrency evidence.
  - Evidence: `AgenciesEndpointTests.ExecuteContestedOwnerMutationAsync(...)` releases `gateTransaction` in `finally`, while `firstRequestTask` and `secondRequestTask` are awaited only on the normal path after both PostgreSQL blocking relationships are established.
  - Smallest safe direction: Revisit task cancellation and draining only if later concurrency checkpoints naturally reuse or extract this test-local coordination mechanism.
  - Classification: Non-blocking, not required to reopen Chapter 11A, and suitable for evidence-based cleanup when related future work touches the same code.
  - Target task: Related future concurrency-test cleanup.

- **CH11-DB-01: Listing creator relationship remains nullable**
  - Area: Listing relational model and deployed-data compatibility.
  - Risk: PostgreSQL permits a listing without `CreatedByUserId`; making the relationship required without knowing deployed data or an authorized backfill could make an otherwise desirable invariant unsafe to migrate.
  - Evidence: `Listing.CreatedByUserId` remains nullable and `ListingConfiguration` configures the existing optional creator relationship with Restrict delete behavior; Chapter 11 added no schema change.
  - Smallest safe direction: Perform an authorized data audit, choose a backfill policy, and implement one focused migration only if the owner approves a required creator relationship.
  - Classification: Accepted owner decision; Chapter 11 is complete without resolving it.
  - Target task: Separate owner-approved data/migration checkpoint.

- **CH11-DB-02: Request validation is not duplicated broadly as database checks**
  - Area: Listing numeric, range, and coordinate integrity.
  - Risk: Direct database writes are not guarded by every rule enforced by request validators, while adding blanket constraints could reject legacy data or prematurely encode product policy.
  - Evidence: Listing validators enforce business ranges, while current listing EF configurations and the 15 committed migrations do not define a corresponding comprehensive check-constraint family.
  - Smallest safe direction: Audit deployed data and approve each constraint family before adding a focused migration.
  - Classification: Accepted owner decision; Chapter 11 is complete without resolving it.
  - Target task: Separate owner-approved data/migration checkpoint.

- **CH11-STATE-01: Broad concurrency and authorization freshness remain undefined**
  - Area: Cross-aggregate state transitions and in-flight authorization.
  - Risk: Commands outside the specifically protected agency-owner, invitation, and listing-image invariants may retain last-write-wins behavior or complete after a later authorization change.
  - Evidence: Chapter 11 adds narrow parent/row write scopes only to the protected handlers and does not add concurrency tokens or a global actor-freshness policy.
  - Smallest safe direction: Decide the desired policy per aggregate, then implement focused optimistic-concurrency or post-wait authorization tasks rather than a generic framework.
  - Classification: Accepted owner decision; Chapter 11 is complete without resolving it.
  - Target task: Separate owner-approved concurrency/authorization work.

- **CH11-FILE-01: Post-commit physical deletion is not durably recoverable**
  - Area: Database/media deletion boundary.
  - Risk: A database media deletion can commit and a later physical-file deletion can fail, leaving an orphan file.
  - Evidence: `DeleteListingImageHandler` commits and disposes its listing-image write scope before calling `DeleteListingImageAsync`; upload compensation does not and cannot reverse this post-commit boundary.
  - Smallest safe direction: Either formally accept the orphan risk or design a persisted deletion intent with retry/reconciliation as a separate operational feature.
  - Classification: Accepted limitation; Chapter 11 intentionally preserves the existing post-commit exception behavior.
  - Target task: Separate owner-approved durable media-cleanup workflow.

- **Deterministic listing test setup uses raw SQL**
  - Area: Listing integration-test fixtures.
  - Risk: `ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(...)` bypasses normal EF change tracking for deterministic status and timestamp setup, which makes the fixture less aligned with the preferred test setup style.
  - Evidence: The helper calls `RealEstateDbContext.Database.ExecuteSqlInterpolatedAsync(...)` to update `Status` and `CreatedAtUtc`; the raw SQL is test-only and is not used by production query or repository code.
  - Smallest safe direction: Later replace the helper with `ExecuteUpdateAsync` or tracked EF setup if that remains practical and deterministic.
  - Acceptance: This remains low-priority test cleanup, not a production architecture issue, and did not block Chapter 10 completion.
  - Target task: Low-priority test cleanup.

- **CH11-STATE-02: Invitation expiry can remain status-stale until touched**
  - Area: Agency invitation lifecycle and API contract.
  - Risk: An invitation can remain `Status = Pending` after `ExpiresAtUtc` has passed, so list responses may show an expired-but-still-Pending row until accept/cancel logic touches and marks it Expired.
  - Evidence: `AcceptAgencyInvitationHandler` and `CancelAgencyInvitationHandler` mark a Pending invitation Expired when `ExpiresAtUtc <= utcNow`; `GetAgencyInvitationsHandler` lists invitations by stored status only; dashboard summary deliberately counts only `Status == Pending && ExpiresAtUtc > utcNow`.
  - Smallest safe direction: Decide whether invitation list responses should expose stored status only, compute an effective status, or run an explicit expiration process before listing.
  - Classification: Accepted lifecycle and API-contract decision; Chapter 11 preserved and tested the action-triggered behavior.
  - Target chapter: Chapter 12 — API Consistency, Observability, and Frontend Readiness.

- **Race-time unique conflicts are not translated consistently**
  - Area: Registration, agency creation, and HTTP conflict handling.
  - Risk: Two concurrent requests can both pass an application-level uniqueness precheck, after which one request loses at the PostgreSQL unique index and may surface an unhandled database exception instead of the existing duplicate business outcome.
  - Evidence: `RegisterUserHandler` checks normalized-email availability before inserting a user, and `CreateAgencyHandler` checks slug availability before inserting an agency; PostgreSQL has unique indexes for both values, but production code does not narrowly translate the resulting expected unique-constraint violations.
  - Smallest safe direction: Keep the database constraints authoritative, catch only the expected named constraint at each affected use-case boundary, translate it to the existing duplicate/conflict result, and rethrow unrelated database failures.
  - Scope note: The invitation-specific pending-invitation conflict is handled in Chapter 11; registration email and agency slug remain deferred.
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
