# Implementation: Core.Queues sweep worker base, job metrics and RetrySchedule (E21.S4)

Worktree: `D:/Personal/elmanhg-wt/307`. Nothing has been committed or pushed. Moves were done with `git mv`, and `Class1.cs` was removed with `git rm`.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/core-libraries/Core.Queues/SweepOptions.cs` | 9 | C1 sweep options (Enabled / IntervalSeconds / BatchSize / Interval) |
| `api/core-libraries/Core.Queues/BackgroundJobMetrics.cs` | 79 | C2, moved with `git mv` from Application. Takes meter name, prefix and ActivitySource; `JobTag`/`OutcomeTag` are instance properties |
| `api/core-libraries/Core.Queues/BackgroundJobRun.cs` | 49 | C3, moved verbatim; only the namespace changed |
| `api/core-libraries/Core.Queues/SweepWorker.cs` | 112 | C4 generic sweep loop. Owns the scope per listing, item and failure record, the PeriodicTimer, the kill switch, deferral and metrics |
| `api/core-libraries/Core.Queues/DependencyInjection.cs` | 16 | C5 `AddCoreBackgroundJobMetrics` |
| `api/core-libraries/Core.DDD/Models/RetrySchedule.cs` | 36 | C6 owned retry value object |
| `api/Elmanhg.Domain/SharedKernel/IRetriedWork.cs` | 8 | C7 |
| `api/Elmanhg.Application/Shared/Retries/IFailRetriedWorkCommand.cs` | 9 | C8 |
| `api/Elmanhg.Application/Shared/Retries/FailRetriedWorkHandler.cs` | 25 | C9 generic Fail handler |
| `api/Elmanhg.Tests/Api/Workers/ElmanhgJobMetrics.cs` | 13 | C10 test helper that pins `elmanhg.job` / `elmanhg.outcome` |
| `api/Elmanhg.Tests/Core/Queues/ProbeSweepWorker.cs` | 29 | C12 delegate-driven probe subclass |
| `api/Elmanhg.Tests/Core/Queues/SweepWorkerTests.cs` | 336 | C13, tests T-SW1..12 |
| `api/Elmanhg.Tests/Core/Queues/BackgroundJobMetricsTests.cs` | 153 | C14, moved with `git mv`. Holds T-BJM1..6 (probe names) plus T-BJM7 and T-BJM8 |
| `api/Elmanhg.Tests/Core/DDD/RetryScheduleTests.cs` | 102 | C15, T-RS1..8 |
| `api/Elmanhg.Tests/Core/Errors/BaseExceptionTests.cs` | 32 | C16, T-ERR1..3 |
| `api/Elmanhg.Tests/Integration/Persistence/RetrySchedulePersistenceTests.cs` | 60 | C17, T-INT1..2 (Testcontainers PostgreSQL) |

C11 (the three Fail handler rewrites) are modifications and are listed below.

## Files modified
| Path | Change |
|---|---|
| `api/core-libraries/Core.Queues/Core.Queues.csproj` | Added a FrameworkReference to AspNetCore.App, MediatR 14.1.0 (already pinned in core) and a ProjectReference to Core.Errors |
| `api/core-libraries/Core.Queues/Class1.cs` | Deleted |
| `api/core-libraries/Core.Errors/BaseException.cs` | Added `public static string ErrorCodeOf(Exception)`, copied verbatim |
| `api/Elmanhg.Api/Elmanhg.Api.csproj` | Added a reference to Core.Queues after Core.OTP |
| `api/Elmanhg.Api/Hosting/ObservabilityExtensions.cs` | Removed the private `MetricPrefix`. Now calls `AddCoreBackgroundJobMetrics(SourceName, MetricPrefix, ActivitySource)` and `AddCoreRequestMetrics(SourceName, ElmanhgTelemetry.MetricPrefix)`. Added `using Core.Queues;` |
| `api/Elmanhg.Application/Shared/Observability/ElmanhgTelemetry.cs` | Added `public const string MetricPrefix = "elmanhg";` |
| `api/Elmanhg.Application/Shared/Observability/BackgroundJobMetrics.cs`, `BackgroundJobRun.cs` | Moved to Core.Queues |
| `api/Elmanhg.Api/Workers/*.cs` (9 files) | Each is now a thin `SweepWorker<TOptions>` subclass that follows the "Worker ports" table. Constructor parameter names are unchanged. The three private `ErrorCodeOf` copies are gone |
| `api/Elmanhg.Domain/EssayGrading/EssayGrade.cs`, `.Grading.cs` | Implements `IRetriedWork`. `Retry` plus 3 pass-throughs. Added `IsPending`. `RecordSuccess`/`RecordFailure` added. The `ErrorCodeMaxLength` const is deleted. The `ToMicroseconds` method and `var at =` lines are unchanged |
| `api/Elmanhg.Domain/MathStepGrading/MathStepGrade.cs`, `.Grading.cs` | Same as EssayGrade. The exhausted branch keeps the FinalAnswerUnchecked/GradingFailed choice |
| `api/Elmanhg.Domain/TrainingExports/TrainingExport.cs`, `.Lifecycle.cs` | Same pattern. `BeginRun` now calls `Retry.Lease`. `Complete` calls `Retry.RecordSuccess()` where `Attempts++` used to be |
| `api/Elmanhg.Domain/TeacherThreads/TeacherVoiceDraft.cs` | `Retry` plus `Attempts`/`NextAttemptAt` pass-throughs. `FailAttempt` calls `Retry.RecordFailure(null, …)`. Does not implement IRetriedWork |
| `api/Elmanhg.Application/{EssayGrading/FailEssayGrade,MathStepGrading/FailMathStepGrade,TrainingExports/FailTrainingExport}/*Command.cs` | Each implements `IFailRetriedWorkCommand` with an explicit `WorkId` |
| `…/*Handler.cs` (same 3 folders) | Each subclasses `FailRetriedWorkHandler<,>`. Constructors are unchanged |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | EssayGrades and TeacherVoiceDrafts now use an `OwnsOne(Retry)` block with explicit column and index names plus `Navigation(...).IsRequired()`. The old LastErrorCode and NextAttemptAt index lines are removed. Added `using Core.DDD.Models;` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.MathStepGrading.cs`, `.TrainingExports.cs` | Same owned block, with `AiIdentifierMaxLength` / `JobErrorCodeMaxLength` |
| `api/Elmanhg.Infrastructure/{EssayGrading,MathStepGrading,TrainingExports,TeacherThreads}/*Repository.cs` | `GetDueIdsAsync` now uses `x.Retry.NextAttemptAt` |
| `api/Elmanhg.Tests/Application/Features/Shared/Observability/BackgroundJobMetricsTests.cs` | Moved to `Tests/Core/Queues` |
| `api/Elmanhg.Tests/Api/Workers/*WorkerTests.cs` (9 files) | Only the D4 edits: the construction expression and the `JobTag`/`OutcomeTag` references |
| `api/Elmanhg.Tests/Api/Hosting/ObservabilityExtensionsTests.cs` | Added T-OBS1 and `using Core.Queues;` |
| `docs/constitution.md` | Stack list, the §4 Elmanhg.Api line and the Observability line |
| `docs/observability.md` | Added "(Core.Queues)" in 6 Source cells, added `math-step-grading` to the jobs list, and added the SweepWorker sentence |
| `docs/essay-grading.md`, `docs/math-step-grading.md`, `docs/ask-teacher.md` | Retry rows now mention `RetrySchedule` |
| `docs/training-data.md` | Step 2: added the audit-diff sentence (D18) |
| `.claude/skills/dotnet-feature/SKILL.md` | Deltas item 5 "Promoted" bullet, a new §6.3 mapping row and a §8.9 "prefer SweepWorker" line |

The Postman collection is unchanged because no HTTP endpoint changed. No options class or appsettings file changed, and no migration or snapshot was touched.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| D20: core tests use the probe names `"Probe.Meter"` | T-BJM8 attaches a global `ActivityListener` to the `"Probe.Meter"` source. Test classes run in parallel, so if SweepWorkerTests started `job probe-job` spans on the same source, T-BJM8's "one activity" check could become flaky | SweepWorkerTests uses meter `"Probe.Meter"` and prefix `"probe"`, but its `ActivitySource` is named `"Probe.Sweep"`. Metrics stay isolated per `IMeterFactory` |
| T-SW11 `Stop_DuringListing_EndsTheLoopWithoutLogging`, with C12's `List` delegate carrying no `CancellationToken` | The probe cannot observe the stopping token directly | The test calls `StopAsync`, which cancels the token synchronously, and then fails the pending listing with `OperationCanceledException`. This is the same path a real cancelled `Send` takes. The C12 delegate signature is kept exactly as planned |
| §8.9: put the new line "under the ✅ DO" | §8.9's DO is a code block | Added it as a paragraph right after the code block, before "The worker is the only place…" |

## Build & test
- `dotnet build api/` gave `0 Warning(s)`, `0 Error(s)`.
- A full `dotnet build api/Elmanhg.slnx -c Release` from scratch gave 0 errors and **9 warnings**. All 9 are pre-existing nullable warnings in files this change does not touch: `Core.Notifications/Entities/Notification.cs` and `NotificationTemplate.cs`, `Core.OTP/Entities/OTP.cs`, and `Core.Validation/Extensions/RequiredValidationExtensions.cs`. None of them is in a touched project file. The DoD's "0 warnings" cannot be met without editing out-of-scope code.
- `dotnet test api/ -c Release --no-build` (Docker running, Testcontainers PostgreSQL) gave **Passed: total 5327, failed 0, succeeded 5327, skipped 0** (1m 03s).
- A targeted run (Core.Queues, RetrySchedule, BaseException, Api.Workers, Api.Hosting, the Fail handlers and the 5 aggregate test classes) gave 193 passed, 0 failed.
- `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build`, run with the CI connection-string env, printed **"No changes have been made to the model since the last migration."** No migration was added and `AppDbContextModelSnapshot.cs` is untouched. The gate passes, so this is not BLOCKED.

## Deferred
None.

## Notes for review
- **Extra scope on failure.** Workers that had no failure recorder (exam, lapse, index, retention, SLA) now open one extra async scope after a failed item, to call the default no-op `RecordFailureAsync`, exactly as the C4 contract specifies. It has no observable effect: no Send happens and nothing is logged. A short-circuit could skip the scope; I did not add one because the plan's contract does not include it.
- **Listing failure in the four retried-work workers.** Essay, Math, TrainingExport and VoiceTranscription used to `return` when listing failed. Now the base continues with an empty list and clears the deferred set. That is unobservable, because these workers ignore `deferredIds` (D7).
- **Log templates.** The base templates (D9) replace the per-worker texts. Levels are unchanged, and no log carries keys or tokens: only `JobName` and the item id. TrainingExport keeps its Information message, and SLA keeps its reschedule message and Warning/Error choice.
- **New guard.** `RetrySchedule.RecordFailure` throws `ArgumentOutOfRangeException` when `maxAttempts < 1`, which the old aggregate code did not. Every `MaxAttempts` option is `[Range(1, 10)]` and validated on start, so production cannot reach it.
- **Owned type and xmin.** The owned type shares the table with an `xmin` row-version owner. EF adds its table-sharing concurrency shadow property on the same column; the model diff is empty and `TrainingExportClaimTests` and `GradeConcurrencyTests` stay green.
- **SweepWorker.cs length.** It is 112 lines, slightly over ~100. The plan allows this and says not to split it.
- **Line endings.** New and rewritten files were written with LF. Git's autocrlf warnings ("LF will be replaced by CRLF") are expected, and the diffs touch only the intended lines.
- No code comment contains `#<number>`. The only new comments are the deferral WHY in `SweepWorker` and the column-width WHY in `RetrySchedule`.

## Rework r1

Addresses two non-blocking items from 03-review.md, at the orchestrator's request.

| Review item | What I changed | File:line |
|---|---|---|
| `SweepWorker.cs:44`: `BeforeListAsync` was not guarded, so a throwing override would end the worker loop (§8.9) | Added `TryBeforeListAsync`. It catches exceptions `when (!stoppingToken.IsCancellationRequested)`, logs Error ("The pre-listing step of job {JobName} failed."), and then lets the sweep continue to the listing. This uses the same filter and log level as the listing-failure path. The run outcome is still driven only by the listing and the items, so it matches the docs ("`Failed` when listing the work threw"). TeacherThreadSlaWorker behaves as before because its override catches its own errors. | `api/core-libraries/Core.Queues/SweepWorker.cs:44`, `:70-80` |
| Test for the guard | Added `SweepWorkerTests.Sweep_BeforeListHookThrows_LogsErrorAndKeepsTheLoopRunning`. The hook throws on every tick. The test asserts that listing still runs on two consecutive ticks, that `ExecuteTask` is not completed, and that the logged levels are `Error, Error`. | `api/Elmanhg.Tests/Core/Queues/SweepWorkerTests.cs` |
| `docs/observability.md:82`: the "failed ids are deferred until a short batch" claim was wider than the code | Reworded. The five workers whose query takes the deferred ids (`exam-auto-submit`, `subscription-lapse`, `lesson-content-index`, `ask-teacher-sla`, `training-export-retention`) skip failed ids until a short batch. `essay-grading`, `math-step-grading`, `training-export` and `teacher-voice-transcription` push `NextAttemptAt` back. I checked this mapping against each worker's `ListDueAsync`. I also noted that a failure in the pre-listing step, the listing or an item does not stop the loop. | `docs/observability.md:82` |

Build & test (r1):
- `dotnet build api/Elmanhg.slnx -c Release`: 0 Warning(s), 0 Error(s).
- `dotnet test api/Elmanhg.Tests -c Release --no-build -- --filter-namespace "Elmanhg.Tests.Core.Queues" --filter-namespace "Elmanhg.Tests.Api.Workers"`: Passed! total 83, failed 0, succeeded 83. This covers SweepWorkerTests, BackgroundJobMetricsTests and all nine worker test classes.
- I did not rerun the full suite.

Note: `SweepWorker.cs` is now about 124 lines, above the ~100-line guideline. It was already 113 lines before this rework.
