# Chapter 10 — Search and Discovery Phase 2

## 1. Purpose

Chapter 10 strengthens the public property-discovery backend before frontend development begins.

The chapter delivers:

```text
deterministic public ordering
essential area and room filters
explicit currency-safe price behavior
deterministic multilingual translation selection
consistent structured location matching
minimal general text search
deterministic comparable listings
price-per-square-meter foundations
basic map-coordinate readiness
PostgreSQL query verification and evidence-based indexing
```

Chapter 10 prepares reliable search, location, comparable, and data foundations for later product and AI work. It does not implement AI, valuation, recommendations, semantic search, or a separate search platform.

Discovery remains the core public experience. Price per square meter and comparable listings are core intelligence foundations. AI remains part of the first MVP, but Chapter 10 adds no speculative AI infrastructure. Map exploration remains required without making the product map-first, and frontend development begins after Chapters 10 through 12.

These rules are locked. Implementation must proceed checkpoint by checkpoint without broadening the chapter merely because additional entity fields or infrastructure options exist.

## 2. Planning Status and Owner Decisions

The Chapter 10 architecture is approved.

The owner decisions are:

1. Price filters require an explicit currency.
2. Price sorting requires an explicit currency.
3. Comparable listings remain same-currency.
4. No exchange-rate conversion is introduced.
5. No authoritative North Macedonia location catalog is currently integrated.
6. Chapter 10 uses the existing schema-free multilingual location tuple.
7. Chapter 10 does not add location tables or canonical location IDs.

Checkpoint 10A is completed by this permanent rules document. Checkpoints 10B through 10G are implementation and verification work and are not completed by this document.

## 3. Non-Negotiable Boundaries

Chapter 10 must preserve:

- `ListingStatus.Active` as the first condition in every public listing query;
- filtering before `totalCount` and pagination;
- deterministic ordering before `Skip` and `Take`;
- the shared public repository path used by general and public agency listings;
- separate private owner and agency-dashboard query paths;
- authorized private access to non-Active listings;
- thin controllers, use-case-focused handlers, and data-focused repositories;
- EF Core as the production query mechanism;
- current pagination response types;
- Chapter 12 ownership of global pagination and error-contract cleanup.

The required public query flow is:

```text
ListingStatus.Active
  → public filters
  → totalCount
  → deterministic ordering
  → Skip / Take
  → load and map selected rows
```

Draft, Archived, Reserved, Sold, and Rented listings must never enter public results, public counts, public pages, or comparable candidates.

Public access to a missing or non-Active listing by identifier returns `404`.

## 4. Public API Contracts

### 4.1 GET /api/listings

Existing parameters remain supported:

```text
lang
listingType
agencyId
propertyType
heatingType
furnishingStatus
condition
hasBasement
hasElevator
apartmentType
houseType
minYardAreaSquareMeters
maxYardAreaSquareMeters
minPrice
maxPrice
city
municipality
neighborhood
page
pageSize
```

Chapter 10 adds:

| Parameter | Type/default | Rule |
|---|---|---|
| `sort` | string, `newest` | Only `newest`, `priceAsc`, and `priceDesc`, compared case-insensitively after trimming |
| `currency` | nullable string | Trim, uppercase invariant, exactly three ASCII letters, exact stored match |
| `minAreaSquareMeters` | nullable decimal | Inclusive and greater than zero |
| `maxAreaSquareMeters` | nullable decimal | Inclusive and greater than zero |
| `minRooms` | nullable decimal | Inclusive and zero or greater |
| `maxRooms` | nullable decimal | Inclusive and zero or greater |
| `q` | nullable string | Trimmed literal contains search; 2–100 characters when nonblank |

The response remains `PagedResponse<ListingResponse>`. Chapter 10 does not rename it, replace offset pagination, or introduce a new pagination abstraction.

`GetListingsHandler` changes from returning the page directly to `ServiceResult<PagedResponse<ListingResponse>>`. `ListingsController` maps `ValidationError` to `BadRequest(result.Error)` and `Success` to `Ok(result.Value)`.

### 4.2 GET /api/agencies/{id}/listings

Add `sort`, defaulting to `newest`, and optional `currency`. Currency is required when a price sort is requested.

Keep `lang`, `page`, `pageSize`, and `PagedResult<ListingResponse>`.

The handler continues to construct the shared public `GetListingsQuery` with `AgencyId` fixed from the route and calls `IListingRepository.GetFilteredReadOnlyAsync`. The route does not gain every general public-search filter in Chapter 10.

Invalid sort or currency input returns `400`. A missing agency with otherwise valid input returns `404`. Validation occurs before the agency-existence lookup so error precedence is deterministic.

### 4.3 GET /api/listings/{id}/comparables

Add:

```http
GET /api/listings/{id}/comparables?lang=mk&limit=6
```

| Input | Rule |
|---|---|
| `id` | Listing identifier |
| `lang` | Default `mk`; normalized like other listing language inputs |
| `limit` | Default `6`; allowed range `1` through `12` |

| Condition | Response |
|---|---|
| Active source and eligible candidates | `200` with `IReadOnlyList<ListingResponse>` |
| Active source and no eligible candidates | `200` with an empty array |
| Missing or non-Active source | `404` |
| Invalid limit | `400` |

The endpoint has no pagination wrapper and exposes no score or internal ranking fields.

### 4.4 POST /api/listings validation changes

No creation fields are added. Currency must normalize to exactly three ASCII letters. Latitude and longitude must both be absent or both present. Latitude is limited to `-90..90` and longitude to `-180..180`, inclusive.

No North Macedonia bounding box is enforced without a separate product rule.

## 5. Deterministic Sorting

Add an application-level `ListingSortOption` with exactly `Newest`, `PriceAsc`, and `PriceDesc`.

Controllers bind `sort` as raw text. Application validation parses only `newest`, `priceAsc`, and `priceDesc`, compared case-insensitively after trimming. Numeric enum forms such as `sort=1` are invalid and return `400`. The repository receives only the validated typed option.

| Sort | Database ordering |
|---|---|
| `newest` | `CreatedAtUtc DESC, Id DESC` |
| `priceAsc` | `Price ASC, CreatedAtUtc DESC, Id DESC` |
| `priceDesc` | `Price DESC, CreatedAtUtc DESC, Id DESC` |

Every branch ends with `Id DESC`. This guarantees a total order for an unchanged result set, not snapshot stability during concurrent writes. Cursor pagination remains Chapter 12 work.

Public agency listings inherit the same behavior. Private owner and agency-dashboard sorting is not redesigned.

## 6. Essential Filters

Chapter 10 adds only inclusive `minAreaSquareMeters`, `maxAreaSquareMeters`, `minRooms`, and `maxRooms`.

- Area bounds must be greater than zero.
- Room bounds may be zero.
- `Rooms == null` does not match when either room bound is present.
- These filters compose with every existing public filter.
- They apply before count and pagination.

Bathrooms, year built, and year renovated are not added as general search filters.

Existing listing type, property type, price, agency, heating, furnishing, condition, basement, elevator, apartment type, house type, yard-area, and structured location filters remain supported.

## 7. Currency and Price Rules

Currency input is trimmed, upper-cased invariant, required to contain exactly three ASCII letters, and matched exactly to `Listing.Currency`.

Locked rules:

- `minPrice` or `maxPrice` requires `currency`;
- `priceAsc` or `priceDesc` requires `currency`;
- `currency` by itself filters to that currency;
- `newest` without price inputs may span currencies;
- comparables use the source currency;
- raw numeric prices are not compared or sorted across currencies when price semantics are requested.

There is no exchange-rate conversion, base-currency storage, exchange-rate service, or cross-currency price sorting.

## 8. Effective Translation Rules

Requested `lang` is trimmed and lower-cased. Null, empty, or whitespace input becomes `mk`.

Choose one effective translation in this order:

1. exact requested `LanguageCode`, case-insensitive;
2. otherwise `mk`, case-insensitive;
3. otherwise lowest `LanguageCode` by PostgreSQL `C`-collation byte ordering;
4. then lowest `Id` by PostgreSQL UUID ordering;
5. if none exists, translated response fields remain null.

`ListingResponse.LanguageCode` reports the actual selected language.

Do not rely on collection order or the database default collation. The EF query uses EF Core collation support for PostgreSQL `C`. The in-memory selector uses equivalent lexicographic UTF-8 byte comparison for `LanguageCode` and canonical UUID-text ordinal comparison for `Id`.

PostgreSQL tests must prove query-time and mapping-time selection parity.

The same effective translation drives:

- public list, listing-by-id, and agency-list mapping;
- structured location filtering;
- `q` filtering;
- comparable source and candidate location logic;
- private response mapping where the shared mapper is used.

Private dashboard authorization, status eligibility, route, filters, ordering, and pagination remain unchanged. The previously unspecified shared fallback text becomes deterministic; this is the only intentional private response correction.

Client-side filtering or ordering before count and pagination is forbidden.

## 9. Location Identity and Matching

Chapter 10 uses this operational tuple:

```text
LanguageCode + City + Municipality + Neighborhood
```

It supports deterministic search and comparable behavior but is not canonical real-world geography. Chapter 10 adds no location table, canonical ID, country hierarchy database, or backfill migration.

Stored-value normalization remains:

- trim and lower-case `LanguageCode`;
- convert blank nullable location fields to null;
- trim nonblank location fields;
- preserve display casing, script, and diacritics.

Do not transliterate, remove diacritics, invent aliases, or rewrite display text.

For `city`, `municipality`, and `neighborhood`:

- trim input;
- treat blank input as absent;
- reject values longer than 100 characters;
- use case-insensitive literal exact matching;
- escape percent, underscore, and the selected escape character;
- do not interpret user input as a wildcard pattern.

The existing leading/trailing wildcard behavior is removed for structured location filters.

All supplied location predicates must match one effective `ListingTranslation` row and are combined with `AND`. Fields remain independently optional for compatibility.

The hierarchy is `city > municipality > neighborhood`. One translation's city must never combine with another translation's municipality or neighborhood. Comparable ranking awards a lower location tier only when its parent levels also match.

Location matching is scoped to the effective translation. It does not search all translations or attempt cross-language aliases.

## 10. Minimal General Text Search

Add `q` to `GET /api/listings`.

- Trim input; whitespace-only means no predicate.
- Require 2–100 characters when nonblank.
- Search the entire value as one literal phrase.
- Use case-insensitive literal contains matching.
- Escape percent, underscore, and the selected escape character.
- Search only the effective translation.

A listing matches when any one searchable field contains the full normalized phrase.

Search `Title`, `City`, `Municipality`, and `Neighborhood`.

Do not search `Description`, `AddressLine`, enum labels, or agency profile fields.

`q` composes with Active visibility, structured filters, currency, count, sorting, and pagination.

Chapter 10 adds no tokenization, stemming, typo correction, fuzzy matching, aliases, relevance weights, multi-term syntax, suggestions, or autocomplete.

## 11. Comparable Listings

### 11.1 Candidate eligibility

A candidate is eligible only when:

1. it is Active at the public base query;
2. its `Id` differs from the source `Id`;
3. it has the same `ListingType`;
4. it has the same `PropertyType`;
5. it has the same normalized `Currency`;
6. source and candidate prices are greater than zero;
7. source and candidate areas are greater than zero;
8. both have effective translations for the request;
9. both effective translations have the same actual `LanguageCode`;
10. both effective translations have nonblank `City`;
11. cities match case-insensitively as literal exact values.

The source is never returned. Personal and agency listings compete under identical public rules.

### 11.2 Deterministic ranking

Candidates are ordered lexicographically by:

1. location tier:
   - same city, same nonblank municipality, and same nonblank neighborhood;
   - then same city and same nonblank municipality;
   - then same city;
2. relative area difference ascending;
3. relative unrounded price-per-square-meter difference ascending;
4. relative price difference ascending;
5. `CreatedAtUtc DESC`;
6. `Id DESC`.

Relative differences use source values as fixed denominators:

```text
area difference = abs(candidate area - source area) / source area
PSM difference = abs(candidate PSM - source PSM) / source PSM
price difference = abs(candidate price - source price) / source price
```

Ranking remains server-side and is not exposed in the response. There are no weighted scores, hard price/area bands, personalization rules, or fallback to countrywide unrelated listings.

If the source has invalid nonpositive price or area, return an empty list instead of dividing by zero.

## 12. Price Per Square Meter

Price per square meter remains calculated as `Price / AreaSquareMeters`.

Response behavior:

- area less than or equal to zero returns `0` defensively;
- otherwise round to two decimal places;
- use explicit `MidpointRounding.ToEven` when mapping is touched;
- return currency separately as today.

Comparable ranking uses the unrounded database expression and only positive-area, same-currency rows.

Do not add a PSM column, generated/stored value, migration, PSM filter, PSM general sort, or currency conversion.

## 13. Map and Coordinate Readiness

`ListingResponse` already exposes enough information for the first frontend marker and preview implementation:

```text
Id
Latitude and Longitude
title and location
price and currency
listing and property types
primary image
```

Chapter 10 validates coordinate pairs and ranges but adds no map-specific projection or endpoint.

Viewport bounds, nearby/radius search, clustering, polygons, heatmaps, and spatial indexing are deferred.

## 14. Application and Repository Boundaries

### 14.1 Controllers

Controllers receive inputs, bind `sort` as raw text, construct request/query objects, call handlers, and map existing `ServiceResult` statuses.

They do not parse sort options, build EF expressions, choose translations, select comparables, or decide public visibility.

### 14.2 Handlers

`GetListingsHandler` normalizes inputs, runs focused validation, parses textual sort, calls the repository, maps selected rows, and returns `ServiceResult<PagedResponse<ListingResponse>>`.

`GetAgencyListingsHandler` validates before repository access, checks agency existence only for valid input, constructs the shared query with route `AgencyId`, and returns the existing `PagedResult<ListingResponse>` inside `ServiceResult`.

`GetComparableListingsHandler` normalizes language, validates limit, returns `ValidationError` for invalid input, returns `NotFound` for a hidden/missing source, and orchestrates repository query and mapping.

### 14.3 Repository

The repository:

- starts every public query with `Status == Active`;
- composes scalar, detail, translation, text, and location predicates;
- counts the fully filtered query;
- applies deterministic ordering and pagination;
- loads only selected rows and required navigations;
- performs comparable eligibility and ranking in PostgreSQL.

A focused Active-public base may be shared by general search and comparables. It must never be used by private owner or agency-dashboard methods.

Focused private helpers are allowed. Generic specifications, generic query builders, dynamic property ordering, generic repositories, and new architecture layers are not.

The repository must not return `IQueryable` across Application, perform HTTP validation or currency conversion, contain role logic, use client evaluation for public filtering/count/order, or use raw SQL production queries.

### 14.4 Mapping and pagination

`ListingMappingExtensions` owns deterministic in-memory selection and response mapping. Its fallback must match the database expression.

Keep:

```text
PagedResponse<ListingResponse> for GET /api/listings
PagedResult<ListingResponse> for public agency and private listing endpoints
page < 1 normalized to 1
pageSize < 1 normalized to 20
pageSize > 100 normalized to 100
```

Global pagination unification remains Chapter 12 work.

## 15. Validation and Error Rules

The focused search validator returns the current single error string. The handler transports it through `ServiceResult.ValidationError`, and controllers use `BadRequest(result.Error)`.

Chapter 10 adds no `ProblemDetails`, new error envelope, or global validation middleware.

Return `400` for:

- unsupported sort, including numeric enum forms;
- currency not exactly three ASCII letters after trimming;
- price bound less than or equal to zero;
- minimum price greater than maximum price;
- price filter or price sort without currency;
- area bound less than or equal to zero;
- minimum area greater than maximum area;
- room bound less than zero;
- minimum rooms greater than maximum rooms;
- yard-area bound less than zero;
- minimum yard area greater than maximum yard area;
- nonblank `q` outside 2–100 characters;
- structured location longer than 100 characters;
- comparable limit outside 1–12;
- invalid creation currency;
- only one coordinate supplied;
- coordinate outside its allowed range.

Whitespace-only `q` and location inputs are absent, not invalid. Do not silently swap reversed ranges. Malformed values already rejected by ASP.NET model binding retain current automatic `400` behavior.

## 16. Schema, Index, and Performance Rules

### 16.1 Required schema work

No schema change is required for the approved design. Do not add location tables/IDs, normalized-text or search-vector columns, PSM storage, currency-conversion fields, or coordinate type changes.

No migration is required for 10B through 10E.

### 16.2 Conditional index work

10F may add a new EF Core migration only when PostgreSQL evidence justifies a focused index.

Before an index migration:

1. generate actual EF SQL;
2. load the locked PostgreSQL 16 benchmark;
3. run `EXPLAIN (ANALYZE, BUFFERS)` outside production code;
4. record estimates, actual rows, rows removed, scan/sort behavior, buffers, execution time, write overhead, and index storage cost;
5. compare before and after on the same database state;
6. apply the acceptance gates below.

The benchmark uses:

- a fixed random seed and recorded seed command;
- 100,000 listings;
- 70 percent Active, with the remainder across all non-public statuses;
- personal listings and at least 100 agencies;
- both listing types and every property type;
- three currencies;
- two translations per listing on average;
- 20 percent null `Rooms`;
- representative price/area and repeated/selective location/title/comparable groups;
- equal timestamps and equal prices.

Run one warm-up and five measured warm executions per query shape on the same machine/database state.

An index is accepted only when it appears in the actual plan, improves the relevant median by at least 25 percent and 5 ms, reduces shared buffer access by at least 20 percent or removes a disk spill, and causes no greater than 10 percent regression in another locked shape.

Write overhead and index storage cost must be evaluated alongside read benefit. An index must not be accepted when its read benefit creates unreasonable write overhead or storage cost.

Otherwise add no index. A no-index outcome is valid.

If justified, create a new migration, leave old migrations unchanged, update EF configuration and snapshot through tooling, verify blank-database migration with Testcontainers, and record the exact before/after evidence.

### 16.3 Shapes and gates

Measure filtered count and page retrieval where applicable for:

- Active plus newest;
- Active plus currency/price ordering;
- Active plus agency/newest;
- area and room filtering;
- effective-translation location filtering;
- `q` contains matching;
- comparable eligibility and ordering.

Do not add low-selectivity single-column indexes on statuses/enums/booleans by default, and do not assume a B-tree supports contains search.

`q` fails the planning gate when its filtered-count or first-page median exceeds 250 ms on the locked profile, or its plan spills to disk. Record evidence and reopen text-search planning. Do not silently add `pg_trgm`, PostgreSQL full-text search, raw SQL, or an external engine.

These gates are reproducible planning rules, not production traffic estimates or public SLOs.

## 17. PostgreSQL Test Rules

Endpoint and query behavior uses the existing PostgreSQL 16 Testcontainers stack. EF InMemory, SQLite, repository mocks, and LINQ-to-Objects do not substitute for database-query tests.

PostgreSQL coverage is required for `ILike`/escaping, collation and effective-translation subqueries, decimal division, count/page semantics, split queries/includes, enum conversions, deterministic ordering, migrations, and indexes.

### 17.1 Isolation

The shared class fixture means new tests must isolate queries with unique agencies, unique search/location tokens, and explicit seeded IDs.

Assert exact IDs, complete order, exact `totalCount`, every row satisfying the rule, and no duplicate/omitted IDs across pages. Do not rely on ambient Active data or inspect only the first broad match.

Correct the brittle `ListingsEndpointTests.GetAll` ambient-data setup when it is directly touched by 10B.

### 17.2 Required coverage

Sorting/filter tests cover every sort, equal values, ID ties, repeated requests, adjacent pages, invalid/numeric sort, inclusive area/room bounds, null rooms, composition, exact counts, invalid ranges, and currency requirements.

Ordering coverage through `GET /api/agencies/{id}/listings` must confirm that the public agency endpoint inherits deterministic sorting through the shared public query path.

Visibility tests cover every non-public status for general search, location, `q`, public agency listings, and comparables. Hidden rows must not affect count or pages. Private tests must continue proving authorized non-Active access.

Translation/location tests cover requested and fallback rows, reverse insertion, `C`/UUID ordering, no translations, query/mapping parity, one-row hierarchy matching, casing/trimming, literal wildcard characters, no partial structured match, and no cross-translation leakage. Add a private-response regression for the shared deterministic fallback.

Text tests cover each locked field, case-insensitive contains, wildcard escaping, blank/invalid `q`, description/address exclusions, effective-language scope, filter composition, visibility, count/page, and deterministic order.

Comparable tests cover source status, source exclusion, candidate visibility, type/property/currency/city/language eligibility, positive values, every ranking tier, final ties, limits, empty results, and personal/agency parity.

PSM/coordinate tests cover two-decimal and midpoint behavior, zero area, unrounded comparable ranking, currency exclusion, divide-by-zero guards, coordinate pairing/ranges, and public coordinate mapping.

Every implementation checkpoint runs:

```bash
dotnet build
dotnet test
```

Changed public API behavior also receives a Swagger or browser smoke test. Performance verification remains a recorded manual/repeatable exercise, not a stopwatch assertion in the correctness suite.

## 18. Implementation Checkpoints

### 10A — Permanent Rules and Owner Confirmations

Scope:

- lock the permanent rules;
- record explicit-currency behavior;
- record the schema-free location decision;
- lock routes, validation, visibility, ordering, deferrals, and checkpoints.

Completion:

- this document exists;
- no owner decision remains open;
- no production implementation occurs in 10A.

Status: completed by this document.

### 10B — Deterministic Public Search and Essential Filters

Scope:

- textual sort input and typed parsing;
- deterministic ordering;
- currency behavior;
- area and room ranges;
- focused validation and `ServiceResult` propagation;
- sort/currency support for public agency listings;
- directly relevant brittle public-search test correction.

Complete when:

- only the three locked sort tokens are accepted;
- numeric sort forms return `400`;
- every sort ends with `Id DESC`;
- area/room filtering and currency rules work;
- invalid or reversed ranges return `400`;
- bathroom, year-built, and year-renovated filters remain absent;
- `GetListingsHandler` returns `ServiceResult<PagedResponse<ListingResponse>>`;
- deterministic behavior is covered by PostgreSQL-backed integration tests;
- Active and all filters precede count/page;
- agency listings still reuse the shared public query;
- private listing query methods are unchanged;
- build and tests pass.

### 10C — Translation and Location Determinism

Scope:

- deterministic effective-translation fallback;
- EF/in-memory selection parity;
- effective-translation literal exact location matching;
- removal of all-translation and cross-translation leakage.

Complete when:

- the locked fallback is deterministic;
- PostgreSQL and mapper selection agree;
- visible text is the text used for matching;
- all structured levels match one translation row;
- partial/cross-translation false matches are covered;
- private query behavior remains intact;
- the broad-translation location issue remains in `backend-quality-handoff.md` until Chapter 10 closeout, after implementation, tests, and review;
- build and tests pass.

### 10D — Minimal General Text Search

Scope:

- bounded `q` over the four locked fields;
- composition with filters, visibility, count, ordering, and pagination.

Complete when:

- only locked fields participate;
- wildcard characters are literal;
- `q` uses the effective translation;
- `q` applies before `totalCount` and pagination;
- `q` results use deterministic sorting;
- hidden statuses never match publicly;
- suggestions, full-text search, and external engines remain absent;
- build and tests pass.

### 10E — Comparable Listings and Map Readiness

Scope:

- comparable endpoint and handler;
- exact eligibility and deterministic ranking;
- same-currency unrounded PSM;
- coordinate validation;
- existing response marker-readiness verification.

Complete when:

- route, limit, statuses, eligibility, and ordering match this document;
- source is excluded and every tie is deterministic;
- candidates remain same-currency;
- source and candidate area/price positivity guards remain enforced;
- comparable ranking uses unrounded price per square meter;
- every locked location and ranking tier is covered before completion;
- no score is exposed;
- coordinates validate as a pair and by range;
- no map endpoint or GIS dependency exists;
- build and tests pass.

### 10F — PostgreSQL Query Review and Conditional Indexing

Scope:

- inspect generated SQL;
- run the fixed benchmark;
- record query plans;
- add only indexes passing the gates;
- create a migration only when justified.

Complete when:

- every locked query shape has recorded evidence;
- fixed seed, server settings, and medians are recorded;
- numeric index and `q` gates are applied;
- every added index has recorded before-and-after evidence;
- index acceptance includes evaluation of write overhead and storage cost;
- any migration is new and snapshot-consistent;
- a clean Testcontainer migrates successfully;
- no speculative index or raw SQL production query exists;
- build and tests pass.

### 10G — Full Verification and Documentation Closeout

Scope:

- full regression and smoke verification;
- Chapter 10 completion status;
- backend context and quality-handoff updates;
- stale Chapter 9L wording correction.

Complete when:

- full build and full test suite pass, with test count recorded;
- general search, agency listings, and comparables pass smoke tests;
- public Active-only and private-dashboard regressions pass;
- this document matches implemented behavior;
- backend context records Chapter 10 completion;
- the broad-translation location issue is removed only after implementation, tests, and review;
- any newly discovered evidence-backed unresolved issue is added to `backend-quality-handoff.md` with an assigned target chapter;
- stale Chapter 9L present-tense wording is corrected;
- the roadmap remains Chapters 10, 11, and 12 before frontend development.

The Chapter 9L wording is documentation debt, not unfinished Chapter 9 implementation. Corrections include the Chapter 9 purpose/final-status wording and the backend-context project snapshot, current-completion task, and next-task policy.

## 19. Likely Files by Checkpoint

### 10B

- `src/RealEstate.Api/Controllers/ListingsController.cs`
- `src/RealEstate.Api/Controllers/AgenciesController.cs`
- `src/RealEstate.Application/Listings/Queries/GetListings/`
- new focused sort option and validator files in `GetListings`
- `src/RealEstate.Application/Listings/Repositories/IListingRepository.cs` only if required
- `src/RealEstate.Infrastructure/Persistence/Repositories/ListingRepository.cs`
- `src/RealEstate.Application/Agencies/Queries/GetAgencyListings/`
- Application dependency injection
- listing get-all/filter/sort integration tests
- public agency-listing integration tests
- `ListingTestHelpers.cs` only for focused setup

### 10C

- `ListingMappingExtensions.cs`
- `ListingRepository.cs`
- focused `GetListings` normalization/validation files if required
- mapping unit tests
- location and public-agency integration tests
- creation handler/validator only if existing normalization requires correction

### 10D

- `ListingsController.cs`
- `GetListingsQuery`, validator, and handler
- `ListingRepository.cs`
- focused text-search integration tests

### 10E

- `ListingsController.cs`
- new `GetComparableListings` feature folder
- `IListingRepository.cs`
- `ListingRepository.cs`
- Application dependency injection
- `ListingMappingExtensions.cs` only for shared mapping
- `CreateListingValidator.cs` and its tests
- comparable and coordinate integration tests

### 10F, conditional

- `ListingConfiguration.cs`
- `ListingTranslationConfiguration.cs` only if evidence supports a translation index
- one new migration and designer
- `RealEstateDbContextModelSnapshot.cs`
- affected query tests and Chapter 10 evidence notes

If no index passes the gates, 10F does not modify EF configurations, migrations, or the snapshot.

### 10G

- this Chapter 10 document
- `docs/backend-context.md`
- `docs/backend-quality-handoff.md`
- `docs/chapters/chapter-09-agency-phase-2.md`

## 20. Explicitly Deferred Work

The following are not Chapter 10 implementation work:

```text
Python or separate AI services
embeddings and vector databases
semantic search
Elasticsearch, OpenSearch, or Meilisearch
recommendation engines and personalization
AI price estimation and automated valuation
exchange-rate conversion or storage
PostgreSQL full-text search
trigram or fuzzy-search extensions
suggestions and autocomplete
saved searches
PostGIS and spatial search
polygon search and heatmaps
backend marker clustering and map viewport filtering
large location or geocoding platforms
geocoding-provider integration
location tables and canonical IDs
precomputed search tables
raw SQL production queries
generic specification/query-builder frameworks
generic repositories
global listing-card/read-model redesign
global pagination cleanup
ProblemDetails or global error cleanup
observability and correlation IDs
private dashboard search redesign
Chapter 11 listing data-integrity backlog
unrelated agency, authentication, or background-processing work
```

## 21. Risks and Safeguards

| Risk | Required safeguard |
|---|---|
| Draft or Archived leakage | One Active-public base and visibility tests for every public path |
| Hidden rows affect count/page | Apply Active and all filters before count and pagination |
| Equal values cause unstable pages | End every sort with `Id DESC` |
| Numeric enum binding expands sort | Bind raw text and parse only documented tokens |
| Validation has no HTTP path | Use `ServiceResult.ValidationError` and thin controller mapping |
| Agency behavior diverges | Keep constructing shared `GetListingsQuery` with fixed `AgencyId` |
| Private dashboards become Active-only | Keep private methods separate and rerun private tests |
| Query and response use different translations | One effective-translation rule plus PostgreSQL parity tests |
| Database and mapper choose different fallback | Locked `C`/UUID ordering and equivalent in-memory comparison |
| Location levels match different translations | Match all levels on one effective translation row |
| Wildcards broaden predicates | Escape pattern metacharacters |
| Free text is mistaken for canonical geography | Treat the tuple as operational and add no canonical schema |
| Comparables cross currencies | Require source currency equality and add no FX |
| Comparable division by zero | Require positive source/candidate price and area |
| Ranking becomes recommendations | Fixed lexicographic dimensions and no exposed score |
| `q` becomes noisy or expensive | Bounded fields/length and PostgreSQL gate |
| Indexes proliferate | Recorded plans and numeric acceptance gates |
| EF performs client evaluation | PostgreSQL tests and generated-SQL review |
| Shared data creates false-positive tests | Unique discriminators and exact ID/count assertions |
| Chapter 12 work leaks in | Preserve pagination types and global errors |
| AI scope expands early | Preserve explicit infrastructure and feature deferrals |

## 22. Chapter Completion Rule

Chapter 10 is complete only when checkpoints 10A through 10G satisfy their completion criteria.

Compilation alone is insufficient. Required behavior, PostgreSQL integration coverage, Active-only regression coverage, full build/test results, smoke tests, performance evidence, and documentation closeout must all be recorded.

Implementation must follow this document. Any request to add a canonical location catalog, cross-currency behavior, advanced search infrastructure, GIS, AI services, or global API-contract cleanup requires explicit replanning rather than silent Chapter 10 expansion.
