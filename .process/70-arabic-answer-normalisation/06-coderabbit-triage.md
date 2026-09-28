TRIAGE: 1 to implement, 0 rejected, 0 dev-decisions

# CodeRabbit triage — #70 Arabic answer normalisation (PR #159)

## RC1 — Remove invisible controls before NFC
**Where:** `api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs:15` (NFC) vs `:20`/`:50` (invisible-control drop, after NFC)
**Classification:** Major
**Decision:** IMPLEMENT

### Reproduction (run, not just read)
I copied `AnswerNormalizer.cs`, `ArabicCharacters.cs` and `AnswerNormalization.cs` from the branch HEAD, unchanged, into a scratch console project (net10.0, ICU globalisation). I ran the current code and a patched copy side by side. `once` = Normalize(x), `twice` = Normalize(once), `accepted` = Normalize("أ" U+0623) under the same rules.

| Input | Rules | Current output | Idempotent | Equals accepted `أ` | Patched output | Patched idempotent |
|---|---|---|---|---|---|---|
| ا U+200C U+0654 | StripTashkeel off, UnifyAlef off | 0627 0654 | no | no | 0623 | yes |
| ا U+200F U+0654 | same | 0627 0654 | no | no | 0623 | yes |
| ا U+061C U+0654 | same | 0627 0654 | no | no | 0623 | yes |
| ا U+FEFF U+0654 | same | 0627 0654 | no | no | 0623 | yes |
| ا U+2060 U+0653 | same | 0627 0653 | no | no (vs آ) | 0622 | yes |
| و U+200C U+0654 | same | 0648 0654 | no | no (vs ؤ) | 0624 | yes |
| ا U+200C U+0654 | **only UnifyAlef off** (tashkeel stripped) | 0627 | yes | **no** | 0623 | yes |
| ا U+200C U+0654 | only StripTashkeel off | 0627 0654 | no | no (accepted gives 0627) | 0627 | yes |
| ا U+200C U+0654 | Default | 0627 | yes | yes | 0627 | yes |

CodeRabbit is right, and the bug is wider than it says. It is enough for **either** `unifyAlef` or `stripTashkeel` to be off. With only `unifyAlef:false` (the case `Normalize_DecomposedHamzaOrMadda_ComposesWithUnifyAlefOff` exists for), the hamza is removed as tashkeel because it never composed. The student gets `ا` and the key is `أ`, so a correct answer is marked wrong and the output is still stable. The ZWNJ/RLM-after-letter pattern is a common Arabic keyboard and paste artefact. D6 exists to neutralise exactly these artefacts, so the step order defeats D6's own purpose. That makes it Major, not Minor. Round-1 review (03-review-r2.md:9) called the order non-blocking polish. The reproduction shows it is a grading error.

### Minimal fix
1. `AnswerNormalizer.cs`: remove invisible controls in the pre-NFC pass. Extend `DropUnnormalizable` (and its `Any` guard at `:55` and the keep condition at `:68`) to also skip `ArabicCharacters.IsInvisibleControl(character)`. Rename it to reflect its job, e.g. `DropBeforeComposition`. Update the WHY comment at `:52`: invisible controls are removed before NFC so they cannot keep a letter and its combining mark apart. After that, `:15` stays `DropBeforeComposition(text).Normalize(NormalizationForm.FormC)`.
2. `IsDropped` (`:50`): remove the `ArabicCharacters.IsInvisibleControl(character) ||` term. NFC never produces those characters, so it is dead after the move.
3. Nothing else changes: tashkeel, tatweel and the mapping steps stay after NFC. Tashkeel must stay after NFC: stripping it earlier would turn a decomposed ا+U+0654 into ا and break N9.

I verified the patched variant in scratch: every row above becomes composed and idempotent, and default-rule results are unchanged.

### Regression tests (exact)
In `api/Elmanhg.Tests/Domain/Questions/Grading/AnswerNormalizerTests.cs`, write every new code point as a `\uXXXX` escape:
- `Normalize_InvisibleControlBetweenLetterAndMark_Composes` [Theory], rules `new AnswerNormalization(StripTashkeel: false, UnifyAlef: false)`:
  - `"ا‌ٔ"` → `"أ"`
  - `"ا‏ٔ"` → `"أ"`
  - `"ا⁠ٓ"` → `"آ"`
  - `"و﻿ٔ"` → `"ؤ"`
- `Normalize_InvisibleControlBetweenLetterAndMark_ComposesWithUnifyAlefOff` [Fact]: `"ا‌ٔ"` with `new AnswerNormalization(UnifyAlef: false)` → `"أ"`. This is the default-tashkeel case; the current code returns `"ا"`.
- `Normalize_IsIdempotent` [Theory], rules `new AnswerNormalization(StripTashkeel: false, UnifyAlef: false)`. The inputs are the four rows above plus `"م‏اء"` and `"  Aـَ٢‏ "`. Assert `Normalize(Normalize(x, rules), rules).Should().Be(Normalize(x, rules))`. Scope the inputs to invisible-control cases only (see Residual below).
In `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs`:
- `GradeShort_InvisibleControlInsideHamza_MatchesComposedAccepted` [Fact]: text spec `acceptedAnswers: ["أ"]`, `Normalization = new AnswerNormalization(UnifyAlef: false)`, answer `"ا‌ٔ"` → `1m`. On current HEAD this returns 0.
Every new test above fails on current HEAD: I confirmed the outputs in the reproduction table. The existing N10 `Normalize_InvisibleControl_IsRemoved` and `Normalize_AllRulesOff_AppliesOnlyAlwaysOnSteps` still pass under the patched logic.

### Docs sync (same change, otherwise a divergence)
- `docs/question-schemas.md:214` (Grading → **Normalisation**) currently says the order is "drop unpaired UTF-16 surrogates and the noncharacter U+FFFE; Unicode NFC; remove invisible marks (…)". Rewrite it as "drop unpaired UTF-16 surrogates, the noncharacter U+FFFE and invisible marks (U+061C, U+200B–U+200F, U+202A–U+202E, U+2060, U+2066–U+2069, U+FEFF), so none of them can split a letter from its mark; Unicode NFC (…); map ، … ی …". Keep the rest of the sentence.
- `docs/PRD.md:182` §6.2 "Always applied: Unicode NFC, removal of invisible bidi and zero-width marks, …": swap to "removal of invisible bidi and zero-width marks, Unicode NFC, …" so both docs list the same order.
- Plan D6 (`01-plan.md:36`) fixed the old order. This triage supersedes D6 step order: 1 = surrogates + U+FFFE + invisible controls, 2 = NFC. The implementer should record this as a deliberate deviation from D6, citing RC1.

### Residual (observed, outside RC1, not counted)
The same reproduction found three more non-idempotent inputs that the RC1 fix does not cover. All need `stripTashkeel:false`:
- ا U+0640 U+0654 with StripTatweel on: tatweel is dropped after NFC, leaving 0627 0654.
- ی U+06CC + U+0654: ی→ي is mapped after NFC, leaving 064A 0654.
- ى U+0649 + U+0654 with UnifyAlefMaqsura on: same effect.
These are rare inputs, not keyboard artefacts. Do not widen this fix for them, and keep the idempotence test off these inputs. If the owner wants full idempotence, the follow-up is to drop tatweel (when on) and map ی/ى→ي in the pre-NFC pass too. Alef unification must stay after NFC.
