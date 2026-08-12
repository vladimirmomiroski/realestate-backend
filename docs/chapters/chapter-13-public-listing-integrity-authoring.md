# Chapter 13 — Public Listing Integrity and Authoring

## Chapter status

This document is the authoritative architecture and implementation plan for Chapter 13. It is a planning artifact, not implementation evidence.

Checkpoints 13A–13G are **COMPLETED** and remain the verified foundation. Chapter 13 was reopened before closeout because the product requirement for a map-ready public location was finalized only after the original Active/public invariant had been implemented. The former Checkpoint 13H must not run in its old position. Checkpoints 13H–13K are **NEWLY REQUIRED BEFORE CLOSEOUT**, and the comprehensive verification/frontend handoff moves to Checkpoint 13L.

Chapter 13 is therefore not complete, and the frontend handoff remains paused, until Checkpoint 13L records the integrated results for the stronger location invariant and geocoding workflow.

Primary evidence: `docs/planning/chapter-13-ultra-preparation.md`.

The filename follows the repository's established lowercase, hyphenated `chapter-NN-description.md` convention and names both responsibilities that must move together: trustworthy public publication state and the supported authoring path needed to reach it.

## 1. Chapter 13 Purpose

Chapter 13 turns `ListingStatus.Active` from principally a status value into a trustworthy publication state. A successful public response must never rely on nullable annotations, fabricated defaults, a lucky language fallback, or unverified client-supplied coordinates. The backend must first make the required content and physical map location true, prevent supported writes from violating them, and expose a Draft-capable edit and location-resolution path.

The chapter also adds the missing production-supported listing update workflow. Authors must be able to retrieve all editable content, complete or correct a Draft, and publish it through the same personal/agency authorization architecture that already exists.

The reopened location work establishes a deliberate split between localized display/search text and one canonical physical map anchor. City, Municipality, AddressLine, and optional Neighborhood remain translated presentation fields. Latitude, Longitude, provider-neutral precision, and internal resolution provenance form one listing-level backend-confirmed geocoded snapshot. A public listing must have both truthful localized location text and that confirmed map snapshot.

The chapter must preserve:

- Active-only public visibility and private Draft management;
- requested language → Macedonian (`mk`) → deterministic other translation fallback;
- the same effective translation for display, structured location, q, and comparables;
- Chapter 10 query/filter/pagination/comparable behavior and proven PostgreSQL read topology;
- Chapter 11 listing-image transaction, lock, and compensation guarantees;
- Chapter 12 ProblemDetails, stable error codes, request IDs, pagination, OpenAPI, and observability conventions.

## 2. Locked Product Decisions

The following are not open during implementation:

1. Description is required and nonblank for Active/public listings.
2. Chapter 13 includes a real supported listing edit/update capability.
3. The current property taxonomy remains Apartment and House only.
4. Commercial/Land and expanded discovery remain later chapters.
5. The frontend cannot tighten until the backend invariant, DTO, OpenAPI, and contract tests are truthful.
6. This public-listing work owns the Chapter 13 number. The historical JWT/configuration item previously called Chapter 13 is provisionally renumbered to Chapter 16 and remains deferred; Chapter 13 performs none of that security/configuration work.
7. Every Active translation must have meaningful City, Municipality, AddressLine, and Description; Neighborhood remains optional.
8. Every Active listing must have a complete backend-confirmed geocoded snapshot with valid coordinates and provider-neutral precision.
9. Ordinary create/full-replacement requests must not accept trusted Latitude or Longitude. The frontend may preview provider candidates but confirms an opaque backend-issued selection token, never arbitrary coordinate numbers.
10. Geocoding is backend-mediated behind an Application-owned abstraction and an Infrastructure adapter selected by configuration. A concrete provider is an operational adapter decision, not a Domain or public-contract type.
11. Stored confirmed coordinates are the public pin for this chapter. Precision communicates granularity; it is not a privacy flag. Separate private-exact/public-approximate coordinates remain explicitly deferred.
12. PostGIS, a canonical national geography catalog, radius/polygon/viewport search, clustering, and AddressLine q-search are not pulled into Chapter 13.

### 2.1 Why Chapter 13 was reopened

The original 13A–13G sequence correctly made LanguageCode, Title, City, and Description truthful for Active rows and strict public responses. It did not settle the product decision that every public listing also needs Municipality, AddressLine, and a trustworthy map pin. Current source still accepts caller-supplied decimal coordinates and records no provider, candidate identity, precision, or confirmation time. Non-null coordinates alone would therefore overstate trust: any in-range numbers can currently be persisted.

This is an extension, not a rewrite. The existing authoring, locking, readiness, PostgreSQL aggregate-integrity, strict-mapper, corruption-boundary, and OpenAPI-separation architecture remains authoritative. New work adds the missing location state and strengthens the same invariants through new forward migrations and bounded checkpoints.

### 2.2 Mandatory current field-model gate

The following matrix describes the repository **before** the newly required 13H–13K work. “Create/PUT” refers to the supported request contract; “Active” means the current 13G publication rule, not the stronger target defined in Section 3. Public and management columns describe the current response nullability. “Search/map” records current behavior, not desired future discovery.

#### Listing aggregate root

| Field / owner | Current CLR / PostgreSQL | Current create / PUT | Current Draft / Active requirement | Current public / management response | Search, publication, and map behavior |
|---|---|---|---|---|---|
| `Id` / Listing | `Guid`; `uuid NOT NULL` PK | generated by handler; not PUT-editable | always present / no readiness role | `Guid` / `Guid` | route identity; no map role |
| `ListingType` / Listing | `ListingType`; `varchar(50) NOT NULL` | defined Sale/Rent required semantically / C# `required`, defined value | required / not a publication-readiness field | non-null enum / non-null enum | public filter and comparable equality |
| `PropertyType` / Listing | `PropertyType`; `varchar(50) NOT NULL` | defined Apartment/House required semantically / C# `required`, defined value | required / not a publication-readiness field | non-null enum / non-null enum | public filter, subtype filter, comparable equality |
| `Price` / Listing | `decimal`; `numeric(18,2) NOT NULL` | greater than zero / C# `required`, greater than zero | positive through supported authoring / not currently rechecked at publish | `decimal` / `decimal` | min/max/sort and comparable ranking/eligibility |
| `Currency` / Listing | `string`; `varchar(3) NOT NULL` | optional transport default `EUR`, then exactly three ASCII letters / C# `required`, same validation | present / not readiness | non-null string / non-null string | public filter and comparable equality |
| `AreaSquareMeters` / Listing | `decimal`; `numeric(10,2) NOT NULL` | greater than zero / C# `required`, greater than zero | positive through supported authoring / not currently rechecked at publish | `decimal` / `decimal` | filters and comparable eligibility/ranking |
| `Rooms` / Listing | `decimal?`; `numeric(4,1) NULL` | optional / optional, omission clears | optional / optional | nullable / nullable | min/max filter; no publication or map role |
| `Bathrooms` / Listing | `decimal?`; `numeric(4,1) NULL` | optional / optional, omission clears | optional / optional | nullable / nullable | display only |
| `YearBuilt` / Listing | `int?`; `integer NULL` | optional / optional, omission clears | optional / optional | nullable / nullable | display only |
| `YearRenovated` / Listing | `int?`; `integer NULL` | optional; if present 1800–2100 and not before YearBuilt / same | optional / optional | nullable / nullable | display only |
| `BalconyCount` / Listing | `int?`; `integer NULL` | optional, nonnegative / same | optional / optional | nullable / nullable | display only |
| `ParkingSpaces` / Listing | `int?`; `integer NULL` | optional, nonnegative / same | optional / optional | nullable / nullable | display only |
| `HasBasement` / Listing | `bool?`; `boolean NULL` | optional / optional, omission clears | optional / optional | nullable / nullable | public filter; no publication/map role |
| `IsExchangePossible` / Listing | `bool?`; `boolean NULL` | optional / optional, omission clears | optional / optional | nullable / nullable | display only |
| `HeatingType` / Listing | non-null enum; `varchar(50) NOT NULL`, default `Unknown` | optional/default `Unknown`; create has no separate optional-enum defined-value check / optional/default `Unknown`, defined-value validation | always stored / no readiness role | non-null enum / non-null enum | display only |
| `FurnishingStatus` / Listing | non-null enum; `varchar(50) NOT NULL`, default `Unknown` | same create behavior / defined on PUT | always stored / no readiness role | non-null enum / non-null enum | display only |
| `Condition` / Listing | non-null enum; `varchar(50) NOT NULL`, default `Unknown` | same create behavior / defined on PUT | always stored / no readiness role | non-null enum / non-null enum | display only |
| `Orientation` / Listing | non-null enum; `varchar(50) NOT NULL`, default `Unknown` | same create behavior / defined on PUT | always stored / no readiness role | non-null enum / non-null enum | display only |
| `Latitude` / Listing | `decimal?`; `numeric(9,6) NULL` | caller may submit; paired and `-90..90` / caller may replace or clear; same rules | optional / optional under current 13G truth | nullable / nullable | no current search/filter/comparable use; current map coordinate has no provenance |
| `Longitude` / Listing | `decimal?`; `numeric(9,6) NULL` | caller may submit; paired and `-180..180` / caller may replace or clear; same rules | optional / optional under current 13G truth | nullable / nullable | no current search/filter/comparable use; current map coordinate has no provenance |
| `AgencyId` / Listing | `Guid?`; `uuid NULL`, `ON DELETE SET NULL` | optional, authorized agency association / immutable in full replacement | optional personal/agency ownership / no readiness role | nullable / nullable | public/private agency filters and authorization; no map role |
| `CreatedByUserId` / Listing | `Guid?`; `uuid NULL`, restricted FK | assigned internally from principal / not editable | nullable at DB for compatibility / ownership only | omitted / nullable | private authorization; no public search/map role |
| `Status` / Listing | `ListingStatus`; `varchar(50) NOT NULL`, Draft default | not writable; create produces Draft / lifecycle-only | Draft allowed / Active currently requires four translated identity fields | non-null / non-null | Active-only public eligibility and lifecycle |
| `CreatedAtUtc` / Listing | `DateTime`; `timestamptz NOT NULL` | set by DbContext auditing / not editable | audit truth / no readiness role | omitted / non-null | newest sort and comparable tie-break |
| `ModifiedAtUtc` / Listing | `DateTime?`; `timestamptz NULL` | set by DbContext auditing / not editable | optional audit / no readiness role | omitted / nullable | no discovery or map role |

`PricePerSquareMeter` is a calculated response member, not a stored field. Images are a separate child aggregate surface governed by Chapter 11 and are not changed by the location replan.

#### Translation fields

| Field / owner | Current CLR / PostgreSQL | Current create / PUT | Current Draft / Active requirement | Current public / management response | Search, publication, and map behavior |
|---|---|---|---|---|---|
| `Id` / ListingTranslation | `Guid`; `uuid NOT NULL` PK | generated; not accepted / retained by normalized language, not accepted | present / no field-level readiness role | omitted / `Guid` | deterministic fallback tie-break |
| `ListingId` / ListingTranslation | `Guid`; `uuid NOT NULL` FK cascade | assigned internally / immutable association | child ownership / aggregate trigger scope | omitted / omitted | query correlation only |
| `LanguageCode` / ListingTranslation | non-null `string`; `varchar(10) NOT NULL`, canonical row check and unique with ListingId | required, normalized and grammar-checked / C# `required`, same | required / canonical on every translation | required non-null on `PublicListingResponse`; nullable effective field on `ListingResponse` / required non-null per authoring translation | effective selector and publication |
| `Title` / ListingTranslation | non-null `string`; `varchar(200) NOT NULL`, trimmed-nonblank row check | required and normalized / C# `required`, same | required / meaningful on every translation | required non-null public; nullable flattened private / required non-null authoring | q-search and publication |
| `Description` / ListingTranslation | `string?`; `varchar(3000) NULL`, optional trimmed-nonblank row check | optional, blank becomes null / same | optional / meaningful on every current Active translation | required non-null public; nullable flattened private / nullable authoring | excluded from q; publication |
| `AddressLine` / ListingTranslation | `string?`; `varchar(300) NULL`, no current row check | optional, blank becomes null / same | optional / currently optional | nullable / nullable | excluded from q and structured filters; human geocoding input but not currently trusted |
| `City` / ListingTranslation | `string?`; `varchar(100) NULL`, optional trimmed-nonblank row check | optional, blank becomes null / same | optional / meaningful on every current Active translation | required non-null public; nullable flattened private / nullable authoring | q, structured filter, comparable eligibility, geocoding input, publication |
| `Municipality` / ListingTranslation | `string?`; `varchar(100) NULL`, no current row check | optional, blank becomes null / same | optional / currently optional | nullable / nullable | q, structured filter, comparable location tier, geocoding input |
| `Neighborhood` / ListingTranslation | `string?`; `varchar(100) NULL`, no current row check | optional, blank becomes null / same | optional / optional | nullable / nullable | q, structured filter, comparable location tier, optional geocoding input |

#### Apartment details

The one-to-one row is required by supported authoring when `PropertyType == Apartment`; the House row must be absent. These fields do not currently participate in publication readiness or map resolution.

| Field / owner | Current CLR / PostgreSQL | Current create / PUT | Draft / Active | Public / management | Search/publication/map |
|---|---|---|---|---|---|
| `ListingId` / ListingApartmentDetails | `Guid`; `uuid NOT NULL` PK/FK cascade | assigned internally / retained or created during type replacement | matching detail row required by supported authoring / no readiness rule | nested identity omitted / omitted | aggregate key only |
| `ApartmentType` | non-null enum; `varchar(50) NOT NULL`, default `Unknown` | payload required; create does not separately reject undefined numeric values / C# `required`, defined value | stored / no readiness role | non-null enum / non-null enum | apartment-type public filter |
| `Floor` | `int?`; `integer NULL` | optional, nonnegative / same | optional / optional | nullable / nullable | display only |
| `TotalFloors` | `int?`; `integer NULL` | optional, nonnegative and not below Floor / same | optional / optional | nullable / nullable | display only |
| `HasElevator` | `bool?`; `boolean NULL` | optional / optional | optional / optional | nullable / nullable | public filter |

#### House details

The one-to-one row is required by supported authoring when `PropertyType == House`; the Apartment row must be absent. These fields do not currently participate in publication readiness or map resolution.

| Field / owner | Current CLR / PostgreSQL | Current create / PUT | Draft / Active | Public / management | Search/publication/map |
|---|---|---|---|---|---|
| `ListingId` / ListingHouseDetails | `Guid`; `uuid NOT NULL` PK/FK cascade | assigned internally / retained or created during type replacement | matching detail row required by supported authoring / no readiness rule | nested identity omitted / omitted | aggregate key only |
| `HouseType` | non-null enum; `varchar(50) NOT NULL`, default `Unknown` | payload required; create does not separately reject undefined numeric values / C# `required`, defined value | stored / no readiness role | non-null enum / non-null enum | house-type public filter |
| `NumberOfFloors` | `int?`; `integer NULL` | optional, nonnegative / same | optional / optional | nullable / nullable | display only |
| `YardAreaSquareMeters` | `decimal?`; `numeric(10,2) NULL` | optional, nonnegative / same | optional / optional | nullable / nullable | yard-area public filter |

### 2.3 Field-model findings and gate decision

The current model has the right place for localized presentation but no authoritative physical-location boundary:

- City, Municipality, AddressLine, and Neighborhood can legitimately differ by language while describing one property.
- Latitude and Longitude live correctly on the parent, but ordinary callers currently set them directly and the database enforces neither pairing nor ranges.
- There is no geocoder abstraction, provider adapter, API-key configuration, provider result identity, precision, confirmation timestamp, cache, rate limit, or retry policy.
- A geocoder cannot prove that `Скопје` and `Skopje`, or `Центар` and `Centar`, are equivalent physical identities. Re-geocoding each translation would permit one listing to acquire contradictory physical locations.
- A full national location catalog/canonical geography-ID model would be disproportionate now and would force discovery/schema work that the product has not requested.

The approved model is therefore a **hybrid confirmed snapshot**:

1. Keep all four existing location strings on `ListingTranslation` as localized display/search text.
2. Require City, Municipality, and AddressLine on every Active translation because any translation may win the effective selector. Neighborhood remains optional.
3. Reuse listing-level Latitude and Longitude as the one canonical physical/public pin.
4. Add one provider-neutral, atomic confirmation snapshot on Listing: nullable `LocationPrecision`, internal nullable `GeocodingProviderKey`, internal nullable opaque `GeocodingResultReference`, optional internal nullable `GeocodedDisplayName`, and nullable `LocationConfirmedAtUtc` alongside Latitude/Longitude. These are the target field names. `GeocodingProviderKey` is a stable adapter identifier such as a configured provider code, never an API credential. Checkpoint 13H locks conservative provider-neutral maximum lengths in EF/PostgreSQL. Before 13I persists a live-provider result, provider approval must permit retention and fit those bounds; otherwise implementation returns to this field-model gate. Bare coordinates are never an equivalent substitute.
5. Treat `LocationPrecision` as a provider-neutral enum with conservative outcomes: `ExactAddress`, `Street`, `Neighborhood`, `Municipality`, `City`, and `Approximate`. Null means unresolved; no provider-specific enum enters Domain or public contracts.
6. Permit a Draft to be unresolved. A confirmed snapshot is all-or-nothing; partial coordinates/provenance are invalid. Transitional legacy paired coordinates may remain explicitly classified as unverified during the first migration, but they can never satisfy final Active readiness.
7. Clear all stored location state—including a transitional legacy-unverified coordinate pair—whenever any normalized City, Municipality, AddressLine, or Neighborhood value in the complete Draft translation set changes. The rule is intentionally conservative because the backend cannot prove cross-language semantic equivalence.

This is the smallest architecture that separates physical truth from localized labels without a new geography platform or a read-side join.

### 2.4 Automatic geocoding and trust boundary

Chapter 13 adopts backend-mediated interactive resolution:

```text
author saves Draft location text
-> authenticated listing-scoped candidate search
-> Application geocoder port
-> configured Infrastructure provider adapter
-> provider-neutral candidates with preview coordinates and precision
-> frontend map/candidate preview
-> user confirms opaque short-lived backend token
-> backend re-resolves/verifies provider candidate outside a DB transaction
-> acquire existing parent Listing FOR UPDATE scope
-> reauthorize current Draft and verify unchanged normalized input fingerprint
-> atomically persist provider-derived snapshot
```

The frontend never submits trusted Latitude or Longitude. It may display coordinates returned in a candidate response, but confirmation submits only a tamper-protected token bound at minimum to listing ID, acting user, provider candidate/reference, source translation/input fingerprint, and expiry. Confirmation re-resolves or otherwise validates the candidate through the configured adapter; it does not deserialize token coordinates and trust them as provider truth. If future UX supports clicking or dragging a pin, that point is input to a backend reverse-geocoding candidate flow and is not persisted directly.

Provider calls do not occur inside Publish or while holding the database transaction/parent lock. Timeouts, cancellation, bounded transient retry, safe server-side key configuration, narrow abuse/rate limits, provider-compliant caching/retention, sanitized dependency failures, and address/key/token-safe logging belong to the geocoding checkpoint. Provider outage leaves the listing Draft and publication-incomplete. Deterministic fake adapters own tests; automated tests do not call the live provider.

PostgreSQL can enforce snapshot shape, ranges, completeness, and immutability; it cannot prove that a historical external call actually occurred. The supported backend confirmation operation is the origin trust boundary. Privileged direct database writers remain an operational trust boundary and must not manufacture provider provenance; Chapter 13 does not claim cryptographic provider attestation inside PostgreSQL.

Frontend-direct geocoding is rejected because it exposes or constrains provider credentials, couples the UI to a vendor, and leaves the backend trusting arbitrary numbers. One-shot backend geocoding without user confirmation is rejected because ambiguous or broad addresses can silently bind the wrong place. Provider selection remains an adapter/configuration decision, but a provider, terms, retention policy, quota, and credential deployment must be approved before Checkpoint 13I can close.

### 2.5 Precision and public-location privacy

Precision is persisted Domain state and part of the public response because a city or municipality centroid must not be represented as an exact building. The adapter maps provider confidence/granularity conservatively; only an explicitly address-level result may become `ExactAddress`. A broad result is acceptable when the submitted address is broad, provided its precision is truthful and the user confirms it.

For this chapter, the confirmed stored coordinates are also the public coordinates, and required AddressLine is public. This is an explicit product boundary, not an assumption that every future listing should reveal an exact private point. Separate private-exact and public-approximate coordinates, jittering, or seller-controlled pin privacy are deferred. Encapsulated Domain location mutation plus centralized strict public mapping leaves a future public-exposure policy possible without changing provider provenance.

## 3. Final Domain/Public Invariant

### 3.1 Supported listing structural invariant

Every listing created or replaced through supported authoring APIs has at least one translation. Every stored translation supplied through those APIs has:

- a canonical `LanguageCode` that satisfies the project language-tag grammar;
- a trimmed, nonblank `Title`;
- normalized optional text, where blank/whitespace optional input becomes `null`;
- no duplicate normalized language within one listing.

Existing core authoring rules remain in force: defined current `ListingType` and `PropertyType` enum values, positive price and area, a three-letter currency, nonnegative/count/year rules, and exactly the current matching Apartment or House detail shape. Chapter 13 shares those rules between create and update; omitted/default/undefined enum values are validation failures. Ordinary create/update coordinate pairing and range validation is retired in 13H because those requests cease accepting coordinates. Equivalent and stronger pair/range checks move to the Domain geocoded-location operation and PostgreSQL. The chapter does not claim a new database-wide invariant for every unrelated historical numeric/property-detail rule.

### 3.2 Active publication invariant

A listing is publishable and may remain Active only when:

1. it has at least one translation; and
2. **every attached translation** has:
   - canonical, syntactically valid `LanguageCode`;
   - trimmed, nonblank `Title`;
   - trimmed, nonblank `City`;
   - trimmed, nonblank `Municipality`;
   - trimmed, nonblank `AddressLine`;
   - trimmed, nonblank `Description`.
3. its listing-level confirmed location snapshot has:
   - non-null Latitude and Longitude as a pair;
   - Latitude in `-90..90` and Longitude in `-180..180`;
   - a non-null provider-neutral `LocationPrecision`;
   - nonblank internal `GeocodingProviderKey` and opaque `GeocodingResultReference`;
   - a non-null confirmation timestamp;
   - no partial or internally contradictory location metadata.

“Meaningful” in Chapter 13 means non-null and non-whitespace after normalization. No arbitrary word count or prose-quality heuristic is introduced.

Requiring every translation is deliberate. If only one fallback translation were valid, an incomplete exact-requested-language row would still win the established selector and invalidate the public promise. Every-row readiness makes all selectable translations safe while leaving fallback order and public query predicates unchanged. The single parent snapshot is deliberate for the opposite reason: localized labels may vary, but the property must have one physical/public map pin.

An Active listing does not have to contain `mk`. If requested and Macedonian translations are absent, the existing deterministic other-language fallback remains valid because every remaining row is publishable.

### 3.3 Status ownership

`Listing.Status` will no longer be publicly settable by ordinary domain/application code. Draft is the construction default; `Publish`, `Unpublish`, and `Archive` own supported transitions. EF Core may still materialize the private setter. Tests must use lifecycle methods or explicitly named database-integrity setup rather than treating direct assignment as normal domain behavior.

## 4. Draft and Authoring Model

Draft is intentionally useful but not publication-complete. The following is the **supported-authoring** contract enforced by POST/PUT, not a claim that PostgreSQL independently requires every Draft aggregate to retain a child:

- a supported create/replacement must retain at least one canonical translation with valid language and nonblank title;
- City, Municipality, AddressLine, Description, and Neighborhood may be `null` in any Draft translation;
- blank optional input is stored as `null`, not as a blank string;
- a Draft may have no geocoded snapshot; if one exists it must be coherent and backend-confirmed;
- a Draft may change current core fields, ListingType, PropertyType, translations, and the matching Apartment/House detail;
- AgencyId, creator, status, images, IDs, audit fields, Latitude, Longitude, precision, and geocoding provenance are not editable through the replacement operation.

Content editing is **Draft-only**. An Active listing is a published snapshot. The supported workflow is:

```text
Active -> unpublish -> Draft -> edit -> publish -> Active
```

Archived, Reserved, Sold, and Rented listings are not editable in Chapter 13. An attempted update returns a resource-state conflict. This boundary keeps publication enforcement simple, makes the concurrency rule understandable, and avoids introducing partial Active mutations. Image operations retain their existing focused endpoints and rules.

## 5. Edit/Update Architecture

### 5.1 Endpoints

Chapter 13 adds:

```http
GET /api/listings/{id}/management
PUT /api/listings/{id}
```

The authenticated management GET is required because the current response exposes only one effective translation. A full-replacement client must be able to retrieve every translation or it could unknowingly delete unseen rows.

`POST /api/listings` remains the creation endpoint and still creates Draft. Its `Location` header changes from the public detail URL—which returns 404 for the new Draft—to `/api/listings/{id}/management`.

### 5.2 Full-replacement PUT

`PUT /api/listings/{id}` is replacement semantics, not a disguised patch:

- required core members omitted or supplied with invalid defaults fail validation;
- a nullable member explicitly supplied as `null` is cleared;
- an omitted nullable member has the same replacement meaning as explicit `null`: it is cleared; nullable members are optional-and-nullable in the request schema, so no JSON-presence tracking is introduced;
- `translations` is the complete authoritative translation set;
- a normalized language present in both old and new sets is updated in place;
- an old language omitted from the request is removed;
- a new normalized language is added;
- `null` never means “leave unchanged.”

If sparse mutation is needed later, it must use an explicitly designed PATCH or focused operation; Chapter 13 does not overload PUT with ambiguous semantics.

The request contains the current editable core fields, the complete translation collection, and exactly one matching Apartment or House detail payload. From 13H onward it excludes status, AgencyId, CreatedByUserId, image mutations, translation IDs, detail IDs, auditing fields, Latitude, Longitude, LocationPrecision, and every geocoding provenance member. Create has the same no-coordinate trust boundary.

Required JSON presence is explicit rather than inferred from CLR defaults. `listingType`, `propertyType`, `price`, `currency`, `areaSquareMeters`, and `translations` are required top-level members; every translation requires `languageCode` and `title`. `UpdateListingRequest` declares these as C# `required` members recognized by the configured System.Text.Json input formatter, so an omitted `currency` cannot silently become `EUR`; focused model-binding tests must prove this configured behavior. The matching `apartmentDetails` or `houseDetails` object is conditionally required by the validated PropertyType. Other nullable members are optional and omission clears them. Optional value enums such as heating/furnishing/condition/orientation and subtype kind reset to their documented `Unknown` default when omitted; every supplied enum numeric value must still be defined.

Translations are reconciled by normalized `LanguageCode`, not deleted/reinserted wholesale. Retained languages preserve their translation IDs; new languages receive new IDs; omitted languages are removed. This avoids unnecessary writes and WAL against the existing trigram index and preserves deterministic identities.

The replacement engine compares the normalized old and new City, Municipality, AddressLine, and Neighborhood values across the complete translation set. Any addition, removal, or semantic-value change clears coordinates and every confirmation/provenance member—including transitional legacy-unverified coordinates—in the same locked Draft transaction. Unrelated edits preserve coherent existing state. This conservative invalidation prevents a stale pin after localized physical-location text changes.

While Draft, an Apartment may become a House and vice versa. The handler must atomically remove the obsolete one-to-one detail and create/update the single detail matching the new `PropertyType`. Both detail rows may never remain attached after a successful supported update.

The full mutation is one transaction. A failure in any scalar, translation, or subtype change rolls back the entire replacement.

### 5.3 Authorization

The existing management distinction is preserved:

- unresolved principal/user: 401;
- Disabled account: 403;
- PendingVerification is treated as current management/create behavior treats it: not Disabled, therefore allowed to manage Draft content;
- personal listing: creator only;
- agency listing: Active membership with Owner or Agent role;
- Manager, Pending/Disabled member, nonmember: 403;
- the agency itself need not be Active for Draft repair;
- publication still separately requires an Active user and Active agency.

Resource-specific status/readiness information is evaluated only after listing ownership/agency authorization. An unauthorized caller cannot learn whether private content is complete.

For update, ASP.NET authentication/model binding still handles transport failures before the handler. Once handler execution begins, the order is principal/account eligibility → locked listing existence → personal/agency authorization → editable-status check → semantic replacement validation → mutation/save. Thus a caller who does not control the listing cannot infer whether it is non-Draft or whether submitted content would be valid.

## 6. Translation Validity Model

### 6.1 Stored language grammar

Chapter 13 defines a project language-tag grammar; it does not claim full BCP 47 conformance and does not introduce a supported-language allow-list.

Canonical storage rules:

```text
trim
-> lowercase invariant
-> total length 2..10
-> ASCII primary subtag: 2 or 3 letters
-> optional "-" separated subtags: 2..8 lowercase ASCII letters/digits
```

Equivalent validation pattern, combined with the existing max length:

```regex
^[a-z]{2,3}(?:-[a-z0-9]{2,8})*$
```

This admits current `mk`, `en`, `sq`, and `de`, plus common forms such as `en-us` and `sr-latn`, without locking the product to a language catalog. Underscores, whitespace, empty subtags, arbitrary text, and noncanonical stored casing are rejected.

Input such as `EN` is accepted by create/update, normalized to `en`, and then validated. Two inputs that normalize to the same code are duplicates. Direct persistence of `EN` is rejected by the database row constraint.

Public `lang` query behavior does not become strict. It remains trimmed/lowercased for selection; an unknown or malformed preference simply has no exact stored match and follows the existing `mk`/deterministic fallback. This avoids changing public discovery semantics.

### 6.2 Text normalization and lengths

- Chapter 13 defines one explicit boundary-whitespace set shared by .NET normalization and PostgreSQL checks: Unicode White_Space code points `U+0009..U+000D`, `U+0020`, `U+0085`, `U+00A0`, `U+1680`, `U+2000..U+200A`, `U+2028`, `U+2029`, `U+202F`, `U+205F`, and `U+3000`. A narrow shared helper trims only these code points from both ends; internal characters are preserved. Infrastructure expresses the identical character set explicitly with PostgreSQL `btrim(value, characters)`. Neither layer relies on an unspecified default `Trim()`/`btrim()` equivalence.
- Title is trimmed and must be nonblank.
- City, Municipality, AddressLine, Description, and Neighborhood are trimmed; blank Draft input becomes `null`; when present they must be nonblank.
- City, Municipality, AddressLine, and Description become required only for Active; Neighborhood remains optional in every lifecycle state.
- Create and update validate all existing EF maximum lengths before persistence: language 10, title 200, description 3000, address 300, and city/municipality/neighborhood 100.

Parity tests must exercise at least space, tab, CR/LF, nonbreaking space (`U+00A0`), em space (`U+2003`), and ideographic space (`U+3000`) through both supported validation and direct PostgreSQL writes. Language normalization uses this same boundary trim before ASCII lowercase/grammar validation.

Validation and normalization are shared as narrow reusable rules, while `CreateListingValidator` and `UpdateListingValidator` remain explicit use-case validators. No validation framework is added.

## 7. Publish Readiness Architecture

Publication readiness is layered:

1. Application create/update validation prevents malformed supported authoring input.
2. Domain translation/publication rules provide one typed readiness evaluation.
3. `Listing.Publish()` evaluates readiness before changing status.
4. The publish handler loads the complete locked aggregate and maps typed readiness failure to the public error contract.
5. The dedicated geocoding workflow is the only supported operation that can establish a confirmed location snapshot.
6. PostgreSQL checks row/location shape, validates activation, and freezes Active translation/location mutation.
7. Strict public mapping refuses impossible malformed Active materialized state.

### 7.1 Domain behavior

The Domain owns a typed publication-readiness result/violation model; handlers must not parse `InvalidOperationException.Message`. Checkpoint 13J extends the existing codes with explicit invalid Municipality, invalid AddressLine, and invalid/missing confirmed-location violations. Location violations identify the listing-level invariant without leaking provider response data. The exact code set is deterministic so publish conflicts and integrity logs remain testable.

Transition order is:

- Archived/Reserved/Sold/Rented publish attempts fail as `conflict.resource_state` without content evaluation.
- Draft publish evaluates readiness, then changes to Active.
- already-Active publish still evaluates readiness; valid Active remains idempotent 200, while malformed Active returns `conflict.listing_not_ready`.

Unpublish and archive do not require readiness. This allows an authorized operator to remove a malformed Active record from publication if physical corruption ever bypasses the normal guarantees.

### 7.2 Handler order

The publish handler preserves this information-disclosure order:

```text
principal/user resolution
-> account publication eligibility
-> locked listing existence
-> personal/agency authorization
-> current status validity
-> publication readiness
-> save and commit
-> response mapping
```

Readiness details are therefore visible only to an authorized manager of that listing.

## 8. Persistence/Database Strategy

Four forward Chapter 13 migrations separate row truth, existing aggregate truth, location-state compatibility, and final strengthened Active truth:

1. completed 13A row truth;
2. completed 13F four-field Active aggregate truth;
3. new 13H nullable location-state and row-shape foundation;
4. new 13J stronger Active translation/location enforcement.

Already-applied 13A/13F migration history is never edited.

### 8.1 Row-level truth

The translation columns keep their current nullability and lengths. Named PostgreSQL checks use the explicit Section 6.2 character string in `btrim(value, characters)`—not default `btrim(value)`—and enforce:

- LanguageCode is trimmed, lowercase, nonblank, and matches the project grammar;
- Title is trimmed and nonblank;
- City is either `NULL` or trimmed/nonblank;
- Description is either `NULL` or trimmed/nonblank.

Title and LanguageCode remain NOT NULL. City and Description remain nullable because Draft may be incomplete. The unique `(ListingId, LanguageCode)` index remains unchanged and becomes semantically canonical because nonlowercase storage is rejected. The four-column trigram GIN index remains unchanged.

The 13H forward migration extends optional row truth to AddressLine, Municipality, and Neighborhood using the same explicit whitespace vocabulary. It also adds nullable location metadata and focused Listing checks for coordinate pairing/ranges and snapshot coherence. The deployable compatibility states are: unresolved (coordinates and provenance absent), explicitly legacy-unverified (a valid paired coordinate exists but confirmation metadata is absent), or confirmed (valid paired coordinates plus the complete required provenance/precision/time set). Partial metadata is rejected. Legacy-unverified is a migration bridge only and cannot satisfy final publication readiness.

### 8.2 Cross-row Active truth

A simple EF/PostgreSQL `CHECK` cannot assert child existence. Chapter 13 does not pretend otherwise.

Because Chapter 13 permits edits only while Draft, PostgreSQL uses targeted immediate, **statement-level** triggers with transition tables rather than per-row or broad deferred-trigger machinery:

1. The completed 13F `AFTER INSERT` and `AFTER UPDATE` statement triggers on `Listings` inspect transition rows set-wise and reject every inserted/transitioned Active listing unless it has at least one translation and every translation satisfies LanguageCode, Title, City, and Description publication requirements.
2. Separate `AFTER INSERT`, `AFTER UPDATE`, and `AFTER DELETE` statement triggers on `ListingTranslations` use the event's old/new transition tables, derive the distinct affected parent IDs, lock existing parent rows in canonical UUID order, and reject translation mutation while any parent is Active. For every accepted Draft mutation, the trigger also performs one set-based no-value-change parent `UPDATE` (for example, `SET Status = Status`) so PostgreSQL creates a new parent MVCC tuple version without falsifying audit timestamps. PostgreSQL's event-specific transition-table restrictions are handled explicitly rather than hidden in one pseudo-trigger.
3. Translation mutation is allowed after Status has first changed away from Active in the same transaction.
4. Parent deletion/cascade is explicitly allowed when the parent row no longer exists; the trigger must not make an otherwise valid aggregate delete impossible.
5. A `ListingId` move checks and locks the union of old and new parent IDs in the same canonical order.

The parent MVCC touch closes the higher-isolation write-skew case: a `REPEATABLE READ` transaction that took an old child snapshot cannot activate the parent after a concurrent Draft child mutation; its root update must observe the newer parent tuple and abort rather than validate stale translations. The set-based shape is required: it must not execute one parent lock, parent touch, or aggregate subquery per translation/profile row. It keeps the 200,000-translation query-review seed and the final 70,000 Active transitions practical. Supported application writers acquire the parent first. Arbitrary direct child SQL can already hold child tuples before its statement trigger locks the parent; in a collision PostgreSQL may abort/deadlock-victimize one out-of-band writer, but no committed result may violate integrity. The database rule remains simple because Active translations are frozen and all supported edits first unpublish.

The completed 13F migration avoids a check-then-enable race. The 13J forward migration must preserve that architecture: in one migration transaction it takes write-conflicting locks on `Listings` and `ListingTranslations` in that order, replaces/extends the named assertion functions and triggers, and runs set-based fail-fast validation before commit. It adds every-translation Municipality/AddressLine checks, complete confirmed root location checks, and Active root-location immutability until unpublish. It must preserve the parent MVCC touch and all transition-table/concurrency guarantees.

No migration fabricates coordinates, precision, provider identity, or confirmation. Before 13J, existing Active rows must be audited and either resolved through the supported Draft workflow (`Active -> unpublish -> resolve -> publish`) or deliberately kept non-Active. Incompatible Active rows make the migration fail atomically. This is a forward compatibility/remediation gate, not production repair machinery. The focused location constraints are a new owner-approved invariant and a narrow exception to the quality handoff's earlier decision not to duplicate request validation broadly in PostgreSQL.

### 8.3 Query-review profile compatibility

The deterministic Chapter 10F seeder currently inserts final Active statuses before translations. The activation trigger would correctly reject that order. The aggregate-integrity checkpoint must change only seed ordering:

```text
insert all profile listings as Draft
-> insert the same translations/details/images
-> apply the same deterministic final status distribution with one set-based SQL command
-> run the same 61 profile invariants
```

The final 100,000-listing/200,000-translation distribution and result semantics remain unchanged. Deterministic test/profile seeders may attach explicitly named trusted test resolution metadata set-wise; they do not call a live geocoder and do not normalize fake metadata into production paths.

## 9. Public vs Management DTO Strategy

Chapter 13 separates contracts by truth level rather than globally tightening the shared DTO.

| Contract | Surfaces | Translation shape | Required public identity |
|---|---|---|---|
| `PublicListingResponse` | public list, public detail, public agency list, comparables, successful publish | one effective translation plus canonical public pin | LanguageCode, Title, City, Municipality, AddressLine, Description, Latitude, Longitude, and LocationPrecision are non-null/non-optional |
| existing `ListingResponse` | create, `/my`, agency dashboard, unpublish, archive | one effective translation | flattened translated fields remain nullable because management includes nonpublic states |
| `ListingAuthoringResponse` | management detail and update response | deterministic collection of every translation plus read-only location state | per row LanguageCode/Title required; City/Municipality/AddressLine/Description/Neighborhood nullable for Draft; coordinates/precision nullable and not writable through PUT |

`ListingAuthoringResponse` contains the editable core fields, current Apartment/House details, status, immutable identity/ownership metadata needed by the client, read-only images, audit timestamps, and every translation with its ID. Translations are ordered by the existing bytewise language comparer and canonical ID tie-break so round trips are deterministic.

Neighborhood, media URLs, unrelated optional numeric fields, and subtype-specific optional values retain their established nullability. `GeocodingProviderKey` and `GeocodingResultReference` are internal and never public. Management may expose nullable read-only precision, provider display label, and confirmation timestamp needed for the location UI without exposing credentials or provider internals.

Public JSON property names and values remain the same for existing members; Municipality, AddressLine, Latitude, and Longitude tighten from nullable to required, and provider-neutral LocationPrecision is added. LanguageCode, Title, City, and Description remain required. No generic success envelope is added.

## 10. Lifecycle Response Strategy

The existing defect is resolved by fixing loading, not by null-forgiving operators or fake defaults.

The authoring write scope locks the listing parent and loads translations, images, ApartmentDetails, and HouseDetails before a lifecycle mutation. The loaded aggregate remains tracked through save/commit and can be mapped without a post-commit read failure window.

Final response contracts are:

- publish 200: `PublicListingResponse`, because the resulting state is Active and has just passed readiness;
- unpublish 200: existing nullable `ListingResponse`, because the result is Draft;
- archive 200: existing nullable `ListingResponse`, because Archived is not a public-readiness contract.

Existing response JSON fields are preserved. Publish continues to use the strict public DTO; unpublish/archive remain nullable lifecycle responses. The status endpoints do not become narrow ad hoc responses in this chapter.

## 11. Invalid-State Defensive Policy

Prevention is primary:

- normal status mutation goes through the Domain;
- update, location confirmation/clearing, and status operations serialize on the parent row;
- activation is database-validated;
- Active translation and confirmed-location mutation is database-rejected.

No `Translations.Any(...)`, usable-row filter, or corruption-hiding predicate is added to hot public SELECTs. Pagination totals and Chapter 10 query topology must not change merely to hide impossible data.

If constraints are disabled or physical corruption nevertheless produces malformed Active state:

- strict public mapping throws a typed integrity exception;
- the API returns sanitized canonical `server.unexpected` ProblemDetails and logs high-signal structured context;
- it never supplies `""`, a fallback fake value, arbitrary coordinates/precision, or null under a required public field;
- it never turns corruption into a public 404;
- it never silently removes a materialized corrupt item from a page.

The comparable-source branch that treats missing effective language/city as integrity failure remains unchanged. Source Title, Description, AddressLine, coordinates, and precision are not used or returned by that source projection, so Chapter 13 does **not** add them merely to defensively scan every comparable request; PostgreSQL aggregate enforcement guards those source-only fields. Returned comparable candidates are fully materialized and therefore pass the strict public mapper, which validates the expanded public location contract.

Chapter 13 does not add a full-table integrity scan to every request. Corruption not selected/materialized by a request is not proactively scanned. This is acceptable because a conforming database prevents it; operational database corruption is not redefined as ordinary public eligibility.

## 12. Authorization and Error Ordering

Chapter 12's closed error catalog remains authoritative.

| Condition | HTTP/code | Rule |
|---|---|---|
| malformed create/update request | 400 `validation.failed` | keyed application/model validation; no raw DB text |
| unresolved/invalid principal | 401 existing authentication code | unchanged |
| Disabled or unauthorized actor | 403 existing authorization code | unchanged |
| inaccessible/missing listing | 404 `resource.not_found` or existing authorization behavior | unchanged per current endpoint |
| update against non-Draft | 409 `conflict.resource_state` | content is not evaluated before authorization |
| illegal lifecycle transition | 409 `conflict.resource_state` | unchanged |
| authorized publish of incomplete Draft/Active | 409 new `conflict.listing_not_ready` | fixed, sanitized catalog descriptor; no field/content detail |
| invalid/expired/stale geocoding selection | existing validation/conflict conventions, finalized in 13I | no provider response/token detail |
| configured geocoding dependency unavailable | sanitized narrow dependency failure finalized in 13I, normally 503 | no provider/key/address/internal detail |
| unexpected named row/trigger violation after validated supported write | 500 `server.unexpected` | indicates validation/implementation drift; sanitized/logged |

`conflict.listing_not_ready` is added to `ErrorCodes`, the closed catalog/descriptors, OpenAPI examples/operation responses as applicable, and contract tests. Its descriptor is fixed (for example, “The listing is not ready for publication.”), so the existing Chapter 12 failure mapper remains authoritative. The typed Domain violation may identify fields for internal tests/logging, but Chapter 13 neither exposes those details nor broadens `ServiceResult` into a multi-error framework.

Database constraint names/messages are never returned to clients. **Every** row-check or trigger violation reached after supported application validation—including the activation trigger—is treated as sanitized `server.unexpected` and logged as validation/implementation drift. No PostgreSQL message is reclassified by string or constraint-name parsing into a user-correctable 409. The 409 readiness response comes only from the authorized application/domain readiness evaluation before persistence.

## 13. Concurrency Decision

Current tracking queries are insufficient after update exists. The dangerous race is:

```text
publish reads complete Draft
-> concurrent update commits incomplete Draft content
-> publish commits Active based on stale content
```

Chapter 13 adds a focused `IListingAuthoringWriteScope`/authoring repository abstraction:

1. begin a Read Committed transaction;
2. acquire a parameterized `SELECT ... FOR UPDATE` lock on the Listing parent;
3. load the tracked authoring aggregate and required navigations after the lock;
4. authorize and re-evaluate current status/content;
5. save;
6. commit explicitly; dispose without commit rolls back.

Update, location confirmation/clearing, publish, unpublish, and archive all use this scope. Provider lookup happens before the transaction; after lookup, confirmation enters the scope, reauthorizes the latest Draft, validates the token/input fingerprint against current state, and persists atomically. Existing listing-image scopes retain their own focused abstraction but lock the same parent first, so **supported application writers** share one parent-first lock order and Chapter 11 guarantees are not weakened. This is not a claim that arbitrary direct SQL acquires locks in that order; the statement-trigger behavior and possible deadlock victimization for out-of-band child SQL are defined in Section 8.2.

Results:

- update commits first → publish sees the final Draft content;
- publish commits first → update wakes, sees Active, and returns 409;
- unpublish commits first → update wakes and may edit Draft;
- update/update requests serialize;
- update changes location text first: stale candidate confirmation fails after the lock;
- location confirmation commits first: a later location-text replacement clears that now-stale snapshot;
- publish commits first: location confirmation wakes, sees Active, and returns a resource-state conflict.

The database protocol extends this beyond the application's Read Committed scopes: every accepted direct Draft translation mutation creates a parent MVCC version as specified in Section 8.2. A stale `REPEATABLE READ` activation must therefore fail with PostgreSQL's concurrency error rather than validate an old translation snapshot. Focused integration coverage must reproduce that exact interleaving.

Chapter 13 intentionally does not add ETags, xmin exposure, a row-version framework, or global locking. Two authorized full Draft PUTs are serialized last-writer-wins. The lock protects publication/state integrity, not stale-editor merge semantics. A later product decision may add optimistic conflict UX.

A translation-only update must still mark the Listing aggregate root as Modified through Infrastructure so `RealEstateDbContext` auditing sets `ModifiedAtUtc`. Handlers must not set timestamps manually.

## 14. Search/Discovery Preservation Strategy

The following remain exactly unchanged:

- requested language, then `mk`, then deterministic bytewise language/UUID fallback;
- case-insensitive requested-language matching;
- q fields: Title, City, Municipality, Neighborhood only;
- Description and AddressLine remain excluded from q despite Description becoming required;
- literal wildcard handling;
- one effective row for display/q/location;
- filters before count/page;
- sort and page tie-breaks;
- public agency-list reuse of the shared public repository;
- comparable eligibility/ranking/order for valid data.

Municipality becoming publication-required does not make it a new candidate eligibility predicate. AddressLine does not join q. Coordinates and precision do not become public filters, viewport/radius inputs, or comparable ranking keys. Public map pins are response data only in Chapter 13.

Canonical stored language removes the old ability to persist case-only duplicate codes such as `en` and `EN`. The comparable test that deliberately relies on that invalid stored pair must be replaced with canonical-storage/constraint coverage. Case-insensitive request behavior remains tested using `lang=EN` against stored `en`.

No public repository eligibility predicate or selector filter is part of the approved architecture.

## 15. Performance Preservation Strategy

The Chapter 10F baseline remains the acceptance reference: N1 56.925 ms first page, P1 41.397, P2 37.122, A1 2.184/3.119, R1 22.137, L1 11.578, Q1 10.584, and C1 63.048/63.325 under the documented five-run median method. Q1 retains its 250 ms/no-spill gate and trigram-index expectations.

| Chapter 13 change | Public SQL impact | Required performance evidence |
|---|---|---|
| input rules and row checks | none; write-side only | existing trigram index/catalog regression |
| authoring lock/lifecycle fixes | none; mutation-only | no Chapter 10F recapture |
| management GET | new private query, not a locked shape | focused query-count/load test only |
| Draft PUT | mutation-only | atomicity and bounded-query tests; no read benchmark |
| activation/translation triggers and parent MVCC touch | write-side only | migration/isolation/trigger tests and set-based query-review profile seed compatibility |
| public DTO/strict mapper | after materialization | generated public SQL equality/review; no recapture if unchanged |
| listing-level precision/provenance scalar columns | EF entity root projection changes; no intended predicate/join/order change | stop-and-review capture in 13H; recapture every actually changed locked command across N1/P1/P2/A1/R1/L1/Q1/C1 and establish the post-location baseline |
| geocoding candidate/confirmation workflow | new authenticated write/dependency paths only | bounded-query/concurrency/resilience evidence; no public-read benchmark |
| stronger publication trigger/readiness | write-side only | migration/trigger/profile compatibility; public SQL exact against 13H baseline |
| expanded strict public mapper/OpenAPI | after materialization | exact generated-SQL comparison against 13H post-location baseline |

The original 13G `33/33` exact SQL result remains the pre-location baseline. A mapped scalar added to `Listing` is selected by EF when materializing root entities, so root projections will materially differ even though discovery semantics do not. Checkpoint 13H must capture production-generated SQL immediately after the final location field model lands, identify the exact changed command roles, review plans/row width/buffers/spills/results, and recapture every affected locked command across all eight logical shapes. Unaffected count, agency-existence, comparable-source, and child-split commands must remain byte-exact. That accepted output becomes the post-location baseline; 13I–13K may not introduce further public SQL differences.

The implementation must still stop and reassess if it deviates beyond the approved scalar projection:

- unconditional Active/translation eligibility join or predicate: N1, P1, P2, A1, R1, L1, Q1, and C1 are affected;
- effective-selector SQL change: L1, Q1, and C1 are affected;
- root/split projection or include topology change on public paths: all eight shapes are affected;
- comparable source/candidate SQL change: C1 is affected.

Recapture uses the existing Chapter 10F capture/replay/export discipline and documented totals, ordered IDs, buffers, spills, and five-run medians rather than a remembered latency. It is not permission to bless new predicates or joins. A full unrelated large-profile/benchmark redesign is not required when the only approved difference is scalar projection and all result/plan gates remain healthy; the existing 61 deterministic profile invariants still run. Checkpoint 13K must prove exact equality to the new 13H baseline before closeout.

## 16. Test Strategy

### 16.1 Fixture vocabulary

Chapter 13 introduces a clear local distinction:

- **valid Draft builder**: at least one canonical language/title; City/Municipality/AddressLine/Description/Neighborhood and confirmed location may be null;
- **publishable Draft builder**: every translation includes canonical language/title/city/municipality/address/description and the root has a complete confirmed location snapshot;
- **valid Active builder**: reaches Active through publish or database setup that first creates complete content;
- **malformed/corrupt entity builder**: unit/domain/mapper use only;
- **database-rejection setup**: explicitly attempts prohibited direct state and asserts constraint/trigger rejection.

Do not disable triggers to manufacture committed corrupt integration rows. Raw SQL status helpers must either operate on publishable content or be renamed/restricted as explicit constraint tests.

### 16.2 Required focused coverage

- language grammar, normalization, duplicate normalization, all lengths, and blank/null text boundaries;
- create Draft behavior with optional location text and no client-writable coordinates;
- management detail authorization and all-translation representation;
- PUT required-member presence, replacement/omission clearing, translation reconciliation/ID preservation, location-snapshot invalidation, type-detail conversion, rollback, and audit propagation;
- location candidate/confirmation authorization, opaque-token binding/expiry/tampering/staleness, provider re-resolution, no-transaction provider call, parent-lock races, timeout/retry/cancellation, rate limiting, sanitized failure/logging, and deterministic fake-adapter behavior;
- personal and agency management/publish permission matrices;
- update/status and status/status concurrency under the shared lock, plus the stale-`REPEATABLE READ` direct-write activation case closed by the parent MVCC touch;
- Domain readiness for zero translations, every required translation field, coordinate pair/ranges, precision, provenance, and confirmation-time cases;
- valid and malformed already-Active publish behavior;
- activation and Active translation/location database triggers, including concurrency, immutability, migration fail-fast, and cascade/no-parent behavior;
- lifecycle responses with loaded translations/images/details;
- strict public mapping and every public surface's expanded required identity/map fields;
- OpenAPI requiredness and public/private schema separation;
- query-review seeder's unchanged final profile invariants and exact post-location SQL-baseline comparison.

### 16.3 Existing tests that change premise

Tests that expect Active no-translation/null-core behavior become prevention or strict-mapper tests, including the dossier's mapping, public detail, no-q list, and comparable-source cases. The null-City q test keeps its optional municipality/neighborhood intent but uses a valid Active City/Description. The case-only `en`/`EN` stored tie test becomes canonical-language constraint/request-case coverage.

### 16.4 Regression unchanged

Rerun without reorganizing:

- publishing ownership/account/agency/role/status behavior;
- unpublish/archive/public visibility;
- `/my` and agency dashboard status/pagination behavior;
- requested/mk/deterministic fallback and PostgreSQL parity;
- q/location/wildcard/no-cross-row semantics;
- sorting, filtering, pagination, comparables ranking, and agency public listing behavior;
- trigram index catalog test;
- Chapter 11 listing-image authorization/concurrency/persistence/compensation;
- Chapter 12 failures, request ID, logging, OpenAPI, health, CORS, and media behavior.

Auth, invitations, unrelated agency profile/member/logo, health, CORS, and global test organization are not refactored merely because the full suite reruns.

## 17. OpenAPI and Frontend Contract Strategy

### 17.1 API contract changes

| Surface | Old contract | Final Chapter 13 contract |
|---|---|---|
| POST `/api/listings` Location | public Draft detail URL that returns 404 | `/api/listings/{id}/management`; body remains Draft `ListingResponse`; request no longer accepts Latitude/Longitude |
| GET `/api/listings/{id}/management` | absent | authenticated `ListingAuthoringResponse` with all translations |
| PUT `/api/listings/{id}` | absent | full Draft replacement without writable coordinates/provenance; 200 `ListingAuthoringResponse` |
| listing location candidate search | absent | authenticated Draft-only provider-neutral candidate collection with map-preview coordinates, precision, label, and opaque confirmation token |
| listing location confirmation/clear | absent | authenticated Draft-only focused operation; token-in, backend-confirmed read-only location state out; no numeric coordinate input |
| public list | `PagedResponse<ListingResponse>` with optional translated identity | `PagedResponse<PublicListingResponse>` with required LanguageCode/Title/City/Municipality/AddressLine/Description/Latitude/Longitude/LocationPrecision |
| public detail | nullable translated `ListingResponse` | strict `PublicListingResponse` |
| public agency list | nullable `PagedResponse<ListingResponse>` | strict `PagedResponse<PublicListingResponse>` |
| comparables | nullable `IReadOnlyList<ListingResponse>` | strict `IReadOnlyList<PublicListingResponse>` |
| publish success | nullable `ListingResponse` | strict `PublicListingResponse` |
| unpublish/archive success | nullable `ListingResponse`, sometimes unloaded | same schema with correctly loaded persisted data |
| not-ready publish | no readiness failure | 409 `conflict.listing_not_ready` |

Swagger must mark public LanguageCode, Title, City, Municipality, AddressLine, Description, Latitude, Longitude, and LocationPrecision as required and non-null. Management/Draft schemas must not inherit those Active-level promises. Create/PUT schemas must not expose writable Latitude, Longitude, precision, or provenance. The PUT schema's required arrays otherwise retain `listingType`, `propertyType`, `price`, `currency`, `areaSquareMeters`, `translations`, and nested `languageCode`/`title`; nullable Draft text documents omission-as-clear. Candidate/confirmation schemas expose provider-neutral data and opaque tokens only, never provider keys, raw responses, API keys, or a provider-specific enum.

OpenAPI structural tests must prove endpoint-to-schema references, required arrays/nullability, all seven pagination members, operation security, 400/401/403/404/409 responses, error-code catalog, and unchanged media/enums.

### 17.2 Frontend handoff order

Chapter 13 closes only after the backend side of this chain is proven:

```text
real invariant
-> application/domain enforcement
-> backend-mediated geocoding and user confirmation
-> persistence/runtime truth
-> strict mapping/DTO
-> Swagger/OpenAPI
-> backend contract tests
-> frontend regenerates openapi.d.ts
-> frontend implements City/Municipality/AddressLine/(optional Neighborhood) entry
-> frontend uses backend candidates, map preview, precision label, and token confirmation
-> frontend public listing/map models require the expanded strict location contract
-> frontend runtime adapter and adapter tests tighten
-> frontend Chapter 2C resumes
```

The frontend repository is not modified by Chapter 13 implementation or by this plan.

## 18. Explicit Deferrals

Out of scope:

- Commercial, Land, Shop, Office, BuildingPlot, AgriculturalLand, or any new PropertyType;
- new subtype storage, taxonomy-specific filters, or taxonomy-specific comparables;
- fuzzy/full-text search, suggestions, or Description q-search;
- AddressLine q-search or any change to the current Title/City/Municipality/Neighborhood q set;
- PostGIS, radius/polygon/viewport filtering, marker clustering, spatial ranking, or map search;
- a national/country geography catalog, canonical City/Municipality IDs, or provider-specific IDs in public/Domain contracts;
- seller privacy masking, coordinate jitter, or separate private-exact/public-approximate pins;
- distributed geocoding cache infrastructure unless provider terms and measured load require it;
- Active partial editing/PATCH;
- agency transfer or creator reassignment;
- image API redesign;
- optimistic version/ETag client conflict handling;
- global test-suite cleanup/reorganization;
- generic repository, UnitOfWork, MediatR, AutoMapper, or FluentValidation package;
- fabricated coordinate/provenance backfill or generic production repair machinery;
- frontend UI/catalog implementation;
- historical JWT/config/security “Chapter 13” work, now provisionally Chapter 16.

Chapter 14 remains property model/taxonomy expansion. Chapter 15 remains integration through discovery/API/performance/hardening. Chapter 16's exact security scope must be replanned when reached.

## 19. Ordered Checkpoint Plan

The checkpoint history is intentionally preserved. 13A–13G describe what was implemented and audited under the then-current four-field location decision. They are frozen foundations, not work to repeat. The newly required 13H–13K extend that architecture; 13L is the moved final closeout.

### Checkpoint 13A — Translation Authoring Rules and Row-Level Truth — COMPLETED

1. **Goal**

   Establish one canonical translation input vocabulary across Domain/Application/Infrastructure and make PostgreSQL enforce its row-level portion.

2. **Why it exists**

   Create currently validates only nonblank language/title and can let malformed language or overlength content reach PostgreSQL. Update and publication must not duplicate drifting rules.

3. **Exact scope**

   - Add narrow shared translation constants/rules for normalization, grammar, lengths, and blank handling.
   - Keep explicit `CreateListingValidator`; refactor it to use the shared rules.
   - Require defined current ListingType/PropertyType values; reject omitted/default and undefined numeric enum values.
   - Validate every existing translation max length before persistence.
   - Preserve at-least-one translation and normalized-duplicate rejection.
   - Add named row constraints for canonical LanguageCode, nonblank trimmed Title, and null-or-trimmed-nonblank City/Description.
   - Express the exact shared Unicode boundary-whitespace character set in both application normalization and PostgreSQL checks.
   - Keep nullable Draft City/Description, unique language index, and trigram index.
   - In this checkpoint, adapt existing persisted-row test premises that the new checks immediately invalidate: noncanonical language, untrimmed/blank Title, and blank/whitespace City or Description. Unrelated Draft intent may use `null`; publishable/Active intent uses canonical nonblank values. Zero-translation or null-City/Description Active premises wait for aggregate enforcement in 13E–13F.

4. **Expected source areas/files or architectural surfaces**

   - `RealEstate.Domain` listing translation rules/constants;
   - `Application/Listings/Commands/CreateListing/*`;
   - `Infrastructure/Persistence/Configurations/ListingTranslationConfiguration.cs`;
   - a new EF migration and model snapshot;
   - create validator/unit/integration, PostgreSQL constraint, and OpenAPI tests.

5. **Contract/invariant established**

   Old: POST accepted any nonblank language and lacked proactive translated-field length coverage. New: POST accepts canonicalizable project-language tags, returns 400 keyed validation for invalid grammar/length, and still accepts Draft City/Description as null.

6. **Tests required**

   - grammar accept/reject table, uppercase normalization, duplicate-after-normalization;
   - omitted/default/undefined ListingType and PropertyType validation;
   - title/language empty/whitespace and every max-length boundary;
   - blank optional text → null, including application/PostgreSQL parity for space, tab, CR/LF, `U+00A0`, `U+2003`, and `U+3000`;
   - direct SQL row-constraint failures and valid Draft persistence;
   - replace the stored `en`/`EN` case-tie premise with canonical-storage coverage.

7. **Regression tests that must remain unchanged**

   Valid personal/agency create, Draft status, fallback selection for distinct valid languages, q/location semantics, pagination, auth, and existing index definition. Fixture values may be made canonical without changing those assertions.

8. **Migration implications**

   Add one migration, provisionally `EnforceListingTranslationRowIntegrity`; no backfill. Invalid development rows require recreation/correction before migration. Snapshot and pending-model verification are mandatory.

9. **OpenAPI implications**

   Create translation request schema documents max lengths and language pattern; City/Description remain optional/nullable. No response schema changes.

10. **Performance/query implications**

    Write validation/checks only. Public SQL is untouched. Verify `IX_ListingTranslations_Q_Trigram` remains valid/ready with the same columns/operators.

11. **Explicit exclusions**

    No publish readiness, update endpoint, Active trigger, allow-list, public DTO, or taxonomy work.

12. **Completion/acceptance criteria**

    Application and PostgreSQL agree on canonical row truth; valid incomplete Drafts persist; malformed rows cannot; migration applies from a fresh schema; focused tests and unchanged index test pass.

13. **Dependencies**

    None.

### Checkpoint 13B — Serialized Authoring Scope and Lifecycle Loading — COMPLETED

1. **Goal**

   Establish the transaction/lock boundary required by update and publication, and stop lifecycle endpoints from mapping unloaded navigations.

2. **Why it exists**

   `GetByIdForUpdateAsync` is only a tracked query, not a lock, and loads no translations. Adding update without serialization would permit stale publish/update races.

3. **Exact scope**

   - Add focused `IListingAuthoringRepository` and `IListingAuthoringWriteScope` abstractions rather than widening unrelated image-test fakes.
   - Begin Read Committed transaction, parameterized parent `FOR UPDATE`, then load translations, images, ApartmentDetails, and HouseDetails.
   - Refactor publish, unpublish, and archive handlers to use the scope and explicit commit/rollback.
   - Preserve all existing permission/status behavior in this checkpoint.
   - Map lifecycle responses from the actually loaded tracked aggregate.
   - Maintain the same parent-first lock order as image write scopes.

4. **Expected source areas/files or architectural surfaces**

   - Application listing authoring repository interfaces/write scope;
   - Infrastructure listing repository implementation/DI;
   - publish/unpublish/archive handlers;
   - lifecycle and concurrency integration tests; image concurrency regressions.

5. **Contract/invariant established**

   Response schema is unchanged. Runtime response data changes from potentially null/empty due to missing includes to the actual persisted effective translation/images/details.

6. **Tests required**

   - lifecycle responses contain persisted translation/image/detail data;
   - missing listing rolls back/disposes cleanly;
   - status/status serialization and deterministic outcome;
   - authorization failures commit no mutation;
   - scope disposal without commit rolls back.

7. **Regression tests that must remain unchanged**

   All personal/agency publish permissions, Active agency publication prerequisite, idempotent valid publish, unpublish/archive transitions, image write-scope concurrency/compensation.

8. **Migration implications**

   None.

9. **OpenAPI implications**

   No schema change; add/extend behavior tests, not schema invention.

10. **Performance/query implications**

    Mutation paths only. Status operations deliberately load the full response aggregate; no Chapter 10F SELECT changes or recapture.

11. **Explicit exclusions**

    No update endpoint, no readiness rule, no generic UnitOfWork, no ETag, no public query changes.

12. **Completion/acceptance criteria**

    Every status writer uses a real parent lock/transaction, all response-required navigations are loaded, failure paths roll back, and Chapter 11 image guarantees still pass.

13. **Dependencies**

    13A.

### Checkpoint 13C — Complete Management Read Contract — COMPLETED

1. **Goal**

   Give authorized personal/agency authors a complete, deterministic representation from which a safe full replacement can be built.

2. **Why it exists**

   Current `ListingResponse` exposes only one effective translation and public detail rejects Draft. PUT without an all-translations read would be operationally unsafe.

3. **Exact scope**

   - Add authenticated `GET /api/listings/{id}/management`.
   - Add `ListingAuthoringResponse` and per-translation DTO with all translations and current core/detail/image/audit data.
   - Order translations deterministically.
   - Apply personal creator and agency Active Owner/Agent membership access; agency status need not be Active.
   - Permit authorized management reads for every status; PUT determines editability.
   - Point successful create `Location` to the management endpoint while preserving the create body.

4. **Expected source areas/files or architectural surfaces**

   - new Application query/handler/DTO/mapping;
   - listing authoring read repository method;
   - `ListingsController` route and create Location;
   - integration authorization/response tests and OpenAPI document tests.

5. **Contract/invariant established**

   Old: no supported full authoring detail and POST points to a public URL that 404s for Draft. New: authorized 200 all-translation management detail; POST Location is followable by its authorized creator/agency manager.

6. **Tests required**

   Personal owner/nonowner, Disabled and PendingVerification users, agency Owner/Agent/Manager/inactive member, PendingVerification account with valid agency membership, inactive agency, all status reads, multiple translations/order, nullable Draft City/Description, and followable create Location.

7. **Regression tests that must remain unchanged**

   Public detail remains Active-only/404 for Draft; `/my` and dashboard paging/order/status behavior remain unchanged.

8. **Migration implications**

   None.

9. **OpenAPI implications**

   Add exact route, security, `ListingAuthoringResponse`, translation collection, and 401/403/404 schemas. No public strictness yet.

10. **Performance/query implications**

    New private by-ID read only. Assert bounded split-query count and deterministic load; no locked Chapter 10F shape.

11. **Explicit exclusions**

    No mutation, public DTO tightening, agency transfer, or translation-specific endpoint.

12. **Completion/acceptance criteria**

    An authorized client can retrieve every value required for lossless replacement; unauthorized clients learn no private content; create Location no longer targets an unavailable Draft resource.

13. **Dependencies**

    13A–13B.

### Checkpoint 13D — Atomic Full Draft Replacement — COMPLETED

1. **Goal**

   Deliver the locked production-supported listing update capability.

2. **Why it exists**

   Description/City readiness would otherwise strand incomplete Drafts, and the project currently has no supported edit path.

3. **Exact scope**

   - Add `PUT /api/listings/{id}` and explicit `UpdateListingRequest`/validator/handler.
   - Implement unambiguous full-replacement semantics for current core fields, translations, and Apartment/House details.
   - Mark `listingType`, `propertyType`, `price`, `currency`, `areaSquareMeters`, `translations`, and each translation's `languageCode`/`title` as System.Text.Json-recognized required members; prove omission does not fall through to CLR defaults.
   - Treat omitted nullable members exactly like explicit `null` (clear them); reject default/undefined required discriminators and every supplied undefined optional enum value; conditionally require the matching subtype detail object.
   - Exclude status/ownership/agency/images/IDs/audit fields.
   - Reconcile translations by normalized language, preserving retained IDs.
   - Allow Apartment↔House conversion only while Draft and remove obsolete detail atomically.
   - Use the 13B scope; authorize before exposing resource status.
   - After transport/model binding, enforce handler order: principal/account → locked existence → ownership/agency authorization → Draft status → semantic replacement validation → mutation.
   - Permit Draft only; return 409 for every other status.
   - Ensure child-only changes mark the root Modified so DbContext auditing sets `ModifiedAtUtc`.
   - Return 200 `ListingAuthoringResponse` from the loaded canonical aggregate.

4. **Expected source areas/files or architectural surfaces**

   - new `Application/Listings/Commands/UpdateListing/*`;
   - shared validation/normalization from 13A;
   - authoring mapping/reconciliation and repository write scope;
   - `ListingsController`/DI;
   - focused unit/integration/concurrency/OpenAPI tests.

5. **Contract/invariant established**

   Old: PUT absent. New: complete Draft replacement with 200 authoring response; 400 validation, 401, 403, 404, and 409 canonical failures. Omitted translations are deleted; both omitted and explicitly null nullable scalar/text members are cleared; omission never means “leave unchanged.”

6. **Tests required**

   - full personal/agency authorization matrix, explicitly including PendingVerification account access to Draft management;
   - authorization/status/semantic-validation precedence without private-state leakage;
   - omission of every required top-level/nested member, all shared validation/normalization boundaries, and default/undefined enum numerics;
   - scalar replacement plus explicit-null and omitted-nullable clearing;
   - add/update/remove/reorder-independent translations and ID preservation;
   - same-type detail update and Apartment↔House conversion;
   - immutable agency/creator/status/images;
   - transaction rollback on persistence failure;
   - Draft-only conflicts;
   - GET → PUT → GET round trip;
   - update/update and update/status serialization/deterministic outcome (publication-content safety is completed in 13E);
   - translation-only audit propagation.

7. **Regression tests that must remain unchanged**

   Create behavior, public visibility, image endpoints, agency management roles, subtype search filters, pagination, and fallback behavior.

8. **Migration implications**

   None beyond consuming 13A row truth.

9. **OpenAPI implications**

   Add full request schema, replacement-semantics description, 200 authoring response, and exact failures/security. The exact required members from Section 5.2 appear in required arrays; nullable replacement members are optional-and-nullable and omission means clear; conditional subtype-detail rules and optional-enum `Unknown` reset semantics are documented. Status/AgencyId/images must not appear as writable request members.

10. **Performance/query implications**

    Write/private-read path only. Reconciliation avoids wholesale translation rewrite/trigram-index churn. No Chapter 10F recapture.

11. **Explicit exclusions**

    PATCH, Active edit, images, agency transfer, ETag/versioning, bulk/import, Commercial/Land, generic mutation framework.

12. **Completion/acceptance criteria**

   Any supported incomplete Draft can be corrected atomically; round trip is lossless; concurrent writers serialize with deterministic status outcomes; auditing remains centralized in DbContext. The lock seam is ready, but the guarantee that publish cannot activate incomplete serialized content is established only by 13E.

13. **Dependencies**

    13A–13C.

### Checkpoint 13E — Domain and Application Publish Readiness — COMPLETED

1. **Goal**

   Make every supported publication decision enforce the complete Active content invariant after the Draft repair path exists.

2. **Why it exists**

   Publish currently checks authorization/status but not translation readiness; normal create can therefore activate content without City or Description.

3. **Exact scope**

   - Add a typed Domain publication-readiness evaluation and make `Listing.Status` non-publicly settable.
   - Require at least one translation and every translation's canonical LanguageCode plus nonblank Title/City/Description.
   - Evaluate readiness inside `Listing.Publish()` for both Draft and already-Active calls, after invalid lifecycle status handling.
   - Use the 13B locked, fully loaded aggregate in personal and agency publish paths.
   - Preserve account/agency/role checks and authorization-before-readiness ordering.
   - Add fixed, sanitized `conflict.listing_not_ready` to the closed Chapter 12 catalog and map only the application/domain result to 409.
   - Keep valid Active publish idempotent; make malformed Active re-publish fail readiness; allow unpublish/archive without readiness.
   - Keep City/Description nullable and authorable while Draft.
   - Remove `CreateListingHandler`'s explicit Draft assignment and rely on the aggregate's Draft default.
   - Adapt **all** compile-time Status assignments in production/tests in this checkpoint: ordinary Active builders create publishable content and call `Publish()`; Draft uses the default; reachable Archived state uses lifecycle behavior. A single narrowly named test-only state materializer may use the private backing state/reflection for Reserved/Sold/Rented cases that have no current Domain transition—no production transition or public setter is added merely for fixtures. Malformed Active unit entities publish validly first and then deliberately corrupt a child through an explicit corrupt-entity helper. Direct-database invalid-state premises are converted to rejection coverage in 13F.

4. **Expected source areas/files or architectural surfaces**

   - Domain `Listing`, translation/publication rules, typed readiness result/violations;
   - `CreateListingHandler` Draft construction and narrowly scoped listing-state test helpers/builders;
   - personal/agency publish handlers using the authoring scope;
   - error codes/catalog/descriptors and controller/OpenAPI operation metadata;
   - focused Domain, handler, API, authorization, and concurrency tests.

5. **Contract/invariant established**

   Old: authorized Draft can become Active regardless of translated content. New: successful supported publish proves at least one translation and all selectable translations have LanguageCode/Title/City/Description. Valid Active re-publish remains 200; malformed Active re-publish is fixed 409 `conflict.listing_not_ready`; nonpublishable statuses remain `conflict.resource_state`.

6. **Tests required**

   - Domain matrix: zero rows; each required field null/empty/each canonical-whitespace class; valid+invalid mixed rows; all-valid;
   - Create still produces Draft without assigning Status; all lifecycle status tests build after setter privatization, including explicit Reserved/Sold/Rented test-only materialization;
   - valid and malformed already-Active idempotency behavior;
   - personal/agency publish, Active user/agency prerequisites, and authorization-before-readiness;
   - fixed/sanitized readiness ProblemDetails with request/trace identifiers and no field/DB disclosure;
   - publish/update serialization showing an incomplete committed Draft is rejected, and publish-first makes update observe Active/409;
   - unpublish malformed Active remains possible, then repair and republish succeeds.

7. **Regression tests that must remain unchanged**

   Existing personal/agency ownership, account/member/role rules, valid publish, unpublish/archive, visibility, fallback, q/location/pagination/comparables, and image concurrency.

8. **Migration implications**

   None. This checkpoint establishes supported runtime truth; PostgreSQL aggregate truth follows in 13F.

9. **OpenAPI implications**

   Publish documents the new fixed 409 code/descriptor. The public success response is not tightened until 13G.

10. **Performance/query implications**

    Mutation/load path only; public SELECTs are unchanged and no Chapter 10F recapture is required.

11. **Explicit exclusions**

    No database aggregate trigger yet, no Active edit, public eligibility filter, public DTO change, backfill, or taxonomy-specific readiness.

12. **Completion/acceptance criteria**

    No supported Domain/Application publication path can activate incomplete content; malformed state is reported only after authorization with the fixed error contract; Draft remains repairable; no public query changed.

13. **Dependencies**

    13A–13D. Supported repair must exist before City/Description become publication prerequisites.

### Checkpoint 13F — PostgreSQL Active Aggregate Integrity — COMPLETED

1. **Goal**

   Make the Active publication invariant true for every conforming PostgreSQL transaction, including direct persistence outside supported handlers.

2. **Why it exists**

   Domain/Application checks cannot prevent raw SQL, future infrastructure mistakes, or cross-row races; a column `CHECK` cannot require child existence.

3. **Exact scope**

   - Add the second migration with named, reversible statement-level trigger functions/triggers described in Section 8.2.
   - Install set-based Listings activation guards and Active translation insert/update/delete guards using event-specific transition tables.
   - Lock distinct old/new parent IDs in canonical UUID order, make each accepted Draft child statement touch affected parent MVCC versions set-wise, and define parent deletion/cascade and ListingId-move behavior.
   - Acquire write-conflicting table locks, install guards, then validate all pre-existing Active rows in the same migration transaction; fail rather than backfill.
   - Treat every post-validation check/trigger violation as sanitized/logged `server.unexpected`, never a parsed 409.
   - Adapt only database-trigger-driven setup left after 13E: raw status helpers and remaining integration premises whose zero-translation/null-City/null-Description Active state is no longer committable. Ordinary object builders and private-Status compile fallout already belong to 13E; explicitly corrupt entities remain unit-only.
   - Change query-review seeding to Draft-first/translations-next/one set-based final-status command while preserving the exact final profile and all 61 invariants; no per-listing application update loop is allowed.

4. **Expected source areas/files or architectural surfaces**

   - new EF migration/model snapshot and PostgreSQL functions/triggers;
   - persistence error/logging boundary and database-integrity/catalog/concurrency tests;
   - focused listing builders/raw SQL helpers and contradictory integration premises;
   - `tools/RealEstate.QueryReview/DeterministicProfileSeeder.cs` ordering only.

5. **Contract/invariant established**

   PostgreSQL now guarantees that an Active listing has at least one translation, every attached row meets all four publication requirements, and no translation can be added/changed/removed while its parent remains Active. Draft City/Description columns remain nullable.

6. **Tests required**

   - direct Active insert/transition rejects zero, null, or mixed-invalid translations and accepts all-valid sets;
   - Active child insert/update/delete rejects; Draft equivalents succeed; unpublish-before-edit succeeds;
   - multi-row statement/transition-table behavior proves set-based checking and deterministic old/new parent locking;
   - concurrent child/status writers preserve integrity; an out-of-band writer may be deadlock-victimized but no invalid commit succeeds;
   - exact `REPEATABLE READ` adversarial interleaving: stale activation snapshot versus committed incomplete Draft child mutation must abort activation because the child trigger version-touched the parent;
   - cascade/no-parent and ListingId move behavior;
   - migration fails atomically on pre-existing malformed Active data and leaves neither partial guards nor claimed truth;
   - trigger/function/check catalog shape, clean fresh apply, repeat database update, Down path, snapshot/pending-model checks;
   - every trigger/check exception after supported validation is sanitized 500;
   - query-review profile uses exactly one set-based final-status seed command, contains no per-row status loop/trigger query, creates successfully, and returns the same 61/61 final invariants; record setup elapsed time without inventing an undocumented threshold.

7. **Regression tests that must remain unchanged**

   Valid lifecycle authorization/results, search/filter/q/location/pagination/comparables, agency public behavior, image concurrency, and final query-review dataset/result identities. Fixtures may become valid without altering unrelated assertions.

8. **Migration implications**

   Add one migration, provisionally `EnforceActiveListingPublicationIntegrity`, with explicit locking, reversible named SQL, guarded validation, and no production backfill/delete. This is the aggregate migration after 13A's row migration.

9. **OpenAPI implications**

   None; consumes 13E's error contract and does not expose PostgreSQL messages.

10. **Performance/query implications**

    Write-side only. Require set-based trigger plans and profile-seed runtime compatibility; do not recapture public read benchmarks. Preserve the trigram index and benchmark artifacts unchanged.

11. **Explicit exclusions**

    No per-row trigger design, deferred generic trigger framework, Active content edit, public eligibility joins, backfill, or taxonomy-specific readiness.

12. **Completion/acceptance criteria**

    No conforming PostgreSQL transaction—including the adversarial stale-`REPEATABLE READ` interleaving—can create or mutate malformed Active translation state; migrations and trigger catalog are reproducible; the healthy 100,000/200,000 profile finishes with identical final invariants via set-based status seeding; no hot public query changes.

13. **Dependencies**

    13A–13E.

### Checkpoint 13G — Truthful Public DTO and OpenAPI Contract — COMPLETED

1. **Goal**

   Expose the now-real Active guarantee without overstating Draft/private management state.

2. **Why it exists**

   The shared nullable `ListingResponse` cannot truthfully express both incomplete management state and required public identity.

3. **Exact scope**

   - Add `PublicListingResponse` with nonnullable LanguageCode, Title, City, Description.
   - Route public list/detail/agency list/comparables and publish success through it.
   - Keep existing nullable `ListingResponse` on create, `/my`, dashboard, unpublish, archive.
   - Keep `ListingAuthoringResponse` for management detail/update.
   - Add explicit strict public mapper/readiness assertion; no `!`, `?? ""`, or fake fallback.
   - Fail materialized corruption as canonical unexpected server failure with structured logging.
   - Change comparable Active-source missing language/city from empty success to integrity failure without altering its SQL projection; do not project/scan source-only Title/Description, whose integrity is guaranteed by 13F.
   - Extend paged OpenAPI schema handling for public and management types.

4. **Expected source areas/files or architectural surfaces**

   - listing DTOs/mappings and public handlers;
   - public methods in `ListingsController`/`AgenciesController`;
   - comparable result/error branch;
   - OpenAPI schema filter/document tests;
   - public/private behavior and mapper unit tests.

5. **Contract/invariant established**

   Old public contract: `ListingResponse`/`PagedResponse<ListingResponse>`, four core translated fields optional/nullable. New public contract: `PublicListingResponse`/paged/list variants with LanguageCode, Title, City, Description required and non-null. Private/Draft flattened fields remain nullable.

6. **Tests required**

   Every public surface returns all four values; strict mapper rejects malformed entity; public/private paged schemas are distinct; OpenAPI required arrays and nullable flags are exact; comparable source language/city integrity branch and strict candidate mapping; lifecycle response data/types.

7. **Regression tests that must remain unchanged**

   Seven-member pagination, sorting/filters/counts, requested/mk/fallback, q field set/wildcards, agency route behavior, comparable order, failure media type/codes, request/trace IDs, security, media URLs, enum strings.

8. **Migration implications**

   None; consumes 13E application truth and 13F database truth.

9. **OpenAPI implications**

   This is the public contract checkpoint. Assert endpoint `$ref`s, required/nonnullable core fields, management nullability, publish response type, errors, and no generic envelope. Generated frontend type may tighten only after these tests pass.

10. **Performance/query implications**

    Mapping occurs after materialization. Public repository LINQ/SQL and selector must remain byte-for-byte/topologically unchanged except non-SQL type plumbing. Focused generated-SQL comparison; no planned recapture.

11. **Explicit exclusions**

    No public integrity filter/join, selector change, Description q-search, frontend edit, or global DTO rewrite.

12. **Completion/acceptance criteria**

    Runtime, mapper, DTO, Swagger, and contract tests agree on four required public fields; management remains truthful; no public SQL topology or result semantics changed.

13. **Dependencies**

    13F.

### Checkpoint 13H — Canonical Geocoded Location State and Draft Contract — NEWLY REQUIRED

1. **Goal**

   Establish the listing-level location state and eliminate arbitrary coordinate authoring before any external provider workflow or stronger Active invariant is introduced.

2. **Why it exists**

   The current parent coordinates have the correct physical ownership but no provenance, precision, confirmation, encapsulation, or PostgreSQL coherence. Combining that field/schema correction with a live provider and irreversible Active enforcement would make review, rollback, and data compatibility unsafe.

3. **Exact scope**

   - Add the provider-neutral location precision enum and coherent listing-level geocoded snapshot members defined in Section 2.3.
   - Encapsulate Latitude/Longitude mutation behind explicit Domain confirm/clear behavior; EF materialization remains supported.
   - Remove Latitude/Longitude from create and full-replacement request contracts and validators; callers cannot write precision or provenance.
   - Keep nullable read-only location state on private/management responses so Draft UX can show resolution status.
   - Conservatively clear all stored location state, including transitional legacy-unverified coordinates, when the normalized complete translation set changes City, Municipality, AddressLine, or Neighborhood.
   - Add one forward nullable schema migration with optional-text row checks, coordinate pair/range checks, and unresolved/legacy-unverified/confirmed snapshot coherence.
   - Audit existing coordinate/location rows before enforcing checks; fail on incompatible partial/blank data rather than silently repairing it.
   - Capture and review production-generated SQL after the mapped root columns land. Recapture every actually changed locked command and establish the post-location baseline under Section 15.

4. **Likely source areas/files or architectural surfaces**

   `Listing`, new Domain enum/value semantics, create/update requests and validators, create handler, `ListingDraftReplacementEngine`, authoring/private DTO mappings, EF configurations/model snapshot/new migration, OpenAPI request/management tests, migration integration tests, query-review capture/verification artifacts and evidence.

5. **Invariant established**

   Supported create/PUT cannot set coordinates. A Draft location is unresolved, explicitly legacy-unverified for migration compatibility, or a fully coherent confirmed snapshot; partial state is impossible through Domain/DB paths. Location-text edits cannot leave a stale confirmed pin.

6. **Tests required**

   Domain confirm/clear/coherence/ranges; direct setter inaccessibility; create/PUT model binding and OpenAPI absence of coordinate inputs; Draft optionality; normalized no-op versus meaningful location-text invalidation; persistence round-trip; row/check-constraint rejection; legacy-coordinate compatibility; fresh/repeat/Down migration and pending-model verification; private/management response truth; all affected frozen SQL commands, result IDs/totals, plans, buffers/spills, and medians.

7. **Regression tests that must remain unchanged**

   Create/update core validation, translation ID reconciliation, subtype conversion, authorization, parent-lock serialization, lifecycle behavior, public result semantics, q/location/comparable behavior, images, ProblemDetails, and existing public DTO contract until 13K.

8. **Migration implications**

   One new forward migration; never edit 13A/13F. New columns are nullable for staged deployment. Named checks enforce optional text normalization and root location coherence/ranges. Down removes only this checkpoint's objects. No Active tightening and no fabricated coordinate/provenance backfill.

9. **OpenAPI implications**

   Remove Latitude/Longitude from create/PUT request schemas. Keep nullable read-only location state in management/private responses. Do not tighten `PublicListingResponse` yet.

10. **Performance/query implications**

    Adding mapped Listing scalar columns changes root SELECT projections even when LINQ is untouched. This checkpoint owns the mandatory stop/review and affected-command Chapter 10F recapture across N1/P1/P2/A1/R1/L1/Q1/C1. Predicates, selector, count, q/location, ranking, joins, limit, and child hydration must remain semantically unchanged. The accepted capture becomes the baseline for 13I–13K.

11. **Explicit exclusions**

    No provider package/call, candidate endpoint, production adapter, publish/readiness change, Active trigger tightening, strict public field change, PostGIS/catalog/search feature, frontend edit, or privacy masking.

12. **Completion/acceptance criteria**

    - No supported create/PUT coordinate input remains in runtime or Swagger.
    - Root location mutation and coherence are encapsulated and tested.
    - Location text invalidates confirmation exactly as specified.
    - The forward migration is fresh/repeat/Down clean and the EF model has no pending change.
    - Existing data has an explicit compatibility/audit outcome; no partial snapshot can persist.
    - Every changed frozen SQL command has accepted plan/result/performance evidence; every unaffected command is exact; no query semantic/topology change is accepted.

13. **Dependencies**

    Completed 13A–13G and explicit approval of the Section 2 field model.

### Checkpoint 13I — Backend-Mediated Geocoding Resolution — NEWLY REQUIRED

1. **Goal**

   Provide a secure, provider-neutral, user-confirmed path from stored human-readable Draft location text to the canonical geocoded snapshot.

2. **Why it exists**

   An external dependency has separate ambiguity, credential, timeout, quota, privacy, testing, and operational risks. It must be proven before publication begins requiring its output.

3. **Exact scope**

   - Add an Application-owned geocoding port and provider-neutral search/candidate/confirmed-snapshot contracts.
   - Add a configured Infrastructure adapter for an owner-approved provider while keeping provider SDK/types out of Domain/API contracts.
   - Add authenticated listing-scoped Draft candidate search, confirmation, and explicit clear operations at `POST /api/listings/{id}/location/candidates`, `PUT /api/listings/{id}/location`, and `DELETE /api/listings/{id}/location`.
   - Build search input from one selected stored translation's City, Municipality, AddressLine, and optional Neighborhood; return provider-neutral label, preview coordinates, precision, and opaque confirmation token.
   - Bind short-lived protected tokens to listing, actor, provider result, normalized input fingerprint, and expiry. Confirmation accepts no numeric coordinates.
   - Re-resolve/validate outside the transaction, then use the existing parent write scope to reauthorize current Draft, reject stale input/token, and persist provider output atomically.
   - Add timeout/cancellation, bounded transient retry, safe configuration/startup validation, narrow rate limits, provider-compliant caching/retention, and sanitized dependency logging/errors.
   - Use deterministic fake adapters for automated tests and local contract seams; never call a live provider from the test suite.

4. **Likely source areas/files or architectural surfaces**

   New Application location feature folders/contracts/handlers, Application provider interface, Infrastructure adapter and options, API controller/request/response metadata, dependency injection/configuration, data-protection/token component, rate-limiting policy, existing authoring write scope, Chapter 12 ProblemDetails/logging boundary, and focused unit/integration/API tests.

5. **Invariant established**

   Every supported confirmed location is derived from a configured backend provider result that the authorized user previewed and confirmed against the current Draft text. The frontend can see preview coordinates but cannot persist arbitrary numbers or stale candidates.

6. **Tests required**

   Personal and agency Owner/Agent authorization; Disabled/Pending/Manager/nonmember behavior according to existing management rules; Draft-only status; missing/inaccessible listing; candidate ordering/shape; exact and broad precision mapping; token tampering/expiry/actor/listing/input binding; provider re-resolution; text-change and publish races; parent-lock atomicity; clear/idempotency; timeout/cancellation/retry limits; rate limiting; configuration failure; sanitized 4xx/409/503 ProblemDetails and logging; no secret/address/raw-provider leakage; deterministic fake-adapter tests.

7. **Regression tests that must remain unchanged**

   Existing create/update/publish authorization ordering, no provider call during Publish, lifecycle concurrency, public queries/responses, images, Chapter 12 request correlation/single-owner logging, and 13H SQL baseline.

8. **Migration implications**

   None planned; 13H owns storage. If provider terms require a materially different persisted field, stop and return to the field-model gate rather than hiding a schema change here.

9. **OpenAPI implications**

   Document candidate, confirm, and clear operations; provider-neutral precision and preview shape; opaque token input; established auth/errors/correlation. Do not expose API keys, provider-specific raw responses/IDs, or writable coordinate properties.

10. **Performance/query implications**

    New authenticated write/dependency paths are outside public discovery. Bound provider calls and database query counts; no change to N1/P1/P2/A1/R1/L1/Q1/C1. Generated SQL must remain exact against the 13H baseline.

11. **Explicit exclusions**

    No frontend-direct geocoder, publish-time geocoding, provider call inside a DB transaction, arbitrary coordinate confirmation, provider-specific public/Domain type, PostGIS, geography catalog, viewport/radius search, clustering, or frontend implementation.

12. **Completion/acceptance criteria**

    - A production-capable provider adapter, its terms/retention/quota, and deployment configuration are explicitly approved and verified.
    - Candidate search and token confirmation work for authorized personal/agency Drafts.
    - Arbitrary/stale/tampered coordinate selection cannot persist.
    - Resilience, rate, privacy/logging, and deterministic-test requirements pass.
    - Provider failure leaves a truthful unresolved Draft; Publish still makes no provider call.
    - Public/query SQL remains exact to 13H.

13. **Dependencies**

    13H and owner/operator provider selection before checkpoint acceptance.

### Checkpoint 13J — Strong Location Publication and PostgreSQL Integrity — NEWLY REQUIRED

1. **Goal**

   Make the expanded location requirement a Domain/Application/PostgreSQL Active invariant after the resolution workflow exists.

2. **Why it exists**

   Provider capability alone does not make publication truthful. Readiness and PostgreSQL must prevent every supported or direct conforming transaction from creating or retaining an Active listing without complete localized location text and a confirmed map snapshot.

3. **Exact scope**

   - Extend `ListingPublicationReadinessViolationCode` with `InvalidMunicipality`, `InvalidAddressLine`, `MissingConfirmedLocation`, `InvalidLatitude`, `InvalidLongitude`, `InvalidLocationPrecision`, and `InvalidGeocodingProvenance` (or an audited equally explicit one-to-one code set).
   - Extend `EvaluatePublicationReadiness()` and `Listing.Publish()` so every translation requires City/Municipality/AddressLine/Description and the root requires a coherent confirmed snapshot.
   - Preserve authorization-before-readiness, existing 409 `conflict.listing_not_ready`, resource-state rules, agency prerequisites, and valid already-Active idempotency.
   - Add a second new forward migration that extends the 13F assertion functions/triggers rather than editing their migration history.
   - Validate stronger translation/root Active truth and freeze root geocoded-location mutation while Active; unpublish remains the supported correction path.
   - Preserve statement-level transition tables, canonical parent locking, parent MVCC touch, stale-`REPEATABLE READ` protection, cascade/no-parent behavior, and set-based profile compatibility.
   - Audit/remediate existing Active rows through supported unpublish/resolve/publish workflow before migration; migration fails atomically on any remaining incompatible row.

4. **Likely source areas/files or architectural surfaces**

   `Listing`, publication-readiness result/codes, publish handler/tests, test builders/seeders, Listing/translation EF check metadata, new migration SQL/functions/triggers/model snapshot, PostgreSQL migration/constraint/trigger/concurrency integration tests, integrity exception/logging tests.

5. **Invariant established**

   Every committed Active listing has at least one translation; every translation has canonical LanguageCode and meaningful Title/City/Municipality/AddressLine/Description; the root has valid paired coordinates, precision, provider provenance, and confirmation time; those translation/location facts cannot mutate while Active.

6. **Tests required**

   Domain readiness null/blank/whitespace and root pair/range/precision/provenance matrix; Draft and already-Active publish; personal/agency authorization-before-readiness; 409 versus typed 500 corruption; fresh/repeat/Down migration; trigger catalog; direct activation rejection for every violation; Active root-location mutation rejection; unpublish-then-change; multirow statements; parent deletion; old/new parent movement; supported writer serialization; stale `REPEATABLE READ`; install-time validation failure; set-based 100,000/200,000 seed with zero malformed Active rows.

7. **Regression tests that must remain unchanged**

   All completed 13E/13F authorization/lifecycle/concurrency semantics, Chapter 11 images, public visibility/fallback/search/pagination/comparables, candidate workflow, and Chapter 12 sanitized errors/logging.

8. **Migration implications**

   One new forward enforcement migration; never edit 13F. It acquires write-conflicting locks in the established order, installs guards, validates all Active rows, and commits atomically. No fake centroid, provider identity, timestamp, or coordinate backfill. With 13A, 13F, 13H, and 13J, Chapter 13 has four migrations.

9. **OpenAPI implications**

   Readiness remains the existing sanitized 409 contract. Runtime public DTO annotations for the newly guaranteed fields remain staged until 13K; no temporary frontend handoff is allowed. No public integrity error schema is added.

10. **Performance/query implications**

    Write-side Domain/trigger changes only. Public/comparable LINQ, projections, predicates, selectors, ranking, joins, and hydration remain exact against the 13H baseline. Profile seeding attaches explicit trusted test resolution metadata set-wise; no live provider call.

11. **Explicit exclusions**

    No query eligibility/readiness filter, usable-row predicate, corruption hiding, DTO/OpenAPI tightening, provider redesign, repair/backfill utility, Active content editing, PostGIS/search change, or frontend work.

12. **Completion/acceptance criteria**

    - Domain and Application enforce the complete stronger readiness rule with existing disclosure ordering.
    - No conforming PostgreSQL transaction can commit malformed or location-mutated Active state.
    - Existing Active compatibility has an explicit audited outcome; enforcement never fabricates truth.
    - Migrations/concurrency/profile tests pass and the public SQL baseline is unchanged.
    - Supported readiness is 409; impossible materialized corruption remains typed/sanitized 500.

13. **Dependencies**

    13I and completion of the existing-Active remediation gate.

### Checkpoint 13K — Truthful Public Location DTO, OpenAPI, and SQL Closure — NEWLY REQUIRED

1. **Goal**

   Expose the stronger persisted Active truth through strict runtime mapping and generated API documentation, and prove no read semantics changed after the 13H projection rebaseline.

2. **Why it exists**

   13J makes the data true, but public CLR/OpenAPI contracts still describe Municipality, AddressLine, Latitude, and Longitude as nullable and do not expose precision. Tightening them before database truth would be dishonest; mixing the contract audit into the trigger migration would be too large.

3. **Exact scope**

   - Extend `PublicListingResponse` so Municipality, AddressLine, Latitude, Longitude, and LocationPrecision join LanguageCode, Title, City, and Description as required/non-null.
   - Extend `ToPublicResponse(...)` after materialization, reusing `EvaluatePublicationReadiness()` and `EffectiveTranslationOrdering`; assign real selected/root values and throw `PublicListingIntegrityException` on impossible state.
   - Preserve nullable `ListingResponse` for create, `/my`, dashboard, unpublish, archive and Draft-truthful `ListingAuthoringResponse` for management GET/PUT.
   - Preserve provider/provenance internals; expose only precision and the confirmed public coordinates/location text.
   - Apply the strict contract automatically to public list/detail/agency list/comparables/publish, which already use `PublicListingResponse`.
   - Update OpenAPI schema handling/tests for DTO requiredness/nullability, endpoints, paged public/private separation, management truth, enums/media/errors/security, and request coordinate exclusion.
   - Capture all 33 production SQL commands and require exact equality with the accepted 13H post-location baseline.

4. **Likely source areas/files or architectural surfaces**

   Listing DTOs/mappers/integrity exception, public/publish/comparable tests, API OpenAPI schema filter/document tests, controllers for metadata orientation only, query-review capture/comparison evidence, and checkpoint documentation.

5. **Invariant established**

   Every strict public surface and Swagger schema truthfully guarantees the effective localized identity plus Municipality, AddressLine, valid public coordinates, and honest precision. Draft/private/management contracts remain nullable where resolution/completeness is optional.

6. **Tests required**

   Strict mapper exact values and every new corruption case; all five strict public surface families; create/private/lifecycle/management nullability; public fields required, string/decimal/enum and non-null in serialized OpenAPI; endpoint `$ref`s; public/private seven-member pagination; no provenance/internal exception schema; candidate API coordinate-input boundary; canonical 500 logging; complete 33-command SQL equality and relevant 61 profile invariants.

7. **Regression tests that must remain unchanged**

   Translation fallback, list/detail/agency behavior, pagination/count, q/location/wildcards, comparable eligibility/ranking/order/limit, publish authorization/readiness, lifecycle responses, Chapter 12 ProblemDetails/security/media/enums/request IDs, and 13I workflow.

8. **Migration implications**

   None; consumes 13J truth. Any requested migration indicates scope/model drift and stops the checkpoint.

9. **OpenAPI implications**

   This is the final contract checkpoint. Public required/non-null fields are LanguageCode, Title, City, Municipality, AddressLine, Description, Latitude, Longitude, and LocationPrecision. Private flattened and authoring location members remain nullable; create/PUT have no writable coordinates; all endpoint schemas/errors/security/media/enums remain accurate.

10. **Performance/query implications**

    Strictness is post-materialization. Do not add a translation `Any`, readiness filter, integrity join, source-only comparable AddressLine/Title/Description/coordinate projection, selector change, q/location change, ranking change, or extra hydration. All 33 commands must equal 13H exactly; a mismatch stops closeout.

11. **Explicit exclusions**

    No query redesign, migration, provider workflow change, structured-error framework, public provenance, privacy masking, benchmark blessing of changed semantics, frontend regeneration, or unrelated DTO refactor.

12. **Completion/acceptance criteria**

    - Runtime mapper/DTO/serialized OpenAPI/tests agree on all nine required public location/identity members.
    - Private and management truth remains nullable and all-translations authoring remains intact.
    - Every strict public endpoint references the right schema and exposes no provider internals.
    - Corruption remains typed internal failure to sanitized `server.unexpected` with one structured log owner.
    - Generated SQL is 33/33 exact to 13H, profile invariants pass, and result semantics are unchanged.

13. **Dependencies**

    13J and the accepted 13H post-location SQL baseline.

### Checkpoint 13L — Comprehensive Verification and Frontend Handoff — MOVED FINAL CLOSEOUT

1. **Goal**

   Verify the complete reopened chapter, record actual evidence, update durable backend context, and hand the truthful backend contract and geocoding flow to the paused frontend work.

2. **Why it exists**

   The final system now crosses authoring, external-provider resilience, authorization, concurrency, two layers of PostgreSQL integrity, strict mapping, OpenAPI, and a controlled SQL rebaseline. Focused checkpoint evidence is necessary but insufficient for final closeout.

3. **Exact scope**

   - Run all focused Domain/Application/provider/PostgreSQL/API/concurrency/OpenAPI/query suites for 13A–13K.
   - Run Release build and the complete backend suite; record exact totals/skips.
   - Apply all migrations to fresh PostgreSQL, repeat update, test relevant Down paths, inspect catalog objects, and prove no pending model changes.
   - Smoke personal and agency create → management location text → candidate search → map preview → token confirmation → publish → every public surface; include broad precision, stale token, provider outage, not-ready, unpublish/repair, and unauthorized paths.
   - Verify provider configuration/startup/resilience with a controlled adapter; no live external dependency is required for deterministic closeout.
   - Reverify the accepted post-location SQL baseline, 61 deterministic profile invariants, result IDs/totals/order, and all performance gates.
   - Update `docs/backend-context.md`, `docs/backend-quality-handoff.md`, Chapter 13 completion evidence, migration counts, and historical security/config deferral to reflect final source truth.
   - Produce a backend-only frontend handoff defining address entry, candidate/precision presentation, map preview/confirmation, stale/unresolved state, errors, and generated contract. Do not modify the frontend.

4. **Likely source areas/files or architectural surfaces**

   Entire backend solution verification, four Chapter 13 migrations/catalog, provider adapter/configuration seam, Swagger provider/document tests, query-review artifacts/profile, context/quality/chapter planning evidence, and backend-only handoff documentation. No new feature implementation belongs here.

5. **Invariant established**

   No new behavior. This checkpoint proves and freezes the integrated stronger Chapter 13 contract from human input/provider confirmation through committed PostgreSQL truth to strict public map responses.

6. **Tests required**

   Focused cumulative chapter suites, full suite, Release build, fresh/repeat/Down migrations, catalog and pending-model verification, controlled provider/resilience/configuration smoke, personal/agency end-to-end flows, serialized OpenAPI, exact SQL/profile/performance verification, and documentation/evidence audit.

7. **Regression tests that must remain unchanged**

   All Chapter 08–12 guarantees, especially auth/invitations, agency ownership/roles, visibility, search/filter/pagination/comparables, images/concurrency/compensation, ProblemDetails/request IDs/logging, health/CORS/media/security/OpenAPI, plus completed 13A–13G evidence.

8. **Migration implications**

   All four Chapter 13 migrations (13A, 13F, 13H, 13J) must apply from empty and through the real upgrade order, have accurate relevant Down behavior, match the snapshot, leave no pending model change, and never fabricate confirmed location data.

9. **OpenAPI implications**

   Generate and inspect the serialized backend v1 document. Prove strict public location, Draft/private/management nullability, geocoding workflow input/output, all endpoint schemas/errors/security/media/enums/correlation, and absence of internal/provider types. Frontend generation remains outside this backend checkpoint.

10. **Performance/query implications**

    Reconfirm exact final equality to the accepted 13H post-location baseline and all 61 profile invariants. No new recapture is allowed during closeout; a mismatch or failed plan/result gate reopens the owning checkpoint rather than being blessed here. Do not recapture unrelated large profiles.

11. **Explicit exclusions**

    No cleanup branch, opportunistic refactor, new provider feature, frontend implementation, taxonomy expansion, spatial discovery, privacy feature, JWT/config Chapter 16 work, or new behavior after verification begins.

12. **Completion/acceptance criteria**

    - Release build is zero warnings/zero errors and full/focused suites are green with exact totals.
    - Fresh/repeat/relevant Down migration, catalog, and pending-model checks pass for all four Chapter 13 migrations.
    - Controlled provider workflow and all authorization/resilience/correlation/error cases pass.
    - OpenAPI proves all nine strict public fields, Draft/management truth, and safe workflow schemas.
    - Final SQL is exact to 13H, profile/performance/result gates pass, and no discovery semantics changed.
    - Durable backend context/handoff documents are current and evidence is source-consistent.
    - A backend-only frontend handoff is complete; only then may the owner authorize frontend regeneration and resume Chapter 2C.

13. **Dependencies**

    13A–13K.

## 20. Final Verification/Closeout Requirements

Chapter 13 is complete only when all of the following are true:

- supported create/update cannot persist malformed translation rows or caller-authored coordinates;
- a Draft can be retrieved/replaced in full, may remain publication-incomplete, and exposes truthful nullable location-resolution state;
- an authorized Draft can resolve provider-neutral candidates, preview precision, and confirm an opaque selection through the backend without a live provider call inside its transaction;
- Active publication requires every translation's LanguageCode, Title, City, Municipality, AddressLine, and Description;
- Active publication also requires valid paired Latitude/Longitude, non-null provider-neutral precision, backend resolution provenance, and confirmation time;
- Neighborhood remains optional and broad pins are labelled with truthful non-exact precision;
- personal/agency authorization and authorization-before-readiness disclosure ordering remain intact;
- update/location/status races serialize on the Listing parent and stale selection tokens cannot overwrite newer Draft text;
- PostgreSQL validates activation, blocks Active translation/location mutation, and aborts stale-snapshot activation after concurrent Draft child change;
- public list/detail/agency list/comparables/publish use strict `PublicListingResponse`; create/`my`/dashboard/unpublish/archive remain nullable `ListingResponse`; management GET/PUT retain all translations and Draft nullability;
- impossible materialized Active corruption becomes `PublicListingIntegrityException`, one structured internal log, and sanitized canonical `server.unexpected` without provider/listing/readiness detail;
- OpenAPI agrees with runtime on all nine public required fields, private/management nullability, workflow schemas, pagination, errors, security, media, and enums;
- q remains Title/City/Municipality/Neighborhood; AddressLine, coordinates, and precision do not become discovery/comparable inputs; fallback, paging, ranking, and agency reuse remain unchanged;
- 13H's approved scalar-projection rebaseline is documented and final SQL is exact against it with profile/result/performance gates green;
- Chapter 11 image guarantees, Chapter 12 failure/pagination/observability guarantees, and completed 13A–13G architecture pass unchanged;
- all four Chapter 13 forward migrations apply cleanly to fresh/upgrade PostgreSQL without fabricated location backfill and leave no pending EF model change;
- actual Release, complete tests, smoke, OpenAPI, provider, migration, profile, and query evidence is recorded;
- frontend remains untouched until it consumes the verified generated contract after the owner completes the manual Git step;
- PostGIS/spatial discovery, canonical geography IDs, public pin privacy, taxonomy, global cleanup, and provisional Chapter 16 security/config remain deferred.

Chapter 13 does not close merely because coordinates are non-null, a provider returned a candidate, or Swagger flags changed. It closes when the backend proves that localized public location text and the confirmed physical map snapshot agree with Domain, PostgreSQL, strict mapping, serialized OpenAPI, query/performance evidence, and every public response.
