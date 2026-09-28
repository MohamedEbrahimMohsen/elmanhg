VERDICT: CHANGES_REQUESTED

# Review — [E3.S1] Question aggregate with typed body and grading spec (#64)

## Blocking

### 1. Short questions accept and store undefined numeric enum values in `answerKind` and `toleranceMode`, which breaks the documented jsonb schema
**Where:** `api/Elmanhg.Application/Questions/Shared/ShortQuestionRules.cs:22-35` (validation), `:41-46` (normalisation). The root cause is `api/Elmanhg.Domain/Questions/Schemas/QuestionJson.cs:12` (`JsonStringEnumConverter` with the default `allowIntegerValues: true`).
**Rule:** plan A-6 ("an unknown `answerKind` … gives `QuestionBodyInvalid`"), plan D4 (`answerKind: "numeric"|"text"`, `toleranceMode: "absolute"|"percent"`), and the docs-sync rule (divergence). `docs/question-schemas.md` § Per-type shapes (line 22: "enum values are camelCase strings"; line 71) and § Rules (line 106: "`numeric` or `text`; any other value is `QUESTION_BODY_INVALID`").
**Problem:** The converter reads any JSON integer into the enum without checking that the value is defined. `AnswerKind is null` is the only check, and every non-`Numeric` value falls into the text branch. `ToleranceMode` is only null-checked. Normalisation then serialises the undefined value back out as a number. The implementer noted this in "Notes for review", but it is not listed as a deviation. It is left for #65/#66, while the plan and the doc both require it to be rejected now. I confirmed the serializer behaviour with a scratch program that uses the same options: reading `{"answerKind":5}` gives `AnswerKind=5` (not Numeric), and writing it back gives `{"answerKind":5}`. Reading `{"toleranceMode":7,...}` writes back `"toleranceMode":7`.
**Failure:** Two inputs show it:
- `POST /api/questions` with `type:"Short"`, `body:{"answerKind":5}`, `gradingSpec:{"acceptedAnswers":["x"]}` returns 200. The jsonb `Body` stored is `{"answerKind":5}`.
- `body:{"answerKind":"numeric"}`, `gradingSpec:{"value":1,"tolerance":0,"toleranceMode":7}` returns 200. The stored spec is `{"value":1,"tolerance":0,"toleranceMode":7}`.

Neither stored shape matches `docs/question-schemas.md`. The student-facing body can therefore carry a value the grader and the #65 editor cannot interpret.
**Fix:** In `ShortQuestionRules.Validate`, add `QuestionBodyInvalid` when `!Enum.IsDefined(shortBody.AnswerKind.Value)`, and treat `spec.ToleranceMode is null || !Enum.IsDefined(spec.ToleranceMode.Value)` as `QuestionToleranceInvalid`. Add one rule test for each (for example `Validate_UndefinedAnswerKindNumber_ReturnsQuestionBodyInvalid` and `Validate_UndefinedToleranceModeNumber_ReturnsQuestionToleranceInvalid`). The other option is `new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)` in `QuestionJson`, but that changes the verbatim D-12 contract and would need a plan note.

## Non-blocking
- `api/Elmanhg.Application/Questions/Shared/QuestionContentFactory.cs:11` and `ChoiceQuestionRules.cs:41-42,54`: text and length rules run on the raw input, and sanitising happens afterwards. Option text such as `"<script>x</script>"` passes `QUESTION_OPTION_TEXT_REQUIRED` and is stored as `""`. The same happens to a stem. The placeholder check in `FillQuestionRules.cs:29-31` also runs on the raw stem, so a `[[id]]` inside stripped markup would pass and then disappear. Only an admin can send this input. Consider validating the sanitised values, or re-checking after sanitising, when #65 builds the editor.
- `api/Elmanhg.Tests/Application/Features/Questions/UpdateQuestion/UpdateQuestionHandlerTests.cs:197`: the sanitiser stub returns its input unchanged, not the `clean:` prefix the plan asks for. This deviation is not reported, but it is forced: with `clean:`, U2 (the metadata-only edit) could not be written, because every re-save would count as a content change. It is the correct choice. It just should have been listed under Deviations.
- `api/Elmanhg.Domain/Questions/Question.Editing.cs:29,37`: two concurrent content edits both compute `Version = 2`. The unique `(QuestionId, Version)` index turns the second one into a `DbUpdateException` (500). Plan D21 accepts this. It is recorded here so #68 picks it up.

## Verified
- Build and tests, re-run by me:
  - `dotnet build api/`: 0 warnings, 0 errors.
  - `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside: Passed, 663/663. The file was restored afterwards.
  - `dotnet format --verify-no-changes`: the only lines reported are in `api/core-libraries` (known noise).
  - `npm --prefix web run build`: built, with only the existing chunk-size warning.
  - `npm --prefix web test -- --run`: 46 files, 240/240 passed. The pre-existing `RichTextEditor.test.tsx` coverage-mode timeout did not appear in this run, and this change does not touch that file or its imports.
- PRD §17 rule 2: `Question.Editing.cs:18-38`. A content edit, compared semantically through `QuestionContent.IsEquivalentTo` and `JsonNode.DeepEquals`, bumps `Version` and appends a revision of the new version in every status. Approved goes back to Pending with `ValidatedBy`/`ValidatedAt` cleared. Rejected stays Rejected (D8). A metadata-only edit changes neither the version nor the revisions. A no-op edit does not stamp the update fields. This matches PRD §5.3 as edited.
- PRD §17 rule 3: `ValidationStatus` has a private setter (`Question.cs:24`). `Create` always sets Pending. `Approve(TeacherSubject)` (`Question.Approval.cs`) is the only transition to Approved, and it checks the assignment is live, for the right subject, and that the question is Pending. No request, command or endpoint carries a status (`Requests.cs`, `QuestionFields.cs`). I3 proves that an extra `validationStatus:"Approved"` in the body is ignored.
- Sanitisation (D5): stem and explanation go through `QuestionContentFactory.cs:11`, and the text of each choice option through `ChoiceQuestionRules.cs:54`. Fill and short accepted answers are only trimmed. Tags are trimmed and de-duplicated case-insensitively.
- jsonb schema per type: the rules in A-3 to A-6 are present with the planned codes, and unknown properties are dropped by re-serialisation. The null-array-element hardening (deviation 2) is present (`x?.Id`, `x?.Text`).
- All 4 reported deviations are confirmed:
  - `QuestionSchemaReader.AddIf` exists.
  - The rules dereference safely (`?.`).
  - S10 uses `DeepEquals`.
  - `AppDbContextTests` gains the `_AddQuestions` entry.
- Every file in *Files to create* exists with the planned signature, and nothing unplanned was added. The EF mapping matches I1 exactly, including the soft-delete filters for `Question` and `QuestionRevision`. The migration has only CreateTable and CreateIndex in `Up`. `IQuestionRepository` is registered.
- The error codes match: 32 application constants and 4 domain constants plus `LessonHasQuestions`, with 37 keys in each resx.
- The 10 D23 caps are in `ContentOptions`, `appsettings.example.json` and `ApiFactory`.
- Lesson delete guard (Published check first, then `LESSON_HAS_QUESTIONS`) and `LessonResult.QuestionCount` are present. The web caption uses the tokens `text-caption text-text-muted`, the same as `UnitItem`. Both en and ar plurals are there, and `errors.LESSON_HAS_QUESTIONS` is in both languages.
- Postman: `questionId` variable, and a "Questions" folder with create lesson, create question (POST `/api/questions`), get (GET `/api/questions/{{questionId}}`) and update (PUT). All inherit the collection Bearer auth and have plausible bodies. The Lessons description mentions the new delete guard.
- Docs:
  - PRD §5.2, §5.3, §6 and §15 are edited as planned.
  - `audit-log.md` has the rows, the `Question` entity and the revision note.
  - `rich-text.md` covers question rich text.
  - `claude-design-prompt.md` §4 shows the lesson question count.
  - `docs/question-schemas.md` is created. It agrees with the code except for the enum-number case in finding 1.
- Guard grep over the new code is clean. The only `catch` is `catch (JsonException)` in `QuestionSchemaReader.cs:26`.
- Every test name in the Test plan exists.

## Test quality
- `QuestionTests` / `QuestionApprovalTests`: these constrain the implementation.
  - The D8 theory proves that each of the 5 content fields triggers the reset.
  - D11 checks reference-equality of `Body` after a reformatted but equivalent edit, which pins the semantic-equality path.
  - D12 pins the no-op path.
  - A5 documents the structural rule that an admin cannot hold a `TeacherSubject`.
- Rule tests (`Choice`, `TrueFalse`, `Fill`, `Short`): each invalid case asserts its specific code, and the Normalize tests assert the exact canonical JSON. There is a gap: nothing covers undefined numeric enum values (finding 1).
- `QuestionFieldsValidatorTests`, `Create`/`Update`/`GetQuestionValidatorTests`: every rule has a failing case, and V2 proves the schema rule is skipped when the type is missing.
- Handler tests: success paths assert real state on the aggregate (status, version, subject, sanitised stem) plus `SaveChangesAsync` `Received(1)`. Every throwing path asserts the exception type, the code and `DidNotReceive()`. No test only asserts a value it stubbed itself.
- `QuestionsEndpointTests`: these check real behaviour against PostgreSQL.
  - I10 re-saves the same content after a jsonb round-trip and proves that the jsonb reformatting in PostgreSQL is not seen as a content edit.
  - I9 checks the audit diff contains `version`.
- W1–W3 check the rendered text, not the stubs.
