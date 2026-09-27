---
name: momenta-api-contract
description: Use when planning, implementing, reviewing or testing any HTTP API change in any backend stack (.NET, Node, Python, Flask, Django) or any client that consumes one — shared OpenAPI-first rules for errors, status codes, pagination, filtering, idempotency, versioning, naming, dates, money, generated clients and breaking changes.
---

# Momenta API contract

One contract for every stack. The stack skills (`dotnet-feature`, `node-feature`, `python-feature`, `python-flask-feature`, `python-django-feature`) say *how* to wire it; this file says *what* goes on the wire. A stack skill rule that contradicts this file loses, except for an existing API's documented legacy shape (§11).

## 1. OpenAPI first

- Every endpoint change starts in the plan: the plan lists method, path, request schema, response schema per status, error codes, and auth scope before any code.
- Spec format: OpenAPI 3.1. One spec per major version, committed at `openapi/v<major>.json` (or `.yaml`) in the service root.
- Code-first stacks (ASP.NET Core, FastAPI, Fastify + Zod, drf-spectacular) generate the spec in CI and fail if the committed file differs. Contract-first stacks (Flask) edit the spec by hand in the same change as the route.
- Every operation has: unique `operationId` (`<resource>_<action>`, e.g. `orders_create`), one tag, `summary`, `security`, every response status it can return with a schema, and `application/problem+json` for every 4xx/5xx.
- Every schema property has a type; objects set `additionalProperties: false` on request bodies.
- Validate: `npx @redocly/cli lint openapi/v1.json` or `uvx openapi-spec-validator openapi/v1.yaml` (verify which one the repo uses in `pipeline.yml`).

## 2. Errors (RFC 9457)

Every non-2xx response body is `application/problem+json`:
```json
{
  "type": "https://<api-host>/problems/order-not-found",
  "title": "Order not found",
  "status": 404,
  "detail": "Order 01J9Z3K8M2 does not exist.",
  "instance": "/v1/orders/01J9Z3K8M2",
  "code": "ORDER_NOT_FOUND",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```
| Member | Rule |
|---|---|
| `type` | Stable URI per error code (`/problems/<kebab-code>`), or `about:blank` when only the status matters. Never changes once published |
| `title` | Short, human, same for every occurrence of the code. English, or localised per `Accept-Language` |
| `status` | Equals the HTTP status |
| `detail` | Occurrence-specific, human, safe to show; localised per `Accept-Language` (`ar`, `en`); may be omitted |
| `instance` | Request path (no query string with personal data) |
| `code` | **Required.** `UPPER_SNAKE` constant from the error-code catalogue; never localised; clients branch on this |
| `traceId` | **Required.** W3C trace id of the request |
| `errors` | Only for `VALIDATION_FAILED`: array `[{ "field": "lines[0].qty", "code": "MUST_BE_POSITIVE", "message": "..." }]` |

- Never in a problem body: stack traces, exception type names, SQL, internal hostnames, file paths, other tenants' ids.
- 5xx bodies: generic `title` ("Internal error"), `code: "INTERNAL_ERROR"`, no `detail`; full error only in logs, linked by `traceId`.
- One problem per response: the most relevant one.

## 3. Error-code catalogue

- Each service keeps one catalogue file (stack skill names it) and a `components.schemas.ErrorCode` enum in the spec, or `x-error-codes` per operation listing the codes it can return.
- Code format: `<AGGREGATE>_<CONDITION>` (`ORDER_NOT_FOUND`, `ORDER_ALREADY_SHIPPED`, `PAYMENT_DECLINED`). A code is never reused for a different meaning and never deleted while any published version returns it.
- Reserved cross-cutting codes (every service uses these exact strings):

| Code | Status |
|---|---|
| `VALIDATION_FAILED` | 400 |
| `MALFORMED_REQUEST` | 400 (unparseable JSON, wrong content type) |
| `UNAUTHENTICATED` | 401 |
| `FORBIDDEN` | 403 |
| `NOT_FOUND` / `<AGGREGATE>_NOT_FOUND` | 404 |
| `METHOD_NOT_ALLOWED` | 405 |
| `CONFLICT` / domain-specific conflict code | 409 |
| `CONCURRENCY_CONFLICT` | 409 (stale version on save) |
| `IDEMPOTENCY_KEY_IN_PROGRESS` | 409 |
| `PRECONDITION_FAILED` | 412 (`If-Match` mismatch) |
| `PRECONDITION_REQUIRED` | 428 (`If-Match` missing where required) |
| `PAYLOAD_TOO_LARGE` | 413 |
| `UNSUPPORTED_MEDIA_TYPE` | 415 |
| `IDEMPOTENCY_KEY_REUSED` | 422 (same key, different body) |
| `BUSINESS_RULE_VIOLATION` / domain-specific rule code | 422 |
| `RATE_LIMITED` | 429 |
| `INTERNAL_ERROR` | 500 |
| `DEPENDENCY_UNAVAILABLE` | 503 |

## 4. Status codes

| Situation | Status | Notes |
|---|---|---|
| Read OK | 200 | |
| Created | 201 | `Location` header with the new resource URL; body = the resource |
| Accepted for async processing | 202 | body has a status URL (`statusUrl`) |
| Success, no body (DELETE, some PUT) | 204 | |
| Request body/params fail validation | 400 | team default; FastAPI's 422 default is overridden to 400 |
| No/invalid credentials | 401 | `WWW-Authenticate` header |
| Authenticated, not allowed (function-level) | 403 | |
| Resource missing **or belongs to another tenant/user** | 404 | never 403 for other tenants' objects (no existence leak) |
| State conflict / stale version / duplicate | 409 | |
| `If-Match` does not match | 412 | |
| Valid request, business rule refuses | 422 | e.g. `ORDER_ALREADY_SHIPPED`; repos with an existing 409/400 convention for rule violations keep it and document it in the spec |
| Rate limit | 429 | `Retry-After` seconds |
| Unhandled | 500 | |
| Downstream dependency down/timeout | 503 | `Retry-After` when known |

- Never 200 with an error body. Never 500 for client mistakes.

## 5. Naming and shapes

- Paths: `/v<major>/<plural-kebab-resource>/{id}/<sub-resource>`; nouns, no verbs, except explicit actions as `POST /v1/orders/{id}:cancel` or `POST /v1/orders/{id}/cancellation` (pick one style per service; default sub-resource).
- JSON members `camelCase`. Enum values `SCREAMING_SNAKE` strings (`"PENDING"`), never integers.
- IDs: strings on the wire (UUIDv7 or opaque public id). Never expose sequential integer ids for guessable resources.
- Booleans are `isX`/`hasX`. Collections are plural. Absent optional member = omitted (not `null`) unless `null` carries meaning; the spec states which.
- Query parameters `camelCase`.
- Request and response schemas are separate (`CreateOrderRequest`, `Order`); requests never contain server-owned fields (`id`, `tenantId`, `createdAt`, `status`, `price`).

## 6. Dates, times, money

- Instants: RFC 3339 / ISO-8601 in UTC with `Z` and at least second precision: `"2026-09-27T08:15:30Z"` (`format: date-time`). Servers store UTC; never local time on the wire.
- Calendar dates: `"2026-09-27"` (`format: date`). Durations: ISO-8601 (`"PT15M"`).
- Time zones, when needed, as an IANA name field (`"timeZone": "Africa/Cairo"`), never an offset guess.
- Money: object `{ "amount": "125.50", "currency": "EGP" }`: `amount` is a decimal **string** with the currency's minor-unit scale, `currency` is ISO 4217. Never a JSON number with a fraction. A service may instead use `{ "amountMinor": 12550, "currency": "EGP" }` (integer minor units); one representation per service, declared once as `components.schemas.Money`.
- Percentages/rates: decimal strings (`"0.1500"`).

## 7. Pagination, filtering, sorting

- Default: cursor (keyset) pagination.
  - Request: `?limit=20&cursor=<opaque>`; `limit` default 20, min 1, max 100 (400 above max).
  - Response: `{ "items": [...], "nextCursor": "eyJ..." | null }`. `nextCursor: null` = last page.
  - Cursor = base64url of the last item's sort key(s) + id; opaque to clients; server rejects tampered cursors with 400 `VALIDATION_FAILED`.
  - Ordering deterministic: sort key(s) + primary key tiebreaker.
- Offset pagination only for admin grids that need page jumps: `?page=1&pageSize=20` (max 100) → `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 123 }`.
- A list endpoint without pagination is a blocking finding.
- Filtering: explicit, documented query params per field (`?status=PAID&createdFrom=2026-01-01T00:00:00Z&createdTo=...`); ranges use `<field>From` / `<field>To` (inclusive from, exclusive to). No generic filter languages (`?filter=...` expressions) without an ADR.
- Sorting: `?sort=-createdAt,amount` (comma list, `-` = descending); only whitelisted fields; unknown field → 400. Default sort documented per endpoint.
- Search: `?q=` for free text, length-capped (≤ 100 chars).

## 8. Idempotency

- `Idempotency-Key` request header (UUID, client-generated) is **required** on POSTs that create money movements, orders, or other non-repeatable side effects; missing → 400 `VALIDATION_FAILED`. Other POSTs accept it optionally.
- Server stores `(userId or clientId, key, requestHash, status, responseStatus, responseBody, createdAt)` with a unique constraint on `(userId, key)`, retained ≥ 24 h.
- Same key + same body, completed → replay the stored status and body (plus header `Idempotent-Replayed: true`).
- Same key + different body → 422 `IDEMPOTENCY_KEY_REUSED`.
- Same key while the first request is still running → 409 `IDEMPOTENCY_KEY_IN_PROGRESS` (+ `Retry-After`).
- Semantics follow the IETF `Idempotency-Key` HTTP header draft (draft-ietf-httpapi-idempotency-key-header).
- Server-side retries of outbound calls are allowed only for idempotent methods or calls carrying an idempotency key.

## 9. Concurrency

- Resources edited by more than one actor expose a version: `ETag` header on GET (strong, quoted), and `version` in the body if clients need it.
- PUT/PATCH on those resources require `If-Match`; missing → 428 `PRECONDITION_REQUIRED`; mismatch → 412 `PRECONDITION_FAILED`; stale save detected by the DB → 409 `CONCURRENCY_CONFLICT`.

## 10. Versioning and breaking changes

- Major version in the URL path (`/v1`). Minor, backwards-compatible changes do not change the path.
- Breaking (needs a new major path and a plan that says so):
  - removing or renaming an endpoint, parameter, request/response member, or enum value
  - making an optional request member required, or adding a required request member
  - changing a member's type, format, or meaning; tightening validation (lower max, new pattern)
  - changing a status code or error `code` for an existing situation
  - changing pagination style, default sort, or auth requirements
- Non-breaking: adding endpoints, optional request members, response members, new error codes for *new* situations, new enum values **only** where the spec marks the enum as extensible (`x-extensible-enum: true`) and clients handle unknown values.
- CI check: `oasdiff breaking <base-spec> <new-spec> --fail-on ERR` against the spec on the target branch (verify flags with `oasdiff breaking --help`). A breaking diff without a new major version fails the build.
- Deprecation: mark with `deprecated: true` + `Deprecation` and `Sunset` response headers (RFC 8594 / RFC 9745); keep the old version ≥ 6 months after the successor ships unless the plan names a shorter date agreed with all consumers.

## 11. Legacy error shapes

- APIs that already return `{ "code": "...", "message": "..." }` migrate additively: add `type`, `title`, `status`, `detail`, `instance`, `traceId`, set `Content-Type: application/problem+json`, and keep `message` as an extension member (= `detail`) until the next major version.
- New APIs never emit `message`.

## 12. Cross-cutting headers

| Header | Direction | Rule |
|---|---|---|
| `Authorization: Bearer <jwt>` | request | only auth mechanism for APIs; tokens never in query strings |
| `Accept-Language` | request | `ar` / `en`; selects `title`/`detail` language; default `en` |
| `Idempotency-Key` | request | §8 |
| `If-Match` / `ETag` | both | §9 |
| `X-Request-Id` | both | echoed back; generated if absent |
| `traceparent` | both | W3C Trace Context, propagated to downstream calls |
| `Retry-After` | response | on 429/503 and 409 in-progress |
| `Location` | response | on 201/202 |
| `Cache-Control` | response | `no-store` on authenticated responses unless the endpoint documents caching |

- CORS: explicit origin allow-list per environment; never `*` with credentials.
- Rate limits: per user (fallback per IP); 429 body is a problem with `code: "RATE_LIMITED"`.

## 13. Generated clients

- Clients are generated from the committed spec, never hand-written: TypeScript/React via the repo's generator (Orval or `openapi-typescript` + `openapi-fetch`), .NET via Kiota or NSwag, Dart/Kotlin via `openapi-generator`. The stack skill of the consumer names the tool and command.
- Generated code lives in a `generated/` folder, is excluded from lint/coverage, and is regenerated (not edited) when the spec changes.
- A spec change and the regenerated client land in the same PR when both live in the repo.
- Clients handle problem+json by `code`, never by parsing `detail`/`title`, and treat unknown `code` values as generic errors.

## 14. Postman and tests

- Every new or changed endpoint has Postman request(s) covering the happy path and each documented error `code` reachable without special setup (pipeline Postman step).
- Integration tests assert status, `Content-Type`, and `code` for every error path they cover.

## 15. Review checklist (each item yes/no)

- [ ] Spec updated in the same change; `operationId`, tag, security, all statuses, problem responses present.
- [ ] Every 4xx/5xx is `application/problem+json` with `code` and `traceId`.
- [ ] New error codes added to the catalogue; no code reused with a new meaning.
- [ ] Lists paginated (cursor default, `limit` ≤ 100); sort/filter fields whitelisted.
- [ ] Money as decimal string or integer minor units + ISO 4217; no floats.
- [ ] Instants UTC ISO-8601 with `Z`.
- [ ] `Idempotency-Key` required on money/order-creating POSTs.
- [ ] Other tenants' resources return 404.
- [ ] `oasdiff breaking` clean, or the plan declares a new major version.
- [ ] Generated clients regenerated, not hand-edited.
