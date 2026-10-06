VERDICT: APPROVED

# Review: [E21.S10] Core.Hosting, Core.Observability, Core.Cache, Core.Spreadsheets, rate limits, redaction (round 1)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Application/DependencyInjection.cs:52`: `CachingOptions.DefaultTtl` is global, and the app derives it from `Dashboard:CacheSeconds`. That is correct today because only dashboard queries implement `ICacheableQuery`. A future non-dashboard `ICacheableQuery` with no `Ttl` would quietly get the dashboard TTL. Consider a short note in the SKILL delta bullet.
- `api/Elmanhg.Application/DependencyInjection.cs:26`: `services.AddMemoryCache()` is now redundant because `AddCoreCache()` calls it. The plan kept it on purpose, and the call is idempotent.
- `api/Elmanhg.Tests/Core/Cache/CachingBehaviourTests.cs` (114 lines) is over the ~100-line guide. The report already says so.
- No test checks that `CachingBehaviour<,>` is actually registered in the app's MediatR pipeline. `DashboardCachingTests` pins the options and the marker interface, and `CachingBehaviourTests` pins the behaviour, but neither proves the app wires the behaviour in. The old suite had no such check either. Worth adding to `PipelineCompositionTests` later.
- `api/core-libraries/Core.Spreadsheets/SpreadsheetOptions.cs:5`: the core default `SPREADSHEET_UNREADABLE` is the same string as the app's code. The plan and the definition-of-done grep both accept this.

## Verified
- **Intent and scope.** All six sub-tasks of story 313 are present. `Core.Notifications` and `CoreDbContext` are untouched (they are not in the diff).
- **App-agnostic core.** Running the DoD grep over the new core code prints only the accepted `SpreadsheetOptions` default. No issue-number references appear in any comment in the touched core, app or test folders.
- **Metrics are unchanged.** The names `elmanhg.requests` and `elmanhg.request.duration`, the tags `elmanhg.request` and `elmanhg.outcome`, and the meter `Elmanhg` all come from `RequestMetrics` built with ("Elmanhg", "elmanhg") (`ObservabilityExtensions.cs:41`). The old instruments are removed from `ElmanhgMetrics`, so the shared meter has no duplicate instruments. The outcome consts are unchanged (`RequestMetricsBehaviour.cs:10-12`).
- **Service name is unchanged.** The default is still `elmanhg-api`. It is applied by `.Configure` before `BindConfiguration` and in the eager read (`Core.Observability/DependencyInjection.cs:19,22`).
- **Dashboard caching is unchanged.** The cache keys are untouched. The TTL comes from `Dashboard:CacheSeconds` (Range 0-3600, default 60), and 0 still disables caching because `ttl <= TimeSpan.Zero` passes through (`CachingBehaviour.cs:16-20`).
- **Error codes are unchanged.** 429 `TOO_MANY_REQUESTS` comes through `AddCoreRateLimiting(ErrorCodes.TooManyRequests)` (`AppRateLimiting.cs:17`). 400 `SPREADSHEET_UNREADABLE` and RTL come through `AddCoreSpreadsheets` (`Infrastructure/DependencyInjection.cs:70-74`).
- **Partition key is unchanged.** The concurrency partition key format policy|userKey is the same (`StudentConcurrencyPartitions.cs:19`).
- **Redaction is unchanged.** `LogRedactor` applies email then phone, in the same order with the same pattern. `TrainingDataScrubber` keeps its own four regexes in the original order, and `TextRules` is initialised after `Number` (`TrainingDataScrubber.cs:18`).
- **Placeholder-secret guard.** `PlaceholderSecretGuard` keeps the old order: secret keys, then the Npgsql password check, then `CoreOtp:Secret` if it is missing and not already listed. The exception text is word-for-word the same, guidance `docs/security.md` included (`Core.Hosting/PlaceholderSecretGuard.cs:18`). It still runs before migrate (`Program.cs:111`).
- **Forwarded headers.** The `ForwardedHeaders` body is verbatim: XFF and XFP only, trusted CIDRs from config, and loopback still trusted by default. `ValidateOnStart` and the CIDR validator are still registered, now from `Program.cs:102` instead of `AddInfrastructure`. The only consumer is `Program.cs:163`, and `ForwardedHeadersTests` is green.
- **Middleware order.** `Program.cs` has exactly the 5 planned edits (+5/-3). No middleware line moved.
- **Packages.** No new package IDs. Core pins match the CPM versions (OpenTelemetry 1.19.1/1.19.0, ClosedXML 0.105.1, MediatR 14.1.0, Caching.Memory 10.0.5). `Microsoft.Extensions.Options` 10.0.7 and EF Relational 10.0.5 were already pinned elsewhere in core-libraries, and core-libraries has CPM off. The three new projects are in `Elmanhg.slnx` under `/core-libraries/`, and `Core.Cache/Class1.cs` is deleted.
- **Contract fidelity.** Every H, O, C, S, L, R4 and O8 file exists with the planned signature. The three listed deviations (git mv instead of delete+create, the nullable-long tuple cast, and the Split count for "exactly once") are real and do not change any behaviour.
- **Style.** File-scoped namespaces, `sealed` on classes and records, `ConfigureAwait(false)` on every await in production code, no new comments apart from the planned WHY lines, and no try/catch added. `dotnet format --verify-no-changes --include` over every changed or new .cs file exits 0.
- **Build, re-run independently.** `dotnet build api/Elmanhg.slnx -c Release` gives 0 errors and 0 warnings.
- **Tests, re-run independently.** I ran a targeted set of classes: Core.*, Api.Hosting.*, StudentConcurrencyPartitions, DashboardCaching, SpreadsheetComposition, PipelineComposition, ClientErrorsEndpoint, ForwardedHeaders, KestrelHardening, *RateLimitTests, TelemetryRegistration, LogRedactor, TrainingDataScrubber, QuestionImportEndpoint, MigrationCommand, DashboardCacheKey, ElmanhgMetrics and Integration.Dashboard.*. All 284 tests ran and passed. I did not re-run the claimed full-suite figure (5152).
- **Postman.** No endpoint changed, so the collection correctly needs no edit.
- **Docs sync.** `docs/constitution.md` (stack line, Observability rule, core-agnostic rule), `docs/security.md` sections 5 and 6, `docs/observability.md` (metric emitters, `LogRedactor` line) and SKILL.md (delta 5, the section 3 tree, section 7.4) all agree with the code. No stale references to the old type names remain in /docs.

## Test quality
- **Core/Hosting/PlaceholderSecretGuardTests:** constrain the implementation. They cover each rule path, de-duplication (exact count), the value never appearing in the message, and the Development bypass.
- **Api/Hosting/AppSecretGuardTests:** 9 moved tests with unchanged assertions. They pin the Elmanhg key list, the parsed Npgsql password and `CoreOtp:Secret`.
- **RateLimitPartitionsTests and CoreRateLimitingTests:** pin the key formats, the fixed-window QueueLimit-0 rejection, the 429 status, and that the configured code is thrown.
- **KestrelHardeningTests:** resolve real options. They pin the custom limit, the 10 MB default and the hidden server header.
- **RequestMetricsTests, RequestMetricsBehaviourTests and ObservabilityExtensionsTests:** use real MetricCollectors. A wrong prefix or meter name fails them, and the app test pins the literal elmanhg.* names.
- **ObservabilityRegistrationTests:** pin the default-then-bind order. Swapping the order would fail T-O5, and dropping the default would fail T-O4.
- **CachingBehaviourTests:** count `next` calls and cache entries. Each TTL branch would fail if inverted.
- **DashboardCachingTests:** pin the config-to-TTL mapping through the real `AddApplication()` and the marker shape of all 9 card queries plus the teacher-stats exclusion. This is structural only; see the pipeline-registration note above.
- **Spreadsheet tests:** cover the configured code, the default code, RTL on and off, and the app wiring.
- **TextRedactorTests:** cover sequential order, null, no match, and literal plus URL-encoded email.
- No vacuous mock-echo tests found.
