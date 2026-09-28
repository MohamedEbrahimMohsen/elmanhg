VERDICT: APPROVED

# Review — Adaptive question selection (#75, E5.S2), round 1

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Integration/Persistence/AttemptSummaryPersistenceTests.cs:112` — no attempt sits exactly on the 0.8 boundary. If `>=` in `SessionRepository.cs:16` became `>`, this test would still pass. Add a 0.8 grade that must count as correct (D1).
- `api/Elmanhg.Domain/Sessions/Selection/QuestionAttemptSummary.cs:6` — when a wrong and a correct attempt share a timestamp, the question counts as correct (D14). This is accepted and documented in `docs/sessions.md` § Selection. It cannot happen today, because there is at most one open quiz per lesson. Revisit it when exams (E6) can serve a question the student also has in an open quiz.
- Plan D13 says the plan has "no Sort". My EXPLAIN ANALYZE on 360k rows (pgvector/pg17, migrations applied) chose a `Bitmap Index Scan on IX_Attempts_StudentId_QuestionId_CreatedAt` (Index Cond `StudentId = … AND QuestionId = ANY(…)`), then a small quicksort, then a GroupAggregate, in 0.38 ms. The index is used, so the claim is only cosmetic.
- `02-implementation.md` says "Deviations: None.", but its Notes list two small departures: a static `T0` field instead of a per-test local, and test files over 100 lines (183/211/210). Both are harmless, and the repo already has many test files over 100 lines (the largest is 406). They belong under Deviations, though.

## Verified
- Buckets match PRD §7.2 and D3 (`QuestionAttemptSummary.cs:6-8`). Unseen is anything with no summary. LastWrong is `LastCorrectAt != LastAttemptedAt`, which includes never correct. CorrectOnce is latest correct with `CorrectCount == 1`. Rest is latest correct with two or more correct. "Wrong" is below `Mastery:CorrectThreshold`.
- Order within each bucket matches D4. Candidates are de-duplicated and sorted first (`QuestionSelector.cs:6-9`). Unseen and CorrectOnce are shuffled. LastWrong is shuffled, then stable-sorted oldest first. Rest is shuffled, ranked oldest first, then Efraimidis–Spirakis with weight n..1, drawing one key per element in rank order (`:48-58`).
- The selector only returns candidate ids. The handler returns only loaded questions, in selection order (`StartQuizSessionHandler.cs:54-70`). Candidates come from `WhereServable` (`QuestionRepository.cs`), and `Session.StartQuiz` re-checks servability. Nothing repeats: `Distinct`, the domain guard and the unique index each prevent it. The quiz length is `min(count, pool)`. An empty pool short-circuits before the summary query and gives the existing 400.
- The summary is one GROUP BY with conditional aggregates. There is no manual IsDeleted, test-mode or version filter, and it uses the `(StudentId, QuestionId, CreatedAt)` index (EXPLAIN above).
- `GetRandomServableInLessonAsync` is gone from the source. `MasteryOptions` is `[Range]` and `ValidateOnStart`. `Random.Shared` is a singleton, and production code has no `new Random()`. Config is in `appsettings.example.json` and `ApiFactory`. The resume path does no selection queries (H6).
- There is no API change, so the Postman collection, OpenAPI, Orval and `web/` are rightly untouched. `docs/sessions.md` has the Selection section, lifecycle step 1, the Options row and the Test mode line, and agrees with the code. PRD §7.2 is implemented as written, so docs and code do not diverge.
- I re-ran CI, all green:
  - api build: 0 errors.
  - `api/openapi` drift: none.
  - `dotnet test api/ -c Release`: 1425/1425 (there is no appsettings.json).
  - vulnerable packages: none.
  - `has-pending-model-changes`: no changes.
  - web: `gen:api` drift none, typecheck clean, tests 70 files / 399 passed.
- Every test B1–B4, S1–S14, O1–O2, H1–H12, P1–P5 and E1–E2 exists with the planned name.

## Test quality
- Mutation 1 swapped LastWrong and CorrectOnce in the concat order. S1, H4 and E1 failed.
- Mutation 2 made the within-bucket order newest first. S6, S12 and E1 failed.
- I restored `QuestionSelector.cs` byte-for-byte (checked with `cmp`) and rebuilt.
- `QuestionSelectorTests`, `QuestionAttemptSummaryTests` and `MasteryOptionsTests` pin real behaviour.
- `StartQuizSessionHandlerTests`: H4 pins bucket order and threading of the threshold and student. H5 pins the skip of a missing question. H12 pins the short-circuit.
- `AttemptSummaryPersistenceTests` exercises the real SQL against Postgres. The one gap is the boundary case in the Non-blocking list above.
- The endpoint tests E1 and E2 drive the real flow end to end.
- None of the tests is vacuous.
