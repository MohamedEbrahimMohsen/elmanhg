# [E3.S1] Question aggregate with typed body and grading spec

Issue: #64

As an admin I can create questions of the v1 types so that the bank can be built. PRD §5.4, §6.

Epic: #63

### Sub-tasks
- [ ] Question entity with JSONB body and grading_spec, difficulty, objective link, explanation, max score, version
- [ ] Per-type body and grading_spec schemas with validation for mcq, multi, tf, fill, short
- [ ] Create and update commands; content edit on approved question resets to pending and bumps version
- [ ] QuestionRevision snapshot on every content edit
- [ ] Unit tests for the reset-to-pending rule and version increments
