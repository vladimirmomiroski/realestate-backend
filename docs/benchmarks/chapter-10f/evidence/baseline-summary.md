# Authoritative permanent Chapter 10F baseline summary

This is the concise permanent evidence exported from a separately retained and verified temporary raw-run directory. Warm-up and nonmedian plans are not permanent evidence.

Run: `chapter-10f-v1-baseline-20260721T201941Z-db3ad322`
Benchmark commit: `db3ad3220f58a23b752c62d2f33b0a01fff864f1`
Result hash: `7f74f991bf29b6f3ad24d48f2e8e13ecf9f375ea6f9eb0da8f18204c528bfb36` (PASS)

## Verified identity

- Profile: `chapter-10f-v1`; listings 100,000; translations 200,000; invariants 61/61.
- PostgreSQL 16.14; pg_trgm 1.6; `IX_ListingTranslations_Q_Trigram` GIN, valid/ready/live.
- Capture: 33 commands; 80 typed parameters; 198 plans; 1 warm-up and 5 measured rounds.
- Safety: spills 0; plan switches 0; anomalies 0; credential findings 0.

## Command medians

| Command | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-01-filtered-count | 0.071 | 21.381 | 2447 | 0 | no |
| N1-02-page-root | 0.482 | 56.460 | 2592 | 0 | no |
| N1-03-translation-split | 0.366 | 0.252 | 161 | 0 | no |
| N1-04-image-split | 0.364 | 0.249 | 133 | 0 | no |
| P1-01-filtered-count | 0.085 | 19.379 | 2447 | 0 | no |
| P1-02-page-root | 0.485 | 40.688 | 2547 | 0 | no |
| P1-03-translation-split | 0.324 | 0.365 | 161 | 0 | no |
| P1-04-image-split | 0.359 | 0.253 | 121 | 0 | no |
| P2-01-filtered-count | 0.076 | 18.352 | 2447 | 0 | no |
| P2-02-page-root | 0.424 | 36.412 | 2547 | 0 | no |
| P2-03-translation-split | 0.319 | 0.377 | 161 | 0 | no |
| P2-04-image-split | 0.350 | 0.297 | 141 | 0 | no |
| A1-01-agency-existence | 0.078 | 0.071 | 2 | 0 | no |
| A1-02-filtered-count | 0.105 | 0.866 | 502 | 0 | no |
| A1-03-page-root | 0.467 | 1.597 | 602 | 0 | no |
| A1-04-translation-split | 0.384 | 0.391 | 161 | 0 | no |
| A1-05-image-split | 0.357 | 0.252 | 121 | 0 | no |
| R1-01-filtered-count | 0.091 | 20.394 | 2447 | 0 | no |
| R1-02-page-root | 0.474 | 21.346 | 2547 | 0 | no |
| R1-03-translation-split | 0.330 | 0.353 | 161 | 0 | no |
| R1-04-image-split | 0.381 | 0.309 | 141 | 0 | no |
| L1-01-filtered-count | 1.715 | 11.179 | 1406 | 0 | no |
| L1-02-page-root | 2.854 | 11.123 | 1506 | 0 | no |
| L1-03-translation-split | 0.311 | 0.254 | 161 | 0 | no |
| L1-04-image-split | 0.351 | 0.204 | 133 | 0 | no |
| Q1-01-filtered-count | 1.830 | 9.419 | 1148 | 0 | no |
| Q1-02-page-root | 3.157 | 9.928 | 1248 | 0 | no |
| Q1-03-translation-split | 0.351 | 0.249 | 161 | 0 | no |
| Q1-04-image-split | 0.345 | 0.230 | 133 | 0 | no |
| C1-01-comparable-source | 0.233 | 0.234 | 9 | 0 | no |
| C1-02-comparable-ranked-root | 3.526 | 62.721 | 20328 | 0 | no |
| C1-03-comparable-translation-split | 0.379 | 0.211 | 49 | 0 | no |
| C1-04-comparable-image-split | 0.411 | 0.166 | 40 | 0 | no |

## Sequence medians

| Sequence | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-first-page | 1.230 | 56.925 | 2886 | 0 | no |
| P1-first-page | 1.168 | 41.397 | 2829 | 0 | no |
| P2-first-page | 1.091 | 37.122 | 2849 | 0 | no |
| A1-first-page | 1.257 | 2.184 | 884 | 0 | no |
| A1-endpoint-supplementary | 1.409 | 3.119 | 1388 | 0 | no |
| R1-first-page | 1.201 | 22.137 | 2849 | 0 | no |
| L1-first-page | 3.448 | 11.578 | 1800 | 0 | no |
| Q1-first-page | 3.788 | 10.584 | 1542 | 0 | no |
| C1-candidate-page | 4.345 | 63.048 | 20417 | 0 | no |
| C1-endpoint-supplementary | 4.722 | 63.325 | 20426 | 0 | no |

## Q1 acceptance: PASS

- Count: 9.419 ms; shared buffers 1148.
- First page: 10.584 ms; shared buffers 1542.
- Total: expected 120, actual 120; ordered IDs: PASS.
- Count and page plans use `IX_ListingTranslations_Q_Trigram` without the old translation search sequential scan.

## A1 approved exception: PASS

- A1-first-page: corrected pre-index 2.335 ms; indexed 2.184 ms; difference -0.151 ms (-6.47%); shared buffers 884/884.
- A1-endpoint-supplementary: corrected pre-index 3.278 ms; indexed 3.119 ms; difference -0.159 ms (-4.85%); shared buffers 1388/1388.
- Buffers equivalent: PASS; scan/join/index topology unchanged: PASS; no new expensive node: PASS.

## Integrity model

`baseline-measurements.json` carries canonical SHA-256 hashes for every SQL file, every median plan, `environment.json`, and this summary. Its terminal trust anchor is the committed Git blob/tree; it intentionally does not claim an impossible self-hash.
