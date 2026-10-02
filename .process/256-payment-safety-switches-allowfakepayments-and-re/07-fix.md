# Fix — CodeRabbit triage, PR #265

| Item | What I changed | File:line |
|---|---|---|
| RC1 | The table passes the raw `refundsEnabled` (`boolean \| undefined`) instead of `refundsEnabled === false`. | `web/src/features/payments/components/PaymentLogTable.tsx:41` |
| RC1 | The row prop `refundsOff: boolean` is now `refundsEnabled: boolean \| undefined`. The button uses `disabled={refundsEnabled !== true}` and sets `aria-describedby` only when `refundsEnabled === false`. | `web/src/features/payments/components/PaymentLogRow.tsx:11,20,82-83` |
| RC1 | When the settings query fails, the notice slot shows a danger `role="alert"` with `payments:refundsOff.loadError` and a secondary `sm` Retry button (`common:actions.retry`, `refetch()`). This copies the `PaymentLogPage` error block. The "refunds off" note still appears only on a confirmed `false`. | `web/src/features/payments/components/RefundsOffNotice.tsx:9-29` |
| RC1 | New strings: `refundsOff.loadError` in en and ar. | `web/src/features/payments/i18n/en.json:79`, `ar.json:79` |
| RC1 tests | Added two tests: loading (button disabled, no note, no alert) and error (alert text, button disabled, no note, then Retry enables the button and clears the alert). I hardened the existing "refunds on" test with `waitFor(...toBeEnabled())` and a no-alert check. The server-refusal test now waits for the button to be enabled before it clicks. | `web/src/features/payments/pages/PaymentLogPage.refundsOff.test.tsx:45-56,70-73,90-129` |
| Docs | The `#/admin/payments` bullet now describes the unresolved state and the load-error state. | `docs/claude-design-prompt.md:158` |

PC1 is a summary only, so I took no action.

## Deviations
None. The prompt asked only for production and test changes. I also edited the docs line because `.claude/rules/docs-sync.md` treats a new UI state as a divergence.

## Build & test
- `npx vitest run src/features/payments --maxWorkers=2`: 7 files, 33 tests passed. The first run with default workers failed with "Timeout waiting for worker to respond" before any test ran. That was machine load, not the change.
- Mutation checks, each restored afterwards (`PaymentLogPage.refundsOff.test.tsx`):
  - `disabled={refundsEnabled === false}` (old fail-open): loading and error tests failed (2 failed, 4 passed).
  - `disabled={true}`: the refunds-on, server-refusal and error/retry tests failed (3 failed).
  - Error branch removed (`isError && false`): the error test failed (1 failed).
  - Notice shown unless confirmed `true`: the loading test failed (1 failed).
- `npm run typecheck` (`tsc -b`): clean.
- `npm run lint` (`eslint . --max-warnings=0`): clean.
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: "All matched files use Prettier code style!"
- `npm run build` followed by `npm run perf:budget`: all budgets ok (for example, entry 208/210 KB, admin-users 259/270 KB).

## Notes for review
- The load-error alert is in `RefundsOffNotice` and not in a new component, which keeps the change to existing files. The component name now covers both the "off" and "unknown" states.
- `PaymentLogPage.refundsOff.test.tsx` is now about 140 lines. That is within the range of the sibling `PaymentLogPage.test.tsx`.
- Nothing is committed.
