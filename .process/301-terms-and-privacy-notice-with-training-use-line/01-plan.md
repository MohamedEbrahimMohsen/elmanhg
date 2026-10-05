# Plan — [E18.S9] Terms and privacy notice with training-use line at sign-up (#301)

## Goal
A visitor signing up (phone or email) now sees one plain caption line under the sign-up form. It says that signing up means agreeing to the terms and privacy policy, which include using chats, without personal data, to improve the AI assistant, and that anyone under 18 needs a parent's or guardian's agreement. The line links to a new public `/privacy` page (ar/en, RTL). That page explains what is collected, how it is used, training use, deleting chats, minors and contact, and it shows the terms version. The API stores the accepted `TermsVersion` and `TermsAcceptedAt` on the new student and rejects an unknown version with 422. The landing footer and the student More page link to the page. The training-data checklist item "Notice or consent" is closed.

## Scope
**In:**
- api: a `TermsVersions` domain constant set. `User.TermsVersion` / `User.TermsAcceptedAt` plus `User.AcceptTerms(...)`. Both self-sign-up commands (`RegisterWithEmailCommand`, `RegisterWithPhoneCommand`) gain `TermsVersion` and validate it. Both handlers record acceptance. One migration (nullable columns, no backfill). Error codes plus resx. OpenAPI regenerated. Postman bodies updated.
- web:
  - The `/privacy` route and a new `legal` feature, whose locales are registered lazily so the entry chunk does not grow with the legal text.
  - `TermsNotice` under the sign-up form.
  - `termsVersion` sent with both registrations.
  - Landing footer link and student More page link.
  - Common error strings.
  - Orval and `routeTree.gen.ts` regenerated.
- docs: PRD §13, §14 (Privacy row) and §15 (User), `docs/training-data.md` checklist, `docs/claude-design-prompt.md` §4–§6, `docs/prototype.md`, `docs/backlog.json` (E18).

**Out:** (per the story) re-prompting existing users when the version changes, parental verification, an opt-out switch. Also: invited teachers and admins (`AcceptInvitation`) do not record terms, because they are staff and do not self-sign-up. Users seeded by `LoadTestUsers` stay null. No terms field on `AuthResult`/`AuthUserResult`.

**Deferred:**
- D1. Legal review of the final ar/en text before go-live. The story says this is the dev's job. It cannot be done by the pipeline.
- D2. Creating the `privacy@elmanhg.com` mailbox shown on the page (see Decision 12). It is a hosting/mail account action that cannot be done from this repo.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Which "sign-up command"? | Both student self-sign-up commands: `RegisterWithEmail` and `RegisterWithPhone`. `AcceptInvitation` is untouched. | They are the only paths that create students (`User.CreateStudentWith*`). Invitees are staff. |
| 2 | Where do known versions live? | Domain constant class `Elmanhg.Domain.Identity.TermsVersions` with `Current = "2026-10-05"` and `IsKnown(string?)`. It is not an Options value. | The text ships in the web bundle under that version, so a new version always needs a code release. That makes it an invariant, not a tunable (skill §1 "no magic values": invariant → named constant + WHY comment). It also avoids a new required env var in every deployment. |
| 3 | Validation codes | `TERMS_VERSION_REQUIRED` (Application `ErrorCodes`, `// VALIDATION`) for empty input. `TERMS_VERSION_UNKNOWN` (Domain `ErrorCodes`, `// USERS`) is used by both the validator (via `DomainErrorCodes` alias, like `InviteUserValidator`) and the domain guard. Both return 422 from the validator. `Cascade(CascadeMode.Stop)` stops an empty value from reporting both codes. | The story requires 422 for an unknown version. The domain guard is defence in depth and reuses the same code, so no constant is duplicated. |
| 4 | How does the entity get the values? | New domain method `User.AcceptTerms(string termsVersion, DateTimeOffset acceptedAt)`, called by the handler right after `User.CreateStudentWith*`. The factory signatures do not change. | The factories have about 70 call sites in tests and production. A domain method keeps the change local and mirrors `ChooseSubjectInterests(…, chosenAt)`. |
| 5 | Time source | Inject `TimeProvider` into both handlers and use `timeProvider.GetUtcNow()`. Tests use `Substitute.For<TimeProvider>()`. | Same as `SaveSubjectInterestsHandler` and its tests. |
| 6 | Where does the new User code go? | New partial file `User.Terms.cs`. | `User.cs` is already 94 lines (≈100 cap). Mirrors `User.Administration.cs`. |
| 7 | Column width | `TermsVersion` `varchar(20)` via a new `AppDbContext` constant `TermsVersionMaxLength = 20` with a WHY comment. `TermsAcceptedAt` `timestamptz` null. No backfill. | Versions are `yyyy-MM-dd`. Existing users stay null, as the story says. |
| 8 | Sign-up line wording and form | Plain `text-caption text-text-muted` paragraph (`TermsNotice`) with the story's sentence, followed by an inline Signal Blue text link «اقرأ الشروط وسياسة الخصوصية». No checkbox and no button, so the page keeps its one mint primary. | Story and design-system rule 1. A plain-text link is used instead of `<Trans>` with an inline-linked phrase because the repo never uses `<Trans>` with i18next-icu. |
| 9 | Where is the line? | In `SignUpPage`, inside the `AuthLayout` card, after the method's form. It is below the email «إنشاء حساب» button, below the phone «إرسال الرمز» button, and below «تحقق» on the OTP step (where registration happens). | One placement covers both methods and every step. |
| 10 | Should the link leave the form? | It opens `/privacy` in a new tab (`target="_blank" rel="noopener noreferrer"`) with a visually hidden "(opens in a new tab)". | Same-tab navigation would lose a half-finished phone/OTP step. |
| 11 | What version does the web send? | `termsVersion` constant in `web/src/shared/lib/terms.ts` (`'2026-10-05'`). It is sent by both sign-up mutations and shown on `/privacy`. It must equal `TermsVersions.Current`. | "The web sends the version it displayed." Putting it in shared code avoids importing the `legal` barrel, which has a top-level locale side effect, into the entry-chunk `session` feature. |
| 12 | Contact section | Text plus a `mailto:` link to `privacyContactEmail = 'privacy@elmanhg.com'` (in `shared/lib/terms.ts`), rendered `dir="ltr"`. | The repo has no contact address. The product domain is `elmanhg.com`. Creating the mailbox is listed under Deferred (D2). |
| 13 | Page bundle size | New feature `web/src/features/legal`. Its `ar`/`en` JSON is registered with `addResourceBundle` when the `/privacy` route chunk loads (`registerLegalLocales()` at module top of `PrivacyPage.tsx`). It is not added to `app/i18n.ts`. | Same as `DashboardPage` (docs/performance.md "Admin-only strings on demand"). The entry and landing budgets are not raised. Only the short sign-up line (session ns) and the footer label (landing ns) are eager. |
| 14 | Page layout | `BrandBar` (logo goes home), `main` in `layoutContainerClassName`. H1, a caption with the version, an intro paragraph, then one white Card with six `h2` sections. No primary button. Public: no `beforeLoad` guard, so signed-in users can open it too. | The design prompt's "Detail screens: cards stacked". The page is reachable from the student More page while signed in. |
| 15 | "Student settings/profile area" | The student More page (`/student/more`, the only account-type page students have). A caption link «الشروط وسياسة الخصوصية» goes below the destination list, for `role === 'student'` only. The nav config is unchanged. | Adding a nav item would create a desktop More menu for students. `AppShell.test` "does not show a More menu to students" pins that design. Desktop students reach the page from sign-up, landing and `/student/more`. |
| 16 | Landing footer | New `LandingFooter` (`<footer>`, contentinfo landmark) after `<main>`, with one caption link to `/privacy`, same tab. | The landing page has no footer today. Same-tab is fine because the visitor is browsing. |
| 17 | Arabic register | The page and the sign-up line use plain Egyptian-friendly wording (copy below, used verbatim). API resx keeps the existing MSA, no-hamza style. Web common error strings keep the existing MSA style. | Story instruction, plus consistency with neighbouring strings. |
| 18 | Stale bundle | When the API returns `TERMS_VERSION_UNKNOWN`/`REQUIRED`, the form shows the root alert «هذه الصفحة قديمة. حدّث الصفحة وحاول مرة أخرى.» | `Form`/`OtpForm` already map unmatched codes to `common:errors.<CODE>`. |
| 19 | Digits | All numbers are Latin ("18", "2026-10-05"). The version is shown as the raw string, not date-formatted. | Design system: Latin digits. Showing the raw version keeps it identical to the stored version. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | `// USERS`: add `public const string TermsVersionUnknown = "TERMS_VERSION_UNKNOWN";` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | `// VALIDATION` group: add `public const string TermsVersionRequired = "TERMS_VERSION_REQUIRED";` |
| `api/Elmanhg.Application/Auth/RegisterWithEmail/RegisterWithEmailCommand.cs` | `public sealed record RegisterWithEmailCommand(string DisplayName, string Email, string Password, string TermsVersion) : IRequest<AuthResult>;` |
| `api/Elmanhg.Application/Auth/RegisterWithEmail/RegisterWithEmailValidator.cs` | Add `using Elmanhg.Domain.Identity;`, `using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;`. Append rule: `RuleFor(x => x.TermsVersion).Cascade(CascadeMode.Stop).ValidateRequired(ErrorCodes.TermsVersionRequired).Must(TermsVersions.IsKnown).WithErrorCode(DomainErrorCodes.TermsVersionUnknown);` (chain one operator per line). |
| `api/Elmanhg.Application/Auth/RegisterWithEmail/RegisterWithEmailHandler.cs` | Ctor: `RegisterWithEmailHandler(UserManager<User> userManager, ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService, TimeProvider timeProvider)`. Steps: (1) conflict check unchanged; (2) `var user = User.CreateStudentWithEmail(request.DisplayName, request.Email);` (3) **new** `user.AcceptTerms(request.TermsVersion, timeProvider.GetUtcNow());` (4–6) `CreateAsync`, failure → `UserCreationFailed`, tokens, unchanged. |
| `api/Elmanhg.Application/Auth/RegisterWithPhone/RegisterWithPhoneCommand.cs` | `public sealed record RegisterWithPhoneCommand(Guid VerificationId, string DisplayName, string TermsVersion) : IRequest<AuthResult>;` |
| `api/Elmanhg.Application/Auth/RegisterWithPhone/RegisterWithPhoneValidator.cs` | Same usings and the same `TermsVersion` rule as the email validator, appended. |
| `api/Elmanhg.Application/Auth/RegisterWithPhone/RegisterWithPhoneHandler.cs` | Ctor appends `TimeProvider timeProvider`. After `var user = User.CreateStudentWithPhone(request.DisplayName, otp.Recipient);` add `user.AcceptTerms(request.TermsVersion, timeProvider.GetUtcNow());`. All other steps unchanged. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Constant block: `// Terms versions are yyyy-MM-dd strings known to the domain (TermsVersions); a schema invariant.` `private const int TermsVersionMaxLength = 20;`. In `ConfigureUsers` add `builder.Property(x => x.TermsVersion).HasMaxLength(TermsVersionMaxLength);` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by the migration. |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | Two keys (see Error codes), next to `DISPLAY_NAME_TOO_LONG`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. `RegisterWithEmailCommand`/`RegisterWithPhoneCommand` gain required `termsVersion: string`. |
| `postman/elmanhg.postman_collection.json` | RegisterWithPhone body adds `"termsVersion": "2026-10-05"`. RegisterWithEmail body adds `"termsVersion": "2026-10-05"`. |
| `api/Elmanhg.Tests/Domain/…` | none modified (new file below). |
| `api/Elmanhg.Tests/Application/Features/Auth/RegisterWithEmail/RegisterWithEmailHandlerTests.cs` | modify: add `private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();` and `private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);`. In the ctor, `_timeProvider.GetUtcNow().Returns(Now);` and pass `_timeProvider` last. Every `new RegisterWithEmailCommand("Mona", Email, Password)` → `new RegisterWithEmailCommand("Mona", Email, Password, TermsVersions.Current)`. Add the new test from the Test plan. |
| `api/Elmanhg.Tests/Application/Features/Auth/RegisterWithEmail/RegisterWithEmailValidatorTests.cs` | modify: add `private const string TermsVersion = TermsVersions.Current;` and append it as the 4th argument of every existing command. Add the new tests. |
| `api/Elmanhg.Tests/Application/Features/Auth/RegisterWithPhone/RegisterWithPhoneHandlerTests.cs` | modify: same `TimeProvider`/`Now` arrangement, ctor arg last. Every `new RegisterWithPhoneCommand(x, "Ahmed")` → `new RegisterWithPhoneCommand(x, "Ahmed", TermsVersions.Current)`. Add the new test. |
| `api/Elmanhg.Tests/Application/Features/Auth/RegisterWithPhone/RegisterWithPhoneValidatorTests.cs` | modify: append `TermsVersions.Current` as the 3rd argument of every existing command. Add the new tests. |
| `api/Elmanhg.Tests/Integration/Auth/AuthTestClient.cs` | `RegisterByPhoneAsync` body → `new { verificationId, displayName = "Student", termsVersion = TermsVersions.Current }` (add `using Elmanhg.Domain.Identity;` if missing). |
| `api/Elmanhg.Tests/Integration/Auth/EmailAuthEndpointTests.cs` | modify: the 4 register bodies add `termsVersion = TermsVersions.Current`. Add 2 tests. |
| `api/Elmanhg.Tests/Integration/Auth/EmailCodeAuthEndpointTests.cs` | modify: the register body (line 20) adds `termsVersion = TermsVersions.Current`. |
| `api/Elmanhg.Tests/Integration/Auth/PhoneAuthEndpointTests.cs` | modify: the 5 direct register bodies add `termsVersion = TermsVersions.Current`. Add 2 tests. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `fortyFifth => fortyFifth.Should().EndWith("_AddUserTermsAcceptance")` after `fortyFourth`. |
| `web/src/shared/api/generated/**` | Regenerated by `npm run gen:api` (never hand-edited). |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (adds `/privacy`). |
| `web/src/features/session/components/EmailSignUpForm.tsx` | `import { termsVersion } from '@/shared/lib/terms';` and `registerWithEmail.mutateAsync({ data: { ...values, termsVersion } })`. |
| `web/src/features/session/components/PhoneSignUp.tsx` | Same import. `data: { verificationId, displayName: start.displayName, termsVersion }`. |
| `web/src/features/session/pages/SignUpPage.tsx` | Import `TermsNotice` from `../components/TermsNotice`. Render `<TermsNotice />` as the last child of `AuthLayout`, after `{method === 'phone' ? … : …}`. |
| `web/src/features/session/i18n/ar.json`, `en.json` | Add `signUp.terms` object (copy below). |
| `web/src/features/landing/pages/LandingPage.tsx` | Import `LandingFooter` from `../components/LandingFooter`. Render `<LandingFooter />` after `</main>`. |
| `web/src/features/landing/i18n/ar.json`, `en.json` | Add `"footer": { "privacy": … }`. |
| `web/src/features/shell/pages/MorePage.tsx` | After `</ul>`: `{role === 'student' ? (<Link to="/privacy" className="self-start text-caption font-bold text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden">{t('more.privacy')}</Link>) : null}` |
| `web/src/features/shell/i18n/ar.json`, `en.json` | `more.privacy`. |
| `web/src/features/shell/pages/MorePage.test.tsx` | modify the test `lists the student destinations that are not in the tab bar`: expected becomes `['Multi-unit exam', 'Assistant', 'Subscription', 'Terms and privacy']`. Add 1 test. |
| `web/src/shared/i18n/ar.json`, `en.json` | `errors.TERMS_VERSION_REQUIRED`, `errors.TERMS_VERSION_UNKNOWN`. |
| `web/src/features/session/pages/SignUpPage.test.tsx` | Add tests (no existing test modified). |
| `web/src/features/landing/pages/LandingPage.test.tsx` | Add 1 test. |
| `docs/PRD.md` | §7.1 step 1 (line 199): after "sign up (phone + one-time code, or email + password)" insert "; the form shows a terms line linking to `/privacy`, and the accepted terms version is stored". §13: append the paragraph "**Notice.** Students are told at sign-up, in a terms line under the form (no pop-up, no opt-in checkbox; dev decision on #273), that their chats, Ask a Teacher threads, answers and grades may be used without names, phone numbers or emails to improve the AI assistant and grading, and that under-18s need a parent's or guardian's agreement. The full text is the public `/privacy` page. Sign-up stores the terms version shown (`TermsVersion`, `TermsAcceptedAt`); an unknown version is rejected; existing users stay null and are not re-prompted (#301)." §14 Privacy row: append " Sign-up shows the terms and privacy line with its training-use clause and stores the accepted terms version (§13, `/privacy`)." §15 User line: `User(id, role[...], phone, email, display_name, status, onboarded_at?, subject_interest_ids[], terms_version?, terms_accepted_at?)`. |
| `docs/training-data.md` | Line 144 → `- [x] **Notice or consent** that interactions are used for training: decided on #273 (2026-10-05): a terms line under the sign-up form links to the public \`/privacy\` page (no pop-up, no opt-in checkbox); sign-up stores the accepted \`TermsVersion\` and \`TermsAcceptedAt\` on the user (#301). Existing users are not re-prompted. Legal review of the text before go-live is the dev's.` |
| `docs/claude-design-prompt.md` | §4 **Landing** block: (a) new bullet before `#/accept-invite`: "- `#/signup` create account (public): the sign-in layout titled «إنشاء حساب», method switch (mobile / email), then under the form, in every step, the caption «بالتسجيل، أنت موافق على الشروط وسياسة الخصوصية، ومنها استخدام المحادثات بدون بياناتك الشخصية لتحسين المساعد الذكي. لو سنك أقل من 18 سنة، لازم ولي أمرك يكون موافق.» followed by the link «اقرأ الشروط وسياسة الخصوصية» (new tab). Not a checkbox and not a button; the form keeps its one mint primary." (b) new bullet after the landing bullet: "- `#/privacy` «الشروط وسياسة الخصوصية» (public, also for signed-in users): app bar with the logo only, H1, caption «الإصدار: 2026-10-05», an intro line, then one white card with six sections: «بنجمع إيه», «بنستخدمها في إيه», «تحسين المساعد الذكي والتصحيح» (chats, Ask a Teacher threads, answers and grades, used without name, phone or email), «مسح محادثاتك», «لو سنك أقل من 18 سنة», «تواصل معانا» (mailto link). No primary button. Linked from the sign-up line, the landing footer («الشروط وسياسة الخصوصية») and the student «المزيد» page." (c) In the landing bullet, append "A footer links to «الشروط وسياسة الخصوصية»." §5: add rule `15. Sign-up records the terms version the student saw; there is no consent checkbox; an unknown version is refused.` §6: add bullet "- **Legal page** (`/privacy`): H1 and a version caption, an intro paragraph, then one white card with `h2` sections (h2 18/24 px 700, body 16 px) separated by 20 px; links Signal Blue; no primary." |
| `docs/prototype.md` | Walkthrough item 1: append "The product also has a terms line under the sign-up form and a public `/privacy` page (linked from sign-up, the landing footer and the student «المزيد» page); the prototype does not simulate them." |
| `docs/backlog.json` | Append to `E18.stories` (after "Student AI chat history and deletion"): `{ "title": "Terms and privacy notice with training-use line at sign-up", "description": "As a student (or parent) I am told, in the terms I accept at sign-up, that my chats with the platform may be used, without personal data, to improve the AI assistant. Dev decision on #273 (2026-10-05): a terms line, no pop-up and no opt-in checkbox. Issue #301.", "tasks": ["API: TermsVersion / TermsAcceptedAt on sign-up, migration, validation, tests", "Web: privacy & terms page, sign-up line, footer/settings links, ar/en, tests", "Docs: PRD privacy row and §13, docs/training-data.md checklist, docs/claude-design-prompt.md §4–§6, docs/prototype.md"] }` |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| A1 | `api/Elmanhg.Domain/Identity/TermsVersions.cs` | static class | `namespace Elmanhg.Domain.Identity;` `public static class TermsVersions` with a WHY comment above `Current`: `// The terms text ships in the web bundle under this version; a new text is a new constant and a release, never a config change.` Members: `public const string Current = "2026-10-05";` `private static readonly string[] Known = [Current];` `public static bool IsKnown(string? version) => version is not null && Known.Contains(version, StringComparer.Ordinal);` New, no Morabh equivalent. |
| A2 | `api/Elmanhg.Domain/Identity/User.Terms.cs` | partial class | `namespace Elmanhg.Domain.Identity;` `public partial class User` with `public string? TermsVersion { get; private set; }`, `public DateTimeOffset? TermsAcceptedAt { get; private set; }`, and `public void AcceptTerms(string termsVersion, DateTimeOffset acceptedAt)` (body in Domain behaviour). Usings: `Core.Errors`, `Elmanhg.Domain.SharedKernel.Exceptions`. New, no Morabh equivalent. |
| A3 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddUserTermsAcceptance.cs` (+ `.Designer.cs`) | EF migration | Generated: `dotnet ef migrations add AddUserTermsAcceptance -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Up: `AddColumn<string>("TermsVersion", "AspNetUsers", type: "character varying(20)", maxLength: 20, nullable: true)` and `AddColumn<DateTimeOffset>("TermsAcceptedAt", "AspNetUsers", type: "timestamp with time zone", nullable: true)`. Down: drop both. No `Sql`, no backfill, nothing else. If the diff contains anything else, stop and report. |
| A4 | `api/Elmanhg.Tests/Domain/Identity/UserTermsTests.cs` | xUnit tests | `namespace Elmanhg.Tests.Domain.Identity;` `public sealed class UserTermsTests`. Tests D1–D4. |
| W1 | `web/src/shared/lib/terms.ts` | constants | `export const termsVersion = '2026-10-05';` (comment: `// must equal TermsVersions.Current in the API`) and `export const privacyContactEmail = 'privacy@elmanhg.com';` |
| W2 | `web/src/features/session/components/TermsNotice.tsx` | component | `export function TermsNotice()`: `useTranslation('session')`. Renders `<p className="text-caption text-text-muted">{t('signUp.terms.notice')}{' '}<Link to="/privacy" target="_blank" rel="noopener noreferrer" className="font-bold text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden">{t('signUp.terms.link')}<span className="sr-only"> {t('signUp.terms.newTab')}</span></Link></p>` |
| W3 | `web/src/features/landing/components/LandingFooter.tsx` | component | `export function LandingFooter()`: `useTranslation('landing')`. `<footer className={cn(layoutContainerClassName, 'border-t border-border py-6')}><Link to="/privacy" className="text-caption font-bold text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden">{t('footer.privacy')}</Link></footer>` |
| W4 | `web/src/features/legal/i18n/ar.json` | locale | Exact copy below. |
| W5 | `web/src/features/legal/i18n/en.json` | locale | Exact copy below. |
| W6 | `web/src/features/legal/locales.ts` | i18n registration | Mirror `features/dashboard/locales.ts`: `export function registerLegalLocales(): void { const i18n = getI18n(); i18n.addResourceBundle('ar', 'legal', ar, true, true); i18n.addResourceBundle('en', 'legal', en, true, true); }` |
| W7 | `web/src/features/legal/components/PrivacySection.tsx` | component | `export interface PrivacySectionProps { title: string; children: ReactNode }`. `export function PrivacySection({ title, children }: PrivacySectionProps)` → `const headingId = useId();` `<section aria-labelledby={headingId} className="flex flex-col gap-2"><h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">{title}</h2>{children}</section>` |
| W8 | `web/src/features/legal/pages/PrivacyPage.tsx` | page | Top of module after the imports: `registerLegalLocales();`. `const sectionKeys = ['collect', 'use', 'training', 'deleteChats', 'minors'] as const;` `export function PrivacyPage()`: `useTranslation('legal')`. Markup: `<div className="flex min-h-dvh flex-col">` `<BrandBar />` `<main id="main" className={cn(layoutContainerClassName, 'flex flex-col gap-6 pt-6 pb-10')}>` `<div className="flex flex-col gap-1"><h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('privacy.title')}</h1><p className="text-caption text-text-muted">{t('privacy.version', { version: termsVersion })}</p></div>` `<p className="text-body">{t('privacy.intro')}</p>` `<div className="flex flex-col gap-5 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">` then `sectionKeys.map((key) => <PrivacySection key={key} title={t(\`privacy.${key}.title\`)}><p className="text-body">{t(\`privacy.${key}.body\`)}</p></PrivacySection>)`, then `<PrivacySection title={t('privacy.contact.title')}><p className="text-body">{t('privacy.contact.body')}{' '}<a href={\`mailto:${privacyContactEmail}\`} dir="ltr" className="font-bold text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden">{privacyContactEmail}</a></p></PrivacySection>` and the closing tags. Under 80 lines. |
| W9 | `web/src/features/legal/index.ts` | barrel | `export { PrivacyPage } from './pages/PrivacyPage';` |
| W10 | `web/src/routes/privacy.tsx` | file route | `import { createFileRoute } from '@tanstack/react-router'; import { PrivacyPage } from '@/features/legal'; export const Route = createFileRoute('/privacy')({ component: PrivacyPage });` No `beforeLoad`. |
| W11 | `web/src/features/legal/pages/PrivacyPage.test.tsx` | tests | Tests P1–P6. |

### Copy (verbatim; do not reword)
`web/src/features/session/i18n/ar.json` → inside `signUp`:
```json
"terms": {
  "notice": "بالتسجيل، أنت موافق على الشروط وسياسة الخصوصية، ومنها استخدام المحادثات بدون بياناتك الشخصية لتحسين المساعد الذكي. لو سنك أقل من 18 سنة، لازم ولي أمرك يكون موافق.",
  "link": "اقرأ الشروط وسياسة الخصوصية",
  "newTab": "(بتفتح في تبويب جديد)"
}
```
`en.json` → inside `signUp`:
```json
"terms": {
  "notice": "By signing up, you agree to the terms and privacy policy, including the use of chats without your personal data to improve the AI assistant. If you are under 18, a parent or guardian must agree.",
  "link": "Read the terms and privacy policy",
  "newTab": "(opens in a new tab)"
}
```
Landing `footer.privacy` and shell `more.privacy`: ar `"الشروط وسياسة الخصوصية"`, en `"Terms and privacy"`.

Common `errors` (both codes take the same text): ar `"هذه الصفحة قديمة. حدّث الصفحة وحاول مرة أخرى."`, en `"This page is out of date. Reload it and try again."`

`web/src/features/legal/i18n/ar.json`:
```json
{
  "privacy": {
    "title": "الشروط وسياسة الخصوصية",
    "version": "الإصدار: {version}",
    "intro": "الصفحة دي بتشرح المنهج بيستخدم بياناتك إزاي. بالتسجيل، أنت موافق على الكلام ده.",
    "collect": {
      "title": "بنجمع إيه",
      "body": "اسمك، ورقم موبايلك أو بريدك الإلكتروني، والمواد اللي اخترتها، وإجاباتك ودرجاتك، ومحادثاتك مع المساعد الذكي، وأسئلتك في «اسأل معلّم» وردود المعلمين عليها، وبيانات اشتراكك ومدفوعاتك."
    },
    "use": {
      "title": "بنستخدمها في إيه",
      "body": "علشان نشغّل حسابك، ونختار لك الأسئلة المناسبة ونتابع إتقانك، ونوصّل سؤالك لمعلم المادة، ونفعّل اشتراكك. المعلمين بيشوفوا اسمك بس، مش رقمك ولا بريدك."
    },
    "training": {
      "title": "تحسين المساعد الذكي والتصحيح",
      "body": "ممكن نستخدم محادثاتك مع المساعد، وأسئلة «اسأل معلّم»، وإجاباتك ودرجاتك علشان نحسّن المساعد الذكي والتصحيح. قبل ما نستخدمها بنشيل اسمك ورقم موبايلك وبريدك، وبنحط كود مكان حسابك. لو كتبت اسم جوه رسالة ممكن يفضل موجود، فمتكتبش بيانات شخصية في المحادثات."
    },
    "deleteChats": {
      "title": "مسح محادثاتك",
      "body": "تقدر تمسح أي محادثة مع المساعد من «محادثاتي السابقة». المحادثة بتتمسح ومعاها نسختها اللي بنستخدمها في التحسين. أسئلة «اسأل معلّم» والإجابات والدرجات بتفضل محفوظة من غير اسمك."
    },
    "minors": {
      "title": "لو سنك أقل من 18 سنة",
      "body": "لازم ولي أمرك يكون موافق على الشروط دي قبل ما تسجّل."
    },
    "contact": {
      "title": "تواصل معانا",
      "body": "عندك سؤال عن بياناتك؟ ابعت لنا على"
    }
  }
}
```
`web/src/features/legal/i18n/en.json` (no apostrophes, because ICU treats `'` specially):
```json
{
  "privacy": {
    "title": "Terms and privacy",
    "version": "Version: {version}",
    "intro": "This page explains how Elmanhg uses your data. By signing up, you agree to it.",
    "collect": {
      "title": "What we collect",
      "body": "Your name, your mobile number or email, the subjects you chose, your answers and grades, your chats with the AI assistant, your Ask a Teacher questions and the replies from teachers, and your subscription and payment records."
    },
    "use": {
      "title": "How we use it",
      "body": "To run your account, pick the right questions for you and track your mastery, send your question to a subject teacher, and turn on your subscription. Teachers see only your name, never your number or email."
    },
    "training": {
      "title": "Improving the AI assistant and grading",
      "body": "We may use your chats with the assistant, your Ask a Teacher questions, and your answers and grades to improve the AI assistant and grading. Before we use them, we remove your name, mobile number and email and replace your account with a code. A name you type inside a message may stay, so do not write personal details in chats."
    },
    "deleteChats": {
      "title": "Deleting your chats",
      "body": "You can delete any assistant chat from Past chats. The chat is deleted together with the copy we use for improvement. Ask a Teacher questions, answers and grades are kept without your name."
    },
    "minors": {
      "title": "If you are under 18",
      "body": "A parent or guardian must agree to these terms before you sign up."
    },
    "contact": {
      "title": "Contact us",
      "body": "Questions about your data? Write to us at"
    }
  }
}
```

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `Application.Exceptions.ErrorCodes.TermsVersionRequired` | `TERMS_VERSION_REQUIRED` | `RegisterWithEmailValidator`, `RegisterWithPhoneValidator` | `ApplicationValidationCoreException` (pipeline) | 422 |
| `Domain.SharedKernel.Exceptions.ErrorCodes.TermsVersionUnknown` | `TERMS_VERSION_UNKNOWN` | both validators (`DomainErrorCodes` alias); `User.AcceptTerms` guard | validator → `ApplicationValidationCoreException`; domain → `BusinessRuleViolationCoreException` | 422 (validator; the domain guard is unreachable through the API) |

Resx (`<data name="…" xml:space="preserve"><value>…</value></data>`):
| Key | en | ar |
|-----|----|----|
| `TERMS_VERSION_REQUIRED` | `The terms version is required. Reload the page and try again.` | `اصدار الشروط مطلوب. حدث الصفحة وحاول مرة اخرى.` |
| `TERMS_VERSION_UNKNOWN` | `These terms are out of date. Reload the page and try again.` | `اصدار الشروط غير معروف. حدث الصفحة وحاول مرة اخرى.` |

## Domain behaviour
```csharp
public void AcceptTerms(string termsVersion, DateTimeOffset acceptedAt)
{
    if (!TermsVersions.IsKnown(termsVersion))
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.TermsVersionUnknown);
    }

    TermsVersion = termsVersion;
    TermsAcceptedAt = acceptedAt;
    UpdationDate = acceptedAt;
}
```
- Guard, then mutate, then stamp. `UpdationDate` uses the same instant (mirrors `ChooseSubjectInterests`).
- No role guard: only the two student sign-up handlers call it.
- Calling it again overwrites (no re-acceptance flow is built). Nothing is backfilled for existing users; both properties stay `null`.

## API surface
| Method | Route | Policy | Request | Response |
|--------|-------|--------|---------|----------|
| POST | `api/auth/register/email` | `[AllowAnonymous]` + `AuthRateLimitPolicies.Credentials` (unchanged) | `RegisterWithEmailCommand { displayName, email, password, termsVersion }` | `AuthResult` (unchanged) |
| POST | `api/auth/register/phone` | `[AllowAnonymous]` + `Credentials` (unchanged) | `RegisterWithPhoneCommand { verificationId, displayName, termsVersion }` | `AuthResult` (unchanged) |

`AuthController.cs` is not edited. The commands are bound directly.

Web route: `GET /privacy` (public SPA route, lazy chunk).

## Test plan
### API (xUnit v3, NSubstitute, FluentAssertions as pinned)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| D1 | `UserTermsTests` | `AcceptTerms_CurrentVersion_RecordsVersionAndAcceptedAt` | `TermsVersion == TermsVersions.Current`, `TermsAcceptedAt == at`, `UpdationDate == at` (fixed `at = new DateTimeOffset(2026,10,5,9,0,0,TimeSpan.Zero)`) |
| D2 | `UserTermsTests` | `AcceptTerms_UnknownVersion_ThrowsTermsVersionUnknown` | `BusinessRuleViolationCoreException` with `ErrorCode == ErrorCodes.TermsVersionUnknown`; `TermsVersion` and `TermsAcceptedAt` stay null |
| D3 | `UserTermsTests` | `CreateStudentWithEmail_Always_HasNoTermsAcceptance` | new student: `TermsVersion` null, `TermsAcceptedAt` null |
| D4 | `UserTermsTests` | `IsKnown_Values_MatchesOnlyKnownVersions` (`[Theory]`: `"2026-10-05"`→true, `"2026-10-04"`→false, `""`→false, `null`→false, `"2026-10-05 "`→false) | `TermsVersions.IsKnown(value)` equals expected |
| V1 | `RegisterWithEmailValidatorTests` | `Validate_EmptyTermsVersion_FailsWithTermsVersionRequired` | error codes contain `TERMS_VERSION_REQUIRED` |
| V2 | `RegisterWithEmailValidatorTests` | `Validate_EmptyTermsVersion_DoesNotReportUnknownVersion` | error codes do not contain `TERMS_VERSION_UNKNOWN` (cascade stop) |
| V3 | `RegisterWithEmailValidatorTests` | `Validate_UnknownTermsVersion_FailsWithTermsVersionUnknown` | `"2020-01-01"` → contains `DomainErrorCodes.TermsVersionUnknown` |
| V4 | `RegisterWithPhoneValidatorTests` | `Validate_EmptyTermsVersion_FailsWithTermsVersionRequired` | as V1 |
| V5 | `RegisterWithPhoneValidatorTests` | `Validate_UnknownTermsVersion_FailsWithTermsVersionUnknown` | as V3 |
| — | both validator test classes | existing `Validate_ValidCommand_Passes` (modified args only) | still `IsValid` with `TermsVersions.Current`, so the valid case for the new rule is covered |
| H1 | `RegisterWithEmailHandlerTests` | `Handle_NewEmail_RecordsTermsAcceptance` | `_userManager.Received(1).CreateAsync(Arg.Is<User>(x => x.TermsVersion == TermsVersions.Current && x.TermsAcceptedAt == Now), Password)` |
| H2 | `RegisterWithPhoneHandlerTests` | `Handle_VerifiedOtpNewPhone_RecordsTermsAcceptance` | `_userManager.Received(1).CreateAsync(Arg.Is<User>(x => x.TermsVersion == TermsVersions.Current && x.TermsAcceptedAt == Now))` |
| — | existing handler throwing tests | unchanged behaviour, args updated | still assert type + code + `CreateAsync`/token `DidNotReceive` |
| I1 | `EmailAuthEndpointTests` | `RegisterWithEmail_CurrentTermsVersion_PersistsTermsAcceptance` | 200. A fresh-scope `AppDbContext.Users.AsNoTracking()` user has `TermsVersion == TermsVersions.Current`, and `TermsAcceptedAt` is between `before` and `after` (`DateTimeOffset.UtcNow` captured around the call, outside the assertion path) |
| I2 | `EmailAuthEndpointTests` | `RegisterWithEmail_UnknownTermsVersion_Returns422AndCreatesNoUser` | 422, `code` contains `TERMS_VERSION_UNKNOWN`, no user with that email in the DB |
| I3 | `PhoneAuthEndpointTests` | `RegisterWithPhone_CurrentTermsVersion_PersistsTermsAcceptance` | 200. The DB user (by `UserName == phone`) has `TermsVersion == Current` and `TermsAcceptedAt` not null |
| I4 | `PhoneAuthEndpointTests` | `RegisterWithPhone_UnknownTermsVersion_Returns422AndOtpStaysUsable` | 422, `code` contains `TERMS_VERSION_UNKNOWN`. A second POST with the same `verificationId` and `TermsVersions.Current` returns 200 (the validator ran before the OTP was consumed) |
| M1 | `AppDbContextTests` (existing, modified) | migration list | ends with `_AddUserTermsAcceptance` |

Repository and controller tests are out of scope (convention).

### Web (Vitest + RTL + MSW, `renderApp`, `userEvent.setup()`)
| # | Test file | `it(...)` | Asserts |
|---|-----------|-----------|---------|
| S1 | `SignUpPage.test.tsx` | `shows the terms line with a link to the privacy page` | text `/By signing up, you agree to the terms and privacy policy/` visible; link `{ name: /Read the terms and privacy policy/ }` has `href="/privacy"` and `target="_blank"` |
| S2 | `SignUpPage.test.tsx` | `keeps the terms line under the code step of a mobile sign-up` | after "Send code" (`getSendOtpMockHandler()`), the "Verification code" field and the terms text are both visible |
| S3 | `SignUpPage.test.tsx` | `shows the terms line in Arabic` | `renderApp('/signup', { lng: 'ar' })` → text containing `لو سنك أقل من 18 سنة، لازم ولي أمرك يكون موافق.` |
| S4 | `SignUpPage.test.tsx` | `sends the displayed terms version with an email sign-up` | `http.post('*/api/auth/register/email')` captures the body; after the onboarding heading, `body.termsVersion === termsVersion` (imported from `@/shared/lib/terms`) |
| S5 | `SignUpPage.test.tsx` | `sends the displayed terms version with a mobile sign-up` | same for `*/api/auth/register/phone` via `signUpWithPhone` |
| S6 | `SignUpPage.test.tsx` | `asks to reload when the terms version is out of date` | register/email → `apiError(422, 'TERMS_VERSION_UNKNOWN')`; `findByRole('alert')` has text `This page is out of date. Reload it and try again.` |
| — | `SignUpPage.test.tsx` existing `has no axe violations` | unchanged | now also covers the notice link |
| P1 | `PrivacyPage.test.tsx` | `shows every section and the terms version` | h1 `Terms and privacy`. h2s, in order: `What we collect`, `How we use it`, `Improving the AI assistant and grading`, `Deleting your chats`, `If you are under 18`, `Contact us`. Text `Version: 2026-10-05` |
| P2 | `PrivacyPage.test.tsx` | `says chats are used without personal data` | text `/we remove your name, mobile number and email/` |
| P3 | `PrivacyPage.test.tsx` | `links to the privacy mailbox` | link `privacy@elmanhg.com` has `href="mailto:privacy@elmanhg.com"` |
| P4 | `PrivacyPage.test.tsx` | `opens for a signed-in student without redirecting` | `renderApp('/privacy', { session: testSessions.student })` → h1 `Terms and privacy` |
| P5 | `PrivacyPage.test.tsx` | `renders right-to-left in Arabic` | `lng: 'ar'` → h1 `الشروط وسياسة الخصوصية`, text `الإصدار: 2026-10-05` (Latin digits), `document.documentElement` has `dir="rtl"` |
| P6 | `PrivacyPage.test.tsx` | `has no axe violations` | `(await axe(container)).violations` equals `[]` |
| L1 | `LandingPage.test.tsx` | `links to the terms and privacy page from the footer` | `within(getByRole('contentinfo')).getByRole('link', { name: 'Terms and privacy' })`; click → h1 `Terms and privacy` |
| R1 | `MorePage.test.tsx` (modified) | `lists the student destinations that are not in the tab bar` | expected `['Multi-unit exam', 'Assistant', 'Subscription', 'Terms and privacy']` |
| R2 | `MorePage.test.tsx` | `opens the terms and privacy page from the student More page` | click link → h1 `Terms and privacy` |
| — | `MorePage.test.tsx` existing admin list test | unchanged | proves admins do not get the link |

## Definition of done
- [ ] `TermsVersions.Current == "2026-10-05"` equals `termsVersion` in `web/src/shared/lib/terms.ts`.
- [ ] `User.TermsVersion`/`TermsAcceptedAt` exist in `User.Terms.cs`. `AcceptTerms` guards unknown versions with `TERMS_VERSION_UNKNOWN` and sets `UpdationDate`. The `User.CreateStudentWith*` signatures are unchanged.
- [ ] Both register commands carry `TermsVersion`. Both validators return 422 `TERMS_VERSION_REQUIRED` (empty) / `TERMS_VERSION_UNKNOWN` (unknown) with cascade stop. Both handlers call `AcceptTerms(request.TermsVersion, timeProvider.GetUtcNow())` before `CreateAsync`.
- [ ] Migration `AddUserTermsAcceptance` only adds two nullable `AspNetUsers` columns (no backfill, no drop). The snapshot and the `AppDbContextTests` list are updated.
- [ ] Both resx files have both keys. The web common `errors` has both keys in ar and en.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` are regenerated with `termsVersion` required. `npm run gen:api` produces no diff. The Postman bodies include `termsVersion`.
- [ ] Every existing integration register call sends `termsVersion`. D1–D4, V1–V5, H1–H2 and I1–I4 pass. `dotnet build` has zero warnings, `dotnet test` is green, and `dotnet format --verify-no-changes` exits 0.
- [ ] The sign-up page shows `TermsNotice` (caption, no checkbox, no extra button) under the form in both methods and on the OTP step. The link goes to `/privacy` in a new tab with an sr-only hint.
- [ ] `/privacy` is public, with ar/en and RTL. Its strings live only in `features/legal/i18n` and are registered lazily (`app/i18n.ts` is not edited). It shows the version, six sections and a mailto link, and has no mint button.
- [ ] The landing footer and the student More page link to `/privacy`. The admin and teacher More pages are unchanged. There is no student desktop More menu.
- [ ] S1–S6, P1–P6, L1, R1–R2 pass. Only `MorePage.test.tsx` R1 is modified among existing web tests. tsc, eslint (`--max-warnings=0`), prettier and vitest exit 0.
- [ ] `npm run build && npm run perf:budget` passes with `budgets.json` unchanged.
- [ ] Latin digits everywhere. Logical properties only. No hex, arbitrary values or physical-direction classes. No `#<number>` in code comments. No new packages.
- [ ] Docs updated as listed: PRD §7.1/§13/§14/§15, `docs/training-data.md` item ticked, `docs/claude-design-prompt.md` §4/§5/§6, `docs/prototype.md` item 1, `docs/backlog.json` E18 story appended.
- [ ] The report lists Deferred D1 (legal review) and D2 (privacy mailbox) for GitHub issues.
