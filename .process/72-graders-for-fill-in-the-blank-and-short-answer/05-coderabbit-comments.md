# CodeRabbit comments — PR #163

Collected 2026-09-28 11:16. Review 5337735647 ("Actionable comments posted: 1"). Verbatim.

## RC1 — `docs/question-schemas.md:225` (🟡 Minor, Functional Correctness)

**Qualify the Fill feedback rules for answered responses.**

When every blank normalises to empty, `TextGrader.GradeFill` returns unanswered feedback before it evaluates the tally or single-blank branches. Qualify line 225 so its rules apply only when at least one blank has a nonempty normalized response.

Suggested:
- A Fill answer with two or more blanks that has at least one blank whose response normalises to nonempty and is not fully correct returns «الفراغات الصحيحة: {right} من {total}.» / "Correct blanks: {right} of {total}." A single-blank Fill whose response normalises to nonempty returns `null`.
