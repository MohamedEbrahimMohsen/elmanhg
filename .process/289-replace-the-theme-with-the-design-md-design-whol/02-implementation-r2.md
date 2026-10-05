# Implementation r2 — Replace the theme with the DESIGN.md design (#289)

## Findings addressed

| # | What I changed | file:line |
|---|---|---|
| 1 | Every filter form's Apply is now `SubmitButton variant="secondary"` | `web/src/features/users/components/UserFiltersForm.tsx:55`, `audit/components/AuditLogFilters.tsx:43`, `payments/components/PaymentLogFilters.tsx:60`, `questions/components/QuestionListFilters.tsx:69`, `avatarConversations/components/AvatarConversationFilters.tsx:46`, `questions/components/ValidationQueueFilters.tsx:81` (all under `web/src/features/`) |
| 1 | Users "Clear filters" in the no-results state is secondary, so «دعوة» stays the single mint on Teachers/Admins (Students has none) | `web/src/features/users/components/UserListEmptyState.tsx:18` |
| 1 | Grep of every mint-rendering `Button`/`SubmitButton` (explicit `primary` or no variant) found three more screens with two mints. Content tree expanded: one NameForm submit per subject/unit plus rename. Fixed with a new `NameForm` `submitVariant` prop (default `secondary`), and `ContentPage` passes `primary` for «إضافة مادة». Question import after a clean check: Check is now secondary. Free-student Home: the plan-line «اشترك» is now secondary | `web/src/features/content/components/NameForm.tsx:19,29,48`, `content/pages/ContentPage.tsx:59`, `questions/components/QuestionImportForm.tsx:78`, `subscription/components/PlanSummaryLine.tsx:25` |
| 1 | Regression tests. New helper `mintButtons()` returns the `[data-slot=button].bg-action` elements. The filtered-empty tests on Audit, Payments, Questions, Avatar conversations and Validation queue assert exactly 1 mint. Users Students filtered-empty asserts 0. A new test, `keeps Invite as the only mint button on a filtered empty Teachers tab`, asserts `['Invite teacher']`. Mutation check: with Apply back to primary on Users and Audit, 3 tests fail. After restoring it they pass | `web/src/test/mintButtons.ts`, `audit/pages/AuditLogPage.test.tsx:75`, `payments/pages/PaymentLogPage.test.tsx:58`, `questions/pages/QuestionListPage.test.tsx:121`, `avatarConversations/pages/AvatarConversationsPage.test.tsx:54`, `questions/pages/ValidationQueuePage.test.tsx:107`, `users/pages/UsersPage.test.tsx:51,62-68` |
| 1 | Layout audit extended. `primary()` now scopes to the top-most open modal dialog, and a dialog counts as its own screen. Re-ran against the demo stack (ar, 375/768/1280) on the Users Teachers/Admins tabs, filtered-empty on all five admin lists and the teacher queue, the Payments review tab, the Invite dialog, the content tree fully expanded, Configuration, the teacher inbox tabs, Progress filtered by kind, Home and Subscription. `primary-count` is 0 everywhere. Rows F7–F12 and a new §6 are in `02-layout-audit.md` | `.process/…/layout-audit.js:151-154`, `.process/…/02-layout-audit.md` §3, §6 |
| 1 | Docs updated to the per-screen rule: tabs, filtered and empty states each count, and a dialog is its own screen. Apply, per-card actions, inline add/rename and the plan-line «اشترك» are outline. Clear filters is outline when the screen already has a mint | `.claude/design-system.md:116,122`, `docs/design-system.md:291`, `docs/claude-design-prompt.md:57` |
| 2 | Orchestrator's option (b): the gradient stays on `/student/progress`. Added "student progress summary" to the gradient token row, the Progress and HeroBanner rows, rule 3 and the `heroClassName` note in `.claude/design-system.md`. Also added it to §2.3 ("exactly four surfaces"), §5 Progress, §5.16 and §9 of `docs/design-system.md`, and to the `--hero` comment, the colour rule, Progress, the Hero banner paragraph, the `#/student/progress` route, the landing line and the "no gradients" rule of `docs/claude-design-prompt.md` | `.claude/design-system.md:35,96,108,124,221`, `docs/design-system.md:76,212,264,294`, `docs/claude-design-prompt.md:53,59,109,123,151,181,232` |
| 3 | Added `keeps the 14px button label size next to the button text colours` (`text-label` with `text-text` and with `text-accent-text`) and `lets the later shadow token win` (`shadow-1` vs `shadow-none`). Mutation check: removing `'label'` from the `text` list fails the first test, and emptying `shadow` fails the second. After restoring `utils.ts`, all 4 pass and `git diff` is identical to r1 | `web/src/shared/lib/utils.test.ts:9-16` |

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `web/src/test/mintButtons.ts` | 4 | test helper: visible mint (`bg-action`) buttons |

## Files modified
Listed in the table above. No other files were changed.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Plan files list did not include `NameForm`, `ContentPage`, `QuestionImportForm`, `PlanSummaryLine`, `UserListEmptyState`, the 6 page tests, or a new `src/test/mintButtons.ts` | The orchestrator asked for every screen state to have at most one primary, plus a test | Touched them, with minimal variant-only changes plus one optional prop |
| Audit `primary` counted the whole document | A modal over a page with a mint always counted 2 | Scoped the check to the open dialog, and documented "a modal dialog is its own screen" in both design-system files and the prompt |

## Build & test (from `web/`)
- `npm run typecheck`: exit 0.
- `npm run lint`: exit 0.
- `npx prettier --check . --end-of-line auto`: "All matched files use Prettier code style!" (plain `npm run format:check` reports CRLF on this Windows checkout, the same as before.)
- `npm test -- --run`: 306 files, 1817 tests passed, no timeouts.
- `npm run build`: ok, "Precompressed 298 files".
- `npm run perf:budget`: entry 189/210, landing 199/220, lesson 220/240, quiz 236/255, admin-dashboard 213/233, admin-users 245/270, teacher-home 245/265, assistant 244/260, all ok. `budgets.json` and `routeTree.gen.ts` are unchanged.
- Both CI literal greps from `web-ci.yml` found no matches (exit 1).

## Notes for review
- These were not driven in the browser: the question import after a clean check (it needs an .xlsx upload) and a Free-student Home (every seeded student is subscribed). I checked both by reading the code.
- The audit ran in Arabic only, because the `primary-count` result does not depend on language.
- Users Students tab now has no mint in the filtered-empty state ("at most one").
