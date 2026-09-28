# Implementation — Graders for fill-in-the-blank and short answer (#72)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| None | | The plan creates no files. |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Questions/Grading/GradeFeedbackKind.cs` | Appended `BlankTally` and `NotANumber`. `Unanswered`=0 and `ChoiceTally`=1 are unchanged. |
| `api/Elmanhg.Domain/Questions/Grading/GradeFeedback.cs` | Added `NotANumber` (static property) and `BlankTally(int right, int total)`. |
| `api/Elmanhg.Domain/Questions/Grading/TextGrader.cs` | `GradeFill` and `GradeShort` now return `NormalisedGrade`. Added unanswered detection, BlankTally and NotANumber feedback, tolerance clamped with `Math.Max(0, …)`, and saturating bounds. Removed `Matches` and the old `AllowedDifference(ShortGradingSpec)`. The new numeric helpers are `Bounds`, `Around` and `SaturatingProduct` (see Deviations). |
| `api/Elmanhg.Domain/Questions/Grading/QuestionGrader.cs` | Removed the `new NormalisedGrade(..., null)` wrappers for Fill and Short. |
| `api/Elmanhg.Application/Questions/Shared/Grading/GradeFeedbackKeys.cs` | Added `BlankTally` and `NotANumber` keys. |
| `api/Elmanhg.Application/Questions/Shared/Grading/GradeFeedbackText.cs` | Added the two switch arms. |
| `api/Elmanhg.Api/Resources/Messages.en.resx` / `Messages.ar.resx` | Added the two strings exactly as in the plan. |
| `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs` | Tests T1–T37. T18 deleted as planned. Escapes (`‏`, `−`, `َ`, `￾`, `أ`…) are verified as literal escapes in the source. |
| `api/Elmanhg.Tests/Domain/Questions/Grading/QuestionGraderTests.cs` | Q1 renamed and made stricter; Q2–Q4 added. |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/GradeFeedbackTextTests.cs` | F1, F2. |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/ShortQuestionRulesTests.cs` | R1, R2. |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/Import/QuestionImportParserTests.cs` | P1. |
| `api/Elmanhg.Tests/Integration/Content/QuestionGradeDraftEndpointTests.cs` | I1–I3. |
| `api/Elmanhg.Tests/Domain/Questions/Grading/EgyptianSpellingVariantsTests.cs` | **Not in plan.** Two assertions changed to `.Value.Should().Be(...)` (see Deviations). |
| `web/src/features/questions/api/questionValues.test.ts` | W1: added a `roundTripCases` entry, formatted by Prettier. |
| `docs/question-schemas.md` | Per-type note, the Fill and Short numeric bullets, and the Feedback bullets. Deleted "Fill and Short return no feedback." |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Saturate `allowed` at `decimal.MaxValue` in `AllowedDifference`, then apply the plan's bound guards. T32 expects `Numeric(decimal.MaxValue, 200m, Percent)` to accept `-79228…335`. | With `allowed` clamped to MaxValue, `lower = MaxValue − MaxValue = 0`, so T32 fails. The true band is `[−MaxValue, 3·MaxValue]`, so T32 is right and clamping `allowed` loses the near bound. | Numeric grading now uses `Bounds(value, amount, mode)`. Absolute tolerance, and percent when the product does not overflow, go through `Around` (the plan's two guards, unchanged). When `|value| × fraction` overflows, the far bound saturates and the near bound is `∓SaturatingProduct(|value|, fraction − 1)`. This uses the same overflow test as the plan (`fraction > 1 && magnitude >= MaxValue / fraction`). The results for all non-overflow inputs are identical to the plan. |
| Docs: "when `allowed` or a bound would pass the decimal range it is clamped". | After the change above, only bounds are clamped. | Wrote "when a bound would pass the decimal range (±…) it is clamped to that limit, so grading never fails." |
| T31 `GradeShort_AbsoluteToleranceAtDecimalMax_AcceptsEveryNumber` uses `Numeric(9.8m, decimal.MaxValue, Absolute)` and expects `-79228…335` to score 1. | `9.8 − MaxValue ≈ −79228…325.2`, which is above `−MaxValue`. The answer is really outside the band, so this fails under the plan's own code as well. | Changed the spec value to `0m`, whose band really is the whole decimal range. The name and both inputs are unchanged. |
| Existing code touched: the list does not include `EgyptianSpellingVariantsTests.cs`. | It calls `TextGrader.GradeShort(...).Should().Be(1m/0m)`. After the return-type change it compiles (object assertion) but 21 cases fail. | Switched both assertions to `.Value.Should().Be(...)`. The expected numbers are unchanged. |
| `TextGrader` code block uses brace-less one-line `if`s and inline comments. | The skill requires braces on every `if` and zero comments except for invariants. | Used braces. Kept only two invariant comments: on `GradeNumeric` (the student number is never an operand) and on `Around` (a non-negative `allowed` keeps the guards overflow-free). |

## Build & test
- `dotnet build api/ -c Release`: 0 errors, 9 warnings (pre-existing, in core-libraries).
- `git status --porcelain api/openapi`: empty (no drift).
- `dotnet test api/ -c Release` (there is no `api/Elmanhg.Api/appsettings.json` in this checkout, matching CI): **Passed. total 1261, failed 0, succeeded 1261.**
- `dotnet list api/ package --vulnerable --include-transitive`: every project reports "no vulnerable packages".
- `dotnet tool restore`: "Restore was successful."
- `dotnet ef migrations has-pending-model-changes …`: "No changes have been made to the model since the last migration."
- `npm --prefix web run gen:api`: exit 0; `git status` of `web/src/shared/api/generated` is clean.
- `typecheck`: clean. `lint`: clean. `format:check`: "All matched files use Prettier code style!" (after running Prettier on the W1 edit).
- `npm --prefix web test -- --run`: 70 files and 399 tests passed.
- `npm --prefix web run build`: built.

## Notes for review
- `TextGrader.cs` is 133 lines, over the ~100 guideline. The main causes are the braces-on-every-`if` rule and the extra bounds helpers. The plan allows no new files, so I did not split it.
- Integration test I3 sends the value `79228162514264337593543950335` as a raw JSON number. It passes validation and returns `Correct`.
- `NormalisedGrade` equality in the tests relies on record equality of `GradeFeedback`; `NotANumber` and `Unanswered` are singletons.
- `PROGRESS.md` was not touched by me; its orchestrator edit is left as it was.
