# [E1.S4] Role-based authorisation and teacher subject scoping

Issue: #57

As the platform I enforce the permission matrix server-side so that a teacher can only act on assigned subjects. PRD §16.

Epic: #53

### Sub-tasks
- [ ] Authorization policies per role applied to every endpoint
- [ ] TeacherSubject entity and admin commands to assign/unassign subjects
- [ ] Subject-scope filter behaviour that rejects any teacher request outside assigned subjects
- [ ] Integration tests: physics teacher cannot read or mutate math content
- [ ] Frontend: hide navigation the current role cannot use
