# Unindexed PostgreSQL 16 subtype baseline

This directory records the Task 15G measurement point before any Commercial or Land subtype index exists. It is synthetic engineering evidence for `four-root-discovery-v1`; it is not production telemetry, a market-distribution claim, an SLO, or an index recommendation.

## Identity and protocol

- Source: `ae89a343a3c41a9c1e7f6f5c4f00fe4d86f805c3` on `benchmarks/unindexed-subtype-baseline`.
- Generation/profile/lane: `four-root-discovery-v1` / `four-root-discovery-v1` / `postgresql-16`.
- PostgreSQL: 16.14, `postgres:16-alpine`, image digest `sha256:57c72fd2a128e416c7fcc499958864df5301e940bca0a56f58fddf30ffc07777`.
- Profile: 179/179 invariants; profile SHA-256 `7d389dfbecb10fa0f491a58e6f83bda265cee1ea7e664a43b53f6fcc7c838946`; invariant manifest/result SHA-256 `bae3243bd1da79993710b3522b2f1e7c1c695152cd8dea25d61d13f9d9331be7`.
- Query contract: 21 shapes, 83 commands, 190 typed parameters, 498 plans; shape-manifest SHA-256 `fce861c91a31a8e505dd0193cc0baaad5982d5c81fc1454258f2281a7b74a979`; accepted aggregate result/order SHA-256 `975a9d37ced4e372c0aec370782bf7932101042ebcd263e14a097fad0d4cab19`.
- Preparation and samples: one `VACUUM (ANALYZE)`, one discarded warm-up per command, then five measured samples per command in fixed manifest order. Medians below use only those five measured samples.
- PostgreSQL settings: `default_statistics_target=100`, `effective_cache_size=524288 x 8kB`, `effective_io_concurrency=1`, `jit=on`, `max_parallel_workers_per_gather=2`, `random_page_cost=4`, `seq_page_cost=1`, `shared_buffers=16384 x 8kB`, and `work_mem=4096kB`.

The disposable database was loopback-only, AutoRemove, and backed by an anonymous `/var/lib/postgresql/data` volume. It was not a shared or development database.

## Unindexed catalog proof

The pre-measurement PostgreSQL catalog contained exactly two indexes across the subtype-detail tables and both were valid/ready shared-primary-key indexes:

| Table | Index | Columns | Heap bytes | Index bytes |
|---|---|---|---:|---:|
| `ListingCommercialDetails` | `PK_ListingCommercialDetails` | `ListingId` | 1,048,576 | 868,352 |
| `ListingLandDetails` | `PK_ListingLandDetails` | `ListingId` | 581,632 | 442,368 |

`non_pk_index_count=0`, `invalid_or_unready_count=0`, `CommercialType covering_index_count=0`, and `LandType covering_index_count=0`. No candidate or experiment index state was present.

Relevant relation sizes at capture were: `Listings` 27,754,496 heap / 32,423,936 total bytes; `ListingCommercialDetails` 1,048,576 / 1,949,696; `ListingLandDetails` 581,632 / 1,056,768; `ListingTranslations` 40,796,160 / 87,859,200; and `ListingImages` 13,402,112 / 21,012,480.

## Measured subtype sequences and classification

Classification is descriptive for this fixed profile and uses measured result cardinality relative to its Active root population, not enum names. The two single-filter Unknown shapes each return 40% of their Active root and are classified nonselective. All other populated subtype shapes return at most 10.01% of their Active root after their additional currency, location, rarity, or agency predicate and are classified selective. The zero-row cross-family shape is a selective correctness control, not an index-candidate assertion.

| Shape | Total | Page items | Active-root share | Class | Sequence plan ms | Sequence exec ms | Shared hit+read | Temp | Spill |
|---|---:|---:|---:|---|---:|---:|---:|---:|---|
| `commercial-unknown-first-page` | 5,600 | 20 | 40.00% | nonselective | 2.707 | 37.696 | 3,926 | 0 | no |
| `commercial-office-first-page` | 1,395 | 20 | 9.96% | selective | 2.419 | 45.035 | 4,089 | 0 | no |
| `commercial-office-root-first-page` | 1,395 | 20 | 9.96% | selective | 2.955 | 44.635 | 4,089 | 0 | no |
| `commercial-shop-location` | 1,140 | 20 | 8.14% | selective | 9.649 | 84.567 | 45,640 | 0 | no |
| `commercial-other-deep-page` | 1,400 | 20 | 10.00% | selective | 2.177 | 32.104 | 3,930 | 0 | no |
| `land-unknown-first-page` | 2,800 | 20 | 40.00% | nonselective | 2.097 | 27.372 | 3,870 | 0 | no |
| `land-building-plot-first-page` | 701 | 20 | 10.01% | selective | 2.245 | 38.461 | 3,982 | 0 | no |
| `land-building-plot-root-first-page` | 701 | 20 | 10.01% | selective | 2.281 | 39.568 | 3,982 | 0 | no |
| `land-agricultural-location` | 558 | 20 | 7.97% | selective | 9.028 | 52.238 | 24,651 | 0 | no |
| `land-other-deep-page` | 700 | 20 | 10.00% | selective | 2.640 | 32.485 | 3,871 | 0 | no |
| `agency-commercial-shop-first-page` | 137 | 20 | 0.98% | selective | 2.378 | 7.382 | 1,396 | 0 | no |
| `agency-land-agricultural-deep-page` | 71 | 11 | 1.01% | selective | 2.322 | 5.432 | 1,159 | 0 | no |
| `cross-family-subtypes-empty` | 0 | 0 | 0.00% | selective correctness control | 2.632 | 0.183 | 0 | 0 | no |

The root shares use 14,000 Active Commercial rows and 7,000 Active Land rows. Matching-root equivalence pairs retained identical totals, IDs, and order hashes.

## Count/page command observations

Actual/estimated rows below are the top-level output rows of the execution-median plan. Every complete median plan retains all node-level estimates, actuals, buffers, scan/join/sort details, and rows removed. All listed shared reads, dirtied/written blocks, and temp reads/writes were zero.

| Shape / role | Plan ms | Exec ms | Actual / estimated rows | Shared hits | Observed scans | Observed indexes |
|---|---:|---:|---:|---:|---|---|
| `commercial-unknown-first-page` count | 0.474 | 28.196 | 1 / 1 | 3,516 | Seq Scan | none |
| `commercial-unknown-first-page` page | 1.712 | 36.816 | 20 / 20 | 3,636 | Index Scan, Seq Scan | dependent PKs only |
| `commercial-office-first-page` count | 0.479 | 27.234 | 1 / 1 | 3,516 | Seq Scan | none |
| `commercial-office-first-page` page | 1.585 | 44.418 | 20 / 20 | 3,807 | Index Scan, Seq Scan | dependent PKs only |
| `commercial-office-root-first-page` count | 0.451 | 24.668 | 1 / 1 | 3,516 | Seq Scan | none |
| `commercial-office-root-first-page` page | 2.104 | 43.918 | 20 / 20 | 3,807 | Index Scan, Seq Scan | dependent PKs only |
| `commercial-shop-location` count | 6.267 | 82.760 | 1 / 1 | 45,225 | Index Only Scan, Index Scan, Seq Scan | translation/root PKs |
| `commercial-shop-location` page | 8.910 | 83.975 | 20 / 1 | 45,345 | Index Only Scan, Index Scan, Seq Scan | translation/detail/root PKs |
| `commercial-other-deep-page` count | 0.448 | 24.004 | 1 / 1 | 3,516 | Seq Scan | none |
| `commercial-other-deep-page` page | 1.295 | 31.332 | 20 / 1 | 3,636 | Index Scan, Seq Scan | dependent PKs only |
| `land-unknown-first-page` count | 0.446 | 21.951 | 1 / 1 | 3,459 | Seq Scan | none |
| `land-unknown-first-page` page | 1.161 | 26.687 | 20 / 20 | 3,579 | Index Scan, Seq Scan | dependent PKs only |
| `land-building-plot-first-page` count | 0.457 | 23.473 | 1 / 1 | 3,459 | Seq Scan | none |
| `land-building-plot-first-page` page | 1.308 | 37.377 | 20 / 20 | 3,683 | Index Scan, Seq Scan | dependent PKs only |
| `land-building-plot-root-first-page` count | 0.424 | 22.301 | 1 / 1 | 3,459 | Seq Scan | none |
| `land-building-plot-root-first-page` page | 1.412 | 38.784 | 20 / 20 | 3,683 | Index Scan, Seq Scan | dependent PKs only |
| `land-agricultural-location` count | 5.611 | 51.268 | 1 / 1 | 24,237 | Index Only Scan, Index Scan, Seq Scan | translation/root PKs |
| `land-agricultural-location` page | 8.013 | 51.605 | 20 / 1 | 24,357 | Index Only Scan, Index Scan, Seq Scan | translation/detail/root PKs |
| `land-other-deep-page` count | 0.529 | 22.759 | 1 / 1 | 3,459 | Seq Scan | none |
| `land-other-deep-page` page | 1.843 | 31.809 | 20 / 1 | 3,579 | Index Scan, Seq Scan | dependent PKs only |
| `agency-commercial-shop-first-page` count | 0.516 | 7.094 | 1 / 1 | 994 | Bitmap Heap/Index Scan, Seq Scan | `IX_Listings_AgencyId` |
| `agency-commercial-shop-first-page` page | 1.485 | 6.478 | 20 / 20 | 1,114 | Bitmap Heap/Index Scan, Index Scan, Seq Scan | agency index and dependent PKs |
| `agency-land-agricultural-deep-page` count | 0.466 | 5.089 | 1 / 1 | 937 | Bitmap Heap/Index Scan, Seq Scan | `IX_Listings_AgencyId` |
| `agency-land-agricultural-deep-page` page | 1.383 | 4.645 | 11 / 1 | 1,003 | Bitmap Heap/Index Scan, Index Scan, Seq Scan | agency index and dependent PKs |
| `cross-family-subtypes-empty` count | 0.907 | 0.020 | 1 / 1 | 0 | short-circuited | none |
| `cross-family-subtypes-empty` page | 2.632 | 0.183 | 0 / 1 | 0 | Index Scan | dependent PKs only |

The location shapes touched materially more shared buffers than the corresponding subtype-only shapes, while the agency compositions used the existing agency index. These are observations from the captured plans, not candidate selection or an index disposition.

## Artifact seal and navigation

The verified curated bundle is [`cfdde3290bcbde6edf1a95fdcc49f494ff9e84118239dd2403daaafc13a635d4`](./cfdde3290bcbde6edf1a95fdcc49f494ff9e84118239dd2403daaafc13a635d4/). Its content address is SHA-256 over the `/`-normalized `relative-path|lowercase-raw-file-sha256` lines sorted with true ordinal (`StringComparer.Ordinal`-equivalent) semantics, LF-delimited with a terminal LF. It contains 170 files and re-verifies offline as generation `four-root-discovery-v1`, lane `postgresql-16`.

Independent Task 15G audit found that the original outer directory name had been computed with culture/case-insensitive ordering. The bundle bytes were valid. The unchanged 170-file map was recomputed with true ordinal ordering, renamed to the address above, and verified byte-identical before and after the rename. No PostgreSQL measurement or bundle regeneration occurred, and the old incorrectly named directory is absent.

- `baseline-measurements.json` locks all 83 command medians and all aligned sequence medians.
- `baseline-plans/` contains each execution-median complete JSON plan, including actual/estimated rows and complete buffer/temp data.
- `sql/` contains the exact normalized SQL plus typed parameter declarations for every command.
- `environment.json` locks runtime, PostgreSQL settings, relation sizes, and index sizes.
- `experimental-manifest.json` locks the curated file set and canonical artifact hashes.

The sealed raw run passed offline verification with all 498 plans (83 discarded warm-ups and 415 measured plans), no spill/temp anomaly, exact Task 15F functional identities, and a credential-scan pass. Task 15G makes no index recommendation; candidate experiments and disposition belong to Task 15H.
