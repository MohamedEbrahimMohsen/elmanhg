VERDICT: APPROVED

# Review — Arabic answer normalisation (#70), round 2

## Blocking
None.

## Non-blocking
- Round-1 non-blocking items still stand and were deliberately left (02-implementation-r2.md, Notes): NFC runs before invisible-mark removal (`AnswerNormalizer.cs:15` vs `:20`), invisible literals in test `InlineData` rows, the `::boolean` cast in the migration SQL.

## Verified
- Finding 1 (U+FFFE): fixed. `AnswerNormalizer.cs:53-75` (`DropUnnormalizable`) drops U+FFFE and lone surrogates before `Normalize(FormC)` at `:15`; valid pairs are kept. Adversarial check with a scratch console app referencing `Elmanhg.Domain`: every code point U+0000-U+10FFFF, each alone, as `"20"+c` and as `ا+c+U+0654`, run through `Normalize` (Default and all-rules-off) and through `TextGrader.GradeShort` (numeric and text specs), plus hand-picked cases (`\uD800￾`, `￾\uDC00`, `\uD83D￾\uDE00`, `￾` alone, `￾` between alef and combining hamza, U+1FFFE/U+10FFFE pairs, U+FFFF, U+FDD0) and 300,000 random 1-8 char strings from a pool of surrogates, U+FFFE, U+FFFF, combining hamza/madda, bidi marks, digits and separators: 0 exceptions. `"a￾b"` gives `"ab"`; numeric `"￾٢٠￾"` against 20 grades 1; text `"2￾0"` against `"20"` grades 1; `"￾"` alone normalises to empty and grades 0.
- Regression tests exist and constrain the fix (both would throw without the pre-pass): `AnswerNormalizerTests.cs:153-156` `Normalize_ByteSwappedBom_IsDropped`, `TextGraderTests.cs:34-37` `GradeFill_AnswerWithByteSwappedBom_ReturnsOne`. Both use `￾` escapes.
- Finding 2 (escapes): all 31 constants in `ArabicCharacters.cs:8-38`, including the new `ByteSwappedBom = '￾'`, are `\uXXXX` escapes. `grep -P '[^\x00-\x7F]'` on the file matches only the `§` in the comment at `:7`; no BOM. Code points unchanged from round 1 (re-checked against plan A3).
- Docs: `docs/question-schemas.md:214` names U+FFFE in the always-on first step, matching the code. PRD §6.2 describes the always-on steps at a level that does not contradict it. No divergence.
- The rework's one declared deviation (two tests not in the test plan) is justified by finding 1.
- No regressions: the changed file set is identical to round 1 plus the two edits described; no new files; `AnswerNormalizer.cs` 76 lines, `ArabicCharacters.cs` 55 lines. No Postman or OpenAPI change needed.
- CI re-run by me: `dotnet build api/ -c Release`: 0 warnings, 0 errors. `api/openapi`: no drift. `dotnet test api/ -c Release --no-build`: 1144/1144 passed. `has-pending-model-changes`: no changes. Web: `gen:api` no drift; typecheck, lint, format:check clean; `test --run`: 70 files / 397 tests passed; `build` succeeded.

## Test quality
- The two new tests fail against the round-1 code (ArgumentException from NFC), so they constrain the fix. Round-1 assessment of the other test classes is unchanged.
