VERDICT: CHANGES_REQUESTED

# Review — [E7.S3] Free tier limits (#87)

## Blocking

### 1. `docs/subscriptions.md` would be committed with CRLF line endings and a stray lone CR
**Where:** `docs/subscriptions.md:173` (the new `GET /api/subscriptions/usage` API row ends in CR CR LF)
**Rule:** Verify every claim in `02-implementation.md` ("git normalises them (autocrlf)"). Keep the diff honest.
**Problem:** The working file is CRLF throughout, and one lone CR sits before the CRLF at the end of line 173. Because of the lone CR, git classifies the file as non-text (`git ls-files --eol` gives `i/lf w/-text`). With `core.autocrlf=true` a non-text file is not normalised, so the whole file is committed with CRLF. The claim in the report that autocrlf normalises the rewritten files is false for this file. Every other changed file checks out as `w/lf` or `w/crlf` and normalises correctly.
**Failure:** `git diff --stat -- docs/subscriptions.md` shows 369 changed lines (192+/177-). With `--ignore-cr-at-eol` it shows 17. After commit, `main` holds a CRLF `docs/subscriptions.md` with a stray CR in the API table, and every later edit to the file produces noisy whole-file diffs.
**Fix:** Remove the extra CR on line 173 and save the file with LF endings. Then `git ls-files --eol docs/subscriptions.md` should report `w/lf` or `w/crlf`, and `git diff --stat` should show about 17 lines.

### 2. `docs/prototype.md` still says the paywall «اشترك» opens the fake checkout
**Where:** `web/src/features/subscription/components/PaywallDialog.tsx:41` (the button links to `/student/subscription`) vs `docs/prototype.md` → "Suggested walkthrough" step 8 (line 37)
**Rule:** `.claude/rules/docs-sync.md`: UI flow is owned by `docs/claude-design-prompt.md` §4–§6 **and** `docs/prototype.md`. Plan D15 deliberately departs from the prototype flow.
**Problem:** Step 8 says "Click **اشترك** to open the fake Paymob checkout". The product now opens the subscription screen instead (D15). `docs/claude-design-prompt.md` was updated (§4 Free plan line, §7 item 4); `docs/prototype.md` was not. When the product differs from the prototype, this file already records it: step 6 has "(the prototype offers 10/20; the product uses 20/40/60, PRD §7.5)", and steps 1, 2, 9 and 10 carry "The product also …" notes. The implementer flagged the gap in "Notes for review" and left it open.
**Failure:** Following `docs/prototype.md` step 8, a reader expects «اشترك» in the paywall to open the fake Paymob page. The app opens `/student/subscription`. The two docs owning this flow give two different answers.
**Fix:** Add one sentence to step 8, for example: "In the product, «اشترك» in the paywall opens the subscription screen, and the fake Paymob page follows from there."

## Non-blocking
- `postman/elmanhg.postman_collection.json:1611,1726`: the Sessions and Exams folder descriptions say "run the Subscriptions folder first", but that folder sits after them. A top-to-bottom run with a fresh (Free) student now gets 403 on exam start, and on quiz start for any non-first lesson. You could move Subscriptions earlier or sign in a Base student for those folders. This is not a contract gap, because every endpoint is mirrored.
- `api/Elmanhg.Application/Sessions/StartQuizSession/StartQuizSessionHandler.cs:42`: the lock check runs before resume, which is correct, but no unit test pins "open session on a locked lesson → 403 `LESSON_LOCKED`", which the DoD names ("start and resume"). A21 only covers a fresh start. Worth adding.
- `web/src/features/browse/pages/LessonPage.tsx:19`: no test asserts that no opening is POSTed for a locked lesson (DoD item). The code is correct: `useLessonOpening` returns early when `unitId` is undefined.
- The skipped A62/A63 (`FreeTierNextLessonEndpointTests`): the reason holds. `NextLessonRecommendation.Pick` ranks every Published lesson in the shared DB, and `Subject.Create` rejects an order below 1. A dedicated subject cannot be forced ahead of the order-1 lessons from other tests, so any deterministic assertion would pass without the filter. The tie would fall to a random `LessonId`. A45/A46 cover the filter at unit level. Accepted as a documented deviation.
- `api/Elmanhg.Application/Exams/StartUnitExam/StartUnitExamHandler.cs` is 118 lines (112 before). The plan accepted this.

## Verified
- **Build and tests, run by me:** `dotnet test api/ -c Release` with `appsettings.json` moved aside gave 2597/2597 passed, 0 skipped. The file was restored afterwards. Web `typecheck` and `lint` are clean. `vitest --run` gave 138 files and 834 tests passed. `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` is clean. These match the numbers in the report.
- **Server-side gates, fail closed:**
  - Quiz start checks the lesson lock before the resume or new branch. The quota is checked on a new session only.
  - Answer submit gates every new attempt in a non-test session: lesson lock, then quota. The lesson comes from the session scope, so it covers a session started while subscribed. It runs before any mutation and `SaveChangesAsync`.
  - Replay is exempt only when an attempt already exists. A different answer goes to 409 in `Session.RecordAttempt`, and no row is added.
  - Unit exam checks entitlement in `StartAsync`, which is reached only when no exam is open. Multi-unit checks inside `if (session is null)`. Both run before the lesson-opened gate.
  - Every gate loads through `StudentEntitlementLoader.LoadAsync`, with no try/catch. `EntitlementResultGenerator` maps any non-Base tier to Free limits. A26 proves a failed lookup propagates and nothing is saved.
  - `Session.StartQuiz`, `StartUnitExam` and `StartMultiUnitExam` are the only session creators (grep), and `RecordAttempt` is only called from `SubmitAnswerHandler`, so no other path creates a quiz or exam.
- **Exemptions:** Admins are exempt through an exact role match on quiz and exam start. Answers in `IsTestMode` sessions are exempt. Browse, usage and the openings endpoint are Student-only policies (`Progress.ViewOwn` / `Subscription.Manage`).
- **Locked lesson content:** `StudentLessonResultGenerator` returns empty strings for explanation and summary, a null video and empty objectives when locked. No other student-facing endpoint returns lesson content: `LessonDetailResult` is admin `Content.Manage`. A42 (unit) and A60 (HTTP) check this, and the source lesson in A60 has objectives, so the empty-array assertion is meaningful.
- **Quota count:** parameterised `SqlQuery` over non-deleted, non-test `Quiz` attempts, using `AT TIME ZONE` with the configured zone and a safe UTC lower bound. A16 pins the Cairo date conversion.
- **Existing tests:** the only changes are wiring. The unit tests got a Base stub, options and a clock. `GetStudentUnitHandlerTests` expected records gained `false`. The subscription and payment integration files switched to `SignedInFreeStudentAsync` with no assertion changes (diff checked line by line). Free flows are still exercised end to end: A47/A48 (10 successful answers, finish, restart), A55 (usage after answers) and A59/A60 (browse).
- **Plan contract:** every file in the plan section *Files to create* exists with the planned signatures. A1–A61 and W-1–W-25 exist with the planned names. Error codes are in `ErrorCodes.cs`, both resx files and both `common:errors` files. `DailyQuotaTimeZone` is in the options with a default and startup validation (A29), and in `appsettings.example.json`, `ApiFactory` and `docs/subscriptions.md`. OpenAPI and Orval are regenerated (`/api/subscriptions/usage`, `isLocked`). Postman has "Get my usage" right after "Get my entitlement", with the same URL, method and auth inheritance.
- **Web:** every class is a token (`bg-soft`, `text-micro`, `border-warning`, `bg-warning-soft`, `shadow-1`, …). There are no literals. Cross-feature imports go through the `@/features/subscription` barrel.
- **Docs agree with the code:** subscriptions, browsing, sessions, exams, mastery and the claude-design-prompt edits all match. `docs/PRD.md` §11.1 agrees.
- **Guard grep:** nothing found for `DateTime.Now/UtcNow`, `.Result`, `.Wait()`, `FromSqlRaw` or `async void`. Both new comments are WHY comments that the plan specified.

## Test quality
- `LessonAccessTests`, `FreeTierGateTests` and `GetMyUsageHandlerTests` are tight: exact codes, the `limit` context, the Cairo date and `Math.Max`.
- `StartQuizSessionFreeTierTests`, `SubmitAnswerFreeTierTests`, `StartUnitExamFreeTierTests` and `StartMultiUnitExamFreeTierTests` constrain well. Throwing paths assert the type, the code and `SaveChangesAsync DidNotReceive`. Success paths assert `Received(1)` and the outcome.
- `GetStudentUnitFreeTierTests`, `GetStudentLessonFreeTierTests` and `GetMasteryOverviewFreeTierTests` constrain well: the lock pattern, the withheld content, and the candidate filter checked against a lower-mastery locked lesson.
- The integration tests assert status, code and DB state (attempt count, no session row).
- The web tests assert user-visible text, roles and hrefs. W-18 seeds the cache, so it fails if the `isTestMode` check is removed.
- None of the tests is vacuous.
