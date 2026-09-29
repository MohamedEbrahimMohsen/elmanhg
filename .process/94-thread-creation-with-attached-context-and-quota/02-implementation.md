# Implementation — [E9.S1] Thread creation with attached context and quota (#94)

Worktree `D:\Personal\elmanhg-wt\94`, branch `feature/94-thread-creation-with-attached-context-and-quota`. Not committed.

## Files created

### API — production
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/TeacherThreads/TeacherThreadStatus.cs` | 3 | `Open, Answered, Closed` |
| `api/Elmanhg.Domain/TeacherThreads/TeacherMessageKind.cs` | 3 | `Text, Voice` |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThreadContext.cs` | 11 | jsonb snapshot record, `ToJson`/`FromJson` (mirrors `QuizScope`) |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.cs` | 39 | Aggregate: `Submit`, `ReadContext`, `IsOverdueAt`, µs truncation |
| `api/Elmanhg.Domain/TeacherThreads/TeacherMessage.cs` | 35 | Child: internal `CreateText` (blank → `TEACHER_MESSAGE_TEXT_REQUIRED`) |
| `api/Elmanhg.Domain/TeacherThreads/ITeacherThreadRepository.cs` | 5 | Repo interface |
| `api/Elmanhg.Application/Shared/Options/AskTeacherOptions.cs` | 17 | Caps with code defaults |
| `api/Elmanhg.Application/TeacherThreads/Shared/AskTeacherGate.cs` | 47 | Calendar-month window (Cairo → UTC), count, add-on + quota gate |
| `.../TeacherThreads/Shared/TeacherThreadContextSources.cs` | 9 | Repo bundle |
| `.../TeacherThreads/Shared/TeacherThreadContextResolver.cs` | 32 | lesson / question / attempt → context |
| `.../TeacherThreads/Shared/TeacherThreadContextResolver.Questions.cs` | 26 | attempt (served revision) + question parts |
| `.../TeacherThreads/Shared/TeacherThreadContextRules.cs` | 10 | exactly-one-context rule |
| `.../TeacherThreads/Shared/TeacherThreadContextResult.cs`, `TeacherMessageResult.cs`, `TeacherThreadResult.cs`, `TeacherThreadSummaryResult.cs` | 3–5 each | Results |
| `.../TeacherThreads/Shared/TeacherThreadResultGenerator.cs` | 33 | Mapping |
| `.../TeacherThreads/CreateTeacherThread/CreateTeacherThreadCommand.cs` | 7 | Command |
| `.../CreateTeacherThread/TeacherThreadImageFormats.cs` | 10 | Allowed formats + `StorageFolder` |
| `.../CreateTeacherThread/CreateTeacherThreadValidator.cs` | 30 | Text / context / image rules |
| `.../CreateTeacherThread/CreateTeacherThreadHandler.cs` | 51 | Gate order, image save, one `SaveChangesAsync` |
| `.../GetTeacherThreadContext/GetTeacherThreadContextQuery.cs`, `Validator.cs`, `Handler.cs` | 6 / 16 / 28 | Context preview |
| `.../GetMyTeacherThreads/GetMyTeacherThreadsQuery.cs`, `Validator.cs`, `Handler.cs` | 7 / 19 / 37 | Paged list |
| `.../GetMyTeacherThread/GetMyTeacherThreadQuery.cs`, `Handler.cs` | 6 / 25 | Owner-scoped read |
| `.../TeacherThreads/CanViewTeacherThreadImage/CanViewTeacherThreadImageQuery.cs` | 5 | **Plan-gate addition** |
| `.../TeacherThreads/CanViewTeacherThreadImage/CanViewTeacherThreadImageHandler.cs` | 33 | **Plan-gate addition**: owner, Admin, or Teacher assigned to the subject |
| `api/Elmanhg.Infrastructure/TeacherThreads/TeacherThreadRepository.cs` | 7 | Repo |
| `api/Elmanhg.Infrastructure/Migrations/20260929132737_AddTeacherThreads.cs` + `.Designer.cs` | gen | CreateTable ×2, FKs, indexes only |
| `api/Elmanhg.Api/Controllers/TeacherThreads/TeacherThreadsController.cs` | 55 | 4 actions, `AskTeacherSubmit` |
| `api/Elmanhg.Api/FileStorage/TeacherThreadMediaMiddleware.cs` | 37 | **Plan-gate addition**: serves `/api/media/teacher-threads/*` only when authorised, else empty 404 |
| `api/Elmanhg.Api/FileStorage/PublicMediaFileProvider.cs` | 28 | **Plan-gate addition**: the public static mount never serves anything physically under `teacher-threads` |

### API — tests
| Path | Tests |
|---|---|
| `Builders/TeacherThreadBuilder.cs` | builder |
| `Domain/TeacherThreads/TeacherThreadTests.cs` | 6 methods (7 cases) |
| `Domain/TeacherThreads/TeacherThreadContextTests.cs` | 1 |
| `Application/Features/TeacherThreads/Shared/AskTeacherGateTests.cs` | 5 |
| `Application/Features/TeacherThreads/Shared/TeacherThreadContextResolverTests.cs` | 10 |
| `.../CreateTeacherThread/CreateTeacherThreadHandlerTests.cs` | 6 |
| `.../CreateTeacherThread/CreateTeacherThreadValidatorTests.cs` | 9 |
| `.../GetTeacherThreadContext/GetTeacherThreadContextValidatorTests.cs` / `HandlerTests.cs` | 2 / 2 |
| `.../GetMyTeacherThreads/GetMyTeacherThreadsValidatorTests.cs` / `HandlerTests.cs` | 3 / 2 |
| `.../GetMyTeacherThread/GetMyTeacherThreadHandlerTests.cs` | 3 |
| `.../CanViewTeacherThreadImage/CanViewTeacherThreadImageHandlerTests.cs` | 7 (**plan-gate addition**) |
| `Integration/TeacherThreads/TeacherThreadTestData.cs` | helpers |
| `Integration/TeacherThreads/CreateTeacherThreadEndpointTests.cs` | 9 |
| `Integration/TeacherThreads/TeacherThreadReadEndpointTests.cs` | 7 |
| `Integration/TeacherThreads/TeacherThreadContextEndpointTests.cs` | 4 |
| `Integration/TeacherThreads/TeacherThreadMediaEndpointTests.cs` | 7 methods, 11 cases (**plan-gate addition**) |

### Web
| Path | Purpose |
|---|---|
| `web/src/features/askTeacher/index.ts`, `locales.ts`, `i18n/ar.json`, `i18n/en.json` | Barrel, locales (plan copy + 2 extra keys, see Deviations) |
| `api/threadBadge.ts` (+ test), `api/remainingHours.ts` (+ test) | Pure helpers |
| `schemas/askTeacherFormSchema.ts`, `askTeacherListSearchSchema.ts`, `askTeacherNewSearchSchema.ts` (+ 3 tests) | Zod |
| `hooks/useMyThreads.ts`, `useAskTeacherListSearch.ts`, `useCreateThread.ts` | Hooks |
| `components/AskTeacherLink.tsx`, `AskTeacherUpsell.tsx`, `ThreadStatusBadge.tsx`, `ThreadListItem.tsx`, `ThreadList.tsx`, `ContextSummary.tsx`, `AttachedContext.tsx`, `LessonPicker.tsx`, `ImageField.tsx`, `AskTeacherForm.tsx`, `ThreadContextCard.tsx`, `ThreadMessage.tsx` | Components per plan |
| `components/ThreadImage.tsx` | **Plan-gate addition**: loads the private photo through the `http` mutator (bearer token) and renders a data URL |
| `pages/AskTeacherListPage.tsx`, `AskTeacherNewPage.tsx`, `TeacherThreadPage.tsx` (+ `AskTeacherListPage.test.tsx` 8, `AskTeacherNewPage.test.tsx` 13, `AskTeacherNewPage.picker.test.tsx` 3, `TeacherThreadPage.test.tsx` 3) | Pages |
| `web/src/routes/student/ask-new.tsx`, `web/src/routes/student/thread.$threadId.tsx` | Routes |
| `web/src/test/askTeacherFixtures.ts` | Fixtures |
| `web/src/features/browse/pages/LessonPage.askTeacher.test.tsx` (2), `web/src/features/quiz/pages/QuizPage.askTeacher.test.tsx` (1) | Entry-point tests |

### Docs
| Path | Purpose |
|---|---|
| `docs/ask-teacher.md` | Model, context, quota, image (private access), SLA, API, web, later stories, limits |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | `// TEACHER THREADS` group before `// SUBSCRIPTIONS` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | `// ASK A TEACHER` group (11 codes) before `// ANALYTICS` |
| `api/Elmanhg.Domain/Sessions/ISessionRepository.cs`, `api/Elmanhg.Infrastructure/Sessions/SessionRepository.cs` | `GetStudentAttemptAsync` |
| `api/Elmanhg.Application/Subscriptions/Shared/UsageResult.cs`, `GetMyUsage/GetMyUsageHandler.cs` | 3 Ask a Teacher counters |
| `api/Elmanhg.Application/DependencyInjection.cs` | `AskTeacherOptions` with `ValidateOnStart` |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `ITeacherThreadRepository` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | DbSets, `ConfigureTeacherThreads` (+ filtered `ImageUrl` index), soft-delete filters |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | 12 codes after `SUBJECT_INTERESTS_DUPLICATE` |
| `api/Elmanhg.Api/appsettings.example.json` (and the worktree's gitignored `appsettings.json`) | `AskTeacher` section |
| `api/Elmanhg.Api/FileStorage/LocalFileStorageExtensions.cs` | **Plan-gate**: mounts `TeacherThreadMediaMiddleware` before the static files and swaps in `PublicMediaFileProvider` |
| `api/openapi/v1.json` | Regenerated by `dotnet build` |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/GetMyUsage/GetMyUsageHandlerTests.cs` | New repo substitute, `UsageResult(…, 0, 0, 0)`, +2 tests |
| `api/Elmanhg.Tests/Integration/Subscriptions/UsageEndpointTests.cs` | +1 test |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | `twentyFifth => _AddTeacherThreads` |
| `postman/elmanhg.postman_collection.json` | `AskTeacher` folder (4 requests, plan order) after `Subscriptions`; variable `teacherThreadId` |
| `web/src/routes/student/ask.tsx` | Placeholder → `AskTeacherListPage` + search schema |
| `web/src/features/browse/pages/LessonPage.tsx` | `AskTeacherLink lessonId` after `<Outlet />` (unlocked only) |
| `web/src/features/quiz/components/FeedbackPanel.tsx`, `QuizQuestionCard.tsx` | `children` slot; `AskTeacherLink attemptId` |
| `web/src/features/content/index.ts` | `ContentEmptyState` export |
| `web/src/app/i18n.ts` | `askTeacher` resources (+ the `ns` list) |
| `web/src/shared/i18n/ar.json`, `en.json` | 12 error codes |
| `web/src/test/subscriptionFixtures.ts` | 3 zero counters on `freeUsage`/`baseUsage` |
| `web/orval.config.ts` | `GetMyTeacherThreads` zod query generation off (see Deviations) |
| `web/src/shared/api/generated/**`, `web/src/routeTree.gen.ts` | Regenerated |
| `docs/PRD.md` | §12.1 (attempt context, calendar-month quota, one photo, photos private), §15 entities, §20 context bundle |
| `docs/subscriptions.md` | usage row, new `## Ask a Teacher quota`, "For later stories" #94 done |
| `docs/claude-design-prompt.md` | §4 lesson, quiz and ask bullets |
| `docs/prototype.md` | Walkthrough item 7 |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Decision 9 / D1: thread photos are public capability URLs under `/api/media` | The plan-gate condition in `00-acceptance.md` overrides it: photos must never be public; only the owning student, a teacher scoped to the thread's subject or an admin; everyone else 404 | Added `CanViewTeacherThreadImageQuery`/`Handler` (Application), `TeacherThreadMediaMiddleware` + `PublicMediaFileProvider` (Api) and changed `LocalFileStorageExtensions` (not in the touched list). Anonymous, other students, unassigned teachers, unknown files and every non-canonical spelling (`//`, case, trailing dot, `%5C`, `..%5C`, `~`) get an empty 404. Added a filtered index on `TeacherMessages.ImageUrl` for the lookup. Tests: 7 handler tests + `TeacherThreadMediaEndpointTests` (7 methods / 11 cases). Docs describe private photos instead of capability URLs. |
| `ThreadMessage` renders `<img src={message.imageUrl}>` | An `<img>` cannot send the bearer token, so a private URL would always 404 | New `components/ThreadImage.tsx` fetches through the `http` mutator and renders a `data:image/…;base64` URL (rejects a non-image content type). Two extra locale keys: `thread.imageLoading`, `thread.imageError`. |
| `AskTeacherGate.MonthStart` builds `new DateTime(…, Unspecified)` | `DateTime` is prohibited by the non-negotiables | Same result with `DateTimeOffset` only: wall clock as `+00:00`, offset resolved in two steps via `TimeZoneInfo.GetUtcOffset(DateTimeOffset)`. The WHY comment is kept. Plan tests 8/9 pass unchanged. |
| Handler key literal `teacher-threads/…` | The folder name is now shared with the media guard | `TeacherThreadImageFormats.StorageFolder` constant used by the handler, middleware and provider. |
| Orval regenerate only | The generated zod for `GetMyTeacherThreads` query params (`.default(1)` on a string format) does not type-check — same problem `GetMyPayments` already works around | Added `GetMyTeacherThreads: { zod: { generate: { query: false } } }` to `web/orval.config.ts`. |
| `i18n.ts`: resources entries only | Every namespace is also listed in `ns` | Added `'askTeacher'` to `ns` as well. |
| `<Form onSubmit={submit}>`; `submit` sends `data` "dropping undefined keys" | `Form.onSubmit` expects `Promise<void>`; `exactOptionalPropertyTypes` forbids `undefined` keys | Wrapped `onSubmit` in an async arrow; body built with conditional spreads. |
| Test 19: revisions "v1 Old stem" | The builder's stem is fixed | v1 is the builder's `<p>2 + 2 = ?</p>`; the question is then edited to v2 `<p>New stem</p>`; asserts the v1 stem, version 1 and attempt id. |
| `TeacherThreadTestData.SeedThreadsAsync` return type unspecified | Tests need the ids | Returns `List<TeacherThread>`; also exposes `PngBytes`. |
| `CreateTeacherThreadEndpointTests` uses `Route` | `using static SessionTestData` also defines `Route` (ambiguity) | Uses `TeacherThreadTestData.Route` explicitly. |

## Build & test
- `dotnet build` (api): **Build succeeded**, 9 warnings, all pre-existing in `core-libraries`; 0 new.
- `dotnet ef migrations add AddTeacherThreads` → `20260929132737_AddTeacherThreads` (CreateTable ×2, FKs, indexes; no Drop/Rename).
- CI parity: `appsettings.json` moved aside, `dotnet test -c Release` → **Passed! total 2712, failed 0, succeeded 2712, skipped 0** (48.9 s); `appsettings.json` restored.
- `dotnet format Elmanhg.slnx --verify-no-changes --exclude core-libraries` → only `Builders/SubscriptionBuilder.cs(57)` whitespace (pre-existing CRLF noise, file untouched).
- `npm ci` then `npm run gen:api` (no further drift on re-run), `npx vite build` (route tree).
- `npm run typecheck` → clean. `npm run lint` → clean. `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → "All matched files use Prettier code style!".
- `npx vitest run` → **Test Files 149 passed, Tests 878 passed**.
- New tests: API 94 methods (+2 modified-file tests, +1 integration), web 44.
- Mutation checks (break → test fails → restore): resolver revision version (test 19 failed), `GetMyUsage` `Math.Max` (over-limit test failed), `TeacherThread` µs truncation (test 3 failed), `FeedbackPanel` children slot and `attemptId` in the create body (web tests failed). The auto-mode classifier refused the security mutations (the public-media filter and the quota window); verified by reading instead: without `PublicMediaFileProvider`'s filter, `PhysicalFileProvider` serves `//teacher-threads/x`, `teacher-threads./x` (Windows) and `%5C` paths, which the 5 `Get_NonCanonicalPathAnonymously_Returns404` cases request; without the handler's role/assignment checks the `Other student`/`Teacher outside` tests return 200. `AskTeacherGateTests` now seeds a previous-month, a next-month and a foreign thread so dropping either month bound or the student filter changes the count.

## Notes for review
- Media middleware sits before `UseAuthorization`; it relies on the automatically inserted authentication middleware (`WebApplication` adds `UseAuthentication` first), which the owner/teacher/admin integration tests confirm. Anonymous callers get 404 (not 401) per the gate, so the web cannot refresh a token on a photo request; the photo query retries on the next page load.
- `PublicMediaFileProvider` also rejects any subpath containing `~` (Windows 8.3 aliases) and exposes no directory listings.
- `CanViewTeacherThreadImageHandler` returns `bool` (no exception) so the middleware can answer with an indistinguishable empty 404; `CoreExceptionMiddleware` runs after the static-file stage.
- A deactivated teacher/assignment check is whatever `ITeacherSubjectRepository.IsAssignedAsync` returns (soft-deleted assignments are filtered).
- D1 (S3 adapter) stays deferred to #96 for the orchestrator's `deferred` issue; with S3 the private bucket + presigned URLs should replace the guarded Local path.
- The data-URL photo rendering keeps the base64 conversion chunked (32 KB) to avoid the spread-argument limit on 5 MB photos.
