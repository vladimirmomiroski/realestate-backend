# Authoritative permanent Chapter 10F baseline summary

This is the concise permanent evidence exported from a separately retained and verified temporary raw-run directory. Warm-up and nonmedian plans are not permanent evidence.

Run: `chapter-10f-v1-baseline-20260814T112202Z-2925368b`
Benchmark commit: `2925368b087a58440b720b6a5db59e4d4d9987be`
Result hash: `7f74f991bf29b6f3ad24d48f2e8e13ecf9f375ea6f9eb0da8f18204c528bfb36` (PASS)

## Verified identity

- Profile: `chapter-10f-v1`; listings 100,000; translations 200,000; invariants 61/61.
- PostgreSQL 16.14; pg_trgm 1.6; `IX_ListingTranslations_Q_Trigram` GIN, valid/ready/live.
- Capture: 33 commands; 80 typed parameters; 198 plans; 1 warm-up and 5 measured rounds.
- Safety: spills 0; plan switches 0; anomalies 0; credential findings 0.

## Command medians

| Command | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-01-filtered-count | 0.083 | 20.685 | 2448 | 0 | no |
| N1-02-page-root | 0.647 | 57.451 | 2593 | 0 | no |
| N1-03-translation-split | 0.381 | 0.251 | 161 | 0 | no |
| N1-04-image-split | 0.515 | 0.199 | 133 | 0 | no |
| P1-01-filtered-count | 0.083 | 19.391 | 2448 | 0 | no |
| P1-02-page-root | 0.667 | 36.323 | 2548 | 0 | no |
| P1-03-translation-split | 0.460 | 0.389 | 161 | 0 | no |
| P1-04-image-split | 0.485 | 0.310 | 121 | 0 | no |
| P2-01-filtered-count | 0.089 | 17.804 | 2448 | 0 | no |
| P2-02-page-root | 0.630 | 35.765 | 2548 | 0 | no |
| P2-03-translation-split | 0.460 | 0.442 | 161 | 0 | no |
| P2-04-image-split | 0.601 | 0.439 | 141 | 0 | no |
| A1-01-agency-existence | 0.083 | 0.138 | 2 | 0 | no |
| A1-02-filtered-count | 0.105 | 0.943 | 502 | 0 | no |
| A1-03-page-root | 0.719 | 1.748 | 602 | 0 | no |
| A1-04-translation-split | 0.527 | 0.367 | 161 | 0 | no |
| A1-05-image-split | 0.499 | 0.300 | 121 | 0 | no |
| R1-01-filtered-count | 0.172 | 20.819 | 2448 | 0 | no |
| R1-02-page-root | 0.662 | 21.266 | 2548 | 0 | no |
| R1-03-translation-split | 0.384 | 0.389 | 161 | 0 | no |
| R1-04-image-split | 0.502 | 0.358 | 141 | 0 | no |
| L1-01-filtered-count | 1.895 | 10.730 | 1420 | 0 | no |
| L1-02-page-root | 2.868 | 10.488 | 1520 | 0 | no |
| L1-03-translation-split | 0.383 | 0.306 | 161 | 0 | no |
| L1-04-image-split | 0.436 | 0.299 | 133 | 0 | no |
| Q1-01-filtered-count | 2.151 | 8.795 | 1164 | 0 | no |
| Q1-02-page-root | 3.210 | 8.915 | 1264 | 0 | no |
| Q1-03-translation-split | 0.435 | 0.248 | 161 | 0 | no |
| Q1-04-image-split | 0.582 | 0.318 | 133 | 0 | no |
| C1-01-comparable-source | 0.336 | 0.259 | 9 | 0 | no |
| C1-02-comparable-ranked-root | 3.922 | 57.736 | 20329 | 0 | no |
| C1-03-comparable-translation-split | 0.392 | 0.175 | 49 | 0 | no |
| C1-04-comparable-image-split | 0.480 | 0.208 | 40 | 0 | no |

## Sequence medians

| Sequence | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-first-page | 1.533 | 57.901 | 2887 | 0 | no |
| P1-first-page | 1.667 | 37.074 | 2830 | 0 | no |
| P2-first-page | 1.678 | 36.562 | 2850 | 0 | no |
| A1-first-page | 1.803 | 2.396 | 884 | 0 | no |
| A1-endpoint-supplementary | 1.969 | 3.585 | 1388 | 0 | no |
| R1-first-page | 1.538 | 21.887 | 2850 | 0 | no |
| L1-first-page | 4.005 | 11.068 | 1814 | 0 | no |
| Q1-first-page | 4.198 | 9.433 | 1558 | 0 | no |
| C1-candidate-page | 4.787 | 58.106 | 20418 | 0 | no |
| C1-endpoint-supplementary | 5.126 | 58.439 | 20427 | 0 | no |

## Q1 acceptance: PASS

- Count: 8.795 ms; shared buffers 1164.
- First page: 9.433 ms; shared buffers 1558.
- Total: expected 120, actual 120; ordered IDs: PASS.
- Count and page plans use `IX_ListingTranslations_Q_Trigram` without the old translation search sequential scan.

## A1 approved exception: PASS

- A1-first-page: corrected pre-index 2.335 ms; indexed 2.396 ms; difference +0.061 ms (+2.61%); shared buffers 884/884.
- A1-endpoint-supplementary: corrected pre-index 3.278 ms; indexed 3.585 ms; difference +0.307 ms (+9.37%); shared buffers 1388/1388.
- Buffers equivalent: PASS; scan/join/index topology unchanged: PASS; no new expensive node: PASS.

## Integrity model

`baseline-measurements.json` carries canonical SHA-256 hashes for every SQL file, every median plan, `environment.json`, and this summary. Its terminal trust anchor is the committed Git blob/tree; it intentionally does not claim an impossible self-hash.
