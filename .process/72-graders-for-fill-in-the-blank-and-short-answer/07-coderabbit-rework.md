# CodeRabbit rework — PR #163 (story #72)

| # | What I changed | File:line |
|---|---|---|
| RC1 | Added the precedence sentence "This rule is checked first; the rules below apply only to an answer that is not unanswered." to the unanswered-feedback bullet, using the triage's wording exactly. The rest of line 223 and lines 224-227 are unchanged. | `docs/question-schemas.md:223` |

## Verification against code
- `ChoiceGrader.GradeMcq` / `GradeTrueFalse` return `NormalisedGrade.Unanswered` on a null option/value before comparing.
- `ChoiceGrader.GradeMulti` returns `Unanswered` when no non-null id is selected, before the tally feedback.
- `TextGrader.GradeFill` returns `Unanswered` when every blank normalises to empty, before the hit count and the single/multi-blank feedback branch.
- `TextGrader.GradeShort` (text) and `GradeNumeric` return `Unanswered` on an empty normalised answer, before matching and before the parse-failure feedback.

So the unanswered check comes first in every grader, and the new sentence matches the code.

## Deviations
None.

## Build & test
Not run. This is a doc-only change. `git diff --stat` shows only `docs/question-schemas.md` changed among tracked files, apart from the `04-metrics.md` change that was already in the working tree before this rework.
