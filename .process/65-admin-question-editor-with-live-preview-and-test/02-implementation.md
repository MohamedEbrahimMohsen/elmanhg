# Implementation — [E3.S2] Admin question editor with live preview and test grader (#65)

## Files created

### api
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Identity/IUserRepository.cs | 7 | D-1, copied from Morabh `Morabh.Domain/Users/IUserRepository.cs` |
| api/Elmanhg.Domain/Questions/Schemas/AnswerSchemas.cs | 13 | D-2 answer records per type |
| api/Elmanhg.Domain/Questions/Grading/GradeOutcome.cs | 3 | D-3 |
| api/Elmanhg.Domain/Questions/Grading/QuestionGrade.cs | 19 | D-4 rounding (2 / 4 decimals, away from zero) |
| api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs | 79 | D-5 PRD §6.2 normaliser, char loop, no regex |
| api/Elmanhg.Domain/Questions/Grading/ChoiceGrader.cs | 37 | D-6 Mcq / TrueFalse / Multi |
| api/Elmanhg.Domain/Questions/Grading/TextGrader.cs | 68 | D-7 Fill / Short (numeric + text) |
| api/Elmanhg.Domain/Questions/Grading/QuestionGrader.cs | 43 | D-8 entry point `Grade(type, gradingSpec, maxScore, answer)` |
| api/Elmanhg.Application/Questions/Shared/QuestionListItemResult.cs | 3 | A-1 |
| api/Elmanhg.Application/Questions/GetQuestions/GetQuestionsQuery.cs | 8 | A-2 |
| api/Elmanhg.Application/Questions/GetQuestions/GetQuestionsValidator.cs | 29 | A-3 |
| api/Elmanhg.Application/Questions/GetQuestions/GetQuestionsFilter.cs | 26 | A-4 |
| api/Elmanhg.Application/Questions/GetQuestions/GetQuestionsHandler.cs | 41 | A-5 (one page query + one lessons query + one users query) |
| api/Elmanhg.Application/Questions/ResubmitQuestion/ResubmitQuestionCommand.cs | 12 | A-6 `IAuditableCommand` `Question.Resubmit` |
| api/Elmanhg.Application/Questions/ResubmitQuestion/ResubmitQuestionValidator.cs | 17 | A-7 |
| api/Elmanhg.Application/Questions/ResubmitQuestion/ResubmitQuestionHandler.cs | 39 | A-8 |
| api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftQuery.cs | 7 | A-9 |
| api/Elmanhg.Application/Questions/GradeQuestionDraft/QuestionGradeResult.cs | 3 | A-10 |
| api/Elmanhg.Application/Questions/GradeQuestionDraft/QuestionAnswerRules.cs | 22 | A-11 |
| api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftValidator.cs | 20 | A-12 |
| api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftHandler.cs | 16 | A-13 (not async, no repository) |
| api/Elmanhg.Application/Teachers/Shared/TeacherResult.cs | 3 | A-14 |
| api/Elmanhg.Application/Teachers/GetTeachers/GetTeachersQuery.cs | 6 | A-15 |
| api/Elmanhg.Application/Teachers/GetTeachers/GetTeachersHandler.cs | 16 | A-16 (Morabh `ListUsersQueryHandler` shape) |
| api/Elmanhg.Infrastructure/Identity/UserRepository.cs | 9 | I-1, copied from Morabh `Morabh.Infrastructure/Users/UserRepository.cs` |
| api/Elmanhg.Infrastructure/Migrations/20260928043922_AddQuestionRejectionReason.cs | 28 | I-2: only `AddColumn<string>("RejectionReason", "Questions", "text", nullable)` / `DropColumn` |
| api/Elmanhg.Infrastructure/Migrations/20260928043922_AddQuestionRejectionReason.Designer.cs | 1135 | I-3 (generated) |
| api/Elmanhg.Tests/Domain/Questions/QuestionRejectionTests.cs | 158 | T-1 (R1–R11) |
| api/Elmanhg.Tests/Domain/Questions/Grading/AnswerNormalizerTests.cs | 55 | T-2 (N1–N8) |
| api/Elmanhg.Tests/Domain/Questions/Grading/ChoiceGraderTests.cs | 66 | T-3 (C1–C9) |
| api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs | 101 | T-4 (X1–X11) |
| api/Elmanhg.Tests/Domain/Questions/Grading/QuestionGraderTests.cs | 51 | T-5 (G1–G5) |
| api/Elmanhg.Tests/Application/Features/Questions/GetQuestions/GetQuestionsValidatorTests.cs | 66 | T-6 (V1–V7) |
| api/Elmanhg.Tests/Application/Features/Questions/GetQuestions/GetQuestionsFilterTests.cs | 87 | T-7 (F1–F7) |
| api/Elmanhg.Tests/Application/Features/Questions/GetQuestions/GetQuestionsHandlerTests.cs | 62 | T-8 (H1–H2) |
| api/Elmanhg.Tests/Application/Features/Questions/ResubmitQuestion/ResubmitQuestionValidatorTests.cs | 35 | T-9 (RV1–RV3) |
| api/Elmanhg.Tests/Application/Features/Questions/ResubmitQuestion/ResubmitQuestionHandlerTests.cs | 93 | T-10 (RH1–RH5) |
| api/Elmanhg.Tests/Application/Features/Questions/GradeQuestionDraft/GradeQuestionDraftValidatorTests.cs | 60 | T-11 (GV1–GV6) |
| api/Elmanhg.Tests/Application/Features/Questions/GradeQuestionDraft/GradeQuestionDraftHandlerTests.cs | 45 | T-12 (GH1–GH2) |
| api/Elmanhg.Tests/Application/Features/Teachers/GetTeachers/GetTeachersHandlerTests.cs | 25 | T-13 (TH1) |
| api/Elmanhg.Tests/Integration/Content/QuestionListEndpointTests.cs | 178 | T-14 (L1–L8) |
| api/Elmanhg.Tests/Integration/Content/QuestionResubmitEndpointTests.cs | 130 | T-15 (S1–S5) |
| api/Elmanhg.Tests/Integration/Content/QuestionGradeDraftEndpointTests.cs | 108 | T-16 (DG1–DG6) |
| api/Elmanhg.Tests/Integration/Teachers/TeachersEndpointTests.cs | 54 | T-17 (TE1–TE3) |

### web
| Path | Lines | Purpose |
|---|---|---|
| web/src/features/questions/index.ts | 6 | W-R1 barrel |
| web/src/features/questions/locales.ts | 4 | W-R2 |
| web/src/features/questions/i18n/en.json, ar.json | — | W-R3, every key in the plan's i18n table |
| web/src/features/questions/api/questionOptions.ts | 45 | W-A1 |
| web/src/features/questions/api/richTextContent.ts | 8 | W-A2 |
| web/src/features/questions/api/questionValues.ts | 192 | W-A3 |
| web/src/features/questions/api/studentQuestion.ts | 51 | W-A4 |
| web/src/features/questions/api/questionListParams.ts | 49 | W-A5 |
| web/src/features/questions/api/stemExcerpt.ts | 6 | W-A6 |
| web/src/features/questions/api/questionErrorFields.ts | 24 | W-A7 |
| web/src/features/questions/schemas/questionEditorSchema.ts | 89 | W-S1 |
| web/src/features/questions/schemas/questionContentSchemas.ts | 34 | W-S2 |
| web/src/features/questions/schemas/questionListSearchSchema.ts | 17 | W-S3 |
| web/src/features/questions/schemas/questionListFiltersSchema.ts | 19 | W-S4 |
| web/src/features/questions/hooks/useQuestionList.ts | 10 | W-H1 |
| web/src/features/questions/hooks/useQuestionListSearch.ts | 40 | W-H2 |
| web/src/features/questions/hooks/useQuestionSave.ts | 54 | W-H3 |
| web/src/features/questions/hooks/useTestGrade.ts | 33 | W-H4 |
| web/src/features/questions/hooks/useQuestionImageUpload.ts | 7 | W-H5 |
| web/src/features/questions/components/SelectField.tsx | 70 | W-C1 |
| web/src/features/questions/components/TextAreaField.tsx | 53 | W-C2 |
| web/src/features/questions/components/CheckboxField.tsx | 29 | W-C3 |
| web/src/features/questions/components/QuestionRichTextField.tsx | 60 | W-C4 |
| web/src/features/questions/components/ChoiceOptionsField.tsx | 56 | W-C5 |
| web/src/features/questions/components/ChoiceOptionRow.tsx | 68 | W-C6 |
| web/src/features/questions/components/FillBlanksField.tsx | 65 | W-C7 |
| web/src/features/questions/components/ShortAnswerFields.tsx | 44 | W-C8 |
| web/src/features/questions/components/TypeSpecificFields.tsx | 34 | W-C9 |
| web/src/features/questions/components/QuestionMetadataFields.tsx | 42 | W-C10 |
| web/src/features/questions/components/QuestionEditorForm.tsx | 71 | W-C11 |
| web/src/features/questions/components/QuestionEditorHeader.tsx | 58 | W-C12 |
| web/src/features/questions/components/QuestionView.tsx | 35 | W-C13 (exported from the barrel for #76) |
| web/src/features/questions/components/ChoiceAnswerInputs.tsx | 70 | W-C14 |
| web/src/features/questions/components/TextAnswerInputs.tsx | 53 | W-C15 |
| web/src/features/questions/components/QuestionPreviewPanel.tsx | 48 | W-C16 |
| web/src/features/questions/components/GradeResultPanel.tsx | 51 | W-C17 |
| web/src/features/questions/components/QuestionStatusBadge.tsx | 26 | W-C18 |
| web/src/features/questions/components/QuestionListFilters.tsx | 76 | W-C19 |
| web/src/features/questions/components/QuestionTable.tsx | 48 | W-C20 |
| web/src/features/questions/components/QuestionRow.tsx | 41 | W-C21 |
| web/src/features/questions/components/QuestionListEmptyState.tsx | 24 | W-C22 |
| web/src/features/questions/pages/QuestionListPage.tsx | 81 | W-P1 |
| web/src/features/questions/pages/QuestionEditorPage.tsx | 54 | W-P2 |
| web/src/features/questions/pages/NewQuestionPage.tsx | 36 | W-P3 |
| web/src/shared/components/Pagination.tsx | 45 | W-X1 (moved from audit) |
| web/src/routes/admin/question.$questionId.tsx | 11 | W-X2 |
| web/src/routes/admin/question.new.$lessonId.tsx | 11 | W-X3 |
| web/src/features/questions/schemas/questionEditorSchema.test.ts | 119 | WT-1 (WE1–WE14) |
| web/src/features/questions/schemas/questionListSearchSchema.test.ts | 38 | WT-2 (WS1–WS2) |
| web/src/features/questions/schemas/questionListFiltersSchema.test.ts | 26 | WT-3 (WF1–WF3) |
| web/src/features/questions/api/questionValues.test.ts | 149 | WT-4 (WM1–WM7) |
| web/src/features/questions/api/studentQuestion.test.ts | 59 | WT-5 (WQ1–WQ3) |
| web/src/features/questions/api/questionListParams.test.ts | 36 | WT-6 (WP1–WP4) |
| web/src/features/questions/api/stemExcerpt.test.ts | 15 | WT-7 (WX1–WX2) |
| web/src/features/questions/components/QuestionView.test.tsx | 108 | WT-8 (WV1–WV7) |
| web/src/features/questions/pages/QuestionListPage.test.tsx | 192 | WT-9 (WL1–WL9) |
| web/src/features/questions/pages/QuestionEditorPage.test.tsx | 230 | WT-10 (WE-P1–WE-P9) |
| web/src/features/questions/pages/NewQuestionPage.test.tsx | 150 | WT-11 (WN1–WN5) |
| web/src/shared/components/Pagination.test.tsx | 20 | WT-12 (WPG1) |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/Questions/Question.cs | `RejectionReason` after `ValidatedAt` |
| api/Elmanhg.Domain/Questions/Question.Approval.cs | `EnsureValidatorCanDecide` extracted; `Reject(TeacherSubject, string)` added; `Approve` behaviour unchanged |
| api/Elmanhg.Domain/Questions/Question.Editing.cs | `Resubmit(type, content, metadata, lesson, resubmittedBy)` |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | `QuestionNotRejected`, `QuestionRejectionReasonRequired` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | 6 new question codes |
| api/Elmanhg.Application/Shared/Options/ContentOptions.cs | `QuestionListMaxPageSize`, `QuestionFilterMaxLength` (`[Range(1, int.MaxValue)]`) |
| api/Elmanhg.Application/Questions/Shared/QuestionDetailResult.cs | `string? RejectionReason` appended |
| api/Elmanhg.Application/Questions/Shared/QuestionResultGenerator.cs | detail passes reason; `GenerateListItem` added |
| api/Elmanhg.Api/Controllers/Questions/QuestionsController.cs | `GetQuestions`, `ResubmitQuestion`, `GradeQuestionDraft` actions (ContentManage) |
| api/Elmanhg.Api/Controllers/Questions/Requests.cs | `GradeQuestionDraftRequest` |
| api/Elmanhg.Api/Controllers/Teachers/TeachersController.cs | `GetTeachers` (UsersManage) |
| api/Elmanhg.Api/Program.cs | `ConfigureHttpJsonOptions(... JsonStringEnumConverter)` after the MVC `AddJsonOptions` line (both pipelines now configured, SKILL §8.6) |
| api/Elmanhg.Api/Resources/Messages.en.resx, Messages.ar.resx | 8 new keys, texts from the plan |
| api/Elmanhg.Api/appsettings.example.json | 2 new Content keys on the same line (no local appsettings.json exists in this container) |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | `IUserRepository` registered before `ISubjectRepository` |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated (+3 lines) |
| api/openapi/v1.json | regenerated by build (string enums, 4 new operations, `rejectionReason`) |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | 2 new Content settings |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | ninth migration entry |
| api/Elmanhg.Tests/Integration/OpenApi/OpenApiEndpointTests.cs | O1 added (see Deviations) |
| api/Elmanhg.Tests/Application/Features/Questions/GetQuestion/GetQuestionHandlerTests.cs | G-GQ3 added |
| api/Elmanhg.Tests/Builders/QuestionBuilder.cs | `Teacher`, `Rejected(reason)`, static `McqFields(stem)`; `Approved()` uses `Teacher` |
| api/Elmanhg.Tests/Integration/Content/QuestionTestData.cs | `SeedRejectedQuestionAsync` |
| postman/elmanhg.postman_collection.json | "List teachers" first in Teachers; "List questions", "Grade question draft", "Resubmit question" after "Update question"; folder description line |
| docs/question-schemas.md | line 3 graders; Versioning sentence; 2 Validation status bullets; new "Answer shapes" and "Grading" sections before "Changing a schema" |
| docs/PRD.md | §5.3 resubmit bullet; §6 pointer line |
| docs/audit-log.md | `ResubmitQuestion` row |
| docs/claude-design-prompt.md | §4 questions line (DOC1); lesson-editor line gains the question links |
| docs/rich-text.md | Images bullet for the question editor |
| web/orval.config.ts | `GetQuestions: { zod: { generate: { query: false } } }` — the plan's conditional; the generated zod file did fail `tsc` without it |
| web/src/app/i18n.ts | `questions` namespace registered |
| web/src/routes/admin/questions.tsx | real route with `validateSearch` |
| web/src/routeTree.gen.ts | regenerated |
| web/src/features/content/index.ts | exports `RichTextEditor`, `ContentErrorState`, `ContentListSkeleton` |
| web/src/features/content/components/RichTextEditor.tsx | optional `onUploadImage`, `compact` prop; image dialog only when upload is available |
| web/src/features/content/components/RichTextToolbar.tsx | optional `onOpenImage`; image tool only when defined |
| web/src/features/content/pages/LessonEditorPage.tsx | "New question" / "Lesson questions" links |
| web/src/features/content/pages/LessonEditorPage.test.tsx | W-LE1 added |
| web/src/features/content/i18n/en.json, ar.json | `lessonEditor.newQuestion`, `lessonEditor.viewQuestions` |
| web/src/features/audit/pages/AuditLogPage.tsx | uses shared `Pagination` |
| web/src/features/audit/components/AuditLogPagination.tsx | **deleted** |
| web/src/features/audit/i18n/en.json, ar.json | `pagination` removed |
| web/src/shared/i18n/en.json, ar.json | `pagination` (same strings); 44 `errors.QUESTION_*` copied from the resx files |
| web/src/shared/api/generated/** | regenerated by `npm run gen:api` |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| O1: `components.schemas.QuestionType.enum` = `["Mcq","Multi","TrueFalse","Fill","Short"]` | The .NET 10 OpenAPI generator appends `null` to every enum that is only referenced as nullable (`QuestionType?` in every request), so the emitted list is `["Mcq",...,"Short",null]` | O1 asserts no numeric members and that the **string** members equal the five names in order; `QuestionValidationStatus` contains "Rejected" as planned. I did not add a schema transformer to strip `null` (not in the plan) |
| W-A1/W-A3 signatures use `QuestionType` / `QuestionDifficulty` (`toQuestionType(value): QuestionType`, `emptyQuestionValues(type: QuestionType)`) | Because of the `null` enum member, Orval now generates `QuestionType = 'Mcq' \| ... \| null` (same for difficulty/status). A nullable type cannot feed `z.enum(questionTypes)` form values | `questionOptions.ts` also exports `EditorQuestionType` / `EditorQuestionDifficulty` (the non-null unions); `toQuestionType`/`toQuestionDifficulty` return those, and `emptyQuestionValues(type: QuestionValues['type'])`. The `satisfies readonly QuestionType[]` checks are kept |
| W-P2: "Either pending → skeleton" listed before the error states | `useGetLesson('')` is disabled until the question loads, so on a question error the lesson query stays pending forever and the skeleton would hide the error | Error checks (question, then lesson) run before the pending check |
| W-A2/W-A6: `(body.textContent ?? '')` | typescript-eslint `no-unnecessary-condition` fails: `body.textContent` is typed `string` | Dropped the `?? ''` |
| W-A3 `nextOptionId` (unspecified body) | `[...optionIdAlphabet]` fails `no-misused-spread` | `for...of` loop over the alphabet |
| D-5 / D-7 constants as literal characters (`'ـ'`, `'ً'`, `'٫'`, `'−'` …) | Several are invisible combining marks, easy to corrupt | Same named constants, values written as `'ـ'`-style escapes; the WHY comments are as planned |
| WL6 asserts "the last request URL has `status=Rejected&teacherId=…&minVersion=2&rejectionReason=unit&pageNumber=1`" | Orval serialises params in object order (`pageNumber`, `pageSize`, then filters) | The test asserts each search param of the last request individually |

## Build & test
All run in this container (no gitignored `api/Elmanhg.Api/appsettings.json` present, so it matches CI):

- `dotnet build api/ -c Release` → `0 Error(s)`; the only warnings are the pre-existing 9 in `core-libraries` (first full build); incremental rebuild `0 Warning(s)`.
- `git status --porcelain api/openapi` → ` M api/openapi/v1.json` (regenerated, commit-ready; a rebuild produces no further change).
- `dotnet test api/ -c Release` → `Test run summary: Passed! total: 774 failed: 0 succeeded: 774 skipped: 0`.
- `dotnet ef migrations has-pending-model-changes ... --configuration Release --no-build` (with `ConnectionStrings__DbConnectionString=Host=localhost;Database=elmanhg_design;Username=design`) → `No changes have been made to the model since the last migration.`
- `dotnet format api/Elmanhg.slnx --verify-no-changes --no-restore` → no output outside `core-libraries`.
- web: `npm run gen:tokens` OK (no diff); `npm run gen:api` OK (generated diff is the expected new/changed client); `npm run typecheck` exit 0; `npm run lint` exit 0; `npm run format:check` → `All matched files use Prettier code style!`; `npm test -- --run` → `Test Files 58 passed (58) · Tests 325 passed (325)`; `npm run build` → `✓ built`.
- Guard grep over the `.cs` diff for `DateTime.Now/UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`, `async void`, `?? throw`, `catch` → nothing.
- `ai/` not touched; not run.

## Notes for review
- `Program.cs` now calls `ConfigureHttpJsonOptions` **in addition to** the existing MVC `AddJsonOptions`; SKILL §8.6's DON'T is "configure one pipeline and assume the other inherits it", which this does not do. The emitted JSON was checked through O1 and the generated client.
- `postman/elmanhg.postman_collection.json` was edited as text to keep its existing layout; the web Prettier config is not applied to it (running it reformats unrelated parts of the file). It parses as JSON.
- `questionValues.ts` (192 lines) imports the content schemas as `import * as contentSchemas` to stay under the 200-line cap with the per-type readers inside.
- X5 (`GradeFill_EmptyAnswer_DoesNotMatch`) uses an accepted answer of `" "` so that the "empty never matches" rule, not a plain mismatch, is what makes it score 0.
- F5 builds the approved and rejected questions from one builder by calling `Approve`/`Reject` directly, so all three share `builder.Teacher`.
- S1 sends the update body with an ignored `lessonId` field (the shared `McqRequest` helper, copied per the plan).
- The editor's version badge uses the neutral badge style (`bg-soft text-text-muted`); `color.v2` is reserved for the "v2" feature badge.
- Integration tests seed a rejected question without saving a `TeacherSubject`, as the plan specifies. That is fine for #65 but not for #68.
- The `AuditLogPagination.tsx` deletion shows in the working tree as an unstaged deletion.
