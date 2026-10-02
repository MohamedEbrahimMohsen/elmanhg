# Plan — Payment safety switches: AllowFakePayments and refunds flag (#256, E18.S4)

## Goal
The owner can run the fake payment gateway only where `Payments:AllowFakePayments` is explicitly on. The switch defaults to on in Development and off everywhere else, the API refuses to start with it on in Production, and the Configuration page shows it read-only. Admin refunds go behind a runtime feature flag `features.refundsEnabled`, which is off by default. While the flag is off, the refund API answers with a clear problem response and the payment log disables «استرداد» and says why. Signed Paymob refund callbacks are still recorded and applied. No refund code is removed.

## Precondition
Implement only after PR #263 (#253, runtime settings store) is merged and this branch is rebased on `main`. Every #253 path below (`Application/Shared/RuntimeSettings/**`, `Infrastructure/Hosting/InfrastructureConfigurationReader.cs`, `docs/configuration.md`, `Tests/Fixtures/RuntimeSettings/FakeRuntimeSettings.cs`, `Tests/Integration/Configuration/**`) assumes that merge. If `IRuntimeSettings` is missing after the rebase, stop with `BLOCKED: #263 not merged`.

## Scope
**In:**
- Change the `AllowFakePayments` default by environment.
- The fake gateway reads only the switch, with no environment-name check.
- A startup rule that rejects the switch being on in Production.
- Remove the key from `appsettings.example.json`.
- The `features.refundsEnabled` runtime setting.
- The refund API guard and the new error code.
- `GET /api/payments/settings`.
- The payment log's disabled state and notice.
- The test changes listed below.
- Docs and env examples.

**Out:**
- Any change to `ProcessPaymentNotificationHandler` or its Reversal partial. Reversals never read the flag.
- Removing refund code.
- A UI to edit `AllowFakePayments`.
- The demo compose branch (`chore/local-demo-compose`), which needs no change (D9).

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | How does the fake decide whether it may serve? | `FakePaymentGateway` gates only on `PaymentsOptions.AllowFakePayments`. `IHostEnvironment` is removed from it. | The rule says the switch replaces today's `IsProduction()` check. |
| D2 | How does "defaults to false outside Development" work for a `bool`? | Keep `bool AllowFakePayments`. `AddPayments` adds a `PostConfigure<IConfiguration, IHostEnvironment>`: when `configuration["Payments:AllowFakePayments"]` is null or blank, it sets `options.AllowFakePayments = hostEnvironment.IsDevelopment()`. An explicit value always wins, including `false` in Development. | The default needs to know whether the key is unset. The #253 `InfrastructureConfigurationReader` already reads `payments.AllowFakePayments` (bool), so it shows the effective value without change. |
| D3 | What does startup validation check? | `PaymentsOptionsValidator` gets `IHostEnvironment`. It fails when `AllowFakePayments && hostEnvironment.IsProduction()`. A fake that is not allowed does not fail the boot: it refuses to serve (503 checkout and refund, 404 fake completion). | Production keeps its documented guarantee that it never grants free plans, now as an explicit boot failure instead of a silent runtime override. Failing the boot on `Provider=Fake` with the switch off would take the whole site down on any host without Paymob keys yet (go-live keys come later). Refusing to serve meets the rule "refuses to start or serve". |
| D4 | `appsettings.example.json` has `"AllowFakePayments": false`, and the Dockerfile bakes it as `appsettings.json`. | Remove the key from the example file. | An explicit `false` in the baked file would disable the fake in every Development container, including the demo. When the key is absent, D2 applies. |
| D5 | Should the refunds flag default come from Options or a constant? | A constant `false`, with no new Options key. | The story says the flag is off by default, and no deployment value exists. One way to turn it on (the Configuration page, audited) is simpler. |
| D6 | Refund flag key, group and labels | `features.refundsEnabled`, group `Features`, type Boolean. Label: «السماح بالاسترداد من سجل المدفوعات» / "Allow refunds from the payment log". Description: «عند الإيقاف، لا يستطيع المدير استرداد أي دفع من التطبيق ويظهر زر الاسترداد معطّلًا. عمليات الاسترداد التي تتم من لوحة Paymob تُسجَّل دائمًا.» / "When off, admins cannot refund a payment from the app and the Refund button is disabled. Refunds made in the Paymob dashboard are always recorded." | This follows the existing `features.*` naming of #253. |
| D7 | Refund-disabled HTTP status and exception | `BusinessRuleViolationCoreException(ErrorCodes.PaymentRefundsDisabled)`, 400 `PAYMENT_REFUNDS_DISABLED`. | The other refund refusals (`PAYMENT_NOT_REFUNDABLE`, `PAYMENT_ALREADY_REFUNDED`) are 400 rule violations. A 403 would read as a missing permission. |
| D8 | Where in `RefundPaymentHandler.Handle` does the guard go? | Right after the authentication check, before the payment lookup and the idempotent replay. Validators (422) still run first through the pipeline. | Nothing touches Paymob or the database while refunds are off. A replay while the flag is off is also refused, which is acceptable because the money already moved and the row already shows Refunded. |
| D9 | Does the demo compose (`deploy/docker-compose.demo.yml` on `chore/local-demo-compose`) need `Payments__AllowFakePayments=true`? | No. It runs `ASPNETCORE_ENVIRONMENT: Development`, so D2 makes the switch true, and D4 removes the baked `false`. Note for later: (a) if the demo ever leaves Development, add `Payments__AllowFakePayments: "true"`; (b) to try a refund in the demo, an admin first turns on «السماح بالاسترداد من سجل المدفوعات» in Configuration. | Read with `git show origin/chore/local-demo-compose:deploy/docker-compose.demo.yml`. |
| D10 | How does the web know the flag? | New admin query `GET /api/payments/settings` returns `PaymentSettingsResult(bool RefundsEnabled)`, policy `Payments.Manage`. `AdminPaymentResult.CanRefund` keeps its meaning (the payment's state only). | `/api/configuration/settings` needs `Configuration.Manage`. A page-level flag avoids repeating the value on every row and keeps the three handlers that build `AdminPaymentResult` unchanged. |
| D11 | UI when the flag is off | A `role="note"` warning-soft card «الاسترداد متوقف» sits above the table, between the filters and the content. Every Refund button of a refundable row stays visible but `disabled`, with `aria-describedby` set to the notice. While the flag is unknown (loading or error), buttons stay enabled and the API is the guard. When the API answers `PAYMENT_REFUNDS_DISABLED`, the dialog shows it inline (existing `FormRootError`) and the settings query is invalidated, so the notice appears. | This follows the rule "disable and explain". It keeps `PaymentLogPage` under 120 lines by reading the flag in `PaymentLogTable` and `RefundsOffNotice`. |
| D12 | Integration tests share one DB, and the flag is global. | Every integration class that refunds through the API or depends on the flag joins `[Collection(RuntimeSettingsCollection.Name)]`, which is serial, and sets the flag explicitly through the real `PUT /api/configuration/settings/features.refundsEnabled`. | Otherwise parallel classes would race on the override row. |
| D13 | Morabh reuse | None. Morabh has no feature flags or payment switches (grep `FeatureFlag\|IsFeatureEnabled` gives no hits). Everything here is new, with no Morabh equivalent, and reuses the #253 settings store. | |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Infrastructure/Payments/PaymentsOptions.cs` | Add `public const string AllowFakePaymentsKey = "Payments:AllowFakePayments";`. Replace the comment above `AllowFakePayments` with `// Unset means true in Development and false elsewhere; the API refuses to start with it true in Production.` |
| `api/Elmanhg.Infrastructure/Payments/PaymentsServiceCollectionExtensions.cs` | Chain `.PostConfigure<IConfiguration, IHostEnvironment>(ApplyAllowFakePaymentsDefault)` after `.BindConfiguration(...)`. Add `private static void ApplyAllowFakePaymentsDefault(PaymentsOptions options, IConfiguration configuration, IHostEnvironment hostEnvironment)` with body `if (string.IsNullOrWhiteSpace(configuration[PaymentsOptions.AllowFakePaymentsKey])) { options.AllowFakePayments = hostEnvironment.IsDevelopment(); }`. Add usings `Microsoft.Extensions.Configuration` and `Microsoft.Extensions.Hosting`. |
| `api/Elmanhg.Infrastructure/Payments/PaymentsOptionsValidator.cs` | Change the signature to `public sealed class PaymentsOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<PaymentsOptions>`. After the timeout rule, add: `if (options.AllowFakePayments && hostEnvironment.IsProduction()) { failures.Add("Payments:AllowFakePayments must not be true in Production: the fake gateway grants plans without payment."); }`. DI registration is unchanged (singleton, resolved from the container). |
| `api/Elmanhg.Infrastructure/Payments/FakePaymentGateway.cs` | Change the signature to `public sealed class FakePaymentGateway(IOptions<PaymentsOptions> paymentsOptions) : IPaymentGateway`. Set `private bool IsAllowed => paymentsOptions.Value.AllowFakePayments;`. Keep `SupportsSimulatedCompletion => IsAllowed` and both `if (!IsAllowed) throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable);`. Remove the `Microsoft.Extensions.Hosting` using. |
| `api/Elmanhg.Infrastructure/Hosting/InfrastructureConfigurationReader.cs` (#253) | Delete `private const string AllowFakePaymentsKey`. Use `PaymentsOptions.AllowFakePaymentsKey` in the `SafetySwitchResult`. |
| `api/Elmanhg.Api/appsettings.example.json` | In `Payments`, remove `"AllowFakePayments": false, ` and leave everything else unchanged (D4). |
| `api/Elmanhg.Application/Shared/RuntimeSettings/Definitions/FeatureFlagRuntimeSettings.cs` (#253) | Add `public static readonly RuntimeSettingKey<bool> RefundsEnabled = new("features.refundsEnabled");`. Append a second definition after the exams one: `RuntimeSettingDefinition.ForBoolean(RefundsEnabled, RuntimeSettingGroup.Features, false, new LocalizedText("السماح بالاسترداد من سجل المدفوعات", "Allow refunds from the payment log"), new LocalizedText("<D6 Arabic>", "<D6 English>"))`, with the D6 texts verbatim. The constructor is unchanged. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | After `PaymentRefundDeclined`, add `public const string PaymentRefundsDisabled = "PAYMENT_REFUNDS_DISABLED";`. |
| `api/Elmanhg.Application/Payments/RefundPayment/RefundPaymentHandler.cs` | Append the constructor parameter `IRuntimeSettings runtimeSettings` at the end of the primary constructor. After the `UserNotAuthenticated` check, add `if (!await runtimeSettings.GetAsync(FeatureFlagRuntimeSettings.RefundsEnabled, cancellationToken).ConfigureAwait(false)) { throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentRefundsDisabled); }`. Nothing else changes. Usings: `Elmanhg.Application.Shared.RuntimeSettings` and `Elmanhg.Application.Shared.RuntimeSettings.Definitions`. |
| `api/Elmanhg.Api/Controllers/Payments/PaymentsController.cs` | Add the action below `GetPaymentLog`: `[HttpGet("settings", Name = "GetPaymentSettings")] [Authorize(Policy = DefaultCodes.PaymentsManage)] [ProducesResponseType<PaymentSettingsResult>(StatusCodes.Status200OK)] public async Task<ActionResult> GetPaymentSettings(CancellationToken cancellationToken) { var result = await mediator.Send(new GetPaymentSettingsQuery(), cancellationToken); return Ok(result); }`. Add using `Elmanhg.Application.Payments.GetPaymentSettings`. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx` | After `PAYMENT_REFUND_DECLINED`, add `<data name="PAYMENT_REFUNDS_DISABLED" xml:space="preserve"><value>الاسترداد متوقف حاليًا. يمكن للمدير تفعيله من صفحة الإعدادات.</value></data>`. |
| `api/Elmanhg.Api/Resources/Messages.en.resx` | Same position: `<value>Refunds are turned off. An admin can turn them on in Configuration.</value>`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. Do not hand-edit. |
| `web/src/shared/api/generated/**` | Regenerated with `npm run gen:api`: `payments/payments.ts`, `payments/payments.msw.ts`, `model/paymentSettingsResult.ts`, `model/index.ts`, `zod/payments/payments.zod.ts`. Do not hand-edit. |
| `web/src/shared/i18n/en.json` / `ar.json` | Under `errors`, after `PAYMENT_REFUND_DECLINED`, add `PAYMENT_REFUNDS_DISABLED` with the same en and ar strings as the resx. |
| `web/src/features/payments/i18n/en.json` | Add `"refundsOff": { "title": "Refunds are turned off", "body": "Refunds from this page are paused until an admin turns on \"Allow refunds from the payment log\" in Configuration. Refunds made in the Paymob dashboard are still recorded here." }` after `refund`. |
| `web/src/features/payments/i18n/ar.json` | `"refundsOff": { "title": "الاسترداد متوقف", "body": "الاسترداد من هذه الصفحة موقوف حتى يفعّل المدير «السماح بالاسترداد من سجل المدفوعات» من صفحة الإعدادات. عمليات الاسترداد التي تتم من لوحة Paymob تظهر هنا دائمًا." }`. |
| `web/src/features/payments/components/PaymentLogTable.tsx` | Call `const { data: refundsEnabled } = useRefundsEnabled();` and pass `refundsOff={refundsEnabled === false}` to each `PaymentLogRow`. |
| `web/src/features/payments/components/PaymentLogRow.tsx` | Add the prop `refundsOff: boolean` to `PaymentLogRowProps`. On the existing Refund `Button`, add `disabled={refundsOff}` and `aria-describedby={refundsOff ? refundsOffNoticeId : undefined}`, imported from `./RefundsOffNotice`. It is still rendered only when `item.canRefund`. |
| `web/src/features/payments/pages/PaymentLogPage.tsx` | Render `<RefundsOffNotice />` between `<PaymentLogFilters …/>` and `{renderContent()}`, plus its import. No other change. |
| `web/src/features/payments/hooks/useRefundPayment.ts` | Add `onError: (error) => { if (error instanceof ApiError && error.code === refundsDisabledCode) { void queryClient.invalidateQueries({ queryKey: getGetPaymentSettingsQueryKey() }); } }` to the mutation options. Add module const `const refundsDisabledCode = 'PAYMENT_REFUNDS_DISABLED';`. Import `ApiError` from `@/shared/lib/apiError` and `getGetPaymentSettingsQueryKey` from the generated payments module. |
| `web/src/test/paymentFixtures.ts` | Add `export function paymentSettings(overrides: Partial<PaymentSettingsResult> = {}): PaymentSettingsResult { return { refundsEnabled: true, ...overrides }; }` and its type import. |
| `web/src/test/msw/server.ts` | Register the default handler `getGetPaymentSettingsMockHandler(paymentSettings())`. Existing payment-log tests keep an enabled Refund button. |
| `deploy/api.env.example` | Line 39 comment becomes `# Payments (docs/paymob.md). The fake serves only when AllowFakePayments is true (unset: true in Development, false elsewhere); the API refuses to start with it true in Production.` Keep `Payments__AllowFakePayments=false`. |
| `.env.example` (root) | Line 37 becomes `# Payments__AllowFakePayments=false   # unset = true in Development (this file); never true in Production`. |
| `postman/elmanhg.postman_collection.json` | Before "Refund payment", insert "Get payment settings": GET `{{baseUrl}}/api/payments/settings`, tests status 200 and `typeof refundsEnabled === "boolean"`. Then insert "Turn on refunds": PUT `{{baseUrl}}/api/configuration/settings/features.refundsEnabled`, body `{ "value": true }`, test status 200. After the last refund item of that folder, add "Reset refunds flag": POST `{{baseUrl}}/api/configuration/settings/features.refundsEnabled/reset`, test status 200. Use the same JSON shape as the neighbouring items. |
| Docs | See Docs-sync below. |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Application/Payments/GetPaymentSettings/GetPaymentSettingsQuery.cs` | query | `namespace Elmanhg.Application.Payments.GetPaymentSettings;` `public sealed record GetPaymentSettingsQuery : IRequest<PaymentSettingsResult>;` |
| 2 | `api/Elmanhg.Application/Payments/GetPaymentSettings/PaymentSettingsResult.cs` | result (admin-facing, nothing localized) | `public sealed record PaymentSettingsResult(bool RefundsEnabled);` |
| 3 | `api/Elmanhg.Application/Payments/GetPaymentSettings/GetPaymentSettingsHandler.cs` | handler | `public sealed class GetPaymentSettingsHandler(IRuntimeSettings runtimeSettings) : IRequestHandler<GetPaymentSettingsQuery, PaymentSettingsResult>`. Handle: (1) `var refundsEnabled = await runtimeSettings.GetAsync(FeatureFlagRuntimeSettings.RefundsEnabled, cancellationToken).ConfigureAwait(false);` (2) `return new PaymentSettingsResult(refundsEnabled);`. No validator; MediatR registration is assembly-scanned. |
| 4 | `web/src/features/payments/hooks/useRefundsEnabled.ts` | hook | `export function useRefundsEnabled() { return useGetPaymentSettings({ query: { select: (data) => data.refundsEnabled } }); }` |
| 5 | `web/src/features/payments/components/RefundsOffNotice.tsx` | component | `export const refundsOffNoticeId = 'refunds-off-notice';` `export function RefundsOffNotice()`: reads `const { data: refundsEnabled } = useRefundsEnabled();` and returns `null` unless `refundsEnabled === false`. Otherwise it renders `<div id={refundsOffNoticeId} role="note" aria-labelledby={`${refundsOffNoticeId}-title`} className="flex flex-col gap-1 rounded-lg border border-border bg-warning-soft p-4">`, then `<p id={`${refundsOffNoticeId}-title`} className="text-ui font-semibold text-text">{t('refundsOff.title')}</p>`, then `<p className="text-caption text-text">{t('refundsOff.body')}</p>`. Uses `useTranslation('payments')`. |
| 6 | `api/Elmanhg.Tests/Application/Features/Payments/GetPaymentSettings/GetPaymentSettingsHandlerTests.cs` | unit tests | Uses `FakeRuntimeSettings`; tests U9–U10. |
| 7 | `api/Elmanhg.Tests/Application/Shared/RuntimeSettings/FeatureFlagRuntimeSettingsTests.cs` | unit tests | Test U11. |
| 8 | `api/Elmanhg.Tests/Integration/Payments/PaymentSettingsEndpointTests.cs` | integration tests | `[Collection(RuntimeSettingsCollection.Name)] public sealed class PaymentSettingsEndpointTests(ApiFactory factory) : IAsyncLifetime`. `InitializeAsync` and `DisposeAsync` both call `ConfigurationTestData.ClearOverridesAsync(factory)`. Tests I1–I5. |
| 9 | `web/src/features/payments/pages/PaymentLogPage.refundsOff.test.tsx` | web tests | Tests W1–W4. Same `openPayments` and `serveLog` pattern as `PaymentLogPage.actions.test.tsx`, defined locally. |

Test helper added to the existing `api/Elmanhg.Tests/Integration/Payments/PaymentsTestData.cs`: `public static async Task SetRefundsEnabledAsync(ApiFactory factory, bool enabled, CancellationToken cancellationToken)`. It gets `AdminClientAsync(factory, cancellationToken)`, sends PUT `/api/configuration/settings/features.refundsEnabled` with `JsonContent.Create(new { value = enabled })`, then calls `response.EnsureSuccessStatusCode()`. Add `public const string RefundsEnabledKey = "features.refundsEnabled";` too.

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ErrorCodes.PaymentRefundsDisabled` | `PAYMENT_REFUNDS_DISABLED` | `RefundPaymentHandler` | `BusinessRuleViolationCoreException` | 400 |

- ar: «الاسترداد متوقف حاليًا. يمكن للمدير تفعيله من صفحة الإعدادات.»
- en: "Refunds are turned off. An admin can turn them on in Configuration."

Startup failure (an `OptionsValidationException` message, not an error code): "Payments:AllowFakePayments must not be true in Production: the fake gateway grants plans without payment."

## Domain behaviour
None. No entity, migration or `DbContext` change. `Payment`, `PaymentRefundSettlement` and `ProcessPaymentNotificationHandler(.Reversal)` stay unchanged: a signed full reversal still calls `PaymentRefundSettlement.Apply` whatever the flag says.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| GET | `/api/payments/settings` (new, name `GetPaymentSettings`) | `DefaultCodes.PaymentsManage` | none | `PaymentSettingsResult { refundsEnabled }` |
| POST | `/api/payments/{paymentId}/refund` (changed) | `DefaultCodes.PaymentsManage` | unchanged | unchanged, plus 400 `PAYMENT_REFUNDS_DISABLED` while the flag is off |
| PUT / POST reset | `/api/configuration/settings/features.refundsEnabled[/reset]` (#253, no code change) | `Configuration.Manage` | `{ value: bool }` | `RuntimeSettingResult` |
| POST | `/api/payments/paymob/webhook` | unchanged | unchanged | unchanged; reversals applied even when the flag is off |

## Test plan
Existing test files the plan changes (the test-integrity guard allows exactly these edits):

| File | Allowed edit |
|---|---|
| `Tests/Infrastructure/Payments/FakePaymentGatewayTests.cs` | Replace the whole file: remove the `IHostEnvironment` substitute and all 12 environment-based tests. The behaviour they pinned (D1) is gone. Write U1–U6 instead. The helper becomes `private FakePaymentGateway Gateway(bool allowFakePayments)`, which builds `PaymentsTestSettings.Fake()` with the switch set and calls `new(Options.Create(options))`. |
| `Tests/Infrastructure/Payments/PaymentsOptionsValidatorTests.cs` | Replace the `_validator` field with `private readonly IHostEnvironment _hostEnvironment = Substitute.For<IHostEnvironment>();` and `private PaymentsOptionsValidator Validator => new(_hostEnvironment);`. Every existing `_validator.` becomes `Validator.`. Add U12–U14. |
| `Tests/Infrastructure/Payments/PaymentsServiceCollectionExtensionsTests.cs` | `BuildProvider` gets the parameter `string environmentName = "Testing"`. It registers `var hostEnvironment = Substitute.For<IHostEnvironment>(); hostEnvironment.EnvironmentName.Returns(environmentName); services.AddSingleton(hostEnvironment);`. Add U15–U19. |
| `Tests/Application/Features/Payments/RefundPayment/RefundPaymentHandlerTests.cs` | Add the field `private readonly FakeRuntimeSettings _runtimeSettings = new FakeRuntimeSettings().Set(FeatureFlagRuntimeSettings.RefundsEnabled, true);` and append `_runtimeSettings` to the handler constructor call. Add U7–U8. |
| `Tests/Application/Shared/RuntimeSettings/RuntimeSettingRegistryTests.cs` (#253) | Rename `Definitions_DefaultOptions_FourteenOrderedByGroup` to `Definitions_DefaultOptions_FifteenOrderedByGroup` and change `HaveCount(14)` to `HaveCount(15)`. |
| `Tests/Integration/Payments/RefundPaymentEndpointTests.cs` | Add `[Collection(RuntimeSettingsCollection.Name)]` and `: IAsyncLifetime`. `InitializeAsync` calls `PaymentsTestData.SetRefundsEnabledAsync(factory, true, CancellationToken)`. `DisposeAsync` calls `ConfigurationTestData.ClearOverridesAsync(factory)`. Add I6. |
| `Tests/Integration/Subscriptions/PaymentHistoryEndpointTests.cs` | Add `[Collection(RuntimeSettingsCollection.Name)]`. In `Get_StudentWithRefundedPayment_ListsRefundedStatus`, the first line becomes `await PaymentsTestData.SetRefundsEnabledAsync(factory, true, CancellationToken);`. |
| `Tests/Integration/Subscriptions/PaymobRefundWebhookEndpointTests.cs` | Add `[Collection(RuntimeSettingsCollection.Name)]`. Add I7. |
| `Tests/Integration/Payments/PaymentsTestData.cs` | Add the helper and constant above. |
| `web/src/test/msw/server.ts`, `web/src/test/paymentFixtures.ts` | As listed in "Existing code touched". |

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| U1 | FakePaymentGatewayTests | `StartCheckoutAsync_AllowFakePayments_ReturnsFakeCheckoutPathForPayment` | `RedirectUrl == "/student/fake-checkout/{paymentId}"` |
| U2 | FakePaymentGatewayTests | `StartCheckoutAsync_FakePaymentsNotAllowed_ThrowsPaymentGatewayUnavailable` | `ServiceUnavailableCoreException` with `ErrorCodes.PaymentGatewayUnavailable` |
| U3 | FakePaymentGatewayTests | `SupportsSimulatedCompletion_AllowFakePayments_IsTrue` | true |
| U4 | FakePaymentGatewayTests | `SupportsSimulatedCompletion_FakePaymentsNotAllowed_IsFalse` | false |
| U5 | FakePaymentGatewayTests | `RefundAsync_AllowFakePayments_ReturnsFakeRefundTransaction` | `TransactionId == $"fake-refund-{paymentId:N}"` |
| U6 | FakePaymentGatewayTests | `RefundAsync_FakePaymentsNotAllowed_ThrowsPaymentGatewayUnavailable` | `ServiceUnavailableCoreException` with `PaymentGatewayUnavailable` |
| U7 | RefundPaymentHandlerTests | `Handle_RefundsDisabled_ThrowsRefundsDisabledWithoutGatewayCall` | Set `RefundsEnabled` to false, use a succeeded payment. Asserts `BusinessRuleViolationCoreException` with `ErrorCodes.PaymentRefundsDisabled`, `RefundAsync` `DidNotReceive`, `SaveChangesAsync` `DidNotReceive`, and `payment.Status == Succeeded` |
| U8 | RefundPaymentHandlerTests | `Handle_RefundsDisabledWithUnknownPayment_ThrowsRefundsDisabledBeforeLookup` | The flag is off and the payment id is unknown. Asserts the error code is `PaymentRefundsDisabled` (not `PaymentNotFound`) and `SaveChangesAsync` `DidNotReceive` |
| U9 | GetPaymentSettingsHandlerTests | `Handle_FlagNeverSet_ReturnsRefundsDisabled` | `new FakeRuntimeSettings()`, so the result is `RefundsEnabled == false` |
| U10 | GetPaymentSettingsHandlerTests | `Handle_RefundsTurnedOn_ReturnsRefundsEnabled` | `.Set(RefundsEnabled, true)`, so the result is `RefundsEnabled == true` |
| U11 | FeatureFlagRuntimeSettingsTests | `Definitions_RefundsEnabled_IsBooleanFeatureFlagDefaultingOff` | The definition with key `features.refundsEnabled` has `Group == Features`, `Type == Boolean` and `DefaultValue.GetBoolean() == false` |
| U12 | PaymentsOptionsValidatorTests | `Validate_AllowFakePaymentsInProduction_FailsNamingKey` | Environment `Production` with `AllowFakePayments = true`: `Failures` contains `"Payments:AllowFakePayments must not be true in Production"` |
| U13 | PaymentsOptionsValidatorTests | `Validate_AllowFakePaymentsOutsideProduction_Succeeds` | `[Theory]` Development / Staging / Testing with the switch on: `Succeeded` |
| U14 | PaymentsOptionsValidatorTests | `Validate_FakePaymentsNotAllowedInProduction_Succeeds` | Production with the switch off: `Succeeded` (no boot failure, D3) |
| U15 | PaymentsServiceCollectionExtensionsTests | `AddPayments_AllowFakePaymentsUnsetInDevelopment_DefaultsToTrue` | `BuildProvider([], "Development")`, so `IOptions<PaymentsOptions>.Value.AllowFakePayments` is true |
| U16 | PaymentsServiceCollectionExtensionsTests | `AddPayments_AllowFakePaymentsUnsetOutsideDevelopment_DefaultsToFalse` | `[Theory]` Staging / Production / Testing: false |
| U17 | PaymentsServiceCollectionExtensionsTests | `AddPayments_AllowFakePaymentsFalseInDevelopment_KeepsFalse` | Config `Payments:AllowFakePayments=false` in Development: false, and the resolved `IPaymentGateway.SupportsSimulatedCompletion` is false |
| U18 | PaymentsServiceCollectionExtensionsTests | `AddPayments_AllowFakePaymentsTrueInProduction_FailsStartupValidation` | `IStartupValidator.Validate()` throws `OptionsValidationException` with message `*AllowFakePayments*` |
| U19 | PaymentsServiceCollectionExtensionsTests | `AddPayments_AllowFakePaymentsTrueInStaging_ResolvesServingFakeGateway` | The gateway is `FakePaymentGateway` with `SupportsSimulatedCompletion` true |
| I1 | PaymentSettingsEndpointTests | `Get_FlagNeverSet_ReturnsRefundsDisabled` | Admin request: 200 with `refundsEnabled` false |
| I2 | PaymentSettingsEndpointTests | `Get_AfterAdminTurnsRefundsOn_ReturnsRefundsEnabled` | After `SetRefundsEnabledAsync(true)`: 200 with `refundsEnabled` true |
| I3 | PaymentSettingsEndpointTests | `Get_Teacher_Returns403` | 403 |
| I4 | PaymentSettingsEndpointTests | `Get_Anonymous_Returns401` | 401 |
| I5 | PaymentSettingsEndpointTests | `GetConfigurationSettings_RefundsFlag_ListedUnderFeaturesAndOff` | `GET /api/configuration/settings`: the `Features` group has `features.refundsEnabled` with `value` false, `defaultValue` false and `isOverridden` false |
| I6 | RefundPaymentEndpointTests | `Post_RefundsOff_Returns400RefundsDisabledAndChangesNothing` | `SetRefundsEnabledAsync(false)`, then refund a succeeded payment. Asserts 400 with code `PAYMENT_REFUNDS_DISABLED`, and the DB (fresh scope) shows the payment still `Succeeded` with `RefundTransactionId` null and the subscription still `Active` |
| I7 | PaymobRefundWebhookEndpointTests | `Post_SignedFullRefundWhileRefundsOff_StillMarksRefunded` | `SetRefundsEnabledAsync(false)`, then a signed full refund payload. Asserts 200 with outcome `Refunded`, the DB payment `Refunded` with `RefundTransactionId == refundId`, and the student tier `Free` |
| W1 | PaymentLogPage refunds off | `disables the refund button and explains why when refunds are off` | `getGetPaymentSettingsMockHandler(paymentSettings({ refundsEnabled: false }))`. Asserts the note "Refunds are turned off" is visible, the row's Refund button `toBeDisabled()`, and `toHaveAccessibleDescription(/Refunds are turned off/)` |
| W2 | PaymentLogPage refunds off | `enables the refund button with no notice when refunds are on` | Default handler: Refund is enabled, and `queryByRole('note')` is null |
| W3 | PaymentLogPage refunds off | `shows the refunds-off error in the dialog and then the notice when the server refuses` | Settings start true. The refund POST flips a local flag to false and returns `400 { code: 'PAYMENT_REFUNDS_DISABLED' }`. Asserts the dialog `alert` has "Refunds are turned off. An admin can turn them on in Configuration." and the note then appears after the settings refetch |
| W4 | PaymentLogPage refunds off | `renders the refunds-off notice in Arabic` | `lng: 'ar'` with the flag off: the note contains «الاسترداد متوقف» and the Refund button («استرداد») is disabled |

## Docs-sync (same PR)
| Doc | Change |
|---|---|
| `docs/PRD.md` §10.6 | In "v1 settings", add "the payment-log refunds flag (feature flag, off by default)". |
| `docs/PRD.md` §11.2 | Append to the bullet that starts "Full transaction log…": "Refunds from the app sit behind the runtime feature flag `features.refundsEnabled`, off by default (dev decision #193). While it is off, the refund API answers 400 `PAYMENT_REFUNDS_DISABLED` and the payment log disables «استرداد» with an explanation. Signed Paymob refund callbacks are always recorded and applied." Add a bullet: "The fake payment gateway serves only where `Payments:AllowFakePayments` is true: unset means true in Development and false elsewhere, and the API refuses to start with it true in Production (#189). It is shown read-only on the Configuration page." |
| `docs/paymob.md` | §1 Fake row: "Development (the switch defaults on), tests, CI and the build-time OpenAPI run; any other non-Production host only with `AllowFakePayments=true`." §2 row `AllowFakePayments`: default "unset: `true` in Development, `false` elsewhere"; note "The fake serves only when true. Must not be true in Production (boot fails)." Remove "Ignored in Production". §3: add the bullet "`AllowFakePayments=true` with `ASPNETCORE_ENVIRONMENT=Production` fails the boot." §6 "Environment lock": rewrite as "the fake serves only when `Payments:AllowFakePayments` is true (unset: true in Development, false elsewhere; the test host sets it). Otherwise it refuses checkout (503 `PAYMENT_GATEWAY_UNAVAILABLE`), refunds (503) and fake completion (404 `FAKE_CHECKOUT_UNAVAILABLE`). Production can never turn it on: the API refuses to start. A local `appsettings.json` copied before #256 may still hold `"AllowFakePayments": false`; delete that key." §9: add "Admin refunds are behind the `features.refundsEnabled` flag, off by default (docs/subscriptions.md → Refunds). Signed reversal callbacks (section 8) are applied whatever the flag says." |
| `docs/subscriptions.md` | In the Checkout "Fake gateway" bullet, replace the sentence that starts "Outside Development the fake refuses…" with "The fake serves only when `Payments:AllowFakePayments` is true (unset: true in Development only; never true in Production, docs/paymob.md §6); otherwise it refuses checkout (503) and fake completion (404 `FAKE_CHECKOUT_UNAVAILABLE`); with the Paymob provider fake completion is always 404." Under Refunds, add a first bullet "**Feature flag.** The refund API works only while the runtime setting `features.refundsEnabled` is on (Configuration page, audited; off by default, dev decision #193). While off it returns 400 `PAYMENT_REFUNDS_DISABLED` before any lookup or Paymob call, and the payment log shows «الاسترداد متوقف» with every Refund button disabled. Signed reversal callbacks are recorded and applied regardless (see Webhook). An open review can still be closed with «إبقاء الدفع»." Under Admin payment log, add "`GET /api/payments/settings` (`Payments.Manage`) returns `{ refundsEnabled }` for the page." In the API table: add the GET row and add `PAYMENT_REFUNDS_DISABLED` to the refund row's 400 list. |
| `docs/configuration.md` | §2: add a row `features.refundsEnabled` · Features · Boolean · – · `false` (constant, no Options key). §4 step 2: add "A flag with no deployment value uses a constant default (for example `features.refundsEnabled`, `false`)." §6 Safety switches: "`Payments:AllowFakePayments`, its effective value (unset: true in Development, false elsewhere), shown on or off. Never editable from the UI or the API; the API refuses to start with it true in Production." |
| `docs/deployment.md` | §2 table row `Payments__AllowFakePayments`: dev "not needed (unset means true in Development)"; staging "`true` only while staging runs `Payments__Provider=Fake`"; production "leave `false`: the API refuses to start with `true`". §4 Payments table row: default "unset: `true` in Development, `false` elsewhere"; note "no; the fake serves only when true; refused at boot in Production". |
| `docs/claude-design-prompt.md` §4 (`#/admin/payments` line) | After the «استرداد» description, add: "While refunds are off (`features.refundsEnabled`), a warning-soft note «الاسترداد متوقف» above the table explains that an admin can turn them on in «الإعدادات» and that Paymob-dashboard refunds are still recorded. Each «استرداد» stays visible but disabled and points to the note. A refusal from the server shows inline in the dialog." |
| `docs/implementation-report.md` | §4 Paymob row, Fake cell: replace "Works in Development; elsewhere only with `Payments__AllowFakePayments=true`; Production always refuses it." with "Serves only when `Payments__AllowFakePayments` is true (unset: true in Development, false elsewhere); the API refuses to start with it true in Production." Add to the Real-adapter cell "admin refunds also need `features.refundsEnabled` on (Configuration page)". §5 Config row: append "If your local `appsettings.json` still has `Payments:AllowFakePayments: false`, delete the key, or fake checkout returns 503." §6 "Given by the dev": add the row `2026-10-01` "#189: an explicit `Payments:AllowFakePayments` switch. #193: hold refunds and keep the code behind the `features.refundsEnabled` flag, off by default (#256)." §7, #193 row: append "Admin refunds are off by default (`features.refundsEnabled`) until this is resolved." |

## Definition of done
- [ ] `FakePaymentGateway` has no `IHostEnvironment` dependency and serves only when `AllowFakePayments` is true.
- [ ] `AllowFakePayments` unset resolves to `IsDevelopment()`, and an explicit value wins (U15–U17).
- [ ] The API fails startup with `AllowFakePayments=true` in Production (U12, U18). Provider Fake with the switch off does not fail the boot (U14).
- [ ] `appsettings.example.json` no longer contains `AllowFakePayments`. The env examples and docs describe the new default and the boot rule.
- [ ] The Configuration page infrastructure card shows the effective value, unchanged from #253, through `PaymentsOptions.AllowFakePaymentsKey`.
- [ ] `features.refundsEnabled` is registered in `FeatureFlagRuntimeSettings`: Boolean, Features group, default false, ar and en label and description (U11, I5).
- [ ] With the flag off, `POST /api/payments/{id}/refund` answers 400 `PAYMENT_REFUNDS_DISABLED` with ar and en messages, before any lookup or gateway call (U7, U8, I6).
- [ ] With the flag off, a signed Paymob full reversal is still applied with outcome `Refunded` (I7). `ProcessPaymentNotificationHandler*` is not modified.
- [ ] `GET /api/payments/settings` exists with `Payments.Manage` and returns `{ refundsEnabled }` (I1–I4). OpenAPI and the Orval output are regenerated, not hand-edited.
- [ ] The payment log shows the refunds-off note and disables every Refund button, which points to the note through `aria-describedby`. A server refusal shows inline and refreshes the note (W1–W4).
- [ ] No refund code is deleted: `RefundPaymentHandler`, `PaymobPaymentGateway.Refund`, `RefundPaymentDialog` and `useRefundPayment` still exist and work with the flag on (the existing tests pass unchanged apart from the listed fixture edits).
- [ ] The only edits to existing tests are the ones in the "Existing test files" table.
- [ ] `PaymentLogPage.tsx` stays at 120 lines or fewer. No new file is over the size limits.
- [ ] Docs-sync rows are all applied: PRD §10.6 and §11.2, paymob.md, subscriptions.md, configuration.md, deployment.md, claude-design-prompt.md, implementation-report.md.
- [ ] The demo compose needs no change (D9). Recorded for later in the PR description.
