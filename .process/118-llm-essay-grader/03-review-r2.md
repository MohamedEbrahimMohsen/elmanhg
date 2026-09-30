VERDICT: APPROVED

# Review (round 2): [E14.S2] LLM essay grader (#118)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Application/Questions/Shared/RichTextContent.cs:15`: `HasContent` no longer has a production caller. Both uses in `EssayQuestionRules` moved to `HasGradableText`, so only `RichTextContentTests` calls it now. Delete it, or keep it on purpose as the mirror of web `hasRichTextContent`.
- `api/Elmanhg.Application/Questions/Shared/RichTextContent.cs:12`: the regex approximation of the extractor is disclosed. The one known false positive is an `alt=` inside another attribute's value, and only a direct API caller can reach it. Acceptable.
- An Essay stem that is only an image with an empty alt still extracts to `""`, and the ai rejects it with 400 because of `question` `min_length=1`. This is disclosed in 02-implementation-r2 "Notes for review" and falls outside this finding.
- The web model-answer check still uses `hasRichTextContent`, where any image counts. The server is now stricter, but the editor's alt `min(1)` means the web cannot send an image without alt, so the two sides cannot disagree in practice.

## Verified
- **Blocking #1a.** `GradeEssayHandlerTests.cs:106` `Handle_QuestionMissing_ThrowsQuestionNotFound`: the null question stub is registered before `Seed()`, and `Seed` does not re-stub the repository. The test asserts the code, and through `ShouldNotGrade` (`:179-183`) it asserts that `GradeAsync` and `SaveChangesAsync` both get `DidNotReceive()`. It would fail if the guard at `GradeEssayHandler.cs:29` were removed.
- **Blocking #1b.** `test_prompt_loader.py:50` monkeypatches `importlib.resources.files` to a `tmp_path` holding a `[]` schema and expects `TypeError`. This targets `loader.py:34-35`.
- **Blocking #1c.** `test_essay_grading_eval.py:107-122` is parametrised over four bad references and exercises both raises in `eval/essay_grading.py:34-37`:
  - unknown id and missing id (the key-set raise)
  - 99 and -1 (the range raise)
- **Blocking #1d.** `EssayGradingRequestFactoryTests.cs:54` `Create_NullSpec_ThrowsInvalidOperationException` passes a `"null"` spec.
- **`HasGradableText` (extra change).**
  - It matches `RichTextExtractor.RawText` (`RichTextExtractor.cs:39-50`). The extractor produces text only from text nodes, math (`data-latex`, which yields at least `$…$`) and `img` alt, and it collapses whitespace with `Split(null)`. `HasGradableText`'s whitespace-only alt and `&nbsp;` checks agree with that.
  - The sanitiser keeps `alt` and `data-latex` (`RichTextSanitizer.cs:11`), so a valid answer still passes `Normalize` after sanitising.
  - The regex is NonBacktracking, and the single comment explains why the check exists.
- **No regression.**
  - `HasContent` behaves as before.
  - The error code is unchanged (`QUESTION_MODEL_ANSWER_REQUIRED`, 422), and there are no resx or contract changes.
  - The replaced test `Validate_ImageOnlyModelAnswer_ReturnsNoErrors` encoded the bug. It is correctly split into WithAlt, which passes validation, and WithoutAlt, which returns 422, and a Normalize case for the post-sanitise path was added.
- **Docs agree with the code.** There is no divergence.
  - `docs/question-schemas.md:150` covers the Essay model-answer rule (text, a formula, or an image with non-blank alt, as sent and after sanitising).
  - `docs/essay-grading.md:49` covers the context: alt only, authoring rejects model answers with no text, and the drop is kept as a guard for older revisions.
  - The `docs/essay-grading.md:102` error table matches.
  - PRD §6 does not specify image model answers.
- **Tests re-run with CI parity.** `api/Elmanhg.Api/appsettings.json` was moved to the scratchpad, then restored; I confirmed the restore with `ls`.
  - `dotnet test api/ -c Release`: 3602 passed, 0 failed, which matches the claim.
  - ai `python -m uv run pytest`: 273 passed and 3 skipped (eval without keys).
  - ai `ruff check`, `ruff format --check` (97 files) and `mypy src` (53 files) are clean.
  - `dotnet format --verify-no-changes` on the six touched C# files: exit 0.
- **Deviations table.** It now records the Postman `{{sessionQuestionId}}` variable and the extra non-blocking fix. Both are disclosed and correct.

## Test quality
- `GradeEssayHandlerTests`, the new test: it constrains the handler. Removing the guard causes a `NullReferenceException` on `question.LessonId` instead of the asserted `NotFoundCoreException`.
- `EssayGradingRequestFactoryTests.Create_NullSpec_...`: it constrains the factory (the exception type is asserted).
- `RichTextContentTests.HasGradableText_*`: the boundary rows (`alt=""`, `alt="&nbsp;"`, no alt, and single-quoted alt) would catch a loosened or tightened regex.
- `EssayQuestionRulesTests`: both the Validate and the Normalize paths are covered. Reverting to `HasContent` fails the two WithoutAlt tests, as the implementer claims.
- The ai loader and eval-case tests assert the exception types on the real validators, with no mocks of the unit under test.
