VERDICT: CHANGES_REQUESTED

# CodeRabbit verify: #70 Arabic answer normalisation (PR #159)

## Blocking
### 1. The new tests use raw invisible characters, not `\uXXXX` escapes, and 07-coderabbit-rework.md says they use escapes
**Where:** `api/Elmanhg.Tests/Domain/Questions/Grading/AnswerNormalizerTests.cs:167-170`, `:179`, `:183-188`; `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs:149`, `:151`
**Rule:** triage 06 "Regression tests (exact)": "write every new code point as a `\uXXXX` escape". The same kind of false escape claim was round-1 finding 2 (03-review.md:14). Reviewer rules: a hidden deviation behind a false claim is blocking.
**Problem:** 07-coderabbit-rework.md:7 says "all with `\uXXXX` escapes". Running `grep -P '\\u[0-9A-Fa-f]{4}'` on AnswerNormalizerTests.cs matches only the older lines 149 and 155. `od -c` shows raw UTF-8 bytes for U+200C, U+200F, U+2060 and U+FEFF inside the new `InlineData` rows, and for U+200C at TextGraderTests.cs:151. The code points are correct; I checked every byte. But the rows cannot be read or reviewed, and nobody can see which control each row covers.
**Failure:** if an editor, formatter or paste drops the raw U+FEFF at `:170` (`"و<FEFF>ٔ"`), the row becomes `"ؤ"`. That row passes on the old, broken code too. The regression guard for that control is gone and no test fails.
**Fix:** write the new strings as escapes, for example `"ا‌ٔ"` → `"أ"`, `"و﻿ٔ"` → `"ؤ"`, `"م‏اء"`, and `"ا‌ٔ"` in TextGraderTests.cs:151. No logic changes.

## Non-blocking
- `api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs:15`, `:43`: a fuzz run found 108 non-idempotent inputs out of 102,400. None of them contain an invisible control; example: `ا U+0655 U+0654` with only UnifyAlef on and StripTashkeel off. They come from stacked hamza or madda marks plus alef unification after NFC. The cause is the same as triage 06 "Residual", but this class is not listed there. It predates this change, the input is not realistic, and it is out of RC1 scope.
- `.process/70-arabic-answer-normalisation/07-coderabbit-rework.md:21`: the build now reports 0 warnings, not 9. The build was incremental, so this is not a problem.

## Verified
- **RC1 fixed.** `AnswerNormalizer.cs:15`, `:54-78`: surrogates, U+FFFE and every `IsInvisibleControl` character are removed before `Normalize(FormC)`. `IsDropped` at `:50` no longer checks invisible controls; that check was dead after the move. Tashkeel and tatweel are still removed after NFC. My own inputs (scratch console against the built Domain dll):
  - `ا ZWNJ ZWJ ٔ` → 0623; `ا ٔ ZWNJ َ` → 0623 064E; `ا ZWNJ َ ٔ` → 0623 064E (NFC reorders the marks and still composes).
  - `ا <lone D800> RLM ٔ` → 0623; `ي ZWNJ ٔ` → 0626; `ا RLO ٕ` → 0625; `ا LRI ٓ PDI` → 0622. All with StripTashkeel and UnifyAlef off.
  - `ا ZWNJ ٔ` with only UnifyAlef off → 0623. With Default rules it gives 0627, the same as `أ` under Default.
  - An emoji ZWJ sequence keeps its surrogate pairs; a lone RLM → empty. Every case is idempotent.
  - Property fuzz: 102,400 random strings over Arabic letters, marks, the controls, U+FFFE, space, `A`, `٢`, `ة`, `،`, under all 256 rule combinations. For every string, `Normalize(x) == Normalize(x with the controls removed)`: 0 violations. The controls are fully transparent.
- **The new tests fail on the committed code.** I stashed only `AnswerNormalizer.cs`, rebuilt and ran the tests: 10 failed. That is 4× `_Composes`, `_ComposesWithUnifyAlefOff` (returned `ا`), 4× `Normalize_IsIdempotent` (the hamza and madda rows), and `GradeShort_InvisibleControlInsideHamza_MatchesComposedAccepted` (returned 0). The idempotence rows `م‏اء` and `  Aـَ٢‏ ` pass on both versions; they are harmless extras. Stash popped, `git stash list` empty, working tree identical to before, rebuilt with the fix.
- **Docs match the code.** `docs/question-schemas.md:214` lists: surrogates + U+FFFE + invisible marks → NFC → ، / ی → rules → trim. The listed ranges match `ArabicCharacters.IsInvisibleControl` (`ArabicCharacters.cs:42`) exactly. `docs/PRD.md:182` gives the same order ("removal of invisible … marks, Unicode NFC, …"). No other doc mentions NFC.
- The D6 step-order deviation is recorded in 07-coderabbit-rework.md "Deviations" with RC1 as the reason.
- No regressions. `dotnet build api/ -c Release`: 0 errors. `git status --porcelain api/openapi`: empty. `dotnet test api/ -c Release --no-build`: 1156/1156 passed, including N10 `Normalize_InvisibleControl_IsRemoved` and `Normalize_AllRulesOff_AppliesOnlyAlwaysOnSteps`. Web `npm run typecheck`: clean.
- Skill: the file-scoped namespace and `sealed` test classes are unchanged. The WHY comment at `AnswerNormalizer.cs:52-53` states a hidden invariant (SKILL §No Comments). No API surface changed, so Postman needs nothing.

## Test quality
- `AnswerNormalizerTests` (new rows): these constrain the fix. They assert exact composed code points, and each one failed on the committed code. The last two `Normalize_IsIdempotent` rows do not test RC1.
- `TextGraderTests.GradeShort_InvisibleControlInsideHamza_MatchesComposedAccepted`: constrains the fix end to end through the grader (0 → 1).
