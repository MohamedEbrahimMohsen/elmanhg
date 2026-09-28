# Plan — Teacher validation queue (#68, E3.S5)

## Goal
A Teacher can open `#/teacher` and see the Pending questions of their assigned subjects only, oldest first. The queue filters by unit, lesson, type, difficulty and age. The Teacher opens a question to read its full stem, body, answer key, grading spec, explanation, the admin's difficulty, the version, and the revision and decision (rejection) history. From there they approve it (optionally changing the difficulty first) or reject it with a required reason. From the queue they can bulk-approve only the questions they opened in the current review session, and the server enforces that rule. Teachers still cannot edit question content, and Admins still cannot approve. Every approve, reject and bulk approve is audited, and the servable count moves after an approval.

## Scope
**In:**
- Domain: `Question.Approve(TeacherSubject, int reviewedVersion, QuestionDifficulty? difficulty)` and `Question.Reject(TeacherSubject, int reviewedVersion, string reason)`. Both check the stale version, append to an append-only `QuestionDecision` history, and stamp `Question.SubmittedAt` (when the current version entered the queue).
- Domain: new `ReviewSession` aggregate with `ReviewSessionOpening` children. It is the server-side record of "opened in the current session".
- Application (area `QuestionValidation`):
  - Queries: `GetValidationQueue`, `GetValidationQueueFilters`, `GetValidationQuestion`.
  - Commands: `ApproveQuestion`, `RejectQuestion`, `StartReviewSession`, `RecordQuestionOpening`, `BulkApproveQuestions`.
  - `QuestionValidationOptions`.
- Infrastructure: `ReviewSessionRepository`, EF mappings, and migration `AddQuestionValidationQueue`, which backfills `SubmittedAt` and the decision history.
- API: `ValidationQueueController` (`api/validation-queue`, policy `QuestionsValidate`), resx strings, OpenAPI plus Orval regeneration, and Postman.
- Web, in `features/questions`:
  - `/teacher` queue page with filters, a list, pagination, loading, empty, no-results and error states, and a bulk-approve bar with a confirm dialog.
  - `/teacher/q/$questionId` detail page with a preview showing the answer key, the grading spec, the history, and approve and reject forms.
- Tests (api unit, integration, web) and docs (PRD §8.1, `question-schemas.md`, `audit-log.md`, `claude-design-prompt.md` §4, `prototype.md` item 9).
- #67 carry-over: servable-count before/after delta tests for teacher approve and reject, in `ServableCountCollection`.

**Out:**
- Teacher personal stats card (`#/teacher/stats`, story #107). The prototype's queue stats strip is not rendered.
- Admin view of decision history. The admin `GET /api/questions/{id}` is unchanged.
- A cleanup job for expired `ReviewSession` rows (small, append-only rows).
- A concurrency token on `Question`. The reviewed-version check covers the stale-content race. Two teachers deciding the same question at the same instant is last-write-wins, with two decision rows.
- The prototype (`prototype/app.js`) is not changed.

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | What does "opened in the current session" mean on the server? | A **review session** is a server row `ReviewSession(TeacherId, CreationDate, ExpiresAt)`, created by `POST /api/validation-queue/review-sessions` and alive for `QuestionValidation:ReviewSessionLifetimeMinutes` (default 480). The web app starts one per app load and holds its id in TanStack Query memory only (not storage). Reload, sign-out or expiry starts a new session. The detail page records an opening, `ReviewSessionOpening(QuestionId, QuestionVersion, OpenedAt)`, through `POST …/review-sessions/{id}/openings/{questionId}`. The server accepts only sessions owned by the caller. Bulk approve re-checks, per question: session owned (else 404), session not expired (`REVIEW_SESSION_EXPIRED`), and an opening exists **for the question's current version** (`QUESTION_NOT_OPENED_IN_SESSION`). It is all-or-nothing, in one `SaveChangesAsync`. | A client-only check is not enforcement. Tying the opening to the version means an admin content edit after opening voids it, which prevents blind approval of unseen content. There is no auth-session id claim in the JWT (`UserClaimsExtensions`), so the session is explicit. |
| 2 | Is the opening recorded by the detail GET? | No. `GET …/questions/{id}` is pure. The page fires the opening POST after the detail loads, from a `useEffect` keyed on `(reviewSessionId, questionId, version)`. That is a side effect of viewing, not data fetching (react skill §3 forbids effects for fetching only). | GET must be safe: router `defaultPreload: 'intent'` would otherwise record openings on hover. |
| 3 | Duplicate openings (StrictMode double effect, concurrent calls) | The domain skips an existing (question, version) pair. The index on `(ReviewSessionId, QuestionId)` is **not unique**, so a rare concurrent duplicate row is harmless. | This avoids turning a unique violation into a 500. `HasOpened` uses `Any`. |
| 4 | Stale content on single approve or reject | The request carries `version` (the version the teacher viewed). The domain throws `ConflictCoreException(QUESTION_VERSION_CHANGED)` (409) when it differs from `Question.Version`. The order of checks is retired, then pending, then assignment, then version. | `docs/question-schemas.md` §Versioning already states "An approval must name the version the teacher reviewed". |
| 5 | Signature change of `Approve` / `Reject` | `Approve(TeacherSubject assignment, int reviewedVersion, QuestionDifficulty? difficulty = null)` and `Reject(TeacherSubject assignment, int reviewedVersion, string reason)`. Every existing test call site passes `question.Version` (mechanical edit, listed in Existing code touched). | This is the only path to Approved (PROGRESS: admins can never approve; approval only through `Question.Approve(TeacherSubject)`). |
| 6 | Change difficulty then approve | `Approve(..., difficulty)` sets `Difficulty` only when it differs. The version and revisions are unchanged (metadata edit, PRD §5.3). The decision row stores `Difficulty` (after) and `DifficultyChangedFrom` (before; null when unchanged). The audit diff shows `difficulty`. | PRD §5.4 "may be changed by Teacher at validation"; PRD §16 `Questions.ChangeDifficulty`. |
| 7 | Prior rejection history (resubmit clears `RejectionReason`) | New child entity `QuestionDecision` (append-only, like `QuestionRevision`), appended by `Approve`/`Reject`. The migration backfills one row per currently Approved/Rejected question from `ValidatedBy`/`ValidatedAt`/`RejectionReason`. | Modelled on Morabh `Morabh.Domain/Orders/OrderStatusHistory.cs`. The audit log is admin-only (PRD §16), so teachers cannot read history from it. |
| 8 | Queue "age" | New column `Question.SubmittedAt`: the time the current version entered review. It is set at `Create` (and import), on every content edit (version bump, matching the prototype's `createdAt = now` on content change), and on `Resubmit`. Metadata-only edits keep it. The filter is `minAgeDays ∈ [1, QueueMaxAgeDays]`, meaning "submitted at least N days ago". The web offers 1 / 3 / 7. The queue is ordered by `SubmittedAt` ascending, then `Id` (oldest first, as in the prototype). | `UpdationDate` also moves on metadata edits and decisions, so it is not a queue age. The backfill uses the latest revision's `EditedAt`, else `CreationDate`. |
| 9 | Scope check for by-id endpoints | The handler loads the question, then requires a live `TeacherSubject` for `(currentUser, question.SubjectId)`. If there is none, it throws `ForbiddenCoreException(SUBJECT_OUT_OF_SCOPE)` (403). `ISubjectScopedRequest` is not used: the subject is not in the request. | This matches the existing fail-closed 403 of `SubjectScopeBehaviour` and the prototype's "غير مسموح". |
| 10 | Queue subject scope | `subjectIds` = the caller's live `TeacherSubject` rows. The filter always intersects with them, so a `unitId`/`lessonId` from another subject yields an empty page, not an error. There is no subject filter (PRD §8.1 lists unit, lesson, type, difficulty, age). | It keeps the query fail-closed with no extra branch. |
| 11 | Which statuses and lessons appear | Pending AND `RetiredAt == null`, in any lesson state (Draft lessons included: approved questions wait silently, PRD §5.3). The filter tree (`/filters`) returns every unit and lesson of the assigned subjects regardless of lesson state. | Validation precedes publishing. The "Published only" rule is for lesson *content* reads. Teachers see only names here. |
| 12 | Who may call | Every `api/validation-queue` action uses `[Authorize(Policy = DefaultCodes.QuestionsValidate)]` (Teacher only). An Admin gets 403 from the policy, and the domain still requires a `TeacherSubject`. | PRD §16. `QuestionsChangeDifficulty` also allows Teacher, so the one policy suffices. |
| 13 | Teachers cannot edit content | No new endpoint accepts content. The approve body is `{version, difficulty}` only. The existing admin `PUT /api/questions/{id}` and `/resubmit` stay `ContentManage` (Admin). Integration tests prove the teacher gets 403 and the content is unchanged. | PRD §8.1. |
| 14 | Bulk approve and difficulty | Bulk approve keeps each question's current difficulty and uses `question.Version` as the reviewed version (the session proved that version was opened). The max count is `QuestionValidation:BulkApproveMaxCount` (default 50). The web selection is per page (page size 20) and clears on filter or page change. | A single cap enforced server-side. |
| 15 | Audit | `ApproveQuestion` → `Question.Approve` / Question / QuestionId. `RejectQuestion` → `Question.Reject`. `BulkApproveQuestions` → `Question.BulkApprove` / `ReviewSession` / ReviewSessionId (the diff lists every approved Question, mirroring Morabh `ActivateBranchesCommand` parent-id bulk audit). `StartReviewSession` and `RecordQuestionOpening` are not audited (a reading aid, not a content or validation change). `ReviewSession`, `ReviewSessionOpening` and `QuestionDecision` are not `IAuditedEntity`. | `docs/audit-log.md` rules. PRD §17 rule 13 covers validation decisions. |
| 16 | Options | New `QuestionValidationOptions` (section `QuestionValidation`) with code defaults: `QueueMaxPageSize=100`, `QueueMaxAgeDays=365`, `RejectionReasonMaxLength=1000`, `BulkApproveMaxCount=50`, `ReviewSessionLifetimeMinutes=480`. It is added to `appsettings.example.json` and `ApiFactory`. | PROGRESS "CI parity": safe code defaults. |
| 17 | Where the teacher UI lives | Inside `web/src/features/questions/`: prefix `Validation*`, i18n keys `validation.*` in the `questions` namespace. | It reuses `QuestionView`, `SelectField`, `TextAreaField`, `QuestionStatusBadge` and `toQuestionValues` without widening a barrel (react skill §1, 6.7). |
| 18 | Queue layout | A list of white items (design-prompt §6 "List screens … queue"): a checkbox at the start, a stem excerpt as the title, a meta caption (type · unit › lesson · difficulty · vN · age), and a trailing "opened" badge. It is not a table. The h1 is exactly "Review queue" / "قائمة المراجعة". Subject names go in the caption line. | Existing `AppShell.test.tsx` asserts the heading name "Review queue". |
| 19 | Answer key display | The preview renders `QuestionView` **disabled**, with `answer = toAnswerKey(values)` (correct options ticked, the true/false value, the first accepted answer per blank, the numeric value or first accepted text). The raw `{body, gradingSpec}` JSON is in a `<details>` (as the prototype has it). | Reuse. PRD §8.1 "full stem, body, grading spec". |
| 20 | Digits | Teacher screens use Latin digits (`formatNumber(..., 'latin')`), like the admin tables. | design-system RTL rule: Latin in admin/staff tables. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/Question.cs` | Add `public DateTimeOffset SubmittedAt { get; private set; }` after `RetiredAt`; add `public List<QuestionDecision> Decisions { get; private set; } = [];` after `Revisions`. In `Create`, add `SubmittedAt = DateTimeOffset.UtcNow,` to the initializer. |
| `api/Elmanhg.Domain/Questions/Question.Approval.cs` | Replace the whole body (see Domain behaviour). |
| `api/Elmanhg.Domain/Questions/Question.Editing.cs` | `Update`: inside `if (contentChanged)`, after `Version += 1;`, add `SubmittedAt = DateTimeOffset.UtcNow;`. `Resubmit`: after `ValidatedAt = null;`, add `SubmittedAt = DateTimeOffset.UtcNow;`. |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Under `// QUESTIONS` append `QuestionVersionChanged = "QUESTION_VERSION_CHANGED"`. Add a `// REVIEW SESSIONS` group: `QuestionNotOpenedInSession = "QUESTION_NOT_OPENED_IN_SESSION"`, `ReviewSessionExpired = "REVIEW_SESSION_EXPIRED"`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Under `// QUESTIONS` append `QuestionVersionInvalid = "QUESTION_VERSION_INVALID"`, `QuestionRejectionReasonTooLong = "QUESTION_REJECTION_REASON_TOO_LONG"`, `QuestionAgeFilterInvalid = "QUESTION_AGE_FILTER_INVALID"`, `QuestionIdsRequired = "QUESTION_IDS_REQUIRED"`, `QuestionIdsTooMany = "QUESTION_IDS_TOO_MANY"`, `QuestionIdsDuplicate = "QUESTION_IDS_DUPLICATE"`. Add a `// REVIEW SESSIONS` group: `ReviewSessionNotFound = "REVIEW_SESSION_NOT_FOUND"`, `ReviewSessionIdRequired = "REVIEW_SESSION_ID_REQUIRED"`. |
| `api/Elmanhg.Application/DependencyInjection.cs` | Add `services.AddOptions<QuestionValidationOptions>().BindConfiguration(QuestionValidationOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` after the `ContentOptions` line. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | DbSets `QuestionDecisions`, `ReviewSessions`, `ReviewSessionOpenings` (after `QuestionImportBatches`). In `ConfigureQuestions`: `builder.HasMany(x => x.Decisions).WithOne().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);` and a `modelBuilder.Entity<QuestionDecision>` block (`Id` `ValueGeneratedNever`; `Outcome`, `Difficulty`, `DifficultyChangedFrom` as `HasConversion<string>().HasMaxLength(EnumColumnMaxLength)`; `HasIndex(x => new { x.QuestionId, x.DecidedAt })`). New private `ConfigureReviewSessions(modelBuilder)` called after `ConfigureQuestionImportBatches`: ReviewSession `HasOne<User>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(Restrict)`, `HasMany(x => x.Openings).WithOne().HasForeignKey(x => x.ReviewSessionId).OnDelete(Restrict)`, `HasIndex(x => x.TeacherId)`. ReviewSessionOpening `Id` `ValueGeneratedNever`, `HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(Restrict)`, `HasIndex(x => new { x.ReviewSessionId, x.QuestionId })` (not unique, Decision 3). Global filter: add `QuestionDecision`, `ReviewSession`, `ReviewSessionOpening`. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<IReviewSessionRepository, ReviewSessionRepository>();` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | 11 keys (see Error codes), after `QUESTION_RETIRED`. |
| `api/Elmanhg.Api/appsettings.example.json` | Add `"QuestionValidation": { "QueueMaxPageSize": 100, "QueueMaxAgeDays": 365, "RejectionReasonMaxLength": 1000, "BulkApproveMaxCount": 50, "ReviewSessionLifetimeMinutes": 480 },` after `Content`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | After `Content:ServableCountCacheSeconds`, add the five `["QuestionValidation:*"]` settings with the values above. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `twelfth => twelfth.Should().EndWith("_AddQuestionValidationQueue")`. |
| `api/Elmanhg.Tests/Builders/QuestionBuilder.cs` | `question.Approve(TeacherSubject.Create(...), question.Version);` and `question.Reject(TeacherSubject.Create(...), question.Version, _rejectionReason);`. |
| `api/Elmanhg.Tests/Integration/Content/QuestionTestData.cs` | The three `Approve`/`Reject` calls pass `question.Version`. `ReadQuestionAsync` adds `.Include(x => x.Decisions)`. Add `public static async Task SetSubmittedAtAsync(ApiFactory factory, Guid questionId, DateTimeOffset submittedAt, CancellationToken cancellationToken)` using `context.Database.ExecuteSqlAsync($"UPDATE \"Questions\" SET \"SubmittedAt\" = {submittedAt} WHERE \"Id\" = {questionId}", cancellationToken)`. |
| `api/Elmanhg.Tests/Domain/Questions/QuestionApprovalTests.cs`, `QuestionRejectionTests.cs`, `QuestionRetirementTests.cs`, `QuestionServabilityEventsTests.cs`, `api/Elmanhg.Tests/Application/Features/Questions/GetQuestions/GetQuestionsFilterTests.cs` | Mechanical: `x.Approve(a)` → `x.Approve(a, x.Version)`; `x.Reject(a, r)` → `x.Reject(a, x.Version, r)`. No assertion changes. New tests are added to the first two (Test plan). |
| `api/Elmanhg.Tests/Integration/Content/ServableQuestionCountEndpointTests.cs` | Add 2 tests plus private helpers `SeedAssignedLessonAsync(LessonState)` → `(Guid SubjectId, Guid LessonId)` and `TeacherClientAsync(Guid subjectId)` (Test plan). |
| `postman/elmanhg.postman_collection.json` | New folder `QuestionValidation` after `Questions` (API surface order). Variables `reviewSessionId`, `rejectQuestionId`, `bulkQuestionId`. |
| `web/src/routes/teacher/index.tsx` | `validateSearch: validationQueueSearchSchema, component: ValidationQueuePage` (imports from `@/features/questions`). |
| `web/src/routeTree.gen.ts` | Regenerated (router plugin) for the new route. |
| `web/src/features/questions/index.ts` | Export `ValidationQueuePage`, `ValidationQuestionPage`, `validationQueueSearchSchema`. |
| `web/src/features/questions/api/questionOptions.ts` | Add `// mirrors QuestionValidation:RejectionReasonMaxLength` `export const rejectionReasonMaxLength = 1000;`, `export const validationAgeFilters = [1, 3, 7] as const;`, `export const validationQueuePageSize = 20;`. |
| `web/src/features/questions/api/questionValues.ts` | `export type QuestionContentSource = Pick<QuestionDetailResult, 'type' \| 'stem' \| 'explanation' \| 'difficulty' \| 'objectiveId' \| 'tags' \| 'maxScore' \| 'body' \| 'gradingSpec'>;` and `toQuestionValues(detail: QuestionContentSource)`. Body unchanged. |
| `web/src/features/questions/i18n/en.json`, `ar.json` | New `validation` object (keys listed under W-i18n). |
| `web/src/shared/i18n/en.json`, `ar.json` | `errors`: add the 11 new codes, plus `SUBJECT_OUT_OF_SCOPE`, `QUESTION_RETIRED`, `QUESTION_NOT_REJECTED` if absent (same text as resx). |
| `web/src/shared/api/generated/**` | Regenerated (`npm --prefix web run gen:api`). |
| `web/src/features/shell/components/AppShell.test.tsx` | **modify**: add `beforeEach(() => { server.use(...getValidationQueueMock()); })` (the Orval-generated all-handlers export of `validation-queue.msw.ts`; use its actual name). The `/teacher` tests now trigger queue requests under `onUnhandledRequest: 'error'`. No assertion changes. |
| `docs/PRD.md` §8.1 | Replace the bulk bullet with: "Bulk approve is allowed only for questions opened in the current review session. The server records each opening against the question's current version, and a session lasts until reload, sign-out or `ReviewSessionLifetimeMinutes`. An edit after opening voids it." Add: "Approve and reject name the version the teacher reviewed; a newer version is refused (`QUESTION_VERSION_CHANGED`). Age = time since the current version entered review." |
| `docs/question-schemas.md` | §Versioning: `SubmittedAt` rules. §Validation status: the new signatures and check order (retired → pending → assigned → version), difficulty-at-approval, the `QuestionDecision` history, and the endpoints of API surface. |
| `docs/audit-log.md` | Audited commands rows: ApproveQuestion, RejectQuestion, BulkApproveQuestions. Remove the "Validation commands (E3) join…" sentence. Not audited: review sessions and openings (reason). |
| `docs/claude-design-prompt.md` §4 Teacher | `#/teacher` filters include age; items opened this session can be selected and bulk-approved with a confirm dialog; `#/teacher/q/:id` records the opening and shows the answer key, the grading spec, and the revision and decision history. |
| `docs/prototype.md` item 9 | Append: "The product also has bulk approve for questions opened in the current session and an age filter; the prototype does not simulate them." |

## Files to create
All C# files use file-scoped namespaces, one-line class declarations, `.ConfigureAwait(false)` on every await outside controllers, and no comments. Handlers guard the current user first: `if (currentUserService.UserId == null || currentUserService.UserId == default) throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);`. "404 X" means `throw new NotFoundCoreException(ErrorCodes.X)`, and "403 scope" means `throw new ForbiddenCoreException(ErrorCodes.SubjectOutOfScope)`.

### Domain
| # | Path | Type | Contract |
|---|------|------|----------|
| D1 | `api/Elmanhg.Domain/Questions/QuestionDecisionOutcome.cs` | enum | `namespace Elmanhg.Domain.Questions; public enum QuestionDecisionOutcome { Approved, Rejected }` |
| D2 | `api/Elmanhg.Domain/Questions/QuestionDecision.cs` | child entity (Morabh `OrderStatusHistory`) | `public class QuestionDecision : Entity`. Props (private set): `Guid QuestionId`, `int Version`, `QuestionDecisionOutcome Outcome`, `string? Reason`, `QuestionDifficulty Difficulty`, `QuestionDifficulty? DifficultyChangedFrom`, `Guid DecidedBy`, `DateTimeOffset DecidedAt`. `private QuestionDecision(Guid id) : base(id) { }`. `internal static QuestionDecision Create(Guid questionId, int version, QuestionDecisionOutcome outcome, string? reason, QuestionDifficulty difficulty, QuestionDifficulty? difficultyChangedFrom, Guid decidedBy, DateTimeOffset decidedAt)` → `new(Guid.NewGuid()) { … }`. |
| D3 | `api/Elmanhg.Domain/ReviewSessions/ReviewSession.cs` | aggregate (new; no Morabh equivalent) | `public class ReviewSession : AuditEntity`. Props: `Guid TeacherId`, `DateTimeOffset ExpiresAt`, `List<ReviewSessionOpening> Openings = []`. `public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;`. `private ReviewSession(Guid id, Guid? createdBy) : base(id, createdBy) { }`. Methods in Domain behaviour. |
| D4 | `api/Elmanhg.Domain/ReviewSessions/ReviewSessionOpening.cs` | child entity | `public class ReviewSessionOpening : Entity`. Props: `Guid ReviewSessionId`, `Guid QuestionId`, `int QuestionVersion`, `DateTimeOffset OpenedAt`. `internal static ReviewSessionOpening Create(Guid reviewSessionId, Guid questionId, int questionVersion)` (`OpenedAt = DateTimeOffset.UtcNow`). |
| D5 | `api/Elmanhg.Domain/ReviewSessions/IReviewSessionRepository.cs` | repo interface | `public interface IReviewSessionRepository : IRepository<ReviewSession> { }` |

### Infrastructure
| # | Path | Type | Contract |
|---|------|------|----------|
| I1 | `api/Elmanhg.Infrastructure/ReviewSessions/ReviewSessionRepository.cs` | repo | `public class ReviewSessionRepository(AppDbContext context) : Repository<ReviewSession>(context), IReviewSessionRepository { }` |
| I2 | `api/Elmanhg.Infrastructure/Migrations/<ts>_AddQuestionValidationQueue.cs` (+ `.Designer.cs`) | migration | Generated by `dotnet ef migrations add AddQuestionValidationQueue -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. It adds non-null `SubmittedAt` (generated default) and creates `QuestionDecisions`, `ReviewSessions` and `ReviewSessionOpenings`. At the **end of `Up`**, append two `migrationBuilder.Sql(...)` calls: (1) `UPDATE "Questions" AS q SET "SubmittedAt" = COALESCE((SELECT MAX(r."EditedAt") FROM "QuestionRevisions" AS r WHERE r."QuestionId" = q."Id"), q."CreationDate");` (2) `INSERT INTO "QuestionDecisions" ("Id", "QuestionId", "Version", "Outcome", "Reason", "Difficulty", "DifficultyChangedFrom", "DecidedBy", "DecidedAt", "IsDeleted", "DeletedAt") SELECT gen_random_uuid(), q."Id", q."Version", q."ValidationStatus", q."RejectionReason", q."Difficulty", NULL, q."ValidatedBy", q."ValidatedAt", false, NULL FROM "Questions" AS q WHERE q."ValidationStatus" IN ('Approved', 'Rejected') AND q."ValidatedBy" IS NOT NULL AND q."ValidatedAt" IS NOT NULL;`. The column names must match the generated table; verify in the generated `CreateTable`. `Down` stays as generated. There must be no other Drop or Rename. |

### Application — `Elmanhg.Application/QuestionValidation/` (namespace `Elmanhg.Application.QuestionValidation.<UseCase>`)
| # | Path | Type | Contract |
|---|------|------|----------|
| A1 | `Shared/Options/QuestionValidationOptions.cs` (under `Elmanhg.Application/Shared/Options/`) | options | `public sealed class QuestionValidationOptions { public const string SectionName = "QuestionValidation"; [Range(1, 1000)] public int QueueMaxPageSize { get; set; } = 100; [Range(1, 3650)] public int QueueMaxAgeDays { get; set; } = 365; [Range(1, 10000)] public int RejectionReasonMaxLength { get; set; } = 1000; [Range(1, 500)] public int BulkApproveMaxCount { get; set; } = 50; [Range(1, 1440)] public int ReviewSessionLifetimeMinutes { get; set; } = 480; }` |
| A2 | `QuestionValidation/Shared/ValidationQueueItemResult.cs` | result | `sealed record ValidationQueueItemResult(Guid Id, Guid SubjectId, Guid UnitId, string UnitName, Guid LessonId, string LessonName, string Type, string Stem, string Difficulty, int Version, DateTimeOffset SubmittedAt, bool OpenedInSession)`. Staff-facing, plain strings (no `LocalizedText` in this repo's content). |
| A3 | `QuestionValidation/Shared/ValidationQueueFiltersResult.cs` | results | `sealed record ValidationSubjectOption(Guid Id, string Name)`; `sealed record ValidationUnitOption(Guid Id, Guid SubjectId, string Name)`; `sealed record ValidationLessonOption(Guid Id, Guid UnitId, string Name)`; `sealed record ValidationQueueFiltersResult(List<ValidationSubjectOption> Subjects, List<ValidationUnitOption> Units, List<ValidationLessonOption> Lessons)`. |
| A4 | `QuestionValidation/Shared/ValidationQuestionDetailResult.cs` | results | `sealed record QuestionRevisionEntryResult(int Version, DateTimeOffset EditedAt)`; `sealed record QuestionDecisionResult(int Version, string Outcome, string? Reason, string Difficulty, string? DifficultyChangedFrom, Guid DecidedBy, string? DecidedByName, DateTimeOffset DecidedAt)`; `sealed record ValidationQuestionDetailResult(Guid Id, Guid SubjectId, string SubjectName, Guid UnitId, string UnitName, Guid LessonId, string LessonName, string LessonState, string Type, string Stem, JsonElement Body, JsonElement GradingSpec, string Explanation, string Difficulty, Guid? ObjectiveId, string? ObjectiveText, List<string> Tags, int MaxScore, int Version, string ValidationStatus, string? RejectionReason, DateTimeOffset SubmittedAt, DateTimeOffset? RetiredAt, List<QuestionRevisionEntryResult> Revisions, List<QuestionDecisionResult> Decisions)`. |
| A5 | `QuestionValidation/Shared/ValidationResultGenerator.cs` | static generator | `GenerateQueueItem(Question question, Lesson? lesson, CurriculumUnit? unit, bool openedInSession)` → names `?? string.Empty`, `UnitId = lesson?.UnitId ?? Guid.Empty`, enums `.ToString()`. `GenerateDetail(Question question, Subject? subject, CurriculumUnit? unit, Lesson lesson, Dictionary<Guid, string> deciderNames)` → `ObjectiveText = lesson.Objectives.FirstOrDefault(x => x.Id == question.ObjectiveId)?.Text`; Body/GradingSpec parsed like `QuestionResultGenerator.ParseJson` (private copy using `JsonDocument.Parse(...).RootElement.Clone()`); `Revisions` ordered by `Version`; `Decisions` ordered by `DecidedAt`, `DecidedByName = deciderNames.GetValueOrDefault(x.DecidedBy)`. Keep ≤ ~100 lines; split a `ValidationHistoryResultGenerator` if needed. |
| A6 | `QuestionValidation/Shared/ReviewSessionResult.cs` | result | `sealed record ReviewSessionResult(Guid ReviewSessionId, DateTimeOffset ExpiresAt)`. |
| A7 | `QuestionValidation/GetValidationQueue/GetValidationQueueQuery.cs` | query | `sealed record GetValidationQueueQuery(Guid? UnitId, Guid? LessonId, QuestionType? Type, QuestionDifficulty? Difficulty, int? MinAgeDays, Guid? ReviewSessionId, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<ValidationQueueItemResult>>`. |
| A8 | `…/GetValidationQueue/GetValidationQueueValidator.cs` | validator (`IOptions<QuestionValidationOptions>`) | `PageNumber` `ValidateMin(1, QuestionPageNumberInvalid)`; `PageSize` `ValidateRange(1, options.QueueMaxPageSize, QuestionPageSizeInvalid)`; `Type` `.IsInEnum().WithErrorCode(QuestionTypeInvalid)`; `Difficulty` `.IsInEnum().WithErrorCode(QuestionDifficultyInvalid)`; `RuleFor(x => x.MinAgeDays.GetValueOrDefault()).ValidateRange(1, options.QueueMaxAgeDays, QuestionAgeFilterInvalid).When(x => x.MinAgeDays.HasValue).OverridePropertyName(nameof(GetValidationQueueQuery.MinAgeDays))`. |
| A9 | `…/GetValidationQueue/ValidationQueueFilter.cs` | static | `public static Expression<Func<Question, bool>> Build(IReadOnlyCollection<Guid> subjectIds, IReadOnlyCollection<Guid>? unitLessonIds, GetValidationQueueQuery query, DateTimeOffset? submittedBefore)` → copy the query fields to locals (as `GetQuestionsFilter` does); `x => subjectIds.Contains(x.SubjectId) && x.ValidationStatus == QuestionValidationStatus.Pending && x.RetiredAt == null && (unitLessonIds == null \|\| unitLessonIds.Contains(x.LessonId)) && (lessonId == null \|\| x.LessonId == lessonId) && (type == null \|\| x.Type == type) && (difficulty == null \|\| x.Difficulty == difficulty) && (submittedBefore == null \|\| x.SubmittedAt <= submittedBefore)`. |
| A10 | `…/GetValidationQueue/GetValidationQueueHandler.cs` | handler | Deps: `IQuestionRepository, ILessonRepository, ICurriculumUnitRepository, ITeacherSubjectRepository, IReviewSessionRepository, ICurrentUserService`. Steps: 1) guard user. 2) `subjectIds` = `teacherSubjectRepository.FindAsync(x => x.TeacherId == userId, asNoTracking: true)` → `SubjectId` list. 3) `ReviewSession? session = null`; if `ReviewSessionId` has a value: `GetByIdAsync(id, include: q => q.Include(x => x.Openings), asNoTracking: true)`; null or `TeacherId != userId` → 404 `ReviewSessionNotFound`. 4) `unitLessonIds` = if `UnitId` has a value, `lessonRepository.FindAsync(x => x.UnitId == unitId, asNoTracking: true)` → ids, else null. 5) `submittedBefore = MinAgeDays is null ? null : DateTimeOffset.UtcNow.AddDays(-MinAgeDays.Value)`. 6) `FindPaginatedAsync(PageNumber, PageSize, filter: ValidationQueueFilter.Build(...), orderBy: q => q.OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id), asNoTracking: true)`. 7) Load the page's lessons (`FindAsync(ids.Contains)`) and their units; build dictionaries. 8) Map with `GenerateQueueItem(q, lesson, unit, session?.HasOpened(q) == true)` into `PageData<>` (copy the paging fields, as `GetQuestionsHandler` does). |
| A11 | `QuestionValidation/GetValidationQueueFilters/GetValidationQueueFiltersQuery.cs` | query | `sealed record GetValidationQueueFiltersQuery : IRequest<ValidationQueueFiltersResult>;` |
| A12 | `…/GetValidationQueueFilters/GetValidationQueueFiltersHandler.cs` | handler | Deps: `ITeacherSubjectRepository, ISubjectRepository, ICurriculumUnitRepository, ILessonRepository, ICurrentUserService`. 1) guard. 2) subjectIds (as in A10). 3) subjects `FindAsync(x => subjectIds.Contains(x.Id), orderBy: Order then CreationDate, asNoTracking)`. 4) units `FindAsync(x => subjectIds.Contains(x.SubjectId), orderBy: Order, asNoTracking)`. 5) lessons `FindAsync(x => unitIds.Contains(x.UnitId), orderBy: Order, asNoTracking)` (all states). 6) Map to options. No validator (no input). |
| A13 | `QuestionValidation/GetValidationQuestion/GetValidationQuestionQuery.cs` + `GetValidationQuestionValidator.cs` | query + validator | `sealed record GetValidationQuestionQuery(Guid QuestionId) : IRequest<ValidationQuestionDetailResult>`. Validator: `QuestionId` `ValidateRequired(QuestionIdRequired)`. |
| A14 | `…/GetValidationQuestion/GetValidationQuestionHandler.cs` | handler | Deps: `IQuestionRepository, ITeacherSubjectRepository, ILessonRepository, ICurriculumUnitRepository, ISubjectRepository, IUserRepository, ICurrentUserService`. 1) guard. 2) `GetByIdAsync(id, include: q => q.Include(x => x.Revisions).Include(x => x.Decisions), asNoTracking: true)`, null → 404 `QuestionNotFound`. 3) `!IsAssignedAsync(userId, question.SubjectId)` → 403 scope. 4) `lessonRepository.GetWithObjectivesAsync(question.LessonId, asNoTracking: true, …)`, null → 404 `LessonNotFound`. 5) `unitRepository.GetByIdAsync(lesson.UnitId, asNoTracking: true)`, `subjectRepository.GetByIdAsync(question.SubjectId, asNoTracking: true)` (null allowed). 6) decider names: `userRepository.FindAsync(x => ids.Contains(x.Id), asNoTracking: true)` → `DisplayName`. 7) `ValidationResultGenerator.GenerateDetail(...)`. |
| A15 | `QuestionValidation/ApproveQuestion/ApproveQuestionCommand.cs` | command | `sealed record ApproveQuestionCommand(Guid QuestionId, int Version, QuestionDifficulty? Difficulty) : IRequest, IAuditableCommand { AuditAction => "Question.Approve"; AuditResourceType => "Question"; AuditResourceId => QuestionId; }` |
| A16 | `…/ApproveQuestion/ApproveQuestionValidator.cs` | validator | `QuestionId` `ValidateRequired(QuestionIdRequired)`; `Version` `ValidateMin(1, QuestionVersionInvalid)`; `Difficulty` `.IsInEnum().WithErrorCode(QuestionDifficultyInvalid)`. |
| A17 | `…/ApproveQuestion/ApproveQuestionHandler.cs` | handler | Deps: `IQuestionRepository, ITeacherSubjectRepository, ICurrentUserService`. 1) guard. 2) `GetByIdAsync(id)` (tracked), null → 404 `QuestionNotFound`. 3) `assignment = teacherSubjectRepository.FirstOrDefaultAsync(x => x.TeacherId == userId && x.SubjectId == question.SubjectId, asNoTracking: true)`, null → 403 scope. 4) `question.Approve(assignment, request.Version, request.Difficulty)`. 5) `questionRepository.SaveChangesAsync`. |
| A18 | `QuestionValidation/RejectQuestion/RejectQuestionCommand.cs` | command | `sealed record RejectQuestionCommand(Guid QuestionId, int Version, string? Reason) : IRequest, IAuditableCommand` (`"Question.Reject"`, `"Question"`, `QuestionId`). |
| A19 | `…/RejectQuestion/RejectQuestionValidator.cs` | validator (`IOptions<QuestionValidationOptions>`) | `using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;` `QuestionId` required (`QuestionIdRequired`); `Version` `ValidateMin(1, QuestionVersionInvalid)`; `Reason` `.ValidateRequired(DomainErrorCodes.QuestionRejectionReasonRequired).ValidateMaxLength(options.RejectionReasonMaxLength, QuestionRejectionReasonTooLong)`. |
| A20 | `…/RejectQuestion/RejectQuestionHandler.cs` | handler | As A17, but step 4 is `question.Reject(assignment, request.Version, request.Reason ?? string.Empty)`. |
| A21 | `QuestionValidation/StartReviewSession/StartReviewSessionCommand.cs` + `StartReviewSessionHandler.cs` | command + handler | `sealed record StartReviewSessionCommand : IRequest<ReviewSessionResult>;`. Handler deps: `IReviewSessionRepository, IOptions<QuestionValidationOptions>, ICurrentUserService`. 1) guard. 2) `session = ReviewSession.Start(userId, TimeSpan.FromMinutes(options.ReviewSessionLifetimeMinutes))`. 3) `AddAsync`. 4) `SaveChangesAsync`. 5) `new ReviewSessionResult(session.Id, session.ExpiresAt)`. No validator. |
| A22 | `QuestionValidation/RecordQuestionOpening/RecordQuestionOpeningCommand.cs` + `RecordQuestionOpeningValidator.cs` | command + validator (shape: Morabh `Core/Core.Notifications/MarkNotificationAsRead/MarkNotificationAsReadHandler.cs`) | `sealed record RecordQuestionOpeningCommand(Guid ReviewSessionId, Guid QuestionId) : IRequest;`. Validator: `ReviewSessionId` `ValidateRequired(ReviewSessionIdRequired)`, `QuestionId` `ValidateRequired(QuestionIdRequired)`. |
| A23 | `…/RecordQuestionOpening/RecordQuestionOpeningHandler.cs` | handler | Deps: `IReviewSessionRepository, IQuestionRepository, ITeacherSubjectRepository, ICurrentUserService`. 1) guard. 2) `session = GetByIdAsync(id, include: Openings)` (tracked); null or other teacher → 404 `ReviewSessionNotFound`. 3) `question = GetByIdAsync(questionId, asNoTracking: true)`, null → 404 `QuestionNotFound`. 4) `!IsAssignedAsync(userId, question.SubjectId)` → 403 scope. 5) `session.RecordOpening(question)`. 6) `reviewSessionRepository.SaveChangesAsync`. |
| A24 | `QuestionValidation/BulkApproveQuestions/BulkApproveQuestionsCommand.cs` + `BulkApproveQuestionsResult.cs` | command + result (Morabh `Morabh.Application/Brands/Branches/ActivateBranches/*`) | `sealed record BulkApproveQuestionsCommand(Guid ReviewSessionId, IList<Guid> QuestionIds) : IRequest<BulkApproveQuestionsResult>, IAuditableCommand` (`"Question.BulkApprove"`, `"ReviewSession"`, `ReviewSessionId`). `sealed record BulkApproveQuestionsResult(int ApprovedCount)`. |
| A25 | `…/BulkApproveQuestions/BulkApproveQuestionsValidator.cs` | validator (`IOptions<QuestionValidationOptions>`) | `ReviewSessionId` `ValidateRequired(ReviewSessionIdRequired)`; `QuestionIds` `.ValidateNotEmptyList(QuestionIdsRequired).ValidateListMaxItems(options.BulkApproveMaxCount, QuestionIdsTooMany)`; `RuleFor(x => x.QuestionIds).Must(ids => ids.Distinct().Count() == ids.Count).WithErrorCode(QuestionIdsDuplicate)`; `RuleForEach(x => x.QuestionIds).ValidateRequired(QuestionIdRequired)`. |
| A26 | `…/BulkApproveQuestions/BulkApproveQuestionsHandler.cs` | handler | Deps: `IReviewSessionRepository, IQuestionRepository, ITeacherSubjectRepository, ICurrentUserService`. 1) guard. 2) session `GetByIdAsync(include: Openings, asNoTracking: true)`; null or other teacher → 404 `ReviewSessionNotFound`. 3) `questions = questionRepository.FindAsync(x => ids.Contains(x.Id))` (tracked); `questions.Count != request.QuestionIds.Count` → 404 `QuestionNotFound`. 4) `assignments = teacherSubjectRepository.FindAsync(x => x.TeacherId == userId && subjectIds.Contains(x.SubjectId), asNoTracking: true)` → dictionary by `SubjectId`. 5) For each question in **request order**: missing assignment → 403 scope; `session.EnsureOpened(question)`; `question.Approve(assignment, question.Version)`. 6) One `questionRepository.SaveChangesAsync` (nothing is saved if any step throws). 7) `new BulkApproveQuestionsResult(questions.Count)`. |

### API
| # | Path | Type | Contract |
|---|------|------|----------|
| P1 | `api/Elmanhg.Api/Controllers/QuestionValidation/Requests.cs` | requests | `sealed record ApproveQuestionRequest(int? Version, QuestionDifficulty? Difficulty);` `sealed record RejectQuestionRequest(int? Version, string? Reason);` `sealed record BulkApproveQuestionsRequest(Guid ReviewSessionId, List<Guid>? QuestionIds);` |
| P2 | `api/Elmanhg.Api/Controllers/QuestionValidation/ValidationQueueController.cs` | controller | `[ApiController][Route("api/validation-queue")][Authorize] public class ValidationQueueController(IMediator mediator) : ControllerBase`. The 8 actions of API surface, each `[Authorize(Policy = DefaultCodes.QuestionsValidate)]` with `Name = "<OperationName>"` and `ProducesResponseType`. Mapping: `request.Version.GetValueOrDefault()` and `request.QuestionIds ?? []`. |

### Web (`web/src/features/questions/` unless stated)
| # | Path | Type | Contract |
|---|------|------|----------|
| W1 | `schemas/validationQueueSearchSchema.ts` | zod | `z.object({ page: z.coerce.number().int().min(1).optional().catch(undefined), unitId: z.guid().optional().catch(undefined), lessonId: <same>, type: z.enum(questionTypes).optional().catch(undefined), difficulty: z.enum(questionDifficulties).optional().catch(undefined), minAgeDays: z.coerce.number().int().refine((value) => (validationAgeFilters as readonly number[]).includes(value)).optional().catch(undefined) })`. Export `type ValidationQueueSearch`. |
| W2 | `schemas/validationQueueFiltersSchema.ts` | zod (form) | `z.object({ unitId: z.string(), lessonId: z.string(), type: z.union([z.literal(''), z.enum(questionTypes)]), difficulty: z.union([z.literal(''), z.enum(questionDifficulties)]), minAgeDays: z.union([z.literal(''), z.enum(['1','3','7'])]) })`; `type ValidationQueueFiltersValues`. |
| W3 | `schemas/approveQuestionSchema.ts` | zod (form) | `z.object({ difficulty: z.enum(questionDifficulties, { error: 'errors.QUESTION_DIFFICULTY_INVALID' }) })`; `type ApproveQuestionValues`. |
| W4 | `schemas/rejectQuestionSchema.ts` | zod (form) | `z.object({ reason: z.string().trim().min(1, { error: 'errors.QUESTION_REJECTION_REASON_REQUIRED' }).max(rejectionReasonMaxLength, { error: 'errors.QUESTION_REJECTION_REASON_TOO_LONG' }) })`; `type RejectQuestionValues`. |
| W5 | `api/validationQueueParams.ts` | pure | `toValidationQueueParams(search: ValidationQueueSearch, reviewSessionId: string): GetValidationQueueParams` (pageNumber `search.page ?? 1`, pageSize `validationQueuePageSize`, only defined filters, always `reviewSessionId`); `hasActiveValidationFilters(search): boolean` (any of unitId, lessonId, type, difficulty, minAgeDays); `toValidationQueuePage(data: PageDataOfValidationQueueItemResult): { items; pageNumber; totalPages; totalItems }` (same coercions as `toQuestionListPage`). |
| W6 | `api/pendingAge.ts` | pure | `formatPendingAge(submittedAt: string, now: Date, lng: string): string`. Elapsed ≥ 1 day → whole days; ≥ 1 hour → hours; else minutes (min 1). Uses `new Intl.RelativeTimeFormat(numberLocale(lng, 'latin'), { numeric: 'always' }).format(-value, unit)`. |
| W7 | `api/reviewHistory.ts` | pure | `type ReviewHistoryEntry = { key: string; at: string; kind: 'revision' \| 'approved' \| 'rejected'; version: number; reason: string \| null; actorName: string \| null; difficulty: string \| null; difficultyChangedFrom: string \| null }`; `buildReviewHistory(revisions: QuestionRevisionEntryResult[], decisions: QuestionDecisionResult[]): ReviewHistoryEntry[]` merged, sorted by `at` ascending (ties: revision first). Keys are `rev-${version}` / `dec-${decidedAt}-${version}`. |
| W8 | `api/answerKey.ts` | pure | `toAnswerKey(values: QuestionValues): QuestionAnswer` → Mcq/Multi: `optionIds` = correct option ids; TrueFalse: `trueFalse = values.trueFalseAnswer === 'true'` (null if ''); Fill: `blanks[id] = first line of acceptedAnswers`; Short numeric: `text = numericValue`; Short text: `text = first accepted answer`. Other fields come from `emptyAnswer()`. |
| W9 | `hooks/useReviewSession.ts` | hook | `useQuery({ queryKey: ['questions', 'review-session'], queryFn: ({ signal }) => startReviewSession({ signal }), staleTime: Infinity, gcTime: Infinity, refetchOnWindowFocus: false, retry: false })` (generated `startReviewSession` from `@/shared/api/generated/validation-queue/validation-queue`; pass the signal through the mutator's options argument, matching the generated signature). Returns `{ reviewSessionId: data?.reviewSessionId, query, restart: () => queryClient.resetQueries({ queryKey: ['questions','review-session'] }) }`. Export `const reviewSessionErrorCodes = ['REVIEW_SESSION_EXPIRED', 'REVIEW_SESSION_NOT_FOUND'] as const`. |
| W10 | `hooks/useValidationQueueSearch.ts` | hook | Mirrors `useQuestionListSearch` with `getRouteApi('/teacher/')`: `{ search, applyFilters(values: ValidationQueueFiltersValues), setPage(page), clearFilters() }` (`minAgeDays` becomes a Number). |
| W11 | `hooks/useValidationQueue.ts` | hook | `useGetValidationQueue(toValidationQueueParams(search, reviewSessionId ?? ''), { query: { enabled: reviewSessionId !== undefined, placeholderData: keepPreviousData, select: toValidationQueuePage } })`. |
| W12 | `hooks/useBulkApprove.ts` | hook | `useBulkApproveQuestions` mutation. `approve(reviewSessionId, questionIds)` → on success: `toast(t('validation.queue.bulkApproved', { count }))`, invalidate `getGetValidationQueueQueryKey()`. On `ApiError`: `toast.error(t([common:errors.<code>, UNHANDLED]))`; if the code is in `reviewSessionErrorCodes`, call `restart()`. |
| W13 | `hooks/useQuestionDecision.ts` | hook | `useApproveQuestion`/`useRejectQuestion`. `approve({ questionId, version, difficulty })` / `reject({ questionId, version, reason })`. On success: toast (`validation.decision.approved` / `.rejected`), invalidate queue and `getGetValidationQuestionQueryKey(questionId)`, `navigate({ to: '/teacher' })`. On error: `toast.error` by code; for `QUESTION_VERSION_CHANGED`, also invalidate the detail query. Expose `isPending`. |
| W14 | `hooks/useRecordOpening.ts` | hook | `useRecordOpening(reviewSessionId: string \| undefined, question: { id: string; version: number } \| undefined)`: `useRecordQuestionOpening` mutation. `useEffect` fires `mutate` when both are defined, keyed `[reviewSessionId, question?.id, question?.version]`. On success, invalidate the queue key. On a session error code, `restart()`. Returns nothing. |
| W15 | `components/ValidationQueueFilters.tsx` | component | RHF plus `validationQueueFiltersSchema`. Props `{ search; filters: ValidationQueueFiltersResult \| undefined; onApply; onClear }`. `SelectField`s: unit (all units), lesson (lessons of the chosen unit when set, else all), type, difficulty, age (`validation.filters.ageOption` with count). Apply (`SubmitButton`) and Clear (ghost). The grid is as in `QuestionListFilters`. |
| W16 | `components/ValidationQueueItem.tsx` | component | `<li>` white item (`rounded-md border border-border bg-surface px-3.5 py-3`). Checkbox `<input type="checkbox">` with `aria-label={t('validation.queue.select', { stem })}`, `disabled={!item.openedInSession}`, size ≥ 24px. Title `<Link to="/teacher/q/$questionId">` with `stemExcerpt(item.stem)` (text-ui font-semibold). Caption: type · `unitName › lessonName` · difficulty · `vN` · `formatPendingAge`. Trailing: an `opened` badge (accent-soft) when opened, else the caption `validation.queue.openToSelect`. Props `{ item; selected; onToggle(id) ; now: Date }`. |
| W17 | `components/ValidationQueueList.tsx` | component | `<ul aria-label={t('validation.queue.listLabel')} className="flex flex-col gap-2">` mapping items. Props `{ items; selectedIds: ReadonlySet<string>; onToggle }`. `now = new Date()` read once per render. |
| W18 | `components/BulkApproveBar.tsx` | component | Shows `validation.queue.selectedCount` and a primary button `validation.queue.bulkApprove` (disabled when 0 or pending). The button opens a `Dialog`/`DialogContent` (shared/ui/dialog) titled `validation.queue.confirmTitle` with body `validation.queue.confirmBody {count}`, a Cancel (secondary) and a Confirm (primary) that calls `onConfirm`. Props `{ count; isPending; onConfirm(): Promise<void> }`. |
| W19 | `components/ValidationQuestionHeader.tsx` | component | Breadcrumb `nav` (`validation.detail.breadcrumb`): link "Review queue" → `/teacher`, current `validation.detail.title`. h1 `validation.detail.title`, `QuestionStatusBadge`, version badge (`editor.versionBadge`), a `retired` badge if `retiredAt`. Meta card: subject · unit › lesson (`validation.detail.lessonState` with state), objective, difficulty, max score, tags. If the latest `Rejected` decision exists: warning notice `validation.detail.previousRejection { reason }` (danger-soft). |
| W20 | `components/ValidationQuestionContent.tsx` | component | A card "preview as the student sees it" with `QuestionView` (`question = toStudentQuestion(values)`, `answer = toAnswerKey(values)`, `disabled`, `onAnswerChange` no-op). An explanation card via `RichTextViewer`. A `<details>` `validation.detail.gradingSpec` with a `<pre dir="ltr" className="font-mono text-mono">` of `JSON.stringify({ body, gradingSpec }, null, 2)`. |
| W21 | `components/ValidationDecisionPanel.tsx` | component | Rendered only when `validationStatus === 'Pending' && !retiredAt`. Two `Form`s: (a) approve: `SelectField` difficulty (default = current), primary `validation.decision.approve`; (b) reject: `TextAreaField` reason, danger `validation.decision.reject`. A caption `validation.decision.noEditNote`. Props `{ question: ValidationQuestionDetailResult }`; uses `useQuestionDecision`. |
| W22 | `components/ReviewHistoryTable.tsx` | component | A table in a card (`overflow-x-auto`, caption sr-only `validation.history.caption`). Columns: date (`formatDate`, latin), by (`actorName ?? '—'`), action (`validation.history.kind.<kind>` + `vN`), details (reason; `validation.history.difficultyChanged {from,to}`). An empty table is not rendered (there is always a revision). |
| W23 | `pages/ValidationQueuePage.tsx` | page | h1 `validation.queue.title`. Caption: subject names from `useGetValidationQueueFilters` joined by `، `/`, `. `ValidationQueueFilters` (keyed by search). Selection `useState<Set<string>>`, reset on search/page change via `key` on the list section. States: session or queue pending → `ContentListSkeleton label=validation.queue.loading`; error (session or queue) → `ContentErrorState title=validation.queue.errorTitle`, retry = session `refetch` or queue `refetch`; empty → `QuestionListEmptyState`-like block with `validation.queue.empty.noData` / `noResults` + clear; else the list, `BulkApproveBar` (when any item is opened) and `Pagination`. |
| W24 | `pages/ValidationQuestionPage.tsx` | page | Props `{ questionId }`. `useGetValidationQuestion(questionId)`, `useReviewSession()`, `useRecordOpening(sessionId, data)`. Pending → skeleton `validation.detail.loading`. Error → `ContentErrorState title=validation.detail.errorTitle` (403 shows the `SUBJECT_OUT_OF_SCOPE` message). Success → Header, Content, DecisionPanel, h2 `validation.history.title` + `ReviewHistoryTable`. `values = toQuestionValues(data)`. |
| W25 | `web/src/routes/teacher/q.$questionId.tsx` | route | `createFileRoute('/teacher/q/$questionId')({ component })` → `<ValidationQuestionPage questionId={Route.useParams().questionId} />`. |
| W-i18n | `i18n/en.json` / `ar.json` → `validation` | keys | `queue.{title,subjects,loading,errorTitle,listLabel,select,openToSelect,opened,age,selectedCount,bulkApprove,confirmTitle,confirmBody,confirm,cancel,bulkApproved,empty.noData,empty.noResults}`, `filters.{unit,lesson,type,difficulty,age,ageOption,all,apply,clear}`, `detail.{title,breadcrumb,queueLink,loading,errorTitle,lessonState,objective,noObjective,difficulty,maxScore,tags,retired,previousRejection,preview,explanation,gradingSpec}`, `decision.{difficulty,approve,reason,reject,noEditNote,approved,rejected}`, `history.{title,caption,date,by,action,details,kind.revision,kind.approved,kind.rejected,difficultyChanged}`. AR examples: `queue.title` "قائمة المراجعة", `queue.bulkApprove` "اعتماد المحدد", `queue.openToSelect` "افتح السؤال أولاً لتحديده", `decision.approve` "اعتماد", `decision.reject` "رفض", `decision.reason` "سبب الرفض", `decision.noEditNote` "لا يمكن للمعلّم تعديل نص السؤال أو الخيارات أو الإجابة. إذا كان هناك خطأ ارفض مع ذكر السبب.", `detail.previousRejection` "سبب الرفض السابق: {reason}", `history.title` "سجل الإصدارات والقرارات". EN `queue.title` "Review queue". Plurals use ICU (`{count, plural, …}`). |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| Domain `QuestionVersionChanged` | `QUESTION_VERSION_CHANGED` | `Question.Approve`/`Reject` | `ConflictCoreException` | 409 |
| Domain `QuestionNotOpenedInSession` | `QUESTION_NOT_OPENED_IN_SESSION` | `ReviewSession.EnsureOpened` (context `questionId`) | `BusinessRuleViolationCoreException` | 400 |
| Domain `ReviewSessionExpired` | `REVIEW_SESSION_EXPIRED` | `ReviewSession.RecordOpening`/`EnsureOpened` | `BusinessRuleViolationCoreException` | 400 |
| `ReviewSessionNotFound` | `REVIEW_SESSION_NOT_FOUND` | A10, A23, A26 | `NotFoundCoreException` | 404 |
| `ReviewSessionIdRequired` | `REVIEW_SESSION_ID_REQUIRED` | A22, A25 | validation | 422 |
| `QuestionIdsRequired` | `QUESTION_IDS_REQUIRED` | A25 | validation | 422 |
| `QuestionIdsTooMany` | `QUESTION_IDS_TOO_MANY` | A25 | validation | 422 |
| `QuestionIdsDuplicate` | `QUESTION_IDS_DUPLICATE` | A25 | validation | 422 |
| `QuestionVersionInvalid` | `QUESTION_VERSION_INVALID` | A16, A19 | validation | 422 |
| `QuestionRejectionReasonTooLong` | `QUESTION_REJECTION_REASON_TOO_LONG` | A19 | validation | 422 |
| `QuestionAgeFilterInvalid` | `QUESTION_AGE_FILTER_INVALID` | A8 | validation | 422 |
| reused `SubjectOutOfScope` | `SUBJECT_OUT_OF_SCOPE` | A14, A17, A20, A23, A26 | `ForbiddenCoreException` | 403 |
| reused `QuestionNotFound`, `LessonNotFound` | | A14/A17/A20/A23/A26, A14 | `NotFoundCoreException` | 404 |
| reused Domain `QuestionRejectionReasonRequired` | | A19 (and domain) | validation / BR | 422 / 400 |

Resource strings (ar without tashkeel / en):
- `QUESTION_VERSION_CHANGED`: "تم تعديل هذا السؤال بعد فتحه. راجع الاصدار الجديد ثم قرر." / "This question changed after you opened it. Review the new version, then decide."
- `QUESTION_NOT_OPENED_IN_SESSION`: "لا يمكن اعتماد سؤال جماعيا الا بعد فتحه في جلسة المراجعة الحالية." / "Open each question in this review session before approving it in bulk."
- `REVIEW_SESSION_EXPIRED`: "انتهت جلسة المراجعة. افتح الاسئلة مرة اخرى." / "Your review session has ended. Open the questions again."
- `REVIEW_SESSION_NOT_FOUND`: "جلسة المراجعة غير موجودة." / "Review session not found."
- `REVIEW_SESSION_ID_REQUIRED`: "جلسة المراجعة مطلوبة." / "A review session is required."
- `QUESTION_IDS_REQUIRED`: "اختر سؤالا واحدا على الاقل." / "Choose at least one question."
- `QUESTION_IDS_TOO_MANY`: "عدد الاسئلة المختارة اكبر من المسموح." / "Too many questions selected."
- `QUESTION_IDS_DUPLICATE`: "تكرر سؤال في الاختيار." / "A question is selected more than once."
- `QUESTION_VERSION_INVALID`: "اصدار السؤال غير صالح." / "The question version is not valid."
- `QUESTION_REJECTION_REASON_TOO_LONG`: "سبب الرفض طويل جدا." / "The rejection reason is too long."
- `QUESTION_AGE_FILTER_INVALID`: "مدة الانتظار غير صالحة." / "The waiting-time filter is not valid."

## Domain behaviour
`Question.Approval.cs` (full file):
```csharp
public void Approve(TeacherSubject assignment, int reviewedVersion, QuestionDifficulty? difficulty = null)
{
    EnsureValidatorCanDecide(assignment, reviewedVersion);

    var now = DateTimeOffset.UtcNow;
    QuestionDifficulty? changedFrom = null;
    if (difficulty is not null && difficulty.Value != Difficulty)
    {
        changedFrom = Difficulty;
        Difficulty = difficulty.Value;
    }

    ValidationStatus = QuestionValidationStatus.Approved;
    ValidatedBy = assignment.TeacherId;
    ValidatedAt = now;
    UpdatedBy = assignment.TeacherId;
    UpdationDate = now;
    Decisions.Add(QuestionDecision.Create(Id, Version, QuestionDecisionOutcome.Approved, null, Difficulty, changedFrom, assignment.TeacherId, now));
    RaiseDomainEvent(new QuestionApproved(Id, LessonId));
}

public void Reject(TeacherSubject assignment, int reviewedVersion, string reason)
{
    EnsureValidatorCanDecide(assignment, reviewedVersion);
    if (string.IsNullOrWhiteSpace(reason)) { throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionRejectionReasonRequired); }

    var now = DateTimeOffset.UtcNow;
    ValidationStatus = QuestionValidationStatus.Rejected;
    RejectionReason = reason.Trim();
    ValidatedBy = assignment.TeacherId;
    ValidatedAt = now;
    UpdatedBy = assignment.TeacherId;
    UpdationDate = now;
    Decisions.Add(QuestionDecision.Create(Id, Version, QuestionDecisionOutcome.Rejected, RejectionReason, Difficulty, null, assignment.TeacherId, now));
    RaiseDomainEvent(new QuestionRejected(Id, LessonId));
}

private void EnsureValidatorCanDecide(TeacherSubject assignment, int reviewedVersion)
{
    EnsureNotRetired();
    if (ValidationStatus != QuestionValidationStatus.Pending) { throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionNotPending); }
    if (assignment.IsDeleted || assignment.SubjectId != SubjectId) { throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionValidatorNotAssigned); }
    if (reviewedVersion != Version) { throw new ConflictCoreException(ErrorCodes.QuestionVersionChanged); }
}
```
(Use braces on separate lines per style. Neither method changes `Version`, `Revisions` or `SubmittedAt`.)

`ReviewSession`:
```csharp
public static ReviewSession Start(Guid teacherId, TimeSpan lifetime)
{
    return new ReviewSession(Guid.NewGuid(), teacherId) { TeacherId = teacherId, ExpiresAt = DateTimeOffset.UtcNow.Add(lifetime) };
}
public bool HasOpened(Question question) => !IsExpired && Openings.Any(x => x.QuestionId == question.Id && x.QuestionVersion == question.Version);
public void RecordOpening(Question question)
{
    EnsureActive();
    if (!Openings.Any(x => x.QuestionId == question.Id && x.QuestionVersion == question.Version))
    {
        Openings.Add(ReviewSessionOpening.Create(Id, question.Id, question.Version));
    }
    UpdatedBy = TeacherId;
    UpdationDate = DateTimeOffset.UtcNow;
}
public void EnsureOpened(Question question)
{
    EnsureActive();
    if (!HasOpened(question))
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionNotOpenedInSession, context: new Dictionary<string, object> { ["questionId"] = question.Id });
    }
}
private void EnsureActive() { if (IsExpired) { throw new BusinessRuleViolationCoreException(ErrorCodes.ReviewSessionExpired); } }
```
`SubmittedAt` is stamped with `DateTimeOffset.UtcNow` at `Create`, on a content-changing `Update` (next to `Version += 1`), and on `Resubmit`. A metadata-only `Update` leaves it unchanged.

## API surface
All on `ValidationQueueController`, `[Authorize(Policy = DefaultCodes.QuestionsValidate)]`. Postman order is the table order.

| # | Method · route | Name | Request | Response |
|---|---|---|---|---|
| 1 | POST `api/validation-queue/review-sessions` | StartReviewSession | — | `ReviewSessionResult` |
| 2 | GET `api/validation-queue/filters` | GetValidationQueueFilters | — | `ValidationQueueFiltersResult` |
| 3 | GET `api/validation-queue` | GetValidationQueue | `[FromQuery] Guid? unitId, Guid? lessonId, QuestionType? type, QuestionDifficulty? difficulty, int? minAgeDays, Guid? reviewSessionId, int pageNumber = 1, int pageSize = 20` | `PageData<ValidationQueueItemResult>` |
| 4 | GET `api/validation-queue/questions/{questionId:guid}` | GetValidationQuestion | route | `ValidationQuestionDetailResult` |
| 5 | POST `api/validation-queue/review-sessions/{reviewSessionId:guid}/openings/{questionId:guid}` | RecordQuestionOpening | route | 200, empty |
| 6 | POST `api/validation-queue/questions/{questionId:guid}/approve` | ApproveQuestion | `ApproveQuestionRequest` | 200, empty |
| 7 | POST `api/validation-queue/questions/{questionId:guid}/reject` | RejectQuestion | `RejectQuestionRequest` (Postman uses `{{rejectQuestionId}}`) | 200, empty |
| 8 | POST `api/validation-queue/bulk-approve` | BulkApproveQuestions | `BulkApproveQuestionsRequest` (Postman body `{"reviewSessionId":"{{reviewSessionId}}","questionIds":["{{bulkQuestionId}}"]}`, preceded in description by opening it) | `BulkApproveQuestionsResult` |

## Test plan
API unit tests use FluentAssertions and NSubstitute, as in the existing suite. Handler success asserts the outcome plus `SaveChangesAsync` `Received(1)`. Every throw asserts type, code, and `DidNotReceive()` save. `Q` means `new QuestionBuilder()`.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `Domain/Questions/QuestionApprovalTests` (add) | `Approve_WithNewDifficulty_ChangesDifficultyAndRecordsPrevious` | Difficulty Hard; `Decisions.Single()` Outcome Approved, DifficultyChangedFrom Medium, Difficulty Hard; Version 1 |
| 2 | 〃 | `Approve_SameDifficulty_RecordsNoDifficultyChange` | DifficultyChangedFrom null |
| 3 | 〃 | `Approve_StaleVersion_ThrowsQuestionVersionChanged` | `ConflictCoreException` code; still Pending; no decision |
| 4 | 〃 | `Approve_Pending_AppendsDecisionWithVersionAndTeacher` | decision Version, DecidedBy == TeacherId, DecidedAt == ValidatedAt |
| 5 | 〃 | `Approve_Pending_KeepsSubmittedAtAndVersion` | SubmittedAt unchanged, Version 1, Revisions count 1 |
| 6 | `Domain/Questions/QuestionRejectionTests` (add) | `Reject_WithReason_AppendsRejectedDecisionWithTrimmedReason` | decision Outcome Rejected, Reason trimmed |
| 7 | 〃 | `Reject_StaleVersion_ThrowsQuestionVersionChanged` | Conflict code; Pending; no decision |
| 8 | `Domain/Questions/QuestionQueueEntryTests` (new) | `Create_SetsSubmittedAtToNow` | `BeCloseTo(UtcNow, 5s)` |
| 9 | 〃 | `Update_MetadataOnly_KeepsSubmittedAt` | equal to before |
| 10 | 〃 | `Update_ContentChange_ResetsSubmittedAt` | `BeOnOrAfter(before)` and close to now; Version 2 |
| 11 | 〃 | `Resubmit_Rejected_ResetsSubmittedAt` | `BeOnOrAfter(before)`; Pending |
| 12 | `Domain/ReviewSessions/ReviewSessionTests` (new) | `Start_SetsTeacherCreatorAndExpiry` | TeacherId, CreatedBy, ExpiresAt ≈ now+lifetime, not expired |
| 13 | 〃 | `RecordOpening_ActiveSession_AddsOpeningAtCurrentVersion` | one opening, QuestionVersion 1 |
| 14 | 〃 | `RecordOpening_SameVersionTwice_AddsOnce` | count 1 |
| 15 | 〃 | `RecordOpening_ExpiredSession_ThrowsReviewSessionExpired` | `Start(id, TimeSpan.Zero)`; code; no opening |
| 16 | 〃 | `HasOpened_AfterContentEdit_ReturnsFalse` | opened v1, then `Update` content → false |
| 17 | 〃 | `EnsureOpened_Opened_DoesNotThrow` | no exception |
| 18 | 〃 | `EnsureOpened_NotOpened_ThrowsWithQuestionIdContext` | code + `Context["questionId"]` |
| 19 | 〃 | `EnsureOpened_ExpiredSession_ThrowsReviewSessionExpired` | code |
| 20 | `Application/Features/QuestionValidation/ApproveQuestion/ApproveQuestionHandlerTests` | `Handle_AssignedTeacher_ApprovesAndSaves` | Approved, ValidatedBy current user, 1 decision, save Received(1) |
| 21 | 〃 | `Handle_WithDifficulty_ChangesDifficulty` | Difficulty Hard |
| 22 | 〃 | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | 401 type + code, no save |
| 23 | 〃 | `Handle_QuestionMissing_ThrowsQuestionNotFound` | |
| 24 | 〃 | `Handle_TeacherNotAssigned_ThrowsSubjectOutOfScope` | `ForbiddenCoreException`; still Pending |
| 25 | 〃 | `Handle_StaleVersion_ThrowsQuestionVersionChanged` | Conflict; no save |
| 26 | `…/ApproveQuestion/ApproveQuestionValidatorTests` | `Validate_ValidCommand_Passes`; `Validate_EmptyQuestionId_FailsQuestionIdRequired`; `Validate_VersionZero_FailsQuestionVersionInvalid`; `Validate_UnknownDifficulty_FailsQuestionDifficultyInvalid` | codes |
| 27 | `…/RejectQuestion/RejectQuestionHandlerTests` | `Handle_AssignedTeacher_RejectsWithReasonAndSaves`; `Handle_NoCurrentUser_ThrowsUserNotAuthenticated`; `Handle_QuestionMissing_ThrowsQuestionNotFound`; `Handle_TeacherNotAssigned_ThrowsSubjectOutOfScope`; `Handle_StaleVersion_ThrowsQuestionVersionChanged` | as rows 20–25 |
| 28 | `…/RejectQuestion/RejectQuestionValidatorTests` | `Validate_ValidCommand_Passes`; `Validate_EmptyQuestionId_Fails…`; `Validate_VersionZero_Fails…`; `Validate_BlankReason_FailsQuestionRejectionReasonRequired`; `Validate_ReasonOverMax_FailsQuestionRejectionReasonTooLong` | codes (options via `Options.Create(new QuestionValidationOptions())`) |
| 29 | `…/StartReviewSession/StartReviewSessionHandlerTests` | `Handle_Teacher_AddsSessionWithConfiguredLifetimeAndSaves` | `AddAsync` received session with TeacherId; result id/expiry ≈ now+480m; save Received(1) |
| 30 | 〃 | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | no add, no save |
| 31 | `…/RecordQuestionOpening/RecordQuestionOpeningHandlerTests` | `Handle_AssignedQuestion_RecordsOpeningAndSaves`; `Handle_NoCurrentUser_Throws…`; `Handle_SessionMissing_ThrowsReviewSessionNotFound`; `Handle_OtherTeachersSession_ThrowsReviewSessionNotFound`; `Handle_QuestionMissing_ThrowsQuestionNotFound`; `Handle_OutOfScope_ThrowsSubjectOutOfScope`; `Handle_ExpiredSession_ThrowsReviewSessionExpired` | opening count / codes / save |
| 32 | `…/RecordQuestionOpening/RecordQuestionOpeningValidatorTests` | `Validate_Valid_Passes`; `Validate_EmptySession_FailsReviewSessionIdRequired`; `Validate_EmptyQuestion_FailsQuestionIdRequired` | codes |
| 33 | `…/BulkApproveQuestions/BulkApproveQuestionsHandlerTests` | `Handle_AllOpened_ApprovesEveryQuestionAndSaves` | both Approved, result 2, save Received(1) |
| 34 | 〃 | `Handle_NoCurrentUser_Throws…`; `Handle_SessionMissing_ThrowsReviewSessionNotFound`; `Handle_OtherTeachersSession_ThrowsReviewSessionNotFound`; `Handle_QuestionMissing_ThrowsQuestionNotFound`; `Handle_QuestionOutOfScope_ThrowsSubjectOutOfScope` | codes; no save |
| 35 | 〃 | `Handle_OneNotOpened_ThrowsQuestionNotOpenedInSessionAndSavesNothing` | code; no save |
| 36 | 〃 | `Handle_OpenedEarlierVersion_ThrowsQuestionNotOpenedInSession` | opened v1, question now v2 |
| 37 | 〃 | `Handle_ExpiredSession_ThrowsReviewSessionExpired` | code; no save |
| 38 | `…/BulkApproveQuestions/BulkApproveQuestionsValidatorTests` | `Validate_Valid_Passes`; `…EmptySession_FailsReviewSessionIdRequired`; `…EmptyList_FailsQuestionIdsRequired`; `…OverMax_FailsQuestionIdsTooMany`; `…Duplicates_FailsQuestionIdsDuplicate`; `…EmptyGuidItem_FailsQuestionIdRequired` | codes |
| 39 | `…/GetValidationQueue/ValidationQueueFilterTests` | `Build_PendingInAssignedSubject_Matches`; `Build_OtherSubject_Excludes`; `Build_Approved_Excludes`; `Build_Retired_Excludes`; `Build_UnitLessonIds_ExcludesOtherLessons`; `Build_LessonId_Matches`; `Build_Type_Excludes`; `Build_Difficulty_Excludes`; `Build_SubmittedBefore_ExcludesNewer` | compiled expression over builder questions |
| 40 | `…/GetValidationQueue/GetValidationQueueHandlerTests` | `Handle_Teacher_ReturnsItemsWithLessonAndUnitNames` | names, SubmittedAt, OpenedInSession false |
| 41 | 〃 | `Handle_WithOwnReviewSession_MarksOpenedItems` | opened true only for the opened one |
| 42 | 〃 | `Handle_ReviewSessionOfOtherTeacher_ThrowsReviewSessionNotFound` | 404 code |
| 43 | 〃 | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | |
| 44 | `…/GetValidationQueue/GetValidationQueueValidatorTests` | `Validate_Defaults_Passes`; `…PageZero_FailsQuestionPageNumberInvalid`; `…PageSizeOverMax_FailsQuestionPageSizeInvalid`; `…UnknownType_FailsQuestionTypeInvalid`; `…UnknownDifficulty_FailsQuestionDifficultyInvalid`; `…AgeZero_FailsQuestionAgeFilterInvalid`; `…AgeOverMax_FailsQuestionAgeFilterInvalid` | codes |
| 45 | `…/GetValidationQueueFilters/GetValidationQueueFiltersHandlerTests` | `Handle_Teacher_ReturnsAssignedSubjectsUnitsAndLessons`; `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | option lists |
| 46 | `…/GetValidationQuestion/GetValidationQuestionHandlerTests` | `Handle_AssignedTeacher_ReturnsDetailWithHistoryAndObjective` | Revisions, Decisions with DecidedByName, ObjectiveText, LessonState |
| 47 | 〃 | `Handle_NoCurrentUser_Throws…`; `Handle_QuestionMissing_ThrowsQuestionNotFound`; `Handle_OutOfScope_ThrowsSubjectOutOfScope`; `Handle_LessonMissing_ThrowsLessonNotFound` | codes |
| 48 | `…/GetValidationQuestion/GetValidationQuestionValidatorTests` | `Validate_Valid_Passes`; `Validate_EmptyId_FailsQuestionIdRequired` | codes |
| 49 | `Integration/QuestionValidation/ValidationTestData.cs` (helper, no tests) | — | `SeedAssignedTeacherAsync(factory, subjectId)` → `(User, HttpClient)`, `SeedSubjectTreeAsync(factory, name)` → `(SubjectId, UnitId, LessonId)` (Draft lesson), `StartSessionAsync(client)` → Guid. Reuses `ScopeTestData`, `ContentTestData`, `QuestionTestData`. |
| 50 | `Integration/QuestionValidation/ValidationQueueEndpointTests` | `Get_AssignedTeacher_ListsPendingOfAssignedSubjectsOnly` | 200; only the physics pending id (not approved, not math) |
| 51 | 〃 | `Get_LessonAndDifficultyFilters_ReturnMatchingOnly` | |
| 52 | 〃 | `Get_MinAgeDays_ExcludesRecentlySubmitted` | backdated via `SetSubmittedAtAsync` included; fresh excluded; oldest first |
| 53 | 〃 | `Get_InvalidPageSize_Returns422` | problem code `QUESTION_PAGE_SIZE_INVALID` |
| 54 | 〃 | `Get_AsAdmin_Returns403`; `Get_Anonymous_Returns401` | status |
| 55 | 〃 | `GetFilters_AssignedTeacher_ReturnsOnlyAssignedTree` | subjects/units/lessons ids |
| 56 | 〃 | `GetQuestion_ResubmittedQuestion_ReturnsRejectionHistory` | seed rejected, admin resubmit via API; detail Pending with 1 Rejected decision + reason |
| 57 | 〃 | `GetQuestion_OtherSubject_Returns403SubjectOutOfScope`; `GetQuestion_Unknown_Returns404QuestionNotFound` | codes |
| 58 | 〃 | `PutQuestion_ContentEdit_ResetsSubmittedAt` | backdate, admin PUT new stem, DB SubmittedAt ≥ test start |
| 59 | `Integration/QuestionValidation/QuestionDecisionEndpointTests` | `Approve_AssignedTeacher_ApprovesStampsValidatorAndAudits` | 200; DB Approved, ValidatedBy teacher, 1 decision; audit `Question.Approve` Success |
| 60 | 〃 | `Approve_WithDifficulty_ChangesDifficultyWithoutVersionBump` | Difficulty Hard, Version 1 |
| 61 | 〃 | `Approve_StaleVersion_Returns409QuestionVersionChanged` | status + code; Pending |
| 62 | 〃 | `Approve_OtherSubject_Returns403SubjectOutOfScope` | |
| 63 | 〃 | `Approve_AsAdmin_Returns403AndStaysPending`; `Approve_Anonymous_Returns401` | |
| 64 | 〃 | `Reject_WithReason_RejectsAndAudits` | Rejected, reason, audit `Question.Reject` |
| 65 | 〃 | `Reject_BlankReason_Returns422QuestionRejectionReasonRequired` | Pending |
| 66 | 〃 | `Reject_RetiredQuestion_Returns400QuestionRetired` | |
| 67 | 〃 | `PutQuestion_AsAssignedTeacher_Returns403AndContentUnchanged`; `ResubmitQuestion_AsAssignedTeacher_Returns403` | stem unchanged |
| 68 | `Integration/QuestionValidation/BulkApproveEndpointTests` | `StartSession_Teacher_ReturnsSessionAndExpiry`; `StartSession_AsAdmin_Returns403` | |
| 69 | 〃 | `RecordOpening_AssignedQuestion_MarksItemOpenedInQueue` | queue `openedInSession` true for it |
| 70 | 〃 | `RecordOpening_OtherTeachersSession_Returns404ReviewSessionNotFound`; `RecordOpening_OtherSubject_Returns403` | |
| 71 | 〃 | `BulkApprove_AllOpened_ApprovesAllAndAudits` | 200 `approvedCount` 2; DB both Approved; audit `Question.BulkApprove` on session id |
| 72 | 〃 | `BulkApprove_OneNotOpened_Returns400AndApprovesNone` | code `QUESTION_NOT_OPENED_IN_SESSION`; both Pending |
| 73 | 〃 | `BulkApprove_OpenedBeforeContentEdit_Returns400QuestionNotOpenedInSession` | admin PUT after opening |
| 74 | 〃 | `BulkApprove_OtherTeachersSession_Returns404`; `BulkApprove_EmptyList_Returns422QuestionIdsRequired`; `BulkApprove_AsAdmin_Returns403`; `BulkApprove_Anonymous_Returns401` | |
| 75 | `Integration/Content/ServableQuestionCountEndpointTests` (add) | `Get_AfterTeacherApproves_CountsIt` | Published lesson, pending Q, assigned teacher; baseline; POST approve v1 → 200; count == baseline + 1 |
| 76 | 〃 | `Get_AfterTeacherRejects_RefreshesCachedCount` | seed Draft lesson L2 with an approved question, and pending P on a Published lesson; baseline (warms cache); `ExecuteSqlAsync` sets L2 `State='Published'` (no event); POST reject P → 200; count == baseline + 1 (only the reject's invalidation can surface it) |
| 77 | `Integration/Persistence/AppDbContextTests` (modify) | existing | twelfth migration |
| W1 | `schemas/validationQueueSearchSchema.test.ts` | `accepts valid filters`; `drops an invalid guid`; `drops an age that is not offered`; `coerces the page number` | parse output |
| W2 | `schemas/validationQueueFiltersSchema.test.ts` | `accepts empty filters`; `rejects an unsupported age` | |
| W3 | `schemas/approveQuestionSchema.test.ts` | `accepts a known difficulty`; `rejects an unknown difficulty with the error key` | |
| W4 | `schemas/rejectQuestionSchema.test.ts` | `accepts a reason`; `rejects a blank reason with the required key`; `rejects a reason over the maximum with the too-long key` | |
| W5 | `api/validationQueueParams.test.ts` | `maps filters and the session id`; `omits empty filters`; `detects active filters` | |
| W6 | `api/pendingAge.test.ts` | `formats days in English`; `formats hours when under a day`; `formats at least one minute`; `formats days in Arabic with Latin digits` | |
| W7 | `api/reviewHistory.test.ts` | `merges revisions and decisions by date`; `keeps the rejection reason and difficulty change` | |
| W8 | `api/answerKey.test.ts` | one `it` per type: mcq, multi, true/false, fill, short numeric, short text | |
| W9 | `pages/ValidationQueuePage.test.tsx` | `shows pending questions after loading`; `shows the empty state when nothing is pending`; `offers clear filters when filters match nothing`; `shows retry on server error`; `sends chosen filters with the review session id`; `disables selection for questions not opened in this session`; `bulk-approves selected opened questions after confirming`; `shows the server error when bulk approval is refused`; `renders right-to-left in Arabic`; `has no axe violations` | DOM, captured request URL/body, toast text |
| W10 | `pages/ValidationQuestionPage.test.tsx` | `shows the question with version, answer key and history`; `shows the previous rejection reason`; `records the opening for this review session`; `approves with the chosen difficulty and returns to the queue`; `requires a reason before rejecting`; `rejects with a reason`; `shows a message when the question changed`; `shows an error for a question outside my subjects`; `hides decisions for a question that is not pending`; `renders right-to-left in Arabic`; `has no axe violations` | |
| W11 | `features/shell/components/AppShell.test.tsx` (modify) | existing | handlers registered only |

## Definition of done
- [ ] Every sub-task is delivered: queue query with its 5 filters; approve with optional difficulty that records `validatedBy`/`validatedAt`; reject requiring a reason; guards proven by tests 63 and 67; teacher UI (queue, detail with revision and rejection history, actions); server-enforced bulk approve.
- [ ] `Question.Approve`/`Reject` are the only writers of Approved/Rejected. They check retired, then pending, then assigned, then version; append a `QuestionDecision`; raise their event; and set `UpdationDate`.
- [ ] Bulk approve is all-or-nothing, checks session ownership, expiry and an opening at the current version per question, and is audited as `Question.BulkApprove`.
- [ ] Every `api/validation-queue` action has `QuestionsValidate`; `EndpointAuthorizationTests` is green.
- [ ] The migration `AddQuestionValidationQueue` includes both backfill SQL statements and has no destructive operations; `AppDbContextTests` is updated; `HasPendingModelChanges` is false.
- [ ] 11 new error codes are in both resx files and in both web `common` locale files.
- [ ] `QuestionValidationOptions` has code defaults and `ValidateOnStart`, and is in `appsettings.example.json` and `ApiFactory`.
- [ ] `dotnet test api/ -c Release` is green with `appsettings.json` moved aside (CI parity).
- [ ] `api/openapi/v1.json`, `web/src/shared/api/generated` and `routeTree.gen.ts` are regenerated with no drift.
- [ ] Postman `QuestionValidation` folder is in the API surface order.
- [ ] Web: typecheck, lint, format:check and `vitest run --coverage` pass. There are no literal colours or sizes and no physical-direction classes. Every string is in en and ar.
- [ ] No existing test is changed except the rows listed under Existing code touched (mechanical call-site updates, AppShell handlers, migration list).
- [ ] Docs are updated: PRD §8.1, `question-schemas.md`, `audit-log.md`, `claude-design-prompt.md` §4, `prototype.md` item 9.
- [ ] Guard grep is clean: no `DateTime.Now/UtcNow`, `.Result`, `FromSqlRaw`, `async void` in the diff.
