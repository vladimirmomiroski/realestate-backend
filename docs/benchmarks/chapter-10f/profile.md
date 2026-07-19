# Chapter 10F deterministic benchmark profile

Profile version: `chapter-10f-v1`

- C# seed: `1042001`. The current profile uses formulas rather than random sampling; the seed is recorded for later tool-local components.
- PostgreSQL seed: `SELECT setseed(0.1042001);`, executed inside the creation transaction.
- Listing ordinals: `1` through `100000`.
- Listing UUID: `40000000-0000-0000-0000-` followed by the ordinal encoded as twelve hexadecimal digits.
- Base timestamp: `2026-01-01T00:00:00Z`.

Creation uses set-based PostgreSQL statements only inside the opt-in query-review tool. It applies existing EF migrations, requires empty profile tables, inserts all data in one transaction, checks every invariant before commit, and rolls back on any mismatch. `profile verify` issues only SELECT statements.

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
| Coordinates | Both null when `i mod 5 = 0`; otherwise deterministic valid six-decimal latitude/longitude; 20,000 null pairs and 80,000 value pairs |
| Translations | Exactly two per listing and 200,000 total; 100,000 `mk`, 90,000 `en`, 5,000 `de`, 5,000 `sq` |
| Translation bands | 5,001-10,000 replace `en` with `de`; 10,001-15,000 replace `en` with `sq`; all retain `mk` |
| Details | Exactly one matching detail row per listing: 50,000 apartment and 50,000 house rows |
| Images | One primary image for every even listing and one secondary image for every tenth listing; 60,000 total |

The controlled comparable cohort changes the natural type/property/currency distribution. Three non-overlapping Active compensation bands restore the exact global and Active totals:

- odd IDs 3,033-3,061 change Rent to Sale (15 rows);
- Apartment-formula IDs 3,101-3,129 change to House (15 rows);
- ten base-EUR IDs in 3,202-3,229 change to USD, and ten in 3,232-3,259 change to MKD.

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
