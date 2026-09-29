# [E14.S2] LLM essay grader

Issue: #118

As the grader I return per-criterion scores, an Arabic justification and a confidence value.

Epic: #116

### Sub-tasks
- [ ] Grading prompt and structured JSON output in the AI service
- [ ] Grading job in .NET calling the AI service asynchronously with retry
- [ ] Pending state shown to the student until the grade is final
- [ ] Evaluation set of teacher-graded essays to measure agreement
