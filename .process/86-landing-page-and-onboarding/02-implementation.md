# Implementation — [E7.S2] Landing page and onboarding (#86)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Analytics/FunnelEventType.cs | 3 | Whitelisted funnel event enum (5 values) |
| api/Elmanhg.Domain/Analytics/FunnelEvent.cs | 24 | Entity + pure `Record` factory (adapted from Morabh `AppEvent`) |
| api/Elmanhg.Domain/Analytics/IFunnelEventRepository.cs | 5 | Repo interface |
| api/Elmanhg.Infrastructure/Analytics/FunnelEventRepository.cs | 7 | Repo |
| api/Elmanhg.Infrastructure/Migrations/20260929062410_AddOnboardingAndFunnelEvents.cs (+ .Designer.cs) | 84 / 2095 | Two `AspNetUsers` columns (`uuid[]` default `'{}'`), `FunnelEvents` table + FK + indexes, hand-added student `OnboardedAt` backfill |
| api/Elmanhg.Application/Shared/Options/StudentsOptions.cs | 11 | `Students:SubjectInterestsMaxCount` (50) |
| api/Elmanhg.Application/Shared/Options/AnalyticsOptions.cs | 14 | `Analytics:FunnelEventPermitLimit` / `WindowSeconds` (60/60) |
| api/Elmanhg.Application/Students/GetSubjectInterests/GetSubjectInterestsQuery.cs | 6 | Query |
| api/Elmanhg.Application/Students/GetSubjectInterests/GetSubjectInterestsHandler.cs | 29 | Handler |
| api/Elmanhg.Application/Students/SaveSubjectInterests/SaveSubjectInterestsCommand.cs | 5 | Command |
| api/Elmanhg.Application/Students/SaveSubjectInterests/SaveSubjectInterestsValidator.cs | 21 | Cap / duplicate / empty-guid rules |
| api/Elmanhg.Application/Students/SaveSubjectInterests/SaveSubjectInterestsHandler.cs | 35 | Handler (count check skipped for empty list) |
| api/Elmanhg.Application/Students/Shared/SubjectInterestsResult.cs | 3 | Result |
| api/Elmanhg.Application/Students/Shared/SubjectInterestResult.cs | 3 | Result |
| api/Elmanhg.Application/Students/Shared/SubjectInterestsResultGenerator.cs | 14 | Generator |
| api/Elmanhg.Application/Analytics/RecordFunnelEvent/RecordFunnelEventCommand.cs | 6 | Command (anon id + enum only) |
| api/Elmanhg.Application/Analytics/RecordFunnelEvent/RecordFunnelEventValidator.cs | 14 | Required id, `IsInEnum` |
| api/Elmanhg.Application/Analytics/RecordFunnelEvent/RecordFunnelEventHandler.cs | 16 | Server-stamped time, optional user id |
| api/Elmanhg.Api/RateLimiting/AnalyticsRateLimitPolicies.cs | 6 | Policy name |
| api/Elmanhg.Api/Controllers/Students/StudentsController.cs | 33 | GET/PUT `me/subject-interests` (`ProgressViewOwn`) |
| api/Elmanhg.Api/Controllers/Students/Requests.cs | 3 | `SubjectInterestsRequest` |
| api/Elmanhg.Api/Controllers/Analytics/AnalyticsController.cs | 24 | Anonymous, rate-limited POST `funnel-events` |
| api/Elmanhg.Api/Controllers/Analytics/Requests.cs | 5 | `RecordFunnelEventRequest(Guid AnonymousId, FunnelEventType Type)` |
| docs/analytics.md | 51 | Event table, anonymous id, endpoint, storage, funnel definition for #104 |
| api/Elmanhg.Tests/Domain/Analytics/FunnelEventTests.cs | 23 | Test 7 |
| api/Elmanhg.Tests/Application/Features/Students/GetSubjectInterests/GetSubjectInterestsHandlerTests.cs | 70 | Tests 8–11 |
| api/Elmanhg.Tests/Application/Features/Students/SaveSubjectInterests/SaveSubjectInterestsHandlerTests.cs | 92 | Tests 12–16 |
| api/Elmanhg.Tests/Application/Features/Students/SaveSubjectInterests/SaveSubjectInterestsValidatorTests.cs | 46 | Tests 17–21 |
| api/Elmanhg.Tests/Application/Features/Analytics/RecordFunnelEvent/RecordFunnelEventHandlerTests.cs | 52 | Tests 22–23 |
| api/Elmanhg.Tests/Application/Features/Analytics/RecordFunnelEvent/RecordFunnelEventValidatorTests.cs | 31 | Tests 24–26 |
| api/Elmanhg.Tests/Application/Features/Auth/Shared/AuthResultGeneratorTests.cs | 35 | Tests 27–29 |
| api/Elmanhg.Tests/Integration/Students/SubjectInterestsEndpointTests.cs | 154 | Tests 32–40 |
| api/Elmanhg.Tests/Integration/Analytics/FunnelEventsEndpointTests.cs | 102 | Tests 41–45 |
| web/src/features/analytics/api/funnelTracker.ts | 32 | Anonymous id + fire-and-forget tracker with once-flag |
| web/src/features/analytics/hooks/useFunnelEventOnMount.ts | 9 | Mount hook |
| web/src/features/analytics/index.ts | 2 | Barrel |
| web/src/features/analytics/api/funnelTracker.test.ts | 64 | Tests 49–51 |
| web/src/features/landing/api/marketing.ts | 2 | `marketedQuestionGoal = 100_000` |
| web/src/features/landing/components/LandingTopBar.tsx | 16 | App name + «تسجيل الدخول» |
| web/src/features/landing/components/LandingHero.tsx | 30 | Aurora hero, live count, goal, CTA to `/signup` |
| web/src/features/landing/components/ValueProps.tsx | 53 | Three value cards |
| web/src/features/landing/i18n/ar.json, en.json | 33 / 33 | Landing copy |
| web/src/features/landing/locales.ts | 4 | Locales |
| web/src/features/landing/index.ts | 1 | Barrel |
| web/src/features/landing/pages/LandingPage.tsx | 51 | Page (loading / error+retry / plans) |
| web/src/features/landing/pages/LandingPage.test.tsx | 115 | Tests 52–60 |
| web/src/features/onboarding/schemas/subjectInterestsSchema.ts | 7 | Zod (min 1) |
| web/src/features/onboarding/schemas/subjectInterestsSchema.test.ts | 16 | Test 61 |
| web/src/features/onboarding/hooks/useSubjectInterestsSave.ts | 51 | Save/skip; session flag, invalidation, toast, navigate |
| web/src/features/onboarding/components/SubjectInterestsForm.tsx | 71 | Checkbox fieldset form |
| web/src/features/onboarding/pages/OnboardingPage.tsx | 47 | Page (loading / error / empty / form) |
| web/src/features/onboarding/i18n/ar.json, en.json | 22 / 22 | Onboarding copy |
| web/src/features/onboarding/locales.ts | 4 | Locales |
| web/src/features/onboarding/index.ts | 1 | Barrel |
| web/src/features/onboarding/pages/OnboardingPage.test.tsx | 176 | Tests 62–72 |
| web/src/routes/onboarding.tsx | 10 | Student-only top-level route |
| web/src/features/mastery/components/HomeSubjects.tsx | 45 | «موادك» / «مواد أخرى» + «تعديل موادي» |
| web/src/features/subscription/hooks/usePlanCardContent.ts | 54 | Title / price / feature copy moved out of `PlanCardGrid` |
| web/src/features/subscription/components/PublicPlanCards.tsx | 19 | Action-less plan cards |
| web/src/test/onboardingFixtures.ts | 13 | `subjectInterests()` fixture |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/Identity/User.cs | `OnboardedAt`, `SubjectInterestIds`, `NeedsOnboarding`, `ChooseSubjectInterests` exactly as planned |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | `UserNotStudent` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | `// STUDENTS` + `// ANALYTICS` groups (4 codes) |
| api/Elmanhg.Application/Auth/Shared/AuthUserResult.cs, AuthResultGenerator.cs | `NeedsOnboarding` |
| api/Elmanhg.Application/Mastery/Shared/SubjectMasteryResult.cs, MasteryOverviewResultGenerator.cs | `IsInterested`; interested first (stable `OrderByDescending`) |
| api/Elmanhg.Application/Mastery/GetMasteryOverview/GetMasteryOverviewHandler.cs | `IUserRepository` dependency, passes interests |
| api/Elmanhg.Application/DependencyInjection.cs | Registers both options with `ValidateOnStart` |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | `FunnelEvents` DbSet, user column config, `ConfigureFunnelEvents`, soft-delete filter |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | Repo registration |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | Regenerated |
| api/Elmanhg.Api/RateLimiting/AuthRateLimiting.cs | Analytics fixed-window policy |
| api/Elmanhg.Api/Resources/Messages.en.resx, Messages.ar.resx | 5 keys each |
| api/Elmanhg.Api/appsettings.example.json | `Students`, `Analytics` sections (also added to the gitignored local `appsettings.json`) |
| api/openapi/v1.json | Regenerated by `dotnet build` |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | 3 config keys |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `twentyFourth => …_AddOnboardingAndFunnelEvents` |
| api/Elmanhg.Tests/Application/Features/Mastery/GetMasteryOverview/GetMasteryOverviewHandlerTests.cs | `_userRepository` ctor arg; trailing `false` on the two expected records (test 30); new test 31 |
| api/Elmanhg.Tests/Domain/Identity/UserTests.cs | Tests 1–6 |
| api/Elmanhg.Tests/Integration/Auth/PhoneAuthEndpointTests.cs | Test 46 |
| api/Elmanhg.Tests/Integration/Mastery/MasteryOverviewEndpointTests.cs | Test 47 |
| postman/elmanhg.postman_collection.json | `Onboarding` folder after `Browse` (3 requests), `Analytics` folder last (noauth POST); each with a `status is 200` test |
| web/src/routes/index.tsx | `redirectSignedIn` + `LandingPage` |
| web/src/routes/student/route.tsx | `requireOnboarded` after `requireRole` |
| web/src/routeTree.gen.ts | Regenerated |
| web/src/app/i18n.ts | `landing`, `onboarding` namespaces |
| web/src/features/session/sessionStore.ts, authSession.ts, guards.ts, index.ts | `needsOnboarding`; `requireOnboarded`; `redirectToHome` deleted (no references remain) |
| web/src/features/session/pages/SignUpPage.tsx | `useFunnelEventOnMount('SignUpStarted')` |
| web/src/features/session/components/PhoneSignUp.tsx, EmailSignUpForm.tsx | `trackFunnelEvent('SignUpCompleted')` between `mutateAsync` and `startSession` |
| web/src/features/quiz/hooks/useQuizAnswer.ts | `trackFunnelEvent('FirstQuizAnswered', { once: true })` in `onSuccess` |
| web/src/features/mastery/pages/StudentHomePage.tsx | Uses `HomeSubjects` |
| web/src/features/mastery/i18n/ar.json, en.json | `subjects.others`, `subjects.edit` |
| web/src/features/subscription/components/PlanCardGrid.tsx | Uses `usePlanCardContent`; markup/behaviour unchanged (existing SubscriptionPage tests pass untouched) |
| web/src/features/subscription/index.ts | Exports `PublicPlanCards` |
| web/src/shared/api/generated/** | Regenerated (`npm --prefix web run gen:api`; re-run shows no further diff) |
| web/src/test/msw/server.ts | Default `getRecordFunnelEventMockHandler()` |
| web/src/test/setup.ts | `localStorage.clear()` in `afterEach` |
| web/src/test/sessions.ts | `needsOnboarding: false` on all three; `onboardingStudent` |
| web/src/test/masteryFixtures.ts | `isInterested: false` on both subjects |
| web/src/app/router.test.tsx | Test 73 (renamed/modified), test 74 |
| web/src/features/session/pages/SignUpPage.test.tsx | `needsOnboarding: true` on `studentResult`; tests 75, 76 modified (mastery mock replaced by the interests mock, final heading `Choose your subjects`, `registeredName` assertion kept); test 77 |
| web/src/features/session/pages/LoginPage.test.tsx | `authResult` helper gains `needsOnboarding: false` (type-only) |
| web/src/features/session/authSession.test.ts | `apiUser` gains `needsOnboarding: false`; test 78 |
| web/src/features/mastery/pages/StudentHomePage.test.tsx | Tests 79, 80 |
| web/src/features/quiz/pages/QuizPage.test.tsx | Test 81 |
| docs/PRD.md | §7.1 step 1 + Home sentence; §10.3 `Sign-up funnel` row; §15 `User(... onboarded_at?, subject_interest_ids[])` + `FunnelEvent(...)` |
| docs/mastery.md | `isInterested` + ordering sentence |
| docs/claude-design-prompt.md | §4 Student Home bullet, new `/onboarding` bullet, Landing CTA/top bar/catalogue/redirect |
| docs/prototype.md | Walkthrough step 1 note |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `OnboardingPage` empty state gets `skip` "from `useSubjectInterestsSave(data.needsOnboarding)`" | A hook cannot be called only after `data` has loaded (rules of hooks); a second component in the same file would break the one-component-per-file rule | The page calls `useSubjectInterestsSave(data?.needsOnboarding ?? false)` unconditionally at the top; by the time the empty state renders it carries the real flag |
| `ConfigureFunnelEvents` lists Type conversion, FK and two indexes | The sibling append-only entity config (`LessonOpening`) also sets `Property(x => x.Id).ValueGeneratedNever()`; EF also adds the conventional FK index `IX_FunnelEvents_UserId` | Added `ValueGeneratedNever()` to match the sibling; kept the scaffolded FK index. No other schema change |
| Test 49/50 wording implies order of captured bodies | Two fire-and-forget POSTs can reach the MSW handler in either order | Assertions compare the sorted list of types (still exact content and count) |

No other deviations. Plan-gate condition holds: the anonymous endpoint's contract is `{ anonymousId: Guid, type: FunnelEventType }` only, validated by `IsInEnum` (numeric out-of-range → 422, tested), no free text, no PII.

## Build & test
- `dotnet build api/` → `Build succeeded. 0 Warning(s) 0 Error(s)`; `api/openapi/v1.json` regenerated.
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside (restored afterwards) → `total: 2536, failed: 0, succeeded: 2536, skipped: 0`. 46 new api tests + 2 modified (tests 30, 48).
- Mutation checks (api): removing the interested-first ordering, replacing `OnboardedAt ??=` with `=`, and removing `[EnableRateLimiting]` each failed the intended tests (4 failures); all restored.
- `npm --prefix web run gen:api` → regenerated; re-run produced no further diff.
- `npx vite build` → `built in 5.75s`.
- `npm --prefix web run typecheck` → clean. `npm --prefix web run lint` → clean (0 warnings).
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → `All matched files use Prettier code style!`
- `npm --prefix web test -- --run` → `Test Files 127 passed (127)`, `Tests 809 passed (809)`. 31 new web tests + 3 modified (73, 75, 76).
- Mutation checks (web): removing `requireOnboarded` from the student route, disabling the once-flag, always tracking `OnboardingCompleted`, and ignoring interests in `HomeSubjects` failed 8 targeted tests; all restored.
- `npx vitest run --coverage` at default parallelism: 2–6 timeouts in unrelated heavy tests (RichTextEditor, NewQuestionPage, LessonItem, ExamPage.submit; different ones each run, the known #148 class of flake). With `--maxWorkers=3`: 809/809 pass, Statements 95.17%, Branches 83.09%, Lines 95.29%; thresholds met. New files: 98.4% lines / 93.9% branches.

## Notes for review
- New error codes (`SUBJECT_INTERESTS_*`, `FUNNEL_*`, `USER_NOT_STUDENT`) are in both `.resx` files but not in `web/src/shared/i18n/*.json` (that file was not in the plan's touched list). None is reachable from the UI (checkboxes are distinct, the cap is 50, funnel errors are swallowed); an unexpected one falls back to the generic message.
- `useSubjectInterestsSave.skip`'s error-toast branch (lines 45–46) is the only uncovered branch in the new code; no planned test exercises a failed skip.
- The funnel POST uses the Orval `recordFunnelEvent` through the `http` mutator, so a signed-in student's bearer token is attached and `UserId` is filled; anonymous visitors send none.
- The rate limiter partitions by IP per policy, so the analytics window is independent of the auth windows.
- `LandingPage` imports `useGetPlanCatalogue` from `@/shared/api/generated/plans/plans` (where Orval puts it), not `subscriptions`.
- `git add -N` was briefly used to count lines and then undone with `git reset -q`; nothing is staged or committed.
