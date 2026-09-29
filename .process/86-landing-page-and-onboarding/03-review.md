VERDICT: APPROVED

# Review — [E7.S2] Landing page and onboarding (#86)

## Blocking
None.

## Non-blocking
1. `web/src/features/onboarding/hooks/useSubjectInterestsSave.ts:44-47`: the failure branch of `skip` (the error toast) has no test. react-testing "Coverage" asks for a test on every new branch. The plan did not list one, and the implementer disclosed the gap. Worth adding: a PUT 500 on «تخطّي الآن» shows the generic error toast and stays on `/onboarding`.
2. Coverage timeouts (`.github/workflows/web-ci.yml:43`, `npm test -- --run --coverage`, no `maxWorkers` or `testTimeout` in `web/vite.config.ts`):
   - **Local default run:** I reproduced 8 timeouts at 5000 ms on this 20-thread machine (19 workers): RichTextEditor x2, NewQuestionPage x3, LessonItem, LessonEditorPage, and the modified `SignUpPage` "lands on onboarding" test.
   - **Local `--maxWorkers=3`:** 809/809 pass in 162 s, which is close to the 151 s CI test step.
   - **CI:** the `ubuntu-latest` runner has 4 vCPUs, so it already runs about 3 workers. The last 15 web-ci runs went green on the first attempt, and only 2 of the last 60 failed (#77, and #81, which was the known #148 flake).
   - **Verdict:** a CI flake is unlikely, so this does not block. For a deterministic run, pin it in `vite.config.ts` `test` (for example `maxWorkers: 3` or `testTimeout: 10_000`) in a follow-up. That is the smallest fix, and it also stops local runs on many-core machines from failing.
3. `web/src/features/quiz/hooks/useQuizAnswer.ts:36`: `FirstQuizAnswered` also fires for an admin answering in test mode. It sets the once-flag in that browser and records an admin `UserId`, which adds a little noise to the funnel.
4. `api/Elmanhg.Api/RateLimiting/AuthRateLimiting.cs:29`: the analytics policy is registered inside `AddAuthRateLimiting`. It works, but the name no longer describes what the method does.
5. `api/Elmanhg.Api/RateLimiting/AuthRateLimiting.cs:36`: requests are partitioned by `Connection.RemoteIpAddress`, and `Program.cs` has no `UseForwardedHeaders`. Behind a reverse proxy (#112), every visitor shares one 60/min bucket, and the swallowed 429s would silently drop funnel events. This is pre-existing and also affects the auth limits. Track it with #112.
6. `docs/PRD.md` §13 Security: "rate limits on auth and Avatar" now also applies to analytics. This is incomplete wording, not a divergence.
7. `web/src/features/onboarding/pages/OnboardingPage.tsx:41`: onboarding sits outside `AppShell` and has no sign-out. A student can only leave through «تخطّي الآن» or «متابعة».
8. The new error codes (`SUBJECT_INTERESTS_*`, `FUNNEL_*`, `USER_NOT_STUDENT`) are not in the web `common:errors` locale, so they fall back to the generic message (disclosed by the implementer). None of them can be reached from the UI.

## Verified
- **API tests:** `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside gave total 2536, failed 0, exit 0. The file was restored, and I confirmed it is present afterwards.
- **Web checks:**
  - `npm run typecheck`, exit 0.
  - `npm run lint`, exit 0.
  - `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: clean.
  - `npm test -- --run`: 127 files, 809 tests passed.
  - `npx vitest run --coverage --maxWorkers=3`: 809/809, All files 95.17 / 83.09 / 91.1 / 95.29, thresholds met, exit 0.
- **Anonymous funnel endpoint:**
  - `AnalyticsController.cs:15-17` is `[AllowAnonymous]` and `[EnableRateLimiting(analytics-funnel-events)]`.
  - The body is only `Guid AnonymousId` and the enum `FunnelEventType` (`Controllers/Analytics/Requests.cs:5`). `IsInEnum` rejects numeric out-of-range values (integration test `type: 99` returns 422).
  - `OccurredAt` is stamped by the server and `UserId` comes only from `ICurrentUserService`. No free text and no PII is stored.
  - A fixed window per IP, 60/60 by default, returns 429 `TOO_MANY_REQUESTS` (integration test with limit 1).
  - Plan Decision 11 documents this exception to the "AllowAnonymous only for public reads" rule in the skill.
- **Onboarding guard:**
  - `requireOnboarded` (`web/src/features/session/guards.ts:125-129`) only redirects a student whose `needsOnboarding` is true. It is called only from `routes/student/route.tsx:8`.
  - `/onboarding` is a top-level route guarded only by `requireRole(student)`, so there is no loop. Router test 74 renders it from `/student`.
  - Teacher and admin routes are untouched, and `NeedsOnboarding` is false for them on the server (`User.cs:23`; tests 2 and 29).
  - A save or skip sets the session flag to false before navigating. The "lands on the home" tests would fail if it did not, because the guard would bounce back.
  - The flag survives a reload because every auth result, including refresh, carries it.
- **Backfill migration** (`20260929062410_AddOnboardingAndFunnelEvents.cs:67`):
  - Sets `OnboardedAt = CreationDate` for every row with `Role` Student, after the columns are added.
  - `Role` is stored as a string, and `CreationDate` exists on `AspNetUsers`.
  - `uuid[]` defaults to an empty array. `Down` drops only what `Up` added. The snapshot is in sync (`HasPendingModelChanges` test).
- **Existing tests with `needsOnboarding`:**
  - The only existing tests changed are the ones the plan lists: tests 30, 48, 73, 75 and 76, the `LoginPage.test` helper (type-only) and the `authSession.test` helper.
  - The `GetMasteryOverviewHandlerTests` expectation only gained a trailing `false`.
  - SignUp tests 75 and 76 still assert the user-visible landing, which is now onboarding, and `registeredName`.
  - Fixtures default to `needsOnboarding: false` and `isInterested: false`, so the other existing screens behave as before.
- **Contract fidelity:**
  - Every file in Files to create (1-60) exists.
  - Signatures match: `AuthUserResult`, `SubjectMasteryResult`, the `MasteryOverviewResultGenerator.Generate` order, and the constructors of both handlers.
  - Nothing extra was added beyond the three deviations the implementer disclosed:
    - `useSubjectInterestsSave` called with the loaded `needsOnboarding` or false, needed for the rules of hooks.
    - `ValueGeneratedNever()` plus the conventional FK index.
    - Sorted assertions in the tracker tests.
  - All three are justified.
- **Plan decisions:**
  - Aurora is used only in `LandingHero.tsx:18` and `SubscribeHeader.tsx:9` (grep).
  - `redirectToHome` has no remaining references.
  - `PlanCardGrid` uses `usePlanCardContent`, and the SubscriptionPage tests pass unchanged.
  - Options are registered with `ValidateOnStart` and are present in `appsettings.example.json` and `ApiFactory`.
  - All 5 error codes are in `ErrorCodes` and in both `.resx` files.
  - The policy is `ProgressViewOwn` on both Students actions.
- **Design system:**
  - No hex values, px arbitrary values or physical-direction utilities in the new feature folders (the CI greps return nothing).
  - Every visual class is a token: `bg-aurora`, `text-surface/80` (same as `SubscribeHeader`), `shadow-1`, the rounded tokens, `accent-text`, `has-checked:bg-soft`.
  - There is one primary action per screen.
  - RTL and axe are tested on both new pages.
- **Postman:**
  - The `Onboarding` folder comes right after `Browse`, with GET, PUT (a body with the `subjectId` variable) and GET-after-save.
  - The `Analytics` folder is last, with a `noauth` POST and a random guid.
  - Every request has a `status is 200` test. The routes match the controllers.
- **Docs:**
  - PRD §7.1, §10.3 (Sign-up funnel row) and §15 match the code.
  - `docs/mastery.md` has `isInterested` and the ordering sentence.
  - `docs/claude-design-prompt.md` §4 has the Landing CTA, top bar, catalogue and redirect, the `/onboarding` bullet and the Home bullet.
  - `docs/prototype.md` walkthrough step 1 is updated.
  - `docs/analytics.md` matches the endpoint, config keys, storage and indexes.
  - I found no divergence.
- **OpenAPI and client:** `api/openapi/v1.json` contains both new routes plus `needsOnboarding` and `isInterested`. The generated `students/` and `analytics/` clients and the model changes are present.

## Test quality
- **UserTests (1-6):** constrain the rules: distinct ids, the first `OnboardedAt` is kept, `UpdationDate`, and the Teacher guard leaves the ids unchanged.
- **SaveSubjectInterestsHandlerTests:** assert the exception type and code with `DidNotReceive()` on every throwing path, and `Received(1)` on success. The empty list asserts `CountAsync` `DidNotReceive()`, which is the behaviour itself. The unknown-subject test also asserts that the user was not mutated.
- **GetSubjectInterestsHandlerTests** and **GetMasteryOverviewHandlerTests (31):** assert real mapping and ordering, not substitute echoes.
- **RecordFunnelEventHandlerTests:** capture the added entity and assert the server time and a null or current `UserId`, so they are not vacuous.
- **Validator tests:** one failing case per rule.
- **Integration tests:** the Students endpoints read the DB from a fresh scope. The funnel endpoints check that no row is written on 422, and the 429 test changes the limit.
- **funnelTracker.test:** exercises the same id every time, the once-flag, and that a 500 is swallowed.
- **LandingPage and OnboardingPage tests:** cover loading, success, empty, error with retry, RTL and axe. Test 66 proves `OnboardingCompleted` fires only on the first completion.
- **Router test 74** proves the guard. **QuizPage test 81** proves the once-flag end to end.
- No vacuous tests found.
