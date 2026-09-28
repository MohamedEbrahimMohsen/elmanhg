TRIAGE: 1 to implement, 0 rejected, 0 dev-decisions

# CodeRabbit triage — PR #163 (story #72)

## RC1 — `docs/question-schemas.md:225` Fill feedback rules not qualified for answered responses

**Classification:** Valid. Minor docs-sync ambiguity. The doc contradicts itself; the code is correct.
**Decision:** IMPLEMENT, but not with CodeRabbit's wording. Add one precedence sentence to the unanswered bullet on line 223.

**Evidence:**
- `api/Elmanhg.Domain/Questions/Grading/TextGrader.cs:37-40`: when every blank normalises to empty, `GradeFill` returns `NormalisedGrade.Unanswered` before it reaches the tally or single-blank branch at line 50. `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs:85-89` (`GradeFill_EmptyAnswer_ReturnsUnanswered`) pins this for a single-blank Fill.
- `docs/question-schemas.md:223` already lists "every Fill blank is missing or normalises to empty" as unanswered. So the case is covered, but nothing says that rule wins over the bullets below it.
- `docs/question-schemas.md:225` says, with no qualification, "A single-blank Fill returns `null`." For an empty single-blank Fill this contradicts line 223 and the code, which return "No answer was given." A two-blank Fill left empty "is not fully correct", so line 225 would also give it "Correct blanks: 0 of 2", while line 223 says unanswered. A reader has two answers to one question and no rule to choose between them. That is misleading, so the comment is not a false positive.
- The same gap exists in the sibling bullets. Line 224: a Multi answer with no selection is "not exactly the correct set", but `MultiGrader` treats it as unanswered. Line 226: an empty numeric Short answer "does not parse as a number", but `TextGrader.cs:74-77` returns unanswered first. CodeRabbit's patch fixes only line 225. Lines 224 and 226 would stay ambiguous, and the list would qualify one bullet but not the other two.

**Exact change (doc-only, `docs/question-schemas.md:223`).** Replace the first sentence of the bullet:

  - An unanswered answer returns «لم تتم الإجابة عن السؤال.» / "No answer was given." An answer is unanswered when:

with:

  - An unanswered answer returns «لم تتم الإجابة عن السؤال.» / "No answer was given." This rule is checked first; the rules below apply only to an answer that is not unanswered. An answer is unanswered when:

Leave the rest of line 223, and lines 224-227, unchanged. No code or test change is needed. Postman is not affected.
