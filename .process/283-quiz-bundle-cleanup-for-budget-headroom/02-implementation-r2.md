# Implementation r2 — Quiz bundle cleanup for budget headroom (#283, E19.S3)

## Rework table
| # | What I changed | file:line |
|---|---|---|
| 1 (blocking) | The guarded hover now excludes the state that has to beat it, so the rules can no longer both match. Primary/accent use `hover:not-disabled:not-active:bg-accent-hover`, and the pressed state keeps `active:bg-accent-pressed`. Secondary uses `hover:not-disabled:not-aria-pressed:bg-soft`, and the toggled state keeps `aria-pressed:bg-accent-soft`. Danger and ghost have no competing state, so they are unchanged. The result does not depend on specificity or on rule order. Disabled buttons still get no hover fill, `asChild` links keep it, and a disabled toggled button keeps `accent.soft`. | `web/src/shared/ui/button.tsx:7`, `:17` |
| 1 (test) | I replaced the class-name tests T20/T21 with cascade tests. They compile the real `src/styles/app.css` with Tailwind's own `compile()` for every `buttonVariants` class string. They then take the `background-color` rules from `@layer utilities`, match each selector against the rendered element, and pick the winner by specificity, then source order. `:hover`/`:active` are rewritten to attribute selectors, which have the same specificity. 13 state cases plus an `asChild` hover case, plus a check of the specificity counter. | `web/src/shared/ui/button.test.tsx:37-83`, helper `web/src/test/tailwindCascade.ts` (new, 64 lines) |
| NB i18n | The re-init branch now has `.catch(() => undefined)`. A failed chunk keeps the current language and no longer becomes an unhandled rejection. Test: `addResourceBundle` throws once, and the test checks that `changeLanguage` is never called and that the language and `lang` stay `ar`. | `web/src/app/i18n.ts:92-94`, `web/src/app/i18n.test.ts:83` |
| NB Arabic | New test: renders `/admin/audit` in Arabic after removing **every** `en` resource bundle. It checks the heading, the "المنفذ" field label and the "تطبيق" button, and that no `page.*`/`filters.*` keys appear. | `web/src/features/audit/pages/AuditLogPage.arabic.test.tsx` (new, 30 lines) |

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `web/src/test/tailwindCascade.ts` | 64 | Compiles app.css with the given classes and resolves which background rule wins |
| `web/src/features/audit/pages/AuditLogPage.arabic.test.tsx` | 30 | Arabic first render of a page-registered namespace with no English bundle |

## Files modified
| Path | Change |
|---|---|
| `web/src/shared/ui/button.tsx` | Added the `not-active` / `not-aria-pressed` hover qualifiers |
| `web/src/shared/ui/button.test.tsx` | T20/T21 replaced by the compiled-CSS cascade tests |
| `web/src/app/i18n.ts` | `.catch` on the re-init load |
| `web/src/app/i18n.test.ts` | The re-init failure test |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| T20/T21 assert class names | They cannot detect precedence (review finding 1) | Replaced them with the cascade tests above |
| New test files only as listed | The rework needed a CSS helper and an isolated Arabic test file. Removing every English bundle has to happen in its own file. | Added the two files above |
| Arabic test: "own registration, no English preload" | In vitest the route imports the `@/features/audit` barrel without tree-shaking, so `registerAuditLocales()` runs when `routeTree` loads, before the test body. A "not yet registered" precondition is therefore impossible in tests. | I assert no English bundle and Arabic output only. If you comment out `registerAuditLocales()` in the page, the test fails (I checked this). |

## Build & test
- `npm run typecheck`: exit 0.
- `npm run lint` (`--max-warnings=0`): clean.
- `npx vitest run`: **297 files, 1738 tests passed**. No timeouts.
- `npx prettier --check --end-of-line auto .`: "All matched files use Prettier code style!"
- `npm run build`: exit 0, "Precompressed 292 files (.br and .gz)."
- `npm run perf:budget`: all ok, no budget changed: entry 190/210, landing 199/220, lesson 221/240, quiz 236/255, admin-dashboard 214/233, admin-users 245/270, teacher-home 246/265.
- Both `web-ci.yml` greps find nothing (exit=1).
- **Built CSS check** (`dist/assets/index-BerJDKqP.css`): `.hover\:not-disabled\:not-active\:bg-accent-hover:hover:not(:disabled):not(:active)` @43238 and `.hover\:not-disabled\:not-aria-pressed\:bg-soft:hover:not(:disabled):not([aria-pressed=true])` @43368. These are (0,4,0) but cannot match while `:active` / `[aria-pressed=true]` holds. Then `.active\:bg-accent-pressed:active` @45309, `.active\:bg-soft:active` @45391 and `.aria-pressed\:bg-accent-soft[aria-pressed=true]` @45884, each (0,2,0). They come after the hover rules and beat the base fills (0,1,0).
- **Regression proof:** I temporarily restored the r1 classes (`hover:not-disabled:*`). Exactly 4 cascade tests failed: primary pressed, accent pressed, toggled secondary hovered and toggled secondary pressed. That matches the review's failure cases. With the `.catch` removed, vitest reports an unhandled rejection from the new i18n test (run errors). Both changes were restored afterwards.

## Notes for review
- The cascade helper ignores `@media (hover: hover)`. It treats that media query as matching, which is the desktop case under review.
- The helper uses `/// <reference types="node" />` and `node:fs`, following the precedent in `src/test/nodeFormData.ts`. `?raw` CSS imports come back empty under vitest, because CSS is stubbed. It reads paths relative to `web/`, which is vitest's root.
