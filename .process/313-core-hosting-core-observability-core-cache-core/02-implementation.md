# Implementation — [E21.S10] Core.Hosting, Core.Observability, Core.Cache, Core.Spreadsheets, rate limits, redaction

All work is in the worktree `D:/Personal/elmanhg-wt/313`. Nothing is committed or pushed. Every file that moved went through `git mv`, so `git status` shows it as a rename (R/RM). That keeps the history from the app files.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/core-libraries/Core.Hosting/Core.Hosting.csproj` | 22 | H1. AspNetCore framework ref, EF Relational 10.0.5, Core.Errors and Core.Identity |
| `api/core-libraries/Core.Hosting/KestrelHardening.cs` | 15 | H2 (git mv from Api). `DefaultMaxRequestBodyBytes` plus an optional limit parameter |
| `api/core-libraries/Core.Hosting/MigrationCommand.cs` | 24 | H3 (git mv from Api). `MigrationCommand<TContext>`; log text is unchanged |
| `api/core-libraries/Core.Hosting/ReverseProxyOptions.cs` | 9 | H4 (git mv from Infrastructure). The Caddy mention is gone from the comment |
| `api/core-libraries/Core.Hosting/ReverseProxyOptionsValidator.cs` | 21 | H5 (git mv). Only the namespace changed |
| `api/core-libraries/Core.Hosting/ReverseProxyExtensions.cs` | 22 | H6 (git mv from Api). Body is verbatim |
| `api/core-libraries/Core.Hosting/PlaceholderSecretRules.cs` | 5 | H7. `PlaceholderValueCheck` and `PlaceholderSecretRules` |
| `api/core-libraries/Core.Hosting/PlaceholderSecretGuard.cs` | 37 | H7b. Generic guard; message text is unchanged |
| `api/core-libraries/Core.Hosting/RateLimiting/RateLimitPartitions.cs` | 28 | H9. `PerClientIp`, `PerUser`, `FixedWindow`, `UserKey` and `ClientIp`; the last four are now public |
| `api/core-libraries/Core.Hosting/DependencyInjection.cs` | 27 | H10. `AddCoreReverseProxy` and `AddCoreRateLimiting(rejectedErrorCode)` |
| `api/core-libraries/Core.Observability/Core.Observability.csproj` | 26 | O1 |
| `api/core-libraries/Core.Observability/ObservabilityOptions.cs` | 26 | O2 (git mv). `ServiceName` default is now `string.Empty` |
| `api/core-libraries/Core.Observability/ObservabilityOptionsValidator.cs` | 26 | O3 (git mv). Only the namespace changed |
| `api/core-libraries/Core.Observability/TelemetrySetup.cs` | 6 | O4 |
| `api/core-libraries/Core.Observability/TelemetryProviders.cs` | 51 | O5 |
| `api/core-libraries/Core.Observability/RequestMetrics.cs` | 31 | O6. Instrument and tag names come from the prefix |
| `api/core-libraries/Core.Observability/RequestMetricsBehaviour.cs` | 40 | O7 (git mv from Application). Now uses `RequestMetrics` |
| `api/core-libraries/Core.Observability/DependencyInjection.cs` | 38 | O7b. `AddCoreObservability` and `AddCoreRequestMetrics` |
| `api/core-libraries/Core.Cache/ICacheableQuery.cs` | 8 | C1 (git mv from `IDashboardQuery.cs`) |
| `api/core-libraries/Core.Cache/CachingOptions.cs` | 6 | C2 |
| `api/core-libraries/Core.Cache/CachingBehaviour.cs` | 31 | C3 (git mv from `DashboardCacheBehaviour.cs`) |
| `api/core-libraries/Core.Cache/DependencyInjection.cs` | 15 | C4 |
| `api/core-libraries/Core.Spreadsheets/Core.Spreadsheets.csproj` | 18 | S1 |
| `api/core-libraries/Core.Spreadsheets/{ISpreadsheetReader,ISpreadsheetWriter,SpreadsheetWorkbook,SpreadsheetSheetDefinition}.cs` | 6/6/9/5 | S2 (git mv). Only the namespace changed |
| `api/core-libraries/Core.Spreadsheets/SpreadsheetOptions.cs` | 8 | S3 |
| `api/core-libraries/Core.Spreadsheets/ClosedXmlSpreadsheetReader.cs` | 93 | S4 (git mv). The error code comes from options |
| `api/core-libraries/Core.Spreadsheets/ClosedXmlSpreadsheetWriter.cs` | 52 | S5 (git mv). `RightToLeft` comes from options |
| `api/core-libraries/Core.Spreadsheets/DependencyInjection.cs` | 14 | S6 |
| `api/core-libraries/Core.Logging/TextRedactor.cs` | 12 | L1 |
| `api/core-libraries/Core.Logging/RedactionPatterns.cs` | 9 | L2. Email pattern copied character for character |
| `api/Elmanhg.Api/Hosting/AppSecretGuard.cs` | 20 | H8 (git mv from `PlaceholderSecretGuard.cs`) |
| `api/Elmanhg.Api/RateLimiting/StudentConcurrencyPartitions.cs` | 25 | R4 (git mv from `RateLimitPartitions.cs`) |
| Tests (new): `Core/Hosting/{PlaceholderSecretGuardTests,RateLimitPartitionsTests,CoreRateLimitingTests,KestrelHardeningTests}.cs`, `Core/Observability/{RequestMetricsTests,ObservabilityRegistrationTests}.cs`, `Core/Cache/CachingBehaviourTests.cs`, `Core/Logging/TextRedactorTests.cs`, `Api/Hosting/ObservabilityExtensionsTests.cs`, `Integration/Composition/SpreadsheetCompositionTests.cs` | 95/71/38/35, 35/57, 114, 48, 51, 19 | T-H1..T-H10, T-O1/1b, T-O4..T-O8, T-C1*, T-L1..T-L4, T-S3 |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.slnx` | Added Core.Hosting, Core.Observability and Core.Spreadsheets in alphabetical order |
| `api/core-libraries/Core.Cache/Core.Cache.csproj` | Added MediatR 14.1.0 and Caching.Memory 10.0.5. `Class1.cs` is deleted (`git rm`) |
| `api/Elmanhg.Api/Elmanhg.Api.csproj` | Removed the 4 OpenTelemetry PackageReferences; added ProjectRefs to Core.Hosting and Core.Observability |
| `api/Elmanhg.Application/Elmanhg.Application.csproj` | Added ProjectRefs to Core.Cache, Core.Logging and Core.Spreadsheets |
| `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` | Removed ClosedXML; added a ProjectRef to Core.Spreadsheets |
| `api/Elmanhg.Api/Program.cs` | Exactly the 5 listed edits (diff: +5/-3). The middleware order is unchanged |
| `api/Elmanhg.Api/Hosting/ObservabilityExtensions.cs` | Rewritten per O8. The signature is unchanged |
| `api/Elmanhg.Api/RateLimiting/AppRateLimiting.cs` | Calls `AddCoreRateLimiting(ErrorCodes.TooManyRequests)`, uses `StudentConcurrencyPartitions` and the new usings |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | Removed the ReverseProxy registration; added an `AddCoreSpreadsheets` block that sets the app error code and RTL; usings updated |
| `api/Elmanhg.Application/DependencyInjection.cs` | Calls `AddCoreCache()`. `CachingOptions.DefaultTtl` comes from `DashboardOptions.CacheSeconds`, configured right after the Dashboard options registration |
| `api/Elmanhg.Application/Dashboard/Shared/IDashboardRangeQuery.cs`, `Dashboard/GetContentMetrics/GetContentMetricsQuery.cs` | Now implement `ICacheableQuery` |
| `api/Elmanhg.Application/Shared/Observability/ElmanhgMetrics.cs` | Removed the request instruments, `RecordRequest`, `RequestTag` and `SuccessOutcome` |
| `api/Elmanhg.Application/Shared/Observability/LogRedactor.cs` | Rewritten per L3. Public API is unchanged |
| `api/Elmanhg.Application/TrainingExports/Shared/TrainingDataScrubber.cs` | `ScrubText` now goes through a `TextRedactor` built from its own 4 regexes, in the same order |
| 7 Application Questions files + 5 Questions test files | `using Elmanhg.Application.Shared.Spreadsheets;` → `using Core.Spreadsheets;` (using block kept sorted) |
| `Tests/Integration/Hosting/MigrationCommandTests.cs` | Now uses `Core.Hosting` and `MigrationCommand<AppDbContext>` (3 call sites) |
| `Tests/Integration/Composition/PipelineCompositionTests.cs` | Added `using Core.Observability;` |
| `Tests/Integration/Observability/ClientErrorsEndpointTests.cs` | `ElmanhgMetrics.RequestTag` → the literal `"elmanhg.request"` |
| `Tests/Application/Features/Shared/Observability/ElmanhgMetricsTests.cs` | Removed `RecordRequest_Success_…`, which moved to core `RequestMetricsTests` |
| Moved tests (git mv) | `RequestMetricsBehaviourTests` and `ObservabilityOptionsValidatorTests` → `Core/Observability`; `ReverseProxyOptionsValidatorTests` → `Core/Hosting`; spreadsheet reader and writer tests → `Core/Spreadsheets`; `PlaceholderSecretGuardTests` → `AppSecretGuardTests`; `RateLimitPartitionsTests` → `StudentConcurrencyPartitionsTests` (6 tests kept); `DashboardCacheBehaviourTests` → `DashboardCachingTests` (rewritten per T-C2) |
| `docs/constitution.md` | Stack line, the Observability rule, and a new core-stays-app-agnostic rule |
| `docs/security.md` | Lines 123 and 145 |
| `docs/observability.md` | Metric table emitters (lines 59–60) and the `LogRedactor` description |
| `.claude/skills/dotnet-feature/SKILL.md` | Delta 5 bullet, the §3 tree (also fixes the duplicated `Core.Notifications · Core.OTP` row) and §7.4 |

No endpoint changed, so `postman/elmanhg.postman_collection.json` is untouched. No error code was added, so the resx files are untouched.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| "**Delete** (moved to core)" for the app-side Kestrel/Migration/ReverseProxy/Observability-options/RequestMetricsBehaviour/spreadsheet/DashboardCacheBehaviour/IDashboardQuery files, with the core files listed as "create" | The caller asked for `git mv` wherever a file moves, so history is kept | I `git mv`'d each app file to its core path and edited it there. The resulting tree matches the plan exactly. Git records these as renames instead of a delete plus an add. |
| T-H9: `(Limits.MaxRequestBodySize, AddServerHeader) == (4096L, false)` | `MaxRequestBodySize` is `long?`. A boxed `ValueTuple<long?,bool>` never equals a `ValueTuple<long,bool>`, so the literal assertion could never pass | Asserted against `((long?)4096L, false)`. The checked value is the same. |
| T-H4b: "message contains `Sample:Required` exactly once" | No FluentAssertions helper does an exact-count substring check in one call | Asserted `Message.Split("Sample:Required")` has 2 parts, which means exactly one occurrence. |

There are no other deviations. Signatures, names, test classes and test method names follow the plan.

## Build & test
- `dotnet build api/Elmanhg.slnx` (Debug) → `0 Error(s)`, `0 Warning(s)` (incremental).
- `dotnet build api/ -c Release --no-incremental` → `0 Error(s)`. The new projects and the Elmanhg projects raise no warnings. The only warnings are the existing CS8618/CS8602 ones in vendored Core.Notifications, Core.OTP and Core.Validation, which are not held to TreatWarningsAsErrors.
- `dotnet test Elmanhg.slnx --no-build` (Debug, Docker 29.6.2 running for Testcontainers) → `Test run summary: Passed! total: 5152 failed: 0 succeeded: 5152 skipped: 0`.
- Targeted Release run of every new, moved and touched class (Core.Hosting.*, Core.Observability.*, Core.Cache.*, TextRedactorTests, Core.Spreadsheets.*, Api.Hosting.*, StudentConcurrencyPartitionsTests, DashboardCachingTests, SpreadsheetCompositionTests, MigrationCommandTests, LogRedactorTests, TrainingDataScrubberTests) → `total: 126 failed: 0 succeeded: 126`.
- EF check, run the way CI runs it: `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build` → `No changes have been made to the model since the last migration.`
- OpenAPI drift after the Release build: `git status --porcelain api/openapi` is empty.
- `dotnet format api/Elmanhg.slnx --verify-no-changes` reports 100 existing WHITESPACE findings. All of them are in files this story does not touch (vendored Core.Notifications, Core.OTP, Core.Utilities and Core.Validation, plus `Tests/Builders/SubscriptionBuilder.cs`). None is in a file created or modified here. CI does not run `dotnet format`.
- Definition-of-done grep (`elmanhg|egypt|change-me|SPREADSHEET_UNREADABLE|TOO_MANY_REQUESTS|Npgsql|caddy` over the new core code) prints only `SpreadsheetOptions.UnreadableErrorCode = "SPREADSHEET_UNREADABLE"`, which the plan expects.

## Deferred
None.

## Notes for review
- `CachingBehaviourTests.cs` has 114 lines. It mirrors the old 105-line `DashboardCacheBehaviourTests`, plus 2 new TTL cases. `ClosedXmlSpreadsheetReaderTests.cs` grew from 127 to 138 lines with the plan's extra T-S1b case. Both were already over the ~100-line guide before this story; production files are all under 100.
- `ObservabilityOptions.ServiceName` keeps the value `elmanhg-api` for hosts without the key. `TelemetrySetup.DefaultServiceName` is applied both through `.Configure(...)` before `BindConfiguration` and in the eager read; T-O4, T-O5 and T-O8 pin this.
- Pipeline order is unchanged: `AddCoreRequestMetrics` registers `RequestMetricsBehaviour<,>` at the same spot as before (after ElmanhgMetrics/BackgroundJobMetrics, before the Quiz/Payment closed behaviours), and `PipelineCompositionTests` still asserts index 1.
- `AddCoreCache()` calls `AddMemoryCache()`. The Application DI keeps its own `services.AddMemoryCache();` call as the plan says, which is idempotent.
- `CachingOptions` is configured from `IOptions<DashboardOptions>`, so the first resolve of `CachingOptions` also validates `DashboardOptions`. That already happens at startup through `ValidateOnStart`.
- The new and edited files were written with LF endings. With `core.autocrlf=true`, git normalises them on commit (it prints "LF will be replaced by CRLF").
- Every test secret in the new tests uses `not-a-secret-…`. No `#<number>` appears in any code or test comment.
