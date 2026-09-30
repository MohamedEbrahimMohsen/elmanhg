# Observability

How the team sees errors, latency, uptime and background-job health in every environment (E13.S2, #113). Hosting is in [docs/deployment.md](deployment.md); the AI service's own telemetry keys are in [docs/ai-service.md](ai-service.md).

## 1. Overview and signal flow

```
browser ──traceparent──▶ Caddy (web) ──▶ api ──traceparent──▶ ai
   │ POST /api/client-errors              │ OTLP/gRPC            │ OTLP/gRPC
   ▼                                      ▼                      ▼
  api                                otel-collector ◀────────────┘
                                          │  ▲ tails /var/lib/docker/containers/*/*-json.log
                          traces ▼        │  │ (api, ai, web, postgres, migrate stdout)
                       Tempo   Prometheus ◀── blackbox probes (edge, api, ai)
                                 │  metrics      logs ▶ Loki
                                 ▼ alerts
                           Alertmanager ──▶ email / webhook (host config)
                       Grafana reads Prometheus, Loki, Tempo and Alertmanager
```

- The API (.NET) and the AI service (Python) emit OpenTelemetry traces and metrics over OTLP/gRPC. Export happens only when an endpoint is set (`OTLP_ENDPOINT` in `.env`); with it empty, spans and trace ids still exist and appear in every log line, and nothing is exported.
- Every container writes JSON logs to stdout. The Docker json-file driver tags each line with the compose service name. The collector tails the files, redacts PII and ships them to Loki. No application runs an OTLP log exporter.
- One trace id runs end to end: a browser or Caddy `traceparent` is continued by the API, and the API's `HttpClient` forwards it to the AI service. The API returns it as `X-Trace-Id`.
- The backend is self-hosted on the same host, behind the compose profile `observability`. Any OTLP SaaS can receive traces and metrics instead: point `OTLP_ENDPOINT` at it and set the header keys (section 13). Logs then stay in the local Loki.

## 2. Turning it on

1. In `.env`: `COMPOSE_PROFILES=ai,observability`, `OTLP_ENDPOINT=http://otel-collector:4317`, `GRAFANA_ADMIN_PASSWORD=$(openssl rand -hex 16)`, and optionally `GRAFANA_PORT` (default 3000).
2. `cp observability/alertmanager/alertmanager.example.yml alertmanager.yml && chmod 600 alertmanager.yml` (section 9).
3. `bash deploy.sh sha-<7>`. It refuses the profile when the Grafana password or `alertmanager.yml` is missing, and waits for Prometheus, Alertmanager, the blackbox exporter and Grafana to be healthy.
4. Grafana listens on the host's loopback only: `ssh -L 3000:127.0.0.1:3000 deploy@<host>`, then open `http://localhost:3000` and sign in as `admin`. Sign-up and anonymous access are off. Nothing else in the stack publishes a port.

Host sizing: the profile adds about 1.5 GB of RAM (memory limits: collector 256 MB, Prometheus 512 MB, Loki 512 MB, Tempo 512 MB, Grafana 256 MB, Alertmanager and blackbox 64 MB each). Use 8 GB of RAM with the profile.

The Grafana, Loki and Tempo images are AGPL-licensed operator tools. They run unmodified as separate containers and are never linked into or shipped with product code.

## 3. Traces

| Service | Spans | Source |
|---|---|---|
| api | one SERVER span per HTTP request (`/health` excluded) | ASP.NET Core instrumentation |
| api | one CLIENT span per outbound HTTP call (AI service, OTP providers, Paymob), with the exception recorded on failure | HttpClient instrumentation |
| api | one span per database command | Npgsql's `ActivitySource` (`Npgsql`) |
| api | one span per background sweep, `job <name>`, tagged `elmanhg.job` and `elmanhg.outcome`, marked as an error when listing failed | `BackgroundJobMetrics` (`ActivitySource` `Elmanhg`) |
| api | errors: `CoreExceptionMiddleware` tags the request span with `app.error_code`, and adds an `exception` event for 5xx | `Core.Exceptions` |
| ai | one SERVER span per request (`/health*` excluded) | FastAPI instrumentation |
| ai | one CLIENT span per model call: `chat <model>`, `embeddings <model>` or `transcription <model>`, with GenAI attributes and token usage (chat and embeddings) | `clients/metered.py` |

- Sampling: `ParentBased(TraceIdRatio)`. `Observability__TraceSampleRatio` and `ELMANHG_AI_TRACE_SAMPLE_RATIO` (default 1.0) set the share of new traces kept. A request that arrives with a sampled `traceparent` is always kept, so API and AI never split a trace.
- Propagation: W3C `traceparent`. Caddy passes the header through unchanged. The API continues it, and its `HttpClient` sends it to the AI service. The AI service reports the server span's trace id as `X-Trace-Id` and in its logs.
- Span URLs never carry query values: the ASP.NET Core and HttpClient instrumentations redact them by default.

## 4. Metrics

Prometheus receives OTLP from the collector (`--web.enable-otlp-receiver`). Names are translated with the usual rules: dots become underscores, the unit is appended (`_seconds`, `_bytes`), counters end in `_total`, and `{…}` units are dropped. The OTLP `service.name` becomes the `job` label, which is why business tags are namespaced (`elmanhg.job`, never `job`).

| Instrument | Type | Unit | Tags | Emitter | Prometheus name |
|---|---|---|---|---|---|
| `elmanhg.requests` | counter | {request} | `elmanhg.request` (request type name), `elmanhg.outcome` | `RequestMetricsBehaviour` (every MediatR request) | `elmanhg_requests_total` |
| `elmanhg.request.duration` | histogram | s | same | `RequestMetricsBehaviour` | `elmanhg_request_duration_seconds_*` |
| `elmanhg.quiz.answers` | counter | {answer} | `elmanhg.outcome` (grading outcome, or `Ungraded`) | `QuizAnswerMetricsBehaviour` (SubmitAnswer) | `elmanhg_quiz_answers_total` |
| `elmanhg.payment.notifications` | counter | {notification} | `elmanhg.outcome` (`PaymentNotificationOutcome`) | `PaymentNotificationMetricsBehaviour` | `elmanhg_payment_notifications_total` |
| `elmanhg.otp.sends` | counter | {message} | `elmanhg.channel` (`WhatsApp`, `Sms`, `Email`), `elmanhg.outcome` (`Delivered`, `Failed`) | `OtpChannelRouter` | `elmanhg_otp_sends_total` |
| `elmanhg.client.errors` | counter | {error} | `elmanhg.source` (`Window`, `UnhandledRejection`, `Route`) | `ReportClientErrorHandler` | `elmanhg_client_errors_total` |
| `elmanhg.ask_teacher.sla_events` | counter | {event} | `elmanhg.kind` (`FirstReminder`, `SecondReminder`, `Breach`) | `ProcessTeacherThreadSlaHandler` | `elmanhg_ask_teacher_sla_events_total` |
| `elmanhg.job.runs` | counter | {run} | `elmanhg.job`, `elmanhg.outcome` (`Succeeded`, `PartiallyFailed`, `Failed`) | `BackgroundJobMetrics` | `elmanhg_job_runs_total` |
| `elmanhg.job.duration` | histogram | s | `elmanhg.job`, `elmanhg.outcome` | `BackgroundJobMetrics` | `elmanhg_job_duration_seconds_*` |
| `elmanhg.job.items` | counter | {item} | `elmanhg.job`, `elmanhg.outcome` (`Succeeded`, `Failed`) | `BackgroundJobMetrics` | `elmanhg_job_items_total` |
| `elmanhg.job.last_success` | gauge | s (unix time) | `elmanhg.job` | `BackgroundJobMetrics` | `elmanhg_job_last_success_seconds` |
| `elmanhg.job.interval` | gauge | s | `elmanhg.job` | `BackgroundJobMetrics` | `elmanhg_job_interval_seconds` |
| `http.server.request.duration` | histogram | s | `http.route`, `http.request.method`, `http.response.status_code` | ASP.NET Core (api), FastAPI (ai) | `http_server_request_duration_seconds_*` |
| (recording rule) | p95 per route | s | `http_route` | Prometheus, over sessions, exams and browse routes of the API | `elmanhg:api_route_latency_p95:rate15m` |
| `http.client.request.duration` | histogram | s | `server.address`, `http.response.status_code` | HttpClient (api) | `http_client_request_duration_seconds_*` |
| `System.Runtime` meter | various | | | .NET runtime (GC, memory, thread pool) | `dotnet_*` |
| `gen_ai.client.operation.duration` | histogram | s | `gen_ai.operation.name` (`chat`, `embeddings`, `transcription`), `gen_ai.provider.name`, `gen_ai.request.model`, `error.type` on failure | `clients/metered.py` (ai) | `gen_ai_client_operation_duration_seconds_*` |
| `gen_ai.client.token.usage` | histogram | {token} | the same, plus `gen_ai.token.type` (`input`, `output`) | `clients/metered.py` | `gen_ai_client_token_usage_*` |
| `elmanhg.ai.cost` | counter | {USD} | operation, provider, model | `clients/metered.py` (price settings in docs/ai-service.md) | `elmanhg_ai_cost_total` |
| `probe_success` | gauge | | `target_name` (`edge`, `api`, `ai`), `instance` | blackbox exporter | `probe_success` |

Request outcomes (`elmanhg.outcome` on `elmanhg.requests`): `Success`; `VALIDATION_FAILED` for any validation failure; `CANCELLED` when the caller went away; the error code of any other core exception (for example `SESSION_NOT_FOUND`, `AI_SERVICE_UNAVAILABLE`); `UNHANDLED_EXCEPTION` otherwise. Every tag value is a type name, an enum name or an error code, never an id or user data.

Background jobs (`elmanhg.job`): `exam-auto-submit`, `subscription-lapse`, `lesson-content-index`, `teacher-voice-transcription`, `ask-teacher-sla`. A job registers when its worker starts (last success = now). Each sweep records one run: `Failed` when listing the work threw, `PartiallyFailed` when at least one item failed, `Succeeded` otherwise. Only a sweep that is not `Failed` advances the last success.

## 5. Logs

| Service | Format | Trace link |
|---|---|---|
| api, migrate | Serilog `RenderedCompactJsonFormatter` (`CoreLogging__Console__Format=Json`, set by compose): `@t`, `@m`, `@l` (only when not Information), `@x`, `@tr`, `@sp`, plus the properties (`SourceContext`, `TraceId`, …). The request log line (`HTTP_REQUEST_COMPLETED`) keeps query keys only (`?hmac=[redacted]`), cuts `ClientIp` and `ForwardedFor` to /24 (IPv4) or /48 (IPv6) and drops the Cloudflare latitude and longitude | `@tr` / `@sp` |
| ai | structlog JSON: `timestamp`, `level`, `event`, `trace_id`, `request_id` | `trace_id` |
| web (Caddy) | Caddy JSON access log and default logger (which carries the `http.log.error.*` handler errors), both on stdout with request headers and response headers deleted, the query string replaced by `?redacted` and `remote_ip` / `client_ip` masked to /24 or /48 | none |
| postgres | plain text | none |

- Collection: the Docker json-file driver writes each container's stdout to `/var/lib/docker/containers/<id>/<id>-json.log` with the label `com.docker.compose.service`. The collector's `filelog` receiver tails these files (read-only mount; the collector runs as root and publishes no port), drops lines without the label (other projects, and the telemetry stack itself, whose containers use a logging anchor without it), names each stream `elmanhg-<service>`, parses JSON bodies into attributes, maps `@tr`/`trace_id` to the log's trace id and `@l`/`level` to its severity, redacts (section 12) and exports to Loki over OTLP.
- In Loki the stream label is `service_name` (for example `elmanhg-api`), the trace id is the `trace_id` structured metadata, and the parsed fields are structured metadata too.
- Queries: `{service_name="elmanhg-api"} | json | @l="Error"`; all logs of one request: `{service_name=~"elmanhg-.+"} | trace_id="<X-Trace-Id>"`. In Grafana, a log line's TraceID field opens the trace in Tempo, and a Tempo span opens its logs (±5 minutes).
- Rotation on the host: 10 MB × 5 files per app container, 10 MB × 3 per telemetry container.
- Retention in Loki: 14 days.

## 6. SLO: 99.5 % monthly

PRD §14 sets availability at 99.5 % a month, so the error budget is 0.5 % (about 3 h 36 min of full outage in 30 days).

- **Request SLI**: API requests whose status is not 5xx, divided by all API requests, over 30 days. `http_route="/health"` is excluded. Source: `http_server_request_duration_seconds_count{job="elmanhg-api"}`.
- **Uptime SLI**: blackbox `probe_success` averaged over 30 days, per target (`edge` = Caddy → API → database, `api`, `ai`).
- **Burn rate** = observed error ratio ÷ 0.005. Recording rules keep the 5xx ratio over 5 m, 30 m, 1 h and 6 h (`elmanhg:api_error_ratio:rate<window>`).
  - Fast burn (critical): ratio > 14.4 × 0.005 = 7.2 % over both 1 h and 5 m. At that rate 2 % of the monthly budget goes in one hour, and the whole budget in about 2 days.
  - Slow burn (warning): ratio > 6 × 0.005 = 3 % over both 6 h and 30 m. 5 % of the budget goes in 6 hours.
  - The short window stops the alert soon after the problem ends.

## 7. Dashboards

Grafana provisions three read-only dashboards in the folder `Elmanhg` (edit the JSON in `deploy/observability/grafana/dashboards/`, not in the UI).

| uid | Panel | Answers |
|---|---|---|
| `elmanhg-service-health` | API availability (30 d), Error budget left (30 d) | are we inside the 99.5 % SLO, and how much budget is left |
| | Probe uptime (30 d), Probe status | is each target reachable, and when was it not |
| | API requests/s by status, 5xx ratio | traffic and error shape; the red line is the fast-burn threshold |
| | API p95 by route (top 10), API → AI p95, AI requests/s and p95 | where latency comes from |
| | Top failure outcomes | which requests fail and with which error code |
| | API memory | the API process working set |
| | Errors across services | Error-level log lines from every container |
| `elmanhg-background-jobs` | Since last success, Stale after | is a job stuck (the alert fires past 3 intervals) |
| | Runs by outcome, Items succeeded and failed, Sweep p95 | is a job failing or slowing down |
| | Worker logs | what the workers logged |
| `elmanhg-business` | Quiz answers/min by outcome, Quiz answers | learning activity |
| | Payment notifications by outcome, Checkouts and refunds | payment health |
| | OTP sends by channel | can people sign in |
| | AI calls, AI tokens, AI cost (USD), AI p95 by operation | assistant usage, spend and latency |
| | Browser errors by source | front-end failures reported through `POST /api/client-errors` |

## 8. Alerts

Rules live in `deploy/observability/prometheus/rules/elmanhg.rules.yml`, and each has a `promtool` unit test in `deploy/observability/prometheus/tests/elmanhg.rules.test.yml` (the smoke test runs them).

| Alert | Expression (short) | Severity | First response |
|---|---|---|---|
| `ApiErrorBudgetFastBurn` | 5xx ratio > 7.2 % over 1 h and 5 m, for 2 m | critical | Open Service health → Top failure outcomes and Errors across services; roll back the last deploy if it started after it |
| `ApiErrorBudgetSlowBurn` | 5xx ratio > 3 % over 6 h and 30 m, for 15 m | warning | Same, during working hours |
| `ServiceDown` | `probe_success == 0` for 2 m | critical | `docker compose ps`, `logs <service>`; for `edge`, check Caddy, then the API and Postgres |
| `ApiTelemetryMissing` | no `target_info{job="elmanhg-api"}` for 10 m, for 5 m | warning | Is `OTLP_ENDPOINT` set, and is `otel-collector` running (`docker compose logs otel-collector`)? |
| `BackgroundJobStale` | now − last success > 3 × interval, for 5 m | warning | Background jobs → Worker logs; restart `api` if the worker loop stopped |
| `BackgroundJobFailing` | ≥ 3 `Failed` sweeps in 15 m | warning | The listing query failed: check the database and the API logs |
| `BackgroundJobItemsFailing` | ≥ 10 failed items in 30 m | warning | Worker logs show the failing ids; fix the data or the dependency (the AI service for `lesson-content-index` and `teacher-voice-transcription`) |
| `ApiUnhandledErrors` | any `UNHANDLED_EXCEPTION` in 10 m | warning | Errors across services; find the trace by its id and fix the bug |
| `ProviderUnavailable` | ≥ 5 `AI_SERVICE_UNAVAILABLE`, `OTP_CHANNEL_UNAVAILABLE` or `PAYMENT_GATEWAY_UNAVAILABLE` in 15 m | warning | Check the provider's status page and the matching `*.env` keys |
| `OtpDeliveryFailing` | ≥ 5 failed OTP sends in 15 m | critical | Nobody can sign in: check the WhatsApp or Resend credentials and quotas ([docs/otp-delivery.md](otp-delivery.md)) |
| `AiModelErrors` | ≥ 5 failed model or embedding calls in 15 m | warning | Business → AI calls shows the `error.type`; check the Anthropic or OpenAI key and status |
| `ClientErrorSpike` | ≥ 50 browser errors in 15 m | warning | Loki: `{service_name="elmanhg-api"} |= "Client error from"`; usually a bad web deploy |
| `PaymentNotificationNeedsReview` | any `FlaggedForReview` notification in 1 h | warning | Resolve it in the admin payment log ([docs/paymob.md](paymob.md)) |
| `ApiHotPathSlow` | p95 of a sessions/exams/browse route > 1 s over 15 m with > 0.05 rps, for 15 m | warning | Service health → API p95 by route; compare with the [docs/performance.md](performance.md) budgets; check the DB ([docs/performance.md](performance.md) §6) and recent deploys |
| `AskTeacherSlaBreached` | any `Breach` in 15 m | warning | Open the teacher inbox as admin, or contact the subject's teachers; `ask-teacher-sla` Worker logs show the thread id ([docs/ask-teacher.md](ask-teacher.md), SLA) |

Every alert carries `summary` and `runbook` annotations.

Counter alerts ("any" or "≥ n in a window") sum `increase(x[w])` over series older than the window plus the whole value of series born inside it: `(increase(x[w]) unless (x unless x offset w)) or (x unless x offset w)`. The OTel SDKs export a new tag set at its first value (1, never 0), and `increase()` alone reads that first event as no change. The promtool tests include series that start at 1. After a Prometheus volume is wiped, counters that are already non-zero look new for one window and can fire once.

## 9. Alert delivery

Alertmanager reads `deploy/alertmanager.yml` on the host (`ALERTMANAGER_CONFIG_FILE`), copied from `observability/alertmanager/alertmanager.example.yml`. The file is gitignored and should be `chmod 600`, because it holds SMTP passwords or webhook URLs. Alertmanager cannot expand environment variables, which is why this is a file and not `.env` keys.

- The default receiver has no integrations: alerts show in Grafana (Alertmanager datasource) and in Alertmanager, but go nowhere. Live receivers are deferred until the dev adds real addresses.
- Email: uncomment the `email` receiver. Resend SMTP: `smarthost: smtp.resend.com:587`, `auth_username: resend`, `auth_password: <Resend API key>`, `require_tls: true`, and a `from` address on a verified domain.
- Webhook: uncomment the `webhook` receiver with the URL of any endpoint that accepts the Alertmanager payload (a chat bridge or a paging service).
- Route critical alerts to the receiver with a `routes:` entry (`matchers: [severity="critical"]`), then `docker compose -f docker-compose.prod.yml restart alertmanager`. Grouping: by `alertname` and `severity`, 30 s wait, repeat every 4 h.

## 10. External uptime monitor

The in-stack probes cannot report the host itself being down. `GET https://<site>/api/health` is public for that: Caddy rewrites it to the API's `/health`, which checks the database, and the body is only `Healthy` (200) or `Unhealthy` (503). Point an external monitor (UptimeRobot, Better Stack or Healthchecks.io) at it with a 1-minute check. This, and a dead-man's switch (an always-firing heartbeat alert sent to Healthchecks.io), are deferred until the live domain exists.

## 11. Error tracking

There is no vendor SDK. Errors reach the team through four paths:
1. **Spans**: `CoreExceptionMiddleware` tags the request span with `app.error_code`, and records the exception on it for 5xx.
2. **Metrics**: `elmanhg.requests` counts every MediatR request by outcome, so the error code shows in Top failure outcomes and in the alerts.
3. **Logs**: Error-level lines (5xx, unhandled exceptions, worker failures) in Loki, linked to their trace.
4. **Browser errors**: the web app reports uncaught errors, unhandled promise rejections and route errors to `POST /api/client-errors`. The endpoint is anonymous, rate-limited per IP (`ClientErrors:PermitLimit` per `ClientErrors:WindowSeconds`, 30 per minute by default, then 429 `TOO_MANY_REQUESTS`) and size-capped (body at most 16 KB, then 413; message 500, name 100, stack 4000, path 300 characters; the web app truncates to the same limits). The path must start with `/`, and the web app sends `location.pathname` only, never the query or hash. The handler increments `elmanhg.client.errors` and logs one Warning with the source, path, user id (when signed in), name, message and stack, each passed through `LogRedactor`. The web app sends at most 10 reports per page load, each distinct message once, and never reports `ApiError` (already logged by the API) or `AbortError`.

A SaaS backend (Grafana Cloud, Honeycomb, any OTLP endpoint) can replace the local stores for traces and metrics through `OTLP_ENDPOINT` plus `Observability__OtlpHeaders` / `ELMANHG_AI_OTLP_HEADERS`. A vendor error tracker (Sentry: issue grouping, source maps, releases) is deferred until there is a DSN.

## 12. PII rules

- Never log, tag or label phone numbers, email addresses, OTP codes, tokens (JWT, refresh, service, API keys) or payment payloads. OTP codes are six digits, which cannot be told apart from other numbers, so the only rule for them is "never logged". The fake OTP channel logs codes in Development only.
- Metric tags carry no ids: only type names, enum names and error codes.
- No PII in query strings: the SPA, the API and Paymob redirects must not put personal data in URLs. Where a query value is sensitive anyway (the Paymob webhook `hmac`, the audit log `actor` filter), no log keeps it: the API request log keeps query keys only, with every value replaced by `[redacted]`, and every Caddy log replaces the whole query string with `?redacted`.
- Three redaction layers:
  1. The apps do not log PII (audited in #113). The API request log drops query values, truncates client IPs to /24 or /48 and drops the Cloudflare coordinates.
  2. `LogRedactor` (email, literal or URL-encoded `%40`, and Egyptian mobile patterns, linear-time regexes) cleans the free text of browser error reports before logging.
  3. The collector's `transform/redact` processor applies the same two patterns to every log body and every (flattened) attribute before Loki, so a leak in any container is masked at ingestion. `transform/redact-traces` applies them to span and span-event attributes (exception messages included) before Tempo.
- Every Caddy log, the access log and the default logger that carries the handler error log (`http.log.error.*`, which holds the full request), drops request and response headers (cookies, `Authorization`, `X-Forwarded-For`, `Cf-Connecting-Ip`) and replaces the query string with `?redacted`, and masks client IPs to /24 or /48. Span URLs never carry query values.
- Replacement markers: `[redacted-email]`, `[redacted-phone]`.

## 13. Configuration reference

| Where | Key | Default | Notes |
|---|---|---|---|
| `.env` | `COMPOSE_PROFILES` | `ai` | add `observability` |
| `.env` | `OTLP_ENDPOINT` | empty | `http://otel-collector:4317` with the profile, or a SaaS OTLP/gRPC URL; compose passes it to `api` and `ai` |
| `.env` | `GRAFANA_ADMIN_PASSWORD` | empty | required with the profile |
| `.env` | `GRAFANA_PORT` | `3000` | loopback only |
| `.env` | `ALERTMANAGER_CONFIG_FILE` | `alertmanager.yml` | required with the profile |
| `api.env` | `Observability__ServiceName` | `elmanhg-api` | |
| compose | `Observability__ServiceVersion` | `IMAGE_TAG` | code default `dev` |
| compose | `Observability__OtlpEndpoint` | `OTLP_ENDPOINT` | must be an absolute http or https URI, or empty; anything else stops the API at startup |
| `api.env` | `Observability__OtlpHeaders` | empty | secret; `key=value,key=value` |
| `api.env` | `Observability__TraceSampleRatio` | `1.0` | 0 to 1 |
| `api.env` | `Observability__MetricExportIntervalSeconds` | `30` | 5 to 3600 |
| compose | `CoreLogging__Console__Format` | `Json` | code default `Text` |
| `api.env` | `ClientErrors__PermitLimit` / `WindowSeconds` | `30` / `60` | per IP |
| `api.env` | `ClientErrors__MessageMaxLength` / `ErrorNameMaxLength` / `StackMaxLength` / `PathMaxLength` | `500` / `100` / `4000` / `300` | keep `web/src/shared/lib/clientErrorReporter.ts` in step |
| compose | `ELMANHG_AI_OTLP_ENDPOINT` / `ELMANHG_AI_SERVICE_VERSION` | `OTLP_ENDPOINT` / `IMAGE_TAG` | |
| `ai.env` | `ELMANHG_AI_OTLP_HEADERS`, `ELMANHG_AI_OTEL_SERVICE_NAME`, `ELMANHG_AI_TRACE_SAMPLE_RATIO`, `ELMANHG_AI_METRIC_EXPORT_INTERVAL_SECONDS` | see [docs/ai-service.md](ai-service.md) | |

## 14. Retention

| Store | Retention | Where set |
|---|---|---|
| Prometheus | 35 days (the 30-day SLO window plus margin) | `--storage.tsdb.retention.time` in compose |
| Loki | 14 days | `limits_config.retention_period` in `observability/loki/config.yaml` |
| Tempo | 7 days | `compactor.compaction.block_retention` in `observability/tempo/config.yaml` |
| Docker log files | 50 MB per app container, 30 MB per telemetry container | `x-logging` anchors in compose |

The stores live in named volumes on the host (`prometheus-data`, `loki-data`, `tempo-data`, `grafana-data`, `alertmanager-data`, `otel-collector-data`). They are not backed up: telemetry is operational data.

## 15. Running it locally

`bash deploy/smoke-test.sh` runs the whole stack with the profile on (`SMOKE_OBSERVABILITY=1`, the default). Before starting it validates every config: `caddy validate`, `promtool check config`, `promtool test rules`, `amtool check-config` and `otelcol validate`. After the usual checks it asserts, within 3 minutes each, that Prometheus has the API request metrics, the API and AI HTTP and model metrics, the client error metric, the job metrics, a passing edge probe and the loaded alert rules; that Tempo has the trace of a request by its `X-Trace-Id`; that Loki has API logs with that trace id and the Caddy access log line with the email redacted; that the three dashboards are provisioned; and that Alertmanager is ready.

On Windows, Docker Desktop may not expose `/var/lib/docker/containers` to the collector; run `SMOKE_OBSERVABILITY=0 bash deploy/smoke-test.sh` to skip the profile there. CI (Linux) is authoritative. Other knobs are in [docs/deployment.md](deployment.md) §12.
