# Chapter 10F deterministic benchmark profile

Current profile version: `chapter-10f-v2`

`chapter-10f-v2` is the Chapter 13J.7-compatible successor to the original Chapter 10F profile. It changes only deterministic coordinate/root ownership as described below. The committed Chapter 10F benchmark evidence remains the historical `chapter-10f-v1` run and is not rewritten or relabeled.

- C# seed: `1042001`. The current profile uses formulas rather than random sampling; the seed is recorded for later tool-local components.
- PostgreSQL seed: `SELECT setseed(0.1042001);`, executed inside the creation transaction.
- Listing ordinals: `1` through `100000`.
- Listing UUID: `40000000-0000-0000-0000-` followed by the ordinal encoded as twelve hexadecimal digits.
- Base timestamp: `2026-01-01T00:00:00Z`.

Creation uses set-based PostgreSQL statements only inside the opt-in query-review tool. Before opening the database connection, `profile create` verifies that its localhost/loopback connection port is the published `5432/tcp` port of the exact named, running, automatically removed `postgres:16-alpine` container on the local Docker engine. The name prefix and confirmation flag remain layered safeguards rather than ownership proof, and any Docker/context/container/host/port mismatch fails before migrations. The tool then applies existing EF migrations, requires empty profile tables, inserts all data in one transaction, checks every invariant before commit, and rolls back on any mismatch. After the logical profile commits, the structurally verified disposable-only tool records the `Listings` heap size, executes `VACUUM FULL (ANALYZE) public."Listings"` outside a transaction, records the compacted size, and reruns all 61 logical invariants. The physical normalization is benchmark preparation, not production maintenance guidance; it retains Draft-first insertion, translation-trigger execution, and final-status application while preventing their deterministic bulk MVCC/index churn from defining the performance baseline. `profile verify` issues only SELECT statements and does not require the container-ownership guard.

## Exact distribution

| Dimension | Locked formula or total |
|---|---|
| Users | 101 deterministic Active users; users 1-100 own agencies and user 101 owns personal listings |
| Agencies | 100 deterministic Active agencies and 100 Active Owner memberships |
| Listings | Exactly 100,000 |
| Status | 1-70,000 Active; 70,001-76,000 Draft; 76,001-82,000 Archived; 82,001-88,000 Reserved; 88,001-94,000 Sold; 94,001-100,000 Rented |
| Ownership | Even ordinals are personal; odd ordinals belong to agency `(((i-1)/2) mod 100)+1`; 50,000 each, 500 listings and 350 Active listings per agency |
| Listing type | Even ordinals Sale and odd ordinals Rent, with controlled comparable overrides and an equal-count compensation band; 50,000 each |
| Property type | `floor((i-1)/2) mod 2` alternates pairs of Apartment and House, with controlled comparable overrides and an equal-count compensation band; 50,000 each |
| Currency | `i mod 3` assigns EUR, USD, MKD with controlled comparable overrides and compensation bands; totals 33,334 EUR, 33,333 USD, 33,333 MKD |
| Active currency | 23,334 EUR, 23,333 USD, 23,333 MKD |
| Rooms | Null when `i mod 5 = 0`; otherwise `1.0 + 0.5 * (i mod 8)`; exactly 20,000 null |
| Area | `40 + (i mod 200)` square metres except the comparable cohort |
| Price | `50000 + 2500 * (i mod 120)` except the comparable cohort |
| CreatedAtUtc | Base timestamp plus `i mod 1000` minutes; all comparable rows use `2026-02-01T00:00:00Z` |
| Coordinates/root ownership | Chapter 13J.7 supersedes only the historical per-sequence coordinate ownership formula: 1-70,000 have trusted test-only confirmed roots; 70,001-87,500 are unresolved; 87,501-100,000 retain the historical `i mod 5` coordinate formula. Global totals remain 20,000 null pairs, 80,000 value pairs, and zero partial pairs. |
| Translations | Exactly two per listing and 200,000 total; 100,000 `mk`, 90,000 `en`, 5,000 `de`, 5,000 `sq` |
| Translation bands | 5,001-10,000 replace `en` with `de`; 10,001-15,000 replace `en` with `sq`; all retain `mk` |
| Details | Exactly one matching detail row per listing: 50,000 apartment and 50,000 house rows |
| Images | One primary image for every even listing and one secondary image for every tenth listing; 60,000 total |

The controlled comparable cohort changes the natural type/property/currency distribution. Three non-overlapping Active compensation bands restore the exact global and Active totals:

- odd IDs 3,033-3,061 change Rent to Sale (15 rows);
- Apartment-formula IDs 3,101-3,129 change to House (15 rows);
- ten base-EUR IDs in 3,202-3,229 change to USD, and ten in 3,232-3,259 change to MKD.

### Chapter 13J.7 coordinate/root ownership supersession

The stronger J.4 invariant requires every Active listing to have complete confirmed location truth. The fixed Active IDs 1-70,000 contain exactly 14,000 rows that the historical `i mod 5` formula left unresolved. Because Active identities and discovery results are frozen, Chapter 13J.7 owner-approves this one profile-data supersession:

- sequences 1-70,000 retain the existing deterministic coordinate formula where already paired and receive the same deterministic pair where previously unresolved; every row has precision `Approximate`, provider key `query-review-trusted-test-only`, result reference `query-review-trusted-test-only:<12-hex-sequence>`, NULL display name, and confirmation time `2026-01-01T00:00:00Z`;
- sequences 70,001-87,500 have all seven root snapshot fields NULL, deterministically displacing exactly 14,000 previously paired roots from non-Active rows;
- sequences 87,501-100,000 retain the historical non-Active formula: multiples of five have all seven root fields NULL, while other rows retain the existing deterministic coordinate pair and unverified NULL precision/provenance/display/confirmation fields.

The resulting coordinate totals remain exactly 80,000 paired, 20,000 null, and zero partial. No listing, translation, status, text, discovery cohort, query input, or expected public result identity changes. These values are trusted QueryReview fixture data only and define no production repair or backfill behavior.

## Location and text cohorts

Effective translation means the current production priority for requested `en`: case-insensitive requested language, then `mk`, then `LanguageCode COLLATE "C"`, then translation UUID.

| Cohort | Exact result |
|---|---|
| Broad Skopje | 28,000 Active effective translations with City `Skopje`, Municipality `Centar`, Neighborhood `Center` |
| Broad Bitola | 14,000 Active effective translations with City `Bitola`, Municipality `Bitola`, Neighborhood `Center` |
| Generic locations | 27,829 Active effective translations spread deterministically across 28 `BenchmarkCityNN` groups |
| Selective location | IDs 1,001-1,140: `AuditCity10F` / `AuditMunicipality10F` / `AuditNeighborhood10F`, exactly 140 |
| Broad title | Active IDs 1-10,000 contain `broadtoken10f`, exactly 10,000 |
| Selective q | Active IDs 2,001-2,120 contain `needle10f` in Title, exactly 120; no other approved q field contains it |
| Excluded decoys | IDs 15,001-15,250 contain `needle10f` only in Description; 15,251-15,500 only in AddressLine; exactly 500 |

The Skopje source range ends at ordinal 28,171. The 140 selective-location rows and 31 comparable rows within that range replace Skopje values, leaving exactly 28,000 Skopje rows. Bitola occupies 28,172-42,171. Generic Active locations occupy 42,172-70,000.

## Comparable cohort

The source is ordinal 3,001, UUID `40000000-0000-0000-0000-000000000bb9`. Candidates are ordinals 3,002-3,031. All 31 rows are Active, Rent, Apartment, EUR, selected stored language `en`, City `ComparableCity10F`, positive price/area, and share `2026-02-01T00:00:00Z`.

Location tiers contain exactly ten candidates each:

- 3,002-3,011: source municipality and source neighborhood;
- 3,012-3,021: source municipality and a different neighborhood;
- 3,022-3,031: different municipality and neighborhood.

Within each ten-row tier, the following `(area, price)` pattern is repeated:

```text
(100, 200000), (100, 200000), (100, 202000), (100, 198000),
(105, 210000), (105, 207900), (110, 220000), (110, 217800),
(120, 240000), (120, 237600)
```

The source uses area `100` and price `200000`. The cluster totals are area `3310` and price `6599900`. The pattern provides equal timestamp, equal area, equal unrounded price-per-square-metre, equal price, and UUID tie cases for the later query review without adding ranking or benchmark execution to profile creation.

## Fixed verification totals

`profile create` and `profile verify` require all 61 metrics to match, including:

- all entity totals, statuses, ownership counts, and per-agency minimum/maximum counts;
- listing/property types, total and Active currency counts;
- null rooms and paired coordinate counts;
- translation totals and exactly-two-per-listing count;
- effective-translation location and text cohort counts;
- comparable pool, tier, numeric, and timestamp totals;
- the exact 1,050-row area/room cohort;
- matching details and image totals.

Any missing, extra, or changed invariant fails the command. The fixed query inputs and performance measurements remain later Chapter 10F checkpoints.
