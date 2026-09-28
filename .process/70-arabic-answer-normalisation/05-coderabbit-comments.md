# CodeRabbit comments — PR #159

Collected 2026-09-28 09:40. Review 5336643555 ("Actionable comments posted: 1"). Verbatim.

## RC1 — `api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs:15` (🟠 Major, Functional Correctness)

**Remove invisible controls before NFC.**

If `StripTashkeel` and `UnifyAlef` are off, the answer `ا‌ٔ` normalizes to decomposed `أ`. The accepted answer `أ` remains composed, so `TextGrader.Matches` rejects a canonically equivalent answer. The output also changes on a second normalization pass. Remove invisible controls after the existing invalid-character cleanup but before `Normalize(FormC)`. Add a regression test with a control between the letter and combining mark.
