# Implementation — [E14.S1] Essay question authoring with rubric (#117)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/Questions/Schemas/EssaySchemas.cs` | 9 | `EssayBody`, `RubricLevel`, `RubricCriterion`, `EssayGradingSpec` records |
| `api/Elmanhg.Application/Questions/Shared/EssayQuestionRules.cs` | 53 | Essay `Validate` (body/spec read, maxWords, model answers) and `Normalize` (trim, sort levels, drop blank description, sanitise model answers) |
| `api/Elmanhg.Application/Questions/Shared/EssayRubricRules.cs` | 45 | `AddErrors` for the 9 rubric rules, `IsFullScale`, `IsTooLong` |
| `web/src/features/questions/api/essayValues.ts` | 75 | `emptyCriterion`, `nextCriterionId`, `readEssay`, `toEssayContent`, `rubricTotalPoints`, `countWords` |
| `web/src/features/questions/schemas/essayRules.ts` | 68 | `addEssayIssues` (client rubric/word-limit/model-answer rules) |
| `web/src/features/questions/components/EssayFields.tsx` | 22 | Word limit + rubric + model answers |
| `web/src/features/questions/components/RubricCriteriaField.tsx` | 51 | Criteria field array, total-points hint, add/remove, root error |
| `web/src/features/questions/components/RubricCriterionCard.tsx` | 47 | One criterion (title, description, points, levels) |
| `web/src/features/questions/components/RubricLevelsField.tsx` | 73 | Level rows, add/remove within 2..6, level error |
| `web/src/features/questions/components/ModelAnswersField.tsx` | 63 | 1..3 rich-text model answers |
| `web/src/features/questions/components/EssayAnswerInput.tsx` | 42 | Student essay textarea with live word count |
| `web/src/features/questions/components/EssayRubricView.tsx` | 57 | Rubric + model answers on the teacher validation page |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/EssayQuestionRulesTests.cs` | 217 | A1–A24 |
| `api/Elmanhg.Tests/Application/Features/ExamBlueprints/Shared/ServableTypeCountsTests.cs` | 20 | A28 |
| `api/Elmanhg.Tests/Integration/Content/EssayQuestionEndpointTests.cs` | 140 | I1–I5 |
| `api/Elmanhg.Tests/Integration/QuestionValidation/EssayValidationEndpointTests.cs` | 83 | I6–I8 |
| `web/src/features/questions/api/essayValues.test.ts` | 96 | W1–W6 |
| `web/src/features/questions/pages/NewEssayQuestion.test.tsx` | 181 | W20–W24 |
| `web/src/features/questions/pages/ValidationEssayQuestion.test.tsx` | 106 | W25–W27 |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Questions/QuestionType.cs` | `Essay` appended |
| `api/Elmanhg.Domain/Questions/ServableQuestionSpecification.cs` | `&& x.Type != QuestionType.Essay` with the #119 comment; `ServedTypes` |
| `api/Elmanhg.Application/Questions/Shared/QuestionSchemaRules.cs` | Essay arms in `Validate` and `Normalize` |
| `api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftValidator.cs` | `QUESTION_TYPE_NOT_GRADABLE` rule; answer rule skips Essay |
| `api/Elmanhg.Application/Questions/Shared/Import/QuestionImportColumns.cs` | `Types` (5 types); `TypeForSheet` iterates it |
| `api/Elmanhg.Application/Questions/Shared/Import/QuestionImportTemplate.cs` | Private `Types` removed; uses `Columns.Types` |
| `api/Elmanhg.Application/ExamBlueprints/Shared/ServableTypeCounts.cs` | `ToResults` iterates `ServedTypes` |
| `api/Elmanhg.Application/Shared/Options/ContentOptions.cs` | 7 caps with code defaults |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | 14 codes |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | 14 entries each |
| `api/Elmanhg.Api/appsettings.example.json` | 7 `Content` keys |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | 7 `Content:` settings |
| `api/Elmanhg.Tests/Builders/QuestionBuilder.cs` | `Essay()`, `EssayContent()`, `EssayFields()`, `EssaySpecJson` |
| `api/Elmanhg.Tests/Domain/Questions/ServableQuestionSpecificationTests.cs` | D1, D2 |
| `api/Elmanhg.Tests/Domain/Questions/Grading/QuestionGraderTests.cs` | D3 |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/QuestionFieldsValidatorTests.cs` | A25 |
| `api/Elmanhg.Tests/Application/Features/Questions/GradeQuestionDraft/GradeQuestionDraftValidatorTests.cs` | A26 |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/Import/QuestionImportColumnsTests.cs` | A27 |
| `api/Elmanhg.Tests/Integration/OpenApi/OpenApiEndpointTests.cs` | **Not in plan** — see Deviations |
| `api/openapi/v1.json` | Regenerated (`QuestionType` gains `Essay`) |
| `postman/elmanhg.postman_collection.json` | "Create essay question" after "Create question" (stores `essayQuestionId`); `essayQuestionId` collection variable |
| `web/src/shared/api/generated/**` | Regenerated (`questionType.ts` + 4 zod files) |
| `web/src/features/questions/api/questionOptions.ts` | `questionTypes` + Essay, `servedQuestionTypes`, 6 essay constants |
| `web/src/features/questions/index.ts` | exports `servedQuestionTypes` |
| `web/src/features/blueprints/api/blueprintValues.ts`, `components/TypeCountsTable.tsx`, `schemas/examBlueprintSchema.ts` | iterate `servedQuestionTypes` |
| `web/src/features/questions/schemas/questionEditorSchema.ts` | `maxWords`, `criteria`, `modelAnswers`; `addEssayIssues` |
| `web/src/features/questions/schemas/questionContentSchemas.ts` | `essayBodySchema`, `essaySpecSchema` |
| `web/src/features/questions/api/questionValues.ts` | essay defaults, `readEssay`, `toEssayContent` |
| `web/src/features/questions/api/studentQuestion.ts` | `maxWords`; Essay payload `{text}` |
| `web/src/features/questions/api/answerKey.ts` | Essay arm |
| `web/src/features/questions/api/questionErrorFields.ts` | 13 essay codes mapped |
| `web/src/features/questions/components/TypeSpecificFields.tsx` | `EssayFields` |
| `web/src/features/questions/components/QuestionEditorForm.tsx` | "Essay (v2)" option label |
| `web/src/features/questions/components/QuestionRichTextField.tsx` | `modelAnswers.${number}.text` name |
| `web/src/features/questions/components/QuestionView.tsx` | `EssayAnswerInput` for Essay |
| `web/src/features/questions/components/QuestionPreviewPanel.tsx` | Essay: note instead of try-answer/alert/result |
| `web/src/features/questions/components/ValidationQuestionContent.tsx` | `EssayRubricView` for Essay |
| `web/src/features/quiz/api/quizItem.ts`, `correctAnswer.ts` | Essay arms |
| `web/src/features/questions/i18n/ar.json`, `en.json` | the 40 keys from the plan |
| `web/src/shared/i18n/ar.json`, `en.json` | 14 error codes (resx text) |
| web test files (add): `questionEditorSchema.test.ts` (W7–W13), `studentQuestion.test.ts` (W14), `answerKey.test.ts` (W15), `quizItem.test.ts` (W16), `correctAnswer.test.ts` (W17), `QuestionView.test.tsx` (W18), `blueprintValues.test.ts` (W19) | new `it`s only; `answerKey.test.ts` and `blueprintValues.test.ts` also gained an import |
| `docs/question-schemas.md` | Types, Essay shapes + score/scale prose, 13 rule rows + non-integer note, id format, Servable + `ServedTypes`, answer shapes, grading bullet, not importable |
| `docs/PRD.md` | §5.3 Servable row, §17 rule 1, §19 Q7 decided |
| `docs/exam-blueprints.md` | line 41 and line 74 |
| `docs/question-import.md` | Essay sheet ignored |
| `docs/claude-design-prompt.md` | §4 teacher detail + question editor lines; **plus** line 168 (see Deviations) |
| `docs/prototype.md` | essay rubric paragraph |
| `docs/content-retrieval.md`, `docs/mastery.md` | **Not in plan** — see Deviations |

Test counts: dotnet 3360 in total. New: 24 methods in `EssayQuestionRulesTests` (30 cases with theory rows), plus A25–A28, D1–D3 and I1–I8. Web 1033 in total. New: 27 tests (W1–W27).

`questionErrorFields` maps all nine `QUESTION_RUBRIC_*` codes to `criteria`. The plan said "eight", but its own error table lists nine: `QUESTION_RUBRIC_TEXT_TOO_LONG` is the ninth.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| "No existing test is edited or deleted." | `OpenApiEndpointTests.Get_OpenApiDocument_DescribesQuestionEnumsAsStrings` asserts `QuestionType` enum equals exactly the 5 v1 values; the plan's own API surface adds `Essay` to OpenAPI, so it failed (1 of 3360). | Updated the expected list to include `"Essay"` (one token; intentional behaviour change per dotnet skill §8.11). Nothing else in the test changed. |
| Docs list: only the named sections. | Three more docs state the servable definition without the essay exclusion: `docs/claude-design-prompt.md` §"business rules" line 168, `docs/content-retrieval.md` line 13, `docs/mastery.md` line 51. Leaving them is a docs-sync divergence. | Appended "not an essay (until #119)" to each. |
| `EssayRubricRules`: "any criterion with valid `Points p` and `!IsFullScale`" | "valid" is ambiguous (non-null vs in range). | Used in range (1..`QuestionRubricPointsMax`), so an out-of-range criterion reports `QUESTION_RUBRIC_POINTS_INVALID` without a cascading level-points code. |
| `EssayRubricView` model answer: `<div … aria-label>` | A bare `div` with `aria-label` has no role; axe/ARIA prohibit naming it. | Added `role="group"` (W25 queries it as a group; W27 axe passes). |
| `docs/prototype.md`: "after line 66" | Line 66 is the middle of the two-item v2 list. | Added the paragraph right after that list (after line 67). |
| New config keys also go in `docs/deployment.md` and `deploy/*.env.example` (orchestrator trap list) | No `Content:*` cap is listed in either today (all have code defaults or come from appsettings). | Did not add them; code defaults + `appsettings.example.json` + `ApiFactory` only. |

## Build & test
- `dotnet build` (api/): `0 Warning(s)`, `0 Error(s)`. `api/openapi/v1.json` regenerated (+`Essay`).
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside (restored afterwards): `total: 3360, failed: 0, succeeded: 3360, skipped: 0`, exit 0. (A first run failed only on the OpenAPI enum test above.)
- `npm --prefix web run gen:api`: regenerated; a second run produced no further diff.
- `npm run typecheck`: clean. `npm run lint` (`eslint . --max-warnings=0`): clean.
- `npm test -- --run`: `Test Files 176 passed (176)`, `Tests 1033 passed (1033)`.
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: "All matched files use Prettier code style!" (after `--write` on 3 files).
- `npm run build`: exit 0.
- Mutation checks, each seen failing then restored:
  - removed `&& x.Type != QuestionType.Essay` → D1 and I8 fail;
  - grade-draft rule neutralised → A26 and I5 fail;
  - `TypeForSheet` back to `Enum.GetValues` → A27 fails;
  - `ToResults` back to `Enum.GetValues` → A28 fails;
  - `IsFullScale` without `Contains(0)` → A18 `"1,4"` fails;
  - Normalize without level sort / blank-description drop → A23 and I1 fail;
  - web: preview essay branch → W24; `EssayRubricView` branch → W25–W27; `levelScale` 0-check → W10; `maxWords` range → W12; `servedQuestionTypes` with Essay → W19; word-limit display → W18.
- No `ai/` changes, so no pytest run.

## Notes for review
- The 422 body `code` is asserted with `Be(...)` (single code) in I2 and I5; the siblings use `Contain` for 422s.
- W22 checks required errors through `aria-invalid` and the accessible description on the title, both level descriptions and the model answer, not through a text count.
- Server rubric codes map to `criteria`, so they show once under the rubric legend and not on the failing field; this is the plan's mapping.
- `RubricLevelsField` sets both the levels' points and description with `TextField`. The level row is `flex items-start gap-2`, and only the description stretches.
- In `EssayRubricView`, `h3` holds `title` followed by `(n points)`. W25 matches the heading with `/Definition/`.
- `QuestionPreviewPanel` checks `values.type` (the form value). The essay note replaces the whole grade block.
