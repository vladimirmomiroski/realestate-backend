# QueryReview temporary baseline summary

Run: `four-root-discovery-v1-baseline-20260925T204352Z-5443f863`

## Command medians

| Command | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-01-filtered-count | 0.067 | 21.500 | 3388 | 0 | no |
| N1-02-page-root | 1.064 | 57.213 | 3658 | 0 | no |
| N1-03-translation-split | 0.405 | 0.286 | 161 | 0 | no |
| N1-04-image-split | 0.406 | 0.221 | 133 | 0 | no |
| P1-01-filtered-count | 0.069 | 21.387 | 3388 | 0 | no |
| P1-02-page-root | 1.042 | 42.793 | 3672 | 0 | no |
| P1-03-translation-split | 0.329 | 0.406 | 161 | 0 | no |
| P1-04-image-split | 0.394 | 0.262 | 121 | 0 | no |
| P2-01-filtered-count | 0.069 | 22.212 | 3388 | 0 | no |
| P2-02-page-root | 1.186 | 44.897 | 3672 | 0 | no |
| P2-03-translation-split | 0.473 | 0.439 | 161 | 0 | no |
| P2-04-image-split | 0.464 | 0.359 | 141 | 0 | no |
| A1-01-agency-existence | 0.069 | 0.089 | 2 | 0 | no |
| A1-02-filtered-count | 0.080 | 1.085 | 502 | 0 | no |
| A1-03-page-root | 1.024 | 1.843 | 682 | 0 | no |
| A1-04-translation-split | 0.360 | 0.413 | 161 | 0 | no |
| A1-05-image-split | 0.423 | 0.290 | 121 | 0 | no |
| R1-01-filtered-count | 0.097 | 35.249 | 3388 | 0 | no |
| R1-02-page-root | 1.156 | 37.562 | 3658 | 0 | no |
| R1-03-translation-split | 0.391 | 0.440 | 161 | 0 | no |
| R1-04-image-split | 0.415 | 0.358 | 141 | 0 | no |
| L1-01-filtered-count | 2.208 | 12.614 | 1427 | 0 | no |
| L1-02-page-root | 4.629 | 12.381 | 1607 | 0 | no |
| L1-03-translation-split | 0.394 | 0.254 | 161 | 0 | no |
| L1-04-image-split | 0.342 | 0.224 | 133 | 0 | no |
| Q1-01-filtered-count | 2.424 | 9.649 | 1188 | 0 | no |
| Q1-02-page-root | 4.828 | 10.206 | 1368 | 0 | no |
| Q1-03-translation-split | 0.445 | 0.342 | 161 | 0 | no |
| Q1-04-image-split | 0.485 | 0.226 | 133 | 0 | no |
| C1-01-comparable-source | 0.234 | 0.349 | 9 | 0 | no |
| C1-02-comparable-ranked-root | 4.448 | 65.524 | 17797 | 0 | no |
| C1-03-comparable-translation-split | 0.397 | 0.195 | 49 | 0 | no |
| C1-04-comparable-image-split | 0.454 | 0.146 | 40 | 0 | no |
| commercial-unknown-first-page-01-filtered-count | 0.434 | 25.436 | 3431 | 0 | no |
| commercial-unknown-first-page-02-page-root | 1.531 | 33.589 | 3551 | 0 | no |
| commercial-unknown-first-page-03-translation-split | 0.475 | 0.331 | 161 | 0 | no |
| commercial-unknown-first-page-04-image-split | 0.382 | 0.358 | 129 | 0 | no |
| commercial-office-first-page-01-filtered-count | 0.467 | 23.782 | 3420 | 0 | no |
| commercial-office-first-page-02-page-root | 1.451 | 42.426 | 3743 | 0 | no |
| commercial-office-first-page-03-translation-split | 0.484 | 0.419 | 161 | 0 | no |
| commercial-office-first-page-04-image-split | 0.392 | 0.385 | 121 | 0 | no |
| commercial-office-root-first-page-01-filtered-count | 0.521 | 22.693 | 3420 | 0 | no |
| commercial-office-root-first-page-02-page-root | 1.703 | 40.698 | 3743 | 0 | no |
| commercial-office-root-first-page-03-translation-split | 0.406 | 0.458 | 161 | 0 | no |
| commercial-office-root-first-page-04-image-split | 0.342 | 0.284 | 121 | 0 | no |
| commercial-shop-location-01-filtered-count | 5.853 | 76.925 | 43870 | 0 | no |
| commercial-shop-location-02-page-root | 8.755 | 76.592 | 43996 | 0 | no |
| commercial-shop-location-03-translation-split | 0.392 | 0.336 | 161 | 0 | no |
| commercial-shop-location-04-image-split | 0.397 | 0.299 | 134 | 0 | no |
| commercial-other-deep-page-01-filtered-count | 0.429 | 20.311 | 3401 | 0 | no |
| commercial-other-deep-page-02-page-root | 1.146 | 26.425 | 3521 | 0 | no |
| commercial-other-deep-page-03-translation-split | 0.437 | 0.333 | 161 | 0 | no |
| commercial-other-deep-page-04-image-split | 0.449 | 0.322 | 133 | 0 | no |
| land-unknown-first-page-01-filtered-count | 0.413 | 21.077 | 3459 | 0 | no |
| land-unknown-first-page-02-page-root | 1.281 | 24.925 | 3579 | 0 | no |
| land-unknown-first-page-03-translation-split | 0.391 | 0.323 | 161 | 0 | no |
| land-unknown-first-page-04-image-split | 0.456 | 0.254 | 130 | 0 | no |
| land-building-plot-first-page-01-filtered-count | 0.413 | 22.567 | 3459 | 0 | no |
| land-building-plot-first-page-02-page-root | 1.197 | 36.813 | 3683 | 0 | no |
| land-building-plot-first-page-03-translation-split | 0.388 | 0.449 | 161 | 0 | no |
| land-building-plot-first-page-04-image-split | 0.447 | 0.337 | 138 | 0 | no |
| land-building-plot-root-first-page-01-filtered-count | 0.393 | 20.682 | 3459 | 0 | no |
| land-building-plot-root-first-page-02-page-root | 1.360 | 34.798 | 3683 | 0 | no |
| land-building-plot-root-first-page-03-translation-split | 0.411 | 0.422 | 161 | 0 | no |
| land-building-plot-root-first-page-04-image-split | 0.341 | 0.331 | 138 | 0 | no |
| land-agricultural-location-01-filtered-count | 4.957 | 48.895 | 24237 | 0 | no |
| land-agricultural-location-02-page-root | 7.268 | 48.289 | 24357 | 0 | no |
| land-agricultural-location-03-translation-split | 0.352 | 0.351 | 161 | 0 | no |
| land-agricultural-location-04-image-split | 0.435 | 0.264 | 133 | 0 | no |
| land-other-deep-page-01-filtered-count | 0.410 | 19.112 | 3459 | 0 | no |
| land-other-deep-page-02-page-root | 1.419 | 22.691 | 3579 | 0 | no |
| land-other-deep-page-03-translation-split | 0.372 | 0.319 | 161 | 0 | no |
| land-other-deep-page-04-image-split | 0.450 | 0.313 | 131 | 0 | no |
| agency-commercial-shop-first-page-01-filtered-count | 0.499 | 4.774 | 892 | 0 | no |
| agency-commercial-shop-first-page-02-page-root | 1.255 | 4.949 | 1012 | 0 | no |
| agency-commercial-shop-first-page-03-translation-split | 0.354 | 0.400 | 161 | 0 | no |
| agency-commercial-shop-first-page-04-image-split | 0.454 | 0.233 | 121 | 0 | no |
| agency-land-agricultural-deep-page-01-filtered-count | 0.372 | 4.346 | 940 | 0 | no |
| agency-land-agricultural-deep-page-02-page-root | 1.266 | 4.099 | 1006 | 0 | no |
| agency-land-agricultural-deep-page-03-translation-split | 0.407 | 0.241 | 89 | 0 | no |
| agency-land-agricultural-deep-page-04-image-split | 0.460 | 0.291 | 67 | 0 | no |
| cross-family-subtypes-empty-01-filtered-count | 0.946 | 0.019 | 0 | 0 | no |
| cross-family-subtypes-empty-02-page-root | 2.118 | 0.196 | 0 | 0 | no |

## Sequence medians

| Sequence | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-first-page | 1.949 | 57.753 | 3952 | 0 | no |
| P1-first-page | 1.826 | 43.532 | 3954 | 0 | no |
| P2-first-page | 2.114 | 46.264 | 3974 | 0 | no |
| A1-first-page | 1.884 | 2.568 | 964 | 0 | no |
| A1-endpoint-supplementary | 2.033 | 3.801 | 1468 | 0 | no |
| R1-first-page | 2.000 | 38.435 | 3960 | 0 | no |
| L1-first-page | 5.321 | 12.826 | 1901 | 0 | no |
| Q1-first-page | 5.629 | 10.742 | 1662 | 0 | no |
| C1-candidate-page | 5.411 | 65.911 | 17886 | 0 | no |
| C1-endpoint-supplementary | 5.645 | 66.104 | 17895 | 0 | no |
| commercial-unknown-first-page-page | 2.455 | 34.305 | 3841 | 0 | no |
| commercial-office-first-page-page | 2.397 | 43.187 | 4025 | 0 | no |
| commercial-office-root-first-page-page | 2.423 | 41.628 | 4025 | 0 | no |
| commercial-shop-location-page | 9.753 | 77.248 | 44291 | 0 | no |
| commercial-other-deep-page-page | 1.986 | 26.999 | 3815 | 0 | no |
| land-unknown-first-page-page | 2.195 | 25.626 | 3870 | 0 | no |
| land-building-plot-first-page-page | 2.178 | 38.058 | 3982 | 0 | no |
| land-building-plot-root-first-page-page | 2.251 | 35.660 | 3982 | 0 | no |
| land-agricultural-location-page | 8.031 | 48.976 | 24651 | 0 | no |
| land-other-deep-page-page | 2.380 | 23.323 | 3871 | 0 | no |
| agency-commercial-shop-first-page-page | 2.095 | 5.702 | 1294 | 0 | no |
| agency-land-agricultural-deep-page-page | 2.170 | 4.627 | 1162 | 0 | no |
| cross-family-subtypes-empty-page | 2.118 | 0.196 | 0 | 0 | no |

## Q1 gate: PASS

Filtered-count median: 9.649 ms.
First-page sequence median: 10.742 ms.
No gate failure reasons.
