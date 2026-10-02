# [E18.S4] Payment safety switches: AllowFakePayments and refunds flag

Issue: #256

Epic: #252

As the owner I want payments to be safe while we launch. Fake payments can run only where they are explicitly allowed, and the refund flow stays off until we decide to open it.

Dev decisions:
- #189: "Agree" to an explicit `AllowFakePayments` switch.
- #193: "Hold the refund process for now. If you already implemented it, don't remove, just disable it via feature flag."

**Rules**
- **`AllowFakePayments`:**
  - It is infrastructure config, set by an environment variable, and defaults to false outside Development.
  - When it is false, the fake gateway refuses to start or serve.
  - The Configuration page shows it read-only; it is never editable from the UI.
- **Refunds feature flag:** a runtime setting, default **off**.
  - When it is off, the refund API returns a clear problem response.
  - The admin UI hides or disables the refund action and explains why.
  - Incoming refund webhooks from Paymob are still recorded, so money that moved at Paymob is never lost.
- **No refund code is removed.**

### Sub-tasks
- [ ] `AllowFakePayments` option and startup validation, plus docs: the deployment and Paymob docs and the env examples
- [ ] Refunds feature flag through the E18.S1 settings store: API guard and admin UI state
- [ ] Tests for both switches
- [ ] Docs: the PRD payments and refunds section, and the implementation report

