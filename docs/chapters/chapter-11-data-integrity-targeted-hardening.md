# Chapter 11 — Data Integrity and Targeted Hardening

## 1. Purpose

Chapter 11 hardens already-approved agency, invitation, and listing-image behavior against concurrent requests and partial failures. It does not introduce new product capabilities. Its purpose is to make the existing invariants remain true when requests overlap, persistence fails, file copying is interrupted, or a database uniqueness constraint becomes the final arbiter of a race.

The chapter is implementation-ready for eight confirmed, bounded findings. Each correction is intentionally narrow, uses the existing Controller → handler → repository interface → Infrastructure repository → PostgreSQL flow, and must preserve current HTTP contracts unless this document explicitly says otherwise.

This document is the permanent Chapter 11 plan. The temporary audit handoff under `docs/planning/` remains supporting evidence only.

## 2. Baseline

The plan starts from the completed Chapter 10 repository state:

- The audited code baseline is commit `58425a048ae0949f64d96e7b62ef05a8308aa2e4` from updated `development`; the documentation planning branch points at the same commit.
- PostgreSQL through EF Core and Npgsql remains the only relational persistence path.
- The verified full-suite baseline is 631 passing tests.
- The model has 15 committed migrations; the latest is `20260721112146_AddListingTranslationQTrigramIndex`.
- The EF model and committed migrations are aligned.
- Chapter 10 query shapes, `pg_trgm` search index, permanent evidence, and benchmark gates are complete and outside Chapter 11 scope.
- Agency membership has a unique database index on `(AgencyId, UserId)`.
- Agency invitations have a unique token index and a filtered unique index on `(AgencyId, NormalizedEmail)` while stored status is `Pending`.
- Listing images have a filtered unique index permitting at most one row with `IsPrimary = true` per listing; `(ListingId, SortOrder)` is indexed but not unique.
- Foreign keys and current delete behaviors prevent the relational child-orphan concerns rejected by the audit.
- `SaveChangesAsync` is atomic for one database save, but the current repository methods named `ForUpdateAsync` are ordinary tracked EF reads and do not acquire PostgreSQL row locks.
- No Chapter 11 implementation checkpoint is complete when this plan is authored.

The chapter baseline does not assume that application prechecks are concurrency controls. A uniqueness check followed by an insert, or a count followed by a mutation, must be assessed as one concurrent operation.

## 3. Principles

1. Preserve approved behavior. Hardening may prevent an invalid interleaving, but it must not silently redesign state machines, authorization, response envelopes, or media contracts.
2. Put use-case decisions in handlers and provider-specific locking, transactions, and PostgreSQL exception inspection in Infrastructure.
3. Use the narrowest serialization target that protects the invariant: the common `Agency` row for owner-set changes, the invitation row for invitation terminal transitions, and the common `Listing` row for image-aggregate mutations.
4. Acquire a lock before the protected re-read, check, count, or mutation. A tracked query alone is not a lock.
5. Use a consistent lock order for every operation participating in the same invariant.
6. Do not hold a database lock while copying a user-provided file.
7. Retain database constraints as final guards. Translate only known, named conflicts at the affected use-case boundary and rethrow unrelated database failures.
8. Keep filesystem and database compensation explicit. A database transaction cannot make a filesystem write atomic.
9. Add deterministic PostgreSQL concurrency tests and precise failure-injection tests; uncontrolled `Task.WhenAll` is not proof that a race was exercised.
10. Prefer a local correction over a generic UnitOfWork, transaction manager, locking framework, retry framework, or new architecture abstraction.
11. One focused outcome is one checkpoint and one commit.

## 4. Confirmed findings and disposition

| Audit ID | Confirmed finding | Disposition |
|---|---|---|
| CH11-OWN-01 | Owner-to-Agent demotion and member disable use check/count-then-mutate flows that can jointly leave an agency with no active owner. | Implement in 11A. |
| CH11-INV-01 | Concurrent invitation accept/cancel/expire operations can overwrite terminal state, and membership creation can diverge from the winning transition. | Implement in 11B. |
| CH11-INV-02 | An elapsed invitation may remain stored as `Pending`, block a replacement through the filtered unique index, and race with another create. | Implement in 11C while preserving Chapter 9's action-triggered expiry rule. |
| CH11-IMG-01 | A disabled listing creator can still upload, delete, set primary, and reorder listing images. | Implement in 11D. |
| CH11-IMG-03 | Direct copying to a final storage path can leave a partial file after failure or cancellation. | Implement in 11E. |
| CH11-IMG-02 | A successfully written listing-image file remains orphaned if later database persistence fails. | Implement in 11F after 11E. |
| CH11-IMG-05 | Concurrent uploads can exceed the 20-image cap, calculate the same append order, or race when choosing the first primary image. | Implement in 11G after storage compensation exists. |
| CH11-IMG-04 | Set-primary and primary-delete use multiple saves without one transaction and can leave zero primary after a mid-operation failure. | Implement in 11H; make all image mutations participate in the listing lock. |

The audit also confirmed three issues that are not Chapter 11 implementation work:

- CH11-API-01 remains a Chapter 12 concern for general registration-email and agency-slug conflict normalization. The invitation-specific conflict is handled narrowly in 11C.
- CH11-API-02, inconsistent API error shapes, remains Chapter 12 work.
- CH11-API-03, duplicated pagination response types, remains Chapter 12 work.

The following concerns are not implementation findings for this chapter:

- CH11-DB-03 is rejected: current keys, foreign keys, unique indexes, and delete behaviors already prevent the alleged relational duplicates and child orphans.
- CH11-INV-03 is rejected as a separate issue: one `SaveChangesAsync` already makes membership insertion and invitation mutation atomic in the sequential case. Its real concurrent form is covered by 11B.
- CH11-AGY-01 is rejected: initial agency and owner membership are persisted in one aggregate save.
- CH11-IMG-06 is rejected as stated: the filtered unique index already prevents multiple primary rows. The uncovered zero-primary failure is CH11-IMG-04.
- CH11-TEST-01 is an accepted testing technique: focused, parameterized raw SQL used for deterministic test setup is not a production defect.
- CH11-NULL-01 is an accepted current model tradeoff: nullable agency and invitation-acceptor relationships are consistent with existing lifecycle semantics.

## 5. Owner decisions

Four audited areas remain intentionally unresolved. None may be implemented implicitly inside the locked checkpoints.

### CH11-DB-01 — Required listing creator relationship

The repository has not established whether any deployed data contains `Listings.CreatedByUserId = null` or what a valid backfill would be. Making the relationship required would need an authorized data audit, a backfill policy, and a separate migration.

Locked interim disposition: retain the nullable relationship. If the owner chooses a required relationship, amend this plan with one separate migration checkpoint; do not add it to another task.

### CH11-DB-02 — Database check-constraint coverage

Request validation enforces several numeric, range, and coordinate rules that PostgreSQL does not duplicate. A blanket set of check constraints could reject legacy data or encode product policy in the schema without an approved compatibility decision.

Locked interim disposition: retain current validation. Any approved constraint family must first receive a data audit and then become its own focused task and migration.

### CH11-STATE-01 — Broad concurrency and authorization-freshness policy

The application has no general concurrency token or policy for every listing, user, agency, and membership transition. Chapter 11 will revalidate the actor membership inside 11A because it is necessary to protect the owner-set invariant, and will serialize the invitation and image operations explicitly listed here. It will not decide whether every already-authorized in-flight command must observe later revocation.

Locked interim disposition: preserve current last-write-wins behavior outside the protected invariants. A broader optimistic-concurrency or authorization-freshness policy needs an owner decision and separate per-aggregate tasks.

### CH11-FILE-01 — Post-commit physical deletion durability

Deleting a database media row and then failing to delete the physical file can leave an orphaned file. Fully durable recovery would require an approved operational policy and likely a persisted deletion intent, retry worker, or reconciliation process.

Locked interim disposition: retain the current post-commit deletion boundary in 11H and report failures through existing behavior. Do not introduce an outbox or background worker. The owner must later choose between accepting this operational risk and funding a separately designed durable cleanup workflow.

These decisions are non-blocking for the eight locked corrections, but 11I must preserve their unresolved status in permanent project documentation.

## 6. Locked technical decisions

### 6.1 Transaction and lock ownership

- Explicit transactions and parameterized PostgreSQL `SELECT ... FOR UPDATE` statements are permitted only in Infrastructure repository implementations.
- Application repository interfaces may expose a narrowly named agency-, invitation-, or listing-image write scope, but must not expose `DbContext`, `IDbContextTransaction`, Npgsql types, SQL text, or a generic UnitOfWork.
- The handler retains business validation and state-transition decisions. The Infrastructure scope begins the transaction, acquires the required row lock, allows the use case to perform its protected work using the same scoped `RealEstateDbContext`, and commits or rolls back.
- Every protected entity or child collection must be materialized or explicitly reloaded after the transaction and row lock are established. Pre-lock access/route probes must be no-tracking projections and must never be reused as the protected write graph through EF identity resolution.
- Lock acquisition must accept the request cancellation token. Do not use `NOWAIT`, `SKIP LOCKED`, or an automatic retry policy in these checkpoints.
- Existing methods whose names include `ForUpdateAsync` must not be treated as locks. Do not broadly rename or rewrite unrelated callers.

### 6.2 Agency ownership serialization

- Both Owner-to-Agent demotion and disabling an active Owner lock the same `Agencies` row first.
- Under that lock, re-read the acting membership and target membership, verify the actor is still an Active Owner, recount Active Owners, and only then mutate.
- Disabling the last active Owner receives the same existing validation-class outcome as demoting the last active Owner. Existing self-disable and authorization behavior remains intact.
- Lock order is `Agency` parent, then member reads/mutations. No member row is locked first.

### 6.3 Invitation serialization and conflicts

- Accept and cancel use an explicit transaction and lock the target `AgencyInvitations` row before re-evaluating status and expiry.
- Acceptance keeps membership insertion and `Pending -> Accepted` in one transaction and one atomic save boundary. Cancellation and action-triggered expiry use the same invitation-row protocol.
- After waiting for a lock, a losing request evaluates the committed terminal state and returns the existing validation-class business outcome. Chapter 11 does not introduce HTTP 409 or a new error envelope.
- Infrastructure may translate only PostgreSQL unique violations for the known membership index `IX_AgencyMembers_AgencyId_UserId` and pending-invitation index `IX_AgencyInvitations_AgencyId_NormalizedEmail`. It returns a provider-neutral result; Application must not reference EF or Npgsql exception types.
- A translated unique violation first rolls back and disposes the aborted PostgreSQL transaction. Failed tracked state is discarded before any re-read or provider-neutral result is returned; an aborted transaction is never committed or reused.
- An unrelated constraint or database error is rethrown.

### 6.4 Invitation expiry semantics

- Chapter 9's rule remains authoritative: an elapsed invitation may stay stored as `Pending` until an action observes it and marks it `Expired`.
- Creating another invitation for the same agency and normalized email is such an action.
- Under one transaction, creation checks the matching stored `Pending` row. A live row is rejected through existing behavior. An elapsed row is locked and marked `Expired`; the status update is saved first so it leaves the partial unique-index predicate, then the replacement is inserted and saved, and only then does the outer transaction commit. A second-phase failure rolls both saves back.
- After any wait/lock and before insertion, creation re-resolves the invited user by normalized email and rechecks agency membership. This prevents acceptance of the old invitation from racing with creation of a new invitation for the member who just joined.
- If no matching row exists, the existing filtered unique index is the final concurrent-create guard.
- Do not add a background expiry service, an effective-status rewrite for list responses, or a time-dependent partial index.

### 6.5 Listing-image serialization

- The common serialization row is the `Listings` parent row.
- Upload, set-primary, delete, and reorder must ultimately acquire that row before reloading and mutating images. Lock order is `Listing` parent, then image children.
- File copying occurs before the upload database lock. If the request loses the capacity race or persistence fails, the exact newly written file is compensated.
- Set-primary and primary-delete may retain their existing two SQL phases needed by the filtered unique-primary index, but both phases must be inside one explicit transaction.
- Reorder participates in the same listing lock so that it cannot race with append/delete and overwrite aggregate order.
- Retain the filtered unique-primary index and the nonunique `(ListingId, SortOrder)` index. Do not add a unique sort-order constraint: it would not enforce the cap and would complicate valid reorder updates.

### 6.6 File safety

- `LocalFileStorageService` writes to a unique temporary file in the final target directory, completes and disposes the copy, then moves it to the generated final name on the same volume.
- Failure or cancellation removes the temporary file and exposes no partial final file.
- The current listing repository has one observable persistence boundary: `SaveChangesAsync` delegates directly to EF Core `DbContext.SaveChangesAsync`. EF Core automatically wraps that single call in a transaction; for the ordinary failure path, a thrown save means the changes are rolled back, while a successful return ends compensation responsibility.
- After storage succeeds, the listing upload handler places entity creation, tracking, and the single repository save inside one `try` block. An exception before `SaveChangesAsync` returns successfully triggers compensation for exactly that request's new file through `DeleteListingImageAsync` with `CancellationToken.None`.
- When compensation cleanup succeeds, rethrow the original persistence failure. When compensation cleanup also fails, throw one `AggregateException` containing the original persistence failure first and the cleanup failure second. Do not add a logging package merely for this checkpoint.
- The current repository interface cannot classify a provider/network failure whose true server-side commit outcome is indeterminate. 11F does not introduce such a classifier or claim distributed atomicity; genuinely ambiguous cross-resource outcomes and post-commit physical deletion durability remain CH11-FILE-01.

### 6.7 Schema decision

No locked checkpoint requires a model change or migration. Existing constraints remain the database backstops. If implementation evidence shows a schema change is necessary, stop that checkpoint and obtain an owner-approved plan amendment rather than generating a migration opportunistically.

## 7. Scope

Chapter 11 includes:

- serialized last-active-owner enforcement for role change and member disable;
- serialized invitation terminal transitions and atomic acceptance;
- action-triggered expiry and safe replacement of an elapsed pending invitation;
- disabled-user authorization on all four listing-image mutation endpoints;
- residue-free local file writes on failure and cancellation;
- listing-upload compensation when database persistence fails;
- serialized listing-image upload capacity, append ordering, and first-primary selection;
- atomic and serialized set-primary, primary-delete, and reorder behavior;
- focused deterministic concurrency, failure-path, and PostgreSQL constraint tests;
- final migration/model/catalog verification and documentation closeout.

## 8. Non-goals

Chapter 11 does not include:

- a generic repository, UnitOfWork, transaction manager, lock service, retry framework, or architecture rewrite;
- global optimistic concurrency or concurrency tokens for every aggregate;
- a new API error envelope, global conflict mapping, or pagination consolidation;
- a background invitation-expiry service or a changed invitation list contract;
- a durable filesystem outbox, deletion worker, media reconciliation subsystem, cloud-storage migration, or media redesign;
- a new unique image sort-order constraint;
- making `Listings.CreatedByUserId` required without the owner/data decision;
- speculative database check constraints;
- changes to Chapter 10 search, comparables, query-review tooling, benchmark gates, indexes, or evidence;
- new listing/user/agency product transitions;
- cleanup of unrelated code or test-only parameterized raw SQL;
- Chapter 12 API consistency, observability, or frontend-readiness work.

## 9. Task and commit policy

- The sequence contains nine checkpoints: three easy and six medium.
- Each checkpoint has one outcome and produces one commit. Do not combine checkpoints even when a later task reuses an earlier lock or compensation boundary.
- A checkpoint may change only the files needed for its outcome. No “while here” cleanup is allowed.
- Each implementation commit must include its focused tests and leave the solution build and full test suite green.
- Provider-specific code remains in Infrastructure; Domain and Application remain provider-neutral.
- New test helpers must be local to the focused test area unless two completed checkpoints demonstrate an actual reuse need.
- No checkpoint may silently resolve an owner decision from section 5.
- If a checkpoint exceeds roughly eight related files, requires a public contract change, or requires a migration not specified here, stop and split or amend the plan before implementation.
- Commit messages should describe the integrity outcome, not the mechanical technique.

## 10. Sequential implementation checkpoints

### 11A — Serialize last-active-owner mutations

- **Size:** medium.
- **Outcome:** concurrent role changes and member disables cannot reduce an agency below one Active Owner.
- **Finding addressed:** CH11-OWN-01.
- **Locked implementation direction:** add a narrow Infrastructure-owned agency write scope that starts a transaction and locks the `Agencies` parent row. Change both handlers to re-read the acting and target memberships and recount active owners under that lock. Reject both demotion and disable when the target is the final Active Owner. Preserve existing self-disable, inactive-member, not-found, and authorization outcomes.
- **Likely files/layers:** `ChangeAgencyMemberRoleHandler.cs`; `DisableAgencyMemberHandler.cs`; `IAgencyRepository.cs`; `AgencyRepository.cs`; existing member role/disable tests plus, if clearer, `AgenciesEndpointTests.MemberConcurrency.cs`.
- **Migration/transaction/constraint requirements:** explicit transaction and parameterized parent-row `FOR UPDATE`; retain `IX_AgencyMembers_AgencyId_UserId`; no migration.
- **Focused tests:** two separately scoped requests that both attempt to demote/disable the two owners; demote-versus-disable; final Active Owner count is one; exactly one valid mutation wins; actor membership is revalidated after waiting; existing sequential role and disable cases remain unchanged.
- **Full verification:** build the solution; run the full test suite; run the pending-model check; run diff checks and inspect Git state.
- **Non-goals:** no broad membership concurrency token, no invitation changes, no global in-flight authorization policy, and no response-envelope change.
- **Completion criteria:** deterministic PostgreSQL tests exercise the read-before-write race and prove zero-owner state is impossible; all existing behavior and the full suite pass; no schema diff exists.
- **Expected commit purpose:** `Protect the last active agency owner under concurrency`.

### 11B — Serialize invitation terminal transitions

- **Size:** medium.
- **Outcome:** exactly one terminal invitation transition wins, and an accepted invitation cannot diverge from its membership row.
- **Finding addressed:** CH11-INV-01.
- **Locked implementation direction:** add a narrow invitation transaction/row-lock operation in the invitation repository. Accept and cancel lock the invitation before checking status or expiry. Acceptance inserts membership and marks the invitation Accepted in the same transaction. Known membership uniqueness conflicts are translated in Infrastructure to the existing business outcome; every other database error escapes.
- **Likely files/layers:** `AcceptAgencyInvitationHandler.cs`; `CancelAgencyInvitationHandler.cs`; `IAgencyInvitationRepository.cs`; `AgencyInvitationRepository.cs`; `AgenciesEndpointTests.InvitationAccept.cs`; `AgenciesEndpointTests.InvitationCancel.cs`; optionally one focused `AgenciesEndpointTests.InvitationConcurrency.cs` partial instead of spreading concurrency setup.
- **Migration/transaction/constraint requirements:** explicit transaction and parameterized invitation-row `FOR UPDATE`; retain the token, membership, and pending-invitation unique indexes; no migration or concurrency token.
- **Focused tests:** accept-versus-accept, accept-versus-cancel, cancel-versus-expiry observation, and a forced membership conflict. Use separate DbContexts/requests and a deterministic lock/barrier. Assert one terminal state, at most one membership, and rollback of both invitation and membership changes on persistence failure.
- **Full verification:** build the solution; run all invitation and membership tests plus the full suite; run the pending-model check; run diff checks and inspect Git state.
- **Non-goals:** no invitation creation change, no 409 contract, no global exception middleware, no background expiry, and no broad agency lock framework.
- **Completion criteria:** all terminal races have one deterministic winner, `Accepted` always agrees with membership persistence, known conflicts map narrowly, unrelated failures are not swallowed, and all tests pass.
- **Expected commit purpose:** `Serialize agency invitation terminal transitions`.

### 11C — Replace elapsed pending invitations safely

- **Size:** medium.
- **Outcome:** an elapsed stored `Pending` invitation no longer blocks its replacement, while concurrent creates still produce at most one live pending invitation.
- **Finding addressed:** CH11-INV-02 and the invitation-specific part of CH11-API-01.
- **Locked implementation direction:** make create-invitation an action that observes the matching stored `Pending` row. Inside one repository-owned transaction, lock the row when present, re-resolve the invited user and agency membership after any wait, and reject a current member or still-live invitation. For an elapsed row, mark it Expired and save that update first, add/save the replacement second, then commit the outer transaction; a second-phase failure rolls both saves back. If no row exists, rely on the filtered unique index as the concurrent-create arbiter and translate only that named violation after rollback to the existing duplicate-pending result.
- **Likely files/layers:** `CreateAgencyInvitationHandler.cs`; `IAgencyInvitationRepository.cs`; `AgencyInvitationRepository.cs`; `AgenciesEndpointTests.Invitations.cs`; `AgencyInvitationPersistenceTests.cs`; optionally the concurrency partial introduced in 11B.
- **Migration/transaction/constraint requirements:** reuse the invitation transaction/lock mechanism; retain `IX_AgencyInvitations_AgencyId_NormalizedEmail`; no migration.
- **Focused tests:** live duplicate remains rejected; elapsed pending is persisted as Expired and replaced atomically; failure to insert replacement rolls back the expiry update; accept-versus-replacement never creates a new invitation for the just-created member; two concurrent creates yield one pending row and one existing validation-class loser. Separately prove that dashboard counts use `Pending && ExpiresAtUtc > utcNow` while invitation lists continue to expose stored status until an action persists `Expired`.
- **Full verification:** build the solution; run invitation creation/list/dashboard/persistence tests and the full suite; run the pending-model check; run diff checks and inspect Git state.
- **Non-goals:** no eager global expiry, scheduler, volatile index predicate, mapping rewrite, email delivery redesign, or general registration/slug conflict work.
- **Completion criteria:** replacement and race tests pass against PostgreSQL; the action-triggered Chapter 9 lifecycle is unchanged; one live stored Pending row is the maximum; no schema or contract drift occurs.
- **Expected commit purpose:** `Allow atomic replacement of expired agency invitations`.

### 11D — Enforce disabled-user image authorization

- **Size:** easy.
- **Outcome:** a Disabled listing creator cannot upload, delete, set primary, or reorder listing images.
- **Finding addressed:** CH11-IMG-01.
- **Locked implementation direction:** inject the existing `IUserRepository` into all four handlers, load the authenticated actor before mutation, and map both a missing actor row and a Disabled actor to each handler's existing `NotListingOwner`/forbidden result. This avoids new result enums or controller mappings and does not reveal user status. Preserve PendingVerification behavior and all route/listing ownership checks.
- **Likely files/layers:** `UploadListingImageHandler.cs`; `DeleteListingImageHandler.cs`; `SetPrimaryListingImageHandler.cs`; `ReorderListingImagesHandler.cs`; `ListingImagesEndpointTests.Authorization.cs`.
- **Migration/transaction/constraint requirements:** none.
- **Focused tests:** each of the four endpoints rejects a Disabled creator through the existing forbidden response; no image row or file changes; a missing actor row follows that same result; an eligible creator still succeeds; non-owner and missing-listing behavior remains unchanged.
- **Full verification:** build the solution; run the listing-image authorization suite and full test suite; run the pending-model check; run diff checks and inspect Git state.
- **Non-goals:** no global authorization policy, no user-state redesign, no new error enum/controller mapping, and no image concurrency work.
- **Completion criteria:** all four mutation paths enforce the same existing private-user policy before mutation and all regression tests pass.
- **Expected commit purpose:** `Block disabled users from listing image mutations`.

### 11E — Make local storage writes residue-free

- **Size:** easy.
- **Outcome:** a failed or cancelled local copy leaves neither a partial final file nor an abandoned temporary file.
- **Finding addressed:** CH11-IMG-03.
- **Locked implementation direction:** refactor only the shared private write path in `LocalFileStorageService` to copy into a unique same-directory temporary file, complete/dispose the stream, and move it to the generated final name. Delete the temporary path on every failure and cancellation. Apply the same primitive to listing images, avatars, and agency logos without changing `IFileStorageService`.
- **Likely files/layers:** `LocalFileStorageService.cs`; one focused test file such as `LocalFileStorageServiceTests.cs` in the existing test project.
- **Migration/transaction/constraint requirements:** none; no database access.
- **Focused tests:** a stream that throws mid-copy, cancellation mid-copy, successful save, unique destination names, and cleanup assertions for listing/avatar/logo directories. Tests use isolated temporary directories and remove their own fixtures.
- **Full verification:** build the solution; run the focused storage tests and full suite; run the pending-model check; run diff checks and inspect Git state.
- **Non-goals:** no cloud storage, new storage abstraction, background cleanup, database compensation, or post-commit delete redesign.
- **Completion criteria:** injected copy failures and cancellation leave the target directory clean, successful URL/file behavior is unchanged, and all tests pass.
- **Expected commit purpose:** `Prevent partial local media files on failed writes`.

### 11F — Compensate failed listing-image persistence

- **Size:** easy.
- **Outcome:** when listing-image database persistence does not commit, the file created by that upload is removed.
- **Finding addressed:** CH11-IMG-02.
- **Locked implementation direction:** after storage returns the new file descriptor, put entity creation, `AddListingImage`, and the single `SaveChangesAsync` call inside one `try` block. If that path throws before `SaveChangesAsync` returns successfully, call `DeleteListingImageAsync` for exactly the new file with `CancellationToken.None`. If cleanup succeeds, rethrow the original persistence failure; if cleanup also fails, throw one `AggregateException` containing the original persistence failure first and the cleanup failure second. Do nothing to prior files and do not compensate after `SaveChangesAsync` returns successfully. This checkpoint follows the same observable save-versus-throw boundary as the existing avatar/logo compensation patterns, while strengthening cleanup cancellation and double-failure handling.
- **Likely files/layers:** `UploadListingImageHandler.cs`; a focused unit file such as `UploadListingImageHandlerTests.cs`.
- **Migration/transaction/constraint requirements:** no migration and no new database transaction; this checkpoint covers one existing save boundary.
- **Focused tests:** entity preparation or repository save failure before successful return triggers exactly one delete of the new file; a cancelled save uses the non-cancelled cleanup token; successful persistence does not delete; cleanup success rethrows the original persistence failure; cleanup failure produces one `AggregateException` with the persistence failure first and cleanup failure second; no prior file is touched.
- **Full verification:** build the solution; run focused handler and upload integration tests plus the full suite; run the pending-model check; run diff checks and inspect Git state.
- **Non-goals:** no concurrent cap correction yet, no post-commit delete durability, no global compensation service, and no API exception-policy change.
- **Completion criteria:** exact-boundary failure injection proves the observable save-versus-throw behavior, double failure preserves both causes in the locked order, successful persistence is never compensated, no claim is made that indeterminate provider/network commit outcomes are solved, and all tests pass.
- **Expected commit purpose:** `Compensate listing files when image persistence fails`.

### 11G — Serialize upload capacity and append ordering

- **Size:** medium.
- **Outcome:** concurrent uploads cannot exceed 20 images, choose duplicate append order, create two first primaries, or leak the losing request's file.
- **Finding addressed:** CH11-IMG-05.
- **Locked implementation direction:** add a narrow listing-image write scope to `IListingRepository`/`ListingRepository`. It starts a transaction, locks the `Listings` parent row, and reloads its images. Keep file copying outside the lock. Once the file exists, enter the scope, recheck listing eligibility and the 20-image cap, compute `max(SortOrder) + 1`, choose primary only when the reloaded collection is empty, save, and commit. Compensate the new file for a capacity loser or any noncommitted database outcome.
- **Likely files/layers:** `UploadListingImageHandler.cs`; `IListingRepository.cs`; `ListingRepository.cs`; `ListingImagesEndpointTests.Upload.cs`; optionally one `ListingImagesEndpointTests.Concurrency.cs` partial.
- **Migration/transaction/constraint requirements:** explicit transaction and parameterized listing-row `FOR UPDATE`; retain current primary and sort indexes; no migration.
- **Focused tests:** two controlled uploads starting from 19 images produce one success and one cap loser with exactly 20 rows and no orphan; concurrent first uploads yield distinct orders and exactly one primary; concurrent appends yield unique computed orders; failure/cancellation compensation from 11F remains effective.
- **Full verification:** build the solution; run upload, authorization, storage, and new concurrency tests plus the full suite; run the pending-model check; run diff checks and inspect Git state.
- **Non-goals:** no unique sort-order index, no set-primary/delete/reorder rewrite in this checkpoint, no file copy while holding a database lock, and no media contract change.
- **Completion criteria:** deterministic PostgreSQL upload-versus-upload races prove cap, append order, first-primary, and file cleanup invariants; the full suite passes without model changes. Cross-operation serialization remains intentionally incomplete until 11H and therefore 11H must follow before the chapter is releasable or complete.
- **Expected commit purpose:** `Serialize listing image upload capacity and ordering`.

### 11H — Make all image aggregate mutations atomic and serialized

- **Size:** medium.
- **Outcome:** primary changes are all-or-nothing, and upload, delete, set-primary, and reorder share one listing-level serialization protocol.
- **Finding addressed:** CH11-IMG-04; completes aggregate participation needed by CH11-IMG-05.
- **Locked implementation direction:** reuse 11G's listing-image write scope in set-primary, delete, and reorder. Keep both set-primary SQL phases and both primary-delete phases inside one transaction so any second-save failure rolls back the first. Commit database deletion/promotion before attempting physical deletion. Reorder locks and reloads the image set before validating and assigning order.
- **Likely files/layers:** `SetPrimaryListingImageHandler.cs`; `DeleteListingImageHandler.cs`; `ReorderListingImagesHandler.cs`; `IListingRepository.cs` and `ListingRepository.cs` only if the 11G scope needs a narrow extension; existing set-primary/delete/reorder tests; optionally the concurrency partial from 11G.
- **Migration/transaction/constraint requirements:** reuse explicit transaction and listing-row `FOR UPDATE`; retain filtered unique index `IX_ListingImages_ListingId`; no migration.
- **Focused tests:** injected failure on the second save rolls back set-primary and primary-delete; concurrent set-versus-set and set-versus-delete leave exactly one valid primary when images remain; delete-versus-upload and reorder-versus-upload preserve cap, membership, and deterministic order; direct PostgreSQL proof confirms the unique-primary index still rejects multiple primaries.
- **Full verification:** build the solution; run every listing-image suite, storage/failure tests, and full test suite; run the pending-model check; run diff checks and inspect Git state.
- **Non-goals:** no durable post-commit file-delete workflow, no sort-order constraint, no image API redesign, and no optimization of unrelated listing writes.
- **Completion criteria:** every participating image mutation acquires the same parent lock, multi-save primary changes roll back as a unit, database uniqueness remains intact, and all focused/full tests pass.
- **Expected commit purpose:** `Make listing image aggregate mutations atomic`.

### 11I — Full verification and documentation closeout

- **Size:** medium.
- **Outcome:** independently verify the complete Chapter 11 scope, reconcile permanent documentation, and leave no tracked, staged, generated, or unexpected untracked change before Chapter 12.
- **Findings addressed:** closes the eight implemented findings and records the dispositions of all deferred, rejected, accepted, and owner-decision items.
- **Locked implementation direction:** run the complete verification sequence against PostgreSQL, then update only this Chapter 11 document, `docs/backend-context.md`, and `docs/backend-quality-handoff.md` with actual results. Do not mark a finding resolved before its focused evidence and full regression pass.
- **Likely files/layers:** documentation only: `docs/chapters/chapter-11-data-integrity-targeted-hardening.md`, `docs/backend-context.md`, and `docs/backend-quality-handoff.md`. No production or test change belongs in closeout.
- **Migration/transaction/constraint requirements:** migrate a fresh PostgreSQL database from zero through all 15 committed migrations, run the update command again and confirm no pending work, run pending-model verification, and inspect the relevant catalog entries for agency membership uniqueness, invitation token/pending uniqueness, listing-image primary uniqueness, foreign keys, filters, validity, and readiness. Because the locked sequence adds no migration, an unexpected migration-count or model change is a failure.
- **Focused tests:** rerun all agency member role/disable, invitation create/accept/cancel/list/dashboard/persistence, listing-image authorization/upload/delete/set-primary/reorder, storage failure, deterministic concurrency, and compensation tests. Record exact totals.
- **Full verification:** build the query-review tool to ensure Chapter 10 tooling remains intact; run `dotnet build`; run the full test project with no build; perform the fresh zero-to-latest migration, repeat update, pending-model check, catalog inspection, `git diff --check`, `git diff --cached --check`, and final `git status --short`. Stop if any check fails.
- **Non-goals:** no implementation fixes during closeout, no Chapter 10 benchmark rerun, no removal of unresolved owner decisions, and no Chapter 12 planning or work.
- **Completion criteria:** focused and full tests pass; fresh migrations, repeat update, pending model, and catalog checks pass; all three documents agree on completed and deferred scope; the final checkpoint is committed as documentation closeout; no tracked or staged changes or generated artifacts remain afterward.
- **Expected commit purpose:** `Close Chapter 11 data integrity hardening`.

Dependency order:

```text
11A
 |
 +----------------------------- independent owner invariant

11B -> 11C                     invitation lock before expiry replacement

11D                             image authorization

11E -> 11F -> 11G -> 11H       safe write, compensation, upload lock, full image lock

11A through 11H -> 11I         final verification and documentation
```

## 11. Verification strategy

Every implementation checkpoint uses four evidence layers:

1. Unit or focused handler tests for the exact branch or injected failure.
2. PostgreSQL-backed endpoint/persistence tests for database constraints, transactions, and concurrent outcomes.
3. `dotnet build` and the full `RealEstate.Tests` suite.
4. Pending-model, diff, and Git-state checks.

The baseline of 631 tests is historical. New tests will increase the count; documentation must record the actual final passing total rather than retaining 631 by assumption.

Recommended full commands for each implementation checkpoint are:

```powershell
dotnet build
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj --no-build
dotnet ef migrations has-pending-model-changes --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api --no-build
git diff --check
git diff --cached --check
git status --short
```

Focused test filters may be used first, but never replace the full suite. Tests must use Testcontainers PostgreSQL or the repository's existing isolated PostgreSQL fixture, never a developer database.

Chapter 10 permanent evidence files must remain byte-identical. Query-review capture or benchmark reruns are not Chapter 11 verification steps.

## 12. Migration/database safety

- The planned final migration count remains 15.
- Do not edit an old applied migration or the model snapshot.
- Do not generate an empty or convenience migration for lock SQL.
- Lock SQL must be fixed-shape and parameterized. Identifiers are constant Infrastructure implementation details; request data is never interpolated as SQL text.
- Keep transactions short and cancellation-aware. Do not perform file copies, email delivery, or other external I/O while a row lock is held.
- Inspect `PostgresException.ConstraintName` only in Infrastructure and only for the named existing indexes required by 11B/11C. SQLSTATE alone is too broad.
- A transaction that returns an expected business failure must still end predictably; no tracked mutation may leak into a later request.
- Final zero-to-latest verification must use a fresh PostgreSQL database, followed by a second update that reports no pending migration and a clean pending-model check.
- Final catalog inspection must verify the unique and filtered indexes remain valid and ready and that relevant foreign keys/delete behaviors match the EF model.
- If an implementation checkpoint discovers that a new constraint or concurrency token is essential, stop. Record the data impact and amend the plan with a separate migration task after owner approval.

## 13. Concurrency/transaction testing

Concurrency tests must be deterministic and provider-realistic:

- Use separate service scopes and therefore separate `RealEstateDbContext` instances, or separate HTTP requests known to resolve separate scopes.
- Establish the contested precondition before releasing both operations. Use a test-owned database row lock, transaction gate, or narrowly local barrier so both requests cannot accidentally execute sequentially.
- Assert the final database state, not only response codes.
- Bound waits with cancellation/timeouts so a deadlock fails clearly rather than hanging the suite.
- Verify the loser follows the existing result contract and that unrelated database exceptions still escape.
- For owner tests, assert at least one Active Owner remains and that the actor was revalidated under the agency lock.
- For invitation tests, assert one terminal status, membership consistency, and rollback after an injected unique conflict.
- For image tests, assert row count, distinct append order, one primary when images remain, and physical-file cleanup.
- Run each new race test repeatedly only when useful for local diagnosis; its correctness must come from a controlled interleaving, not probabilistic repetition.

Production-only test hooks, sleeps in handlers, a global concurrency harness, and an in-memory EF provider are prohibited.

## 14. Failure-path/storage testing

- Inject failure at the exact boundary being claimed: mid-stream copy, persistence save, second save inside a transaction, or compensation deletion.
- An invalid request that never reaches the boundary is not failure-path proof.
- Storage tests use unique temporary roots outside repository content and clean them in `finally`/fixture teardown.
- Mid-copy and cancelled-copy tests assert both the generated final path and temporary-pattern files are absent.
- Upload compensation tests capture the exact returned stored filename and prove only that file is deleted.
- Persistence-failure tests assert the database contains no new image row.
- Cleanup-failure tests assert the aggregate preserves the original database exception first and the cleanup exception second.
- Transaction rollback tests requery with a new DbContext so the assertion is against committed PostgreSQL state rather than a tracked entity graph.
- Primary-delete tests distinguish a listing with remaining images, which requires exactly one promoted primary, from deletion of the final image, where zero images and zero primary is valid.
- Post-commit physical deletion failure is recorded as CH11-FILE-01 and is not reclassified as solved by upload compensation.

## 15. Documentation and quality-handoff rules

Only 11I performs permanent documentation reconciliation.

- Update this document with actual checkpoint completion, final migration count, focused/full test totals, and verification result.
- Update `docs/backend-context.md` to mark Chapter 11 complete and Chapter 12 current/next only after every gate passes.
- Update `docs/backend-quality-handoff.md` finding by finding:
  - close last-active-owner concurrency only after 11A evidence;
  - close invitation terminal and elapsed-pending replacement blockage/concurrent-create issues only after 11B/11C evidence, while retaining action-triggered stored-status lag as an accepted lifecycle tradeoff;
  - close disabled-user image authorization only after 11D evidence;
  - close partial-write and upload precommit-orphan issues only after 11E/11F evidence;
  - close image cap/order/primary transaction issues only after 11G/11H evidence;
  - retain CH11-FILE-01, CH11-DB-01, CH11-DB-02, and CH11-STATE-01 with their exact unresolved dispositions;
  - retain Chapter 12 API error/conflict/pagination issues;
  - do not describe accepted test SQL or nullable lifecycle relationships as defects.
- Do not claim durable file deletion, database-wide checks, required listing creators, global optimistic concurrency, or standardized conflict responses were implemented.
- Preserve Chapter 10 completion and benchmark evidence without regenerating it.
- Documentation changes must describe measured/verified outcomes, not anticipated totals.

## 16. Risks and safeguards

| Risk | Safeguard |
|---|---|
| A method named `ForUpdateAsync` is mistaken for a database lock. | Require explicit transaction plus inspected PostgreSQL `FOR UPDATE` SQL in Infrastructure and a blocking concurrency test. |
| Different commands acquire locks in different order and deadlock. | Lock the common parent/target first and document one order per invariant; use bounded concurrency tests. |
| A database lock is held during file upload. | Copy first, then acquire the listing lock; compensate the file on any losing/noncommitted path. |
| A known uniqueness race leaks as a 500. | Translate only the exact named constraint in Infrastructure to the existing provider-neutral business result. |
| Broad exception handling hides an unrelated database defect. | Match constraint name and unique-violation state; rethrow everything else. |
| Compensation cleanup also fails. | When cleanup succeeds, rethrow the original persistence failure. When cleanup also fails, throw one `AggregateException` containing the original persistence failure first and the cleanup failure second. |
| A provider/network failure makes the true database commit outcome indeterminate. | 11F relies on the ordinary EF Core single-`SaveChangesAsync` transaction boundary and adds no generic outcome classifier; retain genuinely ambiguous cross-resource durability under CH11-FILE-01. |
| Two-save primary logic partially commits. | Enclose both saves in one explicit transaction and prove rollback using a new DbContext. |
| A unique sort-order index is added as an incomplete fix. | Retain parent-row serialization and the current nonunique ordering index. |
| Invitation expiry semantics drift. | Preserve action-triggered expiry and current list/dashboard definition; test both explicitly. |
| Chapter 11 grows into global API or concurrency redesign. | Enforce section 8 and require a plan amendment for any owner-decision item. |
| Tests pass without exercising the race. | Use controlled separate-context interleavings and final-state assertions. |
| A hidden model change appears. | Run pending-model checks at every checkpoint and zero-to-latest migration verification in 11I. |

## 17. Likely files by checkpoint

| Checkpoint | Likely production files | Likely test/documentation files |
|---|---|---|
| 11A | `ChangeAgencyMemberRoleHandler.cs`; `DisableAgencyMemberHandler.cs`; `IAgencyRepository.cs`; `AgencyRepository.cs` | member role/disable partials; optional `AgenciesEndpointTests.MemberConcurrency.cs` |
| 11B | `AcceptAgencyInvitationHandler.cs`; `CancelAgencyInvitationHandler.cs`; `IAgencyInvitationRepository.cs`; `AgencyInvitationRepository.cs` | invitation accept/cancel partials; optional `AgenciesEndpointTests.InvitationConcurrency.cs` |
| 11C | `CreateAgencyInvitationHandler.cs`; `IAgencyInvitationRepository.cs`; `AgencyInvitationRepository.cs` | invitation create/list/dashboard tests; `AgencyInvitationPersistenceTests.cs`; optional shared invitation concurrency partial |
| 11D | four listing-image mutation handlers; existing `IUserRepository` only as a dependency | `ListingImagesEndpointTests.Authorization.cs` |
| 11E | `LocalFileStorageService.cs` | new focused `LocalFileStorageServiceTests.cs` |
| 11F | `UploadListingImageHandler.cs` | new focused `UploadListingImageHandlerTests.cs` |
| 11G | `UploadListingImageHandler.cs`; `IListingRepository.cs`; `ListingRepository.cs` | `ListingImagesEndpointTests.Upload.cs`; optional `ListingImagesEndpointTests.Concurrency.cs` |
| 11H | set-primary, delete, and reorder handlers; listing repository/interface only if the 11G scope needs extension | set-primary/delete/reorder partials; optional shared concurrency partial |
| 11I | none | this chapter document; `docs/backend-context.md`; `docs/backend-quality-handoff.md` |

Exact file names for new test partials are recommendations, not permission to duplicate fixtures. Reuse an existing focused file when that keeps the checkpoint clearer and within the file budget.

## 18. Completion rule

Chapter 11 is complete only when all of the following are true:

1. 11A through 11H are implemented as separate focused commits in dependency order.
2. Every confirmed in-scope finding has deterministic focused proof and the full suite passes.
3. Owner demotion/disable cannot produce zero Active Owners.
4. Invitation terminal transitions have one winner, acceptance agrees with membership persistence, and elapsed pending invitations can be replaced without allowing duplicates.
5. Disabled users cannot mutate listing images.
6. Failed/cancelled local copies leave no partial artifact, and a noncommitted listing-image upload compensates its new file.
7. All listing-image database mutations share the listing lock; cap, order, and primary invariants survive concurrent requests and injected save failures.
8. No public contract, Chapter 10 behavior/evidence, or unrelated aggregate behavior has drifted.
9. The full build and actual full test total pass.
10. A fresh PostgreSQL database migrates from zero through all 15 migrations; the repeat update is empty; pending-model verification and relevant catalog checks pass.
11. Permanent documentation accurately closes only proven findings and retains all owner decisions and Chapter 12 issues.
12. `git diff --check` and `git diff --cached --check` pass, no generated artifacts remain, the closeout commit is complete, and Git has no tracked, staged, or unexpected untracked changes. An expressly retained temporary planning artifact may remain intentionally untracked only when its status is recorded accurately.

The unresolved CH11-DB-01, CH11-DB-02, CH11-STATE-01, and CH11-FILE-01 decisions do not become implicit implementation requirements. Chapter 11 may close with them explicitly retained only because this plan defines a complete, independently correct bounded scope. Any later owner approval must become new separately planned work, not a retroactive expansion of a completed checkpoint.
