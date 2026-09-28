VERDICT: APPROVED

# Review — [E5.S4] Mastery calculation and headline counter (#77)

## Blocking
None.

## Non-blocking
- `web/src/features/session/pages/LoginPage.test.tsx:83,92,137,147,163,173,177-180` and `web/src/features/session/pages/SignUpPage.test.tsx:43-49,83-90`: the plan does not list these existing tests. `.claude/conventions/react-testing.md` "Test integrity" treats unlisted test edits as a finding. The edits are disclosed (Deviation 4). They are the same mechanical change the plan approved for `AppShell.test.tsx` (add `getMasteryMock()` and change the heading from "Home" to the greeting), and they weaken no assertion. I found no wrong result, so this does not block. The orchestrator should record them as a plan amendment so the test-integrity guard accepts them.
- `web/src/features/quiz/hooks/useQuizAnswer.ts:34`: no test fails if `void invalidateMastery(queryClient)` is removed. W9 tests only the utility. It is worth adding a hook or page test that shows the overview refetches after an answer (react-testing "invalidation effect visible in UI").
- `web/src/features/mastery/components/MasteryBar.tsx:8-13`: `.claude/design-system.md` Components › Progress says "fill animates motion.slow". Plan D17 explicitly chose no animation. That is an intentional plan decision, not a literal value, so it does not block. Revisit it when a shared Progress component is built.
- `docs/mastery.md` § Model, row `StudentId`: "FK `Users`". The migration's FK is to `AspNetUsers` (`20260928150149_AddQuestionMastery.cs:58`). It is a wording slip; the behaviour is the same.
- `api/Elmanhg.Infrastructure/Migrations/20260928150149_AddQuestionMastery.cs:12-30`: the backfill is a single statement, not batched (skill §6.7 "backfill in batches"). This is acceptable at the current data size, and the plan specified it.
- `api/Elmanhg.Tests/Application/Features/Mastery/GetMasteryOverview/GetMasteryOverviewHandlerTests.cs:24` uses `Substitute.For<TimeProvider>()` rather than `FakeTimeProvider` (dotnet-testing "Be deterministic"). This matches the plan.
- `api/Elmanhg.Tests/Domain/Mastery/QuestionMasteryTests.cs:125` reads `DateTimeOffset.UtcNow` as the assertion bound (dotnet-testing "Never" #6). The plan specified T10 this way.
- `api/Elmanhg.Application/Mastery/Shared/MasteryOverviewResultGenerator.cs:24`: no test covers the branch that returns null when `nextLesson` or its subject is missing.
- 02-implementation says the mutation checks for T25, T27, T35 and T48 were not re-run. I checked them by reading the code instead. T25 fails if `Record` is skipped. T27 fails if the `IsTestMode` guard is removed. T35 gives 0 under a UTC calendar. T48 gives mastered 1 if mastery is not intersected with servable. All four constrain the code.

## Verified
- `dotnet test api/ -c Release` with `appsettings.json` moved aside, then restored: `total: 1484, failed: 0, succeeded: 1484`. The test DLL was rebuilt after the latest source change.
- `npm --prefix web run typecheck`: clean. `npm --prefix web run lint` (`--max-warnings=0`): clean. `npm --prefix web test -- --run`: 80 files, 499 tests passed.
- `dotnet format --verify-no-changes`: every diagnostic is in vendored `api/core-libraries`; there are none in Elmanhg projects.
- Guard grep `DateTime\.(Now|UtcNow)|\.Result\b|\.Wait\(\)|new HttpClient\(|FromSqlRaw|async void` finds nothing in the new API code. `SqlQuery` is interpolated and parameterised (`SessionRepository.cs:25-31`).
- Every file in the plan's Files to create (#1–#54) exists with the planned signature, and nothing extra was created. Every Existing-code-touched edit was made as specified.
- D1/D4: the attempt and its mastery are written in one `SaveChangesAsync`. Only a new attempt updates mastery (`SubmitAnswerHandler.cs:52-58`). `Record` is idempotent and ordered by time (`QuestionMastery.cs:34-64`).
- D3: test mode is excluded in the handler, the streak SQL and the backfill SQL.
- D5/D7/D8/D9: one grouped servable query (`QuestionMasteryRepository.cs:16-29`); floor percent; pooled totals; seen and mastered intersected with servable.
- D10: Cairo-day streak via the injected `TimeProvider`. `ProgressOptions` is validated on start, including the time-zone check.
- D11: tie-break order matches. D12: `Progress.ViewOwn` is Students only (`PermissionMatrixPolicies.cs:26`).
- D13: the xmin conflict and the unique violation both map to 409 `SESSION_MODIFIED_CONCURRENTLY` (`AppDbContext.cs:54,71-74`). T57 and T58 prove it against real Postgres.
- D14: the backfill SQL is a public const, runs last in `Up`, and `Down` is `DropTable` only. D20: the entity is not audited, and `docs/audit-log.md` says so.
- Web: tokens only, no physical-direction utilities, both locales complete, all states present (greeting, headline, next lesson with "درّب الآن", subject `<article>` cards, loading, error+retry, empty).
- Postman: a "Mastery" folder after "Sessions" with two GET requests. URLs are correct, auth is inherited like "Get session", and `{{subjectId}}` is a collection variable. The collection JSON parses.
- Docs sync: PRD §15, `docs/sessions.md` (lifecycle, options row, error row), `docs/audit-log.md`, `docs/backlog.json` and the new `docs/mastery.md` all agree with the code. `docs/prototype.md` and `docs/claude-design-prompt.md` §4 describe the same home widgets; the missing plan line is incompleteness (E10). No divergence found.
- OpenAPI contains both paths, and their schemas match the result records.

## Test quality
- QuestionMasteryTests, MasteryTotalsTests, NextLessonRecommendationTests, StudyStreakTests: no doubles, and they cover each branch and boundary (threshold equality, ordering by time, idempotency, floor). They constrain the code.
- MasteryAttemptTests: checks the mapping against a real `Attempt`. It constrains the code.
- SubmitAnswerHandlerTests (T24–T27): they assert the mastery row's state and the `AddAsync`/save calls. The replay and test-mode guards are each caught. They constrain the code.
- ProgressOptionsTests: exercises the real `AddApplication` validation. It constrains the code.
- GetMasteryOverviewHandlerTests: totals, ordering, the next-lesson join and the time-zone streak are all checked against stubbed ports; none of the assertions just echo a stub's return value. They constrain the code.
- GetSubjectMasteryHandlerTests: the lesson stub runs the real predicate, so the Published filter is actually tested. They constrain the code.
- GetSubjectMasteryValidatorTests: one pass case and one fail case with the error code. Adequate.
- MasteryOverviewEndpointTests, SubjectMasteryEndpointTests, QuestionMasteryPersistenceTests: they run real HTTP and a real Postgres. They cover retirement, unpublishing, test mode, concurrency and the backfill, and check DB state. They constrain the code.
- StudentHomePage.test.tsx (W1–W8) and invalidateMastery.test.ts (W9): they assert through the DOM with MSW and include a mutation-checked link href. The one gap is the missing test for the call site in `useQuizAnswer`, listed above.
