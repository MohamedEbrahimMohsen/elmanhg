# [E3.S5] Teacher validation queue

Issue: #68

As a teacher I can approve or reject pending questions in my subjects so that only validated content is served. PRD §8.1.

Epic: #63

### Sub-tasks
- [ ] Pending questions query scoped to assigned subjects with unit, lesson, type, difficulty, age filters
- [ ] Approve command, optional difficulty change, records validated_by and validated_at
- [ ] Reject command requiring a reason
- [ ] Guard: teachers cannot edit question content; admins cannot approve
- [ ] Teacher UI: queue list, question detail with revision and rejection history, approve/reject actions
- [ ] Bulk approve only for questions opened in the current session
