# Implementation — [E3.S2] Admin question editor with live preview and test grader (#65), rework round 2

## Findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Added an OpenAPI schema transformer that strips `null` members (C# null or JSON-null `JsonNode`) from every `enum` array, so nullability now lives only on the `oneOf` at the use sites. It is Microsoft.OpenApi v2 `JsonNode`, per SKILL §12. Added `using System.Text.Json;`. | `api/Elmanhg.Api/Program.cs:50-59` |
| 1 | Regenerated: `QuestionType`, `QuestionDifficulty` and `QuestionValidationStatus` now list only strings, and no bare `null` enum member is left in the document. | `api/openapi/v1.json` |
| 1 | Restored O1 to the planned exact-equality assertion. The `ValueKind` filter and the `NotContain(Number)` line are gone. | `api/Elmanhg.Tests/Integration/OpenApi/OpenApiEndpointTests.cs:45` |
| 1 | Regenerated the Orval client. `QuestionType`, `QuestionDifficulty` and `QuestionValidationStatus` are now plain string unions with no `\| null`. | `web/src/shared/api/generated/model/questionType.ts:8`, `questionDifficulty.ts`, `questionValidationStatus.ts` |
| 1 | Removed `EditorQuestionType` and `EditorQuestionDifficulty`. `toQuestionType` and `toQuestionDifficulty` now return `QuestionType` and `QuestionDifficulty`, as the plan's W-A1 says. | `web/src/features/questions/api/questionOptions.ts:35,39` |
| 1 | `emptyQuestionValues(type: QuestionType)`, as the plan's W-A3 says, with `QuestionType` imported from the generated model. Prettier reformatted the import. | `web/src/features/questions/api/questionValues.ts:1-11` |
| 1 | Checked the `GetQuestions` zod `query: false` override. It is still needed: without it, `tsc` fails in `questions.zod.ts:48-49` with a numeric default on an `int32` string format. This is the same Orval issue as `GetAuditLogs` and has nothing to do with the null enum. I kept the override unchanged. | `web/orval.config.ts` |
| 2 | Numeric Short grading now checks `number >= value - allowed && number <= value + allowed`. The student value is never an operand of the arithmetic. | `api/Elmanhg.Domain/Questions/Grading/TextGrader.cs:38-39` |
| 2 | Added the regression Theory `GradeShort_NumericAnswerAtDecimalLimit_ReturnsZero`, which feeds ±`decimal.MaxValue` text into a 9.8 ± 0.1 spec and expects a score of 0. | `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs:65-71` |

## Files created
None.

## Files modified
All of these files were already in the change set from round 1:
- `Program.cs`
- `v1.json`
- `OpenApiEndpointTests.cs`
- `TextGrader.cs`
- `TextGraderTests.cs`
- `questionOptions.ts`
- `questionValues.ts`
- the generated Orval files

## Deviations
None.

## Build & test
There was no local `api/Elmanhg.Api/appsettings.json`, so these runs match CI.
- `dotnet build api/ -c Release`: Build succeeded, 0 errors. `--no-incremental` on the Api shows the 9 known core-libraries warnings.
- OpenAPI drift: after a forced rebuild, `cmp` shows `v1.json` is byte-identical (NO-DRIFT).
- `dotnet test api/ -c Release`: failed 0, succeeded 776, skipped 0. That is 774 before plus the 2 new Theory cases.
- `dotnet ef migrations has-pending-model-changes`, run with the design connection string: "No changes have been made to the model since the last migration."
- web `gen:tokens` and `gen:api`: OK.
- web `typecheck`, `lint` and `format:check`: all clean.
- web `test --run`: 58 files, 325/325.
- web `build`: built OK.

## Notes for review
- The transformer carries a one-line comment explaining the invariant: null stays on the `oneOf` use sites.
- `value ± allowed` could in theory overflow only for an admin-authored spec near `decimal.MaxValue`. The student input can no longer trigger it.
