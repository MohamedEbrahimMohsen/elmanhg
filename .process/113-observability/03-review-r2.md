VERDICT: CHANGES_REQUESTED

# Review r2: [E13.S2] Observability (#113)

Scope: the round-1 findings #1 and #2, and everything that `02-implementation-r2.md` claims, checked against the working tree on `feature/113-observability`.

## Blocking

### 1. Fixed: the API request log keeps query keys only
The fix is confirmed. See Verified.

### 2. Fixed: counter alerts miss the first event of a new series
The fix is confirmed. See Verified.

### 3. Caddy's error log still writes raw query values, raw client IPs and forwarded-IP headers to Loki
**Where:**
- `deploy/Caddyfile:15-27`: the `log` block is the site's access logger (`http.log.access.log0`) only.
- `deploy/observability/otel-collector/config.yaml:14-15,89-92`: every container's stdout and stderr is tailed, and only the email and phone patterns are masked.

**Rule:**
- `docs/observability.md` §12, line 181: "Where a query value is sensitive anyway (the Paymob webhook `hmac`, ...), no log keeps it".
- §12, line 183: IPs are truncated.
- `docs/constitution.md` §4: no tokens or payment payloads in logs.
- This is a docs-sync divergence, and part of the round-1 finding #1 scope ("no path still logs raw query values").

**Problem:** The Caddyfile `log` directive puts its filter encoder on the access logger only. Caddy writes handler errors to the separate `http.log.error.log0` logger, which falls through to the default logger. That logger has no filter. Each such entry carries the full `request` object: the raw `uri` including the query, raw `remote_ip` and `client_ip`, and the request headers. Caddy redacts only Cookie and Authorization, so `Cf-Connecting-Ip` and `X-Forwarded-For` stay intact.

**Failure:** I reproduced this with the pinned `caddy:2.10-alpine@sha256:4c6e91c6…` image and this Caddyfile (`SITE_ADDRESS=:80`, with the `api` upstream unreachable, as during an api restart or crash). `POST /api/payments/paymob/webhook?hmac=SECRETHMAC123` returned 502 and produced two lines:
- `http.log.access.log0`: `"uri":"/api/payments/paymob/webhook?redacted"`, `"remote_ip":"172.17.0.0"`. This one is correct.
- `http.log.error.log0`: `"uri":"/api/payments/paymob/webhook?hmac=SECRETHMAC123"`, `"remote_ip":"172.17.0.1"`, and `"headers":{...}`. This one leaks.

The collector ships the second line to Loki for 14 days. Neither the hmac nor the IPs match the redaction patterns. Paymob retries webhooks, so any api outage or deploy window records them.

**Fix:** Put the same filter on Caddy's default logger in a global options block, so that the `http.log.error.*` entries are filtered too:

```
{ log default { output stdout; format filter { wrap json; fields { request>uri regexp `\?.*$` "?redacted"; request>headers delete; request>remote_ip ip_mask 24 48; request>client_ip ip_mask 24 48 } } } }
```

Also make `docs/observability.md` §5 (web row) and §12 (Caddy line) say that the error log is filtered too.

## Non-blocking
- `deploy/observability/prometheus/rules/elmanhg.rules.yml:62-113`: `unless x offset w` in the second `or` operand is redundant. `or` already drops the right-hand series that match left-hand labels, and a mutant using plain `x` there passes the whole suite. The rule is still correct; this only simplifies it.
- `api/core-libraries/Core.Logging/RequestLogScrubber.cs:25`: an `X-Forwarded-For` hop written with a port (`203.0.113.7:5000`) fails `IPAddress.TryParse` and logs as empty. It fails safe.
- `api/core-libraries/Core.Logging/DependencyInjection.cs:105`: the Azure Table column list still names `Latitude` and `Longitude`, which are no longer logged. This does no harm.

## Verified
- **Finding 1, API request log.**
  - `RequestLoggingMiddleware.cs:47` logs `RequestLogScrubber.Query`, which keeps the keys and turns every value into `[redacted]` (`RequestLogScrubber.cs:14-17`).
  - There is no other `QueryString`, `Request.Query` or `GetDisplayUrl` use in non-test `api/` code.
  - The ASP.NET Core hosting "Request starting" line, which carries the URL, is suppressed in production: the image copies `appsettings.example.json`, which sets `Microsoft.AspNetCore: Warning` (`api/Dockerfile:18`).
  - OTel AspNetCore and Http instrumentation 1.19 redact span query values by default.
- **Finding 1, ai service.**
  - uvicorn runs with `--no-access-log` (`ai/Dockerfile:19`), and the `uvicorn.access` logger has no handlers.
  - `request.completed` logs `request.url.path` only (`ai/src/elmanhg_ai/core/middleware.py:50-57`).
  - The ai routes take no query parameters.
- **Finding 1, Caddy access log.** At runtime it shows `?redacted` and IPs masked to `.0`. The error log does not (finding 3).
- **Finding 1, redaction patterns.** The `%40` email pattern is identical in `LogRedactor.cs:11` and the collector `config.yaml:89,91,98,102`, and is tested (`LogRedactorTests.Redact_UrlEncodedEmail_ReplacesWithMarker`). The traces pipeline now includes `transform/redact-traces` (`config.yaml:127`).
- **IP truncation is correct.**
  - The masks are 24 bits (IPv4) and 48 bits (IPv6), and IPv4-mapped addresses are normalised first.
  - Unparseable values and null give an empty string.
  - Each forwarded hop is truncated, and `Latitude` and `Longitude` are no longer logged. `RequestLogScrubberTests` and `CoreRequestLoggingMiddlewareTests` assert all of this through the real middleware.
- **Finding 2, alert rewrites.**
  - All 8 counter alerts use `(increase(x[w]) unless NEW) or NEW`, where NEW is the series with no sample at `t-w`.
  - `or` is a label-keyed union, so a series is counted once. I found no double count.
  - A new series fires on its whole value, then resolves once it ages past `w`, when its increase is 0. It does not re-fire.
  - Counter resets on a stable series go through `increase`. A restart that changes `instance` produces a new series.
- **promtool (prom/prometheus v3.14.0).**
  - `check rules`: SUCCESS, 17 rules.
  - `test rules`: SUCCESS.
  - Mutation check: I rebuilt the rules without the new-series term, and all 6 new firing cases FAILED, as did the 4 older firing cases.
  - "four failed otp sends on a new series stay quiet" and "an old unhandled exception outside the window stays quiet" constrain the threshold and the age boundary.
- **.NET tests.** I ran `dotnet test api/ -c Release` with `appsettings.json` moved aside, then restored it: Passed, 2918 total, 0 failed, 0 skipped. That matches the claimed count.
- **Request size limit.** `ClientErrorsController.cs:78,83` has `[RequestSizeLimit(16384)]`, and docs §11 says "16 KB, then 413".
- **Docs.** §5 (api row) and §8 (new-series paragraph, line 152) match the code. §12 matches everywhere except the Caddy error-log gap (finding 3).
- **Deviations.** The 8-alert scope and the absence of code-side pre-creation are declared and hold up.

## Test quality
- **Constrain the implementation:**
  - `RequestLogScrubberTests`: exact output strings for the query, IPv4, IPv6, mapped, invalid and null cases.
  - `CoreRequestLoggingMiddlewareTests`: runs the real middleware with a capturing sink, and asserts the absence of `mona` and of the `Latitude` and `Longitude` keys.
  - `LogRedactorTests` (`%40` case).
  - The new promtool cases, which fail against the old rules.
- **Does not constrain anything:** nothing in the Caddy config covers the error-logger path (finding 3). Nothing else is new here.

## Round 3 verification (orchestrator)
Finding #3 is fixed: a global `log default` filter now covers the Caddy error log. Re-running the reviewer case on the pinned image gives 0 matches for the secret, raw IPs and forwarded headers. `caddy validate` passes. VERDICT: APPROVED
