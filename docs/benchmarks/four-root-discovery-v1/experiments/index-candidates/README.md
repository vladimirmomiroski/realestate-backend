# PostgreSQL 16 subtype-index candidate measurements

This directory contains the four Task 15H Checkpoint B experiments locked by `docs/planning/chapter-15h-checkpoint-a-candidate-designs.md`. The Checkpoint B measurements below record factual candidate observations only and grant no migration authority.

The accepted final Task 15H decision is recorded in [`disposition.md`](disposition.md): Commercial = `NO_INDEX`, Land = `NO_INDEX`, combined-winner confirmation is not eligible, and conditional Task 15I is skipped.

## Identity and protocol

- Unindexed comparison: `../unindexed/cfdde3290bcbde6edf1a95fdcc49f494ff9e84118239dd2403daaafc13a635d4/`.
- Accepted measurement lineage: `ae89a343a3c41a9c1e7f6f5c4f00fe4d86f805c3`; measured HEAD: `5443f863d1ffea0a6331d7e91315e9a1f5767b4f`.
- Source-equivalent identities: `src` tree `367529280368366f8216471daff0c2d9e7984ccd`; `tools` tree `8ff9a3c7af656698da7d75b90bca0eb8c6005ccc`; `tests` tree `43e778665e85fb51136da269c839800792e5d713`; `RealEstate.slnx` blob `d73fa4577cbacadd72ac790876e330343aa398fb`.
- PostgreSQL 16.14, `postgres:16-alpine`, immutable image `sha256:57c72fd2a128e416c7fcc499958864df5301e940bca0a56f58fddf30ffc07777`; each run used a separate loopback-only AutoRemove container and anonymous `/var/lib/postgresql/data` volume.
- Profile: 179 invariants; profile SHA-256 `7d389dfbecb10fa0f491a58e6f83bda265cee1ea7e664a43b53f6fcc7c838946`; invariant manifest/result SHA-256 `bae3243bd1da79993710b3522b2f1e7c1c695152cd8dea25d61d13f9d9331be7`.
- Query contract: 21 shapes, 83 commands, 190 parameters, 498 plans; shape-manifest SHA-256 `fce861c91a31a8e505dd0193cc0baaad5982d5c81fc1454258f2281a7b74a979`; aggregate result/order SHA-256 `975a9d37ced4e372c0aec370782bf7932101042ebcd263e14a097fad0d4cab19`; raw result SHA-256 `726498202dd52bbc13f680f221a6a44ff0223fd383f89c7b08eacd6a6ce82f81`.
- Read protocol: locked statistics preparation, one discarded warm-up and five measured samples in fixed order for all 83 commands. Medians use only the five measured samples.
- Write protocol: for both the fixed insert and update probes, one discarded warm-up plus five unindexed and five candidate samples. Every sample is a rolled-back transaction with affected-row proof equal to one and complete `EXPLAIN (ANALYZE, BUFFERS, WAL, SETTINGS, SUMMARY, FORMAT JSON)` output.

Every pre-DDL catalog snapshot contains exactly the valid/ready/live shared PK indexes on `ListingCommercialDetails` and `ListingLandDetails`. Every post-DDL snapshot records exactly one additional locked B-tree candidate with exact definition, key order, predicate, validity/readiness/liveness, index bytes, and dependent heap bytes.

## Complete bundle sealing

Each candidate directory is a complete 205-file artifact:

- 170 offline-verifiable QueryReview curated files under `queryreview/`;
- 24 raw write-probe files: warm-up plus five measured samples for two probes and two phases;
- pre- and post-DDL catalog snapshots;
- exact DDL/build-duration record;
- independently recomputable write summary and factual gate record;
- experiment/source/environment identity;
- four sanitized execution logs under the trackable `command-output/` directory; and
- `complete-bundle-manifest.json`.

The manifest lists every evidence payload file except itself as `relative path + raw SHA-256`, using `/` path normalization and true ordinal ordering. The outer directory address hashes every file, including the manifest, as ordinal `relative-path|lowercase-raw-sha256` lines joined with LF and a terminal LF. This avoids a self-referential manifest while making the complete bundle content-addressed. Independent verification reproduced every manifest hash and all four directory addresses.

## Audit-correction mapping and catalog facts

The prior 170-file-only directories were superseded and removed after the complete bundles verified:

| Candidate | Superseded SHA | Corrected complete-bundle SHA | Raw run | Build ms | Index / heap bytes |
|---|---|---|---|---:|---:|
| `commercial-known-subtypes-partial-covering` | `f4b141253f33619f39d0998c4064d0af18a4f6b63c372612fc5d7d2ec1bccdf4` | [`ffb72a234d1046863f4c084fb2a2da4c687c36ee289df40a94084d750c21b2de`](commercial-known-subtypes-partial-covering/ffb72a234d1046863f4c084fb2a2da4c687c36ee289df40a94084d750c21b2de/) | `four-root-discovery-v1-baseline-20260925T204112Z-5443f863` | 180.1553 | 507,904 / 1,048,576 |
| `commercial-subtypes-full-covering` | `ef10ca5c8f69f8324d8fd79a6d6a782d64148cbeac15f41f19e28d46c0a70d7e` | [`43d7d5f534e13edd524d10760d979274d369f900424d90f47fdde20247f4fe0f`](commercial-subtypes-full-covering/43d7d5f534e13edd524d10760d979274d369f900424d90f47fdde20247f4fe0f/) | `four-root-discovery-v1-baseline-20260925T204352Z-5443f863` | 201.0047 | 827,392 / 1,048,576 |
| `land-known-subtypes-partial-covering` | `dd1db25928deda1e0e1ca59a0e94f7acd2efc33cdabad4a12a3ba62cca62b62f` | [`75a81289d025525188849557aee5d0f20c69a081969e364f08a99ff0978b3490`](land-known-subtypes-partial-covering/75a81289d025525188849557aee5d0f20c69a081969e364f08a99ff0978b3490/) | `four-root-discovery-v1-baseline-20260925T204626Z-5443f863` | 185.2134 | 327,680 / 581,632 |
| `land-subtypes-full-covering` | `8bf4723177d3a0ffe88eb497310407fdda5833d51050249132f1e41b00a83daf` | [`bc60ff5272f3c1b59640f255b0dfae6048c4de0b802cfdc8e591fc56a2aa1079`](land-subtypes-full-covering/bc60ff5272f3c1b59640f255b0dfae6048c4de0b802cfdc8e591fc56a2aa1079/) | `four-root-discovery-v1-baseline-20260925T204857Z-5443f863` | 163.6534 | 483,328 / 581,632 |

Exact locked DDL is preserved in each `ddl/build.json`; no candidate definition or threshold changed.

## Qualifying-family measurements

Positive percentages are reductions from the accepted 15G median. Each command in a qualifying trio must independently reduce shared-access blocks by at least 25% and execution time by at least 20%.

| Candidate | Command | Blocks (base -> candidate) | Block reduction | Time ms (base -> candidate) | Time reduction | Intended use |
|---|---|---:|---:|---:|---:|---:|
| Commercial partial | Office count | 3,516 -> 3,420 | 2.730% | 27.234 -> 24.160 | 11.287% | 6/6 |
| Commercial partial | Office page-root | 3,807 -> 3,743 | 1.681% | 44.418 -> 42.421 | 4.496% | 6/6 |
| Commercial partial | Other deep page-root | 3,636 -> 3,521 | 3.163% | 31.332 -> 26.352 | 15.894% | 6/6 |
| Commercial full | Office count | 3,516 -> 3,420 | 2.730% | 27.234 -> 23.782 | 12.675% | 6/6 |
| Commercial full | Office page-root | 3,807 -> 3,743 | 1.681% | 44.418 -> 42.426 | 4.485% | 6/6 |
| Commercial full | Other deep page-root | 3,636 -> 3,521 | 3.163% | 31.332 -> 26.425 | 15.661% | 6/6 |
| Land partial | BuildingPlot count | 3,459 -> 3,409 | 1.446% | 23.473 -> 21.122 | 10.016% | 6/6 |
| Land partial | BuildingPlot page-root | 3,683 -> 3,575 | 2.932% | 37.377 -> 38.643 | -3.387% | 6/6 |
| Land partial | Other deep page-root | 3,579 -> 3,516 | 1.760% | 31.809 -> 21.198 | 33.358% | 6/6 |
| Land full | BuildingPlot count | 3,459 -> 3,409 | 1.446% | 23.473 -> 21.122 | 10.016% | 6/6 |
| Land full | BuildingPlot page-root | 3,683 -> 3,575 | 2.932% | 37.377 -> 34.163 | 8.599% | 6/6 |
| Land full | Other deep page-root | 3,579 -> 3,516 | 1.760% | 31.809 -> 21.691 | 31.809% | 6/6 |

Every candidate fails Gate 3 because every qualifying trio has shared-block reductions below 25%. This preserves the prior combined-run ineligibility; it is not the final table disposition.

## Fixed write probes

| Candidate | Probe | Median ms (unindexed -> candidate) | Time delta | Shared hits | WAL records/bytes |
|---|---|---:|---:|---:|---:|
| Commercial partial | insert | 1.902 -> 2.011 | +5.731% | 8 -> 10 | 3/207 -> 4/287 |
| Commercial partial | update | 0.362 -> 0.420 | +16.022% | 17 -> 21 | 4/278 -> 5/358 |
| Commercial full | insert | 2.016 -> 1.898 | -5.853% | 8 -> 10 | 3/207 -> 4/287 |
| Commercial full | update | 0.359 -> 0.358 | -0.279% | 17 -> 21 | 4/278 -> 5/358 |
| Land partial | insert | 1.816 -> 1.785 | -1.707% | 7 -> 9 | 2/156 -> 3/244 |
| Land partial | update | 0.302 -> 0.321 | +6.291% | 16 -> 20 | 3/233 -> 4/329 |
| Land full | insert | 1.732 -> 1.683 | -2.829% | 7 -> 9 | 2/156 -> 3/244 |
| Land full | update | 0.295 -> 0.316 | +7.119% | 16 -> 20 | 3/233 -> 4/329 |

Every warm-up and measured probe affected exactly one row inside its transaction and restored the original subtype and `xmin` after rollback. Each machine-readable summary explicitly records five-sample medians and deltas for planning/execution time, shared hit/read/dirtied/written blocks, temp blocks, and WAL records/full-page-images/bytes. All values independently recompute from the raw files. No write probe is both more than 10% and more than 2 ms slower, and each candidate is smaller than its dependent heap; Gate 6 passes for all four.

## Factual gates

| Candidate | G1 correctness | G2 stable use | G3 trio | G4 protected | G5 plan/anomaly | G6 write/storage | All gates |
|---|---|---|---|---|---|---|---|
| Commercial partial | PASS | PASS | **FAIL** | **FAIL** | PASS | PASS | **FAIL** |
| Commercial full | PASS | PASS | **FAIL** | PASS | PASS | PASS | **FAIL** |
| Land partial | PASS | PASS | **FAIL** | **FAIL** | PASS | PASS | **FAIL** |
| Land full | PASS | PASS | **FAIL** | PASS | PASS | PASS | **FAIL** |

Gate 4 failures in the corrected runs are preserved in each `gate-observation.json`. Commercial partial and Land partial each record L1/Q1 block and sequence regressions; the two full candidates record none. Gate 5 passes with 498/498 plans, no anomaly, spill, or temp-access command, and valid/ready/live catalog state. Since no Commercial and no Land candidate passed all gates, the combined-winner precondition is false and no fifth run occurred.

## Scope guard

- No production, EF, migration, QueryReview, test, profile, API, OpenAPI, or permanent-evidence source changed.
- Migration inventory remains 21; candidate DDL existed only in disposed fresh databases.
- No candidate container remains and `docs/benchmarks/four-root-discovery-v1/evidence` remains absent.
- The four old incomplete directories are absent. Only the complete corrected bundles above remain.
- This checkpoint does not select `INDEX` or `NO_INDEX`.
