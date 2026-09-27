# Implementation — Role-based authorisation and teacher subject scoping (#57, E1.S4)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Subjects/Subject.cs | 18 | Minimal `Subject : AuditEntity` (Name), `Create(name, createdBy)` |
| api/Elmanhg.Domain/Subjects/ISubjectRepository.cs | 5 | `IRepository<Subject>` |
| api/Elmanhg.Domain/Teachers/TeacherSubject.cs | 36 | `Create(User, Subject, Guid)` with `USER_NOT_TEACHER` guard; `Unassign(Guid)` |
| api/Elmanhg.Domain/Teachers/ITeacherSubjectRepository.cs | 8 | adds `IsAssignedAsync` |
| api/Elmanhg.Application/Shared/Authorization/ISubjectScopedRequest.cs | 6 | opt-in marker |
| api/Elmanhg.Application/Shared/Authorization/SubjectScopeBehaviour.cs | 38 | MediatR behaviour, the 5 plan steps in order, fails closed |
| api/Elmanhg.Application/Teachers/Shared/TeacherSubjectResult.cs | 3 | admin result |
| api/Elmanhg.Application/Teachers/AssignTeacherSubject/AssignTeacherSubjectCommand.cs | 12 | command + `IAuditableCommand` |
| api/Elmanhg.Application/Teachers/AssignTeacherSubject/AssignTeacherSubjectValidator.cs | 14 | Guid `ValidateRequired` x2 |
| api/Elmanhg.Application/Teachers/AssignTeacherSubject/AssignTeacherSubjectHandler.cs | 46 | 401 → 404 user → 404 subject → 409 → domain create → Add + one Save |
| api/Elmanhg.Application/Teachers/UnassignTeacherSubject/UnassignTeacherSubjectCommand.cs | 11 | command + `IAuditableCommand` |
| api/Elmanhg.Application/Teachers/UnassignTeacherSubject/UnassignTeacherSubjectValidator.cs | 14 | same rules |
| api/Elmanhg.Application/Teachers/UnassignTeacherSubject/UnassignTeacherSubjectHandler.cs | 28 | 401 → 404 not assigned → `Unassign` → one Save |
| api/Elmanhg.Infrastructure/Subjects/SubjectRepository.cs | 7 | base repo |
| api/Elmanhg.Infrastructure/Teachers/TeacherSubjectRepository.cs | 15 | `IsAssignedAsync` using `_dbSet.AnyAsync` |
| api/Elmanhg.Infrastructure/Migrations/20260927221722_AddTeacherSubjectScoping.cs | 91 | creates 2 tables, 2 Restrict FKs, filtered unique index. `Drop` appears only in `Down` |
| api/Elmanhg.Infrastructure/Migrations/20260927221722_AddTeacherSubjectScoping.Designer.cs | 788 | generated |
| api/Elmanhg.Api/Authorization/PermissionMatrixPolicies.cs | 35 | 18 capability policies via `RequireRole(nameof(UserRole.X))` |
| api/Elmanhg.Api/Controllers/Teachers/TeachersController.cs | 33 | POST/DELETE `api/teachers/{teacherId:guid}/subjects/{subjectId:guid}`, `UsersManage` |
| api/Elmanhg.Tests/Domain/Subjects/SubjectTests.cs | 20 | T2 |
| api/Elmanhg.Tests/Domain/Teachers/TeacherSubjectTests.cs | 61 | T3–T6 |
| api/Elmanhg.Tests/Application/Features/Teachers/AssignTeacherSubject/AssignTeacherSubjectHandlerTests.cs | 97 | T7–T12 |
| api/Elmanhg.Tests/Application/Features/Teachers/AssignTeacherSubject/AssignTeacherSubjectValidatorTests.cs | 32 | T13–T15 |
| api/Elmanhg.Tests/Application/Features/Teachers/UnassignTeacherSubject/UnassignTeacherSubjectHandlerTests.cs | 65 | T16–T18 |
| api/Elmanhg.Tests/Application/Features/Teachers/UnassignTeacherSubject/UnassignTeacherSubjectValidatorTests.cs | 32 | T19–T21 |
| api/Elmanhg.Tests/Application/Features/Shared/Authorization/SubjectScopeBehaviourTests.cs | 112 | T22–T28 + `ScopedProbeRequest` / `UnscopedProbeRequest` |
| api/Elmanhg.Tests/Integration/Infrastructure/SubjectScopeProbe/SubjectScopeProbeController.cs | 26 | test-only probe controller |
| api/Elmanhg.Tests/Integration/Infrastructure/SubjectScopeProbe/SubjectScopeProbeRequests.cs | 21 | probe result, query, command and 2 handlers |
| api/Elmanhg.Tests/Integration/Authorization/ScopeTestData.cs | 57 | seed and sign-in helpers (real JWT through `/api/auth/login/email`) |
| api/Elmanhg.Tests/Integration/Authorization/SubjectScopeEndpointTests.cs | 130 | I1–I8 |
| api/Elmanhg.Tests/Integration/Authorization/PermissionMatrixPolicyTests.cs | 49 | I9 (54 hard-coded rows) |
| api/Elmanhg.Tests/Integration/Teachers/TeacherSubjectsEndpointTests.cs | 156 | I10–I18 |
| api/Elmanhg.Tests/Integration/Composition/EndpointAuthorizationTests.cs | 28 | I19 |
| web/src/features/session/permissions.ts | 55 | `capabilities`, `Capability`, `roleCapabilities`, `can` |
| web/src/features/session/permissions.test.ts | 18 | W1–W3 |
| web/src/features/shell/navConfig.test.ts | 29 | W4–W5 |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/SharedKernel/DefaultCodes.cs | 18 constants under `// CAPABILITIES (PRD §16)`. `AuthenticatedUser` kept |
| api/Elmanhg.Domain/Identity/User.cs | `CreateTeacher(displayName, email)` |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | `// TEACHERS` `UserNotTeacher` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | `// SUBJECTS & TEACHERS`, 6 constants |
| api/Elmanhg.Application/DependencyInjection.cs | registers `SubjectScopeBehaviour<,>` after validators (+2 usings) |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | 2 DbSets, `ConfigureSubjects` / `ConfigureTeacherSubjects`, 2 query-filter lines |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | 2 repository registrations |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated by `dotnet ef` |
| api/Elmanhg.Api/Program.cs | `.AddPermissionMatrixPolicies()` chained onto the existing line, plus one `using`. Middleware untouched |
| api/Elmanhg.Api/Resources/Messages.en.resx, Messages.ar.resx | 7 keys each |
| api/openapi/v1.json | regenerated by `dotnet build` (+106 lines: Teachers paths and `TeacherSubjectResult`) |
| postman/elmanhg.postman_collection.json | `Teachers` folder (Assign subject POST, Unassign subject DELETE), collection bearer auth, plus `teacherId`/`subjectId` variables |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | test assembly added as an MVC application part and a MediatR source |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | I20: third `_AddTeacherSubjectScoping` element |
| api/Elmanhg.Tests/Domain/Identity/UserTests.cs | T1 added |
| web/src/features/session/index.ts | exports `can`, `roleCapabilities`, `Capability` |
| web/src/features/shell/navConfig.ts | `capability` on `NavItem` and every item. `visibleNavItems` added. `tabBarItems` / `overflowItems` now take `(role, nav)` |
| web/src/features/shell/components/TopTabs.tsx | renders `visibleNavItems(role, navByRole[role])` |
| web/src/features/shell/components/TabBar.tsx | `tabBarItems(role, nav)`, plus the type fix in Deviations |
| web/src/features/shell/pages/MorePage.tsx | `overflowItems(role, navByRole[role])` |
| web/src/features/shell/components/AppShell.test.tsx | W6 added. No existing test changed |
| web/src/shared/api/generated/** | `npm run gen:api` output: new `teachers/teachers.ts`, `teachers/teachers.msw.ts`, `model/teacherSubjectResult.ts`, `zod/teachers/teachers.zod.ts`; updated `index.ts`, `model/index.ts`, `zod/index.zod.ts` |
| docs/PRD.md §16 | 3 rows inserted where the plan says |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `TabBar.tsx`: only change the call to `tabBarItems(role, nav)` | `NavItem.capability` is now required. The synthetic `More` item in TabBar has no capability, so `tsc` failed with TS2322 | Changed the local annotation to `const items: readonly Omit<NavItem, 'capability'>[]` in the same file. `More` is a shell affordance, not a §16 destination, so it gets no capability |
| Postman requests use `{{teacherId}}` / `{{subjectId}}` | These collection variables did not exist | Added both as empty collection variables next to the existing ones |

## Build & test
- `dotnet build api/Elmanhg.slnx`: **Build succeeded, 0 Error(s)**. The first full build showed 9 warnings, all in `core-libraries` (Core.Validation, Core.OTP, Core.Notifications) and all pre-existing. There are 0 warnings in the Elmanhg.* projects.
- `dotnet ef migrations add AddTeacherSubjectScoping -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`: Done.
- `dotnet test api/` (Docker 29.6.2 running, Testcontainers): **Passed! total 209, failed 0, succeeded 209, skipped 0**.
- A namespace-filtered run of the new and affected classes (`Elmanhg.Tests.exe -namespace …`): **Total 100, Errors 0, Failed 0, Skipped 0**. That is 54 matrix rows + I1–I8 + I10–I18 + I19 + pipeline composition + T2–T28.
- `dotnet format api/Elmanhg.slnx --verify-no-changes`: all reported violations are in `api/core-libraries/**` (pre-existing whitespace). Filtering those out leaves no violations in Elmanhg.* files.
- Guard grep over the `.cs` diff (`DateTime.Now|UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`, `async void`, `Roles =`): no output.
- `npm run gen:api`: regenerated (orval v8.38.0).
- `npx tsc -b --noEmit`: exit 0.
- `npx eslint . --max-warnings=0`: exit 0.
- `npx prettier --check .`: **exit 1, "Code style issues found in 121 files"**. The cause is CRLF working copies from `core.autocrlf=true` on this Windows checkout; untouched files such as `vite.config.ts` and `tsconfig.node.json` fail the same way. `npx prettier --check --end-of-line auto .` gives "All matched files use Prettier code style!" (exit 0), and the index copies are LF.
- `npm --prefix web test -- --run`: **24 files, 113 tests passed**, exit 0.
- `npx vitest run --coverage`: 113 passed. All files 91.03 % statements, 79.5 % branches.
- `npm --prefix web run build`: `tsc -b && vite build`, built, exit 0.

## Notes for review
- `SubjectScopeBehaviour` reads `ClaimTypes.Role` through `ICurrentUserService.GetClaim`. I1–I8 show that the role claim survives JwtBearer inbound mapping with a real login token (D16).
- Registration order is audit → validation → subject scope → handler (D14). This is not asserted by a test beyond `PipelineCompositionTests`; the plan did not ask for one.
- `TeacherSubject.Unassign` relies on Core `Entity.SoftDelete()`, which sets `DeletedAt = null` (vendored Core behaviour, left unchanged).
- I5 also asserts a 200 before the unassign, so the later 403 is shown to come from the unassignment and not from a bad token. I18 asserts that the intermediate assign and unassign each return 200.
- Test files `TeacherSubjectsEndpointTests` (156 lines), `SubjectScopeEndpointTests` (130) and `SubjectScopeBehaviourTests` (112) go over the ~100-line guide, which I read as covering production code. Every production file is ≤ 46 lines, except the generated migration designer and `navConfig.ts` (153 lines after prettier, under the react 200-line limit).
- `AssignTeacherSubjectHandler` loads the subject with `asNoTracking: true` and passes it to the domain factory, which only reads `subject.Id`. No FK navigation is attached.
- No commit, push or branch switch was made. The `.process/` files stay untracked as they were.
