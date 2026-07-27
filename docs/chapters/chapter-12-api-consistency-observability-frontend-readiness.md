# Chapter 12 — API Consistency, Observability, and Frontend Readiness

## 1. Status

**Status: planned; not started.**

| Item | Authoritative Chapter 12 baseline |
|---|---|
| Prerequisite | Chapter 11 is complete on `development` at merge `4ec8271`. Chapters 9–11 are present in first-parent history and Chapter 12 is the next locked roadmap chapter. |
| Tests | The committed Chapter 11 closeout records **704/704 passing**, 0 failed, and 0 skipped. This planning-only task verified that the closeout is current at `HEAD`; it did not rerun the suite. Chapter 12 must establish a new actual total. |
| Database | There are **15 committed migrations**, from `20260610042853_AddListingTables` through `20260721112146_AddListingTranslationQTrigramIndex`. The Chapter 11 closeout records a clean PostgreSQL 16 zero-to-latest migration, empty repeat update, valid catalog checks, and no pending model changes. No Chapter 12 checkpoint is expected to add a migration. |
| Implemented domains | Authentication/users, user avatars, listings and multilingual discovery, listing images, agencies, memberships, invitations, agency dashboards, and platform-admin agency transitions. |
| Intended outcome | Turn the existing feature-complete backend into one predictable frontend integration boundary: stable success DTOs, one documented failure contract, deliberate conflict semantics, diagnosable requests, truthful health/readiness, one pagination contract, reliable local media delivery, accurate OpenAPI, and environment-driven frontend configuration. |
| Next product phase | Begin frontend development after Chapter 12 closes. Chapter 12 is a frontend-development readiness gate, not a claim that every production deployment, account-security, media-durability, or monitoring concern is complete. |

## 2. Problem statement

### API contract inconsistency

The five controllers do not expose one failure contract. `AuthController` and `UsersController` commonly return anonymous `{ message }` objects; `ListingsController`, `AgenciesController`, and `AdminAgenciesController` commonly return raw strings; authorization failures use empty `Forbid()` responses; and `[ApiController]` model-binding failures remain framework-defined. `ServiceResult<T>` has no conflict outcome or stable error identifier. Successful DTOs and success status codes are already coherent enough for a frontend and do not need envelopes or redesign.

### Exception and conflict translation

`Program.cs` has no `AddProblemDetails`, `IExceptionHandler`, or application-owned unexpected-exception boundary. Storage and persistence failures therefore have environment-dependent HTTP behavior. Registration and agency creation precheck normalized email/slug uniqueness but do not translate the exact PostgreSQL race loser, despite committed unique indexes `IX_Users_NormalizedEmail` and `IX_Agencies_Slug`. Existing application-state rejections are mostly `400`, while duplicate registration is `409`.

### Frontend usability

The same pagination JSON is represented by `PagedResponse<T>` and `PagedResult<T>`, producing different OpenAPI schemas. Private paged listing queries lack the public query's `Id` tie-breaker. JWT challenge/forbid bodies, stale principals, file validation, and invitation effective expiry are inconsistent. The Swagger document applies Bearer security globally even though auth, public discovery, public agencies, and health endpoints are anonymous.

### Operational diagnostics

Default host logging is configured, but production code has no deliberate structured request log, log scope, exception owner, or response correlation identifier. `GET /api/health/database` returns HTTP `200` even when its body says unavailable. There are no health, logging, trace, OpenAPI, CORS, or static-file HTTP integration tests.

### Configuration gaps

The four development frontend origins are hard-coded in `Program.cs`; static files execute before CORS. Only `src/RealEstate.Api/wwroot/.gitkeep` is tracked, while runtime storage targets `wwwroot/uploads`; ignored local directories can therefore hide clean-checkout static-media failure. The base `appsettings.json` JWT placeholder is also a verified deployment-safety gap, but account/security configuration is assigned to Chapter 13 or deployment work and is explicitly retained rather than made a Chapter 12 frontend blocker.

### Presentation-only inconsistencies

`GetAgencyInvitationsHandler` maps and filters stored invitation status. An elapsed stored `Pending` row can therefore be returned as Pending while the dashboard correctly excludes it from actionable pending counts. This is a read-contract mismatch, not a need for write-on-read or a background job.

These weaknesses are real but bounded. They do not invalidate Chapter 11's data-integrity work or the existing success APIs, and they do not justify a general backend rewrite.

## 3. Authoritative decisions

| Area | Locked decision and repository fit | Compatibility effect | Rejected alternatives | Required proof |
|---|---|---|---|---|
| Success responses | Preserve current routes, success status codes, DTOs, enum-as-string serialization, `Location` behavior, `204` empty bodies, and API-relative media URLs. Do not add a success envelope. | No intended success-wire break. | A universal `{ data, metadata }` wrapper; controller or DTO rewrite. | Representative success snapshots before/after each domain checkpoint and full regression. |
| Canonical failure format | Adopt ASP.NET Core `ProblemDetails`/`ValidationProblemDetails` as an RFC 7807-compatible contract for non-success `/api` application/framework failures. The explicitly defined health/readiness JSON is the only `/api` exception. Every ProblemDetails body has `type`, `title`, `status`, `detail`, `instance`, `code`, and `traceId`; validation also has `errors`. Content type is `application/problem+json`. | Intentional breaking replacement of raw strings, `{message}`, empty API challenge/forbid bodies, and framework-default validation/500 bodies. | Preserve current shapes; a simple `{message}` object; dual legacy fields. They cannot provide field errors, stable codes, trace linkage, and accurate OpenAPI without permanent parallel contracts. | Exact status, media type, members, code, and trace assertions for framework- and application-generated failures in every domain; exact dedicated health JSON proof. |
| RFC 7807 identity | `type` is `urn:realestate:error:{code}`. `code` is the client machine identifier; English `title`/`detail` are not branching keys. `instance` is the request path without query string. | Additive within the new failure contract. | `about:blank` as the only type; exception-class or documentation-URL identifiers. | Contract tests assert deterministic type/code pairing and no query leakage. |
| Validation | All `400` request/model/form validation uses ValidationProblemDetails. `errors` is an object of JSON-facing field names to string arrays. Use `request` for cross-field/root rules and `file` for upload rules. Existing application validators may keep first-error behavior; framework model state may expose multiple errors. | Failure-body break; safe existing messages move into `errors`. No validation rule is broadened merely for normalization. | Flatten framework validation to one message; aggregate/rewrite all validators; add FluentValidation. | Malformed JSON, missing body, invalid enum/number, handler validation, query validation, and multipart validation. |
| Machine codes | Use a closed, source-controlled, lower-case dotted catalog; never derive a code from an English message or exception. The canonical catalog is defined in section 4. A new semantic category requires plan/review approval and tests. | New stable frontend branching surface. Message wording is explicitly less stable than code/status. | Free-form codes; exception type names; localized messages as identifiers. | Every documented failure path maps to a catalog constant; OpenAPI lists the catalog and tests assert representative codes. |
| `400` versus `409` | `400` is for malformed, missing, unsupported, or intrinsically invalid request input. `409` is for a well-formed request blocked by current persisted state, a capacity/set conflict, or an expected uniqueness collision. Existing idempotent successes stay successful. | Intentional status changes include agency slug duplication, invalid listing/admin transitions, invitation lifecycle/current-membership conflicts, last-owner/member-state protection, image capacity/set-change conflicts, and their deterministic race losers. | Preserve all historical `400`s; map every `DbUpdateException` to `409`; introduce `412`/ETags without a version contract. | One sequential and, where applicable, concurrent test per conflict class; current state/invariant assertions retained. |
| Expected unique races | Translate only PostgreSQL `23505` for exact constraint `IX_Users_NormalizedEmail` or `IX_Agencies_Slug` inside Infrastructure. Return provider-neutral outcomes; clear unusable tracked state as needed; rethrow every other database failure. Registration and slug are separate checkpoints. | Sequential and race-time duplicates converge on the same `409`, code, and safe detail. Agency slug sequential status changes from `400` to `409`. | Global database exception classifier; Npgsql types in Application/API; sharing one cross-domain repository abstraction. | Controlled separate-context PostgreSQL races, exactly one row/success, deliberate loser contract, and unrelated constraint/failure passthrough. |
| Application results | Extend the existing explicit result style with a conflict outcome and stable error-code value. Do not replace CQRS-lite handlers, use exceptions for expected failures, or infer status/code by message matching. Shared API mapping handles failures; controllers retain success-specific mapping. | Internal compile-time change; public effect is the locked failure contract. | New CQRS framework; broad generic controller/action rewrite; exception-driven business flow. | Unit tests for result-to-HTTP mapping plus domain integration tests. |
| Authentication challenge | Missing, malformed, expired, or otherwise rejected Bearer authentication returns canonical `401` with `authentication.required`; preserve `WWW-Authenticate: Bearer` and suppress token-validation detail. A protected endpoint's `authentication.invalid_principal` 401 also supplies the Bearer challenge. Login credential failure returns `authentication.invalid_credentials` without a Bearer challenge because it is the credential-acquisition operation. | Empty/framework challenge bodies become ProblemDetails. | Redirects; `403` for bad authentication; leaking token-validation detail; challenging the login endpoint itself. | Missing/malformed/expired/stale token tests, exact header presence/absence, and generic body assertions. |
| Authorization | An existing authenticated principal that lacks permission receives canonical `403 authorization.forbidden`. A known Disabled user receives `403 authorization.account_disabled` on operations the existing rules disallow. Forbidden details stay generic and do not reveal hidden resources or role checks. | Empty `Forbid()` bodies become ProblemDetails. Existing permission outcomes remain `403`. | Expose current discarded permission strings; convert permission failures to `404` or `401` globally. | Owner/agent/admin/non-member and Disabled-user tests across controllers. |
| Disabled users | Preserve the established lifecycle: Disabled and PendingVerification users may authenticate; Disabled users may read `GET /api/users/me`; existing disallowed mutations/workspace operations remain `403`. Login blocking, token revocation, and account-security redesign are Chapter 13 work. | No success-policy break. Failure body changes only. | Deny Disabled login in Chapter 12; revoke existing tokens; block `/me`. | Explicit login and `/me` tests for Disabled/PendingVerification plus mutation `403` tests. |
| Stale principals | A valid token with a missing/non-Guid subject or no corresponding user row is `401 authentication.invalid_principal` on every user-dependent protected endpoint. It is not `403`, `200` empty data, or `500`. This explicitly supersedes only Chapter 11 11D's missing-listing-image-actor HTTP classification while preserving its no-mutation guarantee. It does not create global in-flight authorization freshness. | Intentional changes: four Chapter 11 image missing-actor tests move `403` to `401`; my listings/agencies no longer return success for a deleted user; throwing paths become `401`. | Treat stale identity as permission failure; silently return data/empty lists; global database-backed JWT validation on every public request. | Cross-domain stale/non-Guid principal tests and unchanged database/filesystem assertions. |
| Unexpected exceptions | Use one built-in ASP.NET Core `IExceptionHandler`/ProblemDetails boundary. Return `500 server.unexpected` with generic detail in every environment; never return exception, SQL, provider, file-path, or connection detail. If the response has started, log but do not promise a replacement body. Request-aborted cancellation is not synthesized as `500`. | Existing test-host exception propagation becomes canonical HTTP `500`. | Developer exception bodies as a public contract; catch-all controller blocks; generic database-to-conflict mapping. | Injected handler/storage/persistence failures, no detail leakage, rollback state, started/cancel behavior where practical. |
| Request identifier | Use server-issued `HttpContext.TraceIdentifier`. Return it as `X-Request-ID` on all API/health responses and served media, as `traceId` in every ProblemDetails body, and in the log scope. Do not trust or adopt client-supplied `X-Request-ID` in Chapter 12. Expose the response header through CORS. | Additive response header and failure-body field. | New GUID unrelated to host tracing; accepting arbitrary client correlation; vendor tracing dependency. | Header/body/log identity; client header cannot overwrite; success and failure coverage. |
| Structured logging | Keep built-in `Microsoft.Extensions.Logging`. Emit one Information completion event per API request with request ID, method, route template (or fixed `unmatched` fallback), status, and elapsed milliseconds. The application exception handler owns one custom Error event for a handled unexpected exception; do not re-enable or add duplicate framework diagnostics. Expected `4xx` outcomes are not Warning/Error events. | Operationally additive; no wire break. | Serilog, OpenTelemetry exporter, metrics platform, per-handler logging campaign, body/header/query logging. | Captured structured properties, exactly one application-owned Error for handled exceptions, completion event with final status, and request-secret absence. |
| Health/readiness | Keep anonymous dependency-free `GET /api/health` and its current `200` body. Add anonymous `GET /api/health/readiness` for PostgreSQL only. Retain `/api/health/database` as an alias to the same database readiness semantics. Ready is `200`; false/exception/internal three-second timeout is `503`; client abort produces no synthetic result. Health uses sanitized JSON, not ProblemDetails. A provider-neutral probe contract fronts an Infrastructure implementation. | Liveness success unchanged. `/api/health/database` unavailable changes from `200` to `503`; new readiness route is additive. | Storage readiness; liveness that checks dependencies; health `200` when unavailable; vendor/orchestrator payload; provider code in API. | Live without DB, ready DB, injected false/exception/internal timeout, client abort, anonymous access, sanitized body, request ID. |
| Pagination | `PagedResponse<T>` becomes the only HTTP/Application output schema. Retain `PagedResult<T>` only as the internal repository read carrier; it must not appear in handler/controller/OpenAPI response types. Preserve current body fields, normalization, and metadata formulas; add `Id DESC` tie-breakers to personal and dashboard listing pages. | JSON unchanged. Generated schema name consolidates; private equal-timestamp order becomes deterministic. | Two public aliases; metadata redesign; headers-only metadata. | All four paged endpoints, all metadata fields, normalization/edge pages, equal-timestamp repeat/adjacent-page stability, one OpenAPI schema. |
| Cursor pagination | Explicitly defer cursor pagination, superseding Chapter 10's sentence assigning it to Chapter 12. Current offset pagination is already implemented and sufficient for initial frontend work; no scale/profile evidence justifies sort-specific cursor contracts now. | No current contract change. | Replace offset pagination now; offer both offset and cursor without a product requirement. | Handoff/context record the deferral; no cursor parameter or schema appears in OpenAPI. |
| Invitation expiry | Invitation list responses expose effective status without persisting on read: stored `Pending` with `ExpiresAtUtc <= capturedUtcNow` is returned as `Expired`. Pending/Expired filters use the same effective rule; other statuses remain stored. Create/accept/cancel persistence remains action-triggered. | Intentional presentation/filter change for elapsed stored Pending rows; DTO shape and write semantics unchanged. | Preserve mismatch; write on read; scheduled expiry/background job; add a second status property. | Exact boundary, Pending/Expired filters, unfiltered response, no database mutation, dashboard/replacement regressions. |
| Media/static behavior | Preserve 5 MB and JPG/JPEG/PNG/WEBP rules, API-relative `/uploads/...` URLs, anonymous successful static delivery, and normal file bytes/content type. API upload failures use canonical errors; operational storage failures use generic `500`. Missing `/uploads` assets remain ordinary static `404`, outside the `/api` ProblemDetails contract. | Failure bodies change; successful URLs/files do not. Allowed-origin media CORS is additive. | Absolute host URLs; secure media redesign; turn static 404 into API JSON; claim durable deletion. | Upload-to-GET bytes/content type, allowed/disallowed media CORS, missing static file, injected file failure. |
| CORS | Bind exact origins from `Cors:AllowedOrigins`. Development defaults are the current four localhost origins. Missing production configuration grants no cross-origin access; invalid nonempty origins fail startup. Preserve any method/header, no credentials, and expose `X-Request-ID`. Run CORS before static files and authentication. | Development behavior preserved. Non-Development must configure deployed origins; media now receives permitted CORS headers. | `AllowAnyOrigin`; credentials/cookies; hard-coded deployment origins; wildcard/subdomain parsing. | Allowed/disallowed actual and preflight requests for API and media; missing/invalid config tests. |
| JWT configuration | Do not change JWT secret loading in Chapter 12. The base placeholder is a verified production-deployment blocker, not a failure-contract or initial frontend integration dependency. Record it as retained `C12-CONFIG-01` in the quality handoff for Chapter 13/deployment hardening; Chapter 12 must not claim production readiness. | No Chapter 12 runtime change; current local/test startup remains compatible. | Silently call the placeholder production-safe; expand this chapter into secret validation, tooling, rotation, or revocation. | Closeout adds/retains the handoff item and frontend-readiness wording remains development-focused. |
| OpenAPI | Keep Swagger UI/document middleware Development-only. Remove document-wide Bearer security and derive security per operation. Document exact success/error schemas, codes, request-ID header, pagination, enum strings, multipart media constraints, and health semantics. Replace the stale weather sample in `RealEstate.Api.http`. | Documentation/client-generation correction; no runtime success change. | Expose Swagger in production by default; hand-maintained claims not tested against runtime; generated client as a chapter deliverable. | Structural generated-document assertions and manual Development review. |
| Compatibility policy | Perform one pre-frontend hard cutover for failures; no legacy `message` alias, dual content type, API version, or temporary bridge. Preserve safe English text where practical, but guarantee only status, shape, code, and documented fields. | The listed failure/status/presentation changes are deliberately breaking before any known public frontend client. | Permanent compatibility shim for unknown clients; postpone consistency until after frontend. | Existing tests updated intentionally, success compatibility assertions retained, OpenAPI matches runtime. |
| Frontend gate | Chapter 12 must satisfy section 11's mandatory guarantees before frontend implementation starts. It does not certify production launch, high-scale pagination, full account security, durable media cleanup, or a monitoring platform. | Establishes the first supported frontend contract baseline. | Start frontend against unstable failures; expand Chapter 12 into deployment/security/media programs. | Section 15 completion evidence and documentation closeout. |

## 4. Canonical API contract

### Successful responses

Successful application responses remain unwrapped:

- `200` returns the existing DTO/list/page.
- `201` returns the existing DTO and current `Location` behavior.
- `204` has no body.
- Enum values remain JSON strings.
- Media URLs remain API-relative.
- Health endpoints use the dedicated operational shapes defined below.

No Chapter 12 implementation may opportunistically rename success fields, change route templates, or broaden business visibility.

### Base failure shape

Except for the dedicated health/readiness JSON contract, every non-validation `/api` application/framework failure body is:

```json
{
  "type": "urn:realestate:error:conflict.agency_slug_already_exists",
  "title": "Conflict",
  "status": 409,
  "detail": "An agency with this slug already exists.",
  "instance": "/api/agencies",
  "code": "conflict.agency_slug_already_exists",
  "traceId": "0HN..."
}
```

The exact `detail` must be safe and useful, but clients must not branch on it. `instance` excludes query strings. No timestamp, stack trace, exception type, SQL state, constraint name, filesystem path, or internal identifier is added.

### Validation shape

Every request-validation `400` is:

```json
{
  "type": "urn:realestate:error:validation.failed",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/auth/register",
  "code": "validation.failed",
  "traceId": "0HN...",
  "errors": {
    "email": ["Email is invalid."]
  }
}
```

Framework model-state and application validation converge on this wire shape. Application validators are not required to aggregate beyond their current first failure. A discrete upload rule may use the matching file-specific response code while retaining the same `errors` object.

### Locked status and code catalog

| HTTP | Code | Meaning and required use |
|---:|---|---|
| 400 | `validation.failed` | General JSON/query/form/request validation. |
| 400 | `validation.file_required` | Required upload part absent. |
| 400 | `validation.file_empty` | Supplied upload has no content. |
| 400 | `validation.file_too_large` | Existing 5 MB limit exceeded. |
| 400 | `validation.file_type_not_supported` | Existing extension/content-type allowlist rejected the file. |
| 401 | `authentication.required` | Missing, malformed, expired, or rejected Bearer authentication. |
| 401 | `authentication.invalid_credentials` | Login email/password combination rejected without revealing which part failed. |
| 401 | `authentication.invalid_principal` | Authenticated token subject is unusable or no longer maps to an application user. |
| 403 | `authorization.forbidden` | Existing authenticated user lacks operation permission. |
| 403 | `authorization.account_disabled` | Existing Disabled user is barred by the established rule. |
| 404 | `resource.not_found` | Requested resource absent or deliberately hidden. Safe resource-specific detail is allowed. |
| 405 | `request.method_not_allowed` | `/api` route exists but method is unsupported. |
| 415 | `request.media_type_not_supported` | API content type is unsupported. |
| 409 | `conflict.email_already_exists` | Sequential or race-time normalized-email duplicate. |
| 409 | `conflict.agency_slug_already_exists` | Sequential or race-time agency-slug duplicate. |
| 409 | `conflict.resource_state` | Valid request conflicts with listing, agency, invitation, or membership current state. |
| 409 | `conflict.resource_capacity` | Current aggregate capacity prevents the request, including the listing image cap. |
| 409 | `conflict.resource_set_changed` | A submitted complete set/order no longer matches current aggregate membership. |
| 500 | `server.unexpected` | Unclassified server, database, or storage failure. |

This intentionally small catalog is sufficient for the verified backend. A domain checkpoint may not invent message-derived variants. If implementation proves that two conditions require different frontend action, amend this plan before adding a new code.

### Status behavior

| Situation | Contract |
|---|---|
| Not found | `404 resource.not_found`; retain existing concealment rules. |
| Unauthenticated | `401`; Bearer challenge retains `WWW-Authenticate`. |
| Forbidden | `403`; generic detail, with Disabled differentiated only by code. |
| Business rule independent of stored state | Validation `400`. |
| Current-state/capacity/set/unique conflict | `409` with the matching conflict code. |
| Unexpected failure | Generic `500 server.unexpected`; exception logged once. |
| Aborted request | No synthetic `500` or readiness `503`; response may be incomplete/host-managed. |
| Unmatched `/api` route, empty 405/415 | API-only fallback writes canonical ProblemDetails. |
| Missing `/uploads` file | Ordinary static `404`; no API body. |
| Database readiness unavailable | Sanitized health JSON with `503`; intentional health-contract exception. |

### Framework/application convergence

- `ApiBehaviorOptions.InvalidModelStateResponseFactory` writes the automatic model-validation contract.
- Bearer challenge/forbid events delegate to the same error writer and do not lose `WWW-Authenticate`.
- A shared API factory/writer converts application failure descriptors and custom auth/image result enums; success mapping remains in controllers.
- API-only empty-status handling covers routing/method/media-type failures without rewriting existing bodies or `/uploads`, and preserves protocol headers such as `Allow` on `405`.
- One exception handler writes the unexpected contract when possible and owns the exception Error log.
- OpenAPI uses concrete schemas that expose `code`, `traceId`, and `errors`; dynamic extension fields may not be invisible to generated documentation.

## 5. Compatibility strategy

### Preserved

- All successful routes, status codes, DTO property names/types, enum strings, list shapes, and `204` bodies.
- Search/filter parameter names, defaults, normalization, visibility, translation, ordering, and comparable-listing semantics.
- The existing pagination JSON field names and formulas.
- Bearer authentication, token payload/lifetime, Disabled `/me`, and existing permission/business rules.
- Agency workspace success APIs, invitation write lifecycle, media size/type rules, public relative URLs, and anonymous static media.
- `/api/health` success body and `/api/health/database` success body.

### Intentional breaking changes

- Every handled API failure body/content type moves to ProblemDetails.
- State/capacity/set conflicts listed in section 3 move from `400` to `409`.
- Duplicate agency slug moves from `400` to `409`; race losers for email/slug become deliberate `409`s.
- A stale/deleted/non-Guid current principal becomes `401` consistently, including the Chapter 11 image paths previously locked as `403`.
- An elapsed stored Pending invitation is presented/filtered as Expired.
- Database-readiness failure becomes `503` rather than `200`.
- OpenAPI schema names consolidate to `PagedResponse` and security is corrected per operation.
- My-listings and agency-dashboard rows with equal `CreatedAtUtc` gain an `Id DESC` tie-breaker; this is a deterministic ordering correction for ties only.
- Non-Development frontend origins must be configured explicitly.

### Cutover policy

There is no known deployed frontend, generated client, or public compatibility promise. Chapter 12 therefore makes one atomic pre-frontend failure-contract cutover and does not retain `{message}`, raw-string, or dual-format aliases. Existing safe English messages should remain discoverable in `detail` or `errors` where that does not leak authorization/provider detail, but exact message wording is not a stable client contract.

Tests and OpenAPI change in the same checkpoint as runtime behavior. Before Chapter 12 closes, only the established success contracts are treated as stable; after closeout, section 11 becomes the supported frontend baseline.

## 6. Chapter scope

| Included area | Verified gap | Locked outcome | Affected layers | Migration | Dependency | Value |
|---|---|---|---|---|---|---|
| Error/result foundation | Multiple body shapes; no conflict/code model | Shared RFC 7807 writer, catalog, validation convergence, conflict-capable result, API-only status fallback | API; small Application common change | None expected | None | One frontend parser |
| Trace identity | No response/log correlation | Server request ID in header/body/scope | API middleware/error writer | None | Error foundation | Supportable failures |
| Exception boundary | Unstable 500 behavior | Generic 500 and exactly-once exception log | API; test injection | None | Error foundation | Safe operational diagnostics |
| Request logging | No deliberate structured request event | Minimal completion event and redaction boundary | API/configuration | None | Request ID | Request-level diagnosis |
| Auth/user consistency | Framework challenge/empty forbid/stale principal differences | Canonical 401/403; current Disabled policy retained; stale identity normalized | API and affected Application handlers/checkers | None | Error foundation | Predictable auth UX |
| Listing API normalization | Raw listing failures; state `400`; stale-principal divergence | Listing core adopts canonical status/code policy without discovery changes | API and listing query/transition handlers | None | Foundation/auth | Predictable listing UX |
| Listing-image/media normalization | Empty/raw media failures; missing actor 403; cap/set `400`; unstable 500 | Four image mutations adopt canonical media/auth/conflict/500 policy | API and explicit image results/handlers | None | Listing boundary; exception boundary | Predictable image UX |
| Agency core/logo normalization | Raw failures, stale workspace users, slug `400`, media inconsistencies | Core/workspace/logo endpoints adopt canonical policy | API and agency core/access/logo handlers | None | Foundation/auth | Predictable workspace UX |
| Member/invitation normalization | Empty/raw permission and lifecycle failures | Member/invitation actions use canonical auth/resource/conflict policy | API and member/invitation results/handlers | None | Foundation/auth | Predictable collaboration UX |
| Admin normalization | Controller-private raw mapper and transition `400`s | Admin transitions use shared canonical policy | API and admin access/transition handlers | None | Foundation/auth | Predictable admin UX |
| Email/slug uniqueness races | Precheck can lose at exact unique index | Two narrow provider-neutral translations | Application repository contracts; Infrastructure repositories | None | Domain mappings | No expected race-time 500 |
| Pagination | Two public schemas; untested metadata; private ties | One HTTP schema, unchanged metadata, deterministic ties | Application; Infrastructure query ordering; API annotations | None | Foundation; before OpenAPI | Stable page consumption |
| Invitation expiry presentation | List/dashboard time semantics differ | Effective read status and filters, no write on read | Application mapping/query; Infrastructure filtering as needed | None | Agency contract | Consistent workspace UI |
| Health/readiness | DB unavailable still HTTP 200 | Liveness plus DB-only readiness/alias with 503 | Provider-neutral probe contract; Infrastructure implementation; API mapping | None | Error/request ID/logging | Truthful probes |
| CORS/static media | Hard-coded origins; CORS after static; typoed tracked root | Configured allowlist, correct pipeline/root, upload-to-GET proof | API/config/storage test host | None | Response-header decision | Browser integration |
| OpenAPI/developer surface | Global Bearer; undocumented errors/media/pages; stale `.http` | Runtime-accurate per-operation document and current samples | API metadata/config/sample | None | All runtime contracts | Frontend discoverability |
| Final reconciliation | Handoff/context still describe pre-Chapter 12 state | Actual results and retained limitations recorded | Documentation only | None | All checkpoints | Auditable handoff to frontend |

## 7. Explicit deferrals and non-goals

| Deferred/rejected item | Disposition and reason | Later destination |
|---|---|---|
| Cursor pagination | Deferred despite Chapter 10's direct Chapter 12 assignment. Offset pagination is implemented, deterministic after 12K, and adequate without scale/UX evidence. Cursor design is sort-specific and breaking. | Later search/scaling chapter after frontend evidence. |
| Broad optimistic concurrency and in-flight authorization freshness (`CH11-STATE-01`) | Retained. Chapter 12 maps known outcomes and validates the request principal; it does not add tokens or a global freshness framework. | Owner-approved per-aggregate work. |
| Required listing creator and blanket database checks (`CH11-DB-01`, `CH11-DB-02`) | Retained. Both require deployed-data audit, policy, and migrations; neither is an API-contract dependency. | Separate owner-approved data/migration checkpoints. |
| Durable post-commit file deletion (`CH11-FILE-01`) | Retained. Generic 500/log/trace makes failure diagnosable but cannot make cross-resource deletion recoverable. | Chapter 14 or separate media reconciliation/outbox work. |
| Storage readiness probe | Rejected for Chapter 12. A truthful local write probe has side effects and the service can serve non-media APIs while storage is impaired. | Reassess with deployment/object-storage architecture. |
| Cloud/object storage, CDN, absolute media URLs, cache policy | Local storage and relative URLs remain accepted for frontend development. | Deployment/media chapter. |
| File signatures, malware scanning, image transformation | No verified product requirement; do not hide security/media platform work inside response cleanup. | Later media/security work. |
| Background invitation expiry or write-on-read | Effective presentation solves the frontend inconsistency without mutation. | Chapter 14 if persisted expiry processing becomes necessary. |
| Invitation email delivery/notification jobs | Not an API-consistency prerequisite. | Chapter 14. |
| Refresh tokens, logout/revocation, password reset, email verification, disabled-login blocking, key rotation | Existing auth lifecycle is preserved. | Chapter 13. |
| Base JWT placeholder and production secret validation | Verified deployment blocker, but not required for the current Bearer-header frontend integration contract. It is not silently certified by Chapter 12. | Chapter 13 or explicit deployment-hardening checkpoint before production launch. |
| Vendor logging, metrics, traces, dashboards, alerts, audit trail | Chapter 12 provides structured logs and IDs only. No deployment target or vendor is chosen. | Deployment/operations work. |
| Client-supplied correlation ID | Server ID is sufficient and avoids trust/length/character policy now. | Later distributed-tracing requirement. |
| Transaction-cleanup exception preservation | The handoff issue predates the global boundary; once cleanup replaces an exception, middleware cannot recover it. Do not expand 12B into transaction refactoring. | Related future transaction hardening. |
| Avatar/logo/listing cross-media compensation redesign | Chapter 12 governs public failure translation and preserves existing Chapter 11 guarantees; it does not redesign every cleanup ordering edge. | Focused media hardening if evidence warrants. |
| Concurrency-test task draining and test-only raw SQL cleanup | Low-priority test hygiene unrelated to the Chapter 12 contract. | Related test maintenance. |
| Success envelopes, API versioning, HAL/JSON:API, generic repository, UnitOfWork, MediatR, AutoMapper, FluentValidation | Rejected as unnecessary architecture/contract churn. | None without a new approved requirement. |
| Frontend implementation | Explicit non-goal. | Starts after Chapter 12. |
| Full production-launch certification | TLS/reverse proxy, `AllowedHosts`, secrets manager, rate limiting, account security, durable media, and monitoring remain outside this chapter. | Deployment and later chapters. |

Public agency visibility, role permissions, listing visibility, invitation credentials, search semantics, and other established business decisions remain unchanged unless a checkpoint explicitly names the presentation/status change.

## 8. Dependency order

Implementation is sequential at the repository level: **one checkpoint, one feature branch, one outcome, one reviewable commit, one merge, then the next branch from updated `development`**. No branch is created during this planning task.

```text
12A canonical failure/request-ID foundation
 |
 +-> 12B exception + request logging -> 12C auth/users/avatar/principal policy
 |                                  |   |
 |                                  |   +-> 12D email race
 |                                  |   +-> 12E listing contract -> 12F listing-image/media contract
 |                                  |   +-> 12G agency core/logo contract -> 12J slug race
 |                                  |   +-> 12H membership/invitation contract -> 12L effective expiry
 |                                  |   +-> 12I admin-agency contract
 |                                  |
 |                                  +-> 12M health/readiness
 |
 +-> 12K pagination contract
 |
 +-> 12N CORS/static media

12A through 12N -> 12O OpenAPI/developer surface -> 12P final closeout
```

Foundational error/code/request-ID behavior must precede controller normalization, because adding new application statuses before controllers can map them risks accidental success fall-through. Domain mappings must precede their unique-race checkpoints so sequential and concurrent losers share one already-proven contract. Pagination, invitation expiry, health, and media/CORS must precede OpenAPI so documentation describes runtime rather than predicting it.

After 12A, pagination and CORS are conceptually independent. The auth/user/avatar checkpoint depends on 12B because its required operational avatar failures need the global 500 boundary; domain checkpoints then inherit that boundary through 12C. This repository implements all paths in the written A–P sequence, one at a time. 12D depends on 12C; 12F depends on 12E; 12J depends on 12G; 12L depends on 12H; 12M depends on 12B; 12O depends on all runtime checkpoints; 12P depends on everything.

## 9. Checkpoint plan

### 12A — Canonical API failure and request-identifier foundation

#### Outcome

One shared API boundary can express every locked failure shape/code and correlate it with a server request ID without changing a success response.

#### Included findings

`C12-ERR-01`, `C12-VAL-01`, the foundation of `C12-TRACE-01`, and the result-model prerequisite for `C12-CONFLICT-02`.

#### Required behaviour

- Add the RFC 7807 factory/writer, closed code constants, and concrete OpenAPI-visible error models.
- Add `X-Request-ID` from `HttpContext.TraceIdentifier` and `traceId` in errors; ignore a client value.
- Converge automatic model validation and empty `/api` 404/405/415 responses; exclude `/uploads`.
- Extend the explicit application result contract with Conflict/error code without rewriting handlers.
- Preserve every success response byte shape and status.

#### Likely production areas

`RealEstate.Api/Program.cs`; a small `RealEstate.Api` error/request-context area; `RealEstate.Application/Common/ServiceResult.cs`; API registration/conventions. Existing controllers are used for probes, not bulk-converted here.

#### Test strategy

Focused integration tests for malformed JSON, invalid query binding, missing body, API 404/405/415, content type, type/code/trace/header identity, client header overwrite rejection, and one unchanged success response. Unit tests for failure mapping/result construction. Negative test that `/uploads/missing` is not rewritten.

#### Migration impact

None expected. Any model change stops the checkpoint.

#### Risks

Double-writing responses, losing `WWW-Authenticate` later, leaking query strings, rewriting static/health bodies, or introducing a generic controller framework.

#### Dependency

None beyond Chapter 11 closeout.

#### Completion criteria

The exact section 4 base/validation shapes are proven for framework paths; request IDs match; success compatibility passes; build, focused tests, full suite, pending-model, and diff checks pass.

#### Estimated size

Medium.

### 12B — Unexpected-exception and structured request logging boundary

#### Outcome

Every completed API request has one minimal structured completion event, and every unexpected exception has one sanitized response and one owning Error event.

#### Included findings

`C12-EXC-01`, `C12-OBS-01`, and completion of `C12-TRACE-01`.

#### Required behaviour

- Register one built-in exception handler using the 12A writer.
- Order middleware as request ID -> completion logging -> exception handling -> CORS/static/auth/endpoints so the completion event observes the final handled status.
- Log Information completion with request ID, method, route template, status, and elapsed milliseconds. Use a fixed `unmatched` route value when endpoint metadata is absent; never fall back to raw path or query.
- Log a handled unexpected exception once at Error with the same request context; lower layers do not duplicate it and framework handled-exception diagnostics are not re-enabled. Framework logging remains the fallback for a response-started/unhandled exception where no replacement body is promised.
- Do not add request/response bodies, headers, query strings, emails, passwords, JWTs, invitation token/code, original filenames/content, SQL/parameters, or connection strings as log properties.
- Keep EF sensitive-data logging and Npgsql detailed-error opt-in disabled. The internal owning Error event may include the thrown exception's type, message, and stack; the public response never does. Tests seed recognizable request secrets and prove those do not appear, rather than asserting that every possible provider/IO exception string is detail-free.
- Treat request-aborted cancellation as cancellation, not a synthetic 500/Error.

#### Likely production areas

`Program.cs`; small middleware and `IExceptionHandler` components; logging configuration. Test host receives a capturing provider and deterministic throwing replacements.

#### Test strategy

Injected application, database, and storage failures; exact public `500 server.unexpected` with no exception text; one application-owned Error for a handled exception; one completion event with final 500; structured keys; matching IDs; cancellation case; rollback/state proof for existing injected image mutation failures.

#### Migration impact

None expected.

#### Risks

Sensitive logs, high-cardinality paths, duplicate exception events, turning cancellation into an error, or swallowing a failure after headers start.

#### Dependency

12A.

#### Completion criteria

Error body/header/log identity and exactly-once ownership are proven; sensitive test values are absent; existing rollback assertions remain; build/full suite/model/diff gates pass.

#### Estimated size

Medium.

### 12C — Authentication, authorization, users, and principal consistency

#### Outcome

The identity boundary has one predictable 401/403 contract while preserving the established Disabled/PendingVerification lifecycle.

#### Included findings

`C12-AUTH-01`, Auth/Users portion of `C12-ERR-01` and `C12-VAL-01`, avatar portion of `C12-MEDIA-01`, and verified stale-principal inconsistencies.

#### Required behaviour

- Configure Bearer challenge/forbid to use the 12A writer and retain `WWW-Authenticate`.
- Normalize `AuthController` and `UsersController`; invalid credentials remain indistinguishable.
- Protected-endpoint stale-principal 401 responses include `WWW-Authenticate: Bearer`; login's `authentication.invalid_credentials` 401 does not.
- Preserve Disabled/PendingVerification login, Disabled `/me` success, and Disabled disallowed-operation `403`.
- Normalize avatar missing/empty/oversized/unsupported input to the locked file-validation codes; unexpected avatar storage/persistence failure flows to 12B.
- Establish `401 authentication.invalid_principal` for unresolved/non-Guid subjects, using reusable application resolution/checking where it reduces duplication without a new authorization framework.
- Domain-specific stale paths are completed in 12E–12I; no handler may return a new result that its current controller cannot map.

#### Likely production areas

`Program.cs`; `AuthController`; `UsersController`; JWT events/auth result wiring; `CurrentUserService`; user handlers; existing/shared Application current-user checking; `ServiceResult` mapping.

#### Test strategy

Missing, malformed, expired, and valid JWT; invalid login credentials; automatic validation; Disabled and PendingVerification login; Disabled `/me` and mutations; deleted/non-Guid principal; exact ProblemDetails and `WWW-Authenticate`; avatar missing/empty/oversized/unsupported codes; injected avatar storage/persistence 500; unchanged avatar success DTO/file cleanup and other user success DTOs.

#### Migration impact

None expected.

#### Risks

Breaking the foundational PendingVerification test workflow, revealing credential or permission details, returning 403 for stale identity, or adding a database query to anonymous endpoints.

#### Dependency

12A and 12B. Auth normalization uses the 12A contract; avatar operational-failure behavior uses the 12B boundary.

#### Completion criteria

All Auth/User failure paths use the canonical schema/codes, lifecycle behavior is explicitly proven, and all standard checkpoint gates pass.

#### Estimated size

Medium.

### 12D — Registration normalized-email race translation

#### Outcome

Sequential and concurrent duplicate registrations have the same deliberate `409 conflict.email_already_exists` result.

#### Included findings

Registration half of `C12-CONFLICT-01` and the quality-handoff race-time uniqueness issue.

#### Required behaviour

- Retain application precheck for normal UX and PostgreSQL uniqueness as authority.
- Inspect only `23505` plus exact `IX_Users_NormalizedEmail` in Infrastructure.
- Return a provider-neutral duplicate outcome; do not expose Npgsql outside Infrastructure.
- Rethrow unrelated constraints/provider failures and leave the context safe for request disposal.

#### Likely production areas

`RegisterUserHandler`; `IUserRepository`; `UserRepository`; `UserConfiguration` only as constraint-name evidence; Auth contract mapping already established by 12C.

#### Test strategy

Sequential duplicate; barrier-controlled two-request PostgreSQL race using separate scopes; one `201`, one exact `409`, one normalized row; unrelated unique/foreign/provider failure passthrough at the narrow boundary.

#### Migration impact

None expected; the named index already exists. Editing its migration is prohibited.

#### Risks

SQLSTATE-only matching, provider leakage, nondeterministic race tests, swallowing a different unique violation, or combining email/slug plumbing prematurely.

#### Dependency

12C.

#### Completion criteria

Sequential/race losers are identical at HTTP level, exactly one row persists, unrelated failures escape to 12B, and all gates pass.

#### Estimated size

Medium.

### 12E — Listing API failure and lifecycle normalization

#### Outcome

Listing create/read/search/private-list/lifecycle endpoints use the canonical 400/401/403/404/409 contract without changing Chapter 10 discovery or listing success semantics.

#### Included findings

Listing-core portion of `C12-ERR-01`, `C12-CONFLICT-02`, `C12-VAL-01`, and `C12-AUTH-01`.

#### Required behaviour

- Convert listing raw-string/empty failures through the shared writer; retain controller-owned success mapping.
- Keep request/query validation as 400 and map a well-formed publish/unpublish/archive request blocked by current listing state to `409 conflict.resource_state`.
- Normalize stale/deleted/non-Guid actors to `401 authentication.invalid_principal` for my listings and lifecycle actions; existing Disabled/ownership/agency permission failures remain 403.
- Preserve public visibility, all Chapter 10 search/filter/sort/translation/comparables behavior, listing DTOs, and success statuses.
- Do not touch listing-image result families in this checkpoint.

#### Likely production areas

Listing portions of `ListingsController`; `GetMyListingsHandler`; create/publish/unpublish/archive handlers; shared API failure mapping. Listing query repositories change only if required to express stale-principal rejection, not to redesign discovery.

#### Test strategy

Representative handler/model/query validation 400; public/private 404; owner/Disabled 403; stale 401; sequential/current-state 409; exact body/content/header; unchanged create/search/details/comparables/private-page/lifecycle success DTOs and semantics.

#### Migration impact

None expected.

#### Risks

Changing discovery/visibility, turning permission into conflict, adding database work to anonymous reads, or allowing new result statuses to fall through as success.

#### Dependency

12A and 12C. It is sequenced after 12D but independent of registration persistence work.

#### Completion criteria

Every non-image listing failure family has exact representative proof, Chapter 10 behavior is unchanged, and build/full-suite/model/diff gates pass.

#### Estimated size

Medium.

### 12F — Listing-image and media failure normalization

#### Outcome

The four listing-image operations expose one deliberate validation/auth/resource/conflict/500 contract while preserving Chapter 11 mutation, compensation, and file guarantees.

#### Included findings

Listing-image portion of `C12-ERR-01`, `C12-CONFLICT-02`, `C12-AUTH-01`, and `C12-MEDIA-01`.

#### Required behaviour

- Map missing/empty/oversized/unsupported image input to the locked file-validation codes.
- Empty or duplicate reorder IDs are validation 400; after a distinct nonempty request, a locked aggregate-set mismatch is `409 conflict.resource_set_changed`; image cap is `409 conflict.resource_capacity`.
- Missing listing/image is 404; existing non-owner/Disabled actor is 403; stale/deleted/non-Guid actor is 401 in upload/delete/set-primary/reorder.
- Explicitly supersede Chapter 11's four missing-actor 403 expectations while preserving their no-database/no-file-mutation proof.
- Unexpected persistence/storage/post-commit deletion failures flow to 12B; public detail is generic and durable deletion is not claimed.
- Preserve 20-image cap, parent serialization, primary/order invariants, atomic saves, new-file compensation, size/type rules, and success DTOs/statuses.

#### Likely production areas

Image actions in `ListingsController`; listing-image result enums/results and four mutation handlers; shared API mapping. No repository/lock redesign is expected.

#### Test strategy

All file-validation codes; 404/403/stale 401; capacity/set 409 including deterministic Chapter 11 losers; injected storage/persistence 500 with rollback/cleanup; four success contracts; full Chapter 11 image authorization/concurrency/persistence/storage regression.

#### Migration impact

None expected.

#### Risks

Weakening locks/compensation, treating malformed duplicate IDs as conflict, claiming rollback after a durable database mutation, or exposing path/provider detail.

#### Dependency

12A–12C and 12E's listing boundary mapping.

#### Completion criteria

All four operations have exact contract proof, missing actors are 401 with unchanged no-mutation effects, Chapter 11 invariants pass, and all standard gates pass.

#### Estimated size

Medium.

### 12G — Agency core, workspace, and logo failure normalization

#### Outcome

Agency creation/public profile/my-agencies/update/dashboard/logo endpoints share the canonical contract without changing agency visibility, role, or success behavior.

#### Included findings

Agency-core portion of `C12-ERR-01`, `C12-CONFLICT-02`, `C12-VAL-01`, `C12-AUTH-01`, and logo portion of `C12-MEDIA-01`.

#### Required behaviour

- Normalize core/workspace actions in `AgenciesController` while leaving member/invitation actions to 12H and admin actions to 12I.
- Make sequential duplicate slug `409 conflict.agency_slug_already_exists`; 12J owns its race translation.
- Normalize stale users to 401 for my agencies/dashboard/update/logo paths; existing Disabled, owner/agent, and hidden-resource rules remain 403/404.
- Apply the locked file validation codes and generic unexpected-failure boundary to agency logos.
- Preserve public profile/status visibility, public/dashboard listing success, dashboard summary, role permissions, and response DTOs.

#### Likely production areas

Core/workspace/logo actions in `AgenciesController`; create/get/update/dashboard/logo handlers; agency access checkers and shared API failure mapping.

#### Test strategy

Public 404; validation 400; stale 401; owner/agent/Disabled 403; sequential slug 409; logo validation/500; core/dashboard success compatibility; no member/invitation/admin contract assertions in this checkpoint.

#### Migration impact

None expected.

#### Risks

Changing public agency visibility, Manager/Owner permissions, dashboard query semantics, or coupling the slug race implementation into controller normalization.

#### Dependency

12A and 12C. Independent of listing checkpoints, but sequenced after them.

#### Completion criteria

Every agency-core/workspace/logo failure family has exact proof, sequential slug contract is ready for 12J, successes remain compatible, and all gates pass.

#### Estimated size

Medium.

### 12H — Agency member and invitation failure normalization

#### Outcome

Member administration and invitation create/list/accept/cancel operations use the canonical auth/resource/current-state contract while preserving Chapter 11 invariants.

#### Included findings

Member/invitation portion of `C12-ERR-01`, `C12-CONFLICT-02`, `C12-VAL-01`, and `C12-AUTH-01`.

#### Required behaviour

- Normalize only member and invitation actions in `AgenciesController` and their application results.
- Map last-active-owner, incompatible member state, invitation already-pending/terminal/expired/already-member, and deterministic current-state race losers to `409 conflict.resource_state`.
- Keep invalid/missing token or role input as validation 400; unknown/hidden agency/member/invitation as 404; stale identity as 401; existing role/email/Disabled permission denial as 403.
- Retain member-owner serialization, invitation row/agency locking, rollback, uniqueness, and final-state semantics.
- Preserve stored-status invitation presentation until 12L; do not mix the read-status change here.

#### Likely production areas

Member/invitation actions in `AgenciesController`; member/invitation handlers, permission checkers, explicit result mappings. Repository locking/constraint code should not change for status normalization.

#### Test strategy

Representative 400/401/403/404; sequential and controlled Chapter 11 loser 409; exact ProblemDetails; unchanged final membership/invitation/database state; full member/invitation concurrency/persistence regression.

#### Migration impact

None expected.

#### Risks

Changing Owner/Agent/Manager semantics, leaking invitation/email detail, weakening concurrency evidence, or conflating presentation expiry with persisted lifecycle.

#### Dependency

12A and 12C. It may reuse agency access mapping established in 12G but must remain a separate outcome.

#### Completion criteria

All member/invitation failure classes have exact proof, Chapter 11 final-state assertions remain, and all standard gates pass.

#### Estimated size

Medium.

### 12I — Admin agency-transition failure normalization

#### Outcome

Platform-admin agency approve/reject/disable operations expose the canonical auth/resource/current-state contract independently of the larger agency workspace.

#### Included findings

Admin portion of `C12-ERR-01`, `C12-CONFLICT-02`, and `C12-AUTH-01`.

#### Required behaviour

- Replace `AdminAgenciesController`'s private raw-string mapper with the shared failure boundary while preserving success DTO/status.
- Stale admin identity is 401; existing non-Admin/Disabled identity is 403; missing agency is 404; invalid current agency transition is `409 conflict.resource_state`.
- Preserve platform role/status checks and transition business semantics.

#### Likely production areas

`AdminAgenciesController`; `PlatformAdminAccessChecker`; approve/reject/disable handlers only where a typed conflict outcome is required.

#### Test strategy

Missing/stale authentication 401; non-admin/Disabled 403; missing 404; each invalid transition 409; approve/reject/disable success DTO compatibility; exact body/content/header.

#### Migration impact

None expected.

#### Risks

Weakening admin checks, exposing transition internals, or changing state-machine semantics while changing HTTP classification.

#### Dependency

12A and 12C. It is otherwise independent and deliberately separate from 12G/12H.

#### Completion criteria

All three operations and access paths have exact proof, success semantics remain, and all gates pass.

#### Estimated size

Small.

### 12J — Agency-slug race translation

#### Outcome

Sequential and concurrent duplicate agency creation share `409 conflict.agency_slug_already_exists` without provider leakage.

#### Included findings

Agency-slug half of `C12-CONFLICT-01` and the quality-handoff race-time uniqueness issue.

#### Required behaviour

Match only PostgreSQL `23505` plus `IX_Agencies_Slug` in Infrastructure, return a provider-neutral duplicate outcome, and rethrow every unrelated failure. Reuse policy, not a new generic repository or cross-domain save abstraction.

#### Likely production areas

`CreateAgencyHandler`; `IAgencyRepository`; `AgencyRepository`; `AgencyConfiguration` as name evidence; 12G mapping.

#### Test strategy

Sequential duplicate exact 409; deterministic separate-scope PostgreSQL race; one agency/owner membership result; one success and one deliberate conflict; unrelated constraint/provider failure passthrough.

#### Migration impact

None expected; the committed unique index is authoritative.

#### Risks

Same as 12D, plus partial agency/member creation assumptions. Final state must include membership consistency.

#### Dependency

12G. It is deliberately separate from 12D.

#### Completion criteria

Sequential/race contracts and final database state match; unrelated failures reach 12B; all standard gates pass.

#### Estimated size

Medium.

### 12K — Unified pagination and deterministic private pages

#### Outcome

Every paged listing endpoint exposes one unchanged, fully tested `PagedResponse<ListingResponse>` contract with deterministic ordering.

#### Included findings

`C12-PAGE-01`, quality-handoff pagination duplication, and the verified private-page tie risk.

#### Required behaviour

- Use `PagedResponse<T>` for public search, my listings, public agency listings, and dashboard listings.
- Retain `PagedResult<T>` only as the internal repository read carrier; centralize conversion to `PagedResponse<T>` so metadata cannot drift.
- Preserve `page < 1 => 1`, `pageSize < 1 => 20`, `pageSize > 100 => 100`, `totalPages` ceiling/zero, `hasNextPage = page < totalPages`, and `hasPreviousPage = page > 1` even on an empty requested page.
- Return `200` with empty items beyond the last page.
- Add `ThenByDescending(Id)` after `CreatedAtUtc DESC` to my listings and dashboard listings.
- Add no cursor, Link header, or second pagination mode.

#### Likely production areas

`PagedResponse.cs`; `PagedResult.cs`; four listing/agency handlers; `ListingRepository` private ordering; controller response annotations.

#### Test strategy

Four endpoint contract tests for zero, one, exact-full, partial, and beyond-last pages; normalized inputs/cap; all metadata members; equal-timestamp repeat and adjacent pages with no duplicate/omission; success JSON compatibility; OpenAPI proof deferred to 12O.

#### Migration impact

None expected.

#### Risks

Accidental metadata change, exposing repository entities, unstable ties, treating wrapper cleanup as a cursor project, or extreme unrelated pagination redesign.

#### Dependency

12A; sequenced after domain normalization to minimize controller/test churn. Must precede 12O.

#### Completion criteria

Only one HTTP schema remains, formulas are exact across all paths, tie tests are deterministic, and all gates pass.

#### Estimated size

Medium.

### 12L — Effective invitation-expiry presentation

#### Outcome

Invitation list status/filter behavior agrees with time-aware dashboard semantics without changing stored lifecycle on reads.

#### Included findings

`C12-INV-01` and `CH11-STATE-02`.

#### Required behaviour

- Capture UTC once for the query.
- Effective status is Expired iff stored status is Pending and `ExpiresAtUtc <= utcNow`; otherwise it is stored status.
- No status filter returns all rows with effective presentation.
- `status=Pending` returns stored Pending with `ExpiresAtUtc > utcNow`.
- `status=Expired` returns stored Expired plus elapsed stored Pending.
- Accepted/Cancelled filters remain stored equality.
- A read does not persist status; create/accept/cancel and the filtered unique index remain unchanged.

#### Likely production areas

`GetAgencyInvitationsHandler/Query`; `AgencyInvitationMappingExtensions`; `IAgencyInvitationRepository`/`AgencyInvitationRepository` filtering as needed. DTO shape does not change.

#### Test strategy

Past/future/exact-boundary mapping; unfiltered, Pending, Expired, Accepted, Cancelled; one captured time semantics; fresh-context proof that read does not update the row; dashboard, replacement, accept/cancel, and unique-index regressions.

#### Migration impact

None expected.

#### Risks

Writing on read, using different clocks for filter/mapping, changing write lifecycle, or making filter results disagree with displayed status.

#### Dependency

12H. Independent of 12J but sequenced after it.

#### Completion criteria

Effective status/filter parity and no-write behavior are proven; `CH11-STATE-02` is eligible for removal at closeout; all gates pass.

#### Estimated size

Medium.

### 12M — Liveness and PostgreSQL readiness

#### Outcome

Operators and the frontend can distinguish a live process from a database-ready service through truthful, sanitized, correlated endpoints.

#### Included findings

`C12-HEALTH-01`.

#### Required behaviour

- Preserve anonymous `/api/health` `200` and current `{status, app}` success body without a dependency call.
- Add anonymous `/api/health/readiness`; retain `/api/health/database` as an alias.
- Both database routes return the current compatible success body `{ "status": "ok", "database": "PostgreSQL" }` with `200`; false, dependency exception, or the fixed internal three-second timeout returns `{ "status": "unavailable", "database": "PostgreSQL" }` with `503`.
- Link the fixed internal timeout with `HttpContext.RequestAborted` but distinguish their causes: client abort produces no synthetic 503, Warning, or response-body promise; only a completed false/exception/internal-timeout result becomes 503.
- Log a handled false/exception/internal-timeout readiness result at most once as a structured Warning without response/provider/connection detail. Do not route it through generic 500 handling.
- Add no storage probe.

#### Likely production areas

`Program.cs`; a provider-neutral `IDatabaseReadinessProbe` contract available to API; an Infrastructure implementation using `RealEstateDbContext`/provider behavior; dependency registration. The API owns the fixed timeout and HTTP/health JSON mapping; provider types do not cross the Infrastructure boundary.

#### Test strategy

Liveness with database unavailable; readiness ready/unavailable/exception/timeout; exact 200/503 body; anonymous access; request ID; cancellation; no sensitive fields; alias parity.

#### Migration impact

None expected.

#### Risks

Liveness depending on DB, an unbounded probe, always-200 readiness, leaking connection data, marking the whole service unready for optional storage, or double-logging.

#### Dependency

12A and 12B.

#### Completion criteria

All liveness/readiness semantics and sanitization are deterministic and tested; no storage dependency was added; full gates pass.

#### Estimated size

Medium.

### 12N — Configurable CORS and reliable static-media delivery

#### Outcome

A clean checkout can serve returned media URLs to a configured browser frontend while cross-origin access remains explicit and fail-closed.

#### Included findings

`C12-CORS-01`, static-media portion of `C12-MEDIA-01`, and the verified `wwroot`/`wwwroot` repository discrepancy.

#### Required behaviour

- Bind/validate `Cors:AllowedOrigins`; put the current four localhost origins in Development configuration only.
- Exact HTTP/HTTPS origins, trimmed/deduplicated; no wildcards, paths, query, or credentials. Missing production list grants none; invalid nonempty values fail startup.
- Retain any method/header and expose `X-Request-ID`.
- Order request-ID context, CORS, static files, authentication, authorization, and controllers so API and successful `/uploads` responses receive intended headers without authenticating media.
- Track `src/RealEstate.Api/wwwroot/.gitkeep` and remove `src/RealEstate.Api/wwroot/.gitkeep`; do not track uploaded files.
- Preserve API-relative URLs and ordinary missing-static `404`.

#### Likely production areas

`Program.cs`; `appsettings.json`; `appsettings.Development.json`; small validated CORS options if useful; tracked web-root placeholder; storage/static integration test support.

#### Test strategy

Allowed/disallowed API actual request; preflight with Authorization/content type/multipart method; allowed/disallowed static response; upload returns URL then GET yields exact bytes/content type; missing static file; missing/invalid/duplicate config. A disallowed origin is proven by absence of `Access-Control-Allow-Origin`, not HTTP 403. The factory must create a unique pre-existing temporary web root before host build, point storage/static serving beneath it, and clean it in fixture disposal; it must not depend on or delete the repository's existing ignored uploads.

#### Migration impact

None expected.

#### Risks

Accidental `AllowAnyOrigin`, enabling credentials, code-dependent production origins, CORS after static files, committing uploads, or tests passing only because an ignored local directory exists.

#### Dependency

12A for the exposed response header; otherwise independent. Must precede 12O.

#### Completion criteria

Configured development origins retain behavior, nonconfigured origins receive no CORS permission header, isolated clean-checkout media delivery works without repository-local ignored state, Git tracks only the intended root placeholder, and all gates pass.

#### Estimated size

Medium.

### 12O — OpenAPI accuracy and frontend developer surface

#### Outcome

The generated OpenAPI document and checked-in request samples accurately describe the completed Chapter 12 runtime contract.

#### Included findings

`C12-OAS-01`, OpenAPI implications of every preceding candidate, and stale `RealEstate.Api.http`.

#### Required behaviour

- Keep Swagger middleware Development-only.
- Remove global Bearer requirement; mark security from actual endpoint authorization metadata.
- Public auth, discovery, public agency, static-media-independent health operations show no Bearer requirement; protected operations do.
- Document actual success schemas/statuses, canonical error and validation schemas, relevant 400/401/403/404/409/500 responses, the closed `code` catalog, `X-Request-ID` as a response header, pagination fields/defaults/caps, string enums, multipart field/size/type constraints, and health 200/503 semantics.
- Ensure only `PagedResponse` appears as the HTTP pagination schema.
- Do not list `/uploads` as an API operation; document returned relative media URLs/upload constraints at the owning operations.
- Replace the weatherforecast sample with current health/auth/listing examples that contain no real secrets.

#### Likely production areas

`Program.cs`; operation/schema filters or conventions; controller metadata; API error models; `RealEstate.Api.http`.

#### Test strategy

Resolve `ISwaggerProvider` from `CustomWebApplicationFactory.Services` in Testing and generate the document without enabling Swagger HTTP middleware there. Assert representative public/protected security, errors and the `X-Request-ID` **response** header, pagination, enum strings, multipart, and health operations. Avoid a brittle full-document snapshot. Manually inspect Swagger UI in Development.

#### Migration impact

None expected.

#### Risks

Documenting behavior not implemented, global security persisting, extension fields missing from schema, production Swagger exposure, or overpromising generated-client stability.

#### Dependency

12A–12N.

#### Completion criteria

Structural assertions match runtime, samples work locally, public/protected operations are correct, no duplicate page schema remains, and all standard gates pass.

#### Estimated size

Medium.

### 12P — Final verification and documentation closeout

#### Outcome

Independently prove the complete Chapter 12 contract, reconcile permanent documentation/handoff, and leave `development` ready for frontend work.

#### Included findings

All Chapter 12 findings; all retained/removed dispositions in section 10; stale backend-context statements identified during planning.

#### Required behaviour

- Run section 14 in full against PostgreSQL and a clean media root.
- Record actual build/test/migration/model/OpenAPI/config results; do not retain 704 as the final assumed total.
- Update only the expected closeout documents in section 16 with proven outcomes.
- Remove resolved handoff entries only after their final proof; retain owner decisions/limitations verbatim in substance.
- Correct backend-context's stale statement that listing-image mutations do not reload/check user status and record the new frontend contract.
- Perform no implementation fixes inside closeout; a failed gate returns to a focused implementation branch/checkpoint.

#### Likely production areas

None. Documentation only: this chapter document, `docs/backend-context.md`, and `docs/backend-quality-handoff.md`.

#### Test strategy

All focused contract groups plus full suite, fresh database, repeat migration, pending model, OpenAPI/CORS/media/health/log verification, and Git checks.

#### Migration impact

None expected. The authoritative expected count remains 15; any additional migration requires a prior amended implementation checkpoint.

#### Risks

Closing findings from generic tests, documenting anticipated instead of actual totals, silent model drift, stale planning artifacts mistaken for production docs, or mixing a late fix into closeout.

#### Dependency

12A–12O merged.

#### Completion criteria

Every section 15 rule is objectively satisfied; the three permanent documents agree; final Git state contains only the intended closeout commit and expressly retained untracked planning directory state.

#### Estimated size

Medium.

## 10. Quality-handoff reconciliation plan

The IDs below preserve existing IDs where present and add neutral planning IDs where the handoff has none. `C12-CONFIG-01` is a newly verified planning finding that must be added as a retained deployment blocker at closeout. The real handoff is not modified until 12P.

| ID | Current issue | Chapter 12 checkpoint | Expected disposition | Proof required | Closeout action |
|---|---|---|---|---|---|
| `QH-TX-01` | Transaction cleanup can replace an in-flight exception | 12B observes only; 12P reviews | Defer; global logging cannot recover an exception already replaced by rollback/disposal | Confirm no claim of original-exception preservation; generic 500/log tests | Retain |
| `QH-TEST-01` | Concurrency-test request tasks are not drained after orchestration failure | None; 12P reviews | Defer test-failure hygiene unless the exact helper is changed | Existing bounded timeout remains; no Chapter 12 claim | Retain |
| `CH11-DB-01` | Listing creator relationship remains nullable | None | Defer owner-approved data audit/migration | No model/migration change; pending model clean | Retain |
| `CH11-DB-02` | Request validation is not duplicated broadly as database checks | None | Defer owner-approved constraint work | No blanket constraints/migration; pending model clean | Retain |
| `CH11-STATE-01` | Broad concurrency and authorization freshness remain undefined | 12C, 12E–12I normalize request-time identity; 12D/12J translate two named races; 12P reviews | Retain broad issue | Tests prove only request-time principal classification and known conflicts; no global token/concurrency framework | Retain |
| `CH11-FILE-01` | Post-commit physical deletion is not durably recoverable | 12B/12C/12F/12G improve public diagnostics only | Retain accepted limitation | Generic 500/log/trace proven; no durable intent/retry claim | Retain |
| `QH-TEST-02` | Deterministic listing setup uses raw SQL | None | Defer low-priority test cleanup | No opportunistic fixture rewrite | Retain |
| `CH11-STATE-02` | Invitation expiry can remain status-stale until touched | 12L | Resolve presentation inconsistency with effective read status/filter, while retaining action-triggered storage as documented behavior | Boundary/filter/no-write/dashboard/replacement tests | Remove; record lasting rule in backend-context |
| `CH11-API-01` | Race-time unique conflicts are not translated consistently | 12D and 12J | Resolve both email and slug paths | Sequential + deterministic race + unrelated-failure proof for both named constraints | Remove only after both pass |
| `CH11-API-02` | API error response shapes are inconsistent | 12A, 12B, 12C, 12E–12I; documented in 12O | Resolve across framework, unexpected failures, and all controller domains | Exact 400/401/403/404/409/500 body/content/code/trace coverage and OpenAPI | Remove |
| `CH11-API-03` | Pagination contracts are duplicated | 12K; documented in 12O | Resolve public contract duplication; retain offset pagination | Four endpoints, metadata/ties, one OpenAPI schema | Remove |
| `C12-CONFIG-01` | Base `appsettings.json` supplies the local JWT placeholder to non-Development hosts | 12P documentation only | Defer secret validation/relocation; treat as a production-deployment blocker, not a frontend-development blocker | Confirm Chapter 12 made no secret claim/change and backend context/handoff identify Chapter 13/deployment destination | Add and retain |

Chapter 12 must not remove a retained entry merely because errors are now logged. Closeout may improve wording to reflect the narrow stale-principal/conflict guarantees, but `CH11-STATE-01` and `CH11-FILE-01` remain substantively open.

## 11. Frontend-readiness contract

### Mandatory guarantees after Chapter 12

- **Success DTO stability:** current endpoint routes, success statuses, DTOs, string enums, and relative media URLs are the supported baseline. No generic success wrapper exists.
- **Predictable failures:** every `/api` application/framework failure covered in section 4 uses `application/problem+json`, stable status/code, and a trace ID. Health JSON is the documented operational exception; `/uploads` remains static.
- **Authentication:** missing/invalid authentication is 401 with Bearer challenge; invalid login credentials are indistinguishable; stale principals are 401; an existing unauthorized or Disabled user receives the documented 403 code. Disabled/PendingVerification login and Disabled `/me` remain available.
- **Pagination:** all four listing page endpoints use the exact `PagedResponse` body and normalization/metadata formulas; offset pages are deterministic for a stable dataset.
- **Search/filter contracts:** current public listing `lang`, `q`, agency/type/property/details/price/currency/area/rooms/sort/location/page filters and agency/dashboard equivalents keep their Chapter 10 semantics and are documented accurately.
- **Agency workspace:** agency/member/invitation/dashboard/admin success APIs remain; failures and current-state conflicts follow the shared contract; invitation lists show effective expiry.
- **Image operations:** existing size/type/ownership/cap/order/primary rules remain; upload failures are predictable; returned `/uploads` URLs can be fetched from a clean checkout and use configured CORS.
- **OpenAPI:** public/protected security, errors, pages, enums, uploads, and health match runtime and can be generated in tests; Development Swagger is usable.
- **CORS/configuration:** current localhost frontends work in Development; other origins require an explicit exact allowlist; no credentialed CORS or wildcard fallback exists. The current base JWT placeholder remains an explicit production-deployment blocker outside this frontend-development gate.
- **Support/debugging:** every API response exposes `X-Request-ID`; every ProblemDetails body and relevant structured logs use the same identifier.
- **Service health:** liveness answers process status without dependencies; readiness/database routes use 200/503 for PostgreSQL availability.

### Later enhancements, not blockers

Cursor pagination, refresh/revocation/password recovery, background notifications/expiry, cloud storage/CDN, durable media deletion, media scanning/transformation, client-supplied distributed correlation, metrics/exporters/alerts, broader concurrency tokens, and production deployment certification.

The frontend may begin only after 12P. It must branch on HTTP status and `code`, not English `detail`, and must treat a `traceId`/`X-Request-ID` as a support identifier rather than a security credential.

## 12. Observability boundary

Chapter 12 observability is intentionally small:

- Keep the default ASP.NET Core logging providers and configuration. Do not add a vendor or third-party logging package.
- Create a request scope containing `RequestId`; completion logs contain `RequestId`, `Method`, `Route`, `StatusCode`, and `ElapsedMilliseconds` as structured properties. Use the route template, or fixed `unmatched` when no endpoint route exists; never use raw path/query as a fallback.
- Register request-ID middleware outermost, completion logging next, and exception handling inside it so handled 500s are visible to the completion event. The application `IExceptionHandler` owns the single custom Error for a handled unexpected exception; do not re-enable duplicate framework handled-exception diagnostics. Framework fallback logging may apply when a response has already started and the exception cannot be handled.
- Expected validation, authentication, authorization, not-found, and conflict outcomes are represented by the completion event, not duplicated as warnings/errors.
- A handled database-readiness failure may emit one sanitized Warning owned by the health component.
- `HttpContext.TraceIdentifier` is the only Chapter 12 correlation identifier. `X-Request-ID`, ProblemDetails `traceId`, and logs must match.
- Never add request/response bodies, Authorization/Cookie headers, query values, passwords, JWTs, invitation credentials, emails, filenames/file bytes, SQL/parameters, or connection strings as structured log properties. Keep EF sensitive-data logging and Npgsql detailed errors disabled. The internal Error event may contain the thrown exception type/message/stack; none of it is copied to the response.
- If cleanup has already replaced an original exception, retain `QH-TX-01`; middleware cannot reconstruct it.
- `GET /api/health` is liveness. `/api/health/readiness` and `/api/health/database` are PostgreSQL readiness. Local storage is not a readiness dependency in this chapter.
- Tests use a capture provider and deterministic dependency replacements. They prove structured keys and forbidden-value absence, not formatted console strings.

No metrics, histograms, audit-event system, OpenTelemetry SDK/exporter, distributed trace backend, dashboard, alert, log sink, sampling policy, or service-level objective belongs in Chapter 12.

## 13. OpenAPI and configuration requirements

### OpenAPI

- The generated document is a tested contract, even though Swagger middleware remains Development-only.
- Bearer auth is defined once and applied only to operations that actually require it. Public auth, public listing discovery/details/comparables, public agency profile/listings, and health operations must not inherit security.
- Each operation documents the actual success type/status and relevant canonical failure types/statuses. Error schemas must show `code` and `traceId`; validation must show `errors`.
- Pagination documents `page` default 1, `pageSize` default 20/cap 100, normalization, body metadata, and one `PagedResponse<ListingResponse>` schema. It does not mention cursor pagination.
- Enums are represented as strings. Query/filter names and defaults match controller binding.
- Multipart avatar/logo/listing-image operations show the `file` field, 5 MB maximum, and JPG/JPEG/PNG/WEBP extension/content-type policy. Returned media paths are relative. Static GET routes are not invented as API operations.
- Responses document `X-Request-ID`; auth challenges document `WWW-Authenticate`; health documents liveness and readiness 200/503 semantics.
- `RealEstate.Api.http` contains safe, current health/auth/listing examples and no committed live token/password/secret.

### CORS and runtime configuration

- `Cors:AllowedOrigins` is an array of exact absolute HTTP/HTTPS origins. Development configuration contains `http://localhost:3000`, `https://localhost:3000`, `http://localhost:5173`, and `https://localhost:5173`.
- Base/production configuration has no implicit allowed origin. Missing means no cross-origin permission; malformed nonempty entries fail startup. Wildcards and credentials are prohibited.
- Any method/header remains allowed for Bearer JSON/multipart clients; `X-Request-ID` is exposed.
- CORS applies to API and static media by pipeline order. Static media stays anonymous.
- JWT secret relocation/validation is not part of Chapter 12. The base placeholder must be documented as a production blocker for Chapter 13/deployment work; no OpenAPI or readiness statement may imply otherwise.
- Chapter 12 does not choose deployed frontend origins, cookies, reverse-proxy/TLS configuration, `AllowedHosts`, or a secret-management vendor. Operators must supply environment values.

## 14. Verification strategy

Every implementation checkpoint must run its focused tests, `dotnet build`, the complete test suite, pending-model verification, and Git/diff checks before merge. 12P repeats the full menu independently.

### Build and test gates

1. Build Chapter 10 tooling where still applicable:

   ```powershell
   dotnet build tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj
   ```

2. Build the solution with no warnings/errors expected:

   ```powershell
   dotnet build
   ```

3. Run focused groups for the active checkpoint, then the complete PostgreSQL-backed suite:

   ```powershell
   dotnet test tests/RealEstate.Tests/RealEstate.Tests.csproj --no-build
   ```

4. Record the actual final count; it must be at least the 704-test prerequisite with 0 failed and 0 skipped, but 704 is not the expected final total.

### Required focused evidence

- **Contract/validation:** exact 400/404/405/415/500 content type, members, codes, trace; malformed JSON, invalid enum/number, missing body/form/file, root/field errors.
- **Authentication/authorization:** missing/malformed/expired token, `WWW-Authenticate`, invalid credentials, non-Guid/deleted user, Disabled/PendingVerification login and `/me`, Disabled/permission 403 across domains.
- **Conflicts/exceptions:** each state/capacity/set class; separate normalized-email and slug PostgreSQL races; exact constraint only; unrelated failure passthrough; rollback/no-mutation; cancellation not 500.
- **Pagination:** four endpoints, all metadata formulas, normalization/cap, beyond-last page, equal timestamps and stable adjacent/repeat results.
- **Invitation:** effective status/filter boundary and no write on read; dashboard/replacement/concurrency regression.
- **Trace/logging:** header/body/log identity, client header ignored, completion keys, exactly one exception Error, sensitive-value absence.
- **Health:** liveness without DB and readiness 200/503/unavailable/timeout/cancellation/alias parity.
- **OpenAPI:** resolve `ISwaggerProvider` in Testing; assert structural security, error-code catalog, page, enum, multipart, response-header, and health contracts; manually smoke-test Development Swagger.
- **CORS/configuration/static:** allowed/disallowed preflight and actual API/media, isolated clean-root upload-to-GET, missing static file, and invalid/missing origins. Disallowed origin proof is absence of `Access-Control-Allow-Origin`, not an invented HTTP 403.

Existing tests that assert only a generic status or substring must be strengthened where they are the proof for a Chapter 12 contract. Deterministic email/slug races use separate scopes and controlled interleaving with final-state assertions. No repeated probabilistic stress loop is required; existing Chapter 11 concurrency tests retain deterministic invariant proof when their loser status changes.

### Database/model gates

```powershell
dotnet ef migrations has-pending-model-changes --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api --no-build
```

- The expected result is no pending changes and exactly 15 committed migrations.
- In 12P, point the configured connection at a fresh PostgreSQL 16 database and run the update twice:

  ```powershell
  dotnet ef database update --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api --no-build
  dotnet ef database update --project src/RealEstate.Infrastructure --startup-project src/RealEstate.Api --no-build
  ```

  The first run must apply all 15 migrations and the second must report no migration work; then run the pending-model check.
- Because no schema change is planned, no new catalog object is expected. Inspect migration history/count and relevant existing unique indexes used by 12D/12J. If any checkpoint genuinely requires schema change, stop, amend the plan, create a separate migration checkpoint, and then add targeted catalog verification; never edit an applied migration.

### Git/documentation gates

At each checkpoint and final closeout:

```powershell
git status --short
git diff --name-status
git diff --stat
git diff --check
git diff --cached --check
```

Review staged/untracked/generated content, confirm only checkpoint files changed, and confirm no uploaded media or secret was added. At 12P reconcile this chapter, backend context, and quality handoff against actual evidence. Planning files remain non-authoritative and untracked unless the owner separately changes that policy.

## 15. Chapter completion rule

Chapter 12 is complete only when all of the following are true:

1. 12A through 12O were implemented as separate bounded branch/commit/merge checkpoints in dependency order, and 12P closeout is merged.
2. Current success routes/statuses/DTOs/string enums/relative media URLs remain compatible, except only the explicitly documented presentation/configuration changes.
3. Framework, authentication, authorization, every controller domain, routing, and unexpected exceptions satisfy the canonical failure contract with stable codes and trace IDs.
4. Missing/invalid/stale identity is 401; existing permission/Disabled rules are 403; Disabled/PendingVerification login and Disabled `/me` remain proven.
5. Named email/slug races and all selected current-state/capacity/set conflicts have exact status/code tests and retain deterministic final-state/invariant proof.
6. All four pagination APIs expose one schema with locked metadata and deterministic private ordering; cursor pagination is explicitly retained as deferred.
7. Invitation list presentation/filtering uses the locked effective-expiry rule without write-on-read.
8. Structured logs, exception ownership, sensitive-data exclusions, request-ID header/body/log correlation, and cancellation behavior are proven.
9. Liveness/readiness, CORS, clean-checkout static media, and OpenAPI behavior match sections 11–13; the deferred base JWT placeholder remains explicitly documented as a production blocker.
10. Focused tests and the complete suite pass with 0 failed/skipped and the actual new total is recorded; solution and query-review tool build cleanly.
11. A fresh PostgreSQL database applies all expected migrations, repeat update is empty, pending-model check is clean, named indexes exist, and no unexplained model/migration drift exists.
12. `CH11-STATE-02`, `CH11-API-01`, `CH11-API-02`, and `CH11-API-03` are removed only after proof; all other retained limitations in section 10 remain explicit.
13. `docs/backend-context.md` documents the supported frontend contract and next phase, and stale/conflicting statements are corrected.
14. Final diff/Git checks are clean, no secret/upload/generated artifact is tracked, documentation agrees, and no unrelated work is present.

Only then may backend-context mark Chapter 12 complete and frontend development current.

## 16. Expected documentation closeout

12P updates exactly these permanent tracked documents, based on actual results:

- `docs/chapters/chapter-12-api-consistency-observability-frontend-readiness.md`: status, completed checkpoint record, actual test/migration/model/OpenAPI/config evidence, final contract, and retained deferrals.
- `docs/backend-context.md`: Chapter 12 completion/lasting API, auth, pagination, invitation, observability, health, CORS/media/config rules; correct the stale earlier statement about listing-image status checks; make frontend development next/current.
- `docs/backend-quality-handoff.md`: remove only the four proven resolved entries identified in section 10; retain the transaction, test-hygiene, DB, broad-state, and durable-file items with accurate post-Chapter 12 wording; add/retain `C12-CONFIG-01` for the base JWT placeholder until Chapter 13/deployment hardening resolves it.

`RealEstate.Api.http` is updated in 12O as implementation evidence, not deferred to closeout. README expansion is not required by this chapter because the tested OpenAPI, request sample, backend context, and chapter document own the relevant contract. Closeout performs no production/test fix.

## 17. Final implementation sequence summary

| Checkpoint | Outcome | Dependency | Size | Migration expectation | Primary proof |
|---|---|---|---|---|---|
| 12A | Canonical ProblemDetails, validation, codes, request ID | Chapter 11 | Medium | None | Framework contract/header tests |
| 12B | Generic exception boundary and structured request logs | 12A | Medium | None | Injected 500 + exact log/trace/cancellation proof |
| 12C | Auth/users/avatar/challenge/forbid/principal policy | 12A–12B | Medium | None | JWT, Disabled, stale-principal, avatar-media tests |
| 12D | Registration email race translation | 12C | Medium | None | Controlled PostgreSQL duplicate race |
| 12E | Listing API/lifecycle failure contract | 12A, 12C | Medium | None | Listing validation/auth/state/success proof |
| 12F | Listing-image/media failure contract | 12A–12C, 12E | Medium | None | Media codes + Chapter 11 image regression |
| 12G | Agency core/workspace/logo contract | 12A, 12C | Medium | None | Core auth/slug/logo/success proof |
| 12H | Member/invitation failure contract | 12A, 12C | Medium | None | State conflicts + Chapter 11 concurrency proof |
| 12I | Admin agency-transition contract | 12A, 12C | Small | None | Admin 401/403/404/409/success proof |
| 12J | Agency slug race translation | 12G | Medium | None | Controlled PostgreSQL slug race |
| 12K | One pagination schema and deterministic private order | 12A; after domain normalization | Medium | None | Four-page metadata/tie tests |
| 12L | Effective invitation expiry presentation | 12H | Medium | None | Filter/boundary/no-write tests |
| 12M | Liveness and PostgreSQL readiness | 12A–12B | Medium | None | Live/ready/timeout/abort 200/503 tests |
| 12N | Configurable CORS and clean-checkout static media | 12A | Medium | None | Header/preflight/isolated upload-to-GET proof |
| 12O | Accurate OpenAPI and developer samples | 12A–12N | Medium | None | `ISwaggerProvider` structural + Swagger/sample review |
| 12P | Full verification and documentation closeout | 12A–12O | Medium | None; remain at 15 | Full suite, fresh/repeat migration, model, Git/docs |
