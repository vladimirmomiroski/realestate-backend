# Chapter 13 — Public Listing Integrity and Authoring

## Chapter status

This document is the authoritative architecture and implementation plan for Chapter 13. It is a planning artifact, not implementation evidence. The chapter is not complete until Checkpoint 13H records actual verification results.

Primary evidence: `docs/planning/chapter-13-ultra-preparation.md`.

The filename follows the repository's established lowercase, hyphenated `chapter-NN-description.md` convention and names both responsibilities that must move together: trustworthy public publication state and the supported authoring path needed to reach it.

## 1. Chapter 13 Purpose

Chapter 13 turns `ListingStatus.Active` from principally a status value into a trustworthy publication state. A successful public response must never rely on nullable annotations, fabricated defaults, or a lucky language fallback. The backend must first make the required content true, prevent supported writes from violating it, and expose a Draft-capable edit path.

The chapter also adds the missing production-supported listing update workflow. Authors must be able to retrieve all editable content, complete or correct a Draft, and publish it through the same personal/agency authorization architecture that already exists.

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

## 3. Final Domain/Public Invariant

### 3.1 Supported listing structural invariant

Every listing created or replaced through supported authoring APIs has at least one translation. Every stored translation supplied through those APIs has:

- a canonical `LanguageCode` that satisfies the project language-tag grammar;
- a trimmed, nonblank `Title`;
- normalized optional text, where blank/whitespace optional input becomes `null`;
- no duplicate normalized language within one listing.

Existing core authoring rules remain in force: defined current `ListingType` and `PropertyType` enum values, positive price and area, a three-letter currency, coordinate pairing/ranges, nonnegative/count/year rules, and exactly the current matching Apartment or House detail shape. Chapter 13 shares those rules between create and update; omitted/default/undefined enum values are validation failures. It does not claim a new database-wide invariant for every historical numeric/property-detail rule.

### 3.2 Active publication invariant

A listing is publishable and may remain Active only when:

1. it has at least one translation; and
2. **every attached translation** has:
   - canonical, syntactically valid `LanguageCode`;
   - trimmed, nonblank `Title`;
   - trimmed, nonblank `City`;
   - trimmed, nonblank `Description`.

“Meaningful” in Chapter 13 means non-null and non-whitespace after normalization. No arbitrary word count or prose-quality heuristic is introduced.

Requiring every translation is deliberate. If only one fallback translation were valid, an incomplete exact-requested-language row would still win the established selector and invalidate the public promise. Every-row readiness makes all selectable translations safe while leaving fallback order and hot public SQL unchanged.

An Active listing does not have to contain `mk`. If requested and Macedonian translations are absent, the existing deterministic other-language fallback remains valid because every remaining row is publishable.

### 3.3 Status ownership

`Listing.Status` will no longer be publicly settable by ordinary domain/application code. Draft is the construction default; `Publish`, `Unpublish`, and `Archive` own supported transitions. EF Core may still materialize the private setter. Tests must use lifecycle methods or explicitly named database-integrity setup rather than treating direct assignment as normal domain behavior.

## 4. Draft and Authoring Model

Draft is intentionally useful but not publication-complete. The following is the **supported-authoring** contract enforced by POST/PUT, not a claim that PostgreSQL independently requires every Draft aggregate to retain a child:

- a supported create/replacement must retain at least one canonical translation with valid language and nonblank title;
- City and Description may be `null` in any Draft translation;
- AddressLine, Municipality, and Neighborhood remain optional;
- blank optional input is stored as `null`, not as a blank string;
- a Draft may change current core fields, ListingType, PropertyType, translations, and the matching Apartment/House detail;
- AgencyId, creator, status, images, IDs, and audit fields are not editable through the replacement operation.

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

The request contains the current editable core fields, the complete translation collection, and exactly one matching Apartment or House detail payload. It excludes status, AgencyId, CreatedByUserId, image mutations, translation IDs, detail IDs, and auditing fields.

Required JSON presence is explicit rather than inferred from CLR defaults. `listingType`, `propertyType`, `price`, `currency`, `areaSquareMeters`, and `translations` are required top-level members; every translation requires `languageCode` and `title`. `UpdateListingRequest` declares these as C# `required` members recognized by the configured System.Text.Json input formatter, so an omitted `currency` cannot silently become `EUR`; focused model-binding tests must prove this configured behavior. The matching `apartmentDetails` or `houseDetails` object is conditionally required by the validated PropertyType. Other nullable members are optional and omission clears them. Optional value enums such as heating/furnishing/condition/orientation and subtype kind reset to their documented `Unknown` default when omitted; every supplied enum numeric value must still be defined.

Translations are reconciled by normalized `LanguageCode`, not deleted/reinserted wholesale. Retained languages preserve their translation IDs; new languages receive new IDs; omitted languages are removed. This avoids unnecessary writes and WAL against the existing trigram index and preserves deterministic identities.

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
- City and Description are trimmed; blank Draft input becomes `null`; when present they must be nonblank.
- AddressLine, Municipality, and Neighborhood retain the same trim/blank-to-null behavior.
- Create and update validate all existing EF maximum lengths before persistence: language 10, title 200, description 3000, address 300, and city/municipality/neighborhood 100.

Parity tests must exercise at least space, tab, CR/LF, nonbreaking space (`U+00A0`), em space (`U+2003`), and ideographic space (`U+3000`) through both supported validation and direct PostgreSQL writes. Language normalization uses this same boundary trim before ASCII lowercase/grammar validation.

Validation and normalization are shared as narrow reusable rules, while `CreateListingValidator` and `UpdateListingValidator` remain explicit use-case validators. No validation framework is added.

## 7. Publish Readiness Architecture

Publication readiness is layered:

1. Application create/update validation prevents malformed supported authoring input.
2. Domain translation/publication rules provide one typed readiness evaluation.
3. `Listing.Publish()` evaluates readiness before changing status.
4. The publish handler loads the complete locked aggregate and maps typed readiness failure to the public error contract.
5. PostgreSQL checks row truth, validates activation, and freezes translations while Active.
6. Strict public mapping refuses impossible malformed Active materialized state.

### 7.1 Domain behavior

The Domain owns a typed publication-readiness result/violation model; handlers must not parse `InvalidOperationException.Message`.

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

Two migrations separate row truth from aggregate publication truth.

### 8.1 Row-level truth

The translation columns keep their current nullability and lengths. Named PostgreSQL checks use the explicit Section 6.2 character string in `btrim(value, characters)`—not default `btrim(value)`—and enforce:

- LanguageCode is trimmed, lowercase, nonblank, and matches the project grammar;
- Title is trimmed and nonblank;
- City is either `NULL` or trimmed/nonblank;
- Description is either `NULL` or trimmed/nonblank.

Title and LanguageCode remain NOT NULL. City and Description remain nullable because Draft may be incomplete. The unique `(ListingId, LanguageCode)` index remains unchanged and becomes semantically canonical because nonlowercase storage is rejected. The four-column trigram GIN index remains unchanged.

### 8.2 Cross-row Active truth

A simple EF/PostgreSQL `CHECK` cannot assert child existence. Chapter 13 does not pretend otherwise.

Because Chapter 13 permits edits only while Draft, PostgreSQL uses targeted immediate, **statement-level** triggers with transition tables rather than per-row or broad deferred-trigger machinery:

1. Separate `AFTER INSERT` and `AFTER UPDATE` statement triggers on `Listings` inspect the transition rows set-wise and reject every inserted/transitioned Active listing unless it has at least one translation and every translation satisfies LanguageCode, Title, City, and Description publication requirements.
2. Separate `AFTER INSERT`, `AFTER UPDATE`, and `AFTER DELETE` statement triggers on `ListingTranslations` use the event's old/new transition tables, derive the distinct affected parent IDs, lock existing parent rows in canonical UUID order, and reject translation mutation while any parent is Active. For every accepted Draft mutation, the trigger also performs one set-based no-value-change parent `UPDATE` (for example, `SET Status = Status`) so PostgreSQL creates a new parent MVCC tuple version without falsifying audit timestamps. PostgreSQL's event-specific transition-table restrictions are handled explicitly rather than hidden in one pseudo-trigger.
3. Translation mutation is allowed after Status has first changed away from Active in the same transaction.
4. Parent deletion/cascade is explicitly allowed when the parent row no longer exists; the trigger must not make an otherwise valid aggregate delete impossible.
5. A `ListingId` move checks and locks the union of old and new parent IDs in the same canonical order.

The parent MVCC touch closes the higher-isolation write-skew case: a `REPEATABLE READ` transaction that took an old child snapshot cannot activate the parent after a concurrent Draft child mutation; its root update must observe the newer parent tuple and abort rather than validate stale translations. The set-based shape is required: it must not execute one parent lock, parent touch, or aggregate subquery per translation/profile row. It keeps the 200,000-translation query-review seed and the final 70,000 Active transitions practical. Supported application writers acquire the parent first. Arbitrary direct child SQL can already hold child tuples before its statement trigger locks the parent; in a collision PostgreSQL may abort/deadlock-victimize one out-of-band writer, but no committed result may violate integrity. The database rule remains simple because Active translations are frozen and all supported edits first unpublish.

The migration avoids a check-then-enable race. In one migration transaction it takes write-conflicting locks on `Listings` and `ListingTranslations` in that order, installs the named functions/triggers, and then runs a set-based fail-fast validation of every existing Active listing before commit. Any failure rolls back the migration and guards together. Invalid development data therefore requires recreation/correction; there is no production backfill, repair, or delete machinery.

### 8.3 Query-review profile compatibility

The deterministic Chapter 10F seeder currently inserts final Active statuses before translations. The activation trigger would correctly reject that order. The aggregate-integrity checkpoint must change only seed ordering:

```text
insert all profile listings as Draft
-> insert the same translations/details/images
-> apply the same deterministic final status distribution with one set-based SQL command
-> run the same 61 profile invariants
```

The final 100,000-listing/200,000-translation data, result sets, query matrix, and profile version remain unchanged. This is seed compatibility, not benchmark recapture and not permission to rewrite benchmark evidence.

## 9. Public vs Management DTO Strategy

Chapter 13 separates contracts by truth level rather than globally tightening the shared DTO.

| Contract | Surfaces | Translation shape | Required public identity |
|---|---|---|---|
| `PublicListingResponse` | public list, public detail, public agency list, comparables, successful publish | one effective translation | LanguageCode, Title, City, Description are non-null/non-optional |
| existing `ListingResponse` | create, `/my`, agency dashboard, unpublish, archive | one effective translation | flattened translated fields remain nullable because management includes nonpublic states |
| `ListingAuthoringResponse` | management detail and update response | deterministic collection of every translation | per row LanguageCode/Title required; City/Description nullable for Draft |

`ListingAuthoringResponse` contains the editable core fields, current Apartment/House details, status, immutable identity/ownership metadata needed by the client, read-only images, audit timestamps, and every translation with its ID. Translations are ordered by the existing bytewise language comparer and canonical ID tie-break so round trips are deterministic.

AddressLine, Municipality, Neighborhood, media URLs, optional numeric fields, and subtype-specific optional values retain their established nullability in all relevant contracts.

Public JSON property names and values remain the same except that the public schema now truthfully requires LanguageCode, Title, City, and Description. No generic success envelope is added.

## 10. Lifecycle Response Strategy

The existing defect is resolved by fixing loading, not by null-forgiving operators or fake defaults.

The authoring write scope locks the listing parent and loads translations, images, ApartmentDetails, and HouseDetails before a lifecycle mutation. The loaded aggregate remains tracked through save/commit and can be mapped without a post-commit read failure window.

Final response contracts are:

- publish 200: `PublicListingResponse`, because the resulting state is Active and has just passed readiness;
- unpublish 200: existing nullable `ListingResponse`, because the result is Draft;
- archive 200: existing nullable `ListingResponse`, because Archived is not a public-readiness contract.

Existing response JSON fields are preserved. Only the publish schema becomes the strict public DTO. The status endpoints do not become narrow ad hoc responses in this chapter.

## 11. Invalid-State Defensive Policy

Prevention is primary:

- normal status mutation goes through the Domain;
- update and status operations serialize on the parent row;
- activation is database-validated;
- Active translation mutation is database-rejected.

No `Translations.Any(...)`, usable-row filter, or corruption-hiding predicate is added to hot public SELECTs. Pagination totals and Chapter 10 query topology must not change merely to hide impossible data.

If constraints are disabled or physical corruption nevertheless produces malformed Active state:

- strict public mapping throws a typed integrity exception;
- the API returns sanitized canonical `server.unexpected` ProblemDetails and logs high-signal structured context;
- it never supplies `""`, a fallback fake value, or null under a required public field;
- it never turns corruption into a public 404;
- it never silently removes a materialized corrupt item from a page.

The comparable-source branch that currently treats missing effective language/city as an empty result must report an integrity failure for an Active source; this branch change requires no SQL change because those fields are already projected. Source Title and Description are neither used nor returned by that query, so Chapter 13 does **not** add them to its projection or defensively scan them on every comparable request; PostgreSQL aggregate enforcement is the guard for those source-only fields. Returned comparable candidates do expose the four public fields and therefore pass the strict public mapper.

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

Update, publish, unpublish, and archive all use this scope. Existing listing-image scopes retain their own focused abstraction but lock the same parent first, so **supported application writers** share one parent-first lock order and Chapter 11 guarantees are not weakened. This is not a claim that arbitrary direct SQL acquires locks in that order; the statement-trigger behavior and possible deadlock victimization for out-of-band child SQL are defined in Section 8.2.

Results:

- update commits first → publish sees the final Draft content;
- publish commits first → update wakes, sees Active, and returns 409;
- unpublish commits first → update wakes and may edit Draft;
- update/update requests serialize.

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

No locked shape requires planned recapture. The closeout must stop and reassess if implementation deviates:

- unconditional Active/translation eligibility join or predicate: N1, P1, P2, A1, R1, L1, Q1, and C1 are affected;
- effective-selector SQL change: L1, Q1, and C1 are affected;
- root/split projection or include topology change on public paths: all eight shapes are affected;
- comparable source/candidate SQL change: C1 is affected.

Focused generated-SQL/plan review is sufficient when SQL text/projection and topology are unchanged. Recapture only the affected locked shapes if they actually change, using documented totals, IDs, buffers, spills, and medians rather than a remembered latency.

## 16. Test Strategy

### 16.1 Fixture vocabulary

Chapter 13 introduces a clear local distinction:

- **valid Draft builder**: at least one canonical language/title; City/Description may be null;
- **publishable Draft builder**: every translation includes canonical language/title/city/description;
- **valid Active builder**: reaches Active through publish or database setup that first creates complete content;
- **malformed/corrupt entity builder**: unit/domain/mapper use only;
- **database-rejection setup**: explicitly attempts prohibited direct state and asserts constraint/trigger rejection.

Do not disable triggers to manufacture committed corrupt integration rows. Raw SQL status helpers must either operate on publishable content or be renamed/restricted as explicit constraint tests.

### 16.2 Required focused coverage

- language grammar, normalization, duplicate normalization, all lengths, and blank/null text boundaries;
- create Draft behavior with optional City/Description;
- management detail authorization and all-translation representation;
- PUT required-member presence, replacement/omission clearing, translation reconciliation/ID preservation, type-detail conversion, rollback, and audit propagation;
- personal and agency management/publish permission matrices;
- update/status and status/status concurrency under the shared lock, plus the stale-`REPEATABLE READ` direct-write activation case closed by the parent MVCC touch;
- Domain readiness for zero translations and every required field's null/empty/whitespace cases;
- valid and malformed already-Active publish behavior;
- activation and Active-translation database triggers, including concurrency and cascade/no-parent behavior;
- lifecycle responses with loaded translations/images/details;
- strict public mapping and every public surface's four required fields;
- OpenAPI requiredness and public/private schema separation;
- query-review seeder's unchanged final profile invariants.

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
| POST `/api/listings` Location | public Draft detail URL that returns 404 | `/api/listings/{id}/management`; body remains current Draft `ListingResponse` |
| GET `/api/listings/{id}/management` | absent | authenticated `ListingAuthoringResponse` with all translations |
| PUT `/api/listings/{id}` | absent | full Draft replacement; 200 `ListingAuthoringResponse` |
| public list | `PagedResponse<ListingResponse>` with optional translated identity | `PagedResponse<PublicListingResponse>` with required LanguageCode/Title/City/Description |
| public detail | nullable translated `ListingResponse` | strict `PublicListingResponse` |
| public agency list | nullable `PagedResponse<ListingResponse>` | strict `PagedResponse<PublicListingResponse>` |
| comparables | nullable `IReadOnlyList<ListingResponse>` | strict `IReadOnlyList<PublicListingResponse>` |
| publish success | nullable `ListingResponse` | strict `PublicListingResponse` |
| unpublish/archive success | nullable `ListingResponse`, sometimes unloaded | same schema with correctly loaded persisted data |
| not-ready publish | no readiness failure | 409 `conflict.listing_not_ready` |

Swagger must mark public LanguageCode, Title, City, and Description as required and non-null. Management/Draft schemas must not inherit those Active-level promises. The PUT schema's required arrays contain `listingType`, `propertyType`, `price`, `currency`, `areaSquareMeters`, `translations`, and nested `languageCode`/`title`; nullable optional members document omission-as-clear, and optional value enums document omission-as-`Unknown`. Request schemas also document conditional subtype details, stored language pattern/max lengths, and replacement semantics.

OpenAPI structural tests must prove endpoint-to-schema references, required arrays/nullability, all seven pagination members, operation security, 400/401/403/404/409 responses, error-code catalog, and unchanged media/enums.

### 17.2 Frontend handoff order

Chapter 13 closes only after the backend side of this chain is proven:

```text
real invariant
-> application/domain enforcement
-> persistence/runtime truth
-> strict mapping/DTO
-> Swagger/OpenAPI
-> backend contract tests
-> frontend regenerates openapi.d.ts
-> frontend PublicListingCard requires title/languageCode/city/description
-> frontend runtime adapter and adapter tests tighten
-> frontend Chapter 2C resumes
```

The frontend repository is not modified by Chapter 13 implementation or by this plan.

## 18. Explicit Deferrals

Out of scope:

- Commercial, Land, Shop, Office, BuildingPlot, AgriculturalLand, or any new PropertyType;
- new subtype storage, taxonomy-specific filters, or taxonomy-specific comparables;
- fuzzy/full-text search, suggestions, or Description q-search;
- Active partial editing/PATCH;
- agency transfer or creator reassignment;
- image API redesign;
- optimistic version/ETag client conflict handling;
- global test-suite cleanup/reorganization;
- generic repository, UnitOfWork, MediatR, AutoMapper, or FluentValidation package;
- production data backfill machinery;
- frontend UI/catalog implementation;
- historical JWT/config/security “Chapter 13” work, now provisionally Chapter 16.

Chapter 14 remains property model/taxonomy expansion. Chapter 15 remains integration through discovery/API/performance/hardening. Chapter 16's exact security scope must be replanned when reached.

## 19. Ordered Checkpoint Plan

### Checkpoint 13A — Translation Authoring Rules and Row-Level Truth

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

### Checkpoint 13B — Serialized Authoring Scope and Lifecycle Loading

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

### Checkpoint 13C — Complete Management Read Contract

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

### Checkpoint 13D — Atomic Full Draft Replacement

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

### Checkpoint 13E — Domain and Application Publish Readiness

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

### Checkpoint 13F — PostgreSQL Active Aggregate Integrity

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

### Checkpoint 13G — Truthful Public DTO and OpenAPI Contract

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

### Checkpoint 13H — Comprehensive Verification and Frontend Handoff

1. **Goal**

   Verify the complete chapter, record actual evidence, and hand the truthful backend contract to the paused frontend work.

2. **Why it exists**

   The chapter intersects mature publishing, agency, search, integrity, OpenAPI, and performance guarantees and cannot close on focused tests alone.

3. **Exact scope**

   - Run all focused Domain/Application/PostgreSQL/API/concurrency/OpenAPI suites.
   - Run Release build and complete test suite; record actual totals.
   - Apply migrations to fresh PostgreSQL and repeat database update; verify no pending model changes.
   - Inspect named checks, triggers/functions, FK, unique language index, and trigram index.
   - Smoke personal and agency create → management GET → PUT → publish → public list/detail, plus not-ready/error cases.
   - Verify public generated SQL/selector unchanged and profile final invariants unchanged.
   - Update backend context/quality handoff and Chapter 13 completion evidence with actual results.
   - Record historical security/config Chapter 13 as deferred provisional Chapter 16.
   - Produce backend-only frontend handoff; do not touch frontend.

4. **Expected source areas/files or architectural surfaces**

   Entire solution verification, migrations/catalog, Swagger provider/document tests, query-review profile verification, backend documentation/handoff. No new feature implementation belongs here.

5. **Contract/invariant established**

   This checkpoint establishes no new behavior; it proves the integrated Chapter 13 contract and freezes the handoff baseline.

6. **Tests required**

   Focused chapter suites, full suite, fresh schema/repeat migration, OpenAPI structural test, smoke paths, query/profile regression, and any affected-shape performance evidence required by actual SQL changes.

7. **Regression tests that must remain unchanged**

   All Chapter 08–12 guarantees, especially auth/invitations, public visibility, agency ownership/roles, search/filter/pagination/comparables, image integrity/concurrency, ProblemDetails/request IDs, health/CORS/media/OpenAPI.

8. **Migration implications**

   Both Chapter 13 migrations must apply cleanly from empty, update idempotently through EF, have accurate Down paths, match snapshot, and leave no pending model change.

9. **OpenAPI implications**

   Generate/inspect backend v1 through `ISwaggerProvider`; prove strict public and nullable management contracts. Do not regenerate or modify frontend files in this checkpoint.

10. **Performance/query implications**

    No recapture if approved architecture preserved public SQL. If actual implementation changes eligibility, selector, root/split, or comparable SQL, stop closeout and recapture only the affected shapes listed in Section 15; require documented totals/order/buffers/spills/thresholds to pass.

11. **Explicit exclusions**

    No cleanup branch, opportunistic refactor, frontend implementation, taxonomy expansion, JWT/config work, or new feature after verification begins.

12. **Completion/acceptance criteria**

    - Release build has zero errors and warnings unless an explicitly accepted preexisting warning is documented.
    - Full suite and all focused tests pass with actual totals recorded.
    - Fresh/repeat migration and catalog checks pass.
    - OpenAPI proves required public LanguageCode/Title/City/Description and truthful management nullability.
    - Public SQL is unchanged or required affected-shape recapture passes.
    - Backend handoff follows invariant → enforcement → persistence/runtime → mapper/DTO → OpenAPI → contract tests.
    - Frontend is authorized to regenerate and tighten before resuming 2C.

13. **Dependencies**

    13A–13G.

## 20. Final Verification/Closeout Requirements

Chapter 13 is complete only when all of the following are true:

- supported create/update cannot persist malformed translation rows;
- a Draft can be retrieved in full and replaced through authorized APIs;
- Active publication requires every translation's LanguageCode, Title, City, and Description;
- personal/agency authorization and disclosure ordering remain intact;
- update/status races serialize on the listing parent;
- PostgreSQL validates activation, blocks Active translation mutation, and aborts stale-snapshot activation after a concurrent Draft child change;
- lifecycle responses map loaded persisted data;
- public DTO/OpenAPI require the four core fields while Draft management remains nullable;
- no hot public query semantics/topology changed, or affected benchmark shapes have passed required recapture;
- Chapter 11 image guarantees and Chapter 12 failure/pagination/observability guarantees pass unchanged;
- both migrations apply cleanly to fresh PostgreSQL without backfill machinery;
- actual Release build, complete test, smoke, OpenAPI, profile, and any performance results are recorded;
- frontend remains untouched until it consumes the verified generated contract;
- Commercial/Land, global test cleanup, and provisional Chapter 16 security/config work remain deferred.

Chapter 13 does not close merely because nullable C# annotations or Swagger flags changed. It closes when the backend can prove the public contract from write input through committed database state to every public response.
