# Chapter 10F temporary baseline summary

Run: `chapter-10f-v1-baseline-20260721T183427Z-0232b031`

## Command medians

| Command | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-01-filtered-count | 0.071 | 25.211 | 2447 | 0 | no |
| N1-02-page-root | 0.545 | 74.074 | 2592 | 0 | no |
| N1-03-translation-split | 0.392 | 0.317 | 161 | 0 | no |
| N1-04-image-split | 0.414 | 0.267 | 133 | 0 | no |
| P1-01-filtered-count | 0.104 | 22.225 | 2447 | 0 | no |
| P1-02-page-root | 0.550 | 41.414 | 2547 | 0 | no |
| P1-03-translation-split | 0.361 | 0.398 | 161 | 0 | no |
| P1-04-image-split | 0.419 | 0.322 | 121 | 0 | no |
| P2-01-filtered-count | 0.082 | 23.037 | 2447 | 0 | no |
| P2-02-page-root | 0.513 | 42.296 | 2547 | 0 | no |
| P2-03-translation-split | 0.387 | 0.445 | 161 | 0 | no |
| P2-04-image-split | 0.439 | 0.332 | 141 | 0 | no |
| A1-01-agency-existence | 0.094 | 0.083 | 2 | 0 | no |
| A1-02-filtered-count | 0.096 | 1.033 | 502 | 0 | no |
| A1-03-page-root | 0.517 | 1.907 | 602 | 0 | no |
| A1-04-translation-split | 0.423 | 0.384 | 161 | 0 | no |
| A1-05-image-split | 0.427 | 0.291 | 121 | 0 | no |
| R1-01-filtered-count | 0.134 | 23.023 | 2447 | 0 | no |
| R1-02-page-root | 0.530 | 24.144 | 2547 | 0 | no |
| R1-03-translation-split | 0.368 | 0.387 | 161 | 0 | no |
| R1-04-image-split | 0.428 | 0.320 | 141 | 0 | no |
| L1-01-filtered-count | 1.761 | 12.386 | 1406 | 0 | no |
| L1-02-page-root | 2.922 | 13.112 | 1506 | 0 | no |
| L1-03-translation-split | 0.416 | 0.320 | 161 | 0 | no |
| L1-04-image-split | 0.472 | 0.293 | 133 | 0 | no |
| Q1-01-filtered-count | 1.994 | 12.464 | 1148 | 0 | no |
| Q1-02-page-root | 3.365 | 12.182 | 1248 | 0 | no |
| Q1-03-translation-split | 0.387 | 0.321 | 161 | 0 | no |
| Q1-04-image-split | 0.424 | 0.288 | 133 | 0 | no |
| C1-01-comparable-source | 0.298 | 0.403 | 9 | 0 | no |
| C1-02-comparable-ranked-root | 3.090 | 77.191 | 20328 | 0 | no |
| C1-03-comparable-translation-split | 0.393 | 0.201 | 49 | 0 | no |
| C1-04-comparable-image-split | 0.450 | 0.196 | 40 | 0 | no |

## Sequence medians

| Sequence | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-first-page | 1.354 | 74.591 | 2886 | 0 | no |
| P1-first-page | 1.312 | 42.092 | 2829 | 0 | no |
| P2-first-page | 1.374 | 43.061 | 2849 | 0 | no |
| A1-first-page | 1.493 | 2.590 | 884 | 0 | no |
| A1-endpoint-supplementary | 1.702 | 3.649 | 1388 | 0 | no |
| R1-first-page | 1.339 | 24.851 | 2849 | 0 | no |
| L1-first-page | 3.809 | 13.686 | 1800 | 0 | no |
| Q1-first-page | 4.324 | 12.883 | 1542 | 0 | no |
| C1-candidate-page | 3.900 | 77.588 | 20417 | 0 | no |
| C1-endpoint-supplementary | 4.194 | 78.016 | 20426 | 0 | no |

## Q1 gate: PASS

Filtered-count median: 12.464 ms.
First-page sequence median: 12.883 ms.
No gate failure reasons.
