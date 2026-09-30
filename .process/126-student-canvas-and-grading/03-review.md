VERDICT: CHANGES_REQUESTED

# Review — [E16.S2] Student canvas and grading (#126)

## Blocking

### 1. `grade-draft` does not enforce the DragDrop raw answer cap that the docs and the plan promise
**Where:** `api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftHandler.cs:27-33` (only `ExceedsLimits` runs; no `IsRawAnswerTooLong`), vs `docs/question-schemas.md:308` (§ Answer shapes, **DragDrop** paragraph).
**Rule:** docs-sync (divergence); plan Scope bullet 2 ("per-type caps (`Sessions:DragDropAnswerMaxLength`, …) on quiz answer, exam save and `grade-draft`").
**Problem:** The doc says "The raw answer is at most `Sessions:DragDropAnswerMaxLength` characters (4000), with at most … placements … and … item ids …, else `422 ATTEMPT_ANSWER_TOO_LONG` (quiz answer, exam save and `grade-draft`)". The quiz answer (`SubmitAnswerHandler.cs:66`) and exam save (`SaveExamAnswerHandler.cs:51`) check the raw length. `grade-draft` checks only the structural counts, and `GradeQuestionDraftValidator` has no raw-length rule.
**Failure:** `POST /api/questions/grade-draft` with a DragDrop draft and an answer whose raw JSON is longer than 4000 characters (for example one valid placement plus a 5000-character unknown property, or one 5000-character item id) returns 200 with a grade. The doc promises 422 `ATTEMPT_ANSWER_TOO_LONG`.
**Fix:** In the handler’s non-essay branch, also check `QuestionAnswerRules.IsRawAnswerTooLong(type, request.Answer, sessionsOptions.Value)`, and add a handler test (`Handle_DragDropOverRawCap_ThrowsAttemptAnswerTooLong`). The alternative is to reword the doc so the raw cap covers only the quiz answer and exam save. If you choose that, the plan’s scope line is being dropped and must be recorded as a deviation.

### 2. After a pointer drag, the "swallow the next click" flag is never cleared, so the student’s next tap or Enter on any chip is ignored
**Where:** `web/src/features/questions/hooks/useDiagramDrag.ts:122` (`swallowClick.current = true`) and `:131-137` (`onClickCapture`). The flag is one ref shared by every chip (`:35`).
**Rule:** plan Decision 14 (tap/click/Enter/Space selects a chip); DoD "canvas works by pointer drag, tap-then-zone and keyboard".
**Problem:** The flag is cleared only inside a chip’s `onClickCapture`. Two cases leave it set:
- A successful drop moves the chip between lists (bank to zone row, or back). React unmounts the pressed button during the pointerup commit, so the browser’s click never passes through that chip’s React capture handler.
- A touch or pen drag that moves beyond the tap slop produces no click at all.

Either way `swallowClick` stays true. The next click that reaches any chip, including a keyboard Enter/Space activation, is prevented and stopped.
**Failure:** On a phone, drag «Wall» onto zone 1, then tap «Nucleus». Nothing happens: no `aria-pressed`, no "Nucleus selected" announcement. The second tap works. A keyboard user who pressed Enter on a chip after a mouse drag gets silence. T22 (`DragDropAnswerInput.test.tsx:100-117`) stops at the drop, so it does not catch this.
**Fix:** Clear the flag outside the click path. For example, reset it at the start of `onPointerDown`, and clear it on a zero-delay timeout scheduled in `onPointerUp` after the drop. Or swallow only a click whose `currentTarget` is the element that started the drag. Extend T22, or add a test: after the drag, clicking the Nucleus chip leaves it `aria-pressed="true"` with the "Nucleus selected. Choose a zone." status.

## Non-blocking
- `web/src/features/questions/hooks/useDiagramDrag.ts:65-69, 113-120`: Escape during a mouse drag clears `press`, so the following pointerup returns early and does not set the swallow flag. The browser’s click then selects the chip and announces "selected" right after the cancel. Consider setting the swallow flag on Escape-cancel as well.
- **Float drift on zone end edges (asked by the orchestrator):** no epsilon or integer point mapping is needed. `web/src/features/questions/api/diagramPlacement.ts:86-99` maps zone bounds to integer hundredths, so the non-overlapping zones partition the axis. Any point, drifted or not, lands in at most one zone. Drift of about 1e-12 % can only decide which of two touching zones owns a point that sits exactly on their shared edge, and no real `clientX`/`getBoundingClientRect` pair produces such a point. Rounding the point would break the 30.099 case in T4.
- `docs/sessions.md:203`: the `ATTEMPT_ANSWER_TOO_LONG` row reads "…MathSteps, else `AnswerMaxLength`), … or a drag-and-drop answer is over `DragDropAnswerMaxLength`". Move DragDrop into the per-type list to avoid the "else" reading.
- `web/src/features/questions/i18n/diagramStudent.en.json` / `.ar.json`: `key.title` is not rendered anywhere (noted in 02). Drop it or use it.
- `DiagramItemChip.tsx:17`, `DiagramAnswerCanvas.tsx:72,76` and `DiagramDragGhost.tsx:11` use `rounded-full`. The tokenised pill radius is `rounded-pill` (`--radius-pill`, design-system `radius.pill`). `rounded-full` is common in the repo, so this is not gated.
- `DiagramItemChip.tsx:40` (`touch-none` on every chip): a vertical scroll gesture that starts on a chip does not scroll the page. This is an acceptable trade-off for drag, but consider applying `touch-action: none` only while a press is active.

## Verified
- **Grader** (`api/Elmanhg.Domain/Questions/Grading/DragDropGrader.cs`) matches Decisions 2–5 and `docs/question-schemas.md` § Grading line for line:
  - Keyed items are the credit units, and the first occurrence in the key wins.
  - Unknown, duplicate and null zones are skipped; the first occurrence of an item wins; null ids are skipped.
  - Distractors and unknown ids count as wrong. A keyed item in the wrong zone or position counts neither way.
  - Ordered zones compare the index in the resolved list.
  - Full credit only when right = K and wrong = 0; otherwise the score is floored at 0.
  - An answer with nothing in a known zone is Unanswered.
  - Over-capacity cannot earn more. Explicit loops, no side-effecting LINQ, sealed private records.
- **Malformed answers never reach the grader or the structural caps unread.** `CanRead` runs first on the quiz answer (`SubmitAnswerHandler.cs:61`), exam save (`SaveExamAnswerHandler.cs:46`) and grade-draft (the validator). `TryRead` rejects non-objects and JSON exceptions; null placement, null or non-string zoneId, and null item id give 422. `ExceedsLimits` runs only afterwards, so none of these paths can produce a 500.
- **The answer key is never sent before answering.** `SessionResultGenerator.cs:28-30` sets `correctAnswer` only when `reveal` is true, and the body holds no key (#125). I1 asserts `correctAnswer` is null at start. The exam reuses the same item generator.
- **The image URL is resolved server-side** from the stored key through `QuestionBodyMedia.Resolve` on every quiz and exam item, with `IFileStorage` appended last in all 8 handlers, the generators and the loader (P15, I1, I6). The web uses only `body.image.url`.
- `ServableQuestionSpecification` has no type clause and `ServedTypes` lists all 8 types. `QUESTION_TYPE_NOT_GRADABLE` is gone from code, resx and web (a git grep outside `.process` finds nothing). The three `Sessions:DragDrop*` keys are in the options (code defaults), `appsettings.example.json`, `ApiFactory` and `docs/deployment.md`.
- **Deviations in 02 checked:** the extra cap initialisers in the two validator tests, Accept-Language ar on I3, the window Escape listener, the aria-label on «ضعه هنا», blank-item filtering, and aria-labelledby on the zone rows. Each is justified and none hides a behaviour change.
- **Accessibility:** chips are aria-pressed buttons of at least 44 px; every canvas zone is a labelled button, and the zone list is the 44 px alternative. The role=status live region carries every action. Focus follows the moved chip (`DragDropAnswerInput.tsx:60-64`). Marks use an icon plus sr-only text. The canvas and ghost are dir=ltr with insetInlineStart/top, and the return icon mirrors in RTL.
- **Lazy loading:** `DragDropAnswerInput` and `DragDropCorrectAnswer` are dynamic imports, and `diagramStudent` strings register only in those modules. Student code imports only types from `dragDropValues`/`diagramGeometry`.
- **Re-runs:**
  - API with `api/Elmanhg.Api/appsettings.json` moved aside (restored afterwards): `dotnet test Elmanhg.Tests -c Release` gave 4378/4378 passed.
  - Web: tsc -b OK; eslint with max-warnings 0 OK; prettier clean; vitest 260 files / 1445 tests passed; build OK.
  - perf:budget: quiz 248/255, with every other chunk within budget (entry 206/210, landing 215/220, lesson 236/240, admin-dashboard 225/230, admin-users 258/270, teacher-home 253/265). `docs/performance.md` records 248.
  - No drift in `api/openapi`, `postman` or the Orval client. There is no contract change, so Postman sync is satisfied.
- **Docs:** PRD §5.3, §6 row and paragraph, and §17 rule 1; question-schemas (shape, drop mapping, servable, grading, feedback); sessions, exams, mastery, exam-blueprints, performance; both design-system files agree; claude-design-prompt §4 (quiz, exam, editor preview); prototype.md. No doc still calls drag-and-drop unservable or ungradable. The one divergence is finding 1.

## Test quality
- `DragDropGraderTests` (D1–D11): strong. Every rule has a distinguishing case, and the mutation log in 02 is consistent with the assertions (exact NormalisedGrade equality, including the tally).
- `DragDropAnswerRulesTests` (P1–P7): these constrain the code; the boundary is exercised by P7 (at caps) against P5/P6.
- `SessionResultGeneratorTests`, `QuestionAnswerRulesTests` P8/P9, grade-draft handler and validator, `SubmitAnswerHandlerTests` P17/P18 (Received(1) / DidNotReceive): these constrain the code.
- Integration I1–I10: real HTTP and DB assertions (canonical stored answer, score, Arabic tally, 422 with no attempt stored). Good.
- `diagramPlacement.test.ts` (T1–T12): these constrain the edge rules, including the hundredths case added to T4.
- `DragDropAnswerInput.test.tsx`: covers keyboard, tap, drag, full, Escape, marks, RTL and axe. The gap is T22, which never interacts after a drag, so finding 2 passes silently.
- No vacuous tests found. Test gap: no test pins the raw cap on grade-draft (finding 1).
