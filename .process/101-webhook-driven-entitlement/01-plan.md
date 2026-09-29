# Plan — [E10.S3] Webhook-driven entitlement (#101)

## Goal
After this ships, a Paymob transaction callback signed with our HMAC secret is the thing that changes entitlement. A verified success starts a subscription, or extends the plan the student already holds. A verified failure marks the payment Failed. A duplicate or out-of-order callback changes nothing. From 7 days before the period ends, the student can renew a plan by paying again: the new period continues from the old end, and the student can switch period (monthly, termly, yearly). The student can cancel a plan, and it stays usable until the paid period ends. A background sweep moves Active subscriptions past their end to PastDue, and anything past its end plus the 3-day grace to Expired, which is the downgrade to Free. The fake gateway settles through the same routine as the webhook.

## Scope
**In (story sub-tasks → where):**
- Webhook endpoint with HMAC verification and raw payload storage → `PaymobWebhooksController`, `PaymobNotificationReader`, `PaymobHmac`, `ProcessPaymentNotification*`, `Payment.RawWebhook`.
- Idempotent processing keyed by Paymob transaction id → handler step 3, `IX_Payments_PaymobTransactionId` mapped to 409, and `xmin` on Payment and Subscription.
- State transitions on success, failure and cancellation → `PaymentSettlement.Succeed/Fail`, `Payment.MarkSucceeded` (now allowed from Failed), the student cancel `CancelSubscription*` and web cancel dialog.
- Renewal with a 3-day grace period, then the downgrade job → renewal window in checkout, `Subscription.Renew` semantics, `Subscription.Lapse`, `SubscriptionLapseSpecification`, `GetLapsedSubscriptionIds`, `LapseSubscription`, `SubscriptionLapseWorker`.
- Integration tests with recorded webhook payloads → `Tests/Fixtures/Paymob/*.json`, `PaymobWebhookEndpointTests`.
- Issue #187 items for #101:
  - Audit exclusion of `RawWebhook`.
  - Renew semantics.
  - The policy when a verified success arrives for a payment the student is no longer eligible for.
- Payment snapshots its period length (`PeriodMonths`), so settlement never reads configuration.
- Paymob order binding (`ProviderOrderId`).
- Docs: `docs/subscriptions.md`, `docs/paymob.md`, `docs/audit-log.md`, `docs/PRD.md` §11.2, `docs/claude-design-prompt.md` §4. Plus Postman, OpenAPI, Orval, `appsettings.example.json`, `.env.example`.

**Out:**
- Refund and void callbacks: acknowledged and ignored. #102 covers refunds.
- The admin view of flagged payments. #102 builds the admin payments page.
- Automatic recurring charges (Paymob card tokens or subscriptions). v1 renews by paying again.
- Rate limiting of the webhook. #115 covers security hardening.
- The #187 UI nits (the AskTeacher line on the Free result, `SubscribeHeader` contrast) and the #189 `useCheckoutResult` invalidation.
- Paymob's browser redirect (GET response callback) is never used to settle.

**Deferred (the orchestrator opens one issue):**
- Live verification against a Paymob test account. There are no credentials here. Check:
  - the exact HMAC field formatting (null values, boolean case, the `created_at` string);
  - that the intention's `special_reference` comes back as `obj.order.merchant_order_id`;
  - that the intention response carries `intention_order_id`;
  - the refund and void callback shape.

  The real reader is built and tested against the documented payload shape. Only the live check waits for credentials.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | #187-1: `RawWebhook` has billing PII and `Payment` is `IAuditedEntity` | Store the full raw body in `Payments.RawWebhook` (jsonb). Exclude it from audit diffs with a new EF property annotation `Core:AuditExcluded`, which `AuditChangeReader` skips. `AppDbContext` marks `Payment.RawWebhook` with it. | The raw body is the dispute evidence. The audit diff is widely readable (admin audit page), so PII must never reach it. `Core` has no exclusion mechanism and Morabh has none either (checked). One annotation constant is the smallest addition. |
| 2 | #187-2: renewal semantics | `Renew(period, periodMonths, reference, renewedAt, grace)`:<br>• allowed only while the subscription is entitled at `renewedAt` (Active or PastDue before end + grace, Cancelled before end); otherwise `SUBSCRIPTION_ENDED`<br>• the new period starts at the old `CurrentPeriodEnd`, even inside grace, so grace days are paid days<br>• `Period` switches to the paid period<br>• Cancelled resumes: `Status` becomes Active and `CancelledAt` is cleared | Continuity: no gap and no double-counted days. After a real lapse there is nothing to renew, so settlement starts a fresh subscription from the payment time. This removes the "extend from the old end after a long lapse" problem. |
| 3 | #187-3: a verified success for a payment the student is "no longer eligible" for | **Extend.** If the student holds an entitled subscription of the paid plan, settlement renews it (decision 2). Otherwise it starts a new one. If the plan is AskTeacher and the student has no entitled Base, the payment still settles (a subscription is started or renewed) and is flagged `ReviewReason = AskTeacherWithoutBase` for an admin refund decision in #102. | The money was captured, so the student must get the value. Extending turns a double payment into more paid time. It needs no refund machinery (PRD §11.2: refunds are admin-initiated, #102) and it is the same code path as a renewal. Only the add-on without Base has no value right now, so it is flagged instead of silently wasted. |
| 4 | Fake completion vs webhook | Fake completion calls the same `PaymentSettlement.Succeed/Fail`. The #100 re-check that returned 400 `CHECKOUT_PLAN_ALREADY_ACTIVE` and marked the payment Failed is removed. It keeps its own guard: only a Pending payment (`PAYMENT_NOT_PENDING`). | The fake must simulate the webhook exactly (`docs/paymob.md` §6 says the webhook decides). |
| 5 | How does a student renew in v1? | There are no automatic charges. From `CurrentPeriodEnd − RenewalWindowDays` (default 7, config), checkout accepts the plan the student holds (any entitled status). The web shows Renew buttons when `SubscriptionResult.CanRenew`. | Paymob Intention checkouts are one-off. The PRD's "failed renewal, then 3-day grace" becomes "not paid by period end": PastDue for 3 days, then Expired. |
| 6 | HMAC fields | HMAC-SHA512, key = `Payments:Paymob:HmacSecret` (UTF-8), message = the concatenation (no separator) of these `obj` values in this exact order:<br>`amount_cents, created_at, currency, error_occured, has_parent_transaction, id, integration_id, is_3d_secure, is_auth, is_capture, is_refunded, is_standalone_payment, is_voided, order.id, owner, pending, source_data.pan, source_data.sub_type, source_data.type, success`.<br>Value text:<br>• JSON string → its value<br>• `true`/`false` → `"true"`/`"false"`<br>• number → its raw JSON text<br>• null, missing, object or array → `""`<br>Digest in lower-case hex. Compare with `CryptographicOperations.FixedTimeEquals` over the UTF-8 bytes of the lower-cased provided value. **Uncertain:** the null formatting and the created_at text are unverified (Deferred). | This is Paymob's documented transaction-callback HMAC. There was no Morabh code to reuse (searched `Morabh/repos/apis` for Paymob, HMAC and webhook: only `HmacOtpHasher`, which is not applicable). |
| 7 | Where is the signature? | The `hmac` query parameter. If it is absent, the top-level `hmac` string in the body. | Paymob posts `?hmac=`. The body fallback is cheap insurance. |
| 8 | Fail closed | An empty `HmacSecret` means every notification is 401. `HmacSecret` is required at startup when `Provider=Paymob`. | A misconfigured server must never accept unsigned entitlement changes. |
| 9 | Non-transaction and non-settling callbacks | `type != "TRANSACTION"` → 200 `Ignored`, with no verification and no state change. `pending=true`, or any of `is_refunded`, `is_voided`, `is_refund`, `is_void`, `has_parent_transaction` is true → 200 `Ignored`. | Token callbacks use another HMAC field set. Refunds are #102. Pending is not final. |
| 10 | Matching the payment | `obj.order.merchant_order_id` parsed as Guid → `Payment.Id`. If it is missing or does not parse, match `Payment.ProviderOrderId == obj.order.id`. No match → 404 `PAYMENT_NOT_FOUND`, so Paymob retries and nothing is lost to a race. | `merchant_order_id` is **not** in the HMAC field set. The signed checks below stop a replayed signed body pointing at another payment. |
| 11 | Binding a notification to a payment | Amount and currency must equal the payment snapshot. When `Payment.ProviderOrderId` is set, it must equal `obj.order.id` (signed). Otherwise 400 `PAYMENT_NOTIFICATION_MISMATCH`: the audit row records it and nothing changes. | Closes the unsigned `merchant_order_id` hole. |
| 12 | `ProviderOrderId` source | Read `intention_order_id` (number or string) from the Intention response and store it on the Payment before its first save. The Fake returns null. | This is the signed order id the webhook carries. |
| 13 | Idempotency | Step 1: a payment already carrying this `PaymobTransactionId` → 200 `Duplicate`, no change. Concurrent deliveries: `xmin` on Payment and Subscription means the loser gets 409 (`PAYMENT_MODIFIED_CONCURRENTLY` / `SUBSCRIPTION_MODIFIED_CONCURRENTLY`), Paymob retries, and the retry is a Duplicate. A unique-index hit on the transaction id → 409 `PAYMENT_TRANSACTION_ALREADY_RECORDED`. | The key is the Paymob transaction id, as the story requires. The single `SaveChanges` is atomic. |
| 14 | Out-of-order and multi-attempt | One Paymob order can carry several transactions (declined, then approved).<br>• A success always wins: `MarkSucceeded` is allowed from Pending **or** Failed.<br>• A failure only applies to a Pending payment; otherwise 200 `OutOfOrder`.<br>• A success for an already-Succeeded payment with a new transaction id → 200 `OutOfOrder`, no change. | Never lose captured money to an earlier decline's late callback. |
| 15 | Settlement times | `CompletedAt` and a new subscription's start = server receipt time (`TimeProvider`), not the Paymob `created_at`. | A delayed callback must not shorten paid time. |
| 16 | Period length at settlement | `Payment.PeriodMonths` is snapshotted at checkout. Settlement never reads `PriceFor`. The migration backfills Monthly 1, Termly 4, Yearly 12. | A config change must never make a paid webhook unsettleable. This follows the "amount snapshot" rule. |
| 17 | Student cancel | `POST /api/subscriptions/{id}/cancel`, owner-scoped, only while entitled (Active or PastDue within end + grace). Otherwise `SUBSCRIPTION_ENDED`. It keeps paid time (`EntitledUntil = CurrentPeriodEnd`) and does not cascade to AskTeacher (the read-time "requires Base" rule already covers that). It returns the fresh `EntitlementResult`. | `docs/subscriptions.md` "For later stories" assigns the student cancel to #101, and the design prompt lists "cancel subscription" as a Danger button. |
| 18 | Sweep | `SubscriptionLapseWorker` mirrors `ExpiredExamSubmissionWorker`: `PeriodicTimer` on `TimeProvider`; a scope per call; `catch … when (!stoppingToken.IsCancellationRequested)`; failed ids deferred until a batch comes back short (the #81 starvation fix). Transitions (`Subscription.Lapse`):<br>• Active, end ≤ now < end + grace → PastDue<br>• Active or PastDue, end + grace ≤ now → Expired with `ExpiredAt = end + grace`<br>• Cancelled, end ≤ now → Expired with `ExpiredAt = end`<br>Defaults: `LapseSweepEnabled` true, interval 300 s, batch 100. | Entitlement is already read-time. The sweep is status bookkeeping for the UI and dashboards. `ExpiredAt` is the true lapse time, even when the sweep runs late. |
| 19 | Auditing | `ProcessPaymentNotification` (`Payment.ProcessNotification`, system actor), `CancelSubscription` (`Subscription.Cancel`) and `LapseSubscription` (`Subscription.Lapse`, system actor) are `IAuditableCommand`. Invalid-signature attempts leave a Failure row. | Entitlement changes are traceable. Failed signatures are a security signal. |
| 20 | Anonymous endpoint | The webhook is `[AllowAnonymous]` (the HMAC is its authentication), with `[RequestSizeLimit(65536)]`, and `[ApiExplorerSettings(IgnoreApi = true)]`, so it is not in OpenAPI or the web client. | Server-to-server only. It still passes `EndpointAuthorizationTests` (`IAllowAnonymous`). |
| 21 | Return URL | No change. `/student/checkout-result/{paymentId}` ignores Paymob's query string (it includes `success` and `hmac`) and only polls the server. This is documented in `docs/paymob.md`. | PRD §17 rule 12. |
| 22 | Grace wording | `SubscriptionResult` gains `InGracePeriod` (Active or PastDue and `CurrentPeriodEnd ≤ now`, computed on the server) and `CanRenew`. The web shows "period ended, available until {entitledUntil}" for Active in grace. | Server-computed flags keep the web tests clock-independent. |
| 23 | Known limit (documented, not built) | Two **different** payments for the same plan that settle within the same few milliseconds can start two overlapping subscriptions. | Needs two card forms completed at the same instant. Admin review in #102. |
| 24 | Logging | The handlers get no logger (the repo has none in Application). Outcomes are in the 200 body (`outcome`), and anomalies are 4xx with audit rows. | Mirrors the existing Application layer. |

## Existing code touched
| File | Change |
|------|--------|
| `api/core-libraries/Core.EntityFrameworkCore/Auditing/AuditChangeReader.cs` | Add `public const string ExcludedAnnotation = "Core:AuditExcluded";`. `IsAudited` returns false when `property.Metadata.FindAnnotation(ExcludedAnnotation)?.Value is true`. |
| `api/Elmanhg.Domain/Subscriptions/Subscription.cs` | Add `public uint Version { get; private set; }`. Add `IsRenewableAt`. See Domain behaviour. |
| `api/Elmanhg.Domain/Subscriptions/Subscription.Lifecycle.cs` | New `Renew` signature and body. Add `Lapse`. See Domain behaviour. |
| `api/Elmanhg.Domain/Subscriptions/Payment.cs` | Add `PeriodMonths`, `ProviderOrderId`, `ReviewReason`, `Version`. New `Create` signature. `MarkSucceeded` accepts Failed. Add `LinkProviderOrder` and `FlagForReview`. |
| `api/Elmanhg.Domain/Subscriptions/StudentEntitlement.cs` | `PurchaseConflict(plan, now, renewalWindow)` and `EnsureCanPurchase(plan, now, renewalWindow)` gain the renewal window. Add `Held(plan)`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add 8 constants (see Error codes) under the `// SUBSCRIPTIONS` group, after `FakeCheckoutUnavailable`. |
| `api/Elmanhg.Application/Shared/Options/SubscriptionsOptions.cs` | Add `RenewalWindowDays`, `LapseSweepEnabled`, `LapseSweepIntervalSeconds`, `LapseSweepBatchSize` and `RenewalWindow`. |
| `api/Elmanhg.Application/Shared/Payments/PaymentCheckout.cs` | `public sealed record PaymentCheckout(string RedirectUrl, string? ProviderOrderId = null);` |
| `api/Elmanhg.Application/Subscriptions/Shared/PaymentSettlement.cs` | Replace `Settle` with `Succeed` and `Fail` (see Files contract #S1). |
| `api/Elmanhg.Application/Subscriptions/Shared/SubscriptionResult.cs` | Append `bool InGracePeriod, bool CanRenew`. |
| `api/Elmanhg.Application/Subscriptions/Shared/EntitlementResultGenerator.cs` | `Generate(StudentEntitlement entitlement, SubscriptionsOptions options, DateTimeOffset now)`. `Map` sets `InGracePeriod = x.Status is Active or PastDue && x.CurrentPeriodEnd <= now` and `CanRenew = entitlement.PurchaseConflict(x.Plan, now, options.RenewalWindow) is null`. |
| `api/Elmanhg.Application/Subscriptions/Shared/StudentEntitlementLoader.cs` | Pass `now` to `Generate`. |
| `api/Elmanhg.Application/Subscriptions/StartCheckout/StartCheckoutHandler.cs` | `EnsureCanPurchase(plan, now, options.RenewalWindow)`. `Payment.Create(studentId, plan, period, price.Months, new Money(...))`. After the gateway call: `if (checkout.ProviderOrderId is not null) payment.LinkProviderOrder(checkout.ProviderOrderId);`, before `AddAsync`. |
| `api/Elmanhg.Application/Subscriptions/CompleteFakePayment/CompleteFakePaymentHandler.cs` | Rewrite `Handle` (see Files contract #S2). |
| `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobOptions.cs` | Add `public string HmacSecret { get; set; } = string.Empty;`. |
| `api/Elmanhg.Infrastructure/Payments/PaymentsOptionsValidator.cs` | Add `("HmacSecret", paymob.HmacSecret)` to the `required` list. |
| `api/Elmanhg.Infrastructure/Payments/PaymentsServiceCollectionExtensions.cs` | `services.AddSingleton<IPaymentNotificationReader, PaymobNotificationReader>();` (always, whatever the provider). |
| `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobIntentionResponse.cs` | Add `[property: JsonPropertyName("intention_order_id")] JsonElement? IntentionOrderId`. |
| `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobPaymentGateway.cs` | Return `new PaymentCheckout(url, ProviderOrderIdFrom(intention))`. `ReadClientSecretAsync` becomes `ReadIntentionAsync` and returns the whole response. The static helper returns `GetRawText()` for a Number, `GetString()` for a non-blank String, otherwise null. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | See the AppDbContext row below. |
| `api/Elmanhg.Api/Controllers/Subscriptions/SubscriptionsController.cs` | Add the `CancelSubscription` action (API surface). |
| `api/Elmanhg.Api/Program.cs` | `builder.Services.AddHostedService<SubscriptionLapseWorker>();` after the exam worker. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | 8 keys (Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | `Subscriptions`: `"RenewalWindowDays": 7, "LapseSweepEnabled": true, "LapseSweepIntervalSeconds": 300, "LapseSweepBatchSize": 100`. `Payments.Paymob`: `"HmacSecret": ""`. |
| `.env.example` | `# Payments__Paymob__HmacSecret=` under the Paymob block. |
| `api/openapi/v1.json` | Regenerated (the cancel endpoint and the `SubscriptionResult` fields; the webhook is hidden). |
| `api/Elmanhg.Tests/Elmanhg.Tests.csproj` | `<ItemGroup><None Include="Fixtures\Paymob\*.json" CopyToOutputDirectory="PreserveNewest" /></ItemGroup>` |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `public const string TestPaymobHmacSecret = "elmanhg-tests-paymob-hmac";`, `builder.UseSetting("Subscriptions:LapseSweepEnabled", "false");` (next to the Exams one) and in-memory keys:<br>`Subscriptions:RenewalWindowDays=7`<br>`Subscriptions:LapseSweepIntervalSeconds=300`<br>`Subscriptions:LapseSweepBatchSize=100`<br>`Payments:Paymob:HmacSecret=TestPaymobHmacSecret` |
| `api/Elmanhg.Tests/Integration/Subscriptions/SubscriptionTestData.cs` | Every `Payment.Create` passes months. Add `public static int MonthsFor(BillingPeriod period) => period switch { BillingPeriod.Monthly => 1, BillingPeriod.Termly => 4, _ => 12 };` and `public const string WebhookRoute = "/api/payments/paymob/webhook";`. |
| `api/Elmanhg.Tests/Infrastructure/Payments/PaymentsTestSettings.cs` | `WithPaymob()` sets `HmacSecret = "hmac_test"`. `ToConfiguration` emits `Payments:Paymob:HmacSecret`. |
| Existing test files listed as "modify" in the Test plan | Only the listed methods. Where `Payment.Create`, `Renew`, `EnsureCanPurchase` or `new SubscriptionResult(...)` appear only in Arrange, the signature change is mechanical (add `MonthsFor(period)` / `1`, `now`, `renewalWindow`, `false, false`). |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `twentyFirst => twentyFirst.Should().EndWith("_AddPaymentWebhookState")` to `Migrate_FreshDatabase_LeavesNoPendingMigrations` (the accepted pattern). |
| `postman/elmanhg.postman_collection.json` | Subscriptions folder:<br>• "Get my entitlement after payment": its test script stores `subscriptions[0].id` into the collection variable `baseSubscriptionId`.<br>• Append "Cancel subscription": `POST {{baseUrl}}/api/subscriptions/{{baseSubscriptionId}}/cancel`, expects 200 and `subscriptions[0].status == "Cancelled"`.<br>New folder "PaymobWebhook" (after Subscriptions) with one request, "Unsigned webhook is rejected": `POST {{baseUrl}}/api/payments/paymob/webhook?hmac=0` with body `{"type":"TRANSACTION","obj":{"id":1,"success":true,"pending":false,"amount_cents":100,"currency":"EGP","order":{"id":1}}}`, expects 401 and `code == "PAYMOB_WEBHOOK_SIGNATURE_INVALID"`. |
| `web/src/shared/api/generated/**` | Regenerated with `npm --prefix web run gen:api`. |
| `web/src/features/subscription/components/PlanCardGrid.tsx` | Renew actions (see Web contract). |
| `web/src/features/subscription/components/BaseCheckoutActions.tsx` | New prop `mode: 'subscribe' \| 'renew'`. Label `t(\`checkout.${mode}.${price.period}\`)`. |
| `web/src/features/subscription/components/AskTeacherCheckoutAction.tsx` | New prop `mode: 'subscribe' \| 'renew'`. Label `mode === 'renew' ? t('checkout.renewAskTeacher') : t('checkout.subscribeAskTeacher')`. |
| `web/src/features/subscription/components/CurrentPlanCard.tsx` | New props `onCancel: (subscriptionId: string) => void` and `isCancelPending: boolean`. Holds `const [cancelling, setCancelling] = useState<SubscriptionResult \| null>(null)`. Renders `<CancelSubscriptionDialog>`. Passes `onCancelClick={setCancelling}` to each line. |
| `web/src/features/subscription/components/SubscriptionStatusLine.tsx` | New prop `onCancelClick: (subscription: SubscriptionResult) => void`. Grace wording. Cancel button (see Web contract). |
| `web/src/features/subscription/pages/SubscriptionPage.tsx` | `const cancellation = useSubscriptionCancellation();`, then `<CurrentPlanCard entitlement={…} onCancel={cancellation.cancel} isCancelPending={cancellation.isPending} />`. |
| `web/src/features/subscription/i18n/en.json`, `ar.json` | New keys (Web contract). |
| `web/src/shared/i18n/en.json`, `ar.json` | `errors.SUBSCRIPTION_ENDED`, `errors.SUBSCRIPTION_NOT_FOUND` and `errors.SUBSCRIPTION_MODIFIED_CONCURRENTLY` (strings = the resx strings below). |
| `web/src/test/subscriptionFixtures.ts` | `subscription()` gains `canRenew: boolean` and `inGracePeriod: boolean` params, both defaulting to false. `baseEntitlement({ withAskTeacher, status, canRenew = false, inGracePeriod = false })` forwards them to every subscription. |
| `docs/subscriptions.md`, `docs/paymob.md`, `docs/audit-log.md`, `docs/PRD.md`, `docs/claude-design-prompt.md` | See Docs. |

`AppDbContext` edits:
- Constants:
  - `public const string PaymentProviderOrderIndex = "IX_Payments_ProviderOrderId";`
  - `public const string SubscriptionLapseIndex = "IX_Subscriptions_Status_CurrentPeriodEnd";`
- `ConfigureSubscriptions`:
  - Subscription: `builder.Property(x => x.Version).IsRowVersion();` and `builder.HasIndex(x => new { x.Status, x.CurrentPeriodEnd }, SubscriptionLapseIndex);`
  - Payment: `builder.Property(x => x.Version).IsRowVersion();`
  - Payment: `builder.Property(x => x.ProviderOrderId).HasMaxLength(PaymobReferenceMaxLength);`
  - Payment: `builder.Property(x => x.ReviewReason).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);`
  - Payment: `builder.Property(x => x.RawWebhook).HasColumnType("jsonb").HasAnnotation(AuditChangeReader.ExcludedAnnotation, true);`
  - Payment: `builder.HasIndex(x => x.ProviderOrderId, PaymentProviderOrderIndex).HasFilter("\"ProviderOrderId\" IS NOT NULL");`
- `SaveChangesAsync` gains three catch clauses, placed right after the existing Session/QuestionMastery clause:
  - `DbUpdateConcurrencyException when Entries.Any(x => x.Entity is Payment)` → `ConflictCoreException(ErrorCodes.PaymentModifiedConcurrently, innerException)`
  - `DbUpdateConcurrencyException when Entries.Any(x => x.Entity is Subscription)` → `ConflictCoreException(ErrorCodes.SubscriptionModifiedConcurrently, …)`
  - `DbUpdateException when InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: PaymobTransactionIndex }` → `ConflictCoreException(ErrorCodes.PaymentTransactionAlreadyRecorded, …)`

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Subscriptions/PaymentReviewReason.cs` | enum | `namespace Elmanhg.Domain.Subscriptions; public enum PaymentReviewReason { AskTeacherWithoutBase }` |
| 2 | `api/Elmanhg.Domain/Subscriptions/SubscriptionLapseSpecification.cs` | static spec | `public static class SubscriptionLapseSpecification { public static Expression<Func<Subscription, bool>> DueAt(DateTimeOffset now, TimeSpan gracePeriod, IReadOnlyCollection<Guid> excludedIds) }`. Body: `var lapsedBefore = now - gracePeriod; return x => !excludedIds.Contains(x.Id) && ((x.Status == Active && x.CurrentPeriodEnd <= now) \|\| (x.Status == PastDue && x.CurrentPeriodEnd <= lapsedBefore) \|\| (x.Status == Cancelled && x.CurrentPeriodEnd <= now));` |
| 3 | `api/Elmanhg.Application/Shared/Payments/PaymentNotification.cs` | record | `public sealed record PaymentNotification(string TransactionId, string? MerchantOrderId, string? ProviderOrderId, bool Succeeded, bool Pending, bool IsRefundOrVoid, long AmountMinor, string Currency);` |
| 4 | `api/Elmanhg.Application/Shared/Payments/IPaymentNotificationReader.cs` | port | `public interface IPaymentNotificationReader { PaymentNotification? Read(string payload, string? signature); }`. Returns null for a non-transaction callback. Throws 401 or 400 (Error codes). |
| 5 | `api/Elmanhg.Application/Subscriptions/Shared/PaymentNotificationOutcome.cs` | enum | `public enum PaymentNotificationOutcome { Succeeded, MarkedFailed, Duplicate, Ignored, OutOfOrder }` |
| 6 | `api/Elmanhg.Application/Subscriptions/Shared/PaymentNotificationResult.cs` | result (server-to-server, not localised) | `public sealed record PaymentNotificationResult(Guid? PaymentId, PaymentNotificationOutcome Outcome) : IAuditableResult { Guid? IAuditableResult.AuditResourceId => PaymentId; }` |
| 7 | `api/Elmanhg.Application/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationCommand.cs` | command | `public sealed record ProcessPaymentNotificationCommand(string Payload, string? Signature) : IRequest<PaymentNotificationResult>, IAuditableCommand`. `AuditAction => "Payment.ProcessNotification"`, `AuditResourceType => "Payment"`, `AuditResourceId => null`. |
| 8 | `…/ProcessPaymentNotification/ProcessPaymentNotificationHandler.cs` | handler | Constructor: `(IPaymentRepository paymentRepository, ISubscriptionRepository subscriptionRepository, IPaymentNotificationReader notificationReader, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider)`. Steps: see #H1 below. |
| 9 | `api/Elmanhg.Application/Subscriptions/CancelSubscription/CancelSubscriptionCommand.cs` | command | `public sealed record CancelSubscriptionCommand(Guid SubscriptionId) : IRequest<EntitlementResult>, IAuditableCommand`. `"Subscription.Cancel"`, `"Subscription"`, `AuditResourceId => SubscriptionId`. |
| 10 | `…/CancelSubscription/CancelSubscriptionHandler.cs` | handler | Constructor: `(ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService)`. Steps:<br>1. `UserId` null or default → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`.<br>2. `FirstOrDefaultAsync(x => x.Id == request.SubscriptionId && x.StudentId == userId, ct)` (tracked) ?? `NotFoundCoreException(ErrorCodes.SubscriptionNotFound)`.<br>3. `now`, `options`. If `subscription.Status != Cancelled && !subscription.IsEntitledAt(now, options.GracePeriod)` → `BusinessRuleViolationCoreException(DomainErrorCodes.SubscriptionEnded)`.<br>4. `subscription.Cancel(now)` (the domain throws `SUBSCRIPTION_ENDED` for Cancelled or Expired).<br>5. `SaveChangesAsync`.<br>6. `return await StudentEntitlementLoader.LoadAsync(subscriptionRepository, userId, options, now, ct)`. |
| 11 | `…/CancelSubscription/CancelSubscriptionValidator.cs` | validator | `RuleFor(x => x.SubscriptionId).ValidateRequired(ErrorCodes.SubscriptionIdRequired);` (`Core.Validation.Extensions`) |
| 12 | `api/Elmanhg.Application/Subscriptions/GetLapsedSubscriptionIds/GetLapsedSubscriptionIdsQuery.cs` | query | `public sealed record GetLapsedSubscriptionIdsQuery(IReadOnlyCollection<Guid> ExcludedIds) : IRequest<List<Guid>>;` |
| 13 | `…/GetLapsedSubscriptionIds/GetLapsedSubscriptionIdsHandler.cs` | handler | Constructor: `(ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider)`. `FindPaginatedAsync(1, options.LapseSweepBatchSize, ct, filter: SubscriptionLapseSpecification.DueAt(now, options.GracePeriod, request.ExcludedIds), orderBy: q => q.OrderBy(x => x.CurrentPeriodEnd), asNoTracking: true)`, then `.Items.Select(x => x.Id).ToList()`. |
| 14 | `api/Elmanhg.Application/Subscriptions/LapseSubscription/LapseSubscriptionCommand.cs` | command | `public sealed record LapseSubscriptionCommand(Guid SubscriptionId) : IRequest, IAuditableCommand`. `"Subscription.Lapse"`, `"Subscription"`, `SubscriptionId`. |
| 15 | `…/LapseSubscription/LapseSubscriptionHandler.cs` | handler | Constructor: `(ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider)`. Steps:<br>1. `GetByIdAsync(request.SubscriptionId, ct)` (tracked); null → return.<br>2. `if (!subscription.Lapse(timeProvider.GetUtcNow(), options.GracePeriod)) return;`<br>3. `SaveChangesAsync`. |
| 16 | `…/LapseSubscription/LapseSubscriptionValidator.cs` | validator | `RuleFor(x => x.SubscriptionId).ValidateRequired(ErrorCodes.SubscriptionIdRequired);` |
| 17 | `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobHmac.cs` | static | `public static class PaymobHmac`:<br>• `public static readonly IReadOnlyList<string> TransactionFields = [ …20 paths in Decision 6 order… ];`<br>• `public static string Compute(JsonElement transaction, string secret)`: `HMACSHA512.HashData(UTF8(secret), UTF8(concat))`, then `Convert.ToHexStringLower`.<br>• `public static bool IsValid(JsonElement transaction, string secret, string? signature)`: false when the secret or signature is blank; otherwise `CryptographicOperations.FixedTimeEquals(UTF8(Compute(...)), UTF8(signature.Trim().ToLowerInvariant()))`.<br>• `private static string ValueAt(JsonElement transaction, string path)`: walks a `.`-split path through objects and applies the Decision 6 text rules. |
| 18 | `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobNotificationReader.cs` | adapter | `public sealed class PaymobNotificationReader(IOptions<PaymentsOptions> paymentsOptions) : IPaymentNotificationReader`. Private consts: `TransactionType = "TRANSACTION"` and the JSON property names. `Read`:<br>1. `JsonDocument.Parse(payload)` inside `try`; on `JsonException` → `BadRequestCoreException(ErrorCodes.PaymobWebhookPayloadInvalid)`. A root that is not an object → same.<br>2. `type` is not the string `TRANSACTION` → return null.<br>3. `obj` is not an object → PayloadInvalid.<br>4. `signature ?? root.hmac` (string). `!PaymobHmac.IsValid(obj, paymob.HmacSecret, sig)` → `UnauthorizedCoreException(ErrorCodes.PaymobWebhookSignatureInvalid)`.<br>5. Extract:<br>&nbsp;&nbsp;• `id` (Number → raw text, or non-blank String)<br>&nbsp;&nbsp;• `success`, `pending` (bool)<br>&nbsp;&nbsp;• `amount_cents` (`TryGetInt64`)<br>&nbsp;&nbsp;• `currency` (non-blank string)<br>&nbsp;&nbsp;• `order` object with `id` (Number or String)<br>&nbsp;&nbsp;Any of these missing or of the wrong kind → PayloadInvalid.<br>&nbsp;&nbsp;• `order.merchant_order_id`: a String, or a Number's raw text, else null.<br>&nbsp;&nbsp;• `IsRefundOrVoid` = any of `is_refunded`, `is_voided`, `is_refund`, `is_void`, `has_parent_transaction` is `true`.<br>6. Return a `PaymentNotification`. It never logs the payload. |
| 19 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddPaymentWebhookState.cs` (+ `.Designer.cs`, snapshot updated) | migration | Generated by `dotnet ef migrations add AddPaymentWebhookState`. Adds:<br>• `Payments.PeriodMonths int NOT NULL DEFAULT 0`<br>• `Payments.ProviderOrderId varchar(100) NULL`<br>• `Payments.ReviewReason varchar(50) NULL`<br>• `xmin` on Payments and Subscriptions<br>• both indexes<br>After the `AddColumn` for `PeriodMonths`, hand-add `migrationBuilder.Sql("UPDATE \"Payments\" SET \"PeriodMonths\" = CASE \"Period\" WHEN 'Monthly' THEN 1 WHEN 'Termly' THEN 4 ELSE 12 END;");`. |
| 20 | `api/Elmanhg.Api/Controllers/Payments/PaymobWebhooksController.cs` | controller | `namespace Elmanhg.Api.Controllers.Payments; [ApiController][Route("api/payments/paymob")][ApiExplorerSettings(IgnoreApi = true)] public class PaymobWebhooksController(IMediator mediator) : ControllerBase`. `private const long MaxNotificationBytes = 65536;`. Action: `[HttpPost("webhook", Name = "ProcessPaymobWebhook")][AllowAnonymous][RequestSizeLimit(MaxNotificationBytes)] public async Task<ActionResult> ProcessWebhook([FromBody] JsonElement payload, [FromQuery] string? hmac, CancellationToken cancellationToken)` → `Ok(await mediator.Send(new ProcessPaymentNotificationCommand(payload.GetRawText(), hmac), cancellationToken))`. |
| 21 | `api/Elmanhg.Api/Workers/SubscriptionLapseWorker.cs` | BackgroundService | `public sealed class SubscriptionLapseWorker(IServiceScopeFactory scopeFactory, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ILogger<SubscriptionLapseWorker> logger) : BackgroundService`. A line-for-line mirror of `ExpiredExamSubmissionWorker`, with these swaps:<br>• `LapseSweepEnabled` / `LapseSweepIntervalSeconds` / `LapseSweepBatchSize`<br>• `GetLapsedSubscriptionIdsQuery([.. _deferredIds])`<br>• `LapseSubscriptionCommand(id)`<br>Log messages: `"Listing lapsed subscriptions failed."` (Error) and `"Lapse of subscription {SubscriptionId} failed."` (Warning). Keep `_deferredIds` and the `when (!stoppingToken.IsCancellationRequested)` filters. |
| 22–26 | `api/Elmanhg.Tests/Fixtures/Paymob/transaction-succeeded.json`, `transaction-declined.json`, `transaction-pending.json`, `transaction-refunded.json`, `token.json` | recorded payloads | Shape in "Recorded payloads" below. |
| 27 | `api/Elmanhg.Tests/Fixtures/Paymob/PaymobPayloads.cs` | test helper | `namespace Elmanhg.Tests.Fixtures.Paymob; public static class PaymobPayloads`:<br>• consts `Succeeded`, `Declined`, `Pending`, `Refunded`, `Token` = the file names<br>• `public static string Read(string fileName)`: `File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Paymob", fileName))`<br>• `public static string ForPayment(string fileName, Guid paymentId, long transactionId, long amountCents, long orderId)`: edits via `JsonNode` `obj.id`, `obj.amount_cents`, `obj.order.id`, `obj.order.merchant_order_id = paymentId.ToString()`, `obj.order.amount_cents`<br>• `public static string Sign(string payload, string secret) => PaymobHmac.Compute(JsonDocument.Parse(payload).RootElement.GetProperty("obj"), secret);` |
| 28–39 | Test classes | tests | See Test plan. |
| 40 | `web/src/features/subscription/components/CancelSubscriptionDialog.tsx` | component | `export interface CancelSubscriptionDialogProps { subscription: SubscriptionResult \| null; isPending: boolean; onOpenChange: (open: boolean) => void; onConfirm: () => void; }`. Mirrors `ExamSubmitDialog`:<br>• `<Dialog open={subscription !== null}>` + `DialogContent title={t('cancel.title', { plan })}`<br>• `<p className="text-ui text-text">{t('cancel.body', { date })}</p>`, where the date is `formatDate(new Date(currentPeriodEnd), i18n.language, 'arabic-indic', { dateStyle: 'medium' })`<br>• Buttons: `secondary` `t('cancel.keep')` (closes), `danger` `t('cancel.confirm')` (disabled while `isPending`, calls `onConfirm`) |
| 41 | `web/src/features/subscription/hooks/useSubscriptionCancellation.ts` | hook | `export interface SubscriptionCancellation { cancel: (subscriptionId: string, onDone: () => void) => void; isPending: boolean; }`. Wraps the generated `useCancelSubscription`:<br>• `onSuccess(result)`: `queryClient.setQueryData(getGetMyEntitlementQueryKey(), result)`, then `toast.success(t('cancel.done'))`<br>• `onError`: `toast.error(t([\`common:errors.${code}\`, 'common:errors.UNHANDLED_EXCEPTION']))`, as in `useCheckout`<br>• `cancel` calls `mutation.mutate({ subscriptionId }, { onSettled: onDone })` |
| 42 | `web/src/features/subscription/pages/SubscriptionPage.manage.test.tsx` | tests | See Test plan. |

### #H1 `ProcessPaymentNotificationHandler.Handle`
1. `var notification = notificationReader.Read(request.Payload, request.Signature);` null → `return new(null, Ignored)`.
2. `notification.Pending || notification.IsRefundOrVoid` → `return new(null, Ignored)`.
3. `var recorded = await paymentRepository.FirstOrDefaultAsync(x => x.PaymobTransactionId == notification.TransactionId, ct, asNoTracking: true)`. Not null → `return new(recorded.Id, Duplicate)`.
4. `var payment = await FindPaymentAsync(notification, ct) ?? throw new NotFoundCoreException(ErrorCodes.PaymentNotFound);`. The private `FindPaymentAsync` is tracked: `Guid.TryParse(MerchantOrderId)` → `FirstOrDefaultAsync(x => x.Id == id)`; else, when `ProviderOrderId` is not null, → `FirstOrDefaultAsync(x => x.ProviderOrderId == notification.ProviderOrderId)`; else null.
5. `payment.AmountMinor != notification.AmountMinor || payment.Currency != notification.Currency || (payment.ProviderOrderId is not null && payment.ProviderOrderId != notification.ProviderOrderId)` → `BadRequestCoreException(ErrorCodes.PaymentNotificationMismatch)`.
6. `var now = timeProvider.GetUtcNow();`
7. `!notification.Succeeded`:
   - `payment.Status != Pending` → `return new(payment.Id, OutOfOrder)`;
   - else `PaymentSettlement.Fail(payment, notification.TransactionId, request.Payload, now)`, `SaveChangesAsync`, `return new(payment.Id, MarkedFailed)`.
8. `payment.Status == Succeeded` → `return new(payment.Id, OutOfOrder)`.
9. `var options = subscriptionsOptions.Value;` `var subscriptions = await subscriptionRepository.FindAsync(SubscriptionEntitlementSpecification.EntitledFor(payment.StudentId, now, options.GracePeriod), ct);` (tracked). `var entitlement = StudentEntitlement.Resolve(subscriptions, now, options.GracePeriod);`
10. `var started = PaymentSettlement.Succeed(payment, entitlement, notification.TransactionId, request.Payload, options.GracePeriod, now);` Not null → `subscriptionRepository.AddAsync(started, ct)`.
11. `paymentRepository.SaveChangesAsync(ct)`. `return new(payment.Id, Succeeded)`.

### #S1 `PaymentSettlement` (Application/Subscriptions/Shared)
```csharp
public static class PaymentSettlement
{
    public static Subscription? Succeed(Payment payment, StudentEntitlement entitlement, string transactionId, string rawNotification, TimeSpan gracePeriod, DateTimeOffset completedAt)
    public static void Fail(Payment payment, string transactionId, string rawNotification, DateTimeOffset completedAt) => payment.MarkFailed(transactionId, rawNotification, completedAt);
}
```
`Succeed`:
1. `var held = entitlement.Held(payment.Plan);`
2. `held` not null:
   - `payment.MarkSucceeded(held.Id, transactionId, rawNotification, completedAt)` first, so a Succeeded payment throws before anything changes;
   - then `held.Renew(payment.Period, payment.PeriodMonths, transactionId, completedAt, gracePeriod)`;
   - `started = null`.
3. `held` null:
   - `started = Subscription.Start(payment.StudentId, payment.Plan, payment.Period, payment.PeriodMonths, completedAt, transactionId, payment.StudentId)`;
   - `payment.MarkSucceeded(started.Id, …)`.
4. `payment.Plan == AskTeacher && entitlement.BaseSubscription is null` → `payment.FlagForReview(PaymentReviewReason.AskTeacherWithoutBase)`.
5. Return `started`.

### #S2 `CompleteFakePaymentHandler.Handle` (same constructor)
1. The user check is unchanged.
2. The `SupportsSimulatedCompletion` check is unchanged.
3. The owned payment lookup is unchanged (tracked).
4. `payment.Status != Pending` → `BusinessRuleViolationCoreException(DomainErrorCodes.PaymentNotPending)` (alias `using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;`).
5. `transactionId`, `rawNotification` and `now` as today.
6. `!request.Succeeded` → `PaymentSettlement.Fail`, save, return.
7. Load the tracked entitled subscriptions (as #H1 step 9), then `Succeed`, then `AddAsync` when started, then save.
8. `return PaymentResultGenerator.Generate(payment)`. The `PriceFor` lookup and the `PurchaseConflict` branch are deleted.

## Error codes
All of these go in `Elmanhg.Application.Exceptions.ErrorCodes`.
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `SubscriptionNotFound` | `SUBSCRIPTION_NOT_FOUND` | CancelSubscriptionHandler | `NotFoundCoreException` | 404 |
| `SubscriptionIdRequired` | `SUBSCRIPTION_ID_REQUIRED` | Cancel/Lapse validators | validation | 422 |
| `SubscriptionModifiedConcurrently` | `SUBSCRIPTION_MODIFIED_CONCURRENTLY` | AppDbContext | `ConflictCoreException` | 409 |
| `PaymentModifiedConcurrently` | `PAYMENT_MODIFIED_CONCURRENTLY` | AppDbContext | `ConflictCoreException` | 409 |
| `PaymentTransactionAlreadyRecorded` | `PAYMENT_TRANSACTION_ALREADY_RECORDED` | AppDbContext | `ConflictCoreException` | 409 |
| `PaymentNotificationMismatch` | `PAYMENT_NOTIFICATION_MISMATCH` | ProcessPaymentNotificationHandler | `BadRequestCoreException` | 400 |
| `PaymobWebhookSignatureInvalid` | `PAYMOB_WEBHOOK_SIGNATURE_INVALID` | PaymobNotificationReader | `UnauthorizedCoreException` | 401 |
| `PaymobWebhookPayloadInvalid` | `PAYMOB_WEBHOOK_PAYLOAD_INVALID` | PaymobNotificationReader | `BadRequestCoreException` | 400 |

Reused codes:
- `PAYMENT_NOT_FOUND` (404, webhook unmatched);
- `SUBSCRIPTION_ENDED` and `PAYMENT_NOT_PENDING` (domain, 400);
- `SUBSCRIPTION_PERIOD_INVALID` (`Payment.Create` months < 1).

| Key | en | ar |
|---|---|---|
| SUBSCRIPTION_NOT_FOUND | Subscription not found. | الاشتراك غير موجود. |
| SUBSCRIPTION_ID_REQUIRED | The subscription id is required. | معرف الاشتراك مطلوب. |
| SUBSCRIPTION_MODIFIED_CONCURRENTLY | This subscription was changed by another request. Please try again. | تم تعديل هذا الاشتراك بواسطة طلب آخر. حاول مرة أخرى. |
| PAYMENT_MODIFIED_CONCURRENTLY | This payment was changed by another request. Please try again. | تم تعديل هذا الدفع بواسطة طلب آخر. حاول مرة أخرى. |
| PAYMENT_TRANSACTION_ALREADY_RECORDED | This payment transaction has already been recorded. | تم تسجيل عملية الدفع هذه بالفعل. |
| PAYMENT_NOTIFICATION_MISMATCH | The payment notification does not match the payment. | اشعار الدفع لا يطابق عملية الدفع. |
| PAYMOB_WEBHOOK_SIGNATURE_INVALID | The payment notification signature is not valid. | توقيع اشعار الدفع غير صالح. |
| PAYMOB_WEBHOOK_PAYLOAD_INVALID | The payment notification is not valid. | اشعار الدفع غير صالح. |

## Domain behaviour
`Subscription` (`Subscription.cs`):
```csharp
public uint Version { get; private set; }
public bool IsRenewableAt(DateTimeOffset now, TimeSpan renewalWindow) => Status != SubscriptionStatus.Expired && CurrentPeriodEnd - renewalWindow <= now;
```
`Subscription.Lifecycle.cs`:
```csharp
public void Renew(BillingPeriod period, int periodMonths, string? paymobReference, DateTimeOffset renewedAt, TimeSpan gracePeriod)
{
    if (!IsEntitledAt(renewedAt, gracePeriod)) throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionEnded);
    EnsurePeriod(periodMonths);
    Period = period;
    CurrentPeriodStart = CurrentPeriodEnd;
    CurrentPeriodEnd = CurrentPeriodEnd.AddMonths(periodMonths);
    Status = SubscriptionStatus.Active;
    CancelledAt = null;
    PaymobReference = paymobReference ?? PaymobReference;
    UpdationDate = DateTimeOffset.UtcNow;
}

public bool Lapse(DateTimeOffset now, TimeSpan gracePeriod)
{
    if (EntitledUntil(gracePeriod) is { } lapsedAt && lapsedAt <= now) { Expire(lapsedAt); return true; }
    if (Status == SubscriptionStatus.Active && CurrentPeriodEnd <= now) { MarkPastDue(); return true; }
    return false;
}
```
`MarkPastDue`, `Cancel` and `Expire` are unchanged; they set `UpdationDate`. Renewing an Expired subscription is rejected (`EntitledUntil` is null, so `IsEntitledAt` is false). Renewing a Cancelled one before its end resumes it.

`Payment`:
- New properties, all with private setters:
  - `public int PeriodMonths { get; private set; }`
  - `public string? ProviderOrderId { get; private set; }`
  - `public PaymentReviewReason? ReviewReason { get; private set; }`
  - `public uint Version { get; private set; }`
- `Create(Guid studentId, SubscriptionPlan plan, BillingPeriod period, int periodMonths, Money amount)`: the existing amount guard, then `periodMonths < 1` → `SubscriptionPeriodInvalid`. Sets `PeriodMonths`.
- `MarkSucceeded(...)`: the guard becomes `if (Status == PaymentStatus.Succeeded) throw PaymentNotPending`, so Pending and Failed are accepted. The body is unchanged and sets `UpdationDate`.
- `MarkFailed`: still Pending only (`EnsurePending`).
- `public void LinkProviderOrder(string providerOrderId) { EnsurePending(); ProviderOrderId = providerOrderId; UpdationDate = DateTimeOffset.UtcNow; }`
- `public void FlagForReview(PaymentReviewReason reason) { ReviewReason = reason; UpdationDate = DateTimeOffset.UtcNow; }`

`StudentEntitlement`:
```csharp
public Subscription? Held(SubscriptionPlan plan) => plan == SubscriptionPlan.Base ? BaseSubscription : AskTeacherSubscription;
public string? PurchaseConflict(SubscriptionPlan plan, DateTimeOffset now, TimeSpan renewalWindow) => plan switch
{
    SubscriptionPlan.AskTeacher when BaseSubscription is null => ErrorCodes.CheckoutRequiresBase,
    _ when Held(plan) is { } held && !held.IsRenewableAt(now, renewalWindow) => ErrorCodes.CheckoutPlanAlreadyActive,
    _ => null,
};
public void EnsureCanPurchase(SubscriptionPlan plan, DateTimeOffset now, TimeSpan renewalWindow) // throws as today
```
`SubscriptionsOptions` additions:
```csharp
[Range(0, 30)] public int RenewalWindowDays { get; set; } = 7;
public bool LapseSweepEnabled { get; set; } = true;
[Range(5, 86400)] public int LapseSweepIntervalSeconds { get; set; } = 300;
[Range(1, 1000)] public int LapseSweepBatchSize { get; set; } = 100;
public TimeSpan RenewalWindow => TimeSpan.FromDays(RenewalWindowDays);
```

## Recorded payloads (`api/Elmanhg.Tests/Fixtures/Paymob/`)
`transaction-succeeded.json` (Paymob transaction-processed callback shape, with fake PII):
```json
{"type":"TRANSACTION","obj":{"id":192036465,"pending":false,"amount_cents":19900,"success":true,"is_auth":false,"is_capture":false,"is_standalone_payment":true,"is_voided":false,"is_refunded":false,"is_3d_secure":true,"integration_id":111,"profile_id":164295,"has_parent_transaction":false,"order":{"id":217503754,"created_at":"2026-09-29T10:15:02.123456","delivery_needed":false,"merchant":{"id":164295,"company_name":"Elmanhg"},"amount_cents":19900,"shipping_data":{"first_name":"Mona","last_name":"Ali","email":"mona@example.test","phone_number":"01012345678","country":"EG","city":"NA","street":"NA","building":"NA","floor":"NA","apartment":"NA","state":"NA"},"currency":"EGP","merchant_order_id":"0b6f5a4e-3c1d-4e2f-9a8b-7c6d5e4f3a2b","payment_status":"PAID"},"created_at":"2026-09-29T10:15:20.463870","currency":"EGP","source_data":{"pan":"2346","type":"card","sub_type":"MasterCard"},"error_occured":false,"owner":302852,"billing_data":{"first_name":"Mona","last_name":"Ali","email":"mona@example.test","phone_number":"01012345678","country":"EG"},"data":{"message":"Approved","txn_response_code":"APPROVED"}},"issuer_bank":null,"transaction_processed_callback_responses":""}
```
The other payloads are the same file with these changes:
- `transaction-declined.json`: `success:false`, `order.payment_status:"UNPAID"`, `data.message:"Do not honour"`, `data.txn_response_code:"DECLINED"`.
- `transaction-pending.json`: `pending:true`, `success:false`.
- `transaction-refunded.json`: `success:true`, `is_refunded:true`.
- `token.json`: `{"type":"TOKEN","obj":{"id":8561234,"token":"tok_test","masked_pan":"xxxx-xxxx-xxxx-2346","merchant_id":164295,"card_subtype":"MasterCard","created_at":"2026-09-29T10:15:21.000000","email":"mona@example.test","order_id":"217503754"}}`

**Known-answer digest**: compute it independently, **not** with the C# code under test, and hard-code the resulting hex as `ExpectedDigest` in `PaymobHmacTests`:
```python
import json, hmac, hashlib
obj = json.load(open(r'api/Elmanhg.Tests/Fixtures/Paymob/transaction-succeeded.json', encoding='utf-8'))['obj']
keys = ['amount_cents','created_at','currency','error_occured','has_parent_transaction','id','integration_id','is_3d_secure','is_auth','is_capture','is_refunded','is_standalone_payment','is_voided','order.id','owner','pending','source_data.pan','source_data.sub_type','source_data.type','success']
def get(o, p):
    for part in p.split('.'):
        o = o.get(part) if isinstance(o, dict) else None
    return o
def fmt(v): return '' if v is None else 'true' if v is True else 'false' if v is False else str(v)
print(hmac.new(b'paymob-known-answer-key', ''.join(fmt(get(obj, k)) for k in keys).encode('utf-8'), hashlib.sha512).hexdigest())
```

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| POST | `/api/payments/paymob/webhook?hmac=` | `[AllowAnonymous]` (HMAC), hidden from OpenAPI | raw Paymob JSON body | 200 `PaymentNotificationResult`; 401 `PAYMOB_WEBHOOK_SIGNATURE_INVALID`; 400 `PAYMOB_WEBHOOK_PAYLOAD_INVALID` / `PAYMENT_NOTIFICATION_MISMATCH`; 404 `PAYMENT_NOT_FOUND`; 409 concurrency or duplicate transaction |
| POST | `/api/subscriptions/{subscriptionId:guid}/cancel` | `DefaultCodes.SubscriptionManage` | none (`[FromRoute] Guid subscriptionId`), `Name = "CancelSubscription"`, `[ProducesResponseType<EntitlementResult>(200)]` | 200 `EntitlementResult`; 400 `SUBSCRIPTION_ENDED`; 404 `SUBSCRIPTION_NOT_FOUND`; 409 |
| GET | `/api/subscriptions/entitlement` (existing) | unchanged | — | `subscriptions[]` gains `inGracePeriod`, `canRenew` |
| POST | `/api/subscriptions/checkout` (existing) | unchanged | — | now allows a held plan inside the renewal window |
| POST | `/api/subscriptions/payments/{id}/fake-completion` (existing) | unchanged | — | a success for a held plan now extends it (200) instead of 400 |

## Web contract
- `PlanCardGrid`:
  - `const held = (plan: SubscriptionPlan) => entitlement.subscriptions.find((s) => s.plan === plan);`
  - Base `actions`:
    - `tier !== 'Base'` → `<BaseCheckoutActions mode="subscribe" …/>`;
    - else `held('Base')?.canRenew` → `<BaseCheckoutActions mode="renew" prices={base.prices} disabled={isCheckoutPending} onCheckout={(p) => onCheckout('Base', p)} />`;
    - else `null`.
  - AskTeacher `actions`:
    - `!hasAskTeacher` → the existing subscribe action (`mode="subscribe"`);
    - else `held('AskTeacher')?.canRenew` → `<AskTeacherCheckoutAction mode="renew" hasBase …/>`;
    - else `null`.
- `SubscriptionStatusLine`:
  - The `Expired` → null rule is unchanged.
  - `const inGrace = status === 'Active' && subscription.inGracePeriod;`
  - The text key is `inGrace ? 'status.ActiveGrace' : \`status.${status}\``. The date is `entitledUntil` when `inGrace` or not Active, else `currentPeriodEnd`.
  - `<li className="flex flex-wrap items-center justify-between gap-2 text-caption text-text-muted"><span>{text}</span>{status === 'Active' \|\| status === 'PastDue' ? <Button variant="danger" size="sm" aria-label={t('cancel.buttonLabel', { plan: t(\`plan.${plan}\`) })} onClick={() => { onCancelClick(subscription); }}>{t('cancel.button')}</Button> : null}</li>`
- `CurrentPlanCard`: `onConfirm={() => { if (cancelling) onCancel(cancelling.id, () => { setCancelling(null); }); }}`. The dialog's `isPending={isCancelPending}`, and `onOpenChange={(open) => { if (!open) setCancelling(null); }}`.
- i18n keys (en / ar):

| Key | en | ar |
|---|---|---|
| `status.ActiveGrace` | {plan} — period ended, available until {date} | {plan} — انتهت المدة، متاحة حتى {date} |
| `checkout.renew.Monthly` | Renew monthly | جدد شهريًا |
| `checkout.renew.Termly` | Renew for a term | جدد لفصل دراسي |
| `checkout.renew.Yearly` | Renew yearly | جدد سنويًا |
| `checkout.renewAskTeacher` | Renew | جدد |
| `cancel.button` | Cancel | إلغاء |
| `cancel.buttonLabel` | Cancel {plan} | إلغاء {plan} |
| `cancel.title` | Cancel {plan}? | إلغاء {plan}؟ |
| `cancel.body` | You keep access until {date}, the end of the period you paid for. | ستبقى الباقة متاحة حتى {date}، نهاية المدة التي دفعت ثمنها. |
| `cancel.keep` | Keep subscription | الإبقاء على الاشتراك |
| `cancel.confirm` | Cancel subscription | إلغاء الاشتراك |
| `cancel.done` | Subscription cancelled | تم إلغاء الاشتراك |

`checkout.subscribe` stays as it is: `BaseCheckoutActions` reads `checkout.subscribe.<period>` or `checkout.renew.<period>`.

## Docs
- `docs/subscriptions.md`:
  - Lifecycle table: the `Renew(period, periodMonths, reference?, renewedAt, grace)` row (from: entitled Active, PastDue or Cancelled; starts at the previous end; switches period; resumes Cancelled; `SUBSCRIPTION_ENDED` when not entitled) and a new `Lapse(now, grace)` row.
  - Config table: `RenewalWindowDays`, `LapseSweepEnabled`, `LapseSweepIntervalSeconds`, `LapseSweepBatchSize`.
  - Payments: `PeriodMonths` snapshot, `ProviderOrderId`, `ReviewReason`, Failed→Succeeded, `xmin`.
  - Checkout: the renewal window replaces "renewal is not sold here".
  - Settlement: the extend policy and the AskTeacher flag.
  - A new "Webhook" section: pointer to `docs/paymob.md` §8, the outcomes table, idempotency and ordering.
  - "Lapse sweep" section.
  - "Student cancel" section.
  - API table: add cancel.
  - Web paragraph: Renew buttons, cancel dialog, grace line.
  - "For later stories": remove the #101 bullet; #102 lists flagged payments and refund/void callbacks. Add the known limit (Decision 23).
- `docs/paymob.md`:
  - §2: add `HmacSecret` (secret; Dashboard → Settings → Account info → HMAC).
  - §3: HmacSecret required with Paymob.
  - §4: `intention_order_id` is stored as `ProviderOrderId`.
  - §5: add the Deferred verification items.
  - §6: the fake settles like the webhook, with no 400.
  - §7: set `Payments__Paymob__HmacSecret` and `NotificationUrl=https://<api>/api/payments/paymob/webhook`.
  - New §8 "Transaction webhook": route; HMAC fields and format (Decision 6) with the uncertainty note; signature location; matching and binding (Decisions 10–11); ignored callbacks; status codes; the return URL is never used to settle (Decision 21).
- `docs/audit-log.md`: rows for `ProcessPaymentNotification` / `Payment.ProcessNotification` (result id; system actor; diff shows the Payment status, and the new or renewed Subscription), `CancelSubscription` / `Subscription.Cancel` (command) and `LapseSubscription` / `Subscription.Lapse` (command; system actor). Update the `CompleteFakePayment` row ("new or renewed Subscription"). In "Excluded properties", add properties carrying the `Core:AuditExcluded` model annotation: `Payment.RawWebhook` (provider payload with billing PII).
- `docs/PRD.md` §11.2: add bullets:
  - v1 has no automatic charge: the student renews by paying again from 7 days before the period end (configurable), and the new period continues from the old end.
  - A verified payment for a plan the student already holds extends it; an Ask a Teacher payment without Base is kept and flagged for admin review.
  - The student may cancel: access continues until the paid period ends.
  - A status sweep marks lapsed plans PastDue, then Expired.
- `docs/claude-design-prompt.md` §4, `#/student/subscription` line: append "; Renew buttons per period from 7 days before the period ends; each active plan line has a Danger 'إلغاء' opening a confirm dialog (access continues to the period end); an Active plan past its end shows 'انتهت المدة، متاحة حتى …'".

## Test plan
Backend: xUnit v3, FluentAssertions (repo-pinned), NSubstitute. Every handler throw asserts the type, the code and `SaveChangesAsync` `DidNotReceive()`. Every handler success asserts `Received(1)`.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `Tests/Domain/Subscriptions/SubscriptionTests` (modify) | `Renew_Active_ExtendsFromPreviousPeriodEnd` | new start = old end, new end = old end + months, Active |
| 2 | 〃 (modify) | `Renew_PastDue_ReactivatesAndExtends` | Active, extended |
| 3 | 〃 (modify, was `Renew_CancelledOrExpired_ThrowsSubscriptionEnded`) | `Renew_Expired_ThrowsSubscriptionEnded` | `SUBSCRIPTION_ENDED` |
| 4 | 〃 (new) | `Renew_CancelledBeforeEnd_ResumesAndClearsCancelledAt` | Active, `CancelledAt` null, extended |
| 5 | 〃 (new) | `Renew_ActivePastGrace_ThrowsSubscriptionEnded` | `SUBSCRIPTION_ENDED` |
| 6 | 〃 (new) | `Renew_InsideGrace_ExtendsFromPreviousPeriodEnd` | start = old end (in the past) |
| 7 | 〃 (new) | `Renew_DifferentPeriod_SwitchesPeriod` | Monthly → Yearly, +12 months |
| 8 | 〃 (modify) | `Renew_PeriodMonthsBelowOne_ThrowsPeriodInvalid` | code |
| 9 | 〃 (modify) | `Renew_WithoutReference_KeepsPreviousReference` | reference kept |
| 10 | 〃 (new) | `IsRenewableAt_ByDaysBeforeEnd_OpensInsideWindow` (Theory: 8 days before → false, 7 → true, 1 → true, after end → true) | bool |
| 11 | 〃 (new) | `Lapse_ActiveBeforeEnd_ReturnsFalseAndKeepsActive` | false, Active |
| 12 | 〃 (new) | `Lapse_ActiveAfterEndInsideGrace_BecomesPastDue` | true, PastDue |
| 13 | 〃 (new) | `Lapse_PastDueAfterGrace_ExpiresAtGraceEnd` | Expired, `ExpiredAt = end + grace` |
| 14 | 〃 (new) | `Lapse_ActiveAfterGrace_ExpiresAtGraceEnd` | Expired directly |
| 15 | 〃 (new) | `Lapse_CancelledAfterEnd_ExpiresAtPeriodEnd` | `ExpiredAt = end` |
| 16 | 〃 (new) | `Lapse_Expired_ReturnsFalse` | false |
| 17 | `Tests/Domain/Subscriptions/SubscriptionLapseSpecificationTests` (new) | `DueAt_StatusAndTimeGrid_AgreesWithLapse` (Theory: 4 statuses × {end−1d, end, end+1d, end+grace, end+grace+1d}) | `DueAt(...).Compile()(fresh)` == `fresh.Lapse(...)` |
| 18 | 〃 | `DueAt_ExcludedId_IsFalse` | false for a due but excluded id |
| 19 | `Tests/Domain/Subscriptions/PaymentTests` (modify all `Create` calls) | `Create_ValidAmount_IsPendingWithAmount` (modify) | also `PeriodMonths` |
| 20 | 〃 (new) | `Create_PeriodMonthsBelowOne_ThrowsPeriodInvalid` | `SUBSCRIPTION_PERIOD_INVALID` |
| 21 | 〃 (modify: `MarkSucceeded_NotPending_ThrowsNotPending(bool)` becomes a Fact) | `MarkSucceeded_AlreadySucceeded_ThrowsNotPending` | code |
| 22 | 〃 (new) | `MarkSucceeded_Failed_RecordsSuccessAndNewTransaction` | Succeeded, new transaction id, `SubscriptionId` |
| 23 | 〃 (new) | `LinkProviderOrder_Pending_StoresOrderId` | value |
| 24 | 〃 (new) | `LinkProviderOrder_NotPending_ThrowsNotPending` | code |
| 25 | 〃 (new) | `FlagForReview_SetsReason` | `AskTeacherWithoutBase` |
| 26 | `Tests/Domain/Subscriptions/StudentEntitlementTests` (modify the 5 `EnsureCanPurchase_*` for the new args, same expectations) | — | unchanged expectations |
| 27 | 〃 (new) | `EnsureCanPurchase_BaseInsideRenewalWindow_DoesNotThrow` | no throw |
| 28 | 〃 (new) | `EnsureCanPurchase_CancelledBaseInsideRenewalWindow_DoesNotThrow` | no throw |
| 29 | 〃 (new) | `EnsureCanPurchase_AskTeacherInsideRenewalWindow_DoesNotThrow` | no throw |
| 30 | `Tests/Application/Features/Subscriptions/Shared/PaymentSettlementTests` (modify: rewrite the 3 tests to the new API) | `Succeed_NoHeldPlan_StartsSubscriptionForSnapshotMonths` | new sub, months from `PeriodMonths`, payment Succeeded with its id |
| 31 | 〃 | `Fail_Pending_MarksFailed` | Failed, transaction id |
| 32 | 〃 | `Succeed_PaymentAlreadySucceeded_ThrowsPaymentNotPending` | code; held subscription unchanged |
| 33 | 〃 (new) | `Succeed_BaseHeld_RenewsHeldAndReturnsNull` | null, held end extended, payment `SubscriptionId` = held id |
| 34 | 〃 (new) | `Succeed_CancelledBaseHeld_ResumesHeld` | Active |
| 35 | 〃 (new) | `Succeed_AskTeacherWithoutBase_StartsAndFlagsForReview` | flag set |
| 36 | 〃 (new) | `Succeed_AskTeacherWithBase_DoesNotFlag` | `ReviewReason` null |
| 37 | `…/ProcessPaymentNotification/ProcessPaymentNotificationHandlerTests` (new) | `Handle_NotATransaction_ReturnsIgnored` | outcome, no save |
| 38 | 〃 | `Handle_PendingTransaction_ReturnsIgnored` | 〃 |
| 39 | 〃 | `Handle_RefundOrVoid_ReturnsIgnored` | 〃 |
| 40 | 〃 | `Handle_TransactionAlreadyRecorded_ReturnsDuplicate` | outcome + payment id, no save |
| 41 | 〃 | `Handle_UnknownMerchantOrder_ThrowsPaymentNotFound` | 404 code, no save |
| 42 | 〃 | `Handle_MerchantOrderMissing_MatchesByProviderOrderId` | Succeeded |
| 43 | 〃 | `Handle_AmountDiffers_ThrowsMismatch` | `PAYMENT_NOTIFICATION_MISMATCH`, payment still Pending |
| 44 | 〃 | `Handle_ProviderOrderDiffers_ThrowsMismatch` | 〃 |
| 45 | 〃 | `Handle_SuccessForPending_StartsSubscriptionAndSaves` | `AddAsync` of the subscription, payment Succeeded, `RawWebhook` = payload, `Received(1)` |
| 46 | 〃 | `Handle_SuccessWhileBaseHeld_RenewsHeldWithoutAdding` | held extended, `AddAsync` DidNotReceive |
| 47 | 〃 | `Handle_SuccessAfterFailedAttempt_MarksSucceeded` | Succeeded |
| 48 | 〃 | `Handle_FailureForPending_MarksFailed` | MarkedFailed, save |
| 49 | 〃 | `Handle_FailureAfterSuccess_ReturnsOutOfOrder` | Succeeded kept, no save |
| 50 | 〃 | `Handle_SecondSuccessForSucceededPayment_ReturnsOutOfOrder` | no save |
| 51 | `…/CompleteFakePayment/CompleteFakePaymentHandlerTests` (modify `Handle_SucceededWhileBaseEntitled_MarksFailedAndThrowsCheckoutPlanAlreadyActive`) | `Handle_SucceededWhileBaseEntitled_RenewsHeldSubscription` | Succeeded, held extended, no Add |
| 52 | 〃 (modify `Handle_PeriodNoLongerConfigured_ThrowsCheckoutPeriodUnavailable`) | `Handle_PeriodNoLongerConfigured_SettlesWithSnapshotMonths` | subscription months = `PeriodMonths` |
| 53 | 〃 (new) | `Handle_FailedPaymentSucceeded_ThrowsPaymentNotPending` | code, no save |
| 54 | `…/StartCheckout/StartCheckoutHandlerTests` (modify `Handle_FreeStudentBaseMonthly_AddsPendingPaymentAtConfiguredPrice`) | same name | also `PeriodMonths == 1` |
| 55 | 〃 (new) | `Handle_BaseInsideRenewalWindow_AddsPendingPayment` | payment added |
| 56 | 〃 (new) | `Handle_GatewayReturnsProviderOrder_StoresProviderOrderId` | `ProviderOrderId` |
| 57 | `…/GetMyEntitlement/GetMyEntitlementHandlerTests` (modify `Handle_EntitledBase_ReturnsUnlimitedQuizzesAndBaseAvatarLimit`: expected `SubscriptionResult` gets `false, false`) | same | — |
| 58 | 〃 (new) | `Handle_BaseInsideRenewalWindow_MarksCanRenew` | `CanRenew` true |
| 59 | 〃 (new) | `Handle_ActiveInsideGrace_MarksInGracePeriod` | `InGracePeriod` true |
| 60 | `…/CancelSubscription/CancelSubscriptionHandlerTests` (new) | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | code |
| 61 | 〃 | `Handle_OtherStudentsSubscription_ThrowsSubscriptionNotFound` | code |
| 62 | 〃 | `Handle_ActiveBase_CancelsAndReturnsEntitlementStillBase` | status Cancelled, result tier Base, save |
| 63 | 〃 | `Handle_AlreadyCancelled_ThrowsSubscriptionEnded` | code |
| 64 | 〃 | `Handle_ActiveLapsedPastGrace_ThrowsSubscriptionEnded` | code |
| 65 | `…/CancelSubscription/CancelSubscriptionValidatorTests` (new) | `Validate_EmptyId_FailsWithSubscriptionIdRequired` / `Validate_Id_Passes` | code / valid |
| 66 | `…/LapseSubscription/LapseSubscriptionHandlerTests` (new) | `Handle_Missing_SavesNothing` | no save |
| 67 | 〃 | `Handle_NotDue_SavesNothing` | Active, no save |
| 68 | 〃 | `Handle_ActivePastEnd_MarksPastDueAndSaves` | PastDue, save |
| 69 | 〃 | `Handle_PastDuePastGrace_ExpiresAndSaves` | Expired |
| 70 | `…/LapseSubscription/LapseSubscriptionValidatorTests` (new) | `Validate_EmptyId_FailsWithSubscriptionIdRequired` / `Validate_Id_Passes` | code / valid |
| 71 | `…/Subscriptions/SubscriptionsOptionsTests` (new) | `AddApplication_RenewalWindowDaysOutOfRange_ThrowsOptionsValidationException` | throws |
| 72 | 〃 (new) | `AddApplication_LapseSweepIntervalBelowMinimum_ThrowsOptionsValidationException` | throws |
| 73 | `Tests/Infrastructure/Payments/PaymobHmacTests` (new) | `Compute_RecordedTransaction_MatchesKnownDigest` | the Python-computed hex (key `paymob-known-answer-key`) |
| 74 | 〃 | `IsValid_UpperCaseSignature_IsTrue` | true |
| 75 | 〃 | `IsValid_TamperedAmount_IsFalse` | false |
| 76 | 〃 | `IsValid_BlankSecret_IsFalse` | false |
| 77 | 〃 | `IsValid_MissingSignature_IsFalse` | false |
| 78 | `Tests/Infrastructure/Payments/PaymobNotificationReaderTests` (new) | `Read_SignedTransaction_MapsFields` | every `PaymentNotification` field |
| 79 | 〃 | `Read_TokenCallback_ReturnsNull` | null |
| 80 | 〃 | `Read_BadSignature_ThrowsSignatureInvalid` | 401 code |
| 81 | 〃 | `Read_EmptyHmacSecret_ThrowsSignatureInvalid` | 〃 (fail closed) |
| 82 | 〃 | `Read_SignatureOnlyInBody_IsAccepted` | notification |
| 83 | 〃 | `Read_MalformedJson_ThrowsPayloadInvalid` | 400 code |
| 84 | 〃 | `Read_SignedWithoutOrder_ThrowsPayloadInvalid` | 400 code |
| 85 | 〃 | `Read_RefundedTransaction_FlagsRefundOrVoid` | true |
| 86 | `Tests/Infrastructure/Payments/PaymobPaymentGatewayTests` (new) | `StartCheckoutAsync_IntentionOrderId_ReturnsProviderOrderId` | `"217503754"` from a numeric value |
| 87 | 〃 (new) | `StartCheckoutAsync_NoIntentionOrderId_ReturnsNullProviderOrderId` | null |
| 88 | `Tests/Infrastructure/Payments/PaymentsOptionsValidatorTests` (modify `Validate_PaymobRequiredValueBlank_FailsNamingKey`: add `[InlineData("HmacSecret")]` and the switch arm that blanks it) | same | failure names `HmacSecret` |
| 89 | `Tests/Infrastructure/Payments/PaymentsServiceCollectionExtensionsTests` (new) | `AddPayments_NoConfiguration_ResolvesPaymobNotificationReader` | type |
| 90 | `Tests/Api/Workers/SubscriptionLapseWorkerTests` (new, mirrors the exam worker tests, reuses `ManualTimeProvider`) | `Sweep_FailingSubscriptions_LogsAndLapsesTheRest` | two Warnings, 3rd lapsed |
| 91 | 〃 | `Sweep_FailedIds_AreSkippedUntilTheBacklogEnds` | excluded-id sequence |
| 92 | 〃 | `Stop_DuringSweep_EndsTheLoopWithoutLogging` | no logs |
| 93 | 〃 | `Execute_Disabled_EndsWithoutSweeping` | `ExecuteTask` completed, `ISender` received nothing |
| 94 | `Tests/Integration/Persistence/AuditChangeCaptureTests` (new) | `SaveChangesAsync_AuditExcludedProperty_IsLeftOutOfTheDiff` | a Payment `MarkFailed` change lists `Status`, not `RawWebhook` |
| 95 | `Tests/Integration/Subscriptions/PaymentPersistenceTests` (modify `SaveChanges_DuplicatePaymobTransactionId_ThrowsUniqueViolation`) | `SaveChanges_DuplicatePaymobTransactionId_ThrowsTransactionAlreadyRecorded` | `ConflictCoreException` code; inner `DbUpdateException` → `PostgresException` constraint `PaymobTransactionIndex` |
| 96 | 〃 (new) | `SaveChanges_StalePayment_ThrowsPaymentModifiedConcurrently` | two scopes, both modify, second → code |
| 97 | 〃 (new) | `SaveChanges_StaleSubscription_ThrowsSubscriptionModifiedConcurrently` | 〃 |
| 98 | `Tests/Integration/Subscriptions/PaymobWebhookEndpointTests` (new, recorded payloads, signed with `ApiFactory.TestPaymobHmacSecret`) | `Post_SignedSuccess_ActivatesBaseAndStoresRawPayload` | 200 `outcome=Succeeded`; tier Base; DB payment Succeeded, `RawWebhook` contains `"merchant_order_id"`, subscription Active |
| 99 | 〃 | `Post_SignedDeclined_MarksFailedAndStudentStaysFree` | Failed, no subscription |
| 100 | 〃 | `Post_SameTransactionTwice_SecondIsDuplicate` | both 200, second `Duplicate`, one subscription |
| 101 | 〃 | `Post_SuccessAfterDeclinedAttempt_ActivatesBase` | Succeeded with the second transaction id |
| 102 | 〃 | `Post_DeclinedAfterSuccess_KeepsSucceeded` | `OutOfOrder`, still Succeeded |
| 103 | 〃 | `Post_SuccessWhileBaseHeld_ExtendsExistingSubscription` | one Base row, end + 1 month, payment `SubscriptionId` = existing |
| 104 | 〃 | `Post_AskTeacherWithoutBase_SucceedsAndFlagsForReview` | `ReviewReason` = `AskTeacherWithoutBase` |
| 105 | 〃 | `Post_InvalidSignature_Returns401AndChangesNothing` | 401 code, payment Pending |
| 106 | 〃 | `Post_MissingSignature_Returns401` | 401 |
| 107 | 〃 | `Post_TokenCallback_Returns200Ignored` | `Ignored` |
| 108 | 〃 | `Post_PendingTransaction_LeavesPaymentPending` | `Ignored`, Pending |
| 109 | 〃 | `Post_AmountMismatch_Returns400AndLeavesPending` | 400 `PAYMENT_NOTIFICATION_MISMATCH` |
| 110 | 〃 | `Post_UnknownMerchantOrder_Returns404` | 404 `PAYMENT_NOT_FOUND` |
| 111 | 〃 | `Post_SignedSuccess_AuditDiffExcludesRawWebhook` | AuditLogs row `Payment.ProcessNotification`, `ResourceId` = payment, Diff contains `"status"` and not `rawWebhook` |
| 112 | `Tests/Integration/Subscriptions/CancelSubscriptionEndpointTests` (new) | `Post_Anonymous_Returns401` | 401 |
| 113 | 〃 | `Post_Teacher_Returns403` | 403 |
| 114 | 〃 | `Post_OwnActiveBase_Returns200AndKeepsAccessUntilPeriodEnd` | 200, tier Base, `subscriptions[0].status=Cancelled`, `entitledUntil = currentPeriodEnd`; DB `CancelledAt` set |
| 115 | 〃 | `Post_OtherStudentsSubscription_Returns404` | `SUBSCRIPTION_NOT_FOUND` |
| 116 | 〃 | `Post_AlreadyCancelled_Returns400SubscriptionEnded` | code |
| 117 | `Tests/Integration/Subscriptions/SubscriptionLapseSweepTests` (new, mediator from a scope) | `Send_GetLapsedIds_ReturnsDueSubscriptionsOnly` | due Active / PastDue / Cancelled ids in the result, a current one absent |
| 118 | 〃 | `Send_GetLapsedIds_SkipsExcludedIds` | excluded absent |
| 119 | 〃 | `Send_LapseSubscription_PastGrace_PersistsExpired` | DB Expired, `ExpiredAt = end + grace` |
| 120 | `Tests/Integration/Subscriptions/CheckoutEndpointTests` (new) | `Post_BaseInsideRenewalWindow_ReturnsRedirect` | 200, Pending payment stored |
| 121 | `Tests/Integration/Subscriptions/EntitlementEndpointTests` (new) | `Get_BaseInsideRenewalWindow_ReportsCanRenew` | `canRenew` true |
| 122 | `Tests/Integration/Subscriptions/FakePaymentCompletionEndpointTests` (modify `Post_SecondBaseCheckoutAfterFirstSucceeded_Returns400AndMarksFailed`) | `Post_SecondBaseCheckoutAfterFirstSucceeded_ExtendsExistingBase` | 200 Succeeded, one Base row, end extended by 1 month |

Mechanical Arrange-only edits: `GetMyPaymentHandlerTests`, `GetMyPaymentsHandlerTests`, `CompleteFakePaymentHandlerTests` (helper), `SubscriptionTestData`, `PaymentSettlementTests` field and `StartCheckoutHandlerTests` (if they build a `PaymentCheckout`). No assertion changes other than the rows above.

Web (Vitest + RTL + MSW; `renderApp`; `userEvent.setup()`):
| # | File | `it(...)` | Asserts |
|---|---|---|---|
| W1 | `SubscriptionPage.manage.test.tsx` (new) | `shows renew buttons for each Base period when renewal is open` | three Renew buttons in the Base article, no Subscribe |
| W2 | 〃 | `starts a Base renewal checkout for the chosen period` | captured body `{plan:'Base', period:'Yearly'}`; navigates to the fake checkout |
| W3 | 〃 | `shows a renew button for Ask a Teacher when its renewal is open` | "Renew" in the Ask a Teacher article |
| W4 | 〃 | `cancels a plan after confirming in the dialog` | dialog title "Cancel Base?"; confirm; toast "Subscription cancelled"; line shows "cancelled, available until" (MSW cancel returns a Cancelled entitlement) |
| W5 | 〃 | `keeps the plan when the dialog is dismissed` | "Keep subscription" closes the dialog; the line still shows "active until" |
| W6 | 〃 | `shows an error toast when cancelling fails` | 400 `SUBSCRIPTION_ENDED` → the localised toast |
| W7 | 〃 | `hides the cancel button for a cancelled plan` | `queryByRole('button', { name: 'Cancel Base' })` is null |
| W8 | 〃 | `shows the grace wording for an active plan past its period end` | "period ended, available until" |
| W9 | 〃 | `renders the cancel dialog right-to-left in Arabic` | `dir="rtl"`, Arabic title |
| W10 | 〃 | `has no axe violations with the cancel dialog open` | axe clean |

Existing web tests stay unedited. Their fixtures default `canRenew`/`inGracePeriod` to false, so behaviour is unchanged.

## Definition of done
- [ ] Every sub-task in the story maps to shipped code and tests (Scope "In").
- [ ] A webhook with a valid HMAC (Decision 6 field order) changes entitlement. An invalid, missing or empty-secret signature returns 401 and changes nothing.
- [ ] The known-answer HMAC test uses a digest computed outside the C# code.
- [ ] A duplicate transaction id is a 200 no-op. Concurrent writes return 409 via `xmin` on Payment and Subscription. The unique transaction index maps to 409.
- [ ] A success wins over an earlier decline (Failed → Succeeded). A decline after a success is ignored.
- [ ] Matching uses `merchant_order_id`, then `ProviderOrderId`. Amount, currency and provider order are bound, and a mismatch returns 400.
- [ ] Settlement extends a held plan (`Renew`: from the old end, switches period, resumes Cancelled, rejects when not entitled). An AskTeacher payment without Base is flagged.
- [ ] Fake completion uses the same `PaymentSettlement`, and its 400 conflict branch is gone.
- [ ] `Payment.PeriodMonths` is snapshotted, and the migration backfills it. Settlement never calls `PriceFor`.
- [ ] `RawWebhook` is stored in full and never appears in an audit diff (the `Core:AuditExcluded` annotation, with a test).
- [ ] Checkout accepts a held plan from `CurrentPeriodEnd − RenewalWindowDays`. `SubscriptionResult.CanRenew` and `InGracePeriod` are exposed.
- [ ] Student cancel endpoint: owner-scoped (404), policy `SubscriptionManage`, keeps paid time, audited.
- [ ] `SubscriptionLapseWorker` mirrors the #81 worker (catch filter, deferred ids) and is disabled in `ApiFactory`. Lapse transitions and `ExpiredAt` match Decision 18. The SQL spec agrees with `Lapse` (grid test).
- [ ] New config keys are in `SubscriptionsOptions`/`PaymobOptions` with safe code defaults, `appsettings.example.json`, `ApiFactory` and `.env.example`. `HmacSecret` is required with Paymob.
- [ ] 8 error codes, with en and ar strings in both resx files. The web common errors add the 3 cancel-reachable codes.
- [ ] Migration `AddPaymentWebhookState` is added and appended to `AppDbContextTests`.
- [ ] `api/openapi/v1.json` and the Orval client are regenerated. The webhook is absent from OpenAPI. Postman is updated in order.
- [ ] Web: Renew buttons, cancel dialog (Danger, focus-trapped), grace wording, en and ar keys, tokens only, logical properties.
- [ ] Docs updated: `subscriptions.md`, `paymob.md` (§8 webhook, verification list), `audit-log.md`, PRD §11.2, design-prompt §4. No divergence left.
- [ ] Every test in the Test plan exists with the listed name. The only existing tests changed are the rows marked modify and the mechanical Arrange edits.
- [ ] `dotnet test api/ -c Release` passes with `appsettings.json` moved aside. `npm --prefix web run typecheck`, `lint` and `test` pass, plus prettier `--end-of-line auto`.
