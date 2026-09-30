# Plan — [E14.S3] Student essay input (#119)

## Goal
A student can answer essay questions in a lesson quiz and in unit and multi-unit exams, on a phone. They write in an RTL plain-text editor that shows live word and character limits. Quiz drafts autosave to the device; exam drafts already autosave to the server. After submitting, the student sees «جارٍ تصحيح إجابتك…», then «قيد المراجعة» or the verdict with per-criterion marks and the justification, on the quiz card and on the quiz and exam result pages. The score updates when the grade lands. On the backend, essays become servable. A written essay creates an `EssayGrade` instead of an attempt. A blank essay scores 0 (Unanswered) with no AI call. The grading worker writes the AI `Attempt` and the mastery update, and re-scores a finished session, when a grade becomes `Graded`.

## Scope
**In**
- Sub-task 1, the rich Arabic editor with autosave. This is an upgraded shared `EssayAnswerInput`: RTL (`dir="auto"`), 8 rows, word count against `maxWords`, an over-limit state, a characters-left line near 20 000 and a `maxLength` cap. It adds a local draft store and hook for quizzes. Exams keep their existing server autosave.
- Sub-task 2, the result view with per-criterion feedback. `EssayGradeStatus` (from #118) is mounted in the quiz card, the quiz result and the exam result, with `onGraded` refreshing the session score.
- API work:
  - essays become servable;
  - quiz essay submission (`SessionItem.SavedAnswer` holds the pending essay);
  - exam essay submission;
  - blank essay → Unanswered attempt;
  - Graded → `Attempt` (`GradedBy = AI`) + mastery + `ScorePercent` recompute;
  - `EssayGrade.TimeTakenMilliseconds` + migration;
  - `SessionItemResult.pendingAnswer`;
  - raw answer cap raised so 20 000-character essays fit.
- Blueprints accept an Essay count (API `ServedTypes` + web `servedQuestionTypes`).
- Docs, OpenAPI and the Orval regen.

**Out**
- Teacher review of `InReview` grades and teacher override (#128). The attempt for an `InReview` grade is written by #128.
- Push or SignalR for grade completion. Polling every 2 s is kept (#118 decision, PRD §18).
- Asking the avatar or a teacher about an essay.
- A formatting toolbar.

**Deferred:** none. The real Claude grading adapter already exists (#118).

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | "Rich" editor: formatting toolbar (TipTap) or plain text? | Plain-text `<textarea>`: `dir="auto"`, paragraphs kept (`whitespace-pre-wrap` on display), spellcheck on, no toolbar. PRD §6 cell "Rich text" is changed to "Plain text (Arabic, multi-paragraph)". | #117 fixed the answer schema as `{ "text": string }`, and the grader and justification are plain text. TipTap would break the quiz bundle budget (255 KB). |
| 2 | Where a submitted-but-ungraded quiz essay lives | `SessionItem.SavedAnswer` (the existing column, until now exam-only) + `AnswerSavedAt`. No attempt until the grade is final. | `Attempts` are append-only (DB trigger), so the score cannot be filled in later. No new table. |
| 3 | How "answered" works for a pending quiz essay | `Session.FindPendingEssayAnswer(item)` = quiz ∧ `SavedAnswer` ≠ null ∧ no attempt. `CurrentPosition` skips such items. `RecordAttempt` on such an item → 409 `SESSION_QUESTION_ALREADY_ANSWERED`. | Resume must not reopen a submitted essay, and a second answer must not race the grade. |
| 4 | When and how the essay `Attempt` is written | In `GradeEssayHandler`, the **same save** as `grade.Complete`, only when the status becomes `Graded`, via `EssayAttemptRecorder`. `GradedBy = AI`, `CreatedAt = grade.RequestedAt`, `TimeTakenMilliseconds = grade.TimeTakenMilliseconds`, `Grade` (feedback) null. Mastery is updated outside test mode. A submitted session gets `ScorePercent` recomputed. | This is atomic. `CreatedAt` = the answer time keeps the day-quota day and the mastery order right (`QuestionMastery.Record` orders by time). The recorder is a static helper that #128 reuses. |
| 5 | Race with a concurrent finish or answer on the same session | `RecordEssayAttempt` always sets `Session.UpdationDate`, so the `xmin` check applies. A lost race fails the worker save. The worker's `FailEssayGradeCommand` retries after backoff (`GradingFailed` after `MaxAttempts`). | Without the stamp, a finish could compute `ScorePercent` without the new attempt. Conflicts need a save in the same few milliseconds; a retry costs one AI call. |
| 6 | Session gone (soft-deleted) or essay not an item of the session | The recorder writes nothing. The grade is still completed. | Existing #118 test data grades an essay that is not a session item. A deleted session must not gain attempts. |
| 7 | Time taken for a quiz essay | New column `EssayGrades.TimeTakenMilliseconds int NOT NULL DEFAULT 0`, set from `Session.SubmitEssay`'s measurement. Exams store 0. | `LastActivityAt` is moved at submission, so the writing time would otherwise be lost from the quiz time. |
| 8 | Blank essays | `QuestionGrader.Grade` supports `Essay` only for a blank (whitespace) text → `NormalisedGrade.Unanswered`. A non-blank or null text throws `InvalidOperationException`. Quiz and exam blank essays go through the normal grading path → Auto attempt with score 0. | #118 contract ("#119 grades blank essays directly as Unanswered"). The existing exam and quiz code paths need no special case. |
| 9 | Essay length limit on student routes | `Sessions:AnswerMaxLength` default 4000 → **45000** (the raw JSON guard). The handlers (`SubmitAnswer`, `SaveExamAnswer`) reject essay text longer than `Content:QuestionEssayAnswerMaxLength` (20 000, untrimmed, same as grade-draft) with 422 `QUESTION_ESSAY_ANSWER_TOO_LONG`. The web caps the textarea at 20 000 (`maxLength`). | The validators cannot know the type. 45 000 fits a 20 000-character essay even if every character is JSON-escaped to two. The existing validator test uses an explicit limit of 20, so it stays valid. |
| 10 | Word limit (`maxWords`) enforcement | Client only. A quiz submit is blocked with `session.essayOverLimit`. In an exam, the editor shows the over-limit state but saves and submits anyway. The server does not count words. | `maxWords` is documented as "the word limit shown to the student". An exam is submitted as a whole and autosave must keep the text. |
| 11 | Quiz essay draft storage | `localStorage` key `elmanhg.essayDraft.<studentId>.<sessionId>.<questionId>`, `{savedAt, answer: string}`, TTL 7 days, save 800 ms after the last change, flush on `visibilitychange: hidden`, `pagehide` and unmount. Removed on a successful submit. | Mirrors `mathDraftStore` / `useMathStepsDraft` (E15.S1). Quizzes have no server draft. |
| 12 | Duplicate storage code with mathSteps | Extract the storage primitives to `web/src/shared/lib/localDraft.ts`. `mathDraftStore.ts` delegates to it; its exports, behaviour and tests are unchanged. The two hooks stay separate: the math hook is coupled to its step value and focus. | React guide §6.7: extract on the second use. |
| 13 | Showing grade completion | Polling as in #118 (`useEssayGrade`, 2 s while `Pending`). New `onGraded` callback fires once on the `Pending → Graded` transition. Pages use it to invalidate the session (quiz) or exam session plus mastery or exam views, so `scorePercent` and `attempt` refresh. | There is no push for grades (PRD §18). Result pages use `staleTime: Infinity`, so without this they would stay stale. |
| 14 | UI for a submitted essay (quiz card, quiz result, exam result) | The stem, the read-only essay text (`EssayAnswerText`), then `EssayGradeStatus`, then the explanation. No `FeedbackPanel`, avatar or «اسأل معلّم» for written essays. A blank essay attempt uses the normal review (`QuizReviewItem` or `FeedbackPanel`, "unanswered" feedback). | `EssayGradeStatus` already has the verdict, score, criteria and justification. Disputing an AI grade is #128. |
| 15 | Result summary while essays are pending | «أجبت عن» counts written essays. A note `result.essaysPending` appears while any written essay has no attempt (Pending or InReview). | The score is provisional until the grade is final (PRD §6.1). |
| 16 | Free-tier daily quota | Unchanged. A pending essay counts once its attempt exists, on the day it was submitted (`CreatedAt = RequestedAt`). A new essay submission passes the lesson and quota gate like any first answer. | This is the existing soft limit (`docs/sessions.md`). |
| 17 | Correct answer and explanation reveal for a pending quiz essay | Revealed as soon as the essay is submitted (`reveal = attempt ∨ pending ∨ submitted`). The web shows only the explanation (`describeCorrectAnswer` returns null for Essay). | This matches "revealed after answering" for every other type. |
| 18 | Test mode (admin) essays | Graded by the AI. The attempt is written. No mastery. | Same as every test-mode attempt. |
| 19 | Postman | No change. | No route or request body changes; only the response field `pendingAnswer` is added. |
| 20 | Morabh reuse | None. New code, no Morabh equivalent: essay submission, the attempt recorder, the local draft store and the editor. Morabh has no assessment, autosave or essay code (checked `D:\Personal\Projects\Projects\Morabh\repos\apis`). | Morabh reuse-first rule (dotnet-feature skill, Elmanhg deltas §5). |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/ServableQuestionSpecification.cs` | Remove `&& x.Type != QuestionType.Essay` and its `#119` comment. `ServedTypes` = `[Mcq, Multi, TrueFalse, Fill, Short, Essay]`. |
| `api/Elmanhg.Domain/Questions/Grading/QuestionGrader.cs` | Add switch arm `QuestionType.Essay => EssayGrader.GradeBlank(ReadAnswer<EssayAnswer>(answer)),`. |
| `api/Elmanhg.Domain/Questions/Grading/EssayGrader.cs` | Add `public static NormalisedGrade GradeBlank(EssayAnswer answer)`. See Domain behaviour. |
| `api/Elmanhg.Domain/EssayGrading/EssayGrade.cs` | Add `public int TimeTakenMilliseconds { get; private set; }`. `Request(...)` gains a last parameter `int timeTakenMilliseconds`. Add `ToQuestionGrade()`. |
| `api/Elmanhg.Domain/Sessions/Attempt.cs` | `Create(...)` gains a last parameter `AttemptGrader gradedBy`, and `GradedBy = gradedBy`. |
| `api/Elmanhg.Domain/Sessions/Session.cs` | `CurrentPosition` also skips items where `FindPendingEssayAnswer(x) is not null`. |
| `api/Elmanhg.Domain/Sessions/Session.Answering.cs` | `RecordAttempt`: pass `AttemptGrader.Auto`. After the existing-attempt block, `if (FindPendingEssayAnswer(item) is not null) throw new ConflictCoreException(ErrorCodes.SessionQuestionAlreadyAnswered);`. |
| `api/Elmanhg.Domain/Sessions/Session.ExamSubmission.cs` | New overload `SubmitExam(IReadOnlyDictionary<Guid, QuestionGrade> grades, IReadOnlySet<Guid> essayQuestionIds, DateTimeOffset now)`. The old two-argument overload delegates with an empty set. `answered` excludes `essayQuestionIds`. `Attempt.Create(..., AttemptGrader.Auto)`. |
| `api/Elmanhg.Application/Questions/Shared/QuestionAnswerRules.cs` | Add `TryReadWrittenEssay` and `IsEssayTooLong`. |
| `api/Elmanhg.Application/Sessions/Shared/SessionItemResult.cs` | Append a positional `JsonElement? PendingAnswer`. |
| `api/Elmanhg.Application/Sessions/Shared/SessionResultGenerator.cs` | Set `PendingAnswer`; `reveal` includes a pending answer. |
| `api/Elmanhg.Application/Sessions/SubmitAnswer/SubmitAnswerHandler.cs` | Add constructor dependencies `IEssayGradeRepository essayGradeRepository` and `IOptions<ContentOptions> contentOptions`. Change the free-tier condition. Add the essay branch. |
| `api/Elmanhg.Application/Exams/SaveExamAnswer/SaveExamAnswerHandler.cs` | Add constructor dependency `IOptions<ContentOptions> contentOptions`. Add the essay length check. |
| `api/Elmanhg.Application/Exams/Shared/ExamSubmission.cs` | New signature and essay handling. |
| `api/Elmanhg.Application/Exams/SubmitExam/SubmitExamHandler.cs` | Add constructor dependency `IEssayGradeRepository essayGradeRepository`. Pass `questionRepository` and `essayGradeRepository` to `ExamSubmission.SubmitAsync`. |
| `api/Elmanhg.Application/Exams/AutoSubmitExam/AutoSubmitExamHandler.cs` | Same as `SubmitExamHandler`. |
| `api/Elmanhg.Application/EssayGrading/GradeEssay/GradeEssayHandler.cs` | Add constructor dependencies `ISessionRepository sessionRepository`, `IQuestionMasteryRepository questionMasteryRepository` and `IOptions<MasteryOptions> masteryOptions`. Call the recorder when the grade is Graded. |
| `api/Elmanhg.Application/Shared/Options/SessionsOptions.cs` | `AnswerMaxLength` default `45000`. |
| `api/Elmanhg.Api/appsettings.example.json` | `"Sessions": {..., "AnswerMaxLength": 45000 }`. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by the migration. |
| `api/openapi/v1.json` | Regenerated by `dotnet build` (`pendingAnswer` on `SessionItemResult`). |
| `api/Elmanhg.Tests/Builders/EssayGradeBuilder.cs` | Add `ForSession(Guid sessionId)` (make `_sessionId` non-readonly), `WithTimeTaken(int ms)`, `WithMaxScore(int)`. `Build()` passes `_timeTakenMilliseconds` (default 0). |
| `api/Elmanhg.Tests/Builders/SessionBuilder.cs` | Add `public Session BuildWithEssay(bool isTestMode = false)` → `StartQuiz` with `[Questions.Approved().Build(), Questions.Essay().Approved().Build()]` (MCQ at position 1, essay at position 2). |
| `api/Elmanhg.Tests/Builders/ExamSessionBuilder.cs` | Add `public Session BuildWithEssay()` → a unit exam with `[mcq, essay]` (blueprint `[Mcq 1, Essay 1]`). |
| `api/Elmanhg.Tests/Integration/EssayGrading/EssayGradingTestData.cs` | Pass `0` for the new `Request` parameter. Add `SeedServableEssayLessonAsync(ApiFactory factory)` → `(Guid LessonId, Guid EssayQuestionId)`: one Published lesson with one Approved essay (`QuestionBuilder.EssayContent()`, max score 5), approved the way `SessionTestData.SeedServableLessonAsync` approves. Add `SeedEssayUnitExamAsync(ApiFactory factory)` → `(Guid UnitId, Guid McqQuestionId, Guid EssayQuestionId)`: a unit whose blueprint is `[Mcq 1, Essay 1]` with one servable question of each, following `ExamTestData`. Add `ReadGradeForAsync(ApiFactory, Guid sessionId, Guid questionId)` → `EssayGrade`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `["Sessions:AnswerMaxLength"] = "45000"`. |
| Existing test files listed as **modify** in the Test plan | Constructor updates and behaviour changes (named per row). |
| `web/src/features/questions/components/EssayAnswerInput.tsx` | The editor upgrade (see Files to create, contract W-EssayAnswerInput). |
| `web/src/features/questions/api/essayValues.ts` | Add `isOverWordLimit(maxWords: number \| null \| undefined, text: string): boolean` → `maxWords != null && countWords(text) > maxWords`. |
| `web/src/features/questions/api/questionOptions.ts` | `servedQuestionTypes` adds `'Essay'` (last). Add `export const essayAnswerMaxLength = 20000; // mirrors Content:QuestionEssayAnswerMaxLength` and `export const essayCharactersLeftShownAt = 1000;`. |
| `web/src/features/questions/index.ts` | Also export `countWords`, `isOverWordLimit` (from `./api/essayValues`), `essayBodySchema` (from `./schemas/questionContentSchemas`) and `essayAnswerMaxLength`. |
| `web/src/features/questions/i18n/{ar,en}.json` | `view.essayOverLimit`, `view.essayCharactersLeft`. |
| `web/src/features/quiz/api/quizItem.ts` | `quizQuestionTypes` adds `'Essay'`. `toQuizQuestion` sets `maxWords: type === 'Essay' ? (essayBodySchema.safeParse(item.body).data?.maxWords ?? null) : null`. |
| `web/src/features/quiz/api/quizSession.ts` | In `mergeAnsweredItem`, unanswered = `item.attempt === null && item.pendingAnswer === null`. |
| `web/src/features/quiz/components/QuizRunner.tsx` | Render `QuizEssayCard` when `nav.item.type === 'Essay'`, else `QuizQuestionCard`, with the same props and `key`. |
| `web/src/features/quiz/components/QuizQuestionCard.tsx` | Replace the inline header `div` with `<QuizQuestionHeading …/>`. No behaviour change. |
| `web/src/features/quiz/components/QuizQuestionActions.tsx` | New prop `isEssay?: boolean`. The check button label is `session.submitEssay` / `session.submittingEssay` when true. |
| `web/src/features/quiz/components/EssayGradeStatus.tsx` | New prop `onGraded?: () => void`, passed to `useEssayGrade`. |
| `web/src/features/quiz/hooks/useEssayGrade.ts` | Signature `useEssayGrade(sessionId, questionId, onGraded?)`. See contract W-useEssayGrade. |
| `web/src/features/quiz/hooks/useQuizAnswer.ts` | Use `useQuizSubmit` for the mutation, paywall and toast. Behaviour identical. |
| `web/src/features/quiz/components/QuizResultSummary.tsx` | Answered = `attempt !== null \|\| isWrittenEssay(item)`. Show `result.essaysPending` (caption, `text-text-muted`) when `hasPendingEssay(session.items)`. |
| `web/src/features/quiz/pages/QuizResultPage.tsx` | The review list keeps items with `attempt !== null \|\| isWrittenEssay(item)`. Written essays render `EssayReviewItem` with `onGraded` = invalidate `getGetSessionQueryKey(sessionId)` + `invalidateMastery(queryClient)`. |
| `web/src/features/quiz/index.ts` | Export `EssayReviewItem`, `isWrittenEssay`, `hasPendingEssay`, `essayAnswerText`. |
| `web/src/features/quiz/i18n/{ar,en}.json` | Keys listed under i18n below. |
| `web/src/features/exam/components/ExamReviewItem.tsx` | Call `useQueryClient()` first. `if (isWrittenEssay(item)) return <EssayReviewItem sessionId={sessionId} item={item} onGraded={…} />;`, where `onGraded` invalidates `getGetExamSessionQueryKey(sessionId)` and calls `invalidateExamViews(queryClient)`. |
| `web/src/features/exam/components/ExamResultSummary.tsx` | Same answered-count and pending-note change as the quiz (key `exam:result.essaysPending`). |
| `web/src/features/exam/i18n/{ar,en}.json` | `result.essaysPending`. |
| `web/src/features/blueprints/api/blueprintValues.ts` | `emptyBlueprintValues.counts.Essay = '0'`; `toFormValues` adds `Essay: count('Essay')`. |
| `web/src/features/blueprints/schemas/examBlueprintSchema.ts` | `counts` adds `Essay: z.string()`. |
| `web/src/features/mathSteps/api/mathDraftStore.ts` | `readMathDraft`, `writeMathDraft`, `clearMathDraft` and `purgeExpiredMathDrafts` delegate to `shared/lib/localDraft`. Private `parseDraft` and `removeDraft` are deleted. Public names, signatures and behaviour are unchanged. |
| `web/src/test/quizFixtures.ts` | `quizItem` adds `pendingAnswer: null`. `quizSession` computes `currentPosition` from `attempt === null && pendingAnswer === null`. Add `essayQuizItem(position, overrides?)` → `quizItem(position, { type: 'Essay', body: { maxWords: 5 }, maxScore: 5, ...overrides })`. |
| `web/src/test/examFixtures.ts` | Add `essayExamItem(position, overrides?)` → `examItem(position, { type: 'Essay', body: {}, maxScore: 5, ...overrides })`. |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api` only. |
| `docs/essay-grading.md`, `docs/sessions.md`, `docs/exams.md`, `docs/question-schemas.md`, `docs/exam-blueprints.md`, `docs/mastery.md`, `docs/content-retrieval.md`, `docs/PRD.md`, `docs/claude-design-prompt.md`, `docs/prototype.md` | See Docs below. |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Sessions/EssaySubmission.cs` | record | `namespace Elmanhg.Domain.Sessions; public sealed record EssaySubmission(bool IsNew, int TimeTakenMilliseconds, DateTimeOffset SubmittedAt) { public static EssaySubmission Replay { get; } = new(false, 0, default); }` |
| 2 | `api/Elmanhg.Domain/Sessions/Session.Essays.cs` | partial class | `public partial class Session`, with `FindPendingEssayAnswer`, `SubmitEssay` and `RecordEssayAttempt`. Bodies are in Domain behaviour. |
| 3 | `api/Elmanhg.Application/EssayGrading/Shared/EssayAttemptRecorder.cs` | static helper | `namespace Elmanhg.Application.EssayGrading.Shared; public static class EssayAttemptRecorder { public static async Task RecordAsync(EssayGrade grade, ISessionRepository sessionRepository, IQuestionMasteryRepository questionMasteryRepository, decimal correctThreshold, DateTimeOffset now, CancellationToken cancellationToken) }`. Steps: (1) `session = await sessionRepository.FirstOrDefaultAsync(x => x.Id == grade.SessionId && x.StudentId == grade.StudentId, ct, include: q => q.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery())`; null → return. (2) `item = session.GetItem(grade.QuestionId)`; null → return. (3) `attempt = session.RecordEssayAttempt(item, grade.Answer, grade.ToQuestionGrade(), AttemptGrader.AI, grade.TimeTakenMilliseconds, grade.RequestedAt, now)`; if `attempt is null \|\| session.IsTestMode` → return. (4) `mastery = await questionMasteryRepository.FirstOrDefaultAsync(x => x.StudentId == session.StudentId && x.QuestionId == attempt.QuestionId, ct)`; null → `await questionMasteryRepository.AddAsync(QuestionMastery.Start(session.StudentId, attempt.QuestionId, MasteryAttempt.From(attempt)), ct)`; else `mastery.Record(MasteryAttempt.From(attempt), correctThreshold)`. No save: the caller saves. `ConfigureAwait(false)` on every await. |
| 4 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddEssayGradeTimeTaken.cs` (+ `.Designer.cs`) | EF migration | Generated: `dotnet ef migrations add AddEssayGradeTimeTaken -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. It must contain only `AddColumn<int>("TimeTakenMilliseconds", "EssayGrades", nullable: false, defaultValue: 0)` and the matching `DropColumn` in `Down`. No `AppDbContext` mapping line is needed. |
| 5 | `web/src/shared/lib/localDraft.ts` | module | `export function readLocalDraft<T>(key: string, now: number, ttlMilliseconds: number, isAnswer: (answer: unknown) => answer is T): T \| null`: `localStorage.getItem` in try/catch (a throw returns null); unparseable JSON, a non-finite or missing `savedAt`, `!isAnswer(answer)` or `now - savedAt > ttl` → remove the key and return null. `export function writeLocalDraft(key: string, answer: unknown, now: number): boolean`: `setItem(key, JSON.stringify({ savedAt: now, answer }))`, false on throw. `export function removeLocalDraft(key: string): void` (swallows throws). `export function purgeLocalDrafts<T>(prefix: string, now: number, ttlMilliseconds: number, isAnswer: (answer: unknown) => answer is T): void`: collect the keys starting with `prefix` first, then `readLocalDraft` each (swallows throws). Moved verbatim from `mathDraftStore.ts`'s logic. |
| 6 | `web/src/shared/lib/localDraft.test.ts` | test | See Test plan. |
| 7 | `web/src/features/quiz/api/essayDraftStore.ts` | module | `export interface EssayDraftOwner { studentId: string; sessionId: string; questionId: string }`; `export const essayDraftKeyPrefix = 'elmanhg.essayDraft.'`; `export const essayDraftTtlMilliseconds = 604_800_000`; `export const essayDraftSaveDelayMilliseconds = 800`; `essayDraftStorageKey(owner)` → `${prefix}${studentId}.${sessionId}.${questionId}`; `const isDraftText = (answer: unknown): answer is string => typeof answer === 'string'`; `readEssayDraft(key, now): string \| null`; `writeEssayDraft(key, text, now): boolean`; `clearEssayDraft(owner): void`; `purgeExpiredEssayDrafts(now): void`. All delegate to `localDraft`. |
| 8 | `web/src/features/quiz/api/essayDraftStore.test.ts` | test | See Test plan. |
| 9 | `web/src/features/quiz/api/essayItem.ts` | module | `export type EssayItem = Pick<SessionItemResult, 'type' \| 'attempt'> & { pendingAnswer?: JsonElement \| null; savedAnswer?: JsonElement \| null }`. `const essayTextSchema = z.object({ text: z.string() })`. `essayAnswerText(item): string`: source = `item.attempt?.answer ?? item.pendingAnswer ?? item.savedAnswer ?? null`; returns `safeParse(source).data?.text ?? ''`. `isWrittenEssay(item): boolean` → `item.type === 'Essay' && essayAnswerText(item).trim() !== ''`. `hasPendingEssay(items: readonly EssayItem[]): boolean` → `items.some(i => i.attempt === null && isWrittenEssay(i))`. |
| 10 | `web/src/features/quiz/api/essayItem.test.ts` | test | See Test plan. |
| 11 | `web/src/features/quiz/hooks/useEssayDraft.ts` | hook | `export type EssayDraftStatusValue = 'idle' \| 'restored' \| 'saved' \| 'error'`. `export interface EssayDraft { text: string; change: (text: string) => void; status: EssayDraftStatusValue; discard: () => void }`. `export function useEssayDraft(owner: EssayDraftOwner): EssayDraft`. Structure mirrors `useMathStepsDraft`: lazy initial read (restored text → status `'restored'`, else `''`/`'idle'`); a `latest` ref and a `timer` ref; an effect keyed on the storage key that purges expired drafts and flushes on `visibilitychange` (hidden), on `pagehide` and on unmount (`flush(false)`); `change` sets the text and ref and restarts an `essayDraftSaveDelayMilliseconds` timer that writes and sets `'saved'`/`'error'`. `discard` clears the pending timer (setting `timer.current = undefined`), calls `clearEssayDraft(owner)` and sets status `'idle'`. |
| 12 | `web/src/features/quiz/hooks/useQuizSubmit.ts` | hook | `export interface QuizSubmit { submit: (data: SubmitAnswerRequest) => void; isPending: boolean; paywall: PaywallReason \| null; closePaywall: () => void }`. `export function useQuizSubmit(sessionId: string, onAnswered?: () => void): QuizSubmit`. Body: the `useSubmitSessionAnswer` config moved from `useQuizAnswer` (onSuccess: merge into the session cache, `invalidateMastery`, invalidate usage, `trackFunnelEvent('FirstQuizAnswered', { once: true })`, then `onAnswered?.()`; onError: paywall or toast, then invalidate session and usage). `submit(data)` → `mutation.mutate({ sessionId, data })`. |
| 13 | `web/src/features/quiz/hooks/useQuizEssay.ts` | hook | `export type EssayProblem = 'required' \| 'overLimit' \| null`. `export interface QuizEssayState { text: string; change: (text: string) => void; draftStatus: EssayDraftStatusValue; problem: EssayProblem; submit: () => void; isSubmitting: boolean; paywall: PaywallReason \| null; closePaywall: () => void; refreshSession: () => void }`. `export function useQuizEssay(sessionId: string, item: SessionItemResult, question: StudentQuestion): QuizEssayState`. Steps: `studentId = useSession()?.userId ?? 'anonymous'`; `draft = useEssayDraft({ studentId, sessionId, questionId: item.questionId })`; `[shownAt] = useState(() => Date.now())`; `[problem, setProblem] = useState<EssayProblem>(null)`; `submitter = useQuizSubmit(sessionId, draft.discard)`; `change` → `draft.change(text); setProblem(null)`; `submit` → blank trimmed → `'required'`; `isOverWordLimit(question.maxWords, draft.text)` → `'overLimit'`; else `submitter.submit({ questionId: item.questionId, answer: { text: draft.text }, timeTakenMilliseconds: Math.max(0, Date.now() - shownAt) })`; `refreshSession` → invalidate `getGetSessionQueryKey(sessionId)` + `invalidateMastery(queryClient)`. |
| 14 | `web/src/features/quiz/components/QuizQuestionHeading.tsx` | component | `export interface QuizQuestionHeadingProps { id: string; position: number; total: number; type: StudentQuestion['type']; focusOnMount: boolean }`. It renders the header `div` (h2 with `tabIndex={-1}`, the focus ref when `focusOnMount`, the counter `session.counter` and the type chip `questions:types.${type}`) exactly as `QuizQuestionCard` does today. |
| 15 | `web/src/features/quiz/components/QuizEssayCard.tsx` | component | Props are identical to `QuizQuestionCardProps`. `question = toQuizQuestion(item)`; `essay = useQuizEssay(sessionId, item, question)`; `submitted = item.attempt !== null \|\| item.pendingAnswer !== null`. An `article` (the same classes as `QuizQuestionCard`) with `QuizQuestionHeading`. **Not submitted:** `QuestionView` (answer `{ ...emptyAnswer(), text: essay.text }`, `onAnswerChange={(a) => essay.change(a.text)}`, `disabled={essay.isSubmitting}`); `EssayDraftStatus`; `role="alert"` `text-caption text-danger` for `problem === 'required'` → `session.answerRequired`, and for `'overLimit'` → `session.essayOverLimit` `{ max: question.maxWords }`. **Submitted:** the stem (`RichTextViewer` in `div.text-body.font-semibold`). If `isWrittenEssay(item)`: `EssayAnswerText` + `EssayGradeStatus sessionId questionId onGraded={essay.refreshSession}` + the explanation (label `feedback.explanation`) when `item.explanation`. Otherwise, when `item.attempt`: `FeedbackPanel` (with the same `ask` as `QuizQuestionCard`). Always: `QuizQuestionActions` (`answered={submitted}`, `isChecking={essay.isSubmitting}`, `onCheck={essay.submit}`, `isEssay`) and `PaywallDialog`. At most 120 lines: the explanation block stays inline, with no extra component. |
| 16 | `web/src/features/quiz/components/EssayDraftStatus.tsx` | component | `EssayDraftStatusProps { status: EssayDraftStatusValue }`. `<p role="status">`, class `text-caption text-danger` for `'error'`, else `text-caption text-text-muted`; text is `''` for `'idle'`, else `t('essayDraft.${status}')` (namespace `quiz`). |
| 17 | `web/src/features/quiz/components/EssayAnswerText.tsx` | component | `EssayAnswerTextProps { text: string }`. `div.flex.flex-col.gap-1` > `p.text-caption.font-semibold.text-text-muted` `t('essayAnswer.label')` + `p[dir=auto].whitespace-pre-wrap.rounded-md.border.border-border.bg-soft.px-3.5.py-3.text-ui.text-text` `{text}`, rendered as React text (escaped). |
| 18 | `web/src/features/quiz/components/EssayReviewItem.tsx` | component | `EssayReviewItemProps { sessionId: string; item: EssayItem & Pick<SessionItemResult, 'position' \| 'questionId' \| 'stem' \| 'explanation'>; onGraded?: () => void }`. An `article` (the classes of `QuizReviewItem`) with `aria-labelledby`: an h3 `result.reviewItem {position}` + the type chip; the stem (`RichTextViewer`); `EssayAnswerText text={essayAnswerText(item)}`; `EssayGradeStatus sessionId questionId onGraded`; the explanation (label `feedback.explanation`) when present. |
| 19 | `web/src/features/quiz/pages/QuizPage.essay.test.tsx` | test | See Test plan. |
| 20 | `web/src/features/quiz/pages/QuizResultPage.essay.test.tsx` | test | See Test plan. |
| 21 | `web/src/features/questions/components/EssayAnswerInput.test.tsx` | test | See Test plan. |
| 22 | `web/src/features/exam/pages/ExamPage.essay.test.tsx` | test | See Test plan. |
| 23 | `web/src/features/exam/pages/ExamResultPage.essay.test.tsx` | test | See Test plan. |
| 24 | `api/Elmanhg.Tests/Domain/Sessions/SessionEssayTests.cs` | test | See Test plan. |
| 25 | `api/Elmanhg.Tests/Integration/Sessions/EssayAnswerEndpointTests.cs` | test | See Test plan. |
| 26 | `api/Elmanhg.Tests/Integration/Exams/ExamEssayEndpointTests.cs` | test | See Test plan. |

**W-EssayAnswerInput** (modifies `EssayAnswerInput.tsx`, same props)
- The `textarea` keeps `id`, `value`, `placeholder`, `disabled` and `onChange`, and adds:
  - `dir="auto"`
  - `rows={8}`
  - `maxLength={essayAnswerMaxLength}`
  - `spellCheck`
  - `aria-invalid={over \|\| undefined}`
  - `aria-describedby={countId}`
- The existing class list is kept. Where `over = isOverWordLimit(question.maxWords, answer.text)`.
- Under the textarea is `div#countId.flex.flex-wrap.justify-between.gap-2`, containing:
  - `p` with the existing word text (`view.essayWordsOf` / `view.essayWords`). Its class is `text-caption text-danger` when `over`, else `text-caption text-text-muted`. When `over`, it appends ` · ` + `t('view.essayOverLimit', { over: count - maxWords })` inside the same `p`.
  - When `left = essayAnswerMaxLength - answer.text.length` is `<= essayCharactersLeftShownAt`: a second `p.text-caption.text-text-muted` with `t('view.essayCharactersLeft', { count: left })`.

**W-useEssayGrade**
- `useEssayGrade(sessionId, questionId, onGraded?: () => void)` returns the same query as today.
- It adds `const status = query.data?.status; const previous = useRef(status);`.
- It adds `useEffect(() => { if (previous.current === 'Pending' && status === 'Graded') { onGraded?.(); } previous.current = status; }, [status, onGraded]);`.

**i18n** (en / ar)
- `quiz` namespace:

  | Key | en | ar |
  |---|---|---|
  | `session.submitEssay` | "Submit answer" | «أرسل الإجابة» |
  | `session.submittingEssay` | "Submitting…" | «جارٍ الإرسال…» |
  | `session.essayOverLimit` | "Shorten your essay to {max, number} words or fewer." | «اختصر مقالك إلى {max, number} كلمة أو أقل.» |
  | `essayDraft.restored` | "Your saved draft was restored." | «استعدنا مسودتك المحفوظة.» |
  | `essayDraft.saved` | "Draft saved on this device" | «حُفظت المسودة على هذا الجهاز» |
  | `essayDraft.error` | "Could not save the draft on this device." | «تعذّر حفظ المسودة على هذا الجهاز.» |
  | `essayAnswer.label` | "Your answer" | «إجابتك» |
  | `result.essaysPending` | "Some essays are still being graded. The score will update when they are done." | «بعض الإجابات المقالية ما زالت قيد التصحيح، وستتحدّث الدرجة عند اكتمالها.» |

- `exam` namespace, `result.essaysPending`: the same text as the quiz key.
- `questions` namespace:

  | Key | en | ar |
  |---|---|---|
  | `view.essayOverLimit` | "{over} words over the limit" | «تجاوزت الحد بـ {over} كلمة» |
  | `view.essayCharactersLeft` | "{count} characters left" | «متبقٍّ {count} حرف» |

## Error codes
No new codes. Reused:

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ErrorCodes.QuestionEssayAnswerTooLong` | `QUESTION_ESSAY_ANSWER_TOO_LONG` | `SubmitAnswerHandler`, `SaveExamAnswerHandler` (essay text > `Content:QuestionEssayAnswerMaxLength`) | `ApplicationValidationCoreException` | 422 |
| `ErrorCodes.SessionQuestionAlreadyAnswered` | `SESSION_QUESTION_ALREADY_ANSWERED` | `Session.SubmitEssay` (different essay after submit), `Session.RecordAttempt` (item has a pending essay) | `ConflictCoreException` | 409 |
| `ErrorCodes.SessionAlreadySubmitted` | `SESSION_ALREADY_SUBMITTED` | `Session.SubmitEssay` on a finished quiz | `BusinessRuleViolationCoreException` | 400 |
| `ErrorCodes.QuestionNotFound` | `QUESTION_NOT_FOUND` | `SubmitAnswerHandler` when the essay's question row is missing even with filters ignored | `NotFoundCoreException` | 404 |

The resource strings already exist in `Messages.{ar,en}.resx` and in `web/src/shared/i18n/{ar,en}.json` (checked).

## Domain behaviour
`EssayGrader.GradeBlank(EssayAnswer answer)`:
```csharp
if (answer.Text is null || !string.IsNullOrWhiteSpace(answer.Text))
{
    throw new InvalidOperationException("A written essay is graded by the AI grader.");
}

return NormalisedGrade.Unanswered;
```
`EssayGrade.Request(..., DateTimeOffset requestedAt, int timeTakenMilliseconds)`: first `ArgumentOutOfRangeException.ThrowIfNegative(timeTakenMilliseconds);` (after the blank check). The initializer sets `TimeTakenMilliseconds = timeTakenMilliseconds`.

`EssayGrade.ToQuestionGrade()`:
```csharp
if (Status != EssayGradeStatus.Graded || Score is null || NormalisedScore is null)
{
    throw new InvalidOperationException("Only a graded essay has a final score.");
}

return new QuestionGrade(Score.Value, NormalisedScore.Value, QuestionGrade.ToOutcome(NormalisedScore.Value), null);
```
`Session.Essays.cs`:
```csharp
public string? FindPendingEssayAnswer(SessionItem item) => !IsExam && item.SavedAnswer is not null && FindAttempt(item.QuestionId) is null ? item.SavedAnswer : null;

public EssaySubmission SubmitEssay(SessionItem item, string answer, int? reportedTimeTakenMilliseconds)
{
    if (IsExam) throw new InvalidOperationException("Exam essays are saved with SaveExamAnswer.");
    if (!Items.Contains(item)) throw new InvalidOperationException("Session item does not belong to this session.");
    var submitted = FindAttempt(item.QuestionId)?.Answer ?? item.SavedAnswer;
    if (submitted is not null)
    {
        if (QuestionJson.AreEquivalent(submitted, answer)) return EssaySubmission.Replay;
        throw new ConflictCoreException(ErrorCodes.SessionQuestionAlreadyAnswered);
    }
    EnsureNotSubmitted();
    var now = UtcNowToMicroseconds();
    var timeTaken = MeasureTimeTaken(LastActivityAt, now, reportedTimeTakenMilliseconds);
    item.SaveAnswer(answer, now);
    Touch(now);                       // sets LastActivityAt, UpdatedBy, UpdationDate
    return new EssaySubmission(true, timeTaken, now);
}

public Attempt? RecordEssayAttempt(SessionItem item, string answer, QuestionGrade grade, AttemptGrader gradedBy, int timeTakenMilliseconds, DateTimeOffset answeredAt, DateTimeOffset now)
{
    if (!Items.Contains(item)) throw new InvalidOperationException("Session item does not belong to this session.");
    if (FindAttempt(item.QuestionId) is not null) return null;
    var attempt = Attempt.Create(this, item, answer, grade, timeTakenMilliseconds, ToMicroseconds(answeredAt), gradedBy);
    Attempts.Add(attempt);
    if (IsSubmitted) ScorePercent = CalculateScorePercent();
    // Stamping the row makes the xmin token serialise this against a concurrent answer or finish.
    UpdationDate = ToMicroseconds(now);
    return attempt;
}
```
Use multi-line braces per the style guide; the one-liners above are shorthand. `RecordEssayAttempt` never checks `EnsureNotSubmitted`, because grades land after finish, and never changes `LastActivityAt`.

`Session.SubmitExam(grades, essayQuestionIds, now)`:
- It is the existing body, except that `answered = Items.Where(x => x.SavedAnswer is not null && !essayQuestionIds.Contains(x.QuestionId))`.
- The missing-grade guard applies to `answered` only.
- `SubmitExam(grades, now) => SubmitExam(grades, new HashSet<Guid>(), now)`.

## Application behaviour (handlers, ordered)
**`QuestionAnswerRules`**
```csharp
public static bool TryReadWrittenEssay(QuestionType type, JsonElement answer, [NotNullWhen(true)] out string? text)
{
    text = type == QuestionType.Essay && QuestionSchemaReader.TryRead<EssayAnswer>(answer, out var essay) && !string.IsNullOrWhiteSpace(essay.Text) ? essay.Text.Trim() : null;
    return text is not null;
}

public static bool IsEssayTooLong(QuestionType type, JsonElement answer, int maxLength) => type == QuestionType.Essay && QuestionSchemaReader.Read<EssayAnswer>(answer).Text!.Length > maxLength;
```

**`SubmitAnswerHandler.Handle`**
1. Authentication, the session load and `item` are unchanged.
2. Free tier: `if (!session.IsTestMode && session.FindAttempt(item.QuestionId) is null && item.SavedAnswer is null) EnsureFreeTierAsync(...)`.
3. The revision, `type` and `CanRead` → 422 steps are unchanged.
4. `if (QuestionAnswerRules.IsEssayTooLong(type, request.Answer, contentOptions.Value.QuestionEssayAnswerMaxLength)) throw new ApplicationValidationCoreException(ErrorCodes.QuestionEssayAnswerTooLong);`
5. `if (QuestionAnswerRules.TryReadWrittenEssay(type, request.Answer, out var essayText))`:
   1. `var submission = session.SubmitEssay(item, QuestionAnswerRules.Canonicalize(type, request.Answer), request.TimeTakenMilliseconds);`
   2. If `submission.IsNew`:
      - `question = await questionRepository.FirstOrDefaultAsync(x => x.Id == item.QuestionId, ct, include: q => q.IgnoreQueryFilters(), asNoTracking: true) ?? throw new NotFoundCoreException(ErrorCodes.QuestionNotFound)`.
      - `await essayGradeRepository.AddAsync(EssayGrade.Request(session.StudentId, session.Id, question.SubjectId, item.QuestionId, item.QuestionVersion, item.MaxScore, essayText, submission.SubmittedAt, submission.TimeTakenMilliseconds), ct)`.
   3. `await sessionRepository.SaveChangesAsync(ct)` (a replay still saves; it is a no-op).
   4. `return SessionResultGenerator.GenerateItem(session, item, revision, localizer)`.
6. Otherwise, the existing path runs unchanged. A blank essay grades as Unanswered through `revision.Grade`.

**`SaveExamAnswerHandler.Handle`**
- After `CanRead`, apply the same `IsEssayTooLong` → 422 check.

**`ExamSubmission.SubmitAsync(Session session, IReadOnlyCollection<QuestionRevision> revisions, IQuestionRepository questionRepository, IQuestionMasteryRepository questionMasteryRepository, IEssayGradeRepository essayGradeRepository, decimal correctThreshold, DateTimeOffset now, CancellationToken ct)`**
1. `if (session.IsSubmitted) return;`
2. `written = session.Items.Where(x => x.SavedAnswer is not null).Select(x => (Item: x, Text: WrittenEssayText(FindRevision(revisions, x), x.SavedAnswer!))).Where(x => x.Text is not null).ToList()`.
   - `WrittenEssayText(revision, answer)` parses the JSON, then calls `TryReadWrittenEssay(revision.ReadSnapshot().Type, element, out var text)` and returns `text` or null.
3. `essayIds = written.Select(x => x.Item.QuestionId).ToHashSet()`.
4. `grades` = items with a `SavedAnswer` that are not in `essayIds`, graded as today.
5. `attempts = session.SubmitExam(grades, essayIds, now)`.
6. If `written.Count > 0`:
   - `questions = await questionRepository.FindAsync(x => essayIds.Contains(x.Id), ct, include: q => q.IgnoreQueryFilters(), asNoTracking: true)`.
   - `await essayGradeRepository.AddRangeAsync(written.Select(x => EssayGrade.Request(session.StudentId, session.Id, questions.First(q => q.Id == x.Item.QuestionId).SubjectId, x.Item.QuestionId, x.Item.QuestionVersion, x.Item.MaxScore, x.Text!, session.SubmittedAt!.Value, 0)).ToList(), ct)`.
7. The mastery block is unchanged, over `attempts`.

**`GradeEssayHandler.Handle`**
- After `grade.Complete(...)`: `if (grade.Status == EssayGradeStatus.Graded) await EssayAttemptRecorder.RecordAsync(grade, sessionRepository, questionMasteryRepository, masteryOptions.Value.CorrectThreshold, timeProvider.GetUtcNow(), ct);`
- Then the existing single `essayGradeRepository.SaveChangesAsync` (a shared `AppDbContext`, so it is one transaction).

**`SessionResultGenerator.GenerateItem`**
- `var pendingAnswer = session.FindPendingEssayAnswer(item);`
- `var reveal = attempt is not null || pendingAnswer is not null || session.IsSubmitted;`
- The last argument is `pendingAnswer is null ? null : Parse(pendingAnswer)`.

## API surface
No new routes or policies. The changed contracts are:

| Method · route | Policy | Change |
|---|---|---|
| `POST /api/sessions/{sessionId}/answers` | `Assessments.Take` (unchanged) | For a written essay: 200 `SessionItemResult` with `attempt: null`, `pendingAnswer: {"text": "<trimmed>"}`, and `correctAnswer`/`explanation` revealed. It creates an `EssayGrade`. 422 `QUESTION_ESSAY_ANSWER_TOO_LONG` for text > 20 000. |
| `GET /api/sessions/{id}`, `POST /api/sessions/quiz`, `POST /api/sessions/{id}/finish` | unchanged | `SessionItemResult.pendingAnswer` (null unless a quiz essay awaits its grade). `currentPosition` skips a pending essay. |
| `PUT /api/exams/{sessionId}/answers/{questionId}` | unchanged | Essay text > 20 000 → 422 `QUESTION_ESSAY_ANSWER_TOO_LONG`. The raw cap is 45 000. |
| `POST /api/exams/{sessionId}/submit` (+ auto-submit) | unchanged | A written essay has no attempt at submit; it creates an `EssayGrade`. `scorePercent`/`isPassed` are provisional until the grade is final. |
| `GET /api/sessions/{sessionId}/questions/{questionId}/essay-grade` | `Assessments.Take` | Unchanged (#118). |

Regenerate: `dotnet build api/` → `api/openapi/v1.json`, then `npm --prefix web run gen:api`.

## Test plan
Dotnet uses xUnit v3 + FluentAssertions (the repo's pin) + NSubstitute. Web uses Vitest + Testing Library + MSW. "modify" rows authorise edits to existing tests; no other existing test may change.

### Dotnet — modify
| # | Test class (file) | Test method | Change / asserts |
|---|---|---|---|
| D1 | `ServableQuestionSpecificationTests` | `IsSatisfiedBy_ApprovedEssayInPublishedLesson_ReturnsFalse` → rename `IsSatisfiedBy_ApprovedEssayInPublishedLesson_ReturnsTrue` | asserts `true` |
| D2 | `ServableQuestionSpecificationTests` | `ServedTypes_EveryTypeExceptEssay` → rename `ServedTypes_EveryType` | `ServedTypes` equals `Enum.GetValues<QuestionType>()` in order |
| D3 | `ServableTypeCountsTests` | `ToResults_EssayAvailable_ListsServedTypesOnly` → rename `ToResults_EssayAvailable_ListsEssayCount` | the result contains `(Essay, 3)`; the other types are 0 except Mcq 2 |
| D4 | `EssayValidationEndpointTests` | `Approve_EssayInPublishedLesson_ApprovedButNotServable` → rename `Approve_EssayInPublishedLesson_ApprovedAndServable` | `servableQuestionCount` 1, `isServable` true |
| D5 | `AppDbContextTests.Migrate_FreshDatabase_LeavesNoPendingMigrations` | the same | append `thirtyFifth => thirtyFifth.Should().EndWith("_AddEssayGradeTimeTaken")` |
| D6 | `GradeEssayHandlerTests`, `SubmitExamHandlerTests`, `AutoSubmitExamHandlerTests`, `SubmitAnswerHandlerTests`, `SubmitAnswerFreeTierTests`, `SaveExamAnswerHandlerTests` | constructor/setup only | add the new substitutes or options (`ISessionRepository`, `IQuestionMasteryRepository`, `Options.Create(new MasteryOptions())`, `IEssayGradeRepository`, `Options.Create(new ContentOptions { QuestionEssayAnswerMaxLength = 20000 })`). No assertion changes. |

### Dotnet — new
| # | Test class | Test method | Asserts |
|---|---|---|---|
| T1 | `QuestionGraderTests` (add) | `Grade_BlankEssay_ReturnsUnanswered` (Theory: `""`, `"   "`) | score 0, `Outcome` Incorrect, feedback `GradeFeedback.Unanswered` |
| T2 | `QuestionGraderTests` | `Grade_WrittenEssay_ThrowsInvalidOperationException` | throws for `{"text":"x"}` |
| T3 | `EssayGradeTests` (add) | `Request_StoresTimeTaken` | `TimeTakenMilliseconds` = the passed value |
| T4 | `EssayGradeTests` | `Request_NegativeTimeTaken_ThrowsArgumentOutOfRange` | exception type |
| T5 | `EssayGradeTests` | `ToQuestionGrade_Graded_ReturnsScoreNormalisedAndOutcome` | the `(Score, NormalisedScore, Outcome, Feedback null)` values from `Complete` |
| T6 | `EssayGradeTests` | `ToQuestionGrade_Pending_ThrowsInvalidOperation` | exception type |
| T7 | `SessionEssayTests` | `SubmitEssay_NewEssay_SavesPendingAnswerAndMeasuresTime` | `IsNew`; `SavedAnswer` = the answer; `AnswerSavedAt` = `SubmittedAt` = `LastActivityAt`; `TimeTakenMilliseconds` ≤ the reported value; no attempt |
| T8 | `SessionEssayTests` | `SubmitEssay_SameEssayAgain_ReturnsReplay` | `IsNew` false; `SavedAnswer` and `LastActivityAt` unchanged |
| T9 | `SessionEssayTests` | `SubmitEssay_DifferentEssayAfterSubmit_ThrowsSessionQuestionAlreadyAnswered` | `ConflictCoreException` + code |
| T10 | `SessionEssayTests` | `SubmitEssay_FinishedQuiz_ThrowsSessionAlreadySubmitted` | `BusinessRuleViolationCoreException` + code |
| T11 | `SessionEssayTests` | `SubmitEssay_Exam_ThrowsInvalidOperation` | exception type |
| T12 | `SessionEssayTests` | `CurrentPosition_PendingEssayAtFirstOpenPosition_SkipsIt` | with the MCQ answered and the essay pending → null; with the essay pending and the MCQ open → 1 (use `BuildWithEssay`; answer an item via `RecordAttempt`) |
| T13 | `SessionEssayTests` | `FindPendingEssayAnswer_ExamSavedAnswer_ReturnsNull` | exam drafts are never pending |
| T14 | `SessionEssayTests` | `RecordAttempt_ItemWithPendingEssay_ThrowsSessionQuestionAlreadyAnswered` | code; no attempt added |
| T15 | `SessionEssayTests` | `RecordEssayAttempt_OpenQuiz_AddsAiAttemptAtAnsweredTime` | `GradedBy` AI; `CreatedAt` = answeredAt; score/normalised from the grade; `TimeTaken` from the parameter; `LastActivityAt` unchanged; `UpdationDate` = now; `ScorePercent` null; `FindPendingEssayAnswer` now null |
| T16 | `SessionEssayTests` | `RecordEssayAttempt_FinishedQuiz_RecomputesScorePercent` | `ScorePercent` goes from 0 to the essay share (e.g. 1 MCQ correct + essay 5/5, items max 1+5 → 100) |
| T17 | `SessionEssayTests` | `RecordEssayAttempt_AlreadyRecorded_ReturnsNull` | null; the attempt count is unchanged |
| T18 | `SessionEssayTests` | `SubmitExam_WrittenEssayDeferred_CreatesAttemptsForOthersOnly` | `ExamSessionBuilder.BuildWithEssay`; both saved; grade only the MCQ; one attempt; `ScorePercent` excludes the essay; no throw |
| T19 | `QuestionAnswerRulesTests` (add) | `TryReadWrittenEssay_WrittenEssay_ReturnsTrimmedText` | true, `"text"` trimmed |
| T20 | `QuestionAnswerRulesTests` | `TryReadWrittenEssay_BlankOrOtherType_ReturnsFalse` (Theory: Essay blank; Short with text) | false |
| T21 | `QuestionAnswerRulesTests` | `IsEssayTooLong_OverMax_ReturnsTrue` | true for a length of max+1; false for max |
| T22 | `SubmitAnswerHandlerTests` (add) | `Handle_WrittenEssay_SavesPendingAnswerAndRequestsGrade` | `essayGradeRepository.AddAsync` received once with the student, session, subject (from the question), question, version, max score, trimmed text and time; the result has `Attempt` null and `PendingAnswer.text`; `SaveChangesAsync` Received(1) |
| T23 | `SubmitAnswerHandlerTests` | `Handle_WrittenEssayReplay_RequestsNoSecondGrade` | the second identical call → `AddAsync` DidNotReceive; the result is the same |
| T24 | `SubmitAnswerHandlerTests` | `Handle_BlankEssay_RecordsUnansweredAttempt` | the attempt has score 0 and `GradedBy` Auto; no `EssayGrade` added |
| T25 | `SubmitAnswerHandlerTests` | `Handle_EssayOverMaxLength_ThrowsQuestionEssayAnswerTooLong` | `ApplicationValidationCoreException` + code; `SaveChangesAsync` DidNotReceive |
| T26 | `SubmitAnswerFreeTierTests` (add) | `Handle_PendingEssayReplay_SkipsFreeTierGate` | an exhausted quota + a replayed essay → no throw |
| T27 | `SaveExamAnswerHandlerTests` (add) | `Handle_EssayOverMaxLength_ThrowsQuestionEssayAnswerTooLong` | code; `SaveChangesAsync` DidNotReceive |
| T28 | `GradeEssayHandlerTests` (add) | `Handle_Graded_RecordsAiAttemptAndStartsMastery` | the session (from `SessionBuilder.BuildWithEssay`, essay submitted, grade `ForSession`/`ForQuestion`) gains an AI attempt; `questionMasteryRepository.AddAsync` received once; one save |
| T29 | `GradeEssayHandlerTests` | `Handle_Graded_TestModeSession_RecordsAttemptWithoutMastery` | attempt yes; mastery `AddAsync` DidNotReceive |
| T30 | `GradeEssayHandlerTests` | `Handle_LowConfidence_RecordsNoAttempt` | the session attempt count is unchanged |
| T31 | `GradeEssayHandlerTests` | `Handle_SessionMissing_CompletesWithoutAttempt` | the grade is Graded; one save; no throw |
| T32 | `SubmitExamHandlerTests` (add) | `Handle_WrittenEssay_RequestsGradeWithoutAttempt` | `essayGradeRepository.AddRangeAsync` with one grade (time 0, `RequestedAt` = `SubmittedAt`); no attempt for the essay |
| T33 | `SubmitExamHandlerTests` | `Handle_BlankEssay_RecordsUnansweredAttempt` | the essay attempt has score 0; no grade requested |
| T34 | `AutoSubmitExamHandlerTests` (add) | `Handle_WrittenEssay_RequestsGrade` | `AddRangeAsync` received once |
| T35 | `EssayAnswerEndpointTests` | `PostAnswer_WrittenEssay_Returns200WithPendingAnswerAndPendingGrade` | 200; `attempt` null; `pendingAnswer.text`; the DB has one `Pending` `EssayGrade` with `TimeTakenMilliseconds` > 0 or = 0 and the right `SubjectId` |
| T36 | `EssayAnswerEndpointTests` | `PostAnswer_EssayOver20000Characters_Returns422QuestionEssayAnswerTooLong` | status + problem `code` |
| T37 | `EssayAnswerEndpointTests` | `PostAnswer_DifferentEssayAfterSubmit_Returns409SessionQuestionAlreadyAnswered` | status + code |
| T38 | `EssayAnswerEndpointTests` | `GetSession_PendingEssay_ReturnsPendingAnswerAndNoCurrentPosition` | a one-essay quiz → `currentPosition` null; the item has `pendingAnswer` |
| T39 | `EssayAnswerEndpointTests` | `GradeEssay_AfterQuizFinished_WritesAiAttemptAndUpdatesScore` | finish → `scorePercent` 0; `GradeAsync` → `GET` `scorePercent` 100; the item `attempt.score` 5; the DB attempt `GradedBy` AI; a `QuestionMastery` row exists |
| T40 | `EssayAnswerEndpointTests` | `PostAnswer_OtherStudentsSession_Returns404SessionNotFound` | status + code |
| T41 | `ExamEssayEndpointTests` | `SaveAnswer_EssayOf10000Characters_Returns200` | 200 (it was over the old 4000 cap) |
| T42 | `ExamEssayEndpointTests` | `Submit_WrittenEssay_CreatesPendingGradeAndScoresOthers` | the MCQ answered correctly → `scorePercent` = 1/6×100 rounded (16.67); the essay item `attempt` null, `savedAnswer` set; one Pending grade |
| T43 | `ExamEssayEndpointTests` | `GradeEssay_AfterSubmit_RaisesScorePercent` | after `GradeAsync` → `GET /api/exams/{id}` `scorePercent` 100, `isPassed` true |

### Web — modify
| # | File | Test | Change |
|---|---|---|---|
| W1 | `features/quiz/api/quizItem.test.ts` | `throws for a type outside v1` → rename `maps an essay item with its word limit` | `toQuizQuestion(quizItem(1, { type: 'Essay', body: { maxWords: 150 } }))` → `type` Essay, `maxWords` 150, `options` [] |
| W2 | `features/blueprints/api/blueprintValues.test.ts` | `counts only the served question types` | the expected list ends with `'Essay'` |
| W3 | same file | the `toFormValues` test | the expected `counts` add `Essay: '0'` |

### Web — new
| # | File | `it(...)` | Asserts |
|---|---|---|---|
| X1 | `shared/lib/localDraft.test.ts` | reads back a written draft | the value round-trips |
| X2 | same | drops an expired draft | null, and the key is removed |
| X3 | same | drops a draft whose answer fails the guard | null, and the key is removed |
| X4 | same | purges only expired drafts under the prefix | a fresh key and a key outside the prefix survive |
| X5 | same | reports a failed write | `setItem` throws → false |
| X6 | `quiz/api/essayDraftStore.test.ts` | keys drafts by student, session and question | different owners do not read each other's drafts |
| X7 | same | reads back essay text and rejects a non-string answer | the string is returned; `{answer: 5}` → null |
| X8 | `quiz/api/essayItem.test.ts` | reads the essay text from the attempt, then the pending answer, then the saved answer | precedence |
| X9 | same | treats blank or non-essay items as not written | false for `{text:'  '}` and for Mcq |
| X10 | same | finds a pending essay only while it has no attempt | `hasPendingEssay` true, then false once there is an attempt |
| X11 | `quiz/api/quizSession.test.ts` (add) | treats a pending essay as answered when merging | `currentPosition` moves past it |
| X12 | `questions/components/EssayAnswerInput.test.tsx` | counts words against the limit | "2 of 5 words" |
| X13 | same | flags an essay over the word limit | textbox `aria-invalid="true"`; "1 words over the limit" visible |
| X14 | same | shows characters left near the maximum | typing to within 1000 → "N characters left" |
| X15 | same | caps the text at the maximum length | textbox `maxLength` attribute 20000 |
| X16 | same | writes right-to-left in Arabic | `lng: 'ar'` → root `dir="rtl"`, textbox `dir="auto"`, label «إجابتك المقالية» |
| X17 | `quiz/pages/QuizPage.essay.test.tsx` | restores a saved draft of an unsubmitted essay | a pre-written localStorage draft → the textbox has the text; "Your saved draft was restored." |
| X18 | same | saves the draft on this device while typing | fake timers → after 800 ms "Draft saved on this device" and localStorage has the text |
| X19 | same | asks for an answer before submitting an empty essay | "Answer the question first." alert; no POST (MSW `onUnhandledRequest` guards) |
| X20 | same | blocks an essay over the word limit | 6 words with `maxWords` 5 → "Shorten your essay to 5 words or fewer." |
| X21 | same | submits the essay, clears the draft and shows grading in progress | the POST body `{questionId, answer:{text}, timeTakenMilliseconds}`; «Grading your essay…» visible; the localStorage key removed; the "Next" button shown |
| X22 | same | shows the verdict with criterion marks once graded | pending → poll → "Partially correct", "Definition", "1 / 2", justification |
| X23 | same | shows under review for a low-confidence grade | "Under review", with no score |
| X24 | same | shows the submitted essay read-only on resume | a session item with `pendingAnswer` → "Your answer" + the text; no textbox |
| X25 | same | renders right-to-left in Arabic | «أرسل الإجابة»; `dir="rtl"` |
| X26 | same | has no axe violations | axe on the unsubmitted card |
| X27 | `quiz/pages/QuizResultPage.essay.test.tsx` | lists a pending essay with its grading status and notes the provisional score | the essay review article, «Grading your essay…» and "Some essays are still being graded…"; "Answered 2 of 2" |
| X28 | same | updates the score when the essay is graded | the session handler returns `scorePercent` 50, then 100 after the grade handler flips to Graded → "100 / 100" appears |
| X29 | same | shows a blank essay attempt as an ordinary review | `{text:''}` attempt → the "Wrong answer" feedback panel; no essay-grade request |
| X30 | `quiz/components/EssayGradeStatus.test.tsx` (add) | calls onGraded once when a pending grade becomes graded | `vi.fn` called exactly once after the poll; not called for an initially Graded grade |
| X31 | `exam/pages/ExamPage.essay.test.tsx` | autosaves an essay answer to the server | PUT body `{ answer: { text } }` after 800 ms; the word counter is visible |
| X32 | `exam/pages/ExamResultPage.essay.test.tsx` | shows a written essay with its grading status instead of unanswered | the essay text + «Grading your essay…»; no "You did not answer this question." for it |
| X33 | same | notes the provisional score while an essay is pending | `exam:result.essaysPending` text |
| X34 | same | renders right-to-left in Arabic | `dir="rtl"`, «قيد المراجعة» for an in-review grade |

Existing suites that guard the refactors and must stay green unchanged: `mathDraftStore.test.ts`, `MathStepsAnswer.autosave.test.tsx`, `QuizPage*.test.tsx`, `QuestionView.test.tsx`, `BlueprintEditor.test.tsx`, `ExamPage*.test.tsx`, `EssayGradeStatus.test.tsx` (existing cases).

## Docs (same PR — docs-sync)
| Doc | Change |
|---|---|
| `docs/essay-grading.md` | Model: add the `TimeTakenMilliseconds` row. Lifecycle: `Request` takes the time taken. On `Graded` the same save writes the Attempt (`AI`, `CreatedAt = RequestedAt`) and mastery (non-test) and recomputes a finished session's `ScorePercent`. `InReview` waits for #128. A missing session or item writes none. A session `xmin` conflict fails the attempt and retries. Replace "What #119 adds" with "Student input (#119)": quiz (`SavedAnswer` pending, `pendingAnswer`), exam (written essays graded after submit), blank → Unanswered, essays servable, and the web (editor, local draft for quizzes, `onGraded` refresh). |
| `docs/sessions.md` | `SessionItem.SavedAnswer`/`AnswerSavedAt`: also a quiz essay awaiting its grade. `GradedBy`: `AI` for essays. Lifecycle step 2: the essay path; `currentPosition` skips pending essays. Grading section: replace the #119 sentence. What is revealed: a pending essay reveals. Scoring: essays count when graded; a finished session is re-scored. Options: `AnswerMaxLength` 45000 with the rationale. API: `SessionItemResult` adds `pendingAnswer?`. Error codes: add `QUESTION_ESSAY_ANSWER_TOO_LONG`. Student screens: the essay card (editor, draft, submit, status, result). |
| `docs/exams.md` | Sitting: essay drafts, 422 too long. Submission: a written essay → `EssayGrade`, no attempt until Graded; `scorePercent`/`isPassed` provisional and recomputed; blank → Unanswered. Student screens: the exam result essay review + the pending note. |
| `docs/question-schemas.md` | Line 14: remove "Not servable until #119". Lines 194–195: servable without the essay clause; `ServedTypes` lists all six. Line 237: the limit also applies to the quiz answer and the exam save. |
| `docs/exam-blueprints.md` | Line 41: drop "and not an essay (until #119)". |
| `docs/mastery.md` | Line 51: drop the essay clause. Update rule: add "Essay attempts update mastery when their grade becomes Graded (`GradeEssayHandler`), ordered by the submission time." |
| `docs/content-retrieval.md` | Line 13: drop "not an essay until #119". |
| `docs/PRD.md` | §5.3 servable row: remove `AND type != Essay …`. §17 rule 1: remove "; essays are not servable until student essay input ships". §6 Essay "Student input": "Plain text (Arabic, multi-paragraph)". |
| `docs/claude-design-prompt.md` | Line 135: the essay editor (word/character limits, autosaved draft on the device, «أرسل الإجابة») + the result page review. Line 136: the exam essay box and the result essay status. Line 168: remove the essay clause. |
| `docs/prototype.md` | Line 69: "essays are served since #119; students write in an RTL plain-text editor". |

## Definition of done
- [ ] Essays are servable: `ServableQuestionSpecification` has no Essay clause; `ServedTypes` has 6 types; the web `servedQuestionTypes` includes `'Essay'`; the blueprint form has an Essay count.
- [ ] A quiz written essay → 200 with `pendingAnswer`, a `Pending` `EssayGrade` with `TimeTakenMilliseconds` and `SubjectId`, and no attempt. A replay creates no second grade. A different essay → 409.
- [ ] A blank essay (quiz or exam) → an Auto attempt with score 0 and "unanswered" feedback, and no AI call.
- [ ] Exam submit: written essays → `EssayGrade` rows (time 0, `RequestedAt = SubmittedAt`) and no attempt; the other items are graded as before.
- [ ] `GradeEssayHandler` on `Graded` writes an `AI` attempt with `CreatedAt = RequestedAt` and mastery (non-test) in the same save, and recomputes `ScorePercent` for a finished session. `InReview` writes nothing.
- [ ] `RecordEssayAttempt` stamps `UpdationDate` (the xmin check) and never touches `LastActivityAt`.
- [ ] Essay text > 20 000 → 422 `QUESTION_ESSAY_ANSWER_TOO_LONG` on the quiz answer and the exam save. `Sessions:AnswerMaxLength` is 45000 in code, `appsettings.example.json` and `ApiFactory`.
- [ ] Migration `AddEssayGradeTimeTaken` contains only the one column and is added to the `AppDbContextTests` list.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` are regenerated with no drift.
- [ ] The editor: RTL `dir="auto"`, a word count against `maxWords` with an over-limit state, characters left within 1000 of 20 000, and `maxLength` 20000. The admin preview gets the same editor.
- [ ] The quiz essay draft autosaves after 800 ms and flushes on hide or unmount, is restored with a status line, and is removed on a successful submit.
- [ ] The quiz card, quiz result and exam result show `EssayGradeStatus` (pending, in review, graded with criteria and justification) under the read-only essay. `onGraded` refreshes the score.
- [ ] The result summaries count written essays as answered and show the provisional-score note while any is pending.
- [ ] `mathDraftStore` delegates to `shared/lib/localDraft` with unchanged behaviour (its tests are untouched and green).
- [ ] Every test in the Test plan exists with the listed name and assertion. Only the "modify" rows changed existing tests.
- [ ] `dotnet test api/ -c Release` is green with `appsettings.json` moved aside. Web: `typecheck`, `lint`, `format`, `test` are green.
- [ ] `npm run build && npm run perf:budget` passes with the quiz page ≤ 255 KB br. No budget is raised and no TipTap or KaTeX is added to the quiz chunk.
- [ ] All i18n keys exist in both `ar` and `en`, with no hard-coded strings. Only token classes and logical properties are used.
- [ ] Every doc in the Docs table is updated. No doc still says essays are unservable or "until #119".
