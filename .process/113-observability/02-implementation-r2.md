# Implementation r2 — [E13.S2] Observability (#113), rework for 03-review.md

## Blocking findings

| # | What I changed | File:line |
|---|---|---|
| 1 | The API request log keeps query keys only: every value becomes `[redacted]` (`?hmac=[redacted]`, `?actor=[redacted]`). The new `RequestLogScrubber.Query` does this, and the middleware calls it. | `api/core-libraries/Core.Logging/RequestLogScrubber.cs:14-17`, `api/core-libraries/Core.Logging/RequestLoggingMiddleware.cs:47` |
| 1 | The collector's email pattern matches a literal `@` or a URL-encoded `%40`, in the body and in attributes. `LogRedactor` uses the same pattern so the "same patterns" statement stays true. | `deploy/observability/otel-collector/config.yaml:89,91`, `api/Elmanhg.Application/Shared/Observability/LogRedactor.cs:11` |
| 1 | Tests for the Paymob `?hmac=` case and the `?actor=mona%40example.com` case, run through the real middleware with a capturing Serilog sink. Also scrubber unit tests and a `%40` redactor test. | `api/Elmanhg.Tests/Core/Logging/CoreRequestLoggingMiddlewareTests.cs:21-35`, `api/Elmanhg.Tests/Core/Logging/RequestLogScrubberTests.cs:10-30`, `api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorTests.cs:15` |
| 1 | Docs: §5 (api and Caddy rows) and §12 (query-string rule, layers 1 to 3, Caddy line) now state that query values are dropped, the `%40` match and the trace redaction. | `docs/observability.md:86,88,181,183-186` |
| 2 | I rewrote every counter-increase alert as `sum[by](( increase(x[w]) unless (x unless x offset w) ) or (x unless x offset w)) CMP`: for series older than the window it takes the increase, and for series born inside the window it takes the whole value. This covers ApiUnhandledErrors, ProviderUnavailable, OtpDeliveryFailing, AiModelErrors and PaymentNotificationNeedsReview, plus BackgroundJobFailing, BackgroundJobItemsFailing and ClientErrorSpike, which have the same cause. A header comment explains why. | `deploy/observability/prometheus/rules/elmanhg.rules.yml:2-3,62,69,78,85,92,99,106,113` |
| 2 | 8 new promtool cases on series that start at 1 with no 0 sample (`_x2 1 ...`): unhandled, payment flagged, OTP 5 (fires), OTP 4 (quiet), provider, model errors and job sweeps. One quiet case covers an old series with no increase inside the window. The existing cases are unchanged and still pass. | `deploy/observability/prometheus/tests/elmanhg.rules.test.yml:190-` |
| 2 | Docs §8: one paragraph on the new-series pattern and its one caveat (a wiped TSDB). | `docs/observability.md` §8, after the alert table |

## Non-blocking items (all cheap, all done)

| Item | Change |
|---|---|
| Client IPs | The middleware logs `ClientIp` and every `ForwardedFor` hop cut to /24 (IPv4) or /48 (IPv6). IPv4-mapped IPv6 addresses are normalised first, and unparseable values log as empty. `Latitude` and `Longitude` are no longer logged. Caddy masks `request>remote_ip` and `request>client_ip` with `ip_mask 24 48` (`deploy/Caddyfile:24-25`). |
| Request size | `[RequestSizeLimit(16384)]` on `POST /api/client-errors` (`ClientErrorsController.cs:15,20`), following the Paymob precedent. Docs §11 says "body at most 16 KB, then 413". |
| Trace redaction | New `transform/redact-traces` processor applies both patterns to span attributes and span-event attributes, and is wired into the traces pipeline. |

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/core-libraries/Core.Logging/RequestLogScrubber.cs` | 47 | Query-value redaction and IP truncation for the request log |
| `api/Elmanhg.Tests/Core/Logging/RequestLogScrubberTests.cs` | 60 | Unit tests for the scrubber |
| `api/Elmanhg.Tests/Core/Logging/CoreRequestLoggingMiddlewareTests.cs` | 82 | Tests the logged properties through the real middleware (non-parallel collection, because it swaps the static `Log.Logger`) |

## Files modified
`api/core-libraries/Core.Logging/RequestLoggingMiddleware.cs`, `api/Elmanhg.Application/Shared/Observability/LogRedactor.cs`, `api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorTests.cs`, `api/Elmanhg.Api/Controllers/Observability/ClientErrorsController.cs`, `deploy/observability/otel-collector/config.yaml`, `deploy/observability/prometheus/rules/elmanhg.rules.yml`, `deploy/observability/prometheus/tests/elmanhg.rules.test.yml`, `deploy/Caddyfile`, `docs/observability.md`.

## Deviations
| Asked | Reality | What I did |
|---|---|---|
| Rewrite "the four alerts" | The same cause affects 8 counter alerts, including the job and client-error alerts. | I applied the same pattern to all 8. |
| The review offered pre-creating enum-bounded series at 0 as an alternative | The rule-level pattern covers both the enum-bounded and the open-ended counters | I added no code-side pre-creation. |
| — | Rework scope: I added a new helper file and tests beyond the literal finding | The helper keeps the middleware readable and testable. I edited `LogRedactor` for pattern parity with the collector. |

## Build & test
- `dotnet test api/ -c Release`, with `api/Elmanhg.Api/appsettings.json` moved aside and restored afterwards: `Test run summary: Passed! total: 2918 failed: 0 succeeded: 2918`. That is 2905 before plus 13 new, and the log has 0 warnings.
- `promtool check config` (prom/prometheus v3.14.0 pinned): SUCCESS, 17 rules found.
- `promtool test rules`: SUCCESS.
- Mutation check: I rebuilt the old rules in the scratchpad and ran the new test file against them. All 6 new firing cases FAILED there, so the cases constrain the fix.
- `otelcol-contrib 0.161.0 validate` (tmpfs mounted on the file_storage directory, as compose's volume does): exit 0.
- `caddy validate` (caddy 2.10-alpine, `SITE_ADDRESS=:80`): Valid configuration.
- I did not re-run web or ai: this round touched neither.

## Notes for review
- The new-series pattern has one false-positive mode. After a Prometheus TSDB wipe, counters that are already non-zero look new for one window and can fire once. §8 documents this.
- `x unless x offset w` relies on the 5 m lookback at `t-w`. The SDKs export every cumulative point each interval, so a live series has no gaps longer than that.
- The middleware test swaps the global `Log.Logger`. Its collection is `DisableParallelization = true`, so no `ApiFactory` host can reassign the logger at the same time.
- `TestServer` does not implement `IHttpMaxRequestBodySizeFeature`, so the 16 KB limit has no integration test.
- The request log still keeps query keys. A key that is itself PII would still reach the collector, where the patterns mask it.
- The middleware keeps its pre-existing comment and style. It is upstream Core code, and I changed only the lines needed.

# Round 3 — review r2 finding #3

| Finding # | What I changed | File:line |
|---|---|---|
| 3 | Added a global options block that configures Caddy's `log default` with the same filter as the site access log: `output stdout`, `format filter { wrap json }`, `request>headers delete`, `resp_headers delete`, `request>uri regexp \?.*$ "?redacted"`, `request>remote_ip ip_mask 24 48` and `request>client_ip ip_mask 24 48`. The `http.log.error.*` entries (the site's and the `:2080` health site's) go to the default logger, so they are now filtered. | `deploy/Caddyfile:4-19` |
| 3 | §5 web row: the default logger (which carries `http.log.error.*`) is filtered like the access log, and both go to stdout. | `docs/observability.md:88` |
| 3 | §12 query-string bullet: every Caddy log replaces the whole query string with `?redacted`. The Caddy bullet now covers every Caddy log, the error log included, and names `X-Forwarded-For` and `Cf-Connecting-Ip` among the dropped headers. | `docs/observability.md:181,186` |

## Deviations
None. The default logger now writes to stdout instead of stderr. The collector tails both streams, so nothing is lost.

## Build & test
- `caddy validate --adapter caddyfile` (pinned `caddy:2.10-alpine@sha256:4c6e91c6…`, `SITE_ADDRESS=:80`): `redirected default logger from stderr to stdout`, then `Valid configuration`.
- Reproduction on the same pinned image (container `caddy-repro-113-r3`, `SITE_ADDRESS=:80`, no `api` upstream): `curl -X POST -H "X-Forwarded-For: 203.0.113.77" -H "Cf-Connecting-Ip: 198.51.100.99" "http://localhost:18113/api/payments/paymob/webhook?hmac=SECRETHMAC123"` returned `502`. The two log lines were:
  - `http.log.error.log0`: `"request":{"remote_ip":"172.17.0.0",...,"client_ip":"172.17.0.0",...,"uri":"/api/payments/paymob/webhook?redacted"}`, with no `headers` key.
  - `http.log.access.log0`: `"remote_ip":"172.17.0.0"`, `"uri":"/api/payments/paymob/webhook?redacted"`.
  - `docker logs … | grep -c -E "SECRETHMAC123|203\.0\.113\.77|198\.51\.100\.99|172\.17\.0\.1\b"` returned `0`.
- I removed the container afterwards (`docker rm -f caddy-repro-113-r3`).
- I did not re-run .NET, web, ai or promtool: this round touched only the Caddyfile and docs.

## Notes for review
- The error entry's `msg` holds the upstream error text (`dial tcp: lookup api: i/o timeout`), which does not contain the request URI. A filter cannot rewrite `msg`, so it is out of reach if an upstream error ever echoed the URL. Reverse-proxy dial errors do not.
