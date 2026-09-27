# Plan — Audit log for content and validation actions (#58, E1.S5)

## Goal
Every command that opts in writes one append-only audit row. The row holds actor (user id, user name, role), action, entity type, entity id, outcome (success or failure plus error code), a field-level diff of the audited entities the command committed, a timestamp and a trace id. PostgreSQL refuses UPDATE, DELETE and TRUNCATE on the table. An admin opens `#/admin/audit` and pages through the log newest first. They can filter by actor, entity type and date range, and can expand any row to see the diff. Later content and validation stories (E2/E3) get auditing by adding two markers and nothing else: `IAuditableCommand` on the command and `IAuditedEntity` on the entity. Teacher-subject assign and unassign prove the mechanism now.

## Scope
**In:**
- Reuse of Morabh `Core.Auditing` (`AuditBehaviour`, `IAuditableCommand`, `IAuditableResult`, `AuditLog`, `CoreAuditing:Enabled`), already vendored in `api/core-libraries/Core.Auditing`. Auditing is switched on.
- New in Core: the `IAuditedEntity` marker. `CoreDbContext` records a change-tracker diff of marked entities after each successful save, into a scoped `IAuditChangeCollector`. `AuditBehaviour` writes that diff into the new `AuditLog.Diff` jsonb column.
- Append-only: `IAuditLogRepository` shrinks to append plus read. `AppendAsync` is a parameterised INSERT that never touches the change tracker. A migration adds `Diff`, two indexes and a trigger that rejects UPDATE, DELETE and TRUNCATE.
- `TeacherSubject` is marked `IAuditedEntity`. The two teacher commands move to Morabh action naming.
- `GET /api/audit-logs` (paged, filtered) and `GET /api/audit-logs/resource-types`, both under the `AuditLog.View` policy (Admin).
- Web: the `features/audit` page replaces the placeholder at `/admin/audit`. Filters are in URL search params; the page has pagination, a diff expander, and loading, empty, no-results and error states.
- `docs/audit-log.md` (from Morabh `AuditLogs.md`), a `docs/PRD.md` §15 field-list update, and Postman requests.

**Out:**
- Auditing content and validation commands. They do not exist yet; E2/E3 stories add the markers (see `docs/audit-log.md`).
- Auditing auth commands, reads and uploads. Morabh excludes these deliberately (`AuditLogs.md` "Not audited").
- Diffs of owned types or navigation collections. Only scalar properties are diffed (D8).
- Export of audit entries (backlog "Export audit entry" is a separate story).
- Resolving actor display names. The row stores the actor snapshot taken at the time (D10).

**Deferred:** None. Everything builds and tests offline.

## Morabh reuse map
| Piece | Morabh source | Elmanhg target |
|---|---|---|
| Opt-in marker, result id | `repos/apis/Core/Core.Auditing/IAuditableCommand.cs`, `IAuditableResult.cs` | `api/core-libraries/Core.Auditing/` (already vendored, unchanged) |
| Pipeline writer | `Core/Core.Auditing/AuditBehaviour.cs` | vendored; modified (diff + append) |
| Entity, toggle, outcome | `Core/Core.Auditing/Entities/AuditLog.cs`, `AuditOptions.cs`, `AuditOutcome.cs` | vendored; `AuditLog` gains `Diff` |
| Repository | `Core/Core.EntityFrameworkCore/Repositories/AuditLogRepository.cs` | vendored; narrowed to append plus read |
| Naming `Resource.Verb`, "Not audited" list, how-to doc | `repos/apis/AuditLogs.md` | `docs/audit-log.md` |
| Change-tracker diff, collector, marker, DB trigger, query, controller, web page | new, no Morabh equivalent (Morabh has "No field-level value diffs by design") | listed below |

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Sub-task says "domain event handler writes an audit row" | Keep Morabh's `AuditBehaviour` (MediatR, outermost) as the only writer. No domain event and no event handler. | Reuse-first. `CoreDbContext` publishes domain events **before** `base.SaveChangesAsync`, so an event handler would log changes that might then fail to commit. It would also miss validation and authorisation failures and double-write alongside the behaviour. The sub-task's intent is automatic rows for every content change and decision with no handler edits, and the behaviour meets it. |
| D2 | PRD asks for `diff_json`, but Morabh has no diffs | Add `AuditLog.Diff` (`string?`, `jsonb`). `CoreDbContext.SaveChangesAsync` reads Added/Modified/Deleted entries whose entity implements `IAuditedEntity`, **after** domain events and before `base.SaveChangesAsync`. It hands them to the scoped `IAuditChangeCollector` only if the save succeeds. `AuditBehaviour` serialises the changes recorded during its own `next()` (count snapshot before, `Skip(count)` after). | Automatic for E2/E3: they mark entities and commands and nothing else. Only committed changes show up. The count snapshot keeps nested or sequential commands in one scope apart. |
| D3 | Where does `IAuditedEntity` live? | `Core.DDD/Entities/IAuditedEntity.cs`, as an empty marker. | `Elmanhg.Domain` references only `Core.DDD`. Marking is opt-in, so PII entities (`User`: password hash, phone) are never diffed. |
| D4 | Append-only enforcement | (a) `IAuditLogRepository` no longer extends `IRepository<AuditLog>`. It exposes only `AppendAsync`, `FindPaginatedAsync` and `GetResourceTypesAsync`. (b) `AppendAsync` is one parameterised `ExecuteSqlAsync` INSERT. (c) The migration adds a `BEFORE UPDATE OR DELETE` row trigger and a `BEFORE TRUNCATE` statement trigger that `RAISE EXCEPTION`. | No code path can update or delete rows. The DB blocks it too. The INSERT bypasses the change tracker, so a failed command's uncommitted leftovers are never flushed by the audit write (a latent Morabh bug with `AddAsync` + `SaveChangesAsync`). |
| D5 | Token for the audit write | `CancellationToken.None`, with a WHY comment. | The request token may be cancelled after the handler committed. Losing the row would break PRD §17 rule 13. |
| D6 | Failed commands | Keep Morabh behaviour: a `Failure` row with `ErrorCode`. The diff is normally null. | Disputes need rejected attempts too. Morabh already does this. |
| D7 | Is auditing on? | `CoreAuditing:Enabled = true` in `appsettings.json`, `appsettings.example.json` and `ApiFactory`. The toggle stays. | PRD §17 rule 13 is mandatory. Mirror Morabh's switch. |
| D8 | Which properties are diffed | Scalar properties only. Excluded: `Id`, `DeletedAt`, `CreatedBy`, `CreationDate`, `UpdatedBy`, `UpdationDate`, shadow properties and concurrency tokens. `IsDeleted` is excluded for Created and hard Deleted. Modified keeps only properties where `IsModified && !Equals(original, current)`. An entry with no remaining properties is dropped. | The row already carries actor and time. A soft delete shows as `isDeleted: false → true`. E2 jsonb columns are scalar properties, so they are included. |
| D9 | Change kind | `Created` (Added), `Deleted` (Deleted state, or Modified with `IsDeleted` going false→true), else `Modified`. | Unassign is a soft delete. It should read as a delete. |
| D10 | Actor shown and filtered | Show `ActorUserName` (the JWT `ClaimTypes.Name`, which is the email or phone) and `ActorRole`. The `actor` filter is a case-insensitive substring match on `ActorUserName`. | An append-only snapshot of who acted. No join, no `UserManager` in the query. The admin UI hint says "Email or mobile number". |
| D11 | Entity-type filter source | A distinct `ResourceType` list from `GET /api/audit-logs/resource-types`, shown in a select. | Matches the prototype's select of distinct entities (`prototype/app.js:897`). It never goes stale as E2/E3 add types. |
| D12 | Date-range semantics | API: `from` inclusive, `to` exclusive, both `DateTimeOffset?`, converted with `.ToUniversalTime()` before comparing. Web: date inputs; `from` → local midnight ISO, `to` → **next** local midnight ISO (inclusive day for the user). | Npgsql rejects non-UTC `DateTimeOffset` for `timestamptz` (a 500). Admins think in whole local days. |
| D13 | Validation of `to` vs `from` | API: `From < To` when both are set, else 422 `AUDIT_LOG_DATE_RANGE_INVALID`. Web: `to >= from` (the same day is allowed) as an inline error on `to`. | Because the web `to` is exclusive next-midnight, the same day is still a valid API range. |
| D14 | Paging caps | `AuditLogsOptions { MaxPageSize = 100, FilterMaxLength = 256 }` bound from `AuditLogs`, with `ValidateOnStart`. Query default page size 20, web fixed page size 20. | Skill §8.1: caps come from options. |
| D15 | Sort | `Timestamp` desc, then `Id` desc. | Newest first like the prototype, with a deterministic tie-break. |
| D16 | Action naming | Change the teacher commands to `Teacher.AssignSubject` / `Teacher.UnassignSubject`. `ResourceType` stays `Teacher`, id `TeacherId`. | Morabh `Resource.Verb` convention (`AuditLogs.md`). No test asserts the old strings. |
| D17 | `Diff` in the API result | A raw JSON string (`string?`). The web pretty-prints it. | Avoids an untyped `JsonElement` schema in OpenAPI/Orval. Shape documented in `docs/audit-log.md`. |
| D18 | Current-user guard in the query handlers | None. | Read-only, never uses the caller id. The `AuditLog.View` policy is the gate (the same as other policy-gated reads). |
| D19 | Web data loading | `useQuery` (generated `useGetAuditLogs`) with `placeholderData: keepPreviousData`, and in-page skeleton, error and empty states. No route loader or suspense. | Filter and page changes keep the previous page visible instead of suspending to a skeleton. |
| D20 | Web `number \| string` paging fields | Normalise in `select` with `Number(...)`. | .NET 10 OpenAPI emits `long` as `integer\|string` (see `generateOTPResult.ts`). |
| D21 | Web table on mobile | Horizontal scroll inside the Card. | `.claude/design-system.md` Table row wins on look over the skill §17 card-list rule. |
| D22 | Action, entity type and ids in the UI | Raw identifiers, `font-mono text-mono`, `dir="ltr"`. | PRD §14: English allowed for admin technical fields. Design system: ids are mono and ltr. |

## Existing code touched
| File | Change |
|------|--------|
| `api/core-libraries/Core.Auditing/Entities/AuditLog.cs` | Add `public string? Diff { get; private set; }`. `Create(...)` gains a last parameter `string? diff` assigned to `Diff`. |
| `api/core-libraries/Core.Auditing/Repositories/IAuditLogRepository.cs` | Replace with the contract in F9 (no longer `: IRepository<AuditLog>`). |
| `api/core-libraries/Core.Auditing/AuditBehaviour.cs` | Full contract in "Domain behaviour § AuditBehaviour". Primary ctor becomes `(IAuditLogRepository auditLogRepository, IAuditChangeCollector auditChangeCollector, ICurrentUserService currentUserService, ILogger<AuditBehaviour<TRequest, TResponse>> logger)`. `ConfigureAwait(false)` on every await. Rename `ex` → `exception`. Remove the unused `using Microsoft.Extensions.Configuration;`. |
| `api/core-libraries/Core.Auditing/DependencyInjection.cs` | Before reading options: `services.AddScoped<IAuditChangeCollector, AuditChangeCollector>();` (unconditional, because `CoreDbContext` needs it even when disabled). |
| `api/core-libraries/Core.EntityFrameworkCore/Context/CoreDbContext.cs` | Primary ctor `(DbContextOptions options, IMediator mediator, IAuditChangeCollector auditChangeCollector)`. In `OnModelCreating`, before `ApplyGlobalFilter...`: `modelBuilder.Entity<AuditLog>(builder => { builder.Property(x => x.Diff).HasColumnType("jsonb"); builder.HasIndex(x => x.Timestamp); builder.HasIndex(x => new { x.ResourceType, x.Timestamp }); });`. `SaveChangesAsync`: after the existing domain-event loop, `var auditedChanges = AuditChangeReader.Read(ChangeTracker);` then `var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false); auditChangeCollector.Record(auditedChanges); return result;`. Nothing else changes. |
| `api/core-libraries/Core.EntityFrameworkCore/Repositories/AuditLogRepository.cs` | Keep `: Repository<AuditLog>(context), IAuditLogRepository` (the base supplies `FindPaginatedAsync`). Add `AppendAsync` and `GetResourceTypesAsync` (F9). |
| `api/Elmanhg.Domain/Teachers/TeacherSubject.cs` | `public class TeacherSubject : AuditEntity, IAuditedEntity`. |
| `api/Elmanhg.Application/Teachers/AssignTeacherSubject/AssignTeacherSubjectCommand.cs` | `AuditAction => "Teacher.AssignSubject"`. |
| `api/Elmanhg.Application/Teachers/UnassignTeacherSubject/UnassignTeacherSubjectCommand.cs` | `AuditAction => "Teacher.UnassignSubject"`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | New group `// AUDIT LOGS` with the 4 constants in "Error codes". |
| `api/Elmanhg.Application/DependencyInjection.cs` | `services.AddOptions<AuditLogsOptions>().BindConfiguration(AuditLogsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Primary ctor `(DbContextOptions options, IMediator mediator, IAuditChangeCollector auditChangeCollector) : CoreDbContext<User, Role, Guid>(options, mediator, auditChangeCollector)`, plus `using Core.Auditing;`. Nothing else. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef`. |
| `api/Elmanhg.Api/appsettings.json`, `appsettings.example.json` | `"CoreAuditing": { "Enabled": true }`. Add `"AuditLogs": { "MaxPageSize": 100, "FilterMaxLength": 256 }`. |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | 4 keys (see "Error codes"). |
| `api/openapi/v1.json` | Regenerated by `dotnet build api/Elmanhg.Api`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `["CoreAuditing:Enabled"] = "true"`. Add `["AuditLogs:MaxPageSize"] = "100"` and `["AuditLogs:FilterMaxLength"] = "256"`. |
| `api/Elmanhg.Tests/Integration/Composition/PipelineCompositionTests.cs` | Add test I20 (no existing test edited). |
| `postman/elmanhg.postman_collection.json` | New folder `AuditLogs` with `Get audit logs` (`GET {{baseUrl}}/api/audit-logs?pageNumber=1&pageSize=20`, disabled query params `actor`, `resourceType`, `from`, `to`) and `Get audit log resource types` (`GET {{baseUrl}}/api/audit-logs/resource-types`). Both inherit collection bearer auth like `Teachers`. |
| `docs/PRD.md` §15 | Replace the `AuditLog(...)` line with `AuditLog(id, actor_id, actor_name, actor_role, action, entity, entity_id, outcome, error_code, diff_json, trace_id, created_at)  -- append-only`. |
| `web/src/routes/admin/audit.tsx` | `createFileRoute('/admin/audit')({ validateSearch: auditLogSearchSchema, component: AuditLogPage })`, importing both from `@/features/audit`. |
| `web/src/app/i18n.ts` | Import `auditLocales` from `@/features/audit`. Add `audit: auditLocales.ar/en` to `resources`, and `'audit'` to `ns`. |
| `web/src/shared/i18n/en.json`, `ar.json` | `errors.AUDIT_LOG_PAGE_NUMBER_INVALID`, `errors.AUDIT_LOG_PAGE_SIZE_INVALID`, `errors.AUDIT_LOG_FILTER_TOO_LONG`, `errors.AUDIT_LOG_DATE_RANGE_INVALID`, using the same strings as the resx. |
| `web/src/shared/form/TextField.tsx` | The `type` union gains `'date'`. |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api` (new tag folder for `AuditLogs`, expected `audit-logs/`, plus models). Never hand-edited. |

## Files to create

### Core (vendored libraries)
| # | Path | Type | Contract |
|---|------|------|----------|
| F1 | `api/core-libraries/Core.DDD/Entities/IAuditedEntity.cs` | interface | `namespace Core.DDD.Entities; public interface IAuditedEntity { }` |
| F2 | `api/core-libraries/Core.Auditing/AuditChangeKind.cs` | enum | `namespace Core.Auditing; public enum AuditChangeKind { Created, Modified, Deleted }` |
| F3 | `api/core-libraries/Core.Auditing/AuditValueChange.cs` | record | `public sealed record AuditValueChange(JsonNode? Before, JsonNode? After);` (`System.Text.Json.Nodes`) |
| F4 | `api/core-libraries/Core.Auditing/AuditEntityChange.cs` | record | `public sealed record AuditEntityChange(string EntityType, Guid EntityId, AuditChangeKind Change, IReadOnlyDictionary<string, AuditValueChange> Properties);` Keys are CLR property names. |
| F5 | `api/core-libraries/Core.Auditing/IAuditChangeCollector.cs` | interface | `IReadOnlyList<AuditEntityChange> Changes { get; }` · `void Record(IReadOnlyList<AuditEntityChange> changes);` |
| F6 | `api/core-libraries/Core.Auditing/AuditChangeCollector.cs` | sealed class | `private readonly List<AuditEntityChange> _changes = [];` · `Changes => _changes` · `Record` → `_changes.AddRange(changes)`. Scoped. |
| F7 | `api/core-libraries/Core.Auditing/AuditDiff.cs` | static class | `public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { DictionaryKeyPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };` · `public static string? Serialize(IReadOnlyList<AuditEntityChange> changes)` → `changes.Count == 0 ? null : JsonSerializer.Serialize(changes, SerializerOptions)`. |
| F8 | `api/core-libraries/Core.EntityFrameworkCore/Auditing/AuditChangeReader.cs` | static class | `namespace Core.EntityFrameworkCore.Auditing;` · `public static List<AuditEntityChange> Read(ChangeTracker changeTracker)`. The body is in "Domain behaviour § AuditChangeReader". |
| F9 | (contract for the modified `IAuditLogRepository` + `AuditLogRepository`) | — | Interface: `Task AppendAsync(AuditLog entry, CancellationToken cancellationToken);` · `Task<PageData<AuditLog>> FindPaginatedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken, Expression<Func<AuditLog, bool>>? filter = null, Func<IQueryable<AuditLog>, IQueryable<AuditLog>>? include = null, Func<IQueryable<AuditLog>, IOrderedQueryable<AuditLog>>? orderBy = null, bool asNoTracking = false);` (identical to `IRepository<T>`, so the `Repository<AuditLog>` base satisfies it) · `Task<List<string>> GetResourceTypesAsync(CancellationToken cancellationToken);`. Implementation: `AppendAsync` → `await _context.Database.ExecuteSqlAsync($"""INSERT INTO "AuditLogs" ("Id", "Timestamp", "ActorUserId", "ActorUserName", "ActorRole", "Action", "ResourceType", "ResourceId", "Outcome", "ErrorCode", "TraceId", "Diff", "IsDeleted", "DeletedAt") VALUES ({entry.Id}, {entry.Timestamp}, {entry.ActorUserId}, {entry.ActorUserName}, {entry.ActorRole}, {entry.Action}, {entry.ResourceType}, {entry.ResourceId}, {entry.Outcome}, {entry.ErrorCode}, {entry.TraceId}, CAST({entry.Diff} AS jsonb), FALSE, NULL)""", cancellationToken).ConfigureAwait(false);` (interpolated, so parameterised; never `ExecuteSqlRaw`) · `GetResourceTypesAsync` → `_dbSet.AsNoTracking().Select(x => x.ResourceType).Distinct().OrderBy(x => x).ToListAsync(cancellationToken).ConfigureAwait(false)`. |

### Application
| # | Path | Type | Contract |
|---|------|------|----------|
| F10 | `api/Elmanhg.Application/Shared/Options/AuditLogsOptions.cs` | sealed class | `namespace Elmanhg.Application.Shared.Options;` · `public const string SectionName = "AuditLogs";` · `[Range(1, int.MaxValue)] public int MaxPageSize { get; set; }` · `[Range(1, int.MaxValue)] public int FilterMaxLength { get; set; }` |
| F11 | `api/Elmanhg.Application/AuditLogs/GetAuditLogs/GetAuditLogsQuery.cs` | sealed record | `namespace Elmanhg.Application.AuditLogs.GetAuditLogs;` · `public sealed record GetAuditLogsQuery(string? Actor, string? ResourceType, DateTimeOffset? From, DateTimeOffset? To, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<AuditLogResult>>;` (not `IAuditableCommand`) |
| F12 | `api/Elmanhg.Application/AuditLogs/GetAuditLogs/GetAuditLogsValidator.cs` | sealed class | `GetAuditLogsValidator(IOptions<AuditLogsOptions> auditLogsOptions) : AbstractValidator<GetAuditLogsQuery>`. Rules (`var options = auditLogsOptions.Value;`): `RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.AuditLogPageNumberInvalid);` · `RuleFor(x => x.PageSize).ValidateRange(1, options.MaxPageSize, ErrorCodes.AuditLogPageSizeInvalid);` · `RuleFor(x => x.Actor).ValidateMaxLength(options.FilterMaxLength, ErrorCodes.AuditLogFilterTooLong);` · `RuleFor(x => x.ResourceType).ValidateMaxLength(options.FilterMaxLength, ErrorCodes.AuditLogFilterTooLong);` · `RuleFor(x => x).Must(x => x.From is null \|\| x.To is null \|\| x.From < x.To).WithErrorCode(ErrorCodes.AuditLogDateRangeInvalid);` (no Core extension exists for a cross-field rule). |
| F13 | `api/Elmanhg.Application/AuditLogs/GetAuditLogs/GetAuditLogsFilter.cs` | static class | `public static Expression<Func<AuditLog, bool>> Build(GetAuditLogsQuery query)`: `var actor = string.IsNullOrWhiteSpace(query.Actor) ? null : query.Actor.Trim().ToLowerInvariant(); var resourceType = string.IsNullOrWhiteSpace(query.ResourceType) ? null : query.ResourceType.Trim(); var from = query.From?.ToUniversalTime(); var to = query.To?.ToUniversalTime(); return x => (actor == null \|\| (x.ActorUserName != null && x.ActorUserName.ToLower().Contains(actor))) && (resourceType == null \|\| x.ResourceType == resourceType) && (from == null \|\| x.Timestamp >= from) && (to == null \|\| x.Timestamp < to);` |
| F14 | `api/Elmanhg.Application/AuditLogs/GetAuditLogs/GetAuditLogsHandler.cs` | sealed class | `GetAuditLogsHandler(IAuditLogRepository auditLogRepository) : IRequestHandler<GetAuditLogsQuery, PageData<AuditLogResult>>`. `Handle`: (1) `var page = await auditLogRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: GetAuditLogsFilter.Build(request), orderBy: query => query.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);` (2) `return new PageData<AuditLogResult> { Items = page.Items.Select(AuditLogResultGenerator.Generate).ToList(), PageNumber = page.PageNumber, PageSize = page.PageSize, TotalItems = page.TotalItems, TotalPages = page.TotalPages };` |
| F15 | `api/Elmanhg.Application/AuditLogs/GetAuditLogResourceTypes/GetAuditLogResourceTypesQuery.cs` | sealed record | `public sealed record GetAuditLogResourceTypesQuery : IRequest<List<string>>;` |
| F16 | `api/Elmanhg.Application/AuditLogs/GetAuditLogResourceTypes/GetAuditLogResourceTypesHandler.cs` | sealed class | `GetAuditLogResourceTypesHandler(IAuditLogRepository auditLogRepository) : IRequestHandler<GetAuditLogResourceTypesQuery, List<string>>`. `Handle` → `return await auditLogRepository.GetResourceTypesAsync(cancellationToken).ConfigureAwait(false);` |
| F17 | `api/Elmanhg.Application/AuditLogs/Shared/AuditLogResult.cs` | sealed record, **admin-facing** (no localisation) | `public sealed record AuditLogResult(Guid Id, DateTimeOffset Timestamp, Guid? ActorUserId, string? ActorUserName, string? ActorRole, string Action, string ResourceType, Guid? ResourceId, string Outcome, string? ErrorCode, string? TraceId, string? Diff);` |
| F18 | `api/Elmanhg.Application/AuditLogs/Shared/AuditLogResultGenerator.cs` | static class | `public static AuditLogResult Generate(AuditLog auditLog)` maps every field one to one. |

### Infrastructure
| # | Path | Type | Contract |
|---|------|------|----------|
| F19 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddAuditLogDiffAndAppendOnly.cs` (+ generated `.Designer.cs`) | migration | Generate with `dotnet ef migrations add AddAuditLogDiffAndAppendOnly -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Expected generated ops: `AddColumn<string>("Diff", "AuditLogs", type: "jsonb", nullable: true)`, `CreateIndex IX_AuditLogs_Timestamp`, `CreateIndex IX_AuditLogs_ResourceType_Timestamp`. At the **end** of `Up` append `migrationBuilder.Sql(...)` with: `CREATE FUNCTION audit_logs_reject_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'AuditLogs is append-only'; END; $$;` · `CREATE TRIGGER audit_logs_append_only BEFORE UPDATE OR DELETE ON "AuditLogs" FOR EACH ROW EXECUTE FUNCTION audit_logs_reject_mutation();` · `CREATE TRIGGER audit_logs_no_truncate BEFORE TRUNCATE ON "AuditLogs" FOR EACH STATEMENT EXECUTE FUNCTION audit_logs_reject_mutation();`. At the **start** of `Down`: `DROP TRIGGER audit_logs_no_truncate ON "AuditLogs"; DROP TRIGGER audit_logs_append_only ON "AuditLogs"; DROP FUNCTION audit_logs_reject_mutation();`. No other Drop or Rename in `Up`. |

### API
| # | Path | Type | Contract |
|---|------|------|----------|
| F20 | `api/Elmanhg.Api/Controllers/AuditLogs/AuditLogsController.cs` | controller | See "API surface". `namespace Elmanhg.Api.Controllers.AuditLogs;` · `[ApiController] [Route("api/audit-logs")] [Authorize] public class AuditLogsController(IMediator mediator) : ControllerBase`. No `Requests.cs`, because query params bind directly. |

### Docs
| # | Path | Contract |
|---|------|----------|
| F21 | `docs/audit-log.md` | Adapted from Morabh `AuditLogs.md`. Sections: How it works (behaviour outermost, success and failure rows, diff from `IAuditedEntity` via `CoreDbContext`, append-only repo + DB trigger, `CoreAuditing:Enabled`). Record fields (the PRD §15 names ↔ columns). Diff JSON shape (the example below). Not audited (reads, auth, refresh, uploads). **Add a new audited command**: the Morabh update/create examples renamed to Elmanhg, plus "mark the entity `IAuditedEntity`". Audited commands table: `AssignTeacherSubject` → `Teacher.AssignSubject` / `Teacher` / command; `UnassignTeacherSubject` → `Teacher.UnassignSubject` / `Teacher` / command. Known limits: scalar properties only, owned types not diffed. |

Diff shape (document verbatim):
```json
[{"entityType":"TeacherSubject","entityId":"<guid>","change":"Created","properties":{"teacherId":{"before":null,"after":"<guid>"},"subjectId":{"before":null,"after":"<guid>"}}}]
```

### Tests (api)
| # | Path | Contract |
|---|------|----------|
| F22 | `api/Elmanhg.Tests/Builders/AuditLogBuilder.cs` | `public sealed class AuditLogBuilder` with fluent `WithActorUserName(string?)`, `WithResourceType(string)`, `WithTimestamp(DateTimeOffset)`, `WithDiff(string?)`, and `Build()` → `AuditLog.Create(...)`. Defaults: timestamp `2026-01-01T00:00:00Z`, actor `admin@elmanhg.test`, role `Admin`, action `Teacher.AssignSubject`, resource `Teacher`, a new resource id, outcome `Success`, trace `trace-1`. |
| F23 | `api/Elmanhg.Tests/Core/Auditing/AuditBehaviourTests.cs` | T1–T8 |
| F24 | `api/Elmanhg.Tests/Core/Auditing/AuditDiffTests.cs` | T9–T10 |
| F25 | `api/Elmanhg.Tests/Application/Features/AuditLogs/GetAuditLogs/GetAuditLogsFilterTests.cs` | T11–T18 |
| F26 | `api/Elmanhg.Tests/Application/Features/AuditLogs/GetAuditLogs/GetAuditLogsValidatorTests.cs` | T19–T26 |
| F27 | `api/Elmanhg.Tests/Application/Features/AuditLogs/GetAuditLogs/GetAuditLogsHandlerTests.cs` | T27 |
| F28 | `api/Elmanhg.Tests/Integration/Persistence/AuditChangeCaptureTests.cs` | I1–I4 |
| F29 | `api/Elmanhg.Tests/Integration/Persistence/AuditLogAppendOnlyTests.cs` | I5–I7 |
| F30 | `api/Elmanhg.Tests/Integration/AuditLogs/AuditLogsEndpointTests.cs` | I8–I19 |

### Web (`web/src/features/audit/`)
| # | Path | Contract |
|---|------|----------|
| W1 | `index.ts` | `export { AuditLogPage } from './pages/AuditLogPage'; export { auditLogSearchSchema, type AuditLogSearch } from './schemas/auditLogSearchSchema'; export const auditLocales = { ar, en };` |
| W2 | `i18n/en.json`, W3 `i18n/ar.json` | Keys in "Web strings". |
| W4 | `schemas/auditLogSearchSchema.ts` | `z.object({ page: z.coerce.number().int().min(1).optional().catch(undefined), actor: z.string().trim().min(1).max(auditLogFilterMaxLength).optional().catch(undefined), resourceType: <same as actor>, from: z.iso.date().optional().catch(undefined), to: z.iso.date().optional().catch(undefined) })`. Every key is optional so plain `/admin/audit` links still typecheck. `export const auditLogFilterMaxLength = 256;` with the comment `// mirrors AuditLogs:FilterMaxLength`. `export type AuditLogSearch = z.infer<...>`. |
| W5 | `schemas/auditLogFiltersSchema.ts` | `z.object({ actor: z.string().trim().max(auditLogFilterMaxLength, { error: 'audit:filters.errors.actorTooLong' }), resourceType: z.string(), from: z.union([z.literal(''), z.iso.date({ error: 'audit:filters.errors.date' })]), to: <same> }).refine((values) => values.from === '' \|\| values.to === '' \|\| values.to >= values.from, { path: ['to'], error: 'audit:filters.errors.dateRange' })`. `export type AuditLogFiltersValues`. |
| W6 | `api/auditLogParams.ts` | `export const auditLogPageSize = 20;` · `toAuditLogParams(search: AuditLogSearch): GetAuditLogsParams` → `pageNumber: search.page ?? 1`, `pageSize: auditLogPageSize`, and `actor` / `resourceType` only when set. `from` → `new Date(year, month - 1, day).toISOString()` (local midnight); `to` → local midnight of the **next** day `.toISOString()`. Undefined keys are omitted with conditional spreads (`exactOptionalPropertyTypes`). · `hasActiveFilters(search): boolean` (any of actor/resourceType/from/to set). · `export interface AuditLogPage { items: AuditLogResult[]; pageNumber: number; totalPages: number; totalItems: number }` and `toAuditLogPage(data: <generated page type>): AuditLogPage` using `Number(...)` on the paging fields. |
| W7 | `components/formatDiff.ts` | `formatDiff(diff: string): string` → `JSON.stringify(JSON.parse(diff), null, 2)`; on a parse failure return `diff` unchanged. |
| W8 | `hooks/useAuditLogs.ts` | `useAuditLogs(search: AuditLogSearch)` → `useGetAuditLogs(toAuditLogParams(search), { query: { placeholderData: keepPreviousData, select: toAuditLogPage } })`. |
| W9 | `hooks/useAuditLogSearch.ts` | `const routeApi = getRouteApi('/admin/audit');` returns `{ search, applyFilters(values: AuditLogFiltersValues), setPage(page: number), clearFilters() }`. `applyFilters` navigates to `{ page: 1, ...non-empty trimmed values }`, `setPage` to `(previous) => ({ ...previous, page })`, and `clearFilters` to `{}`. |
| W10 | `pages/AuditLogPage.tsx` | `<section className="flex flex-col gap-4">`, `<h1>` `audit:page.title` (h1 tokens as in `PlaceholderPage`), `<AuditLogFilters key={JSON.stringify([search.actor, search.resourceType, search.from, search.to])} …/>`, then exactly one of: `AuditLogTableSkeleton` (isPending) · error alert (isError: `role="alert"`, title `audit:error.title`, message `t([\`errors.${code}\`, 'errors.UNHANDLED_EXCEPTION'])`, `Button variant="secondary"` "Retry" → `refetch()`) · `AuditLogEmptyState variant={hasActiveFilters(search) ? 'no-results' : 'no-data'} onClear={clearFilters}` (items empty) · `AuditLogTable` + `AuditLogPagination` (the latter only when `totalPages > 1`). |
| W11 | `components/AuditLogFilters.tsx` | Props `{ search: AuditLogSearch; onApply(values): void; onClear(): void }`. RHF + `zodResolver(auditLogFiltersSchema)`, defaults from `search` (`''` when unset), using shared `Form`. Fields: `TextField name="actor" label=filters.actor description=filters.actorHint`, `ResourceTypeField`, `TextField type="date" name="from"`, `TextField type="date" name="to"`. Buttons: `SubmitButton` `filters.apply`, `Button variant="ghost"` `filters.clear` → `onClear`. Responsive grid: 1 column under md, 4 columns at `md`. |
| W12 | `components/ResourceTypeField.tsx` | `useController({ name: 'resourceType' })` plus `useGetAuditLogResourceTypes()`. A labelled native `<select>` (label `filters.resourceType`, same token classes as `Input`), first `<option value="">` `filters.allResourceTypes`, then one option per type. |
| W13 | `components/AuditLogTable.tsx` | Card (`rounded-lg border border-border bg-surface shadow-1 overflow-x-auto`) holding a `<table>` with `<caption className="sr-only">` `table.caption`. Header cells `text-caption font-semibold text-text-muted text-start`: time, actor, action, entity, entityId, details. Maps items to `AuditLogRow` with `key={item.id}`. |
| W14 | `components/AuditLogRow.tsx` | Cells: time `formatDate(new Date(timestamp), lng, 'latin', { dateStyle: 'medium', timeStyle: 'short' })`; actor `actorUserName ?? t('table.system')` in `<bdi dir="ltr">` + `actorRole` `text-caption text-text-muted`; action, resourceType and resourceId in `font-mono text-mono` `dir="ltr"`; details → `OutcomeBadge`, `errorCode` (mono), and when `diff` is set `<details><summary>{t('table.changes')}</summary><pre dir="ltr" className="font-mono text-mono whitespace-pre-wrap">{formatDiff(diff)}</pre></details>`. Row `border-t border-border hover:bg-soft`. |
| W15 | `components/OutcomeBadge.tsx` | `outcome: string` → pill `rounded-pill px-2.5 py-0.5 text-micro font-semibold`. `Success` → `bg-success text-surface`, `Failure` → `bg-danger text-surface`. Label `t(\`outcome.${outcome}\`)`. |
| W16 | `components/AuditLogTableSkeleton.tsx` | `<div role="status" aria-busy="true" aria-label={t('page.loading')}>` with 5 `bg-soft rounded-md` row blocks (token heights). |
| W17 | `components/AuditLogEmptyState.tsx` | Props `{ variant: 'no-data' \| 'no-results'; onClear(): void }`. Card with a Lucide `ScrollText` icon (`aria-hidden`), text `empty.noData` / `empty.noResults`. `no-results` adds `Button variant="primary"` `filters.clear` → `onClear`. |
| W18 | `components/AuditLogPagination.tsx` | Props `{ page: number; totalPages: number; onPageChange(page: number): void }`. `<nav aria-label={t('pagination.label')}>`, `Button variant="secondary" size="sm"` previous (disabled when `page <= 1`) and next (disabled when `page >= totalPages`), status `<p aria-live="polite">` `t('pagination.status', { page, total: totalPages })`. Chevron icons with `rtl:rotate-180`. |
| W19–W24 | tests | `pages/AuditLogPage.test.tsx`, `schemas/auditLogSearchSchema.test.ts`, `schemas/auditLogFiltersSchema.test.ts`, `api/auditLogParams.test.ts`, `components/formatDiff.test.ts` (W19–W23). |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `AuditLogPageNumberInvalid` | `AUDIT_LOG_PAGE_NUMBER_INVALID` | `GetAuditLogsValidator` | `ValidationBehaviourException` (pipeline) | 422 |
| `AuditLogPageSizeInvalid` | `AUDIT_LOG_PAGE_SIZE_INVALID` | `GetAuditLogsValidator` | same | 422 |
| `AuditLogFilterTooLong` | `AUDIT_LOG_FILTER_TOO_LONG` | `GetAuditLogsValidator` | same | 422 |
| `AuditLogDateRangeInvalid` | `AUDIT_LOG_DATE_RANGE_INVALID` | `GetAuditLogsValidator` | same | 422 |

| Key | English | Arabic |
|---|---|---|
| `AUDIT_LOG_PAGE_NUMBER_INVALID` | Page number must be 1 or more. | رقم الصفحة يجب ان يكون 1 او اكثر. |
| `AUDIT_LOG_PAGE_SIZE_INVALID` | Page size is out of the allowed range. | حجم الصفحة خارج النطاق المسموح. |
| `AUDIT_LOG_FILTER_TOO_LONG` | Filter text is too long. | نص التصفية طويل جدا. |
| `AUDIT_LOG_DATE_RANGE_INVALID` | The end date must be after the start date. | يجب ان يكون تاريخ النهاية بعد تاريخ البداية. |

## Domain behaviour

### `AuditBehaviour.Handle` (ordered)
1. `if (request is not IAuditableCommand command) { return await next(cancellationToken).ConfigureAwait(false); }`
2. `var changesBefore = auditChangeCollector.Changes.Count; var outcome = AuditOutcome.Success; string? errorCode = null; var resourceId = command.AuditResourceId;`
3. `try`: `var response = await next(cancellationToken).ConfigureAwait(false); resourceId ??= (response as IAuditableResult)?.AuditResourceId; return response;`
4. `catch (Exception exception)`: `outcome = AuditOutcome.Failure; errorCode = (exception as BaseException)?.ErrorCode ?? exception.GetType().Name; throw;`
5. `finally`: build `AuditLog.Create(timestamp: DateTimeOffset.UtcNow, actorUserId: currentUserService.UserId, actorUserName: currentUserService.UserName, actorRole: currentUserService.GetClaim(ClaimTypes.Role), action: command.AuditAction, resourceType: command.AuditResourceType, resourceId, outcome: outcome.ToString(), errorCode, traceId: Activity.Current?.TraceId.ToString(), diff: AuditDiff.Serialize(auditChangeCollector.Changes.Skip(changesBefore).ToList()))`. Then the inner `try { await auditLogRepository.AppendAsync(entry, CancellationToken.None).ConfigureAwait(false); } catch (Exception exception) { logger.LogError(exception, <existing message>); }`, with the comment `// The request token may already be cancelled after the handler committed; the row must still be written.` kept above the call. The existing comment "Audit persistence must never break the request it describes." stays.

### `AuditChangeReader.Read`
- `IgnoredProperties` = `[nameof(IEntity.Id), nameof(IEntity.DeletedAt), nameof(IAuditEntity.CreatedBy), nameof(IAuditEntity.CreationDate), nameof(IAuditEntity.UpdatedBy), nameof(IAuditEntity.UpdationDate)]` with a WHY comment ("bookkeeping stamped on every write; the audit row already carries actor and time").
- Entries: `changeTracker.Entries().Where(entry => entry.Entity is IAuditedEntity && entry.Entity is IEntity && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)`.
- Kind: `Added` → `Created`. `Deleted` → `Deleted`. `Modified` with the `IsDeleted` property `IsModified` and current value `true` → `Deleted`. Else `Modified`.
- Property kept when: not in `IgnoredProperties`, `!property.Metadata.IsShadowProperty()`, `!property.Metadata.IsConcurrencyToken`, and:
  - Added or Deleted state: name ≠ `IsDeleted`.
  - Modified state: `property.IsModified && !Equals(property.OriginalValue, property.CurrentValue)`.
- Values: `Before` = Added ? `null` : `Serialize(property.OriginalValue)`; `After` = Deleted state ? `null` : `Serialize(property.CurrentValue)`. `Serialize(object? value) => value is null ? null : JsonSerializer.SerializeToNode(value, AuditDiff.SerializerOptions)`.
- Result: `new AuditEntityChange(entry.Metadata.ClrType.Name, ((IEntity)entry.Entity).Id, kind, properties)`. Drop changes whose `Properties.Count == 0`. Return a `List`.

### Entity
`TeacherSubject` gains only the `IAuditedEntity` marker. `Create` / `Unassign` are unchanged (Unassign already sets `UpdationDate`). `AuditLog` has no mutators; `Create` is its only write path.

## API surface
| Method | Route | Policy | Input | Response |
|---|---|---|---|---|
| GET | `/api/audit-logs` (`Name = "GetAuditLogs"`) | `DefaultCodes.AuditLogView` | `[FromQuery] string? actor, [FromQuery] string? resourceType, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default` → `new GetAuditLogsQuery(actor, resourceType, from, to, pageNumber, pageSize)` | 200 `PageData<AuditLogResult>` (`[ProducesResponseType<PageData<AuditLogResult>>(StatusCodes.Status200OK)]`); 401, 403, 422 |
| GET | `/api/audit-logs/resource-types` (`Name = "GetAuditLogResourceTypes"`) | `DefaultCodes.AuditLogView` | `CancellationToken cancellationToken` → `new GetAuditLogResourceTypesQuery()` | 200 `List<string>` (`[ProducesResponseType<List<string>>(StatusCodes.Status200OK)]`); 401, 403 |

Both actions: `return Ok(result);`, no logic, no `ConfigureAwait` (controllers).

## Web strings (`audit` namespace)
| Key | en | ar |
|---|---|---|
| `page.title` | Audit log | سجل التدقيق |
| `page.loading` | Loading audit log | جار تحميل سجل التدقيق |
| `filters.actor` | Actor | المنفذ |
| `filters.actorHint` | Email or mobile number | البريد الالكتروني او رقم الموبايل |
| `filters.resourceType` | Entity type | نوع الكيان |
| `filters.allResourceTypes` | All entity types | كل الكيانات |
| `filters.from` | From | من |
| `filters.to` | To | الى |
| `filters.apply` | Apply filters | تطبيق |
| `filters.clear` | Clear filters | مسح الفلاتر |
| `filters.errors.actorTooLong` | Actor filter is too long. | نص المنفذ طويل جدا. |
| `filters.errors.date` | Enter a valid date. | ادخل تاريخا صحيحا. |
| `filters.errors.dateRange` | End date must be on or after the start date. | يجب ان يكون تاريخ النهاية في يوم البداية او بعده. |
| `table.caption` | Audit entries | سجلات التدقيق |
| `table.time` / `actor` / `action` / `entity` / `entityId` / `details` | Time / Actor / Action / Entity / ID / Details | الوقت / المنفذ / الاجراء / الكيان / المعرف / التفاصيل |
| `table.changes` | Show changes | عرض التغييرات |
| `table.system` | System | النظام |
| `outcome.Success` / `outcome.Failure` | Success / Failed | نجح / فشل |
| `empty.noData` | No audit entries yet. | لا توجد سجلات تدقيق بعد. |
| `empty.noResults` | No entries match these filters. | لا توجد سجلات تطابق هذه الفلاتر. |
| `error.title` | Could not load the audit log | تعذر تحميل سجل التدقيق |
| `pagination.label` | Pagination | التنقل بين الصفحات |
| `pagination.previous` / `next` | Previous page / Next page | الصفحة السابقة / الصفحة التالية |
| `pagination.status` | Page {page} of {total} | صفحة {page} من {total} |

## Test plan

### api — unit
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T1 | `AuditBehaviourTests` | `Handle_NonAuditableRequest_DoesNotAppend` | Returns the next result; `AppendAsync` `DidNotReceive()`. |
| T2 | `AuditBehaviourTests` | `Handle_AuditableSuccess_AppendsSuccessRowWithActorActionAndResource` | Captured entry: ActorUserId, ActorUserName, ActorRole `Admin` (from `GetClaim(ClaimTypes.Role)`), Action, ResourceType, ResourceId = command id, Outcome `Success`, ErrorCode null. |
| T3 | `AuditBehaviourTests` | `Handle_ChangesRecordedByHandler_AppendsThemAsDiff` | `next` calls `collector.Record([change])`; entry `Diff` equals `AuditDiff.Serialize([change])`. |
| T4 | `AuditBehaviourTests` | `Handle_ChangesRecordedBeforeHandler_ExcludedFromDiff` | Collector pre-seeded with one change; `next` records none → `Diff` null. |
| T5 | `AuditBehaviourTests` | `Handle_HandlerThrowsCoreException_AppendsFailureRowAndRethrows` | `NotFoundCoreException("SUBJECT_NOT_FOUND")` rethrown (same instance); entry Outcome `Failure`, ErrorCode `SUBJECT_NOT_FOUND`. |
| T6 | `AuditBehaviourTests` | `Handle_CommandWithoutResourceId_TakesIdFromAuditableResult` | Command `AuditResourceId` null, result implements `IAuditableResult` → entry ResourceId = result id. |
| T7 | `AuditBehaviourTests` | `Handle_AppendFails_StillReturnsResponse` | `AppendAsync` throws `InvalidOperationException` → the handler result is returned. |
| T8 | `AuditBehaviourTests` | `Handle_AuditableRequest_AppendsWithUncancelledToken` | `AppendAsync` `Received(1)` with `CancellationToken.None` when `Handle` was given a cancelled token (`next` ignores the token). |
| T9 | `AuditDiffTests` | `Serialize_NoChanges_ReturnsNull` | `null`. |
| T10 | `AuditDiffTests` | `Serialize_Change_WritesCamelCaseShapeWithStringKind` | Parsed JSON: `[0].entityType`, `[0].change == "Created"`, `[0].properties.teacherId.before == null`, `.after == "<guid>"`. |
| T11 | `GetAuditLogsFilterTests` | `Build_NoFilters_MatchesEveryEntry` | Compiled predicate over 3 builder entries → 3. |
| T12 | `GetAuditLogsFilterTests` | `Build_Actor_MatchesCaseInsensitiveSubstring` | `"ADMIN@"` matches `admin@elmanhg.test`, not `teacher@…`. |
| T13 | `GetAuditLogsFilterTests` | `Build_Actor_ExcludesEntriesWithoutActor` | Null `ActorUserName` excluded. |
| T14 | `GetAuditLogsFilterTests` | `Build_WhitespaceActor_IgnoresActorFilter` | `"  "` matches all. |
| T15 | `GetAuditLogsFilterTests` | `Build_ResourceType_MatchesExactly` | `Teacher` keeps Teacher, drops `Question`. |
| T16 | `GetAuditLogsFilterTests` | `Build_From_IncludesBoundaryAndExcludesEarlier` | Entry at `From` kept; 1 s earlier dropped. |
| T17 | `GetAuditLogsFilterTests` | `Build_To_ExcludesBoundary` | Entry at `To` dropped; 1 s earlier kept. |
| T18 | `GetAuditLogsFilterTests` | `Build_FromWithOffset_ComparesInUtc` | `From = 2026-01-01T02:00+02:00` keeps an entry at `2026-01-01T00:00Z`. |
| T19 | `GetAuditLogsValidatorTests` | `Validate_ValidQuery_Passes` | Valid. |
| T20 | `GetAuditLogsValidatorTests` | `Validate_PageNumberZero_FailsWithPageNumberInvalid` | Code. |
| T21 | `GetAuditLogsValidatorTests` | `Validate_PageSizeZero_FailsWithPageSizeInvalid` | Code. |
| T22 | `GetAuditLogsValidatorTests` | `Validate_PageSizeAboveMax_FailsWithPageSizeInvalid` | 101 with Max 100 → code. |
| T23 | `GetAuditLogsValidatorTests` | `Validate_ActorTooLong_FailsWithFilterTooLong` | 257 chars → code. |
| T24 | `GetAuditLogsValidatorTests` | `Validate_ResourceTypeTooLong_FailsWithFilterTooLong` | 257 chars → code. |
| T25 | `GetAuditLogsValidatorTests` | `Validate_ToNotAfterFrom_FailsWithDateRangeInvalid` | `To == From` → code. |
| T26 | `GetAuditLogsValidatorTests` | `Validate_OnlyFrom_Passes` | Valid. |
| T27 | `GetAuditLogsHandlerTests` | `Handle_ReturnsPageMappedToResultsNewestFirstQuery` | Repository `FindPaginatedAsync(2, 5, …, asNoTracking: true)` `Received(1)`; the returned page's items map every field (including Diff) and copy the paging totals. |

`GetAuditLogResourceTypesHandler` has a single pass-through branch, so a unit test would only assert the mock (vacuous). I17–I18 cover it end to end.

### api — integration (real PostgreSQL via `ApiFactory`)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| I1 | `AuditChangeCaptureTests` | `SaveChangesAsync_AuditedEntityAdded_RecordsCreatedChange` | Same scope's `IAuditChangeCollector`: one change, `TeacherSubject`, id, `Created`, keys exactly `{TeacherId, SubjectId}`, `After` of TeacherId `ToJsonString()` = quoted teacher id. |
| I2 | `AuditChangeCaptureTests` | `SaveChangesAsync_AuditedEntitySoftDeleted_RecordsDeletedChangeWithIsDeleted` | `Unassign` + save → `Deleted`, keys exactly `{IsDeleted}`, before `false`, after `true`. |
| I3 | `AuditChangeCaptureTests` | `SaveChangesAsync_NonAuditedEntity_RecordsNothing` | Saving a `Subject` → `Changes` empty. |
| I4 | `AuditChangeCaptureTests` | `SaveChangesAsync_SaveFails_RecordsNothing` | Two live `TeacherSubject` rows for one pair → `DbUpdateException`; `Changes` empty. |
| I5 | `AuditLogAppendOnlyTests` | `AppendAsync_Entry_PersistsRowWithJsonbDiff` | Fresh scope reads the row; `JsonNode.DeepEquals(parse(Diff), parse(input))`; Outcome, Action match. |
| I6 | `AuditLogAppendOnlyTests` | `Update_AuditLogRow_RejectedByDatabase` | `ExecuteSqlAsync($"UPDATE \"AuditLogs\" SET \"Action\" = {"Tampered"} WHERE \"Id\" = {id}")` throws `PostgresException` with `SqlState == "P0001"`; row Action unchanged. |
| I7 | `AuditLogAppendOnlyTests` | `Delete_AuditLogRow_RejectedByDatabase` | DELETE throws `PostgresException` P0001; row still present. |
| I8 | `AuditLogsEndpointTests` | `Get_AfterAssign_ReturnsSuccessEntryWithCreatedDiff` | Admin assigns via API, then `GET ?actor=<adminEmail>` → 200, one item: action `Teacher.AssignSubject`, resourceType `Teacher`, resourceId teacher, outcome `Success`, actorRole `Admin`, diff `[0].change == "Created"`, `properties.subjectId.after == subjectId`. |
| I9 | `AuditLogsEndpointTests` | `Get_AfterUnassign_ReturnsEntryWithIsDeletedDiff` | Newest item `Teacher.UnassignSubject`, diff `properties.isDeleted` false→true. |
| I10 | `AuditLogsEndpointTests` | `Get_AfterFailedAssign_ReturnsFailureEntryWithErrorCode` | Assign an unknown subject (404), then the item has outcome `Failure`, errorCode `SUBJECT_NOT_FOUND`, diff null. |
| I11 | `AuditLogsEndpointTests` | `Get_ResourceTypeFilter_ExcludesOtherTypes` | After an assign, `resourceType=Question&actor=<adminEmail>` → `totalItems` 0. |
| I12 | `AuditLogsEndpointTests` | `Get_DateRangeInFuture_ReturnsNoItems` | `from=<now+1d UTC ISO>` → `totalItems` 0. |
| I13 | `AuditLogsEndpointTests` | `Get_PageSizeOne_ReturnsNewestFirstWithTotals` | Two assigns (two subjects) → `pageSize=1`: 1 item = second subject's assign, `totalItems` 2, `totalPages` 2. |
| I14 | `AuditLogsEndpointTests` | `Get_ToBeforeFrom_Returns422DateRangeInvalid` | 422, `code` contains `AUDIT_LOG_DATE_RANGE_INVALID`. |
| I15 | `AuditLogsEndpointTests` | `Get_PageSizeAboveMax_Returns422PageSizeInvalid` | `pageSize=101` → 422, code contains `AUDIT_LOG_PAGE_SIZE_INVALID`. |
| I16 | `AuditLogsEndpointTests` | `Get_TeacherCaller_Returns403` | 403. |
| I17 | `AuditLogsEndpointTests` | `Get_Anonymous_Returns401` | 401. |
| I18 | `AuditLogsEndpointTests` | `GetResourceTypes_AfterAssign_ContainsTeacher` | 200, array contains `Teacher`, ascending order. |
| I19 | `AuditLogsEndpointTests` | `GetResourceTypes_TeacherCaller_Returns403` | 403. |
| I20 | `PipelineCompositionTests` | `Resolve_PipelineBehaviours_AuditBehaviourIsOutermost` | First resolved behaviour is `AuditBehaviour<PipelineProbeRequest, Unit>`. |

Each endpoint test seeds its own admin, teacher and subject with `ScopeTestData` and filters by that admin's email, so rows from other tests never leak in.

### web (Vitest + MSW, `renderApp('/admin/audit', { session: testSessions.admin })`; `beforeEach` → `server.use(getGetAuditLogResourceTypesMockHandler(['Teacher']))`)
| # | Test file | `it(...)` | Asserts |
|---|-----------|-----------|---------|
| R1 | `AuditLogPage.test.tsx` | `shows audit entries after loading` | `status` "Loading audit log" first, then row containing `Teacher.AssignSubject` and `admin@elmanhg.test`. |
| R2 | same | `shows the empty state when there are no entries` | Text "No audit entries yet."; no "Clear filters" button inside it. |
| R3 | same | `offers clear filters when filters match nothing` | Path `?actor=nobody` → "No entries match these filters."; click "Clear filters" → `router.state.location.search` has no `actor`, and a row appears. |
| R4 | same | `shows an error and recovers on retry` | 500 → alert "Could not load the audit log"; reset handler; click "Retry" → row visible. |
| R5 | same | `applies filters to the URL and the request` | Type actor, pick "Teacher", submit → MSW handler echoes the `actor`/`resourceType` it received into the row; `router.state.location.search` matches `{ actor, resourceType: 'Teacher', page: 1 }`. |
| R6 | same | `moves to the next page` | `totalPages: 2` → "Page 1 of 2"; click "Next page" → page-2 row, "Page 2 of 2", Next disabled. |
| R7 | same | `shows an inline error when the end date is before the start date` | Fill from 2026-09-10, to 2026-09-01, submit → "End date must be on or after the start date." and search unchanged. |
| R8 | same | `expands the changes of an entry` | Click "Show changes" → pretty JSON containing `"teacherId"` visible. |
| R9 | same | `renders right-to-left in Arabic` | `lng: 'ar'` → heading "سجل التدقيق", `document.documentElement.dir === 'rtl'`. |
| R10 | same | `has no axe violations` | `axe(container)` after the table renders. |
| R11 | `auditLogSearchSchema.test.ts` | `keeps valid filters and page` / `drops an invalid page` / `drops an invalid date` / `drops a blank actor` / `accepts an empty search` | Parse output per case. |
| R12 | `auditLogFiltersSchema.test.ts` | `accepts blank filters` / `rejects an actor over the limit` / `rejects an end date before the start date` / `accepts the same start and end date` / `rejects a malformed date` | Success or the issue message key per case (the date-range issue has path `['to']`). |
| R13 | `auditLogParams.test.ts` | `maps page and page size` / `sends from as local midnight` / `sends to as the next local midnight` / `omits unset filters` / `detects active filters` / `normalises string paging numbers` | Expected values computed with `new Date(y, m, d).toISOString()` in the test (TZ-independent). |
| R14 | `formatDiff.test.ts` | `pretty-prints valid JSON` / `returns invalid JSON unchanged` | Output string. |

## Definition of done
- [ ] `AuditBehaviour` is the only audit writer. No domain event or event handler was added for auditing.
- [ ] `IAuditLogRepository` exposes only `AppendAsync`, `FindPaginatedAsync` and `GetResourceTypesAsync`. `AppendAsync` is one interpolated `ExecuteSqlAsync` INSERT. No `ExecuteSqlRaw` anywhere.
- [ ] The migration adds `Diff jsonb`, the two indexes and both triggers plus the function. `Down` drops them. `Up` has no Drop or Rename. `dotnet ef migrations has-pending-model-changes` reports none.
- [ ] I6 and I7 prove the DB rejects UPDATE and DELETE on `AuditLogs`.
- [ ] `CoreDbContext` records diffs only for `IAuditedEntity` entries and only after `base.SaveChangesAsync` succeeds (I1–I4).
- [ ] `TeacherSubject : AuditEntity, IAuditedEntity`. `User` is not marked.
- [ ] `CoreAuditing:Enabled` is `true` in both appsettings files and in `ApiFactory`. `AuditLogs` options are bound with `ValidateOnStart`.
- [ ] The teacher commands' `AuditAction` values are `Teacher.AssignSubject` / `Teacher.UnassignSubject`.
- [ ] Both endpoints carry `[Authorize(Policy = DefaultCodes.AuditLogView)]`. `EndpointAuthorizationTests` is still green.
- [ ] The 4 error codes are in `ErrorCodes.cs`, both resx files and both web `shared/i18n` files.
- [ ] The filter converts `from`/`to` to UTC. Paging reads use `asNoTracking: true`. Sort is `Timestamp desc, Id desc`.
- [ ] `api/openapi/v1.json` is regenerated. `npm --prefix web run gen:api` gives no diff after commit.
- [ ] Postman has an `AuditLogs` folder with both GETs.
- [ ] `docs/audit-log.md` exists and matches the implementation. PRD §15 `AuditLog` line updated.
- [ ] Web `/admin/audit` shows loading, error+retry, empty (no-data), no-results + clear, the table, the diff expander and pagination. Filters live in URL search params.
- [ ] No literal colours or px values and no physical-direction utilities in `web/src/features/audit`. Ids, actions and diffs are `dir="ltr"`. Timestamps use Latin digits.
- [ ] Every new string is in `audit` en and ar.
- [ ] Every test T1–T27, I1–I20 and R1–R14 exists with the listed name and passes. No existing test was edited except the additions in `PipelineCompositionTests` and the config lines in `ApiFactory`.
- [ ] `dotnet build` has zero new warnings. `dotnet test` is green. `dotnet format --verify-no-changes` exits 0.
- [ ] `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check .` and `npx vitest run --coverage` (thresholds met) all exit 0.
- [ ] Guard grep is clean (`DateTime.Now|UtcNow`, `.Result`, `.Wait()`, `FromSqlRaw`, `async void`). No file over ~100 lines (C#) or 200 (TSX).
