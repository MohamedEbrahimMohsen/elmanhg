VERDICT: APPROVED

# Review — Role-based authorisation and teacher subject scoping (#57, E1.S4)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Application/Features/Teachers/UnassignTeacherSubject/UnassignTeacherSubjectHandlerTests.cs:64` — the `FirstOrDefaultAsync` stub matches `Arg.Any` for the predicate. A handler that filtered by `TeacherId` alone would still pass this unit test. I16 and I17 cover the real predicate end to end, so this does not gate.
- `api/Elmanhg.Application/Teachers/AssignTeacherSubject/AssignTeacherSubjectHandler.cs:34-42` — two concurrent assigns of the same pair can both pass `IsAssignedAsync`. The filtered unique index then makes the second `SaveChangesAsync` fail with a 500 instead of a 409. Data stays correct; only the status code is wrong. Worth mapping the unique violation later.
- `api/Elmanhg.Tests/Application/Features/Teachers/AssignTeacherSubject/AssignTeacherSubjectHandlerTests.cs:39-42` — T7 does not assert `CreatedBy == currentUserId` or `AssignedAt`. The plan does not ask for either.
- No test covers a teacher requesting a random (non-existent) subject id. The behaviour never loads the subject (`SubjectScopeBehaviour.cs:31`), so D11 holds by construction.
- `TeacherSubjectsEndpointTests.cs` (156 lines), `SubjectScopeEndpointTests.cs` (130) and `SubjectScopeBehaviourTests.cs` (112) are over the ~100-line guide. Existing integration test files already are too (`EmailAuthEndpointTests.cs` 113, `PhoneAuthEndpointTests.cs` 122).

## Verified
- Intent: 18 capability policies are in `DefaultCodes.cs` with the plan's values, registered once in `PermissionMatrixPolicies.cs:15-33` and chained in `Program.cs:51` (one `using` plus one call; middleware untouched). The role sets match the plan table and PRD §16 row by row.
- Every endpoint has a policy: `TeachersController.cs:17,26` use `DefaultCodes.UsersManage` (Admin only). `EndpointAuthorizationTests` (I19) enumerates every controller endpoint, including the test probe. There are no `Roles =` and no raw policy strings in `api/`.
- The scope behaviour fails closed (`SubjectScopeBehaviour.cs:15-36`):
  - Only the exact role claims `Student` and `Admin` bypass the check.
  - A null or unknown role, including `Teacher`, needs a live `TeacherSubject` row checked through `AnyAsync` against the DB (D12).
  - A missing user id returns 401.
  - It has no try/catch and uses `ConfigureAwait(false)` everywhere.
- Out-of-scope requests get 403 `SUBJECT_OUT_OF_SCOPE`. The subject is never loaded, so there is no existence oracle.
- It is registered in `AddApplication()` after `AddCoreAuditing` and `AddCoreCQRS` (`Program.cs:59-64`), so the pipeline order is audit → validation → scope (D14).
- Unassignment takes effect on the next request with the same access token (I5, which also asserts a 200 before the unassign).
- Domain: `Subject` and `TeacherSubject` extend `AuditEntity` with private ctors and static `Create`. `USER_NOT_TEACHER` is thrown as `BusinessRuleViolationCoreException` from the domain `ErrorCodes`. `Unassign` soft-deletes and stamps `UpdatedBy` and `UpdationDate`. `User.CreateTeacher` mirrors `CreateAdmin`.
- Infrastructure:
  - Both DbSets use `{ get; set; }`. Configuration is in named private methods.
  - Both FKs are Restrict, with the filtered unique index `"IsDeleted" = false`, and both entities are in the global soft-delete filter.
  - The migration only creates tables, FKs and indexes; `Drop` appears only in `Down`.
  - `dotnet ef migrations has-pending-model-changes` reports no changes.
- Handlers are `sealed`, have no try/catch, guard the current user first, call `SaveChangesAsync` once and use `ConfigureAwait(false)` on every await. Unassign loads with tracking, which is the Core default (`Repository.cs:167`). Both commands implement `IAuditableCommand` with resource type `Teacher`.
- The 7 resx keys are in both `Messages.en.resx` and `Messages.ar.resx` with the plan's strings.
- Deviation 1 (`TabBar.tsx:13`, `Omit<NavItem,'capability'>`) is real and minimal. Deviation 2 (the `teacherId`/`subjectId` collection variables) is real and needed. No hidden deviations found.
- Postman: the `Teachers` folder has POST and DELETE on `{{baseUrl}}/api/teachers/{{teacherId}}/subjects/{{subjectId}}`, inheriting collection bearer auth like `Logout`. There are no stale requests.
- Docs: `docs/PRD.md` §16 has the 3 rows in the planned positions and agrees with the policies and with `web/src/features/session/permissions.ts`. No divergence found.
- OpenAPI: `api/openapi/v1.json` has both operations and `TeacherSubjectResult`, and no `/api/test` probe route. The generated Orval files are present. I did not re-run `gen:api` to check for a zero diff, because this review is read-only.
- Web:
  - `roleCapabilities` matches the API matrix.
  - Every nav item carries a capability.
  - `TopTabs`, `TabBar` and `MorePage` render through `visibleNavItems`, `tabBarItems` and `overflowItems`.
  - `guards.ts` is unchanged.
  - No new visual literals.
- Independent runs:
  - `dotnet build api/`: succeeded, 0 warnings, 0 errors.
  - `dotnet test api/`: 209 of 209 passed.
  - `dotnet format --verify-no-changes`: violations only in `core-libraries`.
  - `npm --prefix web run build`: exit 0.
  - `npm --prefix web test -- --run`: 24 files, 113 of 113 tests passed.
  - `npx eslint . --max-warnings=0`: exit 0.
  - Guard grep: nothing (the only `IgnoreQueryFilters` is the plan-sanctioned one in I16).

## Test quality
- `PermissionMatrixPolicyTests` (I9): 54 hard-coded rows resolved through the real `IAuthorizationService`. It fails on any role drift. Strong.
- `SubjectScopeEndpointTests` (I1–I8): real login JWT → policy → MediatR → behaviour → Postgres. They prove the role claim survives inbound mapping and that scoping, the admin/student bypass and immediate unassignment all work. Strong.
- `TeacherSubjectsEndpointTests` (I10–I18) cover every status the endpoints return. I14 also asserts that nothing was persisted, and I18 proves the filtered index. Strong.
- `SubjectScopeBehaviourTests` (T22–T28): the lookup stub is keyed to the exact teacher and subject ids, and the fail-closed null-role case is covered. Strong.
- `AssignTeacherSubjectHandlerTests` and `UnassignTeacherSubjectHandlerTests`: every throw has type, code and `DidNotReceive` checks. The success paths assert `Received(1)` and the resulting entity state. Only the Unassign lookup predicate is unconstrained (see Non-blocking).
- Validator and domain tests: each rule and branch has a failing case. None is vacuous.
- Web: W4 is a real drift guard over the whole nav config; W5 uses a synthetic mis-placed item; W6 checks the rendered DOM.
