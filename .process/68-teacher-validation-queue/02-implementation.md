# Implementation — Teacher validation queue (#68, E3.S5)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Questions/QuestionDecisionOutcome.cs | 3 | D1 enum |
| api/Elmanhg.Domain/Questions/QuestionDecision.cs | 32 | D2 append-only decision child entity |
| api/Elmanhg.Domain/ReviewSessions/ReviewSession.cs | 57 | D3 aggregate (Start, HasOpened, RecordOpening, EnsureOpened) |
| api/Elmanhg.Domain/ReviewSessions/ReviewSessionOpening.cs | 24 | D4 child entity |
| api/Elmanhg.Domain/ReviewSessions/IReviewSessionRepository.cs | 5 | D5 |
| api/Elmanhg.Infrastructure/ReviewSessions/ReviewSessionRepository.cs | 7 | I1 |
| api/Elmanhg.Infrastructure/Migrations/20260928080255_AddQuestionValidationQueue.cs (+ .Designer.cs) | 151 / 1357 | I2, generated, plus the two backfill `Sql` calls at the end of `Up` |
| api/Elmanhg.Application/Shared/Options/QuestionValidationOptions.cs | 23 | A1 |
| api/Elmanhg.Application/QuestionValidation/Shared/{ValidationQueueItemResult, ValidationQueueFiltersResult, ValidationQuestionDetailResult, ReviewSessionResult, ValidationResultGenerator}.cs | 3–43 | A2–A6 |
| api/Elmanhg.Application/QuestionValidation/GetValidationQueue/{Query, Validator, ValidationQueueFilter, Handler}.cs | 8/28/23/90 | A7–A10 |
| api/Elmanhg.Application/QuestionValidation/GetValidationQueueFilters/{Query, Handler}.cs | 6/45 | A11–A12 |
| api/Elmanhg.Application/QuestionValidation/GetValidationQuestion/{Query, Validator, Handler}.cs | 6/13/54 | A13–A14 |
| api/Elmanhg.Application/QuestionValidation/ApproveQuestion/{Command, Validator, Handler}.cs | 12/17/36 | A15–A17 |
| api/Elmanhg.Application/QuestionValidation/RejectQuestion/{Command, Validator, Handler}.cs | 11/22/36 | A18–A20 |
| api/Elmanhg.Application/QuestionValidation/StartReviewSession/{Command, Handler}.cs | 6/27 | A21 |
| api/Elmanhg.Application/QuestionValidation/RecordQuestionOpening/{Command, Validator, Handler}.cs | 5/14/43 | A22–A23 |
| api/Elmanhg.Application/QuestionValidation/BulkApproveQuestions/{Command, Result, Validator, Handler}.cs | 11/3/24/57 | A24–A26 |
| api/Elmanhg.Api/Controllers/QuestionValidation/Requests.cs | 9 | P1 |
| api/Elmanhg.Api/Controllers/QuestionValidation/ValidationQueueController.cs | 95 | P2, 8 actions, all `QuestionsValidate` |
| api/Elmanhg.Tests/Domain/Questions/QuestionQueueEntryTests.cs | 61 | tests 8–11 |
| api/Elmanhg.Tests/Domain/ReviewSessions/ReviewSessionTests.cs | 110 | tests 12–19 |
| api/Elmanhg.Tests/Application/Features/QuestionValidation/** (13 files) | 24–153 | tests 20–48 |
| api/Elmanhg.Tests/Integration/QuestionValidation/ValidationTestData.cs | 65 | test 49 helper |
| api/Elmanhg.Tests/Integration/QuestionValidation/{ValidationQueue, QuestionDecision, BulkApprove}EndpointTests.cs | 180/170/203 | tests 50–74 |
| web/src/features/questions/schemas/{validationQueueSearch, validationQueueFilters, approveQuestion, rejectQuestion}Schema.ts (+ .test.ts) | 8–20 | W1–W4 |
| web/src/features/questions/api/{validationQueueParams, pendingAge, reviewHistory, answerKey}.ts (+ .test.ts) | 17–53 | W5–W8 |
| web/src/features/questions/hooks/{useReviewSession, useValidationQueueSearch, useValidationQueue, useBulkApprove, useQuestionDecision, useRecordOpening}.ts | 10–64 | W9–W14 |
| web/src/features/questions/components/{ValidationQueueFilters, ValidationQueueItem, ValidationQueueList, BulkApproveBar, ValidationQuestionHeader, ValidationQuestionContent, ValidationDecisionPanel, ReviewHistoryTable}.tsx | 28–88 | W15–W22 |
| web/src/features/questions/pages/ValidationQueuePage.tsx (+ .test.tsx) | 106 / 209 | W23, test W9 |
| web/src/features/questions/pages/ValidationQuestionPage.tsx (+ .test.tsx) | 49 / 223 | W24, test W10 |
| web/src/routes/teacher/q.$questionId.tsx | 11 | W25 |
| web/src/shared/api/generated/** (validation-queue, zod/validation-queue, 15 model files) | generated | Orval |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/Questions/Question.cs | `SubmittedAt`, `Decisions`; `SubmittedAt` set in `Create` |
| api/Elmanhg.Domain/Questions/Question.Approval.cs | Replaced per plan (version check, difficulty, decision rows) |
| api/Elmanhg.Domain/Questions/Question.Editing.cs | `SubmittedAt` reset on content change and on `Resubmit` |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs, api/Elmanhg.Application/Exceptions/ErrorCodes.cs | 3 domain and 8 application codes |
| api/Elmanhg.Application/DependencyInjection.cs | `QuestionValidationOptions` with `ValidateOnStart` |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | 3 DbSets, decision mapping, `ConfigureReviewSessions`, global filters |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | Repository registration |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | Regenerated |
| api/Elmanhg.Api/Resources/Messages.{ar,en}.resx | 11 keys after `QUESTION_RETIRED` |
| api/Elmanhg.Api/appsettings.example.json | `QuestionValidation` section |
| api/openapi/v1.json | Regenerated by build (deterministic, verified by rebuilding) |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | 5 `QuestionValidation:*` settings |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `twelfth` migration |
| api/Elmanhg.Tests/Builders/QuestionBuilder.cs, Integration/Content/QuestionTestData.cs | Pass `question.Version`; `Include(Decisions)`; `SetSubmittedAtAsync` |
| api/Elmanhg.Tests/Domain/Questions/{QuestionApproval, QuestionRejection, QuestionRetirement, QuestionServabilityEvents}Tests.cs, Application/Features/Questions/GetQuestions/GetQuestionsFilterTests.cs | Mechanical call-site updates only; new tests 1–7 appended to the first two (plus a private `Assignment()` helper in QuestionApprovalTests) |
| api/Elmanhg.Tests/Integration/Content/ServableQuestionCountEndpointTests.cs | Tests 75–76 plus `SeedAssignedLessonAsync` and `TeacherClientAsync` |
| postman/elmanhg.postman_collection.json | `QuestionValidation` folder after `Questions`; variables `reviewSessionId`, `rejectQuestionId`, `bulkQuestionId` |
| web/src/routes/teacher/index.tsx, web/src/routeTree.gen.ts | Queue route with `validateSearch`; tree regenerated |
| web/src/features/questions/index.ts | 3 exports |
| web/src/features/questions/api/questionOptions.ts, api/questionValues.ts | New constants; `QuestionContentSource` |
| web/src/features/questions/i18n/{en,ar}.json | `validation` object |
| web/src/shared/i18n/{en,ar}.json | 11 new codes plus `SUBJECT_OUT_OF_SCOPE` and `QUESTION_RETIRED` (`QUESTION_NOT_REJECTED` was already there) |
| web/src/features/shell/components/AppShell.test.tsx | `beforeEach(() => server.use(...getValidationQueueMock()))` only |
| docs/PRD.md §8.1, docs/question-schemas.md, docs/audit-log.md, docs/claude-design-prompt.md §4, docs/prototype.md item 9 | As planned |
| web/orval.config.ts | **Not in plan**. See Deviations |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Existing code touched does not list `web/orval.config.ts` | Orval's zod client generates `GetValidationQueue` query params that do not compile (`zod.stringFormat('int32', …).default(1)` gives TS2769). The repo already hit this and opted `GetAuditLogs` and `GetQuestions` out. | Added `GetValidationQueue: { zod: { generate: { query: false } } }`, the same one-line pattern as those two. |
| W-i18n key list | I needed a few more keys: lesson-state labels, the decision section heading, and the history version and empty-actor text. Also, ICU `#` renders Arabic-Indic digits in `ar`, while Decision 20 requires Latin digits. | Added `detail.lessonStates.{Draft,Published,Archived}`, `decision.title`, `history.versionValue` and `history.noActor`. The count strings (`selectedCount`, `confirmBody`, `bulkApproved`, `ageOption`) use a `{formattedCount}` value formatted with `formatNumber(…, 'latin')` in place of `#`. |
| W23: reset the selection "via `key` on the list section" | Doing that needs a second component, which is not in the file list. | Selection state is stored as `{ key: JSON(search), ids }`, and ids are ignored when the key no longer matches. It resets on filter or page change without a new file. It is also cleared after a bulk approve. |
| Test 49: ValidationTestData holds 3 helpers | The three endpoint test classes also share admin-client, problem-code, items and request-body helpers. | Added `AdminClientAsync`, `ReadCodeAsync`, `ReadItemsAsync`, `McqRequest` to the same helper file. |
| Postman: bulk approve is "preceded in description by opening it" | A folder must run top to bottom (PROGRESS). | Kept the description, and also added a "Record opening for bulk approve" request (the same endpoint, with `{{bulkQuestionId}}`) before bulk approve. "Get validation queue" sets `questionId`, `rejectQuestionId` and `bulkQuestionId` from the first three items. |

## Build & test
Docker was down at the start. I ran `bash scripts/cloud-setup.sh`, and Docker and .NET 10.0.401 came up. No `appsettings.json` exists, so these runs match CI.
- `dotnet build api/ -c Release`: Build succeeded, with only the 9 existing core-libraries warnings. A second build left the `v1.json` hash unchanged.
- `git status --porcelain api/openapi`: ` M api/openapi/v1.json` (regenerated, not yet committed).
- `dotnet test api/ -c Release`: Passed! total 1070, failed 0, succeeded 1070, skipped 0. The QuestionValidation integration namespace alone is 34/34.
- `dotnet list api/ package --vulnerable --include-transitive`: no vulnerable packages in any project.
- `dotnet tool restore`, then `dotnet ef migrations has-pending-model-changes … --configuration Release --no-build`: "No changes have been made to the model since the last migration."
- `dotnet format api/Elmanhg.slnx --verify-no-changes --exclude api/core-libraries`: no output (clean).
- Guard grep (DateTime.Now/UtcNow, .Result, .Wait(), new HttpClient(, FromSqlRaw, async void) over the diff and new files: nothing.
- web: `npm run gen:tokens` produced no drift. `gen:api` output is committed-ready and a re-run is stable. `typecheck`, `lint` and `format:check` are clean. `npm test -- --run`: 70 files, 391 passed. `npm test -- --run --coverage` (CI form) passes the thresholds. `npm run build`: built.
- ai/: not touched.

## Notes for review
- These are the first Application handlers to use `Microsoft.EntityFrameworkCore` `.Include` through `include:`. The plan prescribes it, it is the same pattern as Morabh `ActivateBranchesHandler`, and the reference comes in transitively via Core.Identity.
- `useQuestionDecision` handles failures in the mutation `onError`: a toast, plus a detail refetch on `QUESTION_VERSION_CHANGED`. `approve`/`reject` then `.catch(() => undefined)` the `mutateAsync` promise so the RHF `Form` does not also write a root error. `useBulkApprove.approve` resolves on `onSettled` for the same reason.
- `useRecordOpening` uses `useEffect` plus `useEffectEvent` (React 19.2+) so the effect deps are exactly `[reviewSessionId, questionId, version]`, as Decision 2 requires. It is a side effect of viewing, not a data fetch.
- `AppDbContext.cs` is now about 197 lines, which the plan anticipated. `GetValidationQueueHandler` is 90 lines.
- The review-history table has no mobile card fallback. It sits inside an `overflow-x-auto` card, as the admin tables do.
- `RejectQuestionValidatorTests.Validate_BlankReason_FailsQuestionRejectionReasonRequired` is a `[Theory]` over `null` and whitespace.
- The `PutQuestion_ContentEdit_ResetsSubmittedAt` integration test (58) asserts `SubmittedAt >= testStart`, with `testStart` taken before seeding.
