# Chapter 15L — Cumulative technical verification gate

Technical disposition: **FINAL_PASS**, submitted for independent audit. This is not Chapter 15 closeout.

**Chapter 15 is NOT complete until 15M passes independent audit and is owner-committed.** 15L does not update chapter status or backend-context closeout fields.

## Immutable source, scope and environment

- Date: 2026-10-08. Branch: `verification/chapter15-cumulative-gate`.
- IMMUTABLE_15L_SOURCE: `cf1eec2d7352a0545e5400ca5eeb9cda8bc3d552`.
- Expected/actual `HEAD:src`: `367529280368366f8216471daff0c2d9e7984ccd`.
- Initially clean worktree/staging; HEAD remained unchanged. No Git mutation, staging or commit.
- 15K owner publication commit: `3466ae1a4ec4427082619d8915dc0215ad0f2a9f`; all 339 PG18.4 files are tracked in the immutable merge above. The raw capture source `fde28a9543831a10fb7eaa3720f779e63adf91e3` is a different, earlier commit. Its src/tools/tests/solution bytes are unchanged in this verification source.
- Prior independent task acceptance is an owner-provided prerequisite. No unresolved prior-task finding surfaced in the accepted artifacts or this verification.
- Windows 10.0.19045 win-x64; SDK 10.0.401; MSBuild 18.9.11; .NET/ASP.NET runtime 10.0.12; EF CLI 10.0.9; Docker Desktop engine 29.8.1 linux/amd64.
- Commercial/Land NO_INDEX; 15I skipped; migrations 21.

Only this gate and ignored `docs/planning/chapter-15l-cumulative-verification-evidence.md` were written in the repository. No executable/test/tool/model/migration/query/profile/SQL/benchmark/frontend/protected-document change, remediation, benchmark recapture or export occurred. Temporary verification artifacts are outside tracked paths. Disposable databases/Testcontainers only; no development database use.

## Release and focused execution

The original restore/build/full-suite claims had no retained authentic logs or full-suite TRX and were NOT ACCEPTED for execution provenance. This is a newly recorded correction run on the same immutable source, not a reconstruction of the original execution. All other independently accepted gates below remain unchanged.

Executed exactly for the recorded correction:

```text
dotnet restore RealEstate.slnx --disable-parallel -m:1 -nodeReuse:false
dotnet build RealEstate.slnx -c Release --no-restore
dotnet test RealEstate.slnx -c Release --no-build --no-restore --results-directory C:/Users/User/AppData/Local/Temp/realestate-chapter15-gate-6c447f8deadb471aa041651e0dfa52e5 --logger "console;verbosity=minimal" --logger "trx;LogFileName=chapter-15-full-suite.trx"
```

Restore PASS. Release build PASS, **0 warnings / 0 errors** (build-reported elapsed 26.98s). Recorded unfiltered solution suite: **2,419 passed, 0 failed, 0 skipped, 2,419 total**, console duration 3m50s. Every native exit code was captured immediately and equals **0**. Totals were independently reconciled from the new console log and TRX, not copied from the original claim.

### Retained authentic execution proof

All four artifacts below were produced from `cf1eec2d7352a0545e5400ca5eeb9cda8bc3d552`. R/B/T identify the exact restore/build/test commands above. Complete stdout/stderr was captured with `2>&1 | Tee-Object`; `$LASTEXITCODE` was saved immediately afterward. Logs/TRX remain intact until independent audit; no output was reconstructed, sanitized or rewritten. The retained `chapter-15-release-execution.json` in the same temporary directory records executable, arguments, source, working directory, timestamps, elapsed time and native exits.

| Command | UTC start (2026-10-08) | UTC end (2026-10-08) | Native exit |
|---|---|---|---:|
| R | 12:15:11.6482762 | 12:15:22.7938980 | 0 |
| B | 12:15:22.8758461 | 12:15:50.2872488 | 0 |
| T | 12:15:50.3091058 | 12:19:50.1284140 | 0 |

| Absolute artifact path | Command | Raw bytes | Raw SHA-256 |
|---|---|---:|---|
| `C:/Users/User/AppData/Local/Temp/realestate-chapter15-gate-6c447f8deadb471aa041651e0dfa52e5/chapter-15-restore.log` | R | 166 | `ddf68db3d8d2f9572a71c51f3f3fdb8d71553978fea549cde378875132e4b7b4` |
| `C:/Users/User/AppData/Local/Temp/realestate-chapter15-gate-6c447f8deadb471aa041651e0dfa52e5/chapter-15-release-build.log` | B | 2030 | `a80db500419b2b8c861b7e18316b1ac04ddcf5274552469ea5109fc890799b19` |
| `C:/Users/User/AppData/Local/Temp/realestate-chapter15-gate-6c447f8deadb471aa041651e0dfa52e5/chapter-15-full-suite.log` | T | 964 | `a0a56b6968cb2da1a69951aee570eede1e09a9b24591b3f4c66b9efa04dd80c0` |
| `C:/Users/User/AppData/Local/Temp/realestate-chapter15-gate-6c447f8deadb471aa041651e0dfa52e5/chapter-15-full-suite.trx` | T | 17211234 | `1650de36cb2afc44eddb56398b8e475e89c143221e2273a10be8e8ce80044bf1` |

The TRX shares command T's execution/source/exit provenance; it is not a separate process. Its test interval is 14:15:52.2992404 through 14:19:49.0626116 +02:00 on 2026-10-08. Independently parsed UnitTestResult count, TRX total/executed/passed and console total/passed all equal 2,419; every individual outcome is Passed. Failed/error/timeout/aborted/inconclusive/passedButRunAborted/notRunnable/notExecuted/disconnected/warning/inProgress/pending counters are all zero. Console skipped is zero. No filter was supplied. The sole solution test project, RealEstate.Tests, is present as `tests/RealEstate.Tests/bin/Release/net10.0/RealEstate.Tests.dll`, built immediately before this no-build/no-restore solution run. No assembly was silently omitted.

Raw-output credential/local-connection-assignment scans found no actual credentials to expose; no sensitive output was copied into this document. Source/staging boundaries and protected evidence hashes were rechecked without rerunning benchmarks or regenerating prior artifacts. The original focused TRX, OpenAPI, migration and catalog artifacts were preserved. Focused accounting below uses ONLY `chapter-15-focused.trx` and `chapter-15-current-migration.trx`, never the new full-suite TRX or a directory-wide TRX wildcard.

Listed all tests, then listed the exact Task 15L main filter and migration filter before execution. Every required family selected a nonzero set. Main focused TRX: 372/372 PASS. Migration TRX: 3/3 PASS. No failed/error/timeout/aborted/not-executed result. 15I's conditional index-migration branch was not activated.

| Family | Selected/executed/passed |
|---|---:|
| OpenApiDocumentTests | 12 |
| RootTypeDiscovery | 19 |
| CommercialAndLandSubtypeDiscovery | 4 |
| GetAgencyListings_SubtypeQueryParameters | 1 |
| GetListingsValidatorTests | 35 |
| GetAgencyDashboardListings | 35 |
| GetComparables | 35 |
| ComparableRootIsolation | 2 |
| RealEstate.Tests.Unit.QueryReview | 229 |
| PostgreSqlCommercialAndLandTaxonomyMigrationTests | 3 |
| Unique focused union | **375** |

TRX XML union key: **testId|testName**, never executionId. Distinct theory rows retained; 375 executions, 375 unique identities, no double-counted duplicate. Focused executions are **not added** to the full-suite total.

Ordinal-sorted stable-identity lines, LF plus terminal LF, UTF-8 SHA-256:
`43218a28946550e8354f1ce18b192f7ac8e7deaaf83f28818c1153fb85fbb399`.

Separately named raw TRX hashes:

- `chapter-15-focused.trx`: `24c36b0628afb64647550ed1d1ceb7f042e14629c110e2e61add21a2a3acde23`.
- `chapter-15-current-migration.trx`: `d4e7ce0e8b65a39bb95deccec416d6ea3a75ab72ce3e91fbbe1a0366299fd058`.

Exact focused commands and unique temporary result location are retained in ignored evidence. The filter is exactly the plan's OR union of the first nine families, plus the separately executed migration family.

## Public discovery, dashboard and comparables

Source inspection and actual passing tests prove:

- PropertyType Apartment=1, House=2, Commercial=3, Land=4. CommercialType Unknown=0, Office=1, Shop=2, Other=3. LandType Unknown=0, BuildingPlot=1, AgriculturalLand=2, Other=3.
- Exact PostgreSQL-backed HTTP discovery for every defined subtype, with IDs/count assertions; Unknown is exact, defined numeric inputs remain accepted, empty nullable input remains absent.
- Commercial/Land each require explicit root AND matching child/subtype. Contradictory root/subtype and cross-family input return empty, including malformed mismatched/dual-child fixtures. Apartment/House predicates remain unchanged.
- Malformed/undefined HTTP enums produce automatic canonical model-state 400; undefined directly constructed enums reject through application validation. No custom binder.
- Reduced agency route ignores subtype-looking keys; agency+subtype composition remains on general public search.
- Active-only, fail-closed persisted integrity, filter-before-count/page, stable tie-breakers and parent paging before child hydration remain protected. `CommercialAndLandSubtypeDiscovery_FilteringPrecedesCountAndPagingWithStableNewestOrder` passed.
- Effective translation remains requested -> mk -> bytewise language -> UUID. The optimized discovery SQL's unique selected language makes UUID ties impossible; mapping/comparable ordering preserves the general rule. q remains only Title/City/Municipality/Neighborhood, escaping backslash then percent then underscore with literal ILIKE semantics.
- Dashboard invalid HTTP status remains model-state 400. Direct undefined status produces validation.failed/key status only after user/agency/member access checks and before repository invocation. Tests prove zero repository calls, all six access-failure precedence scenarios, absent/every-defined status, and unchanged valid-input call order.
- Comparables remain exact-root and subtype-neutral, with six keys: location tier, relative area, relative unrounded unit price, relative price, CreatedAtUtc descending, Id descending. Limit follows ranking before hydration. Commercial/Land root-isolation/order regressions passed; no new similarity rule.

Inspected ListingRepository, GetListings handler/validator, dashboard handler/validator/direct tests, subtype/root/agency integration families, EffectiveTranslationOrdering tests and comparable families. The complete suite also executed protected Chapter 13/14 strong-Active/translation/location integrity, failure-boundary, lifecycle, authorization and concurrency coverage. No protected behavior was inferred solely from a test-name count.

## Generated OpenAPI

Obtained `GET /swagger/v1/swagger.json` from the unchanged Release application launched against a dedicated disposable database. This is the actual Swashbuckle application path; 12 passing OpenApiDocumentTests additionally use ISwaggerProvider.GetSwagger("v1") and serialize it. No manually constructed contract, generated client or committed OpenAPI output.

Generated JSON SHA-256: `e99a94c3aef8a7c07bd5ee91497d8ade0b7caef75315f3f241a9a04f7ff0645e`.

All **46 operations** inspected. Only GET `/api/listings` exposes optional scalar commercialType/landType query parameters (required absent/false), referencing these exact string-enum components:

| Schema | Values |
|---|---|
| PropertyType | Apartment, House, Commercial, Land |
| CommercialType | Unknown, Office, Shop, Other |
| LandType | Unknown, BuildingPlot, AgriculturalLand, Other |

Optionality is represented by non-required parameters; numeric HTTP compatibility is behavior, not advertised enum values. Exact checked reduced GET vocabularies:

- `/api/agencies/{id}/listings`: id, lang, sort, currency, page, pageSize.
- `/api/listings/my`: lang, page, pageSize.
- `/api/agencies/{id}/dashboard/listings`: id, lang, status, page, pageSize.
- `/api/listings/{id}/management`: id.
- `/api/listings/{id}/comparables`: id, lang, limit.

No other operation exposes either subtype query parameter. Dashboard 400 remains application/problem+json -> ApiValidationProblemDetailsResponse. Contract result: PASS.

## Migration/model and lifecycle

Executed with process-local configuration pointing only to a fresh disposable PostgreSQL 16 container:

```text
dotnet ef migrations list --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api
dotnet ef migrations has-pending-model-changes --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api
dotnet ef database update --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api
```

List PASS, 21 identities. On the deliberately empty database the first list emitted a missing-history-table probe and marked migrations Pending; command succeeded. This describes initial database state, not model drift. Model check PASS: no changes since the last migration. Fresh update PASS; independent catalog reported 21 applied migrations.

The dedicated three-test family also passed: exact structural catalog; fresh all-21 apply plus repeat no-op; 20->21 data preservation, Down and re-Up. Earlier supported migration lifecycle matrices passed in the full suite. No conditional/placeholder 15I migration exists.

Both subtype tables have exactly their valid/ready/live shared primary-key indexes, no secondary subtype index:

```sql
CREATE UNIQUE INDEX "PK_ListingCommercialDetails" ON public."ListingCommercialDetails" USING btree ("ListingId")
CREATE UNIQUE INDEX "PK_ListingLandDetails" ON public."ListingLandDetails" USING btree ("ListingId")
```

Migration directory Git tree: `6309b20a572f2fc697d753921dfc79529a0842f7`. Snapshot raw SHA-256: `9c6f54b7ecc1df939958f817f87f16cc50da8192c2592c8acf38ca2e77fc4b13`. Migration/configuration delta from accepted pre-Chapter-15 release: empty. Exact inventory:

```text
20260610042853_AddListingTables
20260610045326_AddListingBasicDetails
20260612071243_RenameUpdatedAtToModifiedAtAndAddAuditing
20260615115032_AddListingImages
20260617111522_AddListingCommonDetailsAndMunicipality
20260618104337_AddListingPropertyDetails
20260618171112_AddUsersTable
20260619141243_AddListingCreatedByUserId
20260624143332_AddAgenciesTable
20260625123943_AddAgencyMembersTable
20260625135937_AddListingAgencyId
20260706171256_AddUserAvatarFields
20260708204036_AddAgencyInvitations
20260711031633_AddAgencyLogoMetadata
20260721112146_AddListingTranslationQTrigramIndex
20260809124123_EnforceListingTranslationRowIntegrity
20260811091318_EnforceActiveListingPublicationIntegrity
20260812172728_EnforceOptionalLocalizedLocationRowIntegrity
20260813100457_AddCanonicalGeocodedLocationSnapshot
20260824141614_EnforceStrongActiveLocationIntegrity
20260904023937_AddCommercialAndLandPropertyTaxonomy
```

## Permanent evidence and measured disposition

Ran the existing offline command for each directory below, with no recapture/export:

```text
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-dir <directory>
```

| Directory under docs/benchmarks | Files | Inventory SHA-256 | Measurements raw SHA-256 |
|---|---:|---|---|
| four-root-discovery-v1/evidence/postgresql-16 | 169 | 41472809280fecf6924abd6622e2bad6c32b7e3d0cbe73a33fc88b31e13e44ef | f781ce5ce18be1c50f49856743fc409f5750e4fcf233a406f7a9464cbd29e8eb |
| four-root-discovery-v1/evidence/postgresql-18.4 | 339 | aa6c862db64eaeba968727e67302f246e19e1d9ff859322b4e557477cae5fbf9 | 6b4f06cca3fbf9c78c877fc584b2019cf2333ac6b99eb0f0bd7d914a06ee428f |
| chapter-10f/evidence | 69 | 5237fef672c2c57f55e4ee4ff22f5af97fc49a09e7ad116e8b02c48d2c9d0cd1 | d6dac6f58245f7ecd65b626ca1c3b85df2a2d39808a350e8b1536b82779f5a17 |

All three offline verifications PASS; independently computed complete inventories match. Inventory: slash-relative-path|lowercase raw SHA, StringComparer.Ordinal sorting, LF joining plus terminal LF, UTF-8 SHA-256; distinct from internal canonical-text hashes.

- Historical committed measurements blob: `051b93a9b19dbdbcac6a3411afa4636b3b191ad2`.
- PG18 sealed summary raw SHA: `c3770701a37e1add9d456efe855a526d87f6c359a21c0a20f14ca8f6759b736b`.
- Profile SHA: `7d389dfbecb10fa0f491a58e6f83bda265cee1ea7e664a43b53f6fcc7c838946`.
- Invariant manifest/result SHA: `bae3243bd1da79993710b3522b2f1e7c1c695152cd8dea25d61d13f9d9331be7`.
- Shape SHA: `fce861c91a31a8e505dd0193cc0baaad5982d5c81fc1454258f2281a7b74a979`.
- Result/order SHA: `975a9d37ced4e372c0aec370782bf7932101042ebcd263e14a097fad0d4cab19`.

Successors preserve 179 invariants, 21 shapes, 83 commands, 190 parameters, 498 source plans, one warm-up/five measured rounds (83/415 samples). Permanent artifacts retain schema-defined median plans and complete capture/sample metadata, not 498 separate permanent plan files. Offline verification passes source/profile/shapes/SQL/parameters/results/order, plan/sample completeness, inventory and safety contracts. Historical schema-1 remains a separate 61-invariant/33-command/80-parameter/198-plan generation.

PG18 embeds its 170-file verified contemporaneous PG16 comparison and seals all **23 sequence rows**, complete topology differences, full Q1 parity and observational/authority qualification. Computed threshold A (>25% AND >2ms slower) = **0**; B (>20% additional shared blocks) = **0**. Both lanes retain Q1 trigram use and reject prohibited/unnamed sequential scans; spill/temp/capture anomalies are zero. Derived L1/Q1 differences contain only PG16's named Listings Seq Scan, no PG18-only entries. No partial report is accepted.

Accepted disposition SHA: `dda685aa1e6053424728cf70dfe8a1a9fc2b9956e8de5c7bd24370cb00c01800`. All candidates failed mandatory Gate 3; Commercial/Land NO_INDEX and migration count 21 are intentional successful outcomes. No thresholds or selection rule were revisited.

**PostgreSQL 16 remains authoritative for correctness, historical comparison, performance and index decisions. PostgreSQL 18.4 is bounded compatibility/performance observation only. Cross-major timing is not an SLA.**

## Quality ownership and frontend protection

All ten register entries remain **OPEN**, unmodified; register SHA `a766170d8cc667266462aae0770a88cc18db0469c892e3d46bc4564359d4a67e`. Functional ownership below follows plan section 17 and the register; it does not invent personal assignees or declare issues resolved.

| Entry | Retained ownership boundary and status |
|---|---|
| QH-TX-01 | Focused transaction-cleanup follow-up; nonblocking diagnostic risk |
| QH-TEST-01 | Agency concurrency-test cleanup; low-priority failure-path hygiene |
| CH11-DB-01 | Owner-authorized data audit/backfill before nullable-creator policy changes |
| CH11-DB-02 | Owner-approved constraint-family/data audit; broad policy deferred |
| CH11-STATE-01 | Product/aggregate-specific concurrency/freshness decision; no global redesign |
| CH11-FILE-01 | Media operational reconciliation/deletion-intent follow-up; accepted orphan limitation |
| QH-TEST-02 | Integration-fixture maintenance; low-priority raw-SQL cleanup |
| C12-CONFIG-01 | Deployment/auth hardening checkpoint; production-deployment blocker still open |
| CH13-J2-DEPLOY-01 | First real staging/production deployment owner; target-zero check still required |
| CH13-PERF-01 | Separately authorized long-lived translation-authoring operational review; nonblocking and not resolved/measured by subtype work |

No entry was removed, reclassified or laundered by passing tests. No real deployment target was assessed.

Frontend authority: accepted pre-Chapter-15 release `8f7c2d9c28624b57653b3b313e68e915f536e4c4` (`backend-property-model-taxonomy-v1`). Frozen handoff blob at that release and HEAD: `de89f71fef111970b9568927f9bc394606892920`. Direct committed-byte comparison PASS; both raw blob SHAs are `79316046af9b8d12592dfb131fe612eac19d4bf4e58a7c6f31e7cc9166dfb138`.

The existing working copy uses CRLF while the committed blob uses LF. Its raw before/after SHA remains `43557548b97f964598ea3a36a2d21e533bc40a0bda29fa734f6d6a8edf9b6f78`; in-memory LF normalization matches the committed hash. No normalization/edit was performed. Cross-representation raw hashes are not conflated.

Git history/path comparison shows no frontend/handoff changes across Chapter 15. Neither revision contains a frontend/client/web source tree in this backend repository; no claim is made about an uninspected sibling repository. Chapter-15 production changes are only eight bounded backend files. No frontend reconciliation occurred.

## Final gate table and disposition

Permanent credential/tamper verification and independent recursive credential/local-connection-assignment scans PASS. New durable documentation contains no secrets or local connection assignments. Existing registered JWT deployment risk is not claimed fixed. Temporary results/API output remain outside tracked paths. All task test containers and the separate EF/Swagger API/container/anonymous storage were disposed; the existing development container was untouched.

| Gate | Result |
|---|---|
| Immutable source/branch, clean start, tracked accepted 15K | PASS |
| Restore / Release build with zero warnings/errors | PASS |
| Full 2,419-test suite, no failed/skipped tests | PASS |
| Nonzero focused selection / stable unique union 375 | PASS |
| Subtype/agency/public inherited Chapter 13/14 behavior | PASS |
| Actual generated OpenAPI contract and route isolation | PASS |
| Dashboard access/validation/repository ordering | PASS |
| Root-only subtype-neutral six-key comparables | PASS |
| 21 migrations / fresh, upgrade, Down/re-Up / no pending model | PASS |
| NO_INDEX catalog/disposition | PASS |
| PG16/PG18/historical offline identities and safety | PASS |
| 23-sequence durable report / zero unresolved thresholds / Q1 parity | PASS |
| Ten quality issues retain ownership and open status | PASS |
| Bytewise handoff and frontend protection | PASS |
| Credentials / cleanup / allowed-file-only delta / diff check | PASS |

Failures requiring return to an owning task: **none**. Technical **FINAL_PASS** is limited to this immutable tree and awaits independent audit; it is not independent self-acceptance or chapter completion.

Only repository outputs:

1. `docs/chapters/chapter-15l-cumulative-chapter-15-verification-gate.md`.
2. `docs/planning/chapter-15l-cumulative-verification-evidence.md` (ignored).

No staged files; HEAD/source unchanged; `git diff --check` PASS. **15M must still pass independent audit and be owner-committed before Chapter 15 is complete.**
