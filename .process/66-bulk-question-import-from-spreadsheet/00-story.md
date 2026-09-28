# [E3.S3] Bulk question import from spreadsheet

Issue: #66

As an admin I can import many questions at once so that reaching 100,000 is feasible. PRD §10.1.

Epic: #63

### Sub-tasks
- [ ] Spreadsheet template per v1 type with documentation
- [ ] Import command: parse, validate rows, report errors per row, create as pending
- [ ] Idempotency via import batch id
- [ ] Admin UI: upload, dry-run report, confirm import
