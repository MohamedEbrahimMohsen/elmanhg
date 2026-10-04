# [E20.S1] Full-page AI assistant chat

Issue: #288

Epic: #287

As a student I want a full-page AI assistant that feels like a normal chat app, alongside the existing floating assistant panel.

Dev request (2026-10-04): "for the smart assistant, it should have also independent page, where it looks like the normal chat experience. but don't change the current one it's perfect, just add the new page for it and any navigation to it."

**Rules**
- **Do not change the existing floating `AvatarPanel` / `AvatarDock`.** The behaviour, look and lesson-context entry points stay as they are.
- **New student route** (e.g. `/student/assistant`) with a normal chat layout:
  - a conversation list on the start side, from the #271 history: new chat, reopen, delete with the same rules and runtime flag;
  - the messages in the main area, with the composer at the bottom;
  - on mobile, the list collapses into a drawer or back button.
- **Same backend and rules as the panel:**
  - the same endpoints, plan limits, citations and lesson sources;
  - the same exam-in-progress block: the page shows the exam notice and does not chat;
  - the same deletion setting and the free-tier daily limit display.
  - Context: when the student starts from the page, they pick a subject and lesson (optional). Reopened chats keep their context.
- **Navigation:**
  - an «المساعد» item in the student desktop nav and the mobile «المزيد» menu;
  - a "Open full page" link inside the existing panel header. This is the only allowed change to the panel: one link.
  - Deep links to a conversation (`/student/assistant/$conversationId`).
- **Shared code:** reuse the panel's hooks and API layer. Share the message list and composer components where it doesn't change the panel's look.
- **Budget:** the page is its own lazy route chunk. The quiz, lesson, entry and landing budgets must not grow beyond the nav entry. Add a budget entry for the new route.
- **Quality:** ar/en and RTL, Indigo calm tokens, keyboard and screen-reader friendly (messages are a live region), and tests.

### Sub-tasks
- [ ] Route, layout, conversation list and chat view, reusing the existing avatar API and hooks
- [ ] Nav entries and the panel's "open full page" link
- [ ] Exam block, plan limits, deletion flag and context picker
- [ ] Tests, budget entry, docs (`docs/avatar.md`, PRD avatar section, `docs/claude-design-prompt.md` §4–6)

