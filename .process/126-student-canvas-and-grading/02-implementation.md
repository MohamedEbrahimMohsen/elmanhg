# Implementation — [E16.S2] Student canvas and grading (#126)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/Questions/Grading/DragDropGrader.cs` | 103 | A1: per-item grader (decisions 2–5), private `KeyPlace` / `ZonePlacement`, `KeyPlaces`, `Resolve` |
| `api/Elmanhg.Application/Questions/Shared/DragDropAnswerRules.cs` | 27 | A2: `CanRead`, `Canonicalize`, `ExceedsLimits` |
| `api/Elmanhg.Tests/Domain/Questions/Grading/DragDropGraderTests.cs` | 103 | D1–D11 |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/DragDropAnswerRulesTests.cs` | 70 | P1–P7 |
| `api/Elmanhg.Tests/Application/Features/Sessions/Shared/SessionResultGeneratorTests.cs` | 45 | P15–P16 |
| `api/Elmanhg.Tests/Integration/Sessions/DragDropAnswerEndpointTests.cs` | 109 | I1–I5 |
| `api/Elmanhg.Tests/Integration/Exams/DragDropExamEndpointTests.cs` | 91 | I6–I8 |
| `web/src/features/questions/schemas/studentDiagramSchema.ts` | 29 | W1 |
| `web/src/features/questions/api/studentDiagram.ts` | 28 | W2 |
| `web/src/features/questions/api/diagramPlacement.ts` | 118 | W3 (+ exported `itemsIn`, `PlacementPayload`) |
| `web/src/features/questions/api/diagramReview.ts` | 45 | W4 |
| `web/src/features/questions/hooks/useDiagramAnswer.ts` | 102 | W5 (also owns the window Escape listener, see Deviations) |
| `web/src/features/questions/hooks/useDiagramDrag.ts` | 143 | W6 (+ exported `DiagramChipHandlers` type) |
| `web/src/features/questions/components/DiagramItemChip.tsx` | 66 | W7 |
| `web/src/features/questions/components/DiagramAnswerCanvas.tsx` | 85 | W8 |
| `web/src/features/questions/components/DiagramZoneRow.tsx` | 121 | W9 (component body ~95 lines; action table at module level) |
| `web/src/features/questions/components/DiagramZoneList.tsx` | 53 | W10 |
| `web/src/features/questions/components/DiagramItemBank.tsx` | 33 | W11 |
| `web/src/features/questions/components/DiagramDragGhost.tsx` | 18 | W12 |
| `web/src/features/questions/components/DragDropAnswerInput.tsx` | 116 | W13 (lazy) |
| `web/src/features/questions/components/DragDropCorrectAnswer.tsx` | 49 | W14 (lazy) |
| `web/src/features/questions/components/LazyDragDropCorrectAnswer.tsx` | 14 | W15 |
| `web/src/features/questions/diagramStudentLocales.ts` | 11 | W16 |
| `web/src/features/questions/i18n/diagramStudent.en.json` / `.ar.json` | 42 / 42 | W17 / W18, strings verbatim from the plan |
| `web/src/features/questions/api/diagramPlacement.test.ts` | 102 | T1–T12 |
| `web/src/features/questions/api/diagramReview.test.ts` | 35 | T13–T16 |
| `web/src/features/questions/schemas/studentDiagramSchema.test.ts` | 35 | T17–T19 |
| `web/src/features/questions/components/DragDropAnswerInput.test.tsx` | 213 | T20–T31 |
| `web/src/features/questions/components/DragDropCorrectAnswer.test.tsx` | 57 | T32–T34 |
| `web/src/features/questions/diagramStudentLocales.test.ts` | 12 | T35 |
| `web/src/features/quiz/api/quizItem.dragDrop.test.ts` | 42 | T38–T41 |
| `web/src/features/quiz/pages/QuizPage.dragDrop.test.tsx` | 112 | T43–T45 |
| `web/src/features/exam/pages/ExamPage.dragDrop.test.tsx` | 67 | T46–T47 |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Questions/ServableQuestionSpecification.cs` | Type clause and `#126` comment removed; `ServedTypes` = all 8 |
| `api/Elmanhg.Domain/Questions/Schemas/DragDropSchemas.cs` | `DiagramPlacement`, `DragDropAnswer` |
| `api/Elmanhg.Domain/Questions/Grading/GradeFeedbackKind.cs`, `GradeFeedback.cs`, `QuestionGrader.cs` | `PlacementTally` kind + factory; DragDrop arm |
| `api/Elmanhg.Application/Shared/Options/SessionsOptions.cs` | 3 DragDrop caps; `RequestAnswerMaxLength` includes the raw cap |
| `api/Elmanhg.Application/Questions/Shared/QuestionAnswerRules.cs` | DragDrop in `CanRead`, `Canonicalize`, `RawAnswerMaxLength`; `ExceedsLimits` is a switch |
| `api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftValidator.cs` | `QuestionTypeNotGradable` rule dropped; `CanRead` runs for DragDrop |
| `api/Elmanhg.Application/Questions/Shared/Grading/GradeFeedbackKeys.cs`, `GradeFeedbackText.cs` | `GRADE_FEEDBACK_PLACEMENT_TALLY` + right/wrong/total arm |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | `QuestionTypeNotGradable` deleted |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | `QUESTION_TYPE_NOT_GRADABLE` removed; `GRADE_FEEDBACK_PLACEMENT_TALLY` added (text from the plan) |
| `api/Elmanhg.Application/Sessions/Shared/SessionResultGenerator.cs` | `IFileStorage` appended; body = `QuestionBodyMedia.Resolve(...)` |
| `api/Elmanhg.Application/Exams/Shared/ExamSessionResultGenerator.cs`, `ExamSessionResultLoader.cs` | `IFileStorage` appended and passed through |
| 8 handlers (StartQuizSession, GetSession, FinishSession, SubmitAnswer, GetExamSession, StartUnitExam, StartMultiUnitExam, SubmitExam) | `IFileStorage fileStorage` appended last; passed to generator/loader |
| `api/Elmanhg.Api/appsettings.example.json`, `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | 3 `Sessions:DragDrop*` keys |
| `api/Elmanhg.Tests/Builders/SessionBuilder.cs` | `BuildWithDragDrop` |
| `api/Elmanhg.Tests/Integration/Content/QuestionTestData.cs` | `SeedDragDropQuestionAsync` (record `with`) |
| 12 handler test files (M1) | constructor call only: `Substitute.For<IFileStorage>()` appended (+ using) |
| `ServableQuestionSpecificationTests`, `QuestionGraderTests`, `QuestionAnswerRulesTests`, `GradeFeedbackTextTests`, `GradeQuestionDraftValidatorTests`, `GradeQuestionDraftHandlerTests`, `SubmitAnswerHandlerTests`, `GetSubjectExamBlueprintsHandlerTests`, `DragDropQuestionEndpointTests`, `DragDropValidationEndpointTests` | D12–D14, P8–P14, P17–P19, I9, I10 as planned |
| `SubmitAnswerValidatorTests`, `SaveExamAnswerValidatorTests` | **not in plan** — see Deviations |
| `web/src/features/questions/api/studentQuestion.ts` | `diagram`, `placements`, `diagramKey`; DragDrop payload |
| `web/src/features/questions/components/AnswerInputs.tsx` | lazy `DragDropAnswerInput` arm |
| `web/src/features/questions/components/QuestionPreviewPanel.tsx` | single return; student canvas + shared «جرّب الإجابة»; key checkbox only for DragDrop |
| `web/src/features/questions/components/DragDropPreview.tsx`, `ValidationQuestionContent.tsx` | key-only preview, `showKey` removed |
| `web/src/features/questions/schemas/dragDropContentSchemas.ts` | `dragDropSpecSchema = diagramKeySchema` |
| `web/src/features/questions/api/questionOptions.ts`, `index.ts` | `servedQuestionTypes` + `'DragDrop'`; barrel exports |
| `web/src/features/questions/i18n/diagram.{ar,en}.json`, `diagramErrors.{ar,en}.json` | removed `preview.dragDropGradingHint`, `view.diagramBank`, `QUESTION_TYPE_NOT_GRADABLE` |
| `web/src/features/quiz/api/quizItem.ts`, `correctAnswer.ts`, `components/CorrectAnswer.tsx` | as planned |
| `web/src/features/blueprints/api/blueprintValues.ts`, `schemas/examBlueprintSchema.ts` | DragDrop count |
| Web tests modified: `studentQuestion.test.ts` (T36, T37), `correctAnswer.test.ts` (T42), `NewDragDropQuestion.test.tsx` (T48 modified, T49 added), `blueprintValues.test.ts` (T50) | as planned |
| Docs: `PRD.md` (§5.3, §6 row + paragraph, §17 rule 1), `question-schemas.md`, `sessions.md`, `exams.md`, `mastery.md`, `exam-blueprints.md`, `performance.md` (quiz measured 248), `design-system.md` §5.13, `.claude/design-system.md`, `claude-design-prompt.md` §4 (quiz, exam, editor preview), `prototype.md` | per the Docs table |
| `docs/deployment.md` | **not in plan** — config row for the 3 new keys (PROGRESS convention) |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Only the listed test files change | `SubmitAnswerValidatorTests.Validate_AnswerOverMaxLength_FailsAttemptAnswerTooLong` and `SaveExamAnswerValidatorTests.Validate_AnswerTooLong_FailsWithAttemptAnswerTooLong` set every raw cap to 20/30; with `RequestAnswerMaxLength` now including `DragDropAnswerMaxLength` (default 4000, decision 9) they failed | Added `DragDropAnswerMaxLength = 20` / `= 30` to their `SessionsOptions` initialisers (same pattern as the existing Essay/MathSteps caps). Intentional behaviour change, no assertion touched. |
| I3 asserts the Arabic tally "(default language)" | The integration student client receives English unless `Accept-Language` is sent (the test failed with the English line) | Added `Accept-Language: ar` to that client, as `QuestionGradeDraftEndpointTests` does; assertion unchanged. |
| W13 root `<div onKeyDown={Escape → clear}>` | `jsx-a11y/no-static-element-interactions` (strict) rejects a key handler on a plain div | `useDiagramAnswer` registers a window `keydown` Escape listener only while an item is selected (clears it and announces). Drag-cancel Escape stays in `useDiagramDrag` as planned. T27 covers it. |
| W9 «ضعه هنا» with `<span className="sr-only"> {placeHereTarget}</span>` | dom-accessibility-api trims the separating space, so the name became "Place here(Nucleus in zone 1)" and T20 could not match | The button carries `aria-label="{placeHere} {placeHereTarget}"` (visible text starts the name, WCAG 2.5.3). Chip mark sr-only span kept as planned. |
| W2 items `{ id, text }` from all editor rows | The editor starts with one empty item row; an empty chip button has no accessible name and the editor page axe test failed (`button-name`) | `studentDiagramFromValues` drops items whose text is blank. |
| W9 zone row `<li data-zone-id>` | Tests need a way to address a zone row by role | Row `<li aria-labelledby>` its «المنطقة n» heading (tests query `listitem` "Zone 1"). |
| W10 renders `DiagramZoneRow`s | — | Implemented as specified; W10 receives the placements and maps rows itself (keeps W13 ≤ 120 lines). |
| Config keys only in the three listed files | PROGRESS "Conventions": new keys also go into `docs/deployment.md` (and `deploy/*.env.example`) | Added one row to `docs/deployment.md`; `deploy/api.env.example` has no `Sessions__*` keys, so left alone. |

## Build & test
- API, CI parity (`api/Elmanhg.Api/appsettings.json` moved aside, restored afterwards; Docker 29.6.2 up): `dotnet test Elmanhg.Tests -c Release` → `total: 4378, failed: 0, succeeded: 4378, skipped: 0` (first run had the 3 failures listed in Deviations; fixed and re-run green).
- `dotnet format --verify-no-changes`: only pre-existing findings in `core-libraries/*` and `Elmanhg.Tests/Builders/SubscriptionBuilder.cs` (untouched); none in changed files. Guard grep (DateTime/.Result/…) prints nothing.
- `api/openapi/v1.json`, Orval `src/shared/api/generated`, Postman: no diff after build (no contract change), so no regeneration.
- Web: `npx tsc -b` OK; `npx eslint . --max-warnings=0` OK; `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → "All matched files use Prettier code style!"; `npx vitest run` → `Test Files 260 passed (260), Tests 1445 passed (1445)`; `npm run build` exit 0.
- `npm run perf:budget`: baseline on this branch before changes `quiz 246/255`; after `quiz 248/255 KB ok` (entry 206, landing 215, lesson 236, admin-dashboard 225, admin-users 258, teacher-home 253 — all ok, no budget raised). `DragDropAnswerInput` and `DragDropCorrectAnswer` are dynamic entries in the manifest; `diagramStudent` strings live in their shared lazy chunk; no `questionsDiagram` / `dragDropValues` / `dragDropContentSchemas` in the quiz critical path.
- `ai/` untouched, not run.

Mutation checks (each mutant applied, tests run, file restored and byte-compared):
- `DragDropGrader` (D1–D12): killed — wrong not counted (D5, D6, D8), order ignored (D7, D9), item dedupe removed (D9), zone dedupe removed (D10), unknown zone kept (D3), unanswered check removed (D2, D3), floor removed (D6), full-credit shortcut removed (D1), zone equality dropped (D9, D11). Survived (equivalent under validated keys): key duplicate "last wins" instead of `TryAdd`, and the `keyPlaces.Count == 0` guard (a saved key always places ≥ 1 item — `QUESTION_DIAGRAM_KEY_EMPTY`).
- `DragDropAnswerRules` (P1–P7): all 6 mutants killed (null id / null zone accepted, empty placements kept, item cap removed, both caps `>`→`>=`).
- `diagramPlacement.ts`: killed — closed end edge (T2, T4), border not closed (T3), point rounded (T4), outside-canvas point accepted (T6), capacity ignored (T8, T23). "No hundredths rounding" initially survived T4; I added one assertion to T4 (zone x 0.01 w 20.1, point 20.11 → null), which kills it.

## Notes for review
- Drop mapping follows decision 16 literally (bounds rounded to hundredths, point unrounded ×100). A consequence: a point typed exactly on an end edge whose float ×100 lands just below (e.g. zone x 0 w 2.01, point 2.01 → 200.99999…) maps into that zone. Irrelevant for real pointer drops; flagging it in case the reviewer wants the point rounded too (that would break T4's 30.099 case).
- `SessionResultGeneratorTests` / `SubmitAnswerHandlerTests` DragDrop cases pass a revision from a separately built DragDrop question (same content, version 1): `BuildWithDragDrop` returns only the `Session`, and `SubmitAnswerHandler` picks the revision by version.
- `diagramStudent:key.title` exists as the plan lists it but nothing renders it (the quiz feedback panel already titles the block «الإجابة الصحيحة»).
- Dropping a chip on the bank calls `returnToBank` only when the item is placed (avoids a misleading "returned" announcement for a bank item).
- `itemsIn` guards with `Object.hasOwn` so a zone id such as `constructor` (valid under the id format) never reads `Object.prototype`.
- `useDiagramDrag` guards `setPointerCapture` / `document.elementFromPoint` with `in` checks (jsdom) — no lint rule disabled.
- Several non-quiz pages moved +1 KB versus the pre-change build (landing, lesson, admin-users); all remain within budget and `docs/performance.md` only records the quiz row as planned.
- PROGRESS.md left untouched (orchestrator edit). Nothing committed.
