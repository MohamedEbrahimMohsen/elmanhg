# Plan: [E13.S2] Observability (#113)

## Goal
After this story ships, the team can see errors, latency, uptime and background-job health for every environment. The API (.NET) and the AI service (Python) emit OpenTelemetry traces and metrics that share one trace id end to end (browser, Caddy, API, AI). Every container writes structured JSON logs, and a self-hosted stack collects them. That stack is behind the `observability` compose profile: OTel Collector, Prometheus, Loki, Tempo, Grafana, Alertmanager and the blackbox exporter. Three provisioned Grafana dashboards cover service health against the 99.5 % SLO, background jobs, and business activity (quiz answers, payments, OTP sends, AI calls and their cost, browser errors). Prometheus rules alert on error-budget burn, service down, stale or failing jobs, and provider or OTP failures, and each rule has a `promtool` unit test. Browser errors reach the API through `POST /api/client-errors`. Phone numbers, emails, OTP codes and payment payloads are kept out of logs.

## Scope
**In:**
- **Sub-task 1, centralised logs and traces.**
  - OTel tracing and metrics in the API (ASP.NET Core, HttpClient, Npgsql, background jobs) and in the AI service (FastAPI, model and embedding calls).
  - OTLP/gRPC export, switched on by one `.env` key.
  - JSON console logs from the API.
  - JSON access logs from Caddy, with query strings and headers dropped.
  - The collector tails every compose container's stdout, redacts PII and ships the logs to Loki. Traces go to Tempo and metrics to Prometheus.
  - Grafana datasources link logs to traces.
  - Browser error reporting through the API.
- **Sub-task 2, error alerting and uptime checks against 99.5 %.**
  - Blackbox probes (edge→API→DB, API, AI).
  - A public `GET /api/health` on Caddy for an external monitor.
  - Multi-window burn-rate alerts on a 30-day request SLI.
  - Error, provider and OTP alerts, routed by Alertmanager.
  - Exceptions recorded on spans, and error codes on spans and metrics.
- **Sub-task 3, background job dashboard.**
  - Per-sweep telemetry for the three workers: runs, items, duration, last success, interval, and one span per sweep.
  - The Grafana "Background jobs" dashboard.
  - Stale and failing job alerts.
- **Business metrics:** quiz answers, payment notifications, OTP sends, AI calls, tokens and cost, and client errors.
- **Docs:** new `docs/observability.md`, plus updates to `docs/deployment.md`, `docs/ai-service.md`, `docs/PRD.md` §14/§18, `docs/constitution.md` §4 and python skill delta 5.

**Out:**
- An in-app (React) admin job dashboard. The Grafana dashboard covers sub-task 3 (Decision 9).
- Tempo metrics-generator service graphs.
- Tail sampling.
- A highly available or off-host telemetry store.
- Hangfire. The repo has no `Elmanhg.Jobs`: jobs are `BackgroundService` workers.

**Deferred** (the orchestrator opens a `deferred` issue for each):
1. **External uptime monitor and dead-man's switch** (UptimeRobot, Better Stack or Healthchecks.io polling `https://<site>/api/health`, plus an always-firing heartbeat). This needs an external account and the live domain, and #112 has no live deploy yet. The endpoint and the runbook ship now.
2. **Vendor error-tracking SDK** (Sentry: issue grouping, source maps, releases). This needs a DSN or account. What ships now: exceptions on spans, error-level logs, and browser errors through the API. Any OTLP SaaS can be reached today by setting `OTLP_ENDPOINT` and the `*_OTLP_HEADERS` keys.
3. **Live alert receiver credentials.** The Alertmanager email (Resend SMTP) and webhook receivers are documented in the example file, and the default is a null receiver. Real addresses and keys are host config the dev adds.
4. **Forwarding logs to a SaaS.** In SaaS mode, logs stay in the local Loki; the collector has no optional upstream log exporter.

## Decisions
| # | Question | Decision | Why |
|---|---|---|---|
| 1 | Exporter and protocol | OTLP over gRPC everywhere: .NET `OpenTelemetry.Exporter.OpenTelemetryProtocol` and Python `opentelemetry-exporter-otlp-proto-grpc`, to `http://otel-collector:4317`. | One protocol. The Python HTTP exporter pulls in `requests`, which python skill delta 3 forbids. |
| 2 | Export on or off | Export happens only when an endpoint is set. .NET: `Observability:OtlpEndpoint` (http/https absolute URI). AI: `ELMANHG_AI_OTLP_ENDPOINT`. Compose feeds both from the `.env` key `OTLP_ENDPOINT` (default empty). With it empty, spans and ids still exist, trace ids are still in every log line, and nothing is exported. | "Behind config with a no-op fallback". Tests and CI need no collector. |
| 3 | Backend | Self-hosted and opt-in through the compose profile `observability`, on the same host. Images: `otel/opentelemetry-collector-contrib:0.161.0`, `prom/prometheus:v3.14.0`, `prom/alertmanager:v0.34.1`, `prom/blackbox-exporter:v0.28.0`, `grafana/loki:3.7.8`, `grafana/tempo:2.10.8`, `grafana/grafana:13.1.6`. Each is pinned `tag@sha256` using `docker buildx imagetools inspect` (deployment.md §5). | Cheap (about 1.5 GB RAM), no SaaS keys, mirrors the existing `ai` profile. Every tag is at least 7 days old. |
| 4 | AGPL (Grafana, Loki, Tempo) | Accepted. They are unmodified operator tools run as separate containers, never linked or shipped as product code. The dependency policy's AGPL ban covers shipped code. | Flag this for the dev in the final report. |
| 5 | How logs are collected | Apps write JSON to stdout. The Docker json-file driver tags each line with `com.docker.compose.service`. The collector's `filelog` receiver tails `/var/lib/docker/containers/*/*-json.log` (read-only mount, collector runs as uid 0, no published port) and exports to Loki over OTLP/HTTP. No OTLP log SDK runs in any app. | One path covers api, ai, web (Caddy), postgres and migrate, including crash output. The Python OTLP log handler would mangle structlog event dicts (checked in `opentelemetry-instrumentation-logging` 0.65b0). |
| 6 | API log format | `CoreLogging:Console:Format` = `Text` (default) or `Json`, using Serilog `RenderedCompactJsonFormatter`. `Serilog.Formatting.Compact` 3.0.0 is already transitive through `Serilog.AspNetCore` 10.0.0. Compose sets `Json`. | JSON output includes `@tr`/`@sp` from `Activity`, and the collector turns them into the log's trace id. |
| 7 | Error tracking | No vendor SDK. Errors go through four paths: (a) `CoreExceptionMiddleware` adds `app.error_code` to the current span, plus an exception event for 5xx; (b) the `elmanhg.requests` metric counts every MediatR request by outcome (error code); (c) Error-level logs in Loki; (d) browser errors go to `POST /api/client-errors`, which logs a redacted Warning and increments `elmanhg.client.errors`. | No keys needed. Sentry is Deferred item 2. |
| 8 | Where business metrics are recorded (feature handlers stay untouched, lane rule) | Generic `RequestMetricsBehaviour<,>`. Closed behaviours `QuizAnswerMetricsBehaviour` (SubmitAnswer) and `PaymentNotificationMetricsBehaviour` (ProcessPaymentNotification), following the `ImportQuestionsReplayBehaviour` precedent. A one-line hook in `OtpChannelRouter`. AI calls, tokens and cost are recorded in the Python service (it knows tokens and prices), by wrapping the model and embedding clients in `lifespan`. `pipelines/chat.py` is not touched (lane A). | Zero edits to feature handlers or `IAiServiceClient` consumers. |
| 9 | "Background job dashboard" | A Grafana dashboard fed by per-sweep job metrics. No new API endpoint and no React page. | This avoids OpenAPI and Orval churn on shared files, and Grafana already needs to exist for alerts. |
| 10 | SLO definition (99.5 % monthly) | **Request SLI:** API requests with a status other than 5xx, divided by all API requests, excluding `http_route="/health"`, over 30 days. **Uptime SLI:** blackbox `probe_success` averaged over 30 days, per target. Error budget is 0.5 %. Alerts use multi-window burn rates: fast is 14.4× over 1 h and 5 m (critical), slow is 6× over 6 h and 30 m (warning). | Standard SRE practice, and it matches PRD §14. |
| 11 | Probe targets | All internal and static: `http://web:2080/api/health` (edge→API→DB), `http://api:8080/health`, `http://ai:8000/health/ready`. External TLS/DNS monitoring is Deferred item 1. | Prometheus config does no env interpolation, and internal names are the same on every host. |
| 12 | Public health endpoint | Caddy snippet `(api_health)`: `handle /api/health { rewrite * /health; reverse_proxy api:8080 }`, imported in the public site and in `:2080`. The body is only `Healthy` or `Unhealthy`. | An external monitor needs a public URL. This changes the #112 "`/health` not proxied" statement, so `docs/deployment.md` is updated in the same change. |
| 13 | Alert routing config | Alertmanager reads a host file `deploy/alertmanager.yml`, copied from `deploy/observability/alertmanager/alertmanager.example.yml`. It is not committed (gitignored), in the same pattern as `api.env`. The default receiver is null (no-op). `deploy.sh` refuses to deploy the profile without it or without `GRAFANA_ADMIN_PASSWORD`. | Alertmanager does no env expansion, and the recipients are per-host. |
| 14 | Grafana exposure | Bound to `127.0.0.1:${GRAFANA_PORT:-3000}` only, reached with `ssh -L`. Admin password comes from `.env`. Sign-up and anonymous access are off. | Nothing new is public. |
| 15 | Metric tag keys | Namespaced `elmanhg.request`, `elmanhg.outcome`, `elmanhg.channel`, `elmanhg.source`, `elmanhg.job` (Prometheus: `elmanhg_*`). Values are type names, enum names or error codes, never ids or user data. | Plain `job` would collide with Prometheus's reserved `job` label, which holds service.name. Cardinality stays bounded. |
| 16 | Request outcome values | `Success` on success. `VALIDATION_FAILED` for `ValidationBehaviourException`, whose code can be a comma-joined list. `CANCELLED` for `OperationCanceledException`. The `BaseException.ErrorCode` for other core exceptions. `UNHANDLED_EXCEPTION` otherwise. | Bounded, and matches the codes clients already see. |
| 17 | Durations | Measured with `Stopwatch.GetTimestamp()` / `Stopwatch.GetElapsedTime`. Wall-clock "last success" uses the injected `TimeProvider`. | Monotonic. The repo's `Substitute.For<TimeProvider>()` returns 0 frequency, which would break `GetElapsedTime`. |
| 18 | Job "last success" | Set to now at `Register(job, interval)`, and on each sweep whose outcome is `Succeeded` or `PartiallyFailed`. A `Failed` sweep (listing threw) does not advance it. The stale alert fires at more than 3× the interval. Item failures have their own alert. | A job that never runs or never lists alerts. Poison items do not mask stalls. |
| 19 | PII redaction | Three layers: (1) apps never log PII (audited: only `FakeOtpChannel` in Development logs a recipient or code); (2) `LogRedactor` (email and Egyptian-mobile regexes, `NonBacktracking`) on client-error text; (3) the collector's `transform/redact` applies the same two patterns to every log body and attribute. Caddy drops `request>headers` and `resp_headers` and replaces the query string with `?redacted` (Paymob redirect queries). ASP.NET and HttpClient span instrumentation redact query values by default. | Defence in depth. OTP codes (6 digits) cannot be pattern-redacted without destroying ids, so the rule is "never logged", documented. |
| 20 | Where the .NET telemetry is registered | `Elmanhg.Api/Hosting/ObservabilityExtensions.AddElmanhgObservability(configuration, environment)`, called in Program right after `AddCoreAuditing` and before `AddCoreCQRS`. It binds `ObservabilityOptions` itself: the exporter must read the options at registration time, the same precedent as `Core.Logging`. `ClientErrorsOptions` (read by the validator and the rate limiter) is bound in `AddApplication`, next to `AnalyticsOptions`. | Keeps `AuditBehaviour` outermost and lets `RequestMetricsBehaviour` see validation failures. One Program line. |
| 21 | Runtime metrics | `.AddMeter("System.Runtime")` (built into .NET 9+), not the `OpenTelemetry.Instrumentation.Runtime` package. Traces add source `"Npgsql"` (Npgsql's built-in ActivitySource). | Fewer dependencies. |
| 22 | AI semconv | Model metrics follow the OTel GenAI conventions: `gen_ai.client.operation.duration` (s), `gen_ai.client.token.usage` ({token}), plus a custom `elmanhg.ai.cost` counter ({USD}). Attributes: `gen_ai.operation.name` (chat or embeddings), `gen_ai.provider.name` (the `llm_provider` / `embedding_provider` setting), `gen_ai.request.model`, `gen_ai.token.type`, and `error.type` on failure. Compose sets `OTEL_SEMCONV_STABILITY_OPT_IN=http` for ai, so FastAPI emits `http.server.request.duration`. | Standard names; the dashboards match them. |
| 23 | Service names | `elmanhg-api` (`Observability:ServiceName`) and `elmanhg-ai` (`ELMANHG_AI_OTEL_SERVICE_NAME`). The collector names log streams `elmanhg-<compose service>`. `service.version` comes from `IMAGE_TAG` through compose. | Loki `service_name` then equals the trace `service.name`. |
| 24 | Retention | Prometheus 35 d (a 30 d SLO window plus margin), Loki 14 d (336 h), Tempo 7 d (168 h). | Fixed in the committed config and documented. |
| 25 | Web client-error limits | Constants in `clientErrorReporter.ts` mirror the `ClientErrors` defaults (message 500, name 100, stack 4000, path 300). At most 10 reports per page load, and each identical message only once. Reports use `window.location.pathname` only, with no query or hash. `ApiError` and `AbortError` are not reported. | Server errors are already logged server-side, and this avoids storms. |
| 26 | Endpoint access | `POST /api/client-errors` is `[AllowAnonymous]` and rate-limited per IP by policy `client-errors`, mirroring `funnel-events`. It returns 200. | Errors happen before sign-in too. |
| 27 | Smoke-test coverage | `deploy/smoke-test.sh` enables the profile by default (`SMOKE_OBSERVABILITY=1`). It validates every config (`promtool`, `amtool`, `otelcol validate`, `caddy validate`) and asserts end to end that metrics, traces, logs (by trace id), redaction, probes, rules and dashboards arrive. On Windows, Docker Desktop may not expose `/var/lib/docker/containers`, so `SMOKE_OBSERVABILITY=0` skips the part locally. CI (Linux) is authoritative. | Configs are only proven by running them. |

## Existing code touched
| File | Change |
|---|---|
| `api/Directory.Packages.props` | Add `PackageVersion`s: `OpenTelemetry.Extensions.Hosting` 1.19.1, `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.19.1, `OpenTelemetry.Instrumentation.AspNetCore` 1.19.0, `OpenTelemetry.Instrumentation.Http` 1.19.0. Under the Testing group, add `Microsoft.Extensions.Diagnostics.Testing` 10.7.0. |
| `api/Elmanhg.Api/Elmanhg.Api.csproj` | Add `PackageReference`s (no Version) for the four OTel packages. |
| `api/Elmanhg.Tests/Elmanhg.Tests.csproj` | Add `PackageReference Include="Microsoft.Extensions.Diagnostics.Testing"`. |
| `api/Elmanhg.Api/Program.cs` | After `builder.Services.AddCoreAuditing(builder.Configuration);` insert the comment `// After auditing (AuditBehaviour stays outermost), before CQRS so RequestMetricsBehaviour also sees validation failures.` and the line `builder.Services.AddElmanhgObservability(builder.Configuration, builder.Environment);`. Add `using Elmanhg.Api.Hosting;` if it is not already present (it is). Change nothing else. |
| `api/core-libraries/Core.Logging/LoggingOptions.cs` | Add `public enum ConsoleLogFormat { Text, Json }` to this file. On `ConsoleLoggingOptions`, add `public ConsoleLogFormat Format { get; set; } = ConsoleLogFormat.Text;`. |
| `api/core-libraries/Core.Logging/DependencyInjection.cs` | In `ConfigureConsole`: when `options.Format == ConsoleLogFormat.Json`, use `a.Console(new RenderedCompactJsonFormatter(), restrictedToMinimumLevel: options.MinimumLevel)` (`using Serilog.Formatting.Compact;`). Otherwise keep the current call. |
| `api/core-libraries/Core.Exceptions/ExceptionMiddleware.cs` | Add `public const string ErrorCodeTag = "app.error_code";` to the class. In `HandleExceptionAsync`, after computing `exceptionDetails`, call `Activity.Current?.SetTag(ErrorCodeTag, exceptionDetails.Code);`. In the non-client-error (5xx) branch also call `Activity.Current?.AddException(exception);` (`using System.Diagnostics;`). |
| `api/Elmanhg.Api/Workers/ExpiredExamSubmissionWorker.cs` | Add ctor param `BackgroundJobMetrics jobMetrics` (last position). Add `private const string JobName = "exam-auto-submit";`. In `ExecuteAsync`, after the enabled check, add `jobMetrics.Register(JobName, TimeSpan.FromSeconds(options.AutoSubmitIntervalSeconds));`. `ListExpiredAsync` returns `Task<List<Guid>?>`, and its catch returns `null`. `SweepAsync` becomes: `using var run = jobMetrics.StartRun(JobName);` then `var sessionIds = await ListExpiredAsync(stoppingToken).ConfigureAwait(false); if (sessionIds is null) { run.MarkListingFailed(); } sessionIds ??= [];`. The existing `Count < batchSize` clear and the loop stay unchanged, except that the loop calls `run.ItemSucceeded()` when `SubmitAsync` returns true and `run.ItemFailed()` before `_deferredIds.Add`. |
| `api/Elmanhg.Api/Workers/SubscriptionLapseWorker.cs` | Same pattern. `JobName = "subscription-lapse"`, interval `LapseSweepIntervalSeconds`, method `ListLapsedAsync`. |
| `api/Elmanhg.Api/Workers/LessonContentIndexWorker.cs` | Same pattern. `JobName = "lesson-content-index"`, interval `IndexSweepIntervalSeconds`, method `ListStaleAsync`. |
| `api/Elmanhg.Api/RateLimiting/AuthRateLimiting.cs` | After the analytics line, add `services.AddOptions<RateLimiterOptions>().Configure<IOptions<ClientErrorsOptions>>((rateLimiter, clientErrorsOptions) => rateLimiter.AddPolicy(ObservabilityRateLimitPolicies.ClientErrors, httpContext => CreateFixedWindow(httpContext, clientErrorsOptions.Value.PermitLimit, clientErrorsOptions.Value.WindowSeconds)));`. |
| `api/Elmanhg.Infrastructure/OtpDelivery/OtpChannelRouter.cs` | Add ctor param `ElmanhgMetrics metrics` (last). `SendAsync`: `var channel = Resolve(recipientType); try { await channels.Single(...).SendAsync(...).ConfigureAwait(false); } catch (Exception) when (!cancellationToken.IsCancellationRequested) { metrics.RecordOtpSend(channel, delivered: false); throw; } metrics.RecordOtpSend(channel, delivered: true); return channel;`. |
| `api/Elmanhg.Application/DependencyInjection.cs` | After the `AnalyticsOptions` line, add `services.AddOptions<ClientErrorsOptions>().BindConfiguration(ClientErrorsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add a `// OBSERVABILITY` group with 7 constants (see Error codes), directly **after** the `// ANALYTICS` group (not at the end, to reduce lane merge conflicts). |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Add 7 `<data>` entries directly after `FUNNEL_EVENT_TYPE_INVALID`. |
| `api/Elmanhg.Api/appsettings.example.json` | In `CoreLogging.Console`, add `"Format": "Text"`. After `"Analytics"`, add `"ClientErrors": { "PermitLimit": 30, "WindowSeconds": 60, "MessageMaxLength": 500, "ErrorNameMaxLength": 100, "StackMaxLength": 4000, "PathMaxLength": 300 }` and `"Observability": { "ServiceName": "elmanhg-api", "ServiceVersion": "dev", "OtlpEndpoint": "", "OtlpHeaders": "", "TraceSampleRatio": 1.0, "MetricExportIntervalSeconds": 30 }`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | In the in-memory dictionary, after the `Analytics:*` lines, add `ClientErrors:PermitLimit`=`1000`, `ClientErrors:WindowSeconds`=`60`, `ClientErrors:MessageMaxLength`=`500`, `ClientErrors:ErrorNameMaxLength`=`100`, `ClientErrors:StackMaxLength`=`4000`, `ClientErrors:PathMaxLength`=`300`. |
| `api/Elmanhg.Tests/Api/Workers/ExpiredExamSubmissionWorkerTests.cs`, `SubscriptionLapseWorkerTests.cs`, `LessonContentIndexWorkerTests.cs` | Modify: add a field `private readonly IMeterFactory _meterFactory = MeterFactories.Create();`. The `RunAsync` helper passes `new BackgroundJobMetrics(_meterFactory, _time)` to the worker ctor. Add the new tests from the Test plan. Existing tests are unchanged. |
| `api/Elmanhg.Tests/Infrastructure/OtpDelivery/OtpChannelRouterTests.cs` | Modify: `Router()` passes `new ElmanhgMetrics(_meterFactory)`, using a new field `_meterFactory = MeterFactories.Create()`. Add 2 tests. |
| `api/Elmanhg.Tests/Infrastructure/OtpDelivery/OtpDeliveryServiceCollectionExtensionsTests.cs` | Modify `BuildProvider`: add `services.AddMetrics(); services.AddSingleton<ElmanhgMetrics>();` before `AddOtpDelivery()`. |
| `api/Elmanhg.Tests/Core/Exceptions/CoreExceptionMiddlewareTests.cs` | Add 2 tests. |
| `api/Elmanhg.Tests/Integration/Composition/PipelineCompositionTests.cs` | Add 3 tests. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`: new path, request schema and the `ClientErrorSource` enum. |
| `postman/elmanhg.postman_collection.json` | Add a folder `Observability` at the end, with request `Report client error`: `POST {{baseUrl}}/api/client-errors`, body `{"message":"TypeError: x is undefined","errorName":"TypeError","stack":"at App (main.js:1:1)","source":"Window","path":"/student"}`. Mirror the Analytics folder's shape. |
| `web/src/main.tsx` | After `initI18n();`, add `clientErrorReporter.install(window);` (import from `@/shared/lib/clientErrorReporter`). |
| `web/src/shared/components/RouteError.tsx` | Add `useEffect(() => { clientErrorReporter.report(error, 'Route'); }, [error]);`. `report` itself skips `ApiError` and `AbortError`. |
| `web/src/shared/components/RouteError.test.tsx` | Add `beforeEach(() => clientErrorReporter.reset())` and 2 tests. |
| `web/src/test/msw/server.ts` | Add the default handler `getReportClientErrorMockHandler()` from the generated `client-errors` msw file (use the path Orval produces). |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api`. |
| `ai/pyproject.toml` | Add to `dependencies`: `"opentelemetry-api==1.44.0"`, `"opentelemetry-sdk==1.44.0"`, `"opentelemetry-exporter-otlp-proto-grpc==1.44.0"`, `"opentelemetry-instrumentation-fastapi==0.65b0"`. If mypy strict reports a missing `py.typed` for a module, add `[[tool.mypy.overrides]] module = ["<that module>"] ignore_missing_imports = true` for that module only. |
| `ai/uv.lock` | Regenerated with `uv lock` (respects `exclude-newer = 2026-09-22`). |
| `ai/src/elmanhg_ai/settings.py` | Add 6 fields and 2 validators (see Files to create, Python). |
| `ai/src/elmanhg_ai/core/middleware.py` | Add `current_span_trace_id() -> str | None`, which returns `trace.format_trace_id(ctx.trace_id)` when `trace.get_current_span().get_span_context().is_valid`, else `None`. In `dispatch`: `trace_id = current_span_trace_id() or trace_id_from(request.headers.get("traceparent")) or secrets.token_hex(16)`. |
| `ai/src/elmanhg_ai/main.py` | `create_app(..., telemetry: Telemetry | None = None)`: `telemetry = telemetry or build_telemetry(settings)`, `app.state.telemetry = telemetry`, `instrument_app(app, telemetry)` after `add_middleware`. In `lifespan`, after building the clients: `metrics = AiMetrics(telemetry.meter_provider.get_meter("elmanhg_ai"))` and `tracer = telemetry.tracer_provider.get_tracer("elmanhg_ai")`; `app.state.model_client = MeteredModelClient(model_client, metrics=metrics, tracer=tracer, settings=settings)`; the same for embeddings with `MeteredEmbeddingClient`. Add `otlp_exporting=telemetry.exporting` to the `service.started` log. In `finally`, after the `aclose`s, call `telemetry.shutdown()`. |
| `ai/tests/unit/test_settings.py` | Add 4 tests. |
| `deploy/docker-compose.prod.yml` | See the compose spec below. |
| `deploy/Caddyfile` | Add the `(api_health)` snippet, the access `log` block, and `import api_health` in both sites (spec below). |
| `deploy/.env.example` | Add the Observability block (spec below). |
| `deploy/api.env.example` | Add a commented block: `# Observability (docs/observability.md). OTLP_ENDPOINT in .env switches export on; headers only for a SaaS endpoint (secret).` and `# Observability__OtlpHeaders=authorization=Bearer <token>`, `# Observability__TraceSampleRatio=1.0`. Also `# ClientErrors__PermitLimit=30`. |
| `deploy/ai.env.example` | Add a commented block: `# ELMANHG_AI_OTLP_HEADERS=authorization=Bearer <token>` (secret) and `# ELMANHG_AI_TRACE_SAMPLE_RATIO=1.0`. |
| `deploy/lib.sh` | Add `profile_enabled <name>` and `check_observability_config` (spec below). |
| `deploy/deploy.sh` | Call `check_observability_config` right after the tag regex check. After the `ai` wait, add `for service in prometheus alertmanager blackbox loki grafana; do if [ -n "$(compose ps -q "$service")" ]; then wait_healthy "$service"; fi; done`. List only services that have a healthcheck (see Decision 3 notes). |
| `deploy/smoke-test.sh` | Observability section (spec below). |
| `.github/workflows/images.yml` | `deploy-smoke.timeout-minutes: 40`. |
| `.github/workflows/deploy.yml` | Upload step: `scp -r -i ~/.ssh/deploy_key deploy/docker-compose.prod.yml deploy/Caddyfile deploy/lib.sh deploy/deploy.sh deploy/backup.sh deploy/restore.sh deploy/observability "$DEPLOY_USER@$DEPLOY_HOST:$DEPLOY_PATH/"`. |
| `.gitignore` | Add `/deploy/alertmanager.yml`. |
| `docs/deployment.md` | §1: add a row per observability service (profile `observability`), and the public `/api/health`. §3: add `alertmanager.yml` to the host files table. §4: add a `.env` block (`OTLP_ENDPOINT`, `GRAFANA_ADMIN_PASSWORD`, `GRAFANA_PORT`, `ALERTMANAGER_CONFIG_FILE`) and the Observability/ClientErrors `api.env` keys; add to "Set by compose" `Observability__OtlpEndpoint`, `Observability__ServiceVersion`, `CoreLogging__Console__Format`. §6: host sizing (8 GB RAM recommended with the profile) and a step to copy `alertmanager.yml`. §7: the workflow uploads `observability/`. §10: add the observability health checks. Replace "the API's `/health` is not proxied" with "`/api/health` is public (rewritten to `/health`)"; replace "Monitoring and alerting come with #113" with a link to docs/observability.md. §12: knobs `SMOKE_OBSERVABILITY` (default 1) and `SMOKE_GRAFANA_PORT` (3300). §13: the Observability row becomes "done; external uptime monitor, Sentry and live alert receivers deferred (#…)". |
| `docs/ai-service.md` | Configuration table: add the 6 `ELMANHG_AI_*` telemetry keys. Rename "Health and logging" to "Health, logging and telemetry" and add the spans (FastAPI server, `chat <model>` / `embeddings <model>` client), the metrics (Decision 22), and the fact that the trace id comes from the current span, falling back to `traceparent`. |
| `docs/PRD.md` | §14 Availability: append "measured and alerted as in docs/observability.md". §18: add the bullet "**Observability**: OpenTelemetry traces and metrics (api, ai), JSON logs from every container, self-hosted collector + Prometheus + Loki + Tempo + Grafana + Alertmanager behind the `observability` compose profile; runbook docs/observability.md." |
| `docs/constitution.md` | In §4 (after the Errors bullet), add: "Observability: telemetry is wired only in `Elmanhg.Api/Hosting/ObservabilityExtensions.cs` and `ai/src/elmanhg_ai/core/telemetry.py`; business metrics go through `ElmanhgMetrics`/`BackgroundJobMetrics` or a pipeline behaviour, never ad-hoc `Meter`s; logs, span attributes and metric tags never carry phone numbers, emails, OTP codes, tokens or payment payloads, and metric tags carry no ids (docs/observability.md)." |
| `.claude/skills/python-feature/SKILL.md` | Replace delta 5 with: "OpenTelemetry (from E13.S2): `core/telemetry.py` builds the tracer and meter providers (OTLP/gRPC only when `ELMANHG_AI_OTLP_ENDPOINT` is set); FastAPI is instrumented there; model and embedding calls are measured by the `clients/metered.py` wrappers applied in `lifespan`. Do not add OTLP log handlers: logs leave through stdout (docs/observability.md)." |

## Files to create

### .NET: Application (`api/Elmanhg.Application/`)
| # | Path | Type | Contract |
|---|---|---|---|
| 1 | `Shared/Observability/ElmanhgTelemetry.cs` | `public static class ElmanhgTelemetry` in namespace `Elmanhg.Application.Shared.Observability` | `public const string SourceName = "Elmanhg";` and `public static readonly ActivitySource ActivitySource = new(SourceName);`. |
| 2 | `Shared/Observability/ElmanhgMetrics.cs` | `public sealed class ElmanhgMetrics` | **Tag constants:** `RequestTag = "elmanhg.request"`, `OutcomeTag = "elmanhg.outcome"`, `ChannelTag = "elmanhg.channel"`, `SourceTag = "elmanhg.source"`, `SuccessOutcome = "Success"`, `DeliveredOutcome = "Delivered"`, `FailedOutcome = "Failed"`.<br>**Constructor:** `ElmanhgMetrics(IMeterFactory meterFactory)` creates `meterFactory.Create(ElmanhgTelemetry.SourceName)` and the instruments:<ul><li>`Counter<long> "elmanhg.requests"` unit `{request}`</li><li>`Histogram<double> "elmanhg.request.duration"` unit `s`</li><li>`Counter<long> "elmanhg.quiz.answers"` unit `{answer}`</li><li>`Counter<long> "elmanhg.payment.notifications"` unit `{notification}`</li><li>`Counter<long> "elmanhg.otp.sends"` unit `{message}`</li><li>`Counter<long> "elmanhg.client.errors"` unit `{error}`</li></ul>Every instrument has a one-sentence description.<br>**Methods:**<ul><li>`void RecordRequest(string requestName, string outcome, TimeSpan duration)`: counter +1 and histogram `duration.TotalSeconds`, both tagged Request and Outcome.</li><li>`void RecordQuizAnswer(string outcome)`</li><li>`void RecordPaymentNotification(PaymentNotificationOutcome outcome)`: Outcome = `outcome.ToString()`.</li><li>`void RecordOtpSend(OtpChannel channel, bool delivered)`: Channel = `channel.ToString()`, Outcome = Delivered or Failed.</li><li>`void RecordClientError(ClientErrorSource source)`: Source = `source.ToString()`.</li></ul> |
| 3 | `Shared/Observability/BackgroundJobMetrics.cs` | `public sealed class BackgroundJobMetrics` | **Constants:** `JobTag = "elmanhg.job"`, `OutcomeTag = "elmanhg.outcome"`.<br>**Constructor:** `BackgroundJobMetrics(IMeterFactory meterFactory, TimeProvider timeProvider)` creates the meter `ElmanhgTelemetry.SourceName` and the instruments:<ul><li>`Counter<long> "elmanhg.job.runs"` `{run}`</li><li>`Histogram<double> "elmanhg.job.duration"` `s`</li><li>`Counter<long> "elmanhg.job.items"` `{item}`</li><li>`ObservableGauge<long> "elmanhg.job.last_success"` `s`: one measurement per registered job, value = unix seconds, tag Job.</li><li>`ObservableGauge<double> "elmanhg.job.interval"` `s`: one per job.</li></ul>State lives in a `ConcurrentDictionary<string, JobState>`, where `JobState` is a private sealed class with `long LastSuccessUnixSeconds` (set with `Interlocked.Exchange`) and `double IntervalSeconds`.<br>**Methods:**<ul><li>`void Register(string jobName, TimeSpan interval)`: adds or replaces with interval and last success = `timeProvider.GetUtcNow().ToUnixTimeSeconds()`.</li><li>`BackgroundJobRun StartRun(string jobName)`: returns `new BackgroundJobRun(this, jobName, Stopwatch.GetTimestamp(), ElmanhgTelemetry.ActivitySource.StartActivity($"job {jobName}"))` and sets tag Job on the activity.</li><li>`internal void Complete(BackgroundJobRun run)`: records runs +1 {Job, Outcome=run.Outcome}; duration seconds {Job, Outcome}; items with Outcome `Succeeded` = run.SucceededItems and Outcome `Failed` = run.FailedItems (only when > 0). When the outcome is not `Failed` and the job is registered, it sets last success to now. It sets activity tag Outcome, sets `ActivityStatusCode.Error` when the outcome is `Failed`, and disposes the activity.</li></ul> |
| 4 | `Shared/Observability/BackgroundJobRun.cs` | `public sealed class BackgroundJobRun : IDisposable` and `public enum BackgroundJobRunOutcome { Succeeded, PartiallyFailed, Failed }` (same file) | **Constructor:** `internal BackgroundJobRun(BackgroundJobMetrics metrics, string jobName, long startedTimestamp, Activity? activity)`.<br>**Properties:** `JobName`, `int SucceededItems`, `int FailedItems`, `internal long StartedTimestamp`, `internal Activity? Activity`, `BackgroundJobRunOutcome Outcome => _listingFailed ? Failed : FailedItems > 0 ? PartiallyFailed : Succeeded`.<br>**Methods:** `MarkListingFailed()`, `ItemSucceeded()`, `ItemFailed()`, and `Dispose()`, which calls `metrics.Complete(this)` once (guard it with a `_completed` flag). |
| 5 | `Shared/Observability/RequestMetricsBehaviour.cs` | `public sealed class RequestMetricsBehaviour<TRequest, TResponse>(ElmanhgMetrics metrics) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull` | `Handle`: `var started = Stopwatch.GetTimestamp(); var outcome = ElmanhgMetrics.SuccessOutcome; try { return await next(cancellationToken).ConfigureAwait(false); } catch (Exception exception) { outcome = OutcomeOf(exception); throw; } finally { metrics.RecordRequest(typeof(TRequest).Name, outcome, Stopwatch.GetElapsedTime(started)); }`.<br>`private static string OutcomeOf(Exception exception) => exception switch { ValidationBehaviourException => "VALIDATION_FAILED", OperationCanceledException => "CANCELLED", BaseException { ErrorCode: { Length: > 0 } code } => code, _ => ExceptionErrorCodes.UnhandledException };`. If Application does not reference `Core.Exceptions`, use a private const `UnhandledOutcome = "UNHANDLED_EXCEPTION"`; do not add the reference. |
| 6 | `Shared/Observability/QuizAnswerMetricsBehaviour.cs` | `public sealed class QuizAnswerMetricsBehaviour(ElmanhgMetrics metrics) : IPipelineBehavior<SubmitAnswerCommand, SessionItemResult>` | `private const string UngradedOutcome = "Ungraded";`. `Handle`: `var result = await next(cancellationToken).ConfigureAwait(false); metrics.RecordQuizAnswer(result.Attempt?.Outcome ?? UngradedOutcome); return result;`. |
| 7 | `Shared/Observability/PaymentNotificationMetricsBehaviour.cs` | `public sealed class PaymentNotificationMetricsBehaviour(ElmanhgMetrics metrics) : IPipelineBehavior<ProcessPaymentNotificationCommand, PaymentNotificationResult>` | `Handle`: `var result = await next(...).ConfigureAwait(false); metrics.RecordPaymentNotification(result.Outcome); return result;`. |
| 8 | `Shared/Observability/LogRedactor.cs` | `public static class LogRedactor` | Constants `EmailReplacement = "[redacted-email]"` and `PhoneReplacement = "[redacted-phone]"`.<br>`private static readonly Regex EmailPattern = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.NonBacktracking);`<br>`private static readonly Regex PhonePattern = new(@"(\+?20)?0?1[0125][0-9]{8}\b", RegexOptions.NonBacktracking);` (comment: Egyptian mobile numbers, local or +20).<br>`[return: NotNullIfNotNull(nameof(value))] public static string? Redact(string? value) => value is null ? null : PhonePattern.Replace(EmailPattern.Replace(value, EmailReplacement), PhoneReplacement);` |
| 9 | `Shared/Observability/ClientErrorSource.cs` | `public enum ClientErrorSource { Window, UnhandledRejection, Route }` | |
| 10 | `Shared/Options/ClientErrorsOptions.cs` | `public sealed class ClientErrorsOptions` | `SectionName = "ClientErrors"`.<br>`[Range(1, int.MaxValue)] int PermitLimit = 30`, `[Range(1, int.MaxValue)] int WindowSeconds = 60`, `[Range(1, 10000)] int MessageMaxLength = 500`, `[Range(1, 1000)] int ErrorNameMaxLength = 100`, `[Range(1, 100000)] int StackMaxLength = 4000`, `[Range(1, 2048)] int PathMaxLength = 300`. |
| 11 | `Observability/ReportClientError/ReportClientErrorCommand.cs` | `public sealed record ReportClientErrorCommand(string Message, string? ErrorName, string? Stack, ClientErrorSource Source, string? Path) : IRequest;` | Not auditable. |
| 12 | `Observability/ReportClientError/ReportClientErrorValidator.cs` | `public sealed class ReportClientErrorValidator : AbstractValidator<ReportClientErrorCommand>`, ctor `(IOptions<ClientErrorsOptions> clientErrorsOptions)` | <ul><li>`Message`: `.ValidateRequired(ErrorCodes.ClientErrorMessageRequired).ValidateMaxLength(options.MessageMaxLength, ErrorCodes.ClientErrorMessageTooLong)`</li><li>`ErrorName`: `.ValidateMaxLength(options.ErrorNameMaxLength, ErrorCodes.ClientErrorNameTooLong)`</li><li>`Stack`: `.ValidateMaxLength(options.StackMaxLength, ErrorCodes.ClientErrorStackTooLong)`</li><li>`Path`: `.ValidateMaxLength(options.PathMaxLength, ErrorCodes.ClientErrorPathTooLong)` and `.Must(x => x is null \|\| x.StartsWith('/')).WithErrorCode(ErrorCodes.ClientErrorPathInvalid)`</li><li>`Source`: `.IsInEnum().WithErrorCode(ErrorCodes.ClientErrorSourceInvalid)`</li></ul>Check that `ValidateMaxLength` accepts `string?` without failing on null, as in the existing validators. If it does not, wrap the rule in `.When(x => x.ErrorName is not null)` (and likewise for the other nullable fields). |
| 13 | `Observability/ReportClientError/ReportClientErrorHandler.cs` | `public sealed class ReportClientErrorHandler(ElmanhgMetrics metrics, ICurrentUserService currentUserService, ILogger<ReportClientErrorHandler> logger) : IRequestHandler<ReportClientErrorCommand>` | `Handle`, in order:<ol><li>`var userId = currentUserService.UserId == null \|\| currentUserService.UserId == default ? null : currentUserService.UserId;`</li><li>`metrics.RecordClientError(request.Source);`</li><li>`logger.LogWarning("Client error from {ClientErrorSource} at {ClientErrorPath} for user {UserId}: {ClientErrorName}: {ClientErrorMessage}{NewLine}{ClientErrorStack}", request.Source, LogRedactor.Redact(request.Path), userId, LogRedactor.Redact(request.ErrorName), LogRedactor.Redact(request.Message), Environment.NewLine, LogRedactor.Redact(request.Stack));`</li><li>`return Task.CompletedTask;` (not `async`)</li></ol> |

### .NET: Infrastructure and API
| # | Path | Type | Contract |
|---|---|---|---|
| 14 | `api/Elmanhg.Infrastructure/Hosting/ObservabilityOptions.cs` | `public sealed class ObservabilityOptions` | `SectionName = "Observability"`.<br>`[Required] string ServiceName = "elmanhg-api"`, `[Required] string ServiceVersion = "dev"`, `string OtlpEndpoint = ""`, `string OtlpHeaders = ""`, `[Range(0.0, 1.0)] double TraceSampleRatio = 1.0`, `[Range(5, 3600)] int MetricExportIntervalSeconds = 30`.<br>`public Uri? ExportEndpoint => Uri.TryCreate(OtlpEndpoint, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp \|\| uri.Scheme == Uri.UriSchemeHttps) ? uri : null;` |
| 15 | `api/Elmanhg.Infrastructure/Hosting/ObservabilityOptionsValidator.cs` | `public sealed class ObservabilityOptionsValidator : IValidateOptions<ObservabilityOptions>` | Mirror `ReverseProxyOptionsValidator`. Failures:<ul><li>OtlpEndpoint not blank and `ExportEndpoint is null`: `"Observability:OtlpEndpoint '<value>' must be an absolute http or https URI such as http://otel-collector:4317."`</li><li>OtlpHeaders not blank and any comma-separated item has no `=` or a blank key: `"Observability:OtlpHeaders must be comma-separated key=value pairs."` (never echo the header value).</li></ul> |
| 16 | `api/Elmanhg.Api/Hosting/ObservabilityExtensions.cs` | `public static class ObservabilityExtensions` | `public static IServiceCollection AddElmanhgObservability(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)`, in order:<ol><li>`AddOptions<ObservabilityOptions>().BindConfiguration(SectionName).ValidateDataAnnotations().ValidateOnStart()` and `AddSingleton<IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator>()`.</li><li>`AddSingleton<ElmanhgMetrics>()`, `AddSingleton<BackgroundJobMetrics>()`.</li><li>`AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestMetricsBehaviour<,>))`, `AddTransient<IPipelineBehavior<SubmitAnswerCommand, SessionItemResult>, QuizAnswerMetricsBehaviour>()`, `AddTransient<IPipelineBehavior<ProcessPaymentNotificationCommand, PaymentNotificationResult>, PaymentNotificationMetricsBehaviour>()`.</li><li>`var options = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ?? new ObservabilityOptions();`</li><li>`services.AddOpenTelemetry().ConfigureResource(resource => resource.AddService(options.ServiceName, serviceVersion: options.ServiceVersion).AddAttributes([new("deployment.environment.name", environment.EnvironmentName)])).WithTracing(tracing => ConfigureTracing(tracing, options)).WithMetrics(metrics => ConfigureMetrics(metrics, options));`</li><li>Return services.</li></ol>`ConfigureTracing`: `.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.TraceSampleRatio))).AddAspNetCoreInstrumentation(x => x.Filter = context => !context.Request.Path.StartsWithSegments(HealthPath)).AddHttpClientInstrumentation(x => x.RecordException = true).AddSource(ElmanhgTelemetry.SourceName).AddSource(NpgsqlSourceName)`. If `options.ExportEndpoint is { } endpoint`, add `.AddOtlpExporter(exporter => Configure(exporter, options, endpoint))`.<br>`ConfigureMetrics`: `.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddMeter(ElmanhgTelemetry.SourceName).AddMeter(RuntimeMeterName)`. When exporting: `.AddOtlpExporter((exporter, reader) => { Configure(exporter, options, endpoint); reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.MetricExportIntervalSeconds * MillisecondsPerSecond; })`.<br>`Configure`: `exporter.Endpoint = endpoint; exporter.Protocol = OtlpExportProtocol.Grpc; if (!string.IsNullOrWhiteSpace(options.OtlpHeaders)) exporter.Headers = options.OtlpHeaders;`.<br>Consts: `HealthPath = "/health"`, `NpgsqlSourceName = "Npgsql"`, `RuntimeMeterName = "System.Runtime"`, `MillisecondsPerSecond = 1000`. Keep the file at about 80 lines or less. |
| 17 | `api/Elmanhg.Api/RateLimiting/ObservabilityRateLimitPolicies.cs` | `public static class ObservabilityRateLimitPolicies` | `public const string ClientErrors = "client-errors";` |
| 18 | `api/Elmanhg.Api/Controllers/Observability/ClientErrorsController.cs` | `[ApiController] [Route("api/client-errors")] [Authorize] public class ClientErrorsController(IMediator mediator) : ControllerBase` | `[HttpPost(Name = "ReportClientError")] [AllowAnonymous] [EnableRateLimiting(ObservabilityRateLimitPolicies.ClientErrors)] [ProducesResponseType(StatusCodes.Status200OK)] public async Task<ActionResult> Report([FromBody] ReportClientErrorRequest request, CancellationToken cancellationToken)`: send `new ReportClientErrorCommand(request.Message, request.ErrorName, request.Stack, request.Source, request.Path)`, then `return Ok();`. |
| 19 | `api/Elmanhg.Api/Controllers/Observability/Requests.cs` | `public sealed record ReportClientErrorRequest(string Message, string? ErrorName, string? Stack, ClientErrorSource Source, string? Path);` | |

### .NET: tests (`api/Elmanhg.Tests/`)
| # | Path | Type | Contract |
|---|---|---|---|
| 20 | `Application/Features/Shared/Observability/MeterFactories.cs` | `internal static class MeterFactories` | `public static IMeterFactory Create() => new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();` |
| 21 | `Application/Features/Shared/Observability/ElmanhgMetricsTests.cs` | tests | see Test plan |
| 22 | `Application/Features/Shared/Observability/BackgroundJobMetricsTests.cs` | tests | |
| 23 | `Application/Features/Shared/Observability/RequestMetricsBehaviourTests.cs` | tests (+ `public sealed record MetricsProbeRequest : IRequest<Unit>;` in the file) | |
| 24 | `Application/Features/Shared/Observability/QuizAnswerMetricsBehaviourTests.cs` | tests | |
| 25 | `Application/Features/Shared/Observability/PaymentNotificationMetricsBehaviourTests.cs` | tests | |
| 26 | `Application/Features/Shared/Observability/LogRedactorTests.cs` | tests | |
| 27 | `Application/Features/Observability/ReportClientError/ReportClientErrorHandlerTests.cs` | tests | |
| 28 | `Application/Features/Observability/ReportClientError/ReportClientErrorValidatorTests.cs` | tests | |
| 29 | `Infrastructure/Hosting/ObservabilityOptionsValidatorTests.cs` | tests | |
| 30 | `Integration/Observability/ClientErrorsEndpointTests.cs` | tests | |
| 31 | `Integration/Observability/TelemetryRegistrationTests.cs` | tests | |

### Python (`ai/`)
| # | Path | Contract |
|---|---|---|
| 32 | `src/elmanhg_ai/settings.py` (modify, listed here for the contract) | New fields:<ul><li>`otlp_endpoint: str \| None = None`</li><li>`otlp_headers: SecretStr \| None = None`</li><li>`otel_service_name: str = Field(default="elmanhg-ai", min_length=1)`</li><li>`service_version: str = Field(default="dev", min_length=1)`</li><li>`trace_sample_ratio: float = Field(default=1.0, ge=0, le=1)`</li><li>`metric_export_interval_seconds: int = Field(default=30, ge=5, le=3600)`</li></ul>`@field_validator("otlp_endpoint")`: strip, then `""` becomes `None`; otherwise the value must start with `http://` or `https://`, else `ValueError("otlp_endpoint must be an http or https URL")`.<br>`@field_validator("otlp_headers")`: `None` or blank becomes `None`; every comma item must contain `=` with a non-blank key, else `ValueError("otlp_headers must be comma-separated key=value pairs")` (never echo the value). |
| 33 | `src/elmanhg_ai/core/telemetry.py` | `@dataclass(frozen=True, slots=True) class Telemetry: tracer_provider: TracerProvider; meter_provider: MeterProvider; exporting: bool; def shutdown(self) -> None` (shuts down both).<br>`def parse_otlp_headers(value: SecretStr \| None) -> dict[str, str]`: `{}` for None; otherwise split on `,` and `=` (first `=` only), strip both sides.<br>`def build_telemetry(settings: Settings) -> Telemetry`: `Resource.create({SERVICE_NAME: settings.otel_service_name, SERVICE_VERSION: settings.service_version, "deployment.environment.name": settings.env})`; `TracerProvider(resource=..., sampler=ParentBased(TraceIdRatioBased(settings.trace_sample_ratio)))`. When `settings.otlp_endpoint`: `add_span_processor(BatchSpanProcessor(OTLPSpanExporter(endpoint=..., headers=headers)))` and `readers=[PeriodicExportingMetricReader(OTLPMetricExporter(endpoint=..., headers=headers), export_interval_millis=settings.metric_export_interval_seconds * 1000)]`, else `readers=[]`. Then `MeterProvider(resource=..., metric_readers=readers)`, and return `Telemetry(..., exporting=settings.otlp_endpoint is not None)`. **Never** call `trace.set_tracer_provider` / `metrics.set_meter_provider` (globals break per-test apps).<br>`def instrument_app(app: FastAPI, telemetry: Telemetry) -> None`: `FastAPIInstrumentor.instrument_app(app, tracer_provider=..., meter_provider=..., excluded_urls=HEALTH_URLS)` with `HEALTH_URLS: Final = "health"`. |
| 34 | `src/elmanhg_ai/clients/metered.py` | `class AiMetrics`: `__init__(self, meter: Meter)` creates `create_histogram("gen_ai.client.operation.duration", unit="s")`, `create_histogram("gen_ai.client.token.usage", unit="{token}")` and `create_counter("elmanhg.ai.cost", unit="{USD}")`. `def record(self, *, operation: Literal["chat", "embeddings"], provider: str, model: str, duration_seconds: float, input_tokens: int \| None, output_tokens: int \| None, cost_usd: Decimal \| None, error_type: str \| None) -> None`: the base attributes are `gen_ai.operation.name`, `gen_ai.provider.name`, `gen_ai.request.model`, plus `error.type` when set. It records duration always; token usage per non-None count with `gen_ai.token.type` = `input` or `output`; and cost `float(cost_usd)` when not None.<br>`def error_type_of(error: Exception) -> str`: `error.code.value` for `DomainError`, else `type(error).__name__`.<br>`class MeteredModelClient` (satisfies `ModelClient`): `__init__(self, inner: ModelClient, *, metrics: AiMetrics, tracer: Tracer, settings: Settings)`. `async def complete(self, request)`: inside `tracer.start_as_current_span(f"chat {settings.chat_model}", kind=SpanKind.CLIENT)` set the span attributes op, provider and model, and time with `time.perf_counter()`. On exception, record with `error_type=error_type_of(error)` and `None` tokens, then `raise`. On success, record the tokens and `estimate_cost_usd(reply.input_tokens, reply.output_tokens, settings)`, set `gen_ai.usage.input_tokens` / `gen_ai.usage.output_tokens` on the span, and return the reply. `aclose` delegates.<br>`class MeteredEmbeddingClient`: the same for `embed` with operation `embeddings`, `settings.embedding_provider`, `settings.embedding_model`, `output_tokens=None`, and cost `estimate_embedding_cost_usd(reply.input_tokens, settings)`. |
| 35 | `tests/unit/test_telemetry.py` | see Test plan |
| 36 | `tests/unit/test_metered_clients.py` | see Test plan. It uses `InMemoryMetricReader` and `InMemorySpanExporter` + `SimpleSpanProcessor` from `opentelemetry.sdk`, with no new packages. |
| 37 | `tests/integration/test_telemetry.py` | see Test plan. The local fixtures `in_memory_telemetry` (returns `(Telemetry, InMemorySpanExporter, InMemoryMetricReader)`), `telemetry_app` (`create_app(settings, model_client=fake_model, embedding_client=fake_embedding, telemetry=...)`) and `telemetry_client` stay in this file, so conftest is not changed. Read the metrics **inside** the `async with` block, because lifespan shuts the providers down. |

### Web (`web/src/`)
| # | Path | Contract |
|---|---|---|
| 38 | `shared/lib/clientErrorReporter.ts` | `export const clientErrorLimits = { message: 500, errorName: 100, stack: 4000, path: 300 } as const;` (comment: mirrors the API `ClientErrors` defaults), and `export const maxReportsPerPage = 10;`.<br>`export interface ClientErrorReporter { report(error: unknown, source: ClientErrorSource): void; install(target: Window): () => void; reset(): void; }`.<br>`export function createClientErrorReporter(): ClientErrorReporter`: closure state `sent = 0` and `seen = new Set<string>()`.<ul><li>`report`: return if `error instanceof ApiError`, or `error instanceof Error && error.name === 'AbortError'`, or `sent >= maxReportsPerPage`. `message = (error instanceof Error ? error.message : String(error)) \|\| (error instanceof Error ? error.name : '') \|\| 'Unknown error'`, truncated. Dedupe key = `${source}\|${message}`. Then `sent++` and `void reportClientError({ message, errorName, stack, source, path: window.location.pathname.slice(0, clientErrorLimits.path) }).catch(() => undefined)`, where `errorName` and `stack` are only set for `Error` and truncated.</li><li>`install`: adds `error` (uses `event.error ?? event.message`, source `'Window'`) and `unhandledrejection` (`event.reason`, `'UnhandledRejection'`) listeners, and returns a remover.</li><li>`reset` clears the state.</li></ul>`export const clientErrorReporter = createClientErrorReporter();`. The generated function and type come from the Orval `client-errors` output and `@/shared/api/generated/model`. |
| 39 | `shared/lib/clientErrorReporter.test.ts` | see Test plan |

### Deploy and observability configs (`deploy/observability/`)
| # | Path | Contract |
|---|---|---|
| 40 | `otel-collector/config.yaml` | Exact shape below. |
| 41 | `prometheus/prometheus.yml` | Exact shape below. |
| 42 | `prometheus/rules/elmanhg.rules.yml` | Recording and alert rules below. |
| 43 | `prometheus/tests/elmanhg.rules.test.yml` | `promtool test rules`: `rule_files: [../rules/elmanhg.rules.yml]`, `evaluation_interval: 30s`, one `tests:` entry per row of the promtool section of the Test plan. |
| 44 | `alertmanager/alertmanager.example.yml` | Header comment: `# Copy to deploy/alertmanager.yml on the host (chmod 600); never commit it (docs/observability.md, Alerts).`. `route: { receiver: default, group_by: [alertname, severity], group_wait: 30s, group_interval: 5m, repeat_interval: 4h }`. `receivers: [ { name: default } ]` (null receiver). Commented examples of an `email_configs` receiver (`smarthost: smtp.resend.com:587`, `auth_username: resend`, `auth_password: <Resend API key>`, `require_tls: true`) and a `webhook_configs` receiver. |
| 45 | `loki/config.yaml` | `auth_enabled: false`; `server.http_listen_port: 3100`, `log_level: warn`; `common` (path_prefix `/loki`, replication_factor 1, ring kvstore inmemory, filesystem chunks `/loki/chunks`, rules `/loki/rules`); `schema_config` from `2026-01-01`, `store: tsdb`, `object_store: filesystem`, `schema: v13`, index prefix `index_`, period 24h; `limits_config: { retention_period: 336h, allow_structured_metadata: true, volume_enabled: true }`; `compactor: { working_directory: /loki/compactor, retention_enabled: true, delete_request_store: filesystem }`; `analytics.reporting_enabled: false`. |
| 46 | `tempo/config.yaml` | `stream_over_http_enabled: true`; `server: { http_listen_port: 3200, log_level: warn }`; `distributor.receivers.otlp.protocols.grpc.endpoint: 0.0.0.0:4317`; `storage.trace: { backend: local, wal.path: /var/tempo/wal, local.path: /var/tempo/blocks }`; `compactor.compaction.block_retention: 168h`; `usage_report.reporting_enabled: false`. |
| 47 | `grafana/provisioning/datasources/datasources.yml` | `apiVersion: 1`. Datasources:<ul><li>`Prometheus` (uid `prometheus`, `http://prometheus:9090`, isDefault)</li><li>`Loki` (uid `loki`, `http://loki:3100`, `jsonData.derivedFields: [{ name: TraceID, matcherType: label, matcherRegex: trace_id, datasourceUid: tempo, url: '${__value.raw}' }]`)</li><li>`Tempo` (uid `tempo`, `http://tempo:3200`, `jsonData.tracesToLogsV2: { datasourceUid: loki, filterByTraceID: true, spanStartTimeShift: '-5m', spanEndTimeShift: '5m' }`)</li><li>`Alertmanager` (uid `alertmanager`, type `alertmanager`, `http://alertmanager:9093`, `jsonData.implementation: prometheus`)</li></ul>All `editable: false`. |
| 48 | `grafana/provisioning/dashboards/dashboards.yml` | `apiVersion: 1`; one provider `elmanhg`, `folder: Elmanhg`, type file, `disableDeletion: true`, `allowUiUpdates: false`, `options.path: /etc/grafana/dashboards`. |
| 49 | `grafana/dashboards/service-health.json` | uid `elmanhg-service-health`, title `Elmanhg · Service health`. Panels are listed below. |
| 50 | `grafana/dashboards/background-jobs.json` | uid `elmanhg-background-jobs`, title `Elmanhg · Background jobs`. |
| 51 | `grafana/dashboards/business.json` | uid `elmanhg-business`, title `Elmanhg · Business activity`. |
| 52 | `docs/observability.md` | Runbook sections, in order:<ol><li>Overview and signal flow</li><li>Turning it on (profile, `.env`, host sizing, Grafana through `ssh -L 3000:127.0.0.1:3000`)</li><li>Traces (sources, sampling, propagation browser→Caddy→api→ai)</li><li>Metrics: a table of every instrument in this plan with name, type, unit, tags, emitter and Prometheus name</li><li>Logs (per-service JSON shape, collection path, retention, the LogQL `\| json` and trace-id link)</li><li>SLO 99.5 % (both SLIs, error budget, burn-rate math)</li><li>Dashboards (3 uids and what each panel answers)</li><li>Alerts: a table of name, expression, severity and first response</li><li>Alert delivery (the `alertmanager.yml` copy step, Resend SMTP and webhook examples, null default)</li><li>External uptime monitor (`/api/health`, recommended 1-min check; deferred)</li><li>Error tracking (the 4 paths, SaaS through `OTLP_ENDPOINT` + headers, Sentry deferred)</li><li>PII rules (never log phone, email, OTP, tokens or payment payloads; no PII in query strings; the three redaction layers; tags carry no ids)</li><li>Configuration reference (compose `.env`, `Observability__*`, `ClientErrors__*`, `ELMANHG_AI_*`)</li><li>Retention</li><li>Running it locally (`bash deploy/smoke-test.sh`, `SMOKE_OBSERVABILITY`)</li></ol> |

(No other files are created. Generated Orval files under `web/src/shared/api/generated/client-errors/` and `generated/model/` come from the generator.)

#### `otel-collector/config.yaml` (intended shape; `otelcol-contrib validate` and the smoke test are the authority. Fix only syntax the validator rejects, e.g. if 0.161.0 renamed the `otlp` exporter type, and keep the behaviour)
```yaml
extensions:
  health_check: { endpoint: 0.0.0.0:13133 }
  file_storage: { directory: /var/lib/otelcol/file_storage }
receivers:
  otlp:
    protocols:
      grpc: { endpoint: 0.0.0.0:4317 }
  filelog/containers:
    include: [/var/lib/docker/containers/*/*-json.log]
    start_at: end
    storage: file_storage
    operators:
      - { id: docker, type: json_parser, timestamp: { parse_from: attributes.time, layout_type: gotime, layout: '2006-01-02T15:04:05.999999999Z07:00' } }
      - { id: foreign, type: filter, expr: 'attributes.attrs == nil || attributes.attrs["com.docker.compose.service"] == nil' }
      - { id: service, type: add, field: 'resource["service.name"]', value: 'EXPR("elmanhg-" + attributes.attrs["com.docker.compose.service"])' }
      - { id: body, type: move, from: attributes.log, to: body }
      - { id: cleanup, type: retain, fields: [attributes.stream] }
      - { id: app_json, type: json_parser, if: 'body matches "^\\s*\\{"', parse_from: body, parse_to: attributes, on_error: send_quiet }
      - { id: dotnet_trace, type: trace_parser, if: 'attributes["@tr"] != nil', trace_id: { parse_from: 'attributes["@tr"]' }, span_id: { parse_from: 'attributes["@sp"]' } }
      - { id: python_trace, type: trace_parser, if: 'attributes.trace_id != nil', trace_id: { parse_from: attributes.trace_id } }
      - { id: dotnet_level, type: severity_parser, if: 'attributes["@l"] != nil', parse_from: 'attributes["@l"]', mapping: &levels { debug: [Debug, Verbose, debug], info: [Information, info], warn: [Warning, warning, warn], error: [Error, error], fatal: [Fatal, critical, fatal] } }
      - { id: text_level, type: severity_parser, if: 'attributes.level != nil', parse_from: attributes.level, mapping: *levels }
processors:
  memory_limiter: { check_interval: 1s, limit_percentage: 80, spike_limit_percentage: 25 }
  batch: {}
  transform/redact:
    error_mode: ignore
    log_statements:
      - context: log
        statements:
          - replace_pattern(body, "[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}", "[redacted-email]") where IsString(body)
          - replace_pattern(body, "(\\+?20)?0?1[0125][0-9]{8}\\b", "[redacted-phone]") where IsString(body)
          - replace_all_patterns(attributes, "value", "[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}", "[redacted-email]")
          - replace_all_patterns(attributes, "value", "(\\+?20)?0?1[0125][0-9]{8}\\b", "[redacted-phone]")
exporters:
  otlp/tempo: { endpoint: tempo:4317, tls: { insecure: true } }
  otlphttp/prometheus: { endpoint: http://prometheus:9090/api/v1/otlp, tls: { insecure: true } }
  otlphttp/loki: { endpoint: http://loki:3100/otlp, tls: { insecure: true } }
service:
  extensions: [health_check, file_storage]
  telemetry: { logs: { level: warn } }
  pipelines:
    traces: { receivers: [otlp], processors: [memory_limiter, batch], exporters: [otlp/tempo] }
    metrics: { receivers: [otlp], processors: [memory_limiter, batch], exporters: [otlphttp/prometheus] }
    logs: { receivers: [filelog/containers], processors: [memory_limiter, transform/redact, batch], exporters: [otlphttp/loki] }
```

#### `prometheus/prometheus.yml`
```yaml
global: { scrape_interval: 30s, evaluation_interval: 30s }
otlp: { promote_resource_attributes: [deployment.environment.name] }
storage: { tsdb: { out_of_order_time_window: 30m } }
rule_files: [/etc/prometheus/rules/*.yml]
alerting: { alertmanagers: [ { static_configs: [ { targets: [alertmanager:9093] } ] } ] }
scrape_configs:
  - job_name: blackbox
    metrics_path: /probe
    params: { module: [http_2xx] }
    static_configs:
      - { targets: [http://web:2080/api/health], labels: { target_name: edge } }
      - { targets: [http://api:8080/health], labels: { target_name: api } }
      - { targets: [http://ai:8000/health/ready], labels: { target_name: ai } }
    relabel_configs:
      - { source_labels: [__address__], target_label: __param_target }
      - { source_labels: [__param_target], target_label: instance }
      - { target_label: __address__, replacement: blackbox:9115 }
```

#### `prometheus/rules/elmanhg.rules.yml`
Define `API = http_server_request_duration_seconds_count{job="elmanhg-api",http_route!="/health"}` (write it out in full in each expression).

**Group `elmanhg-slo-recording`.** For each window W in 5m, 30m, 1h, 6h, record `elmanhg:api_error_ratio:rateW` = `sum(rate(API{http_response_status_code=~"5.."}[W])) / sum(rate(API[W]))`.

**Alert groups:**

| Group | Alert | Expr | for | severity |
|---|---|---|---|---|
| elmanhg-slo | `ApiErrorBudgetFastBurn` | `elmanhg:api_error_ratio:rate1h > (14.4 * 0.005) and elmanhg:api_error_ratio:rate5m > (14.4 * 0.005)` | 2m | critical |
| elmanhg-slo | `ApiErrorBudgetSlowBurn` | `elmanhg:api_error_ratio:rate6h > (6 * 0.005) and elmanhg:api_error_ratio:rate30m > (6 * 0.005)` | 15m | warning |
| elmanhg-uptime | `ServiceDown` | `probe_success{job="blackbox"} == 0` | 2m | critical |
| elmanhg-uptime | `ApiTelemetryMissing` | `absent_over_time(target_info{job="elmanhg-api"}[10m])` | 5m | warning |
| elmanhg-jobs | `BackgroundJobStale` | `time() - elmanhg_job_last_success_seconds > 3 * elmanhg_job_interval_seconds` | 5m | warning |
| elmanhg-jobs | `BackgroundJobFailing` | `sum by (elmanhg_job) (increase(elmanhg_job_runs_total{elmanhg_outcome="Failed"}[15m])) >= 3` | 0m | warning |
| elmanhg-jobs | `BackgroundJobItemsFailing` | `sum by (elmanhg_job) (increase(elmanhg_job_items_total{elmanhg_outcome="Failed"}[30m])) >= 10` | 0m | warning |
| elmanhg-errors | `ApiUnhandledErrors` | `sum(increase(elmanhg_requests_total{elmanhg_outcome="UNHANDLED_EXCEPTION"}[10m])) > 0` | 0m | warning |
| elmanhg-errors | `ProviderUnavailable` | `sum by (elmanhg_outcome) (increase(elmanhg_requests_total{elmanhg_outcome=~"AI_SERVICE_UNAVAILABLE\|OTP_CHANNEL_UNAVAILABLE\|PAYMENT_GATEWAY_UNAVAILABLE"}[15m])) >= 5` | 0m | warning |
| elmanhg-errors | `OtpDeliveryFailing` | `sum(increase(elmanhg_otp_sends_total{elmanhg_outcome="Failed"}[15m])) >= 5` | 0m | critical |
| elmanhg-errors | `AiModelErrors` | `sum(increase(gen_ai_client_operation_duration_seconds_count{error_type!=""}[15m])) >= 5` | 0m | warning |
| elmanhg-errors | `ClientErrorSpike` | `sum(increase(elmanhg_client_errors_total[15m])) >= 50` | 0m | warning |
| elmanhg-errors | `PaymentNotificationNeedsReview` | `sum(increase(elmanhg_payment_notifications_total{elmanhg_outcome="FlaggedForReview"}[1h])) > 0` | 0m | warning |

Every alert has `annotations.summary` (with `{{ $labels.* }}` where useful) and `annotations.runbook: docs/observability.md#alerts`.

#### Compose spec (`deploy/docker-compose.prod.yml`)
- **`x-logging`:** add `labels: "com.docker.compose.service"` under `options`.
- **New anchor `x-observability-logging`:** json-file with `max-size: "10m"` and `max-file: "3"`, no labels. The collector drops these streams, which prevents a feedback loop.
- **`x-api-environment`:** add `Observability__OtlpEndpoint: ${OTLP_ENDPOINT:-}`, `Observability__ServiceVersion: ${IMAGE_TAG:?set IMAGE_TAG in .env}` and `CoreLogging__Console__Format: Json`.
- **`ai.environment`:** add `ELMANHG_AI_OTLP_ENDPOINT: ${OTLP_ENDPOINT:-}`, `ELMANHG_AI_SERVICE_VERSION: ${IMAGE_TAG:?set IMAGE_TAG in .env}` and `OTEL_SEMCONV_STABILITY_OPT_IN: http`.
- **New services.** All have `profiles: ["observability"]`, `restart: unless-stopped` and `logging: *observability-logging`. None publishes a port except grafana. Images are pinned `tag@sha256:<digest from imagetools>`.

| Service | Image | Details |
|---|---|---|
| `otel-collector` | contrib 0.161.0 | `user: "0:0"` (comment: reads root-owned container logs; no published port). Mounts `./observability/otel-collector/config.yaml:/etc/otelcol-contrib/config.yaml:ro`, `/var/lib/docker/containers:/var/lib/docker/containers:ro` and `otel-collector-data:/var/lib/otelcol/file_storage`. `depends_on: [prometheus, loki, tempo]`. `mem_limit: 256m`. |
| `prometheus` | v3.14.0 | `command: ["--config.file=/etc/prometheus/prometheus.yml", "--storage.tsdb.path=/prometheus", "--storage.tsdb.retention.time=35d", "--web.enable-otlp-receiver"]`. Mounts `./observability/prometheus:/etc/prometheus:ro` and `prometheus-data:/prometheus`. Healthcheck `["CMD", "wget", "-qO-", "http://127.0.0.1:9090/-/ready"]`. `mem_limit: 512m`. |
| `alertmanager` | v0.34.1 | `command: ["--config.file=/etc/alertmanager/alertmanager.yml", "--storage.path=/alertmanager"]`. Mounts `./${ALERTMANAGER_CONFIG_FILE:-alertmanager.yml}:/etc/alertmanager/alertmanager.yml:ro` and `alertmanager-data:/alertmanager`. Healthcheck wget `/-/ready` on 9093. `mem_limit: 64m`. |
| `blackbox` | v0.28.0 | Default image config. Healthcheck wget `http://127.0.0.1:9115/-/healthy`. `mem_limit: 64m`. |
| `loki` | 3.7.8 | `command: ["-config.file=/etc/loki/config.yaml"]`. Mounts `./observability/loki/config.yaml:/etc/loki/config.yaml:ro` and `loki-data:/loki`. Healthcheck wget `http://127.0.0.1:3100/ready`, **only if** `docker run --rm --entrypoint wget <image> --help` succeeds; otherwise no healthcheck, and remove it from the deploy.sh loop. `mem_limit: 512m`. |
| `tempo` | 2.10.8 | `user: "0:0"` (comment: the named volume is root-owned; internal only). `command: ["-config.file=/etc/tempo/config.yaml"]`. Mounts `./observability/tempo/config.yaml:/etc/tempo/config.yaml:ro` and `tempo-data:/var/tempo`. No healthcheck. `mem_limit: 512m`. |
| `grafana` | 13.1.6 | Environment: `GF_SECURITY_ADMIN_USER: admin`, `GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_ADMIN_PASSWORD:-}`, `GF_USERS_ALLOW_SIGN_UP: "false"`, `GF_AUTH_ANONYMOUS_ENABLED: "false"`, `GF_ANALYTICS_REPORTING_ENABLED: "false"`, `GF_ANALYTICS_CHECK_FOR_UPDATES: "false"`, `GF_NEWS_NEWS_FEED_ENABLED: "false"`. `ports: ["127.0.0.1:${GRAFANA_PORT:-3000}:3000"]`. Mounts `./observability/grafana/provisioning:/etc/grafana/provisioning:ro`, `./observability/grafana/dashboards:/etc/grafana/dashboards:ro` and `grafana-data:/var/lib/grafana`. Healthcheck wget `http://127.0.0.1:3000/api/health` with the same "only if wget exists" rule. `depends_on: [prometheus, loki, tempo, alertmanager]`. `mem_limit: 256m`. |

- **New volumes:** `otel-collector-data`, `prometheus-data`, `alertmanager-data`, `loki-data`, `tempo-data`, `grafana-data`.

#### `deploy/Caddyfile`
- A new snippet at the top: `(api_health) { handle /api/health { rewrite * /health  reverse_proxy api:8080 } }`, formatted over several lines.
- Inside `{$SITE_ADDRESS}`, after `encode`:

  ```
  log {
      output stdout
      format filter {
          wrap json
          fields {
              request>headers delete
              resp_headers delete
              request>uri regexp `\?.*$` "?redacted"
          }
      }
  }
  import api_health
  ```

- Inside `:2080`, add `import api_health` before `respond`.
- Keep the existing comments. Add a comment above the snippet: `# Public, body-only health for external uptime monitors (docs/observability.md).`

#### `deploy/.env.example` block (append)
```
# Observability (docs/observability.md). Add ",observability" to COMPOSE_PROFILES to run the collector, Prometheus, Loki, Tempo, Grafana and Alertmanager here.
# OTLP/gRPC endpoint for api and ai traces and metrics; empty exports nothing. With the profile: http://otel-collector:4317
OTLP_ENDPOINT=
# Required with the profile: openssl rand -hex 16. Grafana listens on 127.0.0.1 only: ssh -L 3000:127.0.0.1:3000 <host>
GRAFANA_ADMIN_PASSWORD=
GRAFANA_PORT=3000
# Required with the profile: copy observability/alertmanager/alertmanager.example.yml to this path (chmod 600).
ALERTMANAGER_CONFIG_FILE=alertmanager.yml
```

#### `deploy/lib.sh` additions
- `profile_enabled() { local profiles; profiles=$(grep '^COMPOSE_PROFILES=' "$ENV_FILE" | cut -d= -f2-); [[ ",$profiles," == *",$1,"* ]]; }`
- `check_observability_config()`: return 0 unless `profile_enabled observability`. Then `fail` if `GRAFANA_ADMIN_PASSWORD` is empty in `$ENV_FILE`, or if the file named by `ALERTMANAGER_CONFIG_FILE` (default `alertmanager.yml`) does not exist. Both messages cite docs/observability.md.

#### `deploy/smoke-test.sh` additions
- **Knobs:** `observability=${SMOKE_OBSERVABILITY:-1}`. When it is 1:
  - `set_env` `COMPOSE_PROFILES ai,observability`, `OTLP_ENDPOINT http://otel-collector:4317`, `GRAFANA_ADMIN_PASSWORD smokegrafanapasswordnotasecret`, `GRAFANA_PORT ${SMOKE_GRAFANA_PORT:-3300}`, `ALERTMANAGER_CONFIG_FILE .smoke/alertmanager.yml`.
  - `cp observability/alertmanager/alertmanager.example.yml .smoke/alertmanager.yml`
  - `check_observability_config`
- **Before `compose up -d`:**
  - Always: `compose run --rm --no-deps --entrypoint caddy web validate --config /etc/caddy/Caddyfile --adapter caddyfile`.
  - When observability is on:
    - `compose run --rm --no-deps --entrypoint promtool prometheus check config /etc/prometheus/prometheus.yml`
    - `... promtool prometheus test rules /etc/prometheus/tests/elmanhg.rules.test.yml`
    - `compose run --rm --no-deps --entrypoint amtool alertmanager check-config /etc/alertmanager/alertmanager.yml`
    - `compose run --rm --no-deps otel-collector validate --config=/etc/otelcol-contrib/config.yaml`
- **Helpers:**
  - `eventually <description> <command...>` polls every 5 s for up to 180 s, then calls `fail`.
  - `in_prometheus <url>` runs `compose exec -T prometheus wget -qO- "$1"`.
  - `has_series <urlencoded-promql>` succeeds when the `/api/v1/query` body contains `"result":[{`.
- **After the existing checks (always):**
  - `health=$(curl -fsS "$base/api/health")` must equal `Healthy`.
  - `trace_id` comes from `curl -fsS -D - "$base/api/questions/servable-count"`: the `x-trace-id` header, case-insensitive, with `\r` stripped.
  - POST a client error: `{"message":"smoke client error","source":"Window","path":"/smoke"}`, which must return 200.
  - `curl -fsS "$base/smoke-redaction/mona@example.com"` (for the Caddy access log).
  - Send one valid `POST http://ai:8000/v1/embeddings` through `compose exec -T api curl`, with a Bearer token read from `.smoke/ai.env` and a body per docs/ai-service.md.
- **Observability assertions (only when on).** Each is wrapped in `eventually`:
  1. `has_series elmanhg_requests_total`
  2. `http_server_request_duration_seconds_count{job="elmanhg-api"}`
  3. `gen_ai_client_operation_duration_seconds_count{job="elmanhg-ai"}`
  4. `elmanhg_client_errors_total`
  5. `elmanhg_job_interval_seconds`
  6. `probe_success{target_name="edge"}==1`
  7. `in_prometheus http://localhost:9090/api/v1/rules` contains `ApiErrorBudgetFastBurn`
  8. `in_prometheus "http://tempo:3200/api/traces/$trace_id"` contains `elmanhg-api`
  9. The Loki `query_range` for `{service_name="elmanhg-api"}` contains `$trace_id`
  10. The Loki `query_range` for `{service_name="elmanhg-web"} |= "smoke-redaction"` contains `[redacted-email]` and not `mona@example.com`
  11. `curl -fsS -u "admin:$password" "http://localhost:$grafana_port/api/dashboards/uid/<uid>"` succeeds for all 3 uids
  12. `in_prometheus http://alertmanager:9093/-/ready` succeeds
- **Encoding:** URL-encode the PromQL and LogQL by hand into constants. There is no jq.

#### Dashboards (panel title, then the query; `P` = Prometheus, `L` = Loki)

**`service-health.json`:**
1. Stat "API availability (30 d)": P `1 - ((sum(increase(API{http_response_status_code=~"5.."}[30d])) or vector(0)) / sum(increase(API[30d])))`, percentunit, threshold red < 0.995.
2. Stat "Error budget left (30 d)": P `1 - (((sum(increase(API{…5..}[30d])) or vector(0)) / sum(increase(API[30d]))) / 0.005)`.
3. Stat "Probe uptime (30 d)": P `avg by (target_name) (avg_over_time(probe_success{job="blackbox"}[30d]))`.
4. State timeline "Probe status": P `probe_success{job="blackbox"}` by target_name.
5. Timeseries "API requests/s by status": P `sum by (http_response_status_code) (rate(API[5m]))`.
6. Timeseries "5xx ratio": P `elmanhg:api_error_ratio:rate5m`, `elmanhg:api_error_ratio:rate1h`, with a threshold line at 0.072.
7. Timeseries "API p95 by route (top 10)": P `topk(10, histogram_quantile(0.95, sum by (le, http_route) (rate(http_server_request_duration_seconds_bucket{job="elmanhg-api",http_route!="/health"}[5m]))))`.
8. Table "Top failure outcomes": P instant `topk(15, sum by (elmanhg_request, elmanhg_outcome) (increase(elmanhg_requests_total{elmanhg_outcome!="Success"}[$__range])))`.
9. Timeseries "API → AI p95": P `histogram_quantile(0.95, sum by (le) (rate(http_client_request_duration_seconds_bucket{job="elmanhg-api",server_address="ai"}[5m])))`.
10. Timeseries "AI requests/s and p95": `job="elmanhg-ai"` series.
11. Timeseries "API memory": the working-set series from the `System.Runtime` meter. Verify the exact name in Prometheus (`/api/v1/label/__name__/values`) during the smoke run and use the observed name.
12. Logs "Errors across services": L `{service_name=~"elmanhg-.+"} |~ "(?i)\"(@l|level)\":\"(error|fatal|critical)\""`.

**`background-jobs.json`:**
- Template variable `job` = `label_values(elmanhg_job_interval_seconds, elmanhg_job)`, multi-select with All.
- Panels:
  1. Stat "Since last success": P `time() - elmanhg_job_last_success_seconds{elmanhg_job=~"$job"}`, unit s.
  2. Stat "Stale after": P `3 * elmanhg_job_interval_seconds{elmanhg_job=~"$job"}`.
  3. Timeseries "Runs by outcome": P `sum by (elmanhg_job, elmanhg_outcome) (increase(elmanhg_job_runs_total{elmanhg_job=~"$job"}[$__rate_interval]))`.
  4. Timeseries "Items succeeded and failed": the same on `elmanhg_job_items_total`.
  5. Timeseries "Sweep p95": P `histogram_quantile(0.95, sum by (le, elmanhg_job) (rate(elmanhg_job_duration_seconds_bucket{elmanhg_job=~"$job"}[15m])))`.
  6. Logs "Worker logs": L `{service_name="elmanhg-api"} |~ "Elmanhg\\.Api\\.Workers"`.

**`business.json`:**
1. "Quiz answers/min by outcome": `sum by (elmanhg_outcome) (rate(elmanhg_quiz_answers_total[5m])) * 60`.
2. Stat "Quiz answers": `sum(increase(elmanhg_quiz_answers_total[$__range]))`.
3. "Payment notifications by outcome": `sum by (elmanhg_outcome) (increase(elmanhg_payment_notifications_total[$__rate_interval]))`.
4. "Checkouts and refunds": `sum by (elmanhg_request, elmanhg_outcome) (increase(elmanhg_requests_total{elmanhg_request=~"StartCheckoutCommand|RefundPaymentCommand"}[$__rate_interval]))`.
5. "OTP sends by channel": `sum by (elmanhg_channel, elmanhg_outcome) (increase(elmanhg_otp_sends_total[$__rate_interval]))`.
6. "AI calls": `sum by (gen_ai_operation_name, error_type) (increase(gen_ai_client_operation_duration_seconds_count[$__rate_interval]))`.
7. "AI tokens": `sum by (gen_ai_operation_name, gen_ai_token_type) (increase(gen_ai_client_token_usage_sum[$__rate_interval]))`.
8. Stat "AI cost (USD)": `sum(increase(elmanhg_ai_cost_total[$__range]))`, unit currencyUSD.
9. "AI p95 by operation": `histogram_quantile(0.95, sum by (le, gen_ai_operation_name) (rate(gen_ai_client_operation_duration_seconds_bucket[5m])))`.
10. "Browser errors by source": `sum by (elmanhg_source) (increase(elmanhg_client_errors_total[$__rate_interval]))`.

Every dashboard: `schemaVersion` as Grafana 13 writes it, `editable: false`, datasource refs by uid (`prometheus` / `loki`), and time range `now-24h`.

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|---|---|---|---|---|
| `ClientErrorMessageRequired` | `CLIENT_ERROR_MESSAGE_REQUIRED` | ReportClientErrorValidator | ValidationBehaviourException | 422 |
| `ClientErrorMessageTooLong` | `CLIENT_ERROR_MESSAGE_TOO_LONG` | ReportClientErrorValidator | ValidationBehaviourException | 422 |
| `ClientErrorNameTooLong` | `CLIENT_ERROR_NAME_TOO_LONG` | ReportClientErrorValidator | ValidationBehaviourException | 422 |
| `ClientErrorStackTooLong` | `CLIENT_ERROR_STACK_TOO_LONG` | ReportClientErrorValidator | ValidationBehaviourException | 422 |
| `ClientErrorPathTooLong` | `CLIENT_ERROR_PATH_TOO_LONG` | ReportClientErrorValidator | ValidationBehaviourException | 422 |
| `ClientErrorPathInvalid` | `CLIENT_ERROR_PATH_INVALID` | ReportClientErrorValidator | ValidationBehaviourException | 422 |
| `ClientErrorSourceInvalid` | `CLIENT_ERROR_SOURCE_INVALID` | ReportClientErrorValidator | ValidationBehaviourException | 422 |

| Key | ar | en |
|---|---|---|
| CLIENT_ERROR_MESSAGE_REQUIRED | رسالة الخطأ مطلوبة. | The error message is required. |
| CLIENT_ERROR_MESSAGE_TOO_LONG | رسالة الخطأ طويلة جدا. | The error message is too long. |
| CLIENT_ERROR_NAME_TOO_LONG | اسم الخطأ طويل جدا. | The error name is too long. |
| CLIENT_ERROR_STACK_TOO_LONG | تفاصيل الخطأ طويلة جدا. | The error stack is too long. |
| CLIENT_ERROR_PATH_TOO_LONG | مسار الصفحة طويل جدا. | The page path is too long. |
| CLIENT_ERROR_PATH_INVALID | مسار الصفحة غير صالح. | The page path is not valid. |
| CLIENT_ERROR_SOURCE_INVALID | مصدر الخطأ غير صالح. | The error source is not valid. |

The rate limit reuses the existing `TOO_MANY_REQUESTS`.

## Domain behaviour
No domain entity changes, no migration, and no `AppDbContextTests` entry. The only state is in `BackgroundJobRun`, specified in Files #3–4:
- the outcome is derived;
- `Dispose` completes the run exactly once;
- last success advances only for outcomes other than Failed.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| POST | `/api/client-errors` | `[AllowAnonymous]` + rate limit `client-errors` (per IP, `ClientErrors:PermitLimit`/`WindowSeconds`) | `ReportClientErrorRequest { message: string, errorName?: string, stack?: string, source: "Window" \| "UnhandledRejection" \| "Route", path?: string }` | 200, empty body. 422 problem with the codes above. 429 `TOO_MANY_REQUESTS`. |
| GET (Caddy only) | `/api/health` (public), rewritten to API `/health` | none | none | 200 `Healthy` or 503 `Unhealthy` |

## Test plan

### .NET (xUnit v3, FluentAssertions 7, NSubstitute, `MetricCollector<T>`)
Build each `MetricCollector` as `new MetricCollector<long>(meterFactory, "Elmanhg", "<instrument>")`, where `meterFactory` comes from `MeterFactories.Create()`, or from `factory.Services.GetRequiredService<IMeterFactory>()` in integration tests.

| # | Test class | Test method | Asserts |
|---|---|---|---|
| 1 | ElmanhgMetricsTests | RecordRequest_Success_RecordsCountAndDurationWithTags | `elmanhg.requests` has one measurement of 1 with request=`X` and outcome=`Success`; `elmanhg.request.duration` has one measurement equal to the given seconds |
| 2 | ElmanhgMetricsTests | RecordQuizAnswer_Correct_IncrementsWithOutcomeTag | 1 measurement, outcome=`Correct` |
| 3 | ElmanhgMetricsTests | RecordPaymentNotification_FlaggedForReview_TagsEnumName | outcome=`FlaggedForReview` |
| 4 | ElmanhgMetricsTests | RecordOtpSend_Delivery_TagsChannelAndOutcome (Theory: true→`Delivered`, false→`Failed`) | channel=`WhatsApp`, expected outcome |
| 5 | ElmanhgMetricsTests | RecordClientError_Route_TagsSource | source=`Route` |
| 6 | BackgroundJobMetricsTests | Run_AllItemsSucceed_RecordsSucceededRunItemsAndDuration | runs outcome `Succeeded` = 1; items `Succeeded` = 2; no `Failed` items; 1 duration measurement ≥ 0 |
| 7 | BackgroundJobMetricsTests | Run_SomeItemsFail_RecordsPartiallyFailedRun | runs outcome `PartiallyFailed`; items Succeeded 1 and Failed 1 |
| 8 | BackgroundJobMetricsTests | Run_ListingFailed_RecordsFailedRunAndKeepsLastSuccess | runs outcome `Failed`; after `RecordObservableInstruments()`, last_success still equals the registration time |
| 9 | BackgroundJobMetricsTests | Register_ReportsIntervalAndInitialLastSuccess | interval gauge = 60 and last_success = registration unix seconds (time from `Substitute.For<TimeProvider>().GetUtcNow()`), tag job |
| 10 | BackgroundJobMetricsTests | Run_Succeeded_AdvancesLastSuccessToCompletionTime | `GetUtcNow` returns t0 then t1, and last_success = t1 |
| 11 | BackgroundJobMetricsTests | Dispose_CalledTwice_RecordsOneRun | runs total = 1 |
| 12 | RequestMetricsBehaviourTests | Handle_NextSucceeds_RecordsSuccessOutcome | request=`MetricsProbeRequest`, outcome `Success` |
| 13 | RequestMetricsBehaviourTests | Handle_ValidationFails_RecordsValidationFailedAndRethrows | throws ValidationBehaviourException (codes `A,B`); outcome `VALIDATION_FAILED` |
| 14 | RequestMetricsBehaviourTests | Handle_CoreExceptionThrown_RecordsErrorCodeAndRethrows | NotFoundCoreException("PROBE_NOT_FOUND") is rethrown; outcome `PROBE_NOT_FOUND` |
| 15 | RequestMetricsBehaviourTests | Handle_UnexpectedException_RecordsUnhandledAndRethrows | InvalidOperationException is rethrown; outcome `UNHANDLED_EXCEPTION` |
| 16 | RequestMetricsBehaviourTests | Handle_Cancelled_RecordsCancelledAndRethrows | OperationCanceledException; outcome `CANCELLED` |
| 17 | QuizAnswerMetricsBehaviourTests | Handle_AnswerGraded_RecordsAttemptOutcome | the result is returned unchanged; outcome `Correct` |
| 18 | QuizAnswerMetricsBehaviourTests | Handle_ResultWithoutAttempt_RecordsUngraded | outcome `Ungraded` |
| 19 | QuizAnswerMetricsBehaviourTests | Handle_NextThrows_RecordsNothing | exception propagates; 0 measurements |
| 20 | PaymentNotificationMetricsBehaviourTests | Handle_NotificationProcessed_RecordsOutcome | outcome `Succeeded`; result returned |
| 21 | PaymentNotificationMetricsBehaviourTests | Handle_NextThrows_RecordsNothing | 0 measurements |
| 22 | LogRedactorTests | Redact_Email_ReplacesWithMarker | `"mail mona@example.com now"` becomes `"mail [redacted-email] now"` |
| 23 | LogRedactorTests | Redact_LocalMobileNumber_ReplacesWithMarker | `01012345678` becomes `[redacted-phone]` |
| 24 | LogRedactorTests | Redact_InternationalMobileNumber_ReplacesWithMarker | `+201012345678` becomes `[redacted-phone]` |
| 25 | LogRedactorTests | Redact_TextWithoutPersonalData_ReturnsUnchanged | a GUID plus `482913` stay unchanged |
| 26 | LogRedactorTests | Redact_Null_ReturnsNull | null |
| 27 | ReportClientErrorHandlerTests | Handle_Report_RecordsClientErrorMetric | `elmanhg.client.errors` has 1 measurement with source `Window` |
| 28 | ReportClientErrorHandlerTests | Handle_ReportWithEmailAndPhone_LogsRedactedWarning | one `ILogger.Log` call at `Warning` whose state `ToString()` contains `[redacted-email]` and `[redacted-phone]`, and contains neither the raw email nor the raw phone |
| 29 | ReportClientErrorHandlerTests | Handle_SignedInUser_LogsUserId | the logged state contains the user id |
| 30 | ReportClientErrorValidatorTests | Validate_ValidReport_Passes | no errors |
| 31 | ReportClientErrorValidatorTests | Validate_EmptyMessage_FailsWithMessageRequired | code `CLIENT_ERROR_MESSAGE_REQUIRED` |
| 32 | ReportClientErrorValidatorTests | Validate_MessageTooLong_FailsWithMessageTooLong | `CLIENT_ERROR_MESSAGE_TOO_LONG` (501 chars) |
| 33 | ReportClientErrorValidatorTests | Validate_ErrorNameTooLong_FailsWithNameTooLong | `CLIENT_ERROR_NAME_TOO_LONG` |
| 34 | ReportClientErrorValidatorTests | Validate_StackTooLong_FailsWithStackTooLong | `CLIENT_ERROR_STACK_TOO_LONG` |
| 35 | ReportClientErrorValidatorTests | Validate_PathTooLong_FailsWithPathTooLong | `CLIENT_ERROR_PATH_TOO_LONG` |
| 36 | ReportClientErrorValidatorTests | Validate_PathWithoutLeadingSlash_FailsWithPathInvalid | `CLIENT_ERROR_PATH_INVALID` |
| 37 | ReportClientErrorValidatorTests | Validate_UnknownSource_FailsWithSourceInvalid | `(ClientErrorSource)99` gives `CLIENT_ERROR_SOURCE_INVALID` |
| 38 | ReportClientErrorValidatorTests | Validate_NullOptionalFields_Passes | ErrorName, Stack and Path null give no errors |
| 39 | ObservabilityOptionsValidatorTests | Validate_Defaults_Succeeds | Succeeded |
| 40 | ObservabilityOptionsValidatorTests | Validate_HttpEndpointWithHeaders_Succeeds | `http://otel-collector:4317` with `authorization=Bearer x` |
| 41 | ObservabilityOptionsValidatorTests | Validate_RelativeEndpoint_Fails | the failure message mentions `Observability:OtlpEndpoint` |
| 42 | ObservabilityOptionsValidatorTests | Validate_NonHttpScheme_Fails | `ftp://collector` fails |
| 43 | ObservabilityOptionsValidatorTests | Validate_HeaderWithoutEquals_Fails | fails, and the message does not contain the header text |
| 44 | ObservabilityOptionsValidatorTests | Validate_HeaderWithEmptyKey_Fails | `=value` fails |
| 45 | OtpChannelRouterTests | SendAsync_ChannelDelivers_RecordsDeliveredSend | `elmanhg.otp.sends` channel `WhatsApp`, outcome `Delivered` |
| 46 | OtpChannelRouterTests | SendAsync_ChannelThrows_RecordsFailedSendAndRethrows | exception rethrown; outcome `Failed`; no `Delivered` measurement |
| 47 | ExpiredExamSubmissionWorkerTests | Sweep_AllSubmitted_RecordsSucceededRunWithItemCount | after `await collector.WaitForMeasurementsAsync(1, WaitLimit)`, `elmanhg.job.runs` job=`exam-auto-submit`, outcome `Succeeded`, and items Succeeded = number of ids |
| 48 | ExpiredExamSubmissionWorkerTests | Sweep_ListingFails_RecordsFailedRun | the list query throws; runs outcome `Failed` |
| 49 | ExpiredExamSubmissionWorkerTests | Execute_Enabled_RegistersJobInterval | the interval gauge = `AutoSubmitIntervalSeconds` for `exam-auto-submit` |
| 50 | SubscriptionLapseWorkerTests | Sweep_AllLapsed_RecordsSucceededRunWithItemCount | same as #47, job `subscription-lapse` |
| 51 | SubscriptionLapseWorkerTests | Sweep_ListingFails_RecordsFailedRun | Failed |
| 52 | SubscriptionLapseWorkerTests | Execute_Enabled_RegistersJobInterval | interval = `LapseSweepIntervalSeconds` |
| 53 | LessonContentIndexWorkerTests | Sweep_AllReindexed_RecordsSucceededRunWithItemCount | job `lesson-content-index` |
| 54 | LessonContentIndexWorkerTests | Sweep_ListingFails_RecordsFailedRun | Failed |
| 55 | LessonContentIndexWorkerTests | Execute_Enabled_RegistersJobInterval | interval = `IndexSweepIntervalSeconds` |
| 56 | CoreExceptionMiddlewareTests | InvokeAsync_ServerErrorInsideActivity_AddsExceptionEventAndErrorCodeTag | with an `ActivityListener` sampling a test `ActivitySource`: the tag `app.error_code`=`PROBE_UNAVAILABLE`, and one `exception` event |
| 57 | CoreExceptionMiddlewareTests | InvokeAsync_ClientErrorInsideActivity_TagsErrorCodeWithoutExceptionEvent | tag `PROBE_NOT_FOUND`; zero events |
| 58 | PipelineCompositionTests | Resolve_PipelineBehaviours_RequestMetricsRunsInsideAuditAndOutsideValidation | index(RequestMetricsBehaviour) == 1 and < index(ValidationBehaviour) |
| 59 | PipelineCompositionTests | Resolve_SubmitAnswerPipeline_IncludesQuizAnswerMetricsBehaviour | contains the type |
| 60 | PipelineCompositionTests | Resolve_PaymentNotificationPipeline_IncludesPaymentNotificationMetricsBehaviour | contains the type |
| 61 | ClientErrorsEndpointTests | Post_AnonymousReport_Returns200AndRecordsMetric | 200; `elmanhg.client.errors` source `Route` = 1 (this anonymous case is the endpoint's authz case: anonymous access by design) |
| 62 | ClientErrorsEndpointTests | Post_SignedInStudent_Returns200 | 200 (the seeded student client, through `ScopeTestData`) |
| 63 | ClientErrorsEndpointTests | Post_EmptyMessage_Returns422AndRecordsValidationFailed | 422, code contains `CLIENT_ERROR_MESSAGE_REQUIRED`; `elmanhg.requests` request=`ReportClientErrorCommand`, outcome `VALIDATION_FAILED` |
| 64 | ClientErrorsEndpointTests | Post_OverIpLimit_Returns429TooManyRequests | `WithWebHostBuilder` PermitLimit=1: second call 429 `TOO_MANY_REQUESTS` (mirror the FunnelEvents test) |
| 65 | TelemetryRegistrationTests | Services_Default_RegisterTracerAndMeterProviders | `TracerProvider` and `MeterProvider` resolve as non-null from `factory.Services` |
| 66 | TelemetryRegistrationTests | Start_NonHttpOtlpEndpoint_FailsOptionsValidation | `WithWebHostBuilder(UseSetting("Observability:OtlpEndpoint","ftp://collector"))`: `CreateClient()` throws `OptionsValidationException` |

### Python (pytest; `integration` marker on the integration file)
| # | File | Test | Asserts |
|---|---|---|---|
| 67 | tests/unit/test_settings.py | test_settings_blank_otlp_endpoint_is_none | `otlp_endpoint="  "` gives None |
| 68 | tests/unit/test_settings.py | test_settings_otlp_endpoint_without_http_scheme_raises | `ValidationError`, loc `otlp_endpoint` |
| 69 | tests/unit/test_settings.py | test_settings_malformed_otlp_headers_raises | `"novalue"` gives a ValidationError whose message does not contain `novalue` (hide_input) |
| 70 | tests/unit/test_settings.py | test_settings_trace_sample_ratio_above_one_raises | ValidationError, loc `trace_sample_ratio` |
| 71 | tests/unit/test_telemetry.py | test_build_telemetry_without_endpoint_is_not_exporting | `exporting is False` |
| 72 | tests/unit/test_telemetry.py | test_build_telemetry_with_endpoint_is_exporting | `exporting is True` (then call shutdown) |
| 73 | tests/unit/test_telemetry.py | test_build_telemetry_sets_service_resource | `tracer_provider.resource.attributes` has service.name `elmanhg-ai`, service.version and `deployment.environment.name` = `testing` |
| 74 | tests/unit/test_telemetry.py | test_parse_otlp_headers_returns_pairs | `"a=b, c = d=e"` gives `{"a":"b","c":"d=e"}` |
| 75 | tests/unit/test_telemetry.py | test_parse_otlp_headers_none_returns_empty | `{}` |
| 76 | tests/unit/test_metered_clients.py | test_metered_model_success_records_duration_tokens_and_cost | duration count 1 with op `chat`, provider `fake`, model = `settings.chat_model`; token sums input and output match the scripted reply; cost = `estimate_cost_usd` |
| 77 | tests/unit/test_metered_clients.py | test_metered_model_success_creates_client_span_with_usage | 1 span named `chat <model>`, kind CLIENT, `gen_ai.usage.input_tokens` set |
| 78 | tests/unit/test_metered_clients.py | test_metered_model_failure_records_error_type_and_reraises | `ModelUnavailableError` propagates; duration point has `error.type`=`DEPENDENCY_UNAVAILABLE`; no token points; span status ERROR |
| 79 | tests/unit/test_metered_clients.py | test_metered_embedding_success_records_input_tokens_and_cost | op `embeddings`, input tokens, cost = `estimate_embedding_cost_usd` |
| 80 | tests/unit/test_metered_clients.py | test_metered_embedding_failure_records_error_type_and_reraises | error.type recorded; exception raised |
| 81 | tests/unit/test_metered_clients.py | test_metered_clients_aclose_closes_inner | a fake inner records the aclose call |
| 82 | tests/integration/test_telemetry.py | test_chat_request_server_span_continues_incoming_trace | POST /v1/chat with TRACEPARENT: every exported span has trace id `4bf9…4736`; the SERVER span's parent span id = `00f067aa0ba902b7`; `X-Trace-Id` equals it |
| 83 | tests/integration/test_telemetry.py | test_chat_request_without_traceparent_header_matches_server_span | `X-Trace-Id` == the SERVER span trace id (hex) |
| 84 | tests/integration/test_telemetry.py | test_health_request_creates_no_server_span | GET /health gives 0 spans |
| 85 | tests/integration/test_telemetry.py | test_chat_request_records_model_metrics_through_app | the reader shows a `gen_ai.client.operation.duration` point with op `chat` |

### Web (Vitest + Testing Library + MSW)
| # | File | Test | Asserts |
|---|---|---|---|
| 86 | shared/lib/clientErrorReporter.test.ts | posts message, name, stack, source and path when an error is reported | the captured body has the fields; path = pathname, no query |
| 87 | same | truncates message and stack to the server limits | lengths are 500 and 4000 |
| 88 | same | does not report an ApiError or an AbortError | no request captured |
| 89 | same | sends a repeated error only once | 1 body for two identical reports |
| 90 | same | stops after the per-page limit | 10 bodies for 12 distinct errors |
| 91 | same | reports window errors and unhandled rejections once installed | dispatching an `ErrorEvent` and a `PromiseRejectionEvent`-like event produces sources `Window` and `UnhandledRejection` |
| 92 | same | stops listening after cleanup | no body after the remover runs |
| 93 | same | ignores a failed report | server 500: the test completes with no unhandled rejection |
| 94 | shared/components/RouteError.test.tsx | reports an unexpected route error | a thrown `new Error('boom')` gives a captured body with message `boom` and source `Route` |
| 95 | shared/components/RouteError.test.tsx | does not report an ApiError | no body |

### promtool (`deploy/observability/prometheus/tests/elmanhg.rules.test.yml`, run by the smoke test)
| # | Case | Asserts |
|---|---|---|
| 96 | fast burn: 10 % 5xx for 70 m | `ApiErrorBudgetFastBurn` firing at 70m |
| 97 | fast burn: 0.1 % 5xx | no alert |
| 98 | slow burn: 4 % 5xx for 7 h | `ApiErrorBudgetSlowBurn` firing |
| 99 | probe_success 0 for 5 m | `ServiceDown{target_name="api"}` firing |
| 100 | probe_success 1 | no ServiceDown |
| 101 | last_success 400 s old, interval 60 | `BackgroundJobStale` firing |
| 102 | last_success 60 s old, interval 60 | no BackgroundJobStale |
| 103 | 3 failed runs in 15 m | `BackgroundJobFailing` |
| 104 | 5 failed OTP sends in 15 m | `OtpDeliveryFailing` critical |
| 105 | 1 UNHANDLED_EXCEPTION | `ApiUnhandledErrors` |
| 106 | 5 AI_SERVICE_UNAVAILABLE | `ProviderUnavailable{elmanhg_outcome="AI_SERVICE_UNAVAILABLE"}` |
| 107 | no target_info for 20 m | `ApiTelemetryMissing` |

### End-to-end (smoke): the 12 observability assertions listed in the smoke spec, plus `/api/health` = `Healthy`.

**Mutation checks** (implementer, per PROGRESS): remove the `metrics.Record…` line from each behaviour and from the router, and confirm #17, #20 and #45 fail. Remove `LogRedactor.Redact` from the handler, and confirm #28 fails. Remove the `Activity.Current?.AddException` line, and confirm #56 fails. Change the fast-burn factor, and confirm #96 fails.

## Definition of done
- [ ] Every Scope → In item is implemented, and the four Deferred items are listed in `02-implementation.md` for the orchestrator's issue.
- [ ] Packages added exactly as named (NuGet: 4 OTel + Diagnostics.Testing; PyPI: 4 OTel). The registry verification output is pasted in the report (momenta-dependency-policy §2). `uv.lock` is regenerated with `uv lock`. `dotnet list package --vulnerable --include-transitive` and `uvx pip-audit` are clean.
- [ ] `dotnet build api/ -c Release` has zero new warnings. `dotnet test api/ -c Release` is green **with `appsettings.json` moved aside** (CI parity) and restored afterwards.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated/**` are regenerated and committed. The Postman `Observability` folder is added.
- [ ] `uv --directory ai run ruff format --check .`, `ruff check .`, `mypy src` and `pytest -m "not eval"` are green.
- [ ] `npm --prefix web run typecheck`, `lint` and `test` are green. `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` is clean.
- [ ] `bash deploy/smoke-test.sh` passes in CI, with the observability profile on and all 12 assertions plus the config validations (promtool check and test, amtool, otelcol validate, caddy validate).
- [ ] `AuditBehaviour` is still outermost, and `RequestMetricsBehaviour` sits right inside it (tests #58–60).
- [ ] No feature handler file is modified: `git diff origin/main --stat` shows no `*Handler.cs` under `Sessions/`, `Subscriptions/`, `Payments/` or `Auth/`, and no `ai/src/elmanhg_ai/pipelines/*` or `api/chat/*`.
- [ ] No PII is logged. The new log templates carry only redacted text. Caddy logs drop headers and query. The collector redact processor is present, and smoke assertion 10 passes.
- [ ] Metric tags use only the namespaced keys from Decision 15, with bounded values (no ids).
- [ ] Every image is pinned `tag@sha256` (from `docker buildx imagetools inspect`). Grafana is published on 127.0.0.1 only. No other new host port.
- [ ] With `OTLP_ENDPOINT` empty (the default), the stack runs with no export errors in the api or ai logs.
- [ ] `deploy.sh` refuses the profile without `GRAFANA_ADMIN_PASSWORD` or `alertmanager.yml`. `.gitignore` covers `/deploy/alertmanager.yml`.
- [ ] Docs-sync is complete: `docs/observability.md` is new; `docs/deployment.md` (public `/api/health`, services, env, smoke knobs, §13), `docs/ai-service.md`, `docs/PRD.md` §14/§18, `docs/constitution.md` §4 and python skill delta 5 are updated. No doc still says `/health` is unproxied or that monitoring "comes with #113".
- [ ] Every test in the Test plan exists with the exact name, and no existing test was weakened. The worker, router and OTP DI test edits are limited to construction helpers.
- [ ] The guard grep prints nothing: `git diff origin/main -U0 -- '*.cs' | grep -E '^\+.*(DateTime\.(Now|UtcNow)|\.Result\b|\.Wait\(\)|new HttpClient\(|FromSqlRaw|async void)'`, and the python guard from python-feature §15.
