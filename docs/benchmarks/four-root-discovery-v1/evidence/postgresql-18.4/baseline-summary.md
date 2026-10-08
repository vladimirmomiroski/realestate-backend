# PostgreSQL 18.4 Compatibility Evidence

PostgreSQL 16 remains the authoritative Chapter 15 correctness lane, historical-comparison lane, performance lane, and index-decision lane.
PostgreSQL 18.4 is a bounded compatibility/performance-observation lane.
Cross-major timing is observational and is not an SLA.

Generation/profile: `four-root-discovery-v1`.
Run: `four-root-discovery-v1-baseline-20261008T104300Z-fde28a95`.
Capture commit: `fde28a9543831a10fb7eaa3720f779e63adf91e3`.
Disposition: Commercial `NO_INDEX`; Land `NO_INDEX`; migrations 21.
Profile: 179/179 invariants; shape identity `fce861c91a31a8e505dd0193cc0baaad5982d5c81fc1454258f2281a7b74a979`; result/order identity `975a9d37ced4e372c0aec370782bf7932101042ebcd263e14a097fad0d4cab19`.
Capture: 83 commands; 190 typed parameters; 498 plans; 1 warm-up and 5 measured rounds.
Safety: spills 0; plan switches 0; anomalies 0; credential findings 0.

## Cross-major review rules

- Threshold A: PostgreSQL 18.4 is both more than 25% and more than 2 ms slower than the contemporaneous PostgreSQL 16 measured median.
- Threshold B: PostgreSQL 18.4 uses more than 20% additional median shared-access blocks.
- Medians use only the five measured samples; discarded warm-ups are excluded.

## Cross-major sequence comparison

| Sequence | PG16 ms | PG18.4 ms | Delta ms | Delta % | PG16 blocks | PG18.4 blocks | Block delta | Block % | A | B |
|---|---:|---:|---:|---:|---:|---:|---:|---:|:---:|:---:|
| N1-first-page | 60.548 | 53.382 | -7.166 | -11.835% | 3952 | 3840 | -112 | -2.834% | no | no |
| P1-first-page | 45.860 | 41.491 | -4.369 | -9.527% | 3954 | 3956 | +2 | +0.051% | no | no |
| P2-first-page | 51.434 | 43.530 | -7.904 | -15.367% | 3974 | 3976 | +2 | +0.050% | no | no |
| A1-first-page | 2.788 | 1.993 | -0.795 | -28.515% | 964 | 964 | 0 | 0.000% | no | no |
| A1-endpoint-supplementary | 4.373 | 2.926 | -1.447 | -33.089% | 1468 | 1468 | 0 | 0.000% | no | no |
| R1-first-page | 43.712 | 35.779 | -7.933 | -18.148% | 3960 | 3962 | +2 | +0.051% | no | no |
| L1-first-page | 66.259 | 9.030 | -57.229 | -86.372% | 5227 | 1229 | -3998 | -76.487% | no | no |
| Q1-first-page | 62.402 | 10.957 | -51.445 | -82.441% | 4929 | 1070 | -3859 | -78.292% | no | no |
| C1-candidate-page | 62.731 | 46.945 | -15.786 | -25.165% | 17886 | 17736 | -150 | -0.839% | no | no |
| C1-endpoint-supplementary | 63.081 | 47.150 | -15.931 | -25.255% | 17895 | 17745 | -150 | -0.838% | no | no |
| commercial-unknown-first-page-page | 36.362 | 33.908 | -2.454 | -6.749% | 3926 | 3884 | -42 | -1.070% | no | no |
| commercial-office-first-page-page | 51.896 | 37.310 | -14.586 | -28.106% | 4089 | 4091 | +2 | +0.049% | no | no |
| commercial-office-root-first-page-page | 61.414 | 38.370 | -23.044 | -37.522% | 4089 | 4091 | +2 | +0.049% | no | no |
| commercial-shop-location-page | 91.967 | 78.049 | -13.918 | -15.134% | 45640 | 45586 | -54 | -0.118% | no | no |
| commercial-other-deep-page-page | 32.085 | 25.931 | -6.154 | -19.180% | 3930 | 3870 | -60 | -1.527% | no | no |
| land-unknown-first-page-page | 31.176 | 23.993 | -7.183 | -23.040% | 3870 | 3828 | -42 | -1.085% | no | no |
| land-building-plot-first-page-page | 43.191 | 31.952 | -11.239 | -26.022% | 4100 | 3924 | -176 | -4.293% | no | no |
| land-building-plot-root-first-page-page | 41.826 | 33.072 | -8.754 | -20.930% | 3982 | 3924 | -58 | -1.457% | no | no |
| land-agricultural-location-page | 54.871 | 49.642 | -5.229 | -9.530% | 24651 | 24585 | -66 | -0.268% | no | no |
| land-other-deep-page-page | 26.444 | 20.836 | -5.608 | -21.207% | 3871 | 3817 | -54 | -1.395% | no | no |
| agency-commercial-shop-first-page-page | 7.827 | 5.311 | -2.516 | -32.145% | 1373 | 1386 | +13 | +0.947% | no | no |
| agency-land-agricultural-deep-page-page | 5.941 | 3.985 | -1.956 | -32.924% | 1136 | 1149 | +13 | +1.144% | no | no |
| cross-family-subtypes-empty-page | 0.166 | 0.052 | -0.114 | -68.675% | 0 | 0 | 0 | 0.000% | no | no |

- Threshold A exceedances: 0.
- Threshold B exceedances: 0.

## Lane-qualified plan/topology review

- L1-first-page: PostgreSQL 16-only [scan:Seq Scan;relation:Listings;index:-]; PostgreSQL 18.4-only [none].
- L1-first-page: PostgreSQL 18.4 eliminates the PostgreSQL 16 named `Listings` sequential scan.
- Q1-first-page: PostgreSQL 16-only [scan:Seq Scan;relation:Listings;index:-]; PostgreSQL 18.4-only [none].
- Q1-first-page: PostgreSQL 18.4 eliminates the PostgreSQL 16 named `Listings` sequential scan.
- Required Q1 `IX_ListingTranslations_Q_Trigram` behavior remains intact: PASS.

All SQL, typed parameters, totals, selected IDs, order identities, settings, and sample protocol match the verified contemporaneous PostgreSQL 16 comparison.
