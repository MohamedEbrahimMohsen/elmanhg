# Plan — [E6.S3] Multi-unit exam builder (#82)

## Goal
A student (or an admin, in test mode) opens «امتحان متعدد الوحدات», picks a subject, ticks two or more of its units, and chooses 20, 40 or 60 questions. The screen shows the merged blueprint before they start: type counts, available questions, time, pass mark, how many questions come from each unit, and any shortfall. Start builds one exam. Each unit's blueprint (its own, else the subject default) is scaled in proportion to the chosen size, and questions are drawn from each unit's servable pool. If a unit runs short, the rest of that type is taken from the other selected units, not-mastered first. The exam is sat, auto-saved, auto-submitted and graded through the #81 sitting screens and endpoints, with no new sitting code. The result adds a per-unit breakdown and a retake link that reopens the builder with the same units and size. History names a multi-unit exam by its units.

## Scope
**In:**
- **Domain:**
  - `MultiUnitExamScope`, the allowed sizes, the proportional merge (`MultiUnitBlueprintMerge`) with its plan records, and `MultiUnitExamQuestionSelector` (spill-over across units).
  - `Session.StartMultiUnitExam` and `Session.GetExamUnitIds()`. `Session.Exam.cs` is refactored to share the exam construction.
  - `ExamBreakdown.ByUnit` and `ExamUnitShare`.
  - `ExamBlueprintShortfall.Describe` is made public, to be reused.
- **Application:**
  - 3 use cases: overview, preview and start or resume.
  - Shared selection input and validator, planner, and results.
  - `ExamSessionResult` gains `subjectId` and `unitBreakdown`. The loader handles both exam kinds.
  - Session history names multi-unit exams.
- **Api:** 3 actions on `ExamsController`, and one request record.
- **Web:**
  - The `/student/multi-exam` builder replaces the placeholder: subject, units, size, live preview, start.
  - Exam and result titles for multi-unit exams, the per-unit breakdown table, and the multi-unit retake link.
  - Invalidation of the new queries.
- **Docs, generated files and Postman:**
  - Docs: `docs/exams.md`, `docs/exam-blueprints.md`, `docs/sessions.md`, `docs/progress.md`, `docs/PRD.md` §7.5, `docs/claude-design-prompt.md`, `docs/prototype.md`.
  - Generated: OpenAPI, Orval and `routeTree.gen.ts`.
  - Postman.

**Out:**
- Best score and the attempts list (#83). Multi-unit sessions never feed a unit's best score; that is unchanged.
- Subject and unit page entry links (#85). The nav item «امتحان متعدد الوحدات» already exists.
- The free-tier paywall on exams (#87).
- The Avatar refusing to answer during an exam (#91).
- No migration: `Kind` is already a string column that accepts `MultiUnitExam`, `Scope` is jsonb and `ScopeKey` is unbounded text.
- No new config keys.

**Deferred:** none. Everything here runs offline.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Allowed sizes | **20, 40, 60** only (`MultiUnitExamSizes.All`), returned by the overview as `sizes`. `docs/claude-design-prompt.md` and `docs/prototype.md` say "10 / 20"; they are updated to 20 / 40 / 60. The prototype JS is not changed. | PRD §7.5 and the story both say 20/40/60. The PRD owns product rules. |
| 2 | Minimum units | 2 (`MultiUnitExamSizes.MinUnits`). No maximum: every id must be a live unit of the subject, and duplicates are refused. | PRD: "2+ units within one subject". The subject bounds the list. |
| 3 | Blueprint per unit | Each selected unit resolves through the existing `ExamBlueprintResolution.ForUnit`: its own blueprint, else the subject default. A unit that resolves to none returns 400 `MULTI_UNIT_EXAM_NO_BLUEPRINT` with context `units` (the unit names joined by `", "`). The builder disables such units. | PRD: "or uses the subject's default blueprint if units have none". When every unit falls back to the default, the merge is the default scaled to the size. One resolution rule, reused. |
| 4 | Merge weighting | Every (unit, type) cell with count `c` gets `c × size / Σ QuestionCount`, where the sum runs over the selected units' resolved blueprints. The floors are taken, and the leftover goes by **largest remainder**, ties broken by unit order and then `QuestionType` order. The per-type totals are summed across units (`MultiUnitExamPlan.TypeCounts`). | `docs/exam-blueprints.md` step 4 says "merges `GetTypeCounts()` proportionally, using `QuestionCount` as the denominator". The prototype `mergeBlueprints` sums the type counts and applies largest remainder. Keeping cells per unit preserves each unit's share, which the per-unit breakdown needs. |
| 5 | Unit order | Selected units are ordered by `CurriculumUnit.Order`, then `Id`. That order is used for tie-breaks, for the scope's `UnitIds` (which the result and history show) and for the stable order of items. | Deterministic and matches the curriculum. |
| 6 | Unit with a zero share | Allowed. A tiny blueprint next to a big one can round to 0 questions. That unit is ignored for the time limit and pass mark, and it does not appear in `unitBreakdown`. The preview shows its 0. | Guaranteeing a minimum per unit would break proportionality. The preview makes it visible before start. |
| 7 | Time limit | Only units with at least one allocated question count as contributing. If any contributing blueprint is untimed, the exam is untimed. Otherwise `ceil(Σ TimeLimit_u × n_u / QuestionCount_u)`, where `n_u` is the unit's allocated count, clamped to `[1, ExamBlueprints:MaxTimeLimitMinutes]`. | "Same rules as unit exam otherwise": the time comes from the blueprints, at each unit's minutes-per-question rate. The prototype's `size × 2` ignores the admin's configuration. |
| 8 | Pass mark | `round(Σ PassMark_u × n_u / size)`, rounding half away from zero, clamped to 1–100, over the contributing units. | A question-weighted average of the units' pass marks. |
| 9 | Difficulty mix | Each unit's part is drawn with **its own** blueprint's mix. The merged preview shows no mix (`difficultyMix: null`). | Mixes differ per unit; applying each where it was authored is exact. |
| 10 | Selection | 1. For each unit (in order), `ExamQuestionSelector.Select` runs on that unit's candidates with the unit's type counts, each capped at the unit's available count of that type, and the unit's mix.<br>2. For each merged type still short (missing = merged count − picked of that type), `ExamQuestionSelector.Select` runs on the union of every selected unit's not-yet-picked candidates, with a null mix.<br>3. The final order is a stable sort by `QuestionType`, then `Difficulty`, so ties keep unit order. | Reuses the #81 selector unchanged: not mastered first, random, seeded. The spill-over means only a shortfall of the **union** pool blocks the exam, matching the prototype (`shortfall(pool = servableWhere(inUnits(unitIds)), counts)`). |
| 11 | Shortfall check | At start: `MultiUnitExamPlan.EnsureServable(unionAvailableByType)` returns 400 `EXAM_SHORTFALL` with context `types` (the #81 code and format). The preview returns `isAvailable = false` for the same condition. | Same meaning and code as the unit exam. The builder hides Start when short. |
| 12 | Scope | `MultiUnitExamScope(Guid SubjectId, IReadOnlyList<Guid> UnitIds, int Size)`, stored as jsonb (`UnitIds` in unit order). The key is `units:{size}:{sorted lowercase ids joined by ","}`. | The key is independent of selection order. It includes the size, so #83 can group retakes of "the same" multi-unit exam by `ScopeKey`. |
| 13 | One open exam | Unchanged rule: one open exam of any kind. Start with the same key resumes, and submits first when past the deadline plus grace (the #81 path). A different key returns 409 `EXAM_ALREADY_IN_PROGRESS`. Subject and units are validated first, **then** the open exam is checked, **then** the exam is planned, so a resume never fails on a blueprint edited later. | Mirrors `StartUnitExamHandler`. The index `IX_Sessions_OneOpenExam` already covers races. |
| 14 | Builder when an exam is open | The overview returns `inProgressExam { sessionId, isThisUnit: false }` (`isThisUnit` is always false here). The builder shows «لديك امتحان جارٍ.» with a «استكمل الامتحان» link and **no Start button**. The selection and preview still work. | Same message and link as the unit start page (prototype `startExam` alert). Reuses `InProgressExamResult`. |
| 15 | Preview transport | `GET /api/exams/subjects/{subjectId}/multi-unit/preview?unitIds=a&unitIds=b&size=20`, bound as `[FromQuery] List<Guid>? unitIds, [FromQuery] int size`. The web uses the generated `usePreviewMultiUnitExam` query with `placeholderData: keepPreviousData`, enabled only with 2 or more units. | It is a read, so it should be a GET query and not an effect-driven mutation (react skill §3). Orval 8.38's fetch client explodes array query parameters (`explodeParameters`, checked in `node_modules/@orval/fetch`). |
| 16 | Shared input | `MultiUnitExamSelection(Guid SubjectId, List<Guid>? UnitIds, int Size)` with `MultiUnitExamSelectionValidator`. The preview query and the start command each wrap it and validate it with `SetValidator`, exactly like `ExamBlueprintInput`. | One rule set for both use cases. Follows the precedent. |
| 17 | Unit not in the subject | 404 `UNIT_NOT_FOUND`, the same as a missing unit. | "Not found in this subject". It reveals nothing more. |
| 18 | Per-unit breakdown | `ExamBreakdown.ByUnit` sums the per-lesson shares by the live `Lesson.UnitId`, ordered by position in the scope's unit list (units not in the list go last, by id). Only units with at least one placed item appear. It is computed for **both** kinds (a unit exam gives one row); the web shows the table only for `MultiUnitExam`. `ExamSessionResult.unitBreakdown` is empty while the exam is open. | Reuses the lesson placement rules (a deleted question or lesson is left out of the breakdown but still counts in the score). A uniform result shape. |
| 19 | Result fields | `ExamSessionResult` gains `subjectId: Guid?` (the unit's subject for a unit exam, the scope's subject for a multi-unit exam) and `unitBreakdown`. `units` lists every scope unit in scope order, with a null name when deleted. | Retake needs the subject; the title needs every unit. |
| 20 | Loader compatibility | `ExamSessionResultLoader` keeps loading units one at a time with `GetByIdAsync` (a loop over `session.GetExamUnitIds()`) and the subject with `GetByIdAsync`. | The existing handler tests stub exactly these calls, and they must stay green unedited. N is small. |
| 21 | Titles | Web: a unit exam keeps «امتحان: {unit}» and «نتيجة: {unit}». A multi-unit exam uses «امتحان متعدد: {unit}» and «نتيجة امتحان متعدد: {unit}», where `{unit}` is the unit names joined by `" + "`. History `scopeName` for a multi-unit exam is the found unit names in scope order joined by `" + "`, or null when none is found. | Prototype titles: `'امتحان متعدد: ' + names.join(' + ')`. The separator is language-neutral. This replaces the doc line "a multi-unit exam has no name". |
| 22 | Retake | The result's «إعادة الامتحان» for a multi-unit exam links to `/student/multi-exam` with search `{ subjectId, unitIds: units.map(unitId), size: items.length }`. The builder restores the selection from the URL. | Prototype `retake(id)` restarts with the same `unitIds` and `size`. The item count always equals the size, because start refuses a union shortfall. |
| 23 | Builder URL state | Search `{ subjectId?, unitIds?, size? }`, each `.optional().catch(undefined)` (Zod 4). The hook defaults to `unitIds = []`; `size` falls back to `overview.sizes[0]` when missing or not in `sizes`. Changing the subject clears `unitIds` and `size`. Unit toggles and size changes use `replace: true`. | Filters live in the URL (react skill §4). All-optional keeps `<Link to="/student/multi-exam">` in `navConfig` valid. |
| 24 | Admin | `IsTestMode = role == Admin`, as for unit exams. | `docs/exams.md` rule 6. |
| 25 | Time and pass mark precedence | They are fixed on the session at start (from the plan) and never change afterwards. | #81 rule 5. |
| 26 | Morabh reuse | None applies. Morabh has no exam, blueprint or apportioning code (searched `Morabh.Domain`, `Morabh.Application` and `Core` for apportion, largest remainder and proportional). Every new type is new, with no Morabh equivalent. The CQRS, validator and repository shapes follow the vendored `api/core-libraries` and the #81 slices. | Reuse-first rule. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Sessions/Session.Exam.cs` | Refactor. Extract `private static void EnsureExamQuestions(IReadOnlyList<Question> questions, Func<Question, bool> isServable)`, which holds the three checks now inline in `StartUnitExam` (no questions, not servable, duplicate; same codes and order). Extract `private static Session CreateExam(Guid studentId, SessionKind kind, string scope, string scopeKey, int? timeLimitMinutes, int passMark, IReadOnlyList<Question> questions, bool isTestMode, DateTimeOffset now)`, which holds the object initializer and the item creation now in `StartUnitExam`. Rename `IsServableInUnit` to `private static bool IsServableInUnits(Question question, IReadOnlySet<Guid> unitIds, IReadOnlyCollection<Lesson> lessons)` (`lesson.UnitId` in `unitIds`). `StartUnitExam` keeps its signature and behaviour: the blueprint guard, then `EnsureExamQuestions(questions, x => IsServableInUnits(x, new HashSet<Guid> { unit.Id }, lessons))`, then `CreateExam(..., SessionKind.UnitExam, scope.ToJson(), scope.ToKey(), blueprint.TimeLimitMinutes, blueprint.PassMark, ...)`. |
| `api/Elmanhg.Domain/ExamBlueprints/ExamBlueprintShortfall.cs` | Add `public static string Describe(IEnumerable<ExamTypeShortfall> shortfalls) => string.Join(", ", shortfalls.Select(x => $"{x.Type} {x.Available}/{x.Required}"));` |
| `api/Elmanhg.Domain/ExamBlueprints/ExamBlueprint.cs` | Delete the private `Describe`. Both throws call `ExamBlueprintShortfall.Describe(shortfalls)`. |
| `api/Elmanhg.Domain/Sessions/Exams/ExamShare.cs` | Add `public static decimal Percent(decimal score, int maxScore) => maxScore == 0 ? 0m : Math.Round(score * 100m / maxScore, PercentDecimals, MidpointRounding.AwayFromZero);` and make `ScorePercent => Percent(Score, MaxScore)`. |
| `api/Elmanhg.Domain/Sessions/Exams/ExamBreakdown.cs` | Add `ByUnit` (see Domain behaviour). |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | In `// EXAMS`, add `MultiUnitExamUnitsTooFew`, `MultiUnitExamUnitDuplicate`, `MultiUnitExamSizeInvalid` and `MultiUnitExamNoBlueprint` (values in Error codes). |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Add the 4 strings after `UNIT_EXAM_NO_BLUEPRINT`. |
| `api/Elmanhg.Application/Exams/Shared/ExamSessionResult.cs` | New signature: `ExamSessionResult(Guid Id, string Kind, bool IsTestMode, Guid? SubjectId, string? SubjectName, List<ExamUnitResult> Units, DateTimeOffset StartedAt, int? TimeLimitMinutes, DateTimeOffset? Deadline, DateTimeOffset ServerNow, int PassMark, DateTimeOffset? SubmittedAt, decimal? ScorePercent, bool? IsPassed, long ElapsedMilliseconds, List<ExamItemResult> Items, List<ExamLessonResult> Lessons, List<ExamUnitBreakdownResult> UnitBreakdown, List<ExamObjectiveResult> WeakestObjectives)`. |
| `api/Elmanhg.Application/Exams/Shared/ExamBreakdownResults.cs` | Add `public sealed record ExamUnitBreakdownResult(Guid UnitId, string? Name, int QuestionCount, int CorrectCount, decimal Score, int MaxScore, decimal ScorePercent);` |
| `api/Elmanhg.Application/Exams/Shared/ExamBreakdownResultGenerator.cs` | Add `public static List<ExamUnitBreakdownResult> Units(IEnumerable<ExamUnitShare> shares, IReadOnlyCollection<CurriculumUnit> units)` → `new ExamUnitBreakdownResult(x.UnitId, units.FirstOrDefault(u => u.Id == x.UnitId)?.Name, x.QuestionCount, x.CorrectCount, x.Score, x.MaxScore, x.ScorePercent)`. |
| `api/Elmanhg.Application/Exams/Shared/ExamSessionResultGenerator.cs` | `Generate(Session session, IReadOnlyCollection<QuestionRevision> revisions, Guid? subjectId, string? subjectName, List<ExamUnitResult> units, List<ExamLessonResult> lessons, List<ExamUnitBreakdownResult> unitBreakdown, List<ExamObjectiveResult> weakestObjectives, DateTimeOffset now, ILocalizer localizer)`. It passes the two new values through. |
| `api/Elmanhg.Application/Exams/Shared/ExamSessionResultLoader.cs` | Same public signature. Steps:<br>1. `var unitIds = session.GetExamUnitIds();`<br>2. Load each unit with `unitRepository.GetByIdAsync(id, ct, asNoTracking: true)` in a loop, keeping the non-null ones in `List<CurriculumUnit> units`.<br>3. `Guid? subjectId = session.Kind == SessionKind.MultiUnitExam ? MultiUnitExamScope.FromJson(session.Scope).SubjectId : units.FirstOrDefault()?.SubjectId;`<br>4. `subject = subjectId is null ? null : await subjectRepository.GetByIdAsync(subjectId.Value, ct, asNoTracking: true)`.<br>5. `unitResults = unitIds.Select(id => new ExamUnitResult(id, units.FirstOrDefault(x => x.Id == id)?.Name)).ToList()`.<br>6. While open: `Generate(..., subjectId, subject?.Name, unitResults, [], [], [], ...)`.<br>7. When submitted: as today, plus `var lessonShares = ExamBreakdown.ByLesson(...)`, then `var unitShares = ExamBreakdown.ByUnit(lessonShares, lessons.ToDictionary(x => x.Id, x => x.UnitId), unitIds)`, then `ExamBreakdownResultGenerator.Units(unitShares, units)`.<br>If the file passes about 100 lines, move `Place` and `Placement` unchanged into the new `ExamItemPlacements.cs` (Files #27). |
| `api/Elmanhg.Application/Progress/GetSessionHistory/GetSessionHistoryHandler.cs` | `unitIds` = `page.Items.Where(x => x.IsExam).SelectMany(x => x.GetExamUnitIds()).Distinct().ToList()`. |
| `api/Elmanhg.Application/Progress/Shared/SessionHistoryResultGenerator.cs` | The switch arm `SessionKind.UnitExam => ForUnit(session.GetExamUnitIds()[0], units)`. Add `SessionKind.MultiUnitExam => ForUnits(session.GetExamUnitIds(), units)`. `ForUnits` returns `(null, null, names)`, where `names` = the names of the found units in scope order joined by `" + "`, or null when none is found. |
| `api/Elmanhg.Api/Controllers/Exams/ExamsController.cs` | 3 actions (API surface). |
| `api/Elmanhg.Api/Controllers/Exams/Requests.cs` | Add `public sealed record StartMultiUnitExamRequest(List<Guid>? UnitIds, int Size);` |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Domain/Sessions/Exams/ExamBreakdownTests.cs` | **Add** 2 tests (Test plan). Existing tests untouched. |
| `api/Elmanhg.Tests/Application/Features/Exams/GetExamSession/GetExamSessionHandlerTests.cs` | **Add** 1 test. |
| `api/Elmanhg.Tests/Application/Features/Progress/GetSessionHistory/GetSessionHistoryHandlerTests.cs` | **Add** 1 test. |
| `api/Elmanhg.Tests/Integration/Progress/SessionHistoryEndpointTests.cs` | **Add** 1 test. |
| `postman/elmanhg.postman_collection.json` | Add the collection variable `secondUnitId`. In the "Exams" folder, after "Submit exam", add 4 requests in this order: "Get multi-unit exam overview" (GET `{{baseUrl}}/api/exams/subjects/{{subjectId}}/multi-unit`); "Preview multi-unit exam" (GET `.../multi-unit/preview?unitIds={{unitId}}&unitIds={{secondUnitId}}&size=20`); "Start multi-unit exam" (POST `.../multi-unit`, body `{ "unitIds": ["{{unitId}}", "{{secondUnitId}}"], "size": 20 }`, test sets `examSessionId` and `examQuestionId`); "Submit multi-unit exam" (POST `{{baseUrl}}/api/exams/{{examSessionId}}/submit`). Each has a `pm.test` on status 200. Append to the folder description: "Multi-unit: set subjectId, unitId and secondUnitId (two units of that subject with blueprints and 20+ servable questions together)." |
| `web/src/routes/student/multi-exam.tsx` | `createFileRoute('/student/multi-exam')({ validateSearch: multiExamSearchSchema, component: MultiExamBuilderPage })`, imported from `@/features/exam`. |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (build). |
| `web/src/shared/api/generated/**` | Regenerated (`npm --prefix web run gen:api`). |
| `web/src/features/exam/index.ts` | Also export `MultiExamBuilderPage` and `multiExamSearchSchema`, `type MultiExamSearch`. |
| `web/src/features/exam/api/examSession.ts` | Add `export const multiUnitExamKind = 'MultiUnitExam';`, `export function isMultiUnitExam(session: ExamSessionResult): boolean`, and `export function examUnitNames(session: ExamSessionResult): string` (the non-null names joined by `' + '`). Keep `unitIdOf`. |
| `web/src/features/exam/api/invalidateExamViews.ts` | Add `export const examSubjectQueryPrefix = '/api/exams/subjects/';`. The predicate matches either prefix. |
| `web/src/features/exam/components/ExamRunner.tsx` | Title: `t(isMultiUnitExam(session) ? 'exam.multiTitle' : 'exam.title', { unit: examUnitNames(session) })`. |
| `web/src/features/exam/pages/ExamResultPage.tsx` | Title: `result.multiTitle` or `result.title` with `{ unit: examUnitNames(data) }`. After `ExamLessonBreakdown`, render `<ExamUnitBreakdown units={data.unitBreakdown} />` only when `isMultiUnitExam(data)`. Retake: for a multi-unit exam with a non-null `data.subjectId`, `<Link to="/student/multi-exam" search={{ subjectId: data.subjectId, unitIds: data.units.map((u) => u.unitId), size: data.items.length }}>`; otherwise the existing unit link. |
| `web/src/features/exam/i18n/ar.json`, `en.json` | Keys under i18n (Files #45). |
| `web/src/shared/i18n/ar.json`, `en.json` | `errors`: add the 4 new codes, with the same text as the resx without the `{units}` tail (Error codes). |
| `web/src/test/examFixtures.ts` | `openExam` adds `subjectId: '90909090-9090-4909-8909-909090909090'` and `unitBreakdown: []`. Add the exports `examSecondUnitId = '45454545-4545-4454-8454-454545454545'`, `multiOverview(overrides?)`, `multiPreview(overrides?)` and `multiExam(items, overrides?)` (a submitted `MultiUnitExam` with 2 units and a 2-row `unitBreakdown`). |
| `web/src/features/exam/api/examSession.test.ts`, `api/invalidateExamViews.test.ts`, `pages/ExamPage.test.tsx`, `pages/ExamResultPage.test.tsx` | **Add** tests only (Test plan). |
| `docs/exams.md`, `docs/exam-blueprints.md`, `docs/sessions.md`, `docs/progress.md`, `docs/PRD.md`, `docs/claude-design-prompt.md`, `docs/prototype.md` | See Docs below. |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Sessions/MultiUnitExamScope.cs` | sealed record | `namespace Elmanhg.Domain.Sessions; public sealed record MultiUnitExamScope(Guid SubjectId, IReadOnlyList<Guid> UnitIds, int Size)`. `public string ToKey() => $"units:{Size}:{string.Join(",", UnitIds.Order().Select(x => x.ToString("D")))}";`. `ToJson()` and `static FromJson(string)` exactly like `UnitExamScope` (`QuestionJson.SerializerOptions`; null → `InvalidOperationException("Multi-unit exam scope is empty.")`). |
| 2 | `api/Elmanhg.Domain/Sessions/Session.MultiUnitExam.cs` | partial class | `StartMultiUnitExam` and `GetExamUnitIds` (Domain behaviour). |
| 3 | `api/Elmanhg.Domain/Sessions/Exams/MultiUnitExamSizes.cs` | static class | `public const int MinUnits = 2; public static readonly IReadOnlyList<int> All = [20, 40, 60]; public static bool IsAllowed(int size) => All.Contains(size);`. Add a one-line WHY comment on `All`: PRD §7.5 target sizes. |
| 4 | `api/Elmanhg.Domain/Sessions/Exams/MultiUnitExamPart.cs` | sealed record | `public sealed record MultiUnitExamPart(Guid UnitId, ExamBlueprint Blueprint);` |
| 5 | `api/Elmanhg.Domain/Sessions/Exams/MultiUnitExamUnitPlan.cs` | sealed record | `public sealed record MultiUnitExamUnitPlan(Guid UnitId, bool IsSubjectDefault, IReadOnlyList<ExamTypeCount> TypeCounts, ExamDifficultyMix? DifficultyMix) { public int QuestionCount => TypeCounts.Sum(x => x.Count); }` |
| 6 | `api/Elmanhg.Domain/Sessions/Exams/MultiUnitExamPlan.cs` | sealed record | `public sealed record MultiUnitExamPlan(IReadOnlyList<MultiUnitExamUnitPlan> Units, int? TimeLimitMinutes, int PassMark)`. `public List<ExamTypeCount> TypeCounts` = the units' type counts grouped by type, summed, keeping those above 0, ordered by type. `public int QuestionCount => Units.Sum(x => x.QuestionCount);`. `public void EnsureServable(IReadOnlyDictionary<QuestionType, int> servable)`: runs `ExamBlueprintShortfall.Find(TypeCounts, servable)`; when not empty it throws `BusinessRuleViolationCoreException(ErrorCodes.ExamShortfall, context: new Dictionary<string, object> { ["types"] = ExamBlueprintShortfall.Describe(shortfalls) })` (Domain `ErrorCodes`). |
| 7 | `api/Elmanhg.Domain/Sessions/Exams/MultiUnitBlueprintMerge.cs` | static class | `public static MultiUnitExamPlan Merge(IReadOnlyList<MultiUnitExamPart> parts, int size, int maxTimeLimitMinutes)` (Domain behaviour). |
| 8 | `api/Elmanhg.Domain/Sessions/Exams/MultiUnitExamQuestionSelector.cs` | static class | `public static List<Guid> Select(MultiUnitExamPlan plan, IReadOnlyDictionary<Guid, List<ExamCandidate>> candidatesByUnit, IReadOnlySet<Guid> masteredQuestionIds, Random random)` (Domain behaviour). |
| 9 | `api/Elmanhg.Domain/Sessions/Exams/ExamUnitShare.cs` | sealed record | `public sealed record ExamUnitShare(Guid UnitId, int QuestionCount, int CorrectCount, decimal Score, int MaxScore) { public decimal ScorePercent => ExamShare.Percent(Score, MaxScore); }` |
| 10 | `api/Elmanhg.Application/Exams/Shared/MultiUnitExamSelection.cs` | sealed record | `public sealed record MultiUnitExamSelection(Guid SubjectId, List<Guid>? UnitIds, int Size);` |
| 11 | `api/Elmanhg.Application/Exams/Shared/MultiUnitExamSelectionValidator.cs` | sealed class `AbstractValidator<MultiUnitExamSelection>` | 1. `RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);`<br>2. `RuleFor(x => x.UnitIds).Must(list => list is not null && list.Count >= MultiUnitExamSizes.MinUnits).WithErrorCode(ErrorCodes.MultiUnitExamUnitsTooFew).Must(list => list is null || list.Distinct().Count() == list.Count).WithErrorCode(ErrorCodes.MultiUnitExamUnitDuplicate);`<br>3. `RuleForEach(x => x.UnitIds).ValidateRequired(ErrorCodes.UnitIdRequired);` (the `Guid` overload)<br>4. `RuleFor(x => x.Size).Must(MultiUnitExamSizes.IsAllowed).WithErrorCode(ErrorCodes.MultiUnitExamSizeInvalid);` |
| 12 | `api/Elmanhg.Application/Exams/Shared/MultiUnitExamUnits.cs` | sealed record | `public sealed record MultiUnitExamUnits(Subject Subject, List<CurriculumUnit> Units);` (units ordered by `Order`, then `Id`). |
| 13 | `api/Elmanhg.Application/Exams/Shared/MultiUnitExamPlanner.cs` | static class | `LoadUnitsAsync(Guid subjectId, IReadOnlyCollection<Guid> unitIds, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, CancellationToken ct)` → `Task<MultiUnitExamUnits>`:<br>1. `GetByIdAsync(subjectId, asNoTracking: true)`; null → `NotFoundCoreException(ErrorCodes.SubjectNotFound)`.<br>2. `unitRepository.FindAsync(x => x.SubjectId == subjectId && unitIds.Contains(x.Id), ct, asNoTracking: true)`.<br>3. If the count differs from `unitIds.Distinct().Count()` → `NotFoundCoreException(ErrorCodes.UnitNotFound)`.<br>4. Order by `Order`, then `Id`.<br><br>`PlanAsync(MultiUnitExamUnits selection, int size, IExamBlueprintRepository examBlueprintRepository, int maxTimeLimitMinutes, CancellationToken ct)` → `Task<MultiUnitExamPlan>`:<br>1. `FindAsync(x => x.SubjectId == selection.Subject.Id, ct, asNoTracking: true)`.<br>2. For each unit, `ExamBlueprintResolution.ForUnit(blueprints, unit)`.<br>3. Any null → `BadRequestCoreException(ErrorCodes.MultiUnitExamNoBlueprint, context: new Dictionary<string, object> { ["units"] = string.Join(", ", missing.Select(x => x.Name)) })`.<br>4. `MultiUnitBlueprintMerge.Merge(parts, size, maxTimeLimitMinutes)`. |
| 14 | `api/Elmanhg.Application/Exams/Shared/MultiUnitExamOverviewResult.cs` | sealed records | `MultiUnitExamOverviewResult(Guid SubjectId, string SubjectName, List<MultiUnitExamUnitOptionResult> Units, List<int> Sizes, InProgressExamResult? InProgressExam)`; `MultiUnitExamUnitOptionResult(Guid UnitId, string Name, bool HasBlueprint, bool IsSubjectDefault, int ServableCount)`. Client-facing; the names are content (not `.Localized()`). |
| 15 | `api/Elmanhg.Application/Exams/Shared/MultiUnitExamOverviewResultGenerator.cs` | static class | `Generate(Subject subject, IReadOnlyList<CurriculumUnit> units, IReadOnlyCollection<ExamBlueprint> blueprints, IReadOnlyCollection<ServableQuestionCount> counts, Session? openExam)`. For each unit: `resolved = ExamBlueprintResolution.ForUnit(blueprints, unit)`; `HasBlueprint = resolved is not null`; `IsSubjectDefault = resolved?.IsSubjectDefault ?? false`; `ServableCount = counts.Where(x => x.UnitId == unit.Id).Sum(x => x.Count)`. `Sizes = [.. MultiUnitExamSizes.All]`. `InProgressExam = openExam is null ? null : new(openExam.Id, false)`. |
| 16 | `api/Elmanhg.Application/Exams/Shared/MultiUnitExamPreviewResult.cs` | sealed records | `MultiUnitExamPreviewResult(Guid SubjectId, int Size, ExamBlueprintSummaryResult Blueprint, bool IsAvailable, List<MultiUnitExamUnitShareResult> Units)`; `MultiUnitExamUnitShareResult(Guid UnitId, string Name, int QuestionCount, bool IsSubjectDefault)`. |
| 17 | `api/Elmanhg.Application/Exams/Shared/MultiUnitExamPreviewResultGenerator.cs` | static class | `Generate(MultiUnitExamUnits selection, MultiUnitExamPlan plan, int size, IReadOnlyDictionary<QuestionType, int> available)`:<br>`typeCounts = plan.TypeCounts.Select(x => new ExamTypeAvailabilityResult(x.Type, x.Count, available.GetValueOrDefault(x.Type)))`;<br>`Blueprint = new ExamBlueprintSummaryResult(plan.Units.All(x => x.IsSubjectDefault), plan.QuestionCount, typeCounts, null, plan.TimeLimitMinutes, plan.PassMark)`;<br>`IsAvailable = ExamBlueprintShortfall.Find(plan.TypeCounts, available).Count == 0`;<br>`Units` = one per unit plan, with the name from `selection.Units`. |
| 18 | `api/Elmanhg.Application/Exams/GetMultiUnitExamOverview/GetMultiUnitExamOverviewQuery.cs` | sealed record | `GetMultiUnitExamOverviewQuery(Guid SubjectId) : IRequest<MultiUnitExamOverviewResult>` |
| 19 | `…/GetMultiUnitExamOverview/GetMultiUnitExamOverviewValidator.cs` | validator | `RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);` |
| 20 | `…/GetMultiUnitExamOverview/GetMultiUnitExamOverviewHandler.cs` | handler | Constructor: `(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionRepository questionRepository, ISessionRepository sessionRepository, ICurrentUserService currentUserService)`. Handle:<br>1. No user → `UnauthorizedCoreException(USER_NOT_AUTHENTICATED)`.<br>2. Subject by id; null → `NotFoundCoreException(SUBJECT_NOT_FOUND)`.<br>3. Units: `FindAsync(x => x.SubjectId == id, asNoTracking)`, then order by `Order`, `Id`.<br>4. Blueprints: `FindAsync(x => x.SubjectId == id, asNoTracking)`.<br>5. `counts = CountServableByUnitAndTypeAsync(id)`.<br>6. Open exam: `FirstOrDefaultAsync(x => x.StudentId == userId && x.Kind != SessionKind.Quiz && x.SubmittedAt == null, asNoTracking: true)`.<br>7. Return the generator's result. |
| 21 | `api/Elmanhg.Application/Exams/PreviewMultiUnitExam/PreviewMultiUnitExamQuery.cs` | sealed record | `PreviewMultiUnitExamQuery(MultiUnitExamSelection Selection) : IRequest<MultiUnitExamPreviewResult>` |
| 22 | `…/PreviewMultiUnitExam/PreviewMultiUnitExamValidator.cs` | validator | `RuleFor(x => x.Selection).NotNull().WithErrorCode(ErrorCodes.MultiUnitExamUnitsTooFew).SetValidator(new MultiUnitExamSelectionValidator());` |
| 23 | `…/PreviewMultiUnitExam/PreviewMultiUnitExamHandler.cs` | handler | Constructor: `(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionRepository questionRepository, IOptions<ExamBlueprintsOptions> examBlueprintsOptions, ICurrentUserService currentUserService)`. Handle:<br>1. Auth check.<br>2. `selection = await MultiUnitExamPlanner.LoadUnitsAsync(...)`.<br>3. `plan = await MultiUnitExamPlanner.PlanAsync(selection, request.Selection.Size, ..., examBlueprintsOptions.Value.MaxTimeLimitMinutes, ct)`.<br>4. `counts = CountServableByUnitAndTypeAsync(subjectId)`, then `available` = the counts of the selected unit ids grouped by type and summed.<br>5. Return the generator's result. No save. |
| 24 | `api/Elmanhg.Application/Exams/StartMultiUnitExam/StartMultiUnitExamCommand.cs` | sealed record | `StartMultiUnitExamCommand(MultiUnitExamSelection Selection) : IRequest<ExamSessionResult>` (not audited, like `StartUnitExamCommand`). |
| 25 | `…/StartMultiUnitExam/StartMultiUnitExamValidator.cs` | validator | Same as #22. |
| 26 | `…/StartMultiUnitExam/StartMultiUnitExamHandler.cs` | handler | Constructor: `(ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, IQuestionRepository questionRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionMasteryRepository questionMasteryRepository, IOptions<ExamsOptions> examsOptions, IOptions<ExamBlueprintsOptions> examBlueprintsOptions, IOptions<MasteryOptions> masteryOptions, Random random, TimeProvider timeProvider, ICurrentUserService currentUserService, ILocalizer localizer)`. Handle:<br>1. Auth check; `userId`, `now = timeProvider.GetUtcNow()`, `threshold`.<br>2. `selection = LoadUnitsAsync(...)` (404s).<br>3. `key = new MultiUnitExamScope(subjectId, selection.Units.Select(x => x.Id).ToList(), size).ToKey()`.<br>4. `open` = the open exam of any kind for the user, with Items and Attempts included (`AsSplitQuery`), tracked. `open is not null && open.ScopeKey != key` → `ConflictCoreException(EXAM_ALREADY_IN_PROGRESS)`.<br>5. `open is null`: `plan = PlanAsync(...)`; `session = await MultiUnitExamDraw.StartAsync(userId, selection, plan, size, isTestMode: currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin), now, lessonRepository, questionRepository, questionMasteryRepository, random, ct)`; `AddAsync(session)`.<br>6. Otherwise `revisions`; past the deadline → `ExamSubmission.SubmitAsync(...)`; else `open.Resume()`.<br>7. `revisions = GetRevisionsAsync(session item ids)`, as in `StartUnitExamHandler`.<br>8. `SaveChangesAsync`.<br>9. Return `ExamSessionResultLoader.LoadAsync(...)`. |
| 27 | `api/Elmanhg.Application/Exams/Shared/ExamItemPlacements.cs` | static class | Only if the loader passes about 100 lines: `public static List<ExamItemPlacement> Place(IEnumerable<Question> questions, IReadOnlyCollection<Lesson> lessons)`, moved verbatim from the loader. |
| 28 | `api/Elmanhg.Application/Exams/StartMultiUnitExam/MultiUnitExamDraw.cs` | static class | `public static async Task<Session> StartAsync(Guid userId, MultiUnitExamUnits selection, MultiUnitExamPlan plan, int size, bool isTestMode, DateTimeOffset now, ILessonRepository lessonRepository, IQuestionRepository questionRepository, IQuestionMasteryRepository questionMasteryRepository, Random random, CancellationToken ct)`:<br>1. `candidatesByUnit`: for each unit, `await questionRepository.GetServableExamCandidatesAsync([unit.Id], ct)`.<br>2. `available` = the union of the candidates grouped by type and counted; `plan.EnsureServable(available)`.<br>3. `mastered` = the `questionMasteryRepository.FindAsync(x => x.StudentId == userId && x.IsMastered && candidateIds.Contains(x.QuestionId), asNoTracking)` ids, as a set.<br>4. `selectedIds = MultiUnitExamQuestionSelector.Select(plan, candidatesByUnit, mastered, random)`.<br>5. Load the questions, keeping the `selectedIds` order (as in `StartUnitExamHandler.DrawAsync`).<br>6. Load the lessons of those questions.<br>7. `return Session.StartMultiUnitExam(userId, selection.Subject.Id, selection.Units, plan, size, questions, lessons, isTestMode, now);` |
| 29 | `api/Elmanhg.Tests/Builders/MultiUnitExamBuilder.cs` | test builder | `Subject` ("Physics"); `Units` = [`CurriculumUnit.Create(Subject, "Mechanics", 1, …)`, `CurriculumUnit.Create(Subject, "Waves", 2, …)`]; `Lessons` = one lesson per unit (`Lesson.Create(unit, name, 1, …)` then `Publish(…)`); `StudentId`; `Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)`. Methods:<br>`Question Approved(int unitIndex)` (Mcq, `QuestionBuilder.McqContent()`, Medium, approved through `TeacherSubject.Create(teacher, Subject, …)`);<br>`ExamBlueprint UnitBlueprint(int unitIndex, int? timeLimitMinutes, int passMark, params ExamTypeCount[] counts)`;<br>`ExamBlueprint DefaultBlueprint(int? timeLimitMinutes, int passMark, params ExamTypeCount[] counts)`;<br>`Session Build(int perUnit = 10, int size = 20, int? timeLimitMinutes = 30, bool isTestMode = false)` (Mcq `perUnit` blueprint per unit, merge, `perUnit` questions per unit, `Session.StartMultiUnitExam`). Everything goes through domain factories. |
| 30 | `api/Elmanhg.Tests/Integration/Exams/MultiUnitExamTestData.cs` | static helper | `SeedMultiUnitSubjectAsync(ApiFactory factory, int[] mcqPerUnit, int? timeLimitMinutes = 30, int passMark = 50, bool unitBlueprints = true, bool subjectDefault = false)` → `Task<(Guid SubjectId, List<Guid> UnitIds, List<List<Guid>> QuestionIds)>`. It seeds the subject, units "Mechanics", "Waves", … (order i+1), one published lesson per unit, `mcqPerUnit[i]` approved Mcq questions, per-unit blueprints (Mcq = `mcqPerUnit[i]`) and optionally a subject default (Mcq 10). Uses `ContentTestData`, `QuestionTestData` and `ExamBlueprint.CreateForUnit` / `CreateForSubject` with `ExamBlueprintBuilder.Plenty()`. `StartMultiAsync(HttpClient client, Guid subjectId, IEnumerable<Guid> unitIds, int size)` → `Task<HttpResponseMessage>`. `PreviewUrl(Guid subjectId, IEnumerable<Guid> unitIds, int size)` → string with repeated `unitIds=`. |
| 31 | `api/Elmanhg.Tests/…` test classes | tests | Listed in the Test plan. |
| 32 | `web/src/features/exam/schemas/multiExamSearchSchema.ts` | zod | `export const multiExamSearchSchema = z.object({ subjectId: z.uuid().optional().catch(undefined), unitIds: z.array(z.uuid()).optional().catch(undefined), size: z.number().int().optional().catch(undefined) }); export type MultiExamSearch = z.infer<typeof multiExamSearchSchema>;` |
| 33 | `web/src/features/exam/hooks/useMultiExamSearch.ts` | hook | `getRouteApi('/student/multi-exam')`. Returns `{ subjectId: string \| undefined; unitIds: string[]; size: number \| undefined; selectSubject(id: string): void; toggleUnit(id: string, checked: boolean): void; selectSize(size: number): void }`. `selectSubject` navigates with `search: { subjectId: id }`. `toggleUnit` and `selectSize` navigate with `search: (prev) => ({ ...prev, unitIds / size })` and `replace: true`. |
| 34 | `web/src/features/exam/hooks/useStartMultiExam.ts` | hook | Same as `useStartExam`, but calls `useStartMultiUnitExam`. `start(subjectId: string, unitIds: string[], size: number)` → `mutation.mutate({ subjectId, data: { unitIds, size } })`. On success it seeds `getGetExamSessionQueryKey`, calls `invalidateExamViews`, and navigates to the exam or the result. Returns `{ start, isPending, errorCode }`. |
| 35 | `web/src/features/exam/pages/MultiExamBuilderPage.tsx` | page | Heading `multi.title`. Subjects come from `useGetSubjects()`: pending → `ContentListSkeleton(multi.loading)`; error → `ContentErrorState(multi.subjectsErrorTitle)` with retry; empty → `multi.noSubjects`. The selected subject is the search `subjectId` if it is in the list, else the first. Renders `MultiExamSubjectSelect` and `<MultiExamSubjectSection key={subject.id} subjectId={subject.id} />`. |
| 36 | `web/src/features/exam/components/MultiExamSubjectSection.tsx` | component | `useGetMultiUnitExamOverview(subjectId)`: pending → skeleton; error → `ContentErrorState(multi.errorTitle)` with retry; no units → `multi.noUnits`. Otherwise:<br>- the in-progress warning card (`start.otherInProgress` and the link `start.openOther`) when `inProgressExam`;<br>- `MultiExamUnitPicker`;<br>- `MultiExamSizePicker`.<br>`selectedIds` = the search `unitIds` filtered to units that have a blueprint, kept in overview order. `size` = the search size if it is in `sizes`, else `sizes[0]`. Fewer than 2 selected → `<p>{t('multi.chooseTwo')}</p>`; else `<MultiExamPreview subjectId unitIds={selectedIds} size canStart={!inProgressExam} />`. |
| 37 | `web/src/features/exam/components/MultiExamSubjectSelect.tsx` | component | Labelled native `<select>` (label `multi.subject`), with the same classes as `blueprints/components/SubjectPicker.tsx`. Props `{ subjects: readonly SubjectResult[]; value: string; onChange: (id: string) => void }`. |
| 38 | `web/src/features/exam/components/MultiExamUnitPicker.tsx` | component | `<fieldset>` with `<legend>` `multi.units`. One `<label>` per unit with a native checkbox (`min-h-11`), the unit name, and a caption: `multi.available` ({count}) or, with no blueprint, `multi.noBlueprint`, in which case the checkbox is disabled. Props `{ units: MultiUnitExamUnitOptionResult[]; selected: string[]; onToggle: (id: string, checked: boolean) => void }`. |
| 39 | `web/src/features/exam/components/MultiExamSizePicker.tsx` | component | `<fieldset>` with legend `multi.size` and native radios, one per size (label `multi.sizeOption`, {size}). Props `{ sizes: number[]; value: number; onChange: (size: number) => void }`. |
| 40 | `web/src/features/exam/components/MultiExamPreview.tsx` | component | `usePreviewMultiUnitExam(subjectId, { unitIds, size }, { query: { placeholderData: keepPreviousData } })`.<br>- pending → `ContentListSkeleton(multi.previewLoading)`;<br>- error → `<p role="alert">` with `common:errors.{code}` (fallback `UNHANDLED_EXCEPTION`);<br>- data → `<h2>{t('multi.previewTitle')}</h2>`, `<ExamBlueprintSummary blueprint={data.blueprint} />`, `<MultiExamUnitShares units={data.units} />`, then either the shortfall warning (`start.shortfall`, no button) when `!isAvailable`, or, when `canStart`, the `start.start` button (disabled while pending) plus the `role="alert"` start error. |
| 41 | `web/src/features/exam/components/MultiExamUnitShares.tsx` | component | Heading `multi.unitShares` and a `<ul>` of `multi.unitShare` ({unit}, {count}). |
| 42 | `web/src/features/exam/components/ExamUnitBreakdown.tsx` | component | The same card and table as `ExamLessonBreakdown`. Heading `result.byUnit`, columns `result.unit`, `result.percentHeader`, `result.unitCorrectHeader`. Rows: name (or `result.unknownUnit`), `result.percent`, `result.unitCorrect` ({correct},{total}). Returns null when empty. |
| 43 | `web/src/features/exam/schemas/multiExamSearchSchema.test.ts` | test | Test plan. |
| 44 | `web/src/features/exam/pages/MultiExamBuilderPage.test.tsx` | test | Test plan. |
| 45 | i18n keys (`web/src/features/exam/i18n/ar.json` / `en.json`) | json | `multi.title` «امتحان متعدد الوحدات» / "Multi-unit exam"; `multi.subject` «المادة» / "Subject"; `multi.loading` «جارٍ تحميل الوحدات…» / "Loading units…"; `multi.errorTitle` «تعذّر تحميل الوحدات.» / "Couldn't load the units."; `multi.subjectsErrorTitle` «تعذّر تحميل المواد.» / "Couldn't load the subjects."; `multi.noSubjects` «لا توجد مواد بعد.» / "No subjects yet."; `multi.noUnits` «لا توجد وحدات في هذه المادة بعد.» / "This subject has no units yet."; `multi.units` «اختر وحدتين أو أكثر:» / "Choose two or more units:"; `multi.available` «({count, number} سؤال متاح)» / "({count, number} questions available)"; `multi.noBlueprint` «لا يوجد امتحان لهذه الوحدة» / "This unit has no exam"; `multi.size` «عدد الأسئلة» / "Number of questions"; `multi.sizeOption` «{size, number} سؤال» / "{size, number} questions"; `multi.chooseTwo` «اختر وحدتين على الأقل.» / "Choose at least two units."; `multi.previewTitle` «النموذج المدمج (تناسبيًا)» / "Merged blueprint (proportional)"; `multi.previewLoading` «جارٍ حساب الامتحان…» / "Working out the exam…"; `multi.unitShares` «الأسئلة من كل وحدة» / "Questions from each unit"; `multi.unitShare` «{unit}: {count, number} سؤال» / "{unit}: {count, number} questions"; `exam.multiTitle` «امتحان متعدد: {unit}» / "Multi-unit exam: {unit}"; `result.multiTitle` «نتيجة امتحان متعدد: {unit}» / "Multi-unit exam result: {unit}"; `result.byUnit` «حسب الوحدة» / "By unit"; `result.unit` «الوحدة» / "Unit"; `result.unitCorrectHeader` «الإجابات الصحيحة» / "Correct"; `result.unitCorrect` «{correct, number} من {total, number}» / "{correct, number} of {total, number}"; `result.unknownUnit` «غير متاح» / "Not available". |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `MultiUnitExamUnitsTooFew` | `MULTI_UNIT_EXAM_UNITS_TOO_FEW` | `MultiUnitExamSelectionValidator`, and the preview and start validators on a null selection | validation | 422 |
| `MultiUnitExamUnitDuplicate` | `MULTI_UNIT_EXAM_UNIT_DUPLICATE` | `MultiUnitExamSelectionValidator` | validation | 422 |
| `MultiUnitExamSizeInvalid` | `MULTI_UNIT_EXAM_SIZE_INVALID` | `MultiUnitExamSelectionValidator` | validation | 422 |
| `MultiUnitExamNoBlueprint` | `MULTI_UNIT_EXAM_NO_BLUEPRINT` | `MultiUnitExamPlanner.PlanAsync` (context `units`) | `BadRequestCoreException` | 400 |
| reused `SUBJECT_ID_REQUIRED`, `UNIT_ID_REQUIRED` | — | validators | validation | 422 |
| reused `SUBJECT_NOT_FOUND`, `UNIT_NOT_FOUND` | — | overview handler, planner | `NotFoundCoreException` | 404 |
| reused `EXAM_SHORTFALL` | — | `MultiUnitExamPlan.EnsureServable` | `BusinessRuleViolationCoreException` | 400 |
| reused `EXAM_ALREADY_IN_PROGRESS` | — | start handler (and the index race) | `ConflictCoreException` | 409 |
| reused `USER_NOT_AUTHENTICATED` | — | all 3 handlers | `UnauthorizedCoreException` | 401 |

Resx (ar / en):
- `MULTI_UNIT_EXAM_UNITS_TOO_FEW`: «اختر وحدتين على الاقل من نفس المادة.» / "Choose at least two units of the same subject."
- `MULTI_UNIT_EXAM_UNIT_DUPLICATE`: «تم اختيار وحدة اكثر من مرة.» / "A unit was chosen more than once."
- `MULTI_UNIT_EXAM_SIZE_INVALID`: «عدد الاسئلة يجب ان يكون 20 او 40 او 60.» / "The number of questions must be 20, 40 or 60."
- `MULTI_UNIT_EXAM_NO_BLUEPRINT`: «لا يوجد امتحان لبعض الوحدات المختارة: {units}» / "Some chosen units have no exam: {units}"

The web `errors` use the same texts in proper Arabic spelling (أ, إ, ة, as in the existing web entries), and `MULTI_UNIT_EXAM_NO_BLUEPRINT` has no `{units}` tail: «لا يوجد امتحان لبعض الوحدات المختارة.» / "Some chosen units have no exam."

## Domain behaviour

**`MultiUnitBlueprintMerge.Merge(parts, size, maxTimeLimitMinutes)`**
```
if (parts.Count < MultiUnitExamSizes.MinUnits) throw new InvalidOperationException("A multi-unit exam needs at least two units.");
if (!MultiUnitExamSizes.IsAllowed(size)) throw new InvalidOperationException("The multi-unit exam size is not allowed.");
var total = parts.Sum(x => x.Blueprint.QuestionCount);
var cells = parts.SelectMany((part, unitIndex) => part.Blueprint.GetTypeCounts().Where(x => x.Count > 0)
        .Select(x => new Cell(unitIndex, x.Type, x.Count * size / total, x.Count * size % total))).ToList();
var receivers = cells.OrderByDescending(x => x.Remainder).ThenBy(x => x.UnitIndex).ThenBy(x => x.Type)
        .Take(size - cells.Sum(x => x.Allocated)).ToHashSet();
var units = parts.Select((part, unitIndex) => new MultiUnitExamUnitPlan(part.UnitId, part.Blueprint.IsSubjectDefault,
        cells.Where(x => x.UnitIndex == unitIndex).Select(x => new ExamTypeCount(x.Type, x.Allocated + (receivers.Contains(x) ? 1 : 0)))
             .Where(x => x.Count > 0).OrderBy(x => x.Type).ToList(),
        part.Blueprint.GetDifficultyMix())).ToList();
return new MultiUnitExamPlan(units, TimeLimit(parts, units, maxTimeLimitMinutes), PassMark(parts, units, size));
```
- `private sealed record Cell(int UnitIndex, QuestionType Type, int Allocated, int Remainder);`
- `TimeLimit`: `contributing = parts.Zip(units).Where(x => x.Second.QuestionCount > 0)`. If any `First.Blueprint.TimeLimitMinutes is null` → null. Else `(int)Math.Clamp(Math.Ceiling(Σ (decimal)TimeLimitMinutes.GetValueOrDefault() * n / Blueprint.QuestionCount), 1, maxTimeLimitMinutes)`.
- `PassMark`: `(int)Math.Clamp(Math.Round(Σ (decimal)PassMark * n / size, 0, MidpointRounding.AwayFromZero), 1, PassMarkMax)`, with `private const int PassMarkMax = 100; // exam scores are out of 100 (PRD §7.4)`.
- Worked example (used in the tests): A = Mcq 10, 30 min, pass 50; B = Mcq 20, 40 min, pass 60; size 20. That gives A 7 and B 13, time 30·7/10 + 40·13/20 = 47, pass (350 + 780)/20 = 56.5 → 57.

**`MultiUnitExamQuestionSelector.Select`**
```
var all = candidatesByUnit.Values.SelectMany(x => x).DistinctBy(x => x.QuestionId).ToDictionary(x => x.QuestionId);
List<Guid> selected = [];
foreach (var unit in plan.Units)
{
    var pool = candidatesByUnit.GetValueOrDefault(unit.UnitId) ?? [];
    var capped = unit.TypeCounts.Select(x => new ExamTypeCount(x.Type, Math.Min(x.Count, pool.Count(c => c.Type == x.Type)))).ToList();
    selected.AddRange(ExamQuestionSelector.Select(pool, masteredQuestionIds, capped, unit.DifficultyMix, random));
}
var missing = plan.TypeCounts.Select(x => new ExamTypeCount(x.Type, x.Count - selected.Count(id => all[id].Type == x.Type))).Where(x => x.Count > 0).ToList();
if (missing.Count > 0)
{
    var picked = selected.ToHashSet();
    selected.AddRange(ExamQuestionSelector.Select(all.Values.Where(x => !picked.Contains(x.QuestionId)).ToList(), masteredQuestionIds, missing, null, random));
}
return selected.OrderBy(x => all[x].Type).ThenBy(x => all[x].Difficulty).ToList();
```

**`Session.StartMultiUnitExam(Guid studentId, Guid subjectId, IReadOnlyList<CurriculumUnit> units, MultiUnitExamPlan plan, int size, IReadOnlyList<Question> questions, IReadOnlyCollection<Lesson> lessons, bool isTestMode, DateTimeOffset now)`**
1. `units.Count < MultiUnitExamSizes.MinUnits || units.Any(x => x.SubjectId != subjectId)` → `InvalidOperationException("A multi-unit exam needs two or more units of one subject.")`.
2. `var unitIds = units.Select(x => x.Id).ToHashSet(); EnsureExamQuestions(questions, x => IsServableInUnits(x, unitIds, lessons));`. This raises `SESSION_NO_SERVABLE_QUESTIONS`, `SESSION_QUESTION_NOT_SERVABLE` and `SESSION_QUESTION_DUPLICATE` (`BusinessRuleViolationCoreException`).
3. `var scope = new MultiUnitExamScope(subjectId, units.Select(x => x.Id).ToList(), size);`
4. `return CreateExam(studentId, SessionKind.MultiUnitExam, scope.ToJson(), scope.ToKey(), plan.TimeLimitMinutes, plan.PassMark, questions, isTestMode, now);`. `CreateExam` sets `StartedAt`, `LastActivityAt` and the microsecond-truncated `now`; `Deadline = started + TimeLimitMinutes` or null. `UpdationDate` stays unset at creation, as in `StartUnitExam`.

**`Session.GetExamUnitIds()`** → `IReadOnlyList<Guid>`: `Kind switch { SessionKind.UnitExam => [UnitExamScope.FromJson(Scope).UnitId], SessionKind.MultiUnitExam => MultiUnitExamScope.FromJson(Scope).UnitIds, _ => [] }`.

**`ExamBreakdown.ByUnit(IEnumerable<ExamShare> lessonShares, IReadOnlyDictionary<Guid, Guid> unitIdByLessonId, IReadOnlyList<Guid> unitOrder)`** → `List<ExamUnitShare>`: filter to shares whose `LessonId` is in the map, group by the mapped unit, sum `QuestionCount`, `CorrectCount`, `Score` and `MaxScore`, then order by `unitOrder` index (`int.MaxValue` when absent) and then `UnitId`.

The sitting, submit, auto-submit, reveal and mastery rules are all unchanged and kind-agnostic (`Kind != Quiz`).

## API surface
All actions are on `ExamsController` (`api/exams`), with `[Authorize(Policy = DefaultCodes.AssessmentsTake)]`.

| Method | Route | Name | Request | Response |
|---|---|---|---|---|
| GET | `/api/exams/subjects/{subjectId:guid}/multi-unit` | `GetMultiUnitExamOverview` | — | 200 `MultiUnitExamOverviewResult` |
| GET | `/api/exams/subjects/{subjectId:guid}/multi-unit/preview` | `PreviewMultiUnitExam` | `[FromQuery] List<Guid>? unitIds, [FromQuery] int size` → `new PreviewMultiUnitExamQuery(new MultiUnitExamSelection(subjectId, unitIds, size))` | 200 `MultiUnitExamPreviewResult` |
| POST | `/api/exams/subjects/{subjectId:guid}/multi-unit` | `StartMultiUnitExam` | body `StartMultiUnitExamRequest { unitIds, size }` → `new StartMultiUnitExamCommand(new MultiUnitExamSelection(subjectId, request.UnitIds, request.Size))` | 200 `ExamSessionResult` (new, resumed or auto-submitted) |

The sitting endpoints (`GET /api/exams/{id}`, `PUT …/answers/{questionId}`, `POST …/submit`) serve multi-unit exams unchanged.

## Docs
| Doc | Change |
|---|---|
| `docs/exams.md` | Intro: "Multi-unit exams (#82) reuse the sitting endpoints" becomes a link to a new section **Multi-unit exams**. The section covers:<br>- sizes 20/40/60 and 2+ units of one subject;<br>- per-unit resolution and `MULTI_UNIT_EXAM_NO_BLUEPRINT`;<br>- the merge formula and tie-breaks with the worked example;<br>- the time limit and pass mark rules (Decisions 6–8);<br>- selection with spill-over (Decision 10) and the union shortfall `EXAM_SHORTFALL`;<br>- the scope and key format;<br>- start and resume and the one-open-exam rule;<br>- the result `unitBreakdown`, `subjectId` and titles;<br>- the builder screen `/student/multi-exam` (states from Files #35–#41, URL search, retake).<br>Add the 3 rows to the API table, `MultiUnitExamOverviewResult`, `MultiUnitExamPreviewResult` and the new `ExamSessionResult` fields (`subjectId`, `unitBreakdown[] { unitId, name?, questionCount, correctCount, score, maxScore, scorePercent }`), and the 4 codes to the error table. |
| `docs/exam-blueprints.md` | "Resolution for exams" step 4: each selected unit resolves as in step 1, and the merge and time/pass rules are linked to `docs/exams.md#multi-unit-exams`. |
| `docs/sessions.md` | Line 3: drop "(a multi-unit exam follows in #82)" and name multi-unit exams. Line 15: `MultiUnitExam` via `/api/exams/subjects/{id}/multi-unit`, no longer "reserved". |
| `docs/progress.md` | Line 37: "A multi-unit exam shows its unit names joined by « + »; a deleted lesson, or an exam whose units are all deleted, has no name and shows «غير متاح»." |
| `docs/PRD.md` §7.5 | Add bullets: "Sizes are exactly 20, 40 or 60." "Each unit's share is proportional to its blueprint's question count; if a unit is short of a type, the rest comes from the other selected units." "Time limit and pass mark are the question-weighted combination of the units' blueprints; if any contributing blueprint is untimed, the exam is untimed." "Result adds a per-unit breakdown." |
| `docs/claude-design-prompt.md` | §4: `#/student/multi-exam` "size 10 / 20" becomes "size 20 / 40 / 60 (PRD §7.5), live merged preview with shortfall". §7 item 3: "size 10" becomes "size 20". |
| `docs/prototype.md` | Walkthrough item 6: "choose 2+ units and 10/20 questions" becomes "choose 2+ units and a size (the prototype offers 10/20; the product uses 20/40/60, PRD §7.5)". |

## Test plan
### api — Domain (no doubles)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 1 | `Domain/Sessions/Exams/MultiUnitBlueprintMergeTests` | `Merge_EqualBlueprints_SplitsSizeEvenly` | Two units at Mcq 10, size 20 → each unit plan Mcq 10 |
| 2 | 〃 | `Merge_ScalesEachUnitsTypeCountsProportionally` | A {Mcq 6, Fill 4}, B {Mcq 10}, size 40 → A {Mcq 12, Fill 8}, B {Mcq 20} |
| 3 | 〃 | `Merge_EqualRemainders_GoToEarlierUnitThenEarlierType` | A {Mcq 1, Fill 1}, B {Mcq 1}, size 20 → A {Mcq 7, Fill 7}, B {Mcq 6} |
| 4 | 〃 | `Merge_AnySize_TotalEqualsSize` (Theory 20/40/60; blueprints Mcq 7 + Fill 4 and Mcq 11) | `plan.QuestionCount == size` |
| 5 | 〃 | `Merge_TypeCounts_SumAcrossUnitsInTypeOrder` | Merged `TypeCounts` equal [(Mcq, x), (Fill, y)] with the per-unit sums |
| 6 | 〃 | `Merge_KeepsEachUnitsDifficultyMix` | Unit plan `DifficultyMix` equals that unit's blueprint mix; the other unit has null |
| 7 | 〃 | `Merge_AllTimed_TimeLimitIsQuestionWeightedRoundedUp` | Worked example → `TimeLimitMinutes == 47` |
| 8 | 〃 | `Merge_ContributingUnitUntimed_IsUntimed` | Null time limit |
| 9 | 〃 | `Merge_TimeLimitAboveMaximum_IsCapped` | maxTimeLimitMinutes 30 → 30 |
| 10 | 〃 | `Merge_PassMark_QuestionWeightedRoundedHalfUp` | Worked example → 57 |
| 11 | 〃 | `Merge_UnitWithZeroShare_IsIgnoredForTimeAndPassMark` | A {Mcq 1} untimed pass 90, B {Mcq 100} 60 min pass 50, size 20 → A gets 0, time 12, pass 50 |
| 12 | 〃 | `Merge_OnePart_ThrowsInvalidOperation` | `InvalidOperationException` |
| 13 | 〃 | `Merge_SizeNotAllowed_ThrowsInvalidOperation` | size 30 → `InvalidOperationException` |
| 14 | `Domain/Sessions/Exams/MultiUnitExamPlanTests` | `EnsureServable_UnionCoversCounts_DoesNotThrow` | No exception |
| 15 | 〃 | `EnsureServable_TypeShort_ThrowsExamShortfallWithTypes` | `BusinessRuleViolationCoreException`, code `EXAM_SHORTFALL`, context `types` == `"Mcq 3/20"` |
| 16 | `Domain/Sessions/Exams/MultiUnitExamQuestionSelectorTests` | `Select_EachUnitHasEnough_TakesPlannedCountFromEachUnit` | The count per unit pool equals the unit plan count |
| 17 | 〃 | `Select_UnitShortOfType_FillsFromOtherSelectedUnits` | A plans 10 but has 4, B plans 10 and has 20 → 20 ids, 4 from A, 16 from B, no duplicates |
| 18 | 〃 | `Select_PrefersNotMasteredAcrossUnits` | With enough not-mastered questions, the result does not intersect mastered |
| 19 | 〃 | `Select_OrdersByTypeThenDifficulty` | Types and difficulties of the result are non-decreasing |
| 20 | 〃 | `Select_AppliesEachUnitsDifficultyMix` | A with mix 0/0/100 and enough Hard → all of A's picks are Hard |
| 21 | 〃 | `Select_SameSeed_ReturnsSameSelection` | Two runs with `new Random(7)` are equal |
| 22 | `Domain/Sessions/MultiUnitExamScopeTests` | `ToKey_SortsUnitIdsAndIncludesSize` | Keys for [b, a] and [a, b] are equal and start with `units:20:` |
| 23 | 〃 | `FromJson_RoundTripsSubjectUnitsAndSize` | Fields equal after the round trip, unit order kept |
| 24 | 〃 | `FromJsonNull_ThrowsInvalidOperation` | `InvalidOperationException` |
| 25 | `Domain/Sessions/SessionMultiUnitExamStartTests` | `StartMultiUnitExam_SetsKindScopeDeadlineAndPassMark` | Kind `MultiUnitExam`, `ScopeKey` equals the scope key, `Deadline = StartedAt + plan minutes`, `PassMark`, item count, positions 1..n |
| 26 | 〃 | `StartMultiUnitExam_UntimedPlan_HasNoDeadline` | `Deadline` null |
| 27 | 〃 | `StartMultiUnitExam_QuestionOutsideSelectedUnits_ThrowsNotServable` | Code `SESSION_QUESTION_NOT_SERVABLE` |
| 28 | 〃 | `StartMultiUnitExam_NoQuestions_ThrowsNoServableQuestions` | Code `SESSION_NO_SERVABLE_QUESTIONS` |
| 29 | 〃 | `StartMultiUnitExam_DuplicateQuestion_ThrowsDuplicate` | Code `SESSION_QUESTION_DUPLICATE` |
| 30 | 〃 | `StartMultiUnitExam_UnitOfOtherSubject_ThrowsInvalidOperation` | `InvalidOperationException` |
| 31 | 〃 | `StartMultiUnitExam_OneUnit_ThrowsInvalidOperation` | `InvalidOperationException` |
| 32 | 〃 | `GetExamUnitIds_MultiUnitExam_ReturnsScopeOrder` | Equals [Mechanics.Id, Waves.Id] |
| 33 | 〃 | `GetExamUnitIds_UnitExam_ReturnsItsUnit` | Via `ExamSessionBuilder` → [unit id] |
| 34 | 〃 | `SubmitExam_MultiUnitExam_ScoresLikeUnitExam` | Save 1 of 2 answers correct, submit → `ScorePercent` 50 |
| 35 | `Domain/Sessions/Exams/ExamBreakdownTests` (add) | `ByUnit_SumsLessonSharesPerUnitInScopeOrder` | Two units, three lessons → rows in `unitOrder` order with summed score and maxScore |
| 36 | 〃 (add) | `ByUnit_LessonWithoutUnit_IsSkipped` | A share whose lesson is not in the map is excluded |

### api — Application (NSubstitute at repositories)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 37 | `Application/Features/Exams/Shared/MultiUnitExamSelectionValidatorTests` | `Validate_ValidSelection_Passes` | Valid |
| 38 | 〃 | `Validate_EmptySubjectId_ReturnsSubjectIdRequired` | `SUBJECT_ID_REQUIRED` |
| 39 | 〃 | `Validate_NullUnitIds_ReturnsUnitsTooFew` | `MULTI_UNIT_EXAM_UNITS_TOO_FEW` |
| 40 | 〃 | `Validate_OneUnit_ReturnsUnitsTooFew` | 〃 |
| 41 | 〃 | `Validate_DuplicateUnit_ReturnsUnitDuplicate` | `MULTI_UNIT_EXAM_UNIT_DUPLICATE` |
| 42 | 〃 | `Validate_EmptyUnitId_ReturnsUnitIdRequired` | `UNIT_ID_REQUIRED` |
| 43 | 〃 | `Validate_SizeNotAllowed_ReturnsSizeInvalid` (Theory 0, 10, 30, 80) | `MULTI_UNIT_EXAM_SIZE_INVALID` |
| 44 | `Application/Features/Exams/StartMultiUnitExam/StartMultiUnitExamValidatorTests` | `Validate_NullSelection_ReturnsUnitsTooFew` | Code |
| 45 | 〃 | `Validate_SizeInvalid_ReturnsSizeInvalid` | Delegation works |
| 46 | `Application/Features/Exams/PreviewMultiUnitExam/PreviewMultiUnitExamValidatorTests` | `Validate_NullSelection_ReturnsUnitsTooFew` | Code |
| 47 | 〃 | `Validate_OneUnit_ReturnsUnitsTooFew` | Delegation works |
| 48 | `Application/Features/Exams/GetMultiUnitExamOverview/GetMultiUnitExamOverviewValidatorTests` | `Validate_EmptySubjectId_ReturnsSubjectIdRequired` | Code |
| 49 | 〃 | `Validate_SubjectId_Passes` | Valid |
| 50 | `…/GetMultiUnitExamOverview/GetMultiUnitExamOverviewHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException` + code |
| 51 | 〃 | `Handle_SubjectMissing_ThrowsSubjectNotFound` | `NotFoundCoreException` + code |
| 52 | 〃 | `Handle_Units_ReturnsOrderWithBlueprintStateAndServableCount` | Units by order: own blueprint (`HasBlueprint`, not default), default-only, and `ServableCount` summed across types; `Sizes == [20, 40, 60]` |
| 53 | 〃 | `Handle_NoDefaultAndNoUnitBlueprint_UnitHasNoBlueprint` | `HasBlueprint == false` |
| 54 | 〃 | `Handle_OpenExam_ReturnsInProgressExam` | `InProgressExam == new(open.Id, false)` |
| 55 | `…/PreviewMultiUnitExam/PreviewMultiUnitExamHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | Code |
| 56 | 〃 | `Handle_SubjectMissing_ThrowsSubjectNotFound` | Code |
| 57 | 〃 | `Handle_UnitOfOtherSubject_ThrowsUnitNotFound` | `NotFoundCoreException` `UNIT_NOT_FOUND` |
| 58 | 〃 | `Handle_UnitWithoutBlueprint_ThrowsNoBlueprintWithUnitNames` | `BadRequestCoreException`, code `MULTI_UNIT_EXAM_NO_BLUEPRINT`, context `units` == "Waves" |
| 59 | 〃 | `Handle_TwoUnits_ReturnsMergedCountsWithUnionAvailability` | `Blueprint.TypeCounts` [(Mcq, 20, 25)], time, pass, `Units` shares 10/10, `IsAvailable` true |
| 60 | 〃 | `Handle_UnionShort_ReturnsNotAvailable` | `IsAvailable == false` |
| 61 | 〃 | `Handle_AllUnitsOnDefault_MarksSubjectDefault` | `Blueprint.IsSubjectDefault == true` |
| 62 | `…/StartMultiUnitExam/StartMultiUnitExamHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | Code; `SaveChangesAsync` DidNotReceive |
| 63 | 〃 | `Handle_SubjectMissing_ThrowsSubjectNotFound` | Code; DidNotReceive |
| 64 | 〃 | `Handle_UnitNotInSubject_ThrowsUnitNotFound` | Code; DidNotReceive |
| 65 | 〃 | `Handle_OtherExamOpen_ThrowsExamAlreadyInProgress` | `ConflictCoreException`; DidNotReceive |
| 66 | 〃 | `Handle_UnitWithoutBlueprint_ThrowsNoBlueprint` | Code; DidNotReceive |
| 67 | 〃 | `Handle_UnionShort_ThrowsExamShortfall` | `BusinessRuleViolationCoreException` `EXAM_SHORTFALL`; DidNotReceive |
| 68 | 〃 | `Handle_NewSelection_AddsMultiUnitExamOfChosenSize` | `AddAsync` Received(1) with Kind `MultiUnitExam`, 20 items and the plan's pass mark; `SaveChangesAsync` Received(1); result `Kind`, `Units` in unit order, `SubjectId` |
| 69 | 〃 | `Handle_SameSelectionOpen_ResumesWithoutNewDraw` | Result id == open id; `AddAsync` DidNotReceive; `SaveChangesAsync` Received(1) |
| 70 | 〃 | `Handle_SameSelectionOpenPastDeadline_SubmitsIt` | Result `SubmittedAt` not null; `SaveChangesAsync` Received(1) |
| 71 | 〃 | `Handle_Admin_StartsTestModeExam` | `IsTestMode` true |
| 72 | `…/GetExamSession/GetExamSessionHandlerTests` (add) | `Handle_SubmittedMultiUnitExam_ReturnsUnitsSubjectAndUnitBreakdown` | `Units` 2 in scope order, `SubjectId`, `UnitBreakdown` 2 rows ordered by unit |
| 73 | `…/Progress/GetSessionHistory/GetSessionHistoryHandlerTests` (add) | `Handle_MultiUnitExam_JoinsUnitNamesInScopeOrder` | `ScopeName == "Mechanics + Waves"`, `UnitId` null |

### api — Integration (`Integration/Exams/MultiUnitExamEndpointTests`, real PostgreSQL)
| # | Test method | Asserts |
|---|---|---|
| 74 | `GetOverview_Anonymous_Returns401` | 401 |
| 75 | `GetOverview_Teacher_Returns403` | 403 |
| 76 | `GetOverview_Student_ReturnsUnitsWithServableCountsAndSizes` | 200; units by order, `servableCount`, `sizes` [20, 40, 60] |
| 77 | `GetOverview_UnknownSubject_Returns404` | 404 `SUBJECT_NOT_FOUND` |
| 78 | `Preview_TwoUnits_ReturnsMergedCounts` | 200 through repeated `unitIds` query parameters; `blueprint.typeCounts[0]` required 20, `units[*].questionCount` 10/10 |
| 79 | `Preview_OneUnit_Returns422UnitsTooFew` | 422 `MULTI_UNIT_EXAM_UNITS_TOO_FEW` |
| 80 | `Start_TwoUnits_StartsMultiUnitExam` | 200 `kind` `MultiUnitExam`, 20 items, 10 from each unit's questions, no `correctAnswer`; DB session `Kind`, `ScopeKey` starts with `units:20:`, `Deadline` |
| 81 | `Start_SameSelectionAgain_ResumesSameSession` | Same id; one session in the DB |
| 82 | `Start_SizeInvalid_Returns422` | 422 `MULTI_UNIT_EXAM_SIZE_INVALID` |
| 83 | `Start_UnitFromOtherSubject_Returns404` | 404 `UNIT_NOT_FOUND` |
| 84 | `Start_UnionShort_Returns400ExamShortfall` | Units with 5 + 5 questions, size 20 → 400 `EXAM_SHORTFALL`; no session saved |
| 85 | `Start_UnitExamOpen_Returns409` | Unit exam open → 409 `EXAM_ALREADY_IN_PROGRESS` |
| 86 | `Start_Anonymous_Returns401` | 401 |
| 87 | `SaveAndSubmit_MultiUnitExam_ReturnsUnitBreakdown` | Save through `PUT /api/exams/{id}/answers/{q}`, then `POST …/submit` → 200 `submittedAt`, `unitBreakdown` has 2 rows in unit order, `subjectId` |
| 88 | `SessionHistoryEndpointTests.Get_MultiUnitExam_ShowsJoinedUnitNames` (add) | `scopeName` "Mechanics + Waves", `kind` `MultiUnitExam` |

### web (Vitest + Testing Library + MSW; `renderApp`)
| # | File | `it(...)` | Asserts |
|---|---|---|---|
| 89 | `schemas/multiExamSearchSchema.test.ts` | `accepts a subject, units and size` | Parsed equals the input |
| 90 | 〃 | `drops an invalid subject id` | `subjectId` undefined |
| 91 | 〃 | `drops unit ids that are not uuids` | `unitIds` undefined |
| 92 | 〃 | `drops a non-integer size` | `size` undefined |
| 93 | `pages/MultiExamBuilderPage.test.tsx` | `shows the units of the first subject after loading` | Loading status, then the checkbox "Mechanics" and the caption "(12 questions available)" |
| 94 | 〃 | `asks for two units when fewer are selected` | "Choose at least two units." after ticking one |
| 95 | 〃 | `shows the merged blueprint when two units are selected` | Tick two → row cells ['Multiple choice', '20', '25'], "Time: 47 min", "Pass mark: 57", "Mechanics: 10 questions" |
| 96 | 〃 | `updates the preview when another size is chosen` | Choose "40 questions" → row required '40' (MSW reads `size` from the URL) |
| 97 | 〃 | `shows the shortfall and hides Start when questions are short` | Shortfall text; no "Start exam" button |
| 98 | 〃 | `disables a unit without an exam blueprint` | Checkbox disabled and "This unit has no exam" |
| 99 | 〃 | `starts the exam and opens the exam screen` | Click "Start exam" → exam heading "Multi-unit exam: Mechanics + Waves" |
| 100 | 〃 | `shows the server error when the start is refused` | 409 → alert "You already have an exam in progress…" (the web text) |
| 101 | 〃 | `warns about an exam in progress and links to it` | Warning text, link to `/student/exam/{id}`, no Start button |
| 102 | 〃 | `clears the selection when the subject changes` | Select the second subject → no checkbox checked |
| 103 | 〃 | `restores the selection from the URL` | Path `?subjectId=…&unitIds=[…]&size=40` → both checked and "40 questions" radio checked |
| 104 | 〃 | `shows retry when the units fail to load` | 500 → retry → units shown |
| 105 | 〃 | `says the subject has no units` | "This subject has no units yet." |
| 106 | 〃 | `renders right-to-left in Arabic without axe violations` | `dir="rtl"`, «امتحان متعدد الوحدات», `axe` has no violations |
| 107 | `pages/ExamResultPage.test.tsx` (add) | `shows the per-unit breakdown for a multi-unit exam` | Heading "By unit"; rows "Mechanics", "Waves" with percent |
| 108 | 〃 (add) | `retakes a multi-unit exam in the builder with the same units and size` | Click "Retake exam" → builder with both units checked and "20 questions" checked |
| 109 | `pages/ExamPage.test.tsx` (add) | `titles a multi-unit exam with every unit name` | Heading "Multi-unit exam: Mechanics + Waves" |
| 110 | `api/examSession.test.ts` (add) | `joins the unit names and recognises a multi-unit exam` | `examUnitNames` → "Mechanics + Waves"; `isMultiUnitExam` true/false |
| 111 | `api/invalidateExamViews.test.ts` (add) | `marks the multi-unit overview stale` | `getGetMultiUnitExamOverviewQueryKey(subjectId)` is invalidated |

Every new test is mutation-checked: break the line under test on purpose, confirm the test fails, then restore it.

## Definition of done
- [ ] Every one of the 111 tests above exists with the named class or file and method, and passes. No existing test is edited except for the additions listed.
- [ ] `dotnet test api/ -c Release` is green with `appsettings.json` moved aside (CI parity); no new config keys.
- [ ] Web: `npm --prefix web run typecheck`, `lint`, `vitest run`, and `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` are all clean.
- [ ] `api/openapi/v1.json`, `web/src/shared/api/generated/**` and `web/src/routeTree.gen.ts` are regenerated and committed. The generated `getPreviewMultiUnitExamUrl` appends each `unitIds` value separately.
- [ ] No migration is added; `AppDbContextTests` is unchanged.
- [ ] `POST /api/exams/subjects/{id}/multi-unit` with 2 units and size 20 creates a `MultiUnitExam` session with 20 items drawn only from those units, and `ScopeKey` `units:20:…`.
- [ ] The merge follows Decision 4 exactly (largest remainder, ties by unit order then type); the time limit and pass mark follow Decisions 7–8.
- [ ] A unit short of a type is filled from the other selected units; only a union shortfall returns 400 `EXAM_SHORTFALL`.
- [ ] A unit without any blueprint returns 400 `MULTI_UNIT_EXAM_NO_BLUEPRINT` (context `units`), and it is disabled in the builder.
- [ ] One open exam per student still holds across kinds (409 both ways); the same selection resumes.
- [ ] Save, submit, auto-submit, reveal and mastery work for multi-unit exams through the #81 endpoints with no new sitting code.
- [ ] `ExamSessionResult` carries `subjectId` and `unitBreakdown`; the result page shows «حسب الوحدة» for multi-unit exams only.
- [ ] History shows «Mechanics + Waves» for a multi-unit exam.
- [ ] The builder at `/student/multi-exam` has subject, units, size (20/40/60) and live preview, with loading, error-with-retry, empty, in-progress, shortfall, fewer-than-two and RTL states. The selection lives in the URL; retake restores it.
- [ ] Every user-visible string goes through `t()` in both `ar` and `en`. Only design tokens are used; no arbitrary values; logical properties only.
- [ ] The 4 new error codes are in the Application `ErrorCodes`, both resx files and both web `errors` files.
- [ ] Every new endpoint carries `DefaultCodes.AssessmentsTake`; `EndpointAuthorizationTests` is green.
- [ ] Postman mirrors the 3 endpoints in order, with the `secondUnitId` variable.
- [ ] The docs listed in the Docs table are updated; no doc contradicts the implementation (sizes, scope name, multi-unit rules).
- [ ] Every Morabh statement is accurate: no Morabh equivalent exists for the new types.
