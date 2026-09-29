# Implementation r2 — [E9.S1] Thread creation with attached context and quota (#94)

The review returned APPROVED. The orchestrator promoted three of its non-blocking notes to must-fix. After that, the story was committed and merged with origin/main (#90 content retrieval, #112 hosting).

## Must-fix items

| # | What changed | File:line |
|---|---|---|
| 1 | An attempt whose session is an exam that is not yet submitted is refused with `409 TEACHER_THREAD_EXAM_IN_PROGRESS`. The resolver loads `attempt.SessionId`; if the session is missing it returns `404 ATTEMPT_NOT_FOUND`. This applies to both the create endpoint and the preview endpoint. | `api/Elmanhg.Application/TeacherThreads/Shared/TeacherThreadContextResolver.Questions.cs:14-18` |
| 1 | New error code | `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` (`TeacherThreadExamInProgress`) |
| 1 | resx en and ar (the Arabic has no tashkeel) | `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` (`TEACHER_THREAD_EXAM_IN_PROGRESS`) |
| 1 | Web i18n | `web/src/shared/i18n/en.json`, `ar.json` (`errors.TEACHER_THREAD_EXAM_IN_PROGRESS`) |
| 1 | Docs | `docs/ask-teacher.md`: Context table (Attempt row), Quota gate order, API table (context and create rows) |
| 1 | Tests | `TeacherThreadContextResolverTests.ResolveAsync_AttemptFromOpenExam_ThrowsTeacherThreadExamInProgress` and `ResolveAsync_AttemptSessionMissing_ThrowsAttemptNotFound`. `AnsweredAttempt()` now also stubs the session lookup. |
| 2 | Magic-byte check. `TeacherThreadImageFormats.HasMatchingSignature(IFormFile)` reads the first 12 bytes and requires the signature that matches the extension: PNG `89 50 4E 47 0D 0A 1A 0A`, JPEG `FF D8 FF`, WEBP `RIFF....WEBP`. It uses a switch expression and opens a fresh stream each time, so the later save still reads from byte 0. | `api/Elmanhg.Application/TeacherThreads/CreateTeacherThread/TeacherThreadImageFormats.cs:18-30` |
| 2 | New validator rule, which returns `422 TEACHER_THREAD_IMAGE_TYPE_INVALID` | `api/Elmanhg.Application/TeacherThreads/CreateTeacherThread/CreateTeacherThreadValidator.cs:29-31` |
| 2 | Tests | New `TeacherThreadImageFormatsTests`: 5 real images, 7 spoofed or truncated files (SVG text named .png, JPEG bytes named .png, PNG bytes named .jpg, RIFF/WAVE named .webp, a 4-byte PNG prefix, an empty file, GIF). It also checks that the stream can be re-read. New `CreateTeacherThreadValidatorTests.Validate_HtmlBytesNamedPng_FailsImageTypeInvalid`. New integration test `CreateTeacherThreadEndpointTests.Post_HtmlSpoofedAsPng_Returns422TeacherThreadImageTypeInvalid` (no thread is stored). The helper `TeacherThreadImageSignatures` is shared by the tests. |
| 2 | Docs | `docs/ask-teacher.md` Image section |
| 3 | The context preview now runs the same add-on check as create (`StudentEntitlementLoader` + `HasAskTeacher`), before any content lookup. Without the add-on it returns `403 ASK_TEACHER_REQUIRES_SUBSCRIPTION`. It does not check the quota: the web already shows a used-up quota before it calls the preview. | `api/Elmanhg.Application/TeacherThreads/GetTeacherThreadContext/GetTeacherThreadContextHandler.cs:28-32` |
| 3 | Tests | `GetTeacherThreadContextHandlerTests.Handle_BaseWithoutAddOn_ThrowsAskTeacherRequiresSubscription` (also asserts that no lesson lookup happens). Integration test `TeacherThreadContextEndpointTests.Get_BaseWithoutAddOn_Returns403AskTeacherRequiresSubscription`. |
| 3 | Docs and Postman | The API row in `docs/ask-teacher.md` no longer says "names only; no entitlement check". The Postman "Get thread context" test now accepts 200 or 403, the same way "Create thread" does. |

## Merge with origin/main

- Commit `feat(E9.S1): thread creation with attached context and quota` (74f1151), then `git merge origin/main`.
- The only textual conflict was `AppDbContextTests`. I kept main's list and appended `_AddTeacherThreads` as the 26th entry. `20260929131149_AddLessonContentIndex` sorts before `20260929132737_AddTeacherThreads`, so the timestamp order is correct and no migration was renamed.
- The EF snapshot auto-merged. `Model_Current_MatchesLatestMigrationSnapshot` and the migrations-applied test pass. I could not run `dotnet ef migrations has-pending-model-changes` at design time: it needs a DB host, and appsettings has none.
- OpenAPI and Orval: I regenerated them with `dotnet build` and then `npm --prefix web run gen:api`. Neither produced a diff against the auto-merged files.
- Postman, docs, README and `.env.example` auto-merged with both sides kept.
- Config: `docs/deployment.md` has a new "Ask a Teacher" table (`AskTeacher__*`, `Subscriptions__AskTeacherMonthlyQuestions` / `__AskTeacherReplySlaHours`) and a note that photos in `api-media/teacher-threads` are private. `deploy/api.env.example` has the commented keys, and the root `.env.example` has one pointer line.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Refuse an attempt from an open exam and add a test | In the current domain, exam `Attempt`s are only created inside `Session.SubmitExam`, which sets `SubmittedAt` in the same call. Answers saved while an exam is open are `SessionItem.SavedAnswer`, not attempts. So no HTTP path can produce this case today. | I implemented the guard as defence in depth (it covers future changes such as per-question exam attempts). The test is a unit test that stubs the session lookup to return an open exam. There is no integration test, because the state cannot be reached through the API. A student with an exam open can still ask about a lesson or a quiz attempt; blocking that would be a product decision. |

## Build & test
- `dotnet build api/`: 0 errors.
- `dotnet test api/ -c Release` (with `appsettings.json` moved aside, then restored): **Passed, total 2838, failed 0**.
- `npm --prefix web run typecheck`: clean. `npm --prefix web run lint` (`--max-warnings=0`): clean.
- `npx prettier --check --end-of-line auto .` in web: "All matched files use Prettier code style!"
- `npm --prefix web test -- --run`: **149 files, 878 tests passed**.
- `npm ci` was not needed: the web package files are unchanged by the merge.

## Notes for review
- The preview's add-on check adds `ISubscriptionRepository`, `IOptions<SubscriptionsOptions>` and `TimeProvider` to `GetTeacherThreadContextHandler`.
- The web has no special UI for `TEACHER_THREAD_EXAM_IN_PROGRESS`. It shows up as the form's root error through the shared `errors.*` translation, and the UI only links quiz attempts anyway.
- The Arabic web string for `ASK_TEACHER_REQUIRES_SUBSCRIPTION` (written before this round) has a shadda in «معلّم». I left it alone because it is outside this round's scope.
