VERDICT: APPROVED

# Review — Payment safety switches: AllowFakePayments and refunds flag (#256, E18.S4)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Application/Shared/RuntimeSettings/RuntimeSettingValuesTests.cs:70` — an edit to an existing test that the plan's allowed-edit table did not list. It is disclosed in Deviations, it is the test's intended upkeep for a new key, and it does not weaken the test. Accepted.
- `api/Elmanhg.Tests/Integration/Subscriptions/PaymentHistoryEndpointTests.cs:65` — this test turns the flag on and never clears it. That is harmless, because every other class in the serial `RuntimeSettings` collection clears overrides in `InitializeAsync`. It would be tidier to clear it in this test too.
- `api/Elmanhg.Tests/Integration/Subscriptions/PaymobRefundWebhookEndpointTests.cs:44` (I7) is a characterization test. No current code path can break it, because the webhook handler never reads the flag. It is still worth keeping as a regression pin.
- Full `npx vitest run` under load failed 2/1557 on the first run and 6/1557 on the second. Each run failed a different set of tests, all timeouts in ConfigurationPage, the drag-drop and essay pages. None of those files is touched by this diff, and all 14 of them pass in isolation (81/81). This is pre-existing flakiness, not a regression.

## Verified
- Fake payments are impossible in production:
  - `PaymentsOptionsValidator.cs:17` rejects `AllowFakePayments && IsProduction()`. It runs through `ValidateOnStart`, which is pinned by U18 through `IStartupValidator`.
  - `FakePaymentGateway.cs:12` gates only on the switch, so a Fake provider with the switch off refuses checkout and refunds with 503 and fake completion with 404 (`CompleteFakePaymentHandler` checks `SupportsSimulatedCompletion`).
- Where the switch resolves to true. The default `PaymentsServiceCollectionExtensions.cs:37-43` is `IsDevelopment()` only when the key is null or blank, and an explicit value always wins (U15–U17). It is true only in:
  - Development with the key unset (launchSettings, and the demo compose per D9);
  - the test host (`ApiFactory.cs:197` sets `true` in Testing);
  - an explicit `true` on a non-Production host.
- Every deployed and baked config keeps the switch off:
  - The Dockerfile bakes `appsettings.example.json`, which no longer has the key. The aspnet image with no `ASPNETCORE_ENVIRONMENT` is Production, so the switch resolves to false.
  - `deploy/api.env.example:41` keeps an explicit `false`, and `deploy/.env.example` sets Production.
  - `load-test.sh` runs Staging with the explicit `false`, which is unchanged behaviour.
  - Root `.env.example` has the key only as a comment.
- Refunds flag:
  - `features.refundsEnabled` is a Boolean in the Features group, defaults to `false`, and uses the D6 texts verbatim (`FeatureFlagRuntimeSettings.cs:10,15`).
  - The guard sits right after the auth check and before the lookup and replay (`RefundPaymentHandler.cs:24-27`). It throws `BusinessRuleViolationCoreException(PAYMENT_REFUNDS_DISABLED)`, which returns 400, with ar and en entries in both resx files and both web `errors` locales.
- `GET /api/payments/settings` uses policy `PaymentsManage` and returns only `{ refundsEnabled }` (I3 403, I4 401). The handler is `sealed` and uses `.ConfigureAwait(false)`.
- Refund webhook path untouched: no change to `ProcessPaymentNotificationHandler*`. I7 confirms a signed full reversal still gives `Refunded` with the flag off.
- No refund code removed. `RefundPaymentHandler`, the Paymob refund, `RefundPaymentDialog` and `useRefundPayment` are all intact, and the existing refund tests pass with the flag on.
- Web:
  - `RefundsOffNotice` uses tokens only (`bg-warning-soft`, `text-ui`, `text-caption`, `border-border`, `rounded-lg`).
  - Each row's button is `disabled` with `aria-describedby` pointing to the notice. A loading or unknown flag leaves the buttons enabled (`=== false`).
  - On `PAYMENT_REFUNDS_DISABLED`, `onError` invalidates the settings query.
  - `PaymentLogPage.tsx` is 117 lines.
- Generated files in sync:
  - `api/openapi/v1.json` adds the path and the `PaymentSettingsResult` schema.
  - The Orval output (payments.ts, msw, zod, model, index) matches it.
  - Postman adds "Get payment settings" before "Refund payment" and "Turn on refunds" (PUT), and adds "Reset refunds flag" (POST `/reset`) after the refund items. Both routes exist in `ConfigurationController.cs:27,36`.
- Docs-sync: these docs all agree with the code:
  - PRD §10.6 and §11.2;
  - paymob.md §1, §2, §3, §6 and §9;
  - subscriptions.md (Checkout, Refunds, Admin payment log and the API table);
  - configuration.md §2, §3, §4 and §6;
  - deployment.md §2 and §4;
  - claude-design-prompt.md §4;
  - implementation-report.md §4, §5, §6 and §7.

  No stale "Production always refuses" or "ignored in Production" wording remains.
- CI-parity runs by the reviewer:
  - `dotnet test api/ -c Release` with no `appsettings.json`: 4839/4839 passed.
  - `npm run typecheck`: clean.
  - `npm run lint`: clean.
  - Vitest payments and the affected folders in isolation: 81/81 passed.

## Test quality
- FakePaymentGatewayTests (U1–U6) cover both values of the switch for each member, so they constrain `IsAllowed`.
- PaymentsOptionsValidatorTests (U12–U14) catch the IsProduction-to-IsStaging mutant and the dropped `AllowFakePayments &&` mutant.
- PaymentsServiceCollectionExtensionsTests (U15–U19) pin the env default and the rule that an explicit value wins, through real DI and the real startup validator.
- RefundPaymentHandlerTests U7 and U8 assert the code, `DidNotReceive` on the gateway and on `SaveChangesAsync`, and that the guard runs before the lookup.
- GetPaymentSettingsHandlerTests U9 and U10 use a real `FakeRuntimeSettings`, not a substitute echo.
- The integration tests I1–I6 assert DB state in a fresh scope.
- W1–W4 assert disabled state, accessible description, notice absence and the refetch after a server refusal.
- No vacuous tests found.
