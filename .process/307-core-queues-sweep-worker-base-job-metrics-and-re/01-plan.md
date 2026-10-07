# Plan — Core.Queues: sweep worker base, job metrics and RetrySchedule (E21.S4)

## Goal
Engineers get one tested background-sweep pattern in `Core.Queues` (`SweepWorker<TOptions>`, `SweepOptions`, `BackgroundJobMetrics`/`BackgroundJobRun`), one `BaseException.ErrorCodeOf` helper, and one retry value object `Core.DDD.Models.RetrySchedule`. All 9 Elmanhg workers become thin subclasses. The 4 retried aggregates (`EssayGrade`, `MathStepGrade`, `TrainingExport`, `TeacherVoiceDraft`) hold their retry state in `RetrySchedule`. The three identical Fail handlers share one generic base. Runtime behaviour, config keys, `elmanhg.job.*` metric names and the database schema stay exactly as they are.

## Scope
**In:** sub-task 1 (core types + tests), sub-task 2 (port all 9 workers), sub-task 3 (RetrySchedule in 4 aggregates, no migration, tests), sub-task 4 (collapse identical Fail handlers), sub-task 5 (docs).
**Out:** `Core.Notifications` and `CoreDbContext` stay untouched (dev decision 2026-10-06). The private `ToMicroseconds` copies in the aggregates are not changed (lane 306). `AppDbContext.SaveChangesAsync`, the conflict map and every `.IsRowVersion()` line are not changed (lane 309). `RequestMetricsBehaviour.OutcomeOf` stays as it is: its fallback is `UNHANDLED_EXCEPTION`, not the type name, so it is not a copy. Apply handlers stay separate (see D11).
**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Where do job metrics live: Core.Observability or Core.Queues? | `Core.Queues` (namespace `Core.Queues`). `Core.Observability` is unchanged. | The story title puts them in Core.Queues. `SweepWorker` consumes them, so Core.Queues does not need to pull in OpenTelemetry. The type only uses BCL `System.Diagnostics(.Metrics)`. The app keeps exporting meter `Elmanhg` through the existing `TelemetrySetup`. |
| D2 | How are the meter name and instrument prefix passed? | Constructor `BackgroundJobMetrics(IMeterFactory meterFactory, TimeProvider timeProvider, string meterName, string metricPrefix, ActivitySource activitySource)`. Instruments are `{prefix}.job.runs/duration/items/last_success/interval`. Tags are the instance properties `JobTag = "{prefix}.job"` and `OutcomeTag = "{prefix}.outcome"`. The span is named `job {jobName}` and comes from the given `ActivitySource`. | Same shape as `Core.Observability.RequestMetrics(meterFactory, meterName, metricPrefix)`. The app passes `ElmanhgTelemetry.SourceName`, `"elmanhg"` and `ElmanhgTelemetry.ActivitySource`, which gives byte-identical names. |
| D3 | Where does the `"elmanhg"` prefix live? | New `public const string MetricPrefix = "elmanhg";` on `ElmanhgTelemetry`. The private `MetricPrefix` const in `ObservabilityExtensions` is removed, and both `AddCoreRequestMetrics` and `AddCoreBackgroundJobMetrics` use the new const. | One source for the dashboard contract. |
| D4 | Old app tests construct `new BackgroundJobMetrics(_meterFactory, _time)` and use `BackgroundJobMetrics.JobTag`. | Add a test helper `Elmanhg.Tests.Api.Workers.ElmanhgJobMetrics` with `Create(meterFactory, timeProvider)` and literal consts `JobTag = "elmanhg.job"` and `OutcomeTag = "elmanhg.outcome"`. In the 9 worker test files, change only the construction expression and the two const references. No assertion changes. | Pins the dashboard names as literals. The edits are mechanical and do not weaken any test. |
| D5 | Shape of `SweepOptions` when existing keys are prefixed (`SweepEnabled`, `LapseSweepEnabled`, …) and two option classes host two sweeps each (`AskTeacherOptions`, `TrainingExportsOptions`). | `public class SweepOptions { bool Enabled = true; int IntervalSeconds = 60; int BatchSize = 50; TimeSpan Interval }`. It is non-sealed, so a future app can bind a section to a subclass directly. `SweepWorker<TOptions>` has `protected abstract SweepOptions SweepOptionsOf(TOptions options)`, and each Elmanhg worker maps its own prefixed keys. No app options class changes. | This is the only way to keep every existing key, `[Range]` and `ValidateOnStart` unchanged with two sweeps per options class. |
| D6 | What do the abstract members receive? | The base creates the scope and passes the scoped `ISender`: `ListDueAsync(ISender, IReadOnlyCollection<Guid> deferredIds, ct)`, `ProcessAsync(ISender, Guid id, ct)` and optional `RecordFailureAsync(ISender, Guid id, string errorCode, ct)`. Core.Queues references MediatR 14.1.0 (already pinned in Core.CQRS/Core.Observability, so not a new package). | Keeps "a scope per list and per item" (skill §8.9) in one place, with no service locator in subclasses. Essay and Math send 2–3 commands in one item scope, exactly as today. |
| D7 | Deferral for workers whose queries take no excluded ids (essay, math, training export, voice transcription). | The base always keeps the deferred set: a failed id is added, and the set is cleared when a listing (or a failed listing) returns fewer than `BatchSize` ids. Those four workers ignore `deferredIds`. `BatchSize` maps to their `SweepBatchSize`/`TranscriptionSweepBatchSize`, the same limit their due-query uses, so a short batch still clears the set. | Same observable behaviour: their queries never read the set. Listing failure gives zero items in both old code paths (`return` and `?? []`). |
| D8 | Teacher SLA sweep reschedules before it lists. | `protected virtual Task BeforeListAsync(SweepOptions sweep, CancellationToken)` (default `Task.CompletedTask`) runs inside the run, before listing. The base exposes `protected IServiceScopeFactory ScopeFactory` and `protected ILogger Logger`. `TeacherThreadSlaWorker` moves its `RescheduleAsync` body into the override unchanged: a scope per batch, and a Warning on `ConflictCoreException`, Error otherwise. | Keeps the reschedule loop's scope-per-batch and log levels. Subclasses pass primary-ctor parameters only to `base(...)` (no capture, so no CS9107). |
| D9 | Log messages | Base templates: listing `LogError(e, "Listing due items of job {JobName} failed.", JobName)`, item `LogWarning(e, "Job {JobName} failed on item {ItemId}.", JobName, id)`, recording `LogError(e, "Recording the failure of item {ItemId} in job {JobName} failed.", id, JobName)`. TrainingExport keeps `LogInformation("Training export {TrainingExportId} was claimed by another run.", id)`, and SLA keeps its reschedule message. | Levels are identical, which is all the tests assert. Templates become uniform. |
| D10 | `ErrorCodeOf` home | `public static string ErrorCodeOf(Exception exception)` on `Core.Errors.BaseException`, body copied verbatim from the workers. `SweepWorker` calls it. The 3 private copies are deleted. | The exception type owns its own projection. Core.Queues already references Core.Errors. |
| D11 | Which handlers collapse? | `FailEssayGradeHandler`, `FailMathStepGradeHandler` and `FailTrainingExportHandler` (identical bodies) derive from the new generic `FailRetriedWorkHandler<TCommand, TWork>` and only override `MaxAttempts` and `RetryBaseDelay`. `FailVoiceDraftTranscriptionHandler` stays: its command carries no error code and its `FailAttempt` has a different signature. `ApplyEssayGradeHandler` and `ApplyMathStepGradeHandler` stay: they call different recorders (`EssayAttemptRecorder` vs `MathStepAttemptRecorder`). The `GetDue*` handlers stay: each has its own repository and option key. | "Where identical" only. Constructors and command records keep their shape, so all existing handler tests stay green unedited. |
| D12 | How does the generic handler read the id and status? | New domain interface `Elmanhg.Domain.SharedKernel.IRetriedWork { bool IsPending; void FailAttempt(string errorCode, DateTimeOffset failedAt, int maxAttempts, TimeSpan retryBaseDelay); }`, implemented by the 3 aggregates. New `Elmanhg.Application.Shared.Retries.IFailRetriedWorkCommand : IRequest { Guid WorkId; string ErrorCode; }`, implemented by the 3 commands with an explicit `Guid IFailRetriedWorkCommand.WorkId => <ExistingId>;`. | Smallest contract that lets one body serve three types. Terminal transitions stay in each aggregate's `FailAttempt` (story rule). |
| D13 | `RetrySchedule` shape | `public sealed class RetrySchedule` in `Core.DDD.Models` (beside `LocalizedText`), mutable in place through methods, with private setters and a private parameterless ctor. It is an EF owned type. | In-place mutation of an owned reference is the reliable EF change-tracking path; replacing an owned instance is not. `RecordFailure` returns `exhausted` as the story asks. |
| D14 | Error-code truncation length | `public const int ErrorCodeMaxLength = 100;` on `RetrySchedule`, with WHY comment "Owners map LastErrorCode to a 100-character column." The aggregates' private `ErrorCodeMaxLength` consts are deleted. EF mappings keep using `AiIdentifierMaxLength`/`JobErrorCodeMaxLength` (both 100). | Same truncation, same column width, and no unused consts. |
| D15 | Keep `Attempts`/`NextAttemptAt`/`LastErrorCode` on the aggregates? | Yes, as read-only pass-throughs: `public int Attempts => Retry.Attempts;` and so on (voice draft: `Attempts` and `NextAttemptAt` only). EF does not map get-only properties without a backing field. Every LINQ-to-SQL use (the 4 `GetDueIdsAsync`) switches to `x.Retry.NextAttemptAt`. | Result generators, ~15 test files and admin results keep compiling unchanged. Only translated queries need the owned path. |
| D16 | Owned mapping onto existing columns | In each aggregate's existing config method: `builder.OwnsOne(x => x.Retry, retry => { HasColumnName(nameof(...)) per property; HasMaxLength on LastErrorCode; HasIndex(NextAttemptAt).HasFilter("\"Status\" = 'Pending'").HasDatabaseName("IX_<Table>_NextAttemptAt"); });` plus `builder.Navigation(x => x.Retry).IsRequired();`. The old `LastErrorCode` property line and the old `HasIndex(x => x.NextAttemptAt)` line are removed. Voice draft: `retry.Ignore(x => x.LastErrorCode);`. No migration, and the snapshot is not edited. | Explicit column and index names keep `has-pending-model-changes` empty. `IsRequired` keeps `Attempts` NOT NULL. The voice-draft table has no `LastErrorCode` column. |
| D17 | Voice draft error code | `TeacherVoiceDraft.FailAttempt` keeps its signature and calls `Retry.RecordFailure(null, at, maxAttempts, retryBaseDelay)`. | No column, no command field, no behaviour change. |
| D18 | Audit diff side-effect | `TrainingExport` is `IAuditedEntity`. `AuditChangeReader` reads only `IAuditedEntity` entries, so the owned `RetrySchedule` entry's columns (`Attempts`, `NextAttemptAt`, `LastErrorCode`) no longer appear in the `TrainingExport.Request` "Created" audit diff. No other audited command touches retry state. Accepted and recorded in `docs/training-data.md`. | This follows from the owned value object the story mandates. Changing `Core.EntityFrameworkCore` auditing is out of scope, and no test or doc asserts those keys. |
| D19 | `SweepWorker.ExecuteAsync` overridable? | `protected sealed override`. Subclasses only provide `JobName`, `SweepOptionsOf`, `ListDueAsync`, `ProcessAsync` and the optional hooks. | Keeps every worker on one loop. |
| D20 | Core test location | `Elmanhg.Tests/Core/Queues/*`, `Elmanhg.Tests/Core/DDD/RetryScheduleTests.cs`, `Elmanhg.Tests/Core/Errors/BaseExceptionTests.cs`. Core tests use probe names (`"Probe.Meter"`, prefix `"probe"`, job `"probe-job"`), mirroring `RequestMetricsTests`. | E21 rule: moved code keeps its tests next to core coverage. Core stays app-agnostic. |

## Existing code touched
| File | Change |
|------|--------|
| `api/core-libraries/Core.Queues/Core.Queues.csproj` | Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, `<PackageReference Include="MediatR" Version="14.1.0" />`, `<ProjectReference Include="..\Core.Errors\Core.Errors.csproj" />` (same style as `Core.Observability.csproj`). |
| `api/core-libraries/Core.Queues/Class1.cs` | **Delete.** |
| `api/core-libraries/Core.Errors/BaseException.cs` | Add `public static string ErrorCodeOf(Exception exception) => exception is BaseException { ErrorCode: { Length: > 0 } code } ? code : exception.GetType().Name;` inside the class body. |
| `api/Elmanhg.Api/Elmanhg.Api.csproj` | Add `<ProjectReference Include="..\core-libraries\Core.Queues\Core.Queues.csproj" />` (alphabetical, after Core.OTP). |
| `api/Elmanhg.Api/Hosting/ObservabilityExtensions.cs` | Remove `private const string MetricPrefix`. Replace `services.AddSingleton<BackgroundJobMetrics>();` with `services.AddCoreBackgroundJobMetrics(ElmanhgTelemetry.SourceName, ElmanhgTelemetry.MetricPrefix, ElmanhgTelemetry.ActivitySource);`. `AddCoreRequestMetrics(ElmanhgTelemetry.SourceName, ElmanhgTelemetry.MetricPrefix)`. Add `using Core.Queues;`. |
| `api/Elmanhg.Application/Shared/Observability/ElmanhgTelemetry.cs` | Add `public const string MetricPrefix = "elmanhg";`. |
| `api/Elmanhg.Application/Shared/Observability/BackgroundJobMetrics.cs` | **Delete** (moved to core). |
| `api/Elmanhg.Application/Shared/Observability/BackgroundJobRun.cs` | **Delete** (moved to core). |
| `api/Elmanhg.Api/Workers/*.cs` (all 9) | Rewrite as `SweepWorker<TOptions>` subclasses (table "Worker ports"). |
| `api/Elmanhg.Domain/EssayGrading/EssayGrade.cs` | Class declaration `: AuditEntity, IRetriedWork`. Delete `ErrorCodeMaxLength` const and its comment. Replace the 3 auto-properties with `public RetrySchedule Retry { get; private set; } = default!;` plus pass-throughs `Attempts`, `NextAttemptAt`, `LastErrorCode`. In `Request`, replace `Attempts = 0, NextAttemptAt = at,` with `Retry = RetrySchedule.DueAt(at),`. `IsDueAt` becomes `Status == EssayGradeStatus.Pending && Retry.IsDueAt(now)`. Add `public bool IsPending => Status == EssayGradeStatus.Pending;`. Do **not** touch `ToMicroseconds`. |
| `api/Elmanhg.Domain/EssayGrading/EssayGrade.Grading.cs` | `Complete`: replace the 3 lines `Attempts++; NextAttemptAt = null; LastErrorCode = null;` with `Retry.RecordSuccess();`. `FailAttempt`: body per Domain behaviour. Keep each `var at = ToMicroseconds(...)` line byte-identical. |
| `api/Elmanhg.Domain/MathStepGrading/MathStepGrade.cs` | Same as EssayGrade.cs (status `MathStepGradeStatus.Pending`). |
| `api/Elmanhg.Domain/MathStepGrading/MathStepGrade.Grading.cs` | Same as EssayGrade.Grading.cs (exhausted branch keeps the `FinalAnswerVerdict is null ? FinalAnswerUnchecked : GradingFailed` choice). |
| `api/Elmanhg.Domain/TrainingExports/TrainingExport.cs` | Declaration `: AuditEntity, IAuditedEntity, IRetriedWork`. Delete the `ErrorCodeMaxLength` const and its comment. `Retry` plus 3 pass-throughs. `Request` uses `Retry = RetrySchedule.DueAt(at),`. `IsDueAt` uses `Retry.IsDueAt(now)`. Add `IsPending`. |
| `api/Elmanhg.Domain/TrainingExports/TrainingExport.Lifecycle.cs` | `BeginRun`: `NextAttemptAt = at + lease;` becomes `Retry.Lease(at, lease);`. `Complete`: `Attempts++`, `NextAttemptAt = null` and `LastErrorCode = null` are replaced by one `Retry.RecordSuccess();` placed where `Attempts++` was. `FailAttempt` per Domain behaviour. |
| `api/Elmanhg.Domain/TeacherThreads/TeacherVoiceDraft.cs` | `Retry` plus pass-throughs `Attempts` and `NextAttemptAt`. `Record` uses `Retry = RetrySchedule.DueAt(at),`. `IsDueAt` uses `Retry.IsDueAt(now)`. `CompleteTranscription`: `Attempts++; NextAttemptAt = null;` becomes `Retry.RecordSuccess();`. `FailAttempt` per Domain behaviour. Does **not** implement `IRetriedWork`. |
| `api/Elmanhg.Application/EssayGrading/FailEssayGrade/FailEssayGradeCommand.cs` | `: IFailRetriedWorkCommand { Guid IFailRetriedWorkCommand.WorkId => EssayGradeId; }` (replaces `: IRequest`). |
| `api/Elmanhg.Application/EssayGrading/FailEssayGrade/FailEssayGradeHandler.cs` | Becomes a subclass (see Files to create, contract C11). |
| `api/Elmanhg.Application/MathStepGrading/FailMathStepGrade/FailMathStepGradeCommand.cs` | `: IFailRetriedWorkCommand { Guid IFailRetriedWorkCommand.WorkId => MathStepGradeId; }` |
| `api/Elmanhg.Application/MathStepGrading/FailMathStepGrade/FailMathStepGradeHandler.cs` | Subclass (C11). |
| `api/Elmanhg.Application/TrainingExports/FailTrainingExport/FailTrainingExportCommand.cs` | `: IFailRetriedWorkCommand { Guid IFailRetriedWorkCommand.WorkId => ExportId; }` |
| `api/Elmanhg.Application/TrainingExports/FailTrainingExport/FailTrainingExportHandler.cs` | Subclass (C11). |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `ConfigureEssayGrades`: remove the `builder.Property(x => x.LastErrorCode)…` line and the `builder.HasIndex(x => x.NextAttemptAt)…` line, and insert the owned block (D16, table EssayGrades) at the old `LastErrorCode` line position. `ConfigureTeacherVoiceDrafts`: remove the `HasIndex(x => x.NextAttemptAt)` line and add the owned block (TeacherVoiceDrafts, with `Ignore(LastErrorCode)`). Add `using Core.DDD.Models;`. Nothing else in the file. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.MathStepGrading.cs` | Same as Essay (table MathStepGrades). |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.TrainingExports.cs` | Same (table TrainingExports, `HasMaxLength(JobErrorCodeMaxLength)`). |
| `api/Elmanhg.Infrastructure/EssayGrading/EssayGradeRepository.cs` | `GetDueIdsAsync`: `x.NextAttemptAt` becomes `x.Retry.NextAttemptAt` (in `Where` and `ThenBy`). |
| `api/Elmanhg.Infrastructure/MathStepGrading/MathStepGradeRepository.cs` | Same. |
| `api/Elmanhg.Infrastructure/TrainingExports/TrainingExportRepository.cs` | Same in `GetDueIdsAsync` (`Where`, `OrderBy`). |
| `api/Elmanhg.Infrastructure/TeacherThreads/TeacherVoiceDraftRepository.cs` | Same in `GetDueIdsAsync`. |
| `api/Elmanhg.Tests/Application/Features/Shared/Observability/BackgroundJobMetricsTests.cs` | **Delete** (moved to `Tests/Core/Queues/BackgroundJobMetricsTests.cs`). |
| `api/Elmanhg.Tests/Api/Workers/{EssayGrading,ExpiredExamSubmission,LessonContentIndex,MathStepGrading,SubscriptionLapse,TeacherThreadSla,TeacherVoiceTranscription,TrainingExportRetention,TrainingExport}WorkerTests.cs` | Only: `new BackgroundJobMetrics(_meterFactory, _time)` becomes `ElmanhgJobMetrics.Create(_meterFactory, _time)`; `BackgroundJobMetrics.JobTag` becomes `ElmanhgJobMetrics.JobTag`; `BackgroundJobMetrics.OutcomeTag` becomes `ElmanhgJobMetrics.OutcomeTag`. No other line changes. |
| `api/Elmanhg.Tests/Api/Hosting/ObservabilityExtensionsTests.cs` | Add 1 test (T-OBS1) and `using Core.Queues;`. |
| `docs/constitution.md` | Line 3 stack list: add to the Elmanhg-promoted list "`Core.Queues` (`SweepWorker<TOptions>` sweep base with `SweepOptions`, `BackgroundJobMetrics`)" and "`Core.DDD` `RetrySchedule` (owned retry value object)". §Observability line (147): "business metrics go through `ElmanhgMetrics`, `Core.Queues` `BackgroundJobMetrics` (configured with the `Elmanhg` meter and `elmanhg` prefix), `Core.Observability` `RequestMetrics`…". Add one line under the `Elmanhg.Api`/layers list: "Background sweeps derive from `Core.Queues.SweepWorker<TOptions>`; retried aggregates keep their retry state in `RetrySchedule`." |
| `docs/observability.md` | §3 traces row and §4 rows 66–70: Source column `BackgroundJobMetrics` becomes `` `BackgroundJobMetrics` (Core.Queues) ``. Background-jobs list (line 82): add `math-step-grading` (registered by `MathStepGradingWorker` today but missing from the doc). Add the sentence "Every worker is a `Core.Queues` `SweepWorker`: a scope per listing and per item, and failed ids are deferred until a short batch." |
| `docs/essay-grading.md` | Row line 23: "the retry schedule (`RetrySchedule`, Core.DDD, mapped onto these columns)". |
| `docs/math-step-grading.md` | Row line 60: same wording. |
| `docs/ask-teacher.md` | Row line 61: "Retry bookkeeping (`RetrySchedule`, Core.DDD; the error code is not stored)…". |
| `docs/training-data.md` | After §97 step 2, add one sentence: "The retry state (`Attempts`, `NextAttemptAt`, `LastErrorCode`) is a `RetrySchedule` owned value, so it is not part of the `TrainingExport.Request` audit diff." |
| `.claude/skills/dotnet-feature/SKILL.md` | Deltas item 5 "Promoted in Elmanhg" bullet: add "`Core.Queues` (`SweepWorker<TOptions>`, `SweepOptions`, `BackgroundJobMetrics`), `Core.DDD` `RetrySchedule`, `Core.Errors` `BaseException.ErrorCodeOf`", plus "write a background sweep as a `SweepWorker<TOptions>` subclass; keep retry state in a `RetrySchedule` owned value". §6.3 mapping table: add row `` `OwnsOne(x => x.Retry, …)` + `Navigation(x => x.Retry).IsRequired()` `` / "a `RetrySchedule` (explicit `HasColumnName`, index names kept)". §8.9: under the ✅ DO, add "Prefer deriving from `Core.Queues.SweepWorker<TOptions>`, which owns the scope per listing/item, the `PeriodicTimer` on `TimeProvider`, the kill switch, deferral and job metrics." |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| C1 | `api/core-libraries/Core.Queues/SweepOptions.cs` | class | `namespace Core.Queues; public class SweepOptions { public bool Enabled { get; set; } = true; public int IntervalSeconds { get; set; } = 60; public int BatchSize { get; set; } = 50; public TimeSpan Interval => TimeSpan.FromSeconds(IntervalSeconds); }` |
| C2 | `api/core-libraries/Core.Queues/BackgroundJobMetrics.cs` | sealed class | Moved body of the app class, with these changes. Ctor `(IMeterFactory meterFactory, TimeProvider timeProvider, string meterName, string metricPrefix, ActivitySource activitySource)`. Sets `JobTag = $"{metricPrefix}.job"`, `OutcomeTag = $"{metricPrefix}.outcome"` and `meter = meterFactory.Create(meterName)`. Instruments `$"{metricPrefix}.job.runs"` (`"{run}"`), `.job.duration` (`"s"`), `.job.items` (`"{item}"`), `.job.last_success` (gauge long, `"s"`), `.job.interval` (gauge double, `"s"`), descriptions verbatim. `public string JobTag { get; }`, `public string OutcomeTag { get; }`. `public void Register(string jobName, TimeSpan interval)`. `public BackgroundJobRun StartRun(string jobName)` uses `_activitySource.StartActivity($"job {jobName}")`. `internal void Complete(BackgroundJobRun run)`. `RecordItems` and the private `JobState` are unchanged; every `JobTag`/`OutcomeTag` use reads the instance properties. |
| C3 | `api/core-libraries/Core.Queues/BackgroundJobRun.cs` | sealed class + enum | Verbatim move, namespace `Core.Queues`; `public enum BackgroundJobRunOutcome { Succeeded, PartiallyFailed, Failed }` stays in the same file. |
| C4 | `api/core-libraries/Core.Queues/SweepWorker.cs` | abstract class | See SweepWorker contract below. |
| C5 | `api/core-libraries/Core.Queues/DependencyInjection.cs` | static class | `public static IServiceCollection AddCoreBackgroundJobMetrics(this IServiceCollection services, string meterName, string metricPrefix, ActivitySource activitySource)`: `services.TryAddSingleton(TimeProvider.System); services.AddSingleton(provider => new BackgroundJobMetrics(provider.GetRequiredService<IMeterFactory>(), provider.GetRequiredService<TimeProvider>(), meterName, metricPrefix, activitySource)); return services;` |
| C6 | `api/core-libraries/Core.DDD/Models/RetrySchedule.cs` | sealed class | See Domain behaviour. |
| C7 | `api/Elmanhg.Domain/SharedKernel/IRetriedWork.cs` | interface | `namespace Elmanhg.Domain.SharedKernel; public interface IRetriedWork { bool IsPending { get; } void FailAttempt(string errorCode, DateTimeOffset failedAt, int maxAttempts, TimeSpan retryBaseDelay); }` |
| C8 | `api/Elmanhg.Application/Shared/Retries/IFailRetriedWorkCommand.cs` | interface | `namespace Elmanhg.Application.Shared.Retries; public interface IFailRetriedWorkCommand : IRequest { Guid WorkId { get; } string ErrorCode { get; } }` |
| C9 | `api/Elmanhg.Application/Shared/Retries/FailRetriedWorkHandler.cs` | abstract class | See C9 code below. |
| C10 | `api/Elmanhg.Tests/Api/Workers/ElmanhgJobMetrics.cs` | internal static class | `public const string JobTag = "elmanhg.job"; public const string OutcomeTag = "elmanhg.outcome"; public static BackgroundJobMetrics Create(IMeterFactory meterFactory, TimeProvider timeProvider) => new(meterFactory, timeProvider, ElmanhgTelemetry.SourceName, ElmanhgTelemetry.MetricPrefix, ElmanhgTelemetry.ActivitySource);` |
| C11 | (rewrite of the 3 Fail handlers) | sealed class | e.g. `public sealed class FailEssayGradeHandler(IEssayGradeRepository essayGradeRepository, IOptions<EssayGradingOptions> essayGradingOptions, TimeProvider timeProvider) : FailRetriedWorkHandler<FailEssayGradeCommand, EssayGrade>(essayGradeRepository, timeProvider) { protected override int MaxAttempts => essayGradingOptions.Value.MaxAttempts; protected override TimeSpan RetryBaseDelay => TimeSpan.FromSeconds(essayGradingOptions.Value.RetryBaseDelaySeconds); }`. Math uses `MathStepGradingOptions`. TrainingExport uses `TrainingExportsOptions`. Constructor parameter lists are unchanged from today. |
| C12 | `api/Elmanhg.Tests/Core/Queues/ProbeSweepWorker.cs` | internal sealed class | `ProbeSweepWorker(IServiceScopeFactory scopeFactory, SweepOptions options, TimeProvider timeProvider, ILogger logger, BackgroundJobMetrics jobMetrics) : SweepWorker<SweepOptions>(scopeFactory, Options.Create(options), timeProvider, logger, jobMetrics)`. `public const string Name = "probe-job"`. Settable delegates: `Func<IReadOnlyCollection<Guid>, Task<List<Guid>>> List`, `Func<Guid, Task> Process`, `Func<Guid, string, Task>? RecordFailure`, `Func<Task>? BeforeList`. It overrides each member by calling its delegate (`RecordFailureAsync`/`BeforeListAsync` call base when the delegate is null). `SweepOptionsOf(o) => o`. |
| C13 | `api/Elmanhg.Tests/Core/Queues/SweepWorkerTests.cs` | test class | T-SW rows |
| C14 | `api/Elmanhg.Tests/Core/Queues/BackgroundJobMetricsTests.cs` | test class | T-BJM rows (moved + 1 new) |
| C15 | `api/Elmanhg.Tests/Core/DDD/RetryScheduleTests.cs` | test class | T-RS rows |
| C16 | `api/Elmanhg.Tests/Core/Errors/BaseExceptionTests.cs` | test class | T-ERR rows |
| C17 | `api/Elmanhg.Tests/Integration/Persistence/RetrySchedulePersistenceTests.cs` | test class | T-INT rows |

### SweepWorker contract (C4)
```csharp
namespace Core.Queues;

public abstract class SweepWorker<TOptions>(IServiceScopeFactory scopeFactory, IOptions<TOptions> options, TimeProvider timeProvider, ILogger logger, BackgroundJobMetrics jobMetrics) : BackgroundService where TOptions : class
{
    // Ids whose processing failed are left out of later batches until a sweep reaches the end of the backlog, so failing items cannot hold the head of every batch.
    private readonly HashSet<Guid> _deferredIds = [];

    protected abstract string JobName { get; }
    protected IServiceScopeFactory ScopeFactory => scopeFactory;
    protected ILogger Logger => logger;

    protected abstract SweepOptions SweepOptionsOf(TOptions options);
    protected abstract Task<List<Guid>> ListDueAsync(ISender sender, IReadOnlyCollection<Guid> deferredIds, CancellationToken cancellationToken);
    protected abstract Task ProcessAsync(ISender sender, Guid id, CancellationToken cancellationToken);
    protected virtual Task RecordFailureAsync(ISender sender, Guid id, string errorCode, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task BeforeListAsync(SweepOptions sweep, CancellationToken cancellationToken) => Task.CompletedTask;

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    // 1 sweep = SweepOptionsOf(options.Value); 2 if !sweep.Enabled return;
    // 3 jobMetrics.Register(JobName, sweep.Interval); 4 using var timer = new PeriodicTimer(sweep.Interval, timeProvider);
    // 5 while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) await SweepAsync(sweep, stoppingToken).ConfigureAwait(false);

    private async Task SweepAsync(SweepOptions sweep, CancellationToken stoppingToken)
    // using var run = jobMetrics.StartRun(JobName); await BeforeListAsync(sweep, stoppingToken);
    // var ids = await TryListDueAsync(stoppingToken); if (ids is null) run.MarkListingFailed(); ids ??= [];
    // if (ids.Count < sweep.BatchSize) _deferredIds.Clear();
    // foreach id: if (await TryProcessAsync(id, stoppingToken)) { run.ItemSucceeded(); continue; } run.ItemFailed(); _deferredIds.Add(id);

    private async Task<List<Guid>?> TryListDueAsync(CancellationToken stoppingToken)
    // try { await using var scope = scopeFactory.CreateAsyncScope(); return await ListDueAsync(scope.ServiceProvider.GetRequiredService<ISender>(), [.. _deferredIds], stoppingToken); }
    // catch (Exception exception) when (!stoppingToken.IsCancellationRequested) { logger.LogError(…D9…); return null; }

    private async Task<bool> TryProcessAsync(Guid id, CancellationToken stoppingToken)
    // try { new async scope; await ProcessAsync(sender, id, stoppingToken); return true; }
    // catch (Exception exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(…D9…); await TryRecordFailureAsync(id, BaseException.ErrorCodeOf(exception), stoppingToken); return false; }

    private async Task TryRecordFailureAsync(Guid id, string errorCode, CancellationToken stoppingToken)
    // try { new async scope; await RecordFailureAsync(sender, id, errorCode, stoppingToken); }
    // catch (Exception exception) when (!stoppingToken.IsCancellationRequested) { logger.LogError(…D9…); }
}
```
Every `await` has `.ConfigureAwait(false)`. If the file passes ~100 lines, that is acceptable; do not split it.

### C9 code
```csharp
namespace Elmanhg.Application.Shared.Retries;

public abstract class FailRetriedWorkHandler<TCommand, TWork>(IRepository<TWork> repository, TimeProvider timeProvider) : IRequestHandler<TCommand> where TCommand : IFailRetriedWorkCommand where TWork : class, IEntity, IRetriedWork
{
    protected abstract int MaxAttempts { get; }
    protected abstract TimeSpan RetryBaseDelay { get; }

    public async Task Handle(TCommand request, CancellationToken cancellationToken)
    {
        var work = await repository.FirstOrDefaultAsync(x => x.Id == request.WorkId, cancellationToken).ConfigureAwait(false);
        if (work is null || !work.IsPending)
        {
            return;
        }

        work.FailAttempt(request.ErrorCode, timeProvider.GetUtcNow(), MaxAttempts, RetryBaseDelay);

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

### Worker ports (all in `Elmanhg.Api.Workers`, `public sealed class X(IServiceScopeFactory scopeFactory, IOptions<O> o, TimeProvider timeProvider, ILogger<X> logger, BackgroundJobMetrics jobMetrics) : SweepWorker<O>(scopeFactory, o, timeProvider, logger, jobMetrics)`; ctor parameter names keep today's names)
| Worker | O | JobName | SweepOptionsOf → Enabled / IntervalSeconds / BatchSize | ListDueAsync sends | ProcessAsync sends (same scope, in order) | RecordFailureAsync | Extra |
|---|---|---|---|---|---|---|---|
| EssayGradingWorker | EssayGradingOptions | `essay-grading` | SweepEnabled / SweepIntervalSeconds / SweepBatchSize | `new GetDueEssayGradeIdsQuery()` | `GradeEssayCommand(id)`, `ApplyEssayGradeCommand(id)` | `FailEssayGradeCommand(id, errorCode)` | — |
| MathStepGradingWorker | MathStepGradingOptions | `math-step-grading` | SweepEnabled / SweepIntervalSeconds / SweepBatchSize | `new GetDueMathStepGradeIdsQuery()` | `CheckMathStepAnswerCommand`, `GradeMathStepsCommand`, `ApplyMathStepGradeCommand` | `FailMathStepGradeCommand(id, errorCode)` | — |
| TrainingExportWorker | TrainingExportsOptions | `training-export` | SweepEnabled / SweepIntervalSeconds / SweepBatchSize | `new GetDueTrainingExportIdsQuery()` | `RunTrainingExportCommand(id)` wrapped in `try … catch (ConflictCoreException exception) when (exception.ErrorCode == ErrorCodes.TrainingExportModifiedConcurrently && !cancellationToken.IsCancellationRequested) { Logger.LogInformation("Training export {TrainingExportId} was claimed by another run.", id); }` | `FailTrainingExportCommand(id, errorCode)` | — |
| TeacherVoiceTranscriptionWorker | AskTeacherOptions | `teacher-voice-transcription` | TranscriptionSweepEnabled / TranscriptionSweepIntervalSeconds / TranscriptionSweepBatchSize | `new GetDueVoiceDraftIdsQuery()` | `TranscribeVoiceDraftCommand(id)` | `FailVoiceDraftTranscriptionCommand(id)` (errorCode unused) | — |
| ExpiredExamSubmissionWorker | ExamsOptions | `exam-auto-submit` | AutoSubmitEnabled / AutoSubmitIntervalSeconds / AutoSubmitBatchSize | `new GetExpiredExamSessionIdsQuery(deferredIds)` | `AutoSubmitExamCommand(id)` | — | — |
| SubscriptionLapseWorker | SubscriptionsOptions | `subscription-lapse` | LapseSweepEnabled / LapseSweepIntervalSeconds / LapseSweepBatchSize | `new GetLapsedSubscriptionIdsQuery(deferredIds)` | `LapseSubscriptionCommand(id)` | — | — |
| LessonContentIndexWorker | ContentRetrievalOptions | `lesson-content-index` | IndexSweepEnabled / IndexSweepIntervalSeconds / IndexSweepBatchSize | `new GetStaleLessonContentIdsQuery(deferredIds)` | `ReindexLessonContentCommand(id)` | — | — |
| TrainingExportRetentionWorker | TrainingExportsOptions | `training-export-retention` | RetentionSweepEnabled / RetentionSweepIntervalSeconds / RetentionSweepBatchSize | `new GetExpiredTrainingExportIdsQuery(deferredIds)` | `ExpireTrainingExportCommand(id)` | — | — |
| TeacherThreadSlaWorker | AskTeacherOptions | `ask-teacher-sla` | SlaSweepEnabled / SlaSweepIntervalSeconds / SlaSweepBatchSize | `new GetDueSlaThreadIdsQuery(deferredIds)` | `ProcessTeacherThreadSlaCommand(id)` | — | `BeforeListAsync`: today's `RescheduleAsync` body with `batchSize` → `sweep.BatchSize`, `scopeFactory` → `ScopeFactory`, `logger` → `Logger` |

`JobName` is `protected override string JobName => "<name>";`, a string literal or a private const of the same value. Every `Send` gets `.ConfigureAwait(false)`. Worker files import `Core.Queues` and no longer import `Elmanhg.Application.Shared.Observability`.

## Error codes
None added. No resource strings. `ErrorCodes.TrainingExportModifiedConcurrently`, `EssayGradeNotPending`, `MathStepGradeNotPending`, `TrainingExportNotPending` and `TeacherVoiceDraftNotPending` are reused unchanged.

## Domain behaviour

### `Core.DDD.Models.RetrySchedule` (C6)
```csharp
public sealed class RetrySchedule
{
    // Owners map LastErrorCode to a 100-character column.
    public const int ErrorCodeMaxLength = 100;

    public int Attempts { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? LastErrorCode { get; private set; }

    private RetrySchedule() { }

    public static RetrySchedule DueAt(DateTimeOffset at) => new() { NextAttemptAt = at };

    public bool IsDueAt(DateTimeOffset now) => NextAttemptAt <= now;

    public bool RecordFailure(string? errorCode, DateTimeOffset at, int maxAttempts, TimeSpan baseDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        Attempts++;
        LastErrorCode = errorCode is null || errorCode.Length <= ErrorCodeMaxLength ? errorCode : errorCode[..ErrorCodeMaxLength];
        var exhausted = Attempts >= maxAttempts;
        NextAttemptAt = exhausted ? null : at + (baseDelay * Math.Pow(2, Attempts - 1));
        return exhausted;
    }

    public void RecordSuccess()
    {
        Attempts++;
        NextAttemptAt = null;
        LastErrorCode = null;
    }

    public void Lease(DateTimeOffset at, TimeSpan span) => NextAttemptAt = at + span;
}
```
No time truncation inside it: callers pass the already-truncated `at`.

### Aggregate `FailAttempt` bodies (guard, then mutate, then stamp; `UpdationDate = at` always)
- **EssayGrade**: `EnsurePending(); var at = ToMicroseconds(failedAt); if (Retry.RecordFailure(errorCode, at, maxAttempts, retryBaseDelay)) { Status = EssayGradeStatus.InReview; ReviewReason = EssayReviewReason.GradingFailed; } UpdationDate = at;`
- **MathStepGrade**: same, but the exhausted branch sets `Status = MathStepGradeStatus.InReview; ReviewReason = FinalAnswerVerdict is null ? MathStepReviewReason.FinalAnswerUnchecked : MathStepReviewReason.GradingFailed;`
- **TrainingExport**: exhausted branch `Status = TrainingExportStatus.Failed;`
- **TeacherVoiceDraft**: `if (Retry.RecordFailure(null, at, maxAttempts, retryBaseDelay)) { Status = TeacherVoiceDraftStatus.Failed; }` (keeps `TeacherThread.ToMicroseconds`).

Guards are unchanged: `EnsurePending()` throws `ConflictCoreException(ErrorCodes.XNotPending)`. No new exception types.

### EF owned block (example: EssayGrades; the other tables substitute the name and max-length const)
```csharp
builder.OwnsOne(x => x.Retry, retry =>
{
    retry.Property(x => x.Attempts).HasColumnName(nameof(RetrySchedule.Attempts));
    retry.Property(x => x.NextAttemptAt).HasColumnName(nameof(RetrySchedule.NextAttemptAt));
    retry.Property(x => x.LastErrorCode).HasColumnName(nameof(RetrySchedule.LastErrorCode)).HasMaxLength(AiIdentifierMaxLength);
    retry.HasIndex(x => x.NextAttemptAt).HasFilter("\"Status\" = 'Pending'").HasDatabaseName("IX_EssayGrades_NextAttemptAt");
});
builder.Navigation(x => x.Retry).IsRequired();
```
TeacherVoiceDrafts: replace the `LastErrorCode` line with `retry.Ignore(x => x.LastErrorCode);`. TrainingExports: `HasMaxLength(JobErrorCodeMaxLength)`. MathStepGrades: `AiIdentifierMaxLength`.
**Gate:** `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api` must print "No changes have been made to the model since the last migration." If it reports a change, adjust the mapping (column name, nullability, index name). Never add a migration and never edit `AppDbContextModelSnapshot.cs`. If it cannot be made empty, stop and report `BLOCKED`.

## API surface
No HTTP change. The OpenAPI document is unchanged. Config keys are unchanged.

## Test plan
Existing tests that must stay green **unedited** (they verify the port): every method in `EssayGradeTests`, `MathStepGradeTests`, `TrainingExportTests`, `TrainingExportRunTests`, `TeacherVoiceDraftTests`, `FailEssayGradeHandlerTests`, `FailMathStepGradeHandlerTests`, `FailTrainingExportHandlerTests`, `FailVoiceDraftTranscriptionHandlerTests`, `RunTrainingExportHandlerTests`, `TrainingExportClaimTests`, `EssayGradeEndpointTests`, `AppDbContextTests` (includes `HasPendingModelChanges`), `AuditChangeCaptureTests`, `ObservabilityRegistrationTests`. The 9 `*WorkerTests` stay green with only the D4 edits.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T-SW1 | `Tests/Core/Queues/SweepWorkerTests` | `Execute_Disabled_EndsWithoutListing` | `ExecuteTask` completes; `List` delegate never called; no `{prefix}.job.interval` measurement |
| T-SW2 | 〃 | `Execute_Enabled_RegistersIntervalUnderJobName` | `probe.job.interval` = `IntervalSeconds` (45) tagged `probe.job`=`probe-job` |
| T-SW3 | 〃 | `Sweep_AllItemsSucceed_ProcessesEachAndRecordsSucceededRun` | `Process` called for each id in order; `probe.job.runs` tag outcome `Succeeded`; `probe.job.items` Succeeded = 2; no logs |
| T-SW4 | 〃 | `Sweep_ListingAndEachItem_ResolveSenderFromNewScopes` | `ISender` registered `AddScoped` with a factory counting resolutions: 2 ids → 3 resolutions (1 list + 2 items) |
| T-SW5 | 〃 | `Sweep_ItemThrowsCoreException_LogsWarningAndRecordsFailureWithErrorCode` | `RecordFailure` receives `(id, "PROBE_FAILED")` from `ConflictCoreException("PROBE_FAILED")`; logged levels = `[Warning]`; the next id is still processed |
| T-SW6 | 〃 | `Sweep_ItemThrowsPlainException_RecordsExceptionTypeName` | errorCode = `"InvalidOperationException"` |
| T-SW7 | 〃 | `Sweep_RecordingFailureThrows_LogsErrorAndContinues` | levels `[Warning, Error]`; second id processed; run outcome `PartiallyFailed` |
| T-SW8 | 〃 | `Sweep_ListingThrows_LogsErrorAndRecordsFailedRun` | levels `[Error]`; `probe.job.runs` outcome `Failed`; `Process` never called |
| T-SW9 | 〃 | `Sweep_FailedIds_AreDeferredUntilAShortBatch` | BatchSize 2, pages `[a,b]`,`[c,d]`,`[e]`,`[]`, all fail but `e`: deferred sets seen = `[]`, `{a,b}`, `{a,b,c,d}`, `[]` (same scenario as `ExpiredExamSubmissionWorkerTests.Sweep_FailedIds_AreSkippedUntilTheBacklogEnds`) |
| T-SW10 | 〃 | `Sweep_BeforeListHook_RunsBeforeListingInsideTheRun` | call order recorded `BeforeList`, then `List`; one run measurement |
| T-SW11 | 〃 | `Stop_DuringListing_EndsTheLoopWithoutLogging` | `StopAsync` completes `ExecuteTask`; no log calls |
| T-SW12 | 〃 | `Sweep_NoRecordFailureOverride_StillCountsFailedItem` | `RecordFailure` delegate null → default no-op; items Failed = 1; levels `[Warning]` |
| T-BJM1..6 | `Tests/Core/Queues/BackgroundJobMetricsTests` | moved verbatim, names unchanged: `Run_AllItemsSucceed_RecordsSucceededRunItemsAndDuration`, `Run_SomeItemsFail_RecordsPartiallyFailedRun`, `Run_ListingFailed_RecordsFailedRunAndKeepsLastSuccess`, `Register_ReportsIntervalAndInitialLastSuccess`, `Run_Succeeded_AdvancesLastSuccessToCompletionTime`, `Dispose_CalledTwice_RecordsOneRun` | Same assertions, with meter `"Probe.Meter"`, prefix `"probe"`, `static readonly ActivitySource` `"Probe.Meter"`; tags read via `_metrics.JobTag`/`OutcomeTag` |
| T-BJM7 | 〃 | `Constructor_Prefix_DerivesTagNames` | `(JobTag, OutcomeTag)` = `("probe.job", "probe.outcome")` |
| T-BJM8 | 〃 | `StartRun_ListenedSource_StartsJobActivityTaggedWithJobName` | `ActivityListener` on `"Probe.Meter"`: one activity `"job probe-job"` with tag `probe.job`=`probe-job`; after listing failure its status is `Error` |
| T-OBS1 | `Tests/Api/Hosting/ObservabilityExtensionsTests` | `AddElmanhgObservability_JobRun_UsesElmanhgJobMetricNames` | Resolve `BackgroundJobMetrics`; `StartRun("x").Dispose()`; collector (`"Elmanhg"`, `"elmanhg.job.runs"`) has 1 measurement tagged `elmanhg.job`=`x`, `elmanhg.outcome`=`Succeeded` |
| T-RS1 | `Tests/Core/DDD/RetryScheduleTests` | `DueAt_NewSchedule_IsDueFromThatTimeWithNoAttempts` | Attempts 0, NextAttemptAt = at, LastErrorCode null; `IsDueAt(at)` true; `IsDueAt(at - 1µs)` false |
| T-RS2 | 〃 | `RecordFailure_AttemptsLeft_DoublesTheDelayEachTime` | base 30 s, max 4: first → next = t1+30 s, returns false; second → next = t2+60 s, Attempts 2, LastErrorCode = code |
| T-RS3 | 〃 | `RecordFailure_LastAttempt_ReturnsExhaustedAndClearsNextAttempt` | max 1 → returns true, NextAttemptAt null, `IsDueAt(any)` false, LastErrorCode kept |
| T-RS4 | 〃 | `RecordFailure_LongErrorCode_TruncatesToMaxLength` | 150 chars → length 100 |
| T-RS5 | 〃 | `RecordFailure_NullErrorCode_LeavesLastErrorCodeNull` | after a failure with code then one with null → null |
| T-RS6 | 〃 | `RecordFailure_MaxAttemptsBelowOne_Throws` | `ArgumentOutOfRangeException`; Attempts stays 0 |
| T-RS7 | 〃 | `RecordSuccess_AfterFailure_CountsAttemptAndClearsScheduleAndError` | Attempts 2, NextAttemptAt null, LastErrorCode null |
| T-RS8 | 〃 | `Lease_PushesNextAttemptOutWithoutCountingAnAttempt` | NextAttemptAt = at+30 min, Attempts unchanged |
| T-ERR1 | `Tests/Core/Errors/BaseExceptionTests` | `ErrorCodeOf_CoreExceptionWithCode_ReturnsCode` | `NotFoundCoreException("X_NOT_FOUND")` → `"X_NOT_FOUND"` |
| T-ERR2 | 〃 | `ErrorCodeOf_BaseExceptionWithoutCode_ReturnsTypeName` | `new BaseException()` → `"BaseException"`; `new BaseException("")` → `"BaseException"` |
| T-ERR3 | 〃 | `ErrorCodeOf_OtherException_ReturnsTypeName` | `new IOException()` → `"IOException"` |
| T-INT1 | `Tests/Integration/Persistence/RetrySchedulePersistenceTests` (`ApiFactory` fixture as in `TrainingExportClaimTests`) | `SaveChanges_FailedTrainingExportAttempt_RoundTripsRetryColumns` | Request an export via `TrainingExportTestData.RequestAsync` (admin from `ScopeTestData`); in a scope load it, `FailAttempt("PROBE_FAILED", now, 3, 60 s)`, save; `TrainingExportTestData.ReadAsync` gives `(Attempts, LastErrorCode, NextAttemptAt)` = `(1, "PROBE_FAILED", truncated(now)+60 s)` |
| T-INT2 | 〃 | `GetDueIdsAsync_FailedExportPastItsBackoff_IsListedOnlyAfterTheDelay` | After T-INT1-style failure: `GetDueIdsAsync(now+59 s, 1000)` does not contain the id; `GetDueIdsAsync(now+61 s, 1000)` contains it (proves `x.Retry.NextAttemptAt` translates) |

## Definition of done
- [ ] `Core.Queues` contains `SweepOptions`, `SweepWorker<TOptions>`, `BackgroundJobMetrics`, `BackgroundJobRun`/`BackgroundJobRunOutcome` and `DependencyInjection.AddCoreBackgroundJobMetrics`; `Class1.cs` is gone. No Elmanhg name, metric name, error code or Egypt default appears in `api/core-libraries`.
- [ ] `Core.Queues.csproj` adds no new NuGet package (MediatR 14.1.0 is already pinned in core). No new project; the slnx is unchanged.
- [ ] `BaseException.ErrorCodeOf` exists, and none of the 3 private `ErrorCodeOf` copies remain (`grep "ErrorCodeOf" api/Elmanhg.Api` shows only calls, none in Workers).
- [ ] All 9 workers derive from `SweepWorker<TOptions>`. None contains `PeriodicTimer`, `CreateAsyncScope` (except the SLA `BeforeListAsync`) or `_deferredIds`. Essay and Math workers are thin (only `JobName`, `SweepOptionsOf`, `ListDueAsync`, `ProcessAsync`, `RecordFailureAsync`).
- [ ] Every existing config key and option class is unchanged (`git diff` shows no change under `Shared/Options` or in `appsettings*.json`).
- [ ] `elmanhg.job.runs/duration/items/last_success/interval`, tags `elmanhg.job`/`elmanhg.outcome` and span `job <name>` on source `Elmanhg` are unchanged (T-OBS1 plus the worker tests).
- [ ] `RetrySchedule` exists in `Core.DDD.Models` with `DueAt`, `IsDueAt`, `RecordFailure` returning exhausted, `RecordSuccess` and `Lease`. The 4 aggregates hold `Retry` with read-only pass-throughs. Terminal status transitions stay in each aggregate. `UpdationDate` is set in every mutating method.
- [ ] No `ToMicroseconds` method body or call line changed. No `.IsRowVersion()` line or `SaveChangesAsync` code in `AppDbContext*` changed.
- [ ] `dotnet ef migrations has-pending-model-changes` reports no changes. No new migration file. `AppDbContextModelSnapshot.cs` is untouched.
- [ ] The 4 `GetDueIdsAsync` queries use `x.Retry.NextAttemptAt`.
- [ ] `FailEssayGradeHandler`, `FailMathStepGradeHandler` and `FailTrainingExportHandler` derive from `FailRetriedWorkHandler<,>` with unchanged constructors. Their existing tests pass unedited.
- [ ] Every T-* test exists with exactly these names. Existing tests are unedited except the D4 construction/const lines and the moved `BackgroundJobMetricsTests`.
- [ ] `dotnet build api/Elmanhg.slnx -c Release` gives 0 warnings. `dotnet test api/ -c Release` is green.
- [ ] Docs updated as listed: `docs/constitution.md`, `docs/observability.md`, `docs/essay-grading.md`, `docs/math-step-grading.md`, `docs/ask-teacher.md`, `docs/training-data.md`, `.claude/skills/dotnet-feature/SKILL.md`.
- [ ] No code comment contains `#<number>`. The only new comments are the deferral WHY in `SweepWorker` and the column-width WHY in `RetrySchedule`.
