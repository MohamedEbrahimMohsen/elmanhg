VERDICT: APPROVED

# Review r2 — [E3.S1] Question aggregate with typed body and grading spec (#64)

## Blocking
None.

## Round-1 finding 1: resolved
- `api/Elmanhg.Application/Questions/Shared/ShortQuestionRules.cs:28-32`: an undefined `answerKind` value (for example `{"answerKind":5}`) now returns `QUESTION_BODY_INVALID`. The check runs after the null check, so a missing kind still gives `QUESTION_ANSWER_KIND_REQUIRED`. This matches `docs/question-schemas.md` § Rules, line 106 ("any other value is `QUESTION_BODY_INVALID`").
- `ShortQuestionRules.cs:37`: an undefined `toleranceMode` value (for example `7`) now returns `QUESTION_TOLERANCE_INVALID`, the same code as a missing mode. This matches `docs/question-schemas.md` line 108 together with line 71 (`absolute` or `percent`).
- Normalisation (`:45-53`) only runs after validation succeeds, so neither undefined value can reach the jsonb columns any more. The doc did not need an edit. `QuestionJson` is unchanged, so the D-12 converter contract stays as written.
- The only enums inside the jsonb schemas are `ShortAnswerKind` and `ToleranceMode` (`Domain/Questions/Schemas/ShortSchemas.cs:3,5`). No other schema field has the same gap.

## Non-blocking
- The round-1 non-blocking items are unchanged, as rework mode requires. They still stand for #65 and #68.

## Verified
- `dotnet test api/ -c Release`, re-run by me with `appsettings.json` left in place: Passed, 665/665 (663 before plus 2 new).
- The r2 report lists 2 modified files. I read both end to end, and nothing else was changed for this rework. The claim "Deviations: None" holds: the 2 extra tests come from the round-1 fix request.

## Test quality
- `ShortQuestionRulesTests.cs:41` (`Validate_UndefinedAnswerKindNumber_ReturnsQuestionBodyInvalid`): this test would have failed on the round-1 code. There, `5` fell into the text branch, `["x"]` passed, and the result had no errors.
- `ShortQuestionRulesTests.cs:65` (`Validate_UndefinedToleranceModeNumber_ReturnsQuestionToleranceInvalid`): this test would have failed on the round-1 code, which only null-checked the mode.
- Both tests constrain the implementation.
