VERDICT: CHANGES_REQUESTED

# Review — Audit log for content and validation actions (#58, E1.S5)

## Blocking

### 1. `docs/audit-log.md` says failure rows never carry a diff, but the code writes one
**Where:** `docs/audit-log.md:32` (§ Record fields, `diff_json` row: "always null on failure") vs `api/core-libraries/Core.Auditing/AuditBehaviour.cs:57`
**Rule:** `.claude/rules/docs-sync.md` (divergence); plan D6 ("The diff is normally null")
**Problem:** The `finally` block serialises `auditChangeCollector.Changes.Skip(changesBefore)` whatever the outcome. `CoreDbContext.SaveChangesAsync` records changes once `base.SaveChangesAsync` succeeds. So if a handler commits and then throws, it produces an `Outcome = Failure` row with a non-null `Diff`. The doc, which this change creates, states a contract the code does not keep. This is the doc an admin uses to read disputes.
**Failure:** An audited command whose handler calls `SaveChangesAsync` successfully and then throws a `CoreException` (for example, a post-save step in an E2/E3 handler) writes a row with `outcome: "Failure"`, `errorCode: <code>` and `diff: [{"change":"Created",...}]`. The doc says `diff` is always null for that row.
**Fix:** Change the `diff_json` note to match the code and D6, for example: "null when no audited entity changed. On failure it is null unless the handler committed changes before it threw; those committed changes are listed." No code change is needed.

## Non-blocking
- `api/core-libraries/Core.Auditing/AuditBehaviour.cs:64`: no test covers the main claim of D4, that a failed command's tracked but unsaved entities are not flushed by the audit write. I10 fails before anything is tracked, and T1-T8 substitute the repository. If someone reverted `AppendAsync` to `AddAsync` + `SaveChangesAsync`, every test would still pass. Add an integration test: an auditable probe handler tracks a `TeacherSubject`, throws without saving, and then the row count for that pair is 0 while one Failure audit row exists.
- `api/Elmanhg.Api/Controllers/AuditLogs/AuditLogsController.cs:15`: the controller is not `sealed`. This mirrors the existing `TeachersController` and `AuthController`, so it is consistent with the repo but not with the skill's `sealed` absolute. Seal all three in a follow-up.
- `docs/backlog.json:61`: the sub-task still says "Domain event handler that writes an audit row". Plan D1 replaced that on purpose, and `docs/audit-log.md:10` documents the real mechanism. Consider rewording the backlog item so the two do not disagree.
- `web/src/features/audit/components/AuditLogTable.tsx:18`: `.claude/design-system.md` says a Table has a "sticky header on desktop". That is not implemented, and it would not work inside `overflow-x-auto` without a wrapper change. It is cosmetic.
- `api/Elmanhg.Tests/Application/Features/AuditLogs/GetAuditLogs/GetAuditLogsHandlerTests.cs:125`: `filter` and `orderBy` are `Arg.Any`, so the unit test does not pin the sort. I13 covers newest-first end to end, so this is acceptable.
- `02-implementation.md` note 120: the 422 `message` for `AUDIT_LOG_DATE_RANGE_INVALID` reportedly comes from the FluentValidation default text. The resx key exists and `ValidationBehaviour` looks the message up by code, so check it by reading one 422 body. Only the `code` is asserted (I14).

## Verified
- Build and tests, re-run by me: `dotnet build api/Elmanhg.slnx`: 0 warnings, 0 errors. `dotnet test api/Elmanhg.slnx`: 256 total, 256 passed. `npm --prefix web run build`: built. `npm --prefix web test -- --run`: 29 files, 141 tests passed.
- Append-only, repository surface: `IAuditLogRepository.cs` no longer extends `IRepository<AuditLog>`. It exposes only `AppendAsync`, `FindPaginatedAsync` and `GetResourceTypesAsync`. DI registers only the interface (`Core.EntityFrameworkCore/DependencyInjection.cs:26`), so the inherited `Repository<>` mutators are unreachable through DI.
- Append-only, DB: the migration (`20260927225923_AddAuditLogDiffAndAppendOnly.cs:29-33`) creates the function, a row-level `BEFORE UPDATE OR DELETE` trigger and a statement-level `BEFORE TRUNCATE` trigger. `Down` drops them first. `Up` has no Drop or Rename. `AuditLogs` has no FKs in the snapshot, so no cascade can hit the trigger. I6 and I7 assert P0001 and that the row is unchanged.
- No leftovers on failure: `AppendAsync` (`AuditLogRepository.cs:16-19`) is one interpolated `ExecuteSqlAsync` INSERT with `CAST(... AS jsonb)`, so it is parameterised and bypasses the change tracker. There is no `ExecuteSqlRaw` or `FromSqlRaw` anywhere, and the guard grep over added C# is clean. `CoreDbContext.cs:114-117` records diffs only after `base.SaveChangesAsync` returns (I4 proves nothing is recorded on `DbUpdateException`).
- No sensitive fields in diffs: `IAuditedEntity` is implemented only by `TeacherSubject` (grep). `User` is not marked. `AuditChangeReader` drops Id, DeletedAt, Created/Updated stamps, shadow properties and concurrency tokens. I1 asserts the keys are exactly `{TeacherId, SubjectId}`, and I2 asserts exactly `{IsDeleted}`.
- Admin-only read: both actions carry `[Authorize(Policy = DefaultCodes.AuditLogView)]`, which maps to `RequireRole(Admin)` (`PermissionMatrixPolicies.cs:32`). I16, I17 and I19 cover 403 for a teacher and 401 for anonymous. The web route sits under the guarded `/admin` layout.
- AuditBehaviour is outermost: `AddCoreAuditing` is registered before `AddCoreCQRS` (`Program.cs:59-60`), and I20 asserts this. `CancellationToken.None` is used with a WHY comment, and T8 asserts it.
- Deviation 1 (`AppDbContextTests`): confirmed. It appends a fourth `EndWith("_AddAuditLogDiffAndAppendOnly")`, which makes the test stricter.
- Deviation 2 (`web/orval.config.ts`): confirmed. It adds `mock.exactOptional` and turns off zod query generation for `GetAuditLogs` only. Build and tsc pass.
- Deviation 3 (`appsettings.json` is gitignored): confirmed at `.gitignore:23`. The committed `appsettings.example.json` carries `CoreAuditing.Enabled = true` and the `AuditLogs` caps. `ApiFactory` sets auditing on.
- F1-F30 and W1-W24 exist with the planned contracts. The action names are `Teacher.AssignSubject` and `Teacher.UnassignSubject`. The 4 error codes are in `ErrorCodes.cs`, both resx files and both web `shared/i18n` files. `AuditLogsOptions` uses `ValidateDataAnnotations().ValidateOnStart()`. The filter converts to UTC. The handler uses `asNoTracking: true` and sorts `Timestamp desc, Id desc`.
- Postman: the `AuditLogs` folder has `GET {{baseUrl}}/api/audit-logs` (paging params, plus disabled actor, resourceType, from and to) and `GET .../resource-types`, both inheriting collection auth. No stale requests.
- Docs: `docs/PRD.md` §15 `AuditLog(...)` line is updated per plan, and `docs/audit-log.md` exists. Apart from Blocking #1, the doc matches the code.
- Every test T1-T27, I1-I20 and R1-R14 exists with the planned name.

## Test quality
- `AuditBehaviourTests`: constrains the behaviour. It captures the real `AuditLog` passed to `AppendAsync` and uses a real `AuditChangeCollector`. T3 and T4 would fail if the count snapshot or `Skip` were removed. T5 checks that the same exception instance is rethrown. T8 would fail if the request token were passed through.
- `AuditDiffTests`: constrains the camelCase keys, the string enum and null `before`.
- `GetAuditLogsFilterTests`: constrains the filter. It compiles the real predicate, covers both boundaries (`>=` and `<`), and T18 would fail without `ToUniversalTime`.
- `GetAuditLogsValidatorTests`: every rule has a failing case, including the `To == From` boundary.
- `GetAuditLogsHandlerTests`: partly constraining. The field-by-field mapping and `asNoTracking: true` are real checks. The sort is left to I13.
- `AuditChangeCaptureTests` and `AuditLogAppendOnlyTests`: these run against real PostgreSQL and constrain capture, the success-only recording and the trigger.
- `AuditLogsEndpointTests`: end to end. Tests are isolated by admin email, and newest-first is checked through the diff of the second assign.
- Web `AuditLogPage.test.tsx`: R5 has the MSW handler echo the received query params into the row, so it proves the request carries the filters, not only the URL. R3, R6 and R7 assert router search state. No vacuous tests found.
