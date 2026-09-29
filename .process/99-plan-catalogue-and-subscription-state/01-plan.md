# Plan — [E10.S1] Plan catalogue and subscription state (#99)

## Goal
After this ships the platform has a real subscription model: a configured catalogue of the Free, Base (monthly / termly / yearly) and Ask a Teacher (monthly add-on) plans with prices, periods and quotas; `Subscription` and `Payment` aggregates with the PRD §11.2 lifecycle (Active, PastDue, Cancelled, Expired) and the payment states Paymob will drive; and one entitlement definition (derived from status and dates, never stored) that the free-tier gate (#87) and the Ask a Teacher gate (E9) call. Anyone can read the plan catalogue (`GET /api/plans`). A student can read their current entitlement and payment history, and the `/student/subscription` screen shows the aurora header, their current plan, the three plan cards and their payment log. Until #100/#101 wire Paymob, every student resolves to Free and the payment log is empty.

## Scope
**In:**
- Domain: `Subscription`, `Payment`, `Money`, the enums, `SubscriptionEntitlementSpecification`, `StudentEntitlement`, and both repositories. Full lifecycle methods, unit-tested.
- EF mapping, the migration `AddSubscriptionsAndPayments`, and a unique filtered index on `PaymobTransactionId` (the idempotency key #101 will use).
- `SubscriptionsOptions` (section `Subscriptions`): prices, periods, quotas, grace period and currency, validated at startup.
- Queries: `GetPlanCatalogue` (anonymous), `GetMyEntitlement` (student), `GetMyPayments` (student, paged), plus the reusable `StudentEntitlementLoader` for the later gates.
- Controllers `PlansController` and `SubscriptionsController`, the OpenAPI regeneration, the Orval regeneration, and the Postman folder.
- Web: the `features/subscription` feature. It has the aurora `SubscribeHeader`, the current plan card, the Free/Base/Ask a Teacher plan cards, the paged payment log, and loading, error and RTL states. `formatMoney` goes in `shared/lib/format.ts`.
- Docs: new `docs/subscriptions.md`. Edits to `docs/PRD.md` §11, §15 and §19, and to `docs/backlog.json` (the E10 page task moves to this story).

**Out (owned by a later story; this is not a deferral):**
- Subscribe/checkout buttons, the Paymob checkout modal, the real Paymob client, and Payment creation at checkout: #100.
- Webhook endpoint, HMAC, transitions driven by webhooks, the renewal/downgrade sweep job, and the xmin concurrency token on `Subscription`: #101.
- Student cancel button: #101. Cancellation must stop Paymob recurring, so it goes through Paymob.
- Refunds, `PaymentStatus.Refunded`, and the admin payments page: #102.
- The home "plan line", the daily counter, the paywall modal, and quiz/lesson/avatar enforcement: #87.
- The quota check at thread creation: #94.
- Admin "grant complimentary plan": #106. It will call `Subscription.Start` with `paymobReference: null`.
- Dashboards MRR and churn: #104.

**Deferred:** none. Nothing in this story needs credentials or an online service.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Where does the plan catalogue live? | In configuration: `SubscriptionsOptions` (section `Subscriptions`). There is no Plans table and no admin plan CRUD. | The sub-task says "Plan configuration". PRD §19 Q1 leaves prices open, so they must change without a deploy (skill §8.1). The PRD has no admin plan editor. |
| 2 | Money representation | Integer minor units plus an ISO 4217 code. Domain `Money(long AmountMinor, string Currency)`, stored as flat columns `AmountMinor bigint` and `Currency varchar(3)` on `Payment`. On the wire: `{ "amountMinor": 19900, "currency": "EGP" }`, schema name `Money`. No `decimal`, no floats. | Follows the momenta-api-contract §6 option "integer minor units, one representation per service". Paymob's `amount_cents` maps 1:1. Flat columns keep the amount in the audit diff (owned types are not diffed). |
| 3 | Defaults for prices vs limits | Prices have **no code default**. `BasePrices` is empty and `AskTeacherMonthlyPriceMinor` is 0, and `ValidateOnStart` fails the boot if they are missing. Quotas, grace period and currency default in code to the PRD values. | Constitution §2: a new section must not "silently default to 0/empty". Silent placeholder prices in production are worse than a failed boot. The limits are PRD numbers. |
| 4 | Periods | Base: `Monthly` (1 month), `Termly` (4 months), `Yearly` (12 months). Months and price come per period from config, as `BasePrices:{Period}:{Months,AmountMinor}`. Ask a Teacher: Monthly only. It is a named constant of 1 month with a PRD §11.1 WHY comment. | PRD §11.1 lists monthly / termly / yearly for Base and "monthly" for the add-on. An Egyptian school term is about 4 months. A dictionary keyed by enum (not a list) avoids the config-binder append-to-default-list trap. |
| 5 | Placeholder prices (in `appsettings.example.json`, ApiFactory and fixtures only) | Base 19900 / 69900 / 179900 minor (199 / 699 / 1,799 EGP). Ask a Teacher 9900 (99 EGP/month). | 199 and 99 are the prototype's `PRICES`. Termly and yearly are discounted multiples. All are config, and still pending the answer to PRD §19 Q1. |
| 6 | Quota defaults | Free: 10 quiz questions/day, 5 Avatar messages/day, 1 open lesson per unit. Base: 50 Avatar messages/day. Ask a Teacher: 20 questions/month, 24 h reply SLA. Grace period: 3 days. | PRD §11.1 and §11.2. The prototype has `FREE_DAILY=10`, `FREE_AVATAR=5` and `ASK_QUOTA=20`. PRD §19 Q5 leaves Base Avatar open, so 50 is a config default. |
| 7 | Lifecycle states | `Active`, `PastDue`, `Cancelled`, `Expired`. **Trialing is not used in v1.** | PRD §11.2 says "Trialing (if used)", and the story lists four states. |
| 8 | What grants access | Access is derived at read time, never stored. `EntitledUntil(grace)`: Active or PastDue gives `CurrentPeriodEnd + grace`; Cancelled gives `CurrentPeriodEnd`; Expired gives none. The subscription is entitled when `EntitledUntil > now`. | PRD §11.2: "grace 3 days, then downgrade to Free". Deriving it means the downgrade happens on time even before #101's sweep moves the status. An Active row past its end (webhook lag) behaves like PastDue. |
| 9 | Cancel semantics | Cancelled keeps access until `CurrentPeriodEnd` and then lapses. | PRD §17 rule 12: entitlement changes only via Paymob webhooks, so a student's cancel must not revoke paid time. The prototype revokes immediately; the prototype is a simulation, and the PRD wins. Recorded in `docs/subscriptions.md`. |
| 10 | "Ask a Teacher requires Base" | Enforced when entitlement is read: `HasAskTeacher = entitled Base AND entitled AskTeacher`. `Subscription.Start` does **not** guard it. | Checkout (#100) refuses to sell the add-on without Base. The read-time rule also covers Base lapsing while the add-on is still paid. |
| 11 | How Free is represented | There is no Subscription row for Free. Free means "no entitled Base". | The PRD data model has plan values Base and Ask a Teacher only. Free has no billing. |
| 12 | Which Base when there are several entitled rows | Per plan, the entitled subscription with the latest `EntitledUntil`. | Deterministic. Renewals normally extend the same row. |
| 13 | Payment ↔ Subscription | `Payment` carries its own `StudentId`, `Plan`, `Period` and amount snapshot. `SubscriptionId` is nullable and set on success. `Payment.Id` is the future Paymob `merchant_order_id`, so no Paymob order-id column is needed. | On a first purchase the payment exists before any subscription (#100 → #101). The price paid must never be recomputed from config. |
| 14 | Payment states now | `Pending`, `Succeeded`, `Failed`. `Refunded` is added by #102 (adding a string-enum value needs no migration). | This keeps this story to states that have a transition here. |
| 15 | Student payment log | `GET /api/subscriptions/payments`. It lists the student's **completed** payments only (Pending means an abandoned checkout), newest first, paged. | The prototype subscription screen shows the payment log with success/failure only. A list endpoint must be paginated (momenta §7, as in the existing `GetSessionHistory`). |
| 16 | How the gates consume entitlement | Static `StudentEntitlementLoader.LoadAsync(...)` in `Application/Subscriptions/Shared` returns `EntitlementResult`, with the limits already resolved. Gates in #87/#94 call it with their own repository, options and clock. | This follows the existing precedent `Exams/Shared/ExamSessionResultLoader`. There is one definition of both entitlement and limits, and no service layer and no nested `mediator.Send`. |
| 17 | Where the entitlement predicate lives | `SubscriptionEntitlementSpecification.EntitledFor(studentId, now, grace)` (EF-translatable, used for SQL) plus `Subscription.EntitledUntil(grace)` (C#). A domain test proves they agree for every status × time. | This mirrors the `ServableQuestionSpecification` pattern. Npgsql cannot translate `DateTimeOffset + TimeSpan` reliably, so the SQL form compares against `now - grace`. |
| 18 | Endpoint access | `GET /api/plans` is `[AllowAnonymous]`: a public catalogue that the landing page (#86) also needs. `GET /api/subscriptions/entitlement` and `/payments` use `DefaultCodes.SubscriptionManage` (Student only, PRD §16). | Skill §7.3 allows anonymous access for public catalogue reads. The policy already exists and is matrix-tested. |
| 19 | Concurrency token | None yet. #101 adds `Version` (xmin) together with the first writers (webhook, sweep). | No endpoint mutates a Subscription in this story. |
| 20 | Unique index on `PaymobTransactionId` | Added now, filtered to `IS NOT NULL`. There is **no** `SaveChangesAsync` catch mapping; #101 adds it with idempotent processing. | The column exists now, so the schema constraint belongs with it. The mapping belongs with the first writer. |
| 21 | Error-code placement | Codes thrown by the domain go in `Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` (`BusinessRuleViolationCoreException`, 400). Validator codes go in `Elmanhg.Application/Exceptions/ErrorCodes.cs`. | This matches the real repo; e.g. `ExamBlueprint` throws the domain `ErrorCodes.ExamBlueprintShortfall`. |
| 22 | Enum wire values | PascalCase strings (`"Base"`, `"PastDue"`) through the existing `JsonStringEnumConverter`. | This is the repo convention. The repo wins over momenta's SCREAMING_SNAKE. |
| 23 | Subscription page scope vs backlog | This story builds the **read-only** page: header, current plan, plan comparison and payment log. #100 adds the "اشترك" buttons and the Paymob modal. `docs/backlog.json` moves "Subscription page UI with plan comparison and current status" from E10.S2 to E10.S1 and renames E10.S2's task to "Subscribe actions and Paymob checkout modal on the subscription page". | The orchestrator asked for web in this story. Moving scope is a backlog change and must be synced (docs-sync rule). No disabled or non-functional buttons are rendered (design prompt §8). |
| 24 | Subscribe header copy | The aurora card shows h1 "اشترك في المنهج" and a tagline with the **live** servable count from the existing anonymous `GET /api/questions/servable-count`. If that request is pending or has failed, the tagline shows without the number. | The prototype hard-codes "100,000", but the design prompt says the marketed number is the real servable total. The header must not block the page. |
| 25 | Text colour on aurora | `text-surface` (white) for the title and `text-surface/80` for the tagline. | Matches prototype `.aurora { color:#fff }`. The title is display-size (large-text contrast). |
| 26 | Payment log paging on web | URL search param `paymentsPage` (Zod-validated), page size 10. The section is hidden when page 1 is empty. | Skill §4 requires pagination in the URL. The prototype renders the table only when payments exist. |
| 27 | Morabh reuse | Searched `Morabh.Domain`, `.Application`, `.Infrastructure`, `.APIs` and `Core` for subscription, plan, payment, paymob and entitlement. Found only BNPL `InstallmentPlan` (a DB-stored plan with LocalizedText) and `PaymentAttemptBase` (manual InstaPay proof review). No business logic is copied. The only shape reused is the "guard `Status == Pending`, then mutate, then stamp" of `Morabh.Domain/Orders/PaymentAttempt.cs` (`Approve`/`Invalidate` → `PAYMENT_ATTEMPT_NOT_PENDING`), mirrored as `Payment.MarkSucceeded`/`MarkFailed` → `PAYMENT_NOT_PENDING`. Everything else is new, with no Morabh equivalent. | Elmanhg deltas §5. |
| 28 | Instants | Every `DateTimeOffset` given to the domain comes from `TimeProvider.GetUtcNow()` (UTC). Npgsql rejects non-UTC `timestamptz` parameters. | Existing handlers use `TimeProvider`. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Add a `// SUBSCRIPTIONS` group: `SubscriptionPeriodInvalid`, `SubscriptionNotActive`, `SubscriptionEnded`, `SubscriptionAlreadyExpired`, `PaymentAmountInvalid`, `PaymentNotPending` (values in Error codes). |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add a `// SUBSCRIPTIONS` group: `PaymentHistoryPageNumberInvalid`, `PaymentHistoryPageSizeInvalid`. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Add all 8 keys (strings in Error codes). |
| `api/Elmanhg.Application/DependencyInjection.cs` | Register `SubscriptionsOptions` (below). |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<ISubscriptionRepository, SubscriptionRepository>(); services.AddScoped<IPaymentRepository, PaymentRepository>();` plus the usings. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Add DbSets `Subscriptions` and `Payments`, constants, `ConfigureSubscriptions(modelBuilder)` called after `ConfigureExamBlueprints`, and the two global soft-delete filter lines (see Files to create #36 for exact mapping). |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add`. |
| `api/Elmanhg.Api/appsettings.example.json` | Add a one-line `"Subscriptions"` section after `"Exams"`: `{ "Currency": "EGP", "GracePeriodDays": 3, "FreeDailyQuizQuestions": 10, "FreeDailyAvatarMessages": 5, "FreeOpenLessonsPerUnit": 1, "BaseDailyAvatarMessages": 50, "BasePrices": { "Monthly": { "Months": 1, "AmountMinor": 19900 }, "Termly": { "Months": 4, "AmountMinor": 69900 }, "Yearly": { "Months": 12, "AmountMinor": 179900 } }, "AskTeacherMonthlyQuestions": 20, "AskTeacherReplySlaHours": 24, "AskTeacherMonthlyPriceMinor": 9900, "PaymentHistoryMaxPageSize": 50 }`. Build-time OpenAPI reads this file. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add to the in-memory dictionary: `Subscriptions:Currency=EGP`, `GracePeriodDays=3`, `FreeDailyQuizQuestions=10`, `FreeDailyAvatarMessages=5`, `FreeOpenLessonsPerUnit=1`, `BaseDailyAvatarMessages=50`, `BasePrices:Monthly:Months=1`, `BasePrices:Monthly:AmountMinor=19900`, `BasePrices:Termly:Months=4`, `BasePrices:Termly:AmountMinor=69900`, `BasePrices:Yearly:Months=12`, `BasePrices:Yearly:AmountMinor=179900`, `AskTeacherMonthlyQuestions=20`, `AskTeacherReplySlaHours=24`, `AskTeacherMonthlyPriceMinor=9900`, `PaymentHistoryMaxPageSize=50` (all prefixed `Subscriptions:`). |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `twentieth => twentieth.Should().EndWith("_AddSubscriptionsAndPayments")` to the `SatisfyRespectively` list (accepted pattern). |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `postman/elmanhg.postman_collection.json` | New folder `Subscriptions` after `Progress`, containing "Get plan catalogue" (`GET {{baseUrl}}/api/plans`, `"auth": {"type":"noauth"}`, tests: status 200 and `base.prices` is an array), "Get my entitlement" (`GET {{baseUrl}}/api/subscriptions/entitlement`, tests: 200 and `tier` exists) and "Get my payments" (`GET {{baseUrl}}/api/subscriptions/payments?pageNumber=1&pageSize=20`, tests: 200 and `items` is an array). Mirror the shape of the Progress requests. |
| `web/src/routes/student/subscription.tsx` | Replace the placeholder with `createFileRoute('/student/subscription')({ validateSearch: subscriptionSearchSchema, component: SubscriptionPage })`, importing from `@/features/subscription`. |
| `web/src/app/i18n.ts` | Import `subscriptionLocales` from `@/features/subscription/locales`. Add a `subscription` namespace to `resources.ar`/`resources.en` and `'subscription'` to `ns`. |
| `web/src/shared/lib/format.ts` | Add `formatMoney` (#71 below). |
| `web/src/shared/lib/format.test.ts` | Add the 3 `formatMoney` tests (Test plan W1–W3). No existing test is changed. |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api`: new `plans/`, `subscriptions/`, model files (`Money`, `PlanCatalogueResult`, …) and zod. Never hand-edit. |
| `docs/PRD.md` | §11.1, §11.2, §15 and §19 edits (see Files to create #78). |
| `docs/backlog.json` | E10 story "Plan catalogue and subscription state": append task `"Subscription page UI with plan comparison and current status"`. E10 story "Paymob checkout integration": replace that same task string with `"Subscribe actions and Paymob checkout modal on the subscription page"`. |

## Files to create
All C# files use file-scoped namespaces, one type per file, class declarations on one line, no comments except WHY comments, and `.ConfigureAwait(false)` on every await.

### Domain — `api/Elmanhg.Domain`
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `SharedKernel/Money.cs` | value object | `namespace Elmanhg.Domain.SharedKernel; public sealed record Money(long AmountMinor, string Currency);` |
| 2 | `Subscriptions/SubscriptionPlan.cs` | enum | `public enum SubscriptionPlan { Base, AskTeacher }` |
| 3 | `Subscriptions/BillingPeriod.cs` | enum | `public enum BillingPeriod { Monthly, Termly, Yearly }` |
| 4 | `Subscriptions/SubscriptionStatus.cs` | enum + ext | `public enum SubscriptionStatus { Active, PastDue, Cancelled, Expired }` and, in the same file, `public static class SubscriptionStatusExtensions { public static bool HasEnded(this SubscriptionStatus status) => status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired; }` |
| 5 | `Subscriptions/PlanTier.cs` | enum | `public enum PlanTier { Free, Base }` |
| 6 | `Subscriptions/PaymentStatus.cs` | enum | `public enum PaymentStatus { Pending, Succeeded, Failed }` |
| 7 | `Subscriptions/Subscription.cs` | entity (partial) | `public partial class Subscription : AuditEntity, IAuditedEntity`. Properties (all `{ get; private set; }`): `Guid StudentId`, `SubscriptionPlan Plan`, `BillingPeriod Period`, `SubscriptionStatus Status`, `DateTimeOffset CurrentPeriodStart`, `DateTimeOffset CurrentPeriodEnd`, `DateTimeOffset? CancelledAt`, `DateTimeOffset? ExpiredAt`, `string? PaymobReference`. `private Subscription(Guid id, Guid? createdBy) : base(id, createdBy) { }`. `public static Subscription Start(Guid studentId, SubscriptionPlan plan, BillingPeriod period, int periodMonths, DateTimeOffset startsAt, string? paymobReference, Guid? createdBy)`. `public DateTimeOffset? EntitledUntil(TimeSpan gracePeriod)`. `public bool IsEntitledAt(DateTimeOffset now, TimeSpan gracePeriod) => EntitledUntil(gracePeriod) > now;`. `private static void EnsurePeriod(int periodMonths)`. Bodies in Domain behaviour. |
| 8 | `Subscriptions/Subscription.Lifecycle.cs` | partial | `public void Renew(int periodMonths, string? paymobReference)`, `public void MarkPastDue()`, `public void Cancel(DateTimeOffset cancelledAt)`, `public void Expire(DateTimeOffset expiredAt)`. Bodies in Domain behaviour. |
| 9 | `Subscriptions/SubscriptionEntitlementSpecification.cs` | static class | WHY comment: `// PRD §11.2: the single definition of a subscription that grants access; derived from status and dates, never stored.` `public static Expression<Func<Subscription, bool>> EntitledFor(Guid studentId, DateTimeOffset now, TimeSpan gracePeriod)`: `var lapsedBefore = now - gracePeriod; return x => x.StudentId == studentId && (((x.Status == SubscriptionStatus.Active \|\| x.Status == SubscriptionStatus.PastDue) && x.CurrentPeriodEnd > lapsedBefore) \|\| (x.Status == SubscriptionStatus.Cancelled && x.CurrentPeriodEnd > now));` |
| 10 | `Subscriptions/StudentEntitlement.cs` | value object | `public sealed record StudentEntitlement(Subscription? BaseSubscription, Subscription? AskTeacherSubscription)`. Members: `public static StudentEntitlement Free { get; } = new(null, null);`, `public PlanTier Tier => BaseSubscription is null ? PlanTier.Free : PlanTier.Base;`, `public bool HasAskTeacher => BaseSubscription is not null && AskTeacherSubscription is not null;`, `public static StudentEntitlement Resolve(IEnumerable<Subscription> subscriptions, DateTimeOffset now, TimeSpan gracePeriod)`, `private static Subscription? Latest(List<Subscription> entitled, SubscriptionPlan plan, TimeSpan gracePeriod)`. Bodies in Domain behaviour. |
| 11 | `Subscriptions/ISubscriptionRepository.cs` | repo | `public interface ISubscriptionRepository : IRepository<Subscription> { }` (the base `FindAsync` covers the spec). |
| 12 | `Subscriptions/Payment.cs` | entity | `public class Payment : AuditEntity, IAuditedEntity`. Properties (`{ get; private set; }`): `Guid StudentId`, `Guid? SubscriptionId`, `SubscriptionPlan Plan`, `BillingPeriod Period`, `long AmountMinor`, `string Currency = string.Empty`, `PaymentStatus Status`, `string? PaymobTransactionId`, `string? RawWebhook`, `DateTimeOffset? CompletedAt`. `public Money Amount => new(AmountMinor, Currency);`. `private Payment(Guid id, Guid? createdBy) : base(id, createdBy) { }`. `public static Payment Create(Guid studentId, SubscriptionPlan plan, BillingPeriod period, Money amount)`, `public void MarkSucceeded(Guid subscriptionId, string paymobTransactionId, string rawWebhook, DateTimeOffset completedAt)`, `public void MarkFailed(string paymobTransactionId, string rawWebhook, DateTimeOffset completedAt)`, `private void EnsurePending()`. Bodies in Domain behaviour. |
| 13 | `Subscriptions/IPaymentRepository.cs` | repo | `public interface IPaymentRepository : IRepository<Payment> { }` |

### Application — `api/Elmanhg.Application`
| # | Path | Type | Contract |
|---|------|------|----------|
| 14 | `Shared/Options/SubscriptionsOptions.cs` | options | `public sealed class SubscriptionsOptions`. `public const string SectionName = "Subscriptions";`. `// PRD §11.1: the Ask a Teacher add-on is sold monthly only.` `public const int AskTeacherPeriodMonths = 1;`. Properties: `[Required, RegularExpression("^[A-Z]{3}$")] public string Currency { get; set; } = "EGP";` · `[Range(0, 30)] public int GracePeriodDays { get; set; } = 3;` · `[Range(0, 1000)] public int FreeDailyQuizQuestions { get; set; } = 10;` · `[Range(0, 1000)] public int FreeDailyAvatarMessages { get; set; } = 5;` · `[Range(0, 100)] public int FreeOpenLessonsPerUnit { get; set; } = 1;` · `[Range(1, 10000)] public int BaseDailyAvatarMessages { get; set; } = 50;` · `public Dictionary<BillingPeriod, PlanPriceOptions> BasePrices { get; set; } = [];` · `[Range(1, 1000)] public int AskTeacherMonthlyQuestions { get; set; } = 20;` · `[Range(1, 168)] public int AskTeacherReplySlaHours { get; set; } = 24;` · `[Range(typeof(long), "1", "100000000")] public long AskTeacherMonthlyPriceMinor { get; set; }` · `[Range(1, 100)] public int PaymentHistoryMaxPageSize { get; set; } = 50;` · `public TimeSpan GracePeriod => TimeSpan.FromDays(GracePeriodDays);` |
| 15 | `Shared/Options/PlanPriceOptions.cs` | options | `public sealed class PlanPriceOptions { public int Months { get; set; } public long AmountMinor { get; set; } }` (checked by the DI `Validate` lambda; DataAnnotations do not recurse). |
| 16 | `Subscriptions/GetPlanCatalogue/GetPlanCatalogueQuery.cs` | query | `public sealed record GetPlanCatalogueQuery : IRequest<PlanCatalogueResult>;` |
| 17 | `Subscriptions/GetPlanCatalogue/GetPlanCatalogueHandler.cs` | handler | `public sealed class GetPlanCatalogueHandler(IOptions<SubscriptionsOptions> subscriptionsOptions) : IRequestHandler<GetPlanCatalogueQuery, PlanCatalogueResult>`. `Handle`: 1. `return Task.FromResult(PlanCatalogueResultGenerator.Generate(subscriptionsOptions.Value));` (no user guard, anonymous). |
| 18 | `Subscriptions/GetMyEntitlement/GetMyEntitlementQuery.cs` | query | `public sealed record GetMyEntitlementQuery : IRequest<EntitlementResult>;` |
| 19 | `Subscriptions/GetMyEntitlement/GetMyEntitlementHandler.cs` | handler | `public sealed class GetMyEntitlementHandler(ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetMyEntitlementQuery, EntitlementResult>`. `Handle`: 1. if `currentUserService.UserId == null \|\| == default`, throw `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`; 2. `return await StudentEntitlementLoader.LoadAsync(subscriptionRepository, currentUserService.UserId.Value, subscriptionsOptions.Value, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);` |
| 20 | `Subscriptions/GetMyPayments/GetMyPaymentsQuery.cs` | query | `public sealed record GetMyPaymentsQuery(int PageNumber = 1, int PageSize = 20) : IRequest<PageData<PaymentResult>>;` |
| 21 | `Subscriptions/GetMyPayments/GetMyPaymentsValidator.cs` | validator | `public sealed class GetMyPaymentsValidator : AbstractValidator<GetMyPaymentsQuery>`, ctor `(IOptions<SubscriptionsOptions> subscriptionsOptions)`: `RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.PaymentHistoryPageNumberInvalid);` `RuleFor(x => x.PageSize).ValidateRange(1, options.PaymentHistoryMaxPageSize, ErrorCodes.PaymentHistoryPageSizeInvalid);` |
| 22 | `Subscriptions/GetMyPayments/GetMyPaymentsHandler.cs` | handler | `public sealed class GetMyPaymentsHandler(IPaymentRepository paymentRepository, ICurrentUserService currentUserService) : IRequestHandler<GetMyPaymentsQuery, PageData<PaymentResult>>`. `Handle`: 1. user guard as in #19; 2. `var userId = ...Value;` 3. `var page = await paymentRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: x => x.StudentId == userId && x.Status != PaymentStatus.Pending, orderBy: query => query.OrderByDescending(x => x.CreationDate).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);` 4. return a `new PageData<PaymentResult> { ... }` copying every paging field from `page` (mirror `GetSessionHistoryHandler`), with `Items = page.Items.Select(PaymentResultGenerator.Generate).ToList()`. |
| 23 | `Subscriptions/Shared/PlanCatalogueResult.cs` | result (client) | `public sealed record PlanCatalogueResult(FreePlanResult Free, BasePlanResult Base, AskTeacherPlanResult AskTeacher);` |
| 24 | `Subscriptions/Shared/FreePlanResult.cs` | result (client) | `public sealed record FreePlanResult(int DailyQuizQuestions, int DailyAvatarMessages, int OpenLessonsPerUnit);` |
| 25 | `Subscriptions/Shared/BasePlanResult.cs` | result (client) | `public sealed record BasePlanResult(int DailyAvatarMessages, List<PlanPriceResult> Prices);` |
| 26 | `Subscriptions/Shared/AskTeacherPlanResult.cs` | result (client) | `public sealed record AskTeacherPlanResult(int MonthlyQuestions, int ReplySlaHours, List<PlanPriceResult> Prices);` |
| 27 | `Subscriptions/Shared/PlanPriceResult.cs` | result (client) | `public sealed record PlanPriceResult(BillingPeriod Period, int Months, Money Price);` |
| 28 | `Subscriptions/Shared/PlanCatalogueResultGenerator.cs` | static | `public static PlanCatalogueResult Generate(SubscriptionsOptions options)`. Free is built from the three `Free*` options. Base is `new BasePlanResult(options.BaseDailyAvatarMessages, options.BasePrices.OrderBy(x => x.Value.Months).Select(x => new PlanPriceResult(x.Key, x.Value.Months, new Money(x.Value.AmountMinor, options.Currency))).ToList())`, one fluent operator per line. AskTeacher is `new AskTeacherPlanResult(options.AskTeacherMonthlyQuestions, options.AskTeacherReplySlaHours, [new PlanPriceResult(BillingPeriod.Monthly, SubscriptionsOptions.AskTeacherPeriodMonths, new Money(options.AskTeacherMonthlyPriceMinor, options.Currency))])`. |
| 29 | `Subscriptions/Shared/EntitlementResult.cs` | result (client) | `public sealed record EntitlementResult(PlanTier Tier, bool HasAskTeacher, bool CanTakeExams, int? DailyQuizQuestionLimit, int DailyAvatarMessageLimit, int? OpenLessonsPerUnit, int MonthlyAskTeacherQuestionLimit, List<SubscriptionResult> Subscriptions);` A null limit means unlimited. |
| 30 | `Subscriptions/Shared/SubscriptionResult.cs` | result (client) | `public sealed record SubscriptionResult(Guid Id, SubscriptionPlan Plan, BillingPeriod Period, SubscriptionStatus Status, DateTimeOffset CurrentPeriodStart, DateTimeOffset CurrentPeriodEnd, DateTimeOffset EntitledUntil, DateTimeOffset? CancelledAt);` |
| 31 | `Subscriptions/Shared/EntitlementResultGenerator.cs` | static | `public static EntitlementResult Generate(StudentEntitlement entitlement, SubscriptionsOptions options)`. Free tier gives `(PlanTier.Free, false, false, options.FreeDailyQuizQuestions, options.FreeDailyAvatarMessages, options.FreeOpenLessonsPerUnit, 0, subscriptions)`. Base tier gives `(PlanTier.Base, entitlement.HasAskTeacher, true, null, options.BaseDailyAvatarMessages, null, entitlement.HasAskTeacher ? options.AskTeacherMonthlyQuestions : 0, subscriptions)`. `subscriptions` = `[BaseSubscription, AskTeacherSubscription]` with nulls removed, mapped by `private static SubscriptionResult Map(Subscription subscription, TimeSpan gracePeriod)` with `EntitledUntil = subscription.EntitledUntil(gracePeriod) ?? subscription.CurrentPeriodEnd`. |
| 32 | `Subscriptions/Shared/StudentEntitlementLoader.cs` | static | `public static class StudentEntitlementLoader { public static async Task<EntitlementResult> LoadAsync(ISubscriptionRepository subscriptionRepository, Guid studentId, SubscriptionsOptions options, DateTimeOffset now, CancellationToken cancellationToken) }`. Steps: 1. `var subscriptions = await subscriptionRepository.FindAsync(SubscriptionEntitlementSpecification.EntitledFor(studentId, now, options.GracePeriod), cancellationToken, asNoTracking: true).ConfigureAwait(false);` 2. `var entitlement = StudentEntitlement.Resolve(subscriptions, now, options.GracePeriod);` 3. `return EntitlementResultGenerator.Generate(entitlement, options);` This is the entry point that #87 and #94 gates call. |
| 33 | `Subscriptions/Shared/PaymentResult.cs` | result (client) | `public sealed record PaymentResult(Guid Id, SubscriptionPlan Plan, BillingPeriod Period, Money Amount, PaymentStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);` |
| 34 | `Subscriptions/Shared/PaymentResultGenerator.cs` | static | `public static PaymentResult Generate(Payment payment) => new(payment.Id, payment.Plan, payment.Period, payment.Amount, payment.Status, payment.CreationDate, payment.CompletedAt);` |

`DependencyInjection.cs` (Application) adds, after the `ExamsOptions` line:
```csharp
services.AddOptions<SubscriptionsOptions>().BindConfiguration(SubscriptionsOptions.SectionName).ValidateDataAnnotations()
    .Validate(x => x.BasePrices.Count > 0 && x.BasePrices.Values.All(price => price.Months is >= 1 and <= 36 && price.AmountMinor > 0), "Subscriptions:BasePrices needs at least one period, each with Months 1-36 and AmountMinor > 0.")
    .ValidateOnStart();
```

### Infrastructure — `api/Elmanhg.Infrastructure`
| # | Path | Type | Contract |
|---|------|------|----------|
| 35 | `Subscriptions/SubscriptionRepository.cs`, `Subscriptions/PaymentRepository.cs` | repos | `public class SubscriptionRepository(AppDbContext context) : Repository<Subscription>(context), ISubscriptionRepository { }` and the same shape for `PaymentRepository`. |
| 36 | `Data/Context/AppDbContext.cs` (edit) | mapping | Constants: `// ISO 4217 alphabetic codes are exactly three letters; a schema invariant.` `private const int CurrencyCodeLength = 3;` · `// Paymob ids and references are short numeric strings; a schema invariant.` `private const int PaymobReferenceMaxLength = 100;` · `public const string PaymobTransactionIndex = "IX_Payments_PaymobTransactionId";`. DbSets: `public DbSet<Subscription> Subscriptions { get; set; }`, `public DbSet<Payment> Payments { get; set; }`. `ConfigureSubscriptions`: **Subscription**: `Plan`, `Period` and `Status` get `HasConversion<string>().HasMaxLength(EnumColumnMaxLength)`; `PaymobReference` gets `.HasMaxLength(PaymobReferenceMaxLength)`; `HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict)`; `HasIndex(x => new { x.StudentId, x.Plan, x.CurrentPeriodEnd })`. **Payment**: `Plan`, `Period` and `Status` are string enums as above; `Currency` gets `.IsRequired().HasMaxLength(CurrencyCodeLength)`; `PaymobTransactionId` gets `.HasMaxLength(PaymobReferenceMaxLength)`; `RawWebhook` gets `.HasColumnType("jsonb")`; `builder.Ignore(x => x.Amount)`; FK `StudentId` → `User` Restrict; FK `SubscriptionId` → `Subscription` Restrict; `HasIndex(x => new { x.StudentId, x.CreationDate })`; `HasIndex(x => x.PaymobTransactionId, PaymobTransactionIndex).IsUnique().HasFilter("\"PaymobTransactionId\" IS NOT NULL")`. Global filter: add `modelBuilder.Entity<Subscription>().HasQueryFilter(x => !x.IsDeleted);` and the same for `Payment`. There is **no** new `SaveChangesAsync` catch clause. |
| 37 | `Migrations/<timestamp>_AddSubscriptionsAndPayments.cs` (+ `.Designer.cs`) | migration | `dotnet ef migrations add AddSubscriptionsAndPayments -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Review it: only `CreateTable` × 2 plus indexes/FKs. No Drop or Rename. |

### API — `api/Elmanhg.Api`
| # | Path | Type | Contract |
|---|------|------|----------|
| 38 | `Controllers/Subscriptions/PlansController.cs` | controller | `[ApiController] [Route("api/plans")] [Authorize] public class PlansController(IMediator mediator) : ControllerBase`. One action: `[HttpGet(Name = "GetPlanCatalogue")] [AllowAnonymous] [ProducesResponseType<PlanCatalogueResult>(StatusCodes.Status200OK)] public async Task<ActionResult> GetPlanCatalogue(CancellationToken cancellationToken)` → `Ok(await mediator.Send(new GetPlanCatalogueQuery(), cancellationToken))`. |
| 39 | `Controllers/Subscriptions/SubscriptionsController.cs` | controller | `[ApiController] [Route("api/subscriptions")] [Authorize] public class SubscriptionsController(IMediator mediator) : ControllerBase`. Actions: `[HttpGet("entitlement", Name = "GetMyEntitlement")] [Authorize(Policy = DefaultCodes.SubscriptionManage)] [ProducesResponseType<EntitlementResult>(200)] GetMyEntitlement(CancellationToken)` and `[HttpGet("payments", Name = "GetMyPayments")] [Authorize(Policy = DefaultCodes.SubscriptionManage)] [ProducesResponseType<PageData<PaymentResult>>(200)] GetMyPayments([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)`. |

### Tests — `api/Elmanhg.Tests`
| # | Path | Contract |
|---|------|----------|
| 40 | `Builders/SubscriptionBuilder.cs` | `public sealed class SubscriptionBuilder`. Defaults: student `Guid.NewGuid()`, `Base`, `Monthly`, 1 month, start `new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)`, `Active`, reference `"paymob-ref-1"`. Fluent methods: `ForStudent(Guid)`, `WithPlan(SubscriptionPlan)`, `WithPeriod(BillingPeriod, int months)`, `StartingAt(DateTimeOffset)`, `InStatus(SubscriptionStatus)`. `Build()` calls `Subscription.Start(...)`, then reaches the status through domain methods only: PastDue → `MarkPastDue()`; Cancelled → `Cancel(start.AddDays(1))`; Expired → `Expire(subscription.CurrentPeriodEnd)`. |
| 41–54 | test classes | see Test plan |
| 55 | `Integration/Subscriptions/SubscriptionTestData.cs` | `public static class SubscriptionTestData`: `public const string PlansRoute = "/api/plans"; public const string SubscriptionsRoute = "/api/subscriptions";` `Task SeedSubscriptionAsync(ApiFactory factory, Subscription subscription, CancellationToken)` (adds through `AppDbContext` in a new scope, then saves). `Task<Payment> SeedCompletedPaymentAsync(ApiFactory factory, Guid studentId, bool succeeded, long amountMinor, CancellationToken)`: for success it first seeds a Base subscription for the student and calls `MarkSucceeded(subscription.Id, $"txn-{Guid.NewGuid():N}", "{}", DateTimeOffset.UtcNow)`, otherwise it calls `MarkFailed(...)`. `Task SeedPendingPaymentAsync(ApiFactory, Guid studentId, CancellationToken)`. Currency `"EGP"`, plan `Base`, period `Monthly`. Students come from `ScopeTestData.SeedStudentAsync`. |

### Web — `web/src`
| # | Path | Type | Contract |
|---|------|------|----------|
| 56 | `features/subscription/index.ts` | barrel | `export { SubscriptionPage } from './pages/SubscriptionPage'; export { subscriptionSearchSchema } from './schemas/subscriptionSearchSchema';` |
| 57 | `features/subscription/locales.ts` | locales | `export const subscriptionLocales = { ar, en };` (mirror `features/progress/locales.ts`). |
| 58 | `features/subscription/i18n/en.json` | strings | Exactly: `{"header":{"title":"Subscribe to Elmanhg","taglineWithCount":"{count, number} questions approved by real teachers · Unlimited practice · Unit exams","tagline":"Questions approved by real teachers · Unlimited practice · Unit exams"},"page":{"title":"Subscription","loading":"Loading plans…","errorTitle":"Could not load plans."},"current":{"label":"Your current plan","free":"Free","base":"Base","baseWithAskTeacher":"Base + Ask a Teacher"},"plan":{"Free":"Free","Base":"Base","AskTeacher":"Ask a Teacher"},"status":{"Active":"{plan} — active until {date}","PastDue":"{plan} — payment overdue, available until {date}","Cancelled":"{plan} — cancelled, available until {date}"},"price":{"Monthly":"{price}/month","Termly":"{price}/term","Yearly":"{price}/year"},"features":{"free":{"browse":"Browse all content","lessons":"{count, plural, one {The first lesson of each unit} other {The first # lessons of each unit}}","quiz":"{count, number} practice questions a day","avatar":"Assistant: {count, number} messages a day"},"base":{"unlimited":"Unlimited practice and exams","allLessons":"Every lesson","progress":"Full progress","avatar":"Assistant: {count, number} messages a day"},"askTeacher":{"requiresBase":"Requires Base","quota":"{count, number} questions a month","sla":"Reply within {hours, number} hours"}},"badge":{"active":"Active"},"payments":{"title":"Payments","caption":"Your payments","loading":"Loading payments…","errorTitle":"Could not load payments.","date":"Date","plan":"Plan","amount":"Amount","status":"Status","Succeeded":"Successful","Failed":"Failed"}}` |
| 59 | `features/subscription/i18n/ar.json` | strings | Same keys. header: `"اشترك في المنهج"`, `"{count, number} سؤال معتمد من معلّمين حقيقيين · تدريب لا نهائي · امتحانات وحدات"`, `"أسئلة معتمدة من معلّمين حقيقيين · تدريب لا نهائي · امتحانات وحدات"`. page: `"الاشتراك"`, `"جارٍ تحميل الباقات…"`, `"تعذّر تحميل الباقات."`. current: `"باقتك الحالية"`, `"مجاني"`, `"الأساسية"`, `"الأساسية + اسأل معلّم"`. plan: `"مجاني"`, `"الأساسية"`, `"اسأل معلّم"`. status: `"{plan} — نشطة حتى {date}"`, `"{plan} — الدفع متأخر، متاحة حتى {date}"`, `"{plan} — ملغاة، متاحة حتى {date}"`. price: `"{price}/شهر"`, `"{price}/فصل دراسي"`, `"{price}/سنة"`. features.free: `"تصفح المحتوى"`, `"{count, plural, one {الدرس الأول من كل وحدة} other {أول # دروس من كل وحدة}}"`, `"{count, number} أسئلة تدريب يوميًا"`, `"المساعد: {count, number} رسائل يوميًا"`. features.base: `"تدريب وامتحانات بلا حدود"`, `"كل الدروس"`, `"تقدم كامل"`, `"المساعد: {count, number} رسالة يوميًا"`. features.askTeacher: `"تتطلب الأساسية"`, `"{count, number} سؤالًا شهريًا"`, `"رد خلال {hours, number} ساعة"`. badge.active: `"مفعّلة"`. payments: `"المدفوعات"`, `"مدفوعاتك"`, `"جارٍ تحميل المدفوعات…"`, `"تعذّر تحميل المدفوعات."`, `"التاريخ"`, `"الباقة"`, `"المبلغ"`, `"الحالة"`, `"ناجحة"`, `"فاشلة"`. |
| 60 | `features/subscription/pages/SubscriptionPage.tsx` | page | `export function SubscriptionPage()`. It renders `<section className="flex flex-col gap-4">`: `<SubscribeHeader />`, then `<h2>` `t('page.title')`, then the plans region, then `<PaymentHistorySection />`. Plans region: `useGetPlanCatalogue()` + `useGetMyEntitlement()`. If either `isError`, show `ContentErrorState` (title `page.errorTitle`, `onRetry` refetches both). Else if either `isPending`, show `ContentListSkeleton` (label `page.loading`). Else show `<CurrentPlanCard entitlement={…} />` and `<PlanCardGrid catalogue={…} entitlement={…} />`. |
| 61 | `features/subscription/components/SubscribeHeader.tsx` | component | `export function SubscribeHeader()`. `useGetServableQuestionCount()`. `<header className="flex flex-col gap-2 rounded-lg bg-aurora p-5 text-surface shadow-1">` with `<h1 className="font-display text-display font-bold lg:text-display-desktop">` `t('header.title')` and `<p className="text-ui text-surface/80">`. The paragraph shows `t('header.taglineWithCount', { count: Number(data.count) })` when data is present, else `t('header.tagline')`. This is the only use of `bg-aurora` besides the future LandingHero. |
| 62 | `features/subscription/components/CurrentPlanCard.tsx` | component | Props `{ entitlement: EntitlementResult }`. `<section aria-labelledby>` card (`rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5`). It holds `<h3>` `t('current.label')`, a `<p className="font-semibold">` with `t(\`current.${planLabelKey(entitlement)}\`)`, then a `<ul>` of `<SubscriptionStatusLine>` for each `entitlement.subscriptions` item (key `id`). |
| 63 | `features/subscription/components/SubscriptionStatusLine.tsx` | component | Props `{ subscription: SubscriptionResult }`. Returns `null` for `Expired`. Otherwise `<li className="text-caption text-text-muted">` `t(\`status.${status}\`, { plan: t(\`plan.${plan}\`), date })`. The date is `formatDate(new Date(status === 'Active' ? currentPeriodEnd : entitledUntil), i18n.language, 'arabic-indic', { dateStyle: 'medium' })`. |
| 64 | `features/subscription/components/PlanCard.tsx` | component | `export interface PlanCardProps { title: string; priceLines: string[]; features: string[]; isActive: boolean }`. `<article aria-labelledby={headingId}>` card with `<h3 id>` title. `isActive` shows the badge `<span className="rounded-pill bg-success px-2.5 py-0.5 text-micro font-semibold text-surface">` `t('badge.active')`. Price lines are `<p className="font-display text-h3 font-semibold">` (key = line). Features are a `<ul className="list-disc ps-5 text-ui">` (key = feature). |
| 65 | `features/subscription/components/PlanCardGrid.tsx` | component | Props `{ catalogue: PlanCatalogueResult; entitlement: EntitlementResult }`. `<div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">` with three `PlanCard`s. **Free**: title `plan.Free`; no price lines; features `features.free.browse`, `features.free.lessons {count: openLessonsPerUnit}`, `features.free.quiz {count: dailyQuizQuestions}`, `features.free.avatar {count: dailyAvatarMessages}`; `isActive=false`. **Base**: title `plan.Base`; price lines `t(\`price.${p.period}\`, { price: formatMoney(Number(p.price.amountMinor), p.price.currency, i18n.language) })` per price in API order; features `unlimited`, `allLessons`, `progress`, `avatar {count: base.dailyAvatarMessages}`; `isActive = entitlement.tier === 'Base'`. **AskTeacher**: title `plan.AskTeacher`; price lines as for Base; features `requiresBase`, `quota {count: monthlyQuestions}`, `sla {hours: replySlaHours}`; `isActive = entitlement.hasAskTeacher`. Every count is passed through `Number(...)`. |
| 66 | `features/subscription/components/PaymentHistorySection.tsx` | component | Mirrors `progress/components/SessionHistorySection.tsx`. It uses `useSubscriptionSearch()` + `usePaymentHistory(search)` and applies the same `pageOutOfRange` reset via `useEffectEvent` and `setPage(1)`. Pending or out-of-range shows `ContentListSkeleton` (`payments.loading`). Error shows `ContentErrorState` (`payments.errorTitle`, retry = `refetch`). If `items.length === 0`, return `null`: the whole section, heading included, is hidden. Otherwise `<section aria-labelledby>` holds `<h2>` `payments.title`, `<PaymentHistoryTable items>` and, when `totalPages > 1`, `<Pagination page totalPages onPageChange={setPage} />`. |
| 67 | `features/subscription/components/PaymentHistoryTable.tsx` | component | Mirrors the `SessionHistoryTable` markup: a table inside a card with `overflow-x-auto`, `<caption className="sr-only">` `payments.caption`, and headers `date`/`plan`/`amount`/`status`. Each row: date `formatDate(new Date(item.completedAt ?? item.createdAt), lng, 'arabic-indic', { dateStyle: 'medium' })`; plan `t(\`plan.${item.plan}\`)`; amount `formatMoney(Number(item.amount.amountMinor), item.amount.currency, lng)`; status badge (`Succeeded` uses `bg-success text-surface`, `Failed` uses `bg-danger text-surface`, pill `text-micro font-semibold`) with text `t(\`payments.${item.status}\`)`. Row key `item.id`. |
| 68 | `features/subscription/hooks/useSubscriptionSearch.ts` | hook | `getRouteApi('/student/subscription')`. Returns `{ search, setPage: (page: number) => void navigate({ search: (previous) => ({ ...previous, paymentsPage: page }) }) }`. |
| 69 | `features/subscription/hooks/usePaymentHistory.ts` | hook | `export const paymentHistoryPageSize = 10;` `export function usePaymentHistory(search: SubscriptionSearch)` returns `useGetMyPayments({ pageNumber: search.paymentsPage ?? 1, pageSize: paymentHistoryPageSize }, { query: { placeholderData: keepPreviousData } })`. |
| 70 | `features/subscription/schemas/subscriptionSearchSchema.ts` | schema | `export const subscriptionSearchSchema = z.object({ paymentsPage: z.coerce.number().int().min(1).optional().catch(undefined) }); export type SubscriptionSearch = z.infer<typeof subscriptionSearchSchema>;` |
| 71 | `shared/lib/format.ts` (addition) | util | `// EGP and every currency sold here have two minor-unit digits.` `const minorUnitsPerMajor = 100;` `export function formatMoney(amountMinor: number, currency: string, lng: string, digits: DigitStyle = 'arabic-indic'): string` → `new Intl.NumberFormat(numberLocale(lng, digits), { style: 'currency', currency, minimumFractionDigits: amountMinor % minorUnitsPerMajor === 0 ? 0 : 2, maximumFractionDigits: 2 }).format(amountMinor / minorUnitsPerMajor)`. |
| 72 | `features/subscription/api/entitlement.ts` | util | `export type PlanLabelKey = 'free' \| 'base' \| 'baseWithAskTeacher'; export function planLabelKey(entitlement: EntitlementResult): PlanLabelKey` returns `'free'` if `tier === 'Free'`, else `hasAskTeacher ? 'baseWithAskTeacher' : 'base'`. |
| 73–76 | tests | see Test plan (`schemas/subscriptionSearchSchema.test.ts`, `api/entitlement.test.ts`, `pages/SubscriptionPage.test.tsx`, `pages/SubscriptionPage.payments.test.tsx`) |
| 77 | `test/subscriptionFixtures.ts` | fixtures | Typed from `@/shared/api/generated/model`. `planCatalogue(): PlanCatalogueResult` gives free `{10,5,1}`, base `{ dailyAvatarMessages: 50, prices: Monthly 1/19900, Termly 4/69900, Yearly 12/179900 (EGP) }` and askTeacher `{20, 24, [Monthly 1/9900 EGP]}`. `baseSubscriptionId`/`askTeacherSubscriptionId` constant UUIDs. `freeEntitlement()` gives tier Free, the limits 10/5/1/0, `canTakeExams:false` and `subscriptions: []`. `baseEntitlement({ withAskTeacher = false, status = 'Active' } = {})` gives tier Base, limits null/50/null, and subscriptions with `currentPeriodEnd: '2026-10-29T12:00:00Z'`, `entitledUntil: status === 'Active' \|\| status === 'PastDue' ? '2026-11-01T12:00:00Z' : '2026-10-29T12:00:00Z'` (Ask a Teacher is the same, plan `AskTeacher`). `payment(overrides)` defaults to Succeeded, Base, Monthly, 19900 EGP, `completedAt: '2026-09-29T12:00:00Z'`. `paymentsPage(items, { pageNumber = 1, totalPages = 1 })` returns the `PageData` shape (mirror `progressFixtures.sessionHistoryPage`). |

### Docs
| # | Path | Contract |
|---|------|----------|
| 78 | `docs/PRD.md` (edit) | **§11.1**, after the table, add the paragraph: "Prices, billing periods and quotas are configuration (`Subscriptions` section, see `docs/subscriptions.md`), not code. Base is sold monthly (1 month), termly (4 months) and yearly (12 months); Ask a Teacher monthly only. Shipped defaults: Free 10 quiz questions/day, 5 Avatar messages/day, first lesson of each unit; Base 50 Avatar messages/day; Ask a Teacher 20 questions/month with a 24-hour SLA. Prices have no default and must be configured; money is stored in minor units (piastres) with an ISO 4217 currency." **§11.2**, replace the States line with: "States: Active · PastDue · Cancelled · Expired (Trialing is not used in v1). Access is derived from state and dates, never stored: Active and PastDue grant access until the end of the paid period plus the grace period; Cancelled grants access until the end of the paid period; Expired grants none. Ask a Teacher grants access only while Base does." **§15**, replace the two lines with `Subscription(id, student_id, plan[Base\|AskTeacher], period[Monthly\|Termly\|Yearly], status[Active\|PastDue\|Cancelled\|Expired], current_period_start, current_period_end, cancelled_at?, expired_at?, paymob_ref?)` and `Payment(id, student_id, subscription_id?, plan, period, amount_minor, currency, status[Pending\|Succeeded\|Failed], paymob_txn_id?, raw_webhook_json?, completed_at?, created_at)  -- docs/subscriptions.md`. **§19**, append to item 1: " Defaults are configuration (docs/subscriptions.md); final prices are still open." Append to item 5: " Configured defaults: Free 5/day, Base 50/day." |
| 79 | `docs/subscriptions.md` (new) | Sections: **Plans and configuration** (table of every `Subscriptions:*` key, its default, and "no default: must be set" for `BasePrices` and `AskTeacherMonthlyPriceMinor`; the placeholder values in `appsettings.example.json`); **Money** (minor units, `Money` schema, price snapshot on `Payment`); **Subscription lifecycle** (a transition table: Start → Active; Renew from Active/PastDue → Active, extending from the previous period end; MarkPastDue Active → PastDue; Cancel from Active/PastDue → Cancelled; Expire from any non-Expired state → Expired; the error code of each rejected transition); **Entitlement** (the `EntitledUntil` rules, the Ask-requires-Base rule, the "latest entitled row per plan" rule, the Free representation; a note that a lapse takes effect without waiting for a status sweep; the cancel semantics and why they differ from the prototype); **Payments** (states, completed-only student log, the unique Paymob transaction id, `Payment.Id` as the merchant order id); **API** (the three endpoints, their policies and response shapes); **For later stories** (gates call `StudentEntitlementLoader.LoadAsync`; #100 creates Pending payments; #101 adds webhook transitions, the downgrade sweep and xmin; #102 adds `Refunded`). |

## Error codes
| Constant (class) | Value | Thrown by | Exception type | HTTP |
|---|---|---|---|---|
| `SubscriptionPeriodInvalid` (Domain) | `SUBSCRIPTION_PERIOD_INVALID` | `Subscription.Start`, `Renew` when `periodMonths < 1` | `BusinessRuleViolationCoreException` | 400 |
| `SubscriptionNotActive` (Domain) | `SUBSCRIPTION_NOT_ACTIVE` | `MarkPastDue` when `Status != Active` | `BusinessRuleViolationCoreException` | 400 |
| `SubscriptionEnded` (Domain) | `SUBSCRIPTION_ENDED` | `Renew`, `Cancel` when `Status.HasEnded()` | `BusinessRuleViolationCoreException` | 400 |
| `SubscriptionAlreadyExpired` (Domain) | `SUBSCRIPTION_ALREADY_EXPIRED` | `Expire` when `Status == Expired` | `BusinessRuleViolationCoreException` | 400 |
| `PaymentAmountInvalid` (Domain) | `PAYMENT_AMOUNT_INVALID` | `Payment.Create` when `AmountMinor <= 0`, or the currency is not 3 ASCII upper-case letters | `BusinessRuleViolationCoreException` | 400 |
| `PaymentNotPending` (Domain) | `PAYMENT_NOT_PENDING` | `MarkSucceeded`, `MarkFailed` when `Status != Pending` | `BusinessRuleViolationCoreException` | 400 |
| `PaymentHistoryPageNumberInvalid` (Application) | `PAYMENT_HISTORY_PAGE_NUMBER_INVALID` | `GetMyPaymentsValidator` | validation pipeline | 422 |
| `PaymentHistoryPageSizeInvalid` (Application) | `PAYMENT_HISTORY_PAGE_SIZE_INVALID` | `GetMyPaymentsValidator` | validation pipeline | 422 |

Resource strings (Arabic without tashkeel; alef without hamza, as the existing entries do):

| Key | ar | en |
|---|---|---|
| SUBSCRIPTION_PERIOD_INVALID | مدة الاشتراك غير صالحة. | The subscription period is not valid. |
| SUBSCRIPTION_NOT_ACTIVE | الاشتراك غير نشط. | The subscription is not active. |
| SUBSCRIPTION_ENDED | انتهى هذا الاشتراك او تم الغاؤه. | This subscription has ended or was cancelled. |
| SUBSCRIPTION_ALREADY_EXPIRED | هذا الاشتراك منته بالفعل. | This subscription has already expired. |
| PAYMENT_AMOUNT_INVALID | مبلغ الدفع غير صالح. | The payment amount is not valid. |
| PAYMENT_NOT_PENDING | تمت معالجة هذا الدفع بالفعل. | This payment has already been processed. |
| PAYMENT_HISTORY_PAGE_NUMBER_INVALID | رقم الصفحة يجب ان يكون 1 او اكثر. | The page number must be 1 or more. |
| PAYMENT_HISTORY_PAGE_SIZE_INVALID | حجم الصفحة غير صالح. | The page size is not valid. |

## Domain behaviour
Order in every method: guard, then mutate, then stamp. Braces on every `if`. All codes come from `Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`.

```csharp
// Subscription.cs
public static Subscription Start(Guid studentId, SubscriptionPlan plan, BillingPeriod period, int periodMonths, DateTimeOffset startsAt, string? paymobReference, Guid? createdBy)
{
    EnsurePeriod(periodMonths);
    return new Subscription(Guid.NewGuid(), createdBy)
    {
        StudentId = studentId, Plan = plan, Period = period, Status = SubscriptionStatus.Active,
        CurrentPeriodStart = startsAt, CurrentPeriodEnd = startsAt.AddMonths(periodMonths), PaymobReference = paymobReference,
    };
}

public DateTimeOffset? EntitledUntil(TimeSpan gracePeriod)
{
    return Status switch
    {
        SubscriptionStatus.Active or SubscriptionStatus.PastDue => CurrentPeriodEnd + gracePeriod,
        SubscriptionStatus.Cancelled => CurrentPeriodEnd,
        _ => null,
    };
}

private static void EnsurePeriod(int periodMonths)
{
    if (periodMonths < 1) { throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionPeriodInvalid); }
}

// Subscription.Lifecycle.cs
public void Renew(int periodMonths, string? paymobReference)
{
    if (Status.HasEnded()) { throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionEnded); }
    EnsurePeriod(periodMonths);
    CurrentPeriodStart = CurrentPeriodEnd;
    CurrentPeriodEnd = CurrentPeriodEnd.AddMonths(periodMonths);
    Status = SubscriptionStatus.Active;
    PaymobReference = paymobReference ?? PaymobReference;
    UpdationDate = DateTimeOffset.UtcNow;
}

public void MarkPastDue()
{
    if (Status != SubscriptionStatus.Active) { throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionNotActive); }
    Status = SubscriptionStatus.PastDue;
    UpdationDate = DateTimeOffset.UtcNow;
}

public void Cancel(DateTimeOffset cancelledAt)
{
    if (Status.HasEnded()) { throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionEnded); }
    Status = SubscriptionStatus.Cancelled;
    CancelledAt = cancelledAt;
    UpdationDate = DateTimeOffset.UtcNow;
}

public void Expire(DateTimeOffset expiredAt)
{
    if (Status == SubscriptionStatus.Expired) { throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionAlreadyExpired); }
    Status = SubscriptionStatus.Expired;
    ExpiredAt = expiredAt;
    UpdationDate = DateTimeOffset.UtcNow;
}

// StudentEntitlement.cs
public static StudentEntitlement Resolve(IEnumerable<Subscription> subscriptions, DateTimeOffset now, TimeSpan gracePeriod)
{
    var entitled = subscriptions
        .Where(x => x.IsEntitledAt(now, gracePeriod))
        .ToList();
    return new StudentEntitlement(Latest(entitled, SubscriptionPlan.Base, gracePeriod), Latest(entitled, SubscriptionPlan.AskTeacher, gracePeriod));
}

private static Subscription? Latest(List<Subscription> entitled, SubscriptionPlan plan, TimeSpan gracePeriod)
{
    return entitled
        .Where(x => x.Plan == plan)
        .OrderByDescending(x => x.EntitledUntil(gracePeriod))
        .FirstOrDefault();
}

// Payment.cs
public static Payment Create(Guid studentId, SubscriptionPlan plan, BillingPeriod period, Money amount)
{
    if (amount.AmountMinor <= 0 || amount.Currency.Length != 3 || !amount.Currency.All(char.IsAsciiLetterUpper))
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentAmountInvalid);
    }
    return new Payment(Guid.NewGuid(), studentId)
    {
        StudentId = studentId, Plan = plan, Period = period, AmountMinor = amount.AmountMinor, Currency = amount.Currency, Status = PaymentStatus.Pending,
    };
}

public void MarkSucceeded(Guid subscriptionId, string paymobTransactionId, string rawWebhook, DateTimeOffset completedAt)
{
    EnsurePending();
    SubscriptionId = subscriptionId; PaymobTransactionId = paymobTransactionId; RawWebhook = rawWebhook; CompletedAt = completedAt;
    Status = PaymentStatus.Succeeded;
    UpdationDate = DateTimeOffset.UtcNow;
}

public void MarkFailed(string paymobTransactionId, string rawWebhook, DateTimeOffset completedAt)
{
    EnsurePending();
    PaymobTransactionId = paymobTransactionId; RawWebhook = rawWebhook; CompletedAt = completedAt;
    Status = PaymentStatus.Failed;
    UpdationDate = DateTimeOffset.UtcNow;
}

private void EnsurePending()
{
    if (Status != PaymentStatus.Pending) { throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentNotPending); }
}
```
In the real files, the one-line guard braces above are expanded to the repo's multi-line brace style, and the object initialisers use one property per line.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| GET | `/api/plans` | `[AllowAnonymous]` | none | `PlanCatalogueResult` (200) |
| GET | `/api/subscriptions/entitlement` | `DefaultCodes.SubscriptionManage` (Student) | none; the user comes from `ICurrentUserService` | `EntitlementResult` (200); 401 anonymous; 403 teacher/admin |
| GET | `/api/subscriptions/payments?pageNumber&pageSize` | `DefaultCodes.SubscriptionManage` | query ints (defaults 1 and 20) | `PageData<PaymentResult>` (200); 422 `PAYMENT_HISTORY_PAGE_*_INVALID`; 401; 403 |

## Test plan
Conventions: xUnit v3, FluentAssertions (the repo's pinned library), NSubstitute, `TestContext.Current.CancellationToken`, `Method_Scenario_Expected`. Handler tests stub `FindAsync`/`FindPaginatedAsync` by **compiling the passed predicate** over an in-memory list, as `GetWeakSpotsHandlerTests` does, so the specification actually runs. Time comes from `Substitute.For<TimeProvider>()` with `GetUtcNow()` returning a fixed `2026-10-01T12:00:00Z`. Integration tests may read `DateTimeOffset.UtcNow` only in Arrange.

### api — Domain
| # | Test class | Test method | Asserts |
|---|---|---|---|
| D1 | `Domain/Subscriptions/SubscriptionTests` | `Start_ValidInput_IsActiveForOnePeriod` | Status Active; start = startsAt; end = startsAt.AddMonths(4) for Termly/4; plan, period, student and reference set |
| D2 | 〃 | `Start_PeriodMonthsBelowOne_ThrowsPeriodInvalid` | `BusinessRuleViolationCoreException`, `SUBSCRIPTION_PERIOD_INVALID` |
| D3 | 〃 | `Renew_Active_ExtendsFromPreviousPeriodEnd` | new start = old end; new end = old end + months; Active |
| D4 | 〃 | `Renew_PastDue_ReactivatesAndExtends` | Status Active; end extended |
| D5 | 〃 | `Renew_CancelledOrExpired_ThrowsSubscriptionEnded` (Theory: Cancelled, Expired) | code `SUBSCRIPTION_ENDED`; period unchanged |
| D6 | 〃 | `Renew_PeriodMonthsBelowOne_ThrowsPeriodInvalid` | code; end unchanged |
| D7 | 〃 | `Renew_WithoutReference_KeepsPreviousReference` | `PaymobReference` is still `"paymob-ref-1"` |
| D8 | 〃 | `MarkPastDue_Active_BecomesPastDue` | Status PastDue |
| D9 | 〃 | `MarkPastDue_NotActive_ThrowsNotActive` (Theory: PastDue, Cancelled, Expired) | code `SUBSCRIPTION_NOT_ACTIVE` |
| D10 | 〃 | `Cancel_ActiveOrPastDue_BecomesCancelledWithTimestamp` (Theory: Active, PastDue) | Status Cancelled; `CancelledAt` = argument |
| D11 | 〃 | `Cancel_Ended_ThrowsSubscriptionEnded` (Theory: Cancelled, Expired) | code `SUBSCRIPTION_ENDED` |
| D12 | 〃 | `Expire_NotExpired_BecomesExpiredWithTimestamp` (Theory: Active, PastDue, Cancelled) | Status Expired; `ExpiredAt` = argument |
| D13 | 〃 | `Expire_AlreadyExpired_ThrowsAlreadyExpired` | code `SUBSCRIPTION_ALREADY_EXPIRED` |
| D14 | 〃 | `EntitledUntil_ByStatus_AddsGraceOnlyWhileBillable` (Theory: Active → end+3d, PastDue → end+3d, Cancelled → end, Expired → null) | exact value |
| D15 | `Domain/Subscriptions/SubscriptionEntitlementSpecificationTests` | `EntitledFor_StatusAndTime_AgreesWithIsEntitledAt` (Theory: 4 statuses × now at end−1d / end+1d / end+4d, with the expected bool per row: Active/PastDue T,T,F; Cancelled T,F,F; Expired F,F,F) | `EntitledFor(...).Compile()(subscription)` == expected == `subscription.IsEntitledAt(now, 3d)` |
| D16 | 〃 | `EntitledFor_OtherStudent_IsNotSatisfied` | false for an Active in-period subscription of another student |
| D17 | `Domain/Subscriptions/StudentEntitlementTests` | `Resolve_NoSubscriptions_IsFree` | Tier Free; `HasAskTeacher` false; both null |
| D18 | 〃 | `Resolve_EntitledBase_IsBaseTier` | Tier Base; `BaseSubscription` is the one given |
| D19 | 〃 | `Resolve_AskTeacherWithoutEntitledBase_HasNoAskTeacher` | Tier Free; `HasAskTeacher` false |
| D20 | 〃 | `Resolve_BaseAndAskTeacher_HasAskTeacher` | `HasAskTeacher` true |
| D21 | 〃 | `Resolve_LapsedBase_IsFree` | Base ended 4 days before now → Tier Free |
| D22 | 〃 | `Resolve_TwoEntitledBase_PicksLatestEntitledUntil` | the one with the later end is chosen |
| D23 | `Domain/Subscriptions/PaymentTests` | `Create_ValidAmount_IsPendingWithAmount` | Pending; `Amount == new Money(19900, "EGP")`; student, plan, period set; `SubscriptionId` null |
| D24 | 〃 | `Create_NonPositiveAmount_ThrowsAmountInvalid` (Theory: 0, -1) | code `PAYMENT_AMOUNT_INVALID` |
| D25 | 〃 | `Create_InvalidCurrency_ThrowsAmountInvalid` (Theory: "EG", "egp", "EGPX") | code `PAYMENT_AMOUNT_INVALID` |
| D26 | 〃 | `MarkSucceeded_Pending_RecordsTransactionAndSubscription` | Succeeded; `SubscriptionId`, `PaymobTransactionId`, `RawWebhook`, `CompletedAt` set |
| D27 | 〃 | `MarkFailed_Pending_RecordsTransaction` | Failed; txn, raw and completedAt set; `SubscriptionId` null |
| D28 | 〃 | `MarkSucceeded_NotPending_ThrowsNotPending` (Theory: after succeeded, after failed) | code `PAYMENT_NOT_PENDING` |
| D29 | 〃 | `MarkFailed_NotPending_ThrowsNotPending` (Theory: after succeeded, after failed) | code `PAYMENT_NOT_PENDING` |

### api — Application
| # | Test class | Test method | Asserts |
|---|---|---|---|
| A1 | `Application/Features/Subscriptions/SubscriptionsOptionsTests` | `AddApplication_BasePricesMissing_ThrowsOptionsValidationException` | Failures contain the exact BasePrices message (config has AskTeacher price only) |
| A2 | 〃 | `AddApplication_BasePriceNotPositive_ThrowsOptionsValidationException` | `BasePrices:Monthly:AmountMinor=0` → throws |
| A3 | 〃 | `AddApplication_AskTeacherPriceMissing_ThrowsOptionsValidationException` | valid BasePrices, no Ask price → throws |
| A4 | 〃 | `AddApplication_CurrencyNotIsoCode_ThrowsOptionsValidationException` | `Currency=egp` → throws |
| A5 | 〃 | `AddApplication_ConfiguredPrices_ResolvesWithPrdDefaults` | with only prices configured: `(Currency, GracePeriodDays, FreeDailyQuizQuestions, FreeDailyAvatarMessages, FreeOpenLessonsPerUnit, BaseDailyAvatarMessages, AskTeacherMonthlyQuestions, AskTeacherReplySlaHours)` == `("EGP",3,10,5,1,50,20,24)`; `BasePrices[Termly]` is (4, 69900) |
| A6 | `Application/Features/Subscriptions/GetPlanCatalogue/GetPlanCatalogueHandlerTests` | `Handle_ConfiguredOptions_ReturnsFreeLimitsAndBasePricesOrderedByMonths` | Free (10,5,1); Base avatar 50; prices Monthly/Termly/Yearly in that order even when the dictionary is filled Yearly-first; Money amounts and currency |
| A7 | 〃 | `Handle_AskTeacher_ReturnsSingleMonthlyPriceAndQuota` | exactly one price (Monthly, 1, 9900 EGP); quota 20; SLA 24 |
| A8 | `Application/Features/Subscriptions/GetMyEntitlement/GetMyEntitlementHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException`, `USER_NOT_AUTHENTICATED` |
| A9 | 〃 | `Handle_NoSubscriptions_ReturnsFreeLimits` | Tier Free, `CanTakeExams` false, quiz 10, avatar 5, open lessons 1, ask 0, empty list |
| A10 | 〃 | `Handle_EntitledBase_ReturnsUnlimitedQuizzesAndBaseAvatarLimit` | Tier Base, `CanTakeExams` true, quiz null, open lessons null, avatar 50, ask 0, one `SubscriptionResult` with `EntitledUntil` = end+3d |
| A11 | 〃 | `Handle_BaseAndAskTeacher_ReturnsAskTeacherQuota` | `HasAskTeacher` true; ask limit 20; two subscriptions |
| A12 | 〃 | `Handle_PastDueBaseWithinGrace_ReturnsBase` | Tier Base; status PastDue in the list |
| A13 | 〃 | `Handle_BaseLapsedBeyondGrace_ReturnsFree` | Tier Free |
| A14 | 〃 | `Handle_OtherStudentsSubscription_ReturnsFree` | the in-memory list holds only another student's Active Base → Free |
| A15 | `Application/Features/Subscriptions/GetMyPayments/GetMyPaymentsHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | exception and code |
| A16 | 〃 | `Handle_MixedPayments_ReturnsOwnCompletedPaymentsOnly` | own Succeeded and Failed returned; own Pending and another student's Succeeded excluded |
| A17 | 〃 | `Handle_SucceededPayment_MapsAmountPlanPeriodStatusAndDates` | every `PaymentResult` field |
| A18 | `Application/Features/Subscriptions/GetMyPayments/GetMyPaymentsValidatorTests` | `Validate_DefaultPaging_Passes` | valid |
| A19 | 〃 | `Validate_PageNumberBelowOne_FailsWithPageNumberInvalid` | error code |
| A20 | 〃 | `Validate_PageSizeBelowOne_FailsWithPageSizeInvalid` | error code |
| A21 | 〃 | `Validate_PageSizeAboveMax_FailsWithPageSizeInvalid` | pageSize 51 with max 50 → error code |

### api — Integration (Testcontainers, through HTTP)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| I1 | `Integration/Subscriptions/PlanCatalogueEndpointTests` | `Get_Anonymous_ReturnsConfiguredCatalogue` | 200; `free.dailyQuizQuestions` 10; `base.prices` = Monthly/19900, Termly/69900, Yearly/179900 with `currency` "EGP" and `months` 1/4/12; `askTeacher.monthlyQuestions` 20; `askTeacher.prices[0].price.amountMinor` 9900 |
| I2 | 〃 | `Get_SignedInStudent_Returns200` | 200 (an anonymous endpoint also serves authenticated users) |
| I3 | `Integration/Subscriptions/EntitlementEndpointTests` | `Get_Anonymous_Returns401` | 401 |
| I4 | 〃 | `Get_Teacher_Returns403` | 403 |
| I5 | 〃 | `Get_NewStudent_ReturnsFree` | 200; `tier` "Free"; `dailyQuizQuestionLimit` 10; `subscriptions` empty |
| I6 | 〃 | `Get_ActiveBaseAndAskTeacher_ReturnsBaseWithAskTeacher` | seeded Base and Ask (start = UtcNow−5d, 1 month) → `tier` "Base", `hasAskTeacher` true, `dailyQuizQuestionLimit` null, `monthlyAskTeacherQuestionLimit` 20, 2 subscriptions |
| I7 | 〃 | `Get_BaseLapsedBeyondGrace_ReturnsFree` | Active Base started UtcNow−2 months, 1 month → `tier` "Free" |
| I8 | 〃 | `Get_OtherStudentsSubscription_IsNotCounted` | another student's Active Base → caller is "Free" |
| I9 | `Integration/Subscriptions/PaymentHistoryEndpointTests` | `Get_Anonymous_Returns401` | 401 |
| I10 | 〃 | `Get_Teacher_Returns403` | 403 |
| I11 | 〃 | `Get_StudentWithPayments_ReturnsOwnCompletedPaymentsNewestFirst` | seeds own Failed, then own Succeeded, own Pending and another student's Succeeded → `items` = [Succeeded, Failed] in that order; `totalCount` 2; `amount` = `{amountMinor, currency}` |
| I12 | 〃 | `Get_PageSizeAboveMax_Returns422WithCode` | 422; problem `code` "PAYMENT_HISTORY_PAGE_SIZE_INVALID" |
| I13 | `Integration/Subscriptions/PaymentPersistenceTests` | `SaveChanges_DuplicatePaymobTransactionId_ThrowsUniqueViolation` | the second failed payment with the same txn id → `DbUpdateException` whose inner `PostgresException` has `SqlState` 23505 and `ConstraintName` `AppDbContext.PaymobTransactionIndex` |
| I14 | 〃 | `SaveChanges_PendingPaymentsWithoutTransactionId_AreAllowed` | two Pending payments for one student save; count 2 in a fresh scope |
| I15 | `Integration/Persistence/AppDbContextTests` (modify) | `Migrate_FreshDatabase_LeavesNoPendingMigrations` | the list gains `_AddSubscriptionsAndPayments` |

`EndpointAuthorizationTests` and `PermissionMatrixPolicyTests` already cover the new actions and policy. They are unchanged.

### web (Vitest + Testing Library + MSW; `renderApp('/student/subscription', { session: testSessions.student, lng })`)
Default handlers per test: `getGetPlanCatalogueMockHandler(planCatalogue())`, `getGetMyEntitlementMockHandler(...)`, `getGetMyPaymentsMockHandler(paymentsPage([]))`, `getGetServableQuestionCountMockHandler({ count: 1234 })`.

| # | Test file | `it(...)` | Asserts |
|---|---|---|---|
| W1 | `shared/lib/format.test.ts` (add) | `formats whole minor amounts without decimals in English` | `formatMoney(19900,'EGP','en').replace(/\s/gu,' ')` is `'EGP 199'`. Normalise whitespace with a regex; never write a `\u` escape. |
| W2 | 〃 | `keeps two decimals for fractional amounts` | 19950 → `'EGP 199.50'` (whitespace-normalised) |
| W3 | 〃 | `uses Arabic-Indic digits for Arabic` | `formatMoney(19900,'EGP','ar')` contains `'١٩٩'` |
| W4 | `features/subscription/schemas/subscriptionSearchSchema.test.ts` | `accepts a positive payments page` | `{paymentsPage:'2'}` → 2 |
| W5 | 〃 | `drops a non-positive or non-numeric payments page` | `0` and `'x'` → undefined |
| W6 | `features/subscription/api/entitlement.test.ts` | `returns free for the free tier` | `'free'` |
| W7 | 〃 | `returns base without the add-on` | `'base'` |
| W8 | 〃 | `returns baseWithAskTeacher when the add-on is active` | `'baseWithAskTeacher'` |
| W9 | `features/subscription/pages/SubscriptionPage.test.tsx` | `shows loading then the header, current plan and plan cards for a free student` | skeleton `status` "Loading plans…"; h1 "Subscribe to Elmanhg"; text "1,234 questions approved by real teachers · Unlimited practice · Unit exams"; current plan "Free"; articles "Free", "Base" and "Ask a Teacher"; "10 practice questions a day"; "The first lesson of each unit"; "EGP 199/month", "EGP 699/term", "EGP 1,799/year", "EGP 99/month"; "20 questions a month"; "Reply within 24 hours"; no "Active" badge |
| W10 | 〃 | `marks Base and Ask a Teacher active for a subscribed student` | current "Base + Ask a Teacher"; the "Base" and "Ask a Teacher" articles each contain "Active"; the line "Base — active until Oct 29, 2026" |
| W11 | 〃 | `shows the grace line for a past-due subscription` | "Base — payment overdue, available until Nov 1, 2026" |
| W12 | 〃 | `shows the cancelled line with the period end` | "Base — cancelled, available until Oct 29, 2026" |
| W13 | 〃 | `shows the tagline without a count when the question count fails` | servable-count handler returns 500 → "Questions approved by real teachers · Unlimited practice · Unit exams"; the plan cards still render |
| W14 | 〃 | `shows an error with retry when plans fail to load` | catalogue 500 `{ once: true }` → alert "Could not load plans."; click Retry → "Base" article appears |
| W15 | 〃 | `renders right-to-left in Arabic` | `lng:'ar'`: h1 "اشترك في المنهج"; `document.documentElement` has `dir="rtl"`; "مجاني" is shown |
| W16 | 〃 | `has no axe violations` | `axe(container)` after the cards load |
| W17 | `features/subscription/pages/SubscriptionPage.payments.test.tsx` | `hides the payments section when there are no payments` | after the cards load, no heading "Payments" |
| W18 | 〃 | `lists payments with date, plan, amount and status` | the table "Your payments" has a row with "Sep 29, 2026", "Base", "EGP 199" and "Successful"; a Failed row shows "Failed" |
| W19 | 〃 | `pages through payments` | page 1 (`totalPages` 2) → click "Next" → page-2 row visible (the handler branches on `pageNumber`) |
| W20 | 〃 | `shows an error with retry when payments fail` | payments 500 once → alert "Could not load payments."; Retry → row visible |

## Definition of done
- [ ] Every sub-task is delivered: the Subscription and Payment entities with the Active/PastDue/Cancelled/Expired states (and Pending/Succeeded/Failed payment states); plan configuration of prices, periods and quotas in `SubscriptionsOptions`; the entitlement query (`GetMyEntitlement` and `StudentEntitlementLoader`) ready for the #87 and #94 gates.
- [ ] Domain method bodies match "Domain behaviour": guard, then mutate, then stamp `UpdationDate`; every guard throws `BusinessRuleViolationCoreException` with the listed domain code.
- [ ] `SubscriptionEntitlementSpecification` is the only SQL definition of "entitled", and D15 proves it equals `IsEntitledAt`.
- [ ] Money is `long AmountMinor` plus `Currency` everywhere. No `decimal`, `double` or `float` for money. The wire shape is `Money { amountMinor, currency }`.
- [ ] `BasePrices` and `AskTeacherMonthlyPriceMinor` have no code default, and startup fails without them (A1–A3). The limits default to the PRD values (A5).
- [ ] `appsettings.example.json` and `ApiFactory` carry the `Subscriptions` section. The implementation report names the section for the dev to copy into the local `appsettings.json` (constitution §2).
- [ ] The migration `AddSubscriptionsAndPayments` has only creates. The snapshot is updated. The `AppDbContextTests` list is extended. Both entities are in the global soft-delete filter. The unique filtered index `IX_Payments_PaymobTransactionId` exists.
- [ ] The endpoints are exactly the three in API surface. `/api/plans` is `[AllowAnonymous]`; the other two use `DefaultCodes.SubscriptionManage`.
- [ ] All 8 error codes are in the right `ErrorCodes` class and in both resx files.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` are regenerated, with no drift. The Postman `Subscriptions` folder is added.
- [ ] The web `/student/subscription` screen has the aurora header (the only `bg-aurora` use), the current plan, the three plan cards and the paged payment log. It has loading, error-with-retry and hidden-when-empty states, and passes the RTL and axe checks. There are no subscribe or cancel buttons (they come in #100/#101).
- [ ] No literal colour, px or hex values and no physical-direction utilities in `src/features/subscription`. Every string is in both `ar.json` and `en.json`.
- [ ] Every test in the Test plan exists with that name and passes. No existing test is edited except `AppDbContextTests` (I15) and the additions to `format.test.ts` (W1–W3).
- [ ] `dotnet test api/ -c Release` is green with `appsettings.json` moved aside (CI parity). `npx tsc -b`, `eslint --max-warnings=0`, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` and `vitest run --coverage` are all green.
- [ ] Docs: `docs/subscriptions.md` exists with every listed section. `docs/PRD.md` §11.1, §11.2, §15 and §19 and `docs/backlog.json` are edited as specified. No other doc is created.
- [ ] Morabh: nothing is copied. The `PaymentAttempt` guard shape is cited in the report.
