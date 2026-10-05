VERDICT: CHANGES_REQUESTED

# Review — Full-page AI assistant chat (#288, E20.S1)

## Blocking

### 1. The design-system doc still says the student top bar has 5 destinations
**Where:** `web/src/features/shell/navConfig.ts:86` (`topBarKeys` now lists 6 student items, including `assistant`) vs `docs/design-system.md` §5.7 Navigation, line 193: "Each role has a fixed top-bar list: student all 5, teacher all 4, …"
**Rule:** `.claude/rules/docs-sync.md`: a divergence (UI component rules → `docs/design-system.md`); reviewer Review order #8.
**Problem:** This change adds a sixth student destination («المساعد») to the desktop top bar, but the doc that owns the nav rules still says five. The plan's Docs table missed this sentence, and the implementer did not catch it.
**Failure:** The doc and the code give different answers to "how many pills does the student top bar show?": the doc says 5, and `AppShell.test.tsx` now expects 6.
**Fix:** In `docs/design-system.md` §5.7, change "student all 5" to "student all 6". Check whether `.claude/design-system.md` needs a matching edit. Its TopNav row has no count today, so it probably does not.

### 2. T43 does not check that the panel closes: it passes with the close dispatch removed
**Where:** `web/src/features/avatar/components/AvatarPanelHeader.test.tsx:34-42` (test `opens the full page from the panel header and closes the panel`), which guards `web/src/features/avatar/components/AvatarPanelHeader.tsx:32-34` and `:42-44`.
**Rule:** `.claude/conventions/react-testing.md` "Every test must … fail if the production line under test is removed (non-vacuous)"; plan D17 (`onClick` dispatches `close`).
**Problem:** The test's only proof of "closes the panel" is `queryByRole('dialog')` being absent on `/student/assistant`. But `StudentAvatarDock` (`StudentAvatarDock.tsx:5-6`) unmounts `AvatarDock` on the assistant routes, and the panel unmounts with it. So the dialog is gone whether or not `close` is dispatched. Nothing checks the global `isOpen` state.
**Failure:** Delete both `dispatch({ type: 'close' })` click handlers and T43 still passes. In the app, a student who uses «فتح في صفحة كاملة» and then goes back to `/student` would find the panel open again: `AvatarDock` mounts with `state.isOpen === true`, which shows the panel at once and hides the «المساعد» pill.
**Fix:** In T43, after landing on the page, leave it, for example by clicking the «Home» nav link. Then assert that `queryByRole('dialog')` is null and that the dock pill `getByRole('button', { name: 'Assistant' })` is shown.

## Non-blocking
- `web/src/features/avatar/hooks/useAssistantConversation.ts:73-77`: a deep link with an id that is not a GUID (`/student/assistant/abc`) misses the API route constraint `{conversationId:guid}` (`api/Elmanhg.Api/Controllers/Avatar/AvatarController.cs:50`). The response is a 404 with no `AVATAR_CONVERSATION_NOT_FOUND` code, so the page shows «تعذّر فتح المحادثة.» with a Retry that can never work, instead of not-found. No data leaks. Consider treating any 404 as `notFound`.
- No test covers browser back/forward between `/X` and `/Y`. I traced the code: route sync `useAssistantConversation.ts:37-46` dispatches `opening(X)` when it returns to X, which is correct. A history test would lock this in.
- `web/src/features/avatar/pages/AssistantPage.test.tsx:228-229` (T19): the pathname is asserted right after the click. TanStack navigation commits asynchronously, and `replied` would still render the reply after an `opening`. So this assertion may not reliably fail if `preventDefault` were removed. Also assert, after `release()`, that the pathname is the new chat id and not `myAvatarConversationId`.
- `web/src/features/avatar/components/AssistantToolbar.tsx:162-171`: the «محادثاتي السابقة» toggle exposes `aria-expanded`, but pressing it again does not collapse the list. «العودة إلى المحادثة» does go back.
- Hiding the floating dock is tested only on `/student/assistant` (T21), not on `/student/assistant/$conversationId`. The code (`useMatch` on the parent route) covers both.
- If the page deletes a chat that is also open in the floating panel, the panel still holds that id. Its next send gets the `conversationGone` notice and recovers, so the result is acceptable.

## Verified
- **The panel is unchanged.** `git diff origin/main --stat` over `features/avatar/components`, `hooks` and `api`, plus `app/`, `shared/` and `AppShell.tsx`, lists only `AvatarPanelHeader.tsx` (+29 -2: imports, the `state` destructure, one link block under the same condition as the history button). The AvatarPanel, AvatarHistory, AvatarDock, AvatarDock.lazy, avatarReducer and navConfig tests are unedited and pass.
- The only existing tests edited are T47 (array only), T48 (added) and T49 (added), all listed in the plan.
- **Routes:** the parent `/student/assistant` has no Outlet, and the child `$conversationId` has no component (D1). Route-sync tracing for new chat (replace), deep link, list push, New chat, deleting the open chat, and conversationGone matches the plan's scenario table. T12 proves there is no remount: both replies and the first question survive the URL change.
- **Exam:** no list, toolbar, picker or detail request (`canLoad` false; T14 counts 0 detail calls), and a disabled composer. Deviation 2 (phase `idle` when `!canLoad`) is correct and needed.
- **Plan limit:** the Free quota line, the daily-limit notice with Subscribe, and the composer disabled at 0 (T10, T15).
- **Picker:** locked lessons are disabled and labelled «(subscribers only)» (T39); a lesson sends `Lesson` with its `lessonId` (T38); a subject alone keeps `Global` (T40); the context line replaces the picker once the chat starts (T41).
- **Pending:** the typing line shows, New chat is disabled, and list links are `aria-disabled` with `preventDefault` (T19). Citations link to the lesson tab (T11).
- **Error, retry and empty:** status, list, open (not found and error), picker error and the empty list are all present and tested (T17, T25, T26, T30, T31, T42).
- **Accessibility:** the shared `role=log aria-live=polite` log is used; focus returns to the question field after a reply (T13); mobile toggling moves focus to the heading shown (T36); `aria-current=page` is on the current item (T27); axe finds no violations (T23).
- **Security:** the API detail is owner-scoped (`MyAvatarConversationsEndpointTests.GetMineDetail_OtherStudents_Returns404`). On a 404 the page renders only the not-found alert and New chat, with no composer and no messages (`AssistantChat.tsx:39-47`).
- **Deviations 1-4 (and the F16 clarification):** each matches the code (`AssistantChat.tsx:24,63`; `useAssistantConversation.ts:70-77`; `AssistantComposer.tsx:30-37`; `AssistantConversationList.tsx:40-44`) and is justified.
- **Bundle:** the page is reachable only through `features/avatar/assistant.ts`, and `index.ts` does not export it; `app/i18n.ts` is untouched; the ar and en `assistant` keys match.
- **Runs (by me):** typecheck 0; lint 0; prettier `--end-of-line auto` clean; `npm test -- --run --coverage`: 304 files and 1785 tests passed, no timeouts, All files 95.42 % statements and 84.38 % branches; build 0; the route tree is unchanged after the build (+52 lines vs origin only); both CI greps show no matches; `gen:tokens` gives no diff.
- **perf:budget:** entry 190/210, landing 199/220, lesson 221/240, quiz 237/255, admin-dashboard 214/233, admin-users 246/270, teacher-home 246/265, assistant 245/260, all ok. No existing maxKb changed. 260 = 245 x 1.05 rounded up to the next 5, per performance.md §3.
- **Docs:** avatar.md (entry points, Global, history, exam gate, runtime flag, streaming, UI link, the Full page section), PRD §9.1 and §9.4, claude-design-prompt §4 and §6, prototype.md, performance.md §3 and §4, design-system §5.10 with the matching `.claude` AssistantPage row, and backlog E20 (valid JSON, tasks verbatim from the story) all agree with the code, except Blocking #1.
- **Postman:** no API change, so nothing to sync.

## Test quality
- `assistantReducer.test.ts`: constrains every branch, including `clearsOpening` for both ids.
- `assistantSearchSchema.test.ts`, `assistantLocales.test.ts`: adequate.
- `AssistantPage.test.tsx`: strong. It checks request bodies, URLs, focus and the exam call count. T19 is somewhat weak (see Non-blocking).
- `AssistantPage.history.test.tsx`: strong. It covers open, deep link, not found, error and retry, delete, delete of the open chat, the flag, pagination and mobile focus.
- `AssistantPage.context.test.tsx`: strong. It checks request bodies and disabled options.
- `AvatarPanelHeader.test.tsx`: T44 and T45 constrain the code. T43 is vacuous on the close behaviour (Blocking #2).
- T47-T49 constrain the nav order, the More list and the capability.
