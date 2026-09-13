# Chapter 14L — Cumulative Property-Taxonomy Verification Gate

Date: 2026-09-13

## 1. Scope and technical verdict

This record verifies the assembled property-taxonomy backend from one immutable source tree. It is a technical verification record only: it does not declare Chapter 14 product/status closeout, start Chapter 15, declare frontend readiness, complete a frontend handoff, consolidate documentation, or close unrelated quality items.

Technical gate verdict: **PASS, ready for independent review**.

No Domain, Application, Infrastructure, API, test, migration, EF model, QueryReview, generated-SQL baseline, benchmark baseline, or frontend file was changed. The only non-ignored repository artifact created by this gate is this record.

## 2. Immutable source and repository freeze

- Branch: `verify/property-taxonomy-cumulative-gate`.
- Immutable source HEAD: `e53821725d2c50684b301e1936917be313d69097`.
- Starting `git status --short`: empty.
- Starting `git diff --name-status`: empty.
- Starting untracked inventory: empty.

Accepted technical history present in the tested tree:

| Concern | Implementation/proof commit | Merge commit |
|---|---|---|
| Architecture plan | `777dc0f` | `fddb0d8` |
| Existing taxonomy boundary hardening | `e36849e` | `c4d9bb7` |
| Commercial/Land persistence foundation and migration 21 | `653e4f4` | `4b98875` |
| Management read contract | `b768aa1` | `cce1f28` |
| Shared listing read contracts | `19fc2c0` | `3ae8bc0` |
| Commercial/Land creation | `4e2ff19` | `1a4c499` |
| Code-name/chapter decoupling | `c5ba176` | `299b168` |
| Four-type Draft replacement | `410005d` | `37ba779` |
| Subtype replacement concurrency | `0c19be8` | `4499049` |
| Root-type discovery | `89c2687` | `08a603a` |
| Comparable root isolation | `5d6401e` | `6c6c28b` |
| Generated OpenAPI taxonomy contract | `ff13641` | `a79bc46` |
| Property-taxonomy SQL/plan delta proof | `03de306` | `e538217` |

The accepted SQL artifact is `docs/benchmarks/chapter-10f/chapter-14-property-taxonomy-generated-sql-delta-proof.md` at commit `03de30677119f8329d714c5951162ec95a29afd8`.

## 3. Restore, Release build, and complete suite

The literal required restore command was attempted first:

```text
dotnet restore
```

It exited 1 without diagnostic output due to the previously observed unbounded CLI worker failure. A final literal retry after verification reproduced the same silent exit and left 40 idle worker processes created at that attempt; that exact task-created cohort was terminated, and a follow-up process check found no remaining `dotnet` process. The deterministic bounded equivalent was run successfully:

```text
dotnet restore --disable-parallel -m:1 -nodeReuse:false
```

Result: exit 0; all projects were up to date.

```text
dotnet build -c Release --no-restore
```

Result: **0 warnings, 0 errors**, elapsed `00:01:05.31`.

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"
```

Result: **2,183 passed, 0 failed, 0 skipped**, total 2,183, duration `3 m 5 s`. This exactly matches the accepted SQL-proof task's final suite total.

## 4. Focused cumulative verification and accounting

### 4.1 Core assembled-tree selector

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~CreateListingValidatorTests|FullyQualifiedName~UpdateListingValidatorTests|FullyQualifiedName~ListingDraftReplacementEngineTests|FullyQualifiedName~ListingAuthoringRepositoryTests|FullyQualifiedName~ListingPersistenceTests|FullyQualifiedName~ListingUpdateConcurrencyTests|FullyQualifiedName~GetListingsValidatorTests|FullyQualifiedName~GetComparableListings|FullyQualifiedName~OpenApiDocumentTests|FullyQualifiedName~PostgreSqlCommercialAndLandTaxonomyMigrationTests|FullyQualifiedName~RootTypeDiscovery|FullyQualifiedName~ComparableRootIsolation|FullyQualifiedName~SubtypeReplacementConcurrency|Name~ManagementReadContract|Name~SharedListingReadContract" --logger "console;verbosity=minimal"
```

Result: **296 passed, 0 failed, 0 skipped**, total 296, duration 32 s.

### 4.2 Authoring/endpoint extension and combined unique selector

The authoring/replacement diagnostic selector passed **236/236**:

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~CreateListingValidatorTests|FullyQualifiedName~UpdateListingValidatorTests|FullyQualifiedName~ListingDraftReplacementEngineTests|FullyQualifiedName~ListingAuthoringRepositoryTests|FullyQualifiedName~ListingPersistenceTests|FullyQualifiedName~CreateListing_|FullyQualifiedName~UpdateListing_CommercialLand" --logger "console;verbosity=minimal"
```

It intentionally overlaps the 296-case core selector. The exact union was therefore executed instead of adding raw totals:

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~CreateListingValidatorTests|FullyQualifiedName~UpdateListingValidatorTests|FullyQualifiedName~ListingDraftReplacementEngineTests|FullyQualifiedName~ListingAuthoringRepositoryTests|FullyQualifiedName~ListingPersistenceTests|FullyQualifiedName~ListingUpdateConcurrencyTests|FullyQualifiedName~GetListingsValidatorTests|FullyQualifiedName~GetComparableListings|FullyQualifiedName~OpenApiDocumentTests|FullyQualifiedName~PostgreSqlCommercialAndLandTaxonomyMigrationTests|FullyQualifiedName~RootTypeDiscovery|FullyQualifiedName~ComparableRootIsolation|FullyQualifiedName~SubtypeReplacementConcurrency|Name~ManagementReadContract|Name~SharedListingReadContract|FullyQualifiedName~CreateListing_|FullyQualifiedName~UpdateListing_CommercialLand" --logger "console;verbosity=minimal"
```

Result: **340 passed, 0 failed, 0 skipped**, total 340, duration 40 s. Therefore the 296- and 236-case raw groups overlap by 192 executed cases and the authoring extension adds 44 distinct cases.

### 4.3 Protected Chapter 13 regression selector

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~ListingTranslationRulesTests|FullyQualifiedName~EffectiveTranslationOrderingTests|FullyQualifiedName~ListingPublicationReadinessTests|FullyQualifiedName~ListingLocationTests|FullyQualifiedName~CanonicalListingLocationTests|FullyQualifiedName~PostgreSqlActiveListingPublicationIntegrityTests|FullyQualifiedName~PostgreSqlListingTranslationRowIntegrityTests|FullyQualifiedName~PostgreSqlListingLocationIntegrityTests|FullyQualifiedName~GetListings_WhenRequestedLanguage|FullyQualifiedName~GetListings_WhenRequestedAndMacedonian|FullyQualifiedName~GetListings_DeterministicFallback|FullyQualifiedName~DraftCreateAndUpdate_OmittedOptionalLocalizedLocationFieldsRemainNull|FullyQualifiedName~GetMyListings_ReturnsTruthfulPrivateLocationState|FullyQualifiedName~GetListingManagement_ReturnsTruthfulAuthoringLocationState|FullyQualifiedName~PublishListing_Should|FullyQualifiedName~GetListings_DefaultNewestSort|FullyQualifiedName~GetListings_WithEqualSortValues_ReturnsStableAdjacentPages" --logger "console;verbosity=minimal"
```

Result: **355 passed, 0 failed, 0 skipped**, total 355, duration 31 s.

Discovery metadata found 340 behavior methods in the combined property-taxonomy union, 346 methods in the protected union, and **zero intersecting methods**. Theory expansion accounts for the protected run's 355 executed cases. The two unions therefore provide **695 unique focused executions**. No smaller diagnostic rerun below is added to that total.

### 4.4 Diagnostic subset reruns

| Concern | Exact selector | Result |
|---|---|---:|
| Migration lifecycle/catalog | `FullyQualifiedName~PostgreSqlCommercialAndLandTaxonomyMigrationTests` | 3/3 |
| Deterministic concurrency plus management/shared reads | `FullyQualifiedName~SubtypeReplacementConcurrency\|Name~ManagementReadContract\|Name~SharedListingReadContract` | 23/23 |
| Root discovery and filter validation | `FullyQualifiedName~RootTypeDiscovery\|FullyQualifiedName~GetListingsValidatorTests` | 49/49 |
| Comparable handler/root isolation | `FullyQualifiedName~ComparableRootIsolation\|FullyQualifiedName~GetComparableListings` | 17/17 |
| Generated OpenAPI | `FullyQualifiedName~OpenApiDocumentTests` | 12/12 |

All diagnostic subsets had zero failures and zero skips. They are contained in the 340-case combined union and are reported only to make concern ownership explicit.

## 5. Taxonomy, authoring, persistence, and read-contract proof

Source inspection plus the passing focused tests establish:

- `PropertyType` remains exactly `Apartment = 1`, `House = 2`, `Commercial = 3`, `Land = 4`.
- `CommercialType` is exactly `Unknown`, `Office`, `Shop`, `Other`; `LandType` is exactly `Unknown`, `BuildingPlot`, `AgriculturalLand`, `Other`.
- Create and Update validators reject undefined materialized enum values, preserve defined `Unknown`, and require exactly the detail object matching the selected root while forbidding the other three.
- Commercial/Land Create tests exercise every defined subtype, omission-to-`Unknown`, personal and agency authorization parity, persistence, response shape, publication with `Unknown`, and public read.
- The replacement-engine matrix enumerates all 16 source/target cells. It verifies same-type tracked mutation, target creation, incompatible-row deletion across all four tables, malformed-Draft repair, nullable/common clearing, reset-to-`Unknown`, translation-ID preservation by language, classification-only confirmed-location preservation, canonical location-change invalidation, and transactional rollback.
- Management GET → PUT → GET tests represent Commercial/Land children truthfully and leave all incompatible slots null.
- Management, private/shared, public, lifecycle, dashboard, agency, and comparable mappings preserve separate contracts and truthfully map present or absent Commercial/Land children.
- Representative personal/agency authorization, non-Draft conflicts, post-lock authorization, and failure-before-mutation behavior remain protected.

## 6. Migration and EF model verification

```text
dotnet ef migrations list --project src/RealEstate.Infrastructure/RealEstate.Infrastructure.csproj --startup-project src/RealEstate.Api/RealEstate.Api.csproj --configuration Release --no-build --no-connect
```

The command returned these **21 migrations**, in order:

1. `20260610042853_AddListingTables`
2. `20260610045326_AddListingBasicDetails`
3. `20260612071243_RenameUpdatedAtToModifiedAtAndAddAuditing`
4. `20260615115032_AddListingImages`
5. `20260617111522_AddListingCommonDetailsAndMunicipality`
6. `20260618104337_AddListingPropertyDetails`
7. `20260618171112_AddUsersTable`
8. `20260619141243_AddListingCreatedByUserId`
9. `20260624143332_AddAgenciesTable`
10. `20260625123943_AddAgencyMembersTable`
11. `20260625135937_AddListingAgencyId`
12. `20260706171256_AddUserAvatarFields`
13. `20260708204036_AddAgencyInvitations`
14. `20260711031633_AddAgencyLogoMetadata`
15. `20260721112146_AddListingTranslationQTrigramIndex`
16. `20260809124123_EnforceListingTranslationRowIntegrity`
17. `20260811091318_EnforceActiveListingPublicationIntegrity`
18. `20260812172728_EnforceOptionalLocalizedLocationRowIntegrity`
19. `20260813100457_AddCanonicalGeocodedLocationSnapshot`
20. `20260824141614_EnforceStrongActiveLocationIntegrity`
21. `20260904023937_AddCommercialAndLandPropertyTaxonomy`

The 3/3 migration family proves exact Commercial/Land table columns/defaults, shared-PK uniqueness, Listing FKs with cascade, only implicit PK indexes, fresh application of all 21 migrations, repeated Up no-op, migration 20 → 21 preservation, Down to 20, and re-Up. Representative Apartment/House subtype, translation, and trusted-location data remain preserved through the cycle.

```text
dotnet ef migrations has-pending-model-changes --project src/RealEstate.Infrastructure/RealEstate.Infrastructure.csproj --startup-project src/RealEstate.Api/RealEstate.Api.csproj --configuration Release --no-build
```

Result: `No changes have been made to the model since the last migration.`

## 7. Authorization and deterministic concurrency

The passing replacement-concurrency and existing authoring tests verify:

- opposing Apartment/Commercial PUTs serialize on the Listing parent and leave one matching dependent row;
- same-root Commercial subtype replacements serialize and the last accepted request wins without a tracking/shared-PK conflict;
- non-owner publish waits for a Commercial update and authorization still precedes readiness;
- location confirmation and classification-only Commercial replacement serialize while retaining confirmed location;
- all concurrent tasks are deterministically orchestrated and drained; no sleep/timing-based race is used;
- personal and representative agency Create/Update paths retain existing authorization behavior.

No new lock, row version, ETag, isolation mode, retry policy, or concurrency abstraction is present.

## 8. Root discovery and comparables

The 49/49 discovery subset establishes exact equality filtering for all four roots, name/numeric/null binding behavior, canonical rejection of undefined numeric values, Active-only visibility, count-before-page semantics, total/page membership, deterministic newest/UUID ordering, shared effective-language and trusted-location behavior, agency-public path reuse, and absence of `commercialType`/`landType` query parameters.

The locked `q` field set remains Title, City, Municipality, and Neighborhood. Tests prove Description, AddressLine, non-effective translation text, and Commercial/Land subtype labels do not match.

The 17/17 comparable subset establishes Commercial-only and Land-only root equality, Apartment/House regression behavior, and subtype neutrality for Office/Shop and BuildingPlot/AgriculturalLand. Exact IDs/order and response details are asserted. Production inspection confirms the six ranking keys remain, in order:

1. location tier;
2. relative area difference;
3. relative price-per-square-meter difference;
4. relative price difference;
5. `CreatedAtUtc` descending;
6. UUID `Id` descending.

The existing limit, source-state/price-area requirements, effective-language/City semantics, and public response mapping remain protected. CommercialType and LandType do not participate in eligibility or ordering.

## 9. Generated OpenAPI verification

The explicit OpenAPI run passed **12/12**. Its generated-document assertions protect:

- exact symbolic `PropertyType`, `CommercialType`, and `LandType` arrays;
- truthful Create and full-replacement conditional detail prose without falsely requiring all parent detail objects;
- nullable parent detail wrappers;
- Create omission-to-`Unknown` and PUT omission/reset-to-`Unknown` descriptions;
- absence of subtype-enum OpenAPI `default` keywords while retaining the unrelated EUR default;
- required/non-null response subtype members when their parent detail object exists;
- strict existing public required fields;
- four-root `propertyType` query enum and absent `commercialType`/`landType` query parameters;
- POST, PUT, management, public, and comparable schemas plus existing success/error components.

Runtime validation, controller signatures, and failure codes were not changed by this gate.

## 10. Protected Chapter 13 regressions

The 355-case protected union and complete suite retain explicit proof for:

- translation normalization/completeness and requested → `mk` → PostgreSQL-C/UUID deterministic fallback;
- Draft truthful nullable translation/location state;
- Active publication readiness and public fail-closed integrity;
- trusted confirmed-location structure and public integrity;
- public/private/management response separation;
- authorization-before-readiness and stable lifecycle/error behavior;
- deterministic discovery ordering/paging and effective-translation filtering;
- root PropertyType comparable isolation.

No Chapter 13 translation, location, readiness, parent-lock, public-integrity, discovery, comparable, or error contract was reopened.

## 11. Accepted SQL/plan proof freshness

Accepted proof identity:

- Path: `docs/benchmarks/chapter-10f/chapter-14-property-taxonomy-generated-sql-delta-proof.md`
- SHA-256: `ad71c73a85adf61f09268a6b4e5da9ac459a8897273dc66d2d3abab58fe95178`
- Git blob: `e32b340821dbc89871d52d798b81fb2fe2465a5e`
- Introducing commit: `03de30677119f8329d714c5951162ec95a29afd8`

The proof records 33 production commands, 80 exact typed-parameter records, 25 exact historical SQL bodies, exactly eight approved widened root bodies, 198/198 warm-up/measured plan executions, locked results/order, the unchanged 100,000-listing Apartment/House profile with 61/61 invariants, unchanged accepted baseline, Q1 gates, and zero spill/temp anomalies.

Every recorded query/profile input hash was recomputed from the immutable source tree and matched exactly:

| Path | SHA-256 | Git blob |
|---|---|---|
| `src/RealEstate.Infrastructure/Persistence/Repositories/ListingRepository.cs` | `bea302e79470bd5de00c5a382f602ee3246ce056b4422caf66a1b16d08a19975` | `906a3a00957d8504c8bfe93b5e378ce8e24f3512` |
| `src/RealEstate.Infrastructure/Persistence/Repositories/AgencyRepository.cs` | `eac9fdc055518f797ed41af710fad6eee773341e35b86378e01b7c7484411c7f` | `2e437f422558f9eebc4255dce5c87898c406aff9` |
| `src/RealEstate.Application/Listings/Repositories/IListingRepository.cs` | `b132829c0234f1819ee5026f15999814e8e1e50c973663cf3e7fe25b76eaa038` | `22b77be4cd055981db07f94a19fe086239541300` |
| `src/RealEstate.Application/Agencies/Repositories/IAgencyRepository.cs` | `259de554b719fd675bd34b8da1e84182960c4535af376863cada67179985dd1b` | `edab2bdbb7ce57d6f45b2c8de9801ad4b7768e16` |
| `src/RealEstate.Infrastructure/Persistence/Configurations/ListingConfiguration.cs` | `31aaaaab350166375578fda8347c3483d31a1c89a39e536a4389d7ea3dcd5143` | `8c7193e406f14263a7f9c02e400946d5cbea0e10` |
| `src/RealEstate.Infrastructure/Persistence/Configurations/ListingCommercialDetailsConfiguration.cs` | `2fd61d92ec7774253302ac1e79fa631b1dbbb7187bc21130a205676b0d45f85b` | `d4ea2230059cbce076c5bddb424396bcc339f433` |
| `src/RealEstate.Infrastructure/Persistence/Configurations/ListingLandDetailsConfiguration.cs` | `21c5779c7245a6e64f10becf6a1d2fd9eef8a9c6ccde3db1abe502af91d6e254` | `4f39fc420df5034cf8a49c2449934494bfcc63f0` |
| `tools/RealEstate.QueryReview/QueryShapeDefinitions.cs` | `a7344e1404d342b6431bbaae963f0a888112494abfa741eaa852b0df4c52c769` | `c651cef33ef239a86b8c467c2510b1af5ba0ce0d` |
| `tools/RealEstate.QueryReview/ProductionCommandCaptureInterceptor.cs` | `1b2574134afdc370f16bab0ca018f7757f740c6d7ef6ad2eea610a22c484953a` | `d18660b4c4cc7527ea50c359f0e065f58c13635a` |
| `tools/RealEstate.QueryReview/DeterministicProfileSeeder.cs` | `64fe05b32ff2af4f2b8fe4946acc45ad6044b801abda178b361a2d500778c5ae` | `8751b85abd540dbd746cfedd25069af8a6a87750` |

The accepted SQL proof is therefore current. Repeating the full 198-plan workflow was neither necessary nor performed. No `baseline export` was run, and no accepted Chapter 10F baseline file was regenerated or modified.

## 12. Final repository delta

Final non-ignored task delta:

```text
?? docs/chapters/chapter-14l-cumulative-chapter-14-verification-gate.md
```

`git diff --name-status`, `git diff --stat`, and `git diff` are empty because the sole durable artifact is untracked. `git diff --check` passes. The ignored implementation evidence is `docs/planning/chapter-14l-cumulative-verification-implementation.md`.

Zero production, test, migration, snapshot/designer, QueryReview, query, OpenAPI implementation/test, benchmark baseline, or frontend changes were made.

Technical verification disposition: **PASS, pending independent review**.
