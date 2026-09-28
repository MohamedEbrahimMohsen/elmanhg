# Plan — Graders for mcq, multi-select, true/false (#71, E4.S2)

## Goal
Each choice-question grade (Mcq, Multi, TrueFalse) now carries a localised **feedback** line along with the score and normalised score. An unanswered question returns "No answer was given." A multi-select answer that is not exactly right returns a tally of its correct and wrong choices. `POST /api/questions/grade-draft` returns this as `feedback`, and the admin "Try the answer" panel shows it. The multi-select partial-credit rule was already built in #65. Its edge cases (duplicates, unknown ids, null ids, empty answer, all-correct-plus-wrong, equal right and wrong, case, rounding) are now pinned by tests and written down in the docs without ambiguity. The three untested defensive throws in `QuestionGrader` (#151 leftover) get tests.

## Scope
**In:**
- `GradeFeedback` on `QuestionGrade`.
- Choice graders return value plus feedback.
- Server-side localisation of the feedback, and `feedback` on `QuestionGradeResult`.
- Regenerated OpenAPI and Orval output.
- A one-line feedback display in the admin `GradeResultPanel`.
- Edge-case tests, and tests for the three `QuestionGrader` throws.
- Docs: `question-schemas.md`, `PRD.md` §6 and `claude-design-prompt.md` §4.

**Out:**
- Feedback for Fill and Short (#72).
- The student quiz feedback panel, which shows the correct answer and the explanation (#76).
- The attempt submit endpoint (#74).
- Any change to the formula or to the rounding.
- `ai/`.

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | What is the "grader interface"? | `QuestionGrader.Grade(type, gradingSpec, maxScore, answer)` returns `QuestionGrade(Score, NormalisedScore, Outcome, Feedback)`. There is **no** C# `interface` and no DI. | The graders are pure static domain functions (#65). An interface adds nothing until v2's async AI grader exists, and the no-new-abstractions rule applies. |
| D2 | What is the feedback? | A structured domain value `GradeFeedback(Kind, Right, Wrong, Total)`. The Application layer turns it into one localised sentence via `ILocalizer` with `{right}/{wrong}/{total}` placeholders. It is `null` when there is nothing to add. It does **not** include the verdict (the UI derives that from `outcome`), the correct answer or the explanation: #76/#74 read those from the question, as in the prototype `feedbackBox`. | It mirrors the prototype's `grade()`, whose `fb` is an extra line only (multi tally). PRD §6 already expects a text justification from graders (v2 AI). The domain stays language-free. |
| D3 | When does a choice grader emit feedback? | Mcq: `optionId` null → `Unanswered`; otherwise null. TrueFalse: `value` null → `Unanswered`; otherwise null. Multi: no non-null selected id → `Unanswered`; exactly the correct set → null; any other selection → `ChoiceTally(right, wrong, total)`, **in both partial-credit modes**; empty correct set (unreachable after validation) → null with value 0. | "Unanswered" separates a 0 for no answer from a 0 for a wrong answer, which matters for exam review. The tally explains a 0 under all-or-nothing as well as a partial score. |
| D4 | Where is the text localised? | On the server: `GradeFeedbackText.Localize(feedback, ILocalizer)` in Application, with the resx keys `GRADE_FEEDBACK_UNANSWERED` and `GRADE_FEEDBACK_CHOICE_TALLY`. The web shows `feedback` verbatim. | This is the existing pattern (`QuestionImportHelpKeys` + `ILocalizer.GetMessage` with context, Morabh `Morabh.Application/Orders/AddOnlineOrder/AddOnlineOrderHandler.cs` context dictionary). The web already sends `Accept-Language` (`web/src/shared/lib/http.ts`). v2 AI justifications will arrive as server text anyway. |
| D5 | Is the formula correct against PRD? | Yes, no code change. `ChoiceGrader.GradeMulti` (#65 D4) = `max(0, (right − wrong) / |correct|)`. `total` means the number of correct options (as in the prototype's `sp.correct.length`). Distinct ids only. Unknown ids count as wrong. Null ids are ignored. Ordinal (case-sensitive) comparison. `partialCredit` defaults to false (PRD §19 Q3). Rounding: `NormalisedScore = round(v, 4)` and `Score = round(v × maxScore, 2)`, away from zero; the prototype's 2-dp normalised rounding is deliberately not copied (#65 D4, mastery threshold). | The PRD wording "(correct − wrong)/total" is ambiguous about `total`, so the PRD and the doc are clarified (docs-sync). |
| D6 | Grader return type | A new `NormalisedGrade(decimal Value, GradeFeedback? Feedback)` returned by the three `ChoiceGrader` methods. `QuestionGrade.FromNormalised` takes it. `TextGrader` is unchanged: its arms are wrapped as `new NormalisedGrade(x, null)` until #72. | The value and the tally come from one computation, so the logic is not duplicated. |
| D7 | Existing tests that must change | `ChoiceGraderTests`: the 9 existing assertions switch from `.Should().Be(xm)` to `.Value.Should().Be(xm)` (same expected numbers). `QuestionGraderTests.Grade_McqCorrect…` adds `null` and `Grade_MultiPartial…` adds `GradeFeedback.ChoiceTally(2, 1, 3)` to the expected record. `GradeQuestionDraftHandlerTests.Handle_CorrectMcq…` adds `null`. The web `gradeResult` fixture adds `feedback: null`. | The contract changed and every assertion stays as strict or gets stricter. No expected number changes. |
| D8 | `QuestionGrade.Feedback` default | No default value; it is a required positional parameter. | Every construction must decide. |
| D9 | Admin preview display | `GradeResultPanel` renders `result.feedback` as a third line (`text-caption text-text-muted`) only when it is non-null. `docs/claude-design-prompt.md` §4 is updated. `docs/prototype.md` is unchanged, because the prototype already shows `fb` in its result box. | The server now returns it, and otherwise nothing shows it until #76. |
| D10 | #151 leftover | Three tests in `QuestionGraderTests`: an undefined `QuestionType` `(QuestionType)99`, a grading spec of JSON `null`, and an answer of JSON `null`. Each asserts `InvalidOperationException` and its exact message. | These close the `QuestionGrader.cs:17,27,38` item of #151. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/Grading/ChoiceGrader.cs` | Rewrite the bodies to return `NormalisedGrade` (see Domain behaviour). |
| `api/Elmanhg.Domain/Questions/Grading/QuestionGrade.cs` | `public sealed record QuestionGrade(decimal Score, decimal NormalisedScore, GradeOutcome Outcome, GradeFeedback? Feedback)`. `FromNormalised(NormalisedGrade grade, int maxScore)`: the same outcome switch and rounding, applied to `grade.Value`, and it passes through `grade.Feedback`. Keep the existing comment and constants. |
| `api/Elmanhg.Domain/Questions/Grading/QuestionGrader.cs` | The switch yields `NormalisedGrade`. The Fill arm becomes `new NormalisedGrade(TextGrader.GradeFill(...), null)`, and Short likewise. Rename the local `normalised` to `grade`. `return QuestionGrade.FromNormalised(grade, maxScore);`. The three throws stay with the same messages. |
| `api/Elmanhg.Application/Questions/GradeQuestionDraft/QuestionGradeResult.cs` | `public sealed record QuestionGradeResult(decimal Score, decimal NormalisedScore, string Outcome, int MaxScore, string? Feedback);` It is client-facing: `Feedback` is already localised. |
| `api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftHandler.cs` | Constructor `(IRichTextSanitizer richTextSanitizer, ILocalizer localizer)` with `using Core.Localization;` and `using Elmanhg.Application.Questions.Shared.Grading;`. The return appends `GradeFeedbackText.Localize(grade.Feedback, localizer)`. |
| `api/Elmanhg.Api/Resources/Messages.en.resx` | Add 2 `<data>` entries after the `QUESTION_IMPORT_HELP_*` block, in the same single-line format (text in Error codes below). |
| `api/Elmanhg.Api/Resources/Messages.ar.resx` | Same 2 keys, Arabic text. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`: `QuestionGradeResult.feedback` (nullable string). |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api` (`questionGradeResult.ts`, `questions.msw.ts`). No hand edits. |
| `web/src/features/questions/components/GradeResultPanel.tsx` | Inside the `flex flex-col` div, after the score `<p>`, add `{result.feedback ? <p className="text-caption text-text-muted">{result.feedback}</p> : null}`. |
| `api/Elmanhg.Tests/Domain/Questions/Grading/ChoiceGraderTests.cs` | D7 mechanical `.Value` change, plus the new tests CG1–CG20. |
| `api/Elmanhg.Tests/Domain/Questions/Grading/QuestionGraderTests.cs` | D7 updates, plus QG1–QG6. |
| `api/Elmanhg.Tests/Application/Features/Questions/GradeQuestionDraft/GradeQuestionDraftHandlerTests.cs` | Add `private readonly ILocalizer _localizer = Substitute.For<ILocalizer>();` and pass it to the constructor. D7 update. Add GH1. |
| `api/Elmanhg.Tests/Integration/Content/QuestionGradeDraftEndpointTests.cs` | Add DG1–DG3. |
| `web/src/features/questions/pages/QuestionEditorPage.test.tsx` | Add `feedback: null` to the `gradeResult` fixture. Add WE1. |
| `docs/question-schemas.md` § Grading | Replace the Mcq/TrueFalse and Multi bullets, and add a **Feedback** bullet after **Result** (exact text below). |
| `docs/PRD.md` §6 | Replace the Multi-select row's Grading cell with `Set match; optional partial max(0, (correct chosen − wrong chosen) / number of correct options)`. After the sentence "AI-graded types return a partial score plus a written justification." add: `Every grade may also carry a short feedback line in the student's language (for example, how many correct and wrong options a multi-select answer chose); the rules are in docs/question-schemas.md.` |
| `docs/claude-design-prompt.md` §4 (line with `#/admin/question/:id`) | Replace `"جرّب الإجابة" running the real grader` with `"جرّب الإجابة" running the real grader and showing the verdict, the score and the grader's feedback line when there is one`. |

### Doc text for `docs/question-schemas.md` § Grading
- **Mcq, TrueFalse**: exact match (1 or 0). A missing `optionId` or `value` is unanswered and scores 0.
- **Multi**: the rule uses three counts:
  - `right`: the distinct selected ids that are correct.
  - `wrong`: the distinct selected ids that are not correct. An id that is not an option counts as wrong. Ids compare exactly, so case matters. A repeated id counts once. A null id is ignored.
  - `total`: the number of correct options.

  Without `partialCredit` (the default), the score is 1 when `right = total` and `wrong = 0`, else 0. With `partialCredit`, the score is `max(0, (right − wrong) / total)`. For example, with 3 correct options, choosing all three plus one wrong option scores 2/3. With no selection, the answer is unanswered and scores 0 in both modes.
- **Feedback** (after **Result**): every grade carries an optional feedback line, returned as `feedback`. It is localised to the request language (`Accept-Language`, Arabic by default) and is `null` when there is nothing to add.
  - An unanswered Mcq, TrueFalse or Multi answer returns «لم تتم الإجابة عن السؤال.» / "No answer was given."
  - A Multi answer that is not exactly the correct set returns «الاختيارات الصحيحة: {right} من {total}، والخاطئة: {wrong}.» / "Correct choices: {right} of {total}; wrong choices: {wrong}." This applies in both partial-credit modes.
  - Every other case returns `null`.

  The feedback never contains the verdict, the correct answer or the explanation. Fill and Short return no feedback.

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| F1 | `api/Elmanhg.Domain/Questions/Grading/GradeFeedbackKind.cs` | enum | `namespace Elmanhg.Domain.Questions.Grading; public enum GradeFeedbackKind { Unanswered, ChoiceTally }` New; no Morabh equivalent. |
| F2 | `api/Elmanhg.Domain/Questions/Grading/GradeFeedback.cs` | value object | `public sealed record GradeFeedback(GradeFeedbackKind Kind, int Right, int Wrong, int Total)` with `public static GradeFeedback Unanswered { get; } = new(GradeFeedbackKind.Unanswered, 0, 0, 0);` and `public static GradeFeedback ChoiceTally(int right, int wrong, int total)`, which returns `new(GradeFeedbackKind.ChoiceTally, right, wrong, total)`. New. |
| F3 | `api/Elmanhg.Domain/Questions/Grading/NormalisedGrade.cs` | value object | `public sealed record NormalisedGrade(decimal Value, GradeFeedback? Feedback)` with `public static NormalisedGrade Unanswered { get; } = new(0m, GradeFeedback.Unanswered);`. New. |
| F4 | `api/Elmanhg.Application/Questions/Shared/Grading/GradeFeedbackKeys.cs` | static class | `namespace Elmanhg.Application.Questions.Shared.Grading; public static class GradeFeedbackKeys { public const string Unanswered = "GRADE_FEEDBACK_UNANSWERED"; public const string ChoiceTally = "GRADE_FEEDBACK_CHOICE_TALLY"; public const string RightArgument = "right"; public const string WrongArgument = "wrong"; public const string TotalArgument = "total"; }` Mirrors `QuestionImportHelpKeys`. |
| F5 | `api/Elmanhg.Application/Questions/Shared/Grading/GradeFeedbackText.cs` | static class | `public static string? Localize(GradeFeedback? feedback, ILocalizer localizer)`. Steps: (1) `feedback is null` → `return null;` (2) `return feedback.Kind switch { GradeFeedbackKind.Unanswered => localizer.GetMessage(GradeFeedbackKeys.Unanswered), GradeFeedbackKind.ChoiceTally => localizer.GetMessage(GradeFeedbackKeys.ChoiceTally, context: new Dictionary<string, object> { [GradeFeedbackKeys.RightArgument] = feedback.Right, [GradeFeedbackKeys.WrongArgument] = feedback.Wrong, [GradeFeedbackKeys.TotalArgument] = feedback.Total }), _ => throw new InvalidOperationException("Unsupported grade feedback."), };` Context dictionary pattern from Morabh `Morabh.Application/Orders/AddOnlineOrder/AddOnlineOrderHandler.cs`; localizer from vendored `Core.Localization`. |
| F6 | `api/Elmanhg.Tests/Application/Features/Questions/Shared/GradeFeedbackTextTests.cs` | tests | GT1–GT4. |

## Error codes
None: this story adds no error codes or exceptions. It adds 2 **message** keys (not errors):

| Key | English | Arabic |
|---|---|---|
| `GRADE_FEEDBACK_UNANSWERED` | `No answer was given.` | `لم تتم الإجابة عن السؤال.` |
| `GRADE_FEEDBACK_CHOICE_TALLY` | `Correct choices: {right} of {total}; wrong choices: {wrong}.` | `الاختيارات الصحيحة: {right} من {total}، والخاطئة: {wrong}.` |

## Domain behaviour
`ChoiceGrader` (static; ordinal comparisons, as today):
```
GradeMcq(McqGradingSpec spec, McqAnswer answer) → NormalisedGrade
  if answer.OptionId is null → return NormalisedGrade.Unanswered
  return new NormalisedGrade(string.Equals(answer.OptionId, spec.CorrectOptionId, StringComparison.Ordinal) ? 1m : 0m, null)

GradeTrueFalse(TrueFalseGradingSpec spec, TrueFalseAnswer answer) → NormalisedGrade
  if answer.Value is null → return NormalisedGrade.Unanswered
  return new NormalisedGrade(answer.Value == spec.CorrectAnswer ? 1m : 0m, null)

GradeMulti(MultiGradingSpec spec, MultiAnswer answer) → NormalisedGrade
  var selected = (answer.OptionIds ?? []).Where(x => x is not null).ToHashSet(StringComparer.Ordinal)   // LINQ chain one operator per line
  if selected.Count == 0 → return NormalisedGrade.Unanswered
  var correct = (spec.CorrectOptionIds ?? []).ToHashSet(StringComparer.Ordinal)
  if correct.Count == 0 → return new NormalisedGrade(0m, null)
  var right = selected.Count(correct.Contains); var wrong = selected.Count - right
  if right == correct.Count && wrong == 0 → return new NormalisedGrade(1m, null)
  var value = spec.PartialCredit ? Math.Max(0m, (decimal)(right - wrong) / correct.Count) : 0m
  return new NormalisedGrade(value, GradeFeedback.ChoiceTally(right, wrong, correct.Count))
```
There are no entity changes, no `BusinessRuleViolationException` and no `UpdationDate` changes, because the graders are pure functions.

## API surface
`POST /api/questions/grade-draft` has the same route, policy (`Content.Manage`) and request. The response `QuestionGradeResult` gains `feedback: string | null`, localised by `Accept-Language`. There is no new endpoint and no Postman change (the request is unchanged).

## Test plan
All tests use FluentAssertions 7 (pinned) and xUnit v3. Domain tests use no doubles.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| CG1 | ChoiceGraderTests | `GradeMcq_CorrectOption_HasNoFeedback` | `("b","b")` → `Be(new NormalisedGrade(1m, null))` |
| CG2 | ChoiceGraderTests | `GradeMcq_WrongOption_ReturnsZeroWithoutFeedback` | `("b","a")` → `Be(new NormalisedGrade(0m, null))` |
| CG3 | ChoiceGraderTests | `GradeMcq_NoOption_ReturnsUnanswered` | `McqAnswer(null)` → `Be(NormalisedGrade.Unanswered)` |
| CG4 | ChoiceGraderTests | `GradeTrueFalse_TrueMatchesTrue_ReturnsOne` | spec true, answer true → `Be(new NormalisedGrade(1m, null))` |
| CG5 | ChoiceGraderTests | `GradeTrueFalse_WrongValue_ReturnsZeroWithoutFeedback` | spec false, answer true → `Be(new NormalisedGrade(0m, null))` |
| CG6 | ChoiceGraderTests | `GradeTrueFalse_NoValue_ReturnsUnanswered` | answer null → `Be(NormalisedGrade.Unanswered)` |
| CG7 | ChoiceGraderTests | `GradeMulti_ExactSet_HasNoFeedback` (`[Theory]` `true`/`false` partialCredit) | correct [a,c], selected [c,a] → `Be(new NormalisedGrade(1m, null))` |
| CG8 | ChoiceGraderTests | `GradeMulti_EmptySelection_ReturnsUnanswered` (`[Theory]` `true`/`false`) | selected [] → `Be(NormalisedGrade.Unanswered)` |
| CG9 | ChoiceGraderTests | `GradeMulti_NullSelection_ReturnsUnanswered` | `MultiAnswer(null)` → `Be(NormalisedGrade.Unanswered)` |
| CG10 | ChoiceGraderTests | `GradeMulti_OnlyNullIds_ReturnsUnanswered` | `MultiAnswer([null!])` → `Be(NormalisedGrade.Unanswered)` |
| CG11 | ChoiceGraderTests | `GradeMulti_SubsetWithoutPartialCredit_ReportsTally` | correct [a,b], selected [a] → `Be(new NormalisedGrade(0m, GradeFeedback.ChoiceTally(1, 0, 2)))` |
| CG12 | ChoiceGraderTests | `GradeMulti_AllCorrectPlusWrongWithoutPartialCredit_ReturnsZero` | correct [a,b], selected [a,b,c] → `Be(new NormalisedGrade(0m, ChoiceTally(2, 1, 2)))` |
| CG13 | ChoiceGraderTests | `GradeMulti_AllCorrectPlusWrongWithPartialCredit_ReturnsTwoThirds` | correct [a,b,c] partial, selected [a,b,c,d] → `Value` = `2m / 3m`, `Feedback` = `ChoiceTally(3, 1, 3)` |
| CG14 | ChoiceGraderTests | `GradeMulti_PartialCreditRepeatedCorrectId_CountsOnce` | correct [a,b] partial, selected [a,a] → `Be(new NormalisedGrade(0.5m, ChoiceTally(1, 0, 2)))` |
| CG15 | ChoiceGraderTests | `GradeMulti_PartialCreditRepeatedWrongId_CountsOnce` | correct [a,b,c] partial, selected [a,b,d,d] → `Value` = `1m / 3m`, `Feedback` = `ChoiceTally(2, 1, 3)` |
| CG16 | ChoiceGraderTests | `GradeMulti_PartialCreditUnknownId_CountsAsWrong` | correct [a,b] partial, selected [a,b,z] → `Be(new NormalisedGrade(0.5m, ChoiceTally(2, 1, 2)))` |
| CG17 | ChoiceGraderTests | `GradeMulti_PartialCreditEqualRightAndWrong_ReturnsZero` | correct [a,b] partial, selected [a,c] → `Be(new NormalisedGrade(0m, ChoiceTally(1, 1, 2)))` |
| CG18 | ChoiceGraderTests | `GradeMulti_PartialCreditNullIdAmongIds_IsIgnored` | correct [a,b] partial, selected [a, null!] → `Be(new NormalisedGrade(0.5m, ChoiceTally(1, 0, 2)))` |
| CG19 | ChoiceGraderTests | `GradeMulti_IdsCompareCaseSensitively` | correct [a] partial, selected ["A"] → `Be(new NormalisedGrade(0m, ChoiceTally(0, 1, 1)))` |
| CG20 | ChoiceGraderTests | `GradeMulti_EmptyCorrectSet_ReturnsZeroWithoutFeedback` | correct [] partial, selected [a] → `Be(new NormalisedGrade(0m, null))` |
| QG1 | QuestionGraderTests | `Grade_MultiWithoutPartialCreditKey_DefaultsToAllOrNothing` | spec `{"correctOptionIds":["a","b"]}`, answer `{"optionIds":["a"]}`, max 1 → `Be(new QuestionGrade(0m, 0m, GradeOutcome.Incorrect, GradeFeedback.ChoiceTally(1, 0, 2)))` |
| QG2 | QuestionGraderTests | `Grade_MultiTwoThirds_RoundsAwayFromZero` | spec `{"correctOptionIds":["a","b","c"],"partialCredit":true}`, answer `{"optionIds":["a","b","c","d"]}`, max 1 → `Be(new QuestionGrade(0.67m, 0.6667m, GradeOutcome.Partial, GradeFeedback.ChoiceTally(3, 1, 3)))` |
| QG3 | QuestionGraderTests | `Grade_McqUnanswered_ReturnsUnansweredFeedback` | spec `{"correctOptionId":"b"}`, answer `{}`, max 2 → `Be(new QuestionGrade(0m, 0m, GradeOutcome.Incorrect, GradeFeedback.Unanswered))` |
| QG4 | QuestionGraderTests | `Grade_UndefinedType_ThrowsInvalidOperation` | `(QuestionType)99` → `Throw<InvalidOperationException>().WithMessage("Unsupported question type.")` |
| QG5 | QuestionGraderTests | `Grade_NullGradingSpec_ThrowsInvalidOperation` | Mcq, spec `"null"`, answer `{"optionId":"b"}` → `WithMessage("Question grading spec is not readable.")` |
| QG6 | QuestionGraderTests | `Grade_NullAnswer_ThrowsInvalidOperation` | Mcq, spec `{"correctOptionId":"b"}`, answer `Json("null")` → `WithMessage("Question answer was not validated.")` |
| GT1 | GradeFeedbackTextTests | `Localize_NullFeedback_ReturnsNull` | Result null. `localizer.DidNotReceiveWithAnyArgs().GetMessage(default)` |
| GT2 | GradeFeedbackTextTests | `Localize_Unanswered_UsesUnansweredKey` | Substitute: `GetMessage(GradeFeedbackKeys.Unanswered, Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>())` returns `"unanswered"`, anything else `""` → result `"unanswered"` |
| GT3 | GradeFeedbackTextTests | `Localize_ChoiceTally_PassesRightWrongAndTotal` | Substitute matches key `ChoiceTally` and `Arg.Is<Dictionary<string, object>?>(x => x != null && x["right"].Equals(2) && x["wrong"].Equals(1) && x["total"].Equals(3))` → returns `"tally"`. `ChoiceTally(2, 1, 3)` → `"tally"` |
| GT4 | GradeFeedbackTextTests | `Localize_UndefinedKind_ThrowsInvalidOperation` | `new GradeFeedback((GradeFeedbackKind)99, 0, 0, 0)` → `Throw<InvalidOperationException>().WithMessage("Unsupported grade feedback.")` |
| GH1 | GradeQuestionDraftHandlerTests | `Handle_MultiPartial_ReturnsLocalizedFeedback` | Draft Multi (options a,b,c; spec correct [a,b] partial) answer `{"optionIds":["a"]}`. `_localizer` returns `"tally"` for key `GRADE_FEEDBACK_CHOICE_TALLY` → `Be(new QuestionGradeResult(0.5m, 0.5m, "Partial", 1, "tally"))` |
| DG1 | QuestionGradeDraftEndpointTests | `Post_MultiPartialInEnglish_ReturnsTallyFeedback` | `admin.DefaultRequestHeaders.Add("Accept-Language", "en")`. Multi row from the existing theory (correct [a,b] partial, answer [a]) → 200, `feedback` = `"Correct choices: 1 of 2; wrong choices: 0."` |
| DG2 | QuestionGradeDraftEndpointTests | `Post_McqUnansweredInArabic_ReturnsUnansweredFeedback` | `Accept-Language: ar`. Mcq answer `{}` → 200, `outcome` `"Incorrect"`, `feedback` = `"لم تتم الإجابة عن السؤال."` |
| DG3 | QuestionGradeDraftEndpointTests | `Post_CorrectTrueFalse_ReturnsNullFeedback` | TrueFalse correct → 200, `GetProperty("feedback").ValueKind` = `JsonValueKind.Null` |
| WE1 | QuestionEditorPage.test.tsx | `it('shows the grader feedback')` | Mock `gradeResult({ outcome: 'Partial', score: 0.5, normalisedScore: 0.5, feedback: 'Correct choices: 1 of 2; wrong choices: 0.' })`. Click "Try the answer" → `findByText('Correct choices: 1 of 2; wrong choices: 0.')` inside the preview |

Write Arabic literals in C# tests as normal UTF-8 text. They contain no invisible characters, so the `\u` gotcha does not apply.

## Definition of done
- [ ] `QuestionGrade` has `Feedback`. `QuestionGrader.Grade` returns it for all five types (null for Fill and Short).
- [ ] The Mcq, TrueFalse and Multi graders behave exactly as in Domain behaviour. There is no change to the Multi formula, the defaults or the rounding.
- [ ] There is no C# grader interface, no new exception type and no new error code.
- [ ] `GradeFeedbackText` localises via `ILocalizer` with the `{right}/{wrong}/{total}` context. Both resx files have both keys, with the exact texts above.
- [ ] `QuestionGradeResult.Feedback` is on the wire. `api/openapi/v1.json` and the Orval output are regenerated, with no drift.
- [ ] `GradeResultPanel` shows the feedback line only when it is non-null.
- [ ] Tests CG1–CG20, QG1–QG6, GT1–GT4, GH1, DG1–DG3 and WE1 exist with these names. The D7 edits to existing tests only add fields or `.Value` and change no expected number.
- [ ] The #151 `QuestionGrader` throws item is covered by QG4–QG6.
- [ ] `docs/question-schemas.md` § Grading, `docs/PRD.md` §6 and `docs/claude-design-prompt.md` §4 carry the text above.
- [ ] `dotnet test api/ -c Release` passes with `appsettings.json` moved aside. The web checks pass: typecheck, lint, format and test.
