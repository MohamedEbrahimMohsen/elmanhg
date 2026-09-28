VERDICT: CHANGES_REQUESTED

# Review r2 — [E6.S2] Unit exam generation and sitting (#81)

Scope: the three hardening fixes in `02-implementation-r2.md` (worker catch filter, sweep starvation via `ExcludedIds`, web exam-view invalidation). Round 1 stays APPROVED for everything else.

## Blocking

### 1. The starvation test does not pin the "until the backlog ends" condition; clearing the set on every tick passes it
**Where:** `api/Elmanhg.Tests/Api/Workers/ExpiredExamSubmissionWorkerTests.cs:42-68` (guards `api/Elmanhg.Api/Workers/ExpiredExamSubmissionWorker.cs:32`)
**Rule:** testing convention (a test must fail if the code is wrong); orchestrator's must-fix #2; reviewer §5
**Problem:** The test has one full batch and then goes straight to the tail (`[a,b]`, `[c]`, `[c]`), so both the correct `Count < batchSize` clear and an unconditional clear yield the same exclusion sequence `[]`, `{a,b}`, `[c]`. I applied the mutation `sessionIds.Count < batchSize` -> `sessionIds.Count <= batchSize` in a scratch copy of `api/` (the working tree was not touched). This mutation clears the set on every tick, because a listing never exceeds the batch. All 3 worker tests still passed. The implementer's "no Clear" and "no Add" mutations do fail. But the condition that actually prevents starvation is not constrained by any test.
**Failure:** With the `<=` (or always-clear) mutation, batch size 2, and permanently failing head exams a, b, c, d followed by a healthy e: tick 1 lists [a,b] and defers {a,b}. Tick 2 excludes {a,b}, lists [c,d], clears the set and defers {c,d}. Tick 3 excludes {c,d} and lists [a,b] again. After that the sweep alternates forever and e is never submitted, which is the starvation this round was meant to fix. The suite stays green.
**Fix:** Make the test's backlog span at least two full batches before the tail, e.g. pages `[a,b]`, `[c,d]`, `[e]`, `[e]` with batch size 2, and assert that the third listing excludes `{a,b,c,d}` (the set is kept across full batches) and the fourth excludes only `[e]` (it is cleared at the tail).

## Non-blocking
- `api/Elmanhg.Api/Workers/ExpiredExamSubmissionWorker.cs:53,68`: once shutdown has started, a non-OCE exception (e.g. an Npgsql error racing the cancel) now escapes and the host logs it as a BackgroundService fault. This is harmless, since the host is already stopping, but it is noisier than filtering on the exception type as well.
- `ExpiredExamSubmissionWorker.cs:32-35`: the set is cleared before the tail batch is processed, so a small set of permanently failing exams (fewer than a batch) is retried every other tick, not every tick. That is acceptable and arguably desirable.

## Verified
- **Fix 1 (catch filter).** Both filters are `when (!stoppingToken.IsCancellationRequested)` (`:53`, `:68`). A shutdown cancellation propagates, and the loop's `WaitForNextTickAsync(stoppingToken)` (`:23`) also ends on shutdown. A non-shutdown `OperationCanceledException` is logged and swallowed. There is no new race: `_deferredIds` is touched only by the single sequential `ExecuteAsync` loop, and the query takes a snapshot (`[.. _deferredIds]`, `:51`).
- **Fix 2 bounds.** The set only receives ids that were listed and then failed. It is cleared whenever a listing returns fewer than `AutoSubmitBatchSize` rows (range 1-1000, so `Count < batchSize` can always fire), and also when the listing fails (`[]`). Exclusion shrinks the non-excluded backlog each tick, so absent an unbounded inflow of failing exams the sweep reaches the tail and every deferred id is retried. Its size is bounded by the number of currently-failing expired exams. It is lost on restart, which is harmless. Worker and handler read the same `ExamsOptions.AutoSubmitBatchSize`.
- **Handler predicate.** `!request.ExcludedIds.Contains(x.Id)` (`GetExpiredExamSessionIdsHandler.cs:14`) is covered by `Handle_ExcludedIds_SkipsThem`. PostgreSQL translation is covered by `GetExpiredExamSessionIds_ExcludedId_IsSkipped` (`ExpiredExamSubmissionTests.cs:53`).
- **Fix 3 (web).** `invalidateExamViews` (`web/src/features/exam/api/invalidateExamViews.ts`) invalidates the `/api/exams/units/` prefix and `invalidateMastery` (`/api/mastery` and `/api/progress`). The exam session key `/api/exams/{id}` is untouched, so the `setQueryData` that runs just before it in `useStartExam.ts:19-20` and `useSubmitExam.ts:25-26` is not undone. The helper test asserts that the session stays fresh, and the page tests assert that overview and history are invalidated.
- `docs/exams.md` § Auto-submit matches the code: deferral until a short listing, then retry; shutdown-only loop exit; other exceptions logged. No endpoint changed, so Postman and OpenAPI need no change (the query is internal).
- **Builds and tests.** `dotnet test api/ -c Release` with `appsettings.json` moved aside gave total 1786, failed 0, succeeded 1786. The file was restored afterwards. `npm --prefix web test -- --run` gave 98 files and 609 tests passed. Both match the report.

## Test quality
- `ExpiredExamSubmissionWorkerTests.Sweep_FailingSessions_LogsAndSubmitsTheRest` and `Stop_DuringSweep_EndsTheLoopWithoutLogging`: both constrain the filter. The old `is not OperationCanceledException` filter fails the first test, and a catch-all filter fails the second (by trace).
- `ExpiredExamSubmissionWorkerTests.Sweep_FailedIds_AreSkippedUntilTheBacklogEnds`: it constrains `Add` and the existence of `Clear`, but not the clear condition (Blocking #1).
- `GetExpiredExamSessionIdsHandlerTests.Handle_ExcludedIds_SkipsThem` and `ExpiredExamSubmissionTests.GetExpiredExamSessionIds_ExcludedId_IsSkipped`: both constrain the predicate.
- `invalidateExamViews.test.ts`, `ExamStartPage.test.tsx` ("marks the history stale after starting") and `ExamPage.submit.test.tsx` ("marks the exam start page and the history stale after submitting"): these constrain the prefix and the hook wiring.

## Round 3 verification (orchestrator)
Verified with a mutation: `<` changed to `<=` at ExpiredExamSubmissionWorker.cs:32 fails 1 of 3 worker tests. Restored. Blocking #1 is resolved, so the story is APPROVED.
