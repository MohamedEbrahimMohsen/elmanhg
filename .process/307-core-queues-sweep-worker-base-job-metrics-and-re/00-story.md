# [E21.S4] Core.Queues: sweep worker base, job metrics and RetrySchedule

Issue: #307

Fill the empty `Core.Queues` with the background-job pattern the app repeats in 9 workers, and promote the retry state copied in 4 aggregates.

- `SweepWorker<TOptions>` (abstract `ListDueAsync(deferred, ct)`, `ProcessAsync(id, ct)`, optional `RecordFailureAsync(id, errorCode, ct)`; a scope per list and per item; `PeriodicTimer` on `TimeProvider`; deferred-id set cleared on a short batch; kill switch) and a `SweepOptions` base (`Enabled`, `IntervalSeconds`, `BatchSize`). Existing config keys keep working (the prefixed names such as `SweepEnabled`, `LapseSweepEnabled`, `IndexSweepEnabled`) — no configuration breaking change.
- `BackgroundJobMetrics`/`BackgroundJobRun` move to core with the meter name and instrument prefix as parameters; the app keeps its `elmanhg.job.*` names exactly (dashboards depend on them).
- A shared `ErrorCodeOf(exception)` helper (copied 3 times).
- Port all 9 workers in `Elmanhg.Api/Workers`; EssayGrading and MathStepGrading become thin subclasses.
- `RetrySchedule` in Core.DDD: an owned value object (`Attempts`, `NextAttemptAt`, `LastErrorCode`, `RecordFailure(errorCode, at, maxAttempts, baseDelay)` → exhausted, `RecordSuccess()`, `IsDueAt(now)`, optional `Lease(at, span)`), mapped onto the existing columns in `EssayGrade`, `MathStepGrade`, `TrainingExport`, `TeacherVoiceDraft` — no data migration and no column renames (the migration diff must be empty). Terminal status transitions stay in each aggregate. Collapse the Fail handlers that become identical.

### Sub-tasks
- [ ] SweepWorker base, SweepOptions, metrics and ErrorCodeOf in core, with tests
- [ ] Port the 9 workers
- [ ] RetrySchedule value object in the 4 aggregates (no migration), with tests
- [ ] Collapse duplicated Fail/Apply handlers where identical
- [ ] Docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

