# Implementation r2 — Indigo calm theme and header/alignment fixes (#276, E19.S1)

## Findings addressed
| # | What I changed | file:line |
|---|---|---|
| 1 (blocking) | The assistant dock button is no longer rendered on `/student/exam/$sessionId`. The signal is `useMatch({ from: '/student/exam/$sessionId', shouldThrow: false })`, which is route-based. `AvatarPanel`'s `examInProgress` comes from the avatar status query, and that query only loads inside the lazy panel. Reusing it in the dock would add a status fetch and its code to every student page. The panel is still mounted, and the exam bar's own Ask button still opens it. I tried `useMatchRoute` first, but it pushed quiz to 256/255. `useMatch` is under budget. Test added. F2 row in `02-layout-audit.md` now records the overlap: dock 96–144 px vs bar 64–134 px, computed from the classes and not re-measured in a browser. | `web/src/features/avatar/components/AvatarDock.tsx:1,15,23`; `AvatarDock.test.tsx:27`; `.process/.../02-layout-audit.md` F2 |
| NB contrast | Added a `toThrow` case for `relativeLuminance('#FFF')` | `web/scripts/tokens/contrast.test.ts:50` |
| NB focus-out | A native `focusout` listener on the menu root closes the menu when `relatedTarget` is a node outside it. A null `relatedTarget` is left to the pointerdown handler, so Safari link clicks still work. I did not use `onBlur` on the div because it fails jsx-a11y `no-static-element-interactions`. Test: shift+Tab out of the open menu. | `web/src/features/shell/components/TopNavMore.tsx:34-46`; `TopNavMore.test.tsx:66` |
| NB double gutter | Changed to `cn(layoutContainerClassName, 'py-6 in-[main]:px-0 lg:in-[main]:px-0')`. The container keeps its gutter for the root-level not-found, which renders with no shell, and drops it inside the shell `main`. E11 row updated. | `web/src/shared/components/NotFound.tsx:10`, `RouteError.tsx:20` |
| NB 44 px | Sign-out gets `max-lg:min-h-11`, so it is 44 px on touch sizes. The 36 px top-nav pills and sign-out appear only at ≥ 900 px, the pointer layout. That exception is now documented in both design-system docs. | `web/src/features/shell/components/AppBar.tsx:32`; `docs/design-system.md:177,179`; `.claude/design-system.md:94,160` |
| NB More active | Test: on `/admin/audit` the More root contains a `data-status="active"` link, which is what `group-has-[[data-status=active]]` keys on, and the button carries that class. On `/admin` it contains none. | `TopNavMore.test.tsx:77` |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Reuse the existing exam-in-progress signal (`examInProgress`) | That signal is server status, and it is fetched only inside the lazy `AvatarPanel`. Using it in the dock costs a request plus bundle on every student page, and quiz is at the limit. | Used route match `useMatch` on the exam route. |

## Build & test (web, in the worktree)
- `npm run typecheck`: exit 0.
- `npm run lint`: exit 0. The first run flagged `onBlur` on a div (jsx-a11y); I fixed it by using the `focusout` listener.
- `npx prettier --check . --end-of-line auto`: "All matched files use Prettier code style!"
- `npx vitest run --maxWorkers=4`: 283 files and 1673 tests passed, with no timeouts. I ran it after the final change.
- `npm run build`: ok.
- `npm run perf:budget`: entry 209/210, landing 218/220, lesson 240/240, quiz 255/255, admin-dashboard 229/230, admin-users 263/270, teacher-home 257/265, all ok. Exact brotli slack: quiz 5 bytes, lesson 297 bytes. `budgets.json` is untouched.

## Notes for review
- Quiz has 5 bytes of headroom left. The next byte on the student path will trip it.
- While the panel's lazy chunk loads, the Suspense fallback still shows a disabled dock pill for a moment, even on the exam route. I left it alone to stay within the byte budget.
- The F2 overlap rects are computed from the classes and were not measured in a browser.
