# Implementation r2 — [E16.S2] Student canvas and grading (#126), rework

## Findings addressed
| # | What I changed | file:line |
|---|---|---|
| 1 | `GradeQuestionDraftHandler` now runs `QuestionAnswerRules.IsRawAnswerTooLong(type, …) \|\| ExceedsLimits(type, …)` before the type branch, so every type gets the same per-type raw cap as the quiz answer and exam save. That means `AnswerMaxLength` for Mcq/Multi/TrueFalse/Fill/Short, `MathStepsAnswerMaxLength`, `DragDropAnswerMaxLength` and `EssayAnswerMaxLength`. The result is 422 `ATTEMPT_ANSWER_TOO_LONG`. Essay-text length is still checked first by the validator (`QUESTION_ESSAY_ANSWER_TOO_LONG`). | `api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftHandler.cs:27` |
| 1 | Tests: `Handle_DragDropOverRawCap_ThrowsAttemptAnswerTooLong` (valid placement + 4001-char unknown property), `Handle_DragDropAtRawCap_Grades` (exactly 4000 chars grades), `Handle_McqOverRawCap_ThrowsAttemptAnswerTooLong` (consistency for other types) | `api/Elmanhg.Tests/Application/Features/Questions/GradeQuestionDraft/GradeQuestionDraftHandlerTests.cs:189,199,211` |
| 1 | Docs-sync: grade-draft paragraph now states the per-type raw cap, the same as the quiz answer, with the 422 code. Needed because Mcq/Fill/Short/Essay grade-draft now also get a raw cap. | `docs/question-schemas.md:316` |
| 2 | The swallow flag is now `swallowClickOn: HTMLElement \| null`, set to the chip element that ended the drag. A click is swallowed only when its `currentTarget` is that element, so a remounted or other chip is never swallowed. The flag is also cleared by window capture-phase `pointerdown` and `keydown` listeners, so it can never outlive the gesture: a touch drag with no click, or a chip that stays mounted, is cleared by the next tap or key press. | `web/src/features/questions/hooks/useDiagramDrag.ts:35,60-71,134,144-145` |
| 2 | Tests: `selects the next tapped chip after a mouse drag`, `selects the next tapped chip after a touch drag`, `selects the remounted chip with Enter after a drop`, plus the guard `does not select the chip when a mouse drag ends outside every target` (checks the drag's own click is still swallowed). Helpers `mockCanvasBounds` and `dragWallTo`. | `web/src/features/questions/components/DragDropAnswerInput.test.tsx:65-78,134-181` |

## Files created
None.

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftHandler.cs` | raw-length cap for every type |
| `api/Elmanhg.Tests/Application/Features/Questions/GradeQuestionDraft/GradeQuestionDraftHandlerTests.cs` | +3 tests |
| `docs/question-schemas.md` | grade-draft raw cap sentence |
| `web/src/features/questions/hooks/useDiagramDrag.ts` | element-scoped swallow, cleared on next pointerdown/keydown |
| `web/src/features/questions/components/DragDropAnswerInput.test.tsx` | +4 tests, 2 helpers |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Review fix: raw check in the non-essay branch | The orchestrator asked me to check the other types for consistency. The quiz and exam also apply the raw cap to essays. | Placed the check before the essay branch so all 8 types match the quiz answer. Recorded in docs. |
| Review suggested test name `Handle_DragDropOverRawCap_ThrowsAttemptAnswerTooLong` only | — | Added that test plus an at-cap boundary test and an Mcq consistency test. |

## Build & test
- API (CI parity: `api/Elmanhg.Api/appsettings.json` moved to the scratchpad, then restored and confirmed present): `dotnet test api/Elmanhg.Tests -c Release` gave `Passed! total: 4381 failed: 0 succeeded: 4381 skipped: 0`.
- Web `npx tsc -b`: exit 0.
- `npm run lint` (eslint, max-warnings 0): clean.
- `npx prettier --check` on the two touched web files: "All matched files use Prettier code style!". A whole-folder check warns on 46 files. These are CRLF checkout artefacts from `core.autocrlf=true` on this Windows clone, they predate this change, and none are touched files.
- `npx vitest run`: 260 files / 1449 tests passed.
- `npm run build`: OK.
- `npm run perf:budget`: all chunks ok (quiz 248/255, entry 206/210, landing 215/220, lesson 236/240, admin-dashboard 225/230, admin-users 258/270, teacher-home 253/265).
- Red-green: before the hook fix, the 3 "selects …" tests failed (`aria-pressed="false"`). As a mutation check, with the swallow disabled the "does not select …" guard fails.

## Notes for review
- Non-blocking items (Escape-cancel swallow, sessions.md:203 wording, `key.title`, `rounded-full`, `touch-none`) were not touched, per rework mode.
- `useDiagramDrag.ts` is 155 lines. It was already over ~100 before this rework.
- No contract change, so OpenAPI, Orval and Postman are unchanged.

## Merge note — origin/main (#123 LLM step grading) into #126

`git merge origin/main` produced 23 conflicts; all resolved keeping both stories:

| Area | Resolution |
|---|---|
| `GradeFeedbackKind` | main's values unchanged; `MathStepTally` stays 8, `PlacementTally` appended as 9. `GradeFeedback`, `GradeFeedbackKeys`, `GradeFeedbackText` carry both kinds. |
| `Messages.ar/en.resx` | both sets of keys (`GRADE_FEEDBACK_PLACEMENT_TALLY` + #123's math-step keys). |
| StartUnitExam / StartMultiUnitExam / SubmitExam / SubmitAnswer handlers | main's constructor (incl. `IMathStepGradeRepository`, `IMathCheckRateLimiter`) plus `IFileStorage fileStorage` appended last; test constructors updated the same way. |
| `GradeQuestionDraftHandler` | #126's up-front raw-size + limits check for every type, then #123's synchronous `MathStepsDraftGrading` for MathSteps, `QuestionGrader.Grade` otherwise (main's duplicate `ExceedsLimits` check dropped). `GradeQuestionDraftHandlerTests.Handle_DragDropOverPlacementCap_ThrowsAttemptAnswerTooLong` gets the two new constructor args. |
| `QuestionPreviewPanel.tsx` | both `isDragDrop` and `isStepGraded`. |
| Docs (PRD §grading table, sessions.md, question-schemas.md, claude-design-prompt.md §4–§6) | both stories described: steps weight + deferred MathSteps grading, and the drag-and-drop canvas / per-item grader / placement tally. main's "DragDrop not servable until #126" lines replaced by the #126 text. |

Verification after merge: `dotnet build api/ -c Release` succeeded (0 warnings; OpenAPI unchanged vs merged state); `npm --prefix web run gen:api` no drift; `dotnet test api/ -c Release` with `appsettings.json` moved aside: 4526/4526 passed; web lint, typecheck clean; `npm --prefix web test -- --run` 1482/1482 passed; `npm --prefix web run build` ok; `perf:budget` all within budget.
