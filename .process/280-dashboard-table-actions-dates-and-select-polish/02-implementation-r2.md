# Implementation r2: Dashboard, table actions, dates and select polish (#280)

## Rework of 03-review.md findings

| # | What I changed | File:line |
|---|---|---|
| 1 (blocking) | Button notes: "36 for `sm` in dense admin tables" now reads "36 for `sm` table row actions, a documented exception to the 44 rule" | `.claude/design-system.md:86` |
| 1 | Accessibility: the touch-target rule now lists two exceptions. (1) is the desktop top nav and sign-out. (2) is 36 px `sm` row actions in every table, student and admin, because rows are dense; adjacent actions keep adequate spacing per WCAG 2.5.8 | `.claude/design-system.md:163` |
| 1 | Ghost "Use" cell changed from "Inline actions in tables" to "Inline text actions outside table rows (table row actions are secondary or danger `sm`)" | `docs/design-system.md:158` |
| 1 | §5.1 sizing sentence: 36 px `sm` is for table row actions in every table (student and admin). It is a documented exception to the 44 px rule, with the WCAG 2.5.8 spacing note | `docs/design-system.md:160` |
| 1 | §5.7 top nav: "the only exception to principle 5" now reads "one of the two exceptions to principle 5, the other being 36 px `sm` table row actions" | `docs/design-system.md:193` |
| 1 | Ghost "Where" cell: same wording as docs/design-system.md | `docs/claude-design-prompt.md:94` |
| 1 | Buttons paragraph: "dense admin tables" replaced with the same row-action exception wording, so it no longer contradicts line 94 | `docs/claude-design-prompt.md:86` |
| NB | Arabic tile details: the signed number is wrapped in LRI/PDI (`⁦+{count}⁩`, `⁦−{count}⁩`), so the sign stays on the left of the digits inside the RTL paragraph | `web/src/features/dashboard/i18n/ar.json:26,28` |
| NB | Test renamed to "formats ICU numbers with Latin digits in Arabic" | `web/src/app/i18n.test.ts:19` |
| NB | Every substring `toHaveTextContent('…')` is now an anchored regex (`/^80$/u`, `/^1\.5 hours$/u`, `/^40 \(37 within SLA\)$/u`, and so on) | `web/src/features/dashboard/pages/DashboardPage.cards.test.tsx:30-71` |
| NB | Disabled-button hover: **deferred**. I tried `hover:not-disabled:` on all four variant hover fills; I used it instead of `enabled:hover:` because `:enabled` would drop hover from `asChild` links. The build then failed `perf:budget` with `quiz 256/255 KB OVER`. I reverted `button.tsx` byte-for-byte. | `web/src/shared/ui/button.tsx` (unchanged) |

## Files created
None, apart from this report.

## Files modified
| Path | Change |
|---|---|
| `.claude/design-system.md` | Row-action 36 px exception (lines 86, 163) |
| `docs/design-system.md` | Ghost use, `sm` sizing, principle-5 exceptions (lines 158, 160, 193) |
| `docs/claude-design-prompt.md` | Ghost use and the Buttons paragraph (lines 86, 94) |
| `web/src/features/dashboard/i18n/ar.json` | Bidi isolation on the signed count |
| `web/src/app/i18n.test.ts` | Test name |
| `web/src/features/dashboard/pages/DashboardPage.cards.test.tsx` | Anchored text matches |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Fix disabled-button hover only if `perf:budget` passes | `hover:not-disabled:` puts quiz at 256/255 KB | Reverted. Left as deferred. No budget raised. |

## Build & test (all in `web/`)
- `npm run build`: exit 0 (final run is on the reverted button.tsx).
- `npm run perf:budget`: exit 0. entry 209/210, landing 218/220, lesson 240/240, quiz 255/255, admin-dashboard 233/233, admin-users 264/270, teacher-home 258/265.
- `npm run typecheck`: exit 0. `npm run lint`: clean.
- `npx prettier --check . --end-of-line auto`: "All matched files use Prettier code style!"
- `npx vitest run src/app/i18n.test.ts src/features/dashboard`: 12 files, 69/69 passed.
- CI greps (physical direction, literal tokens/hex/arbitrary px): no matches (grep exit 1).
- The full vitest suite was not rerun. Only the affected tests were run.

## Notes for review
- No test asserts the Arabic tile caption, so the LRI/PDI change is checked only by the dashboard tests still passing (English) and by reading the JSON. jsdom cannot show bidi layout.
- The remaining docs-wide "only exception" wording was grepped. None is left that contradicts the code.
