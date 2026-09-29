VERDICT: CHANGES_REQUESTED

# Review: [E13.S2] Observability (#113)

Scope: the whole working tree against `origin/main`@b16a8f0 (tracked and untracked).

## Blocking

### 1. The API request log now sends raw query strings to Loki. These include the Paymob webhook HMAC and URL-encoded emails, which the collector cannot redact.
**Where:**
- `deploy/docker-compose.prod.yml:31` (`CoreLogging__Console__Format: Json`)
- `api/core-libraries/Core.Logging/DependencyInjection.cs:66-70` (`RenderedCompactJsonFormatter` emits every property)
- `api/core-libraries/Core.Logging/RequestLoggingMiddleware.cs:47` (`QueryString`)
- `deploy/observability/otel-collector/config.yaml:87-91` (the redaction patterns only match a literal `@`)

**Rule:**
- plan Decision 19 and DoD "No PII is logged ... Caddy logs drop headers and query"
- `docs/constitution.md` section 4, line 147: logs "never carry ... emails, ... tokens or payment payloads"
- `docs/observability.md` section 12, lines 177-184: "The apps do not log PII (audited in #113)" and "No PII in query strings: ... the API ..."
- This is a docs-sync divergence: the code does one thing and the rule text says another.

**Problem:** Before this story, the console sink used Serilog's default text template, which prints only the message. Compose now switches the API to JSON. Every `ForContext` property of `HTTP_REQUEST_COMPLETED` therefore reaches stdout, and from there the collector and Loki, for 14 days. That includes `QueryString`. The query string was redacted in Caddy (`deploy/Caddyfile`) and in the spans, but the API's own request log was left untouched. Two existing endpoints put sensitive values in the query:
- `POST /api/payments/paymob/webhook?hmac=<signature>` (`api/Elmanhg.Api/Controllers/Payments/PaymobWebhooksController.cs:19`).
- `GET /api/audit-logs?actor=<user name, i.e. an email>` (`api/Elmanhg.Api/Controllers/AuditLogs/AuditLogsController.cs:20`). The Orval client builds this with `URLSearchParams` (`web/src/shared/api/generated/audit-logs/audit-logs.ts:54`), which encodes `@` as `%40`.

The collector's email regex requires a literal `@`, so `mona%40example.com` passes through into both the log body (the raw JSON line is kept as the body) and the flattened `QueryString` attribute.

**Failure:**
- An admin filters the audit log by `mona@example.com`. Loki stores `"QueryString":"?actor=mona%40example.com"` unredacted.
- Every Paymob webhook stores `"QueryString":"?hmac=..."`.

**Fix:** This is cheap, so fix it at the source. In `RequestLoggingMiddleware.cs:47`, log `"?redacted"`, or only the parameter names, whenever `QueryString.HasValue`. This mirrors the Caddy rule. Add one test asserting that the logged `QueryString` carries no values. Alternatively, in the collector, delete or replace `attributes["QueryString"]` and the `"QueryString":"..."` fragment of the body. Either way, `docs/observability.md` sections 5 and 12 must then state that the API request log drops query values.

### 2. Four alert rules miss the first event of a new counter series, and their promtool tests hide this by seeding the counters at 0
**Where:**
- `deploy/observability/prometheus/rules/elmanhg.rules.yml:76` (ApiUnhandledErrors)
- `:83` (ProviderUnavailable)
- `:90` (OtpDeliveryFailing)
- `:97` (AiModelErrors)
- `:111` (PaymentNotificationNeedsReview)
- `:60`, `:67` (the job alerts, same cause)
- Tests: `deploy/observability/prometheus/tests/elmanhg.rules.test.yml:117`, `:133`, `:148`, `:163`

**Rule:** plan Decision 10 and the alert table ("any `FlaggedForReview` notification in 1 h", "any `UNHANDLED_EXCEPTION` in 10 m"). Testing: a test must fail when the code is wrong.

**Problem:** The OTel SDKs (.NET and Python) create a metric point lazily, on the first `Add` for a new tag set. They export it cumulatively, so the series first appears in Prometheus at value 1, never at 0. Prometheus 3.14 (with no feature flag in the compose `command`) does not ingest the OTLP start time as a zero sample. As a result, `increase(x[w])` over a series whose only samples are all 1 returns 0.

The promtool inputs start every such counter at 0 (for example `0 1 1 1 1 1` and `0+1x5 ...`), a state production never produces. That is why the tests pass.

**Failure:**
- After a deploy, the first `ProcessPaymentNotification` that returns `FlaggedForReview` creates `elmanhg_payment_notifications_total{elmanhg_outcome="FlaggedForReview"} = 1`. `increase(...[1h]) = 0`, so `PaymentNotificationNeedsReview` never fires for it.
- The same happens to the first `UNHANDLED_EXCEPTION` of each request type (ApiUnhandledErrors).
- OtpDeliveryFailing (critical) needs 6 WhatsApp failures, not 5, when the `Failed` series is new.

**Fix:**
- For enum-bounded counters, pre-create the series at 0 on registration or construction:
  - `PaymentNotificationOutcome`
  - OTP channel x {Delivered, Failed}
  - job x run and item outcomes in `BackgroundJobMetrics.Register`
- For the open-ended ones (`elmanhg_requests_total` outcomes, `gen_ai_..._count{error_type}`), make the rule also count series that appeared inside the window, for example `(sum(increase(x[10m])) > 0) or (sum(x unless x offset 10m) > 0)`.
- Either way, add promtool cases whose input series start at 1 with earlier samples missing (for example `_x2 1 1 1`). These cases must fail against the current rules.

## Non-blocking
- **Client IPs.** `api/core-libraries/Core.Logging/RequestLoggingMiddleware.cs:49-57` now ships `ClientIp`, `ForwardedFor` and the CF geo headers, including `Latitude` and `Longitude`, to Loki. Caddy's JSON log also carries `remote_ip` and `client_ip` (`deploy/Caddyfile`). Neither the plan nor the docs list IPs as PII, and they are useful for abuse triage within 14 days, so this does not block. Recommended follow-up before launch (Egypt PDPL): truncate IPs to /24 (IPv4) or /48 (IPv6), or hash them with a rotating salt, and drop latitude and longitude. Do it in the middleware and with a Caddy `filter` (`ip_mask`) field. File it as a `deferred` issue.
- **Request size.** `api/Elmanhg.Api/Controllers/Observability/ClientErrorsController.cs:15-19` has no `[RequestSizeLimit]`. The anonymous endpoint buffers up to Kestrel's 30 MB default before the validator's field caps reject the request. The fields reaching logs are capped, which meets the gate's "capped in size". The house precedent is `PaymobWebhooksController.cs:18` (`[RequestSizeLimit(65536)]`); something like 16 KB would do here. The same gap exists on funnel-events.
- **Trace redaction.** `deploy/observability/otel-collector/config.yaml:113-116`: the traces pipeline has no redaction. Exception events (`CoreExceptionMiddleware` for 5xx, and HttpClient `RecordException`) go to Tempo unfiltered. I found no current exception message that carries PII. A `transform` over span attributes and event attributes with the same two patterns would give traces the same defence in depth as logs.
- **Grafana password fallback.** `deploy/docker-compose.prod.yml:217`: an empty `${GRAFANA_ADMIN_PASSWORD:-}` makes Grafana fall back to admin/admin when the stack is started without `deploy.sh`. The `lib.sh` guard and the loopback bind contain this, and no default password is committed. Consider stating in the runbook that `docker compose up` does not run the guard.
- **Permanent ai alert.** `deploy/observability/prometheus/prometheus.yml:80`: when the `ai` profile is off but `observability` is on, the `ai` probe fails and `ServiceDown{target_name="ai"}` (critical) fires permanently.
- **Shutdown sweep.** `api/Elmanhg.Api/Workers/*Worker.cs` (`using var run`): a sweep cancelled at shutdown is recorded as `Succeeded` and advances last success. The implementer flagged this; it is harmless.
- **Telemetry shutdown.** `ai/src/elmanhg_ai/main.py`, in the `finally` of lifespan: `telemetry.shutdown()` is skipped if an `aclose()` raises. Put it in its own try/finally.
- **Untested redaction fields.** `ReportClientErrorHandlerTests` #28 constrains the redaction of message and stack only. Dropping `LogRedactor.Redact` from `ErrorName` or `Path` would not fail any test.
- **Lane drift.** `origin/main` has moved to ad70cfb (E8.S3 avatar chat), so `ai/src/elmanhg_ai/main.py` and the generated artifacts will need a merge and regeneration before the PR.

## Verified
- **.NET tests.** I ran `dotnet test api/ -c Release` with `appsettings.json` moved aside (restored afterwards): 2905 passed, 0 failed, and no warnings in the log.
- **ai checks, run as ai-ci does (`python -m uv`).**
  - `sync --locked`: ok
  - `ruff format --check`: 57 files formatted
  - `ruff check`: passed
  - `mypy src`: 34 files clean
  - `pytest -m "not eval" --cov`: 116 passed, 97 %
  - `pip-audit==2.10.1`: no known vulnerabilities
- **Web checks.** `typecheck` ok, `lint` ok (0 warnings), `test --run`: 150 files and 888 tests passed.
- **promtool** (pinned prometheus v3.14.0 image). `check config`: SUCCESS, 17 rules. `test rules`: SUCCESS. Finding 2 is about what those tests fail to cover.
- **Images.** All 7 new images are pinned tag@sha256.
- **Ports.** Grafana is published on `127.0.0.1:${GRAFANA_PORT:-3000}` only, and no other new host port exists.
- **Grafana access.** Sign-up and anonymous access are off. `.env.example` leaves `GRAFANA_ADMIN_PASSWORD` empty, and `lib.sh` `check_observability_config` refuses the profile without it or without the alertmanager file. `/deploy/alertmanager.yml` is gitignored.
- **Export off by default.**
  - .NET: an empty `OtlpEndpoint` makes `ExportEndpoint` null, so no `AddOtlpExporter` is registered. The providers are still built (test #65), and a bad scheme fails startup (test #66).
  - Python: blank becomes None, giving `readers=[]` and no span processor, with `exporting=False` (tests #67 and #71).
  - Compose defaults `OTLP_ENDPOINT` to empty.
- **Metric labels are bounded.** Values are type names, enum names, error codes (no dynamic `CoreException($"...")` codes exist), job constants, and configured provider and model names. No ids or user data appear in any tag. Business tags are namespaced (`elmanhg.*`).
- **`/api/client-errors`.**
  - `[AllowAnonymous]` plus the per-IP fixed-window `client-errors` policy (test #64 gets a 429 `TOO_MANY_REQUESTS`).
  - Validator caps on message (500), name (100), stack (4000) and path (300), and the path must start with `/`.
  - Message, name, stack and path go through `LogRedactor` (test #28).
  - The web app sends `location.pathname` only (test #86 uses a URL with a query and a hash).
  - Postman has the `Observability` / `Report client error` request (noauth, plausible body).
- **Collector redaction.** `flatten(attributes)` runs before the pattern replacements, so nested Caddy `request.uri` values are reached. Caddy deletes request and response headers and replaces the query with `?redacted`.
- **Feature handlers untouched.** No `*Handler.cs` under Sessions, Subscriptions, Payments or Auth is modified, and nothing under `ai/src/elmanhg_ai/pipelines/` changed. Metrics come from behaviours, the router hook and the Python wrappers. The .cs guard grep prints nothing.
- **Plan items present.**
  - Every file in *Files to create* exists, plus the declared deviation `test_telemetry_app.py`.
  - All 66 .NET test names match the plan's Test plan, as do the web names (#86-#95).
  - Pipeline order is verified by tests #58-#60.
  - The docs edits were made: PRD sections 14/18, constitution section 4, deployment.md (no "/health not proxied" or "comes with #113" remains), ai-service.md and python skill delta 5.
- **Deviations.** Every listed deviation checked out against the code. I found no hidden deviation.

## Test quality
- **Constrain the implementation:**
  - `ElmanhgMetricsTests`
  - `BackgroundJobMetricsTests` (real `MetricCollector`s and observable gauges, time from a substitute `TimeProvider`)
  - `RequestMetricsBehaviourTests`
  - `QuizAnswerMetricsBehaviourTests` and `PaymentNotificationMetricsBehaviourTests` (including a no-measurement-on-throw case)
  - `LogRedactorTests`
  - `ReportClientErrorValidatorTests` (one failing case per rule)
  - `ObservabilityOptionsValidatorTests`
  - `OtpChannelRouterTests` #45 and #46
  - `CoreExceptionMiddlewareTests` #56 and #57 (a real `ActivityListener`)
  - `ClientErrorsEndpointTests`
  - the Python `test_metered_clients` and `test_telemetry_app` (in-memory exporter and reader)
  - the web `clientErrorReporter.test.ts` and `RouteError.test.tsx`
- **Weaker than they look:**
  - `ReportClientErrorHandlerTests` #28 does not cover name or path redaction.
  - The promtool cases for counter alerts (#103-#106) seed series at 0, so they cannot detect finding 2.
