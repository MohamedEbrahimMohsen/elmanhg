# Plan — Refunds and payment log (#102, E10.S4)

## Goal
An admin can open `/admin/payments`, see every transaction (all statuses) with filters and a "needs review" queue, refund a successful payment through Paymob (real adapter behind `Payments:Provider`, fake by default) with a required reason, or close a review by keeping the payment. A refund is recorded locally, removes the paid time of that payment from the student's subscription at once, and is audited. Signed Paymob refund/void callbacks (child transactions) now reverse a payment too, classified only from HMAC-signed fields; the unsigned `is_refund`/`is_void` flags are never read (#191 item).

## Scope
**In:**
- Domain: `PaymentStatus.Refunded`, refund and review-resolution state on `Payment`, `Subscription.RevokePaidPeriod`, `PaymentReviewReason.PartialRefundAtProvider`.
- `IPaymentGateway.RefundAsync`: fake (default) and real Paymob adapter (`POST api/acceptance/void_refund/refund`).
- `RefundPayment` command (Idempotency-Key header, audited `Payment.Refund`), `ResolvePaymentReview` command (audited `Payment.ResolveReview`), `GetPaymentLog` query with filters and the review queue.
- Webhook: signed-only classification (`Charge` / `Reversal` / `Other`); full reversal → Refunded + revoke; partial → review flag; reversal of a Pending payment → 409 so Paymob retries.
- Migration `AddPaymentRefunds`; new policy `Payments.Manage` (Admin).
- Web: `features/payments` admin page, nav item, capability; student payment log and checkout result show "Refunded".
- Docs: PRD, subscriptions.md, paymob.md, audit-log.md, claude-design-prompt.md, prototype.md; Postman; OpenAPI; Orval client.
- #191 small items handled here: currency-term binding test; unsigned `is_refund`/`is_void`; PRD §15 Payment line.

**Out:**
- Partial refunds started by the admin (full amount only).
- Admin payment detail page or export; revenue and refund charts (#104/#105).
- Filtering by student name or phone (filter is `studentId`; #106 will link students to `/admin/payments?studentId=`).
- Changes to `prototype/app.js` (earlier stories updated the docs only).

**Deferred:**
- Live check of the Paymob refund API and the refund/void callback shape. No merchant credentials are available. The adapter is built and tested against a stub handler. Add to the `docs/paymob.md` §5 list; tracked in the existing #191 item.
- Automatic detection of overlapping subscriptions (the known limit in docs/subscriptions.md → Webhook). Removing the race needs a per-student settlement lock (raw-SQL advisory lock plus an explicit transaction in the #101 webhook path). That is a cross-cutting change outside this story's three sub-tasks. New follow-up issue.
- Playwright pay-and-refund journey. The repo has no e2e setup (same reason as #189).

## Decisions
| # | Question | Decision | Why |
|---|---|---|---|
| 1 | Who may refund and see the log | New policy `DefaultCodes.PaymentsManage = "Payments.Manage"`, Admin only, used by all three endpoints. PRD §16 gains the row "Payment log and refunds – – ✓". | PRD §11.2 says "refunds initiated by admin". §16 "Dashboards / finance" is Admin, but its policy is a *View* name; a mutation needs its own capability. |
| 2 | Full or partial refund | Full amount only. The request carries no amount. | PRD names no partial refunds, and entitlement revocation is defined per payment. |
| 3 | Effect on entitlement | `Subscription.RevokePaidPeriod(payment.PeriodMonths, now)` on the payment's subscription: the end moves back by the payment's months. If the new end ≤ now, the subscription becomes Expired at `now` (no grace after a refund). An already Expired subscription is untouched (returns false). | Refunding a first purchase or a duplicate subscription ends access. Refunding a renewal returns the student to the previously paid end. The month clamp (#191) can cost up to 3 days; documented. |
| 4 | When the refund takes effect | On Paymob's synchronous success response to the admin refund call (authenticated with our secret key, server-to-server), in the same save as the local record. The signed refund callback that follows is a `Duplicate`. PRD §17 rule 12 and §11.2 are amended: "entitlement changes only through Paymob-verified events: HMAC-verified webhooks, or Paymob's response to an admin refund. The client never sets entitlement." | PRD §11.2 says "recorded locally, executed in Paymob". Waiting for the callback would leave the admin with a pending state and no feedback. The signed callback stays the safety net (see 7). |
| 5 | Provider decline / pending / outage | Adapter: transport error, timeout, 5xx or 429 → 503 `PAYMENT_GATEWAY_UNAVAILABLE`. Another 4xx, `success != true`, or `pending == true` → 400 `PAYMENT_REFUND_DECLINED`. Nothing is saved in either case. | Mirrors the checkout adapter. A pending refund that completes later arrives as a signed child callback and is applied then. |
| 6 | Idempotency (momenta §8) | Required header `Idempotency-Key` (UUID). Stored as `Payment.RefundIdempotencyKey`. If the payment is already Refunded with the same key → 200 replay of the current result (no gateway call, no save). With a different key → 400 `PAYMENT_ALREADY_REFUNDED`. Missing or empty key → 422 `PAYMENT_REFUND_IDEMPOTENCY_KEY_REQUIRED` (repo convention: validation is 422). No `Idempotent-Replayed` header and no in-progress 409: concurrent same-key calls race on `xmin` (409 `PAYMENT_MODIFIED_CONCURRENTLY`), and Paymob refuses a second full refund. | The key is scoped to one payment, and refund is a single state transition, so no idempotency table is needed. Documented deviation. |
| 7 | Classifying callbacks (#191) | Only HMAC-signed fields decide. `has_parent_transaction` true and `is_capture` false → `Reversal`. `has_parent_transaction` true and `is_capture` true → `Other`. No parent, but `is_refunded` or `is_voided` true → `Other` (the child carries the reversal). Otherwise → `Charge`. `is_refund` and `is_void` are never read. | All four fields are in the Paymob HMAC field set. The unsigned flags could be forged onto a replayed signed body. |
| 8 | Reversal outcomes | A failed or pending child → `Ignored`. A child whose id is already recorded as `RefundTransactionId` → `Duplicate`. Payment Refunded → `Duplicate`. Pending → 409 `PAYMENT_NOT_SETTLED` (Paymob retries after the charge settles). Failed → `OutOfOrder`. Amount ≤ 0 or > payment amount, a currency mismatch, or a provider-order mismatch → 400 `PAYMENT_NOTIFICATION_MISMATCH`. Amount < payment amount → `FlagForReview(PartialRefundAtProvider)`, outcome `FlaggedForReview`, no entitlement change. Full amount → `PaymentRefundSettlement.Apply`, outcome `Refunded`. | Covers dashboard-initiated refunds and admin refunds whose local save failed. Partial refunds cannot be mapped to paid time, so a human decides. |
| 9 | Late success for a Refunded payment | `MarkSucceeded` refuses `Refunded` as well (`PAYMENT_NOT_PENDING`); the webhook charge path returns `OutOfOrder` for `Succeeded` or `Refunded`. | A refunded payment must never re-grant time. |
| 10 | Review queue | Open review = `ReviewReason != null && ReviewResolvedAt == null` (`Payment.NeedsReview`). Closed by a refund (same actor and time) or by "Keep payment" (`ResolvePaymentReview`). `FlagForReview` reopens (clears the resolution). The queue is the log with `needsReview=true`. | One list endpoint (momenta §7 filters) and no second read model. |
| 11 | Log contents and order | All statuses, including Pending (admin view). Order `CreationDate` desc, then `Id` desc. Offset paging `pageNumber`/`pageSize`, max `Subscriptions:AdminPaymentLogMaxPageSize` (100). | Admin grid with page jumps (momenta §7). Mirrors `GetAuditLogs`. |
| 12 | Filters | `status`, `plan`, `needsReview` (bool, default false), `studentId`, `reference` (exact match on payment id, `PaymobTransactionId`, `RefundTransactionId` or `ProviderOrderId`; ≤ `PaymentLogReferenceMaxLength` 100), `from` (inclusive) / `to` (exclusive) on `CreationDate`. | Explicit per-field params (momenta §7). The reference covers dispute lookups. |
| 13 | Student info in the log | `GetPaymentLog` loads the page, then batch-loads users with `IUserRepository.FindAsync(ids)`. `StudentName = DisplayName`, `StudentContact = Email ?? PhoneNumber`. | Same pattern as `GetQuestionsHandler`. No cross-aggregate repository method. |
| 14 | Where the code lives | Admin use cases in the new area `Application/Payments/` (entity stays in `Domain/Subscriptions`). `PaymentRefundSettlement` sits beside `PaymentSettlement` in `Application/Subscriptions/Shared` because the webhook uses it too. | Mirrors the existing split (subscription student flows vs admin flows). |
| 15 | Routes | `GET /api/payments`, `POST /api/payments/{paymentId:guid}/refund`, `POST /api/payments/{paymentId:guid}/review-resolution` in the new `PaymentsController`. The `:guid` constraint keeps them apart from `api/payments/paymob/webhook`. | Repo action style (`/cancel`, `/fake-completion`). |
| 16 | Refund reason | Required, trimmed, ≤ `Subscriptions:RefundReasonMaxLength` (500). Stored as `text`. Shown in the log. | Morabh `RefundOrderDownPaymentValidator` requires `RefundReason`. The audit diff then carries the why. |
| 17 | Fake refund | Returns `PaymentRefund("fake-refund-{paymentId:N}")`. In Production → 503 `PAYMENT_GATEWAY_UNAVAILABLE`. | Same production lock as fake checkout. |
| 18 | Paymob refund request | `POST {BaseUrl}/api/acceptance/void_refund/refund`, `Authorization: Token <SecretKey>`, `User-Agent: Elmanhg/1.0`, body `{ "transaction_id": "<PaymobTransactionId>", "amount_cents": <AmountMinor> }`. No retries (existing `DisableForUnsafeHttpMethods`). Response read: `id` (number or string), `success`, `pending`. Added to the §5 verify list. | The current Paymob refund endpoint accepts secret-key token auth. No new config keys. |
| 19 | Void | No separate void API call. Paymob may require a void for same-day card transactions; that shows as `PAYMENT_REFUND_DECLINED` and goes on the verify list. | Keeps one path; void needs live verification. |
| 20 | Money on the wire | Existing `Money { amountMinor, currency }`. | subscriptions.md → Money (momenta §6, integer minor units). |
| 21 | Enum wire values | PascalCase strings (`"Refunded"`, `"PartialRefundAtProvider"`). | Repo convention (#99 decision 22). |
| 22 | Refund transaction id uniqueness | New unique filtered index `IX_Payments_RefundTransactionId`. A violation maps to 409 `PAYMENT_TRANSACTION_ALREADY_RECORDED` (existing code, same meaning). | Webhook idempotency key for reversals. |
| 23 | Admin page (no prototype screen) | `/admin/payments`: H1 "المدفوعات", pill sub-tabs "كل المعاملات" / "بحاجة لمراجعة" with a count, a filter form, a table, and dialogs for refund and keep. Documented in claude-design-prompt §4 and prototype.md. | The prototype has no admin payments screen; the audit page pattern (#58) is the closest. |
| 24 | Review count on the tab | Second request `GetPaymentLog(needsReview=true, pageSize=1)` → `totalItems`. | No new endpoint. |
| 25 | `TextAreaField` | Moved from `features/questions/components/` to `shared/form/TextAreaField.tsx` (second use appears); three questions imports updated. | react-feature §6.7. |
| 26 | Orval headers | `output.headers: true` in `web/orval.config.ts`, so the generated refund function accepts the `Idempotency-Key` header. Add `GetPaymentLog` to the zod `query: false` overrides. | Without it Orval drops header parameters. |
| 27 | Student side | The student log already lists non-Pending payments, so Refunded appears with a neutral badge "مستردة". The checkout result page gets a Refunded branch. `PaymentResult` shape is unchanged. | A new enum value must render everywhere it can appear. |

## Existing code touched
| File | Change |
|---|---|
| `api/Elmanhg.Domain/Subscriptions/Payment.cs` | `public partial class Payment`. `MarkSucceeded` guard becomes `if (Status is PaymentStatus.Succeeded or PaymentStatus.Refunded)`. `FlagForReview` also sets `ReviewResolvedAt = null; ReviewResolvedBy = null;`. |
| `api/Elmanhg.Domain/Subscriptions/PaymentStatus.cs` | `public enum PaymentStatus { Pending, Succeeded, Failed, Refunded }` |
| `api/Elmanhg.Domain/Subscriptions/PaymentReviewReason.cs` | `{ AskTeacherWithoutBase, PartialRefundAtProvider }` |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Under `// SUBSCRIPTIONS`: `PaymentAlreadyRefunded`, `PaymentNotRefundable`, `PaymentReviewNotOpen`. |
| `api/Elmanhg.Domain/SharedKernel/DefaultCodes.cs` | `public const string PaymentsManage = "Payments.Manage";` after `SubscriptionManage`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Under `// SUBSCRIPTIONS`: the 12 application codes in the Error codes table. |
| `api/Elmanhg.Application/Shared/Options/SubscriptionsOptions.cs` | `[Range(1, 100)] public int AdminPaymentLogMaxPageSize { get; set; } = 100;` `[Range(1, 2000)] public int RefundReasonMaxLength { get; set; } = 500;` `[Range(1, 100)] public int PaymentLogReferenceMaxLength { get; set; } = 100;` |
| `api/Elmanhg.Application/Shared/Payments/IPaymentGateway.cs` | Add `Task<PaymentRefund> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken);` |
| `api/Elmanhg.Application/Shared/Payments/PaymentNotification.cs` | `public sealed record PaymentNotification(string TransactionId, string? MerchantOrderId, string? ProviderOrderId, bool Succeeded, bool Pending, PaymentNotificationKind Kind, long AmountMinor, string Currency);` |
| `api/Elmanhg.Application/Subscriptions/Shared/PaymentNotificationOutcome.cs` | `{ Succeeded, MarkedFailed, Duplicate, Ignored, OutOfOrder, Refunded, FlaggedForReview }` |
| `api/Elmanhg.Application/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationHandler.cs` | `public sealed partial class`. Step 1: `if (notification is null \|\| notification.Pending \|\| notification.Kind == PaymentNotificationKind.Other) return Ignored`. Step 2: `if (notification.Kind == PaymentNotificationKind.Reversal) return await ReverseAsync(notification, cancellationToken).ConfigureAwait(false);`. The binding check becomes `if (!IsBound(payment, notification) \|\| payment.AmountMinor != notification.AmountMinor) throw BadRequestCoreException(PaymentNotificationMismatch)`. Add `private static bool IsBound(Payment payment, PaymentNotification notification) => payment.Currency == notification.Currency && (payment.ProviderOrderId is null \|\| payment.ProviderOrderId == notification.ProviderOrderId);`. The success-path guard becomes `payment.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded` → OutOfOrder. |
| `api/Elmanhg.Infrastructure/Payments/FakePaymentGateway.cs` | Add `RefundAsync` (Decision 17). |
| `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobPaymentGateway.cs` | `public sealed partial class PaymobPaymentGateway`. No other change. |
| `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobNotificationReader.cs` | Remove `RefundOrVoidProperties`. Add constants `HasParentProperty = "has_parent_transaction"`, `IsCaptureProperty = "is_capture"`, `IsRefundedProperty = "is_refunded"`, `IsVoidedProperty = "is_voided"`. Add `private static PaymentNotificationKind KindOf(JsonElement transaction)` (Decision 7), with the one-line WHY comment "Only HMAC-signed fields classify a callback; is_refund/is_void are unsigned." Add `private static bool OptionalFlag(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;`. `ToNotification` passes `KindOf(transaction)`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `public const string PaymentRefundTransactionIndex = "IX_Payments_RefundTransactionId";` `public const string PaymentOpenReviewIndex = "IX_Payments_OpenReview";`. In the Payment config: `RefundTransactionId.HasMaxLength(PaymobReferenceMaxLength)`; `HasIndex(x => x.RefundTransactionId, PaymentRefundTransactionIndex).IsUnique().HasFilter("\"RefundTransactionId\" IS NOT NULL")`; `HasIndex(x => x.CreationDate)`; `HasIndex(x => x.CreationDate, PaymentOpenReviewIndex).HasFilter("\"ReviewReason\" IS NOT NULL AND \"ReviewResolvedAt\" IS NULL")`; `builder.Ignore(x => x.NeedsReview); builder.Ignore(x => x.IsRefundable);`. In `SaveChangesAsync`, the existing unique-violation catch matches `ConstraintName: PaymobTransactionIndex or PaymentRefundTransactionIndex`. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add AddPaymentRefunds`. |
| `api/Elmanhg.Api/Authorization/PermissionMatrixPolicies.cs` | `.AddPolicy(DefaultCodes.PaymentsManage, policy => policy.RequireRole(Admin))` |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | 15 keys (Error codes table). |
| `api/Elmanhg.Api/appsettings.example.json` | `Subscriptions`: add `"AdminPaymentLogMaxPageSize": 100, "RefundReasonMaxLength": 500, "PaymentLogReferenceMaxLength": 100`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add the same three `Subscriptions:*` keys to the settings dictionary. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `twentySecond => twentySecond.Should().EndWith("_AddPaymentRefunds")`. |
| `api/Elmanhg.Tests/Integration/Authorization/PermissionMatrixPolicyTests.cs` | Add rows `{ "Payments.Manage", "Student", false }, { "Payments.Manage", "Teacher", false }, { "Payments.Manage", "Admin", true }`. |
| `api/Elmanhg.Tests/Domain/Subscriptions/PaymentTests.cs`, `SubscriptionTests.cs` | Add tests (Test plan). |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationHandlerTests.cs` | Replace every positional `false` in the 6th `PaymentNotification` argument with `PaymentNotificationKind.Charge`. `Notify(...)` parameter `bool refundOrVoid = false` becomes `PaymentNotificationKind kind = PaymentNotificationKind.Charge`. Rename and change `Handle_RefundOrVoid_ReturnsIgnored` → `Handle_OtherKind_ReturnsIgnored` (kind `Other`). Add 2 tests (Test plan). |
| `api/Elmanhg.Tests/Infrastructure/Payments/PaymobNotificationReaderTests.cs` | `Read_SignedTransaction_MapsFields` expects `PaymentNotificationKind.Charge`. Replace `Read_RefundedTransaction_FlagsRefundOrVoid` with `Read_RefundedParentTransaction_IsOther`. Add 4 tests. |
| `api/Elmanhg.Tests/Infrastructure/Payments/FakePaymentGatewayTests.cs` | Add 2 tests. |
| `api/Elmanhg.Tests/Integration/Subscriptions/PaymentPersistenceTests.cs` | Add 1 test. |
| `api/Elmanhg.Tests/Integration/Subscriptions/PaymentHistoryEndpointTests.cs` | Add 1 test. |
| `api/Elmanhg.Tests/Fixtures/Paymob/PaymobPayloads.cs` | `public const string RefundChild = "transaction-refund.json";` |
| `web/orval.config.ts` | `output.headers: true`; `apiZod.output.override.operations.GetPaymentLog: { zod: { generate: { query: false } } }`. |
| `web/src/shared/api/generated/**` | Regenerated with `npm --prefix web run gen:api` (new `payments/` folder; `PaymentStatus` gains `Refunded`). |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (`npx vite build`). |
| `web/src/app/i18n.ts` | Import `paymentsLocales` from `@/features/payments/locales`; add `payments` to `ar`, `en` and `ns`. |
| `web/src/features/session/permissions.ts` | Add `'paymentsManage'` to `capabilities` (after `'subscriptionManage'`) and to the admin list. |
| `web/src/features/session/permissions.test.ts` | Add 1 test. |
| `web/src/features/shell/navConfig.ts` | Admin item after `users`: `{ key: 'payments', to: '/admin/payments', labelKey: 'nav.admin.payments', icon: CreditCard, capability: 'paymentsManage' }`. |
| `web/src/features/shell/i18n/en.json` / `ar.json` | `nav.admin.payments`: "Payments" / "المدفوعات". |
| `web/src/features/shell/pages/MorePage.test.tsx` | Expected list becomes `['Exam blueprints', 'Users', 'Payments', 'Audit log', 'Data export']`. |
| `web/src/shared/i18n/en.json` / `ar.json` | `errors.<CODE>` for all 15 new codes (same strings as the resx). |
| `web/src/features/subscription/components/PaymentHistoryTable.tsx` | Replace the ternary with `const statusClasses: Record<PaymentStatus, string> = { Succeeded: 'bg-success text-surface', Failed: 'bg-danger text-surface', Refunded: 'bg-soft text-text-muted', Pending: 'bg-soft text-text-muted' }`. Drop `text-surface` from the base class. |
| `web/src/features/subscription/components/CheckoutStatusCard.tsx` | Branch before Failed: `payment.status === 'Refunded'` → title `checkoutResult.refundedTitle`, body `checkoutResult.refundedBody`, `backLink('secondary', t('checkoutResult.done'))`. |
| `web/src/features/subscription/i18n/en.json` / `ar.json` | `payments.Refunded`: "Refunded" / "مستردة". `checkoutResult.refundedTitle`: "This payment was refunded" / "تم استرداد هذا الدفع". `checkoutResult.refundedBody`: "The amount was returned to you." / "أعيد المبلغ إليك." |
| `web/src/features/subscription/pages/SubscriptionPage.payments.test.tsx`, `CheckoutResultPage.test.tsx` | Add 1 test each. |
| `web/src/features/questions/components/FillBlanksField.tsx`, `ShortAnswerFields.tsx`, `ValidationDecisionPanel.tsx` | Import `TextAreaField` from `@/shared/form/TextAreaField`. |
| `web/src/features/questions/components/TextAreaField.tsx` | Deleted (moved; content unchanged). |
| `postman/elmanhg.postman_collection.json` | New folder `AdminPayments` after `AuditLogs` (Postman section). |
| `docs/PRD.md`, `docs/subscriptions.md`, `docs/paymob.md`, `docs/audit-log.md`, `docs/claude-design-prompt.md`, `docs/prototype.md` | Docs section. |

## Files to create
| # | Path | Type | Contract |
|---|---|---|---|
| 1 | `api/Elmanhg.Domain/Subscriptions/Payment.Refund.cs` | partial entity | `namespace Elmanhg.Domain.Subscriptions; public partial class Payment`. Properties (all `{ get; private set; }`): `DateTimeOffset? RefundedAt`, `Guid? RefundedBy`, `string? RefundReason`, `string? RefundTransactionId`, `Guid? RefundIdempotencyKey`, `DateTimeOffset? ReviewResolvedAt`, `Guid? ReviewResolvedBy`. Computed: `public bool NeedsReview => ReviewReason is not null && ReviewResolvedAt is null;` `public bool IsRefundable => Status == PaymentStatus.Succeeded && PaymobTransactionId is not null;` `public bool IsRefundReplay(Guid? idempotencyKey) => Status == PaymentStatus.Refunded && idempotencyKey is not null && RefundIdempotencyKey == idempotencyKey;`. Methods `EnsureRefundable()`, `MarkRefunded(string refundTransactionId, DateTimeOffset refundedAt, Guid? refundedBy, string? reason, Guid? idempotencyKey)`, `ResolveReview(Guid resolvedBy, DateTimeOffset resolvedAt)` (Domain behaviour). |
| 2 | `api/Elmanhg.Domain/Subscriptions/Subscription.Refund.cs` | partial entity | `public partial class Subscription { public bool RevokePaidPeriod(int periodMonths, DateTimeOffset revokedAt) }` (Domain behaviour). |
| 3 | `api/Elmanhg.Application/Shared/Payments/PaymentRefundRequest.cs` | record | `namespace Elmanhg.Application.Shared.Payments; public sealed record PaymentRefundRequest(Guid PaymentId, string ProviderTransactionId, Money Amount);` |
| 4 | `api/Elmanhg.Application/Shared/Payments/PaymentRefund.cs` | record | `public sealed record PaymentRefund(string TransactionId);` |
| 5 | `api/Elmanhg.Application/Shared/Payments/PaymentNotificationKind.cs` | enum | `public enum PaymentNotificationKind { Charge, Reversal, Other }` |
| 6 | `api/Elmanhg.Application/Subscriptions/Shared/PaymentRefundSettlement.cs` | static class | `public static class PaymentRefundSettlement { public static void Apply(Payment payment, Subscription? subscription, string refundTransactionId, DateTimeOffset refundedAt, Guid? refundedBy, string? reason, Guid? idempotencyKey) }`. Body: `payment.MarkRefunded(refundTransactionId, refundedAt, refundedBy, reason, idempotencyKey);` then `subscription?.RevokePaidPeriod(payment.PeriodMonths, refundedAt);`. Touches no repository. |
| 7 | `api/Elmanhg.Application/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationHandler.Reversal.cs` | partial handler | `public sealed partial class ProcessPaymentNotificationHandler { private async Task<PaymentNotificationResult> ReverseAsync(PaymentNotification notification, CancellationToken cancellationToken) }`. Steps: (1) `!notification.Succeeded` → `new(null, Ignored)`. (2) `paymentRepository.FirstOrDefaultAsync(x => x.RefundTransactionId == notification.TransactionId, ct, asNoTracking: true)` not null → `Duplicate` with its id. (3) `FindPaymentAsync(notification, ct) ?? throw new NotFoundCoreException(ErrorCodes.PaymentNotFound)`. (4) `!IsBound(payment, notification) \|\| notification.AmountMinor <= 0 \|\| notification.AmountMinor > payment.AmountMinor` → `BadRequestCoreException(PaymentNotificationMismatch)`. (5) Status switch: `Refunded` → `Duplicate`; `Pending` → `throw new ConflictCoreException(ErrorCodes.PaymentNotSettled)`; `Failed` → `OutOfOrder`. (6) `var now = timeProvider.GetUtcNow();` (7) If `notification.AmountMinor < payment.AmountMinor`: `payment.FlagForReview(PaymentReviewReason.PartialRefundAtProvider)`; save; return `FlaggedForReview`. (8) `var subscription = payment.SubscriptionId is { } subscriptionId ? await subscriptionRepository.FirstOrDefaultAsync(x => x.Id == subscriptionId, ct).ConfigureAwait(false) : null;` then `PaymentRefundSettlement.Apply(payment, subscription, notification.TransactionId, now, null, null, null)`; `paymentRepository.SaveChangesAsync` once; return `Refunded`. |
| 8 | `api/Elmanhg.Application/Payments/Shared/AdminPaymentResult.cs` | record (admin-facing, no localisation) | `namespace Elmanhg.Application.Payments.Shared; public sealed record AdminPaymentResult(Guid Id, Guid StudentId, string StudentName, string? StudentContact, SubscriptionPlan Plan, BillingPeriod Period, int PeriodMonths, Money Amount, PaymentStatus Status, string? PaymobTransactionId, Guid? SubscriptionId, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, PaymentReviewReason? ReviewReason, bool NeedsReview, DateTimeOffset? ReviewResolvedAt, DateTimeOffset? RefundedAt, string? RefundReason, string? RefundTransactionId, bool CanRefund);` |
| 9 | `api/Elmanhg.Application/Payments/Shared/AdminPaymentResultGenerator.cs` | static | `public static AdminPaymentResult Generate(Payment payment, User? student)`: `StudentName = student?.DisplayName ?? string.Empty`, `StudentContact = student?.Email ?? student?.PhoneNumber`, `CreatedAt = payment.CreationDate`, `CanRefund = payment.IsRefundable`, other fields one-to-one. |
| 10 | `api/Elmanhg.Application/Payments/GetPaymentLog/GetPaymentLogQuery.cs` | query | `public sealed record GetPaymentLogQuery(PaymentStatus? Status, SubscriptionPlan? Plan, bool NeedsReview, Guid? StudentId, string? Reference, DateTimeOffset? From, DateTimeOffset? To, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<AdminPaymentResult>>;` |
| 11 | `.../GetPaymentLog/GetPaymentLogValidator.cs` | validator (`IOptions<SubscriptionsOptions>`) | `PageNumber.ValidateMin(1, PaymentLogPageNumberInvalid)`; `PageSize.ValidateRange(1, options.AdminPaymentLogMaxPageSize, PaymentLogPageSizeInvalid)`; `Status.IsInEnum().WithErrorCode(PaymentLogStatusInvalid)`; `Plan.IsInEnum().WithErrorCode(PaymentLogPlanInvalid)`; `Reference.ValidateMaxLength(options.PaymentLogReferenceMaxLength, PaymentLogReferenceTooLong)`; `RuleFor(x => x).Must(x => x.From is null \|\| x.To is null \|\| x.From < x.To).WithErrorCode(PaymentLogDateRangeInvalid)`. |
| 12 | `.../GetPaymentLog/GetPaymentLogFilter.cs` | static | `public static Expression<Func<Payment, bool>> Build(GetPaymentLogQuery query)`. `reference = IsNullOrWhiteSpace ? null : Trim()`; `Guid? referenceId = Guid.TryParse(reference, out var id) ? id : null`; `from`/`to` via `ToUniversalTime()`. Predicate: `(status == null \|\| x.Status == status) && (plan == null \|\| x.Plan == plan) && (!needsReview \|\| (x.ReviewReason != null && x.ReviewResolvedAt == null)) && (studentId == null \|\| x.StudentId == studentId) && (reference == null \|\| x.Id == referenceId \|\| x.PaymobTransactionId == reference \|\| x.RefundTransactionId == reference \|\| x.ProviderOrderId == reference) && (from == null \|\| x.CreationDate >= from) && (to == null \|\| x.CreationDate < to)`. |
| 13 | `.../GetPaymentLog/GetPaymentLogHandler.cs` | handler `(IPaymentRepository paymentRepository, IUserRepository userRepository)` | (1) `FindPaginatedAsync(PageNumber, PageSize, ct, filter: GetPaymentLogFilter.Build(request), orderBy: q => q.OrderByDescending(x => x.CreationDate).ThenByDescending(x => x.Id), asNoTracking: true)`. (2) Distinct `StudentId`s → `userRepository.FindAsync(x => ids.Contains(x.Id), ct, asNoTracking: true)` → dictionary. (3) Map with `AdminPaymentResultGenerator.Generate(payment, users.GetValueOrDefault(payment.StudentId))` into `PageData` (copy paging fields as in `GetAuditLogsHandler`). |
| 14 | `api/Elmanhg.Application/Payments/RefundPayment/RefundPaymentCommand.cs` | command | `public sealed record RefundPaymentCommand(Guid PaymentId, string? Reason, Guid? IdempotencyKey) : IRequest<AdminPaymentResult>, IAuditableCommand { AuditAction => "Payment.Refund"; AuditResourceType => "Payment"; AuditResourceId => PaymentId; }` |
| 15 | `.../RefundPayment/RefundPaymentValidator.cs` | validator (`IOptions<SubscriptionsOptions>`) | `PaymentId.ValidateRequired(PaymentIdRequired)`; `Reason.ValidateRequired(PaymentRefundReasonRequired).ValidateMaxLength(options.RefundReasonMaxLength, PaymentRefundReasonTooLong)`; `IdempotencyKey.ValidateRequired(PaymentRefundIdempotencyKeyRequired).Must(x => x != Guid.Empty).WithErrorCode(PaymentRefundIdempotencyKeyRequired)`. |
| 16 | `.../RefundPayment/RefundPaymentHandler.cs` | handler `(IPaymentRepository paymentRepository, ISubscriptionRepository subscriptionRepository, IUserRepository userRepository, IPaymentGateway paymentGateway, ICurrentUserService currentUserService, TimeProvider timeProvider)` | (1) Null or default `UserId` → `UnauthorizedCoreException(UserNotAuthenticated)`. (2) `payment = await paymentRepository.FirstOrDefaultAsync(x => x.Id == request.PaymentId, ct) ?? throw NotFoundCoreException(PaymentNotFound)` (tracked). (3) `payment.IsRefundReplay(request.IdempotencyKey)` → return the generated result (student loaded as in step 9); no gateway call, no save. (4) `payment.EnsureRefundable()`. (5) `var refund = await paymentGateway.RefundAsync(new PaymentRefundRequest(payment.Id, payment.PaymobTransactionId!, payment.Amount), ct)`. (6) `var now = timeProvider.GetUtcNow()`. (7) Load the subscription as in #7 step 8. (8) `PaymentRefundSettlement.Apply(payment, subscription, refund.TransactionId, now, userId, request.Reason!.Trim(), request.IdempotencyKey)`. (9) `SaveChangesAsync` once; load the student via `userRepository.FirstOrDefaultAsync(x => x.Id == payment.StudentId, ct, asNoTracking: true)`; return `AdminPaymentResultGenerator.Generate`. |
| 17 | `api/Elmanhg.Application/Payments/ResolvePaymentReview/ResolvePaymentReviewCommand.cs` | command | `public sealed record ResolvePaymentReviewCommand(Guid PaymentId) : IRequest<AdminPaymentResult>, IAuditableCommand { "Payment.ResolveReview", "Payment", PaymentId }` |
| 18 | `.../ResolvePaymentReviewValidator.cs` | validator | `PaymentId.ValidateRequired(PaymentIdRequired)` |
| 19 | `.../ResolvePaymentReviewHandler.cs` | handler `(IPaymentRepository, IUserRepository, ICurrentUserService, TimeProvider)` | (1) Auth guard. (2) Load the payment tracked → 404. (3) `payment.ResolveReview(userId, timeProvider.GetUtcNow())`. (4) Save once. (5) Load the student, return the result. |
| 20 | `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobPaymentGateway.Refund.cs` | partial adapter | `public async Task<PaymentRefund> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken)`. Constant `RefundPath = "api/acceptance/void_refund/refund"`. POST `JsonContent.Create(new PaymobRefundRequest(request.ProviderTransactionId, request.Amount.AmountMinor))`, `Authorization: Token SecretKey`, `UserAgent` as in checkout. The catch filter is the same as `StartCheckoutAsync` → log Error "Paymob refund for payment {PaymentId} failed before a response arrived." → 503. `(int)status >= 500 \|\| status == TooManyRequests` → log Error → 503. Other non-2xx → log Warning "Paymob declined the refund for payment {PaymentId} with HTTP {StatusCode}." → `BusinessRuleViolationCoreException(ErrorCodes.PaymentRefundDeclined)`. Read `PaymobRefundResponse` (JsonException → Error log → 503). Missing id (not a number and not a non-blank string) → 503. `Success != true \|\| Pending == true` → Warning → declined. Return `new PaymentRefund(id)`. Never log bodies or keys. |
| 21 | `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobRefundRequest.cs` | record | `public sealed record PaymobRefundRequest([property: JsonPropertyName("transaction_id")] string TransactionId, [property: JsonPropertyName("amount_cents")] long AmountCents);` |
| 22 | `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobRefundResponse.cs` | record | `public sealed record PaymobRefundResponse([property: JsonPropertyName("id")] JsonElement? Id, [property: JsonPropertyName("success")] bool? Success, [property: JsonPropertyName("pending")] bool? Pending);` |
| 23 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddPaymentRefunds.cs` + `.Designer.cs` | migration | Generated. Nullable columns on `Payments`: `RefundedAt timestamptz`, `RefundedBy uuid`, `RefundReason text`, `RefundTransactionId varchar(100)`, `RefundIdempotencyKey uuid`, `ReviewResolvedAt timestamptz`, `ReviewResolvedBy uuid`; indexes `IX_Payments_RefundTransactionId` (unique, filtered), `IX_Payments_CreationDate`, `IX_Payments_OpenReview` (filtered). No drops. |
| 24 | `api/Elmanhg.Api/Controllers/Payments/PaymentsController.cs` | controller | See API surface. `private const string IdempotencyKeyHeader = "Idempotency-Key";` |
| 25 | `api/Elmanhg.Api/Controllers/Payments/Requests.cs` | request | `public sealed record RefundPaymentRequest(string? Reason);` |
| 26 | `api/Elmanhg.Tests/Fixtures/Paymob/transaction-refund.json` | fixture | Copy of `transaction-succeeded.json` with `"has_parent_transaction": true`, `"is_refund": true`, `"parent_transaction": 192036465`, `"is_refunded": false`, `"success": true`, `"pending": false`, `"data": {"message": "Refunded"}`. |
| 27 | `api/Elmanhg.Tests/Integration/Payments/PaymentsTestData.cs` | test helper | `public const string Route = "/api/payments";` `Task<HttpClient> AdminClientAsync(ApiFactory, ct)` (SeedAdminAsync + SignedInClientAsync). `Task<(Payment Payment, Subscription Subscription)> SeedSucceededAsync(ApiFactory factory, Guid studentId, DateTimeOffset startsAt, PaymentReviewReason? flag, CancellationToken ct)`: builds a Base Monthly 19900 EGP payment and its subscription through the domain (`Subscription.Start`, `MarkSucceeded` with `txn-{Guid:N}`, optional `FlagForReview`) and saves both. `HttpRequestMessage RefundRequest(Guid paymentId, string reason, Guid? key)`. |
| 28–43 | Test classes | tests | Listed in the Test plan: `RefundPaymentHandlerTests`, `RefundPaymentValidatorTests`, `ResolvePaymentReviewHandlerTests`, `ResolvePaymentReviewValidatorTests`, `GetPaymentLogHandlerTests`, `GetPaymentLogFilterTests`, `GetPaymentLogValidatorTests` (under `Tests/Application/Features/Payments/<UseCase>/`), `AdminPaymentResultGeneratorTests` (`.../Payments/Shared/`), `PaymentRefundSettlementTests` (`.../Subscriptions/Shared/`), `ProcessPaymentNotificationReversalTests` (`.../Subscriptions/ProcessPaymentNotification/`), `PaymobPaymentGatewayRefundTests` (`Tests/Infrastructure/Payments/`), `PaymentLogEndpointTests`, `RefundPaymentEndpointTests`, `ResolvePaymentReviewEndpointTests` (`Tests/Integration/Payments/`), `PaymobRefundWebhookEndpointTests` (`Tests/Integration/Subscriptions/`, own private helpers mirroring `PaymobWebhookEndpointTests`). |
| 44 | `web/src/routes/admin/payments.tsx` | route | `createFileRoute('/admin/payments')({ validateSearch: paymentLogSearchSchema, component: PaymentLogPage })` |
| 45 | `web/src/features/payments/index.ts` | barrel | exports `PaymentLogPage`, `paymentLogSearchSchema`, `type PaymentLogSearch` |
| 46 | `web/src/features/payments/locales.ts` | locales | `export const paymentsLocales = { ar, en };` |
| 47–48 | `web/src/features/payments/i18n/en.json`, `ar.json` | strings | UI strings table below. |
| 49 | `web/src/features/payments/api/paymentLogParams.ts` | util | `paymentLogPageSize = 20`; `toPaymentLogParams(search: PaymentLogSearch): GetPaymentLogParams` (pageNumber = page ?? 1, pageSize, needsReview = view === 'review', optional status/plan/reference/studentId, from/to as local midnight ISO like `auditLogParams`, `to` + 1 day); `hasActiveFilters(search)` (status, plan, reference, from, to, studentId); `toPaymentLogPage(data)` → `{ items, pageNumber, totalPages, totalItems }`. |
| 50 | `web/src/features/payments/schemas/paymentLogSearchSchema.ts` | zod | `// mirrors Subscriptions:PaymentLogReferenceMaxLength` `paymentLogReferenceMaxLength = 100`. `z.object({ page: z.coerce.number().int().min(1).optional().catch(undefined), view: z.enum(['all','review']).optional().catch(undefined), status: z.enum(['Pending','Succeeded','Failed','Refunded']).optional().catch(undefined), plan: z.enum(['Base','AskTeacher']).optional().catch(undefined), reference: z.string().trim().min(1).max(100).optional().catch(undefined), studentId: z.uuid().optional().catch(undefined), from: z.iso.date().optional().catch(undefined), to: z.iso.date().optional().catch(undefined) })` |
| 51 | `.../schemas/paymentLogFiltersSchema.ts` | zod | `{ status: z.string(), plan: z.string(), reference: z.string().trim().max(100, { error: 'payments:filters.errors.referenceTooLong' }), from: dateField, to: dateField }` with `.refine(to >= from, { path: ['to'], error: 'payments:filters.errors.dateRange' })`; `dateField` as in the audit schema with `'payments:filters.errors.date'`. |
| 52 | `.../schemas/refundPaymentSchema.ts` | zod | `// mirrors Subscriptions:RefundReasonMaxLength` `refundReasonMaxLength = 500`. `z.object({ reason: z.string().trim().min(1, { error: 'payments:refund.errors.reasonRequired' }).max(500, { error: 'payments:refund.errors.reasonTooLong' }) })` |
| 53 | `.../hooks/usePaymentLogSearch.ts` | hook | `getRouteApi('/admin/payments')`; returns `{ search, applyFilters(values), setView(view), setPage(page), filterStudent(studentId), clearFilters() }`. `applyFilters` keeps `view`, resets page to 1, drops empty values. `setView` resets page and keeps filters. `clearFilters` keeps only `view`. |
| 54 | `.../hooks/usePaymentLog.ts` | hook | `useGetPaymentLog(toPaymentLogParams(search), { query: { placeholderData: keepPreviousData, select: toPaymentLogPage } })` |
| 55 | `.../hooks/usePaymentReviewCount.ts` | hook | `useGetPaymentLog({ needsReview: true, pageNumber: 1, pageSize: 1 }, { query: { select: (d) => Number(d.totalItems ?? 0) } })` |
| 56 | `.../hooks/useRefundPayment.ts` | hook | Wraps the generated `useRefundPayment`. `onSuccess`: invalidate `{ queryKey: [getGetPaymentLogQueryKey()[0]] }` and `toast.success(t('refund.done'))`. Exposes `refund(paymentId, reason, idempotencyKey, { onSuccess, onError })` (passes `headers: { 'Idempotency-Key': idempotencyKey }` through the generated headers parameter) and `isPending`. |
| 57 | `.../hooks/useResolvePaymentReview.ts` | hook | Same shape: invalidate the log, `toast.success(t('resolve.done'))`, error → `toast.error(t([common:errors.CODE, common:errors.UNHANDLED_EXCEPTION]))`. Exposes `resolve(paymentId, onDone)` and `isPending`. |
| 58 | `.../components/PaymentLogTabs.tsx` | component | Two pill buttons (`aria-pressed`): `tabs.all`; `tabs.review` plus a count badge (`bg-soft text-text-muted`, `formatNumber(count, lng, 'latin')`) when count > 0. Props `{ view: 'all' \| 'review'; reviewCount: number; onChange }`. |
| 59 | `.../components/PaymentLogSelectField.tsx` | component | Labelled `<select>` bound with `useController` (styling copied from `ResourceTypeField`). Props `{ name: 'status' \| 'plan'; label: string; allLabel: string; options: { value: string; label: string }[] }`. |
| 60 | `.../components/PaymentLogFilters.tsx` | form | RHF + `paymentLogFiltersSchema`; grid `md:grid-cols-5`: status select, plan select, reference `TextField` (`dir="ltr"`, hint), from/to date `TextField`s; `SubmitButton` apply, ghost clear. When `search.studentId` is set: a line `filters.studentActive`. |
| 61 | `.../components/PaymentLogTable.tsx` | component | Table in a card (audit styling), caption `table.caption`, headers `date, student, plan, amount, status, reference, actions`; props `{ items, onRefund(item), onKeep(item), onStudent(studentId) }`. |
| 62 | `.../components/PaymentLogRow.tsx` | component | date `formatDate(createdAt, lng, 'latin', { dateStyle: 'medium', timeStyle: 'short' })`. Student: `<button type="button">` with visible name and `aria-label={t('table.showStudent', { name })}` → `onStudent`, plus contact in caption `dir="ltr"`. Plan: `plan.X · period.Y`. Amount: `formatMoney(Number(amount.amountMinor), currency, lng, 'latin')`. Status: `PaymentStatusBadge`, plus the review badge `review.badge` and caption `review.<reason>` when `needsReview`. Refunded: caption `table.refundedLine` with date and reason. Reference: mono `paymobTransactionId`. Actions: `Button variant="danger" size="sm"` `actions.refund` when `canRefund`; `Button variant="secondary" size="sm"` `actions.keep` when `needsReview`. |
| 63 | `.../components/PaymentStatusBadge.tsx` | component | Pill `text-micro font-semibold`; Succeeded `bg-success text-surface`, Failed `bg-danger text-surface`, Pending/Refunded `bg-soft text-text-muted`; label `status.X`. |
| 64 | `.../components/PaymentLogEmptyState.tsx` | component | Variants `no-data` (`empty.noData`), `no-review` (`empty.noReview`), `no-results` (`empty.noResults` plus a primary clear button); `CreditCard` icon. |
| 65 | `.../components/PaymentLogTableSkeleton.tsx` | component | Copy of the audit skeleton; `aria-label={t('page.loading')}`. |
| 66 | `.../components/RefundPaymentDialog.tsx` | form in dialog | Props `{ payment: AdminPaymentResult \| null; onOpenChange(open) }`. Mounted by the page with `key={payment.id}`. `const [idempotencyKey] = useState(() => crypto.randomUUID())`. Dialog title `refund.title` {amount}; body `refund.body` {name}; `Form` with `TextAreaField name="reason"` label `refund.reason`; `FormRootError`; buttons secondary `refund.cancel`, danger submit `refund.confirm` disabled while pending. Submit → `refund(...)`: success → `onOpenChange(false)`; error → `applyServerErrors(form, error, { PAYMENT_REFUND_REASON_REQUIRED: 'reason', PAYMENT_REFUND_REASON_TOO_LONG: 'reason' })`. |
| 67 | `.../components/ResolveReviewDialog.tsx` | dialog | Same shape as `CancelSubscriptionDialog`: title `resolve.title`, body `resolve.body`, secondary `resolve.cancel`, primary `resolve.confirm` (disabled while pending). |
| 68 | `.../pages/PaymentLogPage.tsx` | page | H1 `page.title`; `PaymentLogTabs`; `PaymentLogFilters` (keyed by a JSON of filter values); content states as in `AuditLogPage` (skeleton / error + retry / empty variant: `no-results` if `hasActiveFilters`, else `no-review` on the review view, else `no-data` / table + `Pagination`); state `refundTarget` and `keepTarget` with the two dialogs. |
| 69 | `web/src/test/paymentFixtures.ts` | fixtures | `adminPayment(overrides)` → a full `AdminPaymentResult` (Succeeded, canRefund true, student "Mona Ali", 19900 EGP); `paymentLogPage(items, pageNumber = 1, totalPages = 1)`. |
| 70 | `web/src/shared/form/TextAreaField.tsx` | moved | Identical content to the old questions file. |
| 71–76 | Web tests | tests | `paymentLogParams.test.ts`, `paymentLogSearchSchema.test.ts`, `paymentLogFiltersSchema.test.ts`, `refundPaymentSchema.test.ts`, `pages/PaymentLogPage.test.tsx`, `pages/PaymentLogPage.actions.test.tsx`. |

### UI strings (`payments` namespace, en / ar)
| Key | en | ar |
|---|---|---|
| page.title | Payments | المدفوعات |
| page.loading | Loading payments… | جارٍ تحميل المدفوعات… |
| tabs.all / tabs.review | All transactions / Needs review | كل المعاملات / بحاجة لمراجعة |
| filters.status / allStatuses | Status / All statuses | الحالة / كل الحالات |
| filters.plan / allPlans | Plan / All plans | الباقة / كل الباقات |
| filters.reference / referenceHint | Reference / Payment id or Paymob transaction id | المرجع / رقم الدفع أو رقم معاملة Paymob |
| filters.from / to / apply / clear | From / To / Apply / Clear filters | من / إلى / تطبيق / مسح الفلاتر |
| filters.studentActive | Showing one student's payments. | تعرض مدفوعات طالب واحد. |
| filters.errors.date / dateRange / referenceTooLong | Enter a valid date. / The end date is before the start date. / The reference is too long. | أدخل تاريخًا صحيحًا. / تاريخ النهاية قبل تاريخ البداية. / المرجع طويل جدًا. |
| status.Pending/Succeeded/Failed/Refunded | Pending / Successful / Failed / Refunded | قيد الانتظار / ناجحة / فاشلة / مستردة |
| plan.Base / plan.AskTeacher | Base / Ask a Teacher | الأساسية / اسأل معلّم |
| period.Monthly/Termly/Yearly | Monthly / Termly / Yearly | شهري / ترم / سنوي |
| review.badge | Needs review | بحاجة لمراجعة |
| review.AskTeacherWithoutBase | Ask a Teacher paid without Base | اسأل معلّم مدفوع بدون الباقة الأساسية |
| review.PartialRefundAtProvider | Partial refund in Paymob | استرداد جزئي في Paymob |
| table.caption | Payment log | سجل المدفوعات |
| table.date/student/plan/amount/status/reference/actions | Date / Student / Plan / Amount / Status / Reference / Actions | التاريخ / الطالب / الباقة / المبلغ / الحالة / المرجع / إجراءات |
| table.showStudent | Show {name}'s payments | عرض مدفوعات {name} |
| table.refundedLine | Refunded on {date}: {reason} | استُرد في {date}: {reason} |
| actions.refund / actions.keep | Refund / Keep payment | استرداد / إبقاء الدفع |
| refund.title | Refund {amount}? | استرداد {amount}؟ |
| refund.body | The amount goes back to {name} through Paymob, and the time this payment bought is removed from the plan at once. A refund cannot be undone. | يعود المبلغ إلى {name} عبر Paymob، وتُخصم المدة التي اشتراها هذا الدفع من الاشتراك فورًا. لا يمكن التراجع عن الاسترداد. |
| refund.reason / cancel / confirm / done | Refund reason / Back / Confirm refund / Payment refunded. | سبب الاسترداد / تراجع / تأكيد الاسترداد / تم استرداد الدفع. |
| refund.errors.reasonRequired / reasonTooLong | Enter a refund reason. / The refund reason is too long. | اكتب سبب الاسترداد. / سبب الاسترداد طويل جدًا. |
| resolve.title / body | Keep this payment? / The review closes; the payment and the plan stay as they are. | إبقاء هذا الدفع؟ / تُغلق المراجعة ويبقى الدفع والاشتراك كما هما. |
| resolve.cancel / confirm / done | Back / Keep payment / Review closed. | تراجع / إبقاء الدفع / أُغلقت المراجعة. |
| empty.noData / noReview / noResults | No payments yet. / No payments need review. / No payments match these filters. | لا توجد مدفوعات بعد. / لا توجد مدفوعات بحاجة لمراجعة. / لا توجد مدفوعات تطابق الفلاتر. |
| error.title | Could not load payments. | تعذّر تحميل المدفوعات. |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|---|---|---|---|---|
| (Domain) PaymentAlreadyRefunded | PAYMENT_ALREADY_REFUNDED | `Payment.EnsureRefundable` | BusinessRuleViolationCoreException | 400 |
| (Domain) PaymentNotRefundable | PAYMENT_NOT_REFUNDABLE | `Payment.EnsureRefundable` | BusinessRuleViolationCoreException | 400 |
| (Domain) PaymentReviewNotOpen | PAYMENT_REVIEW_NOT_OPEN | `Payment.ResolveReview` | BusinessRuleViolationCoreException | 400 |
| PaymentIdRequired | PAYMENT_ID_REQUIRED | Refund/Resolve validators | validation | 422 |
| PaymentRefundReasonRequired | PAYMENT_REFUND_REASON_REQUIRED | RefundPaymentValidator | validation | 422 |
| PaymentRefundReasonTooLong | PAYMENT_REFUND_REASON_TOO_LONG | RefundPaymentValidator | validation | 422 |
| PaymentRefundIdempotencyKeyRequired | PAYMENT_REFUND_IDEMPOTENCY_KEY_REQUIRED | RefundPaymentValidator | validation | 422 |
| PaymentRefundDeclined | PAYMENT_REFUND_DECLINED | `PaymobPaymentGateway.RefundAsync` | BusinessRuleViolationCoreException | 400 |
| PaymentNotSettled | PAYMENT_NOT_SETTLED | webhook `ReverseAsync` | ConflictCoreException | 409 |
| PaymentLogPageNumberInvalid | PAYMENT_LOG_PAGE_NUMBER_INVALID | GetPaymentLogValidator | validation | 422 |
| PaymentLogPageSizeInvalid | PAYMENT_LOG_PAGE_SIZE_INVALID | GetPaymentLogValidator | validation | 422 |
| PaymentLogStatusInvalid | PAYMENT_LOG_STATUS_INVALID | GetPaymentLogValidator | validation | 422 |
| PaymentLogPlanInvalid | PAYMENT_LOG_PLAN_INVALID | GetPaymentLogValidator | validation | 422 |
| PaymentLogReferenceTooLong | PAYMENT_LOG_REFERENCE_TOO_LONG | GetPaymentLogValidator | validation | 422 |
| PaymentLogDateRangeInvalid | PAYMENT_LOG_DATE_RANGE_INVALID | GetPaymentLogValidator | validation | 422 |

Reused: `PAYMENT_NOT_FOUND` (404), `PAYMENT_GATEWAY_UNAVAILABLE` (503), `PAYMENT_MODIFIED_CONCURRENTLY` / `SUBSCRIPTION_MODIFIED_CONCURRENTLY` / `PAYMENT_TRANSACTION_ALREADY_RECORDED` (409), `PAYMENT_NOTIFICATION_MISMATCH` (400), `USER_NOT_AUTHENTICATED` (401).

Resource strings (en / ar, no tashkeel):
- PAYMENT_ALREADY_REFUNDED: This payment has already been refunded. / تم استرداد هذا الدفع بالفعل.
- PAYMENT_NOT_REFUNDABLE: Only a successful payment can be refunded. / لا يمكن استرداد الا دفع ناجح.
- PAYMENT_REVIEW_NOT_OPEN: This payment has no open review. / لا توجد مراجعة مفتوحة لهذا الدفع.
- PAYMENT_ID_REQUIRED: The payment id is required. / معرف الدفع مطلوب.
- PAYMENT_REFUND_REASON_REQUIRED: Enter a refund reason. / اكتب سبب الاسترداد.
- PAYMENT_REFUND_REASON_TOO_LONG: The refund reason is too long. / سبب الاسترداد طويل جدا.
- PAYMENT_REFUND_IDEMPOTENCY_KEY_REQUIRED: The Idempotency-Key header must be a non-empty UUID. / ترويسة Idempotency-Key مطلوبة ويجب ان تكون UUID غير فارغ.
- PAYMENT_REFUND_DECLINED: Paymob declined the refund. Check the transaction in the Paymob dashboard. / رفضت Paymob الاسترداد. راجع المعاملة في لوحة Paymob.
- PAYMENT_NOT_SETTLED: The payment has not been settled yet. / لم تتم تسوية الدفع بعد.
- PAYMENT_LOG_PAGE_NUMBER_INVALID: The page number must be 1 or more. / رقم الصفحة يجب ان يكون 1 او اكثر.
- PAYMENT_LOG_PAGE_SIZE_INVALID: The page size is not valid. / حجم الصفحة غير صالح.
- PAYMENT_LOG_STATUS_INVALID: Unknown payment status. / حالة الدفع غير معروفة.
- PAYMENT_LOG_PLAN_INVALID: Unknown plan. / الباقة غير معروفة.
- PAYMENT_LOG_REFERENCE_TOO_LONG: The reference is too long. / المرجع طويل جدا.
- PAYMENT_LOG_DATE_RANGE_INVALID: The start must be before the end. / يجب ان تكون البداية قبل النهاية.

## Domain behaviour
```csharp
// Payment.Refund.cs  (ErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes)
public void EnsureRefundable()
{
    if (Status == PaymentStatus.Refunded)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentAlreadyRefunded);
    }

    if (!IsRefundable)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentNotRefundable);
    }
}

public void MarkRefunded(string refundTransactionId, DateTimeOffset refundedAt, Guid? refundedBy, string? reason, Guid? idempotencyKey)
{
    EnsureRefundable();
    if (NeedsReview)
    {
        ReviewResolvedAt = refundedAt;
        ReviewResolvedBy = refundedBy;
    }

    Status = PaymentStatus.Refunded;
    RefundTransactionId = refundTransactionId;
    RefundedAt = refundedAt;
    RefundedBy = refundedBy;
    RefundReason = reason;
    RefundIdempotencyKey = idempotencyKey;
    UpdationDate = DateTimeOffset.UtcNow;
}

public void ResolveReview(Guid resolvedBy, DateTimeOffset resolvedAt)
{
    if (!NeedsReview)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentReviewNotOpen);
    }

    ReviewResolvedAt = resolvedAt;
    ReviewResolvedBy = resolvedBy;
    UpdationDate = DateTimeOffset.UtcNow;
}

// Subscription.Refund.cs — never throws for state; Expired is left alone
public bool RevokePaidPeriod(int periodMonths, DateTimeOffset revokedAt)
{
    if (Status == SubscriptionStatus.Expired)
    {
        return false;
    }

    EnsurePeriod(periodMonths);
    var shortenedEnd = CurrentPeriodEnd.AddMonths(-periodMonths);
    if (shortenedEnd <= revokedAt)
    {
        CurrentPeriodEnd = revokedAt < CurrentPeriodEnd ? revokedAt : CurrentPeriodEnd;
        Status = SubscriptionStatus.Expired;
        ExpiredAt = revokedAt;
    }
    else
    {
        CurrentPeriodEnd = shortenedEnd;
    }

    CurrentPeriodStart = CurrentPeriodStart < CurrentPeriodEnd ? CurrentPeriodStart : CurrentPeriodEnd;
    UpdationDate = DateTimeOffset.UtcNow;
    return true;
}
```
Existing changes: `MarkSucceeded` also refuses `Refunded` (`PAYMENT_NOT_PENDING`). `FlagForReview` clears `ReviewResolvedAt`/`ReviewResolvedBy` (reopens). `Period` and `CancelledAt` are unchanged by `RevokePaidPeriod`. A Cancelled subscription with time left stays Cancelled.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| GET | `/api/payments` (Name `GetPaymentLog`) | `DefaultCodes.PaymentsManage` | `[FromQuery] PaymentStatus? status, SubscriptionPlan? plan, bool needsReview = false, Guid? studentId, string? reference, DateTimeOffset? from, DateTimeOffset? to, int pageNumber = 1, int pageSize = 20` | `[ProducesResponseType<PageData<AdminPaymentResult>>(200)]`; 422 codes above |
| POST | `/api/payments/{paymentId:guid}/refund` (Name `RefundPayment`) | `PaymentsManage` | `[FromBody] RefundPaymentRequest`, `[FromHeader(Name = IdempotencyKeyHeader)] Guid? idempotencyKey` → `new RefundPaymentCommand(paymentId, request.Reason, idempotencyKey)` | `AdminPaymentResult` 200; 400 `PAYMENT_ALREADY_REFUNDED`/`PAYMENT_NOT_REFUNDABLE`/`PAYMENT_REFUND_DECLINED`; 404; 409; 422; 503 |
| POST | `/api/payments/{paymentId:guid}/review-resolution` (Name `ResolvePaymentReview`) | `PaymentsManage` | route only | `AdminPaymentResult` 200; 400 `PAYMENT_REVIEW_NOT_OPEN`; 404; 409 |

Controller: `[ApiController] [Route("api/payments")] [Authorize] public class PaymentsController(IMediator mediator) : ControllerBase`; thin; `CancellationToken` on every action. The webhook route is unchanged.

## Postman
New folder `AdminPayments` after `AuditLogs`. Description: "Admin only (Payments.Manage). Sign in as an admin first. Refund uses checkoutPaymentId from the Subscriptions folder (fake-completed). A refund needs an Idempotency-Key header." Requests in order:
1. Get payment log: `GET {{baseUrl}}/api/payments?pageNumber=1&pageSize=20`, with disabled `status`, `plan`, `needsReview`, `studentId`, `reference`, `from`, `to`; tests: 200, `items` is an array.
2. Get review queue: `needsReview=true`; 200.
3. Refund payment: `POST .../{{checkoutPaymentId}}/refund`, header `Idempotency-Key: {{$guid}}`, body `{ "reason": "Duplicate charge" }`; tests: 200, `status === "Refunded"`.
4. Refund payment again: new `{{$guid}}`; tests: 400, `code === "PAYMENT_ALREADY_REFUNDED"`.
5. Refund without key: no header; 422 `PAYMENT_REFUND_IDEMPOTENCY_KEY_REQUIRED`.
6. Resolve review (not flagged): `POST .../{{checkoutPaymentId}}/review-resolution`; 400 `PAYMENT_REVIEW_NOT_OPEN`.

## Docs
- **PRD.md**:
  - §11.2 replace the last bullet with: "Full transaction log with a needs-review queue for the admin; refunds are initiated by an admin (full amount, reason required), executed in Paymob, recorded locally, and remove the time the payment bought (the plan ends at once if nothing paid remains). Signed Paymob refund callbacks have the same effect; a partial refund made in Paymob is flagged for review."
  - §15 Payment line: add `period_months, provider_order_id?, review_reason?, review_resolved_at?, review_resolved_by?, status[...|Refunded], refunded_at?, refunded_by?, refund_reason?, refund_transaction_id?, refund_idempotency_key?`.
  - §16 new row "Payment log and refunds | – | – | ✓".
  - §17 rule 12: "Subscription entitlement changes only through Paymob-verified events: HMAC-verified webhooks, or Paymob's response to an admin refund. The client never sets entitlement."
- **subscriptions.md**:
  - Payments: add the `Refunded` state, `MarkRefunded`, `EnsureRefundable`, the review fields, and `NeedsReview`.
  - New "Refunds" section: Decisions 2–6 and 9, and `RevokePaidPeriod` with the clamp note.
  - New "Admin payment log" section: Decisions 10–13.
  - Webhook table: new rows for reversal outcomes (Decision 8), plus the signed-only classification.
  - API table: the 3 routes.
  - Config table: 3 new keys.
  - "For later stories": #102 done; overlap known limit → follow-up issue.
- **paymob.md**:
  - §1: the adapter also refunds.
  - §5: add refund API checks (secret-key auth on `void_refund/refund`, `transaction_id` as string, response `id/success/pending`, whether same-day card payments need void, child callback shape `has_parent_transaction` + `is_refund`).
  - §8: replace the ignored-callbacks paragraph with the signed-only classification and reversal handling.
  - New §9 "Refunds": the request, error mapping, the fake, no retries.
- **audit-log.md**: rows `RefundPayment | Payment.Refund | Payment | command (the diff shows status, refund fields, review resolution, and the Subscription end/status)` and `ResolvePaymentReview | Payment.ResolveReview | Payment | command`. The `ProcessPaymentNotification` note also mentions reversals.
- **claude-design-prompt.md**:
  - §4 Admin: new bullet `#/admin/payments` (tabs, filters, table columns, refund dialog with required reason, keep-payment dialog, states).
  - §5 rule 11: add "or an admin refund".
- **prototype.md**: in Admin walkthrough, "The product also has a payments page (log, needs-review queue, refunds); the prototype does not simulate it."

## Test plan
### api — unit (Domain, no doubles)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 1 | PaymentTests | MarkRefunded_Succeeded_RecordsRefundAndStatus | Status Refunded; RefundTransactionId, RefundedAt, RefundedBy, RefundReason, RefundIdempotencyKey set; IsRefundable false |
| 2 | PaymentTests | MarkRefunded_FlaggedPayment_ResolvesReview | NeedsReview false; ReviewResolvedAt = refundedAt; ReviewResolvedBy = refundedBy |
| 3 | PaymentTests | MarkRefunded_AlreadyRefunded_ThrowsAlreadyRefunded | exception + `PAYMENT_ALREADY_REFUNDED` |
| 4 | PaymentTests | MarkRefunded_NotSucceeded_ThrowsNotRefundable (Theory: pending, failed) | `PAYMENT_NOT_REFUNDABLE`; status unchanged |
| 5 | PaymentTests | MarkSucceeded_Refunded_ThrowsNotPending | `PAYMENT_NOT_PENDING` |
| 6 | PaymentTests | ResolveReview_OpenReview_RecordsResolver | fields set; NeedsReview false |
| 7 | PaymentTests | ResolveReview_NotFlagged_ThrowsReviewNotOpen | `PAYMENT_REVIEW_NOT_OPEN` |
| 8 | PaymentTests | ResolveReview_AlreadyResolved_ThrowsReviewNotOpen | `PAYMENT_REVIEW_NOT_OPEN` |
| 9 | PaymentTests | FlagForReview_AfterResolution_ReopensReview | NeedsReview true; resolution cleared |
| 10 | PaymentTests | IsRefundReplay_ByKey_MatchesOnlySameKeyAfterRefund (Theory: same key → true, other key → false) | bool |
| 11 | SubscriptionTests | RevokePaidPeriod_RenewedActive_ShortensToPreviousEnd | returns true; end = end − months; Active |
| 12 | SubscriptionTests | RevokePaidPeriod_FirstPeriod_ExpiresAtRevocation | Expired; ExpiredAt = revokedAt; end = revokedAt; start ≤ end |
| 13 | SubscriptionTests | RevokePaidPeriod_ActiveInsideGrace_ExpiresWithoutGrace | Expired; `IsEntitledAt(revokedAt, grace)` false |
| 14 | SubscriptionTests | RevokePaidPeriod_CancelledWithTimeLeft_ShortensAndStaysCancelled | Cancelled; end shortened |
| 15 | SubscriptionTests | RevokePaidPeriod_Expired_ReturnsFalseAndKeepsDates | false; dates unchanged |
| 16 | SubscriptionTests | RevokePaidPeriod_PeriodMonthsBelowOne_ThrowsPeriodInvalid | `SUBSCRIPTION_PERIOD_INVALID` |

### api — unit (Application, NSubstitute at ports, `TimeProvider` substitute as in existing tests)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 17 | PaymentRefundSettlementTests | Apply_WithSubscription_MarksRefundedAndRevokesPaidMonths | payment Refunded; subscription end shortened |
| 18 | PaymentRefundSettlementTests | Apply_WithoutSubscription_MarksRefundedOnly | payment Refunded; no throw |
| 19 | RefundPaymentHandlerTests | Handle_SucceededPayment_RefundsThroughGatewayAndRevokesSubscription | gateway `Received(1)` with (payment id, txn id, amount); payment Refunded with gateway id, admin id, trimmed reason, key; subscription shortened; result Status Refunded, CanRefund false; Save `Received(1)` |
| 20 | RefundPaymentHandlerTests | Handle_FlaggedPayment_ResolvesReviewWithRefund | result NeedsReview false |
| 21 | RefundPaymentHandlerTests | Handle_SameIdempotencyKeyAfterRefund_ReplaysWithoutGatewayCall | result Refunded; gateway and Save `DidNotReceive` |
| 22 | RefundPaymentHandlerTests | Handle_AlreadyRefundedWithOtherKey_ThrowsAlreadyRefunded | code; gateway and Save `DidNotReceive` |
| 23 | RefundPaymentHandlerTests | Handle_FailedPayment_ThrowsNotRefundable | code; gateway and Save `DidNotReceive` |
| 24 | RefundPaymentHandlerTests | Handle_UnknownPayment_ThrowsPaymentNotFound | NotFoundCoreException + code; Save `DidNotReceive` |
| 25 | RefundPaymentHandlerTests | Handle_GatewayDeclines_PropagatesAndSavesNothing | BusinessRuleViolationCoreException `PAYMENT_REFUND_DECLINED`; payment still Succeeded; Save `DidNotReceive` |
| 26 | RefundPaymentHandlerTests | Handle_Anonymous_ThrowsUnauthorized | `USER_NOT_AUTHENTICATED`; Save `DidNotReceive` |
| 27 | RefundPaymentValidatorTests | Validate_ValidCommand_Passes | valid |
| 28 | RefundPaymentValidatorTests | Validate_EmptyPaymentId_FailsWithPaymentIdRequired | code |
| 29 | RefundPaymentValidatorTests | Validate_MissingReason_FailsWithReasonRequired (Theory: null, "", "   ") | code |
| 30 | RefundPaymentValidatorTests | Validate_ReasonTooLong_FailsWithReasonTooLong | code (501 chars) |
| 31 | RefundPaymentValidatorTests | Validate_NullIdempotencyKey_FailsWithKeyRequired | code |
| 32 | RefundPaymentValidatorTests | Validate_EmptyIdempotencyKey_FailsWithKeyRequired | code |
| 33 | ResolvePaymentReviewHandlerTests | Handle_OpenReview_ResolvesAndSaves | resolved by the admin at now; Save `Received(1)` |
| 34 | ResolvePaymentReviewHandlerTests | Handle_NoOpenReview_ThrowsReviewNotOpen | code; Save `DidNotReceive` |
| 35 | ResolvePaymentReviewHandlerTests | Handle_UnknownPayment_ThrowsPaymentNotFound | code; Save `DidNotReceive` |
| 36 | ResolvePaymentReviewHandlerTests | Handle_Anonymous_ThrowsUnauthorized | code |
| 37 | ResolvePaymentReviewValidatorTests | Validate_ValidCommand_Passes | valid |
| 38 | ResolvePaymentReviewValidatorTests | Validate_EmptyPaymentId_FailsWithPaymentIdRequired | code |
| 39 | GetPaymentLogHandlerTests | Handle_Page_ReturnsItemsWithStudentNamesAndPaging | names and contact mapped; paging copied |
| 40 | GetPaymentLogHandlerTests | Handle_StudentMissing_ReturnsEmptyStudentName | "" and null contact |
| 41 | GetPaymentLogFilterTests | Build_NoFilters_MatchesEveryPayment | compiled predicate over an in-memory list |
| 42 | GetPaymentLogFilterTests | Build_Status_MatchesOnlyThatStatus | |
| 43 | GetPaymentLogFilterTests | Build_Plan_MatchesOnlyThatPlan | |
| 44 | GetPaymentLogFilterTests | Build_NeedsReview_MatchesOpenReviewsOnly | flagged-open yes; resolved and unflagged no |
| 45 | GetPaymentLogFilterTests | Build_StudentId_MatchesThatStudent | |
| 46 | GetPaymentLogFilterTests | Build_Reference_MatchesPaymentIdOrTransactionIds (Theory: "id", "paymob", "refund", "order") | the one payment matches |
| 47 | GetPaymentLogFilterTests | Build_DateRange_IsFromInclusiveToExclusive | boundaries |
| 48 | GetPaymentLogValidatorTests | Validate_ValidQuery_Passes | |
| 49 | GetPaymentLogValidatorTests | Validate_PageNumberBelowOne_FailsWithPageNumberInvalid | |
| 50 | GetPaymentLogValidatorTests | Validate_PageSizeOutOfRange_FailsWithPageSizeInvalid (Theory 0, 101) | |
| 51 | GetPaymentLogValidatorTests | Validate_UndefinedStatus_FailsWithStatusInvalid | `(PaymentStatus)99` |
| 52 | GetPaymentLogValidatorTests | Validate_UndefinedPlan_FailsWithPlanInvalid | |
| 53 | GetPaymentLogValidatorTests | Validate_ReferenceTooLong_FailsWithReferenceTooLong | 101 chars |
| 54 | GetPaymentLogValidatorTests | Validate_FromNotBeforeTo_FailsWithDateRangeInvalid | |
| 55 | AdminPaymentResultGeneratorTests | Generate_RefundedPayment_MapsRefundFieldsAndCannotRefund | refund fields; CanRefund false; contact = email else phone |
| 56 | ProcessPaymentNotificationHandlerTests | Handle_OtherKind_ReturnsIgnored (replaces RefundOrVoid) | Ignored; no save |
| 57 | ProcessPaymentNotificationHandlerTests | Handle_SuccessForRefundedPayment_ReturnsOutOfOrder | OutOfOrder; status Refunded; no save |
| 58 | ProcessPaymentNotificationHandlerTests | Handle_CurrencyDiffers_ThrowsMismatch | `PAYMENT_NOTIFICATION_MISMATCH` (#191 item) |
| 59 | ProcessPaymentNotificationReversalTests | Handle_FullReversalForSucceeded_MarksRefundedAndRevokesSubscription | outcome Refunded; RefundTransactionId; RefundedBy null; subscription Expired; Save `Received(1)` |
| 60 | ProcessPaymentNotificationReversalTests | Handle_ReversalAlreadyRecorded_ReturnsDuplicate | Duplicate; no save |
| 61 | ProcessPaymentNotificationReversalTests | Handle_ReversalForRefundedPayment_ReturnsDuplicate | Duplicate; no save |
| 62 | ProcessPaymentNotificationReversalTests | Handle_ReversalForPendingPayment_ThrowsNotSettled | ConflictCoreException `PAYMENT_NOT_SETTLED`; no save |
| 63 | ProcessPaymentNotificationReversalTests | Handle_ReversalForFailedPayment_ReturnsOutOfOrder | no save |
| 64 | ProcessPaymentNotificationReversalTests | Handle_PartialReversal_FlagsForReviewWithoutRevoking | FlaggedForReview; reason PartialRefundAtProvider; status Succeeded; subscription unchanged; save once |
| 65 | ProcessPaymentNotificationReversalTests | Handle_ReversalAboveAmount_ThrowsMismatch | code; no save |
| 66 | ProcessPaymentNotificationReversalTests | Handle_ReversalCurrencyDiffers_ThrowsMismatch | code |
| 67 | ProcessPaymentNotificationReversalTests | Handle_FailedReversal_ReturnsIgnored | Ignored; no repository read of payment state change; no save |

### api — unit (Infrastructure)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 68 | PaymobNotificationReaderTests | Read_SignedTransaction_MapsFields (modified) | Kind Charge |
| 69 | PaymobNotificationReaderTests | Read_RefundedParentTransaction_IsOther (replaces FlagsRefundOrVoid) | Kind Other |
| 70 | PaymobNotificationReaderTests | Read_RefundChildTransaction_IsReversal | Kind Reversal; id, amount |
| 71 | PaymobNotificationReaderTests | Read_UnsignedRefundFlagsOnCharge_StayCharge | `is_refund`/`is_void` true added to a signed charge → Charge |
| 72 | PaymobNotificationReaderTests | Read_CaptureChildTransaction_IsOther | Other |
| 73 | PaymobNotificationReaderTests | Read_ParentFlagTampered_ThrowsSignatureInvalid | sign a charge, set `has_parent_transaction` true → 401 code |
| 74 | PaymobPaymentGatewayRefundTests | RefundAsync_ValidRequest_PostsToRefundEndpointWithTokenAuth | URI `https://accept.paymob.com/api/acceptance/void_refund/refund`; `Token sk_test`; UA |
| 75 | PaymobPaymentGatewayRefundTests | RefundAsync_ValidRequest_SendsTransactionIdAndAmount | body `transaction_id` string, `amount_cents` 19900 |
| 76 | PaymobPaymentGatewayRefundTests | RefundAsync_Approved_ReturnsRefundTransactionId | numeric id → raw text |
| 77 | PaymobPaymentGatewayRefundTests | RefundAsync_NotSuccessful_ThrowsRefundDeclined | code |
| 78 | PaymobPaymentGatewayRefundTests | RefundAsync_PendingResponse_ThrowsRefundDeclined | code |
| 79 | PaymobPaymentGatewayRefundTests | RefundAsync_ClientError_ThrowsRefundDeclined | 400 → code |
| 80 | PaymobPaymentGatewayRefundTests | RefundAsync_ServerError_ThrowsPaymentGatewayUnavailable | 500 |
| 81 | PaymobPaymentGatewayRefundTests | RefundAsync_TooManyRequests_ThrowsPaymentGatewayUnavailable | 429 |
| 82 | PaymobPaymentGatewayRefundTests | RefundAsync_NetworkFailure_ThrowsPaymentGatewayUnavailable | |
| 83 | PaymobPaymentGatewayRefundTests | RefundAsync_MissingId_ThrowsPaymentGatewayUnavailable | |
| 84 | PaymobPaymentGatewayRefundTests | RefundAsync_InvalidJsonBody_ThrowsPaymentGatewayUnavailable | |
| 85 | FakePaymentGatewayTests | RefundAsync_Development_ReturnsFakeRefundTransaction | `fake-refund-{id:N}` |
| 86 | FakePaymentGatewayTests | RefundAsync_Production_ThrowsPaymentGatewayUnavailable | code |

### api — integration (Testcontainers, through HTTP)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 87 | PaymentLogEndpointTests | Get_Anonymous_Returns401 | status |
| 88 | PaymentLogEndpointTests | Get_NonAdmin_Returns403 (Theory: Student, Teacher) | status |
| 89 | PaymentLogEndpointTests | Get_Admin_ReturnsPaymentsNewestFirstWithStudentName | order; `studentName`; `amount.amountMinor`; Pending included |
| 90 | PaymentLogEndpointTests | Get_NeedsReview_ReturnsOnlyOpenReviews | only the flagged payment of the seeded student (filter by `studentId`) |
| 91 | PaymentLogEndpointTests | Get_StatusAndReferenceFilters_NarrowResults | `reference=<paymob txn>` → one; `status=Failed` → failed only |
| 92 | PaymentLogEndpointTests | Get_PageSizeAboveMax_Returns422WithCode | `PAYMENT_LOG_PAGE_SIZE_INVALID` |
| 93 | RefundPaymentEndpointTests | Post_Admin_RefundsPaymentAndStudentLosesAccess | 200 `status` Refunded; DB payment Refunded; subscription Expired; student entitlement `tier` Free |
| 94 | RefundPaymentEndpointTests | Post_SameIdempotencyKeyTwice_ReplaysSameResult | both 200 with the same `refundTransactionId` |
| 95 | RefundPaymentEndpointTests | Post_AlreadyRefundedWithNewKey_Returns400AlreadyRefunded | problem `code` |
| 96 | RefundPaymentEndpointTests | Post_MissingIdempotencyKey_Returns422WithCode | code; DB unchanged |
| 97 | RefundPaymentEndpointTests | Post_UnknownPayment_Returns404 | `PAYMENT_NOT_FOUND` |
| 98 | RefundPaymentEndpointTests | Post_Teacher_Returns403 | status |
| 99 | RefundPaymentEndpointTests | Post_Admin_WritesAuditRowWithRefundDiff | audit row `Payment.Refund`, Success, resourceId = payment id, diff contains `refundReason` |
| 100 | ResolvePaymentReviewEndpointTests | Post_FlaggedPayment_ClosesReviewAndLeavesQueue | 200 `needsReview` false; `needsReview=true` log no longer lists it |
| 101 | ResolvePaymentReviewEndpointTests | Post_NotFlagged_Returns400ReviewNotOpen | code |
| 102 | ResolvePaymentReviewEndpointTests | Post_Student_Returns403 | status |
| 103 | PaymobRefundWebhookEndpointTests | Post_SignedFullRefund_MarksRefundedAndStudentBecomesFree | 200 `Refunded`; DB; tier Free |
| 104 | PaymobRefundWebhookEndpointTests | Post_SameRefundTwice_SecondIsDuplicate | outcomes Refunded, Duplicate |
| 105 | PaymobRefundWebhookEndpointTests | Post_SignedPartialRefund_FlagsForReviewAndKeepsAccess | `FlaggedForReview`; tier Base; reviewReason PartialRefundAtProvider |
| 106 | PaymobRefundWebhookEndpointTests | Post_RefundBeforeSettlement_Returns409NotSettled | 409 `PAYMENT_NOT_SETTLED`; payment Pending |
| 107 | PaymobRefundWebhookEndpointTests | Post_UnsignedRefundFlagOnCharge_SettlesAsCharge | charge + `is_refund:true`, signed → `Succeeded` |
| 108 | PaymentPersistenceTests | SaveChanges_DuplicateRefundTransactionId_ThrowsTransactionAlreadyRecorded | ConflictCoreException code |
| 109 | PaymentHistoryEndpointTests | Get_StudentWithRefundedPayment_ListsRefundedStatus | item `status` Refunded |
| 110 | PermissionMatrixPolicyTests | (rows added) | Payments.Manage matrix |
| 111 | AppDbContextTests | Migrate_FreshDatabase_LeavesNoPendingMigrations (list extended) | `_AddPaymentRefunds` |

### web (Vitest + Testing Library + MSW via Orval handlers)
| # | Test file | it(...) | Asserts |
|---|---|---|---|
| 112 | paymentLogParams.test.ts | maps the search to request params | page default 1, pageSize 20, needsReview from view, local-midnight from/to (+1 day on to), omits empty |
| 113 | paymentLogParams.test.ts | reports active filters | true for each filter, false for only view/page |
| 114 | paymentLogSearchSchema.test.ts | keeps valid search values | parsed object |
| 115 | paymentLogSearchSchema.test.ts | drops invalid values | bad page/view/status/plan/studentId/date → undefined |
| 116 | paymentLogFiltersSchema.test.ts | accepts valid filters | success |
| 117 | paymentLogFiltersSchema.test.ts | rejects a reference over 100 characters | error key |
| 118 | paymentLogFiltersSchema.test.ts | rejects an end date before the start date | path `to`, key |
| 119 | refundPaymentSchema.test.ts | accepts a reason | success |
| 120 | refundPaymentSchema.test.ts | rejects an empty or blank reason | key |
| 121 | refundPaymentSchema.test.ts | rejects a reason over 500 characters | key |
| 122 | PaymentLogPage.test.tsx | shows payments after loading | loading status name "Loading payments…"; row with "Mona Ali" and /EGP\s?199/ |
| 123 | PaymentLogPage.test.tsx | shows the empty state when there are no payments | "No payments yet." |
| 124 | PaymentLogPage.test.tsx | offers clear filters when filters match nothing | "No payments match these filters." + Clear filters restores rows |
| 125 | PaymentLogPage.test.tsx | shows an error and recovers on retry | alert, Retry, rows |
| 126 | PaymentLogPage.test.tsx | applies status, plan and reference filters to the request | request query `status`, `plan`, `reference` |
| 127 | PaymentLogPage.test.tsx | shows the review queue with its count | tab shows count; clicking sends `needsReview=true`; review badge and reason visible |
| 128 | PaymentLogPage.test.tsx | shows the review empty state when nothing needs review | "No payments need review." |
| 129 | PaymentLogPage.test.tsx | filters by a student from the row | request `studentId`; "Showing one student's payments." |
| 130 | PaymentLogPage.test.tsx | moves to the next page | request `pageNumber=2` |
| 131 | PaymentLogPage.test.tsx | shows an inline error when the end date is before the start date | error text; no request with those dates |
| 132 | PaymentLogPage.test.tsx | renders right-to-left in Arabic | `dir="rtl"`, heading "المدفوعات" |
| 133 | PaymentLogPage.test.tsx | has no axe violations | axe |
| 134 | PaymentLogPage.actions.test.tsx | refunds a payment with a reason | request body reason, `Idempotency-Key` header is a UUID, toast "Payment refunded.", refetched row shows "Refunded" |
| 135 | PaymentLogPage.actions.test.tsx | requires a reason before refunding | inline "Enter a refund reason."; no refund request |
| 136 | PaymentLogPage.actions.test.tsx | shows the Paymob decline inside the dialog | 400 `PAYMENT_REFUND_DECLINED` → alert text in the dialog; dialog stays open |
| 137 | PaymentLogPage.actions.test.tsx | hides the refund action when the payment cannot be refunded | no Refund button for `canRefund: false` |
| 138 | PaymentLogPage.actions.test.tsx | keeps a flagged payment after confirming | resolve request sent; toast "Review closed."; badge gone after refetch |
| 139 | permissions.test.ts | grants an admin payment management and denies other roles | can() |
| 140 | MorePage.test.tsx | lists the admin destinations… (modified) | includes 'Payments' |
| 141 | SubscriptionPage.payments.test.tsx | labels a refunded payment | "Refunded" badge |
| 142 | CheckoutResultPage.test.tsx | shows the refunded state for a refunded payment | "This payment was refunded" |

## Morabh reuse
- `Morabh.Application/Orders/Workflow/RefundOrderDownPayment/RefundOrderDownPaymentCommand.cs`, `...Validator.cs`, and `Morabh.Domain/Orders/OrderWorkflowExtensions.cs` `RefundOrder`: shape reused (auditable command `<Resource>.Refund`, required `RefundReason`, domain `ALREADY_REFUNDED` guard). Morabh records a manual InstaPay refund; the gateway call, the entitlement revocation and the review queue are new — no Morabh equivalent.
- Admin log query: new, mirrors Elmanhg's own `GetAuditLogs` (#58); Morabh `ListOrders` has no filters or paging.
- Paymob refund adapter, signed-only callback classification, web page: new — no Morabh equivalent.

## Definition of done
- [ ] `Payments.Manage` policy exists (Admin only); all three endpoints use it; the permission-matrix rows pass.
- [ ] `POST /api/payments/{id}/refund` calls `IPaymentGateway.RefundAsync` once, marks the payment Refunded with reason, actor, key and refund transaction id, and revokes the payment's months from its subscription in one save.
- [ ] A refund of a first purchase leaves the student Free immediately (integration test 93).
- [ ] The same Idempotency-Key replays without a gateway call; a new key on a refunded payment returns 400 `PAYMENT_ALREADY_REFUNDED`; a missing key returns 422.
- [ ] Gateway outage → 503; decline → 400 `PAYMENT_REFUND_DECLINED`; nothing saved in either case.
- [ ] `PaymobPaymentGateway.RefundAsync` posts to `api/acceptance/void_refund/refund` with token auth and no retries; `FakePaymentGateway.RefundAsync` is the default and locked in Production.
- [ ] The webhook classifies callbacks from signed fields only; `is_refund`/`is_void` do not appear in `PaymobNotificationReader`.
- [ ] Full signed reversal → Refunded + revoke; partial → `PartialRefundAtProvider` review; Pending → 409 `PAYMENT_NOT_SETTLED`; a success for a Refunded payment → OutOfOrder.
- [ ] `GET /api/payments` supports status, plan, needsReview, studentId, reference, from/to and paging, newest first, with student names.
- [ ] `ResolvePaymentReview` closes an open review; a refund closes it too; `FlagForReview` reopens it.
- [ ] `Payment.Refund` and `Payment.ResolveReview` are audited; the refund diff shows the refund fields (test 99).
- [ ] Migration `AddPaymentRefunds` is additive only and listed in `AppDbContextTests`.
- [ ] All 15 new error codes exist in both resx files and in web `common` errors.
- [ ] New config keys are in `SubscriptionsOptions` (with defaults), `appsettings.example.json` and `ApiFactory`.
- [ ] `api/openapi/v1.json`, the Orval client (`headers: true`) and `routeTree.gen.ts` are regenerated and committed; Postman has the `AdminPayments` folder.
- [ ] `/admin/payments` shows tabs with the review count, filters in the URL, table, refund dialog (required reason, inline server errors), keep dialog, and loading / empty (three variants) / error+retry states; RTL and axe tests pass.
- [ ] Admin nav shows "المدفوعات"; the student log and checkout result render Refunded.
- [ ] `TextAreaField` lives in `shared/form` and the questions feature imports it from there.
- [ ] PRD (§11.2, §15, §16, §17 rule 12), subscriptions.md, paymob.md (§1, §5, §8, §9), audit-log.md, claude-design-prompt.md (§4, §5) and prototype.md are updated.
- [ ] Every test in the Test plan exists with the listed name; `dotnet test api/ -c Release` passes with `appsettings.json` moved aside; web typecheck, lint, prettier (`--end-of-line auto`) and vitest pass.
- [ ] No file above ~100 lines (api) or 200 lines (web); style-guard grep is clean.
