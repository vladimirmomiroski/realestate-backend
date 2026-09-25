# QueryReview temporary baseline summary

Run: `four-root-discovery-v1-baseline-20260925T204857Z-5443f863`

## Command medians

| Command | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-01-filtered-count | 0.063 | 21.581 | 3388 | 0 | no |
| N1-02-page-root | 1.127 | 56.512 | 3658 | 0 | no |
| N1-03-translation-split | 0.383 | 0.277 | 161 | 0 | no |
| N1-04-image-split | 0.410 | 0.215 | 133 | 0 | no |
| P1-01-filtered-count | 0.068 | 22.100 | 3388 | 0 | no |
| P1-02-page-root | 1.300 | 41.916 | 3672 | 0 | no |
| P1-03-translation-split | 0.393 | 0.452 | 161 | 0 | no |
| P1-04-image-split | 0.336 | 0.281 | 121 | 0 | no |
| P2-01-filtered-count | 0.078 | 21.714 | 3388 | 0 | no |
| P2-02-page-root | 1.187 | 42.006 | 3672 | 0 | no |
| P2-03-translation-split | 0.404 | 0.410 | 161 | 0 | no |
| P2-04-image-split | 0.417 | 0.369 | 141 | 0 | no |
| A1-01-agency-existence | 0.071 | 0.087 | 2 | 0 | no |
| A1-02-filtered-count | 0.088 | 1.008 | 502 | 0 | no |
| A1-03-page-root | 1.039 | 1.842 | 682 | 0 | no |
| A1-04-translation-split | 0.404 | 0.402 | 161 | 0 | no |
| A1-05-image-split | 0.405 | 0.238 | 121 | 0 | no |
| R1-01-filtered-count | 0.088 | 33.455 | 3388 | 0 | no |
| R1-02-page-root | 1.076 | 34.568 | 3658 | 0 | no |
| R1-03-translation-split | 0.464 | 0.378 | 161 | 0 | no |
| R1-04-image-split | 0.394 | 0.286 | 141 | 0 | no |
| L1-01-filtered-count | 2.061 | 11.512 | 1428 | 0 | no |
| L1-02-page-root | 4.140 | 11.885 | 1608 | 0 | no |
| L1-03-translation-split | 0.361 | 0.246 | 161 | 0 | no |
| L1-04-image-split | 0.385 | 0.200 | 133 | 0 | no |
| Q1-01-filtered-count | 2.105 | 9.744 | 1189 | 0 | no |
| Q1-02-page-root | 4.033 | 9.811 | 1369 | 0 | no |
| Q1-03-translation-split | 0.407 | 0.260 | 161 | 0 | no |
| Q1-04-image-split | 0.394 | 0.246 | 133 | 0 | no |
| C1-01-comparable-source | 0.227 | 0.259 | 9 | 0 | no |
| C1-02-comparable-ranked-root | 3.944 | 52.594 | 17797 | 0 | no |
| C1-03-comparable-translation-split | 0.373 | 0.190 | 49 | 0 | no |
| C1-04-comparable-image-split | 0.370 | 0.172 | 40 | 0 | no |
| commercial-unknown-first-page-01-filtered-count | 0.504 | 24.335 | 3516 | 0 | no |
| commercial-unknown-first-page-02-page-root | 1.438 | 31.402 | 3636 | 0 | no |
| commercial-unknown-first-page-03-translation-split | 0.361 | 0.345 | 161 | 0 | no |
| commercial-unknown-first-page-04-image-split | 0.326 | 0.303 | 129 | 0 | no |
| commercial-office-first-page-01-filtered-count | 0.347 | 22.354 | 3516 | 0 | no |
| commercial-office-first-page-02-page-root | 1.270 | 39.472 | 3807 | 0 | no |
| commercial-office-first-page-03-translation-split | 0.357 | 0.337 | 161 | 0 | no |
| commercial-office-first-page-04-image-split | 0.414 | 0.349 | 121 | 0 | no |
| commercial-office-root-first-page-01-filtered-count | 0.359 | 23.156 | 3516 | 0 | no |
| commercial-office-root-first-page-02-page-root | 1.485 | 42.936 | 3807 | 0 | no |
| commercial-office-root-first-page-03-translation-split | 0.440 | 0.475 | 161 | 0 | no |
| commercial-office-root-first-page-04-image-split | 0.401 | 0.231 | 121 | 0 | no |
| commercial-shop-location-01-filtered-count | 5.357 | 81.670 | 45225 | 0 | no |
| commercial-shop-location-02-page-root | 7.156 | 80.938 | 45345 | 0 | no |
| commercial-shop-location-03-translation-split | 0.349 | 0.288 | 161 | 0 | no |
| commercial-shop-location-04-image-split | 0.346 | 0.267 | 134 | 0 | no |
| commercial-other-deep-page-01-filtered-count | 0.370 | 20.698 | 3516 | 0 | no |
| commercial-other-deep-page-02-page-root | 1.241 | 26.982 | 3636 | 0 | no |
| commercial-other-deep-page-03-translation-split | 0.400 | 0.379 | 161 | 0 | no |
| commercial-other-deep-page-04-image-split | 0.435 | 0.239 | 133 | 0 | no |
| land-unknown-first-page-01-filtered-count | 0.392 | 19.570 | 3410 | 0 | no |
| land-unknown-first-page-02-page-root | 1.311 | 24.107 | 3530 | 0 | no |
| land-unknown-first-page-03-translation-split | 0.348 | 0.368 | 161 | 0 | no |
| land-unknown-first-page-04-image-split | 0.340 | 0.264 | 130 | 0 | no |
| land-building-plot-first-page-01-filtered-count | 0.435 | 21.122 | 3409 | 0 | no |
| land-building-plot-first-page-02-page-root | 1.261 | 34.163 | 3575 | 0 | no |
| land-building-plot-first-page-03-translation-split | 0.396 | 0.361 | 161 | 0 | no |
| land-building-plot-first-page-04-image-split | 0.376 | 0.320 | 138 | 0 | no |
| land-building-plot-root-first-page-01-filtered-count | 0.362 | 20.031 | 3409 | 0 | no |
| land-building-plot-root-first-page-02-page-root | 1.194 | 40.497 | 3575 | 0 | no |
| land-building-plot-root-first-page-03-translation-split | 0.362 | 0.454 | 161 | 0 | no |
| land-building-plot-root-first-page-04-image-split | 0.388 | 0.296 | 138 | 0 | no |
| land-agricultural-location-01-filtered-count | 5.117 | 46.669 | 23568 | 0 | no |
| land-agricultural-location-02-page-root | 6.761 | 45.492 | 23692 | 0 | no |
| land-agricultural-location-03-translation-split | 0.357 | 0.262 | 161 | 0 | no |
| land-agricultural-location-04-image-split | 0.386 | 0.247 | 133 | 0 | no |
| land-other-deep-page-01-filtered-count | 0.370 | 17.718 | 3396 | 0 | no |
| land-other-deep-page-02-page-root | 1.356 | 21.691 | 3516 | 0 | no |
| land-other-deep-page-03-translation-split | 0.404 | 0.344 | 161 | 0 | no |
| land-other-deep-page-04-image-split | 0.417 | 0.277 | 131 | 0 | no |
| agency-commercial-shop-first-page-01-filtered-count | 0.394 | 5.791 | 994 | 0 | no |
| agency-commercial-shop-first-page-02-page-root | 1.209 | 5.565 | 1114 | 0 | no |
| agency-commercial-shop-first-page-03-translation-split | 0.335 | 0.372 | 161 | 0 | no |
| agency-commercial-shop-first-page-04-image-split | 0.392 | 0.281 | 121 | 0 | no |
| agency-land-agricultural-deep-page-01-filtered-count | 0.464 | 3.369 | 883 | 0 | no |
| agency-land-agricultural-deep-page-02-page-root | 1.346 | 3.771 | 949 | 0 | no |
| agency-land-agricultural-deep-page-03-translation-split | 0.380 | 0.287 | 89 | 0 | no |
| agency-land-agricultural-deep-page-04-image-split | 0.371 | 0.271 | 67 | 0 | no |
| cross-family-subtypes-empty-01-filtered-count | 0.865 | 0.020 | 0 | 0 | no |
| cross-family-subtypes-empty-02-page-root | 2.485 | 0.157 | 0 | 0 | no |

## Sequence medians

| Sequence | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |
|---|---:|---:|---:|---:|---|
| N1-first-page | 1.910 | 57.072 | 3952 | 0 | no |
| P1-first-page | 2.073 | 42.968 | 3954 | 0 | no |
| P2-first-page | 2.359 | 42.912 | 3974 | 0 | no |
| A1-first-page | 1.832 | 2.523 | 964 | 0 | no |
| A1-endpoint-supplementary | 1.977 | 3.829 | 1468 | 0 | no |
| R1-first-page | 2.163 | 35.348 | 3960 | 0 | no |
| L1-first-page | 4.783 | 12.364 | 1902 | 0 | no |
| Q1-first-page | 4.845 | 10.329 | 1663 | 0 | no |
| C1-candidate-page | 4.670 | 52.910 | 17886 | 0 | no |
| C1-endpoint-supplementary | 4.864 | 53.169 | 17895 | 0 | no |
| commercial-unknown-first-page-page | 2.102 | 32.082 | 3926 | 0 | no |
| commercial-office-first-page-page | 2.166 | 40.138 | 4089 | 0 | no |
| commercial-office-root-first-page-page | 2.293 | 43.717 | 4089 | 0 | no |
| commercial-shop-location-page | 7.831 | 81.663 | 45640 | 0 | no |
| commercial-other-deep-page-page | 2.121 | 27.606 | 3930 | 0 | no |
| land-unknown-first-page-page | 2.056 | 24.801 | 3821 | 0 | no |
| land-building-plot-first-page-page | 2.070 | 34.844 | 3874 | 0 | no |
| land-building-plot-root-first-page-page | 1.998 | 41.335 | 3874 | 0 | no |
| land-agricultural-location-page | 7.479 | 46.036 | 23986 | 0 | no |
| land-other-deep-page-page | 2.139 | 22.312 | 3808 | 0 | no |
| agency-commercial-shop-first-page-page | 1.910 | 6.207 | 1396 | 0 | no |
| agency-land-agricultural-deep-page-page | 2.095 | 4.354 | 1105 | 0 | no |
| cross-family-subtypes-empty-page | 2.485 | 0.157 | 0 | 0 | no |

## Q1 gate: PASS

Filtered-count median: 9.744 ms.
First-page sequence median: 10.329 ms.
No gate failure reasons.
