# Implementation — Servable rule (#67, E3.S4)

## Files created
| Path | Lines | Purpose |
|------|-------|---------|
| `api/Elmanhg.Domain/Questions/ServableQuestionSpecification.cs` | 28 | D1: the single servable definition (two expressions, compiled copies, `IsSatisfiedBy`, `WhereServable`) |
| `api/Elmanhg.Domain/Questions/Question.Retirement.cs` | 29 | D2: `Retire(Guid retiredBy)`, `EnsureNotRetired()` |
| `api/Elmanhg.Domain/Questions/QuestionApproved.cs` | 5 | D3: domain event |
| `api/Elmanhg.Domain/Questions/QuestionRejected.cs` | 5 | D4: domain event |
| `api/Elmanhg.Domain/Questions/QuestionRetired.cs` | 5 | D5: domain event |
| `api/Elmanhg.Domain/Questions/QuestionReturnedToPending.cs` | 5 | D6: domain event |
| `api/Elmanhg.Application/Questions/RetireQuestion/RetireQuestionCommand.cs` | 11 | A1: auditable `Question.Retire` command |
| `api/Elmanhg.Application/Questions/RetireQuestion/RetireQuestionValidator.cs` | 13 | A2 |
| `api/Elmanhg.Application/Questions/RetireQuestion/RetireQuestionHandler.cs` | 28 | A3 (mirrors `ArchiveLessonHandler`) |
| `api/Elmanhg.Application/Questions/GetServableQuestionCount/GetServableQuestionCountQuery.cs` | 5 | A4 |
| `api/Elmanhg.Application/Questions/GetServableQuestionCount/ServableQuestionCountResult.cs` | 3 | A5 |
| `api/Elmanhg.Application/Questions/GetServableQuestionCount/GetServableQuestionCountHandler.cs` | 23 | A6: `IMemoryCache` read-through with `ServableCountCacheSeconds` TTL |
| `api/Elmanhg.Application/Questions/Shared/ServableQuestionCountCache.cs` | 6 | A7: cache key constant |
| `api/Elmanhg.Application/Events/ServableQuestionCountInvalidation/ServableQuestionCountInvalidationHandler.cs` | 51 | A8: 7 `INotificationHandler<T>`, removes the key only |
| `api/Elmanhg.Infrastructure/Migrations/20260928070029_AddQuestionRetiredAt.cs` (+ `.Designer.cs`, 1198) | 29 | M1: only `AddColumn<DateTimeOffset>("RetiredAt", "Questions", timestamptz, nullable)` / `DropColumn` |
| `api/Elmanhg.Tests/Domain/Questions/ServableQuestionSpecificationTests.cs` | 107 | T1 (rows 1–9) |
| `api/Elmanhg.Tests/Domain/Questions/QuestionRetirementTests.cs` | 115 | T2 (rows 10–16) |
| `api/Elmanhg.Tests/Domain/Questions/QuestionServabilityEventsTests.cs` | 91 | T3 (rows 17–22) |
| `api/Elmanhg.Tests/Application/Features/Questions/RetireQuestion/RetireQuestionHandlerTests.cs` | 76 | T4 (rows 23–26) |
| `api/Elmanhg.Tests/Application/Features/Questions/RetireQuestion/RetireQuestionValidatorTests.cs` | 26 | T5 (rows 27–28) |
| `api/Elmanhg.Tests/Application/Features/Questions/GetServableQuestionCount/GetServableQuestionCountHandlerTests.cs` | 60 | T6 (rows 29–31), real `MemoryCache`, disposed |
| `api/Elmanhg.Tests/Application/Features/Events/ServableQuestionCountInvalidationHandlerTests.cs` | 73 | T7 (rows 32–33), `[MemberData]` over 7 events, switch-expression dispatch |
| `api/Elmanhg.Tests/Integration/Content/ServableCountCollection.cs` | 7 | T8: `DisableParallelization = true` collection |
| `api/Elmanhg.Tests/Integration/Content/ServableQuestionCountEndpointTests.cs` | 129 | T9 (rows 41–46), delta assertions after seeding |
| `api/Elmanhg.Tests/Integration/Content/QuestionRetireEndpointTests.cs` | 142 | T10 (rows 47–53) |
| `api/Elmanhg.Tests/Integration/Persistence/ServableQuestionSpecificationPersistenceTests.cs` | 62 | T11 (rows 39–40), proves EF translation |
| `web/src/shared/api/generated/model/servableQuestionCountResult.ts` | 11 | Orval output (generated) |

## Files modified
| Path | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/Question.cs` | `RetiredAt` after `ImportBatchId`; `IsRetired =>` after `CurrentContent` |
| `api/Elmanhg.Domain/Questions/Question.Approval.cs` | `QuestionApproved` / `QuestionRejected` raised after stamps; `EnsureNotRetired()` first in `EnsureValidatorCanDecide` |
| `api/Elmanhg.Domain/Questions/Question.Editing.cs` | `EnsureNotRetired()` first in `Update` and `Resubmit`; `QuestionReturnedToPending` raised in the Approved→Pending branch |
| `api/Elmanhg.Domain/Questions/IQuestionRepository.cs` | `CountServableAsync`, `CountServableByLessonAsync` |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | `QuestionAlreadyRetired`, `QuestionRetired` |
| `api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs` | Two methods via `_dbSet.WhereServable(_context.Set<Lesson>())` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated (`RetiredAt` only) |
| `api/Elmanhg.Application/Shared/Options/ContentOptions.cs` | `[Range(1, 3600)] ServableCountCacheSeconds = 60` |
| `api/Elmanhg.Application/DependencyInjection.cs` | `services.AddMemoryCache();` before options |
| `api/Elmanhg.Application/Elmanhg.Application.csproj`, `api/Directory.Packages.props` | `Microsoft.Extensions.Caching.Memory` 10.0.5 |
| `api/Elmanhg.Application/Questions/Shared/QuestionListItemResult.cs`, `QuestionDetailResult.cs`, `QuestionResultGenerator.cs` | `RetiredAt`, `IsServable` per plan |
| `api/Elmanhg.Application/Questions/GetQuestions/GetQuestionsHandler.cs` | `lessonsById`, private static `Generate(...)` calling `ServableQuestionSpecification.IsSatisfiedBy`; filter unchanged |
| `api/Elmanhg.Application/Lessons/Shared/LessonResult.cs`, `LessonResultGenerator.cs`, `GetLessons/GetLessonsHandler.cs` | `ServableQuestionCount` via `CountServableByLessonAsync`, `lessonIds` hoisted |
| `api/Elmanhg.Api/Controllers/Questions/QuestionsController.cs` | `GetServableQuestionCount` (`[AllowAnonymous]`, before `GetQuestion`), `RetireQuestion` (`ContentManage`, after `ResubmitQuestion`); 99 lines |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Two keys each, after `QUESTION_REJECTION_REASON_REQUIRED` |
| `api/Elmanhg.Api/appsettings.example.json` | `"ServableCountCacheSeconds": 60` |
| `api/openapi/v1.json` | Regenerated by build |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `Content:ServableCountCacheSeconds = 60` |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | `eleventh => …_AddQuestionRetiredAt` |
| `api/Elmanhg.Tests/Builders/QuestionBuilder.cs` | `Retired()` |
| `api/Elmanhg.Tests/Integration/Content/QuestionTestData.cs` | `SeedRetiredQuestionAsync` |
| `api/Elmanhg.Tests/Application/Features/Lessons/GetLessons/GetLessonsHandlerTests.cs` | Stub `CountServableByLessonAsync`; expected 7th arg 2 / 0 |
| `api/Elmanhg.Tests/Application/Features/Questions/GetQuestions/GetQuestionsHandlerTests.cs` | Rows 35–37 |
| `api/Elmanhg.Tests/Application/Features/Questions/GetQuestion/GetQuestionHandlerTests.cs` | Row 38 |
| `postman/elmanhg.postman_collection.json` | "Get servable question count" (`noauth`) and "Retire question" (inherits admin bearer) after "Resubmit question" |
| `web/src/shared/api/generated/**` | Regenerated by `gen:api` |
| `web/src/features/content/components/LessonItem.test.tsx`, `UnitLessons.test.tsx` | `servableQuestionCount: 0` (then Prettier re-wrapped long lines) |
| `web/src/features/questions/pages/QuestionListPage.test.tsx` | `retiredAt: null, isServable: false` |
| `web/src/features/questions/pages/QuestionEditorPage.test.tsx`, `web/src/features/questions/api/questionValues.test.ts` | `retiredAt: null` |
| `web/src/features/questions/pages/NewQuestionPage.test.tsx` | `retiredAt: null` in the mocked `QuestionDetailResult` (see Deviations) |
| `docs/PRD.md` §5.3 | Retire bullet replaced verbatim |
| `docs/question-schemas.md` | Guard order in "Validation status"; new `## Retirement` and `## Servable` sections |
| `docs/audit-log.md` | `RetireQuestion` row |

## Deviations
| Plan said | Reality | What I did |
|-----------|---------|------------|
| Web fixture updates in 5 files (`LessonItem`, `UnitLessons`, `QuestionListPage`, `QuestionEditorPage`, `questionValues`) | `web/src/features/questions/pages/NewQuestionPage.test.tsx` also passes a literal `QuestionDetailResult` to `getGetQuestionMockHandler`, so `tsc -b` failed on the now-required `retiredAt` | Added `retiredAt: null` to that literal (one line, fixture only) |
| T9 helper named `SeedUnitAsync` (subject + unit) | The tests need a lesson in a given state each time | Private helper is `SeedLessonAsync(LessonState)`: it seeds subject, unit and lesson via `ContentTestData.SeedSubjectAsync` / `SeedUnitAsync` / `SeedLessonInStateAsync`. It is private, and the contract is the same |

## Build & test
All run on this container with no `appsettings.json` present (CI parity):
- `dotnet build api/ -c Release`: `Build succeeded.` Only the existing vendored core-libraries warnings appear.
- `git status --porcelain api/openapi`: ` M api/openapi/v1.json` (regenerated; to be committed with the change, and has no drift after that).
- `dotnet test api/ -c Release`: `Test run summary: Passed! total: 938 failed: 0 succeeded: 938 skipped: 0`. The new classes filtered alone gave `total: 50, succeeded: 50`.
- `dotnet list api/ package --vulnerable --include-transitive`: "has no vulnerable packages" for every project.
- `dotnet tool restore` then `dotnet ef migrations has-pending-model-changes … --configuration Release --no-build` (with the design connection string): `No changes have been made to the model since the last migration.`
- `dotnet format api/Elmanhg.slnx --verify-no-changes --exclude api/core-libraries`: clean, no output.
- web: `gen:tokens` produced no diff. `gen:api` regenerated 9 files plus 1 new one. `typecheck`, `lint` (`--max-warnings=0`) and `format:check` all passed: "All matched files use Prettier code style!". `test -- --run` reported `Test Files 60 passed (60) · Tests 344 passed (344)`. `build` reported `✓ built in 4.84s`.
- ai/: not touched, so it was not run.

## Notes for review
- `docs/question-schemas.md` said Approve checks the assignment first and then Pending. The code has always checked Pending first. I rewrote the sentence to give the real order: `QUESTION_RETIRED` → `QUESTION_NOT_PENDING` → `QUESTION_VALIDATOR_NOT_ASSIGNED`.
- `Resubmit` calls `EnsureNotRetired()` itself and then calls `Update`, which calls it again. This does no harm. The plan's order holds: `QUESTION_RETIRED` comes before `QUESTION_NOT_REJECTED`.
- `ContentTestData.SeedLessonInStateAsync` clears domain events, so seeding a Published lesson does not invalidate the cache. Every delta test seeds an approved question afterwards (which raises `QuestionApproved`), then reads the baseline, so the baseline is always fresh.
- The Postman request "Get servable question count" asserts `count >= 0` only, because the value is global. "Retire question" asserts 200. It runs after "Resubmit question", and nothing later in the folder uses `{{questionId}}`.
- `GetQuestionsHandler.Generate` ignores the `bool` returned by `TryGetValue` and relies on the `out` variable being null. No analyser flagged this.
