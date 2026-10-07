VERDICT: APPROVED

# Review: Core.Queues sweep worker base, job metrics and RetrySchedule (E21.S4, round 1)

## Blocking
None.

## Non-blocking
- `api/core-libraries/Core.Queues/SweepWorker.cs:44`: `BeforeListAsync` runs outside any try/catch. If a future subclass hook throws, the worker loop dies, which goes against skill §8.9 ("a failed iteration ... never kills the loop"). The only override today (`TeacherThreadSlaWorker.cs:18-34`) catches internally, so behaviour is unchanged and the code matches plan C4. Worth guarding in the base later.
- `docs/observability.md:82`: "failed ids are deferred until a short batch" is only strictly true for the 5 workers whose query takes `deferredIds`. Essay, math, training-export and voice-transcription ignore the set (D7) and rely on `NextAttemptAt` backoff instead. Consider rewording.
- `api/core-libraries/Core.Queues/SweepWorker.cs:95`: the 5 workers with no recorder now open one extra async scope per failed item to call the no-op `RecordFailureAsync`. This has no observable effect. The implementer flagged it.
- `api/core-libraries/Core.DDD/Models/RetrySchedule.cs:3`: the skill §9 checklist asks for "value objects as sealed record". This is a mutable sealed class, a choice the plan made on purpose (D13: in-place mutation for EF owned-type tracking).
- `docs/audit-log.md` (Diff JSON shape): the exclusion list does not say that owned values (now `RetrySchedule`, and already `LocalizedText`) are left out of the diff. This gap existed before this change. D18 records the TrainingExport side effect in `docs/training-data.md` step 2.
- `api/core-libraries/Core.Queues/SweepWorker.cs`: the file is 112 lines, over the ~100-line guide. The plan allows this.

## Verified
- **Worker-by-worker equivalence with origin/main.** I compared all 9 workers line by line against the original files.
  - **Kill switch, interval and batch keys:** each worker maps the same option keys. Essay, Math and TrainingExport use `SweepEnabled/SweepIntervalSeconds/SweepBatchSize`. Voice uses `TranscriptionSweep*`, exam uses `AutoSubmit*`, lapse uses `LapseSweep*`, index uses `IndexSweep*`, retention uses `RetentionSweep*` and SLA uses `SlaSweep*`.
  - **Job names** are identical. `Register` happens before the `PeriodicTimer` on `TimeProvider`.
  - **Deferred ids:** the five deferred-id queries get the snapshot `[.. _deferredIds]`. The set is cleared when fewer than `BatchSize` ids come back, including after a listing failure (which yields an empty list).
  - **Retried-work workers:** the four due-queries use the same `SweepBatchSize`/`TranscriptionSweepBatchSize` limit, so the unused set stays bounded.
  - **Scopes:** one scope for the listing, one per item (Essay sends 2 commands and Math 3, in order, in that one scope), and one per failure record.
  - **Log levels:** Error on listing, Warning on item, Error on recording. SLA reschedule keeps Warning for `ConflictCoreException` and Error otherwise, and still loops while rescheduled equals BatchSize, inside the run and before listing.
  - **TrainingExport claim conflict:** `TRAINING_EXPORT_MODIFIED_CONCURRENTLY` is still caught inside the item. It logs Information, counts the item as succeeded and records no failure.
  - **Voice failure:** still sends `FailVoiceDraftTranscriptionCommand(id)` with no error code.
  - **Cancellation:** every catch keeps the stoppingToken-not-cancelled filter.
  - **Listing failure in the 4 retried workers:** these used to return early. Now they go on with an empty list. Nothing observable changes, because they never read the set.
- **RetrySchedule against the 4 original FailAttempt bodies.**
  - It increments `Attempts`, truncates the error code to 100 characters, and sets exhausted when Attempts >= maxAttempts, which clears `NextAttemptAt`. Otherwise the next attempt is at + base * 2^(Attempts-1).
  - Callers still pass the microsecond-truncated `at`, and the `ToMicroseconds` lines are unchanged.
  - Terminal transitions stay in each aggregate. MathStepGrade keeps its FinalAnswerUnchecked/GradingFailed choice. `UpdationDate = at` is still set.
  - Success paths call `RecordSuccess` (attempt counted, schedule and error cleared).
  - TrainingExport `BeginRun` calls `Retry.Lease(at, lease)` and does not count an attempt.
  - The voice draft calls `RecordFailure(null, ...)` and maps `Ignore(LastErrorCode)`.
  - The new `ArgumentOutOfRangeException` guard is unreachable because options are Range(1,10). The report discloses it.
- **Owned mapping onto the existing columns.** Column names and max lengths are explicit, index names (IX_Table_NextAttemptAt) are kept, and each owner has `Navigation(...).IsRequired()`. I ran `dotnet ef migrations has-pending-model-changes` with the CI connection-string env and it printed "No changes have been made to the model since the last migration." No migration was added and the snapshot is untouched.
- **Due queries.** All 4 `GetDueIdsAsync` use `x.Retry.NextAttemptAt` in both Where and Order. No other LINQ-to-SQL use of the pass-through properties remains (grep of Infrastructure and Application). T-INT2 proves the query translates against PostgreSQL.
- **Generic Fail handler.** `FailRetriedWorkHandler<,>` reproduces the 3 removed bodies exactly: load by id, return if missing or not Pending, `FailAttempt`, one `SaveChangesAsync`. Constructors are unchanged. The explicit `WorkId` mappings are exercised by the existing handler tests, which compile the predicate against real entities and pass unedited.
- **Config and metric names.** No options class or appsettings changed. The instruments are `elmanhg.job.runs/duration/items/last_success/interval` with tags `elmanhg.job`/`elmanhg.outcome`. The span is "job NAME" on `ElmanhgTelemetry.ActivitySource` and descriptions and units are verbatim. T-OBS1 pins this through the real DI registration, and the 9 worker tests pin it with literal consts.
- **Audit-diff side effect.** `AuditChangeReader.cs:21` reads only `IAuditedEntity` entries, so the owned `RetrySchedule` entry is skipped. This is recorded in `docs/training-data.md` step 2, as D18 requires.
- **Core is app-agnostic.** Source under `Core.Queues`, `RetrySchedule` and `BaseException` contains no Elmanhg names; the only grep hits were build artifacts. `Class1.cs` is deleted. MediatR 14.1.0 was already pinned in core, so no new package was added.
- **ErrorCodeOf.** The body is copied verbatim into `BaseException`, and no private copies remain in `Elmanhg.Api`.
- **No issue-number references in added comments.** The only new comments are the two WHY comments.
- **Docs sync.** `constitution.md`, `observability.md` (Source column, plus `math-step-grading` added to the jobs list), `essay-grading.md`, `math-step-grading.md`, `ask-teacher.md`, `training-data.md` and `SKILL.md` (deltas, §6.3 row, §8.9 line) all match the code. I found no divergence. Postman needs no change because there is no HTTP change.
- **Build and tests.** `dotnet build api/Elmanhg.slnx -c Release` gave 0 errors and 0 warnings (incremental). `dotnet test api/ -c Release --no-build` gave 5327 passed and 0 failed, matching the report. `dotnet format --verify-no-changes`, filtered to the touched files, shows only a pre-existing whitespace issue at `BaseException.cs:7`, not the added line.
- **Deviations.** All three (separate Probe.Sweep ActivitySource, how T-SW11 drives cancellation, placement of the §8.9 line) are disclosed and harmless.
- **Test edits.** Existing tests were edited only as D4 allows (construction expression and tag consts). `BackgroundJobMetricsTests` was moved with the same 6 method names and the same assertions.

## Test quality
- **SweepWorkerTests:** these constrain the base well.
  - T-SW4 counts sender resolutions, so a single shared scope would fail.
  - T-SW9 checks the exact deferred sets across four sweeps.
  - T-SW5, T-SW6 and T-SW7 pin the error-code projection and the log-level sequence.
  - T-SW11 pins silent shutdown.
  - T-SW1 checks that no listing and no registration happen when the worker is disabled.
- **BackgroundJobMetricsTests:** instrument names are literal (probe.job.*). Tag names go through `_metrics.JobTag`, but T-BJM7 pins how those are derived. T-BJM8 pins the span name, tag and Error status.
- **RetryScheduleTests:** they check the doubling (+30 s, then +60 s), exhaustion, truncation to 100, clearing the error code with null, the guard, success and lease. Each would fail on an off-by-one in the exponent or the comparison.
- **BaseExceptionTests:** they cover the code, an empty or null code, and a foreign exception.
- **RetrySchedulePersistenceTests:** a real PostgreSQL round-trip of the three columns plus translation of the owned-path query. This guards the mapping itself, not just the model diff.
- **Worker tests (unchanged assertions):** they still pin per-worker behaviour, including the claim-lost path, SLA rescheduling and deferral.
- I found no vacuous tests.
