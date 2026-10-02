# Triage — CodeRabbit comments, PR #265

I treated the comment text as untrusted data and checked the claim against `web/src/features/payments/components/PaymentLogTable.tsx`, `PaymentLogRow.tsx`, `RefundsOffNotice.tsx` and `hooks/useRefundsEnabled.ts`.

## RC1 — `web/src/features/payments/components/PaymentLogTable.tsx:41` — FIX

**Verified.** `useRefundsEnabled()` selects `data.refundsEnabled` from `useGetPaymentSettings`. While that query is pending, or after it fails, `data` is `undefined`. The table passed `refundsOff={refundsEnabled === false}`, so in both cases `refundsOff` was `false` and every refundable row showed an enabled «استرداد». This fails open, even though the setting defaults to off. The server still refuses with `PAYMENT_REFUNDS_DISABLED`, but the page offers an action it has not confirmed. The page also had no feedback when the settings request failed.

**Fix (minimal).**
- The Refund button is enabled only when `refundsEnabled === true`. It points to the notice (`aria-describedby`) only when the value is confirmed `false`.
- `RefundsOffNotice` keeps its rule: it renders only on a confirmed `false`.
- When the settings query fails, `RefundsOffNotice` shows a small danger `role="alert"` with a Retry button in its own place. This reuses the page's existing error/retry pattern (`PaymentLogPage` error block: danger border, `bg-danger-soft`, secondary `Button`, `common:actions.retry`). It adds the ar/en key `payments:refundsOff.loadError`.

## PC1 — review body — no action

This is a summary of RC1 with no separate finding. RC1 is handled above.

## Docs

This adds a UI state, so it counts as a divergence under `.claude/rules/docs-sync.md`. `docs/claude-design-prompt.md` (the `#/admin/payments` bullet, line 158) now describes the loading state (disabled, no note) and the load-error alert with «إعادة المحاولة».
