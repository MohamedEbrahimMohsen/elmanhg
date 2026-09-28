# Plan — [E6.S1] Exam blueprint authoring (#80)

## Goal
An admin can open `/admin/blueprints`, pick a subject, and define the exam "shape" for that subject's default and for each of its units: a question count per type, an optional difficulty mix, an optional time limit and a pass mark. Next to every type the editor shows how many servable questions are available and highlights any shortfall live. A blueprint that asks for more servable questions of any type than exist cannot be saved. The client blocks the save first and the server enforces the rule (PRD §10.2, §17 rule 8). A unit can drop its own blueprint and go back to the subject default. The model is built so that #81 (unit exam generation), #82 (multi-unit merge) and #83 (retakes) can read a resolved blueprint and re-run the same shortfall check.

## Scope
**In:**
- Domain `ExamBlueprint` (subject default or unit override) with jsonb type counts and difficulty mix, a shortfall guard, and a pure `ExamBlueprintShortfall.Find` that #81 and #82 will reuse.
- Servable counts per unit and type, in one grouped query built on `ServableQuestionSpecification`.
- Admin API: an overview read, save (upsert) of the subject default, save (upsert) of a unit blueprint, and delete of a unit blueprint. All four are audited except the read. Policy is `Blueprints.Manage`.
- Migration `AddExamBlueprints`, with partial unique indexes and their 409 mapping.
- Web `features/blueprints`: subject picker, subject-default editor, per-unit editors, live shortfall, "uses the default" card, create and revert for a unit.
- Docs: new `docs/exam-blueprints.md`, plus updates to PRD §10.2/§15, `docs/audit-log.md` and `docs/claude-design-prompt.md` §4. Postman, OpenAPI and Orval updated.

**Out:**
- Exam generation, sitting, timers and the student exam-start summary (#81). Multi-unit merge (#82). Retakes and best score (#83).
- Optimistic concurrency on blueprint edits: the last write wins, and every save is audited.
- Cascading blueprint deletes when a subject or unit is deleted. Such a blueprint becomes an orphan that no read can reach (see Decisions).

**Deferred:** none. Nothing in this story needs credentials or an external service.

## Decisions
| # | Question | Decision | Why |
|---|---|---|---|
| 1 | Where does the owner link live? PRD §15 puts `default_blueprint_id?` on Subject and `blueprint_id?` on Unit. | The owner lives on the blueprint: `ExamBlueprint.SubjectId` (required) plus `UnitId?`. A null `UnitId` means the subject default. `Subject` and `Unit` are not touched. PRD §15 is updated to match. | Partial unique indexes guarantee one default per subject and one blueprint per unit, with no two-way FK and no orphan from a lost race. The prototype also stores `scope` + `refId` on the blueprint. |
| 2 | The PRD `name` field | Dropped. The UI titles a card from the subject or unit name ("النموذج الافتراضي للمادة", "وحدة: X"). PRD §15 is updated. | The prototype never edits the name, and it is derivable. |
| 3 | Save semantics | Upsert: `PUT` creates the blueprint if missing, otherwise updates it in place, keeping the same id. | The prototype has a single "حفظ"; one idempotent verb. |
| 4 | Which pool does the subject default's shortfall check use? | The subject's whole servable pool (all its units). | Prototype `bpSave` (`inSubject`); the default is also what #82 merges across units. |
| 5 | A unit that uses the default but whose own pool is too small for it | A non-blocking warning ("عجز حالي في هذه الوحدة") on that unit's card, computed on the client. | Rule 8 blocks saving a blueprint. A default can be valid for the subject and still short for one unit. #81 re-checks at exam start. |
| 6 | Difficulty mix | Optional. Easy/Medium/Hard as whole percentages from 0 to 100 that sum to 100. It is a **target**. The save check covers **type counts only**. The generator (#81) follows the mix as far as the pool allows. | PRD §10.2 validation mentions "servable questions of any required type" only. A hard per-cell check makes some blueprints impossible to satisfy. |
| 7 | Pass mark range | Whole number from 1 to 100 (the score is out of 100, PRD §7.4). This is an invariant constant with a WHY comment, not an option. | A percentage is bounded by definition. |
| 8 | Time limit | Optional. When set, a whole number of minutes from 1 to `ExamBlueprints:MaxTimeLimitMinutes` (default 300). | PRD "Time limit (optional)". The cap is tunable, so it lives in Options (skill §8.1). |
| 9 | Size cap | Each count is between 0 and `ExamBlueprints:MaxQuestionCount` (default 100), and so is the total. The total must be at least 1. | Prototype "يجب تحديد سؤال واحد على الأقل". The cap goes in Options. |
| 10 | Type-count storage | A jsonb array holding only types with a count above 0, sorted by `QuestionType` order, camelCase: `[{"type":"mcq","count":10},{"type":"fill","count":5}]`. There is also an int `QuestionCount` column holding the sum. The mix is jsonb `{"easyPercent":30,"mediumPercent":50,"hardPercent":20}` or null. | The skill's jsonb delta. The canonical form keeps audit diffs clean. `QuestionCount` gives #82 the merge denominator. It uses `QuestionJson.SerializerOptions`, like `UnitExamScope`. |
| 11 | API request shape | `typeCounts` is a list of `{ type, count }` using the API's PascalCase enum. Zero counts are allowed and dropped when saved. | A list gives a clean Orval type. |
| 12 | Delete | Only a unit blueprint can be deleted: `DELETE /api/exam-blueprints/{id}`. Deleting the subject default gives 400 `EXAM_BLUEPRINT_DEFAULT_NOT_DELETABLE`. The UI button reads "استخدام النموذج الافتراضي". | This is the only way for a unit to revert to the default. The default is the fallback for every unit, so it stays. |
| 13 | Routes | Controller `api/exam-blueprints`: `GET subjects/{subjectId}`, `PUT subjects/{subjectId}`, `PUT units/{unitId}`, `DELETE {examBlueprintId}`. | This mirrors `api/mastery/subjects/{id}`. The unit id alone determines the subject. |
| 14 | How the web shows the shortfall | The overview returns servable counts for every type (zeros included) for the subject and for each unit. The client computes the shortfall live and blocks the save with the same error text the server uses. After any save (success or error) the overview query is invalidated, so the counts are fresh. | This matches the prototype. The server error body carries only `code` and `message` (`Core.Exceptions.ErrorResponse`), so structured detail must come from the read. |
| 15 | Concurrent first save of the same default or unit | The partial unique index rejects the second insert, which maps to 409 `EXAM_BLUEPRINT_MODIFIED_CONCURRENTLY` in `AppDbContext.SaveChangesAsync`. A retry updates the saved row. | This is the existing pattern for `IX_Sessions_InProgressScope`. |
| 16 | Subject or unit deletion | No cascade. `DeleteUnit` requires no lessons and `DeleteSubject` requires no units, so a leftover blueprint has no servable pool. Every read goes through a live subject or unit, so the orphan is never seen. | Avoids touching the delete handlers and their tests. The behaviour is documented in `docs/exam-blueprints.md`. |
| 17 | Blueprint resolution for #81 and #82 | Documented, not coded: a unit's blueprint first, otherwise the subject default, otherwise no exam. The exam start re-runs `ExamBlueprintShortfall.Find` against the unit pool. A session copies the time limit and pass mark at start, so later edits never change a started exam. | No speculative code. The contract is fixed now so the next stories cannot drift. |
| 18 | Subject selection on the page | A labelled `<select>` "المادة" bound to the URL search param `subjectId`. It defaults to the first subject by order and shows one subject at a time. | Skill §4: filters live in the URL. The prototype stacks all subjects, which becomes N reads with real data. |
| 19 | Base class | `ExamBlueprint : AuditEntity, IAuditedEntity`. | Mirrors `Subject` and `CurriculumUnit`. Audit diffs cover it. |
| 20 | Audit actions | `ExamBlueprint.SaveSubjectDefault`, `ExamBlueprint.SaveUnit`, `ExamBlueprint.Delete`. Resource type `ExamBlueprint`. The resource id comes from the result on save and from the command on delete. | Uses the `docs/audit-log.md` create and update patterns. |
| 21 | Type validation codes | Reuse `QUESTION_TYPE_REQUIRED` and `QUESTION_TYPE_INVALID`. | Same meaning. No duplicate strings. |
| 22 | Morabh reuse | The child-rule list validation comes from Morabh `Morabh.Application/Analytics/RecordAppEvents/RecordAppEventsValidator.cs` (`RuleForEach(...).ChildRules`). Everything else is new; Morabh has no exam, blueprint or upsert equivalent (searched `Morabh.Domain`, `Morabh.Application`). | Reuse-first delta §5. |

## Existing code touched
| File | Change |
|---|---|
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Add a `// EXAM BLUEPRINTS` group: `ExamBlueprintShortfall = "EXAM_BLUEPRINT_SHORTFALL"` and `ExamBlueprintDefaultNotDeletable = "EXAM_BLUEPRINT_DEFAULT_NOT_DELETABLE"`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add a `// EXAM BLUEPRINTS` group (codes in the table below). |
| `api/Elmanhg.Domain/Questions/IQuestionRepository.cs` | Add `Task<List<ServableQuestionCount>> CountServableByUnitAndTypeAsync(Guid subjectId, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs` | Implement it: `_dbSet.WhereServable(_context.Set<Lesson>()).Where(x => x.SubjectId == subjectId).Join(_context.Set<Lesson>(), question => question.LessonId, lesson => lesson.Id, (question, lesson) => new { lesson.UnitId, question.Type }).GroupBy(x => new { x.UnitId, x.Type }).Select(x => new ServableQuestionCount(x.Key.UnitId, x.Key.Type, x.Count())).ToListAsync(cancellationToken).ConfigureAwait(false)`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Add consts `SubjectDefaultBlueprintIndex = "IX_ExamBlueprints_SubjectDefault"` and `UnitBlueprintIndex = "IX_ExamBlueprints_UnitId"`. Add `DbSet<ExamBlueprint> ExamBlueprints { get; set; }` and `ConfigureExamBlueprints(modelBuilder)`, called after `ConfigureQuestionMastery`. Add a catch in `SaveChangesAsync`: `DbUpdateException` with an inner `PostgresException { SqlState: UniqueViolation, ConstraintName: SubjectDefaultBlueprintIndex or UnitBlueprintIndex }` throws `new ConflictCoreException(ErrorCodes.ExamBlueprintModifiedConcurrently, innerException: exception)`. Add `modelBuilder.Entity<ExamBlueprint>().HasQueryFilter(x => !x.IsDeleted);` to the global filter method. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<IExamBlueprintRepository, ExamBlueprintRepository>();` |
| `api/Elmanhg.Application/DependencyInjection.cs` | `services.AddOptions<ExamBlueprintsOptions>().BindConfiguration(ExamBlueprintsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` |
| `api/Elmanhg.Api/appsettings.example.json` | `"ExamBlueprints": { "MaxQuestionCount": 100, "MaxTimeLimitMinutes": 300 },` after `"Progress"`. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | One `<data>` per new code (strings below). |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `["ExamBlueprints:MaxQuestionCount"] = "100"` and `["ExamBlueprints:MaxTimeLimitMinutes"] = "300"` to the in-memory settings. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | `Migrate_FreshDatabase_LeavesNoPendingMigrations`: append `seventeenth => seventeenth.Should().EndWith("_AddExamBlueprints")` (the accepted pattern). |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by the migration. |
| `postman/elmanhg.postman_collection.json` | New folder `ExamBlueprints` after `Progress` (requests below). |
| `web/src/routes/admin/blueprints.tsx` | Replace the placeholder with `validateSearch: blueprintsSearchSchema, component: ExamBlueprintsPage`, both from `@/features/blueprints`. |
| `web/src/app/i18n.ts` | Import `blueprintsLocales` from `@/features/blueprints/locales`. Add a `blueprints` namespace to `ar`, `en` and the `ns` array. |
| `web/src/features/questions/index.ts` | `export { questionTypes } from './api/questionOptions';` |
| `web/src/shared/i18n/ar.json`, `en.json` | `errors.<CODE>` for every new code (strings below). |
| `web/src/shared/api/generated/**` | Regenerated with `npm --prefix web run gen:api`. This adds `exam-blueprints/exam-blueprints.ts`, `.msw.ts`, the zod file and the model types. |
| `docs/PRD.md` | Changes listed under Docs below. |
| `docs/audit-log.md` | 3 rows in "Audited commands". Add `ExamBlueprint` to the "Audited entities" list. |
| `docs/claude-design-prompt.md` | §4 Admin: replace the `#/admin/blueprints` bullet (text below). |

## Files to create

### Domain — `api/Elmanhg.Domain/ExamBlueprints/` (namespace `Elmanhg.Domain.ExamBlueprints`)
| # | Path | Type | Contract |
|---|---|---|---|
| D1 | `ExamTypeCount.cs` | `public sealed record ExamTypeCount(QuestionType Type, int Count);` | |
| D2 | `ExamDifficultyMix.cs` | `public sealed record ExamDifficultyMix(int EasyPercent, int MediumPercent, int HardPercent);` | |
| D3 | `ExamBlueprintShape.cs` | `public sealed record ExamBlueprintShape(IReadOnlyList<ExamTypeCount> TypeCounts, ExamDifficultyMix? DifficultyMix, int? TimeLimitMinutes, int PassMark);` | |
| D4 | `ExamTypeShortfall.cs` | `public sealed record ExamTypeShortfall(QuestionType Type, int Required, int Available) { public int Missing => Required - Available; }` | |
| D5 | `ExamBlueprintShortfall.cs` | `public static class ExamBlueprintShortfall` | `public static List<ExamTypeShortfall> Find(IEnumerable<ExamTypeCount> required, IReadOnlyDictionary<QuestionType, int> available)`: for each `x` with `x.Count > available.GetValueOrDefault(x.Type)`, yield `new ExamTypeShortfall(x.Type, x.Count, available.GetValueOrDefault(x.Type))`, ordered by `Type`, as a list. Zero counts never qualify. |
| D6 | `ExamBlueprintJson.cs` | `public static class ExamBlueprintJson` | `public static string SerializeTypeCounts(IEnumerable<ExamTypeCount> typeCounts)` keeps `Count > 0`, orders by `Type`, and serialises with `QuestionJson.SerializerOptions`. `public static List<ExamTypeCount> DeserializeTypeCounts(string json)` returns the list, or throws `InvalidOperationException("Exam blueprint type counts are empty.")` on null. `public static string SerializeDifficultyMix(ExamDifficultyMix mix)`. `public static ExamDifficultyMix DeserializeDifficultyMix(string json)` (same null handling). |
| D7 | `ExamBlueprint.cs` | `public class ExamBlueprint : AuditEntity, IAuditedEntity` | Properties, all `{ get; private set; }`: `Guid SubjectId`, `Guid? UnitId`, `string TypeCounts = "[]"`, `string? DifficultyMix`, `int QuestionCount`, `int? TimeLimitMinutes`, `int PassMark`. Computed: `public bool IsSubjectDefault => UnitId is null;`. Methods: `public List<ExamTypeCount> GetTypeCounts() => ExamBlueprintJson.DeserializeTypeCounts(TypeCounts);`, `public ExamDifficultyMix? GetDifficultyMix() => DifficultyMix is null ? null : ExamBlueprintJson.DeserializeDifficultyMix(DifficultyMix);`, `private ExamBlueprint(Guid id, Guid? createdBy) : base(id, createdBy) { }`. Factories, `Update` and `Delete` are under Domain behaviour. |
| D8 | `IExamBlueprintRepository.cs` | `public interface IExamBlueprintRepository : IRepository<ExamBlueprint> { }` | The base covers every read. |
| D9 | `api/Elmanhg.Domain/Questions/ServableQuestionCount.cs` | `public sealed record ServableQuestionCount(Guid UnitId, QuestionType Type, int Count);` (namespace `Elmanhg.Domain.Questions`) | |

### Application — `api/Elmanhg.Application/ExamBlueprints/`
| # | Path | Type | Contract |
|---|---|---|---|
| A1 | `../Shared/Options/ExamBlueprintsOptions.cs` | `public sealed class ExamBlueprintsOptions` | `public const string SectionName = "ExamBlueprints";` · `[Range(1, 500)] public int MaxQuestionCount { get; set; } = 100;` · `[Range(1, 1440)] public int MaxTimeLimitMinutes { get; set; } = 300;` |
| A2 | `Shared/ExamBlueprintInput.cs` (ns `Elmanhg.Application.ExamBlueprints.Shared`) | three records | `public sealed record ExamBlueprintInput(List<ExamTypeCountInput> TypeCounts, ExamDifficultyMixInput? DifficultyMix, int? TimeLimitMinutes, int PassMark);` · `public sealed record ExamTypeCountInput(QuestionType? Type, int Count);` · `public sealed record ExamDifficultyMixInput(int EasyPercent, int MediumPercent, int HardPercent);` |
| A3 | `Shared/ExamBlueprintInputValidator.cs` | `public sealed class ExamBlueprintInputValidator : AbstractValidator<ExamBlueprintInput>`, ctor `(IOptions<ExamBlueprintsOptions> examBlueprintsOptions)` | Constants: `// Exam scores are out of 100 (PRD §7.4), so a pass mark is a percentage.` `private const int PassMarkMax = 100;` and `private const int PercentMax = 100;`. Rules, in order: (1) `RuleFor(x => x.TypeCounts).Must(list => list is not null && list.Sum(x => (long)x.Count) >= 1).WithErrorCode(ErrorCodes.ExamBlueprintEmpty)`. (2) `.Must(list => list is null \|\| list.Sum(x => (long)x.Count) <= options.MaxQuestionCount).WithErrorCode(ErrorCodes.ExamBlueprintTooLarge)`. (3) `.Must(list => list is null \|\| list.Where(x => x.Type.HasValue).GroupBy(x => x.Type).All(x => x.Count() == 1)).WithErrorCode(ErrorCodes.ExamBlueprintTypeDuplicate)`. (4) `RuleForEach(x => x.TypeCounts).ChildRules(item => { item.RuleFor(x => x.Type).ValidateRequired(ErrorCodes.QuestionTypeRequired).IsInEnum().WithErrorCode(ErrorCodes.QuestionTypeInvalid); item.RuleFor(x => x.Count).ValidateRange(0, options.MaxQuestionCount, ErrorCodes.ExamBlueprintCountInvalid); })` (Morabh `RecordAppEventsValidator` pattern). (5) `RuleFor(x => x.DifficultyMix).ChildRules(mix => { mix.RuleFor(x => x.EasyPercent).ValidateRange(0, PercentMax, ErrorCodes.ExamBlueprintDifficultyMixInvalid); …Medium…; …Hard…; mix.RuleFor(x => x).Must(x => x.EasyPercent + x.MediumPercent + x.HardPercent == PercentMax).WithErrorCode(ErrorCodes.ExamBlueprintDifficultyMixInvalid); }).When(x => x.DifficultyMix is not null)`. (6) `RuleFor(x => x.TimeLimitMinutes.GetValueOrDefault()).ValidateRange(1, options.MaxTimeLimitMinutes, ErrorCodes.ExamBlueprintTimeLimitInvalid).When(x => x.TimeLimitMinutes.HasValue).OverridePropertyName(nameof(ExamBlueprintInput.TimeLimitMinutes))`. (7) `RuleFor(x => x.PassMark).ValidateRange(1, PassMarkMax, ErrorCodes.ExamBlueprintPassMarkInvalid)`. |
| A4 | `Shared/ExamBlueprintShapeGenerator.cs` | `public static class ExamBlueprintShapeGenerator` | `public static ExamBlueprintShape Generate(ExamBlueprintInput input) => new(input.TypeCounts.Select(x => new ExamTypeCount(x.Type.GetValueOrDefault(), x.Count)).ToList(), input.DifficultyMix is null ? null : new ExamDifficultyMix(input.DifficultyMix.EasyPercent, input.DifficultyMix.MediumPercent, input.DifficultyMix.HardPercent), input.TimeLimitMinutes, input.PassMark);` |
| A5 | `Shared/ExamBlueprintResult.cs` | admin-facing (no `LocalizedText`) | `public sealed record ExamBlueprintResult(Guid Id, Guid SubjectId, Guid? UnitId, List<ExamTypeCountResult> TypeCounts, ExamDifficultyMixResult? DifficultyMix, int QuestionCount, int? TimeLimitMinutes, int PassMark) : IAuditableResult { Guid? IAuditableResult.AuditResourceId => Id; }` · `public sealed record ExamTypeCountResult(QuestionType Type, int Count);` · `public sealed record ExamDifficultyMixResult(int EasyPercent, int MediumPercent, int HardPercent);` |
| A6 | `Shared/SubjectExamBlueprintsResult.cs` | admin-facing | `public sealed record SubjectExamBlueprintsResult(Guid SubjectId, string SubjectName, ExamBlueprintResult? DefaultBlueprint, List<ExamTypeCountResult> Servable, List<UnitExamBlueprintResult> Units);` · `public sealed record UnitExamBlueprintResult(Guid UnitId, string Name, int Order, ExamBlueprintResult? Blueprint, List<ExamTypeCountResult> Servable);` |
| A7 | `Shared/ServableTypeCounts.cs` | `public static class ServableTypeCounts` | `public static Dictionary<QuestionType, int> ForSubject(IEnumerable<ServableQuestionCount> counts)` sums by `Type` over all units. `public static Dictionary<QuestionType, int> ForUnit(IEnumerable<ServableQuestionCount> counts, Guid unitId)` filters `UnitId == unitId` and sums by `Type`. `public static List<ExamTypeCountResult> ToResults(IReadOnlyDictionary<QuestionType, int> available) => Enum.GetValues<QuestionType>().Select(x => new ExamTypeCountResult(x, available.GetValueOrDefault(x))).ToList();` (every type, zeros included, in enum order). |
| A8 | `Shared/ExamBlueprintResultGenerator.cs` | `public static class ExamBlueprintResultGenerator` | `public static ExamBlueprintResult Generate(ExamBlueprint blueprint)` maps `GetTypeCounts()` to `ExamTypeCountResult` and `GetDifficultyMix()` to `ExamDifficultyMixResult?`. `public static SubjectExamBlueprintsResult GenerateOverview(Subject subject, List<CurriculumUnit> units, List<ExamBlueprint> blueprints, List<ServableQuestionCount> counts)`: default = `blueprints.FirstOrDefault(x => x.UnitId == null)`; each unit in the given order becomes `new UnitExamBlueprintResult(unit.Id, unit.Name, unit.Order, blueprint for unit.Id or null, ToResults(ForUnit(counts, unit.Id)))`; subject servable = `ToResults(ForSubject(counts))`. Blueprints whose `UnitId` is not among `units` are ignored. |
| A9 | `GetSubjectExamBlueprints/GetSubjectExamBlueprintsQuery.cs` | `public sealed record GetSubjectExamBlueprintsQuery(Guid SubjectId) : IRequest<SubjectExamBlueprintsResult>;` | |
| A10 | `GetSubjectExamBlueprints/GetSubjectExamBlueprintsValidator.cs` | `sealed class : AbstractValidator<GetSubjectExamBlueprintsQuery>` | `RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);` |
| A11 | `GetSubjectExamBlueprints/GetSubjectExamBlueprintsHandler.cs` | `public sealed class GetSubjectExamBlueprintsHandler(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionRepository questionRepository) : IRequestHandler<GetSubjectExamBlueprintsQuery, SubjectExamBlueprintsResult>` | 1. `subject = subjectRepository.GetByIdAsync(request.SubjectId, cancellationToken, asNoTracking: true)`; if null, throw `NotFoundCoreException(ErrorCodes.SubjectNotFound)`. 2. `units = unitRepository.FindAsync(x => x.SubjectId == subject.Id, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true)`. 3. `blueprints = examBlueprintRepository.FindAsync(x => x.SubjectId == subject.Id, cancellationToken, asNoTracking: true)`. 4. `counts = questionRepository.CountServableByUnitAndTypeAsync(subject.Id, cancellationToken)`. 5. `return ExamBlueprintResultGenerator.GenerateOverview(subject, units, blueprints, counts)`. |
| A12 | `SaveSubjectExamBlueprint/SaveSubjectExamBlueprintCommand.cs` | `public sealed record SaveSubjectExamBlueprintCommand(Guid SubjectId, ExamBlueprintInput Blueprint) : IRequest<ExamBlueprintResult>, IAuditableCommand` | `AuditAction => "ExamBlueprint.SaveSubjectDefault"`, `AuditResourceType => "ExamBlueprint"`, `AuditResourceId => null`. |
| A13 | `SaveSubjectExamBlueprint/SaveSubjectExamBlueprintValidator.cs` | ctor `(IOptions<ExamBlueprintsOptions> examBlueprintsOptions)` | `RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);` `RuleFor(x => x.Blueprint).NotNull().WithErrorCode(ErrorCodes.ExamBlueprintEmpty).SetValidator(new ExamBlueprintInputValidator(examBlueprintsOptions));` |
| A14 | `SaveSubjectExamBlueprint/SaveSubjectExamBlueprintHandler.cs` | `public sealed class SaveSubjectExamBlueprintHandler(ISubjectRepository subjectRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionRepository questionRepository, ICurrentUserService currentUserService) : IRequestHandler<SaveSubjectExamBlueprintCommand, ExamBlueprintResult>` | 1. If `UserId` is null or default, throw `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`. 2. Load the subject with `asNoTracking: true`; if null, throw `NotFoundCoreException(ErrorCodes.SubjectNotFound)`. 3. `servable = ServableTypeCounts.ForSubject(await questionRepository.CountServableByUnitAndTypeAsync(subject.Id, ...))`. 4. `shape = ExamBlueprintShapeGenerator.Generate(request.Blueprint)`. 5. `blueprint = examBlueprintRepository.FirstOrDefaultAsync(x => x.SubjectId == subject.Id && x.UnitId == null, cancellationToken)` (tracked). 6. If null, `blueprint = ExamBlueprint.CreateForSubject(subject, shape, servable, userId)` and `AddAsync`. Otherwise `blueprint.Update(shape, servable, userId)`. 7. `examBlueprintRepository.SaveChangesAsync`. 8. `return ExamBlueprintResultGenerator.Generate(blueprint)`. |
| A15 | `SaveUnitExamBlueprint/SaveUnitExamBlueprintCommand.cs` | `public sealed record SaveUnitExamBlueprintCommand(Guid UnitId, ExamBlueprintInput Blueprint) : IRequest<ExamBlueprintResult>, IAuditableCommand` | `AuditAction => "ExamBlueprint.SaveUnit"`, `AuditResourceType => "ExamBlueprint"`, `AuditResourceId => null`. |
| A16 | `SaveUnitExamBlueprint/SaveUnitExamBlueprintValidator.cs` | same ctor | `RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);` plus the same `Blueprint` rule as A13. |
| A17 | `SaveUnitExamBlueprint/SaveUnitExamBlueprintHandler.cs` | `public sealed class SaveUnitExamBlueprintHandler(ICurriculumUnitRepository unitRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionRepository questionRepository, ICurrentUserService currentUserService) : IRequestHandler<SaveUnitExamBlueprintCommand, ExamBlueprintResult>` | 1. User guard (as A14). 2. `unit = unitRepository.GetByIdAsync(request.UnitId, cancellationToken, asNoTracking: true)`; if null, throw `NotFoundCoreException(ErrorCodes.UnitNotFound)`. 3. `servable = ServableTypeCounts.ForUnit(await questionRepository.CountServableByUnitAndTypeAsync(unit.SubjectId, ...), unit.Id)`. 4. Shape. 5. `FirstOrDefaultAsync(x => x.UnitId == unit.Id, cancellationToken)`. 6. `CreateForUnit(unit, shape, servable, userId)` + `AddAsync`, or `Update`. 7. Save once. 8. Generate. |
| A18 | `DeleteExamBlueprint/DeleteExamBlueprintCommand.cs` | `public sealed record DeleteExamBlueprintCommand(Guid ExamBlueprintId) : IRequest, IAuditableCommand` | `AuditAction => "ExamBlueprint.Delete"`, `AuditResourceType => "ExamBlueprint"`, `AuditResourceId => ExamBlueprintId`. |
| A19 | `DeleteExamBlueprint/DeleteExamBlueprintValidator.cs` | | `RuleFor(x => x.ExamBlueprintId).ValidateRequired(ErrorCodes.ExamBlueprintIdRequired);` |
| A20 | `DeleteExamBlueprint/DeleteExamBlueprintHandler.cs` | `public sealed class DeleteExamBlueprintHandler(IExamBlueprintRepository examBlueprintRepository, ICurrentUserService currentUserService) : IRequestHandler<DeleteExamBlueprintCommand>` | 1. User guard. 2. `GetByIdAsync(request.ExamBlueprintId, cancellationToken)` (tracked); if null, throw `NotFoundCoreException(ErrorCodes.ExamBlueprintNotFound)`. 3. `blueprint.Delete(userId)`. 4. Save once. |

### Infrastructure
| # | Path | Contract |
|---|---|---|
| I1 | `api/Elmanhg.Infrastructure/ExamBlueprints/ExamBlueprintRepository.cs` | `public class ExamBlueprintRepository(AppDbContext context) : Repository<ExamBlueprint>(context), IExamBlueprintRepository { }` |
| I2 | `AppDbContext.ConfigureExamBlueprints` (private static, inside the existing file) | `modelBuilder.Entity<ExamBlueprint>(builder => { builder.Property(x => x.TypeCounts).IsRequired().HasColumnType("jsonb"); builder.Property(x => x.DifficultyMix).HasColumnType("jsonb"); builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict); builder.HasOne<CurriculumUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict); builder.HasIndex(x => x.SubjectId).IsUnique().HasFilter("\"UnitId\" IS NULL AND \"IsDeleted\" = false").HasDatabaseName(SubjectDefaultBlueprintIndex); builder.HasIndex(x => x.UnitId).IsUnique().HasFilter("\"UnitId\" IS NOT NULL AND \"IsDeleted\" = false").HasDatabaseName(UnitBlueprintIndex); });` |
| I3 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddExamBlueprints.cs` (+ `.Designer.cs`) | Generate with `dotnet ef migrations add AddExamBlueprints -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Review it: it must contain only `CreateTable ExamBlueprints` + FKs + indexes, with no Drop or Rename. |

### API
| # | Path | Contract |
|---|---|---|
| P1 | `api/Elmanhg.Api/Controllers/ExamBlueprints/ExamBlueprintsController.cs` | `namespace Elmanhg.Api.Controllers.ExamBlueprints;` `[ApiController] [Route("api/exam-blueprints")] [Authorize] public class ExamBlueprintsController(IMediator mediator) : ControllerBase`. Four actions (see API surface). Each has `[Authorize(Policy = DefaultCodes.BlueprintsManage)]` and `[ProducesResponseType…]`, and passes the `CancellationToken` to `Send`. Save actions bind `[FromBody] ExamBlueprintInput request` (the body is the input; no separate Request record). |

### Tests (api) — listed in the Test plan
| # | Path |
|---|---|
| T1 | `api/Elmanhg.Tests/Domain/ExamBlueprints/ExamBlueprintTests.cs` |
| T2 | `api/Elmanhg.Tests/Domain/ExamBlueprints/ExamBlueprintShortfallTests.cs` |
| T3 | `api/Elmanhg.Tests/Builders/ExamBlueprintBuilder.cs`: `public sealed class ExamBlueprintBuilder` with `Subject` (`Subject.Create("Physics",1,…)`), `Unit` (`CurriculumUnit.Create(Subject,"Mechanics",1,…)`), `static ExamBlueprintShape Shape(params ExamTypeCount[] counts)` (mix null, time 45, pass 50), `static Dictionary<QuestionType,int> Plenty()` (100 of every type), `ExamBlueprint BuildDefault()` and `ExamBlueprint BuildForUnit()` (Mcq 2, via the factories). |
| T4 | `api/Elmanhg.Tests/Application/Features/ExamBlueprints/Shared/ExamBlueprintInputValidatorTests.cs` |
| T5 | `…/ExamBlueprints/GetSubjectExamBlueprints/GetSubjectExamBlueprintsHandlerTests.cs`, `GetSubjectExamBlueprintsValidatorTests.cs` |
| T6 | `…/ExamBlueprints/SaveSubjectExamBlueprint/SaveSubjectExamBlueprintHandlerTests.cs`, `SaveSubjectExamBlueprintValidatorTests.cs` |
| T7 | `…/ExamBlueprints/SaveUnitExamBlueprint/SaveUnitExamBlueprintHandlerTests.cs`, `SaveUnitExamBlueprintValidatorTests.cs` |
| T8 | `…/ExamBlueprints/DeleteExamBlueprint/DeleteExamBlueprintHandlerTests.cs`, `DeleteExamBlueprintValidatorTests.cs` |
| T9 | `api/Elmanhg.Tests/Integration/ExamBlueprints/ExamBlueprintsEndpointTests.cs` (`public sealed class ExamBlueprintsEndpointTests(ApiFactory factory)`, same shape as `UnitsEndpointTests`) |
| T10 | `api/Elmanhg.Tests/Integration/ExamBlueprints/ExamBlueprintTestData.cs`: `SeedServableMcqAsync(ApiFactory factory, Guid unitId, int count, CancellationToken ct)` creates a Published lesson via `ContentTestData.SeedLessonInStateAsync` and `count` × `QuestionTestData.SeedQuestionAsync(approved: true)`. `ReadBlueprintsAsync(ApiFactory factory, Guid subjectId, CancellationToken ct)` returns every row for the subject with `IgnoreQueryFilters()` and `AsNoTracking()`. |
| T11 | `api/Elmanhg.Tests/Integration/Persistence/ExamBlueprintPersistenceTests.cs` |

### Web — `web/src/features/blueprints/`
| # | Path | Contract |
|---|---|---|
| W1 | `schemas/blueprintsSearchSchema.ts` | `export const blueprintsSearchSchema = z.object({ subjectId: z.uuid().optional().catch(undefined) }); export type BlueprintsSearch = z.infer<typeof blueprintsSearchSchema>;` |
| W2 | `schemas/examBlueprintSchema.ts` | Constants: `// mirrors ExamBlueprints:MaxQuestionCount` `export const blueprintMaxQuestionCount = 100;`, `// mirrors ExamBlueprints:MaxTimeLimitMinutes` `export const blueprintMaxTimeLimitMinutes = 300;`, `export const passMarkMax = 100;`. Schema: `z.object({ counts: z.object({ Mcq: z.string(), Multi: z.string(), TrueFalse: z.string(), Fill: z.string(), Short: z.string() }), timeLimitMinutes: z.string(), passMark: z.string(), difficultyMix: z.object({ enabled: z.boolean(), easy: z.string(), medium: z.string(), hard: z.string() }) }).superRefine(...)`. Rules (message = translation key): each count not matching `/^\d+$/` or above max → path `['counts', type]`, `blueprints:editor.errors.countInvalid`. When all counts are valid: total 0 → path `['counts']`, `blueprints:editor.errors.empty`; total above max → `['counts']`, `…tooLarge`. `timeLimitMinutes` not `''` and (not digits, below 1, or above max) → `…timeLimitInvalid`. `passMark` not digits or outside 1..100 → `…passMarkInvalid`. When `difficultyMix.enabled`: each percent not digits or above 100 → `['difficultyMix', key]`, `…percentInvalid`; if all valid and the sum ≠ 100 → `['difficultyMix']`, `…mixSum`. `export type ExamBlueprintValues = z.infer<typeof examBlueprintSchema>;` |
| W3 | `api/blueprintValues.ts` | `export interface TypeShortfall { type: QuestionType; required: number; available: number; missing: number }` · `export function findShortfall(required: readonly ExamTypeCountResult[], available: readonly ExamTypeCountResult[]): TypeShortfall[]` (only `count > 0` and `count > available`, in `questionTypes` order; a missing available entry counts as 0; mirrors `ExamBlueprintShortfall.Find`) · `export function countsFromValues(values: ExamBlueprintValues): ExamTypeCountResult[]` (valid digit strings only; anything else counts as 0) · `export const emptyBlueprintValues: ExamBlueprintValues` (all counts `'0'`, time `''`, pass `'50'`, mix `{ enabled: false, easy: '', medium: '', hard: '' }`) · `export function toFormValues(blueprint: ExamBlueprintResult \| null \| undefined): ExamBlueprintValues` · `export function toBlueprintInput(values: ExamBlueprintValues): ExamBlueprintInput` (`typeCounts` = types with count > 0; `timeLimitMinutes` `''`→`null`; mix disabled → `null`). |
| W4 | `hooks/useBlueprintsSearch.ts` | `getRouteApi('/admin/blueprints')`; returns `{ subjectId: string \| undefined, selectSubject: (subjectId: string) => void }` (navigates `search: { subjectId }`). |
| W5 | `hooks/useBlueprintSave.ts` | `export function useBlueprintSave(subjectId: string): { saveSubjectDefault: (input: ExamBlueprintInput) => Promise<void>; saveUnit: (unitId: string, input: ExamBlueprintInput) => Promise<void> }`. Uses generated `useSaveSubjectExamBlueprint` and `useSaveUnitExamBlueprint`. `onSuccess` shows `toast(t('toasts.saved'))`. `onSettled` invalidates `getGetSubjectExamBlueprintsQueryKey(subjectId)`, so a server shortfall refreshes the counts. Errors propagate to `Form` (mapped by `applyServerErrors`). |
| W6 | `hooks/useBlueprintDelete.ts` | `export function useBlueprintDelete(subjectId: string): (examBlueprintId: string) => void`. Uses generated `useDeleteExamBlueprint`. `onSuccess` shows toast `toasts.deleted` and invalidates the overview. `onError` shows `toast.error` with `common:errors.<code>` (same as `useContentFeedback`). |
| W7 | `components/ShortfallNotice.tsx` | Props `{ title: string; shortfalls: readonly TypeShortfall[] }`. Returns null when empty. Otherwise a `<div aria-live="polite" className="rounded-md border border-warning bg-warning-soft px-3.5 py-3 text-caption text-text">` with the bold title, then one line per item: `t('editor.shortfallLine', { type: t(\`questions:types.${type}\`), required, available, missing })`. |
| W8 | `components/TypeCountsTable.tsx` | Props `{ available: readonly ExamTypeCountResult[] }`. Reads the form through `useFormContext<ExamBlueprintValues>()`. A table in an `overflow-x-auto` wrapper, headers النوع / العدد المطلوب / المتاح. One row per `questionTypes`. The count cell is an `Input` (`@/shared/ui/input`) registered to `counts.<type>`, with `dir="ltr"`, `inputMode="numeric"`, `aria-label={t('editor.countFor', { type })}`, `aria-invalid`. The row gets `bg-warning-soft` when count > available. The available cell shows `t('editor.number', { value })`. A footer row "المجموع" shows the sum of valid counts. Below the table, `errors.counts?.message` and each per-row error message appear in `<p className="text-caption text-danger">`. |
| W9 | `components/DifficultyMixFields.tsx` | A plain `<label className="flex min-h-11 items-center gap-2.5 text-ui"><input type="checkbox" className="size-4.5 accent-text" {...register('difficultyMix.enabled')} />` labelled `editor.mixToggle` (same markup as `questions/components/CheckboxField.tsx`, which is not exported). When enabled, three `TextField`s (`@/shared/form/TextField`, `dir="ltr"`) for easy, medium and hard with labels `editor.mixEasy`, `editor.mixMedium`, `editor.mixHard`. `errors.difficultyMix?.message` shows in a `text-danger` caption. |
| W10 | `components/BlueprintEditor.tsx` | Props `{ title: string; caption?: string; defaults: ExamBlueprintValues; available: readonly ExamTypeCountResult[]; onSave: (input: ExamBlueprintInput) => Promise<void>; onCancel?: () => void; actions?: ReactNode; serverErrorFields }`. `useForm({ resolver: zodResolver(examBlueprintSchema), defaultValues: defaults, mode: 'onBlur' })`. A card `rounded-lg border border-border bg-surface p-4 shadow-1` holds, in order: `<h2>` title and optional caption; `TypeCountsTable`; a row with `TextField` `timeLimitMinutes` (label `editor.timeLimit`, description `editor.timeLimitHint`, `dir="ltr"`) and `TextField` `passMark` (label `editor.passMark`, `dir="ltr"`); `DifficultyMixFields`; `ShortfallNotice` titled `editor.currentShortfall`, with `findShortfall(countsFromValues(watch()), available)` computed live; `FormRootError`; an actions row (`actions`, then Cancel if `onCancel`, then `SubmitButton` `editor.save` last). Submit: `shortfall = findShortfall(...)`; if non-empty, call `form.setError('root.server', { type: 'client', message: 'errors.EXAM_BLUEPRINT_SHORTFALL' })` and return without calling `onSave`. Otherwise `await onSave(toBlueprintInput(values))`. `serverErrorFields = { EXAM_BLUEPRINT_PASS_MARK_INVALID: 'passMark', EXAM_BLUEPRINT_TIME_LIMIT_INVALID: 'timeLimitMinutes' }` (constant in this file). |
| W11 | `components/UnitBlueprintSection.tsx` | Props `{ unit: UnitExamBlueprintResult; defaultBlueprint: ExamBlueprintResult \| null \| undefined; onSave: (unitId, input) => Promise<void>; onDelete: (examBlueprintId: string) => void }`. Local UI state is `'idle' \| 'creating' \| 'confirmingDelete'`. If the unit has a blueprint: `BlueprintEditor` titled `t('editor.unitTitle', { name })`, `defaults = toFormValues(unit.blueprint)`, `key = unit.blueprint.id`. `actions` is a secondary "استخدام النموذج الافتراضي" button that switches to an inline confirm (`editor.confirmRevert` text + danger "تأكيد" + secondary "إلغاء"), as in `content/components/ItemActions.tsx`; confirming calls `onDelete(unit.blueprint.id)`. If there is no blueprint and state is `'creating'`: `BlueprintEditor` with `defaults = toFormValues(defaultBlueprint)`, `onCancel` back to idle, and after `onSave` resolves back to idle. If there is no blueprint and state is idle: a card with the unit title, then `editor.usesDefault` when a default exists, else `editor.noDefaultForUnit`; a `ShortfallNotice` titled `editor.unitShortfall` for `findShortfall(defaultBlueprint.typeCounts, unit.servable)` when a default exists; and the button `editor.createForUnit`. |
| W12 | `components/SubjectBlueprints.tsx` | Props `{ subjectId: string }`. `useGetSubjectExamBlueprints(subjectId)`. Pending: `ContentListSkeleton` (from `@/features/content`) with label `page.loading`. Error: `ContentErrorState` with title `page.errorTitle` and retry = `refetch`. Success: `BlueprintEditor` titled `editor.defaultTitle`, with caption `editor.defaultUnsaved` when `defaultBlueprint` is null, `key = defaultBlueprint?.id ?? 'new'`, `available = data.servable`, `onSave = saveSubjectDefault`; then `UnitBlueprintSection` for each `data.units`. When `units` is empty, show `page.noUnits`. |
| W13 | `components/SubjectPicker.tsx` | Props `{ subjects: readonly SubjectResult[]; value: string; onChange: (id: string) => void }`. A labelled native `<select>` (label `page.subject`) with the classes of `questions/components/SelectField.tsx`. |
| W14 | `components/BlueprintsEmptyState.tsx` | A text `page.noSubjects` and a `<Link to="/admin/content">` labelled `page.goToContent`. |
| W15 | `pages/ExamBlueprintsPage.tsx` | `useGetSubjects()` and `useBlueprintsSearch()`. `<section className="flex flex-col gap-3">` holds `<h1>` `page.title` and the caption `page.intro`. Pending: skeleton. Error: `ContentErrorState` (`page.subjectsErrorTitle`, retry). Empty: `BlueprintsEmptyState`. Otherwise `selected = subjects.find(x => x.id === subjectId) ?? subjects[0]`, then `SubjectPicker` and `<SubjectBlueprints key={selected.id} subjectId={selected.id} />`. Subjects are in API order. |
| W16 | `i18n/ar.json`, `i18n/en.json` | Keys and strings below. |
| W17 | `locales.ts` | `export const blueprintsLocales = { ar, en };` |
| W18 | `index.ts` | `export { ExamBlueprintsPage } from './pages/ExamBlueprintsPage'; export { blueprintsSearchSchema, type BlueprintsSearch } from './schemas/blueprintsSearchSchema'; export { findShortfall, type TypeShortfall } from './api/blueprintValues';` |
| W19 | `web/src/test/blueprintFixtures.ts` | `blueprint(overrides?)`, `overview(overrides?)` and `servable(partial: Partial<Record<QuestionType, number>>)` returning `ExamTypeCountResult[]` for all 5 types. |
| W20 | Tests: `pages/ExamBlueprintsPage.test.tsx`, `components/BlueprintEditor.test.tsx`, `components/UnitBlueprintSection.test.tsx`, `api/blueprintValues.test.ts`, `schemas/examBlueprintSchema.test.ts`, `schemas/blueprintsSearchSchema.test.ts` | See the Test plan. |

### Docs
| # | Path | Content |
|---|---|---|
| X1 | `docs/exam-blueprints.md` (new) | Sections: **Model** (a field table mirroring D7, with the PRD §15 column names `subject_id`, `unit_id?`, `type_counts_json`, `difficulty_mix_json?`, `question_count`, `time_limit_min?`, `pass_mark`; the canonical JSON examples from Decision 10; both partial unique indexes). **Rules** (Decisions 4–9 and 12; shortfall = required > servable for any type, `ServableQuestionSpecification`; the mix is a target). **Resolution for exams** (Decision 17: unit, then subject default, then no exam; the exam start re-checks with `ExamBlueprintShortfall.Find`; the session copies the time limit and pass mark at start; #82 merges `GetTypeCounts()` proportionally using `QuestionCount`). **Orphans** (Decision 16). **Concurrency** (Decision 15; last write wins on update). **API** (the table from API surface). **Error codes** (the table below). **Options** (`ExamBlueprints:MaxQuestionCount` 100, `ExamBlueprints:MaxTimeLimitMinutes` 300). **Admin screen** (W15 flow). **Audit** (the three actions). |
| X2 | `docs/PRD.md` §15 | `Subject(id, name, order)`, `Unit(id, subject_id, name, order)`, and `ExamBlueprint(id, subject_id, unit_id?, type_counts_json, difficulty_mix_json?, question_count, time_limit_min?, pass_mark)  -- unit_id null = the subject default; one default per subject, one per unit (docs/exam-blueprints.md)`. |
| X3 | `docs/PRD.md` §10.2 | Append after the Validation line: "The subject default is checked against the subject's whole servable pool; a unit blueprint against its unit's pool. The difficulty mix is a target (whole percentages summing to 100) and is not part of the save check. Pass mark is 1–100; time limit, when set, is at least 1 minute. A unit blueprint can be removed, returning the unit to the subject default; the subject default cannot be removed. Details: `docs/exam-blueprints.md`." |
| X4 | `docs/audit-log.md` | Rows: `SaveSubjectExamBlueprint \| \`ExamBlueprint.SaveSubjectDefault\` \| ExamBlueprint \| result`, `SaveUnitExamBlueprint \| \`ExamBlueprint.SaveUnit\` \| ExamBlueprint \| result`, `DeleteExamBlueprint \| \`ExamBlueprint.Delete\` \| ExamBlueprint \| command`. Add `ExamBlueprint` to the audited entities list. |
| X5 | `docs/claude-design-prompt.md` §4 | Replace the `#/admin/blueprints` bullet with: "`#/admin/blueprints` exam blueprints per subject (a subject select, kept in the URL): the subject's default blueprint card, then one card per unit. A blueprint card has a table of question type / required count / available servable questions, with short rows highlighted and a total row; the time limit in minutes (optional); the pass mark; an optional difficulty mix (easy/medium/hard %, summing to 100); a live «عجز حالي» notice listing each short type as «مطلوب، متاح، ينقص»; and «حفظ», which is refused while any type is short. A unit without its own blueprint shows «تستخدم النموذج الافتراضي» (or that no exam is available when the subject has no default), a warning when the default is short for that unit, and «إنشاء نموذج للوحدة», which opens the editor prefilled from the default. A unit blueprint offers «استخدام النموذج الافتراضي», confirmed inline, to remove it. Loading skeleton, error with retry, and an empty state linking to content when there are no subjects." |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|---|---|---|---|---|
| `DomainErrorCodes.ExamBlueprintShortfall` | `EXAM_BLUEPRINT_SHORTFALL` | `ExamBlueprint.CreateForSubject/CreateForUnit/Update` | `BusinessRuleViolationCoreException` (context `types`) | 400 |
| `DomainErrorCodes.ExamBlueprintDefaultNotDeletable` | `EXAM_BLUEPRINT_DEFAULT_NOT_DELETABLE` | `ExamBlueprint.Delete` | `BusinessRuleViolationCoreException` | 400 |
| `ErrorCodes.ExamBlueprintNotFound` | `EXAM_BLUEPRINT_NOT_FOUND` | `DeleteExamBlueprintHandler` | `NotFoundCoreException` | 404 |
| `ErrorCodes.ExamBlueprintIdRequired` | `EXAM_BLUEPRINT_ID_REQUIRED` | `DeleteExamBlueprintValidator` | validation | 422 |
| `ErrorCodes.ExamBlueprintEmpty` | `EXAM_BLUEPRINT_EMPTY` | input validator, save validators (null body) | validation | 422 |
| `ErrorCodes.ExamBlueprintTooLarge` | `EXAM_BLUEPRINT_TOO_LARGE` | input validator | validation | 422 |
| `ErrorCodes.ExamBlueprintTypeDuplicate` | `EXAM_BLUEPRINT_TYPE_DUPLICATE` | input validator | validation | 422 |
| `ErrorCodes.ExamBlueprintCountInvalid` | `EXAM_BLUEPRINT_COUNT_INVALID` | input validator | validation | 422 |
| `ErrorCodes.ExamBlueprintDifficultyMixInvalid` | `EXAM_BLUEPRINT_DIFFICULTY_MIX_INVALID` | input validator | validation | 422 |
| `ErrorCodes.ExamBlueprintTimeLimitInvalid` | `EXAM_BLUEPRINT_TIME_LIMIT_INVALID` | input validator | validation | 422 |
| `ErrorCodes.ExamBlueprintPassMarkInvalid` | `EXAM_BLUEPRINT_PASS_MARK_INVALID` | input validator | validation | 422 |
| `ErrorCodes.ExamBlueprintModifiedConcurrently` | `EXAM_BLUEPRINT_MODIFIED_CONCURRENTLY` | `AppDbContext.SaveChangesAsync` (unique index) | `ConflictCoreException` | 409 |
| reused | `SUBJECT_ID_REQUIRED`, `SUBJECT_NOT_FOUND`, `UNIT_ID_REQUIRED`, `UNIT_NOT_FOUND`, `QUESTION_TYPE_REQUIRED`, `QUESTION_TYPE_INVALID`, `USER_NOT_AUTHENTICATED` | | | 422/404/422/404/422/422/401 |

Resource strings. The same text goes into `Messages.{ar,en}.resx` and `web/src/shared/i18n/{ar,en}.json` `errors`. On the web, the `{types}` placeholder is dropped: the web string is the part before the colon.
| Code | ar | en |
|---|---|---|
| EXAM_BLUEPRINT_SHORTFALL | resx: `لم يتم الحفظ — عدد الأسئلة المتاحة غير كافٍ: {types}` · web: `لم يتم الحفظ — عدد الأسئلة المتاحة غير كاف لبعض الأنواع.` | resx: `Not saved: not enough servable questions: {types}` · web: `Not saved: some types do not have enough servable questions.` |
| EXAM_BLUEPRINT_DEFAULT_NOT_DELETABLE | `لا يمكن حذف النموذج الافتراضي للمادة.` | `The subject's default blueprint cannot be removed.` |
| EXAM_BLUEPRINT_NOT_FOUND | `نموذج الامتحان غير موجود.` | `Exam blueprint not found.` |
| EXAM_BLUEPRINT_ID_REQUIRED | `اختر نموذج الامتحان.` | `Choose an exam blueprint.` |
| EXAM_BLUEPRINT_EMPTY | `يجب تحديد سؤال واحد على الأقل.` | `Choose at least one question.` |
| EXAM_BLUEPRINT_TOO_LARGE | `عدد أسئلة النموذج أكبر من الحد المسموح.` | `The blueprint has more questions than allowed.` |
| EXAM_BLUEPRINT_TYPE_DUPLICATE | `كل نوع يظهر مرة واحدة فقط.` | `Each question type can appear only once.` |
| EXAM_BLUEPRINT_COUNT_INVALID | `عدد الأسئلة غير صالح.` | `The question count is not valid.` |
| EXAM_BLUEPRINT_DIFFICULTY_MIX_INVALID | `نسب الصعوبة يجب أن تكون بين 0 و100 ومجموعها 100.` | `Difficulty percentages must be 0–100 and add up to 100.` |
| EXAM_BLUEPRINT_TIME_LIMIT_INVALID | `زمن الامتحان غير صالح.` | `The time limit is not valid.` |
| EXAM_BLUEPRINT_PASS_MARK_INVALID | `درجة النجاح يجب أن تكون بين 1 و100.` | `The pass mark must be between 1 and 100.` |
| EXAM_BLUEPRINT_MODIFIED_CONCURRENTLY | `تم تعديل النموذج في نفس الوقت. أعد المحاولة.` | `The blueprint was changed at the same time. Try again.` |

## Domain behaviour
```csharp
public static ExamBlueprint CreateForSubject(Subject subject, ExamBlueprintShape shape, IReadOnlyDictionary<QuestionType, int> servable, Guid createdBy)
{
    EnsureNoShortfall(shape, servable);
    var blueprint = new ExamBlueprint(Guid.NewGuid(), createdBy) { SubjectId = subject.Id, UnitId = null };
    blueprint.Apply(shape);
    return blueprint;
}

public static ExamBlueprint CreateForUnit(CurriculumUnit unit, ExamBlueprintShape shape, IReadOnlyDictionary<QuestionType, int> servable, Guid createdBy)
{   // same, with SubjectId = unit.SubjectId, UnitId = unit.Id
}

public void Update(ExamBlueprintShape shape, IReadOnlyDictionary<QuestionType, int> servable, Guid updatedBy)
{
    EnsureNoShortfall(shape, servable);
    Apply(shape);
    UpdatedBy = updatedBy;
    UpdationDate = DateTimeOffset.UtcNow;
}

public void Delete(Guid deletedBy)
{
    if (IsSubjectDefault)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.ExamBlueprintDefaultNotDeletable);
    }

    SoftDelete();
    UpdatedBy = deletedBy;
    UpdationDate = DateTimeOffset.UtcNow;
}

private void Apply(ExamBlueprintShape shape)
{
    TypeCounts = ExamBlueprintJson.SerializeTypeCounts(shape.TypeCounts);
    QuestionCount = shape.TypeCounts.Where(x => x.Count > 0).Sum(x => x.Count);
    DifficultyMix = shape.DifficultyMix is null ? null : ExamBlueprintJson.SerializeDifficultyMix(shape.DifficultyMix);
    TimeLimitMinutes = shape.TimeLimitMinutes;
    PassMark = shape.PassMark;
}

private static void EnsureNoShortfall(ExamBlueprintShape shape, IReadOnlyDictionary<QuestionType, int> servable)
{
    var shortfalls = ExamBlueprintShortfall.Find(shape.TypeCounts, servable);
    if (shortfalls.Count > 0)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.ExamBlueprintShortfall, context: new Dictionary<string, object> { ["types"] = string.Join(", ", shortfalls.Select(x => $"{x.Type} {x.Available}/{x.Required}")) });
    }
}
```
- `ErrorCodes` here is `Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`, and `BusinessRuleViolationCoreException` comes from `Core.Errors`, as in `Subject.cs`.
- Guard, then mutate, then stamp. A failed `Update` leaves every property unchanged.
- The shape's input validity (non-negative counts, no duplicate types, total ≥ 1, ranges) is enforced by `ExamBlueprintInputValidator`. The entity enforces only the shortfall rule, the default-not-deletable rule and the canonical form.

## API surface
| Method | Route | Name (operationId) | Policy | Body | Response |
|---|---|---|---|---|---|
| GET | `/api/exam-blueprints/subjects/{subjectId:guid}` | `GetSubjectExamBlueprints` | `DefaultCodes.BlueprintsManage` | — | 200 `SubjectExamBlueprintsResult` |
| PUT | `/api/exam-blueprints/subjects/{subjectId:guid}` | `SaveSubjectExamBlueprint` | `DefaultCodes.BlueprintsManage` | `ExamBlueprintInput` | 200 `ExamBlueprintResult` |
| PUT | `/api/exam-blueprints/units/{unitId:guid}` | `SaveUnitExamBlueprint` | `DefaultCodes.BlueprintsManage` | `ExamBlueprintInput` | 200 `ExamBlueprintResult` |
| DELETE | `/api/exam-blueprints/{examBlueprintId:guid}` | `DeleteExamBlueprint` | `DefaultCodes.BlueprintsManage` | — | 200 (empty) |

`ExamBlueprintInput { typeCounts: [{ type: "Mcq", count: 10 }], difficultyMix?: { easyPercent, mediumPercent, hardPercent } \| null, timeLimitMinutes?: int \| null, passMark: int }`.

Postman folder `ExamBlueprints` (after `Progress`, in this order; collection variables `blueprintSubjectId` and `blueprintUnitId` are added):
1. `Create subject for blueprints`: POST `/api/subjects` `{ "name": "Blueprint subject" }`. Test: 200; sets `blueprintSubjectId`.
2. `Create unit for blueprints`: POST `/api/subjects/{{blueprintSubjectId}}/units`. Test: 200; sets `blueprintUnitId`.
3. `Get subject exam blueprints`: GET. Test: 200 and `units.length === 1`.
4. `Save subject exam blueprint (shortfall)`: PUT subjects with `{ "typeCounts": [{ "type": "Mcq", "count": 1 }], "timeLimitMinutes": 45, "passMark": 50 }`. Test: 400 and `code === "EXAM_BLUEPRINT_SHORTFALL"`. The new subject has no servable questions.
5. `Save unit exam blueprint (shortfall)`: PUT units with the same body. Test: 400, same code.
6. `Delete exam blueprint (unknown)`: DELETE `/api/exam-blueprints/00000000-0000-0000-0000-000000000001`. Test: 404 and `code === "EXAM_BLUEPRINT_NOT_FOUND"`.

## Web strings (`blueprints` namespace)
| Key | ar | en |
|---|---|---|
| page.title | نماذج الامتحانات | Exam blueprints |
| page.intro | لا يمكن حفظ نموذج إذا كان عدد الأسئلة القابلة للعرض غير كافٍ لأي نوع. | A blueprint cannot be saved if any type lacks enough servable questions. |
| page.subject | المادة | Subject |
| page.loading | جارٍ تحميل نماذج الامتحانات | Loading exam blueprints |
| page.errorTitle | تعذّر تحميل نماذج الامتحانات | Could not load exam blueprints |
| page.subjectsErrorTitle | تعذّر تحميل المواد | Could not load subjects |
| page.noSubjects | لا توجد مواد بعد. أضف مادة من صفحة المحتوى. | No subjects yet. Add one from the content page. |
| page.goToContent | المحتوى | Content |
| page.noUnits | لا توجد وحدات في هذه المادة بعد. | This subject has no units yet. |
| editor.defaultTitle | النموذج الافتراضي للمادة | Subject default blueprint |
| editor.defaultUnsaved | لم يُحفظ نموذج افتراضي بعد. | No default blueprint saved yet. |
| editor.unitTitle | وحدة: {name} | Unit: {name} |
| editor.type / editor.required / editor.available / editor.total | النوع / العدد المطلوب / المتاح / المجموع | Type / Required / Available / Total |
| editor.countFor | العدد المطلوب — {type} | Required count — {type} |
| editor.number | {value, number} | {value, number} |
| editor.timeLimit / editor.timeLimitHint | الزمن (دقيقة) / اتركه فارغًا لامتحان بلا وقت | Time limit (minutes) / Leave empty for no time limit |
| editor.passMark | درجة النجاح | Pass mark |
| editor.mixToggle / mixEasy / mixMedium / mixHard | توزيع الصعوبة (اختياري) / سهل % / متوسط % / صعب % | Difficulty mix (optional) / Easy % / Medium % / Hard % |
| editor.currentShortfall | عجز حالي: | Current shortfall: |
| editor.unitShortfall | عجز حالي في هذه الوحدة: | Current shortfall in this unit: |
| editor.shortfallLine | {type}: مطلوب {required, number}، متاح {available, number} (ينقص {missing, number}) | {type}: required {required, number}, available {available, number} ({missing, number} short) |
| editor.save / editor.cancel | حفظ / إلغاء | Save / Cancel |
| editor.usesDefault | تستخدم النموذج الافتراضي. | Uses the subject default. |
| editor.noDefaultForUnit | لا يوجد نموذج افتراضي للمادة، فلا يتوفر امتحان لهذه الوحدة. | The subject has no default blueprint, so this unit has no exam. |
| editor.createForUnit | إنشاء نموذج للوحدة | Create a unit blueprint |
| editor.revert / editor.confirmRevert / editor.confirm | استخدام النموذج الافتراضي / حذف نموذج الوحدة؟ ستستخدم الوحدة النموذج الافتراضي للمادة. / تأكيد | Use the subject default / Remove this unit's blueprint? The unit will use the subject default. / Confirm |
| editor.errors.countInvalid / empty / tooLarge / timeLimitInvalid / passMarkInvalid / percentInvalid / mixSum | عدد غير صالح / يجب تحديد سؤال واحد على الأقل / عدد الأسئلة أكبر من ١٠٠ / زمن غير صالح / درجة النجاح بين ١ و١٠٠ / نسبة غير صالحة / مجموع النسب يجب أن يكون ١٠٠ | Invalid count / Choose at least one question / More than 100 questions / Invalid time limit / Pass mark must be 1–100 / Invalid percentage / Percentages must add up to 100 |
| toasts.saved / toasts.deleted | تم حفظ النموذج / تم حذف نموذج الوحدة | Blueprint saved / Unit blueprint removed |

## Test plan

### API — Domain (no doubles)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 1 | ExamBlueprintTests | CreateForSubject_EnoughServable_StoresCanonicalShape | `TypeCounts` == `[{"type":"mcq","count":2},{"type":"fill","count":1}]` from input (Fill 1, Short 0, Mcq 2); `QuestionCount` 3; `UnitId` null; `IsSubjectDefault`; `SubjectId`; `PassMark`; `TimeLimitMinutes` |
| 2 | ExamBlueprintTests | CreateForUnit_EnoughServable_SetsSubjectAndUnit | `SubjectId == unit.SubjectId`, `UnitId == unit.Id`, `!IsSubjectDefault` |
| 3 | ExamBlueprintTests | CreateForSubject_Shortfall_ThrowsExamBlueprintShortfall | `BusinessRuleViolationCoreException`, code `EXAM_BLUEPRINT_SHORTFALL`, `Context["types"]` == `"Mcq 1/2"` |
| 4 | ExamBlueprintTests | CreateForUnit_Shortfall_ThrowsExamBlueprintShortfall | type + code |
| 5 | ExamBlueprintTests | Update_EnoughServable_ReplacesShapeAndStamps | new counts, `DifficultyMix` null after an update without a mix, `UpdatedBy`, `UpdationDate` later than before |
| 6 | ExamBlueprintTests | Update_Shortfall_ThrowsAndKeepsShape | code; `TypeCounts`, `PassMark` and `UpdatedBy` unchanged |
| 7 | ExamBlueprintTests | Delete_UnitBlueprint_SoftDeletes | `IsDeleted`, `UpdatedBy` |
| 8 | ExamBlueprintTests | Delete_SubjectDefault_ThrowsDefaultNotDeletable | code; `IsDeleted` false |
| 9 | ExamBlueprintTests | GetDifficultyMix_Set_RoundTrips | the stored JSON is `{"easyPercent":30,"mediumPercent":50,"hardPercent":20}`; `GetDifficultyMix()` equals the input record |
| 10 | ExamBlueprintTests | GetTypeCounts_RoundTripsStoredJson | equals the canonical list |
| 11 | ExamBlueprintShortfallTests | Find_EnoughForEveryType_ReturnsEmpty | empty |
| 12 | ExamBlueprintShortfallTests | Find_TypeShort_ReturnsRequiredAvailableMissing | (Mcq, 5, 3), Missing 2 |
| 13 | ExamBlueprintShortfallTests | Find_TypeAbsentFromAvailable_CountsAsZero | (Short, 1, 0) |
| 14 | ExamBlueprintShortfallTests | Find_ZeroRequirement_IsIgnored | empty for Short 0 with no availability |
| 15 | ExamBlueprintShortfallTests | Find_SeveralShort_OrderedByType | [Mcq, Fill] for input order Fill, Mcq |

### API — Application (NSubstitute at repositories; every throwing path asserts `SaveChangesAsync` `DidNotReceive()`)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 16 | ExamBlueprintInputValidatorTests | Validate_ValidInput_Passes | no errors (with mix, time and pass) |
| 17 | 〃 | Validate_TypeMissing_FailsQuestionTypeRequired | code |
| 18 | 〃 | Validate_TypeUndefined_FailsQuestionTypeInvalid | `(QuestionType)99` → code |
| 19 | 〃 | Validate_NegativeCount_FailsCountInvalid | code |
| 20 | 〃 | Validate_CountAboveMax_FailsCountInvalid | 101 → code |
| 21 | 〃 | Validate_DuplicateType_FailsTypeDuplicate | code |
| 22 | 〃 | Validate_AllZero_FailsEmpty | code |
| 23 | 〃 | Validate_NullTypeCounts_FailsEmpty | code |
| 24 | 〃 | Validate_TotalAboveMax_FailsTooLarge | Mcq 60 + Fill 50 → code |
| 25 | 〃 | Validate_MixPercentOutOfRange_FailsDifficultyMixInvalid | (120, -10, -10) → code |
| 26 | 〃 | Validate_MixNotSummingTo100_FailsDifficultyMixInvalid | (30, 30, 30) → code |
| 27 | 〃 | Validate_TimeLimitZero_FailsTimeLimitInvalid | code |
| 28 | 〃 | Validate_TimeLimitAboveMax_FailsTimeLimitInvalid | 301 → code |
| 29 | 〃 | Validate_NoTimeLimitNoMix_Passes | no errors |
| 30 | 〃 | Validate_PassMarkOutOfRange_FailsPassMarkInvalid | `[Theory]` 0 and 101 |
| 31 | GetSubjectExamBlueprintsValidatorTests | Validate_EmptySubjectId_FailsSubjectIdRequired / Validate_SubjectId_Passes | code / none |
| 32 | SaveSubjectExamBlueprintValidatorTests | Validate_EmptySubjectId_FailsSubjectIdRequired · Validate_InvalidBlueprint_FailsWithNestedCode (all zero → `EXAM_BLUEPRINT_EMPTY`) · Validate_NullBlueprint_FailsEmpty · Validate_Valid_Passes | codes |
| 33 | SaveUnitExamBlueprintValidatorTests | Validate_EmptyUnitId_FailsUnitIdRequired · Validate_InvalidBlueprint_FailsWithNestedCode · Validate_Valid_Passes | codes |
| 34 | DeleteExamBlueprintValidatorTests | Validate_EmptyId_FailsExamBlueprintIdRequired · Validate_Id_Passes | codes |
| 35 | GetSubjectExamBlueprintsHandlerTests | Handle_Subject_ReturnsDefaultUnitsAndServableCounts | default id; units in the given order; unit A has its blueprint, unit B null; unit A `Servable` Mcq 2 and the other 4 types 0; subject `Servable` Mcq 3 (A 2 + B 1); `Servable.Count` 5 |
| 36 | 〃 | Handle_BlueprintOfUnknownUnit_IsIgnored | no unit shows it; default null |
| 37 | 〃 | Handle_SubjectNotFound_ThrowsSubjectNotFound | `NotFoundCoreException` + code |
| 38 | SaveSubjectExamBlueprintHandlerTests | Handle_NoDefault_CreatesAndSaves | `AddAsync` received an `ExamBlueprint` with `UnitId` null; result fields; `SaveChangesAsync` `Received(1)`; `CreatedBy` == current user |
| 39 | 〃 | Handle_ExistingDefault_UpdatesInPlace | the same instance is updated; `AddAsync` `DidNotReceive`; result `Id` == existing id; `Received(1)` save |
| 40 | 〃 | Handle_CountsAcrossUnits_UsesSubjectPool | Mcq 1 in unit A + 1 in unit B; requires Mcq 2 → succeeds |
| 41 | 〃 | Handle_Shortfall_ThrowsExamBlueprintShortfall | `BusinessRuleViolationCoreException` + code; no Add, no Save |
| 42 | 〃 | Handle_SubjectNotFound_ThrowsSubjectNotFound | code; no Save |
| 43 | 〃 | Handle_NoCurrentUser_ThrowsUserNotAuthenticated | `UnauthorizedCoreException` + code; no Save |
| 44 | SaveUnitExamBlueprintHandlerTests | Handle_NoBlueprint_CreatesForUnit | `UnitId`, `SubjectId`; Save `Received(1)` |
| 45 | 〃 | Handle_ExistingBlueprint_UpdatesInPlace | no Add; the same id |
| 46 | 〃 | Handle_OtherUnitsQuestions_DoNotCount | Mcq 5 in another unit, 1 in this one; requires 2 → shortfall code; no Save |
| 47 | 〃 | Handle_UnitNotFound_ThrowsUnitNotFound | code; no Save |
| 48 | 〃 | Handle_NoCurrentUser_ThrowsUserNotAuthenticated | code; no Save |
| 49 | DeleteExamBlueprintHandlerTests | Handle_UnitBlueprint_SoftDeletesAndSaves | `IsDeleted`, `UpdatedBy`; Save `Received(1)` |
| 50 | 〃 | Handle_SubjectDefault_ThrowsDefaultNotDeletable | `BusinessRuleViolationCoreException` + code; no Save |
| 51 | 〃 | Handle_NotFound_ThrowsExamBlueprintNotFound | code; no Save |
| 52 | 〃 | Handle_NoCurrentUser_ThrowsUserNotAuthenticated | code; no Save |

### API — Integration (Testcontainers PostgreSQL, through HTTP)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 53 | ExamBlueprintsEndpointTests | Get_Admin_ReturnsServableCountsPerUnitAndSubject | Seed unit A with a Published lesson holding 2 approved Mcq plus 1 pending Mcq, and unit B with a Draft lesson holding 1 approved Mcq. Result: 200; A `Mcq` 2; B `Mcq` 0; subject `Mcq` 2; `defaultBlueprint` null; units ordered by `order` |
| 54 | 〃 | Get_UnknownSubject_Returns404SubjectNotFound | status + `code` |
| 55 | 〃 | Get_Teacher_Returns403 | status |
| 56 | 〃 | Get_Anonymous_Returns401 | status |
| 57 | 〃 | PutSubject_Admin_CreatesDefaultAndAudits | 200 body (`unitId` null, `questionCount` 2); one DB row; audit `ExamBlueprint.SaveSubjectDefault` for the returned id is `Success` |
| 58 | 〃 | PutSubject_Twice_UpdatesSameBlueprint | the second PUT returns the same id with the new `passMark`; exactly one row |
| 59 | 〃 | PutSubject_Shortfall_Returns400AndSavesNothing | 400 `EXAM_BLUEPRINT_SHORTFALL`; zero rows |
| 60 | 〃 | PutSubject_AllZero_Returns422Empty | 422; `code` contains `EXAM_BLUEPRINT_EMPTY` |
| 61 | 〃 | PutSubject_Teacher_Returns403 | status; zero rows |
| 62 | 〃 | PutUnit_Admin_CreatesUnitBlueprint | 200; `unitId` and `subjectId` in body and DB; subsequent GET shows it under the unit and not as the default |
| 63 | 〃 | PutUnit_QuestionsInOtherUnit_Returns400Shortfall | 2 servable Mcq in unit A; PUT for unit B with Mcq 1 → 400 code |
| 64 | 〃 | PutUnit_UnknownUnit_Returns404UnitNotFound | status + code |
| 65 | 〃 | PutUnit_Anonymous_Returns401 | status |
| 66 | 〃 | Delete_UnitBlueprint_SoftDeletesAndAudits | 200; the row has `IsDeleted`; audit `ExamBlueprint.Delete` `Success`; GET shows the unit `blueprint` null |
| 67 | 〃 | Delete_SubjectDefault_Returns400DefaultNotDeletable | status + code; the row is not deleted |
| 68 | 〃 | Delete_Unknown_Returns404 | status + code |
| 69 | 〃 | Delete_Teacher_Returns403 | status; the row is not deleted |
| 70 | ExamBlueprintPersistenceTests | SaveChanges_SecondSubjectDefault_ThrowsModifiedConcurrently | Two contexts each add a default for the same subject. The second save throws `ConflictCoreException` with code `EXAM_BLUEPRINT_MODIFIED_CONCURRENTLY` |
| 71 | 〃 | SaveChanges_SecondBlueprintForUnit_ThrowsModifiedConcurrently | same, for the unit index |
| 72 | 〃 | SaveChanges_UnitBlueprintAfterDelete_Succeeds | soft-delete, then a new unit blueprint saves; 2 rows, 1 not deleted |
| 73 | AppDbContextTests (modify) | Migrate_FreshDatabase_LeavesNoPendingMigrations | the 17th migration ends with `_AddExamBlueprints` |

### Web (Vitest + Testing Library + MSW via the generated `exam-blueprints.msw.ts` and `subjects.msw.ts` handlers; `renderApp('/admin/blueprints…', { session: testSessions.admin })`)
| # | File | `it(...)` | Asserts |
|---|---|---|---|
| 74 | schemas/examBlueprintSchema.test.ts | accepts a valid blueprint | success |
| 75 | 〃 | rejects a non-numeric count | issue at `counts.Mcq`, key `countInvalid` |
| 76 | 〃 | rejects a count above the maximum | `countInvalid` |
| 77 | 〃 | rejects a blueprint with no questions | issue at `counts`, `empty` |
| 78 | 〃 | rejects a total above the maximum | `tooLarge` |
| 79 | 〃 | accepts an empty time limit and rejects 0 and 301 | `timeLimitInvalid` |
| 80 | 〃 | rejects a pass mark of 0 and 101 | `passMarkInvalid` |
| 81 | 〃 | ignores mix fields when the mix is off | success with junk percentages |
| 82 | 〃 | rejects an invalid percentage when the mix is on | `percentInvalid` |
| 83 | 〃 | rejects percentages not adding up to 100 | issue at `difficultyMix`, `mixSum` |
| 84 | schemas/blueprintsSearchSchema.test.ts | keeps a valid subject id / drops an invalid one | parsed values |
| 85 | api/blueprintValues.test.ts | finds the shortfall per type in type order | list |
| 86 | 〃 | returns no shortfall when enough questions exist and ignores zero requirements | empty |
| 87 | 〃 | builds form values from a blueprint and defaults without one | values |
| 88 | 〃 | converts form values to input, dropping zero counts, empty time and a disabled mix | input object |
| 89 | pages/ExamBlueprintsPage.test.tsx | shows the default and unit blueprints after loading | skeleton `role=status` "Loading exam blueprints", then headings "Subject default blueprint" and "Unit: Mechanics"; available cell values |
| 90 | 〃 | shows the empty state when there are no subjects | text + "Content" link |
| 91 | 〃 | shows the error state and retries | alert, then success after "Retry" |
| 92 | 〃 | switches subject from the select | choosing "Math" shows the Math overview and puts `subjectId` in the URL (`router.state.location.search`) |
| 93 | 〃 | renders right-to-left in Arabic | `dir="rtl"` on `html`; "نماذج الامتحانات" heading |
| 94 | 〃 | has no axe violations | `axe` clean |
| 95 | components/BlueprintEditor.test.tsx | highlights a short type and lists the live shortfall | typing Mcq 5 with 3 available shows "Current shortfall:" and "Multiple choice: required 5, available 3 (2 short)" |
| 96 | 〃 | refuses to save while a type is short | submit shows the `EXAM_BLUEPRINT_SHORTFALL` alert; no "Blueprint saved" toast |
| 97 | 〃 | saves the subject default | the PUT body (captured in the MSW resolver) equals the expected input; the "Blueprint saved" toast appears |
| 98 | 〃 | shows the server shortfall and refreshes the available counts | PUT → 400 `EXAM_BLUEPRINT_SHORTFALL` shows the alert; the refetched overview shows the new available count |
| 99 | 〃 | shows a server pass-mark error on the field | 422 `EXAM_BLUEPRINT_PASS_MARK_INVALID` shows the message under "Pass mark" |
| 100 | components/UnitBlueprintSection.test.tsx | shows that a unit uses the default and warns when the default is short for it | "Uses the subject default." plus "Current shortfall in this unit:" |
| 101 | 〃 | says no exam is available when there is no default | `noDefaultForUnit` text |
| 102 | 〃 | opens an editor prefilled from the default and cancels | the Mcq input shows the default's value; Cancel returns to the card |
| 103 | 〃 | removes a unit blueprint after confirming | "Use the subject default" → "Confirm" → DELETE handled → "Unit blueprint removed" toast and the card shows "Uses the subject default." after refetch |

## Definition of done
- [ ] `ExamBlueprint` (AuditEntity, IAuditedEntity) with `CreateForSubject`, `CreateForUnit`, `Update`, `Delete` exactly as in Domain behaviour; the canonical jsonb is used and every mutating method stamps `UpdationDate`.
- [ ] `ExamBlueprintShortfall.Find` is pure and is used by the entity guard. The web `findShortfall` mirrors it.
- [ ] `IQuestionRepository.CountServableByUnitAndTypeAsync` uses `WhereServable` (the only servable definition) in one grouped SQL query.
- [ ] Migration `AddExamBlueprints` creates only the new table, its two FKs (Restrict) and two partial unique indexes. `AppDbContextTests` lists it as the 17th.
- [ ] Unique violations on either index become 409 `EXAM_BLUEPRINT_MODIFIED_CONCURRENTLY`.
- [ ] Four endpoints under `api/exam-blueprints`, each with `[Authorize(Policy = DefaultCodes.BlueprintsManage)]`. Teacher gets 403, anonymous gets 401.
- [ ] The three commands are `IAuditableCommand` with the action names in Decision 20. `ExamBlueprintResult` exposes its id via `IAuditableResult`. `docs/audit-log.md` is updated.
- [ ] `ExamBlueprintsOptions` is bound with `ValidateOnStart`, with code defaults of 100 and 300, present in `appsettings.example.json` and `ApiFactory`.
- [ ] Every new error code is in both resx files and both web `errors` dictionaries.
- [ ] `api/openapi/v1.json` and the Orval client are regenerated with no drift. The Postman folder `ExamBlueprints` is added as specified.
- [ ] `/admin/blueprints` renders the real page: subject select in the URL, default card, unit cards, live shortfall, client-side save block, create from the default, and revert with inline confirm. It has loading, empty and error+retry states and an Arabic RTL render. No placeholder remains.
- [ ] Web uses tokens only, logical properties only, no hard-coded strings, and no file over 200 lines (components ≤ 120).
- [ ] Every test in the Test plan exists with the named method and the named assertion. No existing test is edited except #73.
- [ ] `docs/exam-blueprints.md` exists. PRD §10.2 and §15, and design-prompt §4, match the implementation.
- [ ] `dotnet build` shows zero new warnings. `dotnet test api/ -c Release` is green with `appsettings.json` moved aside. `dotnet format --verify-no-changes` is clean outside `core-libraries`.
- [ ] Web: `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`, `npx vitest run --coverage` and `npm run gen:api` with no diff all pass.
