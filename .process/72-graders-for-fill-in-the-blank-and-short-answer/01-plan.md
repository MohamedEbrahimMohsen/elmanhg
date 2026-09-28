# Plan — Graders for fill-in-the-blank and short answer (#72, E4.S3)

## Goal
The Fill and Short graders built in #65 (`TextGrader`) become total and explain themselves. The grading rules do not change for valid data. Four things are new:
- An admin-authored numeric spec near the decimal limits no longer crashes grading (today `value ± allowed` can throw `OverflowException`, which is a 500).
- Tolerance bounds are pinned as inclusive, including negative and zero values in percent mode.
- Fill and Short return the same localised `feedback` line that #71 added for the choice types:
  - Fill: "Correct blanks: X of Y".
  - Any type: "No answer was given".
  - Numeric Short: "Write the answer as a plain number".
- The percent tolerance path is covered end to end by tests: grader, validator, import and editor.

## Scope
**In:**
- `TextGrader` returns `NormalisedGrade` with feedback.
- Saturating tolerance arithmetic in `TextGrader`.
- Two new feedback kinds, their keys, the resx strings and `GradeFeedbackText` arms.
- Tests for every sub-task branch.
- `docs/question-schemas.md` § Grading, § Short and § Feedback.

**Out:**
- No schema change. Percent tolerance already exists in the spec record, `ShortQuestionRules`, the web editor (`ShortAnswerFields.tsx`, `questionEditorSchema.ts`), the import (`QuestionImportBodies.cs:48`, `docs/question-import.md`) and the docs.
- No migration, no endpoint or OpenAPI change, no Orval regeneration, no Postman change.
- No web component change. `GradeResultPanel.tsx:48` already renders any `feedback`.
- No new validator rules.

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Does percent tolerance need to be added? | No. `ToleranceMode.Percent` exists end to end: `ShortSchemas.cs`, `ShortQuestionRules`, `TextGrader.AllowedDifference`, the web editor option `editor.short.percent`, the import `tolerance_mode` column and the docs. This story adds only tests that pin it: grader, validator, import parser and web round-trip. | Diffed against the code. The #72 brief's "if not, add it" branch does not apply. |
| D2 | Can absolute and percent tolerances both be set? | No. The spec holds exactly one `tolerance` and one `toleranceMode` (PRD §6: "tolerance ±x or %"). There is no combined mode. The docs say so explicitly. | The PRD and the stored shape both define a single mode. Adding a second tolerance would be a schema change with no product ask behind it. |
| D3 | Are the bounds inclusive? | Yes. The answer is correct when `lower ≤ number ≤ upper`, where `lower = value − allowed` and `upper = value + allowed`. | Existing behaviour (`>=` / `<=`), now pinned by tests. |
| D4 | Negative expected value, percent mode | `allowed = |value| × tolerance / 100`, so the band is symmetric around the negative value. | Existing behaviour and doc. Pinned by a test. |
| D5 | Zero expected value, percent mode | `allowed = 0`, so only a number equal to 0 is correct: `0`, `-0` and `0.000`. There is **no** new validator rule rejecting it. | An exact-zero key is still a valid question. Rejecting it would make stored specs of that shape fail on re-save ("existing data must be unaffected"). Documented. |
| D6 | Overflow | Only the admin spec takes part in arithmetic (the #65 fix). `allowed` and the bounds **saturate** at `decimal.MaxValue` / `decimal.MinValue` through guards, not try/catch. The exact code is in Domain behaviour. There are no new validator caps. | Grading must stay total for any stored spec. Validator caps would reject re-saves of existing data. The guards are provably non-throwing (see Domain behaviour). |
| D7 | Negative or missing tolerance, or missing mode, in a stored spec (unreachable after validation) | Tolerance is clamped to `Math.Max(0, tolerance ?? 0)`. A missing mode means absolute. | This keeps `allowed ≥ 0`, which the saturation guards need. A corrupt row degrades to exact match instead of matching nothing. |
| D8 | When is a Fill or Short answer "unanswered"? | Fill: every spec blank either has no response or its text normalises to empty under the question's rules. Short text: the text normalises to empty under the question's rules. Short numeric: the text normalises to empty under the fixed numeric profile (`NumberRules`). All three return `NormalisedGrade.Unanswered`, which is value 0 with `GradeFeedback.Unanswered`. | This mirrors #71 D3 (unanswered vs wrong) and reuses the existing rule "an answer that normalises to empty never matches". |
| D9 | Fill feedback | All blanks hit: `null`. Otherwise (answered, not fully correct) with **2 or more** blanks: `GradeFeedback.BlankTally(hits, blankCount)`. A single-blank Fill that is answered wrong: `null`. | Follows the prototype `grade()` (`prototype/app.js:149`: tally only when there is more than one blank) and #71's "exactly correct gives null". The tally for one blank ("0 of 1") adds nothing to the verdict. |
| D10 | `BlankTally` shape | `new GradeFeedback(GradeFeedbackKind.BlankTally, right, 0, total)`. `Wrong` is always 0 and is not used in the text. | This reuses the #71 record unchanged. Wrong and empty blanks are not separated in the sentence ("Correct blanks: X of Y", per the brief). |
| D11 | Short numeric feedback | Parses and is in tolerance: `null`. Parses and is out of tolerance: `null`, with no direction hint. Normalises to empty: `Unanswered`. Non-empty but does not parse: `GradeFeedback.NotANumber`, "Write the answer as a plain number, without units." | A "too high / too low" hint reveals part of the answer, which #71 D2 forbids. The not-a-number hint reveals nothing, and it explains a 0 for `9.8 m/s` or `9.8e0` under the strict parse rule. |
| D12 | Short text feedback | Unanswered: `Unanswered`. Otherwise `null`. | There is no partial state to explain. |
| D13 | Where the new text lives | Domain `GradeFeedbackKind.BlankTally` and `GradeFeedbackKind.NotANumber`, **appended** to the enum. Application `GradeFeedbackKeys.BlankTally` = `GRADE_FEEDBACK_BLANK_TALLY` and `GradeFeedbackKeys.NotANumber` = `GRADE_FEEDBACK_NOT_A_NUMBER`. Two new `GradeFeedbackText` arms. Two resx entries per language. | This is the same path as #71 D4, which reuses the `ILocalizer.GetMessage(key, context:)` pattern from Morabh `/home/user/apis/Morabh.Application/Orders/AddOnlineOrder/AddOnlineOrderHandler.cs`. The grading logic itself is new, with no Morabh equivalent. |
| D14 | Grader signatures | `TextGrader.GradeFill` and `TextGrader.GradeShort` return `NormalisedGrade`. `QuestionGrader` drops the `new NormalisedGrade(..., null)` wrappers. | This completes #71 D6 ("TextGrader is unchanged … until #72"). |
| D15 | Fill matching details (existing behaviour, now pinned) | Each blank compares only with its **own** accepted list. A response id that is not a spec blank is ignored. A repeated response id uses its first text (`TryAdd`). A blank with no response is a miss. | These are existing behaviours that no test pins yet. |
| D16 | Existing tests whose assertions change | See the Test plan's "modify" rows. Every expected number stays the same. Assertions either switch to `.Value` or get stricter (a full `NormalisedGrade` record). | The contract changed. No assertion is weakened. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/Grading/GradeFeedbackKind.cs` | `public enum GradeFeedbackKind { Unanswered, ChoiceTally, BlankTally, NotANumber }`. |
| `api/Elmanhg.Domain/Questions/Grading/GradeFeedback.cs` | Add `public static GradeFeedback NotANumber { get; } = new(GradeFeedbackKind.NotANumber, 0, 0, 0);` and `public static GradeFeedback BlankTally(int right, int total) { return new(GradeFeedbackKind.BlankTally, right, 0, total); }` after `ChoiceTally`. |
| `api/Elmanhg.Domain/Questions/Grading/TextGrader.cs` | Rewrite per Domain behaviour. Public signatures: `public static NormalisedGrade GradeFill(FillGradingSpec spec, FillAnswer answer)` and `public static NormalisedGrade GradeShort(ShortGradingSpec spec, ShortAnswer answer)`. |
| `api/Elmanhg.Domain/Questions/Grading/QuestionGrader.cs` | Lines 15–16 become `QuestionType.Fill => TextGrader.GradeFill(ReadSpec<FillGradingSpec>(gradingSpec), ReadAnswer<FillAnswer>(answer)),` and `QuestionType.Short => TextGrader.GradeShort(ReadSpec<ShortGradingSpec>(gradingSpec), ReadAnswer<ShortAnswer>(answer)),`. |
| `api/Elmanhg.Application/Questions/Shared/Grading/GradeFeedbackKeys.cs` | Add `public const string BlankTally = "GRADE_FEEDBACK_BLANK_TALLY";` and `public const string NotANumber = "GRADE_FEEDBACK_NOT_A_NUMBER";` after `ChoiceTally`. |
| `api/Elmanhg.Application/Questions/Shared/Grading/GradeFeedbackText.cs` | Two arms before `_`: `GradeFeedbackKind.BlankTally => localizer.GetMessage(GradeFeedbackKeys.BlankTally, context: new Dictionary<string, object> { [GradeFeedbackKeys.RightArgument] = feedback.Right, [GradeFeedbackKeys.TotalArgument] = feedback.Total })` and `GradeFeedbackKind.NotANumber => localizer.GetMessage(GradeFeedbackKeys.NotANumber)`. |
| `api/Elmanhg.Api/Resources/Messages.en.resx` | After line 237: `<data name="GRADE_FEEDBACK_BLANK_TALLY" xml:space="preserve"><value>Correct blanks: {right} of {total}.</value></data>` and `<data name="GRADE_FEEDBACK_NOT_A_NUMBER" xml:space="preserve"><value>Write the answer as a plain number, without units.</value></data>`. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx` | After line 237: `GRADE_FEEDBACK_BLANK_TALLY` → `الفراغات الصحيحة: {right} من {total}.` and `GRADE_FEEDBACK_NOT_A_NUMBER` → `اكتب الإجابة رقمًا فقط، بدون وحدات.` (same `<data>` format). |
| `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs` | Test plan rows T1–T37. |
| `api/Elmanhg.Tests/Domain/Questions/Grading/QuestionGraderTests.cs` | Rows Q1–Q4. |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/GradeFeedbackTextTests.cs` | Rows F1–F2. |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/ShortQuestionRulesTests.cs` | Rows R1–R2. |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/Import/QuestionImportParserTests.cs` | Row P1. |
| `api/Elmanhg.Tests/Integration/Content/QuestionGradeDraftEndpointTests.cs` | Rows I1–I3. |
| `web/src/features/questions/api/questionValues.test.ts` | Row W1: one more `roundTripCases` entry. |
| `docs/question-schemas.md` | The edits in the Docs section below. |

## Files to create
None. Every change goes into an existing file.

## Error codes
None new. Feedback keys are not error codes:

| Key | en | ar |
|-----|----|----|
| `GRADE_FEEDBACK_BLANK_TALLY` | Correct blanks: {right} of {total}. | الفراغات الصحيحة: {right} من {total}. |
| `GRADE_FEEDBACK_NOT_A_NUMBER` | Write the answer as a plain number, without units. | اكتب الإجابة رقمًا فقط، بدون وحدات. |

## Domain behaviour
`TextGrader` (namespace `Elmanhg.Domain.Questions.Grading`, `public static class`). Keep the existing constants and `NumberRules`, and add nothing else to the public surface. The expected body:

```csharp
public static NormalisedGrade GradeFill(FillGradingSpec spec, FillAnswer answer)
{
    var blanks = spec.Blanks ?? [];
    if (blanks.Count == 0) return new NormalisedGrade(0m, null);           // unreachable after validation
    var rules = spec.Normalization ?? AnswerNormalization.Default;
    var responses = /* existing Dictionary<string,string?> + TryAdd loop, unchanged */;
    var normalised = blanks
        .Select(x => x.Id is not null && responses.TryGetValue(x.Id, out var text) ? AnswerNormalizer.Normalize(text, rules) : string.Empty)
        .ToList();
    if (normalised.All(x => x.Length == 0)) return NormalisedGrade.Unanswered;
    var hits = blanks.Where((blank, index) => IsAccepted(normalised[index], blank.AcceptedAnswers, rules)).Count();
    if (hits == blanks.Count) return new NormalisedGrade(1m, null);
    return new NormalisedGrade((decimal)hits / blanks.Count, blanks.Count > 1 ? GradeFeedback.BlankTally(hits, blanks.Count) : null);
}

public static NormalisedGrade GradeShort(ShortGradingSpec spec, ShortAnswer answer)
{
    if (spec.Value is not null) return GradeNumeric(spec.Value.Value, spec.Tolerance, spec.ToleranceMode, answer.Text);
    var rules = spec.Normalization ?? AnswerNormalization.Default;
    var normalised = AnswerNormalizer.Normalize(answer.Text, rules);
    if (normalised.Length == 0) return NormalisedGrade.Unanswered;
    return new NormalisedGrade(IsAccepted(normalised, spec.AcceptedAnswers, rules) ? 1m : 0m, null);
}

private static NormalisedGrade GradeNumeric(decimal value, decimal? tolerance, ToleranceMode? mode, string? text)
{
    var normalised = AnswerNormalizer.Normalize(text, NumberRules);
    if (normalised.Length == 0) return NormalisedGrade.Unanswered;
    if (!TryParseNumber(normalised, out var number)) return new NormalisedGrade(0m, GradeFeedback.NotANumber);
    var allowed = AllowedDifference(value, tolerance, mode);
    // allowed is in [0, decimal.MaxValue], so MinValue + allowed and MaxValue - allowed never overflow.
    var lower = value < decimal.MinValue + allowed ? decimal.MinValue : value - allowed;
    var upper = value > decimal.MaxValue - allowed ? decimal.MaxValue : value + allowed;
    return new NormalisedGrade(number >= lower && number <= upper ? 1m : 0m, null);
}

private static decimal AllowedDifference(decimal value, decimal? tolerance, ToleranceMode? mode)
{
    var amount = Math.Max(0m, tolerance.GetValueOrDefault());
    if (mode != ToleranceMode.Percent) return amount;
    var fraction = amount / PercentDivisor;                                  // never overflows
    var magnitude = Math.Abs(value);
    // Saturate when |value| × fraction would pass decimal.MaxValue; for fraction ≤ 1 the product cannot overflow.
    return fraction > 1m && magnitude >= decimal.MaxValue / fraction ? decimal.MaxValue : magnitude * fraction;
}

private static bool IsAccepted(string normalisedAnswer, List<string>? accepted, AnswerNormalization rules)
{
    return normalisedAnswer.Length > 0 && (accepted ?? []).Any(x => AnswerNormalizer.Normalize(x, rules) == normalisedAnswer);
}

private static bool TryParseNumber(string normalised, out decimal value)
{
    var candidate = normalised
        .Replace(ArabicThousandsSeparator.ToString(), string.Empty)
        .Replace(ArabicDecimalSeparator, '.')
        .Replace(',', '.')
        .Replace(MinusSign, '-');
    return decimal.TryParse(candidate, PlainDecimal, CultureInfo.InvariantCulture, out value);
}
```
- The old `Matches` method and the old two-argument `AllowedDifference(ShortGradingSpec)` are removed.
- A one-line comment on `GradeNumeric` states the invariant: the student number is never an arithmetic operand (the #65 fix).
- `QuestionGrade.FromNormalised` is unchanged.

## API surface
No change. `POST /api/questions/grade-draft` (`Content.Manage`) already returns `feedback: string | null`. It now also returns it for Fill and Short. No OpenAPI, Orval or Postman change.

## Docs (`docs/question-schemas.md`)
| Section | Edit |
|---|---|
| Per-type shapes, after "`toleranceMode` is `absolute` (±tolerance) or `percent` (±tolerance % of the value)." | Append: "A spec has exactly one `tolerance` and one `toleranceMode`; absolute and percent cannot be combined." |
| § Grading, **Fill** bullet | Replace with: "**Fill**: `hits / blanks`. Each blank is compared only with its own accepted answers; it hits when its normalised answer equals any of them. A blank the answer leaves out, or whose text normalises to empty, is a miss. A response whose id is not a blank is ignored; a repeated id uses its first response." |
| § Grading, **Short numeric** bullet | Replace with: "**Short numeric** (the spec has `value`): the answer is parsed as a number and is correct when `value − allowed ≤ answer ≤ value + allowed` (both bounds inclusive). `allowed` is `tolerance` (`absolute`) or `|value| × tolerance / 100` (`percent`). A negative `value` gets the same band as its magnitude, and a `value` of 0 with `percent` accepts only 0. A missing or negative tolerance counts as 0 and a missing mode as `absolute` (unreachable after validation). Only the spec takes part in the arithmetic; when `allowed` or a bound would pass the decimal range (±79228162514264337593543950335) it is clamped to that limit, so grading never fails." |
| § Grading, **Feedback** sub-bullets | Replace the first bullet with: "An unanswered answer returns «لم تتم الإجابة عن السؤال.» / "No answer was given." An answer is unanswered when: Mcq `optionId` or TrueFalse `value` is missing; Multi has no non-null id; every Fill blank is missing or normalises to empty; a text Short answer normalises to empty; a numeric Short answer normalises to empty under the numeric profile." Keep the Multi bullet. Add two bullets: "A Fill answer with two or more blanks that is not fully correct returns «الفراغات الصحيحة: {right} من {total}.» / "Correct blanks: {right} of {total}." A single-blank Fill returns `null`." and "A numeric Short answer that does not parse as a number returns «اكتب الإجابة رقمًا فقط، بدون وحدات.» / "Write the answer as a plain number, without units." A number outside the tolerance returns `null`: a direction hint would reveal part of the answer." |
| § Grading, Feedback closing sentence | Delete "Fill and Short return no feedback." Keep "The feedback never contains the verdict, the correct answer or the explanation." |

`docs/PRD.md` §6, `docs/question-import.md` and `docs/claude-design-prompt.md` §4 already agree. Do not edit them.

## Test plan
All names follow `Method_Scenario_Expected`. Some inputs need Unicode escapes (`‏`, `َ`, `−`). Write them as escape sequences in the C# source. When you use Write or Edit, type `\\u` in the tool argument, or the tool decodes it (PROGRESS gotcha). Large decimal values are passed as `string` in `InlineData` and read with `decimal.Parse(x, CultureInfo.InvariantCulture)`.

### `TextGraderTests` (Domain)
Kind: **modify** changes an existing test, **new** adds one, **delete** removes one.

| # | Kind | Test method | Asserts |
|---|---|---|---|
| T1 | modify (rename from `GradeFill_AllBlanksRight_ReturnsOne`) | `GradeFill_AllBlanksRight_ReturnsOneWithoutFeedback` | `TwoBlanks()` answered `("1","20"),("2","5")` returns `new NormalisedGrade(1m, null)`. |
| T2 | modify (rename from `GradeFill_OneOfTwoBlanksRight_ReturnsHalf`) | `GradeFill_OneOfTwoBlanksRight_ReturnsHalfWithBlankTally` | `("1","20"),("2","7")` returns `new NormalisedGrade(0.5m, GradeFeedback.BlankTally(1, 2))`. |
| T3 | new | `GradeFill_NoBlankRight_ReturnsZeroWithBlankTally` | `("1","7"),("2","x")` returns `(0m, BlankTally(0, 2))`. |
| T4 | new | `GradeFill_OtherBlankEmpty_CountsAsMiss` | `("1","20"),("2","  ")` returns `(0.5m, BlankTally(1, 2))`. |
| T5 | new | `GradeFill_ThreeBlanksTwoRight_ReturnsTwoThirdsWithTally` | Spec blanks 1:`["a"]`, 2:`["b"]`, 3:`["c"]`; answer `a`, `b`, `z`. Returns `(2m / 3m, BlankTally(2, 3))`. |
| T6 | new | `GradeFill_SingleBlankWrong_ReturnsZeroWithoutFeedback` | `Capital(AnswerNormalization.Default)` with `("1","الجيزة")` returns `(0m, null)`. |
| T7 | new | `GradeFill_SecondAcceptedAnswerOfBlank_Matches` | `("1","20"),("2","Five")` returns `(1m, null)`. |
| T8 | new | `GradeFill_AnswerAcceptedOnlyForOtherBlank_DoesNotMatch` | `("1","5"),("2","20")` returns `(0m, BlankTally(0, 2))`. |
| T9 | new | `GradeFill_RepeatedResponseId_UsesFirstResponse` | `("1","20"),("1","7"),("2","5")` returns `(1m, null)`. |
| T10 | modify (rename from `GradeFill_EmptyAnswer_DoesNotMatch`) | `GradeFill_EmptyAnswer_ReturnsUnanswered` | The existing spec `[" "]` with answer `"  "` returns `NormalisedGrade.Unanswered`. |
| T11 | new | `GradeFill_NoBlanksInAnswer_ReturnsUnanswered` | `new FillAnswer(null)` returns `NormalisedGrade.Unanswered`. |
| T12 | new | `GradeFill_OnlyUnknownBlankIds_ReturnsUnanswered` | `("9","20")` returns `NormalisedGrade.Unanswered`. |
| T13 | modify | `GradeFill_SpellingVariantWithUnifyOn_Matches`, `GradeFill_SpellingVariantWithUnifyOff_DoesNotMatch`, `GradeFill_AnswerWithByteSwappedBom_ReturnsOne`, `GradeFill_NullNormalization_UsesDefaultRules` | Each switches to `.Value.Should().Be(<same number>)`. |
| T14 | modify | `GradeShort_NumericWithinAbsoluteTolerance_ReturnsOne`, `GradeShort_NumericWithinPercentTolerance_ReturnsOne`, `GradeShort_NumericWithArabicThousandsSeparator_ReturnsOne`, `GradeShort_NumericWithRightToLeftMark_ReturnsOne`, `GradeShort_NumericWithArabicComma_ReturnsOne`, `GradeShort_NumericIgnoresLetterRules_ParsesArabicDigits`, `GradeShort_TextNullNormalization_UsesDefaultRules`, `GradeShort_TextFoldCaseOff_DoesNotMatchOtherCase`, `GradeShort_InvisibleControlInsideHamza_MatchesComposedAccepted`, `GradeShort_NumericAnswerAtDecimalLimit_ReturnsZero` | Each switches to `.Value.Should().Be(<same number>)`. |
| T15 | modify | `GradeShort_NumericOutsideTolerance_ReturnsZero` | Returns the full record `(0m, null)`: no hint out of tolerance. |
| T16 | modify | `GradeShort_TextAcceptedAfterNormalisation_ReturnsOne` | Returns `(1m, null)`. |
| T17 | modify | `GradeShort_TextNotAccepted_ReturnsZero` | Returns `(0m, null)`. |
| T18 | delete | `GradeShort_NumericNotANumber_ReturnsZero` and `GradeShort_NumericWithExponent_ReturnsZero` | Replaced by T19. |
| T19 | new Theory | `GradeShort_NumericUnparseable_ReturnsNotANumber` | Inputs `"9.8 m/s"`, `"9.8e0"`, `"1 000"`, `"abc"`, `"--1"` against `Numeric(9.8m, 0.1m, Absolute)`. Each returns `new NormalisedGrade(0m, GradeFeedback.NotANumber)`. |
| T20 | new Theory | `GradeShort_NumericEmptyAnswer_ReturnsUnanswered` | Inputs `null`, `""`, `"   "`, `"‏"` return `NormalisedGrade.Unanswered`. |
| T21 | new Theory | `GradeShort_NumericAtAbsoluteBounds_IsInclusive` | `Numeric(9.8m, 0.1m, Absolute)` with `"9.7"` and `"9.9"` returns `(1m, null)`. |
| T22 | new Theory | `GradeShort_NumericJustOutsideAbsoluteBounds_ReturnsZero` | `"9.69"` and `"9.91"` return `(0m, null)`. |
| T23 | new Theory | `GradeShort_NumericAtPercentBounds_IsInclusive` | `Numeric(200m, 5m, Percent)` with `"190"` and `"210"` returns `.Value` 1m. |
| T24 | new Theory | `GradeShort_NumericJustOutsidePercentBounds_ReturnsZero` | `"189.99"` and `"210.01"` return `.Value` 0m. |
| T25 | new Theory `(string text, bool correct)` | `GradeShort_NegativeValueWithPercentTolerance_UsesMagnitude` | `Numeric(-200m, 5m, Percent)`: `("-190",true)`, `("−210",true)`, `("-189",false)`, `("190",false)`. Asserts `.Value == (correct ? 1m : 0m)`. |
| T26 | new Theory | `GradeShort_NegativeValueWithAbsoluteTolerance_IsInclusive` | `Numeric(-9.8m, 0.1m, Absolute)`: `("-9.9",true)`, `("-9.7",true)`, `("9.8",false)`. |
| T27 | new Theory | `GradeShort_ZeroValueWithPercentTolerance_AcceptsOnlyZero` | `Numeric(0m, 10m, Percent)`: `("0",true)`, `("-0",true)`, `("0.000",true)`, `("0.001",false)`, `("-0.001",false)`. |
| T28 | new Theory | `GradeShort_ZeroTolerance_RequiresExactValue` | `Numeric(9.8m, 0m, Absolute)`: `("9.80",true)`, `("9.81",false)`. |
| T29 | new Theory `(string value, string text, bool correct)` | `GradeShort_SpecValueAtDecimalMax_DoesNotOverflow` | value `"79228162514264337593543950335"`, tolerance 1, Absolute: `…335` true, `…334` true, `…333` false. |
| T30 | new Theory | `GradeShort_SpecValueAtDecimalMin_DoesNotOverflow` | value `"-79228162514264337593543950335"`, tolerance 1, Absolute: `-…335` true, `-…333` false. |
| T31 | new Theory | `GradeShort_AbsoluteToleranceAtDecimalMax_AcceptsEveryNumber` | `Numeric(9.8m, decimal.MaxValue, Absolute)`: `"-79228162514264337593543950335"` and `"79228162514264337593543950335"` both return `.Value` 1m. |
| T32 | new | `GradeShort_PercentProductOverflows_SaturatesBounds` | `Numeric(decimal.MaxValue, 200m, Percent)` with `"-79228162514264337593543950335"` returns `.Value` 1m and no exception. |
| T33 | new Theory `(string value)` | `GradeShort_PercentNearOverflowThreshold_DoesNotThrow` | Values `"39614081257132168796771975167"` and `"39614081257132168796771975168"`, tolerance 200, Percent. Answer `"-39614081257132168796771975167"` returns `.Value` 1m. |
| T34 | new Theory `(ToleranceMode mode, string text, bool correct)` | `GradeShort_NegativeToleranceInStoredSpec_TreatedAsZero` | `new ShortGradingSpec(9.8m, -1m, mode, null, null)`: `(Absolute,"9.8",true)`, `(Percent,"9.8",true)`, `(Absolute,"9.79",false)`. |
| T35 | new Theory | `GradeShort_MissingToleranceAndMode_RequiresExactValue` | `new ShortGradingSpec(9.8m, null, null, null, null)`: `("9.8",true)`, `("9.81",false)`. |
| T36 | new | `GradeShort_TextMatchesAnyAcceptedAnswer` | `new ShortGradingSpec(null, null, null, ["ماء", "H2O"], AnswerNormalization.Default)` with `"h2o"` returns `(1m, null)`. |
| T37 | new Theory | `GradeShort_TextEmptyAnswer_ReturnsUnanswered` | `Text("ماء")` with `null`, `""`, `"   "`, `"َ"` (a lone fatha, stripped by tashkeel) returns `NormalisedGrade.Unanswered`. |

### `QuestionGraderTests` (Domain)
| # | Kind | Test method | Asserts |
|---|---|---|---|
| Q1 | modify (rename from `Grade_FillHalf_ReturnsPartial`) | `Grade_FillHalf_ReturnsPartialWithBlankTally` | Same input. Returns `new QuestionGrade(0.5m, 0.5m, GradeOutcome.Partial, GradeFeedback.BlankTally(1, 2))`. |
| Q2 | new | `Grade_ShortPercentJson_ReadsPercentMode` | Spec `{"value":200,"tolerance":5,"toleranceMode":"percent"}`, maxScore 2, answer `{"text":"210"}`. Returns `new QuestionGrade(2m, 1m, GradeOutcome.Correct, null)`. |
| Q3 | new | `Grade_ShortNumericWithUnit_ReturnsNotANumberFeedback` | Spec `{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}`, answer `{"text":"9.8 m/s"}`. Returns `new QuestionGrade(0m, 0m, GradeOutcome.Incorrect, GradeFeedback.NotANumber)`. |
| Q4 | new | `Grade_ShortTextUnanswered_ReturnsUnansweredFeedback` | Spec `{"acceptedAnswers":["ماء"]}`, answer `{}`. Returns `new QuestionGrade(0m, 0m, GradeOutcome.Incorrect, GradeFeedback.Unanswered)`. |

### `GradeFeedbackTextTests` (Application)
| # | Test method | Asserts |
|---|---|---|
| F1 | `Localize_BlankTally_PassesRightAndTotal` | The substitute for `_localizer.GetMessage(GradeFeedbackKeys.BlankTally, Any, Arg.Is(ctx => ctx != null && ctx["right"].Equals(1) && ctx["total"].Equals(3) && ctx.Count == 2))` returns `"blanks"`. `Localize(GradeFeedback.BlankTally(1, 3), _localizer)` returns `"blanks"`. |
| F2 | `Localize_NotANumber_UsesNotANumberKey` | The substitute for key `GradeFeedbackKeys.NotANumber` returns `"nan"`. `Localize(GradeFeedback.NotANumber, _localizer)` returns `"nan"`. |


### `ShortQuestionRulesTests` (Application)
| # | Test method | Asserts |
|---|---|---|
| R1 | `Validate_PercentToleranceOnNegativeValue_ReturnsNoErrors` | `Validate(Json(Numeric), Json("""{"value":-200,"tolerance":5,"toleranceMode":"percent"}"""), _options)` is empty. |
| R2 | `Validate_PercentToleranceOnZeroValue_ReturnsNoErrors` | `{"value":0,"tolerance":10,"toleranceMode":"percent"}` is empty (D5: not rejected). |

### `QuestionImportParserTests` (Application)
| # | Test method | Asserts |
|---|---|---|
| P1 | `ParseAsync_NumericShortRowWithPercentTolerance_KeepsPercentMode` | `Sheet("Short", ["stem","answer_kind","value","tolerance","tolerance_mode","difficulty"], ["g = ?","numeric","-200","5","Percent","medium"])`. `parse.Errors` is empty. The spec has `value` -200m, `tolerance` 5m and `toleranceMode` `"percent"`. |

### `QuestionGradeDraftEndpointTests` (Integration, real PostgreSQL)
| # | Test method | Asserts |
|---|---|---|
| I1 | `Post_FillPartialInEnglish_ReturnsBlankTallyFeedback` | `Accept-Language: en`. `Draft("Fill", "<p>[[1]] + [[2]]</p>", {"blanks":[{"id":"1"},{"id":"2"}]}, {"blanks":[{"id":"1","acceptedAnswers":["20"]},{"id":"2","acceptedAnswers":["5"]}]}, {"blanks":[{"id":"1","text":"20"},{"id":"2","text":"7"}]})`. Returns 200, `outcome` `"Partial"`, `feedback` `"Correct blanks: 1 of 2."`. |
| I2 | `Post_ShortNumericWithUnitInArabic_ReturnsNotANumberFeedback` | `Accept-Language: ar`. Short numeric draft `{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}` with answer `{"text":"9.8 m/s"}`. Returns 200, `outcome` `"Incorrect"`, `feedback` `"اكتب الإجابة رقمًا فقط، بدون وحدات."`. |
| I3 | `Post_ShortNumericSpecAtDecimalLimit_ReturnsCorrect` | Short numeric draft `{"value":79228162514264337593543950335,"tolerance":1,"toleranceMode":"absolute"}` with answer `{"text":"79228162514264337593543950335"}`. Returns 200 (it was 500) and `outcome` `"Correct"`. |

### Web (`questionValues.test.ts`, Vitest)
| # | Test | Asserts |
|---|---|---|
| W1 | Add to `roundTripCases`: `detail({ type: 'Short', body: { answerKind: 'numeric' }, gradingSpec: { value: -200, tolerance: 5, toleranceMode: 'percent' } })` | The existing `it.each(roundTripCases)('round-trips a stored question of each type')` covers percent mode and a negative value through the editor mapping. |

## Definition of done
- [ ] `TextGrader.GradeFill` and `GradeShort` return `NormalisedGrade`. `QuestionGrader` has no `new NormalisedGrade(..., null)` wrapper left.
- [ ] Fill: per-blank accepted lists, per-blank credit `hits / blanks`. Tally feedback only when there are 2 or more blanks and the answer is not fully correct. `Unanswered` when every blank is missing or empty.
- [ ] Short numeric: absolute and percent tolerance, inclusive bounds, `|value|` for percent, only 0 for a zero value in percent mode.
- [ ] Short numeric: no exception for any spec value or tolerance in the decimal range (T29–T33, I3). The student number is never an arithmetic operand.
- [ ] Short numeric: `NotANumber` for unparseable input, `null` out of tolerance, `Unanswered` when empty.
- [ ] Short text: accepted list after normalisation. `Unanswered` when empty, otherwise `null` feedback.
- [ ] `GradeFeedbackKind` values are appended: `Unanswered` = 0 and `ChoiceTally` = 1 are unchanged.
- [ ] `GradeFeedbackKeys`, `GradeFeedbackText` and both resx files contain the two new keys with the exact strings above.
- [ ] No schema, validator, migration, endpoint, OpenAPI, Orval or Postman change. No web component change.
- [ ] `docs/question-schemas.md` edited exactly as in the Docs table, and "Fill and Short return no feedback." is gone.
- [ ] Every test T1–T37, Q1–Q4, F1–F2, R1–R2, P1, I1–I3 and W1 exists with the listed name and assertion. No existing expected number changed.
- [ ] `dotnet test api/ -c Release` is green with `appsettings.json` moved aside. `npm --prefix web run test`, `typecheck`, `lint` and `format:check` are green.
