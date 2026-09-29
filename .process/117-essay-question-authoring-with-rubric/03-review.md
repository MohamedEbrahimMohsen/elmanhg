VERDICT: APPROVED

# Review — [E14.S1] Essay question authoring with rubric (#117)

## Blocking
None.

## Non-blocking
1. `api/Elmanhg.Application/Questions/Shared/EssayQuestionRules.cs:27` — the server only rejects a model answer that is null or whitespace. A model answer like `<p></p>`, or one the sanitizer strips to nothing (line 40), is stored as an empty model answer. The web blocks this with `hasRichTextContent` (`essayRules.ts:64`), and option text uses the same server pattern (`ChoiceQuestionRules.cs:41`). #118 still has to cope with an empty model answer written straight to the API. Worth a follow-up: check emptiness after sanitising.
2. `api/Elmanhg.Tests/Application/Features/Questions/Shared/EssayQuestionRulesTests.cs:83-97` — `QUESTION_RUBRIC_TEXT_TOO_LONG` is tested for the title and a level description, but not for an over-long criterion `description`, which is the third operand at `EssayRubricRules.cs:26`.
3. `web/src/features/questions/schemas/essayRules.ts` — the client does not check the 1000-character rubric text cap. That error only comes back from the server, and it shows once under the rubric legend instead of on the field, which is the mapping the plan asked for.
4. `web/src/features/questions/components/RubricCriterionCard.tsx:23` — "Criterion {n}" is a `span`. A heading (or a `fieldset`/`legend`) would give screen-reader users a way to move between criteria.
5. `web/src/features/questions/api/essayValues.ts:34-36` — if a stored spec fails `essaySpecSchema`, the editor and `EssayRubricView` fall back to the default empty criterion, so a teacher would see an invented rubric. The server stores only canonical specs, so this cannot happen today.
6. `PROGRESS.md:120` ("Servable rule (#67)") still gives the old three-part servable definition. This file belongs to the orchestrator, so it is noted here only.

## Verified
- **Tests pass, run by me.** `dotnet test api/ -c Release` with `appsettings.json` moved aside gave total 3360, failed 0, succeeded 3360. The file was restored afterwards, and the test DLLs are newer than every `.cs` file. Web: `typecheck` is clean, `lint` (with max-warnings 0) is clean, and `vitest --run` passed 176 files and 1033 tests. The prettier check with end-of-line auto passed. All of this matches `02-implementation.md`.
- **Essays are never served.**
  - `ServableQuestionSpecification.cs:10` adds `x.Type != QuestionType.Essay`.
  - Every serving read goes through `WhereServable`/`IsSatisfiedBy`: quiz, unit/multi-unit exam candidates, blueprint counts, lesson counts, the landing count, mastery (`QuestionMasteryRepository`), avatar retrieval (`LessonContentIndexRepository`, `LessonContentChunkRepository`), teacher-thread context, and the admin `isServable` flag.
  - No `ValidationStatus == Approved` filter exists outside the spec.
  - No `Enum.GetValues<QuestionType>()` is left in production code.
- **Blueprints show served types only.** `ServableTypeCounts.ToResults` iterates `ServedTypes`. The web blueprint editor, schema and shortfall iterate `servedQuestionTypes`.
- **The teacher queue includes essays.** The type filter accepts `Essay` (I7). The detail returns the rubric and model answers (I6). Approving an essay leaves it Approved with `isServable false` and `servableQuestionCount 0` (I8).
- **Rubric validation is sound.**
  - `EssayRubricRules` implements all 9 rules in plan order.
  - `IsFullScale` needs non-null, distinct points within 0..p, including both 0 and p.
  - It is gated on in-range criterion points, a declared deviation that avoids a cascading code.
  - Null criteria or levels inside the arrays are handled with null-conditional access.
  - Fractional points fail at read time (A14).
- **Normalize matches the plan.** It trims titles, drops a blank description through `WhenWritingNull`, sorts levels ascending, sanitises model answers, and drops unknown properties (A23, I1).
- **Model answers never reach students.** They live only in the grading spec, which is returned only by the admin question detail and the teacher validation detail. Essays never enter sessions, so `correctAnswer`/avatar paths cannot expose them. The web `toStudentQuestion` carries only `maxWords`.
- **The import no longer 500s on an `Essay` sheet.** `QuestionImportColumns.Types` drives both `TypeForSheet` and the template, and the parser filters sheets through `TypeForSheet` (`QuestionImportParser.cs:18,20`). A27 covers this.
- **Grade-draft refuses essays.** It returns 422 `QUESTION_TYPE_NOT_GRADABLE`, and the answer rule is skipped for Essay (A26, I5). `QuestionGrader` throws for Essay (D3).
- **#118 can grade this rubric shape.** Each criterion has a stable id and weight `points`, with an anchored 0..p level scale stored ascending, plus 1..3 model answers and `maxScore`. This is documented in `docs/question-schemas.md` (Essay block, Score and Level scale bullets).
- **Contract fidelity.**
  - All 19 files in *Files to create* exist, and no extra production files were added.
  - The 14 error codes appear in `ErrorCodes`, both resx files and both web `shared/i18n` files.
  - All 7 `ContentOptions` caps are in place, with code defaults, in `appsettings.example.json` and in `ApiFactory`.
  - The OpenAPI and Orval output were regenerated (Essay enum).
  - The web i18n key sets for `ar` and `en` in `questions` are identical (checked by script).
- **Declared deviations are justified.**
  - The `OpenApiEndpointTests` enum list gains `"Essay"`, a behaviour change the plan requires.
  - The servable-definition docs fix in `content-retrieval.md`, `mastery.md` and `claude-design-prompt.md` §business rules prevents divergence.
  - `role="group"` on the model-answer container is needed for axe.
  - Nine rubric codes are mapped, not eight; the error table in the plan lists nine.
- **UI matches the plan and the design system.**
  - The editor has the word limit, the rubric (criteria, levels, add/remove within limits, a total-points line) and 1..3 model answers.
  - The preview shows the essay box with a word count, and a note replaces "Try the answer".
  - The teacher page shows `EssayRubricView`.
  - Every class is a token or an existing pattern (`TextAreaField` classes, `mt-6` as in `FillBlanksField`).
  - Layout uses logical properties only, and there are no hard-coded strings.
- **Postman.** "Create essay question" sits right after "Create question", with the same method, URL and header (collection-level auth). It sends the I1 body from the plan and stores `essayQuestionId`, which is declared as a collection variable.
- **Docs are in sync.** The PRD (§5.3, §17.1, §19 Q7), `question-schemas.md`, `exam-blueprints.md`, `question-import.md`, `claude-design-prompt.md` §4 and `prototype.md` all agree with the code. No divergence found.

## Test quality
- `EssayQuestionRulesTests`: this constrains the code. Each rule has a failing input that isolates it, the theory rows cover every scale defect, and Normalize is asserted by DeepEquals against the canonical spec. Using `Contain` is acceptable because each input triggers only its target rule.
- `ServableQuestionSpecificationTests` D1/D2 and `ServableTypeCountsTests` A28 constrain the code. The implementer says mutation checks were run.
- `GradeQuestionDraftValidatorTests` A26, `QuestionImportColumnsTests` A27, `QuestionFieldsValidatorTests` A25 (property `Body`) and `QuestionGraderTests` D3 all constrain the code.
- `EssayQuestionEndpointTests` I1–I5 and `EssayValidationEndpointTests` I6–I8 constrain the code. They use a real DB and HTTP, I8 covers both the SQL and the in-memory servability paths, and I2 asserts nothing was persisted.
- Web W1–W27 all constrain the code: request payload shape, error paths and messages, disabled limits, preview branch, rubric rendering, RTL and axe. None of them only asserts the return value of a mock.
