# CodeRabbit triage — PR #218 (done after the merge, orchestrator error)

- RC1/RC2 (a nonblank model answer containing only removable markup can pass validation and be persisted as empty): **fix in #118**. `EssayQuestionRules.Validate` checks the raw string, while `EssayQuestionRules.Normalize` sanitizes model answers before the question is saved.
- RC3/RC4 (the preview and shared i18n say essays are AI-graded now): **fix in #118**, meaning the wording must say grading is not available yet until #118 ships. #118 itself adds the grader, so the final wording depends on its behaviour.
- PC1: review body only.
