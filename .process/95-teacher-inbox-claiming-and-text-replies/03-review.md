VERDICT: APPROVED

# Review — [E9.S2] Teacher inbox, claiming and text replies (#95)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Application/TeacherInbox/GetTeacherInbox/GetTeacherInboxValidator.cs:15`: the `IsInEnum` rule can never be reached over HTTP. Model binding refuses an unknown `filter` with 400 first, so the `TEACHER_INBOX_FILTER_INVALID` resx and web strings are never shown. The implementer disclosed this as a deviation, and `docs/ask-teacher.md` § Teacher inbox matches the 400 behaviour. It is harmless, and the web only ever sends valid enum values.
- `api/Elmanhg.Application/TeacherInbox/Shared/TeacherInboxResultGenerator.cs:23` together with `web/src/features/askTeacher/components/InboxThreadActions.tsx:11-15` and `InboxListItem.tsx:18-22`: if the claiming user is missing from `names` (for example, a soft-deleted teacher filtered out by the global filter), `teacherName` is null. The list then shows «غير مُستلم» and the thread page shows «تم الرد على هذا السؤال.» on an `Open` thread that nobody can claim. It is an edge case, but branching on `claimedAt` rather than `teacherName` would be more robust.
- `api/Elmanhg.Domain/TeacherThreads/TeacherThread.Replies.cs:10`: if the same teacher claims twice truly concurrently (both loaded unclaimed), the second request gets 409 `TEACHER_THREAD_MODIFIED_CONCURRENTLY`, not the idempotent no-op. The web disables the button while the claim is pending, and the error path refetches and shows the thread as claimed by the teacher, so the effect is only a spurious toast.
- `web/src/features/askTeacher/hooks/useReplyToThread.ts:22-26`: on a 409 the thread is refetched. In real use the refetched thread has `canReply=false`, so `ReplyForm` unmounts and its root error alert goes with it. The teacher sees only the "answered"/"claimed by other" note, without the server message. W22 does not show this because its MSW GET keeps returning the claimed thread. A toast on 409 would keep the message visible.
- `api/Elmanhg.Tests/Application/Features/TeacherInbox/*HandlerTests.cs`: these use `Substitute.For<TimeProvider>()` where testing convention §1 says `FakeTimeProvider`. This was disclosed, and 40+ sibling handler tests do the same.
- `web/src/features/askTeacher/components/InboxFilterTabs.tsx:31`: the active tab is text-filled (copied from `PaymentLogTabs`, as the plan said), while `.claude/design-system.md` SubTabs specifies active = surface + border.strong. All classes are tokens, so this is not a literal-value violation.

## Verified
- Gates, run by me:
  - `dotnet test api/ -c Release` with `appsettings.json` moved aside, then restored: **2917/2917 passed**.
  - Web `typecheck`: clean.
  - Web `lint`: clean.
  - Web `test -- --run`: **153 files / 902 tests passed**.
  - Prettier `--end-of-line auto`: clean.
- **Subject scope fails closed on every route:**
  - List: `TeacherInboxQueryShape.Filter` ANDs `subjectIds.Contains` with the tab predicate. An empty assignment gives an empty list (T14). `Mine` is still scoped (D9).
  - By id: GET, claim and reply all run `TeacherInboxAccess.EnsureCanAccessAsync` before any mutation. It returns 403 `SUBJECT_OUT_OF_SCOPE`, and only the Admin role bypasses it.
  - Photo media: the existing `CanViewTeacherThreadImageHandler.cs:25-30` checks `IsAssignedAsync` for teachers and returns false for any other role.
  - Pinned by T41 and T46, plus the handler 403 tests with `DidNotReceive` save.
- **Claiming:**
  - A sequential loser gets `TEACHER_THREAD_ALREADY_CLAIMED` from the domain (`TeacherThread.Replies.cs:15-18`).
  - A true race is decided by the xmin row version. The loser's `DbUpdateConcurrencyException` maps once, in `AppDbContext.cs:95-98`, with no handler try/catch.
  - T56 proves the stale-copy path on PostgreSQL. T45 fires two concurrent HTTP claims and asserts exactly one 200, one 409, and the DB owner equal to the winner.
- **T3/T5, checked by reading since the mutation classifier refused them:**
  - T3 fails if the `TeacherId is not null` guard in `Claim` is removed: the claim would overwrite, not throw, and the `TeacherId` assertion would fail.
  - T5 fails if the null check in `EnsureClaimedBy` is removed: null != teacherId would yield ALREADY_CLAIMED, not NOT_CLAIMED.
  - T6 covers the other-owner check.
- **Replies:**
  - The claim check runs before the status check (D4). Only the claimer can reply; an admin must claim first.
  - A reply adds a teacher Text message and sets `Answered`, `UpdatedBy` and `UpdationDate` (T4, T48).
  - Unclaimed → 409 NOT_CLAIMED (T5/T50). Claimed by another → 409 ALREADY_CLAIMED (T6/T51). Answered → 409 NOT_AWAITING_REPLY (T7). Blank → 422 (T49).
- **Read tracking:** `MarkRepliesRead` touches only `TeacherMessage.StudentReadAt` (the first read is kept). It never stamps the thread, so xmin is unchanged, which T57 pins on real PostgreSQL. A student read therefore cannot 409 a teacher's in-flight reply.
- **Leaks:**
  - Mark-read is owner-scoped in its predicate (`x.StudentId == userId`), so another student's thread gives 404 (T33, T54).
  - Student results gained only `hasUnreadReply`; no teacher identity or sender ids are exposed.
  - Teacher results expose only `DisplayName` for the student and the claimer.
  - `/teacher/*` web routes stay teacher-only.
- **Contract fidelity:**
  - Every file in Files to create (#1–#59) exists, and there are no extras beyond generated Orval/route-tree output.
  - Signatures match the plan.
  - The migration only adds 3 nullable columns, the TeacherId index, and the Restrict FK. It is the newest migration, and the `AppDbContextTests` list is updated.
- **Error codes:** the 7 codes are in both resx files and both web `common:errors` files.
- **Config:** `ReplyTextMaxLength` 4000 `[Range(1,20000)]` is in the options class, `appsettings.example.json`, `deploy/api.env.example` and `docs/deployment.md`.
- **Postman:** "Mark my thread read" is appended to AskTeacher. The new TeacherInbox folder has its 5 requests in the plan's order, with the right URLs and methods, collection bearer auth, a reply body, and `inboxThreadId` wired from Get inbox.
- **Docs sync:** there is no divergence.
  - `docs/ask-teacher.md` agrees with the code, including the 400 for an unknown filter.
  - Also consistent: PRD §15, `docs/claude-design-prompt.md` §4, `docs/prototype.md` items 7 and 9, and `docs/deployment.md`.
  - PRD §12.1 step 2 is honoured.
- **Deviations:** all 5 disclosed deviations are real and harmless.
- **Skills:**
  - .NET: records and handlers are sealed, namespaces are file-scoped, every await has ConfigureAwait(false), and DateTimeOffset is used throughout. There is no handler try/catch and a single save per handler. Every action has a policy. The only comment is the planned WHY comment.
  - React: data comes only from Orval hooks, with no fetch in useEffect (mark-read is a mutation fired via useEffectEvent). Only token classes are used, with logical properties. All strings go through t(). Schemas live in `schemas/` and are tested. URL search state is validated by Zod. Mutations invalidate or seed the cache.

## Test quality
- `TeacherThreadClaimAndReplyTests`: strong. Each guard has an exact-code assertion plus a state assertion.
- `TeacherInboxQueryShapeTests`: strong. The compiled predicate is exercised in and out of scope for every tab, plus the admin null and empty assignment cases, and the order is tested.
- `TeacherInboxResultGeneratorTests`: constrains the CanClaim/CanReply/IsClaimedByMe/names mapping per state.
- `GetTeacherInboxHandlerTests`: the captured filter is evaluated both ways. The admin test proves assignments are not read and the filter is unscoped.
- The GetInboxThread, ClaimTeacherThread, ReplyToTeacherThread and MarkTeacherThreadRead handler tests: the repository fake compiles the handler's own predicate, so scoping is really exercised. Every throwing path asserts type, code and DidNotReceive save. Every success path asserts the outcome and Received(1).
- Integration: T45/T56 prove the race, T57 proves reads never change xmin, and the scope tests seed real out-of-scope data.
- Web: W15 and W21 use stateful MSW to prove the refetch and the read receipt. W17 relies on `onUnhandledRequest: 'error'` to prove no request is sent.
- No vacuous tests found.
