# Implementation — [E6.S2] Unit exam generation and sitting (#81)

## Files created

### API: Domain
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/Sessions/Session.Exam.cs` | 83 | `StartUnitExam`, `IsPastDeadline`, `SaveExamAnswer` |
| `api/Elmanhg.Domain/Sessions/Session.ExamSubmission.cs` | 41 | `SubmitExam` (attempt time 0, idempotent) |
| `api/Elmanhg.Domain/Sessions/Exams/ExamCandidate.cs` | 5 | Candidate record |
| `api/Elmanhg.Domain/Sessions/Exams/ExamDifficultyTargets.cs` | 28 | Largest-remainder apportioning |
| `api/Elmanhg.Domain/Sessions/Exams/ExamQuestionSelector.cs` | 52 | Decision 13 selector |
| `api/Elmanhg.Domain/Sessions/Exams/ExamItemPlacement.cs` | 3 | Item → lesson/objective placement |
| `api/Elmanhg.Domain/Sessions/Exams/ExamShare.cs` | 9 | Breakdown share with `ScorePercent` |
| `api/Elmanhg.Domain/Sessions/Exams/ExamBreakdown.cs` | 59 | Per-lesson and weakest-objective breakdowns |
| `api/Elmanhg.Domain/ExamBlueprints/ExamBlueprintResolution.cs` | 12 | Unit → subject default resolution |

### API: Application
| Path | Lines | Purpose |
|---|---|---|
| `Shared/Options/ExamsOptions.cs` | 24 | `Exams:*` options |
| `Exams/Shared/ExamSessionResult.cs`, `ExamItemResult.cs`, `ExamBreakdownResults.cs`, `ExamAnswerSavedResult.cs`, `UnitExamOverviewResult.cs` | 3–13 each | Result records |
| `Exams/Shared/UnitExamOverviewResultGenerator.cs` | 28 | Overview mapping |
| `Exams/Shared/ExamSessionResultGenerator.cs` | 44 | Session/item mapping (reuses `SessionResultGenerator.GenerateItem`) |
| `Exams/Shared/ExamBreakdownResultGenerator.cs` | 28 | Lesson/objective result mapping |
| `Exams/Shared/ExamSessionResultLoader.cs` | 55 | Loads unit, subject, placements; builds the result |
| `Exams/Shared/ExamSubmission.cs` | 63 | Shared grading + mastery on submit |
| `Exams/GetUnitExamOverview/{Query,Validator,Handler}.cs` | 6/13/43 | Overview use case |
| `Exams/StartUnitExam/{Command,Validator,Handler}.cs` | 6/13/104 | Start / resume / expired-submit |
| `Exams/GetExamSession/{Query,Validator,Handler}.cs` | 6/13/37 | Read |
| `Exams/SaveExamAnswer/{Command,Validator,Handler}.cs` | 7/25/57 | Draft save |
| `Exams/SubmitExam/{Command,Validator,Handler}.cs` | 6/13/44 | Submit |
| `Exams/GetExpiredExamSessionIds/{Query,Handler}.cs` | 5/20 | Worker listing |
| `Exams/AutoSubmitExam/{Command,Validator,Handler}.cs` | 5/13/28 | Worker per-session submit |

### API: Api, Infrastructure
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Api/Controllers/Exams/ExamsController.cs` | 63 | 5 actions, `AssessmentsTake` |
| `api/Elmanhg.Api/Controllers/Exams/Requests.cs` | 5 | `SaveExamAnswerRequest` |
| `api/Elmanhg.Api/Workers/ExpiredExamSubmissionWorker.cs` | 61 | `BackgroundService` + `PeriodicTimer(TimeProvider)`, scope per call |
| `api/Elmanhg.Infrastructure/Migrations/20260928184104_AddExamSittings.cs` (+ Designer) | 90 | 5 nullable AddColumn + 2 CreateIndex, no drops |

### API tests
| Path | Tests |
|---|---|
| `Builders/ExamSessionBuilder.cs` | builder |
| `Domain/Sessions/SessionExamStartTests.cs` | 11 methods (13 cases) — #1–11 |
| `Domain/Sessions/SessionExamAnswerTests.cs` | 8 — #12–19 |
| `Domain/Sessions/SessionExamSubmissionTests.cs` | 7 — #20–26 |
| `Domain/Sessions/Exams/ExamQuestionSelectorTests.cs` | 9 — #27–35 |
| `Domain/Sessions/Exams/ExamDifficultyTargetsTests.cs` | 2 theories — #36–37 |
| `Domain/Sessions/Exams/ExamBreakdownTests.cs` | 10 — #38–47 |
| `Domain/ExamBlueprints/ExamBlueprintResolutionTests.cs` | 4 — #48–51 |
| `Domain/ExamBlueprints/ExamBlueprintServabilityTests.cs` | 2 — #52–53 |
| `Application/Features/Exams/**` (13 files) | A1–A50 (59 methods) |
| `Integration/Exams/ExamTestData.cs` + 6 endpoint/worker test classes | I1–I29 |
| `Integration/Persistence/ExamSessionPersistenceTests.cs` | I30–I32 |

New API test methods: 53 domain + 59 application + 38 integration = 150, plus D1/D2 added to existing classes.

### Web
| Path | Lines | Purpose |
|---|---|---|
| `features/exam/index.ts`, `locales.ts`, `i18n/en.json`, `i18n/ar.json` | 4/4/65/65 | Barrel and locales |
| `features/exam/api/examSession.ts` | 35 | Pure helpers (+ `splitDuration`, as the plan allows) |
| `features/exam/hooks/useStartExam.ts`, `useExamAnswers.ts`, `useExamCountdown.ts`, `useSubmitExam.ts` | 39/115/46/52 | Hooks |
| `features/exam/components/*` (10 files) | 33–80 | Summary, actions, header, card, dialog, runner, result summary, lesson breakdown, weakest objectives, review item |
| `features/exam/pages/ExamStartPage.tsx`, `ExamPage.tsx`, `ExamResultPage.tsx` | 42/35/65 | Pages |
| `routes/student/exam-start.$unitId.tsx`, `exam.$sessionId.tsx`, `exam-result.$sessionId.tsx` | 11 each | Routes |
| `test/examFixtures.ts` | 106 | Fixtures |
| Tests: `examSession.test.ts` (W1–5), `ExamStartPage.test.tsx` (W6–17), `ExamPage.test.tsx` (W18–24), `ExamPage.autosave.test.tsx` (W25–28), `ExamPage.submit.test.tsx` (W29–33), `ExamResultPage.test.tsx` (W34–44), `progress/components/UnitProgressTable.test.tsx` (W-P5) | 45 new `it`s |

### Docs
| Path | Purpose |
|---|---|
| `docs/exams.md` | New exam contract (resolution, selection, sitting, submission, auto-submit, breakdown, API, errors, options, screens) |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Sessions/Session.cs` | `TimeLimitMinutes`, `PassMark`, `Deadline`, `IsExam`; `ToMicroseconds` carries the WHY comment |
| `Session.Answering.cs`, `Session.Submission.cs` | Exam guards; `CalculateScorePercent()` extracted |
| `SessionItem.cs` | `SavedAnswer`, `AnswerSavedAt`, `SaveAnswer` |
| `ExamBlueprints/ExamBlueprint.cs` | `EnsureServable`, `Describe` |
| `Questions/IQuestionRepository.cs`, `Infrastructure/Questions/QuestionRepository.cs` | `GetServableExamCandidatesAsync` |
| Domain + Application `ErrorCodes.cs` | 4 codes (EXAMS groups) |
| `Application/DependencyInjection.cs` | `ExamsOptions` registration |
| `SubmitAnswerHandler.cs`, `FinishSessionHandler.cs` | `&& x.Kind == SessionKind.Quiz` |
| `Infrastructure/Data/Context/AppDbContext.cs` | 2 index constants, 2 indexes, `SavedAnswer` jsonb, `OneOpenExamIndex` → 409 mapping |
| `Migrations/AppDbContextModelSnapshot.cs` | Regenerated |
| `Api/Program.cs` | `AddHostedService<ExpiredExamSubmissionWorker>()` (+ using) |
| `Api/Resources/Messages.{en,ar}.resx` | 4 keys |
| `Api/appsettings.example.json` | `Exams` section |
| `api/openapi/v1.json` | Regenerated by `dotnet build` |
| `Tests/Integration/Infrastructure/ApiFactory.cs` | `UseSetting("Exams:AutoSubmitEnabled","false")` with the plan's comment; 4 in-memory keys |
| `Tests/Integration/Persistence/AppDbContextTests.cs` | `eighteenth => …_AddExamSittings` |
| `SubmitAnswerHandlerTests.cs`, `FinishSessionHandlerTests.cs` | D1, D2 |
| `postman/elmanhg.postman_collection.json` | `Exams` folder after `Sessions` (5 requests in state order), vars `examSessionId`, `examQuestionId` |
| `web/src/features/quiz/index.ts` | Exports for reuse |
| `web/src/app/i18n.ts` | `exam` namespace |
| `web/src/shared/i18n/{en,ar}.json` | 4 error codes |
| `web/src/features/progress/api/sessionHistory.ts` (+ test) | Exam links; W-P1/W-P2 added, "gives no link for exams" deleted |
| `web/src/features/progress/pages/ProgressPage.history.test.tsx` | W-P4 (exam row View link; `examSessionId` was already exported) |
| `web/src/features/progress/components/UnitProgressTable.tsx`, `i18n/{en,ar}.json` | «امتحان الوحدة» column |
| `web/src/shared/api/generated/**`, `web/src/routeTree.gen.ts` | Regenerated |
| `docs/PRD.md` (§7.4, §15), `docs/sessions.md`, `docs/exam-blueprints.md`, `docs/mastery.md`, `docs/progress.md`, `docs/audit-log.md` | Per the plan's Docs table |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `ExamRunner`: set `submitRef.current = submitter.submit` **in render** | Writing a ref during render breaks the React Compiler `react-hooks` refs rule | Set it in a `useEffect` with no deps (runs after every render). Same behaviour |
| `StartUnitExamHandler` step 14: load revisions **after** `SaveChangesAsync` for a new session | One code path loads revisions for both branches; the resume/expired branch needs them before save | Revisions are read once, before the single `SaveChangesAsync`, for both branches. Read-only query; still exactly one save |
| `Session.SubmitExam` step 4: throw on a missing grade inside the item loop | Throwing mid-loop would leave attempts added (guard-then-mutate) | All answered items are checked for a grade first, then attempts are created. Same exception, no partial mutation |
| `ExamTestData` members as listed | Tests also needed a problem-code reader, item-id reader and a retire helper; `QuestionTestData` is not in the touch list | Added `ItemQuestionIds`, `ReadCodeAsync`, `RetireQuestionAsync` to `ExamTestData` (no extra file) |
| I15/I16/I30: DB `SavedAnswer` equals `{"optionId":"b"}` | PostgreSQL jsonb normalises whitespace (`{"optionId": "b"}`) | Assert with `QuestionJson.AreEquivalent` |
| `ExamLessonBreakdown`: `h2` + table with 3 columns | Axe needs a header for the action column | Added an sr-only `th` ("Train now"); card wrapper as on the other result sections |
| Comment `SubmittedAt − StartedAt` | Non-ASCII minus in source | ASCII `-` |

## Build & test
- `dotnet build api/` → `Build succeeded.` `0 Warning(s)` (only pre-existing core-libraries CS8618/CS8602 when building projects individually).
- CI parity: moved `api/Elmanhg.Api/appsettings.json` aside, `dotnet test api/ -c Release` → `Test run summary: Passed! total: 1781 failed: 0 succeeded: 1781`; file restored.
- `dotnet ef migrations add AddExamSittings` → only 5 AddColumn + 2 CreateIndex.
- `npm --prefix web run gen:api` → regenerated; a second run produced no further drift.
- `npx vite build` → `routeTree.gen.ts` regenerated.
- `npm --prefix web run typecheck` → clean. `npm --prefix web run lint` → clean (0 warnings).
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` (in `web/`) → `All matched files use Prettier code style!`
- `npm --prefix web test -- --run` → `Test Files 97 passed (97)`, `Tests 606 passed (606)`.
- Mutation checks (each broken, run, restored, suite re-run green):
  - `IsPastDeadline` without grace → #10 (+10 s), #14, A46 fail. Never-expire → A33 fails.
  - `Kind == Quiz` removed from `SubmitAnswerHandler` → D1 fails.
  - Not-mastered preference reversed → #28, #29, A18 fail.
  - Web: urgency `<=` → `<` fails W4; skipping `flush()` before submit fails W31.

## Notes for review
- Deferred (for the follow-up issue): the lesson-open gate (PRD §7.4 bullet 1) ships with its default "no gate"; needs lesson-open tracking from #85. Recorded in `docs/exams.md`.
- `GetExpiredExamSessionIdsQuery` has no validator (it has no input); the plan lists none.
- The worker catches `Exception` (except `OperationCanceledException`) per plan and skill §8.9; this is the only try/catch added.
- Worker is off in integration tests via `UseSetting`; default ON in code and `appsettings.example.json`. Local devs must copy the `Exams` section into their `appsettings.json` only if they want non-default values (defaults satisfy validation).
- `i18n.ts` imports `@/features/exam/locales` directly, matching how the other namespaces are imported there.
- `PROGRESS.md` was already modified on the branch before this stage; untouched by me.
