# QueryReview temporary baseline summary

Run: `four-root-discovery-v1-baseline-20260925T204112Z-5443f863`

## Command medians

| Command | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-01-filtered-count | 0.066 | 22.584 | 3388 | 0 | no |
| N1-02-page-root | 1.124 | 55.438 | 3658 | 0 | no |
| N1-03-translation-split | 0.439 | 0.313 | 161 | 0 | no |
| N1-04-image-split | 0.425 | 0.245 | 133 | 0 | no |
| P1-01-filtered-count | 0.068 | 23.092 | 3388 | 0 | no |
| P1-02-page-root | 1.182 | 44.218 | 3672 | 0 | no |
| P1-03-translation-split | 0.527 | 0.440 | 161 | 0 | no |
| P1-04-image-split | 0.443 | 0.317 | 121 | 0 | no |
| P2-01-filtered-count | 0.101 | 21.851 | 3388 | 0 | no |
| P2-02-page-root | 1.240 | 46.455 | 3672 | 0 | no |
| P2-03-translation-split | 0.490 | 0.401 | 161 | 0 | no |
| P2-04-image-split | 0.389 | 0.374 | 141 | 0 | no |
| A1-01-agency-existence | 0.083 | 0.110 | 2 | 0 | no |
| A1-02-filtered-count | 0.081 | 1.048 | 502 | 0 | no |
| A1-03-page-root | 0.996 | 1.918 | 682 | 0 | no |
| A1-04-translation-split | 0.357 | 0.340 | 161 | 0 | no |
| A1-05-image-split | 0.466 | 0.313 | 121 | 0 | no |
| R1-01-filtered-count | 0.093 | 36.240 | 3388 | 0 | no |
| R1-02-page-root | 1.120 | 35.584 | 3658 | 0 | no |
| R1-03-translation-split | 0.446 | 0.378 | 161 | 0 | no |
| R1-04-image-split | 0.532 | 0.387 | 141 | 0 | no |
| L1-01-filtered-count | 2.387 | 58.038 | 4753 | 0 | no |
| L1-02-page-root | 5.087 | 57.018 | 4933 | 0 | no |
| L1-03-translation-split | 0.500 | 0.288 | 161 | 0 | no |
| L1-04-image-split | 0.442 | 0.207 | 133 | 0 | no |
| Q1-01-filtered-count | 2.250 | 55.545 | 4455 | 0 | no |
| Q1-02-page-root | 4.699 | 55.398 | 4635 | 0 | no |
| Q1-03-translation-split | 0.375 | 0.378 | 161 | 0 | no |
| Q1-04-image-split | 0.448 | 0.299 | 133 | 0 | no |
| C1-01-comparable-source | 0.273 | 0.220 | 9 | 0 | no |
| C1-02-comparable-ranked-root | 4.211 | 53.646 | 17797 | 0 | no |
| C1-03-comparable-translation-split | 0.400 | 0.232 | 49 | 0 | no |
| C1-04-comparable-image-split | 0.448 | 0.142 | 40 | 0 | no |
| commercial-unknown-first-page-01-filtered-count | 0.518 | 26.161 | 3516 | 0 | no |
| commercial-unknown-first-page-02-page-root | 1.471 | 33.557 | 3636 | 0 | no |
| commercial-unknown-first-page-03-translation-split | 0.423 | 0.411 | 161 | 0 | no |
| commercial-unknown-first-page-04-image-split | 0.538 | 0.283 | 129 | 0 | no |
| commercial-office-first-page-01-filtered-count | 0.498 | 24.160 | 3420 | 0 | no |
| commercial-office-first-page-02-page-root | 1.637 | 42.421 | 3743 | 0 | no |
| commercial-office-first-page-03-translation-split | 0.439 | 0.369 | 161 | 0 | no |
| commercial-office-first-page-04-image-split | 0.431 | 0.275 | 121 | 0 | no |
| commercial-office-root-first-page-01-filtered-count | 0.515 | 23.058 | 3420 | 0 | no |
| commercial-office-root-first-page-02-page-root | 1.438 | 43.324 | 3743 | 0 | no |
| commercial-office-root-first-page-03-translation-split | 0.417 | 0.398 | 161 | 0 | no |
| commercial-office-root-first-page-04-image-split | 0.491 | 0.325 | 121 | 0 | no |
| commercial-shop-location-01-filtered-count | 6.530 | 80.515 | 43870 | 0 | no |
| commercial-shop-location-02-page-root | 8.900 | 76.640 | 43996 | 0 | no |
| commercial-shop-location-03-translation-split | 0.405 | 0.360 | 161 | 0 | no |
| commercial-shop-location-04-image-split | 0.452 | 0.299 | 134 | 0 | no |
| commercial-other-deep-page-01-filtered-count | 0.454 | 21.097 | 3401 | 0 | no |
| commercial-other-deep-page-02-page-root | 1.062 | 26.352 | 3521 | 0 | no |
| commercial-other-deep-page-03-translation-split | 0.379 | 0.360 | 161 | 0 | no |
| commercial-other-deep-page-04-image-split | 0.520 | 0.260 | 133 | 0 | no |
| land-unknown-first-page-01-filtered-count | 0.334 | 21.044 | 3459 | 0 | no |
| land-unknown-first-page-02-page-root | 1.247 | 25.137 | 3579 | 0 | no |
| land-unknown-first-page-03-translation-split | 0.369 | 0.309 | 161 | 0 | no |
| land-unknown-first-page-04-image-split | 0.406 | 0.386 | 130 | 0 | no |
| land-building-plot-first-page-01-filtered-count | 0.398 | 22.092 | 3459 | 0 | no |
| land-building-plot-first-page-02-page-root | 1.175 | 38.117 | 3683 | 0 | no |
| land-building-plot-first-page-03-translation-split | 0.382 | 0.430 | 161 | 0 | no |
| land-building-plot-first-page-04-image-split | 0.457 | 0.319 | 138 | 0 | no |
| land-building-plot-root-first-page-01-filtered-count | 0.425 | 21.211 | 3459 | 0 | no |
| land-building-plot-root-first-page-02-page-root | 1.254 | 35.446 | 3683 | 0 | no |
| land-building-plot-root-first-page-03-translation-split | 0.324 | 0.414 | 161 | 0 | no |
| land-building-plot-root-first-page-04-image-split | 0.415 | 0.323 | 138 | 0 | no |
| land-agricultural-location-01-filtered-count | 4.808 | 47.486 | 24237 | 0 | no |
| land-agricultural-location-02-page-root | 6.873 | 50.272 | 24357 | 0 | no |
| land-agricultural-location-03-translation-split | 0.347 | 0.348 | 161 | 0 | no |
| land-agricultural-location-04-image-split | 0.375 | 0.321 | 133 | 0 | no |
| land-other-deep-page-01-filtered-count | 0.461 | 19.545 | 3459 | 0 | no |
| land-other-deep-page-02-page-root | 1.675 | 23.762 | 3579 | 0 | no |
| land-other-deep-page-03-translation-split | 0.335 | 0.314 | 161 | 0 | no |
| land-other-deep-page-04-image-split | 0.359 | 0.312 | 131 | 0 | no |
| agency-commercial-shop-first-page-01-filtered-count | 0.494 | 5.262 | 866 | 0 | no |
| agency-commercial-shop-first-page-02-page-root | 1.243 | 4.971 | 986 | 0 | no |
| agency-commercial-shop-first-page-03-translation-split | 0.374 | 0.395 | 161 | 0 | no |
| agency-commercial-shop-first-page-04-image-split | 0.377 | 0.330 | 121 | 0 | no |
| agency-land-agricultural-deep-page-01-filtered-count | 0.358 | 4.289 | 914 | 0 | no |
| agency-land-agricultural-deep-page-02-page-root | 1.315 | 4.255 | 980 | 0 | no |
| agency-land-agricultural-deep-page-03-translation-split | 0.417 | 0.288 | 89 | 0 | no |
| agency-land-agricultural-deep-page-04-image-split | 0.427 | 0.193 | 67 | 0 | no |
| cross-family-subtypes-empty-01-filtered-count | 0.897 | 0.018 | 0 | 0 | no |
| cross-family-subtypes-empty-02-page-root | 2.290 | 0.189 | 0 | 0 | no |

## Sequence medians

| Sequence | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-first-page | 2.049 | 56.268 | 3952 | 0 | no |
| P1-first-page | 2.132 | 45.067 | 3954 | 0 | no |
| P2-first-page | 2.092 | 47.157 | 3974 | 0 | no |
| A1-first-page | 1.832 | 2.559 | 964 | 0 | no |
| A1-endpoint-supplementary | 2.073 | 3.734 | 1468 | 0 | no |
| R1-first-page | 1.944 | 36.395 | 3960 | 0 | no |
| L1-first-page | 6.032 | 57.679 | 5227 | 0 | no |
| Q1-first-page | 5.500 | 56.096 | 4929 | 0 | no |
| C1-candidate-page | 5.218 | 53.961 | 17886 | 0 | no |
| C1-endpoint-supplementary | 5.473 | 54.264 | 17895 | 0 | no |
| commercial-unknown-first-page-page | 2.406 | 34.398 | 3926 | 0 | no |
| commercial-office-first-page-page | 2.462 | 43.223 | 4025 | 0 | no |
| commercial-office-root-first-page-page | 2.501 | 44.134 | 4025 | 0 | no |
| commercial-shop-location-page | 9.893 | 77.403 | 44291 | 0 | no |
| commercial-other-deep-page-page | 1.960 | 27.110 | 3815 | 0 | no |
| land-unknown-first-page-page | 2.044 | 25.745 | 3870 | 0 | no |
| land-building-plot-first-page-page | 2.058 | 39.067 | 3982 | 0 | no |
| land-building-plot-root-first-page-page | 2.095 | 36.261 | 3982 | 0 | no |
| land-agricultural-location-page | 7.692 | 50.863 | 24651 | 0 | no |
| land-other-deep-page-page | 2.353 | 24.412 | 3871 | 0 | no |
| agency-commercial-shop-first-page-page | 1.985 | 5.846 | 1268 | 0 | no |
| agency-land-agricultural-deep-page-page | 2.341 | 4.695 | 1136 | 0 | no |
| cross-family-subtypes-empty-page | 2.290 | 0.189 | 0 | 0 | no |

## Q1 gate: PASS

Filtered-count median: 55.545 ms.
First-page sequence median: 56.096 ms.
No gate failure reasons.
