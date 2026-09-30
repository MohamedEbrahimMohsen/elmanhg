VERDICT: APPROVED

# Review r2 — [E16.S2] Student canvas and grading (#126)

## Blocking
None.

## Non-blocking
- `docs/sessions.md:203`: the `ATTEMPT_ANSWER_TOO_LONG` row still reads "...else `AnswerMaxLength`), ... or a drag-and-drop answer is over `DragDropAnswerMaxLength`" (carried over from r1). The meaning is correct but the wording is awkward. Move DragDrop into the per-type list.
- `docs/essay-grading.md:105` lists grade-draft essay errors but not the new raw cap. This is not a divergence: the 121000-character essay raw cap cannot trigger before the 20000-character `QUESTION_ESSAY_ANSWER_TOO_LONG` text cap for any realistic payload, and `docs/question-schemas.md:316` and `docs/sessions.md` document the raw cap.
- No handler test pins an essay payload at or over the essay raw cap on grade-draft. `Handle_Essay_*` tests pass, which shows the essay path is not blocked by the earlier check.
- The other r1 non-blocking items (Escape-cancel swallow, `key.title`, `rounded-full`, `touch-none`) are still open. That was intended in rework mode.

## Verified
- **Finding 1 fixed.**
  - `GradeQuestionDraftHandler.cs:27` runs `IsRawAnswerTooLong || ExceedsLimits` for every type before the essay branch and throws `AttemptAnswerTooLong`.
  - `QuestionAnswerRules.cs:60-66` resolves the per-type cap: Essay 121000, MathSteps 24000, DragDrop 4000, else 4000. This matches the quiz answer and the exam save.
  - The ordering deviation is safe for essays. The validator (`GradeQuestionDraftValidator.cs:22-26`) caps essay text at `Content:QuestionEssayAnswerMaxLength` = 20000 first. 20000 characters, even fully `\uXXXX`-escaped (120000), plus the envelope fits under 121000, so a valid essay never hits the raw cap. The existing essay handler tests still pass.
  - Tests constrain the fix. `Handle_DragDropOverRawCap_ThrowsAttemptAnswerTooLong` uses a valid placement that passes `ExceedsLimits`, so only the raw check can fail it. `Handle_DragDropAtRawCap_Grades` asserts an exact 4000-character answer and would catch a `>=` mistake. `Handle_McqOverRawCap_ThrowsAttemptAnswerTooLong` covers the other types.
  - The docs were updated at `docs/question-schemas.md:316`. The DragDrop paragraph (`:308`) now agrees with the code.
- **Finding 2 fixed.**
  - `useDiagramDrag.ts:35,134,143-148`: the swallow is scoped to the element that ended the drag (`swallowClickOn === event.currentTarget`), so a remounted chip or any other chip is never swallowed.
  - `:61-71`: window capture-phase `pointerdown` and `keydown` listeners clear the flag.
    - The next tap's pointerdown fires before its click.
    - Enter/Space keydown fires before the synthetic click.
    - Neither can be swallowed by a stale flag.
  - The drag's own click (pointerup, then click, with no pointerdown in between) is still swallowed.
  - Four new tests cover this: `selects the next tapped chip after a mouse drag`, `... after a touch drag`, `selects the remounted chip with Enter after a drop`, and the guard `does not select the chip when a mouse drag ends outside every target` (`DragDropAnswerInput.test.tsx:134-181`). The guard fails if the swallow is removed, and the other three fail without the clearing or scoping, so both directions are constrained.
- **Re-runs (independent):**
  - API, CI parity (`api/Elmanhg.Api/appsettings.json` moved to the scratchpad and restored): `dotnet test api/Elmanhg.Tests -c Release` gave 4381/4381 passed, matching 02-r2.
  - Web: `tsc -b` clean; `npm run lint` (max-warnings 0) clean; prettier clean on the two touched files; vitest 260 files / 1449 tests passed; build OK.
  - perf:budget all ok: entry 206/210, landing 215/220, lesson 236/240, quiz 248/255, admin-dashboard 225/230, admin-users 258/270, teacher-home 253/265.
- **No contract change:** there are no modifications under `postman/`, the OpenAPI file or the generated client. Postman sync still holds.

## Test quality
- The three new `GradeQuestionDraftHandlerTests` cases constrain the implementation: over-cap, exact boundary, and a cross-type case.
- The four new `DragDropAnswerInput` tests constrain the implementation in both directions. No vacuous tests.
