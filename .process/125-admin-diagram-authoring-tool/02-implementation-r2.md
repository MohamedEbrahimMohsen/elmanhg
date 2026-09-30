# Implementation r2 — [E16.S1] Admin diagram authoring tool (#125)

## Findings addressed
| # | What I changed | Where |
|---|---|---|
| 1a | The DragDrop-only questions strings now live in a lazy `questionsDiagram` namespace. That covers `editor.dragDrop.*`, the 11 diagram `editor.errors.*`, `preview.showKey`, `preview.dragDropGradingHint`, `view.diagram*` and the list/order separators. The 21 admin-only codes (`QUESTION_TYPE_NOT_GRADABLE` + 20 `QUESTION_DIAGRAM_*`) left `shared/i18n/*.json`, which is now identical to HEAD, and are deep-merged into `common` with `overwrite=false`. `registerDiagramLocales()` does both and follows the dashboard pattern. It runs at module load in `DragDropFields`, `DragDropPreview`, `DiagramKeyLegend` and `QuestionPreviewPanel`, all admin or teacher chunks. It is not called from `app/i18n.ts`. The Diagram* components use `useTranslation('questionsDiagram')`. `dragDropRules` issues `questionsDiagram:editor.errors.*`. **Fallback:** every place that renders a server code (toasts, TextField/TextAreaField/FormRootError, DiagramImageField, QuestionPreviewPanel) already uses `[code, 'errors.UNHANDLED_EXCEPTION']`, so a code seen before the editor chunk loads shows the generic message and never a raw key. `types.DragDrop` stays eager because the type labels use it. | `web/src/features/questions/diagramLocales.ts` (new); `i18n/diagram.{ar,en}.json`, `i18n/diagramErrors.{ar,en}.json` (new); `i18n/{ar,en}.json`; `shared/i18n/{ar,en}.json`; the 8 Diagram*/DragDrop* components; `QuestionPreviewPanel.tsx`; `schemas/dragDropRules.ts:9` |
| 1b | Moved `dragDropBodySchema` / `dragDropSpecSchema` to `schemas/dragDropContentSchemas.ts`. Only `api/dragDropValues.ts` imports it. `questionContentSchemas.ts` is back to its HEAD content. | `schemas/dragDropContentSchemas.ts` (new), `api/dragDropValues.ts:4` |
| 1 (doc) | `docs/performance.md`: one sentence added to the "Admin-only strings on demand" bullet. That bullet is in §4, not §3 as the review says. | `docs/performance.md:60` |
| NB-1 | Key bound to the question's lesson. `DiagramImageKey.BelongsToLesson(key, lessonId)` (valid AND prefix `question-diagrams/{lessonId}/`) is enforced by `QuestionBodyMedia.EnsureLessonMedia(fields, lessonId)`. That method throws 422 `QUESTION_DIAGRAM_IMAGE_INVALID`, an existing code, so no resx change was needed. It is called in the Create, Update and Resubmit handlers before `CreateContent`. `docs/question-schemas.md` §DragDrop and the validation table are updated. | `DiagramImageKey.cs:12`, `QuestionBodyMedia.cs:24-30`, `CreateQuestionHandler.cs:34`, `UpdateQuestionHandler.cs:33`, `ResubmitQuestionHandler.cs:32`, `docs/question-schemas.md:108,173` |
| NB-2 | Added `DragDropQuestionRulesTests.Normalize_DragDropImageUrlSentInBody_IsDropped`. It sends `image.url` next to a valid key and asserts the stored body has no `url` and DeepEquals the canonical body. | `DragDropQuestionRulesTests.cs` |

## Files created
| Path | Purpose |
|---|---|
| web/src/features/questions/diagramLocales.ts | lazy `questionsDiagram` + `common:errors` registration |
| web/src/features/questions/i18n/diagram.ar.json / diagram.en.json | diagram namespace copy |
| web/src/features/questions/i18n/diagramErrors.ar.json / diagramErrors.en.json | 21 admin-only error codes |
| web/src/features/questions/schemas/dragDropContentSchemas.ts | DragDrop zod schemas (admin only) |
| web/src/features/questions/diagramLocales.test.ts | bundle-placement guard: eager common has no diagram codes; the merge resolves and does not overwrite; ar/en key parity |

## Tests added (api)
- CreateQuestionHandlerTests: `Handle_DragDropImageInLesson_AddsQuestion`, `Handle_DragDropImageFromAnotherLesson_ThrowsQuestionDiagramImageInvalid`
- UpdateQuestionHandlerTests and ResubmitQuestionHandlerTests: `Handle_DragDropImageFromAnotherLesson_ThrowsQuestionDiagramImageInvalid`
- DiagramImageKeyTests: `BelongsToLesson_KeyInLessonFolder_ReturnsTrue`, `BelongsToLesson_OtherLessonOrInvalidKey_ReturnsFalse` (3 cases)
- DragDropQuestionEndpointTests: `Post_DiagramKeyFromAnotherLesson_Returns422QuestionDiagramImageInvalid`
- Fixtures: `QuestionBuilder.DragDropImageLessonId` + `ForLesson(json, lessonId)`. The integration create/put helpers and their assertions now use keys under the seeded lesson.

## Deviations
| Plan / review said | Reality | What I did |
|---|---|---|
| Register only from DragDropFields, DragDropPreview, DiagramKeyLegend | `QuestionPreviewPanel` renders `preview.showKey` and `preview.dragDropGradingHint` itself | It also calls `registerDiagramLocales()`. The call is idempotent and the panel is admin-only. |
| Plan: exact test list | Rework adds tests for NB-1 and NB-2, plus `diagramLocales.test.ts` to guard bundle placement, which the review flagged as unguarded | Added them as listed above |
| docs/performance.md §3 | The lazy-strings bullet is in §4 | Edited §4 |

## Build & test
- **API** (CI parity: `appsettings.json` moved to the scratchpad, then restored): `dotnet test api/ -c Release` → **total 4079, failed 0, succeeded 4079**. That is 4069 plus 10 new. The Release build had 0 warnings.
- **Web:**
  - `typecheck` clean; `lint` 0 warnings.
  - `prettier --check . --end-of-line auto` passes: "All matched files use Prettier code style!". Plain `--check` flags 819 files on this Windows checkout (autocrlf CRLF) for line endings only; that is pre-existing and unrelated.
  - `vitest run`: **226 files, 1303 tests passed**.
  - `build` passes.
- **perf:budget.** The baseline is HEAD, built in a temporary detached worktree. Values are exact KB brotli:

| Page | HEAD (pre-change) | r1 (before rework) | r2 (now) | Δ vs HEAD | perf:budget |
|---|---|---|---|---|---|
| entry | 202.82 | 204.99 | 202.96 | +0.14 | 203/210 |
| landing | 211.23 | 213.42 | 211.38 | +0.15 | 212/220 |
| lesson | 231.99 | 234.17 | 232.15 | +0.16 | 233/240 |
| quiz | 251.95 | 254.20 | 252.10 | +0.15 | 253/255 |
| admin-dashboard | 220.86 | 223.03 | 220.96 | +0.10 | 221/230 |

- All student routes are within ±0.3 KB of HEAD. The remaining ~0.15 KB is the `DragDrop` enum value, the `types.DragDrop` label and the `DragDrop` switch arms.
- The lesson route prints 233 rather than 232 only because 232.15 rounds up.
- `grep` on `dist/assets`: `zoneOverCapacity` and `QUESTION_DIAGRAM_ZONES_OVERLAP` appear only in `DragDropPreview-*.js` and `QuestionEditorHeader-*.js`, and `zoneId` only in `DragDropPreview-*.js`. None are in `index-*.js` or `QuestionView-*.js`.

## Notes for review
- The lesson-binding check lives in the handlers, not the validator, because Update and Resubmit only learn the lesson after loading the question. It throws `ApplicationValidationCoreException`, the same 422 path `EssayQuestionRules.Normalize` uses. Import ignores DragDrop, and grade-draft rejects DragDrop in its validator, so neither needs the check.
- Postman is unchanged. "Create drag-and-drop question" already uses `{{diagramImageKey}}` from the upload to the same `{{lessonId}}`.
- Running `prettier --write` on `src/features/questions` re-wrote the working-copy line endings of unrelated files to LF. Git (autocrlf) shows no content diff for them.

## Merge of origin/main (#122 MathSteps, #106 user admin) — 2026-09-30

28 conflicts resolved keeping both question types:
- `QuestionType` = `{ Mcq, Multi, TrueFalse, Fill, Short, Essay, MathSteps, DragDrop }`: main's `MathSteps` keeps ordinal 6, `DragDrop` takes 7 (the column is stored as a string, so data is stable either way). OpenAPI/Orval enum order matches.
- `QuestionSchemaRules`, web `answerKey`, `questionValues`, `studentQuestion`, `TypeSpecificFields`, `questionEditorSchema`, `quiz/correctAnswer`, `quiz/quizItem`, `questionErrorFields`, `questionOptions`: both arms / both field sets kept.
- Servability unchanged: `QuestionCondition` excludes `DragDrop`; `ServedTypes` includes `MathSteps` and not `DragDrop`. `ServableQuestionSpecificationTests` keeps main's MathSteps-servable test and #125's DragDrop-unservable and `ServedTypes_EveryTypeExceptDragDrop` tests. `QuestionBuilder` has both `DragDrop()` and `MathSteps()`.
- i18n: `questions` `types` has both labels (no duplicate keys, both JSON files validated); DragDrop editor strings stay in the lazy `questionsDiagram` namespace.
- Docs (`question-schemas`, `question-import`, `claude-design-prompt`, `performance`) describe both types; servable text says every type except DragDrop incl. MathSteps.
- Generated: `api/openapi/v1.json` from `dotnet build api/ -c Release`, Orval from `npm --prefix web run gen:api`.

Verification: `dotnet test api/ -c Release` (appsettings.json moved aside) 4321/4321 passed; web typecheck, lint clean; prettier (touched folders) clean; vitest 248 files / 1390 tests passed; build ok; perf:budget all within budget (quiz 246/255 KB, lesson 235/240 KB, entry 206/210 KB). `ai/` had no conflicts and pytest was not rerun.
