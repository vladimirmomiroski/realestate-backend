# QueryReview temporary baseline summary

Run: `four-root-discovery-v1-baseline-20260923T151437Z-ae89a343`

## Command medians

| Command | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-01-filtered-count | 0.079 | 24.161 | 3388 | 0 | no |
| N1-02-page-root | 1.181 | 62.147 | 3658 | 0 | no |
| N1-03-translation-split | 0.480 | 0.343 | 161 | 0 | no |
| N1-04-image-split | 0.544 | 0.363 | 133 | 0 | no |
| P1-01-filtered-count | 0.081 | 24.128 | 3388 | 0 | no |
| P1-02-page-root | 1.366 | 47.309 | 3672 | 0 | no |
| P1-03-translation-split | 0.387 | 0.523 | 161 | 0 | no |
| P1-04-image-split | 0.442 | 0.318 | 121 | 0 | no |
| P2-01-filtered-count | 0.079 | 23.892 | 3388 | 0 | no |
| P2-02-page-root | 1.255 | 49.921 | 3672 | 0 | no |
| P2-03-translation-split | 0.566 | 0.460 | 161 | 0 | no |
| P2-04-image-split | 0.431 | 0.405 | 141 | 0 | no |
| A1-01-agency-existence | 0.078 | 0.142 | 2 | 0 | no |
| A1-02-filtered-count | 0.141 | 1.373 | 502 | 0 | no |
| A1-03-page-root | 1.200 | 2.472 | 682 | 0 | no |
| A1-04-translation-split | 0.438 | 0.435 | 161 | 0 | no |
| A1-05-image-split | 0.490 | 0.296 | 121 | 0 | no |
| R1-01-filtered-count | 0.099 | 38.059 | 3388 | 0 | no |
| R1-02-page-root | 1.145 | 37.567 | 3658 | 0 | no |
| R1-03-translation-split | 0.524 | 0.412 | 161 | 0 | no |
| R1-04-image-split | 0.475 | 0.300 | 141 | 0 | no |
| L1-01-filtered-count | 2.193 | 13.103 | 1428 | 0 | no |
| L1-02-page-root | 4.754 | 13.115 | 1608 | 0 | no |
| L1-03-translation-split | 0.390 | 0.357 | 161 | 0 | no |
| L1-04-image-split | 0.381 | 0.310 | 133 | 0 | no |
| Q1-01-filtered-count | 2.395 | 10.883 | 1189 | 0 | no |
| Q1-02-page-root | 5.277 | 10.604 | 1369 | 0 | no |
| Q1-03-translation-split | 0.469 | 0.252 | 161 | 0 | no |
| Q1-04-image-split | 0.363 | 0.245 | 133 | 0 | no |
| C1-01-comparable-source | 0.286 | 0.328 | 9 | 0 | no |
| C1-02-comparable-ranked-root | 4.798 | 56.628 | 17797 | 0 | no |
| C1-03-comparable-translation-split | 0.413 | 0.178 | 49 | 0 | no |
| C1-04-comparable-image-split | 0.515 | 0.186 | 40 | 0 | no |
| commercial-unknown-first-page-01-filtered-count | 0.474 | 28.196 | 3516 | 0 | no |
| commercial-unknown-first-page-02-page-root | 1.712 | 36.816 | 3636 | 0 | no |
| commercial-unknown-first-page-03-translation-split | 0.433 | 0.432 | 161 | 0 | no |
| commercial-unknown-first-page-04-image-split | 0.436 | 0.328 | 129 | 0 | no |
| commercial-office-first-page-01-filtered-count | 0.479 | 27.234 | 3516 | 0 | no |
| commercial-office-first-page-02-page-root | 1.585 | 44.418 | 3807 | 0 | no |
| commercial-office-first-page-03-translation-split | 0.391 | 0.382 | 161 | 0 | no |
| commercial-office-first-page-04-image-split | 0.432 | 0.309 | 121 | 0 | no |
| commercial-office-root-first-page-01-filtered-count | 0.451 | 24.668 | 3516 | 0 | no |
| commercial-office-root-first-page-02-page-root | 2.104 | 43.918 | 3807 | 0 | no |
| commercial-office-root-first-page-03-translation-split | 0.435 | 0.446 | 161 | 0 | no |
| commercial-office-root-first-page-04-image-split | 0.454 | 0.313 | 121 | 0 | no |
| commercial-shop-location-01-filtered-count | 6.267 | 82.760 | 45225 | 0 | no |
| commercial-shop-location-02-page-root | 8.910 | 83.975 | 45345 | 0 | no |
| commercial-shop-location-03-translation-split | 0.395 | 0.307 | 161 | 0 | no |
| commercial-shop-location-04-image-split | 0.468 | 0.351 | 134 | 0 | no |
| commercial-other-deep-page-01-filtered-count | 0.448 | 24.004 | 3516 | 0 | no |
| commercial-other-deep-page-02-page-root | 1.295 | 31.332 | 3636 | 0 | no |
| commercial-other-deep-page-03-translation-split | 0.469 | 0.453 | 161 | 0 | no |
| commercial-other-deep-page-04-image-split | 0.482 | 0.267 | 133 | 0 | no |
| land-unknown-first-page-01-filtered-count | 0.446 | 21.951 | 3459 | 0 | no |
| land-unknown-first-page-02-page-root | 1.161 | 26.687 | 3579 | 0 | no |
| land-unknown-first-page-03-translation-split | 0.386 | 0.424 | 161 | 0 | no |
| land-unknown-first-page-04-image-split | 0.472 | 0.350 | 130 | 0 | no |
| land-building-plot-first-page-01-filtered-count | 0.457 | 23.473 | 3459 | 0 | no |
| land-building-plot-first-page-02-page-root | 1.308 | 37.377 | 3683 | 0 | no |
| land-building-plot-first-page-03-translation-split | 0.365 | 0.423 | 161 | 0 | no |
| land-building-plot-first-page-04-image-split | 0.475 | 0.423 | 138 | 0 | no |
| land-building-plot-root-first-page-01-filtered-count | 0.424 | 22.301 | 3459 | 0 | no |
| land-building-plot-root-first-page-02-page-root | 1.412 | 38.784 | 3683 | 0 | no |
| land-building-plot-root-first-page-03-translation-split | 0.403 | 0.435 | 161 | 0 | no |
| land-building-plot-root-first-page-04-image-split | 0.522 | 0.308 | 138 | 0 | no |
| land-agricultural-location-01-filtered-count | 5.611 | 51.268 | 24237 | 0 | no |
| land-agricultural-location-02-page-root | 8.013 | 51.605 | 24357 | 0 | no |
| land-agricultural-location-03-translation-split | 0.464 | 0.325 | 161 | 0 | no |
| land-agricultural-location-04-image-split | 0.462 | 0.344 | 133 | 0 | no |
| land-other-deep-page-01-filtered-count | 0.529 | 22.759 | 3459 | 0 | no |
| land-other-deep-page-02-page-root | 1.843 | 31.809 | 3579 | 0 | no |
| land-other-deep-page-03-translation-split | 0.395 | 0.410 | 161 | 0 | no |
| land-other-deep-page-04-image-split | 0.458 | 0.315 | 131 | 0 | no |
| agency-commercial-shop-first-page-01-filtered-count | 0.516 | 7.094 | 994 | 0 | no |
| agency-commercial-shop-first-page-02-page-root | 1.485 | 6.478 | 1114 | 0 | no |
| agency-commercial-shop-first-page-03-translation-split | 0.473 | 0.462 | 161 | 0 | no |
| agency-commercial-shop-first-page-04-image-split | 0.436 | 0.322 | 121 | 0 | no |
| agency-land-agricultural-deep-page-01-filtered-count | 0.466 | 5.089 | 937 | 0 | no |
| agency-land-agricultural-deep-page-02-page-root | 1.383 | 4.645 | 1003 | 0 | no |
| agency-land-agricultural-deep-page-03-translation-split | 0.408 | 0.299 | 89 | 0 | no |
| agency-land-agricultural-deep-page-04-image-split | 0.459 | 0.271 | 67 | 0 | no |
| cross-family-subtypes-empty-01-filtered-count | 0.907 | 0.020 | 0 | 0 | no |
| cross-family-subtypes-empty-02-page-root | 2.632 | 0.183 | 0 | 0 | no |

## Sequence medians

| Sequence | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-first-page | 2.312 | 62.975 | 3952 | 0 | no |
| P1-first-page | 2.182 | 48.166 | 3954 | 0 | no |
| P2-first-page | 2.232 | 50.922 | 3974 | 0 | no |
| A1-first-page | 2.211 | 3.310 | 964 | 0 | no |
| A1-endpoint-supplementary | 2.441 | 4.369 | 1468 | 0 | no |
| R1-first-page | 2.244 | 38.439 | 3960 | 0 | no |
| L1-first-page | 5.590 | 13.701 | 1902 | 0 | no |
| Q1-first-page | 6.230 | 11.194 | 1663 | 0 | no |
| C1-candidate-page | 5.914 | 57.107 | 17886 | 0 | no |
| C1-endpoint-supplementary | 6.200 | 57.468 | 17895 | 0 | no |
| commercial-unknown-first-page-page | 2.707 | 37.696 | 3926 | 0 | no |
| commercial-office-first-page-page | 2.419 | 45.035 | 4089 | 0 | no |
| commercial-office-root-first-page-page | 2.955 | 44.635 | 4089 | 0 | no |
| commercial-shop-location-page | 9.649 | 84.567 | 45640 | 0 | no |
| commercial-other-deep-page-page | 2.177 | 32.104 | 3930 | 0 | no |
| land-unknown-first-page-page | 2.097 | 27.372 | 3870 | 0 | no |
| land-building-plot-first-page-page | 2.245 | 38.461 | 3982 | 0 | no |
| land-building-plot-root-first-page-page | 2.281 | 39.568 | 3982 | 0 | no |
| land-agricultural-location-page | 9.028 | 52.238 | 24651 | 0 | no |
| land-other-deep-page-page | 2.640 | 32.485 | 3871 | 0 | no |
| agency-commercial-shop-first-page-page | 2.378 | 7.382 | 1396 | 0 | no |
| agency-land-agricultural-deep-page-page | 2.322 | 5.432 | 1159 | 0 | no |
| cross-family-subtypes-empty-page | 2.632 | 0.183 | 0 | 0 | no |

## Q1 gate: PASS

Filtered-count median: 10.883 ms.
First-page sequence median: 11.194 ms.
No gate failure reasons.
