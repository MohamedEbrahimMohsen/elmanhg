# CodeRabbit rework — #70 Arabic answer normalisation (PR #159)

| # | What I changed | File:line |
|---|---|---|
| RC1.1 | `DropUnnormalizable` renamed `DropBeforeComposition`; its `Any` guard and keep condition now also drop `ArabicCharacters.IsInvisibleControl` chars (through new `IsDroppedBeforeComposition`), before NFC. WHY comment extended. | `api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs:15`, `:52-77`, `:79` |
| RC1.2 | Dead `IsInvisibleControl` term removed from `IsDropped`. | `api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs:50` |
| RC1 tests | `Normalize_InvisibleControlBetweenLetterAndMark_Composes` (Theory, 4 rows), `Normalize_InvisibleControlBetweenLetterAndMark_ComposesWithUnifyAlefOff` (Fact), `Normalize_IsIdempotent` (Theory, 6 rows), all with `\uXXXX` escapes. | `api/Elmanhg.Tests/Domain/Questions/Grading/AnswerNormalizerTests.cs:166-196` |
| RC1 tests | `GradeShort_InvisibleControlInsideHamza_MatchesComposedAccepted` (Fact). | `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs:147-153` |
| RC1 docs | Step 1 now drops surrogates, U+FFFE and invisible marks ("so none of them can split a letter from its mark"); NFC is step 2. | `docs/question-schemas.md:214` |
| RC1 docs | §6.2 order swapped: "removal of invisible bidi and zero-width marks, Unicode NFC, …". | `docs/PRD.md:182` |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Plan D6 (`01-plan.md:36`): NFC before removing invisible controls | Triage RC1 showed that order lets an invisible control keep a letter apart from its mark, so a correct answer was graded wrong | Followed RC1: invisible controls are dropped before NFC. This deliberately supersedes D6's step order. |

The pre-NFC predicate is a small helper, `IsDroppedBeforeComposition`, which both the guard and the keep condition use. Without it, the same two-part condition would be repeated.

## Build & test
- `docker info` failed at first; `bash scripts/cloud-setup.sh` then reported docker 29.3.1 and dotnet 10.0.401.
- `dotnet build api/ -c Release`: 0 errors, 9 warnings.
- `git status --porcelain api/openapi`: empty (no drift).
- `dotnet test api/ -c Release --no-build`: failed 0, succeeded 1156, skipped 0.
- `dotnet ef migrations has-pending-model-changes ...` (with the CI connection string): "No changes have been made to the model since the last migration."
- web: typecheck, lint and format:check all passed. `npm test -- --run`: 70 files and 397 tests passed. `npm run build`: "built in 5.75s". No web files changed.

## Notes for review
- The Residual inputs from the triage (tatweel, ی and ى next to U+0654) were left alone on purpose, and the idempotence test does not use them.
- I did not re-run the new tests against the old HEAD. The triage's reproduction table already records that they fail there.
