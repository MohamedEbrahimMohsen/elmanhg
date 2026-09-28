VERDICT: APPROVED

# Review — [E6.S4] Retakes and best score (#83)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Api/Controllers/Exams/ExamsController.cs` — 113 lines, a little over the ~100-line guide (skill §1). Both actions are required and match their neighbours. The implementer flagged it.
- `api/Elmanhg.Application/Progress/GetSessionHistory/GetSessionHistoryHandler.cs:36` — the ternary with the awaited `.ToDictionary(...)` sits on one very long line. A named local, or a small private helper, would read better.
- `web/src/features/exam/api/invalidateExamViews.ts:17` — the literal `'/api/exams/'` is inline, while the other prefixes are exported constants. Consider an `examQueryPrefix` constant.
- `web/src/features/exam/pages/ExamResultPage.tsx:21` — the attempts query also runs for an open exam, which then redirects to the exam screen, and for a missing session. The request is wasted but harmless: it is 404 or ignored, and the card never renders.
- `api/Elmanhg.Tests/Integration/Exams/ExamAttemptsEndpointTests.cs` — 197 lines of test code. It could be split into session-endpoint and unit-endpoint classes.
- `PROGRESS.md` changed outside the plan. This is the documented per-story bookkeeping (Hand-off step 4.1: #82's row added, counts bumped), not a feature change.

## Verified
- **One best-score definition.** The only copy is `ExamBestScoreSpecification.Condition` (`api/Elmanhg.Domain/Sessions/ExamBestScoreSpecification.cs:8`).
  - `GetBestExamScoresAsync` (`SessionRepository.cs:36-45`) and `GetExamAttemptsAsync` (`:47-58`) both start from `WhereCountsTowardBestScore(studentId)`.
  - The history flag uses `IsSatisfiedBy` (`SessionHistoryResultGenerator.cs:18`), and so does the history handler's gate (`GetSessionHistoryHandler.cs:36`).
  - A grep over `api/` (non-test) finds no other kind / test-mode / submitted / score filter for bests.
  - `UnitExamBestScore` and `GetBestUnitExamScoresAsync` are gone, with no references left.
  - The attempts best is the max of rows that already passed the specification (`ExamAttemptsResultGenerator.cs:9`), as Decision 4 says.
  - The web computes no best anywhere; it only renders `isBest` / `isBestScore`.
- **Attempts scoping.**
  - `GetExamAttemptsHandler.cs:20` loads the anchor session with `x.Id == request.SessionId && x.StudentId == userId && x.Kind != Quiz`. Any miss throws 404 `SESSION_NOT_FOUND` before the list query runs.
  - The list query is keyed by the caller's `userId` (`:26`), never by the anchor's `StudentId`. A student cannot read another student's sittings by session id or by unit id.
  - Integration test #27 proves the 404 on the wire. Test #28 proves that another student's sitting of the same unit (100) is excluded from the list.
  - Both actions carry `[Authorize(Policy = DefaultCodes.AssessmentsTake)]`. Tests #25, #26 and #35 cover 401 and 403.
- **Progress `isBestScore`.**
  - It compares against the student's all-time best per `ScopeKey`, and only for sittings that meet the specification, with exact decimal equality, so ties all get the flag.
  - The best-score query is skipped when no counted exam is on the page (test #21 checks it `DidNotReceive`).
  - Test #20 would fail against a page-local max, because each sitting is its page's max.
  - Integration test #36 covers unit A, unit B and multi scopes on real PostgreSQL.
- **Decisions.** All 20 Decisions are honoured. The route shapes and names match. The policy is right. No new error codes, no migration. Admin test-mode sittings return an empty list (test #34).
- **Files.** All 25 plan files exist. Nothing extra was created beyond the documented Orval re-emits (`progress.msw.ts`, the two zod files), which are generated.
- **Style.** Every type is `sealed` and uses a file-scoped namespace. Every await outside the controller has `.ConfigureAwait(false)`, and dates are `DateTimeOffset`. There is no `try`/`catch`. The only comment is the plan-mandated WHY. §8 DON'T catalog: nothing present. `dotnet format --verify-no-changes` over the touched folders: 0 files changed.
- **Web.**
  - Every class is a design-system token (`bg-accent-soft`, `text-accent`, `text-micro`, `shadow-1`, `py-2.25`, and so on). No physical `ml-/mr-/text-left`.
  - The i18n strings match the plan table exactly, and the card placement matches Decision 14.
  - The card has its own skeleton and its own error with retry.
  - `invalidateExamViews` covers `/api/exams/{id}/attempts`. It is called on start, start-multi and submit.
- **Postman.** "Get exam attempts" and "Get unit exam attempts" follow "Submit exam". They are GET, use collection bearer auth, and have the right URLs.
- **OpenAPI.** Both paths and both schemas are present in `api/openapi/v1.json`, and `isBestScore` is required.
- **Docs sync.** `docs/exams.md`, `docs/progress.md`, `docs/sessions.md` and `docs/claude-design-prompt.md` §4 agree with the code. `docs/prototype.md` step 4 and `prototype/app.js` `examHistory` match the behaviour: newest first, max, «الأفضل» on ties. No divergence.
- **Deviations.** All six are real and justified: `UseQueryResult<T, unknown>`, `exactOptionalPropertyTypes`, the axe matcher form, the extra generated files, row assertions without `within(row)`, and the stricter W3/W7.
- **Builds, run independently:**
  - `dotnet test api/ -c Release`, with `appsettings.json` moved aside and then restored: 1915/1915 passed, and no non-vendored warnings.
  - Web `typecheck`: clean.
  - Web `lint`: clean.
  - Web `test -- --run`: 103 files, 641/641 passed.
  - `npx prettier --check ... --end-of-line auto`: clean.

## Test quality
- `ExamBestScoreSpecificationTests`: constrains every clause (kind, test mode, submitted, owner). This is good.
- `ExamAttemptsResultGeneratorTests`: order, max, ties and null-on-empty. A wrong comparison or reordering fails it.
- `GetExamAttemptsHandlerTests`:
  - The stubs are argument-exact on kind and scope key, so a wrong argument returns an empty list and the test fails.
  - `StubFind` compiles the handler's predicate, so the quiz exclusion is really exercised.
  - Every throwing path checks that `GetExamAttemptsAsync` was `DidNotReceive`.
  - Not vacuous: #13 and #14 assert generator output (the best and the flags), not a pass-through.
- `GetUnitExamAttemptsHandlerTests`: the stub on the exact `unit:{id}` key constrains the scope key.
- `GetSessionHistoryHandlerTests` (#20, #21) and `GetSubjectProgressHandlerTests` (#22): constraining, as described above.
- Validator tests: one failing case and one valid case per rule.
- Integration tests #25–#36: real PostgreSQL. They cover ordering, the specification exclusions, scope identity (unit vs multi, size and unit set), BOLA, the admin path and a real retake flow.
- Web W1–W9: they assert user-visible text, roles and hrefs. W4 covers error then retry, and W5 runs Arabic with axe. None is vacuous.
