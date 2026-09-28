VERDICT: CHANGES_REQUESTED

# Review — Graders for fill-in-the-blank and short answer (#72), round 1

## Blocking

### 1. The new percent-overflow logic from Deviation 1 is not tested: its negative-value branch and its `fraction - 1` product can both be broken with the suite still green
**Where:** `api/Elmanhg.Domain/Questions/Grading/TextGrader.cs:102-103` and `:114-117` (`SaturatingProduct`). The tests that should cover it are `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs` T32 `GradeShort_PercentProductOverflows_SaturatesBounds` and T33 `GradeShort_PercentNearOverflowThreshold_DoesNotThrow`.
**Rule:** skill §9 Tests ("every domain-method branch"); reviewer order #5 ("would each test fail if the code were wrong?").
**Problem:** Deviation 1 replaced the plan's arithmetic with new code: `Bounds` has an overflow branch, `near = -SaturatingProduct(magnitude, fraction - 1m)` and `value < 0m ? (MinValue, -near) : (near, MaxValue)`. The plan's T32 and T33 only reach this branch with a positive `value` and tolerance 200 %. There `fraction - 1 = 1`, so `SaturatingProduct(m, 1)` is just `m`, and the band is symmetric. So these mutants all pass the full suite:
  - `near = -magnitude`, which drops `fraction - 1`;
  - `SaturatingProduct` returning `magnitude`;
  - line 103 always returning `(near, decimal.MaxValue)`, which ignores the sign.

None of these mutants is caught by the 1261 tests.
**Failure:** under the "always `(near, MaxValue)`" mutant, `Numeric(-decimal.MaxValue, 150m, Percent)` with answer `"-79228162514264337593543950335"` returns 0. It should return 1: the band is [MinValue, 0.5·Max]. Under the `near = -magnitude` mutant, `Numeric(decimal.MaxValue, 150m, Percent)` with `"-40000000000000000000000000000"` returns 1. It should return 0: the band starts at -0.5·Max ≈ -3.96e28.
**Fix:** add tests that pin the deviation's new code. I confirmed these expected values against the current code:
- `Numeric(-decimal.MaxValue, 150m, Percent)`:
  - `"-79228162514264337593543950335"` → 1
  - `"39000000000000000000000000000"` → 1
  - `"40000000000000000000000000000"` → 0
- `Numeric(decimal.MaxValue, 150m, Percent)`:
  - `"-39000000000000000000000000000"` → 1
  - `"-40000000000000000000000000000"` → 0
- `Numeric(decimal.MaxValue, 300m, Percent)`: `"-79228162514264337593543950335"` → 1. This exercises the saturating branch of `SaturatingProduct`.

## Non-blocking
- `TextGraderTests.cs` T31 `GradeShort_AbsoluteToleranceAtDecimalMax_AcceptsEveryNumber`: the plan's test was wrong, not the code.
  - With `value` 9.8 and `tolerance` = Max, the band is `[9.8 − Max, 9.8 + Max]`, and `-Max` lies outside it. That follows the inclusive-band rule in `docs/question-schemas.md` § Grading ("value − allowed ≤ answer ≤ value + allowed"), and the plan's own code also returns 0.
  - The replacement `value` 0 is weaker. `0 ± Max` never reaches either saturation guard, so a guard-free `Around` still passes T31. T29 and T30 do cover the guards.
  - Suggest restoring `value` 9.8 with honest expectations: `"79228162514264337593543950335"` → 1, which exercises upper-bound saturation with a huge allowed; `"-79228162514264337593543950325"` → 1; `"-79228162514264337593543950335"` → 0.
- `TextGrader.cs` is 133 lines, against the §1 "~80–100" guideline. I accept it for this story: the plan forbade new files, and the class is cohesive. A follow-up could move `Bounds`, `Around` and `SaturatingProduct` into a `NumericToleranceBand` helper, which would bring it under 100.
- The 02 report calls the `EgyptianSpellingVariantsTests.cs` edit a deviation. It is a `.Value`-only compile fix, and the expected numbers are unchanged. That is fine.

## Verified
- **Deviation 1, adversarial check.** I ran a scratch harness against the real `Elmanhg.Domain` build: 357,782 cases.
  - Inputs: values 0, ±1, ±9.8, ±Max, ±(Max−1), the ±Max/2 threshold values, ±1e-28 and others; tolerances null, 0, -1, -Max, 1e-28, 0.1, 5, 99.99, 100, 100.0000001, 150, 200, 300, 1e26, Max/2, Max; modes null/Absolute/Percent. Answers were ±Max, 0, the value, each exact bound and bound ± epsilon, plus 300k random decimals.
  - Each result was compared with an exact BigInteger reference of the inclusive band.
  - **0 exceptions.**
  - 927 mismatches, all within about one ulp of the spec's magnitude. They come from decimal rounding at 28–29 significant digits (for example, `9.8 + 39614081257132168796771975168` rounds up). The plan's own `Around` path rounds the same way. None is a logic error.
  - Inclusive bounds, negative percent (`|value|`), zero with percent (only 0), and a negative or missing tolerance or mode all behave as documented.
- **Deviation 2 (T31).** The plan's expectation was mathematically wrong (see Non-blocking). The code matches the documented rule.
- **Existing tests.** No existing expected number changed. The edits are `.Value` switches (T13, T14 and EgyptianSpellingVariants) or stricter full-record asserts (T1, T2, T15–T17, Q1). `GradeFill_NullNormalization_UsesDefaultRules` was moved, not changed. T18's two deletions are covered by the T19 theory.
- **Plan rows.** Every row exists with the planned name: T1–T37, Q1–Q4, F1–F2, R1–R2, P1, I1–I3 and W1. Unicode inputs are escape sequences in the source (`−`, `‏`, `َ`).
- **Domain.** The enum is appended (Unanswered=0, ChoiceTally=1). `GradeFeedback.NotANumber` and `BlankTally` are added. `QuestionGrader` has no wrappers left.
- **Localisation.** The keys, the `GradeFeedbackText` arms and both resx strings match the plan exactly.
- **Out-of-scope areas.** No endpoint, OpenAPI, Orval or Postman change was needed or made. `docs/question-schemas.md` has every Docs-table edit, with the wording adjusted for Deviation 1; it matches the code. PRD §6 and `question-import.md` agree. No divergence.
- **CI, re-run by me.**
  - api: build Release 0 errors; no openapi drift; `dotnet test` 1261/1261 (no appsettings.json present); no vulnerable packages; no pending model changes; `dotnet format --verify-no-changes` clean on the touched folders.
  - web: gen:api no drift; typecheck, lint and format:check clean; vitest 70 files / 399 tests; build OK.

## Test quality
- **TextGraderTests.** The Fill tests and the non-overflow numeric tests constrain the code well: full-record asserts, inclusive bounds on both sides, and just-outside cases. They do not constrain the overflow branch of `Bounds` or `SaturatingProduct` (Blocking 1). T31 no longer exercises a guard (Non-blocking).
- **QuestionGraderTests.** Q1–Q4 assert the full `QuestionGrade` and constrain the JSON read and the feedback propagation.
- **GradeFeedbackTextTests.** F1's argument matcher constrains the key, both arguments and the count. F2 is a key-routing check, which is acceptable.
- **ShortQuestionRulesTests (R1, R2) and QuestionImportParserTests (P1).** They pin existing behaviour and would fail on a regression.
- **Integration I1–I3.** They exercise the real localisation and the old 500 path.
