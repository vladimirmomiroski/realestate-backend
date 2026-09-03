# Chapter 14 — Property Model and Taxonomy Expansion

Status: final architecture and execution authority. Chapter 14 is planned but not implemented. The implementation sequence in section 19 is binding unless a later owner-approved architecture change amends this document.

Repository baseline reviewed: `main` at `8b572c6`, tagged `backend-public-listing-integrity-authoring-v1`, with Chapter 13 closeout `9741b6f`, Chapter 13L.1 cumulative verification `ade6618`, and Chapter 13K.3 SQL freeze `f6f6322`.

## 1. Status and purpose

Chapter 13 is complete and is the protected baseline. Chapter 14 expands the Listing property model and its fixed taxonomy without reopening Chapter 13's public integrity, authoring, location, lifecycle, authorization, concurrency, or error contracts.

This document is based on, in descending authority:

1. current production source, the EF model and migration chain, generated OpenAPI behavior, and current tests;
2. [Chapter 13 closeout](chapter-13-public-listing-integrity-authoring.md), [Chapter 13L.1 cumulative verification](chapter-13l1-cumulative-chapter-13-verification-gate.md), and [Chapter 13K.3 SQL freeze](../benchmarks/chapter-10f/chapter-13k3-final-generated-sql-freeze-proof.md);
3. [backend frontend handoff](../backend-frontend-handoff.md), [backend context](../backend-context.md), and [backend quality handoff](../backend-quality-handoff.md);
4. the current Chapter 12 API/OpenAPI/error closeout and Chapter 10 discovery/query records;
5. older chapter records only as historical evidence where newer source and records do not supersede them.

The repository convention places authoritative chapter documents under `docs/chapters`. This file is the sole Chapter 14 architecture and execution plan. It is not implementation evidence and is not a replacement for generated OpenAPI.

### Chapter goal

Add Commercial and Land as first-class root property types, give each the smallest explicit fixed subtype model, and integrate that model through supported authoring, persistence, public/private/management reads, generated OpenAPI, existing root-type discovery, and verification. Preserve the established Apartment and House contract and every Chapter 13 integrity boundary.

### Current truth, target truth, and future work

| Classification | Truth |
|---|---|
| Current truth | Supported authoring has only Apartment and House. Each uses a property-specific one-to-one child. `PropertyType` has two members; Create and replacement logic contain two-type assumptions. PostgreSQL has 20 migrations. Public discovery and comparables use root `PropertyType`. |
| Target Chapter 14 truth | Four root types exist: Apartment, House, Commercial, Land. Commercial and Land each have one fixed subtype enum and one explicit one-to-one child. POST and PUT support exactly one matching child. All read contracts faithfully expose the new subtype. |
| Deferred/future work | Dynamic/admin taxonomy, amenities/features, additional Commercial/Land metrics, subtype filters, subtype-aware comparables, representative new-type performance distributions, broad API/performance/hardening, final frontend reconciliation, and documentation consolidation. |

No unresolved owner decision blocks this architecture.

## 2. Current backend truth

### 2.1 Aggregate and current property model

`Listing` is the aggregate root. `ListingTranslation`, `ListingImage`, `ListingApartmentDetails`, and `ListingHouseDetails` are children. Child entities are reached through Listing navigations; the DbContext intentionally has no child-detail `DbSet` properties.

The current root property fields relevant to Chapter 14 are:

| Field | CLR / database representation | Create and PUT behavior | Draft / Active role | Public/private/management exposure | Discovery / comparables | Validation / database invariant |
|---|---|---|---|---|---|---|
| `ListingType` | non-null enum; `varchar(50) NOT NULL` name string | POST/PUT required; PUT replaces | supported-authoring required; no separate readiness check | all three response families | exact filter; comparable equality | Create/PUT defined-value check; no DB allow-list |
| `PropertyType` | non-null enum; `varchar(50) NOT NULL` name string | POST/PUT required; matching child required | supported-authoring required; no subtype readiness check | all three response families | exact filter; comparable equality | Create/PUT defined-value check; no DB allow-list or cross-table check |
| `Price` | `decimal`; `numeric(18,2) NOT NULL` | required; PUT replaces | positive at request boundary | all three | range filters/sort; comparable input | request `> 0`; no DB check |
| `Currency` | non-null string; `varchar(3) NOT NULL` | POST defaults EUR; normalized upper case; PUT required | request boundary | all three | exact filter; comparable equality | three ASCII letters; no DB format check |
| `AreaSquareMeters` | `decimal`; `numeric(10,2) NOT NULL` | required; PUT replaces | positive at request boundary | all three | range filter; comparable input | request `> 0`; no DB check |
| `Rooms` | `decimal?`; `numeric(4,1) NULL` | optional; PUT omission/null clears | informational | all three | range filter; no comparable role | no current range check |
| `Bathrooms` | `decimal?`; `numeric(4,1) NULL` | optional; PUT omission/null clears | informational | all three | no filter/comparable role | no current range check |
| `BalconyCount` | `int?`; `integer NULL` | optional; PUT omission/null clears | informational | all three | none | request nonnegative; no DB check |
| `ParkingSpaces` | `int?`; `integer NULL` | optional; PUT omission/null clears | informational | all three | none | request nonnegative; no DB check |
| `HasBasement` | `bool?`; `boolean NULL` | optional; PUT omission/null clears | informational | all three | exact nullable filter | no additional check |
| `IsExchangePossible` | `bool?`; `boolean NULL` | optional; PUT omission/null clears | informational | all three | none | no additional check |
| `HeatingType` | non-null enum; `varchar(50) NOT NULL DEFAULT 'Unknown'` | omission defaults/resets Unknown | informational | all three | exact filter | PUT defined-value check; Create currently omits it |
| `FurnishingStatus` | same storage/default pattern | omission defaults/resets Unknown | informational | all three | exact filter | PUT defined-value check; Create currently omits it |
| `Condition` / `PropertyCondition` | same storage/default pattern | omission defaults/resets Unknown | informational | all three | exact filter | PUT defined-value check; Create currently omits it |
| `Orientation` | same storage/default pattern | omission defaults/resets Unknown | informational | all three | none | PUT defined-value check; Create currently omits it |
| `YearRenovated` | `int?`; `integer NULL` | optional; PUT omission/null clears | informational | all three | none | request 1800–2100 and not before YearBuilt |
| `YearBuilt` | `int?`; `integer NULL` | optional; PUT omission/null clears | informational | all three | none | only current cross-check is YearRenovated ordering |
| `PricePerSquareMeter` | response-only computed decimal | never accepted in requests | derived | public and private, not management | ranking computes its own ratio | zero when area is non-positive |

Ownership, status, auditing, translations, images, and trusted location are not taxonomy, but they are part of the aggregate and protected against Chapter 14 regressions.

Current detail children:

| Child | Shape | Database | Supported authoring | Exposure | Query role |
|---|---|---|---|---|---|
| `ListingApartmentDetails` | `ListingId`, `ApartmentType`, `Floor?`, `TotalFloors?`, `HasElevator?` | shared UUID PK/FK, cascade; subtype `varchar(50) NOT NULL DEFAULT 'Unknown'` | required for Apartment POST/PUT and forbidden for House; nonnegative floors and Floor ≤ TotalFloors | nullable object in all three responses | `apartmentType` and `hasElevator` filters; no comparable role |
| `ListingHouseDetails` | `ListingId`, `HouseType`, `NumberOfFloors?`, `YardAreaSquareMeters?` | shared UUID PK/FK, cascade; subtype `varchar(50) NOT NULL DEFAULT 'Unknown'` | required for House POST/PUT and forbidden for Apartment; nonnegative numeric values | nullable object in all three responses | `houseType` and yard-range filters; no comparable role |

The response objects are nullable even though supported writes require the matching child. Neither PostgreSQL nor public eligibility currently enforces exactly one matching detail row.

### 2.2 Current taxonomy

| Enum | Exact current values | Default | Persistence / wire / localization |
|---|---|---|---|
| `ListingType` | `Sale=1`, `Rent=2` | no semantic default | database names; response JSON/OpenAPI names; not server-localized |
| `PropertyType` | `Apartment=1`, `House=2` | no semantic default | same |
| `ApartmentType` | `Unknown=0`, `Studio`, `Standard`, `Penthouse`, `Duplex`, `Loft`, `Maisonette`, `Other` | Unknown | child name string; response JSON/OpenAPI names |
| `HouseType` | `Unknown=0`, `Detached`, `SemiDetached`, `Terraced`, `Townhouse`, `Villa`, `Cottage`, `Other` | Unknown | child name string; response JSON/OpenAPI names |
| `HeatingType` | `Unknown`, `None`, `Electric`, `Central`, `Gas`, `Wood`, `HeatPump`, `Other` | Unknown | root name string; response JSON/OpenAPI names |
| `FurnishingStatus` | `Unknown`, `Unfurnished`, `SemiFurnished`, `Furnished` | Unknown | same |
| `PropertyCondition` | `Unknown`, `New`, `Renovated`, `Good`, `NeedsRenovation` | Unknown | same |
| `Orientation` | Unknown plus current cardinal/intercardinal directions | Unknown | same |

`ListingStatus` and `LocationPrecision` are categorical enums but are lifecycle/location concepts, not Chapter 14 taxonomy.

There is no taxonomy table, reference-data repository, label-localization service, cache, admin taxonomy endpoint, amenity/feature collection, or generic attribute store.

JSON responses emit enum names and generated OpenAPI advertises names. PostgreSQL stores names. The configured `JsonStringEnumConverter` also accepts numeric JSON tokens; a defined ordinal is therefore accepted unless endpoint validation says otherwise. Chapter 14 keeps that global compatibility behavior and explicitly rejects undefined materialized values.

### 2.3 Current authoring, reads, and lifecycle

- `POST /api/listings` authenticates the creator, validates, authorizes personal/agency ownership, creates a Draft, persists one Apartment or House detail, and returns `ListingResponse`.
- `GET /api/listings/{id}/management` loads the complete aggregate and returns `ListingAuthoringResponse` with all translations and Draft-capable nullable state.
- `PUT /api/listings/{id}` performs full Draft replacement. Optional nullable root fields clear on omission/null; optional enums reset to Unknown; translations are the complete authoritative set; subtype transitions remove the former child.
- Create currently validates `ListingType` and `PropertyType` but misses defined-value checks for common categorical enums and current subtype enums.
- Update validates common enums, but its subtype validator and `ListingDraftReplacementEngine` use an Apartment branch with a non-Apartment fallback that assumes House.
- The public `propertyType` filter is an exact optional enum equality. `GetListingsValidator` does not currently reject undefined numeric values materialized into enum filters.
- POST persists its newly created aggregate in one repository SaveChanges boundary and needs no parent lock. PUT, publish, and the Chapter 13 serialized Listing mutation paths relevant to this chapter use a Read Committed transaction and `SELECT ... FOR UPDATE` on the Listing parent. Their authorization/state decisions occur against the post-lock aggregate. Publish authorization precedes readiness evaluation.
- Publication readiness requires complete canonical translations and a complete trusted root location snapshot. It does not require a subtype child or non-Unknown subtype.
- Location coordinates/provenance are backend-owned. PUT clears a confirmed location only when the canonical localized location identity changes.

### 2.4 Current persistence, OpenAPI, and query freeze

- The repository has exactly 20 historical migrations, ending with `20260824141614_EnforceStrongActiveLocationIntegrity`.
- Enum properties use string conversions; no `Listings.PropertyType` schema change is needed to store future names.
- Generated OpenAPI is the machine-readable contract. The three response DTOs are intentionally separate.
- Chapter 13K.3 freezes 33 production commands across eight public/comparable shapes, a 61-invariant 100,000-listing profile, and 198 EXPLAIN executions.
- Public root reads use reference Includes for Apartment/House details, then split translation/image hydration. Comparables use root `PropertyType` equality and six unchanged ranking keys.

## 3. Authoritative Chapter 14 scope

### 3.1 Evidence classification

A. Explicitly deferred vocabulary:

- `Commercial` and `Land`;
- `Office`, `Shop`, `BuildingPlot`, and `AgriculturalLand`;
- the new subtype storage and integration required to make those names usable.

The records name these concepts, but do not explicitly declare their hierarchy. Root/subtype placement is the architecture decision in section 5, inferred from category level and the existing broad-type-plus-specific-subtype model.

B. Strongly supported model gaps:

- unsafe two-type fallbacks in Create/PUT paths;
- Create/PUT defined-enum validation asymmetry;
- no Commercial/Land storage, authoring, mapping, materialization, or OpenAPI representation;
- undefined numeric enum filters reaching query semantics;
- query/profile safety proof required when new one-to-one joins widen frozen root materialization.

C. Speculative ideas excluded from the target:

- amenities/features;
- bedrooms as a new root field;
- garage or any extra root/subtype;
- new heating, furnishing, condition, orientation, or parking concepts;
- arbitrary Commercial or Land measurements/metadata;
- configurable/localized reference data, stable taxonomy IDs, EAV, JSON attributes, or schema engines.

Only A and the bounded B items form Chapter 14.

### 3.2 Chapter outcome

Chapter 14 is backend-complete for the chosen property taxonomy when Commercial and Land can be safely created, replaced, persisted, read through every existing applicable response surface, discovered using the existing root filter, and returned by comparables under the existing root eligibility contract. It does not require broader subtype discovery or valuation behavior.

## 4. Final target property model

Add exactly these two Domain enums:

```text
CommercialType
  Unknown = 0
  Office = 1
  Shop = 2
  Other = 3

LandType
  Unknown = 0
  BuildingPlot = 1
  AgriculturalLand = 2
  Other = 3
```

Append, without renumbering:

```text
PropertyType
  Apartment = 1
  House = 2
  Commercial = 3
  Land = 4
```

Add exactly two dependent entities:

```text
ListingCommercialDetails
  ListingId: Guid
  Listing: Listing
  CommercialType: CommercialType = Unknown

ListingLandDetails
  ListingId: Guid
  Listing: Listing
  LandType: LandType = Unknown
```

Add nullable navigations:

```text
Listing.CommercialDetails: ListingCommercialDetails?
Listing.LandDetails: ListingLandDetails?
```

No new common Listing column and no extra subtype metric is added.

### 4.1 Why explicit one-to-one children

Alternatives considered:

| Alternative | Decision |
|---|---|
| nullable subtype columns on Listing | rejected: mixes category-only fields into the root and diverges from Apartment/House shape |
| one unified details table with many nullable fields/discriminator | rejected: less explicit, still needs conditional rules, and creates an unnecessary abstraction |
| relational subtype lookup tables/IDs | rejected: no runtime lifecycle, admin, localization, or stable-ID requirement |
| enum-only root categories such as Office or BuildingPlot | rejected: mixes hierarchy levels and loses the established root/subtype structure |
| generic polymorphic/EAV/JSON model | rejected: sacrifices type safety and makes validation, querying, migrations, and OpenAPI harder |
| two explicit children matching current patterns | selected: smallest coherent extension and clearest persistence/API shape |

Four explicit child types are intentionally preferable to a generic property-detail hierarchy. Similar code is acceptable when it keeps each fixed subtype obvious.

### 4.2 Common-field applicability

All existing common optional fields remain optional/informational for all four root types. Chapter 14 neither rejects nor automatically clears Rooms, Bathrooms, balconies, parking, basement, heating, furnishing, condition, renovation/construction years, orientation, or exchange possibility when PropertyType becomes Commercial or Land.

That is a conservative compatibility decision, not a declaration that every field is meaningful for Land. No authoritative applicability matrix exists. Rejecting or silently clearing these fields would invent destructive product policy. A future product-approved matrix is deferred.

## 5. Final taxonomy decisions

- Commercial and Land are root `PropertyType` values.
- Office and Shop are `CommercialType` values.
- BuildingPlot and AgriculturalLand are `LandType` values.
- `Unknown` means the fixed subtype is not specified. It is valid in Draft and Active.
- `Other` means a known subtype outside the named fixed vocabulary.
- `ListingType` remains exactly Sale/Rent. All eight ListingType × PropertyType combinations are supported.
- Exact CLR numeric identities are explicit and stable. Existing ordinals never change.
- PostgreSQL stores exact enum names as strings.
- JSON responses emit symbolic names; generated OpenAPI advertises symbolic names.
- Existing numeric JSON enum ingress remains globally accepted when the value maps to a defined member. Validators reject undefined numeric values. Chapter 14 does not globally set `allowIntegerValues: false`.
- Taxonomy values are not server-localized and have no database IDs. Display labels remain a frontend concern.

## 6. Domain and supported-authoring invariants

### 6.1 Domain responsibility

Domain owns:

- exact enum vocabulary and numeric identities;
- the two child entity shapes, their Unknown defaults, and Listing navigations;
- existing local Listing lifecycle/location/readiness behavior, which remains unchanged.

Chapter 14 does not add a generic taxonomy service, polymorphic hierarchy, property-detail helper, or new Listing mutator abstraction. Current Listing/detail setters are public and current detail children are mutable data objects. A new helper would be bypassable unless the aggregate were broadly encapsulated, which is outside scope.

The following remain Application request rules rather than newly claimed Domain invariants:

- exactly one matching request child;
- Apartment/House numeric validation;
- all 16 supported PUT transitions;
- handler construction and stale-child clearing.

Create and replacement code must still use explicit switch expressions/statements with unsupported-value programming guards so no hidden fallback survives.

### 6.2 Supported-authoring invariant

After every successful POST or PUT:

```text
PropertyType = Apartment  => ApartmentDetails exists; House/Commercial/Land details absent
PropertyType = House      => HouseDetails exists; Apartment/Commercial/Land details absent
PropertyType = Commercial => CommercialDetails exists; Apartment/House/Land details absent
PropertyType = Land       => LandDetails exists; Apartment/House/Commercial details absent
```

This is guaranteed by validators plus `CreateListingHandler` within POST's repository SaveChanges boundary, or `ListingDraftReplacementEngine` within PUT's parent-locked authoring write scope. It is not claimed for arbitrary direct SQL or malformed historical rows.

## 7. Authoring semantics

### 7.1 Exact POST contract

Matching/exclusive payload matrix:

| `propertyType` | Required non-null child | All forbidden when non-null |
|---|---|---|
| Apartment | `apartmentDetails` | `houseDetails`, `commercialDetails`, `landDetails` |
| House | `houseDetails` | `apartmentDetails`, `commercialDetails`, `landDetails` |
| Commercial | `commercialDetails` | `apartmentDetails`, `houseDetails`, `landDetails` |
| Land | `landDetails` | `apartmentDetails`, `houseDetails`, `commercialDetails` |

New request shapes:

```text
CreateListingCommercialDetailsRequest
  commercialType: CommercialType = Unknown

CreateListingLandDetailsRequest
  landType: LandType = Unknown

CreateListingRequest
  commercialDetails: CreateListingCommercialDetailsRequest?
  landDetails: CreateListingLandDetailsRequest?
```

The parent child properties are schema-optional/nullable because their requirement is conditional. A supplied child has a non-nullable enum property whose omission uses Unknown.

Validation stays first-failure. Preserve unrelated current ordering and make taxonomy ordering explicit:

1. request presence where applicable;
2. `listingType` and supported `propertyType`;
3. Price/Area/Currency;
4. defined `heatingType`, `furnishingStatus`, `condition`, and `orientation`;
5. translations and current optional numeric/cross-field rules;
6. the matching-child rule: missing target fails on its child key; any nonmatching non-null child fails on `request`; an undefined matching subtype fails on `xDetails.xType`; existing subtype-specific numeric rules follow;
7. POST `agencyId` validation remains in its current final position.

Defined-value checks apply to current and new subtype enums. Unknown is defined and valid. Invalid requests return the existing `validation.failed` ValidationProblem; contradictory children are never ignored.

The canonical taxonomy validation keys are `propertyType`, `apartmentDetails`, `houseDetails`, `commercialDetails`, `landDetails`, `apartmentDetails.apartmentType`, `houseDetails.houseType`, `commercialDetails.commercialType`, and `landDetails.landType`. A nonmatching or multiple-child shape uses `request`; scalar detail rules retain their existing nested keys.

After successful validation, `CreateListingHandler` uses an exhaustive four-way switch, constructs only the matching child with the Listing identity/back-reference, creates a Draft, and preserves current personal/agency authorization, normalization, and auditing. It accepts no coordinates/provenance.

### 7.2 Exact PUT full-replacement contract

New request shapes:

```text
UpdateListingCommercialDetailsRequest
  commercialType: CommercialType = Unknown

UpdateListingLandDetailsRequest
  landType: LandType = Unknown

UpdateListingRequest
  commercialDetails: UpdateListingCommercialDetailsRequest?
  landDetails: UpdateListingLandDetailsRequest?
```

The same four-way required/exclusive/defined-value rule applies. PUT remains full replacement, not patch.

The handler order remains:

1. principal/account preconditions;
2. begin the existing write scope and lock the Listing parent;
3. reload the complete tracked aggregate;
4. re-evaluate authorization;
5. enforce Draft state;
6. validate and normalize the request;
7. apply root, translations, and subtype replacement;
8. save once and commit.

For every source type × target type combination:

- update the already tracked target child in place when it exists;
- create the target child when it is missing;
- set each of the other three navigations to null;
- rely on EF tracking/orphan deletion to delete stale dependent rows at SaveChanges;
- commit root change, stale deletes, and target insert/update atomically.

The exhaustive target switch has an explicit unsupported default; it never treats “not Apartment” as House. A successful replacement repairs a malformed Draft that lacks its target child or contains stale incompatible tracked children. Same-type replacement avoids replacing a tracked shared-PK dependent instance, preventing tracking/delete-insert conflicts.

All root/common properties retain existing replacement behavior. Omitted/null optional values clear; omitted optional enums become Unknown. Translations remain the authoritative complete set and preserve server-owned translation IDs by canonical language. Images, creator, agency, status, and audit history keep current rules.

Changing PropertyType or subtype does not itself clear the confirmed location. Only the existing canonical localized-location comparison may clear it.

### 7.3 Safe implementation staging

Adding new `PropertyType` values before hardening the two-type switches would be unsafe. The execution order therefore is binding:

1. first make Create/PUT fail closed for every property type they do not yet support;
2. add dormant storage/read foundations;
3. append Commercial/Land and activate POST;
4. activate PUT immediately afterward.

Tasks 14E and 14F are one coordinated, non-deployable release unit. The midpoint is intentionally data-safe—POST can create a new type and PUT returns validation for that type—but it is not a complete Chapter 14 API and must not be deployed, released, or declared contract-complete.

## 8. Public, private, and management contracts

The response DTOs remain separate. Add:

```text
ListingCommercialDetailsResponse
  commercialType: CommercialType     # required/non-null inside an existing object

ListingLandDetailsResponse
  landType: LandType                 # required/non-null inside an existing object
```

Exact parent exposure:

| Contract | New members | Parent requiredness/nullability | Semantics |
|---|---|---|---|
| `PublicListingResponse` | `commercialDetails`, `landDetails` | optional and nullable | faithful public classification; absent navigations serialize null |
| `ListingResponse` | same | optional and nullable | private/effective-translation response; Draft tolerant |
| `ListingAuthoringResponse` | same | optional and nullable | management returns all four subtype slots for full-replacement authoring |
| `CreateListingRequest` | Create-specific objects | optional and nullable in schema | runtime conditionally required/exclusive |
| `UpdateListingRequest` | Update-specific objects | optional and nullable in schema | runtime conditionally required/exclusive |

Default System.Text.Json configuration does not omit nulls, so an absent detail object serializes as explicit JSON `null`. Mappings directly reflect loaded navigations, matching current Apartment/House behavior. They do not hide incompatible children or add a new public fail-closed subtype-shape rule.

Public exposure is part of Chapter 14: without it, Office/Shop and BuildingPlot/AgriculturalLand would disappear from public representation. This exposes product classification, not persistence IDs or management internals.

All Chapter 13 public strict localized/location members remain required and non-null. `ListingResponse` remains effective-translation based and Draft-nullable. `ListingAuthoringResponse` retains all translations, ownership/lifecycle timestamps, and truthful nullable location state.

## 9. Draft and Active semantics

| Field/group | Draft | Active |
|---|---|---|
| root ListingType/PropertyType | defined value required by supported authoring | unchanged |
| matching detail object | required/exclusive at POST/PUT boundary | no new readiness rule; DTO remains nullable for malformed direct data |
| CommercialType/LandType | defined; Unknown allowed | Unknown remains valid and informational |
| existing common optional attributes | nullable/Unknown | informational; not newly required |
| translations | current Draft flexibility | all Chapter 13 canonical/completeness requirements remain |
| trusted location | may be unresolved | complete confirmed snapshot remains required |

An Active Commercial listing may validly have `CommercialType.Unknown`; an Active Land listing may validly have `LandType.Unknown`. The root category is still truthful and current Apartment/House subtypes already permit Unknown. No evidence justifies blocking publication or fabricating subtype choices.

`Listing.EvaluatePublicationReadiness` and its violation codes remain unchanged. Public mapping retains Chapter 13 fail-closed translation/location integrity. Chapter 14 adds no readiness condition, database readiness trigger, or compatibility audit.

## 10. Persistence and migration

### 10.1 EF/PostgreSQL shape

Create:

```text
ListingCommercialDetails
  ListingId uuid PRIMARY KEY
  CommercialType varchar(50) NOT NULL DEFAULT 'Unknown'
  FOREIGN KEY (ListingId) REFERENCES Listings(Id) ON DELETE CASCADE

ListingLandDetails
  ListingId uuid PRIMARY KEY
  LandType varchar(50) NOT NULL DEFAULT 'Unknown'
  FOREIGN KEY (ListingId) REFERENCES Listings(Id) ON DELETE CASCADE
```

Use two explicit configurations under `RealEstate.Infrastructure/Persistence/Configurations`. Add no child `DbSet` and no secondary index; each shared PK already indexes `ListingId` and Chapter 14 adds no subtype predicate.

PostgreSQL guarantees:

- a child cannot exist without a Listing;
- at most one row per subtype table per Listing;
- each subtype string is non-null and bounded;
- parent deletion cascades.

PostgreSQL intentionally does not guarantee:

- child/root PropertyType correspondence;
- exactly one of four detail tables;
- mutual exclusion across tables;
- membership of subtype strings in current CLR enum names;
- publication readiness.

Cross-table triggers/discriminator constraints would complicate transaction ordering and would be inconsistent with current Apartment/House persistence. Aggregate business shape remains a supported-Application-write guarantee.

### 10.2 Forward migration

Add one normal forward migration, conceptually `AddCommercialAndLandPropertyTaxonomy`, after the current 20 migrations. It must:

1. create only the two tables above;
2. update its generated designer and the EF snapshot;
3. leave `Listings.PropertyType` unchanged because it is already `varchar(50)`;
4. create no root/detail data and perform no backfill;
5. preserve every existing Draft/Active Apartment/House row and every Chapter 13 trigger/constraint;
6. leave no pending EF model change.

Historical migrations/designers are immutable.

Required lifecycle proof:

- all 21 migrations apply to a fresh database;
- migration 20 → 21 preserves representative Apartment/House, translation, and trusted-location data;
- repeated Up is a no-op;
- Down to migration 20 drops only the new tables;
- re-Up recreates them;
- catalog assertions prove exact columns/defaults/PK/FK/cascade/implicit PK indexes;
- EF round-trips both new name-backed enums;
- no pending model changes remain.

Down necessarily destroys Chapter 14 subtype rows. It must not rewrite Commercial/Land roots to Apartment/House or fabricate replacements. Migration-first/application-second is the deployment order. Once new root values have been written, an old binary cannot safely materialize them; rollback requires an operator to prove/remediate zero Commercial/Land roots. This is a normal additive-enum deployment limitation, not a forward data gate.

### 10.3 Compatibility gate

No J.2-style gate is required. The forward migration creates empty dependent tables, scans/rejects no existing rows, adds no Active invariant, and needs no fabricated data. `CH13-J2-DEPLOY-01` remains open and separately owned for first deployment of Chapter 13 strong Active location integrity.

## 11. Search and comparables

### 11.1 Discovery

The existing optional `propertyType` query parameter becomes:

| Property | Final behavior |
|---|---|
| Serialized type | symbolic `PropertyType` advertised as Apartment, House, Commercial, Land; defined numeric input remains compatible |
| Semantics | one exact root value |
| Predicate | `listing.PropertyType == value` |
| Null | no PropertyType predicate |
| Placement | before count, ordering, Skip/Take |
| Index | no new index; current predicate architecture remains |
| Agency public reuse | unchanged shared Active public repository path |
| OpenAPI | enum expands; no new parameter |

No LINQ predicate change is needed for valid new values. Chapter 14 does add `Enum.IsDefined` validation for every supplied enum filter currently present: ListingType, PropertyType, HeatingType, FurnishingStatus, Condition, ApartmentType, and HouseType. Successfully materialized undefined numeric values return canonical 400 ValidationProblem. Malformed symbolic text remains a model-binding failure. Valid filter SQL must remain unchanged.

Chapter 14 does not add `commercialType` or `landType` query parameters. Faithful storage/authoring/read representation makes the taxonomy usable; new discovery dimensions are broader Chapter 15 integration. No subtype index is added without a predicate and measurements.

Preserve exactly:

- Active eligibility;
- filters before count/page;
- effective translation requested language → `mk` → bytewise language → translation UUID;
- deterministic ordering and offset paging;
- shared agency-public path;
- `q` over Title, City, Municipality, and Neighborhood only.

Do not add subtype names, Description, AddressLine, or enum labels to `q`.

### 11.2 Comparables

Comparable eligibility continues to require exact root `PropertyType` equality. Therefore Commercial compares only with Commercial and Land only with Land.

`CommercialType` and `LandType` affect neither eligibility nor ranking. No current subtype affects comparable ranking, and the repository contains no valuation product rule for these new subtypes. Preserve ListingType, Currency, positive Price/Area, effective-language equality, City eligibility, limit 6, and the existing six ranking keys/order.

Comparable responses include the new nullable details through public mapping. Subtype-aware eligibility/ranking is deferred to Chapter 15 and would require a separate product rule, deterministic SQL, regression analysis, and performance proof.

## 12. Authorization, concurrency, and failure contract

### 12.1 Authorization

Chapter 14 changes no actor or ownership behavior:

- active authenticated users may create personal Drafts;
- existing Owner/Agent agency checks govern agency creation/management;
- PUT remains Draft-only and ownership/agency-authorized;
- publish/unpublish/archive authorization and visibility remain unchanged;
- authorization remains before publication readiness.

Focused Commercial/Land cases prove parity; Chapter 14 does not duplicate every existing Chapter 13 matrix.

### 12.2 Concurrency

All PUT transitions remain inside `IListingAuthoringRepository.BeginWriteAsync`:

```text
Read Committed transaction
  → SELECT Listing parent FOR UPDATE
  → load complete tracked aggregate
  → post-lock authorization/state validation
  → mutate root/children
  → one SaveChanges
  → commit
```

Adding both children to `CompleteAggregateQuery` keeps them within the existing parent serialization boundary. That covers PUT/PUT, PUT/publish, PUT/location, and dependent delete/create transitions. No child lock, row version, ETag, or generic concurrency framework is added.

Race tests use existing deterministic lock hooks and drain all tasks. A discovered defect may receive a bounded correction inside the same write scope; it must not trigger a global redesign.

### 12.3 Failures

No stable error code is added.

- invalid/undefined enums, missing matching child, extra incompatible child, and numeric validation use `validation.failed` with field-keyed ValidationProblem;
- authentication and authorization use existing `authentication.*` / `authorization.*`;
- concealed/absent resources use `resource.not_found`;
- non-Draft/stale state uses `conflict.resource_state`;
- unchanged readiness uses `conflict.listing_not_ready`;
- unexpected invariant/storage defects use `server.unexpected`.

Unsupported-value exceptions after successful validation are programming guards, not a new public failure.

## 13. API and generated OpenAPI

Generated OpenAPI must show:

- `PropertyType` names Apartment, House, Commercial, Land;
- exact `CommercialType` and `LandType` names;
- Create/Update subtype request schemas;
- response subtype schemas whose inner enum member is required/non-null;
- nullable/optional parent references on Create, Update, PublicListingResponse, ListingResponse, and ListingAuthoringResponse;
- unchanged unconditional POST/PUT required arrays, POST EUR default, PUT full-replacement/Unknown-reset semantics;
- a four-way matching-child description on all four request child properties;
- expanded `propertyType` query enum;
- no `commercialType` / `landType` query parameter;
- unchanged Chapter 13 required public translation/location fields and canonical error responses.

The schema cannot naturally express “exactly one child and it must match PropertyType.” Clear descriptions plus runtime validation/API tests are authoritative. Preserve the existing nullable-reference `oneOf` wrapper; Chapter 14 does not introduce a polymorphic/discriminator four-variant `oneOf` to encode PropertyType conditionality.

The global enum converter remains unchanged. Documentation must not claim string-only ingress: names are the advertised form, defined numeric tokens remain accepted, and undefined values are rejected by validation.

## 14. Query and performance policy

### 14.1 Exact expected SQL delta

Adding Commercial/Land Includes to shared public root materialization changes exactly these frozen identities:

```text
N1-02-page-root
P1-02-page-root
P2-02-page-root
A1-03-page-root
R1-02-page-root
L1-02-page-root
Q1-02-page-root
C1-02-comparable-ranked-root
```

Each changed root adds two `LEFT JOIN` clauses and four projected columns: each dependent's `ListingId` plus its subtype name. Aliases may change only as unavoidably emitted by EF.

Exactly 25 identities are expected normalized-text exact against Chapter 13K.3:

- seven filtered counts;
- `A1-01-agency-existence`;
- `C1-01-comparable-source`;
- eight translation child loads;
- eight image child loads.

Command count remains 33. Private/my/dashboard roots and management roots also gain Includes but are outside the frozen eight-shape matrix; focused integration/query-count tests cover them.

If implementation produces a different identity count or any unexpected diff, verification stops. The plan is amended or the defect is corrected; acceptance is never broadened mechanically.

### 14.2 Narrow Chapter 14 performance gate

Chapter 14 owns proof only for its intentional materialization widening:

1. apply all 21 migrations to a fresh disposable PostgreSQL 16 database;
2. preserve the existing Chapter 10F v2 deterministic profile: 100,000 Apartment/House listings and 61/61 invariants;
3. run profile create and independent profile verify;
4. capture all 33 commands and 80 typed parameter records through production repositories;
5. prove every typed parameter record exact and the expected 25 command bodies normalized-text exact against `docs/benchmarks/chapter-10f/evidence/sql/*.sql`, using Chapter 13K.3 hashes/manifests as trust anchors;
6. review the eight root diffs for only the expected joins/four projections/aliases;
7. run all 198 EXPLAIN executions because current sequence and plan gates are suite-wide;
8. preserve result identities, command/parameter counts, deterministic ordering, zero spill/temp anomalies, and current Q1 gates;
9. inspect measured impact and existing PK-index/catalog evidence without requiring PostgreSQL to use an index for empty new tables;
10. record a concise versioned Chapter 14 SQL-delta proof adjacent to the existing benchmark documents.

The existing QueryReview code, 61-invariant contract, Apartment/House distribution, Chapter 10F 69-file baseline, `BaselineEvidenceWriter`, and historical Chapter 13K.3 proof remain unchanged. Raw captures/plans live in disposable temp or ignored task evidence. Do not run `baseline export` and do not overwrite/relabel the Chapter 10 baseline.

A representative Commercial/Land workload, exporter/baseline modernization, subtype-filter plans, reindexing, and general tuning belong to Chapter 15. `CH13-PERF-01` remains open and is not a Chapter 14 success criterion.

## 15. Tests and verification strategy

Tests ship with the behavior they prove. Do not reproduce the same trivial enum assertion at every layer.

| Concern | Owning proof |
|---|---|
| numeric enum stability and Unknown defaults | focused Domain/model tests |
| request shape and defined enum values | Create/Update validator tests plus representative API 400 tests |
| child construction | Create handler/API persistence tests |
| all 16 replacement transitions | data-driven replacement-engine/PostgreSQL integration tests; representative HTTP cases only |
| same-type tracked child update and orphan deletion | persistence/replacement integration |
| public/private/management mapping and null serialization | mapping and endpoint tests at their respective read-contract tasks |
| migration catalog/lifecycle | dedicated PostgreSQL migration family |
| root PropertyType discovery | listing query/API tests |
| comparable root isolation/ranking preservation | comparable repository/API tests |
| parent serialization | focused deterministic PUT/PUT, PUT/publish, and PUT/location races |
| generated schemas | `OpenApiDocumentTests` |
| frozen query delta/plans | late QueryReview verification proof |
| cumulative state | final Release build, full suite, model/migration and evidence checks |

High-value requirements:

- validators cover each target type, missing target, every nonmatching child position, multiple children, Unknown, undefined root/common/subtype numeric values, and stable field keys;
- the replacement transition table covers 4 × 4 source/target combinations at one authoritative layer, including target-child repair and deletion of every stale dependent;
- endpoint authorization uses representative personal and Owner/Agent agency cases, not a duplicate full matrix;
- location tests prove classification-only replacement preserves confirmation and actual canonical location-text change still clears it;
- malformed direct database shape remains tolerated by nullable reads; supported writes repair the tracked Draft shape;
- public tests preserve Active-only visibility, strict effective translation/location mapping, paging/count/order, agency reuse, and explicit null response members;
- Chapter 13 publication, translation, location, authorization-before-readiness, and parent-lock regressions remain green.

Every production/test-changing task must run its focused tests first, then the repository-mandated Release build and complete Release test suite. The cumulative task repeats the assembled final state. SQL/plan work is not duplicated at cumulative closeout when its immutable proof remains unchanged.

Every focused filter must select a nonzero, recorded test count. Use `dotnet test --list-tests` to confirm a future method-name selector before relying on it; a zero-selected command is a failure, not evidence.

## 16. Backend quality-handoff interaction

| Open item | Chapter 14 classification | Chapter 14 rule |
|---|---|---|
| `QH-TX-01` transaction cleanup exception replacement | untouched/out | no agency-owner transaction cleanup refactor |
| `QH-TEST-01` undrained agency concurrency request tasks | out | new Chapter 14 race tests must drain their own tasks; do not absorb unrelated helper cleanup |
| `CH11-DB-01` nullable Listing creator | untouched/out | no creator/ownership schema change |
| `CH11-DB-02` request rules not broadly duplicated in DB | affected, not owned | add only the two tables' structural constraints |
| `CH11-STATE-01` broad freshness/concurrency policy | affected, not owned | preserve the Listing parent lock; no global policy |
| `CH11-FILE-01` post-commit file deletion durability | untouched/out | no media work |
| `QH-TEST-02` raw-SQL listing fixture drift | affected, not owned | new raw SQL/catalog fixtures name required columns; no global fixture rewrite |
| `C12-CONFIG-01` production JWT placeholder | explicitly out | no config/security hardening |
| `CH13-J2-DEPLOY-01` first-deployment target-zero gate | explicitly out | remains open; no Chapter 14 substitute gate |
| `CH13-PERF-01` translation-guard write amplification | context only, not owned | do not close, tune, or rebaseline it |

Chapter 14 owns none of these open items. Closeout reviews their status but may change the register only if actual implementation evidence discovers a new issue or genuinely resolves one under separately authorized scope.

## 17. Explicit exclusions and Chapter 15 boundary

### 17.1 Explicit exclusions

Chapter 14 excludes:

- amenities or generic feature taxonomy;
- EAV, JSON attribute bags, dynamic schema, relational/admin-editable taxonomy, localized taxonomy services, or stable taxonomy IDs;
- any root/subtype beyond the exact final vocabulary;
- arbitrary Commercial metrics or Land metrics;
- a guessed property-type applicability matrix for existing common fields;
- `commercialType` / `landType` discovery filters or multi-value filtering;
- subtype-aware comparable eligibility/ranking;
- `q` expansion;
- PostGIS, radius, polygon, viewport search, canonical geography IDs, or public pin obfuscation;
- authentication/security/JWT/config hardening;
- background jobs or notifications;
- media redesign;
- agency workspace or role expansion;
- generic repository, UnitOfWork, MediatR, AutoMapper, FluentValidation, or architecture rewrite;
- global optimistic concurrency/ETags;
- unrelated Chapter 11/12 work or cleanup;
- frontend code, generated frontend clients, or edits to the Chapter 13 frontend handoff;
- final whole-backend frontend reconciliation or documentation consolidation;
- historical migration, Chapter 10 baseline, or Chapter 13 evidence edits.

### 17.2 Chapter 15 stop line

Chapter 14 stops when the fixed model is authorable, persistent, faithfully readable, represented in generated OpenAPI, discoverable by the existing root filter, compatible with root-only comparables, and safely verified.

Chapter 15 retains integration through discovery, API, performance, and hardening, including:

- deciding and implementing subtype filters;
- realistic Commercial/Land profile distributions and measured subtype indexes;
- any subtype-based valuation/comparable rule;
- QueryReview exporter/baseline modernization or a new broad permanent baseline;
- broader endpoint reconciliation/hardening;
- performance work not caused solely by Chapter 14's two root joins;
- integration of future product-approved attributes.

After Chapter 15, the project performs the owner-approved full backend-to-frontend handoff/reconciliation, then documentation consolidation, then frontend integration.

## 18. Implementation and audit workflow

### 18.1 Dependency and release rules

- A task starts only after every listed dependency has owner-accepted `FINAL_PASS`.
- Start with the repository/branch/status/log/tag freeze used by this chapter. Stop rather than overwrite any unexpected change; expected dependency commits must be identifiable.
- The sequence is strict unless the owner approves a documented plan amendment.
- Each task has one expected owner commit.
- Tasks 14E and 14F are separately reviewable commits but one coordinated non-deployable release unit. Never deploy or declare Chapter 14 contract-complete between them.
- No task author commits, pushes, merges, switches, rebases, stashes, resets, restores, cleans, or rewrites history. The repository owner performs commits after acceptance.
- Historical migrations and accepted Chapter 10/13 evidence remain immutable.

### 18.2 Ignored implementation evidence

Every task creates or updates exactly its named ignored file under `docs/planning/`. The evidence remains uncommitted unless the owner explicitly directs otherwise. Read-only preparation and audit passes must not create or modify planning evidence.

Every task evidence file records:

- exact task scope and interpretation;
- starting repository/branch/status;
- changed files, including untracked files;
- behavior, API, persistence, query, authorization, and compatibility consequences;
- exact commands and complete pass/fail results;
- Release build and complete test results for implementation tasks;
- migration inventory/model state where applicable;
- deviations, discoveries, and unresolved findings;
- final `git status --short`;
- a COMPLETE unified diff of all implementation, test, migration, generated, durable evidence, and documentation changes, excluding only the ignored planning evidence file itself.

The complete diff requirement applies to untracked artifacts. The implementation pass must enumerate `git ls-files --others --exclude-standard` and include a read-only no-index diff from `/dev/null` for each untracked task artifact; it must not stage files merely to obtain a diff. Secrets, live credentials, and disposable raw profile output never enter evidence.

### 18.3 Review and acceptance

Every task follows:

```text
implementation and focused verification
  → implementation self-review
  → separate fresh read-only adversarial audit
  → bounded correction if any High/Medium/Low finding
  → evidence refresh
  → new fresh targeted read-only audit
  → audit declares FINAL_PASS
  → owner manually commits
```

The implementation chat may report readiness for review but cannot self-declare final acceptance. A fresh audit inspects actual source, complete tracked/untracked diff, tests, and ignored evidence. The audit does not edit source or planning evidence. Any severity finding requires correction and re-audit; accepted known limitations must already be explicit in this plan or owner-approved.

### 18.4 Architectural dependency groups

The task sequence implements these bounded groups:

1. fail-closed current boundaries;
2. dormant Domain/EF/migration foundation;
3. management then shared public/private read representation;
4. coordinated POST and PUT activation;
5. discovery, comparable, concurrency, and OpenAPI proof;
6. frozen SQL delta acceptance;
7. cumulative technical verification and durable chapter closeout.

No group is an umbrella implementation task. Tasks below are the final decomposition.

## 19. Final implementation task sequence

### 14A — Harden Existing Taxonomy Boundaries

#### Purpose

- Make the current Apartment/House authoring and discovery enum boundaries fail closed before any new root value exists.

#### Why this task exists

- Create misses defined-value validation for six current categorical fields, Update and replacement contain non-Apartment-means-House logic, and public filters accept materialized undefined numeric enums. Adding root values before correcting those assumptions can persist contradictory shapes.

#### Depends on

- none.

#### Exact scope

- Add Create defined-value validation for HeatingType, FurnishingStatus, PropertyCondition, Orientation, ApartmentType, and HouseType.
- Make Create and Update subtype validation use explicit Apartment and House branches plus an unsupported `propertyType` failure, not a catch-all House branch.
- Make Create handler child construction and replacement-engine subtype mutation explicit for Apartment/House with an unsupported-value programming guard.
- Add `Enum.IsDefined` checks for every supplied current public enum filter: ListingType, PropertyType, HeatingType, FurnishingStatus, PropertyCondition, ApartmentType, and HouseType.
- Preserve Unknown as valid wherever it is a defined member and preserve each existing field key/error family.

#### Explicit non-goals

- No Commercial/Land enum, entity, request, response, predicate, migration, or OpenAPI schema.
- No serializer/model-binder change and no global rejection of defined numeric JSON enum tokens.
- No reordering/refactor of unrelated validation.

#### Expected production layers/files

- Domain: none.
- Application: `CreateListingValidator`, `CreateListingHandler`, `UpdateListingValidator`, `ListingDraftReplacementEngine`, `GetListingsValidator`.
- Infrastructure: none.
- Api: none expected.
- Tests: focused validator, replacement guard, and endpoint validation files.
- Docs: ignored task evidence only.

#### Domain impact

- none.

#### Application impact

- Current valid requests behave identically.
- Undefined category values and any defined-but-not-supported future PropertyType fail before mutation.
- Internal unsupported PropertyType reaches an explicit exception rather than House mutation or detail omission.

#### Persistence/migration impact

- none.

#### API/OpenAPI impact

- Numeric values that materialize to undefined enums now return existing 400 `validation.failed` instead of reaching a no-results query or invalid write.
- Malformed symbolic binding behavior, response schemas, and stable errors are unchanged.

#### Query/performance impact

- Valid predicates and SQL are unchanged; invalid requests stop before repository execution.

#### Authorization/concurrency impact

- none; authoring authorization/lock order is unchanged.

#### Tests required

- parameterized Create parity for all current common/subtype enums;
- Create/Update unsupported PropertyType field key and first-failure behavior;
- direct replacement-engine unsupported programming guard; Create proves validator rejection and every valid handler branch without contrived bypass/reflection testing of the sealed validator;
- one valid Unknown case per relevant enum family;
- each query enum filter accepts a defined value and rejects an undefined numeric value;
- representative API validation proves canonical code/problem/request identifiers;
- existing valid request and query regressions.

#### Verification commands

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~CreateListingValidatorTests|FullyQualifiedName~UpdateListingValidatorTests|FullyQualifiedName~GetListingsValidatorTests|FullyQualifiedName~ListingDraftReplacementEngineTests" --logger "console;verbosity=minimal"

dotnet build -c Release --no-restore
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"
```

#### Evidence requirements

- Create `docs/planning/chapter-14a-harden-taxonomy-boundaries-implementation.md`.
- Follow section 18.2; additionally record exact validation order/keys and proof that valid query SQL paths were not edited.

#### Expected commit shape

- 1 commit containing the fail-closed boundary and its tests.

#### Completion criteria

- All current categorical ingress rejects undefined materialized values; no two-type fallback remains in the touched authoring paths; valid Apartment/House behavior and the full suite pass.

#### Review/audit focus

- Hidden `else => House` behavior, accidental Unknown rejection, changed field keys/precedence, numeric binding confusion, and any valid-query SQL change.

### 14B — Add Commercial and Land Persistence Foundation

#### Purpose

- Add the dormant explicit Domain/EF storage model and one forward migration without activating new root authoring.

#### Why this task exists

- The two new classifications need type-safe aggregate homes and relational structure. Keeping model, configurations, migration, snapshot, and lifecycle proof together avoids a committed pending-model state.

#### Depends on

- 14A `FINAL_PASS`.

#### Exact scope

- Add `CommercialType` and `LandType` with the exact ordinals/names in section 4.
- Add `ListingCommercialDetails` and `ListingLandDetails` with Listing identity/back-reference and Unknown defaults.
- Add nullable Listing navigations.
- Configure both shared-PK one-to-one relationships, string conversions, 50-character non-null columns, Unknown database defaults, and parent cascade.
- Generate one migration after migration 20 plus designer/snapshot changes.
- Add catalog, structural child-enum round-trip, ownership/cardinality/cascade, and full lifecycle tests.
- Update only assertions that intentionally describe the current HEAD migration inventory to 21; tests pinned to a named historical Chapter 13 migration retain their historical 20-row expectation.
- Keep root `PropertyType` at Apartment/House in this task.

#### Explicit non-goals

- No root enum activation; no request/response/mapping/repository Include.
- No Domain mutator/helper/hierarchy.
- No child `DbSet`, subtype index, allow-list check, cross-table trigger, backfill, or historical migration edit.
- No QueryReview or OpenAPI change.

#### Expected production layers/files

- Domain: `Enums/CommercialType.cs`, `Enums/LandType.cs`, two child entities, `Listing.cs` navigations.
- Application: none.
- Infrastructure: two entity configurations, one new migration/designer, `RealEstateDbContextModelSnapshot.cs`.
- Api: none.
- Tests: Domain enum/default tests; persistence and a dedicated Chapter 14 migration lifecycle/catalog family.
- Docs: ignored task evidence only.

#### Domain impact

- Adds exact vocabulary, child shapes, defaults, and navigations only.
- Existing publication/location lifecycle behavior is byte/behavior unchanged.

#### Application impact

- none. 14A continues to reject unsupported root types.

#### Persistence/migration impact

- Creates exactly `ListingCommercialDetails` and `ListingLandDetails` as specified in section 10.
- Migration inventory becomes 21; no parent-column alteration or data creation.
- Down drops the two tables and is explicitly destructive only to Chapter 14 subtype rows.

#### API/OpenAPI impact

- none; the new types are not yet reachable from API schemas.

#### Query/performance impact

- No production Include/query changes. The added empty tables do not change current SQL.

#### Authorization/concurrency impact

- none.

#### Tests required

- exact enum ordinals/names and Unknown defaults;
- structural persistence of CommercialType/LandType name strings on an intentionally DB-permitted fixture, without casting undefined root ordinals or claiming a valid Commercial/Land aggregate;
- PK uniqueness, FK rejection, cascade delete, and implicit PK-index catalog assertions;
- fresh 21-migration application;
- migration-20 → 21 preservation of representative Apartment/House, translation, and trusted-location data;
- repeated Up, Down to 20, and re-Up on pre-Chapter-14 data; document rather than fabricate the unavoidable loss of any future subtype rows on Down;
- no fabricated Commercial/Land rows;
- valid Commercial/Land root-plus-matching-child round-trip is deferred to 14E/14F, after the root enum exists;
- no pending model changes;
- existing historical tests pinned intentionally to earlier migrations remain historical rather than mechanically rewritten.

#### Verification commands

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~ListingPersistenceTests|FullyQualifiedName~PostgreSqlCommercialAndLandTaxonomyMigrationTests" --logger "console;verbosity=minimal"

dotnet ef migrations has-pending-model-changes --project src/RealEstate.Infrastructure/RealEstate.Infrastructure.csproj --startup-project src/RealEstate.Api/RealEstate.Api.csproj --configuration Release

dotnet build -c Release --no-restore
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"
```

#### Evidence requirements

- Create `docs/planning/chapter-14b-commercial-land-persistence-foundation-implementation.md`.
- Follow section 18.2; additionally record the exact migration ID, 21-row history, catalog output, upgrade/Down/re-Up behavior, and no-pending-model result.

#### Expected commit shape

- 1 commit. Domain shape, EF configuration, generated migration/snapshot, and their proof are one coherent schema concern; splitting them would leave an invalid pending-model intermediate.

#### Completion criteria

- Both dormant child types round-trip with the exact schema; all 21 migration paths and catalog checks pass; no existing row changes; root authoring remains two-type and fail closed.

#### Review/audit focus

- Existing enum/migration edits, accidental `PropertyType` activation, wrong string/default/cascade shape, missing Listing back-reference, extra indexes/constraints, fabricated data, and untruthful Down claims.

### 14C — Extend the Management Read Contract

#### Purpose

- Make the complete authoring aggregate load and return both new nullable detail slots before write activation.

#### Why this task exists

- Management GET and PUT responses must round-trip the full editable shape, and PUT later needs both dependents tracked under the existing parent lock. This boundary is separate from the shared public/private ListingRepository query freeze.

#### Depends on

- 14B `FINAL_PASS`.

#### Exact scope

- Add the two response detail DTOs.
- Add nullable `commercialDetails` and `landDetails` to `ListingAuthoringResponse`.
- Map both direct navigations in `ListingAuthoringMappingExtensions`.
- Add both Includes to `ListingAuthoringRepository.CompleteAggregateQuery` for read-only management and locked write scopes.
- Prove explicit nulls for absent details and faithful values for present manually seeded details.
- Preserve management translations, IDs, location state, ownership, timestamps, and authorization.

#### Explicit non-goals

- No `ListingResponse` or `PublicListingResponse` change.
- No shared `ListingRepository` Include.
- No POST/PUT new-type request support and no root enum activation.
- No public fail-closed subtype consistency rule.

#### Expected production layers/files

- Domain: none after 14B.
- Application: two detail response DTOs, `ListingAuthoringResponse`, `ListingAuthoringMappingExtensions`.
- Infrastructure: `ListingAuthoringRepository`.
- Api: controller signature unchanged.
- Tests: authoring mapping, repository, and management endpoint tests.
- Docs: ignored task evidence only.

#### Domain impact

- none.

#### Application impact

- Management responses directly represent loaded Commercial/Land navigations as nullable objects.

#### Persistence/migration impact

- no schema change; consumes 14B tables.
- Locked aggregate reads track both new children for later replacement.

#### API/OpenAPI impact

- Management and current PUT response schema gain nullable objects; global final OpenAPI assertions/descriptions close in 14J.

#### Query/performance impact

- Management/read-write complete-aggregate root SQL gains two reference joins/four projected columns.
- No identity in the frozen 33-command public/comparable matrix is changed here.

#### Authorization/concurrency impact

- Authorization is unchanged.
- Parent lock still precedes tracked aggregate load; no additional locks.

#### Tests required

- authoring mapper present/null behavior;
- management GET returns four subtype slots and all translations;
- read-only versus locked aggregate both include new children;
- no N+1 query;
- existing personal/agency concealment and Draft nullable location behavior remain;
- malformed direct detail combinations are represented truthfully rather than filtered.

#### Verification commands

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "Name~Chapter14ManagementReadContract" --logger "console;verbosity=minimal"
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~ListingAuthoringRepositoryTests" --logger "console;verbosity=minimal"

dotnet build -c Release --no-restore
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"
```

#### Evidence requirements

- Create `docs/planning/chapter-14c-management-read-contract-implementation.md`.
- Follow section 18.2; additionally record observed management JSON null/value semantics and the authoring query/load count.

#### Expected commit shape

- 1 commit for one management aggregate/read-contract concern.

#### Completion criteria

- Management read and locked aggregate load both include/map the two new children without changing auth, translations, or location behavior; focused and full tests pass.

#### Review/audit focus

- Accidentally touching shared public queries, missing the locked path, weakening management nullability, DTO reuse/collapse, authorization drift, or extra round trips.

### 14D — Extend Shared Listing Read Contracts

#### Purpose

- Expose Commercial/Land classification through public and private listing responses and every shared ListingRepository materialization path.

#### Why this task exists

- The taxonomy is incomplete if public clients cannot distinguish its subtypes. `ListingRepository.ApplyListingIncludes` is shared across public and private reads, so response mapping and materialization are one coordinated concern.

#### Depends on

- 14C `FINAL_PASS`.

#### Exact scope

- Add nullable `commercialDetails` and `landDetails` to `ListingResponse` and `PublicListingResponse` using the response DTOs from 14C.
- Extend `ListingMappingExtensions` for direct private/public navigation mapping.
- Add both reference Includes to all `ListingRepository` root materializations that return a complete Listing DTO, including list/by-id, my-listings/dashboard/lifecycle paths, and comparable ranked roots; do not widen scalar/existence/image probes that do not return this contract.
- Preserve split translation/image loading and every current selector/predicate/order.
- Add representative public/private/lifecycle/agency response tests for present and explicit-null behavior.

#### Explicit non-goals

- No Create/Update request support or root enum activation.
- No subtype-shape public eligibility rule.
- No predicate, filter, `q`, paging, translation selection, or comparable ranking change.
- No SQL acceptance/rebaseline; 14K owns final proof.

#### Expected production layers/files

- Domain: none.
- Application: `ListingResponse`, `PublicListingResponse`, `ListingMappingExtensions`.
- Infrastructure: `ListingRepository` Includes only.
- Api: endpoint signatures unchanged.
- Tests: mapping, listing persistence/API, agency public/dashboard, lifecycle, and comparable response shape.
- Docs: ignored task evidence only.

#### Domain impact

- none.

#### Application impact

- Public/private DTOs faithfully include nullable subtype details while retaining separate strictness.

#### Persistence/migration impact

- none after 14B.

#### API/OpenAPI impact

- Public and private response schemas gain optional/nullable detail objects; final schema assertions close in 14J.
- Existing required public localized/location fields remain required.

#### Query/performance impact

- Intentionally changes the eight frozen root identities in section 14 by two LEFT JOINs and four projections.
- Exactly 25 frozen count/source/child identities are expected unchanged.
- Shared private roots outside the frozen matrix also widen; no extra command/N+1 is allowed.

#### Authorization/concurrency impact

- no policy or lock change.

#### Tests required

- mapping of present and absent details across both response families;
- public list/detail/agency and private my/dashboard/lifecycle paths;
- publish/unpublish/archive responses retain new detail when applicable;
- explicit JSON null behavior;
- Active-only and strict public translation/location regressions;
- query-command counts remain bounded and translation/image split hydration unchanged;
- preliminary SQL diff capture identifies only expected root classes, with final acceptance deferred.

#### Verification commands

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "Name~Chapter14SharedReadContract" --logger "console;verbosity=minimal"
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~ListingMappingExtensionsTests" --logger "console;verbosity=minimal"

dotnet build -c Release --no-restore
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"
```

#### Evidence requirements

- Create `docs/planning/chapter-14d-shared-listing-read-contracts-implementation.md`.
- Follow section 18.2; additionally inventory every ListingRepository materialization path and record preliminary changed/unchanged SQL identity classes without accepting a baseline.

#### Expected commit shape

- 1 commit for the shared read representation and its required eager materialization.

#### Completion criteria

- All applicable read surfaces map/load both nullable details with no DTO collapse, N+1, predicate/order change, or Chapter 13 public-integrity regression.

#### Review/audit focus

- Missed read path, weakened public strictness, null omission, inconsistent private/public mapping, added query commands, selector/predicate drift, and premature SQL acceptance.

### 14E — Activate Commercial and Land Creation

#### Purpose

- Append the two root enum values and enable safe POST creation for their exact matching detail shapes.

#### Why this task exists

- The dormant model becomes useful only when supported authoring can create it. POST is independently reviewable, while 14A ensures PUT fails safely until the immediately following replacement task.

#### Depends on

- 14D `FINAL_PASS`.

#### Exact scope

- Append `PropertyType.Commercial = 3` and `PropertyType.Land = 4` without changing existing values.
- Add Create commercial/land detail request classes and nullable request properties.
- Extend Create validation to the exact four-way matrix, defined new subtype values, stable field keys, and explicit unsupported default.
- Extend Create handler to construct exactly one matching child in an exhaustive four-way switch.
- Prove Draft creation, persistence, and `ListingResponse` for both new types under personal and representative agency ownership.
- Minimally update the existing exact OpenAPI PropertyType enum assertion to four names so the mandated full suite remains green; comprehensive schema closure remains 14J.
- Prove a valid confirmed-location Commercial/Unknown and Land/Unknown Draft can publish and public-read with Unknown emitted, while all Chapter 13 translation/location readiness requirements remain.
- Prove Update returns `validation.failed` for Commercial/Land at this temporary midpoint.

#### Explicit non-goals

- No PUT support; 14F owns it.
- No new Active readiness, subtype filter, comparable logic, or extra attribute.
- No deployment/release at the task boundary.
- No Domain mutator abstraction.

#### Expected production layers/files

- Domain: `PropertyType.cs` only.
- Application: Create request/detail types, validator, handler.
- Infrastructure: none after 14B.
- Api: no controller logic change expected.
- Tests: enum stability, Create validator/handler/API/persistence and midpoint PUT rejection.
- Docs: ignored task evidence only.

#### Domain impact

- Root enum appends exact values 3 and 4. All other Domain behavior is unchanged.

#### Application impact

- POST supports all four matching-child shapes.
- Nonmatching child payloads fail on `request`; missing target and undefined subtype use their exact field keys.
- 14A's Update supported-type switch continues to reject new roots until 14F.

#### Persistence/migration impact

- uses the 14B schema; no new migration.
- Existing `Listings.PropertyType` string column writes `Commercial`/`Land`.

#### API/OpenAPI impact

- POST accepts/returns new types.
- `PropertyType` schemas now advertise four names; Create request exposes two new nullable children.
- OpenAPI advertises the four root names, but is knowingly incomplete for PUT until 14F and is not released at this midpoint.

#### Query/performance impact

- no LINQ/query text change; the deterministic profile still contains only Apartment/House.

#### Authorization/concurrency impact

- existing personal and Owner/Agent agency creation policy is reused; no new race boundary.

#### Tests required

- exact enum ordinals and symbolic JSON;
- Commercial Unknown/Office/Shop/Other creation;
- Land Unknown/BuildingPlot/AgriculturalLand/Other creation;
- missing target, each forbidden child, multiple children, undefined subtype/root/common enum;
- omitted inner subtype defaults Unknown;
- personal and representative agency authorization/disabled-state parity;
- Draft status, auditing, name-string persistence, response detail/null slots;
- parameterized Commercial/Unknown and Land/Unknown publish/public-read using valid Chapter 13 translations and a confirmed trusted location;
- minimal existing OpenAPI PropertyType enum assertion updated to the exact four names;
- explicit midpoint PUT rejection without mutation.

#### Verification commands

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~CreateListingValidatorTests|FullyQualifiedName~CreateListing|FullyQualifiedName~ListingPersistenceTests" --logger "console;verbosity=minimal"

dotnet build -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/RealEstate.Infrastructure/RealEstate.Infrastructure.csproj --startup-project src/RealEstate.Api/RealEstate.Api.csproj --configuration Release --no-build
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"
```

#### Evidence requirements

- Create `docs/planning/chapter-14e-commercial-land-create-implementation.md`.
- Follow section 18.2; additionally record all four POST payload shapes, persisted enum names, authorization cases, no-pending-model result, and the safe PUT rejection at the non-releaseable midpoint.

#### Expected commit shape

- 1 commit for the POST command path. It is paired operationally, not combined technically, with 14F.

#### Completion criteria

- Existing and new root types create exactly one correct child; all contradictory/undefined inputs fail canonically; PUT cannot corrupt the new types; no release occurs before 14F.

#### Review/audit focus

- renumbering, silently ignored child objects, default/serialization mismatch, missing agency parity, premature PUT acceptance, detail-less persistence, or accidental publication changes.

### 14F — Activate Four-Type Draft Replacement

#### Purpose

- Complete PUT full replacement across all four root types with exhaustive, atomic subtype reconciliation.

#### Why this task exists

- Chapter 14 cannot be released while Commercial/Land Drafts are uneditable. The existing engine must preserve tracked shared-PK behavior and remove all three incompatible children for every target.

#### Depends on

- 14E `FINAL_PASS`. 14E and 14F remain one non-deployable coordinated release unit.

#### Exact scope

- Add Update commercial/land detail request classes and nullable request properties.
- Extend Update validation to the exact four-way required/exclusive matrix and new defined subtype checks.
- Extend `ListingDraftReplacementEngine` to four explicit target branches and an unsupported default.
- Replace the temporary 14E Commercial/Land PUT-rejection assertion with successful four-type PUT coverage, and minimally update existing exact OpenAPI writable-member/schema-presence expectations so this task's full suite is truthful; comprehensive descriptions/requiredness remain 14J.
- For each branch, mutate an existing tracked target child in place or create it if absent, and null every other detail navigation.
- Prove same-type replacement, all 12 cross-type transitions, malformed-Draft repair, root/common clearing, translations, location behavior, rollback, management response, and representative authorization.

#### Explicit non-goals

- No patch semantics, Active editing, Domain mutator framework, child-specific repository, or database discriminator trigger.
- No broad concurrency matrix; 14G owns adversarial race proof.
- No discovery/comparable/OpenAPI closure beyond schemas naturally generated by request types.

#### Expected production layers/files

- Domain: none.
- Application: Update request/detail types, `UpdateListingValidator`, `ListingDraftReplacementEngine`.
- Infrastructure: no expected change; 14C already loads tracked children.
- Api: controller logic unchanged.
- Tests: Update validator, replacement engine, authoring repository/persistence, management and endpoint cases.
- Docs: ignored task evidence only.

#### Domain impact

- none; supported-authoring shape remains an Application invariant.

#### Application impact

- PUT accepts all four types only with one matching child.
- All root/common fields and translations retain full-replacement semantics.
- Same-type child identity remains tracked; cross-type stale dependents are removed.
- Classification-only changes preserve trusted location; existing location-text changes clear it.

#### Persistence/migration impact

- no schema change.
- One SaveChanges atomically updates root, target child, and orphan deletions inside the parent-locked transaction.

#### API/OpenAPI impact

- PUT request adds nullable new child objects and runtime four-way validation.
- Stable success/failure codes and management response shape remain.
- Final description/requiredness audit occurs in 14J.

#### Query/performance impact

- authoring write graph changes; frozen public discovery/comparable SQL is unchanged from 14D.

#### Authorization/concurrency impact

- preserves parent lock → post-lock auth/state → validation/mutation order.
- Representative personal/agency authorization and rollback cases are required; full adversarial races are 14G.

#### Tests required

- Update validator four-way matrix, Unknown, undefined enums, exact keys/precedence;
- data-driven 4 × 4 transition matrix at the replacement/persistence layer;
- same-type tracked update without duplicate-key/tracking conflict;
- missing-target creation and deletion of all stale incompatible rows;
- optional root null clearing and Unknown resets;
- translations retain server IDs by language;
- classification-only location preservation and canonical-location-change invalidation;
- failed validation/auth/save leaves original graph intact;
- management GET → PUT → GET for Commercial/Land;
- removal/replacement of 14E's temporary PUT-rejection test and minimal generated-schema member presence for the two new Update children;
- representative personal, Owner, and Agent behavior; non-Draft conflict unchanged.

#### Verification commands

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~UpdateListingValidatorTests|FullyQualifiedName~ListingDraftReplacementEngineTests|FullyQualifiedName~ListingAuthoringRepositoryTests|FullyQualifiedName~UpdateListing" --logger "console;verbosity=minimal"

dotnet build -c Release --no-restore
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"
```

#### Evidence requirements

- Create `docs/planning/chapter-14f-four-type-draft-replacement-implementation.md`.
- Follow section 18.2; additionally include a 16-cell transition result table, before/after relational rows for representative same/cross cases, exact location/rollback proof, and the intentional replacement of 14E's temporary rejection assertion.

#### Expected commit shape

- 1 commit for the single PUT full-replacement concern. The validator, engine, and tests must land together; splitting them would create an accepted unsafe command path.

#### Completion criteria

- Every successful PUT ends with exactly one target-matching child and no stale rows; all existing replacement/location/auth behavior remains; 14E/14F together form a complete authoring contract.

#### Review/audit focus

- any un-cleared navigation, replacement of a tracked shared-PK instance, mutation before validation, lock/auth ordering drift, partial saves, location over-clearing, and test duplication that misses the actual 4 × 4 persistence result.

### 14G — Prove Subtype Replacement Concurrency

#### Purpose

- Prove that the existing Listing parent serialization keeps new subtype delete/create transitions coherent under races.

#### Why this task exists

- Correct sequential replacement does not by itself prove PUT/PUT or PUT/lifecycle interleavings cannot leave a root/detail mismatch. Existing lock infrastructure should be sufficient, but the new branch points need focused proof.

#### Depends on

- 14F `FINAL_PASS`.

#### Exact scope

- Add deterministic PostgreSQL race tests for two PUTs targeting different root types.
- Add representative PUT versus publish and PUT versus location mutation cases involving Commercial/Land.
- Assert final root type, exactly one matching row across all four detail tables, no stale/orphan row, expected winner/loser response, and persisted location/lifecycle truth.
- Reuse current lock hooks/transactions and drain all tasks.
- If a defect is found, constrain any production fix to the existing Listing parent write scope and re-audit it.

#### Explicit non-goals

- No row version, ETag, child lock, serializable isolation, retry framework, sleep-based race, or global concurrency work.
- No repetition of the full authorization taxonomy matrix.

#### Expected production layers/files

- Domain: none.
- Application: none expected.
- Infrastructure: none expected; bounded existing write-scope correction only if evidence requires.
- Api: none expected.
- Tests: `ListingUpdateConcurrencyTests`, relevant publication/location concurrency families, and focused helpers.
- Docs: ignored task evidence only.

#### Domain impact

- none.

#### Application impact

- none expected.

#### Persistence/migration impact

- no schema change; tests inspect all four tables after interleavings.

#### API/OpenAPI impact

- none.

#### Query/performance impact

- no discovery/comparable SQL impact; test-only lock/write SQL is not rebaselined.

#### Authorization/concurrency impact

- Verifies parent-lock serialization, post-wait authorization/state decisions, atomic subtype reconciliation, and deterministic final state.

#### Tests required

- PUT/PUT with opposing Apartment↔Commercial or House↔Land targets;
- a case where same root but different subtype values race;
- PUT/publish proving authorization-before-readiness and coherent winner/loser state;
- PUT/location mutation proving no classification-based location clearing;
- task draining on both success and orchestration failure;
- direct catalog/data assertion that only the target table contains the Listing ID.

#### Verification commands

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "Name~Chapter14SubtypeRace" --logger "console;verbosity=minimal"
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~ListingUpdateConcurrencyTests|FullyQualifiedName~PostgreSqlActiveListingPublicationConcurrencyTests|FullyQualifiedName~ListingGeocodingConcurrencyTests" --logger "console;verbosity=minimal"

dotnet build -c Release --no-restore
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"
```

#### Evidence requirements

- Create `docs/planning/chapter-14g-subtype-replacement-concurrency-implementation.md`.
- Follow section 18.2; additionally record lock orchestration, observed response ordering, final four-table state, and proof every request task was drained.

#### Expected commit shape

- 1 commit, normally tests only. A bounded existing-write-scope correction may share the commit only when required by the proven race and remains the same primary concern.

#### Completion criteria

- Deterministic races finish without deadlock/flakiness and always produce one coherent root/detail state under the existing parent lock.

#### Review/audit focus

- fake concurrency, sleeps, pre-lock authorization/state decisions, swallowed loser failures, undrained tasks, and assertions that inspect only the root but not all dependent tables.

### 14H — Verify Root-Type Discovery

#### Purpose

- Prove the existing exact `propertyType` filter discovers Commercial and Land without new discovery architecture.

#### Why this task exists

- Enum expansion naturally extends the predicate, but counts, page membership, Active eligibility, effective translations, and agency reuse must remain correct; 14A separately hardens undefined enum input.

#### Depends on

- 14G `FINAL_PASS`.

#### Exact scope

- Add Commercial and Land fixtures/results to public discovery tests.
- Prove exact root filtering, null/no-filter behavior, totalCount/totalPages/page contents, deterministic ordering, and Active-only visibility.
- Prove unfiltered agency-public reuse returns the new types through the same shared path.
- Re-prove effective-language filtering and the locked `q` field set around new-type results.
- Confirm there is no `commercialType` or `landType` query parameter or predicate.

#### Explicit non-goals

- No subtype filter, multi-value filter, index, `q` expansion, agency-only PropertyType filter, or new paging mode.
- No QueryReview acceptance; 14K owns final SQL proof.

#### Expected production layers/files

- Domain: none.
- Application: no production change expected; `GetListingsValidator` was completed in 14A.
- Infrastructure: no predicate change expected.
- Api: no controller signature change.
- Tests: listing filter/search/pagination/public visibility and agency listing endpoint families.
- Docs: ignored task evidence only.

#### Domain impact

- none.

#### Application impact

- none expected.

#### Persistence/migration impact

- none.

#### API/OpenAPI impact

- Runtime `propertyType=Commercial` and `propertyType=Land` are supported.
- Final generated parameter proof belongs to 14J.

#### Query/performance impact

- Existing equality predicate, count-before-page, ordering, and split loading remain. Valid-filter SQL semantics must not change.

#### Authorization/concurrency impact

- none; public visibility and agency scoping remain current.

#### Tests required

- each new root exact filter and exclusion of the other three roots;
- null filter and defined numeric/name forms;
- undefined numeric 400 regression from 14A;
- Active versus Draft/Archived visibility;
- totalCount/page membership/order;
- language fallback/location filter parity;
- `q` positive cases only for Title/City/Municipality/Neighborhood and negative cases for Description/AddressLine/subtype label;
- agency-public shared-path new-type response.

#### Verification commands

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "Name~Chapter14RootTypeDiscovery" --logger "console;verbosity=minimal"
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~GetListingsValidatorTests" --logger "console;verbosity=minimal"

dotnet build -c Release --no-restore
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"
```

#### Evidence requirements

- Create `docs/planning/chapter-14h-root-type-discovery-implementation.md`.
- Follow section 18.2; additionally record exact query strings, expected/result IDs and counts, q negative cases, and confirmation that no production predicate/index changed.

#### Expected commit shape

- 1 commit, expected to be focused test coverage unless a bounded defect is found.

#### Completion criteria

- Commercial/Land are discoverable through the existing root filter with unchanged paging, language, q, agency, and Active semantics; no subtype filter exists.

#### Review/audit focus

- filter-after-page behavior, q broadening, hidden agency fork, accidental subtype predicates/indexes, and tests that pass without distinguishing all four roots.

### 14I — Preserve Comparable Eligibility and Ranking

#### Purpose

- Prove Commercial and Land use existing root-type comparable isolation while subtype values remain valuation-neutral.

#### Why this task exists

- The new roots must not cross-compare, but the repository provides no basis for subtype eligibility or ranking. Tests must lock that minimal product behavior.

#### Depends on

- 14H `FINAL_PASS`.

#### Exact scope

- Add comparable source/candidate scenarios for Commercial and Land.
- Prove exact root `PropertyType` equality excludes every other root.
- Prove same-root subtype differences affect neither eligibility nor ranking.
- Preserve all source prerequisites, effective-language/City behavior, six ranking keys, limit, deterministic ties, and response mapping.
- Expect no production query change beyond the read Includes already introduced in 14D.

#### Explicit non-goals

- No subtype eligibility/ranking, valuation heuristics, distance behavior, filter, index, or benchmark tuning.

#### Expected production layers/files

- Domain: none.
- Application: no production change expected.
- Infrastructure: no predicate/ranking change expected.
- Api: none.
- Tests: comparable handler and endpoint source/eligibility/ranking/contract families.
- Docs: ignored task evidence only.

#### Domain impact

- none.

#### Application impact

- none expected.

#### Persistence/migration impact

- none.

#### API/OpenAPI impact

- Comparable `PublicListingResponse` already includes new details from 14D; no endpoint signature change.

#### Query/performance impact

- `C1-01` source semantics and `C1-02` eligibility/ranking are preserved. Subtype values do not enter SQL predicates/order. The two detail joins are the already-planned 14D root delta.

#### Authorization/concurrency impact

- none.

#### Tests required

- Commercial source includes only Commercial candidates;
- Land source includes only Land candidates;
- Office versus Shop and BuildingPlot versus AgriculturalLand do not affect eligibility/order;
- Apartment/House regressions;
- all six ranking keys and UUID tie-break remain exact;
- comparable limit/result IDs and new detail response mapping;
- source unavailable/not Active/invalid price-area behavior unchanged.

#### Verification commands

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "Name~Chapter14Comparable" --logger "console;verbosity=minimal"
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~GetComparableListingsHandlerTests" --logger "console;verbosity=minimal"

dotnet build -c Release --no-restore
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"
```

#### Evidence requirements

- Create `docs/planning/chapter-14i-comparable-root-isolation-implementation.md`.
- Follow section 18.2; additionally record source/candidate taxonomy, exact ordered IDs, and SQL inspection proving no subtype predicate/order.

#### Expected commit shape

- 1 focused test/evidence commit unless a bounded regression correction is necessary.

#### Completion criteria

- Both new roots compare only within their root class; subtype is neutral; the locked eligibility/ranking/result contract passes unchanged.

#### Review/audit focus

- test data that cannot detect cross-root leakage, accidental subtype valuation, ordering/tie drift, and changes hidden behind response-only assertions.

### 14J — Close the Generated OpenAPI Contract

#### Purpose

- Make generated OpenAPI exactly describe the settled additive taxonomy and runtime conditional authoring contract.

#### Why this task exists

- C# nullability can describe optional nested objects but not the four-way matching condition. Clients need truthful schemas/descriptions without a disproportionate polymorphic OpenAPI framework.

#### Depends on

- 14I `FINAL_PASS`; all request, response, filter, and runtime behavior is then stable.

#### Exact scope

- Extend `ApiOpenApiSchemaFilter` only where needed for four-way Create/Update conditional descriptions and existing default/replacement prose.
- Mark and assert `commercialType`/`landType` required and non-null in response-detail schemas when their parent object exists; keep request subtype members outside required arrays and describe Create omission as defaulting to Unknown and PUT omission as resetting to Unknown.
- Update `OpenApiDocumentTests` for exact enums, schema references, required arrays, nullability, defaults, response strictness, query parameter values, and stable failures.
- Assert deferred subtype query parameters are absent.
- Preserve controller signatures and generated-document ownership.

#### Explicit non-goals

- Preserve the current nullable-reference `oneOf`; add no polymorphic/discriminator four-variant `oneOf`, separate OpenAPI document, frontend client generation, runtime validation change, or error-code addition.

#### Expected production layers/files

- Domain: none.
- Application: no behavior change; contract types already exist.
- Infrastructure: none.
- Api: `ApiOpenApiSchemaFilter`; operation filter only if a concrete existing description hook requires it.
- Tests: `OpenApiDocumentTests`.
- Docs: ignored task evidence only.

#### Domain impact

- none.

#### Application impact

- none.

#### Persistence/migration impact

- none.

#### API/OpenAPI impact

- Exact final schemas described in section 13.
- Parent detail objects are optional/nullable. Response inner enums are required/non-null. Request inner enums stay optional/non-null and use exact omission-to-Unknown descriptions; Chapter 14 does not add an OpenAPI `default` keyword for subtype enums.
- Runtime conditionality is prose plus validation, not a false unconditional required array.

#### Query/performance impact

- none.

#### Authorization/concurrency impact

- none.

#### Tests required

- exact PropertyType/CommercialType/LandType symbolic members and no renumbering leakage;
- Create/Update new schemas and four-way descriptions;
- nullable reference wrappers and unchanged unconditional required arrays;
- the existing EUR default, no new subtype-enum `default` keyword, and exact Create-default/PUT-reset descriptions;
- required/non-null response-detail subtype members and optional request-detail subtype members;
- all three response schemas and unchanged public required fields;
- expanded `propertyType` parameter, absent subtype parameters;
- POST/PUT/management/public/comparable existing success/error response components;
- numeric ingress is not falsely described as the only or preferred representation.

#### Verification commands

```text
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --filter "FullyQualifiedName~OpenApiDocumentTests" --logger "console;verbosity=minimal"

dotnet build -c Release --no-restore
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"
```

#### Evidence requirements

- Create `docs/planning/chapter-14j-openapi-taxonomy-contract-implementation.md`.
- Follow section 18.2; additionally record the generated schema excerpts/required arrays/defaults and an explicit runtime-versus-schema conditionality statement.

#### Expected commit shape

- 1 contract-focused commit.

#### Completion criteria

- Generated OpenAPI and runtime semantics agree as far as the schema language can express; conditional limits are explicit; all affected schema/failure tests and the full suite pass.

#### Review/audit focus

- false unconditional requiredness, nullable reference loss, accidental public-field weakening, wrong enum names/defaults, missing endpoint schema, and needless polymorphic customization.

### 14K — Accept the Chapter 14 SQL/Plan Delta

#### Purpose

- Produce a bounded, versioned proof that only the expected root materialization SQL changed and its plans remain acceptable.

#### Why this task exists

- Chapter 13 froze exact SQL. Chapter 14 intentionally adds two joins to public/comparable roots; that change needs explicit acceptance without overwriting the historical baseline or absorbing broad performance work.

#### Depends on

- 14J `FINAL_PASS`; no query-affecting production change may follow 14K without invalidating and rerunning this task.

#### Exact scope

- Use a fresh explicitly disposable PostgreSQL 16 database with all 21 migrations.
- Run existing QueryReview profile create/verify with the unchanged 61-invariant Apartment/House profile.
- Independently assert `ListingCommercialDetails` and `ListingLandDetails` each contain zero rows and have zero root-type mismatch rows before capture; the unchanged 61-invariant verifier does not inspect them.
- Capture all 33 production commands and all 80 typed parameter records.
- Compare normalized command bodies directly with `docs/benchmarks/chapter-10f/evidence/sql/*.sql`, using Chapter 13K.3 hashes/manifests as trust anchors; all 80 parameter records remain exact even for changed root commands.
- Prove 25 exact matches and review the eight expected root deltas for only two LEFT JOINs, four projections, and unavoidable aliases.
- Run all 198 warm-up/measured EXPLAIN executions and current result/order/command/Q1/no-spill gates.
- Inspect new table PK/catalog evidence and plan behavior without demanding index use for empty dependent tables.
- Create `docs/benchmarks/chapter-10f/chapter-14-property-taxonomy-generated-sql-delta-proof.md` with commands, identities, hashes/diffs, measured outcomes, environment, and verdict.
- Keep raw captures/plans in disposable temp or ignored evidence.

#### Explicit non-goals

- No production, test, migration, QueryReview source, profile distribution, invariant-total, exporter, or accepted Chapter 10 baseline change.
- Do not run `baseline export`.
- No Commercial/Land bulk seeding, subtype index, tuning, or `CH13-PERF-01` work.
- No mechanical acceptance if identity counts differ.

#### Expected production layers/files

- Domain/Application/Infrastructure/Api: none.
- Tooling: no source change expected.
- Tests: no test-code change expected.
- Docs: one tracked Chapter 14 SQL-delta proof plus ignored task evidence.
- Raw artifacts: temporary/ignored only.

#### Domain impact

- none.

#### Application impact

- none.

#### Persistence/migration impact

- verifies the complete 21-migration chain and new PK/FK catalog; changes nothing.

#### API/OpenAPI impact

- none.

#### Query/performance impact

- Accepts exactly the policy in section 14 or stops on discrepancy.
- Preserves 33 commands, locked result identities/order, 61 profile invariants, 198 plan executions, zero spills/temp anomalies, and existing Q1 thresholds.
- Records measured impact of wider roots without declaring a new broad baseline.

#### Authorization/concurrency impact

- none.

#### Tests required

- QueryReview existing self-validation/build;
- profile create and independent verify;
- independent zero-row/zero-mismatch checks for both new dependent tables;
- exact 33-command capture/classification;
- exact 80 typed parameter records and result/order parity;
- 198 plan validation and measurement;
- proof artifact/link/hash validation;
- accepted Chapter 10F 69-file directory byte/status unchanged.

#### Verification commands

Use an owner-approved, explicitly disposable local target and record the real redacted target identity:

```text
dotnet restore
dotnet restore tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj
dotnet build -c Release --no-restore
dotnet build tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-restore

dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile create --connection-string "<approved-disposable-connection>" --confirm-disposable --container-name <chapter-14-container>

dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- profile verify --connection-string "<approved-disposable-connection>" --confirm-disposable

docker exec <chapter-14-container> psql --username <redacted-user> --dbname <redacted-database> -v ON_ERROR_STOP=1 -c 'SELECT (SELECT count(*) FROM "ListingCommercialDetails") AS commercial_rows, (SELECT count(*) FROM "ListingLandDetails") AS land_rows, (SELECT count(*) FROM "ListingCommercialDetails" d JOIN "Listings" l ON l."Id" = d."ListingId" WHERE l."PropertyType" <> ''Commercial'') AS commercial_mismatches, (SELECT count(*) FROM "ListingLandDetails" d JOIN "Listings" l ON l."Id" = d."ListingId" WHERE l."PropertyType" <> ''Land'') AS land_mismatches;'

dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- capture-sql --connection-string "<approved-disposable-connection>" --confirm-disposable

dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline run --connection-string "<approved-disposable-connection>" --confirm-disposable --container-name <chapter-14-container>

dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -c Release --no-build -- baseline verify --run-directory "<temporary-run-directory>"

dotnet ef migrations has-pending-model-changes --project src/RealEstate.Infrastructure/RealEstate.Infrastructure.csproj --startup-project src/RealEstate.Api/RealEstate.Api.csproj --configuration Release --no-build

dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"

docker stop <chapter-14-container>
```

`baseline run` above creates temporary measured output; it does not authorize `baseline export` or modification of accepted artifacts.
Container cleanup is mandatory in a `finally` path even when an earlier verification step fails; the exact target must be created with auto-remove semantics.

#### Evidence requirements

- Create `docs/planning/chapter-14k-generated-sql-plan-delta-implementation.md`.
- Follow section 18.2; additionally include target/container safety checks, redacted commands, new-table zero-row/mismatch results, all 33 comparisons, all 80 parameter records, explicit eight diffs, all 198 plan results, measurements, hashes of query-affecting source files, accepted-baseline before/after status, and container cleanup.

#### Expected commit shape

- 1 evidence-only commit containing the new tracked Chapter 14 delta proof. Raw tool output and ignored planning evidence are not committed.

#### Completion criteria

- Both new tables are independently empty in the unchanged profile; 25 command bodies and all 80 parameter records are exact; eight bodies contain only explained deltas; result/order/plan/no-spill/Q1 gates pass; no historical baseline/tool/source file changes; the disposable environment is removed.

#### Review/audit focus

- hidden extra SQL changes, incorrect two-versus-four projection claim, normalization beyond line endings, stale or fabricated hashes, baseline overwrite, profile drift, plan regression, credentials, and residual containers.

### 14L — Run the Cumulative Chapter 14 Verification Gate

#### Purpose

- Verify the complete assembled Chapter 14 tree and create a durable technical closure record without adding behavior.

#### Why this task exists

- Slice-level tests prove individual concerns; a bounded cumulative gate proves the final composition, migration model, generated contract, Chapter 13 regressions, and accepted SQL evidence from one immutable tree.

#### Depends on

- 14K `FINAL_PASS`.

#### Exact scope

- Freeze branch/status/HEAD and inventory all Chapter 14 commits/artifacts.
- Run a Release build and the complete test suite.
- Run focused accounting across taxonomy validation, migration/persistence, authoring/transitions, mapping/reads, authorization/concurrency, discovery, comparables, OpenAPI, and protected Chapter 13 integrity families.
- Verify 21 migrations, fresh/upgrade/Down/re-Up coverage, catalog assertions, and no pending model.
- Verify byte/hash equality of every query-affecting source file against the hashes recorded by 14K; do not compare the whole HEAD/tree because the 14K proof commit itself changes HEAD. Do not rerun all 198 plans when the source and immutable proof remain current.
- Create `docs/chapters/chapter-14l-cumulative-chapter-14-verification-gate.md` as a factual technical record. It must not declare chapter/product closeout; 14M owns status.

#### Explicit non-goals

- No production/test/schema/query correction hidden in the gate.
- No duplicate SQL rebaseline, frontend handoff, broad docs consolidation, or quality-issue closure.
- A defect returns to the owning task/correction/re-audit workflow.

#### Expected production layers/files

- Domain/Application/Infrastructure/Api: none.
- Tests/migrations/QueryReview: none.
- Docs: one durable cumulative verification record plus ignored task evidence.

#### Domain impact

- none.

#### Application impact

- none.

#### Persistence/migration impact

- verification only.

#### API/OpenAPI impact

- verifies final generated contract only.

#### Query/performance impact

- verifies 14K artifacts plus byte/hash identity of its recorded query-affecting source files; reruns 14K only if one of those sources changed, in which case 14K must receive a fresh audit before 14L continues.

#### Authorization/concurrency impact

- verifies focused final regression families; no new policy.

#### Tests required

- complete suite with exact pass/fail/skip count;
- focused groups with overlap accounting where counts are reported cumulatively;
- migration inventory/model checks;
- OpenAPI document tests;
- representative personal/agency authoring and deterministic races;
- Chapter 13 translation/location/readiness/public fail-closed regressions;
- artifact/hash/diff freeze.

#### Verification commands

```text
dotnet restore
dotnet build -c Release --no-restore

dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"

dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~CreateListingValidatorTests|FullyQualifiedName~UpdateListingValidatorTests|FullyQualifiedName~ListingDraftReplacementEngineTests|FullyQualifiedName~ListingAuthoringRepositoryTests|FullyQualifiedName~ListingPersistenceTests|FullyQualifiedName~ListingUpdateConcurrencyTests|FullyQualifiedName~GetListingsValidatorTests|FullyQualifiedName~GetComparableListings|FullyQualifiedName~OpenApiDocumentTests|FullyQualifiedName~PostgreSqlCommercialAndLandTaxonomyMigrationTests" --logger "console;verbosity=minimal"

dotnet ef migrations list --project src/RealEstate.Infrastructure/RealEstate.Infrastructure.csproj --startup-project src/RealEstate.Api/RealEstate.Api.csproj --configuration Release --no-build --no-connect

dotnet ef migrations has-pending-model-changes --project src/RealEstate.Infrastructure/RealEstate.Infrastructure.csproj --startup-project src/RealEstate.Api/RealEstate.Api.csproj --configuration Release --no-build
```

The exact focused filters may be refined to the implemented test names, but the durable record must print the final literal commands and explain overlaps.

#### Evidence requirements

- Create `docs/planning/chapter-14l-cumulative-verification-implementation.md`.
- Follow section 18.2; additionally record exact test accounting, migration IDs, no-pending result, 14K artifact hashes and query-affecting source-file hash equality, and a zero-production-change diff.

#### Expected commit shape

- 1 evidence-only commit containing the cumulative verification record.

#### Completion criteria

- Release build and all tests pass with zero failure; model/migration/OpenAPI/concurrency/Chapter 13 regressions are proven; 14K remains current; the task changes no implementation.

#### Review/audit focus

- stale binaries, double-counted tests, a pending EF model, hidden correction, stale SQL proof, incomplete untracked diff, or wording that self-declares Chapter 14 complete.

### 14M — Close Chapter 14 for Chapter 15

#### Purpose

- Make durable chapter status and the Chapter 15 starting context truthful after technical `FINAL_PASS`.

#### Why this task exists

- Technical evidence and project status are separate. Chapter 14 needs a small closeout, but the final whole-backend frontend handoff and documentation consolidation occur only after Chapter 15.

#### Depends on

- 14L `FINAL_PASS`.

#### Exact scope

- Update this document from planned to complete, replace projections with actual task/migration/evidence references, and record the final accepted contract.
- Update `docs/backend-context.md` minimally with Chapter 14 current truth and Chapter 15 as next.
- Review every `docs/backend-quality-handoff.md` entry. Change it only for an evidence-backed new/resolved issue; otherwise record in closeout evidence that all ten entries remain.
- Verify links, task/commit references, migration count, test totals, OpenAPI result, and SQL proof against 14L.
- Perform final repository/diff freeze.

#### Explicit non-goals

- No source, test, migration, QueryReview, OpenAPI implementation, benchmark baseline, or frontend change.
- No edit to `docs/backend-frontend-handoff.md`.
- No final backend-to-frontend reconciliation, docs consolidation, Chapter 15 plan/implementation, or broad quality cleanup.

#### Expected production layers/files

- Domain/Application/Infrastructure/Api: none.
- Tests/migrations/tooling: none.
- Docs: this Chapter 14 document and `backend-context.md`; `backend-quality-handoff.md` only if actual issue evidence requires a truthful change.
- Ignored task evidence.

#### Domain impact

- none.

#### Application impact

- none.

#### Persistence/migration impact

- none; records actual migration identity/count.

#### API/OpenAPI impact

- no contract change; records the accepted generated contract.

#### Query/performance impact

- no query/artifact change; links the accepted 14K proof and leaves `CH13-PERF-01` untouched.

#### Authorization/concurrency impact

- none; records accepted proof.

#### Tests required

- no new tests;
- rerun mandated Release build/full suite on the unchanged technical tree;
- link/reference/status/diff verification.

#### Verification commands

```text
dotnet restore
dotnet build -c Release --no-restore
dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"

git status --short
git diff --name-status
git diff --stat
git diff --check
```

Also explicitly enumerate untracked files and compare the final intended documentation set to task scope.

#### Evidence requirements

- Create `docs/planning/chapter-14m-closeout-implementation.md`.
- Follow section 18.2; additionally record every durable link/value checked, the quality-register disposition table, and confirmation that the frontend handoff/consolidation were not changed.

#### Expected commit shape

- 1 documentation-only commit.

#### Completion criteria

- Chapter 14 status/current context match 14L evidence; Chapter 15 is the next boundary; no unsupported closeout claim or unrelated file change exists.

#### Review/audit focus

- stale planned language, incorrect totals/links, premature frontend readiness, false quality-item closure, Chapter 15 scope theft, and hidden implementation changes.

## 20. Chapter 14 completion gate

Chapter 14 is complete only when all tasks 14A–14M have fresh audit `FINAL_PASS` and owner commits, and all of these statements are true:

- Existing PropertyType ordinals remain stable and exact new root/subtype vocabularies are implemented.
- Two explicit one-to-one children exist; no generic taxonomy/EAV framework was introduced.
- POST and PUT reject contradictory/undefined input and guarantee one matching persisted child across four types.
- The 4 × 4 replacement matrix, same-type tracking, stale-row deletion, rollback, management round-trip, and deterministic races pass.
- Chapter 13 translation, trusted location, public strictness, authorization-before-readiness, lifecycle, and parent locking remain unchanged.
- Unknown remains Draft/Active-valid; publication readiness has no speculative new condition.
- `PublicListingResponse`, `ListingResponse`, and `ListingAuthoringResponse` remain separate and faithfully expose nullable new details.
- The single additive migration is number 21, has exact structural integrity, fabricates no data, and leaves no pending model.
- No J.2-style gate was invented and `CH13-J2-DEPLOY-01` remains separately owned.
- Existing `propertyType` discovery supports Commercial/Land; subtype filters and q expansion do not exist.
- Comparables isolate by root PropertyType and retain identical ranking; subtype is neutral.
- Generated OpenAPI proves exact vocabulary, nullability/requiredness/defaults/descriptions, public strictness, and unchanged failures.
- The SQL proof establishes 25 exact command bodies, eight explained root deltas, all 80 parameter records exact, 33 commands, 61 invariants, 198 acceptable plans, independently empty new tables, and no historical baseline rewrite.
- Release build, complete suite, migration lifecycle, concurrency, OpenAPI, and Chapter 13 regression gates pass on the final tree.
- All quality-handoff items retain truthful ownership.
- No frontend, historical migration/evidence, unrelated source/docs, or broad Chapter 15 work is included.

## 21. Deferred work

The following remain deliberate future decisions, not incomplete Chapter 14 tasks:

- CommercialType/LandType query parameters and their exact single/multi-value semantics;
- representative Commercial/Land profile distribution and measured index needs;
- subtype-aware comparable product rules;
- any new Commercial/Land attribute and field applicability matrix;
- taxonomy label/reference-data localization;
- new property types/subtypes;
- QueryReview exporter/versioned broad baseline redesign;
- `CH13-PERF-01`, `CH13-J2-DEPLOY-01`, `C12-CONFIG-01`, and unrelated quality work;
- Chapter 15 integration/hardening;
- final backend-to-frontend reconciliation, documentation consolidation, and frontend implementation.

## 22. Decision log and architecture rationale

| Decision | Final choice | Primary reason |
|---|---|---|
| hierarchy | Commercial/Land roots; named concepts as two subtypes | matches classification level and existing Apartment/House structure |
| taxonomy ownership | fixed Domain enums | no runtime/admin/reference-data requirement |
| storage | two explicit shared-PK children | smallest coherent extension; clear and type-safe |
| Domain mutation abstraction | none | current aggregate is mutable; a helper would be bypassable and broad encapsulation is out of scope |
| supported child consistency | validators + Create handler/replacement engine | matches current CQRS-lite authoring boundary |
| DB consistency | structural PK/FK/null/default/cascade only | cross-table discriminator triggers are disproportionate |
| Unknown | valid Draft and Active | existing subtype precedent; root remains truthful |
| common fields for Land | retain optional values; no reject/auto-clear | no evidence-backed applicability matrix; avoids destructive policy |
| public subtype exposure | include nullable nested objects now | otherwise public taxonomy loses meaning |
| new subtype filters | defer | broader discovery integration belongs to Chapter 15 |
| comparable subtype behavior | none | no valuation rule; preserve locked root eligibility/ranking |
| serializer | keep current converter/numeric compatibility | global behavior change is unrelated and breaking |
| migration | one additive two-table migration, no data gate | no existing row is invalidated |
| query acceptance | 8 reviewed / 25 exact, temporary profile output, one delta proof | protects freeze without rewriting historical baseline/tooling |
| implementation staging | fail closed → dormant foundation/reads → POST → PUT | prevents contradictory data and keeps commits reviewable |
| commit sizing | 13 tasks, 13 expected commits; none allowed two | each concern is independently reviewable and bounded |
| Chapter 15 boundary | subtype discovery/performance and broad hardening deferred | keeps Chapter 14 model-focused |

### Final self-audit

| Protected contract/risk | Final plan result |
|---|---|
| Chapter 13 location integrity | unchanged; classification alone never clears location |
| public strictness | required localized/location truth unchanged; subtype objects nullable |
| Draft management | all translations and four nullable subtype slots |
| effective translation | requested → mk → bytewise language → translation UUID |
| q | Title/City/Municipality/Neighborhood only |
| paging/count/order | unchanged |
| comparables | root equality and six-key ranking unchanged |
| agency public path | shared path unchanged |
| parent locking | Listing remains serialization boundary |
| publication auth/readiness | authorization precedence and readiness unchanged |
| migration compatibility | additive forward path; truthful old-binary rollback limitation |
| SQL freeze | only eight explicit roots accepted; 25 exact |
| Chapter 10/13 evidence | immutable |
| quality handoff | none silently absorbed |
| Chapter 15 | broader integration/performance/hardening retained |
| task sizing | every task one primary concern and one expected commit |
| owner decisions | none unresolved |
