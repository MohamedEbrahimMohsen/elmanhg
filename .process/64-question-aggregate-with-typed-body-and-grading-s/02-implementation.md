# Implementation — [E3.S1] Question aggregate with typed body and grading spec (#64)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Questions/QuestionType.cs | 3 | D-1 enum |
| api/Elmanhg.Domain/Questions/QuestionDifficulty.cs | 3 | D-2 enum |
| api/Elmanhg.Domain/Questions/QuestionValidationStatus.cs | 3 | D-3 enum |
| api/Elmanhg.Domain/Questions/QuestionContent.cs | 15 | D-4 content value object + `IsEquivalentTo` (semantic JSON) |
| api/Elmanhg.Domain/Questions/QuestionMetadata.cs | 3 | D-5 metadata value object |
| api/Elmanhg.Domain/Questions/Question.cs | 97 | D-6 aggregate, `Create`, `ApplyContent`/`ApplyMetadata`, objective guard |
| api/Elmanhg.Domain/Questions/Question.Editing.cs | 43 | D-7 `Update` (content vs metadata, version bump, revision, Approved→Pending) |
| api/Elmanhg.Domain/Questions/Question.Approval.cs | 28 | D-8 `Approve(TeacherSubject)` |
| api/Elmanhg.Domain/Questions/QuestionRevision.cs | 30 | D-9 revision entity + snapshot |
| api/Elmanhg.Domain/Questions/QuestionRevisionSnapshot.cs | 5 | D-10 snapshot record |
| api/Elmanhg.Domain/Questions/IQuestionRepository.cs | 9 | D-11 `AnyInLessonAsync`, `CountByLessonAsync` |
| api/Elmanhg.Domain/Questions/Schemas/QuestionJson.cs | 19 | D-12 canonical serializer options + `AreEquivalent` |
| api/Elmanhg.Domain/Questions/Schemas/ChoiceSchemas.cs | 9 | D-13 |
| api/Elmanhg.Domain/Questions/Schemas/TrueFalseSchemas.cs | 5 | D-14 |
| api/Elmanhg.Domain/Questions/Schemas/FillSchemas.cs | 9 | D-15 |
| api/Elmanhg.Domain/Questions/Schemas/ShortSchemas.cs | 9 | D-16 |
| api/Elmanhg.Application/Questions/Shared/QuestionFields.cs | 6 | A-1 |
| api/Elmanhg.Application/Questions/Shared/QuestionSchemaReader.cs | 70 | A-2 (only `catch (JsonException)` in the slice) |
| api/Elmanhg.Application/Questions/Shared/ChoiceQuestionRules.cs | 66 | A-3 mcq/multi validate + normalise |
| api/Elmanhg.Application/Questions/Shared/TrueFalseQuestionRules.cs | 28 | A-4 |
| api/Elmanhg.Application/Questions/Shared/FillQuestionRules.cs | 64 | A-5 (`[[id]]` placeholder) |
| api/Elmanhg.Application/Questions/Shared/ShortQuestionRules.cs | 48 | A-6 numeric/text |
| api/Elmanhg.Application/Questions/Shared/QuestionSchemaRules.cs | 34 | A-7 per-type dispatch (switch expressions) |
| api/Elmanhg.Application/Questions/Shared/QuestionFieldsValidator.cs | 47 | A-8 shared field validator |
| api/Elmanhg.Application/Questions/Shared/QuestionContentFactory.cs | 22 | A-9 |
| api/Elmanhg.Application/Questions/Shared/QuestionDetailResult.cs | 5 | A-10 |
| api/Elmanhg.Application/Questions/Shared/QuestionResultGenerator.cs | 18 | A-11 |
| api/Elmanhg.Application/Questions/CreateQuestion/CreateQuestionCommand.cs | 12 | A-12 (audited `Question.Create`) |
| api/Elmanhg.Application/Questions/CreateQuestion/CreateQuestionResult.cs | 8 | A-13 |
| api/Elmanhg.Application/Questions/CreateQuestion/CreateQuestionValidator.cs | 17 | A-14 |
| api/Elmanhg.Application/Questions/CreateQuestion/CreateQuestionHandler.cs | 43 | A-15 |
| api/Elmanhg.Application/Questions/UpdateQuestion/UpdateQuestionCommand.cs | 12 | A-16 (audited `Question.Update`) |
| api/Elmanhg.Application/Questions/UpdateQuestion/UpdateQuestionValidator.cs | 17 | A-17 |
| api/Elmanhg.Application/Questions/UpdateQuestion/UpdateQuestionHandler.cs | 39 | A-18 |
| api/Elmanhg.Application/Questions/GetQuestion/GetQuestionQuery.cs | 6 | A-19 |
| api/Elmanhg.Application/Questions/GetQuestion/GetQuestionValidator.cs | 13 | A-19 |
| api/Elmanhg.Application/Questions/GetQuestion/GetQuestionHandler.cs | 21 | A-19 |
| api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs | 24 | I2 |
| api/Elmanhg.Infrastructure/Migrations/20260928025344_AddQuestions.cs | 121 | I3 (generated; CreateTable x2 + CreateIndex x4 only) |
| api/Elmanhg.Infrastructure/Migrations/20260928025344_AddQuestions.Designer.cs | 1132 | I4 (generated) |
| api/Elmanhg.Api/Controllers/Questions/Requests.cs | 8 | P1 |
| api/Elmanhg.Api/Controllers/Questions/QuestionsController.cs | 45 | P2 (3 actions, `ContentManage`) |
| api/Elmanhg.Tests/Builders/QuestionBuilder.cs | 64 | T1 |
| api/Elmanhg.Tests/Domain/Questions/QuestionTests.cs | 224 | T2 (D1–D16) |
| api/Elmanhg.Tests/Domain/Questions/QuestionApprovalTests.cs | 71 | T3 (A1–A5) |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/ChoiceQuestionRulesTests.cs | 124 | T4 (C1–C15) |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/TrueFalseQuestionRulesTests.cs | 33 | T5 (T1–T4) |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/FillQuestionRulesTests.cs | 100 | T6 (F1–F12) |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/ShortQuestionRulesTests.cs | 79 | T7 (S1–S10) |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/QuestionFieldsValidatorTests.cs | 115 | T8 (V1–V15) |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/QuestionContentFactoryTests.cs | 30 | T9 (QF1–QF2) |
| api/Elmanhg.Tests/Application/Features/Questions/CreateQuestion/CreateQuestionHandlerTests.cs | 95 | T10 (H1–H5) |
| api/Elmanhg.Tests/Application/Features/Questions/CreateQuestion/CreateQuestionValidatorTests.cs | 35 | T11 (CV1–CV3) |
| api/Elmanhg.Tests/Application/Features/Questions/UpdateQuestion/UpdateQuestionHandlerTests.cs | 106 | T12 (U1–U6) |
| api/Elmanhg.Tests/Application/Features/Questions/UpdateQuestion/UpdateQuestionValidatorTests.cs | 35 | T13 (UV1–UV3) |
| api/Elmanhg.Tests/Application/Features/Questions/GetQuestion/GetQuestionHandlerTests.cs | 42 | T14 (G1–G2) |
| api/Elmanhg.Tests/Application/Features/Questions/GetQuestion/GetQuestionValidatorTests.cs | 22 | T15 (GV1–GV2) |
| api/Elmanhg.Tests/Integration/Content/QuestionTestData.cs | 39 | T16 |
| api/Elmanhg.Tests/Integration/Content/QuestionsEndpointTests.cs | 296 | T17 (I1–I14c) |
| docs/question-schemas.md | 137 | DOC1 |
| web/src/shared/api/generated/** (10 new: `questions/`, `zod/questions/`, 7 model files) | — | `npm run gen:api` output, not hand-edited |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/Lessons/Lesson.cs | `Delete(bool hasQuestions, Guid deletedBy)`; Published check first, then `LESSON_HAS_QUESTIONS` |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | `LessonHasQuestions` under CONTENT; new QUESTIONS group (4 codes) |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | QUESTIONS group, 32 constants in table order, after LESSONS |
| api/Elmanhg.Application/Shared/Options/ContentOptions.cs | 10 D23 caps with `[Range(1, int.MaxValue)]` |
| api/Elmanhg.Application/Lessons/Shared/LessonResult.cs | `+ int QuestionCount` |
| api/Elmanhg.Application/Lessons/Shared/LessonResultGenerator.cs | `Generate(Lesson lesson, int questionCount)` |
| api/Elmanhg.Application/Lessons/GetLessons/GetLessonsHandler.cs | injects `IQuestionRepository`, fills counts via `CountByLessonAsync` |
| api/Elmanhg.Application/Lessons/DeleteLesson/DeleteLessonHandler.cs | injects `IQuestionRepository`, `AnyInLessonAsync` → `Delete(hasQuestions, …)` |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | 2 DbSets, `ConfigureQuestions` (I1 verbatim), 2 soft-delete filter lines |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | `IQuestionRepository` registration |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated |
| api/Elmanhg.Api/Resources/Messages.en.resx, Messages.ar.resx | 37 keys each (plan text verbatim) |
| api/Elmanhg.Api/appsettings.example.json | 10 D23 keys in the `Content` line; also mirrored into the local gitignored `appsettings.json` |
| api/openapi/v1.json | regenerated by the build (`/api/questions`, `/api/questions/{questionId}`, `LessonResult.questionCount`) |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | 10 in-memory `Content:Question*` keys |
| api/Elmanhg.Tests/Domain/Lessons/LessonLifecycleTests.cs | 3 `Delete(_actor)` → `Delete(false, _actor)`; + L1 |
| api/Elmanhg.Tests/Application/Features/Lessons/DeleteLesson/DeleteLessonHandlerTests.cs | `IQuestionRepository` substitute + ctor; + L2 |
| api/Elmanhg.Tests/Application/Features/Lessons/GetLessons/GetLessonsHandlerTests.cs | `IQuestionRepository` substitute, count stub, expected results with 3 / 0 |
| api/Elmanhg.Tests/Integration/Content/LessonManagementEndpointTests.cs | + I15 |
| api/Elmanhg.Tests/Integration/Content/LessonsEndpointTests.cs | + I16 |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `_AddQuestions` appended to the applied-migrations list (accepted pattern, not in the plan's table) |
| postman/elmanhg.postman_collection.json | `questionId` variable; "Questions" folder (4 requests, inherits collection Bearer); Lessons folder description notes the new delete guard |
| docs/PRD.md | §5.2, §5.3, §6, §15 as specified |
| docs/audit-log.md | 2 rows, `Question` in audited entities, replaced sentence |
| docs/rich-text.md | line 3 and Storage sentence |
| docs/claude-design-prompt.md | §4 admin content bullet |
| web/src/features/content/components/LessonItem.tsx | question-count caption under the name row |
| web/src/features/content/components/LessonItem.test.tsx | fixtures `questionCount` 2/0/1; + W1–W3 |
| web/src/features/content/components/UnitLessons.test.tsx | fixtures `questionCount: 0` only |
| web/src/features/content/i18n/en.json, ar.json | `lessons.questionCount` plural |
| web/src/shared/i18n/en.json, ar.json | `errors.LESSON_HAS_QUESTIONS` |
| web/src/shared/api/generated/** (6 modified) | regenerated |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| A-2 lists `QuestionSchemaReader`'s members exhaustively | The four rule classes each need "add code if condition" | Added one extra public helper `QuestionSchemaReader.AddIf(List<string>, bool, string)` instead of duplicating a private helper in 4 files. No signature in the plan changed |
| A-3/A-5 rules dereference `o.Id` / `o.Text` / `entry.AcceptedAnswers` | A JSON `null` array element (`"options":[null]`) deserialises to a null record and would throw a 500 | Rules use `x?.Id` / `x?.Text`, so a null element fails as the matching 422 code (invalid id / text required / accepted answers invalid) |
| S10 asserts `{"acceptedAnswers":["ماء"],"unifyLetterVariants":true}` | `QuestionJson.SerializerOptions` (verbatim from D-12) uses the default encoder, which escapes non-ASCII as `\uXXXX`, so the stored text is not byte-equal | Kept the plan's options; S10 compares with `JsonNode.DeepEquals` (semantic equality, same as D2). PostgreSQL `jsonb` stores the unescaped value anyway |
| "Modify only the files in Existing code touched" | The migration test lists every migration | Added `_AddQuestions` to `AppDbContextTests` (the orchestrator's accepted pattern) |

## Build & test
- `dotnet build api/` → `Build succeeded. 0 Error(s)`. The only warnings are the existing CS8618/CS8602 in vendored `core-libraries`, and none are in Elmanhg projects.
- `dotnet ef migrations add AddQuestions -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api` → `Done.` The generated migration has only CreateTable (Questions, QuestionRevisions) and CreateIndex (4: the 2 planned composites, the unique `(QuestionId, Version)` index, and the EF-generated FK index `IX_Questions_ObjectiveId`).
- `dotnet test api/ -c Release` (with `appsettings.json`) → `Test run summary: Passed! total: 663 failed: 0 succeeded: 663 skipped: 0`.
- Same run with `api/Elmanhg.Api/appsettings.json` moved aside → `Passed! total: 663 failed: 0 succeeded: 663 skipped: 0`. The file was restored afterwards and now contains the 10 new keys.
- `dotnet format api/Elmanhg.slnx --verify-no-changes` → exit 2. Every reported line is whitespace inside `api/core-libraries` (the known noise). No Elmanhg file is reported.
- `npm --prefix web run gen:api` → orval regenerated `api` and `apiZod`.
- `npm --prefix web run build` → `✓ built`. There is only the existing chunk-size warning.
- `npx tsc -b --noEmit` → exit 0. `npx eslint . --max-warnings=0` → exit 0.
- `npx prettier --check .` flags CRLF files, which is the known Windows noise. `npx prettier --check --end-of-line auto .` → "All matched files use Prettier code style!".
- `npx vitest run` (no coverage) → `Test Files 46 passed (46), Tests 240 passed (240)`.
- `npm --prefix web test -- --run --coverage` → `Tests 1 failed | 239 passed (240)`, failing the same way on two runs. The failing test is `RichTextEditor.test.tsx > inserts an inline formula without saving the lesson`, which times out at 5000 ms. I did not touch this file. The test passes alone, with and without coverage.
  - I checked it was already failing before this change. I made a temporary git worktree of HEAD (`d48e767`), linked in the same `node_modules`, and ran `npx vitest run --coverage` there: `Tests 1 failed | 236 passed (237)`, the same test failing the same way. The worktree was then removed.
  - Because the run fails, the coverage table and thresholds were not printed.
- Guard grep over the new question code: no `DateTime.Now/UtcNow`, `.Result`, `.Wait()`, `FromSqlRaw` or `async void`. The only `catch` is `catch (JsonException)` in `QuestionSchemaReader.TryRead`.

## Notes for review
- Files over ~100 lines because the plan requires the change there: `AppDbContext.cs` (132; `ConfigureQuestions` must live in the single context, and splitting it would mean creating an unplanned file), `Lesson.cs` (113), and the application `ErrorCodes.cs` (108; one flat class by rule).
- `Question.cs` has one comment, on `ApplyContent`. It records why fields are assigned only when they differ: EF and the audit diff must never see a formatting-only change.
- `QuestionFieldsValidator` is also picked up by `AddValidatorsFromAssembly` as `IValidator<QuestionFields>`. That is harmless, because no MediatR request is a `QuestionFields`.
- The integration test I10 depends on `RichTextSanitizer` returning `<p>2 + 2 = ?</p>`, `<p>Add the numbers.</p>`, `3` and `4` unchanged. It does today (the test passes). If the sanitiser starts changing that markup, the re-save becomes a content edit.
- Enum values in `body`/`gradingSpec` (`answerKind`, `toleranceMode`) are read case-insensitively. Because the plan's options keep the converter's default, integers are also accepted: `"answerKind": 5` deserialises to an undefined value and is then treated as text. I did not tighten this (it would mean `allowIntegerValues: false`, a change to D-12's verbatim options). It is worth a decision in #65 or #66.
- OpenAPI types `body` and `gradingSpec` as `JsonElement` = `{}` (any). Orval generates `JsonElement` as an open type for #65 to narrow.
- I briefly ran `git add -N .` to diff untracked files, then reset the index with `git reset -q`. The index is back to its original state (only the orchestrator's unstaged `.gitignore` change plus my working-tree changes). Nothing was committed.
