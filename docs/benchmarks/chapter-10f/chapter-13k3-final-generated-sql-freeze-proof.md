# Chapter 13K.3 final generated-SQL freeze proof

Date: 2026-09-01

## Verdict and scope

`K3_VERIFICATION_READY_FOR_REVIEW`

Chapter 13K.3 is verification/evidence only. It proves that work after the accepted Chapter 13H.6 post-location projection rebaseline changed no public/comparable generated SQL or locked result semantics. It does not rebaseline, export, bless, or edit SQL and does not change production code, QueryReview expectations, migrations, or the accepted permanent artifacts.

This tracked proof is adjacent to the Chapter 10F benchmark documents rather than inside `docs/benchmarks/chapter-10f/evidence/`, because that accepted baseline directory has a fixed, immutable 69-file inventory. The detailed review record and complete unified diff are kept in the normal ignored planning evidence.

## Accepted baseline and current profile identity

- Accepted H.6 post-location run: `chapter-10f-v1-baseline-20260814T112202Z-2925368b`.
- Benchmark commit recorded by that run: `2925368b087a58440b720b6a5db59e4d4d9987be`.
- Accepted evidence root: `docs/benchmarks/chapter-10f/evidence/`.
- Manifest trust anchor: `docs/benchmarks/chapter-10f/evidence/baseline-measurements.json`.
- Manifest Git blob: `051b93a9b19dbdbcac6a3411afa4636b3b191ad2`.
- Manifest file SHA-256: `d6dac6f58245f7ecd65b626ca1c3b85df2a2d39808a350e8b1536b82779f5a17`.
- Permanent inventory: 69/69 files.
- Manifest-referenced artifacts: 68/68 canonical hashes exact, 0 mismatches.
- Accepted semantic result hash: `7f74f991bf29b6f3ad24d48f2e8e13ecf9f375ea6f9eb0da8f18204c528bfb36` (PASS).
- Current deterministic profile: `chapter-10f-v2`, the Chapter 13J.7 owner-approved coordinate/root ownership successor.
- Locked production SQL run ID: `chapter-10f-v1-production-sql`, intentionally unchanged because v2 changes no query inputs, SQL, or discovery/result identities.
- Current verification commit: `6942cdb5`.

The committed v1 baseline is the accepted H.6 post-location baseline. It was not relabeled as v2. The current v2 profile was used to execute the unchanged production queries and validate current plan gates; no permanent v2 benchmark export occurred.

## Commands

The connection string below is redacted. The actual target was the explicitly named auto-removed local `postgres:16-alpine` container `realestate-queryreview-postgres16-13k3`, loopback port `55447`, database `realestate_queryreview_13k3`.

```text
dotnet build tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-restore

dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile create --connection-string "Host=127.0.0.1;Port=55447;Database=realestate_queryreview_13k3;Username=postgres;Password=<redacted-local-password>;Pooling=false" --confirm-disposable --container-name realestate-queryreview-postgres16-13k3

dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile verify --connection-string "Host=127.0.0.1;Port=55447;Database=realestate_queryreview_13k3;Username=postgres;Password=<redacted-local-password>;Pooling=false" --confirm-disposable

dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- capture-sql --connection-string "Host=127.0.0.1;Port=55447;Database=realestate_queryreview_13k3;Username=postgres;Password=<redacted-local-password>;Pooling=false" --confirm-disposable

dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline run --connection-string "Host=127.0.0.1;Port=55447;Database=realestate_queryreview_13k3;Username=postgres;Password=<redacted-local-password>;Pooling=false" --confirm-disposable --container-name realestate-queryreview-postgres16-13k3

dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-directory "C:\Users\User\AppData\Local\Temp\realestate-queryreview\chapter-10f-v2-baseline-20260901T144239Z-6942cdb5"

dotnet ef migrations has-pending-model-changes --project src/RealEstate.Infrastructure/RealEstate.Infrastructure.csproj --startup-project src/RealEstate.Api/RealEstate.Api.csproj --configuration Release --no-build

dotnet build -c Release --no-restore
```

No `baseline export` command ran.

## Profile, migration chain, and locked identities

`profile create` structurally verified the exact local auto-remove container before database access, applied the complete committed EF migration chain, seeded the current set-based deterministic profile, and finished successfully. Independent `profile verify` then proved the database was complete rather than partial:

- established profile: 61/61;
- J.7 strong-Active integrity: 8/8;
- J.7 coordinate/root ownership: 7/7;
- locked discovery/result identities: 8/8;
- Listings: 100,000;
- translations: 200,000;
- Active: 70,000;
- Draft/Archived/Reserved/Sold/Rented: 6,000 each;
- coordinate pairs/null pairs/partial pairs: 80,000 / 20,000 / 0;
- malformed Active state: 0;
- enabled activation-integrity triggers: 2/2.

The fresh database's `__EFMigrationsHistory` contained exactly 20 rows and ended at `20260824141614_EnforceStrongActiveLocationIntegrity`. The repository also contains 20 forward migration files. EF reported `No changes have been made to the model since the last migration.` No migration, designer, or snapshot changed.

Locked results use the deterministic UUID prefix `40000000-0000-0000-0000-` plus the listing ordinal encoded as 12 hexadecimal digits. Current capture matched these exact totals and orders:

| Shape | Total | Items | Exact ordered ordinals |
|---|---:|---:|---|
| N1 | 70,000 | 20 | 3031 through 3012 descending |
| P1 | 23,334 | 20 | 69,961 descending by 3,000 through 12,961 |
| P2 | 23,334 | 20 | 68,998 descending by 3,000 through 11,998 |
| A1 | 350 | 20 | 3,001; then 69,801 descending by 1,000 through 51,801 |
| R1 | 1,050 | 20 | 69,844 descending by 1,000 through 50,844 |
| L1 | 140 | 20 | 1,140 through 1,121 descending |
| Q1 | 120 | 20 | 2,120 through 2,101 descending |
| C1 | 30 eligible | 6 | 3,003, 3,002, 3,005, 3,004, 3,006, 3,007 |

The capture also verified page size 20, comparable limit 6, counts before page loading, deterministic page ordering, and split child hydration.

## Exact 33-command comparison

The capture reconstructed the established curated representation for each command: command-key header, exact typed parameter metadata/value lines, and complete generated command text. Both current and accepted text used only the documented normalization: CRLF and lone CR became LF. Comparison was ordinal. No whitespace, identifier, parameter, SQL, or metadata normalization was added.

The hash column is both the accepted normalized SHA-256 and the current reconstructed normalized SHA-256 because every pair is equal.

| Shape | Command | Role | Accepted artifact | Accepted/current normalized SHA-256 | Result |
|---|---|---|---|---|---|
| N1 | N1-01-filtered-count | filtered-count | `sql/N1-01-filtered-count.sql` | `71d892484ec51372fa196a1c61ba1f6d4922e85cd1b6de54c86bdba554db148f` | exact |
| N1 | N1-02-page-root | page-root | `sql/N1-02-page-root.sql` | `6ac635d49b34479191afb7d9ce1be6a3e9a19b43861c94327c2fb4988d9e665c` | exact |
| N1 | N1-03-translation-split | translation-split | `sql/N1-03-translation-split.sql` | `b37c0c307121df1df5d6119dcb576d278beff0451d32811844ed2a90e75c5403` | exact |
| N1 | N1-04-image-split | image-split | `sql/N1-04-image-split.sql` | `b48092b5fd5ce53853d5be89a99c577b6f3dce478f221f797001532a3673ad1a` | exact |
| P1 | P1-01-filtered-count | filtered-count | `sql/P1-01-filtered-count.sql` | `7e7cbca5ef4d4bc0c972a3614cdc0298f25d98c6df920a98b6d6d819823d9154` | exact |
| P1 | P1-02-page-root | page-root | `sql/P1-02-page-root.sql` | `0d4484d8900eafef50333afcbd3cd737de67ecebec433251089c8703f00a5afa` | exact |
| P1 | P1-03-translation-split | translation-split | `sql/P1-03-translation-split.sql` | `e4ccdc85713fd6d6fcee8d49b45bf969ebcc1c85bf56fc8225fea6ff68f1a653` | exact |
| P1 | P1-04-image-split | image-split | `sql/P1-04-image-split.sql` | `8b83fea33c68b4053ea5fd026ec2577976114dbce781f2aed6c27d601b8fd922` | exact |
| P2 | P2-01-filtered-count | filtered-count | `sql/P2-01-filtered-count.sql` | `b845629ded9b79684afceb9f1da29e85881a0e25fbc778adbe0a76b08e46fa97` | exact |
| P2 | P2-02-page-root | page-root | `sql/P2-02-page-root.sql` | `401516a17717d27d71166aa6771f56c919991b77a1fda8f9dbd8b133e3f4b0de` | exact |
| P2 | P2-03-translation-split | translation-split | `sql/P2-03-translation-split.sql` | `0020227ffae96e4c3acf6abe79403d4cc95c5b42121f7ce297ce9e97c9b12165` | exact |
| P2 | P2-04-image-split | image-split | `sql/P2-04-image-split.sql` | `a1ddac6b9ed5a6d51e923c16e079b21a108a274af2bca337fb797ed0f2c8c89e` | exact |
| A1 | A1-01-agency-existence | agency-existence | `sql/A1-01-agency-existence.sql` | `6957da296e3df9a17006b00cdf7862ca144caecc6d5c25bf6608e8dd0ab9caa3` | exact |
| A1 | A1-02-filtered-count | filtered-count | `sql/A1-02-filtered-count.sql` | `a506edf16752b908341e0f558d979124d9bd7718758151b7e8bae5bd9dfa363c` | exact |
| A1 | A1-03-page-root | page-root | `sql/A1-03-page-root.sql` | `78592696ad80d0326c0244e60ecfea9a3173e3d0b3799e86d5041e369ba5b23f` | exact |
| A1 | A1-04-translation-split | translation-split | `sql/A1-04-translation-split.sql` | `42d6909ae55df01699676218496223e7ff1afe65c1e89a9066a44638200bca17` | exact |
| A1 | A1-05-image-split | image-split | `sql/A1-05-image-split.sql` | `47118f996169c5eaec50e9daf97184b5a46ed53bd8605adbd1f30b2fd789409b` | exact |
| R1 | R1-01-filtered-count | filtered-count | `sql/R1-01-filtered-count.sql` | `e670819b7f690e0cc3c7bee6160226152431feb1929cd9dd797114e69b6760ed` | exact |
| R1 | R1-02-page-root | page-root | `sql/R1-02-page-root.sql` | `303e2dfe836de14043d960fdc9bfeebcba1e00c4253334868475a5576f6c9bdf` | exact |
| R1 | R1-03-translation-split | translation-split | `sql/R1-03-translation-split.sql` | `9fb4ad29408df9ae8a50997191b4796ad1506a7e809b8a094e8af7d533ca3568` | exact |
| R1 | R1-04-image-split | image-split | `sql/R1-04-image-split.sql` | `2fc8bea5437b48e10534bb17a20af4515a878545cfa249f447ce457ed7978072` | exact |
| L1 | L1-01-filtered-count | filtered-count | `sql/L1-01-filtered-count.sql` | `b796b29ddd815aa3e46243794b90ed937344f81ef811bcdb0c00b059b3e03bff` | exact |
| L1 | L1-02-page-root | page-root | `sql/L1-02-page-root.sql` | `10978b46d7ecf7e416914a1272a55bce9c2b0c28c49c29f08bc9a1bbd84dcddc` | exact |
| L1 | L1-03-translation-split | translation-split | `sql/L1-03-translation-split.sql` | `c69350705a93687913a84ec6872d74a4416465aef13728833b8474bd40e1cd62` | exact |
| L1 | L1-04-image-split | image-split | `sql/L1-04-image-split.sql` | `c1b1626cb8559db294ec739e29eb53339f605b289c5f22907a9d773d2c519814` | exact |
| Q1 | Q1-01-filtered-count | filtered-count | `sql/Q1-01-filtered-count.sql` | `024b959782407204569ce8f26427d2ee4814c31df854937e8cd0385c1d2bbce7` | exact |
| Q1 | Q1-02-page-root | page-root | `sql/Q1-02-page-root.sql` | `aaeef27c03ad826b3040e33436fd3345094cd697cc3eaea46e21d21152eb6a29` | exact |
| Q1 | Q1-03-translation-split | translation-split | `sql/Q1-03-translation-split.sql` | `a95a01bc39823b3067b0cd51a70ee380ece5dcdcc80eaa9b69708330126845a0` | exact |
| Q1 | Q1-04-image-split | image-split | `sql/Q1-04-image-split.sql` | `6de558d72f3dcac53389ca493ccb931b8a728c34aed9ee518cdfc8aaf2a6ce2e` | exact |
| C1 | C1-01-comparable-source | comparable-source | `sql/C1-01-comparable-source.sql` | `c676a6ba3f9752ef1a91862ad0df2051416bd51a4bed37a15b170c4c860f72a4` | exact |
| C1 | C1-02-comparable-ranked-root | comparable-ranked-root | `sql/C1-02-comparable-ranked-root.sql` | `204e9a1c35cbb2deb3b57f078f30be6be81a43ab359f9294a9bc5a5c4c001dc3` | exact |
| C1 | C1-03-comparable-translation-split | comparable-translation-split | `sql/C1-03-comparable-translation-split.sql` | `d105d7ed7dc5296a458706fc0e183cff3967c1979747639f6dbed81fff6d7396` | exact |
| C1 | C1-04-comparable-image-split | comparable-image-split | `sql/C1-04-comparable-image-split.sql` | `8a2bf5088ecfce33e4b295ab559a5bef2c678f320c367e50d49ffac58bd30a73` | exact |

Final comparison: **33/33 exact; 0 mismatches; 0 missing; 0 extra**.

## Frozen semantics

Current source and current generated SQL retain:

- effective translation priority: case-insensitive requested language, then `mk`, then `LanguageCode COLLATE "C"`, then translation UUID;
- one effective translation row used consistently for display, location, q, and comparable selection;
- q restricted to Title, City, Municipality, and Neighborhood;
- Description, AddressLine, coordinates, and LocationPrecision absent from q;
- Active predicate and all filters before count/page;
- count before page loading, deterministic ordering, `Skip`/`Take`, and split translation/image hydration;
- agency existence plus reuse of `IListingRepository.GetFilteredReadOnlyAsync`;
- comparable source population, exact candidate eligibility, three-tier location ranking, numeric/timestamp/UUID tie-breaks, limit 6, and split hydration;
- no location-precision ranking, readiness predicate, `Translations.Any(...)` readiness check, corruption-hiding filter, or broader comparable source projection.

The Git blob identities of `ListingRepository.cs`, `AgencyRepository.cs`, `IListingRepository.cs`, and `IAgencyRepository.cs` are byte-identical at H.6 commit `670dba5` and current `HEAD`. K.3 makes no production correction.

## Current plan and performance verification

Transient raw run: `chapter-10f-v2-baseline-20260901T144239Z-6942cdb5`.

- current profile before plan capture: 61/61;
- production capture: 33 commands, 80 typed parameters;
- raw plans: 198/198 (33 warm-up, 165 measured);
- structural plan and row-count validation: PASS;
- plan switches/anomalies: 0;
- spills: 0 across every warm-up and measured plan;
- median temp read+write blocks: 0 for every command and sequence;
- Q1 filtered-count median: 9.294 ms, below the 250 ms gate;
- Q1 aligned first-page median: 9.760 ms, below the 250 ms gate;
- Q1 count/page plans use `IX_ListingTranslations_Q_Trigram`;
- trigram index definition remains GIN over Title, City, Municipality, Neighborhood with `gin_trgm_ops`, valid/ready/live;
- Q1 shared-buffer medians: 1,163 for count and 1,263 for page root; aligned first page 1,557;
- root projection plan width: 854 for N1/P1/P2/A1/R1/L1/Q1 page roots and 954 for the comparable ranked root;
- root actual rows: 20 for paged shapes and 6 for C1 ranked candidates.

Measured aligned execution medians:

| Sequence | Execution ms | Shared blocks | Temp blocks | Spill |
|---|---:|---:|---:|---|
| N1 first page | 55.157 | 3,830 | 0 | no |
| P1 first page | 43.690 | 3,832 | 0 | no |
| P2 first page | 44.432 | 3,852 | 0 | no |
| A1 first page | 2.135 | 884 | 0 | no |
| A1 endpoint supplementary | 3.365 | 1,388 | 0 | no |
| R1 first page | 32.701 | 3,838 | 0 | no |
| L1 first page | 10.947 | 1,813 | 0 | no |
| Q1 first page | 9.760 | 1,557 | 0 | no |
| C1 candidate page | 51.949 | 21,318 | 0 | no |
| C1 endpoint supplementary | 52.147 | 21,327 | 0 | no |

These are fresh K.3 measurements under the existing five-run aligned-median method, not a new baseline and not a replacement for H.6 artifacts.

## Build, artifact freeze, and transient output

- QueryReview Release build: 0 warnings, 0 errors.
- Normal `dotnet build -c Release --no-restore`: 0 warnings, 0 errors.
- Accepted baseline diff: zero.
- Accepted baseline export: not run.
- Production/repository/query diff in K.3: zero.
- Migration/designer/snapshot diff in K.3: zero.

Ignored/transient outputs, not intended for commit:

- `C:\Users\User\AppData\Local\Temp\realestate-queryreview\chapter-10f-v1-production-sql\captured-commands.json`;
- `C:\Users\User\AppData\Local\Temp\realestate-queryreview\chapter-10f-v2-baseline-20260901T144239Z-6942cdb5\` (raw capture, 198 plans, measurements, and temporary curated verification output);
- `docs/planning/chapter-13k3-final-generated-sql-freeze-proof-evidence.md`.

The disposable container was stopped and auto-removed after verification. No deviation or blocker remains.
