# Plan — [E2.S3] Lesson lifecycle: draft, published, archived (#62)

## Goal
An admin can publish a Draft lesson, move a Published or Archived lesson back to Draft, archive a Published lesson, and republish an Archived one. They can also reorder lessons inside a unit and soft-delete a lesson that is not Published. Each of these actions asks for confirmation in the content tree (`#/admin/content`), and the lifecycle and delete actions also sit in the lesson editor (`#/admin/lesson/:id`). Every action is audited. Publishing stamps `PublishedAt`. Entering or leaving Published raises a domain event, so E3's servable recalculation can subscribe later. Students and teachers reading a subject now see lesson counts for Published lessons only.

## Scope
**In:**
- Domain: `Lesson.Publish/Unpublish/Archive/MoveTo/Delete`, `Lesson.PublishedAt`, and three domain events (`LessonPublished`, `LessonUnpublished`, `LessonArchived`).
- Application: 5 audited commands (PublishLesson, UnpublishLesson, ArchiveLesson, ReorderLesson, DeleteLesson), each with a validator and a handler.
- Student-facing filter: `GetSubject` lesson counts are Published-only for non-admins.
- EF migration for `PublishedAt`, resx, Postman, and regenerated OpenAPI/Orval output.
- Web: per-lesson move up/down, lifecycle and delete actions with inline confirmation in the tree; lifecycle and delete in the editor header.
- Docs: PRD §5.2 transition rules, `docs/audit-log.md`, and `docs/claude-design-prompt.md` §4.

**Out:**
- Showing `PublishedAt` in any result or the UI. Nothing reads it yet; the student browsing story exposes it.
- A lesson rename action in the tree (the editor owns the name).
- A reorder action in the editor.
- The "lesson has questions" delete guard, servable recalculation handlers and question counts in the tree. All of these arrive with E3 Questions.
- A student lesson page or student lesson queries (browsing story, E-student). No student lesson query exists today (see D7).
- Drag-and-drop (#142).

**Deferred:** none. Everything in the story and the orchestrator additions can be built and tested offline.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Scope additions | Lesson reorder (1-based move among siblings, same shape as `ReorderUnit`) and lesson soft-delete are **in**. | Orchestrator decision. PRD §10.1 asks for "CRUD for … Lessons with ordering", and no other story covers them (#61 Out list). |
| D2 | Transition rules | **Publish**: Draft → Published or Archived → Published. Published → `LESSON_ALREADY_PUBLISHED`. **Unpublish**: Published → Draft or Archived → Draft. Draft → `LESSON_ALREADY_DRAFT`. **Archive**: Published → Archived. Draft → `LESSON_NOT_PUBLISHED`; Archived → `LESSON_ALREADY_ARCHIVED`. | PRD §5.2 plus the orchestrator chain (Draft → Published → Archived, unpublish back to Draft). The prototype (`A.lessonState`) lets an archived lesson go back to published or draft, so both exits exist. Draft → Archived is refused: a draft that has never been visible is deleted, not archived (D8). |
| D3 | Domain events | `sealed record LessonPublished/LessonUnpublished/LessonArchived(Guid LessonId, Guid UnitId) : DomainEvent` (Core.DDD). Raised with `Entity.RaiseDomainEvent` **only when the lesson enters or leaves Published**: `LessonPublished` on every Publish, `LessonUnpublished` only when Unpublish starts from Published, `LessonArchived` on every Archive (which always starts from Published). `CoreDbContext.SaveChangesAsync` dispatches them through MediatR `Publish`. There is no consumer in this story. | PRD §17 rule 1: servable depends on `lesson.state == Published`, so these are exactly the servability changes. Archived → Draft changes nothing a consumer cares about. The dispatch mechanism is vendored Core (Morabh `Core.DDD/Entities/Entity.cs` + `Core.EntityFrameworkCore/Context/CoreDbContext.cs`), so nothing new is built. |
| D4 | `PublishedAt` | `DateTimeOffset? PublishedAt`, null until the first publish. Every Publish overwrites it with `DateTimeOffset.UtcNow`. Unpublish and Archive keep it. Stored (`timestamptz`, nullable), not in any result. | PRD §15 `published_at`. "Last published at" keeps the history that §5.2 asks for. No reader exists yet, so exposing it would be dead API. |
| D5 | Policies | Publish, unpublish and archive use `DefaultCodes.LessonsPublish`. Reorder and delete use `DefaultCodes.ContentManage`. Both already exist and are Admin-only (`PermissionMatrixPolicies`). | PRD §16 separates "Publish lessons" from "Create / edit content". The constants already exist, so there are no new policies. |
| D6 | Routes | `POST /api/lessons/{id}/publish`, `/unpublish`, `/archive` (no body). `PUT /api/lessons/{id}/position` (body `{ position }`). `DELETE /api/lessons/{id}`. | These mirror `PUT …/units/{id}/position` and `DELETE …/units/{id}`. The lifecycle verbs are commands, not field updates, so they use POST. |
| D7 | "Student queries filter to published only" | The only lesson data a non-admin can read today is `UnitResult.LessonCount` in `GET /api/subjects/{id}` (`ContentBrowse`). `GetSubjectHandler` passes `publishedOnly = role != Admin` into `ILessonRepository.CountByUnitAsync(unitIds, publishedOnly, ct)`. Every lesson endpoint in `LessonsController` stays Admin-only (`ContentManage`/`LessonsPublish`), so Drafts cannot leak through them. **Rule for later stories** (goes into PRD §5.2): any lesson read reachable by Student or Teacher filters `State == LessonState.Published` in the repository predicate. Servable question queries join on the same predicate. | This is the literal sub-task applied where lessons are read for non-admins. Teachers get the same filter: PRD §16 "Browse published tree". A missing role claim counts as non-admin (fail closed). |
| D8 | Delete rules | `Lesson.Delete(deletedBy)` is allowed from Draft or Archived. Published → `LESSON_IS_PUBLISHED`. It soft-deletes the lesson and every loaded live objective. Siblings are not renumbered, mirroring `DeleteUnitHandler`; the next reorder or create closes the gap. | A soft delete of a visible lesson would silently remove servable questions later, so the admin must unpublish or archive first. Objectives belong to the aggregate. |
| D9 | Reorder semantics | This is a copy of `ReorderUnitHandler`: load the siblings (same `UnitId`, order `Order` then `CreationDate`), remove the lesson, insert it at `Math.Min(position, count + 1) - 1`, then `MoveTo(index + 1)` for all. The lesson is loaded first with `GetByIdAsync` because the route has no unit id. | #60 D-reorder. A position past the end clamps to last. |
| D10 | Editing a Published/Archived lesson | This is unchanged: `UpdateLesson` works in every state. | No sub-task restricts it, and PRD §5.2 only defines visibility. |
| D11 | Publish preconditions | None. No content-completeness check. | PRD defines none. A guard would be an invented domain rule. |
| D12 | HTTP for transition errors | Domain throws `BusinessRuleViolationCoreException` (400), mirroring `UNIT_HAS_LESSONS` and `LESSON_OBJECTIVE_UNKNOWN`. | Same exception, same status as every existing content rule. There is no new exception type. |
| D13 | `Lesson.cs` length | `Lesson` becomes `public partial class`. The state transitions live in a new `Lesson.Lifecycle.cs`. `Lesson.cs` keeps the properties plus Create, Update, MoveTo and Delete. | SKILL §1 "~80–100 lines max". Private setters must stay on the entity, so a partial class is the only split that keeps them private. |
| D14 | Confirmation UI | Inline confirmation, the same pattern as `ItemActions` "confirmingDelete". The action buttons are replaced by a sentence, a confirm button and Cancel. This is not a modal. | The existing delete confirmation already uses this pattern. It needs no focus trap, and the tree stays scannable. |
| D15 | Which actions show | `availableLessonActions(state)`: Draft → Publish, Delete. Published → Move to draft, Archive. Archived → Publish, Move to draft, Delete. Unpublish is labelled "Move to draft" because it also applies to Archived. | The same table as D2 and D8, so the UI never offers an action the server will refuse. |
| D16 | UI placement | Tree lesson rows get move up/down plus `LessonActions`. The editor header (next to the state badge) gets `LessonActions`. After a delete in the editor the page navigates to `/admin/content`. | The orchestrator asked for actions in both the tree and the editor. Reorder needs sibling context, which only the tree has. |
| D17 | Cache refresh | Every lesson mutation invalidates every query whose first key starts with `/api/subjects` or `/api/lessons` (`isContentQuery`). Delete excludes the deleted lesson's own detail key, so the editor does not refetch a 404 before it navigates. | The editor does not know the subject id. Orval keys are URL strings, so there is no shared prefix across `/api/lessons` and `/api/lessons/{id}`. |
| D18 | Event dispatch test | Integration tests record events through a test-assembly `INotificationHandler` (`LessonEventRecorder`) that writes to a singleton `LessonEventLog` on `ApiFactory`. `ApiFactory` already scans the test assembly with `AddMediatR`. | Proves the events leave `SaveChangesAsync`, following the same pattern as `RecordingSmsSender`. |
| D19 | CI parity | No new configuration keys and no new options. Tests use only `ApiFactory`'s in-memory configuration. | The suite must pass without the gitignored `api/Elmanhg.Api/appsettings.json`. |
| D20 | Docs | PRD §5.2 gains the transition table, the events and the D7 read rule. `docs/audit-log.md` gains 5 rows. `claude-design-prompt.md` §4 admin bullet gains delete, confirmation and editor actions. | docs-sync: these are a policy change and a UI content change. |

Morabh reuse: domain events and their dispatch are vendored Core. Morabh sources are `Core/Core.DDD/Entities/DomainEvent.cs`, `Entity.cs` and `Core/Core.EntityFrameworkCore/Context/CoreDbContext.cs`. Reorder and delete mirror #60's `ReorderUnit` and `DeleteUnit` slices. Lifecycle commands, events and UI are new, with no Morabh equivalent.

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Lessons/Lesson.cs` | `public class Lesson` → `public partial class Lesson : AuditEntity, IAuditedEntity` (base list stays here only). Add `public DateTimeOffset? PublishedAt { get; private set; }` after `VideoUrl`. Add `MoveTo` and `Delete` (Domain behaviour). |
| `api/Elmanhg.Domain/Lessons/ILessonRepository.cs` | `Task<Dictionary<Guid, int>> CountByUnitAsync(IReadOnlyCollection<Guid> unitIds, bool publishedOnly, CancellationToken cancellationToken);` |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Under `// CONTENT` append: `LessonAlreadyPublished = "LESSON_ALREADY_PUBLISHED"`, `LessonAlreadyDraft = "LESSON_ALREADY_DRAFT"`, `LessonAlreadyArchived = "LESSON_ALREADY_ARCHIVED"`, `LessonNotPublished = "LESSON_NOT_PUBLISHED"`, `LessonIsPublished = "LESSON_IS_PUBLISHED"`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | In `// LESSONS` after `LessonIdRequired`: `public const string LessonPositionInvalid = "LESSON_POSITION_INVALID";` |
| `api/Elmanhg.Application/Subjects/GetSubject/GetSubjectHandler.cs` | Constructor: `(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, ICurrentUserService currentUserService)`. Before the count, add `var publishedOnly = currentUserService.GetClaim(ClaimTypes.Role) != nameof(UserRole.Admin);`. Count call: `lessonRepository.CountByUnitAsync(units.Select(x => x.Id).ToList(), publishedOnly, cancellationToken)`. Usings: `Core.Identity.Tokens.CurrentUser`, `Elmanhg.Domain.Identity`, `System.Security.Claims`. No user-null guard (it is a query; the behaviour matches today). |
| `api/Elmanhg.Infrastructure/Lessons/LessonRepository.cs` | `CountByUnitAsync(IReadOnlyCollection<Guid> unitIds, bool publishedOnly, CancellationToken cancellationToken)`: `.Where(x => unitIds.Contains(x.UnitId) && (!publishedOnly \|\| x.State == LessonState.Published))`, rest unchanged. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by the migration. `AppDbContext.cs` itself is **unchanged**: `DateTimeOffset?` maps by convention. |
| `api/Elmanhg.Api/Controllers/Lessons/LessonsController.cs` | Add the 5 actions in API surface, after `UpdateLesson`. Usings for the 5 use-case namespaces. |
| `api/Elmanhg.Api/Controllers/Lessons/Requests.cs` | Append `public sealed record LessonPositionRequest(int Position);` |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | The 6 keys in Error codes. |
| `api/openapi/v1.json` | Regenerated by `dotnet build api/Elmanhg.Api`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `public LessonEventLog LessonEvents { get; } = new();`. In `ConfigureTestServices` add `services.AddSingleton(LessonEvents);`. |
| `api/Elmanhg.Tests/Integration/Content/ContentTestData.cs` | Add `public static async Task<Guid> SeedLessonInStateAsync(ApiFactory factory, Guid unitId, string name, int order, LessonState state, CancellationToken cancellationToken)`. It loads the unit and runs `Lesson.Create(unit, name, order, creator)`. For `state is LessonState.Published or LessonState.Archived` it calls `lesson.Publish(creator)`. For `Archived` it then calls `lesson.Archive(creator)`. Then `lesson.ClearDomainEvents()`, `context.Lessons.Add`, save and return the id. |
| `api/Elmanhg.Tests/Application/Features/Subjects/GetSubject/GetSubjectHandlerTests.cs` | **modify**: add `ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>()` and pass it as the 4th constructor argument. In `Handle_ExistingSubject_ReturnsSubjectWithUnits`, stub `_currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin))` and change the count stub to `CountByUnitAsync(Arg.Any<IReadOnlyCollection<Guid>>(), false, Arg.Any<CancellationToken>())`. Assertions stay unchanged. Add row A40. |
| `api/Elmanhg.Tests/Integration/Content/SubjectsEndpointTests.cs` | Add rows I21–I22. Add helper `private async Task<HttpClient> StudentClientAsync()` → `ScopeTestData.SeedStudentAsync` + `ScopeTestData.SignedInClientAsync` (same shape as `AdminClientAsync`). |
| `postman/elmanhg.postman_collection.json` | "Lessons" folder: add "Publish lesson", "Unpublish lesson", "Archive lesson" (POST, no body), "Reorder lesson" (PUT `{"position": 1}`), "Delete lesson" (DELETE), all with `{{lessonId}}` and Bearer auth like the siblings. Append to the folder description: "Publish, unpublish and archive need Lessons.Publish (Admin). Allowed moves: Draft→Published, Archived→Published, Published/Archived→Draft, Published→Archived. A published lesson cannot be deleted." |
| `docs/PRD.md` | §5.2: after "Only Admin publishes…" add the **Transitions** block (text below). |
| `docs/audit-log.md` | "Audited commands": after the `UploadLessonImage` row add the 5 rows in the Audit table. |
| `docs/claude-design-prompt.md` | §4 Admin bullet: replace "`#/admin/content` subject, unit, lesson tree with reorder, publish, unpublish, archive;" with "`#/admin/content` subject, unit, lesson tree with reorder, publish, move to draft, archive and delete, each confirmed inline and offered only when the lesson's state allows it;". At the end of the editor sentence, append ", plus the same publish, move to draft, archive and delete actions next to the state badge". |
| `web/src/features/content/components/UnitLessons.tsx` | Replace the inline `<li>` in `data.map` with `data.map((lesson, index) => <LessonItem key={lesson.id} lesson={lesson} isFirst={index === 0} isLast={index === data.length - 1} position={index + 1} />)`. Remove the now-unused `Link` and `LessonStateBadge` imports. |
| `web/src/features/content/pages/LessonEditorPage.tsx` | `const navigate = useNavigate();` (from `@tanstack/react-router`). Inside the title `<div className="flex flex-wrap items-center gap-2">`, after the badge: `{data ? <LessonActions lessonId={data.id} name={data.name} state={data.state} onDeleted={() => { void navigate({ to: '/admin/content' }); }} /> : null}`. |
| `web/src/features/content/i18n/en.json`, `ar.json` | Keys in "Locale keys". |
| `web/src/shared/i18n/en.json`, `ar.json` | `errors.*` for the 6 codes in Error codes, with the same text as the resx. |
| `web/src/shared/api/generated/**` | `npm run gen:api` after the API build. Adds `usePublishLesson`, `useUnpublishLesson`, `useArchiveLesson`, `useReorderLesson`, `useDeleteLesson`, their `get…MockHandler`s, `LessonPositionRequest`, and the zod files. Never hand-edited. |

## Files to create
Conventions (#60 and #61 carry-over):
- Every command handler starts with `if (currentUserService.UserId == null || currentUserService.UserId == default) { throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated); }`.
- Every command ends with exactly one `SaveChangesAsync`.
- `.ConfigureAwait(false)` on every await, braces on every `if`, one-line class declarations.
- Application `ErrorCodes` = `Elmanhg.Application.Exceptions.ErrorCodes`; domain `ErrorCodes` = `Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`.
- Commands implement `IRequest, IAuditableCommand` with `AuditResourceType => "Lesson"` and `AuditResourceId => LessonId`.
- Validators have no constructor parameters.

### Domain (`api/Elmanhg.Domain/Lessons/`, namespace `Elmanhg.Domain.Lessons`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `Lesson.Lifecycle.cs` | partial entity | `public partial class Lesson` (no base list) with `public void Publish(Guid publishedBy)`, `public void Unpublish(Guid updatedBy)` and `public void Archive(Guid updatedBy)`. Bodies are in Domain behaviour. Usings: `Core.Errors`, `Elmanhg.Domain.SharedKernel.Exceptions`. |
| 2 | `LessonPublished.cs` | domain event | `using Core.DDD.Entities;` `public sealed record LessonPublished(Guid LessonId, Guid UnitId) : DomainEvent;` |
| 3 | `LessonUnpublished.cs` | domain event | `public sealed record LessonUnpublished(Guid LessonId, Guid UnitId) : DomainEvent;` |
| 4 | `LessonArchived.cs` | domain event | `public sealed record LessonArchived(Guid LessonId, Guid UnitId) : DomainEvent;` |

### Application (all under `api/Elmanhg.Application/Lessons/`, namespace `Elmanhg.Application.Lessons.<UseCase>`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 5 | `PublishLesson/PublishLessonCommand.cs` | command | `public sealed record PublishLessonCommand(Guid LessonId) : IRequest, IAuditableCommand` · `AuditAction => "Lesson.Publish"` |
| 6 | `PublishLesson/PublishLessonValidator.cs` | validator | `RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);` |
| 7 | `PublishLesson/PublishLessonHandler.cs` | handler | `(ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<PublishLessonCommand>`. 1. User guard. 2. `var lesson = await lessonRepository.GetByIdAsync(request.LessonId, cancellationToken).ConfigureAwait(false);`. If null, throw `NotFoundCoreException(ErrorCodes.LessonNotFound)`. 3. `lesson.Publish(currentUserService.UserId.Value);` 4. `SaveChangesAsync`. |
| 8 | `UnpublishLesson/UnpublishLessonCommand.cs` · `UnpublishLessonValidator.cs` · `UnpublishLessonHandler.cs` | set | Same as 5–7 with `UnpublishLessonCommand`, `"Lesson.Unpublish"`, and `lesson.Unpublish(userId)`. |
| 9 | `ArchiveLesson/ArchiveLessonCommand.cs` · `ArchiveLessonValidator.cs` · `ArchiveLessonHandler.cs` | set | Same as 5–7 with `ArchiveLessonCommand`, `"Lesson.Archive"`, and `lesson.Archive(userId)`. |
| 10 | `ReorderLesson/ReorderLessonCommand.cs` | command | `public sealed record ReorderLessonCommand(Guid LessonId, int Position) : IRequest, IAuditableCommand` · `"Lesson.Reorder"` |
| 11 | `ReorderLesson/ReorderLessonValidator.cs` | validator | `RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);` · `RuleFor(x => x.Position).ValidateMin(1, ErrorCodes.LessonPositionInvalid);` |
| 12 | `ReorderLesson/ReorderLessonHandler.cs` | handler | `(ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<ReorderLessonCommand>`. 1. User guard. 2. `lesson = GetByIdAsync(request.LessonId, ct)` (tracked). If null, throw `NotFoundCoreException(ErrorCodes.LessonNotFound)`. 3. `var siblings = await lessonRepository.FindAsync(x => x.UnitId == lesson.UnitId, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate)).ConfigureAwait(false);` 4. `siblings.RemoveAll(x => x.Id == lesson.Id); siblings.Insert(Math.Min(request.Position, siblings.Count + 1) - 1, lesson);` 5. `for (var index = 0; index < siblings.Count; index++) { siblings[index].MoveTo(index + 1, currentUserService.UserId.Value); }` 6. `SaveChangesAsync`. |
| 13 | `DeleteLesson/DeleteLessonCommand.cs` | command | `public sealed record DeleteLessonCommand(Guid LessonId) : IRequest, IAuditableCommand` · `"Lesson.Delete"` |
| 14 | `DeleteLesson/DeleteLessonValidator.cs` | validator | `RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);` |
| 15 | `DeleteLesson/DeleteLessonHandler.cs` | handler | `(ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<DeleteLessonCommand>`. 1. User guard. 2. `var lesson = await lessonRepository.GetWithObjectivesAsync(request.LessonId, asNoTracking: false, cancellationToken).ConfigureAwait(false);`. If null, throw `NotFoundCoreException(ErrorCodes.LessonNotFound)`. 3. `lesson.Delete(currentUserService.UserId.Value);` (domain throws `LESSON_IS_PUBLISHED`) 4. `SaveChangesAsync`. |

**Audit** (goes into `docs/audit-log.md`):
| Command | AuditAction | ResourceType | Id source |
|---|---|---|---|
| PublishLesson | `Lesson.Publish` | Lesson | command |
| UnpublishLesson | `Lesson.Unpublish` | Lesson | command |
| ArchiveLesson | `Lesson.Archive` | Lesson | command |
| ReorderLesson | `Lesson.Reorder` | Lesson | command (the diff lists every sibling whose `Order` changed) |
| DeleteLesson | `Lesson.Delete` | Lesson | command (the diff lists the lesson and every objective soft-deleted with it) |

### Infrastructure
| # | Path | Type | Contract |
|---|------|------|----------|
| 16–17 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddLessonPublishedAt.cs` + `.Designer.cs` | migration | `dotnet ef migrations add AddLessonPublishedAt -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Expected content is only `AddColumn<DateTimeOffset>(name: "PublishedAt", table: "Lessons", type: "timestamp with time zone", nullable: true)` and the matching `DropColumn` in `Down`. No other operations. |

### API
The 5 actions in `LessonsController` (modify) are specified in API surface. There are no new API files.

### Tests (api)
| # | Path |
|---|------|
| 18 | `api/Elmanhg.Tests/Domain/Lessons/LessonLifecycleTests.cs` |
| 19–23 | `api/Elmanhg.Tests/Application/Features/Lessons/{PublishLesson,UnpublishLesson,ArchiveLesson,ReorderLesson,DeleteLesson}/<UseCase>HandlerTests.cs` |
| 24–28 | `api/Elmanhg.Tests/Application/Features/Lessons/{PublishLesson,UnpublishLesson,ArchiveLesson,ReorderLesson,DeleteLesson}/<UseCase>ValidatorTests.cs` |
| 29 | `api/Elmanhg.Tests/Integration/Infrastructure/LessonEventLog.cs`: `public sealed class LessonEventLog { private readonly ConcurrentQueue<DomainEvent> _events = new(); public void Record(DomainEvent domainEvent) { _events.Enqueue(domainEvent); } public IReadOnlyList<DomainEvent> Events => [.. _events]; }` |
| 30 | `api/Elmanhg.Tests/Integration/Infrastructure/LessonEventRecorder.cs`: `public sealed class LessonEventRecorder(LessonEventLog log) : INotificationHandler<LessonPublished>, INotificationHandler<LessonUnpublished>, INotificationHandler<LessonArchived>`. Each `Handle(TEvent notification, CancellationToken cancellationToken)` calls `log.Record(notification); return Task.CompletedTask;`. It is discovered by the existing test-assembly `AddMediatR` scan. |
| 31 | `api/Elmanhg.Tests/Integration/Content/LessonLifecycleEndpointTests.cs` (publish, unpublish, archive). Private helpers mirror `LessonsEndpointTests`: `SeedUnitAsync`, `AdminClientAsync`, `TeacherClientAsync(Guid)`, `StudentClientAsync()`, `ReadCodeAsync`. Lessons are seeded with `ContentTestData.SeedLessonInStateAsync`. |
| 32 | `api/Elmanhg.Tests/Integration/Content/LessonManagementEndpointTests.cs` (reorder and delete), with the same helpers. |

Handler tests use NSubstitute for `ILessonRepository` and `ICurrentUserService`. Lessons come from `Lesson.Create(CurriculumUnit.Create(Subject.Create("Physics", 1, id), "Mechanics", 1, id), name, order, id)`. Stub `GetByIdAsync(lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>())` and `GetWithObjectivesAsync(lesson.Id, false, Arg.Any<CancellationToken>())`. Domain codes use the alias `using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;`.

### Web (`web/src/features/content/` unless stated)
| # | Path | Type | Contract |
|---|------|------|----------|
| W-a | `api/lessonLifecycle.ts` | util | `export type LessonAction = 'publish' \| 'unpublish' \| 'archive' \| 'delete';` · `const actionsByState: Partial<Record<string, LessonAction[]>> = { Draft: ['publish', 'delete'], Published: ['unpublish', 'archive'], Archived: ['publish', 'unpublish', 'delete'] };` · `export function availableLessonActions(state: string): LessonAction[] { return actionsByState[state] ?? []; }` |
| W-b | `api/lessonLifecycle.test.ts` | test | Rows W1–W4. |
| W-c | `api/contentQueries.ts` | util | `import type { QueryKey } from '@tanstack/react-query';` · `const contentPrefixes = ['/api/subjects', '/api/lessons'];` · `export function isContentQuery(queryKey: QueryKey, excludedLessonId?: string): boolean { const [key] = queryKey; if (typeof key !== 'string') { return false; } if (excludedLessonId !== undefined && key === `/api/lessons/${excludedLessonId}`) { return false; } return contentPrefixes.some((prefix) => key.startsWith(prefix)); }` |
| W-d | `api/contentQueries.test.ts` | test | Rows W5–W7. |
| W-e | `hooks/useLessonMutations.ts` | hook | `export interface LessonMutations { publish: (lessonId: string) => void; unpublish: (lessonId: string) => void; archive: (lessonId: string) => void; move: (lessonId: string, position: number) => void; remove: (lessonId: string, onDeleted?: () => void) => void }` · `export function useLessonMutations(): LessonMutations`. It uses `useQueryClient()` and `useContentFeedback()`. `const settle = async (key: string, excludedLessonId?: string) => { success(key); await queryClient.invalidateQueries({ predicate: (query) => isContentQuery(query.queryKey, excludedLessonId) }); };`. Mutations: `usePublishLesson({ mutation: { onSuccess: () => settle('lessons.published'), onError: failure } })`, the same for `useUnpublishLesson` (`lessons.unpublished`), `useArchiveLesson` (`lessons.archived`) and `useReorderLesson` (`lessons.moved`), and `useDeleteLesson({ mutation: { onSuccess: (_data, variables) => settle('lessons.deleted', variables.lessonId), onError: failure } })`. It returns `publish: (lessonId) => { publishLesson.mutate({ lessonId }); }` (the same shape for unpublish and archive), `move: (lessonId, position) => { reorderLesson.mutate({ lessonId, data: { position } }); }` and `remove: (lessonId, onDeleted) => { deleteLesson.mutate({ lessonId }, { onSuccess: () => { onDeleted?.(); } }); }`. |
| W-f | `components/LessonActions.tsx` | component | `export interface LessonActionsProps { lessonId: string; name: string; state: string; onDeleted?: () => void }`. `const { publish, unpublish, archive, remove } = useLessonMutations();` `const [pending, setPending] = useState<LessonAction \| null>(null);` `const perform: Record<LessonAction, () => void> = { publish: () => { publish(lessonId); }, unpublish: () => { unpublish(lessonId); }, archive: () => { archive(lessonId); }, delete: () => { remove(lessonId, onDeleted); } };`. Root `<div role="group" aria-label={t('lessonActions.label', { name })} className="flex flex-wrap items-center gap-2">`. When `pending` is set: `<p className="text-ui text-text">{t(`lessonActions.confirm.${pending}`, { name })}</p>`, then `<Button size="sm" variant={pending === 'delete' \|\| pending === 'archive' ? 'danger' : 'primary'} onClick={() => { perform[pending](); setPending(null); }}>{t(`lessonActions.confirmButton.${pending}`)}</Button>`, then `<Button size="sm" variant="secondary" onClick={() => { setPending(null); }}>{t('actions.cancel')}</Button>`. Otherwise `availableLessonActions(state).map((action) => <Button key={action} size="sm" variant={action === 'delete' ? 'danger' : 'secondary'} onClick={() => { setPending(action); }}>{t(`lessonActions.${action}`)}</Button>)`. |
| W-g | `components/LessonItem.tsx` | component | `export interface LessonItemProps { lesson: LessonResult; isFirst: boolean; isLast: boolean; position: number }`. `const { move } = useLessonMutations();`. `<li className="flex flex-col gap-2 rounded-md border border-border bg-surface p-3">`. First row `<div className="flex flex-wrap items-center gap-2">`: `<Link to="/admin/lesson/$lessonId" params={{ lessonId: lesson.id }} className="text-ui font-semibold text-accent underline">{lesson.name}</Link><LessonStateBadge state={lesson.state} />`. Second row `<div className="flex flex-wrap items-center gap-2">`: a `sm` secondary `Button` with `aria-label={t('actions.moveUp', { name: lesson.name })}`, `disabled={isFirst}`, `onClick={() => { move(lesson.id, targetPosition(position - 1, 'up')); }}` wrapping `<ChevronUp aria-hidden className="size-4" />`; the same for down (`actions.moveDown`, `isLast`, `'down'`, `ChevronDown`); then `<LessonActions lessonId={lesson.id} name={lesson.name} state={lesson.state} />`. |
| W-h | `components/LessonItem.test.tsx` | test | Rows W8–W17 (through `renderApp('/admin/content', { session: testSessions.admin })`: expand "Show units", then "Show lessons"). |
| W-i | `components/LessonActions.test.tsx` | test | Rows W18–W21 (through `renderApp('/admin/lesson/l1', { session: testSessions.admin })`). |

Tree fixtures (W-h): subject `s1` Physics with unit `u1` Mechanics (`lessonCount: 3`). `lessons = [{ id: 'l1', unitId: 'u1', name: "Newton's laws", order: 1, state: 'Draft' }, { id: 'l2', …, name: 'Momentum', order: 2, state: 'Published' }, { id: 'l3', …, name: 'Energy', order: 3, state: 'Archived' }]`. Scope with `within(list).getAllByRole('listitem')` on the list "Lessons of Mechanics", then `within(row).getByRole('group', { name: 'Actions for <name>' })`. Record calls through `getPublishLessonMockHandler(({ params }) => { calls.push(params.lessonId); })` and the same for the other endpoints. For reorder, push `await request.json()`.

Locale keys (`content` namespace, en | ar):
- `lessons.moved` Lesson moved. | تم نقل الدرس. · `lessons.published` Lesson published. | تم نشر الدرس. · `lessons.unpublished` Lesson moved to draft. | تمت إعادة الدرس إلى مسودة. · `lessons.archived` Lesson archived. | تمت أرشفة الدرس. · `lessons.deleted` Lesson deleted. | تم حذف الدرس.
- `lessonActions.label` Actions for {name} | إجراءات {name}
- `lessonActions.publish` Publish | نشر · `.unpublish` Move to draft | إعادة إلى مسودة · `.archive` Archive | أرشفة · `.delete` Delete | حذف
- `lessonActions.confirm.publish` Publish {name}? Students will see it. | نشر {name}؟ سيظهر للطلاب. · `.unpublish` Move {name} to draft? Students will not see it. | إعادة {name} إلى مسودة؟ لن يظهر للطلاب. · `.archive` Archive {name}? Students will not see it. | أرشفة {name}؟ لن يظهر للطلاب. · `.delete` Delete {name}? | حذف {name}؟
- `lessonActions.confirmButton.publish` Yes, publish | نعم، انشر · `.unpublish` Yes, move to draft | نعم، أعد إلى مسودة · `.archive` Yes, archive | نعم، أرشف · `.delete` Yes, delete | نعم، احذف

PRD §5.2 **Transitions** text (insert verbatim):
> Transitions: **Publish** Draft or Archived → Published (sets `published_at`). **Unpublish** Published or Archived → Draft. **Archive** Published → Archived. A draft is deleted, not archived. A published lesson cannot be deleted; move it to draft or archive it first. Entering or leaving Published raises `LessonPublished`, `LessonUnpublished` or `LessonArchived`; servable and mastery recalculation subscribe to these. Every lesson read that a Student or Teacher can reach returns Published lessons only.

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| Domain `ErrorCodes.LessonAlreadyPublished` | `LESSON_ALREADY_PUBLISHED` | `Lesson.Publish` | `BusinessRuleViolationCoreException` | 400 |
| Domain `ErrorCodes.LessonAlreadyDraft` | `LESSON_ALREADY_DRAFT` | `Lesson.Unpublish` | `BusinessRuleViolationCoreException` | 400 |
| Domain `ErrorCodes.LessonAlreadyArchived` | `LESSON_ALREADY_ARCHIVED` | `Lesson.Archive` | `BusinessRuleViolationCoreException` | 400 |
| Domain `ErrorCodes.LessonNotPublished` | `LESSON_NOT_PUBLISHED` | `Lesson.Archive` (from Draft) | `BusinessRuleViolationCoreException` | 400 |
| Domain `ErrorCodes.LessonIsPublished` | `LESSON_IS_PUBLISHED` | `Lesson.Delete` | `BusinessRuleViolationCoreException` | 400 |
| `ErrorCodes.LessonPositionInvalid` | `LESSON_POSITION_INVALID` | `ReorderLessonValidator` | validation | 422 |
| `ErrorCodes.LessonIdRequired` (existing) | `LESSON_ID_REQUIRED` | all 5 validators | validation | 422 |
| `ErrorCodes.LessonNotFound` (existing) | `LESSON_NOT_FOUND` | all 5 handlers | `NotFoundCoreException` | 404 |
| Domain `ErrorCodes.ContentOrderInvalid` (existing) | `CONTENT_ORDER_INVALID` | `Lesson.MoveTo` | `BusinessRuleViolationCoreException` | 400 |

Resx (en | ar, no tashkeel, hamza style as in the existing ar.resx):
- LESSON_ALREADY_PUBLISHED: This lesson is already published. | هذا الدرس منشور بالفعل.
- LESSON_ALREADY_DRAFT: This lesson is already a draft. | هذا الدرس مسودة بالفعل.
- LESSON_ALREADY_ARCHIVED: This lesson is already archived. | هذا الدرس مؤرشف بالفعل.
- LESSON_NOT_PUBLISHED: Only a published lesson can be archived. Delete a draft instead. | يمكن ارشفة الدرس المنشور فقط. احذف المسودة بدلا من ذلك.
- LESSON_IS_PUBLISHED: Move this lesson to draft or archive it before deleting it. | اعد الدرس الى مسودة او ارشفه قبل حذفه.
- LESSON_POSITION_INVALID: Position must be 1 or more. | الترتيب يجب ان يكون 1 او اكثر.

## Domain behaviour
```csharp
// Lesson.cs (partial) — additions
public DateTimeOffset? PublishedAt { get; private set; }

public void MoveTo(int order, Guid updatedBy)
{
    if (order < 1) { throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid); }
    if (Order == order) { return; }
    Order = order; UpdatedBy = updatedBy; UpdationDate = DateTimeOffset.UtcNow;
}

public void Delete(Guid deletedBy)
{
    if (State == LessonState.Published) { throw new BusinessRuleViolationCoreException(ErrorCodes.LessonIsPublished); }
    foreach (var objective in Objectives) { objective.Delete(deletedBy); }
    SoftDelete(); UpdatedBy = deletedBy; UpdationDate = DateTimeOffset.UtcNow;
}

// Lesson.Lifecycle.cs (partial)
public void Publish(Guid publishedBy)
{
    if (State == LessonState.Published) { throw new BusinessRuleViolationCoreException(ErrorCodes.LessonAlreadyPublished); }
    State = LessonState.Published;
    PublishedAt = DateTimeOffset.UtcNow;
    UpdatedBy = publishedBy; UpdationDate = DateTimeOffset.UtcNow;
    RaiseDomainEvent(new LessonPublished(Id, UnitId));
}

public void Unpublish(Guid updatedBy)
{
    if (State == LessonState.Draft) { throw new BusinessRuleViolationCoreException(ErrorCodes.LessonAlreadyDraft); }
    var wasPublished = State == LessonState.Published;
    State = LessonState.Draft;
    UpdatedBy = updatedBy; UpdationDate = DateTimeOffset.UtcNow;
    if (wasPublished) { RaiseDomainEvent(new LessonUnpublished(Id, UnitId)); }
}

public void Archive(Guid updatedBy)
{
    if (State == LessonState.Archived) { throw new BusinessRuleViolationCoreException(ErrorCodes.LessonAlreadyArchived); }
    if (State == LessonState.Draft) { throw new BusinessRuleViolationCoreException(ErrorCodes.LessonNotPublished); }
    State = LessonState.Archived;
    UpdatedBy = updatedBy; UpdationDate = DateTimeOffset.UtcNow;
    RaiseDomainEvent(new LessonArchived(Id, UnitId));
}
```
The code above is compressed. Write it with braces on their own lines and one statement per line, following the repo style. The order is guard, then mutate, then stamp, then raise. Events are raised last, so a thrown guard leaves no event behind. `PublishedAt` is untouched by Unpublish and Archive. `ErrorCodes` is the Domain one.

## API surface
| Method | Route (Name) | Policy | Body | Response |
|---|---|---|---|---|
| POST | `/api/lessons/{lessonId:guid}/publish` (`PublishLesson`) | `DefaultCodes.LessonsPublish` | — | `200` |
| POST | `/api/lessons/{lessonId:guid}/unpublish` (`UnpublishLesson`) | `DefaultCodes.LessonsPublish` | — | `200` |
| POST | `/api/lessons/{lessonId:guid}/archive` (`ArchiveLesson`) | `DefaultCodes.LessonsPublish` | — | `200` |
| PUT | `/api/lessons/{lessonId:guid}/position` (`ReorderLesson`) | `DefaultCodes.ContentManage` | `LessonPositionRequest` | `200` |
| DELETE | `/api/lessons/{lessonId:guid}` (`DeleteLesson`) | `DefaultCodes.ContentManage` | — | `200` |
| GET | `/api/subjects/{subjectId}` (existing) | unchanged (`ContentBrowse`) | — | `units[].lessonCount` counts Published lessons only for Student and Teacher |

Every action has `[ProducesResponseType(StatusCodes.Status200OK)]` and the signature `([FromRoute] Guid lessonId, CancellationToken cancellationToken)`. Reorder adds `[FromBody] LessonPositionRequest request`. The body is `await mediator.Send(new XCommand(lessonId[, request.Position]), cancellationToken); return Ok();`.

## Test plan
### API — domain (`LessonLifecycleTests`, no doubles)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| A1 | LessonLifecycleTests | `Create_Always_HasNoPublishedAtAndNoEvents` | `PublishedAt` null, `GetDomainEvents()` empty |
| A2 | LessonLifecycleTests | `Publish_Draft_PublishesStampsAndRaisesLessonPublished` | State Published, `PublishedAt` not null, `UpdatedBy` = publisher, events `Equal(new LessonPublished(lesson.Id, lesson.UnitId))` |
| A3 | LessonLifecycleTests | `Publish_Archived_RepublishesAndRaisesLessonPublished` | Publish→Archive→`ClearDomainEvents()`→Publish: State Published, events = one `LessonPublished` |
| A4 | LessonLifecycleTests | `Publish_Published_ThrowsLessonAlreadyPublished` | `BusinessRuleViolationCoreException` + `LESSON_ALREADY_PUBLISHED`; after `ClearDomainEvents()` before act, events still empty |
| A5 | LessonLifecycleTests | `Unpublish_Published_ReturnsToDraftKeepsPublishedAtAndRaisesLessonUnpublished` | State Draft, `PublishedAt` equals the value captured after Publish, events = `LessonUnpublished(id, unitId)` (after clearing) |
| A6 | LessonLifecycleTests | `Unpublish_Archived_ReturnsToDraftWithoutEvent` | State Draft, events empty (after clearing) |
| A7 | LessonLifecycleTests | `Unpublish_Draft_ThrowsLessonAlreadyDraft` | exception + code |
| A8 | LessonLifecycleTests | `Archive_Published_ArchivesAndRaisesLessonArchived` | State Archived, `UpdatedBy`, events = `LessonArchived(id, unitId)` (after clearing) |
| A9 | LessonLifecycleTests | `Archive_Draft_ThrowsLessonNotPublished` | exception + `LESSON_NOT_PUBLISHED`, State still Draft |
| A10 | LessonLifecycleTests | `Archive_Archived_ThrowsLessonAlreadyArchived` | exception + code |
| A11 | LessonLifecycleTests | `MoveTo_NewOrder_SetsOrderAndUpdater` | Order, `UpdatedBy` |
| A12 | LessonLifecycleTests | `MoveTo_SameOrder_LeavesUpdaterUnchanged` | `UpdatedBy` still creator |
| A13 | LessonLifecycleTests | `MoveTo_OrderZero_ThrowsContentOrderInvalid` | exception + code |
| A14 | LessonLifecycleTests | `Delete_DraftWithObjectives_SoftDeletesLessonAndObjectives` | lesson updated with 2 objectives → `IsDeleted` true on lesson and both objectives, `UpdatedBy` = deleter |
| A15 | LessonLifecycleTests | `Delete_Archived_SoftDeletes` | `IsDeleted` true |
| A16 | LessonLifecycleTests | `Delete_Published_ThrowsLessonIsPublished` | exception + code, `IsDeleted` false |

### API — handlers and validators
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| A17 | PublishLessonHandlerTests | `Handle_DraftLesson_PublishesAndSaves` | State Published, `SaveChangesAsync` Received(1) |
| A18 | PublishLessonHandlerTests | `Handle_PublishedLesson_ThrowsLessonAlreadyPublished` | `BusinessRuleViolationCoreException` + domain code, save DidNotReceive |
| A19 | PublishLessonHandlerTests | `Handle_LessonNotFound_ThrowsLessonNotFound` | `NotFoundCoreException` + code, save DidNotReceive |
| A20 | PublishLessonHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException` + code, save DidNotReceive |
| A21 | UnpublishLessonHandlerTests | `Handle_PublishedLesson_ReturnsToDraftAndSaves` | State Draft, save Received(1) |
| A22 | UnpublishLessonHandlerTests | `Handle_DraftLesson_ThrowsLessonAlreadyDraft` | exception + code, save DidNotReceive |
| A23 | UnpublishLessonHandlerTests | `Handle_LessonNotFound_ThrowsLessonNotFound` | as A19 |
| A24 | UnpublishLessonHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | as A20 |
| A25 | ArchiveLessonHandlerTests | `Handle_PublishedLesson_ArchivesAndSaves` | State Archived, save Received(1) |
| A26 | ArchiveLessonHandlerTests | `Handle_DraftLesson_ThrowsLessonNotPublished` | exception + code, save DidNotReceive |
| A27 | ArchiveLessonHandlerTests | `Handle_LessonNotFound_ThrowsLessonNotFound` | as A19 |
| A28 | ArchiveLessonHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | as A20 |
| A29 | ReorderLessonHandlerTests | `Handle_MoveThirdToFirst_RenumbersSiblingsAndSaves` | siblings A,B,C, move C to 1 → C=1, A=2, B=3, save Received(1) |
| A30 | ReorderLessonHandlerTests | `Handle_PositionBeyondCount_MovesToLast` | move A to 99 → B=1, C=2, A=3 |
| A31 | ReorderLessonHandlerTests | `Handle_LessonNotFound_ThrowsLessonNotFound` | as A19 |
| A32 | ReorderLessonHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | as A20 |
| A33 | DeleteLessonHandlerTests | `Handle_DraftLesson_SoftDeletesAndSaves` | `IsDeleted` true, save Received(1) |
| A34 | DeleteLessonHandlerTests | `Handle_PublishedLesson_ThrowsLessonIsPublished` | exception + code, save DidNotReceive |
| A35 | DeleteLessonHandlerTests | `Handle_LessonNotFound_ThrowsLessonNotFound` | as A19 |
| A36 | DeleteLessonHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | as A20 |
| A37 | {Publish,Unpublish,Archive,Delete}LessonValidatorTests (4 classes) | `Validate_ValidCommand_Passes` · `Validate_EmptyLessonId_FailsLessonIdRequired` | valid → no errors; `Guid.Empty` → error code `LESSON_ID_REQUIRED` |
| A38 | ReorderLessonValidatorTests | `Validate_ValidCommand_Passes` · `Validate_EmptyLessonId_FailsLessonIdRequired` | as A37 |
| A39 | ReorderLessonValidatorTests | `Validate_PositionZero_FailsLessonPositionInvalid` | error code `LESSON_POSITION_INVALID` |
| A40 | GetSubjectHandlerTests | `Handle_NonAdmin_ReturnsPublishedLessonCounts` | role `Student`; `CountByUnitAsync(any, true, any)` → `{first: 1}`, `(any, false, any)` → `{first: 3}`; result unit first `LessonCount` 1 |

### API — integration (Testcontainers, through HTTP)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| I1 | LessonLifecycleEndpointTests | `PostPublish_DraftLesson_PublishesAndAudits` | 200; DB State Published and `PublishedAt` not null; audit `Lesson.Publish` Outcome Success |
| I2 | LessonLifecycleEndpointTests | `PostPublish_DraftLesson_DispatchesLessonPublished` | `factory.LessonEvents.Events` contains `new LessonPublished(lessonId, unitId)` |
| I3 | LessonLifecycleEndpointTests | `PostPublish_PublishedLesson_Returns400LessonAlreadyPublished` | 400 + code |
| I4 | LessonLifecycleEndpointTests | `PostPublish_UnknownLesson_Returns404LessonNotFound` | 404 + code |
| I5 | LessonLifecycleEndpointTests | `PostPublish_Teacher_Returns403` | 403; DB State still Draft |
| I6 | LessonLifecycleEndpointTests | `PostPublish_Anonymous_Returns401` | 401 |
| I7 | LessonLifecycleEndpointTests | `PostUnpublish_PublishedLesson_ReturnsToDraftAndDispatchesEvent` | 200; State Draft; `PublishedAt` not null; events contain `LessonUnpublished(lessonId, unitId)`; audit `Lesson.Unpublish` Success |
| I8 | LessonLifecycleEndpointTests | `PostUnpublish_DraftLesson_Returns400LessonAlreadyDraft` | 400 + code |
| I9 | LessonLifecycleEndpointTests | `PostUnpublish_Student_Returns403` | 403 |
| I10 | LessonLifecycleEndpointTests | `PostArchive_PublishedLesson_ArchivesAndDispatchesEvent` | 200; State Archived; events contain `LessonArchived(lessonId, unitId)`; audit `Lesson.Archive` Success |
| I11 | LessonLifecycleEndpointTests | `PostArchive_DraftLesson_Returns400LessonNotPublished` | 400 + code |
| I12 | LessonLifecycleEndpointTests | `PostArchive_Teacher_Returns403` | 403 |
| I13 | LessonManagementEndpointTests | `PutPosition_MoveLastToFirst_RenumbersLessonsAndAudits` | 3 lessons; move 3rd to 1 → DB orders 2,3,1; audit `Lesson.Reorder` Success |
| I14 | LessonManagementEndpointTests | `PutPosition_PositionZero_Returns422LessonPositionInvalid` | 422 + code contains `LESSON_POSITION_INVALID` |
| I15 | LessonManagementEndpointTests | `PutPosition_UnknownLesson_Returns404LessonNotFound` | 404 + code |
| I16 | LessonManagementEndpointTests | `PutPosition_Teacher_Returns403` | 403 |
| I17 | LessonManagementEndpointTests | `Delete_DraftLesson_SoftDeletesWithObjectivesAndAudits` | lesson seeded with 2 objectives (`ContentTestData.SeedLessonAsync`); 200; `ReadLessonAsync` → lesson and objectives `IsDeleted`; `GET /api/lessons?unitId=` no longer lists it; audit `Lesson.Delete` Success |
| I18 | LessonManagementEndpointTests | `Delete_PublishedLesson_Returns400LessonIsPublished` | 400 + code; DB `IsDeleted` false |
| I19 | LessonManagementEndpointTests | `Delete_UnknownLesson_Returns404LessonNotFound` | 404 + code |
| I20 | LessonManagementEndpointTests | `Delete_Teacher_Returns403` | 403; `IsDeleted` false |
| I21 | SubjectsEndpointTests | `GetById_Student_CountsPublishedLessonsOnly` | unit with Draft + Published + Archived lessons (`SeedLessonInStateAsync`) → student `lessonCount` 1 |
| I22 | SubjectsEndpointTests | `GetById_Admin_CountsLessonsInEveryState` | same seed → admin `lessonCount` 3 |

### Web
| # | Test file | `it(...)` | Asserts |
|---|-----------|-----------|---------|
| W1 | lessonLifecycle.test.ts | returns publish and delete for a draft | `['publish', 'delete']` |
| W2 | lessonLifecycle.test.ts | returns unpublish and archive for a published lesson | `['unpublish', 'archive']` |
| W3 | lessonLifecycle.test.ts | returns publish, unpublish and delete for an archived lesson | exact array |
| W4 | lessonLifecycle.test.ts | returns no actions for an unknown state | `[]` |
| W5 | contentQueries.test.ts | matches subject and lesson query keys | `['/api/subjects']`, `['/api/subjects/s1']`, `['/api/lessons', { unitId: 'u1' }]`, `['/api/lessons/l1']` → true |
| W6 | contentQueries.test.ts | ignores other query keys | `['/api/audit-logs']`, `[42]` → false |
| W7 | contentQueries.test.ts | excludes only the deleted lesson detail | `(['/api/lessons/l1'], 'l1')` false; `(['/api/lessons/l2'], 'l1')` true; `(['/api/lessons'], 'l1')` true |
| W8 | LessonItem.test.tsx | publishes a draft lesson after confirmation | in the Newton's laws group, click "Publish"; text "Publish Newton's laws? Students will see it." appears; click "Yes, publish"; toast "Lesson published."; calls `['l1']` |
| W9 | LessonItem.test.tsx | does not call the API when the confirmation is cancelled | Momentum "Archive" → "Cancel" → "Archive" button back; archive calls `[]` |
| W10 | LessonItem.test.tsx | offers only the actions each state allows | l1 group: Publish, Delete, no Archive; l2: Move to draft, Archive, no Delete, no Publish; l3: Publish, Move to draft, Delete |
| W11 | LessonItem.test.tsx | archives a published lesson after confirmation | "Yes, archive" → toast "Lesson archived.", calls `['l2']` |
| W12 | LessonItem.test.tsx | moves an archived lesson to draft after confirmation | l3 "Move to draft" → "Yes, move to draft" → toast "Lesson moved to draft.", calls `['l3']` |
| W13 | LessonItem.test.tsx | moves a lesson down | "Move Newton's laws up" disabled; click "Move Newton's laws down" → body `[{ position: 2 }]`, toast "Lesson moved." |
| W14 | LessonItem.test.tsx | deletes a draft lesson after confirmation | l1 "Delete" → "Delete Newton's laws?" → "Yes, delete" → toast "Lesson deleted.", delete calls `['l1']` |
| W15 | LessonItem.test.tsx | shows the server error when a transition is rejected | publish handler → `HttpResponse.json({ code: 'LESSON_ALREADY_PUBLISHED' }, { status: 400 })`; toast "This lesson is already published." |
| W16 | LessonItem.test.tsx | shows the confirmation in Arabic | `lng: 'ar'`; `document.documentElement` has `dir="rtl"`; expand "عرض الوحدات"/"عرض الدروس"; click "نشر" in the l1 group; text "نشر Newton's laws؟ سيظهر للطلاب." |
| W17 | LessonItem.test.tsx | has no axe violations while confirming | a confirmation is open → `(await axe(container)).violations` equals `[]` |
| W18 | LessonActions.test.tsx | publishes from the editor and shows the new state | stateful `getGetLessonMockHandler(() => ({ ...lesson, state }))` where the publish handler sets `state = 'Published'`; after confirm, "Published" badge visible and the group offers "Move to draft" and "Archive" |
| W19 | LessonActions.test.tsx | returns to the content tree after deleting | `getGetSubjectsMockHandler([])` also registered; "Delete" → "Yes, delete" → toast "Lesson deleted." and `router.state.location.pathname` is `/admin/content` |
| W20 | LessonActions.test.tsx | does not offer delete for a published lesson | lesson `state: 'Published'` → group has no "Delete" button |
| W21 | LessonActions.test.tsx | stays on the editor when delete is rejected | delete handler 400 `LESSON_IS_PUBLISHED` → toast "Move this lesson to draft or archive it before deleting it."; pathname still `/admin/lesson/l1` |

No existing test is edited except `GetSubjectHandlerTests` (listed in Existing code touched). `UnitLessons.test.tsx` and `LessonEditorPage.test.tsx` must pass unchanged.

## Definition of done
- [ ] `Lesson` is `partial`; `Lesson.Lifecycle.cs` holds Publish/Unpublish/Archive exactly as in Domain behaviour; `Lesson.cs` has `PublishedAt`, `MoveTo`, `Delete`; every mutating method sets `UpdatedBy` and `UpdationDate`.
- [ ] Transition table D2 is enforced in the domain with the 5 domain codes; Draft→Archived and deleting a Published lesson are refused.
- [ ] `LessonPublished`, `LessonUnpublished` and `LessonArchived` are `sealed record … : DomainEvent`, raised only on entering or leaving Published (Archived→Draft raises none).
- [ ] 5 commands implement `IAuditableCommand` with the actions in the Audit table; `docs/audit-log.md` lists them.
- [ ] Publish, unpublish and archive use `[Authorize(Policy = DefaultCodes.LessonsPublish)]`; reorder and delete use `ContentManage`; no new policy constants.
- [ ] `CountByUnitAsync` takes `publishedOnly`; `GetSubjectHandler` passes `role != Admin`; I21 and I22 are green.
- [ ] Migration `AddLessonPublishedAt` contains only the nullable `PublishedAt` column (no Drop, Rename or AlterColumn in `Up`).
- [ ] 6 new codes are present in both resx files and both web `shared/i18n` files with the texts above.
- [ ] `LessonPositionRequest` is appended to `Controllers/Lessons/Requests.cs`; `openapi/v1.json` and `web/src/shared/api/generated/**` are regenerated, not hand-edited.
- [ ] Postman "Lessons" folder has the 5 new requests and the updated description.
- [ ] PRD §5.2 Transitions paragraph and `claude-design-prompt.md` §4 bullet are updated verbatim.
- [ ] Every test row A1–A40, I1–I22, W1–W21 exists with that name and passes; no other existing test is edited, skipped or deleted.
- [ ] `dotnet build` has zero new warnings; `dotnet test` is green **with `api/Elmanhg.Api/appsettings.json` absent**; `dotnet format --verify-no-changes` exits 0.
- [ ] No new configuration keys or options classes; `appsettings.example.json` is unchanged.
- [ ] Guard grep is clean (`DateTime.Now/UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`, `async void`).
- [ ] Web: `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check .`, `npx vitest run --coverage` all exit 0; `npm run gen:api` leaves no diff.
- [ ] Web: no physical-direction utilities or literal colours in `src/features/content`; every new string is in `en` and `ar`; the only confirmation is the inline pattern (no new dialog).
- [ ] Lesson rows in the tree show move up/down plus only the state-allowed actions; the editor header shows the same lifecycle and delete actions; deleting in the editor navigates to `/admin/content`.
