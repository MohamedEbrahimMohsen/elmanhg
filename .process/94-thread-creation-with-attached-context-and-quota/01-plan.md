# Plan — [E9.S1] Thread creation with attached context and quota (#94)

## Goal
A student who holds the Ask a Teacher add-on (on top of an entitled Base) can open «اسأل معلّم» from a lesson page or a quiz answer. The subject, unit, lesson and, when there is one, the question are attached automatically. From the Ask a Teacher tab the student can also pick a lesson by hand. They write a question, may attach one photo of their work, and send it. The server enforces the monthly quota and starts the SLA clock (`SlaDueAt = SubmittedAt + AskTeacherReplySlaHours`). The student then sees their threads, newest first, with status badges (awaiting with hours left, overdue, answered, closed), the monthly allowance «الرصيد الشهري: X / N», and a thread view. This story creates the `TeacherThread`/`TeacherMessage` model that #95 (inbox, claim, text replies, follow-up, rating), #96 (voice + transcription) and #97 (SLA reminders) extend.

## Scope
**In:**
- Domain `TeacherThread` (aggregate) and `TeacherMessage` (child), with a jsonb context snapshot, status, `SubmittedAt`, `SlaDueAt`, and an xmin version. Migration `AddTeacherThreads`.
- `POST /api/teacher-threads` (multipart). It checks the add-on entitlement and the monthly quota, resolves the context (lesson, question or attempt) and takes an optional image upload.
- `GET /api/teacher-threads/context` shows the context before sending. `GET /api/teacher-threads` is the student's paged list. `GET /api/teacher-threads/{id}` is the student's thread.
- `UsageResult` gains the Ask a Teacher monthly counters.
- Web pages: `/student/ask` (list, allowance, upsell), `/student/ask-new` (compose with auto-attached context or a lesson picker, image, SLA note) and `/student/thread/$threadId` (thread view). There is also an «اسأل معلّم» link on the lesson page and on quiz feedback.
- Docs: `docs/ask-teacher.md` (new), PRD §12.1/§15/§20, `docs/subscriptions.md`, `docs/claude-design-prompt.md` §4, `docs/prototype.md`.
- Postman folder `AskTeacher`.

**Out** (belongs to later stories, not deferred):
- #95: teacher inbox, claiming (`TeacherId`, `ClaimedAt`), teacher text replies, the student follow-up, rating and closing (`ClosedAt`, `Rating`), `SubjectScopeBehaviour` on teacher reads, and the 409 mapping for `TeacherThread` concurrency.
- #96: voice (`AudioUrl`, `TranscriptFinal`) and transcription.
- #97: SLA reminders at 12h/20h and the breach alert.
- Avatar handoff (#91).

**Deferred:**
- D1 — S3-compatible storage adapter (`FileStorageProvider.S3`, MinIO in compose, private bucket + presigned GET URLs). It needs a new SDK package plus compose/env changes, which collide with the parallel #112 hosting lane that owns compose right now. #96 (voice notes) is the natural home. Until then, thread images use the existing `Local` provider and are served from `/api/media` behind unguessable capability URLs (see Decision 9).

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | What context can be attached? | Exactly one of `lessonId`, `questionId`, `attemptId`; otherwise 422 `TEACHER_THREAD_CONTEXT_INVALID`. `Guid.Empty` counts as invalid. The server resolves it to subject → unit → lesson (+ question). | PRD §12.1 says "lesson or question". The orchestrator adds "attempt". An attempt is the richest context from quiz feedback (the teacher can see what the student answered, #95). |
| 2 | How is the context stored? | A jsonb `Context` string holding the `TeacherThreadContext` snapshot: ids + subject/unit/lesson names + question id/version/stem + attempt id. The `SubjectId` column is duplicated for inbox routing. | Mirrors `Session.Scope`. A snapshot keeps the thread readable after content edits. The ids feed the training export (PRD §13). |
| 3 | Which stem goes with an attempt? | The stem of the served revision (`QuestionRevision` at `attempt.QuestionVersion`). A missing revision gives 404 `QUESTION_NOT_FOUND`, as in `SaveExamAnswerHandler`. | The student asks about what they saw, not a later edit. |
| 4 | Visibility rules | Lesson context: the lesson must be Published, else 404 `LESSON_NOT_FOUND`. Question context: the question must be servable (`ServableQuestionSpecification.IsSatisfiedBy`), else 404 `QUESTION_NOT_FOUND`. Attempt context: the attempt must be the student's, else 404 `ATTEMPT_NOT_FOUND`. The question does not have to be servable now (already seen), but its lesson must still be Published (`LESSON_NOT_FOUND`). | Student reads return Published lessons only (PROGRESS: Conventions). Another student's attempt returns 404, never 403 (BOLA). |
| 5 | What is "a month" for the quota? | The calendar month in `Subscriptions:DailyQuotaTimeZone` (Africa/Cairo). Count threads with `StudentId == me && SubmittedAt ∈ [monthStartUtc, nextMonthStartUtc)`. Follow-ups (#95) do not count. | PRD says "N questions/month". This matches the existing quota time-zone setting. Billing-period windows break on early renewal (`Renew` sets `CurrentPeriodStart` = the old end, in the future). |
| 6 | Quota strictness | Soft limit, like the quiz quota: two parallel creates at N−1 can both pass. Documented as a known limit. | Same accepted trade-off as #87. No lock infrastructure exists. |
| 7 | Status codes and gate order | Validation 422 → user 401 → no add-on 403 `ASK_TEACHER_REQUIRES_SUBSCRIPTION` → quota 403 `ASK_TEACHER_MONTHLY_LIMIT_REACHED` (context `limit`) → context 404s → store the image → save. | Mirrors `FreeTierGate` (403 for entitlement limits). Nothing is stored when a check fails. |
| 8 | How is the image uploaded? | In the same multipart `POST /api/teacher-threads` (field `image`), not a separate upload endpoint. One image per message. Allowed: `.png .jpg .jpeg .webp` with matching content types, at most `AskTeacher:ImageMaxSizeInMb` (5). No GIF, no SVG. | No orphan uploads from abandoned forms, and no client-supplied URL to validate (URL forgery or SSRF). Reuses the `UploadLessonImage` pattern (Morabh `UploadDownPaymentInstaPayImage*`). |
| 9 | Image storage and serving | `IFileStorage.SaveAsync(stream, "teacher-threads/{Guid:N}{ext}")` on the Local provider. The URL is served by the existing `/api/media` static files with `nosniff`, and `TeacherMessage.ImageUrl` stores it. It is a capability URL (122-bit random key). | Reuses the only existing storage path. The private-bucket/presigned alternative arrives with D1. Recorded in docs as a known limit. |
| 10 | When is the image written? | After every check passes, just before `SaveChangesAsync`. A failed DB save leaves an orphan file, which is accepted (no cleanup job). | Nothing is stored for rejected requests. |
| 11 | Status model | `TeacherThreadStatus { Open, Answered, Closed }` is defined now. #94 only produces `Open`. `IsOverdueAt(now)` = `Status == Open && now >= SlaDueAt` and is computed server-side into `isOverdue`. | The state machine is documented for #95/#97. The web renders all three now (fixture-tested). The server clock is the source of truth. |
| 12 | Which columns ship now? | Only what #94 writes: StudentId, SubjectId, Context, Status, SubmittedAt, SlaDueAt, Version (xmin, no physical column); message Id, ThreadId, SenderId, Kind, Text, ImageUrl, CreatedAt. TeacherId/ClaimedAt/ClosedAt/Rating/AudioUrl/TranscriptFinal come with #95/#96 in their own migrations. | No dead columns or untested branches. PRD §15 is updated to show the target. |
| 13 | Timestamps | `Submit` truncates `submittedAt` to microseconds (WHY comment). | PostgreSQL keeps microseconds, so the create response equals what later GETs return (same reason as `Session`). |
| 14 | Auditing | Not audited: no `IAuditableCommand` on the command and no `IAuditedEntity` on the entities. | Audit covers content changes and validation decisions (PRD §14). A student question is activity, and the row itself is the record. |
| 15 | Authorisation | All four endpoints use `DefaultCodes.AskTeacherSubmit` (Student only; the policy exists). Reads are owner-scoped: another student's thread returns 404 `TEACHER_THREAD_NOT_FOUND`. | PRD §16. Teacher/admin reads come in #95 through their own controller. |
| 16 | Where do the counters live? | `UsageResult` gains `MonthlyAskTeacherQuestionLimit`, `AskTeacherQuestionsUsedThisMonth` and `AskTeacherQuestionsRemainingThisMonth` (never below 0). The limit is 0 without the add-on. | `docs/subscriptions.md` → "For later stories" says #94 adds its counters to `UsageResult` through `StudentEntitlementLoader`. |
| 17 | Options | New `AskTeacherOptions` (section `AskTeacher`) with code defaults: `QuestionTextMaxLength` 2000, `ImageMaxSizeInMb` 5, `ThreadListMaxPageSize` 50. Quota and SLA stay in `SubscriptionsOptions`. | Caps live in Options (skill §8.1). Code defaults need no test-factory change (CI parity). |
| 18 | Web routes | Flat: `/student/ask`, `/student/ask-new?lessonId=&questionId=&attemptId=`, `/student/thread/$threadId`. The list page uses search param `page`. | Prototype route names (`ask-new`, `thread/:id`). Search params instead of `:lid/:qid` path segments. Flat files avoid turning `ask.tsx` into a layout. |
| 19 | Web entry points | Lesson page (not locked): `AskTeacherLink lessonId`. Quiz feedback: `AskTeacherLink attemptId` next to «اسأل المساعد». Exam result: none. | Matches prototype lines 441/457. Exam review has only the avatar in the prototype. |
| 20 | Compose without context | Subject select (from `GET /api/mastery/overview` → `subjects`) + lesson select grouped by unit (`<optgroup>`, from `GET /api/mastery/subjects/{id}`). No new endpoint. | Both endpoints exist, are Student-policied and return Published lessons only. |
| 21 | Upsell copy | «هذه الخدمة إضافة مدفوعة وتتطلب الباقة الأساسية.» + a «الاشتراك» button to `/student/subscription`. The price is not shown. | Prices are configuration. The subscription page already shows the price. |
| 22 | SLA note hours | From `GET /api/plans` → `askTeacher.replySlaHours`. The note is omitted while the catalogue loads or on error. | No magic "24" in the web. The note is non-critical, so it needs no extra loading/error UI. |
| 23 | "Time left" format | Whole hours rounded up (`remainingHours`), ICU plural. The value is frozen per render with `useState(() => new Date())`. | Matches the prototype badge «بانتظار الرد · متبقٍ …». The React Compiler purity rule forbids `Date.now()` in render. |
| 24 | Teacher line on the thread card | Omitted until #95 introduces claiming. | No teacher data exists in #94. |
| 25 | Parallel lanes (#90, #112) | Insert into shared files at the anchors named below (not at file ends). `AppDbContextModelSnapshot.cs`, `api/openapi/v1.json`, `web/src/shared/api/generated/**`, `web/src/routeTree.gen.ts` and the migration list in `AppDbContextTests` are regenerated or re-merged on rebase. If #90's migration lands first, re-create `AddTeacherThreads` after rebase so its timestamp sorts last, and append its entry after #90's. | Keeps hunks apart and keeps migrations self-contained. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Add group `// TEACHER THREADS` **before** `// SUBSCRIPTIONS`: `public const string TeacherMessageTextRequired = "TEACHER_MESSAGE_TEXT_REQUIRED";` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add group `// ASK A TEACHER` **after** the `// STUDENTS` group (before `// ANALYTICS`): the 11 constants in "Error codes". |
| `api/Elmanhg.Domain/Sessions/ISessionRepository.cs` | Add `Task<Attempt?> GetStudentAttemptAsync(Guid attemptId, Guid studentId, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/Sessions/SessionRepository.cs` | Implement it: `_context.Set<Attempt>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == attemptId && x.StudentId == studentId, cancellationToken).ConfigureAwait(false)`. |
| `api/Elmanhg.Application/Subscriptions/Shared/UsageResult.cs` | Append 3 positional params: `int MonthlyAskTeacherQuestionLimit, int AskTeacherQuestionsUsedThisMonth, int AskTeacherQuestionsRemainingThisMonth`. |
| `api/Elmanhg.Application/Subscriptions/GetMyUsage/GetMyUsageHandler.cs` | Constructor gains `ITeacherThreadRepository teacherThreadRepository` (right after `ISessionRepository sessionRepository`). After `used`: `var askTeacherUsed = await AskTeacherGate.CountQuestionsThisMonthAsync(userId, teacherThreadRepository, options, now, cancellationToken).ConfigureAwait(false);`. Return adds `entitlement.MonthlyAskTeacherQuestionLimit, askTeacherUsed, Math.Max(0, entitlement.MonthlyAskTeacherQuestionLimit - askTeacherUsed)`. |
| `api/Elmanhg.Application/DependencyInjection.cs` | After the `StudentsOptions` line: `services.AddOptions<AskTeacherOptions>().BindConfiguration(AskTeacherOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | After `services.AddScoped<IPaymentRepository, PaymentRepository>();`: `services.AddScoped<ITeacherThreadRepository, TeacherThreadRepository>();` (+ usings). |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `using Elmanhg.Domain.TeacherThreads;`. DbSets after `Payments`: `public DbSet<TeacherThread> TeacherThreads { get; set; }`, `public DbSet<TeacherMessage> TeacherMessages { get; set; }`. In `OnModelCreating`, add `ConfigureTeacherThreads(modelBuilder);` right after `ConfigureSubscriptions(modelBuilder);`. New private static `ConfigureTeacherThreads` placed right after the `ConfigureSubscriptions` method (body in "Domain behaviour → Mapping"). In `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`, after the `Payment` line: `modelBuilder.Entity<TeacherThread>().HasQueryFilter(x => !x.IsDeleted);` and `modelBuilder.Entity<TeacherMessage>().HasQueryFilter(x => !x.IsDeleted);` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add AddTeacherThreads -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | 12 new `<data>` entries inserted after `SUBJECT_INTERESTS_DUPLICATE` (strings in "Error codes"). |
| `api/Elmanhg.Api/appsettings.example.json` | After the `"Students"` line: `"AskTeacher": { "QuestionTextMaxLength": 2000, "ImageMaxSizeInMb": 5, "ThreadListMaxPageSize": 50 },` |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/GetMyUsage/GetMyUsageHandlerTests.cs` | **modify**: add `ITeacherThreadRepository` substitute (`CountAsync` returns 0 by default) to the constructor call. Update `Handle_FreeStudent_ReturnsLimitUsedAndRemaining` to `new UsageResult(PlanTier.Free, false, 10, 3, 7, 5, 0, 0, 0)`. Add 2 tests (Test plan). |
| `api/Elmanhg.Tests/Integration/Subscriptions/UsageEndpointTests.cs` | **modify**: add 1 test (Test plan). |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | **modify**: append `twentyFifth => twentyFifth.Should().EndWith("_AddTeacherThreads")`. |
| `postman/elmanhg.postman_collection.json` | New folder `AskTeacher` right after `Subscriptions` (4 requests, below) + collection variable `teacherThreadId` = "". |
| `web/src/routes/student/ask.tsx` | Replace the placeholder: `validateSearch: askTeacherListSearchSchema`, `component: AskTeacherListPage` (from `@/features/askTeacher`). |
| `web/src/features/browse/pages/LessonPage.tsx` | Inside the not-locked fragment, after `<Outlet />`: `<AskTeacherLink lessonId={lessonId} />` (import from `@/features/askTeacher`). |
| `web/src/features/quiz/components/FeedbackPanel.tsx` | Add optional prop `children?: ReactNode`. Replace the bare `<AskAvatarButton />` with `<div className="flex flex-wrap items-start gap-2"><AskAvatarButton />{children}</div>`. |
| `web/src/features/quiz/components/QuizQuestionCard.tsx` | `<FeedbackPanel …><AskTeacherLink attemptId={attempt.id} /></FeedbackPanel>`. |
| `web/src/app/i18n.ts` | `import { askTeacherLocales } from '@/features/askTeacher/locales';` and `askTeacher: askTeacherLocales.ar` / `.en` right after the `onboarding` entries. |
| `web/src/shared/i18n/ar.json`, `en.json` | `errors`: 12 new codes after `EXAM_REQUIRES_SUBSCRIPTION` (same text as the resx; the web may keep the shadda in «معلّم»). |
| `web/src/test/subscriptionFixtures.ts` | **modify** `freeUsage`/`baseUsage`: add `monthlyAskTeacherQuestionLimit: 0, askTeacherQuestionsUsedThisMonth: 0, askTeacherQuestionsRemainingThisMonth: 0`. |
| `web/src/shared/api/generated/**` | `npm --prefix web run gen:api`. |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (`npx vite build` or dev). |
| `docs/PRD.md` | §12.1 step 1: add «or the quiz attempt being asked about». New sentence after step 1: "Each new question counts against the monthly quota, which resets on the 1st of each calendar month in `DailyQuotaTimeZone` (Africa/Cairo); a follow-up does not count. The student may attach one photo (PNG, JPG or WEBP)." §15: `TeacherThread(id, student_id, teacher_id?, subject_id, context_json, status[Open\|Answered\|Closed], submitted_at, sla_due_at, closed_at?, rating?)  -- docs/ask-teacher.md` and `TeacherMessage(id, thread_id, sender_id, kind[Text\|Voice], text, image_url?, audio_url?, transcript_final bool, created_at)`. §20 Context bundle: append "; a teacher thread also keeps the attempt id when asked from a quiz answer". |
| `docs/subscriptions.md` | API table `usage` row: add the 3 fields. Add a new subsection `## Ask a Teacher quota` after "Free tier gates" (gate order, calendar month, soft limit, 403 codes, "see docs/ask-teacher.md"). "For later stories": change the #94 mention to "**#94** (done): Ask a Teacher counters in `UsageResult` and the create gate (`AskTeacherGate`)". |
| `docs/claude-design-prompt.md` | §4 lesson bullet: replace "…"اسأل معلّم" arrive with the Avatar and Ask a Teacher stories." with "…"اسأل المساعد عن الدرس" arrives with the Avatar story; "اسأل معلّم" opens the new-question page with the lesson attached." Quiz bullet: add «"اسأل معلّم"» after «"اسأل المساعد"». Ask bullet: `#/student/ask` threads list with the monthly allowance «الرصيد الشهري: X / N» and an upsell without the add-on; `#/student/ask-new` (lesson picker) and `#/student/ask-new?lessonId|questionId|attemptId` compose with auto-attached context and an optional photo; `#/student/thread/:id` thread view (follow-up and 1 to 5 rating arrive with teacher replies). |
| `docs/prototype.md` | Walkthrough item 7: append "The product attaches the lesson, or the quiz attempt, automatically, lets the student pick a lesson when there is no context, counts questions per calendar month, and accepts one photo (PNG, JPG or WEBP); the prototype simulates the photo with a checkbox." |

## Files to create

### Domain (`api/Elmanhg.Domain/TeacherThreads/`, namespace `Elmanhg.Domain.TeacherThreads`) — new, no Morabh equivalent
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `TeacherThreadStatus.cs` | enum | `public enum TeacherThreadStatus { Open, Answered, Closed }` |
| 2 | `TeacherMessageKind.cs` | enum | `public enum TeacherMessageKind { Text, Voice }` |
| 3 | `TeacherThreadContext.cs` | sealed record | `public sealed record TeacherThreadContext(Guid SubjectId, string SubjectName, Guid UnitId, string UnitName, Guid LessonId, string LessonName, Guid? QuestionId, int? QuestionVersion, string? QuestionStem, Guid? AttemptId)` + `public string ToJson() => JsonSerializer.Serialize(this, QuestionJson.SerializerOptions);` + `public static TeacherThreadContext FromJson(string json) => JsonSerializer.Deserialize<TeacherThreadContext>(json, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Teacher thread context is empty.");` (mirror `QuizScope`) |
| 4 | `TeacherThread.cs` | class `TeacherThread : AuditEntity` | Body in "Domain behaviour". |
| 5 | `TeacherMessage.cs` | class `TeacherMessage : Entity` | Body in "Domain behaviour". |
| 6 | `ITeacherThreadRepository.cs` | interface | `public interface ITeacherThreadRepository : IRepository<TeacherThread> { }` (the base covers `CountAsync`, `FindPaginatedAsync`, `FirstOrDefaultAsync`, `AddAsync`). |

### Application
| # | Path | Type | Contract |
|---|------|------|----------|
| 7 | `api/Elmanhg.Application/Shared/Options/AskTeacherOptions.cs` | sealed class | `namespace Elmanhg.Application.Shared.Options; public const string SectionName = "AskTeacher"; [Range(1, 20000)] public int QuestionTextMaxLength { get; set; } = 2000; [Range(1, 20)] public int ImageMaxSizeInMb { get; set; } = 5; [Range(1, 100)] public int ThreadListMaxPageSize { get; set; } = 50;` |
| 8 | `api/Elmanhg.Application/TeacherThreads/Shared/AskTeacherGate.cs` | static class | ns `Elmanhg.Application.TeacherThreads.Shared`. New (shape mirrors `FreeTierGate`). `public static (DateTimeOffset Start, DateTimeOffset End) CurrentQuotaMonth(DateTimeOffset now, string timeZone)`: zone = `TimeZoneInfo.FindSystemTimeZoneById(timeZone)`; local = `TimeZoneInfo.ConvertTime(now, zone)`; start = `MonthStart(local.Year, local.Month, zone)`; end = `local.Month == 12 ? MonthStart(local.Year + 1, 1, zone) : MonthStart(local.Year, local.Month + 1, zone)`. `private static DateTimeOffset MonthStart(int year, int month, TimeZoneInfo zone)`: `var wall = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified); return new DateTimeOffset(wall, zone.GetUtcOffset(wall)).ToUniversalTime();`. WHY comment: Npgsql writes only UTC offsets to timestamptz. `public static async Task<int> CountQuestionsThisMonthAsync(Guid studentId, ITeacherThreadRepository teacherThreadRepository, SubscriptionsOptions options, DateTimeOffset now, CancellationToken cancellationToken)` → `var (start, end) = CurrentQuotaMonth(now, options.DailyQuotaTimeZone); return await teacherThreadRepository.CountAsync(cancellationToken, x => x.StudentId == studentId && x.SubmittedAt >= start && x.SubmittedAt < end).ConfigureAwait(false);`. `public static async Task EnsureCanAskAsync(EntitlementResult entitlement, Guid studentId, ITeacherThreadRepository teacherThreadRepository, SubscriptionsOptions options, DateTimeOffset now, CancellationToken cancellationToken)`: `if (!entitlement.HasAskTeacher) throw new ForbiddenCoreException(ErrorCodes.AskTeacherRequiresSubscription);` then `used = await CountQuestionsThisMonthAsync(...)`; `if (used >= entitlement.MonthlyAskTeacherQuestionLimit) throw new ForbiddenCoreException(ErrorCodes.AskTeacherMonthlyLimitReached, context: new Dictionary<string, object> { ["limit"] = entitlement.MonthlyAskTeacherQuestionLimit });` |
| 9 | `.../TeacherThreads/Shared/TeacherThreadContextSources.cs` | sealed record | `public sealed record TeacherThreadContextSources(ILessonRepository Lessons, ICurriculumUnitRepository Units, ISubjectRepository Subjects, IQuestionRepository Questions, ISessionRepository Sessions);` |
| 10 | `.../TeacherThreads/Shared/TeacherThreadContextResolver.cs` | `public static partial class TeacherThreadContextResolver` | `public static async Task<TeacherThreadContext> ResolveAsync(Guid? lessonId, Guid? questionId, Guid? attemptId, Guid studentId, TeacherThreadContextSources sources, CancellationToken cancellationToken)`. Ordered steps: (1) `var part = attemptId is { } attempt ? await ResolveAttemptAsync(attempt, studentId, sources, cancellationToken).ConfigureAwait(false) : questionId is { } question ? await ResolveQuestionAsync(question, sources, cancellationToken).ConfigureAwait(false) : null;` (2) `var lesson = await sources.Lessons.GetByIdAsync(part?.Question.LessonId ?? lessonId ?? Guid.Empty, cancellationToken, asNoTracking: true)`; (3) `if (part is { AttemptId: null } && (lesson is null \|\| !ServableQuestionSpecification.IsSatisfiedBy(part.Question, lesson))) throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);` (4) `if (lesson is null \|\| lesson.State != LessonState.Published) throw new NotFoundCoreException(ErrorCodes.LessonNotFound);` (5) `unit = await sources.Units.GetByIdAsync(lesson.UnitId, …, asNoTracking: true) ?? throw NotFound(LessonNotFound)`; (6) `subject = await sources.Subjects.GetByIdAsync(unit.SubjectId, …, asNoTracking: true) ?? throw NotFound(LessonNotFound)`; (7) `return new TeacherThreadContext(subject.Id, subject.Name, unit.Id, unit.Name, lesson.Id, lesson.Name, part?.Question.Id, part?.Version, part?.Stem, part?.AttemptId);` |
| 11 | `.../TeacherThreads/Shared/TeacherThreadContextResolver.Questions.cs` | partial of #10 | `private sealed record QuestionPart(Question Question, int Version, string Stem, Guid? AttemptId);` `private static async Task<QuestionPart> ResolveAttemptAsync(Guid attemptId, Guid studentId, TeacherThreadContextSources sources, CancellationToken cancellationToken)`: attempt = `sources.Sessions.GetStudentAttemptAsync(attemptId, studentId, …)` ?? NotFound(`AttemptNotFound`); question = `sources.Questions.GetByIdAsync(attempt.QuestionId, …, asNoTracking: true)` ?? NotFound(`QuestionNotFound`); revision = `(await sources.Questions.GetRevisionsAsync([question.Id], …)).FirstOrDefault(x => x.Version == attempt.QuestionVersion)` ?? NotFound(`QuestionNotFound`); return `new(question, attempt.QuestionVersion, revision.ReadSnapshot().Stem, attempt.Id)`. `private static async Task<QuestionPart> ResolveQuestionAsync(Guid questionId, TeacherThreadContextSources sources, CancellationToken cancellationToken)`: question ?? NotFound(`QuestionNotFound`); return `new(question, question.Version, question.Stem, null)`. |
| 12 | `.../TeacherThreads/Shared/TeacherThreadContextRules.cs` | static class | `public static bool HasExactlyOne(Guid? lessonId, Guid? questionId, Guid? attemptId)` → `Guid?[] ids = [lessonId, questionId, attemptId]; return ids.Count(x => x is not null) == 1 && ids.All(x => x != Guid.Empty);` |
| 13 | `.../TeacherThreads/Shared/TeacherThreadContextResult.cs` | sealed record | `(Guid SubjectId, string SubjectName, Guid UnitId, string UnitName, Guid LessonId, string LessonName, Guid? QuestionId, int? QuestionVersion, string? QuestionStem, Guid? AttemptId)`. Client-facing; names are plain strings (no `LocalizedText` in this codebase). |
| 14 | `.../TeacherThreads/Shared/TeacherMessageResult.cs` | sealed record | `(Guid Id, bool IsFromStudent, TeacherMessageKind Kind, string Text, string? ImageUrl, DateTimeOffset CreatedAt)`. Client-facing. |
| 15 | `.../TeacherThreads/Shared/TeacherThreadResult.cs` | sealed record | `(Guid Id, TeacherThreadContextResult Context, TeacherThreadStatus Status, bool IsOverdue, DateTimeOffset SubmittedAt, DateTimeOffset SlaDueAt, List<TeacherMessageResult> Messages)`. Client-facing. |
| 16 | `.../TeacherThreads/Shared/TeacherThreadSummaryResult.cs` | sealed record | `(Guid Id, string SubjectName, string LessonName, string QuestionText, TeacherThreadStatus Status, bool IsOverdue, DateTimeOffset SubmittedAt, DateTimeOffset SlaDueAt)`. Client-facing. |
| 17 | `.../TeacherThreads/Shared/TeacherThreadResultGenerator.cs` | static class | `public static TeacherThreadResult Generate(TeacherThread thread, DateTimeOffset now)` → `new(thread.Id, GenerateContext(thread.ReadContext()), thread.Status, thread.IsOverdueAt(now), thread.SubmittedAt, thread.SlaDueAt, thread.Messages.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => GenerateMessage(thread, x)).ToList())`. `public static TeacherThreadSummaryResult GenerateSummary(TeacherThread thread, DateTimeOffset now)` → context = `thread.ReadContext()`; QuestionText = first message where `SenderId == thread.StudentId`, ordered by `CreatedAt` then `Id` (`.First().Text`). `public static TeacherThreadContextResult GenerateContext(TeacherThreadContext context)` (field copy). `private static TeacherMessageResult GenerateMessage(TeacherThread thread, TeacherMessage message)` → `IsFromStudent = message.SenderId == thread.StudentId`. |
| 18 | `.../TeacherThreads/CreateTeacherThread/CreateTeacherThreadCommand.cs` | sealed record | `public sealed record CreateTeacherThreadCommand(string? Text, Guid? LessonId, Guid? QuestionId, Guid? AttemptId, IFormFile? Image) : IRequest<TeacherThreadResult>;` (not auditable, Decision 14) |
| 19 | `.../CreateTeacherThread/TeacherThreadImageFormats.cs` | static class | WHY comment: "Raster photos only: SVG can carry script and media is served from the API origin." `public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".webp"];` `public static readonly IReadOnlySet<string> ContentTypes = new HashSet<string>(["image/png", "image/jpeg", "image/webp"], StringComparer.OrdinalIgnoreCase);` (mirrors `LessonImageFormats`) |
| 20 | `.../CreateTeacherThread/CreateTeacherThreadValidator.cs` | sealed class `AbstractValidator<CreateTeacherThreadCommand>` | ctor `(IOptions<AskTeacherOptions> askTeacherOptions)`. Rules: `RuleFor(x => x.Text).Cascade(CascadeMode.Stop).ValidateRequired(ErrorCodes.TeacherThreadTextRequired).ValidateMaxLength(options.QuestionTextMaxLength, ErrorCodes.TeacherThreadTextTooLong);` · `RuleFor(x => x).Must(x => TeacherThreadContextRules.HasExactlyOne(x.LessonId, x.QuestionId, x.AttemptId)).WithErrorCode(ErrorCodes.TeacherThreadContextInvalid);` · `RuleFor(x => x.Image).ValidateAllowedExtensions(TeacherThreadImageFormats.Extensions, ErrorCodes.TeacherThreadImageTypeInvalid).ValidateMaxFileSize(options.ImageMaxSizeInMb, ErrorCodes.TeacherThreadImageTooLarge);` · `RuleFor(x => x.Image).Must(file => file is null \|\| TeacherThreadImageFormats.ContentTypes.Contains(file.ContentType)).WithErrorCode(ErrorCodes.TeacherThreadImageTypeInvalid);` |
| 21 | `.../CreateTeacherThread/CreateTeacherThreadHandler.cs` | sealed class | Morabh source for the upload step: `Morabh.Application/Files/UploadDownPaymentInstaPayImage/UploadDownPaymentInstaPayImageHandler.cs` (as already adapted in `UploadLessonImageHandler`). ctor (one line): `(ITeacherThreadRepository teacherThreadRepository, ISubscriptionRepository subscriptionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IQuestionRepository questionRepository, ISessionRepository sessionRepository, IFileStorage fileStorage, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<CreateTeacherThreadCommand, TeacherThreadResult>`. `Handle`: (1) user guard → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`; (2) `userId`, `now = timeProvider.GetUtcNow()`, `options = subscriptionsOptions.Value`; (3) `entitlement = await StudentEntitlementLoader.LoadAsync(subscriptionRepository, userId, options, now, cancellationToken)`; (4) `await AskTeacherGate.EnsureCanAskAsync(entitlement, userId, teacherThreadRepository, options, now, cancellationToken)`; (5) `context = await TeacherThreadContextResolver.ResolveAsync(request.LessonId, request.QuestionId, request.AttemptId, userId, new TeacherThreadContextSources(lessonRepository, unitRepository, subjectRepository, questionRepository, sessionRepository), cancellationToken)`; (6) `var imageUrl = request.Image is { } image ? await SaveImageAsync(image, cancellationToken).ConfigureAwait(false) : null;` where the private `SaveImageAsync` builds the key `$"teacher-threads/{Guid.NewGuid():N}{Path.GetExtension(image.FileName).ToLowerInvariant()}"`, does `await using var content = image.OpenReadStream();` and returns `await fileStorage.SaveAsync(content, key, cancellationToken)`; (7) `thread = TeacherThread.Submit(userId, context, request.Text ?? string.Empty, imageUrl, now, TimeSpan.FromHours(options.AskTeacherReplySlaHours))`; (8) `await teacherThreadRepository.AddAsync(thread, …)`, then `await teacherThreadRepository.SaveChangesAsync(…)` (once); (9) `return TeacherThreadResultGenerator.Generate(thread, now);`. Every await uses `.ConfigureAwait(false)`. |
| 22 | `.../TeacherThreads/GetTeacherThreadContext/GetTeacherThreadContextQuery.cs` | sealed record | `(Guid? LessonId, Guid? QuestionId, Guid? AttemptId) : IRequest<TeacherThreadContextResult>` |
| 23 | `.../GetTeacherThreadContext/GetTeacherThreadContextValidator.cs` | sealed class | One rule: `RuleFor(x => x).Must(x => TeacherThreadContextRules.HasExactlyOne(x.LessonId, x.QuestionId, x.AttemptId)).WithErrorCode(ErrorCodes.TeacherThreadContextInvalid);` |
| 24 | `.../GetTeacherThreadContext/GetTeacherThreadContextHandler.cs` | sealed class | ctor `(ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IQuestionRepository questionRepository, ISessionRepository sessionRepository, ICurrentUserService currentUserService)`. Steps: user guard; `context = await TeacherThreadContextResolver.ResolveAsync(…, userId, new TeacherThreadContextSources(…), ct)`; `return TeacherThreadResultGenerator.GenerateContext(context);`. No entitlement gate (a preview reveals only names). |
| 25 | `.../TeacherThreads/GetMyTeacherThreads/GetMyTeacherThreadsQuery.cs` | sealed record | `(int PageNumber = 1, int PageSize = 20) : IRequest<PageData<TeacherThreadSummaryResult>>` |
| 26 | `.../GetMyTeacherThreads/GetMyTeacherThreadsValidator.cs` | sealed class | ctor `(IOptions<AskTeacherOptions>)`. `RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.TeacherThreadPageNumberInvalid);` `RuleFor(x => x.PageSize).ValidateRange(1, options.ThreadListMaxPageSize, ErrorCodes.TeacherThreadPageSizeInvalid);` (mirrors `GetMyPaymentsValidator`) |
| 27 | `.../GetMyTeacherThreads/GetMyTeacherThreadsHandler.cs` | sealed class | ctor `(ITeacherThreadRepository teacherThreadRepository, TimeProvider timeProvider, ICurrentUserService currentUserService)`. Steps: user guard; `page = await teacherThreadRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: x => x.StudentId == userId, include: query => query.Include(x => x.Messages), orderBy: query => query.OrderByDescending(x => x.SubmittedAt).ThenByDescending(x => x.Id), asNoTracking: true)`; `now = timeProvider.GetUtcNow()`; map to `PageData<TeacherThreadSummaryResult>` with `GenerateSummary(x, now)` (shape of `GetMyPaymentsHandler`). |
| 28 | `.../TeacherThreads/GetMyTeacherThread/GetMyTeacherThreadQuery.cs` | sealed record | `(Guid ThreadId) : IRequest<TeacherThreadResult>` (no validator; `Guid.Empty` returns 404, like `GetMyPaymentQuery`) |
| 29 | `.../GetMyTeacherThread/GetMyTeacherThreadHandler.cs` | sealed class | ctor `(ITeacherThreadRepository teacherThreadRepository, TimeProvider timeProvider, ICurrentUserService currentUserService)`. Steps: user guard; `thread = await teacherThreadRepository.FirstOrDefaultAsync(x => x.Id == request.ThreadId && x.StudentId == userId, cancellationToken, include: query => query.Include(x => x.Messages), asNoTracking: true)`; null → `NotFoundCoreException(ErrorCodes.TeacherThreadNotFound)`; `return TeacherThreadResultGenerator.Generate(thread, timeProvider.GetUtcNow());` |

### Infrastructure / API
| # | Path | Type | Contract |
|---|------|------|----------|
| 30 | `api/Elmanhg.Infrastructure/TeacherThreads/TeacherThreadRepository.cs` | class | `public class TeacherThreadRepository(AppDbContext context) : Repository<TeacherThread>(context), ITeacherThreadRepository { }` |
| 31 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddTeacherThreads.cs` + `.Designer.cs` | generated | Must contain only `CreateTable TeacherThreads`, `CreateTable TeacherMessages`, their FKs and indexes. No Drop/Rename. |
| 32 | `api/Elmanhg.Api/Controllers/TeacherThreads/TeacherThreadsController.cs` | controller | See "API surface". `[ApiController] [Route("api/teacher-threads")] [Authorize] public class TeacherThreadsController(IMediator mediator) : ControllerBase`. |

### API tests (`api/Elmanhg.Tests/`)
| # | Path | Contract |
|---|------|----------|
| 33 | `Builders/TeacherThreadBuilder.cs` | `public sealed class TeacherThreadBuilder`; `public static readonly DateTimeOffset DefaultSubmittedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);` defaults: new student id, context `new TeacherThreadContext(Guid.NewGuid(), "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", null, null, null, null)`, text `"Why is F = ma?"`, no image, SLA 24h. Fluent `ForStudent(Guid)`, `WithContext(TeacherThreadContext)`, `SubmittedAt(DateTimeOffset)`, `WithImage(string url)`, `Build()` → `TeacherThread.Submit(...)`. |
| 34–44 | unit test classes | see Test plan |
| 45 | `Integration/TeacherThreads/TeacherThreadTestData.cs` | `public const string Route = "/api/teacher-threads";` `SignedInAskTeacherStudentAsync(ApiFactory)` → seeds a student + Base + AskTeacher (`SubscriptionBuilder().WithPlan(SubscriptionPlan.AskTeacher)`), both starting `UtcNow.AddDays(-1)`, returns `(User, HttpClient)`. `SeedPublishedLessonAsync(ApiFactory)` → `(Guid SubjectId, Guid LessonId)` via `ContentTestData`. `QuestionForm(string text, Guid? lessonId = null, Guid? attemptId = null, string? imageFileName = null, string? imageContentType = null)` → `MultipartFormDataContent` (image bytes `[0x89, 0x50, 0x4E, 0x47]`). `SeedThreadsAsync(ApiFactory, Guid studentId, Guid subjectId, int count, DateTimeOffset submittedAt)` (builder + context with the real subject id, added through `AppDbContext`). `ReadThreadsAsync(ApiFactory, Guid studentId)` → threads with messages from a fresh scope. |
| 46–48 | integration test classes | see Test plan |

### Docs
| # | Path | Contract |
|---|------|----------|
| 49 | `docs/ask-teacher.md` | Sections: **Model** (tables, columns, indexes, status state machine Open → Answered → (follow-up) Open → Answered → Closed, owned by #95), **Context** (the three kinds, resolution rules and 404s from Decision 4, snapshot fields), **Quota** (calendar month in `DailyQuotaTimeZone`, what counts, soft limit, gate order + codes), **Image** (formats, size, key, capability URL; the S3 adapter is deferred), **SLA** (`SlaDueAt`, `isOverdue`), **API** (the 4 endpoints and their errors), **Web** (routes, entry points, states), **For later stories** (#95: TeacherId/ClaimedAt/ClosedAt/Rating, claim concurrency → map `DbUpdateConcurrencyException` on `TeacherThread` to 409, `SubjectScopeBehaviour`; #96: AudioUrl/TranscriptFinal + S3 adapter; #97: an index on `(Status, SlaDueAt)` for the reminder sweep and a follow-up that resets `SlaDueAt`), **Known limits** (soft quota, public capability URLs, orphan file on a failed save). |

### Web (`web/src/features/askTeacher/`) — new feature, i18n namespace `askTeacher`
| # | Path | Contract |
|---|------|----------|
| 50 | `index.ts` | `export { AskTeacherListPage } from './pages/AskTeacherListPage'; export { AskTeacherNewPage } from './pages/AskTeacherNewPage'; export { TeacherThreadPage } from './pages/TeacherThreadPage'; export { AskTeacherLink } from './components/AskTeacherLink'; export { askTeacherListSearchSchema } from './schemas/askTeacherListSearchSchema'; export { askTeacherNewSearchSchema } from './schemas/askTeacherNewSearchSchema';` |
| 51 | `locales.ts` | `export const askTeacherLocales = { ar, en };` |
| 52–53 | `i18n/ar.json`, `i18n/en.json` | Keys in the "Web copy" table below. |
| 54 | `api/threadBadge.ts` | `export type ThreadBadge = 'closed' \| 'overdue' \| 'awaiting' \| 'answered'; export function threadBadge(thread: Pick<TeacherThreadSummaryResult, 'status' \| 'isOverdue'>): ThreadBadge` → `Closed`→closed; `isOverdue`→overdue; `Open`→awaiting; otherwise answered. |
| 56 | `api/remainingHours.ts` | `const millisecondsPerHour = 3_600_000; export function remainingHours(slaDueAt: string, now: Date): number` → `Math.max(0, Math.ceil((Date.parse(slaDueAt) - now.getTime()) / millisecondsPerHour))`. |
| 58 | `schemas/askTeacherFormSchema.ts` | `export function askTeacherFormSchema(requiresLesson: boolean)` → `z.object({ text: z.string().trim().min(1, { error: 'askTeacher:form.textRequired' }), lessonId: requiresLesson ? z.string().min(1, { error: 'askTeacher:form.lessonRequired' }) : z.string(), image: z.instanceof(File).nullable() })`; `export type AskTeacherFormValues = { text: string; lessonId: string; image: File \| null };` |
| 60 | `schemas/askTeacherListSearchSchema.ts` | `z.object({ page: z.coerce.number().int().min(1).optional().catch(undefined) })`; type `AskTeacherListSearch`. |
| 62 | `schemas/askTeacherNewSearchSchema.ts` | `z.object({ lessonId: z.guid().optional().catch(undefined), questionId: z.guid().optional().catch(undefined), attemptId: z.guid().optional().catch(undefined) })`; type `AskTeacherNewSearch`. |
| 64 | `hooks/useMyThreads.ts` | `export const threadListPageSize = 20; export function useMyThreads(page: number)` → `useGetMyTeacherThreads({ pageNumber: page, pageSize: threadListPageSize }, { query: { placeholderData: keepPreviousData } })`. |
| 65 | `hooks/useAskTeacherListSearch.ts` | `getRouteApi('/student/ask')`; returns `{ page: search.page ?? 1, setPage(page) }` (mirrors `useSubscriptionSearch`). |
| 66 | `hooks/useCreateThread.ts` | `export function useCreateThread(context: AskTeacherNewSearch)` → wraps `useCreateTeacherThread`. `onSuccess(result)`: `queryClient.setQueryData(getGetMyTeacherThreadQueryKey(result.id), result)`; `void queryClient.invalidateQueries({ queryKey: getGetMyTeacherThreadsQueryKey() })`; the same for `getGetMyUsageQueryKey()`; `toast.success(t('toast.sent'))`; `await navigate({ to: '/student/thread/$threadId', params: { threadId: result.id } })`. Returns `{ submit: (values: AskTeacherFormValues) => Promise<unknown> }`. `submit` sends `data` with `text`, `lessonId` (`context.lessonId ?? (values.lessonId \|\| undefined)`), `questionId: context.questionId`, `attemptId: context.attemptId`, `image: values.image ?? undefined`, dropping undefined keys. |
| 67 | `components/AskTeacherLink.tsx` | `export interface AskTeacherLinkProps { lessonId?: string; attemptId?: string }` → `<Button asChild variant="accent"><Link to="/student/ask-new" search={{ lessonId, attemptId }}><MessageCircleQuestion aria-hidden strokeWidth={1.8} className="size-4" />{t('link')}</Link></Button>`. |
| 68 | `components/AskTeacherUpsell.tsx` | A card (`rounded-lg border border-border bg-warning-soft p-4`): text `t('upsell.body')` + `<Button asChild size="sm" variant="accent"><Link to="/student/subscription">{t('upsell.cta')}</Link></Button>`. |
| 69 | `components/ThreadStatusBadge.tsx` | props `{ thread: Pick<TeacherThreadSummaryResult, 'status' \| 'isOverdue' \| 'slaDueAt'> }`. `const [now] = useState(() => new Date());` Classes: closed `bg-soft text-text-muted`, overdue `bg-danger text-surface`, awaiting `bg-soft text-text-muted`, answered `bg-success text-surface`, plus the base `inline-flex rounded-pill px-2.5 py-0.5 text-micro font-semibold` (mirrors `PaymentStatusBadge`). Text `t(`badge.${badge}`, { hours })`. |
| 70 | `components/ThreadListItem.tsx` | `<li>` holding a `<Link to="/student/thread/$threadId">` block (`rounded-md bg-surface border border-border px-3.5 py-3 flex items-start justify-between gap-3`). Title: the question text in `text-ui font-semibold line-clamp-2`. Meta caption: `t('list.meta', { subject, lesson, date })`, with the date via `formatDate(..., lng, 'arabic-indic', { dateStyle: 'medium', timeStyle: 'short' })`. Trailing `ThreadStatusBadge`. |
| 71 | `components/ThreadList.tsx` | Uses `useAskTeacherListSearch` + `useMyThreads(page)`. Pending → `ContentListSkeleton label={t('list.loading')}`. Error → `ContentErrorState title={t('list.errorTitle')} … onRetry={refetch}`. Empty → `ContentEmptyState message={t('list.empty')}` imported from `@/features/content` (this story adds that barrel export, see the row below the table). Otherwise `<ul className="flex flex-col gap-2">` of `ThreadListItem`, plus `Pagination` when `totalPages > 1`. |
| 72 | `components/ContextSummary.tsx` | props `{ context: TeacherThreadContextResult }` → caption `t('context.label')`, the line `t('context.path', { subject, unit, lesson })`, and when there is a `questionStem`: `t('context.question')` + `<RichTextViewer html={context.questionStem} />`. |
| 73 | `components/AttachedContext.tsx` | props `{ search: AskTeacherNewSearch }` → `useGetTeacherThreadContext({ lessonId, questionId, attemptId })` (undefined keys omitted). Pending → `ContentListSkeleton label={t('context.loading')}`; error → `ContentErrorState title={t('context.errorTitle')} … retry`; success → `ContextSummary`. |
| 74 | `components/LessonPicker.tsx` | Local `useState` subjectId. `useGetMasteryOverview()` → a subject `<select>` (label `t('picker.subject')`, first option `t('picker.chooseSubject')` value ""). `useGetSubjectMastery(subjectId)` (enabled only with an id) → lesson `<select>` bound to RHF `lessonId` via `useController<AskTeacherFormValues, 'lessonId'>`; one `<optgroup label={unit.name}>` per unit; first option `t('picker.chooseLesson')`; disabled until a subject is chosen or while loading. Overview pending → `ContentListSkeleton`; overview or subject error → `ContentErrorState` with retry. The error for `lessonId` is shown under the lesson select (`aria-invalid`, `aria-describedby`). Select classes copied from `PaymentLogSelectField`. |
| 75 | `components/ImageField.tsx` | `useController<AskTeacherFormValues, 'image'>`; `<input type="file" accept="image/png,image/jpeg,image/webp">` with `Label` `t('form.image')`; `onChange={(event) => { onChange(event.target.files?.[0] ?? null); }}`; the chosen file name as a caption; the error text `t([message, 'errors.UNHANDLED_EXCEPTION'])` with `aria-describedby`/`aria-invalid`. |
| 76 | `components/AskTeacherForm.tsx` | props `{ search: AskTeacherNewSearch; replySlaHours: number \| undefined }`. `hasContext = Boolean(search.lessonId ?? search.questionId ?? search.attemptId)`. `useForm<AskTeacherFormValues>({ resolver: zodResolver(askTeacherFormSchema(!hasContext)), defaultValues: { text: '', lessonId: '', image: null } })`. `<Form form onSubmit={submit} serverErrorFields={{ TEACHER_THREAD_TEXT_REQUIRED: 'text', TEACHER_THREAD_TEXT_TOO_LONG: 'text', TEACHER_THREAD_IMAGE_TYPE_INVALID: 'image', TEACHER_THREAD_IMAGE_TOO_LARGE: 'image' }}>` then: `hasContext ? <AttachedContext search /> : <LessonPicker />`, `<TextAreaField name="text" label={t('form.text')} description={t('form.textHint')} />`, `<ImageField />`, the SLA note `t('form.slaNote', { hours })` only when `replySlaHours` is defined, `<FormRootError />`, `<SubmitButton>{t('form.send')}</SubmitButton>`. |
| 77 | `components/ThreadContextCard.tsx` | props `{ thread: TeacherThreadResult }` → card (`rounded-lg border border-border bg-surface p-4 shadow-1 flex flex-col gap-2`): `ContextSummary` fields as «المادة: … · الدرس: …» (`t('thread.path', { subject, lesson })`), the question stem when present, `t('thread.due', { date })` + `ThreadStatusBadge`. |
| 78 | `components/ThreadMessage.tsx` | props `{ message: TeacherMessageResult }` → `<article>` bubble: student `bg-soft rounded-md p-3`, other `bg-surface border border-border rounded-md p-3`. Sender caption `t(message.isFromStudent ? 'thread.you' : 'thread.teacher')` · date. Text `whitespace-pre-wrap text-ui`. Image: `<img src={message.imageUrl} alt={t('thread.imageAlt')} loading="lazy" className="max-h-80 w-auto rounded-sm border border-border" />`. |
| 79 | `pages/AskTeacherListPage.tsx` | `<section className="flex flex-col gap-4">` + `<h1>` `t('list.title')`. `useGetMyUsage()`: pending → skeleton (`t('list.loading')`); error → `ContentErrorState` retry; `!hasAskTeacher` → `AskTeacherUpsell`; else a row `<Button asChild><Link to="/student/ask-new">{t('list.new')}</Link></Button>` + caption `t('list.allowance', { used, limit })` (numbers via `formatNumber`) + `<ThreadList />`. |
| 81 | `pages/AskTeacherNewPage.tsx` | props none; `getRouteApi('/student/ask-new').useSearch()`. `<h1>` `t('new.title')`. `useGetMyUsage()` states as in #79; `!hasAskTeacher` → `AskTeacherUpsell`; `askTeacherQuestionsRemainingThisMonth === 0` → notice card `t('new.quotaUsed', { limit })` + `<Link to="/student/ask">{t('new.backToList')}</Link>`; else `useGetPlanCatalogue()` and `<AskTeacherForm search={search} replySlaHours={catalogue.data?.askTeacher.replySlaHours} />`. |
| 84 | `pages/TeacherThreadPage.tsx` | props `{ threadId: string }` → `useGetMyTeacherThread(threadId)`. Pending → `ContentListSkeleton label={t('thread.loading')}`; error → `ContentErrorState title={t('thread.errorTitle')} … retry`. Success: `<nav aria-label={t('thread.breadcrumb')}>` with `<Link to="/student/ask">{t('list.title')}</Link>` › `t('thread.title')` (chevron `rtl:rotate-180`), `<h1>` `t('thread.title')`, `ThreadContextCard`, then the messages via `ThreadMessage` in a `flex flex-col gap-3` list. |
| 86 | `web/src/routes/student/ask-new.tsx` | `createFileRoute('/student/ask-new')({ validateSearch: askTeacherNewSearchSchema, component: AskTeacherNewPage })` |
| 87 | `web/src/routes/student/thread.$threadId.tsx` | `createFileRoute('/student/thread/$threadId')` with a component that reads `Route.useParams()` and renders `<TeacherThreadPage threadId={threadId} />` (mirrors `lesson.$lessonId.tsx`). |
| 88 | `web/src/test/askTeacherFixtures.ts` | `threadId`, `attemptId` (valid UUIDs); `threadContext(overrides?)`, `threadSummary(overrides?)`, `threadsPage(items, { pageNumber, totalPages })`, `teacherThread(overrides?)` (one student message with an `imageUrl`), `askTeacherUsage(overrides?)` (`baseUsage()` + `hasAskTeacher: true, monthlyAskTeacherQuestionLimit: 20, askTeacherQuestionsUsedThisMonth: 3, askTeacherQuestionsRemainingThisMonth: 17`), `subjectMasteryDetail()` (1 subject, 2 units × 1 lesson). |
| 55, 57, 59, 61, 63, 80, 82, 83, 85, 89, 90 | test files | see Test plan |

Also **modify** `web/src/features/content/index.ts`: add the `ContentEmptyState` export (row 71).

### Web copy (`askTeacher` namespace)
| Key | ar | en |
|---|---|---|
| `link` | اسأل معلّم | Ask a teacher |
| `upsell.body` | هذه الخدمة إضافة مدفوعة وتتطلب الباقة الأساسية. | This is a paid add-on and needs the Base plan. |
| `upsell.cta` | الاشتراك | Subscription |
| `list.title` | اسأل معلّم | Ask a teacher |
| `list.new` | سؤال جديد | New question |
| `list.allowance` | الرصيد الشهري: {used} / {limit} | Monthly allowance: {used} / {limit} |
| `list.loading` | جارٍ تحميل أسئلتك… | Loading your questions… |
| `list.errorTitle` | تعذّر تحميل أسئلتك | Could not load your questions |
| `list.empty` | لا توجد أسئلة. | No questions yet. |
| `list.meta` | {subject} / {lesson} · {date} | {subject} / {lesson} · {date} |
| `badge.closed` | مغلق | Closed |
| `badge.overdue` | متأخر | Overdue |
| `badge.awaiting` | بانتظار الرد · متبقٍ {hours, plural, zero {أقل من ساعة} one {ساعة واحدة} two {ساعتان} few {# ساعات} many {# ساعة} other {# ساعة}} | Awaiting reply · {hours, plural, one {# hour} other {# hours}} left |
| `badge.answered` | تم الرد | Answered |
| `new.title` | سؤال جديد للمعلّم | New question for a teacher |
| `new.quotaUsed` | استخدمت كل أسئلتك لهذا الشهر ({limit}). | You have used all {limit} questions for this month. |
| `new.backToList` | العودة إلى أسئلتي | Back to my questions |
| `context.label` | السياق المرفق تلقائيًا | Attached automatically |
| `context.path` | {subject} / {unit} / {lesson} | {subject} / {unit} / {lesson} |
| `context.question` | السؤال: | Question: |
| `context.loading` | جارٍ تحميل السياق… | Loading the context… |
| `context.errorTitle` | تعذّر تحميل السياق | Could not load the context |
| `picker.subject` | المادة | Subject |
| `picker.chooseSubject` | اختر المادة | Choose a subject |
| `picker.lesson` | الدرس | Lesson |
| `picker.chooseLesson` | اختر الدرس | Choose a lesson |
| `form.text` | سؤالك | Your question |
| `form.textHint` | اكتب سؤالك للمعلّم | Write your question for the teacher |
| `form.textRequired` | اكتب سؤالك | Write your question |
| `form.lessonRequired` | اختر الدرس | Choose a lesson |
| `form.image` | إرفاق صورة لحلّك (اختياري) | Attach a photo of your work (optional) |
| `form.slaNote` | سيُرسل السؤال لمعلّمي المادة، ومهلة الرد {hours} ساعة. | Your question goes to the subject's teachers, who reply within {hours} hours. |
| `form.send` | إرسال | Send |
| `toast.sent` | تم إرسال سؤالك. | Your question was sent. |
| `thread.title` | المحادثة | Conversation |
| `thread.breadcrumb` | مسار التنقل | Breadcrumb |
| `thread.path` | المادة: {subject} · الدرس: {lesson} | Subject: {subject} · Lesson: {lesson} |
| `thread.due` | موعد الرد: {date} | Reply due: {date} |
| `thread.you` | أنت | You |
| `thread.teacher` | المعلّم | Teacher |
| `thread.imageAlt` | صورة مرفقة بالسؤال | Photo attached to the question |
| `thread.loading` | جارٍ تحميل المحادثة… | Loading the conversation… |
| `thread.errorTitle` | تعذّر تحميل المحادثة | Could not load the conversation |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| Domain `TeacherMessageTextRequired` | `TEACHER_MESSAGE_TEXT_REQUIRED` | `TeacherMessage.CreateText` | `BusinessRuleViolationCoreException` | 400 |
| `TeacherThreadNotFound` | `TEACHER_THREAD_NOT_FOUND` | `GetMyTeacherThreadHandler` | `NotFoundCoreException` | 404 |
| `AttemptNotFound` | `ATTEMPT_NOT_FOUND` | `TeacherThreadContextResolver` | `NotFoundCoreException` | 404 |
| `AskTeacherRequiresSubscription` | `ASK_TEACHER_REQUIRES_SUBSCRIPTION` | `AskTeacherGate.EnsureCanAskAsync` | `ForbiddenCoreException` | 403 |
| `AskTeacherMonthlyLimitReached` | `ASK_TEACHER_MONTHLY_LIMIT_REACHED` | `AskTeacherGate.EnsureCanAskAsync` (context `limit`) | `ForbiddenCoreException` | 403 |
| `TeacherThreadContextInvalid` | `TEACHER_THREAD_CONTEXT_INVALID` | Create and GetContext validators | validation | 422 |
| `TeacherThreadTextRequired` | `TEACHER_THREAD_TEXT_REQUIRED` | Create validator | validation | 422 |
| `TeacherThreadTextTooLong` | `TEACHER_THREAD_TEXT_TOO_LONG` | Create validator | validation | 422 |
| `TeacherThreadImageTypeInvalid` | `TEACHER_THREAD_IMAGE_TYPE_INVALID` | Create validator | validation | 422 |
| `TeacherThreadImageTooLarge` | `TEACHER_THREAD_IMAGE_TOO_LARGE` | Create validator | validation | 422 |
| `TeacherThreadPageNumberInvalid` | `TEACHER_THREAD_PAGE_NUMBER_INVALID` | GetMyTeacherThreads validator | validation | 422 |
| `TeacherThreadPageSizeInvalid` | `TEACHER_THREAD_PAGE_SIZE_INVALID` | GetMyTeacherThreads validator | validation | 422 |
| existing `LessonNotFound`, `QuestionNotFound`, `UserNotAuthenticated` | — | resolver, handlers | NotFound / Unauthorized | 404 / 401 |

Resource strings (resx, Arabic without tashkeel; the web `errors` may use «معلّم»):
| Key | ar | en |
|---|---|---|
| TEACHER_MESSAGE_TEXT_REQUIRED | نص الرسالة مطلوب. | The message text is required. |
| TEACHER_THREAD_NOT_FOUND | السؤال غير موجود. | The question was not found. |
| ATTEMPT_NOT_FOUND | المحاولة غير موجودة. | The attempt was not found. |
| ASK_TEACHER_REQUIRES_SUBSCRIPTION | خدمة اسأل معلم تتطلب الإضافة مع الباقة الأساسية. | Ask a Teacher needs the add-on with an active Base plan. |
| ASK_TEACHER_MONTHLY_LIMIT_REACHED | استخدمت كل أسئلتك لهذا الشهر. | You have used all your questions for this month. |
| TEACHER_THREAD_CONTEXT_INVALID | أرفق درسا أو سؤالا أو محاولة واحدة فقط. | Attach exactly one lesson, question or attempt. |
| TEACHER_THREAD_TEXT_REQUIRED | اكتب سؤالك. | Write your question. |
| TEACHER_THREAD_TEXT_TOO_LONG | السؤال طويل جدا. | The question is too long. |
| TEACHER_THREAD_IMAGE_TYPE_INVALID | الصورة يجب أن تكون PNG أو JPG أو WEBP. | The image must be PNG, JPG or WEBP. |
| TEACHER_THREAD_IMAGE_TOO_LARGE | حجم الصورة أكبر من المسموح. | The image is too large. |
| TEACHER_THREAD_PAGE_NUMBER_INVALID | رقم الصفحة غير صالح. | The page number is not valid. |
| TEACHER_THREAD_PAGE_SIZE_INVALID | حجم الصفحة غير صالح. | The page size is not valid. |

## Domain behaviour
```csharp
// TeacherThread.cs
using Core.DDD.Entities;
namespace Elmanhg.Domain.TeacherThreads;

public class TeacherThread : AuditEntity
{
    public Guid StudentId { get; private set; }
    public Guid SubjectId { get; private set; }
    public string Context { get; private set; } = "{}";
    public TeacherThreadStatus Status { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset SlaDueAt { get; private set; }
    public uint Version { get; private set; }
    public List<TeacherMessage> Messages { get; private set; } = [];

    private TeacherThread(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static TeacherThread Submit(Guid studentId, TeacherThreadContext context, string text, string? imageUrl, DateTimeOffset submittedAt, TimeSpan replySla)
    {
        var at = ToMicroseconds(submittedAt);
        var thread = new TeacherThread(Guid.NewGuid(), studentId)
        {
            StudentId = studentId,
            SubjectId = context.SubjectId,
            Context = context.ToJson(),
            Status = TeacherThreadStatus.Open,
            SubmittedAt = at,
            SlaDueAt = at + replySla,
        };
        thread.Messages.Add(TeacherMessage.CreateText(thread.Id, studentId, text, imageUrl, at));
        return thread;
    }

    public TeacherThreadContext ReadContext() => TeacherThreadContext.FromJson(Context);

    public bool IsOverdueAt(DateTimeOffset now) => Status == TeacherThreadStatus.Open && now >= SlaDueAt;

    // PostgreSQL timestamptz keeps microseconds; truncating keeps the returned result equal to what is stored.
    private static DateTimeOffset ToMicroseconds(DateTimeOffset value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));
}

// TeacherMessage.cs
using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
namespace Elmanhg.Domain.TeacherThreads;

public class TeacherMessage : Entity
{
    public Guid ThreadId { get; private set; }
    public Guid SenderId { get; private set; }
    public TeacherMessageKind Kind { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private TeacherMessage(Guid id) : base(id) { }

    internal static TeacherMessage CreateText(Guid threadId, Guid senderId, string text, string? imageUrl, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.TeacherMessageTextRequired);
        }

        return new TeacherMessage(Guid.NewGuid())
        {
            ThreadId = threadId,
            SenderId = senderId,
            Kind = TeacherMessageKind.Text,
            Text = text.Trim(),
            ImageUrl = imageUrl,
            CreatedAt = createdAt,
        };
    }
}
```
- No mutating methods in #94, so no `UpdationDate` stamping. #95 adds `Claim`, `Reply`, `FollowUp`, `Rate` and `Close`, each setting `UpdationDate`.
- Invariants: a thread always has ≥1 message (the first is the student's); `SubjectId == ReadContext().SubjectId`; `SlaDueAt = SubmittedAt + replySla`.

**Mapping** (`ConfigureTeacherThreads`):
```csharp
modelBuilder.Entity<TeacherThread>(builder =>
{
    builder.Property(x => x.Context).IsRequired().HasColumnType("jsonb");
    builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
    builder.Property(x => x.Version).IsRowVersion();
    builder.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
    builder.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => new { x.StudentId, x.SubmittedAt });
    builder.HasIndex(x => new { x.SubjectId, x.Status, x.SubmittedAt });
});
modelBuilder.Entity<TeacherMessage>(builder =>
{
    builder.Property(x => x.Id).ValueGeneratedNever();
    builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
    builder.Property(x => x.Text).IsRequired();
    builder.HasOne<User>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => new { x.ThreadId, x.CreatedAt });
});
```

## API surface
| Method | Route | Policy | Input | Response | Errors |
|---|---|---|---|---|---|
| GET | `/api/teacher-threads/context` (Name `GetTeacherThreadContext`) | `DefaultCodes.AskTeacherSubmit` | `[FromQuery] Guid? lessonId, Guid? questionId, Guid? attemptId` → `GetTeacherThreadContextQuery` | `TeacherThreadContextResult` | 422 `TEACHER_THREAD_CONTEXT_INVALID`; 404 `LESSON_NOT_FOUND` / `QUESTION_NOT_FOUND` / `ATTEMPT_NOT_FOUND`; 401; 403 |
| GET | `/api/teacher-threads` (Name `GetMyTeacherThreads`) | `AskTeacherSubmit` | `[FromQuery] int pageNumber = 1, int pageSize = 20` | `PageData<TeacherThreadSummaryResult>` | 422 `TEACHER_THREAD_PAGE_*` |
| GET | `/api/teacher-threads/{threadId:guid}` (Name `GetMyTeacherThread`) | `AskTeacherSubmit` | route `threadId` | `TeacherThreadResult` | 404 `TEACHER_THREAD_NOT_FOUND` |
| POST | `/api/teacher-threads` (Name `CreateTeacherThread`), `[Consumes("multipart/form-data")]` | `AskTeacherSubmit` | `[FromForm] string? text, [FromForm] Guid? lessonId, [FromForm] Guid? questionId, [FromForm] Guid? attemptId, IFormFile? image` → `CreateTeacherThreadCommand` | `TeacherThreadResult` | 422 (text/context/image); 403 `ASK_TEACHER_REQUIRES_SUBSCRIPTION` / `ASK_TEACHER_MONTHLY_LIMIT_REACHED`; 404 context codes |
Every action has `[ProducesResponseType<T>(StatusCodes.Status200OK)]`, passes `cancellationToken` to `Send` and returns `Ok(result)`. `GetMyUsage` is unchanged in route and shape apart from the 3 new fields.

**Postman** folder `AskTeacher` (in this order; the student token from the Auth folder):
1. "Get thread context": GET `{{baseUrl}}/api/teacher-threads/context?lessonId={{lessonId}}`. Tests: status 200 and body has `subjectName`.
2. "Create thread": POST `{{baseUrl}}/api/teacher-threads`, `formdata` `text` = "Why is F = ma?", `lessonId` = `{{lessonId}}`. Tests: status is one of `[200, 403]` (403 = no add-on in a fresh DB); on 200, `pm.collectionVariables.set("teacherThreadId", pm.response.json().id)`.
3. "List my threads": GET `{{baseUrl}}/api/teacher-threads?pageNumber=1&pageSize=20`. Tests: 200 and `items` is an array.
4. "Get my thread": GET `{{baseUrl}}/api/teacher-threads/{{teacherThreadId}}`. Tests: status one of `[200, 404]`.

## Test plan

### API — unit (`api/Elmanhg.Tests/`)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `Domain/TeacherThreads/TeacherThreadTests` | `Submit_ValidInput_OpensThreadWithSlaAndFirstStudentMessage` | Status Open; StudentId; SubjectId = context.SubjectId; CreatedBy = student; SubmittedAt; SlaDueAt = +24h; one message (ThreadId = thread.Id, SenderId = student, Kind Text, trimmed text, ImageUrl, CreatedAt = SubmittedAt) |
| 2 | 〃 | `Submit_BlankText_ThrowsTeacherMessageTextRequired` | `BusinessRuleViolationCoreException` with code `TEACHER_MESSAGE_TEXT_REQUIRED` |
| 3 | 〃 | `Submit_SubMicrosecondTime_TruncatesToMicroseconds` | SubmittedAt ticks % 10 == 0; SlaDueAt computed from the truncated value |
| 4 | 〃 | `ReadContext_AfterSubmit_ReturnsTheSubmittedContext` | ReadContext() equals the input record (a question/attempt context with all fields set) |
| 5 | 〃 | `IsOverdueAt_BeforeSlaDue_ReturnsFalse` | false one second before SlaDueAt |
| 6 | 〃 | `IsOverdueAt_AtOrAfterSlaDue_ReturnsTrue` (Theory: 0 s, +1 h) | true |
| 7 | `Domain/TeacherThreads/TeacherThreadContextTests` | `FromJson_NullDocument_ThrowsInvalidOperation` | `InvalidOperationException` for `"null"` |
| 8 | `Application/Features/TeacherThreads/Shared/AskTeacherGateTests` | `CurrentQuotaMonth_JustAfterCairoMidnightOnTheFirst_ReturnsTheNewLocalMonthInUtc` | now 2026-01-31T22:30Z → (2026-01-31T22:00Z, 2026-02-28T22:00Z), both with offset zero |
| 9 | 〃 | `CurrentQuotaMonth_December_EndsOnTheFirstOfJanuary` | now 2026-12-15T10:00Z → (2026-11-30T22:00Z, 2026-12-31T22:00Z) |
| 10 | 〃 | `EnsureCanAskAsync_WithoutAskTeacher_ThrowsAskTeacherRequiresSubscription` | Forbidden + code |
| 11 | 〃 | `EnsureCanAskAsync_UsedBelowLimit_Completes` | no exception at 19/20 |
| 12 | 〃 | `EnsureCanAskAsync_UsedAtLimit_ThrowsMonthlyLimitReachedWithLimit` | Forbidden + code + `Context["limit"] == 20` |
| 13 | `…/Shared/TeacherThreadContextResolverTests` | `ResolveAsync_PublishedLesson_ReturnsSubjectUnitLessonWithoutQuestion` | names/ids; question fields null |
| 14 | 〃 | `ResolveAsync_UnknownLesson_ThrowsLessonNotFound` | NotFound + code |
| 15 | 〃 | `ResolveAsync_DraftLesson_ThrowsLessonNotFound` | NotFound + code |
| 16 | 〃 | `ResolveAsync_ServableQuestion_ReturnsCurrentStemAndVersion` | QuestionId, Version, Stem; AttemptId null |
| 17 | 〃 | `ResolveAsync_UnknownQuestion_ThrowsQuestionNotFound` | NotFound + code |
| 18 | 〃 | `ResolveAsync_PendingQuestion_ThrowsQuestionNotFound` | NotFound + code |
| 19 | 〃 | `ResolveAsync_OwnAttempt_ReturnsServedRevisionStemAndAttemptId` | revisions stubbed with v1 "Old stem" / the question at v2; attempt at v1 → stem "Old stem", version 1, attempt id (the attempt is built through `SessionBuilder` + the session's answer method, as in `SessionTests`) |
| 20 | 〃 | `ResolveAsync_AttemptNotOwned_ThrowsAttemptNotFound` | `GetStudentAttemptAsync` returns null → NotFound + code |
| 21 | 〃 | `ResolveAsync_AttemptRevisionMissing_ThrowsQuestionNotFound` | NotFound + code |
| 22 | 〃 | `ResolveAsync_AttemptOnUnpublishedLesson_ThrowsLessonNotFound` | NotFound + `LESSON_NOT_FOUND` |
| 23 | `…/CreateTeacherThread/CreateTeacherThreadHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | Unauthorized + code; SaveChanges DidNotReceive |
| 24 | 〃 | `Handle_WithoutAskTeacher_ThrowsAskTeacherRequiresSubscription` | Base only → Forbidden + code; SaveChanges and file storage DidNotReceive |
| 25 | 〃 | `Handle_MonthlyLimitReached_ThrowsAskTeacherMonthlyLimitReached` | `CountAsync` → 20 → Forbidden + code; SaveChanges DidNotReceive |
| 26 | 〃 | `Handle_LessonContext_AddsOpenThreadAndReturnsResult` | AddAsync received a thread (StudentId, SubjectId, SlaDueAt = now + 24h); result Open, IsOverdue false, 1 message IsFromStudent; SaveChanges Received(1); storage DidNotReceive |
| 27 | 〃 | `Handle_WithImage_StoresUnderTeacherThreadsKeyAndAttachesUrl` | SaveAsync key starts with `teacher-threads/` and ends with `.png` (from `Photo.PNG`); message ImageUrl = returned URL |
| 28 | 〃 | `Handle_UnknownLesson_ThrowsLessonNotFoundAndStoresNothing` | NotFound + code; storage and SaveChanges DidNotReceive |
| 29 | `…/CreateTeacherThread/CreateTeacherThreadValidatorTests` | `Validate_TextWithOneContext_Passes` | valid |
| 30 | 〃 | `Validate_BlankText_FailsTextRequired` | code |
| 31 | 〃 | `Validate_TextOverMax_FailsTextTooLong` | code (2001 chars) |
| 32 | 〃 | `Validate_NoContext_FailsContextInvalid` | code |
| 33 | 〃 | `Validate_TwoContexts_FailsContextInvalid` | code |
| 34 | 〃 | `Validate_EmptyGuidContext_FailsContextInvalid` | code |
| 35 | 〃 | `Validate_SvgExtension_FailsImageTypeInvalid` | code |
| 36 | 〃 | `Validate_PngNameWithTextContentType_FailsImageTypeInvalid` | code |
| 37 | 〃 | `Validate_ImageOverMaxSize_FailsImageTooLarge` | code (length 5 MB + 1) |
| 38 | `…/GetTeacherThreadContext/GetTeacherThreadContextValidatorTests` | `Validate_OneContext_Passes` / `Validate_NoContext_FailsContextInvalid` | valid / code |
| 39 | `…/GetTeacherThreadContext/GetTeacherThreadContextHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | Unauthorized + code |
| 40 | 〃 | `Handle_PublishedLesson_ReturnsContextResult` | result fields equal the lesson/unit/subject |
| 41 | `…/GetMyTeacherThreads/GetMyTeacherThreadsValidatorTests` | `Validate_DefaultPage_Passes` / `Validate_PageNumberZero_FailsPageNumberInvalid` / `Validate_PageSizeOverMax_FailsPageSizeInvalid` | valid / code / code |
| 42 | `…/GetMyTeacherThreads/GetMyTeacherThreadsHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | Unauthorized + code |
| 43 | 〃 | `Handle_Threads_ReturnsSummariesWithQuestionTextAndOverdueFlag` | `FindPaginatedAsync` stubbed with one thread submitted 25 h before now → SubjectName, LessonName, QuestionText, Status Open, IsOverdue true, paging fields copied |
| 44 | `…/GetMyTeacherThread/GetMyTeacherThreadHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | Unauthorized + code |
| 45 | 〃 | `Handle_OwnThread_ReturnsContextAndMessages` | context and message mapping |
| 46 | 〃 | `Handle_ThreadNotFound_ThrowsTeacherThreadNotFound` | NotFound + code |
| 47 | `…/Subscriptions/GetMyUsage/GetMyUsageHandlerTests` (modify) | `Handle_AskTeacherStudent_ReturnsMonthlyLimitUsedAndRemaining` | Base + AskTeacher, `CountAsync` 3 → (20, 3, 17) |
| 48 | 〃 (modify) | `Handle_AskTeacherStudentOverLimit_ReturnsZeroRemaining` | count 25 → remaining 0 |

### API — integration (`api/Elmanhg.Tests/Integration/`)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 49 | `TeacherThreads/CreateTeacherThreadEndpointTests` | `Post_LessonContextWithImage_PersistsThreadAndServesImage` | 200; body status `Open`, `isOverdue` false, context names; message `imageUrl` starts with `/api/media/teacher-threads/` and ends with `.png`; GET of the URL returns 200 with the bytes and `nosniff`; DB: 1 thread (StudentId, SubjectId, SlaDueAt − SubmittedAt = 24h) with 1 message |
| 50 | 〃 | `Post_AttemptContext_SnapshotsQuestionStemAndAttemptId` | start a quiz, answer, take `items[0].attempt.id`; 200; `context.attemptId`, `questionId`, `questionStem` non-empty |
| 51 | 〃 | `Post_BaseWithoutAddOn_Returns403AskTeacherRequiresSubscription` | 403 + code; no thread in DB |
| 52 | 〃 | `Post_MonthlyQuotaUsed_Returns403AskTeacherMonthlyLimitReached` | 20 threads seeded at UtcNow → 403 + code; still 20 threads |
| 53 | 〃 | `Post_OtherStudentsAttempt_Returns404AttemptNotFound` | 404 + code |
| 54 | 〃 | `Post_TwoContexts_Returns422TeacherThreadContextInvalid` | 422 + code |
| 55 | 〃 | `Post_SvgImage_Returns422TeacherThreadImageTypeInvalid` | 422 + code; no thread |
| 56 | 〃 | `Post_Teacher_Returns403` / `Post_Anonymous_Returns401` | status |
| 57 | `TeacherThreads/TeacherThreadReadEndpointTests` | `GetList_ReturnsOwnThreadsNewestFirst` | 2 own + 1 foreign seeded → 2 items newest first, `questionText`, `totalItems` 2 |
| 58 | 〃 | `GetList_ThreadPastSla_ReportsOverdue` | a thread submitted 25 h ago → `isOverdue` true |
| 59 | 〃 | `GetList_PageSizeOverMax_Returns422` | 422 + `TEACHER_THREAD_PAGE_SIZE_INVALID` |
| 60 | 〃 | `GetById_OwnThread_ReturnsContextAndMessages` | 200; context + message |
| 61 | 〃 | `GetById_OtherStudentsThread_Returns404` | 404 + `TEACHER_THREAD_NOT_FOUND` |
| 62 | 〃 | `GetList_Anonymous_Returns401` / `GetList_Teacher_Returns403` | status |
| 63 | `TeacherThreads/TeacherThreadContextEndpointTests` | `Get_PublishedLesson_ReturnsNames` | 200; subject/unit/lesson names; `questionId` null |
| 64 | 〃 | `Get_DraftLesson_Returns404LessonNotFound` | 404 + code |
| 65 | 〃 | `Get_NoContext_Returns422` | 422 + `TEACHER_THREAD_CONTEXT_INVALID` |
| 66 | 〃 | `Get_Admin_Returns403` | 403 |
| 67 | `Subscriptions/UsageEndpointTests` (modify) | `Get_AskTeacherStudentWithThreadsThisMonth_ReturnsMonthlyQuota` | 2 threads seeded now → `monthlyAskTeacherQuestionLimit` 20, used 2, remaining 18 |
| 68 | `Persistence/AppDbContextTests` (modify) | `Migrate_FreshDatabase_LeavesNoPendingMigrations` | list includes `_AddTeacherThreads` |

### Web (Vitest + Testing Library + MSW; `renderApp` with `testSessions.student`; time frozen with `vi.useFakeTimers({ shouldAdvanceTime: true })` + `vi.setSystemTime` where badges show hours)
| # | File | `it(...)` | Asserts |
|---|------|-----------|---------|
| 69 | `features/askTeacher/api/threadBadge.test.ts` | returns closed for a closed thread; returns overdue for an open thread past its SLA; returns awaiting for an open thread within its SLA; returns answered for an answered thread | return values |
| 70 | `features/askTeacher/api/remainingHours.test.ts` | rounds a partial hour up; returns zero once the due time has passed | values |
| 71 | `features/askTeacher/schemas/askTeacherFormSchema.test.ts` | accepts text when a context is attached; rejects blank text with the textRequired key; requires a lesson in picker mode; accepts a picked lesson in picker mode | success / issue messages |
| 72 | `features/askTeacher/schemas/askTeacherListSearchSchema.test.ts` | parses a page number; drops an invalid page | values |
| 73 | `features/askTeacher/schemas/askTeacherNewSearchSchema.test.ts` | keeps a valid attempt id; drops a malformed lesson id | values |
| 74 | `features/askTeacher/pages/AskTeacherListPage.test.tsx` | shows loading then the monthly allowance and threads newest first | skeleton status, then «Monthly allowance: 3 / 20», items in order, meta text, «Awaiting reply · 5 hours left» |
| 75 | 〃 | shows the overdue, answered and closed badges | three badge texts |
| 76 | 〃 | shows the empty state when there are no threads | «No questions yet.» |
| 77 | 〃 | shows the error state and retries | error title → Retry → items |
| 78 | 〃 | shows the upsell with a subscription link without the add-on | body text; link href `/student/subscription`; no «New question» link |
| 79 | 〃 | moves to the next page | click Next → request with `pageNumber=2`; URL search `page=2` |
| 80 | 〃 | renders right to left in Arabic | `dir="rtl"`; «الرصيد الشهري: ٣ / ٢٠» |
| 81 | 〃 | has no axe violations | axe |
| 82 | `features/askTeacher/pages/AskTeacherNewPage.test.tsx` | shows the attached lesson context from a lesson link | «Attached automatically», «Physics / Mechanics / Newton's laws» |
| 83 | 〃 | shows the question stem for an attempt context | «Question:» + stem |
| 84 | 〃 | sends the question with the attempt and photo, then opens the thread | the MSW handler captures FormData `text`, `attemptId`, `image` (file name), no `lessonId`; toast «Your question was sent.»; URL `/student/thread/<id>`; thread page shows the text from the seeded cache |
| 85 | 〃 | shows the required error when the question is blank | inline «Write your question»; no request sent |
| 86 | 〃 | shows the server image error under the photo field | 422 `TEACHER_THREAD_IMAGE_TYPE_INVALID` → the error text next to the file input (`aria-invalid`) |
| 87 | 〃 | shows the monthly limit error as a form alert | 403 `ASK_TEACHER_MONTHLY_LIMIT_REACHED` → `role=alert` text |
| 88 | 〃 | shows the used-quota notice instead of the form | remaining 0 → «You have used all 20 questions for this month.»; no Send button |
| 89 | 〃 | shows the upsell without the add-on | upsell text |
| 90 | 〃 | shows the context error state and retries | error title → Retry → context |
| 91 | 〃 | shows the reply-time note from the plan catalogue | «…reply within 24 hours.» |
| 92 | 〃 | disables Send while submitting | delayed handler → button disabled + `aria-busy` |
| 93 | 〃 | renders right to left in Arabic | `dir="rtl"`; «سؤال جديد للمعلّم» |
| 94 | 〃 | has no axe violations | axe |
| 95 | `features/askTeacher/pages/AskTeacherNewPage.picker.test.tsx` | lists the subjects and then the chosen subject's lessons grouped by unit | selects; optgroups labelled by unit |
| 96 | 〃 | requires a lesson before sending | «Choose a lesson» inline; no request |
| 97 | 〃 | sends the picked lesson id | FormData `lessonId` equals the picked id |
| 98 | `features/askTeacher/pages/TeacherThreadPage.test.tsx` | shows loading then the context, due time, badge and the student's message with its photo | texts; `img` with alt «Photo attached to the question»; breadcrumb link to `/student/ask` |
| 99 | 〃 | shows the error state and retries | 404 once → Retry → content |
| 100 | 〃 | renders right to left in Arabic | `dir="rtl"`; «المحادثة» |
| 101 | `features/browse/pages/LessonPage.askTeacher.test.tsx` | links Ask a teacher to the new-question page with the lesson id | link href contains `/student/ask-new?lessonId=<id>` |
| 102 | 〃 | hides Ask a teacher on a locked lesson | no link (locked fixture as in `LessonPage.freeTier.test.tsx`) |
| 103 | `features/quiz/pages/QuizPage.askTeacher.test.tsx` | links Ask a teacher with the answered attempt id | reuses the "every question is answered" setup from `QuizPage.test.tsx`; link href contains `attemptId=<attempt id>` |

Existing web tests must stay green with no edits beyond `subscriptionFixtures.ts` (fixture data, not a test).

## Definition of done
- [ ] Every sub-task is covered: entities with a context JSON, status and SLA due time; the create command with the add-on + monthly quota checks; the image attachment upload; the student UI (compose with auto-attached context, my threads list, thread view).
- [ ] Every file in "Files to create" exists; no others were added (the migration pair counts as one row).
- [ ] `TeacherThread`/`TeacherMessage` match "Domain behaviour"; the entity has no public setters; `SlaDueAt = SubmittedAt + AskTeacherReplySlaHours`.
- [ ] Migration `AddTeacherThreads` has only CreateTable/FK/Index operations; both entities are in the global soft-delete filter.
- [ ] Gate order in `CreateTeacherThreadHandler`: 401 → 403 add-on → 403 quota → 404 context → image → one `SaveChangesAsync`. Nothing is stored on any failure.
- [ ] The quota counts the Cairo calendar month using UTC bounds; `UsageResult` exposes limit/used/remaining (never negative).
- [ ] An attempt context uses the served-revision stem; another student's attempt returns 404 `ATTEMPT_NOT_FOUND`; another student's thread returns 404 `TEACHER_THREAD_NOT_FOUND`.
- [ ] All 4 endpoints carry `DefaultCodes.AskTeacherSubmit`; `EndpointAuthorizationTests` is green.
- [ ] 12 new error codes are in `ErrorCodes` (11 Application + 1 Domain) and in both resx files and both web `errors` maps.
- [ ] `AskTeacherOptions` is registered with `ValidateOnStart` and code defaults, and is in `appsettings.example.json`.
- [ ] `dotnet build` shows 0 new warnings; `dotnet test api/ -c Release` is green with `appsettings.json` moved aside (CI parity); `dotnet format --verify-no-changes` is clean outside `core-libraries`.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` are regenerated (no drift); `routeTree.gen.ts` is regenerated.
- [ ] Web: `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` and `npx vitest run --coverage` all pass; the §14/§16 greps are clean in `src/features/askTeacher`.
- [ ] Every data view (list, compose context, picker, thread) has loading, empty (list) and error-with-retry states plus an `ar` RTL render.
- [ ] All strings are in both `askTeacher` locale files; no literal colours or sizes; logical properties only; chevrons use `rtl:rotate-180`.
- [ ] Every test in the Test plan exists by name; each new test was mutation-checked (break the line, the test fails, restore it).
- [ ] Postman `AskTeacher` folder has the 4 requests in order and the `teacherThreadId` variable.
- [ ] Docs are updated in the same change: `docs/ask-teacher.md` (new), PRD §12.1/§15/§20, `docs/subscriptions.md`, `docs/claude-design-prompt.md` §4, `docs/prototype.md`.
- [ ] Deferred D1 (S3 adapter) is listed for the orchestrator to open as a `deferred` issue.
