VERDICT: CHANGES_REQUESTED

# Review — Arabic answer normalisation (#70), round 1

## Blocking

### 1. A pasted U+FFFE makes grading throw, which is a regression
**Where:** `api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs:15` (and `DropUnpairedSurrogates`, `:53-74`)
**Rule:** plan D6 (step 1 exists so that "a pasted answer must never fail grading", the WHY comment at `:52`); plan Goal ("Every existing question keeps grading exactly as it does today").
**Problem:** `string.Normalize(NormalizationForm.FormC)` throws `ArgumentException` on the noncharacter U+FFFE (the byte-swapped BOM) as well as on lone surrogates. I checked every code point from U+0000 to U+10FFFF against .NET 10/ICU in this environment. U+FFFE is the only non-surrogate that throws. The pre-pass only removes surrogates, so U+FFFE reaches `Normalize`.
**Failure:** `AnswerNormalizer.Normalize("a￾b", AnswerNormalization.Default)` throws `ArgumentException`. I reproduced this with a scratch console app that references `Elmanhg.Domain`. So a fill or short answer such as `"20￾"` now throws from `QuestionGrader.Grade`, and `POST /api/questions/grade-draft` returns 500. Before this change the same answer graded 1. A numeric answer fails the same way, because `TryParseNumber` also goes through `Normalize`.
**Fix:** drop U+FFFE in the pre-pass before `Normalize` (for example, in `DropUnpairedSurrogates`, renamed to match), and add a test next to N13, e.g. `Normalize_ByteSwappedBom_IsDropped`: `"a￾b"` → `"ab"`. Update the always-on list in `docs/question-schemas.md` § Grading → Normalisation to match.

### 2. `ArabicCharacters` uses literal characters, not `\uXXXX` escapes, and the report says otherwise
**Where:** `api/Elmanhg.Domain/Questions/Grading/ArabicCharacters.cs:8-37`
**Rule:** plan D12 and Definition of done ("The code-point constants are `\uXXXX` escapes in `ArabicCharacters`"). 02-implementation.md, Deviations row 3, says "Wrote every constant as an escape".
**Problem:** all 30 constants are raw characters (for example `ZeroWidthSpace = '​'`, `ByteOrderMark = '﻿'`, `RightToLeftMark = '‏'`). `grep -P '\\u[0-9A-F]{4}'` finds no match in the file. Only `TextGrader.cs:9-11` uses escapes. The code points themselves are correct (I checked each one). The invisible ones cannot be read or reviewed, and D12 existed to close exactly that #151 nit. This is an unreported deviation behind a false claim.
**Failure:** in any editor or diff, lines 30-37 look like `internal const char X = '';`. Nobody can tell whether U+200F or U+200E is in the range bound, or whether the character has been lost altogether.
**Fix:** rewrite lines 8-37 as `'ـ'` … `'﻿'`.

## Non-blocking
- `AnswerNormalizer.cs:15` with `:22` — NFC runs before invisible marks are removed (the plan's D6 order). An invisible mark between a letter and a combining hamza survives NFC, so the result is not idempotent. For example, with `StripTashkeel: false, UnifyAlef: false`, `"ا‌ٔ"` becomes `"أ"`, and normalising it again gives `"أ"`. With the default rules the output is the same either way. Removing invisible marks before NFC would make the "Normalize is idempotent" claim in the plan hold for every input.
- `AnswerNormalizerTests.cs` N10 `[InlineData('…')]` rows and the `"‏ماء"` row in `EgyptianSpellingVariantsTests.cs:25` also use invisible literals. Escapes would make them reviewable. The bytes are correct: U+061C, U+200B–U+200F, U+202B, U+202E, U+2060, U+2066, U+2069 and U+FEFF.
- Migration SQL `("GradingSpec" ->> 'unifyLetterVariants')::boolean` would abort on a non-boolean legacy value. Canonical storage has only ever written booleans, so this is safe today.
- The migration `.cs` lost its BOM (the report mentions this). No effect.

## Verified
- Backward compatibility of the migration (`20260928085849_AddAnswerNormalizationRules.cs:10-24`): the SQL matches the plan exactly. It only touches rows that have `unifyLetterVariants`, so a second run is a no-op (idempotent). A missing key or a JSON null maps to `true` through `COALESCE`. The three letter rules take the old flag and the other five are `true`, which reproduces the old grading exactly (the old flag gated only alef, taa marbuta and maqsura). Numeric and non-text specs never had the key and are untouched (N39). The revision `WHERE` evaluates to NULL/false when a snapshot has no `gradingSpec`. The model snapshot is unchanged, and `has-pending-model-changes` reports no changes.
- Specs without the key (and without `normalization`) grade with `Default` (all on), which is identical to the old absent-means-true behaviour (N20, N21).
- The numeric rule uses `NumberStyles.AllowLeadingSign | AllowDecimalPoint` (`TextGrader.cs:12`), so exponents are rejected (N15). `docs/question-schemas.md` § Numeric parsing and PRD §6.2 agree with the code. ٬ is stripped and ، maps to `,` and then `.`. Per-question rules are ignored (N19).
- Import: `QuestionImportBodies.cs:39,49,55` maps the single `unify_letter_variants` column onto the three letter rules (empty means true), and N32/N33 cover `false`. The en and ar resx help texts match the plan, and `docs/question-import.md` agrees.
- Unicode: NFC runs before tashkeel stripping, so a decomposed ا+U+0654/0655/0653 composes first (N9, and the corpus row `رئيس` with ي+U+0654 would fail without NFC). Lone high or low surrogates and reversed pairs are dropped, and valid pairs are kept (checked directly). The corpus covers ة/ه, ى/ي in both directions, أ/إ/آ/ٱ, tashkeel, tatweel, digits, whitespace, ی, ، , RLM and case. Hamza seats are pinned as distinct.
- Every file in Files to create exists. There are no extra production files. The signatures match A1–A3 and M1. The `bool` overload is gone, and there is no second normaliser in the repo. No `unifyLetterVariants` is left in the web, the Postman collection or the production api code (only in the migration and tests, as intended).
- Docs: PRD §6.2, question-schemas (examples, rule table, canonical storage, migration bullet, normalisation, numeric parsing), question-import, claude-design-prompt §4/§5 and prototype.md are updated. Finding 1's fix will need one line in question-schemas.
- Postman: the routes and contracts are unchanged, and there is no stale key.
- CI re-run by me: `dotnet build -c Release`: 0 warnings, 0 errors. `dotnet test api/ -c Release`: **1142/1142 passed**. The openapi file has no drift. No vulnerable packages. `has-pending-model-changes`: no changes. Web: `gen:api` gives no drift. Typecheck, lint and format:check are clean. `test`: **70 files / 397 tests passed**. `build` succeeds.
- Deviations 1 (the `readNormalization` parameter type) and 2 (`findAllByText` in N43) are justified and keep the planned assertions.

## Test quality
- `AnswerNormalizerTests`: each toggle test (N1–N8) is paired with the existing on-case, so flipping any guard fails a test. N3 and N4 check that the rules are independent. N14 pins that only the always-on steps run.
- `TextGraderTests`: N15 fails with the old `NumberStyles.Float`. N16–N18 fail without the ٬ strip and without invisible-mark removal. N19 fails if numeric parsing reads the spec's rules.
- `EgyptianSpellingVariantsTests`: this is a real corpus, and the `رئيس` row constrains NFC.
- `QuestionGraderTests` N23 and N24, `FillQuestionRulesTests` N27–N29, `ShortQuestionRulesTests` N30 and N31: all constrain the partial-object defaults, the JSON binding and the 422 path.
- `AnswerNormalizationRulesMigrationTests`: runs the real SQL constants against Postgres and does DeepEquals on both the spec and the snapshot. It constrains the implementation.
- Web N40–N45: the round trip and the save body are asserted exactly, and the group is shown only for text answers. They constrain the implementation.
- Missing: no test covers U+FFFE (finding 1).
