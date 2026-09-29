# Implementation — [E13.S2] Observability (#113)

Branch `feature/113-observability` (worktree `D:\Personal\elmanhg-wt\113`). `origin/main` merged first (fast-forward to `b16a8f0`, #94 teacher threads; no conflicts, so no merge commit was needed). Story work is uncommitted.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Application/Shared/Observability/ElmanhgTelemetry.cs | 10 | `Elmanhg` ActivitySource + meter name |
| api/Elmanhg.Application/Shared/Observability/ElmanhgMetrics.cs | 49 | request, quiz, payment, OTP and client-error instruments |
| api/Elmanhg.Application/Shared/Observability/BackgroundJobMetrics.cs | 74 | per-sweep job runs/duration/items + last-success/interval gauges + sweep span |
| api/Elmanhg.Application/Shared/Observability/BackgroundJobRun.cs | 49 | one sweep (derived outcome, completes once) + `BackgroundJobRunOutcome` |
| api/Elmanhg.Application/Shared/Observability/RequestMetricsBehaviour.cs | 40 | open-generic MediatR behaviour: `elmanhg.requests` by outcome |
| api/Elmanhg.Application/Shared/Observability/QuizAnswerMetricsBehaviour.cs | 17 | SubmitAnswer → `elmanhg.quiz.answers` |
| api/Elmanhg.Application/Shared/Observability/PaymentNotificationMetricsBehaviour.cs | 15 | ProcessPaymentNotification → `elmanhg.payment.notifications` |
| api/Elmanhg.Application/Shared/Observability/LogRedactor.cs | 18 | email + Egyptian mobile redaction (NonBacktracking) |
| api/Elmanhg.Application/Shared/Observability/ClientErrorSource.cs | 3 | enum |
| api/Elmanhg.Application/Shared/Options/ClientErrorsOptions.cs | 26 | rate limit + size caps |
| api/Elmanhg.Application/Observability/ReportClientError/ReportClientErrorCommand.cs | 6 | command |
| api/Elmanhg.Application/Observability/ReportClientError/ReportClientErrorValidator.cs | 20 | 7 rules / 7 codes |
| api/Elmanhg.Application/Observability/ReportClientError/ReportClientErrorHandler.cs | 17 | metric + one redacted Warning |
| api/Elmanhg.Infrastructure/Hosting/ObservabilityOptions.cs | 26 | options + `ExportEndpoint` |
| api/Elmanhg.Infrastructure/Hosting/ObservabilityOptionsValidator.cs | 26 | endpoint scheme + header shape (never echoes header values) |
| api/Elmanhg.Api/Hosting/ObservabilityExtensions.cs | 82 | `AddElmanhgObservability`: options, metrics, behaviours, OTel tracing/metrics, OTLP/gRPC when configured |
| api/Elmanhg.Api/RateLimiting/ObservabilityRateLimitPolicies.cs | 6 | `client-errors` policy name |
| api/Elmanhg.Api/Controllers/Observability/ClientErrorsController.cs | 24 | `POST /api/client-errors` (anonymous, rate-limited) |
| api/Elmanhg.Api/Controllers/Observability/Requests.cs | 5 | `ReportClientErrorRequest` |
| api/Elmanhg.Tests/Application/Features/Shared/Observability/MeterFactories.cs | 9 | test `IMeterFactory` |
| api/Elmanhg.Tests/Application/Features/Shared/Observability/ElmanhgMetricsTests.cs | 76 | tests #1–5 |
| api/Elmanhg.Tests/Application/Features/Shared/Observability/BackgroundJobMetricsTests.cs | 120 | tests #6–11 |
| api/Elmanhg.Tests/Application/Features/Shared/Observability/RequestMetricsBehaviourTests.cs | 72 | tests #12–16 (+ `MetricsProbeRequest`) |
| api/Elmanhg.Tests/Application/Features/Shared/Observability/QuizAnswerMetricsBehaviourTests.cs | 56 | tests #17–19 |
| api/Elmanhg.Tests/Application/Features/Shared/Observability/PaymentNotificationMetricsBehaviourTests.cs | 44 | tests #20–21 |
| api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorTests.cs | 39 | tests #22–26 |
| api/Elmanhg.Tests/Application/Features/Observability/ReportClientError/ReportClientErrorHandlerTests.cs | 70 | tests #27–29 |
| api/Elmanhg.Tests/Application/Features/Observability/ReportClientError/ReportClientErrorValidatorTests.cs | 72 | tests #30–38 |
| api/Elmanhg.Tests/Infrastructure/Hosting/ObservabilityOptionsValidatorTests.cs | 55 | tests #39–44 |
| api/Elmanhg.Tests/Integration/Observability/ClientErrorsEndpointTests.cs | 80 | tests #61–64 |
| api/Elmanhg.Tests/Integration/Observability/TelemetryRegistrationTests.cs | 29 | tests #65–66 |
| ai/src/elmanhg_ai/core/telemetry.py | 79 | tracer/meter providers, OTLP/gRPC only with an endpoint, FastAPI instrumentation |
| ai/src/elmanhg_ai/clients/metered.py | 170 | `AiMetrics`, `MeteredModelClient`, `MeteredEmbeddingClient` (GenAI semconv + cost) |
| ai/tests/unit/test_telemetry.py | 40 | tests #71–75 |
| ai/tests/unit/test_metered_clients.py | 203 | tests #76–81 |
| ai/tests/integration/test_telemetry_app.py | 133 | tests #82–85 (renamed, see Deviations) |
| web/src/shared/lib/clientErrorReporter.ts | 80 | browser error reporter (limits, dedupe, per-page cap, path only) |
| web/src/shared/lib/clientErrorReporter.test.ts | 151 | tests #86–93 |
| web/src/shared/api/generated/client-errors/*, zod/client-errors/*, model/clientErrorSource.ts, model/reportClientErrorRequest.ts | gen | Orval output |
| deploy/observability/otel-collector/config.yaml | 124 | OTLP in, filelog tail, redaction, Tempo/Prometheus/Loki out |
| deploy/observability/prometheus/prometheus.yml | 42 | OTLP receiver settings, rules, Alertmanager, blackbox scrape |
| deploy/observability/prometheus/rules/elmanhg.rules.yml | 116 | 4 recording + 13 alert rules |
| deploy/observability/prometheus/tests/elmanhg.rules.test.yml | 189 | 12 promtool cases (#96–107) |
| deploy/observability/alertmanager/alertmanager.example.yml | 30 | null default receiver + commented Resend SMTP / webhook |
| deploy/observability/loki/config.yaml | 40 | single binary, 14 d retention |
| deploy/observability/tempo/config.yaml | 28 | single binary, 7 d retention |
| deploy/observability/grafana/provisioning/datasources/datasources.yml | 47 | Prometheus, Loki (→Tempo), Tempo (→Loki), Alertmanager |
| deploy/observability/grafana/provisioning/dashboards/dashboards.yml | 11 | file provider, folder Elmanhg |
| deploy/observability/grafana/dashboards/service-health.json | 504 | `elmanhg-service-health` (12 panels) |
| deploy/observability/grafana/dashboards/background-jobs.json | 250 | `elmanhg-background-jobs` (6 panels + `job` variable) |
| deploy/observability/grafana/dashboards/business.json | 342 | `elmanhg-business` (10 panels) |
| docs/observability.md | 223 | runbook, 15 sections in plan order |

## Files modified
| Path | Change |
|---|---|
| api/Directory.Packages.props | OTel Extensions.Hosting 1.19.1, Exporter.OpenTelemetryProtocol 1.19.1, Instrumentation.AspNetCore 1.19.0, Instrumentation.Http 1.19.0; Testing: Microsoft.Extensions.Diagnostics.Testing 10.7.0 |
| api/Elmanhg.Api/Elmanhg.Api.csproj, api/Elmanhg.Tests/Elmanhg.Tests.csproj | package references |
| api/Elmanhg.Api/Program.cs | comment + `AddElmanhgObservability(...)` after `AddCoreAuditing` |
| api/core-libraries/Core.Logging/LoggingOptions.cs, DependencyInjection.cs | `ConsoleLogFormat { Text, Json }`; Json → `RenderedCompactJsonFormatter` |
| api/core-libraries/Core.Exceptions/ExceptionMiddleware.cs | `ErrorCodeTag`; tag the span; `AddException` for 5xx |
| api/Elmanhg.Api/Workers/{ExpiredExamSubmission,SubscriptionLapse,LessonContentIndex}Worker.cs | `BackgroundJobMetrics` ctor param, `JobName`, `Register`, `StartRun`, item counts, list returns `null` on failure |
| api/Elmanhg.Api/RateLimiting/AuthRateLimiting.cs | `client-errors` fixed-window policy per IP |
| api/Elmanhg.Infrastructure/OtpDelivery/OtpChannelRouter.cs | `ElmanhgMetrics` param; Delivered / Failed (rethrow) |
| api/Elmanhg.Application/DependencyInjection.cs | bind + validate `ClientErrorsOptions` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | `// OBSERVABILITY` group (7 codes) after ANALYTICS |
| api/Elmanhg.Api/Resources/Messages.ar.resx, Messages.en.resx | 7 entries after `FUNNEL_EVENT_TYPE_INVALID` |
| api/Elmanhg.Api/appsettings.example.json | `Console.Format`, `ClientErrors`, `Observability` sections |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | 6 `ClientErrors:*` keys |
| api/Elmanhg.Tests/Api/Workers/*WorkerTests.cs (3) | `_meterFactory`, `BackgroundJobMetrics` in construction, 3 new tests each (#47–55) |
| api/Elmanhg.Tests/Infrastructure/OtpDelivery/OtpChannelRouterTests.cs | `_meterFactory`, `Router()` ctor, tests #45–46 |
| api/Elmanhg.Tests/Infrastructure/OtpDelivery/OtpDeliveryServiceCollectionExtensionsTests.cs | `AddMetrics()` + `ElmanhgMetrics` in `BuildProvider` |
| api/Elmanhg.Tests/Core/Exceptions/CoreExceptionMiddlewareTests.cs | tests #56–57 |
| api/Elmanhg.Tests/Integration/Composition/PipelineCompositionTests.cs | tests #58–60 |
| api/openapi/v1.json | regenerated: `/api/client-errors`, `ReportClientErrorRequest`, `ClientErrorSource` |
| postman/elmanhg.postman_collection.json | folder `Observability` → `Report client error` (noauth, example body) |
| web/src/main.tsx | `clientErrorReporter.install(window)` after `initI18n()` |
| web/src/shared/components/RouteError.tsx | reports the route error in an effect |
| web/src/shared/components/RouteError.test.tsx | `beforeEach` reset, `failure` typed `Error`, tests #94–95 |
| web/src/test/msw/server.ts | default `getReportClientErrorMockHandler()` |
| web/src/shared/api/generated/{index.ts, model/index.ts, zod/index.zod.ts} | regenerated |
| ai/pyproject.toml, ai/uv.lock | 4 OTel packages; `uv lock` (+ transitive grpcio, protobuf, wrapt, …) |
| ai/src/elmanhg_ai/settings.py | 6 fields + 2 validators |
| ai/src/elmanhg_ai/core/middleware.py | `current_span_trace_id()`; span id first, then `traceparent`, then random |
| ai/src/elmanhg_ai/main.py | `telemetry` param, instrumentation, metered wrappers in `lifespan`, `otlp_exporting`, shutdown |
| ai/tests/unit/test_settings.py | tests #67–70 |
| deploy/docker-compose.prod.yml | logging label, observability logging anchor, api/ai env, 7 pinned services, 6 volumes |
| deploy/Caddyfile | `(api_health)` snippet, JSON access log with header/query filter, imports in both sites |
| deploy/.env.example, api.env.example, ai.env.example | Observability blocks |
| deploy/lib.sh | `profile_enabled`, `check_observability_config` |
| deploy/deploy.sh | config check after the tag check; waits for prometheus/alertmanager/blackbox/grafana |
| deploy/smoke-test.sh | knobs, config validations, public checks, 12 observability assertions |
| .github/workflows/images.yml | deploy-smoke timeout 40 |
| .github/workflows/deploy.yml | `scp -r … deploy/observability` |
| .gitignore | `/deploy/alertmanager.yml` |
| docs/deployment.md | §1 services + public `/api/health`, §3 host file, §4 env + Observability/ClientErrors + set-by-compose, §5 pinning, §6 sizing + setup, §7 upload + deploy.sh, §10 checks, §12 knobs, §13 row |
| docs/ai-service.md | 6 config keys; "Health, logging and telemetry" (spans, metrics, trace id source) |
| docs/PRD.md | §14 availability note; §18 Observability bullet |
| docs/constitution.md | §4 Observability rule |
| .claude/skills/python-feature/SKILL.md | delta 5 replaced |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Integration test file `ai/tests/integration/test_telemetry.py` | The test dirs have no `__init__.py`; with `tests/unit/test_telemetry.py` also planned, pytest's default import mode refuses two modules with the same basename ("import file mismatch") | Named it `ai/tests/integration/test_telemetry_app.py`. Test names unchanged |
| Worker test edits limited to the `RunAsync` helper | `SubscriptionLapseWorkerTests` and `LessonContentIndexWorkerTests` also construct the worker directly in `Execute_Disabled_EndsWithoutSweeping` | Added the `BackgroundJobMetrics` argument there too (construction only; assertions untouched) |
| `clientErrorReporter.report` skips `error instanceof Error && error.name === 'AbortError'` | jsdom's `DOMException` is not `instanceof Error` (browsers' is), so an aborted fetch in tests was reported | `(error instanceof Error \|\| error instanceof DOMException) && error.name === 'AbortError'` |
| Collector `transform/redact` statements as listed | `replace_all_patterns(attributes, …)` only reaches top-level values; Caddy's JSON nests the URI (`request.uri`), so a raw email would reach Loki structured metadata | Added `flatten(attributes)` as the first statement (comment explains). Smoke assertion 10 proves it |
| Loki healthcheck only if `wget` exists | `grafana/loki:3.7.8` has no `wget` (exit 127); `grafana/grafana:13.1.6` has it; Tempo has none | Loki: no healthcheck and removed from the deploy.sh wait loop (plan's own rule). Grafana: healthcheck kept |
| Smoke uses `local/elmanhg-*:smoke` | Parallel lanes share Docker and the `:smoke` tags | Added knob `SMOKE_IMAGE_TAG` (default `smoke`, so CI unchanged); documented in deployment.md §12 |
| Smoke assertion 9: Loki `{service_name="elmanhg-api"}` contains the trace id | An unfiltered query returns the newest 100–1000 lines and can miss the line | Query `{service_name="elmanhg-api"} \| trace_id="<id>"` (structured-metadata filter), which also proves the collector's trace parsing |
| Smoke assertion 12: `in_prometheus http://alertmanager:9093/-/ready` | Worked, but printed `OK` into the log | Wrapped in `alertmanager_ready` capturing the body |
| promtool #101 "last_success 400 s old, interval 60" | With `for: 5m` the alert cannot be firing when the condition (age > 180 s) has held for only 220 s | Test uses last success at 0 and evaluates at 10 m (age 600 s). #102 (60 s old) as planned |
| `RequestMetricsBehaviour` literals `"VALIDATION_FAILED"`, `"CANCELLED"` | constitution §0.3 (no magic values) | Private consts `ValidationFailedOutcome`, `CancelledOutcome`, `UnhandledOutcome` (Application does not reference `Core.Exceptions`, as the plan anticipated) |
| Dashboard `schemaVersion` "as Grafana 13 writes it" | Highest version in the 13.1.6 frontend bundle is 42 | 42 |

## Build & test
- Merge: `git fetch origin main` + `git merge origin/main` → `Fast-forward 733b0fe..b16a8f0`.
- NuGet registry check (flatcontainer index): OpenTelemetry.Extensions.Hosting and Exporter.OpenTelemetryProtocol latest `1.19.1`; Instrumentation.AspNetCore and Instrumentation.Http latest `1.19.0`; Microsoft.Extensions.Diagnostics.Testing lists `10.7.0` (latest 10.10.0; 10.7.0 matches the repo's Http.Resilience 10.7.0).
- PyPI check: `opentelemetry-api 1.44.0 Apache-2.0 2026-07-16`, `opentelemetry-sdk 1.44.0 Apache-2.0 2026-07-16`, `opentelemetry-exporter-otlp-proto-grpc 1.44.0 Apache-2.0 2026-07-16`, `opentelemetry-instrumentation-fastapi 0.65b0 Apache-2.0 2026-07-16` (all before `exclude-newer 2026-09-22`). `python -m uv lock` → `Resolved 55 packages`. mypy strict needed no overrides.
- Image digests (`docker buildx imagetools inspect`): collector-contrib 0.161.0 `fd328de2…`, prometheus v3.14.0 `5ce7540c…`, alertmanager v0.34.1 `e9733baf…`, blackbox v0.28.0 `e753ff9f…`, loki 3.7.8 `1107dd52…`, tempo 2.10.8 `f0561deb…`, grafana 13.1.6 `d8276d62…` (full digests in compose).
- `dotnet build api/` → `0 Warning(s)`, `Build succeeded.` (the pre-existing CS8618/CS8602 warnings in vendored Core.Notifications/OTP/Validation appear only in a clean Release build and are unchanged).
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside (restored afterwards) → `Passed! total: 2905, failed: 0, succeeded: 2905, skipped: 0`.
- `dotnet list package --vulnerable --include-transitive` → every project "has no vulnerable packages".
- ai, as `ai-ci.yml`: `uv sync --locked` ok; `ruff format --check .` → 57 files already formatted; `ruff check .` → All checks passed; `mypy src` → no issues in 34 source files; `pytest -m "not eval" --cov…` → `116 passed`, total coverage 97 %; `pip-audit==2.10.1` on the exported requirements → No known vulnerabilities found. Docker image build ran inside the smoke test.
- web: `npm ci`; `npm run gen:api`; `npm run typecheck` ok; `npm run lint` ok (0 warnings); `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → All matched files use Prettier code style; `npm test -- --run` → `Test Files 150 passed (150)`, `Tests 888 passed (888)`.
- promtool: `check config` → SUCCESS (17 rules); `test rules` → SUCCESS (12 cases). `amtool check-config` on the example → SUCCESS; `otelcol-contrib validate` → exit 0; `caddy validate` (caddy:2.10 pinned) → Valid configuration; `loki -verify-config` → exit 0.
- Smoke with the observability profile (Windows, Docker Desktop, lane-unique project `elmanhg-smoke-113`, ports 8113/8513/3313, subnet 172.30.113.0/24, tag `smoke113`): first run failed on my own embeddings assertion (wrong field name); after the fix `SMOKE_SKIP_BUILD=1 … bash deploy/smoke-test.sh` → all 12 `observability:` assertions printed, `Smoke test passed`, exit 0. Stack and volumes removed by the script's cleanup. A final run after the last script edit passed the same way (exit 0). Containers, volumes and the `smoke113` images were removed afterwards.
- Mutation checks (each restored afterwards): removing `RecordQuizAnswer` → #17/#18 fail; `RecordPaymentNotification` → #20 fails; router `RecordOtpSend(…, delivered: true)` → #45 fails; `LogRedactor.Redact` on message/stack → #28 fails (the classifier allowed it); `Activity.Current?.AddException` → #56 fails; fast-burn factor 14.4→24 → promtool #96 fails; Python: cost `None` → #76 fails, middleware without `current_span_trace_id()` → #83 fails, `excluded_urls=None` → #84 fails.
- Guard greps (`.cs` and `.py` from the skills) print nothing. `git diff origin/main --stat` shows no modified `*Handler.cs` under Sessions/Subscriptions/Payments/Auth and nothing under `ai/src/elmanhg_ai/pipelines/` or `api/chat/`.

## Test counts
- .NET: 66 new test methods (plan #1–#66; #4 is a 2-row Theory); suite 2905 passed.
- Python: 19 new (#67–#85); suite 116 passed.
- Web: 10 new (#86–#95); suite 888 passed.
- promtool: 12 cases (#96–#107).
- Smoke: the 12 observability assertions + `/api/health` = `Healthy` + config validations.

## Deferred (for the orchestrator's `deferred` issue)
1. External uptime monitor and dead-man's switch polling `https://<site>/api/health` (needs an account and the live domain).
2. Vendor error-tracking SDK (Sentry: grouping, source maps, releases); any OTLP SaaS works today via `OTLP_ENDPOINT` + `*_OTLP_HEADERS`.
3. Live alert receiver credentials (Resend SMTP / webhook in `alertmanager.yml`; default is the null receiver).
4. Forwarding logs to a SaaS (logs stay in the local Loki in SaaS mode).

## Notes for review
- **AGPL (for the dev to confirm):** Grafana, Loki and Tempo are AGPL; they run unmodified as separate operator containers, never linked or shipped as product code (documented in docs/observability.md §2).
- **Existing request log carries client IP, user agent and query string** (`Core.Logging/RequestLoggingMiddleware.cs`, unchanged by this story). With JSON logs now shipped to Loki, IP addresses land there too. The collector redacts emails/phones in them, but IPs are not redacted. Worth a follow-up decision (drop `ClientIp`/`QueryString` or mask them).
- `/api/client-errors` plan-gate conditions: per-IP fixed window (`client-errors`, 30/min default, 429 `TOO_MANY_REQUESTS`, integration-tested); size caps on all four text fields (validator + web truncation); message, name, stack and path go through `LogRedactor`; the web sends `location.pathname` only.
- A sweep interrupted by shutdown (list call cancelled) still disposes its run as `Succeeded` with 0 items, because cancellation is not a listing failure. Harmless (the process is stopping) but visible as one extra run at shutdown.
- `ElmanhgMetrics` and `BackgroundJobMetrics` both create meter `Elmanhg` from the same `IMeterFactory`; the factory returns one shared `Meter`, which is intended.
- The API memory panel uses `dotnet_process_memory_working_set_bytes`, the name observed in Prometheus during the smoke run (`/api/v1/label/__name__/values`).
- Windows note: the smoke passed locally with the profile on, so `/var/lib/docker/containers` is reachable on this Docker Desktop; the `SMOKE_OBSERVABILITY=0` escape hatch stays documented for machines where it is not.
