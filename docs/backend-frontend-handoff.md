# Chapter 13 Backend-to-Frontend Handoff

## Purpose and authority

This is the durable frontend-facing contract finalized by Chapter 13. It describes the verified Chapter 13 HTTP workflow and serialized contracts without changing any frontend file or generating frontend types.

This is not the final whole-backend-to-frontend integration document. Chapter 14 is next and Chapter 15 follows it. After both are complete, a full backend-to-frontend handoff and documentation reconciliation will incorporate this Chapter 13 contract with the rest of the finished backend before frontend integration begins. Do not add Chapter 14 or Chapter 15 contracts here.

The generated OpenAPI document is the contract authority for endpoint schemas, JSON names, required arrays, nullability, enums, security, and canonical error responses. When later frontend integration begins, consume the document emitted by the API's Development Swagger endpoint rather than manually reconstructing DTOs from this prose. This Chapter 13 handoff explains workflow semantics that generated types alone cannot express.

## Authoring workflow

The relevant schemas are:

- `CreateListingRequest` and nested `CreateListingTranslationRequest`;
- `UpdateListingRequest` and nested `UpdateListingTranslationRequest`;
- `ListingResponse` for Create and private lifecycle reads;
- `ListingAuthoringResponse` and `ListingAuthoringTranslationResponse` for management GET/PUT;
- `ListingLocationCandidateResponse`, `ConfirmListingLocationRequest`, and `ListingLocationStateResponse` for location resolution;
- `PublicListingResponse` for public truth and successful publish.

`POST /api/listings` creates a Draft. Drafts may be incomplete for publication. `GET /api/listings/{id}/management` returns the complete server-owned translation set. `PUT /api/listings/{id}` is full replacement of editable Draft content: the submitted translation collection is authoritative, omitted stored languages are deleted, and retained canonical languages preserve their server-owned IDs.

Create and PUT do not accept writable `latitude`, `longitude`, `locationPrecision`, `geocodingProviderKey`, `geocodingResultReference`, or `locationConfirmedAtUtc`. Candidate preview coordinates are display data, never trusted persistence input.

Canonical location-text identity includes every translation's language, City, Municipality, AddressLine, and optional Neighborhood. Changing that identity through PUT invalidates an existing confirmed snapshot. It also makes a previously issued selection token stale.

## Nullable Draft location state

Management responses and `ListingLocationStateResponse` expose the current nullable state through:

- `latitude`;
- `longitude`;
- `locationPrecision`;
- `geocodedDisplayName`;
- `locationConfirmedAtUtc`.

All five are null for a newly unresolved or cleared Draft. A transitional legacy-unverified Draft may truthfully expose a paired latitude/longitude while precision, display name, and confirmation time remain null; that state is not publishable and should be resolved or cleared through the supported workflow. A successful confirmation populates a coherent root snapshot. If authoring changes canonical location text, the snapshot is cleared. The frontend must render the state returned by the backend and must not infer confirmation from candidate preview data or coordinates alone.

Provider keys, provider result references, token claims, credentials, and raw provider responses are not frontend contracts and are not exposed by these DTOs.

## Candidate search

```http
POST /api/listings/{id}/location/candidates
Authorization: Bearer <access-token>
Content-Type: application/json

{
  "languageCode": "mk"
}
```

`languageCode` is optional. The backend selects the effective translation and derives the search input from persisted canonical location text. The selected translation must contain City, Municipality, and AddressLine. The listing must be a Draft, and the actor must be allowed to manage the personal or agency listing.

Each `ListingLocationCandidateResponse` contains required, non-null:

- `label`;
- `previewLatitude`;
- `previewLongitude`;
- `precision`;
- `confirmationToken`.

The token is opaque and short-lived. Do not parse, persist as domain data, log, or modify it. Candidate search is protected by the named authenticated geocoding rate policy. The contract includes 400 validation, 401 authentication, 403 authorization, 404 missing resource, 409 state conflict, 429 rate limit, and 503 dependency-unavailable outcomes.

## Confirmation

```http
PUT /api/listings/{id}/location
Authorization: Bearer <access-token>
Content-Type: application/json

{
  "confirmationToken": "<opaque-token>"
}
```

`ConfirmListingLocationRequest` accepts the opaque token only. The backend validates its listing, actor, expiry, and location fingerprint; re-resolves the provider reference; then enters the Listing parent-locked write transaction, reauthorizes, and rechecks current state before persisting the confirmed snapshot. No live provider call occurs inside that write transaction.

Malformed, tampered, or expired tokens produce canonical validation failure. A token whose location text no longer matches produces `409 conflict.resource_set_changed`. Wrong actor, missing listing, non-Draft state, rate exhaustion, and provider failure retain their canonical 403/404/409/429/503 contracts. Successful confirmation returns `ListingLocationStateResponse` with backend-trusted coordinates, precision, optional display name, and confirmation time.

## Clear

```http
DELETE /api/listings/{id}/location
Authorization: Bearer <access-token>
```

Clear is authorized and Draft-only. It clears confirmed or legacy-unverified state, is idempotent for an already unresolved Draft, and returns `ListingLocationStateResponse` with the location state cleared. It does not call the provider and is not attached to the provider request rate policy. Clearing makes the Draft not ready for publication until a candidate is confirmed again.

## Precision and map preview

`LocationPrecision` is serialized as a provider-neutral string enum:

```text
ExactAddress
Street
Neighborhood
Municipality
City
Approximate
```

Candidate `previewLatitude`/`previewLongitude` and `precision` support a preview pin. They do not authorize coordinate persistence. Confirmed private/management state and strict public `latitude`, `longitude`, and `locationPrecision` are backend truth.

Display the granularity honestly. `ExactAddress` may be represented as exact; `Street`, `Neighborhood`, `Municipality`, `City`, and `Approximate` must not be promoted to exact-address precision. Chapter 13 does not obfuscate public pins or maintain separate exact/private and approximate/public coordinates.

## Publication readiness

Publishing uses `PUT /api/listings/{id}/publish`. Every translation must have meaningful canonical:

- `LanguageCode`;
- `Title`;
- `City`;
- `Municipality`;
- `AddressLine`;
- `Description`.

`Neighborhood` remains optional. The Listing root must have a complete valid backend-confirmed:

- `Latitude` and `Longitude` pair;
- `LocationPrecision`;
- internal backend provenance;
- `LocationConfirmedAtUtc`.

An authorized incomplete Draft returns `409 conflict.listing_not_ready`. The response deliberately does not expose internal readiness or provider diagnostics. Correct published data through the supported `Active -> unpublish -> resolve/edit -> publish` path.

## Strict public contract

The following `PublicListingResponse` identity/map members are required and non-null in serialized OpenAPI and at runtime:

1. `languageCode`;
2. `title`;
3. `city`;
4. `municipality`;
5. `addressLine`;
6. `description`;
7. `latitude`;
8. `longitude`;
9. `locationPrecision`.

The six translated fields come from one effective translation: case-insensitive requested language, then `mk`, then deterministic bytewise language ordering, then translation-ID tie-break. The three map fields come from the Listing root.

Five endpoint families return this strict shape:

| Family | Endpoint | Response shape |
|---|---|---|
| Public list | `GET /api/listings` | `PagedResponse<PublicListingResponse>` |
| Public detail | `GET /api/listings/{id}` | `PublicListingResponse` |
| Public agency listings | `GET /api/agencies/{id}/listings` | `PagedResponse<PublicListingResponse>` |
| Comparables | `GET /api/listings/{id}/comparables` | array of `PublicListingResponse` |
| Publish success | `PUT /api/listings/{id}/publish` | `PublicListingResponse` |

Do not make public provenance or internal integrity state part of frontend types. Provider key/reference and confirmation time are not public response fields.

## Private and management separation

Do not reuse `PublicListingResponse` for Draft management.

- `ListingResponse` remains nullable/private-capable for Create, `GET /api/listings/my`, agency dashboard listings, unpublish, and archive.
- `ListingAuthoringResponse` is used by management GET/PUT, carries all translations, and has a required/non-null `translations` collection.
- Within each authoring translation, `languageCode` and `title` are required/non-null; Draft `city`, `municipality`, `addressLine`, `description`, and `neighborhood` remain nullable.
- Management root location state remains nullable until confirmation.

## Create and PUT serialized truth

For `CreateListingRequest`, OpenAPI requires `listingType`, `propertyType`, `price`, `areaSquareMeters`, and `translations`. `currency` is optional on the wire: omission uses the runtime default `EUR`; explicit null or invalid currency is rejected. Nested Create translations require non-null `languageCode` and `title`.

For full-replacement `UpdateListingRequest`, OpenAPI requires and makes non-null all six members: `listingType`, `propertyType`, `price`, `currency`, `areaSquareMeters`, and `translations`. Nested update translations require non-null `languageCode` and `title`. Optional Draft translation fields may be omitted or null to clear them according to the generated schema descriptions.

Neither request schema accepts coordinates, precision, provenance, provider identity/reference, or confirmation time.

## Error and failure handling

Frontend code should branch on HTTP status and stable `code`, not provider text or human-readable `detail`:

| HTTP | Relevant stable code | Meaning |
|---:|---|---|
| 400 | `validation.failed` | Invalid authoring input or malformed/expired/tampered confirmation token |
| 401 | `authentication.required` / `authentication.invalid_principal` | Anonymous or unusable authenticated principal |
| 403 | `authorization.forbidden` / `authorization.account_disabled` | Actor cannot manage the listing |
| 404 | `resource.not_found` | Listing/resource is unavailable to the operation |
| 409 | `conflict.resource_state` | Operation conflicts with listing status |
| 409 | `conflict.resource_set_changed` | Candidate selection is stale |
| 409 | `conflict.listing_not_ready` | Authorized Draft is not publishable |
| 429 | `rate_limit.geocoding_exceeded` | Candidate/confirmation request rate exceeded; honor `Retry-After` when present |
| 503 | `dependency.geocoding_unavailable` | Provider-mediated search/confirmation is temporarily unavailable |
| 500 | `server.unexpected` | Sanitized unexpected failure |

Canonical ProblemDetails includes request correlation through `traceId`/`X-Request-ID`. Never surface or log raw provider errors, credentials, references, addresses, tokens, PostgreSQL diagnostics, `PublicListingIntegrityException`, or readiness internals. Impossible materialized Active corruption is sanitized as `500 server.unexpected`; it is not a public 404 and no fake values are returned.

## Generated OpenAPI consumption

Generate frontend types and clients later from the verified live OpenAPI document, particularly the Listings and Agencies operations and these schema families:

- authoring: `CreateListingRequest`, `UpdateListingRequest`, nested translation requests, `ListingAuthoringResponse`;
- location workflow: `SearchLocationCandidatesRequest`, `ListingLocationCandidateResponse`, `ConfirmListingLocationRequest`, `ListingLocationStateResponse`, `LocationPrecision`;
- public/private output: `PublicListingResponse`, `ListingResponse`, and their pagination wrappers;
- failures: canonical ProblemDetails schemas and stable error codes.

Preserve generated required/nullability distinctions. Do not hand-maintain one shared listing interface that makes strict public fields nullable or makes Draft fields required. Do not generate frontend types as part of backend Chapter 13L.2.

## Explicit deferrals

Chapter 13 did not implement PostGIS, radius/polygon/viewport or other spatial discovery, canonical geography IDs, clustering, public-pin privacy/obfuscation, taxonomy expansion, global cleanup, production JWT/configuration hardening, or unrelated deferred Chapter 11/12 work. Do not infer those capabilities from confirmed coordinates.

Durable backend evidence:

- [Chapter 13 authoritative closeout](chapters/chapter-13-public-listing-integrity-authoring.md)
- [Chapter 13L.1 cumulative verification](chapters/chapter-13l1-cumulative-chapter-13-verification-gate.md)
- [Chapter 13K.3 generated-SQL freeze proof](benchmarks/chapter-10f/chapter-13k3-final-generated-sql-freeze-proof.md)
- [Backend quality handoff](backend-quality-handoff.md)
