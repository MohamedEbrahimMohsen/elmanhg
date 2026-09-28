# [E4.S2] Graders for mcq, multi-select, true/false

Issue: #71

As the grader I return exact or partial scores for choice questions. PRD §6.

Epic: #69

### Sub-tasks
- [ ] Grader interface returning score, normalised score and feedback
- [ ] MCQ and true/false exact match graders
- [ ] Multi-select grader with optional partial credit max(0, (correct minus wrong) divided by total)
- [ ] Unit tests including the partial-credit edge cases
