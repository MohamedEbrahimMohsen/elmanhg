# Implementation — Arabic answer normalisation (#70), rework round 2

## Findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Renamed the pre-pass `DropUnpairedSurrogates` to `DropUnnormalizable`. It now also drops U+FFFE (new constant `ArabicCharacters.ByteSwappedBom = '￾'`) before the NFC step, and the WHY comment names U+FFFE as well. Added the regression tests `AnswerNormalizerTests.Normalize_ByteSwappedBom_IsDropped` (`"a￾b"` gives `"ab"`) and `TextGraderTests.GradeFill_AnswerWithByteSwappedBom_ReturnsOne` (`"20￾"` grades like `"20"`). Added U+FFFE to the always-on step list in `docs/question-schemas.md`. | `api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs:15,52-75`; `api/Elmanhg.Domain/Questions/Grading/ArabicCharacters.cs:38`; `api/Elmanhg.Tests/Domain/Questions/Grading/AnswerNormalizerTests.cs:152-156`; `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs:33-37`; `docs/question-schemas.md:214` |
| 2 | Rewrote all 30 existing constants as `\uXXXX` escapes. The code points are the same, and a script did the conversion from the literal characters, so none were retyped. | `api/Elmanhg.Domain/Questions/Grading/ArabicCharacters.cs:8-37` |

## Files created
None. The two tests were added to existing test classes.

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs` | The pre-pass also drops U+FFFE and is renamed `DropUnnormalizable`. |
| `api/Elmanhg.Domain/Questions/Grading/ArabicCharacters.cs` | Constants written as escapes; added `ByteSwappedBom`. |
| `api/Elmanhg.Tests/Domain/Questions/Grading/AnswerNormalizerTests.cs` | Added `Normalize_ByteSwappedBom_IsDropped`. |
| `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs` | Added `GradeFill_AnswerWithByteSwappedBom_ReturnsOne`. |
| `docs/question-schemas.md` | § Normalisation: the always-on first step now names U+FFFE. |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| The test plan does not list the two new tests. | Review finding 1 asked for a regression test, and the caller asked for the `"20￾"` case. | Added one normaliser-level test and one grader-level test. |

The earlier report's Deviations row 3 ("Wrote every constant as an escape") was false when I wrote it. It is true only as of this round.

## Build & test
- `dotnet build api/ -c Release`: Build succeeded, 0 Warning(s), 0 Error(s).
- `git status --porcelain api/openapi`: empty, so there is no drift.
- `dotnet test api/ -c Release --no-build`: total 1144, failed 0, succeeded 1144 (up from 1142 by the 2 new tests).
- `dotnet ef migrations has-pending-model-changes ...` (with the design connection string): "No changes have been made to the model since the last migration."
- web: `gen:api` produced no drift in `src/shared/api/generated`. `typecheck`, `lint` and `format:check` are clean ("All matched files use Prettier code style!"). `test -- --run`: 70 files / 397 tests passed. `build`: "built in 6.27s".

## Notes for review
- I did not sweep code points myself for "other noncharacters that Normalize rejects". I relied on the reviewer's full U+0000–U+10FFFF sweep, which found that U+FFFE is the only non-surrogate that throws. So I drop only U+FFFE. Other noncharacters, such as U+FFFF and U+FDD0–U+FDEF, keep their previous behaviour.
- I did not touch the non-blocking items: NFC/invisible-mark order, escapes in test InlineData, and the migration cast.
