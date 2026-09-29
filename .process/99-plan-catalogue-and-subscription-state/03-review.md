VERDICT: APPROVED

# Review: [E10.S1] Plan catalogue and subscription state (#99)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Application/Subscriptions/Shared/EntitlementResultGenerator.cs:10-17`: when Base has lapsed but Ask a Teacher is still entitled, the Free result still lists the AskTeacher subscription. The page then shows "Ask a Teacher — active until …" under "Your current plan: Free", and the Ask card has no badge. This matches plan #31, but #101 should decide whether to show it.
- `web/src/features/subscription/components/SubscriptionStatusLine.tsx:18`: an Active row that is past `currentPeriodEnd` but still inside the grace period (webhook lag) reads "active until <a past date>". Consider using `entitledUntil`, or the PastDue wording, once `now > currentPeriodEnd`.
- `api/Elmanhg.Application/DependencyInjection.cs:36`: no test fails if `.ValidateOnStart()` is removed. A1–A4 trigger validation through `IOptions.Value`. The same gap exists in every existing `*OptionsTests`, so this is not new debt.
- `api/Elmanhg.Tests/Domain/Subscriptions/SubscriptionEntitlementSpecificationTests.cs:25`: the grid has no row at exactly `end + grace`. The strict `>` boundary is only held by A13 (`GetMyEntitlementHandlerTests.cs:87`), whose data puts `end + 3d == Now` exactly. That makes its name "BeyondGrace" misleading. Add an explicit boundary row to D15.
- The spec's SQL translation is exercised against Postgres only for Active rows (I6/I7/I8). The PastDue and Cancelled branches are proven only in compiled form (D15). A seeded Cancelled or PastDue integration case would close this.
- `api/Elmanhg.Domain/Subscriptions/Payment.cs:8`: `Payment` is `IAuditedEntity`, so when #101 writes `RawWebhook` (a Paymob payload that carries billing name, email and phone), it will land in the audit diff. #101 should exclude or redact it.
- `api/Elmanhg.Domain/Subscriptions/Subscription.Lifecycle.cs:8-21`: `Renew` does not change `Period` (for example a Monthly→Yearly switch), and it extends from the old end even after a long lapse. Both are #101 design choices to settle, not defects here.
- `web/src/features/subscription/components/SubscribeHeader.tsx:11`: `text-surface/80` over the peach end of the aurora is low-contrast for body-size text. This is plan Decision #25 plus the prototype; axe colour-contrast is disabled repo-wide, so it would not be caught.
- `PROGRESS.md` is modified but not listed in 02-implementation.md. The diff is orchestrator bookkeeping from the #171 merge, not story work.

## Verified
- Checks I ran myself:
  - `dotnet test api/ -c Release` with `appsettings.json` moved aside: total 2080, failed 0. The file was restored afterwards.
  - Web `typecheck` and `lint` both clean.
  - `vitest --run`: 108 files and 668 tests passed.
  - `prettier --check`: clean.
- Money is minor units only: `Money(long AmountMinor, string Currency)`, `bigint AmountMinor` plus `varchar(3) Currency`, and wire schema `Money {amountMinor:int64, currency}`. There is no decimal or float anywhere. `formatMoney` divides by 100 for display only.
- Entitlement edges:
  - `EntitledUntil` is Active/PastDue → end + grace, Cancelled → end, Expired → null.
  - `IsEntitledAt` is strict `>`. The spec (`CurrentPeriodEnd > now - grace` / `> now`) is algebraically identical.
  - Ask a Teacher without Base gives `HasAskTeacher = false` (D19).
  - With several rows for the same plan, the latest `EntitledUntil` wins (D22).
- Access:
  - Both student handlers filter by `ICurrentUserService.UserId` (A14, A16, I8, I11). Both actions use `DefaultCodes.SubscriptionManage` (Student only; I4 and I10 return 403).
  - `/api/plans` is `[AllowAnonymous]` and returns configuration values only.
- Startup:
  - Empty `BasePrices` fails with an explicit message.
  - `AskTeacherMonthlyPriceMinor` has no default and is `[Range(1, …)]`.
  - `ValidateOnStart` is present. The example config and `ApiFactory` carry the section.
- Readiness for #100/#101:
  - `Payment` is created Pending, with nullable `SubscriptionId` and an amount snapshot.
  - The filtered unique `IX_Payments_PaymobTransactionId` index exists (I13 checks SqlState 23505 and the constraint name).
  - `Start` takes `paymobReference: null` for #106.
  - Gates get `StudentEntitlementLoader.LoadAsync`.
- The migration contains only CreateTable ×2 and 4 indexes and FKs; there are no drops or renames. Soft-delete filters are added for both entities, `AppDbContextTests` gains the twentieth entry, and the snapshot is updated.
- All 8 error codes are in the correct classes and in both resx files with the plan's strings.
- Every file in "Files to create" exists. Nothing extra was created beyond the disclosed `SubscriptionTestData.NewPayment` and `Currency`.
- All 5 deviations are disclosed and justified:
  - the orval `GetMyPayments` zod exclusion, as for its siblings;
  - `totalItems`;
  - "Next page";
  - the `NewPayment` helper;
  - the builder switch expression.
- Postman: a `Subscriptions` folder after `Progress` with 3 requests. The plans request is `noauth`; entitlement and payments inherit bearer auth. URLs, the query and the tests match the plan.
- Docs:
  - `docs/subscriptions.md` has every listed section and agrees with the code.
  - PRD §11.1, §11.2, §15 and §19 have been edited as specified and agree with the code.
  - `docs/backlog.json` moves the page task and renames the E10.S2 task.
  - The design prompt's Paymob modal and subscribe buttons are #100 work (incompleteness, not divergence).
- Web:
  - Every class is a token (`bg-aurora` appears only in `SubscribeHeader`).
  - Only logical utilities are used.
  - ar and en locale keys match, and the en strings match the plan verbatim.
  - The heading order h1 → h2 → h3 does not skip levels.
  - The payments section is hidden when empty; pagination is in the URL through Zod.

## Test quality
- SubscriptionTests (D1–D14): every guard and branch asserts the exact code and the state left behind. Constrains the implementation.
- SubscriptionEntitlementSpecificationTests: the 12-row grid asserts both the spec and `IsEntitledAt` against a fixed expectation, not just that they agree. Constrains the implementation (only the exact boundary row is missing; see above).
- StudentEntitlementTests: the Ask-without-Base and latest-wins cases would fail under the obvious mutations. Constrains the implementation.
- PaymentTests: covers amount, currency and not-pending guards and the recorded fields. Constrains the implementation.
- GetMyEntitlementHandlerTests and GetMyPaymentsHandlerTests: the stubs compile the real predicate over in-memory lists, so the specification and filter actually run; nothing is a tautology. Ordering is not covered at unit level but is covered by I11.
- GetMyPaymentsValidatorTests: one failing case per rule plus the max bound. Constrains the implementation.
- SubscriptionsOptionsTests: A1 and A2 assert the exact message. A3 and A4 assert only that validation throws, which is acceptable because every other value is valid. `ValidateOnStart` itself is not constrained (see above).
- Integration I1–I14: real HTTP and Postgres, with the emitted JSON inspected (enum strings, the `Money` shape, `totalItems`). These constrain the implementation.
- Web W1–W20: behaviour through roles, with MSW. The status-line and empty-section mutations are caught (the implementer reported this; the assertions I read are consistent with it). The axe test is present.
