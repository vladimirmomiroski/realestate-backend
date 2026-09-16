# Chapter 15 — Discovery, Performance, and Integration Hardening

## 1. Status and authority

**Status:** authoritative implementation plan; implementation has not started.

This chapter plan was prepared on `planning/discovery-performance-hardening` at `4e9499fc077f3e7b238177c5263b93aacb295921`. The planning tree was clean before this file was created. The annotated release tag `backend-property-model-taxonomy-v1` contains this commit and resolves to the later `main` merge `8f7c2d9c28624b57653b3b313e68e915f536e4c4`.

Primary technical input is `docs/planning/chapter-15-implementation-discovery.md`; important claims were checked against current source. Durable authority is, in descending relevance:

1. this plan once owner-approved;
2. `docs/backend-context.md` and `docs/backend-quality-handoff.md`;
3. `docs/chapters/chapter-14-property-model-taxonomy-expansion.md` and `docs/chapters/chapter-14l-cumulative-chapter-14-verification-gate.md`;
4. `docs/benchmarks/chapter-10f/chapter-14-property-taxonomy-generated-sql-delta-proof.md`;
5. protected Chapter 13 authority and the frozen historical `docs/backend-frontend-handoff.md`.

Chapter 14 is accepted, owner-committed, merged, and tagged. Older source-controlled wording that says 14M acceptance or the owner commit is pending is documentation lag, not an incomplete technical gate.

## 2. Repository baseline

| Fact | Planning baseline |
|---|---|
| Branch | `planning/discovery-performance-hardening` |
| Source commit | `4e9499fc077f3e7b238177c5263b93aacb295921` |
| Chapter 14 release | `backend-property-model-taxonomy-v1` -> `8f7c2d9c28624b57653b3b313e68e915f536e4c4` |
| Migrations | 21; latest `20260904023937_AddCommercialAndLandPropertyTaxonomy` |
| Accepted complete suite | 2,183 passed, 0 failed, 0 skipped |
| Accepted focused accounting | 695 unique focused executions |
| OpenAPI / migration gates | 12/12 OpenAPI; 3/3 migration lifecycle/catalog; no pending model |
| Historical QueryReview profile | 100,000 listings, 200,000 translations; 50,000 Apartment, 50,000 House, no Commercial/Land |
| Historical SQL evidence | immutable 69-file baseline; 33 commands, 80 typed parameters, 198 plans, 61/61 invariants |
| Accepted historical result hash | `7f74f991bf29b6f3ad24d48f2e8e13ecf9f375ea6f9eb0da8f18204c528bfb36` |
| PostgreSQL evidence/runtime | accepted integration and performance evidence on PostgreSQL 16; tracked Docker runtime image 18.4 |

All totals are baselines, not promises about the final Chapter 15 totals. Closeout must record the actual values.

The historical result SHA-256 above is the exact 64-character value locked by `BaselineEvidenceWriter`, `docs/benchmarks/chapter-10f/evidence/baseline-measurements.json`, and `docs/benchmarks/chapter-10f/chapter-14-property-taxonomy-generated-sql-delta-proof.md`.

## 3. Chapter goal

Chapter 15 makes the completed four-root property model operationally mature. Its smallest coherent surface is:

- exact Commercial/Land subtype discovery on the general public search route;
- one bounded application-layer enum defense for directly constructed agency dashboard queries;
- a maintainable, explicitly versioned QueryReview successor generation;
- a deterministic four-root engineering profile and subtype query shapes;
- PostgreSQL 16 unindexed characterization and evidence-led index disposition;
- a production index migration only if a candidate meets the locked gate;
- immutable successor SQL/result/order/plan evidence plus a bounded PostgreSQL 18.4 compatibility lane;
- cumulative verification and documentation closeout.

This is discovery, performance, and integration hardening. It is not another property-model chapter and does not authorize new product attributes.

## 4. Protected inherited contracts

Chapter 15 must preserve all of the following.

- Public listing reads are Active-only and fail closed on public-integrity violations. Draft authoring remains truthfully nullable.
- Public, private, management, and authoring contracts remain distinct; private metadata must not leak publicly.
- Publication retains canonical translations, trusted confirmed location, and current readiness rules.
- Effective translation remains `requested -> mk -> bytewise language -> translation UUID`. Text and location predicates inspect that single translation.
- `q` remains exactly Title, City, Municipality, and Neighborhood. Escaping remains backslash first, then `%`, then `_`, wrapped in `%...%`, and executed with PostgreSQL `ILIKE ... ESCAPE '\\'`; caller wildcards are literals. No q expansion is authorized.
- Every filter applies before count and page. Count and page derive from the same predicate. Parent rows are paged before child translations/images are hydrated.
- Ordering remains deterministic: selected sort followed by `CreatedAtUtc` and `Id` tie-breakers as currently defined.
- `PropertyType` is exactly Apartment=1, House=2, Commercial=3, Land=4. `CommercialType` is Unknown=0, Office=1, Shop=2, Other=3. `LandType` is Unknown=0, BuildingPlot=1, AgriculturalLand=2, Other=3.
- Exactly one matching subtype child remains the valid aggregate invariant, but it is not a cross-table database constraint; malformed persisted rows are possible and public/filter logic must not infer root identity from a child alone. `Unknown` is a valid persisted Draft and Active subtype.
- Comparables remain Active/public eligible, equal in root `PropertyType`, subtype-neutral, currency/location/price/area constrained as currently implemented, ranked by the current six keys, and limited only after ordering.
- Listing-parent serialization, post-lock aggregate authorization, lifecycle/readiness behavior, and stable failure ordering/codes remain unchanged.
- The CQRS-lite controller -> handler -> repository interface -> repository -> DbContext -> PostgreSQL flow remains intact.

## 5. Current implementation truth

The public discovery path is `src/RealEstate.Api/Controllers/ListingsController.cs` (`GetListings`) -> `GetListingsQuery` / `GetListingsHandler` / `GetListingsValidator` under `src/RealEstate.Application/Listings/Queries/GetListings/` -> `IListingRepository.GetFilteredReadOnlyAsync` -> `src/RealEstate.Infrastructure/Persistence/Repositories/ListingRepository.cs` -> `RealEstateDbContext` -> PostgreSQL.

All current discovery filters are optional scalars. Apartment and House subtype predicates live in `ListingRepository.ApplyPropertyDetailFilters` and currently test only the matching dependent row; Chapter 15 preserves that behavior. A dependent row does not prove root identity at the persistence boundary: current PostgreSQL keys permit a mismatched root and additional subtype children, as `ListingPersistenceTests.Can_round_trip_dormant_commercial_and_land_enum_names` demonstrates. The new Commercial/Land filters therefore require explicit root equality as well as the matching dependent row/type. Contradictory filters compose with ordinary AND semantics and return an empty page rather than validation failure. `CommercialType` and `LandType` already exist in the Domain and in authoring/read DTOs, but are absent from the discovery request, validator, controller, repository predicates, OpenAPI parameters, and representative QueryReview data.

`GET /api/agencies/{id}/listings` intentionally accepts a reduced vocabulary (`lang`, `sort`, `currency`, `page`, `pageSize`). `GetAgencyListingsHandler` still delegates to the shared filtered repository path with `AgencyId`. Rich discovery for one agency is already available through `GET /api/listings?agencyId=...`.

`GET /api/agencies/{id}/dashboard/listings` accepts nullable `ListingStatus`, but unlike public discovery enum filters its application query lacks explicit `Enum.IsDefined` defense. At HTTP boundaries, the ASP.NET Core nullable-enum binder rejects both malformed symbols and undefined non-flags numeric values before controller/handler execution. The concrete gap is limited to `GetAgencyDashboardListingsQuery` instances constructed directly in code: an undefined value can currently reach repository execution. Defense against that internal path is the one bounded hardening concern accepted into scope.

Comparables are implemented in `GetComparableListings` application code and `ListingRepository.GetComparableListingsReadOnlyAsync`. No current source or durable product authority defines subtype-aware valuation behavior.

QueryReview is reliable but its generation assumptions are static across `Program`, `DeterministicProfileSeeder`, `ProfileInvariants`, `QueryShapeDefinitions`, `ProductionCommandCaptureInterceptor`, `EnvironmentSnapshotCollector`, `ExplainRunner`, and `BaselineEvidenceWriter`. Profile/version terminology is also inconsistent: the current seeder is `chapter-10f-v2`, the logical run ID remains `chapter-10f-v1-production-sql`, and writer messages still refer to v1. The writer can stage and replace its fixed destination, so successor routing must make the historical evidence path impossible to overwrite.

## 6. Final Chapter 15 scope

The final scope consists of the thirteen task specifications in section 20: twelve mandatory tasks and one measurement-gated conditional index task. Core execution has no unresolved product blocker.

The plan makes these decisions:

- Commercial/Land subtype filters are scalar, optional, exact-value filters on general public discovery only, and each requires explicit matching root equality plus matching child/type.
- Existing compositional AND semantics and Apartment/House behavior are preserved.
- The public agency route and private/management listing vocabularies are not broadened.
- Comparables deliberately remain root-only and subtype-neutral.
- A synthetic, reproducible 100,000-row four-root stress scenario is accepted as engineering evidence, not market truth.
- QueryReview uses shared mechanics with two explicit generation definitions and separate immutable evidence destinations.
- PostgreSQL 16 remains the authoritative performance lane; 18.4 receives a bounded final compatibility lane.
- No production subtype index is assumed. `NO_INDEX` is a successful and complete outcome if no candidate passes.

## 7. Explicit exclusions

Chapter 15 excludes generic amenities/features, EAV, JSON attribute bags, dynamic/admin taxonomy, additional root types, speculative Commercial/Land fields, guessed common-field applicability rules, q expansion, PostGIS/radius/polygon/viewport work, public pin obfuscation, auth/JWT/config hardening, background jobs/notifications, media redesign, agency role/workspace redesign, generic repository/Unit of Work, MediatR, AutoMapper, architecture rewrite, global ETags/optimistic concurrency, frontend code, generated clients, final backend/frontend reconciliation, and broad documentation consolidation.

It also excludes subtype-aware comparable implementation without a later approved product rule, remediation of `CH13-PERF-01`, and unrelated quality-register work.

## 8. Subtype discovery architecture

### Final contract

1. `commercialType` binds to nullable scalar `CommercialType?`.
2. `landType` binds to nullable scalar `LandType?`.
3. Neither requires `propertyType`.
4. Each subtype predicate independently selects its root through an explicit conjunction. Commercial is `PropertyType == Commercial` **and** matching `CommercialDetails.CommercialType`; Land is `PropertyType == Land` **and** matching `LandDetails.LandType`. Child presence alone is never treated as proof of root identity.
5. All supplied filters compose under AND semantics. A contradictory root/subtype combination is valid input and returns a normal empty page.
6. Supplying both subtype families is valid input and returns a normal empty page because the predicates require mutually exclusive root equalities. This remains empty even if malformed persisted data contains both subtype children.
7. `Unknown` is an exact, filterable persisted value, not an alias for omitted input.
8. Existing `apartmentType` and `houseType` behavior is preserved exactly; Chapter 15 does not harmonize or break it.
9. Only general `GET /api/listings` gains the two parameters.
10. Public agency, personal/my, and dashboard/management listing routes gain no new discovery vocabulary.

The request remains scalar because there is no multi-value precedent and no product requirement for OR-within-family behavior. Repository predicates are conditional root-plus-child equality predicates inside `ApplyPropertyDetailFilters`; count and page continue to share the composed `IQueryable`. Apartment/House predicates are not changed by this correction.

### Examples

| Request | Required outcome |
|---|---|
| `GET /api/listings?commercialType=Office` | 200; Active rows with `PropertyType=Commercial` and exact Office detail only; a mismatched non-Commercial row carrying an Office child is excluded |
| `GET /api/listings?propertyType=Commercial&commercialType=Office` | same result set as the preceding request for the same remaining inputs |
| `GET /api/listings?propertyType=Land&commercialType=Office` | 200; valid empty page, `totalCount=0` |
| `GET /api/listings?commercialType=Office&landType=BuildingPlot` | 200; valid empty page, `totalCount=0`, including when a malformed row carries both children |
| `GET /api/listings?commercialType=Unknown` | 200; exact persisted Commercial `Unknown` values only |
| `GET /api/listings?landType=0` | numeric compatibility retained; exact persisted Land `Unknown` values only |
| `GET /api/listings?commercialType=Warehouse` | canonical model-binding 400 validation problem |
| `GET /api/listings?landType=999` | canonical automatic model-state 400 keyed to `landType`; controller/handler/application validation is not reached |

Defined symbolic and defined numeric representations remain accepted because current ASP.NET enum binding supports both. Malformed symbolic and undefined numeric HTTP values are both rejected by automatic model-state validation before controller/handler execution. Empty nullable query tokens retain normal absent/null binding behavior. Directly constructed application queries still undergo explicit `Enum.IsDefined` validation as defense in depth. OpenAPI advertises symbolic string enum values, not numeric compatibility.

## 9. Agency-route decision

**Decision: option A — subtype filters exist only on general public search.**

The agency route remains a deliberately small browsing surface. Clients that require agency plus subtype composition use `/api/listings?agencyId={id}&commercialType=...` or `landType=...`; that route already uses the same repository and public mapping. Broadening `/api/agencies/{id}/listings` merely for symmetry would enlarge a contract that currently and intentionally omits root, text, location, price, and detail filters.

Integration and OpenAPI tests must assert both sides: subtype parameters work on `/api/listings`; they are absent from the agency operation. Existing behavior in which unsupported agency query keys are ignored remains unchanged and documented as compatibility behavior, not promoted as a general API feature.

## 10. Comparable disposition

**Decision: preserve root-only, subtype-neutral comparables for all of Chapter 15.**

This is a deliberate completed scope decision, not unfinished Chapter 15 work. Source must be Active and have a usable effective language/city plus positive price/area. Candidates must be Active, have a different ID, equal `ListingType`, equal root `PropertyType`, equal `Currency`, positive price/area, and the same selected effective language and literal case-insensitive city as the source. The six ranking keys remain, in order: (1) municipality/neighborhood location tier, (2) absolute relative area difference, (3) absolute relative unrounded price-per-square-meter difference, (4) absolute relative price difference, (5) `CreatedAtUtc` descending, and (6) `Id` descending. `Take(limit)` remains after ranking and before detail/translation/image hydration.

There is no conditional implementation task in the core graph. A future owner/product-approved rule must specify at least source applicability, candidate compatibility, Unknown/Other treatment, fallback behavior, ranking-versus-exclusion semantics, and behavior across all roots. That later rule requires a new plan amendment or later chapter; it cannot be inferred from taxonomy names.

## 11. Four-root profile architecture

The successor profile is a deterministic engineering workload named `four-root-discovery-v1`. Its percentages are synthetic selectivity bands, not claimed North Macedonian market shares and not production analytics. One fixed scenario is preferable to an unbounded collection of profiles because it keeps result/order locks, before/after plans, and index comparisons reproducible.

### Locked population model

| Dimension | Required successor profile |
|---|---|
| Listings / translations | 100,000 listings; exactly two canonical translations each (200,000 total) |
| Root distribution | 40,000 Apartment; 30,000 House; 20,000 Commercial; 10,000 Land |
| Commercial subtype bands | 8,000 Unknown; 6,000 Office; 4,000 Shop; 2,000 Other |
| Land subtype bands | 4,000 Unknown; 3,000 BuildingPlot; 2,000 AgriculturalLand; 1,000 Other |
| Lifecycle per root/subtype | 70% Active; 6% each Draft, Archived, Reserved, Sold, and Rented, applied within every Commercial/Land subtype band as well as each root |
| Ownership per root/subtype | 50% personal; 50% agency within every root and new subtype band; deterministic agency cohorts include common and sparse agencies |
| Listing type per root/subtype | 50% Sale; 50% Rent within every root and new subtype band |
| Currency | preserve exact keyed totals: EUR=33,334; USD=33,333; MKD=33,333; distribute each deterministically across all roots/subtypes |
| Images | preserve the 60,000-image total and deterministic zero/one/multiple-image cohorts |
| Geography | retain exact protected city/municipality/neighborhood cohorts and distribute bulk rows across roots/subtypes |
| Price, area, rooms, age | retain deterministic ranges, nullability rules, and tie cohorts; interleave root/subtype values across creation time so ordering is not root-clustered |
| Search and comparables | preserve literal-wildcard q cohorts, effective-translation/location cohorts, and the C1 comparable source/candidate ranking cohort |

The Apartment/House data is reduced only outside protected cohorts. Existing shape inputs, selected row IDs, totals where feasible, order, q/location cases, and comparable cluster must remain locked. If a legacy semantic result cannot remain exact because the profile changed, implementation must stop and document the precise dataset-only reason before any successor acceptance; it may not silently update a hash.

The exact profile invariant manifest must assert root/subtype counts, exactly one matching detail row, detail mismatch count zero, status/ownership/listing-type/currency/translation/image counts, geography and search cohorts, selected page IDs, and comparable rank order. The exact invariant count is owned by the successor definition and becomes immutable when the profile task is accepted; it is not forced to remain the historical 61.

Required query coverage includes Unknown, medium, and rare subtype bands; root-plus-subtype equivalence; cross-family empty intersection; count and root page; selected-ID child hydration; shallow and deep pages; newest and price ordering where applicable; location plus subtype; and `agencyId` plus subtype through general public search. No shape may imply that the reduced agency route gained the parameters.

## 12. QueryReview successor architecture

Three designs were considered.

| Design | Assessment |
|---|---|
| A. Narrowly add constants to the existing static tooling | Small initial diff, but continues the current profile/run/version mismatch and makes historical destination replacement too easy |
| B. Duplicate a separate successor tool | Strong isolation, but duplicates capture, replay, safety, environment, hashing, and credential logic and invites drift |
| C. Shared mechanics plus explicit generation definitions | Chosen: isolates historical and successor contracts without creating a generic benchmark framework |

The chosen design has exactly two explicit generation definitions:

- a frozen historical definition that verifies the accepted Chapter 10F/13/14 artifacts and can never replace their permanent directory; and
- `four-root-discovery-v1`, which owns profile version, accepted server lane, invariant manifest, query-shape/command/parameter/sequence manifest, result/order locks, acceptance gates, and a fixed successor evidence destination.

Shared code remains responsible for disposable-database safety, EF command interception, exact typed-parameter capture, normalization, replay/EXPLAIN, environment snapshots, hashing, credential scanning, staging, and offline verification. The design is a two-definition dispatch, not a plugin system, arbitrary manifest language, or general benchmark framework.

The successor permanent destination root is fixed and allowlisted as `docs/benchmarks/four-root-discovery-v1/evidence`, with independently allowlisted atomic destinations `postgresql-16/` and `postgresql-18.4/`. Export stages and replaces only the selected lane directory; it must hash and preserve the sibling lane before/after. The historical `docs/benchmarks/chapter-10f/evidence` directory is verify-only. Unit tests must prove that no successor identifier or destination can select, stage over, back up, or replace the historical path or an unselected successor lane.

Online commands select a generation explicitly, for example `--profile four-root-discovery-v1`. Offline verify/export infer and validate the recorded generation and reject a caller/profile mismatch. Safety expectations remain exact by lane: PostgreSQL 16 uses the accepted exact image/storage rules; the bounded 18.4 lane adds its own exact image-declared storage-layout rules rather than relaxing the verifier generically.

The PostgreSQL 18.4 permanent export has one narrow additional offline input: `--comparison-run-dir <contemporaneous-pg16-run>`. That option is mandatory for the 18.4 lane, forbidden for the 16 lane, accepts no destination, independently verifies both sealed raw runs, requires matching source/profile/shape/settings hashes and exact SQL/parameter/result/order identities, and embeds the curated 16 comparison sub-bundle in the atomically staged 18.4 lane. This is not a general multi-run exporter.

Successor readiness is staged and fail-closed. After 15D, all successor database/capture/export operations report “not provisioned.” 15E enables only profile create/verify. 15F enables capture, baseline run, and raw/curated offline verify. Permanent lane export remains disabled until 15J binds the final migration count, 15H disposition identity, source/profile/shape hashes, and final gates; that one finalization enables both fixed lane destinations. No intermediate task may relabel historical or incomplete data as successor evidence.

Experimental artifacts never use permanent export. Raw runs are written outside the repository. The existing `baseline verify --run-dir <raw-run>` operation already materializes `<raw-run>/curated`; 15D makes that output a self-manifesting experimental bundle and teaches the same offline verifier to validate such a bundle directly, without adding an arbitrary output-path option. 15G/15H copy `<raw-run>/curated` once into an absent content-addressed directory under `docs/benchmarks/four-root-discovery-v1/experiments/`, then re-verify the tracked bytes and record file/hash inventory. Overwrite/replacement is forbidden. Permanent and experimental manifests are distinct, and the verifier rejects the wrong artifact class or expected path.

Legacy shape identities `N1`, `P1`, `P2`, `A1`, `R1`, `L1`, `Q1`, and `C1` retain an explicit cross-generation mapping. The exact new shape inventory is locked below; implementation may not add, remove, or rename a shape without amending this plan.

| Shape ID | Locked query essence | Primary proof |
|---|---|---|
| `commercial-unknown-first-page` | `commercialType=Unknown`, newest, page 1 x 20 | nonselective/Unknown exactness |
| `commercial-office-first-page` | `commercialType=Office`, `currency=EUR`, `sort=priceAsc`, page 1 x 20 | explicit Commercial root isolation without a supplied root parameter |
| `commercial-office-root-first-page` | preceding inputs plus `propertyType=Commercial` | exact result/order equivalence with preceding shape |
| `commercial-shop-location` | `commercialType=Shop` plus locked city/municipality, newest, page 1 x 20 | medium selectivity/effective translation |
| `commercial-other-deep-page` | `commercialType=Other`, newest, page 25 x 20 | rare/deep paging |
| `land-unknown-first-page` | `landType=Unknown`, newest, page 1 x 20 | nonselective/Unknown exactness |
| `land-building-plot-first-page` | `landType=BuildingPlot`, `currency=EUR`, `sort=priceDesc`, page 1 x 20 | explicit Land root isolation without a supplied root parameter |
| `land-building-plot-root-first-page` | preceding inputs plus `propertyType=Land` | exact result/order equivalence with preceding shape |
| `land-agricultural-location` | `landType=AgriculturalLand` plus locked city/municipality, newest, page 1 x 20 | medium selectivity/effective translation |
| `land-other-deep-page` | `landType=Other`, newest, page 25 x 20 | rare/deep paging |
| `agency-commercial-shop-first-page` | locked `agencyId` plus `commercialType=Shop`, newest, page 1 x 20 | rich general-search agency composition |
| `agency-land-agricultural-deep-page` | locked `agencyId` plus `landType=AgriculturalLand`, newest, page 10 x 20 | agency/subtype deep page |
| `cross-family-subtypes-empty` | `commercialType=Office` plus `landType=BuildingPlot`, page 1 x 20 | valid empty AND composition from mutually exclusive explicit root predicates |

Every nonempty shape locks count, root page, translation hydration, and image hydration roles. The empty shape locks the exact roles the real repository emits and must include count and root-page execution. These 13 new shapes plus the eight mapped legacy shapes are the complete successor inventory.

Historical anchors remain immutable: 69 files; measurements SHA-256 `d6dac6f58245f7ecd65b626ca1c3b85df2a2d39808a350e8b1536b82779f5a17`; measurements Git blob `051b93a9b19dbdbcac6a3411afa4636b3b191ad2`; the accepted aggregate result hash; and Chapter 14's exact/delta proof.

## 13. Index/measurement policy

**Policy: NO INDEX BEFORE MEASUREMENT.** A subtype query parameter alone needs no migration. Candidate DDL may exist only in fresh disposable database clones until an accepted disposition record names a winner.

The mandatory order is: implemented semantics -> accepted successor profile/shapes -> unindexed PostgreSQL 16 capture -> candidate experiments -> disposition -> conditional migration -> final proof.

Every unindexed/candidate comparison uses byte-identical hashes for the complete production-query, profile, QueryReview, build-input, and executable source set; evidence-only descendant commits are allowed, and 15H must prove that none of those hashes changed since 15G. It also uses the same profile hash, PostgreSQL 16 image digest, database settings, statistics preparation (`VACUUM (ANALYZE)` or the tool's locked equivalent), one discarded warm-up, five measured samples, command order, and parameter values. For each count and page-root command it records median execution time, shared hit/read/dirtied/written blocks, temp/spill evidence, plan node/index use, estimated versus actual rows, and returned count/IDs/order. It also records index DDL, method/columns/order/predicate, build duration, index bytes, dependent-table heap bytes, and a fixed insert plus subtype-update write probe.

A candidate qualifies only when all of these predeclared gates pass:

1. Generated SQL, exact typed parameters, count, result IDs, and order hashes are byte-for-byte identical to the unindexed run.
2. The intended candidate index is used in the declared selective family; there is no unstable plan switching across measured samples.
3. For at least one declared selective subtype family, the filtered count, shallow page-root, and deep page-root commands each reduce median shared-access blocks by at least 25% and median execution time by at least 20%.
4. No protected legacy or nonselective successor command increases shared-access blocks by more than 10%; no sequence is both more than 20% and more than 2 ms slower.
5. There is no spill, temp I/O, missing plan, capture anomaly, invalid index, or worse estimate/actual-row divergence attributable to the candidate.
6. Neither fixed write probe is both more than 10% and more than 2 ms slower, and candidate bytes do not exceed the dependent table's heap bytes. A result outside either bound is rejected rather than rationalized after measurement.

Candidates are not predetermined as simple, composite, or partial. Their DDL is chosen only after the unindexed plans reveal the limiting predicate/order path. Each candidate is measured alone on an identically rebuilt disposable database. If no candidate satisfies every gate, the disposition is `NO_INDEX`, migration count remains 21, and Chapter 15 proceeds successfully.

Candidate scope is capped at two configurations for Commercial and two for Land, plus one final combined-winner confirmation when both tables independently qualify: five candidate runs maximum. Full self-manifesting curated evidence is retained for every run. If the unindexed plans cannot support a defensible disposition within this cap, 15H stops for a plan amendment rather than trying unbounded combinations in one task.

## 14. SQL/performance acceptance model

PostgreSQL 16 is the authoritative correctness, historical-comparison, before/after, and index-decision lane. PostgreSQL 18.4 is a bounded final compatibility/performance-observation lane because it is the tracked development/runtime image. Absolute timings and plan JSON are not compared across major versions as an SLA.

Evidence is classified as follows.

| Class | Acceptance rule |
|---|---|
| Historical Chapter 10F/13/14 artifacts | Immutable and still verified by existing hashes; never regenerated into the old path |
| Existing shapes with null new filters | Chapter 14 command SQL and all 80 typed parameters remain exact; a source-file hash change alone is expected |
| Existing shapes on successor data | Semantic identity is mapped; result/order hashes remain exact when protected cohorts permit. Dataset-driven totals/plans must be separately versioned and explained |
| New subtype shapes | New command identities, predicates, enum parameters, command/parameter/plan totals, results, and order are intentional; all become exact under the successor manifest |
| Unindexed versus candidate/indexed | SQL, typed parameters, results, and order exact; only index catalog, plan, buffers, and timing may change |
| PostgreSQL 16 versus 18.4 | SQL, typed parameters, results, and order exact; lane-qualified plans/metrics may differ |

The successor aggregate result hash and every per-shape order hash are namespaced by generation/profile and cannot replace the historical hash. The final proof inventories source blobs/hashes, profile hash, environment/image digest, commands, typed parameters, result and order hashes, plans, buffers, timings, spills, and index catalog.

A regression is any unexplained old-shape SQL or parameter drift; q field/escaping drift; effective-translation, Active-only, ordering, paging, or child-hydration-role drift; missing/extra command; count/result/order drift; incomplete capture; spill/temp anomaly; or failure of a locked gate. Accepted intentional deltas are new conditional subtype predicates/commands, declared dataset/statistics-driven plan changes, and post-disposition catalog/plan changes.

The PostgreSQL 18.4 lane hard-fails on SQL/parameter/result/order drift, errors, incomplete capture, spill/temp I/O, or missing/invalid catalog state. Its performance reference is a contemporaneous PostgreSQL 16 companion run on the same host/session/source/profile/settings, whose correctness hashes must first equal 15J. It stops for owner review if a protected or subtype sequence is both more than 25% and more than 2 ms slower than that companion median, or uses more than 20% additional shared-access blocks. Such a review records the major-version explanation; it does not rewrite the PostgreSQL 16 acceptance threshold.

## 15. API/OpenAPI contract policy

The new request members are nullable enum scalars in `GetListingsQuery`, controller query binding, validator, and generated OpenAPI. At HTTP boundaries, symbolic and defined numeric values bind; malformed symbolic and undefined numeric values both fail through the canonical `[ApiController]` model-state 400 and neither reaches the controller/handler. `GetListingsValidator` retains `Enum.IsDefined` for supplied values as defense in depth when application code constructs a query directly; such invalid direct queries fail before repository execution.

The repository adds conditional explicit-root-plus-dependent-row predicates: Commercial root equality and matching Commercial child/type, or Land root equality and matching Land child/type. The same composed source query feeds count and ordered page materialization. Public mapping, response DTOs, route, status codes, pagination envelope, effective translation, unfiltered reads, and child hydration are unchanged.

Generated OpenAPI must advertise `commercialType` and `landType` only on `GET /api/listings`, as nullable string enums with exactly the accepted symbolic values. It must continue to omit them from the public agency, personal, and dashboard routes. No custom binder is introduced.

The bounded dashboard hardening adds explicit defined-value validation for a directly constructed `GetAgencyDashboardListingsQuery` after the existing principal/account/agency-access checks and before listing-repository execution. It prevents internal/non-HTTP callers from forwarding `(ListingStatus)999` to the repository. It does not change HTTP enum binding, authentication middleware behavior, valid-input authorization/repository ordering, filters, or valid-query SQL.

## 16. Migration policy

Subtype parameters, QueryReview work, profile data, and dashboard validation require no migration. Historical migrations remain immutable.

A new migration is permitted only if the candidate disposition names a passing production index. Then the conditional task adds one behavior-named EF configuration and one new immutable migration. If it is the next migration, inventory moves from 21 to 22. Acceptance requires a fresh apply, Down then re-Up, catalog assertions for method/columns/order/predicate/readiness/validity, data preservation, zero pending model changes, and final plan evidence proving index use where intended.

If disposition is `NO_INDEX`, there is no empty migration, snapshot churn, or placeholder migration; Chapter 15 closes at 21 migrations.

## 17. Quality-handoff ownership

The ten live entries remain open. Chapter 15 does not mark any resolved.

| Entry | Chapter 15 ownership |
|---|---|
| `QH-TX-01` transaction cleanup exception replacement | Untouched; not a discovery/performance dependency |
| `QH-TEST-01` agency concurrency task draining | Untouched; Chapter 15 read-only filter work changes no race boundary |
| `CH11-DB-01` nullable creator relationship | Untouched; profile data must honor the existing model, not change its policy |
| `CH11-DB-02` broad DB constraint policy | Untouched; a measured index is not validation-constraint expansion |
| `CH11-STATE-01` broad state/concurrency freshness | Untouched; no global freshness design is introduced |
| `CH11-FILE-01` durable physical deletion | Untouched; media lifecycle is excluded |
| `QH-TEST-02` raw-SQL test fixture | Untouched; QueryReview deterministic seeding does not remediate integration fixture policy |
| `C12-CONFIG-01` JWT/config deployment hardening | Untouched and explicitly out of scope |
| `CH13-J2-DEPLOY-01` active-location deployment gate | Untouched; current confirmed-location contracts remain protected |
| `CH13-PERF-01` translation-trigger write amplification | Untouched. Profile rebuilds and subtype-index write probes are not an operational translation workload and must not claim to resolve or measure this item |

Any new issue discovered during implementation is added truthfully through normal quality-handoff rules; it is not hidden inside Chapter 15 acceptance.

## 18. Test strategy

Tests use behavior/domain names, never chapter-number runtime names. Required coverage includes:

- HTTP integration cases for both subtype filters cover all defined values including Unknown, defined symbolic/numeric forms, and automatic model-state 400 for malformed symbols and undefined numerics; validator unit cases separately prove `Enum.IsDefined` defense for directly constructed invalid queries;
- PostgreSQL discovery cases prove explicit Commercial/Land root isolation, matching root equivalence, contradictory root/subtype, both-family empty intersection, malformed nonmatching-root children and malformed dual-child rows, count/page consistency, shallow/deep pages, ordering, and combinations with agency/location/q;
- Apartment/House subtype regression, Active-only truth, effective translation, literal q wildcard escaping and unchanged four-field q set, parent-page-before-child hydration, four-root detail mapping, and deterministic order;
- generated OpenAPI presence/value truth on general search and absence on public agency/private/management operations;
- dashboard HTTP cases prove existing automatic model-state behavior for malformed/undefined status and existing authentication precedence; validator/direct-handler cases prove direct-query defense after principal/account/agency access and no listing-repository call on validation failure; valid-input authorization/repository ordering remains exact;
- comparable root isolation and existing six-key result order, with Commercial/Land subtype differences deliberately ignored;
- QueryReview unit tests for generation selection, exact lane safety, frozen historical routing/hashes, manifest validation, command/parameter/result/order locks, credential rejection, and offline verification;
- PostgreSQL profile create/verify, SQL capture, result/order locks, plan/performance gates, and migration lifecycle/catalog tests only if a schema change is selected.

No new concurrency test is required: neither read-only discovery nor validation changes a concurrency boundary. Existing authoring/lifecycle/concurrency suites run at the cumulative gate as protected regressions.

## 19. Implementation/audit workflow

Each task is implemented on the owner-controlled branch, handed off with its ignored `docs/planning/` evidence record, owner-committed in the stated commit count, and independently audited before its dependents proceed. Audits inspect the committed immutable source, not an uncommitted working tree. Production/test/runtime names must be behavior-based; planning identifiers may appear only in planning/history/evidence documents.

The implementer must stop rather than broaden a task when a product rule, unexpected schema change, historical evidence drift, or failed measurement gate appears. Audit remediation stays inside the same task only when it preserves the primary concern and two-commit cap; otherwise the plan is amended.

The documentation-status correction is intentionally first and documentation-only. Profile/tool foundation may proceed after plan approval, but the successor shape manifest cannot be accepted before subtype semantics are implemented. Performance work cannot precede accepted profile and shapes. The cumulative gate consumes one immutable assembled source. Closeout follows, never precedes, cumulative `FINAL_PASS`.

## 20. Final task sequence

### 15A — Correct the durable Chapter 14 status lag

#### Title

Correct accepted taxonomy release status

#### Purpose

Make the two current authority documents acknowledge the already-completed owner acceptance, merge, and tag without rewriting technical history.

#### Why this task exists

`docs/backend-context.md` and `docs/chapters/chapter-14-property-model-taxonomy-expansion.md` still contain self-non-accepting 14M wording. Git history and `backend-property-model-taxonomy-v1` now supersede it.

#### Depends on

Owner approval of this Chapter 15 plan only.

#### Exact scope

- Change only stale status/next-step sentences in the two authority documents.
- Record the accepted closeout commit/merge/tag relationship and retain all original Chapter 14 technical facts.
- Point the next backend boundary to approved Chapter 15 execution.

#### Explicit non-goals

No code, broad documentation refresh, evidence regeneration, frontend handoff edit, or retroactive change to Chapter 14 task results.

#### Expected files/layers

- Domain: none.
- Application: none.
- Infrastructure: none.
- API: none.
- Tests: none.
- QueryReview: none.
- Migrations: none.
- Docs/evidence: `docs/backend-context.md`, `docs/chapters/chapter-14-property-model-taxonomy-expansion.md`, and the ignored task evidence file.

#### Domain impact

None.

#### Application impact

None.

#### Persistence/migration impact

None; inventory remains 21.

#### API/OpenAPI impact

None.

#### Query/performance impact

None; historical evidence stays immutable.

#### Authorization/concurrency impact

None.

#### Protected regressions

Do not alter Chapter 14 enum values, migration/test/evidence totals, task conclusions, or historical chronology.

#### Tests required

No executable tests. Verify the tag/commit facts directly and inspect the exact documentation diff.

#### Verification commands

```powershell
git show --no-patch --decorate backend-property-model-taxonomy-v1
git merge-base --is-ancestor 4e9499fc077f3e7b238177c5263b93aacb295921 backend-property-model-taxonomy-v1
rg -n "14M|pending|backend-property-model-taxonomy-v1|Chapter 15" docs/backend-context.md docs/chapters/chapter-14-property-model-taxonomy-expansion.md
git diff --check
git diff -- docs/backend-context.md docs/chapters/chapter-14-property-model-taxonomy-expansion.md
```

#### Evidence file

`docs/planning/chapter-15a-authority-status-correction-evidence.md`

#### Expected commit count

**Expected owner commits: 1**

#### Completion criteria

Both documents state the later acceptance/merge/tag truth, preserve technical history, and contain no new implementation claim.

#### Independent audit focus

Commit ancestry, tag target, minimal diff, no changed historical totals, and no frontend handoff change.

#### Stop conditions

Stop if repository history does not prove a proposed sentence or if correction would require reinterpreting an accepted result.

### 15B — Add exact Commercial/Land subtype discovery

#### Title

Add scalar Commercial and Land subtype filters to public discovery

#### Purpose

Implement the contract in section 8 end to end as one atomic API/query change.

#### Why this task exists

The four-root Domain, persistence, authoring, and response models exist, but general discovery has subtype filters only for Apartment and House.

#### Depends on

15A.

#### Exact scope

- Add nullable scalar `CommercialType` and `LandType` members to `GetListingsQuery` and matching `commercialType`/`landType` controller parameters.
- Use existing ASP.NET enum binding without a custom binder. Retain `Enum.IsDefined` with stable field keys for directly constructed `GetListingsQuery` values as application defense in depth.
- Add exact root-plus-child predicates in `ListingRepository.ApplyPropertyDetailFilters`: Commercial root equality and matching Commercial child/type; Land root equality and matching Land child/type.
- Preserve conditional predicate composition so null inputs do not alter old queries.
- Assert defined symbolic/numeric/Unknown semantics, HTTP automatic model-state failures for malformed/undefined values, direct-query validator failures, explicit root isolation/equivalence, contradictions, both-family empty result, filter-before-count/page, ordering, and generated OpenAPI.
- Reuse `ListingTestHelpers.SeedDormantSubtypeDetailsAsync` or an equivalent repository-grounded malformed fixture with a nonmatching root plus Commercial/Land children; prove neither subtype filter leaks it and both filters remain empty even when both children exist.
- Assert subtype parameters remain absent on agency/private/management operations and Apartment/House behavior remains exact.
- Add a direct public-agency regression proving `commercialType` and `landType` query keys remain unsupported/ignored and do not alter its response; do not rely only on the existing ignored-`q` test.
- Keep new public integration methods in the behavior-named `CommercialAndLandSubtypeDiscovery` family and the agency regression in `GetAgencyListings_SubtypeQueryParameters`, so focused selection is stable without chapter-number test names.

#### Explicit non-goals

No multi-value filters, special contradiction validation, agency-route expansion, q change, comparable change, new DTO detail, index, migration, database constraint, unfiltered-read change, Apartment/House semantic change, QueryReview baseline, or generic filter abstraction.

#### Expected files/layers

- Domain: none; reuse accepted enums.
- Application: `GetListingsQuery.cs`, `GetListingsValidator.cs`, and only directly affected discovery tests/handler plumbing if required.
- Infrastructure: `ListingRepository.cs`.
- API: `ListingsController.cs`; generated contract tests, not hand-authored OpenAPI documents.
- Tests: validator, public PostgreSQL integration, agency regression, and `OpenApiDocumentTests.cs`.
- QueryReview: none in this task.
- Migrations: none.
- Docs/evidence: ignored task evidence only.

#### Domain impact

None.

#### Application impact

Two optional enum request members plus defense-in-depth validation for directly constructed queries; public response models remain unchanged.

#### Persistence/migration impact

Query-only. Existing shared-PK child tables and PK indexes are used; migration count remains 21.

#### API/OpenAPI impact

`GET /api/listings` gains two nullable symbolic enum query parameters. No other operation changes.

#### Query/performance impact

Non-null filters add explicit root equality and matching child existence/type equality to both count and page. Null filters must leave old generated SQL/parameters exact. Representative performance acceptance is deferred to 15G onward.

#### Authorization/concurrency impact

None; route remains anonymous and read-only.

#### Protected regressions

Active-only truth, effective translation, q fields/escaping, current filters, page/count agreement, deterministic order, parent-page hydration, Apartment/House semantics, response detail mapping, and agency-route vocabulary.

#### Tests required

- Unit: validator defined/Unknown cases plus cast undefined values on directly constructed queries, proving failure before repository execution.
- HTTP/PostgreSQL integration: all contract examples and combinations above, automatic model-state 400 for both malformed symbols and undefined numerics, explicit root isolation against mismatched/dual-child persisted rows, paging/order, and four-root response details.
- OpenAPI: exact symbolic values/presence on general search and absence elsewhere.
- Manual smoke: through generated Swagger/browser against a disposable local API, exercise one Commercial success, one Land Unknown, one contradiction/empty page, one malformed-symbol automatic 400, one undefined-numeric automatic 400, and agency-route absence/ignored behavior; record requests/responses without credentials.
- Migration/concurrency: none.
- SQL/result/order/plan: later successor tasks; old null-filter SQL lock is asserted there.

#### Verification commands

```powershell
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~GetListingsValidatorTests|FullyQualifiedName~RootTypeDiscovery|FullyQualifiedName~CommercialAndLandSubtypeDiscovery|FullyQualifiedName~GetAgencyListings_SubtypeQueryParameters|FullyQualifiedName~OpenApiDocument_PropertyTaxonomyContract_IsTruthful"
dotnet build RealEstate.slnx -c Release
dotnet test RealEstate.slnx -c Release
git diff --check
```

The focused command must first be listed/checked to ensure it selects a nonzero set; evidence records unique focused and full-suite totals separately.

#### Evidence file

`docs/planning/chapter-15b-subtype-discovery-evidence.md`

#### Expected commit count

**Expected owner commits: 1**

#### Completion criteria

Every section 8 example has a passing integration assertion; HTTP binding and direct-query validation are separately proven; malformed child rows cannot escape explicit root isolation; OpenAPI is truthful; no migration exists; valid old requests and the agency route remain unchanged; full Release build/suite pass.

#### Independent audit focus

Conflating HTTP binding with application validation, omitted explicit root equality, malformed mismatched/dual-child leakage, defined numeric and Unknown treatment, unintended OR semantics, count/page predicate drift, accidental agency parameter exposure, q drift, and hidden schema changes.

#### Stop conditions

Stop if implementation requires a custom binder, schema change, multi-value product decision, or behavior different from the locked compositional contract.

### 15C — Defend direct agency dashboard queries against undefined status

#### Title

Defend directly constructed agency dashboard queries against undefined status

#### Purpose

Apply the established nullable-enum validation pattern as application-layer defense in depth without changing HTTP binding behavior.

#### Why this task exists

HTTP binding already rejects malformed and undefined numeric `ListingStatus` values before controller/handler execution. The handler itself has no `Enum.IsDefined` defense, so code that directly constructs `GetAgencyDashboardListingsQuery` with `(ListingStatus)999` can pass that value to the listing repository after access checks.

#### Depends on

15A. It is technically independent of subtype discovery and may be implemented in parallel with 15B after plan approval.

#### Exact scope

- Add a behavior-named validator for `GetAgencyDashboardListingsQuery` and register it through existing explicit DI conventions.
- Invoke it only after the current principal, account, and agency-access checks and before the listing repository.
- For directly constructed invalid queries, return canonical application `validation.failed` with field key `status` and do not call the listing repository.
- Preserve existing automatic HTTP model-state 400 for malformed symbols and undefined numerics; add no custom binder and make no claim that the validator handles those HTTP failures.
- Advertise/verify the canonical 400 response in generated OpenAPI if not already present.
- Test absent/every defined value, both invalid HTTP forms, direct cast-undefined application input, no listing-repository call after successful access plus validation failure, and protected authentication/authorization ordering.

#### Explicit non-goals

No new dashboard filter, filter vocabulary reconciliation, status semantics change, role redesign, shared validation framework, SQL rewrite, or concurrency work.

#### Expected files/layers

- Domain: none.
- Application: agency dashboard query folder plus DI registration location.
- Infrastructure: none.
- API: `AgenciesController.cs` only if response mapping/metadata is required.
- Tests: dashboard validator/unit and endpoint/OpenAPI integration tests.
- QueryReview: none.
- Migrations: none.
- Docs/evidence: ignored task evidence only.

#### Domain impact

None; current `ListingStatus` values remain authoritative.

#### Application impact

One explicit request validator and stable direct-query failure conversion.

#### Persistence/migration impact

None. Valid request SQL remains unchanged; invalid directly constructed values no longer reach the listing repository.

#### API/OpenAPI impact

HTTP behavior is unchanged: malformed symbols and undefined numerics already produce canonical automatic model-state 400 before controller/handler execution, subject to existing authentication middleware precedence. OpenAPI gains truthful 400 metadata if absent. Valid/omitted behavior and response shape are unchanged.

#### Query/performance impact

None for valid requests; no new QueryReview shape is warranted.

#### Authorization/concurrency impact

Authorization precedence must remain unchanged. No concurrency boundary changes.

#### Protected regressions

HTTP authentication/model-binding behavior, direct-handler principal/account/agency-access ordering, 401/403/404 behavior, Owner/Agent access, valid status filtering, pagination/order, response detail mapping, and stable problem contract.

#### Tests required

Unit validator tests cast an undefined status into a directly constructed query. Direct-handler tests prove principal/account/agency access remains before validation and that, after access succeeds, validation failure prevents the listing-repository call. HTTP endpoint cases separately prove existing automatic model-state 400 for malformed/undefined input and existing authentication precedence; valid inputs protect repository ordering. Generated OpenAPI and a manual Swagger/browser smoke cover defined/undefined HTTP input plus unauthenticated precedence. No migration, plan, or new race test.

#### Verification commands

```powershell
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~GetAgencyDashboardListings|FullyQualifiedName~OpenApiDocument"
dotnet build RealEstate.slnx -c Release
dotnet test RealEstate.slnx -c Release
git diff --check
```

#### Evidence file

`docs/planning/chapter-15c-dashboard-status-validation-evidence.md`

#### Expected commit count

**Expected owner commits: 1**

#### Completion criteria

HTTP invalid-enum behavior remains automatic and unchanged; directly constructed undefined status returns the canonical application failure after protected access checks and never reaches the listing repository; valid SQL/order is unchanged; full build/suite pass.

#### Independent audit focus

Accidental custom binding or attribution of HTTP failures to the validator, validation preceding direct-handler access checks, listing-repository execution after direct-query validation failure, wrong error key/code, changed valid behavior, or broadened dashboard scope.

#### Stop conditions

Stop if the fix requires a shared validation rewrite, authorization reordering, or any database change.

### 15D — Isolate QueryReview generations and destinations

#### Title

Introduce explicit QueryReview generation boundaries

#### Purpose

Make historical and four-root runs selectable and structurally isolated while retaining proven shared mechanics.

#### Why this task exists

Current static constants span the entry point, seeder, invariants, shapes, capture, environment, explain, and writer. Version labels disagree, and the exporter can replace its fixed historical destination.

#### Depends on

15A. It may proceed in parallel with 15B; section 12 is the architecture authority.

#### Exact scope

- Add a small explicit generation definition/catalog for frozen historical and `four-root-discovery-v1` behavior.
- Register the successor with its identity, lanes, and destination but explicit unavailable capability states; until 15E/15F supply profile/shapes, affected commands must fail before database or permanent-filesystem access rather than use placeholders.
- Route profile selection, exact accepted PostgreSQL lane/image/storage rules, manifests, and expected environment facts through that definition.
- Preserve shared capture/replay/hash/credential mechanics.
- Make historical permanent export verify-only and successor export target a different fixed allowlisted destination.
- Reject unknown/mismatched profile/run/destination combinations before database or filesystem mutation.
- Parse and validate the lane-specific offline comparison option: required only for a future PostgreSQL 18.4 permanent export, rejected everywhere else, and incapable of choosing an output path.
- Move `ExplainRunner` and `BaselineEvidenceWriter` from global static expectations to the selected explicit definition without relaxing any historical gate.
- Preserve raw-run offline verification and add explicit offline verification of a committed permanent-evidence directory from its manifest/hashes, so cumulative gates never need a database or regeneration.
- Add an experimental artifact class to the existing verify-generated `<raw-run>/curated` output, including a complete relative-path/content-hash manifest, and support direct offline re-verification after that directory is copied unchanged; do not add an arbitrary export destination.
- Add routing, lane-safety, manifest, historical-hash/file-count, staging, and offline verify/export tests; update tool usage documentation.
- At task completion, prove every successor profile/create/verify/capture/run/verify/export command fails closed as not provisioned; only historical verification is operational.

#### Explicit non-goals

No generic plugin/manifest language, arbitrary destination flag, new profile data, new query shapes, production API change, baseline regeneration, index, or migration.

#### Expected files/layers

- Domain/Application/Infrastructure/API: none.
- Tests: `tests/RealEstate.Tests/Unit/QueryReview/` and narrow fixture support.
- QueryReview: `Program.cs`, safety/options/environment/measurement models, `ExplainRunner.cs`, `BaselineEvidenceWriter.cs`, and small behavior-named generation definition/catalog files.
- Migrations: none.
- Docs/evidence: QueryReview usage documentation and ignored task evidence; no permanent baseline output.

#### Domain impact

None.

#### Application impact

None.

#### Persistence/migration impact

Only disposable QueryReview databases. No EF model change.

#### API/OpenAPI impact

None.

#### Query/performance impact

No accepted SQL or threshold changes. This task changes selection/routing of definitions, not query behavior.

#### Authorization/concurrency impact

None. Disposable-database and filesystem safety are the relevant boundaries.

#### Protected regressions

Historical 33 commands, 80 parameters, 198 plans, 61 invariants, 69 files, PostgreSQL 16 gates, aggregate hash, A1 topology, Q1 gate, credential rejection, exact container safety, and all evidence bytes.

#### Tests required

Unit tests for CLI/profile selection, exact per-lane safety, raw-versus-permanent manifest dispatch, manifest mismatch, destination allowlist, traversal/collision rejection, frozen historical export, staging failure recovery, credential scanning, and historical offline verification. A copied accepted raw fixture may be verified; no permanent export occurs.

#### Verification commands

```powershell
dotnet build tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~RealEstate.Tests.Unit.QueryReview"
(Get-ChildItem docs/benchmarks/chapter-10f/evidence -File -Recurse).Count
git hash-object docs/benchmarks/chapter-10f/evidence/baseline-measurements.json
dotnet build RealEstate.slnx -c Release
dotnet test RealEstate.slnx -c Release
git diff --check
```

The evidence must additionally compute raw SHA-256 and confirm the historical 69-file inventory and anchors in section 12.

#### Evidence file

`docs/planning/chapter-15d-queryreview-generation-boundary-evidence.md`

#### Expected commit count

**Expected owner commits: 2**

Commit 1 owns generation/profile selection, explicit lane safety, and shared-mechanics dispatch with historical behavior unchanged.

Commit 2 owns offline verification/export routing, frozen historical destination enforcement, regression tests, and usage proof. The split is necessary because database-generation selection and permanent-filesystem export are separate safety boundaries that each need an independently reviewable commit.

#### Completion criteria

Both definitions select deterministically; not-yet-supplied successor capabilities fail closed; historical verification remains exact; no command can overwrite the historical directory; successor routing points only to its fixed path; full build/suite pass.

#### Independent audit focus

Fallback to wrong generation, relaxed container/storage validation, path aliasing/case traversal, accidental historical replacement, changed hashes, credentials in artifacts, and an over-generalized framework.

#### Stop conditions

Stop on any unexplained historical byte/hash/identity drift, any route capable of replacing the old directory, or any design that needs arbitrary executable configuration.

### 15E — Seed and verify the deterministic four-root profile

#### Title

Create the four-root discovery performance profile

#### Purpose

Implement the exact synthetic population and invariant model in section 11, without drawing a performance or index conclusion.

#### Why this task exists

The accepted 100,000-row historical profile has zero Commercial and zero Land rows and cannot represent subtype selectivity.

#### Depends on

15D.

#### Exact scope

- Add `four-root-discovery-v1` seeding for the locked root, subtype, status, ownership, listing-type, translation, image, currency, geography, price/area/room, age/order, q, and comparable cohorts.
- Preserve historical protected cohort IDs/order wherever required by section 11.
- Add exact invariant queries for all population facts, aggregate child integrity, search/location cohorts, and comparable order.
- Enable only successor profile create/verify; capture, baseline run, and every export command remain fail-closed until their owning tasks.
- Verify create and independent verify on a fresh, exact, auto-removed PostgreSQL 16 container.
- Document that the distribution is a synthetic selectivity scenario, not market data.

#### Explicit non-goals

No new query shape, production seed, performance threshold, index experiment, migration, API change, or production-distribution claim.

#### Expected files/layers

- Domain/Application/Infrastructure/API: none.
- Tests: QueryReview profile/invariant unit tests if logic can be isolated; PostgreSQL CLI verification evidence.
- QueryReview: `DeterministicProfileSeeder.cs`, `ProfileInvariants.cs`, the successor definition/model, and small seed helpers.
- Migrations: none.
- Docs/evidence: profile documentation and ignored task evidence; no final baseline export.

#### Domain impact

None; profile consumes existing enum/domain contracts.

#### Application impact

None.

#### Persistence/migration impact

Data exists only in a confirmed disposable database. The tracked model remains unchanged.

#### API/OpenAPI impact

None.

#### Query/performance impact

Creates the controlled data basis for later measurement. It does not claim a performance pass.

#### Authorization/concurrency impact

None; seeded personal/agency ownership supports query cases only.

#### Protected regressions

Exactly one matching detail row, public-valid Active rows, canonical translations, confirmed locations, q literal cohorts, C1 root-only ranking, old selected IDs/order, and no credential/persistent-volume use.

#### Tests required

Unit checks for deterministic generation/count arithmetic; PostgreSQL profile create plus independent profile verify; repeat-create hash equivalence on a fresh database; invariant failure test for at least one deliberately corrupted disposable copy. No OpenAPI, concurrency, or migration tests.

#### Verification commands

```powershell
dotnet build tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile create --profile four-root-discovery-v1 --connection-string "<fresh-disposable-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile verify --profile four-root-discovery-v1 --connection-string "<fresh-disposable-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-container>"
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~RealEstate.Tests.Unit.QueryReview"
dotnet build RealEstate.slnx -c Release
dotnet test RealEstate.slnx -c Release
git diff --check
```

The actual evidence substitutes and records an exact local-only connection, container name, image tag/digest, and auto-remove/storage inspection; no secret is committed.

#### Evidence file

`docs/planning/chapter-15e-four-root-profile-evidence.md`

#### Expected commit count

**Expected owner commits: 1**

#### Completion criteria

Two independent fresh runs produce the same profile/invariant hashes; every locked count and cohort passes; exact four-root details are valid; capture/run/export remain disabled; no performance or market claim is made; full build/suite pass.

#### Independent audit focus

Arithmetic drift, correlated data that defeats selectivity testing, subtype values missing from non-Active states, broken old cohorts, accidental production DB use, and nondeterminism.

#### Stop conditions

Stop if protected q/location/comparable cohorts or legacy selected IDs cannot be preserved without changing section 11, or if safety verification cannot prove disposal/isolation.

### 15F — Lock successor discovery query shapes

#### Title

Define four-root discovery SQL and result identities

#### Purpose

Add the behavior-based subtype/selectivity shapes and exact successor command/result/order manifest.

#### Why this task exists

The historical eight shapes have no Commercial/Land subtype filters, deep subtype pages, cross-family empty case, or agency-plus-subtype composition.

#### Depends on

15B, 15D, and 15E.

#### Exact scope

- Retain explicit mappings for `N1/P1/P2/A1/R1/L1/Q1/C1` and verify null subtype inputs preserve their Chapter 14 SQL/typed parameters.
- Add exactly the 13 behavior-named shapes in section 12; no extra exploratory shape enters the manifest.
- Require every Commercial/Land subtype count/page SQL identity to contain both the explicit root `PropertyType` equality and matching child/type predicate; require `cross-family-subtypes-empty` to contain both mutually exclusive root predicates.
- Lock each shape's input, command roles/order, exact SQL normalization identity, typed parameter type/value/nullability, total count, selected IDs, and order hash.
- Lock the successor command/parameter/plan/invariant totals in its generation definition after capture.
- Exercise real handlers/repositories; do not introduce benchmark-only SQL.
- Enable successor capture, baseline run, and raw/curated offline verify; permanent export must still reject as not finalized.

#### Explicit non-goals

No public agency-route parameter, q expansion, comparable subtype rule, index, performance acceptance, schema change, or final permanent export.

#### Expected files/layers

- Domain/Application/Infrastructure/API: no production edits expected; task consumes 15B code.
- Tests: QueryReview manifest/result/order unit checks and PostgreSQL capture evidence.
- QueryReview: `QueryShapeDefinitions.cs`, capture role validation, successor definition, and measurement models only as needed for explicit shapes.
- Migrations: none.
- Docs/evidence: successor shape map and ignored task evidence; captured raw run remains outside the permanent evidence path.

#### Domain impact

None.

#### Application impact

None; production query contract was completed in 15B.

#### Persistence/migration impact

Read-only against the disposable profile; no migration.

#### API/OpenAPI impact

No new API change. Shapes must reflect section 8 and use `agencyId` through general search, not the reduced agency route.

#### Query/performance impact

Establishes exact functional SQL/result/order identities; measurements are collected for validation but no index/performance disposition is made.

#### Authorization/concurrency impact

None.

#### Protected regressions

Old SQL/80 typed parameters with null new filters; explicit root-plus-child SQL for every new subtype shape; q/effective-translation behavior; count-before-page; deterministic order; child query roles; C1 six-key/root-only behavior.

#### Tests required

QueryReview unit/manifest tests; PostgreSQL capture on a verified profile; exact count/result/order and explicit root-predicate assertions; malformed manifest and unexpected command-role failure tests. The well-formed performance profile is not a substitute for 15B's malformed-row integration regression. Generated OpenAPI is not retested here beyond 15B/full suite.

#### Verification commands

```powershell
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- capture-sql --profile four-root-discovery-v1 --connection-string "<verified-disposable-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline run --profile four-root-discovery-v1 --connection-string "<verified-disposable-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-container>"
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~RealEstate.Tests.Unit.QueryReview"
dotnet build RealEstate.slnx -c Release
dotnet test RealEstate.slnx -c Release
git diff --check
```

#### Evidence file

`docs/planning/chapter-15f-successor-query-shapes-evidence.md`

#### Expected commit count

**Expected owner commits: 1**

#### Completion criteria

All 13 new and eight mapped legacy shapes have exact command/parameter/result/order locks; every new subtype shape locks explicit root equality plus child/type equality; old shapes have no unexplained drift; shape totals are manifest-owned; capture is repeatable; permanent export remains fail-closed; full build/suite pass.

#### Independent audit focus

Benchmark-only SQL, a new subtype shape missing explicit root equality, a shape accidentally calling the agency route, missing count/deep-page coverage, accidental q/comparable change, parameter type drift, or quietly updated expected IDs.

#### Stop conditions

Stop on unexplained old-shape SQL/parameter/result/order drift or if real repository execution cannot express a required accepted contract.

### 15G — Characterize unindexed PostgreSQL 16 behavior

#### Title

Capture the mandatory unindexed subtype baseline

#### Purpose

Establish accepted correctness and cost evidence before any candidate index exists.

#### Why this task exists

Commercial/Land dependent tables currently have only shared-PK coverage. Index usefulness cannot be inferred without the implemented predicates and four-root selectivity profile.

#### Depends on

15F.

#### Exact scope

- Rebuild a fresh exact PostgreSQL 16 profile from the accepted source.
- Prove no non-PK subtype index exists in the catalog.
- Run locked statistics preparation, one warm-up, and five measured samples in fixed order.
- Capture exact SQL/parameters/results/order, actual/estimated rows, plans, buffers, timings, spill/temp, relation/index sizes, and sequence medians for all protected and successor shapes.
- Commit immutable experimental artifacts under `docs/benchmarks/four-root-discovery-v1/experiments/unindexed/<bundle-sha256>/` plus a concise interpretation record.
- Copy only the verified curated bundle to a previously absent content-addressed tracked directory, then offline-verify that committed-path content and record its complete file/hash inventory.
- Classify selective versus nonselective subtype shapes from measured rows, not enum intuition.

#### Explicit non-goals

No candidate DDL, EF configuration, migration, threshold relaxation, production SLO claim, or final successor export.

#### Expected files/layers

- Domain/Application/Infrastructure/API/tests/QueryReview/migrations: none expected.
- Docs/evidence: tracked unindexed experiment directory/report and ignored task evidence.

#### Domain impact

None.

#### Application impact

None.

#### Persistence/migration impact

Disposable profile only; production inventory remains 21 migrations.

#### API/OpenAPI impact

None.

#### Query/performance impact

Creates the authoritative pre-index measurement point. Slowness may be recorded; functional/hash/capture failures may not.

#### Authorization/concurrency impact

None.

#### Protected regressions

All section 14 correctness identities, historical directory immutability, no spill/temp, and catalog proof of the unindexed starting state.

#### Tests required

PostgreSQL profile verify, capture, one locked run containing the discarded warm-up plus five measured samples, offline raw verification, and committed-curated-bundle re-verification. No second cross-run timing comparison is implied. No unit source change is expected; if tooling needs correction, return to the owning task instead of hiding it here.

#### Verification commands

```powershell
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile create --profile four-root-discovery-v1 --connection-string "<fresh-disposable-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile verify --profile four-root-discovery-v1 --connection-string "<fresh-disposable-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline run --profile four-root-discovery-v1 --connection-string "<fresh-disposable-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-dir "<sealed-unindexed-run-directory>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-dir "<tracked-content-addressed-unindexed-directory>"
git diff --check
```

#### Evidence file

`docs/planning/chapter-15g-unindexed-performance-evidence.md`

#### Expected commit count

**Expected owner commits: 1**

#### Completion criteria

The sealed run passes every correctness/manifest gate, proves the unindexed catalog, contains five valid measured samples, and reports all section 13 metrics without an index recommendation being assumed in advance.

#### Independent audit focus

Warm-cache/order bias, missing samples, catalog contamination, selective/nonselective misclassification, median arithmetic, plan/result mismatch, and use of a persistent database.

#### Stop conditions

Stop on correctness/hash drift, missing/incomplete plans, spills, environment mismatch, unexpected subtype index, or a need to change code/tooling under an evidence-only task.

### 15H — Test candidates and record the index disposition

#### Title

Measure subtype index candidates and decide index or no-index

#### Purpose

Use the locked section 13 gate to choose, independently per dependent table, a measured index configuration or `NO_INDEX`.

#### Why this task exists

The unindexed evidence is necessary but insufficient to assess read benefit, plan stability, storage, and write cost.

#### Depends on

15G.

#### Exact scope

- Derive a bounded candidate set from observed filters/plans; record the reason and exact DDL before running it.
- Test at most two configurations per subtype table plus one combined-winner confirmation, as locked in section 13.
- Measure one candidate configuration at a time on freshly rebuilt identical PostgreSQL 16 profiles.
- Use the same statistics, warm-up/sample protocol, shape order, parameters, and correctness locks as 15G.
- Capture index use, plan/row/buffer/time changes, bytes/build duration, and fixed insert/update write probes.
- Apply every section 13 qualification and regression threshold without post-result adjustment.
- Commit each full bundle under `docs/benchmarks/four-root-discovery-v1/experiments/index-candidates/<behavior-id>/<bundle-sha256>/` and one durable disposition: `NO_INDEX`, or the exact winning Commercial/Land index set. Owner acceptance of this commit accepts the measured storage/write tradeoff only; it grants no broader schema authority.
- Prove production/query/profile/QueryReview/build source hashes equal 15G, then copy each full verified curated bundle once to an absent content-addressed tracked directory and re-verify those tracked bytes.

#### Explicit non-goals

No production EF configuration/migration, arbitrary index combinations, unrelated query tuning, q index change, translation-trigger work, or threshold renegotiation after seeing results.

#### Expected files/layers

- Domain/Application/Infrastructure/API/tests/QueryReview/migrations: none expected.
- Docs/evidence: `docs/benchmarks/four-root-discovery-v1/experiments/index-candidates/`, durable disposition report, and ignored task evidence.

#### Domain impact

None.

#### Application impact

None.

#### Persistence/migration impact

Candidate DDL exists only in confirmed disposable databases. Production migration inventory stays 21 during this task.

#### API/OpenAPI impact

None.

#### Query/performance impact

This task is the sole index decision point. A table receives an index only if its own family meets every gate. `NO_INDEX` is a valid pass.

#### Authorization/concurrency impact

None.

#### Protected regressions

Exact SQL/parameters/results/order; legacy/nonselective performance bounds; no spill/temp; fixed environment; no candidate leakage into source/migrations.

#### Tests required

Repeated PostgreSQL before/after measurement for each candidate and negative gate evaluation demonstrating that a deliberately nonqualifying result cannot be selected. No application unit/OpenAPI/concurrency test is required.

#### Verification commands

```powershell
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-dir "<sealed-unindexed-run-directory>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile create --profile four-root-discovery-v1 --connection-string "<fresh-candidate-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-candidate-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile verify --profile four-root-discovery-v1 --connection-string "<fresh-candidate-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-candidate-container>"
# Apply exactly one pre-recorded candidate DDL configuration to this disposable database, then:
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline run --profile four-root-discovery-v1 --connection-string "<fresh-candidate-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-candidate-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-dir "<sealed-candidate-run-directory>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-dir "<tracked-content-addressed-candidate-directory>"
git diff --check
git diff --name-status
```

Each disposable DDL command, catalog snapshot, container/image digest, and comparison calculation must be reproduced verbatim in the evidence record.

#### Evidence file

`docs/planning/chapter-15h-index-candidate-disposition-evidence.md`

#### Expected commit count

**Expected owner commits: 1**

#### Completion criteria

Every tested candidate is directly comparable to 15G; the disposition follows all locked thresholds; a winner is exact and bounded, or `NO_INDEX` is explicit; no production schema file changed.

#### Independent audit focus

Cherry-picked samples, candidates measured together, changed data/statistics, ignored write/storage cost, threshold arithmetic, unsupported causal claims, and hidden source migration.

#### Stop conditions

Stop rather than select a marginal candidate, change gates, tune unrelated queries, or proceed when correctness/environment equivalence is not exact.

### 15I — Add only a measured winning production index (conditional)

#### Title

Materialize the accepted subtype index configuration

#### Purpose

Translate the exact 15H winner, if any, into EF configuration and one immutable migration.

#### Why this task exists

Disposable candidate DDL is not a production schema contract. A qualifying winner must be represented in EF and exercised through migration lifecycle before final proof.

#### Depends on

15H with a disposition other than `NO_INDEX`. If 15H records `NO_INDEX`, this task is formally skipped and no placeholder commit is made.

#### Exact scope

- Add only the winning index or independently winning Commercial/Land index set, exactly matching accepted method, columns/order, predicate, and database naming.
- Generate one behavior-named migration and update the model snapshot.
- Add the behavior-named `PostgreSqlSubtypeDiscoveryIndexMigrationTests` family for fresh/upgrade/Down/re-Up/catalog/data/model assertions; this exact family is the cumulative gate's conditional selector.
- Verify fresh apply, previous migration -> new migration, Down/re-Up, catalog readiness/validity/definition, row/data preservation, and no pending model changes.
- Preserve every old migration byte.

#### Explicit non-goals

No speculative companion index, query rewrite, table/column/default change, backfill, production deployment, or acceptance of a candidate that missed one threshold.

#### Expected files/layers

- Domain/Application/API: none.
- Infrastructure: only affected subtype entity configuration(s), one generated migration/designer, and model snapshot.
- Tests: behavior-named `PostgreSqlSubtypeDiscoveryIndexMigrationTests` lifecycle/catalog/model family for the accepted index.
- QueryReview: none.
- Migrations: one new migration; 21 -> 22.
- Docs/evidence: ignored task evidence only; 15H remains the durable justification.

#### Domain impact

None.

#### Application impact

None.

#### Persistence/migration impact

Conditional new index-only migration. `Down` removes only the new index/indexes; data and constraints remain unchanged.

#### API/OpenAPI impact

None.

#### Query/performance impact

No SQL/result contract change is allowed. Final index use and performance are not accepted until 15J.

#### Authorization/concurrency impact

None.

#### Protected regressions

Old migration hashes, complete data, four-root constraints/details, no pending model, and no unmeasured schema object.

#### Tests required

PostgreSQL fresh apply, upgrade, Down/re-Up, catalog definition/readiness/validity, data preservation, migration inventory, no-pending-model, and complete Release suite. No OpenAPI or concurrency-specific addition.

#### Verification commands

```powershell
dotnet ef migrations list --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api
dotnet ef database update --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api --connection "<fresh-disposable-pg16-connection>"
dotnet ef database update <previous-migration> --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api --connection "<fresh-disposable-pg16-connection>"
dotnet ef database update <new-index-migration> --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api --connection "<fresh-disposable-pg16-connection>"
dotnet ef migrations has-pending-model-changes --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api
dotnet build RealEstate.slnx -c Release
dotnet test RealEstate.slnx -c Release
git diff --check
```

Catalog SQL and before/after row/data hashes are recorded in evidence.

#### Evidence file

`docs/planning/chapter-15i-measured-index-migration-evidence.md`

#### Expected commit count

**Expected owner commits: 1 (conditional)**

#### Completion criteria

The migration exactly represents the accepted DDL, is migration 22 if activated, passes all lifecycle/catalog/data/model checks, and introduces no other schema change.

#### Independent audit focus

Mismatch from measured DDL, implicit provider options, old migration edits, lossy Down, invalid/not-ready catalog state, snapshot noise, and pending model drift.

#### Stop conditions

Skip on `NO_INDEX`. Otherwise stop if generated schema differs from 15H, lifecycle/data checks fail, or another schema fix appears necessary.

### 15J — Publish the authoritative PostgreSQL 16 successor baseline

#### Title

Accept final four-root SQL, results, order, and plans on PostgreSQL 16

#### Purpose

Create the immutable successor baseline from the final 21- or 22-migration schema.

#### Why this task exists

Experimental evidence cannot serve as the permanent generation authority, and final plans must reflect the accepted index disposition.

#### Depends on

15H, plus 15I only when an index was selected.

#### Exact scope

- Finalize the successor permanent-export manifest with the accepted 15H disposition identity, final migration count, source/profile/shape hashes, lane destinations, and gates; add focused manifest/export tests.
- Build a fresh disposable PostgreSQL 16 database from the final schema and exact `four-root-discovery-v1` profile.
- Capture and verify all mapped legacy and new subtype shapes using one warm-up plus five measured samples.
- Export through the fixed successor route to `docs/benchmarks/four-root-discovery-v1/evidence/postgresql-16/`.
- Commit a durable proof at `docs/benchmarks/four-root-discovery-v1/chapter-15-successor-sql-plan-proof.md` covering source/profile/environment hashes, SQL/typed parameters, result/order, command/plan/invariant totals, plan/buffer/time/spill gates, and index/no-index disposition.
- Verify credential absence and re-run offline verification from committed bytes.

#### Explicit non-goals

No historical baseline rewrite, new query/index tuning, threshold change, PostgreSQL 18.4 conclusion, or production SLO.

#### Expected files/layers

- Domain/Application/Infrastructure/API/migrations: none.
- Tests/QueryReview: successor definition/export-gate constants and focused unit tests only; no query/seeder/capture behavior change.
- Docs/evidence: fixed PostgreSQL 16 successor evidence directory, proof document, and ignored task evidence.

#### Domain impact

None.

#### Application impact

None.

#### Persistence/migration impact

Evidence observes final schema only; inventory is 21 for `NO_INDEX` or 22 for a winner.

#### API/OpenAPI impact

None.

#### Query/performance impact

This is authoritative Chapter 15 PostgreSQL 16 acceptance. It locks final SQL/parameters/results/order and accepted plans/metrics without replacing old generation hashes.

#### Authorization/concurrency impact

None.

#### Protected regressions

Every section 14 rule, section 13 final disposition, historical 69 files/hashes, q/C1 behavior, no spill/temp, and source/profile/environment provenance.

#### Tests required

Focused export-finalization tests; fresh profile create/verify, capture, baseline run, export, committed offline verify, file/hash inventory, credential scan, and comparison to 15G/15H; full Release build/suite because the generation definition changes.

#### Verification commands

```powershell
# Commit 1 verification:
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~RealEstate.Tests.Unit.QueryReview"
dotnet build RealEstate.slnx -c Release
dotnet test RealEstate.slnx -c Release
# Only after Commit 1 is independently audited and owner-committed:
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile create --profile four-root-discovery-v1 --connection-string "<fresh-disposable-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile verify --profile four-root-discovery-v1 --connection-string "<fresh-disposable-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline run --profile four-root-discovery-v1 --connection-string "<fresh-disposable-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline export --run-dir "<sealed-final-pg16-run-directory>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-dir docs/benchmarks/four-root-discovery-v1/evidence/postgresql-16
git diff --check
```

The evidence also hashes and recounts the historical directory before and after export.

#### Evidence file

`docs/planning/chapter-15j-postgresql16-successor-baseline-evidence.md`

#### Expected commit count

**Expected owner commits: 2**

Commit 1 owns the final disposition/schema/lane export manifest, its focused tests, and the full Release build/suite. It must be independently audited and owner-committed before evidence capture.

Commit 2 owns the PostgreSQL 16 permanent export and durable proof generated from Commit 1's immutable source. The split is necessary so permanent evidence never cites an uncommitted tool/manifest tree.

#### Completion criteria

The final manifest gates both lane exports to the accepted schema/disposition; committed PostgreSQL 16 evidence verifies offline; its proof explains every old/new/profile/index delta; the selected/no-index gate passes; no credential exists; historical evidence is byte-identical; full build/suite pass.

#### Independent audit focus

Wrong source/schema/profile, nonfixed destination, result/order blessing, omitted parameters/plans, inconsistent totals, timing cherry-pick, and historical-path mutation.

#### Stop conditions

Stop on any failed manifest/correctness/performance gate or any need to alter production/tooling code beyond the planned final export-manifest/gate values; route other remediation to its owning task and regenerate from a new immutable source.

### 15K — Verify the tracked PostgreSQL 18.4 runtime lane

#### Title

Run bounded PostgreSQL 18.4 compatibility and performance observation

#### Purpose

Prove that the final schema/profile/query generation works on the tracked runtime major without sacrificing PostgreSQL 16 historical authority.

#### Why this task exists

Integration and accepted QueryReview evidence use PostgreSQL 16, while current `docker-compose` uses PostgreSQL 18.4.

#### Depends on

15J.

#### Exact scope

- Use a separate exact auto-removed `postgres:18.4` container and its image-declared storage layout; never use the persistent development database.
- On the same host and in the same session, build a fresh companion PostgreSQL 16 container with identical source/profile/settings and sample order; require its correctness hashes to equal 15J before using its medians as the cross-major reference.
- Apply the final migration chain, seed/verify the same profile, capture the same shapes/parameters/results/order, and collect lane-qualified plans/metrics/catalog facts.
- Export compatibility evidence atomically to the fixed `docs/benchmarks/four-root-discovery-v1/evidence/postgresql-18.4/` lane. That lane bundle includes the contemporaneous PostgreSQL 16 curated comparison sub-bundle and cross-major report.
- Hash the accepted PostgreSQL 16 permanent sibling before/after and prove the 18.4 export neither stages over nor changes it.
- Apply section 14 hard failures and review thresholds; explain material major-version plan/metric differences without treating plan JSON as byte-equal.

#### Explicit non-goals

No move of primary performance authority to 18.4, cross-major timing SLA, docker-compose mutation, deployment hardening, query/index retuning, or historical evidence rewrite.

#### Expected files/layers

- Domain/Application/Infrastructure/API/tests/QueryReview/migrations: none expected.
- Docs/evidence: fixed 18.4 compatibility evidence/summary and ignored task evidence.

#### Domain impact

None.

#### Application impact

None.

#### Persistence/migration impact

Fresh disposable final migration chain only; no new migration.

#### API/OpenAPI impact

None.

#### Query/performance impact

SQL/typed parameters/results/order must equal PostgreSQL 16. Plans and metrics are separately versioned observations subject to the bounded review gate.

#### Authorization/concurrency impact

None.

#### Protected regressions

Exact lane safety, final index catalog, all correctness hashes, no spill/temp, and PostgreSQL 16 authority.

#### Tests required

Exact-container safety for both contemporaneous containers, fresh migration/profile verify, full capture/run/offline verify, catalog comparison, credential scan, 15J-to-companion correctness equivalence, and cross-lane correctness/metric report. No new application tests expected.

#### Verification commands

```powershell
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile create --profile four-root-discovery-v1 --connection-string "<fresh-contemporaneous-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-pg16-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile verify --profile four-root-discovery-v1 --connection-string "<fresh-contemporaneous-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-pg16-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline run --profile four-root-discovery-v1 --connection-string "<fresh-contemporaneous-pg16-connection>" --confirm-disposable --container-name "<exact-auto-remove-pg16-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-dir "<sealed-contemporaneous-pg16-run-directory>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile create --profile four-root-discovery-v1 --connection-string "<fresh-disposable-pg18.4-connection>" --confirm-disposable --container-name "<exact-auto-remove-pg18.4-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile verify --profile four-root-discovery-v1 --connection-string "<fresh-disposable-pg18.4-connection>" --confirm-disposable --container-name "<exact-auto-remove-pg18.4-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline run --profile four-root-discovery-v1 --connection-string "<fresh-disposable-pg18.4-connection>" --confirm-disposable --container-name "<exact-auto-remove-pg18.4-container>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline export --run-dir "<sealed-final-pg18.4-run-directory>" --comparison-run-dir "<sealed-contemporaneous-pg16-run-directory>"
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-dir docs/benchmarks/four-root-discovery-v1/evidence/postgresql-18.4
git diff --check
```

#### Evidence file

`docs/planning/chapter-15k-postgresql18-compatibility-evidence.md`

#### Expected commit count

**Expected owner commits: 1**

#### Completion criteria

The 18.4 lane passes exact correctness/catalog/safety gates; every review-threshold exceedance is resolved or explicitly blocks; plan differences are lane-qualified; PostgreSQL 16 remains authoritative.

#### Independent audit focus

Use of persistent dev storage, relaxed image checks, accidental primary-lane change, comparing noisy cross-major timings as an SLA, hidden correctness drift, and unreviewed material regression.

#### Stop conditions

Stop on any hard failure or unresolved section 14 review threshold. Do not tune production queries or indexes inside this evidence task.

### 15L — Run cumulative technical verification

#### Title

Verify the assembled Chapter 15 technical tree

#### Purpose

Produce one durable, independently auditable technical gate from one immutable source.

#### Why this task exists

Task-local passes do not prove the assembled tree, unique test accounting, migration state, generated OpenAPI, and evidence provenance together.

#### Depends on

15K and all prior mandatory tasks; 15I only if activated.

#### Exact scope

- Freeze source commit/tree and environment before execution.
- Run Release restore/build and complete suite; separately account for unique focused test identities without adding focused executions to the full-suite total.
- Generate/assert OpenAPI, migrations/model, subtype discovery/agency decision, root-only comparable, and protected Chapter 13/14 regressions.
- Offline-verify PostgreSQL 16 successor and 18.4 compatibility evidence plus historical hashes.
- Inventory source/evidence hashes, migrations, tests, quality-register ownership, and frontend non-change.
- Create `docs/chapters/chapter-15l-cumulative-chapter-15-verification-gate.md` with `FINAL_PASS` only if every technical gate passes.
- State explicitly that chapter status is not complete until 15M passes and is owner-committed.

#### Explicit non-goals

No remediation, production/test/query/evidence mutation, threshold waiver, closeout/status completion, or frontend reconciliation.

#### Expected files/layers

- Domain/Application/Infrastructure/API/tests/QueryReview/migrations: none.
- Docs/evidence: the cumulative verification record and ignored task evidence only.

#### Domain impact

None.

#### Application impact

None.

#### Persistence/migration impact

Read-only verification against fresh disposable databases; assert 21 or 22 according to disposition and no pending model.

#### API/OpenAPI impact

Generated contract is verified, not edited.

#### Query/performance impact

All permanent evidence and hashes are verified from committed bytes; no recapture under a different source is accepted.

#### Authorization/concurrency impact

Existing complete suite covers protected lifecycle/authorization/concurrency behavior; no new race orchestration is introduced.

#### Protected regressions

All sections 4, 8, 9, 10, 13, and 14, plus no frontend changes and truthful quality ownership.

#### Tests required

Complete Release suite; listed focused families with nonzero selection and unique accounting; generated OpenAPI; migration fresh/upgrade/model checks; historical/successor offline evidence verification; file/hash/credential inventory.

#### Verification commands

```powershell
dotnet restore RealEstate.slnx --disable-parallel -m:1 -nodeReuse:false
dotnet build RealEstate.slnx -c Release --no-restore
dotnet test RealEstate.slnx -c Release --no-build --no-restore --logger "console;verbosity=minimal"
$verificationResults = New-Item -ItemType Directory -Path (Join-Path ([System.IO.Path]::GetTempPath()) ("realestate-chapter15-gate-" + [guid]::NewGuid().ToString("N")))
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --list-tests
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --list-tests --filter "FullyQualifiedName~OpenApiDocumentTests|FullyQualifiedName~RootTypeDiscovery|FullyQualifiedName~CommercialAndLandSubtypeDiscovery|FullyQualifiedName~GetAgencyListings_SubtypeQueryParameters|FullyQualifiedName~GetListingsValidatorTests|FullyQualifiedName~GetAgencyDashboardListings|FullyQualifiedName~GetComparables|FullyQualifiedName~ComparableRootIsolation|FullyQualifiedName~RealEstate.Tests.Unit.QueryReview"
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~OpenApiDocumentTests|FullyQualifiedName~RootTypeDiscovery|FullyQualifiedName~CommercialAndLandSubtypeDiscovery|FullyQualifiedName~GetAgencyListings_SubtypeQueryParameters|FullyQualifiedName~GetListingsValidatorTests|FullyQualifiedName~GetAgencyDashboardListings|FullyQualifiedName~GetComparables|FullyQualifiedName~ComparableRootIsolation|FullyQualifiedName~RealEstate.Tests.Unit.QueryReview" --results-directory $verificationResults.FullName --logger "trx;LogFileName=chapter-15-focused.trx"
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~PostgreSqlCommercialAndLandTaxonomyMigrationTests" --results-directory $verificationResults.FullName --logger "trx;LogFileName=chapter-15-current-migration.trx"
# If 15I activated:
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --list-tests --filter "FullyQualifiedName~PostgreSqlSubtypeDiscoveryIndexMigrationTests"
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~PostgreSqlSubtypeDiscoveryIndexMigrationTests" --results-directory $verificationResults.FullName --logger "trx;LogFileName=chapter-15-index-migration.trx"
$focusedTrxPaths = Get-ChildItem -LiteralPath $verificationResults.FullName -Filter 'chapter-15-*.trx' -File
$focusedIdentities = @($focusedTrxPaths | ForEach-Object { [xml]$trx = Get-Content -Raw -LiteralPath $_.FullName; $trx.GetElementsByTagName('UnitTestResult') | ForEach-Object { "{0}|{1}" -f $_.testId, $_.testName } } | Sort-Object -Unique)
if ($focusedIdentities.Count -eq 0) { throw 'Focused verification selected zero unique tests.' }
$focusedIdentities.Count
dotnet ef migrations list --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api
dotnet ef migrations has-pending-model-changes --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-dir docs/benchmarks/four-root-discovery-v1/evidence/postgresql-16
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-dir docs/benchmarks/four-root-discovery-v1/evidence/postgresql-18.4
git diff --check
```

The gate parses the focused TRX files, unions stable `testId|testName` identities (thereby retaining distinct theory-row display names without using run-specific `executionId`), proves each filter selected a nonzero set, and reports that unique focused count separately; it never adds focused executions to the complete-suite total. Fresh migration lifecycle/profile-create commands, generated OpenAPI extraction, historical hash verification, credential scanning, and frontend path comparison are also recorded verbatim in the gate.

#### Evidence file

`docs/planning/chapter-15l-cumulative-verification-evidence.md`

#### Expected commit count

**Expected owner commits: 1**

#### Completion criteria

The durable record reports technical `FINAL_PASS`, exact actual totals and hashes, no double-counting, no pending model, correct migration disposition, accepted SQL/performance lanes, open quality ownership, and no frontend change.

#### Independent audit focus

Wrong source, stale artifacts, double-counted tests, skipped failures, migration/profile mismatch, credential leakage, quality issue laundering, and premature chapter completion.

#### Stop conditions

Any failure blocks `FINAL_PASS`. Return to the owning task; do not fix code, tests, migrations, evidence, or docs inside the gate.

### 15M — Close Chapter 15 documentation and status

#### Title

Close discovery and performance hardening

#### Purpose

Record the accepted final state and establish post-Chapter-15 backend/frontend reconciliation as the next boundary.

#### Why this task exists

Technical `FINAL_PASS` must be reflected minimally in durable authority without hiding corrections in a documentation commit.

#### Depends on

15L `FINAL_PASS` and independent acceptance of that gate.

#### Exact scope

- Update this chapter status and task table with actual audited outcomes/commit references.
- Minimally update `docs/backend-context.md` with actual migration/test/profile/SQL/index/PG-lane totals and decisions.
- Review all quality-handoff entries; edit `docs/backend-quality-handoff.md` only if independently proven truth changed or a new unresolved issue must be registered.
- Preserve `docs/backend-frontend-handoff.md` byte-for-byte.
- Name final backend/frontend reconciliation as the next boundary.
- Record `NO_INDEX`/21 migrations or measured index/22 migrations exactly as accepted.

#### Explicit non-goals

No technical correction, evidence regeneration, test update, migration, production code, frontend change, broad docs consolidation, or claim that an open quality item is resolved.

#### Expected files/layers

- Domain/Application/Infrastructure/API/tests/QueryReview/migrations: none.
- Docs/evidence: this plan, `docs/backend-context.md`, possibly `docs/backend-quality-handoff.md` under the rule above, and ignored closeout evidence.

#### Domain impact

None.

#### Application impact

None.

#### Persistence/migration impact

Documentation records, but does not change, final inventory.

#### API/OpenAPI impact

None.

#### Query/performance impact

Documentation records accepted immutable evidence; it does not alter it.

#### Authorization/concurrency impact

None.

#### Protected regressions

All accepted totals/hashes/outcomes, quality-register openness, frozen frontend handoff, and no self-acceptance beyond independently audited facts.

#### Tests required

No new executable tests. Re-verify referenced hashes/status, link/path existence, exact diff scope, and absence of technical changes.

#### Verification commands

```powershell
git status --short
git diff --name-status
git diff --stat
git diff --check
git diff
git ls-files --others --exclude-standard
```

The closeout evidence also rechecks the 15L source/evidence hashes and confirms no path under the frontend or frozen frontend handoff changed.

#### Evidence file

`docs/planning/chapter-15m-closeout-evidence.md`

#### Expected commit count

**Expected owner commits: 1**

#### Completion criteria

All authority reflects independently accepted actual results; only bounded documentation changed; next boundary is explicit; independent closeout audit returns `FINAL_PASS`; owner commits the closeout.

#### Independent audit focus

Invented totals, premature completion, hidden technical diff, quality item marked resolved without proof, stale index/migration outcome, and any frontend handoff change.

#### Stop conditions

Stop if any technical correction is needed, 15L is not final, an audit outcome is missing, or recorded totals cannot be reproduced from durable evidence.

## 21. Dependency graph

```text
owner approves plan
        |
       15A
      / | \
   15B 15C 15D
     \       |
      \     15E
       \    /
        15F
         |
        15G
         |
        15H
       /   \
 NO_INDEX  winner
     |       |
     |      15I (conditional)
     |       |
     +------15J
              |
             15K
              |
       15C ---+--- all accepted prior work
              |
             15L FINAL_PASS
              |
             15M closeout
```

The graph expresses causality, not a demand that every independent branch run serially. In particular, 15B, 15C, and 15D can be developed independently after 15A; 15F is the first join of API semantics and profile/tool capability.

There are **13 task specifications: 12 mandatory and 1 conditional**. The mandatory path expects **14 owner commits**. Activating 15I raises the maximum to **15 owner commits**. No task exceeds two commits.

## 22. Release-unit/deployability notes

- **15B is an atomic API/query release unit.** Request contract, validation, predicate, tests, and OpenAPI truth land in its one commit; there is no intermediate contract with ignored parameters.
- **15C is independently deployable.** It adds defense only for directly constructed invalid dashboard application queries; existing invalid-HTTP binding behavior is unchanged, and it need not wait for performance work.
- **15D's two commits are one audited tooling unit.** Commit 1 retains historical behavior and has no production/API runtime effect; commit 2 enables safe successor routing. Neither changes the API. The owner should merge/release the task only after both commits pass its audit.
- **15E and 15F are individually reviewable tooling/data units.** A seeded profile without new shapes is harmless; no successor evidence is declared authoritative until 15F and later measurement tasks pass.
- **15I and 15J form a coordinated release unit if an index wins.** The migration commit is independently reviewable, but merging/releasing 15I into a deployable integration branch before 15J's evidence commit and independent audit is forbidden. The owner may retain the reviewed 15I commit on the task branch so 15J can use it as immutable input. If 15H says `NO_INDEX`, this unit collapses to 15J on the 21-migration schema.
- **15J's two commits are one evidence release unit.** Commit 1 must be owner-committed to become immutable capture input, but integrating or presenting the successor baseline as accepted between Commit 1 and Commit 2 is forbidden. Commit 2 and its independent audit make the baseline releasable.
- **15K is a release gate, not a runtime change.** Its 18.4 evidence must pass before cumulative acceptance because 18.4 is the tracked runtime image.
- **15L and 15M cannot be combined.** The technical gate must reach independent `FINAL_PASS` before documentation can declare completion. No merging between them is permitted when 15L has not passed.

## 23. Chapter completion gate

Chapter 15 may be declared complete only when all conditions below are true from durable evidence.

1. The scalar subtype discovery contract in section 8 is implemented and tested, including Unknown, defined numeric input, automatic HTTP model-state failure for malformed/undefined input, direct-query validator defense, explicit Commercial/Land root-plus-child isolation against malformed persisted rows, contradictions, and both-family input.
2. Generated OpenAPI advertises the exact general-search parameters/values and omits them from reduced/private/management routes.
3. Public agency behavior follows section 9; Apartment/House behavior is protected.
4. Active-only truth, effective translation, q's exact four fields/escaping, filter-before-count/page, deterministic order, and parent-page hydration pass.
5. Dashboard HTTP invalid-enum behavior remains unchanged, while directly constructed undefined status is rejected after existing access checks and before the listing repository; valid-input authorization/repository order remains exact.
6. Comparables remain exact-root and subtype-neutral with the existing six-key ranking unless this plan is formally amended by an approved product rule.
7. `four-root-discovery-v1` passes all exact population, integrity, search/location, and comparable invariants and is explicitly described as synthetic engineering evidence.
8. Historical QueryReview artifacts remain immutable; explicit generation routing and destination protection pass.
9. Accepted unindexed PostgreSQL 16 evidence precedes the candidate decision.
10. The index disposition follows the locked quantitative/qualitative gates. If no candidate wins, no migration exists and 21 migrations is the correct successful outcome. If a candidate wins, migration 22 passes fresh apply, Down/re-Up, catalog, preservation, model, and final plan gates.
11. Final PostgreSQL 16 successor SQL/typed parameters/results/order/plans verify from committed bytes, with every accepted delta classified.
12. The bounded PostgreSQL 18.4 lane passes correctness, catalog, safety, spill, and review gates without replacing PostgreSQL 16 authority.
13. Release restore/build, complete test suite, unique focused accounting, generated OpenAPI, migrations/model, credentials, source/evidence hashes, and protected Chapter 13/14 regressions all pass from one immutable source.
14. All ten quality-register entries have truthful ownership; no frontend code or frozen frontend handoff changed.
15. Every activated implementation task has an independent `FINAL_PASS`; 15L records cumulative technical `FINAL_PASS`; 15M records closeout `FINAL_PASS`; all expected owner commits are complete.

The technical gate must not label the chapter complete while 15M remains pending.

## 24. Deferred work after Chapter 15

The next planned boundary is final backend/frontend reconciliation using the frozen handoff as historical input and the completed Chapter 15 API as current truth.

The following remain deferred unless separately authorized: subtype-aware valuation/comparables; product-derived market distributions; future approved Commercial/Land attributes; wider agency discovery vocabulary; q expansion; operational translation write-amplification work; all other live quality-register items; deployment/auth hardening; geographic search; media/jobs/notifications; dynamic taxonomy/amenities; generic architecture changes; and frontend/generated-client work.

## 25. Decision log / architecture rationale

| Decision | Rationale | Rejected alternative / trigger to revisit |
|---|---|---|
| Optional scalar exact subtype filters | Matches every existing filter and requires the smallest compatible extension | Multi-value OR has no precedent/product requirement |
| New Commercial/Land subtype predicates require explicit root equality plus matching child/type; contradictions use AND/empty | Persistence permits mismatched/additional children, so child presence cannot establish root identity; Apartment/House behavior remains unchanged | Revisit only with explicit product/client contract or a separately approved persistence invariant change |
| Unknown is filterable | It is a valid persisted Active value by Chapter 14 contract | Treating it as absence would misrepresent stored data |
| General public search only | Rich `agencyId` composition already exists; reduced agency route is intentional | Broaden only with explicit client requirement |
| Private/my/dashboard vocabulary unchanged | They are management surfaces, not public discovery | Revisit as a separately scoped management product feature |
| Root-only comparables | No subtype valuation rule exists | Requires approved applicability, fallback, Unknown/Other, and rank/exclusion semantics |
| Include dashboard direct-query status validation | Protects internal/non-HTTP handler calls after existing access checks; HTTP binding already rejects malformed and undefined values | No custom binder or broader endpoint cleanup follows from it |
| Synthetic 40/30/20/10 root scenario with 40/30/20/10 subtype bands | Preserves 100k scale/legacy cohorts while creating common-to-rare deterministic selectivity | It is not market truth; production-derived weighting requires authorized data and a plan amendment |
| Shared mechanics plus two explicit QueryReview definitions | Prevents historical/successor drift without duplicating a tool or building a framework | Static constants are unsafe; a duplicate/generic framework is unnecessary |
| PostgreSQL 16 primary plus 18.4 compatibility | Retains historical comparability and covers tracked runtime | Moving primary evidence to 18.4 would sever before/after comparability |
| No index before measurement | Current subtype columns have no representative cardinality evidence | Migration is activated only by 15H's locked gate |
| `NO_INDEX` is a passing outcome | Avoids schema/write cost when no candidate materially wins | No empty migration or post-hoc lowered threshold |
| Tiny Chapter 14 status correction first | Durable authority should not keep known superseded pending wording | It does not rewrite Chapter 14 evidence/history |

There is **no remaining product decision blocking the core Chapter 15 path**. Subtype-aware comparables and claimed market distributions are deliberately excluded. The index outcome is a measurement result, not an unresolved semantic choice. Normal owner approval is still required for this plan, each task commit, and any measured schema winner.

## 26. Final self-audit

### Commit-feasibility review

| Task | Concern boundary | Expected commits | Feasibility result |
|---|---|---:|---|
| 15A | status-only documentation correction | 1 | bounded |
| 15B | one atomic subtype API/query contract | 1 | bounded |
| 15C | direct dashboard-query enum defense in depth | 1 | bounded |
| 15D | generation selection plus permanent-export safety | 2 | bounded; two independently auditable safety commits justified |
| 15E | profile data and exact invariants only | 1 | bounded |
| 15F | query/result/order manifest only | 1 | bounded |
| 15G | unindexed evidence only | 1 | bounded |
| 15H | disposable candidates and disposition only | 1 | bounded |
| 15I | exact measured EF index migration only | 1 conditional | bounded |
| 15J | final export manifest, then PostgreSQL 16 evidence/proof | 2 | bounded; immutable-source split required |
| 15K | PostgreSQL 18.4 compatibility evidence only | 1 | bounded |
| 15L | cumulative verification record only | 1 | bounded |
| 15M | final documentation/status only | 1 | bounded |
| **Mandatory total** | **12 activated tasks** | **14** | **within cap** |
| **Conditional maximum** | **15I activated** | **15** | **within cap** |

### Architecture challenge results

- No task needs more than two commits; tooling, profile, shapes, unindexed evidence, candidate decision, migration, final proof, compatibility, gate, and closeout are not combined into umbrellas.
- Core subtype semantics, explicit Commercial/Land root isolation, agency scope, Unknown behavior, separate HTTP/direct-query invalid-enum paths, and comparable disposition are explicit; no minor design decision is unnecessarily returned to the owner.
- No market statistic or subtype valuation rule is invented.
- Historical evidence is verify-only and destination-isolated; the successor has generation- and lane-qualified hashes.
- Index choice follows mandatory unindexed evidence and predeclared gates; no migration is mandatory.
- q, frontend, quality-register, authorization, concurrency, and architecture scope do not expand.
- Generated OpenAPI, Apartment/House compatibility, agency-route absence, PostgreSQL 18.4 compatibility, migration/no-migration branches, cumulative verification, and closeout are all explicit.
- Runtime/test/tool identifiers are behavior-based. Chapter identifiers appear only in planning/history/evidence filenames and headings.
- Conditional 15I does not block the no-index path; its dependency and release coupling to final proof are explicit.
- The plan contains no implementation and authorizes no Git operation by an implementation agent.
