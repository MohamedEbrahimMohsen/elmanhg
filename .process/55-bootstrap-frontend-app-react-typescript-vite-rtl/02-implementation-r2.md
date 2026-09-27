# Implementation r2 — Bootstrap frontend app (React, TypeScript, Vite, RTL) (#55, E1.S2)

## Blocking findings addressed
| # | Change | File:line |
|---|---|---|
| 1 | Added `expect(screen.queryByRole('button')).toBeNull();` to the `asChild` test, so a `<button>` wrapping the anchor now fails it. | `web/src/shared/ui/button.test.tsx:21` |
| 2 | Rewrote the **Navigation** mobile/desktop sentences in § 2.4 to match `docs/design-system.md` §5.7: at most 4 items, label 12 px (micro), 3 primary plus "المزيد" opening the rest, desktop (≥ 900 px) shows every destination. Icon, colour, underline and sub-tab wording is unchanged. | `docs/claude-design-prompt.md:102` |

## Files created
None.

## Files modified
| Path | Change |
|---|---|
| `web/src/shared/ui/button.test.tsx` | Added one assertion (finding 1) |
| `docs/claude-design-prompt.md` | Updated the § 2.4 Navigation paragraph (finding 2) |

## Deviations
None.

## Build & test
- `npm --prefix web test -- --run`: Test Files 15 passed (15), Tests 70 passed (70).
- Mutation check: I temporarily changed `button.tsx:29` to `const Comp = 'button';` and ran `npx vitest run src/shared/ui/button.test.tsx`. Result: 1 failed | 1 passed (2). I then restored the file from a backup and confirmed line 29 matches the original.

## Notes for review
None of the non-blocking items were touched.
