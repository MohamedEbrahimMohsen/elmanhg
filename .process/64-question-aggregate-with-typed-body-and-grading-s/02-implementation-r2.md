# Implementation r2 — [E3.S1] Question aggregate with typed body and grading spec (#64)

## Rework — blocking findings from 03-review.md

| # | What I changed | file:line |
|---|---|---|
| 1 | `ShortQuestionRules.Validate` now returns `QuestionBodyInvalid` when `answerKind` is not a defined `ShortAnswerKind` value (`!Enum.IsDefined(...)`, placed after the null check). It also returns `QuestionToleranceInvalid` when `toleranceMode` is not a defined `ToleranceMode` value. `QuestionJson` is unchanged, so the D-12 converter contract stays as written. | `api/Elmanhg.Application/Questions/Shared/ShortQuestionRules.cs:28-32`, `:37` |
| 1 | I added two rule tests, one for each field: `Validate_UndefinedAnswerKindNumber_ReturnsQuestionBodyInvalid` (`{"answerKind":5}`) and `Validate_UndefinedToleranceModeNumber_ReturnsQuestionToleranceInvalid` (`{"value":1,"tolerance":0,"toleranceMode":7}`). | `api/Elmanhg.Tests/Application/Features/Questions/Shared/ShortQuestionRulesTests.cs:41`, `:65` |

## Files created
None.

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Application/Questions/Shared/ShortQuestionRules.cs` | Rejects undefined enum values for `answerKind` and `toleranceMode` (54 lines) |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/ShortQuestionRulesTests.cs` | Adds 2 tests |

## Deviations
None. The two new tests are not in the plan's Test plan. The review's finding 1 asked for them.

## Build & test
I moved `api/Elmanhg.Api/appsettings.json` to the scratchpad, ran the tests, then put it back. I checked that the file is back in place.

`dotnet test api/ -c Release`:
```
Test run summary: Passed!
  total: 665
  failed: 0
  succeeded: 665
  skipped: 0
```
That is 663 tests from before plus the 2 new ones.

## Notes for review
- `docs/question-schemas.md` did not need editing. The code now does what it already says: any other value gives `QUESTION_BODY_INVALID`, and enum values are strings.
- I left the non-blocking findings alone, as rework mode requires.
