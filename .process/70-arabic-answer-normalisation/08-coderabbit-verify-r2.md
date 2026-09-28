VERDICT: APPROVED

# CodeRabbit verify r2: #70 Arabic answer normalisation (PR #159)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Domain/Questions/Grading/AnswerNormalizerTests.cs:108-110`, `:117-128`, `:163`: these older rows still use raw characters, some of them invisible (for example U+200B, U+2060 and U+FEFF as `char` literals at `:118`, `:125`, `:128`). This does not block, for three reasons:
  1. Plan D12 (`01-plan.md:42`) and its Definition-of-done line (`01-plan.md:192`) cover only the code-point constants in `ArabicCharacters`. They say nothing about test data.
  2. The triage 06 escape rule (`06-coderabbit-triage.md:35`) covers *new* code points only.
  3. 03-review-r2 approved these lines, and 02-implementation-r2.md:37 lists test escapes as non-blocking.

  They do carry the same readability and silent-drop risk that 08 finding 1 described, so escaping them would be a good mechanical follow-up.

## Verified
- **Finding 1 fixed.** I ran `LC_ALL=C.UTF-8 grep -nP '[^\x00-\x7F]'` on both test files. It finds no non-ASCII byte on `AnswerNormalizerTests.cs:165-196` or on `TextGraderTests.cs:147-153`. Every new string is a `\uXXXX` escape.
- **Code points unchanged.** Each escaped row has the code points that 08 recorded:
  - `:167` U+200C
  - `:168` U+200F
  - `:169` U+2060 with U+0653 → U+0622
  - `:170` U+FEFF on waw → U+0624
  - `:179` and `:183-186` the same controls
  - `:187` U+200F in `م‏اء`
  - `:188` `  A ـ َ ٢ U+200F `
  - `TextGraderTests.cs:150` `["أ"]`, `:152` `"ا‌ٔ"`

  The implementer's line correction for TextGraderTests.cs (`:150`/`:152`, not `:149`/`:151`) is accurate.
- **No logic changes.** The `AnswerNormalizer.cs` diff has the RC1 change as 08 described and verified it (`DropBeforeComposition`, `IsDroppedBeforeComposition`, and the invisible-control check removed from `IsDropped`). Test method names, rule arguments and assertions are unchanged.
- **Tests pass.** I ran `dotnet test api/ -c Release` myself: 1156/1156 passed, 0 failed. This matches the claim in 07-coderabbit-rework-r2.md.
- "Deviations: None." is accurate.

## Test quality
The escaped rows still constrain RC1. They assert the same exact composed code points that 08 showed failing on the committed code.
