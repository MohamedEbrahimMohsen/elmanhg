VERDICT: CHANGES_REQUESTED

# Review — Core hygiene: dead code, packages, TimeProvider and audit actor stamping (E21.S2, #305), round 1

## Blocking

### 1. `Elmanhg.Domain` still references `Core.Utilities`, so Domain still pulls the ASP.NET Core shared framework. The report says this was done.
**Where:** `api/Elmanhg.Domain/Elmanhg.Domain.csproj:10` (unchanged, and not in `git status`). The claim is in `02-implementation.md`, Files modified: "`api/Elmanhg.Domain/Elmanhg.Domain.csproj` | Dropped the `Core.Utilities` reference."
**Rule:** plan Goal ("`Elmanhg.Domain` no longer pulls the ASP.NET Core shared framework"), plan D1, plan "Existing code touched" (`Elmanhg.Domain.csproj`: **Remove** the `Core.Utilities` `ProjectReference`), and DoD ("`Elmanhg.Domain` references only `Core.DDD` among core projects"). Reviewer rule: an unverified claim is a finding.
**Problem:** The `ProjectReference` to `..\core-libraries\Core.Utilities\Core.Utilities.csproj` is still there. `Core.Utilities.csproj` carries the `Microsoft.AspNetCore.App` FrameworkReference, and that reference flows transitively. `api/Elmanhg.Domain/obj/project.assets.json:580-593` confirms that Domain still resolves `Core.Utilities` together with `Microsoft.AspNetCore.App`. Test row 30 (`api/Elmanhg.Tests/Domain/DomainAssemblyReferencesTests.cs:10-12`) passes anyway. It reads `GetReferencedAssemblies()`, which lists only assemblies the compiled IL actually uses, so the test cannot see a csproj reference that nothing uses.
**Failure:** Add `using Microsoft.AspNetCore.Http;` and an `HttpContext` parameter to any Domain aggregate and it still builds. The layering goal of D1 is not met, and the report file table is wrong.
**Fix:** Delete line 10 of `Elmanhg.Domain.csproj`. No Domain `.cs` file uses `Core.Utilities` any more (grep is empty), so nothing else changes. Optionally harden row 30 so it fails on the reference itself, for example by asserting that the csproj has no `Core.Utilities` `ProjectReference`.

## Non-blocking
- `api/core-libraries/Core.EntityFrameworkCore/Repositories/Repository.cs:256`: `IsModified` cannot tell "the aggregate did not set it" apart from "the aggregate set it to the value it already had". `QuestionMastery.Record` sets `UpdatedBy = StudentId` (`api/Elmanhg.Domain/Mastery/QuestionMastery.cs:62`), and that is always the existing value, so in the teacher-review flow (`ReviewEssayGradeHandler.cs:37` -> `EssayAttemptRecorder`) the repository overwrites it with the teacher id. Today nothing reads `UpdatedBy` (no query, result or audit diff uses it), and plan D5 chose this rule, so this does not gate. It is worth a sentence in SKILL section 4.2 or a different comparison later.
- `api/core-libraries/Core.EntityFrameworkCore/Context/CoreDbContext.cs:111`: domain events are dispatched inside the context `SaveChangesAsync`, after the repository stamping loop has run. So rows that event handlers add (training records) or modify are never actor-stamped. Nothing regressed (the D9 methods are not called from any handler; I checked all 6 `INotificationHandler`s). This is a limit of "the repository stamps the rest" worth knowing.
- `api/core-libraries/Core.CQRS/Behaviours/ValidationBehaviour.cs:17`: the per-validator context is a sound deviation. Every request type in the app has exactly one `AbstractValidator<T>` (counted), and MS DI does not resolve contravariant validators, so no API response could have depended on the old duplicated codes.
- `Microsoft.Extensions.Options`: core pins 10.0.5, but app projects resolve 10.0.9 transitively (through `Http.Resilience`). There is no conflict and no NU warning. Noted only for the "align versions" goal.

## Verified
- Build: `dotnet build api/Elmanhg.slnx -c Release` gives 0 errors and the same 9 vendored-core nullable warnings. `api/openapi` is unchanged after the build (the CI step would pass).
- Tests: `dotnet test --solution Elmanhg.slnx -c Release --no-build` gives **5489 total, 0 failed** (Docker/Testcontainers).
- Vulnerable-package scan with `--include-transitive`: 0 vulnerable. There are no `2.3.9`, `Azure.Core` or `Version=` entries in core csproj files. `api/core-libraries/Directory.Packages.props` is deleted, and the 21 `PackageVersion`s are in `api/Directory.Packages.props` with the versions the plan gives. The FrameworkReference is on the 8 listed projects. MediatR and FluentValidation are direct where listed. `Core.Auditing` no longer references Identity, `Core.CQRS` no longer references Utilities, and `Core.Identity` no longer references Validation.
- Repository stamping (`Repository.cs:235-264`) matches the plan body exactly. Probe tests rows 3-11 would fail on an always-overwrite or always-clear implementation. A null actor never clears `UpdatedBy`. `CreatedBy`/`UpdatedBy ??=` applies on insert only.
- No save bypasses the repository in production code. The only `SaveChangesAsync` call sites outside repositories are the `AppDbContext`/`CoreDbContext` overrides. The `UserManager` writers (Suspend/Reactivate/Invite/Register/SetTeacherPhone/SeedAdmin/Logout/LoadTestUsers) do not touch the 17 de-stamped aggregates. The one test helper that saves through raw `AppDbContext` (`TouchLessonAsync`) is why `Lesson.MoveTo` keeps its stamp, which is a correct application of D9.
- The 17 removed `UpdationDate` lines each sit after an unconditional change to a mapped scalar on the same entity, or behind a no-op early return (Subject/Unit/LessonObjective `MoveTo`/`Update`, mastery `Record`, Payment x5, Subscription x5, Lesson x3). So the entry is `Modified` and the repository stamps it. No result generator reads `UpdationDate` from these aggregates in memory. The 14 "keep" lines are untouched.
- Audit rows: `CurrentUserService.Role` is `FindFirst(ClaimTypes.Role)`, the same lookup as the old `GetClaim(ClaimTypes.Role)`. `ICurrentUser` is forwarded to the same scoped `ICurrentUserService` instance.
- OTP: the `Verify`/`MarkUsed`/`ConsumeAsync` order is unchanged and only `now` is substituted. `VerifyOTPHandler` and the 4 app handlers read `TimeProvider`. `PhoneCodes`/`PhoneLength` are required with `ValidateOnStart`. They are set by `appsettings.example.json:35` (baked by `api/Dockerfile:18` and loaded by the build-time OpenAPI host at `Program.cs:50`), by `ApiFactory.cs:212-216`, and by the local `appsettings.json` in the main checkout. The CI `ef` commands do not start the host. The 3 validator test suites build `OtpOptions` explicitly.
- `TruncateToMicroseconds` replacements are byte-identical to the deleted private copies. The `Core.DDD.Time` files are identical to the deleted `Core.Utilities.Time` files apart from the namespace.
- `FileSignature.Create` guards zero patterns (`ThrowIfZero`, ParamName `patterns`).
- Deleted code has no remaining reference in `api/`, `docs/` or `.claude/` (including json config).
- Every core `await` has `ConfigureAwait(false)`, multi-line ones included. `DateTimeOffset.UtcNow` remains only in `AuditEntity`, `RequestLoggingMiddleware` and `Core.Notifications`. There is no commented-out code and no issue-number reference in added lines. `CoreDbContext.cs` has no diff. `RuntimeSettingOverrideRepository` and the lane-312 folders are untouched. No migration was added.
- `dotnet format whitespace` on the changed files: every finding is on lines this diff did not touch (pre-existing), which matches the deviation note.
- Docs-sync: `docs/constitution.md` (line 3, section 1.4, section 4) and the SKILL.md deltas 1/5, sections 2, 4.2, 6, 8.2, 8.10, 9 and 11 all agree with the code. `docs/user-administration.md:61` already names the config keys. No divergence found.
- Postman: no endpoint, route or contract changed (the deleted `CoreAuthEndpoints` was never mapped, and OpenAPI is unchanged), so no collection change is needed.
- Every Test-plan row exists with its exact name.

## Test quality
- `RepositoryAuditStampingTests`: strong. Each row pins one branch (keep vs stamp, null actor, unchanged entry).
- `ValidationBehaviourTests`: the gated test really proves sequential execution (it would fail under `Task.WhenAll`). The codes-order test pins the de-duplication.
- `OtpOptionsRegistrationTests`: constrains both the required-at-start rule and the "no appended defaults" binder fix.
- `JwtTokenServiceTests` / `RefreshTokenServiceTests` / OTP expiry rows: constrain the injected clock (they use fixed `Now` boundaries, not wall time).
- `DomainAssemblyReferencesTests`: catches use of Core.Utilities or ASP.NET types, but not the csproj reference (see Blocking 1).
