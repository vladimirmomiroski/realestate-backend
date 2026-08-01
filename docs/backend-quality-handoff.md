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

- **QH-TX-01: Transaction cleanup can replace an in-flight exception**
  - Area: Agency-owner transaction cleanup.
  - Risk: If rollback or transaction disposal fails while another exception is propagating, the cleanup exception may replace the original. Chapter 12's exception boundary can log only the exception that reaches it.
  - Evidence: `AgencyRepository.BeginLastActiveOwnerMutationAsync(...)` and `AgencyOwnerMutationScope.DisposeAsync()` perform rollback/disposal without a separate original-exception preservation mechanism.
  - Smallest safe direction: Revisit only with focused transaction-cleanup work that defines how both failures are retained.
  - Classification: Non-blocking diagnostic risk; Chapter 12 did not claim to resolve it.

- **QH-TEST-01: Concurrency-test request tasks are not drained after orchestration failure**
  - Area: Deterministic agency-owner concurrency-test cleanup.
  - Risk: If orchestration fails after request tasks start but before their normal awaits, the database gate is released but the request tasks are not explicitly drained. The shared timeout still bounds them.
  - Evidence: `AgenciesEndpointTests.ExecuteContestedOwnerMutationAsync(...)` releases `gateTransaction` in `finally`; the request tasks are awaited only on the normal path.
  - Smallest safe direction: Add cancellation/draining if this coordination helper is next reused or extracted.
  - Classification: Low-priority test-failure-path hygiene, not missing invariant proof.

- **CH11-DB-01: Listing creator relationship remains nullable**
  - Area: Listing relational model and deployed-data compatibility.
  - Risk: PostgreSQL permits a listing without `CreatedByUserId`; making it required without an authorized data audit/backfill may be unsafe.
  - Evidence: `Listing.CreatedByUserId` remains nullable and `ListingConfiguration` retains the optional Restrict relationship. The model is clean at 15 migrations.
  - Smallest safe direction: Perform an authorized data audit and backfill decision before a focused migration.
  - Classification: Accepted owner decision.

- **CH11-DB-02: Request validation is not duplicated broadly as database checks**
  - Area: Listing numeric, range, and coordinate integrity.
  - Risk: Direct database writes are not guarded by every request-validator rule; blanket constraints could reject legacy data or prematurely encode policy.
  - Evidence: Validators enforce the business ranges, while the current EF model and 15 migrations have no comprehensive matching check-constraint family.
  - Smallest safe direction: Audit deployed data and approve each constraint family before a focused migration.
  - Classification: Accepted owner decision.

- **CH11-STATE-01: Broad concurrency and authorization freshness remain undefined**
  - Area: Cross-aggregate state transitions and in-flight authorization.
  - Risk: Commands outside specifically protected invariants may retain last-write-wins behavior or complete after a later authorization change.
  - Evidence: Chapters 11–12 protect named owner, invitation, listing-image, normalized-email, and slug boundaries and normalize request-time principal state, but add no global concurrency token or in-flight freshness framework.
  - Smallest safe direction: Choose policy per aggregate and add focused optimistic-concurrency or post-wait authorization proof only where product evidence requires it.
  - Classification: Accepted owner decision; the narrower request-time and named-conflict guarantees are complete.

- **CH11-FILE-01: Post-commit physical deletion is not durably recoverable**
  - Area: Database/media deletion boundary.
  - Risk: A database deletion can commit and a later physical-file deletion can fail, leaving an orphan file.
  - Evidence: `DeleteListingImageHandler` commits before physical deletion. Chapter 12 adds canonical failure translation, request IDs, and logging but no persisted deletion intent or retry.
  - Smallest safe direction: Formally accept the orphan risk or design a persisted deletion-intent/reconciliation workflow as separate operational work.
  - Classification: Accepted limitation.

- **QH-TEST-02: Deterministic listing test setup uses raw SQL**
  - Area: Listing integration-test fixtures.
  - Risk: `ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(...)` bypasses normal EF change tracking for deterministic setup.
  - Evidence: The helper uses `ExecuteSqlInterpolatedAsync(...)`; this SQL is test-only and is not used by production repositories.
  - Smallest safe direction: Replace it with `ExecuteUpdateAsync` or tracked EF setup if that remains equally deterministic.
  - Classification: Low-priority test cleanup.

- **C12-CONFIG-01: Base JWT placeholder can flow to non-Development hosts**
  - Area: Production authentication configuration.
  - Risk: The local JWT placeholder in base `appsettings.json` is not a production-grade secret and could be used if deployment configuration fails to override it.
  - Evidence: Chapter 12 intentionally left JWT secret loading unchanged; its frontend-readiness result covers Bearer API integration, not production secret provisioning or startup validation.
  - Smallest safe direction: Relocate/validate the secret in Chapter 13 or an explicit deployment-hardening checkpoint, with safe local/test configuration and startup-failure tests.
  - Classification: Production-deployment blocker; not a blocker for the current local frontend foundation.
