VERDICT: APPROVED

# Review — [E3.S2] Admin question editor with live preview and test grader (#65), round 2

## Blocking
None.

## Round-1 findings
### 1. Enum components listed `null`: fixed
- `api/Elmanhg.Api/Program.cs:50-59`: a schema transformer now strips null members (C# null and JSON null) from every `enum`. The one comment states a hidden invariant, which SKILL "No Comments" allows.
- `api/openapi/v1.json`: I compared it with `git show HEAD:api/openapi/v1.json` using a JSON-level diff.
  - HEAD had no null enum members, and the working tree has none either.
  - The changes to existing schemas are only these, all from the feature itself:
    - `QuestionType` and `QuestionDifficulty` went from `{"type":"integer"}` to string enums.
    - `QuestionDetailResult` gained `rejectionReason`.
    - `/api/questions` gained a `get` operation.
  - Six new schemas and three new paths come from the story. No other schema was touched. The textual diff has 531 insertions and 3 deletions.
- Nullability is preserved. Every nullable use site is `oneOf: [{type:null},{$ref}]`: the `type` and `difficulty` fields of the `Create`, `Update` and `GradeQuestionDraft` requests. The `GET /api/questions` `status` and `type` query parameters `$ref` the enums directly.
- `OpenApiEndpointTests.cs:45` is back to exact `Equal("Mcq","Multi","TrueFalse","Fill","Short")`.
- The generated `questionType.ts`, `questionDifficulty.ts` and `questionValidationStatus.ts` are plain string unions.
- `EditorQuestion*` is gone: grep finds no hits in `web/src`. `toQuestionType` and `toQuestionDifficulty` return the generated types (`questionOptions.ts:35,39`), and `emptyQuestionValues(type: QuestionType)` takes one (`questionValues.ts:11`).

### 2. Numeric Short overflow: fixed
- `TextGrader.cs:38-39` compares `number >= v - allowed && number <= v + allowed`, so student input is never an arithmetic operand.
- `TextGraderTests.cs:65-71` feeds ±`decimal.MaxValue` and expects 0. The old code threw `OverflowException` on this input, so the test constrains the fix.

## Non-blocking
- The round-1 non-blocking items still stand: Arabic normalisation edge cases, the `NumberStyles.Float` exponent vs `docs/question-schemas.md` § Grading, stale grade result in the preview, and TH1 being mapping-only.

## Verified
I re-ran every CI step myself.

**API:**

| Check | Result |
|---|---|
| `dotnet build api/ -c Release --no-incremental` | 0 errors, 9 known core-libraries warnings. |
| OpenAPI drift | `v1.json` is byte-identical after the build. |
| `dotnet test api/ -c Release --no-build` | 776/776 passed. |
| `ef migrations has-pending-model-changes` with the design connection string | "No changes have been made to the model since the last migration." |

**Web:**

| Check | Result |
|---|---|
| `gen:tokens` and `gen:api` | No content drift against a pre-run snapshot. |
| `typecheck`, `lint`, `format:check` | All exit 0. |
| CI literal-token and physical-direction greps | Clean. |
| `test --run --coverage` | 58 files, 325/325 passed. |
| `build` | OK. |
| `routeTree.gen.ts` | No drift. |

`git status` is identical before and after all runs. The report's claims are confirmed: "Deviations: None", "Files created: None" and the build and test numbers.
- Postman and docs: round 2 changes no endpoint or behaviour contract, so the round-1 Postman and docs verification stands.

## Test quality
- **O1:** it constrains the contract again.
- **The new TextGrader Theory:** it fails on the old implementation.
