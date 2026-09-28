VERDICT: CHANGES_REQUESTED

# Review — [E3.S2] Admin question editor with live preview and test grader (#65), round 1

## Blocking

### 1. The OpenAPI enum components list `null` as a member. The generated web types inherit it, and the fix was a workaround downstream plus a looser test.
**Where:**
- `api/openapi/v1.json:2545`, `:2669` and `:2679`: `QuestionDifficulty`, `QuestionType` and `QuestionValidationStatus` each have `"enum": [..., null]`.
- `api/Elmanhg.Api/Program.cs:48-49`: `ConfigureHttpJsonOptions` and `AddOpenApi()`, with no schema transformer.
- `web/src/shared/api/generated/model/questionType.ts:8`: `QuestionType = ... | null`. The same happens in `questionDifficulty.ts` and `questionValidationStatus.ts`.
- `web/src/features/questions/api/questionOptions.ts:31-45`: hand-written `EditorQuestionType` and `EditorQuestionDifficulty` duplicate the contract.
- `api/Elmanhg.Tests/Integration/OpenApi/OpenApiEndpointTests.cs:486-487`: O1 now filters out non-string members before asserting.

**Rule:**
- Plan D16 and DoD: "`api/openapi/v1.json` shows string enums; the generated web model has string unions".
- Test plan O1: the enum must equal exactly `["Mcq","Multi","TrueFalse","Fill","Short"]`.
- SKILL §8.11: never weaken a test to go green.
- PROGRESS: generated artifacts are the contract.

**Problem:** This is a defect, not a correct spec. Every nullable use site is already expressed as `oneOf: [{type: null}, {$ref}]`, for example `UpdateQuestionRequest.type` and `GradeQuestionDraftRequest.difficulty`. So the `null` inside the named component is redundant, and it is also wrong: the component now says `null` is a legal `QuestionType`. That leaks into two places:
- Every non-nullable consumer, such as the `status` and `type` query parameters of `GET /api/questions` today, and any future result typed as the enum. #76 and #74 will consume these types.
- The web model. Its answer was to add parallel hand-maintained unions rather than fix the contract.

The plan's O1 assertion would have caught this, and it was loosened instead.

**Failure:**
- `components.schemas.QuestionType.enum[5] === null`.
- In TypeScript, `const t: QuestionType = null` type-checks.
- A generated client or zod schema built from the spec accepts `?type=null` as a valid enum value.

**Fix:**
- Remove `null` from enum arrays at the source, with an OpenAPI schema transformer in `Program.cs`, for example `builder.Services.AddOpenApi(options => options.AddSchemaTransformer(...))`, working on v2 `JsonNode` enum members (SKILL §12). Nullability stays on the `oneOf` at the use sites.
- Regenerate `v1.json` and the Orval client.
- Restore O1 to the planned exact-equality assertion.
- Drop `EditorQuestionType` and `EditorQuestionDifficulty`, returning to the plan's `QuestionType`/`QuestionDifficulty` signatures.
- Check whether the `GetQuestions` zod `query: false` override in `web/orval.config.ts` is still needed after the fix.

### 2. The numeric Short grader throws `OverflowException` (HTTP 500) on a large student answer.
**Where:** `api/Elmanhg.Domain/Questions/Grading/TextGrader.cs:38`, `Math.Abs(number - spec.Value.Value)`.
**Rule:**
- Plan D3: malformed answers are rejected so that "the graders [stay] total".
- Plan D6: anything that does not parse scores 0.
- Review order: Correctness.

**Problem:** `decimal.TryParse` accepts any value up to ±`decimal.MaxValue`. Decimal subtraction then throws on overflow, whatever the checked or unchecked context. The draft validator does not bound the answer text. Attempts (#74) will call this same grader.

**Failure:** `POST /api/questions/grade-draft` returns 500 (an unhandled `OverflowException`, logged as an error) instead of `200 Incorrect`. The request is a numeric Short draft with `{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}` and the answer `{"text":"-79228162514264337593543950335"}`. I reproduced the exception with the same `TryParse` and subtraction.

**Fix:** Compare without subtracting from the student value, for example `number >= value - allowed && number <= value + allowed`. Only the admin-validated spec is on the arithmetic side. Add a TextGrader test for the extreme answer; it should score 0.

## Non-blocking
- **`AnswerNormalizer.cs:37-57`: Arabic edge cases to hand to #70/#71.**
  - There is no `NormalizationForm.FormC` step. Decomposed hamza or madda (ا + U+0654/U+0655/U+0653) is stripped to a bare ا by the diacritic range. So with `unifyLetterVariants` off, a decomposed "أ" no longer equals a precomposed "أ".
  - Bidi and zero-width controls are neither stripped nor treated as whitespace: U+200B–U+200F, U+061C, U+2066–U+2069 and U+FEFF. A pasted `"‏٩٫٧٥"` with an RLM fails to parse and scores 0.
  - The Arabic comma U+060C and the Arabic thousands separator U+066C are not mapped.
  - `","` → `"."` means `"1,000"` grades as 1. This is plan D6 and is documented.
  - Persian ی (U+06CC) is not unified with ي. That belongs to the #71 corpus.
- **`TextGrader.cs:66`:** `NumberStyles.Float` also accepts exponents (`"9.8e0"`). `docs/question-schemas.md` § Grading says "a plain decimal number". Either tighten the style or say so in the doc.
- **`api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs:8-26`:** the constants are literal characters, not escapes. The report's deviation row says they are "written as escapes". The code points are correct: I checked all 21 against D5.
- **`web/src/features/questions/components/QuestionPreviewPanel.tsx:164`:** after the admin edits the draft or the answer, the last grade result stays on screen, which can mislead. Consider resetting the mutation on change.
- **`api/Elmanhg.Domain/Questions/Grading/QuestionGrader.cs:17,27,38`:** the three defensive `InvalidOperationException` throws have no tests. They are unreachable after validation.
- **`api/Elmanhg.Tests/Application/Features/Teachers/GetTeachers/GetTeachersHandlerTests.cs`:** TH1 only checks the mapping. The `Role == Teacher` predicate is constrained only by the integration test TE1.

## Verified
CI checks I re-ran myself:

| Check | Result |
|---|---|
| `dotnet build api/ -c Release` | 0 errors. A `--no-incremental` Api build shows only the 9 known `core-libraries` warnings. |
| OpenAPI drift | `v1.json` is byte-identical after a forced rebuild. |
| `dotnet test api/ -c Release` | Passed, 774/774. There is no local `appsettings.json`, so this matches CI. |
| `dotnet ef migrations has-pending-model-changes` | "No changes have been made to the model since the last migration." |
| `npm run gen:api` and `gen:tokens` | No content drift when re-run. |
| `npm run typecheck`, `lint`, `format:check` | All exit 0. |
| `npm test -- --run --coverage` | 58 files, 325/325 tests. |
| `npm run build` | OK. |

Plan items confirmed:
- **Domain:**
  - `Reject` guards Pending, then assignment, then blank reason, and trims the reason.
  - `Resubmit` needs Rejected, goes through `Update`, then sets Pending and clears the reason, the validator and the validation date.
  - `Approve` behaviour is unchanged.
- **Migration:** it only adds a nullable `text` column. It is the ninth entry in `AppDbContextTests`.
- **Grading:**
  - The graders and normaliser live in Domain and use no regex.
  - The Multi partial-credit formula, the duplicate and unknown-id handling, and the Fill hits/blanks rule match D4.
  - Rounding is 2 and 4 decimals, `AwayFromZero`.
  - The outcome thresholds are correct.
- **Endpoints:**
  - All 4 endpoints carry the planned policies.
  - `ResubmitQuestion` is `IAuditableCommand` `Question.Resubmit` with one `SaveChangesAsync`.
  - `GetQuestions` uses one page query plus one lesson query and one user query, `asNoTracking`, ordered by `UpdationDate` desc.
- **Reuse and config:**
  - `IUserRepository` and `UserRepository` follow the Morabh shape and are registered in DI.
  - The options are in `ContentOptions`, `appsettings.example.json` and `ApiFactory`.
  - All 8 resx keys exist in en and ar.
  - All 44 `QUESTION_*` codes are in the web `common` errors, identical to the resx text in both languages.
- **Serialisation:** both the MVC and the HTTP JSON pipelines now register `JsonStringEnumConverter` (SKILL §8.6).
- **Tests:** every API and web test name in the plan's Test plan exists.
- **Web:**
  - `AuditLogPagination` was moved to `shared/components/Pagination` and deleted from audit.
  - `RichTextEditor` has `compact` and an optional upload.
  - The lesson editor has the two links.
  - No arbitrary values, hex colours or physical-direction utilities appear in `features/questions`. The classes used are design-system tokens.
- **Postman:** "List teachers" is first in Teachers. "List questions", "Grade question draft" and "Resubmit question" follow "Update question", with the planned URLs, methods, bodies and tests. Bearer auth is inherited. The folder description line is present.
- **Docs:** `question-schemas.md`, PRD §5.3 and §6, `audit-log.md`, `claude-design-prompt.md` §4 (DOC1 and the lesson line) and `rich-text.md` are updated and agree with the code, apart from the non-blocking exponent note above.
- **Guard grep:** there is no `DateTime.Now`, `.Result`, `?? throw` or new `catch` in new C#.

## Test quality
- **AnswerNormalizer, ChoiceGrader, TextGrader, QuestionGrader:** strong. The tests pin exact values, including 1/3, 0.67/0.3333 and the "empty never matches" case with a blank accepted answer. They miss the overflow and the bidi-mark cases above.
- **QuestionRejectionTests:** they constrain guard order, the post-guard state and the version and revision counts. Good.
- **GetQuestionsFilterTests:** they compile the real expression and check both a positive and a negative case per filter. Good.
- **GetQuestionsHandlerTests:** the filter is `Arg.Any`, but the name mapping and the null teacher are real assertions. Acceptable, given F1–F7 and L1–L5.
- **ResubmitQuestionHandlerTests:** the success path asserts `Received(1)`, and every throw asserts the exception type, the code and `DidNotReceive`. Good.
- **GetTeachersHandlerTests:** mapping only; see Non-blocking.
- **OpenApiEndpointTests O1:** weakened relative to the plan, so it no longer constrains the contract (Blocking #1).
- **Web page tests:** they assert request bodies and URLs, not only rendering. WE-P2 and WE-P3 check the exact PUT and resubmit bodies, WL6 checks the query params, and WN1 checks the POST body and the navigation. Good.
