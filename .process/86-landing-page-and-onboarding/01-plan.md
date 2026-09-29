# Plan — [E7.S2] Landing page and onboarding (#86)

## Goal
An anonymous visitor opening `/` now sees a marketing landing page (aurora hero with the live count of teacher-approved questions against the 100,000 promise, three value props, the plan catalogue, and one "ابدأ مجانًا" call to action that goes to sign-up). They no longer get bounced straight to the sign-in form. A new student is sent to a one-screen onboarding step after sign-up to choose subjects of interest (they can skip it). Home then lists those subjects first, and the student can edit them later. The product records five first-party funnel events (landing viewed → sign-up started → account created → onboarding done → first quiz answer), so the PRD §2.1 "landing to first graded quiz in under 3 minutes" goal can be measured.

## Scope
**In:**
- api: `User` onboarding state and subject interests; `GET`/`PUT /api/students/me/subject-interests`; `needsOnboarding` on every auth result; `isInterested` on the mastery overview (interested subjects first); anonymous rate-limited `POST /api/analytics/funnel-events` with a `FunnelEvent` table; one migration.
- web:
  - Landing page at `/`.
  - Onboarding page at `/onboarding`, plus the student-area guard that sends students who still need onboarding there.
  - Home split into "موادك" / "مواد أخرى" with an "تعديل موادي" link.
  - Funnel tracker, called from landing, sign-up, onboarding and the first quiz answer.
  - Reusable public plan cards.
- docs: PRD §7.1, §10.3, §15; `docs/mastery.md`; `docs/claude-design-prompt.md` §4; `docs/prototype.md`; new `docs/analytics.md`.
- Postman, OpenAPI and the Orval client, all regenerated.

**Out:**
- The funnel report and dashboard card. The events are captured here; the read side is the "Sign-up funnel" row added to PRD §10.3, built by #104 (Dashboard metrics queries) and #105 (Dashboard UI).
- The next recommended lesson preferring interested subjects. It stays as it is today.
- Grade and track choice. Not in the PRD: Thanaweya Amma is the only curriculum (PRD §4.3).
- Free-tier gating (#87).

**Deferred:** none. Nothing here needs credentials or an external provider. Analytics is first-party.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | What does onboarding ask? | Only "choose subjects of interest": a multi-select of all subjects in admin order. | PRD §7.1 step 1 and the story sub-task. There are no grades or tracks in scope (PRD §4.3). |
| 2 | Where are interests stored? | On `User`: `List<Guid> SubjectInterestIds` (PostgreSQL `uuid[]`, default `'{}'`) plus `DateTimeOffset? OnboardedAt`. No new table. | One aggregate, one save, and no new repository. `Question.Tags` already maps a primitive list. Stale ids (a deleted subject) are harmless: every read intersects with the live subjects. |
| 3 | Is choosing required? | No. The primary "متابعة" needs at least one subject (client schema), and a secondary "تخطّي الآن" saves an empty list. Both set `OnboardedAt`. The API accepts an empty list. | Keeps the 3-minute sign-up goal and avoids a dead end when no subjects exist yet. |
| 4 | How does the SPA know onboarding is due? | `AuthUserResult` gains `bool NeedsOnboarding` (= `Role == Student && OnboardedAt == null`), which sets `Session.needsOnboarding`. The `/student` layout guard redirects to `/onboarding` while it is true. | Every sign-in and refresh already returns `AuthUserResult`, so no extra request is needed and it survives reloads. |
| 5 | Existing students after the migration? | The migration backfills `OnboardedAt = CreationDate` for `Role = 'Student'`. | Students who already use the product are not forced through onboarding. |
| 6 | Editing interests later | The same `/onboarding` page. In edit mode (`needsOnboarding` false) it shows "العودة إلى الرئيسية" instead of "تخطّي الآن". Home links to it as "تعديل موادي". | One screen and one endpoint. |
| 7 | How do interests change Home? | `SubjectMasteryResult` gains `IsInterested`, and the overview orders interested subjects first (stable, keeping admin order within each group). The web shows "موادك" (interested) and "مواد أخرى" (the rest). With no interests, one "موادك" list shows everything, as today. | Makes the choice visible without hiding content. |
| 8 | Which policy guards subject interests? | `DefaultCodes.ProgressViewOwn` (Student only). | Same precedent as the #85 `BrowseController` student-own reads. PRD §16 has no separate profile capability. |
| 9 | Are interests audited? | No `IAuditableCommand`. | PRD §14 and §17.13 audit content changes and validation decisions, not student preferences. |
| 10 | Analytics provider | First-party: an anonymous `POST /api/analytics/funnel-events` writes a `FunnelEvent` row. Adapted from Morabh `AnalyticsController` / `RecordAppEvents`: a single event instead of a batch, `Guid AnonymousId`, no platform or app version, and the server stamps the time. | No third-party tracker, so no PII leaves the platform and no credentials are needed. The server clock means the client cannot backdate events. |
| 11 | Anonymous write endpoint vs the skill's "[AllowAnonymous] only for public reads" | A justified exception: landing visitors have no account. Guarded by a new fixed-window IP rate-limit policy (`Analytics:FunnelEventPermitLimit` per `Analytics:FunnelEventWindowSeconds`, code defaults 60/60) and by the validator. It stores only a random browser id, an optional user id and an enum. | Mirrors Morabh's anonymous analytics endpoint. The rate limit closes the abuse vector. |
| 12 | Linking a visitor to an account | The client sends a random `crypto.randomUUID()` kept in `localStorage['elmanhg.anonymousId']`. The server adds `UserId` from `ICurrentUserService` when a bearer token is present. | A device-level funnel with no fingerprinting. The funnel counts distinct `AnonymousId` per step, so duplicates are harmless. |
| 13 | "First quiz answer" detection | Sent from `useQuizAnswer` on a successful answer, at most once per browser (`localStorage['elmanhg.funnel.FirstQuizAnswered']`). | The client-side once-flag keeps the table small. Distinct counting keeps the step correct anyway. |
| 14 | Analytics failures | Fire-and-forget. A failed POST is swallowed in the tracker. | Analytics must never block or break the journey. |
| 15 | Landing counter wording | Hero H1 "{count} سؤال راجعها معلّمون حقيقيون" from the anonymous cached `GET /api/questions/servable-count`, with the goal line "هدفنا 100,000 سؤال…". While the count is loading or has failed, the headline shows no number. The goal is a web constant `marketedQuestionGoal = 100_000`. | PRD §2.1.4 (a live, honest counter) and the design prompt ("100,000 سؤال" style headline using the real number). |
| 16 | Landing plan cards | Reuse the #99 public `GET /api/plans`. Extract `usePlanCardContent` from `PlanCardGrid` and add `PublicPlanCards` (no actions, no active badge), exported from the subscription barrel. | No duplicated feature and price copy. Features only import each other through barrels. |
| 17 | Landing CTA target | "ابدأ مجانًا" goes to `/signup`, and "تسجيل الدخول" in the top bar goes to `/login`. A signed-in user at `/` is redirected to their role home (existing `redirectSignedIn`). | The design prompt's "go to #/student as سارة" is prototype-only. The product needs a real account. `redirectToHome` becomes dead code and is removed. |
| 18 | Aurora usage | Only the new `LandingHero` component uses `bg-aurora`. | `.claude/design-system.md` names exactly LandingHero and SubscribeHeader. |
| 19 | Interests cap | `Students:SubjectInterestsMaxCount` (code default 50) in the new `StudentsOptions`. | Bounds the `Contains` query. Caps live in Options (skill §8.1). |
| 20 | Where the onboarding page lives | Top-level route `/onboarding`, outside `AppShell`, with a centered layout like the auth pages. Guard: `requireRole(session, 'student', href)`. | A first-run step should have no navigation chrome, and it must not go through the `/student` onboarding guard (that would loop). |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Identity/User.cs` | Add `OnboardedAt`, `SubjectInterestIds`, `NeedsOnboarding`, `ChooseSubjectInterests` (see Domain behaviour). |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | `// USERS`: add `public const string UserNotStudent = "USER_NOT_STUDENT";` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add a `// STUDENTS` group: `SubjectInterestsTooMany = "SUBJECT_INTERESTS_TOO_MANY"`, `SubjectInterestsDuplicate = "SUBJECT_INTERESTS_DUPLICATE"`. Add an `// ANALYTICS` group: `FunnelAnonymousIdRequired = "FUNNEL_ANONYMOUS_ID_REQUIRED"`, `FunnelEventTypeInvalid = "FUNNEL_EVENT_TYPE_INVALID"`. |
| `api/Elmanhg.Application/Auth/Shared/AuthUserResult.cs` | `public sealed record AuthUserResult(Guid Id, string DisplayName, string Role, string? PhoneNumber, string? Email, bool NeedsOnboarding);` |
| `api/Elmanhg.Application/Auth/Shared/AuthResultGenerator.cs` | Pass `user.NeedsOnboarding` as the last `AuthUserResult` argument. |
| `api/Elmanhg.Application/Mastery/Shared/SubjectMasteryResult.cs` | Append `bool IsInterested` as the last positional member. |
| `api/Elmanhg.Application/Mastery/Shared/MasteryOverviewResultGenerator.cs` | New signature `Generate(IReadOnlyCollection<LessonMasteryCount> lessons, IReadOnlyList<Subject> subjects, IReadOnlyCollection<Guid> interestedSubjectIds, int streakDays, LessonMasteryCount? next, Lesson? nextLesson)`. Subjects are `.OrderByDescending(subject => interestedSubjectIds.Contains(subject.Id))` (stable) before `.Select`. `GenerateSubject(subject, totals, isInterested)` passes `IsInterested`. |
| `api/Elmanhg.Application/Mastery/GetMasteryOverview/GetMasteryOverviewHandler.cs` | Add ctor dependency `IUserRepository userRepository` right after `ILessonRepository lessonRepository`. After the auth guard: `var student = await userRepository.GetByIdAsync(userId, cancellationToken, asNoTracking: true).ConfigureAwait(false);`. Pass `student?.SubjectInterestIds ?? []` to the generator. |
| `api/Elmanhg.Application/DependencyInjection.cs` | Register `StudentsOptions` and `AnalyticsOptions` with `.BindConfiguration(X.SectionName).ValidateDataAnnotations().ValidateOnStart()`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Add `public DbSet<FunnelEvent> FunnelEvents { get; set; }`. In `ConfigureUsers` add `builder.Property(x => x.SubjectInterestIds).IsRequired().HasDefaultValueSql("'{}'");`. Add a `ConfigureFunnelEvents(modelBuilder)` call and method: `Type` `.HasConversion<string>().HasMaxLength(EnumColumnMaxLength)`; `HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict)`; `HasIndex(x => new { x.Type, x.OccurredAt })`; `HasIndex(x => x.AnonymousId)`. Add `modelBuilder.Entity<FunnelEvent>().HasQueryFilter(x => !x.IsDeleted);` to the global filter method. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<IFunnelEventRepository, FunnelEventRepository>();` |
| `api/Elmanhg.Api/RateLimiting/AuthRateLimiting.cs` | After the auth `Configure`, add `services.AddOptions<RateLimiterOptions>().Configure<IOptions<AnalyticsOptions>>((rateLimiter, analyticsOptions) => rateLimiter.AddPolicy(AnalyticsRateLimitPolicies.FunnelEvents, httpContext => CreateFixedWindow(httpContext, analyticsOptions.Value.FunnelEventPermitLimit, analyticsOptions.Value.FunnelEventWindowSeconds)));` |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | Five new keys (see Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | Add `"Students": { "SubjectInterestsMaxCount": 50 }` and `"Analytics": { "FunnelEventPermitLimit": 60, "FunnelEventWindowSeconds": 60 }`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated with the migration. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `["Students:SubjectInterestsMaxCount"] = "50"`, `["Analytics:FunnelEventPermitLimit"] = "1000"` and `["Analytics:FunnelEventWindowSeconds"] = "60"` to the in-memory config. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `twentyFourth => twentyFourth.Should().EndWith("_AddOnboardingAndFunnelEvents")` to the migration list. |
| `api/Elmanhg.Tests/Application/Features/Mastery/GetMasteryOverview/GetMasteryOverviewHandlerTests.cs` | Add a `IUserRepository _userRepository` substitute to the ctor call. Modify the expected records in `Handle_Subjects_ReturnsEverySubjectInOrderWithWeightedMastery` to end with `false`. Add the new test from the Test plan. |
| `api/Elmanhg.Tests/Domain/Identity/UserTests.cs` | Add the tests listed in the Test plan. |
| `api/Elmanhg.Tests/Integration/Auth/PhoneAuthEndpointTests.cs` | Add one test (see Test plan). |
| `api/Elmanhg.Tests/Integration/Mastery/MasteryOverviewEndpointTests.cs` | Add one test (see Test plan). |
| `postman/elmanhg.postman_collection.json` | New folder `Onboarding` after `Browse`: "Get subject interests" (GET), "Save subject interests" (PUT, body `{ "subjectIds": ["{{subjectId}}"] }`), "Get subject interests after save". Folder description: "Sign in as a Student." New folder `Analytics` last: "Record funnel event" (POST, `"auth": { "type": "noauth" }`, body `{ "anonymousId": "{{$guid}}", "type": "LandingViewed" }`). Every request has a `status is 200` test. |
| `web/src/routes/index.tsx` | `beforeLoad: ({ context }) => { redirectSignedIn(context.sessionStore.get(), undefined); }`, `component: LandingPage` (from `@/features/landing`). |
| `web/src/routes/student/route.tsx` | In `beforeLoad`, after `requireRole(...)`, call `requireOnboarded(context.sessionStore.get());`. |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (vitest or vite build). |
| `web/src/app/i18n.ts` | Register the `landing` and `onboarding` namespaces (imports from `@/features/landing/locales` and `@/features/onboarding/locales`, and add both to `resources.ar`, `resources.en` and `ns`). |
| `web/src/features/session/sessionStore.ts` | `Session` gains `needsOnboarding: boolean`. |
| `web/src/features/session/authSession.ts` | `toSession` returns `{ userId, displayName, role, needsOnboarding: user.needsOnboarding }`. |
| `web/src/features/session/guards.ts` | Add `export function requireOnboarded(session: Session \| null): void { if (session?.role === 'student' && session.needsOnboarding) { throw redirect({ to: '/onboarding' }); } }`. Delete `redirectToHome`. |
| `web/src/features/session/index.ts` | Export `requireOnboarded`. Drop `redirectToHome`. |
| `web/src/features/session/pages/SignUpPage.tsx` | Call `useFunnelEventOnMount('SignUpStarted')` (from `@/features/analytics`). |
| `web/src/features/session/components/PhoneSignUp.tsx` | In `onVerified`, after `mutateAsync` resolves and before `startSession`, call `trackFunnelEvent('SignUpCompleted')`. |
| `web/src/features/session/components/EmailSignUpForm.tsx` | Same: `const result = await registerWithEmail.mutateAsync(...)`; `trackFunnelEvent('SignUpCompleted')`; `startSession(result)`. |
| `web/src/features/quiz/hooks/useQuizAnswer.ts` | In the mutation `onSuccess`, add `trackFunnelEvent('FirstQuizAnswered', { once: true });`. |
| `web/src/features/mastery/pages/StudentHomePage.tsx` | Replace the H2 and subject list block with `<HomeSubjects subjects={data.subjects} />`. |
| `web/src/features/mastery/i18n/ar.json`, `en.json` | Add `subjects.others` ("مواد أخرى" / "Other subjects") and `subjects.edit` ("تعديل موادي" / "Edit my subjects"). |
| `web/src/features/subscription/components/PlanCardGrid.tsx` | Use `usePlanCardContent(catalogue)` for each card's `title`, `priceLines` and `features`. Behaviour and markup are unchanged. |
| `web/src/features/subscription/index.ts` | `export { PublicPlanCards } from './components/PublicPlanCards';` |
| `web/src/shared/api/generated/**` | Regenerated with `npm --prefix web run gen:api`. |
| `web/src/test/msw/server.ts` | Add the default `getRecordFunnelEventMockHandler()` (from `@/shared/api/generated/analytics/analytics.msw`). |
| `web/src/test/setup.ts` | In `afterEach`, add `localStorage.clear();`. |
| `web/src/test/sessions.ts` | Add `needsOnboarding: false` to all three sessions. Add `export const onboardingStudent: Session = { ...testSessions.student, needsOnboarding: true };`. |
| `web/src/test/masteryFixtures.ts` | Add `isInterested: false` to both subjects. |
| `web/src/app/router.test.tsx` | Modify the first test (see Test plan) and add one test. |
| `web/src/features/session/pages/SignUpPage.test.tsx` | Set `studentResult.user.needsOnboarding = true`. Modify the two "lands on the student home" tests and add one test (see Test plan). |
| `web/src/features/session/pages/LoginPage.test.tsx` | The `authResult` helper adds `needsOnboarding: false` to `user`. This is a type-only change: no assertion changes. |
| `web/src/features/session/authSession.test.ts` | The `apiUser` helper adds `needsOnboarding: false`. Add one test. |
| `web/src/features/mastery/pages/StudentHomePage.test.tsx` | Add two tests. |
| `web/src/features/quiz/pages/QuizPage.test.tsx` | Add one test. |
| `docs/PRD.md` | §7.1 step 1: "Landing (live servable counter, value props, plans) → sign up (…) → choose subjects of interest (skippable; editable later from Home)". Add after the list: "Home lists the chosen subjects first under «موادك», the rest under «مواد أخرى»." §10.3: add the row `Sign-up funnel` \| "Distinct visitors per step: landing viewed → sign-up started → account created → onboarding done → first quiz answer, plus median landing-to-first-answer time (`docs/analytics.md`)". §15: extend `User(...)` with `onboarded_at?, subject_interest_ids[]`. Add `FunnelEvent(id, anonymous_id, user_id?, type[LandingViewed\|SignUpStarted\|SignUpCompleted\|OnboardingCompleted\|FirstQuizAnswered], occurred_at)  -- docs/analytics.md`. |
| `docs/mastery.md` | Line ~92: add `isInterested` to `subjects[]`, plus the sentence "Subjects the student chose at onboarding come first (`isInterested: true`), each group in admin order." |
| `docs/claude-design-prompt.md` | §4 **Landing**: replace "goes to `#/student` as سارة" with "goes to sign-up (the prototype jumps to `#/student` as سارة)". Add: top-bar "تسجيل الدخول", the plan cards come from the live catalogue, a signed-in visitor is sent to their home. §4 **Student**: add the bullet "`/onboarding` after sign-up: «اختر موادك», subject checkboxes, «متابعة» (needs one), «تخطّي الآن»; reopened from Home «تعديل موادي» with «العودة إلى الرئيسية»; loading, empty, error-with-retry." Extend the Home bullet: "chosen subjects under «موادك», others under «مواد أخرى», link «تعديل موادي»". |
| `docs/prototype.md` | Walkthrough step 1: append "The product also opens on a landing page and asks a new student to choose subjects of interest after sign-up; the prototype does not simulate them." |

## Files to create
### api
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Analytics/FunnelEventType.cs` | enum | `namespace Elmanhg.Domain.Analytics; public enum FunnelEventType { LandingViewed, SignUpStarted, SignUpCompleted, OnboardingCompleted, FirstQuizAnswered }` (new — adapted from Morabh `Morabh.Domain/Analytics/AppEvent.cs` `AppEventType`) |
| 2 | `api/Elmanhg.Domain/Analytics/FunnelEvent.cs` | entity | `public class FunnelEvent : Entity`. Properties with `{ get; private set; }`: `Guid AnonymousId`, `Guid? UserId`, `FunnelEventType Type`, `DateTimeOffset OccurredAt`. `private FunnelEvent(Guid id) : base(id) { }`. `public static FunnelEvent Record(Guid anonymousId, Guid? userId, FunnelEventType type, DateTimeOffset occurredAt)` returns `new FunnelEvent(Guid.NewGuid()) { ... }`. Reused from Morabh `Morabh.Domain/Analytics/AppEvent.cs` (drops `Platform`, `AppVersion`, `ReceivedAt`). |
| 3 | `api/Elmanhg.Domain/Analytics/IFunnelEventRepository.cs` | repo interface | `public interface IFunnelEventRepository : IRepository<FunnelEvent> { }` (Morabh `IAppEventRepository.cs`) |
| 4 | `api/Elmanhg.Infrastructure/Analytics/FunnelEventRepository.cs` | repo | `public class FunnelEventRepository(AppDbContext context) : Repository<FunnelEvent>(context), IFunnelEventRepository { }` (Morabh `Morabh.Infrastructure/Analytics/AppEventRepository.cs`) |
| 5 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddOnboardingAndFunnelEvents.cs` (+ `.Designer.cs`) | migration | Scaffold with `dotnet ef migrations add AddOnboardingAndFunnelEvents -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Up: AddColumn `AspNetUsers.OnboardedAt` timestamptz null; AddColumn `AspNetUsers.SubjectInterestIds` uuid[] not null default `'{}'`; CreateTable `FunnelEvents` (Id, AnonymousId, UserId?, Type varchar(50), OccurredAt, IsDeleted, DeletedAt, FK to AspNetUsers Restrict, the two indexes). Hand-add at the end of `Up`: `migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"OnboardedAt\" = \"CreationDate\" WHERE \"Role\" = 'Student';");`. Down drops both columns and the table. No other destructive operation. |
| 6 | `api/Elmanhg.Application/Shared/Options/StudentsOptions.cs` | options | `public sealed class StudentsOptions { public const string SectionName = "Students"; [Range(1, 1000)] public int SubjectInterestsMaxCount { get; set; } = 50; }` |
| 7 | `api/Elmanhg.Application/Shared/Options/AnalyticsOptions.cs` | options | `public sealed class AnalyticsOptions { public const string SectionName = "Analytics"; [Range(1, int.MaxValue)] public int FunnelEventPermitLimit { get; set; } = 60; [Range(1, int.MaxValue)] public int FunnelEventWindowSeconds { get; set; } = 60; }` |
| 8 | `api/Elmanhg.Application/Students/GetSubjectInterests/GetSubjectInterestsQuery.cs` | query | `public sealed record GetSubjectInterestsQuery : IRequest<SubjectInterestsResult>;` (new — no Morabh equivalent) |
| 9 | `api/Elmanhg.Application/Students/GetSubjectInterests/GetSubjectInterestsHandler.cs` | handler | `public sealed class GetSubjectInterestsHandler(IUserRepository userRepository, ISubjectRepository subjectRepository, ICurrentUserService currentUserService) : IRequestHandler<GetSubjectInterestsQuery, SubjectInterestsResult>`. Handle: (1) if `currentUserService.UserId` is null or default, throw `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`; (2) `user = await userRepository.GetByIdAsync(userId, ct, asNoTracking: true)`; null → `NotFoundCoreException(ErrorCodes.UserNotFound)`; (3) `subjects = await subjectRepository.GetAllAsync(ct, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true) ?? []`; (4) `return SubjectInterestsResultGenerator.Generate(user, subjects);` |
| 10 | `api/Elmanhg.Application/Students/SaveSubjectInterests/SaveSubjectInterestsCommand.cs` | command | `public sealed record SaveSubjectInterestsCommand(IList<Guid> SubjectIds) : IRequest;` |
| 11 | `api/Elmanhg.Application/Students/SaveSubjectInterests/SaveSubjectInterestsValidator.cs` | validator | `public SaveSubjectInterestsValidator(IOptions<StudentsOptions> studentsOptions)`: `RuleFor(x => x.SubjectIds).ValidateListMaxItems(options.SubjectInterestsMaxCount, ErrorCodes.SubjectInterestsTooMany);` `RuleFor(x => x.SubjectIds).Must(ids => ids == null \|\| ids.Distinct().Count() == ids.Count).WithErrorCode(ErrorCodes.SubjectInterestsDuplicate);` `RuleForEach(x => x.SubjectIds).ValidateRequired(ErrorCodes.SubjectIdRequired);` An empty list is valid. |
| 12 | `api/Elmanhg.Application/Students/SaveSubjectInterests/SaveSubjectInterestsHandler.cs` | handler | `public sealed class SaveSubjectInterestsHandler(IUserRepository userRepository, ISubjectRepository subjectRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<SaveSubjectInterestsCommand>`. Handle: (1) auth guard as in #9; (2) `var subjectIds = request.SubjectIds.ToList();` then, if `subjectIds.Count > 0` and `await subjectRepository.CountAsync(ct, x => subjectIds.Contains(x.Id)) != subjectIds.Count`, throw `NotFoundCoreException(ErrorCodes.SubjectNotFound)`; (3) `user = await userRepository.GetByIdAsync(userId, ct)` (tracked); null → `NotFoundCoreException(ErrorCodes.UserNotFound)`; (4) `user.ChooseSubjectInterests(subjectIds, timeProvider.GetUtcNow());`; (5) `await userRepository.SaveChangesAsync(ct)`. |
| 13 | `api/Elmanhg.Application/Students/Shared/SubjectInterestsResult.cs` | result (client) | `public sealed record SubjectInterestsResult(bool NeedsOnboarding, List<SubjectInterestResult> Subjects);` |
| 14 | `api/Elmanhg.Application/Students/Shared/SubjectInterestResult.cs` | result (client) | `public sealed record SubjectInterestResult(Guid SubjectId, string Name, bool IsSelected);` `Name` is a plain string (Subject.Name is not `LocalizedText`). |
| 15 | `api/Elmanhg.Application/Students/Shared/SubjectInterestsResultGenerator.cs` | generator | `public static SubjectInterestsResult Generate(User user, IReadOnlyList<Subject> subjects)` returns `new(user.NeedsOnboarding, subjects.Select(subject => new SubjectInterestResult(subject.Id, subject.Name, user.SubjectInterestIds.Contains(subject.Id))).ToList())`. |
| 16 | `api/Elmanhg.Application/Analytics/RecordFunnelEvent/RecordFunnelEventCommand.cs` | command | `public sealed record RecordFunnelEventCommand(Guid AnonymousId, FunnelEventType Type) : IRequest;` (Morabh `RecordAppEventsCommand.cs`, single event) |
| 17 | `api/Elmanhg.Application/Analytics/RecordFunnelEvent/RecordFunnelEventValidator.cs` | validator | `RuleFor(x => x.AnonymousId).ValidateRequired(ErrorCodes.FunnelAnonymousIdRequired);` `RuleFor(x => x.Type).IsInEnum().WithErrorCode(ErrorCodes.FunnelEventTypeInvalid);` (Morabh `RecordAppEventsValidator.cs`) |
| 18 | `api/Elmanhg.Application/Analytics/RecordFunnelEvent/RecordFunnelEventHandler.cs` | handler | `public sealed class RecordFunnelEventHandler(IFunnelEventRepository funnelEventRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<RecordFunnelEventCommand>`. Handle: (1) `var userId = currentUserService.UserId == null \|\| currentUserService.UserId == default ? null : currentUserService.UserId;` (anonymous is allowed, so no throw); (2) `await funnelEventRepository.AddAsync(FunnelEvent.Record(request.AnonymousId, userId, request.Type, timeProvider.GetUtcNow()), ct)`; (3) `await funnelEventRepository.SaveChangesAsync(ct)`. (Morabh `RecordAppEventsHandler.cs`) |
| 19 | `api/Elmanhg.Api/RateLimiting/AnalyticsRateLimitPolicies.cs` | constants | `public static class AnalyticsRateLimitPolicies { public const string FunnelEvents = "analytics-funnel-events"; }` |
| 20 | `api/Elmanhg.Api/Controllers/Students/StudentsController.cs` | controller | `[ApiController][Route("api/students")][Authorize] public class StudentsController(IMediator mediator) : ControllerBase`. `[HttpGet("me/subject-interests", Name = "GetSubjectInterests")][Authorize(Policy = DefaultCodes.ProgressViewOwn)][ProducesResponseType<SubjectInterestsResult>(200)] GetSubjectInterests(CancellationToken)` sends `new GetSubjectInterestsQuery()` and returns `Ok(result)`. `[HttpPut("me/subject-interests", Name = "SaveSubjectInterests")][Authorize(Policy = DefaultCodes.ProgressViewOwn)][ProducesResponseType(200)] SaveSubjectInterests([FromBody] SubjectInterestsRequest request, CancellationToken)` sends `new SaveSubjectInterestsCommand(request.SubjectIds ?? [])` and returns `Ok()`. |
| 21 | `api/Elmanhg.Api/Controllers/Students/Requests.cs` | request | `public sealed record SubjectInterestsRequest(List<Guid>? SubjectIds);` |
| 22 | `api/Elmanhg.Api/Controllers/Analytics/AnalyticsController.cs` | controller | `[ApiController][Route("api/analytics")][Authorize] public class AnalyticsController(IMediator mediator)`. `[HttpPost("funnel-events", Name = "RecordFunnelEvent")][AllowAnonymous][EnableRateLimiting(AnalyticsRateLimitPolicies.FunnelEvents)][ProducesResponseType(200)] RecordFunnelEvent([FromBody] RecordFunnelEventRequest request, CancellationToken)` sends `new RecordFunnelEventCommand(request.AnonymousId, request.Type)` and returns `Ok()`. (Morabh `Morabh.APIs/Controllers/Analytics/AnalyticsController.cs`) |
| 23 | `api/Elmanhg.Api/Controllers/Analytics/Requests.cs` | request | `public sealed record RecordFunnelEventRequest(Guid AnonymousId, FunnelEventType Type);` |
| 24 | `docs/analytics.md` | doc | Sections: purpose (PRD §2.1 goal 1); the event table (type, emitted where, once-only?); anonymous id (random UUID in `localStorage`, no PII, no third party); endpoint, body, rate limit and the `Analytics:*` config; storage (`FunnelEvents`, the server-stamped `OccurredAt`, `UserId` when signed in); funnel definition for #104: distinct `AnonymousId` per `Type` in the date range, conversion = step / previous step, median seconds from the first `LandingViewed` to the first `FirstQuizAnswered` per `AnonymousId`. |

### api tests
| # | Path |
|---|------|
| 25 | `api/Elmanhg.Tests/Domain/Analytics/FunnelEventTests.cs` |
| 26 | `api/Elmanhg.Tests/Application/Features/Students/GetSubjectInterests/GetSubjectInterestsHandlerTests.cs` |
| 27 | `api/Elmanhg.Tests/Application/Features/Students/SaveSubjectInterests/SaveSubjectInterestsHandlerTests.cs` |
| 28 | `api/Elmanhg.Tests/Application/Features/Students/SaveSubjectInterests/SaveSubjectInterestsValidatorTests.cs` |
| 29 | `api/Elmanhg.Tests/Application/Features/Analytics/RecordFunnelEvent/RecordFunnelEventHandlerTests.cs` |
| 30 | `api/Elmanhg.Tests/Application/Features/Analytics/RecordFunnelEvent/RecordFunnelEventValidatorTests.cs` |
| 31 | `api/Elmanhg.Tests/Application/Features/Auth/Shared/AuthResultGeneratorTests.cs` |
| 32 | `api/Elmanhg.Tests/Integration/Students/SubjectInterestsEndpointTests.cs` |
| 33 | `api/Elmanhg.Tests/Integration/Analytics/FunnelEventsEndpointTests.cs` |

### web
| # | Path | Type | Contract |
|---|------|------|----------|
| 34 | `web/src/features/analytics/api/funnelTracker.ts` | module | `export const anonymousIdKey = 'elmanhg.anonymousId';` `export function readAnonymousId(): string` (reads `localStorage[anonymousIdKey]`; if missing, stores and returns `crypto.randomUUID()`). `export interface TrackOptions { once?: boolean }`. `export function trackFunnelEvent(type: FunnelEventType, options: TrackOptions = {}): void`: when `once` is set and `localStorage['elmanhg.funnel.' + type]` exists, return; when `once` is set, write that key as `'1'`; then `void recordFunnelEvent({ anonymousId: readAnonymousId(), type }).catch(() => undefined);`, with the single why-comment `// analytics must never block or break the journey`. `FunnelEventType` and `recordFunnelEvent` come from the generated client. |
| 35 | `web/src/features/analytics/hooks/useFunnelEventOnMount.ts` | hook | `export function useFunnelEventOnMount(type: FunnelEventType): void { useEffect(() => { trackFunnelEvent(type); }, [type]); }` |
| 36 | `web/src/features/analytics/index.ts` | barrel | exports `trackFunnelEvent`, `useFunnelEventOnMount` |
| 37 | `web/src/features/analytics/api/funnelTracker.test.ts` | test | see Test plan |
| 38 | `web/src/features/landing/api/marketing.ts` | const | `// the PRD §2.1 marketing promise the live counter is measured against` then `export const marketedQuestionGoal = 100_000;` |
| 39 | `web/src/features/landing/components/LandingTopBar.tsx` | component | `<header className="mx-auto flex max-w-layout items-center gap-3 px-4 py-3 lg:px-6">`: app name (`common:app.name`, `font-display text-h3 font-bold`), then `ms-auto` `<Button asChild variant="secondary" size="sm"><Link to="/login">{t('topBar.signIn')}</Link></Button>`. |
| 40 | `web/src/features/landing/components/LandingHero.tsx` | component | Props `{ servableCount: number \| undefined }`. `<section aria-labelledby={id} className="flex flex-col items-start gap-3 rounded-lg bg-aurora p-5 text-surface shadow-1 lg:p-8">`: `<p className="text-caption font-semibold">{t('hero.eyebrow')}</p>`; `<h1 id className="font-display text-display font-bold lg:text-display-desktop">` with `hero.titleWithCount` `{count}` when defined, else `hero.title`; `<p className="text-ui text-surface/80">{t('hero.goal', { goal: marketedQuestionGoal })}</p>`; `<Button asChild variant="primary"><Link to="/signup">{t('hero.start')}</Link></Button>`. This is the only other `bg-aurora` user besides `SubscribeHeader`. |
| 41 | `web/src/features/landing/components/ValueProps.tsx` | component | Props `{ replySlaHours: number \| undefined }`. H2 `values.title`, then `<ul className="grid grid-cols-1 gap-3 md:grid-cols-3">` of three white cards (`rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5`), each with a Lucide icon (`InfinityIcon`, `ClipboardCheck`, `MessageCircleQuestionMark`; `aria-hidden`, `strokeWidth={1.8}`, `size-6 text-accent`), an `h3` title (`font-display text-h3 font-bold`) and a caption body (`text-caption text-text-muted`). The third body is `values.askTeacher.bodyWithHours` `{hours}` when `replySlaHours` is defined, else `values.askTeacher.body`. The key list is a static `const` array `['practice', 'exams', 'askTeacher']`, used as the `key`. |
| 42 | `web/src/features/landing/pages/LandingPage.tsx` | page | `useFunnelEventOnMount('LandingViewed')`; `const count = useGetServableQuestionCount(); const catalogue = useGetPlanCatalogue();`. Renders `<LandingTopBar />`, then `<main id="main" className="mx-auto flex max-w-layout flex-col gap-6 px-4 pb-10 lg:px-6">` containing `<LandingHero servableCount={count.data ? Number(count.data.count) : undefined} />`, `<ValueProps replySlaHours={catalogue.data ? Number(catalogue.data.askTeacher.replySlaHours) : undefined} />`, and `<section aria-labelledby>` with H2 `plans.title` holding: `ContentErrorState` (title `plans.errorTitle`, retry → `catalogue.refetch()`) when `catalogue.isError`; `ContentListSkeleton` (label `plans.loading`) when pending; otherwise `<PublicPlanCards catalogue={catalogue.data} />`. |
| 43 | `web/src/features/landing/i18n/ar.json`, `en.json` | locales | Keys and values in the i18n table below. |
| 44 | `web/src/features/landing/locales.ts` | locales | `export const landingLocales = { ar, en };` |
| 45 | `web/src/features/landing/index.ts` | barrel | `export { LandingPage } from './pages/LandingPage';` |
| 46 | `web/src/features/landing/pages/LandingPage.test.tsx` | test | see Test plan |
| 47 | `web/src/features/onboarding/schemas/subjectInterestsSchema.ts` | schema | `export const subjectInterestsSchema = z.object({ subjectIds: z.array(z.string()).min(1, { error: 'onboarding:form.subjectRequired' }) }); export type SubjectInterestsValues = z.infer<typeof subjectInterestsSchema>;` |
| 48 | `web/src/features/onboarding/schemas/subjectInterestsSchema.test.ts` | test | see Test plan |
| 49 | `web/src/features/onboarding/hooks/useSubjectInterestsSave.ts` | hook | `export function useSubjectInterestsSave(needsOnboarding: boolean): { save: (subjectIds: string[]) => Promise<void>; skip: () => void; isPending: boolean }`. It wraps the generated `useSaveSubjectInterests` with `onSuccess`: (1) if `needsOnboarding`, `trackFunnelEvent('OnboardingCompleted')`; (2) `const session = store.get(); if (session) store.set({ ...session, needsOnboarding: false });`; (3) `await queryClient.invalidateQueries({ queryKey: getGetSubjectInterestsQueryKey() }); void invalidateMastery(queryClient);`; (4) `toast.success(t('toast.saved'))`; (5) `await navigate({ to: '/student' })`. `save` = `mutateAsync({ data: { subjectIds } })`, which rejects so the Form maps root errors. `skip` = `save([]).catch((error: unknown) => toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION'])))`, with `code` from `ApiError` or `unhandledErrorCode`. |
| 50 | `web/src/features/onboarding/components/SubjectInterestsForm.tsx` | component | Props `{ interests: SubjectInterestsResult }`. `useForm<SubjectInterestsValues>({ resolver: zodResolver(subjectInterestsSchema), defaultValues: { subjectIds: interests.subjects.filter(x => x.isSelected).map(x => x.subjectId) } })`. `<Form form onSubmit={(values) => save(values.subjectIds)}>`: `<FormRootError />`; a `<fieldset aria-describedby={errorId when invalid}>` with `<legend className="text-caption text-text-muted">{t('form.legend')}</legend>` and `<div className="grid grid-cols-1 gap-2 md:grid-cols-2">` holding one `<label className="flex min-h-12 items-center gap-3 rounded-md border border-border-strong bg-surface px-3.5 py-3 text-ui has-checked:border-text has-checked:bg-soft">` per subject, around `<input type="checkbox" value={subjectId} {...register('subjectIds')} className="size-5 accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden" />` and the name; the field error `<p id={errorId} role="alert" className="text-caption text-danger">`; then an actions row `flex flex-wrap gap-2` with `<SubmitButton>{t('actions.continue')}</SubmitButton>` and either (`interests.needsOnboarding`) `<Button variant="secondary" disabled={isPending} onClick={skip}>{t('actions.skip')}</Button>` or `<Button asChild variant="secondary"><Link to="/student">{t('actions.backHome')}</Link></Button>`. |
| 51 | `web/src/features/onboarding/pages/OnboardingPage.tsx` | page | `const { data, error, isPending, isError, refetch } = useGetSubjectInterests();`. Layout: `<main id="main" className="mx-auto flex min-h-dvh max-w-layout flex-col gap-4 px-4 py-6 lg:px-6">`, H1 `page.title` (`font-display text-h1 font-bold lg:text-h1-desktop`), intro `<p className="text-ui text-text-muted">`. States: error → `ContentErrorState` (title `page.errorTitle`, retry → `refetch()`); pending → `ContentListSkeleton` (label `page.loading`); `data.subjects.length === 0` → `<p className="text-ui text-text-muted">{t('page.empty')}</p>` plus a primary `<Button onClick={skip}>{t('actions.continueHome')}</Button>` (from `useSubjectInterestsSave(data.needsOnboarding)`); otherwise `<SubjectInterestsForm interests={data} />`. |
| 52 | `web/src/features/onboarding/i18n/ar.json`, `en.json` | locales | see i18n table |
| 53 | `web/src/features/onboarding/locales.ts` | locales | `export const onboardingLocales = { ar, en };` |
| 54 | `web/src/features/onboarding/index.ts` | barrel | `export { OnboardingPage } from './pages/OnboardingPage';` |
| 55 | `web/src/features/onboarding/pages/OnboardingPage.test.tsx` | test | see Test plan |
| 56 | `web/src/routes/onboarding.tsx` | route | `createFileRoute('/onboarding')({ beforeLoad: ({ context, location }) => { requireRole(context.sessionStore.get(), 'student', location.href); }, component: OnboardingPage })` |
| 57 | `web/src/features/mastery/components/HomeSubjects.tsx` | component | Props `{ subjects: SubjectMasteryResult[] }`. `const chosen = subjects.filter(x => x.isInterested); const others = subjects.filter(x => !x.isInterested); const primary = chosen.length > 0 ? chosen : subjects;`. Renders a header row (`flex items-center justify-between gap-2`) with H2 `subjects.title` and `<Link to="/onboarding" className="text-ui font-semibold text-accent focus-visible:ring-2 ...">{t('subjects.edit')}</Link>`. If `subjects.length === 0`: the existing `subjects.empty` paragraph. Else: the existing card grid for `primary`, then, when `chosen.length > 0 && others.length > 0`, H2 `subjects.others` and a second grid for `others`. The grid markup is the same as the one it replaces in `StudentHomePage`. |
| 58 | `web/src/features/subscription/hooks/usePlanCardContent.ts` | hook | `export interface PlanCardContent { title: string; priceLines: string[]; features: string[] }`. `export function usePlanCardContent(catalogue: PlanCatalogueResult): Record<'free' \| 'base' \| 'askTeacher', PlanCardContent>`. Moves the `priceLines` / `features` / `title` expressions out of `PlanCardGrid` verbatim. |
| 59 | `web/src/features/subscription/components/PublicPlanCards.tsx` | component | Props `{ catalogue: PlanCatalogueResult }`. The same grid wrapper as `PlanCardGrid` (`grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3`), with three `<PlanCard {...content.x} isActive={false} />` and no actions. |
| 60 | `web/src/test/onboardingFixtures.ts` | fixture | `export function subjectInterests(overrides?: Partial<SubjectInterestsResult>): SubjectInterestsResult` → `{ needsOnboarding: true, subjects: [{ subjectId: physicsId, name: 'Physics', isSelected: false }, { subjectId: chemistryId, name: 'Chemistry', isSelected: false }], ...overrides }` (ids from `masteryFixtures`). |

### i18n values
| Key | ar | en |
|---|---|---|
| landing `topBar.signIn` | تسجيل الدخول | Sign in |
| `hero.eyebrow` | للثانوية العامة | For Thanaweya Amma |
| `hero.titleWithCount` | {count, number} سؤال راجعها معلّمون حقيقيون | {count, number} questions checked by real teachers |
| `hero.title` | أسئلة راجعها معلّمون حقيقيون | Questions checked by real teachers |
| `hero.goal` | هدفنا {goal, number} سؤال، والعدّاد حقيقي يزيد مع كل سؤال يعتمده معلّم. | Our goal is {goal, number} questions. The counter is live and grows with every question a teacher approves. |
| `hero.start` | ابدأ مجانًا | Start for free |
| `values.title` | لماذا المنهج؟ | Why Elmanhg? |
| `values.practice.title` / `.body` | تدريب لا نهائي / أسئلة تكيّفية لكل درس تبدأ بما لم تره وبما أخطأت فيه. | Unlimited practice / Adaptive questions for every lesson, starting with what you have not seen and what you got wrong. |
| `values.exams.title` / `.body` | امتحانات الوحدات / امتحانات بنموذج ثابت لوحدة أو لعدة وحدات، مع نتيجة مفصّلة. | Unit exams / Blueprint exams for one unit or several, with a detailed result. |
| `values.askTeacher.title` | اسأل معلّم | Ask a teacher |
| `values.askTeacher.bodyWithHours` | معلّم مختص يرد على سؤالك خلال {hours, number} ساعة. | A subject teacher answers your question within {hours, plural, one {# hour} other {# hours}}. |
| `values.askTeacher.body` | معلّم مختص يرد على سؤالك كتابةً أو صوتًا. | A subject teacher answers your question in text or voice. |
| `plans.title` / `.loading` / `.errorTitle` | الخطط / جارٍ تحميل الخطط… / تعذّر تحميل الخطط. | Plans / Loading plans… / Could not load the plans. |
| onboarding `page.title` | اختر موادك | Choose your subjects |
| `page.intro` | اختر المواد التي تذاكرها لنعرضها أولًا في الرئيسية. يمكنك تغييرها لاحقًا. | Pick the subjects you study and we will show them first on your home. You can change them later. |
| `page.loading` / `.errorTitle` / `.empty` | جارٍ تحميل المواد… / تعذّر تحميل المواد. / لا توجد مواد بعد. | Loading subjects… / Could not load the subjects. / No subjects yet. |
| `form.legend` / `.subjectRequired` | المواد / اختر مادة واحدة على الأقل. | Subjects / Choose at least one subject. |
| `actions.continue` / `.skip` / `.backHome` / `.continueHome` | متابعة / تخطّي الآن / العودة إلى الرئيسية / متابعة إلى الرئيسية | Continue / Skip for now / Back to home / Continue to home |
| `toast.saved` | تم حفظ موادك. | Your subjects are saved. |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ErrorCodes.SubjectInterestsTooMany` | `SUBJECT_INTERESTS_TOO_MANY` | `SaveSubjectInterestsValidator` | validation pipeline | 422 |
| `ErrorCodes.SubjectInterestsDuplicate` | `SUBJECT_INTERESTS_DUPLICATE` | `SaveSubjectInterestsValidator` | validation pipeline | 422 |
| `ErrorCodes.SubjectIdRequired` (existing) | `SUBJECT_ID_REQUIRED` | `SaveSubjectInterestsValidator` (each item) | validation pipeline | 422 |
| `ErrorCodes.SubjectNotFound` (existing) | `SUBJECT_NOT_FOUND` | `SaveSubjectInterestsHandler` | `NotFoundCoreException` | 404 |
| `ErrorCodes.UserNotFound` (existing) | `USER_NOT_FOUND` | both Students handlers | `NotFoundCoreException` | 404 |
| `ErrorCodes.FunnelAnonymousIdRequired` | `FUNNEL_ANONYMOUS_ID_REQUIRED` | `RecordFunnelEventValidator` | validation pipeline | 422 |
| `ErrorCodes.FunnelEventTypeInvalid` | `FUNNEL_EVENT_TYPE_INVALID` | `RecordFunnelEventValidator` | validation pipeline | 422 |
| Domain `ErrorCodes.UserNotStudent` | `USER_NOT_STUDENT` | `User.ChooseSubjectInterests` | `BusinessRuleViolationCoreException` | 400 |
| existing `TOO_MANY_REQUESTS` | — | rate limiter | `RateLimitExceededCoreException` | 429 |

Resource strings (Arabic without tashkeel or hamza-on-alef, matching the file):
| Key | en | ar |
|---|---|---|
| SUBJECT_INTERESTS_TOO_MANY | Too many subjects were chosen. | تم اختيار عدد كبير جدا من المواد. |
| SUBJECT_INTERESTS_DUPLICATE | A subject was chosen more than once. | تم اختيار مادة اكثر من مرة. |
| FUNNEL_ANONYMOUS_ID_REQUIRED | The visitor id is required. | معرف الزائر مطلوب. |
| FUNNEL_EVENT_TYPE_INVALID | The event type is not valid. | نوع الحدث غير صالح. |
| USER_NOT_STUDENT | This action is for students only. | هذا الاجراء للطلاب فقط. |

## Domain behaviour
`User` (added members, placed after `IsActive`):
```csharp
public DateTimeOffset? OnboardedAt { get; private set; }
public List<Guid> SubjectInterestIds { get; private set; } = [];
public bool NeedsOnboarding => Role == UserRole.Student && OnboardedAt is null;

public void ChooseSubjectInterests(IReadOnlyCollection<Guid> subjectIds, DateTimeOffset chosenAt)
{
    if (Role != UserRole.Student)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.UserNotStudent);
    }

    SubjectInterestIds = [.. subjectIds.Distinct()];
    OnboardedAt ??= chosenAt;
    UpdationDate = chosenAt;
}
```
- The first call sets `OnboardedAt`. Later calls keep it and only replace the interests.
- `FunnelEvent.Record` is a pure factory: no guards, and it never mutates after creation.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| GET | `/api/students/me/subject-interests` | `DefaultCodes.ProgressViewOwn` | — | 200 `SubjectInterestsResult { needsOnboarding, subjects[] { subjectId, name, isSelected } }` |
| PUT | `/api/students/me/subject-interests` | `DefaultCodes.ProgressViewOwn` | `SubjectInterestsRequest { subjectIds: Guid[]? }` | 200 empty · 404 `SUBJECT_NOT_FOUND` · 422 |
| POST | `/api/analytics/funnel-events` | `[AllowAnonymous]` + rate limit `analytics-funnel-events` | `RecordFunnelEventRequest { anonymousId: Guid, type: FunnelEventType }` | 200 empty · 422 · 429 `TOO_MANY_REQUESTS` |
| changed | every `/api/auth/*` result | — | — | `user.needsOnboarding: bool` added |
| changed | GET `/api/mastery/overview` | — | — | `subjects[].isInterested: bool` added; interested subjects first |

## Test plan
### api (xUnit v3, FluentAssertions, NSubstitute; `Method_Scenario_Expected`)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `UserTests` | `NeedsOnboarding_NewStudent_IsTrue` | `User.CreateStudentWithEmail(...)` has `NeedsOnboarding` true and `OnboardedAt` null |
| 2 | `UserTests` | `NeedsOnboarding_Teacher_IsFalse` | `CreateTeacher` has `NeedsOnboarding` false |
| 3 | `UserTests` | `ChooseSubjectInterests_Student_StoresDistinctIdsAndMarksOnboarded` | ids `[a, a, b]` give `SubjectInterestIds == [a, b]`; `OnboardedAt == chosenAt`; `UpdationDate == chosenAt`; `NeedsOnboarding` false |
| 4 | `UserTests` | `ChooseSubjectInterests_SecondCall_KeepsFirstOnboardedAtAndReplacesIds` | first `t1 [a]`, then `t2 [b]`: `OnboardedAt == t1`, ids `== [b]`, `UpdationDate == t2` |
| 5 | `UserTests` | `ChooseSubjectInterests_EmptyList_MarksOnboardedWithoutInterests` | ids empty, `OnboardedAt` set |
| 6 | `UserTests` | `ChooseSubjectInterests_Teacher_ThrowsUserNotStudent` | `BusinessRuleViolationCoreException` with `ErrorCode == USER_NOT_STUDENT`; ids unchanged |
| 7 | `FunnelEventTests` | `Record_AnyInput_SetsEveryField` | `AnonymousId`, `UserId`, `Type`, `OccurredAt` equal the inputs; `Id` not empty |
| 8 | `GetSubjectInterestsHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException` / `USER_NOT_AUTHENTICATED` |
| 9 | `GetSubjectInterestsHandlerTests` | `Handle_UserMissing_ThrowsUserNotFound` | `NotFoundCoreException` / `USER_NOT_FOUND` |
| 10 | `GetSubjectInterestsHandlerTests` | `Handle_StudentWithInterests_ReturnsEverySubjectWithSelection` | student chose `[chemistry]`: result `NeedsOnboarding` false, `Subjects` equal `[(physics, "Physics", false), (chemistry, "Chemistry", true)]` in the repository order |
| 11 | `GetSubjectInterestsHandlerTests` | `Handle_NewStudent_ReturnsNeedsOnboarding` | `NeedsOnboarding` true, all `IsSelected` false |
| 12 | `SaveSubjectInterestsHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | exception + code; `SaveChangesAsync` `DidNotReceive()` |
| 13 | `SaveSubjectInterestsHandlerTests` | `Handle_UnknownSubject_ThrowsSubjectNotFound` | `CountAsync` returns 1 for 2 ids: `NotFoundCoreException` / `SUBJECT_NOT_FOUND`; `DidNotReceive()` save; user ids unchanged |
| 14 | `SaveSubjectInterestsHandlerTests` | `Handle_UserMissing_ThrowsUserNotFound` | `NotFoundCoreException` / `USER_NOT_FOUND`; `DidNotReceive()` save |
| 15 | `SaveSubjectInterestsHandlerTests` | `Handle_KnownSubjects_SavesInterestsAndOnboards` | user ids `== request ids`; `OnboardedAt == timeProvider now`; `SaveChangesAsync` `Received(1)` |
| 16 | `SaveSubjectInterestsHandlerTests` | `Handle_EmptyList_OnboardsWithoutCountingSubjects` | `OnboardedAt` set, ids empty; `subjectRepository.CountAsync` `DidNotReceive()` (this interaction is the behaviour); save `Received(1)` |
| 17 | `SaveSubjectInterestsValidatorTests` | `Validate_DistinctIds_Passes` | valid |
| 18 | `SaveSubjectInterestsValidatorTests` | `Validate_EmptyList_Passes` | valid |
| 19 | `SaveSubjectInterestsValidatorTests` | `Validate_OverMaxCount_FailsTooMany` | max 2, 3 ids: `SUBJECT_INTERESTS_TOO_MANY` |
| 20 | `SaveSubjectInterestsValidatorTests` | `Validate_DuplicateIds_FailsDuplicate` | `SUBJECT_INTERESTS_DUPLICATE` |
| 21 | `SaveSubjectInterestsValidatorTests` | `Validate_EmptyGuid_FailsSubjectIdRequired` | `SUBJECT_ID_REQUIRED` |
| 22 | `RecordFunnelEventHandlerTests` | `Handle_Anonymous_AddsEventWithoutUser` | `AddAsync` received a `FunnelEvent` with the request id and type, `UserId` null, `OccurredAt == now`; save `Received(1)` |
| 23 | `RecordFunnelEventHandlerTests` | `Handle_SignedIn_AddsEventWithUserId` | `UserId == currentUser` |
| 24 | `RecordFunnelEventValidatorTests` | `Validate_ValidEvent_Passes` | valid |
| 25 | `RecordFunnelEventValidatorTests` | `Validate_EmptyAnonymousId_FailsRequired` | `FUNNEL_ANONYMOUS_ID_REQUIRED` |
| 26 | `RecordFunnelEventValidatorTests` | `Validate_UnknownType_FailsInvalid` | `(FunnelEventType)99`: `FUNNEL_EVENT_TYPE_INVALID` |
| 27 | `AuthResultGeneratorTests` | `Generate_NewStudent_ReturnsNeedsOnboardingTrue` | `result.User.NeedsOnboarding` true |
| 28 | `AuthResultGeneratorTests` | `Generate_OnboardedStudent_ReturnsNeedsOnboardingFalse` | after `ChooseSubjectInterests`: false |
| 29 | `AuthResultGeneratorTests` | `Generate_Admin_ReturnsNeedsOnboardingFalse` | false |
| 30 | `GetMasteryOverviewHandlerTests` (modify) | `Handle_Subjects_ReturnsEverySubjectInOrderWithWeightedMastery` | the existing expectation with a trailing `false` on both records |
| 31 | `GetMasteryOverviewHandlerTests` (add) | `Handle_SubjectInterests_ListsInterestedSubjectsFirst` | student chose `[_subjectB]`: `Subjects` order is `[B (IsInterested true), A (false)]` |
| 32 | `SubjectInterestsEndpointTests` | `Get_Anonymous_Returns401` | 401 |
| 33 | `SubjectInterestsEndpointTests` | `Get_Teacher_Returns403` | 403 |
| 34 | `SubjectInterestsEndpointTests` | `Get_NewStudent_ReturnsNeedsOnboardingAndSeededSubjectUnselected` | 200; `needsOnboarding` true; the seeded subject is present with `isSelected` false |
| 35 | `SubjectInterestsEndpointTests` | `Put_ChosenSubject_PersistsAndClearsNeedsOnboarding` | PUT `[s1]` gives 200; GET shows s1 selected and s2 not, `needsOnboarding` false; a fresh-scope DB read shows `OnboardedAt` not null and `SubjectInterestIds == [s1]` |
| 36 | `SubjectInterestsEndpointTests` | `Put_EmptyList_CompletesOnboarding` | 200; DB `OnboardedAt` not null, ids empty |
| 37 | `SubjectInterestsEndpointTests` | `Put_UnknownSubject_Returns404` | 404 + `code == SUBJECT_NOT_FOUND`; DB `OnboardedAt` still null |
| 38 | `SubjectInterestsEndpointTests` | `Put_DuplicateIds_Returns422` | 422 + code contains `SUBJECT_INTERESTS_DUPLICATE` |
| 39 | `SubjectInterestsEndpointTests` | `Put_Admin_Returns403` | 403 |
| 40 | `SubjectInterestsEndpointTests` | `Put_Anonymous_Returns401` | 401 |
| 41 | `FunnelEventsEndpointTests` | `Post_Anonymous_RecordsEventWithoutUser` | 200; DB row for the random `anonymousId` with `Type == LandingViewed`, `UserId` null |
| 42 | `FunnelEventsEndpointTests` | `Post_SignedInStudent_RecordsEventWithUserId` | 200; row `UserId == student.Id` |
| 43 | `FunnelEventsEndpointTests` | `Post_EmptyAnonymousId_Returns422` | 422 + `FUNNEL_ANONYMOUS_ID_REQUIRED`; no row |
| 44 | `FunnelEventsEndpointTests` | `Post_UnknownNumericType_Returns422` | body `type: 99` gives 422 + `FUNNEL_EVENT_TYPE_INVALID` |
| 45 | `FunnelEventsEndpointTests` | `Post_OverIpLimit_Returns429TooManyRequests` | `factory.WithWebHostBuilder` with `Analytics:FunnelEventPermitLimit=1` (pattern from `AuthRateLimitTests`): the second call is 429 with `TOO_MANY_REQUESTS` |
| 46 | `PhoneAuthEndpointTests` (add) | `RegisterWithPhone_NewStudent_ReturnsNeedsOnboarding` | body `user.needsOnboarding` true |
| 47 | `MasteryOverviewEndpointTests` (add) | `Get_AfterChoosingSubject_ListsItFirstAsInterested` | seed two subjects, PUT interests `[second]`: `subjects[0].subjectId == second` and `isInterested` true |
| 48 | `AppDbContextTests` (modify) | `Migrate_FreshDatabase_LeavesNoPendingMigrations` | list extended with `_AddOnboardingAndFunnelEvents` |

### web (Vitest, Testing Library, MSW; render through `renderApp`)
| # | File | `it(...)` | Asserts |
|---|---|---|---|
| 49 | `funnelTracker.test.ts` | `posts the event with the same anonymous id every time` | two `trackFunnelEvent` calls produce two captured bodies with the given types and identical, non-empty `anonymousId` |
| 50 | `funnelTracker.test.ts` | `sends a once-only event a single time` | two `{ once: true }` calls capture one body |
| 51 | `funnelTracker.test.ts` | `swallows a failed request` | a 500 handler; `await waitFor` the call count to reach 1; the test ends with no unhandled rejection |
| 52 | `LandingPage.test.tsx` | `shows the live question count in the hero` | `getServableQuestionCount` mock `{ count: 1234 }` gives the heading `1,234 questions checked by real teachers` and the goal text with `100,000` |
| 53 | `LandingPage.test.tsx` | `shows the headline without a number while the count is unavailable` | a 500 on servable-count gives the heading `Questions checked by real teachers` |
| 54 | `LandingPage.test.tsx` | `shows the plans after loading` | loading `status` "Loading plans…", then headings `Free`, `Base`, `Ask a Teacher` (subscription `plan.*` copy) and a Base monthly price line |
| 55 | `LandingPage.test.tsx` | `shows an error and retries the plans` | a 500 gives an alert with "Could not load the plans."; after resetting handlers, click `Retry` and the plan headings appear |
| 56 | `LandingPage.test.tsx` | `starts sign-up from the call to action` | click link `Start for free`; heading `Create account` |
| 57 | `LandingPage.test.tsx` | `offers sign-in from the top bar` | link `Sign in` has `href="/login"` |
| 58 | `LandingPage.test.tsx` | `records a landing view` | the captured funnel body has `type: 'LandingViewed'` |
| 59 | `LandingPage.test.tsx` | `renders right-to-left in Arabic` | `lng: 'ar'`: `document.documentElement` has `dir="rtl"` and the link `ابدأ مجانًا` is present |
| 60 | `LandingPage.test.tsx` | `has no axe violations` | `axe(container).violations` equals `[]` |
| 61 | `subjectInterestsSchema.test.ts` | `accepts one chosen subject` / `rejects no subject` | success; error message `onboarding:form.subjectRequired` |
| 62 | `OnboardingPage.test.tsx` | `shows a loading state then the subjects with the saved choice ticked` | `status` "Loading subjects…"; checkbox `Chemistry` checked when fixture `isSelected` |
| 63 | `OnboardingPage.test.tsx` | `requires one subject before continuing` | click `Continue` with none gives the alert "Choose at least one subject." and the `Physics` checkbox has focus; no PUT |
| 64 | `OnboardingPage.test.tsx` | `saves the chosen subjects and lands on the home` | PUT body `{ subjectIds: [physicsId] }`; heading `Hello, أحمد` (mastery mock `masteryOverview()`) |
| 65 | `OnboardingPage.test.tsx` | `skips with no subjects and lands on the home` | click `Skip for now`: PUT body `{ subjectIds: [] }`; home heading |
| 66 | `OnboardingPage.test.tsx` | `records onboarding completed on first completion only` | `onboardingStudent` session plus `needsOnboarding: true` data gives a funnel body `OnboardingCompleted`; a second render with `needsOnboarding: false` data sends none |
| 67 | `OnboardingPage.test.tsx` | `shows the server error when saving fails` | a PUT 404 `SUBJECT_NOT_FOUND` gives the alert "Subject not found." |
| 68 | `OnboardingPage.test.tsx` | `offers the way home instead of skipping when editing` | `needsOnboarding: false`: link `Back to home`, no `Skip for now` button |
| 69 | `OnboardingPage.test.tsx` | `continues home when there are no subjects` | `subjects: []`: text "No subjects yet."; click `Continue to home` gives PUT `[]` and the home heading |
| 70 | `OnboardingPage.test.tsx` | `shows an error and retries loading` | a 500 alert "Could not load the subjects.", then `Retry` shows the checkboxes |
| 71 | `OnboardingPage.test.tsx` | `renders right-to-left in Arabic` | `dir="rtl"`, heading `اختر موادك` |
| 72 | `OnboardingPage.test.tsx` | `has no axe violations` | `[]` |
| 73 | `router.test.tsx` (modify) | rename `sends an anonymous visitor at / to sign in` → `shows the landing page to an anonymous visitor at /` | servable-count and plan mocks; link `Start for free` visible |
| 74 | `router.test.tsx` (add) | `sends a student who still needs onboarding to onboarding` | `renderApp('/student', { session: onboardingStudent })` with interests mock gives heading `Choose your subjects` |
| 75 | `SignUpPage.test.tsx` (modify) | `creates a student account with email and lands on onboarding` (was "…lands on the student home") | with `getGetSubjectInterestsMockHandler(subjectInterests())`: heading `Choose your subjects` |
| 76 | `SignUpPage.test.tsx` (modify) | `creates a student account with a mobile code` | final assertion becomes heading `Choose your subjects`; `registeredName` assertion kept |
| 77 | `SignUpPage.test.tsx` (add) | `records sign-up started and completed events` | captured funnel types include `SignUpStarted` then `SignUpCompleted` after the email sign-up |
| 78 | `authSession.test.ts` (add) | `carries needsOnboarding onto the session` | `toSession({ ...apiUser('Student'), needsOnboarding: true }).needsOnboarding` is `true` |
| 79 | `StudentHomePage.test.tsx` (add) | `lists chosen subjects under Your subjects and the rest under Other subjects` | Chemistry `isInterested: true`: heading `Your subjects` precedes the Chemistry card; heading `Other subjects` precedes Physics |
| 80 | `StudentHomePage.test.tsx` (add) | `links to editing my subjects` | link `Edit my subjects` has `href="/onboarding"` |
| 81 | `QuizPage.test.tsx` (add) | `records the first quiz answer once` | answer two questions; exactly one captured funnel body with `FirstQuizAnswered` |

Every existing test not listed above stays untouched. It must still pass after `LoginPage.test.tsx`'s type-only helper change and the fixture and session updates.

## Definition of done
- [ ] `User` has `OnboardedAt`, `SubjectInterestIds`, `NeedsOnboarding`, and `ChooseSubjectInterests` exactly as in Domain behaviour. The Teacher and Admin guard throws `USER_NOT_STUDENT`.
- [ ] Migration `AddOnboardingAndFunnelEvents` adds the two user columns (`uuid[]` default `'{}'`), backfills `OnboardedAt` for existing students, and creates `FunnelEvents` with the FK and both indexes. There are no other destructive operations.
- [ ] `FunnelEvent` has a soft-delete global filter line. The repository is registered in `AddInfrastructure`.
- [ ] `GET` and `PUT /api/students/me/subject-interests` use `ProgressViewOwn`. `POST /api/analytics/funnel-events` is `[AllowAnonymous]` with `EnableRateLimiting(AnalyticsRateLimitPolicies.FunnelEvents)`.
- [ ] Every auth result carries `user.needsOnboarding`. The mastery overview carries `isInterested`, with interested subjects first.
- [ ] `StudentsOptions` and `AnalyticsOptions` have code defaults and are registered with `ValidateOnStart`, present in `appsettings.example.json` and in `ApiFactory`. `dotnet test api/ -c Release` passes with `appsettings.json` moved aside.
- [ ] All 5 error codes are in `ErrorCodes` and in both `.resx` files.
- [ ] All tests 1–48 exist with those names and pass. The migration list is extended.
- [ ] `api/openapi/v1.json` is regenerated, and `npm --prefix web run gen:api` produces no diff after commit.
- [ ] Postman has the `Onboarding` and `Analytics` folders, in the order given.
- [ ] `/` shows the landing page to anonymous visitors and redirects signed-in users home. `bg-aurora` is used only in `LandingHero` and `SubscribeHeader` (grep).
- [ ] The landing shows the live servable count, the goal line with 100,000, three value props, catalogue plan cards, and one primary CTA to `/signup`. Loading, error+retry, RTL and axe are all covered.
- [ ] `/onboarding` is student-only and outside `AppShell`. The `/student` guard redirects when `needsOnboarding` is true. Saving or skipping sets the session flag to false, invalidates the interests and mastery queries, toasts, and navigates to `/student`.
- [ ] Home shows "موادك" / "مواد أخرى" and "تعديل موادي".
- [ ] Funnel events fire at: landing mount, sign-up mount, successful registration (phone and email), first onboarding save, and the first quiz answer (once).
- [ ] The tracker never throws and stores only a random UUID in `localStorage`.
- [ ] `PlanCardGrid` uses `usePlanCardContent`, and the existing SubscriptionPage tests pass unchanged.
- [ ] Web tests 49–81 exist and pass.
- [ ] `tsc -b`, `eslint --max-warnings=0` and `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` are clean. Coverage thresholds are met.
- [ ] No literal hex, px or arbitrary Tailwind values in `src/features` (the §16 grep). No `ml-`, `mr-`, `left` or `right` (the §14 grep). Every new string exists in both `ar` and `en`.
- [ ] `redirectToHome` is removed with no remaining references.
- [ ] Docs are updated: PRD §7.1, §10.3 (Sign-up funnel row) and §15; `docs/mastery.md`; `docs/claude-design-prompt.md` §4; `docs/prototype.md`; new `docs/analytics.md`.
- [ ] No new NuGet or npm packages.
