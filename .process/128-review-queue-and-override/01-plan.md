# Plan — [E17.S1] Review queue and override (#128, v2)

Worktree `D:/Personal/elmanhg-wt/128`, branch `feature/128-review-queue-and-override`.

**Hard precondition: #123 (LLM step grading) must be merged into `main` first.** This plan builds on #123's `MathStepGrade` aggregate, which is planned in `D:/Personal/elmanhg-wt/123/.process/123-llm-step-grading/01-plan.md` (D-1…D-8, P-12…P-26, I-5, I-6, F-5…F-9). Before writing any code, run `git merge origin/main`. If `api/Elmanhg.Domain/MathStepGrading/MathStepGrade.cs` does not exist after the merge, stop and report `BLOCKED: #123 not merged`. Every file marked **(#123)** below is created by #123 and is edited additively here.

Other lanes run in parallel: #125 (DragDrop) touches `QuestionType.cs`, `QuestionSchemaRules.cs`, `ContentOptions.cs`, `QuestionBuilder.cs`, `ApiFactory.cs` and `openapi/v1.json`. This plan does not edit those files, except to regenerate `openapi/v1.json`.

## Goal
A teacher opens «مراجعة التصحيح» (`/teacher/grades`). They see, for each subject they are assigned to, the AI essay grades and math step grades that are waiting in review: low confidence, grading failed, or a final answer the CAS could not check after its retries. They open one and see the question, the grading key, the student's answer, and the AI's score, confidence and reasons. They then **accept** the AI score or **override** it with their own score and a comment. The decision makes the score final in one save. It writes the attempt (`GradedBy = Teacher`), updates mastery and the quiz or exam score, and writes the training rows. The decision is audited and safe against two teachers deciding at once. The student sees «قيد المراجعة» turn into the graded result, with the teacher's note, through a realtime push.

## Scope
**In:**
- **API:**
  - The review domain on `EssayGrade` and `MathStepGrade`: accept, override, final score, and the `EssayGradeReviewed` event (the #118 follow-up "teacher-resolve event").
  - Queue, subject summary, detail and review endpoints, all subject-scoped.
  - A `GradeReview` options section.
  - The `xmin` conflict mapping.
  - A `Trigger` column and review columns on `EssayGradeTrainingRecords`, with one export line per essay grade.
  - A realtime `gradeReviewed` push to the student.
  - The student grade results carry the review note.
  - Audit, migration, OpenAPI, Postman.
- **Web:**
  - The teacher queue and detail pages.
  - The teacher nav (3 tabs + «المزيد»).
  - The student-side teacher note on essay and math grades.
  - Realtime refresh.
- **Docs.**

**Out:**
- **Legacy `mathUnchecked` attempts from #122.** They are not listed. #123 stops creating them, and there has been no live deploy (#112), so they exist only in development databases. See D3.
- An admin review UI. The API allows Admin (PRD §16), but there is no admin screen.
- A math grader-calibration training table. #123 left it as a follow-up; see D12.
- Per-criterion or per-step teacher scores. The teacher sets a total; see D6.

**Deferred:** none. Nothing here needs credentials.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | What is reviewable | (a) `EssayGrade` with `Status = InReview` (`LowConfidence` or `GradingFailed`). (b) `MathStepGrade` (#123) with `Status = InReview` (`LowConfidence`, `GradingFailed` or `FinalAnswerUnchecked`). Grades from admin **test-mode** sessions are excluded from the queue and the counts (join on `Sessions.IsTestMode = false`). | Orchestrator update: both follow the "grade aggregate → apply" pattern. Teachers must not grade an admin's trial runs. |
| D2 | How the final score reaches the student without mutating attempts | The decision is stored on the grade aggregate: `ReviewDecision`, `ReviewedScore`, `ReviewedNormalisedScore`, `ReviewComment`, `ReviewedBy`, `ReviewedAt`. It flips `InReview → Graded`. In the **same handler and save**, the existing recorder writes the attempt (`EssayAttemptRecorder` / `MathStepAttemptRecorder`), mastery is recorded, and `MarkApplied` runs. No attempt is ever updated. | An in-review grade has no attempt yet (essay-grading.md; #123 D8), so the review is the first attempt. One save makes the decision atomic with the attempt, mastery and `ScorePercent`. |
| D3 | Legacy `mathUnchecked` attempts (#122) | Not reviewable. The PRD §8.3, backlog E17 and `math-cas.md` text that promised it is changed in this story (Docs). | Resolving an append-only attempt needs an override record that every score reader honours: session score, breakdowns, selection SQL, dashboard SQL and mastery. That is a large cross-cutting change for rows that exist only in dev databases (no live deploy, #112), and #123 stops creating them. |
| D4 | The AI score is kept | The AI's `Score`/`NormalisedScore` are never overwritten. `FinalScore => ReviewedScore ?? Score` and `FinalNormalisedScore => ReviewedNormalisedScore ?? NormalisedScore`. **Accept** copies the AI score into `ReviewedScore`. | The calibration pair (AI vs teacher) stays on the row and in the training record. The readers use `Final*` only. |
| D5 | When Accept is allowed | Only when the AI produced a score (`Score` not null): essay `LowConfidence`, math `LowConfidence`. Otherwise it is 400 `GRADE_REVIEW_NO_AI_SCORE`. `GradingFailed` and `FinalAnswerUnchecked` can only be overridden. | There is nothing to accept. |
| D6 | The override input | A total score `0 ≤ score ≤ MaxScore` with at most 2 decimals. The normalised score is `round(score / MaxScore, 4, AwayFromZero)`. There are no per-criterion or per-step marks. A **comment is required on override** and optional on accept. It is at most `GradeReview:CommentMaxLength` (2000) characters, and whitespace-only counts as none. | PRD §8.3 says "override with a score and comment". The comment explains the change to the student and labels the training example. A total keeps the teacher UI simple. |
| D7 | `Attempt.GradedBy` after a review | `Teacher` for both accept and override (`GradedBy => ReviewDecision is null ? AI : Teacher` on both aggregates). | A teacher made the score final (PRD §15 `graded_by[Auto\|AI\|Teacher]`). Accept vs override detail lives on the grade and in the essay training row. |
| D8 | What the student sees after a review | The grade result status is `Graded` with `Final*` scores and the outcome from `FinalNormalisedScore`, plus `review { decision, comment, reviewedAt }` (no teacher identity). On **Overridden**, the AI criteria/steps are `[]` and the AI justification is `null`, so they are not shown next to a score they no longer explain. On **Accepted**, the AI detail stays. On an overridden math grade, the attempt feedback is `null`; on an accepted one, the stored feedback is kept. | The AI's reasons would contradict the teacher's score. |
| D9 | Subject scoping (`ISubjectScopedRequest`) | Every queue, detail and review request carries `SubjectId` from the route (`api/subjects/{subjectId}/grade-reviews/...`). `SubjectScopeBehaviour` returns 403 `SUBJECT_OUT_OF_SCOPE` for an unassigned teacher. Handlers also filter by `x.SubjectId == request.SubjectId`, so a grade from another subject is 404 `GRADE_REVIEW_NOT_FOUND` (BOLA-safe). The subject summary (`GET api/grade-reviews/subjects`) has no subject: it lists the teacher's assignments, or every subject for Admin. | Orchestrator requirement. It fails closed, and the id cannot jump subjects. |
| D10 | Policy | `DefaultCodes.AiGradesOverride` (already Teacher + Admin in `PermissionMatrixPolicies`) on every action. | PRD §16 "Override AI grade (v2): Teacher (assigned) ✓, Admin ✓". It already exists. |
| D11 | Concurrency (`xmin`) | Both grades already have `Version` as `xmin`. The domain guard `EnsureInReview` → 409 `GRADE_NOT_IN_REVIEW` for a second decision. A lost race in the same instant fails the save: `AppDbContext` maps `DbUpdateConcurrencyException` on `EssayGrade`/`MathStepGrade`, and the unique violation on the new `IX_EssayGradeTrainingRecords_EssayGradeId_Trigger`, to 409 `GRADE_MODIFIED_CONCURRENTLY`. A race on the session row keeps the existing 409 `SESSION_MODIFIED_CONCURRENTLY` / `SESSION_QUESTION_ALREADY_ANSWERED`. The web treats every 409 on submit alike. There is no client version token. | A grade in review changes only through a review, so the status check plus `xmin` is enough. The worker only touches `Pending` or unapplied `Graded` grades, never `InReview`. |
| D12 | Training records, no double counting | **Essay:** the new `EssayGradeReviewed` event writes an `EssayGradeTrainingRecords` row with `Trigger = TeacherReviewed`: every AI field, plus decision, reviewed score, comment and `ReviewedAt`. `OccurredAt` equals the AI `GradedAt`, the same as the `Completed` row. The export (`GetExportPageAsync`) drops a `Completed` row when a `TeacherReviewed` row exists for the same `EssayGradeId`, so each essay grade gives one line. `GradingFailed` reviews write no essay row, because there is no AI output to calibrate. **Both kinds:** the applied attempt raises `AttemptsRecorded` as today, so `AttemptTrainingRecords` gets one row with `GradedBy = Teacher` and the final score. That row is the "training record for every override", for math too. `AttemptTrainingRecords` is unchanged. | This follows #110 plan D4 ("#128 adds that event and a `Trigger` column"). Sharing `OccurredAt` keeps both rows in the same export range, so dedupe is exact. The attempt row is written once, as for AI-applied essays. |
| D13 | Queue shape | There is one list per `(subject, kind)`, with `kind` ∈ `Essay` / `MathSteps` as a required query parameter defaulting to `Essay`. It is ordered oldest first (`RequestedAt`, then `Id`) and paged (`PageData`, page size ≤ `GradeReview:QueueMaxPageSize` 50). The summary gives counts per subject and kind, which feed the tabs. | Two tables cannot be paged together without a new cross-aggregate abstraction. Tabs match the counts. |
| D14 | "Confidence threshold configuration" sub-task | Routing uses the existing `EssayGrading:ReviewConfidenceThreshold` and `MathStepGrading:ReviewConfidenceThreshold` (both 0.7). The queue shows each grade's confidence. No new threshold is added. | The threshold is applied when the grade completes (#118, #123). A second threshold would disagree with it. |
| D15 | Detail content | The detail reads the **served revision** (`GetRevisionsAsync`, `Version == QuestionVersion`): stem, type, body and grading spec. It also has the lesson and unit names, the student's answer JSON, the AI result (score, normalised score, confidence, justification, criteria or steps, final-answer verdict), the reason, `RequestedAt`, and `review` once decided. It has **no student identity**. Only grades that were ever in review are readable (`ReviewReason != null`); any other grade is 404. | The teacher must judge against what the student answered. Blind grading, and PRD §8.4. |
| D16 | Student notification | A new `IGradeReviewNotifier` sends the SignalR event `gradeReviewed` `{ sessionId, questionId }` to the student after the commit. It is best-effort, like `SignalRTeacherThreadNotifier`. The web's existing `useStudentRealtime` (one connection) invalidates the essay/math grade, session, exam-session and mastery queries, and toasts. | #97 channel; it is cheap. A second `useRealtimeEvents` would open a second connection. |
| D17 | Audit | `ReviewEssayGradeCommand` (`EssayGrade.Review`) and `ReviewMathStepGradeCommand` (`MathStepGrade.Review`) are `IAuditableCommand`. `EssayGrade` and `MathStepGrade` gain `IAuditedEntity` through their new partial files, so the diff lists the review fields and status. | Constitution rule 13 / PRD §14: validation decisions are audited. |
| D18 | Shared validation | `IGradeReviewInput { Decision, Score, Comment }` is implemented by both commands. `GradeReviewInputValidator : AbstractValidator<IGradeReviewInput>` holds the rules, and each command validator `Include`s it (`IValidator<in T>` is contravariant). | This avoids duplicating 7 rules and their tests. |
| D19 | Teacher nav | Teacher items become `queue`, `gradeReviews`, `inbox`, `stats`. `tabBarKeys = ['queue','gradeReviews','inbox']`, `morePath = '/teacher/more'` (stats moves under «المزيد»). | #135 (confirmed): 3 tabs + «المزيد». Review is daily work; stats are occasional. |
| D20 | Routes (web) | List `/teacher/grades?subjectId&kind&page`, detail `/teacher/grade/$subjectId/$kind/$gradeId` with `kind` ∈ `essay` / `math-steps`, and more `/teacher/more`. A distinct `grades` / `grade.` prefix keeps them sibling routes (no `<Outlet/>`). | This mirrors `admin/questions.tsx` vs `admin/question.$questionId.tsx`. |
| D21 | Morabh reuse | None applicable. I checked `D:\Personal\Projects\Projects\Morabh\repos\apis` for review, override and queue, and found nothing. Everything is **new — no Morabh equivalent**, mirroring this repo's #118/#119/#123 grade slices and the #68 validation queue. | Reuse-first rule. |

## Existing code touched
Paths verified in this worktree, except **(#123)**, which exist after the merge.

### api/
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/EssayGrading/EssayGrade.Grading.cs` | `ToQuestionGrade()`: the guard becomes `Status != Graded \|\| FinalScore is null \|\| FinalNormalisedScore is null`, and it returns `new QuestionGrade(FinalScore.Value, FinalNormalisedScore.Value, QuestionGrade.ToOutcome(FinalNormalisedScore.Value), null)`. |
| `api/Elmanhg.Domain/EssayGrading/IEssayGradeRepository.cs` | Add `Task<PageData<EssayGrade>> GetInReviewPageAsync(Guid subjectId, int pageNumber, int pageSize, CancellationToken cancellationToken);` and `Task<Dictionary<Guid, int>> CountInReviewBySubjectAsync(IReadOnlyCollection<Guid>? subjectIds, CancellationToken cancellationToken);` (`using Core.DDD.Models;`). |
| `api/Elmanhg.Domain/MathStepGrading/IMathStepGradeRepository.cs` **(#123)** | The same two methods for `MathStepGrade`. |
| `api/Elmanhg.Domain/MathStepGrading/MathStepGrade.Grading.cs` **(#123)** | `ToQuestionGrade()`: use `FinalScore`/`FinalNormalisedScore` (the same guard shape as the essay), with feedback `ReviewDecision == GradeReviewDecision.Overridden \|\| Feedback is null ? null : Deserialize<GradeFeedback>(Feedback)`. |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | New group `// GRADE REVIEW`: `GradeNotInReview = "GRADE_NOT_IN_REVIEW"`, `GradeReviewNoAiScore = "GRADE_REVIEW_NO_AI_SCORE"`, `GradeReviewScoreOutOfRange = "GRADE_REVIEW_SCORE_OUT_OF_RANGE"`. |
| `api/Elmanhg.Domain/TrainingData/EssayGradeTrainingRecord.cs` | Add props `EssayGradeTrainingTrigger Trigger`, `GradeReviewDecision? ReviewDecision`, `decimal? ReviewedScore`, `decimal? ReviewedNormalisedScore`, `string? ReviewComment`, `DateTimeOffset? ReviewedAt` (all `private set`). `From(...)` sets `Trigger = EssayGradeTrainingTrigger.Completed`. Add `FromReview` (Domain behaviour). The file may exceed 100 lines; if so, move `FromReview` to a new partial `EssayGradeTrainingRecord.Review.cs` and make the class `partial`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | New group `// GRADE REVIEW` (Error codes, the Application rows). |
| `api/Elmanhg.Application/EssayGrading/Shared/EssayAttemptRecorder.cs` | Pass `grade.GradedBy` instead of `AttemptGrader.AI` to `session.RecordEssayAttempt(...)`. |
| `api/Elmanhg.Application/EssayGrading/Shared/EssayGradeResult.cs` | Append the positional parameter `GradeReviewNoteResult? Review` (`using Elmanhg.Application.GradeReviews.Shared;`). |
| `api/Elmanhg.Application/EssayGrading/Shared/EssayGradeResultGenerator.cs` | `Generate`: the two non-graded branches pass `Review: null`. The Graded branch uses `FinalScore`, `FinalNormalisedScore` and the outcome from `FinalNormalisedScore`. `criteria = grade.ReviewDecision == Overridden ? [] : mapped AI criteria`, `justification = Overridden ? null : grade.Justification`, `Review = GradeReviewResultGenerator.Note(grade.ReviewDecision, grade.ReviewComment, grade.ReviewedAt)`. |
| `api/Elmanhg.Application/MathStepGrading/Shared/MathStepAttemptRecorder.cs` **(#123)** | Pass `grade.GradedBy` instead of `AttemptGrader.AI`. |
| `api/Elmanhg.Application/MathStepGrading/Shared/MathStepGradeResult.cs` **(#123)** | Append `GradeReviewNoteResult? Review`. |
| `api/Elmanhg.Application/MathStepGrading/Shared/MathStepGradeResultGenerator.cs` **(#123)** | `Generate`: non-graded branches → `Review: null`. Graded → `Final*` scores and outcome, `FinalAnswerVerdict = grade.FinalAnswerVerdict?.ToString()` (null-safe: an overridden `FinalAnswerUnchecked` grade has no verdict), `Steps = Overridden ? [] : mapped`, `Justification = Overridden ? null : grade.Justification`, `Review = GradeReviewResultGenerator.Note(...)`. |
| `api/Elmanhg.Application/TrainingExports/Shared/TrainingExportLines.cs` | `EssayGradeExportLine`: append `EssayGradeTrainingTrigger Trigger, GradeReviewDecision? ReviewDecision, decimal? ReviewedScore, decimal? ReviewedNormalisedScore, string? ReviewComment, DateTimeOffset? ReviewedAt`. |
| `api/Elmanhg.Application/TrainingExports/Shared/TrainingExportLineGenerator.cs` | `EssayGrade(record)`: pass the new fields, with `ReviewComment = record.ReviewComment is null ? null : TrainingDataScrubber.ScrubText(record.ReviewComment)`. |
| `api/Elmanhg.Application/DependencyInjection.cs` | `services.AddOptions<GradeReviewOptions>().BindConfiguration(GradeReviewOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` next to the other options. |
| `api/Elmanhg.Infrastructure/EssayGrading/EssayGradeRepository.cs` | Implement both new methods (Repository queries). |
| `api/Elmanhg.Infrastructure/MathStepGrading/MathStepGradeRepository.cs` **(#123)** | Implement both new methods (the same queries on `MathStepGradeStatus.InReview`). |
| `api/Elmanhg.Infrastructure/TrainingData/EssayGradeTrainingRecordRepository.cs` | `GetExportPageAsync`: after the range `Where`, add `.Where(x => x.Trigger == EssayGradeTrainingTrigger.TeacherReviewed \|\| !_dbSet.Any(y => y.EssayGradeId == x.EssayGradeId && y.Trigger == EssayGradeTrainingTrigger.TeacherReviewed))`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `ConfigureEssayGrades`: `builder.Property(x => x.ReviewDecision).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);`, `builder.Property(x => x.ReviewedScore).HasPrecision(9, 2);`, `builder.Property(x => x.ReviewedNormalisedScore).HasPrecision(5, 4);` (`ReviewComment` is `text`, with no max in the DB: the cap lives in options, skill §8.1). `SaveChangesAsync`: add after the `TrainingExport` catch `catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(x => x.Entity is EssayGrade or MathStepGrade)) { throw new ConflictCoreException(ErrorCodes.GradeModifiedConcurrently, innerException: exception); }`, and among the unique-violation catches `catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: EssayGradeTrainingTriggerIndex }) { throw new ConflictCoreException(ErrorCodes.GradeModifiedConcurrently, innerException: exception); }`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.MathStepGrading.cs` **(#123)** | In `ConfigureMathStepGrades`: the same three property lines for `MathStepGrade`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.TrainingData.cs` | Add `public const string EssayGradeTrainingTriggerIndex = "IX_EssayGradeTrainingRecords_EssayGradeId_Trigger";`. In the `EssayGradeTrainingRecord` builder: `Trigger` and `ReviewDecision` `.HasConversion<string>().HasMaxLength(EnumColumnMaxLength)`, `ReviewedScore` (9,2), `ReviewedNormalisedScore` (5,4). Replace `builder.HasIndex(x => x.EssayGradeId).IsUnique();` with `builder.HasIndex(x => new { x.EssayGradeId, x.Trigger }).IsUnique().HasDatabaseName(EssayGradeTrainingTriggerIndex);`. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add AddGradeReviews -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. |
| `api/Elmanhg.Api/Realtime/RealtimeEvents.cs` | Add `public const string GradeReviewed = "gradeReviewed";`. |
| `api/Elmanhg.Api/Realtime/RealtimeExtensions.cs` | `services.AddSingleton<IGradeReviewNotifier, SignalRGradeReviewNotifier>();` after the teacher-thread notifier. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | One key per new code (Error codes), placed after the `ESSAY_GRADE_NOT_PENDING` line. |
| `api/Elmanhg.Api/appsettings.example.json` | Add `"GradeReview": { "CommentMaxLength": 2000, "QueueMaxPageSize": 50 },` after the `EssayGrading` section (or after `MathStepGrading` once #123 is merged). |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Builders/EssayGradeBuilder.cs` | `_subjectId` is no longer `readonly`. Add `public EssayGradeBuilder ForSubject(Guid subjectId)`, and `public static EssayGrade InReview(EssayGradeBuilder builder, decimal confidence = 0.5m)`, which builds and calls `Complete(Assessment(confidence), new QuestionGrade(2.5m, 0.5m, GradeOutcome.Partial, null), 0.7m, DefaultRequestedAt.AddSeconds(40))`. Add `public static EssayGrade GradingFailed(EssayGradeBuilder builder)`, which builds and calls `FailAttempt("ESSAY_GRADING_UNAVAILABLE", DefaultRequestedAt.AddSeconds(5), 1, TimeSpan.FromSeconds(30))`. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append the next ordinal after the last entry present after the merge, `… => ….Should().EndWith("_AddGradeReviews")` (the accepted pattern). |
| Existing test classes listed as **modify** in the Test plan | Add only the listed tests. |

### Migration `AddGradeReviews` (review the generated file)
- `EssayGrades` and `MathStepGrades` each get these nullable columns: `ReviewDecision` varchar(50), `ReviewedBy` uuid, `ReviewedAt` timestamptz, `ReviewComment` text, `ReviewedScore` numeric(9,2), `ReviewedNormalisedScore` numeric(5,4).
- `EssayGradeTrainingRecords`:
  - `Trigger` varchar(50) **NOT NULL**. Hand-edit the generated `AddColumn` to `defaultValue: "Completed"`; the model has no default, so the snapshot is untouched. PG 11+ adds a constant default without rewriting rows, so the append-only row trigger never fires.
  - Nullable `ReviewDecision`, `ReviewedScore`, `ReviewedNormalisedScore`, `ReviewComment`, `ReviewedAt`.
  - `DropIndex IX_EssayGradeTrainingRecords_EssayGradeId` and `CreateIndex IX_EssayGradeTrainingRecords_EssayGradeId_Trigger` (unique). This index swap is the only drop, and it is intended.
- No other operation.

### web/
| File | Change |
|------|--------|
| `web/src/features/shell/navConfig.ts` | Teacher items: after `queue`, insert `{ key: 'gradeReviews', to: '/teacher/grades', labelKey: 'nav.teacher.gradeReviews', icon: ClipboardCheck, capability: 'aiGradesOverride' }` (import `ClipboardCheck` from lucide-react). `tabBarKeys: ['queue', 'gradeReviews', 'inbox']`, `morePath: '/teacher/more'`. |
| `web/src/features/shell/i18n/en.json` / `ar.json` | `nav.teacher.gradeReviews`: "AI grades" / «مراجعة التصحيح». |
| `web/src/features/questions/index.ts` | Also export `MathStepsReadOnly` (`./components/MathStepsReadOnly`), `GradingKeyView` (new), `stemExcerpt` (`./api/stemExcerpt`), `formatPendingAge` (`./api/pendingAge`), and `MathStepScoreList` (`./components/MathStepScoreList`, **#123**) if #123 did not already export it. |
| `web/src/features/quiz/components/EssayGradeOutcome.tsx` | Render `<EssayCriteriaList>` only when `grade.criteria.length > 0`, and the justification block only when `grade.justification !== null`. After them, render `{grade.review ? <TeacherReviewNote review={grade.review} /> : null}`. |
| `web/src/features/quiz/components/MathStepGradeOutcome.tsx` **(#123)** | Render the final-answer verdict line only when `grade.finalAnswerVerdict !== null`. Append `{grade.review ? <TeacherReviewNote review={grade.review} /> : null}`. |
| `web/src/features/quiz/i18n/en.json` / `ar.json` | Keys under `teacherReview` (see F-17). |
| `web/src/shared/realtime/realtimeClient.ts` | `realtimeEventNames = ['teacherReplyReceived', 'teacherThreadReminder', 'gradeReviewed'] as const`. |
| `web/src/shared/realtime/realtimeEvents.ts` | `export const gradeReviewedSchema = z.object({ sessionId: z.uuid(), questionId: z.uuid() });` |
| `web/src/features/askTeacher/hooks/useStudentRealtime.ts` | Add a `gradeReviewed` handler that parses with `gradeReviewedSchema` and returns on failure. It invalidates `getGetEssayGradeQueryKey(sessionId, questionId)`, `getGetMathStepGradeQueryKey(sessionId, questionId)` (#123 generated), `getGetSessionQueryKey(sessionId)` and `getGetExamSessionQueryKey(sessionId)`, then calls `void invalidateMastery(queryClient)` (from `@/features/mastery`) and `toast.info(t('realtime.gradeReviewed'))`. |
| `web/src/features/askTeacher/i18n/en.json` / `ar.json` | `realtime.gradeReviewed`: "Your teacher reviewed one of your answers. The grade is now final." / «راجع معلمك إحدى إجاباتك، وأصبحت درجتها نهائية.» |
| `web/src/app/i18n.ts` | Register `gradeReview: gradeReviewLocales.ar` / `.en` and add `'gradeReview'` to `ns`. |
| `web/src/test/essayGradeFixtures.ts` | Add `review: null` to `hidden`. Add `acceptedEssayGrade = { ...gradedEssayGrade, review: { decision: 'Accepted', comment: null, reviewedAt: '2026-10-01T09:00:00Z' } }` and `overriddenEssayGrade = { ...gradedEssayGrade, score: 4, normalisedScore: 0.8, outcome: 'Partial', justification: null, criteria: [], review: { decision: 'Overridden', comment: 'Good example; full marks for the definition.', reviewedAt: '2026-10-01T09:00:00Z' } }`. |
| `web/src/test/mathStepGradeFixtures.ts` **(#123)** | Add `review: null` to every existing grade fixture. Add `overriddenMathStepGrade = { ...gradedMathStepGrade, score: 2, normalisedScore: 1, outcome: 'Correct', finalAnswerVerdict: null, justification: null, steps: [], review: { decision: 'Overridden', comment: 'Correct method.', reviewedAt: '2026-10-01T09:00:00Z' } }`. |
| `web/src/shared/api/generated/**` | Regenerated with `npm --prefix web run gen:api`. |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (`npm --prefix web run build`). |

### Other
| File | Change |
|------|--------|
| `postman/elmanhg.postman_collection.json` | New folder "Grade reviews", in this order: `GET {{baseUrl}}/api/grade-reviews/subjects`; `GET …/api/subjects/{{subjectId}}/grade-reviews?kind=Essay`; `…?kind=MathSteps`; `GET …/grade-reviews/essays/{{reviewEssayGradeId}}`; `POST …/grade-reviews/essays/{{reviewEssayGradeId}}` with body `{"decision":"Overridden","score":3.5,"comment":"…"}`; `GET …/grade-reviews/math-steps/{{reviewMathStepGradeId}}`; `POST …/grade-reviews/math-steps/{{reviewMathStepGradeId}}` with body `{"decision":"Accepted","score":null,"comment":null}`. Teacher bearer. The folder description says that an in-review grade needs a real grader or a seeded row (the fake grader never goes below 0.7). |

## Files to create

### api/ — Domain
| # | Path | Type | Contract |
|---|------|------|----------|
| D-1 | `api/Elmanhg.Domain/Questions/Grading/GradeReviewDecision.cs` | enum | `namespace Elmanhg.Domain.Questions.Grading; public enum GradeReviewDecision { Accepted, Overridden }` |
| D-2 | `api/Elmanhg.Domain/Questions/Grading/GradeReviewScore.cs` | static | `public static class GradeReviewScore { private const int NormalisedScoreDecimals = 4; public static decimal Normalise(decimal score, int maxScore) => Math.Round(score / maxScore, NormalisedScoreDecimals, MidpointRounding.AwayFromZero); }` with a WHY comment on the const ("matches `QuestionGrade`'s 4 decimals for mastery thresholds"). |
| D-3 | `api/Elmanhg.Domain/EssayGrading/EssayGrade.Review.cs` | partial | `public partial class EssayGrade : IAuditedEntity`, with review props (`private set`): `GradeReviewDecision? ReviewDecision`, `Guid? ReviewedBy`, `DateTimeOffset? ReviewedAt`, `string? ReviewComment`, `decimal? ReviewedScore`, `decimal? ReviewedNormalisedScore`. Computed: `public decimal? FinalScore => ReviewedScore ?? Score;`, `public decimal? FinalNormalisedScore => ReviewedNormalisedScore ?? NormalisedScore;`, `public AttemptGrader GradedBy => ReviewDecision is null ? AttemptGrader.AI : AttemptGrader.Teacher;`. Methods `Accept(Guid teacherId, string? comment, DateTimeOffset reviewedAt)`, `Override(decimal score, Guid teacherId, string? comment, DateTimeOffset reviewedAt)`, private `Resolve(...)`, private `EnsureInReview()` (Domain behaviour). `using Core.DDD.Entities;` for `IAuditedEntity`. |
| D-4 | `api/Elmanhg.Domain/EssayGrading/EssayGradeReviewed.cs` | event | `public sealed record EssayGradeReviewed(EssayGrade Grade) : DomainEvent;` |
| D-5 | `api/Elmanhg.Domain/MathStepGrading/MathStepGrade.Review.cs` | partial | `public partial class MathStepGrade : IAuditedEntity`, with the same props, computed members and `Accept` / `Override` / `Resolve` / `EnsureInReview` as D-3, on `MathStepGradeStatus`. **No domain event.** |
| D-6 | `api/Elmanhg.Domain/TrainingData/EssayGradeTrainingTrigger.cs` | enum | `public enum EssayGradeTrainingTrigger { Completed, TeacherReviewed }` |

### api/ — Application (namespace root `Elmanhg.Application.GradeReviews`)
| # | Path | Type | Contract |
|---|------|------|----------|
| P-1 | `api/Elmanhg.Application/Shared/Options/GradeReviewOptions.cs` | options | `public sealed class GradeReviewOptions { public const string SectionName = "GradeReview"; [Range(1, 10000)] public int CommentMaxLength { get; set; } = 2000; [Range(1, 100)] public int QueueMaxPageSize { get; set; } = 50; }` |
| P-2 | `api/Elmanhg.Application/Shared/Realtime/IGradeReviewNotifier.cs` | port | `public interface IGradeReviewNotifier { Task NotifyReviewedAsync(Guid studentId, Guid sessionId, Guid questionId, CancellationToken cancellationToken); }` |
| P-3 | `GradeReviews/Shared/GradeReviewKind.cs` | enum | `public enum GradeReviewKind { Essay, MathSteps }` |
| P-4 | `GradeReviews/Shared/IGradeReviewInput.cs` | interface | `GradeReviewDecision Decision { get; } decimal? Score { get; } string? Comment { get; }` |
| P-5 | `GradeReviews/Shared/GradeReviewInputValidator.cs` | validator | `public sealed class GradeReviewInputValidator : AbstractValidator<IGradeReviewInput>`, ctor `(IOptions<GradeReviewOptions> gradeReviewOptions)`. Rules, in order: (1) `RuleFor(x => x.Decision).IsInEnum().WithErrorCode(ErrorCodes.GradeReviewDecisionInvalid);` (2) `RuleFor(x => x.Score).ValidateRequired(ErrorCodes.GradeReviewScoreRequired).When(x => x.Decision == GradeReviewDecision.Overridden);` (3) `RuleFor(x => x.Score).Null().WithErrorCode(ErrorCodes.GradeReviewScoreNotAllowed).When(x => x.Decision == GradeReviewDecision.Accepted);` (4) `RuleFor(x => x.Score!.Value).ValidateNonNegative(ErrorCodes.GradeReviewScoreInvalid).Must(x => decimal.Round(x, 2) == x).WithErrorCode(ErrorCodes.GradeReviewScoreInvalid).When(x => x.Score.HasValue).OverridePropertyName(nameof(IGradeReviewInput.Score));` (5) `RuleFor(x => x.Comment).Must(x => !string.IsNullOrWhiteSpace(x)).WithErrorCode(ErrorCodes.GradeReviewCommentRequired).When(x => x.Decision == GradeReviewDecision.Overridden);` (6) `RuleFor(x => x.Comment).ValidateMaxLength(options.CommentMaxLength, ErrorCodes.GradeReviewCommentTooLong);` Verify the exact `ValidateNonNegative` signature in `core-libraries/Core.Validation`. |
| P-6 | `GradeReviews/Shared/GradeReviewNoteResult.cs` | result (student + teacher) | `public sealed record GradeReviewNoteResult(string Decision, string? Comment, DateTimeOffset ReviewedAt);` It has no teacher identity and no localisation (teacher text). |
| P-7 | `GradeReviews/Shared/GradeReviewSubjectResult.cs` | result (teacher) | `(Guid SubjectId, string Name, int EssayCount, int MathStepsCount)` |
| P-8 | `GradeReviews/Shared/GradeReviewItemResult.cs` | result (teacher) | `(Guid Id, string Kind, Guid QuestionId, string Stem, string UnitName, string LessonName, string ReviewReason, int MaxScore, decimal? AiScore, decimal? Confidence, DateTimeOffset RequestedAt)`. `Stem` is the current question HTML (the web makes the excerpt). |
| P-9 | `GradeReviews/Shared/GradeReviewDetailResult.cs` | result (teacher) | `(Guid Id, string Kind, Guid SubjectId, Guid QuestionId, int QuestionVersion, string QuestionType, string Stem, JsonElement Body, JsonElement GradingSpec, string UnitName, string LessonName, JsonElement Answer, int MaxScore, string Status, string ReviewReason, DateTimeOffset RequestedAt, decimal? AiScore, decimal? Confidence, string? Justification, IReadOnlyList<EssayCriterionResult> Criteria, IReadOnlyList<MathStepScoreResult> Steps, string? FinalAnswerVerdict, decimal? FinalScore, GradeReviewNoteResult? Review)`. `Criteria` is `[]` for math and `Steps` is `[]` for essays. It carries no student identity (D15). |
| P-10 | `GradeReviews/Shared/GradeReviewPlacement.cs` | record | `public sealed record GradeReviewPlacement(string UnitName, string LessonName);` plus `public static GradeReviewPlacement Unknown { get; } = new(string.Empty, string.Empty);` |
| P-11 | `GradeReviews/Shared/GradeReviewPlacementLoader.cs` | static | `LoadAsync(IReadOnlyCollection<Guid> questionIds, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, CancellationToken ct) : Task<(Dictionary<Guid, Question> Questions, Dictionary<Guid, GradeReviewPlacement> Placements)>`. It makes 3 queries: questions `FindAsync(x => ids.Contains(x.Id), include: q => q.IgnoreQueryFilters(), asNoTracking: true)`, lessons by the distinct `LessonId`s, and units by the distinct `UnitId`s (`asNoTracking: true`). A missing lesson or unit gives `Unknown`. |
| P-12 | `GradeReviews/Shared/GradeReviewRevisionLoader.cs` | static | `LoadAsync(Guid questionId, int version, IQuestionRepository questionRepository, CancellationToken ct) : Task<QuestionRevisionSnapshot>`: `(await GetRevisionsAsync([questionId], ct)).FirstOrDefault(x => x.Version == version)?.ReadSnapshot() ?? throw new NotFoundCoreException(ErrorCodes.QuestionNotFound)`. |
| P-13 | `GradeReviews/Shared/GradeReviewResultGenerator.cs` | static | `Note(GradeReviewDecision? decision, string? comment, DateTimeOffset? reviewedAt) : GradeReviewNoteResult?` returns null when `decision` or `reviewedAt` is null, else `new(decision.ToString(), comment, reviewedAt.Value)`. `Item(EssayGrade grade, Question? question, GradeReviewPlacement placement)` and `Item(MathStepGrade grade, …)` → `GradeReviewItemResult` (Kind `nameof(GradeReviewKind.Essay)` / `nameof(GradeReviewKind.MathSteps)`, `Stem = question?.Stem ?? string.Empty`, `ReviewReason = grade.ReviewReason!.Value.ToString()`, `AiScore = grade.Score`). The file stays ≤ 100 lines; the Detail methods go in P-14. |
| P-14 | `GradeReviews/Shared/GradeReviewDetailGenerator.cs` | static | `Essay(EssayGrade grade, QuestionRevisionSnapshot snapshot, GradeReviewPlacement placement)` and `MathSteps(MathStepGrade grade, QuestionRevisionSnapshot snapshot, GradeReviewPlacement placement)` → `GradeReviewDetailResult`. Body/spec use `JsonSerializer.SerializeToElement(snapshot.Body ?? new JsonObject())` (the same for `GradingSpec`), and `Answer = JsonDocument.Parse(grade.Answer).RootElement.Clone()`. `Status = grade.Status.ToString()`. Essay criteria: `grade.ReadCriteria().Select(x => new EssayCriterionResult(x.CriterionId, x.Title, x.Points, x.MaxPoints, x.Justification))`. Math steps: `grade.ReadSteps().Select(x => new MathStepScoreResult(x.StepIndex, x.Step, x.Points, x.MaxPoints, x.Justification))`, with `FinalAnswerVerdict = grade.FinalAnswerVerdict?.ToString()`. `FinalScore = grade.ReviewDecision is null ? null : grade.FinalScore`. `Review = GradeReviewResultGenerator.Note(...)`. The teacher always sees the AI fields, including after an override. |
| P-15 | `GradeReviews/GetGradeReviewSubjects/GetGradeReviewSubjectsQuery.cs` | query | `public sealed record GetGradeReviewSubjectsQuery : IRequest<List<GradeReviewSubjectResult>>;` |
| P-16 | `…/GetGradeReviewSubjects/GetGradeReviewSubjectsHandler.cs` | handler | Deps `(ITeacherSubjectRepository teacherSubjectRepository, ISubjectRepository subjectRepository, IEssayGradeRepository essayGradeRepository, IMathStepGradeRepository mathStepGradeRepository, ICurrentUserService currentUserService)`. (1) Null user → `UnauthorizedCoreException(UserNotAuthenticated)`. (2) `isAdmin = currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin)`. (3) Admin → `subjectIds = null`, subjects = `subjectRepository.FindAsync(_ => true, asNoTracking: true)`. Teacher → assignments `FindAsync(x => x.TeacherId == userId, asNoTracking)`, ids, subjects `FindAsync(x => ids.Contains(x.Id), asNoTracking)`. (4) `essays = await essayGradeRepository.CountInReviewBySubjectAsync(subjectIds, ct)`, then `maths = …` (sequential awaits). (5) Return subjects ordered by `Order`, then `Name`, then `Id`, mapped to `new GradeReviewSubjectResult(x.Id, x.Name, essays.GetValueOrDefault(x.Id), maths.GetValueOrDefault(x.Id))`. Subjects with 0 are included. |
| P-17 | `GradeReviews/GetGradeReviewQueue/GetGradeReviewQueueQuery.cs` | query | `public sealed record GetGradeReviewQueueQuery(Guid SubjectId, GradeReviewKind Kind, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<GradeReviewItemResult>>, ISubjectScopedRequest;` |
| P-18 | `…/GetGradeReviewQueue/GetGradeReviewQueueValidator.cs` | validator | Ctor `(IOptions<GradeReviewOptions>)`. `RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);` `RuleFor(x => x.Kind).IsInEnum().WithErrorCode(ErrorCodes.GradeReviewKindInvalid);` `RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.GradeReviewPageNumberInvalid);` `RuleFor(x => x.PageSize).ValidateRange(1, options.QueueMaxPageSize, ErrorCodes.GradeReviewPageSizeInvalid);` |
| P-19 | `…/GetGradeReviewQueue/GetGradeReviewQueueHandler.cs` | handler | Deps `(IEssayGradeRepository, IMathStepGradeRepository, IQuestionRepository, ILessonRepository, ICurriculumUnitRepository)`. `Handle` → `request.Kind switch { Essay => await EssaysAsync(request, ct), MathSteps => await MathStepsAsync(request, ct), _ => throw new BadRequestCoreException(ErrorCodes.GradeReviewKindInvalid) }` (an expression-bodied switch over awaited private methods). Each private method: (1) `page = repo.GetInReviewPageAsync(request.SubjectId, request.PageNumber, request.PageSize, ct)`; (2) `(questions, placements) = GradeReviewPlacementLoader.LoadAsync(page.Items.Select(x => x.QuestionId).Distinct().ToList(), …)`; (3) return `new PageData<GradeReviewItemResult> { Items = page.Items.Select(x => GradeReviewResultGenerator.Item(x, questions.GetValueOrDefault(x.QuestionId), placements.GetValueOrDefault(x.QuestionId) ?? GradeReviewPlacement.Unknown)).ToList(), PageNumber = page.PageNumber, PageSize = page.PageSize, TotalItems = page.TotalItems, TotalPages = page.TotalPages }`. |
| P-20 | `GradeReviews/GetEssayGradeReview/GetEssayGradeReviewQuery.cs` | query | `(Guid SubjectId, Guid EssayGradeId) : IRequest<GradeReviewDetailResult>, ISubjectScopedRequest` |
| P-21 | `…/GetEssayGradeReview/GetEssayGradeReviewValidator.cs` | validator | `SubjectId` → `ValidateRequired(ErrorCodes.SubjectIdRequired)`; `EssayGradeId` → `ValidateRequired(ErrorCodes.GradeReviewIdRequired)`. |
| P-22 | `…/GetEssayGradeReview/GetEssayGradeReviewHandler.cs` | handler | Deps `(IEssayGradeRepository, IQuestionRepository, ILessonRepository, ICurriculumUnitRepository)`. (1) `grade = FirstOrDefaultAsync(x => x.Id == request.EssayGradeId && x.SubjectId == request.SubjectId && x.ReviewReason != null, ct, asNoTracking: true) ?? throw new NotFoundCoreException(ErrorCodes.GradeReviewNotFound)`. (2) `snapshot = GradeReviewRevisionLoader.LoadAsync(grade.QuestionId, grade.QuestionVersion, …)`. (3) `(_, placements) = GradeReviewPlacementLoader.LoadAsync([grade.QuestionId], …)`. (4) `return GradeReviewDetailGenerator.Essay(grade, snapshot, placements.GetValueOrDefault(grade.QuestionId) ?? GradeReviewPlacement.Unknown)`. |
| P-23 | `GradeReviews/GetMathStepGradeReview/GetMathStepGradeReviewQuery.cs` + `Validator.cs` + `Handler.cs` | query set | The same as P-20…P-22 with `MathStepGradeId`, `IMathStepGradeRepository` and `GradeReviewDetailGenerator.MathSteps`. |
| P-24 | `GradeReviews/ReviewEssayGrade/ReviewEssayGradeCommand.cs` | command | `public sealed record ReviewEssayGradeCommand(Guid SubjectId, Guid EssayGradeId, GradeReviewDecision Decision, decimal? Score, string? Comment) : IRequest<GradeReviewDetailResult>, ISubjectScopedRequest, IGradeReviewInput, IAuditableCommand { public string AuditAction => "EssayGrade.Review"; public string AuditResourceType => "EssayGrade"; public Guid? AuditResourceId => EssayGradeId; }` |
| P-25 | `…/ReviewEssayGrade/ReviewEssayGradeValidator.cs` | validator | Ctor `(IOptions<GradeReviewOptions> gradeReviewOptions)`. `RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);` `RuleFor(x => x.EssayGradeId).ValidateRequired(ErrorCodes.GradeReviewIdRequired);` `Include(new GradeReviewInputValidator(gradeReviewOptions));` |
| P-26 | `…/ReviewEssayGrade/ReviewEssayGradeHandler.cs` | handler | Deps `(IEssayGradeRepository essayGradeRepository, ISessionRepository sessionRepository, IQuestionMasteryRepository questionMasteryRepository, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, IOptions<MasteryOptions> masteryOptions, IGradeReviewNotifier gradeReviewNotifier, TimeProvider timeProvider, ICurrentUserService currentUserService)`. `Handle`: (1) null user → `UnauthorizedCoreException(UserNotAuthenticated)`; (2) `grade = FirstOrDefaultAsync(x => x.Id == request.EssayGradeId && x.SubjectId == request.SubjectId, ct) ?? throw NotFound(GradeReviewNotFound)` (tracked); (3) `now = timeProvider.GetUtcNow()`; (4) `if (request.Decision == GradeReviewDecision.Accepted) grade.Accept(userId, request.Comment, now); else grade.Override(request.Score!.Value, userId, request.Comment, now);`; (5) `await EssayAttemptRecorder.RecordAsync(grade, sessionRepository, questionMasteryRepository, masteryOptions.Value.CorrectThreshold, now, ct)`; (6) `grade.MarkApplied(now)`; (7) `await essayGradeRepository.SaveChangesAsync(ct)` once; (8) `await gradeReviewNotifier.NotifyReviewedAsync(grade.StudentId, grade.SessionId, grade.QuestionId, ct)`; (9) load the snapshot and placement as in P-22 and `return GradeReviewDetailGenerator.Essay(...)`. |
| P-27 | `GradeReviews/ReviewMathStepGrade/ReviewMathStepGradeCommand.cs` + `Validator.cs` + `Handler.cs` | command set | The same as P-24…P-26 with `MathStepGradeId`, audit `"MathStepGrade.Review"` / `"MathStepGrade"`, `IMathStepGradeRepository`, `MathStepAttemptRecorder.RecordAsync` **(#123)** and `GradeReviewDetailGenerator.MathSteps`. |
| P-28 | `api/Elmanhg.Application/Events/TrainingRecords/EssayGradeReviewTrainingRecordHandler.cs` | handler | `INotificationHandler<EssayGradeReviewed>`. Deps are the same as `EssayGradeTrainingRecordHandler`. (1) `grade = notification.Grade`; if `grade.Confidence is null` → return (a GradingFailed review has no AI output to pair; the attempt row carries the teacher score, D12). (2) `session = sessionRepository.GetByIdAsync(grade.SessionId, ct, asNoTracking: true) ?? throw new InvalidOperationException(...)`; test mode → return. (3) Placement as in the existing handler. (4) `AddAsync(EssayGradeTrainingRecord.FromReview(grade, session.Kind, placement, studentIdHasher.Hash(grade.StudentId), timeProvider.GetUtcNow()), ct)`. |

### api/ — Infrastructure / Api
| # | Path | Type | Contract |
|---|------|------|----------|
| I-1 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddGradeReviews.cs` + `.Designer.cs` | migration | See Migration above. |
| A-1 | `api/Elmanhg.Api/Realtime/GradeReviewedMessage.cs` | record | `public sealed record GradeReviewedMessage(Guid SessionId, Guid QuestionId);` |
| A-2 | `api/Elmanhg.Api/Realtime/SignalRGradeReviewNotifier.cs` | notifier | `public sealed class SignalRGradeReviewNotifier(IHubContext<NotificationsHub> hubContext, ILogger<SignalRGradeReviewNotifier> logger) : IGradeReviewNotifier`. `NotifyReviewedAsync` sends `RealtimeEvents.GradeReviewed` with `new GradeReviewedMessage(sessionId, questionId)` to `hubContext.Clients.User(studentId.ToString())`, inside `try` / `catch (Exception exception) when (!cancellationToken.IsCancellationRequested)` → `logger.LogWarning(exception, "Realtime push {EventName} for session {SessionId} failed.", …)`. The WHY comment is copied from `SignalRTeacherThreadNotifier` (the push runs after the commit). |
| A-3 | `api/Elmanhg.Api/Controllers/GradeReviews/GradeReviewsController.cs` | controller | `[ApiController] [Route("api")] [Authorize] public class GradeReviewsController(IMediator mediator) : ControllerBase` with the 6 actions in API surface, each `[Authorize(Policy = DefaultCodes.AiGradesOverride)]` and `[ProducesResponseType<T>(StatusCodes.Status200OK)]`, in the thin style of `SessionsController`. |
| A-4 | `api/Elmanhg.Api/Controllers/GradeReviews/Requests.cs` | request | `public sealed record ReviewGradeRequest(GradeReviewDecision Decision, decimal? Score, string? Comment);` |

### api/ — Repository queries (implement exactly)
`EssayGradeRepository.GetInReviewPageAsync`:
```csharp
var query = _dbSet.AsNoTracking()
    .Where(x => x.SubjectId == subjectId && x.Status == EssayGradeStatus.InReview)
    .Where(x => _context.Set<Session>().Any(s => s.Id == x.SessionId && !s.IsTestMode));
var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
var items = await query.OrderBy(x => x.RequestedAt).ThenBy(x => x.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
return new PageData<EssayGrade> { Items = items, PageNumber = pageNumber, PageSize = pageSize, TotalItems = total, TotalPages = (total + pageSize - 1) / pageSize };
```
`CountInReviewBySubjectAsync`: the same two `Where`s (the subject filter is `subjectIds == null || subjectIds.Contains(x.SubjectId)`), then `.GroupBy(x => x.SubjectId).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct)`. Write each chain one operator per line (skill §1). `MathStepGradeRepository` is identical on `MathStepGradeStatus.InReview`.

### web/ — new feature `web/src/features/gradeReview/`
| # | Path | Type | Contract |
|---|------|------|----------|
| F-1 | `index.ts` | barrel | `export { GradeReviewQueuePage } …; export { GradeReviewDetailPage } …; export { gradeReviewSearchSchema, type GradeReviewSearch } …;` |
| F-2 | `locales.ts`, `i18n/ar.json`, `i18n/en.json` | i18n | `export const gradeReviewLocales = { ar, en };` Keys in "UI copy" below. |
| F-3 | `api/gradeReviewOptions.ts` | consts | `export const gradeReviewKinds = ['Essay', 'MathSteps'] as const; export type GradeReviewKindValue = (typeof gradeReviewKinds)[number];` `// mirrors GradeReview:CommentMaxLength` `export const gradeReviewCommentMaxLength = 2000;` `export const gradeReviewPageSize = 20;` `export const kindSegments = { Essay: 'essay', MathSteps: 'math-steps' } as const;` `export function kindFromSegment(segment: string): GradeReviewKindValue \| null` (`'essay'` → `'Essay'`, `'math-steps'` → `'MathSteps'`, else null). |
| F-4 | `api/toReviewRequest.ts` | util | `toReviewRequest(values: GradeReviewFormValues): ReviewGradeRequest` → `{ decision, score: decision === 'Overridden' ? Number(values.score.trim()) : null, comment: values.comment.trim() === '' ? null : values.comment.trim() }`. |
| F-5 | `api/reviewErrorFields.ts` | map | `export const reviewErrorFields: Record<string, 'score' \| 'comment'> = { GRADE_REVIEW_SCORE_REQUIRED: 'score', GRADE_REVIEW_SCORE_INVALID: 'score', GRADE_REVIEW_SCORE_OUT_OF_RANGE: 'score', GRADE_REVIEW_COMMENT_REQUIRED: 'comment', GRADE_REVIEW_COMMENT_TOO_LONG: 'comment' };` |
| F-6 | `schemas/gradeReviewSearchSchema.ts` | zod | `z.object({ subjectId: z.guid().optional().catch(undefined), kind: z.enum(gradeReviewKinds).catch('Essay'), page: z.coerce.number().int().min(1).optional().catch(undefined) })`. The type is `GradeReviewSearch`. |
| F-7 | `schemas/gradeReviewFormSchema.ts` | zod | `export function gradeReviewFormSchema(maxScore: number)`: `z.object({ decision: z.enum(['Accepted', 'Overridden']), score: z.string(), comment: z.string().max(gradeReviewCommentMaxLength, { error: 'form.commentTooLong' }) }).superRefine(...)`. For Overridden: blank score → `['score']` `form.scoreRequired`; not `/^\d+(\.\d{1,2})?$/` or `Number(score) > maxScore` → `['score']` `form.scoreRange`; blank comment → `['comment']` `form.commentRequired`. `export type GradeReviewFormValues = z.infer<ReturnType<typeof gradeReviewFormSchema>>`. |
| F-8 | `hooks/useGradeReviewSearch.ts` | hook | `getRouteApi('/teacher/grades')`: `{ search, selectSubject(id) → navigate({ search: { subjectId: id, kind: search.kind } }), selectKind(kind) → navigate({ search: prev => ({ ...prev, kind, page: undefined }) }), setPage(page) }`. |
| F-9 | `hooks/useGradeReviewQueue.ts` | hook | `useGradeReviewQueue(subjectId: string \| undefined, kind, page)` = generated `useGetGradeReviewQueue(subjectId ?? '', { kind, pageNumber: page ?? 1, pageSize: gradeReviewPageSize }, { query: { enabled: subjectId !== undefined } })`. |
| F-10 | `hooks/useReviewGrade.ts` | hook | `useReviewGrade(kind, subjectId, gradeId)` wraps the generated `useReviewEssayGrade` / `useReviewMathStepGrade` (chosen by kind). `submit(values, form)` calls `mutateAsync` with `toReviewRequest(values)`. On success it invalidates the queue (`getGetGradeReviewQueueQueryKey(subjectId)` prefix predicate), `getGetGradeReviewSubjectsQueryKey()` and the detail key, then `toast.success(t('detail.saved'))`. On `ApiError`: a code in `reviewErrorFields` → `form.setError(field, { message: t(\`errors.${code}\`, { defaultValue: message }) })` and focus; status 409 → `toast.error(t('detail.conflict'))` and invalidate the detail; else `toast.error(t('detail.saveFailed'))`. It returns `{ submit, isPending }`. |
| F-11 | `components/GradeReviewSubjectPicker.tsx` | component | Props `{ subjects: GradeReviewSubjectResult[]; selectedId: string; onSelect(id) }`. It is a `role="group"` `aria-label={t('queue.subjects')}` of pill `<button type="button" aria-pressed>`s, labelled `t('queue.subjectPill', { name, count: essayCount + mathStepsCount })`. |
| F-12 | `components/GradeReviewKindTabs.tsx` | component | Props `{ kind; essayCount; mathStepsCount; onSelect(kind) }`. It has two `aria-pressed` buttons, `t('queue.kinds.Essay', { count })` and `t('queue.kinds.MathSteps', { count })`. |
| F-13 | `components/GradeReviewListItem.tsx` | component | Props `{ item: GradeReviewItemResult; subjectId: string; now: Date }`. It is an `<li>` in the `ValidationQueueItem` styling. The `Link` goes to `/teacher/grade/$subjectId/$kind/$gradeId` (`kindSegments[item.kind]`), with the text `stemExcerpt(item.stem)` (`@/features/questions`). Meta line: `unit › lesson · t('reasons.<reason>') · t('queue.age', { age: formatPendingAge(item.requestedAt, now, lng) })`. The trailing chip is `t('queue.aiScore', { score, maxScore })` or `t('queue.noAiScore')`, and when there is a confidence, `t('queue.confidence', { percent })` (percent = round(confidence × 100), latin digits via `formatNumber`). |
| F-14 | `components/GradeReviewList.tsx` | component | `<ul aria-label={t('queue.listLabel')} className="flex flex-col gap-2">` of F-13. |
| F-15 | `components/StudentAnswerCard.tsx` | component | Props `{ detail: GradeReviewDetailResult }`. It is a card with `h2 t('detail.answer')`. Essay → `<p dir="auto" className="whitespace-pre-wrap text-ui">{text}</p>` (zod-parse `{ text: string }`, default `''`). MathSteps → `<MathStepsReadOnly solution={{ steps, finalAnswer }}/>` (zod-parse `{ steps: (string\|null)[], finalAnswer: string }`, nulls → `''`). |
| F-16 | `components/AiGradeCard.tsx` | component | Props `{ detail }`. It is a card with `h2 t('detail.ai')`. When `aiScore !== null`: `t('detail.aiScore', { score, maxScore })` and `t('detail.confidence', { percent })`; essay `<EssayCriteriaList criteria/>`; math `<MathStepScoreList steps/>` when there are steps; `t('detail.verdict', { verdict: t(\`verdicts.${v}\`) })` when `finalAnswerVerdict` is present; justification paragraph when non-null. When `aiScore === null`: `t('detail.noAiScore')` plus the reason text `t('reasons.<reason>')`. |
| F-17 | `features/quiz/components/TeacherReviewNote.tsx` | component (quiz feature) | Props `{ review: GradeReviewNoteResult }`. `<div role="note" className="flex flex-col gap-1 rounded-md border border-border bg-soft px-3.5 py-3">` → `<p className="text-ui font-semibold">{t(review.decision === 'Overridden' ? 'teacherReview.overridden' : 'teacherReview.accepted')}</p>`, and when there is a comment, `<p dir="auto" className="text-ui text-text-muted">{t('teacherReview.comment', { comment })}</p>`. Namespace `quiz`. |
| F-18 | `components/GradeReviewForm.tsx` | component | Props `{ detail; onSubmit(values, form) ; isPending }`. RHF with `zodResolver(gradeReviewFormSchema(detail.maxScore))`; defaults `decision: detail.aiScore === null ? 'Overridden' : 'Accepted', score: '', comment: ''`, `mode: 'onBlur'`. It is a `fieldset` of two radios labelled `t('form.accept', { score, maxScore })` (disabled when `aiScore === null`, with hint `t('form.acceptUnavailable')`) and `t('form.override')`. When Overridden: an input `type="text" inputMode="decimal" dir="ltr"` labelled `t('form.score', { maxScore })`. A textarea labelled `t(decision === 'Overridden' ? 'form.commentRequiredLabel' : 'form.commentOptionalLabel')` with hint `t('form.commentHint', { max })`. Errors are shown with `aria-describedby` / `aria-invalid`. Submit is `<Button variant="primary" type="submit" disabled={isPending}>{t('form.submit')}</Button>`. Under 200 lines; split the radio group into `GradeReviewDecisionField.tsx` (F-19) if needed. |
| F-19 | `components/GradeReviewDecisionField.tsx` | component | The radio `fieldset` from F-18 (legend `t('form.decision')`). |
| F-20 | `components/ReviewedCard.tsx` | component | Props `{ detail }` (review non-null). `role="status"` card: `t('detail.reviewed')`, `t('decisions.<decision>')`, `t('detail.finalScore', { score: finalScore, maxScore })`, the comment when present, and `t('detail.reviewedAt', { date })` (`formatDate`). |
| F-21 | `components/GradeReviewEmptyState.tsx` | component | Props `{ messageKey: 'queue.empty.noSubjects' \| 'queue.empty.noItems' }`. The icon `ClipboardCheck` + text, in the style of the ValidationQueuePage empty block. |
| F-22 | `pages/GradeReviewQueuePage.tsx` | page | Uses `useGetGradeReviewSubjects()`, `useGradeReviewSearch()` and `useGradeReviewQueue(subjectId, kind, page)`, where `subjectId = search.subjectId ?? subjects[0]?.subjectId`. Layout: H1 `t('queue.title')`, caption `t('queue.caption')`, then the subject picker (≥ 1 subject), kind tabs and list or state, then `<Pagination>` when `totalPages > 1`. States: loading → `ContentListSkeleton label={t('queue.loading')}`; error on either query → `ContentErrorState` with retry; no subjects → F-21 `noSubjects`; empty list → F-21 `noItems`. `now = new Date()` once per render. |
| F-23 | `pages/GradeReviewDetailPage.tsx` | page | Uses `getRouteApi('/teacher/grade/$subjectId/$kind/$gradeId').useParams()`. With `kind = kindFromSegment(params.kind)`, null → `<NotFound/>`. The query is the generated `useGetEssayGradeReview` or `useGetMathStepGradeReview` (both hooks are called, each with `enabled` by kind, so the hook order stays fixed). Layout: a back `Link` `t('detail.back')` to `/teacher/grades` with `{ subjectId, kind }`; H1 `t('detail.title')` with the reason chip and `unit › lesson`; a `RichTextViewer` stem card (`@/features/content`); `<GradingKeyView type maxScore body gradingSpec/>`; F-15; F-16; then `review ? <ReviewedCard/> : <GradeReviewForm/>`. On success: navigate to `/teacher/grades` with the same `subjectId`/`kind`. States: loading skeleton, 404 `ApiError` → `t('detail.notFound')` with the back link, other error → `ContentErrorState` with retry. |
| F-24 | `web/src/features/questions/components/GradingKeyView.tsx` | component (questions feature) | Props `{ type: string; maxScore: number; body: JsonElement; gradingSpec: JsonElement }`. `values = toQuestionValues({ type, stem: '', explanation: '', difficulty: 'Medium', objectiveId: null, tags: [], maxScore, body, gradingSpec })`. Essay → `<EssayRubricView criteria={values.criteria} modelAnswers={values.modelAnswers}/>`; MathSteps → `<MathAnswerRulesView …/>` (the same props as `ValidationQuestionContent`); other types → `null`. |
| F-25 | `web/src/routes/teacher/grades.tsx` | route | `createFileRoute('/teacher/grades')({ validateSearch: gradeReviewSearchSchema, component: GradeReviewQueuePage })` |
| F-26 | `web/src/routes/teacher/grade.$subjectId.$kind.$gradeId.tsx` | route | `createFileRoute('/teacher/grade/$subjectId/$kind/$gradeId')({ component: GradeReviewDetailPage })` |
| F-27 | `web/src/routes/teacher/more.tsx` | route | `createFileRoute('/teacher/more')({ component: () => <MorePage role="teacher" /> })` |
| F-28 | `web/src/test/gradeReviewFixtures.ts` | fixtures | `reviewSubjectId`, `otherSubjectId`, `gradeReviewSubjects: GradeReviewSubjectResult[]` (Physics 2 essays + 1 math; Chemistry 0/0), `essayQueueItem`, `mathQueueItem` (FinalAnswerUnchecked, `aiScore: null`), `queuePage(items)` → `PageDataOfGradeReviewItemResult` (use the generated model name), `essayReviewDetail` (LowConfidence, `aiScore` 2.5/5, confidence 0.55, one criterion, essay spec with one criterion and one model answer, `answer {text}`), `failedEssayReviewDetail` (GradingFailed, AI fields null), `mathReviewDetail` (FinalAnswerUnchecked, answer `{steps:['2x = 4'], finalAnswer:'x = 2'}`, spec `{acceptedAnswers:['x = 2'], form:'equivalent'}`), `reviewedEssayDetail` (review Overridden, `finalScore` 4). |

### UI copy (`gradeReview` namespace, en / ar)
| Key | en | ar |
|---|---|---|
| `queue.title` | AI grade review | مراجعة تصحيح الذكاء الاصطناعي |
| `queue.caption` | Answers the AI graded with low confidence or could not grade. Accept or change the score to make it final. | إجابات صحّحها الذكاء الاصطناعي بثقة منخفضة أو تعذّر تصحيحها. اعتمد الدرجة أو عدّلها لتصبح نهائية. |
| `queue.subjects` | Subjects | المواد |
| `queue.subjectPill` | {{name}} · {{count}} | {{name}} · {{count}} |
| `queue.kinds.Essay` | Essays ({{count}}) | المقالات ({{count}}) |
| `queue.kinds.MathSteps` | Math steps ({{count}}) | خطوات الرياضيات ({{count}}) |
| `queue.listLabel` | Answers waiting for review | إجابات بانتظار المراجعة |
| `queue.loading` | Loading the review queue… | جارٍ تحميل قائمة المراجعة… |
| `queue.errorTitle` | Could not load the review queue. | تعذّر تحميل قائمة المراجعة. |
| `queue.age` | Waiting {{age}} | بانتظار المراجعة منذ {{age}} |
| `queue.aiScore` | AI: {{score}} / {{maxScore}} | الذكاء الاصطناعي: {{score}} من {{maxScore}} |
| `queue.noAiScore` | No AI score | بدون درجة آلية |
| `queue.confidence` | Confidence {{percent}}% | الثقة {{percent}}٪ |
| `queue.empty.noSubjects` | No subjects are assigned to you. | لا توجد مواد مسندة إليك. |
| `queue.empty.noItems` | No answers are waiting for review. | لا توجد إجابات بانتظار المراجعة. |
| `reasons.LowConfidence` | Low confidence | ثقة منخفضة |
| `reasons.GradingFailed` | AI grading failed | تعذّر التصحيح الآلي |
| `reasons.FinalAnswerUnchecked` | Final answer could not be checked | تعذّر التحقق من الإجابة النهائية |
| `detail.title` | Review answer | مراجعة الإجابة |
| `detail.back` | Back to AI grade review | العودة إلى مراجعة التصحيح |
| `detail.question` | Question | السؤال |
| `detail.answer` | Student's answer | إجابة الطالب |
| `detail.ai` | AI grading | تصحيح الذكاء الاصطناعي |
| `detail.aiScore` | Suggested score: {{score}} of {{maxScore}} | الدرجة المقترحة: {{score}} من {{maxScore}} |
| `detail.confidence` | Confidence: {{percent}}% | الثقة: {{percent}}٪ |
| `detail.verdict` | Final answer: {{verdict}} | الإجابة النهائية: {{verdict}} |
| `detail.noAiScore` | The AI could not give this answer a score. | لم يتمكن الذكاء الاصطناعي من إعطاء درجة لهذه الإجابة. |
| `detail.reviewed` | Reviewed | تمت المراجعة |
| `detail.finalScore` | Final score: {{score}} of {{maxScore}} | الدرجة النهائية: {{score}} من {{maxScore}} |
| `detail.reviewedAt` | Reviewed on {{date}} | رُوجعت في {{date}} |
| `detail.saved` | Decision saved. The score is now final. | حُفظ القرار، وأصبحت الدرجة نهائية. |
| `detail.conflict` | Another teacher just reviewed this answer. | راجع معلّم آخر هذه الإجابة للتو. |
| `detail.saveFailed` | Could not save the decision. | تعذّر حفظ القرار. |
| `detail.notFound` | This answer was not found. | لم يتم العثور على هذه الإجابة. |
| `detail.loading` / `detail.errorTitle` | Loading the answer… / Could not load the answer. | جارٍ تحميل الإجابة… / تعذّر تحميل الإجابة. |
| `decisions.Accepted` / `decisions.Overridden` | AI score accepted / Score changed | اعتُمدت درجة الذكاء الاصطناعي / عُدّلت الدرجة |
| `verdicts.Equivalent` / `NotEquivalent` / `WrongForm` / `Unreadable` / `Unchecked` | Correct / Not correct / Wrong form / Unreadable / Not checked | صحيحة / غير صحيحة / بصورة غير مطلوبة / غير مقروءة / لم يُتحقق منها |
| `form.decision` | Your decision | قرارك |
| `form.accept` | Accept the suggested score ({{score}} of {{maxScore}}) | اعتماد الدرجة المقترحة ({{score}} من {{maxScore}}) |
| `form.acceptUnavailable` | There is no suggested score to accept. | لا توجد درجة مقترحة لاعتمادها. |
| `form.override` | Set the score myself | تحديد الدرجة بنفسي |
| `form.score` | Score (out of {{maxScore}}) | الدرجة (من {{maxScore}}) |
| `form.commentRequiredLabel` / `form.commentOptionalLabel` | Note to the student / Note to the student (optional) | ملاحظة للطالب / ملاحظة للطالب (اختيارية) |
| `form.commentHint` | Up to {{max}} characters. The student sees this note. | حتى {{max}} حرف، ويراها الطالب. |
| `form.submit` | Save decision | حفظ القرار |
| `form.scoreRequired` / `form.scoreRange` / `form.commentRequired` / `form.commentTooLong` | Enter the score. / Enter a number from 0 to the full marks, with up to 2 decimals. / Write a note explaining the score. / The note is too long. | أدخل الدرجة. / أدخل رقمًا من 0 حتى الدرجة الكاملة بمنزلتين عشريتين على الأكثر. / اكتب ملاحظة توضّح الدرجة. / الملاحظة أطول من المسموح. |

Quiz namespace (F-17): `teacherReview.accepted` "Your teacher reviewed this grade and accepted it." / «راجع معلمك هذا التصحيح واعتمده.»; `teacherReview.overridden` "Your teacher reviewed this answer and set its score." / «راجع معلمك هذه الإجابة وحدّد درجتها.»; `teacherReview.comment` "Teacher's note: {{comment}}" / «ملاحظة المعلم: {{comment}}».

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| Domain `GradeNotInReview` | `GRADE_NOT_IN_REVIEW` | `EssayGrade`/`MathStepGrade.EnsureInReview` | `ConflictCoreException` | 409 |
| Domain `GradeReviewNoAiScore` | `GRADE_REVIEW_NO_AI_SCORE` | `Accept` when `Score` is null | `BusinessRuleViolationCoreException` | 400 |
| Domain `GradeReviewScoreOutOfRange` | `GRADE_REVIEW_SCORE_OUT_OF_RANGE` | `Override` when score < 0 or > `MaxScore` | `BusinessRuleViolationCoreException` | 400 |
| `GradeReviewNotFound` | `GRADE_REVIEW_NOT_FOUND` | detail/review handlers (missing, other subject, never in review) | `NotFoundCoreException` | 404 |
| `GradeModifiedConcurrently` | `GRADE_MODIFIED_CONCURRENTLY` | `AppDbContext.SaveChangesAsync` (`xmin` on a grade; the training trigger index) | `ConflictCoreException` | 409 |
| `GradeReviewIdRequired` | `GRADE_REVIEW_ID_REQUIRED` | 4 validators | validation | 422 |
| `GradeReviewKindInvalid` | `GRADE_REVIEW_KIND_INVALID` | queue validator (+ the unreachable switch arm) | validation / `BadRequestCoreException` | 422 |
| `GradeReviewDecisionInvalid` | `GRADE_REVIEW_DECISION_INVALID` | P-5 | validation | 422 |
| `GradeReviewScoreRequired` | `GRADE_REVIEW_SCORE_REQUIRED` | P-5 | validation | 422 |
| `GradeReviewScoreNotAllowed` | `GRADE_REVIEW_SCORE_NOT_ALLOWED` | P-5 | validation | 422 |
| `GradeReviewScoreInvalid` | `GRADE_REVIEW_SCORE_INVALID` | P-5 | validation | 422 |
| `GradeReviewCommentRequired` | `GRADE_REVIEW_COMMENT_REQUIRED` | P-5 | validation | 422 |
| `GradeReviewCommentTooLong` | `GRADE_REVIEW_COMMENT_TOO_LONG` | P-5 | validation | 422 |
| `GradeReviewPageNumberInvalid` | `GRADE_REVIEW_PAGE_NUMBER_INVALID` | queue validator | validation | 422 |
| `GradeReviewPageSizeInvalid` | `GRADE_REVIEW_PAGE_SIZE_INVALID` | queue validator | validation | 422 |
| existing `SubjectIdRequired`, `SubjectOutOfScope`, `QuestionNotFound`, `UserNotAuthenticated` | reused | | | 422 / 403 / 404 / 401 |

Resource strings (Arabic without tashkeel, per skill §7.5):
| Key | ar | en |
|---|---|---|
| GRADE_NOT_IN_REVIEW | هذه الاجابة لم تعد بانتظار المراجعة. | This answer is no longer waiting for review. |
| GRADE_REVIEW_NO_AI_SCORE | لا توجد درجة من الذكاء الاصطناعي لاعتمادها. حدد الدرجة بنفسك. | There is no AI score to accept. Enter the score yourself. |
| GRADE_REVIEW_SCORE_OUT_OF_RANGE | الدرجة يجب ان تكون بين صفر والدرجة الكاملة للسؤال. | The score must be between 0 and the question's full marks. |
| GRADE_REVIEW_NOT_FOUND | لم يتم العثور على هذه الاجابة في قائمة المراجعة. | This answer was not found in the review queue. |
| GRADE_MODIFIED_CONCURRENTLY | تغير هذا التصحيح للتو. حدث الصفحة وحاول مرة اخرى. | This grade just changed. Refresh and try again. |
| GRADE_REVIEW_ID_REQUIRED | معرف التصحيح مطلوب. | The grade id is required. |
| GRADE_REVIEW_KIND_INVALID | نوع المراجعة غير صالح. | The review kind is not valid. |
| GRADE_REVIEW_DECISION_INVALID | القرار غير صالح. | The decision is not valid. |
| GRADE_REVIEW_SCORE_REQUIRED | حدد الدرجة. | Enter the score. |
| GRADE_REVIEW_SCORE_NOT_ALLOWED | لا ترسل درجة عند اعتماد درجة الذكاء الاصطناعي. | Do not send a score when accepting the AI score. |
| GRADE_REVIEW_SCORE_INVALID | الدرجة يجب ان تكون رقما غير سالب بمنزلتين عشريتين على الاكثر. | The score must be a non-negative number with at most two decimals. |
| GRADE_REVIEW_COMMENT_REQUIRED | اكتب ملاحظة للطالب توضح الدرجة. | Write a note to the student explaining the score. |
| GRADE_REVIEW_COMMENT_TOO_LONG | الملاحظة اطول من المسموح. | The note is too long. |
| GRADE_REVIEW_PAGE_NUMBER_INVALID | رقم الصفحة غير صالح. | The page number is not valid. |
| GRADE_REVIEW_PAGE_SIZE_INVALID | حجم الصفحة غير صالح. | The page size is not valid. |

## Domain behaviour
`EssayGrade` (D-3). `MathStepGrade` (D-5) is identical except for the status enum and the missing event.
```csharp
public void Accept(Guid teacherId, string? comment, DateTimeOffset reviewedAt)
{
    EnsureInReview();
    if (Score is null || NormalisedScore is null)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.GradeReviewNoAiScore);
    }

    Resolve(GradeReviewDecision.Accepted, Score.Value, NormalisedScore.Value, teacherId, comment, reviewedAt);
}

public void Override(decimal score, Guid teacherId, string? comment, DateTimeOffset reviewedAt)
{
    EnsureInReview();
    if (score < 0 || score > MaxScore)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.GradeReviewScoreOutOfRange);
    }

    Resolve(GradeReviewDecision.Overridden, score, GradeReviewScore.Normalise(score, MaxScore), teacherId, comment, reviewedAt);
}

private void Resolve(GradeReviewDecision decision, decimal score, decimal normalisedScore, Guid teacherId, string? comment, DateTimeOffset reviewedAt)
{
    var at = ToMicroseconds(reviewedAt);
    ReviewDecision = decision;
    ReviewedScore = score;
    ReviewedNormalisedScore = normalisedScore;
    ReviewComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    ReviewedBy = teacherId;
    ReviewedAt = at;
    GradedAt ??= at;
    Status = EssayGradeStatus.Graded;
    UpdatedBy = teacherId;
    UpdationDate = at;
    RaiseDomainEvent(new EssayGradeReviewed(this));   // EssayGrade only
}

private void EnsureInReview()
{
    if (Status != EssayGradeStatus.InReview)
    {
        throw new ConflictCoreException(ErrorCodes.GradeNotInReview);
    }
}
```
- `ReviewReason` is kept (the history of why the grade was reviewed). `AppliedAt` stays null until the handler calls the existing `MarkApplied` (the grade is now `Graded`, so `IsAwaitingApplication` is true).
- `ToQuestionGrade` uses `Final*` (Existing code touched).
- `EssayGradeTrainingRecord.FromReview(EssayGrade grade, SessionKind sessionKind, QuestionPlacement placement, string studentHash, DateTimeOffset recordedAt)` works as follows:
  - `ArgumentException.ThrowIfNullOrWhiteSpace(studentHash)`.
  - A placement mismatch → `InvalidOperationException`, as in `From`.
  - `grade is not { ReviewDecision: {} decision, ReviewedAt: {} reviewedAt, ReviewedScore: {} reviewedScore, ReviewedNormalisedScore: {} reviewedNormalised, GradedAt: {} gradedAt, Score: {} score, NormalisedScore: {} normalised, Criteria: {} criteria, Justification: {} justification, Confidence: {} confidence, Model: {} model, PromptVersion: {} promptVersion }` → `InvalidOperationException("Only a reviewed AI grade becomes a review training record.")`.
  - Otherwise every `From` field, with `Trigger = TeacherReviewed`, `Outcome = EssayGradeStatus.InReview` (the AI outcome), `OccurredAt = gradedAt` (the AI time, equal to the `Completed` row), plus the review fields.

## API surface
Controller `GradeReviewsController`, class route `api`, every action `[Authorize(Policy = DefaultCodes.AiGradesOverride)]`.
| Method | Route (Name) | Request → message | Response |
|---|---|---|---|
| GET | `grade-reviews/subjects` (`GetGradeReviewSubjects`) | `GetGradeReviewSubjectsQuery` | 200 `List<GradeReviewSubjectResult>` · 401 · 403 (student) |
| GET | `subjects/{subjectId:guid}/grade-reviews` (`GetGradeReviewQueue`) | `[FromQuery] GradeReviewKind kind = Essay, int pageNumber = 1, int pageSize = 20` → `GetGradeReviewQueueQuery` | 200 `PageData<GradeReviewItemResult>` · 403 `SUBJECT_OUT_OF_SCOPE` · 422 |
| GET | `subjects/{subjectId:guid}/grade-reviews/essays/{essayGradeId:guid}` (`GetEssayGradeReview`) | `GetEssayGradeReviewQuery` | 200 `GradeReviewDetailResult` · 404 `GRADE_REVIEW_NOT_FOUND` · 403 |
| POST | `subjects/{subjectId:guid}/grade-reviews/essays/{essayGradeId:guid}` (`ReviewEssayGrade`) | `[FromBody] ReviewGradeRequest` → `ReviewEssayGradeCommand(subjectId, essayGradeId, r.Decision, r.Score, r.Comment)` | 200 `GradeReviewDetailResult` · 400 · 404 · 409 · 422 · 403 |
| GET | `subjects/{subjectId:guid}/grade-reviews/math-steps/{mathStepGradeId:guid}` (`GetMathStepGradeReview`) | `GetMathStepGradeReviewQuery` | as essay |
| POST | `subjects/{subjectId:guid}/grade-reviews/math-steps/{mathStepGradeId:guid}` (`ReviewMathStepGrade`) | `ReviewGradeRequest` → `ReviewMathStepGradeCommand` | as essay |

Changed student responses:
- `GET /api/sessions/{sessionId}/questions/{questionId}/essay-grade` → `EssayGradeResult` gains `review`, and the D8 fields.
- `…/math-step-grade` (#123) → `MathStepGradeResult` gains `review`, and the D8 fields.

Realtime: `gradeReviewed` `{ sessionId, questionId }` is sent to the student's user id.

## Test plan
xUnit v3 + FluentAssertions (pinned) + NSubstitute. Mutation-check every new test: break the line, see it fail, restore it.

### api — Domain
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 1 | `Domain/EssayGrading/EssayGradeReviewTests` (new) | `Accept_LowConfidenceGrade_FinalisesWithAiScoreAndRaisesEvent` | Status Graded; `ReviewedScore == Score` (2.5); decision Accepted; `ReviewedBy`, `ReviewedAt`; `ReviewReason` still LowConfidence; one `EssayGradeReviewed` event |
| 2 | 〃 | `Accept_GradingFailedGrade_ThrowsGradeReviewNoAiScore` | `BusinessRuleViolationCoreException`, code; status still InReview |
| 3 | 〃 | `Accept_PendingGrade_ThrowsGradeNotInReview` | `ConflictCoreException`, code |
| 4 | 〃 | `Override_LowConfidenceGrade_KeepsAiScoreAndStoresTeacherScore` | `Score` 2.5 unchanged; `ReviewedScore` 4; `ReviewedNormalisedScore` 0.8; `FinalScore` 4; comment trimmed |
| 5 | 〃 | `Override_GradingFailedGrade_SetsGradedAtToReviewTime` | `GradedAt == ReviewedAt` |
| 6 | 〃 | `Override_ScoreAboveMax_ThrowsGradeReviewScoreOutOfRange` | code; nothing changed |
| 7 | 〃 | `Override_NegativeScore_ThrowsGradeReviewScoreOutOfRange` | code |
| 8 | 〃 | `Override_AlreadyReviewed_ThrowsGradeNotInReview` | code |
| 9 | 〃 | `Override_BlankComment_StoresNullComment` | `ReviewComment` null |
| 10 | 〃 | `ToQuestionGrade_Overridden_ReturnsTeacherScore` | Score 4, normalised 0.8, outcome Partial, feedback null |
| 11 | 〃 | `GradedBy_Reviewed_IsTeacher` / `GradedBy_Unreviewed_IsAi` | enum values |
| 12 | 〃 | `MarkApplied_AfterReview_StampsAppliedAt` | `AppliedAt` set, `IsAwaitingApplication` false |
| 13 | `Domain/MathStepGrading/MathStepGradeReviewTests` (new) | `Accept_LowConfidenceGrade_FinalisesWithAiScore` | Graded, reviewed = AI score, no domain event |
| 14 | 〃 | `Accept_FinalAnswerUnchecked_ThrowsGradeReviewNoAiScore` | code |
| 15 | 〃 | `Override_FinalAnswerUnchecked_StoresScoreAndGradedAt` | reviewed 2/2 → normalised 1, `GradedAt` set, verdict still null |
| 16 | 〃 | `Override_ScoreAboveMax_ThrowsGradeReviewScoreOutOfRange` | code |
| 17 | 〃 | `Accept_PendingGrade_ThrowsGradeNotInReview` | code |
| 18 | 〃 | `ToQuestionGrade_Overridden_ReturnsTeacherScoreWithoutFeedback` | feedback null |
| 19 | 〃 | `ToQuestionGrade_Accepted_KeepsStoredFeedback` | feedback `MathStepTally` |
| 20 | 〃 | `GradedBy_Reviewed_IsTeacher` | Teacher |
| 21 | `Domain/Questions/Grading/GradeReviewScoreTests` (new) | `Normalise_ThirdOfMaxScore_RoundsToFourDecimals` | 1/3 → 0.3333 |
| 22 | `Domain/TrainingData/EssayGradeTrainingRecordTests` (**modify: add only**) | `From_CompletedGrade_SetsCompletedTrigger` | Trigger Completed, review fields null |
| 23 | 〃 | `FromReview_OverriddenGrade_CopiesAiAndTeacherFields` | Trigger TeacherReviewed; Outcome InReview; AI score 2.5; reviewed 4/0.8; comment; `OccurredAt == GradedAt` |
| 24 | 〃 | `FromReview_UnreviewedGrade_ThrowsInvalidOperationException` | throws |
| 25 | 〃 | `FromReview_GradingFailedReview_ThrowsInvalidOperationException` | throws (no AI fields) |

### api — Application
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 26 | `Application/Features/GradeReviews/ReviewEssayGrade/ReviewEssayGradeHandlerTests` (new; set up like `ApplyEssayGradeHandlerTests`) | `Handle_Accept_RecordsTeacherAttemptStartsMasteryAndNotifies` | attempt `GradedBy Teacher`, score 2.5, `CreatedAt == RequestedAt`; mastery `AddAsync` Received; `AppliedAt` set; `SaveChangesAsync` Received(1); notifier Received(1) with (student, session, question); result `Review.Decision == "Accepted"` |
| 27 | 〃 | `Handle_Override_RecordsAttemptWithTeacherScore` | attempt score 4, normalised 0.8 |
| 28 | 〃 | `Handle_OverrideOnFinishedSession_RecomputesScorePercent` | `session.ScorePercent` includes 4 |
| 29 | 〃 | `Handle_TestModeSession_RecordsAttemptWithoutMastery` | mastery DidNotReceive |
| 30 | 〃 | `Handle_SessionMissing_MarksAppliedWithoutAttempt` | `AppliedAt` set, save Received(1) |
| 31 | 〃 | `Handle_GradeInOtherSubject_ThrowsGradeReviewNotFound` | `NotFoundCoreException`, code; save and notifier DidNotReceive |
| 32 | 〃 | `Handle_NotInReview_ThrowsGradeNotInReview` | `ConflictCoreException`; save DidNotReceive |
| 33 | 〃 | `Handle_AcceptWithoutAiScore_ThrowsGradeReviewNoAiScore` | code; save DidNotReceive |
| 34 | 〃 | `Handle_NoCurrentUser_ThrowsUnauthorized` | `UserNotAuthenticated` |
| 35 | `…/ReviewMathStepGrade/ReviewMathStepGradeHandlerTests` (new) | `Handle_OverrideUncheckedGrade_RecordsTeacherAttemptMasteryAndNotifies` | attempt Teacher 2/2, feedback null, mastery, save Received(1), notifier |
| 36 | 〃 | `Handle_AcceptLowConfidence_RecordsAttemptWithStoredFeedback` | attempt feedback `MathStepTally` |
| 37 | 〃 | `Handle_GradeInOtherSubject_ThrowsGradeReviewNotFound` | code, save DidNotReceive |
| 38 | 〃 | `Handle_NotInReview_ThrowsGradeNotInReview` | code |
| 39 | 〃 | `Handle_NoCurrentUser_ThrowsUnauthorized` | code |
| 40 | `…/Shared/GradeReviewInputValidatorTests` (new; validates `ReviewEssayGradeCommand` through `ReviewEssayGradeValidator`) | `Validate_AcceptWithoutScore_Passes` | valid |
| 41 | 〃 | `Validate_OverrideWithScoreAndComment_Passes` | valid |
| 42 | 〃 | `Validate_UndefinedDecision_ReturnsDecisionInvalid` | `(GradeReviewDecision)9` → code |
| 43 | 〃 | `Validate_OverrideWithoutScore_ReturnsScoreRequired` | code |
| 44 | 〃 | `Validate_AcceptWithScore_ReturnsScoreNotAllowed` | code |
| 45 | 〃 | `Validate_NegativeScore_ReturnsScoreInvalid` | code |
| 46 | 〃 | `Validate_ThreeDecimalScore_ReturnsScoreInvalid` | 1.234 → code |
| 47 | 〃 | `Validate_OverrideWithBlankComment_ReturnsCommentRequired` | `"  "` → code |
| 48 | 〃 | `Validate_CommentOverMax_ReturnsCommentTooLong` | max+1 → code |
| 49 | `…/ReviewEssayGrade/ReviewEssayGradeValidatorTests` (new) | `Validate_EmptySubjectId_ReturnsSubjectIdRequired` / `Validate_EmptyEssayGradeId_ReturnsGradeReviewIdRequired` | codes |
| 50 | `…/ReviewMathStepGrade/ReviewMathStepGradeValidatorTests` (new) | `Validate_EmptySubjectId_ReturnsSubjectIdRequired` / `Validate_EmptyMathStepGradeId_ReturnsGradeReviewIdRequired` / `Validate_OverrideWithoutScore_ReturnsScoreRequired` (the Include is wired) | codes |
| 51 | `…/GetGradeReviewQueue/GetGradeReviewQueueHandlerTests` (new) | `Handle_Essays_ReturnsItemsWithPlacementAndAiScore` | stem, unit/lesson names, reason `LowConfidence`, aiScore, confidence, paging copied |
| 52 | 〃 | `Handle_MathSteps_ReturnsItemsWithReason` | kind MathSteps, reason `FinalAnswerUnchecked`, aiScore null |
| 53 | 〃 | `Handle_MissingLesson_ReturnsEmptyNames` | `""` names |
| 54 | `…/GetGradeReviewQueue/GetGradeReviewQueueValidatorTests` (new) | `Validate_Valid_Passes`, `Validate_EmptySubjectId_ReturnsSubjectIdRequired`, `Validate_PageZero_ReturnsPageNumberInvalid`, `Validate_PageSizeOverMax_ReturnsPageSizeInvalid`, `Validate_UndefinedKind_ReturnsKindInvalid` | codes |
| 55 | `…/GetGradeReviewSubjects/GetGradeReviewSubjectsHandlerTests` (new) | `Handle_Teacher_ReturnsAssignedSubjectsWithCountsInOrder` | includes the 0-count subject; ordered by `Order`; counts per kind |
| 56 | 〃 | `Handle_Admin_ReturnsEverySubject` | the count repos receive `null` subject ids |
| 57 | 〃 | `Handle_NoCurrentUser_ThrowsUnauthorized` | code |
| 58 | `…/GetEssayGradeReview/GetEssayGradeReviewHandlerTests` (new) | `Handle_InReview_ReturnsDetailFromServedRevision` | stem/spec from revision v1 (question now at v2); answer text; criteria; review null; finalScore null |
| 59 | 〃 | `Handle_Reviewed_ReturnsReviewNoteAndFinalScore` | review Overridden; finalScore 4; AI criteria still present (teacher view) |
| 60 | 〃 | `Handle_GradeNeverInReview_ThrowsGradeReviewNotFound` | code |
| 61 | 〃 | `Handle_RevisionMissing_ThrowsQuestionNotFound` | code |
| 62 | `…/GetMathStepGradeReview/GetMathStepGradeReviewHandlerTests` (new) | `Handle_UncheckedGrade_ReturnsAnswerWithoutAiFields` | steps `[]`, verdict null, aiScore null |
| 63 | 〃 | `Handle_GradeInOtherSubject_ThrowsGradeReviewNotFound` | code |
| 64 | `…/GetEssayGradeReview/GetEssayGradeReviewValidatorTests` + `…/GetMathStepGradeReview/GetMathStepGradeReviewValidatorTests` (new) | `Validate_Valid_Passes`, `Validate_EmptySubjectId_ReturnsSubjectIdRequired`, `Validate_EmptyId_ReturnsGradeReviewIdRequired` (each class) | codes |
| 65 | `Application/Features/EssayGrading/GetEssayGrade/GetEssayGradeHandlerTests` (**modify: add only**) | `Handle_Overridden_ReturnsTeacherScoreWithoutAiDetail` | score 4, outcome Partial, criteria empty, justification null, review Overridden with comment |
| 66 | 〃 | `Handle_Accepted_ReturnsAiDetailWithReviewNote` | criteria present, review Accepted |
| 67 | `Application/Features/MathStepGrading/GetMathStepGrade/GetMathStepGradeHandlerTests` **(#123)** (**modify: add only**) | `Handle_OverriddenUncheckedGrade_ReturnsTeacherScoreAndNullVerdict` | score, `FinalAnswerVerdict` null, steps empty, review |
| 68 | `Application/Features/Events/EssayGradeReviewTrainingRecordHandlerTests` (new) | `Handle_ReviewedAiGrade_AddsTeacherReviewedRecord` | `AddAsync` with Trigger TeacherReviewed and hash |
| 69 | 〃 | `Handle_GradingFailedReview_AddsNothing` | DidNotReceive |
| 70 | 〃 | `Handle_TestModeSession_AddsNothing` | DidNotReceive |
| 71 | `Application/Features/TrainingExports/Shared/TrainingExportLineGeneratorTests` (**modify: add only**) | `EssayGrade_ReviewedRecord_CarriesReviewFieldsAndScrubsComment` | Trigger, decision, reviewed score; `"call 01112345678"` → `[number]` |

### api — Integration (Testcontainers)
New helper `api/Elmanhg.Tests/Integration/GradeReviews/GradeReviewTestData.cs`:
- `SeedInReviewEssayAsync(ApiFactory factory, Guid subjectId, bool gradingFailed = false, bool isTestMode = false)` seeds a unit, a published lesson and an approved essay question (as `EssayGradingTestData.SeedApprovedEssayAsync`), plus a student (`ScopeTestData.SeedStudentAsync`). It creates `Session.StartQuiz(student.Id, lesson, [essay], isTestMode)`, an `EssayGrade.Request(...)` for that item with `maxScore = item.MaxScore`, then `Complete(EssayGradeBuilder.Assessment(0.5m), new QuestionGrade(…), 0.7m, now)`, or `FailAttempt(…, maxAttempts: 1, …)`. It saves and returns `(Guid GradeId, Guid SessionId, Guid QuestionId, User Student)`.
- `SeedInReviewMathStepAsync(...)` does the same with a MathSteps question (`QuestionBuilder.MathStepsContent()`), `MathStepGrade.Request(…, finalAnswerVerdict: null, …)` and `FailAttempt(…, 1, …)` → `FinalAnswerUnchecked`.
- `ReviewClientAsync(ApiFactory factory, Guid subjectId)` = `TeacherInboxTestData.SignedInTeacherForAsync`.

| # | Test class | Test method | Asserts |
|---|---|---|---|
| 72 | `Integration/GradeReviews/GradeReviewQueueEndpointTests` | `GetSubjects_Teacher_ReturnsAssignedSubjectCounts` | 200; the assigned subject with essayCount 1, mathStepsCount 1; an unassigned subject absent |
| 73 | 〃 | `GetQueue_Essays_ListsInReviewGradesOldestFirstExcludingTestMode` | two in review (older first); the test-mode one and a Graded one absent; totalItems 2 |
| 74 | 〃 | `GetQueue_MathSteps_ListsUncheckedGrade` | reason `FinalAnswerUnchecked`, aiScore null |
| 75 | 〃 | `GetQueue_UnassignedTeacher_Returns403SubjectOutOfScope` | 403, code |
| 76 | 〃 | `GetQueue_Student_Returns403` | 403 |
| 77 | 〃 | `GetQueue_Anonymous_Returns401` | 401 |
| 78 | 〃 | `GetEssayReview_GradeFromOtherSubjectInRoute_Returns404` | 404 `GRADE_REVIEW_NOT_FOUND` (the teacher is assigned to both subjects) |
| 79 | `Integration/GradeReviews/ReviewEssayGradeEndpointTests` | `Post_Override_FinalisesGradeAndWritesAttemptMasteryAndTrainingRows` | 200 detail with review; DB: grade Graded, `AppliedAt` set, `ReviewedScore` 4; attempt `GradedBy Teacher` score 4; `QuestionMasteries` row; `EssayGradeTrainingRecords` has Completed + TeacherReviewed; `AttemptTrainingRecords` has one row with `GradedBy Teacher`; `AuditLogs` row `EssayGrade.Review` Success |
| 80 | 〃 | `Post_Accept_StudentGradeReadsGradedWithReviewNote` | the student `GET …/essay-grade` → `Graded`, score 2.5, `review.decision` Accepted |
| 81 | 〃 | `Post_Twice_Returns409GradeNotInReview` | the second 409 with the code; one attempt in DB |
| 82 | 〃 | `Post_OverrideWithoutComment_Returns422CommentRequired` | 422 code |
| 83 | 〃 | `Post_ScoreAboveMax_Returns400ScoreOutOfRange` | 400 code; grade still InReview |
| 84 | 〃 | `Post_AcceptGradingFailed_Returns400NoAiScore` | 400 code |
| 85 | 〃 | `Post_Admin_Succeeds` | 200 (admin not assigned) |
| 86 | 〃 | `Post_UnassignedTeacher_Returns403` | 403 `SUBJECT_OUT_OF_SCOPE`; audit row Failure |
| 87 | `Integration/GradeReviews/ReviewMathStepGradeEndpointTests` | `Post_OverrideUnchecked_WritesTeacherAttemptAndStudentSeesGraded` | 200; attempt Teacher; the student `GET …/math-step-grade` → Graded, verdict null, review |
| 88 | 〃 | `Post_GradeFromOtherSubject_Returns404` | 404 |
| 89 | `Integration/Persistence/GradeConcurrencyTests` (new) | `SaveChanges_StaleEssayGrade_ThrowsGradeModifiedConcurrently` | two scopes load one InReview grade; the first `Override` + save succeeds; the second save → `ConflictCoreException` `GRADE_MODIFIED_CONCURRENTLY` |
| 90 | `Integration/TrainingData/EssayGradeTrainingExportPageTests` (new) | `GetExportPageAsync_ReviewedGrade_ReturnsOnlyTeacherReviewedRow` | one reviewed and one unreviewed grade in range → 2 rows: the TeacherReviewed and the other's Completed |
| 91 | `Integration/Realtime/NotificationsHubTests` (**modify: add only**) | `Hub_StudentConnected_ReceivesGradeReviewed` | the teacher POSTs the override; the student connection gets `gradeReviewed` with sessionId/questionId |
| 92 | `Integration/Persistence/AppDbContextTests` (**modify**, accepted pattern) | existing `Migrate_FreshDatabase_LeavesNoPendingMigrations` | the list includes `_AddGradeReviews` |

### web (Vitest + Testing Library + MSW; Orval-generated MSW handlers)
| # | Test file | `it(...)` | Asserts |
|---|---|---|---|
| 93 | `features/gradeReview/schemas/gradeReviewSearchSchema.test.ts` | defaults the kind to Essay when missing or invalid · drops an invalid subject id · coerces the page | parsed values |
| 94 | `features/gradeReview/schemas/gradeReviewFormSchema.test.ts` | accepts an accept decision without a score · requires a score to override · rejects a score above the full marks · rejects three decimals · requires a comment to override · rejects a comment over the limit | issue path + key |
| 95 | `features/gradeReview/api/toReviewRequest.test.ts` | sends a null score and a trimmed comment for accept · sends a number and the comment for override · sends a null comment when blank | object |
| 96 | `features/gradeReview/api/gradeReviewOptions.test.ts` | maps route segments to kinds and back · returns null for an unknown segment | values |
| 97 | `features/gradeReview/pages/GradeReviewQueuePage.test.tsx` | shows the first subject's essays after loading (skeleton `aria-busy` first) · shows math items when the math tab is chosen · switches subject from the pills · shows the empty state when nothing waits · shows the no-subjects state · shows retry on error and recovers · renders right to left in Arabic · has no axe violations | text/roles/URL search |
| 98 | `features/gradeReview/pages/GradeReviewDetailPage.test.tsx` | shows the question, the student's essay, the suggested score and the criteria · accepts the suggested score, then toasts and returns to the queue (MSW asserts the body `{decision:'Accepted',score:null,comment:null}`) · shows inline errors when overriding without score or note · sends the teacher score and note on override · disables accept when there is no AI score (failed essay) · shows the math steps and final answer for an unchecked math answer · shows the reviewed card and no form for a reviewed answer · shows a conflict toast on 409 · shows a server field error under the score (422 `GRADE_REVIEW_SCORE_INVALID`) · shows not found for an unknown answer · has no axe violations | DOM + request bodies |
| 99 | `features/quiz/components/EssayGradeStatus.test.tsx` (**modify: add only**) | shows the teacher note and comment without AI criteria when overridden · shows the criteria and the accepted note when accepted | text present/absent |
| 100 | `features/quiz/components/MathStepGradeStatus.test.tsx` **(#123)** (**modify: add only**) | shows the teacher score and note without a verdict line when an unchecked answer was overridden | text |
| 101 | `features/askTeacher/components/StudentRealtimeListener.test.tsx` (**modify: add only**) | refreshes the essay grade and shows a toast when a grade is reviewed · ignores a malformed grade-reviewed event | the toast text; the essay status turns from «Under review» to the graded score after emit (MSW handler switches) |
| 102 | `features/shell/navConfig.test.ts` (**modify**) | existing `hides a destination whose capability the role lacks` | expected `['queue','gradeReviews','inbox','stats']`, `overflowItems` → `['stats']` |
| 103 | `features/shell/components/AppShell.test.tsx` (**modify**) | rename `shows the three teacher tabs without More` → `shows three teacher tabs and More`: `['Review queue','AI grades','Student questions','More']` · `shows only teacher destinations in the top tabs` → `['Review queue','AI grades','Student questions','My stats']` | link names |

## Docs to update (docs-sync: behaviour and scope change)
| Doc | Change |
|---|---|
| `docs/PRD.md` §8.3 | Replace the bullets with these points:
- The queue lists, per assigned subject, essay grades and math step grades in review: low confidence, grading failed, or final answer unchecked after retries. Test-mode sessions are excluded, and grades are shown oldest first.
- The teacher sees the question (served version), the grading key, the answer, and the AI score, confidence and reasons. No student identity is shown.
- Accept is possible only when the AI produced a score. Override takes a score from 0 to full marks (2 decimals) and a required note.
- The decision is final. It writes the attempt (`graded_by = Teacher`), mastery and the session score, and the student sees the note.
- Legacy `mathUnchecked` attempts from before #123 are not reviewed (no live deploy).
- Details: `docs/grade-review.md`. |
| `docs/PRD.md` §13 | "teacher overrides arrive with E17" → "a teacher review adds a `TeacherReviewed` row (#128)". Row "AI grading (v2)" is unchanged. |
| `docs/PRD.md` §15 | `EssayGrade`/`MathStepGrade` lines (if present) and `EssayGradeTrainingRecord(… trigger[Completed\|TeacherReviewed], review_decision?, reviewed_score?, reviewed_normalised_score?, review_comment?, reviewed_at? …)`. |
| `docs/grade-review.md` (new) | The contract doc:
- Overview.
- What is listed (D1, D14).
- Decisions (D4–D8).
- Model columns.
- API table with error codes.
- Concurrency (D11).
- Training data (D12).
- Realtime (D16).
- Options table (`GradeReview:CommentMaxLength` 2000, `GradeReview:QueueMaxPageSize` 50).
- Web routes and states. |
| `docs/essay-grading.md` | Model table: add the six review columns. Lifecycle: "`InReview` grades are not applied; #128 writes their attempt" → points to the review (D2). Student API: `review` and the D8 fields. Replace the "What #128 adds" section with a link to `grade-review.md`. |
| `docs/math-step-grading.md` **(#123)** | Add a "Teacher review (#128)" section: accept/override rules, the D8 student fields, `GradedBy = Teacher`, and a link. |
| `docs/math-cas.md` | Remove any text saying #128 resolves legacy `mathUnchecked` attempts; state D3. |
| `docs/training-data.md` | EssayGradeTrainingRecords: `Trigger` and review columns; unique `(EssayGradeId, Trigger)`; the "Why both" and "No double counting" paragraphs updated (D12). The trigger table gets a row: teacher review (`EssayGrade.Accept/Override`) → `EssayGradeReviewed` → `EssayGradeReviewTrainingRecordHandler` → `TeacherReviewed` row (AI grades only). Attempts: reviewed grades have `GradedBy = Teacher`. Export line shape for EssayGrades: the six new fields; one line per essay grade. |
| `docs/audit-log.md` | Audited commands: `ReviewEssayGrade` \| `EssayGrade.Review` \| EssayGrade \| command (the diff shows the status and review fields); `ReviewMathStepGrade` \| `MathStepGrade.Review` \| MathStepGrade \| command. |
| `docs/sessions.md` | Where `GradedBy` is described: `Teacher` = a grade finalised by a teacher review (#128). |
| `docs/claude-design-prompt.md` §4 | Teacher: add a `#/teacher/grades` / `#/teacher/grade/:subjectId/:kind/:id` bullet with the content, flow and states above. The teacher nav is 3 tabs (review queue, AI grades, student questions) + «المزيد» (stats). Student quiz/exam: a teacher-reviewed grade shows the note «راجع معلمك…» and the teacher's comment; overridden grades hide the AI reasons. |
| `docs/prototype.md` | The prototype does not simulate the AI grade review queue (#128). |
| `docs/backlog.json` E17 | description: "AI essay grades and MathSteps step grades that land in review (low confidence, grading failed, or a final answer the CAS could not check after retries) are accepted or overridden by teachers of the subject; the decision applies the final score and is captured as training data. PRD §8.3."; story description: "As a teacher I see AI grades waiting for review in my subjects and can accept or override them."; tasks: `["Queue query scoped to assigned subjects (routing uses the EssayGrading and MathStepGrading confidence thresholds)", "Accept and override commands that apply the final score and notify the student", "Training record for every review", "Teacher review queue UI"]`. |

## Definition of done
- [ ] #123 is merged into the branch before any code; `MathStepGrade` edits are additive partials plus the listed lines.
- [ ] Every file in "Files to create" exists with the stated namespace, type and signatures; no other new files.
- [ ] `EssayGrade`/`MathStepGrade` `Accept`/`Override` match Domain behaviour: guards, `GradedAt ??=`, `UpdationDate`, `UpdatedBy`; the AI `Score` is never overwritten.
- [ ] A review writes the attempt, mastery (not test mode), `ScorePercent` and `AppliedAt` in **one** `SaveChangesAsync`; no attempt row is ever updated.
- [ ] Attempts from reviewed grades have `GradedBy = Teacher`; unreviewed AI grades still have `AI`.
- [ ] Every endpoint has `[Authorize(Policy = DefaultCodes.AiGradesOverride)]`; queue, detail and review requests implement `ISubjectScopedRequest`; a grade id from another subject returns 404.
- [ ] The queue excludes test-mode sessions and is ordered by `RequestedAt`, then `Id`; the summary includes zero-count subjects; Admin sees every subject.
- [ ] No student identity appears in any grade-review result.
- [ ] Two decisions on one grade → 409 (`GRADE_NOT_IN_REVIEW` or `GRADE_MODIFIED_CONCURRENTLY`); the `xmin` mapping and the trigger-index mapping are in `AppDbContext.SaveChangesAsync`.
- [ ] An `EssayGradeTrainingRecords` `TeacherReviewed` row is written for reviewed AI essay grades (not GradingFailed, not test mode); the export returns one line per essay grade; `AttemptTrainingRecords` is unchanged in schema.
- [ ] Migration `AddGradeReviews` has only the listed operations; `Trigger` defaults to `'Completed'` for existing rows; the append-only triggers still pass their tests.
- [ ] The student essay/math grade results expose `review` and D8 hiding; the web shows `TeacherReviewNote`; the realtime `gradeReviewed` refreshes and toasts.
- [ ] Both commands are `IAuditableCommand`; both grades are `IAuditedEntity`; the integration test sees the audit row.
- [ ] `GradeReviewOptions` is bound with `ValidateOnStart`, has code defaults, and is in `appsettings.example.json`.
- [ ] Every new error code is in both resx files, with the listed text.
- [ ] Teacher nav is 3 tabs + «المزيد»; `/teacher/more` exists; the queue and detail pages have loading, empty, error-with-retry and not-found states; every string is in en + ar; axe passes.
- [ ] Every test in the Test plan exists with that name, and no other existing test was edited; each new test was mutation-checked.
- [ ] `dotnet build` has zero new warnings; `dotnet test api/ -c Release` is green with `appsettings.json` moved aside (CI parity); `dotnet format --verify-no-changes` is clean outside `core-libraries`.
- [ ] `api/openapi/v1.json`, `web/src/shared/api/generated`, `web/src/routeTree.gen.ts` and the Postman folder are regenerated or updated.
- [ ] Web: `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`, and `npx vitest run --coverage` all exit 0; the §14/§16 greps are clean.
- [ ] The docs in "Docs to update" agree with the code (docs-sync rule).
