# Implementation — [E19.S6] Floating assistant button must not cover page content (#296)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `web/src/features/avatar/hooks/useAssistantDockShown.ts` | 7 | Single source for "the dock button shows on this route": false on `/student/assistant` (and its children) and `/student/exam/$sessionId`. |
| `web/src/features/shell/components/AppShell.assistantSpace.test.tsx` | 58 | 4 tests: room reserved on `/student`; none on the full-page assistant, the exam route, or for teachers. |

## Files modified
| Path | Change |
|---|---|
| `web/src/styles/app.css` | New utilities `pb-assistant-dock` (`--ds-space-1 * 40` = offset 96 + height 48 + gap 16, plus `env(safe-area-inset-bottom)`) and `pb-assistant-dock-desktop` (`* 22` = 24 + 48 + 16, plus safe-area). |
| `web/src/features/shell/components/AppShell.tsx` | New prop `reserveAssistantSpace?: boolean` (default false). `<main>` gets `pb-assistant-dock lg:pb-assistant-dock-desktop` when true, otherwise the old `pb-24 lg:pb-8`. Only one pair is ever applied. |
| `web/src/features/avatar/components/AvatarDock.tsx` | Uses `useAssistantDockShown()` in place of its own exam `useMatch`. Same behaviour, same class string, panel unchanged. |
| `web/src/features/avatar/index.ts` | Exports `useAssistantDockShown`. |
| `web/src/routes/student/route.tsx` | The inline component is now a named `StudentLayout` that calls the hook and passes `reserveAssistantSpace` to `AppShell`. |
| `docs/design-system.md` | Assistant section: one sentence saying student pages that show the button keep room below their content for it, and the full-page assistant and exam pages do not. |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| First draft of the plan put the tests in `AppShell.test.tsx` | That file is already 177 lines | Put them in a sibling `AppShell.assistantSpace.test.tsx` (same pattern as `AvatarDock.lazy.test.tsx`) and updated `01-plan.md` before reporting. |
| Orchestrator: check with DOM measurement on a production build at 375/768/1280 | `vite build` and `vite preview` (proxied to :8080) both ran, and I could sign in to the demo. But headless Chromium would not start in the sandbox: `browserType.launch: spawn UNKNOWN`. Running it again with the sandbox turned off was denied. | **No browser DOM measurement was done.** I checked by arithmetic and by the built CSS instead (see Notes). The measurement script is in the session scratchpad (`measure.mjs`, not committed). It signs in as the first seeded load-test student, scrolls each student route to the end at the 3 widths, and compares the bottom of the last focusable element with the dock's top. It also re-measures with the old `pb-24` as a before/after control. |

## Build & test
- `npm ci`: the worktree had no node_modules. 687 packages added.
- `npm run typecheck` (`tsc -b`): clean.
- `npm run lint` (`eslint . --max-warnings=0`): clean.
- `npx prettier --check --end-of-line auto .`: first run flagged `AppShell.tsx`. Fixed with `--write`, then clean.
- `npx vitest run` (full): `Test Files 307 passed (307)`, `Tests 1821 passed (1821)`. No timeouts.
- `npm run build`: `✓ built in 15.92s`, `Precompressed 298 files`. The built `index-*.css` contains `.pb-assistant-dock{padding-block-end:calc(var(--ds-space-1) * 40 + env(safe-area-inset-bottom))}` and `.lg\:pb-assistant-dock-desktop{… * 22 + env(safe-area-inset-bottom))}`. The `lg` rule comes before `.lg\:pb-8`, which is fine because the two pairs are never applied together.
- `npm run perf:budget`: entry 189/210, landing 199/220, lesson 221/240, quiz 236/255, admin-dashboard 213/233, admin-users 245/270, teacher-home 245/265, assistant 244/260. All ok, no budget changed.
- CI greps (physical directions; literal tokens/arbitrary px): both clean.
- `git status` after the build shows no `routeTree.gen.ts` or `tokens.css` drift.

## Notes for review
- Root cause: `<main>` reserved only `pb-24` (96 px, the tab bar). On mobile the dock (`bottom-24 h-12`) has its top edge 144 px above the viewport bottom. On desktop (`lg:bottom-6 h-12`) it is 72 px up, against `lg:pb-8` (32 px). So the last 48 px (mobile) or 40 px (desktop) of content stayed under the pill.
- Arithmetic check: once scrolled to the end, the last element's bottom is at most `viewport − padding`. Mobile/768 (`lg` is 900 px in tokens, so 768 uses the mobile values): `vh − 160 ≤ vh − 144`, a 16 px gap. 1280: `vh − 88 ≤ vh − 72`, also 16 px. Short pages that do not scroll still end at least the padding above the bottom, because `<main>` is the last block in a `min-h-dvh` shell.
- Safe-area: the padding adds `env(safe-area-inset-bottom)` as the story asks. The dock's own offset does not include the inset, and I left it alone ("no change to the dock's look or position"). On notched phones this gives a little extra room, never less.
- The space is tied to the route, not to `state.isOpen`, so opening the panel does not shift the page. `AvatarDock` and the shell now read the same hook, so the two cannot drift apart.
- The tests use `toHaveClass` on `getByRole('main')`, as `ExamPage.test`, `FeedbackPanel.test` and `TopNavMore.test` already do. jsdom has no layout, so a pixel check would need browser mode, which this repo has not set up.
- `route.tsx` now has a 9-line `StudentLayout` that calls one hook. That is composition only, but a reviewer may want to check it against the skill's "route files hold no business logic" rule.
- Nothing was committed. The preview server I started has been stopped.
