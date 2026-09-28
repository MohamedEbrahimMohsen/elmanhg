# Implementation r2 — [E6.S2] Unit exam generation and sitting (#81)

Scope: three non-blocking notes from `03-review.md` that the orchestrator made must-fix. Nothing else was changed.

## Findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Worker exception filter. Both catch filters are now `when (!stoppingToken.IsCancellationRequested)`. Only a cancellation during host shutdown ends the loop. Any other exception, including an `OperationCanceledException` that is not a shutdown, is logged and the sweep continues. | `api/Elmanhg.Api/Workers/ExpiredExamSubmissionWorker.cs:53,68` |
| 2 | Sweep starvation. `SubmitAsync` now returns `bool`. A failed id goes into an in-memory `_deferredIds` set, and the next listings exclude that set (`GetExpiredExamSessionIdsQuery(IReadOnlyCollection<Guid> ExcludedIds)`, filter `!request.ExcludedIds.Contains(x.Id)`). When a listing returns less than a full batch (the end of the backlog), the set is cleared, so the failed ids are retried. A comment on the field records the invariant. | `ExpiredExamSubmissionWorker.cs:11-44`, `GetExpiredExamSessionIdsQuery.cs:5`, `GetExpiredExamSessionIdsHandler.cs:14` |
| 3 | Web cache. New `invalidateExamViews(queryClient)` marks every `/api/exams/units/…` start-page query stale and calls `invalidateMastery`, which covers `/api/mastery` and `/api/progress` (history, subjects, weak spots). It is called on start success and on submit success, where it replaces the bare `invalidateMastery` call. | `web/src/features/exam/api/invalidateExamViews.ts`, `hooks/useStartExam.ts:20`, `hooks/useSubmitExam.ts:26` |

### Why this starvation fix
- **Why not offset or keyset paging.** The generic repository has no arbitrary `Skip`, and `FindPaginatedAsync` pages are page-aligned. Keyset paging on `(Deadline, Id)` needs a Guid comparison that behaves the same in C# and in PostgreSQL, and it needs a new result type.
- **Why not a persisted failure marker.** That needs a migration.
- **What the exclusion set gives.** It is a single `NOT = ALL(@ids)` predicate. Each sweep still processes one batch, so a handler that returns without doing anything can never cause an infinite loop.
- **Bounds.** The set holds only exams that are currently failing, and it is cleared every time the sweep reaches the tail. Every expired exam is attempted within about `ceil(backlog / batch) + 1` ticks.
- **Trade-offs.** The set is lost on restart, which is harmless because everything is retried. A failing listing returns `[]`, which also clears the set.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Tests/Api/Workers/ExpiredExamSubmissionWorkerTests.cs` | 104 | Three worker tests that drive real sweeps through the timer |
| `api/Elmanhg.Tests/Api/Workers/ManualTimeProvider.cs` | 31 | Hand-written `TimeProvider` whose `Tick()` fires the `PeriodicTimer`. `Microsoft.Extensions.TimeProvider.Testing` is not referenced, so I did not add a package. |
| `web/src/features/exam/api/invalidateExamViews.ts` | 16 | Invalidates the start page, mastery and progress queries |
| `web/src/features/exam/api/invalidateExamViews.test.ts` | 27 | Checks that overview, history and progress are invalidated and the exam session is not |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Api/Workers/ExpiredExamSubmissionWorker.cs` | Exception filters (#1) and deferral set (#2) |
| `api/Elmanhg.Application/Exams/GetExpiredExamSessionIds/GetExpiredExamSessionIdsQuery.cs` | Adds the `ExcludedIds` parameter |
| `api/Elmanhg.Application/Exams/GetExpiredExamSessionIds/GetExpiredExamSessionIdsHandler.cs` | Adds the exclusion predicate |
| `api/Elmanhg.Tests/Application/Features/Exams/GetExpiredExamSessionIds/GetExpiredExamSessionIdsHandlerTests.cs` | Existing call changed to `([])`. New test `Handle_ExcludedIds_SkipsThem`. |
| `api/Elmanhg.Tests/Integration/Exams/ExpiredExamSubmissionTests.cs` | Existing call changed to `([])`. New test `GetExpiredExamSessionIds_ExcludedId_IsSkipped`, which proves the predicate translates on PostgreSQL. |
| `web/src/features/exam/hooks/useStartExam.ts`, `useSubmitExam.ts` | Call `invalidateExamViews` |
| `web/src/test/renderWithProviders.tsx` | `renderApp` also returns its `queryClient` (additive) |
| `web/src/features/exam/pages/ExamStartPage.test.tsx` | New test: "marks the history stale after starting" |
| `web/src/features/exam/pages/ExamPage.submit.test.tsx` | New test: "marks the exam start page and the history stale after submitting" |
| `docs/exams.md` § Auto-submit | Adds the deferral rule and the shutdown-only loop exit (docs sync) |

## Tests and mutation checks
Each mutation was applied, run, seen to fail, and then reverted.

- `Sweep_FailingSessions_LogsAndSubmitsTheRest`: ids a, b and c. a throws `InvalidOperationException`, b throws a non-shutdown `OperationCanceledException`, and c must still be sent. The test also expects 2 Warning logs and a loop that is still running.
  - Mutation: the old `is not OperationCanceledException` filter. Result: FAIL.
- `Sweep_FailedIds_AreSkippedUntilTheBacklogEnds`: batch size 2, all submits fail. The excluded sets per listing must be `[]`, then `{a,b}`, then `[c]`.
  - Mutation: no `Add`. Result: FAIL.
  - Mutation: no `Clear`. Result: FAIL.
- `Stop_DuringSweep_EndsTheLoopWithoutLogging`: `StopAsync` during a listing ends `ExecuteTask` with nothing logged.
  - Mutation: catch-all filter. Result: FAIL.
- `Handle_ExcludedIds_SkipsThem` and `GetExpiredExamSessionIds_ExcludedId_IsSkipped`.
  - Mutation: predicate removed. Result: both FAIL.
- Web start and submit tests.
  - Mutation: the call removed from both hooks. Result: both FAIL.
- `invalidateExamViews` test and the submit test.
  - Mutation: wrong prefix. Result: both FAIL.

## Deviations
None. The three fixes had no plan signatures. The query record gaining a parameter is the only contract change, and the query is internal (no endpoint, so no Postman or OpenAPI change).

## Build & test
- `api/Elmanhg.Api/appsettings.json` was moved to the scratchpad before the dotnet runs and restored afterwards.
- `dotnet test api/ -c Release`: `total: 1786, failed: 0, succeeded: 1786` (1781 before, plus 5 new).
- `dotnet build api/ -c Release --no-incremental`: 0 errors. The 9 warnings are all pre-existing ones in `core-libraries` (CS8618/CS8602). None are in the touched code.
- `npm --prefix web run typecheck`: clean.
- `npm --prefix web run lint` (`--max-warnings=0`): clean.
- `npm --prefix web test -- --run`: `Test Files 98 passed (98)`, `Tests 609 passed (609)`.
- `npm --prefix web run format:check -- --end-of-line auto`: "All matched files use Prettier code style!"

## Notes for review
- The worker tests wait on a `TaskCompletionSource`, with a 10 s `WaitAsync` guard that is only hit on failure. There are no sleeps. In .NET 10, `ExecuteAsync` runs on a background task, so each test awaits `ManualTimeProvider.TimerCreated` before its first tick.
- The start-page test asserts invalidation on the history only. The start page's own overview query is active when it is invalidated, so it refetches at once and `isInvalidated` resets. Asserting on it would be racy. The helper test covers the overview prefix, and the submit test covers the overview (inactive there).
- `PROGRESS.md` and `scripts/` were not touched. Nothing is committed.

## Round 3 (test fix)

| # | What changed | Where |
|---|---|---|
| 1 | Rewrote `Sweep_FailedIds_AreSkippedUntilTheBacklogEnds`. It now uses batch size 2 and five ids, with pages `[a,b]`, `[c,d]`, `[e]`, `[]`. a to d always fail and e succeeds. The test asserts the excluded set for each listing: `[]`, `{a,b}`, `{a,b,c,d}` (the set is kept across both full batches), then `[]` (cleared after the short batch, with e submitted). It also asserts that e, which sits behind the failing exams, received exactly one `AutoSubmitExamCommand`. | `api/Elmanhg.Tests/Api/Workers/ExpiredExamSubmissionWorkerTests.cs:42-70` |

No production code changed.

### Mutation check
- `sessionIds.Count < batchSize` -> `<=` at `ExpiredExamSubmissionWorker.cs:32`. Result: FAIL (`Expected excluded[2] to be a collection with 4 item(s)`, but it had {c,d}). The mutation was reverted, and line 32 reads `<` again.
- The no-`Clear` mutation fails by construction, because the fourth listing would exclude {a,b,c,d} instead of `[]`. This one was not re-run.

### Build & test
- Worker tests (`-- --filter-class Elmanhg.Tests.Api.Workers.ExpiredExamSubmissionWorkerTests`): `total: 3, failed: 0, succeeded: 3`.
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved to the scratchpad: `total: 1786, failed: 0, succeeded: 1786`. The file was restored afterwards.
- Nothing is committed.
