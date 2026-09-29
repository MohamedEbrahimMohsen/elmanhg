# Plan — Paymob checkout integration (#100, E10.S2)

## Goal
A Free student can press **اشترك** on a Base period (monthly, termly or yearly), or on Ask a Teacher when Base is already active. The API creates a Pending `Payment` at the configured price and returns a redirect URL. With the real adapter that URL is Paymob's unified checkout (card and wallet). With the default fake it is an in-app simulated Paymob page whose "نجاح الدفع" or "فشل الدفع" buttons settle the payment like a webhook would. The student then lands on a result page that polls the payment until it is Succeeded or Failed, and a successful payment makes the entitlement read Base. The browser never activates a plan: the return page only reads the payment status (PRD §11.2, §17 rule 12).

## Scope
**In:**
- API: `IPaymentGateway` port; `FakePaymentGateway` (the default); `PaymobPaymentGateway` (real, Intention API, switched on by config); options with startup validation.
- API: `StartCheckout` command (Pending payment + redirect). `GetMyPayment` query (single payment, Pending included, owner-scoped). `CompleteFakePayment` command (fake-only settlement). `PaymentSettlement` shared settle routine that #101 reuses.
- Domain: purchase rules on `StudentEntitlement`.
- Web: subscribe buttons on the plan cards, the simulated Paymob checkout page, and the checkout result page with polling.
- Docs, Postman, OpenAPI and Orval regeneration.

**Out (other stories):**
- The webhook endpoint, HMAC verification, idempotent processing, renewals and the downgrade sweep: #101.
- Refunds and the admin payments page: #102.
- The student cancel button: #101.
- `RawWebhook` audit redaction: #101 (#187). The fake payload has no PII.

**Deferred (becomes a GitHub issue):**
- **Live verification of `PaymobPaymentGateway` against a Paymob test account.** No Paymob merchant credentials exist in this repo. The adapter is built and tested against a stub `HttpMessageHandler` only. The field names flagged under Decision D2 must be checked in the Paymob dashboard and docs before go-live.
- **Playwright E2E for the pay journey.** The repo has no `web/e2e` setup. No story has added one yet; the convention's E2E row stays unbuilt, as in every earlier story.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Intention API vs the legacy auth → order → payment_key flow | **Intention API**: one `POST {BaseUrl}/v1/intention/` with `Authorization: Token <SecretKey>`, then redirect to `{CheckoutUrl}?publicKey=<PublicKey>&clientSecret=<client_secret>` (unified checkout). | The dev prefers it. It is Paymob's current documented flow: one call instead of three, one hosted page for card and wallets (`payment_methods` lists several integration ids), and no iframe id. It covers every sub-task step: "auth token" is the secret-key header, "order registration" is the intention (Paymob creates the order), "payment key" is `client_secret`, and "redirect URL" is the unified checkout URL. |
| D2 | Paymob field-name certainty | **Request:** `amount` (minor units, integer), `currency`, `payment_methods` (int[] of integration ids), `items[] {name, amount, description, quantity}`, `billing_data {first_name, last_name, email, phone_number, apartment, floor, street, building, city, country, state}`, `special_reference`, `notification_url` (omitted when blank), `redirection_url`. **Response read:** `client_secret` only (`id` is parsed, not used). **Uncertain, must be verified before go-live (listed in `docs/paymob.md`):** whether `special_reference` echoes back as `merchant_order_id` in the webhook (#101 depends on it); whether `billing_data.email` accepts `"NA"`; the phone format Paymob wants (local `010…` vs `+20…`); the query-parameter names `publicKey`/`clientSecret` and the trailing slash on `/unifiedcheckout/`; whether `payment_methods` may mix integer ids with strings. | This is the best knowledge of the 2024–2026 Intention API. It is isolated in two request/response records, so fixing a field touches one file. |
| D3 | How the fake completes a payment end-to-end locally | The fake redirect URL is the in-app path `{Payments:FakeCheckoutPath}/{paymentId}` (`/student/fake-checkout/{id}`). That web page calls `POST /api/subscriptions/payments/{paymentId}/fake-completion {succeeded}`, which runs the same `PaymentSettlement.Settle` that #101's webhook will call. | This mirrors the real redirect → hosted page → server-side confirmation flow. Entitlement still changes only through a server-side settlement that stands in for the webhook. |
| D4 | Could the fake grant free plans in production? | `FakePaymentGateway.SupportsSimulatedCompletion` is `!IHostEnvironment.IsProduction()`. In Production the fake's `StartCheckoutAsync` throws `ServiceUnavailableCoreException(PAYMENT_GATEWAY_UNAVAILABLE)`, and `CompleteFakePayment` returns 404 `FAKE_CHECKOUT_UNAVAILABLE`. There is no startup failure. | The build-time OpenAPI run and CI must boot without keys. A misconfigured production server must still never hand out free plans. |
| D5 | Default provider | `Payments:Provider` defaults to `Fake` in code, in `appsettings.example.json` and in `ApiFactory`. | Dev decision: fakes by default; tests and CI need no keys. D4 covers the production risk. |
| D6 | Buying a plan the student already has | Refused: `StudentEntitlement.EnsureCanPurchase(plan)` throws `CHECKOUT_PLAN_ALREADY_ACTIVE` (400) while an entitled subscription of that plan exists, including a Cancelled one that is still inside its paid period. Settlement therefore always calls `Subscription.Start(now)`, never `Renew`. | Renewal semantics belong to #101 (#187 follow-up). Early re-purchase is not in the PRD. |
| D7 | Ask a Teacher without Base | Refused: `CHECKOUT_REQUIRES_BASE` (400) when no entitled Base exists. | `docs/subscriptions.md` says "Checkout (#100) refuses to sell it alone"; PRD §11.1 says the add-on requires Base. |
| D8 | Period availability | `SubscriptionsOptions.PriceFor(plan, period)` returns `null` for a Base period not in `BasePrices` and for Ask a Teacher with any period other than Monthly. The validator turns that into 422 `CHECKOUT_PERIOD_UNAVAILABLE`. The handler's `?? throw BadRequestCoreException(CHECKOUT_PERIOD_UNAVAILABLE)` covers configuration changing between validation and handling. | Prices and periods are configuration (PRD §11.1). |
| D9 | Order of save vs the gateway call | `Payment.Create` (the id is known) → gateway call → `AddAsync` → one `SaveChangesAsync`. A gateway failure saves nothing. | No orphan Pending rows for failed calls. A Paymob intention orphaned by a failed save is harmless: its webhook finds no payment, which #101 handles. |
| D10 | Store the Paymob intention id? | No. There is no new column and no migration. `Payment.Id` is `special_reference` (merchant order id, as `docs/subscriptions.md` already states). | This story needs no schema change. The transaction id arrives with the webhook (#101). |
| D11 | Web return URL for the real adapter | `redirection_url = {Payments:Paymob:RedirectionUrl}/{paymentId}`, where `RedirectionUrl` is the web route base, for example `https://app.example/student/checkout-result`. The result page ignores whatever query string Paymob appends. | A path segment survives Paymob appending `?success=…`. Query parameters from the browser are never trusted. |
| D12 | Result page polling | Poll `GET /api/subscriptions/payments/{id}` every `checkoutPollIntervalMs = 2000` while the payment is Pending and less than `checkoutConfirmationTimeoutMs = 60000` has passed since the page opened. After that, show "confirmation has not arrived yet" with a **Check again** button. | Until #101 ships, the real adapter never confirms, so the page must end in an honest state. |
| D13 | Base period choice UI | One accent button per configured Base price, in catalogue order: "اشترك شهريًا", "اشترك لفصل دراسي", "اشترك سنويًا". Ask a Teacher gets one "اشترك" button, disabled with a visible hint when the student has no Base. Active plans keep the "مفعّلة" badge and show no button. The Free card has no button. | The prototype shows the button or the badge per card and a disabled Ask a Teacher button without Base. Separate buttons avoid a form (the §4 RHF rule) for a single choice. |
| D14 | Fake checkout as a modal (prototype) vs a page | A page at `/student/fake-checkout/$paymentId` with the prototype modal's content: "Paymob checkout (محاكاة)", plan and amount, the fake card, the HMAC note, and **نجاح الدفع** / **فشل الدفع** / **إلغاء**. The prototype's result modal becomes `/student/checkout-result/$paymentId`. | The real flow is a full-page redirect, so the fake must follow the same path. `docs/claude-design-prompt.md` §4 is updated to match (docs-sync). |
| D15 | External redirect in the SPA | `shared/lib/redirect.ts` exports `isInAppUrl(url)` (starts with `/` and not `//`) and `redirectToExternal(url)` (`window.location.assign`). In-app URLs go through `useRouter().history.push(url)`. | This keeps `window.location` out of features and makes the external branch mockable in one test file. |
| D16 | Auditing | `StartCheckoutCommand` and `CompleteFakePaymentCommand` implement `IAuditableCommand`. `Payment` and `Subscription` are already `IAuditedEntity`. | Business mutations must be audited (`docs/audit-log.md`). |
| D17 | Retries on the Paymob POST | Standard resilience handler with `Retry.DisableForUnsafeHttpMethods()` and the attempt and total timeouts from `Payments` options (same shape as `OtpDelivery`). | A POST retry without an idempotency key could create duplicate intentions (skill §8.8). |
| D18 | Two quick checkouts, both paid | Not guarded here: the web disables every subscribe button while a checkout is pending. A duplicate paid Base is a #101/#102 reconciliation or refund concern and is noted in `docs/subscriptions.md` → "For later stories". | Settlement is webhook-driven; only #101 sees real double payments. |

## Morabh reuse
Morabh has no payment-gateway code: its PRD says "no automated payment gateway in MVP", and payments are manual InstaPay proofs. Everything below is **new, with no Morabh equivalent**. The in-repo precedent, which must be mirrored, is #171: `api/Elmanhg.Infrastructure/OtpDelivery/{OtpDeliveryServiceCollectionExtensions,OtpDeliveryOptionsValidator,OtpProviderHttpExtensions,FakeOtpChannel}.cs` and `WhatsApp/MetaWhatsAppOtpChannel.cs`.

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Subscriptions/StudentEntitlement.cs` | Add `EnsureCanPurchase(SubscriptionPlan plan)` (see Domain behaviour). |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Under `// SUBSCRIPTIONS`, add `CheckoutPlanAlreadyActive = "CHECKOUT_PLAN_ALREADY_ACTIVE"` and `CheckoutRequiresBase = "CHECKOUT_REQUIRES_BASE"`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Under `// SUBSCRIPTIONS`, add the 8 application codes from the Error codes table. |
| `api/Elmanhg.Application/Shared/Options/SubscriptionsOptions.cs` | Add `public PlanPriceOptions? PriceFor(SubscriptionPlan plan, BillingPeriod period)`: a switch expression. `Base` → `BasePrices.GetValueOrDefault(period)`. `AskTeacher when period == BillingPeriod.Monthly` → `new PlanPriceOptions { Months = AskTeacherPeriodMonths, AmountMinor = AskTeacherMonthlyPriceMinor }`. `_` → `null`. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | Call `services.AddPayments();` right after `services.AddOtpDelivery();`. |
| `api/Elmanhg.Api/Controllers/Subscriptions/SubscriptionsController.cs` | Add 3 actions (see API surface). |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Add 10 keys (see Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | Add the `Payments` section (see F13). |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `.env.example` | Add a commented block: `# Payments (docs/paymob.md). Fake is the default; set Paymob and its keys to go live.` then `# Payments__Provider=Paymob`, `# Payments__Paymob__SecretKey=`, `# Payments__Paymob__PublicKey=`, `# Payments__Paymob__IntegrationIds__0=`, `# Payments__Paymob__IntegrationIds__1=`, `# Payments__Paymob__RedirectionUrl=`, `# Payments__Paymob__NotificationUrl=`. |
| `postman/elmanhg.postman_collection.json` | In the "Subscriptions" folder, after "Get my payments", add in order: **Start Base checkout**, **Get checkout payment**, **Complete fake payment**, **Get my entitlement after payment**. Update the folder description (see F-P). |
| `api/Elmanhg.Tests/Domain/Subscriptions/StudentEntitlementTests.cs` | Add tests T1–T5 (add only). |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/SubscriptionsOptionsTests.cs` | Add tests T6–T9 (add only). |
| `api/Elmanhg.Tests/Infrastructure/OtpDelivery/StubHttpMessageHandler.cs` | Additive: `public string? ResponseBody { get; set; }`. The response becomes `new HttpResponseMessage(StatusCode) { Content = new StringContent(ResponseBody ?? string.Empty, Encoding.UTF8, "application/json") }`. Existing OTP tests are unaffected. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add to the in-memory dictionary: `["Payments:Provider"] = "Fake"`, `["Payments:FakeCheckoutPath"] = "/student/fake-checkout"`, `["Payments:AttemptTimeoutSeconds"] = "10"`, `["Payments:TotalTimeoutSeconds"] = "30"`. |
| `api/Elmanhg.Tests/Integration/Subscriptions/SubscriptionTestData.cs` | Add `public static async Task<Payment> SeedPendingPaymentAsync(ApiFactory factory, Guid studentId, SubscriptionPlan plan, BillingPeriod period, long amountMinor, CancellationToken cancellationToken)`. It creates and saves a Pending payment and returns it; the existing overload is unchanged. Add `public const string FakeCheckoutPath = "/student/fake-checkout";`. |
| `web/src/features/subscription/components/PlanCard.tsx` | Add the optional prop `actions?: ReactNode`, rendered after the `<ul>` inside `<div className="mt-auto flex flex-col gap-2">` when present. |
| `web/src/features/subscription/components/PlanCardGrid.tsx` | New props `onCheckout: (plan: SubscriptionPlan, period: BillingPeriod) => void` and `isCheckoutPending: boolean`. Base card `actions` = `<BaseCheckoutActions>` unless `entitlement.tier === 'Base'`. AskTeacher card `actions` = `<AskTeacherCheckoutAction>` unless `entitlement.hasAskTeacher`. The Free card has no actions. |
| `web/src/features/subscription/pages/SubscriptionPage.tsx` | `const checkout = useCheckout();`. Pass `onCheckout={checkout.start}` and `isCheckoutPending={checkout.isPending}` to `PlanCardGrid`. While pending, render `<p role="status" className="text-caption text-text-muted">{t('checkout.redirecting')}</p>` above the grid. |
| `web/src/features/subscription/index.ts` | Also export `FakeCheckoutPage` and `CheckoutResultPage`. |
| `web/src/features/subscription/i18n/ar.json`, `en.json` | Add the `period`, `checkout`, `fakeCheckout` and `checkoutResult` blocks (see F-W12). |
| `web/src/shared/i18n/ar.json`, `en.json` | Under `errors`, add the 10 new codes plus `PAYMENT_NOT_PENDING` (see Error codes). |
| `web/src/test/subscriptionFixtures.ts` | Add `export const checkoutPaymentId = 'd4d4d4d4-d4d4-4d4d-8d4d-d4d4d4d4d4d4';` and `export function checkoutResult(redirectUrl = '/student/fake-checkout/' + checkoutPaymentId): CheckoutResult` returning `{ paymentId: checkoutPaymentId, redirectUrl, amount: { amountMinor: 19900, currency: 'EGP' } }`. The existing `payment()` gets no change; tests call `payment({ id: checkoutPaymentId, status: 'Pending', completedAt: null })`. |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api`. Produces `useStartCheckout`, `useGetMyPayment`, `useCompleteFakePayment`, their MSW handlers, and the `CheckoutResult`, `StartCheckoutCommand` and `CompleteFakePaymentRequest` models. |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (build or dev). |
| `docs/subscriptions.md` | See Docs. |
| `docs/PRD.md` §11.2 | See Docs. |
| `docs/claude-design-prompt.md` §4 (the `#/student/subscription` line) | See Docs. |

No migration. `Payment` and `Subscription` already have every column this story needs.

## Files to create

### Domain / Application (api)
| # | Path | Type | Contract |
|---|------|------|----------|
| F1 | `api/Elmanhg.Application/Shared/Payments/IPaymentGateway.cs` | interface | `namespace Elmanhg.Application.Shared.Payments;` `public interface IPaymentGateway { bool SupportsSimulatedCompletion { get; } Task<PaymentCheckout> StartCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken); }` |
| F2 | `api/Elmanhg.Application/Shared/Payments/PaymentCheckoutRequest.cs` | sealed record | `public sealed record PaymentCheckoutRequest(Guid PaymentId, Money Amount, SubscriptionPlan Plan, BillingPeriod Period, PaymentCustomer Customer);` |
| F3 | `api/Elmanhg.Application/Shared/Payments/PaymentCustomer.cs` | sealed record | `public sealed record PaymentCustomer(string DisplayName, string? Email, string? PhoneNumber);` |
| F4 | `api/Elmanhg.Application/Shared/Payments/PaymentCheckout.cs` | sealed record | `public sealed record PaymentCheckout(string RedirectUrl);` |
| F5 | `api/Elmanhg.Application/Subscriptions/Shared/CheckoutResult.cs` | sealed record (client-facing, no localized fields) | `public sealed record CheckoutResult(Guid PaymentId, string RedirectUrl, Money Amount);` |
| F6 | `api/Elmanhg.Application/Subscriptions/Shared/PaymentSettlement.cs` | static class | `namespace Elmanhg.Application.Subscriptions.Shared; public static class PaymentSettlement { public static Subscription? Settle(Payment payment, bool succeeded, string transactionId, string rawNotification, int periodMonths, DateTimeOffset completedAt) }`. Body: `if (!succeeded) { payment.MarkFailed(transactionId, rawNotification, completedAt); return null; }` then `var subscription = Subscription.Start(payment.StudentId, payment.Plan, payment.Period, periodMonths, completedAt, transactionId, payment.StudentId); payment.MarkSucceeded(subscription.Id, transactionId, rawNotification, completedAt); return subscription;`. Does not touch repositories; the caller adds and saves. |
| F7 | `api/Elmanhg.Application/Subscriptions/StartCheckout/StartCheckoutCommand.cs` | sealed record | `public sealed record StartCheckoutCommand(SubscriptionPlan? Plan, BillingPeriod? Period) : IRequest<CheckoutResult>, IAuditableCommand;` |
| F8 | `api/Elmanhg.Application/Subscriptions/StartCheckout/StartCheckoutValidator.cs` | sealed class | Constructor `(IOptions<SubscriptionsOptions> subscriptionsOptions)`. Rules: `RuleFor(x => x.Plan).ValidateRequired(ErrorCodes.CheckoutPlanRequired).IsInEnum().WithErrorCode(ErrorCodes.CheckoutPlanInvalid);` `RuleFor(x => x.Period).ValidateRequired(ErrorCodes.CheckoutPeriodRequired).IsInEnum().WithErrorCode(ErrorCodes.CheckoutPeriodInvalid);` `RuleFor(x => x).Must(x => options.PriceFor(x.Plan!.Value, x.Period!.Value) is not null).WithErrorCode(ErrorCodes.CheckoutPeriodUnavailable).When(x => x.Plan.HasValue && x.Period.HasValue && Enum.IsDefined(x.Plan.Value) && Enum.IsDefined(x.Period.Value));` (`ValidateRequired<T,TValue>` from `Core.Validation.Extensions`). |
| F9 | `api/Elmanhg.Application/Subscriptions/StartCheckout/StartCheckoutHandler.cs` | sealed class | `public sealed class StartCheckoutHandler(IPaymentRepository paymentRepository, ISubscriptionRepository subscriptionRepository, IUserRepository userRepository, IPaymentGateway paymentGateway, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<StartCheckoutCommand, CheckoutResult>`. Handle steps: 1) `UserId` null/default → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`. 2) `studentId`, `options = subscriptionsOptions.Value`, `now = timeProvider.GetUtcNow()`, `plan = request.Plan!.Value`, `period = request.Period!.Value`. 3) `subscriptions = await subscriptionRepository.FindAsync(SubscriptionEntitlementSpecification.EntitledFor(studentId, now, options.GracePeriod), cancellationToken, asNoTracking: true)`. 4) `StudentEntitlement.Resolve(subscriptions, now, options.GracePeriod).EnsureCanPurchase(plan);`. 5) `price = options.PriceFor(plan, period) ?? throw new BadRequestCoreException(ErrorCodes.CheckoutPeriodUnavailable);`. 6) `user = await userRepository.GetByIdAsync(studentId, cancellationToken, asNoTracking: true) ?? throw new NotFoundCoreException(ErrorCodes.UserNotFound);`. 7) `payment = Payment.Create(studentId, plan, period, new Money(price.AmountMinor, options.Currency));`. 8) `checkout = await paymentGateway.StartCheckoutAsync(new PaymentCheckoutRequest(payment.Id, payment.Amount, plan, period, new PaymentCustomer(user.DisplayName, user.Email, user.PhoneNumber)), cancellationToken);`. 9) `await paymentRepository.AddAsync(payment, cancellationToken); await paymentRepository.SaveChangesAsync(cancellationToken);`. 10) `return new CheckoutResult(payment.Id, checkout.RedirectUrl, payment.Amount);`. `.ConfigureAwait(false)` on every await. |
| F10 | `api/Elmanhg.Application/Subscriptions/GetMyPayment/GetMyPaymentQuery.cs` | sealed record | `public sealed record GetMyPaymentQuery(Guid PaymentId) : IRequest<PaymentResult>;` |
| F11 | `api/Elmanhg.Application/Subscriptions/GetMyPayment/GetMyPaymentHandler.cs` | sealed class | `(IPaymentRepository paymentRepository, ICurrentUserService currentUserService) : IRequestHandler<GetMyPaymentQuery, PaymentResult>`. Steps: 1) user guard → `UnauthorizedCoreException(UserNotAuthenticated)`. 2) `payment = await paymentRepository.FirstOrDefaultAsync(x => x.Id == request.PaymentId && x.StudentId == userId, cancellationToken, asNoTracking: true) ?? throw new NotFoundCoreException(ErrorCodes.PaymentNotFound);`. 3) `return PaymentResultGenerator.Generate(payment);`. Pending is included. |
| F12 | `api/Elmanhg.Application/Subscriptions/CompleteFakePayment/CompleteFakePaymentCommand.cs` | sealed record | `public sealed record CompleteFakePaymentCommand(Guid PaymentId, bool Succeeded) : IRequest<PaymentResult>, IAuditableCommand;` |
| F13h | `api/Elmanhg.Application/Subscriptions/CompleteFakePayment/CompleteFakePaymentHandler.cs` | sealed class | `(IPaymentRepository paymentRepository, ISubscriptionRepository subscriptionRepository, IPaymentGateway paymentGateway, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<CompleteFakePaymentCommand, PaymentResult>`. Constants: `private const string FakeTransactionPrefix = "fake-";` and `private const string FakeNotificationSource = "fake-gateway";`. Steps: 1) user guard. 2) `if (!paymentGateway.SupportsSimulatedCompletion) throw new NotFoundCoreException(ErrorCodes.FakeCheckoutUnavailable);`. 3) `payment = await paymentRepository.FirstOrDefaultAsync(x => x.Id == request.PaymentId && x.StudentId == userId, cancellationToken) ?? throw new NotFoundCoreException(ErrorCodes.PaymentNotFound);` (tracked). 4) `months = subscriptionsOptions.Value.PriceFor(payment.Plan, payment.Period)?.Months ?? throw new BadRequestCoreException(ErrorCodes.CheckoutPeriodUnavailable);`. 5) `transactionId = FakeTransactionPrefix + payment.Id.ToString("N")`; `raw = JsonSerializer.Serialize(new { source = FakeNotificationSource, success = request.Succeeded })`. 6) `subscription = PaymentSettlement.Settle(payment, request.Succeeded, transactionId, raw, months, timeProvider.GetUtcNow());`. 7) `if (subscription is not null) await subscriptionRepository.AddAsync(subscription, cancellationToken);`. 8) `await paymentRepository.SaveChangesAsync(cancellationToken);` (the shared DbContext saves both). 9) `return PaymentResultGenerator.Generate(payment);`. |

### Infrastructure (api)
| # | Path | Type | Contract |
|---|------|------|----------|
| F13 | `api/Elmanhg.Infrastructure/Payments/PaymentsOptions.cs` | sealed class | `namespace Elmanhg.Infrastructure.Payments;` `SectionName = "Payments"`. `public PaymentProvider Provider { get; set; } = PaymentProvider.Fake;` `[Required, RegularExpression("^/[A-Za-z0-9/_-]*$")] public string FakeCheckoutPath { get; set; } = "/student/fake-checkout";` `// Capped at 15 so the standard circuit breaker's 30 s sampling window stays at least twice the attempt timeout.` `[Range(1, 15)] public int AttemptTimeoutSeconds { get; set; } = 10;` `[Range(1, 120)] public int TotalTimeoutSeconds { get; set; } = 30;` `public PaymobOptions Paymob { get; set; } = new();`. The `appsettings.example.json` section: `"Payments": { "Provider": "Fake", "FakeCheckoutPath": "/student/fake-checkout", "AttemptTimeoutSeconds": 10, "TotalTimeoutSeconds": 30, "Paymob": { "BaseUrl": "https://accept.paymob.com", "CheckoutUrl": "https://accept.paymob.com/unifiedcheckout/", "SecretKey": "", "PublicKey": "", "IntegrationIds": [], "NotificationUrl": "", "RedirectionUrl": "", "BillingCountry": "EG" } }` |
| F14 | `api/Elmanhg.Infrastructure/Payments/PaymentProvider.cs` | enum | `public enum PaymentProvider { Fake, Paymob }` |
| F15 | `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobOptions.cs` | sealed class | `BaseUrl = "https://accept.paymob.com"`, `CheckoutUrl = "https://accept.paymob.com/unifiedcheckout/"`, `SecretKey = ""`, `PublicKey = ""`, `List<int> IntegrationIds = []`, `NotificationUrl = ""`, `RedirectionUrl = ""`, `BillingCountry = "EG"`. All are `{ get; set; }`. |
| F16 | `api/Elmanhg.Infrastructure/Payments/PaymentsOptionsValidator.cs` | sealed class `: IValidateOptions<PaymentsOptions>` | Mirrors `OtpDeliveryOptionsValidator`. Always: `AttemptTimeoutSeconds > TotalTimeoutSeconds` → `"Payments:AttemptTimeoutSeconds must not exceed Payments:TotalTimeoutSeconds."`. Only when `Provider == Paymob`: required (non-blank) `SecretKey`, `PublicKey`, `RedirectionUrl`, `BillingCountry`, each with message `"Payments:Paymob:{Key} is required when the Paymob provider is selected."`; `IntegrationIds.Count == 0 \|\| any <= 0` → `"Payments:Paymob:IntegrationIds needs at least one positive integration id."`; absolute https for `BaseUrl`, `CheckoutUrl`, `RedirectionUrl` and (only when non-blank) `NotificationUrl` → `"Payments:Paymob:{Key} must be an absolute https URL."`. Returns `Success` or `Fail(failures)`. |
| F17 | `api/Elmanhg.Infrastructure/Payments/FakePaymentGateway.cs` | sealed class `: IPaymentGateway` | `(IOptions<PaymentsOptions> paymentsOptions, IHostEnvironment hostEnvironment)`. `SupportsSimulatedCompletion => !hostEnvironment.IsProduction();`. `StartCheckoutAsync`: `if (hostEnvironment.IsProduction()) throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable);` then `return Task.FromResult(new PaymentCheckout($"{paymentsOptions.Value.FakeCheckoutPath.TrimEnd('/')}/{request.PaymentId}"));`. |
| F18 | `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobIntentionRequest.cs` | sealed records | `PaymobIntentionRequest([property: JsonPropertyName("amount")] long Amount, ["currency"] string Currency, ["payment_methods"] List<int> PaymentMethods, ["items"] List<PaymobItem> Items, ["billing_data"] PaymobBillingData BillingData, ["special_reference"] string SpecialReference, ["notification_url", JsonIgnore(Condition = WhenWritingNull)] string? NotificationUrl, ["redirection_url"] string RedirectionUrl)` with `public static PaymobIntentionRequest Create(PaymentCheckoutRequest request, PaymobOptions options)`. Item name is `$"Elmanhg {request.Plan} {request.Period}"`, used for both `name` and `description`; amount is `request.Amount.AmountMinor`; quantity is 1. `SpecialReference = request.PaymentId.ToString()`. `NotificationUrl` is null when blank. `RedirectionUrl = $"{options.RedirectionUrl.TrimEnd('/')}/{request.PaymentId}"`. `BillingData = PaymobBillingData.From(request.Customer, options.BillingCountry)`. Also in this file: `public sealed record PaymobItem(["name"] string Name, ["amount"] long Amount, ["description"] string Description, ["quantity"] int Quantity);`. |
| F19 | `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobBillingData.cs` | sealed record | Properties with `JsonPropertyName`: `first_name, last_name, email, phone_number, apartment, floor, street, building, city, country, state` (all `string`). `// Paymob rejects blank billing fields; "NA" is its documented placeholder.` `private const string NotAvailable = "NA";`. `public static PaymobBillingData From(PaymentCustomer customer, string country)`: `parts = customer.DisplayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries \| StringSplitOptions.TrimEntries)`; first = `parts.Length > 0 ? parts[0] : NotAvailable`; last = `parts.Length > 1 ? parts[1] : NotAvailable`; email = blank → `NA`; phone = blank → `NA`; `country` passed through; every other field `NA`. |
| F20 | `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobIntentionResponse.cs` | sealed record | `public sealed record PaymobIntentionResponse([property: JsonPropertyName("id")] string? Id, [property: JsonPropertyName("client_secret")] string? ClientSecret);` |
| F21 | `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobPaymentGateway.cs` | sealed class `: IPaymentGateway` | `(HttpClient httpClient, IOptions<PaymentsOptions> paymentsOptions, ILogger<PaymobPaymentGateway> logger)`. Constants: `IntentionPath = "v1/intention/"`, `TokenScheme = "Token"`, `UserAgent = "Elmanhg/1.0"` (reuse `OtpProviderHttpExtensions.UserAgent`). `SupportsSimulatedCompletion => false`. `StartCheckoutAsync`: build `HttpRequestMessage(Post, IntentionPath) { Content = JsonContent.Create(PaymobIntentionRequest.Create(request, paymob)) }`, set `Authorization = new AuthenticationHeaderValue(TokenScheme, paymob.SecretKey)`, then `UserAgent.ParseAdd`. `try { response = await httpClient.SendAsync(...) } catch (Exception exception) when (exception is HttpRequestException or ExecutionRejectedException \|\| (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))` → `logger.LogError(exception, "Paymob intention for payment {PaymentId} failed before a response arrived.", request.PaymentId)` and throw `ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable, innerException: exception)`. `using (response)`: non-success → `LogError("Paymob rejected the intention for payment {PaymentId} with HTTP {StatusCode}.")` and throw the same code. Read the body with `ReadFromJsonAsync<PaymobIntentionResponse>`, catching `JsonException` → log and throw the same. Blank `ClientSecret` → log `"Paymob returned no client secret for payment {PaymentId}."` and throw the same. Return `new PaymentCheckout($"{paymob.CheckoutUrl}?publicKey={Uri.EscapeDataString(paymob.PublicKey)}&clientSecret={Uri.EscapeDataString(clientSecret)}")`. Never log the body, the keys or the billing data. |
| F22 | `api/Elmanhg.Infrastructure/Payments/PaymentsServiceCollectionExtensions.cs` | static class | `public static IServiceCollection AddPayments(this IServiceCollection services)`: `AddOptions<PaymentsOptions>().BindConfiguration(SectionName).ValidateDataAnnotations().ValidateOnStart()`; `AddSingleton<IValidateOptions<PaymentsOptions>, PaymentsOptionsValidator>()`; `AddHttpClient<PaymobPaymentGateway>((sp, client) => client.BaseAddress = new Uri(Options(sp).Paymob.BaseUrl.TrimEnd('/') + "/"))`, then `.AddStandardResilienceHandler().Configure((resilience, sp) => { attempt/total timeouts from options; resilience.Retry.DisableForUnsafeHttpMethods(); })`; `AddScoped<FakePaymentGateway>()`; `AddScoped<IPaymentGateway>(sp => Options(sp).Provider switch { PaymentProvider.Fake => sp.GetRequiredService<FakePaymentGateway>(), PaymentProvider.Paymob => sp.GetRequiredService<PaymobPaymentGateway>(), _ => throw new InvalidOperationException("Unsupported Payments:Provider.") })`. Private `Options(IServiceProvider)` helper as in OTP. |

### API
| # | Path | Type | Contract |
|---|------|------|----------|
| F23 | `api/Elmanhg.Api/Controllers/Subscriptions/Requests.cs` | sealed record | `namespace Elmanhg.Api.Controllers.Subscriptions; public sealed record CompleteFakePaymentRequest(bool Succeeded);` |

### Web
| # | Path | Type | Contract |
|---|------|------|----------|
| W1 | `web/src/shared/lib/redirect.ts` | module | `export function isInAppUrl(url: string): boolean` (returns `url.startsWith('/') && !url.startsWith('//')`) and `export function redirectToExternal(url: string): void` (`window.location.assign(url)`). |
| W2 | `web/src/features/subscription/api/checkoutPolling.ts` | constants | `export const checkoutPollIntervalMs = 2000;` `export const checkoutConfirmationTimeoutMs = 60000;` |
| W3 | `web/src/features/subscription/hooks/useCheckout.ts` | hook | `export interface Checkout { start: (plan: SubscriptionPlan, period: BillingPeriod) => void; isPending: boolean }`. Uses the generated `useStartCheckout`. `onSuccess(result)`: if `isInAppUrl(result.redirectUrl)` → `router.history.push(result.redirectUrl)` (`const router = useRouter()`), else `redirectToExternal(result.redirectUrl)`. `onError`: `toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']))` with `code = error instanceof ApiError ? error.code : unhandledErrorCode`. `start` calls `mutate({ data: { plan, period } })`. |
| W4 | `web/src/features/subscription/hooks/useFakePaymentCompletion.ts` | hook | `export function useFakePaymentCompletion(paymentId: string): { complete: (succeeded: boolean) => void; isPending: boolean }`. Uses `useCompleteFakePayment`. `onSuccess(payment)`: `queryClient.setQueryData(getGetMyPaymentQueryKey(paymentId), payment)`; `await queryClient.invalidateQueries({ queryKey: getGetMyEntitlementQueryKey() })`; `await queryClient.invalidateQueries({ queryKey: [getGetMyPaymentsQueryKey()[0]] })`; `await navigate({ to: '/student/checkout-result/$paymentId', params: { paymentId } })`. `onError`: toast as in W3. |
| W5 | `web/src/features/subscription/hooks/useCheckoutResult.ts` | hook | `export function useCheckoutResult(paymentId: string)`. `const [openedAt] = useState(() => Date.now())`; `query = useGetMyPayment(paymentId, { query: { refetchInterval: (q) => q.state.data?.status === 'Pending' && q.state.dataUpdatedAt - openedAt < checkoutConfirmationTimeoutMs ? checkoutPollIntervalMs : false } })`. Returns `{ query, timedOut: query.data?.status === 'Pending' && query.dataUpdatedAt - openedAt >= checkoutConfirmationTimeoutMs }`. |
| W6 | `web/src/features/subscription/components/BaseCheckoutActions.tsx` | component | Props `{ prices: PlanPriceResult[]; disabled: boolean; onCheckout: (period: BillingPeriod) => void }`. For each price: `<Button variant="accent" className="w-full" disabled={disabled} onClick={() => onCheckout(price.period)}>{t(`checkout.subscribe.${price.period}`)}</Button>`, keyed by `price.period`. |
| W7 | `web/src/features/subscription/components/AskTeacherCheckoutAction.tsx` | component | Props `{ hasBase: boolean; disabled: boolean; onCheckout: () => void }`. `const hintId = useId()`. Renders `<Button variant="accent" className="w-full" disabled={disabled \|\| !hasBase} aria-describedby={hasBase ? undefined : hintId} onClick={onCheckout}>{t('checkout.subscribeAskTeacher')}</Button>`, plus `{!hasBase && <p id={hintId} className="text-caption text-text-muted">{t('checkout.requiresBaseHint')}</p>}`. |
| W8 | `web/src/features/subscription/components/FakeCheckoutCard.tsx` | component | Props `{ payment: PaymentResult; isPending: boolean; onComplete: (succeeded: boolean) => void }`. `<section aria-labelledby>` card (`rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5 flex flex-col gap-3`): `<h1>` `fakeCheckout.title`; `<p>` `fakeCheckout.summary` with `{ plan: t(`plan.${payment.plan}`), period: t(`period.${payment.period}`), amount: formatMoney(Number(payment.amount.amountMinor), payment.amount.currency, i18n.language) }`; a `rounded-md bg-soft p-3` box with `fakeCheckout.card`; `<p className="text-caption text-text-muted">` `fakeCheckout.note`; a row (`flex flex-wrap gap-2`) of `<Button variant="accent">` `fakeCheckout.succeed` → `onComplete(true)`, `<Button variant="danger">` `fakeCheckout.fail` → `onComplete(false)` (both disabled while `isPending`), and `<Button variant="secondary" asChild><Link to="/student/subscription">` `fakeCheckout.cancel`. |
| W9 | `web/src/features/subscription/components/CheckoutStatusCard.tsx` | component | Props `{ payment: PaymentResult; timedOut: boolean; isFetching: boolean; onCheckAgain: () => void }`. `Succeeded`: `<h1>` `checkoutResult.succeededTitle`, `<p>` `checkoutResult.succeededBody {plan}`, and `<Button variant="accent" asChild><Link to="/student/subscription">` `checkoutResult.done`. `Failed`: `<h1>` `checkoutResult.failedTitle`, `<p>` `failedBody`, and `<Button variant="secondary" asChild><Link to="/student/subscription">` `checkoutResult.tryAgain`. `Pending` and not `timedOut`: `<div role="status" aria-live="polite">` with `<h1>` `pendingTitle` and `<p>` `pendingBody`. `Pending` and `timedOut`: `<h1>` `slowTitle`, `<p>` `slowBody`, `<Button variant="secondary" disabled={isFetching} onClick={onCheckAgain}>` `checkAgain`, and `<Button variant="ghost" asChild><Link to="/student/subscription">` `back`. Same card classes as W8. |
| W10 | `web/src/features/subscription/pages/FakeCheckoutPage.tsx` | page | `export function FakeCheckoutPage({ paymentId }: { paymentId: string })`. `payment = useGetMyPayment(paymentId)`; `completion = useFakePaymentCompletion(paymentId)`. Error → `<ContentErrorState title={t('fakeCheckout.errorTitle')} error={payment.error} onRetry={() => void payment.refetch()} />`. Pending → `<ContentListSkeleton label={t('fakeCheckout.loading')} />`. `payment.data.status !== 'Pending'` → `<Navigate to="/student/checkout-result/$paymentId" params={{ paymentId }} replace />`. Otherwise `<FakeCheckoutCard payment={payment.data} isPending={completion.isPending} onComplete={completion.complete} />`. |
| W11 | `web/src/features/subscription/pages/CheckoutResultPage.tsx` | page | `export function CheckoutResultPage({ paymentId }: { paymentId: string })`. `{ query, timedOut } = useCheckoutResult(paymentId)`. Error → `ContentErrorState` (`checkoutResult.errorTitle`, retry refetch). Pending → `ContentListSkeleton` (`checkoutResult.loading`). Otherwise `<CheckoutStatusCard payment={query.data} timedOut={timedOut} isFetching={query.isFetching} onCheckAgain={() => void query.refetch()} />`. |
| W12 | `web/src/routes/student/fake-checkout.$paymentId.tsx` | route | `createFileRoute('/student/fake-checkout/$paymentId')({ component: FakeCheckoutRoute })`. `FakeCheckoutRoute` reads `Route.useParams()` and renders `<FakeCheckoutPage paymentId={paymentId} />` (mirror `exam-result.$sessionId.tsx`). |
| W13 | `web/src/routes/student/checkout-result.$paymentId.tsx` | route | Same shape, rendering `CheckoutResultPage`. |

**i18n (`features/subscription/i18n`). Keys and exact strings (ar / en):**
- `period.Monthly` شهري / Monthly · `period.Termly` فصل دراسي / Term · `period.Yearly` سنوي / Yearly
- `checkout.subscribe.Monthly` اشترك شهريًا / Subscribe monthly · `.Termly` اشترك لفصل دراسي / Subscribe for a term · `.Yearly` اشترك سنويًا / Subscribe yearly
- `checkout.subscribeAskTeacher` اشترك / Subscribe · `checkout.requiresBaseHint` اشترك في الباقة الأساسية أولًا / Subscribe to Base first · `checkout.redirecting` جارٍ التحويل إلى صفحة الدفع… / Redirecting to payment…
- `fakeCheckout.title` Paymob checkout (محاكاة) / Paymob checkout (simulation) · `fakeCheckout.summary` الباقة: {plan} · {period} — المبلغ: {amount} / Plan: {plan} · {period} — Amount: {amount} · `fakeCheckout.card` رقم البطاقة: •••• •••• •••• •••• أو محفظة إلكترونية / Card number: •••• •••• •••• •••• or mobile wallet · `fakeCheckout.note` في النظام الحقيقي لا يفعّل العميل الاشتراك؛ Paymob يرسل Webhook موقّع (HMAC) للخادم. / In the real system the client never activates the plan; Paymob sends a signed (HMAC) webhook to the server. · `fakeCheckout.succeed` نجاح الدفع / Payment succeeds · `fakeCheckout.fail` فشل الدفع / Payment fails · `fakeCheckout.cancel` إلغاء / Cancel · `fakeCheckout.loading` جارٍ تحميل الدفع… / Loading payment… · `fakeCheckout.errorTitle` تعذّر تحميل الدفع. / Could not load the payment.
- `checkoutResult.loading` جارٍ تحميل الدفع… / Loading payment… · `.errorTitle` تعذّر تحميل الدفع. / Could not load the payment. · `.pendingTitle` جارٍ تأكيد الدفع… / Confirming your payment… · `.pendingBody` ننتظر تأكيد Paymob. لا تغلق الصفحة. / Waiting for Paymob to confirm. Keep this page open. · `.slowTitle` لم يصل التأكيد بعد / Confirmation has not arrived yet · `.slowBody` ستُفعَّل باقتك فور تأكيد Paymob للدفع. يمكنك التحقق مرة أخرى أو العودة لاحقًا. / Your plan activates as soon as Paymob confirms the payment. Check again or come back later. · `.checkAgain` تحقّق مرة أخرى / Check again · `.back` العودة إلى الاشتراك / Back to subscription · `.succeededTitle` تم الدفع بنجاح / Payment successful · `.succeededBody` تم تفعيل باقة {plan}. / Your {plan} plan is active. · `.failedTitle` فشل الدفع / Payment failed · `.failedBody` لم يتم تفعيل الاشتراك. / Your plan was not activated. · `.done` حسنًا / Done · `.tryAgain` حاول مرة أخرى / Try again

**Postman (F-P).** New requests:
- **Start Base checkout:** `POST {{baseUrl}}/api/subscriptions/checkout` with body `{"plan":"Base","period":"Monthly"}`. Test: status is 200 or 400. On 200 it sets `checkoutPaymentId` from `paymentId` and asserts that `redirectUrl` starts with `/student/fake-checkout/`.
- **Get checkout payment:** `GET /api/subscriptions/payments/{{checkoutPaymentId}}`. Test: 200.
- **Complete fake payment:** `POST /api/subscriptions/payments/{{checkoutPaymentId}}/fake-completion` with body `{"succeeded":true}`. Test: 200 and `status === "Succeeded"`.
- **Get my entitlement after payment:** `GET /api/subscriptions/entitlement`. Test: 200 and `tier === "Base"`.

Folder description: "…Checkout creates a Pending payment. With the fake gateway (default) complete it via fake-completion. A second run on the same student returns 400 CHECKOUT_PLAN_ALREADY_ACTIVE."

## Error codes
| Constant | Value | Class | Thrown by | Exception type | HTTP | en | ar |
|---|---|---|---|---|---|---|---|
| `CheckoutPlanAlreadyActive` | `CHECKOUT_PLAN_ALREADY_ACTIVE` | Domain | `StudentEntitlement.EnsureCanPurchase` | `BusinessRuleViolationCoreException` | 400 | This plan is already active. | هذه الباقة مفعّلة بالفعل. |
| `CheckoutRequiresBase` | `CHECKOUT_REQUIRES_BASE` | Domain | `StudentEntitlement.EnsureCanPurchase` | `BusinessRuleViolationCoreException` | 400 | Ask a Teacher requires an active Base plan. | اسأل معلّم يتطلب الباقة الأساسية. |
| `CheckoutPlanRequired` | `CHECKOUT_PLAN_REQUIRED` | Application | `StartCheckoutValidator` | validation | 422 | Choose a plan. | اختر الباقة. |
| `CheckoutPlanInvalid` | `CHECKOUT_PLAN_INVALID` | Application | `StartCheckoutValidator` | validation | 422 | Unknown plan. | الباقة غير معروفة. |
| `CheckoutPeriodRequired` | `CHECKOUT_PERIOD_REQUIRED` | Application | `StartCheckoutValidator` | validation | 422 | Choose a billing period. | اختر مدة الاشتراك. |
| `CheckoutPeriodInvalid` | `CHECKOUT_PERIOD_INVALID` | Application | `StartCheckoutValidator` | validation | 422 | Unknown billing period. | مدة الاشتراك غير معروفة. |
| `CheckoutPeriodUnavailable` | `CHECKOUT_PERIOD_UNAVAILABLE` | Application | validator (422); `StartCheckoutHandler`, `CompleteFakePaymentHandler` (400) | validation / `BadRequestCoreException` | 422 / 400 | This billing period is not available for this plan. | مدة الاشتراك هذه غير متاحة لهذه الباقة. |
| `PaymentNotFound` | `PAYMENT_NOT_FOUND` | Application | `GetMyPaymentHandler`, `CompleteFakePaymentHandler` | `NotFoundCoreException` | 404 | Payment not found. | لم يتم العثور على عملية الدفع. |
| `PaymentGatewayUnavailable` | `PAYMENT_GATEWAY_UNAVAILABLE` | Application | `PaymobPaymentGateway`, `FakePaymentGateway` (Production) | `ServiceUnavailableCoreException` | 503 | The payment service is unavailable. Try again in a moment. | تعذّر الاتصال ببوابة الدفع. حاول مرة أخرى بعد قليل. |
| `FakeCheckoutUnavailable` | `FAKE_CHECKOUT_UNAVAILABLE` | Application | `CompleteFakePaymentHandler` | `NotFoundCoreException` | 404 | Simulated checkout is not available. | الدفع التجريبي غير متاح. |

Existing codes reused: `USER_NOT_AUTHENTICATED` (401), `USER_NOT_FOUND` (404), `PAYMENT_NOT_PENDING` (400, domain). The web `common:errors` gets all 10 rows above plus `PAYMENT_NOT_PENDING` ("This payment was already processed." / "تمت معالجة هذا الدفع بالفعل."), with the same strings as the resx.

## Domain behaviour
`StudentEntitlement` (record) gains:
```csharp
public void EnsureCanPurchase(SubscriptionPlan plan)
{
    if (plan == SubscriptionPlan.Base && BaseSubscription is not null)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.CheckoutPlanAlreadyActive);
    }

    if (plan == SubscriptionPlan.AskTeacher && BaseSubscription is null)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.CheckoutRequiresBase);
    }

    if (plan == SubscriptionPlan.AskTeacher && AskTeacherSubscription is not null)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.CheckoutPlanAlreadyActive);
    }
}
```
It is a pure guard with no state, so no `UpdationDate`. `Payment.MarkSucceeded`/`MarkFailed` and `Subscription.Start` are reused unchanged; they already set `UpdationDate` and guard `PAYMENT_NOT_PENDING`.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| POST | `/api/subscriptions/checkout` (`Name = "StartCheckout"`) | `DefaultCodes.SubscriptionManage` | `[FromBody] StartCheckoutCommand` `{ plan, period }` | 200 `CheckoutResult` `{ paymentId, redirectUrl, amount }`; 400 `CHECKOUT_PLAN_ALREADY_ACTIVE` / `CHECKOUT_REQUIRES_BASE`; 422; 503 `PAYMENT_GATEWAY_UNAVAILABLE` |
| GET | `/api/subscriptions/payments/{paymentId:guid}` (`Name = "GetMyPayment"`) | `DefaultCodes.SubscriptionManage` | route | 200 `PaymentResult`; 404 `PAYMENT_NOT_FOUND` |
| POST | `/api/subscriptions/payments/{paymentId:guid}/fake-completion` (`Name = "CompleteFakePayment"`) | `DefaultCodes.SubscriptionManage` | `[FromBody] CompleteFakePaymentRequest` `{ succeeded }` → `new CompleteFakePaymentCommand(paymentId, request.Succeeded)` | 200 `PaymentResult`; 400 `PAYMENT_NOT_PENDING`; 404 `PAYMENT_NOT_FOUND` / `FAKE_CHECKOUT_UNAVAILABLE` |

Each action carries `[ProducesResponseType<T>(StatusCodes.Status200OK)]`, as its neighbours do.

## Docs (docs-sync)
| Doc | Change |
|---|---|
| `docs/subscriptions.md` | Add a "Checkout" section after "Payments": the rules from D6, D7 and D8; the Pending payment created before the redirect; the gateway failure saving nothing (D9); `redirection_url` (D11); the result page polling (D12); the fake completion and its Production lock (D3, D4); `PaymentSettlement.Settle` as the one settle routine #101 calls. Add the 3 routes to the API table. Replace "It has no subscribe or cancel buttons yet" with the subscribe-button rules (D13) and "no cancel button yet (#101)". In "For later stories", change the #100 bullet to done, and add under #101 "call `PaymentSettlement.Settle` from the webhook; guard double payment for the same plan (D18)". |
| `docs/paymob.md` (new) | Mirrors `docs/otp-delivery.md`: the provider switch (`Payments:Provider` Fake/Paymob), a key table for `Payments` and `Payments:Paymob` with defaults and meaning, secrets in env vars only (`Payments__Paymob__SecretKey`…), the startup validation rules (F16), the Intention API flow (D1) with the request and response fields, **"Verify before go-live"** (D2 list), the fake flow and its Production lock, and go-live steps (create integrations for card and wallet → copy ids, secret key and public key → set `RedirectionUrl` → set `NotificationUrl` once #101 ships → switch the provider). |
| `docs/PRD.md` §11.2 | After "Card and mobile wallet via Paymob checkout.", add: "Checkout creates a pending payment and redirects to Paymob's unified checkout; the return page only reads the payment status and never activates a plan. A plan already held cannot be bought again, and Ask a Teacher needs an active Base. Configuration and go-live: `docs/paymob.md`." |
| `docs/claude-design-prompt.md` §4 | Replace the `#/student/subscription` line's "simulated Paymob modal with success and failure" with: "subscribe buttons per Base period and for Ask a Teacher (disabled without Base); checkout redirects to Paymob, or with the fake gateway to `#/student/fake-checkout/:paymentId` (simulated Paymob page with success, failure and cancel); `#/student/checkout-result/:paymentId` shows confirming, success, failure or still-confirming." |

## Test plan

### api — Domain
| # | Test class | Test method | Asserts |
|---|---|---|---|
| T1 | `StudentEntitlementTests` | `EnsureCanPurchase_FreeStudentBase_DoesNotThrow` | `Free.EnsureCanPurchase(Base)` does not throw |
| T2 | `StudentEntitlementTests` | `EnsureCanPurchase_EntitledBase_ThrowsCheckoutPlanAlreadyActive` | `BusinessRuleViolationCoreException`, code `CHECKOUT_PLAN_ALREADY_ACTIVE` |
| T3 | `StudentEntitlementTests` | `EnsureCanPurchase_AskTeacherWithoutBase_ThrowsCheckoutRequiresBase` | code `CHECKOUT_REQUIRES_BASE` |
| T4 | `StudentEntitlementTests` | `EnsureCanPurchase_AskTeacherWithBase_DoesNotThrow` | does not throw |
| T5 | `StudentEntitlementTests` | `EnsureCanPurchase_AskTeacherAlreadyEntitled_ThrowsCheckoutPlanAlreadyActive` | code `CHECKOUT_PLAN_ALREADY_ACTIVE` |

### api — Application
| # | Test class | Test method | Asserts |
|---|---|---|---|
| T6 | `SubscriptionsOptionsTests` | `PriceFor_BaseConfiguredPeriod_ReturnsConfiguredPrice` | Termly → Months 4, AmountMinor 69900 |
| T7 | `SubscriptionsOptionsTests` | `PriceFor_BasePeriodNotConfigured_ReturnsNull` | only Monthly configured; Yearly → null |
| T8 | `SubscriptionsOptionsTests` | `PriceFor_AskTeacherMonthly_ReturnsOneMonthAtAddOnPrice` | Months 1, AmountMinor 9900 |
| T9 | `SubscriptionsOptionsTests` | `PriceFor_AskTeacherTermly_ReturnsNull` | null |
| T10 | `StartCheckoutValidatorTests` | `Validate_BaseMonthly_Passes` | valid |
| T11 | `StartCheckoutValidatorTests` | `Validate_PlanMissing_FailsWithPlanRequired` | code `CHECKOUT_PLAN_REQUIRED` |
| T12 | `StartCheckoutValidatorTests` | `Validate_PlanOutOfRange_FailsWithPlanInvalid` | `(SubscriptionPlan)99` → `CHECKOUT_PLAN_INVALID`, and no `CHECKOUT_PERIOD_UNAVAILABLE` |
| T13 | `StartCheckoutValidatorTests` | `Validate_PeriodMissing_FailsWithPeriodRequired` | `CHECKOUT_PERIOD_REQUIRED` |
| T14 | `StartCheckoutValidatorTests` | `Validate_PeriodOutOfRange_FailsWithPeriodInvalid` | `CHECKOUT_PERIOD_INVALID` |
| T15 | `StartCheckoutValidatorTests` | `Validate_AskTeacherYearly_FailsWithPeriodUnavailable` | `CHECKOUT_PERIOD_UNAVAILABLE` |
| T16 | `StartCheckoutValidatorTests` | `Validate_BasePeriodNotConfigured_FailsWithPeriodUnavailable` | only Monthly configured; Base Yearly → `CHECKOUT_PERIOD_UNAVAILABLE` |
| T17 | `StartCheckoutHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException` + code; Save `DidNotReceive` |
| T18 | `StartCheckoutHandlerTests` | `Handle_FreeStudentBaseMonthly_AddsPendingPaymentAtConfiguredPrice` | the `AddAsync` arg is Pending, Base, Monthly, 19900 EGP, `StudentId` = user; Save `Received(1)`; result amount = 19900 EGP; result `PaymentId` = added payment id |
| T19 | `StartCheckoutHandlerTests` | `Handle_FreeStudentBaseMonthly_ReturnsGatewayRedirectAndSendsCustomer` | result `RedirectUrl` = the gateway URL; gateway received a request whose `PaymentId` = result id, `Amount` 19900 EGP, `Customer` ("Mona Ali", null, "01012345678") |
| T20 | `StartCheckoutHandlerTests` | `Handle_EntitledBase_ThrowsCheckoutPlanAlreadyActive` | code; gateway `DidNotReceive`; Save `DidNotReceive` |
| T21 | `StartCheckoutHandlerTests` | `Handle_AskTeacherWithoutBase_ThrowsCheckoutRequiresBase` | code; Save `DidNotReceive` |
| T22 | `StartCheckoutHandlerTests` | `Handle_AskTeacherWithBase_AddsAskTeacherPaymentAtAddOnPrice` | added payment AskTeacher, Monthly, 9900 |
| T23 | `StartCheckoutHandlerTests` | `Handle_PeriodUnavailable_ThrowsBadRequest` | AskTeacher Termly (with Base) → `BadRequestCoreException` `CHECKOUT_PERIOD_UNAVAILABLE`; Save `DidNotReceive` |
| T24 | `StartCheckoutHandlerTests` | `Handle_UserMissing_ThrowsUserNotFound` | `NotFoundCoreException` `USER_NOT_FOUND`; Save `DidNotReceive` |
| T25 | `StartCheckoutHandlerTests` | `Handle_GatewayUnavailable_PropagatesAndSavesNothing` | gateway throws `ServiceUnavailableCoreException(PAYMENT_GATEWAY_UNAVAILABLE)` → same exception; `AddAsync` and Save `DidNotReceive` |
| T26 | `GetMyPaymentHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | type + code |
| T27 | `GetMyPaymentHandlerTests` | `Handle_OwnPendingPayment_ReturnsPendingResult` | result id, status Pending, amount, `CompletedAt` null |
| T28 | `GetMyPaymentHandlerTests` | `Handle_OtherStudentsPayment_ThrowsPaymentNotFound` | the repository predicate is compiled over a list holding the other student's payment → `NotFoundCoreException` `PAYMENT_NOT_FOUND` |
| T29 | `PaymentSettlementTests` | `Settle_Succeeded_StartsSubscriptionForPaymentPlan` | returned subscription: StudentId, Plan, Period, Active, start = completedAt, end = completedAt + months, `PaymobReference` = txn; payment Succeeded with `SubscriptionId`, txn, raw, `CompletedAt` |
| T30 | `PaymentSettlementTests` | `Settle_Failed_MarksFailedAndReturnsNull` | null; payment Failed, txn set, `SubscriptionId` null |
| T31 | `PaymentSettlementTests` | `Settle_PaymentNotPending_ThrowsPaymentNotPending` | `BusinessRuleViolationCoreException` `PAYMENT_NOT_PENDING` |
| T32 | `CompleteFakePaymentHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | type + code; Save `DidNotReceive` |
| T33 | `CompleteFakePaymentHandlerTests` | `Handle_GatewayCannotSimulate_ThrowsFakeCheckoutUnavailable` | `NotFoundCoreException` `FAKE_CHECKOUT_UNAVAILABLE`; Save `DidNotReceive` |
| T34 | `CompleteFakePaymentHandlerTests` | `Handle_PaymentNotOwned_ThrowsPaymentNotFound` | `PAYMENT_NOT_FOUND`; Save `DidNotReceive` |
| T35 | `CompleteFakePaymentHandlerTests` | `Handle_Succeeded_AddsSubscriptionAndReturnsSucceeded` | `subscriptionRepository.AddAsync` received a Base Monthly subscription whose end is now + 1 month; result status Succeeded; payment `PaymobTransactionId` = `"fake-" + id:N`; Save `Received(1)` |
| T36 | `CompleteFakePaymentHandlerTests` | `Handle_Failed_ReturnsFailedWithoutSubscription` | result Failed; `subscriptionRepository.AddAsync` `DidNotReceive`; Save `Received(1)` |
| T37 | `CompleteFakePaymentHandlerTests` | `Handle_PaymentAlreadyCompleted_ThrowsPaymentNotPending` | `PAYMENT_NOT_PENDING`; Save `DidNotReceive` |

Handler tests use `FakeTimeProvider` or a `TimeProvider` substitute as in `GetMyEntitlementHandlerTests`, and NSubstitute for the repositories and `IPaymentGateway`.

### api — Infrastructure
| # | Test class | Test method | Asserts |
|---|---|---|---|
| T38 | `PaymobPaymentGatewayTests` | `StartCheckoutAsync_ValidRequest_PostsToIntentionEndpointWithTokenAuth` | POST `https://accept.paymob.com/v1/intention/`; `Authorization` "Token sk_test"; User-Agent `Elmanhg/1.0` |
| T39 | `PaymobPaymentGatewayTests` | `StartCheckoutAsync_ValidRequest_SendsAmountMethodsItemsAndReferences` | JSON body: `amount` 19900, `currency` "EGP", `payment_methods` [111, 222], `items[0]` {name "Elmanhg Base Monthly", amount 19900, quantity 1}, `special_reference` = payment id, `notification_url`, `redirection_url` "https://app.test/student/checkout-result/{id}" |
| T40 | `PaymobPaymentGatewayTests` | `StartCheckoutAsync_ValidRequest_SendsBillingDataFromCustomer` | `first_name` "Mona", `last_name` "Ali", `phone_number` "01012345678", `email` "NA", `country` "EG", `street` "NA" |
| T41 | `PaymobPaymentGatewayTests` | `StartCheckoutAsync_SingleWordName_SendsNaLastName` | `last_name` "NA" |
| T42 | `PaymobPaymentGatewayTests` | `StartCheckoutAsync_NotificationUrlBlank_OmitsNotificationUrl` | no `notification_url` property |
| T43 | `PaymobPaymentGatewayTests` | `StartCheckoutAsync_Success_ReturnsUnifiedCheckoutUrl` | `https://accept.paymob.com/unifiedcheckout/?publicKey=pk_test&clientSecret=egy_csk_test_1` |
| T44 | `PaymobPaymentGatewayTests` | `StartCheckoutAsync_ServerError_ThrowsPaymentGatewayUnavailable` | 500 → `ServiceUnavailableCoreException` `PAYMENT_GATEWAY_UNAVAILABLE` |
| T45 | `PaymobPaymentGatewayTests` | `StartCheckoutAsync_NetworkFailure_ThrowsPaymentGatewayUnavailable` | `Throw = new HttpRequestException()` → same |
| T46 | `PaymobPaymentGatewayTests` | `StartCheckoutAsync_MissingClientSecret_ThrowsPaymentGatewayUnavailable` | body `{"id":"pi_1"}` → same |
| T47 | `PaymobPaymentGatewayTests` | `StartCheckoutAsync_InvalidJsonBody_ThrowsPaymentGatewayUnavailable` | body `not-json` → same |
| T48 | `FakePaymentGatewayTests` | `StartCheckoutAsync_Development_ReturnsFakeCheckoutPathForPayment` | `/student/fake-checkout/{id}` |
| T49 | `FakePaymentGatewayTests` | `StartCheckoutAsync_Production_ThrowsPaymentGatewayUnavailable` | type + code |
| T50 | `FakePaymentGatewayTests` | `SupportsSimulatedCompletion_Production_IsFalse` | false |
| T51 | `FakePaymentGatewayTests` | `SupportsSimulatedCompletion_Testing_IsTrue` | true |
| T52 | `PaymentsOptionsValidatorTests` | `Validate_FakeProviderWithBlankPaymobSettings_Succeeds` | Succeeded |
| T53 | `PaymentsOptionsValidatorTests` | `Validate_CompletePaymobSettings_Succeeds` | Succeeded |
| T54 | `PaymentsOptionsValidatorTests` | `Validate_PaymobRequiredValueBlank_FailsNamingKey` (Theory: `SecretKey`, `PublicKey`, `RedirectionUrl`, `BillingCountry`) | failure message contains `Payments:Paymob:{key} is required` |
| T55 | `PaymentsOptionsValidatorTests` | `Validate_PaymobWithoutIntegrationIds_Fails` | IntegrationIds message |
| T56 | `PaymentsOptionsValidatorTests` | `Validate_PaymobNonPositiveIntegrationId_Fails` | `[0]` → IntegrationIds message |
| T57 | `PaymentsOptionsValidatorTests` | `Validate_PaymobUrlNotHttps_FailsRequiringHttps` (Theory: `BaseUrl`, `CheckoutUrl`, `RedirectionUrl`, `NotificationUrl` set to an `http://` value) | `must be an absolute https URL` for that key |
| T58 | `PaymentsOptionsValidatorTests` | `Validate_AttemptTimeoutAboveTotal_Fails` | timeout message |
| T59 | `PaymentsServiceCollectionExtensionsTests` | `AddPayments_NoConfiguration_ResolvesFakeGateway` | empty config → `FakePaymentGateway` |
| T60 | `PaymentsServiceCollectionExtensionsTests` | `AddPayments_PaymobProvider_ResolvesPaymobGateway` | `PaymobPaymentGateway`; `SupportsSimulatedCompletion` false |
| T61 | `PaymentsServiceCollectionExtensionsTests` | `AddPayments_PaymobWithoutSecretKey_FailsStartupValidation` | `IStartupValidator.Validate()` throws `OptionsValidationException` with `*SecretKey*` |
| T62 | `PaymentsServiceCollectionExtensionsTests` | `AddPayments_PaymobServerError_MakesExactlyOneAttempt` | stub handler 500 via `ConfigurePrimaryHttpMessageHandler` → throws `PAYMENT_GATEWAY_UNAVAILABLE`, `CallCount == 1` |

Helper (not a test class): `api/Elmanhg.Tests/Infrastructure/Payments/PaymentsTestSettings.cs`, a static class with `Fake()`, `WithPaymob()` (SecretKey `sk_test`, PublicKey `pk_test`, IntegrationIds [111, 222], RedirectionUrl `https://app.test/student/checkout-result`, NotificationUrl `https://api.test/api/payments/paymob/webhook`) and `ToConfiguration(PaymentsOptions)` (a key/value dictionary for the DI tests). This mirrors `OtpDeliveryTestSettings`.

### api — Integration (`Tests/Integration/Subscriptions/`)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| T63 | `CheckoutEndpointTests` | `Post_Anonymous_Returns401` | 401 |
| T64 | `CheckoutEndpointTests` | `Post_Teacher_Returns403` | 403 |
| T65 | `CheckoutEndpointTests` | `Post_FreeStudentBaseMonthly_ReturnsFakeRedirectAndStoresPendingPayment` | 200; `redirectUrl` = `/student/fake-checkout/{paymentId}`; `amount.amountMinor` 19900; a fresh DbContext holds that Payment, Pending, Base, Monthly, StudentId = student |
| T66 | `CheckoutEndpointTests` | `Post_AskTeacherWithoutBase_Returns400CheckoutRequiresBase` | 400 + `code` `CHECKOUT_REQUIRES_BASE`; no payment row for the student |
| T67 | `CheckoutEndpointTests` | `Post_BaseAlreadyEntitled_Returns400PlanAlreadyActive` | seeded Active Base → 400 `CHECKOUT_PLAN_ALREADY_ACTIVE` |
| T68 | `CheckoutEndpointTests` | `Post_PlanMissing_Returns422WithCode` | `{ "period": "Monthly" }` → 422, `code` contains `CHECKOUT_PLAN_REQUIRED` |
| T69 | `PaymentEndpointTests` | `Get_Anonymous_Returns401` | 401 |
| T70 | `PaymentEndpointTests` | `Get_OwnPendingPayment_Returns200WithPendingStatus` | 200, `status` "Pending", id |
| T71 | `PaymentEndpointTests` | `Get_OtherStudentsPayment_Returns404PaymentNotFound` | 404 + `PAYMENT_NOT_FOUND` |
| T72 | `FakePaymentCompletionEndpointTests` | `Post_Anonymous_Returns401` | 401 |
| T73 | `FakePaymentCompletionEndpointTests` | `Post_Succeeded_ActivatesBase` | 200 `status` "Succeeded"; then `GET /api/subscriptions/entitlement` → `tier` "Base"; a fresh DbContext holds an Active Base subscription and the payment's `SubscriptionId` set to it |
| T74 | `FakePaymentCompletionEndpointTests` | `Post_Failed_MarksFailedAndStudentStaysFree` | 200 `status` "Failed"; entitlement `tier` "Free"; no subscription row |
| T75 | `FakePaymentCompletionEndpointTests` | `Post_AlreadyCompleted_Returns400PaymentNotPending` | second call → 400 `PAYMENT_NOT_PENDING` |
| T76 | `FakePaymentCompletionEndpointTests` | `Post_OtherStudentsPayment_Returns404PaymentNotFound` | 404; the payment stays Pending in the DB |

The existing `EndpointAuthorizationTests` already fails on any unpoliced action; no change is needed there.

### web
| # | Test file | `it(...)` | Asserts |
|---|---|---|---|
| T77 | `features/subscription/pages/SubscriptionPage.checkout.test.tsx` | `shows a subscribe button for each Base period for a free student` | within article "Base": buttons "Subscribe monthly", "Subscribe for a term", "Subscribe yearly" |
| T78 | same | `disables Ask a Teacher subscribe with a hint when the student has no Base` | the button "Subscribe" in "Ask a Teacher" is disabled and has the accessible description "Subscribe to Base first" |
| T79 | same | `enables Ask a Teacher subscribe for a Base student` | Base entitlement → enabled; Base card has no subscribe buttons |
| T80 | same | `hides subscribe buttons for active plans` | Base + AskTeacher → no button named /Subscribe/ |
| T81 | same | `opens the simulated checkout after choosing a period` | the MSW `StartCheckout` handler captures body `{plan:'Base', period:'Termly'}` and returns `checkoutResult()`; `GetMyPayment` returns pending → heading "Paymob checkout (simulation)" visible |
| T82 | same | `redirects to Paymob when checkout returns an external URL` | `vi.mock('@/shared/lib/redirect', async (orig) => ({ ...(await orig()), redirectToExternal: vi.fn() }))`; `redirectToExternal` called with `https://accept.paymob.com/unifiedcheckout/?publicKey=pk&clientSecret=cs` |
| T83 | same | `shows an error toast when checkout fails` | 503 `PAYMENT_GATEWAY_UNAVAILABLE` → text "The payment service is unavailable. Try again in a moment."; buttons re-enabled |
| T84 | `features/subscription/pages/FakeCheckoutPage.test.tsx` | `shows loading then the simulated checkout with plan and amount` | status "Loading payment…", then "Plan: Base · Monthly — Amount: EGP 199" (whitespace-normalised) |
| T85 | same | `shows the success result after a successful simulated payment` | click "Payment succeeds" → the completion handler returns Succeeded → heading "Payment successful" and "Your Base plan is active." |
| T86 | same | `shows the failure result after a failed simulated payment` | click "Payment fails" → "Payment failed" and a "Try again" link to `/student/subscription` |
| T87 | same | `returns to the subscription page on cancel` | click "Cancel" → heading "Subscribe to Elmanhg" |
| T88 | same | `goes to the result when the payment is no longer pending` | GET returns Succeeded → "Payment successful" without clicks |
| T89 | same | `shows an error with retry when the payment cannot be loaded` | 404 once → alert "Could not load the payment." + "Payment not found."; Retry → the checkout heading |
| T90 | same | `shows an error toast when simulated completion is refused` | 404 `FAKE_CHECKOUT_UNAVAILABLE` → toast "Simulated checkout is not available." |
| T91 | same | `renders right-to-left in Arabic` | heading "Paymob checkout (محاكاة)", `dir="rtl"` |
| T92 | same | `has no axe violations` | `axe(container).violations` empty |
| T93 | `features/subscription/pages/CheckoutResultPage.test.tsx` | `shows loading then the success message for a succeeded payment` | "Loading payment…" then "Payment successful" + "Done" link |
| T94 | same | `shows the failure message with try again for a failed payment` | "Payment failed", "Try again" |
| T95 | same | `polls a pending payment until it succeeds` | `vi.useFakeTimers({ shouldAdvanceTime: true })`; first GET Pending (`once`), then Succeeded; "Confirming your payment…" status, `advanceTimersByTimeAsync(checkoutPollIntervalMs)`, then "Payment successful" |
| T96 | same | `shows the still-confirming message after the confirmation window` | always Pending; advance past `checkoutConfirmationTimeoutMs` → "Confirmation has not arrived yet" + "Check again" |
| T97 | same | `check again shows the result once confirmed` | after timeout, set the handler to Succeeded, click "Check again" → "Payment successful" |
| T98 | same | `shows an error with retry when the payment cannot be loaded` | 500 once → alert + Retry → result |
| T99 | same | `renders right-to-left in Arabic` | "تم الدفع بنجاح", `dir="rtl"` |
| T100 | same | `has no axe violations` | empty violations (pending state) |
| T101 | `shared/lib/redirect.test.ts` | `treats a same-origin path as in-app` | `isInAppUrl('/student/fake-checkout/x')` true |
| T102 | same | `treats an absolute URL as external` | `https://accept.paymob.com/…` false |
| T103 | same | `treats a protocol-relative URL as external` | `//evil.test/x` false |

Existing tests (`SubscriptionPage.test.tsx`, `SubscriptionPage.payments.test.tsx` and all api tests) must stay green unmodified. Only the files listed in "Existing code touched" may change, and only additively.

## Definition of done
- [ ] `IPaymentGateway` in Application; `FakePaymentGateway` is the default; `PaymobPaymentGateway` is selected by `Payments:Provider=Paymob`; no package added.
- [ ] Paymob uses the Intention API (`POST v1/intention/`, `Token` auth) and returns the unified-checkout URL; POST retries disabled; timeouts from options.
- [ ] `PaymentsOptions` has `ValidateDataAnnotations` + `PaymentsOptionsValidator` + `ValidateOnStart`; Paymob keys are required only when the provider is Paymob; no secret in any committed file.
- [ ] The fake cannot start checkout or settle in Production (503 / 404).
- [ ] `StartCheckout` refuses a held plan (`CHECKOUT_PLAN_ALREADY_ACTIVE`) and Ask a Teacher without Base (`CHECKOUT_REQUIRES_BASE`); prices come from `SubscriptionsOptions.PriceFor`; a gateway failure saves nothing.
- [ ] `GetMyPayment` is owner-scoped (404 for another student) and returns Pending.
- [ ] `CompleteFakePayment` settles through `PaymentSettlement.Settle`; success starts a subscription and entitlement reads Base; failure leaves the student Free.
- [ ] Both commands implement `IAuditableCommand`; all 3 actions carry `DefaultCodes.SubscriptionManage`.
- [ ] 10 new error codes in the right `ErrorCodes` class, both resx files, and web `common:errors` (plus `PAYMENT_NOT_PENDING` on the web).
- [ ] No migration; `AppDbContextTests` migration list unchanged.
- [ ] `appsettings.example.json`, `ApiFactory`, `.env.example` and the code defaults all carry `Payments`; `dotnet test api/ -c Release` is green with `appsettings.json` moved aside.
- [ ] `api/openapi/v1.json` and the Orval client are regenerated with no drift; `routeTree.gen.ts` regenerated.
- [ ] Postman "Subscriptions" folder has the 4 new requests in state order.
- [ ] Web: per-period Base buttons, Ask a Teacher disabled with hint without Base, no buttons on active plans, a status while redirecting, an error toast on failure.
- [ ] Web: fake checkout page and result page cover loading, error + retry, every payment state, the polling timeout, RTL and axe.
- [ ] Web: no `window.location` in features; tokens only; logical properties; all strings in ar + en.
- [ ] Every test T1–T103 exists with that name and is mutation-checked (break the line, the test fails, restore).
- [ ] Docs updated: `docs/subscriptions.md`, new `docs/paymob.md`, `docs/PRD.md` §11.2, `docs/claude-design-prompt.md` §4.
- [ ] `dotnet build` shows 0 new warnings; `dotnet format` clean outside `core-libraries`; web `tsc`, `eslint --max-warnings=0`, `prettier --check … --end-of-line auto` and `vitest --coverage` all pass.
- [ ] The guard grep (`DateTime.Now|UtcNow`, `.Result`, `new HttpClient(`, `FromSqlRaw`, `async void`) prints nothing.
