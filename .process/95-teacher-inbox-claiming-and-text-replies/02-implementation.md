# Implementation — [E9.S2] Teacher inbox, claiming and text replies (#95)

Worktree `D:\Personal\elmanhg-wt\95`, branch `feature/95-teacher-inbox-claiming-and-text-replies`. Nothing committed.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/TeacherThreads/TeacherThread.Replies.cs | 66 | `Claim`, `Reply`, `MarkRepliesRead` (+ the planned WHY comment), private `EnsureClaimedBy` |
| api/Elmanhg.Infrastructure/Migrations/20260929150401_AddTeacherThreadClaims.cs (+ .Designer.cs) | 70 (+2370 generated) | 3 nullable columns, `IX_TeacherThreads_TeacherId`, FK `FK_TeacherThreads_AspNetUsers_TeacherId` (Restrict). No drops/renames |
| api/Elmanhg.Application/TeacherThreads/MarkTeacherThreadRead/MarkTeacherThreadReadCommand.cs | 5 | command |
| api/Elmanhg.Application/TeacherThreads/MarkTeacherThreadRead/MarkTeacherThreadReadHandler.cs | 25 | owner-scoped tracked load, `MarkRepliesRead`, one save |
| api/Elmanhg.Application/TeacherInbox/Shared/TeacherInboxFilter.cs | 3 | `All, Unclaimed, Mine` |
| api/Elmanhg.Application/TeacherInbox/Shared/TeacherInboxItemResult.cs | 5 | list item result |
| api/Elmanhg.Application/TeacherInbox/Shared/TeacherInboxThreadResult.cs | 6 | thread result (`CanClaim`, `CanReply`, …) |
| api/Elmanhg.Application/TeacherInbox/Shared/TeacherInboxResultGenerator.cs | 24 | `GenerateItem`, `GenerateThread` |
| api/Elmanhg.Application/TeacherInbox/Shared/TeacherInboxAccess.cs | 22 | admin bypass, else `IsAssignedAsync` → 403 `SUBJECT_OUT_OF_SCOPE` |
| api/Elmanhg.Application/TeacherInbox/Shared/TeacherInboxNames.cs | 18 | one user query for student + claimer display names |
| api/Elmanhg.Application/TeacherInbox/Shared/TeacherInboxQueryShape.cs | 20 | scope/filter predicate + Open-first order |
| api/Elmanhg.Application/TeacherInbox/GetTeacherInbox/GetTeacherInboxQuery.cs | 7 | query |
| api/Elmanhg.Application/TeacherInbox/GetTeacherInbox/GetTeacherInboxValidator.cs | 19 | filter enum, page number, page size |
| api/Elmanhg.Application/TeacherInbox/GetTeacherInbox/GetTeacherInboxHandler.cs | 50 | scoped paged inbox |
| api/Elmanhg.Application/TeacherInbox/GetInboxThread/GetInboxThreadQuery.cs | 6 | query |
| api/Elmanhg.Application/TeacherInbox/GetInboxThread/GetInboxThreadHandler.cs | 30 | by id + access check |
| api/Elmanhg.Application/TeacherInbox/ClaimTeacherThread/ClaimTeacherThreadCommand.cs | 6 | command |
| api/Elmanhg.Application/TeacherInbox/ClaimTeacherThread/ClaimTeacherThreadHandler.cs | 35 | tracked load, access, `Claim`, one save |
| api/Elmanhg.Application/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadCommand.cs | 6 | command |
| api/Elmanhg.Application/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadValidator.cs | 20 | required + `ReplyTextMaxLength` |
| api/Elmanhg.Application/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadHandler.cs | 35 | tracked load, access, `Reply`, one save |
| api/Elmanhg.Api/Controllers/TeacherInbox/TeacherInboxController.cs | 54 | 4 actions, `DefaultCodes.AskTeacherReply` |
| api/Elmanhg.Api/Controllers/TeacherInbox/Requests.cs | 3 | `ReplyToTeacherThreadRequest` |
| api/Elmanhg.Tests/Domain/TeacherThreads/TeacherThreadClaimAndReplyTests.cs | 140 | T1–T12 |
| api/Elmanhg.Tests/Application/Features/TeacherInbox/Shared/TeacherInboxQueryShapeTests.cs | 88 | T13–T18 |
| api/Elmanhg.Tests/Application/Features/TeacherInbox/Shared/TeacherInboxResultGeneratorTests.cs | 62 | T19–T22 |
| api/Elmanhg.Tests/Application/Features/TeacherInbox/GetTeacherInbox/GetTeacherInboxHandlerTests.cs | 90 | T23–T25 |
| api/Elmanhg.Tests/Application/Features/TeacherInbox/GetTeacherInbox/GetTeacherInboxValidatorTests.cs | 45 | T26 |
| api/Elmanhg.Tests/Application/Features/TeacherInbox/GetInboxThread/GetInboxThreadHandlerTests.cs | 93 | T27 |
| api/Elmanhg.Tests/Application/Features/TeacherInbox/ClaimTeacherThread/ClaimTeacherThreadHandlerTests.cs | 106 | T28 |
| api/Elmanhg.Tests/Application/Features/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadHandlerTests.cs | 109 | T29 |
| api/Elmanhg.Tests/Application/Features/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadValidatorTests.cs | 47 | T32 |
| api/Elmanhg.Tests/Application/Features/TeacherThreads/MarkTeacherThreadRead/MarkTeacherThreadReadHandlerTests.cs | 67 | T33 |
| api/Elmanhg.Tests/Integration/TeacherInbox/TeacherInboxTestData.cs | 41 | helpers per plan #34 |
| api/Elmanhg.Tests/Integration/TeacherInbox/TeacherInboxEndpointTests.cs | 144 | T34–T42 |
| api/Elmanhg.Tests/Integration/TeacherInbox/ClaimTeacherThreadEndpointTests.cs | 100 | T43–T47 |
| api/Elmanhg.Tests/Integration/TeacherInbox/ReplyToTeacherThreadEndpointTests.cs | 99 | T48–T52 |
| api/Elmanhg.Tests/Integration/TeacherThreads/MarkTeacherThreadReadEndpointTests.cs | 67 | T53–T55 |
| api/Elmanhg.Tests/Integration/Persistence/TeacherThreadPersistenceTests.cs | 62 | T56–T57 |
| web/src/routes/teacher/thread.$threadId.tsx | 11 | route → `InboxThreadPage` |
| web/src/features/askTeacher/schemas/teacherInboxSearchSchema.ts | 8 | `?filter=&page=` |
| web/src/features/askTeacher/schemas/replyFormSchema.ts | 9 | reply form schema |
| web/src/features/askTeacher/hooks/useTeacherInboxSearch.ts | 21 | URL state; tab change clears `page` |
| web/src/features/askTeacher/hooks/useTeacherInbox.ts | 12 | `useGetTeacherInbox` + keepPreviousData |
| web/src/features/askTeacher/hooks/useClaimThread.ts | 36 | claim; toast; refetch on error |
| web/src/features/askTeacher/hooks/useReplyToThread.ts | 34 | reply; toast; refetch on 409 |
| web/src/features/askTeacher/hooks/useMarkThreadRead.ts | 28 | background read receipt (`useEffectEvent`) |
| web/src/features/askTeacher/components/InboxFilterTabs.tsx | 40 | pill tabs (`aria-pressed`) |
| web/src/features/askTeacher/components/InboxListItem.tsx | 47 | item link |
| web/src/features/askTeacher/components/InboxList.tsx | 45 | loading / error / empty / list / pagination |
| web/src/features/askTeacher/components/ReplyForm.tsx | 46 | RHF + zod, server field mapping |
| web/src/features/askTeacher/components/InboxThreadActions.tsx | 35 | claim button / reply form / note |
| web/src/features/askTeacher/pages/TeacherInboxPage.tsx | 17 | page |
| web/src/features/askTeacher/pages/InboxThreadPage.tsx | 70 | page |
| web/src/test/teacherInboxFixtures.ts | 78 | fixtures |
| web/src/features/askTeacher/schemas/teacherInboxSearchSchema.test.ts | 18 | W1–W3 |
| web/src/features/askTeacher/schemas/replyFormSchema.test.ts | 14 | W4–W5 |
| web/src/features/askTeacher/pages/TeacherInboxPage.test.tsx | 138 | W6–W12 |
| web/src/features/askTeacher/pages/InboxThreadPage.test.tsx | 167 | W13–W19, W22 |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/TeacherThreads/TeacherThread.cs | `partial`; `TeacherId`, `ClaimedAt`; `IsClaimedBy`, `HasUnreadReply()` |
| api/Elmanhg.Domain/TeacherThreads/TeacherMessage.cs | `StudentReadAt`; internal `MarkReadByStudent` (`??=`) |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | 3 domain codes |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | 4 application codes |
| api/Elmanhg.Application/Shared/Options/AskTeacherOptions.cs | `ReplyTextMaxLength` 4000 `[Range(1, 20000)]` |
| api/Elmanhg.Application/TeacherThreads/Shared/TeacherThreadResult.cs, TeacherThreadSummaryResult.cs | trailing `bool HasUnreadReply` |
| api/Elmanhg.Application/TeacherThreads/Shared/TeacherThreadResultGenerator.cs | public `QuestionText`, `GenerateMessages`; passes `HasUnreadReply()` |
| api/Elmanhg.Api/Controllers/TeacherThreads/TeacherThreadsController.cs | `POST {threadId}/read` (`MarkTeacherThreadRead`) |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | `TeacherThread` concurrency catch → 409 `TEACHER_THREAD_MODIFIED_CONCURRENTLY`; `TeacherId` FK (Restrict) |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated |
| api/Elmanhg.Api/Resources/Messages.ar.resx, Messages.en.resx | 7 keys |
| api/Elmanhg.Api/appsettings.example.json, deploy/api.env.example, docs/deployment.md | `ReplyTextMaxLength` 4000 |
| api/openapi/v1.json | regenerated by `dotnet build` |
| api/Elmanhg.Tests/Builders/TeacherThreadBuilder.cs | `ClaimedBy`, `AnsweredBy` |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `twentySeventh` → `_AddTeacherThreadClaims` |
| api/Elmanhg.Tests/…/GetMyTeacherThreadsHandlerTests.cs, GetMyTeacherThreadHandlerTests.cs | T30, T31 added (existing tests unchanged) |
| postman/elmanhg.postman_collection.json | AskTeacher "Mark my thread read"; new `TeacherInbox` folder (5 requests, in plan order); variable `inboxThreadId` |
| web/orval.config.ts | `GetTeacherInbox` zod query override |
| web/src/shared/api/generated/**, web/src/routeTree.gen.ts | regenerated (`gen:api`, `vite build`) |
| web/src/routes/teacher/inbox.tsx | placeholder → `TeacherInboxPage` + `validateSearch` |
| web/src/features/askTeacher/index.ts | exports `TeacherInboxPage`, `InboxThreadPage`, `teacherInboxSearchSchema` |
| web/src/features/askTeacher/components/ThreadContextCard.tsx | `Pick<>` prop, optional `children` |
| web/src/features/askTeacher/components/ThreadMessage.tsx | optional `authorLabel` |
| web/src/features/askTeacher/components/ThreadListItem.tsx | «رد جديد» pill |
| web/src/features/askTeacher/pages/TeacherThreadPage.tsx | `useMarkThreadRead(threadId, data?.hasUnreadReply === true)` |
| web/src/features/askTeacher/i18n/ar.json, en.json; web/src/shared/i18n/ar.json, en.json | plan copy + 7 error strings |
| web/src/test/askTeacherFixtures.ts | `hasUnreadReply: false` defaults |
| web/src/features/askTeacher/pages/AskTeacherListPage.test.tsx, TeacherThreadPage.test.tsx | W20, W21 added (existing tests unchanged) |
| docs/ask-teacher.md, docs/PRD.md (§15), docs/claude-design-prompt.md (§4), docs/prototype.md | as listed in the plan's Docs section |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| T37 `GetInbox_UnknownFilter_Returns422`: `?filter=99` → 422 `TEACHER_INBOX_FILTER_INVALID` | ASP.NET Core's enum model binder rejects an undefined enum value before MediatR runs: the API returns `400` ValidationProblemDetails (`errors.filter`), never 422. Same for `?filter=Bogus`. | Kept the validator rule (unit-tested by T26 for direct MediatR callers). Renamed the integration test to `GetInbox_UnknownFilter_Returns400` and asserted `400` + `errors.filter`. `docs/ask-teacher.md` says "an unknown value is refused by model binding with 400". The error code and its resx/web strings still ship, as the plan listed. |
| T48 read the reply as "the second message" | Builder threads are submitted at a fixed future date (2026-10-01), while the HTTP reply uses the real clock, so ordering by `CreatedAt` puts the reply first. | Asserted the teacher's message by `SenderId` instead of position. Behaviour unchanged. |
| Migration FK name `FK_TeacherThreads_Users_TeacherId` | The identity table is `AspNetUsers`. | Generated name `FK_TeacherThreads_AspNetUsers_TeacherId`. |
| Plan line refs "prototype.md line 36/38" | The teacher and student bullets are items 7 and 9. | Edited those bullets with the plan's text. |
| T28: "`ClaimedAt` = FakeTimeProvider now" | Sibling handler tests substitute `TimeProvider` with NSubstitute. | Followed the sibling pattern (`Substitute.For<TimeProvider>()`). |

## Build & test
- `dotnet build api/`: 0 errors. The only warnings are 9 existing ones in `core-libraries`. `api/openapi/v1.json` was regenerated.
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside (restored afterwards): **Passed. total 2917, failed 0, succeeded 2917, skipped 0.**
- New API tests: 12 domain, 43 application (T13–T29, T32, T33; the T32 Theory counts as 2 cases), 2 added to existing handler tests (T30, T31), 24 integration (T34–T57), plus the migration-list update. Total 81 new test cases.
- `npm --prefix web run typecheck`: clean. `npm --prefix web run lint`: clean. `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: "All matched files use Prettier code style!"
- `npm --prefix web test -- --run`: **153 files, 902 tests passed.** 24 of them are new (W1–W22; W12 and W19 have two `it`s each).
- Orval: running `gen:api` a second time gives an identical tree (same md5).
- Guard grep over the `.cs` diff prints nothing. The only added comment is the planned `MarkRepliesRead` WHY line.
- `dotnet format --verify-no-changes --exclude api/core-libraries` flags one whitespace issue in `Elmanhg.Tests/Builders/SubscriptionBuilder.cs:57`. I did not touch that file; it is the known local CRLF noise.
- Mutation checks:
  - T57: stamping `UpdationDate` in `MarkRepliesRead` made it fail.
  - T56 and T45: pointing the `AppDbContext` concurrency catch at `TeacherMessage` made both fail.
  - W15: removing the thread refetch on claim error made it fail.
  - W21: passing `false` to `useMarkThreadRead` made it fail.
  - All mutations were restored.
  - T3 and T5: the auto-mode classifier refused the claim-guard mutations as "Security Weaken". I did not retry them and verified by reading instead. T3 asserts the exact `TEACHER_THREAD_ALREADY_CLAIMED` code and that `TeacherId` is unchanged. T5 asserts `TEACHER_THREAD_NOT_CLAIMED`: without the null guard, `EnsureClaimedBy` would throw `ALREADY_CLAIMED`, so T5 would fail.

## Notes for review
- T45 (concurrent claims) goes through the real HTTP pipeline with `Task.WhenAll` over two `HttpClient`s. Each request has its own DI scope and DbContext, so the skill's rule against `Task.WhenAll` over one context does not apply. The mutation run showed it really hits the xmin race path.
- `GetInbox_UnknownFilter` now returns 400 (see Deviations). If the dev wants a 422 with a code, the filter must be bound as a string or through a custom binder. That changes the OpenAPI/Orval contract, so I did not do it.
- The reply form's English hint and required message are both "Write your reply" (plan copy). W17 therefore asserts `aria-invalid` plus two occurrences of the text.
- `TeacherInboxQueryShape.Filter` captures `subjectIds` and `filter` in the expression. EF evaluates the null and enum checks as parameters, so only `SubjectId IN (…)` / `TeacherId` predicates reach SQL. The integration tests cover the scope, the Mine filter and the ordering on PostgreSQL.
- Migration `20260929150401_AddTeacherThreadClaims` is currently the newest. If lane #91 (`AddAvatarMessageUsages`) merges first, re-create it after merging `origin/main` and move its `AppDbContextTests` entry after #91's (plan D20).
- Out-of-band reply notifications (WhatsApp/Email) are out of scope per the plan; suggest a follow-up issue.
