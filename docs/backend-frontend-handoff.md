# Backend-to-Frontend Integration Handoff — Accepted Backend through Chapter 15

## 1. Purpose, authority and OpenAPI consumption

This is the single current backend-to-frontend integration handoff for the accepted backend through Chapter 15. It explicitly supersedes this file's former Chapter-13-only freeze. The historical snapshot remains recoverable from Git; historical gates and commits are unchanged. No separate Chapter 14/15 handoff or frontend implementation is introduced.

Reconciliation source: `addda7529dfd96a8f5a221c98448f278abef54a8`, branch `docs/final-backend-frontend-handoff`; production `src` tree `367529280368366f8216471daff0c2d9e7984ccd`. The owner confirms independent 15M acceptance; closeout content commit `a84a0f44dfac321af006e81e0c060e448a74f666` is included in this source. Earlier submission-stage wording in the chapter plan/backend-context that still says 15M is pending records the pre-acceptance state, not unfinished backend work.

The **actual generated OpenAPI** is the HTTP schema authority for paths, JSON names, parameters, required members, nullability, security and responses. This document explains workflow and authorization semantics that generated types alone cannot express. Consume the Development API's `/swagger/v1/swagger.json`; do not reconstruct DTOs from prose. Swagger UI is a Development integration aid, not a promised production endpoint.

This reconciliation inspected the authentic application-generated OpenAPI retained by [15L](chapters/chapter-15l-cumulative-chapter-15-verification-gate.md): **46 operations**, SHA-256 `e99a94c3aef8a7c07bd5ee91497d8ade0b7caef75315f3f241a9a04f7ff0645e`. Its verified API source tree is identical to the reconciliation source. No new test run, benchmark capture or generated client was needed. Route tables below cover those 46 operations; `[]` means an array and `Paged<T>` means the corresponding generated pagination schema. Success status/body is listed; exact per-operation errors remain in OpenAPI and section 12 explains their handling.

Further authority: [backend context](backend-context.md), [quality register](backend-quality-handoff.md), [Chapter 11 integrity](chapters/chapter-11-data-integrity-targeted-hardening.md), [Chapter 12 HTTP consistency](chapters/chapter-12-api-consistency-observability-frontend-readiness.md), [Chapter 13 authoring/integrity](chapters/chapter-13-public-listing-integrity-authoring.md), [Chapter 14 taxonomy](chapters/chapter-14-property-model-taxonomy-expansion.md), and [Chapter 15 discovery](chapters/chapter-15-discovery-performance-hardening.md).

## 2. Authentication, users, JWT and authorization

| Operation | Access / request | Success |
|---|---|---|
| `POST /api/auth/register` | Anonymous; `RegisterRequest` | 201 `AuthResponse` |
| `POST /api/auth/login` | Anonymous; `LoginRequest` | 200 `LoginResponse` |
| `GET /api/users/me` | Bearer | 200 `UserProfileResponse` |
| `PUT /api/users/me/profile` | Bearer; `UpdateUserProfileRequest` | 200 `UserProfileResponse` |
| `PUT /api/users/me/avatar` | Bearer; multipart `file` | 200 `UserProfileResponse` |
| `DELETE /api/users/me/avatar` | Bearer | 204, no body |

Registration takes `email`, `password`, `firstName`, `lastName`, optional `phoneNumber`. Runtime requires valid email, password of at least eight characters and nonblank names, even though this older request schema has no OpenAPI `required` array. Duplicate normalized email returns 409. Registration creates a `User` / `PendingVerification` account and returns `{ user }`, **not a JWT**. Login takes email/password, returns `{ accessToken, user }`, and uses indistinguishable 401 invalid-credentials errors. Use `/api/users/me` for the current user; registration's Location header is not an implemented general user-by-ID read endpoint.

Send `Authorization: Bearer <access-token>` on protected calls. OpenAPI declares HTTP bearer scheme `Bearer`, format `JWT`; Swagger UI expects the raw token. JWT issuer, audience, signing key and lifetime are validated. Never log tokens or put them in URLs. There are no refresh-token, logout/revocation, password-reset or email-verification endpoints. Client logout clears client-held state; it does not revoke the server token.

Login currently does not block PendingVerification or Disabled users, and `/me` can return a Disabled user. A token is not operation permission: handlers resolve current user state/resource access. Disabled users are rejected for protected mutations. PendingVerification users can author Drafts; **publication requires an Active user**. There is no frontend self-activation endpoint.

Profile update accepts `firstName`, `lastName`, `phoneNumber`, not email, role or status. `UserProfileResponse` includes identity, names/contact, role, status, avatarUrl, createdAtUtc and modifiedAtUtc. `AuthUserResponse` instead exposes role/status as strings and has no avatar/audit fields; generate each schema separately.

## 3. Agencies, memberships, invitations and roles

| Operation | Access / request | Success |
|---|---|---|
| `POST /api/agencies` | Bearer; `CreateAgencyRequest` | 201 `AgencyResponse` |
| `GET /api/agencies/my` | Bearer | 200 `MyAgencyResponse[]` |
| `GET /api/agencies/by-slug/{slug}` | Anonymous | 200 `AgencyResponse` |
| `GET /api/agencies/{id}` | Anonymous | 200 `AgencyResponse` |
| `PUT /api/agencies/{id}` | Active Owner membership; `UpdateAgencyRequest` | 200 `AgencyResponse` |
| `GET /api/agencies/{id}/members` | Active membership | 200 `AgencyMemberResponse[]` |
| `POST /api/agencies/{id}/invitations` | Active Owner membership; `CreateAgencyInvitationRequest` | 201 `AgencyInvitationCreatedResponse` |
| `GET /api/agencies/{id}/invitations` | Active Owner membership; optional `status` | 200 `AgencyInvitationListItemResponse[]` |
| `PUT /api/agencies/invitations/accept` | Bearer; `AcceptAgencyInvitationRequest` | 200 `AgencyInvitationListItemResponse` |
| `PUT /api/agencies/{agencyId}/invitations/{invitationId}/cancel` | Active Owner membership | 200 `AgencyInvitationListItemResponse` |
| `PUT /api/agencies/{agencyId}/members/{memberId}/disable` | Active Owner membership | 204, no body |
| `PUT /api/agencies/{agencyId}/members/{memberId}/role` | Active Owner membership; `ChangeAgencyMemberRoleRequest` | 204, no body |
| `PUT /api/agencies/{agencyId}/logo` | Active Owner membership; multipart `file` | 200 `AgencyResponse` |
| `DELETE /api/agencies/{agencyId}/logo` | Active Owner membership | 204, no body |
| `PUT /api/admin/agencies/{agencyId}/approve` | Active global Admin | 200 `AgencyResponse` |
| `PUT /api/admin/agencies/{agencyId}/reject` | Active global Admin | 200 `AgencyResponse` |
| `PUT /api/admin/agencies/{agencyId}/disable` | Active global Admin | 200 `AgencyResponse` |

All membership/admin operations require Bearer authentication and current server-side checks. Here “Active Owner” describes membership, not a substitute for account checks. Disabled users are forbidden. Global `UserRole` (User, Agent, AgencyOwner, Admin) does not grant agency membership. `AgencyMemberRole` has Owner, Manager, Agent, but invitation and role-change requests accept **only Owner or Agent**. Do not offer Manager assignment because it appears in the shared enum. Manager has no general listing-management/dashboard permission. Member-list access accepts any Active membership.

Agency creation starts PendingVerification and creates an Active Owner membership for its creator. `CreateAgencyRequest` has name, slug, description, phoneNumber, email, websiteUrl, addressLine, city, municipality; update has the same editable profile fields **except slug**. Runtime validates fields independently of older schemas' optionality. Slug conflicts return 409. Public agency reads are not limited to Active agencies; render the returned status. There is no general agency-directory/search endpoint. Platform agency administration is separate from ownership.

`MyAgencyResponse` distinguishes agencyId, agencyStatus, memberRole and memberStatus. Member administration uses `AgencyMemberResponse.memberId`, not userId. Invitation creation takes `{ email, role }`; its response includes token and code, while subsequent list/accept/cancel responses omit both. Accept sends **`{ token }`**, not the code or a claimed user ID, and must match the authenticated user's email. No code-based acceptance or automatic email-delivery contract exists. Treat invitation credentials as secrets.

Invitation statuses are Pending, Accepted, Cancelled, Expired; reads/filtering reflect expiry without a background job. Acceptance/cancellation are contested transitions: handle 409 by reloading. Last-active-owner protection and self-disable restrictions remain enforced; role changes may not remove the last Active Owner. Role UI is advisory, never a replacement for server checks.

## 4. Listing creation, editing, lifecycle and images

| Operation | Access / request | Success |
|---|---|---|
| `POST /api/listings` | Bearer; `CreateListingRequest` | 201 `ListingResponse` |
| `GET /api/listings/{id}/management` | Listing management permission | 200 `ListingAuthoringResponse` |
| `PUT /api/listings/{id}` | Listing management permission; `UpdateListingRequest` | 200 `ListingAuthoringResponse` |
| `GET /api/listings/my` | Bearer; lang, page, pageSize | 200 `Paged<ListingResponse>` |
| `PUT /api/listings/{id}/publish` | Publish permission; optional lang | 200 `PublicListingResponse` |
| `PUT /api/listings/{id}/unpublish` | Listing management permission; optional lang | 200 `ListingResponse` |
| `PUT /api/listings/{id}/archive` | Listing management permission; optional lang | 200 `ListingResponse` |
| `POST /api/listings/{id}/images` | Creator-only; multipart file | 201 `ListingImageResponse` |
| `DELETE /api/listings/{listingId}/images/{imageId}` | Creator-only | 204, no body |
| `PUT /api/listings/{listingId}/images/{imageId}/primary` | Creator-only | 200 `ListingImageResponse` |
| `PUT /api/listings/{listingId}/images/order` | Creator-only; `ReorderListingImagesRequest` | 200 `ListingImageResponse[]` |

All require Bearer authentication. Personal authoring/management/lifecycle requires the creator. Agency listing authoring/management/lifecycle requires an Active Owner or Agent membership, not necessarily the creator. Agency publication additionally requires an Active agency; all publication requires an Active user. Management access is not a global Admin override. Image mutations remain **creator-only**, including agency listings: management rights alone do not grant image rights. `/my` is a creator-oriented private list, not the agency dashboard.

Create produces a Draft. Optional `agencyId` on Create selects agency ownership (null means personal); PUT does not transfer ownership. Management GET returns all translations. PUT is **full replacement of editable Draft content**, not PATCH: omitted nullable values clear, omitted optional enums reset Unknown, and translations are the complete authoritative set. Omitted stored languages are deleted; retained canonical languages retain server-owned translation IDs. Images, creator, agency and audit fields are not replaced. Root changes require the new matching detail object and remove incompatible details.

Supported lifecycle: Draft -> publish -> Active; Active -> unpublish -> Draft; Draft/Active -> archive -> Archived. Repeating publish on a ready Active listing, unpublish on Draft, or archive on Archived is state-idempotent but still subject to authorization/readiness. Other state conflicts return 409. Reserved, Sold, Rented exist in the status vocabulary but have no transition endpoints here. There is no listing DELETE, restore-from-archive or arbitrary status setter. Edit published content through unpublish -> edit/resolve -> publish.

Publication requires every translation to have meaningful canonical language, title, city, municipality, address and description, and the root to have a valid backend-confirmed coordinate pair, precision, internal provenance and confirmation time. Neighborhood stays optional. Authorized incomplete Drafts return `409 conflict.listing_not_ready` without internal readiness/provider diagnostics. Unknown subtypes and optional attributes do not become new publication requirements.

All three uploads (listing image, avatar, agency logo) use multipart field `file`: maximum **5,242,880 bytes**, .jpg/.jpeg/.png/.webp, matching image/jpeg, image/png or image/webp. Listing capacity is 20 images. Reorder sends `{ imageIds: [...] }` with the entire current set exactly once; refresh after capacity/set conflicts. Images expose id, url, contentType, sizeBytes, sortOrder, isPrimary. Scoped server serialization protects order/primary selection under concurrent image writes; it does not imply universal ETags.

Media URLs are API-relative `/uploads/...`: resolve against the API origin, not the frontend origin. Successful files are anonymously served as bytes. Missing static files are ordinary 404s, not API ProblemDetails. Database success does not promise durably retried physical-file deletion.

## 5. Four-root taxonomy and subtype contracts

| Root PropertyType | Required matching authoring child | Subtype enum |
|---|---|---|
| Apartment = 1 | apartmentDetails | ApartmentType |
| House = 2 | houseDetails | HouseType |
| Commercial = 3 | commercialDetails | CommercialType |
| Land = 4 | landDetails | LandType |

`CommercialType`: Unknown=0, Office=1, Shop=2, Other=3.
`LandType`: Unknown=0, BuildingPlot=1, AgriculturalLand=2, Other=3.

Exactly one matching detail object must be submitted in Create/PUT; other non-null family objects are forbidden. OpenAPI parent child properties are conditionally required and nullable (`oneOf`), not four unconditional required fields. `{ "commercialDetails": {} }` uses commercialType Unknown; `{ "landDetails": {} }` uses landType Unknown. Explicit null subtype/undefined enum is not a request to default. Unknown is legitimate in Draft and Active.

Current detail fields:

- Apartment: apartmentType, nullable floor, totalFloors, hasElevator.
- House: houseType, nullable numberOfFloors, yardAreaSquareMeters.
- Commercial: **only commercialType**.
- Land: **only landType**.

Do not invent zoning, frontage, utility, office-capacity or valuation fields. Existing Apartment/House validation remains: nonnegative floor/yard counts and consistent floor bounds. Common optional fields remain available as defined by generated schemas; they are not newly required by a root.

| Schema | Exact names |
|---|---|
| ListingType | Sale, Rent |
| ApartmentType | Unknown, Studio, Standard, Penthouse, Duplex, Loft, Maisonette, Other |
| HouseType | Unknown, Detached, SemiDetached, Terraced, Townhouse, Villa, Cottage, Other |
| HeatingType | Unknown, None, Electric, Central, Gas, Wood, HeatPump, Other |
| FurnishingStatus | Unknown, Unfurnished, SemiFurnished, Furnished |
| PropertyCondition | Unknown, New, Renovated, Good, NeedsRenovation |
| Orientation | Unknown, North, South, East, West, NorthEast, NorthWest, SouthEast, SouthWest |
| ListingStatus | Draft, Active, Reserved, Sold, Rented, Archived |

Responses serialize enum **names**. Defined numeric enum inputs remain accepted by established binding/converter behavior; names are preferable for clients. Undefined values and malformed names reject, not silently become Unknown. Shared vocabulary does not authorize every value on every endpoint (notably agency Manager).

## 6. Public discovery, filters, sorting and paging

| Operation | Query / access | Success |
|---|---|---|
| `GET /api/listings` | General search below; anonymous | 200 `Paged<PublicListingResponse>` |
| `GET /api/listings/{id}` | lang; anonymous | 200 `PublicListingResponse` |

General search advertises exactly these 29 optional query parameters:

| Group | Exact names |
|---|---|
| Language/search | lang, q |
| Ownership/type | listingType, agencyId, propertyType |
| Common attributes | heatingType, furnishingStatus, condition, hasBasement |
| Detail filters | hasElevator, apartmentType, houseType, commercialType, landType |
| House yard | minYardAreaSquareMeters, maxYardAreaSquareMeters |
| Price | minPrice, maxPrice, currency |
| Area/rooms | minAreaSquareMeters, maxAreaSquareMeters, minRooms, maxRooms |
| Location | city, municipality, neighborhood |
| Order/page | sort, page, pageSize |

Subtype filters are **scalar**, not lists. Omit to disable; `commercialType=Unknown` or `commercialType=0` actively filters Unknown. Commercial and Land each require explicit matching root equality **and** matching child/subtype. `commercialType=Office` restricts Commercial without needing propertyType; adding `propertyType=Commercial` is equivalent. Contradictory root/subtype inputs and simultaneous Commercial/Land subtype inputs use AND semantics and return a normal empty page, not a union or special 400. Malformed/undefined HTTP enum values return canonical model-state 400; direct application-query undefined enums also reject. Protected Apartment/House filters retain their existing semantics.

Example query strings:

```text
/api/listings?lang=mk&commercialType=Office&currency=EUR&sort=priceAsc&page=1&pageSize=20
/api/listings?landType=Unknown&sort=newest
/api/listings?agencyId=<agency-uuid>&commercialType=Shop
/api/listings?commercialType=Office&landType=BuildingPlot
```

`sort` accepts newest (default), priceAsc, priceDesc. Newest orders CreatedAtUtc descending then ID descending; price order adds those same deterministic ties. Any price range or price sort requires currency; no conversion or cross-currency comparison is provided. Currency trims/uppercases to three ASCII letters; it is **not an EUR/USD/MKD-only enum**. Price/area bounds must be positive and ordered; room/yard bounds nonnegative and ordered. Nullable attributes do not acquire fake zero values.

`q` trims; blank means absent, otherwise length 2–100. It is case-insensitive literal substring matching on **Title, City, Municipality, Neighborhood only**, from one effective translation. It does not search description/address/provider data. Backslash, % and _ are escaped as literals by the backend; URL-encode input but do not implement SQL escaping in the client. Location filters trim, allow at most 100 characters, and perform literal case-insensitive equality on that same translation. They are strings, not canonical geography IDs or spatial queries.

Paging defaults to page 1 / size 20; page below 1 normalizes to 1, size below 1 to 20, size over 100 to 100. Consume returned normalized values. Page fields are items, page, pageSize, totalCount, totalPages, hasNextPage, hasPreviousPage; zero matches gives empty items and zero totalPages. Filtering precedes count/page, root paging precedes translation/image hydration, and hydration does not multiply root items. Deterministic ordering is not a snapshot guarantee across separate requests while data changes.

## 7. Translation fallback and public visibility

Public detail/list/agency/comparable reads are **Active-only**. Missing or non-Active public detail/comparable sources return 404; authoring data belongs on management/private endpoints. Select one effective translation: case-insensitive requested language -> mk -> deterministic bytewise language order -> translation UUID tie-break. Missing/blank lang defaults to mk. Fallback supplies the complete translation, not independently mixed fields. Search/location filtering and response mapping use the same effective language.

Strict `PublicListingResponse` requires nine non-null members in OpenAPI and runtime: **languageCode, title, city, municipality, addressLine, description, latitude, longitude, locationPrecision**. Neighborhood stays nullable. Root coordinates are backend truth, not a translation property. Public output omits provider key/reference, confirmation timestamp and internal readiness diagnostics.

Fail-closed integrity does not authorize placeholder values or silently dropping corrupted rows. Impossible materialized Active translation/location corruption becomes sanitized `500 server.unexpected`, not public 404. Commercial/Land subtype discovery excludes missing/mismatched matching detail rows; do not infer a universal legacy detail-repair mechanism. Supported authoring creates valid matching details.

## 8. Draft location search, confirmation and clearing

| Operation | Request / access | Success |
|---|---|---|
| `POST /api/listings/{id}/location/candidates` | `SearchLocationCandidatesRequest`; Draft management permission | 200 `ListingLocationCandidateResponse[]` |
| `PUT /api/listings/{id}/location` | `ConfirmListingLocationRequest`; Draft management permission | 200 `ListingLocationStateResponse` |
| `DELETE /api/listings/{id}/location` | Draft management permission | 200 `ListingLocationStateResponse` |

All require Bearer authentication. Create and authoring PUT do **not** accept writable latitude, longitude, locationPrecision, geocodingProviderKey, geocodingResultReference or locationConfirmedAtUtc. Candidate preview coordinates are not trusted persistence input.

1. Save Draft translation text first. Search sends `{ "languageCode": "mk" }` (language optional); the backend selects the effective persisted translation and requires its City, Municipality and AddressLine.
2. Display candidates' required/non-null label, previewLatitude, previewLongitude, precision, confirmationToken. The token is opaque and short-lived: do not parse, modify, log or persist as domain data.
3. Confirm with `{ "confirmationToken": "<opaque-token>" }` only. The backend validates actor/listing/expiry/text fingerprint, re-resolves the provider reference outside the write transaction, then reauthorizes/rechecks under the Listing parent lock before persistence.
4. Render returned state. Malformed/tampered/expired token is 400; changed location text is `409 conflict.resource_set_changed`. Reload and search again on stale selection. Candidate search and confirmation can return 429 with Retry-After and 503 geocoding unavailable. Wrong actor, missing listing and non-Draft state retain 403/404/409 behavior.
5. Clear uses DELETE, returns **200 state, not 204**, and is idempotent for an unresolved Draft. It makes no provider call and is not attached to the provider request rate policy. Resolve again before publication.

Nullable Draft/management state is latitude, longitude, locationPrecision, geocodedDisplayName, locationConfirmedAtUtc. All five are null on a new/cleared Draft. A legacy-unverified Draft can expose paired coordinates with the other confirmation fields null; that is not publishable. Successful confirmation sets a coherent root snapshot with optional display name. Coordinates alone or preview data do not prove confirmation.

The fingerprint covers **every translation's** language, City, Municipality, AddressLine and optional Neighborhood. Changing that canonical identity through PUT clears confirmation and makes old selection tokens stale. Description/title-only edits are not location-fingerprint fields.

`LocationPrecision`: ExactAddress, Street, Neighborhood, Municipality, City, Approximate. Show the true granularity; never promote a coarse result to exact. There is no public-pin obfuscation or separate exact-private/approximate-public coordinate contract. Provider credentials, keys, references, token claims and raw provider responses are not client DTOs.

## 9. Public agency listings and private dashboards

| Operation | Query / access | Success |
|---|---|---|
| `GET /api/agencies/{id}/listings` | Anonymous; lang, sort, currency, page, pageSize | 200 `Paged<PublicListingResponse>` |
| `GET /api/agencies/{id}/dashboard/listings` | Bearer, Active Owner/Agent membership; lang, status, page, pageSize | 200 `Paged<ListingResponse>` |
| `GET /api/agencies/{agencyId}/dashboard/summary` | Bearer, Active Owner/Agent membership | 200 `AgencyDashboardSummaryResponse` |

Public agency listings stay Active-only and retain the reduced five-parameter vocabulary. They do **not** advertise/apply propertyType, subtype, q or other general-search filters; unbound subtype keys are ignored, not supported filters. For agency + subtype discovery use general `/api/listings?agencyId=...&commercialType=...` (or landType).

Dashboard listings are private and accept a defined ListingStatus; omission does not impose public Active-only semantics. They retain nullable ListingResponse, deterministic paging and no subtype/search vocabulary. Dashboard access rejects Disabled users, missing agencies and unauthorized/inactive memberships before direct-query undefined-Status validation; malformed HTTP enums reject earlier at model binding. Chapter 15 did not move repository access ahead of authorization or broaden Manager access.

Summary fields: agencyId, agencyName, agencyStatus, totalListings, draftListings, activeListings, archivedListings, membersCount, activeMembersCount, pendingInvitationsCount. Do not invent reserved/sold/rented counters or assume the three named listing counters necessarily exhaust totalListings.

## 10. Comparable listings

| Operation | Query / access | Success |
|---|---|---|
| `GET /api/listings/{id}/comparables` | Anonymous; lang, limit | 200 `PublicListingResponse[]` |

Default limit is **6**, valid range **1–12** (invalid values return 400, not a clamp). Source must be Active; unavailable source returns 404, source integrity violations sanitized 500. Candidates exclude the source, are Active, have the same ListingType, **exact PropertyType root**, currency and effective City, and usable positive price/area. Results can be fewer than limit or empty.

The six ranking keys remain: municipality/neighborhood match tier; relative area difference; relative price-per-square-meter difference; relative price difference; CreatedAtUtc descending; ID descending. This is **not subtype-aware**: Office and Shop may compare within Commercial; Land subtypes are not separately ranked. No subtype filters, cross-root similarity, currency conversion, valuation estimate or market-value guarantee is offered.

## 11. DTO serialization, enums, required members and nullability

Use generated camelCase names. UUIDs are strings, timestamps date-time strings, and price/area JSON numbers (OpenAPI number/double, backed by .NET decimal); preserve currency precision in UI calculations. Currency is a string, not an enum. Required presence and nullability are **separate** properties; older schemas can advertise optional/nullable fields which runtime validation requires. Do not strengthen generated types by assumption.

| Contract | Required/nullability distinction |
|---|---|
| CreateListingRequest | Required listingType, propertyType, price, areaSquareMeters, translations. Omitted currency defaults EUR; explicit null/invalid currency rejects. Matching detail conditionally required. |
| UpdateListingRequest | Requires those five plus currency; complete Draft replacement, not partial update. Optional nullable root fields clear on omission/null; optional enums reset Unknown. |
| Nested create/update translation | Required non-null languageCode, title; Draft description, addressLine, city, municipality, neighborhood may be omitted/null. Canonical language entries must be unique. |
| ListingAuthoringResponse | Required currency, images, translations; currency/images still carry nullable schema flags. Translations is non-null and complete. Each translation requires non-null languageCode/title and includes server id. Root location and detail objects nullable. |
| ListingResponse | Create, /my, dashboard, unpublish/archive. One effective translation with nullable Draft text/location; not all translations or strict public truth. |
| PublicListingResponse | Public list/detail/agency/comparables and publish. Nine strict members from section 7; other nullable fields, detail-object oneOf and media nullability remain as generated. No private confirmation/provenance fields. |
| Pagination | Required non-null items for public/private wrappers; use metadata, not item count, for totals. |
| ProblemDetails | Required code, traceId; validation also requires errors. Generated nullable flags remain; do not pretend every standard ProblemDetails field is required. |

Common inputs include nullable rooms, bathrooms, balcony/parking counts, basement/exchange flags, renovation/build years and optional enum attributes. HasElevator belongs to Apartment details; yard area to House details. Responses have four nullable family detail properties: narrow on propertyType while respecting nullability, not a fabricated flat subtype field. Do not weaken strict public types or force Draft fields to be non-null in a shared interface.

Remaining exact vocabularies: UserRole = User/Agent/AgencyOwner/Admin; UserStatus = PendingVerification/Active/Disabled; AgencyStatus = PendingVerification/Active/Disabled/Rejected; AgencyMemberRole = Owner/Manager/Agent; AgencyMemberStatus = Active/Pending/Disabled; AgencyInvitationStatus = Pending/Accepted/Cancelled/Expired. Auth response role/status strings and enums on other responses remain distinct contracts.

## 12. Canonical errors, correlation, health and CORS

API failures use canonical `application/problem+json`: standard type, title, status, detail and instance fields, plus stable code and traceId matching response `X-Request-ID`. Validation adds errors (field/request keys mapped to message arrays). Preserve the generated optionality/nullability rather than requiring every standard field. Branch on status and code, not human-readable detail or database/provider messages. Retain correlation IDs for support; client-supplied request-ID values do not override server tracing.

| HTTP | Stable codes / action |
|---:|---|
| 400 | validation.failed; validation.file_required, validation.file_empty, validation.file_too_large, validation.file_type_not_supported. Show field/upload feedback; invalid bindings are not empty searches. |
| 401 | authentication.required, authentication.invalid_principal, authentication.invalid_credentials. Protected challenges/stale principal use WWW-Authenticate: Bearer; invalid-login credentials are not a protected-route challenge. |
| 403 | authorization.forbidden, authorization.account_disabled. A token/role label is not permission. |
| 404 | resource.not_found. Public non-Active listings are unavailable; management has separate checks. |
| 405 / 415 | request.method_not_allowed / request.media_type_not_supported. Correct method/content type. |
| 409 | conflict.email_already_exists, conflict.agency_slug_already_exists, conflict.resource_state, conflict.listing_not_ready, conflict.resource_capacity, conflict.resource_set_changed. Refresh; do not blindly replay stale replacement/reorder/confirmation. |
| 429 | rate_limit.geocoding_exceeded. Honor Retry-After when present. |
| 503 | dependency.geocoding_unavailable. Preserve Draft and retry deliberately. |
| 500 | server.unexpected. Sanitized failure; use correlation ID, never fabricate DTO values or infer 404. |

Errors are operation-specific in OpenAPI; this table does not promise every code on every route. Never expose/log raw provider errors, credentials/tokens, internal references, personal addresses, PostgreSQL diagnostics or readiness/integrity internals.

| Operation | Access | Success / expected unavailability |
|---|---|---|
| `GET /api/health` | Anonymous | 200 dependency-free liveness JSON |
| `GET /api/health/readiness` | Anonymous | 200 ready / 503 not ready JSON |
| `GET /api/health/database` | Anonymous | 200 / 503, database-readiness alias |

Health uses dedicated sanitized JSON for expected readiness failure, **not ProblemDetails**. These are not session checks or frontend performance guarantees.

CORS uses exact configured `Cors:AllowedOrigins`; Development defaults permit localhost ports 3000 and 5173 over HTTP/HTTPS. API and successful media responses support permitted origins and expose X-Request-ID; credentials/cookie CORS is not enabled. Non-Development must configure real origins; missing configuration grants no wildcard access. Do not assume cookie authentication or hard-code deployment origins to work around CORS.

## 13. Practical frontend workflows and integration order

1. **Contract/client foundation:** consume OpenAPI, preserve DTO families/nullability, centralize HTTP errors/correlation, configure API origin/bearer attachment/API-relative media. No secrets in generated clients.
2. **Public browsing:** general search/detail/paging and effective-language display; scalar filters; reset page when filters change. Comparables are exact-root suggestions, not valuation.
3. **Accounts:** register -> login -> /me; display PendingVerification truthfully. Do not fabricate activation, refresh, password-reset or revocation calls. Handle later 401/403 despite earlier login success.
4. **Personal Draft:** create with exactly one matching detail; load management; submit complete translations/details on edit. Preserve reset semantics/server IDs. Use creator-only images and returned order/primary metadata.
5. **Location/publication:** save text -> search -> preview precision -> confirm token -> render state -> publish when ready. Discard old candidates after location edits. Unpublish before editing Active content; do not PATCH private fields into public DTOs.
6. **Agency workspace:** separate owner administration, Owner/Agent listing management and creator-only images. Invite/accept with token; refresh contested state. General search handles agency + subtype; global Admin handles approval.
7. **Concurrency/failure UX:** retain form data appropriately but refetch before resolving 409. No universal optimistic-version contract exists. Handle nullable media/location and static 404 without changing backend meaning.

This is an integration sequence, not authorization to implement deferrals or a claim that an existing frontend already conforms.

## 14. Known limitations, deferred features and quality issues

Accepted [15L verification](chapters/chapter-15l-cumulative-chapter-15-verification-gate.md) records .NET 10, **2,419 passed / 0 failed / 0 skipped**, **375 unique focused identities separately counted**, 46 OpenAPI operations, 21 migrations and no pending EF model changes. This handoff does not rerun/add those counts. Commercial/Land **NO_INDEX** is an intentional measured decision; conditional 15I migration was skipped.

[PG16 permanent evidence](benchmarks/four-root-discovery-v1/evidence/postgresql-16/baseline-summary.md) remains Chapter 15 correctness, historical-comparison, performance and index-decision authority. [PG18.4 evidence](benchmarks/four-root-discovery-v1/evidence/postgresql-18.4/baseline-summary.md) is bounded compatibility/performance observation, not replacement authority. Synthetic four-root data and zero cross-major review exceedances are **not market distributions, production latency guarantees or an SLA**. No benchmark tuning/new evidence belongs to this handoff.

All ten [quality-register](backend-quality-handoff.md) items remain **OPEN**, with existing ownership and context unchanged:

| ID | Limitation / integration implication |
|---|---|
| QH-TX-01 | Transaction cleanup may replace an in-flight exception; rely on canonical errors/correlation, not internal text. |
| QH-TEST-01 | Concurrency-test task draining remains harness follow-up, not a newly resolved production guarantee. |
| CH11-DB-01 | Creator FK remains nullable; no universal ownership repair or frontend reassignment. |
| CH11-DB-02 | Request rules are not all DB checks; use supported APIs, not direct DB writes. |
| CH11-STATE-01 | No global concurrency/authorization-freshness policy; scoped locks do not imply universal ETags/stale-write protection. |
| CH11-FILE-01 | Physical-file deletion has no durable retry; HTTP success does not guarantee storage cleanup. |
| QH-TEST-02 | Raw SQL deterministic test setup remains test debt, not a client API. |
| C12-CONFIG-01 | Production JWT placeholder/configuration hardening remains a deployment blocker; suite success is not deployment readiness. |
| CH13-J2-DEPLOY-01 | First-deployment Active-location target-zero check remains required; remediation uses supported lifecycle/location workflows. |
| CH13-PERF-01 | Long-lived translation-guard write amplification needs operational review; no authoring-load performance promise. |

Still deferred: broader auth lifecycle, automatic invitation delivery, subtype-aware valuation/comparables, product-derived market distributions, future approved Commercial/Land attributes, broader agency subtype vocabulary, search expansion, PostGIS/radius/polygon/viewport search, canonical geography IDs, clustering and public-pin privacy. No payments/subscriptions, arbitrary status transitions or frontend capabilities are implied by enums or chapter completion.

This reconciles the backend for frontend implementation; it does not certify an existing client, resolve the quality register or declare deployment readiness. Historical snapshots remain evidence of their own scope. Future changes need their own approved contract and verification.
