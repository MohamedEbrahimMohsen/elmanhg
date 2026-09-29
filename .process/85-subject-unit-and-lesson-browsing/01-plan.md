# Plan — [E7.S1] Subject, unit and lesson browsing (#85)

## Goal
A student can walk the published curriculum tree: from a home subject card to a **subject page** (its units with mastery %, published-lesson count and best unit-exam score), then to a **unit page** (its published lessons in order with mastery % and the unit-exam entry), then to a **lesson page** with the tabs الشرح / الأهداف / الملخص / التدريب, breadcrumbs on every level, and previous/next-lesson links that continue across units. Opening a lesson page is recorded, which lets the optional PRD §7.4 gate ("the unit exam unlocks after every lesson is opened", issue #179) be switched on by config (`Exams:RequireAllLessonsOpened`, default `false`). The existing student pages (home, progress, exam start, quiz result) now link into the tree.

## Scope
**In**
- api: 3 student tree reads (subject, unit, lesson), with published lessons only and per-node mastery; lesson-opening record (`POST`); `LessonOpening` entity, table and migration; exam lesson-open gate on unit-exam start, multi-unit-exam start and the unit-exam overview (`unopenedLessonCount`); error codes and resources; Postman; OpenAPI.
- web: new feature `browse` (subject, unit and lesson pages, with tab routes, breadcrumbs, prev/next, mastery lines and loading/empty/error/RTL states); home, progress, exam-start and quiz-result links wired; gate warning on exam start; browse queries refreshed after quiz answers.
- docs: new `docs/browsing.md`; updates to `docs/exams.md`, `docs/mastery.md`, `docs/progress.md`, `docs/claude-design-prompt.md` §4 and `docs/prototype.md`.
- #179, first checkbox (lesson-open gate): built (Decision D1).

**Out**
- Free-tier locks, the paywall and daily counters: #87 (Decision D9 names its extension points).
- "اسأل المساعد عن الدرس": #91 (Avatar entry points, PRD §9.1). "اسأل معلّم" from a lesson: #94 (thread creation with attached context).
- Teacher browsing of the published tree (PRD §16). It is already served by `GET /api/subjects{/id}` (`Content.Browse`, subject-scoped). No teacher UI is added.
- The other #179 checkboxes (expired-exam 409, race toast, sessions.md wording, ExamTimer size, CI flake). They stay open in #179.
- Landing page and onboarding: #86.

**Deferred**
- **Automated 375 px viewport test.** The repo has no browser runner: no Playwright, and no Vitest browser mode (`web/package.json`). Adding one is a tooling change beyond this story. In this story, 375 px is covered by the layout rules in "Mobile 375 px rules", which the reviewer checks line by line. Follow-up: add Playwright or Vitest browser mode and a 375 px no-horizontal-scroll check for `/student/subject/:id`, `/student/unit/:id` and `/student/lesson/:id`.

## Decisions
| # | Question | Decision | Why |
|---|---|---|---|
| D1 | Build the #179 lesson-open gate now? | **Yes.** Add lesson-open tracking (`LessonOpening`) plus `ExamsOptions.RequireAllLessonsOpened` (default `false`, so nothing changes by default). | PRD §7.4 names the gate as configurable and §19 Q4 leaves only the *default* open. The tracking belongs with the lesson page, which ships here, so the cost is one table, one command and one check. Deferring again would leave #179 with no owner. With the switch built, the product owner can answer §19 Q4 by configuration alone. |
| D2 | Where does the gate apply? | Unit-exam **start** (new session only; resuming an open exam is never blocked), multi-unit-exam **start** (every selected unit), and the unit-exam **overview** (`unopenedLessonCount`, 0 when off). Admin test mode is exempt. | PRD §7.5: "Same rules as unit exam otherwise". Without the multi-unit check, a 2-unit exam would bypass the gate. Admins cannot record openings (D4 policy), so the gate would lock them out of test mode. |
| D3 | What counts as "opened"? | The student loaded the lesson page, and the web posted `POST /api/browse/lessons/{id}/openings` once the lesson read succeeded. The gate counts **Published** lessons of the unit that have no opening row. A later-published lesson re-locks the gate until it is opened. | This is the simplest observable signal. A query must not write (CQRS), so recording is a separate command. |
| D4 | Policy for the new endpoints | All four use `DefaultCodes.ProgressViewOwn` (Student only). | Every response carries the caller's own mastery, like the existing `GET /api/mastery/subjects/{id}`, which uses the same policy. The opening is part of the student's own progress record. Teachers and admins get 403. `/student/*` web routes are Student-only (`requireRole`). |
| D5 | New endpoints, or extend `GET /api/mastery/subjects/{id}`? | New `BrowseController` at `api/browse`. The mastery endpoint is untouched. | The pages need the best score, the published-lesson count, the breadcrumb names, the lesson content and the neighbours, which the mastery shape does not have. Changing it would be a breaking change for no caller (web does not use it). |
| D6 | Next/previous across unit borders? | Yes. The sequence is the subject's units (Order, CreationDate, Id), then each unit's **published** lessons (Order, CreationDate, Id). The last lesson of a unit links to the first published lesson of the next unit that has one. At the end of the subject, "next" is null and the web shows "العودة إلى {unit}". | "Next-lesson navigation" is most useful when it does not dead-end at a unit border. Draft and archived lessons are invisible to students (PRD §5.2). |
| D7 | Units with no published lessons on the subject page | Listed with `lessonCount` 0 and 0 % mastery. The unit page shows its empty state. | This matches the prototype `vSubject` (`subjectUnits`, all units) and the progress page. |
| D8 | Lesson mastery line placement | Under the lesson `h1` on every tab: «إتقانك X٪ · أسئلة متاحة: N · شاهدت: M» plus a mastery bar. The practice tab shows only the size picker. | The prototype puts this line in the practice tab. Here it is a per-node mastery indicator, which is the story's point, and keeping it out of the practice tab avoids coupling the quiz feature to the browse query. Recorded in `docs/claude-design-prompt.md` §4. |
| D9 | What to expose for #87 (free tier) | Nothing lock-related. Lessons are returned in curriculum order. #87 will add `IsLocked` to `StudentLessonSummaryResult` and `StudentLessonResult`, a lock check to `GetStudentLessonHandler` and `StartQuizSession`, and a locked variant of `LessonListItem`. | The orchestrator asked for "only what #87 will need". The ordered list is what #87's "first lesson per unit" rule reads. |
| D10 | Race: two concurrent first openings | The unique index `IX_LessonOpenings_StudentId_LessonId` is mapped in `AppDbContext.SaveChangesAsync` to 409 `LESSON_ALREADY_OPENED`. The web never shows it (the call has no UI). A `useRef` guard stops the StrictMode double effect. | This mirrors the `QuestionMasteryPerStudentIndex` mapping. Handlers have no try/catch, and raw `ON CONFLICT` SQL would bypass the domain factory. |
| D11 | Is the opening audited? | No (`IAuditableCommand` not applied, `IAuditedEntity` not implemented). | The audit log covers content, validation, publishing, grants and exports (PRD §17 rule 13, design prompt §5 rule 12). Student reading activity is none of these. |
| D12 | Recording from the web | `useLessonOpening(lessonId, unitId)` runs `mutate` in a `useEffect` once `unitId` is known (after the lesson read succeeds), guarded by a `useRef` of the last recorded lessonId. On success it invalidates `getGetUnitExamOverviewQueryKey(unitId)`. | This is a write on view, not data fetching, so the react-feature §3 ban on fetching in `useEffect` does not apply. Invalidating the overview keeps the exam-start gate warning current. |
| D13 | Lesson tabs as routes or as a search param? | Routes. A new layout `/student/lesson/$lessonId` with children `/` (explanation), `/objectives`, `/summary` and the **existing** `/practice`. | The prototype uses `#/student/lesson/:id/:tab`, and the existing practice route keeps its URL. TanStack flat files make `lesson.$lessonId.practice.tsx` a child of `lesson.$lessonId.tsx` automatically. |
| D14 | `PracticePage` inside the layout | Remove its `h1` (the layout's `h1` is the lesson name; one `h1` per page) and delete the now-unused `quiz` key `practice.title`. | react-feature §15. Its tests still pass because they reach the page by URL. The MSW defaults in D15 serve the new layout requests. |
| D15 | Existing web tests that render `/student/lesson/.../practice` | Add default MSW handlers for `GET /api/browse/lessons/:id` and `POST .../openings` in `web/src/test/msw/server.ts`. Existing test files are not edited, except the listed additions. | `onUnhandledRequest: 'error'` would otherwise fail every existing practice test. |
| D16 | Breadcrumb component location | `StudentBreadcrumbs` in `features/browse`, exported from its barrel and used by `features/exam` ExamStartPage. `browse` never imports `exam` (it uses only the generated `getGetUnitExamOverviewQueryKey`). | Typed TanStack links per level, with no cycle between the barrels. |
| D17 | Lesson video | A link «شاهد فيديو الدرس» to `toSafeVideoUrl(videoUrl)` (content barrel export added), on the explanation tab. | This matches the admin `LessonPreview`. No embed provider is chosen yet. |
| D18 | Gate error type | `BusinessRuleViolationCoreException(ErrorCodes.ExamLessonsNotOpened)` → 400, thrown from the Application helper `ExamLessonGate`. There is no context dictionary. | It sits beside `UNIT_EXAM_NO_BLUEPRINT` (400). The overview already gives the UI the count. |

**Morabh reuse-first.** I searched `D:\Personal\Projects\Projects\Morabh\repos\apis` (Core, Application, Domain; terms: breadcrumb, opened/viewed/read-at, mark-as-read). Nothing covers curriculum browsing or view tracking. Every piece below is **new, with no Morabh equivalent**. The idempotent "mark once" command follows the shape of `Core/Core.Notifications/MarkNotificationAsRead/MarkNotificationAsReadHandler.cs` (guard user → load → no-op if already done → mutate → save). Within this repo, the plan mirrors `ReviewSessionOpening`, `GetSubjectMasteryHandler`/`SubjectMasteryResultGenerator`, `SubjectProgressResultGenerator` (best-score lookup) and the `QuestionMasteryPerStudentIndex` conflict mapping.

## Existing code touched
| File | Change |
|---|---|
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Under `// LESSONS` add `LessonAlreadyOpened = "LESSON_ALREADY_OPENED"`. Under `// EXAMS` add `ExamLessonsNotOpened = "EXAM_LESSONS_NOT_OPENED"`. |
| `api/Elmanhg.Application/Shared/Options/ExamsOptions.cs` | Add `public bool RequireAllLessonsOpened { get; set; }` (default `false`, no attribute). |
| `api/Elmanhg.Application/Exams/Shared/UnitExamOverviewResult.cs` | Add a trailing positional parameter `int UnopenedLessonCount` to `UnitExamOverviewResult`. |
| `api/Elmanhg.Application/Exams/Shared/UnitExamOverviewResultGenerator.cs` | `Generate(CurriculumUnit unit, Subject subject, ExamBlueprint? blueprint, IReadOnlyDictionary<QuestionType, int> available, Session? openExam, int unopenedLessonCount)`, passing it through to the result. |
| `api/Elmanhg.Application/Exams/GetUnitExamOverview/GetUnitExamOverviewHandler.cs` | Add constructor deps `ILessonRepository lessonRepository, ILessonOpeningRepository lessonOpeningRepository, IOptions<ExamsOptions> examsOptions` (before `ICurrentUserService`). After `openExam`: `var isTestMode = currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin);` `var unopened = examsOptions.Value.RequireAllLessonsOpened && !isTestMode ? await ExamLessonGate.CountUnopenedAsync(userId, [unit.Id], lessonRepository, lessonOpeningRepository, cancellationToken).ConfigureAwait(false) : 0;` Pass `unopened` to the generator. |
| `api/Elmanhg.Application/Exams/StartUnitExam/StartUnitExamHandler.cs` | Add constructor dep `ILessonOpeningRepository lessonOpeningRepository` (after `ILessonRepository`). In `StartAsync`, first lines: `var isTestMode = currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin);` `if (examsOptions.Value.RequireAllLessonsOpened && !isTestMode) { await ExamLessonGate.EnsureOpenedAsync(userId, [unit.Id], lessonRepository, lessonOpeningRepository, cancellationToken).ConfigureAwait(false); }` Then the existing blueprint resolution. Replace the inline claim check in the `Session.StartUnitExam(...)` call with `isTestMode`. |
| `api/Elmanhg.Application/Exams/StartMultiUnitExam/StartMultiUnitExamHandler.cs` | Add constructor dep `ILessonOpeningRepository lessonOpeningRepository` (after `ILessonRepository`). Inside `if (session is null)`: move `isTestMode` to the first line, then `if (examsOptions.Value.RequireAllLessonsOpened && !isTestMode) { await ExamLessonGate.EnsureOpenedAsync(userId, selection.Units.Select(x => x.Id).ToList(), lessonRepository, lessonOpeningRepository, cancellationToken).ConfigureAwait(false); }` and then `PlanAsync`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `using` stays (Lessons already imported). Add `public const string LessonOpeningPerStudentIndex = "IX_LessonOpenings_StudentId_LessonId";`. Add `public DbSet<LessonOpening> LessonOpenings { get; set; }` after `LessonObjectives`. In `SaveChangesAsync` add `catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: LessonOpeningPerStudentIndex }) { throw new ConflictCoreException(ErrorCodes.LessonAlreadyOpened, innerException: exception); }` (before the last catch). Call `ConfigureLessonOpenings(modelBuilder);` after `ConfigureLessons`. New method `ConfigureLessonOpenings`: `Id` `ValueGeneratedNever()`; `HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict)`; `HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict)`; `HasIndex(x => new { x.StudentId, x.LessonId }).IsUnique().HasDatabaseName(LessonOpeningPerStudentIndex)`. Global filter: `modelBuilder.Entity<LessonOpening>().HasQueryFilter(x => !x.IsDeleted);`. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<ILessonOpeningRepository, LessonOpeningRepository>();` after `ILessonRepository`. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add AddLessonOpenings -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | Two new keys (see Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | `"Exams"` gets `"RequireAllLessonsOpened": false`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `builder.UseSetting("Exams:RequireAllLessonsOpened", "false");` after the `Exams:AutoSubmitEnabled` line. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `twentyThird => twentyThird.Should().EndWith("_AddLessonOpenings")` (the accepted migration pattern). |
| `api/Elmanhg.Tests/Application/Features/Exams/GetUnitExamOverview/GetUnitExamOverviewHandlerTests.cs` | Constructor: add substitutes `ILessonRepository`, `ILessonOpeningRepository` and `Options.Create(new ExamsOptions())` (field `_examsOptions`, mutable in tests). Add the tests listed in the Test plan. |
| `api/Elmanhg.Tests/Application/Features/Exams/StartUnitExam/StartUnitExamHandlerTests.cs` | Constructor: add `ILessonOpeningRepository` substitute, with `FindAsync` stubbed like `_lessonRepository` over a `List<LessonOpening> _openings`. Add the listed tests. The existing `IOptions<ExamsOptions>` instance must allow setting `RequireAllLessonsOpened`. |
| `api/Elmanhg.Tests/Application/Features/Exams/StartMultiUnitExam/StartMultiUnitExamHandlerTests.cs` | Same constructor change. Add the listed tests. |
| `postman/elmanhg.postman_collection.json` | New folder `Browse` after `Mastery`: `Get student subject` GET `{{baseUrl}}/api/browse/subjects/{{subjectId}}`, `Get student unit` GET `/api/browse/units/{{unitId}}`, `Get student lesson` GET `/api/browse/lessons/{{lessonId}}`, `Record lesson opening` POST `/api/browse/lessons/{{lessonId}}/openings`, in this order, with Bearer `{{accessToken}}` as in `Mastery`. |
| `web/src/app/i18n.ts` | Import `browseLocales` from `@/features/browse/locales` and register namespace `browse` for `ar` and `en`. |
| `web/src/features/content/index.ts` | `export { toSafeVideoUrl } from './api/lessonValues';` |
| `web/src/features/mastery/components/SubjectMasteryCard.tsx` | `h3` content becomes `<Link to="/student/subject/$subjectId" params={{ subjectId: subject.subjectId }} className="rounded-sm text-text hover:text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden">{subject.name}</Link>`. |
| `web/src/features/mastery/api/invalidateMastery.ts` | Add `export const browseQueryPrefix = '/api/browse';` and include `first.startsWith(browseQueryPrefix)` in the predicate. |
| `web/src/features/progress/components/SubjectProgressCard.tsx` | The `h3` name becomes a `Link` to `/student/subject/$subjectId` (same classes as SubjectMasteryCard). |
| `web/src/features/progress/components/UnitProgressTable.tsx` | The unit-name cell becomes a `Link` to `/student/unit/$unitId` (the accent link classes already used in the exam cell). |
| `web/src/features/exam/pages/ExamStartPage.tsx` | Before the `h1`: `<StudentBreadcrumbs subject={{ id: data.subjectId, name: data.subjectName }} unit={{ id: data.unitId, name: data.unitName }} current={t('start.crumb')} />` (import from `@/features/browse`). |
| `web/src/features/exam/components/ExamStartActions.tsx` | New branch after the two `inProgress` branches and before the shortfall branch: `if (Number(overview.unopenedLessonCount) > 0)` → a `warningClassName` div with `<p className="text-ui text-text">{t('start.lessonsNotOpened', { count: Number(overview.unopenedLessonCount) })}</p>` and `<Link to="/student/unit/$unitId" params={{ unitId: overview.unitId }} className="rounded-sm text-ui text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden">{t('start.toUnit')}</Link>`, with no start button. |
| `web/src/features/exam/i18n/en.json`, `ar.json` | `start.crumb`, `start.lessonsNotOpened`, `start.toUnit` (strings below). |
| `web/src/features/quiz/pages/PracticePage.tsx` | Body becomes `return <PracticeStart lessonId={lessonId} />;` Remove `useTranslation` and the `h1`. |
| `web/src/features/quiz/pages/QuizResultPage.tsx` | After `NewPracticeButton`, when `lessonId !== null`: `<Button asChild variant="secondary" className="self-start"><Link to="/student/lesson/$lessonId" params={{ lessonId }}>{t('result.backToLesson')}</Link></Button>`. |
| `web/src/features/quiz/i18n/en.json`, `ar.json` | Remove `practice.title`. Add `result.backToLesson`. |
| `web/src/shared/i18n/en.json`, `ar.json` | `errors.EXAM_LESSONS_NOT_OPENED`, `errors.LESSON_ALREADY_OPENED`. |
| `web/src/styles/app.css` | `.rich-text` gets `overflow-wrap: anywhere;` (long words at 375 px). |
| `web/src/test/msw/server.ts` | Add the defaults `getGetStudentLessonMockHandler(studentLesson())` and `getRecordLessonOpeningMockHandler()`. |
| `web/src/test/examFixtures.ts` | `overview()` default gets `unopenedLessonCount: 0`. |
| `web/src/routeTree.gen.ts` | Regenerated (by the vite plugin in `npm run build`). |
| `web/src/shared/api/generated/**` | Regenerated with `npm --prefix web run gen:api`. Never hand-edited. |
| Test files modified (additions only) | `StudentHomePage.test.tsx`, `ProgressPage.test.tsx`, `UnitProgressTable.test.tsx`, `ExamStartPage.test.tsx`, `QuizResultPage.test.tsx`, `invalidateMastery.test.ts` (rows in the Test plan). `PracticePage.test.tsx` is **not** edited and must stay green. |
| `docs/exams.md` | Line 19: replace the "ships with its default…#85" sentence with the gate rule (D2, D3) and a link to `docs/browsing.md`. Step list: add "Lesson-open gate" before Resolution. Multi-unit **Start and resume**: add the gate sentence. API: `UnitExamOverviewResult` gets `unopenedLessonCount`. Error table: add `EXAM_LESSONS_NOT_OPENED` 400. Options: add the `Exams:RequireAllLessonsOpened` row (false; when true a student must have opened every published lesson of the unit, or of every selected unit, before a new exam starts). Student screens `/student/exam-start`: add the breadcrumb (الرئيسية › subject › unit › امتحان الوحدة) and the warning «افتح كل دروس الوحدة قبل الامتحان. متبقٍّ N درس.» with «إلى دروس الوحدة», and replace "Reached from the progress unit table until #85 adds the unit page." with "Reached from the unit page, the subject page and the progress unit table." |
| `docs/mastery.md` | Line 124: "Cards are not links until #85 adds subject pages." → "Each card's name links to `/student/subject/{id}` (`docs/browsing.md`)." Add that browse queries (`/api/browse*`) are also invalidated after a quiz answer or an exam submission. |
| `docs/progress.md` | Subjects bullet: the subject name links to `/student/subject/{id}` and the unit name to `/student/unit/{id}`. Drop "(the entry point until the unit page ships in #85)". |
| `docs/claude-design-prompt.md` | §4 Student lines 132–133 rewritten (text in "Docs text"). |
| `docs/prototype.md` | Walkthrough step 2: append "The product also has breadcrumbs on every level, previous/next lesson links across units and the mastery line under the lesson title; the prototype does not simulate them." |

## Files to create

### Domain
| # | Path | Type | Contract |
|---|---|---|---|
| 1 | `api/Elmanhg.Domain/Lessons/LessonOpening.cs` | `public class LessonOpening : Entity` (Core.DDD.Entities) in `Elmanhg.Domain.Lessons` | Props (private set): `Guid StudentId`, `Guid LessonId`, `DateTimeOffset OpenedAt`. `private LessonOpening(Guid id) : base(id) { }`. `public static LessonOpening Record(Guid studentId, Lesson lesson, DateTimeOffset openedAt)`: see Domain behaviour. |
| 2 | `api/Elmanhg.Domain/Lessons/ILessonOpeningRepository.cs` | `public interface ILessonOpeningRepository : IRepository<LessonOpening>` | `Task<bool> IsOpenedAsync(Guid studentId, Guid lessonId, CancellationToken cancellationToken);` |
| 3 | `api/Elmanhg.Domain/Lessons/LessonSequence.cs` | `public static class LessonSequence` + `public sealed record LessonNeighbours(Lesson? Previous, Lesson? Next);` (same file) | `public static List<Lesson> Order(IEnumerable<CurriculumUnit> units, IEnumerable<Lesson> lessons)` and `public static LessonNeighbours Neighbours(IReadOnlyList<Lesson> ordered, Guid lessonId)`. Bodies in Domain behaviour. |

### Infrastructure
| # | Path | Type | Contract |
|---|---|---|---|
| 4 | `api/Elmanhg.Infrastructure/Lessons/LessonOpeningRepository.cs` | `public class LessonOpeningRepository(AppDbContext context) : Repository<LessonOpening>(context), ILessonOpeningRepository` | `IsOpenedAsync` → `await _dbSet.AnyAsync(x => x.StudentId == studentId && x.LessonId == lessonId, cancellationToken).ConfigureAwait(false)`. |
| 5 | `api/Elmanhg.Infrastructure/Migrations/<ts>_AddLessonOpenings.cs` + `.Designer.cs` | generated | Creates `LessonOpenings` (Id uuid PK, StudentId, LessonId, OpenedAt timestamptz, IsDeleted bool), FKs Restrict, unique `IX_LessonOpenings_StudentId_LessonId`. It must contain no Drop or Rename operations. |

### Application: `Elmanhg.Application.Browse.*`
| # | Path | Type | Contract |
|---|---|---|---|
| 6 | `Browse/Shared/StudentSubjectResult.cs` | 2 records, client-facing (plain `string`, no LocalizedText) | `public sealed record StudentSubjectResult(Guid Id, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, List<StudentUnitSummaryResult> Units);` `public sealed record StudentUnitSummaryResult(Guid Id, string Name, int LessonCount, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, decimal? BestExamScorePercent);` |
| 7 | `Browse/Shared/StudentUnitResult.cs` | 2 records | `public sealed record StudentUnitResult(Guid Id, string Name, Guid SubjectId, string SubjectName, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, decimal? BestExamScorePercent, List<StudentLessonSummaryResult> Lessons);` `public sealed record StudentLessonSummaryResult(Guid Id, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent);` |
| 8 | `Browse/Shared/StudentLessonResult.cs` | 2 records | `public sealed record StudentLessonResult(Guid Id, string Name, Guid UnitId, string UnitName, Guid SubjectId, string SubjectName, string Explanation, string Summary, string? VideoUrl, List<LessonObjectiveResult> Objectives, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, LessonLinkResult? PreviousLesson, LessonLinkResult? NextLesson);` (reuses `Elmanhg.Application.Lessons.Shared.LessonObjectiveResult`) `public sealed record LessonLinkResult(Guid Id, string Name, Guid UnitId, string UnitName);` |
| 9 | `Browse/Shared/UnitBestScore.cs` | `public static class UnitBestScore` | `public static decimal? Find(IReadOnlyCollection<ExamBestScore> bests, Guid unitId)` → `bests.FirstOrDefault(x => x.ScopeKey == new UnitExamScope(unitId).ToKey())?.BestScorePercent`. |
| 10 | `Browse/Shared/StudentSubjectResultGenerator.cs` | static | `public static StudentSubjectResult Generate(Subject subject, IReadOnlyList<CurriculumUnit> units, IReadOnlyDictionary<Guid, int> lessonCounts, IReadOnlyCollection<LessonMasteryCount> counts, IReadOnlyCollection<ExamBestScore> bests)`. Subject totals come from `MasteryTotals.Of(counts)`. For each unit, in the given order: `MasteryTotals.Of(counts.Where(x => x.UnitId == unit.Id))`, `lessonCounts.GetValueOrDefault(unit.Id)` and `UnitBestScore.Find(bests, unit.Id)`. |
| 11 | `Browse/Shared/StudentUnitResultGenerator.cs` | static | `public static StudentUnitResult Generate(CurriculumUnit unit, Subject subject, IReadOnlyList<Lesson> lessons, IReadOnlyCollection<LessonMasteryCount> counts, IReadOnlyCollection<ExamBestScore> bests)`. Unit totals are `MasteryTotals.Of(counts.Where(x => x.UnitId == unit.Id))`. Lessons keep the given order. Per lesson: the count with the same `LessonId` gives `new MasteryTotals(c.ServableCount, c.MasteredCount, c.SeenCount)`; with no count, `new MasteryTotals(0, 0, 0)`. |
| 12 | `Browse/Shared/StudentLessonResultGenerator.cs` | static | `public static StudentLessonResult Generate(Lesson lesson, CurriculumUnit unit, Subject subject, IReadOnlyList<CurriculumUnit> units, IReadOnlyList<Lesson> publishedLessons, IReadOnlyCollection<LessonMasteryCount> counts)`. `ordered = LessonSequence.Order(units, publishedLessons)`, `neighbours = LessonSequence.Neighbours(ordered, lesson.Id)`. Each link is `new LessonLinkResult(x.Id, x.Name, x.UnitId, units.First(u => u.Id == x.UnitId).Name)`. Objectives are `lesson.Objectives.OrderBy(x => x.Order).Select(x => new LessonObjectiveResult(x.Id, x.Text, x.Order)).ToList()`. Totals come from the count for `lesson.Id`, else zeros. |
| 13 | `Browse/GetStudentSubject/GetStudentSubjectQuery.cs` | `public sealed record GetStudentSubjectQuery(Guid SubjectId) : IRequest<StudentSubjectResult>;` | |
| 14 | `Browse/GetStudentSubject/GetStudentSubjectValidator.cs` | `AbstractValidator<GetStudentSubjectQuery>` | `RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);` |
| 15 | `Browse/GetStudentSubject/GetStudentSubjectHandler.cs` | `(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ICurrentUserService currentUserService)` | 1. `UserId` null/default → `UnauthorizedCoreException(UserNotAuthenticated)`. 2. `subject = GetByIdAsync(request.SubjectId, asNoTracking: true)`; null → `NotFoundCoreException(SubjectNotFound)`. 3. `units = unitRepository.FindAsync(x => x.SubjectId == subject.Id, orderBy: q => q.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true)`. 4. `lessonCounts = lessonRepository.CountByUnitAsync(unitIds, publishedOnly: true)`. 5. `counts = questionMasteryRepository.GetLessonCountsAsync(userId, subject.Id)`. 6. `bests = sessionRepository.GetBestExamScoresAsync(userId)`. 7. `return StudentSubjectResultGenerator.Generate(...)`. |
| 16 | `Browse/GetStudentUnit/GetStudentUnitQuery.cs` | `public sealed record GetStudentUnitQuery(Guid UnitId) : IRequest<StudentUnitResult>;` | |
| 17 | `Browse/GetStudentUnit/GetStudentUnitValidator.cs` | | `RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);` |
| 18 | `Browse/GetStudentUnit/GetStudentUnitHandler.cs` | `(ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, ILessonRepository lessonRepository, IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ICurrentUserService currentUserService)` | 1. user guard. 2. `unit` null → `NotFoundCoreException(UnitNotFound)`. 3. `subject = GetByIdAsync(unit.SubjectId)`; null → `NotFoundCoreException(UnitNotFound)`. 4. `lessons = lessonRepository.FindAsync(x => x.UnitId == unit.Id && x.State == LessonState.Published, orderBy Order then CreationDate, asNoTracking: true)`. 5. `counts = GetLessonCountsAsync(userId, unit.SubjectId)`. 6. `bests = GetBestExamScoresAsync(userId)`. 7. generator. |
| 19 | `Browse/GetStudentLesson/GetStudentLessonQuery.cs` | `public sealed record GetStudentLessonQuery(Guid LessonId) : IRequest<StudentLessonResult>;` | |
| 20 | `Browse/GetStudentLesson/GetStudentLessonValidator.cs` | | `RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);` |
| 21 | `Browse/GetStudentLesson/GetStudentLessonHandler.cs` | `(ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IQuestionMasteryRepository questionMasteryRepository, ICurrentUserService currentUserService)` | 1. user guard. 2. `lesson = lessonRepository.GetWithObjectivesAsync(request.LessonId, asNoTracking: true)`; null **or** `State != LessonState.Published` → `NotFoundCoreException(LessonNotFound)`. 3. `unit` null → `NotFoundCoreException(LessonNotFound)`. 4. `subject` null → `NotFoundCoreException(LessonNotFound)`. 5. `units = FindAsync(x => x.SubjectId == subject.Id, orderBy Order then CreationDate, asNoTracking)`. 6. `lessons = lessonRepository.FindAsync(x => unitIds.Contains(x.UnitId) && x.State == LessonState.Published, asNoTracking: true)`. 7. `counts = GetLessonCountsAsync(userId, subject.Id)`. 8. generator. |
| 22 | `Browse/RecordLessonOpening/RecordLessonOpeningCommand.cs` | `public sealed record RecordLessonOpeningCommand(Guid LessonId) : IRequest;` (not `IAuditableCommand`, D11) | |
| 23 | `Browse/RecordLessonOpening/RecordLessonOpeningValidator.cs` | | `RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);` |
| 24 | `Browse/RecordLessonOpening/RecordLessonOpeningHandler.cs` | `(ILessonRepository lessonRepository, ILessonOpeningRepository lessonOpeningRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<RecordLessonOpeningCommand>` | 1. user guard. 2. `lesson = lessonRepository.GetByIdAsync(request.LessonId, asNoTracking: true)`; null or not Published → `NotFoundCoreException(LessonNotFound)`. 3. `if (await lessonOpeningRepository.IsOpenedAsync(userId, lesson.Id)) { return; }` 4. `var opening = LessonOpening.Record(userId, lesson, timeProvider.GetUtcNow());` 5. `AddAsync(opening)`. 6. `SaveChangesAsync` once. |

### Application: exam gate helper
| # | Path | Type | Contract |
|---|---|---|---|
| 25 | `api/Elmanhg.Application/Exams/Shared/ExamLessonGate.cs` | `public static class ExamLessonGate` | `public static async Task<int> CountUnopenedAsync(Guid studentId, IReadOnlyCollection<Guid> unitIds, ILessonRepository lessonRepository, ILessonOpeningRepository lessonOpeningRepository, CancellationToken cancellationToken)`: `lessons = lessonRepository.FindAsync(x => unitIds.Contains(x.UnitId) && x.State == LessonState.Published, cancellationToken, asNoTracking: true)`; `lessonIds` = their ids; `openings = lessonOpeningRepository.FindAsync(x => x.StudentId == studentId && lessonIds.Contains(x.LessonId), cancellationToken, asNoTracking: true)`; return `lessonIds.Count - openings.Select(x => x.LessonId).Distinct().Count()`. `public static async Task EnsureOpenedAsync(same params)`: `if (await CountUnopenedAsync(...) > 0) { throw new BusinessRuleViolationCoreException(ErrorCodes.ExamLessonsNotOpened); }` |

### API
| # | Path | Type | Contract |
|---|---|---|---|
| 26 | `api/Elmanhg.Api/Controllers/Browse/BrowseController.cs` | `[ApiController][Route("api/browse")][Authorize] public class BrowseController(IMediator mediator) : ControllerBase` | Actions in API surface. Each action has `[Authorize(Policy = DefaultCodes.ProgressViewOwn)]` and `[ProducesResponseType<T>(StatusCodes.Status200OK)]` (plain `ProducesResponseType(StatusCodes.Status200OK)` for the POST). Thin: send, then `Ok(result)` / `Ok()`. |

### api tests (files)
| # | Path |
|---|---|
| 27 | `api/Elmanhg.Tests/Domain/Lessons/LessonOpeningTests.cs` |
| 28 | `api/Elmanhg.Tests/Domain/Lessons/LessonSequenceTests.cs` |
| 29–36 | `api/Elmanhg.Tests/Application/Features/Browse/{GetStudentSubject,GetStudentUnit,GetStudentLesson,RecordLessonOpening}/{<Name>HandlerTests,<Name>ValidatorTests}.cs` (8 files) |
| 37 | `api/Elmanhg.Tests/Application/Features/Exams/Shared/ExamLessonGateTests.cs` |
| 38 | `api/Elmanhg.Tests/Integration/Browse/BrowseTestData.cs`: `public const string Route = "/api/browse";` plus helpers `GetAsync(HttpClient, string path)` → `JsonElement` (asserts 200), `OpenAsync(HttpClient, Guid lessonId)` → `HttpResponseMessage`, and `ReadOpeningsAsync(ApiFactory, Guid studentId)` → `List<LessonOpening>` through a fresh scope. |
| 39–42 | `api/Elmanhg.Tests/Integration/Browse/{StudentSubjectEndpointTests,StudentUnitEndpointTests,StudentLessonEndpointTests,LessonOpeningEndpointTests}.cs` |
| 43 | `api/Elmanhg.Tests/Integration/Exams/ExamLessonGateEndpointTests.cs`: private `WebApplicationFactory<Program> Gated() => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.PostConfigure<ExamsOptions>(o => o.RequireAllLessonsOpened = true)));`. Clients: `gated.CreateClient()`, with `Authorization` copied from `SignedInStudentAsync(factory)`'s client. |
| 44 | `api/Elmanhg.Tests/Integration/Persistence/LessonOpeningPersistenceTests.cs` |

### web: feature `web/src/features/browse/`
| # | Path | Contract |
|---|---|---|
| 45 | `index.ts` | Exports `SubjectPage`, `UnitPage`, `LessonPage`, `LessonExplanationTab`, `LessonObjectivesTab`, `LessonSummaryTab`, `StudentBreadcrumbs`. |
| 46 | `locales.ts` | `export const browseLocales = { ar, en };` (mirrors mastery). |
| 47–48 | `i18n/en.json`, `i18n/ar.json` | Exact content under "Web strings". |
| 49 | `api/lessonTabs.ts` | `export const lessonTabs = [{ key: 'explanation', to: '/student/lesson/$lessonId' }, { key: 'objectives', to: '/student/lesson/$lessonId/objectives' }, { key: 'summary', to: '/student/lesson/$lessonId/summary' }, { key: 'practice', to: '/student/lesson/$lessonId/practice' }] as const;` |
| 50 | `api/richText.ts` | `export function hasRichText(html: string): boolean { return /<img\b|data-latex=/i.test(html) \|\| html.replace(/<[^>]*>/g, '').replaceAll('&nbsp;', ' ').trim() !== ''; }` |
| 51 | `api/richText.test.ts` | See the Test plan. |
| 52 | `hooks/useLessonOpening.ts` | `export function useLessonOpening(lessonId: string, unitId: string \| undefined): void`. It uses the generated `useRecordLessonOpening({ mutation: { onSuccess: () => { if (unitId) { void queryClient.invalidateQueries({ queryKey: getGetUnitExamOverviewQueryKey(unitId) }); } } } })`, a `useRef<string \| null>(null)`, and `useEffect(() => { if (unitId === undefined \|\| recorded.current === lessonId) { return; } recorded.current = lessonId; mutate({ lessonId }); }, [lessonId, unitId, mutate]);`. No toast. |
| 53 | `components/StudentBreadcrumbs.tsx` | Props `{ subject?: { id: string; name: string }; unit?: { id: string; name: string }; current: string }`. `<nav aria-label={t('breadcrumb.label')}><ol className="flex flex-wrap gap-2 text-caption text-text-muted">`: `li` Link `/student` «الرئيسية»; `li` Link `/student/subject/$subjectId` when `subject`; `li` Link `/student/unit/$unitId` when `unit`; `<li aria-current="page" className="min-w-0 break-words">{current}</li>`. Links use `text-accent underline`. Separators come from CSS content, not text: each `li` except the first gets `before:me-2 before:content-['/']`. That is a Tailwind arbitrary *content* value, not a size, and allowed; if lint rejects it, use `<span aria-hidden>/</span>`. |
| 54 | `components/MasterySummary.tsx` | Props `{ name: string; percent: number; servable: number; seen: number }`. `<p className="text-caption text-text-muted">{t('mastery.line', { percent, servable, seen })}</p>` + `<MasteryBar percent={percent} label={t('mastery.barLabel', { name })} />` (from `@/features/mastery`). |
| 55 | `components/UnitListItem.tsx` | Props `{ unit: StudentUnitSummaryResult }`. `li` card (`rounded-md border border-border bg-surface px-3.5 py-3 shadow-1 flex flex-col gap-2`). Contents: `Link` to the unit page (`text-ui font-semibold break-words`, name); `MasteryBar`; caption `t('subject.unitMeta', { percent, lessons })`; caption best score (`t('subject.bestExam', { score: Math.round(n) })` or `t('subject.noExam')`); `Button asChild variant="secondary" size="sm"` Link to `/student/exam-start/$unitId` with visible text `t('subject.unitExam')` and `aria-label={t('subject.unitExamLabel', { name })}`. |
| 56 | `components/LessonListItem.tsx` | Props `{ lesson: StudentLessonSummaryResult }`. The same card style: `Link` to `/student/lesson/$lessonId` (name), `MasteryBar`, caption `t('unit.lessonMeta', { percent, questions: servableCount })`. |
| 57 | `components/UnitExamCard.tsx` | Props `{ unitId: string; bestExamScorePercent: StudentUnitResult['bestExamScorePercent'] }`. A card with `h2` `t('unit.examTitle')`, a line `t('unit.bestExam', { score: Math.round(Number(x)) })` or `t('unit.noExam')`, and `Button asChild variant="primary"` Link to `/student/exam-start/$unitId` `t('unit.openExam')`. |
| 58 | `components/LessonTabs.tsx` | Props `{ lessonId: string }`. `<nav aria-label={t('lesson.tabsLabel')}><ul className="flex flex-wrap gap-2">`, one `Link` per `lessonTabs` entry (`params={{ lessonId }}`, `activeOptions={{ exact: true }}`, `activeProps={{ className: 'border-text bg-text text-surface' }}`, `inactiveProps={{ className: 'border-border-strong bg-surface text-text hover:bg-soft' }}`), base class `inline-flex min-h-11 items-center rounded-pill border px-3.5 text-micro font-semibold focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden`, label `t('lesson.tabs.<key>')`. TanStack sets `aria-current="page"` on the active link. |
| 59 | `components/LessonNavigation.tsx` | Props `{ lesson: StudentLessonResult }`. `<nav aria-label={t('lesson.navLabel')} className="flex flex-col gap-2 md:flex-row md:justify-between">`. When there is a previous lesson: Link `/student/lesson/$lessonId` `t('lesson.previous', { name })`. Then either a next lesson: Link `t('lesson.next', { name })`, or Link `/student/unit/$unitId` (`lesson.unitId`) `t('lesson.backToUnit', { name: lesson.unitName })`. Links: `rounded-sm text-ui text-accent break-words focus-visible:ring-2 …`. Directional icons `ChevronRight`/`ChevronLeft` (lucide, `aria-hidden`, `rtl:rotate-180`). |
| 60 | `components/LessonRichText.tsx` | Props `{ html: string; emptyText: string }`. Card (`rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5`) with `RichTextViewer` (content barrel) when `hasRichText(html)`, else `<p className="text-ui text-text-muted">{emptyText}</p>`. |
| 61 | `pages/SubjectPage.tsx` | Props `{ subjectId: string }`. `useGetStudentSubject(subjectId)`. Error: `ContentErrorState title={t('subject.errorTitle')}` + retry. Pending: `ContentListSkeleton label={t('subject.loading')}`. Success, as a `section flex flex-col gap-4`: `StudentBreadcrumbs current={data.name}`, `h1` name, `MasterySummary`, `h2` `t('subject.unitsTitle')`, then an empty `<p className="text-ui text-text-muted">{t('subject.empty')}</p>` or `<ul className="flex flex-col gap-2">` of `UnitListItem`, then `Button asChild variant="secondary" className="self-start"` Link `to="/student/multi-exam" search={{ subjectId: data.id }}` `t('subject.multiExam')`. |
| 62 | `pages/UnitPage.tsx` | Props `{ unitId: string }`. `useGetStudentUnit(unitId)`. Error, loading and success as above. Success: `StudentBreadcrumbs subject={{ id: data.subjectId, name: data.subjectName }} current={data.name}`, `h1`, `MasterySummary`, `h2` `t('unit.lessonsTitle')`, the empty text `t('unit.empty')` or a `ul` of `LessonListItem`, then `UnitExamCard`. |
| 63 | `pages/LessonPage.tsx` | Props `{ lessonId: string }`. `const query = useGetStudentLesson(lessonId); useLessonOpening(lessonId, query.data?.unitId);`. Error: `ContentErrorState title={t('lesson.errorTitle')}`. Pending: skeleton `t('lesson.loading')`. Success: `StudentBreadcrumbs subject unit current={data.name}`, `h1 className="font-display text-h1 font-bold break-words lg:text-h1-desktop"`, `MasterySummary`, `LessonTabs`, `<Outlet />`, `LessonNavigation`. |
| 64 | `pages/LessonExplanationTab.tsx` | Props `{ lessonId }`. `const { data } = useGetStudentLesson(lessonId); if (!data) { return null; }`. `LessonRichText html={data.explanation} emptyText={t('lesson.noExplanation')}`, plus a video link when `toSafeVideoUrl(data.videoUrl ?? '')` is non-null (`target="_blank" rel="noopener noreferrer" dir="ltr"`, `t('lesson.video')`). |
| 65 | `pages/LessonObjectivesTab.tsx` | A card with `<ol className="list-decimal ps-6 flex flex-col gap-1">` of `data.objectives` (key `objective.id`), or `t('lesson.noObjectives')`. |
| 66 | `pages/LessonSummaryTab.tsx` | `LessonRichText html={data.summary} emptyText={t('lesson.noSummary')}`. |
| 67–69 | `pages/SubjectPage.test.tsx`, `pages/UnitPage.test.tsx`, `pages/LessonPage.test.tsx` | See the Test plan. |

### web: routes and test fixtures
| # | Path | Contract |
|---|---|---|
| 70 | `web/src/routes/student/subject.$subjectId.tsx` | `createFileRoute('/student/subject/$subjectId')`. The component reads `subjectId` and renders `<SubjectPage subjectId={subjectId} />` (mirrors `exam-start.$unitId.tsx`). |
| 71 | `web/src/routes/student/unit.$unitId.tsx` | `'/student/unit/$unitId'` → `UnitPage`. |
| 72 | `web/src/routes/student/lesson.$lessonId.tsx` | `'/student/lesson/$lessonId'` → `LessonPage` (the layout). |
| 73 | `web/src/routes/student/lesson.$lessonId.index.tsx` | `'/student/lesson/$lessonId/'` → `LessonExplanationTab`. |
| 74 | `web/src/routes/student/lesson.$lessonId.objectives.tsx` | `'/student/lesson/$lessonId/objectives'` → `LessonObjectivesTab`. |
| 75 | `web/src/routes/student/lesson.$lessonId.summary.tsx` | `'/student/lesson/$lessonId/summary'` → `LessonSummaryTab`. |
| 76 | `web/src/test/browseFixtures.ts` | Exports `browseSubjectId`, `browseUnitId`, `browseSecondUnitId`, `browseLessonId`, `browsePreviousLessonId`, `browseNextLessonId` (fixed UUID v4 strings), and `studentSubject(overrides?)`, `studentUnit(overrides?)`, `studentLesson(overrides?)` with these defaults. **Subject** "Physics": 40 %, servable 10, mastered 4, seen 6, units [Mechanics (id `browseUnitId`, 2 lessons, 50 %, servable 6, best 72.4), Waves (id `browseSecondUnitId`, 0 lessons, 0 %, servable 0, best null)]. **Unit** "Mechanics": subject Physics, 50 %, best 72.4, lessons [Forces (`browsePreviousLessonId`, 50 %, 4 questions), Energy (`browseLessonId`, 0 %, 2 questions)]. **Lesson** "Energy": unit Mechanics, subject Physics, explanation `<p>Energy is conserved.</p>`, summary `<p>Energy summary.</p>`, videoUrl null, objectives [{ id, text 'Define energy', order 1 }, { id, text 'Apply conservation', order 2 }], servable 2, mastered 1, seen 2, percent 50, previous { Forces, `browsePreviousLessonId`, unit Mechanics }, next { Wave basics, `browseNextLessonId`, unitId `browseSecondUnitId`, unitName Waves }. |

### Docs
| # | Path | Content |
|---|---|---|
| 77 | `docs/browsing.md` | Sections: **Purpose** (PRD §7.1 step 3–4, §5.2). **Endpoints** (the API surface table, policy `Progress.ViewOwn`, Students only; teachers and admins 403; anonymous 401). **Rules**: published lessons only (a draft or archived lesson is 404 `LESSON_NOT_FOUND`); order (Order, then CreationDate); mastery per node from `GetLessonCountsAsync`/`MasteryTotals`, as in `docs/mastery.md`; lesson counts are published lessons; best score as in `docs/progress.md`; the previous/next sequence across units (D6). **Result shapes** (the records above). **Lesson openings** (`POST …/openings`: idempotent; 404 for an unknown or unpublished lesson; a concurrent duplicate returns 409 `LESSON_ALREADY_OPENED`; not audited). **Unit-exam gate** (D1–D3, with a link to `docs/exams.md`). **Web** (routes, tabs, breadcrumbs, states, the opening call and its overview invalidation, the invalidation of `/api/browse*` after answers). **Free tier** (#87 hooks per D9). |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|---|---|---|---|---|
| `ErrorCodes.ExamLessonsNotOpened` (Application) | `EXAM_LESSONS_NOT_OPENED` | `ExamLessonGate.EnsureOpenedAsync` (StartUnitExam, StartMultiUnitExam) | `BusinessRuleViolationCoreException` | 400 |
| `ErrorCodes.LessonAlreadyOpened` (Application) | `LESSON_ALREADY_OPENED` | `AppDbContext.SaveChangesAsync` on `IX_LessonOpenings_StudentId_LessonId` | `ConflictCoreException` | 409 |
| `DomainErrorCodes.LessonNotPublished` (reused) | `LESSON_NOT_PUBLISHED` | `LessonOpening.Record` | `BusinessRuleViolationCoreException` | 400 (unreachable over HTTP; the handler 404s first) |
| Reused | `SUBJECT_NOT_FOUND`, `UNIT_NOT_FOUND`, `LESSON_NOT_FOUND`, `SUBJECT_ID_REQUIRED`, `UNIT_ID_REQUIRED`, `LESSON_ID_REQUIRED`, `USER_NOT_AUTHENTICATED` | browse handlers/validators | NotFound / validation / Unauthorized | 404 / 422 / 401 |

Resource strings (`Messages.*.resx`; `web/src/shared/i18n/*.json` `errors.*` use the same text):
- `EXAM_LESSONS_NOT_OPENED`: en "Open every lesson in the unit before starting the exam." · ar "افتح كل دروس الوحدة قبل بدء الامتحان."
- `LESSON_ALREADY_OPENED`: en "This lesson opening was already recorded." · ar "تم تسجيل فتح هذا الدرس من قبل."

## Domain behaviour
```csharp
public static LessonOpening Record(Guid studentId, Lesson lesson, DateTimeOffset openedAt)
{
    if (lesson.State != LessonState.Published)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.LessonNotPublished);
    }

    return new LessonOpening(Guid.NewGuid())
    {
        StudentId = studentId,
        LessonId = lesson.Id,
        OpenedAt = openedAt,
    };
}
```
`LessonOpening` has no mutating methods, so there is no `UpdationDate`: it extends `Entity`, like `ReviewSessionOpening`, and is never edited.

```csharp
// D6: curriculum order is unit order then lesson order; ties fall back to creation date then id so the sequence is stable.
public static List<Lesson> Order(IEnumerable<CurriculumUnit> units, IEnumerable<Lesson> lessons)
{
    var rank = units
        .OrderBy(x => x.Order)
        .ThenBy(x => x.CreationDate)
        .ThenBy(x => x.Id)
        .Select((unit, index) => (unit.Id, index))
        .ToDictionary(x => x.Id, x => x.index);
    return lessons
        .Where(x => rank.ContainsKey(x.UnitId))
        .OrderBy(x => rank[x.UnitId])
        .ThenBy(x => x.Order)
        .ThenBy(x => x.CreationDate)
        .ThenBy(x => x.Id)
        .ToList();
}

public static LessonNeighbours Neighbours(IReadOnlyList<Lesson> ordered, Guid lessonId)
{
    var index = ordered
        .Select((lesson, position) => (lesson.Id, position))
        .FirstOrDefault(x => x.Id == lessonId, (Guid.Empty, -1)).position;
    if (index < 0)
    {
        return new LessonNeighbours(null, null);
    }

    return new LessonNeighbours(index > 0 ? ordered[index - 1] : null, index < ordered.Count - 1 ? ordered[index + 1] : null);
}
```

## API surface
| Method | Route | Name (operationId) | Policy | Request | Response |
|---|---|---|---|---|---|
| GET | `/api/browse/subjects/{subjectId:guid}` | `GetStudentSubject` | `DefaultCodes.ProgressViewOwn` | route `subjectId` → `GetStudentSubjectQuery` | 200 `StudentSubjectResult` |
| GET | `/api/browse/units/{unitId:guid}` | `GetStudentUnit` | `ProgressViewOwn` | route `unitId` → `GetStudentUnitQuery` | 200 `StudentUnitResult` |
| GET | `/api/browse/lessons/{lessonId:guid}` | `GetStudentLesson` | `ProgressViewOwn` | route `lessonId` → `GetStudentLessonQuery` | 200 `StudentLessonResult` |
| POST | `/api/browse/lessons/{lessonId:guid}/openings` | `RecordLessonOpening` | `ProgressViewOwn` | route `lessonId` → `RecordLessonOpeningCommand` (no body) | 200 empty |
| GET | `/api/exams/units/{unitId}` (changed) | `GetUnitExamOverview` | unchanged `AssessmentsTake` | — | `UnitExamOverviewResult` + `unopenedLessonCount` |

No `Requests.cs` (no bodies).

## Web strings
`features/browse/i18n/en.json`:
```json
{
  "breadcrumb": { "label": "Breadcrumb", "home": "Home" },
  "mastery": {
    "line": "{percent, number}% mastered · {servable, plural, one {# question} other {# questions}} available · Seen {seen, number}",
    "barLabel": "{name} mastery"
  },
  "subject": {
    "loading": "Loading the subject…", "errorTitle": "Could not load the subject.", "unitsTitle": "Units",
    "empty": "This subject has no units yet.",
    "unitMeta": "{percent, number}% mastered · {lessons, plural, one {# lesson} other {# lessons}}",
    "bestExam": "Best exam score: {score, number} / 100", "noExam": "Best exam score: —",
    "unitExam": "Unit exam", "unitExamLabel": "Unit exam: {name}", "multiExam": "Multi-unit exam"
  },
  "unit": {
    "loading": "Loading the unit…", "errorTitle": "Could not load the unit.", "lessonsTitle": "Lessons",
    "empty": "This unit has no lessons yet.",
    "lessonMeta": "{percent, number}% mastered · {questions, plural, one {# question} other {# questions}}",
    "examTitle": "Unit exam", "bestExam": "Best score: {score, number} / 100",
    "noExam": "You have not taken this exam yet.", "openExam": "Open the unit exam"
  },
  "lesson": {
    "loading": "Loading the lesson…", "errorTitle": "Could not load the lesson.", "tabsLabel": "Lesson sections",
    "tabs": { "explanation": "Explanation", "objectives": "Objectives", "summary": "Summary", "practice": "Practice" },
    "noExplanation": "This lesson has no explanation yet.", "noObjectives": "This lesson has no objectives yet.",
    "noSummary": "This lesson has no summary yet.", "video": "Watch the lesson video",
    "navLabel": "Lesson navigation", "previous": "Previous lesson: {name}", "next": "Next lesson: {name}",
    "backToUnit": "Back to {name}"
  }
}
```
`ar.json`, with the same keys: breadcrumb `مسار التنقل` / `الرئيسية`; `mastery.line` `إتقانك {percent, number}٪ · أسئلة متاحة: {servable, number} · شاهدت: {seen, number}`; `mastery.barLabel` `إتقان {name}`; subject: `جارٍ تحميل المادة…`, `تعذّر تحميل المادة.`, `الوحدات`, `لا توجد وحدات في هذه المادة بعد.`, `إتقان {percent, number}٪ · {lessons, number} درس`, `أفضل درجة امتحان: {score, number} / 100`, `أفضل درجة امتحان: —`, `امتحان الوحدة`, `امتحان الوحدة: {name}`, `امتحان متعدد الوحدات`; unit: `جارٍ تحميل الوحدة…`, `تعذّر تحميل الوحدة.`, `الدروس`, `لا توجد دروس في هذه الوحدة بعد.`, `إتقان {percent, number}٪ · {questions, number} سؤال`, `امتحان الوحدة`, `أفضل درجة: {score, number} / 100`, `لم تمتحن هذه الوحدة بعد.`, `افتح امتحان الوحدة`; lesson: `جارٍ تحميل الدرس…`, `تعذّر تحميل الدرس.`, `أقسام الدرس`, tabs `الشرح`/`الأهداف`/`الملخص`/`التدريب`, `لا يوجد شرح لهذا الدرس بعد.`, `لا توجد أهداف لهذا الدرس بعد.`, `لا يوجد ملخص لهذا الدرس بعد.`, `شاهد فيديو الدرس`, `التنقل بين الدروس`, `الدرس السابق: {name}`, `الدرس التالي: {name}`, `العودة إلى {name}`.

Exam: `start.crumb` en "Unit exam" / ar "امتحان الوحدة"; `start.lessonsNotOpened` en "Open every lesson in this unit before the exam. {count, plural, one {# lesson} other {# lessons}} left." / ar "افتح كل دروس الوحدة قبل الامتحان. متبقٍّ {count, number} درس."; `start.toUnit` en "Go to the unit lessons" / ar "إلى دروس الوحدة". Quiz: `result.backToLesson` en "Back to the lesson" / ar "العودة للدرس".

## Mobile 375 px rules (reviewer checks each)
1. Pages are a single column below `md`. Only `LessonNavigation` switches to a row at `md`.
2. Breadcrumbs and tabs use `flex flex-wrap`. There is no fixed width anywhere in `features/browse`.
3. Every user-supplied name (subject, unit, lesson, link labels) has `break-words`. `.rich-text` has `overflow-wrap: anywhere`, and block math and `pre` keep their existing `overflow-x: auto`.
4. All actions are ≥44 px high (`min-h-11` tabs, `Button` defaults; `size="sm"` only on the per-unit exam link, which the design system allows).
5. There are no tables in the new pages.
6. Only logical properties (`ms/me/ps/pe`, `text-start`). Chevrons carry `rtl:rotate-180`.

## Docs text
`docs/claude-design-prompt.md` §4, replacing lines 132–133:
- `#/student/subject/:id` breadcrumb (الرئيسية › subject), subject mastery line and bar, units in order with mastery bar, published-lesson count, best unit-exam score («—» when none) and «امتحان الوحدة»; «امتحان متعدد الوحدات» opens the builder for this subject. `#/student/unit/:id` breadcrumb (الرئيسية › subject › unit), unit mastery, published lessons in order with mastery and question count (locked lessons for Free users arrive with #87), and a unit-exam card with the best score and a link to the exam start. Loading, empty, error-with-retry and RTL states on both.
- `#/student/lesson/:id` breadcrumb (الرئيسية › subject › unit › lesson), the lesson title with «إتقانك X٪ · أسئلة متاحة: N · شاهدت: M» and a mastery bar, pill tabs الشرح / الأهداف / الملخص / التدريب (each a route; التدريب is `/practice`: pick 5 / 10 / 20 and start), empty messages per tab, and previous/next lesson links that continue into the next unit («العودة إلى {unit}» at the end). Opening the page records the lesson as opened (used by the optional unit-exam gate). "اسأل المساعد عن الدرس" and "اسأل معلّم" arrive with the Avatar and Ask a Teacher stories.

## Test plan

### Domain (no doubles)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 1 | `LessonOpeningTests` | `Record_PublishedLesson_SetsStudentLessonAndTime` | StudentId, LessonId and OpenedAt equal the inputs; Id is not empty. |
| 2 | 〃 | `Record_DraftLesson_ThrowsLessonNotPublished` | `BusinessRuleViolationCoreException`, code `LESSON_NOT_PUBLISHED`. |
| 3 | 〃 | `Record_ArchivedLesson_ThrowsLessonNotPublished` | The same (publish, then archive). |
| 4 | `LessonSequenceTests` | `Order_LessonsAcrossUnits_OrdersByUnitThenLessonOrder` | Unit B (order 2) lessons come after unit A (order 1) lessons, whatever the input order. |
| 5 | 〃 | `Order_LessonOfUnknownUnit_IsExcluded` | A lesson whose UnitId is not in `units` is absent. |
| 6 | 〃 | `Neighbours_MiddleLesson_ReturnsPreviousAndNext` | Previous and next are the adjacent lessons in the same unit. |
| 7 | 〃 | `Neighbours_LastLessonOfUnit_NextIsFirstLessonOfNextUnit` | Next is the first lesson of the next unit. |
| 8 | 〃 | `Neighbours_FirstLessonOfSubject_HasNoPrevious` | Previous is null; next is set. |
| 9 | 〃 | `Neighbours_LastLessonOfSubject_HasNoNext` | Next is null. |
| 10 | 〃 | `Neighbours_LessonNotInSequence_ReturnsNoNeighbours` | Both are null. |

### Application (NSubstitute at repositories; FluentAssertions, as in the repo)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 11 | `GetStudentSubjectHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException` `USER_NOT_AUTHENTICATED`. |
| 12 | 〃 | `Handle_UnknownSubject_ThrowsSubjectNotFound` | `NotFoundCoreException` `SUBJECT_NOT_FOUND`. |
| 13 | 〃 | `Handle_Subject_ReturnsUnitsInOrderWithMasteryLessonCountAndBestScore` | Subject totals are weighted (e.g. 8/2/3 → 25 %). Units follow the repository order. The unit has `LessonCount` from `CountByUnitAsync`, its mastery, and `BestExamScorePercent` from the `unit:<id>` key. |
| 14 | 〃 | `Handle_UnitWithoutServableQuestionsOrExam_ReturnsZerosAndNullBest` | 0/0/0/0 %, `BestExamScorePercent` null, `LessonCount` 0. |
| 15 | `GetStudentSubjectValidatorTests` | `Validate_SubjectId_Passes` / `Validate_EmptySubjectId_FailsWithSubjectIdRequired` | Valid; error code `SUBJECT_ID_REQUIRED`. |
| 16 | `GetStudentUnitHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | 401 code. |
| 17 | 〃 | `Handle_UnknownUnit_ThrowsUnitNotFound` | `UNIT_NOT_FOUND`. |
| 18 | 〃 | `Handle_UnitOfMissingSubject_ThrowsUnitNotFound` | `UNIT_NOT_FOUND`. |
| 19 | 〃 | `Handle_Unit_ReturnsPublishedLessonsWithMastery` | Draft and archived lessons are excluded (the predicate is compiled over a list, as in `GetSubjectMasteryHandlerTests`). Per-lesson counts are right. A lesson with no count gives zeros. Unit totals, subject id/name and best score are right. |
| 20 | 〃 | `Handle_NoUnitExam_ReturnsNullBestScore` | Null. |
| 21 | `GetStudentUnitValidatorTests` | `Validate_UnitId_Passes` / `Validate_EmptyUnitId_FailsWithUnitIdRequired` | `UNIT_ID_REQUIRED`. |
| 22 | `GetStudentLessonHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | 401 code. |
| 23 | 〃 | `Handle_UnknownLesson_ThrowsLessonNotFound` | `LESSON_NOT_FOUND`. |
| 24 | 〃 | `Handle_DraftLesson_ThrowsLessonNotFound` | `LESSON_NOT_FOUND`. |
| 25 | 〃 | `Handle_ArchivedLesson_ThrowsLessonNotFound` | `LESSON_NOT_FOUND`. |
| 26 | 〃 | `Handle_LessonOfMissingUnit_ThrowsLessonNotFound` | `LESSON_NOT_FOUND`. |
| 27 | 〃 | `Handle_PublishedLesson_ReturnsContentObjectivesAndBreadcrumb` | Name, explanation, summary and videoUrl; objectives in order; UnitId/UnitName and SubjectId/SubjectName. |
| 28 | 〃 | `Handle_PublishedLesson_ReturnsLessonMastery` | Servable, mastered, seen and percent from the lesson's count. |
| 29 | 〃 | `Handle_LastLessonOfUnit_NextIsFirstLessonOfNextUnit` | `NextLesson` has the next unit's first published lesson id, name, UnitId and UnitName; `PreviousLesson` is the prior lesson. A draft in between is skipped. |
| 30 | 〃 | `Handle_OnlyPublishedLesson_HasNoNeighbours` | Both null. |
| 31 | `GetStudentLessonValidatorTests` | `Validate_LessonId_Passes` / `Validate_EmptyLessonId_FailsWithLessonIdRequired` | `LESSON_ID_REQUIRED`. |
| 32 | `RecordLessonOpeningHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | 401 code; `SaveChangesAsync` DidNotReceive. |
| 33 | 〃 | `Handle_UnknownLesson_ThrowsLessonNotFound` | 404 code; no save. |
| 34 | 〃 | `Handle_DraftLesson_ThrowsLessonNotFound` | 404 code; no save. |
| 35 | 〃 | `Handle_FirstOpening_AddsOpeningAndSaves` | `AddAsync` received one `LessonOpening` with StudentId, LessonId and OpenedAt = the TimeProvider value; `SaveChangesAsync` Received(1). |
| 36 | 〃 | `Handle_AlreadyOpened_DoesNotAddOrSave` | `AddAsync` and `SaveChangesAsync` DidNotReceive. |
| 37 | `RecordLessonOpeningValidatorTests` | `Validate_LessonId_Passes` / `Validate_EmptyLessonId_FailsWithLessonIdRequired` | `LESSON_ID_REQUIRED`. |
| 38 | `ExamLessonGateTests` | `CountUnopenedAsync_SomeLessonsOpened_ReturnsUnopenedCount` | 3 published and 1 opened → 2. A draft lesson and another student's opening are not counted. |
| 39 | 〃 | `CountUnopenedAsync_NoPublishedLessons_ReturnsZero` | 0. |
| 40 | 〃 | `EnsureOpenedAsync_AllOpened_DoesNotThrow` | Completes. |
| 41 | 〃 | `EnsureOpenedAsync_LessonUnopened_ThrowsExamLessonsNotOpened` | `BusinessRuleViolationCoreException` `EXAM_LESSONS_NOT_OPENED`. |
| 42 | `StartUnitExamHandlerTests` (add) | `Handle_GateOnLessonUnopened_ThrowsExamLessonsNotOpened` | Code; `AddAsync` and `SaveChangesAsync` DidNotReceive. |
| 43 | 〃 | `Handle_GateOnAllLessonsOpened_StartsExam` | Result kind UnitExam; `SaveChangesAsync` Received(1). |
| 44 | 〃 | `Handle_GateOnAdmin_StartsTestModeExamWithoutOpenings` | `IsTestMode` true, and the exam started with no openings. |
| 45 | 〃 | `Handle_GateOnOpenExamSameUnit_ResumesWithoutGate` | The open session is returned, with no throw and no openings. |
| 46 | `StartMultiUnitExamHandlerTests` (add) | `Handle_GateOnLessonUnopened_ThrowsExamLessonsNotOpened` | Code; no save. |
| 47 | 〃 | `Handle_GateOnAllLessonsOpened_StartsExam` | Started; `SaveChangesAsync` Received(1). |
| 48 | `GetUnitExamOverviewHandlerTests` (add) | `Handle_GateOff_ReturnsZeroUnopened` | `UnopenedLessonCount` 0 while there are unopened lessons. |
| 49 | 〃 | `Handle_GateOnLessonsUnopened_ReturnsUnopenedCount` | The count equals the unopened published lessons. |
| 50 | 〃 | `Handle_GateOnAdmin_ReturnsZeroUnopened` | 0. |

### Integration (Testcontainers; `ApiFactory`)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 51 | `StudentSubjectEndpointTests` | `Get_Anonymous_Returns401` | 401. |
| 52 | 〃 | `Get_Teacher_Returns403` | 403. |
| 53 | 〃 | `Get_UnknownSubject_Returns404` | 404 + `code` `SUBJECT_NOT_FOUND`. |
| 54 | 〃 | `Get_SubjectWithProgress_ReturnsUnitsWithMasteryLessonCountAndBestScore` | Seed 2 units: unit 1 has 2 published lessons and a draft, and a practised lesson; unit 2 is empty. Units come in order; `lessonCount` is 2 (the draft is excluded); mastery % is right; `bestExamScorePercent` is null (no exam). |
| 55 | `StudentUnitEndpointTests` | `Get_Teacher_Returns403` | 403. |
| 56 | 〃 | `Get_UnknownUnit_Returns404` | 404 `UNIT_NOT_FOUND`. |
| 57 | 〃 | `Get_Unit_ReturnsPublishedLessonsInOrderWithMastery` | Lesson ids in order, with the draft and archived lessons excluded; the practised lesson's percent; `subjectName`. |
| 58 | `StudentLessonEndpointTests` | `Get_Teacher_Returns403` | 403. |
| 59 | 〃 | `Get_DraftLesson_Returns404` | 404 `LESSON_NOT_FOUND`. |
| 60 | 〃 | `Get_PublishedLesson_ReturnsContentBreadcrumbAndNeighbours` | Objectives in order; unit and subject names; `nextLesson` is the first lesson of the next unit; `previousLesson` null for the first lesson. |
| 61 | `LessonOpeningEndpointTests` | `Post_Anonymous_Returns401` | 401. |
| 62 | 〃 | `Post_DraftLesson_Returns404` | 404 `LESSON_NOT_FOUND`; no opening row. |
| 63 | 〃 | `Post_PublishedLessonTwice_StoresOneOpening` | Both 200; `ReadOpeningsAsync` has exactly one row for (student, lesson). |
| 64 | `ExamLessonGateEndpointTests` | `Post_UnitExamGateOnLessonUnopened_Returns400ExamLessonsNotOpened` | 400 + code; no session row for the student. |
| 65 | 〃 | `Post_UnitExamGateOnLessonOpened_StartsExam` | After `POST …/openings`, the start returns 200 with kind `UnitExam`. |
| 66 | 〃 | `Get_OverviewGateOn_ReportsUnopenedLessonCount` | `unopenedLessonCount` 1 before opening and 0 after. |
| 67 | 〃 | `Get_OverviewGateOff_ReportsZeroUnopened` | Default factory; 0 with nothing opened. |
| 68 | 〃 | `Post_MultiUnitExamGateOnLessonUnopened_Returns400ExamLessonsNotOpened` | 400 + code (seed with `MultiUnitExamTestData`). |
| 69 | `LessonOpeningPersistenceTests` | `SaveChangesAsync_DuplicateOpening_ThrowsLessonAlreadyOpened` | A second `LessonOpening` for the same student and lesson, in a fresh context → `ConflictCoreException` `LESSON_ALREADY_OPENED`. |
| 70 | `AppDbContextTests` (modify) | `Migrate_FreshDatabase_LeavesNoPendingMigrations` | The list ends with `_AddLessonOpenings`. |

### Web (Vitest + Testing Library + MSW; `renderApp` at the route, `testSessions.student`)
| # | File | `it(...)` | Asserts |
|---|---|---|---|
| 71 | `browse/api/richText.test.ts` | `is false for empty or tag-only html` | `''`, `'<p></p>'`, `'<p>&nbsp;</p>'` → false. |
| 72 | 〃 | `is true for text content` | `'<p>Hi</p>'` → true. |
| 73 | 〃 | `is true for an image or a formula without text` | `<img src="x">`, `<span data-latex="x^2"></span>` → true. |
| 74 | `SubjectPage.test.tsx` | `shows a loading state then the subject mastery and its units in order` | status "Loading the subject…"; h1 "Physics"; "40% mastered · 10 questions available · Seen 6"; list items in order Mechanics, Waves. |
| 75 | 〃 | `links each unit to its unit page and its exam start` | Link "Mechanics" href `/student/unit/<id>`; link "Unit exam: Mechanics" href `/student/exam-start/<id>`. |
| 76 | 〃 | `shows the best exam score or a dash per unit` | "Best exam score: 72 / 100" and "Best exam score: —"; "50% mastered · 2 lessons". |
| 77 | 〃 | `links to the multi-unit exam builder for this subject` | Link "Multi-unit exam" href contains `/student/multi-exam` and the subject id. |
| 78 | 〃 | `shows breadcrumbs back to home` | nav "Breadcrumb": link "Home" `/student`; current item "Physics" has `aria-current="page"`. |
| 79 | 〃 | `shows the empty state when the subject has no units` | "This subject has no units yet." |
| 80 | 〃 | `shows the error state and retries` | 404 `SUBJECT_NOT_FOUND` → alert "Subject not found."; Retry → h1 "Physics". |
| 81 | 〃 | `renders right to left in Arabic` | `dir="rtl"`; "امتحان متعدد الوحدات". |
| 82 | 〃 | `has no axe violations` | axe clean. |
| 83 | `UnitPage.test.tsx` | `shows a loading state then the lessons in order with their mastery` | status "Loading the unit…"; Forces then Energy; "50% mastered · 4 questions". |
| 84 | 〃 | `links each lesson to its lesson page` | Link "Energy" href `/student/lesson/<browseLessonId>`. |
| 85 | 〃 | `shows breadcrumbs to home and the subject` | Link "Physics" → `/student/subject/<id>`; current "Mechanics". |
| 86 | 〃 | `shows the best unit exam score and links to the exam start` | "Best score: 72 / 100"; link "Open the unit exam" → `/student/exam-start/<id>`. |
| 87 | 〃 | `says the exam was not taken when there is no score` | "You have not taken this exam yet." |
| 88 | 〃 | `shows the empty state when the unit has no lessons` | "This unit has no lessons yet." |
| 89 | 〃 | `shows the error state and retries` | "Unit not found." then recovery. |
| 90 | 〃 | `renders right to left in Arabic` | rtl; "الدروس". |
| 91 | 〃 | `has no axe violations` | clean. |
| 92 | `LessonPage.test.tsx` | `shows a loading state then the lesson title, mastery line and explanation` | status "Loading the lesson…"; h1 "Energy"; "50% mastered · 2 questions available · Seen 2"; "Energy is conserved."; tab "Explanation" has `aria-current="page"`. |
| 93 | 〃 | `shows breadcrumbs to home, the subject and the unit` | Links Home, Physics (`/student/subject/…`), Mechanics (`/student/unit/…`); current "Energy". |
| 94 | 〃 | `switches to the objectives tab and lists the objectives in order` | Click "Objectives" → listitems "Define energy", "Apply conservation"; the tab is current. |
| 95 | 〃 | `shows the summary tab` | Click "Summary" → "Energy summary." |
| 96 | 〃 | `opens the practice tab with the question-count choices` | Route `/practice` → buttons "5 questions"/"10 questions"/"20 questions"; tab "Practice" current; exactly one `h1`. |
| 97 | 〃 | `shows empty messages when the lesson has no explanation, objectives or summary` | The three empty texts on their tabs (explanation `''`, objectives `[]`, summary `'<p></p>'`). |
| 98 | 〃 | `links the video when the lesson has one` | videoUrl `https://www.youtube.com/watch?v=x` → link "Watch the lesson video" with that href. |
| 99 | 〃 | `links to the previous and next lessons` | "Previous lesson: Forces" → `/student/lesson/<prev>`; "Next lesson: Wave basics" → `/student/lesson/<next>`. |
| 100 | 〃 | `links back to the unit when there is no next lesson` | `nextLesson: null` → link "Back to Mechanics" → `/student/unit/<id>`. |
| 101 | 〃 | `records the lesson opening once` | An MSW handler on `POST */api/browse/lessons/:lessonId/openings` collects `params.lessonId`. After the h1 appears and waiting for the handler, the list equals `[browseLessonId]`. The boundary call is the behaviour (no UI). |
| 102 | 〃 | `does not record an opening when the lesson fails to load` | GET 404 → alert shown; the collected list is empty. |
| 103 | 〃 | `shows the error state and retries` | "Lesson not found." then recovery. |
| 104 | 〃 | `renders right to left in Arabic` | rtl; tabs "الشرح", "التدريب". |
| 105 | 〃 | `has no axe violations` | clean (three distinct nav labels). |
| 106 | `StudentHomePage.test.tsx` (add) | `links each subject card to its subject page` | Link with the subject name → `/student/subject/<id>`. |
| 107 | `ProgressPage.test.tsx` (add) | `links each subject name to its subject page` | Link href. |
| 108 | `UnitProgressTable.test.tsx` (add) | `links each unit name to its unit page` | Link href `/student/unit/<id>`. |
| 109 | `ExamStartPage.test.tsx` (add) | `shows breadcrumbs to home, the subject and the unit` | Links Home/Physics/Mechanics; current "Unit exam". |
| 110 | 〃 (add) | `asks to open every lesson and hides the start button when lessons are unopened` | `overview({ unopenedLessonCount: 2 })` → "Open every lesson in this unit before the exam. 2 lessons left."; link "Go to the unit lessons" → `/student/unit/<id>`; no "Start exam" button. |
| 111 | `QuizResultPage.test.tsx` (add) | `links back to the lesson` | Link "Back to the lesson" → `/student/lesson/<quizLessonId>`. |
| 112 | `invalidateMastery.test.ts` (add) | `invalidates browse queries` | A query keyed `['/api/browse/lessons/x']` becomes invalidated (`isInvalidated` true); a key `['/api/other']` does not. |

Each new test must fail if its production line is removed (mutation-check it once).

## Definition of done
- [ ] `GET /api/browse/subjects|units|lessons/{id}` and `POST /api/browse/lessons/{id}/openings` exist with `ProgressViewOwn`. Teachers get 403 and anonymous callers 401.
- [ ] Student reads return Published lessons only. A draft or archived lesson id is 404 `LESSON_NOT_FOUND` on read and on opening.
- [ ] Per-node mastery (subject, unit, lesson) uses `GetLessonCountsAsync` + `MasteryTotals` (servable rule); no new mastery maths.
- [ ] Previous/next follow D6, including crossing to the next unit and skipping drafts.
- [ ] `LessonOpening` (Entity, `Record` guard), its repository `IsOpenedAsync`, the unique index, the 409 mapping, the global soft-delete filter and the migration `AddLessonOpenings` (no drops), with AppDbContextTests updated.
- [ ] `Exams:RequireAllLessonsOpened` (default false) is in `ExamsOptions`, `appsettings.example.json` and `ApiFactory`. The gate is enforced on new unit-exam and multi-unit-exam starts, never on resume, never for admins. The overview exposes `unopenedLessonCount`.
- [ ] Both new error codes are in `ErrorCodes`, both resx files and both web `errors` locales.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` are regenerated and committed; `routeTree.gen.ts` too.
- [ ] Postman has a `Browse` folder with the 4 requests in order.
- [ ] Web routes `/student/subject/$subjectId`, `/student/unit/$unitId`, and `/student/lesson/$lessonId` with `/`, `/objectives`, `/summary`, `/practice`, each with loading, empty, error-with-retry and RTL states.
- [ ] Breadcrumbs on the subject, unit, lesson and exam-start pages. Home subject cards, progress subject and unit names, and the quiz result all link into the tree.
- [ ] Exam start shows the unopened-lessons warning and no start button when `unopenedLessonCount > 0`.
- [ ] After a quiz answer, `invalidateMastery` also invalidates `/api/browse*`. A recorded opening invalidates that unit's exam overview.
- [ ] `PracticePage` has no `h1`; `quiz` `practice.title` removed; `PracticePage.test.tsx` unchanged and green.
- [ ] All 6 "Mobile 375 px rules" hold (reviewer walks the diff).
- [ ] No hard-coded user-visible strings; `en` and `ar` keys match; no physical-direction utilities; no raw colours or sizes.
- [ ] Every row of the Test plan exists with that name and passes. No existing test is edited except the listed additions and constructor updates.
- [ ] `dotnet build` has zero new warnings. `dotnet test api/ -c Release` is green with `appsettings.json` moved aside (CI parity). `dotnet format --verify-no-changes` is clean outside `core-libraries`.
- [ ] Web: `npm run typecheck`, `npm run lint`, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`, `npx vitest run --coverage` and `npm run gen:api` with no diff.
- [ ] Docs: `docs/browsing.md` created; `docs/exams.md`, `docs/mastery.md`, `docs/progress.md`, `docs/claude-design-prompt.md` §4 and `docs/prototype.md` updated as specified; no "until #85" text left in `/docs`.
