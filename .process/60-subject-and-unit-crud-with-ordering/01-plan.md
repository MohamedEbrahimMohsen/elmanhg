# Plan — [E2.S1] Subject and unit CRUD with ordering (#60)

## Goal
An admin opens `#/admin/content` and can add, rename, move up/down and delete subjects. They can expand a subject and do the same for its units, so the curriculum tree follows the syllabus order. Every one of these mutations writes an audit row with a field diff. Teachers and students can read subjects through the API: the subject list and subject detail (which includes the ordered units). For a teacher, the list shows only assigned subjects, and subject detail returns 403 for any other subject (`SubjectScopeBehaviour`).

## Scope
**In:** `Subject` gains `Order` plus rename/move/delete behaviour. There is a new `CurriculumUnit` aggregate with `Order`. There are 8 audited commands (create, update, reorder, delete × subject and unit) with validators, and 2 queries: the subject list (with unit counts, filtered for teachers) and subject detail (with ordered units, subject-scoped). EF mapping and a migration with an order backfill. `ContentOptions` for name caps, configured in `appsettings.example.json` and `ApiFactory`. Two controllers, resx strings, Postman and the regenerated OpenAPI/Orval client. The admin UI at `/admin/content`: subject list, expandable unit list, add/rename/up-down/delete with states. `docs/audit-log.md` is updated.
**Out:** Drag-and-drop reorder. The story allows "drag or up/down". Up/down is keyboard-accessible (WCAG 2.5.7), matches the prototype `ctl()` ▲▼ buttons and needs no new dependency. The position API already supports drag. Also out: lessons, blueprints (`default_blueprint_id`, `blueprint_id`), the teacher/student UI, and subject name uniqueness.
**Deferred:**
- **Lesson and question counts** on the list/detail results. The `Lesson` (E2.S2) and `Question` (E3.S1) entities do not exist, so nothing can be counted. E2.S2 adds `LessonCount`; E3.S1 adds `QuestionCount`.
- **"Unit has lessons" delete guard.** Lessons do not exist yet. E2.S2 must add `CurriculumUnit.Delete(bool hasLessons, …)` in the same shape as `Subject.Delete`.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Entity name for Unit | `CurriculumUnit`, folder/namespace `Elmanhg.Domain.Units`, DbSet `Units` (so the table is `Units`). API routes, JSON and audit ResourceType stay "Unit". | `MediatR.Unit` collides with a class named `Unit` in every file that has `using MediatR;` (handlers, `AppDbContext`, `PipelineCompositionTests` already uses `Unit`), which gives CS0104. |
| D2 | Two aggregates or Subject owning Units | Two aggregates, `Subject : AuditEntity, IAuditedEntity` and `CurriculumUnit : AuditEntity, IAuditedEntity`. `CurriculumUnit.SubjectId` is an FK with Restrict. | The story sub-task says "Subject and Unit aggregates". This mirrors `TeacherSubject`. The repo's `AggregateRoot` has no audit fields, so it is not used. |
| D3 | Bilingual names? | Plain `string Name` (unchanged on Subject). | Existing `Subject.Name` is a string, the prototype has a single name, and SKILL §4.5 says plain `string` until a field is genuinely bilingual. |
| D4 | Reorder API shape | Move one item to a 1-based `Position` among its live siblings. The handler loads the siblings ordered, removes the item, inserts it at `Math.Min(Position, count) - 1`, and renumbers everyone 1..n. | This serves up/down (±1) and future drag (drop index). Unlike a full-list permutation it does not race with parallel creates, which matters because integration tests share one DB. A position past the end is clamped to the end; it is not an error. |
| D5 | New item order | `(highest live sibling Order ?? 0) + 1`, read with `FirstOrDefaultAsync(orderBy: OrderByDescending(Order), asNoTracking: true)`. | Same append-at-end behaviour as the prototype `addSubject`/`addUnit`. No new repo method. |
| D6 | Order after delete | No re-compaction; gaps are allowed. Reads sort `Order, then CreationDate`. The next reorder renumbers 1..n. | Smallest change; order semantics stay correct. |
| D7 | Delete a subject that has units | Blocked with a domain guard `Subject.Delete(bool hasUnits, Guid deletedBy)` → `BusinessRuleViolationCoreException(SUBJECT_HAS_UNITS)` (400). | Prototype `del()`: "cannot delete while child content exists". This mirrors `TeacherSubject.Create`, which throws the same exception type. |
| D8 | Teacher assignments on a deleted subject | Left as they are (not cascaded or blocked). | The subject is hidden by the soft-delete filter, and assign/read then 404. Unassign still works. Admin teacher UI is E9. |
| D9 | Soft or hard delete | Soft delete via `SoftDelete()` plus `UpdatedBy`/`UpdationDate`, mirroring `TeacherSubject.Unassign`. | Skill §4.1, and history is kept for the audit trail. |
| D10 | Read policies & teacher scope | `GET /api/subjects` and `GET /api/subjects/{id}` use `DefaultCodes.ContentBrowse`. Detail query implements `ISubjectScopedRequest`, so an unassigned teacher gets 403 `SUBJECT_OUT_OF_SCOPE`. The list handler filters to the teacher's assigned subjects when the role claim is `Teacher`. All mutations use `DefaultCodes.ContentManage` (Admin only). | PRD §16: "Browse published tree ✓ (assigned subjects)" and "Create/edit content Admin". This is the caller's instruction to apply subject scope where teachers read content. |
| D11 | Unit list / unit detail query | Units are returned inside subject detail (`SubjectDetailResult.Units`). There is no separate unit-detail endpoint. | No screen needs unit detail yet. E2.S2 adds unit detail with lessons. Subject detail covers both "list" (units) and "detail". |
| D12 | Unit ownership check | Unit commands carry `SubjectId` (route) plus `UnitId`. The handler throws 404 `UNIT_NOT_FOUND` when the unit is missing or `unit.SubjectId != request.SubjectId`. | This stops a unit being mutated through a URL for the wrong subject. The branch is testable because the handler uses `GetByIdAsync` and then compares. |
| D13 | Audit ResourceType/Action | See the "Audit" table in Files. Create commands take their ResourceId from `IAuditableResult`. | Follows `docs/audit-log.md` "Add a new audited command". |
| D14 | Name caps | `ContentOptions { SubjectNameMaxLength, UnitNameMaxLength }`, section `Content`, value `100` in `appsettings.example.json` and `ApiFactory`. There is no DB column max length (it stays `text` like the current `Name`). | SKILL §8.1 (caps in Options). CI has no `appsettings.json`: `ApiFactory` supplies the values, and build-time OpenAPI reads `appsettings.example.json`. |
| D15 | Name trimming | The domain stores `name.Trim()`. The validator's `ValidateRequired` (NotEmpty) already rejects whitespace-only names. | Avoids names that differ only by spaces. |
| D16 | `MoveTo` no-op | `MoveTo(order)` with an unchanged order returns without stamping. | Keeps reorder diffs to rows that actually moved. `Modified` entries with no changes are dropped anyway. |
| D17 | Request bodies | `Requests.cs` records per controller, and the controller maps them to commands. | The commands carry route ids plus `IAuditableCommand` members, which must not appear in the OpenAPI body schema. |
| D18 | Existing test using Subject as the "non-audited" example | `AuditChangeCaptureTests.SaveChangesAsync_NonAuditedEntity_RecordsNothing` switches to an `Otp` (plain `Entity`, not audited). The behaviour it covers is unchanged. | Subject is intentionally now `IAuditedEntity` (SKILL §8.11 allows updating a test when behaviour intentionally changes). |
| D19 | UI layout | One route `/admin/content`. Subjects are shown as a list of cards. Each card has a disclosure button that mounts `UnitPanel`, which fetches `GET /api/subjects/{id}` only while expanded. Rename and delete-confirm are inline (no dialog). | Matches the single-tree prototype `vAdminContent` and `docs/claude-design-prompt.md` §`#/admin/content`, so no docs divergence. It needs no new shadcn component, and there is no N+1 on load. |
| D20 | Mutation feedback | Create and rename use `mutateAsync` inside `Form`: server codes map to the `name` field, and a success toast is shown. Move and delete use `mutate`, with a toast on success and an error toast (`common:errors.<code>`) on failure. All of them invalidate the list and the affected subject detail. | react-feature §4 "invalidate + toast". |
| D21 | Client name validation | Zod `trim().min(1)` only. The max length comes from the server (`*_NAME_TOO_LONG` mapped to the field). | The server cap is configurable, and there is no magic number in the client. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Subjects/Subject.cs` | Add `IAuditedEntity`, `Order`, and a new `Create` signature. Add `Rename`, `MoveTo`, `Delete` (see Domain behaviour). |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Under `// CONTENT` add `SubjectHasUnits = "SUBJECT_HAS_UNITS"` and `ContentOrderInvalid = "CONTENT_ORDER_INVALID"`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Under `// SUBJECTS & TEACHERS` add `SubjectNameRequired`, `SubjectNameTooLong`, `SubjectPositionInvalid`. Add a new `// UNITS` group: `UnitNotFound`, `UnitIdRequired`, `UnitNameRequired`, `UnitNameTooLong`, `UnitPositionInvalid` (values in Error codes). |
| `api/Elmanhg.Application/DependencyInjection.cs` | `services.AddOptions<ContentOptions>().BindConfiguration(ContentOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Add `public DbSet<CurriculumUnit> Units { get; set; }`. `ConfigureSubjects`: keep `Name` required and add nothing else. Add a new `ConfigureUnits(modelBuilder)` called after `ConfigureSubjects`: `builder.Property(x => x.Name).IsRequired(); builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict); builder.HasIndex(x => new { x.SubjectId, x.Order });`. Global filter: add `modelBuilder.Entity<CurriculumUnit>().HasQueryFilter(x => !x.IsDeleted);`. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<ICurriculumUnitRepository, CurriculumUnitRepository>();` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add`. |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | Add the 10 keys in Error codes. |
| `api/Elmanhg.Api/appsettings.example.json` | Add `"Content": { "SubjectNameMaxLength": 100, "UnitNameMaxLength": 100 },` after `"AuditLogs"`. The same section also goes in the local gitignored `appsettings.json` if present (not committed). |
| `api/openapi/v1.json` | Regenerated by `dotnet build api/Elmanhg.Api`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | In-memory config: `["Content:SubjectNameMaxLength"] = "100"`, `["Content:UnitNameMaxLength"] = "100"`. |
| `api/Elmanhg.Tests/Domain/Subjects/SubjectTests.cs` | **modify**: existing test calls `Subject.Create("Physics", 1, createdBy)` and also asserts `Order == 1`. Add the new tests (Test plan). |
| `api/Elmanhg.Tests/Domain/Teachers/TeacherSubjectTests.cs` | **modify** line 12: `Subject.Create("Physics", 1, Guid.NewGuid())`. |
| `api/Elmanhg.Tests/Application/Features/Teachers/AssignTeacherSubject/AssignTeacherSubjectHandlerTests.cs` | **modify** line 23: same signature change. |
| `api/Elmanhg.Tests/Application/Features/Teachers/UnassignTeacherSubject/UnassignTeacherSubjectHandlerTests.cs` | **modify** line 30: same signature change. |
| `api/Elmanhg.Tests/Integration/Authorization/ScopeTestData.cs` | **modify** line 31: `Subject.Create(name, 1, Guid.NewGuid())`. |
| `api/Elmanhg.Tests/Integration/Persistence/AuditChangeCaptureTests.cs` | **modify** `SaveChangesAsync_NonAuditedEntity_RecordsNothing`: replace the Subject add with `context.Otps.Add(new OtpBuilder().ForPhone(AuthTestClient.NewPhoneNumber()).Build());` (add `using Elmanhg.Tests.Builders; using Elmanhg.Tests.Integration.Auth;`). Remove `using Elmanhg.Domain.Subjects;` only if it becomes unused. |
| `postman/elmanhg.postman_collection.json` | Add a folder "Subjects" (Get subjects, Get subject, Create subject, Update subject, Reorder subject, Delete subject) and a folder "Units" (Create unit, Update unit, Reorder unit, Delete unit), with bearer auth like the "Teachers" folder. |
| `docs/audit-log.md` | "Audited commands" table: add the 8 rows from the Audit table. "Audited entities" line: `TeacherSubject`, `Subject`, `CurriculumUnit`. |
| `web/src/routes/admin/content.tsx` | `component: ContentPage` imported from `@/features/content` (replaces `PlaceholderPage`). |
| `web/src/app/i18n.ts` | Import `contentLocales` from `@/features/content`. Add `content: contentLocales.ar/.en` to resources and `'content'` to `ns`. |
| `web/src/shared/i18n/en.json`, `ar.json` | `errors`: add `SUBJECT_NAME_REQUIRED`, `SUBJECT_NAME_TOO_LONG`, `SUBJECT_POSITION_INVALID`, `SUBJECT_HAS_UNITS`, `SUBJECT_NOT_FOUND`, `UNIT_NOT_FOUND`, `UNIT_NAME_REQUIRED`, `UNIT_NAME_TOO_LONG`, `UNIT_POSITION_INVALID` (same text as resx). |
| `web/src/shared/api/generated/**` | Regenerated with `npm run gen:api` after the API build. This adds `subjects/`, `units/`, model and zod files. Never hand-edited. |

## Files to create

Every command below ends with `: IRequest…, IAuditableCommand`. Every command handler starts with `if (currentUserService.UserId == null || currentUserService.UserId == default) { throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated); }` (step 0, not repeated below). Every mutation ends with exactly one `SaveChangesAsync`. `.ConfigureAwait(false)` goes on every await. Sort `S` = `q => q.OrderBy(x => x.Order).ThenBy(x => x.CreationDate)`. `ErrorCodes` = `Elmanhg.Application.Exceptions.ErrorCodes`. The domain uses `Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`.

### Domain / Infrastructure (Morabh source: `CRUD_FEATURE_CREATION_GUIDE.md` Step 2–3, shape only)
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Units/CurriculumUnit.cs` | entity | `namespace Elmanhg.Domain.Units; public class CurriculumUnit : AuditEntity, IAuditedEntity` · `Guid SubjectId {get; private set;}` · `string Name {get; private set;} = string.Empty` · `int Order {get; private set;}` · `private CurriculumUnit(Guid id, Guid? createdBy) : base(id, createdBy) { }` · `static CurriculumUnit Create(Subject subject, string name, int order, Guid createdBy)` · `void Rename(string name, Guid updatedBy)` · `void MoveTo(int order, Guid updatedBy)` · `void Delete(Guid deletedBy)` (bodies in Domain behaviour) |
| 2 | `api/Elmanhg.Domain/Units/ICurriculumUnitRepository.cs` | repo interface | `public interface ICurriculumUnitRepository : IRepository<CurriculumUnit>` { `Task<bool> AnyInSubjectAsync(Guid subjectId, CancellationToken cancellationToken);` `Task<Dictionary<Guid, int>> CountBySubjectAsync(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken);` } (the base cannot express existence-by-FK or group-by) |
| 3 | `api/Elmanhg.Infrastructure/Units/CurriculumUnitRepository.cs` | repo | `public class CurriculumUnitRepository(AppDbContext context) : Repository<CurriculumUnit>(context), ICurriculumUnitRepository`. `AnyInSubjectAsync` → `_dbSet.AnyAsync(x => x.SubjectId == subjectId, cancellationToken)`. `CountBySubjectAsync` → `_dbSet.Where(x => subjectIds.Contains(x.SubjectId)).GroupBy(x => x.SubjectId).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken)` |
| 4–5 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddSubjectOrderAndUnits.cs` + `.Designer.cs` | migration | Generated by `dotnet ef migrations add AddSubjectOrderAndUnits -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Expect: `AddColumn<int>("Order", "Subjects", nullable: false, defaultValue: 0)` and `CreateTable("Units")` with an FK to `Subjects` (Restrict) and index `(SubjectId, Order)`. After the AddColumn, **hand-add** a backfill: `migrationBuilder.Sql("UPDATE \"Subjects\" AS s SET \"Order\" = r.rn FROM (SELECT \"Id\", ROW_NUMBER() OVER (ORDER BY \"CreationDate\") AS rn FROM \"Subjects\") AS r WHERE s.\"Id\" = r.\"Id\";");`. There must be no Drop or Rename. |

### Application — options & shared
| # | Path | Type | Contract |
|---|------|------|----------|
| 6 | `api/Elmanhg.Application/Shared/Options/ContentOptions.cs` | options | `public sealed class ContentOptions { public const string SectionName = "Content"; [Range(1, int.MaxValue)] public int SubjectNameMaxLength { get; set; } [Range(1, int.MaxValue)] public int UnitNameMaxLength { get; set; } }` |
| 7 | `api/Elmanhg.Application/Subjects/Shared/SubjectResult.cs` | result (admin/list) | `public sealed record SubjectResult(Guid Id, string Name, int Order, int UnitCount);` |
| 8 | `api/Elmanhg.Application/Subjects/Shared/SubjectDetailResult.cs` | result | `public sealed record SubjectDetailResult(Guid Id, string Name, int Order, List<UnitResult> Units);` |
| 9 | `api/Elmanhg.Application/Subjects/Shared/SubjectResultGenerator.cs` | static | `static SubjectResult Generate(Subject subject, int unitCount)` · `static SubjectDetailResult GenerateDetail(Subject subject, List<CurriculumUnit> units)` → `units.Select(UnitResultGenerator.Generate).ToList()` |
| 10 | `api/Elmanhg.Application/Units/Shared/UnitResult.cs` | result | `public sealed record UnitResult(Guid Id, Guid SubjectId, string Name, int Order);` |
| 11 | `api/Elmanhg.Application/Units/Shared/UnitResultGenerator.cs` | static | `static UnitResult Generate(CurriculumUnit unit)` |

No field is `LocalizedText`, so there is no `.Localized()` anywhere (D3).

### Application — Subjects (namespace `Elmanhg.Application.Subjects.<UseCase>`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 12 | `Subjects/CreateSubject/CreateSubjectCommand.cs` | command | `sealed record CreateSubjectCommand(string Name) : IRequest<CreateSubjectResult>, IAuditableCommand` · `AuditAction => "Subject.Create"` · `AuditResourceType => "Subject"` · `AuditResourceId => null` |
| 13 | `Subjects/CreateSubject/CreateSubjectResult.cs` | result | `sealed record CreateSubjectResult(Guid Id) : IAuditableResult { Guid? IAuditableResult.AuditResourceId => Id; }` |
| 14 | `Subjects/CreateSubject/CreateSubjectValidator.cs` | validator | ctor `(IOptions<ContentOptions> contentOptions)`: `RuleFor(x => x.Name).ValidateRequired(ErrorCodes.SubjectNameRequired).ValidateMaxLength(options.SubjectNameMaxLength, ErrorCodes.SubjectNameTooLong);` |
| 15 | `Subjects/CreateSubject/CreateSubjectHandler.cs` | handler | `(ISubjectRepository subjectRepository, ICurrentUserService currentUserService)`. 1. `last = subjectRepository.FirstOrDefaultAsync(_ => true, ct, orderBy: q => q.OrderByDescending(x => x.Order), asNoTracking: true)` 2. `subject = Subject.Create(request.Name, (last?.Order ?? 0) + 1, userId)` 3. `AddAsync` 4. `SaveChangesAsync` 5. `return new CreateSubjectResult(subject.Id)` |
| 16 | `Subjects/UpdateSubject/UpdateSubjectCommand.cs` | command | `sealed record UpdateSubjectCommand(Guid SubjectId, string Name) : IRequest, IAuditableCommand` · `"Subject.Update"` · `"Subject"` · `AuditResourceId => SubjectId` |
| 17 | `Subjects/UpdateSubject/UpdateSubjectValidator.cs` | validator | `(IOptions<ContentOptions>)`: `SubjectId.ValidateRequired(ErrorCodes.SubjectIdRequired)`. Name rules as in #14. |
| 18 | `Subjects/UpdateSubject/UpdateSubjectHandler.cs` | handler | `(ISubjectRepository, ICurrentUserService)`. 1. `subject = GetByIdAsync(request.SubjectId, ct)` (tracked). If null, throw `NotFoundCoreException(ErrorCodes.SubjectNotFound)` 2. `subject.Rename(request.Name, userId)` 3. `SaveChangesAsync` |
| 19 | `Subjects/ReorderSubject/ReorderSubjectCommand.cs` | command | `sealed record ReorderSubjectCommand(Guid SubjectId, int Position) : IRequest, IAuditableCommand` · `"Subject.Reorder"` · `"Subject"` · `SubjectId` |
| 20 | `Subjects/ReorderSubject/ReorderSubjectValidator.cs` | validator | `SubjectId.ValidateRequired(ErrorCodes.SubjectIdRequired)` · `Position.ValidateMin(1, ErrorCodes.SubjectPositionInvalid)` |
| 21 | `Subjects/ReorderSubject/ReorderSubjectHandler.cs` | handler | `(ISubjectRepository, ICurrentUserService)`. 1. `siblings = subjectRepository.FindAsync(_ => true, ct, orderBy: S)` (tracked) 2. `subject = siblings.FirstOrDefault(x => x.Id == request.SubjectId)`. If null, throw `NotFoundCoreException(ErrorCodes.SubjectNotFound)` 3. `siblings.Remove(subject); siblings.Insert(Math.Min(request.Position, siblings.Count + 1) - 1, subject);` 4. `for (var index = 0; index < siblings.Count; index++) { siblings[index].MoveTo(index + 1, userId); }` 5. `SaveChangesAsync` |
| 22 | `Subjects/DeleteSubject/DeleteSubjectCommand.cs` | command | `sealed record DeleteSubjectCommand(Guid SubjectId) : IRequest, IAuditableCommand` · `"Subject.Delete"` · `"Subject"` · `SubjectId` |
| 23 | `Subjects/DeleteSubject/DeleteSubjectValidator.cs` | validator | `SubjectId.ValidateRequired(ErrorCodes.SubjectIdRequired)` |
| 24 | `Subjects/DeleteSubject/DeleteSubjectHandler.cs` | handler | `(ISubjectRepository, ICurriculumUnitRepository unitRepository, ICurrentUserService)`. 1. `subject = GetByIdAsync(id, ct)`. If null, throw `NotFoundCoreException(SubjectNotFound)` 2. `hasUnits = unitRepository.AnyInSubjectAsync(subject.Id, ct)` 3. `subject.Delete(hasUnits, userId)` (the domain throws `SUBJECT_HAS_UNITS`) 4. `SaveChangesAsync` |
| 25 | `Subjects/GetSubject/GetSubjectQuery.cs` | query | `sealed record GetSubjectQuery(Guid SubjectId) : IRequest<SubjectDetailResult>, ISubjectScopedRequest` |
| 26 | `Subjects/GetSubject/GetSubjectValidator.cs` | validator | `SubjectId.ValidateRequired(ErrorCodes.SubjectIdRequired)` |
| 27 | `Subjects/GetSubject/GetSubjectHandler.cs` | handler | `(ISubjectRepository, ICurriculumUnitRepository)`, no user guard (policy plus scope behaviour). 1. `subject = GetByIdAsync(id, ct, asNoTracking: true)`. If null, throw `NotFoundCoreException(SubjectNotFound)` 2. `units = unitRepository.FindAsync(x => x.SubjectId == subject.Id, ct, orderBy: S, asNoTracking: true)` 3. `return SubjectResultGenerator.GenerateDetail(subject, units)` |
| 28 | `Subjects/GetSubjects/GetSubjectsQuery.cs` | query | `sealed record GetSubjectsQuery : IRequest<List<SubjectResult>>;` (no validator: no input) |
| 29 | `Subjects/GetSubjects/GetSubjectsHandler.cs` | handler | `(ISubjectRepository, ITeacherSubjectRepository teacherSubjectRepository, ICurriculumUnitRepository unitRepository, ICurrentUserService)`. 0. user guard 1. If `currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Teacher)`: `assignedIds = (await teacherSubjectRepository.FindAsync(x => x.TeacherId == userId, ct, asNoTracking: true)).Select(x => x.SubjectId).ToList(); subjects = await subjectRepository.FindAsync(x => assignedIds.Contains(x.Id), ct, orderBy: S, asNoTracking: true);`. Else: `subjects = await subjectRepository.GetAllAsync(ct, orderBy: S, asNoTracking: true) ?? [];` 2. `counts = unitRepository.CountBySubjectAsync(subjects.Select(x => x.Id).ToList(), ct)` 3. `return subjects.Select(x => SubjectResultGenerator.Generate(x, counts.GetValueOrDefault(x.Id))).ToList();` |

### Application — Units (namespace `Elmanhg.Application.Units.<UseCase>`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 30 | `Units/CreateUnit/CreateUnitCommand.cs` | command | `sealed record CreateUnitCommand(Guid SubjectId, string Name) : IRequest<CreateUnitResult>, IAuditableCommand` · `"Unit.Create"` · `"Unit"` · `null` |
| 31 | `Units/CreateUnit/CreateUnitResult.cs` | result | `sealed record CreateUnitResult(Guid Id) : IAuditableResult { Guid? IAuditableResult.AuditResourceId => Id; }` |
| 32 | `Units/CreateUnit/CreateUnitValidator.cs` | validator | `(IOptions<ContentOptions>)`: `SubjectId.ValidateRequired(SubjectIdRequired)` · `Name.ValidateRequired(UnitNameRequired).ValidateMaxLength(options.UnitNameMaxLength, UnitNameTooLong)` |
| 33 | `Units/CreateUnit/CreateUnitHandler.cs` | handler | `(ISubjectRepository, ICurriculumUnitRepository, ICurrentUserService)`. 1. `subject = subjectRepository.GetByIdAsync(request.SubjectId, ct, asNoTracking: true)`. If null, throw `NotFoundCoreException(SubjectNotFound)` 2. `last = unitRepository.FirstOrDefaultAsync(x => x.SubjectId == subject.Id, ct, orderBy: q => q.OrderByDescending(x => x.Order), asNoTracking: true)` 3. `unit = CurriculumUnit.Create(subject, request.Name, (last?.Order ?? 0) + 1, userId)` 4. `unitRepository.AddAsync` 5. `unitRepository.SaveChangesAsync` 6. `return new CreateUnitResult(unit.Id)` |
| 34 | `Units/UpdateUnit/UpdateUnitCommand.cs` | command | `(Guid SubjectId, Guid UnitId, string Name) : IRequest, IAuditableCommand` · `"Unit.Update"` · `"Unit"` · `UnitId` |
| 35 | `Units/UpdateUnit/UpdateUnitValidator.cs` | validator | `(IOptions<ContentOptions>)`: SubjectId → `SubjectIdRequired`. UnitId → `UnitIdRequired`. Name rules as in #32. |
| 36 | `Units/UpdateUnit/UpdateUnitHandler.cs` | handler | `(ICurriculumUnitRepository, ICurrentUserService)`. 1. `unit = GetByIdAsync(request.UnitId, ct)` (tracked). If `unit is null \|\| unit.SubjectId != request.SubjectId`, throw `NotFoundCoreException(ErrorCodes.UnitNotFound)` 2. `unit.Rename(request.Name, userId)` 3. `SaveChangesAsync` |
| 37 | `Units/ReorderUnit/ReorderUnitCommand.cs` | command | `(Guid SubjectId, Guid UnitId, int Position) : IRequest, IAuditableCommand` · `"Unit.Reorder"` · `"Unit"` · `UnitId` |
| 38 | `Units/ReorderUnit/ReorderUnitValidator.cs` | validator | SubjectId → `SubjectIdRequired` · UnitId → `UnitIdRequired` · `Position.ValidateMin(1, ErrorCodes.UnitPositionInvalid)` |
| 39 | `Units/ReorderUnit/ReorderUnitHandler.cs` | handler | `(ICurriculumUnitRepository, ICurrentUserService)`. 1. `siblings = FindAsync(x => x.SubjectId == request.SubjectId, ct, orderBy: S)` (tracked) 2. `unit = siblings.FirstOrDefault(x => x.Id == request.UnitId)`. If null, throw `NotFoundCoreException(UnitNotFound)` 3–5. Same as #21 steps 3–5. |
| 40 | `Units/DeleteUnit/DeleteUnitCommand.cs` | command | `(Guid SubjectId, Guid UnitId) : IRequest, IAuditableCommand` · `"Unit.Delete"` · `"Unit"` · `UnitId` |
| 41 | `Units/DeleteUnit/DeleteUnitValidator.cs` | validator | SubjectId → `SubjectIdRequired` · UnitId → `UnitIdRequired` |
| 42 | `Units/DeleteUnit/DeleteUnitHandler.cs` | handler | `(ICurriculumUnitRepository, ICurrentUserService)`. 1. Lookup plus 404 as in #36 2. `unit.Delete(userId)` 3. `SaveChangesAsync` |

All paths in #12–#42 are under `api/Elmanhg.Application/`.

**Audit** (goes into `docs/audit-log.md`):
| Command | AuditAction | ResourceType | Id source |
|---|---|---|---|
| CreateSubject | `Subject.Create` | Subject | result |
| UpdateSubject | `Subject.Update` | Subject | command |
| ReorderSubject | `Subject.Reorder` | Subject | command (diff lists every sibling whose Order changed) |
| DeleteSubject | `Subject.Delete` | Subject | command |
| CreateUnit | `Unit.Create` | Unit | result |
| UpdateUnit | `Unit.Update` | Unit | command |
| ReorderUnit | `Unit.Reorder` | Unit | command |
| DeleteUnit | `Unit.Delete` | Unit | command |

### API
| # | Path | Type | Contract |
|---|------|------|----------|
| 43 | `api/Elmanhg.Api/Controllers/Subjects/Requests.cs` | requests | `public sealed record SubjectNameRequest(string Name);` `public sealed record SubjectPositionRequest(int Position);` |
| 44 | `api/Elmanhg.Api/Controllers/Subjects/SubjectsController.cs` | controller | `[ApiController][Route("api/subjects")][Authorize] public class SubjectsController(IMediator mediator) : ControllerBase`. The 6 actions are listed in API surface. Each has `Name =` for Orval and `[ProducesResponseType]`, and mirrors `TeachersController`. |
| 45 | `api/Elmanhg.Api/Controllers/Units/Requests.cs` | requests | `public sealed record UnitNameRequest(string Name);` `public sealed record UnitPositionRequest(int Position);` |
| 46 | `api/Elmanhg.Api/Controllers/Units/UnitsController.cs` | controller | `[ApiController][Route("api/subjects/{subjectId:guid}/units")][Authorize] public class UnitsController(IMediator mediator) : ControllerBase`, with the 4 actions in API surface. |

### Tests (api)
| # | Path |
|---|------|
| 47 | `api/Elmanhg.Tests/Domain/Units/CurriculumUnitTests.cs` |
| 48–53 | `api/Elmanhg.Tests/Application/Features/Subjects/{CreateSubject,UpdateSubject,ReorderSubject,DeleteSubject,GetSubject,GetSubjects}/<UseCase>HandlerTests.cs` |
| 54–58 | `api/Elmanhg.Tests/Application/Features/Subjects/{CreateSubject,UpdateSubject,ReorderSubject,DeleteSubject,GetSubject}/<UseCase>ValidatorTests.cs` |
| 59–62 | `api/Elmanhg.Tests/Application/Features/Units/{CreateUnit,UpdateUnit,ReorderUnit,DeleteUnit}/<UseCase>HandlerTests.cs` |
| 63–66 | `api/Elmanhg.Tests/Application/Features/Units/{CreateUnit,UpdateUnit,ReorderUnit,DeleteUnit}/<UseCase>ValidatorTests.cs` |
| 67 | `api/Elmanhg.Tests/Integration/Content/ContentTestData.cs`: `public static class ContentTestData` with `static Task<Guid> SeedSubjectAsync(ApiFactory factory, string name, int order, CancellationToken)`, `static Task<Guid> SeedUnitAsync(ApiFactory factory, Guid subjectId, string name, int order, CancellationToken)`, `static Task<int> ReadOrderAsync(ApiFactory factory, Guid subjectOrUnitId, CancellationToken)` (checks `Subjects`, then `Units`), `static Task<AuditLog> ReadAuditAsync(ApiFactory factory, string action, Guid resourceId, CancellationToken)`. Each uses a fresh scope `AppDbContext`, following the `ScopeTestData` style. |
| 68 | `api/Elmanhg.Tests/Integration/Content/SubjectsEndpointTests.cs` |
| 69 | `api/Elmanhg.Tests/Integration/Content/UnitsEndpointTests.cs` |

Handler tests use NSubstitute for repositories and `ICurrentUserService` (`UserId.Returns(Guid.NewGuid())`, `GetClaim(ClaimTypes.Role).Returns(...)`). Validator tests use `Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100 })`, following `GetAuditLogsValidatorTests`.

### Web (`web/src/features/content/`, Glass tokens only, existing classes as in `features/audit`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 70 | `index.ts` | barrel | `export { ContentPage } from './pages/ContentPage'; export const contentLocales = { ar, en };` |
| 71–72 | `i18n/en.json`, `i18n/ar.json` | locales | Keys listed below the table. |
| 73 | `pages/ContentPage.tsx` | page | `h1` `t('page.title')`, then `NameForm` (label `subjects.nameLabel`, submit `subjects.add`, serverErrorFields `{SUBJECT_NAME_REQUIRED:'name', SUBJECT_NAME_TOO_LONG:'name'}`, reset on success) calling `create(name)`. `useGetSubjects()`: pending shows `ContentListSkeleton label=t('page.loading')`; error shows `ContentErrorState title=t('error.title') onRetry=refetch`; `[]` shows `ContentEmptyState message=t('subjects.empty')`; otherwise `<ol aria-label={t('subjects.listLabel')}>` of `SubjectCard key={id}`. |
| 74 | `components/SubjectCard.tsx` | component | Props `{ subject: SubjectResult; isFirst: boolean; isLast: boolean; position: number }`. `<li>` card: `h2` name, `t('subjects.unitCount', { count })`, `ItemActions`, and a toggle `<button aria-expanded aria-controls>` (`subjects.showUnits`/`hideUnits`, local `useState`). When expanded it renders `<UnitPanel subjectId id=…>`. |
| 75 | `components/UnitPanel.tsx` | component | Props `{ subjectId: string; id: string }`. `useGetSubject(subjectId)`: pending → skeleton `units.loading`; error → `ContentErrorState title=t('units.errorTitle')`; `units: []` → `ContentEmptyState t('units.empty')`; otherwise `<ol aria-label={t('units.listLabel', { name })}>` of `<li>` with name plus `ItemActions`. It also has a `NameForm` (label `units.nameLabel`, submit `units.add`, fields `{UNIT_NAME_REQUIRED:'name', UNIT_NAME_TOO_LONG:'name'}`). |
| 76 | `components/ItemActions.tsx` | component | Props `{ name: string; isFirst: boolean; isLast: boolean; onMove: (direction: MoveDirection) => void; onRename: (name: string) => Promise<void>; onDelete: () => void; renameLabel: string; serverErrorFields: ServerErrorFields<NameValues> }`. Local mode is `'idle' \| 'renaming' \| 'confirmingDelete'`. Idle shows `sm` buttons: up (`aria-label t('actions.moveUp',{name})`, `disabled={isFirst}`, lucide `ChevronUp`), down (same pattern, `isLast`), `actions.rename`, `actions.delete` (variant danger). Renaming shows `NameForm` with `defaultName=name` and `onCancel`, and returns to idle on success. ConfirmingDelete shows `<p>{t('actions.confirmDelete',{name})}</p>` plus a danger `actions.confirm` button (calls `onDelete`, then idle) and `actions.cancel`. |
| 77 | `components/NameForm.tsx` | component | Props `{ label: string; submitLabel: string; defaultName?: string; serverErrorFields: ServerErrorFields<NameValues>; onSubmit: (name: string) => Promise<void>; onCancel?: () => void }`. RHF with `zodResolver(nameSchema)`, `Form` + `FormRootError` + `TextField name="name"` + `SubmitButton` + optional cancel `Button variant="secondary"`. After a successful submit it calls `form.reset({ name: '' })` when `defaultName` is undefined. |
| 78 | `components/ContentListSkeleton.tsx` | component | `{ label: string }` → `role="status" aria-busy aria-label={label}` with 3 `h-11 rounded-md bg-soft` rows (copy `AuditLogTableSkeleton`). |
| 79 | `components/ContentEmptyState.tsx` | component | `{ message: string }` → bordered surface card with lucide `FolderTree` `aria-hidden` and the message. |
| 80 | `components/ContentErrorState.tsx` | component | `{ title: string; error: unknown; onRetry: () => void }` → the `role="alert"` block copied from `AuditLogPage` (code message via `common:errors.<code>` and Retry `common:actions.retry`). |
| 81 | `hooks/useContentFeedback.ts` | hook | `useContentFeedback()` → `{ success: (key: string) => void; failure: (error: unknown) => void }`. `toast(t(key))` / `toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']))` with the code taken from `ApiError`. |
| 82 | `hooks/useSubjectMutations.ts` | hook | Returns `{ create(name): Promise<void>, rename(subjectId, name): Promise<void>, move(subjectId, position): void, remove(subjectId): void }` over the generated `useCreateSubject/useUpdateSubject/useReorderSubject/useDeleteSubject`. `onSuccess`: `queryClient.invalidateQueries({ queryKey: getGetSubjectsQueryKey() })` plus success toast (`subjects.created/renamed/moved/deleted`). Move and delete `onError` → `failure(error)`. Create and rename use `mutateAsync` so errors reach `Form`. |
| 83 | `hooks/useUnitMutations.ts` | hook | `useUnitMutations(subjectId)` → same shape over `useCreateUnit/useUpdateUnit/useReorderUnit/useDeleteUnit`. Invalidates `getGetSubjectQueryKey(subjectId)` and `getGetSubjectsQueryKey()`. Toast keys `units.*`. |
| 84 | `api/position.ts` | util | `export type MoveDirection = 'up' \| 'down'; export function targetPosition(index: number, direction: MoveDirection): number` → `direction === 'up' ? index : index + 2` (1-based target from a 0-based index). |
| 85 | `api/position.test.ts` | test | See Test plan. |
| 86 | `schemas/nameSchema.ts` | schema | `export const nameSchema = z.object({ name: z.string().trim().min(1, { error: 'validation.required' }) }); export type NameValues = z.infer<typeof nameSchema>;` |
| 87 | `schemas/nameSchema.test.ts` | test | See Test plan. |
| 88 | `pages/ContentPage.test.tsx` | test | See Test plan. |
| 89 | `components/UnitPanel.test.tsx` | test | See Test plan (renders through `renderApp('/admin/content', { session: testSessions.admin })` and expands a subject). |

Locale keys (en | ar):
- `page.title` Content tree | شجرة المحتوى
- `page.loading` Loading subjects | جارٍ تحميل المواد
- `error.title` Could not load the content tree | تعذّر تحميل شجرة المحتوى
- `subjects.listLabel` Subjects | المواد
- `subjects.nameLabel` Subject name | اسم المادة
- `subjects.add` Add subject | إضافة مادة
- `subjects.empty` No subjects yet. Add the first subject. | لا توجد مواد بعد. أضف أول مادة.
- `subjects.unitCount` `{count, plural, one {# unit} other {# units}}` | `{count, plural, zero {لا وحدات} one {وحدة واحدة} two {وحدتان} few {# وحدات} many {# وحدة} other {# وحدة}}`
- `subjects.showUnits` Show units | عرض الوحدات
- `subjects.hideUnits` Hide units | إخفاء الوحدات
- `subjects.created` Subject added. | تمت إضافة المادة.
- `subjects.renamed` Subject renamed. | تمت إعادة تسمية المادة.
- `subjects.moved` Subject moved. | تم نقل المادة.
- `subjects.deleted` Subject deleted. | تم حذف المادة.
- `units.listLabel` Units of {name} | وحدات {name}
- `units.loading` Loading units | جارٍ تحميل الوحدات
- `units.errorTitle` Could not load units | تعذّر تحميل الوحدات
- `units.nameLabel` Unit name | اسم الوحدة
- `units.add` Add unit | إضافة وحدة
- `units.empty` No units in this subject yet. | لا توجد وحدات في هذه المادة بعد.
- `units.created/renamed/moved/deleted` Unit added. / Unit renamed. / Unit moved. / Unit deleted. | تمت إضافة الوحدة. / تمت إعادة تسمية الوحدة. / تم نقل الوحدة. / تم حذف الوحدة.
- `actions.moveUp` Move {name} up | نقل {name} لأعلى
- `actions.moveDown` Move {name} down | نقل {name} لأسفل
- `actions.rename` Rename | إعادة تسمية
- `actions.newName` New name | الاسم الجديد
- `actions.save` Save | حفظ
- `actions.cancel` Cancel | إلغاء
- `actions.delete` Delete | حذف
- `actions.confirmDelete` Delete {name}? | حذف {name}؟
- `actions.confirm` Yes, delete | نعم، احذف

`ItemActions` passes `renameLabel` = `t('actions.newName')` and submit label `actions.save`.

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ErrorCodes.SubjectNameRequired` | `SUBJECT_NAME_REQUIRED` | Create/UpdateSubjectValidator | validation | 422 |
| `ErrorCodes.SubjectNameTooLong` | `SUBJECT_NAME_TOO_LONG` | Create/UpdateSubjectValidator | validation | 422 |
| `ErrorCodes.SubjectPositionInvalid` | `SUBJECT_POSITION_INVALID` | ReorderSubjectValidator | validation | 422 |
| `ErrorCodes.SubjectIdRequired` (existing) | `SUBJECT_ID_REQUIRED` | all subject/unit validators | validation | 422 |
| `ErrorCodes.SubjectNotFound` (existing) | `SUBJECT_NOT_FOUND` | Update/Reorder/Delete/GetSubject, CreateUnit handlers | `NotFoundCoreException` | 404 |
| `ErrorCodes.UnitIdRequired` | `UNIT_ID_REQUIRED` | Update/Reorder/DeleteUnitValidator | validation | 422 |
| `ErrorCodes.UnitNameRequired` | `UNIT_NAME_REQUIRED` | Create/UpdateUnitValidator | validation | 422 |
| `ErrorCodes.UnitNameTooLong` | `UNIT_NAME_TOO_LONG` | Create/UpdateUnitValidator | validation | 422 |
| `ErrorCodes.UnitPositionInvalid` | `UNIT_POSITION_INVALID` | ReorderUnitValidator | validation | 422 |
| `ErrorCodes.UnitNotFound` | `UNIT_NOT_FOUND` | Update/Reorder/DeleteUnit handlers | `NotFoundCoreException` | 404 |
| Domain `ErrorCodes.SubjectHasUnits` | `SUBJECT_HAS_UNITS` | `Subject.Delete` | `BusinessRuleViolationCoreException` | 400 |
| Domain `ErrorCodes.ContentOrderInvalid` | `CONTENT_ORDER_INVALID` | `Subject/CurriculumUnit.Create`, `MoveTo` | `BusinessRuleViolationCoreException` | 400 |

Resx (en | ar, no tashkeel, same spelling style as the existing ar.resx):
- SUBJECT_NAME_REQUIRED: Enter the subject name. | اكتب اسم المادة.
- SUBJECT_NAME_TOO_LONG: Subject name is too long. | اسم المادة طويل جدا.
- SUBJECT_POSITION_INVALID: Position must be 1 or more. | الترتيب يجب ان يكون 1 او اكثر.
- SUBJECT_HAS_UNITS: This subject has units. Delete its units first. | لا يمكن حذف المادة لانها تحتوي على وحدات. احذف الوحدات اولا.
- UNIT_NOT_FOUND: Unit not found. | الوحدة غير موجودة.
- UNIT_ID_REQUIRED: Choose a unit. | اختر الوحدة.
- UNIT_NAME_REQUIRED: Enter the unit name. | اكتب اسم الوحدة.
- UNIT_NAME_TOO_LONG: Unit name is too long. | اسم الوحدة طويل جدا.
- UNIT_POSITION_INVALID: Position must be 1 or more. | الترتيب يجب ان يكون 1 او اكثر.
- CONTENT_ORDER_INVALID: Order must be 1 or more. | الترتيب يجب ان يكون 1 او اكثر.

Web `shared/i18n/{en,ar}.json` `errors.*`: the same text for every code in the list above except `UNIT_ID_REQUIRED` and `CONTENT_ORDER_INVALID` (the UI cannot trigger those).

## Domain behaviour
```csharp
// Subject : AuditEntity, IAuditedEntity   (Order: public int { get; private set; })
public static Subject Create(string name, int order, Guid createdBy)
{
    if (order < 1) { throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid); }
    return new Subject(Guid.NewGuid(), createdBy) { Name = name.Trim(), Order = order };
}
public void Rename(string name, Guid updatedBy) { Name = name.Trim(); UpdatedBy = updatedBy; UpdationDate = DateTimeOffset.UtcNow; }
public void MoveTo(int order, Guid updatedBy)
{
    if (order < 1) { throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid); }
    if (Order == order) { return; }
    Order = order; UpdatedBy = updatedBy; UpdationDate = DateTimeOffset.UtcNow;
}
public void Delete(bool hasUnits, Guid deletedBy)
{
    if (hasUnits) { throw new BusinessRuleViolationCoreException(ErrorCodes.SubjectHasUnits); }
    SoftDelete(); UpdatedBy = deletedBy; UpdationDate = DateTimeOffset.UtcNow;
}

// CurriculumUnit : AuditEntity, IAuditedEntity
public static CurriculumUnit Create(Subject subject, string name, int order, Guid createdBy)
{
    if (order < 1) { throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid); }
    return new CurriculumUnit(Guid.NewGuid(), createdBy) { SubjectId = subject.Id, Name = name.Trim(), Order = order };
}
// Rename, MoveTo: identical to Subject.  Delete(Guid deletedBy): SoftDelete(); UpdatedBy = deletedBy; UpdationDate = DateTimeOffset.UtcNow;
```
Formatting: braces on their own lines, following the repo style (above is compressed). `ErrorCodes` = `Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`. Guard, then mutate, then stamp.

## API surface
| Method | Route | Policy | Body | Response |
|---|---|---|---|---|
| GET | `/api/subjects` (Name `GetSubjects`) | `DefaultCodes.ContentBrowse` | — | `200 List<SubjectResult>` |
| GET | `/api/subjects/{subjectId:guid}` (`GetSubject`) | `ContentBrowse` + scope | — | `200 SubjectDetailResult` |
| POST | `/api/subjects` (`CreateSubject`) | `ContentManage` | `SubjectNameRequest` | `200 CreateSubjectResult` |
| PUT | `/api/subjects/{subjectId:guid}` (`UpdateSubject`) | `ContentManage` | `SubjectNameRequest` | `200` |
| PUT | `/api/subjects/{subjectId:guid}/position` (`ReorderSubject`) | `ContentManage` | `SubjectPositionRequest` | `200` |
| DELETE | `/api/subjects/{subjectId:guid}` (`DeleteSubject`) | `ContentManage` | — | `200` |
| POST | `/api/subjects/{subjectId:guid}/units` (`CreateUnit`) | `ContentManage` | `UnitNameRequest` | `200 CreateUnitResult` |
| PUT | `/api/subjects/{subjectId:guid}/units/{unitId:guid}` (`UpdateUnit`) | `ContentManage` | `UnitNameRequest` | `200` |
| PUT | `/api/subjects/{subjectId:guid}/units/{unitId:guid}/position` (`ReorderUnit`) | `ContentManage` | `UnitPositionRequest` | `200` |
| DELETE | `/api/subjects/{subjectId:guid}/units/{unitId:guid}` (`DeleteUnit`) | `ContentManage` | — | `200` |

Every action takes `CancellationToken cancellationToken` and uses `[FromRoute]`/`[FromBody]`. It maps request to command, calls `Send`, and returns `Ok(result)` or `Ok()`.

## Test plan
### Domain (no doubles)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | SubjectTests | `Create_Always_SetsNameAndCreator` (**modify**: new signature) | Name "Physics", Order 1, CreatedBy, Id not empty, not deleted |
| 2 | SubjectTests | `Create_NameWithSurroundingSpaces_StoresTrimmedName` | `"  Physics "` → "Physics" |
| 3 | SubjectTests | `Create_OrderZero_ThrowsContentOrderInvalid` | type + code |
| 4 | SubjectTests | `Rename_NewName_SetsTrimmedNameAndUpdater` | Name, UpdatedBy |
| 5 | SubjectTests | `MoveTo_NewOrder_SetsOrderAndUpdater` | Order, UpdatedBy |
| 6 | SubjectTests | `MoveTo_SameOrder_LeavesUpdaterUnchanged` | UpdatedBy still equals creator |
| 7 | SubjectTests | `MoveTo_OrderZero_ThrowsContentOrderInvalid` | type + code, Order unchanged |
| 8 | SubjectTests | `Delete_NoUnits_SoftDeletesAndSetsUpdater` | IsDeleted, UpdatedBy |
| 9 | SubjectTests | `Delete_HasUnits_ThrowsSubjectHasUnits` | type + code, IsDeleted false |
| 10 | CurriculumUnitTests | `Create_Always_SetsSubjectNameOrderAndCreator` | SubjectId, trimmed Name, Order, CreatedBy |
| 11 | CurriculumUnitTests | `Create_OrderZero_ThrowsContentOrderInvalid` | type + code |
| 12 | CurriculumUnitTests | `Rename_NewName_SetsTrimmedNameAndUpdater` | |
| 13 | CurriculumUnitTests | `MoveTo_NewOrder_SetsOrderAndUpdater` | |
| 14 | CurriculumUnitTests | `MoveTo_SameOrder_LeavesUpdaterUnchanged` | |
| 15 | CurriculumUnitTests | `MoveTo_OrderZero_ThrowsContentOrderInvalid` | |
| 16 | CurriculumUnitTests | `Delete_Always_SoftDeletesAndSetsUpdater` | |

### Handlers (success asserts result/state plus `SaveChangesAsync` `Received(1)`; throw paths assert type, code and `DidNotReceive()`)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 17 | CreateSubjectHandlerTests | `Handle_ExistingSubjects_AddsSubjectAfterLastAndSaves` | last Order 3 → added subject Order 4, result Id == added Id |
| 18 | CreateSubjectHandlerTests | `Handle_NoSubjects_AddsSubjectAtOrderOne` | Order 1 |
| 19 | CreateSubjectHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | |
| 20 | UpdateSubjectHandlerTests | `Handle_ExistingSubject_RenamesAndSaves` | subject.Name |
| 21 | UpdateSubjectHandlerTests | `Handle_SubjectNotFound_ThrowsSubjectNotFound` | |
| 22 | UpdateSubjectHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | |
| 23 | ReorderSubjectHandlerTests | `Handle_MoveLastToFirst_RenumbersSiblingsAndSaves` | A,B,C; C→1 gives C=1, A=2, B=3 |
| 24 | ReorderSubjectHandlerTests | `Handle_PositionBeyondCount_MovesSubjectToEnd` | A→99 gives B=1, C=2, A=3 |
| 25 | ReorderSubjectHandlerTests | `Handle_SubjectNotFound_ThrowsSubjectNotFound` | |
| 26 | ReorderSubjectHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | |
| 27 | DeleteSubjectHandlerTests | `Handle_SubjectWithoutUnits_SoftDeletesAndSaves` | IsDeleted |
| 28 | DeleteSubjectHandlerTests | `Handle_SubjectHasUnits_ThrowsSubjectHasUnits` | `BusinessRuleViolationCoreException`, domain code |
| 29 | DeleteSubjectHandlerTests | `Handle_SubjectNotFound_ThrowsSubjectNotFound` | |
| 30 | DeleteSubjectHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | |
| 31 | GetSubjectHandlerTests | `Handle_ExistingSubject_ReturnsSubjectWithUnits` | Id, Name, Units mapped (Id, SubjectId, Name, Order) |
| 32 | GetSubjectHandlerTests | `Handle_SubjectNotFound_ThrowsSubjectNotFound` | |
| 33 | GetSubjectsHandlerTests | `Handle_Admin_ReturnsAllSubjectsWithUnitCounts` | counts `{A:2}` → A.UnitCount 2, B.UnitCount 0 |
| 34 | GetSubjectsHandlerTests | `Handle_Teacher_QueriesOnlyAssignedSubjects` | `subjectRepository.Received(1).FindAsync(Arg.Is<Expression<Func<Subject,bool>>>(e => e.Compile()(assigned) && !e.Compile()(other)), …)`, `GetAllAsync` DidNotReceive, and result equals the returned assigned list |
| 35 | GetSubjectsHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | |
| 36 | CreateUnitHandlerTests | `Handle_ExistingUnits_AddsUnitAfterLastAndSaves` | last Order 2 → 3, SubjectId, result Id |
| 37 | CreateUnitHandlerTests | `Handle_SubjectNotFound_ThrowsSubjectNotFound` | |
| 38 | CreateUnitHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | |
| 39 | UpdateUnitHandlerTests | `Handle_ExistingUnit_RenamesAndSaves` | |
| 40 | UpdateUnitHandlerTests | `Handle_UnitNotFound_ThrowsUnitNotFound` | |
| 41 | UpdateUnitHandlerTests | `Handle_UnitOfOtherSubject_ThrowsUnitNotFound` | unit name unchanged |
| 42 | UpdateUnitHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | |
| 43 | ReorderUnitHandlerTests | `Handle_MoveFirstToLast_RenumbersSiblingsAndSaves` | A,B,C; A→3 gives B=1, C=2, A=3 |
| 44 | ReorderUnitHandlerTests | `Handle_UnitNotInSubject_ThrowsUnitNotFound` | |
| 45 | ReorderUnitHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | |
| 46 | DeleteUnitHandlerTests | `Handle_ExistingUnit_SoftDeletesAndSaves` | |
| 47 | DeleteUnitHandlerTests | `Handle_UnitNotFound_ThrowsUnitNotFound` | |
| 48 | DeleteUnitHandlerTests | `Handle_UnitOfOtherSubject_ThrowsUnitNotFound` | IsDeleted false |
| 49 | DeleteUnitHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | |

### Validators (assert `Errors.Select(x => x.ErrorCode)` contains the code)
| # | Test class | Test methods |
|---|-----------|-------------|
| 50 | CreateSubjectValidatorTests | `Validate_ValidCommand_Passes` · `Validate_EmptyName_FailsWithSubjectNameRequired` · `Validate_WhitespaceName_FailsWithSubjectNameRequired` · `Validate_NameOverMax_FailsWithSubjectNameTooLong` (101 chars) |
| 51 | UpdateSubjectValidatorTests | `Validate_ValidCommand_Passes` · `Validate_EmptySubjectId_FailsWithSubjectIdRequired` · `Validate_EmptyName_FailsWithSubjectNameRequired` · `Validate_NameOverMax_FailsWithSubjectNameTooLong` |
| 52 | ReorderSubjectValidatorTests | `Validate_ValidCommand_Passes` · `Validate_EmptySubjectId_FailsWithSubjectIdRequired` · `Validate_PositionZero_FailsWithSubjectPositionInvalid` |
| 53 | DeleteSubjectValidatorTests | `Validate_ValidCommand_Passes` · `Validate_EmptySubjectId_FailsWithSubjectIdRequired` |
| 54 | GetSubjectValidatorTests | `Validate_ValidQuery_Passes` · `Validate_EmptySubjectId_FailsWithSubjectIdRequired` |
| 55 | CreateUnitValidatorTests | `Validate_ValidCommand_Passes` · `Validate_EmptySubjectId_FailsWithSubjectIdRequired` · `Validate_EmptyName_FailsWithUnitNameRequired` · `Validate_NameOverMax_FailsWithUnitNameTooLong` |
| 56 | UpdateUnitValidatorTests | `Validate_ValidCommand_Passes` · `Validate_EmptySubjectId_FailsWithSubjectIdRequired` · `Validate_EmptyUnitId_FailsWithUnitIdRequired` · `Validate_EmptyName_FailsWithUnitNameRequired` · `Validate_NameOverMax_FailsWithUnitNameTooLong` |
| 57 | ReorderUnitValidatorTests | `Validate_ValidCommand_Passes` · `Validate_EmptySubjectId_FailsWithSubjectIdRequired` · `Validate_EmptyUnitId_FailsWithUnitIdRequired` · `Validate_PositionZero_FailsWithUnitPositionInvalid` |
| 58 | DeleteUnitValidatorTests | `Validate_ValidCommand_Passes` · `Validate_EmptySubjectId_FailsWithSubjectIdRequired` · `Validate_EmptyUnitId_FailsWithUnitIdRequired` |

### Integration (real PostgreSQL via `ApiFactory`, shared DB: assert on seeded ids only, never on whole-list equality)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 59 | SubjectsEndpointTests | `Get_Admin_ReturnsSeededSubjectWithUnitCount` | 200; the seeded subject's item has `unitCount` 2 |
| 60 | SubjectsEndpointTests | `Get_Teacher_ReturnsOnlyAssignedSubjects` | contains the assigned id and not the other seeded id |
| 61 | SubjectsEndpointTests | `Get_Anonymous_Returns401` | |
| 62 | SubjectsEndpointTests | `GetById_Admin_ReturnsUnitsInOrder` | units seeded with orders 2,1 come back in order 1,2 |
| 63 | SubjectsEndpointTests | `GetById_UnknownSubject_Returns404SubjectNotFound` | code |
| 64 | SubjectsEndpointTests | `GetById_UnassignedTeacher_Returns403SubjectOutOfScope` | code |
| 65 | SubjectsEndpointTests | `GetById_AssignedTeacher_Returns200` | |
| 66 | SubjectsEndpointTests | `Post_Admin_PersistsSubjectAndWritesAuditRow` | 200, id; DB row exists with Order ≥ 1; `AuditLogs` row `Subject.Create`, ResourceId = id, Outcome `Success`, Diff not null |
| 67 | SubjectsEndpointTests | `Post_EmptyName_Returns422SubjectNameRequired` | status, code contains |
| 68 | SubjectsEndpointTests | `Post_Teacher_Returns403` | |
| 69 | SubjectsEndpointTests | `Post_Anonymous_Returns401` | |
| 70 | SubjectsEndpointTests | `Put_Admin_RenamesSubject` | DB Name |
| 71 | SubjectsEndpointTests | `Put_UnknownSubject_Returns404SubjectNotFound` | |
| 72 | SubjectsEndpointTests | `Put_Teacher_Returns403` | |
| 73 | SubjectsEndpointTests | `PutPosition_MoveBeforeSibling_ReordersSubjects` | seed A (created first), then B. PUT B position = A's current Order. Result: `ReadOrderAsync(B) < ReadOrderAsync(A)` |
| 74 | SubjectsEndpointTests | `PutPosition_PositionZero_Returns422SubjectPositionInvalid` | |
| 75 | SubjectsEndpointTests | `PutPosition_Student_Returns403` | |
| 76 | SubjectsEndpointTests | `Delete_SubjectWithoutUnits_SoftDeletesAndAudits` | 200, row `IsDeleted` (IgnoreQueryFilters), audit `Subject.Delete` Success |
| 77 | SubjectsEndpointTests | `Delete_SubjectWithUnits_Returns400SubjectHasUnitsAndAuditsFailure` | 400, code, not deleted, audit row Outcome `Failure` ErrorCode `SUBJECT_HAS_UNITS` |
| 78 | SubjectsEndpointTests | `Delete_Teacher_Returns403` | |
| 79 | UnitsEndpointTests | `Post_Admin_AppendsUnitAtEndAndAudits` | existing unit order 1 → new unit Order 2; audit `Unit.Create` |
| 80 | UnitsEndpointTests | `Post_UnknownSubject_Returns404SubjectNotFound` | |
| 81 | UnitsEndpointTests | `Post_EmptyName_Returns422UnitNameRequired` | |
| 82 | UnitsEndpointTests | `Post_Teacher_Returns403` | |
| 83 | UnitsEndpointTests | `Post_Anonymous_Returns401` | |
| 84 | UnitsEndpointTests | `Put_Admin_RenamesUnit` | |
| 85 | UnitsEndpointTests | `Put_UnitOfOtherSubject_Returns404UnitNotFound` | name unchanged |
| 86 | UnitsEndpointTests | `Put_Teacher_Returns403` | |
| 87 | UnitsEndpointTests | `PutPosition_MoveLastToFirst_RenumbersUnits` | units 1,2,3; move 3rd → 1 gives orders 1,2,3 = C,A,B |
| 88 | UnitsEndpointTests | `PutPosition_PositionZero_Returns422UnitPositionInvalid` | |
| 89 | UnitsEndpointTests | `PutPosition_Teacher_Returns403` | |
| 90 | UnitsEndpointTests | `Delete_Admin_SoftDeletesUnit` | IsDeleted; subject GET no longer lists it |
| 91 | UnitsEndpointTests | `Delete_UnknownUnit_Returns404UnitNotFound` | |
| 92 | UnitsEndpointTests | `Delete_Teacher_Returns403` | |
| 93 | AuditChangeCaptureTests | `SaveChangesAsync_NonAuditedEntity_RecordsNothing` (**modify**: Otp instead of Subject) | collector empty |

### Web (Vitest + RTL + MSW handlers from `subjects.msw` / `units.msw`; `userEvent.setup()`; `renderApp('/admin/content', { session: testSessions.admin })`)
| # | File | `it(...)` | Asserts |
|---|------|-----------|---------|
| 94 | nameSchema.test.ts | `accepts a non-empty name` / `trims surrounding spaces` / `rejects an empty name with validation.required` / `rejects a whitespace-only name` | parse result/issue message |
| 95 | position.test.ts | `returns the previous position for up` (index 2 → 2) / `returns the next position for down` (index 0 → 2) | |
| 96 | ContentPage.test.tsx | `shows subjects in order after loading` | loading status `Loading subjects`, then list items in order with "2 units" |
| 97 | ContentPage.test.tsx | `shows the empty state when there are no subjects` | `No subjects yet…` |
| 98 | ContentPage.test.tsx | `shows an error and recovers on retry` | alert "Could not load the content tree", then Retry shows the list |
| 99 | ContentPage.test.tsx | `adds a subject and refreshes the list` | POST body `{ name: 'Chemistry' }` captured; new item visible; toast "Subject added."; input cleared |
| 100 | ContentPage.test.tsx | `shows the required error without calling the API` | submit empty → "This field is required."; no POST |
| 101 | ContentPage.test.tsx | `shows a server name error inline` | POST 422 `SUBJECT_NAME_TOO_LONG` → "Subject name is too long." under the field |
| 102 | ContentPage.test.tsx | `moves a subject down` | click "Move Physics down" → PUT `/position` body `{ position: 2 }`; toast "Subject moved."; first item's up button and last item's down button are disabled |
| 103 | ContentPage.test.tsx | `renames a subject` | Rename → New name → Save → PUT body `{ name }`; toast |
| 104 | ContentPage.test.tsx | `deletes a subject after confirmation` | Delete → "Delete Physics?" → Yes, delete → DELETE called; Cancel path sends no request |
| 105 | ContentPage.test.tsx | `shows an error toast when a subject with units cannot be deleted` | DELETE 400 `SUBJECT_HAS_UNITS` → toast text |
| 106 | ContentPage.test.tsx | `renders right-to-left in Arabic` | heading "شجرة المحتوى", `dir="rtl"` |
| 107 | ContentPage.test.tsx | `has no axe violations` | |
| 108 | UnitPanel.test.tsx | `shows the units of an expanded subject` | "Show units" → `aria-expanded=true`, units in order |
| 109 | UnitPanel.test.tsx | `shows the empty state for a subject without units` | |
| 110 | UnitPanel.test.tsx | `shows an error and recovers on retry` | alert "Could not load units" → Retry |
| 111 | UnitPanel.test.tsx | `adds a unit` | POST `/api/subjects/{id}/units` body; toast "Unit added." |
| 112 | UnitPanel.test.tsx | `moves a unit up` | PUT `/units/{id}/position` `{ position: 1 }` |
| 113 | UnitPanel.test.tsx | `renames a unit` | PUT `/units/{id}` body `{ name }`; toast "Unit renamed." |
| 113b | UnitPanel.test.tsx | `deletes a unit after confirmation` | DELETE called after "Yes, delete"; toast "Unit deleted." |
| 114 | UnitPanel.test.tsx | `has no axe violations when expanded` | |

## Definition of done
- [ ] `Subject` has `Order` and `IAuditedEntity`. `CurriculumUnit` exists in `Elmanhg.Domain.Units`. Each domain method matches Domain behaviour: guard, then mutate, then stamp `UpdatedBy`/`UpdationDate`.
- [ ] The 8 commands implement `IAuditableCommand` with the Action/ResourceType/Id in the Audit table. Create results implement `IAuditableResult` explicitly.
- [ ] `GetSubjectQuery` implements `ISubjectScopedRequest`. The `GetSubjects` handler filters by teacher assignments.
- [ ] Every handler guards the current user first (except GetSubject). There is exactly one `SaveChangesAsync`, no try/catch, `ConfigureAwait(false)` everywhere, and `asNoTracking: true` on every read that does not mutate.
- [ ] `ICurriculumUnitRepository` has only `AnyInSubjectAsync` and `CountBySubjectAsync` beyond the base, and both run in SQL.
- [ ] `AppDbContext`: `DbSet<CurriculumUnit> Units { get; set; }`, `ConfigureUnits` is a named method, the FK is Restrict, the `(SubjectId, Order)` index exists, and the global filter line is added.
- [ ] Migration `AddSubjectOrderAndUnits` is generated. It includes the backfill SQL and no Drop or Rename, and the snapshot is updated.
- [ ] `ContentOptions` is bound with `ValidateDataAnnotations().ValidateOnStart()`. The section is in `appsettings.example.json` and `ApiFactory`. The tests pass without `api/Elmanhg.Api/appsettings.json`.
- [ ] All 10 new codes are in both resx files. Web `errors.*` has the 9 UI-reachable codes in en and ar.
- [ ] Controllers are thin. Every action has `[Authorize(Policy = DefaultCodes.X)]` as in API surface, a `Name`, `[ProducesResponseType]`, and a `CancellationToken`. `EndpointAuthorizationTests` stays green.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated/**` are regenerated. `npm run gen:api` produces no diff.
- [ ] Postman has the "Subjects" and "Units" folders with 10 requests.
- [ ] `docs/audit-log.md` lists the 8 commands and the `Subject` and `CurriculumUnit` entities.
- [ ] Every test in the Test plan exists with exactly these names. The only existing tests modified are the 6 listed (signature change, plus the Otp swap in #93). No test is skipped or weakened.
- [ ] `/admin/content` renders `ContentPage`, which has loading/empty/error states for both the subject list and the unit panel. Up/down buttons have `aria-label`s and are disabled at the edges. Deletes are confirmed inline. Every mutation invalidates the affected queries and toasts.
- [ ] Web uses no hard-coded strings (en and ar keys for every string), no physical-direction utilities, and no raw colours or arbitrary values. No web file is over 200 lines and no component is over 120.
- [ ] `dotnet build` shows zero new warnings. `dotnet test` is green with Docker. `dotnet format --verify-no-changes` exits 0.
- [ ] Web: `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check .` and `npx vitest run --coverage` all exit 0, and `src/features/**` meets its thresholds.
- [ ] No new NuGet or npm dependency.
