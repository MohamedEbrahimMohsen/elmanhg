# [E21.S10] Core.Hosting, Core.Observability, Core.Cache, Core.Spreadsheets, rate limits, redaction

Issue: #313

Promote the remaining generic hosting and cross-cutting pieces.

- New `Core.Hosting`: `ReverseProxyExtensions` + options/validator, a generic `MigrationCommand<TContext>`, `KestrelHardening`, and the placeholder-secret guard mechanism (the key list and the Npgsql password check stay in the app and are passed in).
- New `Core.Observability`: the OpenTelemetry setup from `ObservabilityExtensions` + options/validator, and the generic `RequestMetricsBehaviour` (meter name as a parameter; app metric names unchanged).
- Fill `Core.Cache`: `ICacheableQuery { CacheKey, Ttl }` + a MediatR caching behaviour (from `DashboardCacheBehaviour`); dashboards switch to it with identical keys and TTLs.
- New `Core.Spreadsheets`: the ClosedXML reader/writer (error code and right-to-left as options); the 3 consumers switch.
- Rate limiting: `RateLimitPartitions.PerClientIp/PerUser/FixedWindow` and the `OnRejected → RateLimitExceededCoreException` wiring move to core; policy names and options stay in the app.
- Redaction: one configurable text redactor in Core.Logging next to `RequestLogScrubber`; `LogRedactor` uses it. `TrainingDataScrubber` stays in the app (app-specific keys) but may reuse the shared patterns.
- Behaviour, metric names and config keys unchanged.

### Sub-tasks
- [ ] Core.Hosting
- [ ] Core.Observability and RequestMetricsBehaviour
- [ ] Core.Cache caching behaviour; dashboards switched
- [ ] Core.Spreadsheets
- [ ] Rate-limit partitions; redactor
- [ ] Tests and docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

