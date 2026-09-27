# Plan — Role-based authorisation and teacher subject scoping (#57, E1.S4)

## Goal
The PRD §16 permission matrix is enforced on the server. Every capability is a named policy in `DefaultCodes`, bound to roles in one place. An admin can assign subjects to a teacher and unassign them. Any MediatR request that carries a subject id and opts in with `ISubjectScopedRequest` is rejected with 403 `SUBJECT_OUT_OF_SCOPE` when the caller is a teacher who is not assigned to that subject. The check runs against the database, so an unassignment takes effect on the teacher's next request. The web shells show each role only the destinations its capabilities allow, driven by the same matrix. Later stories (content #60+, validation queue, Ask-a-Teacher, AI grade review) attach a policy constant and `ISubjectScopedRequest` and inherit the enforcement.

## Scope
**In:**
- 18 capability policies (PRD §16 rows plus 3 rows implied by §11/§12/§17.13) in `DefaultCodes`, registered in one extension, and a guard test that every controller action declares a policy or `[AllowAnonymous]`.
- A minimal `Subject` aggregate (Id, Name) and `TeacherSubject` (TeacherId, SubjectId) with FKs, a filtered unique index and a migration.
- `User.CreateTeacher` factory.
- Admin commands `AssignTeacherSubject` / `UnassignTeacherSubject` with endpoints, audit opt-in and postman entries.
- `SubjectScopeBehaviour` (MediatR pipeline) plus the `ISubjectScopedRequest` marker.
- Integration tests through a test-only probe controller proving that a physics teacher cannot read or mutate math content.
- Web: a `permissions.ts` capability matrix. The shell nav is filtered by it, and a config test fails on drift.

**Out:**
- Subject/Unit CRUD and ordering (#60 extends `Subject`).
- Teacher invite (the "Student and teacher administration" story will reuse `User.CreateTeacher`).
- Admin users UI with assignment checkboxes (same story).
- A "list a teacher's subjects" query (the first story that renders it adds it).
- Real subject-scoped content endpoints (their own stories).
- Web role route guards: already built in #55 (`requireRole`) and left unchanged.

**Deferred:** None. Everything in the story is buildable offline.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | How to model Subject before #60 | Create a **minimal `Subject` aggregate now**: `Id` and `Name` (plain `string`), with a `Subjects` table. `TeacherSubject.SubjectId` is a real FK. No subject endpoints. #60 adds `Order`, units and CRUD by extending this class. | Assigning must reject unknown subject ids (404) instead of storing orphan rows or tripping a FK 500. A FK added later would need a data-cleaning migration. Content is Arabic-only (PRD §14, "Language"), and the skill says to use plain `string` until a field is genuinely bilingual. |
| D2 | Base class for Subject / TeacherSubject | `AuditEntity(Guid id, Guid? createdBy)`, not `AggregateRoot`. | The vendored `AggregateRoot(Guid id)` has no audit fields (`api/core-libraries/Core.DDD/Entities/AggregateRoot.cs`). `CoreDbContext` does not stamp audit fields, so domain methods set them. |
| D3 | TeacherSubject lifecycle | Assign creates a row. Unassign calls `SoftDelete()` and stamps `UpdatedBy`/`UpdationDate`. There is a unique index on `(TeacherId, SubjectId)` filtered `"IsDeleted" = false`, so re-assigning after an unassign inserts a new row. | Every entity is `ISoftDeletable`. The filtered index keeps the history and still blocks live duplicates. |
| D4 | Assigning an already-assigned pair | `ConflictCoreException(TEACHER_SUBJECT_ALREADY_ASSIGNED)` → 409. | The skill's duplicate pattern. It is explicit and testable. |
| D5 | Unassigning a pair that is not assigned | `NotFoundCoreException(TEACHER_SUBJECT_NOT_ASSIGNED)` → 404. | Mirrors the not-found pattern. |
| D6 | Assign target is not a teacher | Domain guard in `TeacherSubject.Create` throws `BusinessRuleViolationCoreException(USER_NOT_TEACHER)` → 400. The code goes in the **domain** `ErrorCodes`. | Mirrors `User.Suspend()`, which throws `BusinessRuleViolationCoreException` with `Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`. `BusinessRuleViolationException` does not exist in the vendored Core. |
| D7 | Policy style | Capability policies, not role policies. Each `DefaultCodes` constant maps to the roles allowed by §16 through `policy.RequireRole(nameof(UserRole.X), …)`. They are registered in `Elmanhg.Api/Authorization/PermissionMatrixPolicies.cs`, which is chained onto the existing `AddAuthorizationBuilder()` call in `Program.cs`. | Skill §7.1 forbids `[Authorize(Roles=…)]` and role strings on actions. Morabh has no policy registry (it uses `[Authorize(Roles = "Admin")]` in `Morabh.APIs/Controllers/*`, which is prohibited here), so this is **new, with no Morabh equivalent**. The registration mirrors the extension style of `Elmanhg.Api/RateLimiting/AuthRateLimiting.cs`. |
| D8 | Rows not literally in §16 | Add `AskTeacher.Submit` (Student, §12), `Subscription.Manage` (Student, §11) and `AuditLog.View` (Admin, §17.13 plus the prototype admin nav). Also add these rows to `docs/PRD.md` §16. | Existing shell destinations need a capability. Without the PRD edit, the code and the doc would disagree (docs-sync). |
| D9 | "Policies applied to every endpoint" | Every existing action already has a policy or `[AllowAnonymous]`. Add an integration test that enumerates every controller endpoint from `EndpointDataSource` and fails if any lacks both. No global fallback policy. | A fallback policy would 401 `/openapi` and `/scalar` and would force `Program.cs` middleware or mapping changes. The test enforces the rule for every future controller. |
| D10 | How the scope filter decides who is scoped | `SubjectScopeBehaviour` passes through non-scoped requests. It also passes through callers whose role claim is `Student` or `Admin`. **Everyone else, including a missing role claim, must hold a live `TeacherSubject` row.** | This fails closed. Students and admins are not subject-scoped by §16; their access is decided by the endpoint policy. |
| D11 | Out-of-scope HTTP status | `ForbiddenCoreException(SUBJECT_OUT_OF_SCOPE)` → 403. It is the same for existing and non-existent subject ids. | Subject existence is public (students browse the tree), so 403 leaks nothing. The check never loads the subject, so a missing subject also returns 403. There is no existence oracle. |
| D12 | Source of a teacher's subjects | Database lookup per scoped request (`ITeacherSubjectRepository.IsAssignedAsync`). Not a JWT claim. | Access tokens live for hours; an unassignment must take effect immediately. |
| D13 | Contract for later stories whose target entity carries its own subject (Question, TeacherThread) | The request implements `ISubjectScopedRequest` with a `SubjectId`. The handler must load the target and throw `NotFoundCoreException(<Entity>NotFound)` when `entity.SubjectId != request.SubjectId`. Handlers that derive the subject only from an entity call `ITeacherSubjectRepository.IsAssignedAsync` themselves. | The behaviour only sees request data. This closes the "own subject id plus foreign entity id" bypass. It is stated here so later plans cite it; nothing in this story needs it. |
| D14 | Behaviour order | Register `SubjectScopeBehaviour<,>` in `AddApplication()`. Because it is registered after `AddCoreCQRS()`, the pipeline runs audit → validation → subject scope → handler. | Malformed input gets 422 before the scope check hits the database. |
| D15 | Test endpoint for "read or mutate math content" | A test-assembly-only `SubjectScopeProbeController` (`GET` / `POST api/test/subjects/{subjectId:guid}/content`) sends test-assembly `ISubjectScopedRequest` requests. `ApiFactory` adds the test assembly as an MVC application part and a MediatR registration source. | No content endpoint exists yet, and Morabh has no test-endpoint pattern. The probe exercises the real JWT → policy → MediatR → behaviour → DB path. Build-time OpenAPI (`api/openapi/v1.json`) never sees it. |
| D16 | Auth in integration tests | Use a real JWT from `POST /api/auth/login/email` for users seeded with `AuthTestClient.SeedUserAsync`. | This proves that the `ClaimTypes.Role` claim written by `UserClaimsExtensions` survives JwtBearer inbound mapping, which `RequireRole` and the behaviour both rely on. |
| D17 | Assign/unassign audit | Both commands implement `IAuditableCommand` (`AuditResourceType = "Teacher"`, `AuditResourceId = TeacherId`). | PRD §17.13 and skill §5.6. Auditing is pipeline-side; handlers are untouched. |
| D18 | `User.CreateTeacher` shape | `CreateTeacher(string displayName, string email)`, mirroring `CreateAdmin`: `Email` = `UserName`, `EmailConfirmed = true`, `Role = Teacher`. | Tests must create teachers through a domain factory. Teachers are admin-provisioned (PRD §10.4 invite). |
| D19 | Web nav filtering | Keep `navByRole` (layout is per role). Add a `capability` to each `NavItem`. `TopTabs`, `TabBar` and `MorePage` render `visibleNavItems(role, nav)`, which drops items the role cannot use. Route guards are unchanged. | The shells already split nav by role. What was missing is binding the nav to the §16 matrix, so a mis-placed destination is hidden and caught by a config test. Nothing is duplicated. |
| D20 | Web capability location | `src/features/session/permissions.ts`, exported through the session barrel. | The session feature owns `Role`. Shell imports only through the barrel (react skill §1). |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/SharedKernel/DefaultCodes.cs` | Add 18 constants (table in API surface). Keep `AuthenticatedUser`. |
| `api/Elmanhg.Domain/Identity/User.cs` | Add `public static User CreateTeacher(string displayName, string email)` (body in Domain behaviour). |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Add a `// TEACHERS` group: `UserNotTeacher = "USER_NOT_TEACHER"`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add a `// SUBJECTS & TEACHERS` group with the 6 constants in Error codes (all except `UserNotTeacher`). |
| `api/Elmanhg.Application/DependencyInjection.cs` | After `AddValidatorsFromAssembly`, add `services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SubjectScopeBehaviour<,>));`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Add `DbSet<Subject> Subjects { get; set; }` and `DbSet<TeacherSubject> TeacherSubjects { get; set; }`. Add `ConfigureSubjects` / `ConfigureTeacherSubjects` calls in `OnModelCreating` (after `ConfigureUsers`). Add 2 query-filter lines (see mapping below). |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<ISubjectRepository, SubjectRepository>(); services.AddScoped<ITeacherSubjectRepository, TeacherSubjectRepository>();` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add`. |
| `api/Elmanhg.Api/Program.cs` | IDENTITY region: `builder.Services.AddAuthorizationBuilder().AddPolicy(DefaultCodes.AuthenticatedUser, policy => policy.RequireAuthenticatedUser()).AddPermissionMatrixPolicies();` plus `using Elmanhg.Api.Authorization;`. Nothing else changes (middleware order is fixed). |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | 7 keys each (strings in Error codes). |
| `api/openapi/v1.json` | Regenerated by `dotnet build` (build-time document). Never hand-edit it. |
| `postman/elmanhg.postman_collection.json` | New top-level folder `Teachers` with `Assign subject` (POST) and `Unassign subject` (DELETE) on `/api/teachers/{{teacherId}}/subjects/{{subjectId}}`, using the same base-URL variable and bearer-auth style as the existing `Auth` > `Logout` item. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | In `ConfigureTestServices`, after the SMS lines: `services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);` and `services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(ApiFactory).Assembly));`. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | **Modify** `Migrate_FreshDatabase_LeavesNoPendingMigrations`: add a third `SatisfyRespectively` element, `third => third.Should().EndWith("_AddTeacherSubjectScoping")`. This is an intentional behaviour change (new migration). |
| `api/Elmanhg.Tests/Domain/Identity/UserTests.cs` | Add test T1. |
| `web/src/features/session/index.ts` | `export { can, roleCapabilities, type Capability } from './permissions';` |
| `web/src/features/shell/navConfig.ts` | `NavItem` gains `capability: Capability`. Every item gets its capability (map in Files to create #W1 notes). Add `visibleNavItems`. `tabBarItems` and `overflowItems` take `(role: Role, nav: RoleNav)` and work on `visibleNavItems(role, nav)`. |
| `web/src/features/shell/components/TopTabs.tsx` | Map over `visibleNavItems(role, navByRole[role])` instead of `navByRole[role].items`. |
| `web/src/features/shell/components/TabBar.tsx` | `tabBarItems(role, nav)`. |
| `web/src/features/shell/pages/MorePage.tsx` | `overflowItems(role, navByRole[role])`. |
| `web/src/features/shell/components/AppShell.test.tsx` | Add test W6 (no existing test modified). |
| `web/src/shared/api/generated/**` | Regenerated by `npm run gen:api` after the API build: `teachers/teachers.ts`, `teachers/teachers.msw.ts`, `model/teacherSubjectResult.ts`, `model/index.ts`, `index.ts`, `zod/teachers/teachers.zod.ts`, `zod/index.zod.ts` (whatever Orval emits). Commit exactly the output; never hand-edit. |
| `docs/PRD.md` §16 | Insert three rows into the matrix table. After "Reply to Ask a Teacher", add `\| Ask a Teacher (submit) \| ✓ \| – \| – \|`. After "View any student's progress", add `\| Manage own subscription \| ✓ \| – \| – \|`. After "Manage users / teachers", add `\| View audit log \| – \| – \| ✓ \|`. |

## Files to create

### Domain
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Subjects/Subject.cs` | class | `namespace Elmanhg.Domain.Subjects; public class Subject : AuditEntity`. `public string Name { get; private set; } = string.Empty;` · `private Subject(Guid id, Guid? createdBy) : base(id, createdBy) { }` · `public static Subject Create(string name, Guid createdBy)` → `new Subject(Guid.NewGuid(), createdBy) { Name = name }`. |
| 2 | `api/Elmanhg.Domain/Subjects/ISubjectRepository.cs` | interface | `public interface ISubjectRepository : IRepository<Subject> { }` |
| 3 | `api/Elmanhg.Domain/Teachers/TeacherSubject.cs` | class | `namespace Elmanhg.Domain.Teachers; public class TeacherSubject : AuditEntity`. `public Guid TeacherId { get; private set; }` · `public Guid SubjectId { get; private set; }` · `private TeacherSubject(Guid id, Guid? createdBy) : base(id, createdBy) { }` · `public static TeacherSubject Create(User teacher, Subject subject, Guid createdBy)` · `public void Unassign(Guid unassignedBy)`. Bodies are in Domain behaviour. |
| 4 | `api/Elmanhg.Domain/Teachers/ITeacherSubjectRepository.cs` | interface | `public interface ITeacherSubjectRepository : IRepository<TeacherSubject> { Task<bool> IsAssignedAsync(Guid teacherId, Guid subjectId, CancellationToken cancellationToken); }`. This is an existence question, so it uses `AnyAsync` inside the repository (skill §6.6). |

### Application
| # | Path | Type | Contract |
|---|------|------|----------|
| 5 | `api/Elmanhg.Application/Shared/Authorization/ISubjectScopedRequest.cs` | interface | `namespace Elmanhg.Application.Shared.Authorization; public interface ISubjectScopedRequest { Guid SubjectId { get; } }` |
| 6 | `api/Elmanhg.Application/Shared/Authorization/SubjectScopeBehaviour.cs` | sealed class | `public sealed class SubjectScopeBehaviour<TRequest, TResponse>(ICurrentUserService currentUserService, ITeacherSubjectRepository teacherSubjectRepository) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull`. `Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)` steps: **1** `if (request is not ISubjectScopedRequest scoped)` → `return await next(cancellationToken).ConfigureAwait(false);` **2** `var role = currentUserService.GetClaim(ClaimTypes.Role);` if `role is nameof(UserRole.Student) or nameof(UserRole.Admin)` → return `await next(...)`. **3** `if (currentUserService.UserId is null)` → `throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);` **4** `if (!await teacherSubjectRepository.IsAssignedAsync(currentUserService.UserId.Value, scoped.SubjectId, cancellationToken).ConfigureAwait(false))` → `throw new ForbiddenCoreException(ErrorCodes.SubjectOutOfScope);` **5** `return await next(cancellationToken).ConfigureAwait(false);`. No try/catch. |
| 7 | `api/Elmanhg.Application/Teachers/Shared/TeacherSubjectResult.cs` | sealed record | `namespace Elmanhg.Application.Teachers.Shared; public sealed record TeacherSubjectResult(Guid TeacherId, Guid SubjectId, DateTimeOffset AssignedAt);`. Admin-facing, with no localized fields. `AssignedAt = teacherSubject.CreationDate`. Mapping is inline in the handler (trivial, so no generator). |
| 8 | `api/Elmanhg.Application/Teachers/AssignTeacherSubject/AssignTeacherSubjectCommand.cs` | sealed record | `public sealed record AssignTeacherSubjectCommand(Guid TeacherId, Guid SubjectId) : IRequest<TeacherSubjectResult>, IAuditableCommand { public string AuditAction => "AssignTeacherSubject"; public string AuditResourceType => "Teacher"; public Guid? AuditResourceId => TeacherId; }`. It deliberately does **not** implement `ISubjectScopedRequest`: admin-only, and the SubjectId is the target, not a scope. |
| 9 | `.../AssignTeacherSubject/AssignTeacherSubjectValidator.cs` | sealed class | `AbstractValidator<AssignTeacherSubjectCommand>`: `RuleFor(x => x.TeacherId).ValidateRequired(ErrorCodes.TeacherIdRequired);` `RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);` (the `Guid` overload in `Core.Validation/Extensions/RequiredValidationExtensions.cs`). |
| 10 | `.../AssignTeacherSubject/AssignTeacherSubjectHandler.cs` | sealed class | `public sealed class AssignTeacherSubjectHandler(UserManager<User> userManager, ISubjectRepository subjectRepository, ITeacherSubjectRepository teacherSubjectRepository, ICurrentUserService currentUserService) : IRequestHandler<AssignTeacherSubjectCommand, TeacherSubjectResult>`. Steps: **1** `currentUserService.UserId` null or default → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`. **2** `var teacher = await userManager.FindByIdAsync(request.TeacherId.ToString()).ConfigureAwait(false);` null → `NotFoundCoreException(ErrorCodes.UserNotFound)`. **3** `var subject = await subjectRepository.GetByIdAsync(request.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);` null → `NotFoundCoreException(ErrorCodes.SubjectNotFound)`. **4** `IsAssignedAsync(request.TeacherId, request.SubjectId, ...)` true → `ConflictCoreException(ErrorCodes.TeacherSubjectAlreadyAssigned)`. **5** `var teacherSubject = TeacherSubject.Create(teacher, subject, currentUserService.UserId.Value);` (the domain throws `USER_NOT_TEACHER`). **6** `AddAsync` then `SaveChangesAsync` (once). **7** return `new TeacherSubjectResult(teacherSubject.TeacherId, teacherSubject.SubjectId, teacherSubject.CreationDate)`. |
| 11 | `api/Elmanhg.Application/Teachers/UnassignTeacherSubject/UnassignTeacherSubjectCommand.cs` | sealed record | `public sealed record UnassignTeacherSubjectCommand(Guid TeacherId, Guid SubjectId) : IRequest, IAuditableCommand` with the same three audit members, `AuditAction => "UnassignTeacherSubject"`. |
| 12 | `.../UnassignTeacherSubject/UnassignTeacherSubjectValidator.cs` | sealed class | Same two rules and codes as #9. |
| 13 | `.../UnassignTeacherSubject/UnassignTeacherSubjectHandler.cs` | sealed class | `public sealed class UnassignTeacherSubjectHandler(ITeacherSubjectRepository teacherSubjectRepository, ICurrentUserService currentUserService) : IRequestHandler<UnassignTeacherSubjectCommand>`. Steps: **1** user guard → 401 `UserNotAuthenticated`. **2** `var teacherSubject = await teacherSubjectRepository.FirstOrDefaultAsync(x => x.TeacherId == request.TeacherId && x.SubjectId == request.SubjectId, cancellationToken).ConfigureAwait(false);` (tracking). null → `NotFoundCoreException(ErrorCodes.TeacherSubjectNotAssigned)`. **3** `teacherSubject.Unassign(currentUserService.UserId.Value);` **4** `SaveChangesAsync` once. |

### Infrastructure
| # | Path | Type | Contract |
|---|------|------|----------|
| 14 | `api/Elmanhg.Infrastructure/Subjects/SubjectRepository.cs` | class | `public class SubjectRepository(AppDbContext context) : Repository<Subject>(context), ISubjectRepository { }` |
| 15 | `api/Elmanhg.Infrastructure/Teachers/TeacherSubjectRepository.cs` | class | `public class TeacherSubjectRepository(AppDbContext context) : Repository<TeacherSubject>(context), ITeacherSubjectRepository`. `IsAssignedAsync` → `return await _dbSet.AnyAsync(x => x.TeacherId == teacherId && x.SubjectId == subjectId, cancellationToken).ConfigureAwait(false);` (soft-deleted rows are excluded by the global filter). |
| 16–17 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddTeacherSubjectScoping.cs` + `.Designer.cs` | migration | Generate it with `dotnet ef migrations add AddTeacherSubjectScoping -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Review: it creates only the `Subjects` and `TeacherSubjects` tables, 2 FKs (Restrict) and the filtered unique index. No Drop or Rename. |

**AppDbContext mapping** (private static methods in `AppDbContext.cs`):
```csharp
private static void ConfigureSubjects(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Subject>(builder => builder.Property(x => x.Name).IsRequired());
}

private static void ConfigureTeacherSubjects(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<TeacherSubject>(builder =>
    {
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TeacherId, x.SubjectId }).IsUnique().HasFilter("\"IsDeleted\" = false");
    });
}
```
In `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`, add `modelBuilder.Entity<Subject>().HasQueryFilter(x => !x.IsDeleted);` and `modelBuilder.Entity<TeacherSubject>().HasQueryFilter(x => !x.IsDeleted);`.

### API
| # | Path | Type | Contract |
|---|------|------|----------|
| 18 | `api/Elmanhg.Api/Authorization/PermissionMatrixPolicies.cs` | static class | `namespace Elmanhg.Api.Authorization; public static class PermissionMatrixPolicies { public static AuthorizationBuilder AddPermissionMatrixPolicies(this AuthorizationBuilder builder) }`. There are private `const string` fields `Student = nameof(UserRole.Student)`, `Teacher = …`, `Admin = …`. The body is one fluent chain of 18 `.AddPolicy(DefaultCodes.X, policy => policy.RequireRole(...))` calls, one per row of the API surface table, returning `builder`. |
| 19 | `api/Elmanhg.Api/Controllers/Teachers/TeachersController.cs` | class | `[ApiController] [Route("api/teachers")] [Authorize] public class TeachersController(IMediator mediator) : ControllerBase`. It has two actions (API surface). There is no Requests.cs because the route params map straight to the command. |

### Tests (api)
| # | Path |
|---|------|
| 20 | `api/Elmanhg.Tests/Domain/Subjects/SubjectTests.cs` |
| 21 | `api/Elmanhg.Tests/Domain/Teachers/TeacherSubjectTests.cs` |
| 22 | `api/Elmanhg.Tests/Application/Features/Teachers/AssignTeacherSubject/AssignTeacherSubjectHandlerTests.cs` |
| 23 | `api/Elmanhg.Tests/Application/Features/Teachers/AssignTeacherSubject/AssignTeacherSubjectValidatorTests.cs` |
| 24 | `api/Elmanhg.Tests/Application/Features/Teachers/UnassignTeacherSubject/UnassignTeacherSubjectHandlerTests.cs` |
| 25 | `api/Elmanhg.Tests/Application/Features/Teachers/UnassignTeacherSubject/UnassignTeacherSubjectValidatorTests.cs` |
| 26 | `api/Elmanhg.Tests/Application/Features/Shared/Authorization/SubjectScopeBehaviourTests.cs`. This file also declares two test requests: `public sealed record ScopedProbeRequest(Guid SubjectId) : IRequest<Unit>, ISubjectScopedRequest;` and `public sealed record UnscopedProbeRequest : IRequest<Unit>;`. |
| 27 | `api/Elmanhg.Tests/Integration/Infrastructure/SubjectScopeProbe/SubjectScopeProbeController.cs`. `[ApiController] [Route("api/test/subjects")] [Authorize] public class SubjectScopeProbeController(IMediator mediator) : ControllerBase`. `[HttpGet("{subjectId:guid}/content")] [Authorize(Policy = DefaultCodes.ContentBrowse)] Read(Guid subjectId, CancellationToken)` → `Ok(await mediator.Send(new ReadSubjectContentProbeQuery(subjectId), cancellationToken))`. `[HttpPost("{subjectId:guid}/content")] [Authorize(Policy = DefaultCodes.QuestionsValidate)] Mutate(...)` → `Ok(await mediator.Send(new MutateSubjectContentProbeCommand(subjectId), cancellationToken))`. |
| 28 | `api/Elmanhg.Tests/Integration/Infrastructure/SubjectScopeProbe/SubjectScopeProbeRequests.cs`. `public sealed record SubjectContentProbeResult(Guid SubjectId);` · `public sealed record ReadSubjectContentProbeQuery(Guid SubjectId) : IRequest<SubjectContentProbeResult>, ISubjectScopedRequest;` · `public sealed record MutateSubjectContentProbeCommand(Guid SubjectId) : IRequest<SubjectContentProbeResult>, ISubjectScopedRequest;` · two `sealed class` handlers, each returning `new SubjectContentProbeResult(request.SubjectId)`. |
| 29 | `api/Elmanhg.Tests/Integration/Authorization/ScopeTestData.cs`. A static helper holding `public const string Password = "Passw0rd1";` and these methods (all take `CancellationToken`): `Task<User> SeedTeacherAsync(ApiFactory, …)` (`User.CreateTeacher("Teacher", AuthTestClient.NewEmail())` via `AuthTestClient.SeedUserAsync(..., Password, suspended: false, …)`), `SeedAdminAsync`, `SeedStudentAsync` (`User.CreateStudentWithEmail`), `Task<Guid> SeedSubjectAsync(ApiFactory, string name, …)` (adds `Subject.Create(name, Guid.NewGuid())` via an `AppDbContext` scope), `Task AssignAsync(ApiFactory, Guid teacherId, Guid subjectId, …)` (adds `TeacherSubject.Create` via an `AppDbContext` scope after loading both), and `Task<HttpClient> SignedInClientAsync(ApiFactory, User user, …)` (`AuthTestClient.Create`, `POST /api/auth/login/email`, reads `accessToken`, sets the `Authorization: Bearer` default header). |
| 30 | `api/Elmanhg.Tests/Integration/Authorization/SubjectScopeEndpointTests.cs` |
| 31 | `api/Elmanhg.Tests/Integration/Authorization/PermissionMatrixPolicyTests.cs` |
| 32 | `api/Elmanhg.Tests/Integration/Teachers/TeacherSubjectsEndpointTests.cs` |
| 33 | `api/Elmanhg.Tests/Integration/Composition/EndpointAuthorizationTests.cs` |

### Web
| # | Path | Type | Contract |
|---|------|------|----------|
| W1 | `web/src/features/session/permissions.ts` | module | `export const capabilities = ['contentBrowse','assessmentsTake','contentManage','lessonsPublish','questionsValidate','questionsChangeDifficulty','blueprintsManage','askTeacherSubmit','askTeacherReply','aiGradesOverride','progressViewOwn','progressViewAny','subscriptionManage','dashboardsView','teacherStatsViewOwn','usersManage','auditLogView','trainingDataExport'] as const; export type Capability = (typeof capabilities)[number]; export const roleCapabilities: Record<Role, readonly Capability[]>` holds exactly the role columns of the API surface table. `export function can(role: Role, capability: Capability): boolean` returns `roleCapabilities[role].includes(capability)`. |
| W2 | `web/src/features/session/permissions.test.ts` | test | Tests W1–W3. |
| W3 | `web/src/features/shell/navConfig.test.ts` | test | Tests W4–W5. |

**navConfig capability map** (W1 consumer): student: home→`contentBrowse`, progress→`progressViewOwn`, multiExam→`assessmentsTake`, ask→`askTeacherSubmit`, subscription→`subscriptionManage`. Teacher: queue→`questionsValidate`, inbox→`askTeacherReply`, stats→`teacherStatsViewOwn`. Admin: dashboard→`dashboardsView`, content→`contentManage`, questions→`contentManage`, blueprints→`blueprintsManage`, users→`usersManage`, audit→`auditLogView`, export→`trainingDataExport`.
New functions:
- `export function visibleNavItems(role: Role, nav: RoleNav): readonly NavItem[]` returns `nav.items.filter((item) => can(role, item.capability))`.
- `tabBarItems(role, nav)` uses `nav.tabBarKeys.flatMap(key => visibleNavItems(role, nav).filter(item => item.key === key))`.
- `overflowItems(role, nav)` uses `visibleNavItems(role, nav).filter(item => !nav.tabBarKeys.includes(item.key))`.

No new i18n strings.

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ErrorCodes.SubjectOutOfScope` | `SUBJECT_OUT_OF_SCOPE` | `SubjectScopeBehaviour` | `ForbiddenCoreException` | 403 |
| `ErrorCodes.SubjectNotFound` | `SUBJECT_NOT_FOUND` | `AssignTeacherSubjectHandler` | `NotFoundCoreException` | 404 |
| `ErrorCodes.TeacherSubjectAlreadyAssigned` | `TEACHER_SUBJECT_ALREADY_ASSIGNED` | `AssignTeacherSubjectHandler` | `ConflictCoreException` | 409 |
| `ErrorCodes.TeacherSubjectNotAssigned` | `TEACHER_SUBJECT_NOT_ASSIGNED` | `UnassignTeacherSubjectHandler` | `NotFoundCoreException` | 404 |
| `ErrorCodes.TeacherIdRequired` | `TEACHER_ID_REQUIRED` | both validators | validation pipeline | 422 |
| `ErrorCodes.SubjectIdRequired` | `SUBJECT_ID_REQUIRED` | both validators | validation pipeline | 422 |
| Domain `ErrorCodes.UserNotTeacher` | `USER_NOT_TEACHER` | `TeacherSubject.Create` | `BusinessRuleViolationCoreException` | 400 |
| (reused) `ErrorCodes.UserNotFound` | `USER_NOT_FOUND` | `AssignTeacherSubjectHandler` | `NotFoundCoreException` | 404 |
| (reused) `ErrorCodes.UserNotAuthenticated` | `USER_NOT_AUTHENTICATED` | both handlers and the behaviour | `UnauthorizedCoreException` | 401 |

Resource strings (Arabic without tashkeel; the existing `USER_NOT_FOUND` key stays as it is):
| Key | en | ar |
|-----|----|----|
| `SUBJECT_OUT_OF_SCOPE` | This subject is not assigned to you. | هذه المادة غير مسندة اليك. |
| `SUBJECT_NOT_FOUND` | Subject not found. | المادة غير موجودة. |
| `TEACHER_SUBJECT_ALREADY_ASSIGNED` | This subject is already assigned to the teacher. | هذه المادة مسندة بالفعل الى المعلم. |
| `TEACHER_SUBJECT_NOT_ASSIGNED` | This subject is not assigned to the teacher. | هذه المادة غير مسندة الى المعلم. |
| `TEACHER_ID_REQUIRED` | Choose a teacher. | اختر المعلم. |
| `SUBJECT_ID_REQUIRED` | Choose a subject. | اختر المادة. |
| `USER_NOT_TEACHER` | Subjects can only be assigned to teachers. | يمكن اسناد المواد الى المعلمين فقط. |

## Domain behaviour
```csharp
// User.cs
public static User CreateTeacher(string displayName, string email)
{
    var user = Create(displayName, UserRole.Teacher, userName: email);
    user.Email = email;
    user.EmailConfirmed = true;
    return user;
}

// TeacherSubject.cs
public static TeacherSubject Create(User teacher, Subject subject, Guid createdBy)
{
    if (teacher.Role != UserRole.Teacher)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.UserNotTeacher);
    }

    return new TeacherSubject(Guid.NewGuid(), createdBy)
    {
        TeacherId = teacher.Id,
        SubjectId = subject.Id,
    };
}

public void Unassign(Guid unassignedBy)
{
    SoftDelete();
    UpdatedBy = unassignedBy;
    UpdationDate = DateTimeOffset.UtcNow;
}
```
`ErrorCodes` here is `Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`. `Subject` has no mutators in this story.

## API surface
| Method | Route | Policy | Request | Response |
|--------|-------|--------|---------|----------|
| POST | `/api/teachers/{teacherId:guid}/subjects/{subjectId:guid}` (`Name = "AssignTeacherSubject"`) | `DefaultCodes.UsersManage` | route → `AssignTeacherSubjectCommand` | `200 TeacherSubjectResult` (`[ProducesResponseType<TeacherSubjectResult>(StatusCodes.Status200OK)]`) |
| DELETE | `/api/teachers/{teacherId:guid}/subjects/{subjectId:guid}` (`Name = "UnassignTeacherSubject"`) | `DefaultCodes.UsersManage` | route → `UnassignTeacherSubjectCommand` | `200` empty (`[ProducesResponseType(StatusCodes.Status200OK)]`) |

Both actions use `[FromRoute] Guid teacherId, [FromRoute] Guid subjectId, CancellationToken cancellationToken`, send the command and return `Ok(result)` or `Ok()`. Method names are `AssignSubject` and `UnassignSubject`.

**DefaultCodes + policy matrix** (S = Student, T = Teacher, A = Admin). Group the constants under `// CAPABILITIES (PRD §16)`:
| Constant | Value | S | T | A |
|---|---|:-:|:-:|:-:|
| `ContentBrowse` | `Content.Browse` | ✓ | ✓ | ✓ |
| `AssessmentsTake` | `Assessments.Take` | ✓ | – | ✓ |
| `ContentManage` | `Content.Manage` | – | – | ✓ |
| `LessonsPublish` | `Lessons.Publish` | – | – | ✓ |
| `QuestionsValidate` | `Questions.Validate` | – | ✓ | – |
| `QuestionsChangeDifficulty` | `Questions.ChangeDifficulty` | – | ✓ | ✓ |
| `BlueprintsManage` | `Blueprints.Manage` | – | – | ✓ |
| `AskTeacherSubmit` | `AskTeacher.Submit` | ✓ | – | – |
| `AskTeacherReply` | `AskTeacher.Reply` | – | ✓ | ✓ |
| `AiGradesOverride` | `AiGrades.Override` | – | ✓ | ✓ |
| `ProgressViewOwn` | `Progress.ViewOwn` | ✓ | – | – |
| `ProgressViewAny` | `Progress.ViewAny` | – | – | ✓ |
| `SubscriptionManage` | `Subscription.Manage` | ✓ | – | – |
| `DashboardsView` | `Dashboards.View` | – | – | ✓ |
| `TeacherStatsViewOwn` | `TeacherStats.ViewOwn` | – | ✓ | – |
| `UsersManage` | `Users.Manage` | – | – | ✓ |
| `AuditLogView` | `AuditLog.View` | – | – | ✓ |
| `TrainingDataExport` | `TrainingData.Export` | – | – | ✓ |

## Test plan
Assertions use FluentAssertions, which is already pinned in the repo. Pass `TestContext.Current.CancellationToken` everywhere. Throwing handler paths assert the exception type, the `ErrorCode`, and `SaveChangesAsync` `DidNotReceive()`.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T1 | `UserTests` | `CreateTeacher_Always_CreatesActiveTeacherWithConfirmedEmail` | `Role == Teacher`, `IsActive`, `UserName == Email == "t@elmanhg.test"`, `EmailConfirmed` is true |
| T2 | `SubjectTests` | `Create_Always_SetsNameAndCreator` | `Name`, `CreatedBy`, `Id` not empty, `IsDeleted` false |
| T3 | `TeacherSubjectTests` | `Create_TeacherAndSubject_LinksBoth` | `TeacherId == teacher.Id`, `SubjectId == subject.Id`, `CreatedBy == createdBy`, `IsDeleted` false |
| T4 | `TeacherSubjectTests` | `Create_StudentUser_ThrowsUserNotTeacher` | `BusinessRuleViolationCoreException` with `ErrorCode == USER_NOT_TEACHER` |
| T5 | `TeacherSubjectTests` | `Create_AdminUser_ThrowsUserNotTeacher` | same as T4 |
| T6 | `TeacherSubjectTests` | `Unassign_Always_SoftDeletesAndStampsActor` | `IsDeleted` true, `UpdatedBy == unassignedBy`, `UpdationDate >= CreationDate` |
| T7 | `AssignTeacherSubjectHandlerTests` | `Handle_ValidTeacherAndSubject_ReturnsResultAndSaves` | result `TeacherId`/`SubjectId` match; `AddAsync` received an entity with those ids; `SaveChangesAsync` `Received(1)` |
| T8 | `AssignTeacherSubjectHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException` `USER_NOT_AUTHENTICATED` |
| T9 | `AssignTeacherSubjectHandlerTests` | `Handle_TeacherNotFound_ThrowsUserNotFound` | `NotFoundCoreException` `USER_NOT_FOUND` |
| T10 | `AssignTeacherSubjectHandlerTests` | `Handle_SubjectNotFound_ThrowsSubjectNotFound` | `NotFoundCoreException` `SUBJECT_NOT_FOUND` |
| T11 | `AssignTeacherSubjectHandlerTests` | `Handle_AlreadyAssigned_ThrowsTeacherSubjectAlreadyAssigned` | `ConflictCoreException` `TEACHER_SUBJECT_ALREADY_ASSIGNED`; `AddAsync` `DidNotReceive` |
| T12 | `AssignTeacherSubjectHandlerTests` | `Handle_UserIsStudent_ThrowsUserNotTeacher` | `BusinessRuleViolationCoreException` `USER_NOT_TEACHER` |
| T13 | `AssignTeacherSubjectValidatorTests` | `Validate_ValidCommand_Passes` | `IsValid` |
| T14 | `AssignTeacherSubjectValidatorTests` | `Validate_EmptyTeacherId_FailsWithTeacherIdRequired` | error codes contain `TEACHER_ID_REQUIRED` |
| T15 | `AssignTeacherSubjectValidatorTests` | `Validate_EmptySubjectId_FailsWithSubjectIdRequired` | contains `SUBJECT_ID_REQUIRED` |
| T16 | `UnassignTeacherSubjectHandlerTests` | `Handle_Assigned_SoftDeletesAndSaves` | the returned entity has `IsDeleted` true and `UpdatedBy ==` current user; `SaveChangesAsync` `Received(1)` |
| T17 | `UnassignTeacherSubjectHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | 401 exception + code |
| T18 | `UnassignTeacherSubjectHandlerTests` | `Handle_NotAssigned_ThrowsTeacherSubjectNotAssigned` | `NotFoundCoreException` `TEACHER_SUBJECT_NOT_ASSIGNED` |
| T19 | `UnassignTeacherSubjectValidatorTests` | `Validate_ValidCommand_Passes` | `IsValid` |
| T20 | `UnassignTeacherSubjectValidatorTests` | `Validate_EmptyTeacherId_FailsWithTeacherIdRequired` | code |
| T21 | `UnassignTeacherSubjectValidatorTests` | `Validate_EmptySubjectId_FailsWithSubjectIdRequired` | code |
| T22 | `SubjectScopeBehaviourTests` | `Handle_UnscopedRequest_CallsNextWithoutLookup` | next invoked; `IsAssignedAsync` `DidNotReceive` |
| T23 | `SubjectScopeBehaviourTests` | `Handle_AdminOnScopedRequest_CallsNextWithoutLookup` | role claim `"Admin"`: next invoked; no lookup |
| T24 | `SubjectScopeBehaviourTests` | `Handle_StudentOnScopedRequest_CallsNextWithoutLookup` | role `"Student"`: same |
| T25 | `SubjectScopeBehaviourTests` | `Handle_TeacherAssignedToSubject_CallsNext` | `IsAssignedAsync(teacherId, subjectId)` returns true: next invoked |
| T26 | `SubjectScopeBehaviourTests` | `Handle_TeacherNotAssigned_ThrowsSubjectOutOfScope` | `ForbiddenCoreException` `SUBJECT_OUT_OF_SCOPE`; next not invoked |
| T27 | `SubjectScopeBehaviourTests` | `Handle_MissingRoleClaimNotAssigned_ThrowsSubjectOutOfScope` | role claim null: 403 code; next not invoked (fail-closed) |
| T28 | `SubjectScopeBehaviourTests` | `Handle_TeacherWithoutUserId_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException` `USER_NOT_AUTHENTICATED`; next not invoked |
| I1 | `SubjectScopeEndpointTests` | `Get_PhysicsTeacherOnMathContent_Returns403SubjectOutOfScope` | 403, body `code == "SUBJECT_OUT_OF_SCOPE"` |
| I2 | `SubjectScopeEndpointTests` | `Post_PhysicsTeacherOnMathContent_Returns403SubjectOutOfScope` | 403, `code` |
| I3 | `SubjectScopeEndpointTests` | `Get_PhysicsTeacherOnPhysicsContent_Returns200` | 200, body `subjectId ==` physics id |
| I4 | `SubjectScopeEndpointTests` | `Post_PhysicsTeacherOnPhysicsContent_Returns200` | 200, `subjectId` |
| I5 | `SubjectScopeEndpointTests` | `Get_AfterUnassign_Returns403SubjectOutOfScope` | assign physics, then `DELETE /api/teachers/{t}/subjects/{physics}` as admin (200), then the teacher's GET physics returns 403 `SUBJECT_OUT_OF_SCOPE` with the same access token |
| I6 | `SubjectScopeEndpointTests` | `Get_AdminOnMathContent_Returns200` | 200 (admin is not subject-scoped) |
| I7 | `SubjectScopeEndpointTests` | `Post_StudentOnMathContent_Returns403` | 403 from the policy (`QuestionsValidate` is teacher-only) |
| I8 | `SubjectScopeEndpointTests` | `Get_Anonymous_Returns401` | 401 |
| I9 | `PermissionMatrixPolicyTests` | `AuthorizeAsync_RoleAgainstPolicy_MatchesPrdMatrix` | `[Theory] [MemberData(nameof(Matrix))]`. `Matrix` is a hard-coded `TheoryData<string, string, bool>` of all 54 (policy value, role, allowed) rows copied from the API surface table (not built from `PermissionMatrixPolicies`). It resolves `IAuthorizationService` from a `factory.Services` scope with a `ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"))` and checks `result.Succeeded == allowed`. |
| I10 | `TeacherSubjectsEndpointTests` | `Post_AdminAssignsTeacherSubject_Returns200AndPersists` | 200, body `teacherId`/`subjectId`; a fresh scope shows one live `TeacherSubjects` row |
| I11 | `TeacherSubjectsEndpointTests` | `Post_AlreadyAssigned_Returns409` | 409 `TEACHER_SUBJECT_ALREADY_ASSIGNED` |
| I12 | `TeacherSubjectsEndpointTests` | `Post_UnknownSubject_Returns404` | 404 `SUBJECT_NOT_FOUND` |
| I13 | `TeacherSubjectsEndpointTests` | `Post_TargetIsStudent_Returns400UserNotTeacher` | 400 `USER_NOT_TEACHER` |
| I14 | `TeacherSubjectsEndpointTests` | `Post_TeacherCaller_Returns403` | 403 (policy) and no row persisted |
| I15 | `TeacherSubjectsEndpointTests` | `Post_Anonymous_Returns401` | 401 |
| I16 | `TeacherSubjectsEndpointTests` | `Delete_AdminUnassigns_Returns200AndSoftDeletes` | 200; a fresh scope with `IgnoreQueryFilters()` finds the row with `IsDeleted` true (a documented admin/audit read in test only) |
| I17 | `TeacherSubjectsEndpointTests` | `Delete_NotAssigned_Returns404` | 404 `TEACHER_SUBJECT_NOT_ASSIGNED` |
| I18 | `TeacherSubjectsEndpointTests` | `Post_AfterUnassign_Returns200Reassigns` | assign, unassign, assign again returns 200 (proves the filtered unique index) |
| I19 | `EndpointAuthorizationTests` | `Endpoints_EveryControllerAction_DeclaresPolicyOrAllowAnonymous` | for each `RouteEndpoint` in `factory.Services.GetRequiredService<EndpointDataSource>().Endpoints` that has `ControllerActionDescriptor` metadata: metadata contains `IAllowAnonymous` **or** some `IAuthorizeData` with non-empty `Policy`. Failures list the display names. |
| I20 | `AppDbContextTests` (modify) | `Migrate_FreshDatabase_LeavesNoPendingMigrations` | third migration ends with `_AddTeacherSubjectScoping` |
| W1 | `permissions.test.ts` › `can` | `grants a teacher the validation and reply capabilities` | `can('teacher','questionsValidate')` and `can('teacher','askTeacherReply')` are true |
| W2 | `permissions.test.ts` › `can` | `denies a teacher content management and user management` | `contentManage` and `usersManage` are false for teacher |
| W3 | `permissions.test.ts` › `can` | `denies an admin question validation` | `can('admin','questionsValidate')` is false (PRD: admins cannot approve) |
| W4 | `navConfig.test.ts` › `navByRole` | `grants every configured destination to its role` | for each role, every `navByRole[role].items[i].capability` satisfies `can(role, …)` (drift guard) |
| W5 | `navConfig.test.ts` › `visibleNavItems` | `hides a destination whose capability the role lacks` | for a `RoleNav` built in the test with the teacher items plus `{ key:'users', capability:'usersManage', … }`, `visibleNavItems('teacher', nav)` keys equal `['queue','inbox','stats']`; `overflowItems('teacher', nav)` is empty |
| W6 | `AppShell.test.tsx` (add) | `shows only teacher destinations in the top tabs` | `Main navigation` link names equal `['Review queue','Student questions','My stats']` |

## Definition of done
- [ ] `DefaultCodes` has the 18 constants and values exactly as in the matrix table. `PermissionMatrixPolicies.AddPermissionMatrixPolicies` registers each with exactly those roles via `RequireRole(nameof(UserRole.X))`.
- [ ] `Program.cs` changes only in the `AddAuthorizationBuilder()` line and one `using`. Middleware order is untouched.
- [ ] No `[Authorize(Roles = …)]` and no raw policy strings anywhere in `api/`.
- [ ] `Subject` and `TeacherSubject` extend `AuditEntity` with private ctors and static `Create`. `Unassign` soft-deletes and sets `UpdatedBy` and `UpdationDate`.
- [ ] `TeacherSubject.Create` throws `BusinessRuleViolationCoreException(USER_NOT_TEACHER)` for non-teachers.
- [ ] `User.CreateTeacher` exists and mirrors `CreateAdmin`.
- [ ] `AppDbContext` has both DbSets, named config methods, Restrict FKs, the filtered unique index `"IsDeleted" = false`, and both entities in the global soft-delete filter.
- [ ] Migration `AddTeacherSubjectScoping` only creates tables, FKs and indexes. The snapshot is updated and `HasPendingModelChanges` is false.
- [ ] `ITeacherSubjectRepository.IsAssignedAsync` uses `AnyAsync`. There are no other custom repository methods.
- [ ] `SubjectScopeBehaviour` follows the 5 steps in order, fails closed on a missing role, and is registered in `AddApplication()` (after validation).
- [ ] Both commands implement `IAuditableCommand`. Handlers have no try/catch, call `SaveChangesAsync` exactly once, and `ConfigureAwait(false)` every await.
- [ ] `TeachersController` has 2 actions with `DefaultCodes.UsersManage`, kebab routes, thin bodies, and `CancellationToken` passed through.
- [ ] 7 new resx keys exist in both `Messages.en.resx` and `Messages.ar.resx`.
- [ ] `api/openapi/v1.json` is regenerated by the build. `npm run gen:api` output is committed and produces no diff.
- [ ] The postman collection has a `Teachers` folder with 2 requests.
- [ ] `docs/PRD.md` §16 has the 3 added rows. No other doc changes.
- [ ] Every test T1–T28, I1–I20 and W1–W6 exists with exactly these names and passes. No other existing test is edited (only I20 is modified).
- [ ] `navConfig.ts` items all carry a `capability`. `TopTabs`, `TabBar` and `MorePage` render only `visibleNavItems`. The route guards in `guards.ts` are unchanged.
- [ ] `dotnet build` has zero new warnings. `dotnet test` is green. `dotnet format --verify-no-changes` exits 0.
- [ ] `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check .` and `npx vitest run --coverage` exit 0.
- [ ] The guard grep for `DateTime.Now|UtcNow`, `.Result`, `.Wait()`, `FromSqlRaw` and `async void` prints nothing.
