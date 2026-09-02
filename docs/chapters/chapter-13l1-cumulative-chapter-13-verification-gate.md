# Chapter 13L.1 — Cumulative Chapter 13 Verification Gate

Date: 2026-09-01

## Verdict and scope

`L1_VERIFICATION_READY_FOR_REVIEW`

Chapter 13L.1 adds no behavior. This evidence proves the completed Chapter 13 implementation works cumulatively across Domain, Application, provider adapters, PostgreSQL, API, concurrency, OpenAPI, migrations, QueryReview SQL/results/plans, and the complete backend suite. It does not declare Chapter 13 complete; durable status/context/handoff changes remain owned by 13L.2 after repository-owner review.

No production code, test code, migration, designer, snapshot, QueryReview expectation, SQL baseline, provider behavior, frontend, or pre-existing documentation was edited. The only artifact intended for commit is this verification record.

## Initial source and worktree freeze

- Branch: `feature/chapter-13l1-cumulative-chapter-13-verification-gate`.
- Starting `git status --short`: empty.
- Starting `git diff --name-status`: empty.
- Starting `git diff --stat`: empty.
- Starting commit: `866b0dd` (`Merge pull request #183 ... chapter-13k3-final-generated-sql-freeze-proof`).
- K.3 canonical evidence was already merged.

## Release build

```text
dotnet build -c Release --no-restore
```

Result: **0 warnings, 0 errors**, elapsed `00:01:08.55`.

The dedicated QueryReview Release build also passed with 0 warnings and 0 errors in `00:00:02.68`.

## Cumulative focused verification

All commands used the final Release binaries, `--no-build --no-restore`, and minimal console logging.

### Translation, authoring, location, readiness, mapping

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~ListingTranslationRulesTests|FullyQualifiedName~CreateListingTranslationValidationTests|FullyQualifiedName~CreateListingValidatorTests|FullyQualifiedName~UpdateListingValidatorTests|FullyQualifiedName~ListingDraftReplacementEngineTests|FullyQualifiedName~ListingAuthoringRepositoryTests|FullyQualifiedName~ListingLocationTests|FullyQualifiedName~ListingPublicationReadinessTests|FullyQualifiedName~EffectiveTranslationOrderingTests|FullyQualifiedName~ListingMappingExtensionsTests|FullyQualifiedName~ListingPersistenceTests" --logger "console;verbosity=minimal"
```

Result: **417 passed, 0 failed, 0 skipped**, total 417, duration 19 s.

### Controlled provider, token, resilience, authorization, rate, HTTP

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~CanonicalListingLocationTests|FullyQualifiedName~ListingLocationFingerprintTests|FullyQualifiedName~GeocodingContractsTests|FullyQualifiedName~LocationConfirmationTokenClaimsTests|FullyQualifiedName~DataProtectionLocationConfirmationTokenProtectorTests|FullyQualifiedName~LocationConfirmationDataProtectionRegistrationTests|FullyQualifiedName~Geoapify|FullyQualifiedName~SearchLocationCandidatesHandlerTests|FullyQualifiedName~ConfirmListingLocationHandlerTests|FullyQualifiedName~ClearListingLocationHandlerTests|FullyQualifiedName~GeocodingRateLimitFoundationTests|FullyQualifiedName~GeocodingRateLimitingRegistrationTests|FullyQualifiedName~ListingGeocodingEndpointTests" --logger "console;verbosity=minimal"
```

Result: **282 passed, 0 failed, 0 skipped**, total 282, duration 19 s.

### PostgreSQL migration, integrity, and concurrency

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~PostgreSqlListingTranslationRowIntegrityTests|FullyQualifiedName~PostgreSqlOptionalLocalizedLocationMigrationTests|FullyQualifiedName~PostgreSqlListingLocationIntegrityTests|FullyQualifiedName~PostgreSqlCanonicalLocationMigrationTests|FullyQualifiedName~PostgreSqlActiveListingPublicationIntegrityTests|FullyQualifiedName~PostgreSqlActiveListingPublicationFailureBoundaryTests|FullyQualifiedName~PostgreSqlActiveListingPublicationConcurrencyTests|FullyQualifiedName~PostgreSqlStrongActiveLocationIntegrityMigrationTests|FullyQualifiedName~PostgreSqlStrongActiveDirectIntegrityMatrixTests|FullyQualifiedName~PostgreSqlStrongActiveConcurrencyClosureTests|FullyQualifiedName~ListingUpdateConcurrencyTests|FullyQualifiedName~ListingGeocodingConcurrencyTests" --logger "console;verbosity=minimal"
```

Result: **251 passed, 0 failed, 0 skipped**, total 251, duration 17 s.

### Strict public surfaces and unexpected-integrity boundary

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~GetListings_ReturnsPagedListings|FullyQualifiedName~GetListingById_WithExistingListing_ReturnsListingInRequestedLanguage|FullyQualifiedName~GetAgencyListings_ReturnsOnlyListingsForAgency|FullyQualifiedName~GetComparables_PersonalAndAgencyCandidatesCompeteTogetherAndPreserveResponseShape|FullyQualifiedName~PublishListing_ShouldPublishPersonalDraftListing_WhenUserIsOwnerAndActive|FullyQualifiedName~PublishListing_ShouldPublishAgencyDraftListing_WhenUserIsActiveOwner|FullyQualifiedName~PublicListingIntegrityFailure_ReturnsSanitized500AndLogsDiagnosticsOnce" --logger "console;verbosity=minimal"
```

Result: **7 passed, 0 failed, 0 skipped**, total 7, duration 4 s.

### Nullable private lifecycle and Draft-capable management

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~CreateListing_WithValidRequest_ReturnsCreated|FullyQualifiedName~GetMyListings_ReturnsTruthfulPrivateLocationState|FullyQualifiedName~GetAgencyDashboardListings_DeterministicFallback_PreservesPrivateResponseBehavior|FullyQualifiedName~LifecycleWriter_ReturnsFullyLoadedPersistedAggregate|FullyQualifiedName~GetListingManagement_ReturnsTruthfulAuthoringLocationState|FullyQualifiedName~UpdateListing_ManagementGetPutGet_RoundTripsCompleteReplacement" --logger "console;verbosity=minimal"
```

Result: **12 passed, 0 failed, 0 skipped**, total 12, duration 5 s.

### Serialized OpenAPI

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~OpenApiDocumentTests" --logger "console;verbosity=minimal"
```

Result: **11 passed, 0 failed, 0 skipped**, total 11, duration 2 s.

The six commands produced **980 successful executions, 0 failed, 0 skipped**. They cover **970 distinct executed test cases** because the Foundation and Provider selections overlap on 10 `CanonicalListingLocationTests` cases.

### Focused-selection overlap audit

The six exact filters above were independently rerun with TRX output under `%LOCALAPPDATA%/Temp/realestate-l1-overlap-audit-da02ecbee40546f6b59e12f46cdc5ca1/`. Their execution counts were 417, 282, 251, 7, 12, and 11, totaling 980. A cross-command multiset union preserves separately executed theory rows within each command and subtracts only cases selected by more than one command; its result is 970 distinct cases.

The only cross-command overlap is between Foundation's substring selector `FullyQualifiedName~ListingLocationTests` and Provider's explicit `FullyQualifiedName~CanonicalListingLocationTests`. It comprises these 10 exact discovered cases (six method FQNs, with five data rows for `HasSameIdentityAs_DetectsEveryH4IdentityChange`):

```text
RealEstate.Tests.Unit.Application.Listings.CanonicalListingLocationTests.From_RejectsDuplicateCanonicalLanguageLikeExistingH4Comparison
RealEstate.Tests.Unit.Application.Listings.CanonicalListingLocationTests.From_UsesExistingH4CanonicalNormalizationAndFieldSet
RealEstate.Tests.Unit.Application.Listings.CanonicalListingLocationTests.HasSameIdentityAs_DetectsEveryH4IdentityChange(component: "addressLine")
RealEstate.Tests.Unit.Application.Listings.CanonicalListingLocationTests.HasSameIdentityAs_DetectsEveryH4IdentityChange(component: "city")
RealEstate.Tests.Unit.Application.Listings.CanonicalListingLocationTests.HasSameIdentityAs_DetectsEveryH4IdentityChange(component: "language")
RealEstate.Tests.Unit.Application.Listings.CanonicalListingLocationTests.HasSameIdentityAs_DetectsEveryH4IdentityChange(component: "municipality")
RealEstate.Tests.Unit.Application.Listings.CanonicalListingLocationTests.HasSameIdentityAs_DetectsEveryH4IdentityChange(component: "neighborhood")
RealEstate.Tests.Unit.Application.Listings.CanonicalListingLocationTests.HasSameIdentityAs_DetectsLanguageAdditionAndRemoval
RealEstate.Tests.Unit.Application.Listings.CanonicalListingLocationTests.HasSameIdentityAs_IsIndependentOfTranslationInputOrder
RealEstate.Tests.Unit.Application.Listings.CanonicalListingLocationTests.HasSameIdentityAs_TreatsCanonicalLanguageAndTextAsEquivalent
```

No filter was changed to manufacture the distinct count.

## Controlled-provider Draft-to-public smoke

The full existing test-suite search found no test that carries the same persisted personal or agency listing through candidate-token acquisition, confirmation, publication, and public visibility. Endpoint tests stop after confirmation, the same-listing confirm/publish concurrency test lacks candidate search and public visibility, and publish endpoint tests use the strong-location fixture. L.1 therefore used a temporary external verifier rather than editing or adding a repository test.

Temporary verifier: `%LOCALAPPDATA%/Temp/realestate-l1-connected-smoke-20260901/`. It references the current `RealEstate.Tests` project (and transitively the current API/Application/Domain/Infrastructure assemblies), uses the repository `CustomWebApplicationFactory`, authentication helpers, WebApplicationFactory host, and PostgreSQL Testcontainer, and replaces only `IListingGeocoder` with a deterministic controlled provider. Aggregate SHA-256 over the four source/marker files in sorted filename order, with each filename plus LF preceding its bytes: `56D1F32DB5671B6E986EB77B84829C685648C78CC55516DDE3477B9469EEE92B`.

Successful command:

```text
dotnet run --project "C:\Users\User\AppData\Local\Temp\realestate-l1-connected-smoke-20260901\RealEstate.L1ConnectedSmoke.csproj" -c Release --no-restore
```

Result: exit 0 and `L1_CONNECTED_SMOKE_PASS`.

### Personal connected listing

- Active authenticated user: `c6a01a49-fe9d-4af2-a889-695d11e748ba`.
- Listing: `67f06cec-6962-411c-9242-bdeaf7c6397a` at create, management GET, full-replacement PUT, candidate search, token confirmation, confirmed management/EF read, publish, public detail, and public-list read.
- The API-created Draft received complete translated authoring content through PUT. Candidate output contained `label`, `previewLatitude`, `previewLongitude`, `precision = ExactAddress`, and a nonblank opaque token that contained neither provider key nor provider reference.
- The token returned for this listing was the only confirmation input. Backend re-resolution persisted latitude `41.9981`, longitude `21.4254`, precision `ExactAddress`, provider provenance, and confirmation timestamp while the listing remained Draft.
- Publishing this same listing returned the six coherent translation fields plus the three root location fields required by strict `PublicListingResponse`; public detail and public list both returned this same ID and strict truth.
- Provider calls for the chain: search 1, resolve 1. Counts did not change during publish or public reads.

Personal smoke: **PASS**.

### Agency connected listing

- Active authenticated user: `a0bd0ccc-8d4a-4a5a-bd43-ebfa67c3b412`.
- Active agency: `01e17831-4e7f-4756-9d84-8e20e949bbd2`.
- Actor premise: Active Owner membership; agency and user both Active.
- Listing: `abd719ec-ece3-441d-aed6-d43d5e9ce0a5` at API create, management GET, full-replacement PUT, candidate search, token confirmation, confirmed management/EF read, publish, and agency-public-list read.
- The same controlled candidate/token/re-resolution path persisted the confirmed root snapshot. Publishing this same listing returned all nine strict fields, and the public agency collection contained this exact listing ID.
- Provider calls for the chain: search 1, resolve 1. Counts did not change during publish or the agency public read.

Agency smoke: **PASS**.

Across both chains the aggregate controlled-provider counts were search 2 and resolve 2. No live provider, caller-authored coordinate/provenance input, `PrepareStrongLocationPublishableDraftAsync`, trusted-location attachment, direct location assignment, raw SQL backfill, or separately prepared publish listing was used. EF access in the verifier was read-only for location proof; the only fixture state mutation beyond supported APIs was normal agency approval/user-status setup. Existing focused tests separately retain transaction-ordering and Agent authorization regression coverage; the connected agency smoke itself makes no Agent-publication claim.

## Provider negative gates

The 282-test provider group plus concurrency group verifies:

- success, empty result, provider rate exhaustion, unavailable/permanent/malformed outcomes;
- total timeout, exact bounded retry, recovery/exhaustion, and caller cancellation;
- tampered, malformed, expired, wrong-listing/actor, and stale-fingerprint tokens;
- provider re-resolution completes before write-scope acquisition;
- anonymous 401 precedence, unauthorized 403, missing 404, non-Draft 409;
- shared authenticated candidate/confirmation rate policy and clear outside that policy;
- clear idempotency and complete snapshot removal;
- one sanitized terminal dependency owner and no address, token, credential, provider reference, or raw response leakage.

## Migration chain, repeat, Down/re-Up, and catalog

The five Chapter 13 migrations are:

1. `20260809124123_EnforceListingTranslationRowIntegrity`;
2. `20260811091318_EnforceActiveListingPublicationIntegrity`;
3. `20260812172728_EnforceOptionalLocalizedLocationRowIntegrity`;
4. `20260813100457_AddCanonicalGeocodedLocationSnapshot`;
5. `20260824141614_EnforceStrongActiveLocationIntegrity`.

A fresh, structurally verified, auto-removed `postgres:16-alpine` container and empty `realestate_queryreview_13l1` database were created through the QueryReview `profile create` workflow. It applied the complete committed chain successfully. `__EFMigrationsHistory` contained **20** entries with tail `20260824141614_EnforceStrongActiveLocationIntegrity`.

The established lifecycle subset ran:

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~MigrationLifecycle_FreshRepeatDownAndUpAgain_IsExact|FullyQualifiedName~MigrationLifecycle_PreservesUnresolvedAndLegacyRowsAndRestoresH2OnDown|FullyQualifiedName~MigrationLifecycle_UpDownAndUpAgain_PreservesEarlierObjects|FullyQualifiedName~MigrationLifecycle_FreshRepeatDownExact13FAndReUpHasTwentyEntries" --logger "console;verbosity=minimal"
```

Result: **4 passed, 0 failed, 0 skipped**, total 4, duration 2 s. This proves the repository-defined repeat semantics, H.2/H.3/F/J.4 relevant Down states, exact restoration of earlier objects, re-Up, no fabricated location/provenance, and final 20-entry history.

The catalog subset ran:

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Catalog_ContainsNamedUnicodeConstraintsOnNullableColumns|FullyQualifiedName~Catalog_ContainsExpectedNullableColumnsAndNamedConstraints|FullyQualifiedName~Catalog_ContainsSchemaSafeStatementTriggersWithTransitionTables|FullyQualifiedName~Catalog_Retains13FObjectsAndAddsExactOldNewRootGuard|FullyQualifiedName~RowIntegrityConstraints_CoexistWithUniqueAndTrigramIndexes" --logger "console;verbosity=minimal"
```

Result: **5 passed, 0 failed, 0 skipped**, total 5, duration 285 ms.

Read-only final-database catalog inspection independently found:

- seven canonical `ListingTranslations` checks, its Listing FK and PK;
- eight Listing-root location checks, Listing ownership FKs and PK;
- `TR_ListingTranslations_ActiveFreeze_Insert/Update/Delete` enabled;
- `TR_Listings_ActivePublicationIntegrity_Insert/Update` enabled;
- unique `(ListingId, LanguageCode)` index;
- `IX_ListingTranslations_Q_Trigram` GIN index over Title/City/Municipality/Neighborhood;
- expected Listing ownership indexes and exact 20-row migration history.

## EF model and migration freeze

```text
dotnet ef migrations has-pending-model-changes --project src/RealEstate.Infrastructure/RealEstate.Infrastructure.csproj --startup-project src/RealEstate.Api/RealEstate.Api.csproj --configuration Release --no-build
```

Result: `No changes have been made to the model since the last migration.`

- Migration count: 20.
- J.4 migration SHA-256: `9E4A5571936DE26C454907C43B0EA466A68AA308B3740E316050F85566D9950B`.
- J.4 designer SHA-256: `31792873C417FB00C5A58F0009D4210B8BDAA6385D3C9AF0817646C9888657F9`.
- Model snapshot SHA-256: `5C7CC5FCE7DD399033D124F1DF3C66E7F8BC680CC1F00A7D7177F64B933605D5`.
- Migration/designer/snapshot diff: zero.

## Complete backend suite

```text
dotnet test -c Release --no-build --logger "console;verbosity=minimal"
```

Result: **2022 passed, 0 failed, 0 skipped**, total 2022, duration 2m47s. This equals the latest final K.2 repository count; discovery did not change.

## Final OpenAPI disposition

The 11/11 serialized-document tests prove:

- all nine public members present, required, non-null, and correctly typed;
- all five public endpoint families resolve to `PublicListingResponse` through their wrappers;
- nullable private `ListingResponse` and Draft-capable `ListingAuthoringResponse` remain separate;
- management translations are required/non-null while Draft text/location members remain nullable;
- Create requiredness/default matches runtime and PUT's six required members are non-null;
- Create/PUT remain coordinate/provenance-free;
- candidate/confirm/clear contracts remain provider-neutral;
- seven-member pagination, canonical errors, security, request correlation, media, and enums remain intact;
- provider/internal/integrity implementation schemas remain excluded.

## Final QueryReview SQL/profile/result proof

Current profile: `chapter-10f-v2`. Locked SQL run ID: `chapter-10f-v1-production-sql`.

`profile create` and independent read-only `profile verify` both passed:

- established profile: **61/61**;
- J.7 strong-Active gates: **8/8**;
- J.7 coordinate/root ownership gates: **7/7**;
- locked discovery/result identities: **8/8**;
- Listings/translations/Active: 100,000 / 200,000 / 70,000;
- coordinate pairs/null/partial: 80,000 / 20,000 / 0;
- malformed Active: 0.

Production capture emitted 33 commands and 80 typed parameters for N1/P1/P2/A1/R1/L1/Q1/C1. The established curated representation was compared to accepted H.6 run `chapter-10f-v1-baseline-20260814T112202Z-2925368b` using only CRLF/lone-CR to LF normalization and ordinal equality.

Result: **33/33 exact; 0 mismatches; 0 missing; 0 extra**.

Locked totals and ordering passed:

- N1 70,000; P1/P2 23,334; A1 350; R1 1,050; L1 140; Q1 120;
- C1 30 eligible and exact six-result ordinal order 3003, 3002, 3005, 3004, 3006, 3007;
- page size 20, comparable limit 6, deterministic counts/pages/split hydration.

## Plans, buffers, spills, index, and performance

Transient run: `chapter-10f-v2-baseline-20260901T185116Z-866b0dd1`.

- 33 commands, 198/198 plans: 33 warm-up and 165 measured;
- structural plan/row validation: PASS;
- anomalies: 0;
- spills: 0;
- every command/sequence median temp block count: 0;
- Q1 GIN trigram index: valid, ready, live, and used;
- Q1 count median: 9.579 ms;
- Q1 aligned first page: 10.668 ms;
- Q1 250 ms/no-spill gate: PASS;
- page-root observed width/rows: 853/20 for N1/P1/P2/A1/R1/L1/Q1;
- comparable ranked-root observed width/rows: 953/6.

Aligned execution medians:

| Sequence | Execution ms | Shared blocks | Temp blocks | Spill |
|---|---:|---:|---:|---|
| N1 first page | 53.898 | 3,830 | 0 | no |
| P1 first page | 42.735 | 3,832 | 0 | no |
| P2 first page | 45.166 | 3,852 | 0 | no |
| A1 first page | 2.439 | 884 | 0 | no |
| A1 endpoint supplementary | 3.476 | 1,388 | 0 | no |
| R1 first page | 36.113 | 3,838 | 0 | no |
| L1 first page | 12.595 | 1,813 | 0 | no |
| Q1 first page | 10.668 | 1,557 | 0 | no |
| C1 candidate page | 54.682 | 21,318 | 0 | no |
| C1 endpoint supplementary | 54.927 | 21,327 | 0 | no |

No baseline export or rebaseline occurred.

## Chapter 13 contract checklist

Source, focused tests, full-suite tests, OpenAPI, PostgreSQL, and QueryReview evidence jointly prove:

- supported create/update reject malformed translated truth and caller-authored coordinates;
- management GET/PUT retrieve and replace complete all-translation Draft state while Draft may remain incomplete;
- private/management responses expose truthful nullable resolution state;
- authorized Draft candidate search returns provider-neutral preview data and opaque token;
- confirmation is backend-mediated and provider resolution finishes before the parent-locked transaction;
- connected personal and Active-Owner agency smokes carry one persisted listing per flow from API Draft creation through authoring, controlled-provider candidate/token confirmation, persisted confirmed state, publication, and the corresponding public read;
- stale tokens cannot overwrite newer Draft location text;
- Active requires every required translated field and a complete valid confirmed root snapshot;
- Neighborhood remains optional and precision remains truthful/provider-neutral;
- authorization precedes readiness/private-state disclosure;
- update/location/status and supported child races serialize on the Listing parent;
- PostgreSQL activation, Active translation/location immutability, parent MVCC touch, and stale `REPEATABLE READ` protection hold;
- the five public families use strict `PublicListingResponse` with all nine fields;
- private lifecycle responses remain nullable `ListingResponse` and management remains Draft-capable;
- impossible materialized Active corruption throws `PublicListingIntegrityException` internally and returns sanitized `server.unexpected` externally;
- OpenAPI matches runtime;
- q remains Title/City/Municipality/Neighborhood only;
- AddressLine, coordinates, and precision are not discovery/comparable inputs;
- requested/`mk`/bytewise/UUID fallback, paging, ranking, split hydration, and agency reuse remain unchanged;
- final SQL remains 33/33 exact and profile remains 61/61;
- Chapter 11 image guarantees and Chapter 12 failure/pagination/observability guarantees remain green in the complete suite;
- all five Chapter 13 and all 20 repository migrations verify cleanly;
- frontend is untouched;
- PostGIS/spatial/geography/privacy/taxonomy and deferred authentication/security configuration work remain deferred.

Checklist disposition: **PASS**.

## Accepted baseline and source freeze

- Accepted baseline inventory: 69 files.
- Manifest-referenced canonical hashes: 68/68 exact, 0 mismatch.
- Manifest Git blob: `051b93a9b19dbdbcac6a3411afa4636b3b191ad2`.
- Accepted evidence diff: zero.
- Production/query/test/migration behavior diff: zero.
- No accepted artifact, profile expectation, repository query, or benchmark file changed.

## Transient and ignored output

Not intended for commit:

- `%LOCALAPPDATA%/Temp/realestate-queryreview/chapter-10f-v1-production-sql/captured-commands.json`;
- `%LOCALAPPDATA%/Temp/realestate-queryreview/chapter-10f-v2-baseline-20260901T185116Z-866b0dd1/`;
- `%LOCALAPPDATA%/Temp/realestate-l1-overlap-audit-da02ecbee40546f6b59e12f46cdc5ca1/`;
- `%LOCALAPPDATA%/Temp/realestate-l1-connected-smoke-20260901/`;
- `docs/planning/chapter-13l1-cumulative-chapter-13-verification-gate-evidence.md`.

The explicitly disposable PostgreSQL container was stopped and auto-removed. Deviations/blockers: none.
