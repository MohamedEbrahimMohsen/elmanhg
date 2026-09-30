# Implementation (rework 2): [E14.S2] LLM essay grader (#118)

## Blocking findings addressed

| # | What I changed | file:line |
|---|---|---|
| 1a | Added `Handle_QuestionMissing_ThrowsQuestionNotFound`. It stubs the revisions, makes the question repository return null, and asserts `NotFoundCoreException` with `QUESTION_NOT_FOUND`. Through `ShouldNotGrade` it also asserts that neither the client nor `SaveChangesAsync` is called. | `api/Elmanhg.Tests/Application/Features/EssayGrading/GradeEssay/GradeEssayHandlerTests.cs:106` |
| 1b | Added `test_load_output_schema_non_object_raises_type_error`. It monkeypatches `importlib.resources.files` to a `tmp_path` holding a `[]` schema. | `ai/tests/unit/test_prompt_loader.py:50` |
| 1c | Added `test_essay_eval_case_bad_reference_raises_validation_error`, parametrised over four bad references: an unknown id, a missing id, a value above the criterion maximum, and a negative value. It calls `EssayEvalCase.model_validate` and expects `ValidationError`. | `ai/tests/unit/test_essay_grading_eval.py:116` |
| 1d | Added `Create_NullSpec_ThrowsInvalidOperationException`, which passes a `"null"` spec. | `api/Elmanhg.Tests/Application/Features/EssayGrading/Shared/EssayGradingRequestFactoryTests.cs:54` |

### Mutant kills (each mutant was applied, the suite was run, and the source was restored; I confirmed the restore with `cmp`/`git diff`)
| Mutant | Failing test(s) |
|---|---|
| `GradeEssayHandler.cs:29`: `?? throw new NotFoundCoreException(...)` replaced by `?? null!` | `Handle_QuestionMissing_ThrowsQuestionNotFound` |
| `EssayGradingRequestFactory.cs:12`: `?? throw new InvalidOperationException(...)` replaced by `?? null!` | `Create_NullSpec_ThrowsInvalidOperationException` |
| `loader.py:34-35`: `raise TypeError` replaced by `pass` | `test_load_output_schema_non_object_raises_type_error` |
| `essay_grading.py`: keys `raise ValueError` replaced by `pass` | `[unknown-criterion-id]`, `[missing-criterion-id]` |
| `essay_grading.py`: range `raise ValueError` replaced by `pass` | `[above-criterion-points]`, `[negative-points]` |
| `EssayQuestionRules.cs`: `HasGradableText` reverted to `HasContent` | `Validate_ImageOnlyModelAnswerWithoutAlt_...`, `Normalize_ModelAnswerImageWithoutAltAfterSanitising_...` |

The `null!` form was needed because the nullable warnings-as-errors setting stops a bare deletion from compiling. The runtime behaviour matches the reviewer's "delete the guard" scenario.

## Non-blocking fix: a model answer that is only an image with an empty alt

- `api/Elmanhg.Application/Questions/Shared/RichTextContent.cs:31` adds `HasGradableText(html)`. It returns true when the answer has visible text, a formula (`data-latex`), or an `<img>` whose alt is not blank after HTML-decoding. These are the only inputs from which `RichTextExtractor` produces text. The file has one WHY comment stating that invariant. It is 36 lines.
- `api/Elmanhg.Application/Questions/Shared/EssayQuestionRules.cs:28` (Validate, as sent) and `:43` (Normalize, after sanitising) now use `HasGradableText` instead of `HasContent`. The error code is still `QUESTION_MODEL_ANSWER_REQUIRED` (422), so there is no new code and no resx change.
- Tests:
  - `RichTextContentTests.HasGradableText_TextAltOrFormula_ReturnsTrue` (4 rows) and `HasGradableText_NoTextOrImageWithoutAlt_ReturnsFalse` (5 rows: null, markup only, `alt=""`, `alt="&nbsp;"`, no alt).
  - In `EssayQuestionRulesTests`, the existing `Validate_ImageOnlyModelAnswer_ReturnsNoErrors` asserted the old, buggy behaviour. I replaced it with `Validate_ImageOnlyModelAnswerWithAlt_ReturnsNoErrors` and `Validate_ImageOnlyModelAnswerWithoutAlt_ReturnsQuestionModelAnswerRequired`, and added `Normalize_ModelAnswerImageWithoutAltAfterSanitising_ThrowsQuestionModelAnswerRequired`.
- Docs-sync:
  - `docs/question-schemas.md`: the Essay row for `QUESTION_MODEL_ANSWER_REQUIRED` now says an image needs non-blank alt text.
  - `docs/essay-grading.md` §context: an image contributes its alt, authoring rejects model answers with no readable text, and the drop is kept only as a guard for older revisions.
  - `docs/essay-grading.md` error table: updated to match.
- `EssayGradingRequestFactory` still drops empty model answers, which covers revisions saved before this fix. That behaviour and its test `Create_DropsModelAnswersWithoutText` are unchanged.

## Files created
None.

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Application/Questions/Shared/RichTextContent.cs` | Adds `HasGradableText`, the `ImageAlt` regex (NonBacktracking) and the private `HasVisibleText` helper. `HasContent` behaves exactly as before. |
| `api/Elmanhg.Application/Questions/Shared/EssayQuestionRules.cs` | Model-answer checks use `HasGradableText`. |
| `api/Elmanhg.Tests/.../GradeEssay/GradeEssayHandlerTests.cs` | +1 test. |
| `api/Elmanhg.Tests/.../Shared/EssayGradingRequestFactoryTests.cs` | +1 test. |
| `api/Elmanhg.Tests/.../Questions/Shared/RichTextContentTests.cs` | +2 theories. |
| `api/Elmanhg.Tests/.../Questions/Shared/EssayQuestionRulesTests.cs` | 1 test replaced by 2, and +1 Normalize test. |
| `ai/tests/unit/test_prompt_loader.py` | +1 test. |
| `ai/tests/unit/test_essay_grading_eval.py` | +1 parametrised test (4 cases). |
| `docs/question-schemas.md`, `docs/essay-grading.md` | Docs-sync for the model-answer rule. |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Rework mode says to fix only the blocking findings. | The orchestrator also asked for the non-blocking image-alt bug to be fixed. | Fixed it on the #117 `EssayQuestionRules` path with tests and docs, as instructed. The replaced test `Validate_ImageOnlyModelAnswer_ReturnsNoErrors` encoded the bug, so I renamed it and inverted its assertion. |

The Postman note from the previous review, `{{sessionQuestionId}}` instead of the plan's `{{questionId}}`, is recorded here as a deviation. It was correct and disclosed, but it was missing from the earlier Deviations table.

## Build & test
- CI parity: `api/Elmanhg.Api/appsettings.json` was moved to the scratchpad, then restored. I confirmed the restore with `ls`.
  - `dotnet build api/ -c Release`: `0 Warning(s) 0 Error(s)`.
  - `dotnet test api/ -c Release --no-build`: `Test run summary: Passed! total: 3602 failed: 0 succeeded: 3602`.
- `dotnet format api/Elmanhg.slnx --verify-no-changes --include <touched files>`: exit 0.
- `api/openapi/v1.json`: the build regenerated it and it is byte-identical to before this rework, because there is no contract change.
- ai:
  - `python -m uv run pytest -m "not eval"`: `273 passed, 3 deselected`.
  - `-m eval`: `3 skipped` (no keys).
  - `ruff check`: `All checks passed!`
  - `ruff format --check`: `97 files already formatted`.
  - `mypy src`: `Success: no issues found in 53 source files`.
- web: not touched in this rework, so it was not re-run.

## Notes for review
- `HasGradableText` uses a regex to approximate the AngleSharp extractor. An `alt` value that contains only characters .NET treats as whitespace after decoding (such as `&nbsp;`) counts as empty, which matches `RichTextExtractor.Text`'s `Split(null)`. Exotic markup, for example `alt` text inside another attribute's value on an `<img>`, could produce a false positive. The web editor requires alt text, so only direct API callers reach this path.
- Same bug class, not fixed (out of scope): an Essay **stem** that is only an image with an empty alt extracts to `""`, and the ai `question` field has `min_length=1`, so it returns a 400. Stems are shared across all question types, where image-only stems are legitimate. If you want this closed, the fix is an Essay-only stem check using `HasGradableText`.
- Nothing was committed.
