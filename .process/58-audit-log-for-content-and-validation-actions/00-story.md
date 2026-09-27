# [E1.S5] Audit log for content and validation actions

Issue: #58

As an admin I can see who changed what and when so that content disputes can be resolved. PRD §14, §17 rule 13.

Epic: #53

### Sub-tasks
- [ ] AuditLog entity and append-only repository
- [ ] Domain event handler that writes an audit row for content changes and validation decisions
- [ ] Query with filters: actor, entity type, date range
- [ ] Admin audit log page with pagination
