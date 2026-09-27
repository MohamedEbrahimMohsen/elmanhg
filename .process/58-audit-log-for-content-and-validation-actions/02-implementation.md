# Implementation — Audit log for content and validation actions (#58, E1.S5)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/core-libraries/Core.DDD/Entities/IAuditedEntity.cs | 5 | F1 opt-in diff marker |
| api/core-libraries/Core.Auditing/AuditChangeKind.cs | 8 | F2 Created / Modified / Deleted |
| api/core-libraries/Core.Auditing/AuditValueChange.cs | 5 | F3 before/after `JsonNode` pair |
| api/core-libraries/Core.Auditing/AuditEntityChange.cs | 3 | F4 one entity's diff |
| api/core-libraries/Core.Auditing/IAuditChangeCollector.cs | 8 | F5 scoped collector contract |
| api/core-libraries/Core.Auditing/AuditChangeCollector.cs | 10 | F6 collector |
| api/core-libraries/Core.Auditing/AuditDiff.cs | 15 | F7 shared serializer options + `Serialize` |
| api/core-libraries/Core.EntityFrameworkCore/Auditing/AuditChangeReader.cs | 65 | F8 change-tracker diff reader |
| api/Elmanhg.Application/Shared/Options/AuditLogsOptions.cs | 14 | F10 caps (`MaxPageSize`, `FilterMaxLength`) |
| api/Elmanhg.Application/AuditLogs/GetAuditLogs/GetAuditLogsQuery.cs | 7 | F11 |
| api/Elmanhg.Application/AuditLogs/GetAuditLogs/GetAuditLogsValidator.cs | 21 | F12 |
| api/Elmanhg.Application/AuditLogs/GetAuditLogs/GetAuditLogsFilter.cs | 20 | F13 predicate builder (UTC conversion) |
| api/Elmanhg.Application/AuditLogs/GetAuditLogs/GetAuditLogsHandler.cs | 25 | F14 |
| api/Elmanhg.Application/AuditLogs/GetAuditLogResourceTypes/GetAuditLogResourceTypesQuery.cs | 5 | F15 |
| api/Elmanhg.Application/AuditLogs/GetAuditLogResourceTypes/GetAuditLogResourceTypesHandler.cs | 12 | F16 |
| api/Elmanhg.Application/AuditLogs/Shared/AuditLogResult.cs | 3 | F17 admin result |
| api/Elmanhg.Application/AuditLogs/Shared/AuditLogResultGenerator.cs | 11 | F18 |
| api/Elmanhg.Infrastructure/Migrations/20260927225923_AddAuditLogDiffAndAppendOnly.cs | 58 | F19 `Diff jsonb`, 2 indexes, function + 2 triggers (Down drops them first) |
| api/Elmanhg.Infrastructure/Migrations/20260927225923_AddAuditLogDiffAndAppendOnly.Designer.cs | 795 | F19 generated |
| api/Elmanhg.Api/Controllers/AuditLogs/AuditLogsController.cs | 34 | F20 two GETs, `AuditLog.View` |
| docs/audit-log.md | 108 | F21 |
| api/Elmanhg.Tests/Builders/AuditLogBuilder.cs | 40 | F22 |
| api/Elmanhg.Tests/Core/Auditing/AuditBehaviourTests.cs | 144 | F23 T1–T8 |
| api/Elmanhg.Tests/Core/Auditing/AuditDiffTests.cs | 30 | F24 T9–T10 |
| api/Elmanhg.Tests/Application/Features/AuditLogs/GetAuditLogs/GetAuditLogsFilterTests.cs | 99 | F25 T11–T18 |
| api/Elmanhg.Tests/Application/Features/AuditLogs/GetAuditLogs/GetAuditLogsValidatorTests.cs | 80 | F26 T19–T26 |
| api/Elmanhg.Tests/Application/Features/AuditLogs/GetAuditLogs/GetAuditLogsHandlerTests.cs | 45 | F27 T27 |
| api/Elmanhg.Tests/Integration/Persistence/AuditChangeCaptureTests.cs | 93 | F28 I1–I4 |
| api/Elmanhg.Tests/Integration/Persistence/AuditLogAppendOnlyTests.cs | 70 | F29 I5–I7 |
| api/Elmanhg.Tests/Integration/AuditLogs/AuditLogsEndpointTests.cs | 214 | F30 I8–I19 |
| web/src/features/audit/index.ts | 7 | W1 barrel |
| web/src/features/audit/i18n/en.json, ar.json | 49 / 49 | W2/W3 |
| web/src/features/audit/schemas/auditLogSearchSchema.ts | 16 | W4 |
| web/src/features/audit/schemas/auditLogFiltersSchema.ts | 18 | W5 |
| web/src/features/audit/api/auditLogParams.ts | 40 | W6 |
| web/src/features/audit/components/formatDiff.ts | 7 | W7 |
| web/src/features/audit/hooks/useAuditLogs.ts | 10 | W8 |
| web/src/features/audit/hooks/useAuditLogSearch.ts | 39 | W9 |
| web/src/features/audit/pages/AuditLogPage.tsx | 71 | W10 |
| web/src/features/audit/components/AuditLogFilters.tsx | 50 | W11 |
| web/src/features/audit/components/ResourceTypeField.tsx | 37 | W12 |
| web/src/features/audit/components/AuditLogTable.tsx | 39 | W13 |
| web/src/features/audit/components/AuditLogRow.tsx | 63 | W14 |
| web/src/features/audit/components/OutcomeBadge.tsx | 26 | W15 |
| web/src/features/audit/components/AuditLogTableSkeleton.tsx | 20 | W16 |
| web/src/features/audit/components/AuditLogEmptyState.tsx | 24 | W17 |
| web/src/features/audit/components/AuditLogPagination.tsx | 43 | W18 |
| web/src/features/audit/pages/AuditLogPage.test.tsx | 191 | R1–R10 |
| web/src/features/audit/schemas/auditLogSearchSchema.test.ts | 38 | R11 |
| web/src/features/audit/schemas/auditLogFiltersSchema.test.ts | 34 | R12 |
| web/src/features/audit/api/auditLogParams.test.ts | 40 | R13 |
| web/src/features/audit/components/formatDiff.test.ts | 12 | R14 |
| web/src/shared/api/generated/audit-logs/**, zod/audit-logs/**, model/{auditLogResult,getAuditLogsParams,pageDataOfAuditLogResult}.ts | generated | Orval output (never hand-edited) |

## Files modified
| Path | Change |
|---|---|
| api/core-libraries/Core.Auditing/Entities/AuditLog.cs | `Diff` property; `Create(..., string? diff)` |
| api/core-libraries/Core.Auditing/Repositories/IAuditLogRepository.cs | Narrowed to `AppendAsync` / `FindPaginatedAsync` / `GetResourceTypesAsync` (no `IRepository<AuditLog>`) |
| api/core-libraries/Core.Auditing/AuditBehaviour.cs | Collector in ctor, count snapshot + `Skip`, `AppendAsync(entry, CancellationToken.None)` with WHY comment, `ConfigureAwait(false)`, `ex` → `exception`, unused `using` removed |
| api/core-libraries/Core.Auditing/DependencyInjection.cs | Unconditional scoped `IAuditChangeCollector` |
| api/core-libraries/Core.EntityFrameworkCore/Context/CoreDbContext.cs | Ctor gains collector; `AuditLog` mapping (jsonb, 2 indexes); diff read before `base.SaveChangesAsync`, recorded after success |
| api/core-libraries/Core.EntityFrameworkCore/Repositories/AuditLogRepository.cs | `AppendAsync` (interpolated `ExecuteSqlAsync` INSERT, `CAST(... AS jsonb)`), `GetResourceTypesAsync` |
| api/Elmanhg.Domain/Teachers/TeacherSubject.cs | `: AuditEntity, IAuditedEntity` |
| api/Elmanhg.Application/Teachers/AssignTeacherSubject/AssignTeacherSubjectCommand.cs | `Teacher.AssignSubject` |
| api/Elmanhg.Application/Teachers/UnassignTeacherSubject/UnassignTeacherSubjectCommand.cs | `Teacher.UnassignSubject` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | `// AUDIT LOGS` group, 4 codes |
| api/Elmanhg.Application/DependencyInjection.cs | `AuditLogsOptions` bound with `ValidateDataAnnotations().ValidateOnStart()` |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | Ctor passes `IAuditChangeCollector` |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | Regenerated by `dotnet ef` |
| api/Elmanhg.Api/appsettings.example.json | `CoreAuditing.Enabled = true`, `AuditLogs` section |
| api/Elmanhg.Api/appsettings.json | Same edit, but the file is gitignored (`.gitignore:23`) so it is a local-only change |
| api/Elmanhg.Api/Resources/Messages.en.resx, Messages.ar.resx | 4 keys each |
| api/openapi/v1.json | Regenerated by the build (+2 paths, 3 schemas) |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | Auditing on, `AuditLogs:*` caps |
| api/Elmanhg.Tests/Integration/Composition/PipelineCompositionTests.cs | I20 added |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | Migration list gains the 4th migration (see Deviations) |
| postman/elmanhg.postman_collection.json | `AuditLogs` folder, 2 GETs, inherits collection bearer auth |
| docs/PRD.md §15 | `AuditLog(...)` line replaced per plan |
| web/src/routes/admin/audit.tsx | `validateSearch` + `AuditLogPage` |
| web/src/app/i18n.ts | `audit` namespace |
| web/src/shared/i18n/en.json, ar.json | 4 `errors.AUDIT_LOG_*` keys |
| web/src/shared/form/TextField.tsx | `type` union gains `'date'` |
| web/orval.config.ts | See Deviations |
| web/src/shared/api/generated/{index.ts, model/index.ts, zod/index.zod.ts} | Regenerated |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| No existing test edited except `PipelineCompositionTests` and `ApiFactory` | `AppDbContextTests.Migrate_FreshDatabase_LeavesNoPendingMigrations` pins the exact list of applied migrations. With the plan's new migration it fails ("Expected applied to contain exactly 3 items, but it contains 4 items"). | Appended `fourth => fourth.Should().EndWith("_AddAuditLogDiffAndAppendOnly")`. This makes the assertion stricter, not weaker, and it is the same edit E1.S4 made (commit 6a06771). |
| `web/orval.config.ts` is not in *Existing code touched* | With the plan's API contract, Orval 8.38 generates code that fails `tsc`: (1) the MSW mock for `PageDataOfAuditLogResult` (all properties optional) assigns `undefined` under `exactOptionalPropertyTypes`; (2) the zod query schema emits `zod.stringFormat(...).default(20)` for the `integer\|string` paging params (TS2769). | Added `override.mock.exactOptional: true` (api output) and `override.operations.GetAuditLogs.zod.generate.query = false` (apiZod output). The existing auth/teachers generated files are byte-identical, and `gen:api` is idempotent. |
| `appsettings.json` gets `CoreAuditing.Enabled = true` and `AuditLogs` | `api/Elmanhg.Api/appsettings.json` is gitignored | Edited it locally anyway. Only `appsettings.example.json` carries the change into the commit. |

## Build & test
- `dotnet build api/Elmanhg.slnx`: `0 Warning(s)`, `0 Error(s)`. With `--no-incremental`, the only warnings are the existing ones in vendored `Core.Notifications`, `Core.OTP` and `Core.Validation` (CS8618/CS8602). None are in touched files.
- `dotnet ef migrations add AddAuditLogDiffAndAppendOnly -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`: `Done.` The generated ops matched the plan (AddColumn + 2 CreateIndex, no Drop/Rename in `Up`). Trigger SQL was appended by hand.
- `dotnet test api/Elmanhg.slnx` (Docker 29.6.2, Testcontainers pgvector:pg17): `total: 256, failed: 0, succeeded: 256, skipped: 0`. The first run, before the `AppDbContextTests` update, was `failed: 1`, and that one failure was the migration-count test above. `Model_Current_MatchesLatestMigrationSnapshot` passes, so there are no pending model changes.
- `dotnet format api/Elmanhg.slnx --verify-no-changes`: exit 2. Every reported file is an untouched vendored `core-libraries` file (known noise). No story file is listed.
- Guard grep (`DateTime.Now|UtcNow`, `.Result`, `.Wait()`, `FromSqlRaw`, `ExecuteSqlRaw`, `async void`, `new HttpClient(`) over added C# lines: empty.
- `npm --prefix web run gen:api`: generated output regenerated. Running it a second time produced identical file hashes.
- `npx tsc -b --noEmit`: exit 0. `npx eslint . --max-warnings=0`: exit 0.
- `npx prettier --check .`: exit 1, "Code style issues found in 132 files" (repo-wide CRLF noise). `npx prettier --check --end-of-line auto` on every touched web path: "All matched files use Prettier code style!"
- `npm --prefix web run build`: exit 0.
- `npm --prefix web test -- --run`: `Test Files 29 passed (29)`, `Tests 141 passed (141)`.
- `npx vitest run --coverage`: exit 0, thresholds met (All files: 92.26 lines / 79.34 branches).

## Notes for review
- `AuditBehaviour` keeps Morabh's XML `<summary>` doc block and the two WHY comments the plan asked for.
- `AuditChangeReader.IgnoredProperties` is a `HashSet<string>` (plan: list of `nameof`s), for `Contains`.
- In the web error alert the message key is `common:errors.${code}` and Retry is `common:actions.retry` ("Retry"). The page uses the `audit` namespace, so the plan's bare `errors.*` would have resolved inside `audit`.
- `OutcomeBadge` falls back to neutral `bg-soft text-text-muted` and the raw string for an unknown outcome. That fallback branch has no test (OutcomeBadge branches 50%).
- R10 (axe) renders a second, failure row (null actor, error code, no diff) so axe covers the badge, error code and "System" paths. This adds no assertions beyond the plan.
- `AppendAsync` sends `Diff` as text and casts it `CAST(@p AS jsonb)`. A null diff is inferred from the cast. I5 proves the jsonb round-trip, and I6/I7 prove the P0001 rejections.
- The date inputs rely on user-event typing into `type="date"` in jsdom (R7 passes).
- The 422 body's `message` for `AUDIT_LOG_DATE_RANGE_INVALID` comes from the existing `ValidationBehaviour` message path (log showed FluentValidation's default localized text). The `code` is correct. I did not touch that path.
