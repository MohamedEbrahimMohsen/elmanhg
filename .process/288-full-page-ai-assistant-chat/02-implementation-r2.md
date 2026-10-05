# Implementation r2 — Full-page AI assistant chat (#288, E20.S1)

## Review findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | §5.7 now says "student all 6". `.claude/design-system.md` has no count in its TopNav row (line 95), so I left it unchanged. | `docs/design-system.md:193` |
| 2 | T43 now clicks «Open full page», checks it lands on `/student/assistant`, then clicks «Home» in Main navigation. It waits for `/student` and asserts that the dock pill `button 'Assistant'` shows and no `dialog` is open. **Mutation check:** I removed both `dispatch({ type: 'close' })` calls in `AvatarPanelHeader.tsx`, and T43 failed ("Unable to find role=button name=Assistant"). I then restored the file from a backup. Its diff vs main is still +29 -2. The panel tests were not edited. | `web/src/features/avatar/components/AvatarPanelHeader.test.tsx:34-50` |

## Non-blockers taken
| Item | Change | File:line |
|---|---|---|
| A non-GUID id shows a generic error | `notFound` now means any `ApiError` with `status === 404`, not only the `AVATAR_CONVERSATION_NOT_FOUND` code. New test `shows not found for a chat id that is not a GUID`, using a bare 404 with no body. It fails against the old code; I mutation-checked it and restored the fix. | `web/src/features/avatar/hooks/useAssistantConversation.ts:73`; `AssistantPage.history.test.tsx:189` |
| Back/forward | New test `follows browser back and forward between two chats`. It opens chat A, then chat B, and uses `router.history.back()` and `forward()`. Each time it asserts the reply, the absence of the other chat's reply, and the pathname. | `web/src/features/avatar/pages/AssistantPage.history.test.tsx:199` |
| Weak URL check in T19 | It now asserts `aria-disabled="true"` on the list item. After `release()`, it waits for the pathname to equal the new chat id (`avatarConversationId`) and checks that the item is not `aria-current`. Mutation check: with `event.preventDefault()` removed in `AssistantConversationItem.tsx`, T19 fails. I restored it. | `web/src/features/avatar/pages/AssistantPage.test.tsx:228-238` |
| Doc for the 404 change | avatar.md Full page URL bullet: "An unknown, malformed or deleted id (any 404) shows…" | `docs/avatar.md:204` |

## Files modified (this round)
`docs/design-system.md`, `docs/avatar.md`, `web/src/features/avatar/hooks/useAssistantConversation.ts`, `web/src/features/avatar/components/AvatarPanelHeader.test.tsx`, `web/src/features/avatar/pages/AssistantPage.test.tsx`, `web/src/features/avatar/pages/AssistantPage.history.test.tsx`.

## Deviations
None.

## Build & test (web, all observed)
- `npm run typecheck`: exit 0.
- `npm run lint`: exit 0 (after I changed the two `act(async …)` calls to sync `act` to satisfy `require-await`).
- `npx prettier --check --end-of-line auto .`: "All matched files use Prettier code style!"
- `npm test -- --run`: 304 files, 1787 tests passed, no timeouts.
- `npm run build`: exit 0, 298 files precompressed.
- `npm run perf:budget`: entry 190/210, landing 199/220, lesson 221/240, quiz 237/255, admin-dashboard 214/233, admin-users 246/270, teacher-home 246/265, assistant 245/260, all ok. No budget changed.
- CI greps (logical-properties and raw-colour from `web-ci.yml`): no matches in either.

## Notes for review
- The 404→notFound broadening also covers a route-level 404 with no code body. The page already treats an owner-scoped 404 as not-found, so no data leaks.
- Not taken: the toggle does not collapse on a second press, the dock is not tested on the `$conversationId` route, and the stale panel id after a page delete. The reviewer judged these acceptable, and they are outside the requested items.
