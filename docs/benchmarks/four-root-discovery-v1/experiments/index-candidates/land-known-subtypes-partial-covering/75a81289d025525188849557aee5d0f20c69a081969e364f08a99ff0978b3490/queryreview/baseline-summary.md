# QueryReview temporary baseline summary

Run: `four-root-discovery-v1-baseline-20260925T204626Z-5443f863`

## Command medians

| Command | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-01-filtered-count | 0.070 | 22.097 | 3388 | 0 | no |
| N1-02-page-root | 1.045 | 58.441 | 3658 | 0 | no |
| N1-03-translation-split | 0.513 | 0.406 | 161 | 0 | no |
| N1-04-image-split | 0.422 | 0.262 | 133 | 0 | no |
| P1-01-filtered-count | 0.080 | 22.301 | 3388 | 0 | no |
| P1-02-page-root | 1.160 | 45.748 | 3672 | 0 | no |
| P1-03-translation-split | 0.433 | 0.428 | 161 | 0 | no |
| P1-04-image-split | 0.458 | 0.328 | 121 | 0 | no |
| P2-01-filtered-count | 0.070 | 21.517 | 3388 | 0 | no |
| P2-02-page-root | 1.071 | 43.600 | 3672 | 0 | no |
| P2-03-translation-split | 0.459 | 0.405 | 161 | 0 | no |
| P2-04-image-split | 0.369 | 0.294 | 141 | 0 | no |
| A1-01-agency-existence | 0.071 | 0.086 | 2 | 0 | no |
| A1-02-filtered-count | 0.083 | 1.022 | 502 | 0 | no |
| A1-03-page-root | 0.947 | 1.837 | 682 | 0 | no |
| A1-04-translation-split | 0.432 | 0.390 | 161 | 0 | no |
| A1-05-image-split | 0.453 | 0.274 | 121 | 0 | no |
| R1-01-filtered-count | 0.099 | 34.055 | 3388 | 0 | no |
| R1-02-page-root | 1.033 | 36.235 | 3658 | 0 | no |
| R1-03-translation-split | 0.424 | 0.348 | 161 | 0 | no |
| R1-04-image-split | 0.440 | 0.327 | 141 | 0 | no |
| L1-01-filtered-count | 1.993 | 51.915 | 4753 | 0 | no |
| L1-02-page-root | 3.593 | 49.754 | 4933 | 0 | no |
| L1-03-translation-split | 0.384 | 0.235 | 161 | 0 | no |
| L1-04-image-split | 0.471 | 0.204 | 133 | 0 | no |
| Q1-01-filtered-count | 2.320 | 49.804 | 4455 | 0 | no |
| Q1-02-page-root | 3.406 | 49.899 | 4635 | 0 | no |
| Q1-03-translation-split | 0.339 | 0.274 | 161 | 0 | no |
| Q1-04-image-split | 0.402 | 0.212 | 133 | 0 | no |
| C1-01-comparable-source | 0.223 | 0.208 | 9 | 0 | no |
| C1-02-comparable-ranked-root | 3.540 | 50.325 | 17797 | 0 | no |
| C1-03-comparable-translation-split | 0.388 | 0.186 | 49 | 0 | no |
| C1-04-comparable-image-split | 0.411 | 0.147 | 40 | 0 | no |
| commercial-unknown-first-page-01-filtered-count | 0.348 | 25.032 | 3516 | 0 | no |
| commercial-unknown-first-page-02-page-root | 1.374 | 33.429 | 3636 | 0 | no |
| commercial-unknown-first-page-03-translation-split | 0.400 | 0.332 | 161 | 0 | no |
| commercial-unknown-first-page-04-image-split | 0.453 | 0.342 | 129 | 0 | no |
| commercial-office-first-page-01-filtered-count | 0.449 | 24.335 | 3516 | 0 | no |
| commercial-office-first-page-02-page-root | 1.382 | 51.019 | 3807 | 0 | no |
| commercial-office-first-page-03-translation-split | 0.402 | 0.383 | 161 | 0 | no |
| commercial-office-first-page-04-image-split | 0.433 | 0.238 | 121 | 0 | no |
| commercial-office-root-first-page-01-filtered-count | 0.461 | 23.978 | 3516 | 0 | no |
| commercial-office-root-first-page-02-page-root | 1.247 | 41.241 | 3807 | 0 | no |
| commercial-office-root-first-page-03-translation-split | 0.436 | 0.433 | 161 | 0 | no |
| commercial-office-root-first-page-04-image-split | 0.406 | 0.275 | 121 | 0 | no |
| commercial-shop-location-01-filtered-count | 4.659 | 80.175 | 45225 | 0 | no |
| commercial-shop-location-02-page-root | 7.792 | 73.530 | 45345 | 0 | no |
| commercial-shop-location-03-translation-split | 0.436 | 0.278 | 161 | 0 | no |
| commercial-shop-location-04-image-split | 0.356 | 0.277 | 134 | 0 | no |
| commercial-other-deep-page-01-filtered-count | 0.445 | 21.323 | 3516 | 0 | no |
| commercial-other-deep-page-02-page-root | 1.172 | 26.167 | 3636 | 0 | no |
| commercial-other-deep-page-03-translation-split | 0.336 | 0.303 | 161 | 0 | no |
| commercial-other-deep-page-04-image-split | 0.425 | 0.241 | 133 | 0 | no |
| land-unknown-first-page-01-filtered-count | 0.465 | 19.815 | 3459 | 0 | no |
| land-unknown-first-page-02-page-root | 1.170 | 24.232 | 3579 | 0 | no |
| land-unknown-first-page-03-translation-split | 0.382 | 0.360 | 161 | 0 | no |
| land-unknown-first-page-04-image-split | 0.471 | 0.309 | 130 | 0 | no |
| land-building-plot-first-page-01-filtered-count | 0.454 | 21.122 | 3409 | 0 | no |
| land-building-plot-first-page-02-page-root | 1.309 | 38.643 | 3575 | 0 | no |
| land-building-plot-first-page-03-translation-split | 0.398 | 0.431 | 161 | 0 | no |
| land-building-plot-first-page-04-image-split | 0.410 | 0.345 | 138 | 0 | no |
| land-building-plot-root-first-page-01-filtered-count | 0.593 | 21.202 | 3409 | 0 | no |
| land-building-plot-root-first-page-02-page-root | 1.295 | 35.591 | 3575 | 0 | no |
| land-building-plot-root-first-page-03-translation-split | 0.379 | 0.450 | 161 | 0 | no |
| land-building-plot-root-first-page-04-image-split | 0.423 | 0.333 | 138 | 0 | no |
| land-agricultural-location-01-filtered-count | 4.989 | 45.450 | 23568 | 0 | no |
| land-agricultural-location-02-page-root | 7.156 | 46.301 | 23692 | 0 | no |
| land-agricultural-location-03-translation-split | 0.424 | 0.379 | 161 | 0 | no |
| land-agricultural-location-04-image-split | 0.472 | 0.274 | 133 | 0 | no |
| land-other-deep-page-01-filtered-count | 0.479 | 19.028 | 3396 | 0 | no |
| land-other-deep-page-02-page-root | 1.020 | 21.198 | 3516 | 0 | no |
| land-other-deep-page-03-translation-split | 0.354 | 0.310 | 161 | 0 | no |
| land-other-deep-page-04-image-split | 0.381 | 0.255 | 131 | 0 | no |
| agency-commercial-shop-first-page-01-filtered-count | 0.535 | 5.828 | 972 | 0 | no |
| agency-commercial-shop-first-page-02-page-root | 0.946 | 5.517 | 1092 | 0 | no |
| agency-commercial-shop-first-page-03-translation-split | 0.353 | 0.362 | 161 | 0 | no |
| agency-commercial-shop-first-page-04-image-split | 0.459 | 0.245 | 121 | 0 | no |
| agency-land-agricultural-deep-page-01-filtered-count | 0.496 | 3.489 | 861 | 0 | no |
| agency-land-agricultural-deep-page-02-page-root | 1.121 | 3.943 | 927 | 0 | no |
| agency-land-agricultural-deep-page-03-translation-split | 0.377 | 0.259 | 89 | 0 | no |
| agency-land-agricultural-deep-page-04-image-split | 0.415 | 0.218 | 67 | 0 | no |
| cross-family-subtypes-empty-01-filtered-count | 0.868 | 0.019 | 0 | 0 | no |
| cross-family-subtypes-empty-02-page-root | 1.889 | 0.166 | 0 | 0 | no |

## Sequence medians

| Sequence | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-first-page | 1.965 | 59.059 | 3952 | 0 | no |
| P1-first-page | 2.206 | 46.489 | 3954 | 0 | no |
| P2-first-page | 1.992 | 44.300 | 3974 | 0 | no |
| A1-first-page | 1.904 | 2.636 | 964 | 0 | no |
| A1-endpoint-supplementary | 2.054 | 3.736 | 1468 | 0 | no |
| R1-first-page | 1.953 | 36.851 | 3960 | 0 | no |
| L1-first-page | 4.448 | 50.193 | 5227 | 0 | no |
| Q1-first-page | 4.152 | 50.370 | 4929 | 0 | no |
| C1-candidate-page | 4.507 | 50.627 | 17886 | 0 | no |
| C1-endpoint-supplementary | 4.694 | 50.808 | 17895 | 0 | no |
| commercial-unknown-first-page-page | 2.369 | 34.072 | 3926 | 0 | no |
| commercial-office-first-page-page | 2.148 | 51.552 | 4089 | 0 | no |
| commercial-office-root-first-page-page | 2.276 | 42.089 | 4089 | 0 | no |
| commercial-shop-location-page | 8.454 | 74.209 | 45640 | 0 | no |
| commercial-other-deep-page-page | 1.963 | 26.801 | 3930 | 0 | no |
| land-unknown-first-page-page | 2.100 | 24.897 | 3870 | 0 | no |
| land-building-plot-first-page-page | 2.205 | 39.355 | 3874 | 0 | no |
| land-building-plot-root-first-page-page | 2.273 | 36.418 | 3874 | 0 | no |
| land-agricultural-location-page | 7.966 | 47.131 | 23986 | 0 | no |
| land-other-deep-page-page | 1.758 | 21.834 | 3808 | 0 | no |
| agency-commercial-shop-first-page-page | 1.870 | 6.337 | 1374 | 0 | no |
| agency-land-agricultural-deep-page-page | 2.071 | 4.565 | 1083 | 0 | no |
| cross-family-subtypes-empty-page | 1.889 | 0.166 | 0 | 0 | no |

## Q1 gate: PASS

Filtered-count median: 49.804 ms.
First-page sequence median: 50.370 ms.
No gate failure reasons.
