# [E18.S7] Student AI chat history and deletion

Issue: #271

Epic: #252

As a student I can see my past AI tutor chats, reopen one to continue it, and delete any chat I no longer want.

Dev decision on #215 (2026-10-02): keep the history by default, and let the student delete chats they don't need ("why not allowing the student directly delete the not-needed chats?").

**Rules**
- **History view:** the student sees only their own conversations, newest first. Each shows the lesson/subject, the date and the first line. A student can open one and continue it from the avatar panel.
- **Delete:** the student deletes one conversation, after a confirmation, and it is gone.
  - Its messages are removed from `AvatarMessages`. The append-only trigger needs a narrow, explicit erasure path; it must not be dropped.
  - Its copies in `AvatarTrainingRecords` are removed too. They are the student's text, even though they sit under a hashed id.
- **Usage counters stay:** `AvatarMessageUsage` rows hold no text and enforce the free-tier daily limit, so deleting a chat must not reset the limit.
- **Audit:** the deletion is audited (student id, conversation id, message count), with no message text.
- **Teacher and admin views:** they no longer show a deleted conversation.
- **Config:** a runtime setting, "students can delete chats", on the Configuration page, default on.

### Sub-tasks
- [ ] Student conversation list and detail queries, scoped to the caller
- [ ] Delete command: an erasure path through the append-only trigger, removal of the training-record copies, audit, and usage counters kept
- [ ] Student history UI: list, reopen in the avatar panel, delete with confirmation; ar/en, RTL
- [ ] Runtime setting and Configuration page entry
- [ ] Docs: `docs/avatar.md` (retention, privacy, history view), `docs/training-data.md` (erasure), PRD avatar section, `docs/configuration.md`; close #215

