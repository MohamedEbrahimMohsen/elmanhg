# CodeRabbit triage — PR #218 (done after the merge, orchestrator error)

- RC1/RC2 (an empty or sanitised-away model answer is accepted server-side): **fix in #118**. Verified: `EssayQuestionRules.cs:27` checks the raw string only.
- RC3/RC4 (the preview and shared i18n say essays are AI-graded now): **fix in #118**, meaning the wording must say grading is not available yet until #118 ships. #118 itself adds the grader, so the final wording depends on its behaviour.
- PC1: review body only.
