VERDICT: CHANGES_REQUESTED

# Review — Bootstrap backend solution (.NET 10, DDD/CQRS, PostgreSQL) · Story #54 [E1.S1]

## Blocking

### 1. Constitution §2 still says local config comes from `dotnet user-secrets` and a scaffolder-written `appsettings.json`. The code uses `.env` and has no user-secrets.
**Where:** `api/Elmanhg.Api/Program.cs:20-26` (`Env.TraversePath().Load()` + `AddEnvironmentVariables()`, Development only), `api/Elmanhg.Api/Elmanhg.Api.csproj` (no `UserSecretsId`, per plan D7) and `README.md` "Run the backend locally" (`cp api/Elmanhg.Api/appsettings.example.json ...`). The stale doc is `docs/constitution.md` § 2 "Configuration Pattern", lines 99-104.
**Rule:** `.claude/rules/docs-sync.md`, divergence: a config-policy change that the owning doc (`docs/constitution.md`, "Engineering rules, style, config policy") does not reflect. Review order #8.
**Problem:** This change is the first to decide how local config and secrets reach the API: repo-root `.env` plus `appsettings.example.json` copied by hand, with `AddUserSecrets` deliberately dropped (plan D6/D7). Constitution §2 answers the same question differently. It says "The template writes it and the project scaffolder fills in the per-solution values … Every other environment supplies its own: `dotnet user-secrets` locally". §0.4 of the same file already says `.env`, so the doc now contradicts both itself and the code. Plan D6/D7 resolved the conflict in favour of §0.4, but §2 was not updated in the same change. The implementation report's claim "No `/docs` divergence found" is therefore wrong.
**Failure:** A developer follows constitution §2 and runs `dotnet user-secrets set ConnectionStrings:DbConnectionString ...`. The command fails because the project has no `UserSecretsId`. Even if they add one, `WebApplication.CreateBuilder` does not load it, so the API starts with an empty connection string and `/health` returns 503.
**Fix:** In `docs/constitution.md` §2, replace the scaffolder and `dotnet user-secrets` sentences with the `.env` mechanism: repo-root `.env` (from `.env.example`) loaded in Development, and `appsettings.json` copied from the committed `appsettings.example.json`. Keep the "CI and fresh clones have NO configuration" consequence.

## Non-blocking
- `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs:69`: `persisted.CreationDate.Offset.Should().Be(TimeSpan.Zero)` is always true on read-back, because Npgsql materialises `timestamptz` with offset 0. The test only guards C4 because Npgsql rejects a non-UTC write, and that happens only on a host with a non-zero offset. On the UTC CI runner, reverting C4 to `DateTimeOffset.Now` would not fail it. A stronger test would also assert `template.CreationDate.Offset == TimeSpan.Zero` on the in-memory entity before saving.
- No test exercises C5 (`Repository.SaveChangesAsync` stamping, `api/core-libraries/Core.EntityFrameworkCore/Repositories/Repository.cs:265,271`). T8 saves through `AppDbContext`, not `Repository<T>`. The first story with a repository can cover it.
- `api/Elmanhg.Api/Program.cs:75,80`: the top-level awaits have no `.ConfigureAwait(false)`. This is harmless (top-level, no sync context), the plan specified the text verbatim, and the implementer declared it.
- `api/Elmanhg.Tests/Core/Exceptions/CoreExceptionMiddlewareTests.cs:70` uses `.ConfigureAwait(false)` in a helper, but `ApiFactory.cs:24-26,44-45` does not. The two should be consistent, and the ApiFactory skeleton in the testing convention has none.
- The EF `CollectionWithoutComparer` runtime warning on `Notification.Data` is not suppressed (declared in the report). This is acceptable under F3's "no `OnConfiguring`".

## Verified
- **Build:** I ran `dotnet build api/`: succeeded, 0 errors (incremental run, 0 warnings). A clean Release build of a copy reports 9 warnings, all in vendored `core-libraries`, and zero in `Elmanhg.*` under `TreatWarningsAsErrors`. This matches the report.
- **Tests:** `dotnet test api/` (Docker running): 14/14 passed.
- **CI parity (independent):** In a scratch copy without the gitignored `appsettings.json`, as CI will have it, `dotnet restore` / `build -c Release` / `test -c Release --no-build` passed 14/14. `dotnet ef migrations has-pending-model-changes ... --configuration Release --no-build` with only the CI env var: "No changes have been made to the model since the last migration.", exit 0.
- **Vulnerable packages:** `dotnet list api/ package --vulnerable --include-transitive` reports 20/20 projects with no vulnerable packages.
- **Vendored copy:** `diff -rq` against `Morabh/repos/apis/Core` (excluding bin/obj) shows only `Core.Azure` absent, the two new props files, and exactly the 8 declared files differing. Line diffs match C1–C8:
  - C1 removes the MSSqlServer PackageReference.
  - C2 removes the two usings, the call and the `ConfigureSql` method.
  - C3 removes the property and two classes.
  - C4 and C5 change `Now` to `UtcNow`, twice each.
  - C6 chains `.HasColumnType("jsonb")` after the converter.
  - C7 is byte-identical to the template's `ExceptionMiddleware.cs`.
  - C8 matches the plan's constructor, using and Data expression, and the other two methods are unchanged.
- **DoD greps:** SqlServer/MSSql, `DateTimeOffset.Now`/`DateTime.Now|UtcNow`, `NoWarn`/`UserSecretsId`, and `Migrate()`/`EnsureCreated()` in production code all print nothing.
- **Git hygiene:** `api/Elmanhg.Api/appsettings.json` is ignored (`.gitignore:23`). `.env.example` and `appsettings.example.json` are not ignored. There is no `.env` in the working tree, and no bin/obj among the untracked files.
- **Migration:** exactly one migration (`20260927195417_InitialCreate`). `Notifications.Data` has `type: "jsonb"`, and there are no `nvarchar`/`datetime2` types. There are 20 `timestamp with time zone` columns. `DropTable` appears only in `Down()`.
- **Files match the plan:**
  - A1–A7 match the plan contracts verbatim: global.json, the tools manifest, compose, `.env.example`, CI yml, the Postman collection and environment.
  - B1–B5 match: slnx with 15 + 5 projects and the Solution Items; CPM versions exactly as B3, with both comments; the core opt-out props.
  - D–H match the contracts. `Program.cs` is identical to G2, including order, the `!IsProduction()` docs gate, `/health` `AllowAnonymous`, and `public partial class Program;`. `AppDbContext` is identical to F3, and `ApiFactory` is identical to H2.
  - No extra files were created beyond the declared local-only `appsettings.json`.
- **Tests exist with the exact names:** all 14 plan rows, T1–T14.
- **Declared deviation:** the resx group comment was omitted. It is harmless and honestly reported.
- **Postman:** `postman/elmanhg.postman_collection.json` has `GET {{baseUrl}}/health` with `noauth` and both assertions. The collection uses bearer `{{accessToken}}`. No controllers exist, so nothing is missing or stale.
- **README:** the Folders rows and the run section have the exact commands from the plan.
- **Skill §8:**
  - §8.6: the error serializer uses `JsonSerializerDefaults.Web`, and MVC `AddJsonOptions` is used instead of `ConfigureHttpJsonOptions`.
  - §8.7 / §10 (error leakage): 4xx is logged at Warning, and the stack trace appears only in Development.
  - No other DON'T pattern appears in the diff.

## Test quality
- `HealthEndpointTests`: constrains the behaviour. T1 fails if the DB check, the anonymous mapping or the route breaks. T2 fails if `CoreRequestLoggingMiddleware` is removed.
- `OpenApiEndpointTests`: constrains the behaviour. Both tests fail if the docs mapping or its environment gate is removed or inverted for Testing.
- `AppDbContextTests`: T5, T6 and T7 constrain the behaviour (migration applied, snapshot in sync, jsonb column). T8's Code/Title assertions prove the `LocalizedText` round-trip. Its UTC assertion is weak (see Non-blocking).
- `PipelineCompositionTests`: constrains the behaviour. It fails if `AddCoreCQRS` stops registering `ValidationBehaviour`.
- `CoreExceptionMiddlewareTests`: constrains the behaviour. T10 fails on PascalCase (`GetProperty("code")`). T11 fails if C8 leaks `data` in Production. T12 guards the pass-through. The SUT is real; only the localizer and environment are substituted.
- `ErrorResponseHandlerTests`: constrains the behaviour. Both branches of the C8 conditional are pinned.
- None of the tests are vacuous.
