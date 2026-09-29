# Plan — [E9.S2] Teacher inbox, claiming and text replies (#95)

## Goal
A teacher opens «أسئلة الطلاب» (`/teacher/inbox`) and sees the Ask a Teacher threads of the subjects they are assigned to. Each thread shows its question, subject / lesson, the student's display name, who claimed it, and the SLA badge («بانتظار الرد · متبقٍ N ساعة», «متأخر», «تم الرد», «مغلق»). Pill tabs switch between الكل / غير مُستلمة / الخاصة بي. The teacher opens a thread (`/teacher/thread/$threadId`), claims it with «استلام السؤال» (first claim wins; a concurrent loser gets 409), and replies in text. The reply moves the thread to `Answered`. The student is notified in-app: the thread shows «رد جديد» in their list until they open it. Each Q&A is kept as text in `TeacherMessage` rows, the source that #109 will turn into training records.

## Scope
**In:**
- Domain: `TeacherThread.TeacherId`, `ClaimedAt`, `Claim`, `Reply`, `MarkRepliesRead`, `HasUnreadReply`; `TeacherMessage.StudentReadAt`. Migration `AddTeacherThreadClaims`. `DbUpdateConcurrencyException` on `TeacherThread` maps to 409 `TEACHER_THREAD_MODIFIED_CONCURRENTLY`.
- Teacher API (new `TeacherInboxController`, policy `AskTeacher.Reply`): the paged inbox, the thread view, claim, and a text reply.
- Student API: `POST /api/teacher-threads/{threadId}/read`, plus `hasUnreadReply` on the student list and thread results.
- Web: `/teacher/inbox` (replaces the placeholder), `/teacher/thread/$threadId`, the «رد جديد» pill on the student list, and mark-read when the student opens a thread.
- Docs (`docs/ask-teacher.md`, PRD §15, `docs/claude-design-prompt.md` §4, `docs/prototype.md`, `docs/deployment.md`), config examples, Postman, OpenAPI, Orval.

**Out** (owned by other stories, not deferred):
- #96: voice replies, transcript editing, the S3 adapter. The reply form is text only.
- #97 (per `docs/backlog.json`): the student follow-up command, closing after the second reply, the 1–5 rating (`ClosedAt`, `Rating`), 12h/20h reminders, the breach alert, and realtime SignalR push of replies.
- #107: the teacher stats card (`/teacher/stats` stays a placeholder).
- #105/#106: an admin inbox UI. Admin API access ships now (PRD §16).
- Out-of-band reply notifications (WhatsApp/Email through the #171 channels): not asked by the story. A WhatsApp business-initiated message needs a separately approved Meta utility template. Suggest a follow-up issue.
- Unclaim / reassign: not in PRD §12.

**Deferred:** none. Nothing in this story needs credentials or an unavailable service.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Follow-up and rating in #95? | No. #95 ships claim + reply (`Open → Answered`) only. `Reply` is written so #97's follow-up (`Answered → Open`) reuses it unchanged. | `docs/backlog.json` lists "Follow-up command…" and "Rating command 1 to 5" under #97. `docs/ask-teacher.md` "For later stories" is corrected: `ClosedAt`/`Rating` move to #97. |
| 2 | Student notification: which channel? | In-app only. `TeacherMessage.StudentReadAt` (null = unread). `TeacherThread.HasUnreadReply()` = any non-student message with `StudentReadAt == null`. The student list shows «رد جديد», and opening the thread calls `POST /api/teacher-threads/{id}/read`. | The story says "student notification" and names no channel. #97 adds realtime push. The #171 channels are OTP-specific (the WhatsApp template is an OTP auth template), and the orchestrator allowed them only if the story asks. |
| 3 | Where is "read" stored? | On the teacher's `TeacherMessage` rows, not on the thread. `MarkRepliesRead` touches only message rows and leaves the thread's `UpdationDate` alone, so the thread's xmin never changes on a read. | A thread-level column would bump xmin, and a student opening the thread could then make a teacher's in-flight reply fail with 409. Pinned by a persistence test. |
| 4 | Who may reply? | Only the claiming user. Unclaimed → 409 `TEACHER_THREAD_NOT_CLAIMED`. Claimed by someone else → 409 `TEACHER_THREAD_ALREADY_CLAIMED`. Status ≠ `Open` → 409 `TEACHER_THREAD_NOT_AWAITING_REPLY`. The claim check runs before the status check. | Prototype `vTeacherThread`: claim first, then reply. "First teacher to claim it owns it" (PRD §12.1). |
| 5 | Claiming twice | Self-claim is idempotent: no change, and `ClaimedAt` keeps the first value. Claiming a thread owned by another user → 409 `TEACHER_THREAD_ALREADY_CLAIMED`. | A double click must not fail. |
| 6 | Concurrent claims | xmin (`TeacherThread.Version`, already `IsRowVersion`). The loser of a true race gets 409 `TEACHER_THREAD_MODIFIED_CONCURRENTLY` from `AppDbContext.SaveChangesAsync`. The loser of a sequential race gets 409 `TEACHER_THREAD_ALREADY_CLAIMED` from the domain. No handler try/catch. | Skill §6.8: the concurrency mapping lives in one place (existing `AppDbContext` catch pattern for Session/Payment/Subscription). The web treats both codes the same way: toast plus a refetch that shows the new owner. |
| 7 | Subject scoping | List: filter by the caller's `TeacherSubject` ids. No assignment means an empty list (fail closed). By id: `TeacherInboxAccess.EnsureCanAccessAsync` → 403 `SUBJECT_OUT_OF_SCOPE` when the teacher is not assigned. `SubjectScopeBehaviour` is not used because it needs a `SubjectId` on the request, and thread routes carry only `threadId`. | Same pattern as #68 (`GetValidationQueueHandler`, `GetValidationQuestionHandler`, `ApproveQuestionHandler`). |
| 8 | Admin | Policy `AskTeacher.Reply` = Teacher + Admin (exists). Admin skips the assignment check (sees every subject) and may claim and reply like a teacher. No admin web UI in this story. | PRD §16: "Reply to Ask a Teacher: Admin ✓". The web `/teacher/*` routes are teacher-only. |
| 9 | Unassigned after claiming | A teacher who loses the subject assignment loses access to their claimed threads (list and by id), which fails closed. Listed under Known limits. | Scope is re-checked on every request. |
| 10 | Inbox filters and order | `TeacherInboxFilter { All, Unclaimed, Mine }` (query `filter`, default `All`). Order: `Open` first, then `SlaDueAt` ascending, then `Id`. | Prototype `vTeacherInbox` (the tabs; sorted by `slaDueAt`). Open-first keeps the work queue on page 1. |
| 11 | Inbox paging | `pageNumber`/`pageSize`, max `AskTeacher:ThreadListMaxPageSize` (50). Reuses `TEACHER_THREAD_PAGE_NUMBER_INVALID` / `_PAGE_SIZE_INVALID`. | Same cap and codes as the student list. No new option. |
| 12 | Identity shown to the teacher | The student's `User.DisplayName` only. The claimer's display name is shown too. | PRD §8.4, §14 (privacy). |
| 13 | Reply length | New `AskTeacherOptions.ReplyTextMaxLength` = 4000 (`[Range(1, 20000)]`). Codes `TEACHER_THREAD_REPLY_TEXT_REQUIRED` / `_TOO_LONG` (422). | Caps in Options (skill §8.1). Replies explain more than questions. |
| 14 | Auditing | Claim and reply are not audited (no `IAuditableCommand`). | `docs/ask-teacher.md`: "Threads are not audited". `ClaimedAt`, `TeacherId` and the message rows are the record. |
| 15 | Training data | Nothing new is persisted. The question text, the context snapshot (ids) and the teacher's final reply text already live in `TeacherThread.Context` and the `TeacherMessage` rows. #109/#110 build the append-only records from them. | Orchestrator: "persist only what this story needs". |
| 16 | Timestamps | `ClaimedAt`, reply `CreatedAt` and `StudentReadAt` are truncated to microseconds with the existing private `ToMicroseconds`. | Same reason as `Submit` (#94 decision 13). |
| 17 | Result shape for the teacher | Server-computed `IsClaimedByMe`, `CanClaim` (`TeacherId == null && Status != Closed`) and `CanReply` (`TeacherId == caller && Status == Open`). The web holds no rules. | Same as `isOverdue` (#94). |
| 18 | Web placement | Teacher pages live in `features/askTeacher` (same feature, shared components). The student page `TeacherThreadPage` keeps its name. The teacher pages are `TeacherInboxPage` / `InboxThreadPage`. `ThreadMessage` gains an optional `authorLabel`. `ThreadContextCard` accepts a `Pick<>` so both result types fit. | Reuses badge, card, message and image components. The photo already loads for assigned teachers (`CanViewTeacherThreadImageQuery`). |
| 19 | Inbox URL state | Search `?filter=Unclaimed|Mine&page=N`. No `filter` means All. Changing the tab clears `page`. | The enum values go straight to the API. Same URL-state pattern as the validation queue and the student list. |
| 20 | Migration collisions | Migration `AddTeacherThreadClaims` must be the newest. If lane #91 (`AddAvatarMessageUsages`) merges first, re-create this migration after merging `origin/main` so it sorts last, and append its entry after #91's in `AppDbContextTests`. | PROGRESS "Parallel lanes". |

Morabh reuse: none applies. Morabh has no inbox/claim/ticket or message-thread feature (searched `Morabh.Domain`/`Morabh.Application` for claim/inbox/ticket/assign). Everything below is **new — no Morabh equivalent**. It is built on vendored `Core.DDD` (`IRepository.FindPaginatedAsync`, `PageData`), `Core.Errors`, `Core.Validation`. `Core.Notifications` was considered and rejected: it is Firebase-bound and not wired in Elmanhg.

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.cs` | `public partial class TeacherThread`. Add `public Guid? TeacherId { get; private set; }` and `public DateTimeOffset? ClaimedAt { get; private set; }` after `SlaDueAt`. Add `public bool IsClaimedBy(Guid userId) => TeacherId == userId;` and `public bool HasUnreadReply() => Messages.Any(x => x.SenderId != StudentId && x.StudentReadAt == null);`. |
| `api/Elmanhg.Domain/TeacherThreads/TeacherMessage.cs` | Add `public DateTimeOffset? StudentReadAt { get; private set; }` and `internal void MarkReadByStudent(DateTimeOffset readAt) => StudentReadAt ??= readAt;`. |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | In `// TEACHER THREADS` after `TeacherMessageTextRequired`: `TeacherThreadAlreadyClaimed = "TEACHER_THREAD_ALREADY_CLAIMED"`, `TeacherThreadNotClaimed = "TEACHER_THREAD_NOT_CLAIMED"`, `TeacherThreadNotAwaitingReply = "TEACHER_THREAD_NOT_AWAITING_REPLY"`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | In `// ASK A TEACHER` after `TeacherThreadPageSizeInvalid`: `TeacherThreadModifiedConcurrently`, `TeacherThreadReplyTextRequired`, `TeacherThreadReplyTextTooLong`, `TeacherInboxFilterInvalid` (values in "Error codes"). |
| `api/Elmanhg.Application/Shared/Options/AskTeacherOptions.cs` | Add `[Range(1, 20000)] public int ReplyTextMaxLength { get; set; } = 4000;` |
| `api/Elmanhg.Application/TeacherThreads/Shared/TeacherThreadResult.cs` | Append the last positional param `bool HasUnreadReply`. |
| `api/Elmanhg.Application/TeacherThreads/Shared/TeacherThreadSummaryResult.cs` | Append the last positional param `bool HasUnreadReply`. |
| `api/Elmanhg.Application/TeacherThreads/Shared/TeacherThreadResultGenerator.cs` | `Generate` passes `thread.HasUnreadReply()`; `GenerateSummary` passes `thread.HasUnreadReply()` and uses `QuestionText(thread)`. Extract `public static string QuestionText(TeacherThread thread)` (the first student message's text: the existing `Where/OrderBy/ThenBy/First` chain). Make `public static List<TeacherMessageResult> GenerateMessages(TeacherThread thread)` (the existing ordered select) and use it in `Generate`. |
| `api/Elmanhg.Api/Controllers/TeacherThreads/TeacherThreadsController.cs` | Add the `MarkTeacherThreadRead` action (see API surface). |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | (a) In `SaveChangesAsync`, after the `Subscription` catch: `catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(x => x.Entity is TeacherThread)) { throw new ConflictCoreException(ErrorCodes.TeacherThreadModifiedConcurrently, innerException: exception); }`. (b) In `ConfigureTeacherThreads` → `TeacherThread`, after the `StudentId` FK: `builder.HasOne<User>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add AddTeacherThreadClaims`. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Seven new keys after `TEACHER_THREAD_PAGE_SIZE_INVALID` (strings in "Error codes"). |
| `api/Elmanhg.Api/appsettings.example.json` | `"AskTeacher"`: add `"ReplyTextMaxLength": 4000`. |
| `deploy/api.env.example` | After `# AskTeacher__ThreadListMaxPageSize=50`: `# AskTeacher__ReplyTextMaxLength=4000`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Builders/TeacherThreadBuilder.cs` | Add `_claimedBy` (Guid?) and `_answered` (bool). `public TeacherThreadBuilder ClaimedBy(Guid teacherId)` and `public TeacherThreadBuilder AnsweredBy(Guid teacherId)` (sets both). `Build()`: after `Submit`, if `_claimedBy` → `thread.Claim(id, _submittedAt.AddMinutes(10))`; if `_answered` → `thread.Reply(id, "Because force equals mass times acceleration.", _submittedAt.AddHours(1))`. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Migration list: append `twentySeventh => twentySeventh.Should().EndWith("_AddTeacherThreadClaims")` (after #91's entry if #91 merged first, see D20). |
| `api/Elmanhg.Tests/Application/Features/TeacherThreads/GetMyTeacherThreads/GetMyTeacherThreadsHandlerTests.cs` | Add one test (Test plan #T30). |
| `api/Elmanhg.Tests/Application/Features/TeacherThreads/GetMyTeacherThread/GetMyTeacherThreadHandlerTests.cs` | Add one test (#T31). |
| `postman/elmanhg.postman_collection.json` | `AskTeacher` folder: append "Mark my thread read" after "Get my thread". New folder `TeacherInbox` after `AskTeacher` (see API surface). |
| `web/orval.config.ts` | `apiZod…operations`: add `GetTeacherInbox: { zod: { generate: { query: false } } },` after `GetMyTeacherThreads`. |
| `web/src/shared/api/generated/**`, `web/src/routeTree.gen.ts` | Regenerated (`npm --prefix web run gen:api`; the router plugin during `npm run build`). |
| `web/src/routes/teacher/inbox.tsx` | Replace the placeholder: `validateSearch: teacherInboxSearchSchema`, `component: TeacherInboxPage`. |
| `web/src/features/askTeacher/index.ts` | Export `TeacherInboxPage`, `InboxThreadPage`, `teacherInboxSearchSchema`. |
| `web/src/features/askTeacher/components/ThreadContextCard.tsx` | Prop type → `thread: Pick<TeacherThreadResult, 'context' \| 'slaDueAt' \| 'status' \| 'isOverdue'>`; optional `children?: ReactNode`, rendered after the due/badge row. |
| `web/src/features/askTeacher/components/ThreadMessage.tsx` | Optional prop `authorLabel?: string`. When set it replaces the `thread.you`/`thread.teacher` text. |
| `web/src/features/askTeacher/components/ThreadListItem.tsx` | When `thread.hasUnreadReply`, render a pill `<span className="inline-flex rounded-pill bg-accent px-2.5 py-0.5 text-micro font-semibold text-surface">{t('badge.newReply')}</span>` before `ThreadStatusBadge` (wrap both in `flex shrink-0 flex-col items-end gap-1`). |
| `web/src/features/askTeacher/pages/TeacherThreadPage.tsx` | Call `useMarkThreadRead(threadId, data?.hasUnreadReply === true)` at the top (before any early return). |
| `web/src/features/askTeacher/i18n/ar.json`, `en.json` | Add keys (see "Web copy"). |
| `web/src/shared/i18n/ar.json`, `en.json` | `errors`: the seven new codes (same strings as the resx). |
| `web/src/test/askTeacherFixtures.ts` | `threadSummary` and `teacherThread` defaults gain `hasUnreadReply: false`. |
| `web/src/features/askTeacher/pages/AskTeacherListPage.test.tsx`, `TeacherThreadPage.test.tsx` | Add the tests listed as #W20 and #W21 (existing tests unchanged). |
| `docs/ask-teacher.md` | See "Docs". |
| `docs/PRD.md` | §15: `TeacherThread(… sla_due_at, claimed_at?, closed_at?, rating?)`; `TeacherMessage(… transcript_final bool, student_read_at?, created_at)`. |
| `docs/claude-design-prompt.md` | §4 Teacher: replace the `#/teacher/inbox` line (see "Docs"). §4 Student `#/student/ask` line: add «a thread with an unread teacher reply shows «رد جديد» until the student opens it». |
| `docs/prototype.md` | Line 38 (Teacher): after "…editable transcript." add "The product has the same three inbox tabs, shows the student's display name only, lets only the claiming teacher reply, and answers a lost claim race with «استلم معلم آخر هذا السؤال.»". Line 36 (student): add "The product marks a new reply «رد جديد» in the list until the student opens the thread." |
| `docs/deployment.md` | AskTeacher table: add the row `AskTeacher__ReplyTextMaxLength` \| `4000` \| 1 to 20000. |

## Files to create

### Domain
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/TeacherThreads/TeacherThread.Replies.cs` | partial class | `namespace Elmanhg.Domain.TeacherThreads; public partial class TeacherThread`. Methods `Claim`, `Reply`, `MarkRepliesRead`, private `EnsureClaimedBy`. Bodies in "Domain behaviour". |

### Infrastructure
| # | Path | Type | Contract |
|---|------|------|----------|
| 2 | `api/Elmanhg.Infrastructure/Migrations/<ts>_AddTeacherThreadClaims.cs` (+ `.Designer.cs`) | EF migration | Generated. Expected ops only: `AddColumn TeacherThreads.TeacherId uuid NULL`, `AddColumn TeacherThreads.ClaimedAt timestamptz NULL`, `AddColumn TeacherMessages.StudentReadAt timestamptz NULL`, `CreateIndex IX_TeacherThreads_TeacherId`, `AddForeignKey FK_TeacherThreads_Users_TeacherId` (Restrict). No drops or renames. |

### Application — student mark-read (`Elmanhg.Application.TeacherThreads.MarkTeacherThreadRead`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 3 | `…/TeacherThreads/MarkTeacherThreadRead/MarkTeacherThreadReadCommand.cs` | sealed record | `MarkTeacherThreadReadCommand(Guid ThreadId) : IRequest`. |
| 4 | `…/TeacherThreads/MarkTeacherThreadRead/MarkTeacherThreadReadHandler.cs` | sealed class | `MarkTeacherThreadReadHandler(ITeacherThreadRepository teacherThreadRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<MarkTeacherThreadReadCommand>`. Steps: (1) `UserId` null/default → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`. (2) `thread = FirstOrDefaultAsync(x => x.Id == request.ThreadId && x.StudentId == userId, ct, include: q => q.Include(x => x.Messages))` (tracked) ?? `NotFoundCoreException(ErrorCodes.TeacherThreadNotFound)`. (3) `thread.MarkRepliesRead(timeProvider.GetUtcNow())`. (4) `SaveChangesAsync`. No validator: the route constraint guarantees a Guid, and `Guid.Empty` gives 404. |

### Application — teacher inbox (`Elmanhg.Application.TeacherInbox.*`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 5 | `…/TeacherInbox/Shared/TeacherInboxFilter.cs` | enum | `namespace Elmanhg.Application.TeacherInbox.Shared; public enum TeacherInboxFilter { All, Unclaimed, Mine }` |
| 6 | `…/TeacherInbox/Shared/TeacherInboxItemResult.cs` | sealed record | `TeacherInboxItemResult(Guid Id, string SubjectName, string LessonName, string QuestionText, string StudentName, string? TeacherName, bool IsClaimedByMe, TeacherThreadStatus Status, bool IsOverdue, DateTimeOffset SubmittedAt, DateTimeOffset SlaDueAt)`. Staff-facing; names are plain strings (no `LocalizedText` in this model). |
| 7 | `…/TeacherInbox/Shared/TeacherInboxThreadResult.cs` | sealed record | `TeacherInboxThreadResult(Guid Id, TeacherThreadContextResult Context, string StudentName, string? TeacherName, bool IsClaimedByMe, bool CanClaim, bool CanReply, TeacherThreadStatus Status, bool IsOverdue, DateTimeOffset SubmittedAt, DateTimeOffset SlaDueAt, DateTimeOffset? ClaimedAt, List<TeacherMessageResult> Messages)`. |
| 8 | `…/TeacherInbox/Shared/TeacherInboxResultGenerator.cs` | static class | `public static TeacherInboxItemResult GenerateItem(TeacherThread thread, IReadOnlyDictionary<Guid, string> names, Guid callerId, DateTimeOffset now)`: context = `thread.ReadContext()`; `QuestionText = TeacherThreadResultGenerator.QuestionText(thread)`; `StudentName = names.GetValueOrDefault(thread.StudentId, string.Empty)`; `TeacherName = thread.TeacherId is { } teacherId ? names.GetValueOrDefault(teacherId) : null`; `IsClaimedByMe = thread.IsClaimedBy(callerId)`; `IsOverdue = thread.IsOverdueAt(now)`. `public static TeacherInboxThreadResult GenerateThread(TeacherThread thread, IReadOnlyDictionary<Guid, string> names, Guid callerId, DateTimeOffset now)`: `Context = TeacherThreadResultGenerator.GenerateContext(thread.ReadContext())`; `CanClaim = thread.TeacherId == null && thread.Status != TeacherThreadStatus.Closed`; `CanReply = thread.IsClaimedBy(callerId) && thread.Status == TeacherThreadStatus.Open`; `Messages = TeacherThreadResultGenerator.GenerateMessages(thread)`; the other fields as above. |
| 9 | `…/TeacherInbox/Shared/TeacherInboxAccess.cs` | static class | `public static async Task EnsureCanAccessAsync(Guid subjectId, Guid userId, string? role, ITeacherSubjectRepository teacherSubjectRepository, CancellationToken cancellationToken)`: `if (role == nameof(UserRole.Admin)) return;` then `if (!await teacherSubjectRepository.IsAssignedAsync(userId, subjectId, cancellationToken).ConfigureAwait(false)) throw new ForbiddenCoreException(ErrorCodes.SubjectOutOfScope);` |
| 10 | `…/TeacherInbox/Shared/TeacherInboxNames.cs` | static class | `public static async Task<Dictionary<Guid, string>> LoadAsync(IUserRepository userRepository, IReadOnlyCollection<TeacherThread> threads, CancellationToken cancellationToken)`: ids = `threads.Select(x => x.StudentId).Concat(threads.Where(x => x.TeacherId != null).Select(x => x.TeacherId!.Value)).Distinct().ToList()`; `users = await userRepository.FindAsync(x => ids.Contains(x.Id), cancellationToken, asNoTracking: true)`; return `users.ToDictionary(x => x.Id, x => x.DisplayName)`. |
| 11 | `…/TeacherInbox/Shared/TeacherInboxQueryShape.cs` | static class | `public static Expression<Func<TeacherThread, bool>> Filter(IReadOnlyCollection<Guid>? subjectIds, TeacherInboxFilter filter, Guid callerId)` → `x => (subjectIds == null \|\| subjectIds.Contains(x.SubjectId)) && (filter != TeacherInboxFilter.Unclaimed \|\| x.TeacherId == null) && (filter != TeacherInboxFilter.Mine \|\| x.TeacherId == callerId)`. `public static IOrderedQueryable<TeacherThread> Order(IQueryable<TeacherThread> query)` → `query.OrderBy(x => x.Status == TeacherThreadStatus.Open ? 0 : 1).ThenBy(x => x.SlaDueAt).ThenBy(x => x.Id)` (one operator per line). |
| 12 | `…/TeacherInbox/GetTeacherInbox/GetTeacherInboxQuery.cs` | sealed record | `GetTeacherInboxQuery(TeacherInboxFilter Filter = TeacherInboxFilter.All, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<TeacherInboxItemResult>>`. |
| 13 | `…/TeacherInbox/GetTeacherInbox/GetTeacherInboxValidator.cs` | sealed class | Ctor `(IOptions<AskTeacherOptions> askTeacherOptions)`. `RuleFor(x => x.Filter).IsInEnum().WithErrorCode(ErrorCodes.TeacherInboxFilterInvalid)`; `RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.TeacherThreadPageNumberInvalid)`; `RuleFor(x => x.PageSize).ValidateRange(1, options.ThreadListMaxPageSize, ErrorCodes.TeacherThreadPageSizeInvalid)`. |
| 14 | `…/TeacherInbox/GetTeacherInbox/GetTeacherInboxHandler.cs` | sealed class | `GetTeacherInboxHandler(ITeacherThreadRepository teacherThreadRepository, ITeacherSubjectRepository teacherSubjectRepository, IUserRepository userRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetTeacherInboxQuery, PageData<TeacherInboxItemResult>>`. Steps: (1) 401 as above. (2) `isAdmin = currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin)`. (3) `List<Guid>? subjectIds = null`; if not admin: `(await teacherSubjectRepository.FindAsync(x => x.TeacherId == userId, ct, asNoTracking: true)).Select(x => x.SubjectId).ToList()`. (4) `page = FindPaginatedAsync(request.PageNumber, request.PageSize, ct, filter: TeacherInboxQueryShape.Filter(subjectIds, request.Filter, userId), include: q => q.Include(x => x.Messages), orderBy: TeacherInboxQueryShape.Order, asNoTracking: true)`. (5) `names = TeacherInboxNames.LoadAsync(userRepository, page.Items, ct)`. (6) `now = timeProvider.GetUtcNow()`; return `PageData<TeacherInboxItemResult>` with `Items = page.Items.Select(x => TeacherInboxResultGenerator.GenerateItem(x, names, userId, now)).ToList()` and the four paging fields copied (same shape as `GetMyTeacherThreadsHandler`). |
| 15 | `…/TeacherInbox/GetInboxThread/GetInboxThreadQuery.cs` | sealed record | `GetInboxThreadQuery(Guid ThreadId) : IRequest<TeacherInboxThreadResult>`. |
| 16 | `…/TeacherInbox/GetInboxThread/GetInboxThreadHandler.cs` | sealed class | `GetInboxThreadHandler(ITeacherThreadRepository teacherThreadRepository, ITeacherSubjectRepository teacherSubjectRepository, IUserRepository userRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetInboxThreadQuery, TeacherInboxThreadResult>`. Steps: (1) 401. (2) `thread = FirstOrDefaultAsync(x => x.Id == request.ThreadId, ct, include: q => q.Include(x => x.Messages), asNoTracking: true) ?? NotFoundCoreException(TeacherThreadNotFound)`. (3) `TeacherInboxAccess.EnsureCanAccessAsync(thread.SubjectId, userId, currentUserService.GetClaim(ClaimTypes.Role), teacherSubjectRepository, ct)`. (4) names for `[thread]`. (5) `GenerateThread(thread, names, userId, now)`. |
| 17 | `…/TeacherInbox/ClaimTeacherThread/ClaimTeacherThreadCommand.cs` | sealed record | `ClaimTeacherThreadCommand(Guid ThreadId) : IRequest<TeacherInboxThreadResult>`. |
| 18 | `…/TeacherInbox/ClaimTeacherThread/ClaimTeacherThreadHandler.cs` | sealed class | Same ctor deps as #16. Steps: (1) 401. (2) load **tracked** with messages (no `asNoTracking`) ?? 404. (3) access check (403). (4) `now = timeProvider.GetUtcNow()`; `thread.Claim(userId, now)`. (5) `SaveChangesAsync`. (6) names; `GenerateThread(thread, names, userId, now)`. |
| 19 | `…/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadCommand.cs` | sealed record | `ReplyToTeacherThreadCommand(Guid ThreadId, string? Text) : IRequest<TeacherInboxThreadResult>`. |
| 20 | `…/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadValidator.cs` | sealed class | Ctor `(IOptions<AskTeacherOptions> askTeacherOptions)`. `RuleFor(x => x.Text).Cascade(CascadeMode.Stop).ValidateRequired(ErrorCodes.TeacherThreadReplyTextRequired).ValidateMaxLength(options.ReplyTextMaxLength, ErrorCodes.TeacherThreadReplyTextTooLong)`. |
| 21 | `…/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadHandler.cs` | sealed class | Same deps as #16. Steps: (1) 401. (2) tracked load with messages ?? 404. (3) access check. (4) `thread.Reply(userId, request.Text ?? string.Empty, now)`. (5) `SaveChangesAsync`. (6) names; `GenerateThread`. |

All handlers: `.ConfigureAwait(false)` on every await; no try/catch. Validators are picked up by the existing assembly scan (no DI change).

### API
| # | Path | Type | Contract |
|---|------|------|----------|
| 22 | `api/Elmanhg.Api/Controllers/TeacherInbox/TeacherInboxController.cs` | controller | `[ApiController] [Route("api/teacher-inbox")] [Authorize] public class TeacherInboxController(IMediator mediator) : ControllerBase`. Actions in "API surface". |
| 23 | `api/Elmanhg.Api/Controllers/TeacherInbox/Requests.cs` | records | `public sealed record ReplyToTeacherThreadRequest(string? Text);` |

### Tests (api)
| # | Path |
|---|------|
| 24 | `api/Elmanhg.Tests/Domain/TeacherThreads/TeacherThreadClaimAndReplyTests.cs` |
| 25 | `api/Elmanhg.Tests/Application/Features/TeacherInbox/Shared/TeacherInboxQueryShapeTests.cs` |
| 26 | `api/Elmanhg.Tests/Application/Features/TeacherInbox/Shared/TeacherInboxResultGeneratorTests.cs` |
| 27 | `api/Elmanhg.Tests/Application/Features/TeacherInbox/GetTeacherInbox/GetTeacherInboxHandlerTests.cs` |
| 28 | `api/Elmanhg.Tests/Application/Features/TeacherInbox/GetTeacherInbox/GetTeacherInboxValidatorTests.cs` |
| 29 | `api/Elmanhg.Tests/Application/Features/TeacherInbox/GetInboxThread/GetInboxThreadHandlerTests.cs` |
| 30 | `api/Elmanhg.Tests/Application/Features/TeacherInbox/ClaimTeacherThread/ClaimTeacherThreadHandlerTests.cs` |
| 31 | `api/Elmanhg.Tests/Application/Features/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadHandlerTests.cs` |
| 32 | `api/Elmanhg.Tests/Application/Features/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadValidatorTests.cs` |
| 33 | `api/Elmanhg.Tests/Application/Features/TeacherThreads/MarkTeacherThreadRead/MarkTeacherThreadReadHandlerTests.cs` |
| 34 | `api/Elmanhg.Tests/Integration/TeacherInbox/TeacherInboxTestData.cs`: static helpers `Route = "/api/teacher-inbox"`; `Task<(User Teacher, HttpClient Client)> SignedInTeacherForAsync(ApiFactory factory, Guid subjectId)` (SeedTeacherAsync + AssignAsync + SignedInClientAsync); `Task<TeacherThread> SeedThreadAsync(ApiFactory factory, TeacherThread thread)` (adds, saves, returns); `Task<TeacherThread> ReadThreadAsync(ApiFactory factory, Guid threadId)` (fresh scope, `Include(Messages)`, `AsNoTracking`); `TeacherThreadContext ContextFor(Guid subjectId)`. |
| 35 | `api/Elmanhg.Tests/Integration/TeacherInbox/TeacherInboxEndpointTests.cs` |
| 36 | `api/Elmanhg.Tests/Integration/TeacherInbox/ClaimTeacherThreadEndpointTests.cs` |
| 37 | `api/Elmanhg.Tests/Integration/TeacherInbox/ReplyToTeacherThreadEndpointTests.cs` |
| 38 | `api/Elmanhg.Tests/Integration/TeacherThreads/MarkTeacherThreadReadEndpointTests.cs` |
| 39 | `api/Elmanhg.Tests/Integration/Persistence/TeacherThreadPersistenceTests.cs` |

Seeded threads need a real student (FK) and a real subject: use `ScopeTestData.SeedStudentAsync` and `TeacherThreadTestData.SeedPublishedLessonAsync` (or `ScopeTestData.SeedSubjectAsync`). Integration classes follow the existing `(ApiFactory factory)` primary-ctor pattern.

### Web
| # | Path | Contract |
|---|------|----------|
| 40 | `web/src/routes/teacher/thread.$threadId.tsx` | `createFileRoute('/teacher/thread/$threadId')({ component: ThreadRoute })`; `ThreadRoute` reads `threadId` and renders `<InboxThreadPage threadId={threadId} />` (mirrors `routes/student/thread.$threadId.tsx`). |
| 41 | `web/src/features/askTeacher/schemas/teacherInboxSearchSchema.ts` | `teacherInboxSearchSchema = z.object({ filter: z.enum(['Unclaimed', 'Mine']).optional().catch(undefined), page: z.coerce.number().int().min(1).optional().catch(undefined) })`; `export type TeacherInboxSearch`. |
| 42 | `web/src/features/askTeacher/schemas/replyFormSchema.ts` | `replyFormSchema = z.object({ text: z.string().trim().min(1, { error: 'askTeacher:inboxThread.replyRequired' }) })`; `export interface ReplyFormValues { text: string }`. |
| 43 | `web/src/features/askTeacher/hooks/useTeacherInboxSearch.ts` | `getRouteApi('/teacher/inbox')`. Returns `{ filter: TeacherInboxFilter ('All' when undefined), page (default 1), setFilter(filter: TeacherInboxFilter) → navigate search { filter: filter === 'All' ? undefined : filter, page: undefined }, setPage(page) → keeps filter }`. |
| 44 | `web/src/features/askTeacher/hooks/useTeacherInbox.ts` | `export const inboxPageSize = 20;` `useTeacherInbox(filter, page)` = `useGetTeacherInbox({ filter, pageNumber: page, pageSize: inboxPageSize }, { query: { placeholderData: keepPreviousData } })`. |
| 45 | `web/src/features/askTeacher/hooks/useClaimThread.ts` | `useClaimThread(threadId)` → `{ claim: () => void, isPending }`. `useClaimTeacherThread({ mutation: { onSuccess: (result) => { setQueryData(getGetInboxThreadQueryKey(threadId), result); invalidate getGetTeacherInboxQueryKey(); toast.success(t('inboxThread.claimed')); }, onError: async (error) => { toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION'])); await invalidate getGetInboxThreadQueryKey(threadId); invalidate inbox; } } })`. `code` as in `useQuestionDecision`. |
| 46 | `web/src/features/askTeacher/hooks/useReplyToThread.ts` | `useReplyToThread(threadId)` → `{ submit: (values: ReplyFormValues) => Promise<unknown> }` = `mutateAsync({ threadId, data: { text: values.text } })`. onSuccess: setQueryData(thread key, result), invalidate inbox, `toast.success(t('inboxThread.sent'))`. onError: if `error instanceof ApiError && error.status === 409` → invalidate the thread key. (The `Form` shows the error: field for the REPLY codes, root otherwise.) |
| 47 | `web/src/features/askTeacher/hooks/useMarkThreadRead.ts` | `useMarkThreadRead(threadId: string, hasUnreadReply: boolean): void`. `useMarkTeacherThreadRead({ mutation: { onSuccess: () => { invalidate getGetMyTeacherThreadsQueryKey(); invalidate getGetMyTeacherThreadQueryKey(threadId); } } })`. `useEffectEvent` + `useEffect([threadId, hasUnreadReply])`: `if (hasUnreadReply) mutate({ threadId })` (mirrors `useRecordOpening`). Errors are silent: no toast, since this is a background receipt. |
| 48 | `web/src/features/askTeacher/components/InboxFilterTabs.tsx` | Props `{ filter: TeacherInboxFilter; onChange: (filter) => void }`. `role="group"` + `aria-label={t('inbox.filters.label')}`. Buttons for `['All','Unclaimed','Mine']` with `aria-pressed` and label `t('inbox.filters.<value>')`; classes copied from `PaymentLogTabs` (pill, 44 px). |
| 49 | `web/src/features/askTeacher/components/InboxListItem.tsx` | Props `{ thread: TeacherInboxItemResult }`. `<li><Link to="/teacher/thread/$threadId">` with the same classes as `ThreadListItem`: title `questionText` (line-clamp-2, ui 600); caption 1 `t('inbox.meta', { subject, lesson, student, date })` (date formatted like `ThreadListItem`); caption 2 `isClaimedByMe ? t('inbox.claimedByMe') : teacherName ? t('inbox.claimedBy', { teacher }) : t('inbox.unclaimed')`; trailing `<ThreadStatusBadge thread={thread} />`. |
| 50 | `web/src/features/askTeacher/components/InboxList.tsx` | Uses `useTeacherInboxSearch` + `useTeacherInbox`. States (copy `ThreadList`): `ContentListSkeleton label=inbox.loading`, `ContentErrorState title=inbox.errorTitle` with retry, `ContentEmptyState message=inbox.empty`, list + `Pagination` when `totalPages > 1`. |
| 51 | `web/src/features/askTeacher/components/ReplyForm.tsx` | Props `{ threadId: string }`. `useForm<ReplyFormValues>({ resolver: zodResolver(replyFormSchema), defaultValues: { text: '' } })`. `<Form serverErrorFields={{ TEACHER_THREAD_REPLY_TEXT_REQUIRED: 'text', TEACHER_THREAD_REPLY_TEXT_TOO_LONG: 'text' }}>`, `h2` `inboxThread.replyTitle` (h2 style), `TextAreaField name="text" label=inboxThread.replyLabel description=inboxThread.replyHint`, `FormRootError`, `SubmitButton` `inboxThread.send`. In a white card (`rounded-lg border border-border bg-surface p-4 shadow-1`). |
| 52 | `web/src/features/askTeacher/components/InboxThreadActions.tsx` | Props `{ thread: TeacherInboxThreadResult }`. `canClaim` → `<Button onClick={claim} disabled={isPending}>{t('inboxThread.claim')}</Button>`; else `canReply` → `<ReplyForm threadId>`; else `!isClaimedByMe && teacherName` → `<p className="text-caption text-text-muted">{t('inboxThread.claimedByOther')}</p>`; else status `Closed` → `inboxThread.closed`; else (mine, Answered) → `inboxThread.answered`. |
| 53 | `web/src/features/askTeacher/pages/TeacherInboxPage.tsx` | `section flex-col gap-4`: `h1` `inbox.title` (h1 classes as `AskTeacherListPage`), `<InboxFilterTabs filter onChange={setFilter} />`, `<InboxList />`. |
| 54 | `web/src/features/askTeacher/pages/InboxThreadPage.tsx` | Props `{ threadId }`. `useGetInboxThread(threadId)`. Loading `ContentListSkeleton label=thread.loading`; error `ContentErrorState title=thread.errorTitle` with retry (403 shows the common `SUBJECT_OUT_OF_SCOPE` text). Success: breadcrumb (copy `TeacherThreadPage`) linking `/teacher/inbox` with the `inbox.title` label → `thread.title`; `h1 thread.title`; `<ThreadContextCard thread={data}><p className="text-caption text-text-muted">{t('inboxThread.people', { student: data.studentName, teacher: data.isClaimedByMe ? t('thread.you') : data.teacherName ?? t('inbox.unclaimed') })}</p></ThreadContextCard>`; messages `<ThreadMessage authorLabel={m.isFromStudent ? data.studentName : data.isClaimedByMe ? t('thread.you') : data.teacherName ?? t('thread.teacher')} />`; `<InboxThreadActions thread={data} />`. |
| 55 | `web/src/test/teacherInboxFixtures.ts` | `inboxItem(overrides?)`: `TeacherInboxItemResult` (id `threadId`, Physics / Newton's laws, `questionText 'Why is F = ma?'`, `studentName 'Ahmed'`, `teacherName null`, `isClaimedByMe false`, `status 'Open'`, `isOverdue false`, dates as `threadSummary`). `inboxPage(items, { pageNumber, totalPages })`. `inboxThread(overrides?)`: `TeacherInboxThreadResult` (context `threadContext()`, `studentName 'Ahmed'`, `teacherName null`, `isClaimedByMe false`, `canClaim true`, `canReply false`, `claimedAt null`, messages = the student message of `teacherThread()`). `claimedInboxThread()`: `teacherName 'Mohamed'`, `isClaimedByMe true`, `canClaim false`, `canReply true`, `claimedAt` set. `answeredInboxThread()`: claimed + `status 'Answered'`, `canReply false`, plus a teacher message `{ isFromStudent: false, text: 'Because F = ma.' }`. |
| 56 | `web/src/features/askTeacher/schemas/teacherInboxSearchSchema.test.ts` | tests #W1–#W3 |
| 57 | `web/src/features/askTeacher/schemas/replyFormSchema.test.ts` | tests #W4–#W5 |
| 58 | `web/src/features/askTeacher/pages/TeacherInboxPage.test.tsx` | tests #W6–#W12 |
| 59 | `web/src/features/askTeacher/pages/InboxThreadPage.test.tsx` | tests #W13–#W19 plus #W22 |

### Web copy (`features/askTeacher/i18n`)
| Key | ar | en |
|---|---|---|
| `badge.newReply` | رد جديد | New reply |
| `inbox.title` | أسئلة الطلاب | Student questions |
| `inbox.filters.label` | تصفية الأسئلة | Filter questions |
| `inbox.filters.All` / `.Unclaimed` / `.Mine` | الكل / غير مُستلمة / الخاصة بي | All / Unclaimed / Mine |
| `inbox.loading` | جارٍ تحميل أسئلة الطلاب… | Loading student questions… |
| `inbox.errorTitle` | تعذّر تحميل أسئلة الطلاب | Could not load student questions |
| `inbox.empty` | لا توجد أسئلة. | No questions. |
| `inbox.meta` | {subject} / {lesson} · {student} · {date} | {subject} / {lesson} · {student} · {date} |
| `inbox.unclaimed` | غير مُستلم | Unclaimed |
| `inbox.claimedByMe` | مستلم بواسطتك | Claimed by you |
| `inbox.claimedBy` | المعلّم: {teacher} | Teacher: {teacher} |
| `inboxThread.people` | الطالب: {student} · المعلّم: {teacher} | Student: {student} · Teacher: {teacher} |
| `inboxThread.claim` | استلام السؤال | Claim question |
| `inboxThread.claimed` | تم استلام السؤال. | Question claimed. |
| `inboxThread.claimedByOther` | هذه المحادثة مستلمة بواسطة معلّم آخر. | Another teacher has claimed this conversation. |
| `inboxThread.replyTitle` | الرد | Reply |
| `inboxThread.replyLabel` | ردّك | Your reply |
| `inboxThread.replyHint` | اكتب ردك | Write your reply |
| `inboxThread.replyRequired` | اكتب الرد | Write your reply |
| `inboxThread.send` | إرسال الرد | Send reply |
| `inboxThread.sent` | تم إرسال الرد. | Reply sent. |
| `inboxThread.answered` | تم الرد على هذا السؤال. | This question has been answered. |
| `inboxThread.closed` | أُغلق هذا السؤال. | This question is closed. |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| Domain `TeacherThreadAlreadyClaimed` | `TEACHER_THREAD_ALREADY_CLAIMED` | `TeacherThread.Claim`, `Reply` (`EnsureClaimedBy`) | `ConflictCoreException` | 409 |
| Domain `TeacherThreadNotClaimed` | `TEACHER_THREAD_NOT_CLAIMED` | `EnsureClaimedBy` | `ConflictCoreException` | 409 |
| Domain `TeacherThreadNotAwaitingReply` | `TEACHER_THREAD_NOT_AWAITING_REPLY` | `Reply` | `ConflictCoreException` | 409 |
| App `TeacherThreadModifiedConcurrently` | `TEACHER_THREAD_MODIFIED_CONCURRENTLY` | `AppDbContext.SaveChangesAsync` | `ConflictCoreException` | 409 |
| App `TeacherThreadReplyTextRequired` | `TEACHER_THREAD_REPLY_TEXT_REQUIRED` | `ReplyToTeacherThreadValidator` | validation | 422 |
| App `TeacherThreadReplyTextTooLong` | `TEACHER_THREAD_REPLY_TEXT_TOO_LONG` | `ReplyToTeacherThreadValidator` | validation | 422 |
| App `TeacherInboxFilterInvalid` | `TEACHER_INBOX_FILTER_INVALID` | `GetTeacherInboxValidator` | validation | 422 |
| (existing) `SUBJECT_OUT_OF_SCOPE`, `TEACHER_THREAD_NOT_FOUND`, `USER_NOT_AUTHENTICATED`, `TEACHER_THREAD_PAGE_*_INVALID`, `TEACHER_MESSAGE_TEXT_REQUIRED` | — | reused | — | 403 / 404 / 401 / 422 / 400 |

Strings (resx and web `common:errors`; Arabic without tashkeel):
| Code | ar | en |
|---|---|---|
| TEACHER_THREAD_ALREADY_CLAIMED | استلم معلم آخر هذا السؤال. | Another teacher has already claimed this question. |
| TEACHER_THREAD_NOT_CLAIMED | استلم السؤال أولا قبل الرد. | Claim the question before replying. |
| TEACHER_THREAD_NOT_AWAITING_REPLY | هذا السؤال لا ينتظر ردا الآن. | This question is not waiting for a reply. |
| TEACHER_THREAD_MODIFIED_CONCURRENTLY | تغيرت المحادثة للتو. أعد تحميلها ثم حاول مرة أخرى. | The conversation just changed. Reload it and try again. |
| TEACHER_THREAD_REPLY_TEXT_REQUIRED | اكتب الرد. | Write your reply. |
| TEACHER_THREAD_REPLY_TEXT_TOO_LONG | الرد طويل جدا. | The reply is too long. |
| TEACHER_INBOX_FILTER_INVALID | تصفية الأسئلة غير صالحة. | The inbox filter is not valid. |

## Domain behaviour
`TeacherThread.Replies.cs` (uses `Core.Errors`, `Elmanhg.Domain.SharedKernel.Exceptions`):
```csharp
public void Claim(Guid teacherId, DateTimeOffset claimedAt)
{
    if (TeacherId == teacherId)
    {
        return;
    }

    if (TeacherId is not null)
    {
        throw new ConflictCoreException(ErrorCodes.TeacherThreadAlreadyClaimed);
    }

    var at = ToMicroseconds(claimedAt);
    TeacherId = teacherId;
    ClaimedAt = at;
    UpdatedBy = teacherId;
    UpdationDate = at;
}

public TeacherMessage Reply(Guid teacherId, string text, DateTimeOffset repliedAt)
{
    EnsureClaimedBy(teacherId);
    if (Status != TeacherThreadStatus.Open)
    {
        throw new ConflictCoreException(ErrorCodes.TeacherThreadNotAwaitingReply);
    }

    var at = ToMicroseconds(repliedAt);
    var message = TeacherMessage.CreateText(Id, teacherId, text, null, at);
    Messages.Add(message);
    Status = TeacherThreadStatus.Answered;
    UpdatedBy = teacherId;
    UpdationDate = at;
    return message;
}

// Read receipts live on the message rows so a student opening the thread never changes the thread's xmin under a teacher's reply.
public void MarkRepliesRead(DateTimeOffset readAt)
{
    var at = ToMicroseconds(readAt);
    foreach (var message in Messages.Where(x => x.SenderId != StudentId))
    {
        message.MarkReadByStudent(at);
    }
}

private void EnsureClaimedBy(Guid teacherId)
{
    if (TeacherId is null)
    {
        throw new ConflictCoreException(ErrorCodes.TeacherThreadNotClaimed);
    }

    if (TeacherId != teacherId)
    {
        throw new ConflictCoreException(ErrorCodes.TeacherThreadAlreadyClaimed);
    }
}
```
- `MarkReadByStudent` keeps the first read time (`??=`).
- A blank reply text throws the existing `BusinessRuleViolationCoreException(TEACHER_MESSAGE_TEXT_REQUIRED)` from `TeacherMessage.CreateText`. The validator normally catches it first.
- For #96: voice adds a sibling `ReplyWithVoice` that reuses `EnsureClaimedBy` and the status guard. For #97: the follow-up sets `Status = Open` and resets `SlaDueAt`. Neither ships here.

## API surface
| Method | Route | Policy | Input | Response |
|---|---|---|---|---|
| GET | `/api/teacher-inbox?filter&pageNumber&pageSize` (Name `GetTeacherInbox`) | `DefaultCodes.AskTeacherReply` | `[FromQuery] TeacherInboxFilter filter = TeacherInboxFilter.All, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20` → `GetTeacherInboxQuery` | `PageData<TeacherInboxItemResult>` |
| GET | `/api/teacher-inbox/{threadId:guid}` (Name `GetInboxThread`) | `AskTeacherReply` | route → `GetInboxThreadQuery` | `TeacherInboxThreadResult` |
| POST | `/api/teacher-inbox/{threadId:guid}/claim` (Name `ClaimTeacherThread`) | `AskTeacherReply` | route → `ClaimTeacherThreadCommand` | `TeacherInboxThreadResult` |
| POST | `/api/teacher-inbox/{threadId:guid}/replies` (Name `ReplyToTeacherThread`) | `AskTeacherReply` | `[FromBody] ReplyToTeacherThreadRequest` → `new ReplyToTeacherThreadCommand(threadId, request.Text)` | `TeacherInboxThreadResult` |
| POST | `/api/teacher-threads/{threadId:guid}/read` (Name `MarkTeacherThreadRead`) | `AskTeacherSubmit` | route → `MarkTeacherThreadReadCommand` | `Ok()` (200, empty) |

Each action has `[ProducesResponseType<T>(StatusCodes.Status200OK)]` (none for `read`: use `[ProducesResponseType(StatusCodes.Status200OK)]`) and passes `cancellationToken` (mirror `TeacherThreadsController`).

Postman `TeacherInbox` folder (description: "Teacher (assigned to the subject) or Admin. Sign in as the teacher so accessToken is set; Get inbox fills inboxThreadId from the first unclaimed item."), in order: "Get inbox" (`?filter=Unclaimed&pageNumber=1&pageSize=20`, test: 200 + set `inboxThreadId` from `items[0].id` when present), "Get inbox thread", "Claim thread", "Reply to thread" (body `{ "text": "Because force equals mass times acceleration." }`), "Get inbox (mine)" (`?filter=Mine`). `AskTeacher` folder: "Mark my thread read" (`POST {{baseUrl}}/api/teacher-threads/{{teacherThreadId}}/read`, test status 200).

## Test plan

### Domain — `TeacherThreadClaimAndReplyTests` (no doubles; `TeacherThreadBuilder`)
| # | Method | Asserts |
|---|---|---|
| T1 | `Claim_UnclaimedThread_SetsTeacherAndClaimedAt` | `TeacherId`, `ClaimedAt` (microsecond-truncated), `UpdatedBy`, `UpdationDate`; status stays Open |
| T2 | `Claim_SameTeacherAgain_KeepsFirstClaim` | second `Claim` with a later time leaves `ClaimedAt` unchanged, no throw |
| T3 | `Claim_ClaimedByAnotherTeacher_ThrowsAlreadyClaimed` | `ConflictCoreException` + `TEACHER_THREAD_ALREADY_CLAIMED`; `TeacherId` unchanged |
| T4 | `Reply_ClaimedOpenThread_AddsTextReplyAndMarksAnswered` | returned message (`SenderId` teacher, `Kind` Text, trimmed text, `ThreadId`, `CreatedAt`), `Messages` count 2, `Status` Answered, `UpdationDate` |
| T5 | `Reply_UnclaimedThread_ThrowsNotClaimed` | `TEACHER_THREAD_NOT_CLAIMED`; messages count 1 |
| T6 | `Reply_ClaimedByAnotherTeacher_ThrowsAlreadyClaimed` | `TEACHER_THREAD_ALREADY_CLAIMED` |
| T7 | `Reply_AnsweredThread_ThrowsNotAwaitingReply` | `TEACHER_THREAD_NOT_AWAITING_REPLY` (builder `AnsweredBy`) |
| T8 | `Reply_BlankText_ThrowsTeacherMessageTextRequired` | `BusinessRuleViolationCoreException` + `TEACHER_MESSAGE_TEXT_REQUIRED`; status Open |
| T9 | `HasUnreadReply_OnlyStudentMessage_ReturnsFalse` | false |
| T10 | `HasUnreadReply_AfterReply_ReturnsTrue` | true |
| T11 | `MarkRepliesRead_AfterReply_StampsTeacherMessagesOnly` | teacher message `StudentReadAt` = truncated time; the student message stays null; `HasUnreadReply()` false |
| T12 | `MarkRepliesRead_AlreadyRead_KeepsFirstReadTime` | a second call with a later time keeps the first |

### Application
| # | Class | Method | Asserts |
|---|---|---|---|
| T13 | `TeacherInboxQueryShapeTests` | `Filter_TeacherScope_KeepsOnlyAssignedSubjects` | compiled filter over threads in 2 subjects keeps only the assigned subject |
| T14 | 〃 | `Filter_EmptyAssignment_KeepsNothing` | empty list → none (fail closed) |
| T15 | 〃 | `Filter_AdminScope_KeepsEverySubject` | `subjectIds null` → all |
| T16 | 〃 | `Filter_Unclaimed_KeepsOnlyThreadsWithoutTeacher` | |
| T17 | 〃 | `Filter_Mine_KeepsOnlyThreadsClaimedByCaller` | excludes unclaimed and other-teacher threads |
| T18 | 〃 | `Order_OpenThreadsFirstByDueTimeThenOthers` | `Order(list.AsQueryable())` → [open due earlier, open due later, answered] |
| T19 | `TeacherInboxResultGeneratorTests` | `GenerateItem_ClaimedByCaller_MapsNamesClaimAndOverdue` | StudentName, TeacherName, IsClaimedByMe true, IsOverdue at SLA, QuestionText |
| T20 | 〃 | `GenerateThread_Unclaimed_CanClaimOnly` | CanClaim true, CanReply false, TeacherName null |
| T21 | 〃 | `GenerateThread_ClaimedByCallerOpen_CanReplyOnly` | CanReply true, CanClaim false |
| T22 | 〃 | `GenerateThread_ClaimedByAnother_NeitherClaimNorReply` | both false, IsClaimedByMe false |
| T23 | `GetTeacherInboxHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException` + code |
| T24 | 〃 | `Handle_Teacher_PassesAssignedSubjectsToFilterAndMapsItems` | capture the filter arg: evaluates true for an assigned-subject thread and false for another; items mapped with the student name from `IUserRepository` |
| T25 | 〃 | `Handle_Admin_DoesNotReadAssignments` | `teacherSubjectRepository.FindAsync` `DidNotReceive`; the captured filter keeps an arbitrary subject |
| T26 | `GetTeacherInboxValidatorTests` | `Validate_DefaultQuery_Passes`; `Validate_PageNumberZero_FailsWithPageNumberInvalid`; `Validate_PageSizeAboveMax_FailsWithPageSizeInvalid`; `Validate_UnknownFilter_FailsWithFilterInvalid` (`(TeacherInboxFilter)99`) | one rule each |
| T27 | `GetInboxThreadHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated`; `Handle_UnknownThread_ThrowsTeacherThreadNotFound`; `Handle_TeacherNotAssigned_ThrowsSubjectOutOfScope`; `Handle_AssignedTeacher_ReturnsThreadWithNames`; `Handle_Admin_SkipsAssignmentCheck` (`IsAssignedAsync` `DidNotReceive`) | type + code / result fields |
| T28 | `ClaimTeacherThreadHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated`; `Handle_UnknownThread_ThrowsTeacherThreadNotFound`; `Handle_TeacherNotAssigned_ThrowsSubjectOutOfScope`; `Handle_ClaimedByAnother_ThrowsAlreadyClaimed`; `Handle_UnclaimedThread_ClaimsAndSaves` (result `IsClaimedByMe`, `CanReply`, `TeacherName`, thread `TeacherId`, `ClaimedAt` = FakeTimeProvider now) | throwing paths: `SaveChangesAsync` `DidNotReceive`; success: `Received(1)` |
| T29 | `ReplyToTeacherThreadHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated`; `Handle_UnknownThread_ThrowsTeacherThreadNotFound`; `Handle_TeacherNotAssigned_ThrowsSubjectOutOfScope`; `Handle_UnclaimedThread_ThrowsNotClaimed`; `Handle_AnsweredThread_ThrowsNotAwaitingReply`; `Handle_ClaimedOpenThread_AppendsReplyAndSaves` (result Status Answered, last message not from student with the text, `CanReply` false) | same Received/DidNotReceive rule |
| T30 | `GetMyTeacherThreadsHandlerTests` (existing) | `Handle_ThreadWithUnreadReply_ReportsHasUnreadReply` | summary `HasUnreadReply` true for an `AnsweredBy` thread, false for an open one |
| T31 | `GetMyTeacherThreadHandlerTests` (existing) | `Handle_ThreadWithUnreadReply_ReportsHasUnreadReply` | result `HasUnreadReply` true |
| T32 | `ReplyToTeacherThreadValidatorTests` | `Validate_Text_Passes`; `Validate_TextAtMaxLength_Passes`; `Validate_BlankText_FailsWithReplyTextRequired` (`[Theory]` null / "   "); `Validate_TextOverMax_FailsWithReplyTextTooLong` | code per rule |
| T33 | `MarkTeacherThreadReadHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated`; `Handle_OtherStudentsThread_ThrowsTeacherThreadNotFound` (predicate evaluated against the thread; `DidNotReceive` save); `Handle_OwnAnsweredThread_MarksRepliesReadAndSaves` (teacher message `StudentReadAt` = now, `Received(1)`) | |

### Integration (real PostgreSQL)
| # | Class | Method | Asserts |
|---|---|---|---|
| T34 | `TeacherInboxEndpointTests` | `GetInbox_AssignedTeacher_ListsOnlyAssignedSubjectThreads` | items = the physics thread only (a math thread is excluded); `studentName` = "Student"; `teacherName` null |
| T35 | 〃 | `GetInbox_OpenThreadsFirstByDueTime` | open older-due, open newer-due, then answered (ids in order) |
| T36 | 〃 | `GetInbox_MineFilter_ListsOnlyOwnClaims` | `?filter=Mine` → only the thread claimed by this teacher |
| T37 | 〃 | `GetInbox_UnknownFilter_Returns422` | `?filter=99` → 422 + `TEACHER_INBOX_FILTER_INVALID` |
| T38 | 〃 | `GetInbox_Student_Returns403` | 403 |
| T39 | 〃 | `GetInbox_Anonymous_Returns401` | 401 |
| T40 | 〃 | `GetThread_AssignedTeacher_ReturnsContextMessagesAndCanClaim` | `context.lessonName`, message text, `canClaim` true, `canReply` false |
| T41 | 〃 | `GetThread_TeacherOfOtherSubject_Returns403` | 403 + `SUBJECT_OUT_OF_SCOPE` |
| T42 | 〃 | `GetThread_Unknown_Returns404` | 404 + `TEACHER_THREAD_NOT_FOUND` |
| T43 | `ClaimTeacherThreadEndpointTests` | `Claim_AssignedTeacher_ClaimsAndPersists` | 200, `isClaimedByMe` true; DB `TeacherId` = teacher, `ClaimedAt` not null |
| T44 | 〃 | `Claim_AlreadyClaimedByAnother_Returns409AlreadyClaimed` | 409 + code; DB owner unchanged |
| T45 | 〃 | `Claim_TwoTeachersAtOnce_ExactlyOneWins` | two assigned teachers POST concurrently (two clients, `Task.WhenAll` over HTTP): exactly one 200, one 409 with code ∈ {`TEACHER_THREAD_ALREADY_CLAIMED`, `TEACHER_THREAD_MODIFIED_CONCURRENTLY`}; DB `TeacherId` = the 200 caller |
| T46 | 〃 | `Claim_TeacherOfOtherSubject_Returns403` | 403; DB `TeacherId` null |
| T47 | 〃 | `Claim_Student_Returns403` | 403 |
| T48 | `ReplyToTeacherThreadEndpointTests` | `Reply_ClaimingTeacher_StoresReplyAndStudentSeesNewReply` | 200, `status` Answered; DB: 2 messages, the second from the teacher with the text; the student's `GET /api/teacher-threads` item has `hasUnreadReply` true and `status` Answered |
| T49 | 〃 | `Reply_BlankText_Returns422` | 422 + `TEACHER_THREAD_REPLY_TEXT_REQUIRED`; DB 1 message |
| T50 | 〃 | `Reply_UnclaimedThread_Returns409NotClaimed` | 409 + code |
| T51 | 〃 | `Reply_ClaimedByAnother_Returns409AlreadyClaimed` | 409 + code |
| T52 | 〃 | `Reply_Student_Returns403` | 403 |
| T53 | `MarkTeacherThreadReadEndpointTests` | `MarkRead_OwnAnsweredThread_ClearsNewReply` | 200; then `GET /api/teacher-threads/{id}` `hasUnreadReply` false; DB teacher message `StudentReadAt` not null |
| T54 | 〃 | `MarkRead_OtherStudentsThread_Returns404` | 404 + `TEACHER_THREAD_NOT_FOUND` |
| T55 | 〃 | `MarkRead_Teacher_Returns403` | 403 |
| T56 | `TeacherThreadPersistenceTests` | `SaveChanges_StaleTeacherThread_ThrowsTeacherThreadModifiedConcurrently` | two scopes load one thread; first `Claim(teacherA)` saves; second `Claim(teacherB)` save → `ConflictCoreException` + `TEACHER_THREAD_MODIFIED_CONCURRENTLY` (pattern of `SaveChanges_StalePayment_…`) |
| T57 | 〃 | `SaveChanges_MarkRepliesRead_LeavesThreadVersionUnchanged` | seed an `AnsweredBy` thread; read `Version` (scope A); `MarkRepliesRead` + save (scope B); re-read `Version` (scope C) equals A; teacher message `StudentReadAt` not null |
| T58 | `AppDbContextTests` (existing) | `Migrate_FreshDatabase_LeavesNoPendingMigrations` (modify list) | includes `_AddTeacherThreadClaims` |


### Web (Vitest + RTL + MSW; `renderApp` with `testSessions.teacher` / `.student`; fake timers at `2026-10-02T02:00:00Z` like the existing thread test)
| # | File | `it(...)` | Asserts |
|---|---|---|---|
| W1 | `teacherInboxSearchSchema.test.ts` | `accepts the Unclaimed and Mine filters` | parsed values |
| W2 | 〃 | `drops an unknown filter` | `filter` undefined |
| W3 | 〃 | `coerces the page and drops an invalid one` | `'2'` → 2; `'0'` → undefined |
| W4 | `replyFormSchema.test.ts` | `accepts a reply` | success |
| W5 | 〃 | `rejects a blank reply` | issue message `askTeacher:inboxThread.replyRequired` |
| W6 | `TeacherInboxPage.test.tsx` | `shows loading then threads with student, claim state and SLA badge` | skeleton status → item link with question text, meta containing «Ahmed», «Unclaimed», badge «Awaiting reply · 5 hours left» |
| W7 | 〃 | `lists unclaimed threads when the Unclaimed tab is pressed` | MSW handler returns a different item when `filter=Unclaimed`; the tab gets `aria-pressed=true` and the new item shows |
| W8 | 〃 | `shows claimed by you and overdue for my overdue thread` | «Claimed by you» + «Overdue» |
| W9 | 〃 | `shows the empty state when there are no threads` | «No questions.» |
| W10 | 〃 | `shows the error state and retries` | alert, then retry → item |
| W11 | 〃 | `moves to the next page` | Pagination next → page-2 item |
| W12 | 〃 | `renders right to left in Arabic` + `has no axe violations` (two `it`s) | `dir="rtl"`, heading «أسئلة الطلاب»; axe clean |
| W13 | `InboxThreadPage.test.tsx` | `shows the context, student name, message and the claim button for an unclaimed thread` | breadcrumb link «Student questions», «Student: Ahmed · Teacher: Unclaimed», message author «Ahmed», button «Claim question» |
| W14 | 〃 | `claims the thread and shows the reply form` | click claim → toast «Question claimed.», textbox «Your reply» visible, claim button gone |
| W15 | 〃 | `shows the server message and refreshes when another teacher claimed first` | claim POST → 409 `TEACHER_THREAD_ALREADY_CLAIMED`; toast «Another teacher has already claimed this question.»; refetched thread (claimed by another) shows «Another teacher has claimed this conversation.» |
| W16 | 〃 | `sends a reply and shows the answered note` | type + «Send reply» → toast «Reply sent.», the new message «Because F = ma.» with author «You», «This question has been answered.» |
| W17 | 〃 | `shows an inline error for a blank reply` | submit empty → «Write your reply» under the field; no request (MSW `onUnhandledRequest: 'error'` guards it) |
| W18 | 〃 | `shows the out-of-scope error for a thread outside my subjects` | GET 403 `SUBJECT_OUT_OF_SCOPE` → alert with «This subject is not assigned to you.» |
| W19 | 〃 | `renders right to left in Arabic` + `has no axe violations` (two `it`s) | `dir="rtl"`, «استلام السؤال»; axe clean |
| W20 | `AskTeacherListPage.test.tsx` (existing, add) | `marks a thread with a new teacher reply` | item with `hasUnreadReply: true` shows «New reply» |
| W21 | `TeacherThreadPage.test.tsx` (existing, add) | `clears the new-reply mark after the student opens the thread` | stateful MSW: the list returns `hasUnreadReply` true until `POST …/read` is received; open the thread, then navigate to `/student/ask` via the breadcrumb → «New reply» absent |
| W22 | `InboxThreadPage.test.tsx` | `shows the server error inline when the reply is refused` | replies POST → 409 `TEACHER_THREAD_NOT_AWAITING_REPLY` → root alert with its message |

Mutation-check (PROGRESS rule): after writing T3, T5, T45, T56, T57 and W15, break the guarded line on purpose, confirm the test fails, then restore.

## Docs (`docs/ask-teacher.md`)
- Intro: "#95 adds the teacher inbox, claiming, text replies and the student's new-reply mark. Voice arrives with #96. Follow-up, rating, closing, reminders and realtime push arrive with #97."
- Model → TeacherThread table: add `TeacherId` (`teacher_id?`, FK `Users` restrict, the claimer) and `ClaimedAt` (`claimed_at?`, truncated to microseconds). Mention migration `AddTeacherThreadClaims`. TeacherMessage table: add `StudentReadAt` (`student_read_at?`, set when the student opens the thread; null for student messages; kept on messages so a read never changes the thread's xmin).
- Status machine: "#95: `Open` → `Answered` when the claiming teacher replies. #97: the follow-up and `Closed`."
- New section **Teacher inbox**: scope (assigned subjects, admin all, fail closed; 403 `SUBJECT_OUT_OF_SCOPE` by id); filters All/Unclaimed/Mine; order; student display name only; claim rules (first claim wins, idempotent self-claim, 409 `TEACHER_THREAD_ALREADY_CLAIMED`, race 409 `TEACHER_THREAD_MODIFIED_CONCURRENTLY` via xmin); reply rules (claimer only, `Open` only, 422 text codes, `ReplyTextMaxLength` 4000); not audited.
- New section **Student notification**: in-app «رد جديد» (`hasUnreadReply`), `POST /api/teacher-threads/{id}/read` when the thread opens; no WhatsApp/Email in #95; realtime push in #97.
- API: add the `read` row to the student table; a new table of the four teacher endpoints (policy `AskTeacher.Reply`, Teacher assigned or Admin) with their errors.
- Options: add `ReplyTextMaxLength` 4000.
- Web: `/teacher/inbox` (H1 «أسئلة الطلاب», tabs, item content, states, `?filter=&page=`), `/teacher/thread/$threadId` (context card with «الطالب / المعلّم», messages, «استلام السؤال» / reply form / notes, 403 state), and the student «رد جديد».
- For later stories: #96 unchanged + "`ReplyWithVoice` reuses the claim and status guards". #97: "`ClosedAt`, `Rating`, the follow-up (`Answered` → `Open`, resets `SlaDueAt`), closing after the second reply, the (Status, SlaDueAt) index, reminders and realtime push". Remove the old #95 bullet.
- Known limits: add "A teacher unassigned from a subject loses access to threads they claimed there." and "Admins can claim and reply through the API; there is no admin inbox screen yet."

`docs/claude-design-prompt.md` §4 Teacher, new inbox line: "`#/teacher/inbox` «أسئلة الطلاب»: Ask a Teacher threads for assigned subjects, awaiting ones first by reply due time, with pill tabs الكل / غير مُستلمة / الخاصة بي; each item shows the question, subject / lesson, the student's display name, the date, who claimed it (غير مُستلم / مستلم بواسطتك / المعلّم: …) and the SLA badge; loading, empty, error-with-retry, paged. `#/teacher/thread/:id` the context card with «الطالب · المعلّم», the messages, «استلام السؤال» (a lost race shows «استلم معلم آخر هذا السؤال.»), then a text reply (voice and its editable transcript arrive with the voice story); a thread claimed by another teacher shows «هذه المحادثة مستلمة بواسطة معلّم آخر.»"

## Definition of done
- [ ] Every file in "Files to create" exists; no other new files.
- [ ] Migration `AddTeacherThreadClaims` only adds 3 nullable columns, the `TeacherId` index and the FK; `Model_Current_MatchesLatestMigrationSnapshot` passes; the migration list is updated.
- [ ] `TeacherThread.Claim/Reply/MarkRepliesRead` match "Domain behaviour"; the `MarkRepliesRead` WHY comment is present; no other comments.
- [ ] `AppDbContext` maps `DbUpdateConcurrencyException` on `TeacherThread` to 409 `TEACHER_THREAD_MODIFIED_CONCURRENTLY`; no handler try/catch.
- [ ] Teacher list is scoped by `TeacherSubject` (empty assignment = empty list); by-id endpoints return 403 `SUBJECT_OUT_OF_SCOPE`; admin bypasses the scope.
- [ ] Only the claimer can reply; the reply sets `Answered`; codes and statuses match "Error codes".
- [ ] Student list/thread results carry `hasUnreadReply`; `POST /api/teacher-threads/{id}/read` clears it and does not change the thread's xmin (T57).
- [ ] Every endpoint has a `DefaultCodes` policy; 7 new codes in both resx files and both web `common:errors` files.
- [ ] `AskTeacherOptions.ReplyTextMaxLength` in code (4000), `appsettings.example.json`, `deploy/api.env.example`, `docs/deployment.md`.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` regenerated with no drift; `orval.config.ts` has the `GetTeacherInbox` zod override; `routeTree.gen.ts` regenerated.
- [ ] Postman `TeacherInbox` folder + "Mark my thread read" added in the stated order.
- [ ] Web: `/teacher/inbox` and `/teacher/thread/$threadId` render with the copy in "Web copy", loading/empty/error/RTL states, tokens only (no raw colours).
- [ ] Every test T1–T58 and W1–W22 exists with the listed name and passes; no existing test is edited except the listed additions, `askTeacherFixtures.ts` defaults and the migration list.
- [ ] `dotnet test api/ -c Release` green with `appsettings.json` moved aside; `dotnet format --verify-no-changes` clean outside `core-libraries`.
- [ ] `npm --prefix web run typecheck`, `lint`, `npx vitest run`, and `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` pass.
- [ ] Docs updated as listed (`docs/ask-teacher.md`, PRD §15, `docs/claude-design-prompt.md` §4, `docs/prototype.md`, `docs/deployment.md`); no doc outside `/docs`.
- [ ] Guard grep over the diff prints nothing (`DateTime.Now|UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`, `async void`).
