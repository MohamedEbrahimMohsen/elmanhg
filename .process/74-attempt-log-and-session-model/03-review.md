VERDICT: CHANGES_REQUESTED

# Review — Attempt log and session model (#74, E5.S1), round 1

## Blocking

### 1. Idempotent replays do not return the same body: in-memory timestamps carry sub-microsecond ticks that Postgres drops, and test 100 was loosened to hide it
**Where:** `api/Elmanhg.Domain/Sessions/Session.cs:47` and `:65`, `api/Elmanhg.Domain/Sessions/Session.Answering.cs:28`, `api/Elmanhg.Domain/Sessions/Session.Submission.cs:15`; test `api/Elmanhg.Tests/Integration/Sessions/FinishSessionEndpointTests.cs:12-13,41`
**Rule:** plan Decision 9 ("the existing attempt is returned (200, same body, no new row)") and Decision 16 ("a second finish changes nothing and returns the same result"); `docs/sessions.md` § Idempotency (lines 68 and 72 make the same promises); skill §8.11 (never weaken a test to go green).
**Problem:** `now = DateTimeOffset.UtcNow` keeps 100 ns ticks. The first response is built from that in-memory value, but every later read comes from `timestamptz`, which stores microseconds. Plan test 100 asserted equality. It was changed to `BeCloseTo(…, 1 µs)` instead of fixing the code, so the contract is broken and the test no longer checks it.
**Failure:** `POST /api/sessions/{id}/finish` returns `submittedAt: "…:45.1234567+00:00"`. A retry, or `GET /api/sessions/{id}`, returns `"…:45.123456+00:00"`. The same happens to `attempt.createdAt` when an answer is replayed (Decision 9 promises the same body), and to `startedAt` between the start and resume responses.
**Fix:** Truncate `now` to whole microseconds in the aggregate, with one private static helper in `Session.cs` used by `StartQuiz`, `Resume`, `RecordAttempt` and `Submit` (for example `now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond))`, with a WHY comment about `timestamptz` precision). Then restore exact equality in test 100 and delete `PostgresTimestampPrecision`.

### 2. The time-taken tests cannot fail if the reported time is ignored or if the total is not summed
**Where:** `api/Elmanhg.Tests/Domain/Sessions/SessionAnsweringTests.cs:41-49` (test 17), `api/Elmanhg.Tests/Domain/Sessions/SessionSubmissionTests.cs:59-68` (test 31), `api/Elmanhg.Tests/Integration/Sessions/SubmitAnswerEndpointTests.cs:189`
**Rule:** reviewer order #5 (a vacuous test is blocking); plan Decision 14 and the Definition of Done item "Time taken is clamp(reported, 0, now − LastActivityAt)".
**Problem:** Test 17 reports 0, and the server-measured elapsed time in a unit test is also about 0 ms, so it passes whether the reported value is kept or thrown away. Test 31 expects `(0, 0)`, so a constant 0 passes. The integration test accepts any value from 0 to 1000 with 1000 reported, so both "always elapsed" and "always reported" pass. Only the upper clamp (test 18) is actually constrained.
**Failure:** Replacing the body of `MeasureTimeTaken` (`Session.Answering.cs:37-38`) with `return elapsed;` discards the client's time for every attempt. Changing `TotalTimeTakenMilliseconds` (`Session.cs:24`) to `=> 0` loses the session time. With either change the whole suite (1388 tests) stays green.
**Fix:** Make elapsed large enough to be deterministic. In an integration test, backdate the session with `UPDATE "Sessions" SET "LastActivityAt" = now() - interval '1 hour'` (Sessions is not append-only), then:
- answer with `timeTakenMilliseconds: 12000` and assert exactly 12000 is stored;
- answer a second item with no time after backdating again, and assert at least 3,600,000;
- assert that `GET` `timeTakenMilliseconds` equals the sum of the stored attempt times.
Either strengthen tests 17 and 31 along these lines, or keep their names and add the integration assertions to test 91.

## Non-blocking
- **Deviation 1 is correct; do not change it.** `docs/question-schemas.md` § Answer shapes (line 172) says "A missing field means 'no answer' and scores 0", and 422 is only for a non-object or a type mismatch. grade-draft uses the same `QuestionAnswerRules.CanRead`. `{"optionIds":1}` for an Mcq reads as `McqAnswer(null)`, which is graded as unanswered. `Canonicalize` stores it as `{}` (unknown property dropped, null not written), so nothing malformed is stored permanently. Optional: add to `docs/sessions.md:64` that a missing field is recorded as an unanswered attempt that scores 0.
- `docs/sessions.md:77`: "a gap between visits is never counted" is only true for a resume through `POST /api/sessions/quiz`. A reload through `GET /api/sessions/{id}` (the prototype route) does not move `LastActivityAt`, so when the client reports no time, the gap is counted. #76 should always send `timeTakenMilliseconds`, or the doc should say this.
- `api/Elmanhg.Application/Sessions/FinishSession/FinishSessionHandler.cs:29` racing `SubmitAnswerHandler.cs:49`: with no concurrency token on `Session` (#158), a finish can commit a `ScorePercent` that leaves out an attempt committed at the same moment. Track this with #158.
- `SubmitAnswerHandler.cs:49` grades before the replay or conflict check (already noted by the implementer). It does no harm.

## Verified
- **CI checks, re-run here** (no `appsettings.json`, Docker up):
  - `dotnet build -c Release`: 0 warnings, 0 errors.
  - `dotnet test -c Release`: 1388 of 1388 passed.
  - vulnerable packages: none in any project.
  - `has-pending-model-changes`: "No changes".
  - openapi `v1.json`: regenerated with 456 added lines.
  - web: `gen:api` shows no drift (same 3 modified and 7 new generated paths); typecheck, lint and format:check are clean; 70 files and 399 tests pass; build passes.
- **Every file in the plan's *Files to create* exists, and nothing extra was added.** The only undeclared change is the private `Touch` extraction, which the report discloses. The old `QuestionAnswerRules.cs` is deleted.
- **All 114 test-plan method names exist** (checked by script).
- **Append-only trigger** (`20260928114118_AddSessionsAndAttempts.cs:163-167`):
  - The function name `reject_append_only_mutation` does not collide with the audit log's `audit_logs_reject_mutation`.
  - `Down` drops both triggers and the function before `DROP TABLE`. DROP TABLE is not TRUNCATE, so it is unaffected.
  - The tests use no TRUNCATE, Respawn or delete cleanup (grep), and no production path hard-deletes users or questions, so the `Restrict` FKs never block anything.
  - Tests 108 and 109 check P0001 and that the row is unchanged.
- **Access:**
  - Every lookup has `x.StudentId == userId` in the same predicate (`StartQuizSessionHandler.cs:35`, `SubmitAnswerHandler.cs:24`, `FinishSessionHandler.cs:23`, `GetSessionHandler.cs:23`). `SessionRepositoryStub` compiles the real predicate, so the IDOR unit tests would fail without the filter. Integration tests 95, 103 and 105 return 404.
  - All four actions carry `AssessmentsTake`, and the Teacher 403 and anonymous 401 tests pass.
  - Test mode is taken from `GetClaim(ClaimTypes.Role) == "Admin"`, matching `GetSubjectHandler`, and tests 42 and 88 cover it.
- **Races:**
  - `IX_Attempts_SessionId_QuestionId` and `IX_Sessions_InProgressScope` (partial, `SubmittedAt IS NULL AND IsDeleted = false`) are mapped to 409 in `AppDbContext.cs:56-63`, inside the same `try`. Tests 111 to 113 exercise them against real Postgres.
  - Time can only be deflated, never inflated: it is clamped to `now − LastActivityAt` (`Session.Answering.cs:37-38`), and test 18 covers the upper bound.
- **Grading:**
  - Grading uses the revision at `item.QuestionVersion` (`SubmitAnswerHandler.cs:36-49` → `QuestionRevision.Grade`), never the live row.
  - Tests 35, 52 and 94 edit the key to "a" after serving and still grade "b" as Correct at v1. Each would fail if the live row or the latest revision were used.
- **Reveal rules:** answer keys are revealed only after an answer or after submission (`SessionResultGenerator.cs:25-27`), and tests 75, 76 and 104 cover this.
- **Resx:** all 12 keys are present in en and ar, matching the plan's table.
- **Other plan items present:**
  - `SessionsOptions` with `ValidateOnStart`, and the keys in `appsettings.example.json` and `ApiFactory`.
  - Postman `Sessions` folder with 4 requests in state order, the collection's bearer auth, the planned bodies and script, and the `sessionId` and `sessionQuestionId` variables.
  - `docs/PRD.md` §15, the `docs/audit-log.md` "Not audited" row, and the new `docs/sessions.md`.
- **Deviation 1:** confirmed against `docs/question-schemas.md` (see Non-blocking).
- **Deviation 2:** the behaviour is confirmed, and it is a defect (Blocking 1).

## Test quality
- **Constrain the implementation:**
  - SessionTests.
  - SessionAnsweringTests, except test 17, which is vacuous (Blocking 2). Test 19 only checks a range.
  - QuestionRevisionTests, QuestionAnswerRulesTests, AttemptTests.
  - StartQuiz, SubmitAnswer, Finish and GetSession handler tests. They use the real predicate through `SessionRepositoryStub`, and they assert Received(1) on success and DidNotReceive on throwing paths.
  - Validator tests.
  - Endpoint tests 81 to 107, and the persistence tests 108 to 113.
- **Do not constrain it:**
  - `SessionAnsweringTests.RecordAttempt_ReportedTimeWithinElapsed_KeepsReportedTime` and `SessionSubmissionTests.TotalTimeTakenMilliseconds_SumsAttempts` (Blocking 2).
  - `FinishSessionEndpointTests.Post_Twice_Returns200SameSubmittedAt`, as loosened (Blocking 1).
